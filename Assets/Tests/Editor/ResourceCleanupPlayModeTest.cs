using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEditor.SceneManagement; // Required for EnterPlayMode options if needed

public class ResourceCleanupPlayModeTest
{
    [UnityTest]
    public IEnumerator PlayModeCycles_ShouldNotCrash_And_ProperlyExitPlayMode()
    {
        // Optional: Disable domain reload for this test to match user's scenario
        // This requires Unity 2019.3+ and can be set in Project Settings > Editor > Enter Play Mode Settings
        // Or programmatically if an API exists (currently not straightforward for tests)
        // For now, we assume the user has Domain Reload disabled globally if that's the test condition.

        Debug.Log("Starting PlayModeCycles test...");

        for (int i = 0; i < 3; i++)
        {
            Debug.Log($"Test Cycle {i + 1}/3: Entering Play Mode...");
            // Enter Play Mode
            yield return new EnterPlayMode(); // Consider EnterPlayModeOptions if scene setup is needed

            // Optional: Add a small delay or wait for a specific condition if your scene takes time to load
            // yield return new WaitForSeconds(1); 

            Debug.Log($"Test Cycle {i + 1}/3: Exiting Play Mode...");
            // Exit Play Mode
            yield return new ExitPlayMode();

            // Optional: Add a small delay to ensure full teardown
            // yield return new WaitForSeconds(0.5f); 
        }

        Debug.Log("PlayModeCycles test completed all cycles.");

        // Assert that the application is not in play mode at the end
        Assert.IsFalse(Application.isPlaying, "Application should not be in Play Mode after test cycles.");
        
        // Assert that no exceptions were logged (UnityTest will usually fail on unhandled exceptions anyway)
        // This can be made more specific if you expect certain logs or want to fail on any error/exception log.
        // For now, relying on the test runner's default behavior for unhandled exceptions.
        // LogAssert.NoUnexpectedReceived(); // This could be used if you want to be very strict about logs.
    }
} 