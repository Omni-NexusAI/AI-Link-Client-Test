using UnityEngine;
using UnityEditor;
using System;

/// <summary>
/// Automatically saves Unity editor settings when they change.
/// </summary>
[InitializeOnLoad]
public static class SettingsAutoSaver
{
    // Last known values of settings
    private static string lastCompanyName;
    private static string lastProductName;
    
    // Static constructor called on Unity startup
    static SettingsAutoSaver()
    {
        // Initialize last known values
        lastCompanyName = PlayerSettings.companyName;
        lastProductName = PlayerSettings.productName;
        
        // Subscribe to update event
        EditorApplication.update += OnEditorUpdate;
    }
    
    private static void OnEditorUpdate()
    {
        // Check for changes in settings
        CheckForSettingChanges();
    }
    
    private static void CheckForSettingChanges()
    {
        bool settingsChanged = false;
        
        // Check company name
        if (lastCompanyName != PlayerSettings.companyName)
        {
            SettingsPersistence.SetSetting("CompanyName", PlayerSettings.companyName);
            lastCompanyName = PlayerSettings.companyName;
            settingsChanged = true;
        }
        
        // Check product name
        if (lastProductName != PlayerSettings.productName)
        {
            SettingsPersistence.SetSetting("ProductName", PlayerSettings.productName);
            lastProductName = PlayerSettings.productName;
            settingsChanged = true;
        }
        
        // Add more settings to monitor as needed
        
        // Save settings if any changes were detected
        if (settingsChanged)
        {
            SettingsPersistence.SaveSettings();
        }
    }
}
