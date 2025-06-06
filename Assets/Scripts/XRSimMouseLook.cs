using UnityEngine;

/// <summary>
/// FPS-style mouse look for XR simulation mode.
/// Pitch on the camera, yaw on the body or parent.
/// </summary>
public class XRSimMouseLook : MonoBehaviour
{
    [Tooltip("Mouse sensitivity multiplier")] public float mouseSensitivity = 100f;
    [Tooltip("Transform to apply yaw rotation to. Defaults to parent if null.")] public Transform playerBody;

    private float xRotation = 0f;

    void Start()
    {
        // Lock cursor for consistent input
        Cursor.lockState = CursorLockMode.Locked;
    }

    void Update()
    {
        // Get mouse delta
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity * Time.deltaTime;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity * Time.deltaTime;

        // Apply vertical rotation (pitch)
        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, -90f, 90f);
        transform.localRotation = Quaternion.Euler(xRotation, 0f, 0f);

        // Apply horizontal rotation (yaw)
        Transform yawTarget = playerBody != null ? playerBody : transform.parent;
        if (yawTarget != null)
        {
            yawTarget.Rotate(Vector3.up * mouseX, Space.Self);
        }
    }
}
