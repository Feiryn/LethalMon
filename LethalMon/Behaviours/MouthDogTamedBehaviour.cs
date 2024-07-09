using System;
using System.Collections;
using GameNetcodeStuff;
using UnityEngine;

namespace LethalMon.Behaviours;

public class MouthDogTamedBehaviour : TamedEnemyBehaviour
{
    internal MouthDogAI mouthDog  { get; private set; }
    
    internal override void Start()
    {
        base.Start();

        mouthDog = (Enemy as MouthDogAI)!;
        if (mouthDog == null)
            mouthDog = gameObject.AddComponent<MouthDogAI>();

        if (ownerPlayer != null)
        {
            mouthDog.agent.speed = 5f;
            mouthDog.gameObject.transform.localScale = new Vector3(0.28f, 0.28f, 0.28f);
            mouthDog.creatureAnimator.Play("Base Layer.Idle1");
            mouthDog.creatureVoice.volume = 0f;
            mouthDog.creatureSFX.volume = 0f;
        }
    }

    internal override void OnUpdate(bool update = false, bool doAIInterval = true)
    {
        base.OnUpdate(update, false);

        var position = mouthDog.transform.position;
        mouthDog.creatureAnimator.SetFloat("speedMultiplier", Vector3.ClampMagnitude(position - mouthDog.previousPosition, 1f).sqrMagnitude / (Time.deltaTime / 4f));
        mouthDog.previousPosition = position;
    }

    private IEnumerator EscapedFromBallCoroutine(PlayerControllerB playerWhoThrewBall)
    {
        int chaseTime = 5;
        while (chaseTime > 0 && !playerWhoThrewBall.isPlayerDead)
        {
            chaseTime--;
            mouthDog.DetectNoise(playerWhoThrewBall.transform.position, float.MaxValue);
            yield return new WaitForSeconds(5f);
        }
    }
    
    internal override void OnEscapedFromBall(PlayerControllerB playerWhoThrewBall)
    {
        StartCoroutine(EscapedFromBallCoroutine(playerWhoThrewBall));
    }
}