using UnityEngine;
using UnityEngine.SceneManagement;

public class InteractionHandler : MonoBehaviour
{
    [Header("Scene Names")]
    [Tooltip("StartScene")]
    public string startSceneName = "StartScene";

    [Tooltip("GameScene")]
    public string mainSceneName = "GameScene";

    public void StartToGame()
    {
        if (string.IsNullOrWhiteSpace(mainSceneName))
        {
            Debug.LogError("Main scene name is not configured.", this);
            return;
        }

        SceneManager.LoadScene(mainSceneName);
        Debug.Log($"[Interaction] Loading scene: {mainSceneName}", this);
    }
}
