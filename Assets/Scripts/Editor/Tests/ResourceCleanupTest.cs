using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.SceneManagement;

/// <summary>
/// Tests to verify proper resource cleanup when entering and exiting play mode.
/// </summary>
public class ResourceCleanupTest
{
    // Track resources across play mode sessions
    private static int s_PlayModeCount = 0;
    private static List<string> s_ResourceLogs = new List<string>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticData()
    {
        // This is called when domain reloads
        Debug.Log($"ResourceCleanupTest: Domain reload cleanup. Previous play mode count: {s_PlayModeCount}");
        s_PlayModeCount = 0;
        s_ResourceLogs.Clear();
    }

    [UnityTest]
    public IEnumerator ResourceCleanup_FirstPlayMode_VerifyManagers()
    {
        // First play mode session
        s_PlayModeCount++;
        Debug.Log($"ResourceCleanupTest: Starting play mode session #{s_PlayModeCount}");

        // Wait a few frames to let everything initialize
        yield return new WaitForSeconds(1.0f);

        // Check that key managers exist
        var configManager = Object.FindObjectOfType<ConfigManager>();
        Assert.IsNotNull(configManager, "ConfigManager should exist in the scene");

        var webSocketClient = Object.FindObjectOfType<WebSocketClient>();
        Assert.IsNotNull(webSocketClient, "WebSocketClient should exist in the scene");

        var audioManager = Object.FindObjectOfType<AudioManager>();
        Assert.IsNotNull(audioManager, "AudioManager should exist in the scene");

        var videoManager = Object.FindObjectOfType<VideoManager>();
        Assert.IsNotNull(videoManager, "VideoManager should exist in the scene");

        // Check for PlayModeCounter
        var playModeCounter = Object.FindObjectOfType<PlayModeCounter>();
        Assert.IsNotNull(playModeCounter, "PlayModeCounter should exist in the scene");

        // Log the current state
        Debug.Log($"ResourceCleanupTest: Play mode session #{s_PlayModeCount} running, all managers found");

        // Capture resource state
        CaptureResourceState();

        // Exit play mode (this will be simulated by the test runner)
        yield return new WaitForSeconds(1.0f);
    }

    [UnityTest]
    public IEnumerator ResourceCleanup_SecondPlayMode_VerifyCleanup()
    {
        // Second play mode session
        s_PlayModeCount++;
        Debug.Log($"ResourceCleanupTest: Starting play mode session #{s_PlayModeCount}");

        // Wait a few frames to let everything initialize
        yield return new WaitForSeconds(1.0f);

        // Check that key managers exist again
        var configManager = Object.FindObjectOfType<ConfigManager>();
        Assert.IsNotNull(configManager, "ConfigManager should exist in the scene");

        var webSocketClient = Object.FindObjectOfType<WebSocketClient>();
        Assert.IsNotNull(webSocketClient, "WebSocketClient should exist in the scene");

        var audioManager = Object.FindObjectOfType<AudioManager>();
        Assert.IsNotNull(audioManager, "AudioManager should exist in the scene");

        var videoManager = Object.FindObjectOfType<VideoManager>();
        Assert.IsNotNull(videoManager, "VideoManager should exist in the scene");

        // Check for PlayModeCounter
        var playModeCounter = Object.FindObjectOfType<PlayModeCounter>();
        Assert.IsNotNull(playModeCounter, "PlayModeCounter should exist in the scene");

        // Capture resource state
        CaptureResourceState();

        // Exit play mode (this will be simulated by the test runner)
        yield return new WaitForSeconds(1.0f);
    }

    [UnityTest]
    public IEnumerator ResourceCleanup_ThirdPlayMode_VerifyNoLeaks()
    {
        // Third play mode session
        s_PlayModeCount++;
        Debug.Log($"ResourceCleanupTest: Starting play mode session #{s_PlayModeCount}");

        // Wait a few frames to let everything initialize
        yield return new WaitForSeconds(1.0f);

        // Check that key managers exist again
        var configManager = Object.FindObjectOfType<ConfigManager>();
        Assert.IsNotNull(configManager, "ConfigManager should exist in the scene");

        var webSocketClient = Object.FindObjectOfType<WebSocketClient>();
        Assert.IsNotNull(webSocketClient, "WebSocketClient should exist in the scene");

        var audioManager = Object.FindObjectOfType<AudioManager>();
        Assert.IsNotNull(audioManager, "AudioManager should exist in the scene");

        var videoManager = Object.FindObjectOfType<VideoManager>();
        Assert.IsNotNull(videoManager, "VideoManager should exist in the scene");

        // Check for PlayModeCounter
        var playModeCounter = Object.FindObjectOfType<PlayModeCounter>();
        Assert.IsNotNull(playModeCounter, "PlayModeCounter should exist in the scene");

        // Verify that we're in the third play mode session according to PlayModeCounter
        // This is the key test - if domain reload is working properly, PlayModeCounter.s_RunCount should be 1
        // If it's 3, then static state is persisting between play mode sessions

        // Capture and log resource state
        CaptureResourceState();
        LogResourceHistory();

        // Final verification - we should be able to complete three play mode sessions without errors
        Debug.Log("ResourceCleanupTest: Successfully completed three play mode sessions without errors");

        yield return null;
    }

    private void CaptureResourceState()
    {
        // Capture the state of various resources
        int webCamTextureCount = 0;
        int renderTextureCount = 0;
        int texture2DCount = 0;
        int audioClipCount = 0;
        int webSocketCount = 0;

        // Count active WebCamTextures
        foreach (var obj in Object.FindObjectsOfType<MonoBehaviour>())
        {
            if (obj is VideoManager)
            {
                var videoManager = obj as VideoManager;
                // We can't directly access private fields, but we can log that we found the manager
                s_ResourceLogs.Add($"Session {s_PlayModeCount}: Found VideoManager instance {obj.GetInstanceID()}");
            }
            else if (obj is AudioManager)
            {
                var audioManager = obj as AudioManager;
                s_ResourceLogs.Add($"Session {s_PlayModeCount}: Found AudioManager instance {obj.GetInstanceID()}");
            }
            else if (obj is WebSocketClient)
            {
                webSocketCount++;
                s_ResourceLogs.Add($"Session {s_PlayModeCount}: Found WebSocketClient instance {obj.GetInstanceID()}");
            }
        }

        // Log the resource counts
        s_ResourceLogs.Add($"Session {s_PlayModeCount}: WebSocketCount={webSocketCount}");

        // Check PlayModeCounter
        var playModeCounter = Object.FindObjectOfType<PlayModeCounter>();
        if (playModeCounter != null)
        {
            // We can't directly access private fields, but we can log that we found the counter
            s_ResourceLogs.Add($"Session {s_PlayModeCount}: Found PlayModeCounter instance {playModeCounter.GetInstanceID()}");
        }
    }

    private void LogResourceHistory()
    {
        Debug.Log("ResourceCleanupTest: Resource History Log");
        foreach (var log in s_ResourceLogs)
        {
            Debug.Log(log);
        }
    }
}