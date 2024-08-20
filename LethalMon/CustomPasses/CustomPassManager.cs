using System.Collections.Generic;
using System.Linq;
using UnityEngine.Rendering.HighDefinition;

namespace LethalMon.CustomPasses
{
    internal class CustomPassManager
    {
        public enum CustomPassType
        {
            SeeThroughEnemies,
            SeeThroughItems,
            SeeThroughPlayers
        }

        private Dictionary<CustomPassType, CustomPass> _customPasses = new Dictionary<CustomPassType, CustomPass>();

        private static CustomPassManager? _instance;
        public static CustomPassManager Instance
        {
            get
            {
                if (_instance == null)
                    _instance = new CustomPassManager();

                return _instance;
            }
        }

        private CustomPassVolume? _volume;
        public CustomPassVolume Volume
        {
            get
            {
                if (_volume == null)
                {
                    _volume = Utils.CurrentPlayer.gameplayCamera.gameObject.AddComponent<CustomPassVolume>();
                    configureVolume();
                }

                return _volume;
            }
        }

        private void configureVolume()
        {
            if (_volume == null) return;

            _volume.targetCamera = Utils.CurrentPlayer.gameplayCamera;
            _volume.injectionPoint = CustomPassInjectionPoint.BeforeTransparent;
            _volume.isGlobal = true;
        }

        private void InitCustomPass(CustomPassType type)
        {
            if (HasCustomPass(type)) return;

            CustomPass? customPass = null;
            switch (type)
            {
                case CustomPassType.SeeThroughEnemies:
                    {
                        var seeThrough = new SeeThroughCustomPass();
                        seeThrough.clearFlags = UnityEngine.Rendering.ClearFlag.None;
                        seeThrough.seeThroughLayer = 1 << (int)Utils.LayerMasks.Mask.Enemies;
                        seeThrough.ConfigureMaterial(Utils.EnemyHighlightOutline, Utils.EnemyHighlightInline, 0.04f);
                        customPass = seeThrough;
                        break;
                    }
                case CustomPassType.SeeThroughItems:
                    {
                        var seeThrough = new SeeThroughCustomPass();
                        seeThrough.clearFlags = UnityEngine.Rendering.ClearFlag.None;
                        seeThrough.seeThroughLayer = 1 << (int)Utils.LayerMasks.Mask.Props;
                        seeThrough.ConfigureMaterial(Utils.ItemHighlightOutline, Utils.ItemHighlightInline, 0.04f);
                        customPass = seeThrough;
                        break;
                    }
                    case CustomPassType.SeeThroughPlayers:
                    {
                        var seeThrough = new SeeThroughCustomPass();
                        seeThrough.clearFlags = UnityEngine.Rendering.ClearFlag.None;
                        seeThrough.seeThroughLayer = Utils.LayerMasks.ToInt(new[] { Utils.LayerMasks.Mask.Player, Utils.LayerMasks.Mask.PlayerRagdoll });
                        seeThrough.ConfigureMaterial(Utils.PlayerHighlightOutline, Utils.PlayerHighlightInline, 0.04f);
                        customPass = seeThrough;
                        break;
                    }
                default: break;
            }

            if(customPass != null)
            {
                _customPasses.Add(type, customPass);
                Volume.customPasses.Add(customPass);
            }
        }

        private void RemoveCustomPass(CustomPassType type)
        {
            Volume.customPasses.Remove(_customPasses[type]);
            _customPasses.Remove(type);
        }

        public bool HasCustomPass(CustomPassType type)
        {
            return _customPasses.ContainsKey(type);
        }

        public CustomPass? CustomPassOfType(CustomPassType type)
        {
            return _customPasses.GetValueOrDefault(type);
        }

        public bool IsCustomPassEnabled(CustomPassType type)
        {
            return _customPasses.ContainsKey(type) ? _customPasses[type].enabled : false;
        }

        public void EnableCustomPass(CustomPassType type, bool enable = true)
        {
            if (!enable && !HasCustomPass(type)) return;

            InitCustomPass(type); // ensure it exists
            _customPasses[type].enabled = enable;
        }

        public void CleanUp()
        {
            for(int i = _customPasses.Count - 1; i >= 0; i--)
                RemoveCustomPass(_customPasses.ElementAt(i).Key);

            UnityEngine.Object.DestroyImmediate(_volume);
        }
    }
}
