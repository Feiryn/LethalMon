namespace LethalMon.Compatibility;

public abstract class ModCompatibility(string referenceChain)
{
    private bool? _enabled;
    
    public bool Enabled
    {
        get
        {
            if (_enabled == null)
            {
                _enabled = BepInEx.Bootstrap.Chainloader.PluginInfos.ContainsKey(referenceChain);
                LethalMon.Log(referenceChain + " enabled? " + _enabled);
            }

            return _enabled.Value;
        }
    }
}