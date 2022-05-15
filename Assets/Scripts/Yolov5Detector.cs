using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Unity.Barracuda;
using UnityEngine;

namespace Assets.Scripts
{
    public class Yolov5Detector : MonoBehaviour, Detector
    {
        public string INPUT_NAME;

        public int IMAGE_SIZE = 416;
        public int CLASS_COUNT = 3;
        public int OUTPUT_ROWS = 10647;
        public float MINIMUM_CONFIDENCE = 0.25f;
        public int OBJECTS_LIMIT = 20;

        public NNModel modelFile;
        public TextAsset labelsFile;

        private string[] labels;
        private IWorker worker;

        private const int IMAGE_MEAN = 0;
        private const float IMAGE_STD = 255.0F;

        public void Start()
        {
            this.labels = Regex.Split(this.labelsFile.text, "\n|\r|\r\n")
                .Where(s => !String.IsNullOrEmpty(s)).ToArray();
            var model = ModelLoader.Load(this.modelFile);
            this.worker = GraphicsWorker.GetWorker(model);
        }

        public IEnumerator Detect(Color32[] picture, int width, System.Action<IList<BoundingBox>> callback)
        {
            using (var tensor = TransformInput(picture, IMAGE_SIZE, IMAGE_SIZE, width))
            {
                var inputs = new Dictionary<string, Tensor>();
                inputs.Add(INPUT_NAME, tensor);
                yield return StartCoroutine(worker.StartManualSchedule(inputs));
                var output = worker.PeekOutput("output");
                var results = ParseYoloV5Output(output, MINIMUM_CONFIDENCE);

                var boxes = FilterBoundingBoxes(results, OBJECTS_LIMIT, MINIMUM_CONFIDENCE);
                callback(boxes);
            }
        }

        private static Tensor TransformInput(Color32[] pic, int width, int height, int requestedWidth)
        {
            float[] floatValues = new float[width * height * 3];

            int beginning = (((pic.Length / requestedWidth) - height) * requestedWidth) / 2;
            int leftOffset = (requestedWidth - width) / 2;
            for (int i = 0; i < height; i++)
            {
                for (int j = 0; j < width; j++)
                {
                    var color = pic[beginning + leftOffset + j];

                    floatValues[(i * width + j) * 3 + 0] = (color.r - IMAGE_MEAN) / IMAGE_STD;
                    floatValues[(i * width + j) * 3 + 1] = (color.g - IMAGE_MEAN) / IMAGE_STD;
                    floatValues[(i * width + j) * 3 + 2] = (color.b - IMAGE_MEAN) / IMAGE_STD;
                }
                beginning += requestedWidth;
            }

            return new Tensor(1, height, width, 3, floatValues);
        }


        private IList<BoundingBox> ParseYoloV5Output(Tensor tensor, float thresholdMax)
        {
            var boxes = new List<BoundingBox>();

            for (int i = 0; i < OUTPUT_ROWS; i++)
            {
                float confidence = GetConfidence(tensor, i);
                if (confidence < thresholdMax)
                    continue;

                BoundingBoxDimensions dimensions = ExtractBoundingBoxDimensionsYolov5(tensor, i);
                (int classIdx, float maxClass) = GetClassIdx(tensor, i);

                float maxScore = confidence * maxClass;

                if (maxScore < thresholdMax)
                    continue;

                boxes.Add(new BoundingBox
                {
                    Dimensions = MapBoundingBoxToCell(dimensions),
                    Confidence = confidence,
                    Label = labels[classIdx]
                });
            }

            return boxes;
        }

        private BoundingBoxDimensions ExtractBoundingBoxDimensionsYolov5(Tensor tensor, int row)
        {
            return new BoundingBoxDimensions
            {
                X = tensor[0, 0, 0, row],
                Y = tensor[0, 0, 1, row],
                Width = tensor[0, 0, 2, row],
                Height = tensor[0, 0, 3, row]
            };
        }

        private float GetConfidence(Tensor tensor, int row)
        {
            float tConf = tensor[0, 0, 4, row];
            return Sigmoid(tConf);
        }

        private ValueTuple<int, float> GetClassIdx(Tensor tensor, int row)
        {
            int classIdx = 0;

            float maxConf = tensor[0, 0, 5, row];

            for (int i = 0; i < CLASS_COUNT; i++)
            {
                if (tensor[0, 0, 5 + i, row] > maxConf)
                {
                    maxConf = tensor[0, 0, 5 + i, row];
                    classIdx = i;
                }
            }
            return (classIdx, maxConf);
        }

        private float Sigmoid(float value)
        {
            var k = (float)Math.Exp(value);

            return k / (1.0f + k);
        }

        private BoundingBoxDimensions MapBoundingBoxToCell(BoundingBoxDimensions boxDimensions)
        {
            return new BoundingBoxDimensions
            {
                X = (boxDimensions.X) * (IMAGE_SIZE / IMAGE_SIZE),
                Y = (boxDimensions.Y) * (IMAGE_SIZE / IMAGE_SIZE),
                Width = boxDimensions.Width * (IMAGE_SIZE / IMAGE_SIZE),
                Height = boxDimensions.Height * (IMAGE_SIZE / IMAGE_SIZE),
            };
        }

        private IList<BoundingBox> FilterBoundingBoxes(IList<BoundingBox> boxes, int limit, float threshold)
        {
            var activeCount = boxes.Count;
            var isActiveBoxes = new bool[boxes.Count];

            for (int i = 0; i < isActiveBoxes.Length; i++)
            {
                isActiveBoxes[i] = true;
            }

            var sortedBoxes = boxes.Select((b, i) => new { Box = b, Index = i })
                    .OrderByDescending(b => b.Box.Confidence)
                    .ToList();

            var results = new List<BoundingBox>();

            for (int i = 0; i < boxes.Count; i++)
            {
                if (isActiveBoxes[i])
                {
                    var boxA = sortedBoxes[i].Box;
                    results.Add(boxA);

                    if (results.Count >= limit)
                        break;

                    for (var j = i + 1; j < boxes.Count; j++)
                    {
                        if (isActiveBoxes[j])
                        {
                            var boxB = sortedBoxes[j].Box;

                            if (IntersectionOverUnion(boxA.Rect, boxB.Rect) > threshold)
                            {
                                isActiveBoxes[j] = false;
                                activeCount--;

                                if (activeCount <= 0)
                                    break;
                            }
                        }
                    }

                    if (activeCount <= 0)
                        break;
                }
            }
            return results;
        }

        private float IntersectionOverUnion(Rect boundingBoxA, Rect boundingBoxB)
        {
            var areaA = boundingBoxA.width * boundingBoxA.height;

            if (areaA <= 0)
                return 0;

            var areaB = boundingBoxB.width * boundingBoxB.height;

            if (areaB <= 0)
                return 0;

            var minX = Math.Max(boundingBoxA.xMin, boundingBoxB.xMin);
            var minY = Math.Max(boundingBoxA.yMin, boundingBoxB.yMin);
            var maxX = Math.Min(boundingBoxA.xMax, boundingBoxB.xMax);
            var maxY = Math.Min(boundingBoxA.yMax, boundingBoxB.yMax);

            var intersectionArea = Math.Max(maxY - minY, 0) * Math.Max(maxX - minX, 0);

            return intersectionArea / (areaA + areaB - intersectionArea);
        }
    }
}
