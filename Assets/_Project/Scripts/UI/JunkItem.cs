using UnityEngine;
using UnityEngine.EventSystems;

namespace Desk42.UI
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public class JunkItem : MonoBehaviour, IPointerEnterHandler, IPointerMoveHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        private RectTransform _rt;
        private Vector2 _velocity;
        private float _angularVelocity;
        private bool _isDragging;

        private void Awake()
        {
            _rt = GetComponent<RectTransform>();
        }

        private void Update()
        {
            if (_isDragging) return;

            // Apply friction
            if (_velocity.sqrMagnitude > 0.01f)
            {
                _rt.anchoredPosition += _velocity * Time.deltaTime;
                _velocity = Vector2.Lerp(_velocity, Vector2.zero, Time.deltaTime * 3f);
            }
            
            if (Mathf.Abs(_angularVelocity) > 0.01f)
            {
                _rt.Rotate(Vector3.forward, _angularVelocity * Time.deltaTime);
                _angularVelocity = Mathf.Lerp(_angularVelocity, 0f, Time.deltaTime * 2f);
            }
        }

        public void ApplyImpulse(Vector2 force, float torque)
        {
            _velocity += force;
            _angularVelocity += torque;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (!_isDragging) Sweep(eventData.delta);
        }

        public void OnPointerMove(PointerEventData eventData)
        {
            if (!_isDragging) Sweep(eventData.delta);
        }

        private void Sweep(Vector2 delta)
        {
            if (delta.sqrMagnitude > 4f)
            {
                _velocity += delta * 15f;
                _angularVelocity += Random.Range(-300f, 300f);
            }
        }

        public void OnBeginDrag(PointerEventData eventData) 
        {
            _isDragging = true;
            _velocity = Vector2.zero;
            _angularVelocity = 0f;
        }
        
        public void OnDrag(PointerEventData eventData)
        {
            if (RectTransformUtility.ScreenPointToWorldPointInRectangle(
                transform.parent as RectTransform, eventData.position, eventData.pressEventCamera, out Vector3 worldPoint))
            {
                _rt.position = worldPoint;
            }
        }
        
        public void OnEndDrag(PointerEventData eventData) 
        {
            _isDragging = false;
            _velocity = eventData.delta * 20f;
            _angularVelocity = Random.Range(-200f, 200f);
        }
    }
}
