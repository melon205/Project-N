using UnityEngine;
using DG.Tweening;

public class MapController : MonoBehaviour
{
    [Header("UI References")]
    public RectTransform mapPanelRect;
    
    [Header("Settings")]
    public float transitionDuration = 0.5f;
    public Ease transitionEase = Ease.OutCubic;

    private bool isMapActive = false;
    private float visibleY = 0f;
    
    // 지도가 완전히 숨겨지기 위한 Y 좌표를 계산하는 함수
    private float GetHiddenY()
    {
        if (mapPanelRect == null) return 3000f; // 기본값
        
        // 부모(Canvas) 높이의 절반 + 자기 자신 높이의 절반 + 여유분(100)
        // 화면 해상도가 바뀌어도 대응할 수 있도록 계산합니다.
        float screenHeight = 2340f; // 기준 해상도
        return (screenHeight / 2f) + (mapPanelRect.rect.height / 2f) + 100f;
    }

    void Awake()
    {
        if (mapPanelRect != null)
        {
            // 시작 시 계산된 Hidden 위치로 즉시 이동
            mapPanelRect.anchoredPosition = new Vector2(0, GetHiddenY());
        }
    }

    public void ToggleMap()
    {
        if (mapPanelRect == null) return;

        isMapActive = !isMapActive;
        
        // 목표 위치 설정
        float targetY = isMapActive ? visibleY : GetHiddenY();

        // 부드러운 이동 애니메이션
        mapPanelRect.DOAnchorPosY(targetY, transitionDuration)
                    .SetEase(transitionEase)
                    .OnComplete(() => {
                        // 선택사항: 지도가 완전히 닫히면 오브젝트를 비활성화해서 터치를 막을 수도 있습니다.
                        // if (!isMapActive) mapPanelRect.gameObject.SetActive(false);
                    });

        Debug.Log("지도 활성화 상태: " + isMapActive);
    }
}