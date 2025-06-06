using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit.UI.BodyUI;

/// <summary>
/// Simple toggle for the entire hand menu when palm detection isn't working
/// </summary>
public class SimpleHandMenuToggle : MonoBehaviour
{
    [Header("Target")]
    [Tooltip("The GameObject with the HandMenu component")]
    public GameObject handMenuGameObject;
    
    [Header("Controls")]
    [Tooltip("Key to toggle the menu")]
    public Key toggleKey = Key.Tab;
    
    private bool isMenuEnabled = true;
    
    void Start()
    {
        if (handMenuGameObject == null)
        {
            // Try to find it automatically
            handMenuGameObject = GameObject.Find("Hand Menu Setup MR Template Variant");
        }
        
        if (handMenuGameObject != null)
        {
            isMenuEnabled = handMenuGameObject.activeSelf;
            Debug.Log($"SimpleHandMenuToggle: Found hand menu, initial state: {isMenuEnabled}");
        }
        else
        {
            Debug.LogError("SimpleHandMenuToggle: Could not find hand menu GameObject!");
        }
    }
    
    void Update()
    {
        if (Keyboard.current != null && Keyboard.current[toggleKey].wasPressedThisFrame)
        {
            ToggleMenu();
        }
    }
    
    public void ToggleMenu()
    {
        if (handMenuGameObject == null) return;
        
        isMenuEnabled = !isMenuEnabled;
        handMenuGameObject.SetActive(isMenuEnabled);
        
        Debug.Log($"SimpleHandMenuToggle: Hand menu toggled to {isMenuEnabled}");
    }
}