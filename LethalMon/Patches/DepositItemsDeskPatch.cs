using HarmonyLib;
using LethalMon.Behaviours;
using LethalMon.CatchableEnemy;
using LethalMon.Items;
using System.Linq;
using UnityEngine;

namespace LethalMon.Patches
{
    internal class DepositItemsDeskPatch
    {
        [HarmonyPatch(typeof(DepositItemsDesk), nameof(DepositItemsDesk.Start))]
        [HarmonyPostfix]
        private static void DepositItemsDeskStartPostfix(DepositItemsDesk __instance)
        {
            CompanyMonsterAI.ExtractTentaclesFromItemDesk(__instance);

            if(__instance.isActiveAndEnabled) // todo: might be obsolete
            {
                var companyMonsterCaught = Object.FindObjectsOfType<PokeballItem>().Where(ball => ball.enemyCaptured && ball.catchableEnemy?.GetType() == typeof(CatchableCompanyMonster)).Any();
                LethalMon.Log("Caught company monster: " + companyMonsterCaught);
                if (companyMonsterCaught)
                {
                    __instance.gameObject.SetActive(false);
                    return;
                }

                CompanyMonsterAI.AttachToItemsDesk(__instance);
            }
        }

        /*[HarmonyPatch(typeof(EnemyAI), nameof(EnemyAI.Update))]
        [HarmonyPrefix]
        public static bool UpdatePrefix(EnemyAI __instance)
        {
            if (__instance.enemyType.isDaytimeEnemy && !__instance.daytimeEnemyLeaving)
            {
                __instance.CheckTimeOfDayToLeave();
            }
            if (__instance.stunnedIndefinitely <= 0)
            {
                if (__instance.stunNormalizedTimer >= 0f)
                {
                    __instance.stunNormalizedTimer -= Time.deltaTime / __instance.enemyType.stunTimeMultiplier;
                }
                else
                {
                    __instance.stunnedByPlayer = null;
                    if (__instance.postStunInvincibilityTimer >= 0f)
                    {
                        __instance.postStunInvincibilityTimer -= Time.deltaTime * 5f;
                    }
                }
            }
            if (!__instance.ventAnimationFinished && __instance.timeSinceSpawn < __instance.exitVentAnimationTime + 0.005f * (float)RoundManager.Instance.numberOfEnemiesInScene)
            {
                __instance.timeSinceSpawn += Time.deltaTime;
                if (!__instance.IsOwner)
                {
                    _ = __instance.serverPosition;
                    if (__instance.serverPosition != Vector3.zero)
                    {
                        __instance.transform.position = __instance.serverPosition;
                        __instance.transform.eulerAngles = new Vector3(__instance.transform.eulerAngles.x, __instance.targetYRotation, __instance.transform.eulerAngles.z);
                    }
                }
                else if (__instance.updateDestinationInterval >= 0f)
                {
                    __instance.updateDestinationInterval -= Time.deltaTime;
                }
                else
                {
                    __instance.SyncPositionToClients();
                    __instance.updateDestinationInterval = 0.1f;
                }
                return false;
            }
            if (!__instance.inSpecialAnimation && !__instance.ventAnimationFinished)
            {
                __instance.ventAnimationFinished = true;
                if (__instance.creatureAnimator != null)
                {
                    __instance.creatureAnimator.SetBool("inSpawningAnimation", value: false);
                }
            }
            if (!__instance.IsOwner)
            {
                if (__instance.currentSearch.inProgress)
                {
                    __instance.StopSearch(__instance.currentSearch);
                }
                __instance.SetClientCalculatingAI(enable: false);
                if (!__instance.inSpecialAnimation)
                {
                    if (RoundManager.Instance.currentDungeonType == 4 && Vector3.Distance(__instance.transform.position, RoundManager.Instance.currentMineshaftElevator.elevatorInsidePoint.position) < 1f)
                    {
                        __instance.serverPosition += RoundManager.Instance.currentMineshaftElevator.elevatorInsidePoint.position - RoundManager.Instance.currentMineshaftElevator.previousElevatorPosition;
                    }
                    __instance.transform.position = Vector3.SmoothDamp(__instance.transform.position, __instance.serverPosition, ref __instance.tempVelocity, __instance.syncMovementSpeed);
                    __instance.transform.eulerAngles = new Vector3(__instance.transform.eulerAngles.x, Mathf.LerpAngle(__instance.transform.eulerAngles.y, __instance.targetYRotation, 15f * Time.deltaTime), __instance.transform.eulerAngles.z);
                }
                __instance.timeSinceSpawn += Time.deltaTime;
                return false;
            }
            if (__instance.isEnemyDead)
            {
                __instance.SetClientCalculatingAI(enable: false);
                return false;
            }
            if (!__instance.inSpecialAnimation)
            {
                __instance.SetClientCalculatingAI(enable: true);
            }
            if (__instance.movingTowardsTargetPlayer && __instance.targetPlayer != null)
            {
                if (__instance.setDestinationToPlayerInterval <= 0f)
                {
                    __instance.setDestinationToPlayerInterval = 0.25f;
                    __instance.destination = RoundManager.Instance.GetNavMeshPosition(__instance.targetPlayer.transform.position, RoundManager.Instance.navHit, 2.7f);
                }
                else
                {
                    __instance.destination = new Vector3(__instance.targetPlayer.transform.position.x, __instance.destination.y, __instance.targetPlayer.transform.position.z);
                    __instance.setDestinationToPlayerInterval -= Time.deltaTime;
                }
                if (__instance.addPlayerVelocityToDestination > 0f)
                {
                    if (__instance.targetPlayer == GameNetworkManager.Instance.localPlayerController)
                    {
                        __instance.destination += Vector3.Normalize(__instance.targetPlayer.thisController.velocity * 100f) * __instance.addPlayerVelocityToDestination;
                    }
                    else if (__instance.targetPlayer.timeSincePlayerMoving < 0.25f)
                    {
                        __instance.destination += Vector3.Normalize((__instance.targetPlayer.serverPlayerPosition - __instance.targetPlayer.oldPlayerPosition) * 100f) * __instance.addPlayerVelocityToDestination;
                    }
                }
            }
            if (__instance.inSpecialAnimation)
            {
                return false;
            }
            if (__instance.updateDestinationInterval >= 0f)
            {
                __instance.updateDestinationInterval -= Time.deltaTime;
            }
            else
            {
                __instance.DoAIInterval();
                __instance.updateDestinationInterval = __instance.AIIntervalTime + UnityEngine.Random.Range(-0.015f, 0.015f);
            }
            if (Mathf.Abs(__instance.previousYRotation - __instance.transform.eulerAngles.y) > 6f)
            {
                __instance.previousYRotation = __instance.transform.eulerAngles.y;
                __instance.targetYRotation = __instance.previousYRotation;
                if (__instance.IsServer)
                {
                    __instance.UpdateEnemyRotationClientRpc((short)__instance.previousYRotation);
                }
                else
                {
                    __instance.UpdateEnemyRotationServerRpc((short)__instance.previousYRotation);
                }
            }
            return false;
        }*/
    }
}
