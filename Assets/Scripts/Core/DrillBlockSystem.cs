using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using JewelsHexaPuzzle.Data;

namespace JewelsHexaPuzzle.Core
{
    public class DrillBlockSystem : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private HexGrid hexGrid;
        [SerializeField] private BlockRemovalSystem removalSystem;
        
        [Header("Drill Settings")]
        [SerializeField] private float drillSpeed = 0.08f;
        [SerializeField] private float explosionDuration = 0.15f;
        
        [Header("Effect Settings")]
        [SerializeField] private GameObject explosionEffectPrefab;
        
        public event System.Action<int> OnDrillComplete;
        
        private bool isDrilling = false;
        public bool IsDrilling => isDrilling;
        
        public DrillDirection? DetectDrillPattern(List<HexBlock> matchedBlocks)
        {
            if (matchedBlocks == null || matchedBlocks.Count < 4)
                return null;
            
            List<HexCoord> coords = new List<HexCoord>();
            foreach (var block in matchedBlocks)
                coords.Add(block.Coord);
            
            coords.Sort((a, b) => {
                if (a.q != b.q) return a.q.CompareTo(b.q);
                return a.r.CompareTo(b.r);
            });
            
            if (IsHorizontalPattern(coords)) return DrillDirection.Vertical;
            if (IsSlashPattern(coords)) return DrillDirection.BackSlash;
            if (IsBackSlashPattern(coords)) return DrillDirection.Slash;
            
            return null;
        }
        
        private bool IsHorizontalPattern(List<HexCoord> coords)
        {
            int baseR = coords[0].r;
            for (int i = 0; i < coords.Count; i++)
            {
                if (coords[i].r != baseR) return false;
                if (i > 0 && coords[i].q != coords[i-1].q + 1) return false;
            }
            return true;
        }
        
        private bool IsSlashPattern(List<HexCoord> coords)
        {
            for (int i = 1; i < coords.Count; i++)
            {
                int dq = coords[i].q - coords[i-1].q;
                int dr = coords[i].r - coords[i-1].r;
                if (!(dq == 1 && dr == -1) && !(dq == 0 && dr == -1))
                    return false;
            }
            return true;
        }
        
        private bool IsBackSlashPattern(List<HexCoord> coords)
        {
            for (int i = 1; i < coords.Count; i++)
            {
                int dq = coords[i].q - coords[i-1].q;
                int dr = coords[i].r - coords[i-1].r;
                if (!(dq == 0 && dr == 1) && !(dq == 1 && dr == 0))
                    return false;
            }
            return true;
        }
        
        public void CreateDrillBlock(HexBlock block, DrillDirection direction, GemType gemType)
        {
            if (block == null) return;
            BlockData drillData = new BlockData(gemType);
            drillData.specialType = SpecialBlockType.Drill;
            drillData.drillDirection = direction;
            block.SetBlockData(drillData);
            block.ShowDrillIndicator(direction);
        }
        
        public void ActivateDrill(HexBlock drillBlock)
        {
            if (isDrilling || drillBlock == null) return;
            if (drillBlock.Data == null || drillBlock.Data.specialType != SpecialBlockType.Drill)
                return;
            StartCoroutine(DrillCoroutine(drillBlock));
        }
        
        private IEnumerator DrillCoroutine(HexBlock drillBlock)
        {
            isDrilling = true;
            
            DrillDirection direction = drillBlock.Data.drillDirection;
            HexCoord startCoord = drillBlock.Coord;
            
            List<HexBlock> targets1 = GetBlocksInDirection(startCoord, direction, true);
            List<HexBlock> targets2 = GetBlocksInDirection(startCoord, direction, false);
            
            yield return StartCoroutine(ExplodeBlock(drillBlock));
            drillBlock.SetBlockData(new BlockData());
            
            int totalScore = 100;
            
            Coroutine drill1 = StartCoroutine(DrillLine(targets1));
            Coroutine drill2 = StartCoroutine(DrillLine(targets2));
            
            yield return drill1;
            yield return drill2;
            
            totalScore += (targets1.Count + targets2.Count) * 50;
            OnDrillComplete?.Invoke(totalScore);
            isDrilling = false;
        }
        
        private List<HexBlock> GetBlocksInDirection(HexCoord start, DrillDirection direction, bool positive)
        {
            List<HexBlock> blocks = new List<HexBlock>();
            HexCoord delta = GetDirectionDelta(direction, positive);
            HexCoord current = start + delta;
            
            while (hexGrid.IsValidCoord(current))
            {
                HexBlock block = hexGrid.GetBlock(current);
                if (block != null && block.Data != null && block.Data.gemType != GemType.None)
                    blocks.Add(block);
                current = current + delta;
            }
            return blocks;
        }
        
        private HexCoord GetDirectionDelta(DrillDirection direction, bool positive)
        {
            int sign = positive ? 1 : -1;
            switch (direction)
            {
                case DrillDirection.Vertical: return new HexCoord(0, sign);
                case DrillDirection.Slash: return new HexCoord(sign, -sign);
                case DrillDirection.BackSlash: return new HexCoord(sign, 0);
                default: return new HexCoord(0, sign);
            }
        }
        
        private IEnumerator DrillLine(List<HexBlock> targets)
        {
            foreach (var block in targets)
            {
                if (block == null) continue;
                yield return StartCoroutine(ExplodeBlock(block));
                block.SetBlockData(new BlockData());
                yield return new WaitForSeconds(drillSpeed);
            }
        }
        
        private IEnumerator ExplodeBlock(HexBlock block)
        {
            if (block == null) yield break;
            
            Vector3 pos = block.transform.position;
            int effectCount = Random.Range(2, 4);
            
            for (int i = 0; i < effectCount; i++)
            {
                Vector3 offset = new Vector3(Random.Range(-15f, 15f), Random.Range(-15f, 15f), 0);
                float scale = Random.Range(0.5f, 1.5f);
                float delay = Random.Range(0f, 0.05f);
                StartCoroutine(SpawnExplosionEffect(pos + offset, scale, delay));
            }
            
            float elapsed = 0f;
            Vector3 originalScale = block.transform.localScale;
            
            while (elapsed < explosionDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / explosionDuration;
                block.transform.localScale = originalScale * (1f - t);
                yield return null;
            }
            block.transform.localScale = Vector3.one;
        }
        
        private IEnumerator SpawnExplosionEffect(Vector3 position, float scale, float delay)
        {
            yield return new WaitForSeconds(delay);
            
            if (explosionEffectPrefab != null)
            {
                GameObject effect = Instantiate(explosionEffectPrefab, position, Quaternion.identity);
                effect.transform.localScale = Vector3.one * scale;
                Destroy(effect, 0.5f);
            }
            else
            {
                yield return StartCoroutine(SimpleExplosionEffect(position, scale));
            }
        }
        
        private IEnumerator SimpleExplosionEffect(Vector3 position, float scale)
        {
            GameObject effectObj = new GameObject("ExplosionEffect");
            effectObj.transform.SetParent(transform, false);
            effectObj.transform.position = position;
            
            var image = effectObj.AddComponent<UnityEngine.UI.Image>();
            image.color = new Color(1f, 0.8f, 0.2f, 0.8f);
            image.raycastTarget = false;
            
            var rt = effectObj.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(30f * scale, 30f * scale);
            
            float duration = 0.3f;
            float elapsed = 0f;
            
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                float currentScale = scale * (1f + t * 2f);
                rt.sizeDelta = new Vector2(30f * currentScale, 30f * currentScale);
                image.color = new Color(1f, 0.8f, 0.2f, 0.8f * (1f - t));
                yield return null;
            }
            Destroy(effectObj);
        }
    }
}
