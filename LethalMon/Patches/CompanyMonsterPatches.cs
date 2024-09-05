using HarmonyLib;
using LethalMon.Behaviours;
using LethalMon.CatchableEnemy;
using LethalMon.Items;
using System.Linq;
using Unity.Netcode;
using UnityEngine;

namespace LethalMon.Patches
{
    internal class CompanyMonsterPatches
    {
        [HarmonyPatch(typeof(DepositItemsDesk), nameof(DepositItemsDesk.Start))]
        [HarmonyPostfix]
        private static void DepositItemsDeskStartPostfix(DepositItemsDesk __instance)
        {
            CompanyMonsterAI.ExtractTentaclesFromItemDesk(__instance);

            var companyMonsterCaught = Object.FindObjectsOfType<PokeballItem>().Where(ball => ball.enemyCaptured && ball.catchableEnemy?.GetType() == typeof(CatchableCompanyMonster)).Any();
            LethalMon.Log("Caught company monster: " + companyMonsterCaught);
            if (companyMonsterCaught)
            {
                __instance.gameObject.SetActive(false);
                return;
            }
        }

        [HarmonyPatch(typeof(CompanyMonsterCollisionDetect), nameof(CompanyMonsterCollisionDetect.OnTriggerEnter))]
        [HarmonyPostfix]
        private static void OnTriggerEnterPostfix(CompanyMonsterCollisionDetect __instance, Collider other)
        {
            if (!other.gameObject.TryGetComponent(out PokeballItem ball)) return;

            LethalMon.Log("Collided with pokeball.");
            var companyMonsterAI = Object.Instantiate(CompanyMonsterAI.companyMonsterPrefab)?.GetComponent<CompanyMonsterAI>();
            if(companyMonsterAI == null)
            {
                LethalMon.Log("Unable to create CompanyMonsterAI.");
                return;
            }

            companyMonsterAI.GetComponent<NetworkObject>()?.Spawn();

            ball.CollidedWithEnemy(companyMonsterAI);

            StopDeskAnimation(__instance.monsterAnimationID);
        }

        static void StopDeskAnimation(int monsterAnimationID)
        {
            var desk = Object.FindObjectOfType<DepositItemsDesk>();
            if (desk == null || !desk.attacking) return;

            desk.StopAllCoroutines();
            desk.attacking = false;
            desk.deskAudio.Stop();
            desk.wallAudio.Stop();

            var players = Utils.AllPlayers;
            if (players != null && desk.monsterAnimations.Length > monsterAnimationID)
            {
                foreach (var playerDying in players)
                {
                    if (!playerDying.isPlayerDead || playerDying.deadBody.attachedTo == null) continue;

                    if (desk.monsterAnimations[monsterAnimationID].monsterAnimatorGrabPoint == playerDying.deadBody.attachedTo)
                    {
                        playerDying.deadBody.attachedTo = null;
                        playerDying.deadBody.attachedLimb = null;
                        playerDying.deadBody.matchPositionExactly = false;
                    }
                }
            }

            desk.FinishKillAnimation();
            foreach (var animator in desk.monsterAnimations)
                animator.monsterAnimator.StopPlayback();
        }
    }
}
