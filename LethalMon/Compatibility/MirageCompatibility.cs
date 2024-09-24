using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using LethalMon.Behaviours;
using Mirage.Unity;
using UnityEngine;

namespace LethalMon.Compatibility
{
    internal class MirageCompatibility() : ModCompatibility("Mirage")
    {
        public static MirageCompatibility Instance { get; } = new();
        
        internal static Dictionary<int, List<GameObject>> headMasks = new Dictionary<int, List<GameObject>>();

        public static void SaveHeadMasksOf(GameObject gameObject)
        {
            headMasks[gameObject.GetInstanceID()] = gameObject.GetComponentsInChildren<Transform>()
                .Where((t) => t.name.StartsWith("HeadMask"))
                .Select((t) => t.gameObject)
                .ToList();
        }

        public static void ShowMaskOf(GameObject gameObject, bool show = true)
        {
            if (!Instance.Enabled || !headMasks.ContainsKey(gameObject.GetInstanceID())) return;

            foreach (var mask in headMasks[gameObject.GetInstanceID()])
                mask.SetActive(show);
        }

        public static bool IsMaskEnabled(GameObject gameObject)
        {
            if (!Instance.Enabled) return true;

            if (!headMasks.ContainsKey(gameObject.GetInstanceID()))
            {
                LethalMon.Log("No HeadMasks saved..", LethalMon.LogType.Warning);
                return false;
            }
            var masks = headMasks[gameObject.GetInstanceID()];
            if (masks.Count == 0) return false;

            return masks.First().activeSelf;
        }

        [HarmonyPatch(typeof(MimicPlayer.MimicPlayer), nameof(MimicPlayer.MimicPlayer.MimicPlayer))]
        [HarmonyPrefix]
        private static bool MimicPlayerPreFix(MimicPlayer.MimicPlayer __instance/*, int playerId*/)
        {
            TamedEnemyBehaviour tamedEnemyBehaviour = __instance.GetComponentInParent<TamedEnemyBehaviour>();
            LethalMon.Log("TamedEnemyBehaviour: " + tamedEnemyBehaviour + ", Owner: " + tamedEnemyBehaviour?.ownerPlayer);
            
            if (tamedEnemyBehaviour == null || tamedEnemyBehaviour.ownerPlayer == null) return true;
            
            LethalMon.Log("Preventing tamed monster from mimicking player");
            return false;
        }
    }
}
