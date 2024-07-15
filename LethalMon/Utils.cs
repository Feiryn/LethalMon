using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using GameNetcodeStuff;
using LethalMon.Behaviours;
using Unity.Netcode;
using UnityEngine;
using Object = UnityEngine.Object;
using Random = System.Random;

namespace LethalMon;

public class Utils
{
    public static readonly Random Random = new Random();
    
    public static TamedEnemyBehaviour? GetPlayerPet(PlayerControllerB player)
    {
        return GameObject.FindObjectsOfType<TamedEnemyBehaviour>().FirstOrDefault(tamedBehaviour => tamedBehaviour.ownClientId == player.playerClientId && tamedBehaviour.isOutsideOfBall);
    }

    public static Vector3 GetPositionInFrontOfPlayerEyes(PlayerControllerB player)
    {
        return player.playerEye.position + player.playerEye.forward * 2.5f;
    }
    
    public static Vector3 GetPositionBehindPlayer(PlayerControllerB player)
    {
        return player.transform.position + player.transform.forward * -2.0f;
    }

    public static EnemyAI? GetMostProbableAttackerEnemy(PlayerControllerB player, StackTrace stackTrace)
    {
        StackFrame[] stackFrames = stackTrace.GetFrames();

        foreach (StackFrame stackFrame in stackFrames[1..])
        {
            Type classType = stackFrame.GetMethod().DeclaringType;
            LethalMon.Log("Stackframe type: " + classType);

            if (classType.IsSubclassOf(typeof(EnemyAI)))
            {
                LethalMon.Log("Class is assignable from EnemyAI");
                EnemyAI? closestEnemy = null;
                float? closestEnemyDistance = float.MaxValue;
                
                Collider[] colliders = Physics.OverlapSphere(player.transform.position, 10f);
                foreach (Collider collider in colliders)
                {
                    EnemyAI? enemyAI = collider.GetComponentInParent<EnemyAI>();
                    if (enemyAI != null && enemyAI.GetType() == classType)
                    {
                        float distance = Vector3.Distance(player.transform.position, enemyAI.transform.position);
                        if (closestEnemyDistance > distance)
                        {
                            closestEnemy = enemyAI;
                            closestEnemyDistance = distance;
                        }
                    }
                }

                if (closestEnemy != null)
                {
                    return closestEnemy;
                }
            }
        }
        
        return null;
    }
    
    private static Dictionary<string, TerminalNode> infoNodes = new Dictionary<string, TerminalNode>();
    
    public static TerminalNode CreateTerminalNode(string name, string description)
    {
        if (infoNodes.TryGetValue(name, out var terminalNode)) return terminalNode;
        
        TerminalNode node = ScriptableObject.CreateInstance<TerminalNode>();
        Object.DontDestroyOnLoad(node);
        node.clearPreviousText = true;
        node.name = name + "InfoNode";
        node.displayText = description + "\n\n";
        infoNodes.Add(name, node);
        return node;
    }

    #region Player
    public static List<PlayerControllerB>? AllPlayers => StartOfRound.Instance?.allPlayerScripts?.Where(pcb => pcb != null && (pcb.isPlayerControlled || pcb.isPlayerDead)).ToList();
    public static List<PlayerControllerB>? AlivePlayers => AllPlayers?.Where(pcb => !pcb.isPlayerDead).ToList();

    public static PlayerControllerB CurrentPlayer => GameNetworkManager.Instance.localPlayerController;

    public static ulong? CurrentPlayerID => CurrentPlayer?.playerClientId;

    public static bool IsHost
    {
        get
        {
            if (NetworkManager.Singleton != null)
                return NetworkManager.Singleton.IsHost || NetworkManager.Singleton.IsServer;

            if (CurrentPlayerID.HasValue)
                return CurrentPlayerID.Value == 0ul;

            return false;
        }
    }

    public static readonly float DefaultJumpForce = 13f;
    public static float DefaultPlayerSpeed => CurrentPlayer.isSprinting ? 2.25f : 1f;
    #endregion

    #region Enemy
    public static List<EnemyType> EnemyTypes => Resources.FindObjectsOfTypeAll<EnemyType>().ToList();

    public static void OpenDoorsAsEnemyAroundPosition(Vector3 position)
    {
        Collider[] colliders = Physics.OverlapSphere(position, 0.5f);
        foreach (Collider collider in colliders)
        {
            DoorLock doorLock = collider.GetComponentInParent<DoorLock>();
            if (doorLock != null && !doorLock.isDoorOpened && !doorLock.isLocked)
            {
                LethalMon.Log("Door opened at " + position);
                if (doorLock.gameObject.TryGetComponent(out AnimatedObjectTrigger trigger))
                {
                    trigger.TriggerAnimationNonPlayer(false, true, false);
                }

                doorLock.OpenDoorAsEnemyServerRpc();
            }
        }
    }

    public enum Enemy // EnemyType.name
    {
        BaboonHawk,
        Blob,
        BushWolf,
        Butler,
        ButlerBees,
        Centipede,
        ClaySurgeon,
        Crawler,
        DocileLocustBees,
        Doublewing,
        FlowerSnake,
        RedLocustBees,
        DressGirl,
        Flowerman,
        ForestGiant,
        HoarderBug,
        Jester,
        LassoMan,
        MaskedPlayerEnemy,
        MouthDog,
        Nutcracker,
        Puffer,
        RadMech,
        RedPillEnemyType,
        SandSpider,
        SandWorm,
        SpringMan
    }

    public static Bounds? RealEnemyBounds(EnemyAI enemy)
    {
        if (enemy == null) return null;

        var renderers = enemy.gameObject.GetComponentsInChildren<Renderer>();
        if (renderers == null) return null;

        var bounds = renderers[0].bounds;
        for (var i = 1; i < renderers.Length; ++i)
            bounds.Encapsulate(renderers[i].bounds);

        return bounds;
    }
    #endregion
}