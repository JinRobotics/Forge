# Unity Perception Package - Architecture Analysis

## Package Overview
Unity Perception is a **discontinued** toolkit for generating synthetic datasets with ground truth annotations. It provides deterministic camera capture, labelers for semantic annotations, domain randomization, and structured JSON output (SOLO format).

## Key Architecture Components

### 1. Data Capture & Sensing
- **PerceptionCamera**: Core component that captures ground truth from any Camera. Manages:
  - Scheduled/manual capture modes
  - Frame synchronization via DatasetCapture
  - Sensor definition registration
  - Labeler lifecycle management
- **DatasetCapture**: Global manager orchestrating frame scheduling, sensor registration, metric/annotation collection
- **CameraSensor**: Pluggable sensor types (Unity standard, cubemap, fisheye)
- **Frame**: Top-level container for per-frame data (frame ID, sequence, metrics, sensor captures)

### 2. Ground Truth Labelers (15 types)
Extensible labeler pattern inheriting CameraLabeler:
- **Object Detection**: BoundingBox2D, BoundingBox3D
- **Semantic Understanding**: SemanticSegmentation, InstanceSegmentation
- **Geometry**: Depth, Normal, Keypoints, PixelPosition
- **Metadata**: MetadataReporter (custom tags), RenderedObjectInfo, ObjectCount, Occlusion

Each labeler produces annotation definitions + per-frame annotations, stored in async futures.

### 3. Randomization System
Scenario-based domain randomization with three-tier hierarchy:
- **Scenario**: Active manager holding randomizers, configuration, iteration state
- **Randomizer**: Base class with Parameters + samplers; uses TagManager to Query/randomize GameObjects
- **Parameters+Samplers**: Decoupled param definitions (Categorical, Numeric) from value samplers (Constant, Uniform, Normal, AnimationCurve)
- **RandomizerLibrary**: 9+ pre-built randomizers (Transform, Light, Material, Skybox, Volume effects, etc.)

### 4. Output Architecture
- **IConsumerEndpoint**: Abstract endpoint interface for output handling
- **SoloEndpoint** (default): Writes structured JSON datasets with metadata, frame records, definitions
- **PerceptionEndpoint** (alternative): Different serialization format
- **NoOutputEndpoint**: Discard mode
- JSON schema: SOLO format (0.0.1) with sensor defs, annotation defs, metrics defs, frame data

### 5. Data Model
Serialization-agnostic structure:
- **DataModel**: Base classes (DataModelElement, MetricDefinition, AnnotationDefinition, RgbSensorDefinition)
- **Message Building**: IMessageBuilder/IMessageProducer for converting data to output format
- **Metadata**: Simulation-wide metadata (software versions, perception version 0.0.1)

## CCTV Synthetic Data Relevance

### What We Can Reuse
1. **PerceptionCamera + Labeler pattern** - Perfect for camera-based CCTV data capture; easily extensible
2. **Randomization framework** - Domain randomization for camera angles, lighting, materials
3. **Metadata/annotation system** - Ground truth labeling infrastructure
4. **JSON dataset structure** - Can adapt SOLO format for CCTV-specific annotations
5. **Sensor abstraction** - Base for CCTV-specific camera types (fisheye, PTZ projection)

### What We Need to Build
1. **CCTV-specific labelers**: Person detection, crowd density, anomaly indicators
2. **CCTV metadata tags**: Camera ID, timestamp alignment, motion patterns
3. **Fisheye/PTZ sensor types**: Beyond standard camera sensors
4. **Real-world randomization**: Realistic scene variation for CCTV (occlusion, lighting variations)
5. **Output post-processing**: Convert SOLO → CCTV annotation format (if needed)
6. **Streaming/live capture support**: Real-time vs batch generation

## Architecture Patterns to Adopt
- **Pluggable labelers**: Extend CameraLabeler base; decouple annotation logic
- **Scenario + Randomizer hierarchy**: Parameterized simulations vs hardcoded variation
- **Async futures for annotations**: Don't block main loop during expensive computations
- **Message builders**: Decouple data model from output serialization
- **Tag-based object querying**: Randomizers tag & query objects dynamically (no hardcoded scene dependencies)
