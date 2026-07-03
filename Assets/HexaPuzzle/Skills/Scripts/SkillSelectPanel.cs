using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace HexaPuzzle.Skills
{
    /// <summary>
    /// 스킬 선택 패널 — 레벨업 시 후보 N장을 띄우고 하나를 고르게 합니다.
    /// (Skill Upgrade Select.html 의 카드 그리드 + 리롤 포팅)
    ///
    /// 흐름:
    ///   1) 달성 게이지가 가득 차면 게임이 Open() 호출
    ///   2) 후보 풀에서 N개를 뽑아 카드로 표시 (현재 보유 레벨+1 을 제안)
    ///   3) 플레이어가 선택 → 레벨 +1 → onSkillChosen 발생 → 패널 닫힘
    ///
    /// 보유 레벨은 SkillProgress(딕셔너리)에 저장. 게임 세이브와 연동하세요.
    /// </summary>
    public class SkillSelectPanel : MonoBehaviour
    {
        [Header("Data")]
        public SkillCatalog catalog;

        [Header("UI")]
        public Transform        cardParent;   // 카드가 들어갈 레이아웃 그룹
        public SkillSelectCard  cardPrefab;   // 카드 프리팹
        public int              drawCount = 3;
        public GameObject       root;         // 패널 루트(열고 닫기). 비우면 this.gameObject

        [Header("Reroll (선택)")]
        public Button rerollButton;
        public Text   rerollLabel;
        public Text   rerollCost;
        public int    rerollGoldCost = 100;   // 0 = 무료, -1 = 비용 숨김

        [Header("후보 풀 (비우면 카탈로그 전체)")]
        [Tooltip("후보로 등장할 스킬 id 목록. 비우면 catalog.skills 전체에서 뽑습니다.")]
        public List<string> pool = new List<string>();

        /// <summary>스킬 선택 완료. (스킬, 새 레벨)</summary>
        public event Action<SkillDefinition, int> onSkillChosen;
        /// <summary>리롤 요청. true 반환 시 비용 지불 성공으로 간주하고 다시 뽑음.</summary>
        public Func<int, bool> onRerollRequested;

        // 플레이어 보유 레벨 (id → level). 0/없음 = 미보유.
        private readonly Dictionary<string, int> _progress = new Dictionary<string, int>();
        private readonly List<SkillSelectCard> _spawned = new List<SkillSelectCard>();
        private System.Random _rng = new System.Random();

        private GameObject Root => root != null ? root : gameObject;

        private void Awake()
        {
            if (rerollButton != null) rerollButton.onClick.AddListener(Reroll);
            Root.SetActive(false);
        }

        // ─── 진행도 API (세이브와 연동) ─────────────────────────────
        public int LevelOf(string id) => _progress.TryGetValue(id, out var l) ? l : 0;
        public int LevelOf(SkillId id) => LevelOf(SkillIds.ToJsonId(id));
        public void SetLevel(string id, int level) { _progress[id] = level; }
        public IReadOnlyDictionary<string, int> Progress => _progress;
        public void LoadProgress(Dictionary<string, int> saved)
        {
            _progress.Clear();
            if (saved != null) foreach (var kv in saved) _progress[kv.Key] = kv.Value;
        }

        // ─── 열기 ───────────────────────────────────────────────────
        /// <summary>후보를 자동으로 뽑아 패널을 엽니다.</summary>
        public void Open()
        {
            Open(DrawCandidates(drawCount));
        }

        /// <summary>특정 후보 id 목록으로 패널을 엽니다.</summary>
        public void Open(IEnumerable<string> candidateIds)
        {
            ClearCards();
            Root.SetActive(true);

            foreach (var id in candidateIds)
            {
                var def = catalog != null ? catalog.Get(id) : null;
                if (def == null) continue;
                int nextLevel = Mathf.Min(LevelOf(id) + 1, Mathf.Max(1, def.maxLevel));
                var card = Instantiate(cardPrefab, cardParent);
                card.Bind(def, nextLevel);
                card.onChosen += OnCardChosen;
                _spawned.Add(card);
            }
            RefreshReroll();
        }

        public void Close() => Root.SetActive(false);

        // ─── 선택/리롤 ─────────────────────────────────────────────
        private void OnCardChosen(SkillSelectCard card)
        {
            if (card.Skill == null) return;
            string id = card.Skill.id;
            int newLevel = Mathf.Min(LevelOf(id) + 1, Mathf.Max(1, card.Skill.maxLevel));
            _progress[id] = newLevel;
            onSkillChosen?.Invoke(card.Skill, newLevel);
            Close();
        }

        private void Reroll()
        {
            // 비용 처리는 게임에 위임. 콜백이 false 면 리롤 취소.
            if (onRerollRequested != null && !onRerollRequested(Mathf.Max(0, rerollGoldCost))) return;
            Open(DrawCandidates(drawCount));
        }

        // ─── 후보 추첨 ─────────────────────────────────────────────
        /// <summary>풀에서 (최대 레벨 미만인) 스킬 count개를 중복 없이 추첨.</summary>
        public List<string> DrawCandidates(int count)
        {
            var src = new List<string>();
            if (pool != null && pool.Count > 0) src.AddRange(pool);
            else if (catalog != null) foreach (var s in catalog.skills) if (s != null) src.Add(s.id);

            // 이미 최대 레벨인 스킬 제외
            src.RemoveAll(id =>
            {
                var def = catalog != null ? catalog.Get(id) : null;
                return def == null || LevelOf(id) >= Mathf.Max(1, def.maxLevel);
            });

            // Fisher–Yates 셔플 후 앞에서 count개
            for (int i = src.Count - 1; i > 0; i--)
            {
                int j = _rng.Next(i + 1);
                (src[i], src[j]) = (src[j], src[i]);
            }
            if (src.Count > count) src.RemoveRange(count, src.Count - count);
            return src;
        }

        // ─── 내부 ───────────────────────────────────────────────────
        private void RefreshReroll()
        {
            if (rerollLabel != null) rerollLabel.text = "다시 뽑기";
            if (rerollCost != null)
            {
                if (rerollGoldCost < 0)      { rerollCost.text = ""; rerollCost.gameObject.SetActive(false); }
                else if (rerollGoldCost == 0){ rerollCost.text = "무료"; rerollCost.gameObject.SetActive(true); }
                else                         { rerollCost.text = rerollGoldCost + " 골드"; rerollCost.gameObject.SetActive(true); }
            }
        }

        private void ClearCards()
        {
            foreach (var c in _spawned) if (c != null) Destroy(c.gameObject);
            _spawned.Clear();
        }
    }
}
