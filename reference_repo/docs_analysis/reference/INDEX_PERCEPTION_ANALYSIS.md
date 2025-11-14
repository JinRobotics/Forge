# Unity Perception Package Analysis - Document Index

## Quick Navigation

### For Project Managers & Architects
Start here: **README_PERCEPTION_ANALYSIS.md**
- Overview of what Perception provides
- What we can reuse for CCTV
- What we need to build
- Architecture patterns to adopt

### For Developers Implementing Labelers
Read in order:
1. **PERCEPTION_ANALYSIS.md** - Understand the system
2. **PERCEPTION_KEY_REFERENCES.md** - Find CameraLabeler.cs location
3. Reference implementation: `/BoundingBox/BoundingBoxLabeler.cs`

### For Developers Implementing Randomizers
Read in order:
1. **PERCEPTION_ANALYSIS.md** - Understand randomization system
2. **PERCEPTION_KEY_REFERENCES.md** - Find Randomizer.cs location
3. Reference implementation: `/RandomizerLibrary/Transform/TransformRandomizer.cs`

### For System Architects Customizing Output
Read in order:
1. **PERCEPTION_ANALYSIS.md** - Output Architecture section
2. **PERCEPTION_KEY_REFERENCES.md** - Output/Serialization System section
3. Reference implementation: `/Consumers/Solo/SoloEndpoint.cs`

### For File Locating & Navigation
Use: **PERCEPTION_DIRECTORY_MAP.txt**
- Complete runtime directory tree
- All labeler types listed
- All randomizer types listed
- Key patterns explained

---

## Document Summary

### 1. README_PERCEPTION_ANALYSIS.md (7.0 KB)
**Purpose**: Executive summary and quick start guide
**Contents**:
- What each document covers
- 3-step quick start for CCTV dev
- Extension points (labelers, randomizers, sensors, output)
- Key architectural insights
- What Perception doesn't provide
- Next steps

**Read this if**: You want a 10-minute overview before diving in

---

### 2. PERCEPTION_ANALYSIS.md (4.1 KB)
**Purpose**: Core architecture breakdown
**Contents**:
- Package overview
- 5 key architecture components:
  1. Data Capture & Sensing
  2. Ground Truth Labelers (15 types)
  3. Randomization System
  4. Output Architecture
  5. Data Model
- CCTV relevance assessment
- Reusable vs custom breakdown
- Architecture patterns to adopt

**Read this if**: You need to understand the system architecture

---

### 3. PERCEPTION_DIRECTORY_MAP.txt (6.2 KB)
**Purpose**: Navigation and structure reference
**Contents**:
- Complete Runtime/ directory tree with annotations
- Editor/ directory structure
- Documentation/ structure
- Key patterns explained:
  - Labeler hierarchy
  - Randomization hierarchy
  - Sensor chain
  - Output pipeline
  - Async pattern
- Important file locations

**Read this if**: You need to find files or understand relationships

---

### 4. PERCEPTION_KEY_REFERENCES.md (7.1 KB)
**Purpose**: Detailed file reference and development patterns
**Contents**:
- 6 major subsystems with file-by-file breakdown:
  1. Core Capture System (PerceptionCamera, DatasetCapture, SimulationState)
  2. Labeler System (CameraLabeler base + examples)
  3. Randomization Framework (Scenario, Randomizer, Parameters, Samplers)
  4. Output/Serialization System (IConsumerEndpoint, SoloEndpoint, DataModel)
  5. Sensor Implementations (CameraSensor types)
  6. Label Management (IdLabelConfig, Labeling)
- Code pattern examples for custom labelers
- Code pattern examples for custom randomizers
- Data flow architecture diagram
- SOLO JSON output format specification
- Package statistics (100+ files, 15 labelers, 9+ randomizers, etc.)

**Read this if**: You're implementing custom components

---

## Reference Directory Structure

```
/home/jinhyuk2me/test_ws/lk_sdg/
├── INDEX_PERCEPTION_ANALYSIS.md          [This file - You are here]
├── README_PERCEPTION_ANALYSIS.md         [Start here - Executive summary]
├── PERCEPTION_ANALYSIS.md                [Architecture breakdown]
├── PERCEPTION_DIRECTORY_MAP.txt          [File navigation]
├── PERCEPTION_KEY_REFERENCES.md          [Detailed developer reference]
│
└── reference_repo/
    └── com.unity.perception/
        ├── README.md                     [Package README]
        ├── com.unity.perception/
        │   ├── Runtime/
        │   │   ├── GroundTruth/         [Data capture & annotation]
        │   │   ├── Randomization/       [Domain randomization]
        │   │   └── RandomizerLibrary/   [Pre-built randomizers]
        │   ├── Documentation~/          [Official guides]
        │   └── Editor/                  [Editor UI]
        └── PerceptionHDRP/              [HDRP integration]
```

---

## Reading Order Recommendations

### Quick Overview (15 minutes)
1. This file (INDEX_PERCEPTION_ANALYSIS.md)
2. README_PERCEPTION_ANALYSIS.md
3. PERCEPTION_ANALYSIS.md

### Deep Dive (1-2 hours)
1. PERCEPTION_ANALYSIS.md
2. PERCEPTION_KEY_REFERENCES.md
3. Review reference implementation files mentioned
4. Examine actual code in reference_repo/

### Implementation-Focused (2-4 hours)
1. README_PERCEPTION_ANALYSIS.md - Step 1-3
2. PERCEPTION_KEY_REFERENCES.md - Your target subsystem
3. PERCEPTION_DIRECTORY_MAP.txt - Find specific files
4. Reference implementations in reference_repo/
5. Actual code implementation

---

## Key Takeaways

### What Perception Is
- Toolkit for generating synthetic datasets with ground truth
- Provides camera capture, annotation, randomization framework
- Used by Unity for computer vision research projects
- Open source, Apache 2.0 license, now community-maintained

### Core Innovations in Perception
1. **Pluggable Labelers**: Add new annotation types without modifying core
2. **Parameter-based Randomization**: Scene variation via configuration, not code
3. **Async Futures**: Expensive computations don't block main loop
4. **Message-based Serialization**: Decouple data model from output format
5. **Tag-based Object Queries**: Dynamic object selection without scene hardcoding

### For Your CCTV Project
- **Reuse**: PerceptionCamera, Labeler/Randomizer framework, async futures, JSON output
- **Build**: CCTV-specific labelers, randomizers, sensor types, output format
- **Adapt**: Parameter system, tag manager, scenario lifecycle

### Architecture Pattern to Adopt
```
Scenario (lifecycle management)
  └─ Randomizers (variation per iteration)
     ├─ Parameters (configurable values)
     └─ Samplers (random value generators)
        └─ Query tagged GameObjects (dynamic object selection)

PerceptionCamera (frame capture)
  └─ Labelers (annotation generation)
     └─ Report AsyncFuture<Annotation> (non-blocking)
        └─ DatasetCapture (collect and serialize)
           └─ IConsumerEndpoint (write to disk/network/custom)
```

---

## File Statistics

| Document | Size | Lines | Purpose |
|----------|------|-------|---------|
| README_PERCEPTION_ANALYSIS.md | 7.0 KB | ~180 | Executive summary + quick start |
| PERCEPTION_ANALYSIS.md | 4.1 KB | 69 | Architecture breakdown |
| PERCEPTION_DIRECTORY_MAP.txt | 6.2 KB | 123 | File navigation |
| PERCEPTION_KEY_REFERENCES.md | 7.1 KB | 191 | Detailed developer reference |
| **TOTAL** | **24.4 KB** | **563** | Complete analysis |

Perception Package Reference:
- 100+ C# files in Runtime/
- 15 labeler implementations
- 9+ pre-built randomizers
- 3 output endpoint implementations
- 5+ camera sensor types

---

## Next Steps

1. Read README_PERCEPTION_ANALYSIS.md
2. Choose your development path:
   - Labeler development → Read PERCEPTION_KEY_REFERENCES.md section 2
   - Randomizer development → Read PERCEPTION_KEY_REFERENCES.md section 3
   - Sensor development → Read PERCEPTION_KEY_REFERENCES.md section 5
   - Output customization → Read PERCEPTION_KEY_REFERENCES.md section 4
3. Review corresponding reference implementations in reference_repo/
4. Begin implementation following the code patterns provided

---

## Contact Points & Questions

**If you need to find...** → **Use this document**
- A specific class | PERCEPTION_DIRECTORY_MAP.txt
- A class definition | PERCEPTION_KEY_REFERENCES.md (with paths)
- Development pattern | PERCEPTION_KEY_REFERENCES.md (code examples)
- Architecture explanation | PERCEPTION_ANALYSIS.md
- Quick overview | README_PERCEPTION_ANALYSIS.md
- Next steps guidance | README_PERCEPTION_ANALYSIS.md (Next Steps section)

---

Generated: 2025-11-14
Analyzed Package: Unity Perception (discontinued, community-maintained)
Package Location: /home/jinhyuk2me/test_ws/lk_sdg/reference_repo/com.unity.perception/
