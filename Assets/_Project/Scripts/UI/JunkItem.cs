using UnityEngine;
using UnityEngine.EventSystems;

namespace Desk42.UI
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody2D))]
    public class JunkItem : MonoBehaviour, IPointerEnterHandler, IPointerMoveHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        private Rigidbody2D _rb;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody2D>();
            // Ensure gravity is somewhat appropriate if it's a top-down desk
            _rb.gravityScale = 0f;
            _rb.drag = 3f;
            _rb.angularDrag = 2f;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            Sweep(eventData.delta);
        }

        public void OnPointerMove(PointerEventData eventData)
        {
            Sweep(eventData.delta);
        }

        private void Sweep(Vector2 delta)
        {
            // Sweep with mouse velocity
            if (_rb != null && delta.sqrMagnitude > 4f)
            {
                // Apply force in the direction of mouse movement
                _rb.AddForce(delta * 50f);
                _rb.AddTorque(Random.Range(-50f, 50f));
            }
        }

        // Allow direct dragging as well
        public void OnBeginDrag(PointerEventData eventData) 
        {
            if (_rb != null)
            {
                _rb.velocity = Vector2.zero;
                _rb.angularVelocity = 0f;
            }
        }
        
        public void OnDrag(PointerEventData eventData)
        {
            if (_rb != null)
            {
                // Move towards mouse position
                if (RectTransformUtility.ScreenPointToWorldPointInRectangle(
                    transform.parent as RectTransform, eventData.position, eventData.pressEventCamera, out Vector3 worldPoint))
                {
                    _rb.MovePosition(worldPoint);
                }
                else if (eventData.pressEventCamera != null)
                {
                    _rb.MovePosition(eventData.pressEventCamera.ScreenToWorldPoint(new Vector3(eventData.position.x, eventData.position.y, 10f)));
                }
            }
        }
        
        public void OnEndDrag(PointerEventData eventData) 
        {
            if (_rb != null)
            {
                // Fling the object when releasing
                _rb.AddForce(eventData.delta * 20f);
            }
        }
    }
}
