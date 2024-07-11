using System.Collections;
using GameNetcodeStuff;
using Unity.Netcode;
using UnityEngine;

namespace LethalMon.Behaviours;

public class MouthDogTamedBehaviour : TamedEnemyBehaviour
{
    #region Properties
    internal MouthDogAI mouthDog  { get; private set; }

    private float CumulatedCheckDogsSeconds = 0f;
    
    private static readonly float CheckDogsSecondsInterval = 1f;

    private float HowlTimer = 0f;
    
    private bool howled = false;

    private float HowlCooldown = 0f;
    
    private static readonly float HowlCooldownSeconds = 5f;
    #endregion
    
    #region Base Methods
    internal override void Start()
    {
        base.Start();

        mouthDog = (Enemy as MouthDogAI)!;
        if (mouthDog == null)
            mouthDog = gameObject.AddComponent<MouthDogAI>();

        if (ownerPlayer != null)
        {
            mouthDog.gameObject.transform.localScale = new Vector3(0.28f, 0.28f, 0.28f);
            mouthDog.creatureSFX.volume = 0f;
            mouthDog.creatureVoice.pitch = 1.4f;
            mouthDog.breathingSFX = null;
            WalkMode();
        }
    }

    internal override void OnUpdate(bool update = false, bool doAIInterval = true)
    {
        base.OnUpdate(update, false);

        var position = mouthDog.transform.position;
        mouthDog.creatureAnimator.SetFloat("speedMultiplier", Vector3.ClampMagnitude(position - mouthDog.previousPosition, 1f).sqrMagnitude / (Time.deltaTime / 4f));
        mouthDog.previousPosition = position;

        if (HowlCooldown > 0f)
        {
            HowlCooldown -= Time.deltaTime;
        }
    }
    
    internal override void OnEscapedFromBall(PlayerControllerB playerWhoThrewBall)
    {
        StartCoroutine(EscapedFromBallCoroutine(playerWhoThrewBall));
    }

    internal override void OnTamedFollowing()
    {
        base.OnTamedFollowing();

        if (HowlCooldown <= 0f)
        {
            DefendOwnerFromCloseDogs();
        }
    }
    
    internal override void OnTamedDefending()
    {
        base.OnTamedDefending();

        if (HowlTimer > 0f)
        {
            HowlTimer -= Time.deltaTime;
            return;
        }

        if (howled)
        {
            if (targetEnemy != null)
            {
                ((MouthDogAI)targetEnemy!).suspicionLevel = 0;
                targetEnemy = null;
                WalkServerRpc();
            }
        }
        else
        {
            if (Vector3.Distance(mouthDog.transform.position, mouthDog.destination) < 1f)
            {
                HowlServerRpc();
                HowlTimer = mouthDog.screamSFX.length;
                howled = true;
                HowlCooldown = HowlCooldownSeconds;
                var noisePosition = mouthDog.transform.position;
                ((MouthDogAI)targetEnemy!).lastHeardNoisePosition = noisePosition;
                ((MouthDogAI)targetEnemy!).DetectNoise(noisePosition, float.MaxValue);
            }
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
            mouthDog.DetectNoise(playerWhoThrewBall.transform.position, float.MaxValue);
            yield return new WaitForSeconds(5f);
        }
    }
    
    private void DefendOwnerFromCloseDogs()
    {
        CumulatedCheckDogsSeconds += Time.deltaTime;
        
        if (CumulatedCheckDogsSeconds < CheckDogsSecondsInterval) return;
        
        CumulatedCheckDogsSeconds = 0f;

        MouthDogAI[] mouthDogAIs = FindObjectsOfType<MouthDogAI>();
        foreach (MouthDogAI mouthDogAI in mouthDogAIs)
        {
            if (mouthDogAI == mouthDog || mouthDogAI.isEnemyDead || Vector3.Distance(mouthDogAI.transform.position, ownerPlayer!.transform.position) > 20f) continue;
            
            bool foundPath = false;
            for (float i = 15f; i > 7f && !foundPath; i--)
            {
                Vector3[] farPositions = GetPositionsFarFromOwner(mouthDogAI, i);
                foreach (Vector3 farPosition in farPositions)
                {
                    if (mouthDog.SetDestinationToPosition(farPosition, true))
                    {
                        foundPath = true;
                        break;
                    }
                }
            }

            if (foundPath)
            {
                ChaseServerRpc();
                targetEnemy = mouthDogAI;
                howled = false;
                HowlTimer = 0f;
                SwitchToTamingBehaviour(TamingBehaviour.TamedDefending);
                return;
            }
        }
    }
    
    private Vector3[] GetPositionsFarFromOwner(MouthDogAI enemyToDefendFrom, float distance)
    {
        const int sidesPoints = 10;
        const float sidesPointsDistance = 0.5f;

        Vector3 enemyPosition = enemyToDefendFrom.transform.position;
        Vector3 direction = Vector3.Normalize(enemyPosition - ownerPlayer!.transform.position);
        Vector3 destinationPoint = enemyPosition + direction * distance;
        
        Vector3[] positionsArray = new Vector3[1 + sidesPoints * 2];
        positionsArray[0] = destinationPoint;

        Vector2 projectedDirectionOnXY = new Vector2(direction.x, direction.y);
        Vector2 perpendicular = Vector2.Perpendicular(projectedDirectionOnXY);

        for (int i = 1; i <= sidesPoints; ++i)
        {
            positionsArray[1 + (i - 1) * 2] = new Vector3(destinationPoint.x + perpendicular.x * sidesPointsDistance * i, destinationPoint.y + perpendicular.y * sidesPointsDistance * i, destinationPoint.z);
            positionsArray[2 + (i - 1) * 2] = new Vector3(destinationPoint.x - perpendicular.x * sidesPointsDistance * i, destinationPoint.y - perpendicular.y * sidesPointsDistance * i, destinationPoint.z);
        }

        for (int i = 0; i < positionsArray.Length; ++i)
        {
            RaycastHit hitInfo;
            if (Physics.Raycast(new Ray(positionsArray[i], Vector3.down), out hitInfo, 30f, 268437761, QueryTriggerInteraction.Ignore))
            {
                positionsArray[i] = hitInfo.point;
            }
            else if (Physics.Raycast(new Ray(positionsArray[i], Vector3.up), out hitInfo, 30f, 268437761, QueryTriggerInteraction.Ignore))
            {
                positionsArray[i] = hitInfo.point;
            }
        }

        return positionsArray;
    }
    
    private void WalkMode()
    {
        mouthDog.agent.speed = 5f;
        mouthDog.creatureAnimator.Play("Base Layer.Idle1");
    }
    
    private void ChaseMode()
    {
        mouthDog.agent.speed = 13f;
        mouthDog.creatureAnimator.Play("Base Layer.Chase");
    }

    private void Howl()
    {
        mouthDog.creatureAnimator.Play("Base Layer.ChaseHowl");
        mouthDog.creatureVoice.PlayOneShot(mouthDog.screamSFX);
    }
    #endregion
    
    #region RPCs
    [ServerRpc(RequireOwnership = false)]
    public void WalkServerRpc()
    {
        WalkClientRpc();
    }
    
    [ClientRpc]
    public void WalkClientRpc()
    {
        WalkMode();
    }
    
    [ServerRpc(RequireOwnership = false)]
    public void ChaseServerRpc()
    {
        ChaseClientRpc();
    }
    
    [ClientRpc]
    public void ChaseClientRpc()
    {
        ChaseMode();
    }
    
    [ServerRpc(RequireOwnership = false)]
    public void HowlServerRpc()
    {
        HowlClientRpc();
    }
    
    [ClientRpc]
    public void HowlClientRpc()
    {
        Howl();
    }
    #endregion
}