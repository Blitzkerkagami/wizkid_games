using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

public class DraggableLetter : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public TextMeshProUGUI letterText;

    private Vector3 startPosition;
    private Transform startParent;
    private CanvasGroup canvasGroup;
    private LayoutElement layoutElement;

    private void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = gameObject.AddComponent<CanvasGroup>();

        layoutElement = GetComponent<LayoutElement>();
        if (layoutElement == null)
            layoutElement = gameObject.AddComponent<LayoutElement>();
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        startPosition = transform.position;
        startParent = transform.parent;

        AlphabetOrderManager manager = FindObjectOfType<AlphabetOrderManager>();
        if (manager != null) manager.isDragging = true;

        if (layoutElement != null)
            layoutElement.ignoreLayout = true;

        canvasGroup.blocksRaycasts = false;
    }

    public void OnDrag(PointerEventData eventData)
    {
        transform.position = eventData.position;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        canvasGroup.blocksRaycasts = true;

        if (layoutElement != null)
            layoutElement.ignoreLayout = false;

        AlphabetOrderManager manager = FindObjectOfType<AlphabetOrderManager>();
        if (manager != null) manager.isDragging = false;

        if (eventData.pointerEnter != null)
        {
            DropSlot slot = eventData.pointerEnter.GetComponentInParent<DropSlot>();
            if (slot != null)
            {
                slot.PlaceLetter(this);
                return;
            }
        }

        transform.position = startPosition;
        transform.SetParent(startParent);
        transform.localScale = Vector3.one;

        if (manager != null) manager.ResetIdleTimer(false);
    }
}