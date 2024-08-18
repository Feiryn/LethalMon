using GameNetcodeStuff;
using System.Collections.Generic;
using System;
using LethalMon.Items;
using UnityEngine;
using System.Collections;
using Unity.Netcode;
using LethalMon.Patches;
using System.Linq;

namespace LethalMon.Behaviours
{
#if DEBUG
    internal class ManeaterTamedBehaviour : TamedEnemyBehaviour
    {
        #region Properties
        private CaveDwellerAI? _maneater = null; // Replace with enemy class
        internal CaveDwellerAI Maneater
        {
            get
            {
                if (_maneater == null)
                    _maneater = (Enemy as CaveDwellerAI)!;

                return _maneater;
            }
        }

        private bool IsChild => Maneater.babyContainer.activeSelf;
        private bool IsAdult => Maneater.adultContainer.activeSelf;
        private bool _transformAnimationRecorded = false;

        // Status handling
        private enum Status
        {
            Calm,
            Scared,
            Stressed,
            VeryStressed,
            Lonely,
            LeftAlone,
            StartAttacking
        }
        private Status _currentStatus = Status.Calm;

        private const float LonelinessPoint = 0.4f; // Point after which the baby feels lonely
        private const float LeftAlonePoint = 0.85f; // Point after which the baby feels left alone, shortly before attacking

        private const float StressedPoint = 0.5f; // ManeaterMemory.likeMeter point below which the baby feels stressed towards it
        private const float VeryStressedPoint = 0.15f; // ManeaterMemory.likeMeter point below which the baby feels under huge pressure, shortly before attacking

        internal override string DefendingBehaviourDescription => "You can change the displayed text when the enemy is defending by something more precise... Or remove this line to use the default one";

        internal override bool CanDefend => false; // You can return false to prevent the enemy from switching to defend mode in some cases (if already doing another action or if the enemy can't defend at all)
        #endregion

        #region Cooldowns
        private const string AttackingCooldownID = "maneater_attacking";
    
        internal override Cooldown[] Cooldowns => [new Cooldown(AttackingCooldownID, "Attacking", 10f)];

        private CooldownNetworkBehaviour? attackingCooldown;
        #endregion

        #region Custom behaviours
        internal enum CustomBehaviour
        {
            Transforming = 1,
            Attacking,
        }
        internal override List<Tuple<string, string, Action>>? CustomBehaviourHandler =>
        [
            new (CustomBehaviour.Transforming.ToString(), "Transforming", OnTransformBehaviour),
            new (CustomBehaviour.Attacking.ToString(), "Attacking", OnAttackingBehaviour)
        ];

        internal override void InitCustomBehaviour(int behaviour)
        {
            // ANY CLIENT
            base.InitCustomBehaviour(behaviour);

            switch ((CustomBehaviour)behaviour)
            {
                case CustomBehaviour.Transforming:
                    if (IsOwner)
                    {
                        if (IsChild)
                            BecomeAdultServerRpc();
                        else
                            BecomeChildServerRpc();

                        Invoke(nameof(Transformed), 2.2f);
                    }
                    break;
                case CustomBehaviour.Attacking:
                    // ...
                    break;
                default:
                    break;
            }
        }

        internal void OnTransformBehaviour() { }

        internal void Transformed()
        {
            if (IsAdult)
                SwitchToCustomBehaviour((int)CustomBehaviour.Attacking);
            else
                SwitchToTamingBehaviour(TamingBehaviour.TamedFollowing);
        }
        internal void OnAttackingBehaviour()
        {

        }

        internal void OnTestBehavior()
        {
            /* USE THIS SOMEWHERE TO ACTIVATE THE CUSTOM BEHAVIOR
                *   SwitchToCustomBehaviour((int)CustomBehaviour.TestBehavior);
            */
        }
        #endregion

        #region Action Keys
        private readonly List<ActionKey> _actionKeys =
        [
            new ActionKey() { Key = ModConfig.Instance.ActionKey1, Description = "Transform" }
        ];
        internal override List<ActionKey> ActionKeys => _actionKeys;

        internal override void ActionKey1Pressed()
        {
            base.ActionKey1Pressed();

            /*foreach (var para in Maneater.babyCreatureAnimator.parameters)
                LethalMon.Log(para.name, LethalMon.LogType.Warning);
            LethalMon.Log("--------------");
            foreach (var para in Maneater.creatureAnimator.parameters)
                LethalMon.Log(para.name, LethalMon.LogType.Warning);*/

            if (IsChild)
                BecomeAdultServerRpc();
            else
                BecomeChildServerRpc();
        }
        #endregion

        #region Base Methods
        internal override void Start()
        {
            SetTamedByHost_DEBUG();
            base.Start();
            SwitchToTamingBehaviour(TamingBehaviour.TamedFollowing);

            attackingCooldown = GetCooldownWithId(AttackingCooldownID);
        }

        internal override void InitTamingBehaviour(TamingBehaviour behaviour)
        {
            // ANY CLIENT
            base.InitTamingBehaviour(behaviour);

            switch(behaviour)
            {
                case TamingBehaviour.TamedFollowing:
                    EnableActionKeyControlTip(ModConfig.Instance.ActionKey1, true);
                    Maneater.lonelinessMeter = 0f;
                    UpdateHUDStatus();
                    break;

                case TamingBehaviour.TamedDefending:
                    break;

                default: break;
            }
        }

        internal override void OnTamedFollowing()
        {
            // OWNER ONLY
            base.OnTamedFollowing();
        }

        internal override void OnTamedDefending()
        {
            // OWNER ONLY
            base.OnTamedDefending();
        }

        internal override void OnEscapedFromBall(PlayerControllerB playerWhoThrewBall)
        {
            // ANY CLIENT
            base.OnEscapedFromBall(playerWhoThrewBall);
        }

        internal override void OnUpdate(bool update = false, bool doAIInterval = true)
        {
            base.OnUpdate(update, false);
        }

        public override PokeballItem? RetrieveInBall(Vector3 position)
        {
            // ANY CLIENT
            return base.RetrieveInBall(position);
        }

        public override bool CanBeTeleported()
        {
            // HOST ONLY
            return base.CanBeTeleported();
        }
        #endregion

        internal void DetectNoise(Vector3 noisePosition, float noiseLoudness, int timesPlayedInOneSpot = 0, int noiseID = 0)
        {
            if (noiseID == 6 || noiseID == 7 || (!IsServer && noiseID != 75) || Maneater.isEnemyDead || noiseLoudness <= 0.1f || Vector3.Distance(noisePosition, Maneater.transform.position + Vector3.up * 0.4f) < 0.8f)
                return;

            float num = Vector3.Distance(noisePosition, Maneater.transform.position);
            if (Physics.Linecast(Maneater.transform.position, noisePosition, StartOfRound.Instance.collidersAndRoomMaskAndDefault, QueryTriggerInteraction.Ignore))
            {
                noiseLoudness *= 0.6f;
                num += 3f;
            }
        }

        #region Memories
        internal struct ManeaterMemory(GameObject gameObject)
        {
            public GameObject gameObject { get; set; } = gameObject;
            public float likeMeter { get; set; } = 1f;
            public bool scared { get; set; } = false;
        }

        private bool MemoryInLineOfSight(ManeaterMemory memory) => Maneater.CheckLineOfSightForPosition(memory.gameObject.transform.position, 180f);

        private List<ManeaterMemory> _maneaterMemories = new List<ManeaterMemory>();

        internal List<ManeaterMemory> MemoriesInLoS
        {
            get
            {
                var memories = _maneaterMemories.Where(MemoryInLineOfSight);
                return memories.Any() ? memories.ToList() : [];
            }
        }

        internal ManeaterMemory? LeastLikedMemoryInLoS
        {
            get
            {
                var memoriesInLoS = MemoriesInLoS;
                if (memoriesInLoS.Count == 0)
                    return null;

                var min = memoriesInLoS.Min(mem => mem.likeMeter);
                return memoriesInLoS.Where(mem => mem.likeMeter == min).First();
            }
        }

        private List<ManeaterMemory> MemoriesInLoSForStatus(Status status)
        {
            IEnumerable<ManeaterMemory> memories;
            if (status == Status.Scared)
                memories = _maneaterMemories.Where(mem => mem.scared);
            else if (status == Status.Stressed)
                memories = _maneaterMemories.Where(mem => mem.likeMeter < StressedPoint);
            else if (status == Status.VeryStressed)
                memories = _maneaterMemories.Where(mem => mem.likeMeter < VeryStressedPoint);
            else
                return [];

            return memories.Any() ? memories.ToList() : [];
        }
        #endregion

        #region Status
        private Status CurrentStatus
        {
            get
            {
                var memory = LeastLikedMemoryInLoS;
                if (!memory.HasValue || Maneater.lonelinessMeter > (1f - memory.Value.likeMeter))
                {
                    if (Maneater.lonelinessMeter > 1f)
                        return Status.StartAttacking;

                    if (Maneater.lonelinessMeter > LeftAlonePoint)
                        return Status.LeftAlone;

                    return Maneater.lonelinessMeter > LonelinessPoint ? Status.Lonely : Status.Calm;
                }

                if (memory.Value.likeMeter <= 0f)
                    return Status.StartAttacking;

                if (memory.Value.likeMeter < VeryStressedPoint)
                    return Status.VeryStressed;

                if (memory.Value.likeMeter < StressedPoint)
                    return Status.Stressed;

                return memory.Value.scared ? Status.Scared : Status.Calm;
            }
        }

        internal void UpdateStatus()
        {
            var currentStatus = CurrentStatus;
            if (_currentStatus == currentStatus)
                return;
            _currentStatus = currentStatus;

            UpdateHUDStatus();
        }

        internal void UpdateHUDStatus()
        {
            switch (_currentStatus)
            {
                case Status.Calm:
                    HUDManagerPatch.UpdateTamedMonsterAction("Feels good.");
                    break;
                case Status.Scared:
                    HUDManagerPatch.UpdateTamedMonsterAction("Scared of " + string.Join(", ", MemoriesInLoSForStatus(_currentStatus)));
                    break;
                case Status.Stressed:
                    HUDManagerPatch.UpdateTamedMonsterAction("Stressed from " + string.Join(", ", MemoriesInLoSForStatus(_currentStatus)));
                    break;
                case Status.VeryStressed:
                    HUDManagerPatch.UpdateTamedMonsterAction("Very stressed from " + string.Join(", ", MemoriesInLoSForStatus(_currentStatus)) + ". ATTENTION!");
                    break;
                case Status.Lonely:
                    HUDManagerPatch.UpdateTamedMonsterAction("Starts to feel lonely.");
                    break;
                case Status.LeftAlone:
                    HUDManagerPatch.UpdateTamedMonsterAction("Feels left alone. ATTENTION!");
                    break;
                default:
                    break;
            }
        }
        #endregion

        #region Baby
        internal void BabyUpdate()
        {
            UpdateStatus();
            if(_currentStatus == Status.StartAttacking)
            {

            }
        }
        internal void BabyAIInterval()
        {

        }
        #endregion

        #region Adult
        internal void AdultUpdate()
        {

        }
        internal void AdultAIInterval()
        {

        }
        #endregion

        #region Transforming
        [ServerRpc(RequireOwnership = false)]
        public void BecomeChildServerRpc()
        {
            BecomeChildClientRpc();
        }

        [ClientRpc]
        public void BecomeChildClientRpc()
        {
            Maneater.agent.acceleration = 35f;
            Maneater.agent.angularSpeed = 220;
            Maneater.syncMovementSpeed = 0.26f;
            Maneater.addPlayerVelocityToDestination = 0f;
            Maneater.updatePositionThreshold = 0.26f;
            Maneater.propScript.EnablePhysics(enable: true);
            Maneater.propScript.grabbable = true;
            Maneater.propScript.grabbableToEnemies = true;
            Maneater.propScript.enabled = true;
            Maneater.inSpecialAnimation = true;
            Maneater.agent.enabled = false;
            Maneater.StartCoroutine(becomeChildAnimation());
        }

        private IEnumerator becomeChildAnimation()
        {
            Maneater.creatureSFX.volume = 0.5f;
            Maneater.creatureSFX.PlayOneShot(Maneater.transformationSFX);
            WalkieTalkie.TransmitOneShotAudio(Maneater.creatureSFX, Maneater.transformationSFX);

            yield return StartCoroutine(Utils.StartPlaybackOfAnimator(Maneater.creatureAnimator, true));

            LethalMon.Log("Enabling baby.");
            Maneater.babyContainer.SetActive(value: true);
            Maneater.adultContainer.SetActive(value: false);
            yield return new WaitForSeconds(0.05f);

            yield return StartCoroutine(Utils.StartPlaybackOfAnimator(Maneater.babyCreatureAnimator, true));
            Maneater.babyCreatureAnimator.SetBool("Transform", false);

            Maneater.creatureSFX.volume = 1f;

            // todo: find out why it plays the animation normally again afterwards
            Maneater.inSpecialAnimation = false;
            if (IsOwner)
                Maneater.agent.enabled = true;
        }

        [ServerRpc(RequireOwnership = false)]
        public void BecomeAdultServerRpc()
        {
            BecomeAdultClientRpc();
        }

        [ClientRpc]
        public void BecomeAdultClientRpc()
        {
            if (!_transformAnimationRecorded)
                Maneater.StartCoroutine(RecordTransformAnimation());
            Maneater.StartTransformationAnim();
            Maneater.addPlayerVelocityToDestination = 0f;
        }

        public IEnumerator RecordTransformAnimation()
        {
            yield return StartCoroutine(Utils.RecordAnimation(Maneater.babyCreatureAnimator, 0.5f));
            yield return new WaitForSeconds(0.05f);
            yield return StartCoroutine(Utils.RecordAnimation(Maneater.creatureAnimator, 1.7f));
            _transformAnimationRecorded = true;
        }
        #endregion
    }
#endif
}
