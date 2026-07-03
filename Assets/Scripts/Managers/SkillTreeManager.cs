using UnityEngine;
using System;
using System.Collections.Generic;
using System.Globalization;
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
    /// 레벨 기반 순차 해금 + 최초 클리어 SP/골드 보상 시스템
    /// </summary>
    public class SkillTreeManager : MonoBehaviour
    {
        public static SkillTreeManager Instance { get; private set; }

        // 스킬 포인트
        private int skillPoints = 0;
        public int SkillPoints => skillPoints;

        // 해금된 스킬 목록
        private HashSet<SkillType> unlockedSkills = new HashSet<SkillType>();
        // ★ 오렌지 리워드 등 '비용 면제' 경로로 해금된 스킬 (감사 H2).
        //   ResetAllSkills 환급에서 제외 — 추적하지 않으면 무료 해금 → 로비 복귀 → 정가 환급의 무한 재화 파밍이 가능.
        private HashSet<SkillType> freeUnlockedSkills = new HashSet<SkillType>();

        // 이벤트
        public event System.Action<int> OnSkillPointsChanged;        // (현재 SP)
        public event System.Action<SkillType> OnSkillUnlocked;       // (해금된 스킬)
        public event System.Action OnSkillTreeReset;                  // 전체 초기화

        // 저장 키
        private const string SP_KEY = "SkillPoints";
        private const string SKILL_PREFIX = "Skill_";
        private const string FREE_SKILL_PREFIX = "SkillFree_"; // 무료 해금 플래그 (환급 제외 추적)
        private const string FIRST_CLEAR_PREFIX = "FirstClear_";     // 최초 클리어 기록
        private const string SP_INITIALIZED_KEY = "SkillTree_SPInitialized"; // 소급 SP 지급 여부

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
            // SP 경제 폐기(런별 로그라이크) — 소급 SP 지급 비활성. EnsureRetroactiveSP() 호출 제거.
        }

        // ============================================================
        // 최초 클리어 추적 시스템
        // ============================================================

        /// <summary>
        /// 레벨 최초 클리어 여부 확인
        /// </summary>
        public bool IsFirstClear(int level)
        {
            return JewelsHexaPuzzle.Utils.SecurePrefs.GetInt(FIRST_CLEAR_PREFIX + level, 0) == 0;
        }

        /// <summary>
        /// 레벨 최초 클리어 기록 (다시 클리어해도 보상 없음)
        /// </summary>
        public void MarkFirstClear(int level)
        {
            JewelsHexaPuzzle.Utils.SecurePrefs.SetInt(FIRST_CLEAR_PREFIX + level, 1);
            PlayerPrefs.Save();
        }

        /// <summary>
        /// 최고 클리어 레벨 반환 (FirstClear 마킹 기준)
        /// </summary>
        public int GetHighestClearedLevel()
        {
            int highest = 0;
            for (int i = 1; i <= 300; i++)
            {
                if (JewelsHexaPuzzle.Utils.SecurePrefs.GetInt(FIRST_CLEAR_PREFIX + i, 0) == 1)
                    highest = i;
            }
            return highest;
        }

        /// <summary>
        /// 플레이어가 도달한 최고 레벨 반환 — 클리어 기록 + 해금된 레벨(Level_N_Unlocked) 모두 고려
        /// 에디터 레벨 활성화 버튼으로 해금해도 반영됨
        /// </summary>
        public int GetHighestReachedLevel()
        {
            int highest = GetHighestClearedLevel();
            // Level_N_Unlocked 키도 검사 (에디터 활성화 버튼 경로)
            for (int i = 1; i <= 300; i++)
            {
                if (PlayerPrefs.GetInt("Level_" + i + "_Unlocked", i == 1 ? 1 : 0) == 1)
                {
                    if (i > highest) highest = i;
                }
            }
            return highest;
        }

        /// <summary>
        /// 스킬트리 오픈 시 소급 SP 지급 (레벨 21 도달 시 이전 클리어 분 포함 20SP)
        /// 한 번만 실행
        /// </summary>
        private void EnsureRetroactiveSP()
        {
            if (JewelsHexaPuzzle.Utils.SecurePrefs.GetInt(SP_INITIALIZED_KEY, 0) == 1) return;

            // 이미 클리어한 레벨 수 계산
            int clearedCount = 0;
            for (int i = 1; i <= 300; i++)
            {
                if (JewelsHexaPuzzle.Utils.SecurePrefs.GetInt(FIRST_CLEAR_PREFIX + i, 0) == 1)
                    clearedCount++;
            }

            // 클리어 기록이 없으면 레벨 해금 수로 추정
            if (clearedCount == 0)
            {
                // ★ 최고 해금 레벨 먼저 계산 (감사 M16)
                //   레벨 N 해금 = N-1 클리어이므로, 최고 해금 레벨 N 자체는 아직 미클리어.
                //   (이전: 해금된 전 레벨에 FirstClear 마킹 → 최고 레벨 실제 첫 클리어 시
                //    IsFirstClear가 false로 나와 최초 클리어 보상 SP/골드가 영영 미지급)
                int maxUnlocked = 0;
                for (int i = 1; i <= 300; i++)
                {
                    string unlockKey = "Level_" + i + "_Unlocked";
                    if (PlayerPrefs.GetInt(unlockKey, i == 1 ? 1 : 0) == 1)
                        maxUnlocked = i;
                }

                // 최초 클리어 마킹 (소급) — 최고 해금 레벨(미클리어)은 제외
                for (int i = 1; i < maxUnlocked; i++)
                {
                    JewelsHexaPuzzle.Utils.SecurePrefs.SetInt(FIRST_CLEAR_PREFIX + i, 1);
                }
                // N해금 = N-1 클리어
                clearedCount = Mathf.Max(0, maxUnlocked - 1);
            }

            // SP 소급 지급: 클리어 레벨 수만큼 (최소 0)
            if (clearedCount > 0 && skillPoints == 0)
            {
                skillPoints = clearedCount;
                OnSkillPointsChanged?.Invoke(skillPoints);
                SaveSkillData();
                Debug.Log($"[SkillTreeManager] 소급 SP 지급: {clearedCount}SP (클리어 {clearedCount}레벨)");
            }

            JewelsHexaPuzzle.Utils.SecurePrefs.SetInt(SP_INITIALIZED_KEY, 1);
            PlayerPrefs.Save();
        }

        /// <summary>
        /// 최초 클리어 골드 보상 계산
        /// 1~100: 50G, 101~200: 60G, 201~300: 70G, ...
        /// </summary>
        public int GetFirstClearGoldReward(int level)
        {
            int tier = (level - 1) / 100; // 0, 1, 2, ...
            return 50 + tier * 10;
        }

        /// <summary>
        /// 스테이지 클리어 보상 계산 및 지급
        /// </summary>
        /// <returns>(골드 보상, SP 보상)</returns>
        public (int goldReward, int spReward) CalculateAndGrantClearRewards(int level, int remainingMoves)
        {
            int goldReward = 0;
            int spReward = 0;

            bool isFirst = IsFirstClear(level);

            if (isFirst)
            {
                // 최초 클리어: 기본 보상 + 남은 이동횟수 보너스
                goldReward = GetFirstClearGoldReward(level) + remainingMoves;
                MarkFirstClear(level);
            }
            else
            {
                // 재클리어: 남은 이동횟수 × 1골드만
                goldReward = remainingMoves;
            }

            // SP 경제 폐기(런별 로그라이크 모델) — SP 미지급. spReward는 호환 위해 0 반환.
            Debug.Log($"[SkillTreeManager] 레벨{level} 보상: 골드={goldReward} (최초={isFirst}), 남은턴={remainingMoves}");
            return (goldReward, spReward);
        }

        // ============================================================
        // 레벨 기반 스킬 카테고리 해금 확인
        // ============================================================

        /// <summary>
        /// 특정 스킬의 카테고리가 현재 플레이어 레벨에서 해금되었는지 확인
        /// (에디터 레벨 활성화 버튼으로 해금된 레벨도 반영)
        /// </summary>
        public bool IsCategoryAvailable(SkillType skillType)
        {
            int highest = GetHighestReachedLevel();
            return SkillUnlockSchedule.IsCategoryUnlocked(skillType, highest);
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
        /// 스킬 잠금 (에디터 전용) — 해금 목록에서 제거. SP/골드 환급 없음.
        /// </summary>
        public void LockSkill(SkillType skillType)
        {
            if (!unlockedSkills.Contains(skillType)) return;
            unlockedSkills.Remove(skillType);
            SaveSkillData();
            Debug.Log($"[SkillTreeManager] 스킬 잠금: {skillType}");
        }

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

            // ★ 카테고리 레벨 게이팅 체크 (Lv1 첫 스킬 기준)
            if (!IsCategoryAvailable(skillType))
            {
                int reqLv = GetCategoryRequiredLevel(skillType);
                Debug.Log($"[SkillTreeManager] 레벨 부족(카테고리): {nodeData.skillName} 필요 레벨={reqLv}");
                return false;
            }

            // ★ 개별 스킬 레벨 게이트 (Lv2/Lv3도 자체 requiredLevel 적용)
            if (nodeData.requiredLevel > 0)
            {
                int reached = GetHighestReachedLevel();
                if (reached < nodeData.requiredLevel)
                {
                    Debug.Log($"[SkillTreeManager] 레벨 부족(개별): {nodeData.skillName} 필요 레벨={nodeData.requiredLevel}, 도달={reached}");
                    return false;
                }
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

        // ============================================================
        // 카테고리 체인 + 오렌지 리워드 강제 해금 헬퍼
        // ============================================================

        /// <summary>13 카테고리 × Lv1~3 체인 (1레벨 → 2레벨 → 3레벨 순서)</summary>
        public static readonly SkillType[][] CategoryChains = new[]
        {
            new[] { SkillType.DrillMove1,         SkillType.DrillMove2,         SkillType.DrillMove3 },
            new[] { SkillType.BombMove1,          SkillType.BombMove2,          SkillType.BombMove3 },
            new[] { SkillType.BombKnockback1,     SkillType.BombKnockback2,     SkillType.BombKnockback3 },
            new[] { SkillType.BombDamage1,        SkillType.BombDamage2,        SkillType.BombDamage3 },
            new[] { SkillType.DrillDamage1,       SkillType.DrillDamage2,       SkillType.DrillDamage3 },
            new[] { SkillType.DrillCushion1,      SkillType.DrillCushion2,      SkillType.DrillCushion3 },
            new[] { SkillType.DroneTargetDamage1, SkillType.DroneTargetDamage2, SkillType.DroneTargetDamage3 },
            new[] { SkillType.HammerLevel1,       SkillType.HammerLevel2,       SkillType.HammerLevel3 },
            new[] { SkillType.SwapLevel1,         SkillType.SwapLevel2,         SkillType.SwapLevel3 },
            new[] { SkillType.LineLevel1,         SkillType.LineLevel2,         SkillType.LineLevel3 },
            new[] { SkillType.ChainBomb1,         SkillType.ChainBomb2,         SkillType.ChainBomb3 },
            new[] { SkillType.TargetDamage1,      SkillType.TargetDamage2,      SkillType.TargetDamage3 },
            new[] { SkillType.DrillPenetrate1,    SkillType.DrillPenetrate2,    SkillType.DrillPenetrate3 },
            new[] { SkillType.DroneClone1,        SkillType.DroneClone2,        SkillType.DroneClone3 },
            new[] { SkillType.DirectHit1,         SkillType.DirectHit2,         SkillType.DirectHit3 },
            new[] { SkillType.AdjacentHit1,       SkillType.AdjacentHit2,       SkillType.AdjacentHit3 },
            new[] { SkillType.FallCritChance1,    SkillType.FallCritChance2,    SkillType.FallCritChance3 },
            new[] { SkillType.FallCritDamage1,    SkillType.FallCritDamage2,    SkillType.FallCritDamage3 },
        };

        /// <summary>
        /// 각 카테고리에서 "다음으로 학습할 수 있는" 스킬을 모아 반환.
        /// 카테고리 자체가 레벨 게이팅으로 비해금 상태면 제외.
        /// 카테고리 전부 해금됐으면 제외. 결과 리스트에서 랜덤 N개를 뽑아 오렌지 리워드 선택지로 사용.
        /// </summary>
        public List<SkillType> GetOrangeRewardCandidates()
        {
            var list = new List<SkillType>();
            foreach (var chain in CategoryChains)
            {
                if (chain.Length == 0) continue;
                // 레벨 게이팅 제거(런별 로그라이크): 모든 카테고리의 다음 미해금 스킬을 후보로.
                //   등장 조절은 희귀도 확률(고급60/희귀30/전설10) + 체인max 클램프만 담당.
                foreach (var s in chain)
                {
                    if (!IsSkillUnlocked(s))
                    {
                        list.Add(s);
                        break; // 한 카테고리당 1개만 (다음 레벨)
                    }
                }
            }
            return list;
        }

        /// <summary>
        /// 비용 면제 강제 해금 (오렌지 리워드 전용). 선행/레벨 검증은 유지.
        /// 성공 시 SP/골드 차감 없이 해금만 처리.
        /// </summary>
        public bool ForceUnlockSkill(SkillType skillType)
        {
            var nodeData = SkillTreeDefinition.GetSkill(skillType);
            if (nodeData == null) return false;
            if (IsSkillUnlocked(skillType)) return false;
            // 레벨 게이팅 제거(런별 로그라이크): 카테고리/requiredLevel 검사 없음. 선행 스킬만 검증.
            if (nodeData.prerequisite != SkillType.None && !IsSkillUnlocked(nodeData.prerequisite)) return false;

            unlockedSkills.Add(skillType);
            freeUnlockedSkills.Add(skillType); // 비용 면제 해금(런별, 환급 없음)
            OnSkillUnlocked?.Invoke(skillType);
            SaveSkillData();
            Debug.Log($"[SkillTreeManager] 리워드 해금: {nodeData.skillName} (런별, 레벨 게이팅 없음)");
            return true;
        }

        /// <summary>
        /// 스킬 노드 상태 조회 (레벨 게이팅 포함)
        /// </summary>
        public SkillState GetSkillState(SkillType skillType)
        {
            if (IsSkillUnlocked(skillType))
                return SkillState.Unlocked;

            var nodeData = SkillTreeDefinition.GetSkill(skillType);
            if (nodeData == null) return SkillState.Locked;

            // ★ 카테고리 레벨 게이팅 체크 (Lv1 첫 스킬 기준)
            if (!IsCategoryAvailable(skillType))
                return SkillState.Locked;

            // ★ 개별 스킬 레벨 게이트 (Lv2/Lv3도 자체 requiredLevel 적용)
            if (nodeData.requiredLevel > 0 && GetHighestReachedLevel() < nodeData.requiredLevel)
                return SkillState.Locked;

            // 선행 스킬 체크
            if (nodeData.prerequisite != SkillType.None && !IsSkillUnlocked(nodeData.prerequisite))
                return SkillState.Locked;

            return SkillState.Available;
        }

        /// <summary>
        /// 스킬 카테고리의 요구 레벨 반환 (UI 표시용)
        /// </summary>
        public int GetCategoryRequiredLevel(SkillType skillType)
        {
            SkillType firstSkill = SkillUnlockSchedule.GetCategoryFirstSkill(skillType);
            return SkillUnlockSchedule.GetRequiredLevel(firstSkill);
        }

        /// <summary>
        /// 현재 SP/골드로 즉시 배울 수 있는 스킬이 하나라도 있는지 검사.
        /// 로비의 스킬트리 버튼 알림 인디케이터(빨간 점) 표시 조건.
        /// 조건: 미해금 + 레벨 게이트 통과 + 선행 스킬 해금 + SP 충분 + 골드 충분.
        /// </summary>
        public bool HasAnyLearnableSkill()
        {
            int curGold = (GameManager.Instance != null) ? GameManager.Instance.CurrentGold : 0;
            var allSkills = SkillTreeDefinition.GetAllSkills();
            foreach (var skill in allSkills)
            {
                if (skill == null) continue;
                // GetSkillState가 미해금/카테고리/선행 스킬을 모두 검사 → Available일 때만 후속 자원 검사
                if (GetSkillState(skill.skillType) != SkillState.Available) continue;
                if (skillPoints < skill.skillPointCost) continue;
                if (curGold < skill.goldCost) continue;
                return true;
            }
            return false;
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

        /// <summary>
        /// 현재 해금된 폭탄 이동 최대 범위 반환 (0 = 미해금)
        /// </summary>
        public int GetBombMoveRange()
        {
            if (IsSkillUnlocked(SkillType.BombMove3)) return 3;
            if (IsSkillUnlocked(SkillType.BombMove2)) return 2;
            if (IsSkillUnlocked(SkillType.BombMove1)) return 1;
            return 0;
        }

        /// <summary>
        /// 현재 해금된 폭탄 넉백 추가 거리 반환 (0 = 미해금)
        /// </summary>
        public int GetBombKnockbackBonus()
        {
            if (IsSkillUnlocked(SkillType.BombKnockback3)) return 3;
            if (IsSkillUnlocked(SkillType.BombKnockback2)) return 2;
            if (IsSkillUnlocked(SkillType.BombKnockback1)) return 1;
            return 0;
        }

        /// <summary>
        /// 현재 해금된 폭탄 데미지 추가량 반환 (0 = 미해금)
        /// </summary>
        public int GetBombDamageBonus()
        {
            if (IsSkillUnlocked(SkillType.BombDamage3)) return 3;
            if (IsSkillUnlocked(SkillType.BombDamage2)) return 2;
            if (IsSkillUnlocked(SkillType.BombDamage1)) return 1;
            return 0;
        }

        /// <summary>
        /// 현재 해금된 드릴 추가 발사체 수 (0~3)
        /// </summary>
        public int GetDrillDamageBonus()
        {
            if (IsSkillUnlocked(SkillType.DrillDamage3)) return 3;
            if (IsSkillUnlocked(SkillType.DrillDamage2)) return 2;
            if (IsSkillUnlocked(SkillType.DrillDamage1)) return 1;
            return 0;
        }


        /// <summary>
        /// 현재 해금된 드론 타겟 데미지 추가량 반환 (0 = 미해금)
        /// </summary>
        public int GetDroneTargetDamageBonus()
        {
            if (IsSkillUnlocked(SkillType.DroneTargetDamage3)) return 3;
            if (IsSkillUnlocked(SkillType.DroneTargetDamage2)) return 2;
            if (IsSkillUnlocked(SkillType.DroneTargetDamage1)) return 1;
            return 0;
        }

        /// <summary>드릴 쿠션 반사 횟수 (0=미해금, 1~3)</summary>
        public int GetDrillCushionLevel()
        {
            if (IsSkillUnlocked(SkillType.DrillCushion3)) return 3;
            if (IsSkillUnlocked(SkillType.DrillCushion2)) return 2;
            if (IsSkillUnlocked(SkillType.DrillCushion1)) return 1;
            return 0;
        }

        /// <summary>망치 아이템 레벨 (0=미해금, 1~3)</summary>
        public int GetHammerLevel()
        {
            if (IsSkillUnlocked(SkillType.HammerLevel3)) return 3;
            if (IsSkillUnlocked(SkillType.HammerLevel2)) return 2;
            if (IsSkillUnlocked(SkillType.HammerLevel1)) return 1;
            return 0;
        }

        /// <summary>스왑 아이템 레벨 (0=미해금, 1~3)</summary>
        public int GetSwapLevel()
        {
            if (IsSkillUnlocked(SkillType.SwapLevel3)) return 3;
            if (IsSkillUnlocked(SkillType.SwapLevel2)) return 2;
            if (IsSkillUnlocked(SkillType.SwapLevel1)) return 1;
            return 0;
        }

        /// <summary>라인 아이템 레벨 (0=미해금, 1~3)</summary>
        public int GetLineLevel()
        {
            if (IsSkillUnlocked(SkillType.LineLevel3)) return 3;
            if (IsSkillUnlocked(SkillType.LineLevel2)) return 2;
            if (IsSkillUnlocked(SkillType.LineLevel1)) return 1;
            return 0;
        }

        /// <summary>연쇄폭탄 레벨 (0=미해금, 1~3) — 소형 폭탄 투척 개수</summary>
        public int GetChainBombLevel()
        {
            if (IsSkillUnlocked(SkillType.ChainBomb3)) return 3;
            if (IsSkillUnlocked(SkillType.ChainBomb2)) return 2;
            if (IsSkillUnlocked(SkillType.ChainBomb1)) return 1;
            return 0;
        }

        /// <summary>드릴 관통 스킬 리워드 레벨 (0=미해금, 1~3) — 기본 관통 1에 더해지는 보너스. 실제 관통 = 1 + 이 값(최대 4).</summary>
        public int GetDrillPenetrateLevel()
        {
            if (IsSkillUnlocked(SkillType.DrillPenetrate3)) return 3;
            if (IsSkillUnlocked(SkillType.DrillPenetrate2)) return 2;
            if (IsSkillUnlocked(SkillType.DrillPenetrate1)) return 1;
            return 0;
        }

        /// <summary>타겟 강화 레벨 (0=미해금, 1~3) — 모든 특수 블록 몬스터 데미지 +N</summary>
        public int GetTargetDamageBonus()
        {
            if (IsSkillUnlocked(SkillType.TargetDamage3)) return 3;
            if (IsSkillUnlocked(SkillType.TargetDamage2)) return 2;
            if (IsSkillUnlocked(SkillType.TargetDamage1)) return 1;
            return 0;
        }

        /// <summary>드론 분신 개수 (0=미해금, 1~3) — 추가로 등장하는 드론 수. 총 드론 = 1 + 이 값.</summary>
        public int GetDroneCloneBonus()
        {
            if (IsSkillUnlocked(SkillType.DroneClone3)) return 3;
            if (IsSkillUnlocked(SkillType.DroneClone2)) return 2;
            if (IsSkillUnlocked(SkillType.DroneClone1)) return 1;
            return 0;
        }

        /// <summary>직접 타격 데미지 보너스 (0=미해금, +2/+4/+6) — 직접 명중(드릴/드론/폭탄 중심) 몬스터 데미지 가산.</summary>
        public int GetDirectHitBonus()
        {
            if (IsSkillUnlocked(SkillType.DirectHit3)) return 6;
            if (IsSkillUnlocked(SkillType.DirectHit2)) return 4;
            if (IsSkillUnlocked(SkillType.DirectHit1)) return 2;
            return 0;
        }

        /// <summary>인접(범위) 타격 데미지 보너스 (0=미해금, +1/+2/+3) — 폭발 인접 칸 등 범위 명중 몬스터 데미지 가산.</summary>
        public int GetAdjacentHitBonus()
        {
            if (IsSkillUnlocked(SkillType.AdjacentHit3)) return 3;
            if (IsSkillUnlocked(SkillType.AdjacentHit2)) return 2;
            if (IsSkillUnlocked(SkillType.AdjacentHit1)) return 1;
            return 0;
        }

        /// <summary>낙하 치명타 확률 보너스 % (0=미해금, +20/+40/+60). 실제 확률 = 10(기본) + 이 값(최대 70).</summary>
        public int GetFallCritChanceBonus()
        {
            if (IsSkillUnlocked(SkillType.FallCritChance3)) return 60;
            if (IsSkillUnlocked(SkillType.FallCritChance2)) return 40;
            if (IsSkillUnlocked(SkillType.FallCritChance1)) return 20;
            return 0;
        }

        /// <summary>낙하 치명타 데미지 보너스 (0=미해금, +1/+2/+3). 실제 치명타 추가 데미지 = 1(기본) + 이 값(최대 +4).</summary>
        public int GetFallCritDamageBonus()
        {
            if (IsSkillUnlocked(SkillType.FallCritDamage3)) return 3;
            if (IsSkillUnlocked(SkillType.FallCritDamage2)) return 2;
            if (IsSkillUnlocked(SkillType.FallCritDamage1)) return 1;
            return 0;
        }

        // ============================================================
        // 초기화 (에디터 디버그)
        // ============================================================

        // ============================================================
        // 스킬 초기화 쿨다운 시스템 (실제 유저용 — 구독 여부에 따라 다른 쿨다운)
        //   - 미구독 상태에서 사용: 7일 (168h) 쿨다운
        //   - 구독 상태에서 사용: 24h 쿨다운
        //   - 구독 중이면 쿨다운 무시 (즉시 사용 가능) — 구독 가입 시 즉시 잠금 해제
        // 디버그 버튼은 이 쿨다운을 무시하고 ResetAllSkills를 직접 호출.
        // ============================================================

        private const string RESET_COOLDOWN_END_KEY = "SkillReset_CooldownEndUtc";
        private const float RESET_COOLDOWN_HOURS_SUBSCRIBED = 24f;
        private const float RESET_COOLDOWN_HOURS_FREE = 168f; // 7 days

        /// <summary>스킬 초기화 쿨다운 종료 시각(UTC). 미설정 시 DateTime.MinValue.</summary>
        public DateTime GetResetCooldownEndUtc()
        {
            string s = PlayerPrefs.GetString(RESET_COOLDOWN_END_KEY, "");
            if (string.IsNullOrEmpty(s)) return DateTime.MinValue;
            // SubscriptionManager와 동일한 RoundtripKind 사용 (Z/오프셋 포함 ISO8601)
            if (DateTime.TryParse(s, CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind, out var dt))
            {
                return dt.Kind == DateTimeKind.Utc ? dt : dt.ToUniversalTime();
            }
            return DateTime.MinValue;
        }

        /// <summary>해금된 스킬이 하나라도 있는지 (= 초기화할 대상이 존재).</summary>
        public bool HasAnyUnlockedSkill => unlockedSkills.Count > 0;

        /// <summary>스킬 초기화 사용 가능 여부 (구독 중이면 항상 true, 아니면 쿨다운 만료 여부).</summary>
        public bool IsResetAvailable
        {
            get
            {
                // 구독 중이면 쿨다운 무시
                if (SubscriptionManager.Instance != null && SubscriptionManager.Instance.IsSubscribed)
                    return true;
                return DateTime.UtcNow >= GetResetCooldownEndUtc();
            }
        }

        /// <summary>잔여 쿨다운 시간 (만료 시 TimeSpan.Zero).</summary>
        public TimeSpan ResetRemainingCooldown
        {
            get
            {
                var end = GetResetCooldownEndUtc();
                var remaining = end - DateTime.UtcNow;
                return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
            }
        }

        /// <summary>
        /// 잔여 쿨다운 포맷:
        ///   - 24h 이상: "Xd Yh"
        ///   - 1h 이상: "Xh Ym"
        ///   - 1h 미만: "Xm Ys"
        /// </summary>
        public static string FormatResetCooldown(TimeSpan remaining)
        {
            if (remaining <= TimeSpan.Zero) return "";

            int totalDays = (int)remaining.TotalDays;
            int totalHours = (int)remaining.TotalHours;
            int hours = remaining.Hours;
            int minutes = remaining.Minutes;
            int seconds = remaining.Seconds;

            if (totalDays >= 1) return $"{totalDays}d {hours}h";
            if (totalHours >= 1) return $"{hours}h {minutes}m";
            return $"{minutes}m {seconds}s";
        }

        /// <summary>
        /// 실제 스킬 초기화 (쿨다운 적용). 구독 여부에 따라 24h/168h 쿨다운 설정.
        /// IsResetAvailable이 false면 아무 동작 안 함.
        /// </summary>
        /// <returns>실제로 초기화가 수행되었는지</returns>
        public bool TryResetWithCooldown()
        {
            if (!IsResetAvailable) return false;

            bool isSubscribed = SubscriptionManager.Instance != null
                                && SubscriptionManager.Instance.IsSubscribed;
            float hours = isSubscribed ? RESET_COOLDOWN_HOURS_SUBSCRIBED
                                       : RESET_COOLDOWN_HOURS_FREE;
            DateTime newEnd = DateTime.UtcNow.AddHours(hours);
            PlayerPrefs.SetString(RESET_COOLDOWN_END_KEY,
                newEnd.ToString("o", CultureInfo.InvariantCulture));
            PlayerPrefs.Save();

            ResetAllSkills();

            Debug.Log($"[SkillTreeManager] 실제 스킬 초기화 완료 (쿨다운 {hours}h, 구독 중: {isSubscribed})");
            return true;
        }

        /// <summary>
        /// 모든 스킬 초기화 + 투자한 SP/골드 반환
        /// </summary>
        public void ResetAllSkills()
        {
            // 투자한 SP 합산 반환
            // ★ 무료 해금(오렌지 리워드) 스킬은 지불한 비용이 0이므로 환급에서 제외 (감사 H2)
            int refundSP = 0;
            int refundGold = 0;
            foreach (var skill in unlockedSkills)
            {
                if (freeUnlockedSkills.Contains(skill)) continue; // 비용 면제 해금 → 환급 없음
                var nodeData = SkillTreeDefinition.GetSkill(skill);
                if (nodeData != null)
                {
                    refundSP += nodeData.skillPointCost;
                    refundGold += nodeData.goldCost;
                }
            }

            unlockedSkills.Clear();
            freeUnlockedSkills.Clear();
            skillPoints += refundSP;
            OnSkillPointsChanged?.Invoke(skillPoints);

            // 골드 반환
            if (GameManager.Instance != null && refundGold > 0)
                GameManager.Instance.AddGold(refundGold);

            OnSkillTreeReset?.Invoke();
            SaveSkillData();

            Debug.Log($"[SkillTreeManager] 스킬 전체 초기화 (SP 반환: +{refundSP}, 골드 반환: +{refundGold})");
        }

        /// <summary>
        /// 런(스테이지 1회 도전) 단위 스킬 초기화 — 리워드로 얻은 스킬을 모두 비운다.
        /// SP/골드 환급·쿨다운 없음(런별 로그라이크 모델, 영구 경제 폐기). 매 스테이지 시작/재시작 시 호출되어
        /// "매 게임마다 처음부터 다시 배움 / 재시작 시 초기화"를 구현한다. (Dev_SkillReward_RunBased.md)
        /// </summary>
        public void ResetRunSkills()
        {
            bool had = unlockedSkills.Count > 0 || freeUnlockedSkills.Count > 0;
            unlockedSkills.Clear();
            freeUnlockedSkills.Clear();
            OnSkillTreeReset?.Invoke();      // 학습 아이콘 바 갱신
            SaveSkillData();                 // 영속 상태도 비워 앱 재시작 후에도 런 시작은 항상 빈 상태
            if (had) Debug.Log("[SkillTreeManager] 런 스킬 초기화 (per-run: 스테이지 시작 시 리워드 스킬 비움)");
        }

        // ============================================================
        // 저장/로드 (PlayerPrefs)
        // ============================================================

        private void SaveSkillData()
        {
            // ★ 보안: SP/스킬 해금 HMAC 서명 저장 (평문 변조로 스킬 무단 해금 차단)
            JewelsHexaPuzzle.Utils.SecurePrefs.SetInt(SP_KEY, skillPoints);

            // 해금 스킬 저장 (각 스킬 타입별 0/1)
            var allSkills = SkillTreeDefinition.GetAllSkills();
            foreach (var skill in allSkills)
            {
                string key = SKILL_PREFIX + (int)skill.skillType;
                JewelsHexaPuzzle.Utils.SecurePrefs.SetInt(key, unlockedSkills.Contains(skill.skillType) ? 1 : 0);
                // ★ 무료 해금 플래그도 함께 저장 — 앱 강제종료 후 재시작해도 환급 제외가 유지되어야 함 (감사 H2)
                JewelsHexaPuzzle.Utils.SecurePrefs.SetInt(FREE_SKILL_PREFIX + (int)skill.skillType,
                    freeUnlockedSkills.Contains(skill.skillType) ? 1 : 0);
            }

            PlayerPrefs.Save();
        }

        private void LoadSkillData()
        {
            // ★ 보안: 서명 검증 로드 (검증 실패 시 0/미해금으로 안전 폴백)
            skillPoints = JewelsHexaPuzzle.Utils.SecurePrefs.GetInt(SP_KEY, 0);
            unlockedSkills.Clear();
            freeUnlockedSkills.Clear();

            var allSkills = SkillTreeDefinition.GetAllSkills();
            foreach (var skill in allSkills)
            {
                string key = SKILL_PREFIX + (int)skill.skillType;
                if (JewelsHexaPuzzle.Utils.SecurePrefs.GetInt(key, 0) == 1)
                {
                    unlockedSkills.Add(skill.skillType);
                    if (JewelsHexaPuzzle.Utils.SecurePrefs.GetInt(FREE_SKILL_PREFIX + (int)skill.skillType, 0) == 1)
                        freeUnlockedSkills.Add(skill.skillType);
                }
            }

            Debug.Log($"[SkillTreeManager] 로드 완료: SP={skillPoints}, 해금 스킬={unlockedSkills.Count}개");
        }
    }
}
