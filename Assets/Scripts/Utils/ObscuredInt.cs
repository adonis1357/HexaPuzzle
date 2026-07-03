using System;
using UnityEngine;

namespace JewelsHexaPuzzle.Utils
{
    /// <summary>
    /// 메모리 치트 내성 정수 (2026-07 보안 강화).
    /// 실제 값을 평문으로 저장하지 않고 XOR 난독값 + 체크섬 셰도우로 보관한다.
    /// GameGuardian류가 표시 수치(예: 골드 74738)로 메모리를 검색해도 난독값만 상주하므로 특정이 어렵고,
    /// 난독값을 강제로 고정(freeze)하면 체크섬 불일치로 다음 읽기에서 변조가 감지된다.
    ///
    /// int와 양방향 implicit 변환 + 증감/문자열 연산자를 제공하므로 기존 int 필드를 그대로 대체할 수 있다.
    /// (호출부 코드 무변경: currentGold += n, currentGold >= cost, $"{currentGold}" 모두 그대로 컴파일)
    ///
    /// 한계: 서버 없는 로컬 게임의 클라이언트 방어라 '완전 차단'이 아니라 '특정·변조 비용 상승 + 탐지'가 목표.
    /// 저장 무결성은 <see cref="SecurePrefs"/>가 담당(둘은 상호 보완).
    /// </summary>
    [Serializable]
    public struct ObscuredInt : IEquatable<ObscuredInt>, IComparable<ObscuredInt>
    {
        private const int MAGIC = 0x5F3A9C7B;

        // 인스턴스별 XOR 키를 다르게 하려는 회전 시드 (보안 목적이지 난수 품질은 불필요)
        private static int _seed = unchecked((int)0x9E3779B1);

        private int _key;
        private int _hidden; // value ^ _key
        private int _check;  // value ^ MAGIC (셰도우)
        private bool _set;

        public ObscuredInt(int value)
        {
            _seed = unchecked(_seed * 1103515245 + 12345);
            _key = _seed;
            _hidden = value ^ _key;
            _check = value ^ MAGIC;
            _set = true;
        }

        private int Decode()
        {
            if (!_set) return 0; // 기본초기화(default) 상태 = 합법적 0
            int value = _hidden ^ _key;
            if ((value ^ MAGIC) != _check)
            {
                // 체크섬 불일치 → 메모리 변조(freeze 등) 감지. 소프트 대응: 로그 + 치트값 무력화(0 반환).
                Debug.LogWarning("[ObscuredInt] 메모리 변조 감지 → 값 무효화");
                return 0;
            }
            return value;
        }

        private void Encode(int value)
        {
            _seed = unchecked(_seed * 1103515245 + 12345);
            _key = _seed;
            _hidden = value ^ _key;
            _check = value ^ MAGIC;
            _set = true;
        }

        // ── int 양방향 변환 ──
        public static implicit operator int(ObscuredInt o) => o.Decode();
        public static implicit operator ObscuredInt(int v) => new ObscuredInt(v);

        // ── 증감 (구 int 코드의 ++/-- 호환) ──
        public static ObscuredInt operator ++(ObscuredInt o) { o.Encode(o.Decode() + 1); return o; }
        public static ObscuredInt operator --(ObscuredInt o) { o.Encode(o.Decode() - 1); return o; }

        // ── 비교/동등 ──
        public bool Equals(ObscuredInt other) => Decode() == other.Decode();
        public int CompareTo(ObscuredInt other) => Decode().CompareTo(other.Decode());
        public override bool Equals(object obj)
        {
            if (obj is ObscuredInt oi) return Decode() == oi.Decode();
            if (obj is int i) return Decode() == i;
            return false;
        }
        public override int GetHashCode() => Decode();
        public override string ToString() => Decode().ToString();
        public string ToString(string format) => Decode().ToString(format);
    }
}
