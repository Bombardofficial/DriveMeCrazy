using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System.Collections.Generic;
using UnityEngine.InputSystem;

public class MenuManager : MonoBehaviour
{
    // References to your menus and UI elements
    public GameObject titles;
    public GameObject mainMenu;
    public GameObject settingsMenu;
    public GameObject controlsMenu;
    public GameObject displayMenu;
    public GameObject audioMenu;
    public GameObject customizeControlsMenu;
    public GameObject storyMenu;
    public GameObject playerJoinPanel;
    [Header("Quit Confirmation")]
    public GameObject quitConfirmationDialog;  // your new panel
    public Button quitConfirmYesButton;
    public Button quitConfirmNoButton;


    public MenuTransitionEffect transitionEffect;
    public AudioSource fastForwardSound;
    public AudioSource rewindSound;

    private bool isTransitioning = false;
    private Stack<GameObject> menuStack = new Stack<GameObject>();
    private VehicleControls inputActions;
    // Class-level variable to keep track of the current menu
    private GameObject currentMenu;

    void Start()
    {
        // Initialize by showing the main menu
        ShowOnlyMenu(mainMenu);
        titles.SetActive(true);

        // Clear and initialize the menu stack
        menuStack.Clear();
        menuStack.Push(mainMenu);

        // HIDE the quit?dialog at launch
        quitConfirmationDialog.SetActive(false);

        // Wire up the Yes/No buttons
        quitConfirmYesButton.onClick.AddListener(OnQuitConfirmed);
        quitConfirmNoButton.onClick.AddListener(OnQuitCanceled);

        // Set the current menu
        currentMenu = mainMenu;
    }

    void Awake()
    {
        inputActions = new VehicleControls(); // instantiate
    }

    void OnEnable()
    {
        inputActions.UI.Enable();
        inputActions.UI.Pause.performed += OnPausePressed;
    }

    void OnDisable()
    {
        inputActions.UI.Pause.performed -= OnPausePressed;
        inputActions.UI.Disable();
    }

    private void OnPausePressed(InputAction.CallbackContext context)
    {
        if (currentMenu != mainMenu && quitConfirmationDialog.activeSelf == false)
        {
            NavigateToMenu(mainMenu);
        }
    }


    /// <summary>
    /// Called by your “Quit” button in the UI.
    /// </summary>
    public void QuitGame()
    {
        if (isTransitioning) return;
        
        // Instead of quitting immediately, show "Are you sure?"
        quitConfirmationDialog.SetActive(true);
        // optionally hide the rest of the menu so only the dialog is visible:
        currentMenu.SetActive(false);
        titles.SetActive(false);
    }

    /// <summary>
    /// The player clicked “Yes, I really want to quit.”
    /// </summary>
    private void OnQuitConfirmed()
    {
        Application.Quit();
        // (in the Editor this won’t do anything, so you can also
        Debug.Log("Quit!");
    }

    /// <summary>
    /// The player clicked “No, nevermind.”
    /// </summary>
    private void OnQuitCanceled()
    {
        // hide the dialog and restore the previous menu
        quitConfirmationDialog.SetActive(false);
        currentMenu.SetActive(true);
        titles.SetActive(true);
    }

    /// <summary>
    /// Navigates to a specified menu, pushing the current menu onto the stack.
    /// </summary>
    public void NavigateToMenu(GameObject targetMenu)
    {
        if (isTransitioning || targetMenu == null)
            return;

        // Prevent navigating to the same menu
        if (currentMenu == targetMenu)
            return;

        menuStack.Push(targetMenu);
        GameObject previousMenu = currentMenu;

        StartTransition(
            () =>
            {
                // Middle of transition: switch menus
                previousMenu.SetActive(false);
                targetMenu.SetActive(true);
                currentMenu = targetMenu;
                UpdateTitlesVisibility();
            },
            TransitionDirection.Forward);
    }

    /// <summary>
    /// Navigates back to the previous menu, popping the current menu off the stack.
    /// </summary>
    public void NavigateBack()
    {
        if (isTransitioning || menuStack.Count <= 1)
            return;

        GameObject currentMenuToHide = currentMenu;
        menuStack.Pop(); // Remove current menu from stack
        GameObject previousMenu = menuStack.Peek(); // Get previous menu

        StartTransition(
            () =>
            {
                // Middle of transition: switch menus
                currentMenuToHide.SetActive(false);
                previousMenu.SetActive(true);
                currentMenu = previousMenu;
                UpdateTitlesVisibility();
            },
            TransitionDirection.Backward);
    }

    /// <summary>
    /// Starts the game, transitioning to the next scene.
    /// </summary>
    public void PlayGame()
    {
        if (isTransitioning)
            return;

        isTransitioning = true;
        DisableAllButtons();

        fastForwardSound.Play();

        transitionEffect.TriggerTransition(
            null,
            () =>
            {
                // After transition completes
                isTransitioning = false;
                //SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex + 1);
                mainMenu.SetActive(false);
                playerJoinPanel.SetActive(true);
            }
        );
    }
    /// <summary>
    /// Starts the transition effect with specified actions.
    /// </summary>
    private void StartTransition(System.Action onTransitionAction, TransitionDirection direction)
    {
        if (isTransitioning)
            return;

        isTransitioning = true;
        DisableAllButtons();

        // Play appropriate sound
        if (direction == TransitionDirection.Forward)
            fastForwardSound.Play();
        else
            rewindSound.Play();

        transitionEffect.TriggerTransition(
            () =>
            {
                // Middle of transition: execute action
                onTransitionAction?.Invoke();
                // Disable buttons in the new menu
                DisableAllButtons();
            },
            () =>
            {
                // After transition completes
                isTransitioning = false;
                EnableAllButtons();
            }
        );
    }

    /// <summary>
    /// Disables all buttons in all menus.
    /// </summary>
    private void DisableAllButtons()
    {
        GameObject[] menus = { mainMenu, settingsMenu, controlsMenu, customizeControlsMenu, storyMenu };
        foreach (GameObject menu in menus)
        {
            if (menu == null) continue;

            Button[] buttons = menu.GetComponentsInChildren<Button>(true);
            foreach (Button button in buttons)
            {
                button.interactable = false;
            }
        }
    }

    /// <summary>
    /// Enables buttons in the current menu after transitions.
    /// </summary>
    private void EnableAllButtons()
    {
        if (currentMenu == null)
            return;

        Button[] buttons = currentMenu.GetComponentsInChildren<Button>(true);
        foreach (Button button in buttons)
        {
            button.interactable = true;
        }
    }

    /// <summary>
    /// Updates the visibility of the titles based on the current menu.
    /// </summary>
    private void UpdateTitlesVisibility()
    {
        // Show titles only on the main menu
        titles.SetActive(currentMenu == mainMenu);
    }

    /// <summary>
    /// Shows only the specified menu, hiding all others.
    /// </summary>
    private void ShowOnlyMenu(GameObject menuToShow)
    {
        GameObject[] menus = { mainMenu, settingsMenu, controlsMenu, customizeControlsMenu, storyMenu };
        foreach (GameObject menu in menus)
        {
            if (menu != null)
                menu.SetActive(menu == menuToShow);
        }

        UpdateTitlesVisibility();
    }

    // Enum to represent transition direction
    public enum TransitionDirection
    {
        Forward,
        Backward
    }

    // Methods to be assigned to buttons

    public void GoToSettingsMenu()
    {
        NavigateToMenu(settingsMenu);
    }

    public void GoToControlsMenu()
    {
        NavigateToMenu(controlsMenu);
    }

    public void GoToDisplaysMenu()
    {
        NavigateToMenu(displayMenu);
    }
    public void GoToAudioMenu()
    {
        NavigateToMenu(audioMenu);
    }

    public void GoToCustomizeControlsMenu()
    {
        NavigateToMenu(customizeControlsMenu);
    }

    public void GoToStoryMenu()
    {
        NavigateToMenu(storyMenu);
    }

    /// <summary>
    /// Called by back buttons to navigate to the previous menu.
    /// </summary>
    public void OnBackButtonPressed()
    {
        NavigateBack();
    }
}
