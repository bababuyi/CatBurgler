using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Main menu buttons. Attach to a Canvas or dedicated MainMenu GameObject in Scene 0.
/// </summary>
public class MainMenu : MonoBehaviour
{
    private void Start()
    {
        // Always reset timescale when returning to menu
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void Play()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.LoadScene(1);
        else
            SceneManager.LoadScene(1);
    }

    public void Quit()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.QuitGame();
        else
        {
            Application.Quit();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#endif
        }
    }
}
