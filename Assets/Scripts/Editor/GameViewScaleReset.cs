#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.Reflection;

namespace JewelsHexaPuzzle.EditorTools
{
    /// <summary>
    /// 유니티 실행(에디터 세션 시작) 시 Game 뷰 "Scale" 줌을 1배로 초기화한다 (예: 1.25/1.3 → 1).
    /// GameView/ZoomableArea는 internal이라 리플렉션으로 m_ZoomArea.m_Scale/m_Translation을 리셋.
    /// 세션당 1회만 실행(SessionState) → 개발 중 수동 줌은 유지, 재실행 때마다 1배로 시작.
    /// </summary>
    [InitializeOnLoad]
    public static class GameViewScaleReset
    {
        private const string DONE_KEY = "GVScaleReset_done_v1";

        static GameViewScaleReset()
        {
            if (SessionState.GetBool(DONE_KEY, false)) return; // 세션당 1회
            EditorApplication.delayCall += ResetScale;
        }

        static void ResetScale()
        {
            try
            {
                var gvType = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");
                if (gvType == null) return;
                var windows = Resources.FindObjectsOfTypeAll(gvType);
                if (windows == null || windows.Length == 0)
                {
                    // Game 뷰가 아직 안 열렸으면 다음 틱에 재시도(세션 플래그는 아직 안 세움)
                    EditorApplication.delayCall += ResetScale;
                    return;
                }

                const BindingFlags BF = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance;
                foreach (var w in windows)
                {
                    var zoom = gvType.GetField("m_ZoomArea", BF)?.GetValue(w);
                    if (zoom != null)
                    {
                        var zt = zoom.GetType();
                        var scaleF = zt.GetField("m_Scale", BF);
                        Vector2 before = scaleF != null ? (Vector2)scaleF.GetValue(zoom) : Vector2.one;
                        scaleF?.SetValue(zoom, Vector2.one);                       // 줌 1배
                        zt.GetField("m_Translation", BF)?.SetValue(zoom, Vector2.zero); // 팬 리셋(리페인트 시 중앙 클램프)
                        if (before != Vector2.one)
                            Debug.Log($"[GVScale] Game 뷰 줌 {before.x:0.00} → 1");
                    }
                    var defF = gvType.GetField("m_defaultScale", BF);
                    if (defF != null && defF.FieldType == typeof(float)) defF.SetValue(w, 1f);
                    (w as EditorWindow)?.Repaint();
                }
                SessionState.SetBool(DONE_KEY, true);
            }
            catch (System.Exception e) { Debug.LogWarning("[GVScale] 실패: " + e.Message); }
        }
    }
}
#endif
