using UnityEngine;
using UnityEngine.UI;

namespace JewelsHexaPuzzle.Core
{
    /// <summary>
    /// 몽둥이 고블린 스프라이트시트 애니메이터.
    ///
    /// 입력 스프라이트시트: Resources/Sprites/ClubGoblinSheet (1774×887 PNG)
    ///   - 12 columns × 5 rows
    ///   - 좌측 6칸 = 오른쪽 바라보기, 우측 6칸 = 왼쪽 바라보기 (미러)
    ///   - 본 컴포넌트는 좌측 6프레임만 사용하고, 좌우 반전은 localScale.x로 처리
    ///
    /// 행(top→bottom):
    ///   0: Idle  (가만히 서서 숨쉬기)
    ///   1: Walk  (이동)
    ///   2: Attack (공격 — 몽둥이 휘두름)
    ///   3: Hit   (피격)
    ///   4: Death (사망 — 무너짐 → 재 → 해골)
    ///
    /// 사용:
    ///   var animator = goblinObj.AddComponent&lt;ClubGoblinAnimator&gt;();
    ///   animator.Initialize(image);
    ///   animator.SetState(AnimState.Walking);
    ///   animator.SetFacing(true);  // 오른쪽
    /// </summary>
    public class ClubGoblinAnimator : MonoBehaviour
    {
        public enum AnimState { Idle, Walking, Attacking, Hit, Dying }

        // ============================================================
        // 시트 레이아웃 — 1774×887 PNG, 행마다 높이가 다른 비균일 그리드
        //   PIL 이미지 분석으로 측정한 정확한 행/열 좌표 사용 (균등 분할은 어긋남)
        //   좌측 6프레임만 사용, 우측 6프레임은 미러 → localScale.x로 동적 반전
        // ============================================================
        private const int FRAMES_PER_ANIM = 6;

        // === LEFT 6프레임 X 범위 (이미지 좌표, top-left origin) ===
        //   x=30~810, 균등 6분할 → 폭 130px 슬라이스
        private const float LEFT_X_START = 30f;
        private const float LEFT_X_END = 810f;
        private const float FRAME_W = (LEFT_X_END - LEFT_X_START) / FRAMES_PER_ANIM; // 130

        // === 각 행의 Y 범위 (이미지 좌표, top → bottom) ===
        //   분석 결과:
        //     Idle:   image y=41-159  → 30-170 (padding 포함)
        //     Walk:   image y=183-301 → 175-315
        //     Attack: image y=340-482 → 325-495 (몽둥이 들어 올림 = 키 큼)
        //     Hit:    image y=524-640 → 510-660
        //     Death:  image y=726-830 → 705-855
        private static readonly float[] ROW_Y_TOP    = {  30f, 175f, 325f, 510f, 705f };
        private static readonly float[] ROW_Y_BOTTOM = { 170f, 315f, 495f, 660f, 855f };

        private const int ROW_IDLE   = 0;
        private const int ROW_WALK   = 1;
        private const int ROW_ATTACK = 2;
        private const int ROW_HIT    = 3;
        private const int ROW_DEATH  = 4;

        // 애니메이션별 FPS
        private const float FPS_IDLE   = 4f;
        private const float FPS_WALK   = 20f;  // 2배 빠른 보행 애니메이션 (기존 10fps → 20fps)
        private const float FPS_ATTACK = 12f;
        private const float FPS_HIT    = 14f;
        private const float FPS_DEATH  = 10f;  // 죽음 시트 8프레임 × 10fps = 0.8초

        // 죽음 전용 스프라이트시트 (Resources/Sprites/ClubGoblinDeath, 2048×256, 8프레임 × 256px)
        private const int DEATH_FRAMES = 8;
        private const int DEATH_FRAME_SIZE = 256;

        // 걷기 전용 스프라이트시트 (Resources/Sprites/ClubGoblinWalk, 2048×256, 8프레임 × 256px)
        private const int WALK_FRAMES = 8;
        private const int WALK_FRAME_SIZE = 256;

        // 숨쉬기(Idle) 전용 스프라이트시트 (Resources/Sprites/ClubGoblinIdle, 2048×256, 8프레임 × 256px)
        private const int IDLE_FRAMES = 8;
        private const int IDLE_FRAME_SIZE = 256;

        // 공격(Attack) 전용 스프라이트시트 (Resources/Sprites/ClubGoblinAttack, 2048×256, 8프레임 × 256px)
        private const int ATTACK_FRAMES = 8;
        private const int ATTACK_FRAME_SIZE = 256;

        // 피격(Hit) 전용 스프라이트시트 (Resources/Sprites/ClubGoblinHit, 2048×256, 8프레임 × 256px)
        private const int HIT_FRAMES = 8;
        private const int HIT_FRAME_SIZE = 256;

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
            // 시작 상태: Idle 첫 프레임
            currentState = AnimState.Idle;
            frameIdx = 0;
            frameTimer = 0f;
            UpdateSprite();
        }

        private static void EnsureFramesLoaded()
        {
            if (framesLoaded) return;
            framesLoaded = true; // 실패해도 재시도 안 함

            Texture2D tex = Resources.Load<Texture2D>("Sprites/ClubGoblinSheet");
            if (tex == null)
            {
                Debug.LogError("[ClubGoblinAnimator] Resources/Sprites/ClubGoblinSheet 텍스처를 찾을 수 없습니다.");
                return;
            }

            // === 공격(Attack) 애니메이션 — 별도 시트(Resources/Sprites/ClubGoblinAttack) 사용 ===
            //   2048×256, 8프레임 × 256×256px (가로 한 줄). 몽둥이 휘두르기 + 슬래시 이펙트 포함.
            Texture2D attackTex = Resources.Load<Texture2D>("Sprites/ClubGoblinAttack");
            if (attackTex != null)
            {
                attackFrames = new Sprite[ATTACK_FRAMES];
                for (int c = 0; c < ATTACK_FRAMES; c++)
                {
                    Rect rect = new Rect(c * ATTACK_FRAME_SIZE, 0, ATTACK_FRAME_SIZE, ATTACK_FRAME_SIZE);
                    attackFrames[c] = Sprite.Create(attackTex, rect, new Vector2(0.5f, 0.5f), 100f);
                }
            }
            else
            {
                Debug.LogWarning("[ClubGoblinAnimator] ClubGoblinAttack 시트 없음 — 기본 시트의 Attack 행으로 폴백");
                attackFrames = SliceRow(tex, ROW_ATTACK);
            }

            // === 피격(Hit) 애니메이션 — 별도 시트(Resources/Sprites/ClubGoblinHit) 사용 ===
            //   2048×256, 8프레임 × 256×256px. 흰색 플래시 실루엣 프레임 포함.
            Texture2D hitTex = Resources.Load<Texture2D>("Sprites/ClubGoblinHit");
            if (hitTex != null)
            {
                hitFrames = new Sprite[HIT_FRAMES];
                for (int c = 0; c < HIT_FRAMES; c++)
                {
                    Rect rect = new Rect(c * HIT_FRAME_SIZE, 0, HIT_FRAME_SIZE, HIT_FRAME_SIZE);
                    hitFrames[c] = Sprite.Create(hitTex, rect, new Vector2(0.5f, 0.5f), 100f);
                }
            }
            else
            {
                Debug.LogWarning("[ClubGoblinAnimator] ClubGoblinHit 시트 없음 — 기본 시트의 Hit 행으로 폴백");
                hitFrames = SliceRow(tex, ROW_HIT);
            }

            // === 숨쉬기(Idle) 애니메이션 — 별도 시트(Resources/Sprites/ClubGoblinIdle) 사용 ===
            //   2048×256, 8프레임 × 256×256px (가로 한 줄)
            Texture2D idleTex = Resources.Load<Texture2D>("Sprites/ClubGoblinIdle");
            if (idleTex != null)
            {
                idleFrames = new Sprite[IDLE_FRAMES];
                for (int c = 0; c < IDLE_FRAMES; c++)
                {
                    Rect rect = new Rect(c * IDLE_FRAME_SIZE, 0, IDLE_FRAME_SIZE, IDLE_FRAME_SIZE);
                    idleFrames[c] = Sprite.Create(idleTex, rect, new Vector2(0.5f, 0.5f), 100f);
                }
            }
            else
            {
                Debug.LogWarning("[ClubGoblinAnimator] ClubGoblinIdle 시트 없음 — 기본 시트의 Idle 행으로 폴백");
                idleFrames = SliceRow(tex, ROW_IDLE);
            }

            // === 걷기 애니메이션 — 별도 시트(Resources/Sprites/ClubGoblinWalk) 사용 ===
            //   2048×256, 8프레임 × 256×256px (가로 한 줄)
            Texture2D walkTex = Resources.Load<Texture2D>("Sprites/ClubGoblinWalk");
            if (walkTex != null)
            {
                walkFrames = new Sprite[WALK_FRAMES];
                for (int c = 0; c < WALK_FRAMES; c++)
                {
                    Rect rect = new Rect(c * WALK_FRAME_SIZE, 0, WALK_FRAME_SIZE, WALK_FRAME_SIZE);
                    walkFrames[c] = Sprite.Create(walkTex, rect, new Vector2(0.5f, 0.5f), 100f);
                }
            }
            else
            {
                Debug.LogWarning("[ClubGoblinAnimator] ClubGoblinWalk 시트 없음 — 기본 시트의 Walk 행으로 폴백");
                walkFrames = SliceRow(tex, ROW_WALK);
            }

            // === 죽음 애니메이션 — 별도 시트(Resources/Sprites/ClubGoblinDeath) 사용 ===
            //   2048×256, 8프레임 × 256×256px (가로 한 줄)
            //   1~6: 비틀거림/위협 자세, 7: 무릎 꿇음, 8: 쓰러져 누움
            Texture2D deathTex = Resources.Load<Texture2D>("Sprites/ClubGoblinDeath");
            if (deathTex != null)
            {
                deathFrames = new Sprite[DEATH_FRAMES];
                for (int c = 0; c < DEATH_FRAMES; c++)
                {
                    Rect rect = new Rect(c * DEATH_FRAME_SIZE, 0, DEATH_FRAME_SIZE, DEATH_FRAME_SIZE);
                    deathFrames[c] = Sprite.Create(deathTex, rect, new Vector2(0.5f, 0.5f), 100f);
                }
            }
            else
            {
                Debug.LogWarning("[ClubGoblinAnimator] ClubGoblinDeath 시트 없음 — 기본 시트의 Death 행으로 폴백");
                deathFrames = SliceRow(tex, ROW_DEATH);
            }
        }

        /// <summary>
        /// 한 행에서 LEFT 6프레임을 잘라 Sprite 배열로 반환.
        /// 좌표 변환: 분석은 PIL(top-left origin), Sprite.Create는 Unity(bottom-left origin).
        ///   image_y_top  ↔ texture_y = (texHeight - image_y_top)
        ///   rect의 y는 BOTTOM 기준 → texture_y_bottom = texHeight - image_y_bottom
        /// </summary>
        private static Sprite[] SliceRow(Texture2D tex, int rowFromTop)
        {
            float yImgTop = ROW_Y_TOP[rowFromTop];
            float yImgBot = ROW_Y_BOTTOM[rowFromTop];
            float rectH = yImgBot - yImgTop;
            // 텍스처 y는 하단 기준 → image의 아래쪽(yImgBot)이 텍스처에서 더 작은 y
            float rectY = tex.height - yImgBot;

            Sprite[] arr = new Sprite[FRAMES_PER_ANIM];
            for (int c = 0; c < FRAMES_PER_ANIM; c++)
            {
                float xLeft = LEFT_X_START + c * FRAME_W;
                Rect rect = new Rect(xLeft, rectY, FRAME_W, rectH);
                // pivot 중앙 — 회전/스케일 시 안정적
                arr[c] = Sprite.Create(tex, rect, new Vector2(0.5f, 0.5f), 100f);
            }
            return arr;
        }

        // ============================================================
        // 외부 API
        // ============================================================
        /// <summary>
        /// 애니메이션 상태 변경. 현재 상태와 같으면 무시 (중복 트리거 방지).
        /// onComplete는 1회용 콜백 (논루프 애니메이션 종료 시 호출).
        /// </summary>
        public void SetState(AnimState state, System.Action onComplete = null)
        {
            // 죽음 중에는 다른 상태로 변경 불가
            if (currentState == AnimState.Dying) return;
            // 같은 루프 상태 재호출은 무시 (프레임 리셋 방지)
            if (currentState == state && IsLooping(state) && onComplete == null) return;

            currentState = state;
            frameIdx = 0;
            frameTimer = 0f;
            onCurrentAnimComplete = onComplete;
            UpdateSprite();
        }

        /// <summary>좌우 바라보기 설정 (true=오른쪽, false=왼쪽).</summary>
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
            if (absX < 0.0001f) absX = 1f; // 스폰 시 0 스케일 보호
            s.x = facingRight ? absX : -absX;
            targetRt.localScale = s;

            // ★ HP 바/숫자 카운터 플립 — 부모가 -1로 뒤집힐 때 자식도 -1로 두면
            //   월드 스케일 = (-1) × (-1) = 1 이 되어 텍스트/바가 정방향으로 읽힘
            CounterFlipChild("HPBarBg");
            CounterFlipChild("HPText");
        }

        /// <summary>
        /// targetRt 자식 중 지정 이름과 일치하는 것을 부모와 같은 부호로 localScale.x 설정
        /// → 부모 ×(-1), 자식 ×(-1) = 월드 ×(+1) (미러링 상쇄)
        /// </summary>
        private void CounterFlipChild(string childName)
        {
            if (targetRt == null) return;
            var child = targetRt.Find(childName);
            if (child == null) return;
            var s = child.localScale;
            float absX = Mathf.Abs(s.x);
            if (absX < 0.0001f) absX = 1f;
            // 부모가 facingRight=false → -1. 자식도 -1 으로 두어 월드 +1 (정방향).
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
                    return; // 사망 마지막 프레임 유지, 더 이상 진행 안 함
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
                    // 비루프 애니메이션 종료 처리
                    frameIdx = frames.Length - 1; // 마지막 프레임 유지
                    var cb = onCurrentAnimComplete;
                    onCurrentAnimComplete = null;
                    cb?.Invoke();

                    // Death가 아니면 자동으로 Idle 복귀
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
            // Idle/Walking은 무한 반복, 나머지는 1회 재생
            return state == AnimState.Idle || state == AnimState.Walking;
        }
    }
}
