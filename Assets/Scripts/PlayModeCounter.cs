using UnityEngine;
using System.Collections.Generic;
using System;

/// <summary>
/// Tracks play mode cycles to verify proper cleanup between sessions.
/// Provides detailed diagnostics about static state persistence between play sessions.
/// </summary>
public class PlayModeCounter : MonoBehaviour
{
    // Static counter that should be reset to 0 if domain reload is working properly
    private static int s_RunCount = 0;

    // Track static resources that should be cleaned up
    private static Dictionary<string, bool> s_ResourceTracker = new Dictionary<string, bool>();

    // Instance counter for this specific instance
    [SerializeField] private int _instanceRunCount = 0;

    // Inspector-visible diagnostics
    [SerializeField] private bool _domainReloadDetected = false;
    [SerializeField] private string _diagnosticMessage = "";

    /// <summary>
    /// Called when domain reloads (SubsystemRegistration is before Scene/Object initialization)
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void DomainCleanup()
    {
        // This should be called when domain reloads (if domain reload is enabled)
        Debug.Log($"PlayModeCounter: Domain reload cleanup called. Previous run count: {s_RunCount}");

        // Log the state of the resource tracker before clearing
        if (s_ResourceTracker.Count > 0)
        {
            Debug.Log("PlayModeCounter: Resource tracker state before cleanup:");
            foreach (var resource in s_ResourceTracker)
            {
                Debug.Log($"  - {resource.Key}: {(resource.Value ? "Active" : "Inactive")}");
            }
        }

        // Reset all static state
        s_RunCount = 0;
        s_ResourceTracker.Clear();
    }

    /// <summary>
    /// Register a static resource to be tracked
    /// </summary>
    public static void RegisterResource(string resourceName, bool isActive = true)
    {
        if (s_ResourceTracker.ContainsKey(resourceName))
        {
            s_ResourceTracker[resourceName] = isActive;
            Debug.Log($"PlayModeCounter: Updated resource '{resourceName}' state to {(isActive ? "Active" : "Inactive")}");
        }
        else
        {
            s_ResourceTracker.Add(resourceName, isActive);
            Debug.Log($"PlayModeCounter: Registered new resource '{resourceName}' as {(isActive ? "Active" : "Inactive")}");
        }
    }

    /// <summary>
    /// Unregister a static resource (mark as cleaned up)
    /// </summary>
    public static void UnregisterResource(string resourceName)
    {
        if (s_ResourceTracker.ContainsKey(resourceName))
        {
            s_ResourceTracker[resourceName] = false;
            Debug.Log($"PlayModeCounter: Marked resource '{resourceName}' as Inactive (cleaned up)");
        }
    }

    private void Awake()
    {
        // Increment the static counter
        s_RunCount++;

        // Set the instance counter
        _instanceRunCount = s_RunCount;

        // Check if domain reload is working
        _domainReloadDetected = s_RunCount == 1;

        // Log the current run count
        Debug.Log($"PlayModeCounter: Play mode session #{s_RunCount} started");

        // If this is the third run and s_RunCount is still 3, we might have an issue with domain reload
        if (s_RunCount >= 3)
        {
            string message = $"PlayModeCounter: Run count is {s_RunCount}, which suggests domain reload might not be clearing static state properly!";
            Debug.LogWarning(message);
            _diagnosticMessage = message;
        }
        else
        {
            _diagnosticMessage = $"Play mode session #{s_RunCount}";
        }

        // Register this instance
        RegisterResource($"PlayModeCounter_Instance_{_instanceRunCount}");
    }

    private void OnEnable()
    {
        // Log active resources on enable
        if (s_ResourceTracker.Count > 0)
        {
            Debug.Log($"PlayModeCounter: Active resources in session #{s_RunCount}:");
            foreach (var resource in s_ResourceTracker)
            {
                if (resource.Value) // Only log active resources
                {
                    Debug.Log($"  - {resource.Key}");
                }
            }
        }
    }

    private void OnDestroy()
    {
        Debug.Log($"PlayModeCounter: Instance #{_instanceRunCount} destroyed");

        // Unregister this instance
        UnregisterResource($"PlayModeCounter_Instance_{_instanceRunCount}");
    }
}