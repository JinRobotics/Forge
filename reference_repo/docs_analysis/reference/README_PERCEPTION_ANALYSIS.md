# Unity Perception Package Analysis - Summary

## Files Generated

1. **PERCEPTION_ANALYSIS.md** (69 lines)
   - High-level package overview and architecture
   - 5 key components breakdown
   - CCTV relevance assessment (reusable vs custom)
   - Architecture patterns to adopt

2. **PERCEPTION_DIRECTORY_MAP.txt** (123 lines)
   - Complete Runtime directory structure
   - Key patterns and relationships
   - Critical file locations

3. **PERCEPTION_KEY_REFERENCES.md** (191 lines)
   - File-by-file reference with paths
   - Development pattern examples
   - Data flow architecture diagram
   - SOLO output format specification

## Quick Start for CCTV Development

### Step 1: Understand Core Classes
1. **PerceptionCamera** - Your main entry point
   - Attach to any camera in Unity
   - Add labelers to it
   - Configure capture triggers

2. **CameraLabeler** - Create custom labelers
   - Inherit from CameraLabeler (abstract base)
   - Implement: description, labelerId, supportsVisualization
   - Override: Setup(), OnUpdate(), Cleanup()

3. **Randomizer** - Create scene variation
   - Inherit from Randomizer
   - Use Parameters + Samplers for tunable randomization
   - Query tagged GameObjects

### Step 2: Key Integration Points

**For CCTV Capture:**
```
Scene Setup
  ├─ Add PerceptionCamera to camera GameObject
  ├─ Add custom CCTV labelers (PersonDetection, AnomalyDetection, etc.)
  └─ Add Scenario with Randomizers for variation

Runtime Flow
  └─ PerceptionCamera.OnUpdate() invokes labelers each frame
     └─ Labelers report AsyncFuture<Annotation>
        └─ DatasetCapture collects and builds Frame
           └─ SoloEndpoint writes JSON dataset
```

**For Domain Randomization:**
```
Custom Scenario (extend ScenarioBase)
  └─ Add Randomizers with Parameters
     └─ OnIterationStart() varies scene conditions
        ├─ Camera position/angle
        ├─ Lighting conditions
        ├─ Background/foreground objects
        └─ Material properties
```

### Step 3: Extension Points

**Add CCTV-Specific Labelers:**
- Inherit CameraLabeler
- Examples: PersonDetectionLabeler, CrowdDensityLabeler, AnomalyLabeler
- See `/BoundingBox/BoundingBoxLabeler.cs` as reference

**Add CCTV-Specific Randomizers:**
- Inherit Randomizer
- Examples: CCTVPositionRandomizer, LightingVariationRandomizer
- See `/RandomizerLibrary/Transform/TransformRandomizer.cs` as reference

**Add CCTV-Specific Sensors:**
- Inherit CameraSensor
- Examples: FisheyeCameraSensor, PTZCameraSensor
- See `/Sensors/SensorTypes/CircularFisheyeCameraSensor.cs` as reference

**Customize Output Format:**
- Inherit IConsumerEndpoint
- Or adapt SoloEndpoint JSON to CCTV schema
- See `/Consumers/Solo/SoloEndpoint.cs` as reference

## Key Architectural Insights

### 1. Labeler Pattern
Clean separation: each labeler handles one annotation type
- BoundingBoxLabeler handles bboxes
- SemanticSegmentationLabeler handles segmentation
- Your CCTVPersonDetectionLabeler handles person detection

### 2. Randomization Pattern
Scenario manages iteration; Randomizers execute per iteration
- More flexible than hardcoded variation
- Parameters make it configurable via JSON config files
- TagManager avoids scene hardcoding

### 3. Async Processing
Labelers return AsyncFuture<Annotation>, not blocking
- Expensive computations don't halt main loop
- Multiple labelers can process in parallel
- Futures collected at frame boundary

### 4. Pluggable Output
IConsumerEndpoint abstraction
- Can write to different formats (SOLO, COCO, custom)
- Can write to different locations (disk, network, custom)
- Decoupled from data model

### 5. Sensor Abstraction
CameraSensor base class with multiple implementations
- StandardCamera vs FisheyeCamera vs CubemapCamera
- Easy to add PTZCamera or DistortedCamera
- Channels (RGB, Depth, etc.) compose cleanly

## What Perception Doesn't Provide (You'll Build)

1. **CCTV-Specific Annotations**
   - Person tracks (across frames)
   - Anomaly detection labels
   - Crowd density metrics
   - Motion patterns

2. **CCTV Randomization**
   - Realistic occlusion patterns
   - Time-of-day lighting progression
   - Weather/seasonal variations
   - Realistic pedestrian spawn patterns

3. **CCTV Output Format**
   - Timestamp synchronization with real CCTV systems
   - Multi-camera frame alignment
   - Stream-based export (not just file-based)
   - Integration with analysis pipelines

4. **CCTV Sensors**
   - Wide-angle/fisheye camera models
   - PTZ (Pan-Tilt-Zoom) camera models
   - Camera calibration parameters
   - Realistic lens distortion

## File Structure for Your CCTV Extension

```
lk_sdg/
├── reference_repo/com.unity.perception/   [Reference implementation]
├── Assets/
│   ├── Scripts/CCTV/
│   │   ├── Labelers/
│   │   │   ├── PersonDetectionLabeler.cs
│   │   │   ├── AnomalyDetectionLabeler.cs
│   │   │   └── CrowdDensityLabeler.cs
│   │   ├── Randomizers/
│   │   │   ├── CCTVPositionRandomizer.cs
│   │   │   ├── LightingRandomizer.cs
│   │   │   └── PedestrianSpawnerRandomizer.cs
│   │   ├── Sensors/
│   │   │   ├── FisheyeCameraSensor.cs
│   │   │   └── PTZCameraSensor.cs
│   │   ├── DataModel/
│   │   │   └── CCTVFrame.cs (extends Frame if needed)
│   │   └── Scenarios/
│   │       └── CCTVScenario.cs
│   └── Scenes/
│       └── CCTVCapture.unity
```

## References for Understanding

**Labeler Reference:** 
- See `BoundingBox2DLabeler.cs` - simple pattern
- See `InstanceSegmentationLabeler.cs` - complex pattern

**Randomizer Reference:**
- See `TransformRandomizer.cs` - position/rotation variation
- See `LightRandomizer.cs` - lighting variation

**Output Reference:**
- See `SoloEndpoint.cs` - JSON serialization
- See `Frame.cs` - data model structure

**Full Documentation:**
- See `/Documentation~/PerceptionCamera.md`
- See `/Documentation~/Randomization/index.md`

## Important Notes

1. **Perception is Discontinued**: It still works but won't receive updates. This is fine - we use it as reference architecture.

2. **SOLO Format Version 0.0.1**: Current schema is mature. You may extend it for CCTV without breaking existing tools.

3. **Python Analysis**: Perception ecosystem includes `pysolotools` for dataset analysis. Your CCTV JSON should also be compatible.

4. **Performance**: Async futures and efficient rendering are critical. The package's rendering optimization utilities are valuable (see `/Utilities/RenderUtilities.cs`).

5. **Editor Integration**: Many Perception features use Editor-only code. For runtime-only scenarios, focus on `/Runtime/` directory.

## Next Steps

1. Study the three generated analysis files in order
2. Review `CameraLabeler.cs` and `Randomizer.cs` base classes
3. Examine one complete example (e.g., BoundingBoxLabeler)
4. Design your CCTV labelers/randomizers based on patterns
5. Adapt output format as needed for your domain

