using UnityEngine;
using Bow.Art;
using Bow.Audio;
using Bow.Core;
using Bow.Net;
using Bow.UI;

namespace Bow.Game
{
    /// <summary>
    /// 「활」 진입점. 씬에는 Main Camera와 이 컴포넌트가 붙은 GameObject만 있고, 나머지는 전부 런타임 생성.
    /// 로비 → (봇 / LAN 호스트 / LAN 참가) → MatchController → 결과 → 로비.
    /// </summary>
    public sealed class BowBootstrap : MonoBehaviour
    {
        private Camera cam;
        private LobbyView lobby;
        private MatchController match;
        private IMatchTransport pendingTransport;
        private BotProfile lastBot;

        private Transform backdrop;
        private SpriteRenderer paper;
        private readonly SpriteRenderer[] mountains = new SpriteRenderer[3];
        private static readonly float[] MountainBasePx = { 120f, 60f, 0f };
        private static readonly float[] MountainHeightPx = { 520f, 340f, 190f };

        private void Awake()
        {
            Application.targetFrameRate = 60;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            cam = Camera.main;
            if (cam == null)
            {
                GameObject cg = new GameObject("Main Camera");
                cg.tag = "MainCamera";
                cam = cg.AddComponent<Camera>();
                cg.AddComponent<AudioListener>();
            }
            cam.orthographic = true;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Palette.Paper;
            cam.nearClipPlane = 0.1f; cam.farClipPlane = 200f;

            UiFactory.EnsureEventSystem();
            BowAudio bgm = BowAudio.I;

            BuildBackdrop();
            ConfigureBackdrop(10f);
            cam.transform.position = new Vector3(0f, 10f * (1f - 2f * 700f / 1920f), -10f);

            lobby = new GameObject("LobbyView").AddComponent<LobbyView>();
            lobby.transform.SetParent(transform, false);
            lobby.Build();
            lobby.OnBotMatch = StartBot;
            lobby.OnHost = HostLan;
            lobby.OnJoin = JoinLan;
            lobby.OnCharacterPreview = ShowPreview;
            bgm.PlayBgm("bgm_lobby");

            // 자동 플레이 테스트 (Editor/PlayTestRunner가 PlayerPrefs로 지시): 1.5초 뒤 봇 대전 자동 시작
            if (PlayerPrefs.GetInt("hwal_autotest", 0) == 1)
            {
                autoTest = true;
                Debug.Log("[활] 자동 테스트 모드: 봇 대전(보통) 자동 시작");
                Invoke("AutoTestStart", 1.5f);
            }
        }

        private bool autoTest;
        private ArcherView preview;

        /// <summary>로비 캐릭터 선택 미리보기: 월드에 3배 크기 궁수를 세운다 (null이면 제거)</summary>
        private void ShowPreview(CharacterDef def)
        {
            if (preview != null) { Destroy(preview.gameObject); preview = null; }
            if (def == null) return;
            GameObject go = new GameObject("Preview");
            go.transform.SetParent(transform, false);
            // 발 위치 = 화면 위에서 980px 지점 (3배 궁수가 450~980px 영역에 서도록)
            float ortho = cam.orthographicSize;
            float feetY = cam.transform.position.y + ortho - 980f / 1920f * 2f * ortho;
            // 오른쪽에는 능력치 육각형이 오므로 궁수는 왼쪽 (화면 x −270px ≈ −2.8m @ ortho 10)
            float px2m = 2f * ortho * cam.aspect / Screen.width * (Screen.width / 1080f);
            go.transform.position = new Vector3(-270f * px2m, feetY, 0f);
            go.transform.localScale = new Vector3(3f, 3f, 1f);
            preview = go.AddComponent<ArcherView>();
            preview.Build(0, 1, def);
            preview.SetAim(35f, 0.55f);
            preview.transform.localScale = new Vector3(3f, 3f, 1f);
        }

        private void AutoTestStart()
        {
            StartBot(BotProfile.Normal());
            if (match != null) match.autoTest = true;
        }

        private void Update()
        {
            if (pendingTransport != null)
            {
                pendingTransport.Poll();
                if (lobby != null) lobby.SetStatus(pendingTransport.Status);
            }
        }

        // ---------------------------------------------------------------
        // 배경 (카메라 자식: 한지 + 원경 3겹). 로비/경기 공용.
        // ---------------------------------------------------------------
        private void BuildBackdrop()
        {
            backdrop = new GameObject("Backdrop").transform;
            backdrop.SetParent(cam.transform, false);
            int seed = System.DateTime.Now.Millisecond;
            GameObject p = new GameObject("Paper");
            p.transform.SetParent(backdrop, false);
            p.transform.localPosition = new Vector3(0f, 0f, 60f);
            paper = p.AddComponent<SpriteRenderer>();
            paper.sprite = ProceduralSprites.Paper(seed);
            paper.sortingOrder = ArtConstants.SortSky;
            paper.sharedMaterial = ProceduralSprites.SpriteMaterial;

            Color[] cols = { Palette.WithAlpha(Palette.Ink, 0.10f), Palette.WithAlpha(Palette.Ink, 0.18f), Palette.WithAlpha(Palette.GreyD, 0.30f) };
            int[] orders = { ArtConstants.SortMountainFar, ArtConstants.SortMountainMid, ArtConstants.SortMountainNear };
            for (int i = 0; i < 3; i++)
            {
                GameObject m = new GameObject("Mountain" + i);
                m.transform.SetParent(backdrop, false);
                m.transform.localPosition = new Vector3(0f, 0f, 50f - i);
                mountains[i] = m.AddComponent<SpriteRenderer>();
                mountains[i].sprite = ProceduralSprites.MountainLayer(seed, i, 1f, cols[i], 1f);
                mountains[i].sortingOrder = orders[i];
                mountains[i].sharedMaterial = ProceduralSprites.SpriteMaterial;
            }
        }

        /// <summary>카메라 orthoSize에 맞춰 배경 크기/위치 재설정 (지면선 = 화면 하단 700px)</summary>
        public void ConfigureBackdrop(float ortho)
        {
            float aspect = cam.aspect;
            float viewH = 2f * ortho, viewW = viewH * aspect;
            paper.transform.localScale = new Vector3(viewW / 5.4f * 1.03f, viewH / 9.6f * 1.03f, 1f);
            float horizonY = -ortho + 700f / 1920f * viewH;
            for (int i = 0; i < 3; i++)
            {
                float baseY = horizonY + MountainBasePx[i] / 1920f * viewH;
                float h = MountainHeightPx[i] / 1920f * viewH;
                mountains[i].transform.localPosition = new Vector3(0f, baseY, 50f - i);
                mountains[i].transform.localScale = new Vector3(viewW / 12.8f * 1.05f, h / 5.12f, 1f);
            }
        }

        // ---------------------------------------------------------------
        // 로비 액션
        // ---------------------------------------------------------------
        private void StartBot(BotProfile profile)
        {
            lastBot = profile;
            StartMatch(new LoopbackTransport(), profile, NewSeed());
        }

        private void HostLan()
        {
            CancelPending();
            LanTcpTransport t = LanTcpTransport.Host();
            pendingTransport = t;
            t.OnPeerConnected += () =>
            {
                if (pendingTransport != t) return;
                pendingTransport = null;
                StartMatch(t, null, NewSeed());
            };
            t.OnDisconnected += (reason) => { if (lobby != null) lobby.SetStatus(reason); };
            lobby.SetStatus(t.Status);
        }

        private void JoinLan(string ip)
        {
            CancelPending();
            LanTcpTransport t = LanTcpTransport.Connect(ip);
            pendingTransport = t;
            t.OnMessage += (m) =>
            {
                if (m.type != NetMsgType.Start || pendingTransport != t) return;
                pendingTransport = null;
                StartMatch(t, null, m.Int(0));
            };
            t.OnDisconnected += (reason) => { if (lobby != null) lobby.SetStatus(reason); };
            lobby.SetStatus(t.Status);
        }

        private void CancelPending()
        {
            if (pendingTransport != null) { pendingTransport.Close(); pendingTransport = null; }
        }

        private static int NewSeed() { return Random.Range(1, int.MaxValue); }

        private void StartMatch(IMatchTransport transport, BotProfile bot, int seed)
        {
            if (match != null) { match.Teardown(); match = null; }
            ShowPreview(null);
            lobby.SetVisible(false);
            GameObject go = new GameObject("Match");
            match = go.AddComponent<MatchController>();
            match.Begin(this, cam, transport, bot, seed);
        }

        public void ReturnToLobby()
        {
            if (match != null) { match.Teardown(); match = null; }
            ShowPreview(null);
            CancelPending();
            BowAudio.I.StopWind(0.3f);
            ConfigureBackdrop(10f);
            cam.transform.position = new Vector3(0f, 10f * (1f - 2f * 700f / 1920f), -10f);
            cam.orthographicSize = 10f;
            lobby.SetVisible(true);
            lobby.ShowMain();
            lobby.SetStatus("");
            BowAudio.I.PlayBgm("bgm_lobby");
        }

        public void Retry()
        {
            if (lastBot == null) { ReturnToLobby(); return; }
            if (match != null) { match.Teardown(); match = null; }
            BowAudio.I.StopWind(0.1f);
            StartBot(lastBot);
        }

        private void OnApplicationQuit() { CancelPending(); if (match != null) match.Teardown(); }
    }
}
