using System;
using System.Collections;
using System.Collections.Generic;
using GameNetcodeStuff;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Serialization;

namespace LethalMon.Behaviours;

internal class MouthDogTamedBehaviour : TamedEnemyBehaviour
{
    #region Properties
    private MouthDogAI? _mouthDog = null; // Replace with enemy class

    internal MouthDogAI MouthDog
    {
        get
        {
            if (_mouthDog == null)
                _mouthDog = (Enemy as MouthDogAI)!;

            return _mouthDog;
        }
    }

    public override bool Controllable => true;
    
    public override bool CanDefend => false;
    
    private EnemyController? _controller = null;
    
    private Coroutine? _lungeCoroutine = null;
    
    public EnemyAI? enemyBeingDamaged = null;
    #endregion

    #region Custom behaviours
    private enum CustomBehaviour
    {
        Riding = 1,
        DamageEnemy = 2,
        KillingPlayer = 3,
    }
    public override List<Tuple<string, string, Action>>? CustomBehaviourHandler =>
    [
        new Tuple<string, string, Action>(CustomBehaviour.Riding.ToString(), "Is being rode...", () => {}),
        new Tuple<string, string, Action>(CustomBehaviour.DamageEnemy.ToString(), "Is attacking an enemy...", () => {}),
        new Tuple<string, string, Action>(CustomBehaviour.KillingPlayer.ToString(), "Is killing a player...", () => {}),
    ];
    #endregion
    
    #region Base Methods

    public override void Awake()
    {
        base.Awake();
        
        if (TryGetComponent(out _controller) && _controller != null)
        {
            _controller.OnStartControlling = OnStartRiding;
            _controller.OnStopControlling = OnStopRiding;
            
            _controller.OnStopSprinting = OnStopSprinting;
            _controller.OnStartSprinting = OnStartSprinting;
            _controller.enemySpeedOutside = 10f;
            _controller.enemySpeedInside = 2f;
                
            _controller.enemyCanJump = false;
            _controller.enemyStrength = 0.5f;
            _controller.enemyStaminaUseMultiplier = 1.5f;
        }
    }

    public override void Start()
    {
        base.Start();

        if (IsTamed)
        {
            MouthDog.gameObject.transform.localScale = Vector3.one * 0.4f;
            MouthDog.creatureSFX.volume = 0f;
            MouthDog.creatureVoice.pitch = 1.4f;
            MouthDog.breathingSFX = null;
        }
    }
    
    private IEnumerator StopLungeCoroutine()
    {
        yield return new WaitForSeconds(1.5f);
        MouthDog.creatureAnimator.SetTrigger("EndLungeNoKill");
        _controller!.enemyCanSprint = true;
        _controller!.forceMoveForward = false;
        _controller!.StopSprinting(false);
    }

    private void OnStartSprinting()
    {
        MouthDog.creatureAnimator.Play("Base Layer.Chase");
    }
    
    private void OnStopSprinting()
    {
        MouthDog.creatureAnimator.SetTrigger("Lunge");
        _controller!.enemyCanSprint = false;
        _controller!.forceMoveForward = true;
        Utils.PlaySoundAtPosition(MouthDog.transform.position, MouthDog.screamSFX);
        _lungeCoroutine = StartCoroutine(StopLungeCoroutine());
    }
    
    internal void OnStartRiding()
    {
        if (Utils.IsHost)
            SwitchToCustomBehaviour((int)CustomBehaviour.Riding);
    }

    internal void OnStopRiding()
    {
        if(Utils.IsHost)
            SwitchToTamingBehaviour(TamingBehaviour.TamedFollowing);
    }

    public override void OnUpdate(bool update = false, bool doAIInterval = true)
    {
        base.OnUpdate(update, false);

        var position = MouthDog.transform.position;
        MouthDog.creatureAnimator.SetFloat("speedMultiplier", Vector3.ClampMagnitude(position - MouthDog.previousPosition, 1f).sqrMagnitude / (Time.deltaTime / 4f));
        MouthDog.previousPosition = position;
    }
    
    public override void OnEscapedFromBall(PlayerControllerB playerWhoThrewBall)
    {
        if (Utils.IsHost)
            StartCoroutine(EscapedFromBallCoroutine(playerWhoThrewBall));
    }
    
    public override void OnDestroy()
    {
        _controller!.StopControlling(true);
        Destroy(_controller!);

        if (enemyBeingDamaged != null)
        {
            enemyBeingDamaged.agent.enabled = true;
            enemyBeingDamaged.enabled = true;
        }
            
        base.OnDestroy();
    }
    
    public override void OnCallFromBall()
    {
        base.OnCallFromBall();

        if(IsOwnerPlayer)
            Utils.CallNextFrame(() => _controller!.AddTrigger("Ride"));

        _controller!.SetControlTriggerVisible();
    }

    public override void InitTamingBehaviour(TamingBehaviour behaviour)
    {
        base.InitTamingBehaviour(behaviour);

        if (behaviour == TamingBehaviour.TamedFollowing)
        {
            if (MouthDog != null)
            {
                if (MouthDog.agent != null)
                {
                    MouthDog.agent.speed = 8f;
                }
                MouthDog.creatureAnimator.Play("Base Layer.Idle1");
            }
        }
    }

    public override void InitCustomBehaviour(int behaviour)
    {
        base.InitCustomBehaviour(behaviour);
        
        if (behaviour == (int) CustomBehaviour.DamageEnemy)
        {
            MouthDog.creatureAnimator.Play("Base Layer.LungeKill");
        }
    }

    #endregion

    #region Methods
    private IEnumerator EscapedFromBallCoroutine(PlayerControllerB playerWhoThrewBall)
    {
        int chaseTime = 5;
        while (chaseTime > 0 && !playerWhoThrewBall.isPlayerDead)
        {
            chaseTime--;
            MouthDog.DetectNoise(playerWhoThrewBall.transform.position, float.MaxValue);
            yield return new WaitForSeconds(5f);
        }
    }
    
    private IEnumerator DamageEnemyCoroutine(EnemyAI enemyAI)
    {
        yield return new WaitForSeconds(5f);
        enemyBeingDamaged = null;
        _controller!.enemyCanSprint = true;
        _controller!.forceMoveForward = false;
        _controller!.StopSprinting(false);
        enemyAI.agent.enabled = true;
        enemyAI.enabled = true;
        enemyAI.HitEnemyOnLocalClient(2, default, ownerPlayer);
        enemyAI.SetEnemyStunned(true, 1f, ownerPlayer);
        
        SwitchToTamingBehaviour(TamingBehaviour.TamedFollowing);
    }

    private void DamageEnemy(EnemyAI enemyAI)
    {
        _controller!.StopControllingServerRpc();
        
        SwitchToCustomBehaviour((int) CustomBehaviour.DamageEnemy);
        
        if (_lungeCoroutine != null)
        {
            StopCoroutine(_lungeCoroutine);
            _lungeCoroutine = null;
        }
        
        enemyBeingDamaged = enemyAI;
        
        if (enemyAI.agent != null)
            enemyAI.enabled = false;
        enemyAI.enabled = false;
        
        StartCoroutine(DamageEnemyCoroutine(enemyAI));
    }

    public override void OnCollideWithEnemy(Collider other, EnemyAI collidedEnemy)
    {
        if (enemyBeingDamaged != null || !_controller!.forceMoveForward || collidedEnemy.isEnemyDead || !collidedEnemy.enemyType.canDie)
        {
            return;
        }
        
        TamedEnemyBehaviour tamedEnemyBehaviour = collidedEnemy.GetComponent<TamedEnemyBehaviour>();
        if (tamedEnemyBehaviour == null || !tamedEnemyBehaviour.IsTamed)
        {
            DamageEnemy(collidedEnemy);
        }
    }

    public override void OnCollideWithPlayer(Collider other)
    {
        if (!_controller!.forceMoveForward)
        {
            return;
        }
        
        PlayerControllerB playerControllerB = MouthDog.MeetsStandardPlayerCollisionConditions(other);
        if (playerControllerB == null || playerControllerB == ownerPlayer)
        {
            return;
        }
        
        _controller!.forceMoveForward = false;
        
        SwitchToCustomBehaviour((int) CustomBehaviour.KillingPlayer);
        
        MouthDog.KillPlayerServerRpc((int) playerControllerB.playerClientId);
    }

    public override void LateUpdate()
    {
        base.LateUpdate();
        
        if (enemyBeingDamaged != null)
        {
            enemyBeingDamaged.transform.position = MouthDog.mouthGrip.position;
        }
    }
    #endregion
    
    #region RPCs

    [ServerRpc(RequireOwnership = false)]
    private void LungeServerRpc()
    {
        LungeClientRpc();
    }
    
    [ClientRpc]
    private void LungeClientRpc()
    {

    }
    #endregion
}