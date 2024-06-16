namespace LethalMon.AI;

public class RedLocustBeesCustomAI : CustomAI
{
    public override void Start()
    {
        base.Start();
        
        this.creatureSFX.Stop(); // Do not make sound
    }

    public override void DoAIInterval()
    {
        base.DoAIInterval();
        
        this.FollowOwner();
    }
}