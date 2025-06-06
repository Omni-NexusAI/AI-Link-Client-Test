using UnityEngine;
using UnityEngine.UI;

#if UNITY_EDITOR
using UnityEngine.XR;
#endif

public class EditorTestMode : MonoBehaviour
{
    public RawImage webcamDisplay; // Assign a UI RawImage in the Inspector
    private WebCamTexture webCamTexture;

    void Start()
    {
        bool shouldSimulate = false;

#if UNITY_EDITOR
        // Explicitly check Application.isEditor as well for extra safety
        // Check if an XR device is active
        if (Application.isEditor && !XRSettings.isDeviceActive)
        {
            shouldSimulate = true;
            Debug.Log("EDITOR MODE: No XR device active. Attempting webcam simulation.");
        }
        else
        {
            Debug.Log("EDITOR MODE: XR device active or not in editor. Webcam simulation disabled.");
        }
#else
        // Build path: Simulation should always be disabled.
        Debug.Log("BUILD MODE: Webcam simulation disabled.");
#endif

        if (shouldSimulate)
        {
            if (webcamDisplay != null)
            {
                webcamDisplay.gameObject.SetActive(true); // Ensure it's active if simulating
            }
            else
            {
                 Debug.LogWarning("EditorTestMode: webcamDisplay RawImage is not assigned in the inspector. Cannot show simulation.");
                 // Disable self if display is missing, as simulation is pointless
                 enabled = false;
                 return;
            }
            InitializeWebcam();
        }
        else
        {
            // Disable the parent Canvas GameObject if not simulating
            if (webcamDisplay != null && webcamDisplay.transform.parent != null)
            {
                 // Assuming the RawImage is directly under the Canvas
                webcamDisplay.transform.parent.gameObject.SetActive(false);
                Debug.Log("Disabling WebcamCanvas GameObject.");
            }
            else if (webcamDisplay != null)
            {
                 // Fallback: disable just the RawImage if parent is somehow null
                 webcamDisplay.gameObject.SetActive(false);
                 Debug.LogWarning("WebcamDisplay parent not found, disabling RawImage only.");
            }

            // Disable this script component if not simulating in editor, destroy in build
#if UNITY_EDITOR
             enabled = false; // Keep component but disable update loop etc.
#else
            Destroy(this); // Remove component entirely in builds
#endif
        }
    }

     void InitializeWebcam()
    {
        if (WebCamTexture.devices.Length == 0)
        {
            Debug.LogError("EditorTestMode: No webcam devices found!");
            return;
        }

        // Use the default webcam
        webCamTexture = new WebCamTexture();

        if (webcamDisplay != null)
        {
            webcamDisplay.texture = webCamTexture;
            webcamDisplay.material.mainTexture = webCamTexture; // Ensure material uses the texture
        }
        else
        {
            Debug.LogWarning("EditorTestMode: No RawImage assigned for webcam display.");
            // Optionally, create a simple plane to display the texture if no UI is assigned
            // CreateWebcamPlane();
        }

        webCamTexture.Play();
        Debug.Log($"EditorTestMode: Started webcam '{webCamTexture.deviceName}'");
    }

    void OnDisable()
    {
        CleanupWebcam();
    }

    void OnDestroy()
    {
        CleanupWebcam();
    }

    void OnApplicationQuit()
    {
        CleanupWebcam();
    }

    private void CleanupWebcam()
    {
        if (webCamTexture != null)
        {
            if (webCamTexture.isPlaying)
            {
                webCamTexture.Stop();
                Debug.Log("EditorTestMode: Stopped webcam texture");
            }

            // Clear references to the texture
            if (webcamDisplay != null)
            {
                webcamDisplay.texture = null;
                if (webcamDisplay.material != null)
                {
                    webcamDisplay.material.mainTexture = null;
                }
            }

            // Destroy the texture
            Destroy(webCamTexture);
            webCamTexture = null;
            Debug.Log("EditorTestMode: Cleaned up webcam resources");
        }
    }

    // Optional: Method to create a simple plane for display if no UI RawImage is set
    /*
    void CreateWebcamPlane()
    {
        GameObject plane = GameObject.CreatePrimitive(PrimitiveType.Plane);
        plane.name = "WebcamDisplayPlane";
        plane.transform.SetParent(transform); // Attach to this GameObject
        plane.transform.localPosition = new Vector3(0, 0, 1); // Position in front
        plane.transform.localRotation = Quaternion.Euler(90, 180, 0); // Orient correctly
        plane.transform.localScale = new Vector3(0.16f, 0.1f, 0.09f); // Adjust scale as needed (e.g., 16:9 aspect ratio)

        Renderer planeRenderer = plane.GetComponent<Renderer>();
        if (planeRenderer != null)
        {
            planeRenderer.material = new Material(Shader.Find("Unlit/Texture"));
            planeRenderer.material.mainTexture = webCamTexture;
        }
        Destroy(plane.GetComponent<Collider>()); // Remove collider
    }
    */
}
