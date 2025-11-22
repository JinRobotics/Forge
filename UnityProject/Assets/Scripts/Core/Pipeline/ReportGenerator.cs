using System.IO;
using Forge.Core.Session;
using UnityEngine;

namespace Forge.Core.Pipeline
{
    /// <summary>
    /// Phase 1 간이 manifest/validation/statistics 생성기.
    /// </summary>
    public static class ReportGenerator
    {
        public static void WriteReports(string outputDir, SessionConfig config, int framesGenerated, float avgCaptureMs, float avgBackpressure, string[] warnings)
        {
            if (string.IsNullOrEmpty(outputDir) || config == null) return;
            var manifestPath = Path.Combine(outputDir, "manifest.json");
            var validationPath = Path.Combine(outputDir, "validation.json");
            var statsPath = Path.Combine(outputDir, "statistics.json");

            var manifestJson = JsonUtility.ToJson(new ManifestStub
            {
                sessionId = config.sessionId,
                totalFrames = config.totalFrames,
                generatedFrames = framesGenerated,
                qualityMode = config.qualityMode,
                frameRatePolicy = config.frameRatePolicy,
                performanceSummary = new PerformanceSummary
                {
                    avgCaptureMs = avgCaptureMs,
                    avgBackpressure = avgBackpressure
                },
                warnings = warnings
            }, true);

            var validationJson = JsonUtility.ToJson(new ValidationStub
            {
                frameDrops = Mathf.Max(0, config.totalFrames - framesGenerated),
                poseMissing = 0,
                driftExceeded = false
            }, true);

            var statsJson = JsonUtility.ToJson(new StatisticsStub
            {
                avgCaptureMs = avgCaptureMs,
                backpressure = avgBackpressure
            }, true);

            File.WriteAllText(manifestPath, manifestJson);
            File.WriteAllText(validationPath, validationJson);
            File.WriteAllText(statsPath, statsJson);
        }

        [System.Serializable]
        private class ManifestStub
        {
            public string sessionId;
            public int totalFrames;
            public int generatedFrames;
            public string qualityMode;
            public string frameRatePolicy;
            public PerformanceSummary performanceSummary;
            public string[] warnings;
        }

        [System.Serializable]
        private class PerformanceSummary
        {
            public float avgCaptureMs;
            public float avgBackpressure;
        }

        [System.Serializable]
        private class ValidationStub
        {
            public int frameDrops;
            public int poseMissing;
            public bool driftExceeded;
        }

        [System.Serializable]
        private class StatisticsStub
        {
            public float avgCaptureMs;
            public float backpressure;
        }
    }
}
