using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEditor;
using System.Collections.Generic;
using Object = UnityEngine.Object;
using System;
using System.Reflection;


#if UNITY_EDITOR
public class FastFurGrooming : MonoBehaviour
{
	#region "Defines"
	public bool enableDebugText = false;

	//--------------------------------------------------------------------------------
	// Mesh-related stuff

	public bool showDuplicateMaterialWarning = true;
	public Renderer initialTarget;
	public int finalOverPainting = 4;

	private FastFurMat[] fastFurMats;
	private FastFurMat selectedFastFurMat;
	private Renderer selectedRenderer;
	private Material selectedMaterial;
	private int subMeshIndex;
	private int targetTriangleStartIndex;
	private int targetTriangleEndIndex;
	private int targetVertexStartIndex;
	private int targetVertexEndIndex;
	private MeshCollider myMeshCollider;
	private Mesh bakedMesh;
	private float lastMeshUpdate;

	private GameObject colliderGameObject;

	private int selectedUV = 0;

	private class FastFurMat
	{
		public Renderer renderer { get; set; }
		public Material material { get; set; }
		public int materialIndex { get; set; }
		public string filename { get; set; }
		public string albedoFilename { get; set; }
		public Texture albedoOriginalTexture;
		public Texture furShapeOriginalTexture;
	};


	//--------------------------------------------------------------------------------
	// Materials and textures
	private Material distanceMaterial;
	private Material closestMaterial;
	private Material directionMaterial;
	private Material fillMaterial;
	private Material groomingMaterial;
	private Material cursorMaterial;
	private Material fixEdgesMaterial;
	private Material stencilMaterial;
	private Material combMaterial;

	private Material distanceMaterialOld;
	private Material closestMaterialOld;
	private Material directionMaterialOld;
	private Material fillMaterialOld;
	private Material groomingMaterialOld;
	private Material cursorMaterialOld;
	private Material fixEdgesMaterialOld;
	private Material stencilMaterialOld;
	private Material combMaterialOld;

	private RenderTexture distanceRenderTexture;
	private RenderTexture closestPointRenderTexture;
	private RenderTexture closestPointRenderTextureBuffer;
	private RenderTexture directionRenderTexture;
	private RenderTexture fillRenderTexture;
	private RenderTexture fillRenderTextureBuffer;

	private Texture albedoFallbackTexture;
	private RenderTexture albedoRenderTextureTarget;
	private RenderTexture albedoRenderTextureBuffer;
	private RenderTexture albedoRenderTextureFinal;

	private RenderTexture furShapeRenderTexturePreEdit;
	private RenderTexture furShapeRenderTextureBase;
	private RenderTexture furShapeRenderTextureTarget;
	private RenderTexture furShapeRenderTextureBuffer;
	private RenderTexture furShapeRenderTextureFinal;
	private RenderTexture[] furShapeRenderTextureUndo;

	private RenderTexture combRenderTextureTarget;
	private RenderTexture combRenderTextureBuffer;
	private RenderTexture combRenderTextureFinal;
	private RenderTexture[] combRenderTextureHistory;
	private RenderTexture combRenderTextureBlank;

	private RenderTexture densityTexture;


	//--------------------------------------------------------------------------------
	// Camera control
	private Camera myCamera;
	private GameObject cameraGameObject;

	private int maxFrameRate = 60;

	[Range(1, 100)]
	public int undoBufferSize = 50;

	[Range(0.0f, 25.0f)]
	public float turnSpeed = 2f;
	[Range(0.015625f, 128.0f)]
	public float moveSpeed = 16f;
	private float moveSpeedPrev = 16f;
	private float yaw = 0f;
	private float pitch = 0f;

	public KeyCode forwardKey = KeyCode.W;
	public KeyCode leftKey = KeyCode.A;
	public KeyCode backKey = KeyCode.S;
	public KeyCode rightKey = KeyCode.D;
	public KeyCode upKey = KeyCode.E;
	public KeyCode downKey = KeyCode.Q;


	//--------------------------------------------------------------------------------
	// Command buffers
	private CommandBuffer distanceCommandBuffer;
	private CommandBuffer directionCommandBuffer;
	private CommandBuffer combCommandBuffer;
	private CommandBuffer groomCommandBuffer;


	//--------------------------------------------------------------------------------
	// GUI elements
	[HideInInspector]
	public GameObject groomingGUI;

	private Component[] guiComponents;

	private Toggle lengthToggle;
	private Toggle densityToggle;
	private Toggle combingToggle;

	private Toggle mirrorToggle;
	private Toggle sphericalToggle;
	private Toggle showDataToggle;

	enum BrushMode { Normal, Increase, Decrease, Copy };
	private BrushMode brushMode;
	enum LengthMode { NoMask, UseMask };
	private LengthMode lengthMode;
	enum DensityMode { Absolute, Relative };
	private DensityMode densityMode;
	enum CombingMode { Both, Strength, Direction };
	private CombingMode combingMode;

	private Slider lengthSlider;
	private Slider densitySlider;
	private Slider combingSlider;

	private Slider sizeSlider;
	private Slider strengthSlider;
	private Slider falloffSlider;
	private Slider visibilitySlider;

	private InputField sizeValue;
	private InputField strengthValue;
	private InputField falloffValue;
	private InputField visibilityValue;

	private InputField filenameValue;

	private InputField lengthValue;
	private InputField densityValue;
	private InputField combingValue;

	private Image warningImage;
	private Text warningText;

	private Text helpText;
	private Component helpPopupMove;
	private Component helpPopupSpeed;
	private Component helpPopupCamera;

	private Dropdown materialSelector;
	private Dropdown targetMapSelector;
	private Dropdown targetChannelSelector;
	private Dropdown modeSelector;
	private Dropdown lengthModeSelector;
	private Dropdown densityModeSelector;
	private Dropdown combingModeSelector;

	private Button undoButton;
	private Button redoButton;
	private Button saveButton;

	private Button setAllLengthButton;
	private Button setAllDensityButton;
	private Button setAllCombingButton;


	//--------------------------------------------------------------------------------
	// Active brush settings
	private float brushRadius = 12.5f;
	private float brushFalloff = 0.25f;
	private float brushStrength = 1.0f;
	private float brushVisibility = 0.5f;
	private float furHeight = 0.0f;
	private float furCombing = 0.5f;
	private float furDensity = 0.5f;

	private bool furHeightEnable = true;
	private bool furCombingEnable = false;
	private bool furDensityEnable = false;

	private int furHeightSetAll = 0;
	private int furCombingSetAll = 0;
	private int furDensitySetAll = 0;

	private bool furMirror = true;
	private bool furSphere = true;
	private bool furShowData = false;

	private float baseDensity = 1.0f;

	private float mirrorX;


	//--------------------------------------------------------------------------------
	// Working variables
	private Vector4 mouseHit;
	private Vector2 mouseHitUV;
	private Vector2 mouseHitMirrorUV;
	private Vector4 oldMouseHitWrite = Vector4.negativeInfinity;
	private Vector4 oldMouseHitComb = Vector4.negativeInfinity;

	private int hitIndex;
	private int hits;

	private bool groomingIsStopped = false;
	private bool uploadActive = false;

	private int maxUndoSize = 100;
	private int currentUndo;
	private int highestUndo;
	private bool undoOverflow = false;
	private bool doWrite = false;

	private int validSamples;
	private int maxCombTextures = 10;

	private string filename = "";
	private float fileNotSaved = 0;

	private int activeGroomers = 0;

	private int activeMaterialIndex = 0;
	#endregion

	#region "Getters and Setters"
	//--------------------------------------------------------------------------------
	// Getters and Setters
	public void SetSize(float value) { brushRadius = Mathf.Round(value * 100.0f) * 0.01f; }
	public void SetSize(string value) { float.TryParse(value, out brushRadius); }
	public void SetStrength(float value) { brushStrength = Mathf.Round(value) * 0.01f; }
	public void SetStrength(string value) { float.TryParse(value, out brushStrength); brushStrength *= 0.01f; }
	public void SetFalloff(float value) { brushFalloff = Mathf.Round(value) * 0.01f; }
	public void SetFalloff(string value) { float.TryParse(value, out brushFalloff); brushFalloff *= 0.01f; }
	public void SetVisibility(float value) { brushVisibility = Mathf.Round(value) * 0.01f; }
	public void SetVisibility(string value) { float.TryParse(value, out brushVisibility); brushVisibility *= 0.01f; }
	public void SetHeight(float value) { furHeight = Mathf.Round(value) / 255f; }
	public void SetHeight(string value) { float.TryParse(value, out furHeight); furHeight /= 255f; }
	public void SetCombing(float value) { furCombing = Mathf.Round(value) / 255f; }
	public void SetCombing(string value) { float.TryParse(value, out furCombing); furCombing /= 255f; }
	public void SetDensity(float value) { furDensity = Mathf.Round(value) / 255f; }
	public void SetDensity(string value) { float.TryParse(value, out furDensity); furDensity /= 255f; }

	public void SetHeightEnable(bool value)
	{
		furHeightEnable = value;
		if (value)
		{
			if (densityToggle != null) densityToggle.SetIsOnWithoutNotify(false);
			if (combingToggle != null) combingToggle.SetIsOnWithoutNotify(false);
			furDensityEnable = false;
			furCombingEnable = false;
		}
	}
	public void SetCombingEnable(bool value)
	{
		furCombingEnable = value;
		if (value)
		{
			if (lengthToggle != null) lengthToggle.SetIsOnWithoutNotify(false);
			if (densityToggle != null) densityToggle.SetIsOnWithoutNotify(false);
			furHeightEnable = false;
			furDensityEnable = false;
		}

	}
	public void SetDensityEnable(bool value)
	{
		furDensityEnable = value;
		if (value)
		{
			if (lengthToggle != null) lengthToggle.SetIsOnWithoutNotify(false);
			if (combingToggle != null) combingToggle.SetIsOnWithoutNotify(false);
			furHeightEnable = false;
			furCombingEnable = false;
		}
	}
	public void SetHeightSetAll() { furHeightSetAll = 2; }
	public void SetCombingSetAll() { furCombingSetAll = 2; }
	public void SetDensitySetAll() { furDensitySetAll = 2; }
	public void SetMirror(bool value) { furMirror = value; }
	public void SetSpherical(bool value) { furSphere = value; }
	public void SetShowData(bool value) { furShowData = value; }

	public void SetFilename(string value) { filename = value; }

	public void SetBrushMode(int value) { brushMode = (BrushMode)value; }
	public void SetLengthMode(int value) { lengthMode = (LengthMode)value; }
	public void SetDensityMode(int value) { densityMode = (DensityMode)value; }
	public void SetCombingMode(int value) { combingMode = (CombingMode)value; }
	#endregion

	#region "Startup and Shutdown"
	//////////////////////////////////////////////////////////////////////////////////
	//--------------------------------------------------------------------------------
	// Start is called before the first frame update
	//--------------------------------------------------------------------------------
	//////////////////////////////////////////////////////////////////////////////////

	void Start()
	{
		new WaitForSeconds(.5f);

		if (enableDebugText) Debug.Log("[WFFS] Start() called at frame " + Time.frameCount);


		// If this is an old version of the Fur Grooming prefab, delete everything attached to it
		var guiComps = gameObject.GetComponentsInChildren(typeof(Component), true);
		foreach (var guiComp in guiComps)
		{
			if (guiComp.gameObject != gameObject)
			{
				if (enableDebugText) Debug.Log("[WFFS] Destroying: " + guiComp.name);
				Destroy(guiComp.gameObject);
			}
		}

		maxUndoSize = undoBufferSize;


		// Disable any animation controllers (since the error message telling people to
		// disable their animation controller just results in them sending screenshots
		// of the error message, lol...)
		Animator[] animators = GameObject.FindObjectsOfType<Animator>();
		foreach (Animator animator in animators)
		{
			animator.enabled = false;
		}


		// Check if VR Chat is trying to publish
		MonoBehaviour[] scripts = GameObject.FindObjectsOfType<MonoBehaviour>();
		foreach (MonoBehaviour script in scripts)
		{
			if (script.ToString().Contains("VRCSDK"))
			{
				if (enableDebugText) Debug.Log("[WFFS] Upload detected at frame " + Time.frameCount);
				uploadActive = true;
			}
		}


		if (!uploadActive)
		{
			// If initialization succeeds, this will be changed to 'false'
			groomingIsStopped = true;

			// Disable any physbones or dynamic bones

			MonoBehaviour[] scripts2 = GameObject.FindObjectsOfType<MonoBehaviour>();
			foreach (MonoBehaviour script in scripts2)
			{
				if (script.ToString().Contains("PhysBone") || script.ToString().Contains("DynamicBone"))
				{
					script.enabled = false;
				}
			}

			// Prep the GUI
			if (!initializeGUI()) return;

			// Prep the materials
			if (!initializeGroomingMats()) return;
			int startingIndex = initializeMats();
			if (startingIndex < 0) return;
			if (!loadRenderer(fastFurMats[startingIndex])) return;
			if (!loadMaterial(fastFurMats[startingIndex])) return;
			activeMaterialIndex = materialSelector.value;
			lastMeshUpdate = Time.realtimeSinceStartup;

			// Prep the camera
			if (!initializeCamera()) return;

			groomingIsStopped = false;
		}

		if (enableDebugText) Debug.Log("[WFFS] Finished Start() at frame " + Time.frameCount);
	}



	//////////////////////////////////////////////////////////////////////////////////
	//--------------------------------------------------------------------------------
	// OnApplicationQuit is called when the user exits
	//--------------------------------------------------------------------------------
	//////////////////////////////////////////////////////////////////////////////////

	void OnApplicationQuit()
	{
		if (enableDebugText) Debug.Log("[WFFS] OnApplicationQuit() called at frame " + Time.frameCount);
	}
	#endregion

	#region "Initialization"
	//--------------------------------------------------------------------------------
	// Initialize GUI

	private bool initializeGUI()
	{
		if (enableDebugText) Debug.Log("[WFFS] initializeGUI() called at frame " + Time.frameCount);

		try
		{
			// Create the GUI
			groomingGUI = Instantiate((GameObject) Resources.Load("WFFS Groom GUI Prefab v4.1.2"));

			guiComponents = groomingGUI.GetComponentsInChildren(typeof(Component), true);
			foreach (Component c in guiComponents)
			{
				if (c is MonoBehaviour) ((MonoBehaviour)c).enabled = true;
				if (!c.name.Equals("FurGroom Error Message") && !c.name.Equals("FurGroom Warning Message") && !c.name.Equals("Template")) c.gameObject.SetActive(true);

				if (c.name.Equals("FurGroom Warning Message")) warningImage = (Image)c.GetComponentInChildren(typeof(Image));
				if (c.name.Equals("FurGroom Warning Message Text")) warningText = (Text)c.GetComponentInChildren(typeof(Text));
				if (c.name.Equals("FurGroomHelpText")) helpText = (Text)c.GetComponentInChildren(typeof(Text));

				if (c.name.Equals("LengthSlider")) lengthSlider = (Slider)c.GetComponentInChildren(typeof(Slider));
				if (c.name.Equals("DensitySlider")) densitySlider = (Slider)c.GetComponentInChildren(typeof(Slider));
				if (c.name.Equals("CombingSlider")) combingSlider = (Slider)c.GetComponentInChildren(typeof(Slider));
				if (c.name.Equals("SizeSlider")) sizeSlider = (Slider)c.GetComponentInChildren(typeof(Slider));
				if (c.name.Equals("StrengthSlider")) strengthSlider = (Slider)c.GetComponentInChildren(typeof(Slider));
				if (c.name.Equals("FalloffSlider")) falloffSlider = (Slider)c.GetComponentInChildren(typeof(Slider));
				if (c.name.Equals("VisibilitySlider")) visibilitySlider = (Slider)c.GetComponentInChildren(typeof(Slider));

				if (c.name.Equals("SizeValue")) sizeValue = (InputField)c.GetComponentInChildren(typeof(InputField));
				if (c.name.Equals("StrengthValue")) strengthValue = (InputField)c.GetComponentInChildren(typeof(InputField));
				if (c.name.Equals("FalloffValue")) falloffValue = (InputField)c.GetComponentInChildren(typeof(InputField));
				if (c.name.Equals("VisibilityValue")) visibilityValue = (InputField)c.GetComponentInChildren(typeof(InputField));
				if (c.name.Equals("LengthValue")) lengthValue = (InputField)c.GetComponentInChildren(typeof(InputField));
				if (c.name.Equals("DensityValue")) densityValue = (InputField)c.GetComponentInChildren(typeof(InputField));
				if (c.name.Equals("CombingValue")) combingValue = (InputField)c.GetComponentInChildren(typeof(InputField));
				if (c.name.Equals("FileName")) filenameValue = (InputField)c.GetComponentInChildren(typeof(InputField));

				if (c.name.Equals("ToggleLength")) lengthToggle = (Toggle)c.GetComponentInChildren(typeof(Toggle));
				if (c.name.Equals("ToggleDensity")) densityToggle = (Toggle)c.GetComponentInChildren(typeof(Toggle));
				if (c.name.Equals("ToggleCombing")) combingToggle = (Toggle)c.GetComponentInChildren(typeof(Toggle));

				if (c.name.Equals("ToggleMirror")) mirrorToggle = (Toggle)c.GetComponentInChildren(typeof(Toggle));
				if (c.name.Equals("ToggleSpherical")) sphericalToggle = (Toggle)c.GetComponentInChildren(typeof(Toggle));
				if (c.name.Equals("ToggleShowData")) showDataToggle = (Toggle)c.GetComponentInChildren(typeof(Toggle));

				if (c.name.Equals("Undo")) undoButton = (Button)c.GetComponentInChildren(typeof(Button));
				if (c.name.Equals("Redo")) redoButton = (Button)c.GetComponentInChildren(typeof(Button));
				if (c.name.Equals("Save")) saveButton = (Button)c.GetComponentInChildren(typeof(Button));

				if (c.name.Equals("SetAllLength")) setAllLengthButton = (Button)c.GetComponentInChildren(typeof(Button));
				if (c.name.Equals("SetAllDensity")) setAllDensityButton = (Button)c.GetComponentInChildren(typeof(Button));
				if (c.name.Equals("SetAllCombing")) setAllCombingButton = (Button)c.GetComponentInChildren(typeof(Button));

				if (c.name.Equals("MaterialSelection")) materialSelector = (Dropdown)c.GetComponentInChildren(typeof(Dropdown));
				if (c.name.Equals("TargetMapSelection")) targetMapSelector = (Dropdown)c.GetComponentInChildren(typeof(Dropdown));
				if (c.name.Equals("TargetMapChannel")) targetChannelSelector = (Dropdown)c.GetComponentInChildren(typeof(Dropdown));
				if (c.name.Equals("MasterMode")) modeSelector = (Dropdown)c.GetComponentInChildren(typeof(Dropdown));
				if (c.name.Equals("ModeLength")) lengthModeSelector = (Dropdown)c.GetComponentInChildren(typeof(Dropdown));
				if (c.name.Equals("ModeDensity")) densityModeSelector = (Dropdown)c.GetComponentInChildren(typeof(Dropdown));
				if (c.name.Equals("ModeCombing")) combingModeSelector = (Dropdown)c.GetComponentInChildren(typeof(Dropdown));

				if (c.name.Equals("HelpPopupMove")) helpPopupMove = c;
				if (c.name.Equals("HelpPopupSpeed")) helpPopupSpeed = c;
				if (c.name.Equals("HelpPopupCamera")) helpPopupCamera = c;
			}

			if (helpText == null ||
				sizeSlider == null || strengthSlider == null || falloffSlider == null || visibilitySlider == null ||
				lengthSlider == null || densitySlider == null || combingSlider == null ||
				sizeValue == null || strengthValue == null || falloffValue == null || visibilityValue == null ||
				lengthValue == null || densityValue == null || combingValue == null ||
				filenameValue == null ||
				lengthToggle == null || densityToggle == null || combingToggle == null ||
				mirrorToggle == null || sphericalToggle == null || showDataToggle == null ||
				undoButton == null || redoButton == null || saveButton == null ||
				setAllLengthButton == null || setAllDensityButton == null || setAllCombingButton == null ||
				materialSelector == null || modeSelector == null || lengthModeSelector == null || densityModeSelector == null || combingModeSelector == null ||
				warningImage == null || warningText == null)
			{
				errorMessage("The Fur Grooming GUI is missing some elements. Please upgrade the Fur Grooming prefab to the newest version.");
				return false;
			}
		}

		catch (System.Exception e)
		{
			Debug.LogError(e.ToString());
			errorMessage("Unable to initialize GUI: " + e);
			return false;
		}

		return true;
	}



	//--------------------------------------------------------------------------------
	// For some reason, the GUI method calls can get cleared. I don't know what causes this,
	// but if it happens it breaks the GUI. The workaround is to re-build all of the GUI method
	// calls once per second, rather than relying on them to be correct in the prefab. This
	// is frankly pretty dumb, but I would rather not waste several weeks trying to figure out
	// why Unity sometimes deletes the method calls.
	private void configureGUI()
	{
		// Sliders
		sizeSlider.onValueChanged.RemoveAllListeners();
		sizeSlider.onValueChanged.AddListener(new UnityEngine.Events.UnityAction<float>(SetSize));
		strengthSlider.onValueChanged.RemoveAllListeners();
		strengthSlider.onValueChanged.AddListener(new UnityEngine.Events.UnityAction<float>(SetStrength));
		falloffSlider.onValueChanged.RemoveAllListeners();
		falloffSlider.onValueChanged.AddListener(new UnityEngine.Events.UnityAction<float>(SetFalloff));
		visibilitySlider.onValueChanged.RemoveAllListeners();
		visibilitySlider.onValueChanged.AddListener(new UnityEngine.Events.UnityAction<float>(SetVisibility));

		lengthSlider.onValueChanged.RemoveAllListeners();
		lengthSlider.onValueChanged.AddListener(new UnityEngine.Events.UnityAction<float>(SetHeight));
		densitySlider.onValueChanged.RemoveAllListeners();
		densitySlider.onValueChanged.AddListener(new UnityEngine.Events.UnityAction<float>(SetDensity));
		combingSlider.onValueChanged.RemoveAllListeners();
		combingSlider.onValueChanged.AddListener(new UnityEngine.Events.UnityAction<float>(SetCombing));

		// Values (text boxes)
		sizeValue.onValueChanged.RemoveAllListeners();
		sizeValue.onValueChanged.AddListener(new UnityEngine.Events.UnityAction<string>(SetSize));
		strengthValue.onValueChanged.RemoveAllListeners();
		strengthValue.onValueChanged.AddListener(new UnityEngine.Events.UnityAction<string>(SetStrength));
		falloffValue.onValueChanged.RemoveAllListeners();
		falloffValue.onValueChanged.AddListener(new UnityEngine.Events.UnityAction<string>(SetFalloff));
		visibilityValue.onValueChanged.RemoveAllListeners();
		visibilityValue.onValueChanged.AddListener(new UnityEngine.Events.UnityAction<string>(SetVisibility));

		lengthValue.onValueChanged.RemoveAllListeners();
		lengthValue.onValueChanged.AddListener(new UnityEngine.Events.UnityAction<string>(SetHeight));
		densityValue.onValueChanged.RemoveAllListeners();
		densityValue.onValueChanged.AddListener(new UnityEngine.Events.UnityAction<string>(SetDensity));
		combingValue.onValueChanged.RemoveAllListeners();
		combingValue.onValueChanged.AddListener(new UnityEngine.Events.UnityAction<string>(SetCombing));

		filenameValue.onValueChanged.RemoveAllListeners();
		filenameValue.onValueChanged.AddListener(new UnityEngine.Events.UnityAction<string>(SetFilename));

		// Toggles
		lengthToggle.onValueChanged.RemoveAllListeners();
		lengthToggle.onValueChanged.AddListener(new UnityEngine.Events.UnityAction<bool>(SetHeightEnable));
		densityToggle.onValueChanged.RemoveAllListeners();
		densityToggle.onValueChanged.AddListener(new UnityEngine.Events.UnityAction<bool>(SetDensityEnable));
		combingToggle.onValueChanged.RemoveAllListeners();
		combingToggle.onValueChanged.AddListener(new UnityEngine.Events.UnityAction<bool>(SetCombingEnable));

		mirrorToggle.onValueChanged.RemoveAllListeners();
		mirrorToggle.onValueChanged.AddListener(new UnityEngine.Events.UnityAction<bool>(SetMirror));
		sphericalToggle.onValueChanged.RemoveAllListeners();
		sphericalToggle.onValueChanged.AddListener(new UnityEngine.Events.UnityAction<bool>(SetSpherical));
		showDataToggle.onValueChanged.RemoveAllListeners();
		showDataToggle.onValueChanged.AddListener(new UnityEngine.Events.UnityAction<bool>(SetShowData));

		// Buttons
		undoButton.onClick.RemoveAllListeners();
		undoButton.onClick.AddListener(new UnityEngine.Events.UnityAction(doUndo));
		redoButton.onClick.RemoveAllListeners();
		redoButton.onClick.AddListener(new UnityEngine.Events.UnityAction(doRedo));
		saveButton.onClick.RemoveAllListeners();
		saveButton.onClick.AddListener(new UnityEngine.Events.UnityAction(doSave));

		setAllLengthButton.onClick.RemoveAllListeners();
		setAllLengthButton.onClick.AddListener(new UnityEngine.Events.UnityAction(SetHeightSetAll));
		setAllDensityButton.onClick.RemoveAllListeners();
		setAllDensityButton.onClick.AddListener(new UnityEngine.Events.UnityAction(SetDensitySetAll));
		setAllCombingButton.onClick.RemoveAllListeners();
		setAllCombingButton.onClick.AddListener(new UnityEngine.Events.UnityAction(SetCombingSetAll));

		// Dropdown lists
		modeSelector.onValueChanged.RemoveAllListeners();
		modeSelector.onValueChanged.AddListener(new UnityEngine.Events.UnityAction<int>(SetBrushMode));
		lengthModeSelector.onValueChanged.RemoveAllListeners();
		lengthModeSelector.onValueChanged.AddListener(new UnityEngine.Events.UnityAction<int>(SetLengthMode));
		densityModeSelector.onValueChanged.RemoveAllListeners();
		densityModeSelector.onValueChanged.AddListener(new UnityEngine.Events.UnityAction<int>(SetDensityMode));
		combingModeSelector.onValueChanged.RemoveAllListeners();
		combingModeSelector.onValueChanged.AddListener(new UnityEngine.Events.UnityAction<int>(SetCombingMode));
	}



	//--------------------------------------------------------------------------------
	// Initialize camera
	private bool initializeCamera()
	{
		if (enableDebugText) Debug.Log("[WFFS] initializeCamera() called at frame " + Time.frameCount);

		try
		{
			Camera[] cameras = Camera.allCameras;
			foreach (Camera camera in cameras) camera.enabled = false;

			cameraGameObject = new GameObject("Fur Grooming Camera");
			myCamera = cameraGameObject.AddComponent<Camera>();
			myCamera.nearClipPlane = 0.01f;
			myCamera.enabled = true;

			// Position the camera to be facing the first renderer
			myCamera.transform.SetPositionAndRotation(selectedRenderer.transform.position, Quaternion.identity);
			if (initialTarget != null)
			{
				myCamera.transform.SetPositionAndRotation(initialTarget.transform.position, Quaternion.identity);
			}
			myCamera.transform.Translate(0, 1, 1);
			myCamera.transform.Rotate(0, 180, 0);
			myCamera.nearClipPlane = 0.01f;
			yaw = myCamera.transform.eulerAngles.y;
			pitch = myCamera.transform.eulerAngles.x;
		}

		catch (System.Exception e)
		{
			Debug.LogError(e.ToString());
			errorMessage("Unable to initialize camera: " + e);
			return false;
		}

		return true;
	}



	//--------------------------------------------------------------------------------
	// Initialize grooming materials
	private bool initializeGroomingMats()
	{
		if (enableDebugText) Debug.Log("[WFFS] initializeGroomingMats() called at frame " + Time.frameCount);

		try
		{
			// Create the materials used for grooming
			fillMaterial = new Material(Shader.Find("Warren's Fast Fur/Internal Utilities/Fill"));
			distanceMaterial = new Material(Shader.Find("Warren's Fast Fur/Internal Utilities/Distance"));
			closestMaterial = new Material(Shader.Find("Warren's Fast Fur/Internal Utilities/Closest"));
			directionMaterial = new Material(Shader.Find("Warren's Fast Fur/Internal Utilities/Direction"));
			combMaterial = new Material(Shader.Find("Warren's Fast Fur/Internal Utilities/Comb"));
			stencilMaterial = new Material(Shader.Find("Warren's Fast Fur/Internal Utilities/Stencil"));
			cursorMaterial = new Material(Shader.Find("Warren's Fast Fur/Internal Utilities/Cursor"));
			groomingMaterial = new Material(Shader.Find("Warren's Fast Fur/Internal Utilities/Groomer"));
			fixEdgesMaterial = new Material(Shader.Find("Warren's Fast Fur/Internal Utilities/Fix Edges"));

			if (fillMaterial == null || distanceMaterial == null || closestMaterial == null || directionMaterial == null || combMaterial == null
				|| stencilMaterial == null || cursorMaterial == null || groomingMaterial == null || fixEdgesMaterial == null)
			{
				errorMessage("The Fur Grooming GUI can't find required shaders. Perhaps the shader package needs to be re-installed?");
				return false;
			}

		}

		catch (System.Exception e)
		{
			Debug.LogError(e.ToString());
			errorMessage("Unable to initialize grooming materials: " + e);
			return false;
		}

		return true;
	}



	//--------------------------------------------------------------------------------
	// Initialize materials
	private int initializeMats()
	{
		if (enableDebugText) Debug.Log("[WFFS] initializeMats() called at frame " + Time.frameCount);

		try
		{
			// Generate a list of all of the Fast Fur materials in the project, along with their parent renderers and their filenames
			List<FastFurMat> fastFurMatsList = new List<FastFurMat>();
			List<Dropdown.OptionData> materialNames = new List<Dropdown.OptionData>();

			int index = 0;
			int startingIndex = -1;
			Renderer[] allRenderers = Object.FindObjectsOfType<Renderer>();
			for (int z = 0; z < allRenderers.Length; z++)
			{
				Renderer myRenderer = allRenderers[z];
				if (!myRenderer.enabled) continue;

				Material[] materials = myRenderer.materials;
				for (int x = 0; x < materials.Length; x++)
				{
					string shaderName = materials[x].shader.name;
					if (shaderName.StartsWith("Warren's Fast Fur/Fast Fur") || shaderName.StartsWith("Warren's Fast Fur/Older Versions/Fast Fur") || shaderName.StartsWith("Warren's Fast Fur/Special Variants"))
					{
						FastFurMat newFastFurMat = new FastFurMat();
						lock (newFastFurMat)
						{
							newFastFurMat.renderer = myRenderer;
							newFastFurMat.material = materials[x];
							newFastFurMat.materialIndex = x;

							try
							{
								newFastFurMat.filename = AssetDatabase.GetAssetPath(materials[x].GetTexture("_FurShapeMap").GetInstanceID());
								newFastFurMat.furShapeOriginalTexture = AssetDatabase.LoadAssetAtPath<Texture>(newFastFurMat.filename);
							}
							catch (System.Exception)
							{
								errorMessage("The '" + newFastFurMat.material.name + "' material does not have a Fur Shape Data Map texture. You must create the texture first before you can edit it.");
								return -1;
							}

							try
							{
								newFastFurMat.albedoOriginalTexture = materials[x].GetTexture("_MainTex");
								newFastFurMat.albedoFilename = AssetDatabase.GetAssetPath(materials[x].GetTexture("_MainTex").GetInstanceID());
							}
							catch (System.Exception)
							{
								if (enableDebugText) Debug.Log("[WFFS] failed to get albedo texture at frame " + Time.frameCount);
								newFastFurMat.albedoOriginalTexture = null;
								newFastFurMat.albedoFilename = null;
							}

							fastFurMatsList.Add(newFastFurMat);
						}

						Dropdown.OptionData dropDownItem = new Dropdown.OptionData();
						dropDownItem.text = myRenderer.gameObject.name + " - ";
						dropDownItem.text += materials[x].name.Replace(" (Instance)", "");
						materialNames.Add(dropDownItem);
						if (initialTarget != null)
						{
							if (myRenderer.Equals(initialTarget) && startingIndex < 0)
							{
								startingIndex = index;
							}
						}
						index++;
					}
				}
			}

			AssetDatabase.Refresh();

			if (fastFurMatsList.Count == 0)
			{
				errorMessage("Unable to locate any Fast Fur Materials. ");
				return (-1);
			}

			if (startingIndex < 0) startingIndex = 0;

			fastFurMats = fastFurMatsList.ToArray();

			materialSelector.options = materialNames;
			materialSelector.value = startingIndex;

			return (startingIndex);
		}

		catch (System.Exception e)
		{
			Debug.LogError(e.ToString());
			errorMessage("Unable to initialize materials: " + e);
		}

		return -1;
	}
	#endregion

	#region "Load Renderer"
	//--------------------------------------------------------------------------------
	// Load renderer
	private bool loadRenderer(FastFurMat myFastFurMat)
	{
		if (enableDebugText) Debug.Log("[WFFS] loadRenderer() called at frame " + Time.frameCount);

		Renderer renderer = myFastFurMat.renderer;

		try
		{
			if (renderer is SkinnedMeshRenderer || renderer is MeshRenderer)
			{
				// Create a mesh collider, attach it to a new game object, then bake it from the target mesh
				if (colliderGameObject == null)
				{
					colliderGameObject = new GameObject("Fur Grooming Mesh Collider");
					myMeshCollider = colliderGameObject.AddComponent<MeshCollider>();
				}

				if (renderer is SkinnedMeshRenderer)
				{
					bakedMesh = new Mesh();
					((SkinnedMeshRenderer)renderer).BakeMesh(bakedMesh);
					myMeshCollider.sharedMesh = bakedMesh;

				}

				else if (renderer is MeshRenderer)
				{
					MeshFilter meshFilter = renderer.GetComponent<MeshFilter>();
					myMeshCollider.sharedMesh = meshFilter.sharedMesh;
				}

				colliderGameObject.transform.SetPositionAndRotation(renderer.transform.position, renderer.transform.rotation);
				mirrorX = renderer.transform.position.x;

				lastMeshUpdate = Time.realtimeSinceStartup;

				addCommandBuffers();
			}

			selectedRenderer = renderer;


		}

		catch (System.Exception e)
		{
			Debug.LogError(e.ToString());
			errorMessage("The Target Renderer failed to load: " + e.ToString());
			return false;
		}

		return true;
	}
	#endregion

	#region "Load Material"
	//--------------------------------------------------------------------------------
	// Load material
	private bool loadMaterial(FastFurMat myFastFurMat)
	{
		if (enableDebugText) Debug.Log("[WFFS] loadMaterial() called at frame " + Time.frameCount);

		try
		{
			Material material = myFastFurMat.material;

			if (myFastFurMat != selectedFastFurMat)
			{
				string warningText = "";
				for (int x = 0; x < fastFurMats.Length; x++)
				{
					if (fastFurMats[x] != myFastFurMat)
					{
						// Using a material in multiple places seems to be a common problem that people are having trouble figuring out.
						// I've added a bunch of code to hopefully make it easy for people to determine where the multiple copies are.
						if (fastFurMats[x].filename.Equals(myFastFurMat.filename) && showDuplicateMaterialWarning)
						{
							warningText = "The target 'Fur Shape Data Map' texture is being used in multiple places. It is being used on the '";
							warningText += myFastFurMat.renderer.name + "' mesh in material " + myFastFurMat.materialIndex + ": '" + myFastFurMat.material.name.Replace(" (Instance)", "") + "', and also on the '";
							warningText += fastFurMats[x].renderer.name + "' mesh in material " + x + ": '" + fastFurMats[x].material.name.Replace(" (Instance)", "") + "'. ";
							warningText += "When you save, YOU WILL BE SAVING TO BOTH LOCATIONS! Edits might overwrite each other, and may also cause edge corruption if the overpainting is too large. To prevent this, you should make a separate copy of the material with its own separate 'Fur Shape Data Map' texture.";
							warningMessage(warningText);
						}
					}
				}
				warningMessage(warningText);

				if (myFastFurMat.renderer is SkinnedMeshRenderer)
				{
					targetTriangleStartIndex = ((SkinnedMeshRenderer)myFastFurMat.renderer).sharedMesh.GetSubMesh(myFastFurMat.materialIndex).indexStart;
					targetTriangleEndIndex = targetTriangleStartIndex + ((SkinnedMeshRenderer)myFastFurMat.renderer).sharedMesh.GetSubMesh(myFastFurMat.materialIndex).indexCount;
					targetVertexStartIndex = ((SkinnedMeshRenderer)myFastFurMat.renderer).sharedMesh.GetSubMesh(myFastFurMat.materialIndex).baseVertex;
					targetVertexEndIndex = targetVertexStartIndex + ((SkinnedMeshRenderer)myFastFurMat.renderer).sharedMesh.GetSubMesh(myFastFurMat.materialIndex).vertexCount;
				}
				else if (myFastFurMat.renderer is MeshRenderer)
				{
					MeshFilter meshFilter = myFastFurMat.renderer.GetComponent<MeshFilter>();
					targetTriangleStartIndex = meshFilter.sharedMesh.GetSubMesh(myFastFurMat.materialIndex).indexStart;
					targetTriangleEndIndex = targetTriangleStartIndex + meshFilter.sharedMesh.GetSubMesh(myFastFurMat.materialIndex).indexCount;
					targetVertexStartIndex = meshFilter.sharedMesh.GetSubMesh(myFastFurMat.materialIndex).baseVertex;
					targetVertexEndIndex = targetVertexStartIndex + meshFilter.sharedMesh.GetSubMesh(myFastFurMat.materialIndex).vertexCount;
				}
				else
				{
					errorMessage("The Fur Grooming currently only supports 'Mesh Renderers' and 'Skinned Mesh Renderers'. Since you appear to be editing something else, let Warren know on the Discord server and he'll try to add support for what you are trying to edit.");
					return false;
				}


				if (material.GetTexture("_FurShapeMap") == null)
				{
					errorMessage("The Target Material does not have a Fur Shape Data Map texture. You must create the texture first before you can edit the material.");
					return false;
				}
				if (material.GetTexture("_HairMap") == null)
				{
					errorMessage("The Target Material does not have a Hair Pattern Map texture. You must create the texture first before you can edit the material.");
					return false;
				}


				// Restore default settings to the de-selected material
				if (selectedFastFurMat != null)
				{
					selectedFastFurMat.material.SetTexture("_MainTex", selectedFastFurMat.albedoOriginalTexture);
					selectedFastFurMat.material.SetTexture("_FurShapeMap", selectedFastFurMat.furShapeOriginalTexture);
				}

				selectedFastFurMat = myFastFurMat;
				selectedMaterial = material;
				subMeshIndex = myFastFurMat.materialIndex;
			}


			// Re-load the 'furShapeOriginalTexture', since it might have changed if the material is being shared
			myFastFurMat.furShapeOriginalTexture = AssetDatabase.LoadAssetAtPath<Texture>(myFastFurMat.filename);
			myFastFurMat.albedoOriginalTexture = AssetDatabase.LoadAssetAtPath<Texture>(myFastFurMat.albedoFilename);


			// Set the filename
			foreach (Component c in guiComponents)
			{
				if (c.name.Equals("FileName"))
				{
					((InputField)c.GetComponentInChildren(typeof(InputField))).text = myFastFurMat.filename;
					filename = myFastFurMat.filename;
				}
			}

			// Create the albedo textures that will be used to display the cursor

			Texture mainTex = material.GetTexture("_MainTex");
			if (mainTex == null)
			{
				albedoFallbackTexture = new Texture2D(1024, 1024, TextureFormat.ARGB32, false, true);
				var newPixels = ((Texture2D)albedoFallbackTexture).GetPixels();
				var newColour = new Color(1f, 1f, 1f, 1f);
				for (int x = 0; x < newPixels.Length; x++) newPixels[x] = newColour;

				((Texture2D)albedoFallbackTexture).SetPixels(newPixels);
				((Texture2D)albedoFallbackTexture).Apply();
				mainTex = albedoFallbackTexture;
			}
			albedoRenderTextureTarget = new RenderTexture(mainTex.width, mainTex.height, 0, RenderTextureFormat.ARGB32);
			albedoRenderTextureTarget.filterMode = FilterMode.Point;
			albedoRenderTextureBuffer = new RenderTexture(mainTex.width, mainTex.height, 0, RenderTextureFormat.ARGB32);
			albedoRenderTextureBuffer.filterMode = FilterMode.Point;
			albedoRenderTextureFinal = new RenderTexture(mainTex.width, mainTex.height, 0, RenderTextureFormat.ARGB32);
			material.SetTexture("_MainTex", albedoRenderTextureFinal);


			//String furTexPath = AssetDatabase.GetAssetPath(material.GetTexture("_FurShapeMap"));
			//var texData = System.IO.File.ReadAllBytes(furTexPath);
			//Texture2D furTex = new Texture2D(1,1,TextureFormat.ARGB32,true,true);
			//furTex.LoadImage(texData);
			Texture furTex = material.GetTexture("_FurShapeMap");


			if (furTex == null)
			{
				errorMessage("The Fur Shape Data Map is missing on the target material.");
				return false;
			}
			int targetWidth = furTex.width;
			int targetHeight = furTex.height;
			// Create the fur shape textures.
			furShapeRenderTexturePreEdit = new RenderTexture(targetWidth, targetHeight, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
			furShapeRenderTexturePreEdit.filterMode = FilterMode.Point;
			furShapeRenderTextureBase = new RenderTexture(targetWidth, targetHeight, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
			furShapeRenderTextureBase.filterMode = FilterMode.Point;
			furShapeRenderTextureTarget = new RenderTexture(targetWidth, targetHeight, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
			furShapeRenderTextureTarget.filterMode = FilterMode.Point;
			furShapeRenderTextureBuffer = new RenderTexture(targetWidth, targetHeight, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
			furShapeRenderTextureBuffer.filterMode = FilterMode.Point;
			furShapeRenderTextureFinal = new RenderTexture(targetWidth, targetHeight, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
			furShapeRenderTextureUndo = new RenderTexture[maxUndoSize + 1];
			for (int i = 0; i <= maxUndoSize; i++) furShapeRenderTextureUndo[i] = new RenderTexture(targetWidth, targetHeight, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
			currentUndo = 0;
			highestUndo = 0;
			undoOverflow = false;
			doWrite = false;
			validSamples = 0;
			material.SetTexture("_FurShapeMap", furShapeRenderTextureFinal);
			Graphics.Blit(furTex, furShapeRenderTextureBase);
			Graphics.Blit(furTex, furShapeRenderTexturePreEdit);
			Graphics.Blit(furTex, furShapeRenderTextureUndo[0]);

			// Create the distance and direction textures. Note that these are high-precision textures.
			distanceRenderTexture = new RenderTexture(furTex.width, furTex.height, 0, RenderTextureFormat.RFloat, RenderTextureReadWrite.Linear);
			distanceRenderTexture.filterMode = FilterMode.Point;
			closestPointRenderTexture = new RenderTexture(furTex.width, furTex.height, 0, RenderTextureFormat.RFloat, RenderTextureReadWrite.Linear);
			closestPointRenderTexture.filterMode = FilterMode.Point;
			closestPointRenderTextureBuffer = new RenderTexture(furTex.width, furTex.height, 0, RenderTextureFormat.RFloat, RenderTextureReadWrite.Linear);
			closestPointRenderTextureBuffer.filterMode = FilterMode.Point;
			directionRenderTexture = new RenderTexture(furTex.width, furTex.height, 0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
			directionRenderTexture.filterMode = FilterMode.Point;
			fillRenderTexture = new RenderTexture(furTex.width, furTex.height, 0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
			fillRenderTexture.filterMode = FilterMode.Point;
			fillRenderTextureBuffer = new RenderTexture(furTex.width, furTex.height, 0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
			fillRenderTextureBuffer.filterMode = FilterMode.Point;

			// Create the comb textures. Note that these are high-precision textures.
			combRenderTextureTarget = new RenderTexture(furTex.width, furTex.height, 0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
			combRenderTextureTarget.filterMode = FilterMode.Point;
			combRenderTextureBuffer = new RenderTexture(furTex.width, furTex.height, 0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
			combRenderTextureBuffer.filterMode = FilterMode.Point;
			combRenderTextureFinal = new RenderTexture(furTex.width, furTex.height, 0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
			combRenderTextureFinal.filterMode = FilterMode.Point;
			combRenderTextureBlank = new RenderTexture(furTex.width, furTex.height, 0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
			combRenderTextureBlank.filterMode = FilterMode.Point;
			combRenderTextureHistory = new RenderTexture[maxCombTextures];
			for (int i = 0; i < maxCombTextures; i++)
			{
				combRenderTextureHistory[i] = new RenderTexture(furTex.width, furTex.height, 0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
				combRenderTextureHistory[i].filterMode = FilterMode.Point;
			}

			// Determine the average density of the mesh and use that as a baseline for density painting
			Material densityCheckMaterial = new Material(Shader.Find("Warren's Fast Fur/Internal Utilities/Density Check"));
			densityTexture = new RenderTexture(furTex.width, furTex.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
			densityCheckMaterial.SetMatrix("_FurMeshMatrix", myMeshCollider.transform.localToWorldMatrix);
			CommandBuffer densityCommandBuffer = new CommandBuffer();
			densityCommandBuffer.name = "FastFurDensityCheck";
			densityCommandBuffer.SetRenderTarget(densityTexture);
			densityCommandBuffer.DrawMesh(myMeshCollider.sharedMesh, Matrix4x4.identity, densityCheckMaterial);
			Graphics.ExecuteCommandBuffer(densityCommandBuffer);
			densityTexture.filterMode = FilterMode.Point;

			Texture2D outputTexture = new Texture2D(densityTexture.width, densityTexture.height, TextureFormat.ARGB32, true, true);
			outputTexture.filterMode = FilterMode.Point;
			RenderTexture.active = densityTexture;
			outputTexture.ReadPixels(new Rect(0, 0, outputTexture.width, outputTexture.height), 0, 0);
			outputTexture.Apply();
			RenderTexture.active = null;

			RenderTexture.active = null;
			Color[] pixels = outputTexture.GetPixels();
			float[] pixelCount = new float[256];
			float totalPixels = furTex.width * furTex.height;
			for (int x = 0; x < 256; x++) pixelCount[x] = 0;
			for (int x = 0; x < totalPixels; x++)
			{
				if (pixels[x].a > 0) pixelCount[(int)(pixels[x].r * 255)]++;
			}
			float activePixels = 0;
			for (int x = 0; x < 256; x++) activePixels += pixelCount[x];

			float densityThreshold = 0;
			float total = 0;
			for (int x = 0; x < 256; x++)
			{
				total += pixelCount[x];
				if (total < (activePixels * 0.5)) densityThreshold = x;
				else break;
			}
			// Unpack the density so that 0 -> 0.01, 0.5 -> 1, 1 -> 100
			baseDensity = (Mathf.Pow(10, (densityThreshold / 255) * 4 - 2) + Mathf.Pow(10, (((densityThreshold + 1) / 255) * 4 - 2))) * 0.5f;

			lastMeshUpdate = Time.realtimeSinceStartup - 5;
		}

		catch (System.Exception e)
		{
			Debug.LogError(e.ToString());
			errorMessage("The Target Material failed to load: " + e.ToString());
			return false;
		}

		return true;
	}
	#endregion

	#region "Update Frame"
	//////////////////////////////////////////////////////////////////////////////////
	//--------------------------------------------------------------------------------
	// Update is called every frame
	//--------------------------------------------------------------------------------
	//////////////////////////////////////////////////////////////////////////////////

	void Update()
	{
		try
		{
			if (uploadActive)
			{
				if (groomingGUI != null)
				{
					groomingGUI.SetActive(false);
					if (enableDebugText) Debug.Log("[WFFS] Disabled GUI");
				}
				if (enableDebugText) Debug.Log("[WFFS] Exiting (upload is active)");
				return;
			}
			if (groomingIsStopped)
			{
				MonoBehaviour[] scripts = GameObject.FindObjectsOfType<MonoBehaviour>();
				foreach (MonoBehaviour script in scripts)
				{
					if (script.name.Contains("VRCSDK"))
					{
						Debug.Log("Stopping and removing Fur Grooming");
						uploadActive = true;
						return;
					}
				}

				if (enableDebugText) Debug.Log("[WFFS] Exiting (grooming is stopped)");
				return;
			}

			Application.targetFrameRate = maxFrameRate;
			moveCamera();

			// Before we do anything else, run a sanity check. If something has changed that would break the Fur Grooming
			// then we want to stop everything and inform the user, rather than just quietly breaking.
			sanityCheck();


			// Has the material selection changed?
			if (materialSelector.value != activeMaterialIndex)
			{
				activeMaterialIndex = materialSelector.value;

				// Load the new material
				loadRenderer(fastFurMats[activeMaterialIndex]);
				loadMaterial(fastFurMats[activeMaterialIndex]);
			}


			bool textureChanged = false;

			// Render the fur grooming
			Graphics.ExecuteCommandBuffer(distanceCommandBuffer);
			Graphics.Blit(closestPointRenderTexture, closestPointRenderTextureBuffer);
			Graphics.Blit(distanceRenderTexture, closestPointRenderTexture, closestMaterial);
			Graphics.ExecuteCommandBuffer(directionCommandBuffer);
			if (!furSphere)
			{
				Graphics.Blit(directionRenderTexture, fillRenderTexture, fillMaterial);
			}
			else
			{
				Graphics.Blit(directionRenderTexture, fillRenderTexture);
			}
			Graphics.ExecuteCommandBuffer(combCommandBuffer);
			Graphics.ExecuteCommandBuffer(groomCommandBuffer);

			Graphics.Blit(selectedFastFurMat.albedoOriginalTexture, albedoRenderTextureTarget, cursorMaterial);

			fixEdges(false);

			// Check to see if the mouse is over the mesh
			mouseHit = Vector4.negativeInfinity;
			Ray rayCast = myCamera.ScreenPointToRay(Input.mousePosition);
			Ray mirrorRayCast = new Ray(new Vector3(-rayCast.origin.x, rayCast.origin.y, rayCast.origin.z), new Vector3(-rayCast.direction.x, rayCast.direction.y, rayCast.direction.z));
			RaycastHit hit;
			RaycastHit mirrorHit;
			bool success = false;
			bool mirrorSuccess = false;
			hits = 0;

			while (Physics.Raycast(rayCast, out hit))
			{
				// We got a hit, but was it the correct material?
				hits++;
				hitIndex = hit.triangleIndex * 3;
				if (hitIndex >= targetTriangleStartIndex && hitIndex <= targetTriangleEndIndex)
				{
					success = true;
					break;
				}
				// Not the right material, so move a tiny bit further ahead and do another raycast in the same direction
				rayCast = new Ray(hit.point + rayCast.direction.normalized * 0.00001f, rayCast.direction);
			}

			// Do it again for the mirror (we only need this if spherical is off)
			mirrorHit = hit;
			if(!furSphere)
			{
				while (Physics.Raycast(mirrorRayCast, out mirrorHit))
				{
					// We got a hit, but was it the correct material?
					hits++;
					hitIndex = mirrorHit.triangleIndex * 3;
					if (hitIndex >= targetTriangleStartIndex && hitIndex <= targetTriangleEndIndex)
					{
						mirrorSuccess = true;
						break;
					}
					// Not the right material, so move a tiny bit further ahead and do another raycast in the same direction
					mirrorRayCast = new Ray(mirrorHit.point + mirrorRayCast.direction.normalized * 0.00001f, mirrorRayCast.direction);
				}
			}

			if (success)
			{
				mouseHit = hit.point;
				mouseHitUV = hit.textureCoord;
				mouseHitMirrorUV = mirrorSuccess ? mirrorHit.textureCoord : mouseHitUV;

				// Check if we have a valid combing sample
				if (mouseHit != oldMouseHitComb && !EventSystem.current.IsPointerOverGameObject())
				{
					for (int i = maxCombTextures - 1; i > 0; i--) Graphics.Blit(combRenderTextureHistory[i - 1], combRenderTextureHistory[i]);
					Graphics.Blit(combRenderTextureFinal, combRenderTextureHistory[0]);
					oldMouseHitComb = mouseHit;
					validSamples++;
				}

				// If the mouse is clicked, save the changes
				if (mouseHit != oldMouseHitWrite && Input.GetMouseButton(0) && !EventSystem.current.IsPointerOverGameObject())
				{
					textureChanged = true;
					Graphics.Blit(furShapeRenderTextureFinal, furShapeRenderTextureBase);
					oldMouseHitWrite = mouseHit;
				}

				// If the middle mouse button is clicked, sample the texture
				if (Input.GetMouseButton(2))
				{
					Texture2D outputTexture = new Texture2D(1, 1, TextureFormat.ARGB32, true, true);
					outputTexture.filterMode = FilterMode.Point;
					RenderTexture.active = furShapeRenderTexturePreEdit;
					outputTexture.ReadPixels(new Rect((int)(hit.textureCoord.x * furShapeRenderTexturePreEdit.width), (int)((1 - hit.textureCoord.y) * furShapeRenderTexturePreEdit.height), 1, 1), 0, 0);
					outputTexture.Apply();
					RenderTexture.active = null;
					Color pixel = outputTexture.GetPixel(0, 0);

					RenderTexture.active = densityTexture;
					outputTexture.ReadPixels(new Rect((int)(hit.textureCoord.x * densityTexture.width), (int)((1 - hit.textureCoord.y) * densityTexture.height), 1, 1), 0, 0);
					outputTexture.Apply();
					RenderTexture.active = null;
					Color densityPixel = outputTexture.GetPixel(0, 0);

					furHeight = pixel.b;
					Vector2 combing = new Vector2(pixel.r * 2 - 1, pixel.g * 2 - 1);
					furCombing = Mathf.Min(1, combing.magnitude); // Due to rounding errors, the combing length can sometimes go slightly past 1
					furDensity = Mathf.Round(pixel.a * 64) / 64; // Drop some precision, otherwise compression artifacts will cause visible seams
																 //Debug.Log("Fur density = " + furDensity);

					if ((int)densityMode == 1)
					{
						//Debug.Log("Density pixel = " + densityPixel.r);
						// Unpack the density so that 0 -> 0.01, 0.5 -> 1, 1 -> 100
						float actualDensity = Mathf.Pow(10, (float)densityPixel.r * 4 - 2) * Mathf.Pow(10, furDensity * 4 - 2);
						//Debug.Log("Actual density = " + actualDensity);
						furDensity = actualDensity / baseDensity;
						// Re-pack the density so that 0.01 -> 0, 1 -> 0.5, 100 -> 1
						furDensity = Mathf.Min(1, Mathf.Max(0, ((Mathf.Log10(furDensity) + 2) * 0.25f)));
					}

					// Set the GUI sliders
					lengthSlider.value = furHeight * 255f;
					densitySlider.value = furDensity * 255f;
					combingSlider.value = furCombing * 255f;
				}
			}
			else
			{
				oldMouseHitWrite = Vector4.negativeInfinity;
				oldMouseHitComb = Vector4.negativeInfinity;
				validSamples = 0;
			}


			// If the Set All button is clicked, copy the groomed texture 
			if (furHeightSetAll > 0 || furCombingSetAll > 0 || furDensitySetAll > 0)
			{
				textureChanged = true;
				Graphics.Blit(furShapeRenderTextureFinal, furShapeRenderTextureBase);
			}

			// Check if we should make changes permanent
			if (doWrite)
			{
				if ((!Input.GetMouseButton(0) && (furHeightSetAll + furCombingSetAll + furDensitySetAll) == 0) || furHeightSetAll == 1 || furCombingSetAll == 1 || furDensitySetAll == 1)
				{
					writeChanges();
					doWrite = false;
				}
			}
			else
			{
				if ((Input.GetMouseButton(0) && !EventSystem.current.IsPointerOverGameObject()) || furHeightSetAll > 0 || furCombingSetAll > 0 || furDensitySetAll > 0) doWrite = true;
			}

			// If the mouse button has been released, then reset the "closest" texture
			if (!Input.GetMouseButton(0)) Graphics.Blit(closestPointRenderTexture, closestPointRenderTexture, distanceMaterial, 0);

			// Update the shader properties
			updateMaterials();

			if (furHeightSetAll > 0) furHeightSetAll--;
			if (furCombingSetAll > 0) furCombingSetAll--;
			if (furDensitySetAll > 0) furDensitySetAll--;


			// Periodically update the mesh collider and the command buffers
			if (lastMeshUpdate < (Time.realtimeSinceStartup - 1))
			{
				MonoBehaviour[] scripts = GameObject.FindObjectsOfType<MonoBehaviour>();
				foreach (MonoBehaviour script in scripts)
				{
					if (script.name.Contains("VRCSDK"))
					{
						Debug.Log("Stopping and removing Fur Grooming");
						uploadActive = true;
						return;
					}
				}

				configureGUI();

				// Re-loading the renderer updates the mesh collider and the command buffers
				loadRenderer(selectedFastFurMat);
				//loadMaterial(selectedFastFurMat);
			}

			// Update the sliders
			sizeValue.text = "" + string.Format("{0:0.00}", Mathf.Round(brushRadius * 100) * 0.01);
			strengthValue.text = "" + Mathf.Round(brushStrength * 100f);
			falloffValue.text = "" + Mathf.Round(brushFalloff * 100f);
			visibilityValue.text = "" + Mathf.Round(brushVisibility * 100f);
			sizeSlider.value = brushRadius;
			strengthSlider.value = brushStrength * 100f;
			falloffSlider.value = brushFalloff * 100f;
			visibilitySlider.value = brushVisibility * 100f;

			lengthValue.text = "" + Mathf.Round(furHeight * 255f);
			densityValue.text = "" + Mathf.Round(furDensity * 255f);
			combingValue.text = "" + Mathf.Round(furCombing * 255f);
			lengthSlider.value = furHeight * 255f;
			densitySlider.value = furDensity * 255f;
			combingSlider.value = furCombing * 255f;

			// Update the slider colours
			if (furHeightEnable && (int)brushMode != 3) { lengthSlider.image.color = Color.white; }
			else lengthSlider.image.color = Color.grey;
			if (furDensityEnable && (int)brushMode != 3) { densitySlider.image.color = Color.white; }
			else densitySlider.image.color = Color.grey;
			if (furCombingEnable && (int)brushMode != 3 && (int)combingMode != 2) { combingSlider.image.color = Color.white; }
			else combingSlider.image.color = Color.grey;

			// Update the Save button
			if (fileNotSaved == 0 && !textureChanged)
			{
				saveButton.image.color = Color.white;
				materialSelector.image.color = Color.white;
				materialSelector.enabled = true;
			}
			else
			{
				float pulseSpeed = 1f + ((float)currentUndo / ((float)maxUndoSize * 0.5f));
				float pulseIntensity = 0.1f * pulseSpeed;
				fileNotSaved += pulseSpeed;

				float pulseRed = pulseIntensity - Mathf.Sin((float)fileNotSaved * 0.02f) * pulseIntensity;
				float pulseBlue = (2 * pulseIntensity) + Mathf.Sin((float)fileNotSaved * 0.02f) * (2 * pulseIntensity);
				float fadeIn = Mathf.Min(1f, ((float)fileNotSaved * 0.1f));
				pulseRed = 1f - (pulseRed * fadeIn);
				pulseBlue = 1f - (pulseBlue * fadeIn);
				saveButton.image.color = new Color(pulseRed, 1f - (0.75f * fadeIn), pulseBlue, 1f);
				materialSelector.image.color = Color.grey;
				materialSelector.enabled = false;
			}
		}
		catch (System.Exception e)
		{
			Debug.LogError(e.ToString());
			errorMessage("Failed during Update(): " + e);
			return;
		}
	}
	#endregion

	#region "Command Buffers"
	//--------------------------------------------------------------------------------
	// Add command buffers
	private void addCommandBuffers()
	{
		if (enableDebugText) Debug.Log("[WFFS] addCommandBuffers() called at frame " + Time.frameCount);

		try
		{
			distanceCommandBuffer = new CommandBuffer();
			distanceCommandBuffer.name = "Fast Fur Distance";
			distanceCommandBuffer.SetRenderTarget(distanceRenderTexture);
			distanceCommandBuffer.DrawMesh(myMeshCollider.sharedMesh, Matrix4x4.identity, distanceMaterial, subMeshIndex);

			directionCommandBuffer = new CommandBuffer();
			directionCommandBuffer.name = "Fast Fur Direction";
			directionCommandBuffer.SetRenderTarget(directionRenderTexture);
			directionCommandBuffer.DrawMesh(myMeshCollider.sharedMesh, Matrix4x4.identity, directionMaterial, subMeshIndex);

			combCommandBuffer = new CommandBuffer();
			combCommandBuffer.name = "Fast Fur Comb";
			combCommandBuffer.SetRenderTarget(combRenderTextureTarget);
			combCommandBuffer.DrawMesh(myMeshCollider.sharedMesh, Matrix4x4.identity, combMaterial, subMeshIndex);

			groomCommandBuffer = new CommandBuffer();
			groomCommandBuffer.name = "Fast Fur Grooming";
			groomCommandBuffer.SetRenderTarget(furShapeRenderTextureTarget);
			groomCommandBuffer.DrawMesh(myMeshCollider.sharedMesh, Matrix4x4.identity, groomingMaterial, subMeshIndex);
		}

		catch (System.Exception e)
		{
			Debug.LogError(e.ToString());
			errorMessage("Can't add command buffers: " + e.ToString());
			return;
		}
	}
	#endregion

	#region "Update Materials"
	//--------------------------------------------------------------------------------
	// Update material properties
	private void updateMaterials()
	{

		selectedUV = 0;
		if (selectedMaterial.HasProperty("_SelectedUV")) selectedUV = selectedMaterial.GetInt("_SelectedUV");

		selectedMaterial.SetTexture("_MainTex", albedoRenderTextureFinal);
		selectedMaterial.SetTexture("_FurShapeMap", furShapeRenderTextureFinal);
		selectedMaterial.SetFloat("_CameraProximityTouch", 0);

		distanceMaterial.SetInt("_SelectedUV", selectedUV);
		distanceMaterial.SetVector("_FurGroomMouseHit", mouseHit);
		distanceMaterial.SetInt("_FurMirror", furMirror ? 1 : 0);
		distanceMaterial.SetFloat("_FurMirrorX", mirrorX);
		distanceMaterial.SetMatrix("_FurMeshMatrix", myMeshCollider.transform.localToWorldMatrix);

		closestMaterial.SetTexture("_MainTex", distanceRenderTexture);
		closestMaterial.SetTexture("_ClosestTex", closestPointRenderTextureBuffer);

		directionMaterial.SetInt("_SelectedUV", selectedUV);
		directionMaterial.SetTexture("_MainTex", distanceRenderTexture);
		directionMaterial.SetVector("_FurGroomMouseHit", mouseHit);
		directionMaterial.SetInt("_FurMirror", furMirror ? 1 : 0);
		directionMaterial.SetFloat("_FurMirrorX", mirrorX);
		directionMaterial.SetMatrix("_FurMeshMatrix", myMeshCollider.transform.localToWorldMatrix);

		combMaterial.SetInt("_SelectedUV", selectedUV);
		combMaterial.SetTexture("_DirectionTex", fillRenderTexture);
		combMaterial.SetMatrix("_FurMeshMatrix", myMeshCollider.transform.localToWorldMatrix);

		fillMaterial.SetInt("_SelectedUV", selectedUV);
		fillMaterial.SetFloat("_FurGroomBrushRadius", brushRadius);
		fillMaterial.SetVector("_HitUV", mouseHitUV);
		fillMaterial.SetVector("_MirrorHitUV", mouseHitMirrorUV);

		cursorMaterial.SetInt("_SelectedUV", selectedUV);
		cursorMaterial.SetTexture("_DirectionTex", fillRenderTexture);
		cursorMaterial.SetFloat("_FurGroomBrushRadius", brushRadius);
		cursorMaterial.SetFloat("_FurGroomBrushFalloff", brushFalloff);
		cursorMaterial.SetFloat("_FurGroomBrushVisibility", brushVisibility);

		groomingMaterial.SetInt("_SelectedUV", selectedUV);
		groomingMaterial.SetTexture("_DirectionTex", fillRenderTexture);
		groomingMaterial.SetTexture("_ClosestTex", closestPointRenderTexture);
		groomingMaterial.SetFloat("_FurBaseDensity", baseDensity);
		groomingMaterial.SetTexture("_FurCombPosition1", combRenderTextureHistory[0]);
		groomingMaterial.SetTexture("_FurCombPosition2", combRenderTextureHistory[maxCombTextures - 1]);
		groomingMaterial.SetTexture("_FurShapeMap", furShapeRenderTextureBase);
		groomingMaterial.SetTexture("_FurShapeMapPreEdit", furShapeRenderTexturePreEdit);
		groomingMaterial.SetTexture("_FurGroomingMask", selectedMaterial.GetTexture("_FurGroomingMask"));
		if (selectedMaterial.HasProperty("_FurMinHeight")) groomingMaterial.SetFloat("_FurMinHeight", selectedMaterial.GetFloat("_FurMinHeight"));
		else groomingMaterial.SetFloat("_FurMinHeight", 0.01f);
		groomingMaterial.SetFloat("_FurGroomBrushRadius", brushRadius);
		groomingMaterial.SetFloat("_FurGroomBrushFalloff", brushFalloff);
		groomingMaterial.SetFloat("_FurGroomBrushStrength", brushStrength);
		groomingMaterial.SetInt("_FurGroomFurHeightEnabled", furHeightEnable ? 1 : 0);
		groomingMaterial.SetInt("_FurGroomFurCombingEnabled", furCombingEnable && validSamples > maxCombTextures ? 1 : 0);
		groomingMaterial.SetInt("_FurGroomFurDensityEnabled", furDensityEnable ? 1 : 0);
		groomingMaterial.SetFloat("_FurGroomFurHeight", furHeight);
		groomingMaterial.SetFloat("_FurGroomFurCombing", furCombing);
		groomingMaterial.SetFloat("_FurGroomFurDensity", furDensity);
		groomingMaterial.SetInt("_FurGroomFurHeightSetAll", furHeightSetAll);
		groomingMaterial.SetInt("_FurGroomFurCombingSetAll", furCombingSetAll);
		groomingMaterial.SetInt("_FurGroomFurDensitySetAll", furDensitySetAll);
		groomingMaterial.SetInt("_FurBrushMode", (int)brushMode);
		groomingMaterial.SetInt("_FurLengthMode", (int)lengthMode);
		groomingMaterial.SetInt("_FurDensityMode", (int)densityMode);
		groomingMaterial.SetInt("_FurCombingMode", (int)combingMode);
		groomingMaterial.SetMatrix("_FurMeshMatrix", myMeshCollider.transform.localToWorldMatrix);

		stencilMaterial.SetTexture("_FurShapeMap", selectedFastFurMat.furShapeOriginalTexture);


		// Update the debugging options
		selectedMaterial.SetInt("_FurDebugLength", furShowData && furHeightEnable ? 1 : 0);
		selectedMaterial.SetInt("_FurDebugDensity", furShowData && furDensityEnable ? 1 : 0);
		selectedMaterial.SetInt("_FurDebugCombing", furShowData && furCombingEnable ? 1 : 0);

		if (furShowData) selectedMaterial.EnableKeyword("FUR_DEBUGGING");
	}
	#endregion

	#region "Texture Manipulation"
	//--------------------------------------------------------------------------------
	// Fix edges by adding overpainting. If we are finished grooming, also apply the
	// stencil to the grooming map, so that we preserve other parts of the map that we
	// were not editing.
	private void fixEdges(bool finalFix)
	{
		float overPainting = finalFix ? finalOverPainting : Mathf.Max(16, finalOverPainting);

		try
		{
			if (overPainting == 0)
			{
				Graphics.Blit(albedoRenderTextureTarget, albedoRenderTextureFinal);
				Graphics.Blit(combRenderTextureTarget, combRenderTextureFinal);
				Graphics.Blit(furShapeRenderTextureTarget, furShapeRenderTextureFinal);
			}
			else if (overPainting == 1)
			{
				Graphics.Blit(albedoRenderTextureTarget, albedoRenderTextureFinal, fixEdgesMaterial);
				Graphics.Blit(combRenderTextureTarget, combRenderTextureFinal, fixEdgesMaterial);
				Graphics.Blit(furShapeRenderTextureTarget, furShapeRenderTextureFinal, fixEdgesMaterial);
			}
			else
			{
				Graphics.Blit(albedoRenderTextureTarget, albedoRenderTextureBuffer, fixEdgesMaterial);
				Graphics.Blit(combRenderTextureTarget, combRenderTextureBuffer, fixEdgesMaterial);
				Graphics.Blit(furShapeRenderTextureTarget, furShapeRenderTextureBuffer, fixEdgesMaterial);
			}

			int x = 1;
			while (x < overPainting)
			{
				if (x + 1 == overPainting)
				{
					Graphics.Blit(albedoRenderTextureBuffer, albedoRenderTextureFinal, fixEdgesMaterial);
					Graphics.Blit(combRenderTextureBuffer, combRenderTextureFinal, fixEdgesMaterial);
					Graphics.Blit(furShapeRenderTextureBuffer, furShapeRenderTextureFinal, fixEdgesMaterial);
					x++;
				}
				else if (x + 2 == overPainting)
				{
					Graphics.Blit(albedoRenderTextureBuffer, albedoRenderTextureFinal, fixEdgesMaterial);
					Graphics.Blit(combRenderTextureBuffer, combRenderTextureFinal, fixEdgesMaterial);
					Graphics.Blit(furShapeRenderTextureBuffer, furShapeRenderTextureFinal, fixEdgesMaterial);

					Graphics.Blit(albedoRenderTextureFinal, albedoRenderTextureBuffer, fixEdgesMaterial);
					Graphics.Blit(combRenderTextureFinal, combRenderTextureBuffer, fixEdgesMaterial);
					Graphics.Blit(furShapeRenderTextureFinal, furShapeRenderTextureBuffer, fixEdgesMaterial);

					Graphics.Blit(albedoRenderTextureBuffer, albedoRenderTextureFinal);
					Graphics.Blit(combRenderTextureBuffer, combRenderTextureFinal);
					Graphics.Blit(furShapeRenderTextureBuffer, furShapeRenderTextureFinal);
					x += 2;
				}
				else
				{
					Graphics.Blit(albedoRenderTextureBuffer, albedoRenderTextureFinal, fixEdgesMaterial);
					Graphics.Blit(combRenderTextureBuffer, combRenderTextureFinal, fixEdgesMaterial);
					Graphics.Blit(furShapeRenderTextureBuffer, furShapeRenderTextureFinal, fixEdgesMaterial);

					Graphics.Blit(albedoRenderTextureFinal, albedoRenderTextureBuffer, fixEdgesMaterial);
					Graphics.Blit(combRenderTextureFinal, combRenderTextureBuffer, fixEdgesMaterial);
					Graphics.Blit(furShapeRenderTextureFinal, furShapeRenderTextureBuffer, fixEdgesMaterial);
					x += 2;
				}
			}

			Graphics.Blit(furShapeRenderTextureFinal, furShapeRenderTextureBuffer);
			Graphics.Blit(furShapeRenderTextureBuffer, furShapeRenderTextureFinal, stencilMaterial);
		}

		catch (System.Exception e)
		{
			Debug.LogError(e.ToString());
			errorMessage("Failed to Fix Edges: " + e.ToString());
			return;
		}
	}



	//--------------------------------------------------------------------------------
	// Write current changes to the texture
	private void writeChanges()
	{
		try
		{
			if (currentUndo == maxUndoSize)
			{
				undoOverflow = true;
				for (int x = 0; x < maxUndoSize; x++)
				{
					Graphics.Blit(furShapeRenderTextureUndo[x + 1], furShapeRenderTextureUndo[x]);
				}
			}
			else currentUndo++;

			fileNotSaved++;
			highestUndo = currentUndo;
			undoButton.interactable = true;
			redoButton.interactable = false;

			Graphics.Blit(furShapeRenderTextureFinal, furShapeRenderTexturePreEdit);
			Graphics.Blit(furShapeRenderTexturePreEdit, furShapeRenderTextureUndo[currentUndo]);
		}

		catch (System.Exception e)
		{
			Debug.LogError(e.ToString());
			errorMessage("Failed to write changes: " + e.ToString());
			return;
		}
	}



	//--------------------------------------------------------------------------------
	// Undo changes
	public void doUndo()
	{
		try
		{
			if (currentUndo == 0) return;

			currentUndo--;
			if (currentUndo == 0 && !undoOverflow) fileNotSaved = 0;
			undoButton.interactable = (currentUndo != 0);
			redoButton.interactable = true;

			Graphics.Blit(furShapeRenderTextureUndo[currentUndo], furShapeRenderTexturePreEdit);
			Graphics.Blit(furShapeRenderTextureUndo[currentUndo], furShapeRenderTextureBase);
		}

		catch (System.Exception e)
		{
			Debug.LogError(e.ToString());
			errorMessage("Failed to undo: " + e.ToString());
			return;
		}
	}



	//--------------------------------------------------------------------------------
	// Redo changes
	public void doRedo()
	{
		try
		{
			if (currentUndo == highestUndo) return;

			currentUndo++;
			fileNotSaved++;
			undoButton.interactable = true;
			redoButton.interactable = (currentUndo != highestUndo);

			Graphics.Blit(furShapeRenderTextureUndo[currentUndo], furShapeRenderTexturePreEdit);
			Graphics.Blit(furShapeRenderTextureUndo[currentUndo], furShapeRenderTextureBase);
		}

		catch (System.Exception e)
		{
			Debug.LogError(e.ToString());
			errorMessage("Failed to redo: " + e.ToString());
			return;
		}
	}



	//--------------------------------------------------------------------------------
	// Save changes
	public void doSave()
	{
		if (enableDebugText) Debug.Log("[WFFS] doSave() called at frame " + Time.frameCount);

		try
		{
			if (filename.Length < 1) return;

			fastFurMats[activeMaterialIndex].filename = filename;

			fixEdges(true);
			Texture2D outputTexture = new Texture2D(furShapeRenderTexturePreEdit.width, furShapeRenderTexturePreEdit.height, TextureFormat.ARGB32, true, true);
			outputTexture.filterMode = FilterMode.Point;
			RenderTexture.active = furShapeRenderTexturePreEdit;
			outputTexture.ReadPixels(new Rect(0, 0, outputTexture.width, outputTexture.height), 0, 0);
			outputTexture.Apply();
			RenderTexture.active = null;

			byte[] bytes = outputTexture.EncodeToPNG();
			System.IO.File.WriteAllBytes(filename, bytes);
			AssetDatabase.Refresh();

			// If the filename has changed, then we need to set the texture import setting
			TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(filename);
			importer.sRGBTexture = false;
			importer.crunchedCompression = false;
			importer.textureCompression = TextureImporterCompression.Uncompressed;
			importer.streamingMipmaps = true;
			importer.SaveAndReimport();

			// Re-load the material from the newly saved version
			AssetDatabase.Refresh();
			fastFurMats[activeMaterialIndex].furShapeOriginalTexture = AssetDatabase.LoadAssetAtPath<Texture>(fastFurMats[activeMaterialIndex].filename);
			loadMaterial(fastFurMats[activeMaterialIndex]);

			undoOverflow = false;
			currentUndo = 0;
			highestUndo = 0;
			fileNotSaved = 0;

			undoButton.interactable = false;
			redoButton.interactable = false;

			Graphics.Blit(furShapeRenderTexturePreEdit, furShapeRenderTextureUndo[0]);
		}

		catch (System.Exception e)
		{
			Debug.LogError(e.ToString());
			errorMessage("Failed to save: " + e.ToString());
			return;
		}
	}
	#endregion

	#region "Camera Control"
	//--------------------------------------------------------------------------------
	// Move camera
	private void moveCamera()
	{
		try
		{
			float speedChange = Input.GetAxis("Mouse ScrollWheel");
			if (speedChange > 0) moveSpeed *= (1 + (speedChange * 10));
			if (speedChange < 0) moveSpeed /= (1 - (speedChange * 10));
			if (moveSpeed < 0.015625f) moveSpeed = 0.015625f;
			if (moveSpeed > 128f) moveSpeed = 128f;
			if (moveSpeed != moveSpeedPrev)
			{
				helpText.text = helpText.text.Substring(0, 67) + moveSpeed.ToString().Substring(0, Mathf.Min(5, moveSpeed.ToString().Length)) + ")";
				moveSpeedPrev = moveSpeed;
				helpPopupSpeed.gameObject.SetActive(false);
			}
			float shiftPressed = Input.GetKey(KeyCode.RightShift) || Input.GetKey(KeyCode.LeftShift) ? 2 : 1;

			if (Input.GetMouseButton(1))
			{
				yaw += turnSpeed * Input.GetAxis("Mouse X");
				pitch -= turnSpeed * Input.GetAxis("Mouse Y");
				myCamera.transform.eulerAngles = new Vector3(pitch, yaw, 0f);
				helpPopupCamera.gameObject.SetActive(false);
			}

			if (Input.GetKey(forwardKey))
			{
				myCamera.transform.Translate(0, 0, 0.001f * moveSpeed * shiftPressed);
				helpPopupMove.gameObject.SetActive(false);
			}
			if (Input.GetKey(backKey))
			{
				myCamera.transform.Translate(0, 0, -0.001f * moveSpeed * shiftPressed);
				helpPopupMove.gameObject.SetActive(false);
			}
			if (Input.GetKey(upKey))
			{
				myCamera.transform.Translate(0, 0.001f * moveSpeed * shiftPressed, 0);
				helpPopupMove.gameObject.SetActive(false);
			}
			if (Input.GetKey(downKey))
			{
				myCamera.transform.Translate(0, -0.001f * moveSpeed * shiftPressed, 0);
				helpPopupMove.gameObject.SetActive(false);
			}
			if (Input.GetKey(rightKey))
			{
				myCamera.transform.Translate(0.001f * moveSpeed * shiftPressed, 0, 0);
				helpPopupMove.gameObject.SetActive(false);
			}
			if (Input.GetKey(leftKey))
			{
				myCamera.transform.Translate(-0.001f * moveSpeed * shiftPressed, 0, 0);
				helpPopupMove.gameObject.SetActive(false);
			}
		}

		catch (System.Exception e)
		{
			Debug.LogError(e.ToString());
			errorMessage("Failed to move camera: " + e.ToString());
			return;
		}
	}
	#endregion



	#region "Error Handling"
	//--------------------------------------------------------------------------------
	// Display an error message
	private void errorMessage(string error)
	{
		Component[] guiComponents = groomingGUI.GetComponentsInChildren(typeof(Component), true);
		foreach (Component c in guiComponents)
		{
			if (c.name.StartsWith("FurGroom Error Message")) c.gameObject.SetActive(true);
			if (c.name.Equals("FurGroom GUI")) c.gameObject.SetActive(false);
			if (c.name.Equals("FurGroom Error Message Text")) ((Text)c.GetComponentInChildren(typeof(Text))).text = "ERROR: " + error;
		}
		groomingIsStopped = true;
		if (enableDebugText) Debug.Log("[WFFS] Error Message. Grooming stopped.");
	}



	//--------------------------------------------------------------------------------
	// Display a warning message
	private void warningMessage(string warning)
	{
		warningText.text = "WARNING: " + warning;
		warningImage.gameObject.SetActive(warning.Length > 0);
	}



	//--------------------------------------------------------------------------------
	// Sanity checks
	private void sanityCheck()
	{
		try
		{
			if (selectedFastFurMat.renderer != selectedRenderer)
			{
				string error = "Something (likely an animation controller, or a custom script) has interfered with the Fur Grooming. The selected renderer was modified while the Fur Grooming was running: '";
				error += selectedFastFurMat.renderer.name + "' -> '" + selectedRenderer.name + "'";
				return;
			}

			if (selectedFastFurMat.material != selectedMaterial)
			{
				string error = "Something (likely an animation controller, or a custom script) has interfered with the Fur Grooming. The selected material was modified while the Fur Grooming was running: '";
				error += selectedFastFurMat.material.name + "' -> '" + selectedMaterial.name + "'";
				errorMessage(error);
				return;
			}

			Material[] checkMats = selectedRenderer.materials;
			bool found = false;
			foreach (Material mat in checkMats)
			{
				if (mat.GetInstanceID() == selectedMaterial.GetInstanceID()) found = true;
			}
			if (!found)
			{
				errorMessage("Something (likely an animation controller, or a custom script) has interfered with the Fur Grooming. The selected material is no longer attached to the selected renderer.");
				return;
			}

			if (selectedFastFurMat.material.GetTexture("_FurShapeMap").GetInstanceID() != furShapeRenderTextureFinal.GetInstanceID())
			{
				string error = "Something (likely an animation controller, or a custom script) has interfered with the Fur Grooming. The selected fur shape texture was modified while the Fur Grooming was running: '";
				error += selectedFastFurMat.material.GetTexture("_FurShapeMap").GetInstanceID() + "' -> '" + furShapeRenderTextureFinal.GetInstanceID() + "'";
				errorMessage(error);
				return;
			}

			if (selectedFastFurMat.material.GetTexture("_MainTex").GetInstanceID() != albedoRenderTextureFinal.GetInstanceID())
			{
				string error = "Something (likely an animation controller, or a custom script) has interfered with the Fur Grooming. The selected albedo texture was modified while the Fur Grooming was running: '";
				error += selectedFastFurMat.material.GetTexture("_MainTex").GetInstanceID() + "' -> '" + albedoRenderTextureFinal.GetInstanceID() + "'";
				errorMessage(error);
				return;
			}

			if (myMeshCollider == null)
			{
				errorMessage("Something (likely an animation controller, or a custom script) has interfered with the Fur Grooming. The mesh collider was removed.");
				return;
			}

			Camera[] cameras = Camera.allCameras;
			foreach (Camera camera in cameras)
			{
				if (camera.isActiveAndEnabled && camera != myCamera)
				{
					errorMessage("Something (likely an animation controller, or a custom script) has interfered with the Fur Grooming. The active camera was changed while the Fur Grooming was running.");
					return;
				}
			}

			FastFurGrooming[] groomingPrefabs = (FastFurGrooming[])Resources.FindObjectsOfTypeAll(typeof(FastFurGrooming));
			activeGroomers = 0;
			foreach (FastFurGrooming groomer in groomingPrefabs) if (groomer.isActiveAndEnabled) activeGroomers++;
			if (activeGroomers > 1)
			{
				errorMessage("There is more than 1 active copy of the Fur Grooming. Please remove the other Fur Grooming prefabs from the active Heirarchy.");
				return;
			}
		}

		catch (System.Exception e)
		{
			Debug.LogError(e.ToString());
			errorMessage("Failed Sanity Check: " + e.ToString());
			return;
		}
	}
	#endregion
}
#endif
