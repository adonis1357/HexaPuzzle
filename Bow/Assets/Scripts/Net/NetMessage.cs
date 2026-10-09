using System;
using System.Globalization;
using System.Text;
using Bow.Core;

namespace Bow.Net
{
    public enum NetMsgType
    {
        Hello = 0,   // 접속 인사 (버전)
        Start = 1,   // seed, 호스트 경기시간 → 경기 시작
        Shot = 2,    // 사격 파라미터
        Hit = 3,     // 호스트 권위 판정 결과 (targetId, zone, perfect, damage, hpAfter)
        End = 4,     // 경기 종료 (winner, draw)
        Ping = 5,    // 클라이언트 로컬시간
        Pong = 6,    // (클라이언트 로컬시간, 호스트 경기시간)
        Leave = 7,
        Pick = 8     // (playerId, characterId) 캐릭터 선택 동기화
    }

    /// <summary>
    /// 줄 단위 텍스트 직렬화 메시지: "TYPE|arg0|arg1|..." (InvariantCulture).
    /// 프로토타입용으로 사람이 읽기 쉽고 디버깅이 간단한 포맷을 택했다.
    /// </summary>
    public sealed class NetMessage
    {
        public NetMsgType type;
        public string[] args;

        public NetMessage(NetMsgType type, params string[] args)
        {
            this.type = type;
            this.args = args ?? new string[0];
        }

        public static string F(float v) { return v.ToString("R", CultureInfo.InvariantCulture); }
        public static float PF(string s) { return float.Parse(s, CultureInfo.InvariantCulture); }
        public static int PI(string s) { return int.Parse(s, CultureInfo.InvariantCulture); }

        public float Float(int i) { return PF(args[i]); }
        public int Int(int i) { return PI(args[i]); }
        public bool Bool(int i) { return args[i] == "1"; }

        public string Serialize()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append(((int)type).ToString(CultureInfo.InvariantCulture));
            for (int i = 0; i < args.Length; i++)
            {
                sb.Append('|');
                sb.Append(args[i].Replace("|", "/").Replace("\n", " "));
            }
            return sb.ToString();
        }

        public static NetMessage Parse(string line)
        {
            if (string.IsNullOrEmpty(line)) return null;
            string[] parts = line.Split('|');
            int t;
            if (!int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out t)) return null;
            string[] a = new string[parts.Length - 1];
            Array.Copy(parts, 1, a, 0, a.Length);
            return new NetMessage((NetMsgType)t, a);
        }

        // ---------- 편의 생성자 ----------
        public static NetMessage Hello(string version) { return new NetMessage(NetMsgType.Hello, version); }

        public static NetMessage Start(int seed, float hostMatchTime)
        {
            return new NetMessage(NetMsgType.Start, seed.ToString(CultureInfo.InvariantCulture), F(hostMatchTime));
        }

        public static NetMessage Shot(ShotParams s)
        {
            return new NetMessage(NetMsgType.Shot, s.shooterId.ToString(CultureInfo.InvariantCulture),
                F(s.angleDeg), F(s.power), F(s.deviation), F(s.launchTime), F(s.breathEff));
        }

        public ShotParams ToShot()
        {
            return new ShotParams(Int(0), Float(1), Float(2), Float(3), Float(4), args.Length > 5 ? Float(5) : 0f);
        }

        public static NetMessage Hit(int shooterId, float launchTime, int targetId, HitZone zone, bool perfect, int damage, int hpAfter)
        {
            return new NetMessage(NetMsgType.Hit,
                shooterId.ToString(CultureInfo.InvariantCulture), F(launchTime),
                targetId.ToString(CultureInfo.InvariantCulture), ((int)zone).ToString(CultureInfo.InvariantCulture),
                perfect ? "1" : "0", damage.ToString(CultureInfo.InvariantCulture), hpAfter.ToString(CultureInfo.InvariantCulture));
        }

        public static NetMessage End(int winner, bool draw)
        {
            return new NetMessage(NetMsgType.End, winner.ToString(CultureInfo.InvariantCulture), draw ? "1" : "0");
        }

        public static NetMessage Pick(int playerId, string characterId)
        {
            return new NetMessage(NetMsgType.Pick, playerId.ToString(CultureInfo.InvariantCulture), characterId ?? "default");
        }

        public static NetMessage Ping(float clientLocalTime) { return new NetMessage(NetMsgType.Ping, F(clientLocalTime)); }
        public static NetMessage Pong(float clientLocalTime, float hostMatchTime) { return new NetMessage(NetMsgType.Pong, F(clientLocalTime), F(hostMatchTime)); }
    }
}
