using System;
using UnityEngine;
using UnityEngine.Perception.Randomization.Randomizers;
using UnityEngine.Perception.Randomization.Parameters;

namespace Forge.Core.Randomizers
{
    [Serializable]
    [AddRandomizerMenu("Forge/Camera Placement Randomizer")]
    public class CameraPlacementRandomizer : Randomizer
    {
        // We can use FloatParameter for random range, or fixed values from Config
        public Vector3Parameter position = new Vector3Parameter();
        public Vector3Parameter rotation = new Vector3Parameter();

        protected override void OnIterationStart()
        {
            // Find all cameras tagged with a specific tag or use Perception's tag system
            var tags = tagManager.Query<CameraPlacementRandomizerTag>();
            foreach (var tag in tags)
            {
                tag.transform.position = position.Sample();
                tag.transform.rotation = Quaternion.Euler(rotation.Sample());
            }
        }
    }

    [AddComponentMenu("Forge/RandomizerTags/Camera Placement Randomizer Tag")]
    public class CameraPlacementRandomizerTag : RandomizerTag
    {
        // Tag component to mark objects for randomization
    }
}
