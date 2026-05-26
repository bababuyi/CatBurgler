using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Level Settings")]
    public int totalFoodItems = 5;

    [Header("References")]
    public GrandmaAI grandma;

    private int collectedFood;
    private bool levelComplete;
    private Vector3 checkpointPosition;
    private Quaternion checkpointRotation;
    private bool hasCheckpoint;

    public bool AllFoodCollected => collectedFood >= totalFoodItems;
    public int CollectedFood => collectedFood;

    public event System.Action<int, int> OnFoodCollected;
    public event System.Action OnAllFoodCollected;
    public event System.Action OnLevelComplete;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        if (grandma != null)
            grandma.OnAllWindowsClosed += HandleAllWindowsClosed;
        DontDestroyOnLoad(gameObject);
    }

    public void CollectFood()
    {
        if (levelComplete) return;
        collectedFood++;
        OnFoodCollected?.Invoke(collectedFood, totalFoodItems);
        if (collectedFood >= totalFoodItems)
            OnAllFoodCollected?.Invoke();
    }

    private void HandleAllWindowsClosed()
    {
        if (!levelComplete)
        {
            Debug.Log("GameManager: All windows closed — cat is trapped!");
            ReloadScene();
        }
    }

    public void RespawnPlayer(GameObject player)
    {
        if (player == null) { ReloadScene(); return; }

        var rb = player.GetComponent<Rigidbody>();
        if (rb) { rb.linearVelocity = Vector3.zero; rb.angularVelocity = Vector3.zero; }

        if (hasCheckpoint)
            player.transform.SetPositionAndRotation(checkpointPosition, checkpointRotation);
        else
            ReloadScene();

        player.GetComponent<HealthScript>()?.ResetHealth();
        Time.timeScale = 1f;
    }

    public void TriggerLevelComplete()
    {
        if (levelComplete) return;
        levelComplete = true;
        Time.timeScale = 0f;
        OnLevelComplete?.Invoke();
    }

    public void ReloadScene() => SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    public void LoadScene(int index) => SceneManager.LoadScene(index);

    public void QuitGame()
    {
        Application.Quit();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }
}
