using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

/// <summary>
/// Provides alternate ways to trigger the hand menu when standard palm detection isn't working.
/// </summary>
public class HandMenuTrigger : MonoBehaviour
{
    [Header("Target Settings")]
    [Tooltip("The UI GameObject that gets shown/hidden when the menu is activated")]
    public GameObject menuUIObject;
    
    [Header("Keyboard Controls")]
    [Tooltip("Key that toggles the menu on/off")]
    public Key toggleKey = Key.Tab;
    
    [Header("Controller Controls")]
    [Tooltip("Whether to use the controller button to toggle")]
    public bool useControllerButton = true;
    
    private bool isMenuVisible = false;
    private bool wasControllerButtonPressed = false;
    
    // Input System
    private InputAction menuButtonAction;
    
    void Awake()
    {
        // Setup controller input
        SetupInputActions();
    }
    
    void OnEnable()
    {
        // Enable input actions
        if (menuButtonAction != null) menuButtonAction.Enable();
    }
    
    void OnDisable()
    {
        // Disable input actions
        if (menuButtonAction != null) menuButtonAction.Disable();
    }
    
    void Start()
    {
        // Store initial state of the menu
        if (menuUIObject != null)
        {
            isMenuVisible = menuUIObject.activeSelf;
        }
    }
    
    void Update()
    {
        // Check for keyboard input
        if (Keyboard.current != null && Keyboard.current[toggleKey].wasPressedThisFrame)
        {
            ToggleMenu();
        }
        
        // Check for controller input
        CheckControllerInput();
    }
    
    void SetupInputActions()
    {
        if (useControllerButton)
        {
            // Create input action for menu button on both controllers
            // This will work with "Menu" button on Quest controllers or any similarly mapped button
            menuButtonAction = new InputAction("MenuButton", InputActionType.Button);
            
            // Add bindings for various controller buttons that could be used to trigger the menu
            menuButtonAction.AddBinding("<XRController>{LeftHand}/menu");
            menuButtonAction.AddBinding("<XRController>{RightHand}/menu");
            menuButtonAction.AddBinding("<XRController>{LeftHand}/secondaryButton"); // Y button on left controller
            menuButtonAction.AddBinding("<XRController>{RightHand}/secondaryButton"); // B button on right controller
            
            menuButtonAction.Enable();
        }
    }
    
    void CheckControllerInput()
    {
        if (!useControllerButton || menuButtonAction == null) return;
        
        bool isButtonPressed = menuButtonAction.triggered;
        
        // Toggle menu if button was just pressed
        if (isButtonPressed && !wasControllerButtonPressed)
        {
            ToggleMenu();
        }
        
        // Update button state
        wasControllerButtonPressed = isButtonPressed;
    }
    
    /// <summary>
    /// Toggles the visibility of the menu
    /// </summary>
    public void ToggleMenu()
    {
        isMenuVisible = !isMenuVisible;
        
        if (menuUIObject != null)
        {
            // Show/hide the menu
            menuUIObject.SetActive(isMenuVisible);
            
            // If showing the menu, place it in front of the camera
            if (isMenuVisible)
            {
                PositionMenuForReadability();
            }
            
            Debug.Log($"HandMenuTrigger: Menu visibility set to {isMenuVisible}");
        }
        else
        {
            Debug.LogError("HandMenuTrigger: Can't toggle menu - menuUIObject is null");
        }
    }
    
    /// <summary>
    /// Positions the menu in front of the player for readability
    /// </summary>
    private void PositionMenuForReadability()
    {
        if (menuUIObject == null) return;
        
        // Get the main camera
        Camera mainCamera = Camera.main;
        if (mainCamera == null) return;
        
        // If we're in a Canvas-based UI, we don't need to reposition
        Canvas canvas = menuUIObject.GetComponentInParent<Canvas>();
        if (canvas != null && (canvas.renderMode == RenderMode.ScreenSpaceOverlay || 
                              canvas.renderMode == RenderMode.ScreenSpaceCamera))
        {
            return;
        }
        
        // Position the menu in front of the camera at a comfortable distance
        // We don't set the parent to camera to avoid issues with the HandMenu component's own positioning
        Transform menuTransform = menuUIObject.transform;
        menuTransform.position = mainCamera.transform.position + mainCamera.transform.forward * 0.5f;
        menuTransform.rotation = Quaternion.LookRotation(menuTransform.position - mainCamera.transform.position);
    }
}