using DG.Tweening;
using UnityEngine;

public class MapController : MonoBehaviour
{
    private const float HiddenPadding = 100f;
    private const float FallbackHiddenY = 3000f;

    [Header("UI References")]
    public RectTransform mapPanelRect;

    [Header("Settings")]
    public float transitionDuration = 0.5f;
    public Ease transitionEase = Ease.OutCubic;
    [SerializeField] private float visibleY = 0f;

    private bool isMapActive;

    private void Awake()
    {
        if (mapPanelRect == null)
        {
            return;
        }

        SetPanelY(GetHiddenY());
    }

    public void ToggleMap()
    {
        if (mapPanelRect == null)
        {
            return;
        }

        SetMapVisible(!isMapActive);
    }

    private void SetMapVisible(bool visible)
    {
        isMapActive = visible;
        float targetY = isMapActive ? visibleY : GetHiddenY();

        mapPanelRect.DOKill();
        mapPanelRect
            .DOAnchorPosY(targetY, transitionDuration)
            .SetEase(transitionEase);

        Debug.Log($"Map active: {isMapActive}", this);
    }

    private float GetHiddenY()
    {
        if (mapPanelRect == null)
        {
            return FallbackHiddenY;
        }

        RectTransform parentRect = mapPanelRect.parent as RectTransform;
        float referenceHeight = parentRect != null ? parentRect.rect.height : Screen.height;
        return (referenceHeight * 0.5f) + (mapPanelRect.rect.height * 0.5f) + HiddenPadding;
    }

    private void SetPanelY(float y)
    {
        Vector2 position = mapPanelRect.anchoredPosition;
        position.y = y;
        mapPanelRect.anchoredPosition = position;
    }
}
