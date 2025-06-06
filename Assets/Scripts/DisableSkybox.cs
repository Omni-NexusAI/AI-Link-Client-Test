using UnityEngine;

public class DisableSkybox : MonoBehaviour
{
    void Start()
    {
        // Disable skybox
        RenderSettings.skybox = null;
        
        // Log for debugging
        Debug.Log("Skybox disabled by DisableSkybox script");
    }
}