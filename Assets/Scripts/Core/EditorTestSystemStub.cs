// ★ 보안(2026-07): 릴리스 빌드 전용 스텁.
//   실제 치트 패널(EditorTestSystem, 블록 설치·몬스터 소환·MP/게이지 치트·스킬 해금 취소)은
//   #if UNITY_EDITOR || DEVELOPMENT_BUILD 로 릴리스 바이너리에서 완전히 제외된다.
//   그러나 InputSystem·GameManager·각 Gauge·SkillTreeUI 등 여러 파일이 이 타입/멤버를
//   (에디터 모드 게이트 목적으로) 참조하므로, 릴리스에서도 컴파일이 되도록 동일 API의
//   빈 스텁을 제공한다. 모든 멤버는 무동작(치트 기능 없음)이라 보안상 안전하다.
#if !UNITY_EDITOR && !DEVELOPMENT_BUILD
using UnityEngine;
using JewelsHexaPuzzle.Data;

namespace JewelsHexaPuzzle.Core
{
    /// <summary>릴리스 전용 무동작 스텁 (치트 코드 제외). 실제 구현은 EditorTestSystem.cs 참조.</summary>
    public class EditorTestSystem : MonoBehaviour
    {
        public static bool IsGaugeAddMode() => false;

        public bool IsSkillUnlockCancelMode => false;
        public int LastModeChangeFrame => -1;

        public void InitializeUI(Canvas canvas, HexGrid grid) { }
        public void ShowPanel(bool show) { }
        public void DeactivateMode() { }

        public bool TryPlaceOnBlock(HexBlock block) => false;
        public bool TryPlaceMonsterAtCoord(HexCoord coord) => false;

        public bool IsEditorModeActive => false;
        public bool IsMonsterMode => false;
        public bool IsColorMode => false;
        public SpecialBlockType ActiveBlockType => SpecialBlockType.None;
        public GemType ActiveGemType => GemType.None;
        public GameObject PanelObject => null;
        public GameObject EditorToggleButtonObject => null;
    }
}
#endif
