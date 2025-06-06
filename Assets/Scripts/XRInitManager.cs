using UnityEngine;
using UnityEngine.XR;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Manages XR subsystem initialization and cleanup to prevent crashes on multiple runs.
/// </summary>
public class XRInitManager : MonoBehaviour
{
    private static XRInitManager _instance;
    public static XRInitManager Instance => _instance;

    [Header("Debug")]
    [SerializeField] private bool _logXREvents = true;

    private List<XRDisplaySubsystem> _displaySubsystems = new List<XRDisplaySubsystem>();
    private List<XRInputSubsystem> _inputSubsystems = new List<XRInputSubsystem>();

    /// <summary>
    /// Cleans up static references when the domain is reloaded.
    /// This is critical for preventing crashes on domain reload (e.g., when entering/exiting play mode).
    /// </summary>
    /// <remarks>
    /// RESOURCE MANAGEMENT: This method is automatically called by Unity when the domain is reloaded.
    /// It ensures that static references are cleared to prevent issues with stale references.
    ///
    /// The [RuntimeInitializeOnLoadMethod] attribute with SubsystemRegistration timing ensures
    /// this runs at the right time during domain reload.
    ///
    /// IMPORTANT: All singleton classes with static references should implement this pattern
    /// to prevent crashes on domain reload.
    /// </remarks>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void DomainCleanup()
    {
        // This method is called when the domain is reloaded (e.g., when entering/exiting play mode)
        Debug.Log("XRInitManager: Domain reload cleanup");

        // Clean up static instance to prevent issues on domain reload
        if (_instance != null)
        {
            // The actual GameObject will be destroyed by Unity, we just need to clear the static reference
            _instance = null;
        }
    }

    private void Awake()
    {
        // Singleton pattern
        if (_instance != null && _instance != this)
        {
            Debug.Log($"XRInitManager: Destroying duplicate instance (existing: {_instance.gameObject.name}, this: {gameObject.name})");
            Destroy(gameObject);
            return;
        }

        _instance = this;
        DontDestroyOnLoad(gameObject);

        if (_logXREvents)
        {
            Debug.Log("XRInitManager: Initialized");
        }

        // Register for application events
        Application.quitting += OnApplicationQuitting;
    }

    private void Start()
    {
        // Get references to all XR subsystems
        GetXRSubsystems();

        // Log initial state
        if (_logXREvents)
        {
            LogXRState();
        }
    }

    private void OnDestroy()
    {
        // Unregister from application events
        Application.quitting -= OnApplicationQuitting;

        // Clean up XR subsystems
        CleanupXRSubsystems();
    }

    private void OnApplicationQuit()
    {
        if (_logXREvents)
        {
            Debug.Log("XRInitManager: OnApplicationQuit called");
        }

        // Clean up XR subsystems
        CleanupXRSubsystems();
    }

    private void OnApplicationQuitting()
    {
        if (_logXREvents)
        {
            Debug.Log("XRInitManager: Application.quitting event received");
        }

        // Clean up XR subsystems
        CleanupXRSubsystems();
    }

    private void GetXRSubsystems()
    {
        // Get all XR display subsystems
        SubsystemManager.GetSubsystems(_displaySubsystems);

        // Get all XR input subsystems
        SubsystemManager.GetSubsystems(_inputSubsystems);

        if (_logXREvents)
        {
            Debug.Log($"XRInitManager: Found {_displaySubsystems.Count} display subsystems and {_inputSubsystems.Count} input subsystems");
        }
    }

    private void CleanupXRSubsystems()
    {
        if (_logXREvents)
        {
            Debug.Log("XRInitManager: Cleaning up XR subsystems");
        }

        // Stop all display subsystems
        foreach (var displaySubsystem in _displaySubsystems)
        {
            if (displaySubsystem != null && displaySubsystem.running)
            {
                try
                {
                    displaySubsystem.Stop();
                    if (_logXREvents)
                    {
                        Debug.Log($"XRInitManager: Stopped display subsystem {displaySubsystem}");
                    }
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"XRInitManager: Error stopping display subsystem: {e.Message}");
                }
            }
        }

        // Stop all input subsystems
        foreach (var inputSubsystem in _inputSubsystems)
        {
            if (inputSubsystem != null && inputSubsystem.running)
            {
                try
                {
                    inputSubsystem.Stop();
                    if (_logXREvents)
                    {
                        Debug.Log($"XRInitManager: Stopped input subsystem {inputSubsystem}");
                    }
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"XRInitManager: Error stopping input subsystem: {e.Message}");
                }
            }
        }

        // Clear the lists
        _displaySubsystems.Clear();
        _inputSubsystems.Clear();

        // Force a GC collection to clean up any lingering resources
        System.GC.Collect();
    }

    private void LogXRState()
    {
        Debug.Log("XRInitManager: Current XR State:");
        Debug.Log($"  - XR Device Active: {XRSettings.isDeviceActive}");
        Debug.Log($"  - XR Device Name: {XRSettings.loadedDeviceName}");
        Debug.Log($"  - XR Enabled: {XRSettings.enabled}");

        foreach (var displaySubsystem in _displaySubsystems)
        {
            if (displaySubsystem != null)
            {
                Debug.Log($"  - Display Subsystem: {displaySubsystem}, Running: {displaySubsystem.running}");
            }
        }

        foreach (var inputSubsystem in _inputSubsystems)
        {
            if (inputSubsystem != null)
            {
                Debug.Log($"  - Input Subsystem: {inputSubsystem}, Running: {inputSubsystem.running}");
            }
        }
    }

    /// <summary>
    /// Checks if XR is currently running
    /// </summary>
    public bool IsXRRunning()
    {
        // Refresh the subsystems list
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
}