using System;
using System.Collections.Generic;
using UnityEngine;

namespace Bow.Art
{
    /// <summary>캐릭터 꾸밈 파트 (Docs/05_캐릭터_파트_DSL.md)</summary>
    [Serializable]
    public class CharacterPart
    {
        public string kind = "rect";     // rect | circle | tri | soft | ring
        public string anchor = "torso";  // feet | belt | torso | back | head | bow
        public float x = 0f, y = 0f;
        public float w = 0.1f, h = 0.1f;
        public float rot = 0f;
        public string color = "body";    // body | bodyDark | bodyLight | accent | team | paper | #hex
        public float alpha = 1f;
        public int order = 0;            // −3 ~ +3
        public bool mirrorX = false;
        public string name = "";
    }

    /// <summary>
    /// 캐릭터 능력치 (1~5, 기본 3). 모든 캐릭터 합계 18로 밸런스.
    /// 체력: 최대 HP / 파워: 화살 속도 / 바람저항: 바람 영향 감소 / 장전: 기본 딜레이 단축
    /// 호흡: 호흡 효율 보너스(최대 감소율↑) / 정확: 추 미터 느림 + 각도 오차 감소
    /// </summary>
    [Serializable]
    public class CharacterStats
    {
        public int hp = 3, power = 3, wind = 3, reload = 3, breath = 3, accuracy = 3;

        public static readonly string[] Labels = { "체력", "파워", "바람저항", "장전", "호흡", "정확" };
        public int[] Values { get { return new[] { hp, power, wind, reload, breath, accuracy }; } }
        public int Total { get { return hp + power + wind + reload + breath + accuracy; } }

        private static int C(int v) { return v < 1 ? 1 : (v > 5 ? 5 : v); }

        public int MaxHp(int baseHp) { return baseHp + (C(hp) - 3) * 15; }            // 70 ~ 130
        public float SpeedMul { get { return 1f + (C(power) - 3) * 0.06f; } }          // 0.88 ~ 1.12
        public float WindMul { get { return 1f - (C(wind) - 3) * 0.12f; } }            // 1.24 ~ 0.76 (낮을수록 바람 영향 적음)
        public float DelayMul { get { return 1f - (C(reload) - 3) * 0.08f; } }         // 1.16 ~ 0.84
        public float ReductionBonus { get { return (C(breath) - 3) * 0.04f; } }        // −0.08 ~ +0.08 (최대 감소율 0.62~0.78)
        public float MeterPeriodMul { get { return 1f + (C(accuracy) - 3) * 0.08f; } } // 0.84 ~ 1.16 (높을수록 추가 느림)
        public float ErrorMul { get { return 1f - (C(accuracy) - 3) * 0.08f; } }       // 1.16 ~ 0.84

        /// <summary>가장 높은 능력치 1~2개 이름 (특징 표시용)</summary>
        public string Highlights()
        {
            int[] v = Values; string a = "", b = ""; int ba = -1, bb = -1;
            for (int i = 0; i < v.Length; i++) if (v[i] > ba) { bb = ba; b = a; ba = v[i]; a = Labels[i]; } else if (v[i] > bb) { bb = v[i]; b = Labels[i]; }
            if (bb >= 4 && ba >= 4) return a + " · " + b;
            return a;
        }
    }

    /// <summary>캐릭터 정의 (characters.json 한 항목)</summary>
    [Serializable]
    public class CharacterDef
    {
        public string id = "default";
        public string name = "궁수";
        public string theme = "기본";
        public string desc = "";
        public string bodyColor = "#1E1B18";
        public string accentColor = "#C9A227";
        public string headShape = "circle";  // circle | oval | square | tri
        public float[] headScale = null;     // [sx, sy]
        public string bowStyle = "classic";  // classic | recurve | longbow | mech
        public string bowColor = "body";
        public CharacterStats stats = new CharacterStats();
        public CharacterPart[] parts = new CharacterPart[0];

        public Color Body { get { return Palette.Hex(bodyColor); } }
        public Color Accent { get { return Palette.Hex(accentColor); } }
        public Vector2 HeadScale
        {
            get
            {
                if (headScale == null || headScale.Length < 2) return Vector2.one;
                return new Vector2(Mathf.Clamp(headScale[0], 0.5f, 2f), Mathf.Clamp(headScale[1], 0.5f, 2f));
            }
        }

        /// <summary>색 역할 문자열 → 실제 색</summary>
        public Color Resolve(string role, Color team)
        {
            if (string.IsNullOrEmpty(role)) return Body;
            switch (role)
            {
                case "body": return Body;
                case "bodyDark": return Color.Lerp(Body, Color.black, 0.2f);
                case "bodyLight": return Color.Lerp(Body, Palette.Paper, 0.25f);
                case "accent": return Accent;
                case "team": return team;
                case "paper": return Palette.Paper;
                default:
                    if (role.StartsWith("#")) return Palette.Hex(role);
                    return Body;
            }
        }
    }

    [Serializable]
    public class CharacterCatalogData
    {
        public CharacterDef[] characters = new CharacterDef[0];
    }

    /// <summary>Resources/Characters/characters.json 로더 (없으면 기본 궁수 1종)</summary>
    public static class CharacterCatalog
    {
        private static CharacterDef[] all;

        public static CharacterDef[] All
        {
            get { if (all == null) Load(); return all; }
        }

        public static int Count { get { return All.Length; } }

        public static void Reload() { all = null; Load(); }

        private static void Load()
        {
            List<CharacterDef> list = new List<CharacterDef>();
            TextAsset ta = Resources.Load<TextAsset>("Characters/characters");
            if (ta != null)
            {
                try
                {
                    CharacterCatalogData data = JsonUtility.FromJson<CharacterCatalogData>(ta.text);
                    if (data != null && data.characters != null)
                        foreach (CharacterDef d in data.characters)
                            if (d != null && !string.IsNullOrEmpty(d.id)) list.Add(d);
                }
                catch (Exception e) { Debug.LogWarning("[활] characters.json 파싱 실패: " + e.Message); }
            }
            if (list.Count == 0) list.Add(DefaultDef());
            all = list.ToArray();
            Debug.Log("[활] 캐릭터 " + all.Length + "종 로드");
        }

        public static CharacterDef DefaultDef()
        {
            return new CharacterDef { id = "default", name = "먹 궁수", theme = "기본", desc = "한지 위의 이름 없는 궁수" };
        }

        public static CharacterDef Get(string id)
        {
            CharacterDef[] a = All;
            for (int i = 0; i < a.Length; i++) if (a[i].id == id) return a[i];
            return a[0];
        }

        public static int IndexOf(string id)
        {
            CharacterDef[] a = All;
            for (int i = 0; i < a.Length; i++) if (a[i].id == id) return i;
            return 0;
        }

        public static CharacterDef At(int index)
        {
            CharacterDef[] a = All;
            if (a.Length == 0) return DefaultDef();
            index = ((index % a.Length) + a.Length) % a.Length;
            return a[index];
        }
    }
}
