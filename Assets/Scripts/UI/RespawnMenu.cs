using UnityEngine;

/// <summary>
/// Shown when the player dies. Pauses the game and offers Retry or Main Menu.
/// Called by HealthScript.OnDeath via the Show() method.
/// </summary>
public class RespawnMenu : MonoBehaviour
{
    public GameObject respawnPanel;
    private GameObject player;

    private void Start()
    {
        if (respawnPanel) respawnPanel.SetActive(false);
        player = GameObject.FindGameObjectWithTag("Player");
    }

    public void Show()
    {
        if (respawnPanel) respawnPanel.SetActive(true);
        Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void Respawn()
    {
        if (respawnPanel) respawnPanel.SetActive(false);
        if (player == null) player = GameObject.FindGameObjectWithTag("Player");

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        GameManager.Instance?.RespawnPlayer(player);
    }

    public void MainMenu() => GameManager.Instance?.LoadScene(0);
    public void Quit()     => GameManager.Instance?.QuitGame();
}
