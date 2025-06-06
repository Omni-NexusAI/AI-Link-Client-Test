using UnityEngine;
using UnityEngine.InputSystem;

public class ControllerMenuTrigger : MonoBehaviour
{
    // Reference to the menu GameObject
    public GameObject menuObject;
    
    // Controller input references
    public InputActionReference leftControllerPrimaryButton;
    public InputActionReference rightControllerPrimaryButton;
    
    private bool wasActive = false;
    
    void Start()
    {
        // Find the menu if not assigned
        if (menuObject == null)
        {
            // Try to find the Follow GameObject directly
            GameObject followObj = GameObject.Find("Follow GameObject");
            if (followObj != null)
            {
                menuObject = followObj;
            }
            else
            {
                // Try to find it via the Hand Menu Setup
                GameObject handMenuSetup = GameObject.Find("Hand Menu Setup MR Template Variant");
                if (handMenuSetup != null)
                {
                    Transform followTransform = handMenuSetup.transform.Find("Follow GameObject");
                    if (followTransform != null)
                    {
                        menuObject = followTransform.gameObject;
                    }
                }
            }
        }
        
        // Enable the actions if assigned
        if (leftControllerPrimaryButton != null && leftControllerPrimaryButton.action != null)
        {
            leftControllerPrimaryButton.action.Enable();
        }
        
        if (rightControllerPrimaryButton != null && rightControllerPrimaryButton.action != null)
        {
            rightControllerPrimaryButton.action.Enable();
        }
        
        // Ensure menu starts hidden
        if (menuObject != null)
        {
            menuObject.SetActive(false);
            wasActive = false;
        }
    }
    
    void Update()
    {
        if (menuObject == null) return;
        
        bool shouldBeActive = false;
        
        // Check keyboard input
        if (Input.GetKey(KeyCode.M) || Input.GetKey(KeyCode.H))
        {
            shouldBeActive = true;
        }
        
        // Check controller button input if defined
        if (leftControllerPrimaryButton != null && leftControllerPrimaryButton.action != null)
        {
            float value = leftControllerPrimaryButton.action.ReadValue<float>();
            if (value > 0.5f)
            {
                shouldBeActive = true;
            }
        }
        
        if (rightControllerPrimaryButton != null && rightControllerPrimaryButton.action != null)
        {
            float value = rightControllerPrimaryButton.action.ReadValue<float>();
            if (value > 0.5f)
            {
                shouldBeActive = true;
            }
        }
        
        // Toggle on change
        if (shouldBeActive != wasActive)
        {
            menuObject.SetActive(shouldBeActive);
            wasActive = shouldBeActive;
            
            if (shouldBeActive)
            {
                PositionMenuInView();
            }
        }
    }
    
    void PositionMenuInView()
    {
        if (menuObject == null) return;
        
        Camera mainCamera = Camera.main;
        if (mainCamera != null)
        {
            // Position menu in front of camera
            Vector3 position = mainCamera.transform.position + mainCamera.transform.forward * 0.5f;
            menuObject.transform.position = position;
            
            // Make menu face the camera
            menuObject.transform.LookAt(2f * menuObject.transform.position - mainCamera.transform.position);
            
            Debug.Log("ControllerMenuTrigger: Positioned menu at " + position);
        }
    }
    
    // Public method that can be called from UI buttons or events
    public void ToggleMenu()
    {
        if (menuObject != null)
        {
            bool newState = !menuObject.activeSelf;
            menuObject.SetActive(newState);
            wasActive = newState;
            
            if (newState)
            {
                PositionMenuInView();
            }
            
            Debug.Log("ControllerMenuTrigger: Menu manually toggled to " + newState);
        }
    }
}