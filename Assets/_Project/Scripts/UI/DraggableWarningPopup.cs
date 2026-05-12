using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Desk42.UI
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CanvasGroup))]
    public class DraggableWarningPopup : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerEnterHandler, IPointerExitHandler
    {
        private RectTransform _rectTransform;
        private CanvasGroup _canvasGroup;
        private Vector2 _dragOffset;
        
        private float _hoverTimer = 0f;
        private bool _isHovering = false;
        private bool _canDrag = false;
        private bool _isDragging = false;

        private void Awake()
        {
            _rectTransform = GetComponent<RectTransform>();
            _canvasGroup = GetComponent<CanvasGroup>();
        }

        private void Update()
        {
            if (_isHovering && !_canDrag)
            {
                _hoverTimer += Time.deltaTime;
                if (_hoverTimer >= 0.5f)
                {
                    _canDrag = true;
                    // Conflict Prevention: Change cursor or visual feedback to indicate grab-ability
                    // For now, we'll just tint it slightly to indicate it's draggable
                    var img = GetComponent<Image>();
                    if (img != null) img.color = new Color(0.9f, 0.9f, 0.9f, 1f);
                }
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            _isHovering = true;
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _isHovering = false;
            _hoverTimer = 0f;
            if (!_isDragging) 
            {
                _canDrag = false;
                var img = GetComponent<Image>();
                if (img != null) img.color = Color.white;
            }
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (!_canDrag) 
            {
                eventData.pointerDrag = null; 
                return; 
            }
            
            _isDragging = true;
            _canvasGroup.blocksRaycasts = false;
            _canvasGroup.alpha = 0.8f;
            
            if (transform.parent != null && transform.parent is RectTransform parentRect)
            {
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    parentRect, eventData.position, eventData.pressEventCamera, out Vector2 localMousePos);
                _dragOffset = _rectTransform.anchoredPosition - localMousePos;
            }
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!_isDragging) return;
            if (transform.parent != null && transform.parent is RectTransform parentRect)
            {
                if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    parentRect, eventData.position, eventData.pressEventCamera, out Vector2 localMousePos))
                {
                    _rectTransform.anchoredPosition = localMousePos + _dragOffset;
                }
            }
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (!_isDragging) return;
            _isDragging = false;
            _canvasGroup.blocksRaycasts = true;
            _canvasGroup.alpha = 1f;
        }
    }
}
