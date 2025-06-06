using UnityEngine;
using UnityEngine.UI;

public class HandMenuConnector : MonoBehaviour
{
    [Header("References")]
    public Toggle passthroughToggle;
    public PassthroughToggleController passthroughController;

    void Start()
    {
        // Find components if not assigned
        if (passthroughToggle == null)
        {
            passthroughToggle = FindAnyObjectByType<Toggle>();
        }

        if (passthroughController == null)
        {
            passthroughController = FindAnyObjectByType<PassthroughToggleController>();
        }

        // Connect the toggle to the controller
        if (passthroughToggle != null && passthroughController != null)
        {
            passthroughToggle.onValueChanged.AddListener(passthroughController.SetPassthroughState);
            Debug.Log("[HandMenuConnector] Connected passthrough toggle to controller");
        }
        else
        {
            Debug.LogError("[HandMenuConnector] Could not find toggle or controller components");
        }
    }
} 