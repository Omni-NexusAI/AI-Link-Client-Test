using UnityEngine;

/// <summary>
/// Adjusts XR simulation settings at runtime to match Oculus passthrough and align trackables plane.
/// Attach to any GameObject in your scene.
/// </summary>
public class FixXRSimulationSettings : MonoBehaviour
{
    [Tooltip("The color to use for the simulated passthrough camera background")]
    public Color passthroughColor = new Color(0.15f, 0.15f, 0.15f, 1.0f); // Dark gray matching Oculus passthrough

    private void Start()
    {
        // Set the passthrough camera color
        UpdatePassthroughColor();
        
        // Align the trackables plane with the simulated environment
        AlignTrackablesPlane();
    }

    private void UpdatePassthroughColor()
    {
        // Find all cameras in the scene and update their background color
        Camera[] cameras = FindObjectsByType<Camera>(FindObjectsSortMode.None);
        foreach (Camera cam in cameras)
        {
            // Check if this is the main camera or XR camera
            if (cam.gameObject.name.Contains("Camera") || cam.gameObject.name.Contains("Eye"))
            {
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = passthroughColor;
                Debug.Log("[FixXRSimulationSettings] Updated camera background color to match Oculus passthrough: " + cam.gameObject.name);
            }
        }
    }

    private void AlignTrackablesPlane()
    {
        // Find any GameObject with "Trackables" in the name and align it to y=0
        GameObject[] allObjects = FindObjectsByType<GameObject>(FindObjectsSortMode.None);
        foreach (GameObject obj in allObjects)
        {
            if (obj.name.Contains("Trackables") || obj.name.Contains("Plane"))
            {
                Vector3 currentPosition = obj.transform.position;
                obj.transform.position = new Vector3(currentPosition.x, 0f, currentPosition.z);
                Debug.Log("[FixXRSimulationSettings] Aligned trackables plane with simulated environment floor: " + obj.name);
            }
        }
    }
}