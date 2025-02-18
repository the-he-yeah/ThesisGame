using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.Video;
using System.Linq;
using System.Collections;
using System.Collections.Generic;

public class GameManager : MonoBehaviour
{
    [Header("Menu Panels")]
    [SerializeField] private CanvasGroup mainMenuPanel;
    [SerializeField] private CanvasGroup pauseMenuPanel;
    [SerializeField] private CanvasGroup gameOverPanel;
    [SerializeField] private CanvasGroup howToPlayPanel;
    [SerializeField] private CanvasGroup calibratePanel;



    [Header("Main Menu Buttons")]
    [SerializeField] private Button startGameButton;
    [SerializeField] private Button mainMenuHowToPlayButton;
    [SerializeField] private Button exitGameButton;
    [SerializeField] private Button calibrateButton;


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

    [Header("Calibration")]
    [SerializeField] private Button startCalibrationButton;
    [SerializeField] private VideoPlayer calibrationVideo;
    [SerializeField] private GameObject calibrationCompleteText;
    [SerializeField] private Button backFromCalibrationButton;

    [Header("Heartbeat Calibration")]
    [SerializeField] private float maxHRDeviation = 10f;
    [SerializeField] private float calibrationSampleInterval = 0.2f;

    private InputSystem_Actions inputActions;
    private bool isGamePaused;
    private bool isGameActive;
    private CanvasGroup previousMenu;
    private float updateTimer = 0f;
    private List<int> calibrationHeartbeats = new List<int>();
    private float baselineHeartbeat;
    public float BaselineHeartbeat => baselineHeartbeat;  // Public getter for other scripts
    private bool isCalibrating = false;

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
        calibrateButton.onClick.AddListener(() => ShowCalibratePanel(mainMenuPanel));


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

        // Calibration Panel
        startCalibrationButton.onClick.AddListener(StartCalibration);
        backFromCalibrationButton.onClick.AddListener(GoBackFromCalibration);

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
        SetMenuState(calibratePanel, false);
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
        // Don't handle pause if we're in calibration
        if (calibratePanel.alpha > 0)
            return;

        // If in game
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
        // If in How To Play menu
        else if (howToPlayPanel.alpha > 0)
        {
            GoBackFromHowToPlay();
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
        Time.timeScale = 0;
        DisableGameplayInput();
        isGameActive = false;
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

    #region Calibration Management

    private float CalculateMedian(List<int> values)
    {
        var sortedValues = values.OrderBy(v => v).ToList();
        int count = sortedValues.Count;
        if (count == 0) return 0;
        
        if (count % 2 == 0)
        {
            return (sortedValues[count / 2 - 1] + sortedValues[count / 2]) / 2f;
        }
        return sortedValues[count / 2];
    }

    private float CalculateBaselineHeartbeat()
    {
        Debug.Log($"Starting baseline calculation with {calibrationHeartbeats.Count} readings");
        if (calibrationHeartbeats.Count == 0)
        {
            Debug.LogWarning("No heartbeat readings collected during calibration");
            return 0f;
        }
        float medianHR = CalculateMedian(calibrationHeartbeats);
        Debug.Log($"Median HR: {medianHR}");
        float minHR = medianHR - maxHRDeviation;
        float maxHR = medianHR + maxHRDeviation;
        Debug.Log($"Valid range: {minHR} to {maxHR}");

        var validHeartbeats = calibrationHeartbeats
            .Where(hr => hr >= minHR && hr <= maxHR)
            .ToList();

        Debug.Log($"Valid readings count: {validHeartbeats.Count}");

        if (validHeartbeats.Count == 0)
        {
            Debug.LogWarning("No valid heartbeats found within deviation range");
            return 0f;
        }

        float average = (float)validHeartbeats.Average();
        Debug.Log($"Calculated average: {average}");
        return average;
    }

    private IEnumerator RecordHeartbeats()
    {
        Debug.Log("Started recording heartbeats");
        calibrationHeartbeats.Clear();
        isCalibrating = true;
        
        // Keep recording until explicitly stopped
        while (isCalibrating)
        {
            if (UDPReceiver.Instance != null)
            {
                int currentHeartbeat = UDPReceiver.Instance.Heartbeat;
                calibrationHeartbeats.Add(currentHeartbeat);
                Debug.Log($"Added heartbeat: {currentHeartbeat}, Total readings: {calibrationHeartbeats.Count}");
            }
            else
            {
                Debug.LogWarning("UDPReceiver.Instance is null during recording");
            }
            yield return new WaitForSecondsRealtime(calibrationSampleInterval);
        }
        
        Debug.Log($"Finished recording. Total heartbeats collected: {calibrationHeartbeats.Count}");
    }

    private void ShowCalibratePanel(CanvasGroup callingMenu)  // Modified to accept callingMenu
    {
        previousMenu = callingMenu;    // Store the calling menu, just like How To Play
        ShowMenu(calibratePanel);
        Time.timeScale = 0;
        ResetCalibrationState();
        ShowCursor();  // Ensure cursor is visible
        DisableGameplayInput();
        isGameActive = false;
        Debug.Log("Showing Calibration Panel");
    }

    private void ResetCalibrationState()
    {
        // Set initial state of calibration elements
        if (startCalibrationButton != null)
            SetGameObjectState(startCalibrationButton.gameObject, true);
        if (calibrationVideo != null)
            SetGameObjectState(calibrationVideo.gameObject, false);
        if (calibrationCompleteText != null)
            SetGameObjectState(calibrationCompleteText, false);
        if (backFromCalibrationButton != null)
            SetGameObjectState(backFromCalibrationButton.gameObject, false);
    }

    private void SetGameObjectState(GameObject obj, bool state)
    {
        if (obj != null)
        {
            obj.SetActive(state);
        }
    }

    private void StartCalibration()
    {
        Time.timeScale = 0;
        calibrationHeartbeats.Clear();
        isCalibrating = true;  // Start recording flag
        
        if (startCalibrationButton != null)
            SetGameObjectState(startCalibrationButton.gameObject, false);
        
        if (calibrationVideo != null)
        {
            SetGameObjectState(calibrationVideo.gameObject, true);
            StartCoroutine(RecordHeartbeats());  // Start recording first
            calibrationVideo.Play();
            Debug.Log("Playing Calibration Video");
            calibrationVideo.loopPointReached += OnCalibrationVideoComplete;
        }
    }

    private void OnCalibrationVideoComplete(VideoPlayer vp)
    {
        isCalibrating = false;  // Stop recording
        StartCoroutine(FinalizeCalibration());
    }

    private IEnumerator FinalizeCalibration()
    {
        // Wait a short moment to ensure we've collected all readings
        yield return new WaitForSecondsRealtime(calibrationSampleInterval * 2);
        
        baselineHeartbeat = CalculateBaselineHeartbeat();
        Debug.Log($"Calibration complete. Baseline heartbeat: {baselineHeartbeat}");

        SetGameObjectState(calibrationCompleteText, true);
        if (backFromCalibrationButton != null)
            SetGameObjectState(backFromCalibrationButton.gameObject, true);
        
        if (calibrationVideo != null)
        {
            calibrationVideo.loopPointReached -= OnCalibrationVideoComplete;
        }
    }

    private void GoBackFromCalibration()
    {
        isCalibrating = false;  // Make sure to stop recording if user goes back
        if (calibrationVideo != null)
        {
            calibrationVideo.Stop();
        }
        
        if (previousMenu != null)
        {
            ShowMenu(previousMenu);
        }
        else
        {
            ShowMainMenu();
        }
    }

    #endregion

    #region Public Helpers

    public bool IsGameActive() => isGameActive;
    public bool IsGamePaused() => isGamePaused;

    public float GetBaselineHeartbeat()
    {
        return baselineHeartbeat;
    }

    #endregion
}
