using UnityEngine;

/// <summary>
/// Shown when the player reaches the cat bed with all food collected.
/// Called by LevelExit via ShowWin().
/// </summary>
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

    public void MainMenu() => GameManager.Instance?.LoadScene(0);
    public void Quit()     => GameManager.Instance?.QuitGame();
}
