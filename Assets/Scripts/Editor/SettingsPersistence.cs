using UnityEngine;
using UnityEditor;
using System;
using System.IO;
using System.Collections.Generic;

/// <summary>
/// Serializable class for storing settings
/// </summary>
[Serializable]
public class SettingItem
{
    public string key;
    public string value;
}

/// <summary>
/// Serializable class for storing all settings
/// </summary>
[Serializable]
public class EditorSettingsData
{
    public List<SettingItem> settings = new List<SettingItem>();
}

/// <summary>
/// Manages persistent editor settings across Unity restarts.
/// </summary>
[InitializeOnLoad]
public static class SettingsPersistence
{
    // Path to store settings file
    private static readonly string SettingsFilePath = Path.Combine(
        Application.dataPath, 
        "EditorSettings.json"
    );

    // Dictionary to store all settings
    private static Dictionary<string, string> editorSettings = new Dictionary<string, string>();

    // Static constructor called on Unity startup
    static SettingsPersistence()
    {
        Debug.Log("SettingsPersistence initialized");
        
        // Load settings when Unity starts
        LoadSettings();
        
        // Apply settings
        ApplySettings();
    }

    /// <summary>
    /// Loads settings from the JSON file.
    /// </summary>
    public static void LoadSettings()
    {
        try
        {
            if (File.Exists(SettingsFilePath))
            {
                string json = File.ReadAllText(SettingsFilePath);
                EditorSettingsData data = JsonUtility.FromJson<EditorSettingsData>(json);
                
                editorSettings = new Dictionary<string, string>();
                foreach (var item in data.settings)
                {
                    editorSettings[item.key] = item.value;
                }
                
                Debug.Log("Editor settings loaded successfully.");
            }
            else
            {
                Debug.Log("No editor settings file found. Using default settings.");
                editorSettings = new Dictionary<string, string>();
                
                // Initialize with current Unity settings
                CaptureCurrentUnitySettings();
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Error loading editor settings: {e.Message}");
            editorSettings = new Dictionary<string, string>();
            
            // Initialize with current Unity settings
            CaptureCurrentUnitySettings();
        }
    }

    /// <summary>
    /// Captures current Unity settings to use as defaults
    /// </summary>
    private static void CaptureCurrentUnitySettings()
    {
        // Project Settings
        editorSettings["CompanyName"] = PlayerSettings.companyName;
        editorSettings["ProductName"] = PlayerSettings.productName;
        
        // XR Settings
        editorSettings["VirtualRealitySupported"] = PlayerSettings.virtualRealitySupported.ToString();
    }

    /// <summary>
    /// Saves current settings to the JSON file.
    /// </summary>
    public static void SaveSettings()
    {
        try
        {
            EditorSettingsData data = new EditorSettingsData();
            data.settings = new List<SettingItem>();
            
            foreach (var kvp in editorSettings)
            {
                // Skip null values (used for removing settings)
                if (kvp.Value != null)
                {
                    data.settings.Add(new SettingItem { key = kvp.Key, value = kvp.Value });
                }
            }
            
            string json = JsonUtility.ToJson(data, true);
            File.WriteAllText(SettingsFilePath, json);
            Debug.Log("Editor settings saved successfully.");
        }
        catch (Exception e)
        {
            Debug.LogError($"Error saving editor settings: {e.Message}");
        }
    }

    /// <summary>
    /// Applies loaded settings to Unity.
    /// </summary>
    public static void ApplySettings()
    {
        foreach (var setting in editorSettings)
        {
            ApplySetting(setting.Key, setting.Value);
        }
    }

    /// <summary>
    /// Sets a setting value and saves it.
    /// </summary>
    public static void SetSetting(string key, string value)
    {
        if (value == null)
        {
            // Remove the setting if value is null
            editorSettings.Remove(key);
        }
        else
        {
            editorSettings[key] = value;
            ApplySetting(key, value);
        }
    }

    /// <summary>
    /// Gets a setting value.
    /// </summary>
    public static string GetSetting(string key, string defaultValue = "")
    {
        if (editorSettings.TryGetValue(key, out string value))
        {
            return value;
        }
        
        return defaultValue;
    }

    /// <summary>
    /// Gets all setting keys.
    /// </summary>
    public static List<string> GetAllSettingKeys()
    {
        return new List<string>(editorSettings.Keys);
    }

    /// <summary>
    /// Applies a specific setting to Unity.
    /// </summary>
    private static void ApplySetting(string key, string value)
    {
        try
        {
            // Handle different setting types
            switch (key)
            {
                // Project Settings
                case "CompanyName":
                    PlayerSettings.companyName = value;
                    break;

                case "ProductName":
                    PlayerSettings.productName = value;
                    break;
                
                // XR Settings
                case "VirtualRealitySupported":
                    if (bool.TryParse(value, out bool vrSupported))
                    {
                        PlayerSettings.virtualRealitySupported = vrSupported;
                    }
                    break;

                // AI Link Specific Settings
                case "AILink.Camera.PassthroughEnabled":
                    EditorPrefs.SetString(key, value); // Assuming this was the intended behavior
                    break;
                
                case "AILink.Server.URL":
                    EditorPrefs.SetString(key, value); // Assuming this was the intended behavior
                    break;

                // Add more settings as needed
                default:
                    // For settings that don't need special handling, store them in EditorPrefs
                    // EditorPrefs.SetString(key, value); // OLD BEHAVIOR
                    Debug.LogWarning($"SettingsPersistence: Attempted to apply unhandled setting '{key}' with value '{value}'. This setting was not applied. If this setting should be persisted, please add an explicit case for it in SettingsPersistence.ApplySetting().");
                    break;
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Error applying setting {key}: {e.Message}");
        }
    }
}
