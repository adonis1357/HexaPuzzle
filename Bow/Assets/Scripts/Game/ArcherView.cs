using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Bow.Art;
using Bow.Core;

namespace Bow.Game
{
    /// <summary>
    /// 궁수 실루엣 (아트 §3.1~3.2, §4). 루트 피벗 = 발 중앙. 로컬 좌표는 시뮬레이션 좌표(m)와 동일하며
    /// 전방은 루트 localScale.x(=facing)로 표현한다. 활/시위는 LineRenderer(로컬 공간).
    /// </summary>
    public sealed class ArcherView : MonoBehaviour
    {
        private static readonly Vector2 Shoulder = new Vector2(0.05f, 1.38f);
        private static readonly Vector2 Shoulder2 = new Vector2(-0.02f, 1.38f);

        public int teamId;
        public int facing = 1;
        public bool IsDead { get; private set; }

        private readonly List<SpriteRenderer> parts = new List<SpriteRenderer>();
        private readonly List<Color> partColors = new List<Color>();
        private Transform bodyGroup;        // 몸통+머리 (상체 기울임)
        private Transform bowArm, drawArm, bowGroup, grip;
        private LineRenderer bow, bowString;
        private SpriteRenderer shadow;

        // 캐릭터 꾸밈
        private CharacterDef def;
        private Transform headTr;
        private Color bodyC, bodyDark, bodyLight;
        private float bowR0 = 0.85f, bowR1 = 0.72f, bowWidth = 0.07f;
        private int bowPoints = 24;
        public CharacterDef Character { get { return def; } }

        private float aimAngle = 20f, targetAngle = 20f;
        private float draw = 0f, targetDraw = 0f;
        private float bodyTilt = 0f, bodyTiltTarget = 0f;
        private float bowKick = 0f;          // 발사 반동 회전(°)
        private float stringSnap = 0f;       // 시위 노크 변위(m)
        private float drawArmPush = 0f;
        private Vector2 shakeOffset;
        private float trembleAmp = 0f;
        private float flash = 0f;
        private Vector2 nockLocal;

        /// <summary>현재 노크(화살 출발) 위치 — 루트 로컬(m)</summary>
        public Vector2 NockLocal { get { return nockLocal; } }
        public float AimAngle { get { return aimAngle; } }

        public void Build(int team, int facingDir) { Build(team, facingDir, null); }

        /// <summary>골격 생성 + 캐릭터 꾸밈 적용. character가 null이면 기본 먹 궁수.</summary>
        public void Build(int team, int facingDir, CharacterDef character)
        {
            teamId = team; facing = facingDir;
            def = character ?? CharacterCatalog.DefaultDef();
            transform.localScale = new Vector3(facing, 1f, 1f);
            Color teamC = Palette.Team(team);
            bodyC = def.Body;
            bodyDark = Color.Lerp(bodyC, Color.black, 0.2f);
            bodyLight = Color.Lerp(bodyC, Palette.Paper, 0.25f);
            ApplyBowStyle();

            shadow = MakeSprite("shadow", ProceduralSprites.SoftCircle(), Palette.WithAlpha(Palette.Ink, 0.25f), new Vector2(0f, 0.02f), new Vector2(0.9f, 0.18f), ArtConstants.SortArcher - 1, false);

            MakePart("legFront", new Vector2(0.20f, 0.80f), new Vector2(0.10f, 0.80f), new Vector2(0f, -0.40f), bodyC, 6f);
            MakePart("legBack", new Vector2(0.20f, 0.80f), new Vector2(-0.10f, 0.80f), new Vector2(0f, -0.40f), bodyDark, -6f);

            bodyGroup = new GameObject("bodyGroup").transform;
            bodyGroup.SetParent(transform, false);
            bodyGroup.localPosition = new Vector3(0f, 0.80f, 0f);
            MakePart(bodyGroup, "torso", new Vector2(0.50f, 0.66f), new Vector2(0f, 0f), new Vector2(0f, 0.33f), bodyC, 0f);
            MakePart(bodyGroup, "belt", new Vector2(0.52f, 0.07f), new Vector2(0f, 0.30f), Vector2.zero, teamC, 0f);

            // 머리 (모양/배율은 캐릭터 정의)
            Sprite headSprite = ProceduralSprites.Circle();
            Vector2 headSize = new Vector2(0.42f, 0.42f);
            float headRot = 0f;
            switch (def.headShape)
            {
                case "oval": headSize = new Vector2(0.40f, 0.50f); break;
                case "square": headSprite = ProceduralSprites.WhiteRect(); headSize = new Vector2(0.40f, 0.40f); break;
                case "tri": headSprite = ProceduralSprites.Triangle(); headSize = new Vector2(0.52f, 0.50f); headRot = 90f; break;
            }
            headSize = Vector2.Scale(headSize, def.HeadScale);
            SpriteRenderer head = MakeSprite("head", headSprite, bodyC, new Vector2(0.03f, 0.92f), headSize, ArtConstants.SortArcher, true);
            head.transform.SetParent(bodyGroup, false);
            head.transform.localPosition = new Vector3(0.03f, 0.92f, 0f);
            head.transform.localRotation = Quaternion.Euler(0f, 0f, headRot);
            // 파트 앵커용 머리 트랜스폼 (회전/스케일 영향 없이)
            headTr = new GameObject("headAnchor").transform;
            headTr.SetParent(bodyGroup, false);
            headTr.localPosition = new Vector3(0.03f, 0.92f, 0f);
            MakePart(bodyGroup, "headband", new Vector2(0.44f * def.HeadScale.x, 0.05f), new Vector2(0.03f, 0.96f), Vector2.zero, teamC, 0f);

            // 활 그룹 (떨림 오프셋 적용용)
            bowGroup = new GameObject("bowGroup").transform;
            bowGroup.SetParent(transform, false);

            bowArm = MakePart(bowGroup, "bowArm", new Vector2(0.11f, 0.62f), Shoulder, new Vector2(0f, -0.31f), bodyC, 0f);
            drawArm = MakePart(bowGroup, "drawArm", new Vector2(0.11f, 1f), Shoulder2, new Vector2(0f, -0.5f), bodyLight, 0f);

            Color bowC = def.Resolve(def.bowColor, teamC);
            bow = MakeLine("bow", bowPoints, bowC, ArtConstants.SortBow);
            float k = bowWidth / 0.07f;
            AnimationCurve wc = new AnimationCurve(new Keyframe(0f, 0.03f * k), new Keyframe(0.5f, 0.07f * k), new Keyframe(1f, 0.03f * k));
            bow.widthCurve = wc; bow.widthMultiplier = 1f;
            bowString = MakeLine("string", 3, Palette.WithAlpha(Palette.Ink, 0.7f), ArtConstants.SortBow - 1);
            bowString.widthMultiplier = 0.04f;
            grip = MakePart(bowGroup, "grip", new Vector2(0.08f, 0.14f), Vector2.zero, Vector2.zero, teamC, 0f);
            GetSprite(grip).sortingOrder = ArtConstants.SortBow + 1;

            ApplyParts(teamC);
            UpdatePose();
        }

        /// <summary>캐릭터를 바꿔 다시 생성 (네트워크 Pick 수신 등)</summary>
        public void Rebuild(CharacterDef character)
        {
            StopAllCoroutines();
            for (int i = transform.childCount - 1; i >= 0; i--) Destroy(transform.GetChild(i).gameObject);
            parts.Clear(); partColors.Clear();
            shakeOffset = Vector2.zero; flash = 0f; bodyTilt = 0f; trembleAmp = 0f; stringSnap = 0f; bowKick = 0f;
            transform.localRotation = Quaternion.identity;
            IsDead = false;
            Build(teamId, facing, character);
        }

        private void ApplyBowStyle()
        {
            switch (def.bowStyle)
            {
                case "recurve": bowR0 = 0.65f; bowR1 = 0.58f; bowWidth = 0.09f; bowPoints = 24; break;
                case "longbow": bowR0 = 1.05f; bowR1 = 0.90f; bowWidth = 0.05f; bowPoints = 24; break;
                case "mech": bowR0 = 0.80f; bowR1 = 0.70f; bowWidth = 0.09f; bowPoints = 5; break;
                default: bowR0 = 0.85f; bowR1 = 0.72f; bowWidth = 0.07f; bowPoints = 24; break;
            }
        }

        /// <summary>DSL 파트 생성 (Docs/05_캐릭터_파트_DSL.md)</summary>
        private void ApplyParts(Color teamC)
        {
            if (def.parts == null) return;
            foreach (CharacterPart p in def.parts)
            {
                if (p == null) continue;
                MakeDecoration(p, teamC, false);
                if (p.mirrorX) MakeDecoration(p, teamC, true);
            }
        }

        private void MakeDecoration(CharacterPart p, Color teamC, bool mirrored)
        {
            Transform parent; Vector2 basePos; int baseOrder = ArtConstants.SortArcher;
            switch (p.anchor)
            {
                case "feet": parent = transform; basePos = Vector2.zero; break;
                case "belt": parent = transform; basePos = new Vector2(0f, 1.10f); break;
                case "back": parent = bodyGroup; basePos = new Vector2(-0.25f, 0.40f); break;
                case "head": parent = headTr; basePos = Vector2.zero; break;
                case "bow": parent = grip; basePos = Vector2.zero; baseOrder = ArtConstants.SortBow; break;
                default: parent = bodyGroup; basePos = new Vector2(0f, 0.33f); break; // torso
            }
            Sprite sprite;
            switch (p.kind)
            {
                case "circle": sprite = ProceduralSprites.Circle(); break;
                case "tri": sprite = ProceduralSprites.Triangle(); break;
                case "soft": sprite = ProceduralSprites.SoftCircle(); break;
                case "ring": sprite = ProceduralSprites.Ring(); break;
                default: sprite = ProceduralSprites.WhiteRect(); break;
            }
            float sx = mirrored ? -p.x : p.x;
            float rot = mirrored ? -p.rot : p.rot;
            GameObject go = new GameObject("part_" + (string.IsNullOrEmpty(p.name) ? p.kind : p.name));
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(basePos.x + sx, basePos.y + p.y, 0f);
            go.transform.localRotation = Quaternion.Euler(0f, 0f, rot);
            // Ring 스프라이트는 ppu 100 (지름 2.56m) → 보정
            float unit = p.kind == "ring" ? 1f / 2.56f : 1f;
            go.transform.localScale = new Vector3((mirrored ? -p.w : p.w) * unit, p.h * unit, 1f);
            SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            Color c = def.Resolve(p.color, teamC);
            c.a *= Mathf.Clamp01(p.alpha);
            sr.color = c;
            sr.sortingOrder = baseOrder + Mathf.Clamp(p.order, -3, 3);
            sr.sharedMaterial = ProceduralSprites.SpriteMaterial;
            parts.Add(sr); partColors.Add(c);
        }

        private SpriteRenderer MakeSprite(string name, Sprite sprite, Color color, Vector2 pos, Vector2 size, int order, bool tintable)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(pos.x, pos.y, 0f);
            go.transform.localScale = new Vector3(size.x, size.y, 1f);
            SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite; sr.color = color; sr.sortingOrder = order;
            sr.sharedMaterial = ProceduralSprites.SpriteMaterial;
            if (tintable) { parts.Add(sr); partColors.Add(color); }
            return sr;
        }

        private Transform MakePart(string name, Vector2 size, Vector2 pivotPos, Vector2 spriteOffset, Color color, float rotZ)
        {
            return MakePart(transform, name, size, pivotPos, spriteOffset, color, rotZ);
        }

        /// <summary>피벗 GO + 자식 스프라이트(오프셋)로 임의 피벗 사각 파트 생성</summary>
        private Transform MakePart(Transform parent, string name, Vector2 size, Vector2 pivotPos, Vector2 spriteOffset, Color color, float rotZ)
        {
            GameObject pivot = new GameObject(name);
            pivot.transform.SetParent(parent, false);
            pivot.transform.localPosition = new Vector3(pivotPos.x, pivotPos.y, 0f);
            pivot.transform.localRotation = Quaternion.Euler(0f, 0f, rotZ);
            GameObject go = new GameObject("sprite");
            go.transform.SetParent(pivot.transform, false);
            go.transform.localPosition = new Vector3(spriteOffset.x, spriteOffset.y, 0f);
            go.transform.localScale = new Vector3(size.x, size.y, 1f);
            SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = ProceduralSprites.WhiteRect(); sr.color = color; sr.sortingOrder = ArtConstants.SortArcher;
            sr.sharedMaterial = ProceduralSprites.SpriteMaterial;
            parts.Add(sr); partColors.Add(color);
            return pivot.transform;
        }

        private static SpriteRenderer GetSprite(Transform pivot) { return pivot.GetChild(0).GetComponent<SpriteRenderer>(); }

        private LineRenderer MakeLine(string name, int count, Color color, int order)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(bowGroup, false);
            LineRenderer lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = false;
            lr.positionCount = count;
            lr.sharedMaterial = ProceduralSprites.SpriteMaterial;
            lr.startColor = color; lr.endColor = color;
            lr.numCapVertices = 4; lr.numCornerVertices = 2;
            lr.sortingOrder = order;
            lr.alignment = LineAlignment.TransformZ;
            return lr;
        }

        // ---------------------------------------------------------------
        // 조준/발사/피격 API
        // ---------------------------------------------------------------

        /// <summary>조준 각도(전방 기준 °)와 당김(0~1) 목표 설정</summary>
        public void SetAim(float angleDeg, float power)
        {
            targetAngle = angleDeg; targetDraw = Mathf.Clamp01(power);
            bodyTiltTarget = -4f * targetDraw;
        }

        public void SetIdle() { SetAim(20f, 0f); trembleAmp = 0f; }

        /// <summary>홀드 떨림 (추 미터 구간): 경과 시간에 따라 0.01→0.03m</summary>
        public void SetTremble(float elapsed) { trembleAmp = Mathf.Lerp(0.01f, 0.03f, Mathf.Clamp01(elapsed / 2.5f)); }

        public void Fire()
        {
            trembleAmp = 0f;
            StartCoroutine(FireRoutine());
        }


        private IEnumerator FireRoutine()
        {
            float nock = 0.15f + 0.60f * draw;
            targetDraw = 0f;
            float t = 0f;
            while (t < ArtConstants.RecoilDuration)
            {
                t += Time.deltaTime;
                stringSnap = t < 0.25f ? -nock * Mathf.Exp(-t / ArtConstants.StringSnapDecay) * Mathf.Cos(2f * Mathf.PI * ArtConstants.StringSnapFreq * t) : 0f;
                bowKick = t < ArtConstants.BowKickDuration ? -6f * (1f - Ease.OutBack(t / ArtConstants.BowKickDuration)) : 0f;
                bodyTilt = t < 0.20f ? -3f * (1f - Ease.OutCubic(t / 0.20f)) : 0f;
                drawArmPush = t < 0.12f ? 0.08f * Mathf.Sin(t / 0.12f * Mathf.PI) : 0f;
                yield return null;
            }
            stringSnap = 0f; bowKick = 0f; drawArmPush = 0f;
            bodyTiltTarget = 0f;
        }

        /// <summary>피격 연출. arrowDirSign: 화살 진행 방향(시뮬 x 부호)</summary>
        public void Hit(HitZone zone, int arrowDirSign)
        {
            StartCoroutine(HitRoutine(zone == HitZone.Head, arrowDirSign));
        }

        private IEnumerator HitRoutine(bool head, int dirSign)
        {
            float amp = head ? 0.20f : 0.12f;
            float dur = head ? ArtConstants.HitShakeHead : ArtConstants.HitShakeBody;
            float decay = head ? 0.12f : 0.09f;
            float flashDur = head ? ArtConstants.HitFlashHead : ArtConstants.HitFlashBody;
            float tilt = head ? 11f : 6f;
            float t = 0f;
            while (t < dur)
            {
                t += Time.deltaTime;
                // 흔들림: 화살 반대 방향으로 밀림 (루트 로컬이므로 facing으로 나눔)
                float sx = amp * Mathf.Exp(-t / decay) * Mathf.Cos(2f * Mathf.PI * 22f * t);
                shakeOffset = new Vector2(sx * dirSign * facing, 0f);
                float ft = t / flashDur;
                flash = ft < 1f ? 1f - Ease.OutCubic(ft) : (t < flashDur + 0.06f ? 0.5f : 0f);
                bodyTilt = tilt * dirSign * facing * (1f - Ease.OutElastic(t / (head ? 0.40f : 0.25f)));
                yield return null;
            }
            shakeOffset = Vector2.zero; flash = 0f; bodyTilt = 0f;
        }

        /// <summary>사망: 화살 진행 방향으로 쓰러짐 (1.4s)</summary>
        public void Die(int arrowDirSign, System.Action<Vector2> onLanded)
        {
            if (IsDead) return;
            IsDead = true;
            StopAllCoroutines();
            shakeOffset = Vector2.zero; bodyTilt = 0f; flash = 0f; trembleAmp = 0f;
            StartCoroutine(DieRoutine(arrowDirSign, onLanded));
        }

        private IEnumerator DieRoutine(int dirSign, System.Action<Vector2> onLanded)
        {
            // 루트 로컬 회전: 시뮬 +x 방향으로 넘어짐 = 시계 방향(−z). facing과 무관하게 시뮬 기준으로 계산.
            float sign = -dirSign;
            float t = 0f;
            Color grey = Palette.Grey;
            while (t < ArtConstants.DeathFallDuration)
            {
                t += Time.deltaTime;
                float a = 88f * Ease.InQuad(t / ArtConstants.DeathFallDuration);
                transform.localRotation = Quaternion.Euler(0f, 0f, a * sign);
                float g = Mathf.Clamp01(t / ArtConstants.DeathFadeDuration);
                for (int i = 0; i < parts.Count; i++) parts[i].color = Color.Lerp(partColors[i], grey, g);
                yield return null;
            }
            if (onLanded != null) onLanded(new Vector2(transform.localPosition.x, transform.localPosition.y));
            t = 0f;
            while (t < ArtConstants.DeathBounceDuration)
            {
                t += Time.deltaTime;
                float u = t / ArtConstants.DeathBounceDuration;
                float a = 88f - 4f * Mathf.Sin(u * Mathf.PI);
                transform.localRotation = Quaternion.Euler(0f, 0f, a * sign);
                yield return null;
            }
            transform.localRotation = Quaternion.Euler(0f, 0f, 88f * sign);
            for (int i = 0; i < parts.Count; i++) parts[i].color = grey;
        }

        // ---------------------------------------------------------------
        // 매 프레임 포즈 갱신
        // ---------------------------------------------------------------
        private void Update()
        {
            if (IsDead) return;
            float dt = Time.deltaTime;
            aimAngle += (targetAngle - aimAngle) * Ease.Follow(ArtConstants.DrawFollowRate, dt);
            draw += (targetDraw - draw) * Ease.Follow(ArtConstants.DrawFollowRate, dt);
            if (bodyTiltTarget != 0f || Mathf.Abs(bodyTilt) > 0.01f)
                bodyTilt += (bodyTiltTarget - bodyTilt) * Ease.Follow(12f, dt);
            UpdatePose();
        }

        private void UpdatePose()
        {
            float rad = (aimAngle + bowKick) * Mathf.Deg2Rad;
            Vector2 dir = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
            Vector2 tremble = trembleAmp > 0f ? new Vector2(Mathf.Sin(Time.time * 14f * 2f * Mathf.PI) * trembleAmp * 0.3f, Mathf.Sin(Time.time * 14f * 2f * Mathf.PI + 1f) * trembleAmp) : Vector2.zero;
            bowGroup.localPosition = new Vector3(tremble.x, tremble.y, 0f);

            // 활 든 팔
            bowArm.localRotation = Quaternion.Euler(0f, 0f, aimAngle + bowKick + 90f);
            Vector2 G = Shoulder + dir * 0.62f;
            float R = Mathf.Lerp(bowR0, bowR1, draw);
            float alpha = Mathf.Lerp(55f, 70f, draw) * Mathf.Deg2Rad;
            Vector2 C = G - dir * R;
            int n = bowPoints;
            for (int i = 0; i < n; i++)
            {
                float th = Mathf.Lerp(-alpha, alpha, i / (float)(n - 1));
                float c = Mathf.Cos(th), s = Mathf.Sin(th);
                Vector2 rd = new Vector2(dir.x * c - dir.y * s, dir.x * s + dir.y * c);
                Vector2 p = C + rd * R;
                bow.SetPosition(i, new Vector3(p.x, p.y, 0f));
            }
            Vector3 tipA = bow.GetPosition(0), tipB = bow.GetPosition(n - 1);
            Vector2 nock = G - dir * (0.15f + 0.60f * draw + stringSnap);
            nockLocal = nock;
            bowString.SetPosition(0, tipA);
            bowString.SetPosition(1, new Vector3(nock.x, nock.y, 0f));
            bowString.SetPosition(2, tipB);
            grip.localPosition = new Vector3(G.x, G.y, 0f);
            grip.localRotation = Quaternion.Euler(0f, 0f, aimAngle + bowKick);

            // 시위 당기는 팔: 어깨2 → 노크(손)
            Vector2 hand = nock - dir * drawArmPush;
            Vector2 d2 = hand - Shoulder2;
            float len = Mathf.Max(0.30f, d2.magnitude);
            drawArm.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d2.y, d2.x) * Mathf.Rad2Deg + 90f);
            drawArm.GetChild(0).localScale = new Vector3(0.11f, len, 1f);
            drawArm.GetChild(0).localPosition = new Vector3(0f, -len * 0.5f, 0f);

            // 상체 기울임 + 피격 흔들림
            bodyGroup.localRotation = Quaternion.Euler(0f, 0f, bodyTilt);
            bodyGroup.localPosition = new Vector3(shakeOffset.x, 0.80f + shakeOffset.y, 0f);

            // 백색 플래시
            if (flash > 0f) for (int i = 0; i < parts.Count; i++) parts[i].color = Color.Lerp(partColors[i], Color.white, flash);
            else for (int i = 0; i < parts.Count; i++) if (parts[i].color != partColors[i]) parts[i].color = partColors[i];
        }
    }
}
