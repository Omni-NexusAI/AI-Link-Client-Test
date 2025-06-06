using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.UI.BodyUI;

/// <summary>
/// Fixes and repositions the hand menu elements
/// </summary>
public class HandMenuFixer : MonoBehaviour
{
    [Header("Debug")]
    [Tooltip("Enable debug logging")]
    public bool enableDebug = true;
    
    void Start()
    {
        Debug.Log("HandMenuFixer: Script loaded and ready. Press M key to fix menu.");
    }
    
    void Update()
    {
        // Use legacy Input system for better compatibility
        if (Input.GetKeyDown(KeyCode.M))
        {
            Debug.Log("HandMenuFixer: M key pressed!");
            FixAndShowMenu();
        }
        
        // Also add alternative keys for testing
        if (Input.GetKeyDown(KeyCode.F))
        {
            Debug.Log("HandMenuFixer: F key pressed!");
            FixAndShowMenu();
        }

        // Additional manual trigger option for hand menu
        if (Input.GetKeyDown(KeyCode.H))
        {
            Debug.Log("HandMenuFixer: H key pressed for direct hand menu configuration!");
            ConfigureHandMenu();
        }
    }
    
    public void FixAndShowMenu()
    {
        Debug.Log("HandMenuFixer: Starting menu fix...");
        
        // Find the hand menu GameObject
        GameObject handMenu = GameObject.Find("Hand Menu Setup MR Template Variant");
        if (handMenu == null)
        {
            Debug.LogError("HandMenuFixer: Could not find Hand Menu Setup MR Template Variant");
            
            // Let's see what GameObjects we do have
            GameObject[] allObjects = FindObjectsOfType<GameObject>();
            Debug.Log($"HandMenuFixer: Found {allObjects.Length} total GameObjects in scene");
            
            foreach (GameObject obj in allObjects)
            {
                if (obj.name.Contains("Menu") || obj.name.Contains("Hand"))
                {
                    Debug.Log($"HandMenuFixer: Found potential menu object: {obj.name}");
                }
            }
            return;
        }
        
        Debug.Log($"HandMenuFixer: Found hand menu: {handMenu.name}");
        
        // Make sure it's active
        handMenu.SetActive(true);
        
        // Find all UI elements that might be part of the menu
        GameObject[] uiObjects = FindObjectsOfType<GameObject>();
        int fixedCount = 0;
        
        foreach (GameObject obj in uiObjects)
        {
            // Check if this object has UI components and might be part of the menu
            RectTransform rect = obj.GetComponent<RectTransform>();
            if (rect != null)
            {
                // Check if it's positioned far away (like at y=-100)
                if (rect.localPosition.y < -50 || rect.localPosition.y > 50)
                {
                    Debug.Log($"HandMenuFixer: Found UI element at unusual position: {obj.name} at {rect.localPosition}");
                    
                    // Reset its local position to something more reasonable
                    rect.localPosition = new Vector3(rect.localPosition.x, 0, rect.localPosition.z);
                    fixedCount++;
                    
                    Debug.Log($"HandMenuFixer: Fixed position of {obj.name} to {rect.localPosition}");
                }
                
                // Make sure it's active
                obj.SetActive(true);
            }
        }
        
        Debug.Log($"HandMenuFixer: Fixed {fixedCount} UI elements");
        
        // Try to position the entire menu in front of the camera
        Camera mainCamera = Camera.main;
        if (mainCamera != null && handMenu != null)
        {
            Vector3 newPosition = mainCamera.transform.position + mainCamera.transform.forward * 0.8f;
            handMenu.transform.position = newPosition;
            handMenu.transform.LookAt(mainCamera.transform);
            
            Debug.Log($"HandMenuFixer: Positioned hand menu at {newPosition}");
        }
        else
        {
            Debug.LogWarning("HandMenuFixer: Could not find main camera or hand menu for positioning");
        }
        
        // Also try to find and enable any Canvas components
        Canvas[] canvases = FindObjectsOfType<Canvas>(true);
        Debug.Log($"HandMenuFixer: Found {canvases.Length} canvases");
        
        foreach (Canvas canvas in canvases)
        {
            Debug.Log($"HandMenuFixer: Found canvas: {canvas.name}");
            if (canvas.name.Contains("Menu") || canvas.name.Contains("UI") || canvas.name.Contains("Background"))
            {
                canvas.gameObject.SetActive(true);
                canvas.enabled = true;
                Debug.Log($"HandMenuFixer: Enabled canvas: {canvas.name}");
            }
        }
        
        Debug.Log("HandMenuFixer: Menu fix completed!");
    }
    
    public void ConfigureHandMenu()
    {
        Debug.Log("HandMenuFixer: Configuring HandMenu component...");
        
        // Find the hand menu GameObject
        GameObject handMenuObj = GameObject.Find("Hand Menu Setup MR Template Variant");
        if (handMenuObj == null)
        {
            Debug.LogError("HandMenuFixer: Could not find Hand Menu Setup MR Template Variant");
            return;
        }
        
        // Get the HandMenu component
        var handMenuComponent = handMenuObj.GetComponent<HandMenu>();
        if (handMenuComponent == null)
        {
            Debug.LogError("HandMenuFixer: HandMenu component not found");
            return;
        }
        
        // Find the Follow GameObject
        GameObject followObj = handMenuObj.transform.Find("Follow GameObject")?.gameObject;
        if (followObj == null)
        {
            Debug.LogError("HandMenuFixer: Could not find Follow GameObject");
            return;
        }
        
        // Find hand tracked anchors
        GameObject leftHandObj = handMenuObj.transform.Find("Left Hand Tracked Anchor")?.gameObject;
        GameObject rightHandObj = handMenuObj.transform.Find("Right Hand Tracked Anchor")?.gameObject;
        
        if (leftHandObj == null || rightHandObj == null)
        {
            Debug.LogError("HandMenuFixer: Could not find hand tracking anchors");
            return;
        }
        
        Debug.Log("HandMenuFixer: Setting HandMenu properties...");
        
        // Configure HandMenu properties
        handMenuComponent.handMenuUIGameObject = followObj;
        handMenuComponent.menuHandedness = HandMenu.MenuHandedness.Either; // Value 3
        handMenuComponent.leftPalmAnchor = leftHandObj.transform;
        handMenuComponent.rightPalmAnchor = rightHandObj.transform;
        
        // Make sure the Follow GameObject is active
        followObj.SetActive(true);
        
        // Make sure hand anchors are active
        leftHandObj.SetActive(true);
        rightHandObj.SetActive(true);
        
        Debug.Log("HandMenuFixer: HandMenu configuration completed!");
        Debug.Log($"- handMenuUIGameObject: {followObj.name}");
        Debug.Log($"- menuHandedness: {handMenuComponent.menuHandedness}");
        Debug.Log($"- leftPalmAnchor: {leftHandObj.name}");
        Debug.Log($"- rightPalmAnchor: {rightHandObj.name}");
    }
}