using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;

// 누르고 있는 동안 held=true, 누르는 순간 onTap 1회. 키보드 대체용 터치 버튼
public class TouchHoldButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
{
    public bool held;
    public UnityEvent onTap = new UnityEvent();

    public void OnPointerDown(PointerEventData e)
    {
        held = true;
        onTap.Invoke();
    }

    public void OnPointerUp(PointerEventData e) => held = false;
    public void OnPointerExit(PointerEventData e) => held = false;
}
