using UnityEngine;
using UnityEngine.XR;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Loads the simulated environment prefab in both editor and HMD modes.
/// Attach to an empty GameObject in the scene.
/// </summary>
public class SimulatedEnvironmentLoader : MonoBehaviour
{
    [Header("Environment Settings")]
    [Tooltip("The simulated environment prefab to instantiate. If not set, will try to load the default XR simulation environment prefab.")]
    public GameObject simulatedEnvironmentPrefab;

    [Tooltip("Whether to load the environment in HMD mode")]
    public bool loadInHMDMode = true;

    [Tooltip("Whether to load the environment in editor mode")]
    public bool loadInEditorMode = true;

    [Tooltip("Position offset for the environment")]
    public Vector3 positionOffset = Vector3.zero;

    [Tooltip("Scale of the environment")]
    public Vector3 environmentScale = Vector3.one;
    
    [Tooltip("Force load in Edit mode (not just Play mode)")]
    public bool forceLoadInEditMode = true;

    private GameObject _instantiatedEnvironment;
    private bool _xrRunning = false;
    private List<XRDisplaySubsystem> _displaySubsystems = new List<XRDisplaySubsystem>();

    void Awake()
    {
        // Check if XR is running
        _xrRunning = IsXRRunning();

        // Load the environment based on the current mode
        LoadEnvironment();
    }

    void Start()
    {
        // Double-check in Start in case XR system initializes after Awake
        if (_instantiatedEnvironment == null)
        {
            _xrRunning = IsXRRunning();
            LoadEnvironment();
        }
    }

    private void LoadEnvironment()
    {
        bool shouldLoad = false;

        // Determine if we should load the environment based on the current mode
        if (_xrRunning && loadInHMDMode)
        {
            Debug.Log("SimulatedEnvironmentLoader: Loading environment in HMD mode");
            shouldLoad = true;
        }
        else if (!_xrRunning && loadInEditorMode)
        {
            Debug.Log("SimulatedEnvironmentLoader: Loading environment in editor mode");
            shouldLoad = true;
        }

        // Try to get the environment prefab if not set
        if (simulatedEnvironmentPrefab == null)
        {
            // Direct reference to the default XR simulation environment prefab
            // This is the prefab referenced in XRSimulationPreferences.asset
            string prefabPath = "Packages/com.unity.xr.arfoundation/Assets/Prefabs/DefaultSimulationEnvironment.prefab";

            // Try to load using AssetDatabase in editor
#if UNITY_EDITOR
            simulatedEnvironmentPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (simulatedEnvironmentPrefab != null)
            {
                Debug.Log("SimulatedEnvironmentLoader: Loaded environment prefab from AssetDatabase");
            }
#endif

            // If still null, try to load from Resources
            if (simulatedEnvironmentPrefab == null)
            {
                // Try to load from Resources folder
                simulatedEnvironmentPrefab = Resources.Load<GameObject>("XRSimulationEnvironment");
                if (simulatedEnvironmentPrefab != null)
                {
                    Debug.Log("SimulatedEnvironmentLoader: Loaded environment prefab from Resources");
                }
                else
                {
                    Debug.LogWarning("SimulatedEnvironmentLoader: Could not find environment prefab. Please assign it manually in the inspector.");
                }
            }
        }

        // Load the environment if needed
        if (shouldLoad && simulatedEnvironmentPrefab != null && _instantiatedEnvironment == null)
        {
            _instantiatedEnvironment = Instantiate(simulatedEnvironmentPrefab, transform.position + positionOffset, Quaternion.identity);
            _instantiatedEnvironment.transform.SetParent(transform);
            _instantiatedEnvironment.transform.localScale = environmentScale;
            Debug.Log("SimulatedEnvironmentLoader: Environment loaded successfully");
        }
        else if (simulatedEnvironmentPrefab == null)
        {
            Debug.LogWarning("SimulatedEnvironmentLoader: No environment prefab assigned");
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
}