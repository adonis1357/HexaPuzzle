using Bow.Core;

namespace Bow.Game
{
    /// <summary>
    /// 봇 상대 (호스트에서만 동작). 호흡 시스템을 동일하게 사용하며,
    /// 장전 완료 후 난이도별 초과 대기 h를 고른 뒤 그 시점에 사격한다. 발사 0.8초 전부터 조준 동작을 보여준다.
    /// </summary>
    public sealed class BotController
    {
        private readonly int botId;
        private readonly BotProfile profile;
        private readonly DuelConfig cfg;
        private readonly MatchSetup setup;
        private readonly WindModel wind;
        private readonly DeterministicRandom rng;
        public BreathSystem Breath { get; private set; }

        private float targetOverhold;
        private bool planned;
        private ShotParams plan;

        public BotController(int botId, BotProfile profile, DuelConfig cfg, MatchSetup setup, WindModel wind, int seed, float matchStartTime)
        {
            this.botId = botId; this.profile = profile; this.cfg = cfg; this.setup = setup; this.wind = wind;
            rng = new DeterministicRandom(seed ^ 0x0B07);
            Breath = new BreathSystem(cfg, matchStartTime);
            targetOverhold = BotBrain.PickOverhold(profile, rng);
            planned = false;
        }

        /// <summary>조준 표시용: 계획된 각도/힘 (planned일 때만 유효)</summary>
        public bool HasPlan { get { return planned; } }
        public ShotParams Plan { get { return plan; } }

        /// <summary>매 프레임 호출. 사격이 발생하면 true와 ShotParams를 돌려준다.</summary>
        public bool Tick(float matchTime, out ShotParams shot)
        {
            shot = default(ShotParams);
            if (!Breath.IsReady(matchTime)) { planned = false; return false; }
            float h = Breath.Overhold(matchTime);
            if (!planned && h >= targetOverhold - 0.8f)
            {
                float launchAt = Breath.ReadyTime + targetOverhold;
                plan = BotBrain.Plan(botId, profile, setup, wind.GetWind(launchAt), rng, cfg, launchAt, Breath.Efficiency(targetOverhold));
                planned = true;
            }
            if (planned && h >= targetOverhold)
            {
                if (!Breath.TryFire(matchTime)) return false;
                shot = new ShotParams(botId, plan.angleDeg, plan.power, plan.deviation, matchTime, Breath.LastEfficiency);
                planned = false;
                targetOverhold = BotBrain.PickOverhold(profile, rng);
                return true;
            }
            return false;
        }
    }
}
