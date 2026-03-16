using UnityEngine;
using UnityEngine.SceneManagement; // 씬 관리를 위한 필수 네임스페이스

public class InteractionHandler : MonoBehaviour
{
    [Header("Scene Names")]
    [Tooltip("MapScene")]
    public string detailSceneName = "MapScene";

    [Tooltip("GameScene")]
    public string mainSceneName = "GameScene";

    /// <summary>
    /// 상세 화면으로 이동합니다. (Map 이미지 버튼에 연결)
    /// </summary>
    public void GoToMapDetail()
    {
        if (!string.IsNullOrEmpty(detailSceneName))
        {
            SceneManager.LoadScene(detailSceneName);
            Debug.Log($"[Interaction] {detailSceneName}으로 이동합니다.");
        }
        else
        {
            Debug.LogError("이동할 씬 이름이 설정되지 않았습니다!");
        }
    }

    /// <summary>
    /// 메인 화면으로 돌아갑니다. (상세 화면의 '뒤로가기' 버튼에 연결)
    /// </summary>
    public void BackToMain()
    {
        if (!string.IsNullOrEmpty(mainSceneName))
        {
            SceneManager.LoadScene(mainSceneName);
            Debug.Log($"[Interaction] {mainSceneName}으로 돌아갑니다.");
        }
    }

    public void GoToGameDetail()
    {
        if (!string.IsNullOrEmpty(mainSceneName))
        {
            SceneManager.LoadScene(mainSceneName);
            Debug.Log($"[Interaction] {mainSceneName}으로 이동합니다.");
        }
        else
        {
            Debug.LogError("이동할 씬 이름이 설정되지 않았습니다!");
        }
    }
    
}