using UnityEngine;

/// <summary>
/// Helper script to reference the simulation environment prefab.
/// This is used by the SimulatedEnvironmentLoader to find the prefab.
/// </summary>
[CreateAssetMenu(fileName = "SimulationEnvironmentReference", menuName = "AI Link/Simulation Environment Reference")]
public class SimulationEnvironmentReference : ScriptableObject
{
    [Tooltip("Reference to the simulation environment prefab")]
    public GameObject environmentPrefab;
}
