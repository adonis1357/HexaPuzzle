using UnityEngine;
using System;

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
        [SerializeField] private float comboResetTime = 2f;
        
        // 현재 점수
        private int currentScore = 0;
        private int highScore = 0;
        private int totalGoldEarned = 0;
        
        // 콤보 시스템
        private int currentCombo = 0;
        private float lastMatchTime = 0f;
        
        // 이벤트
        public event Action<int> OnScoreChanged;
        public event Action<int> OnComboChanged;
        public event Action<int, Vector3> OnScorePopup; // 점수, 위치
        
        // 프로퍼티
        public int CurrentScore => currentScore;
        public int HighScore => highScore;
        public int CurrentCombo => currentCombo;
        public float ComboMultiplier => comboMultiplierBase + (currentCombo * comboMultiplierIncrement);
        
        private const string HIGH_SCORE_KEY = "HighScore";
        private const string TOTAL_GOLD_KEY = "TotalGold";
        
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
        /// 점수 추가
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
        /// 점수 리셋 (스테이지 재시작)
        /// </summary>
        public void ResetScore()
        {
            currentScore = 0;
            currentCombo = 0;
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
    }
}
