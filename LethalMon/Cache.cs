using System.Collections.Generic;
using GameNetcodeStuff;
using LethalMon.Behaviours;
using UnityEngine;

namespace LethalMon;

internal class Cache
{
    private static readonly Dictionary<int, TamedEnemyBehaviour?> TamedEnemyBehaviours = new();
    
    private static readonly Dictionary<ulong, TamedEnemyBehaviour?> PlayerPets = new();
    
    private static readonly Dictionary<int, PlayerControllerB> PlayersColliders = new();
    
    private static readonly Dictionary<int, GrabbableObject> GrabbableObjectsColliders = new();
    
    private static readonly Dictionary<int, AnimatedObjectTrigger> DoorsAnimatedObjectTriggers = new();
    
    private static readonly Dictionary<int, Collider> TerminalAccessibleObjectsColliders = new();
    
    private static readonly Dictionary<int, DoorLock> DoorLocksColliders = new();
    
    public static TamedEnemyBehaviour? GetTamedEnemyBehaviour(EnemyAI enemyAI)
    {
        if (enemyAI == null) return null;
        
        if (TamedEnemyBehaviours.TryGetValue(enemyAI.GetInstanceID(), out TamedEnemyBehaviour? tamedEnemyBehaviour))
        {
            return tamedEnemyBehaviour;
        }
        
        tamedEnemyBehaviour = enemyAI.GetComponent<TamedEnemyBehaviour>();
        TamedEnemyBehaviours.Add(enemyAI.GetInstanceID(), tamedEnemyBehaviour);
        
        return tamedEnemyBehaviour;
    }
    
    public static void SetPlayerPet(PlayerControllerB player, TamedEnemyBehaviour tamedEnemyBehaviour)
    {  
        PlayerPets[player.playerClientId] = tamedEnemyBehaviour;
    }
    
    public static void RemovePlayerPet(PlayerControllerB player)
    {
        PlayerPets.Remove(player.playerClientId);
    }

    public static void ClearPlayerPets()
    {
        PlayerPets.Clear();
    }
    
    public static bool GetPlayerPet(PlayerControllerB player, out TamedEnemyBehaviour? playerPet)
    {
        return PlayerPets.TryGetValue(player.playerClientId, out playerPet);
    }
    
    public static PlayerControllerB? GetPlayerFromCollider(Collider collider)
    {
        if (collider == null) return null;
        
        if (PlayersColliders.TryGetValue(collider.GetInstanceID(), out PlayerControllerB? player))
        {
            return player;
        }
        
        player = collider.gameObject.GetComponent<PlayerControllerB>();
        PlayersColliders.Add(collider.GetInstanceID(), player);
        
        return player;
    }
    
    public static GrabbableObject? GetGrabbableObjectFromCollider(Collider collider)
    {
        if (collider == null) return null;
        
        if (GrabbableObjectsColliders.TryGetValue(collider.GetInstanceID(), out GrabbableObject? grabbableObject))
        {
            return grabbableObject;
        }
        
        grabbableObject = collider.gameObject.GetComponent<GrabbableObject>();
        GrabbableObjectsColliders.Add(collider.GetInstanceID(), grabbableObject);
        
        return grabbableObject;
    }
    
    public static AnimatedObjectTrigger? GetDoorAnimatedObjectTrigger(DoorLock doorLock)
    {
        if (doorLock == null) return null;
        
        if (DoorsAnimatedObjectTriggers.TryGetValue(doorLock.GetInstanceID(), out AnimatedObjectTrigger? animatedObjectTrigger))
        {
            return animatedObjectTrigger;
        }
        
        animatedObjectTrigger = doorLock.GetComponent<AnimatedObjectTrigger>();
        DoorsAnimatedObjectTriggers.Add(doorLock.GetInstanceID(), animatedObjectTrigger);
        
        return animatedObjectTrigger;
    }
    
    public static Collider? GetTerminalAccessibleObjectCollider(TerminalAccessibleObject terminalAccessibleObject)
    {
        if (terminalAccessibleObject == null) return null;
        
        if (TerminalAccessibleObjectsColliders.TryGetValue(terminalAccessibleObject.GetInstanceID(), out Collider? collider))
        {
            return collider;
        }
        
        collider = terminalAccessibleObject.GetComponentInParent<Collider>();
        TerminalAccessibleObjectsColliders.Add(terminalAccessibleObject.GetInstanceID(), collider);
        
        return collider;
    }
    
    public static DoorLock? GetDoorLockFromCollider(Collider collider)
    {
        if (collider == null) return null;
        
        if (DoorLocksColliders.TryGetValue(collider.GetInstanceID(), out DoorLock? doorLock))
        {
            return doorLock;
        }
        
        doorLock = collider.gameObject.GetComponent<DoorLock>();
        DoorLocksColliders.Add(collider.GetInstanceID(), doorLock);
        
        return doorLock;
    }
}