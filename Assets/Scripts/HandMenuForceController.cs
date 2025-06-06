using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.UI.BodyUI;
using System.Reflection;

public class HandMenuForceController : MonoBehaviour
{
    [Header("Debug")]
    public bool enableDebugLogs = true;
    
    [Header("Manual Control")]
    public bool forceMenuVisible = false;
    
    private HandMenu handMenuComponent;
    private FieldInfo isShowingField;
    private FieldInfo followGameObjectField;
    private GameObject followGameObject;
    private bool wasForceVisible = false;
    
    void Start()
    {
        LogDebug("=== HandMenuForceController Starting ===");
        
        // Find the HandMenu component
        handMenuComponent = FindObjectOfType<HandMenu>();
        if (handMenuComponent == null)
        {
            LogDebug("ERROR: HandMenu component not found!");
            return;
        }
        
        LogDebug($"Found HandMenu component on: {handMenuComponent.name}");
        
        // Use reflection to access private fields
        SetupReflection();
        
        // Get the follow GameObject
        if (followGameObjectField != null && followGameObject == null)
        {
            followGameObject = followGameObjectField.GetValue(handMenuComponent) as GameObject;
            LogDebug($"Follow GameObject from HandMenu: {(followGameObject != null ? followGameObject.name : "NULL")}");
        }
        
        LogDebug("=== HandMenuForceController Ready ===");
        LogDebug("Press M, H, or Space to toggle menu");
        LogDebug("Set 'forceMenuVisible' in inspector to force menu on");
    }
    
    void SetupReflection()
    {
        var handMenuType = typeof(HandMenu);
        
        // Try to find the internal showing state field
        isShowingField = handMenuType.GetField("m_IsShowing", BindingFlags.NonPublic | BindingFlags.Instance);
        if (isShowingField == null)
        {
            isShowingField = handMenuType.GetField("isShowing", BindingFlags.NonPublic | BindingFlags.Instance);
        }
        LogDebug($"IsShowing field found: {isShowingField != null}");
        
        // Get the follow GameObject field
        followGameObjectField = handMenuType.GetField("m_FollowGameObject", BindingFlags.NonPublic | BindingFlags.Instance);
        if (followGameObjectField == null)
        {
            followGameObjectField = handMenuType.GetField("followGameObject", BindingFlags.NonPublic | BindingFlags.Instance);
        }
        LogDebug($"FollowGameObject field found: {followGameObjectField != null}");
    }
    
    void Update()
    {
        if (handMenuComponent == null) return;
        
        // Check for keyboard input
        bool toggleRequested = false;
        if (Input.GetKeyDown(KeyCode.M))
        {
            LogDebug("M key pressed");
            toggleRequested = true;
        }
        else if (Input.GetKeyDown(KeyCode.H))
        {
            LogDebug("H key pressed");
            toggleRequested = true;
        }
        else if (Input.GetKeyDown(KeyCode.Space))
        {
            LogDebug("Space key pressed");
            toggleRequested = true;
        }
        
        if (toggleRequested)
        {
            forceMenuVisible = !forceMenuVisible;
            LogDebug($"Force menu visible set to: {forceMenuVisible}");
        }
        
        // Check for palm gestures (if not manually forced)
        if (!toggleRequested)
        {
            bool palmDetected = CheckPalmGestures();
            if (palmDetected && !forceMenuVisible)
            {
                forceMenuVisible = true;
                LogDebug("Palm gesture detected - showing menu");
            }
            else if (!palmDetected && forceMenuVisible && !Input.GetKey(KeyCode.M) && !Input.GetKey(KeyCode.H))
            {
                forceMenuVisible = false;
                LogDebug("Palm gesture released - hiding menu");
            }
        }
        
        // Apply forced visibility
        if (forceMenuVisible != wasForceVisible)
        {
            ApplyForcedVisibility();
            wasForceVisible = forceMenuVisible;
        }
    }
    
    bool CheckPalmGestures()
    {
        Camera mainCamera = Camera.main;
        if (mainCamera == null) return false;
        
        Vector3 cameraForward = mainCamera.transform.forward;
        float threshold = 0.5f; // Adjust this for sensitivity
        
        // Check left hand
        GameObject leftHand = GameObject.Find("Left Hand Tracked Anchor");
        if (leftHand != null)
        {
            // For left hand: check if palm faces towards camera
            Vector3 leftPalmDirection = -leftHand.transform.right; // Palm normal is opposite to right
            float leftDot = Vector3.Dot(leftPalmDirection, cameraForward);
            
            if (leftDot >= threshold)
            {
                LogDebug($"Left palm towards camera: {leftDot:F2}");
                return true;
            }
        }
        
        // Check right hand  
        GameObject rightHand = GameObject.Find("Right Hand Tracked Anchor");
        if (rightHand != null)
        {
            // For right hand: check if palm faces towards camera
            Vector3 rightPalmDirection = rightHand.transform.right; // Palm normal is same as right
            float rightDot = Vector3.Dot(rightPalmDirection, cameraForward);
            
            if (rightDot >= threshold)
            {
                LogDebug($"Right palm towards camera: {rightDot:F2}");
                return true;
            }
        }
        
        return false;
    }
    
    void ApplyForcedVisibility()
    {
        if (handMenuComponent == null) return;
        
        LogDebug($"Applying forced visibility: {forceMenuVisible}");
        
        // Method 1: Try to call HandMenu's internal methods via reflection
        TryReflectionMethod();
        
        // Method 2: Direct GameObject manipulation
        TryDirectManipulation();
        
        // Method 3: Try to simulate the conditions that would trigger the menu
        TryConditionSimulation();
    }
    
    void TryReflectionMethod()
    {
        if (handMenuComponent == null) return;
        
        try
        {
            var handMenuType = typeof(HandMenu);
            
            if (forceMenuVisible)
            {
                // Try to find and call Show method
                var showMethod = handMenuType.GetMethod("Show", BindingFlags.NonPublic | BindingFlags.Instance);
                if (showMethod == null)
                {
                    showMethod = handMenuType.GetMethod("ShowMenu", BindingFlags.NonPublic | BindingFlags.Instance);
                }
                
                if (showMethod != null)
                {
                    LogDebug("Calling HandMenu Show method via reflection");
                    showMethod.Invoke(handMenuComponent, null);
                    return;
                }
                
                // Try to set internal showing state
                if (isShowingField != null)
                {
                    LogDebug("Setting isShowing field to true");
                    isShowingField.SetValue(handMenuComponent, true);
                }
            }
            else
            {
                // Try to find and call Hide method
                var hideMethod = handMenuType.GetMethod("Hide", BindingFlags.NonPublic | BindingFlags.Instance);
                if (hideMethod == null)
                {
                    hideMethod = handMenuType.GetMethod("HideMenu", BindingFlags.NonPublic | BindingFlags.Instance);
                }
                
                if (hideMethod != null)
                {
                    LogDebug("Calling HandMenu Hide method via reflection");
                    hideMethod.Invoke(handMenuComponent, null);
                    return;
                }
                
                // Try to set internal showing state
                if (isShowingField != null)
                {
                    LogDebug("Setting isShowing field to false");
                    isShowingField.SetValue(handMenuComponent, false);
                }
            }
        }
        catch (System.Exception e)
        {
            LogDebug($"Reflection method failed: {e.Message}");
        }
    }
    
    void TryDirectManipulation()
    {
        // Find Follow GameObject by name if not set
        if (followGameObject == null)
        {
            followGameObject = GameObject.Find("Follow GameObject");
            if (followGameObject != null)
            {
                LogDebug("Found Follow GameObject by name");
            }
        }
        
        if (followGameObject == null) 
        {
            LogDebug("ERROR: Follow GameObject not found!");
            return;
        }
        
        LogDebug($"Direct manipulation: Setting Follow GameObject active to {forceMenuVisible}");
        followGameObject.SetActive(forceMenuVisible);
        
        if (forceMenuVisible)
        {
            // Position the menu in front of the camera
            Camera mainCamera = Camera.main;
            if (mainCamera != null)
            {
                Vector3 targetPosition = mainCamera.transform.position + mainCamera.transform.forward * 0.5f;
                followGameObject.transform.position = targetPosition;
                
                // Make menu face the camera properly
                Vector3 lookDirection = (mainCamera.transform.position - targetPosition).normalized;
                followGameObject.transform.rotation = Quaternion.LookRotation(lookDirection);
                
                LogDebug($"Menu positioned at: {followGameObject.transform.position}");
            }
        }
    }
    
    void TryConditionSimulation()
    {
        // This method would try to simulate the conditions that make HandMenu think it should show
        // For now, we'll just ensure the component is enabled
        if (handMenuComponent != null)
        {
            handMenuComponent.enabled = true;
            LogDebug($"HandMenu component enabled: {handMenuComponent.enabled}");
        }
    }
    
    void LogDebug(string message)
    {
        if (enableDebugLogs)
        {
            Debug.Log($"[HandMenuForce] {message}");
        }
    }
    
    // Inspector methods for testing
    [ContextMenu("Force Show Menu")]
    void ForceShowMenu()
    {
        forceMenuVisible = true;
        ApplyForcedVisibility();
    }
    
    [ContextMenu("Force Hide Menu")]
    void ForceHideMenu()
    {
        forceMenuVisible = false;
        ApplyForcedVisibility();
    }
} 