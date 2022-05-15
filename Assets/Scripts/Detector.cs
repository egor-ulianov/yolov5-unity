using System;
using UnityEngine;
using Unity.Barracuda;
using System.Collections;
using System.Collections.Generic;

public interface Detector
{
    void Start();
    IEnumerator Detect(Color32[] picture, int requestedWidth, System.Action<IList<BoundingBox>> callback);

}

public class BoundingBoxDimensions
{
    public float X { get; set; }
    public float Y { get; set; }
    public float Height { get; set; }
    public float Width { get; set; }
}

public class BoundingBox
{
    public BoundingBoxDimensions Dimensions { get; set; }

    public string Label { get; set; }

    public float Confidence { get; set; }

    public Rect Rect
    {
        get { return new Rect(Dimensions.X, Dimensions.Y, Dimensions.Width, Dimensions.Height); }
    }

    public override string ToString()
    {
        return $"{Label}:{Confidence}, {Dimensions.X}:{Dimensions.Y} - {Dimensions.Width}:{Dimensions.Height}";
    }
}
