using UnityEngine;
using UnityEngine.UI; // Required for Toggle

public class PassthroughToggleController : MonoBehaviour
{
    [Tooltip("Assign the OVRPassthroughLayer component here. It's usually on the OVRCameraRig or its CenterEyeAnchor.")]
    public OVRPassthroughLayer passthroughLayer;

    [Tooltip("Assign your UI Toggle from the hand menu here to sync its state on start.")]
    public Toggle uiToggle; // Explicitly UnityEngine.UI.Toggle

    void Awake()
    {
        // Attempt to find PassthroughLayer if not assigned
        if (passthroughLayer == null)
        {
            Debug.Log("[PassthroughToggleController] PassthroughLayer not assigned in Inspector. Attempting to find...");
            passthroughLayer = FindAnyObjectByType<OVRPassthroughLayer>();
        }

        if (passthroughLayer != null)
        {
            Debug.Log("[PassthroughToggleController] Found or was assigned PassthroughLayer: " + passthroughLayer.gameObject.name);
        }
        else
        {
            Debug.LogError("[PassthroughToggleController] CRITICAL: OVRPassthroughLayer could not be found or assigned! Script will not function.");
            enabled = false;
            return;
        }

        // Attempt to find UI Toggle if not assigned (less critical but good for init)
        if (uiToggle == null)
        {
            Debug.LogWarning("[PassthroughToggleController] UI Toggle not assigned in Inspector. Initial visual state might not sync perfectly if passthrough layer was enabled in editor.");
        }
    }

    void Start()
    {
        if (passthroughLayer == null) return; // Already handled in Awake, but as a safeguard

        Debug.Log("[PassthroughToggleController] Start() called. Forcing passthroughLayer.enabled to false initially.");
        passthroughLayer.enabled = false; // Force it off

        if (uiToggle != null)
        {
            Debug.Log("[PassthroughToggleController] Setting uiToggle.isOn to false.");
            uiToggle.isOn = false; // Force toggle to off
        }
        else
        {
            Debug.LogWarning("[PassthroughToggleController] uiToggle is not assigned. Cannot set its visual state.");
        }
        Debug.Log("[PassthroughToggleController] Initial state: passthroughLayer.enabled = " + passthroughLayer.enabled + (uiToggle != null ? ", uiToggle.isOn = " + uiToggle.isOn : ""));
    }

    public void SetPassthroughState(bool isOn)
    {
        if (passthroughLayer == null)
        {
            Debug.LogError("[PassthroughToggleController] SetPassthroughState called, but passthroughLayer is null!");
            return;
        }

        Debug.Log("[PassthroughToggleController] SetPassthroughState called with: " + isOn + ". Current passthroughLayer.enabled = " + passthroughLayer.enabled);
        passthroughLayer.enabled = isOn;
        Debug.Log("[PassthroughToggleController] After setting: passthroughLayer.enabled = " + passthroughLayer.enabled);

        // Optional: If you want the UI toggle to also update if changed from elsewhere (though it shouldn't typically happen)
        if (uiToggle != null && uiToggle.isOn != isOn)
        {
            // uiToggle.isOn = isOn; // Usually the event system handles this, but can be a safeguard
        }
    }
}
