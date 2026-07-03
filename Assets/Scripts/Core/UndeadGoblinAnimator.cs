using UnityEngine;
using UnityEngine.UI;

namespace JewelsHexaPuzzle.Core
{
    /// <summary>
    /// 언데드 고블린 idle 스프라이트시트 애니메이터.
    ///
    /// 입력 스프라이트시트:
    ///   Resources/Sprites/UndeadGoblinIdle  — 가만히 서있을 때 숨쉬기 (대기)
    ///     소스: 언데드고블린_숨쉬기.png (2026-06-26 교체본, 투명배경)
    ///     1280×1280, 5열 × 5행 = 25프레임, 각 256×256. 시트는 좌→우 / 상→하 순서.
    ///
    /// 언데드는 별도 Walk/Attack/Hit/Death 시트가 없어 idle 1종만 무한 루프한다.
    /// (이동/공격 중에도 숨쉬기 idle 유지 — "가만히 서있을 때" 연출 요구사항 충족)
    /// 기존 궁수/갑옷 애니메이터(8프레임 가로 한 줄)와 달리 5×5 그리드라 행 뒤집기 매핑 필요.
    /// </summary>
    public class UndeadGoblinAnimator : MonoBehaviour
    {
        // 시트 그리드 (5열 × 5행 = 25프레임)
        private const int COLS = 5;
        private const int ROWS = 5;
        private const int FRAME_COUNT = COLS * ROWS;
        private const float FPS_IDLE = 12f; // 25프레임 / 12fps ≈ 2.08초 루프 (숨쉬기 속도)

        // Static 프레임 캐시 (모든 언데드 인스턴스 공유)
        private static bool framesLoaded = false;
        private static Sprite[] idleFrames;

        // 인스턴스 상태
        private Image targetImage;
        private int frameIdx = 0;
        private float frameTimer = 0f;

        public void Initialize(Image image)
        {
            targetImage = image;
            EnsureFramesLoaded();
            frameIdx = 0;
            frameTimer = 0f;
            UpdateSprite();
        }

        private static void EnsureFramesLoaded()
        {
            if (framesLoaded) return;
            framesLoaded = true;

            Texture2D tex = Resources.Load<Texture2D>("Sprites/UndeadGoblinIdle");
            if (tex == null)
            {
                Debug.LogWarning("[UndeadGoblinAnimator] Sprites/UndeadGoblinIdle 로드 실패 — 프로시저럴 폴백 유지");
                return;
            }

            int fw = tex.width / COLS;
            int fh = tex.height / ROWS;
            Sprite[] arr = new Sprite[FRAME_COUNT];
            for (int r = 0; r < ROWS; r++)
            {
                for (int c = 0; c < COLS; c++)
                {
                    // 시트는 좌→우 / 상→하. Unity 텍스처는 bottom-left 원점이라
                    // 행(r=0=시트 최상단)을 뒤집어 텍스처 y로 매핑한다.
                    Rect rect = new Rect(c * fw, (ROWS - 1 - r) * fh, fw, fh);
                    arr[r * COLS + c] = Sprite.Create(tex, rect, new Vector2(0.5f, 0.5f), 100f);
                }
            }
            idleFrames = arr;
            Debug.Log($"[UndeadGoblinAnimator] idle {FRAME_COUNT}프레임 분할 완료 (텍스처 {tex.width}×{tex.height}, 프레임 {fw}×{fh})");
        }

        private void Update()
        {
            if (targetImage == null || idleFrames == null || idleFrames.Length == 0) return;

            frameTimer += Time.deltaTime;
            float frameDuration = 1f / FPS_IDLE;
            while (frameTimer >= frameDuration)
            {
                frameTimer -= frameDuration;
                frameIdx = (frameIdx + 1) % idleFrames.Length;
            }
            UpdateSprite();
        }

        private void UpdateSprite()
        {
            if (idleFrames == null || idleFrames.Length == 0 || targetImage == null) return;
            int idx = Mathf.Clamp(frameIdx, 0, idleFrames.Length - 1);
            if (idleFrames[idx] != null)
                targetImage.sprite = idleFrames[idx];
        }
    }
}
