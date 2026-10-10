using UnityEngine;
using UnityEngine.UI;

namespace Bow.UI
{
    /// <summary>
    /// 육각형 레이더 차트 (uGUI 커스텀 Graphic). 격자 3단 + 축선 + 채움 다각형 + 외곽선을 한 메시로 그린다.
    /// 값은 1~5, 축 수는 values.Length (6 권장). 라벨/숫자는 바깥에서 Text로 배치한다.
    /// </summary>
    public sealed class RadarGraphic : MaskableGraphic
    {
        public int[] values = { 3, 3, 3, 3, 3, 3 };
        public int maxValue = 5;
        public float radius = 200f;
        public Color fillColor = new Color(0.71f, 0.19f, 0.17f, 0.35f);
        public Color lineColor = new Color(0.12f, 0.11f, 0.09f, 1f);
        public Color gridColor = new Color(0.12f, 0.11f, 0.09f, 0.18f);
        public float outlineWidth = 7f;
        public float gridWidth = 2f;

        public void SetValues(int[] v) { values = v; SetVerticesDirty(); }

        public Vector2 AxisPoint(int i, float r)
        {
            int n = values.Length;
            float a = (90f + 360f * i / n) * Mathf.Deg2Rad; // 첫 축은 12시 방향, 반시계
            return new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            int n = values != null ? values.Length : 0;
            if (n < 3) return;

            // 격자 (1/3, 2/3, 3/3) + 축선
            for (int ring = 1; ring <= 3; ring++)
            {
                float r = radius * ring / 3f;
                for (int i = 0; i < n; i++)
                    AddLine(vh, AxisPoint(i, r), AxisPoint((i + 1) % n, r), gridWidth, gridColor);
            }
            for (int i = 0; i < n; i++) AddLine(vh, Vector2.zero, AxisPoint(i, radius), gridWidth, gridColor);

            // 채움 다각형 (중심 팬)
            Vector2[] pts = new Vector2[n];
            for (int i = 0; i < n; i++)
            {
                float v = Mathf.Clamp(values[i], 0, maxValue) / (float)maxValue;
                pts[i] = AxisPoint(i, radius * v);
            }
            int c = vh.currentVertCount;
            vh.AddVert(Vector3.zero, fillColor, Vector2.zero);
            for (int i = 0; i < n; i++) vh.AddVert(pts[i], fillColor, Vector2.zero);
            for (int i = 0; i < n; i++) vh.AddTriangle(c, c + 1 + i, c + 1 + (i + 1) % n);

            // 외곽선 + 꼭짓점 점
            for (int i = 0; i < n; i++) AddLine(vh, pts[i], pts[(i + 1) % n], outlineWidth, lineColor);
            for (int i = 0; i < n; i++) AddDot(vh, pts[i], outlineWidth * 1.4f, lineColor);
        }

        private static void AddLine(VertexHelper vh, Vector2 a, Vector2 b, float width, Color col)
        {
            Vector2 d = b - a;
            if (d.sqrMagnitude < 1e-4f) return;
            Vector2 nrm = new Vector2(-d.y, d.x).normalized * (width * 0.5f);
            int c = vh.currentVertCount;
            vh.AddVert(a - nrm, col, Vector2.zero);
            vh.AddVert(a + nrm, col, Vector2.zero);
            vh.AddVert(b + nrm, col, Vector2.zero);
            vh.AddVert(b - nrm, col, Vector2.zero);
            vh.AddTriangle(c, c + 1, c + 2);
            vh.AddTriangle(c, c + 2, c + 3);
        }

        private static void AddDot(VertexHelper vh, Vector2 p, float size, Color col)
        {
            float h = size * 0.5f;
            int c = vh.currentVertCount;
            vh.AddVert(p + new Vector2(-h, -h), col, Vector2.zero);
            vh.AddVert(p + new Vector2(-h, h), col, Vector2.zero);
            vh.AddVert(p + new Vector2(h, h), col, Vector2.zero);
            vh.AddVert(p + new Vector2(h, -h), col, Vector2.zero);
            vh.AddTriangle(c, c + 1, c + 2);
            vh.AddTriangle(c, c + 2, c + 3);
        }
    }
}
