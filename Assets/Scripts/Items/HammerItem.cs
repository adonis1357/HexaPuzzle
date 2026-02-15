using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using JewelsHexaPuzzle.Core;
using JewelsHexaPuzzle.Data;
using JewelsHexaPuzzle.Managers;

namespace JewelsHexaPuzzle.Items
{
    public class HammerItem : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Button hammerButton;
        [SerializeField] private Image hammerIcon;
        [SerializeField] private Image backgroundOverlay;
        [SerializeField] private HexGrid hexGrid;
        [SerializeField] private BlockRemovalSystem blockRemovalSystem;
        [SerializeField] private DrillBlockSystem drillSystem;
        [SerializeField] private InputSystem inputSystem;

        [Header("Settings")]
        [SerializeField] private Color activeOverlayColor = new Color(0.6f, 1f, 0.6f, 0.15f);

        private bool isActive = false;
        private bool isProcessing = false;

        public bool IsActive => isActive;

        private void Start()
        {
            AutoFindReferences();
            SetupUI();
            if (backgroundOverlay != null)
            {
                backgroundOverlay.gameObject.SetActive(false);
                backgroundOverlay.raycastTarget = false;
            }
        }

        private void AutoFindReferences()
        {
            if (hexGrid == null) hexGrid = FindObjectOfType<HexGrid>();
            if (blockRemovalSystem == null) blockRemovalSystem = FindObjectOfType<BlockRemovalSystem>();
            if (drillSystem == null) drillSystem = FindObjectOfType<DrillBlockSystem>();
            if (inputSystem == null) inputSystem = FindObjectOfType<InputSystem>();
        }

        private void SetupUI()
        {
            if (hammerButton != null)
                hammerButton.onClick.AddListener(OnHammerButtonClicked);
        }

        private void OnHammerButtonClicked()
        {
            if (isProcessing) return;
            if (isActive) Deactivate();
            else Activate();
        }

        public void Activate()
        {
            if (isActive || isProcessing) return;
            isActive = true;
            if (inputSystem != null) inputSystem.SetEnabled(false);
            if (backgroundOverlay != null)
            {
                backgroundOverlay.gameObject.SetActive(true);
                backgroundOverlay.color = activeOverlayColor;
            }
            if (hammerButton != null)
            {
                var img = hammerButton.GetComponent<Image>();
                if (img != null) img.color = new Color(1f, 0.85f, 0.2f, 1f);
            }
            Debug.Log("[HammerItem] Activated");
        }


        public void Deactivate()
        {
            isActive = false;
            // 처리 중이 아닐 때만 입력 복원 (처리 중이면 SmashBlock 끝에서 복원)
            if (!isProcessing && inputSystem != null && GameManager.Instance != null &&
                GameManager.Instance.CurrentState == GameState.Playing)
                inputSystem.SetEnabled(true);
            if (backgroundOverlay != null)
                backgroundOverlay.gameObject.SetActive(false);
            if (hammerButton != null)
            {
                var img = hammerButton.GetComponent<Image>();
                if (img != null) img.color = new Color(0.25f, 0.25f, 0.35f, 0.9f);
            }
            Debug.Log("[HammerItem] Deactivated");
        }

        private void Update()
        {
            if (!isActive || isProcessing) return;
            if (Input.GetMouseButtonDown(0))
                HandleClick(Input.mousePosition);
        }

        private void HandleClick(Vector2 screenPos)
        {
            var es = UnityEngine.EventSystems.EventSystem.current;
            if (es != null && es.IsPointerOverGameObject())
            {
                var pd = new UnityEngine.EventSystems.PointerEventData(es);
                pd.position = screenPos;
                var results = new List<UnityEngine.EventSystems.RaycastResult>();
                es.RaycastAll(pd, results);
                foreach (var r in results)
                {
                    if (r.gameObject == hammerButton?.gameObject)
                        return;
                }
            }

            HexBlock clickedBlock = FindBlockAtPosition(screenPos);
            if (clickedBlock != null && clickedBlock.Data != null && clickedBlock.Data.gemType != GemType.None)
            {
                isProcessing = true;
                Deactivate();
                StartCoroutine(SmashBlock(clickedBlock));
            }
            else
            {
                Deactivate();
            }
        }

        private HexBlock FindBlockAtPosition(Vector2 screenPos)
        {
            if (hexGrid == null) return null;
            HexBlock closest = null;
            float closestDist = float.MaxValue;
            foreach (var block in hexGrid.GetAllBlocks())
            {
                if (block == null || block.Data == null || block.Data.gemType == GemType.None) continue;
                RectTransform rt = block.GetComponent<RectTransform>();
                if (rt == null) continue;
                Vector2 localPoint;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(rt, screenPos, null, out localPoint);
                if (rt.rect.Contains(localPoint))
                {
                    float dist = localPoint.sqrMagnitude;
                    if (dist < closestDist) { closestDist = dist; closest = block; }
                }
            }
            return closest;
        }

        private IEnumerator SmashBlock(HexBlock block)
        {
            isProcessing = true;
            Debug.Log("[HammerItem] Smashing block at " + block.Coord);
            bool isSpecial = block.Data.specialType != SpecialBlockType.None;
            SpecialBlockType specialType = block.Data.specialType;

            yield return StartCoroutine(SmashAnimation(block));

            if (isSpecial)
            {
                switch (specialType)
                {
                    case SpecialBlockType.Drill:
                        if (drillSystem != null)
                        {
                            drillSystem.ActivateDrill(block);
                            yield return new WaitForSeconds(0.1f);
                            while (drillSystem.IsBlockActive(block)) yield return null;
                        }
                        break;
                }
            }
            else
            {
                block.ClearData();
                block.transform.localScale = Vector3.one;
            }

            if (blockRemovalSystem != null)
            {
                // 아이템 사용 시 회색 블록 생성 방지
                if (GameManager.Instance != null)
                    GameManager.Instance.IsItemAction = true;

                blockRemovalSystem.TriggerFallOnly();
                while (blockRemovalSystem.IsProcessing) yield return null;
            }

            isProcessing = false;
            if (inputSystem != null && GameManager.Instance != null &&
                GameManager.Instance.CurrentState == GameState.Playing)
                inputSystem.SetEnabled(true);
        }

        private IEnumerator SmashAnimation(HexBlock block)
        {
            if (block == null) yield break;
            Vector3 origPos = block.transform.position;
            Vector3 origScale = block.transform.localScale;

            float shakeDur = 0.15f;
            float elapsed = 0f;
            while (elapsed < shakeDur)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / shakeDur;
                float intensity = (1f - t) * 6f;
                float ox = Mathf.Sin(t * Mathf.PI * 12f) * intensity;
                float oy = Mathf.Cos(t * Mathf.PI * 10f) * intensity * 0.5f;
                block.transform.position = origPos + new Vector3(ox, oy, 0);
                yield return null;
            }
            block.transform.position = origPos;

            SpawnShards(block);

            float shrinkDur = 0.12f;
            elapsed = 0f;
            while (elapsed < shrinkDur)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / shrinkDur;
                float scale = 1f - t;
                float punch = 1f + 0.2f * Mathf.Sin(t * Mathf.PI * 4f);
                block.transform.localScale = origScale * scale * punch;
                yield return null;
            }
            block.transform.localScale = Vector3.zero;
            yield return new WaitForSeconds(0.05f);
        }

        private void SpawnShards(HexBlock block)
        {
            if (block == null) return;
            Color c = Color.gray;
            if (block.Data != null) c = GemColors.GetColor(block.Data.gemType);
            Vector3 center = block.transform.position;
            for (int i = 0; i < 8; i++)
                StartCoroutine(AnimateShard(center, c));
        }

        private IEnumerator AnimateShard(Vector3 center, Color color)
        {
            GameObject shard = new GameObject("Shard");
            shard.transform.SetParent(transform.root, false);
            shard.transform.position = center;
            var img = shard.AddComponent<Image>();
            img.raycastTarget = false;
            img.color = color;
            RectTransform rt = shard.GetComponent<RectTransform>();
            float size = Random.Range(6f, 14f);
            rt.sizeDelta = new Vector2(size, size);
            rt.localRotation = Quaternion.Euler(0, 0, Random.Range(0f, 360f));

            float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
            float speed = Random.Range(150f, 350f);
            Vector2 vel = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * speed;
            float grav = -800f;
            float rotSpd = Random.Range(-720f, 720f);
            float life = Random.Range(0.3f, 0.5f);
            float elapsed = 0f;

            while (elapsed < life)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / life;
                vel.y += grav * Time.deltaTime;
                Vector3 pos = shard.transform.position;
                pos.x += vel.x * Time.deltaTime;
                pos.y += vel.y * Time.deltaTime;
                shard.transform.position = pos;
                vel *= 0.97f;
                rt.localRotation = Quaternion.Euler(0, 0, rt.localEulerAngles.z + rotSpd * Time.deltaTime);
                float s = size * (1f - t * 0.5f);
                rt.sizeDelta = new Vector2(s, s);
                color.a = 1f - t;
                img.color = color;
                yield return null;
            }
            Destroy(shard);
        }

        private void OnDestroy()
        {
            if (hammerButton != null)
                hammerButton.onClick.RemoveListener(OnHammerButtonClicked);
        }
    }
}
