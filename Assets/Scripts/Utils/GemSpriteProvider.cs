using UnityEngine;
using JewelsHexaPuzzle.Data;
using System.Collections.Generic;

namespace JewelsHexaPuzzle.Utils
{
    /// <summary>
    /// 젬 스프라이트 제공자.
    /// Resources/Gems/ 폴더에 외부 텍스처가 있으면 사용하고,
    /// 없으면 기존 프로시저럴 스프라이트로 fallback.
    ///
    /// 지원 텍스처 이름 규칙:
    ///   개별 컬러 텍스처: gem_red, gem_blue, gem_green, gem_yellow, gem_purple, gem_orange
    ///   공용 그레이스케일: gem_base (Image.color로 틴팅)
    ///   테두리: gem_border
    ///   배경: gem_background
    ///
    /// 텍스처는 투명 배경 PNG, Sprite (2D and UI) 타입으로 임포트 필요.
    /// </summary>
    public static class GemSpriteProvider
    {
        private static bool _initialized;
        private static bool _hasExternalTextures;
        private static bool _hasIndividualTextures;
        private static bool _hasBaseTexture;

        private static Sprite _baseSprite;
        private static Sprite _borderSprite;
        private static Sprite _backgroundSprite;
        private static Dictionary<GemType, Sprite> _gemSprites;

        // 외부 텍스처 사용 여부 (HexBlock에서 색상 처리 결정에 사용)
        public static bool HasExternalTextures => _hasExternalTextures;
        public static bool HasIndividualTextures => _hasIndividualTextures;

        public static void Initialize()
        {
            if (_initialized) return;
            _initialized = true;
            _gemSprites = new Dictionary<GemType, Sprite>();

            // 개별 컬러 텍스처 로드 시도
            LoadIndividualTextures();

            // 개별 텍스처가 없으면 공용 그레이스케일 로드 시도
            if (!_hasIndividualTextures)
                LoadBaseTexture();

            _hasExternalTextures = _hasIndividualTextures || _hasBaseTexture;

            // 테두리, 배경 로드
            _borderSprite = Resources.Load<Sprite>("Gems/gem_border");
            _backgroundSprite = Resources.Load<Sprite>("Gems/gem_background");

            if (_hasExternalTextures)
                Debug.Log($"[GemSpriteProvider] External textures loaded. Individual: {_hasIndividualTextures}, Base: {_hasBaseTexture}");
            else
                Debug.Log("[GemSpriteProvider] No external textures found, using procedural sprites");
        }

        private static void LoadIndividualTextures()
        {
            var mapping = new Dictionary<GemType, string>
            {
                { GemType.Red, "gem_red" },
                { GemType.Ruby, "gem_red" },
                { GemType.Blue, "gem_blue" },
                { GemType.Sapphire, "gem_blue" },
                { GemType.Green, "gem_green" },
                { GemType.Emerald, "gem_green" },
                { GemType.Yellow, "gem_yellow" },
                { GemType.Amber, "gem_yellow" },
                { GemType.Purple, "gem_purple" },
                { GemType.Amethyst, "gem_purple" },
                { GemType.Orange, "gem_orange" }
            };

            int loadedCount = 0;
            foreach (var kvp in mapping)
            {
                Sprite sprite = Resources.Load<Sprite>($"Gems/{kvp.Value}");
                if (sprite != null)
                {
                    _gemSprites[kvp.Key] = sprite;
                    loadedCount++;
                }
            }

            // 최소 기본 5색이 모두 있어야 개별 텍스처 모드 사용
            _hasIndividualTextures = loadedCount >= 5;
        }

        private static void LoadBaseTexture()
        {
            _baseSprite = Resources.Load<Sprite>("Gems/gem_base");
            _hasBaseTexture = _baseSprite != null;
        }

        /// <summary>
        /// 젬 타입에 맞는 스프라이트 반환.
        /// 개별 텍스처 > 공용 베이스 > null(프로시저럴 fallback) 순으로 시도.
        /// </summary>
        public static Sprite GetGemSprite(GemType type)
        {
            if (!_initialized) Initialize();

            // 개별 컬러 텍스처
            if (_hasIndividualTextures && _gemSprites.TryGetValue(type, out Sprite sprite))
                return sprite;

            // 공용 그레이스케일 베이스
            if (_hasBaseTexture)
                return _baseSprite;

            // 외부 텍스처 없음 → null 반환 (호출자가 프로시저럴 사용)
            return null;
        }

        /// <summary>
        /// 외부 테두리 스프라이트 반환. 없으면 null.
        /// </summary>
        public static Sprite GetBorderSprite()
        {
            if (!_initialized) Initialize();
            return _borderSprite;
        }

        /// <summary>
        /// 외부 배경 스프라이트 반환. 없으면 null.
        /// </summary>
        public static Sprite GetBackgroundSprite()
        {
            if (!_initialized) Initialize();
            return _backgroundSprite;
        }

        /// <summary>
        /// 특정 젬 타입에 색상을 적용해야 하는지 여부.
        /// 개별 컬러 텍스처면 false (이미 색상 포함), 그 외 true (틴팅 필요).
        /// </summary>
        public static bool NeedsTinting(GemType type)
        {
            if (_hasIndividualTextures && _gemSprites.ContainsKey(type))
                return false;
            return true;
        }
    }
}
