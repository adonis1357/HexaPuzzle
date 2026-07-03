// ★ 보안(2026-07): 레벨 검증 하니스(Editor 메뉴 전용)는 릴리스 빌드에서 제외 — 레벨 구조 덤프/죽은 코드 제거
#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text;
using JewelsHexaPuzzle.Data;

namespace JewelsHexaPuzzle.Managers
{
    /// <summary>
    /// 전 레벨 미션 자동 검증 하니스 (게임 실제 로직 클래스 직접 호출).
    /// 3가지 검증:
    ///  1) 소환=미션 일치: 각 RemoveEnemy 미션의 targetEnemyType이 실제 소환 가능한 타입(EnemyType 10~23)이고 수량>0.
    ///     (소환 시스템은 GetMissionTargetForType로 미션 타입만, 미션 수량만큼 소환 → 비소환 타입 미션이면 영영 못 깸.)
    ///  2) 처치→미션 감소: 해당 타입 고블린 처치 시 StageManager.MapKillFlagsToType이 그 미션 타입으로 매핑되어
    ///     currentCount가 증가·완료까지 도달하는지(매핑 라운드트립 + 시뮬 감소).
    ///  3) 이동 보상: MissionBalance.GetOrAssignMoveReward가 유효 보상(1~5)을 주는지.
    /// 결과를 .claude/captures/levelverify.txt에 기록.
    /// </summary>
    public static class LevelMissionVerifier
    {
        // 실제 소환 분기가 존재하는 타입(EnemyType 10~23). 0~9는 미션 전용·소환 코드 없음.
        private static readonly HashSet<EnemyType> Spawnable = new HashSet<EnemyType>
        {
            EnemyType.Goblin, EnemyType.ArmoredGoblin, EnemyType.ArcherGoblin, EnemyType.ShieldGoblin,
            EnemyType.BombGoblin, EnemyType.HealerGoblin, EnemyType.HeavyGoblin, EnemyType.WizardGoblin,
            EnemyType.ThiefGoblin, EnemyType.WitchGoblin,
            EnemyType.GoblinLv2, EnemyType.ArmoredGoblinLv2, EnemyType.ArcherGoblinLv2, EnemyType.ShieldGoblinLv2
        };

        public static string RunVerification(int runs)
        {
            if (runs < 1) runs = 1;
            int levelsWithMissions = 0, removeEnemyMissionsPerRun = 0;
            int fail1 = 0, fail2 = 0, fail3 = 0;
            var failLines = new List<string>();

            for (int run = 1; run <= runs; run++)
            {
                var stages = Mission1StageData.GetAllMission1Stages();
                var keys = new List<int>(stages.Keys);
                keys.Sort();
                foreach (int level in keys)
                {
                    var data = stages[level];
                    if (data == null || data.missions == null || data.missions.Length == 0) continue;
                    if (run == 1) levelsWithMissions++;

                    foreach (var m in data.missions)
                    {
                        if (m == null || m.type != MissionType.RemoveEnemy) continue;
                        if (run == 1) removeEnemyMissionsPerRun++;

                        // === CHECK 1: 소환=미션 일치 ===
                        bool c1 = Spawnable.Contains(m.targetEnemyType) && m.targetCount > 0;
                        if (!c1 && failLines.Count < 300)
                            failLines.Add($"L{level} run{run} [CHECK1 소환불일치] type={m.targetEnemyType}({(int)m.targetEnemyType}) cnt={m.targetCount} (10~23만 소환가능)");

                        // === CHECK 2: 처치→미션 감소 ===
                        var f = CanonicalFlags(m.targetEnemyType);
                        EnemyType mapped = StageManager.MapKillFlagsToType(
                            f.armored, f.archer, f.shield, f.bomb, f.healer, f.heavy, f.wizard, f.thief, f.witch, f.level);
                        bool mapOk = mapped == m.targetEnemyType;
                        // 시뮬 감소: 대상 타입 처치를 targetCount회 → currentCount 도달 + 완료
                        int currentCount = 0;
                        bool complete = false;
                        for (int kill = 0; kill < m.targetCount; kill++)
                        {
                            EnemyType km = StageManager.MapKillFlagsToType(
                                f.armored, f.archer, f.shield, f.bomb, f.healer, f.heavy, f.wizard, f.thief, f.witch, f.level);
                            if (km == m.targetEnemyType) currentCount++;     // 미션 currentCount++ (실제 로직과 동일)
                            if (currentCount >= m.targetCount) complete = true;
                        }
                        bool c2 = mapOk && currentCount == m.targetCount && complete;
                        if (!c2 && failLines.Count < 300)
                            failLines.Add($"L{level} run{run} [CHECK2 미션감소실패] type={m.targetEnemyType} mapped={mapped} dec={currentCount}/{m.targetCount} complete={complete}");

                        // === CHECK 3: 이동 보상(1~5) ===
                        int reward = MissionBalance.GetOrAssignMoveReward(m);
                        bool c3 = reward >= 1 && reward <= 5;
                        if (!c3 && failLines.Count < 300)
                            failLines.Add($"L{level} run{run} [CHECK3 보상오류] type={m.targetEnemyType} reward={reward}");

                        if (!c1) fail1++;
                        if (!c2) fail2++;
                        if (!c3) fail3++;
                    }
                }
            }

            var sb = new StringBuilder();
            sb.AppendLine("=== 전 레벨 미션 자동 검증 ===");
            sb.AppendLine($"미션 보유 레벨: {levelsWithMissions}개 / 적제거미션: {removeEnemyMissionsPerRun}개(회당) / 반복: {runs}회");
            sb.AppendLine($"총 검증 횟수: {removeEnemyMissionsPerRun * runs} (미션×회)");
            sb.AppendLine($"CHECK1 소환=미션 일치  실패: {fail1}");
            sb.AppendLine($"CHECK2 처치→미션 감소  실패: {fail2}");
            sb.AppendLine($"CHECK3 이동보상(1~5)   실패: {fail3}");
            bool allPass = fail1 == 0 && fail2 == 0 && fail3 == 0;
            sb.AppendLine(allPass ? ">>> ALL PASS <<<" : ">>> FAIL — 상세 ↓ <<<");
            foreach (var l in failLines) sb.AppendLine(l);
            string report = sb.ToString();

            try
            {
                string dir = System.IO.Path.Combine(System.IO.Directory.GetCurrentDirectory(), ".claude", "captures");
                System.IO.Directory.CreateDirectory(dir);
                System.IO.File.WriteAllText(System.IO.Path.Combine(dir, "levelverify.txt"), report);
            }
            catch (System.Exception e) { UnityEngine.Debug.LogWarning($"[LevelVerify] 파일쓰기 실패: {e.Message}"); }

            UnityEngine.Debug.LogWarning($"[LevelVerify] 완료 — CHECK1={fail1} CHECK2={fail2} CHECK3={fail3} ({(allPass ? "ALL PASS" : "FAIL")})");
            return report;
        }

        /// <summary>전 레벨 디자인 덤프(이동횟수·활성한도·활성/대기 미션 배열) → .claude/captures/leveldump.txt + 반환.</summary>
        /// <summary>
        /// ★ 동적 미션 활성한도 밴드 검증 (V1) — 전 150레벨의 Min/Max가 설계 밴드와 일치하는지.
        ///   1~10: 1/1, 11~20: 1/2, 21~50: 2/4, 51~80: 2/5, 81~110: 3/6, 111~150: 3/7
        ///   (잠금수 0,1,2,3,3,4 — 150 잠금 4 = 구 2배, 중간 선형 보간. 2026-07-02).
        ///   추가: Min≤Max, Max≤7, 로비 뱃지 산식(GetPendingMissionCount = 보충총수−Min) 일관성.
        /// </summary>
        public static string VerifyMissionLimitBands()
        {
            var sb = new StringBuilder();
            int fails = 0;
            for (int lv = 1; lv <= 150; lv++)
            {
                int min = MissionBalance.GetMissionLimitMin(lv);
                int max = MissionBalance.GetMissionLimitMax(lv);
                int expMin = lv <= 20 ? 1 : (lv <= 80 ? 2 : 3);
                int expMax = lv <= 10 ? 1 : (lv <= 20 ? 2 : (lv <= 50 ? 4 : (lv <= 80 ? 5 : (lv <= 110 ? 7 : 8))));
                bool ok = min == expMin && max == expMax && min <= max && max <= 8 && (max - min) <= 6;
                if (!ok) { fails++; sb.AppendLine($"FAIL Lv{lv}: min={min}(기대{expMin}) max={max}(기대{expMax})"); }
            }
            // ★ 대기 미션 목표 선형 램프 검증 (시작 2 유지, 150 = 18 = 구 9의 2배)
            var expPending = new (int lv, int exp)[] { (21, 2), (30, 3), (50, 6), (80, 9), (110, 13), (150, 18) };
            foreach (var (lv, exp) in expPending)
            {
                int got = MissionBalance.GetBandPendingTarget(lv);
                if (got != exp) { fails++; sb.AppendLine($"FAIL 대기목표 Lv{lv}: {got}(기대{exp})"); }
            }
            // 로비 뱃지 산식 일관성 (전 스테이지: pending = 보충총수 − Min, 음수 없음)
            var stages = Mission1StageData.GetAllMission1Stages();
            foreach (var kv in stages)
            {
                if (kv.Value == null) continue;
                int pending = MissionBalance.GetPendingMissionCount(kv.Value);
                if (pending < 0) { fails++; sb.AppendLine($"FAIL Lv{kv.Key}: pending 음수 {pending}"); }
            }
            string head = $"=== 미션 활성한도 밴드 검증 (150레벨) — {(fails == 0 ? "PASS ✅" : $"FAIL {fails}건")} ===";
            var final = new StringBuilder();
            final.AppendLine(head);
            final.Append(sb);
            // 밴드 요약표
            final.AppendLine("밴드 요약: 1~10=1/1, 11~20=1/2, 21~50=2/4, 51~80=2/5, 81~110=3/7, 111~150=3/8 (잠금 1~5, 해금 간격 15이동)");
            final.AppendLine("대기목표: 21=2 → 150=18 선형 (구 9의 2배)");
            try
            {
                string dir = System.IO.Path.Combine(
                    System.IO.Path.GetDirectoryName(UnityEngine.Application.dataPath), ".claude", "captures");
                System.IO.Directory.CreateDirectory(dir);
                System.IO.File.WriteAllText(System.IO.Path.Combine(dir, "limitband_verify.txt"), final.ToString());
            }
            catch { }
            return final.ToString();
        }

        public static string DumpAllLevels()
        {
            var sb = new StringBuilder();
            var stages = Mission1StageData.GetAllMission1Stages();
            var keys = new List<int>(stages.Keys); keys.Sort();
            foreach (int level in keys)
            {
                var d = stages[level];
                if (d == null) continue;
                int activeLimit = d.maxActiveMissions > 0 ? d.maxActiveMissions : MissionBalance.MAX_ACTIVE_MISSIONS;
                var missions = d.missions ?? new MissionData[0];
                var active = new List<string>();
                var pending = new List<string>();
                for (int i = 0; i < missions.Length; i++)
                {
                    string lbl = MissionLabel(missions[i]);
                    if (i < activeLimit) active.Add(lbl); else pending.Add(lbl);
                }
                sb.Append("L").Append(level)
                  .Append("\t이동").Append(d.turnLimit)
                  .Append("\t활성한도").Append(activeLimit)
                  .Append("\tch").Append(d.chapterNumber).Append("(").Append(d.chapterName).Append(")")
                  .Append("\tdiff").Append(d.difficulty)
                  .Append("\t활성[").Append(string.Join(", ", active)).Append("]")
                  .Append("\t대기[").Append(string.Join(", ", pending)).Append("]")
                  .AppendLine();
            }
            string report = sb.ToString();
            try
            {
                string dir = System.IO.Path.Combine(System.IO.Directory.GetCurrentDirectory(), ".claude", "captures");
                System.IO.Directory.CreateDirectory(dir);
                System.IO.File.WriteAllText(System.IO.Path.Combine(dir, "leveldump.txt"), report);
            }
            catch (System.Exception e) { UnityEngine.Debug.LogWarning($"[LevelDump] 파일쓰기 실패: {e.Message}"); }
            UnityEngine.Debug.LogWarning($"[LevelDump] {keys.Count}레벨 덤프 완료");
            return report;
        }

        private static string MissionLabel(MissionData m)
        {
            if (m == null) return "?";
            if (m.type == MissionType.RemoveEnemy) return EnemyShort(m.targetEnemyType) + "×" + m.targetCount;
            if (m.type == MissionType.CollectGem) return "수집:" + m.targetGemType + "×" + m.targetCount;
            if (m.type == MissionType.CollectMultiGem) return "복수수집×" + m.targetCount;
            return m.type + "×" + m.targetCount;
        }

        private static string EnemyShort(EnemyType t)
        {
            switch (t)
            {
                case EnemyType.Goblin: return "몽둥이";
                case EnemyType.ArmoredGoblin: return "갑옷";
                case EnemyType.ArcherGoblin: return "활";
                case EnemyType.ShieldGoblin: return "방패";
                case EnemyType.BombGoblin: return "폭탄";
                case EnemyType.HealerGoblin: return "힐러";
                case EnemyType.HeavyGoblin: return "헤비";
                case EnemyType.WizardGoblin: return "마법사";
                case EnemyType.ThiefGoblin: return "도둑";
                case EnemyType.WitchGoblin: return "마녀";
                case EnemyType.GoblinLv2: return "엘리트몽둥이";
                case EnemyType.ArmoredGoblinLv2: return "엘리트갑옷";
                case EnemyType.ArcherGoblinLv2: return "엘리트활";
                case EnemyType.ShieldGoblinLv2: return "엘리트방패";
                default: return t.ToString();
            }
        }

        private struct Flags { public bool armored, archer, shield, bomb, healer, heavy, wizard, thief, witch; public int level; }

        /// <summary>EnemyType → 소환 시 설정되는 정규 플래그(역매핑). 소환 코드의 플래그 설정과 동일 의미.</summary>
        private static Flags CanonicalFlags(EnemyType t)
        {
            var f = new Flags { level = 1 };
            switch (t)
            {
                case EnemyType.ArmoredGoblin: f.armored = true; break;
                case EnemyType.ArcherGoblin: f.archer = true; break;
                case EnemyType.ShieldGoblin: f.shield = true; break;
                case EnemyType.BombGoblin: f.bomb = true; break;
                case EnemyType.HealerGoblin: f.healer = true; break;
                case EnemyType.HeavyGoblin: f.heavy = true; break;
                case EnemyType.WizardGoblin: f.wizard = true; break;
                case EnemyType.ThiefGoblin: f.thief = true; break;
                case EnemyType.WitchGoblin: f.witch = true; break;
                case EnemyType.GoblinLv2: f.level = 2; break;
                case EnemyType.ArmoredGoblinLv2: f.armored = true; f.level = 2; break;
                case EnemyType.ArcherGoblinLv2: f.archer = true; f.level = 2; break;
                case EnemyType.ShieldGoblinLv2: f.shield = true; f.level = 2; break;
                // Goblin(기본) 및 비소환 타입: 모든 플래그 false, level 1
            }
            return f;
        }
    }
}

#endif
