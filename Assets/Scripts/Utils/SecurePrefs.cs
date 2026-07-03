using System;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace JewelsHexaPuzzle.Utils
{
    /// <summary>
    /// 저장 데이터 무결성 래퍼 (2026-07 보안 강화).
    /// PlayerPrefs 값에 HMAC-SHA256 서명을 붙여 저장하고, 로드 시 재계산해 일치할 때만 값을 반환한다.
    /// 불일치(변조/손상) 시 기본값을 반환하므로, 루팅 기기의 shared_prefs XML 직접 편집을 무력화한다.
    ///
    /// 정책:
    ///  - 서명 키는 기기 고유값(deviceUniqueIdentifier) + 앱 시크릿(조립식 난독화)에서 유도 →
    ///    한 기기에서 만든 서명본을 다른 기기로 이식해도 무효.
    ///  - 서명 입력에 키 이름을 포함 → "Item_Hammer" 값을 "TotalGold"에 복사하는 값 이식 공격 차단.
    ///  - **마이그레이션 없음(출시 전 정책)**: 서명본만 신뢰한다. 구 평문 값은 읽지 않으므로
    ///    "평문 재삽입 → 자동 승계" 공격 표면 자체가 없다. (이미 배포된 세이브가 있다면 이 정책을 바꿔야 함)
    ///
    /// 한계: HMAC은 '저장파일 변조'만 방어한다. 메모리 편집(GameGuardian)은 편집값이 정상 서명으로
    ///       재저장되므로 막지 못한다 → 핵심 수치는 <see cref="ObscuredInt"/>로 별도 방어.
    /// </summary>
    public static class SecurePrefs
    {
        private const string SIG_SUFFIX = "__s"; // 서명본 저장 키 접미사 (구 평문 키와 네임스페이스 분리)

        // ── 앱 시크릿: 소스/APK 문자열 덤프에 평문으로 노출되지 않도록 바이트 배열 XOR 조립 ──
        private static readonly byte[] SECRET_ENC =
            { 0x2A, 0x5F, 0x71, 0x18, 0x3C, 0x6D, 0x04, 0x59, 0x22, 0x7E, 0x11, 0x48, 0x35, 0x60, 0x1B, 0x77 };
        private const byte SECRET_XOR = 0x3B;

        private static string _hmacKeyCache;

        private static string HmacKey
        {
            get
            {
                if (_hmacKeyCache == null)
                {
                    var sb = new StringBuilder(SECRET_ENC.Length);
                    for (int i = 0; i < SECRET_ENC.Length; i++)
                        sb.Append((char)(SECRET_ENC[i] ^ SECRET_XOR ^ (byte)(i * 7)));
                    string device;
                    try { device = SystemInfo.deviceUniqueIdentifier; }
                    catch { device = "nodevice"; }
                    _hmacKeyCache = Sha256(device + "|" + sb.ToString());
                }
                return _hmacKeyCache;
            }
        }

        private static string Sha256(string s)
        {
            using (var sha = SHA256.Create())
                return Convert.ToBase64String(sha.ComputeHash(Encoding.UTF8.GetBytes(s)));
        }

        private static string Sign(string key, string value)
        {
            using (var h = new HMACSHA256(Encoding.UTF8.GetBytes(HmacKey)))
                return Convert.ToBase64String(h.ComputeHash(Encoding.UTF8.GetBytes(key + "|" + value)));
        }

        // ============================================================
        // 쓰기
        // ============================================================
        public static void SetInt(string key, int value)
            => SetString(key, value.ToString(CultureInfo.InvariantCulture));

        public static void SetString(string key, string value)
        {
            if (value == null) value = "";
            string signed = value + "|" + Sign(key, value);
            PlayerPrefs.SetString(key + SIG_SUFFIX, signed);
            // 혹시 남아있을 수 있는 구 평문 키 제거 (서명본만 신뢰)
            if (PlayerPrefs.HasKey(key)) PlayerPrefs.DeleteKey(key);
            // ★ Save()는 호출자가 배치 종료 시 1회 수행 (루프 저장 시 매번 디스크 flush 방지).
            //   PlayerPrefs.Save()가 서명본 SetString까지 함께 flush하므로 기존 저장부의 Save로 충분.
        }

        /// <summary>영속화 flush — SetInt/SetString 배치 후 1회 호출.</summary>
        public static void Save() => PlayerPrefs.Save();

        // ============================================================
        // 읽기 (서명 검증 실패 → 기본값)
        // ============================================================
        public static int GetInt(string key, int defaultValue = 0)
        {
            string s = GetString(key, null);
            if (s != null && int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out int v))
                return v;
            return defaultValue;
        }

        public static string GetString(string key, string defaultValue = "")
        {
            string raw = PlayerPrefs.GetString(key + SIG_SUFFIX, null);
            if (string.IsNullOrEmpty(raw)) return defaultValue;

            int idx = raw.LastIndexOf('|'); // 값에는 '|'가 없고 서명은 Base64('|' 미포함)라 안전
            if (idx <= 0) { OnTamper(key); return defaultValue; }

            string value = raw.Substring(0, idx);
            string sig = raw.Substring(idx + 1);
            if (!string.Equals(Sign(key, value), sig, StringComparison.Ordinal))
            {
                OnTamper(key);
                return defaultValue;
            }
            return value;
        }

        // ============================================================
        // 유틸
        // ============================================================
        public static bool HasKey(string key) => PlayerPrefs.HasKey(key + SIG_SUFFIX);

        public static void DeleteKey(string key)
        {
            PlayerPrefs.DeleteKey(key + SIG_SUFFIX);
            if (PlayerPrefs.HasKey(key)) PlayerPrefs.DeleteKey(key); // 혹시 모를 구 평문도 제거
            PlayerPrefs.Save();
        }

        private static void OnTamper(string key)
        {
            // 로컬 게임이므로 소프트 대응: 로그만 남기고 호출자에 기본값을 돌려준다(강제종료·차단 금지).
            Debug.LogWarning($"[SecurePrefs] 저장 데이터 변조/손상 감지 → 기본값 사용: {key}");
        }
    }
}
