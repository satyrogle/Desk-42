using UnityEngine;
using UnityEngine.UI;
using Desk42.Core;
using System.Collections.Generic;

namespace Desk42.UI
{
    /// <summary>
    /// Renders the Conspiracy Board where the player places collected Post-It notes.
    /// Tracks solved clusters for the Great Audit ending.
    /// </summary>
    public class ConspiracyBoardUI : MonoBehaviour
    {
        [Header("Board")]
        [SerializeField] private RectTransform _boardArea;
        [SerializeField] private GameObject _postItPrefab;
        [SerializeField] private Button _closeButton;

        [Header("Completion")]
        [Tooltip("Number of solved clusters required to unlock The Great Audit.")]
        [SerializeField] private int _requiredClusterCount = 5;

        private MetaProgressData _meta;
        private List<GameObject> _spawnedPostIts = new List<GameObject>();

        private void Start()
        {
            if (_closeButton != null)
                _closeButton.onClick.AddListener(() => gameObject.SetActive(false));

            if (GameManager.Instance != null && GameManager.Instance.Meta != null)
                Initialize(GameManager.Instance.Meta);
        }

        private void Initialize(MetaProgressData meta)
        {
            _meta = meta;
            RefreshBoard();
        }

        public void RefreshBoard()
        {
            if (_meta == null) return;

            foreach (var go in _spawnedPostIts) Destroy(go);
            _spawnedPostIts.Clear();

            foreach (var fragment in _meta.ConspiracyBoard.Fragments)
            {
                if (!fragment.IsPlacedOnBoard) continue;

                var go = Instantiate(_postItPrefab, _boardArea);
                var rect = go.GetComponent<RectTransform>();
                rect.anchoredPosition = new Vector2(fragment.BoardPositionX, fragment.BoardPositionY);

                // Set text / visual based on fragment ID
                var textUI = go.GetComponentInChildren<TMPro.TextMeshProUGUI>();
                if (textUI != null) textUI.text = fragment.FragmentId;

                _spawnedPostIts.Add(go);
            }

            CheckBoardCompletion();
        }

        /// <summary>
        /// Evaluates whether all required fragment clusters are solved.
        /// If so, unlocks The Great Audit ending on MetaProgressData.
        /// </summary>
        private void CheckBoardCompletion()
        {
            if (_meta == null) return;
            if (_meta.ConspiracyBoard.GreatAuditUnlocked) return; // already done

            if (_meta.ConspiracyBoard.SolvedClusters.Count >= _requiredClusterCount)
            {
                _meta.ConspiracyBoard.GreatAuditUnlocked = true;
                SaveSystem.SaveMeta(_meta);

                RumorMill.PublishDeferred(new MilestoneReachedEvent(
                    MilestoneID.TheGreatAudit));

                Debug.Log($"[ConspiracyBoard] All {_requiredClusterCount} clusters solved. " +
                          "The Great Audit unlocked.");
            }
        }

        /// <summary>
        /// Called by the player when they confirm a new cluster link.
        /// Add the cluster ID to SolvedClusters and refresh.
        /// </summary>
        public void SolveCluster(string clusterId)
        {
            if (_meta == null) return;

            if (_meta.ConspiracyBoard.SolvedClusters.Add(clusterId))
            {
                SaveSystem.SaveMeta(_meta);
                Debug.Log($"[ConspiracyBoard] Cluster '{clusterId}' solved " +
                          $"({_meta.ConspiracyBoard.SolvedClusters.Count}/{_requiredClusterCount}).");
                RefreshBoard();
            }
        }
    }
}

