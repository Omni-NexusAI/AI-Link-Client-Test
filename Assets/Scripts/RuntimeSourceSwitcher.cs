using UnityEngine;
using UnityEngine.XR;
using System.Collections.Generic;

/// <summary>
/// Handles switching between XR passthrough mode and desktop camera mode based on runtime environment.
/// Attach to an empty GameObject or the XR Rig root.
/// </summary>
public class RuntimeSourceSwitcher : MonoBehaviour
{
    [Header("Assign in Inspector")]
    [Tooltip("GameObject holding OVRPassthroughLayer component")]
    public GameObject passthroughLayer;

    [Tooltip("Camera for desktop webcam capture")]
    public Camera desktopCaptureCamera;

    [Tooltip("UI Canvas that should be positioned correctly in front of the user")]
    public Canvas mainUICanvas;

    [Tooltip("DiagnosticsScreen prefab to attach to main camera")]
    public GameObject diagnosticsScreenPrefab;

    private bool _xrRunning = false;
    private Camera _mainCamera;
    private List<XRDisplaySubsystem> _displaySubsystems = new List<XRDisplaySubsystem>();

    void Awake()
    {
        // Find main camera if not explicitly set
        _mainCamera = Camera.main;
        if (_mainCamera == null)
        {
            Debug.LogWarning("Main camera not found. Some functionality may not work correctly.");
        }

        // Setup on Awake to ensure proper initialization before other components
        SetupEnvironment();
    }

    void Start()
    {
        // Double-check in Start in case XR system initializes after Awake
        SetupEnvironment();
    }

    private void SetupEnvironment()
    {
        // Check if XR is running
        _xrRunning = IsXRRunning();
        bool editorOnly = Application.isEditor && !_xrRunning;

        Debug.Log($"RuntimeSourceSwitcher: XR Running: {_xrRunning}, Editor Only: {editorOnly}");

        if (_xrRunning)
        {
            // Headset active → enable passthrough, disable desktop camera
            SetupXRMode();
        }
        else if (editorOnly)
        {
            // No XR display, running in Editor → show desktop camera feed
            SetupDesktopMode();
        }
    }

    private bool IsXRRunning()
    {
        // Use XRInitManager if available
        if (XRInitManager.Instance != null)
        {
            return XRInitManager.Instance.IsXRRunning();
        }

        // Fallback to direct check if XRInitManager is not available
        _displaySubsystems.Clear();
        SubsystemManager.GetSubsystems(_displaySubsystems);

        foreach (var displaySubsystem in _displaySubsystems)
        {
            if (displaySubsystem != null && displaySubsystem.running)
            {
                return true;
            }
        }

        return false;
    }

    private void SetupXRMode()
    {
        Debug.Log("Setting up XR Mode with passthrough");

        // Enable passthrough layer if available, but don't make it exclusive
        // This allows the simulated environment to be visible in HMD mode
        if (passthroughLayer != null)
        {
            passthroughLayer.SetActive(true);
        }
        else
        {
            Debug.LogWarning("PassthroughLayer not assigned in RuntimeSourceSwitcher");
        }

        // Disable desktop camera
        if (desktopCaptureCamera != null)
        {
            desktopCaptureCamera.gameObject.SetActive(false);
        }

        // Setup main camera for passthrough
        if (_mainCamera != null)
        {
            // Use Skybox instead of solid color to allow environment to be visible
            // This is the key change to make the environment visible in HMD mode
            _mainCamera.clearFlags = CameraClearFlags.Skybox;

            // Attach diagnostics screen if provided
            if (diagnosticsScreenPrefab != null)
            {
                GameObject diagnosticsScreen = Instantiate(diagnosticsScreenPrefab, _mainCamera.transform);
                diagnosticsScreen.transform.localPosition = new Vector3(0, 0, 2);
                diagnosticsScreen.transform.localScale = new Vector3(0.0025f, 0.0025f, 0.0025f);
            }
        }

        // Position UI canvas properly in front of user
        PositionUICanvas();

        // Don't disable skybox - allow it to be visible
        // RenderSettings.skybox = null;
    }

    private void SetupDesktopMode()
    {
        Debug.Log("Setting up Desktop Mode with webcam");

        // Disable passthrough layer
        if (passthroughLayer != null)
        {
            passthroughLayer.SetActive(false);
        }

        // Enable desktop camera
        if (desktopCaptureCamera != null)
        {
            desktopCaptureCamera.gameObject.SetActive(true);
        }
        else
        {
            Debug.LogWarning("DesktopCaptureCamera not assigned in RuntimeSourceSwitcher");
        }

        // Position UI canvas properly in front of user
        PositionUICanvas();
    }

    private void PositionUICanvas()
    {
        if (mainUICanvas != null)
        {
            // Ensure canvas is in world space
            mainUICanvas.renderMode = RenderMode.WorldSpace;

            // If we have a main camera, position relative to it
            if (_mainCamera != null)
            {
                // Parent to camera for proper positioning
                mainUICanvas.transform.SetParent(_mainCamera.transform, false);

                // Position in front of user
                mainUICanvas.transform.localPosition = new Vector3(0, -0.1f, 1.6f);

                // Ensure it's facing the user
                mainUICanvas.transform.localRotation = Quaternion.identity;
            }
        }
    }
}