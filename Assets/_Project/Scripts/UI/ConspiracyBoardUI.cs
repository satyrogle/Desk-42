using UnityEngine;
using UnityEngine.UI;
using Desk42.Core;
using Desk42.Persistence;
using System.Collections.Generic;

namespace Desk42.UI
{
    /// <summary>
    /// Renders the Conspiracy Board where the player places collected Post-It notes.
    /// Tracks solved clusters for the Great Audit ending.
    /// </summary>
    public class ConspiracyBoardUI : MonoBehaviour
    {
        [SerializeField] private RectTransform _boardArea;
        [SerializeField] private GameObject _postItPrefab;
        [SerializeField] private Button _closeButton;

        private MetaProgressData _meta;
        private List<GameObject> _spawnedPostIts = new List<GameObject>();

        private void Start()
        {
            if (_closeButton != null)
                _closeButton.onClick.AddListener(() => gameObject.SetActive(false));
            
            if (GameManager.Instance != null && GameManager.Instance.Meta != null)
                Initialize(GameManager.Instance.Meta);
        }

        private void OnDestroy()
        {
            // Removed OnMetaProgressLoaded unsubscription
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
        }
    }
}
