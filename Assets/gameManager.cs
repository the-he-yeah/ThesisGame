using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.InputSystem;

public class GameManager : MonoBehaviour
{
    [Header("Menu Panels")]
    [SerializeField] private CanvasGroup mainMenuPanel;
    [SerializeField] private CanvasGroup pauseMenuPanel;
    [SerializeField] private CanvasGroup gameOverPanel;
    [SerializeField] private CanvasGroup howToPlayPanel;


    [Header("Main Menu Buttons")]
    [SerializeField] private Button startGameButton;
    [SerializeField] private Button mainMenuHowToPlayButton;
    [SerializeField] private Button exitGameButton;

    [Header("Pause Menu Buttons")]
    [SerializeField] private Button resumeGameButton;
    [SerializeField] private Button resetGameButton;
    [SerializeField] private Button pauseMenuHowToPlayButton;
    [SerializeField] private Button goToMainMenuButton;

    [Header("Game Over Buttons")]
    [SerializeField] private Button retryGameButton;
    [SerializeField] private Button gameOverToMainMenuButton;

    [Header("How To Play")]
    [SerializeField] private Button goBackButton;

    [Header("Gameplay UI")]
    [SerializeField] private CanvasGroup gameplayUIPanel;
    [SerializeField] private TMPro.TextMeshProUGUI heartbeatText;
    [SerializeField] private string heartbeatFormat = "BPM: {0}";
    [SerializeField] private float updateInterval = 0.5f; // How often to update the display

    private InputSystem_Actions inputActions;
    private bool isGamePaused;
    private bool isGameActive;
    private CanvasGroup previousMenu;
    private float updateTimer = 0f;

    private void Awake()
    {
        // Initialize input system
        inputActions = new InputSystem_Actions();
        SetupInputActions();
        
        // Initialize game state
        isGameActive = false;
        isGamePaused = false;
    }

    private void UpdateHeartbeatDisplay()
    {
        if (heartbeatText != null && UDPReceiver.Instance != null)
        {
            int currentHeartbeat = UDPReceiver.Instance.Heartbeat;
            heartbeatText.text = string.Format(heartbeatFormat, currentHeartbeat);
            //Debug.Log($"Updating display with heartbeat: {currentHeartbeat}"); // Debug log
        }
        else
        {
            Debug.LogWarning("Missing reference: " + 
                           (heartbeatText == null ? "heartbeatText is null" : "") +
                           (UDPReceiver.Instance == null ? "UDPReceiver.Instance is null" : ""));
        }
    }

    private void Update()
    {
        if (isGameActive && !isGamePaused)
        {
            updateTimer += Time.deltaTime;
            if (updateTimer >= updateInterval)
            {
                UpdateHeartbeatDisplay();
                updateTimer = 0f;
                //Debug.Log("Update timer triggered"); // Debug log
            }
        }
    }

    private void OnEnable()
    {
        inputActions.Enable();
    }

    private void OnDisable()
    {
        inputActions.Disable();
    }

    private void Start()
    {
        SetupButtonListeners();
        ShowMainMenu();
    }

    private void SetupInputActions()
    {
        // Setup pause action
        inputActions.UI.Pause.performed += _ => OnPausePerformed();
    }

    private void SetupButtonListeners()
    {
        // Main Menu
        startGameButton.onClick.AddListener(StartGame);
        mainMenuHowToPlayButton.onClick.AddListener(() => ShowHowToPlay(mainMenuPanel));
        exitGameButton.onClick.AddListener(ExitGame);

        // Pause Menu
        resumeGameButton.onClick.AddListener(ResumeGame);
        resetGameButton.onClick.AddListener(RestartGame);
        pauseMenuHowToPlayButton.onClick.AddListener(() => ShowHowToPlay(pauseMenuPanel));
        goToMainMenuButton.onClick.AddListener(QuitToMainMenu);

        // Game Over
        retryGameButton.onClick.AddListener(RestartGame);
        gameOverToMainMenuButton.onClick.AddListener(QuitToMainMenu);

        // How To Play
        goBackButton.onClick.AddListener(GoBackFromHowToPlay);
    }

    #region Menu Management

    private void ShowMenu(CanvasGroup menu)
    {
        HideAllMenus();
        menu.alpha = 1;
        menu.interactable = true;
        menu.blocksRaycasts = true;
        ShowCursor();
    }

    private void HideAllMenus()
    {
        SetMenuState(mainMenuPanel, false);
        SetMenuState(pauseMenuPanel, false);
        SetMenuState(gameOverPanel, false);
        SetMenuState(howToPlayPanel, false);
        if (gameplayUIPanel != null)
        {
            SetMenuState(gameplayUIPanel, false);
        }
    }

    private void SetMenuState(CanvasGroup menu, bool shown)
    {
        menu.alpha = shown ? 1 : 0;
        menu.interactable = shown;
        menu.blocksRaycasts = shown;
    }

    private void ShowMainMenu()
    {
        ShowMenu(mainMenuPanel);
        Time.timeScale = 0;
        isGameActive = false;
        DisableGameplayInput();
        Debug.Log("Showing Main Menu");
    }

    #endregion

    #region Game State Management

    private void SetGameplayUIState(bool shown)
    {
        if (gameplayUIPanel != null)
        {
            gameplayUIPanel.alpha = shown ? 1 : 0;
            gameplayUIPanel.interactable = false; // Keep it non-interactable
            gameplayUIPanel.blocksRaycasts = false; // Don't block raycasts
            
            if (shown)
            {
                UpdateHeartbeatDisplay(); // Update when showing UI
            }
        }
    }


    public void StartGame()
    {
        HideAllMenus();
        SetGameplayUIState(true);
        UpdateHeartbeatDisplay(); // Initial update
        Time.timeScale = 1;
        isGameActive = true;
        isGamePaused = false;
        HideCursor();
        EnableGameplayInput();
        Debug.Log("Starting Game");
    }

    private void OnPausePerformed()
    {
        if (isGameActive)
        {
            if (isGamePaused)
            {
                ResumeGame();
            }
            else
            {
                PauseGame();
            }
        }
    }

    public void PauseGame()
    {
        if (!isGameActive) return;
        
        ShowMenu(pauseMenuPanel);
        Time.timeScale = 0;
        isGamePaused = true;
        DisableGameplayInput();
        Debug.Log("Game Paused");
    }

    public void ResumeGame()
    {
        HideAllMenus();
        if (gameplayUIPanel != null)
        {
            gameplayUIPanel.alpha = 1;
            gameplayUIPanel.interactable = true;
            gameplayUIPanel.blocksRaycasts = true;
        }
        Time.timeScale = 1;
        isGamePaused = false;
        HideCursor();
        EnableGameplayInput();
        Debug.Log("Game Resumed");
    }

    public void RestartGame()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        Time.timeScale = 1;
        isGameActive = true;
        isGamePaused = false;
        HideCursor();
        EnableGameplayInput();
        Debug.Log("Restarting Game");
    }

    public void ShowGameOver()
    {
        Debug.Log("GameManager: ShowGameOver called");
        ShowMenu(gameOverPanel);
        Time.timeScale = 0;
        isGameActive = false;
        DisableGameplayInput();
        Debug.Log("GameManager: Game Over screen should be visible now");
    }

    public void QuitToMainMenu()
    {
        ShowMainMenu();
        Debug.Log("Returning to Main Menu");
    }

    public void ExitGame()
    {
        Debug.Log("Exiting Game");
        #if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
        #else
            Application.Quit();
        #endif
    }

    #endregion

    #region How To Play Management

    public void ShowHowToPlay(CanvasGroup callingMenu)
    {
        previousMenu = callingMenu;
        ShowMenu(howToPlayPanel);
        Debug.Log("Showing How To Play Screen");
    }

    public void GoBackFromHowToPlay()
    {
        if (previousMenu != null)
        {
            ShowMenu(previousMenu);
        }
        else
        {
            ShowMainMenu();
        }
        Debug.Log("Returning from How To Play Screen");
    }

    #endregion

    #region Input Management

    private void EnableGameplayInput()
    {
        inputActions.Player.Enable();
        inputActions.UI.Disable();
        inputActions.UI.Pause.Enable(); // Keep pause action enabled during gameplay
    }

    private void DisableGameplayInput()
    {
        inputActions.Player.Disable();
        inputActions.UI.Enable();
    }

    private void ShowCursor()
    {
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }

    private void HideCursor()
    {
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;
    }

    #endregion

    #region Public Helpers

    public bool IsGameActive() => isGameActive;
    public bool IsGamePaused() => isGamePaused;

    #endregion
}
