using UnityEngine;

public class HUDFollower : MonoBehaviour
{
    public Transform cameraTransform;
    public float distance = 0.3f;
    public float smoothTime = 0.1f;
    
    [Tooltip("Position offset from camera forward direction")]
    public Vector3 offset = new Vector3(0.15f, -0.1f, 0); // Offset to position in corner
    
    [Tooltip("Which corner to pin the HUD to (0=top-right, 1=top-left, 2=bottom-right, 3=bottom-left)")]
    public int cornerPosition = 2; // Default to bottom-right
    
    [Tooltip("Maximum angle the HUD can diverge from center view before repositioning")]
    public float maxViewAngle = 35f; // Similar to Coaching UI
    
    private Vector3 velocity = Vector3.zero;
    private Quaternion rotationVelocity = Quaternion.identity;

    void Start()
    {
        if (cameraTransform == null)
        {
            // Try to find the camera if not assigned
            var centerEyeAnchor = GameObject.Find("CenterEyeAnchor");
            if (centerEyeAnchor != null)
            {
                cameraTransform = centerEyeAnchor.transform;
            }
            else
            {
                cameraTransform = Camera.main.transform;
            }
        }
    }

    void LateUpdate()
    {
        if (cameraTransform != null)
        {
            // Calculate corner offset based on selected corner
            Vector3 cornerOffset = GetCornerOffset();
            
            // Calculate the target position
            Vector3 targetPosition = cameraTransform.position + 
                                    cameraTransform.forward * distance +
                                    cameraTransform.right * cornerOffset.x +
                                    cameraTransform.up * cornerOffset.y;
            
            // Check if HUD is outside of comfortable view angle
            float viewAngle = Vector3.Angle(cameraTransform.forward, 
                                          (transform.position - cameraTransform.position).normalized);
            
            if (viewAngle > maxViewAngle)
            {
                // Reposition to be directly in front, then it will smoothly move to corner
                targetPosition = cameraTransform.position + cameraTransform.forward * distance;
            }
            
            // Smoothly move the HUD to the target position
            transform.position = Vector3.SmoothDamp(transform.position, targetPosition, ref velocity, smoothTime);
            
            // Make the HUD face the camera
            Quaternion targetRotation = Quaternion.LookRotation(transform.position - cameraTransform.position);
            transform.rotation = targetRotation;
        }
    }
    
    private Vector3 GetCornerOffset()
    {
        Vector3 result = offset;
        
        switch (cornerPosition)
        {
            case 0: // Top-right
                result = new Vector3(Mathf.Abs(offset.x), Mathf.Abs(offset.y), offset.z);
                break;
            case 1: // Top-left
                result = new Vector3(-Mathf.Abs(offset.x), Mathf.Abs(offset.y), offset.z);
                break;
            case 2: // Bottom-right
                result = new Vector3(Mathf.Abs(offset.x), -Mathf.Abs(offset.y), offset.z);
                break;
            case 3: // Bottom-left
                result = new Vector3(-Mathf.Abs(offset.x), -Mathf.Abs(offset.y), offset.z);
                break;
        }
        
        return result;
    }
}