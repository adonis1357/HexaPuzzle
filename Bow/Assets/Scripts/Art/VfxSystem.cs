using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Bow.Art
{
    /// <summary>
    /// 월드 VFX 총괄: 카메라 흔들림(max 합성, 상한 28px), 히트스톱+슬로모(표시 계층 전용), 줌 펀치, 파티클 버스트.
    /// 카메라는 LateUpdate에서 base 위치/줌에 오프셋을 더해 배치한다 (HUD는 Overlay Canvas라 영향 없음).
    /// </summary>
    public sealed class VfxSystem : MonoBehaviour
    {
        private struct Shake { public float ampMeters; public float duration; public float t; }

        public Camera cam;
        public Transform effectParent;
        public System.Func<float, float> groundHeight;

        private readonly List<Shake> shakes = new List<Shake>();
        private Vector3 basePos;
        private float baseOrtho = 10f;
        private float zoomScale = 1f;
        private float ppm = 50f;
        private Coroutine hitStopRoutine;
        private Coroutine zoomRoutine;

        public float PixelsPerMeter { get { return ppm; } }

        public void SetCameraBase(Vector3 pos, float ortho)
        {
            basePos = pos; baseOrtho = ortho;
            if (cam != null)
            {
                cam.transform.position = pos;
                cam.orthographicSize = ortho;
                ppm = Screen.height / (2f * ortho);
            }
        }

        private void LateUpdate()
        {
            if (cam == null) return;
            float dt = Time.deltaTime;
            float maxAmp = 0f;
            for (int i = shakes.Count - 1; i >= 0; i--)
            {
                Shake s = shakes[i];
                s.t += dt;
                if (s.t >= s.duration) { shakes.RemoveAt(i); continue; }
                float k = 1f - s.t / s.duration;
                float a = s.ampMeters * k * k;
                if (a > maxAmp) maxAmp = a;
                shakes[i] = s;
            }
            float cap = 28f / ppm;
            if (maxAmp > cap) maxAmp = cap;
            Vector2 off = maxAmp > 0f ? Random.insideUnitCircle * maxAmp : Vector2.zero;
            cam.transform.position = basePos + new Vector3(off.x, off.y, 0f);
            cam.orthographicSize = baseOrtho * zoomScale;
        }

        /// <summary>화면 흔들림 (진폭 px, 아트 §5.1)</summary>
        public void ShakePx(float px, float duration)
        {
            if (px <= 0f || duration <= 0f) return;
            shakes.Add(new Shake { ampMeters = px / ppm, duration = duration, t = 0f });
        }

        /// <summary>히트스톱 + 슬로모 (표시 계층 전용: Time.timeScale). 경기 시계는 unscaled로 흐른다.</summary>
        public void HitStop(float stopDuration, float slowScale, float slowDuration)
        {
            if (hitStopRoutine != null) { StopCoroutine(hitStopRoutine); Time.timeScale = 1f; }
            hitStopRoutine = StartCoroutine(HitStopRoutine(stopDuration, slowScale, slowDuration));
        }

        private IEnumerator HitStopRoutine(float stop, float slowScale, float slowDur)
        {
            Time.timeScale = 0f;
            float t = 0f;
            while (t < stop) { t += Time.unscaledDeltaTime; yield return null; }
            if (slowDur > 0f)
            {
                Time.timeScale = slowScale;
                t = 0f;
                while (t < slowDur) { t += Time.unscaledDeltaTime; yield return null; }
            }
            Time.timeScale = 1f;
            hitStopRoutine = null;
        }

        /// <summary>줌 펀치 ×scale (0.10s 진입, 0.20s 복귀)</summary>
        public void ZoomPunch(float scale)
        {
            if (zoomRoutine != null) StopCoroutine(zoomRoutine);
            zoomRoutine = StartCoroutine(ZoomRoutine(scale));
        }

        private IEnumerator ZoomRoutine(float scale)
        {
            float t = 0f;
            while (t < ArtConstants.ZoomPunchIn)
            {
                t += Time.unscaledDeltaTime;
                zoomScale = Mathf.Lerp(1f, scale, Ease.OutQuart(t / ArtConstants.ZoomPunchIn));
                yield return null;
            }
            t = 0f;
            while (t < ArtConstants.ZoomPunchOut)
            {
                t += Time.unscaledDeltaTime;
                zoomScale = Mathf.Lerp(scale, 1f, Ease.OutBack(t / ArtConstants.ZoomPunchOut));
                yield return null;
            }
            zoomScale = 1f;
            zoomRoutine = null;
        }

        private void OnDisable() { Time.timeScale = 1f; }

        // ---------------------------------------------------------------
        // 파티클
        // ---------------------------------------------------------------

        public ParticleMote Spawn(Sprite sprite, Vector2 pos, Vector2 vel, Color color, float size, float life, int sortOrder)
        {
            GameObject go = new GameObject("fx");
            go.transform.SetParent(effectParent, false);
            go.transform.position = new Vector3(pos.x, pos.y, 0f);
            SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = color;
            sr.sortingOrder = sortOrder;
            sr.sharedMaterial = ProceduralSprites.SpriteMaterial;
            ParticleMote m = go.AddComponent<ParticleMote>();
            m.velocity = vel;
            m.life = life;
            m.startScale = size; m.endScale = size;
            m.groundHeight = groundHeight;
            m.Init(sr);
            return m;
        }

        private static Vector2 Cone(Vector2 dir, float halfAngleDeg, float speed)
        {
            float a = Random.Range(-halfAngleDeg, halfAngleDeg) * Mathf.Deg2Rad;
            float c = Mathf.Cos(a), s = Mathf.Sin(a);
            Vector2 d = new Vector2(dir.x * c - dir.y * s, dir.x * s + dir.y * c);
            return d.normalized * speed;
        }

        /// <summary>몸통 명중: 먹 점 6 + 작은 스플래시</summary>
        public void BodyHit(Vector2 pos, Vector2 arrowDir)
        {
            for (int i = 0; i < 6; i++)
            {
                ParticleMote m = Spawn(ProceduralSprites.Circle(), pos, Cone(arrowDir, 40f, Random.Range(3f, 6f)), Palette.Ink, Random.Range(0.05f, 0.10f), 0.5f, ArtConstants.SortVfx);
                m.gravity = 9.8f; m.endAlpha = 0f;
            }
            ParticleMote blob = Spawn(ProceduralSprites.InkBlob(), pos, Vector2.zero, Palette.WithAlpha(Palette.Ink, 0.8f), 0.1f, 0.45f, ArtConstants.SortVfx);
            blob.startScale = 0.1f; blob.endScale = 0.5f; blob.startAlpha = 0.8f; blob.endAlpha = 0f;
            blob.transform.Rotate(0f, 0f, Random.Range(0f, 360f));
        }

        /// <summary>머리 명중: 큰 블롭 1 + 물방울 14 (히트스톱/줌 펀치/흔들림은 호출자가 조합)</summary>
        public void HeadHit(Vector2 pos, Vector2 arrowDir)
        {
            ParticleMote blob = Spawn(ProceduralSprites.InkBlob(), pos, Vector2.zero, Palette.Ink, 0.3f, 0.7f, ArtConstants.SortVfx);
            blob.startScale = 0.3f; blob.endScale = 1.6f; blob.startAlpha = 0.9f; blob.endAlpha = 0f;
            blob.transform.Rotate(0f, 0f, Random.Range(0f, 360f));
            for (int i = 0; i < 14; i++)
            {
                ParticleMote m = Spawn(ProceduralSprites.Circle(), pos, Cone(arrowDir, 70f, Random.Range(4f, 9f)), Palette.Ink, Random.Range(0.06f, 0.18f), 0.6f, ArtConstants.SortVfx);
                m.gravity = 9.8f; m.endAlpha = 0f;
            }
        }

        /// <summary>지면 착탄: 흙 튐 8 + 먹 번짐 링</summary>
        public void GroundImpact(Vector2 pos)
        {
            for (int i = 0; i < 8; i++)
            {
                Color c = (i % 2 == 0) ? Palette.Ink : Palette.Grey;
                Sprite sp = (i % 2 == 0) ? ProceduralSprites.WhiteRect() : ProceduralSprites.Circle();
                ParticleMote m = Spawn(sp, pos, Cone(Vector2.up, 50f, Random.Range(2.5f, 5f)), c, Random.Range(0.04f, 0.10f), 0.5f, ArtConstants.SortVfx);
                m.gravity = 9.8f; m.endAlpha = 0f; m.angularSpeed = Random.Range(-360f, 360f);
            }
            ParticleMote ring = Spawn(ProceduralSprites.SoftCircle(), pos, Vector2.zero, Palette.WithAlpha(Palette.Ink, 0.5f), 0.2f, 0.35f, ArtConstants.SortVfx);
            ring.startScale = 0.2f; ring.endScale = 0.8f; ring.startAlpha = 0.5f; ring.endAlpha = 0f;
            ring.transform.localScale = new Vector3(0.2f, 0.04f, 1f);
            StartCoroutine(FlattenY(ring.transform, 0.2f, 0.35f));
        }

        private IEnumerator FlattenY(Transform tr, float yRatio, float dur)
        {
            float t = 0f;
            while (tr != null && t < dur)
            {
                t += Time.deltaTime;
                Vector3 s = tr.localScale; s.y = s.x * yRatio; tr.localScale = s;
                yield return null;
            }
        }

        /// <summary>발사 먼지 (발 위치, 조준 반대 방향)</summary>
        public void FireDust(Vector2 feetPos, Vector2 backDir, float power)
        {
            int n = power > 0.7f ? 8 : 5;
            for (int i = 0; i < n; i++)
            {
                Color c = Random.value < 0.7f ? Palette.PaperD : Palette.Grey;
                Vector2 v = backDir.normalized * Random.Range(1.5f, 3f) + Vector2.up * 0.6f;
                ParticleMote m = Spawn(ProceduralSprites.SoftCircle(), feetPos + new Vector2(Random.Range(-0.1f, 0.1f), 0.1f), v, c, 0.24f, 0.45f, ArtConstants.SortVfx);
                m.startScale = 0.24f; m.endScale = 0.6f; m.startAlpha = 0.5f; m.endAlpha = 0f;
            }
        }

        /// <summary>Perfect 금빛 링 (노크 위치)</summary>
        public void GoldRing(Vector2 pos)
        {
            ParticleMote ring = Spawn(ProceduralSprites.Ring(), pos, Vector2.zero, Palette.GoldL, 0.3f, 0.30f, ArtConstants.SortVfx + 1);
            // Ring 스프라이트는 ppu 100 → 2.56m 지름이므로 스케일 보정
            ring.startScale = 0.6f / 2.56f; ring.endScale = 2.8f / 2.56f; ring.startAlpha = 1f; ring.endAlpha = 0f;
            for (int i = 0; i < 4; i++)
            {
                ParticleMote m = Spawn(ProceduralSprites.Circle(), pos, Random.insideUnitCircle.normalized * Random.Range(2f, 4f), Palette.GoldL, 0.08f, 0.4f, ArtConstants.SortVfx + 1);
                m.endAlpha = 0f;
            }
        }

        /// <summary>사망 시 먹 웅덩이</summary>
        public void InkPuddle(Vector2 feetPos)
        {
            ParticleMote p = Spawn(ProceduralSprites.SoftCircle(), feetPos, Vector2.zero, Palette.WithAlpha(Palette.Ink, 0.4f), 0.2f, 6f, ArtConstants.SortGround + 1);
            p.startScale = 0.2f; p.endScale = 1.1f; p.startAlpha = 0.4f; p.endAlpha = 0.4f;
            StartCoroutine(FlattenY(p.transform, 0.16f, 6f));
        }

        public void CleanupEffects()
        {
            if (effectParent == null) return;
            for (int i = effectParent.childCount - 1; i >= 0; i--) Destroy(effectParent.GetChild(i).gameObject);
            shakes.Clear();
            zoomScale = 1f;
            Time.timeScale = 1f;
        }
    }
}
