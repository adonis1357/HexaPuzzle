using System.Collections;
using UnityEngine;
using Bow.Art;
using Bow.Core;

namespace Bow.Game
{
    /// <summary>
    /// 화살 뷰: 미리 계산된 ArrowFlight를 Time.deltaTime(스케일됨)으로 재생한다 → 히트스톱 시 함께 멈춤.
    /// 로컬 좌표 = 시뮬 좌표. 도착 시 OnArrived 1회 발생 후 꽂힘 → 페이드 → 파괴.
    /// </summary>
    public sealed class ArrowView : MonoBehaviour
    {
        public ArrowFlight Flight { get; private set; }
        public bool Arrived { get; private set; }
        public System.Action<ArrowView> OnArrived;

        private SpriteRenderer sr;
        private LineRenderer trail;
        private Vector3[] trailPts = new Vector3[12];
        private float elapsed;
        private float trailTimer;
        private AudioSource whistle;
        private Color teamColor;
        private bool perfect;

        public void Build(ArrowFlight flight, int teamId, bool isPerfect, float startElapsed, Transform trailParent)
        {
            Flight = flight; perfect = isPerfect;
            teamColor = Palette.Team(teamId);
            elapsed = Mathf.Max(0f, startElapsed);
            sr = gameObject.AddComponent<SpriteRenderer>();
            sr.sprite = ProceduralSprites.Arrow(teamId);
            sr.sortingOrder = ArtConstants.SortArrow;
            sr.sharedMaterial = ProceduralSprites.SpriteMaterial;

            GameObject tgo = new GameObject("trail");
            tgo.transform.SetParent(trailParent, false);
            trail = tgo.AddComponent<LineRenderer>();
            trail.useWorldSpace = false;
            trail.positionCount = trailPts.Length;
            trail.sharedMaterial = ProceduralSprites.SpriteMaterial;
            trail.sortingOrder = ArtConstants.SortTrail;
            trail.alignment = LineAlignment.TransformZ;
            trail.widthCurve = new AnimationCurve(new Keyframe(0f, 0.07f), new Keyframe(1f, 0f));
            Color tc = perfect ? Palette.WithAlpha(Palette.Gold, 0.7f) : Palette.WithAlpha(teamColor, 0.55f);
            Gradient g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(tc, 0f), new GradientColorKey(tc, 1f) }, new[] { new GradientAlphaKey(tc.a, 0f), new GradientAlphaKey(0f, 1f) });
            trail.colorGradient = g;
            Vec2 p0 = flight.PositionAt(elapsed);
            for (int i = 0; i < trailPts.Length; i++) trailPts[i] = new Vector3(p0.x, p0.y, 0f);
            trail.SetPositions(trailPts);

            Place(elapsed);
            whistle = Bow.Audio.BowAudio.I.StartLoop("arrow_whistle", 0.0f, 1f);
        }

        private void Place(float e)
        {
            Vec2 p = Flight.PositionAt(e);
            Vec2 d = Flight.VelocityDirAt(e);
            transform.localPosition = new Vector3(p.x, p.y, 0f);
            transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
        }

        private void Update()
        {
            if (Arrived) return;
            float dt = Time.deltaTime;
            elapsed += dt;
            if (elapsed >= Flight.flightTime)
            {
                Arrived = true;
                Place(Flight.flightTime);
                if (Flight.zone != HitZone.Out)
                    transform.localPosition = new Vector3(Flight.hitPoint.x, Flight.hitPoint.y, 0f);
                Bow.Audio.BowAudio.I.StopLoop(whistle, 0.05f);
                whistle = null;
                StartCoroutine(StuckRoutine());
                if (OnArrived != null) OnArrived(this);
                return;
            }
            Place(elapsed);

            // 트레일: 0.025s 간격 샘플
            trailTimer += dt;
            if (trailTimer >= 0.025f)
            {
                trailTimer = 0f;
                for (int i = trailPts.Length - 1; i > 0; i--) trailPts[i] = trailPts[i - 1];
                trailPts[0] = transform.localPosition;
                trail.SetPositions(trailPts);
            }

            // 휘파람: 속도에 따른 볼륨/피치 (도플러 근사)
            if (whistle != null)
            {
                Vec2 a = Flight.PositionAt(elapsed), b = Flight.PositionAt(Mathf.Min(elapsed + 0.05f, Flight.flightTime));
                float speed = (b - a).Length / 0.05f;
                whistle.volume = Mathf.Clamp01(speed / 22f) * 0.3f;
                whistle.pitch = Mathf.Clamp(0.85f + speed / 22f * 0.4f, 0.8f, 1.25f);
            }
        }

        private IEnumerator StuckRoutine()
        {
            // 착탄 흔들림 ±4° 0.3s
            Quaternion baseRot = transform.localRotation;
            float t = 0f;
            while (t < 0.30f)
            {
                t += Time.deltaTime;
                float a = 4f * Mathf.Exp(-t / 0.1f) * Mathf.Sin(t * 40f);
                transform.localRotation = baseRot * Quaternion.Euler(0f, 0f, a);
                yield return null;
            }
            transform.localRotation = baseRot;
            // 트레일 페이드 0.4s
            float tt = 0f;
            while (tt < ArtConstants.TrailLifetime && trail != null)
            {
                tt += Time.deltaTime;
                trail.widthMultiplier = 1f - tt / ArtConstants.TrailLifetime;
                yield return null;
            }
            if (trail != null) Destroy(trail.gameObject);
            if (Flight.zone == HitZone.Out) { Destroy(gameObject); yield break; }
            t = 0f;
            while (t < ArtConstants.ArrowStickDuration) { t += Time.deltaTime; yield return null; }
            t = 0f;
            while (t < 0.5f)
            {
                t += Time.deltaTime;
                sr.color = new Color(1f, 1f, 1f, 1f - t / 0.5f);
                yield return null;
            }
            Destroy(gameObject);
        }

        private void OnDestroy()
        {
            if (whistle != null) Destroy(whistle.gameObject);
            if (trail != null) Destroy(trail.gameObject);
        }
    }
}
