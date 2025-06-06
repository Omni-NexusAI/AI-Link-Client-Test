using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

/// <summary>
/// Editor window for managing persistent editor settings.
/// </summary>
public class SettingsWindow : EditorWindow
{
    private Vector2 scrollPosition;
    private string newSettingKey = "";
    private string newSettingValue = "";

    // Dictionary to store current settings values for the UI
    private Dictionary<string, string> currentValues = new Dictionary<string, string>();
    
    // List of all setting keys
    private List<string> allSettingKeys = new List<string>();

    [MenuItem("Window/AI Link/Settings")]
    public static void ShowWindow()
    {
        SettingsWindow window = GetWindow<SettingsWindow>("AI Link Settings");
        window.minSize = new Vector2(400, 300);
        window.Show();
    }

    private void OnEnable()
    {
        // Initialize with some default settings if they don't exist
        if (string.IsNullOrEmpty(SettingsPersistence.GetSetting("CompanyName")))
        {
            SettingsPersistence.SetSetting("CompanyName", PlayerSettings.companyName);
        }
        
        if (string.IsNullOrEmpty(SettingsPersistence.GetSetting("ProductName")))
        {
            SettingsPersistence.SetSetting("ProductName", PlayerSettings.productName);
        }
        
        // Initialize AI Link settings
        if (string.IsNullOrEmpty(SettingsPersistence.GetSetting("AILink.Camera.PassthroughEnabled")))
        {
            SettingsPersistence.SetSetting("AILink.Camera.PassthroughEnabled", "true");
        }
        
        if (string.IsNullOrEmpty(SettingsPersistence.GetSetting("AILink.Server.URL")))
        {
            SettingsPersistence.SetSetting("AILink.Server.URL", "ws://localhost:8080");
        }
        
        RefreshCurrentValues();
    }

    private void RefreshCurrentValues()
    {
        currentValues.Clear();
        allSettingKeys = SettingsPersistence.GetAllSettingKeys();
        
        foreach (var key in allSettingKeys)
        {
            currentValues[key] = SettingsPersistence.GetSetting(key);
        }
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("AI Link Settings", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("These settings will persist across Unity restarts.", MessageType.Info);
        EditorGUILayout.Space();

        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

        // Display all settings
        foreach (var key in allSettingKeys)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(key, GUILayout.Width(150));
            string newValue = EditorGUILayout.TextField(currentValues[key]);
            if (newValue != currentValues[key])
            {
                SettingsPersistence.SetSetting(key, newValue);
                currentValues[key] = newValue;
            }
            
            if (GUILayout.Button("X", GUILayout.Width(20)))
            {
                // Remove this setting
                SettingsPersistence.SetSetting(key, null);
                currentValues.Remove(key);
                RefreshCurrentValues();
                GUIUtility.ExitGUI(); // Prevent GUI errors
                return;
            }
            
            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.Space();
        EditorGUILayout.Separator();

        // Add new custom setting
        EditorGUILayout.LabelField("Add New Setting", EditorStyles.boldLabel);
        
        newSettingKey = EditorGUILayout.TextField("Key", newSettingKey);
        newSettingValue = EditorGUILayout.TextField("Value", newSettingValue);
        
        if (GUILayout.Button("Add Setting"))
        {
            if (!string.IsNullOrEmpty(newSettingKey))
            {
                SettingsPersistence.SetSetting(newSettingKey, newSettingValue);
                currentValues[newSettingKey] = newSettingValue;
                newSettingKey = "";
                newSettingValue = "";
                RefreshCurrentValues();
            }
        }

        EditorGUILayout.EndScrollView();

        EditorGUILayout.Space();
        
        // Buttons for saving and loading settings
        EditorGUILayout.BeginHorizontal();
        
        if (GUILayout.Button("Save Settings"))
        {
            SettingsPersistence.SaveSettings();
        }
        
        if (GUILayout.Button("Load Settings"))
        {
            SettingsPersistence.LoadSettings();
            SettingsPersistence.ApplySettings();
            RefreshCurrentValues();
        }
        
        EditorGUILayout.EndHorizontal();
    }
}
