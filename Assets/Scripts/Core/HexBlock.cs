using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using JewelsHexaPuzzle.Data;
using JewelsHexaPuzzle.Utils;
using System;
using System.Collections;

namespace JewelsHexaPuzzle.Core
{
    [RequireComponent(typeof(Image))]
    public class HexBlock : MonoBehaviour, IPointerClickHandler, IPointerDownHandler, IPointerUpHandler
    {
        [Header("Visual Components")]
        [SerializeField] private Image backgroundImage;
        [SerializeField] private Image gemImage;
        [SerializeField] private Image borderImage;
        // ★ isCracked(몬스터 공격받음) 블록의 상단 3면 회색 표시용 — Image.Filled Vertical/Top/0.5
        private Image grayTopBorderImage;
        [SerializeField] private Image overlayImage;
        [SerializeField] private Image drillIndicator;
        [SerializeField] private Text timerText;
        private Text bombSkillText; // 폭탄 스킬 레벨 텍스트 (v1/v2/v3)

        private HexCoord coord;
        private HexGrid parentGrid;
        private BlockData blockData;
        private EnemyType enemyType = EnemyType.None;

        private bool isSelected;
        private bool isHighlighted;
        private bool isMatched;

        // ��������Ʈ ĳ�� (�ν��Ͻ����� �������� �ʰ� static���� ����)
        private static Sprite hexFillSprite;      // Ǯ ������ (����)
        private static Sprite hexBorderSprite;     // �׵θ� �� (�׵θ���)
        private static Sprite hexGemSprite;        // �׵θ� ���� ������ (�� �����)
        private static Sprite drillVerticalSprite;
        private static Sprite drillSlashSprite;
        private static Sprite drillBackSlashSprite;
        // 외부 드릴 텍스처 캐시 (Resources/Icons/icon_drill_base)
        private static Texture2D _drillBaseTexture;
        private static Color[] _drillBasePixels;
        private static int _drillBaseWidth;
        private static int _drillBaseHeight;
        private static bool _drillBaseLoadAttempted;
                private static Sprite bombIconSprite;
        private static Sprite donutIconSprite;
        private static Sprite xBlockIconSprite;
        private static Sprite droneIconSprite;
        private static Sprite chainOverlaySprite;
        private static Sprite thornOverlaySprite;

        // ★ DamagedBlocks 패키지 PNG 캐시
        //   - clawedSprites[GemType] : 깨진 블록 본체 (Clawed = 몬스터에게 공격받음)
        //   - stolenSprite           : 회색 쉘 블록 본체 (Stolen = 완전히 점령당함)
        private static Sprite[] clawedSprites;
        private static Sprite stolenSprite;
        private static bool _damagedSpritesAttempted;

        // ★ 매 재생 시작 시 PNG 캐시를 모두 클리어 → 새 PNG로 강제 재로드.
        //   Unity "Enter Play Mode → Reload Domain" 옵션이 꺼져있어도 동작.
        //   Resources/Icons/icon_*_base.png 변경 후 재생만으로 즉시 반영된다.
        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetIconCaches_HexBlock()
        {
            drillVerticalSprite = null;
            drillSlashSprite = null;
            drillBackSlashSprite = null;
            _drillBaseTexture = null;
            _drillBasePixels = null;
            _drillBaseLoadAttempted = false;
            bombIconSprite = null;
            donutIconSprite = null;
            xBlockIconSprite = null;
            droneIconSprite = null;
            clawedSprites = null;
            stolenSprite = null;
            _damagedSpritesAttempted = false;
        }

        /// <summary>
        /// DamagedBlocks 패키지 PNG 로드 (Clawed 6색 + Stolen 1장).
        /// Resources/Gems/clawed_{red,orange,yellow,green,blue,purple}.png + stolen.png.
        /// 첫 호출 시 1회만 로드 시도 (실패 시 null 유지 → 폴백 동작).
        /// </summary>
        private static void EnsureDamagedSpritesLoaded()
        {
            if (_damagedSpritesAttempted) return;
            _damagedSpritesAttempted = true;

            // GemType 최대값 + 1 (None=0, Red=1, Blue=2, Green=3, Yellow=4, Purple=5, Orange=6)
            clawedSprites = new Sprite[7];
            clawedSprites[(int)GemType.Red]    = LoadDamagedSprite("Gems/clawed_red");
            clawedSprites[(int)GemType.Blue]   = LoadDamagedSprite("Gems/clawed_blue");
            clawedSprites[(int)GemType.Green]  = LoadDamagedSprite("Gems/clawed_green");
            clawedSprites[(int)GemType.Yellow] = LoadDamagedSprite("Gems/clawed_yellow");
            clawedSprites[(int)GemType.Purple] = LoadDamagedSprite("Gems/clawed_purple");
            clawedSprites[(int)GemType.Orange] = LoadDamagedSprite("Gems/clawed_orange");
            stolenSprite = LoadDamagedSprite("Gems/stolen");
        }

        private static Sprite LoadDamagedSprite(string resourcePath)
        {
            var tex = Resources.Load<Texture2D>(resourcePath);
            if (tex == null) return null;
            return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height),
                                  new Vector2(0.5f, 0.5f), 100f);
        }

        /// <summary>현재 블록 색상에 맞는 Clawed 본체 스프라이트 반환. 없으면 null.</summary>
        private Sprite GetClawedSpriteForCurrentColor()
        {
            if (blockData == null) return null;
            EnsureDamagedSpritesLoaded();
            int idx = (int)blockData.gemType;
            if (clawedSprites == null || idx < 0 || idx >= clawedSprites.Length) return null;
            return clawedSprites[idx];
        }

        // 적군 오버레이 스프라이트
        private static Sprite dividerOverlaySprite;
        private static Sprite gravityWarperOverlaySprite;
        private static Sprite reflectionShieldOverlaySprite;
        private static Sprite timeFreezerOverlaySprite;
        private static Sprite resonanceTwinOverlaySprite;
        private static Sprite shadowSporeOverlaySprite;
        private static Sprite chaosOverlordOverlaySprite;
        private static Sprite crackedOverlaySprite;
        private static Sprite goblinBombOverlaySprite;
        // 폭탄 카운트다운 3프레임 시트 (00:03 / 00:02 / 00:01), null = PNG 미발견(프로시저럴 폴백)
        private static Sprite[] goblinBombCountdownFrames;
        private static bool goblinBombCountdownLoaded;

        // 고블린 폭탄 오버레이 UI
        private Image goblinBombImage;
        private Text goblinBombCountdownText;
        private static Sprite shellOverlaySprite;

        private const float BORDER_WIDTH = 10f;
        private const float INNER_BORDER_WIDTH = 7f;  // ���� �׵θ� 30% ��� (10 * 0.7)

        public event Action<HexBlock> OnBlockClicked;
        public event Action<HexBlock> OnBlockPressed;
        public event Action<HexBlock> OnBlockReleased;

        /// <summary>
        /// 플래시 이펙트용 육각형 스프라이트 반환 (캐싱)
        /// </summary>
        public static Sprite GetHexFlashSprite()
        {
            if (hexFillSprite == null)
            {
                const int TEX_SIZE = 512;
                hexFillSprite = CreateAAHexSprite(TEX_SIZE, 0, false);
            }
            return hexFillSprite;
        }

        // ★ 인디케이터(특수블록 이동범위/아이템 지정·목표) 공통 원형 스프라이트 + 3색 통일.
        //   사각형/육각형 → 원형, 모든 블록에서 잘 보이게: 검정 외곽링(틴트해도 검정 유지=밝은 블록 대비) +
        //   흰 컬러링(틴트=상태색, 어두운 블록 대비) + 반투명 채움.
        private static Sprite _circleIndicatorSprite;
        public static readonly Color IndicatorInitial = new Color(1f, 1f, 1f, 1f);      // 처음 지정(선택) — 흰색
        public static readonly Color IndicatorMovable = new Color(0.25f, 1f, 0.5f, 1f);  // 이동 가능 — 녹색
        public static readonly Color IndicatorTarget  = new Color(1f, 0.62f, 0.1f, 1f);  // 드래그 목표 — 호박색
        public static Sprite GetCircleIndicatorSprite()
        {
            if (_circleIndicatorSprite != null && _circleIndicatorSprite.texture != null) return _circleIndicatorSprite;
            const int SZ = 128;
            var tex = new Texture2D(SZ, SZ, TextureFormat.RGBA32, false);
            var px = new Color[SZ * SZ];
            float c = (SZ - 1) * 0.5f;
            float rOut = SZ * 0.49f, rBlackIn = SZ * 0.45f, rRingIn = SZ * 0.37f;
            for (int y = 0; y < SZ; y++)
                for (int x = 0; x < SZ; x++)
                {
                    float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c));
                    Color col;
                    if (d > rOut) col = new Color(0f, 0f, 0f, 0f);
                    else if (d > rBlackIn) col = new Color(0f, 0f, 0f, 1f - Mathf.Clamp01((d - (rOut - 1.5f)) / 1.5f)); // 검정 외곽(소프트 끝)
                    else if (d > rRingIn) col = new Color(1f, 1f, 1f, 1f);   // 컬러 링(Image.color로 틴트)
                    else col = new Color(1f, 1f, 1f, 0.28f);                 // 반투명 채움
                    px[y * SZ + x] = col;
                }
            tex.SetPixels(px); tex.Apply(); tex.filterMode = FilterMode.Bilinear;
            _circleIndicatorSprite = Sprite.Create(tex, new Rect(0, 0, SZ, SZ), new Vector2(0.5f, 0.5f), 100f);
            return _circleIndicatorSprite;
        }

        public static Sprite GetHexBorderSprite()
        {
            if (hexBorderSprite == null)
            {
                const int TEX_SIZE = 512;
                float scale = TEX_SIZE / 128f;
                hexBorderSprite = CreateAAHexSprite(TEX_SIZE, INNER_BORDER_WIDTH * scale, true);
            }
            return hexBorderSprite;
        }

        public HexCoord Coord => coord;
        public BlockData Data => blockData;
        public bool IsSelected => isSelected;
        public bool CanInteract => blockData != null && blockData.gemType != GemType.None && blockData.CanMove();
        public EnemyType CurrentEnemyType => enemyType;

        // ============================================================
        // 부족 피드백 흔들림 (MP/자원 부족 시 능력 차단 시각 표현)
        // ============================================================
        private Coroutine insufficientShakeCoroutine;
        private Vector2 insufficientShakeOriginalPos;
        private bool insufficientShakeOriginalCaptured;

        /// <summary>
        /// MP 부족 등으로 능력이 차단됐을 때 블록을 좌우로 짧게 흔드는 피드백.
        /// 연속 호출 시 진행 중 흔들림을 멈추고 원래 위치 복원 후 새로 시작 → 위치 드리프트 방지.
        /// </summary>
        public void PlayInsufficientShake(float duration = 0.35f, float magnitude = 6f)
        {
            var rt = GetComponent<RectTransform>();
            if (rt == null) return;

            // 진행 중 흔들림 중단 + 원래 위치 강제 복원
            if (insufficientShakeCoroutine != null)
            {
                StopCoroutine(insufficientShakeCoroutine);
                insufficientShakeCoroutine = null;
                if (insufficientShakeOriginalCaptured)
                    rt.anchoredPosition = insufficientShakeOriginalPos;
            }

            // 원래 위치 1회만 캡처 (흔들림 중 캡처 방지)
            if (!insufficientShakeOriginalCaptured)
            {
                insufficientShakeOriginalPos = rt.anchoredPosition;
                insufficientShakeOriginalCaptured = true;
            }

            insufficientShakeCoroutine = StartCoroutine(InsufficientShakeCoroutine(rt, duration, magnitude));
        }

        /// <summary>
        /// 진행 중인 InsufficientShake 즉시 중단 + 원래 위치 복원.
        /// 회전/이동/스왑 등 외부 시스템이 블록 위치를 변경하기 직전에 호출해
        /// 흔들림이 새 위치를 덮어쓰지 않도록 함.
        /// </summary>
        public void CancelInsufficientShake()
        {
            if (insufficientShakeCoroutine == null) return;

            StopCoroutine(insufficientShakeCoroutine);
            insufficientShakeCoroutine = null;

            // 흔들림 시작 전 캡처한 원위치로 즉시 복원 → 외부 시스템(회전 등)이
            // 그 다음에 정확한 셀 위치로 다시 트윈/세팅하면 됨
            if (insufficientShakeOriginalCaptured)
            {
                var rt = GetComponent<RectTransform>();
                if (rt != null) rt.anchoredPosition = insufficientShakeOriginalPos;
                insufficientShakeOriginalCaptured = false;
            }
        }

        private IEnumerator InsufficientShakeCoroutine(RectTransform rt, float duration, float magnitude)
        {
            float elapsed = 0f;
            int oscillations = 4; // 좌우 4번 진동

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime; // 일시정지 중에도 동작
                float t = elapsed / duration;

                // 좌우 흔들림 (감쇠 사인파)
                float shake = Mathf.Sin(t * Mathf.PI * oscillations * 2f) * magnitude * (1f - t);
                rt.anchoredPosition = insufficientShakeOriginalPos + new Vector2(shake, 0f);

                yield return null;
            }

            // 원래 위치 복원
            rt.anchoredPosition = insufficientShakeOriginalPos;
            insufficientShakeCoroutine = null;
            insufficientShakeOriginalCaptured = false; // 다음 호출에서 새로 캡처
        }

        /// <summary>
        /// 블록에 적군 설정 (색상도둑 등)
        /// </summary>
        public void SetEnemyType(EnemyType type)
        {
            enemyType = type;

            if (blockData != null)
            {
                blockData.enemyType = type;
            }

            // 색상도둑인 경우 시각 처리
            if (type == EnemyType.Chromophage)
            {
                ApplyChromophageVisuals();
            }
            else
            {
                UpdateOverlay();
            }
        }

        /// <summary>
        /// 블록이 적군을 가지고 있는지 확인
        /// </summary>
        public bool HasEnemy()
        {
            return enemyType != EnemyType.None;
        }

        /// <summary>
        /// 특정 적군 타입 여부 확인
        /// </summary>
        public bool HasEnemyOfType(EnemyType type)
        {
            return enemyType == type;
        }

        /// <summary>
        /// 색상도둑 시각 처리: 회색 블록 + 빨간 테두리 강조 + 펄스 애니메이션
        /// </summary>
        private void ApplyChromophageVisuals()
        {
            if (blockData == null) return;

            // 블록 색상을 회색으로 변경
            SetGemColor(new Color(0.5f, 0.5f, 0.5f, 1f));

            // 색상도둑 오버레이 표시 (슬라임 느낌)
            if (overlayImage != null)
            {
                overlayImage.color = new Color(0.5f, 0.55f, 0.5f, 0.35f); // 약간 초록빛의 회색 슬라임
                overlayImage.enabled = true;
            }

            // 빨간 테두리 강조 애니메이션 시작
            if (borderImage != null)
            {
                StartCoroutine(ChromophageBorderHighlight());
            }

            // 펄스 애니메이션 시작 (오버레이)
            StartCoroutine(ChromophagePulseAnimation());
        }

        /// <summary>
        /// 색상도둑 빨간 테두리 강조 (처음 나타날 때)
        /// </summary>
        private IEnumerator ChromophageBorderHighlight()
        {
            if (borderImage == null) yield break;

            // 1단계: 빨간 테두리 강조 (0.4초)
            float highlightDuration = 0.4f;
            float elapsed = 0f;

            while (elapsed < highlightDuration && HasEnemyOfType(EnemyType.Chromophage))
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / highlightDuration);

                // 빨강 강조에서 회색으로 페이드
                Color highlightColor = Color.Lerp(
                    new Color(1f, 0.3f, 0.3f, 0.9f),  // 빨간색
                    new Color(0.4f, 0.4f, 0.4f, 0.7f),  // 회색
                    VisualConstants.EaseOutCubic(t)
                );
                borderImage.color = highlightColor;
                yield return null;
            }

            // 2단계: 회색 테두리로 고정
            if (borderImage != null && HasEnemyOfType(EnemyType.Chromophage))
            {
                borderImage.color = new Color(0.4f, 0.4f, 0.4f, 0.7f);
            }
        }

        /// <summary>
        /// 색상도둑 펄스 애니메이션 (0.5초 주기)
        /// </summary>
        private IEnumerator ChromophagePulseAnimation()
        {
            while (HasEnemyOfType(EnemyType.Chromophage))
            {
                // 밝아짐 (0.25초)
                float elapsed = 0f;
                while (elapsed < 0.25f && HasEnemyOfType(EnemyType.Chromophage))
                {
                    elapsed += Time.deltaTime;
                    float alpha = Mathf.Lerp(0.3f, 0.5f, elapsed / 0.25f);
                    if (overlayImage != null)
                    {
                        overlayImage.color = new Color(0.5f, 0.55f, 0.5f, alpha); // 슬라임 색상과 일치
                    }
                    yield return null;
                }

                // 어두워짐 (0.25초)
                elapsed = 0f;
                while (elapsed < 0.25f && HasEnemyOfType(EnemyType.Chromophage))
                {
                    elapsed += Time.deltaTime;
                    float alpha = Mathf.Lerp(0.5f, 0.3f, elapsed / 0.25f);
                    if (overlayImage != null)
                    {
                        overlayImage.color = new Color(0.5f, 0.55f, 0.5f, alpha); // 슬라임 색상과 일치
                    }
                    yield return null;
                }
            }
        }

        private void Awake()
        {
            if (backgroundImage == null)
                backgroundImage = GetComponent<Image>();

            FindChildComponents();
            EnsureSpritesCreated();
            SetupBorder();
            SetupDrillIndicator();
            ApplyGemMaterials();
        }

        private static bool _materialLogPrinted = false;

        private void ApplyGemMaterials()
        {
            Material gemMat = GemMaterialManager.GetGemMaterial();
            Material borderMat = GemMaterialManager.GetBorderGlowMaterial();
            Material bgMat = GemMaterialManager.GetBackgroundMaterial();

            if (!_materialLogPrinted)
            {
                _materialLogPrinted = true;
                Debug.Log($"[HexBlock] ApplyGemMaterials: gemMat={gemMat?.name ?? "NULL"}, borderMat={borderMat?.name ?? "NULL"}, bgMat={bgMat?.name ?? "NULL"}");
                if (gemMat == null)
                    Debug.LogWarning("[HexBlock] GemMaterial is NULL - UI/HexGem shader not found! Gems will use default flat color.");
            }

            if (gemImage != null && gemMat != null)
                gemImage.material = gemMat;
            if (borderImage != null && borderMat != null)
                borderImage.material = borderMat;
            if (backgroundImage != null && bgMat != null)
                backgroundImage.material = bgMat;
        }

        private void FindChildComponents()
        {
            if (gemImage == null)
            {
                Transform t = transform.Find("GemImage");
                if (t != null) gemImage = t.GetComponent<Image>();
            }
            if (borderImage == null)
            {
                Transform t = transform.Find("BorderImage") ?? transform.Find("Border");
                if (t != null) borderImage = t.GetComponent<Image>();
            }
            if (overlayImage == null)
            {
                Transform t = transform.Find("OverlayImage");
                if (t != null) overlayImage = t.GetComponent<Image>();
            }
            if (drillIndicator == null)
            {
                Transform t = transform.Find("DrillIndicator");
                if (t != null) drillIndicator = t.GetComponent<Image>();
            }
            if (timerText == null)
            {
                Transform t = transform.Find("TimerText");
                if (t != null) timerText = t.GetComponent<Text>();
            }
        }

        /// <summary>
        /// ��������Ʈ�� ������ ���� (Play ��� ����� �ÿ��� ����)
        /// </summary>
        private void EnsureSpritesCreated()
        {
            // 외부 텍스처 초기화 (한 번만)
            GemSpriteProvider.Initialize();

            const int TEX_SIZE = 512;
            float scale = TEX_SIZE / 128f; // 4x

            // 프로시저럴 스프라이트는 항상 생성 (fallback + 배경/테두리용)
            if (hexFillSprite == null)
                hexFillSprite = CreateAAHexSprite(TEX_SIZE, 0, false);
            if (hexBorderSprite == null)
                hexBorderSprite = CreateAAHexSprite(TEX_SIZE, INNER_BORDER_WIDTH * scale, true);
            if (hexGemSprite == null)
                hexGemSprite = CreateAAInnerHexSprite(TEX_SIZE, INNER_BORDER_WIDTH * scale);
            if (drillVerticalSprite == null)
            {
                // ★ PowerUps PNG는 가로 화살표 디자인이라 모든 드릴에 +90° 시계방향 보정.
                //   Vertical: 0+90=90 (세로 ↕)
                //   Slash:   -60+90=30 (사선 /)
                //   BackSlash: 60+90=150 (사선 \\)
                drillVerticalSprite = CreateArrowSprite(256, 90);
                drillSlashSprite = CreateArrowSprite(256, 30);
                drillBackSlashSprite = CreateArrowSprite(256, 150);
            }
            // ★ 특수 블록 외부 PNG 로드 트리거 (startup 진단용) — 콘솔에 로드 성공/실패 로그가 1회씩 출력됨
            if (bombIconSprite == null) bombIconSprite = BombBlockSystem.GetBombIconSprite();
            if (donutIconSprite == null) donutIconSprite = DonutBlockSystem.GetDonutIconSprite();
            if (droneIconSprite == null) droneIconSprite = DroneBlockSystem.GetDroneIconSprite();
        }

        /// <summary>
        /// ������ flat-top ������ �������� signed distance (����=����, ���=�ٱ�)
        /// flat-top: �������� 0��,60��,120��... �� ���� ������ 30��,90��,150��...
        /// </summary>
        private static float HexSignedDistance(Vector2 point, Vector2 center, float radius)
        {
            Vector2 p = point - center;
            float maxDist = float.MinValue;
            for (int i = 0; i < 6; i++)
            {
                // flat-top ����: 30�� + i*60��
                float angle = (30f + i * 60f) * Mathf.Deg2Rad;
                Vector2 normal = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                // flat-top���� �߽�~�� �Ÿ� = radius * cos(30��) = radius * sqrt(3)/2
                float edgeDist = radius * 0.8660254f; // sqrt(3)/2
                float dist = Vector2.Dot(p, normal) - edgeDist;
                if (dist > maxDist) maxDist = dist;
            }
            return maxDist;
        }

        /// <summary>
        /// 고퀄리티 AA 육각형 스프라이트 (배경 fill 또는 베벨 테두리)
        /// - 배경(fill): 안쪽이 어둡고 가장자리가 밝은 움푹 들어간 슬롯 느낌
        /// - 테두리(border): 방향성 조명 베벨 + 소프트 외곽 글로우
        /// </summary>
        private static Sprite CreateAAHexSprite(int size, float borderWidth, bool isBorderOnly)
        {
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            Color[] pixels = new Color[size * size];

            Vector2 center = new Vector2(size / 2f, size / 2f);
            float outerRadius = size / 2f - 2f;
            float innerRadius = outerRadius - borderWidth;
            float aa = 3.0f; // AA 폭 1.5배 확대
            // 조명 방향 (좌상단에서 우하단으로)
            Vector2 lightDir = new Vector2(-0.707f, 0.707f);
            float maxPixelDist = outerRadius * 0.8660254f; // hex apothem

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    Vector2 point = new Vector2(x + 0.5f, y + 0.5f);
                    float outerDist = HexSignedDistance(point, center, outerRadius);

                    if (isBorderOnly)
                    {
                        // === 강화된 베벨 테두리 ===
                        float innerDist = HexSignedDistance(point, center, innerRadius);
                        float outerAlpha = Mathf.Clamp01(1f - outerDist / aa);
                        float innerAlpha = Mathf.Clamp01(innerDist / aa);
                        float ringAlpha = outerAlpha * innerAlpha;

                        // 강화된 방향성 베벨 (명도 범위 확대: 0.70 ~ 0.95)
                        Vector2 dir = (point - center);
                        float dirLen = dir.magnitude;
                        if (dirLen > 0.001f) dir /= dirLen;
                        float lightDot = Vector2.Dot(dir, lightDir);
                        float bevel = 0.825f + lightDot * 0.125f; // 0.70 ~ 0.95 (2배 강화)

                        // 링 중심부 밝기 부스트 (강화)
                        float ringCenter = Mathf.Min(outerAlpha, innerAlpha);
                        bevel += ringCenter * 0.08f;

                        // 소프트 외곽 글로우 (강화: 더 큼)
                        float glowAlpha = Mathf.Clamp01(1f - outerDist / (aa * 2f)) * 0.15f;
                        float finalAlpha = Mathf.Clamp01(ringAlpha + glowAlpha);

                        float b = Mathf.Clamp01(bevel);
                        pixels[y * size + x] = new Color(b, b, b, finalAlpha);
                    }
                    else
                    {
                        // === 부드러운 볼록 쿠션 배경 (강화된 음영) ===
                        float alpha = Mathf.Clamp01(1f - outerDist / aa);

                        Vector2 offset = point - center;
                        float pixelDist = offset.magnitude;
                        float normDist = Mathf.Clamp01(pixelDist / maxPixelDist);

                        // 중심부 어두움, 가장자리 밝음 (움푹 들어간 슬롯 느낌, 명도 범위 확대: 0.65~0.95)
                        float cushion = 0.85f - normDist * 0.15f;  // 중심: 0.85, 가장자리: 0.70

                        // 방향성 조명 (좌상단 약간 밝게)
                        float dirLen = offset.magnitude;
                        if (dirLen > 0.001f)
                        {
                            Vector2 dir = offset / dirLen;
                            cushion += Vector2.Dot(dir, lightDir) * 0.04f;
                        }

                        float b = Mathf.Clamp01(cushion);
                        pixels[y * size + x] = new Color(b, b, b, alpha);
                    }
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        /// <summary>
        /// 고퀄리티 AA 내부 젬 스프라이트 (보석 느낌)
        /// - 볼록 깊이감: 중심 밝고 가장자리 어두움
        /// - 6면 패싯 패턴: 커팅된 보석 표면 시뮬레이션
        /// - 스펙큘러 하이라이트: 좌상단 메인 + 우하단 서브
        /// - 에지 베벨: 가장자리 얇은 밝은 라인
        /// - 방향성 조명: 좌상단→우하단 라이팅
        /// </summary>
        private static Sprite CreateAAInnerHexSprite(int size, float borderWidth)
        {
            // 마카롱 표면 미세 질감 + 셰이더(HexGem/HexSpecialGem)가 깊이/하이라이트/SSS 처리
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            Color[] pixels = new Color[size * size];

            Vector2 center = new Vector2(size / 2f, size / 2f);
            float outerRadius = size / 2f - 2f;
            float innerRadius = outerRadius - borderWidth;
            float aa = 2.0f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    Vector2 point = new Vector2(x + 0.5f, y + 0.5f);
                    float sdf = HexSignedDistance(point, center, innerRadius);
                    float alpha = Mathf.Clamp01(1f - sdf / aa);

                    if (alpha < 0.001f)
                    {
                        pixels[y * size + x] = Color.clear;
                        continue;
                    }

                    // PerlinNoise로 마카롱 표면 미세 질감 (0.97~1.0 범위, 셰이더 이중적용 방지)
                    float noise = Mathf.PerlinNoise(x * 0.08f, y * 0.08f);
                    float texVal = 0.97f + noise * 0.03f; // 최대 3% 변화만
                    pixels[y * size + x] = new Color(texVal, texVal, texVal, alpha);
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        /// <summary>
        /// Mathf.SmoothStep 래핑 (셰이더의 smoothstep과 동일 동작)
        /// </summary>
        private static float SmoothStep(float edge0, float edge1, float x)
        {
            float t = Mathf.Clamp01((x - edge1) / (edge0 - edge1));
            return t * t * (3f - 2f * t);
        }

        private Sprite CreateArrowSprite(int size, float rotation)
        {
            return CreateDrillSprite_Refined(size, rotation);
        }

        /// <summary>
        /// 드릴 아이콘 - 외부 PNG(Resources/Icons/icon_drill_base)를 회전하여 생성
        /// 이미지 로드 실패 시 기존 프로시저럴 폴백
        /// </summary>
        private static Sprite CreateDrillSprite_Refined(int size, float rotation)
        {
            if (EnsureDrillBasePixelsLoaded())
                return CreateRotatedDrillFromImage(size, rotation);
            return CreateDrillSprite_Procedural(size, rotation);
        }

        /// <summary>
        /// 외부 드릴 이미지 텍스처를 Resources에서 로드하고 픽셀 배열을 캐시
        /// </summary>
        private static bool EnsureDrillBasePixelsLoaded()
        {
            if (_drillBasePixels != null) return true;
            if (_drillBaseLoadAttempted) return false;
            _drillBaseLoadAttempted = true;
            _drillBaseTexture = Resources.Load<Texture2D>("Icons/icon_drill_base");
            if (_drillBaseTexture == null)
            {
                Debug.LogWarning("[HexBlock] Resources/Icons/icon_drill_base 텍스처를 찾을 수 없습니다. 프로시저럴 폴백 사용.");
                return false;
            }
            try
            {
                _drillBasePixels = _drillBaseTexture.GetPixels();
                _drillBaseWidth = _drillBaseTexture.width;
                _drillBaseHeight = _drillBaseTexture.height;
                return true;
            }
            catch (UnityException e)
            {
                Debug.LogWarning($"[HexBlock] icon_drill_base GetPixels 실패 (isReadable 확인 필요): {e.Message}");
                _drillBasePixels = null;
                return false;
            }
        }

        /// <summary>
        /// 캐시된 드릴 픽셀을 지정 각도로 회전하여 출력 크기 스프라이트 생성
        /// 회전 방향은 기존 프로시저럴(CreateDrillSprite_Procedural)과 동일하게 매칭:
        /// - rotation > 0: 시계 방향, rotation < 0: 반시계 방향
        /// </summary>
        private static Sprite CreateRotatedDrillFromImage(int outputSize, float degrees)
        {
            int sw = _drillBaseWidth;
            int sh = _drillBaseHeight;
            Color[] srcPixels = _drillBasePixels;

            Texture2D dst = new Texture2D(outputSize, outputSize, TextureFormat.RGBA32, false);
            dst.filterMode = FilterMode.Bilinear;
            Color[] dstPixels = new Color[outputSize * outputSize];

            float rad = degrees * Mathf.Deg2Rad;
            // 출력→원본 역회전 매트릭스 (프로시저럴 코드와 동일 방향: Cos(-rad), Sin(-rad))
            float cos = Mathf.Cos(-rad);
            float sin = Mathf.Sin(-rad);

            float outCenter = outputSize / 2f;
            float srcCenterX = sw / 2f;
            float srcCenterY = sh / 2f;
            float scale = (float)sw / outputSize;

            for (int y = 0; y < outputSize; y++)
            {
                for (int x = 0; x < outputSize; x++)
                {
                    float ox = (x - outCenter) * scale;
                    float oy = (y - outCenter) * scale;
                    float sx = ox * cos - oy * sin + srcCenterX;
                    float sy = ox * sin + oy * cos + srcCenterY;

                    if (sx < 0f || sx >= sw - 1f || sy < 0f || sy >= sh - 1f)
                    {
                        dstPixels[y * outputSize + x] = Color.clear;
                        continue;
                    }

                    int x0 = (int)sx;
                    int y0 = (int)sy;
                    int x1 = x0 + 1;
                    int y1 = y0 + 1;
                    float fx = sx - x0;
                    float fy = sy - y0;

                    Color c00 = srcPixels[y0 * sw + x0];
                    Color c10 = srcPixels[y0 * sw + x1];
                    Color c01 = srcPixels[y1 * sw + x0];
                    Color c11 = srcPixels[y1 * sw + x1];

                    float w00 = (1f - fx) * (1f - fy);
                    float w10 = fx * (1f - fy);
                    float w01 = (1f - fx) * fy;
                    float w11 = fx * fy;

                    dstPixels[y * outputSize + x] = new Color(
                        c00.r * w00 + c10.r * w10 + c01.r * w01 + c11.r * w11,
                        c00.g * w00 + c10.g * w10 + c01.g * w01 + c11.g * w11,
                        c00.b * w00 + c10.b * w10 + c01.b * w01 + c11.b * w11,
                        c00.a * w00 + c10.a * w10 + c01.a * w01 + c11.a * w11
                    );
                }
            }

            dst.SetPixels(dstPixels);
            dst.Apply();
            return Sprite.Create(dst, new Rect(0, 0, outputSize, outputSize), new Vector2(0.5f, 0.5f), 100f);
        }

        /// <summary>
        /// 프로시저럴 드릴 아이콘 - 양방향 화살표 + 메탈릭 그라데이션 + 글로우
        /// 외부 이미지 로드 실패 시 폴백으로 사용
        /// </summary>
        private static Sprite CreateDrillSprite_Procedural(int size, float rotation)
        {
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            Color[] pixels = new Color[size * size];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = Color.clear;

            Vector2 center = new Vector2(size / 2f, size / 2f);
            float rad = rotation * Mathf.Deg2Rad;

            // === 드릴 비트 형태 파라미터 ===
            float bodyLen = size * 0.22f;        // 몸통 길이 (중심~비트 시작)
            float bodyHalfW = size * 0.065f;     // 몸통 반폭
            float bitLen = size * 0.22f;         // 드릴 비트 길이 (나선 구간)
            float bitBaseW = size * 0.09f;       // 비트 시작 반폭
            float collarHalfW = size * 0.085f;   // 중앙 칼라 반폭
            float collarHalfH = size * 0.035f;   // 중앙 칼라 반높이
            float spiralFreq = 8.0f;             // 나선 홈 빈도
            float spiralDepth = 0.35f;           // 나선 홈 깊이 (셰이딩 비율)
            float glowR = size * 0.045f;         // 글로우 반경

            float totalLen = bodyLen + bitLen;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    // 회전 변환
                    Vector2 p = new Vector2(x - center.x, y - center.y);
                    float rx = p.x * Mathf.Cos(-rad) - p.y * Mathf.Sin(-rad);
                    float ry = p.x * Mathf.Sin(-rad) + p.y * Mathf.Cos(-rad);
                    float absRx = Mathf.Abs(rx);
                    float absRy = Mathf.Abs(ry);

                    float alpha = 0f;
                    float shade = 0f;
                    bool isCollar = false;
                    bool isBit = false;

                    // --- 중앙 칼라 (척 고리) ---
                    if (absRy < collarHalfH + 1f && absRx < collarHalfW + 1f)
                    {
                        float edgeX = collarHalfW - absRx;
                        float edgeY = collarHalfH - absRy;
                        float edge = Mathf.Min(edgeX, edgeY);
                        float aa = Mathf.Clamp01(edge * 2f + 0.5f);
                        // 칼라 그라데이션: 위쪽 밝고 아래쪽 어둡게 (입체감)
                        shade = 0.85f - absRy / collarHalfH * 0.2f;
                        // 가장자리 어둡게
                        shade *= 1f - Mathf.Pow(absRx / collarHalfW, 3f) * 0.3f;
                        alpha = aa;
                        isCollar = true;
                    }

                    // --- 몸통 (원통, 칼라~비트 시작) ---
                    if (!isCollar && absRy >= collarHalfH && absRy < bodyLen + 1.5f && absRx < bodyHalfW + 1.5f)
                    {
                        float edgeD = bodyHalfW - absRx;
                        float aa = Mathf.Clamp01(edgeD * 1.5f + 0.5f);
                        // 원통형 셰이딩
                        shade = 1f - Mathf.Pow(absRx / bodyHalfW, 2f) * 0.45f;
                        // 축 방향 약간 어두워짐
                        float axialT = (absRy - collarHalfH) / (bodyLen - collarHalfH);
                        shade *= Mathf.Lerp(0.95f, 0.8f, axialT);
                        // 나선 홈 패턴 (몸통에도 약간)
                        float spiralPhase = Mathf.Sin(absRy * spiralFreq / size * Mathf.PI * 2f + rx / bodyHalfW * 1.5f);
                        shade *= 1f - spiralPhase * spiralDepth * 0.3f;
                        alpha = Mathf.Max(alpha, aa);
                    }

                    // --- 드릴 비트 (나선 홈이 있는 테이퍼 구간) ---
                    if (absRy >= bodyLen && absRy < bodyLen + bitLen + 1.5f)
                    {
                        float bitT = (absRy - bodyLen) / bitLen; // 0=시작 1=끝
                        // 테이퍼: 시작은 넓고 끝은 뾰족하게
                        float bitW = Mathf.Lerp(bitBaseW, 0f, Mathf.Pow(bitT, 1.3f));
                        float edgeD = bitW - absRx;
                        if (edgeD > -1.5f)
                        {
                            float aa = Mathf.Clamp01(edgeD * 1.5f + 0.5f);
                            float tipAA = Mathf.Clamp01((bodyLen + bitLen + 1.5f - absRy) * 1.5f);

                            // 나선 홈 (Spiral flute) — 드릴의 핵심 디테일
                            float normalizedX = rx / Mathf.Max(bitW, 0.01f); // -1 ~ +1
                            float spiralAngle = absRy * spiralFreq / size * Mathf.PI * 2f;
                            float spiralVal = Mathf.Sin(spiralAngle + normalizedX * Mathf.PI);

                            // 기본 원통 셰이딩
                            float baseShade = 1f - Mathf.Pow(absRx / Mathf.Max(bitW, 0.1f), 2f) * 0.4f;
                            // 나선 홈 적용 (어두운 홈 + 밝은 능선)
                            float fluteShade = spiralVal * spiralDepth;
                            shade = baseShade * (1f - fluteShade);
                            // 끝으로 갈수록 약간 밝게 (날카로운 금속 느낌)
                            shade *= Mathf.Lerp(0.85f, 1.1f, bitT);
                            shade = Mathf.Clamp01(shade);

                            alpha = Mathf.Max(alpha, aa * tipAA);
                            isBit = true;
                        }
                    }

                    // --- 색상 적용 ---
                    if (alpha > 0.01f)
                    {
                        float cr, cg, cb;
                        if (isCollar)
                        {
                            // 칼라: 약간 금색 틴트의 메탈릭
                            cr = Mathf.Lerp(0.45f, 0.85f, shade);
                            cg = Mathf.Lerp(0.42f, 0.80f, shade);
                            cb = Mathf.Lerp(0.35f, 0.65f, shade);
                        }
                        else if (isBit)
                        {
                            // 비트: 밝은 스틸 실버 (하이라이트 강조)
                            cr = Mathf.Lerp(0.42f, 1f, shade);
                            cg = Mathf.Lerp(0.44f, 0.98f, shade);
                            cb = Mathf.Lerp(0.50f, 1f, shade);
                        }
                        else
                        {
                            // 몸통: 중간톤 스틸
                            cr = Mathf.Lerp(0.48f, 0.92f, shade);
                            cg = Mathf.Lerp(0.48f, 0.90f, shade);
                            cb = Mathf.Lerp(0.52f, 0.95f, shade);
                        }

                        // 스페큘러 하이라이트 (왼쪽 광원)
                        float specRegion = Mathf.Clamp01(1f - absRx / Mathf.Max(bodyHalfW, bitBaseW));
                        float specT = specRegion * Mathf.Clamp01(1f - absRy / totalLen);
                        float spec = Mathf.Pow(specT, 4f) * 0.4f;
                        if (rx < 0) spec *= 1.6f;

                        pixels[y * size + x] = new Color(
                            Mathf.Clamp01(cr + spec),
                            Mathf.Clamp01(cg + spec),
                            Mathf.Clamp01(cb + spec * 0.7f),
                            alpha
                        );
                    }

                    // --- 글로우 (외곽 발광) ---
                    float shapeW = absRy < bodyLen ? bodyHalfW : Mathf.Lerp(bitBaseW, 0f, Mathf.Clamp01((absRy - bodyLen) / bitLen));
                    bool nearShape = absRx < shapeW + glowR && absRy < totalLen + glowR;
                    if (nearShape && alpha < 0.5f)
                    {
                        float dBody = Mathf.Max(absRx - shapeW, 0f);
                        float dTip = Mathf.Max(absRy - totalLen, 0f);
                        float dMin = Mathf.Sqrt(dBody * dBody + dTip * dTip);
                        if (dMin < glowR)
                        {
                            float ga = Mathf.Pow(1f - dMin / glowR, 2f) * 0.3f;
                            Color gc = new Color(0.8f, 0.88f, 1f, ga);
                            pixels[y * size + x] = Color.Lerp(pixels[y * size + x], gc, ga);
                        }
                    }
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        /// <summary>
        /// 프로시저럴 사슬 오버레이 스프라이트 생성
        /// 육각형 위에 X자 형태로 교차하는 체인 링크 패턴
        /// </summary>
        private static Sprite CreateChainOverlaySprite(int size)
        {
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            Color[] pixels = new Color[size * size];
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = Color.clear;

            Vector2 center = new Vector2(size / 2f, size / 2f);
            float hexRadius = size * 0.42f;

            // 체인 링크 파라미터
            float linkWidth = size * 0.12f;   // 링크 타원 장축
            float linkHeight = size * 0.07f;  // 링크 타원 단축
            float ringThickness = size * 0.02f; // 링크 두께
            float edgeAA = 1.5f; // AA 영역

            // 체인 색상 (메탈릭 실버)
            Color chainLight = new Color(0.82f, 0.82f, 0.85f, 0.92f);
            Color chainDark = new Color(0.45f, 0.45f, 0.50f, 0.92f);
            Color chainMid = new Color(0.62f, 0.62f, 0.68f, 0.92f);

            // 링크 배치: 대각선 두 줄 (\ 방향 + / 방향)이 교차하며 체인 형성
            // 각 줄에 5개 링크, 번갈아 가며 방향 전환
            float[] angles = { 45f, -45f }; // 두 대각선 방향
            float spacing = size * 0.13f;

            for (int lineIdx = 0; lineIdx < 2; lineIdx++)
            {
                float baseAngle = angles[lineIdx];
                float perpAngle = baseAngle + 90f;
                float baseRad = baseAngle * Mathf.Deg2Rad;

                for (int linkIdx = -2; linkIdx <= 2; linkIdx++)
                {
                    // 링크 중심 위치
                    float cx = center.x + Mathf.Cos(baseRad) * linkIdx * spacing;
                    float cy = center.y + Mathf.Sin(baseRad) * linkIdx * spacing;

                    // 육각형 내부인지 체크 (여유 포함)
                    float hexDist = HexSignedDistance(new Vector2(cx, cy), center, hexRadius);
                    if (hexDist > -size * 0.05f) continue;

                    // 링크 방향: 링크마다 번갈아 기울어짐
                    float linkAngle = (linkIdx % 2 == 0) ? baseAngle : perpAngle;
                    float linkRad = linkAngle * Mathf.Deg2Rad;
                    float cosA = Mathf.Cos(-linkRad);
                    float sinA = Mathf.Sin(-linkRad);

                    // 링크 렌더링 범위
                    int minX = Mathf.Max(0, (int)(cx - linkWidth - 4));
                    int maxX = Mathf.Min(size - 1, (int)(cx + linkWidth + 4));
                    int minY = Mathf.Max(0, (int)(cy - linkWidth - 4));
                    int maxY = Mathf.Min(size - 1, (int)(cy + linkWidth + 4));

                    for (int y = minY; y <= maxY; y++)
                    {
                        for (int x = minX; x <= maxX; x++)
                        {
                            // 로컬 좌표 변환 (회전)
                            float lx = (x - cx) * cosA - (y - cy) * sinA;
                            float ly = (x - cx) * sinA + (y - cy) * cosA;

                            // 타원 거리 (링크 외곽)
                            float ex = lx / linkWidth;
                            float ey = ly / linkHeight;
                            float ellipseDist = Mathf.Sqrt(ex * ex + ey * ey);

                            // 링 형태: 외곽 - 내곽 사이
                            float outerDist = Mathf.Abs(ellipseDist - 1f) * linkHeight;

                            if (outerDist < ringThickness + edgeAA)
                            {
                                // AA 알파
                                float alpha = 1f - Mathf.Clamp01((outerDist - ringThickness) / edgeAA);

                                // 방향성 조명 (위에서 빛)
                                float lightFactor = Mathf.Clamp01(0.5f + ly / (linkHeight * 2f));
                                Color linkColor = Color.Lerp(chainDark, chainLight, lightFactor);

                                // 하이라이트 (상단 가장자리)
                                if (ly > 0 && outerDist < ringThickness * 0.6f)
                                    linkColor = Color.Lerp(linkColor, chainLight, 0.4f);

                                int idx = y * size + x;
                                Color existing = pixels[idx];
                                // 교차 영역: 뒤쪽 링크는 어둡게 (깊이감)
                                if (existing.a > 0.1f && lineIdx > 0)
                                {
                                    // 앞쪽 링크(lineIdx=1)가 뒤쪽(lineIdx=0) 위에 오버레이
                                    linkColor.a *= alpha;
                                    pixels[idx] = Color.Lerp(existing, linkColor, linkColor.a);
                                    pixels[idx].a = Mathf.Min(1f, existing.a + linkColor.a * 0.5f);
                                }
                                else
                                {
                                    linkColor.a *= alpha;
                                    if (linkColor.a > existing.a)
                                        pixels[idx] = linkColor;
                                }
                            }
                        }
                    }
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();
            tex.filterMode = FilterMode.Bilinear;
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        /// <summary>
        /// 깨진 블록 금간 오버레이 스프라이트 생성 (프로시저럴)
        /// 육각형 내부에 대각선 금 패턴을 그린다
        /// </summary>
        private static Sprite CreateCrackedOverlaySprite(int size)
        {
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            Color[] pixels = new Color[size * size];
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = Color.clear;

            Vector2 center = new Vector2(size / 2f, size / 2f);
            float hexRadius = size * 0.42f;
            Color crackColor = new Color(1f, 1f, 1f, 0.9f);

            // 금 라인 정의: 시작점, 끝점, 두께
            // 주 금: 좌상→우하 대각선
            DrawCrackLine(pixels, size, center,
                new Vector2(center.x - hexRadius * 0.5f, center.y + hexRadius * 0.6f),
                new Vector2(center.x + hexRadius * 0.3f, center.y - hexRadius * 0.5f),
                2.5f, crackColor, hexRadius);
            // 분기 1: 주 금 중앙에서 오른쪽 위로
            DrawCrackLine(pixels, size, center,
                new Vector2(center.x - hexRadius * 0.1f, center.y + hexRadius * 0.1f),
                new Vector2(center.x + hexRadius * 0.5f, center.y + hexRadius * 0.35f),
                1.8f, crackColor, hexRadius);
            // 분기 2: 주 금 아래에서 왼쪽으로
            DrawCrackLine(pixels, size, center,
                new Vector2(center.x + hexRadius * 0.1f, center.y - hexRadius * 0.15f),
                new Vector2(center.x - hexRadius * 0.45f, center.y - hexRadius * 0.35f),
                1.8f, crackColor, hexRadius);
            // 작은 분기 3
            DrawCrackLine(pixels, size, center,
                new Vector2(center.x - hexRadius * 0.3f, center.y + hexRadius * 0.35f),
                new Vector2(center.x - hexRadius * 0.6f, center.y + hexRadius * 0.15f),
                1.2f, crackColor, hexRadius);

            tex.SetPixels(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        /// <summary>
        /// 금간 라인을 텍스처에 그리기 (육각형 내부만)
        /// </summary>
        private static void DrawCrackLine(Color[] pixels, int size, Vector2 center,
            Vector2 from, Vector2 to, float thickness, Color color, float hexRadius)
        {
            Vector2 dir = to - from;
            float length = dir.magnitude;
            if (length < 0.01f) return;
            dir /= length;
            Vector2 perp = new Vector2(-dir.y, dir.x);

            int padding = (int)(thickness + 3);
            int minX = Mathf.Max(0, (int)(Mathf.Min(from.x, to.x) - padding));
            int maxX = Mathf.Min(size - 1, (int)(Mathf.Max(from.x, to.x) + padding));
            int minY = Mathf.Max(0, (int)(Mathf.Min(from.y, to.y) - padding));
            int maxY = Mathf.Min(size - 1, (int)(Mathf.Max(from.y, to.y) + padding));

            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    Vector2 p = new Vector2(x, y);

                    // 육각형 내부 체크
                    float hexDist = HexSignedDistance(p, center, hexRadius);
                    if (hexDist > -1f) continue;

                    // 라인까지 거리
                    Vector2 toP = p - from;
                    float proj = Vector2.Dot(toP, dir);
                    if (proj < -1f || proj > length + 1f) continue;
                    float perpDist = Mathf.Abs(Vector2.Dot(toP, perp));

                    if (perpDist < thickness)
                    {
                        // 끝 부분 페이드
                        float endFade = 1f;
                        if (proj < 0) endFade = 1f - Mathf.Abs(proj);
                        else if (proj > length) endFade = 1f - (proj - length);
                        endFade = Mathf.Clamp01(endFade);

                        // 가장자리 AA
                        float edgeFade = 1f - Mathf.Clamp01((perpDist - (thickness - 1f)));

                        float alpha = color.a * endFade * edgeFade;
                        int idx = y * size + x;
                        Color existing = pixels[idx];
                        float blendA = alpha + existing.a * (1f - alpha);
                        if (blendA > 0.001f)
                        {
                            pixels[idx] = new Color(
                                (color.r * alpha + existing.r * existing.a * (1f - alpha)) / blendA,
                                (color.g * alpha + existing.g * existing.a * (1f - alpha)) / blendA,
                                (color.b * alpha + existing.b * existing.a * (1f - alpha)) / blendA,
                                blendA
                            );
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 껍데기 블록 오버레이 스프라이트 생성 (중앙이 깨진 잔해 패턴)
        /// 테두리만 남고 가운데가 부서진 느낌
        /// </summary>
        private static Sprite CreateShellOverlaySprite(int size)
        {
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            Color[] pixels = new Color[size * size];
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = Color.clear;

            Vector2 center = new Vector2(size / 2f, size / 2f);
            float hexRadius = size * 0.42f;
            Color shellColor = new Color(1f, 1f, 1f, 0.85f);
            Color darkColor = new Color(0.3f, 0.25f, 0.2f, 0.7f);

            // 중앙에 큰 깨진 구멍 패턴 (불규칙한 다각형)
            float holeRadius = hexRadius * 0.5f;
            // 파편 금 라인들: 구멍 가장자리에서 방사형으로 퍼짐
            for (int angleI = 0; angleI < 8; angleI++)
            {
                float angle = angleI * 45f + UnityEngine.Random.Range(-10f, 10f);
                float rad = angle * Mathf.Deg2Rad;
                // 구멍 가장자리에서 바깥으로
                Vector2 from = center + new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * holeRadius * 0.6f;
                Vector2 to = center + new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * holeRadius * 1.1f;
                DrawCrackLine(pixels, size, center, from, to, 2.0f, shellColor, hexRadius);
            }

            // 중앙 구멍: 어두운 영역으로 표시
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    Vector2 p = new Vector2(x, y);
                    float distFromCenter = (p - center).magnitude;

                    // 육각형 내부만
                    float hexDist = HexSignedDistance(p, center, hexRadius);
                    if (hexDist > -1f) continue;

                    // 중앙 구멍 영역
                    if (distFromCenter < holeRadius * 0.55f)
                    {
                        // 중앙에 가까울수록 더 진한 어둠 (부서진 느낌)
                        float holeFactor = 1f - (distFromCenter / (holeRadius * 0.55f));
                        float alpha = darkColor.a * holeFactor * 0.8f;
                        int idx = y * size + x;
                        Color existing = pixels[idx];
                        float blendA = alpha + existing.a * (1f - alpha);
                        if (blendA > 0.001f)
                        {
                            pixels[idx] = new Color(
                                (darkColor.r * alpha + existing.r * existing.a * (1f - alpha)) / blendA,
                                (darkColor.g * alpha + existing.g * existing.a * (1f - alpha)) / blendA,
                                (darkColor.b * alpha + existing.b * existing.a * (1f - alpha)) / blendA,
                                blendA
                            );
                        }
                    }
                    // 구멍 가장자리: 파편 조각 (불규칙한 링)
                    else if (distFromCenter < holeRadius * 0.75f)
                    {
                        float edgeFactor = 1f - ((distFromCenter - holeRadius * 0.55f) / (holeRadius * 0.2f));
                        // 불규칙한 패턴: 각도에 따라 투명도 변화
                        float angle = Mathf.Atan2(p.y - center.y, p.x - center.x);
                        float noise = Mathf.Sin(angle * 5f) * 0.3f + Mathf.Sin(angle * 11f) * 0.2f;
                        float alpha = shellColor.a * edgeFactor * (0.4f + noise);
                        if (alpha > 0.05f)
                        {
                            int idx = y * size + x;
                            Color existing = pixels[idx];
                            float blendA = alpha + existing.a * (1f - alpha);
                            if (blendA > 0.001f)
                            {
                                pixels[idx] = new Color(
                                    (shellColor.r * alpha + existing.r * existing.a * (1f - alpha)) / blendA,
                                    (shellColor.g * alpha + existing.g * existing.a * (1f - alpha)) / blendA,
                                    (shellColor.b * alpha + existing.b * existing.a * (1f - alpha)) / blendA,
                                    blendA
                                );
                            }
                        }
                    }
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        private void SetupBorder()
        {
            // ��� �̹��� - ���� ������ (raycast ������)
            if (backgroundImage != null)
            {
                backgroundImage.sprite = GemSpriteProvider.GetBackgroundSprite() ?? hexFillSprite;
                backgroundImage.color = new Color(0.96f, 0.93f, 0.90f, 0.28f);
                backgroundImage.type = Image.Type.Simple;
            }

            // �� �̹��� - �׵θ� ���ʸ� ä��� ������ (�������� ����)
            if (gemImage == null)
            {
                GameObject gemObj = new GameObject("GemImage");
                gemObj.transform.SetParent(transform, false);

                gemImage = gemObj.AddComponent<Image>();

                // Ǯ������ ��Ŀ - ��������Ʈ ��ü�� inner hex�̹Ƿ� ��� ���ʿ�
                RectTransform rt = gemObj.GetComponent<RectTransform>();
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
            }

            gemImage.sprite = hexGemSprite;  // 기본 프로시저럴 (UpdateVisuals에서 외부 텍스처로 교체)
            gemImage.raycastTarget = false;
            gemImage.type = Image.Type.Simple;

            // �׵θ� �̹��� - �� �̹��� ���� ��ġ�Ͽ� �������� �κ��� ����
            if (borderImage == null)
            {
                GameObject borderObj = new GameObject("Border");
                borderObj.transform.SetParent(transform, false);
                borderObj.transform.SetAsLastSibling();  // �� ���� �׸� (gem�� ����)

                borderImage = borderObj.AddComponent<Image>();

                RectTransform rt = borderObj.GetComponent<RectTransform>();
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
            }
            else
            {
                // �̹� �����ϸ� �� ���� �̵�
                borderImage.transform.SetAsLastSibling();
            }

            borderImage.sprite = GemSpriteProvider.GetBorderSprite() ?? hexBorderSprite;
            borderImage.color = GetDefaultBorderColor();  // 강화된 테두리 색상 및 불투명도
            borderImage.raycastTarget = false;
            borderImage.type = Image.Type.Simple;

            // ★ 상단 3면 회색 오버레이 (isCracked 블록 전용)
            //   borderImage와 동일한 sprite + RectTransform, type=Filled Vertical Top 0.5로
            //   상단 절반(=flat-top 헥사의 위 3면)만 회색으로 덮어 표시.
            //   isCracked가 아닐 때는 enabled=false로 숨김.
            if (grayTopBorderImage == null)
            {
                GameObject grayTopObj = new GameObject("BorderTopGray");
                grayTopObj.transform.SetParent(transform, false);
                grayTopObj.transform.SetAsLastSibling();  // borderImage 위에 그리기

                grayTopBorderImage = grayTopObj.AddComponent<Image>();
                RectTransform gtRt = grayTopObj.GetComponent<RectTransform>();
                gtRt.anchorMin = Vector2.zero;
                gtRt.anchorMax = Vector2.one;
                gtRt.offsetMin = Vector2.zero;
                gtRt.offsetMax = Vector2.zero;
            }
            else
            {
                grayTopBorderImage.transform.SetAsLastSibling();
            }
            grayTopBorderImage.sprite = borderImage.sprite;
            grayTopBorderImage.color = Color.gray;
            grayTopBorderImage.raycastTarget = false;
            grayTopBorderImage.type = Image.Type.Filled;
            grayTopBorderImage.fillMethod = Image.FillMethod.Vertical;
            grayTopBorderImage.fillOrigin = (int)Image.OriginVertical.Top;
            grayTopBorderImage.fillAmount = 0.5f;
            grayTopBorderImage.enabled = false;

            // ★ 초기 숨김: SetBlockData → UpdateVisuals 전까지 보이지 않도록
            gemImage.color = Color.clear;
            gemImage.enabled = false;
            borderImage.enabled = false;
            if (backgroundImage != null)
                backgroundImage.color = Color.clear;
        }

        private void SetupDrillIndicator()
        {
            if (drillIndicator == null)
            {
                GameObject drillObj = new GameObject("DrillIndicator");
                drillObj.transform.SetParent(transform, false);
                drillObj.transform.SetAsLastSibling();  // �� ���� ǥ��

                drillIndicator = drillObj.AddComponent<Image>();

                RectTransform rt = drillObj.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0.5f, 0.5f);
                rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(70f, 70f); // 초기값, Initialize()에서 hexSize 기반 재계산
                rt.anchoredPosition = Vector2.zero;
            }
            drillIndicator.raycastTarget = false;
            drillIndicator.enabled = false;
        }

        public void Initialize(HexCoord coord, HexGrid grid)
        {
            this.coord = coord;
            this.parentGrid = grid;
            this.blockData = new BlockData();

            // 특수 블록 아이콘 크기: 블록 높이(√3 × hexSize)의 105%
            //   PowerUps 패키지 PNG는 콘텐츠가 가운데에 약 70% 비율로 그려져 있어
            //   기존 0.8 계수로는 폭탄/드론이 작아 보였다 → 1.05로 키워 헥사 내부에 가득.
            if (drillIndicator != null && grid != null)
            {
                float iconSize = grid.HexSize * Mathf.Sqrt(3f) * 1.05f;
                drillIndicator.rectTransform.sizeDelta = new Vector2(iconSize, iconSize);
            }

            // 스프라이트 확보 (Initialize는 Awake 이후 호출되므로)
            EnsureSpritesCreated();

            if (backgroundImage != null)
            {
                backgroundImage.raycastTarget = true;
                if (backgroundImage.sprite == null)
                    backgroundImage.sprite = hexFillSprite;
            }

            if (borderImage != null && borderImage.sprite == null)
                borderImage.sprite = hexBorderSprite;

            if (gemImage != null && gemImage.sprite == null)
                gemImage.sprite = hexGemSprite;

            SetupVisuals();
        }

        private void SetupVisuals()
        {
            if (backgroundImage != null) backgroundImage.color = new Color(0.96f, 0.93f, 0.90f, 0.28f);
            // 초기 상태: 젬과 테두리를 숨김 (SetBlockData 전 흰색 블록 깜빡임 방지)
            if (gemImage != null) { gemImage.color = Color.clear; gemImage.enabled = false; }
            if (borderImage != null) { borderImage.enabled = false; }
            if (overlayImage != null) overlayImage.enabled = false;
            if (timerText != null) timerText.enabled = false;
            if (drillIndicator != null) drillIndicator.enabled = false;
            if (bombSkillText != null) bombSkillText.enabled = false;
        }

public void SetBlockData(BlockData data)
        {
            // 기존 점멸 정지 (데이터가 바뀌므로)
            if (blinkCoroutine != null)
            {
                StopCoroutine(blinkCoroutine);
                blinkCoroutine = null;
            }
            isPendingActivation = false;

            blockData = data != null ? data.Clone() : new BlockData();

            // 최종 안전장치 1: 적군이 아닌데 회색인 경우만 변환
            // (적군 블록은 Gray가 정상이므로 변환하면 안 됨)
            if (blockData.gemType == GemType.Gray && blockData.enemyType == EnemyType.None)
            {
                Debug.LogError($"[HexBlock] 🚨 적군 아닌 회색 블록이 {Coord}에 설정됨! GemTypeHelper.GetRandom()으로 변환");
                blockData.gemType = GemTypeHelper.GetRandom();
            }

            // 최종 안전장치 2: ActiveGemTypeCount 범위 밖의 비활성 기본 색상 차단
            // Orange(6) 등 비활성 색은 외부 Gems 텍스처가 없으면 grayscale fallback으로 회색처럼 렌더링됨.
            // 일반 블록에만 적용 — 상위 티어 보석(Ruby/Emerald/Sapphire/Amber/Amethyst)은 허용.
            int activeMax = GemTypeHelper.ActiveGemTypeCount;
            int gemInt = (int)blockData.gemType;
            if (gemInt > activeMax && gemInt <= (int)GemType.Orange
                && blockData.enemyType == EnemyType.None
                && blockData.specialType == SpecialBlockType.None
                && blockData.tier == BlockTier.Normal)
            {
                Debug.LogWarning($"[HexBlock] ⚠ 비활성 색상({blockData.gemType}, Active={activeMax}) 감지 @ {Coord} → 활성 색상으로 교정");
                blockData.gemType = GemTypeHelper.GetRandom();
            }

            isMatched = false;
            UpdateVisuals();

            // 새 데이터에 pendingActivation이 있으면 점멸 재시작
            if (blockData.pendingActivation)
            {
                isPendingActivation = true;
                StartWarningBlink(10f);
            }
        }

        /// <summary>
        /// 비주얼 업데이트 없이 데이터만 교체 (가상 회전 체크용)
        /// Clone하지 않고 참조만 교체하므로 반드시 원복해야 함
        /// </summary>
        public void SetBlockDataSilent(BlockData data)
        {
            blockData = data;
        }

        /// <summary>
        /// ������ Ŭ���� - �� ������� ����� �ð������� ������ ����
        /// </summary>
        public void ClearData()
        {
            blockData = new BlockData();
            blockData.gemType = GemType.None;
            isMatched = false;
            isHighlighted = false;

            // ��� �ð������� ���� (�ܻ� ����)
            HideVisuals();
        }

        /// <summary>
        /// ��� �ð��� ��Ҹ� ��� ���� (�ܻ� ������)
        /// </summary>
        public void HideVisuals()
        {
            if (gemImage != null)
            {
                gemImage.enabled = false;
                gemImage.color = Color.clear;
            }

            if (borderImage != null)
            {
                borderImage.enabled = false;
            }

            if (backgroundImage != null)
            {
                backgroundImage.color = new Color(0, 0, 0, 0);
            }

            if (overlayImage != null) overlayImage.enabled = false;
            if (timerText != null) timerText.enabled = false;
            if (drillIndicator != null) drillIndicator.enabled = false;
            if (bombSkillText != null) bombSkillText.enabled = false;
            HideGoblinBombOverlay();
        }

        /// <summary>
        /// 기본(비매칭/비강조) 상태의 테두리 색상.
        /// 사용자 요청: "기본 블록의 특징이 되는 색상" — 블록의 GemType 색을
        /// 외곽선에 강하게 표시해 색상 식별성을 높인다.
        /// blockData가 없거나 GemType.None이면 기존 톤(베이지)로 폴백.
        /// </summary>
        private Color GetDefaultBorderColor()
        {
            if (blockData == null || blockData.gemType == GemType.None)
                return new Color(0.94f, 0.91f, 0.88f, 0.45f);

            // ★ 손상 상태: 점령당함(isShell) → 전체 테두리 검정.
            //   공격받음(isCracked)는 grayTopBorderImage로 상단 3면만 회색 처리 →
            //   borderImage 본체는 원래 색상 유지 (이 헬퍼에서 회색으로 덮지 않음).
            if (blockData.isShell) return Color.black;

            Color c = GemColors.GetColor(blockData.gemType);
            // 풀컬러 + 강한 알파(0.95)로 외곽선 색상을 또렷하게 표현.
            // 너무 어두운 색(블루/퍼플)은 살짝 밝혀 어두운 배경에서 잘 보이게 한다.
            float luma = c.r * 0.299f + c.g * 0.587f + c.b * 0.114f;
            if (luma < 0.45f)
            {
                float lift = (0.45f - luma) * 0.8f;
                c.r = Mathf.Clamp01(c.r + lift);
                c.g = Mathf.Clamp01(c.g + lift);
                c.b = Mathf.Clamp01(c.b + lift);
            }

            // ★ 미션 타겟 색 강조 (플레이테스트 개선 #2) — 활성 수집 미션의 타겟 색이면
            //   외곽선을 흰색 쪽으로 45% 끌어올려 은은하게 빛나는 느낌 (정적 — 프레임 비용 0).
            //   "무엇을 노릴지" 시각 동기: 선택지가 40~60개라 매칭 자체는 쉽고,
            //   어느 색을 매칭할지가 실제 게임이기 때문.
            if (MissionTargetColors.Contains(blockData.gemType))
            {
                c = Color.Lerp(c, Color.white, 0.45f);
                return new Color(c.r, c.g, c.b, 1f);
            }

            return new Color(c.r, c.g, c.b, 0.95f);
        }

        /// <summary>
        /// 외곽선 색만 재적용 (미션 타겟 변경 시 일괄 갱신용 — StageManager에서 호출).
        /// </summary>
        public void RefreshBorderColor()
        {
            if (borderImage != null && !isMatched)
                borderImage.color = GetDefaultBorderColor();
        }

public void UpdateVisuals()
        {
            EnsureSpritesCreated();

            if (blockData == null || blockData.gemType == GemType.None)
            {
                SetEmpty();
                return;
            }

            // ── 껍데기(쉘) 블록: "완전히 점령당함" — MonsterStolen.png 사용 ──
            if (blockData.isShell)
            {
                EnsureDamagedSpritesLoaded();
                Color shellGray = new Color(0.52f, 0.50f, 0.48f, 1f);

                if (backgroundImage != null)
                    backgroundImage.color = shellGray;

                if (gemImage != null)
                {
                    if (stolenSprite != null)
                    {
                        // ★ PNG 사용: MonsterStolen 디자인 (점령당한 표시) 그대로 표시.
                        //   PNG 자체에 색상/디자인 포함 → 흰색 tint로 원본 색 유지.
                        gemImage.sprite = stolenSprite;
                        gemImage.color = Color.white;
                    }
                    else
                    {
                        // 폴백: 기존 회색 헥사 (PNG 미발견 시)
                        gemImage.sprite = hexFillSprite;
                        gemImage.color = shellGray;
                    }
                    gemImage.enabled = true;

                    // ★ 점령(쉘) 시 특수 블록 반짝임(UI/HexSpecialGem shimmer) 머티리얼 제거 → 일반 젬 머티리얼로 복귀.
                    //   특수 블록이 크랙 단계 없이 직접 쉘로 변환되는 경로(ConvertToShellBlock 직접 호출 등)에서는
                    //   specialMat이 남아 점령된 자리가 계속 반짝이던 버그가 있었다. 일반 쉘과 동일 외형으로 통일.
                    Material shellGemMat = GemMaterialManager.GetGemMaterial();
                    if (shellGemMat != null) gemImage.material = shellGemMat;
                }

                // 테두리: 점령당한 블록 = 검정색 고정
                if (borderImage != null)
                {
                    borderImage.enabled = true;
                    borderImage.color = Color.black;
                }
                // 점령당함은 전체 검정 → 상단 회색 오버레이 끔
                if (grayTopBorderImage != null) grayTopBorderImage.enabled = false;

                // 특수 블록 아이콘 숨김
                if (drillIndicator != null) drillIndicator.enabled = false;

                // 심한 크랙 오버레이
                UpdateOverlay();
                // ★ 쉘 블록이라도 폭탄 고블린 시한폭탄(hasGoblinBomb)은 유지하여 표시
                //   카운트다운 로직과 폭발은 그대로 동작, 시각만 회색 쉘 위에 오버레이됨
                UpdateGoblinBombOverlay();
                return;
            }

            if (backgroundImage != null)
            {
                backgroundImage.color = new Color(0.96f, 0.93f, 0.90f, 0.28f);
            }

            Color gemColor = GemColors.GetColor(blockData.gemType);
            SetGemColor(gemColor);

            // ★ 깨진 블록(Clawed = 몬스터에게 공격받음): 본체 sprite를 색상별 _Clawed PNG로 교체.
            //   PNG에 발톱 자국 + 손상 효과가 색상별로 통합되어 있어 별도 오버레이 불필요
            //   (UpdateOverlay에서 isCracked 분기의 crackedOverlaySprite는 PNG 사용 시 스킵).
            if (blockData.isCracked && gemImage != null)
            {
                Sprite clawedSp = GetClawedSpriteForCurrentColor();
                if (clawedSp != null)
                {
                    gemImage.sprite = clawedSp;
                    gemImage.color = Color.white;  // PNG 본체에 색상 포함
                }
            }

            // 테두리 색상 결정 (강화된 값으로 업데이트)
            if (borderImage != null)
            {
                borderImage.enabled = true;
                if (blockData.pendingActivation)
                    borderImage.color = new Color(0.95f, 0.72f, 0.68f, 0.8f);
                else
                    borderImage.color = isMatched ? Color.white : GetDefaultBorderColor();  // 강화된 기본 테두리
            }

            // ★ 상단 3면 회색(isCracked 전용) — 본체 borderImage는 원래 색 유지, 위 절반만 회색 오버레이.
            if (grayTopBorderImage != null)
            {
                bool showTopGray = blockData.isCracked && !blockData.isShell;
                grayTopBorderImage.enabled = showTopGray;
                if (showTopGray)
                {
                    grayTopBorderImage.sprite = borderImage != null ? borderImage.sprite : null;
                    grayTopBorderImage.color = Color.gray;
                    grayTopBorderImage.fillAmount = 0.5f;
                    grayTopBorderImage.transform.SetAsLastSibling();
                }
            }

            // 특수 블록 아이콘/추가 시각 처리 (통합)
            UpdateSpecialIndicator();
            UpdateOverlay();

            // 고블린 폭탄 오버레이
            UpdateGoblinBombOverlay();

            // 흙더미 오버레이 (1/3 또는 2/3 블록을 덮음)
            UpdateDirtMoundOverlay();
        }

/// <summary>
        /// 특수 블록 통합 시각 처리
        /// 새 특수 블록 추가 시 이 메서드에 case만 추가하면 됨
        /// - 아이콘: drillIndicator(특수 블록 공용 아이콘 이미지)에 스프라이트 설정
        /// - 젬 색상 오버라이드: SetGemColor 호출
        /// - 추가 UI(타이머 등): 개별 처리
        /// </summary>
private void UpdateSpecialIndicator()
        {
            if (blockData == null)
            {
                if (drillIndicator != null) drillIndicator.enabled = false;
                return;
            }

            switch (blockData.specialType)
            {
                case SpecialBlockType.Drill:
                    ShowSpecialIcon(GetDrillSprite(blockData.drillDirection));
                    break;

                case SpecialBlockType.Bomb:
                    if (bombIconSprite == null)
                        bombIconSprite = BombBlockSystem.GetBombIconSprite();
                    ShowSpecialIcon(bombIconSprite);
                    UpdateBombSkillText();
                    break;

                // 타겟 레이저 (Rainbow) — 블록에 크로스헤어 아이콘 표시
                case SpecialBlockType.Rainbow:
                    if (donutIconSprite == null)
                        donutIconSprite = DonutBlockSystem.GetDonutIconSprite();
                    ShowSpecialIcon(donutIconSprite);
                    // 레이저 타겟 전용: 원형 보석 중앙(상단 약 40% 지점)을 회전축으로 설정
                    // sprite는 세로 길쭉 → preserveAspect로 비율 유지
                    drillIndicator.rectTransform.pivot = new Vector2(0.5f, 0.6f);
                    drillIndicator.preserveAspect = true;
                    break;

                case SpecialBlockType.XBlock:
                    if (xBlockIconSprite == null)
                        xBlockIconSprite = XBlockSystem.GetXBlockIconSprite();
                    ShowSpecialIcon(xBlockIconSprite);
                    break;

                case SpecialBlockType.Drone:
                    if (droneIconSprite == null)
                        droneIconSprite = DroneBlockSystem.GetDroneIconSprite();
                    ShowSpecialIcon(droneIconSprite);
                    break;

                case SpecialBlockType.TimeBomb:
                    if (drillIndicator != null) drillIndicator.enabled = false;
                    ShowTimerText(blockData.timeBombCount);
                    break;

                case SpecialBlockType.MoveBlock:
                    if (drillIndicator != null) drillIndicator.enabled = false;
                    SetGemColor(new Color(0.82f, 0.78f, 0.75f, 0.8f));
                    break;

                case SpecialBlockType.FixedBlock:
                    if (drillIndicator != null) drillIndicator.enabled = false;
                    SetGemColor(new Color(0.7f, 0.68f, 0.72f, 1f));
                    break;

                default:
                    if (drillIndicator != null) drillIndicator.enabled = false;
                    break;
            }

            // Apply special or normal gem material
            if (gemImage != null)
            {
                if (blockData.specialType != SpecialBlockType.None &&
                    blockData.specialType != SpecialBlockType.TimeBomb &&
                    blockData.specialType != SpecialBlockType.MoveBlock &&
                    blockData.specialType != SpecialBlockType.FixedBlock)
                {
                    Material specialMat = GemMaterialManager.GetSpecialGemMaterial(blockData.specialType);
                    if (specialMat != null)
                        gemImage.material = specialMat;
                }
                else
                {
                    Material gemMat = GemMaterialManager.GetGemMaterial();
                    if (gemMat != null)
                        gemImage.material = gemMat;
                }
            }
        }

/// <summary>
        /// 특수 블록 아이콘 표시 (공용)
        /// drillIndicator를 특수 블록 공용 아이콘 이미지로 사용
        /// 스킬 해금 레벨에 따라 아이콘 색조(tint) 적용
        /// </summary>
        private void ShowSpecialIcon(Sprite iconSprite)
        {
            if (drillIndicator == null) return;
            // ★ 흰색 사각형 버그 방지:
            //   sprite가 null인 Image는 enabled 상태에서 흰 쿼드(흰 박스)를 그린다.
            //   외부 PNG 로드 실패 등으로 아이콘이 null이면 박스를 그리지 말고 숨긴다.
            if (iconSprite == null)
            {
                drillIndicator.sprite = null;
                drillIndicator.enabled = false;
                return;
            }
            drillIndicator.enabled = true;
            drillIndicator.sprite = iconSprite;
            drillIndicator.color = GetSpecialIconTint();
            // 기본 pivot 리셋 (정사각형 sprite 중앙). Rainbow 케이스는 호출 후 재설정.
            drillIndicator.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            drillIndicator.preserveAspect = false;
            // ★ 특수 블록 아이콘을 테두리(borderImage/grayTopBorderImage)보다 위로 올린다.
            //   SetupBorder/UpdateOverlay가 테두리를 SetAsLastSibling으로 올리므로,
            //   아이콘 표시 시점에 drillIndicator를 최상단으로 재배치해야 가려지지 않음.
            drillIndicator.transform.SetAsLastSibling();
        }

        /// <summary>
        /// 특수 블록 타입 + 스킬 해금 레벨에 따른 아이콘 색조 반환.
        /// BlockSkillColors 중앙 색상 상수 참조 — 스킬트리 노드와 동일 색상 보장.
        /// </summary>
        private Color GetSpecialIconTint()
        {
            if (blockData == null) return Color.white;

            var stm = JewelsHexaPuzzle.Managers.SkillTreeManager.Instance;

            switch (blockData.specialType)
            {
                case SpecialBlockType.Drill:
                {
                    int level = (stm != null) ? stm.GetDrillDamageBonus() : 0;
                    return JewelsHexaPuzzle.Utils.BlockSkillColors.GetByLevel(
                        JewelsHexaPuzzle.Utils.BlockSkillColors.Drill, level);
                }
                case SpecialBlockType.Bomb:
                {
                    // 외부 폭탄 PNG(빨간 폭탄 + 흰 테두리)는 자체 색상을 그대로 살린다.
                    // BombBase(0.15 검정) 곱셈 시 어둡게 표시되므로 인게임 아이콘은 흰색 유지.
                    // 스킬 레벨 구분은 UpdateBombSkillText()의 v1/v2/v3 텍스트로 표시됨.
                    return Color.white;
                }
                case SpecialBlockType.Drone:
                {
                    int level = (stm != null) ? stm.GetDroneTargetDamageBonus() : 0;
                    return JewelsHexaPuzzle.Utils.BlockSkillColors.GetByLevel(
                        JewelsHexaPuzzle.Utils.BlockSkillColors.Drone, level);
                }
                default:
                    return Color.white;
            }
        }

/// <summary>
        /// 드릴 방향에 맞는 스프라이트 반환
        /// </summary>
        private Sprite GetDrillSprite(DrillDirection direction)
        {
            switch (direction)
            {
                case DrillDirection.Vertical:  return drillVerticalSprite;
                case DrillDirection.Slash:     return drillSlashSprite;
                case DrillDirection.BackSlash: return drillBackSlashSprite;
                default: return drillVerticalSprite;
            }
        }

        /// <summary>
        /// 드릴 방향별 아이콘 스프라이트 반환 (미션 UI용 공개 접근자)
        /// </summary>
        public static Sprite GetDrillIconSprite(DrillDirection direction)
        {
            // 스프라이트가 아직 생성되지 않았으면 생성
            //   모든 드릴에 +90° 시계방향 보정 (PowerUps PNG가 가로 화살표 디자인)
            if (drillVerticalSprite == null)
            {
                drillVerticalSprite = CreateArrowSprite_Static(256, 90);
                drillSlashSprite = CreateArrowSprite_Static(256, 30);
                drillBackSlashSprite = CreateArrowSprite_Static(256, 150);
            }
            switch (direction)
            {
                case DrillDirection.Vertical:  return drillVerticalSprite;
                case DrillDirection.Slash:     return drillSlashSprite;
                case DrillDirection.BackSlash: return drillBackSlashSprite;
                default: return drillVerticalSprite;
            }
        }

        /// <summary>
        /// 화살표 스프라이트 생성 (static 버전, 미션 아이콘 초기화용)
        /// </summary>
        private static Sprite CreateArrowSprite_Static(int size, float rotation)
        {
            return CreateDrillSprite_Refined(size, rotation);
        }

        private static Sprite _drillAnySprite;

        /// <summary>
        /// 3방향 드릴 아이콘 스프라이트 (↕ / \ 겹침) — "아무 방향" 드릴 미션용
        /// 세 방향 드릴을 반투명으로 겹쳐 한 아이콘에 표시
        /// </summary>
        public static Sprite GetDrillAnyIconSprite()
        {
            if (_drillAnySprite != null) return _drillAnySprite;

            const int size = 256;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;

            // 3방향 드릴 개별 텍스처 생성
            Sprite vSprite = CreateDrillSprite_Refined(size, 0);
            Sprite sSprite = CreateDrillSprite_Refined(size, -60);
            Sprite bSprite = CreateDrillSprite_Refined(size, 60);

            Color[] vPx = vSprite.texture.GetPixels();
            Color[] sPx = sSprite.texture.GetPixels();
            Color[] bPx = bSprite.texture.GetPixels();
            Color[] result = new Color[size * size];

            for (int i = 0; i < result.Length; i++)
            {
                // 각 방향의 알파를 합산 (겹치는 부분은 밝아짐)
                float aV = vPx[i].a;
                float aS = sPx[i].a;
                float aB = bPx[i].a;
                float totalA = Mathf.Clamp01(aV + aS + aB);

                if (totalA < 0.01f)
                {
                    result[i] = Color.clear;
                    continue;
                }

                // 가중 평균 색상 (알파 기반 블렌딩)
                Color blended = (vPx[i] * aV + sPx[i] * aS + bPx[i] * aB);
                if (totalA > 0.01f)
                {
                    blended.r /= totalA;
                    blended.g /= totalA;
                    blended.b /= totalA;
                }
                blended.a = totalA;
                result[i] = blended;
            }

            tex.SetPixels(result);
            tex.Apply();
            _drillAnySprite = Sprite.Create(tex, new Rect(0, 0, size, size), Vector2.one * 0.5f, 100f);
            return _drillAnySprite;
        }

/// <summary>
        /// 외부에서 직접 특수 블록 아이콘 표시 (드릴 생성 시 등)
        /// SetBlockData → UpdateVisuals에서 자동 처리되므로 보통은 호출 불필요
        /// </summary>
        public void ShowDrillIndicator(DrillDirection direction)
        {
            ShowSpecialIcon(GetDrillSprite(direction));
        }

        
        public void ShowDonutIndicator()
        {
            if (donutIconSprite == null)
                donutIconSprite = DonutBlockSystem.GetDonutIconSprite();
            ShowSpecialIcon(donutIconSprite);
            // 레이저 타겟 전용: 원형 보석 중앙을 회전축으로 (UpdateVisuals 케이스와 동일)
            drillIndicator.rectTransform.pivot = new Vector2(0.5f, 0.6f);
            drillIndicator.preserveAspect = true;
        }
public void ShowXBlockIndicator()
        {
            if (xBlockIconSprite == null)
                xBlockIconSprite = XBlockSystem.GetXBlockIconSprite();
            ShowSpecialIcon(xBlockIconSprite);
        }
public void ShowBombIndicator()
        {
            if (bombIconSprite == null)
                bombIconSprite = BombBlockSystem.GetBombIconSprite();
            ShowSpecialIcon(bombIconSprite);
        }
public void ShowDroneIndicator()
        {
            if (droneIconSprite == null)
                droneIconSprite = DroneBlockSystem.GetDroneIconSprite();
            ShowSpecialIcon(droneIconSprite);
        }

        private void SetGemColor(Color color)
        {
            if (gemImage != null)
            {
                // 외부 텍스처가 있으면 사용, 없으면 프로시저럴
                GemType currentGem = blockData != null ? blockData.gemType : GemType.None;
                Sprite externalSprite = GemSpriteProvider.GetGemSprite(currentGem);

                if (externalSprite != null)
                {
                    gemImage.sprite = externalSprite;
                    // 개별 컬러 텍스처면 흰색 (텍스처 자체에 색상 포함)
                    // 그레이스케일 베이스면 틴팅 적용
                    gemImage.color = GemSpriteProvider.NeedsTinting(currentGem) ? color : Color.white;
                }
                else
                {
                    if (gemImage.sprite == null)
                        gemImage.sprite = hexGemSprite;
                    gemImage.color = color;
                }
                gemImage.enabled = true;
            }
            else if (backgroundImage != null)
            {
                backgroundImage.color = color;
            }

            if (borderImage != null)
            {
                borderImage.enabled = true;
            }
        }

        public void SetEmpty()
        {
            // Revert to normal gem material
            if (gemImage != null)
            {
                Material gemMat = GemMaterialManager.GetGemMaterial();
                if (gemMat != null)
                    gemImage.material = gemMat;
            }

            // gemImage�� ������ �����ϰ� (�ܻ� ����)
            if (gemImage != null)
            {
                gemImage.color = Color.clear;  // ���� ����
                gemImage.enabled = false;      // ��Ȱ��ȭ
            }

            if (backgroundImage != null)
            {
                backgroundImage.color = new Color(0.96f, 0.93f, 0.90f, 0.04f);
            }

            if (borderImage != null)
            {
                borderImage.enabled = false;
            }
            // ★ 상단 회색 오버레이도 함께 해제
            if (grayTopBorderImage != null) grayTopBorderImage.enabled = false;

            if (overlayImage != null) overlayImage.enabled = false;
            if (timerText != null) timerText.enabled = false;
            if (drillIndicator != null) drillIndicator.enabled = false;
            if (bombSkillText != null) bombSkillText.enabled = false;
        }

        private void UpdateOverlay()
        {
            if (overlayImage == null || blockData == null) return;

            if (blockData.hasChain)
            {
                if (chainOverlaySprite == null)
                    chainOverlaySprite = CreateChainOverlaySprite(256);
                overlayImage.sprite = chainOverlaySprite;
                overlayImage.color = Color.white;
                overlayImage.enabled = true;
            }
            else if (blockData.hasThorn)
            {
                EnsureEnemyOverlaySprites();
                overlayImage.sprite = thornOverlaySprite;
                overlayImage.color = new Color(0.8f, 0.2f, 0.3f, 0.85f);
                overlayImage.enabled = true;
            }
            else if (blockData.enemyType != JewelsHexaPuzzle.Data.EnemyType.None &&
                     blockData.enemyType != JewelsHexaPuzzle.Data.EnemyType.Chromophage &&
                     blockData.enemyType != JewelsHexaPuzzle.Data.EnemyType.ChainAnchor &&
                     blockData.enemyType != JewelsHexaPuzzle.Data.EnemyType.ThornParasite)
            {
                EnsureEnemyOverlaySprites();
                Sprite enemySprite = GetEnemyOverlaySprite(blockData.enemyType);
                if (enemySprite != null)
                {
                    overlayImage.sprite = enemySprite;
                    overlayImage.color = GetEnemyOverlayColor(blockData.enemyType);
                    overlayImage.enabled = true;
                }
            }
            else if (blockData.vinylLayer > 0)
            {
                overlayImage.sprite = null;
                float alpha = blockData.vinylLayer == 2 ? 0.6f : 0.3f;
                overlayImage.color = new Color(1f, 1f, 1f, alpha);
                overlayImage.enabled = true;
            }
            else if (blockData.isShell)
            {
                // 껍데기 블록(Stolen): MonsterStolen PNG 사용 시 본체에 디자인 통합 → 오버레이 끔.
                EnsureDamagedSpritesLoaded();
                if (stolenSprite != null)
                {
                    overlayImage.sprite = null;
                    overlayImage.enabled = false;
                }
                else
                {
                    // 폴백: 기존 프로시저럴 크랙 오버레이
                    if (crackedOverlaySprite == null)
                        crackedOverlaySprite = CreateCrackedOverlaySprite(256);
                    overlayImage.sprite = crackedOverlaySprite;
                    overlayImage.color = new Color(0.12f, 0.10f, 0.08f, 0.9f);
                    overlayImage.enabled = true;
                }
            }
            else if (blockData.isCracked)
            {
                // 깨진 블록(Clawed): _Clawed PNG가 본체에 통합 → 오버레이 끔.
                Sprite clawedSp = GetClawedSpriteForCurrentColor();
                if (clawedSp != null)
                {
                    overlayImage.sprite = null;
                    overlayImage.enabled = false;
                }
                else
                {
                    // 폴백: 기존 프로시저럴 크랙 오버레이
                    if (crackedOverlaySprite == null)
                        crackedOverlaySprite = CreateCrackedOverlaySprite(256);
                    overlayImage.sprite = crackedOverlaySprite;
                    overlayImage.color = new Color(0.15f, 0.1f, 0.05f, 0.55f);
                    overlayImage.enabled = true;
                }
            }
            else
            {
                overlayImage.sprite = null;
                overlayImage.enabled = false;
            }
        }

        /// <summary>
        /// 적군 오버레이 스프라이트 초기화 (한 번만)
        /// </summary>
        private static void EnsureEnemyOverlaySprites()
        {
            if (dividerOverlaySprite != null) return;

            const int size = 256;
            thornOverlaySprite = CreateEnemySymbolSprite(size, EnemySymbol.Thorn);
            dividerOverlaySprite = CreateEnemySymbolSprite(size, EnemySymbol.Divider);
            gravityWarperOverlaySprite = CreateEnemySymbolSprite(size, EnemySymbol.GravityWarper);
            reflectionShieldOverlaySprite = CreateEnemySymbolSprite(size, EnemySymbol.Shield);
            timeFreezerOverlaySprite = CreateEnemySymbolSprite(size, EnemySymbol.Clock);
            resonanceTwinOverlaySprite = CreateEnemySymbolSprite(size, EnemySymbol.Twin);
            shadowSporeOverlaySprite = CreateEnemySymbolSprite(size, EnemySymbol.Spore);
            chaosOverlordOverlaySprite = CreateEnemySymbolSprite(size, EnemySymbol.Crown);
        }

        private enum EnemySymbol { Thorn, Divider, GravityWarper, Shield, Clock, Twin, Spore, Crown }

        /// <summary>
        /// 프로시저럴 적군 심볼 스프라이트 생성
        /// </summary>
        private static Sprite CreateEnemySymbolSprite(int size, EnemySymbol symbol)
        {
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            Color[] pixels = new Color[size * size];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = Color.clear;

            Vector2 center = new Vector2(size / 2f, size / 2f);
            float radius = size * 0.35f;

            switch (symbol)
            {
                case EnemySymbol.Thorn:
                    // X자 가시 패턴
                    DrawCross(pixels, size, center, radius * 0.7f, size * 0.08f, new Color(1f, 1f, 1f, 0.9f));
                    break;
                case EnemySymbol.Divider:
                    // 세포분열 패턴 (두 원)
                    DrawCircleRing(pixels, size, center + new Vector2(-radius * 0.25f, 0), radius * 0.4f, size * 0.04f, new Color(1f, 1f, 1f, 0.9f));
                    DrawCircleRing(pixels, size, center + new Vector2(radius * 0.25f, 0), radius * 0.4f, size * 0.04f, new Color(1f, 1f, 1f, 0.9f));
                    break;
                case EnemySymbol.GravityWarper:
                    // 소용돌이
                    DrawSpiral(pixels, size, center, radius * 0.6f, new Color(1f, 1f, 1f, 0.85f));
                    break;
                case EnemySymbol.Shield:
                    // 방패
                    DrawShield(pixels, size, center, radius * 0.55f, new Color(1f, 1f, 1f, 0.9f));
                    break;
                case EnemySymbol.Clock:
                    // 시계
                    DrawCircleRing(pixels, size, center, radius * 0.5f, size * 0.04f, new Color(1f, 1f, 1f, 0.9f));
                    DrawLine(pixels, size, center, center + new Vector2(0, radius * 0.35f), size * 0.04f, new Color(1f, 1f, 1f, 0.9f));
                    DrawLine(pixels, size, center, center + new Vector2(radius * 0.25f, 0), size * 0.04f, new Color(1f, 1f, 1f, 0.9f));
                    break;
                case EnemySymbol.Twin:
                    // 쌍둥이 링크
                    DrawCircleFill(pixels, size, center + new Vector2(-radius * 0.3f, 0), radius * 0.22f, new Color(1f, 1f, 1f, 0.9f));
                    DrawCircleFill(pixels, size, center + new Vector2(radius * 0.3f, 0), radius * 0.22f, new Color(1f, 1f, 1f, 0.9f));
                    DrawLine(pixels, size, center + new Vector2(-radius * 0.1f, 0), center + new Vector2(radius * 0.1f, 0), size * 0.05f, new Color(1f, 1f, 1f, 0.8f));
                    break;
                case EnemySymbol.Spore:
                    // 포자 점들
                    DrawCircleFill(pixels, size, center, radius * 0.15f, new Color(1f, 1f, 1f, 0.9f));
                    for (int i = 0; i < 6; i++)
                    {
                        float angle = i * 60f * Mathf.Deg2Rad;
                        Vector2 p = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius * 0.4f;
                        DrawCircleFill(pixels, size, p, radius * 0.1f, new Color(1f, 1f, 1f, 0.7f));
                    }
                    break;
                case EnemySymbol.Crown:
                    // 왕관
                    DrawCrown(pixels, size, center, radius * 0.5f, new Color(1f, 1f, 1f, 0.9f));
                    break;
            }

            tex.SetPixels(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        // 적군 심볼 그리기 헬퍼
        private static void DrawLine(Color[] pixels, int size, Vector2 a, Vector2 b, float width, Color color)
        {
            float halfW = width * 0.5f;
            int minX = Mathf.Max(0, (int)(Mathf.Min(a.x, b.x) - halfW - 1));
            int maxX = Mathf.Min(size - 1, (int)(Mathf.Max(a.x, b.x) + halfW + 1));
            int minY = Mathf.Max(0, (int)(Mathf.Min(a.y, b.y) - halfW - 1));
            int maxY = Mathf.Min(size - 1, (int)(Mathf.Max(a.y, b.y) + halfW + 1));
            Vector2 dir = (b - a);
            float len = dir.magnitude;
            if (len < 0.001f) return;
            dir /= len;
            Vector2 perp = new Vector2(-dir.y, dir.x);
            for (int y = minY; y <= maxY; y++)
                for (int x = minX; x <= maxX; x++)
                {
                    Vector2 p = new Vector2(x + 0.5f, y + 0.5f) - a;
                    float along = Vector2.Dot(p, dir);
                    if (along < -1f || along > len + 1f) continue;
                    float dist = Mathf.Abs(Vector2.Dot(p, perp));
                    float alpha = Mathf.Clamp01(1f - (dist - halfW) / 1.5f);
                    if (alpha > 0f)
                    {
                        Color c = color; c.a *= alpha;
                        int idx = y * size + x;
                        if (c.a > pixels[idx].a) pixels[idx] = c;
                    }
                }
        }

        private static void DrawCross(Color[] pixels, int size, Vector2 center, float armLen, float width, Color color)
        {
            DrawLine(pixels, size, center + new Vector2(-armLen, -armLen), center + new Vector2(armLen, armLen), width, color);
            DrawLine(pixels, size, center + new Vector2(-armLen, armLen), center + new Vector2(armLen, -armLen), width, color);
        }

        private static void DrawCircleRing(Color[] pixels, int size, Vector2 center, float radius, float thickness, Color color)
        {
            float halfT = thickness * 0.5f;
            int minX = Mathf.Max(0, (int)(center.x - radius - halfT - 2));
            int maxX = Mathf.Min(size - 1, (int)(center.x + radius + halfT + 2));
            int minY = Mathf.Max(0, (int)(center.y - radius - halfT - 2));
            int maxY = Mathf.Min(size - 1, (int)(center.y + radius + halfT + 2));
            for (int y = minY; y <= maxY; y++)
                for (int x = minX; x <= maxX; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), center);
                    float ringDist = Mathf.Abs(dist - radius);
                    float alpha = Mathf.Clamp01(1f - (ringDist - halfT) / 1.5f);
                    if (alpha > 0f)
                    {
                        Color c = color; c.a *= alpha;
                        int idx = y * size + x;
                        if (c.a > pixels[idx].a) pixels[idx] = c;
                    }
                }
        }

        private static void DrawCircleFill(Color[] pixels, int size, Vector2 center, float radius, Color color)
        {
            int minX = Mathf.Max(0, (int)(center.x - radius - 2));
            int maxX = Mathf.Min(size - 1, (int)(center.x + radius + 2));
            int minY = Mathf.Max(0, (int)(center.y - radius - 2));
            int maxY = Mathf.Min(size - 1, (int)(center.y + radius + 2));
            for (int y = minY; y <= maxY; y++)
                for (int x = minX; x <= maxX; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), center);
                    float alpha = Mathf.Clamp01(1f - (dist - radius) / 1.5f);
                    if (alpha > 0f)
                    {
                        Color c = color; c.a *= alpha;
                        int idx = y * size + x;
                        if (c.a > pixels[idx].a) pixels[idx] = c;
                    }
                }
        }

        private static void DrawSpiral(Color[] pixels, int size, Vector2 center, float maxRadius, Color color)
        {
            float thickness = size * 0.035f;
            for (float t = 0; t < Mathf.PI * 4f; t += 0.05f)
            {
                float r = maxRadius * (t / (Mathf.PI * 4f));
                Vector2 p = center + new Vector2(Mathf.Cos(t), Mathf.Sin(t)) * r;
                DrawCircleFill(pixels, size, p, thickness, color);
            }
        }

        private static void DrawShield(Color[] pixels, int size, Vector2 center, float radius, Color color)
        {
            // 방패: 상단 반원 + 하단 삼각형
            float thickness = size * 0.04f;
            DrawCircleRing(pixels, size, center + new Vector2(0, radius * 0.15f), radius * 0.6f, thickness, color);
            DrawLine(pixels, size, center + new Vector2(-radius * 0.52f, center.y * 0.02f),
                     center + new Vector2(0, -radius * 0.7f), thickness, color);
            DrawLine(pixels, size, center + new Vector2(radius * 0.52f, center.y * 0.02f),
                     center + new Vector2(0, -radius * 0.7f), thickness, color);
        }

        private static void DrawCrown(Color[] pixels, int size, Vector2 center, float radius, Color color)
        {
            float thickness = size * 0.04f;
            float baseY = center.y - radius * 0.3f;
            float topY = center.y + radius * 0.5f;
            // 바닥선
            DrawLine(pixels, size, new Vector2(center.x - radius, baseY), new Vector2(center.x + radius, baseY), thickness, color);
            // 5개 봉우리
            for (int i = 0; i < 5; i++)
            {
                float x = center.x - radius + radius * 2f * i / 4f;
                float peakY = (i % 2 == 0) ? topY : topY - radius * 0.3f;
                DrawLine(pixels, size, new Vector2(x, baseY), new Vector2(x, peakY), thickness, color);
            }
            // 상단 연결
            DrawLine(pixels, size, new Vector2(center.x - radius, topY), new Vector2(center.x + radius, topY), thickness * 0.7f, color);
        }

        private static Sprite GetEnemyOverlaySprite(JewelsHexaPuzzle.Data.EnemyType type)
        {
            switch (type)
            {
                case JewelsHexaPuzzle.Data.EnemyType.Divider: return dividerOverlaySprite;
                case JewelsHexaPuzzle.Data.EnemyType.GravityWarper: return gravityWarperOverlaySprite;
                case JewelsHexaPuzzle.Data.EnemyType.ReflectionShield: return reflectionShieldOverlaySprite;
                case JewelsHexaPuzzle.Data.EnemyType.TimeFreezer: return timeFreezerOverlaySprite;
                case JewelsHexaPuzzle.Data.EnemyType.ResonanceTwin: return resonanceTwinOverlaySprite;
                case JewelsHexaPuzzle.Data.EnemyType.ShadowSpore: return shadowSporeOverlaySprite;
                case JewelsHexaPuzzle.Data.EnemyType.ChaosOverlord: return chaosOverlordOverlaySprite;
                default: return null;
            }
        }

        private static Color GetEnemyOverlayColor(JewelsHexaPuzzle.Data.EnemyType type)
        {
            return JewelsHexaPuzzle.Data.EnemyRegistry.GetOverlayColor(type);
        }

        /// <summary>
        /// 폭탄 블록 스킬 레벨 텍스트 갱신 (v1/v2/v3 또는 숨김)
        /// </summary>
        private void UpdateBombSkillText()
        {
            var stm = JewelsHexaPuzzle.Managers.SkillTreeManager.Instance;
            int level = (stm != null) ? stm.GetBombDamageBonus() : 0;
            string text = JewelsHexaPuzzle.Utils.BlockSkillColors.GetBombLevelText(level);

            if (string.IsNullOrEmpty(text))
            {
                // 미해금: 텍스트 숨김
                if (bombSkillText != null) bombSkillText.enabled = false;
                return;
            }

            // 텍스트 오브젝트 생성 (최초 1회)
            if (bombSkillText == null)
            {
                GameObject textObj = new GameObject("BombSkillText");
                textObj.transform.SetParent(transform, false);
                bombSkillText = textObj.AddComponent<Text>();
                bombSkillText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                bombSkillText.fontStyle = FontStyle.Bold;
                bombSkillText.alignment = TextAnchor.MiddleCenter;
                bombSkillText.raycastTarget = false;

                Outline outline = textObj.AddComponent<Outline>();
                outline.effectColor = new Color(0f, 0f, 0f, 0.8f);
                outline.effectDistance = new Vector2(1f, -1f);

                RectTransform rt = textObj.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0.5f, 0.5f);
                rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = new Vector2(0f, -8f); // 아이콘 아래쪽
                rt.sizeDelta = new Vector2(60f, 20f);
                bombSkillText.fontSize = 14;
            }

            bombSkillText.text = text;
            bombSkillText.color = Color.white;
            bombSkillText.enabled = true;
        }

        private void ShowTimerText(int count)
        {
            if (timerText != null)
            {
                timerText.text = count.ToString();
                timerText.enabled = true;
                timerText.color = count <= 3 ? Color.red : Color.white;
            }
        }

        public void SetSelected(bool selected) { isSelected = selected; }

        public void SetHighlighted(bool highlighted)
        {
            isHighlighted = highlighted;
            if (borderImage != null)
            {
                // 하이라이트 시작 시 흰색 강조 (매칭)
                if (highlighted) borderImage.color = new Color(1f, 1f, 1f, 1f);
                else if (!isMatched) borderImage.color = GetDefaultBorderColor();  // 강화된 기본 테두리
            }
        }

        public void SetMatched(bool matched)
        {
            isMatched = matched;
            if (borderImage != null)
                borderImage.color = matched ? Color.white : GetDefaultBorderColor();  // 강화된 기본 테두리
        }

        // ============================================================
        // 튜토리얼 디밍/글로우 (TutorialManager에서 사용)
        // ============================================================
        private bool isTutorialDimmed = false;
        private bool isTutorialGlowing = false;
        private Coroutine tutorialGlowCoroutine;
        private CanvasGroup tutorialCanvasGroup;

        /// <summary>
        /// 블록을 어둡게 처리 (튜토리얼에서 비활성 블록 표시)
        /// </summary>
        public void SetTutorialDimmed(bool dimmed)
        {
            isTutorialDimmed = dimmed;
            EnsureTutorialCanvasGroup();
            if (tutorialCanvasGroup != null)
                tutorialCanvasGroup.alpha = dimmed ? 0.25f : 1f;
        }

        /// <summary>
        /// 블록에 글로우 펄스 효과 (튜토리얼에서 활성 블록 강조)
        /// </summary>
        /// <summary>
        /// Stage 1 힌트용 외곽선 점멸 — TutorialGlow와 독립적.
        /// alpha 값으로 은은한 노란 밝기 조절. false 호출 시 기본 테두리로 복구.
        /// </summary>
        public void SetStage1Hint(bool active, float alpha = 1f)
        {
            if (borderImage == null) return;
            if (active)
            {
                borderImage.enabled = true;
                // 부드러운 노란 강조색, 알파로 밝기 조절
                borderImage.color = new Color(1f, 0.95f, 0.45f, Mathf.Clamp01(alpha));
            }
            else if (!isMatched && !isHighlighted && !isTutorialGlowing)
            {
                borderImage.color = GetDefaultBorderColor();
            }
        }

        // Stage 1 힌트 바운스 원본 컬러 저장 (플래시 복원용)
        private Color stage1OriginalGemColor;
        private bool stage1GemColorCached = false;

        /// <summary>
        /// Stage 1 힌트 바운스 — 블록 크기 스케일 + 면 플래시.
        /// scalePulse: 1.0 기준 상대 스케일 (예: 0.15면 1.15배 → 1.0으로 복귀).
        /// flashIntensity: 0~1 범위. 1이면 완전 밝은 흰색 플래시.
        /// </summary>
        public void ApplyStage1HintBounce(float scalePulse, float flashIntensity)
        {
            // 스케일 펄스 적용
            float scale = 1f + Mathf.Clamp(scalePulse, 0f, 0.5f);
            transform.localScale = Vector3.one * scale;

            // 면 플래시 — gemImage를 원본 색상에서 흰색으로 블렌딩
            if (gemImage != null && gemImage.enabled)
            {
                if (!stage1GemColorCached)
                {
                    stage1OriginalGemColor = gemImage.color;
                    stage1GemColorCached = true;
                }
                float t = Mathf.Clamp01(flashIntensity);
                gemImage.color = Color.Lerp(stage1OriginalGemColor, Color.white, t);
            }
        }

        /// <summary>
        /// Stage 1 힌트 바운스 종료 — 스케일/색상 복원.
        /// </summary>
        public void ClearStage1HintBounce()
        {
            transform.localScale = Vector3.one;
            if (gemImage != null && stage1GemColorCached)
            {
                gemImage.color = stage1OriginalGemColor;
                stage1GemColorCached = false;
            }
        }

        public void SetTutorialGlow(bool glow)
        {
            isTutorialGlowing = glow;
            if (glow)
            {
                EnsureTutorialCanvasGroup();
                if (tutorialCanvasGroup != null)
                    tutorialCanvasGroup.alpha = 1f;
                if (borderImage != null)
                    borderImage.color = new Color(1f, 0.95f, 0.5f, 1f); // 밝은 노란 테두리
                if (tutorialGlowCoroutine != null) StopCoroutine(tutorialGlowCoroutine);
                tutorialGlowCoroutine = StartCoroutine(TutorialGlowPulse());
            }
            else
            {
                if (tutorialGlowCoroutine != null)
                {
                    StopCoroutine(tutorialGlowCoroutine);
                    tutorialGlowCoroutine = null;
                }
                if (borderImage != null && !isMatched && !isHighlighted)
                    borderImage.color = GetDefaultBorderColor();
                transform.localScale = Vector3.one;
            }
        }

        /// <summary>
        /// 튜토리얼 비주얼 전체 초기화
        /// </summary>
        public void ClearTutorialVisuals()
        {
            SetTutorialGlow(false);
            SetTutorialDimmed(false);
        }

        private void EnsureTutorialCanvasGroup()
        {
            if (tutorialCanvasGroup == null)
            {
                tutorialCanvasGroup = GetComponent<CanvasGroup>();
                if (tutorialCanvasGroup == null)
                    tutorialCanvasGroup = gameObject.AddComponent<CanvasGroup>();
            }
        }

        private IEnumerator TutorialGlowPulse()
        {
            float speed = 3f;
            while (isTutorialGlowing)
            {
                float t = (Mathf.Sin(Time.unscaledTime * speed) + 1f) / 2f; // 0~1
                float scale = 1f + t * 0.08f; // 1.0 ~ 1.08
                transform.localScale = Vector3.one * scale;

                if (borderImage != null)
                {
                    float alpha = 0.7f + t * 0.3f;
                    borderImage.color = new Color(1f, 0.95f, 0.5f, alpha);
                }
                yield return null;
            }
        }

// === 빨간색 테두리 점멸 (특수 블록 연쇄 발동 예고) ===
        private bool isPendingActivation;
        public bool IsPendingActivation => isPendingActivation;
        private Coroutine blinkCoroutine;
        private Color originalBorderColor;

        /// <summary>
        /// 빨간색 테두리 점멸 시작 (충돌 직후 호출)
        /// 시간이 지날수록 점멸 속도가 빨라져서 유저가 발동 시점을 예측 가능
        /// </summary>
public void StartWarningBlink(float totalDuration = 1.5f)
        {
            StopWarningBlink();
            if (borderImage != null)
            {
                originalBorderColor = borderImage.color;
                borderImage.enabled = true;
            }
            blinkCoroutine = StartCoroutine(WarningBlinkCoroutine(totalDuration));
        }

        /// <summary>
        /// 점멸 정지 및 원래 색상 복원
        /// </summary>
public void StopWarningBlink()
        {
            if (blinkCoroutine != null)
            {
                StopCoroutine(blinkCoroutine);
                blinkCoroutine = null;
            }
            // 빨간색 테두리 상태 해제 → 원래 색상 복원
            isPendingActivation = false;
            if (borderImage != null)
            {
                borderImage.color = isMatched ? Color.white : GetDefaultBorderColor();  // 강화된 기본 테두리
            }
        }

private IEnumerator WarningBlinkCoroutine(float totalDuration)
        {
            // 빨간색 테두리는 SetPendingActivation에서 이미 설정됨
            // 여기서는 점멸 속도를 점점 빨리 하여 발동 예고
            Color blinkColor = new Color(0.95f, 0.72f, 0.68f, 0.8f); // 빨간색
            Color dimColor = new Color(0.85f, 0.60f, 0.55f, 0.5f); // 어두운 파스텔 코랄
            float elapsed = 0f;

            // 초기 점멸 주기 0.4초 → 발동 직전 0.06초까지 가속
            float startInterval = 0.4f;
            float endInterval = 0.06f;

            while (elapsed < totalDuration)
            {
                float t = elapsed / totalDuration; // 0 → 1
                float currentInterval = Mathf.Lerp(startInterval, endInterval, t * t); // 점점 가속

                // 밝은 빨간색 ON
                if (borderImage != null)
                    borderImage.color = blinkColor;
                yield return new WaitForSeconds(currentInterval * 0.5f);
                elapsed += currentInterval * 0.5f;

                // 어두운 빨간색 OFF (완전히 사라지지 않고 빨간 톤 유지)
                if (borderImage != null)
                    borderImage.color = dimColor;
                yield return new WaitForSeconds(currentInterval * 0.5f);
                elapsed += currentInterval * 0.5f;
            }

            // 마지막에 밝은 빨간색으로 끝내서 발동 순간 강조
            if (borderImage != null)
                borderImage.color = blinkColor;

            blinkCoroutine = null;
        }


        public bool DecrementTimeBomb()
        {
            if (blockData != null && blockData.specialType == SpecialBlockType.TimeBomb)
            {
                blockData.timeBombCount--;
                ShowTimerText(blockData.timeBombCount);
                return blockData.timeBombCount <= 0;
            }
            return false;
        }

        public bool RemoveVinyl()
        {
            if (blockData != null && blockData.vinylLayer > 0)
            {
                blockData.vinylLayer--;
                UpdateOverlay();
                return blockData.vinylLayer == 0;
            }
            return true;
        }

        public void RemoveChain()
        {
            if (blockData != null)
            {
                blockData.hasChain = false;
                UpdateOverlay();
                StartCoroutine(ChainBreakEffect());
            }
        }

        /// <summary>
        /// 사슬 파괴 이펙트 — 사슬 조각 산개 + 플래시 + 스케일 펄스
        /// </summary>
        private IEnumerator ChainBreakEffect()
        {
            Vector3 center = transform.position;

            // 1. 백색 플래시
            GameObject flash = new GameObject("ChainBreakFlash");
            flash.transform.SetParent(transform, false);
            flash.transform.localPosition = Vector3.zero;
            var flashImg = flash.AddComponent<Image>();
            flashImg.raycastTarget = false;
            flashImg.sprite = GetHexFlashSprite();
            flashImg.color = new Color(0.8f, 0.85f, 0.9f, 0.8f);
            RectTransform flashRt = flash.GetComponent<RectTransform>();
            flashRt.sizeDelta = new Vector2(40f, 40f);

            // 2. 사슬 조각 산개 (8개)
            for (int i = 0; i < 8; i++)
                StartCoroutine(AnimateChainShard(center));

            // 3. 블록 스케일 펄스 + 플래시 페이드
            float duration = 0.25f;
            float elapsed = 0f;
            Vector3 origScale = transform.localScale;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                // 스케일: 1.0 → 1.15 → 1.0
                float pulse = 1f + 0.15f * Mathf.Sin(t * Mathf.PI);
                transform.localScale = origScale * pulse;

                // 플래시 확대 + 페이드
                float flashScale = 1f + t * 4f;
                flashRt.sizeDelta = new Vector2(40f * flashScale, 40f * flashScale);
                flashImg.color = new Color(0.8f, 0.85f, 0.9f, 0.8f * (1f - t));

                yield return null;
            }

            transform.localScale = origScale;
            Destroy(flash);
        }

        /// <summary>
        /// 개별 사슬 파편 애니메이션 — 메탈릭 실버 파편이 회전하며 산개
        /// </summary>
        private IEnumerator AnimateChainShard(Vector3 center)
        {
            // 사슬 조각이 블록 위에 그려지도록 부모의 부모(그리드)에 생성
            Transform effectParent = transform.parent != null ? transform.parent : transform;

            GameObject shard = new GameObject("ChainShard");
            shard.transform.SetParent(effectParent, false);
            shard.transform.position = center;

            var image = shard.AddComponent<Image>();
            image.raycastTarget = false;

            RectTransform rt = shard.GetComponent<RectTransform>();
            // 직사각형 파편 (사슬 링크 조각 느낌)
            float w = UnityEngine.Random.Range(4f, 10f);
            float h = UnityEngine.Random.Range(2f, 5f);
            rt.sizeDelta = new Vector2(w, h);

            // 랜덤 방향 산개
            float angle = UnityEngine.Random.Range(0f, 360f) * Mathf.Deg2Rad;
            float speed = UnityEngine.Random.Range(120f, 280f);
            Vector2 velocity = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * speed;

            // 메탈릭 실버 색상 (밝기 랜덤)
            float gray = UnityEngine.Random.Range(0.55f, 0.9f);
            Color shardColor = new Color(gray, gray, gray + 0.05f, 1f);
            image.color = shardColor;

            // 초기 회전
            float rotSpeed = UnityEngine.Random.Range(-600f, 600f);
            float initRot = UnityEngine.Random.Range(0f, 360f);
            shard.transform.localRotation = Quaternion.Euler(0, 0, initRot);

            float lifetime = UnityEngine.Random.Range(0.25f, 0.45f);
            float elapsedTime = 0f;
            float gravityY = -400f; // 아래로 떨어지는 중력

            while (elapsedTime < lifetime)
            {
                elapsedTime += Time.deltaTime;
                float t = elapsedTime / lifetime;

                // 이동 + 중력
                velocity.y += gravityY * Time.deltaTime;
                Vector3 pos = shard.transform.position;
                pos.x += velocity.x * Time.deltaTime;
                pos.y += velocity.y * Time.deltaTime;
                shard.transform.position = pos;

                // 감속
                velocity.x *= 0.97f;

                // 회전
                shard.transform.Rotate(0, 0, rotSpeed * Time.deltaTime);

                // 페이드 + 축소
                shardColor.a = 1f - t * t;
                image.color = shardColor;
                float scale = 1f - t * 0.4f;
                rt.sizeDelta = new Vector2(w * scale, h * scale);

                yield return null;
            }

            Destroy(shard);
        }

        public void OnPointerClick(PointerEventData eventData) { OnBlockClicked?.Invoke(this); }
        public void OnPointerDown(PointerEventData eventData) { OnBlockPressed?.Invoke(this); }
        public void OnPointerUp(PointerEventData eventData) { OnBlockReleased?.Invoke(this); }

        public void SwapDataWith(HexBlock other)
        {
            if (other == null) return;
            BlockData temp = this.blockData.Clone();
            this.SetBlockData(other.blockData);
            other.SetBlockData(temp);
        }
    

/// <summary>
        /// 특수 블록 아이콘을 별도 GameObject로 복제하여 부모 아래에 배치.
        /// ClearData 후에도 아이콘이 남아 있도록 하기 위함.
        /// 호출자가 반환된 GameObject의 수명을 관리해야 함.
        /// </summary>
public GameObject CreateFloatingSpecialIcon(Transform parent)
        {
            if (drillIndicator == null || !drillIndicator.enabled || drillIndicator.sprite == null)
                return null;

            GameObject floatingIcon = new GameObject("FloatingSpecialIcon");
            floatingIcon.transform.SetParent(parent, false);
            floatingIcon.transform.position = transform.position;

            var img = floatingIcon.AddComponent<Image>();
            img.sprite = drillIndicator.sprite;
            img.color = drillIndicator.color;
            img.raycastTarget = false;

            RectTransform rt = floatingIcon.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(40f, 40f);

            return floatingIcon;
        }

/// <summary>
        /// 드릴에 의해 영향받은 즉시 호출 - 빨간색 테두리로 변경
        /// 이후 실제 발동 전에 StartWarningBlink로 점멸 가속
        /// </summary>
public void SetPendingActivation()
        {
            isPendingActivation = true;
            // Data에도 플래그 설정 (낙하 시 Data가 Clone되어 이동해도 플래그 유지)
            if (blockData != null)
                blockData.pendingActivation = true;
            if (borderImage != null)
            {
                borderImage.enabled = true;
                borderImage.color = new Color(0.95f, 0.72f, 0.68f, 0.8f);
            }
        }

        // ============================================================
        // 고블린 폭탄 오버레이 (블록 일체형)
        // ============================================================

        /// <summary>
        /// 고블린 폭탄 오버레이 업데이트: hasGoblinBomb이면 아이콘+카운트다운 표시
        /// </summary>
        private void UpdateGoblinBombOverlay()
        {
            if (blockData == null || !blockData.hasGoblinBomb)
            {
                HideGoblinBombOverlay();
                return;
            }

            // 이미지 생성 (최초 1회)
            if (goblinBombImage == null)
            {
                CreateGoblinBombOverlay();
            }

            goblinBombImage.enabled = true;

            // ★ 폭탄 PNG 시트(00:03/00:02/00:01)가 있으면 카운트다운별 프레임 교체
            Sprite[] frames = GetGoblinBombCountdownFrames();
            if (frames != null)
            {
                // 이미지 자체에 카운트다운 숫자가 새겨져 있음 → 프레임만 교체하고 별도 텍스트는 숨김
                int idx = Mathf.Clamp(3 - blockData.goblinBombCountdown, 0, 2);
                goblinBombImage.sprite = frames[idx];
                goblinBombImage.color = Color.white;
                if (goblinBombCountdownText != null) goblinBombCountdownText.enabled = false;
                return;
            }

            // 폴백: 프로시저럴 폭탄 + 카운트다운 텍스트
            if (blockData.goblinBombCountdown <= 1)
                goblinBombImage.color = new Color(1f, 0.3f, 0.15f, 0.95f); // 빨간색 경고
            else
                goblinBombImage.color = new Color(0.15f, 0.12f, 0.1f, 0.9f); // 검정 폭탄

            if (goblinBombCountdownText != null)
            {
                goblinBombCountdownText.enabled = true;
                goblinBombCountdownText.text = blockData.goblinBombCountdown.ToString();
                goblinBombCountdownText.color = blockData.goblinBombCountdown <= 1
                    ? new Color(1f, 1f, 0.3f, 1f)  // 노란색 긴급
                    : Color.white;
            }
        }

        /// <summary>
        /// 고블린 폭탄 오버레이 숨김
        /// </summary>
        private void HideGoblinBombOverlay()
        {
            if (goblinBombImage != null) goblinBombImage.enabled = false;
            if (goblinBombCountdownText != null) goblinBombCountdownText.enabled = false;
        }

        // ── 흙더미 오버레이 ──
        private Image dirtMoundImage;
        private static Sprite dirtMoundSprite1; // 1/3 쌓임
        private static Sprite dirtMoundSprite2; // 2/3 쌓임

        /// <summary>
        /// 외부 시스템(미션 UI 등)에서 흙더미 아이콘 스프라이트를 얻기 위한 공개 접근.
        /// level=1: 1/3 쌓임, level=2: 2/3 쌓임. 캐시된 스프라이트 재사용.
        /// </summary>
        public static Sprite GetDirtMoundSprite(int level)
        {
            if (level >= 2)
            {
                if (dirtMoundSprite2 == null) dirtMoundSprite2 = CreateDirtMoundSprite(level: 2);
                return dirtMoundSprite2;
            }
            if (dirtMoundSprite1 == null) dirtMoundSprite1 = CreateDirtMoundSprite(level: 1);
            return dirtMoundSprite1;
        }

        /// <summary>
        /// 흙더미 오버레이 갱신 — dirtMound = 0 숨김, 1 = 1/3, 2 = 2/3.
        /// </summary>
        private void UpdateDirtMoundOverlay()
        {
            if (blockData == null || blockData.dirtMound <= 0)
            {
                if (dirtMoundImage != null) dirtMoundImage.enabled = false;
                return;
            }

            if (dirtMoundImage == null)
                CreateDirtMoundOverlay();

            if (dirtMoundImage == null) return;

            // 레벨별 스프라이트 + 크기 (blockHeight의 1/3, 2/3 비율)
            int level = Mathf.Clamp(blockData.dirtMound, 1, 2);
            if (dirtMoundSprite1 == null) dirtMoundSprite1 = CreateDirtMoundSprite(level: 1);
            if (dirtMoundSprite2 == null) dirtMoundSprite2 = CreateDirtMoundSprite(level: 2);

            dirtMoundImage.sprite = (level == 2) ? dirtMoundSprite2 : dirtMoundSprite1;
            dirtMoundImage.enabled = true;
            dirtMoundImage.color = Color.white;
        }

        /// <summary>
        /// 흙더미 오버레이 GameObject 생성 (블록 자식). 블록 하단에 부착.
        /// </summary>
        private void CreateDirtMoundOverlay()
        {
            GameObject obj = new GameObject("DirtMoundOverlay");
            obj.transform.SetParent(transform, false);

            RectTransform rt = obj.AddComponent<RectTransform>();
            // 하단 정렬 — 블록 너비 100%, 높이 100% (스프라이트 자체가 1/3 또는 2/3 크기)
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = Vector2.zero;
            rt.localScale = Vector3.one;

            dirtMoundImage = obj.AddComponent<Image>();
            dirtMoundImage.raycastTarget = false;
            dirtMoundImage.preserveAspect = false;
            // 항상 다른 오버레이보다 위에 표시
            obj.transform.SetAsLastSibling();
        }

        /// <summary>
        /// 프로시저럴 흙더미 스프라이트 생성 — 헥스 블록 외곽선 안쪽에 자연스럽게 쌓인 형태.
        /// level=1: 헥스 하단 1/3 영역, level=2: 하단 2/3 영역.
        /// Flat-top 헥스 마스크 적용 + 부드러운 굴곡 표면 + 갈색 그라데이션 + 작은 돌 디테일.
        /// </summary>
        private static Sprite CreateDirtMoundSprite(int level)
        {
            int texW = 128;
            int texH = 128;
            Texture2D tex = new Texture2D(texW, texH, TextureFormat.ARGB32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;

            Color32 clear = new Color32(0, 0, 0, 0);
            // 갈색 톤 (자연 흙)
            Color32 dirtBase  = new Color32(118, 78, 42, 255);   // 메인 갈색
            Color32 dirtDark  = new Color32(72, 46, 22, 255);    // 깊은 그림자
            Color32 dirtLight = new Color32(165, 120, 75, 255);  // 햇빛 하이라이트
            Color32 dirtRim   = new Color32(40, 24, 10, 255);    // 표면 윤곽선
            Color32 dirtMid   = new Color32(140, 95, 55, 255);   // 중간 톤

            Color32[] pixels = new Color32[texW * texH];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = clear;

            // ── Flat-top 헥스 기하 ──
            // 가로 = 2s, 세로 = sqrt(3)*s. 텍스처에 맞춰 fit.
            //   s = texW / 2 = 64 → 헥스 너비 128, 높이 sqrt(3)*64 ≈ 110.85
            //   세로 여유 (texH - hexH) / 2 ≈ 8.5px씩 위·아래 여백
            float s = texW * 0.5f;
            float hexHalfW = s;                        // 64
            float hexHalfH = s * Mathf.Sqrt(3f) * 0.5f; // ≈ 55.43
            float cx = texW * 0.5f;
            float cy = texH * 0.5f;

            // 흙 표면 기준 Y (hex 내부에서 수평 비율 fillRatio)
            //   level 1: 0.34 (1/3), level 2: 0.68 (2/3)
            float fillRatio = (level == 2) ? 0.68f : 0.34f;
            float surfaceCenterY = -hexHalfH + 2f * hexHalfH * fillRatio; // hex 중심 기준 Y (하단=-h, 상단=+h)

            // ── 픽셀별 흙 영역 판정 + 색칠 ──
            for (int py = 0; py < texH; py++)
            {
                float ly = py - cy; // hex 중심 기준 Y (음수=하단)
                if (ly < -hexHalfH || ly > hexHalfH) continue; // hex 세로 범위 밖

                // hex 내부의 x 최대값: maxX = s * (1 - |ly| / hexH_total) where hexH_total = sqrt(3)*s
                //   상단 가까울수록 좁아짐
                float relAbsY = Mathf.Abs(ly);
                float hexMaxX = s * (1f - relAbsY / (s * Mathf.Sqrt(3f)));

                // 표면 곡선: 가운데가 살짝 솟고 가장자리가 낮은 자연 굴곡
                //   bump = surfaceCenterY + height * (1 - 4*(x/halfW)^2)  [x중심에서 양쪽 가장자리 0]
                //   level 2는 굴곡 더 큼

                for (int px = 0; px < texW; px++)
                {
                    float lx = px - cx;
                    if (Mathf.Abs(lx) > hexMaxX) continue; // hex 외곽 밖

                    // 흙 표면 Y 계산 — 가운데가 살짝 솟음 + 미세 노이즈
                    float xnorm = lx / hexHalfW; // -1 ~ +1
                    float bump = (1f - xnorm * xnorm); // 가운데 1, 가장자리 0
                    float bumpAmp = (level == 2) ? 6f : 4f;
                    float noise = (Mathf.PerlinNoise(px * 0.08f, level * 17f) - 0.5f) * 3f;
                    float surfaceY = surfaceCenterY + bumpAmp * bump + noise;

                    if (ly > surfaceY) continue; // 표면 위쪽은 비어있음

                    // 흙으로 채움
                    // 깊이별 톤: 표면 가까울수록 어두운 그림자, 깊을수록 메인/밝은 톤
                    float depthFromSurface = surfaceY - ly; // 양수 = 표면 아래
                    float depthFromBottom = ly + hexHalfH;  // 양수 = 하단부터 위
                    Color32 c;

                    if (depthFromSurface < 1.5f)
                    {
                        // 표면 윤곽선
                        c = dirtRim;
                    }
                    else if (depthFromSurface < 5f)
                    {
                        // 표면 바로 아래 그림자
                        c = dirtDark;
                    }
                    else if (depthFromBottom < 4f)
                    {
                        // 헥스 바닥 가까이 — 살짝 어두운 톤
                        c = dirtDark;
                    }
                    else
                    {
                        // 메인 갈색 ↔ 밝은 갈색 노이즈로 자연스러운 질감
                        float n = (Mathf.PerlinNoise(px * 0.10f + level, py * 0.10f) - 0.5f);
                        Color32 baseMix = LerpC(dirtBase, dirtMid, 0.5f + n * 0.6f);
                        // 가운데 융기부 살짝 밝게
                        if (bump > 0.65f && depthFromSurface < 12f)
                            baseMix = LerpC(baseMix, dirtLight, 0.4f);
                        c = baseMix;
                    }

                    // 헥스 외곽 가까운 픽셀은 더 짙게 (가장자리 윤곽 강화)
                    float edgeDist = hexMaxX - Mathf.Abs(lx);
                    if (edgeDist < 1.5f) c = dirtRim;
                    else if (edgeDist < 3f) c = dirtDark;

                    pixels[py * texW + px] = c;
                }
            }

            // ── 작은 돌 디테일 (흙 영역 안에만) ──
            var rng = new System.Random(level * 47 + 11);
            int stoneCount = (level == 2) ? 18 : 9;
            for (int i = 0; i < stoneCount; i++)
            {
                int sx = rng.Next(8, texW - 8);
                int sy = rng.Next(6, Mathf.Max(8, (int)(cy + surfaceCenterY) - 4));
                int sz = rng.Next(2, 4);
                Color32 stoneCol = (rng.Next(2) == 0) ? dirtDark : dirtRim;
                for (int dy = -sz; dy <= sz; dy++)
                    for (int dx = -sz; dx <= sz; dx++)
                    {
                        int x2 = sx + dx, y2 = sy + dy;
                        if (x2 < 0 || x2 >= texW || y2 < 0 || y2 >= texH) continue;
                        if (dx * dx + dy * dy > sz * sz) continue;
                        if (pixels[y2 * texW + x2].a == 0) continue; // 흙 영역 밖이면 무시
                        pixels[y2 * texW + x2] = stoneCol;
                    }
            }

            // ── 표면 위쪽에 작은 흙 알갱이 (자연스러운 흩뿌림) ──
            int sprinkleCount = (level == 2) ? 8 : 5;
            for (int i = 0; i < sprinkleCount; i++)
            {
                int sx = rng.Next(10, texW - 10);
                // 표면 위 약간 위쪽에 작은 점들
                int surfacePixelY = (int)(cy + surfaceCenterY + (rng.NextDouble() < 0.3 ? 4 : -2));
                int sy = surfacePixelY + rng.Next(-2, 4);
                if (sy < 0 || sy >= texH) continue;
                int x2 = sx, y2 = sy;
                if (x2 < 0 || x2 >= texW) continue;
                pixels[y2 * texW + x2] = dirtDark;
            }

            tex.SetPixels32(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, texW, texH), new Vector2(0.5f, 0.5f), 100f);
        }

        private static Color32 LerpC(Color32 a, Color32 b, float t)
        {
            t = Mathf.Clamp01(t);
            return new Color32(
                (byte)(a.r + (b.r - a.r) * t),
                (byte)(a.g + (b.g - a.g) * t),
                (byte)(a.b + (b.b - a.b) * t),
                (byte)(a.a + (b.a - a.a) * t));
        }

        /// <summary>
        /// 고블린 폭탄 오버레이 UI 요소 생성 (블록의 자식으로)
        /// </summary>
        private void CreateGoblinBombOverlay()
        {
            // 폭탄 아이콘
            GameObject bombObj = new GameObject("GoblinBombOverlay");
            bombObj.transform.SetParent(transform, false);
            RectTransform brt = bombObj.AddComponent<RectTransform>();
            brt.anchoredPosition = Vector2.zero;

            goblinBombImage = bombObj.AddComponent<Image>();
            goblinBombImage.raycastTarget = false;

            float size;
            Sprite[] frames = GetGoblinBombCountdownFrames();
            if (frames != null)
            {
                // PNG 폭탄(타이머 디스플레이 포함): 2배 확대(44→88) + 원본 비율 유지
                size = 88f;
                goblinBombImage.sprite = frames[0];
                goblinBombImage.preserveAspect = true;
            }
            else
            {
                // 프로시저럴 폭탄: 작은 둥근 아이콘
                size = 30f;
                if (goblinBombOverlaySprite == null)
                    goblinBombOverlaySprite = CreateGoblinBombSprite();
                goblinBombImage.sprite = goblinBombOverlaySprite;
            }
            brt.sizeDelta = new Vector2(size, size);
            brt.localScale = Vector3.one;

            // 카운트다운 텍스트 (프로시저럴 폴백 전용 — PNG 사용 시 UpdateGoblinBombOverlay가 숨김)
            GameObject textObj = new GameObject("GoblinBombCountdown");
            textObj.transform.SetParent(bombObj.transform, false);
            RectTransform trt = textObj.AddComponent<RectTransform>();
            trt.anchoredPosition = Vector2.zero;
            trt.sizeDelta = new Vector2(size, size);

            goblinBombCountdownText = textObj.AddComponent<Text>();
            goblinBombCountdownText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (goblinBombCountdownText.font == null)
                goblinBombCountdownText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            goblinBombCountdownText.fontSize = 18;
            goblinBombCountdownText.alignment = TextAnchor.MiddleCenter;
            goblinBombCountdownText.fontStyle = FontStyle.Bold;
            goblinBombCountdownText.color = Color.white;
            goblinBombCountdownText.raycastTarget = false;

            Outline outline = textObj.AddComponent<Outline>();
            outline.effectColor = new Color(0.05f, 0f, 0f, 1f);
            outline.effectDistance = new Vector2(1f, -1f);
        }

        /// <summary>
        /// 폭탄 카운트다운 시트(Resources/Goblins/goblin_bomb_countdown) 로드 → 가로 3등분 슬라이스.
        /// 프레임 0 = 00:03, 1 = 00:02, 2 = 00:01. 로드 실패 시 frames = null (프로시저럴 폴백).
        /// </summary>
        private static Sprite[] GetGoblinBombCountdownFrames()
        {
            // ★ 자기치유 캐시(2026-07-02): Play 유지 중 AssetDatabase.Refresh(에셋만 변경)가 텍스처를
            //   재임포트하면 기존 Texture2D 인스턴스가 파괴되는데 도메인은 유지돼 정적 캐시가 죽은
            //   텍스처를 참조 → 폭탄이 "흰색"으로 렌더되던 반복 버그. 텍스처 유효성까지 검사해 재생성.
            if (goblinBombCountdownLoaded &&
                (goblinBombCountdownFrames == null || goblinBombCountdownFrames.Length == 0 ||
                 goblinBombCountdownFrames[0] == null || goblinBombCountdownFrames[0].texture == null))
            {
                goblinBombCountdownLoaded = false;
                goblinBombCountdownFrames = null;
                Debug.LogWarning("[HexBlock] 폭탄 카운트다운 스프라이트 캐시 무효(텍스처 재임포트) → 재생성");
            }
            if (goblinBombCountdownLoaded) return goblinBombCountdownFrames;

            Texture2D tex = Resources.Load<Texture2D>("Goblins/goblin_bomb_countdown");
            // ★ 로드 실패 시 null을 영구 캐싱하지 않는다(loaded 플래그 보류) → 다음 호출에서 재시도.
            //   (초기 프레임에 Resources가 준비되기 전 호출되면 한 번 null이 캐싱되어 이후 모든 폭탄이
            //    프로시저럴 폴백으로 떨어지는 잠재 버그 방지. 성공해야만 캐시 확정.)
            if (tex == null) return null;

            int fw = tex.width / 3;
            int fh = tex.height;
            var frames = new Sprite[3];
            for (int i = 0; i < 3; i++)
            {
                Rect rect = new Rect(i * fw, 0, fw, fh);
                frames[i] = Sprite.Create(tex, rect, new Vector2(0.5f, 0.5f), 100f);
            }
            goblinBombCountdownFrames = frames;
            goblinBombCountdownLoaded = true;
            return goblinBombCountdownFrames;
        }

        /// <summary>
        /// 고블린 폭탄 오버레이용 프로시저럴 스프라이트 (둥근 검정 폭탄)
        /// </summary>
        private static Sprite CreateGoblinBombSprite()
        {
            int sz = 64;
            Texture2D tex = new Texture2D(sz, sz, TextureFormat.RGBA32, false);
            Color[] px = new Color[sz * sz];
            float c = sz / 2f;
            float r = sz * 0.38f;

            for (int y = 0; y < sz; y++)
            {
                for (int x = 0; x < sz; x++)
                {
                    float dx = x - c, dy = y - c;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    if (d < r)
                    {
                        float grad = 1f - (d / r) * 0.3f;
                        px[y * sz + x] = new Color(0.15f * grad, 0.12f * grad, 0.1f * grad, 0.9f);
                    }
                    else if (d < r + 2f)
                    {
                        px[y * sz + x] = new Color(0.8f, 0.15f, 0.1f, 0.9f);
                    }
                    else
                    {
                        px[y * sz + x] = Color.clear;
                    }
                }
            }

            // 도화선
            int fuseY0 = (int)(c + r * 0.65f);
            int fuseY1 = Mathf.Min(sz - 3, (int)(c + r * 1.1f));
            for (int y = fuseY0; y < fuseY1; y++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    int px2 = (int)c + dx;
                    if (px2 >= 0 && px2 < sz) px[y * sz + px2] = new Color(0.6f, 0.4f, 0.1f, 1f);
                }

            tex.SetPixels(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, sz, sz), new Vector2(0.5f, 0.5f));
        }
}
}