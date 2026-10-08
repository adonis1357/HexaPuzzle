using UnityEngine;

namespace Bow.Art
{
    /// <summary>
    /// 단순 파티클 (SpriteRenderer 1장). 속도/중력/수명/페이드/스케일 변화를 Time.deltaTime(스케일됨)으로 갱신하므로
    /// 히트스톱(Time.timeScale) 동안 함께 멈춘다. 수명이 끝나면 Destroy.
    /// </summary>
    public sealed class ParticleMote : MonoBehaviour
    {
        public Vector2 velocity;
        public float gravity = 0f;
        public float life = 0.5f;
        public float startAlpha = 1f;
        public float endAlpha = 0f;
        public float startScale = 0.1f;
        public float endScale = 0.1f;
        public float angularSpeed = 0f;
        public float drag = 0f;
        public bool stopOnGround = false;
        public System.Func<float, float> groundHeight;

        private float age;
        private SpriteRenderer sr;
        private Color baseColor;
        private bool stopped;

        public void Init(SpriteRenderer renderer)
        {
            sr = renderer;
            baseColor = sr.color;
            age = 0f;
            Apply();
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            age += dt;
            if (age >= life) { Destroy(gameObject); return; }
            if (!stopped)
            {
                velocity.y -= gravity * dt;
                if (drag > 0f) velocity *= Mathf.Max(0f, 1f - drag * dt);
                Vector3 p = transform.position;
                p.x += velocity.x * dt; p.y += velocity.y * dt;
                if (stopOnGround && groundHeight != null && p.y <= groundHeight(p.x))
                {
                    p.y = groundHeight(p.x); stopped = true;
                }
                transform.position = p;
                if (angularSpeed != 0f) transform.Rotate(0f, 0f, angularSpeed * dt);
            }
            Apply();
        }

        private void Apply()
        {
            float u = Mathf.Clamp01(age / life);
            float e = Ease.OutCubic(u);
            float s = Mathf.Lerp(startScale, endScale, e);
            transform.localScale = new Vector3(s, s, 1f);
            Color c = baseColor;
            c.a = Mathf.Lerp(startAlpha, endAlpha, u);
            sr.color = c;
        }
    }
}
