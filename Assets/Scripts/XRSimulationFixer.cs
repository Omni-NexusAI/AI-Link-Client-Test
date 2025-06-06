using UnityEngine;
using System.Collections;

public class XRSimulationFixer : MonoBehaviour
{
    [Header("Passthrough Settings")]
    public Color passthroughColor = new Color(0.15f, 0.15f, 0.15f, 1.0f);
    public bool applyInEditMode = true;
    public bool applyEveryFrame = true;
    public float checkInterval = 0.5f;
    
    private Camera[] cameras;
    private GameObject[] trackables;
    private Coroutine fixCoroutine;
    
    void Awake()
    {
        // Initialize references
        FindReferences();
        
        // Apply fixes immediately
        FixPassthroughColor();
        FixTrackablesAlignment();
    }

    void Start()
    {
        // Apply fixes in play mode
        FixPassthroughColor();
        FixTrackablesAlignment();
        
        // Continue applying every frame if needed
        if (applyEveryFrame && fixCoroutine == null)
        {
            fixCoroutine = StartCoroutine(ApplyFixesCoroutine());
        }
    }
    
    void OnEnable()
    {
        // Apply fixes when the component is enabled
        FindReferences();
        FixPassthroughColor();
        FixTrackablesAlignment();
        
        // Start the coroutine if it's not running
        if (applyEveryFrame && fixCoroutine == null)
        {
            fixCoroutine = StartCoroutine(ApplyFixesCoroutine());
        }
    }
    
    void OnDisable()
    {
        // Stop the coroutine when disabled
        if (fixCoroutine != null)
        {
            StopCoroutine(fixCoroutine);
            fixCoroutine = null;
        }
    }
    
    private IEnumerator ApplyFixesCoroutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(checkInterval);
            FixPassthroughColor();
            FixTrackablesAlignment();
        }
    }
    
    private void FindReferences()
    {
        cameras = FindObjectsByType<Camera>(FindObjectsSortMode.None);
        
        // Look for objects with names containing "Trackables" or "Plane"
        var allObjects = FindObjectsByType<GameObject>(FindObjectsSortMode.None);
        System.Collections.Generic.List<GameObject> trackablesList = new System.Collections.Generic.List<GameObject>();
        
        foreach (var obj in allObjects)
        {
            if (obj.name.Contains("Trackables") || obj.name.Contains("Plane"))
            {
                trackablesList.Add(obj);
            }
        }
        
        trackables = trackablesList.ToArray();
    }

    public void FixPassthroughColor()
    {
        if (cameras == null || cameras.Length == 0)
        {
            cameras = FindObjectsByType<Camera>(FindObjectsSortMode.None);
        }
        
        foreach (Camera cam in cameras)
        {
            if (cam != null && (cam.gameObject.name.Contains("Camera") || cam.gameObject.name.Contains("Eye")))
            {
                if (cam.clearFlags != CameraClearFlags.SolidColor || cam.backgroundColor != passthroughColor)
                {
                    cam.clearFlags = CameraClearFlags.SolidColor;
                    cam.backgroundColor = passthroughColor;
                    Debug.Log("Updated camera: " + cam.gameObject.name + " with color: " + passthroughColor);
                }
            }
        }
        
        // Try to find and update OVRPassthroughLayer if present
        var passthroughLayers = FindObjectsByType<OVRPassthroughLayer>(FindObjectsSortMode.None);
        foreach (var layer in passthroughLayers)
        {
            if (layer != null)
            {
                bool changed = false;
                
                if (layer.textureOpacity != 1.0f)
                {
                    layer.textureOpacity = 1.0f;
                    changed = true;
                }
                
                if (layer.edgeColor != passthroughColor)
                {
                    layer.edgeColor = passthroughColor;
                    changed = true;
                }
                
                if (layer.colorMapEditorType != OVRPassthroughLayer.ColorMapEditorType.None)
                {
                    layer.colorMapEditorType = OVRPassthroughLayer.ColorMapEditorType.None;
                    changed = true;
                }
                
                if (changed)
                {
                    Debug.Log("Updated OVRPassthroughLayer on: " + layer.gameObject.name);
                }
            }
        }
    }

    public void FixTrackablesAlignment()
    {
        if (trackables == null || trackables.Length == 0)
        {
            FindReferences();
        }
        
        foreach (GameObject obj in trackables)
        {
            if (obj != null)
            {
                Vector3 pos = obj.transform.position;
                if (pos.y != 0f)
                {
                    obj.transform.position = new Vector3(pos.x, 0f, pos.z);
                    Debug.Log("Aligned: " + obj.name + " to y=0");
                }
            }
        }
    }
    
#if UNITY_EDITOR
    // This will run in edit mode
    void OnValidate()
    {
        if (applyInEditMode && !Application.isPlaying)
        {
            FindReferences();
            FixPassthroughColor();
            FixTrackablesAlignment();
        }
    }
#endif
}