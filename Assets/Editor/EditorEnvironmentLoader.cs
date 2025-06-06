using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

[InitializeOnLoad]
public static class EditorEnvironmentLoader
{
    static EditorEnvironmentLoader()
    {
        // This constructor is called when the editor is started or scripts are recompiled
        EditorApplication.delayCall += LoadEnvironmentInEditor;
    }

    [MenuItem("XR/Show Simulated Environment in Edit Mode")]
    public static void LoadEnvironmentInEditor()
    {
        // Find existing environment loader components
        SimulatedEnvironmentLoader[] loaders = Object.FindObjectsOfType<SimulatedEnvironmentLoader>(true);
        
        if (loaders.Length > 0)
        {
            Debug.Log("Found existing SimulatedEnvironmentLoader components.");
            foreach (var loader in loaders)
            {
                // Enable the force load option
                loader.forceLoadInEditMode = true;
                
                // Try to load the environment directly
                LoadEnvironment(loader);
            }
        }
        else
        {
            Debug.Log("No SimulatedEnvironmentLoader found. Create one using GameObject menu > XR > Simulated Environment Loader");
        }
    }
    
    [MenuItem("GameObject/XR/Simulated Environment Loader", false, 10)]
    static void CreateEnvironmentLoader(MenuCommand menuCommand)
    {
        // Create a new GameObject with a SimulatedEnvironmentLoader
        GameObject go = new GameObject("XR Simulated Environment Loader");
        GameObjectUtility.SetParentAndAlign(go, menuCommand.context as GameObject);
        
        // Add the loader component
        SimulatedEnvironmentLoader loader = go.AddComponent<SimulatedEnvironmentLoader>();
        loader.forceLoadInEditMode = true;
        
        // Try to load the environment
        LoadEnvironment(loader);
        
        // Register undo operation
        Undo.RegisterCreatedObjectUndo(go, "Create Simulated Environment Loader");
        
        // Select the new GameObject
        Selection.activeObject = go;
    }
    
    private static void LoadEnvironment(SimulatedEnvironmentLoader loader)
    {
        if (loader == null) return;
        
        // Try to get the environment prefab if not set
        if (loader.simulatedEnvironmentPrefab == null)
        {
            // Direct reference to the default XR simulation environment prefab
            string prefabPath = "Packages/com.unity.xr.arfoundation/Assets/Prefabs/DefaultSimulationEnvironment.prefab";
            loader.simulatedEnvironmentPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            
            if (loader.simulatedEnvironmentPrefab == null)
            {
                loader.simulatedEnvironmentPrefab = Resources.Load<GameObject>("XRSimulationEnvironment");
            }
        }
        
        // Mark the scene as dirty to save changes
        if (loader.simulatedEnvironmentPrefab != null)
        {
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log("XR environment prefab loaded in Editor mode");
        }
    }
    
    [MenuItem("XR/Fix Passthrough Color")]
    public static void FixPassthroughColor()
    {
        // Find all cameras in the scene and update their background color
        Camera[] cameras = Object.FindObjectsOfType<Camera>();
        foreach (Camera cam in cameras)
        {
            if (cam.gameObject.name.Contains("Camera") || cam.gameObject.name.Contains("Eye"))
            {
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0.15f, 0.15f, 0.15f, 1.0f);
                Debug.Log("Updated camera background color: " + cam.gameObject.name);
            }
        }
        
        // Update OVRPassthroughLayer if present
        var passthroughLayers = Object.FindObjectsOfType<OVRPassthroughLayer>();
        foreach (var layer in passthroughLayers)
        {
            if (layer != null)
            {
                // Force background color settings
                layer.textureOpacity = 1.0f;
                layer.edgeColor = new Color(0.15f, 0.15f, 0.15f, 1.0f);
                layer.colorMapEditorType = OVRPassthroughLayer.ColorMapEditorType.None;
                Debug.Log("Updated OVRPassthroughLayer on: " + layer.gameObject.name);
            }
        }
        
        // Mark the scene as dirty to save changes
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
    }
    
    [MenuItem("XR/Fix Trackables Alignment")]
    public static void FixTrackablesAlignment()
    {
        // Find trackables and align them
        GameObject[] allObjects = Object.FindObjectsOfType<GameObject>();
        foreach (GameObject obj in allObjects)
        {
            if (obj.name.Contains("Trackables") || obj.name.Contains("Plane"))
            {
                Vector3 pos = obj.transform.position;
                obj.transform.position = new Vector3(pos.x, 0f, pos.z);
                Debug.Log("Aligned trackables: " + obj.name);
            }
        }
        
        // Mark the scene as dirty to save changes
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
    }
}