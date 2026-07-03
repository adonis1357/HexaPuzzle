using System;
using UnityEngine;

namespace HexaPuzzle.RuneBlocks
{
    /// <summary>
    /// 6가지 룬 컬러. 헥사 퍼즐의 매칭 단위.
    /// 각 컬러는 색맹 접근성을 위해 고유 룬 문양과 1:1로 매칭됩니다.
    /// </summary>
    public enum RuneColor
    {
        Ruby     = 0, // 빨강  / 삼각형 (불)
        Amber    = 1, // 주황  / 마름모
        Citrine  = 2, // 노랑  / 별
        Emerald  = 3, // 초록  / 잎
        Sapphire = 4, // 파랑  / 물방울
        Amethyst = 5, // 보라  / 소용돌이 (신비)
    }

    /// <summary>RuneColor별 상수: 표시 이름, 한글, oklch hue.</summary>
    public static class RuneColorMeta
    {
        public struct Entry
        {
            public RuneColor color;
            public string englishName;
            public string koreanName;
            public string koreanColor;
            public float hue;           // oklch hue, 0..360
        }

        public static readonly Entry[] All = new Entry[]
        {
            new Entry { color = RuneColor.Ruby,     englishName = "Ruby",     koreanName = "루비",     koreanColor = "빨강", hue =  25f },
            new Entry { color = RuneColor.Amber,    englishName = "Amber",    koreanName = "앰버",     koreanColor = "주황", hue =  60f },
            new Entry { color = RuneColor.Citrine,  englishName = "Citrine",  koreanName = "시트린",   koreanColor = "노랑", hue =  95f },
            new Entry { color = RuneColor.Emerald,  englishName = "Emerald",  koreanName = "에메랄드", koreanColor = "초록", hue = 155f },
            new Entry { color = RuneColor.Sapphire, englishName = "Sapphire", koreanName = "사파이어", koreanColor = "파랑", hue = 245f },
            new Entry { color = RuneColor.Amethyst, englishName = "Amethyst", koreanName = "자수정",   koreanColor = "보라", hue = 305f },
        };

        public static Entry Get(RuneColor c) => All[(int)c];
        public static int Count => All.Length;
    }
}
