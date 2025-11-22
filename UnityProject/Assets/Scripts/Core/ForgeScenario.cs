using System;
using UnityEngine;
using UnityEngine.Perception.Randomization.Scenarios;

namespace Forge.Core
{
    // Wraps Perception's FixedLengthScenario to integrate with Forge SessionManager
    public class ForgeScenario : FixedLengthScenario
    {
        public static ForgeScenario Instance { get; private set; }
        
        // Expose currentIteration for monitoring (it's protected in base class)
        public int CurrentIteration => currentIteration;

        protected override void Awake()
        {
            base.Awake();
            Instance = this;
        }

        public void Configure(int totalFrames, int seed)
        {
            // FIXED: Perception 1.0.0-preview.1 uses 'iterationCount' not 'totalIterations'
            this.constants.iterationCount = totalFrames;
            
            // Set random seed for reproducibility
            this.constants.randomSeed = (uint)seed;
            
            Debug.Log($"[ForgeScenario] Configured: Iterations={totalFrames}, Seed={seed}");
        }
    }
}
