using UnityEngine;
using System;
using System.Collections.Generic;

namespace JewelsHexaPuzzle.Core
{
    /// <summary>
    /// 육각형 그리드 좌표 시스템 (Axial Coordinates)
    /// 큐브 좌표계를 사용하여 육각형 위치를 표현
    /// </summary>
    [Serializable]
    public struct HexCoord : IEquatable<HexCoord>
    {
        public int q; // column (axial)
        public int r; // row (axial)
        
        // 큐브 좌표계의 s 값 (q + r + s = 0)
        public int S => -q - r;
        
        public HexCoord(int q, int r)
        {
            this.q = q;
            this.r = r;
        }
        
        // 육각형의 6방향 이웃 (pointy-top 기준)
        public static readonly HexCoord[] Directions = new HexCoord[]
        {
            new HexCoord(1, 0),   // 오른쪽
            new HexCoord(1, -1),  // 오른쪽 위
            new HexCoord(0, -1),  // 왼쪽 위
            new HexCoord(-1, 0),  // 왼쪽
            new HexCoord(-1, 1),  // 왼쪽 아래
            new HexCoord(0, 1)    // 오른쪽 아래
        };
        
        /// <summary>
        /// 특정 방향의 이웃 좌표 반환
        /// </summary>
        public HexCoord GetNeighbor(int direction)
        {
            direction = ((direction % 6) + 6) % 6;
            return this + Directions[direction];
        }
        
        /// <summary>
        /// 모든 6방향 이웃 좌표 반환
        /// </summary>
        public HexCoord[] GetAllNeighbors()
        {
            HexCoord[] neighbors = new HexCoord[6];
            for (int i = 0; i < 6; i++)
            {
                neighbors[i] = GetNeighbor(i);
            }
            return neighbors;
        }
        
        /// <summary>
        /// Axial 좌표를 월드 좌표로 변환 (pointy-top hexagon)
        /// </summary>
        public Vector2 ToWorldPosition(float hexSize)
        {
            float x = hexSize * (Mathf.Sqrt(3f) * q + Mathf.Sqrt(3f) / 2f * r);
            float y = hexSize * (3f / 2f * r);
            return new Vector2(x, -y); // Unity는 Y가 위로 증가하므로 반전
        }
        
        /// <summary>
        /// 월드 좌표를 가장 가까운 Hex 좌표로 변환
        /// </summary>
        public static HexCoord FromWorldPosition(Vector2 worldPos, float hexSize)
        {
            float q = (Mathf.Sqrt(3f) / 3f * worldPos.x - 1f / 3f * (-worldPos.y)) / hexSize;
            float r = (2f / 3f * (-worldPos.y)) / hexSize;
            return Round(q, r);
        }
        
        /// <summary>
        /// 실수 좌표를 정수 Hex 좌표로 반올림
        /// </summary>
        public static HexCoord Round(float q, float r)
        {
            float s = -q - r;
            
            int qi = Mathf.RoundToInt(q);
            int ri = Mathf.RoundToInt(r);
            int si = Mathf.RoundToInt(s);
            
            float qDiff = Mathf.Abs(qi - q);
            float rDiff = Mathf.Abs(ri - r);
            float sDiff = Mathf.Abs(si - s);
            
            if (qDiff > rDiff && qDiff > sDiff)
            {
                qi = -ri - si;
            }
            else if (rDiff > sDiff)
            {
                ri = -qi - si;
            }
            
            return new HexCoord(qi, ri);
        }
        
        /// <summary>
        /// 두 Hex 좌표 사이의 거리
        /// </summary>
        public int DistanceTo(HexCoord other)
        {
            return (Mathf.Abs(q - other.q) + Mathf.Abs(r - other.r) + Mathf.Abs(S - other.S)) / 2;
        }
        
        /// <summary>
        /// 주어진 반경 내의 모든 좌표 반환
        /// </summary>
        public static List<HexCoord> GetHexesInRadius(HexCoord center, int radius)
        {
            List<HexCoord> results = new List<HexCoord>();
            
            for (int q = -radius; q <= radius; q++)
            {
                int r1 = Mathf.Max(-radius, -q - radius);
                int r2 = Mathf.Min(radius, -q + radius);
                
                for (int r = r1; r <= r2; r++)
                {
                    results.Add(new HexCoord(center.q + q, center.r + r));
                }
            }
            
            return results;
        }
        
        // 연산자 오버로딩
        public static HexCoord operator +(HexCoord a, HexCoord b)
        {
            return new HexCoord(a.q + b.q, a.r + b.r);
        }
        
        public static HexCoord operator -(HexCoord a, HexCoord b)
        {
            return new HexCoord(a.q - b.q, a.r - b.r);
        }
        
        public static bool operator ==(HexCoord a, HexCoord b)
        {
            return a.q == b.q && a.r == b.r;
        }
        
        public static bool operator !=(HexCoord a, HexCoord b)
        {
            return !(a == b);
        }
        
        public bool Equals(HexCoord other)
        {
            return q == other.q && r == other.r;
        }
        
        public override bool Equals(object obj)
        {
            return obj is HexCoord other && Equals(other);
        }
        
        public override int GetHashCode()
        {
            return HashCode.Combine(q, r);
        }
        
        public override string ToString()
        {
            return $"Hex({q}, {r})";
        }
    }
}
