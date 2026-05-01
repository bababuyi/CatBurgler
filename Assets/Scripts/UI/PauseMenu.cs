// ═══════════════════════════════════════════════════════════════════════════
// PauseMenu.cs
// ═══════════════════════════════════════════════════════════════════════════
using UnityEngine;

/// <summary>
/// Pause menu. Toggle with Escape. Restores cursor visibility while paused.
/// Attach to a persistent Canvas or dedicated PauseMenu GameObject.
/// </summary>
public class PauseMenu : MonoBehaviour
{
    public GameObject pausePanel;
    private bool isPaused;

    private void Start()
    {
        if (pausePanel) pausePanel.SetActive(false);
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape)) TogglePause();
    }

    public void TogglePause() { if (isPaused) Resume(); else Pause(); }

    public void Pause()
    {
        isPaused = true;
        if (pausePanel) pausePanel.SetActive(true);
        Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void Resume()
    {
        isPaused = false;
        if (pausePanel) pausePanel.SetActive(false);
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    public void MainMenu() => GameManager.Instance?.LoadScene(0);
    public void Quit()     => GameManager.Instance?.QuitGame();
}
