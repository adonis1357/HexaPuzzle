using UnityEngine;

namespace JewelsHexaPuzzle.Data
{
    /// <summary>
    /// 레벨 데이터 - 한 레벨의 모든 설정을 담는 데이터 클래스.
    /// Stage 모드와 Infinite 모드 모두 이 클래스로 통합 관리한다.
    /// </summary>
    [System.Serializable]
    public class LevelData
    {
        // 기본 정보
        public int levelId;                        // 고유 레벨 번호
        public string levelName;                   // 표시 이름
        public string subtitle;                    // 부제목
        public GameMode gameMode;                  // Stage 또는 Infinite
        public DifficultyType difficultyType;          // 난이도 (Easy/Normal/Hard)
        public bool isLocked;                      // 잠금 상태
        public int unlockRequirement;              // 해금 조건 (선행 레벨 ID, 0이면 즉시 해금)

        // 모드별 설정 (사용하는 모드의 것만 할당)
        public InfiniteConfig infiniteConfig;      // Infinite 모드일 때 사용

        // 로비 표시
        public LobbyDisplayConfig lobbyDisplay;    // 로비 버튼 비주얼
    }

    /// <summary>
    /// Infinite 모드 설정 - 생존 미션 웨이브 설정
    /// </summary>
    [System.Serializable]
    public class InfiniteConfig
    {
        public int initialMoves = 15;              // 초기 이동 횟수
        public int activeGemTypeCount = 5;         // 활성 보석 색상 수
    }

    /// <summary>
    /// 로비 버튼 비주얼 설정
    /// </summary>
    [System.Serializable]
    public class LobbyDisplayConfig
    {
        public Color backgroundColor;
        public Color borderColor;
        public float buttonSize = 200f;
    }
}
