using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;
using UnityEngine.SceneManagement;
using TMPro; // If using TextMeshPro for UI texts
using System.Collections.Generic;
using System.Linq;
using UnityEngine.InputSystem;
public class PauseMenu : MonoBehaviour
{
    public static bool GameIsPaused = false;

    [Header("UI Panels")]
    public GameObject pauseMenuUI;
    public GameObject settingsMenuUI;
    public GameObject controlsMenu;
    public GameObject displayMenu;
    public GameObject audioMenu;
    public GameObject customizeControlsMenu;
    public GameObject confirmationDialogUI;
    public GameObject Background;

    [Header("Buttons")]
    public Button continueButton;
    public Button settingsButton;
    public Button mainMenuButton;

    public Button confirmYesButton;
    public Button confirmNoButton;
    public TextMeshProUGUI confirmationText;

    [Header("Audio")]
    public AudioSource audioSource;
    public AudioSource audioSourceNoise;

    public VideoPlayer glitchVideo; // Reference to the VideoPlayer
    public RawImage glitchImage;

    private Stack<GameObject> menuStack = new Stack<GameObject>();
    private GameObject currentMenu;
    private bool isTransitioning = false; // Not used currently, but can be used if adding transitions

    private List<AudioSource> pausedAudioSources = new List<AudioSource>();
    private VehicleControls inputActions;
    private void Start()
    {

        // Ensure all menus are inactive except the pause menu
        pauseMenuUI.SetActive(false);

        if (settingsMenuUI != null) settingsMenuUI.SetActive(false);
        if (controlsMenu != null) controlsMenu.SetActive(false);
        if (displayMenu != null) displayMenu.SetActive(false);
        if (audioMenu != null) audioMenu.SetActive(false);
        if (customizeControlsMenu != null) customizeControlsMenu.SetActive(false);
        if (confirmationDialogUI != null) confirmationDialogUI.SetActive(false);
        if (Background != null) Background.SetActive(false);
        // Add listeners to buttons
        continueButton.onClick.AddListener(Resume);
        //settingsButton.onClick.AddListener(OpenSettingsMenu);
        mainMenuButton.onClick.AddListener(ShowConfirmationDialog);
    }
    private void Awake()
    {
        inputActions = new VehicleControls();
    }
    private void OnEnable()
    {
        inputActions.UI.Enable();
        inputActions.UI.Pause.performed += OnPausePressed;
    }

    private void OnDisable()
    {
        inputActions.UI.Pause.performed -= OnPausePressed;
        inputActions.UI.Disable();
    }
    private void OnPausePressed(InputAction.CallbackContext context)
    {
        if (GameIsPaused)
        {
            if (currentMenu != pauseMenuUI)
            {
                OnBackButtonPressed();
            }
            else
            {
                Resume();
            }
        }
        else
        {
            Pause();
        }
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        if (!hasFocus && !GameIsPaused)
        {
            Pause();
        }
    }

    public void ShowConfirmationDialog()
    {
        PlayButtonClickSound();
        NavigateToMenu(confirmationDialogUI);

        // Set the confirmation text
        if (confirmationText != null)
        {
            confirmationText.text = "Are you sure you want to return to the main menu?";
        }

        // Add listeners to the confirmation buttons
        confirmYesButton.onClick.RemoveAllListeners();
        confirmYesButton.onClick.AddListener(GoToMainMenu);

        confirmNoButton.onClick.RemoveAllListeners();
        confirmNoButton.onClick.AddListener(OnBackButtonPressed);
    }

    public void Resume()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        audioSourceNoise.Stop();
        Background.SetActive(false);
        PlayButtonClickSound();
        pauseMenuUI.SetActive(false);
        Time.timeScale = 1f; // Resume game time
        GameIsPaused = false;
        // Lock cursor
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        ResumeAllAudio();
        // Clear menu stack
        menuStack.Clear();
        currentMenu = null;
    }

    private void Pause()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        audioSourceNoise.Play();
        Background.SetActive(true);
        PlayButtonClickSound();
        pauseMenuUI.SetActive(true);
        Time.timeScale = 0f; // Pause game time
        GameIsPaused = true;

        // Initialize menu stack
        menuStack.Clear();
        menuStack.Push(pauseMenuUI);
        currentMenu = pauseMenuUI;
        PauseAllAudio();
        // Unlock cursor
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void PauseAllAudio()
    {
        // Clear the list to prevent duplicates
        pausedAudioSources.Clear();

        // Find all active AudioSources in the scene
        AudioSource[] allAudioSources = FindObjectsOfType<AudioSource>();

        foreach (AudioSource audioSource in allAudioSources)
        {
            if (audioSource.isPlaying && audioSource != this.audioSource && audioSource != this.audioSourceNoise)  // Exclude the UI audio source
            {
                audioSource.Pause();
                pausedAudioSources.Add(audioSource);
            }
        }
    }

    private void ResumeAllAudio()
    {
        foreach (AudioSource audioSource in pausedAudioSources)
        {
            if (audioSource != null)
            {
                audioSource.UnPause();
            }
        }

        // Clear the list after resuming
        pausedAudioSources.Clear();
    }

    public void OpenSettingsMenu()
    {
        PlayButtonClickSound();
        NavigateToMenu(settingsMenuUI);
    }

    public void OnBackButtonPressed()
    {
        PlayButtonClickSound();
        NavigateBack();
    }

    public void GoToMainMenu()
    {

        PlayButtonClickSound();
        Time.timeScale = 1f; // Ensure time scale is reset
        GameIsPaused = false;


        // Load the main menu scene (replace "MainMenu" with your scene name)
        SceneManager.LoadScene("MainMenu");
    }

    public void NavigateToMenu(GameObject targetMenu)
    {
        if (isTransitioning || targetMenu == null)
            return;

        // Prevent navigating to the same menu
        if (currentMenu == targetMenu)
            return;

        menuStack.Push(targetMenu);
        GameObject previousMenu = currentMenu;

        // Transition effect if needed (optional)
        previousMenu.SetActive(false);
        targetMenu.SetActive(true);
        currentMenu = targetMenu;
    }

    public void NavigateBack()
    {
        if (isTransitioning || menuStack.Count <= 1)
            return;

        GameObject currentMenuToHide = currentMenu;
        menuStack.Pop(); // Remove current menu from stack
        GameObject previousMenu = menuStack.Peek(); // Get previous menu

        // Transition effect if needed (optional)
        currentMenuToHide.SetActive(false);
        previousMenu.SetActive(true);
        currentMenu = previousMenu;
    }

    private void PlayButtonClickSound()
    {
        if (audioSource != null)
        {
            audioSource.Play();
        }
    }

    // Methods to navigate to submenus

    public void GoToControlsMenu()
    {
        PlayButtonClickSound();
        NavigateToMenu(controlsMenu);
    }

    public void GoToDisplayMenu()
    {
        PlayButtonClickSound();
        NavigateToMenu(displayMenu);
    }

    public void GoToAudioMenu()
    {
        PlayButtonClickSound();
        NavigateToMenu(audioMenu);
    }

    public void GoToCustomizeControlsMenu()
    {
        PlayButtonClickSound();
        NavigateToMenu(customizeControlsMenu);
    }
}
