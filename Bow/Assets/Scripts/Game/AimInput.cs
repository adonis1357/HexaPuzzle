using UnityEngine;
using UnityEngine.EventSystems;

namespace Bow.Game
{
    /// <summary>
    /// 한 손 조준 입력 (GDD §3 / 아트 §2.7): 드래그(앵그리버드식 당김) → 손 떼면 추 미터 → 탭 발사.
    /// 마우스/터치 공용(Unity 터치→마우스 시뮬레이션). UI 위에서 시작한 포인터는 무시한다.
    /// 각도는 "화면 오른쪽 = 전방" 기준 (클라이언트는 월드가 미러링되므로 항상 성립).
    /// </summary>
    public sealed class AimInput : MonoBehaviour
    {
        public enum Mode { Disabled, Idle, Dragging, Metering }

        public float maxPullPx = 300f;   // 1080px 기준
        public float deadZonePx = 40f;
        public float minAngle = -10f, maxAngle = 85f;

        public Mode CurrentMode { get; private set; }
        public Vector2 DragStartScreen { get; private set; }
        public Vector2 DragCurrentScreen { get; private set; }

        public System.Action OnDragStart;
        public System.Action<float, float> OnDragUpdate;   // angle, power
        public System.Action OnDragCancel;
        public System.Action<float, float> OnDragRelease;  // angle, power
        public System.Action OnTap;                        // Metering 중 탭

        private float lastAngle, lastPower;

        public void SetMode(Mode m) { CurrentMode = m; }

        private bool PointerOverUI()
        {
            if (EventSystem.current == null) return false;
            if (Input.touchCount > 0) return EventSystem.current.IsPointerOverGameObject(Input.GetTouch(0).fingerId);
            return EventSystem.current.IsPointerOverGameObject();
        }

        private void Update()
        {
            if (CurrentMode == Mode.Disabled) return;
            Vector2 pos = Input.mousePosition;
            float scale = 1080f / Screen.width;

            if (CurrentMode == Mode.Metering)
            {
                if (Input.GetMouseButtonDown(0) && !PointerOverUI())
                {
                    if (OnTap != null) OnTap();
                }
                return;
            }

            if (CurrentMode == Mode.Idle)
            {
                if (Input.GetMouseButtonDown(0) && !PointerOverUI())
                {
                    CurrentMode = Mode.Dragging;
                    DragStartScreen = pos; DragCurrentScreen = pos;
                    lastAngle = 45f; lastPower = 0f;
                    if (OnDragStart != null) OnDragStart();
                }
                return;
            }

            if (CurrentMode == Mode.Dragging)
            {
                DragCurrentScreen = pos;
                Vector2 pull = (pos - DragStartScreen) * scale;
                float len = pull.magnitude;
                Vector2 dir = -pull;
                float angle = len > 1f ? Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg : 45f;
                angle = Mathf.Clamp(angle, minAngle, maxAngle);
                float power = Mathf.Clamp01((len - deadZonePx) / (maxPullPx - deadZonePx));
                lastAngle = angle; lastPower = power;
                if (Input.GetMouseButton(0))
                {
                    if (OnDragUpdate != null) OnDragUpdate(angle, power);
                }
                else
                {
                    if (len < deadZonePx)
                    {
                        CurrentMode = Mode.Idle;
                        if (OnDragCancel != null) OnDragCancel();
                    }
                    else
                    {
                        CurrentMode = Mode.Metering;
                        if (OnDragRelease != null) OnDragRelease(angle, power);
                    }
                }
            }
        }
    }
}
