using UnityEngine;
using JewelsHexaPuzzle.Data;
using System.Collections.Generic;

namespace JewelsHexaPuzzle.Utils
{
    public static class GemMaterialManager
    {
        private static Material _gemMat;
        private static Material _borderMat;
        private static Material _bgMat;
        private static Dictionary<SpecialBlockType, Material> _specialMats;

        public static Material GetGemMaterial()
        {
            if (_gemMat == null)
            {
                Shader shader = Shader.Find("UI/HexGem");
                if (shader == null)
                {
                    Debug.LogError("[GemMaterialManager] UI/HexGem shader NOT FOUND! Check Assets/Shaders/HexGem.shader exists and compiles.");
                    return null;
                }
                _gemMat = new Material(shader);
                _gemMat.name = "GemMaterial (Runtime)";
                Debug.Log("[GemMaterialManager] UI/HexGem shader loaded successfully!");
            }
            return _gemMat;
        }

        public static Material GetBorderGlowMaterial()
        {
            if (_borderMat == null)
            {
                Shader shader = Shader.Find("UI/HexBorderGlow");
                if (shader == null)
                {
                    Debug.LogWarning("[GemMaterialManager] UI/HexBorderGlow shader not found, using default");
                    return null;
                }
                _borderMat = new Material(shader);
                _borderMat.name = "BorderGlow (Runtime)";
            }
            return _borderMat;
        }

        public static Material GetBackgroundMaterial()
        {
            if (_bgMat == null)
            {
                Shader shader = Shader.Find("UI/HexBackground");
                if (shader == null)
                {
                    Debug.LogWarning("[GemMaterialManager] UI/HexBackground shader not found, using default");
                    return null;
                }
                _bgMat = new Material(shader);
                _bgMat.name = "Background (Runtime)";
            }
            return _bgMat;
        }

        public static Material GetSpecialGemMaterial(SpecialBlockType type)
        {
            if (_specialMats == null)
                _specialMats = new Dictionary<SpecialBlockType, Material>();

            if (!_specialMats.ContainsKey(type))
            {
                Shader shader = Shader.Find("UI/HexSpecialGem");
                if (shader == null)
                {
                    Debug.LogWarning("[GemMaterialManager] UI/HexSpecialGem shader not found, falling back to gem");
                    return GetGemMaterial();
                }

                Material mat = new Material(shader);
                mat.name = $"SpecialGem_{type} (Runtime)";

                // 파스텔 톤에 맞게 shimmer/energy 전체 감쇠
                switch (type)
                {
                    case SpecialBlockType.Drill:
                        mat.SetFloat("_ShimmerSpeed", 1.5f);
                        mat.SetFloat("_ShimmerIntensity", 0.08f);
                        mat.SetFloat("_EnergyPulse", 0.10f);
                        mat.SetFloat("_RainbowStrength", 0f);
                        break;
                    case SpecialBlockType.Bomb:
                        mat.SetFloat("_ShimmerSpeed", 0.8f);
                        mat.SetFloat("_ShimmerIntensity", 0.10f);
                        mat.SetFloat("_EnergyPulse", 0.18f);
                        mat.SetFloat("_RainbowStrength", 0f);
                        break;

                    case SpecialBlockType.XBlock:
                        mat.SetFloat("_ShimmerSpeed", 1.0f);
                        mat.SetFloat("_ShimmerIntensity", 0.08f);
                        mat.SetFloat("_EnergyPulse", 0.10f);
                        mat.SetFloat("_RainbowStrength", 0f);
                        break;
                    case SpecialBlockType.Rainbow:
                        mat.SetFloat("_ShimmerSpeed", 1.5f);
                        mat.SetFloat("_ShimmerIntensity", 0.10f);
                        mat.SetFloat("_EnergyPulse", 0.15f);
                        mat.SetFloat("_RainbowStrength", 0.4f);
                        break;
                    case SpecialBlockType.Drone:
                        mat.SetFloat("_ShimmerSpeed", 2.0f);
                        mat.SetFloat("_ShimmerIntensity", 0.12f);
                        mat.SetFloat("_EnergyPulse", 0.14f);
                        mat.SetFloat("_RainbowStrength", 0f);
                        break;
                }

                _specialMats[type] = mat;
            }

            return _specialMats[type];
        }

        public static void Cleanup()
        {
            if (_gemMat != null) Object.Destroy(_gemMat);
            if (_borderMat != null) Object.Destroy(_borderMat);
            if (_bgMat != null) Object.Destroy(_bgMat);

            if (_specialMats != null)
            {
                foreach (var mat in _specialMats.Values)
                    if (mat != null) Object.Destroy(mat);
                _specialMats.Clear();
            }

            _gemMat = null;
            _borderMat = null;
            _bgMat = null;
        }
    }
}
