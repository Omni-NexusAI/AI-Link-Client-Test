using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit.UI.BodyUI;

public class HandMenuDiagnosticController : MonoBehaviour
{
    [Header("Debug Settings")]
    public bool enableDebugLogs = true;
    
    [Header("Manual References (Optional)")]
    public GameObject followGameObject;
    public Transform leftHandAnchor;
    public Transform rightHandAnchor;
    public HandMenu handMenuComponent;
    
    [Header("Settings")]
    [Range(0f, 1f)]
    public float palmTowardsCameraThreshold = 0.5f;
    public float menuDistance = 0.5f;
    
    // Private references
    private GameObject menuContainer;
    private Camera mainCamera;
    private bool menuVisible = false;
    
    void Start()
    {
        LogDebug("=== HandMenuDiagnosticController Starting ===");
        
        // Find main camera
        mainCamera = Camera.main;
        if (mainCamera == null)
        {
            mainCamera = FindObjectOfType<Camera>();
        }
        LogDebug($"Main Camera: {(mainCamera != null ? mainCamera.name : "Not Found")}");
        
        // Auto-find objects if not manually assigned
        FindMenuObjects();
        
        LogDebug($"Final Setup - Follow GameObject: {(followGameObject != null ? followGameObject.name : "NULL")}");
        LogDebug($"Left Hand: {(leftHandAnchor != null ? leftHandAnchor.name : "NULL")}");
        LogDebug($"Right Hand: {(rightHandAnchor != null ? rightHandAnchor.name : "NULL")}");
        LogDebug($"HandMenu Component: {(handMenuComponent != null ? "Found" : "NULL")}");
        
        // Set menu container - try multiple approaches
        SetMenuContainer();
        
        // Disable the original HandMenu component to prevent interference
        if (handMenuComponent != null)
        {
            handMenuComponent.enabled = false;
            LogDebug("Disabled original HandMenu component to prevent interference");
        }
        
        LogDebug("=== Initialization Complete ===");
    }
    
    void FindMenuObjects()
    {
        LogDebug("--- Finding Menu Objects ---");
        
        // Find Hand Menu Setup
        if (handMenuComponent == null)
        {
            handMenuComponent = FindObjectOfType<HandMenu>();
            LogDebug($"HandMenu component found: {(handMenuComponent != null ? handMenuComponent.name : "Not Found")}");
        }
        
        // Find Follow GameObject
        if (followGameObject == null)
        {
            followGameObject = GameObject.Find("Follow GameObject");
            LogDebug($"Follow GameObject by name: {(followGameObject != null ? "Found" : "Not Found")}");
            
            if (followGameObject == null && handMenuComponent != null)
            {
                // Try to get it from the HandMenu component
                var handMenuType = typeof(HandMenu);
                var followField = handMenuType.GetField("m_FollowGameObject", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (followField != null)
                {
                    followGameObject = followField.GetValue(handMenuComponent) as GameObject;
                    LogDebug($"Follow GameObject from HandMenu component: {(followGameObject != null ? "Found" : "Not Found")}");
                }
            }
        }
        
        // Find hand anchors
        if (leftHandAnchor == null)
        {
            GameObject leftHandObj = GameObject.Find("Left Hand Tracked Anchor");
            if (leftHandObj != null)
            {
                leftHandAnchor = leftHandObj.transform;
                LogDebug($"Left Hand Anchor: Found ({leftHandObj.name})");
            }
            else
            {
                LogDebug("Left Hand Anchor: Not Found by name");
            }
        }
        
        if (rightHandAnchor == null)
        {
            GameObject rightHandObj = GameObject.Find("Right Hand Tracked Anchor");
            if (rightHandObj != null)
            {
                rightHandAnchor = rightHandObj.transform;
                LogDebug($"Right Hand Anchor: Found ({rightHandObj.name})");
            }
            else
            {
                LogDebug("Right Hand Anchor: Not Found by name");
            }
        }
        
        // List all children of Hand Menu Setup if we found it
        if (handMenuComponent != null)
        {
            LogDebug($"--- Hand Menu Setup Children ---");
            Transform handMenuTransform = handMenuComponent.transform;
            for (int i = 0; i < handMenuTransform.childCount; i++)
            {
                Transform child = handMenuTransform.GetChild(i);
                LogDebug($"Child {i}: {child.name} (Active: {child.gameObject.activeSelf})");
                
                // Look for anything that might be the menu UI
                if (child.name.Contains("Follow") || child.name.Contains("Menu") || child.name.Contains("UI"))
                {
                    if (followGameObject == null)
                    {
                        followGameObject = child.gameObject;
                        LogDebug($"Using {child.name} as followGameObject");
                    }
                }
            }
        }
    }
    
    void SetMenuContainer()
    {
        LogDebug("--- Setting Menu Container ---");
        
        if (followGameObject != null)
        {
            menuContainer = followGameObject;
            LogDebug($"Using Follow GameObject as menu container: {menuContainer.name}");
            
            // Log initial state
            LogDebug($"Initial menu active state: {menuContainer.activeSelf}");
            LogDebug($"Menu position: {menuContainer.transform.position}");
            
            // Check for child UI elements
            LogDebug($"Menu container children: {menuContainer.transform.childCount}");
            for (int i = 0; i < menuContainer.transform.childCount; i++)
            {
                Transform child = menuContainer.transform.GetChild(i);
                LogDebug($"  Child {i}: {child.name} (Active: {child.gameObject.activeSelf})");
            }
            
            // Initially hide the menu
            menuContainer.SetActive(false);
            menuVisible = false;
        }
        else
        {
            LogDebug("ERROR: No menu container found!");
        }
    }
    
    void Update()
    {
        // Check for keyboard input (multiple keys for testing)
        if (Input.GetKeyDown(KeyCode.M))
        {
            LogDebug("M key pressed - toggling menu");
            ToggleMenu();
            return;
        }
        
        if (Input.GetKeyDown(KeyCode.H))
        {
            LogDebug("H key pressed - toggling menu");
            ToggleMenu();
            return;
        }
        
        if (Input.GetKeyDown(KeyCode.Space))
        {
            LogDebug("Space key pressed - toggling menu");
            ToggleMenu();
            return;
        }
        
        // Check hand orientations and get the triggering hand
        Transform triggeringHand = GetTriggeringHand();
        bool shouldShow = triggeringHand != null;
        
        if (shouldShow && !menuVisible)
        {
            LogDebug("Hand orientation detected - showing menu");
            ShowMenu(triggeringHand);
        }
        else if (!shouldShow && menuVisible && !Input.GetKey(KeyCode.M) && !Input.GetKey(KeyCode.H))
        {
            // Only hide if not holding a key
            HideMenu();
        }
    }
    
    Transform GetTriggeringHand()
    {
        if (mainCamera == null) return null;
        
        Vector3 cameraForward = mainCamera.transform.forward;
        
        if (leftHandAnchor != null)
        {
            // For left hand: palm facing towards camera means hand.right should be pointing towards camera
            Vector3 leftPalmDirection = -leftHandAnchor.right; // Negative because palm normal is opposite to right vector
            float leftDotProduct = Vector3.Dot(leftPalmDirection, cameraForward);
            
            if (leftDotProduct >= palmTowardsCameraThreshold)
            {
                LogDebug($"Left hand palm towards user detected: {leftDotProduct:F2}");
                return leftHandAnchor;
            }
        }
        
        if (rightHandAnchor != null)
        {
            // For right hand: palm facing towards camera means hand.right should be pointing away from camera
            Vector3 rightPalmDirection = rightHandAnchor.right; // Right palm normal points in same direction as right vector
            float rightDotProduct = Vector3.Dot(rightPalmDirection, cameraForward);
            
            if (rightDotProduct >= palmTowardsCameraThreshold)
            {
                LogDebug($"Right hand palm towards user detected: {rightDotProduct:F2}");
                return rightHandAnchor;
            }
        }
        
        return null;
    }
    
    void ToggleMenu()
    {
        if (menuVisible)
        {
            HideMenu();
        }
        else
        {
            ShowMenu();
        }
    }
    
    void ShowMenu(Transform triggeringHand = null)
    {
        if (menuContainer != null)
        {
            menuVisible = true;
            menuContainer.SetActive(true);
            
            // Position menu relative to the triggering hand or camera
            if (triggeringHand != null && mainCamera != null)
            {
                // Position menu slightly forward and to the side of the triggering hand
                Vector3 handPosition = triggeringHand.position;
                Vector3 cameraDirection = (mainCamera.transform.position - handPosition).normalized;
                
                // Offset the menu slightly towards the camera and slightly upward
                Vector3 targetPosition = handPosition + cameraDirection * 0.3f + Vector3.up * 0.1f;
                menuContainer.transform.position = targetPosition;
                
                // Make menu face towards the camera/user
                Vector3 lookDirection = (mainCamera.transform.position - targetPosition).normalized;
                menuContainer.transform.rotation = Quaternion.LookRotation(lookDirection);
                
                LogDebug($"Menu positioned near {triggeringHand.name} at: {targetPosition}");
            }
            else if (mainCamera != null)
            {
                // Fallback: position in front of camera
                Vector3 targetPosition = mainCamera.transform.position + mainCamera.transform.forward * menuDistance;
                menuContainer.transform.position = targetPosition;
                
                // Make menu face the camera
                Vector3 lookDirection = (mainCamera.transform.position - targetPosition).normalized;
                menuContainer.transform.rotation = Quaternion.LookRotation(lookDirection);
                
                LogDebug($"Menu positioned in front of camera at: {targetPosition}");
            }
        }
        else
        {
            LogDebug("ERROR: Cannot show menu - menuContainer is null!");
        }
    }
    
    void HideMenu()
    {
        if (menuContainer != null)
        {
            menuVisible = false;
            menuContainer.SetActive(false);
            LogDebug("Menu hidden");
        }
    }
    
    void LogDebug(string message)
    {
        if (enableDebugLogs)
        {
            Debug.Log($"[HandMenuDiagnostic] {message}");
        }
    }
    
    // Button for manual testing in inspector
    [ContextMenu("Test Show Menu")]
    void TestShowMenu()
    {
        LogDebug("Manual test - showing menu");
        ShowMenu();
    }
    
    [ContextMenu("Test Hide Menu")]
    void TestHideMenu()
    {
        LogDebug("Manual test - hiding menu");
        HideMenu();
    }
    
    [ContextMenu("Refresh Object References")]
    void RefreshReferences()
    {
        LogDebug("Manually refreshing object references");
        FindMenuObjects();
        SetMenuContainer();
    }
} 