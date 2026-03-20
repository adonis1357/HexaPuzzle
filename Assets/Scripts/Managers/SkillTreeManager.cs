using UnityEngine;
using System.Collections.Generic;
using JewelsHexaPuzzle.Data;

// ============================================================================
// SkillTreeManager.cs - 스킬 트리 매니저 (싱글톤)
// ============================================================================
// 플레이어의 스킬 해금 상태, 스킬 포인트, 저장/로드를 관리합니다.
// PlayerPrefs 기반 영속 저장.
// ============================================================================

namespace JewelsHexaPuzzle.Managers
{
    /// <summary>
    /// 스킬 트리 매니저 — 스킬 해금 상태 및 스킬 포인트 관리
    /// </summary>
    public class SkillTreeManager : MonoBehaviour
    {
        public static SkillTreeManager Instance { get; private set; }

        // 스킬 포인트
        private int skillPoints = 0;
        public int SkillPoints => skillPoints;

        // 해금된 스킬 목록
        private HashSet<SkillType> unlockedSkills = new HashSet<SkillType>();

        // 이벤트
        public event System.Action<int> OnSkillPointsChanged;        // (현재 SP)
        public event System.Action<SkillType> OnSkillUnlocked;       // (해금된 스킬)
        public event System.Action OnSkillTreeReset;                  // 전체 초기화

        // 저장 키
        private const string SP_KEY = "SkillPoints";
        private const string SKILL_PREFIX = "Skill_";

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else
            {
                Destroy(gameObject);
                return;
            }
            LoadSkillData();
        }

        // ============================================================
        // 스킬 포인트 관리
        // ============================================================

        /// <summary>
        /// 스킬 포인트 추가
        /// </summary>
        public void AddSkillPoints(int amount)
        {
            if (amount <= 0) return;
            skillPoints += amount;
            OnSkillPointsChanged?.Invoke(skillPoints);
            SaveSkillData();
            Debug.Log($"[SkillTreeManager] SP +{amount}, 현재: {skillPoints}");
        }

        /// <summary>
        /// 스킬 포인트 설정 (에디터 디버그용)
        /// </summary>
        public void SetSkillPoints(int amount)
        {
            skillPoints = Mathf.Max(0, amount);
            OnSkillPointsChanged?.Invoke(skillPoints);
            SaveSkillData();
        }

        // ============================================================
        // 스킬 해금
        // ============================================================

        /// <summary>
        /// 스킬 해금 시도 — SP + 골드 소모
        /// </summary>
        /// <returns>해금 성공 여부</returns>
        public bool TryUnlockSkill(SkillType skillType)
        {
            var nodeData = SkillTreeDefinition.GetSkill(skillType);
            if (nodeData == null)
            {
                Debug.LogWarning($"[SkillTreeManager] 존재하지 않는 스킬: {skillType}");
                return false;
            }

            // 이미 해금됨
            if (IsSkillUnlocked(skillType))
            {
                Debug.Log($"[SkillTreeManager] 이미 해금된 스킬: {nodeData.skillName}");
                return false;
            }

            // 선행 스킬 체크
            if (nodeData.prerequisite != SkillType.None && !IsSkillUnlocked(nodeData.prerequisite))
            {
                Debug.Log($"[SkillTreeManager] 선행 스킬 미충족: {nodeData.prerequisite}");
                return false;
            }

            // SP 체크
            if (skillPoints < nodeData.skillPointCost)
            {
                Debug.Log($"[SkillTreeManager] SP 부족: 필요={nodeData.skillPointCost}, 보유={skillPoints}");
                return false;
            }

            // 골드 체크
            if (GameManager.Instance != null && GameManager.Instance.CurrentGold < nodeData.goldCost)
            {
                Debug.Log($"[SkillTreeManager] 골드 부족: 필요={nodeData.goldCost}, 보유={GameManager.Instance.CurrentGold}");
                return false;
            }

            // === 비용 차감 ===
            skillPoints -= nodeData.skillPointCost;
            OnSkillPointsChanged?.Invoke(skillPoints);

            if (GameManager.Instance != null && nodeData.goldCost > 0)
                GameManager.Instance.SpendGold(nodeData.goldCost);

            // === 해금 ===
            unlockedSkills.Add(skillType);
            OnSkillUnlocked?.Invoke(skillType);
            SaveSkillData();

            Debug.Log($"[SkillTreeManager] 스킬 해금: {nodeData.skillName} (SP -{nodeData.skillPointCost}, 골드 -{nodeData.goldCost})");
            return true;
        }

        /// <summary>
        /// 스킬 해금 여부 확인
        /// </summary>
        public bool IsSkillUnlocked(SkillType skillType)
        {
            return unlockedSkills.Contains(skillType);
        }

        /// <summary>
        /// 스킬 노드 상태 조회
        /// </summary>
        public SkillState GetSkillState(SkillType skillType)
        {
            if (IsSkillUnlocked(skillType))
                return SkillState.Unlocked;

            var nodeData = SkillTreeDefinition.GetSkill(skillType);
            if (nodeData == null) return SkillState.Locked;

            // 선행 스킬 체크
            if (nodeData.prerequisite != SkillType.None && !IsSkillUnlocked(nodeData.prerequisite))
                return SkillState.Locked;

            return SkillState.Available;
        }

        // ============================================================
        // 드릴 이동 스킬 조회 (게임플레이 연동)
        // ============================================================

        /// <summary>
        /// 현재 해금된 드릴 이동 최대 범위 반환 (0 = 미해금)
        /// </summary>
        public int GetDrillMoveRange()
        {
            if (IsSkillUnlocked(SkillType.DrillMove3)) return 3;
            if (IsSkillUnlocked(SkillType.DrillMove2)) return 2;
            if (IsSkillUnlocked(SkillType.DrillMove1)) return 1;
            return 0;
        }

        // ============================================================
        // 초기화 (에디터 디버그)
        // ============================================================

        /// <summary>
        /// 모든 스킬 초기화 + 투자한 SP/골드 반환
        /// </summary>
        public void ResetAllSkills()
        {
            // 투자한 SP 합산 반환
            int refundSP = 0;
            int refundGold = 0;
            foreach (var skill in unlockedSkills)
            {
                var nodeData = SkillTreeDefinition.GetSkill(skill);
                if (nodeData != null)
                {
                    refundSP += nodeData.skillPointCost;
                    refundGold += nodeData.goldCost;
                }
            }

            unlockedSkills.Clear();
            skillPoints += refundSP;
            OnSkillPointsChanged?.Invoke(skillPoints);

            // 골드 반환
            if (GameManager.Instance != null && refundGold > 0)
                GameManager.Instance.AddGold(refundGold);

            OnSkillTreeReset?.Invoke();
            SaveSkillData();

            Debug.Log($"[SkillTreeManager] 스킬 전체 초기화 (SP 반환: +{refundSP}, 골드 반환: +{refundGold})");
        }

        // ============================================================
        // 저장/로드 (PlayerPrefs)
        // ============================================================

        private void SaveSkillData()
        {
            PlayerPrefs.SetInt(SP_KEY, skillPoints);

            // 해금 스킬 저장 (각 스킬 타입별 0/1)
            var allSkills = SkillTreeDefinition.GetAllSkills();
            foreach (var skill in allSkills)
            {
                string key = SKILL_PREFIX + (int)skill.skillType;
                PlayerPrefs.SetInt(key, unlockedSkills.Contains(skill.skillType) ? 1 : 0);
            }

            PlayerPrefs.Save();
        }

        private void LoadSkillData()
        {
            skillPoints = PlayerPrefs.GetInt(SP_KEY, 0);
            unlockedSkills.Clear();

            var allSkills = SkillTreeDefinition.GetAllSkills();
            foreach (var skill in allSkills)
            {
                string key = SKILL_PREFIX + (int)skill.skillType;
                if (PlayerPrefs.GetInt(key, 0) == 1)
                    unlockedSkills.Add(skill.skillType);
            }

            Debug.Log($"[SkillTreeManager] 로드 완료: SP={skillPoints}, 해금 스킬={unlockedSkills.Count}개");
        }
    }
}
