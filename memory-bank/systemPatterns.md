# System Patterns
## Architecture
- XR Interaction Toolkit for core VR functionality
- Simulation subsystem for headset-free testing
- Component-based design for modular interaction systems

## Key Technical Decisions
- Using XR Device Simulator for input simulation
- TransformSync components for positional tracking
- MouseLook scripts for camera control in simulation mode

## Component Relationships
```mermaid
graph TD
    XRInitManager -->|configures| XRDeviceSimulator
    XRDeviceSimulator -->|provides input| XRInteractionManager
    XRInteractionManager -->|controls| XRCameraRig
    XRCameraRig -->|position sync| TransformSync
    XRSimMouseLook -->|controls| XRCameraRig
