using System;
using System.Collections.Generic;

namespace Forge.Core.Session
{
    public static class SessionConfigValidator
    {
        public static void Validate(SessionConfig config)
        {
            if (config == null) throw new Exception("config is null");
            if (string.IsNullOrEmpty(config.sessionId)) throw new Exception("sessionId is required");
            if (config.totalFrames <= 0) throw new Exception("totalFrames must be > 0");
            if (config.targetFps <= 0) throw new Exception("targetFps must be > 0");
            if (string.IsNullOrEmpty(config.sceneName)) throw new Exception("sceneName is required");
            if (string.IsNullOrEmpty(config.qualityMode)) config.qualityMode = "strict";
            if (string.IsNullOrEmpty(config.frameRatePolicy)) config.frameRatePolicy = "quality_first";
            if (config.cameras == null || config.cameras.Count == 0) throw new Exception("at least one camera is required");

            var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var cam in config.cameras)
            {
                if (string.IsNullOrEmpty(cam.id)) throw new Exception("camera id is required");
                if (!ids.Add(cam.id)) throw new Exception($"duplicate camera id: {cam.id}");
                if (cam.width <= 0 || cam.height <= 0) throw new Exception($"camera {cam.id} resolution must be > 0");
                if (cam.fov <= 0) throw new Exception($"camera {cam.id} fov must be > 0");
            }
        }
    }
}
