using Assets.Scripts;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PhoneCamera : MonoBehaviour
{

    private bool isCamera;
    private WebCamTexture cameraTexture;
    private Texture bckgDefault;
    private static Texture2D boxOutlineTexture;
    public GameObject rects;

    public Color colorTag1 = new Color(0.3843137f, 0, 0.9333333f, 0.1f);
    public Color colorTag2 = new Color(0.3843137f, 0, 0.9333333f, 0.1f);
    public Color colorTag3 = new Color(0.3843137f, 0, 0.9333333f, 0.1f);

    public RawImage bckg;
    public AspectRatioFitter fit;
    public Yolov5Detector yolov5Detector;
    public GameObject boxContainer;
    public GameObject boxPrefab;
    public GameObject background;
    public TextMeshProUGUI text;

    public int WINDOW_SIZE = 416;

    private int framesCount = 0;
    private float timeCount = 0.0f;
    private float refreshTime = 1.0f;
    public float fps = 0.0f;


    // Start is called before the first frame update
    void Start()
    {
        bckgDefault = bckg.texture;
        WebCamDevice[] devices = WebCamTexture.devices;

        if (devices.Length == 0)
        {
            isCamera = false;
            return;
        }

        for (int i = 0; i < devices.Length; i++)
        {
            if (!devices[i].isFrontFacing)
                cameraTexture = new WebCamTexture(devices[i].name, 640, 480);
        }

        if (cameraTexture == null)
        {
            if (devices.Length != 0)
                cameraTexture = new WebCamTexture(devices[0].name, 640, 480);
            else
            {
                isCamera = false;
                return;
            }
            
        }

        cameraTexture.Play();
        bckg.texture = cameraTexture;
        float ratio = ((RectTransform)background.transform).rect.width / 640;
        boxContainer.transform.localScale = new Vector2(ratio, ratio);

        isCamera = true;

    }

    // Update is called once per frame
    void Update()
    {
        if (!isCamera)
            return;

        float ratio = (float)cameraTexture.width / (float)cameraTexture.height;
        fit.aspectRatio = ratio;

        float scaleY = cameraTexture.videoVerticallyMirrored ? -1f : 1f;
        bckg.rectTransform.localScale = new Vector3(1f, scaleY, 1f);

        int orient = -cameraTexture.videoRotationAngle;
        bckg.rectTransform.localEulerAngles = new Vector3(0, 0, orient);

        Texture texture = bckg.texture;

        WebCamTexture newTexture = (WebCamTexture)bckg.texture;
        Debug.Log(bckg.texture.width + " " + bckg.texture.height);

        StartCoroutine(yolov5Detector.Detect(newTexture.GetPixels32(), newTexture.width, boxes =>
        {
            Resources.UnloadUnusedAssets();
            
            foreach(Transform child in boxContainer.transform)
            {
                Destroy(child.gameObject);
            }

            for (int i = 0; i < boxes.Count; i++)
            {
                Debug.Log(boxes[i].ToString());
                GameObject newBox = Instantiate(boxPrefab);
                newBox.name = boxes[i].Label + " " + boxes[i].Confidence;
                newBox.GetComponent<Image>().color = boxes[i].Label == "QR" ? colorTag1 : (boxes[i].Label == "ArUco" ? colorTag2 : colorTag3);
                newBox.transform.parent = boxContainer.transform;
                newBox.transform.localPosition = new Vector3(boxes[i].Rect.x - WINDOW_SIZE/2, boxes[i].Rect.y - WINDOW_SIZE/2);
                newBox.transform.localScale = new Vector2(boxes[i].Rect.width/100, boxes[i].Rect.height/100);
            }
        }));

        CountFps();

    }

    private void CountFps()
    {
        if (timeCount < refreshTime)
        {
            timeCount += Time.deltaTime;
            framesCount += 1;
        }
        else
        {
            fps = (float)framesCount / timeCount;
            Debug.Log("FPS: " + fps);
            text.text = "FPS: " + fps;
            framesCount = 0;
            timeCount = 0.0f;
        }
    }
}
