using UnityEngine;

public class MapController : MonoBehaviour
{
    // Inspector에서 'Map' 오브젝트(MapPanel의 부모)를 연결하세요.
    public GameObject mapObject; 
    
    private bool isMapActive = false;

    void Awake()
    {
        // 1. 시작할 때 지도를 안 보이게 설정
        if (mapObject != null)
        {
            mapObject.SetActive(false);
        }
    }

    // 2 & 3. 버튼을 누를 때마다 상태를 반전시키는 함수
    public void ToggleMap()
    {
        if (mapObject == null) return;

        isMapActive = !isMapActive; // 상태 반전 (true -> false, false -> true)
        mapObject.SetActive(isMapActive);

        // 콘솔창에서 작동 확인용
        Debug.Log("지도 활성화 상태: " + isMapActive);
    }
}