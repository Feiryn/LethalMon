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
    }
    public override List<Tuple<string, string, Action>>? CustomBehaviourHandler =>
    [
        new Tuple<string, string, Action>(CustomBehaviour.Riding.ToString(), "Is being rode...", () => {}),
        new Tuple<string, string, Action>(CustomBehaviour.DamageEnemy.ToString(), "Is attacking an enemy...", () => {})
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
        LungeServerRpc();
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
            Utils.CallAfterTime(() => _controller!.AddTrigger("Ride"), 0.5f);

        _controller!.SetControlTriggerVisible();
    }
    
    public override void OnRetrieveInBall()
    {
        base.OnRetrieveInBall();
            
        _controller!.SetControlTriggerVisible(false);
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
        if (IsOwnerPlayer)
        {
            enemyAI.HitEnemyOnLocalClient(2, default, ownerPlayer);
        }

        enemyAI.SetEnemyStunned(true, 1f, ownerPlayer);
        
        if (Utils.IsHost)
            SwitchToTamingBehaviour(TamingBehaviour.TamedFollowing);
    }

    private void DamageEnemy(EnemyAI enemyAI)
    {
        _controller!.StopControlling();
        
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
        if (!IsOwnerPlayer || enemyBeingDamaged != null || !_controller!.forceMoveForward || collidedEnemy.isEnemyDead || !collidedEnemy.enemyType.canDie)
        {
            return;
        }
        
        TamedEnemyBehaviour? tamedEnemyBehaviour = Cache.GetTamedEnemyBehaviour(collidedEnemy);
        NetworkObject networkObject = collidedEnemy.GetComponent<NetworkObject>();
        if ((tamedEnemyBehaviour == null || !tamedEnemyBehaviour.IsTamed) && networkObject != null)
        {
            enemyBeingDamaged = collidedEnemy;
            DamageEnemyServerRpc(networkObject);
        }
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
        MouthDog.creatureAnimator.SetTrigger("Lunge");
        _controller!.enemyCanSprint = false;
        _controller!.forceMoveForward = true;
        Utils.PlaySoundAtPosition(MouthDog.transform.position, MouthDog.screamSFX);
        _lungeCoroutine = StartCoroutine(StopLungeCoroutine());
    }
    
    [ServerRpc(RequireOwnership = false)]
    private void DamageEnemyServerRpc(NetworkObjectReference enemyAI)
    {
        DamageEnemyClientRpc(enemyAI);
    }
    
    [ClientRpc]
    private void DamageEnemyClientRpc(NetworkObjectReference enemyAI)
    {
        if (enemyAI.TryGet(out NetworkObject enemyObject) && enemyObject.TryGetComponent(out EnemyAI enemy))
        {
            DamageEnemy(enemy);
        }
    }
    #endregion
}