본 문서는 **최종적으로 목표하는 레포지토리 구조**를 설명한다.

```
forge/
│
├── docs/                               # 모든 문서 (v2)
│   ├── README.md
│   ├── concept/
│   │   ├── 0_Concept_Document.md
│   │   └── 1_User_Requirements.md
│   └── design/
│       ├── 0_Repo Structure.md
│       ├── 1_System_Requirements.md
│       ├── 2_System_Architecture.md
│       ├── 3_Class_Design_Document.md
│       ├── 4_Data_Pipeline_Specification.md
│       ├── 5_API Specification.md
│       └── diagrams/              # planned
│           ├── architecture.png   # planned
│           ├── pipeline.png       # planned
│           ├── class-diagram.png  # planned
│           └── sequence/          # planned
│
├── unity/                              # Unity Simulation Layer 프로젝트
│   ├── Assets/
│   │   ├── Scenes/
│   │   │   ├── Factory.unity
│   │   │   ├── Office.unity
│   │   │   ├── Warehouse.unity
│   │   │   └── ...
│   │   ├── Scripts/
│   │   │   ├── Simulation/
│   │   │   │   ├── EnvironmentService.cs
│   │   │   │   ├── CameraService.cs
│   │   │   │   ├── CrowdService.cs
│   │   │   │   ├── BehaviorSystem.cs
│   │   │   │   ├── TimeWeatherService.cs
│   │   │   │   ├── VisibilityService.cs
│   │   │   └── Utils/
│   │   ├── Prefabs/
│   │   └── Materials/
│   ├── ProjectSettings/
│   └── unity_project.sln
│
├── src/                                # C# Backend (Orchestration + Pipeline)
│   ├── Application/
│   │   ├── GenerationCommand.cs
│   │   ├── ConfigurationLoader.cs
│   │   ├── ProgressReporter.cs
│   │   └── AppEntrypoint.cs
│   │
│   ├── Orchestration/
│   │   ├── SessionManager.cs
│   │   ├── ScenarioManager.cs
│   │   ├── EnvironmentCoordinator.cs
│   │   ├── GenerationController.cs
│   │   └── PipelineCoordinator.cs
│   │
│   ├── Simulation/                     # Unity와 인터페이스하는 Wrapper
│   │   ├── ISimulationAdapter.cs
│   │   └── UnitySimulationAdapter.cs
│   │
│   ├── DataPipeline/
│   │   ├── FrameBus.cs
│   │   ├── CaptureWorker.cs
│   │   ├── DetectionWorker.cs
│   │   ├── TrackingWorker.cs
│   │   ├── ReIDWorker.cs
│   │   ├── OcclusionWorker.cs
│   │   ├── LabelAssembler.cs
│   │   ├── EncodeWorker.cs
│   │   └── StorageWorker.cs
│   │
│   ├── Services/
│   │   ├── ValidationService.cs
│   │   ├── StatsService.cs
│   │   └── ManifestService.cs
│   │
│   ├── DataModel/
│   │   ├── FrameContext.cs
│   │   ├── RawImageData.cs
│   │   ├── DetectionData.cs
│   │   ├── TrackingData.cs
│   │   ├── ReIDData.cs
│   │   ├── OcclusionData.cs
│   │   ├── LabeledFrame.cs
│   │   └── EncodedFrame.cs
│   │
│   └── utils/
│       ├── Threading/
│       ├── Logging/
│       ├── FileUtils.cs
│       └── ConfigUtils.cs
│
├── pipeline/                           # Batch scripts / Worker configs / tuning
│   ├── configs/
│   │   ├── session_example_factory.json
│   │   ├── session_multiscene_daynight.json
│   │   └── worker_settings.json
│   ├── validation/
│   ├── export/
│   └── logs/
│
├── tools/                              # 도구/스크립트 (Phase 3 확장)
│   ├── export_tflite.py
│   ├── export_onnx.py
│   ├── image_inspector.py
│   ├── manifest_viewer.py
│   └── dataset_stats_visualizer.ipynb
│
├── tests/                              # 단위 테스트 + 통합 테스트
│   ├── test_pipeline/
│   ├── test_labels/
│   ├── test_storage/
│   └── test_config/
│
├── .gitignore
├── README.md
└── LICENSE

```
