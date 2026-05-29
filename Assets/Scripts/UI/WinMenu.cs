using UnityEngine;

public class WinMenu : MonoBehaviour
{
    public GameObject winPanel;

    private void Start()
    {
        if (winPanel) winPanel.SetActive(false);
    }

    public void ShowWin()
    {
        if (winPanel) winPanel.SetActive(true);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void Retry()
    {
        Time.timeScale = 1f;
        GameManager.Instance?.LoadScene(1);
    }
    public void MainMenu() => GameManager.Instance?.LoadScene(0);
    public void Quit() => GameManager.Instance?.QuitGame();
}