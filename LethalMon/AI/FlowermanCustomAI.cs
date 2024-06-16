using UnityEngine;

namespace LethalMon.AI;

public class FlowermanCustomAI : CustomAI
{
    private Vector3 agentLocalVelocity;
    
    public Transform animationContainer;
    
    private float velX;
    
    private float velZ;
    
    public override void Start()
    {
        base.Start();
        
        creatureAnimator.SetBool("sneak", value: true);
        this.creatureAnimator.Play("Base Layer.CreepForward");
    }

    public override void DoAIInterval()
    {
        base.DoAIInterval();
        
        this.FollowOwner();
    }

    public override void Update()
    {
        base.Update();

        CalculateAnimationDirection();
    }

    private void CalculateAnimationDirection(float maxSpeed = 1f)
    {
        agentLocalVelocity = animationContainer.InverseTransformDirection(Vector3.ClampMagnitude(base.transform.position - previousPosition, 1f) / (Time.deltaTime * 2f));
        velX = Mathf.Lerp(velX, agentLocalVelocity.x, 10f * Time.deltaTime);
        creatureAnimator.SetFloat("VelocityX", Mathf.Clamp(velX, 0f - maxSpeed, maxSpeed));
        velZ = Mathf.Lerp(velZ, 0f - agentLocalVelocity.y, 10f * Time.deltaTime);
        creatureAnimator.SetFloat("VelocityZ", Mathf.Clamp(velZ, 0f - maxSpeed, maxSpeed));
        creatureAnimator.SetFloat("speedMultiplier", Vector3.ClampMagnitude(base.transform.position - previousPosition, 1f).sqrMagnitude / (Time.deltaTime / 4f));
        previousPosition = base.transform.position;
    }

    public override void CopyProperties(EnemyAI enemyAI)
    {
        base.CopyProperties(enemyAI);

        this.animationContainer = ((FlowermanAI) enemyAI).animationContainer;
    }
}