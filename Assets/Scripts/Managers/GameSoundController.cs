using UnityEngine;
using JewelsHexaPuzzle.Core;

namespace JewelsHexaPuzzle.Managers
{
    /// <summary>
    /// 게임 사운드 컨트롤러 — 게임플레이 사운드 트리거를 중앙 관리
    /// RotationSystem, BlockRemovalSystem 등의 이벤트를 구독하여
    /// AudioManager에 사운드 재생을 위임합니다.
    ///
    /// 게임 로직(Core)과 오디오 엔진(AudioManager)의 브릿지 역할.
    /// 사운드 관련 수정은 이 파일에서 관리합니다.
    /// </summary>
    public class GameSoundController : MonoBehaviour
    {
        public static GameSoundController Instance { get; private set; }

        // 참조 (GameManager에서 초기화)
        private RotationSystem rotationSystem;
        private BlockRemovalSystem blockRemovalSystem;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else
            {
                Destroy(this);
            }
        }

        // ============================================================
        // 초기화 — 게임 시스템 이벤트 구독
        // ============================================================

        /// <summary>
        /// 게임 시스템 참조 설정 및 이벤트 구독
        /// GameManager.StartGame()에서 호출
        /// </summary>
        public void Initialize(RotationSystem rotation, BlockRemovalSystem removal)
        {
            // 기존 구독 해제 (재초기화 안전)
            Unsubscribe();

            rotationSystem = rotation;
            blockRemovalSystem = removal;

            // 이벤트 구독
            if (rotationSystem != null)
            {
                rotationSystem.OnRotationStarted += HandleRotationStarted;
                rotationSystem.OnMatchDetected += HandleMatchDetected;
                rotationSystem.OnRotationComplete += HandleRotationComplete;
            }

            Debug.Log("[GameSoundController] 초기화 완료 — 사운드 이벤트 구독됨");
        }

        private void OnDestroy()
        {
            Unsubscribe();
            if (Instance == this) Instance = null;
        }

        private void Unsubscribe()
        {
            if (rotationSystem != null)
            {
                rotationSystem.OnRotationStarted -= HandleRotationStarted;
                rotationSystem.OnMatchDetected -= HandleMatchDetected;
                rotationSystem.OnRotationComplete -= HandleRotationComplete;
            }
        }

        // ============================================================
        // 회전 사운드
        // ============================================================

        /// <summary>
        /// 블록 회전 시작 시 — 노이즈 밴드패스 스윕 사운드
        /// </summary>
        private void HandleRotationStarted()
        {
            if (AudioManager.Instance != null)
                AudioManager.Instance.PlayRotateSound();
        }

        // ============================================================
        // 매칭 사운드
        // ============================================================

        /// <summary>
        /// 매칭 감지 시 — 매칭된 블록 수에 따라 다른 아르페지오 재생
        /// 3매치: C6→E6→G6, 4매치: +C7, 5+매치: +E7
        /// </summary>
        private void HandleMatchDetected(int matchedBlockCount)
        {
            if (AudioManager.Instance != null)
                AudioManager.Instance.PlayMatchSound(matchedBlockCount);
        }

        // ============================================================
        // 회전 완료/실패 사운드
        // ============================================================

        /// <summary>
        /// 회전 완료 시 — 매칭 실패인 경우 실패음 재생
        /// matched = true: 사운드 없음 (이미 HandleMatchDetected에서 처리)
        /// matched = false: 하행 2음 실패 사운드
        /// </summary>
        private void HandleRotationComplete(bool matched)
        {
            if (!matched && AudioManager.Instance != null)
                AudioManager.Instance.PlayFailSound();
        }

        // ============================================================
        // 외부 호출용 사운드 메서드 (아이템, UI 등에서 직접 호출)
        // ============================================================

        /// <summary>회전 사운드 직접 재생 (SwapItem, LineDrawItem 등)</summary>
        public void PlayRotateSound()
        {
            if (AudioManager.Instance != null)
                AudioManager.Instance.PlayRotateSound();
        }

        /// <summary>매칭 사운드 직접 재생</summary>
        public void PlayMatchSound(int count = 3)
        {
            if (AudioManager.Instance != null)
                AudioManager.Instance.PlayMatchSound(count);
        }

        /// <summary>실패 사운드 직접 재생</summary>
        public void PlayFailSound()
        {
            if (AudioManager.Instance != null)
                AudioManager.Instance.PlayFailSound();
        }
    }
}
