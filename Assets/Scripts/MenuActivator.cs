using UnityEngine;

public class MenuActivator : MonoBehaviour
{
    public GameObject handMenu;
    public GameObject followGameObject;
    
    void Start()
    {
        if (handMenu == null)
            handMenu = GameObject.Find("Hand Menu Setup MR Template Variant");
            
        if (followGameObject == null && handMenu != null)
            followGameObject = handMenu.transform.Find("Follow GameObject")?.gameObject;
    }
    
    void Update()
    {
        // Simple keyboard activation for testing
        if (Input.GetKeyDown(KeyCode.M) || Input.GetKeyDown(KeyCode.H))
        {
            ToggleMenu();
        }
        
        // Check controller rotation (basic implementation)
        CheckControllerOrientation();
    }
    
    void CheckControllerOrientation()
    {
        // Find controller transforms
        Transform leftHand = GameObject.Find("Left Hand Tracked Anchor")?.transform;
        Transform rightHand = GameObject.Find("Right Hand Tracked Anchor")?.transform;
        
        bool shouldShow = false;
        
        // Check left hand orientation
        if (leftHand != null)
        {
            float upwardFacing = Vector3.Dot(leftHand.up, Vector3.up);
            if (upwardFacing > 0.7f)
            {
                shouldShow = true;
                Debug.Log("Left hand palm up: " + upwardFacing);
            }
        }
        
        // Check right hand orientation
        if (rightHand != null)
        {
            float upwardFacing = Vector3.Dot(rightHand.up, Vector3.up);
            if (upwardFacing > 0.7f)
            {
                shouldShow = true;
                Debug.Log("Right hand palm up: " + upwardFacing);
            }
        }
        
        // Show or hide menu based on orientation
        if (followGameObject != null)
        {
            bool isCurrentlyActive = followGameObject.activeSelf;
            if (shouldShow && !isCurrentlyActive)
            {
                followGameObject.SetActive(true);
            }
            else if (!shouldShow && isCurrentlyActive)
            {
                followGameObject.SetActive(false);
            }
        }
    }
    
    public void ToggleMenu()
    {
        if (followGameObject != null)
        {
            followGameObject.SetActive(!followGameObject.activeSelf);
            Debug.Log("Menu visibility: " + followGameObject.activeSelf);
        }
    }
}