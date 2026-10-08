using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Bow.Art;
using Bow.Audio;
using Bow.Core;
using Bow.Net;
using Bow.UI;

namespace Bow.Game
{
    /// <summary>
    /// 한 경기의 오케스트레이터. 월드 생성, 입력→사격, 네트워크 메시지, 봇, 판정/이펙트, HUD, 종료 처리.
    /// 시뮬 좌표(m)는 worldRoot 로컬 좌표와 같고, 클라이언트(id 1)는 worldRoot.scale.x = −1로 미러링해
    /// 항상 자기 궁수가 화면 왼쪽에 보이게 한다. 경기 시계는 unscaled(히트스톱 영향 없음).
    /// </summary>
    public sealed class MatchController : MonoBehaviour
    {
        private const float CountdownSeconds = 3f;

        private DuelConfig cfg;
        private BowBootstrap app;
        private IMatchTransport transport;
        private BotProfile botProfile;
        private bool isBotMatch;
        private int localId, remoteId;
        private float viewSign = 1f;

        private MatchSetup setup;
        private WindModel wind;
        private DuelState state;
        private BreathSystem localBreath;
        private AccuracyMeter meter;
        private BotController bot;

        private Transform worldRoot, fxRoot;
        private Camera cam;
        private VfxSystem vfx;
        private WindFieldView windField;
        private readonly ArcherView[] archers = new ArcherView[2];
        private readonly List<ArrowView> arrows = new List<ArrowView>();
        private SpriteRenderer[] previewDots;
        private HudView hud;
        private ResultView result;
        private AimInput input;

        private float matchTime = -CountdownSeconds;
        private bool playing, ended, worldBuilt;
        private int lastCountdownShown = -1;
        private float pingTimer;

        // 조준 상태
        private bool aiming;
        private float aimAngle, aimPower;
        private bool metering;
        private float meterAngle, meterPower;
        private AudioSource drawLoop, tensionLoop;

        // 통계
        private int shotsFired, shotsHit, headHits;
        private float maxEff;

        public bool IsRunning { get { return worldBuilt && !ended; } }

        /// <summary>자동 테스트: 장전될 때마다 임의 각도/힘/오차로 사격 (PlayTestRunner용)</summary>
        public bool autoTest;

        // ---------------------------------------------------------------
        // 시작
        // ---------------------------------------------------------------
        public void Begin(BowBootstrap application, Camera camera, IMatchTransport tr, BotProfile botProf, int seed)
        {
            app = application; cam = camera; transport = tr; botProfile = botProf;
            cfg = new DuelConfig();
            isBotMatch = botProf != null;
            localId = transport.IsHost ? 0 : 1;
            remoteId = 1 - localId;
            viewSign = localId == 0 ? 1f : -1f;

            setup = new MatchSetup(seed, cfg);
            wind = new WindModel(seed, cfg);
            state = new DuelState(cfg);
            meter = new AccuracyMeter(cfg);
            if (isBotMatch) bot = new BotController(remoteId, botProfile, cfg, setup, wind, seed, 0f);

            transport.OnMessage += HandleMessage;
            transport.OnDisconnected += HandleDisconnect;
            if (transport.IsHost && !isBotMatch) transport.Send(NetMessage.Start(seed, -CountdownSeconds));

            BuildWorld();
            BuildUi();
            matchTime = -CountdownSeconds;
            BowAudio.I.StartWind();
            BowAudio.I.PlayBgm("bgm_duel");
            Debug.Log("[활] 경기 시작 seed=" + seed + " 거리=" + setup.Distance.ToString("F1") + "m 높이차=" + setup.HeightDiff.ToString("F2") + "m " + transport.Status);
        }

        private void BuildWorld()
        {
            worldRoot = new GameObject("World").transform;
            worldRoot.SetParent(transform, false);
            worldRoot.localScale = new Vector3(viewSign, 1f, 1f);
            fxRoot = new GameObject("FX").transform;
            fxRoot.SetParent(transform, false);

            // 카메라 프레이밍 (아트 §2.1)
            float halfW = (setup.Distance + 4f) * 0.5f;
            float ortho = Mathf.Max(halfW / cam.aspect, 9f);
            float camY = ortho * (1f - 2f * 700f / 1920f);
            vfx = gameObject.AddComponent<VfxSystem>();
            vfx.cam = cam; vfx.effectParent = fxRoot;
            vfx.groundHeight = (x) => setup.GroundHeight(x * viewSign);
            vfx.SetCameraBase(new Vector3(0f, camY, -10f), ortho);
            app.ConfigureBackdrop(ortho);

            // 지면
            GameObject g = new GameObject("Ground");
            g.transform.SetParent(worldRoot, false);
            g.transform.localPosition = new Vector3(0f, 2f, 0f);
            SpriteRenderer gsr = g.AddComponent<SpriteRenderer>();
            gsr.sprite = ProceduralSprites.Ground(setup);
            gsr.sortingOrder = ArtConstants.SortGround;
            gsr.sharedMaterial = ProceduralSprites.SpriteMaterial;

            // 장애물: 먹 기둥 (가운데, 직선 사격 차단)
            BuildObstacle();

            // 궁수
            for (int i = 0; i < 2; i++)
            {
                GameObject a = new GameObject("Archer" + i);
                a.transform.SetParent(worldRoot, false);
                Vec2 f = setup.Feet(i);
                a.transform.localPosition = new Vector3(f.x, f.y, 0f);
                archers[i] = a.AddComponent<ArcherView>();
                archers[i].Build(i, MatchSetup.Facing(i));
                archers[i].SetIdle();
            }

            // 바람 입자
            GameObject wf = new GameObject("WindField");
            wf.transform.SetParent(fxRoot, false);
            windField = wf.AddComponent<WindFieldView>();
            windField.Init(cam, wf.transform, Mathf.Max(setup.Feet(0).y, setup.Feet(1).y), viewSign);

            // 조준 가이드 점 3개
            previewDots = new SpriteRenderer[3];
            float[] alphas = { 0.9f, 0.6f, 0.35f };
            for (int i = 0; i < 3; i++)
            {
                GameObject d = new GameObject("guide" + i);
                d.transform.SetParent(worldRoot, false);
                previewDots[i] = d.AddComponent<SpriteRenderer>();
                previewDots[i].sprite = ProceduralSprites.Circle();
                previewDots[i].color = Palette.WithAlpha(Palette.Team(localId), alphas[i]);
                previewDots[i].sortingOrder = ArtConstants.SortVfx;
                previewDots[i].sharedMaterial = ProceduralSprites.SpriteMaterial;
                float size = 14f / vfx.PixelsPerMeter;
                d.transform.localScale = new Vector3(size, size, 1f);
                d.SetActive(false);
            }
            worldBuilt = true;
        }

        /// <summary>먹 기둥 장애물 비주얼: 본체 + 붓결 줄무늬 + 꼭대기 풀</summary>
        private void BuildObstacle()
        {
            GameObject root = new GameObject("Obstacle");
            root.transform.SetParent(worldRoot, false);
            float cx = setup.ObstacleX, hw = setup.ObstacleHalfWidth;
            float bottom = setup.ObstacleBottom, top = setup.ObstacleTop, h = top - bottom;
            root.transform.localPosition = new Vector3(cx, bottom, 0f);
            System.Random rng = new System.Random(setup.Seed ^ 0x0B5);

            MakeRect(root.transform, "body", new Vector2(0f, h * 0.5f), new Vector2(hw * 2f, h), Palette.Ink, ArtConstants.SortArcher - 2);
            // 울퉁불퉁한 가장자리: 좌우로 삐져나온 작은 덩어리들
            for (int i = 0; i < 6; i++)
            {
                float y = (float)rng.NextDouble() * h * 0.9f + h * 0.05f;
                float side = i % 2 == 0 ? -1f : 1f;
                float w = 0.15f + (float)rng.NextDouble() * 0.2f;
                MakeRect(root.transform, "lump" + i, new Vector2(side * (hw + w * 0.3f), y), new Vector2(w, 0.25f + (float)rng.NextDouble() * 0.4f), Palette.Ink, ArtConstants.SortArcher - 2);
            }
            // 붓결(연먹 세로 줄)
            for (int i = 0; i < 3; i++)
            {
                float x = -hw * 0.6f + i * hw * 0.6f;
                MakeRect(root.transform, "stroke" + i, new Vector2(x, h * 0.5f + (float)rng.NextDouble() * 0.3f), new Vector2(0.06f, h * (0.5f + (float)rng.NextDouble() * 0.4f)), Palette.WithAlpha(Palette.InkL, 0.8f), ArtConstants.SortArcher - 1);
            }
            // 꼭대기 풀 터치
            for (int i = 0; i < 5; i++)
            {
                float x = -hw + (float)rng.NextDouble() * hw * 2f;
                MakeRect(root.transform, "grass" + i, new Vector2(x, h + 0.1f), new Vector2(0.05f, 0.2f + (float)rng.NextDouble() * 0.2f), Palette.Ink, ArtConstants.SortArcher - 1)
                    .transform.localRotation = Quaternion.Euler(0f, 0f, (float)(rng.NextDouble() - 0.5) * 30f);
            }
            // 발밑 그림자
            MakeRect(root.transform, "shadow", new Vector2(0f, 0.3f), new Vector2(hw * 2f + 0.6f, 0.12f), Palette.WithAlpha(Palette.Ink, 0.25f), ArtConstants.SortGround + 1);
        }

        private static SpriteRenderer MakeRect(Transform parent, string name, Vector2 pos, Vector2 size, Color color, int order)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(pos.x, pos.y, 0f);
            go.transform.localScale = new Vector3(size.x, size.y, 1f);
            SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = ProceduralSprites.WhiteRect();
            sr.color = color;
            sr.sortingOrder = order;
            sr.sharedMaterial = ProceduralSprites.SpriteMaterial;
            return sr;
        }

        private void BuildUi()
        {
            hud = new GameObject("HudView").AddComponent<HudView>();
            hud.transform.SetParent(transform, false);
            hud.Build(cam);
            hud.SetNames("나", isBotMatch ? "봇 (" + botProfile.name + ")" : "상대");
            hud.SetStatus(transport.Status + " · 사거리 " + setup.Distance.ToString("F0") + "m");
            hud.OnExitPressed = () => app.ReturnToLobby();
            hud.SetHp(0, cfg.maxHp, cfg.maxHp, false);
            hud.SetHp(1, cfg.maxHp, cfg.maxHp, false);
            hud.SetTimer(cfg.matchDuration);

            result = new GameObject("ResultView").AddComponent<ResultView>();
            result.transform.SetParent(transform, false);
            result.Build();
            result.OnRetry = () => app.Retry();
            result.OnLobby = () => app.ReturnToLobby();

            input = gameObject.AddComponent<AimInput>();
            input.minAngle = cfg.minAimAngle; input.maxAngle = cfg.maxAimAngle;
            input.SetMode(AimInput.Mode.Disabled);
            input.OnDragStart = OnDragStart;
            input.OnDragUpdate = OnDragUpdate;
            input.OnDragCancel = OnDragCancel;
            input.OnDragRelease = OnDragRelease;
            input.OnTap = OnTap;
        }

        // ---------------------------------------------------------------
        // 좌표 변환
        // ---------------------------------------------------------------
        private Vector3 SimToWorld(Vec2 v) { return worldRoot.TransformPoint(new Vector3(v.x, v.y, 0f)); }
        private Vector2 SimToWorld2(Vec2 v) { Vector3 w = SimToWorld(v); return new Vector2(w.x, w.y); }
        private Vector2 SimDirToWorld(Vec2 d) { return new Vector2(d.x * viewSign, d.y); }

        private Vector3 LocalNockWorld()
        {
            Vector2 n = archers[localId].NockLocal;
            Vec2 f = setup.Feet(localId);
            return SimToWorld(new Vec2(f.x + n.x * MatchSetup.Facing(localId), f.y + n.y));
        }

        // ---------------------------------------------------------------
        // 매 프레임
        // ---------------------------------------------------------------
        private void Update()
        {
            if (!worldBuilt) return;
            transport.Poll();
            float dt = Time.unscaledDeltaTime;
            matchTime += dt;

            float wt = Mathf.Max(0f, matchTime);
            float w = wind.GetWind(wt);
            windField.SetWind(w);
            hud.SetWind(w * viewSign, wind.GetNextWind(wt) * viewSign, wind.SecondsUntilChange(wt), wind.IsTransitioning(wt));
            BowAudio.I.SetWind(w);

            // 카운트다운
            if (!playing)
            {
                int n = Mathf.CeilToInt(-matchTime);
                if (matchTime < 0f)
                {
                    hud.SetCountdown(n.ToString(), true);
                    if (n != lastCountdownShown)
                    {
                        lastCountdownShown = n;
                        float pitch = n == 3 ? 0.94f : n == 2 ? 1f : 1.06f;
                        BowAudio.I.Play("countdown_tick", 0.7f, pitch, pitch);
                    }
                }
                else
                {
                    StartPlay();
                }
                return;
            }

            if (ended) return;

            // 클라이언트 시계 동기화 핑
            if (!transport.IsHost)
            {
                pingTimer += dt;
                if (pingTimer >= 1f) { pingTimer = 0f; transport.Send(NetMessage.Ping(Time.realtimeSinceStartup)); }
            }

            state.SetMatchTime(matchTime);
            hud.SetTimer(state.TimeRemaining);
            if (matchTime < 0.6f) hud.SetCountdown("시작!", true); else hud.SetCountdown("", false);

            // 호흡 HUD
            bool ready = localBreath.IsReady(matchTime);
            float eff = localBreath.CurrentEfficiency(matchTime);
            hud.SetBreath(localBreath.CooldownRemaining(matchTime), localBreath.CooldownProgress(matchTime), ready,
                          localBreath.Overhold(matchTime), eff, localBreath.PreviewNextDelay(matchTime), cfg.breathPeak);

            // 추 미터
            if (metering)
            {
                float p = meter.Position(matchTime);
                hud.SetMeter(true, p, meter.IsPerfect(p), meter.Elapsed(matchTime) / cfg.meterTimeout, eff);
                archers[localId].SetTremble(meter.Elapsed(matchTime));
                // 틱 사운드: 중앙 근접 시 피치 상승
                float tickPitch = 1f + 0.5f * (1f - Mathf.Abs(p));
                BowAudio.I.Play("meter_tick", 0.4f, tickPitch, tickPitch, 0.09f);
                if (meter.IsTimedOut(matchTime)) FireLocal(p, true);
            }
            else hud.SetMeter(false, 0f, false, 0f, eff);

            // 자동 테스트 사격
            if (autoTest && state.Phase == MatchPhase.Playing && !metering && !aiming && !archers[localId].IsDead
                && localBreath.IsReady(matchTime) && localBreath.Overhold(matchTime) >= 1f)
            {
                meterAngle = Random.Range(30f, 60f);
                meterPower = Random.Range(0.35f, 0.8f);
                archers[localId].SetAim(meterAngle, meterPower);
                FireLocal(Random.Range(-0.6f, 0.6f), false);
            }

            // 봇
            if (bot != null && state.Phase == MatchPhase.Playing)
            {
                ShotParams s;
                if (bot.HasPlan) archers[remoteId].SetAim(bot.Plan.angleDeg, bot.Plan.power);
                if (bot.Tick(matchTime, out s)) { archers[remoteId].Fire(); SpawnArrow(s); }
                else if (!bot.HasPlan && !archers[remoteId].IsDead) archers[remoteId].SetIdle();
            }

            // 시간 종료 (호스트 권위, 클라이언트도 로컬 판정 후 End 메시지로 보정)
            if (state.Phase == MatchPhase.Ended) EndMatch();
        }

        private void StartPlay()
        {
            playing = true;
            state.BeginPlay();
            localBreath = new BreathSystem(cfg, 0f);
            hud.SetCountdown("시작!", true);
            BowAudio.I.Play("countdown_go", 0.9f);
            input.SetMode(AimInput.Mode.Idle);
        }

        // ---------------------------------------------------------------
        // 입력 → 조준/사격
        // ---------------------------------------------------------------
        private void OnDragStart()
        {
            if (archers[localId].IsDead || ended) { input.SetMode(AimInput.Mode.Idle); return; }
            aiming = true;
            BowAudio.I.Play("bow_draw_start", 0.55f, 0.96f, 1.04f);
            drawLoop = BowAudio.I.StartLoop("bow_draw_loop", 0.2f, 0.9f);
            tensionLoop = BowAudio.I.StartLoop("string_tension_loop", 0.12f, 1f);
            for (int i = 0; i < 3; i++) previewDots[i].gameObject.SetActive(true);
        }

        private void OnDragUpdate(float angle, float power)
        {
            if (!aiming) return;
            aimAngle = angle; aimPower = power;
            archers[localId].SetAim(angle, power);
            hud.SetAimInfo(true, SimToWorld(new Vec2(setup.Feet(localId).x, setup.Feet(localId).y + 1.91f + 1.2f)), angle, power);
            Vec2[] pts = ArrowSimulator.PreviewNoWind(localId, angle, power, setup, cfg, 0.5f, 3);
            for (int i = 0; i < 3; i++) previewDots[i].transform.localPosition = new Vector3(pts[i].x, pts[i].y, 0f);
            if (drawLoop != null) { drawLoop.volume = 0.2f + 0.3f * power; drawLoop.pitch = 0.9f + 0.35f * power; }
            if (tensionLoop != null) { tensionLoop.volume = 0.12f + 0.25f * power; tensionLoop.pitch = 1f + 1f * power; }
        }

        private void StopDrawLoops()
        {
            BowAudio.I.StopLoop(drawLoop); drawLoop = null;
            BowAudio.I.StopLoop(tensionLoop); tensionLoop = null;
        }

        private void OnDragCancel()
        {
            aiming = false;
            StopDrawLoops();
            hud.SetAimInfo(false, Vector3.zero, 0f, 0f);
            for (int i = 0; i < 3; i++) previewDots[i].gameObject.SetActive(false);
            archers[localId].SetIdle();
        }

        private void OnDragRelease(float angle, float power)
        {
            aiming = false;
            StopDrawLoops();
            hud.SetAimInfo(false, Vector3.zero, 0f, 0f);
            for (int i = 0; i < 3; i++) previewDots[i].gameObject.SetActive(false);
            if (!localBreath.IsReady(matchTime))
            {
                hud.ShowMessage("장전 중 " + localBreath.CooldownRemaining(matchTime).ToString("F1") + "s", Palette.Grey, 0.5f, 56);
                archers[localId].SetIdle();
                input.SetMode(AimInput.Mode.Idle);
                return;
            }
            metering = true;
            meterAngle = angle; meterPower = power;
            meter.Start(matchTime, localBreath.MeterPeriod(matchTime));
        }

        private void OnTap()
        {
            if (!metering) return;
            FireLocal(meter.Position(matchTime), false);
        }

        private void FireLocal(float p, bool auto)
        {
            metering = false;
            meter.Stop();
            input.SetMode(AimInput.Mode.Idle);
            if (auto && Mathf.Abs(p) < cfg.perfectThreshold) p = cfg.perfectThreshold * (p < 0f ? -1f : 1f); // 자동 발사는 Perfect 불가
            if (!localBreath.TryFire(matchTime)) { archers[localId].SetIdle(); return; }
            ShotParams s = new ShotParams(localId, meterAngle, meterPower, p, matchTime, localBreath.LastEfficiency);
            if (localBreath.LastEfficiency > maxEff) maxEff = localBreath.LastEfficiency;
            shotsFired++;
            bool perfect = Mathf.Abs(p) < cfg.perfectThreshold;
            hud.BreathFired();
            hud.FreezeMeter(p, perfect, 1.5f);
            archers[localId].Fire();
            transport.Send(NetMessage.Shot(s));
            SpawnArrow(s);

            BowAudio.I.Play("release", 0.85f, 0.95f, 1.05f);
            Vec2 f = setup.Feet(localId);
            vfx.FireDust(SimToWorld2(f), SimDirToWorld(new Vec2(-MatchSetup.Facing(localId), 0f)), meterPower);
            if (perfect)
            {
                BowAudio.I.Play("perfect_bell", 0.75f);
                vfx.GoldRing(LocalNockWorld());
                hud.Flash(Palette.Gold, 0.22f, ArtConstants.PerfectFlashDuration);
                hud.ShowMessage("완벽! ×1.25", Palette.GoldL);
                vfx.ShakePx(6f, 0.10f);
            }
            else
            {
                hud.ShowMessage(Mathf.Abs(p) < 0.4f ? "좋음" : "보통", Mathf.Abs(p) < 0.4f ? Palette.Ink : Palette.Grey, 0.4f, 64);
                if (meterPower > 0.7f) vfx.ShakePx(4f, 0.08f);
            }
            StartCoroutine(IdleAfter(0.35f, localId));
        }

        private IEnumerator IdleAfter(float delay, int id)
        {
            float t = 0f;
            while (t < delay) { t += Time.unscaledDeltaTime; yield return null; }
            if (!aiming && !metering && !archers[id].IsDead) archers[id].SetIdle();
        }

        // ---------------------------------------------------------------
        // 화살
        // ---------------------------------------------------------------
        private void SpawnArrow(ShotParams s)
        {
            ArrowFlight flight = ArrowSimulator.Simulate(s, setup, wind, cfg);
            GameObject go = new GameObject("Arrow");
            go.transform.SetParent(worldRoot, false);
            ArrowView av = go.AddComponent<ArrowView>();
            float startElapsed = Mathf.Max(0f, matchTime - s.launchTime);
            av.Build(flight, s.shooterId, flight.perfect, startElapsed, worldRoot);
            av.OnArrived = OnArrowArrived;
            arrows.Add(av);
        }

        private void OnArrowArrived(ArrowView a)
        {
            arrows.Remove(a);
            ArrowFlight f = a.Flight;
            Vector2 hitW = SimToWorld2(f.hitPoint);
            Vec2 dirSim = f.VelocityDirAt(f.flightTime - 0.01f);
            int dirSign = dirSim.x >= 0f ? 1 : -1;

            if (f.zone == HitZone.Ground)
            {
                vfx.GroundImpact(hitW);
                BowAudio.I.Play("miss_ground", 0.6f, 0.9f, 1.1f);
                return;
            }
            if (f.zone == HitZone.Obstacle)
            {
                vfx.BodyHit(hitW, SimDirToWorld(dirSim));
                vfx.ShakePx(3f, 0.08f);
                BowAudio.I.Play("miss_ground", 0.7f, 1.15f, 1.3f);
                if (f.shot.shooterId == localId) hud.ShowMessage("막힘", Palette.Grey, 0.4f, 56);
                return;
            }
            if (f.zone == HitZone.Out) return;

            int target = f.targetId;
            bool head = f.zone == HitZone.Head;
            ArcherView victim = archers[target];
            a.transform.SetParent(victim.transform, true);

            if (f.shot.shooterId == localId) { shotsHit++; if (head) headHits++; }

            // 연출
            victim.Hit(f.zone, dirSign);
            Vector2 dirW = SimDirToWorld(dirSim);
            if (head)
            {
                vfx.HeadHit(hitW, dirW);
                vfx.HitStop(ArtConstants.HitStopHead, ArtConstants.SlowMoScale, ArtConstants.SlowMoDuration);
                vfx.ZoomPunch(0.97f);
                vfx.ShakePx(14f, 0.25f);
                BowAudio.I.Play("hit_head", 1f, 0.95f, 1.05f);
                BowAudio.I.DuckBgm(ArtConstants.HitStopHead + ArtConstants.SlowMoDuration);
            }
            else
            {
                vfx.BodyHit(hitW, dirW);
                vfx.HitStop(ArtConstants.HitStopBody, 1f, 0f);
                vfx.ShakePx(8f, 0.15f);
                BowAudio.I.Play("hit_body", 0.9f, 0.93f, 1.07f);
            }
            if (f.perfect) BowAudio.I.Play("perfect_bell", 0.75f * 0.55f, 1.5f, 1.5f);

            // 판정 (호스트 권위, 클라이언트는 결정론 시뮬로 선반영 후 Hit 메시지로 보정)
            if (state.Phase == MatchPhase.Playing)
            {
                int dmg = state.DamageFor(f.zone, f.perfect);
                int hpAfter = state.ApplyDamage(target, dmg);
                int slot = target == localId ? 0 : 1;
                hud.SetHp(slot, hpAfter, cfg.maxHp, true);
                hud.ShowDamage(SimToWorld(f.hitPoint), dmg, f.perfect ? Palette.Gold : head ? Palette.Red : Palette.Ink, head ? 64 : 48);
                if (head) hud.ShowMessage("급소!", Palette.Red, 0.9f, 80);
                if (transport.IsHost) transport.Send(NetMessage.Hit(f.shot.shooterId, f.shot.launchTime, target, f.zone, f.perfect, dmg, hpAfter));
                if (hpAfter <= 0) KillArcher(target, dirSign);
            }
        }

        private void KillArcher(int id, int dirSign)
        {
            if (archers[id].IsDead) return;
            Vec2 feet = setup.Feet(id);
            archers[id].Die(dirSign, (p) => { vfx.InkPuddle(SimToWorld2(feet)); });
            vfx.ShakePx(22f, 0.40f);
            vfx.HitStop(0f, 0.4f, 0.6f);
        }

        // ---------------------------------------------------------------
        // 네트워크
        // ---------------------------------------------------------------
        private void HandleMessage(NetMessage m)
        {
            switch (m.type)
            {
                case NetMsgType.Shot:
                {
                    ShotParams s = m.ToShot();
                    if (s.shooterId == localId) break;
                    archers[s.shooterId].SetAim(s.angleDeg, s.power);
                    archers[s.shooterId].Fire();
                    SpawnArrow(s);
                    StartCoroutine(IdleAfter(0.5f, s.shooterId));
                    break;
                }
                case NetMsgType.Hit:
                {
                    if (transport.IsHost) break;
                    int target = m.Int(2);
                    int hpAfter = m.Int(6);
                    if (state.Hp(target) != hpAfter)
                    {
                        state.ForceHp(target, hpAfter);
                        hud.SetHp(target == localId ? 0 : 1, hpAfter, cfg.maxHp, true);
                        if (hpAfter <= 0) KillArcher(target, target == 0 ? 1 : -1);
                    }
                    break;
                }
                case NetMsgType.End:
                {
                    if (transport.IsHost) break;
                    state.ForceEnd(m.Int(0), m.Bool(1));
                    EndMatch();
                    break;
                }
                case NetMsgType.Ping:
                {
                    if (transport.IsHost) transport.Send(NetMessage.Pong(m.Float(0), matchTime));
                    break;
                }
                case NetMsgType.Pong:
                {
                    float rtt = Time.realtimeSinceStartup - m.Float(0);
                    float est = m.Float(1) + rtt * 0.5f;
                    float delta = est - matchTime;
                    if (Mathf.Abs(delta) > 0.02f) matchTime += delta * 0.5f;
                    break;
                }
                case NetMsgType.Leave:
                    HandleDisconnect("상대가 나갔습니다");
                    break;
            }
        }

        private void HandleDisconnect(string reason)
        {
            if (ended) return;
            hud.SetStatus(reason);
            hud.ShowMessage("연결 끊김", Palette.Grey, 1.5f, 72);
            // 남은 HP로 종료
            state.EndByTimeout();
            EndMatch();
        }

        // ---------------------------------------------------------------
        // 종료
        // ---------------------------------------------------------------
        private void EndMatch()
        {
            if (ended) return;
            ended = true;
            input.SetMode(AimInput.Mode.Disabled);
            metering = false; aiming = false;
            StopDrawLoops();
            hud.SetMeter(false, 0f, false, 0f, 0f);
            if (transport.IsHost) transport.Send(NetMessage.End(state.Winner, state.IsDraw));
            BowAudio.I.StopWind(1f);
            BowAudio.I.StopBgm(1f);
            StartCoroutine(EndSequence());
        }

        private IEnumerator EndSequence()
        {
            float t = 0f;
            while (t < 1.4f) { t += Time.unscaledDeltaTime; yield return null; }
            int outcome = state.IsDraw ? 0 : (state.Winner == localId ? 1 : -1);
            float acc = shotsFired > 0 ? 100f * shotsHit / shotsFired : 0f;
            string stats = "남은 HP  " + state.Hp(localId) + " : " + state.Hp(remoteId) + "\n"
                         + "명중률  " + acc.ToString("F0") + "%  (" + shotsHit + "/" + shotsFired + ")\n"
                         + "머리 명중  " + headHits + "회\n"
                         + "최대 호흡 효율  " + Mathf.RoundToInt(maxEff * 100f) + "%";
            result.Show(outcome, stats, isBotMatch);
            if (outcome > 0) vfx.ShakePx(10f, 0.2f);
        }

        public void Teardown()
        {
            if (transport != null)
            {
                transport.OnMessage -= HandleMessage;
                transport.OnDisconnected -= HandleDisconnect;
                try { transport.Send(new NetMessage(NetMsgType.Leave)); } catch (System.Exception) { }
                transport.Close();
            }
            StopDrawLoops();
            Time.timeScale = 1f;
            if (vfx != null) vfx.CleanupEffects();
            Destroy(gameObject);
        }
    }
}
