/*
 * Author: Tobias Sorg
 * Date: 2025-01-15
 * Description: Unity Editor tools for setting up DECIDE VR framework scenes
 * License: GPLv3
 */

#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEngine.XR.Interaction.Toolkit;
using DECIDE.Core;
using DECIDE.VR;
using DECIDE.UI;
using DECIDE.Avatars;
using DECIDE.Terrain;

namespace DECIDE.Editor {
    /// <summary>
    /// Editor tools for setting up DECIDE framework scenes
    /// </summary>
    public class DecideEditorTools : EditorWindow {
        private ScenarioConfiguration _scenarioConfig;
        private bool _createVRRig = true;
        private bool _createManagers = true;
        private bool _createTerrain = true;
        private bool _createUI = true;
        private bool _createLighting = true;
        
        private Vector2 _scrollPosition;
        private GUIStyle _headerStyle;
        private GUIStyle _subHeaderStyle;
        
        [MenuItem("DECIDE/Setup Tools")]
        public static void ShowWindow() {
            DecideEditorTools window = GetWindow<DecideEditorTools>("DECIDE Setup");
            window.minSize = new Vector2(400, 600);
        }
        
        [MenuItem("DECIDE/Create Base Scene")]
        public static void CreateBaseScene() {
            CreateCompleteSceneSetup();
        }
        
        [MenuItem("DECIDE/Managers/Create Scenario Manager")]
        public static void CreateScenarioManager() {
            CreateManager<ScenarioManager>("DECIDE_ScenarioManager");
        }
        
        [MenuItem("DECIDE/Managers/Create Stress Manager")]
        public static void CreateStressManager() {
            CreateManager<StressManager>("DECIDE_StressManager");
        }
        
        [MenuItem("DECIDE/Managers/Create Metrics Manager")]
        public static void CreateMetricsManager() {
            CreateManager<MetricsManager>("DECIDE_MetricsManager");
        }
        
        private void OnEnable() {
            InitializeStyles();
        }
        
        private void InitializeStyles() {
            _headerStyle = new GUIStyle {
                fontSize = 16,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white },
                margin = new RectOffset(0, 0, 10, 10)
            };
            
            _subHeaderStyle = new GUIStyle {
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.8f, 0.8f, 0.8f) },
                margin = new RectOffset(0, 0, 5, 5)
            };
        }
        
        private void OnGUI() {
            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);
            
            // Title
            EditorGUILayout.LabelField("DECIDE VR Framework Setup", _headerStyle);
            EditorGUILayout.Space(10);
            
            // Scenario Configuration
            EditorGUILayout.LabelField("Scenario Configuration", _subHeaderStyle);
            _scenarioConfig = EditorGUILayout.ObjectField("Config Asset", _scenarioConfig, 
                typeof(ScenarioConfiguration), false) as ScenarioConfiguration;
            
            if (GUILayout.Button("Create New Configuration")) {
                CreateScenarioConfiguration();
            }
            
            EditorGUILayout.Space(10);
            
            // Setup Options
            EditorGUILayout.LabelField("Setup Options", _subHeaderStyle);
            _createVRRig = EditorGUILayout.Toggle("Create VR Rig", _createVRRig);
            _createManagers = EditorGUILayout.Toggle("Create Managers", _createManagers);
            _createTerrain = EditorGUILayout.Toggle("Create Terrain", _createTerrain);
            _createUI = EditorGUILayout.Toggle("Create UI", _createUI);
            _createLighting = EditorGUILayout.Toggle("Create Lighting", _createLighting);
            
            EditorGUILayout.Space(20);
            
            // Main Setup Button
            if (GUILayout.Button("Create Complete Scene", GUILayout.Height(40))) {
                if (EditorUtility.DisplayDialog("Create DECIDE Scene",
                    "This will create a complete DECIDE VR scene setup. Continue?",
                    "Yes", "Cancel")) {
                    CreateCompleteSceneSetup();
                }
            }
            
            EditorGUILayout.Space(10);
            
            // Individual Creation Buttons
            EditorGUILayout.LabelField("Individual Components", _subHeaderStyle);
            
            if (GUILayout.Button("Create VR Rig Only")) {
                CreateVRRig();
            }
            
            if (GUILayout.Button("Create Managers Only")) {
                CreateAllManagers();
            }
            
            if (GUILayout.Button("Create Terrain Only")) {
                CreateUrbanTerrain();
            }
            
            if (GUILayout.Button("Create UI Only")) {
                CreateUISystem();
            }
            
            EditorGUILayout.Space(10);
            
            // Validation
            EditorGUILayout.LabelField("Scene Validation", _subHeaderStyle);
            
            if (GUILayout.Button("Validate Current Scene")) {
                ValidateScene();
            }
            
            if (GUILayout.Button("Fix Common Issues")) {
                FixCommonIssues();
            }
            
            EditorGUILayout.EndScrollView();
        }
        
        /// <summary>
        /// Creates a complete scene setup
        /// </summary>
        private static void CreateCompleteSceneSetup() {
            // Clear existing setup
            if (GameObject.Find("DECIDE_Core") != null) {
                if (!EditorUtility.DisplayDialog("Existing Setup Found",
                    "An existing DECIDE setup was found. Replace it?",
                    "Replace", "Cancel")) {
                    return;
                }
                DestroyImmediate(GameObject.Find("DECIDE_Core"));
            }
            
            // Create core container
            GameObject coreContainer = new GameObject("DECIDE_Core");
            
            // Create VR Rig
            GameObject vrRig = CreateVRRig();
            
            // Create Managers
            GameObject managers = CreateAllManagers();
            managers.transform.SetParent(coreContainer.transform);
            
            // Create Terrain
            GameObject terrain = CreateUrbanTerrain();
            
            // Create UI
            GameObject ui = CreateUISystem();
            ui.transform.SetParent(coreContainer.transform);
            
            // Create Lighting
            CreateLighting();
            
            // Create Avatar Pool
            GameObject avatarPool = CreateAvatarPool();
            avatarPool.transform.SetParent(managers.transform);
            
            // Setup references
            SetupReferences(managers, vrRig, ui, avatarPool, terrain);
            
            Debug.Log("DECIDE VR Scene setup complete!");
            EditorUtility.DisplayDialog("Setup Complete",
                "DECIDE VR scene has been set up successfully!\n\n" +
                "Next steps:\n" +
                "1. Configure the ScenarioConfiguration asset\n" +
                "2. Add avatar prefabs to the AvatarPool\n" +
                "3. Configure stressors in StressManager\n" +
                "4. Test in VR",
                "OK");
        }
        
        /// <summary>
        /// Creates the VR rig with XR Interaction Toolkit
        /// </summary>
        private static GameObject CreateVRRig() {
            // Check for existing rig
            GameObject existingRig = GameObject.Find("XR Origin (XR Rig)");
            if (existingRig != null) {
                return existingRig;
            }
            
            // Create XR Rig
            GameObject xrRig = new GameObject("XR Origin (XR Rig)");
            XROrigin xrOrigin = xrRig.AddComponent<XROrigin>();
            xrRig.AddComponent<CharacterController>();
            
            // Create Camera Offset
            GameObject cameraOffset = new GameObject("Camera Offset");
            cameraOffset.transform.SetParent(xrRig.transform);
            cameraOffset.transform.localPosition = new Vector3(0, 1.36144f, 0);
            xrOrigin.CameraFloorOffsetObject = cameraOffset;
            
            // Create Main Camera
            GameObject mainCamera = new GameObject("Main Camera");
            mainCamera.transform.SetParent(cameraOffset.transform);
            mainCamera.transform.localPosition = Vector3.zero;
            mainCamera.tag = "MainCamera";
            
            Camera camera = mainCamera.AddComponent<Camera>();
            camera.nearClipPlane = 0.01f;
            mainCamera.AddComponent<AudioListener>();
            
            // Create Controllers
            GameObject leftController = CreateController("LeftHand Controller", XRNode.LeftHand);
            leftController.transform.SetParent(cameraOffset.transform);
            
            GameObject rightController = CreateController("RightHand Controller", XRNode.RightHand);
            rightController.transform.SetParent(cameraOffset.transform);
            
            // Add VR Interaction Controller
            GameObject interactionController = new GameObject("VR Interaction Controller");
            interactionController.transform.SetParent(xrRig.transform);
            VRInteractionController vrInteraction = interactionController.AddComponent<VRInteractionController>();
            
            // Set references
            xrOrigin.Camera = camera;
            
            Debug.Log("VR Rig created successfully");
            return xrRig;
        }
        
        /// <summary>
        /// Creates a controller gameobject with XR components
        /// </summary>
        private static GameObject CreateController(string name, XRNode node) {
            GameObject controller = new GameObject(name);
            
            XRController xrController = controller.AddComponent<XRController>();
            xrController.controllerNode = node;
            
            // Add Ray Interactor for UI interaction
            XRRayInteractor rayInteractor = controller.AddComponent<XRRayInteractor>();
            LineRenderer lineRenderer = controller.AddComponent<LineRenderer>();
            
            // Configure line renderer
            lineRenderer.startWidth = 0.01f;
            lineRenderer.endWidth = 0.01f;
            lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
            lineRenderer.startColor = Color.white;
            lineRenderer.endColor = new Color(1, 1, 1, 0.1f);
            
            // Add controller model placeholder
            GameObject model = GameObject.CreatePrimitive(PrimitiveType.Cube);
            model.name = "Controller Model";
            model.transform.SetParent(controller.transform);
            model.transform.localScale = new Vector3(0.05f, 0.05f, 0.1f);
            
            return controller;
        }
        
        /// <summary>
        /// Creates all manager systems
        /// </summary>
        private static GameObject CreateAllManagers() {
            GameObject managers = new GameObject("Managers");
            
            // Scenario Manager
            GameObject scenarioManager = CreateManager<ScenarioManager>("ScenarioManager");
            scenarioManager.transform.SetParent(managers.transform);
            
            // Stress Manager
            GameObject stressManager = CreateManager<StressManager>("StressManager");
            stressManager.transform.SetParent(managers.transform);
            
            // Metrics Manager
            GameObject metricsManager = CreateManager<MetricsManager>("MetricsManager");
            metricsManager.transform.SetParent(managers.transform);
            
            Debug.Log("All managers created successfully");
            return managers;
        }
        
        /// <summary>
        /// Creates a specific manager
        /// </summary>
        private static GameObject CreateManager<T>(string name) where T : Component {
            GameObject managerObject = new GameObject(name);
            T manager = managerObject.AddComponent<T>();
            
            // Configure manager based on type
            if (manager is ScenarioManager scenarioManager) {
                // Create default configuration if needed
                ScenarioConfiguration config = CreateOrGetDefaultConfiguration();
                SerializedObject serializedManager = new SerializedObject(scenarioManager);
                SerializedProperty configProp = serializedManager.FindProperty("_configuration");
                if (configProp != null) {
                    configProp.objectReferenceValue = config;
                    serializedManager.ApplyModifiedProperties();
                }
            }
            
            return managerObject;
        }
        
        /// <summary>
        /// Creates the urban terrain
        /// </summary>
        private static GameObject CreateUrbanTerrain() {
            GameObject terrain = new GameObject("Urban Terrain");
            
            // Add terrain generator
            UrbanTerrainGenerator generator = terrain.AddComponent<UrbanTerrainGenerator>();
            
            // Generate basic terrain
            generator.GenerateTerrain();
            
            Debug.Log("Urban terrain created");
            return terrain;
        }
        
        /// <summary>
        /// Creates the UI system
        /// </summary>
        private static GameObject CreateUISystem() {
            GameObject uiSystem = new GameObject("UI System");
            
            // Create HUD
            GameObject hudObject = new GameObject("HUD");
            hudObject.transform.SetParent(uiSystem.transform);
            
            Canvas hudCanvas = hudObject.AddComponent<Canvas>();
            hudCanvas.renderMode = RenderMode.WorldSpace;
            hudObject.AddComponent<CanvasScaler>();
            hudObject.AddComponent<GraphicRaycaster>();
            
            HUDController hudController = hudObject.AddComponent<HUDController>();
            
            // Create basic HUD elements
            CreateHUDElements(hudObject);
            
            Debug.Log("UI System created");
            return uiSystem;
        }
        
        /// <summary>
        /// Creates basic HUD elements
        /// </summary>
        private static void CreateHUDElements(GameObject hudObject) {
            // Timer Text
            GameObject timerObject = new GameObject("Timer");
            timerObject.transform.SetParent(hudObject.transform);
            Text timerText = timerObject.AddComponent<Text>();
            timerText.text = "00:00";
            timerText.fontSize = 24;
            timerText.alignment = TextAnchor.MiddleCenter;
            
            RectTransform timerRect = timerObject.GetComponent<RectTransform>();
            timerRect.sizeDelta = new Vector2(200, 50);
            timerRect.anchoredPosition = new Vector2(0, 100);
        }
        
        /// <summary>
        /// Creates avatar pool system
        /// </summary>
        private static GameObject CreateAvatarPool() {
            GameObject poolObject = new GameObject("Avatar Pool");
            AvatarPool pool = poolObject.AddComponent<AvatarPool>();
            
            Debug.Log("Avatar Pool created - Remember to add avatar prefabs!");
            return poolObject;
        }
        
        /// <summary>
        /// Creates scene lighting
        /// </summary>
        private static void CreateLighting() {
            // Directional Light
            GameObject lightObject = new GameObject("Directional Light");
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1f;
            light.color = Color.white;
            lightObject.transform.rotation = Quaternion.Euler(45f, -30f, 0);
            
            // Configure ambient lighting
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.5f, 0.7f, 0.9f);
            RenderSettings.ambientEquatorColor = new Color(0.4f, 0.5f, 0.6f);
            RenderSettings.ambientGroundColor = new Color(0.2f, 0.2f, 0.2f);
            
            Debug.Log("Lighting configured");
        }
        
        /// <summary>
        /// Sets up references between components
        /// </summary>
        private static void SetupReferences(GameObject managers, GameObject vrRig, 
            GameObject ui, GameObject avatarPool, GameObject terrain) {
            
            // Get components
            ScenarioManager scenarioManager = managers.GetComponentInChildren<ScenarioManager>();
            HUDController hudController = ui.GetComponentInChildren<HUDController>();
            AvatarPool pool = avatarPool.GetComponent<AvatarPool>();
            Transform playerTransform = vrRig.transform;
            
            // Set references
            if (scenarioManager != null) {
                SerializedObject serializedManager = new SerializedObject(scenarioManager);
                
                SerializedProperty hudProp = serializedManager.FindProperty("_hudController");
                if (hudProp != null) hudProp.objectReferenceValue = hudController;
                
                SerializedProperty poolProp = serializedManager.FindProperty("_avatarPool");
                if (poolProp != null) poolProp.objectReferenceValue = pool;
                
                SerializedProperty playerProp = serializedManager.FindProperty("_playerTransform");
                if (playerProp != null) playerProp.objectReferenceValue = playerTransform;
                
                serializedManager.ApplyModifiedProperties();
            }
        }
        
        /// <summary>
        /// Creates or gets the default scenario configuration
        /// </summary>
        private static ScenarioConfiguration CreateOrGetDefaultConfiguration() {
            string path = "Assets/DECIDE/Configurations/DefaultScenarioConfig.asset";
            ScenarioConfiguration config = AssetDatabase.LoadAssetAtPath<ScenarioConfiguration>(path);
            
            if (config == null) {
                config = CreateScenarioConfiguration();
            }
            
            return config;
        }
        
        /// <summary>
        /// Creates a new scenario configuration asset
        /// </summary>
        private static ScenarioConfiguration CreateScenarioConfiguration() {
            ScenarioConfiguration config = ScriptableObject.CreateInstance<ScenarioConfiguration>();
            config.SetDefaults();
            
            string path = "Assets/DECIDE/Configurations";
            if (!AssetDatabase.IsValidFolder(path)) {
                System.IO.Directory.CreateDirectory(path);
            }
            
            string assetPath = AssetDatabase.GenerateUniqueAssetPath(path + "/ScenarioConfig.asset");
            AssetDatabase.CreateAsset(config, assetPath);
            AssetDatabase.SaveAssets();
            
            EditorUtility.FocusProjectWindow();
            Selection.activeObject = config;
            
            Debug.Log($"Created ScenarioConfiguration at {assetPath}");
            return config;
        }
        
        /// <summary>
        /// Validates the current scene setup
        /// </summary>
        private void ValidateScene() {
            bool hasIssues = false;
            System.Text.StringBuilder report = new System.Text.StringBuilder();
            report.AppendLine("Scene Validation Report:");
            report.AppendLine("------------------------");
            
            // Check for VR Rig
            if (GameObject.Find("XR Origin (XR Rig)") == null) {
                report.AppendLine("❌ Missing VR Rig");
                hasIssues = true;
            } else {
                report.AppendLine("✓ VR Rig found");
            }
            
            // Check for Managers
            if (FindObjectOfType<ScenarioManager>() == null) {
                report.AppendLine("❌ Missing ScenarioManager");
                hasIssues = true;
            } else {
                report.AppendLine("✓ ScenarioManager found");
            }
            
            if (FindObjectOfType<StressManager>() == null) {
                report.AppendLine("❌ Missing StressManager");
                hasIssues = true;
            } else {
                report.AppendLine("✓ StressManager found");
            }
            
            if (FindObjectOfType<MetricsManager>() == null) {
                report.AppendLine("❌ Missing MetricsManager");
                hasIssues = true;
            } else {
                report.AppendLine("✓ MetricsManager found");
            }
            
            // Check for Avatar Pool
            if (FindObjectOfType<AvatarPool>() == null) {
                report.AppendLine("❌ Missing AvatarPool");
                hasIssues = true;
            } else {
                report.AppendLine("✓ AvatarPool found");
            }
            
            // Check for HUD
            if (FindObjectOfType<HUDController>() == null) {
                report.AppendLine("❌ Missing HUDController");
                hasIssues = true;
            } else {
                report.AppendLine("✓ HUDController found");
            }
            
            // Display report
            EditorUtility.DisplayDialog("Scene Validation",
                report.ToString(),
                hasIssues ? "Fix Issues" : "OK");
            
            if (hasIssues) {
                Debug.LogWarning(report.ToString());
            } else {
                Debug.Log(report.ToString());
            }
        }
        
        /// <summary>
        /// Attempts to fix common setup issues
        /// </summary>
        private void FixCommonIssues() {
            bool fixedAny = false;
            
            // Fix missing VR Rig
            if (GameObject.Find("XR Origin (XR Rig)") == null) {
                CreateVRRig();
                fixedAny = true;
                Debug.Log("Fixed: Created missing VR Rig");
            }
            
            // Fix missing managers
            if (FindObjectOfType<ScenarioManager>() == null) {
                CreateManager<ScenarioManager>("ScenarioManager");
                fixedAny = true;
                Debug.Log("Fixed: Created missing ScenarioManager");
            }
            
            if (FindObjectOfType<StressManager>() == null) {
                CreateManager<StressManager>("StressManager");
                fixedAny = true;
                Debug.Log("Fixed: Created missing StressManager");
            }
            
            if (FindObjectOfType<MetricsManager>() == null) {
                CreateManager<MetricsManager>("MetricsManager");
                fixedAny = true;
                Debug.Log("Fixed: Created missing MetricsManager");
            }
            
            if (fixedAny) {
                EditorUtility.DisplayDialog("Issues Fixed",
                    "Common issues have been fixed. Please validate the scene again.",
                    "OK");
            } else {
                EditorUtility.DisplayDialog("No Issues Found",
                    "No common issues were detected.",
                    "OK");
            }
        }
    }
}
#endif