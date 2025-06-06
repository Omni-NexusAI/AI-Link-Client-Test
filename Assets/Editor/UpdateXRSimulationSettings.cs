using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

[ExecuteInEditMode]
public class UpdateXRSimulationSettings : MonoBehaviour
{
    public void UpdateSettings()
    {
        // Find all cameras in the scene and update their background color
        Camera[] cameras = FindObjectsOfType<Camera>();
        foreach (Camera cam in cameras)
        {
            if (cam.gameObject.name.Contains("Camera") || cam.gameObject.name.Contains("Eye"))
            {
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0.15f, 0.15f, 0.15f, 1.0f);
                Debug.Log("Updated camera background color: " + cam.gameObject.name);
            }
        }

        // Find trackables and align them
        GameObject[] allObjects = FindObjectsOfType<GameObject>();
        foreach (GameObject obj in allObjects)
        {
            if (obj.name.Contains("Trackables") || obj.name.Contains("Plane"))
            {
                Vector3 pos = obj.transform.position;
                obj.transform.position = new Vector3(pos.x, 0f, pos.z);
                Debug.Log("Aligned trackables: " + obj.name);
            }
        }
        
        // Update OVRPassthroughLayer if present
        var passthroughLayers = FindObjectsOfType<OVRPassthroughLayer>();
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

        // Save the scene
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
        Debug.Log("Scene saved with updated XR simulation settings");
    }
}

// Add a menu item to execute the update
public class UpdateXRSimulationSettingsMenu
{
    [MenuItem("XR/Update XR Simulation Settings")]
    static void UpdateSettings()
    {
        var go = new GameObject("XR Simulation Settings Updater");
        var updater = go.AddComponent<UpdateXRSimulationSettings>();
        updater.UpdateSettings();
        Object.DestroyImmediate(go);
        Debug.Log("XR Simulation settings updated");
    }
    
    [MenuItem("XR/Fix Simulated Environment")]
    static void FixSimulatedEnvironment()
    {
        // Find environment loader
        SimulatedEnvironmentLoader[] loaders = Object.FindObjectsOfType<SimulatedEnvironmentLoader>(true);
        
        if (loaders.Length > 0)
        {
            foreach (var loader in loaders)
            {
                // Enable force load in edit mode
                loader.forceLoadInEditMode = true;
                Debug.Log("Updated SimulatedEnvironmentLoader on: " + loader.gameObject.name);
            }
        }
        else
        {
            // Create a new one if not found
            var go = new GameObject("XR Simulated Environment Loader");
            var loader = go.AddComponent<SimulatedEnvironmentLoader>();
            loader.forceLoadInEditMode = true;
            Debug.Log("Created new SimulatedEnvironmentLoader");
        }
        
        // Save changes
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
    }
}