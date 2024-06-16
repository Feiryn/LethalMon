using System.Linq;
using GameNetcodeStuff;
using LethalMon.AI;
using UnityEngine;

namespace LethalMon;

public class Utils
{
    public static CustomAI? GetPlayerPet(PlayerControllerB player)
    {
        return GameObject.FindObjectsOfType<CustomAI>().FirstOrDefault(customAi => customAi.ownClientId == player.playerClientId);
    }

    public static Vector3 GetPositionInFrontOfPlayerEyes(PlayerControllerB player)
    {
        return player.playerEye.position + player.playerEye.forward * 2.5f;
    }
    
    public static Vector3 GetPositionBehindPlayer(PlayerControllerB player)
    {
        return player.transform.position + player.transform.forward * -2.0f;
    }
}