using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public enum SudokuGameMode
{
    Daily,
    Classic
}

public enum SudokuDifficulty
{
    Easy,
    Medium,
    Hard,
    Expert,
    Master,
    Extreme
}

[DisallowMultipleComponent]
[AddComponentMenu("UI/Sudoku App Controller")]
public sealed class SudokuAppController : MonoBehaviour
{
    private const int DailyLevelCount = 10;
    private const int ClassicLevelCount = 20;
    private const int DailyRefreshHour = 8;
    private const string RuntimeGeneratedPrefix = "Generated_";
    private const string BottomNavName = "BottomNav";
    private const string DailyCardName = "Daily_Card";
    private const string MeCardName = "Me_Card";
    private const string ResumeProgressPopupName = "ResumeProgressPopup";
    private const string ButtonNewName = "Button_New";
    private const string ButtonContinueName = "Button_Continue";
    private const string LevelProgressPrefix = "Sudoku_LevelProgress_";

    [SerializeField] private SudukuGameController gameController;
    [SerializeField] private GameObject objectHome;
    [SerializeField] private GameObject dailyLevelsPage;
    [SerializeField] private GameObject classicDifficultyPage;
    [SerializeField] private GameObject wellDonePopup;
    [SerializeField] private GameObject resumeProgressPopup;
    [SerializeField] private GameObject resumeProgressPopupPrefab;
    [SerializeField] private GameObject bottomNav;
    [SerializeField] private GameObject dailyCardPage;
    [SerializeField] private GameObject meCardPage;
    [SerializeField] private GameObject btnLevelPrefab;
    [SerializeField] private GameObject[] classicLevelPages;

    private readonly Dictionary<SudokuDifficulty, GameObject> classicPages = new Dictionary<SudokuDifficulty, GameObject>();
    private readonly Dictionary<SudokuDifficulty, SudokuPuzzleSet> classicPuzzleSets = new Dictionary<SudokuDifficulty, SudokuPuzzleSet>();
    private readonly List<GameObject> menuPages = new List<GameObject>();

    private Canvas canvas;
    private Transform dailyLevelsPanel;
    private TMP_Text refreshCountdownText;
    private SudokuPuzzleSet dailyPuzzleSet;
    private Coroutine countdownRoutine;
    private GameObject activeLevelSelectPage;
    private SudokuGameMode activeGameMode;
    private SudokuDifficulty activeDifficulty;
    private int activeLevelIndex = -1;
    private int activeLevelCount;
    private string activeProgressKey;
    private string activeLevelSaveKey;
    private SudokuPuzzleSet activePuzzleSet;
    private SudokuPuzzleData activePuzzleData;
    private Animation wellDoneAnimation;
    private Button wellDoneNextButton;
    private Button wellDoneQuitButton;
    private Button resumeNewButton;
    private Button resumeContinueButton;
    private PendingPuzzleStart pendingPuzzleStart;
    private bool shouldShowBottomNav;

    private sealed class PendingPuzzleStart
    {
        public SudokuGameMode Mode;
        public SudokuDifficulty Difficulty;
        public int LevelIndex;
        public int LevelCount;
        public string ProgressKey;
        public SudokuPuzzleSet PuzzleSet;
        public SudokuPuzzleData PuzzleData;
        public string SaveKey;
        public SudokuLevelProgressData SavedProgress;
    }

    private void Awake()
    {
        CacheSceneReferences();
        BuildMissingPages();
        RegisterPages();
        BindHomeButtons();
        BindDailyPage();
        BindClassicDifficultyPage();
        BindClassicLevelPages();
        BindWellDonePopup();
        BindResumeProgressPopup();

        if (gameController != null)
        {
            gameController.SetGameBackHandler(ReturnToActiveLevelSelectPage);
            gameController.SetPuzzleCompletedHandler(HandlePuzzleCompleted);
        }
    }

    private void Start()
    {
        HideWellDonePopup();
        ShowHome();
    }

    private void LateUpdate()
    {
        SetBottomNavVisible(shouldShowBottomNav);
    }

    public void ShowHome()
    {
        SaveActivePuzzleProgressIfNeeded();
        activeLevelSelectPage = null;
        HideWellDonePopup();
        HideResumeProgressPopup();
        HideGameplay();
        ShowOnly(objectHome);
        ShowBottomNavForCurrentPage(true);
    }

    public void ShowDailyCard()
    {
        SaveActivePuzzleProgressIfNeeded();
        activeLevelSelectPage = null;
        HideWellDonePopup();
        HideResumeProgressPopup();
        HideGameplay();
        ShowOnly(dailyCardPage);
        ShowBottomNavForCurrentPage(true);
    }

    public void ShowMeCard()
    {
        SaveActivePuzzleProgressIfNeeded();
        activeLevelSelectPage = null;
        HideWellDonePopup();
        HideResumeProgressPopup();
        HideGameplay();
        ShowOnly(meCardPage);
        ShowBottomNavForCurrentPage(true);
    }

    public void ShowDailyLevels()
    {
        SaveActivePuzzleProgressIfNeeded();
        HideWellDonePopup();
        HideResumeProgressPopup();
        HideGameplay();
        EnsureDailyLevels();
        activeLevelSelectPage = dailyLevelsPage;
        ShowOnly(dailyLevelsPage);
        StartRefreshCountdown();
        ShowBottomNavForCurrentPage(false);
    }

    public void ShowClassicDifficulty()
    {
        SaveActivePuzzleProgressIfNeeded();
        activeLevelSelectPage = null;
        HideWellDonePopup();
        HideResumeProgressPopup();
        HideGameplay();
        ShowOnly(classicDifficultyPage);
        ShowBottomNavForCurrentPage(false);
    }

    public void ShowClassicLevels(SudokuDifficulty difficulty)
    {
        SaveActivePuzzleProgressIfNeeded();
        HideWellDonePopup();
        HideResumeProgressPopup();
        HideGameplay();
        EnsureClassicLevels(difficulty);
        GameObject page = classicPages[difficulty];
        activeLevelSelectPage = page;
        ShowOnly(page);
        ShowBottomNavForCurrentPage(false);
    }

    private void StartDailyLevel(int index)
    {
        EnsureDailyLevels();
        if (dailyPuzzleSet == null || dailyPuzzleSet.Puzzles == null || index < 0 || index >= dailyPuzzleSet.Puzzles.Length)
        {
            return;
        }

        activeLevelSelectPage = dailyLevelsPage;
        StartPuzzle(SudokuGameMode.Daily, SudokuDifficulty.Easy, index, dailyPuzzleSet.Puzzles.Length, GetDailyProgressKey(), dailyPuzzleSet, dailyPuzzleSet.Puzzles[index]);
    }

    private void StartClassicLevel(SudokuDifficulty difficulty, int index)
    {
        EnsureClassicLevels(difficulty);
        SudokuPuzzleSet set = classicPuzzleSets[difficulty];
        if (set.Puzzles == null || index < 0 || index >= set.Puzzles.Length)
        {
            return;
        }

        activeLevelSelectPage = classicPages[difficulty];
        StartPuzzle(SudokuGameMode.Classic, difficulty, index, set.Puzzles.Length, GetClassicProgressKey(difficulty), set, set.Puzzles[index]);
    }

    private void StartPuzzle(SudokuGameMode mode, SudokuDifficulty difficulty, int levelIndex, int levelCount, string progressKey, SudokuPuzzleSet puzzleSet, SudokuPuzzleData puzzleData)
    {
        string saveKey = GetLevelProgressKey(mode, difficulty, puzzleSet, levelIndex);
        if (!TryLoadLevelProgress(saveKey, out SudokuLevelProgressData savedProgress))
        {
            string legacySaveKey = GetLegacyLevelProgressKey(mode, difficulty, levelIndex);
            if (legacySaveKey != saveKey && TryLoadLevelProgress(legacySaveKey, out savedProgress))
            {
                PlayerPrefs.SetString(saveKey, PlayerPrefs.GetString(legacySaveKey));
                PlayerPrefs.DeleteKey(legacySaveKey);
                PlayerPrefs.Save();
                Debug.Log("Migrated sudoku level progress from " + legacySaveKey + " to " + saveKey, this);
            }
        }

        if (savedProgress != null)
        {
            pendingPuzzleStart = CreatePendingPuzzleStart(mode, difficulty, levelIndex, levelCount, progressKey, puzzleSet, puzzleData, saveKey, savedProgress);
            StartPuzzleInternal(mode, difficulty, levelIndex, levelCount, progressKey, puzzleSet, puzzleData, saveKey, null);
            ShowResumeProgressPopup();
            ShowBottomNavForCurrentPage(false);
            return;
        }

        StartPuzzleInternal(mode, difficulty, levelIndex, levelCount, progressKey, puzzleSet, puzzleData, saveKey, null);
    }

    private void StartPuzzleInternal(
        SudokuGameMode mode,
        SudokuDifficulty difficulty,
        int levelIndex,
        int levelCount,
        string progressKey,
        SudokuPuzzleSet puzzleSet,
        SudokuPuzzleData puzzleData,
        string saveKey,
        SudokuLevelProgressData progressData)
    {
        activeGameMode = mode;
        activeDifficulty = difficulty;
        activeLevelIndex = levelIndex;
        activeLevelCount = levelCount;
        activeProgressKey = progressKey;
        activeLevelSaveKey = saveKey;
        activePuzzleSet = puzzleSet;
        activePuzzleData = puzzleData;
        HideWellDonePopup();
        HideResumeProgressPopup();
        HideAllMenuPages();
        if (gameController != null)
        {
            if (progressData != null)
            {
                gameController.LoadPuzzleProgress(puzzleData, progressData);
            }
            else
            {
                gameController.LoadPuzzle(puzzleData);
            }
        }

        HideResumeProgressPopup();
        ShowBottomNavForCurrentPage(false);
    }

    private void ReturnToActiveLevelSelectPage()
    {
        SaveActivePuzzleProgressIfNeeded();

        if (activeLevelSelectPage == dailyLevelsPage)
        {
            ShowDailyLevels();
            return;
        }

        if (activeLevelSelectPage != null)
        {
            HideGameplay();
            ShowOnly(activeLevelSelectPage);
            ShowBottomNavForCurrentPage(false);
            return;
        }

        ShowHome();
    }

    private void HandlePuzzleCompleted()
    {
        if (activeLevelIndex < 0 || string.IsNullOrEmpty(activeProgressKey))
        {
            return;
        }

        UnlockNextLevel(activeProgressKey, activeLevelIndex, activeLevelCount);
        DeleteLevelProgress(activeLevelSaveKey);

        if (activeGameMode == SudokuGameMode.Daily)
        {
            EnsureDailyLevels();
        }
        else
        {
            EnsureClassicLevels(activeDifficulty);
        }

        ShowWellDonePopup();
    }

    private void CacheSceneReferences()
    {
        if (gameController == null)
        {
            gameController = FindFirstObjectByName<SudukuGameController>(null);
        }

        if (canvas == null)
        {
            canvas = FindFirstObjectByName<Canvas>("Canvas");
        }

        if (canvas == null)
        {
            GameObject canvasObject = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(750f, 1334f);
            scaler.matchWidthOrHeight = 0.5f;
        }

        if (objectHome == null)
        {
            Transform home = FindSceneTransformByName("ObjectHome");
            objectHome = home != null ? home.gameObject : null;
        }
        else
        {
            objectHome = EnsureScenePageInstance(objectHome, "ObjectHome");
        }

        if (dailyLevelsPage == null)
        {
            Transform page = FindSceneTransformByName("Daily_10_Levels");
            dailyLevelsPage = page != null ? page.gameObject : null;
        }
        else
        {
            dailyLevelsPage = EnsureScenePageInstance(dailyLevelsPage, "Daily_10_Levels");
        }

        if (classicDifficultyPage == null)
        {
            Transform page = FindSceneTransformByName("ClassicDifficultyPage");
            classicDifficultyPage = page != null ? page.gameObject : null;
        }
        else
        {
            classicDifficultyPage = EnsureScenePageInstance(classicDifficultyPage, "ClassicDifficultyPage");
        }

        if (wellDonePopup == null)
        {
            Transform popup = FindSceneTransformByName("Object_WellDone");
            wellDonePopup = popup != null ? popup.gameObject : null;
        }
        else
        {
            wellDonePopup = EnsureScenePageInstance(wellDonePopup, "Object_WellDone");
        }

        if (resumeProgressPopup == null)
        {
            Transform popup = FindSceneTransformByName(ResumeProgressPopupName);
            resumeProgressPopup = popup != null ? popup.gameObject : null;
        }

        if (resumeProgressPopup == null && resumeProgressPopupPrefab != null)
        {
            resumeProgressPopup = EnsureScenePageInstance(resumeProgressPopupPrefab, ResumeProgressPopupName);
        }
        else if (resumeProgressPopup != null)
        {
            resumeProgressPopup = EnsureScenePageInstance(resumeProgressPopup, ResumeProgressPopupName);
        }

        if (bottomNav == null)
        {
            Transform nav = FindSceneTransformByName(BottomNavName);
            bottomNav = nav != null ? nav.gameObject : null;
        }

        dailyCardPage = ResolvePage(dailyCardPage, DailyCardName);
        meCardPage = ResolvePage(meCardPage, MeCardName);

        CacheClassicLevelPages();
    }

    private GameObject ResolvePage(GameObject page, string pageName)
    {
        if (page == null)
        {
            Transform scenePage = FindSceneTransformByName(pageName);
            return scenePage != null ? scenePage.gameObject : null;
        }

        return EnsureScenePageInstance(page, pageName);
    }

    private void CacheClassicLevelPages()
    {
        classicPages.Clear();
        if (classicLevelPages != null)
        {
            foreach (GameObject page in classicLevelPages)
            {
                if (page == null)
                {
                    continue;
                }

                if (TryParseDifficultyFromPageName(page.name, out SudokuDifficulty difficulty))
                {
                    classicPages[difficulty] = EnsureScenePageInstance(page, GetClassicLevelsPageName(difficulty));
                }
            }
        }

        foreach (SudokuDifficulty difficulty in Enum.GetValues(typeof(SudokuDifficulty)))
        {
            if (classicPages.ContainsKey(difficulty))
            {
                continue;
            }

            Transform page = FindSceneTransformByName(GetClassicLevelsPageName(difficulty));
            if (page != null)
            {
                classicPages[difficulty] = page.gameObject;
            }
        }
    }

    private void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus)
        {
            SaveActivePuzzleProgressIfNeeded();
        }
    }

    private void OnApplicationQuit()
    {
        SaveActivePuzzleProgressIfNeeded();
    }

    private GameObject EnsureScenePageInstance(GameObject page, string pageName)
    {
        if (page == null)
        {
            return null;
        }

        if (page.scene.IsValid() && page.scene.isLoaded)
        {
            page.name = pageName;
            return page;
        }

        GameObject instance = Instantiate(page, canvas.transform);
        instance.name = pageName;
        return instance;
    }

    private void BuildMissingPages()
    {
        if (objectHome == null)
        {
            objectHome = CreateBasePage("ObjectHome", new Color(0.125f, 0.6f, 1f, 1f));
            Transform menuGroup = CreateRectChild(objectHome.transform, "MenuGroup", new Vector2(100f, 100f), Vector2.zero);
            VerticalLayoutGroup layout = menuGroup.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.spacing = 80f;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            CreateMenuButton(menuGroup, "BtnDailyChallenge", "Daily Challenge", new Vector2(300f, 100f));
            CreateMenuButton(menuGroup, "BtnClassicMode", "Classic Mode", new Vector2(300f, 100f));
        }

        if (dailyLevelsPage == null)
        {
            dailyLevelsPage = CreateBasePage("Daily_10_Levels", new Color(0.125f, 0.6f, 1f, 1f));
            dailyLevelsPanel = CreateRectChild(dailyLevelsPage.transform, "Panel_10_Levels", new Vector2(600f, 350f), new Vector2(0f, 10f));
            AddGridLayout(dailyLevelsPanel.gameObject, new Vector2(90f, 90f), new Vector2(18f, 18f), 5);
            Transform countdownRoot = CreateRectChild(dailyLevelsPage.transform, "RefreshCountdown", new Vector2(600f, 90f), new Vector2(0f, -390f));
            refreshCountdownText = CreateText(countdownRoot, "CountdownText", string.Empty, 28f, Color.white);
            CreateTopBackButton(dailyLevelsPage.transform, "BackToHome", ShowHome);
        }

        if (classicDifficultyPage == null)
        {
            classicDifficultyPage = CreateBasePage("ClassicDifficultyPage", new Color(0.125f, 0.6f, 1f, 1f));
            Transform group = CreateRectChild(classicDifficultyPage.transform, "DifficultyGroup", new Vector2(640f, 520f), new Vector2(0f, 40f));
            AddGridLayout(group.gameObject, new Vector2(210f, 80f), new Vector2(30f, 30f), 2);
            foreach (SudokuDifficulty difficulty in Enum.GetValues(typeof(SudokuDifficulty)))
            {
                CreateMenuButton(group, "Button_" + difficulty, difficulty.ToString(), new Vector2(210f, 80f));
            }
            CreateTopBackButton(classicDifficultyPage.transform, "BackToHome", ShowHome);
        }

        foreach (SudokuDifficulty difficulty in Enum.GetValues(typeof(SudokuDifficulty)))
        {
            if (!classicPages.ContainsKey(difficulty) || classicPages[difficulty] == null)
            {
                GameObject page = CreateBasePage(GetClassicLevelsPageName(difficulty), new Color(0.125f, 0.6f, 1f, 1f));
                Transform panel = CreateRectChild(page.transform, "Panel_Levels", new Vector2(620f, 620f), new Vector2(0f, 0f));
                AddGridLayout(panel.gameObject, new Vector2(90f, 90f), new Vector2(18f, 18f), 5);
                CreateTopBackButton(page.transform, "BackToDifficulty", ShowClassicDifficulty);
                classicPages[difficulty] = page;
            }
        }
    }

    private void RegisterPages()
    {
        menuPages.Clear();
        AddPage(objectHome);
        AddPage(dailyCardPage);
        AddPage(meCardPage);
        AddPage(dailyLevelsPage);
        AddPage(classicDifficultyPage);
        foreach (GameObject page in classicPages.Values)
        {
            AddPage(page);
        }
    }

    private void AddPage(GameObject page)
    {
        if (page != null && !menuPages.Contains(page))
        {
            menuPages.Add(page);
        }
    }

    private void BindHomeButtons()
    {
        BindButton(objectHome, "BtnDailyChallenge", ShowDailyLevels);
        BindButton(objectHome, "BtnClassicMode", ShowClassicDifficulty);
    }

    private void BindDailyPage()
    {
        if (dailyLevelsPage == null)
        {
            return;
        }

        if (dailyLevelsPanel == null)
        {
            Transform panel = FindDeepChild(dailyLevelsPage.transform, "Panel_10_Levels");
            dailyLevelsPanel = panel != null ? panel : dailyLevelsPage.transform;
        }

        AddGridLayout(dailyLevelsPanel.gameObject, new Vector2(90f, 90f), new Vector2(18f, 18f), 5);

        Transform countdownRoot = FindDeepChild(dailyLevelsPage.transform, "RefreshCountdown");
        if (countdownRoot != null)
        {
            refreshCountdownText = countdownRoot.GetComponentInChildren<TMP_Text>(true);
            if (refreshCountdownText == null)
            {
                refreshCountdownText = CreateText(countdownRoot, "CountdownText", string.Empty, 28f, Color.white);
            }
        }

        EnsureTopBackButton(dailyLevelsPage.transform, "BackToHome", ShowHome);
    }

    private void BindClassicDifficultyPage()
    {
        if (classicDifficultyPage != null)
        {
            EnsureTopBackButton(classicDifficultyPage.transform, "BackToHome", ShowHome);
        }

        foreach (SudokuDifficulty difficulty in Enum.GetValues(typeof(SudokuDifficulty)))
        {
            SudokuDifficulty capturedDifficulty = difficulty;
            BindButton(classicDifficultyPage, "Button_" + difficulty, () => ShowClassicLevels(capturedDifficulty));
        }
    }

    private void BindClassicLevelPages()
    {
        foreach (SudokuDifficulty difficulty in Enum.GetValues(typeof(SudokuDifficulty)))
        {
            if (!classicPages.TryGetValue(difficulty, out GameObject page) || page == null)
            {
                continue;
            }

            if (FindDeepChild(page.transform, "Panel_Levels") == null)
            {
                Transform panel = CreateRectChild(page.transform, "Panel_Levels", new Vector2(620f, 620f), Vector2.zero);
                AddGridLayout(panel.gameObject, new Vector2(90f, 90f), new Vector2(18f, 18f), 5);
            }
            else
            {
                Transform panel = FindDeepChild(page.transform, "Panel_Levels");
                AddGridLayout(panel.gameObject, new Vector2(90f, 90f), new Vector2(18f, 18f), 5);
            }

            EnsureTopBackButton(page.transform, "BackToDifficulty", ShowClassicDifficulty);
        }
    }

    private void BindWellDonePopup()
    {
        if (wellDonePopup == null)
        {
            return;
        }

        wellDoneAnimation = wellDonePopup.GetComponent<Animation>();
        wellDoneNextButton = GetOrCreateButton(FindDeepChild(wellDonePopup.transform, "Button_Next"));
        wellDoneQuitButton = GetOrCreateButton(FindDeepChild(wellDonePopup.transform, "Button_Quit"));

        if (wellDoneNextButton != null)
        {
            wellDoneNextButton.onClick.RemoveListener(HandleWellDoneNext);
            wellDoneNextButton.onClick.AddListener(HandleWellDoneNext);
        }

        if (wellDoneQuitButton != null)
        {
            wellDoneQuitButton.onClick.RemoveListener(HandleWellDoneQuit);
            wellDoneQuitButton.onClick.AddListener(HandleWellDoneQuit);
        }

        HideWellDonePopup();
    }

    private void BindResumeProgressPopup()
    {
        if (!EnsureResumeProgressPopupReference())
        {
            return;
        }

        resumeNewButton = GetOrCreateButton(FindDeepChild(resumeProgressPopup.transform, ButtonNewName));
        resumeContinueButton = GetOrCreateButton(FindDeepChild(resumeProgressPopup.transform, ButtonContinueName));

        if (resumeNewButton != null)
        {
            resumeNewButton.onClick.RemoveListener(HandleResumeNew);
            resumeNewButton.onClick.AddListener(HandleResumeNew);
        }

        if (resumeContinueButton != null)
        {
            resumeContinueButton.onClick.RemoveListener(HandleResumeContinue);
            resumeContinueButton.onClick.AddListener(HandleResumeContinue);
        }

        HideResumeProgressPopup();
    }

    private bool EnsureResumeProgressPopupReference()
    {
        if (resumeProgressPopup == null)
        {
            Transform popup = FindSceneTransformByName(ResumeProgressPopupName);
            resumeProgressPopup = popup != null ? popup.gameObject : null;
        }

        if (resumeProgressPopup == null && resumeProgressPopupPrefab != null)
        {
            resumeProgressPopup = EnsureScenePageInstance(resumeProgressPopupPrefab, ResumeProgressPopupName);
        }
        else if (resumeProgressPopup != null)
        {
            resumeProgressPopup = EnsureScenePageInstance(resumeProgressPopup, ResumeProgressPopupName);
        }

        return resumeProgressPopup != null;
    }

    private void ShowResumeProgressPopup()
    {
        if (!EnsureResumeProgressPopupReference())
        {
            Debug.LogWarning("ResumeProgressPopup is missing. Continuing saved sudoku progress instead of deleting it.", this);
            HandleResumeContinue();
            return;
        }

        if (resumeNewButton == null || resumeContinueButton == null)
        {
            BindResumeProgressPopup();
        }

        HideWellDonePopup();
        resumeProgressPopup.SetActive(true);
        resumeProgressPopup.transform.SetAsLastSibling();
    }

    private void HideResumeProgressPopup()
    {
        if (resumeProgressPopup != null)
        {
            resumeProgressPopup.SetActive(false);
        }

        Transform[] transforms = UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (Transform foundTransform in transforms)
        {
            if (foundTransform.name == ResumeProgressPopupName)
            {
                foundTransform.gameObject.SetActive(false);
            }
        }
    }

    private void HandleResumeNew()
    {
        if (pendingPuzzleStart == null)
        {
            HideResumeProgressPopup();
            return;
        }

        PendingPuzzleStart pending = pendingPuzzleStart;
        pendingPuzzleStart = null;
        Debug.Log("Starting a new sudoku level and deleting saved progress: " + pending.SaveKey, this);
        HideResumeProgressPopup();
        DeleteLevelProgress(pending.SaveKey);
        StartPuzzleInternal(
            pending.Mode,
            pending.Difficulty,
            pending.LevelIndex,
            pending.LevelCount,
            pending.ProgressKey,
            pending.PuzzleSet,
            pending.PuzzleData,
            pending.SaveKey,
            null);
    }

    private void HandleResumeContinue()
    {
        if (pendingPuzzleStart == null)
        {
            HideResumeProgressPopup();
            return;
        }

        PendingPuzzleStart pending = pendingPuzzleStart;
        pendingPuzzleStart = null;
        Debug.Log("Continuing saved sudoku progress: " + pending.SaveKey, this);
        HideResumeProgressPopup();
        StartPuzzleInternal(
            pending.Mode,
            pending.Difficulty,
            pending.LevelIndex,
            pending.LevelCount,
            pending.ProgressKey,
            pending.PuzzleSet,
            pending.PuzzleData,
            pending.SaveKey,
            pending.SavedProgress);
    }

    private void ShowWellDonePopup()
    {
        if (wellDonePopup == null)
        {
            ReturnToActiveLevelSelectPage();
            return;
        }

        wellDonePopup.SetActive(true);
        wellDonePopup.transform.SetAsLastSibling();

        if (wellDoneAnimation == null)
        {
            wellDoneAnimation = wellDonePopup.GetComponent<Animation>();
        }

        if (wellDoneAnimation != null && wellDoneAnimation.GetClip("WellDone_open") != null)
        {
            wellDoneAnimation.Stop();
            wellDoneAnimation.Play("WellDone_open");
        }
    }

    private void HideWellDonePopup()
    {
        if (wellDonePopup != null)
        {
            wellDonePopup.SetActive(false);
        }
    }

    private void HandleWellDoneNext()
    {
        HideWellDonePopup();

        int nextLevelIndex = activeLevelIndex + 1;
        if (activePuzzleSet == null ||
            activePuzzleSet.Puzzles == null ||
            nextLevelIndex >= activeLevelCount ||
            nextLevelIndex >= activePuzzleSet.Puzzles.Length)
        {
            ReturnToActiveLevelSelectPage();
            return;
        }

        StartPuzzle(activeGameMode, activeDifficulty, nextLevelIndex, activeLevelCount, activeProgressKey, activePuzzleSet, activePuzzleSet.Puzzles[nextLevelIndex]);
    }

    private void HandleWellDoneQuit()
    {
        HideWellDonePopup();
        ReturnToActiveLevelSelectPage();
    }

    private void EnsureDailyLevels()
    {
        string key = GetDailyKey(DateTime.Now);
        if (dailyPuzzleSet == null || dailyPuzzleSet.Key != key)
        {
            dailyPuzzleSet = LoadOrCreatePuzzleSet(key, DailyLevelCount, CreateDailyHoles, "Daily");
        }

        int unlockedCount = GetUnlockedLevelCount(GetDailyProgressKey(), dailyPuzzleSet.Puzzles.Length);
        RebuildLevelButtons(dailyLevelsPanel, dailyPuzzleSet.Puzzles.Length, index => StartDailyLevel(index), index => index < unlockedCount);
    }

    private void EnsureClassicLevels(SudokuDifficulty difficulty)
    {
        string key = "Classic_" + difficulty;
        if (!classicPuzzleSets.TryGetValue(difficulty, out SudokuPuzzleSet set) || set == null)
        {
            set = LoadOrCreatePuzzleSet(key, ClassicLevelCount, index => CreateClassicHoles(difficulty), "Classic_" + difficulty);
            classicPuzzleSets[difficulty] = set;
        }

        Transform panel = FindDeepChild(classicPages[difficulty].transform, "Panel_Levels");
        int unlockedCount = GetUnlockedLevelCount(GetClassicProgressKey(difficulty), set.Puzzles.Length);
        RebuildLevelButtons(panel, set.Puzzles.Length, index => StartClassicLevel(difficulty, index), index => index < unlockedCount);
    }

    private SudokuPuzzleSet LoadOrCreatePuzzleSet(string key, int count, Func<int, int> holesProvider, string storagePrefix)
    {
        string storageKey = "Sudoku_" + storagePrefix + "_" + key;
        string json = PlayerPrefs.GetString(storageKey, string.Empty);
        if (!string.IsNullOrEmpty(json))
        {
            SudokuPuzzleSet loaded = JsonUtility.FromJson<SudokuPuzzleSet>(json);
            if (loaded != null && loaded.Puzzles != null && loaded.Puzzles.Length == count)
            {
                if (string.IsNullOrEmpty(loaded.Key))
                {
                    loaded.Key = key;
                    PlayerPrefs.SetString(storageKey, JsonUtility.ToJson(loaded));
                    PlayerPrefs.Save();
                }

                return loaded;
            }
        }

        SudokuPuzzleSet created = new SudokuPuzzleSet
        {
            Key = key,
            Puzzles = new SudokuPuzzleData[count]
        };

        int seedBase = StableHash(storagePrefix + "_" + key);
        for (int index = 0; index < count; index++)
        {
            created.Puzzles[index] = SudokuPuzzleGenerator.Generate(holesProvider(index), seedBase + index * 9973);
        }

        PlayerPrefs.SetString(storageKey, JsonUtility.ToJson(created));
        PlayerPrefs.Save();
        return created;
    }

    private void SaveActivePuzzleProgressIfNeeded()
    {
        if (gameController == null ||
            activePuzzleData == null ||
            string.IsNullOrEmpty(activeLevelSaveKey) ||
            !gameController.TryCaptureProgress(activePuzzleData, out SudokuLevelProgressData progressData))
        {
            return;
        }

        PlayerPrefs.SetString(activeLevelSaveKey, JsonUtility.ToJson(progressData));
        PlayerPrefs.Save();
        Debug.Log("Saved sudoku level progress: " + activeLevelSaveKey, this);
    }

    private static bool TryLoadLevelProgress(string saveKey, out SudokuLevelProgressData progressData)
    {
        progressData = null;

        if (string.IsNullOrEmpty(saveKey))
        {
            return false;
        }

        string json = PlayerPrefs.GetString(saveKey, string.Empty);
        if (string.IsNullOrEmpty(json))
        {
            return false;
        }

        progressData = JsonUtility.FromJson<SudokuLevelProgressData>(json);
        bool isValid = progressData != null &&
                       progressData.Values != null &&
                       progressData.Values.Length == SudokuPuzzleData.BoardLength * SudokuPuzzleData.BoardLength;

        if (isValid)
        {
            Debug.Log("Loaded sudoku level progress: " + saveKey);
        }
        else
        {
            Debug.LogWarning("Ignored invalid sudoku level progress: " + saveKey);
        }

        return isValid;
    }

    private static void DeleteLevelProgress(string saveKey)
    {
        if (string.IsNullOrEmpty(saveKey) || !PlayerPrefs.HasKey(saveKey))
        {
            return;
        }

        PlayerPrefs.DeleteKey(saveKey);
        PlayerPrefs.Save();
    }

    private static string GetLevelProgressKey(SudokuGameMode mode, SudokuDifficulty difficulty, SudokuPuzzleSet puzzleSet, int levelIndex)
    {
        string setKey = puzzleSet != null ? puzzleSet.Key : string.Empty;
        return string.Format("{0}{1}_{2}_{3}_{4}", LevelProgressPrefix, mode, difficulty, setKey, levelIndex);
    }

    private static string GetLegacyLevelProgressKey(SudokuGameMode mode, SudokuDifficulty difficulty, int levelIndex)
    {
        return string.Format("{0}{1}_{2}_{3}_{4}", LevelProgressPrefix, mode, difficulty, string.Empty, levelIndex);
    }

    private static PendingPuzzleStart CreatePendingPuzzleStart(
        SudokuGameMode mode,
        SudokuDifficulty difficulty,
        int levelIndex,
        int levelCount,
        string progressKey,
        SudokuPuzzleSet puzzleSet,
        SudokuPuzzleData puzzleData,
        string saveKey,
        SudokuLevelProgressData savedProgress)
    {
        return new PendingPuzzleStart
        {
            Mode = mode,
            Difficulty = difficulty,
            LevelIndex = levelIndex,
            LevelCount = levelCount,
            ProgressKey = progressKey,
            PuzzleSet = puzzleSet,
            PuzzleData = puzzleData,
            SaveKey = saveKey,
            SavedProgress = savedProgress
        };
    }

    private int CreateDailyHoles(int index)
    {
        float t = DailyLevelCount <= 1 ? 0f : index / (float)(DailyLevelCount - 1);
        int baseHoles = Mathf.RoundToInt(Mathf.Lerp(32f, 56f, t));
        int jitter = (StableHash(GetDailyKey(DateTime.Now) + "_" + index) & int.MaxValue) % 3;
        return Mathf.Clamp(baseHoles + jitter - 1, 30, 58);
    }

    private static int CreateClassicHoles(SudokuDifficulty difficulty)
    {
        switch (difficulty)
        {
            case SudokuDifficulty.Easy:
                return 34;
            case SudokuDifficulty.Medium:
                return 40;
            case SudokuDifficulty.Hard:
                return 46;
            case SudokuDifficulty.Expert:
                return 51;
            case SudokuDifficulty.Master:
                return 55;
            case SudokuDifficulty.Extreme:
                return 59;
            default:
                return 45;
        }
    }

    private void RebuildLevelButtons(Transform panel, int count, Action<int> onClicked, Func<int, bool> isUnlocked)
    {
        if (panel == null)
        {
            return;
        }

        for (int index = panel.childCount - 1; index >= 0; index--)
        {
            Transform child = panel.GetChild(index);
            if (child.name.StartsWith(RuntimeGeneratedPrefix))
            {
                Destroy(child.gameObject);
            }
        }

        for (int index = 0; index < count; index++)
        {
            int capturedIndex = index;
            GameObject buttonObject = CreateLevelButton(panel, index + 1);
            Button button = buttonObject.GetComponent<Button>();
            button.onClick.RemoveAllListeners();
            bool unlocked = isUnlocked == null || isUnlocked(capturedIndex);
            button.interactable = unlocked;
            SetLevelButtonLocked(buttonObject, !unlocked);
            if (unlocked)
            {
                button.onClick.AddListener(() => onClicked(capturedIndex));
            }
        }
    }

    private GameObject CreateLevelButton(Transform parent, int levelNumber)
    {
        GameObject buttonObject = btnLevelPrefab != null
            ? Instantiate(btnLevelPrefab, parent)
            : CreateMenuButton(parent, RuntimeGeneratedPrefix + "BtnLevel_" + levelNumber, levelNumber.ToString(), new Vector2(80f, 80f));

        buttonObject.name = RuntimeGeneratedPrefix + "BtnLevel_" + levelNumber;

        TMP_Text label = buttonObject.GetComponentInChildren<TMP_Text>(true);
        if (label != null)
        {
            label.text = levelNumber.ToString();
        }

        return buttonObject;
    }

    private static void SetLevelButtonLocked(GameObject buttonObject, bool locked)
    {
        if (buttonObject == null)
        {
            return;
        }

        CanvasGroup canvasGroup = buttonObject.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            canvasGroup = buttonObject.AddComponent<CanvasGroup>();
        }

        canvasGroup.alpha = locked ? 0.45f : 1f;
    }

    private static int GetUnlockedLevelCount(string progressKey, int levelCount)
    {
        return Mathf.Clamp(PlayerPrefs.GetInt(progressKey, 1), 1, levelCount);
    }

    private static void UnlockNextLevel(string progressKey, int completedLevelIndex, int levelCount)
    {
        int currentUnlocked = GetUnlockedLevelCount(progressKey, levelCount);
        int targetUnlocked = Mathf.Clamp(completedLevelIndex + 2, 1, levelCount);
        if (targetUnlocked <= currentUnlocked)
        {
            return;
        }

        PlayerPrefs.SetInt(progressKey, targetUnlocked);
        PlayerPrefs.Save();
    }

    private string GetDailyProgressKey()
    {
        return "Sudoku_Daily_Unlocked_" + GetDailyKey(DateTime.Now);
    }

    private static string GetClassicProgressKey(SudokuDifficulty difficulty)
    {
        return "Sudoku_Classic_Unlocked_" + difficulty;
    }

    private void StartRefreshCountdown()
    {
        if (countdownRoutine != null)
        {
            StopCoroutine(countdownRoutine);
        }

        countdownRoutine = StartCoroutine(RefreshCountdownRoutine());
    }

    private IEnumerator RefreshCountdownRoutine()
    {
        while (dailyLevelsPage != null && dailyLevelsPage.activeInHierarchy)
        {
            if (dailyPuzzleSet == null || dailyPuzzleSet.Key != GetDailyKey(DateTime.Now))
            {
                EnsureDailyLevels();
            }

            UpdateRefreshCountdownText();
            yield return new WaitForSeconds(1f);
        }

        countdownRoutine = null;
    }

    private void UpdateRefreshCountdownText()
    {
        if (refreshCountdownText == null)
        {
            return;
        }

        TimeSpan remaining = GetNextDailyRefreshTime(DateTime.Now) - DateTime.Now;
        if (remaining < TimeSpan.Zero)
        {
            remaining = TimeSpan.Zero;
        }

        refreshCountdownText.text = string.Format("{0:00}:{1:00}:{2:00}", (int)remaining.TotalHours, remaining.Minutes, remaining.Seconds);
    }

    private void ShowOnly(GameObject page)
    {
        HideAllMenuPages();
        if (page != null)
        {
            page.SetActive(true);
        }
    }

    private void HideAllMenuPages()
    {
        foreach (GameObject page in menuPages)
        {
            if (page != null)
            {
                page.SetActive(false);
            }
        }
    }

    private void HideGameplay()
    {
        if (gameController != null)
        {
            gameController.HideGameplayForMenu();
        }
    }

    private void ShowBottomNavForCurrentPage(bool visible)
    {
        shouldShowBottomNav = visible;
        SetBottomNavVisible(visible);
    }

    private void SetBottomNavVisible(bool visible)
    {
        if (bottomNav == null)
        {
            Transform nav = FindSceneTransformByName(BottomNavName);
            bottomNav = nav != null ? nav.gameObject : null;
        }

        if (bottomNav == null)
        {
            return;
        }

        if (bottomNav.activeSelf != visible)
        {
            bottomNav.SetActive(visible);
        }

        if (visible)
        {
            bottomNav.transform.SetAsLastSibling();
        }
    }

    private GameObject CreateBasePage(string pageName, Color color)
    {
        GameObject page = new GameObject(pageName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        page.transform.SetParent(canvas.transform, false);
        RectTransform rect = page.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(750f, 1334f);
        rect.anchoredPosition = Vector2.zero;
        Image image = page.GetComponent<Image>();
        image.color = color;
        return page;
    }

    private static Transform CreateRectChild(Transform parent, string childName, Vector2 size, Vector2 position)
    {
        GameObject child = new GameObject(childName, typeof(RectTransform));
        child.transform.SetParent(parent, false);
        RectTransform rect = child.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
        return child.transform;
    }

    private static TMP_Text CreateText(Transform parent, string textName, string value, float size, Color color)
    {
        GameObject textObject = new GameObject(textName, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(parent, false);
        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        TMP_Text text = textObject.GetComponent<TMP_Text>();
        text.text = value;
        text.fontSize = size;
        text.color = color;
        text.alignment = TextAlignmentOptions.Center;
        text.raycastTarget = false;
        return text;
    }

    private GameObject CreateMenuButton(Transform parent, string buttonName, string label, Vector2 size)
    {
        GameObject buttonObject = new GameObject(buttonName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(parent, false);
        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.sizeDelta = size;
        Image image = buttonObject.GetComponent<Image>();
        image.color = Color.white;
        Button button = buttonObject.GetComponent<Button>();
        button.targetGraphic = image;
        CreateText(buttonObject.transform, "Label", label, 24f, new Color(0.196f, 0.196f, 0.196f, 1f));
        return buttonObject;
    }

    private void CreateTopBackButton(Transform parent, string buttonName, UnityEngine.Events.UnityAction onClick)
    {
        GameObject buttonObject = CreateMenuButton(parent, buttonName, "Back", new Vector2(130f, 64f));
        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(32f, -32f);
        Button button = buttonObject.GetComponent<Button>();
        button.onClick.AddListener(onClick);
    }

    private void EnsureTopBackButton(Transform parent, string buttonName, UnityEngine.Events.UnityAction onClick)
    {
        if (parent == null || FindDeepChild(parent, buttonName) != null)
        {
            return;
        }

        CreateTopBackButton(parent, buttonName, onClick);
    }

    private static void AddGridLayout(GameObject target, Vector2 cellSize, Vector2 spacing, int constraintCount)
    {
        GridLayoutGroup grid = target.GetComponent<GridLayoutGroup>();
        if (grid != null)
        {
            return;
        }

        grid = target.AddComponent<GridLayoutGroup>();
        grid.cellSize = cellSize;
        grid.spacing = spacing;
        grid.childAlignment = TextAnchor.MiddleCenter;
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = constraintCount;
    }

    private void BindButton(GameObject root, string buttonName, UnityEngine.Events.UnityAction action)
    {
        if (root == null)
        {
            return;
        }

        Transform target = FindDeepChild(root.transform, buttonName);
        if (target == null)
        {
            return;
        }

        Button button = target.GetComponent<Button>();
        if (button == null)
        {
            button = target.gameObject.AddComponent<Button>();
        }

        if (button.targetGraphic == null)
        {
            button.targetGraphic = target.GetComponent<Graphic>();
        }

        button.onClick.AddListener(action);
    }

    private static Button GetOrCreateButton(Transform target)
    {
        if (target == null)
        {
            return null;
        }

        Button button = target.GetComponent<Button>();
        if (button == null)
        {
            button = target.gameObject.AddComponent<Button>();
        }

        if (button.targetGraphic == null)
        {
            button.targetGraphic = target.GetComponent<Graphic>();
        }

        return button;
    }

    private static string GetDailyKey(DateTime now)
    {
        DateTime refreshTime = now.Date.AddHours(DailyRefreshHour);
        DateTime activeDay = now >= refreshTime ? now.Date : now.Date.AddDays(-1);
        return activeDay.ToString("yyyyMMdd");
    }

    private static DateTime GetNextDailyRefreshTime(DateTime now)
    {
        DateTime todayRefresh = now.Date.AddHours(DailyRefreshHour);
        return now < todayRefresh ? todayRefresh : todayRefresh.AddDays(1);
    }

    private static string GetClassicLevelsPageName(SudokuDifficulty difficulty)
    {
        return "Levels_" + difficulty;
    }

    private static bool TryParseDifficultyFromPageName(string pageName, out SudokuDifficulty difficulty)
    {
        difficulty = SudokuDifficulty.Easy;
        if (string.IsNullOrEmpty(pageName) || !pageName.StartsWith("Levels_", StringComparison.Ordinal))
        {
            return false;
        }

        string difficultyName = pageName.Substring("Levels_".Length);
        return Enum.TryParse(difficultyName, out difficulty);
    }

    private static int StableHash(string value)
    {
        unchecked
        {
            int hash = (int)2166136261;
            for (int index = 0; index < value.Length; index++)
            {
                hash ^= value[index];
                hash *= 16777619;
            }

            return hash;
        }
    }

    private static T FindFirstObjectByName<T>(string objectName) where T : Component
    {
        T[] objects = UnityEngine.Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (T obj in objects)
        {
            if (string.IsNullOrEmpty(objectName) || obj.name == objectName)
            {
                return obj;
            }
        }

        return null;
    }

    private static Transform FindSceneTransformByName(string objectName)
    {
        Transform[] transforms = UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (Transform foundTransform in transforms)
        {
            if (foundTransform.name == objectName)
            {
                return foundTransform;
            }
        }

        return null;
    }

    private static Transform FindDeepChild(Transform parent, string childName)
    {
        if (parent == null)
        {
            return null;
        }

        for (int index = 0; index < parent.childCount; index++)
        {
            Transform child = parent.GetChild(index);
            if (child.name == childName)
            {
                return child;
            }

            Transform result = FindDeepChild(child, childName);
            if (result != null)
            {
                return result;
            }
        }

        return null;
    }
}
