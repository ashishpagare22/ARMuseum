using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Unity.XR.CoreUtils;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using UnityEngine.XR.Management;
using System.IO;
using UnityEditor.XR.ARSubsystems;

public static class ARMuseumSetup
{
    private const string ScenePath = "Assets/Scenes/MobileARScene.unity";
    private const string LibraryPath = "Assets/Artworks/NationalGalleryImages.asset";

    [MenuItem("VRMuseum/Import Painting Photos into AR Library")]
    public static void ImportPaintingPhotos()
    {
        var artworkFiles = new[]
        {
            new ArtworkImport("IMG_3689", "Saint Francis Receiving the Stigmata", "El Greco", "1590–1595", "Oil on canvas", "Saint Francis looks heavenwards as turbulent clouds frame his vision. The skull beside him is a reminder of human mortality.", new Vector2(1.04f, 1.14f)),
            new ArtworkImport("IMG_3688", "The Resurrection", "Antonio Palma", "c. 1535–1580", "Oil on panel", "Christ rises above the crowded scene as angels and witnesses gather around him. The work is a large Venetian Renaissance altarpiece.", new Vector2(1.85f, 2.11f)),
            new ArtworkImport("IMG_3719", "Saint Mark's Square, Venice", "Canaletto", "c. 1756", "Oil on canvas", "Canaletto depicts the Campanile and Procuratie Nuove in the famous square. Its wide-angle view may have been planned with a camera obscura.", new Vector2(0.77f, 0.46f)),
            new ArtworkImport("IMG_3674", "The Gleaners", "Jules Breton", "1854", "Oil on canvas", "Women and children gather the remains of the harvest in Breton's native village of Courrières in northern France.", new Vector2(2.35f, 1.55f)),
            new ArtworkImport("IMG_3715", "Dockers", "Maurice MacGonigal", "1934", "Oil on canvas", "Three dock workers wait for work beside a red cargo ship. MacGonigal used models provided by trade-union leader James Larkin.", new Vector2(1.5f, 1.8f))
        };

        var library = AssetDatabase.LoadAssetAtPath<XRReferenceImageLibrary>(LibraryPath);
        if (library == null)
        {
            library = ScriptableObject.CreateInstance<XRReferenceImageLibrary>();
            AssetDatabase.CreateAsset(library, LibraryPath);
        }
        while (library.count > 0) library.RemoveAt(library.count - 1);

        var tracker = Object.FindFirstObjectByType<MuseumImageTracker>();
        if (tracker == null)
        {
            EditorUtility.DisplayDialog("Scene not configured", "Open MobileARScene and run Configure Mobile AR Scene first.", "OK");
            return;
        }

        var records = new ArtworkRecord[artworkFiles.Length];
        for (int i = 0; i < artworkFiles.Length; i++)
        {
            var item = artworkFiles[i];
            var sourcePath = $"Assets/Artworks/{item.fileName}.jpg";
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(sourcePath);
            if (texture == null)
            {
                Debug.LogError($"Painting photo is missing: {sourcePath}");
                continue;
            }
            library.Add();
            library.SetName(i, item.fileName);
            library.SetTexture(i, texture, true);
            library.SetSpecifySize(i, true);
            library.SetSize(i, item.size);
            records[i] = new ArtworkRecord { imageName = item.fileName, title = item.title, artist = item.artist, year = item.year, medium = item.medium, description = item.description };
        }

        var serialized = new SerializedObject(tracker);
        var array = serialized.FindProperty("artworks");
        array.arraySize = records.Length;
        for (int i = 0; i < records.Length; i++)
        {
            var element = array.GetArrayElementAtIndex(i);
            var record = records[i];
            element.FindPropertyRelative("imageName").stringValue = record.imageName;
            element.FindPropertyRelative("title").stringValue = record.title;
            element.FindPropertyRelative("artist").stringValue = record.artist;
            element.FindPropertyRelative("year").stringValue = record.year;
            element.FindPropertyRelative("medium").stringValue = record.medium;
            element.FindPropertyRelative("description").stringValue = record.description;
        }
        serialized.ApplyModifiedPropertiesWithoutUndo();

        // Essential TMP resources may exist while TMP_Settings has no active default font.
        var fallbackFont = TMP_Settings.defaultFontAsset;
        if (fallbackFont == null)
            fallbackFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
        if (fallbackFont != null)
        {
            foreach (var text in Object.FindObjectsByType<TMP_Text>(FindObjectsSortMode.None))
            {
                if (text.font == null)
                {
                    text.font = fallbackFont;
                    EditorUtility.SetDirty(text);
                }
            }
        }
        var manager = Object.FindFirstObjectByType<ARTrackedImageManager>();
        if (manager != null)
        {
            manager.referenceLibrary = library;
            manager.requestedMaxNumberOfMovingImages = artworkFiles.Length;
            manager.enabled = true;
            EditorUtility.SetDirty(manager);
        }
        EditorUtility.SetDirty(tracker);
        EditorUtility.SetDirty(library);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        EditorUtility.DisplayDialog("Painting photos imported", "Five painting images and artwork details are now in the AR reference library. The image manager is enabled in MobileARScene.", "OK");
    }

    private struct ArtworkImport
    {
        public string fileName, title, artist, year, medium, description;
        public Vector2 size;
        public ArtworkImport(string fileName, string title, string artist, string year, string medium, string description, Vector2 size)
        {
            this.fileName = fileName; this.title = title; this.artist = artist; this.year = year; this.medium = medium; this.description = description; this.size = size;
        }
    }

    [MenuItem("VRMuseum/Configure Mobile AR Scene")]
    public static void Configure()
    {
        EnsureFolders();
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        RenderSettings.ambientLight = new Color(0.55f, 0.55f, 0.55f);

        var sessionObject = new GameObject("AR Session");
        sessionObject.AddComponent<ARSession>();
        sessionObject.AddComponent<ARInputManager>();

        var origin = new GameObject("XR Origin (Mobile AR)");
        var cameraOffset = new GameObject("Camera Offset");
        cameraOffset.transform.SetParent(origin.transform, false);
        var cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener), typeof(ARCameraManager), typeof(ARCameraBackground));
        cameraObject.tag = "MainCamera";
        cameraObject.transform.SetParent(cameraOffset.transform, false);
        var camera = cameraObject.GetComponent<Camera>();
        camera.nearClipPlane = 0.01f;
        camera.farClipPlane = 100f;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = Color.black;

        var xrOrigin = origin.AddComponent<XROrigin>();
        xrOrigin.Camera = camera;
        xrOrigin.Origin = origin;
        xrOrigin.CameraFloorOffsetObject = cameraOffset;
        xrOrigin.RequestedTrackingOriginMode = XROrigin.TrackingOriginMode.Device;

        var imageManager = origin.AddComponent<ARTrackedImageManager>();
        var library = AssetDatabase.LoadAssetAtPath<XRReferenceImageLibrary>(LibraryPath);
        if (library == null)
        {
            library = ScriptableObject.CreateInstance<XRReferenceImageLibrary>();
            AssetDatabase.CreateAsset(library, LibraryPath);
        }
        imageManager.referenceLibrary = library;
        imageManager.requestedMaxNumberOfMovingImages = 1;
        imageManager.enabled = false; // Enable after adding the painting scans to the library.

        ConfigureARKitLoader();
        BuildInformationPanel(imageManager);

        EditorSceneManager.SaveScene(scene, ScenePath);
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        AssetDatabase.SaveAssets();
        Selection.activeObject = origin;
        EditorUtility.DisplayDialog("Mobile AR scene created",
            "The VR scene is preserved. The build now opens the Mobile AR scene. Add 3–5 painting images to Assets/Artworks/NationalGalleryImages, then enable AR Tracked Image Manager on XR Origin (Mobile AR).", "OK");
    }

    private static void BuildInformationPanel(ARTrackedImageManager manager)
    {
        var canvasObject = new GameObject("Museum Information UI", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(MuseumImageTracker));
        var canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1170, 2532);
        scaler.matchWidthOrHeight = 0.5f;

        var panel = new GameObject("Artwork Information Panel", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(canvasObject.transform, false);
        var panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.06f, 0.04f);
        panelRect.anchorMax = new Vector2(0.94f, 0.31f);
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;
        panel.GetComponent<Image>().color = new Color(0.06f, 0.08f, 0.10f, 0.92f);

        var status = CreateText("Status", panel.transform, new Vector2(0.08f, 0.78f), new Vector2(0.92f, 0.94f), 22, new Color(0.79f, 0.67f, 0.42f), FontStyles.Bold);
        var title = CreateText("Artwork Title", panel.transform, new Vector2(0.08f, 0.54f), new Vector2(0.92f, 0.78f), 38, Color.white, FontStyles.Bold);
        var details = CreateText("Artwork Details", panel.transform, new Vector2(0.08f, 0.08f), new Vector2(0.92f, 0.52f), 23, new Color(0.88f, 0.89f, 0.90f), FontStyles.Normal);
        details.textWrappingMode = TextWrappingModes.Normal;
        details.overflowMode = TextOverflowModes.Overflow;
        details.alignment = TextAlignmentOptions.TopLeft;
        status.text = "NATIONAL GALLERY · AR GUIDE";
        title.text = "Scan a painting";
        details.text = "Point your camera at one of the paintings in the guide. Its artwork details will appear here.";

        var tracker = canvasObject.GetComponent<MuseumImageTracker>();
        var serialized = new SerializedObject(tracker);
        serialized.FindProperty("imageManager").objectReferenceValue = manager;
        serialized.FindProperty("informationPanel").objectReferenceValue = panel;
        serialized.FindProperty("statusText").objectReferenceValue = status;
        serialized.FindProperty("titleText").objectReferenceValue = title;
        serialized.FindProperty("detailsText").objectReferenceValue = details;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static TextMeshProUGUI CreateText(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, float size, Color color, FontStyles style)
    {
        var textObject = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(parent, false);
        var rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = new Vector2(0, 0);
        rect.offsetMax = new Vector2(0, 0);
        var text = textObject.GetComponent<TextMeshProUGUI>();
        text.font = TMP_Settings.defaultFontAsset;
        if (text.font == null)
            text.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
        text.fontSize = size;
        text.color = color;
        text.fontStyle = style;
        text.alignment = TextAlignmentOptions.Left;
        text.textWrappingMode = TextWrappingModes.Normal;
        return text;
    }

    private static void EnsureFolders()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Editor"))
            AssetDatabase.CreateFolder("Assets", "Editor");
        if (!AssetDatabase.IsValidFolder("Assets/Scripts"))
            AssetDatabase.CreateFolder("Assets", "Scripts");
        if (!AssetDatabase.IsValidFolder("Assets/Artworks"))
            AssetDatabase.CreateFolder("Assets", "Artworks");
    }

    private static void ConfigureARKitLoader()
    {
        const string settingsPath = "Assets/XR/XRGeneralSettingsPerBuildTarget.asset";
        var buildTargetSettings = AssetDatabase.LoadAssetAtPath<XRGeneralSettingsPerBuildTarget>(settingsPath);
        if (buildTargetSettings == null)
        {
            Debug.LogWarning("XR Plug-in Management settings asset was not found; enable ARKit for iOS in Project Settings > XR Plug-in Management.");
            return;
        }

        if (!buildTargetSettings.HasSettingsForBuildTarget(BuildTargetGroup.iOS))
            buildTargetSettings.CreateDefaultSettingsForBuildTarget(BuildTargetGroup.iOS);
        if (buildTargetSettings.ManagerSettingsForBuildTarget(BuildTargetGroup.iOS) == null)
            buildTargetSettings.CreateDefaultManagerSettingsForBuildTarget(BuildTargetGroup.iOS);

        var iosSettings = XRGeneralSettingsPerBuildTarget.XRGeneralSettingsForBuildTarget(BuildTargetGroup.iOS);
        if (iosSettings == null || iosSettings.Manager == null ||
            !XRPackageMetadataStore.AssignLoader(iosSettings.Manager, "UnityEngine.XR.ARKit.ARKitLoader", BuildTargetGroup.iOS))
            Debug.LogWarning("ARKit loader could not be assigned automatically; enable ARKit for iOS in Project Settings > XR Plug-in Management.");

        PlayerSettings.iOS.cameraUsageDescription = "Use the camera to recognise National Gallery paintings and show artwork information.";
    }
}
