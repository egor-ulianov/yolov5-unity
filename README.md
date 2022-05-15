# yolov5-unity
The C# project associated with the Unity project contains four files:
1.	Detector.cs

interface	Detector	with	two	provided	methods,
void	Start(), 
a	standard	Unity	method,
IEnumerator Detect (Color32[] picture, int requestedWidth, SystemAction<IList<BoundingBox>>	callback), 
which detects the objects from aa picture represented as an array of Color32.

class	BoundingBoxDimensions 
with	size	and	coordinates		properties

class	BoundingBox 
with	the	following	properties:
BoundingBoxDimensions	Dimensions,
string	Label,
float	Confidence,
Rect	Rect

The reason of this file existence is that in future other kind of detector can be	explored.

2.	GraphicsWorker.cs

Provides	an	only	one	static	method
IWorker	GetWorker	(Model	model),
which returns an instance of IWorker depending on a current platform and GPU availability

3.	PhoneCamera.cs

Contains
class	PhoneCamera:	MonoBehaviour
which gets all needed inputs from Unity, including box colors, background, detector, a prefab for box, a text field for FPS, and provides the following methods:
void Start(),
in which the texture from a camera is got and ratio for detecting frame is	set,
void Update(),
where the input from camera is provided to the Detector, and the detection starts on each frame. Also, bounding boxes are redrawn here, and FPS is counted

4.	Yolov5Detector.cs

Contains
class Yolov5Detector: MonoBehaviour, Detector,
in which the detector’s parameters are handled, such as image size, number of classes, number of the model’s output rows, minimal confidence rate, limit of detectable objects, neural network model file and labels file.

It provides the following methods:
void Start (),
in which labels, a model and a worker are loaded,
IEnumerator Detect (Color32[] picture, int requestedWidth, SystemAction<IList<BoundingBox>> callback), 
as described in the base Detect class

