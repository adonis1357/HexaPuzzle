using System;
using System.Globalization;
using UnityEngine;

namespace JewelsHexaPuzzle.Managers
{
    /// <summary>
    /// 프리미엄 구독 관리 — 종료 시각(UTC)을 PlayerPrefs에 저장하여 영속화.
    ///   - IsSubscribed: 종료 시각이 현재보다 미래면 true
    ///   - Subscribe(hours): 추가 시 이미 구독 중이면 보너스 10% 시간 자동 적립
    ///   - 구독 시 MPManager.maxMP가 30 → 100으로 상승 (OnSubscriptionChanged 이벤트 통해)
    /// </summary>
    public class SubscriptionManager : MonoBehaviour
    {
        public static SubscriptionManager Instance { get; private set; }

        private const string KEY_END = "Subscription_EndUtc";
        private const string KEY_LASTSEEN = "Subscription_LastSeenUtc"; // ★ 보안: 시계 역행 감지용

        /// <summary>구독 시간 변경 이벤트 (구매 / 만료 시 발생)</summary>
        public event Action OnSubscriptionChanged;

        // ============================================================
        // 구독 정의 (티어별 가격/시간)
        // ============================================================
        public struct Tier
        {
            public int hours;
            public int priceWon;
            public string label;
        }

        public static readonly Tier[] Tiers = new[]
        {
            new Tier { hours = 24,  priceWon = 1100,  label = "24시간" },
            new Tier { hours = 72,  priceWon = 2200,  label = "72시간" },
            new Tier { hours = 168, priceWon = 3300,  label = "7일" },
            new Tier { hours = 720, priceWon = 11000, label = "30일" },
        };

        // ============================================================
        // 라이프사이클
        // ============================================================
        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else if (Instance != this)
            {
                Destroy(gameObject);
                return;
            }
        }

        // ============================================================
        // 상태 조회
        // ============================================================

        private DateTime _lastSeenMem = DateTime.MinValue;

        /// <summary>
        /// ★ 보안: 시계 역행 감지 — 구독 후 기기 시계를 뒤로 돌려 프리미엄을 무한 연장하는 공격 차단.
        /// 마지막 관측 시각보다 현재가 5분 이상 과거면, 역행한 만큼 종료 시각을 앞으로 당긴다(잔여 실시간 보존).
        /// </summary>
        private void ClockGuard()
        {
            DateTime now = DateTime.UtcNow;
            DateTime lastSeen = _lastSeenMem;
            if (lastSeen == DateTime.MinValue)
            {
                string ls = JewelsHexaPuzzle.Utils.SecurePrefs.GetString(KEY_LASTSEEN, "");
                if (!string.IsNullOrEmpty(ls) && DateTime.TryParse(ls, CultureInfo.InvariantCulture,
                        DateTimeStyles.RoundtripKind, out var dt))
                    lastSeen = dt.Kind == DateTimeKind.Utc ? dt : dt.ToUniversalTime();
            }

            if (lastSeen != DateTime.MinValue && now < lastSeen - TimeSpan.FromMinutes(5))
            {
                TimeSpan back = lastSeen - now;
                DateTime end = GetEndUtcRaw();
                if (end > DateTime.MinValue)
                {
                    DateTime pulled = end - back;
                    JewelsHexaPuzzle.Utils.SecurePrefs.SetString(KEY_END,
                        pulled.ToString("o", CultureInfo.InvariantCulture));
                    PlayerPrefs.Save();
                    Debug.LogWarning($"[SubscriptionManager] 시계 역행 감지({back.TotalMinutes:F0}분) → 구독 종료시각 보정");
                }
            }

            if (now > lastSeen)
            {
                bool persist = lastSeen == DateTime.MinValue || (now - lastSeen) > TimeSpan.FromMinutes(1);
                _lastSeenMem = now;
                if (persist)
                {
                    JewelsHexaPuzzle.Utils.SecurePrefs.SetString(KEY_LASTSEEN,
                        now.ToString("o", CultureInfo.InvariantCulture));
                    PlayerPrefs.Save();
                }
            }
        }

        /// <summary>현재 구독 종료 시각(UTC). 미구독이면 DateTime.MinValue.</summary>
        public DateTime GetEndUtc()
        {
            ClockGuard();
            return GetEndUtcRaw();
        }

        /// <summary>서명 검증만 수행하는 순수 로드(시계 가드 미포함 — ClockGuard 내부 재귀 방지).</summary>
        private DateTime GetEndUtcRaw()
        {
            // ★ 보안: HMAC 서명 검증 로드 — 평문 XML 편집으로 종료시각을 미래로 위조하는 영구 프리미엄 차단
            string s = JewelsHexaPuzzle.Utils.SecurePrefs.GetString(KEY_END, "");
            if (string.IsNullOrEmpty(s)) return DateTime.MinValue;
            // ★ "o" 포맷은 타임존 오프셋(Z 또는 ±HH:mm)을 포함하므로 RoundtripKind 단독 사용
            //   AssumeUniversal/AssumeLocal/AdjustToUniversal는 RoundtripKind와 결합 불가 (.NET 제약)
            if (DateTime.TryParse(s, CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind, out var dt))
            {
                // 파싱 결과가 Local일 수 있으므로 명시적으로 UTC로 변환
                return dt.Kind == DateTimeKind.Utc ? dt : dt.ToUniversalTime();
            }
            return DateTime.MinValue;
        }

        /// <summary>현재 구독 중인지 (종료 시각이 현재보다 미래면 true)</summary>
        public bool IsSubscribed => GetEndUtc() > DateTime.UtcNow;

        /// <summary>남은 구독 시간 (만료 시 TimeSpan.Zero)</summary>
        public TimeSpan RemainingTime
        {
            get
            {
                var end = GetEndUtc();
                var remaining = end - DateTime.UtcNow;
                return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
            }
        }

        // ============================================================
        // 구매 처리
        // ============================================================

        /// <summary>
        /// 구독 추가 — 이미 구독 중이면 추가 시간 + 보너스(10%) 시간 적립.
        /// 새로 구매: hoursPurchased 시간만큼 종료 시각 = 현재 + hoursPurchased
        /// 추가 구매: hoursPurchased + Floor(hoursPurchased × 0.1) 시간만큼 기존 종료 시각에 추가
        /// </summary>
        /// <returns>(purchasedHours, bonusHours, totalAddedHours, totalRemainingAfter, newEndUtc)</returns>
        public PurchaseResult Subscribe(int hoursPurchased)
        {
            if (hoursPurchased <= 0) return default;

            bool wasSubscribed = IsSubscribed;
            DateTime now = DateTime.UtcNow;
            DateTime currentEnd = GetEndUtc();
            DateTime baseEnd = wasSubscribed && currentEnd > now ? currentEnd : now;

            // 추가 구매 시 보너스 10% (소수점 버림)
            int bonus = wasSubscribed ? Mathf.FloorToInt(hoursPurchased * 0.1f) : 0;
            int totalAdded = hoursPurchased + bonus;
            DateTime newEnd = baseEnd.AddHours(totalAdded);

            JewelsHexaPuzzle.Utils.SecurePrefs.SetString(KEY_END, newEnd.ToString("o", CultureInfo.InvariantCulture));
            PlayerPrefs.Save();

            OnSubscriptionChanged?.Invoke();

            return new PurchaseResult
            {
                wasSubscribed = wasSubscribed,
                purchasedHours = hoursPurchased,
                bonusHours = bonus,
                totalAddedHours = totalAdded,
                newEndUtc = newEnd,
                totalRemainingHours = (newEnd - now).TotalHours,
            };
        }

        /// <summary>구매 결과 데이터</summary>
        public struct PurchaseResult
        {
            public bool wasSubscribed;          // 추가 구매였는지
            public int purchasedHours;          // 구매한 시간
            public int bonusHours;              // 보너스 시간 (추가 구매 시)
            public int totalAddedHours;         // 보너스 포함 추가 시간
            public DateTime newEndUtc;          // 새 종료 시각
            public double totalRemainingHours;  // 구매 직후 총 잔여 시간
        }

        /// <summary>
        /// 구독 만료 체크 — 만료되었지만 PlayerPrefs에 종료 시각이 남아있다면 이벤트 발생.
        /// 매 프레임 또는 주기적으로 호출 가능.
        /// </summary>
        public void CheckExpiration()
        {
            DateTime end = GetEndUtc();
            if (end == DateTime.MinValue) return;
            if (end <= DateTime.UtcNow)
            {
                // 만료됨 — 키 그대로 두면 IsSubscribed가 false 반환하므로 그대로 둠
                // 단, 이벤트는 한 번만 발생하도록 하려면 추가 플래그 필요.
                // 단순화: 매 호출마다 IsSubscribed를 직접 검사하므로 이벤트 없이도 UI 갱신 가능
            }
        }

        // ============================================================
        // 포맷팅
        // ============================================================

        /// <summary>
        /// 잔여 시간 포맷:
        ///   - 24h 이상: "Xd Yh" (예: "6d 23h")
        ///   - 24h 미만: "Xh Ym" (예: "23h 59m")
        /// </summary>
        public static string FormatRemaining(TimeSpan remaining)
        {
            if (remaining <= TimeSpan.Zero) return "만료됨";

            int totalDays = (int)remaining.TotalDays;
            int hours = remaining.Hours;
            int minutes = remaining.Minutes;

            if (totalDays >= 1) return $"{totalDays}d {hours}h";
            return $"{hours}h {minutes}m";
        }

        /// <summary>구독 중 라벨 ("구독중 Xd Yh" 형식)</summary>
        public string GetStatusLabel()
        {
            if (!IsSubscribed) return "";
            return $"구독중 {FormatRemaining(RemainingTime)}";
        }

        // ============================================================
        // 디버그/에디터 헬퍼
        // ============================================================

        /// <summary>구독 즉시 만료 (테스트용)</summary>
        public void DebugClearSubscription()
        {
            JewelsHexaPuzzle.Utils.SecurePrefs.DeleteKey(KEY_END);
            OnSubscriptionChanged?.Invoke();
        }
    }
}
