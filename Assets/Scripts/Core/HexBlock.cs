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
        [SerializeField] private Image overlayImage;
        [SerializeField] private Image drillIndicator;
        [SerializeField] private Text timerText;

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
                private static Sprite bombIconSprite;
        private static Sprite donutIconSprite;
        private static Sprite xBlockIconSprite;
        private static Sprite laserIconSprite;
        private static Sprite chainOverlaySprite;
        private static Sprite thornOverlaySprite;

        // 적군 오버레이 스프라이트
        private static Sprite dividerOverlaySprite;
        private static Sprite gravityWarperOverlaySprite;
        private static Sprite reflectionShieldOverlaySprite;
        private static Sprite timeFreezerOverlaySprite;
        private static Sprite resonanceTwinOverlaySprite;
        private static Sprite shadowSporeOverlaySprite;
        private static Sprite chaosOverlordOverlaySprite;

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
        /// 색상도둑 시각 처리: 회색 블록 + 펄스 애니메이션
        /// </summary>
        private void ApplyChromophageVisuals()
        {
            if (blockData == null) return;

            // 블록 색상을 회색으로 변경
            SetGemColor(new Color(0.5f, 0.5f, 0.5f, 1f));

            // 색상도둑 오버레이 표시
            if (overlayImage != null)
            {
                overlayImage.color = new Color(0.6f, 0.6f, 0.6f, 0.4f); // 회색 슬라임
                overlayImage.enabled = true;
            }

            // 테두리를 회색으로
            if (borderImage != null)
            {
                borderImage.color = new Color(0.4f, 0.4f, 0.4f, 0.7f);
            }

            // 펄스 애니메이션 시작
            StartCoroutine(ChromophagePulseAnimation());
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
                        overlayImage.color = new Color(0.6f, 0.6f, 0.6f, alpha);
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
                        overlayImage.color = new Color(0.6f, 0.6f, 0.6f, alpha);
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
                drillVerticalSprite = CreateArrowSprite(64, 0);      // r축: 화면 세로 ↕
                drillSlashSprite = CreateArrowSprite(64, -60);     // s축: 화면 / 방향 (세로에서 시계 60°)
                drillBackSlashSprite = CreateArrowSprite(64, 60);  // q축: 화면 \\ 방향 (세로에서 시계 120°)면 \\ 방향방향 (-60°)
            }
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
                        // === 베벨 테두리 ===
                        float innerDist = HexSignedDistance(point, center, innerRadius);
                        float outerAlpha = Mathf.Clamp01(1f - outerDist / aa);
                        float innerAlpha = Mathf.Clamp01(innerDist / aa);
                        float ringAlpha = outerAlpha * innerAlpha;

                        // 방향성 베벨 (매트: 완만한 명암)
                        Vector2 dir = (point - center);
                        float dirLen = dir.magnitude;
                        if (dirLen > 0.001f) dir /= dirLen;
                        float lightDot = Vector2.Dot(dir, lightDir);
                        float bevel = 0.78f + lightDot * 0.10f; // 0.68 ~ 0.88 (축소된 베벨)

                        // 링 중심부 밝기 부스트 (매트: 미세)
                        float ringCenter = Mathf.Min(outerAlpha, innerAlpha);
                        bevel += ringCenter * 0.08f;

                        // 소프트 외곽 글로우 (매트: 거의 없음)
                        float glowAlpha = Mathf.Clamp01(1f - outerDist / (aa * 2f)) * 0.08f;
                        float finalAlpha = Mathf.Clamp01(ringAlpha + glowAlpha);

                        float b = Mathf.Clamp01(bevel);
                        pixels[y * size + x] = new Color(b, b, b, finalAlpha);
                    }
                    else
                    {
                        // === 부드러운 볼록 쿠션 배경 ===
                        float alpha = Mathf.Clamp01(1f - outerDist / aa);

                        Vector2 offset = point - center;
                        float pixelDist = offset.magnitude;
                        float normDist = Mathf.Clamp01(pixelDist / maxPixelDist);

                        // 중심부 밝고 가장자리 약간 어둡게 (볼록 쿠션 느낌)
                        float cushion = 0.90f + (1f - normDist) * 0.08f;

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
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Color[] pixels = new Color[size * size];

            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = Color.clear;

            Vector2 center = new Vector2(size / 2f, size / 2f);
            float arrowLength = size * 0.4f;
            float arrowWidth = size * 0.15f;
            float rad = rotation * Mathf.Deg2Rad;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    Vector2 p = new Vector2(x - center.x, y - center.y);
                    float rx = p.x * Mathf.Cos(-rad) - p.y * Mathf.Sin(-rad);
                    float ry = p.x * Mathf.Sin(-rad) + p.y * Mathf.Cos(-rad);

                    bool inArrow = false;
                    if (Mathf.Abs(rx) < arrowWidth / 2f && Mathf.Abs(ry) < arrowLength)
                        inArrow = true;
                    if (ry > arrowLength * 0.5f && ry < arrowLength)
                    {
                        float arrowHead = (arrowLength - ry) / (arrowLength * 0.5f) * arrowWidth;
                        if (Mathf.Abs(rx) < arrowHead) inArrow = true;
                    }
                    if (ry < -arrowLength * 0.5f && ry > -arrowLength)
                    {
                        float arrowHead = (arrowLength + ry) / (arrowLength * 0.5f) * arrowWidth;
                        if (Mathf.Abs(rx) < arrowHead) inArrow = true;
                    }

                    if (inArrow) pixels[y * size + x] = new Color(0.98f, 0.95f, 0.90f, 1f); // 크림 아이보리
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
            borderImage.color = new Color(0.92f, 0.88f, 0.85f, 0.30f);
            borderImage.raycastTarget = false;
            borderImage.type = Image.Type.Simple;
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
                rt.sizeDelta = new Vector2(40f, 40f);
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

            // ��������Ʈ ��Ȯ�� (Initialize�� Awake �Ŀ� ȣ��ǹǷ�)
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
            if (overlayImage != null) overlayImage.enabled = false;
            if (timerText != null) timerText.enabled = false;
            if (drillIndicator != null) drillIndicator.enabled = false;
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
        }

public void UpdateVisuals()
        {
            EnsureSpritesCreated();

            if (blockData == null || blockData.gemType == GemType.None)
            {
                SetEmpty();
                return;
            }

            if (backgroundImage != null)
            {
                backgroundImage.color = new Color(0.96f, 0.93f, 0.90f, 0.28f);
            }

            Color gemColor = GemColors.GetColor(blockData.gemType);
            SetGemColor(gemColor);

            // 테두리 색상 결정
            if (borderImage != null)
            {
                borderImage.enabled = true;
                if (blockData.pendingActivation)
                    borderImage.color = new Color(0.95f, 0.72f, 0.68f, 0.8f);
                else
                    borderImage.color = isMatched ? Color.white : new Color(0.92f, 0.88f, 0.85f, 0.30f);
            }

            // 특수 블록 아이콘/추가 시각 처리 (통합)
            UpdateSpecialIndicator();
            UpdateOverlay();
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
                    break;

                case SpecialBlockType.Rainbow:
                    if (donutIconSprite == null)
                        donutIconSprite = DonutBlockSystem.GetDonutIconSprite();
                    ShowSpecialIcon(donutIconSprite);
                    break;

                case SpecialBlockType.XBlock:
                    if (xBlockIconSprite == null)
                        xBlockIconSprite = XBlockSystem.GetXBlockIconSprite();
                    ShowSpecialIcon(xBlockIconSprite);
                    break;

                case SpecialBlockType.Laser:
                    if (laserIconSprite == null)
                        laserIconSprite = LaserBlockSystem.GetLaserIconSprite();
                    ShowSpecialIcon(laserIconSprite);
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
        /// </summary>
        private void ShowSpecialIcon(Sprite iconSprite)
        {
            if (drillIndicator == null) return;
            drillIndicator.enabled = true;
            drillIndicator.sprite = iconSprite;
            drillIndicator.color = Color.white;
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

            if (overlayImage != null) overlayImage.enabled = false;
            if (timerText != null) timerText.enabled = false;
            if (drillIndicator != null) drillIndicator.enabled = false;
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
            switch (type)
            {
                case JewelsHexaPuzzle.Data.EnemyType.Divider: return new Color(0.1f, 0.85f, 0.85f, 0.8f);       // 청록
                case JewelsHexaPuzzle.Data.EnemyType.GravityWarper: return new Color(0.6f, 0.2f, 0.9f, 0.8f);    // 보라
                case JewelsHexaPuzzle.Data.EnemyType.ReflectionShield: return new Color(0.8f, 0.85f, 0.9f, 0.85f); // 은색
                case JewelsHexaPuzzle.Data.EnemyType.TimeFreezer: return new Color(0.3f, 0.6f, 1f, 0.8f);        // 파란
                case JewelsHexaPuzzle.Data.EnemyType.ResonanceTwin: return new Color(1f, 0.9f, 0.2f, 0.8f);      // 노란
                case JewelsHexaPuzzle.Data.EnemyType.ShadowSpore: return new Color(0.35f, 0.1f, 0.45f, 0.85f);   // 검보라
                case JewelsHexaPuzzle.Data.EnemyType.ChaosOverlord: return new Color(1f, 0.5f, 0.8f, 0.9f);      // 무지개(핑크)
                default: return Color.white;
            }
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
                // ���̶���Ʈ ������ ���� ������� ���� (����� ���)
                if (highlighted) borderImage.color = new Color(1f, 1f, 1f, 1f);
                else if (!isMatched) borderImage.color = new Color(0.92f, 0.88f, 0.85f, 0.30f);
            }
        }

        public void SetMatched(bool matched)
        {
            isMatched = matched;
            if (borderImage != null)
                borderImage.color = matched ? Color.white : new Color(0.92f, 0.88f, 0.85f, 0.30f);
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
                borderImage.color = isMatched ? Color.white : new Color(0.92f, 0.88f, 0.85f, 0.30f);
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
}
}