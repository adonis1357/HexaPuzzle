using UnityEngine;
using JewelsHexaPuzzle.Data;

namespace JewelsHexaPuzzle.Core
{
    /// <summary>
    /// X블록 디버그 테스트
    /// C키: (0,0)에 X특수블록 직접 배치 → 클릭해서 발동 테스트
    /// X키: (0,0) 중심+6이웃 같은색 세팅 → 매칭으로 X블록 생성 테스트
    /// </summary>
    public class XBlockDebugTest : MonoBehaviour
    {
        private HexGrid hexGrid;

        private void Start()
        {
            hexGrid = FindObjectOfType<HexGrid>();
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.C))
                PlaceXBlockDirectly();

            if (Input.GetKeyDown(KeyCode.X))
                ForceXBlockPattern();
        }

        private void PlaceXBlockDirectly()
        {
            if (hexGrid == null) { Debug.LogError("[XBlockDebugTest] HexGrid not found!"); return; }

            var xSystem = FindObjectOfType<XBlockSystem>();
            if (xSystem == null) { Debug.LogError("[XBlockDebugTest] XBlockSystem not found!"); return; }

            HexCoord center = new HexCoord(0, 0);
            HexBlock centerBlock = hexGrid.GetBlock(center);
            if (centerBlock == null) { Debug.LogError("[XBlockDebugTest] Center (0,0) not found!"); return; }

            GemType color = (centerBlock.Data != null && centerBlock.Data.gemType != GemType.None)
                ? centerBlock.Data.gemType : GemType.Red;

            xSystem.CreateXBlock(centerBlock, color);
            Debug.Log($"[XBlockDebugTest] Placed X-block at (0,0) color={color}. Click it to activate!");
        }

        private void ForceXBlockPattern()
        {
            if (hexGrid == null) { Debug.LogError("[XBlockDebugTest] HexGrid not found!"); return; }

            HexCoord center = new HexCoord(0, 0);
            HexBlock centerBlock = hexGrid.GetBlock(center);
            if (centerBlock == null) { Debug.LogError("[XBlockDebugTest] Center (0,0) not found!"); return; }

            GemType forceColor = GemType.Red;
            centerBlock.SetBlockData(new BlockData(forceColor));

            var neighbors = center.GetAllNeighbors();
            int count = 0;
            foreach (var nc in neighbors)
            {
                HexBlock nb = hexGrid.GetBlock(nc);
                if (nb != null)
                {
                    nb.SetBlockData(new BlockData(forceColor));
                    count++;
                }
            }
            Debug.Log($"[XBlockDebugTest] Forced X pattern: center + {count} neighbors = {forceColor}. Rotate to trigger matching!");
        }
    }
}
