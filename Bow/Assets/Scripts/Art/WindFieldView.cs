using UnityEngine;

namespace Bow.Art
{
    /// <summary>
    /// 바람 먹선 입자 (아트 §5): 풍속에 따라 개수·길이·속도·알파가 변하는 가는 스트로크.
    /// 풀 40개, 20%는 앞 레이어.
    /// </summary>
    public sealed class WindFieldView : MonoBehaviour
    {
        private const int PoolSize = 40;

        private sealed class Stroke
        {
            public SpriteRenderer sr;
            public bool active;
            public float life, maxLife, age, phase, baseY;
        }

        private Stroke[] pool;
        private float currentCount = 4f;
        private float wind;
        private Camera cam;
        private float groundTop = 0f;
        private float viewSign = 1f; // 클라이언트 미러 뷰일 때 −1

        public void Init(Camera camera, Transform parent, float groundTopY, float viewSign)
        {
            cam = camera;
            this.viewSign = viewSign;
            groundTop = groundTopY;
            pool = new Stroke[PoolSize];
            for (int i = 0; i < PoolSize; i++)
            {
                GameObject go = new GameObject("wind" + i);
                go.transform.SetParent(parent, false);
                SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = ProceduralSprites.Stroke();
                sr.sharedMaterial = ProceduralSprites.SpriteMaterial;
                sr.sortingOrder = (i % 5 == 0) ? ArtConstants.SortWindFront : ArtConstants.SortWindBack;
                sr.color = new Color(Palette.Ink.r, Palette.Ink.g, Palette.Ink.b, 0f);
                go.SetActive(false);
                pool[i] = new Stroke { sr = sr, active = false };
            }
        }

        public void SetWind(float w) { wind = w; }

        private void Update()
        {
            if (pool == null || cam == null) return;
            float dt = Time.deltaTime;
            float aw = Mathf.Abs(wind);
            float target = Mathf.Clamp(Mathf.Round(4f + 4f * aw), 4f, 32f);
            currentCount = Mathf.MoveTowards(currentCount, target, 1.5f * dt);
            int want = Mathf.RoundToInt(currentCount);

            float halfH = cam.orthographicSize, halfW = halfH * cam.aspect;
            float cx = cam.transform.position.x, cy = cam.transform.position.y;
            float speed = (1.5f + 1.2f * aw) * Mathf.Sign(wind == 0f ? 1f : wind) * viewSign;
            float len = 0.8f + 0.5f * aw;
            float alpha = 0.12f + 0.30f * Mathf.Min(aw / 7f, 1f);

            int activeCount = 0;
            for (int i = 0; i < PoolSize; i++)
            {
                Stroke s = pool[i];
                if (!s.active)
                {
                    if (activeCount < want && Random.value < 0.08f)
                    {
                        s.active = true; s.age = 0f; s.maxLife = Random.Range(2.5f, 4f); s.phase = Random.Range(0f, 6.28f);
                        s.baseY = Random.Range(groundTop + 2f, cy + halfH);
                        float sx = speed > 0f ? cx - halfW - len : cx + halfW + len;
                        if (Random.value < 0.5f) sx = Random.Range(cx - halfW, cx + halfW); // 초기엔 화면 안에도 분포
                        s.sr.transform.position = new Vector3(sx, s.baseY, 0f);
                        s.sr.gameObject.SetActive(true);
                    }
                    continue;
                }
                activeCount++;
                s.age += dt;
                if (s.age >= s.maxLife || activeCount > want + 4)
                {
                    s.active = false; s.sr.gameObject.SetActive(false); continue;
                }
                Vector3 p = s.sr.transform.position;
                p.x += speed * dt;
                p.y = s.baseY + 0.3f * Mathf.Sin(1.3f * s.age + s.phase);
                if (p.x > cx + halfW + len) p.x = cx - halfW - len;
                if (p.x < cx - halfW - len) p.x = cx + halfW + len;
                s.sr.transform.position = p;
                s.sr.transform.localScale = new Vector3(len, 0.05f / 0.0625f, 1f);
                float fade = Mathf.Min(1f, s.age / 0.3f, (s.maxLife - s.age) / 0.3f);
                float a = alpha * fade * (s.sr.sortingOrder == ArtConstants.SortWindFront ? 0.7f : 1f);
                s.sr.color = new Color(Palette.Ink.r, Palette.Ink.g, Palette.Ink.b, a);
            }
        }
    }
}
