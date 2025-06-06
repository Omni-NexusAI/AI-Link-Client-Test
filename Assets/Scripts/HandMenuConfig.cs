using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.UI.BodyUI;

/// <summary>
/// Updates the HandMenu component configuration to ensure correct settings
/// </summary>
public class HandMenuConfig : MonoBehaviour
{
    void Start()
    {
        Debug.Log("HandMenuConfig: Starting configuration...");
        
        // Find the Hand Menu GameObject
        GameObject handMenuObj = GameObject.Find("Hand Menu Setup MR Template Variant");
        if (handMenuObj == null)
        {
            Debug.LogError("HandMenuConfig: Could not find Hand Menu Setup MR Template Variant");
            return;
        }
        
        Debug.Log("HandMenuConfig: Found hand menu GameObject");
        
        // Get the HandMenu component
        HandMenu handMenu = handMenuObj.GetComponent<HandMenu>();
        if (handMenu == null)
        {
            Debug.LogError("HandMenuConfig: HandMenu component not found on Hand Menu Setup MR Template Variant");
            return;
        }
        
        Debug.Log("HandMenuConfig: Found HandMenu component");
        
        // Find the Follow GameObject (UI container)
        GameObject followObj = GameObject.Find("Follow GameObject");
        if (followObj == null)
        {
            // Try to find it as a child of the hand menu
            followObj = handMenuObj.transform.Find("Follow GameObject")?.gameObject;
            if (followObj == null)
            {
                Debug.LogError("HandMenuConfig: Could not find Follow GameObject");
                return;
            }
        }
        
        // Find the hand tracked anchors
        GameObject leftHandObj = GameObject.Find("Left Hand Tracked Anchor");
        if (leftHandObj == null)
        {
            // Try to find it as a child
            leftHandObj = handMenuObj.transform.Find("Left Hand Tracked Anchor")?.gameObject;
            if (leftHandObj == null)
            {
                Debug.LogError("HandMenuConfig: Could not find Left Hand Tracked Anchor");
                return;
            }
        }
        
        GameObject rightHandObj = GameObject.Find("Right Hand Tracked Anchor");
        if (rightHandObj == null)
        {
            // Try to find it as a child
            rightHandObj = handMenuObj.transform.Find("Right Hand Tracked Anchor")?.gameObject;
            if (rightHandObj == null)
            {
                Debug.LogError("HandMenuConfig: Could not find Right Hand Tracked Anchor");
                return;
            }
        }
        
        Debug.Log("HandMenuConfig: Found all required GameObjects");
        
        // Set the HandMenu properties
        handMenu.handMenuUIGameObject = followObj;
        handMenu.menuHandedness = HandMenu.MenuHandedness.Either; // Setting to 'Either' (value 3)
        handMenu.leftPalmAnchor = leftHandObj.transform;
        handMenu.rightPalmAnchor = rightHandObj.transform;
        
        Debug.Log("HandMenuConfig: Successfully updated HandMenu component with correct settings");
        Debug.Log($"- handMenuUIGameObject: {followObj.name}");
        Debug.Log($"- menuHandedness: {handMenu.menuHandedness}");
        Debug.Log($"- leftPalmAnchor: {leftHandObj.name}");
        Debug.Log($"- rightPalmAnchor: {rightHandObj.name}");
    }
}