using UnityEngine;
using UnityEngine.EventSystems;

namespace JungleBooze.UI.Common
{
    /// <summary>Pressed buttons shrink to 96 % (instant, no tween) for a tactile feel.</summary>
    public sealed class PressScale : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        private static readonly Vector3 Pressed = new Vector3(0.96f, 0.96f, 1f);

        public void OnPointerDown(PointerEventData eventData)
        {
            transform.localScale = Pressed;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            transform.localScale = Vector3.one;
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            transform.localScale = Vector3.one;
        }

        private void OnDisable()
        {
            transform.localScale = Vector3.one;
        }
    }
}
