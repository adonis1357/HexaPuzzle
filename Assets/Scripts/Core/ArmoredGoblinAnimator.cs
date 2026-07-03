using UnityEngine;
using UnityEngine.UI;

namespace JewelsHexaPuzzle.Core
{
    /// <summary>
    /// 갑옷 고블린 스프라이트시트 애니메이터.
    ///
    /// 입력 스프라이트시트 (각 2048×256, 8프레임 × 256×256 가로 한 줄):
    ///   Resources/Sprites/ArmoredGoblinIdle    — 숨쉬기
    ///   Resources/Sprites/ArmoredGoblinWalk    — 이동/걷기
    ///   Resources/Sprites/ArmoredGoblinAttack  — 공격
    ///   Resources/Sprites/ArmoredGoblinHit     — 피격 (흰 플래시 포함)
    ///   Resources/Sprites/ArmoredGoblinDeath   — 죽음 (쓰러져 누움까지)
    ///
    /// 좌우 반전은 localScale.x 로 처리.
    ///
    /// 사용:
    ///   var animator = goblinObj.AddComponent&lt;ArmoredGoblinAnimator&gt;();
    ///   animator.Initialize(image);
    ///   animator.SetState(AnimState.Walking);
    ///   animator.SetFacing(true);
    /// </summary>
    public class ArmoredGoblinAnimator : MonoBehaviour
    {
        public enum AnimState { Idle, Walking, Attacking, Hit, Dying }

        // 시트 한 컷 사이즈 — 가로 배치, 프레임 개수/크기 모두 텍스처에서 자동 추출
        //   (Idle 7프레임 ×382, Walk/Attack/Hit/Death 8프레임 ×256 등 가변 사이즈 지원)
        //   기본값 8: 시트 로드 실패 시 fallback에 사용
        private const int DEFAULT_FRAMES_PER_ANIM = 8;

        // 애니메이션별 FPS
        private const float FPS_IDLE   = 4f;
        private const float FPS_WALK   = 20f;
        private const float FPS_ATTACK = 12f;
        private const float FPS_HIT    = 14f;
        private const float FPS_DEATH  = 10f;

        // ============================================================
        // 정적 프레임 캐시 (모든 인스턴스 공유)
        // ============================================================
        private static bool framesLoaded = false;
        private static Sprite[] idleFrames;
        private static Sprite[] walkFrames;
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

            idleFrames   = LoadSheet("ArmoredGoblinIdle");
            walkFrames   = LoadSheet("ArmoredGoblinWalk");
            attackFrames = LoadSheet("ArmoredGoblinAttack");
            hitFrames    = LoadSheet("ArmoredGoblinHit");
            deathFrames  = LoadSheet("ArmoredGoblinDeath");
        }

        /// <summary>
        /// 가로 한 줄 시트(2048×256, 8프레임 × 256)를 Sprite 배열로 분할.
        /// 시트 누락 시 null 반환.
        /// </summary>
        private static Sprite[] LoadSheet(string resourceName)
        {
            Texture2D tex = Resources.Load<Texture2D>("Sprites/" + resourceName);
            if (tex == null)
            {
                Debug.LogWarning($"[ArmoredGoblinAnimator] Resources/Sprites/{resourceName} 시트 없음 — 해당 상태에서 비주얼 갱신 안 됨");
                return null;
            }
            // 시트별 명시 프레임 수 (대두 크롭 후 발 baseline 정렬되어 가로/높이 비율이 가변)
            //   모든 액션 시트 8프레임 (Idle도 신규 업데이트로 8프레임)
            int frames = 8;
            int frameW = tex.width / frames;
            int frameH = tex.height;
            Sprite[] arr = new Sprite[frames];
            for (int c = 0; c < frames; c++)
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

            // HP 바/숫자 카운터 플립 — 미러링 상쇄
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
                case AnimState.Walking:   return walkFrames;
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
                case AnimState.Walking:   return FPS_WALK;
                case AnimState.Attacking: return FPS_ATTACK;
                case AnimState.Hit:       return FPS_HIT;
                case AnimState.Dying:     return FPS_DEATH;
            }
            return FPS_IDLE;
        }

        private static bool IsLooping(AnimState state)
        {
            return state == AnimState.Idle || state == AnimState.Walking;
        }
    }
}
