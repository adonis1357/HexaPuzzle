using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace JewelsHexaPuzzle.Utils
{
    /// <summary>
    /// 모든 숫자 텍스트의 "순차 카운트(롤링)" 연출을 통일하는 유틸.
    ///
    /// 사용:
    ///   NumberRoller.Roll(scoreText, score);                         // "{0}"
    ///   NumberRoller.Roll(goldText, gold, n => $"보유: {n} 골드");    // 접두/접미 포함
    ///   NumberRoller.SetImmediate(turnText, turns);                  // 애니 없이 즉시(초기화/리셋)
    ///
    /// 특징:
    ///   - 증가/감소 동일한 이징(ease-out cubic) + 동일 지속시간으로 톤 통일.
    ///   - 정수 단위로 한 칸씩 변하며 표시(롤링).
    ///   - 중간에 다시 Roll되면 현재 표시값에서 새 목표로 이어서 진행(점프 없음).
    ///   - Time.unscaledDeltaTime 사용 → 일시정지(리워드 모달 등 timeScale=0) 중에도 카운트.
    ///   - Text별 상태를 사전으로 관리(파괴된 Text는 자동 정리).
    /// </summary>
    public class NumberRoller : MonoBehaviour
    {
        public const float DefaultDuration = 0.45f;

        private static NumberRoller _instance;
        private static NumberRoller Instance
        {
            get
            {
                if (_instance == null)
                {
                    var go = new GameObject("NumberRoller");
                    Object.DontDestroyOnLoad(go);
                    _instance = go.AddComponent<NumberRoller>();
                }
                return _instance;
            }
        }

        private class State
        {
            public int displayed;
            public int target;
            public Coroutine co;
            public System.Func<int, string> fmt;
        }

        private readonly Dictionary<Text, State> _states = new Dictionary<Text, State>();

        /// <summary>Text의 표시 숫자를 target까지 순차 카운트. format이 null이면 "{0}"(n.ToString()).
        /// minDuration: 작은 변화도 이 시간 이상 보이게(골드 카운트업처럼 또렷한 연출용, 기본 0.12).</summary>
        /// <param name="perUnit">단위(1)당 지속시간(기본 0.035s). 크게 주면 한 칸씩 또렷이 보임(게이지 등).</param>
        public static void Roll(Text text, int target, System.Func<int, string> format = null,
                                float duration = DefaultDuration, float minDuration = 0.12f, float perUnit = 0.035f)
        {
            if (text == null) return;
            Instance.RollInternal(text, target, format, duration, minDuration, perUnit);
        }

        /// <summary>애니메이션 없이 즉시 표시(초기값/리셋용). 내부 표시 상태도 동기화.</summary>
        public static void SetImmediate(Text text, int value, System.Func<int, string> format = null)
        {
            if (text == null) return;
            Instance.SetImmediateInternal(text, value, format);
        }

        private void RollInternal(Text text, int target, System.Func<int, string> format, float duration, float minDuration, float perUnit = 0.035f)
        {
            if (!_states.TryGetValue(text, out var st))
            {
                int seed = ParseCurrent(text);
                st = new State { displayed = seed, target = seed };
                _states[text] = st;
            }
            st.fmt = format ?? DefaultFormat;

            if (st.displayed == target)
            {
                if (st.co != null) { StopCoroutine(st.co); st.co = null; }
                st.target = target;
                Apply(text, st, target);
                return;
            }
            // ★ 같은 목표로 이미 롤 진행 중이면 재시작하지 않는다 — 반복 호출(UpdateUI 등) 시 t가 리셋되어
            //   진행이 막히던 버그 방지. (목표가 바뀐 경우에만 현재값에서 리타겟)
            if (st.co != null && st.target == target) return;

            st.target = target;
            // ★ 변화량 적응형 지속시간: 단위당 ~0.035s, [minDuration, duration] 범위로 클램프.
            //   작은 변화도 minDuration 이상 보이게(골드처럼 또렷한 카운트업), 큰 변화는 duration 상한까지.
            int delta = Mathf.Abs(target - st.displayed);
            float eff = Mathf.Min(duration, Mathf.Max(minDuration, delta * perUnit));
            if (st.co != null) StopCoroutine(st.co);
            st.co = StartCoroutine(RollCo(text, st, target, eff));
        }

        private void SetImmediateInternal(Text text, int value, System.Func<int, string> format)
        {
            if (!_states.TryGetValue(text, out var st)) { st = new State(); _states[text] = st; }
            if (st.co != null) { StopCoroutine(st.co); st.co = null; }
            st.fmt = format ?? DefaultFormat;
            st.displayed = value;
            st.target = value;
            Apply(text, st, value);
        }

        private IEnumerator RollCo(Text text, State st, int target, float duration)
        {
            int from = st.displayed;
            float t = 0f;
            while (t < duration)
            {
                if (text == null) { _states.Remove(text); yield break; }
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / duration);
                float e = 1f - Mathf.Pow(1f - k, 3f); // ease-out cubic (증가/감소 동일)
                int cur = Mathf.RoundToInt(Mathf.Lerp(from, target, e));
                if (cur != st.displayed)
                {
                    st.displayed = cur;
                    Apply(text, st, cur);
                }
                yield return null;
            }
            st.displayed = target;
            Apply(text, st, target);
            st.co = null;
        }

        private void Apply(Text text, State st, int v)
        {
            if (text != null) text.text = (st.fmt ?? DefaultFormat)(v);
        }

        private static readonly System.Func<int, string> DefaultFormat = n => n.ToString();

        /// <summary>기존 텍스트에서 첫 정수(부호 포함)를 추출 — 첫 Roll 시 시작값 시드.</summary>
        private static int ParseCurrent(Text text)
        {
            if (text == null || string.IsNullOrEmpty(text.text)) return 0;
            var sb = new System.Text.StringBuilder();
            bool started = false, neg = false;
            foreach (char c in text.text)
            {
                if (c >= '0' && c <= '9') { sb.Append(c); started = true; }
                else if (c == '-' && !started) neg = true;
                else if (started) break;
                else neg = false; // 숫자 전의 '-'가 실제 부호가 아니면 리셋
            }
            if (sb.Length == 0) return 0;
            if (!int.TryParse(sb.ToString(), out int val)) return 0;
            return neg ? -val : val;
        }
    }
}
