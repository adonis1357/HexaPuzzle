// ============================================================================
// LearningAutoPlay — 학습형 자동플레이 (Editor 전용)
// ============================================================================
// 목표: 레벨을 "클리어"하되, 남은 이동횟수(leftMoves)가 많을수록 fitness가 높다.
//   → 봇이 반복 주행하며 이동 평가 가중치(MatchingSystem.W_*)를 스스로 튜닝한다.
//
// 알고리즘: (1+1)-ES (부모1 + 자식1). 자식을 가우시안 변이 → N패스 평가 →
//   부모보다 나으면 승격. 1/5 성공률로 σ 적응. 상태는 weights.json에 영속.
//
// fitness(1런):
//   클리어  = CLEAR_BONUS + leftMoves*MOVE_COEF           (남은이동 많을수록↑ — 사용자 요구)
//   미클리어 = (완료미션/전체)*PARTIAL_SCALE - GAMEOVER_PENALTY
//   ※ 클리어는 게임 규약상 모든 미션 완료가 전제이므로, 같은 레벨의 부모/자식 비교에서
//      이동보상 인플레는 상수로 상쇄된다(leftMoves가 곧 효율 신호). 비평 #2 대응.
//
// 검증 시스템과 상호배제: Verify_Active 중이면 학습 시작을 거부, 학습 중이면 검증이 스킵.
// ============================================================================
#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using JewelsHexaPuzzle.Core;

namespace JewelsHexaPuzzle.EditorTools
{
    [InitializeOnLoad]
    public static class LearningAutoPlay
    {
        // ── fitness 상수 ──
        private const float CLEAR_BONUS = 1000f;
        private const float MOVE_COEF = 100f;      // 남은 이동 1당 (사용자: "많을수록 높게")
        private const float PARTIAL_SCALE = 300f;  // 미클리어 미션완료율 최대기여(<CLEAR)
        private const float GAMEOVER_PENALTY = 50f;
        private const float GOLD_PENALTY = 1f;     // ★ 골드 1 소비당 -1점 (사용자: 골드 사용은 마이너스 점수)

        // ── ES 상수 ──
        private const float SIGMA_MIN = 0.02f, SIGMA_MAX = 1.0f;
        private const float W_MAX = 64f;
        private const float ACCEPT_MARGIN = 0.005f; // 자식이 부모보다 0.5%+ 나을 때만 승격(노이즈 랜덤워크 방지)
        private const int MAX_GEN = 300;

        private static string LearnDir => Path.Combine(
            Path.GetDirectoryName(Application.dataPath), ".claude", "autoplay", "learn");
        private static string WeightsPath => Path.Combine(LearnDir, "weights.json");
        private static string FitnessPath => Path.Combine(LearnDir, "fitness.jsonl");
        private static string CsvPath => Path.Combine(LearnDir, "results.csv"); // ★ 사용자 열람용 결과 데이터

        [Serializable]
        private class LearnState
        {
            public int schema = 1;
            public int startStage = 1, endStage = 1, N = 3;
            public int generation = 0;
            public bool parentEvaluated = false;
            public float sigma = 0.3f;
            public int recentSuccess = 0, recentTotal = 0;
            // 부모(현재 best)
            public float p_cell = 1, p_mission = 8, p_goblin = 12, p_fall = 4;
            public float parentFitness = 0f;
            // 평가 중인 자식(후보)
            public float c_cell = 1, c_mission = 8, c_goblin = 12, c_fall = 4;
            public int passesDone = 0;
            public float candidateAccum = 0f;
            public int totalRuns = 0;
            public int totalFails = 0;      // ★ 누적 실패(게임오버) 횟수
            public int totalContinues = 0;  // ★ 누적 이어하기 사용 횟수
            public int totalGoldSpent = 0;  // ★ 누적 골드 소비
            public bool finished = false;
        }

        static LearningAutoPlay()
        {
            EditorApplication.update += Tick;
            Debug.Log("[Learn] LearningAutoPlay loaded");
        }

        // ── 매 에디터 업데이트: 학습 중이면 후보 W를 MatchingSystem에 재적용 ──
        //    (도메인 리로드로 static W가 기본값으로 초기화돼도 다음 폴에서 즉시 복구 — 비평 #17)
        private static void Tick()
        {
            if (!SessionState.GetBool("AutoPlay_Learn", false)) return;
            if (!EditorApplication.isPlaying) return;
            var st = Load();
            if (st == null || st.finished) return;
            ApplyW(st);
        }

        private static void ApplyW(LearnState st)
        {
            MatchingSystem.W_Cell = st.c_cell;
            MatchingSystem.W_MissionColor = st.c_mission;
            MatchingSystem.W_Goblin = st.c_goblin;
            MatchingSystem.W_FallDamage = st.c_fall;
        }

        // ── 세션 시작 (AutoPlayTester가 "learn" 트리거에서 호출) ──
        public static void BeginSession(int start, int end, int n)
        {
            Directory.CreateDirectory(LearnDir);
            var st = Load() ?? new LearnState();
            // 범위/ N이 바뀌면 학습 상태를 이어가되 진행 카운터만 리셋
            st.startStage = start; st.endStage = end; st.N = Mathf.Max(1, n);
            st.finished = false;
            st.passesDone = 0; st.candidateAccum = 0f;
            if (!st.parentEvaluated)
            {
                // 최초: 부모(기본 가중치)를 그대로 후보로 평가 → 베이스라인 fitness 확보
                st.c_cell = st.p_cell; st.c_mission = st.p_mission;
                st.c_goblin = st.p_goblin; st.c_fall = st.p_fall;
            }
            else
            {
                // 이어하기: 부모에서 변이한 새 후보로 시작
                Mutate(st);
            }
            ApplyW(st);
            Save(st);
            Debug.Log($"[Learn] 세션 시작 stage {start}~{end}, N={st.N}, gen={st.generation}, " +
                      $"parentF={st.parentFitness:F0}, 후보W=({st.c_cell:F1},{st.c_mission:F1},{st.c_goblin:F1},{st.c_fall:F1})");
        }

        // ── 1런(레벨 클리어/게임오버) 완료 시 AutoPlayTester가 호출 ──
        //   goldSpent: 이번 런 골드 소비(마이너스 점수), continues: 이어하기 사용 횟수(실패 데이터).
        public static void OnRunComplete(int stage, bool cleared, int startTurns, int leftMoves,
                                          int missionsDone, int missionsTotal,
                                          int goldSpent = 0, int continues = 0, int gameScore = 0)
        {
            if (!SessionState.GetBool("AutoPlay_Learn", false)) return;
            var st = Load(); if (st == null || st.finished) return;

            float f;
            if (cleared)
                f = CLEAR_BONUS + Mathf.Max(0, leftMoves) * MOVE_COEF;
            else
            {
                float ratio = missionsTotal > 0 ? (float)missionsDone / missionsTotal : 0f;
                f = ratio * PARTIAL_SCALE - GAMEOVER_PENALTY;
            }
            f -= goldSpent * GOLD_PENALTY; // ★ 골드 사용 → 마이너스 점수 전환

            st.candidateAccum += f;
            st.totalRuns++;
            if (!cleared) st.totalFails++;
            st.totalContinues += continues;
            st.totalGoldSpent += goldSpent;
            Save(st);
            // ⚠️ 포맷지정자 바로 뒤 }} 는 포맷스펙에 흡수돼 깨지므로 f를 미리 문자열화한다.
            string fStr = f.ToString("F1");
            AppendFitness("{" + $"\"gen\":{st.generation},\"stage\":{stage},\"cleared\":{(cleared ? "true" : "false")}," +
                          $"\"leftMoves\":{leftMoves},\"missions\":\"{missionsDone}/{missionsTotal}\"," +
                          $"\"goldSpent\":{goldSpent},\"continues\":{continues},\"gameScore\":{gameScore},\"f\":{fStr}" + "}");
            // ★ 사용자 열람용 CSV (엑셀에서 바로 열림)
            AppendCsv(st.generation, stage, cleared, leftMoves, $"{missionsDone}/{missionsTotal}",
                      goldSpent, continues, gameScore, f, st.totalFails, st.totalContinues);
            Debug.Log($"[Learn] run stage={stage} {(cleared ? "CLEAR" : "FAIL")} left={leftMoves} gold=-{goldSpent} cont={continues} score={gameScore} f={f:F0} (누적 {st.candidateAccum:F0})");
        }

        /// <summary>사용자 열람용 결과 CSV — 시간,세대,레벨,결과,잔여이동,골드소비,이어하기,게임점수,학습점수,누적실패,누적이어하기.</summary>
        private static void AppendCsv(int gen, int stage, bool cleared, int leftMoves, string missions,
                                       int goldSpent, int continues, int gameScore, float f, int totalFails, int totalContinues)
        {
            try
            {
                Directory.CreateDirectory(LearnDir);
                if (!File.Exists(CsvPath))
                    File.WriteAllText(CsvPath,
                        "시간,세대,레벨,결과,잔여이동(미션완료시),미션,골드소비,이어하기,게임점수,학습점수,누적실패,누적이어하기\n",
                        System.Text.Encoding.UTF8);
                File.AppendAllText(CsvPath,
                    $"{System.DateTime.Now:MM-dd HH:mm:ss},{gen},{stage},{(cleared ? "클리어" : "실패")},{leftMoves}," +
                    $"{missions},{goldSpent},{continues},{gameScore},{f:F0},{totalFails},{totalContinues}\n",
                    System.Text.Encoding.UTF8);
            }
            catch { }
        }

        // ── 범위 1패스(start~end) 완주 시 호출 → N패스 모이면 ES 세대 진행 ──
        public static void OnPassComplete()
        {
            if (!SessionState.GetBool("AutoPlay_Learn", false)) return;
            var st = Load(); if (st == null || st.finished) return;
            st.passesDone++;
            if (st.passesDone < st.N) { Save(st); return; }

            // 후보 평가 종료 → fitness = 패스 평균
            float candF = st.candidateAccum / st.N;
            bool accepted = false;
            if (!st.parentEvaluated)
            {
                st.parentFitness = candF;
                st.parentEvaluated = true;
                Debug.Log($"[Learn] gen{st.generation} 베이스라인 부모 fitness={candF:F0} 확정");
            }
            else
            {
                st.recentTotal++;
                if (candF > st.parentFitness * (1f + ACCEPT_MARGIN))
                {
                    // 자식 승격
                    st.p_cell = st.c_cell; st.p_mission = st.c_mission;
                    st.p_goblin = st.c_goblin; st.p_fall = st.c_fall;
                    st.parentFitness = candF;
                    st.recentSuccess++;
                    accepted = true;
                }
                // 1/5 성공률 규칙(최근 5세대마다 σ 적응)
                if (st.recentTotal >= 5)
                {
                    float rate = (float)st.recentSuccess / st.recentTotal;
                    st.sigma = Mathf.Clamp(st.sigma * (rate > 0.2f ? 1.22f : 0.82f), SIGMA_MIN, SIGMA_MAX);
                    st.recentSuccess = 0; st.recentTotal = 0;
                }
                Debug.Log($"[Learn] gen{st.generation} 후보F={candF:F0} vs 부모F={st.parentFitness:F0} " +
                          $"→ {(accepted ? "승격✓" : "기각")} σ={st.sigma:F2}");
            }

            st.generation++;
            st.candidateAccum = 0f; st.passesDone = 0;
            if (st.generation >= MAX_GEN)
            {
                st.finished = true;
                // 종료 시 best(부모) 적용
                st.c_cell = st.p_cell; st.c_mission = st.p_mission;
                st.c_goblin = st.p_goblin; st.c_fall = st.p_fall;
                ApplyW(st); Save(st);
                Debug.Log($"[Learn] === 학습 종료(gen {MAX_GEN}) best W=({st.p_cell:F1},{st.p_mission:F1},{st.p_goblin:F1},{st.p_fall:F1}) F={st.parentFitness:F0} ===");
                return;
            }
            // 다음 후보 변이
            Mutate(st);
            ApplyW(st);
            Save(st);
            Debug.Log($"[Learn] gen{st.generation} 새 후보W=({st.c_cell:F1},{st.c_mission:F1},{st.c_goblin:F1},{st.c_fall:F1})");
        }

        // ── 부모에서 자식 변이 (가우시안, 클램프, 미션색 하한) ──
        private static void Mutate(LearnState st)
        {
            st.c_cell = Clamp(st.p_cell + Gauss() * st.sigma * Mathf.Max(st.p_cell, 1f));
            st.c_mission = Clamp(st.p_mission + Gauss() * st.sigma * Mathf.Max(st.p_mission, 1f));
            st.c_goblin = Clamp(st.p_goblin + Gauss() * st.sigma * Mathf.Max(st.p_goblin, 1f));
            st.c_fall = Clamp(st.p_fall + Gauss() * st.sigma * Mathf.Max(st.p_fall, 1f));
            // 미션색은 항상 일반셀 이상(미션 신호 0붕괴 방지 — 비평 #1)
            if (st.c_mission < st.c_cell) st.c_mission = st.c_cell;
        }

        private static float Clamp(float v) => Mathf.Clamp(v, 0f, W_MAX);

        // Box-Muller 표준정규
        private static float Gauss()
        {
            float u1 = Mathf.Max(1e-6f, UnityEngine.Random.value);
            float u2 = UnityEngine.Random.value;
            return Mathf.Sqrt(-2f * Mathf.Log(u1)) * Mathf.Cos(2f * Mathf.PI * u2);
        }

        // ── 영속 ──
        private static LearnState Load()
        {
            try
            {
                if (!File.Exists(WeightsPath)) return null;
                return JsonUtility.FromJson<LearnState>(File.ReadAllText(WeightsPath));
            }
            catch { return null; }
        }

        private static void Save(LearnState st)
        {
            try
            {
                Directory.CreateDirectory(LearnDir);
                File.WriteAllText(WeightsPath, JsonUtility.ToJson(st, true));
            }
            catch { }
        }

        private static void AppendFitness(string json)
        {
            try { Directory.CreateDirectory(LearnDir); File.AppendAllText(FitnessPath, json + "\n"); }
            catch { }
        }

        // ── 상태 조회(리포트/디버그용) ──
        public static bool IsFinished() { var st = Load(); return st == null || st.finished; }
    }
}
#endif
