using ModelReplacement;
using ModelReplacement.Monobehaviors.Enemies;
using UnityEngine;

namespace LethalMon.Compatibility
{
    internal class ModelReplacementAPICompatibility() : ModCompatibility("meow.ModelReplacementAPI")
    {
        public static ModelReplacementAPICompatibility Instance { get; } = new();

        public static GameObject? FindCurrentReplacementModelIn(GameObject? gameObject, bool isEnemy = false)
        {
            if (gameObject == null) return null;

            if (isEnemy)
            {
                var replacementBase = gameObject.GetComponent<MaskedReplacementBase>();
                return replacementBase != null && replacementBase.IsActive ? replacementBase.replacementModel : null;
            }
            else
            {
                var replacementBase = gameObject.GetComponent<BodyReplacementBase>();
                return replacementBase != null && replacementBase.IsActive ? replacementBase.replacementModel : null;
            }
        }
    }
}
