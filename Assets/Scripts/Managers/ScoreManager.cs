using UnityEngine;
using System;
using System.Collections.Generic;
using JewelsHexaPuzzle.Data;

namespace JewelsHexaPuzzle.Managers
{
    /// <summary>
    /// 점수(골드) 관리 시스템
    /// </summary>
    public class ScoreManager : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] private int comboMultiplierBase = 1;
        [SerializeField] private float comboMultiplierIncrement = 0.5f;
        [SerializeField] private float comboResetTime = 3f;
        [SerializeField] private int maxCombo = 4;

        // 현재 점수
        private int currentScore = 0;
        private int highScore = 0;
        private int totalGoldEarned = 0;

        // 콤보 시스템
        private int currentCombo = 0;
        private float lastMatchTime = 0f;

        // 스테이지 요약 추적
        private int stageBaseScore = 0;
        private int maxComboReached = 0;

        // 턴 내 추적 (생성 가산 + 적군 멀티킬)
        private int turnCreationCount = 0;
        private int turnEnemyKillCount = 0;

        // 이벤트
        public event Action<int> OnScoreChanged;
        public event Action<int> OnComboChanged;
        public event Action<int, Vector3> OnScorePopup; // 점수, 위치

        // 프로퍼티
        public int CurrentScore => currentScore;
        public int HighScore => highScore;
        public int CurrentCombo => currentCombo;
        public float ComboMultiplier
        {
            get
            {
                int cappedCombo = currentCombo > maxCombo ? maxCombo : currentCombo;
                return comboMultiplierBase + (cappedCombo * comboMultiplierIncrement);
            }
        }

        private const string HIGH_SCORE_KEY = "HighScore";
        private const string TOTAL_GOLD_KEY = "TotalGold";
        private const string RANKING_KEY = "InfiniteRanking";
        private const string MAX_MOVES_KEY = "MaxMoves";
        private const int MAX_RANKING_COUNT = 10;

        private int maxMoves = 0;
        public int MaxMoves => maxMoves;

        private void Awake()
        {
            LoadSavedData();
        }

        private void Update()
        {
            // 콤보 타이머 체크
            if (currentCombo > 0 && Time.time - lastMatchTime > comboResetTime)
            {
                ResetCombo();
            }
        }

        /// <summary>
        /// 저장된 데이터 로드
        /// </summary>
        private void LoadSavedData()
        {
            highScore = PlayerPrefs.GetInt(HIGH_SCORE_KEY, 0);
            totalGoldEarned = PlayerPrefs.GetInt(TOTAL_GOLD_KEY, 0);
            maxMoves = PlayerPrefs.GetInt(MAX_MOVES_KEY, 0);
        }

        /// <summary>
        /// 데이터 저장
        /// </summary>
        private void SaveData()
        {
            PlayerPrefs.SetInt(HIGH_SCORE_KEY, highScore);
            PlayerPrefs.SetInt(TOTAL_GOLD_KEY, totalGoldEarned);
            PlayerPrefs.Save();
        }

        /// <summary>
        /// 매치 점수 추가 (블록 제거 시 호출)
        /// </summary>
        public void AddMatchScore(int blockCount, int cascadeDepth, Vector3 position)
        {
            int baseScore = ScoreCalculator.CalculateMatchGroupScore(blockCount);
            float cascadeMultiplier = ScoreCalculator.GetCascadeScoreMultiplier(cascadeDepth);
            int cascadedScore = Mathf.RoundToInt(baseScore * cascadeMultiplier);

            // 콤보 적용
            int finalScore = Mathf.RoundToInt(cascadedScore * ComboMultiplier);

            stageBaseScore += baseScore;
            currentScore += finalScore;
            totalGoldEarned += finalScore;

            // 하이스코어 갱신
            if (currentScore > highScore)
            {
                highScore = currentScore;
                SaveData();
            }

            // 콤보 증가
            IncrementCombo();

            OnScoreChanged?.Invoke(currentScore);
            // 점수 팝업 비활성화
            // OnScorePopup?.Invoke(finalScore, position);

            Debug.Log($"Score +{finalScore} (base: {baseScore}, cascade: x{cascadeMultiplier:F1}, combo: x{ComboMultiplier:F1}) Total: {currentScore}");
        }

        /// <summary>
        /// 특수 블록 점수 추가
        /// </summary>
        public void AddSpecialBlockScore(int rawScore, int cascadeDepth, Vector3 position)
        {
            float cascadeMultiplier = ScoreCalculator.GetCascadeScoreMultiplier(cascadeDepth);
            int finalScore = Mathf.RoundToInt(rawScore * cascadeMultiplier * ComboMultiplier);

            currentScore += finalScore;
            totalGoldEarned += finalScore;

            if (currentScore > highScore)
            {
                highScore = currentScore;
                SaveData();
            }

            IncrementCombo();

            OnScoreChanged?.Invoke(currentScore);
            // 점수 팝업 비활성화
            // OnScorePopup?.Invoke(finalScore, position);

            Debug.Log($"Special Score +{finalScore} (raw: {rawScore}, cascade: x{cascadeMultiplier:F1}, combo: x{ComboMultiplier:F1}) Total: {currentScore}");
        }

        /// <summary>
        /// 점수 추가 (하위 호환용)
        /// </summary>
        public void AddScore(int baseScore, Vector3? popupPosition = null)
        {
            // 콤보 적용
            int finalScore = Mathf.RoundToInt(baseScore * ComboMultiplier);

            currentScore += finalScore;
            totalGoldEarned += finalScore;

            // 하이스코어 갱신
            if (currentScore > highScore)
            {
                highScore = currentScore;
                SaveData();
            }

            // 콤보 증가
            IncrementCombo();

            OnScoreChanged?.Invoke(currentScore);

            if (popupPosition.HasValue)
            {
                OnScorePopup?.Invoke(finalScore, popupPosition.Value);
            }

            Debug.Log($"Score +{finalScore} (base: {baseScore}, combo: x{ComboMultiplier:F1}) Total: {currentScore}");
        }

        /// <summary>
        /// 콤보 증가
        /// </summary>
        private void IncrementCombo()
        {
            currentCombo++;
            if (currentCombo > maxComboReached)
                maxComboReached = currentCombo;
            lastMatchTime = Time.time;
            OnComboChanged?.Invoke(currentCombo);
        }

        /// <summary>
        /// 콤보 리셋
        /// </summary>
        private void ResetCombo()
        {
            if (currentCombo > 0)
            {
                currentCombo = 0;
                OnComboChanged?.Invoke(currentCombo);
            }
        }

        /// <summary>
        /// 스테이지 클리어 보너스 계산 및 적용
        /// </summary>
        public StageSummaryData CalculateStageClearBonus(int remainingTurns, int turnLimit)
        {
            int turnsUsed = turnLimit - remainingTurns;
            int remainingTurnsBonus = ScoreCalculator.CalculateRemainingTurnsBonus(remainingTurns);
            int efficiencyBonus = ScoreCalculator.CalculateEfficiencyBonus(turnsUsed, turnLimit);

            // 보너스 적용
            currentScore += remainingTurnsBonus + efficiencyBonus;
            totalGoldEarned += remainingTurnsBonus + efficiencyBonus;

            if (currentScore > highScore)
            {
                highScore = currentScore;
                SaveData();
            }

            int targetScore = turnLimit * 500; // 기본 목표 점수

            StageSummaryData summary = new StageSummaryData
            {
                baseScore = stageBaseScore,
                remainingTurns = remainingTurns,
                remainingTurnsBonus = remainingTurnsBonus,
                efficiencyBonus = efficiencyBonus,
                totalScore = currentScore,
                highScore = highScore,
                isNewHighScore = currentScore >= highScore,
                starRating = CalculateStarRating(targetScore),
                maxComboReached = maxComboReached
            };

            OnScoreChanged?.Invoke(currentScore);

            return summary;
        }

        /// <summary>
        /// 점수 리셋 (스테이지 재시작)
        /// </summary>
        public void ResetScore()
        {
            currentScore = 0;
            currentCombo = 0;
            stageBaseScore = 0;
            maxComboReached = 0;
            turnCreationCount = 0;
            turnEnemyKillCount = 0;
            OnScoreChanged?.Invoke(currentScore);
            OnComboChanged?.Invoke(currentCombo);
        }

        /// <summary>
        /// 현재 스테이지 점수 가져오기
        /// </summary>
        public int GetStageScore()
        {
            return currentScore;
        }

        /// <summary>
        /// 별 등급 계산 (1~3성)
        /// </summary>
        public int CalculateStarRating(int targetScore)
        {
            if (targetScore <= 0) return 3;
            float ratio = (float)currentScore / targetScore;

            if (ratio >= 2f) return 3;
            if (ratio >= 1.5f) return 2;
            if (ratio >= 1f) return 1;
            return 0;
        }

        /// <summary>
        /// 누적 골드 가져오기
        /// </summary>
        public int GetTotalGold()
        {
            return totalGoldEarned;
        }

        /// <summary>
        /// 현재 점수를 랭킹에 저장 (Top 10, 내림차순)
        /// </summary>
        public void SaveScoreToRanking(int score, int moves = 0)
        {
            if (score <= 0) return;

            // 최대 이동횟수 갱신
            if (moves > maxMoves)
            {
                maxMoves = moves;
                PlayerPrefs.SetInt(MAX_MOVES_KEY, maxMoves);
            }

            List<int> scores = new List<int>(GetTopScores());
            scores.Add(score);
            scores.Sort((a, b) => b.CompareTo(a));

            if (scores.Count > MAX_RANKING_COUNT)
                scores.RemoveRange(MAX_RANKING_COUNT, scores.Count - MAX_RANKING_COUNT);

            string[] parts = new string[scores.Count];
            for (int i = 0; i < scores.Count; i++)
                parts[i] = scores[i].ToString();

            PlayerPrefs.SetString(RANKING_KEY, string.Join(",", parts));
            PlayerPrefs.Save();
        }

        /// <summary>
        /// Top 10 점수 배열 반환 (내림차순)
        /// </summary>
        public int[] GetTopScores()
        {
            string data = PlayerPrefs.GetString(RANKING_KEY, "");
            if (string.IsNullOrEmpty(data)) return new int[0];

            string[] parts = data.Split(',');
            List<int> scores = new List<int>();
            for (int i = 0; i < parts.Length; i++)
            {
                int val;
                if (int.TryParse(parts[i], out val) && val > 0)
                    scores.Add(val);
            }
            scores.Sort((a, b) => b.CompareTo(a));
            return scores.ToArray();
        }

        /// <summary>
        /// 해당 점수의 순위 반환 (1부터 시작, 기록 없으면 -1)
        /// </summary>
        public int GetRankForScore(int score)
        {
            if (score <= 0) return -1;

            int[] top = GetTopScores();
            for (int i = 0; i < top.Length; i++)
            {
                if (top[i] == score)
                    return i + 1;
            }
            return top.Length + 1;
        }

        // ============================================================
        // 특수 블록 생성 가산점
        // ============================================================

        /// <summary>
        /// 특수 블록 생성 가산점 추가
        /// </summary>
        public void AddCreationBonus(SpecialBlockType type, int cascadeDepth, Vector3 position)
        {
            int bonus = ScoreCalculator.GetSpecialBlockCreationBonus(type);
            if (bonus <= 0) return;

            float cascadeMultiplier = ScoreCalculator.GetCascadeScoreMultiplier(cascadeDepth);
            int finalScore = Mathf.RoundToInt(bonus * cascadeMultiplier);

            currentScore += finalScore;
            totalGoldEarned += finalScore;
            turnCreationCount++;

            if (currentScore > highScore) { highScore = currentScore; SaveData(); }

            OnScoreChanged?.Invoke(currentScore);
            OnScorePopup?.Invoke(finalScore, position);

            Debug.Log($"Creation Bonus +{finalScore} ({type}, cascade x{cascadeMultiplier:F1})");
        }

        // ============================================================
        // 적군 파괴 점수
        // ============================================================

        /// <summary>
        /// 적군 파괴 점수 추가
        /// </summary>
        public void AddEnemyScore(EnemyType type, RemovalMethod method, RemovalCondition condition, Vector3 position)
        {
            int score = ScoreCalculator.CalculateEnemyScore(type, method, condition);
            if (score <= 0) return;  // 가시+매칭 → 0점

            currentScore += score;
            totalGoldEarned += score;
            turnEnemyKillCount++;

            if (currentScore > highScore) { highScore = currentScore; SaveData(); }

            OnScoreChanged?.Invoke(currentScore);
            OnScorePopup?.Invoke(score, position);

            Debug.Log($"Enemy Score +{score} ({type}, {method}, {condition})");
        }

        // ============================================================
        // 턴 종료 보너스
        // ============================================================

        /// <summary>
        /// 턴 종료 시 복수 생성 + 멀티킬 보너스 적용
        /// </summary>
        public int ApplyTurnEndBonuses()
        {
            int bonus = 0;

            // 복수 생성 보너스
            if (turnCreationCount >= 2)
            {
                int creationBonus = ScoreCalculator.GetMultiCreationBonus(turnCreationCount);
                bonus += creationBonus;
                Debug.Log($"Multi-creation bonus: +{creationBonus} ({turnCreationCount}개)");
            }

            // 멀티킬 보너스
            if (turnEnemyKillCount >= 2)
            {
                int killBonus = ScoreCalculator.GetMultiKillBonus(turnEnemyKillCount);
                bonus += killBonus;
                Debug.Log($"Multi-kill bonus: +{killBonus} ({turnEnemyKillCount}마리)");
            }

            if (bonus > 0)
            {
                currentScore += bonus;
                totalGoldEarned += bonus;
                if (currentScore > highScore) { highScore = currentScore; SaveData(); }
                OnScoreChanged?.Invoke(currentScore);
            }

            // 리셋
            turnCreationCount = 0;
            turnEnemyKillCount = 0;

            return bonus;
        }
    }

    /// <summary>
    /// 스테이지 클리어 요약 데이터
    /// </summary>
    public struct StageSummaryData
    {
        public int baseScore;
        public int remainingTurns;
        public int remainingTurnsBonus;
        public int efficiencyBonus;
        public int totalScore;
        public int highScore;
        public bool isNewHighScore;
        public int starRating;
        public int maxComboReached;
    }
}
