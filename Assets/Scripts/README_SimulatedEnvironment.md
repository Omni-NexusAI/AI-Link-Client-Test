# Simulated Environment in HMD Mode

This set of scripts allows the XR simulation environment to be visible in both editor mode and HMD mode.

## Setup Instructions

1. Create an empty GameObject in your scene (e.g., "EnvironmentLoader")
2. Add the `SimulatedEnvironmentLoader` component to this GameObject
3. The script will automatically try to load the default XR simulation environment prefab
4. If you want to use a different environment prefab, you can assign it directly in the inspector

## Configuration Options

- **Simulated Environment Prefab**: The prefab to instantiate. If not set, will try to load the default XR simulation environment prefab.
- **Load In HMD Mode**: Whether to load the environment when using a headset.
- **Load In Editor Mode**: Whether to load the environment when in editor mode.
- **Position Offset**: Offset from the GameObject's position where the environment will be instantiated.
- **Environment Scale**: Scale of the instantiated environment.

## How It Works

1. The `SimulatedEnvironmentLoader` script checks if XR is running
2. Based on the configuration, it decides whether to load the environment
3. It then instantiates the environment prefab as a child of the GameObject
4. The `RuntimeSourceSwitcher` script has been modified to allow the environment to be visible in HMD mode by:
   - Using Skybox clear flags instead of solid color
   - Not disabling the skybox renderer

## Troubleshooting

- If the environment is not visible in HMD mode, check that:
  - The `loadInHMDMode` option is enabled
  - The camera's clear flags are set to Skybox
  - The environment prefab is correctly assigned
- If the environment appears too large or small, adjust the `environmentScale` parameter
- If the environment is not positioned correctly, adjust the `positionOffset` parameter
