# Unity Perception Package - Key File References

## Critical Files for CCTV Implementation

### 1. CORE CAPTURE SYSTEM
**PerceptionCamera.cs** (27KB)
- Path: `/com.unity.perception/Runtime/GroundTruth/PerceptionCamera.cs`
- Role: Main component for camera-based data capture
- Key Methods: Setup, OnUpdate (labeler invocation), RequestCapture
- Properties: captureRgbImages, firstCaptureFrame, framesBetweenCaptures
- Critical Relationships: Holds CameraLabelers collection, registers with DatasetCapture

**DatasetCapture.cs** (32KB)
- Path: `/com.unity.perception/Runtime/GroundTruth/DatasetCapture.cs`
- Role: Global frame/sensor orchestration
- Key Static Methods: RegisterSensor(), RegisterMetric(), RegisterAnnotationDefinition()
- State: Maintains active SimulationState, frame scheduling

**SimulationState.cs** (61KB)
- Path: `/com.unity.perception/Runtime/GroundTruth/SimulationState.cs`
- Role: Simulation lifecycle and frame management
- Key Methods: AddSensor(), RegisterMetric(), GetSequenceAndStepFromFrame()

### 2. LABELER SYSTEM (Extensible Ground Truth)
**CameraLabeler.cs** (base class, ~500 lines)
- Path: `/com.unity.perception/Runtime/GroundTruth/Labelers/CameraLabeler.cs`
- Abstract Methods: description, labelerId, supportsVisualization
- Virtual Methods: Setup(PerceptionCamera), OnUpdate, Cleanup
- Key Property: sensorHandle (for reporting annotations/metrics)

**Example Labelers:**
- BoundingBox2DLabeler: `/BoundingBox/BoundingBoxLabeler.cs` - Pattern reference
- SemanticSegmentationLabeler: `/SemanticSegmentation/SemanticSegmentationLabeler.cs`
- InstanceSegmentationLabeler: `/InstanceSegmentation/InstanceSegmentationLabeler.cs`
- ObjectCountLabeler: `/ObjectCount/ObjectCountLabeler.cs` - Simple metric pattern

### 3. RANDOMIZATION FRAMEWORK
**ScenarioBase.cs** (base scenario class)
- Path: `/com.unity.perception/Runtime/Randomization/Scenarios/ScenarioBase.cs`
- Role: Container for randomizers and parameter configuration
- Key Properties: state, activeRandomizers, randomizers list
- Key Methods: OnStartRunning(), OnIterationStart(), OnIterationEnd()
- Hierarchy: ScenarioBase -> derive custom scenarios

**Randomizer.cs** (base randomizer class)
- Path: `/com.unity.perception/Runtime/Randomization/Randomizers/Randomizer.cs`
- Key Methods: OnCreate(), OnEnable(), OnDisable(), OnIterationStart(), OnIterationEnd()
- Key Property: tagManager (access RandomizerTagManager)
- Pattern: Query<T>() to find tagged GameObjects

**RandomizerTagManager.cs**
- Path: `/com.unity.perception/Runtime/Randomization/Randomizers/RandomizerTagManager.cs`
- Role: Tags GameObjects and queries them dynamically
- Key Method: Query<T>() where T : RandomizerTag

**Parameter.cs** (base parameter class)
- Path: `/com.unity.perception/Runtime/Randomization/Parameters/Parameter.cs`
- Abstract Methods: sampleType, samplers, GenericSample()
- Hierarchy: Parameter -> NumericParameter / CategoricalParameterBase

**Samplers:**
- FloatRange: `/Samplers/FloatRange.cs` - Range specification
- SamplerState: `/Samplers/SamplerState.cs` - RNG state
- UniformSampler: `/SamplerTypes/UniformSampler.cs`
- NormalSampler: `/SamplerTypes/NormalSampler.cs`

### 4. OUTPUT/SERIALIZATION SYSTEM
**IConsumerEndpoint.cs** (interface)
- Path: `/com.unity.perception/Runtime/GroundTruth/Consumers/IConsumerEndpoint.cs`
- Methods: StartRecording(), EndRecording(), ProcessFrame()

**SoloEndpoint.cs** (default implementation)
- Path: `/com.unity.perception/Runtime/GroundTruth/Consumers/Solo/SoloEndpoint.cs`
- Default output format for SOLO JSON datasets
- Properties: basePath, currentPath, soloDatasetName
- Key Methods: ProcessFrame(), WriteFrame()

**DataModel (Serialization-agnostic base classes)**
- Frame.cs: `/DataModel/Frame.cs` - Per-frame container (frame #, sequence, step, timestamp)
- Annotation.cs: `/DataModel/Annotation.cs` - Base annotation structure
- Metric.cs: `/DataModel/Metric.cs` - Base metric structure
- Sensor.cs / RgbSensor.cs: `/DataModel/Sensor.cs` - Sensor data capture record

### 5. SENSOR IMPLEMENTATIONS
**CameraSensor.cs** (base)
- Path: `/com.unity.perception/Runtime/GroundTruth/Sensors/CameraSensor.cs`
- Role: Abstract sensor interface

**UnityCameraSensor.cs**
- Path: `/com.unity.perception/Runtime/GroundTruth/Sensors/SensorTypes/UnityCameraSensor.cs`
- Standard camera implementation

**CircularFisheyeCameraSensor.cs**
- Path: `/com.unity.perception/Runtime/GroundTruth/Sensors/SensorTypes/CircularFisheyeCameraSensor.cs`
- Reference for fisheye camera type

**CubemapCameraSensor.cs**
- Path: `/com.unity.perception/Runtime/GroundTruth/Sensors/SensorTypes/CubemapCameraSensor.cs`
- Omnidirectional capture example

### 6. LABEL MANAGEMENT
**IdLabelConfig.cs**
- Path: `/com.unity.perception/Runtime/GroundTruth/LabelManagement/IdLabelConfig.cs`
- Maps GameObjects to semantic labels

**Labeling.cs** (component)
- Path: `/com.unity.perception/Runtime/GroundTruth/LabelManagement/Labeling.cs`
- Attach to GameObjects to assign labels

## Development Pattern: Creating a Custom CCTV Labeler

Inherit from CameraLabeler:
```csharp
public class PersonDetectionLabeler : CameraLabeler {
    public override string description => "Detects persons in view";
    public override string labelerId => "person-detection";
    protected override bool supportsVisualization => true;
    
    protected override void Setup() { /* Initialize */ }
    protected override void OnUpdate() { /* Capture frame */ }
    void ReportAnnotation() { sensorHandle.ReportAnnotation(...); }
}
```

## Development Pattern: Creating a Custom Randomizer

Inherit from Randomizer:
```csharp
public class CCTVPositionRandomizer : Randomizer {
    public NumericParameter cameraHeightParameter = new();
    public NumericParameter cameraAngleParameter = new();
    
    public override void OnIterationStart() {
        foreach (var tag in Query<CCTVCameraTag>()) {
            tag.transform.position = new Vector3(
                0, cameraHeightParameter.Sample(), 0
            );
        }
    }
}
```

## Data Flow Architecture

```
PerceptionCamera.OnUpdate()
  ├→ DatasetCapture.ShouldCaptureThisFrame()
  ├→ For each enabled labeler:
  │  ├→ labeler.OnUpdate()  
  │  └→ AsyncFuture<Annotation> returned
  └→ Collect all futures, build Frame object
    └→ Frame.ToMessage(IMessageBuilder)
      └→ SoloEndpoint.ProcessFrame() writes JSON
```

## SOLO Output Format (JSON)

Top-level structure:
```
{
  "version": "0.0.1",
  "sensorDefinitions": [...],
  "annotationDefinitions": [...],
  "metricDefinitions": [...],
  "frames": [
    {
      "frame": 0,
      "sequence": 0,
      "step": 0,
      "timestamp": 0.0,
      "sensors": [{...RGB image ref...}],
      "metrics": [...],
      "annotations": [...]
    },
    ...
  ]
}
```

## Files NOT in Reference (Editor-only)
- Editor/Randomization/UI/ - Visual randomization editor
- Editor/RandomizerLibrary/ - Pre-built randomizer browser
(These are UI layers above the Runtime)

## Total Package Statistics
- 100+ CS files in Runtime
- 15 labeler types with examples
- 9+ pre-built randomizers
- 3 output endpoints
- 5+ sensor types
- Supports Python analysis via pysolotools
