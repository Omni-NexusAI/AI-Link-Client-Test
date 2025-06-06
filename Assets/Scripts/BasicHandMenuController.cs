using UnityEngine;

public class BasicHandMenuController : MonoBehaviour
{
    public GameObject menuUI;
    public Transform leftHand;
    public Transform rightHand;
    
    [Range(0f, 1f)]
    public float palmUpThreshold = 0.7f;
    
    void Start()
    {
        // Auto-find components if not set
        if (menuUI == null)
        {
            menuUI = GameObject.Find("Follow GameObject");
        }
        
        if (leftHand == null)
        {
            GameObject leftHandObj = GameObject.Find("Left Hand Tracked Anchor");
            if (leftHandObj) leftHand = leftHandObj.transform;
        }
        
        if (rightHand == null)
        {
            GameObject rightHandObj = GameObject.Find("Right Hand Tracked Anchor");
            if (rightHandObj) rightHand = rightHandObj.transform;
        }
        
        // Hide menu initially
        if (menuUI != null)
        {
            menuUI.SetActive(false);
        }
    }
    
    void Update()
    {
        bool shouldShowMenu = false;
        
        // Check for keyboard input
        if (Input.GetKey(KeyCode.M) || Input.GetKey(KeyCode.P))
        {
            shouldShowMenu = true;
        }
        
        // Check hand orientation
        if (leftHand != null)
        {
            float upwardAmount = Vector3.Dot(leftHand.up, Vector3.up);
            if (upwardAmount >= palmUpThreshold)
            {
                shouldShowMenu = true;
            }
        }
        
        if (rightHand != null)
        {
            float upwardAmount = Vector3.Dot(rightHand.up, Vector3.up);
            if (upwardAmount >= palmUpThreshold)
            {
                shouldShowMenu = true;
            }
        }
        
        // Update menu visibility
        if (menuUI != null)
        {
            menuUI.SetActive(shouldShowMenu);
            
            // Position menu in front of camera when visible
            if (shouldShowMenu)
            {
                Camera mainCamera = Camera.main;
                if (mainCamera != null)
                {
                    // Position slightly in front of the camera
                    menuUI.transform.position = mainCamera.transform.position + mainCamera.transform.forward * 0.5f;
                    
                    // Face the menu toward the camera
                    menuUI.transform.LookAt(2 * menuUI.transform.position - mainCamera.transform.position);
                }
            }
        }
    }
}