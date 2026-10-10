using System;

namespace Bow.Core
{
    public enum MatchPhase { Countdown = 0, Playing = 1, Ended = 2 }

    /// <summary>
    /// 경기 상태(HP, 타이머, 승패). 네트워크 대전에서는 호스트가 권위(authority)를 가지며
    /// 클라이언트는 HitMessage로 전달된 결과를 그대로 적용한다.
    /// </summary>
    public sealed class DuelState
    {
        private readonly DuelConfig cfg;
        private readonly int[] hp = new int[2];
        private readonly int[] maxHp = new int[2];

        public MatchPhase Phase { get; private set; }
        /// <summary>승자 id. 무승부/미종료면 -1</summary>
        public int Winner { get; private set; }
        public bool IsDraw { get; private set; }
        public float MatchTime { get; private set; }
        public float TimeRemaining { get { float r = cfg.matchDuration - MatchTime; return r > 0f ? r : 0f; } }

        public DuelState(DuelConfig config) : this(config, config.maxHp, config.maxHp) { }

        public DuelState(DuelConfig config, int maxHp0, int maxHp1)
        {
            cfg = config;
            maxHp[0] = maxHp0 > 0 ? maxHp0 : cfg.maxHp; maxHp[1] = maxHp1 > 0 ? maxHp1 : cfg.maxHp;
            hp[0] = maxHp[0]; hp[1] = maxHp[1];
            Phase = MatchPhase.Countdown;
            Winner = -1;
            MatchTime = 0f;
        }

        public int Hp(int playerId) { return hp[playerId == 0 ? 0 : 1]; }
        public int MaxHp(int playerId) { return maxHp[playerId == 0 ? 0 : 1]; }
        public float HpRatio(int playerId) { return (float)Hp(playerId) / MaxHp(playerId); }

        public void BeginPlay() { Phase = MatchPhase.Playing; MatchTime = 0f; }

        /// <summary>경기 시간 설정(클라이언트는 호스트 시계에 동기화된 값을 넣는다)</summary>
        public void SetMatchTime(float t)
        {
            MatchTime = t;
            if (Phase == MatchPhase.Playing && MatchTime >= cfg.matchDuration) EndByTimeout();
        }

        /// <summary>부위 기본 데미지 × 정확도 배율 (반올림)</summary>
        public int DamageFor(HitZone zone, float deviation)
        {
            int baseDmg = zone == HitZone.Head ? cfg.headDamage : zone == HitZone.Body ? cfg.bodyDamage : 0;
            if (baseDmg == 0) return 0;
            return (int)MathF.Round(baseDmg * cfg.DamageMultiplier(deviation));
        }

        /// <summary>데미지 적용 후 남은 HP 반환</summary>
        public int ApplyDamage(int targetId, int damage)
        {
            int i = targetId == 0 ? 0 : 1;
            hp[i] -= damage;
            if (hp[i] < 0) hp[i] = 0;
            if (hp[i] == 0 && Phase == MatchPhase.Playing)
            {
                Phase = MatchPhase.Ended;
                Winner = 1 - i;
                IsDraw = false;
            }
            return hp[i];
        }

        /// <summary>클라이언트 동기화용: HP를 호스트 값으로 강제 설정</summary>
        public void ForceHp(int playerId, int value)
        {
            int i = playerId == 0 ? 0 : 1;
            hp[i] = value < 0 ? 0 : value;
            if (hp[i] == 0 && Phase == MatchPhase.Playing)
            {
                Phase = MatchPhase.Ended; Winner = 1 - i; IsDraw = false;
            }
        }

        public void EndByTimeout()
        {
            if (Phase == MatchPhase.Ended) return;
            Phase = MatchPhase.Ended;
            if (hp[0] == hp[1]) { IsDraw = true; Winner = -1; }
            else { IsDraw = false; Winner = hp[0] > hp[1] ? 0 : 1; }
        }

        public void ForceEnd(int winner, bool draw)
        {
            Phase = MatchPhase.Ended; Winner = winner; IsDraw = draw;
        }
    }
}
