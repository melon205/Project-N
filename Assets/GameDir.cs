using UnityEngine;
using UnityEngine.SceneManagement;

public class GameDir : MonoBehaviour
{    
    public void Awake()
    {
        DontDestroyOnLoad(gameObject);
    }
    public void stclick()
    {
        SceneManager.LoadScene("GameScene");
    }
}
