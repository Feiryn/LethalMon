using ModelReplacement;
using ModelReplacement.Monobehaviors.Enemies;
using UnityEngine;

namespace LethalMon.Compatibility
{
    internal class ModelReplacementAPICompatibility
    {
        public const string ModelReplacementApiReferenceChain = "meow.ModelReplacementAPI";

        private static bool? _modelReplacementApiEnabled;

        public static bool Enabled
        {
            get
            {
                if (_modelReplacementApiEnabled == null)
                {
                    _modelReplacementApiEnabled = BepInEx.Bootstrap.Chainloader.PluginInfos.ContainsKey(ModelReplacementApiReferenceChain);
                    LethalMon.Log("MRApi enabled -> " + _modelReplacementApiEnabled);
                }

                return _modelReplacementApiEnabled.Value;
            }
        }

        public static GameObject? FindCurrentReplacementModelIn(GameObject? gameObject, bool isEnemy = false)
        {
            if (gameObject == null) return null;

            if (isEnemy)
                return gameObject.GetComponent<MaskedReplacementBase>()?.replacementModel;
            else
                return gameObject.GetComponent<BodyReplacementBase>()?.replacementModel;
        }
    }
}
