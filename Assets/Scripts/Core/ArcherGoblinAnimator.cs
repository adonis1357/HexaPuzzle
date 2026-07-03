using UnityEngine;
using UnityEngine.UI;

namespace JewelsHexaPuzzle.Core
{
    /// <summary>
    /// 궁수 고블린 스프라이트시트 애니메이터.
    ///
    /// 입력 스프라이트시트 (각 2048×256, 8프레임 × 256×256 가로 한 줄):
    ///   Resources/Sprites/ArcherGoblinIdle    — 숨쉬기 (대기)
    ///   Resources/Sprites/ArcherGoblinAttack  — 공격 (활 시위 당김 → 화살 발사)
    ///   Resources/Sprites/ArcherGoblinHit     — 피격 (흰 플래시 포함)
    ///   Resources/Sprites/ArcherGoblinDeath   — 죽음 (쓰러져 누움까지)
    ///
    /// 궁수는 고정 배치라 Walk 시트가 없음 — 4상태(Idle/Attacking/Hit/Dying)만 지원.
    ///
    /// 좌우 반전은 localScale.x 로 처리.
    /// </summary>
    public class ArcherGoblinAnimator : MonoBehaviour
    {
        public enum AnimState { Idle, Attacking, Hit, Dying }

        // 시트 한 컷 사이즈 (모든 시트 동일)
        private const int FRAMES_PER_ANIM = 8;
        // FRAME_SIZE 제거 — 대두 크롭 후 시트마다 프레임 크기가 가변. LoadSheet에서 tex.width/FRAMES_PER_ANIM로 자동 계산

        // 애니메이션별 FPS
        private const float FPS_IDLE   = 4f;
        private const float FPS_ATTACK = 12f;
        private const float FPS_HIT    = 14f;
        private const float FPS_DEATH  = 10f;

        // ============================================================
        // 정적 프레임 캐시 (모든 인스턴스 공유)
        // ============================================================
        private static bool framesLoaded = false;
        private static Sprite[] idleFrames;
        private static Sprite[] attackFrames;
        private static Sprite[] hitFrames;
        private static Sprite[] deathFrames;

        // ============================================================
        // 인스턴스 상태
        // ============================================================
        private Image targetImage;
        private RectTransform targetRt;
        private AnimState currentState = AnimState.Idle;
        private int frameIdx = 0;
        private float frameTimer = 0f;
        private bool facingRight = true;
        private System.Action onCurrentAnimComplete;

        // ============================================================
        // 초기화
        // ============================================================
        public void Initialize(Image image)
        {
            targetImage = image;
            if (image != null) targetRt = image.rectTransform;
            EnsureFramesLoaded();
            currentState = AnimState.Idle;
            frameIdx = 0;
            frameTimer = 0f;
            UpdateSprite();
        }

        private static void EnsureFramesLoaded()
        {
            if (framesLoaded) return;
            framesLoaded = true;

            idleFrames   = LoadSheet("ArcherGoblinIdle");
            attackFrames = LoadSheet("ArcherGoblinAttack");
            hitFrames    = LoadSheet("ArcherGoblinHit");
            deathFrames  = LoadSheet("ArcherGoblinDeath");
        }

        private static Sprite[] LoadSheet(string resourceName)
        {
            Texture2D tex = Resources.Load<Texture2D>("Sprites/" + resourceName);
            if (tex == null)
            {
                Debug.LogWarning($"[ArcherGoblinAnimator] Resources/Sprites/{resourceName} 시트 없음 — 해당 상태에서 비주얼 갱신 안 됨");
                return null;
            }
            // 시트마다 다른 프레임 크기를 자동 처리 (대두 크롭 후 가변 사이즈)
            //   모든 시트는 8프레임 가로 배치
            int frameW = tex.width / FRAMES_PER_ANIM;
            int frameH = tex.height;
            Sprite[] arr = new Sprite[FRAMES_PER_ANIM];
            for (int c = 0; c < FRAMES_PER_ANIM; c++)
            {
                Rect rect = new Rect(c * frameW, 0, frameW, frameH);
                arr[c] = Sprite.Create(tex, rect, new Vector2(0.5f, 0.5f), 100f);
            }
            return arr;
        }

        // ============================================================
        // 외부 API
        // ============================================================
        public void SetState(AnimState state, System.Action onComplete = null)
        {
            if (currentState == AnimState.Dying) return;
            if (currentState == state && IsLooping(state) && onComplete == null) return;

            currentState = state;
            frameIdx = 0;
            frameTimer = 0f;
            onCurrentAnimComplete = onComplete;
            UpdateSprite();
        }

        public void SetFacing(bool right)
        {
            if (facingRight == right) return;
            facingRight = right;
            ApplyFacing();
        }

        private void ApplyFacing()
        {
            if (targetRt == null) return;
            var s = targetRt.localScale;
            float absX = Mathf.Abs(s.x);
            if (absX < 0.0001f) absX = 1f;
            s.x = facingRight ? absX : -absX;
            targetRt.localScale = s;

            CounterFlipChild("HPBarBg");
            CounterFlipChild("HPText");
        }

        private void CounterFlipChild(string childName)
        {
            if (targetRt == null) return;
            var child = targetRt.Find(childName);
            if (child == null) return;
            var s = child.localScale;
            float absX = Mathf.Abs(s.x);
            if (absX < 0.0001f) absX = 1f;
            s.x = facingRight ? absX : -absX;
            child.localScale = s;
        }

        // ============================================================
        // 매 프레임
        // ============================================================
        private void Update()
        {
            if (targetImage == null) return;
            Sprite[] frames = GetFramesForCurrentState();
            if (frames == null || frames.Length == 0) return;

            float fps = GetFpsForCurrentState();
            frameTimer += Time.deltaTime;

            float frameDuration = 1f / fps;
            while (frameTimer >= frameDuration)
            {
                frameTimer -= frameDuration;
                AdvanceFrame(frames);
                if (currentState == AnimState.Dying && frameIdx == frames.Length - 1)
                    return;
            }
            UpdateSprite();
        }

        private void AdvanceFrame(Sprite[] frames)
        {
            frameIdx++;
            if (frameIdx >= frames.Length)
            {
                if (IsLooping(currentState))
                {
                    frameIdx = 0;
                }
                else
                {
                    frameIdx = frames.Length - 1;
                    var cb = onCurrentAnimComplete;
                    onCurrentAnimComplete = null;
                    cb?.Invoke();

                    if (currentState != AnimState.Dying)
                    {
                        currentState = AnimState.Idle;
                        frameIdx = 0;
                        frameTimer = 0f;
                    }
                }
            }
        }

        private void UpdateSprite()
        {
            Sprite[] frames = GetFramesForCurrentState();
            if (frames == null || frames.Length == 0) return;
            int idx = Mathf.Clamp(frameIdx, 0, frames.Length - 1);
            if (targetImage != null && frames[idx] != null)
                targetImage.sprite = frames[idx];
        }

        private Sprite[] GetFramesForCurrentState()
        {
            switch (currentState)
            {
                case AnimState.Idle:      return idleFrames;
                case AnimState.Attacking: return attackFrames;
                case AnimState.Hit:       return hitFrames;
                case AnimState.Dying:     return deathFrames;
            }
            return idleFrames;
        }

        private float GetFpsForCurrentState()
        {
            switch (currentState)
            {
                case AnimState.Idle:      return FPS_IDLE;
                case AnimState.Attacking: return FPS_ATTACK;
                case AnimState.Hit:       return FPS_HIT;
                case AnimState.Dying:     return FPS_DEATH;
            }
            return FPS_IDLE;
        }

        private static bool IsLooping(AnimState state)
        {
            return state == AnimState.Idle;
        }
    }
}
