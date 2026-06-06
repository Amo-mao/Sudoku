using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Spine.Unity;

public enum MathChallengeQuitPenalty
{
    None,
    LockInput,
    ShowAd,
    ShowAdThenLock
}

public enum SudukuGameState
{
    Home,
    Playing,
    MathChallenge,
    Paused
}

[DisallowMultipleComponent]
[AddComponentMenu("UI/Suduku Game Controller")]
public sealed class SudukuGameController : MonoBehaviour
{
    private const int BoardLength = 9;
    private const int BoxLength = 3;
    private const int EmptyValue = 0;
    private const int ShineSweepFrameStagger = 15;
    private const string ReturnButtonName = "ButtonReturn";
    private const string EraseButtonName = "ButtonErase";
    private const string NotesButtonName = "ButtonNotes";
    private const string HintButtonName = "ButtonHint";
    private const string HintCountTextName = "HintCount";
    private const string HintAdBadgeName = "AdBadge";
    private const string HintAdBadgeWiggleAnimationName = "AdBadge_wiggle";
    private const string CellShineSweepAnimationName = "Cell_ShineSweep";
    private const string EraseIdleAnimationName = "idle";
    private const string EraseClickAnimationName = "Click";
    private const string HintIdleAnimationName = "idle";
    private const string HintClickAnimationName = "Click";
    private const string HintWiggleAnimationName = "wiggle";
    private const string ReturnIdleAnimationName = "Return_idle";
    private const string ReturnClickAnimationName = "Return_Click";
    private const string ObjectHomeName = "ObjectHome";
    private const string ButtonPlayName = "ButtonPlay";
    private const string IconBackName = "IconBack";

    [SerializeField] private SudukuBoardLayout boardLayout;
    [SerializeField, Range(20, 64)] private int holesToDig = 45;
    [SerializeField] private bool generateOnStart = true;

    [Header("Game Flow")]
    [SerializeField] private bool useAppController = true;
    [SerializeField] private bool startAtHome = true;
    [SerializeField] private GameObject objectHome;
    [SerializeField] private Button playButton;
    [SerializeField] private Button backButton;
    [SerializeField] private GameObject[] gameplayRootObjects;

    [Header("Number Colors")]
    [SerializeField] private Color wrongNumberColor = new Color(0.85f, 0.12f, 0.12f, 1f);

    [Header("Note Mode")]
    [SerializeField] private Button noteModeButton;
    [SerializeField] private SkeletonGraphic noteModeSkeletonGraphic;
    [SerializeField] private SkeletonAnimation noteModeSkeletonAnimation;
    [SerializeField] private string noteModeIdleAnimation = "idle";
    [SerializeField] private string noteModeActiveAnimation = "activate";

    [Header("Hints")]
    [SerializeField, Min(0)] private int initialHintCount = 3;
    [SerializeField] private TMP_Text hintCountText;
    [SerializeField] private string hintCountFormat = "{0}";
    [SerializeField] private GameObject hintAdBadge;
    [SerializeField] private UnityEngine.Animation hintAdBadgeAnimation;

    [Header("Math Challenge")]
    [SerializeField] private MathChallengePopup mathChallengePopup;
    [SerializeField] private GameObject mathChallengePrefab;
    [SerializeField] private Transform mathChallengeParent;
    [SerializeField, Min(1)] private int wrongPlacementsBeforeMathChallenge = 2;
    [SerializeField] private MathChallengeQuitPenalty quitPenalty = MathChallengeQuitPenalty.LockInput;
    [SerializeField, Min(0f)] private float quitLockSeconds = 10f;

    [Header("Cell Highlight Colors")]
    [SerializeField] private Color normalCellColor = Color.white;
    [SerializeField] private Color relatedCellColor = new Color(0.9f, 0.95f, 1f, 1f);
    [SerializeField] private Color sameNumberCellColor = new Color(1f, 0.93f, 0.72f, 1f);
    [SerializeField] private Color selectedCellColor = new Color(0.62f, 0.82f, 1f, 1f);

    private readonly SudukuCell[,] cells = new SudukuCell[BoardLength, BoardLength];
    private readonly int[,] solution = new int[BoardLength, BoardLength];
    private readonly int[,] puzzle = new int[BoardLength, BoardLength];
    private readonly Stack<MoveRecord> history = new Stack<MoveRecord>();
    private readonly bool[] completedRows = new bool[BoardLength];
    private readonly bool[] completedColumns = new bool[BoardLength];
    private readonly bool[] completedBoxes = new bool[BoardLength];
    private readonly List<GameObject> autoGameplayRootObjects = new List<GameObject>();

    private SudukuCell selectedCell;
    private SudukuCell pendingMathChallengeCell;
    private Button hintButton;
    private AnimatedUIButton hintAnimatedButton;
    private int selectedNumber;
    private int consecutiveWrongPlacements;
    private int remainingHintCount;
    private bool isMathChallengeActive;
    private bool isNoteMode;
    private bool hasGeneratedPuzzle;
    private Coroutine shineSweepRoutine;
    private SudukuGameState currentState = SudukuGameState.Home;
    private SudukuGameState stateBeforeMathChallenge = SudukuGameState.Playing;
    private Action gameBackHandler;
    private Action puzzleCompletedHandler;
    private bool hasCompletedCurrentPuzzle;
    private bool completePuzzleAfterShineSweep;

    private struct MoveRecord
    {
        public SudukuCell Cell;
        public int PreviousValue;
        public int NewValue;
        public bool PreviousWasHint;
        public bool NewWasHint;
    }

    private void Awake()
    {
        remainingHintCount = initialHintCount;

        if (boardLayout == null)
        {
            boardLayout = GetComponent<SudukuBoardLayout>();
        }

        PrepareCells(true);
        PrepareMathChallengePopup();
        BindCellButtons();
        BindControlButtons();
        PrepareGameFlow();
        RefreshHintCountDisplay();
    }

    private void OnValidate()
    {
        if (Application.isPlaying)
        {
            RefreshBoardState();
        }
    }

    private void Start()
    {
        if (useAppController)
        {
            if (FindObjectsByType<SudokuAppController>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length == 0)
            {
                gameObject.AddComponent<SudokuAppController>();
            }

            return;
        }

        if (startAtHome && objectHome != null)
        {
            EnterHome(false);
            return;
        }

        EnterPlaying(generateOnStart);
    }

    public void EnterHome()
    {
        EnterHome(true);
    }

    public void EnterPlaying()
    {
        EnterPlaying(!hasGeneratedPuzzle && generateOnStart);
    }

    public void EnterPlaying(bool generatePuzzle)
    {
        CloseMathChallengeIfNeeded();
        currentState = SudukuGameState.Playing;
        isMathChallengeActive = false;

        SetHomeVisible(false);
        SetGameplayVisible(true);

        if (generatePuzzle)
        {
            GenerateNewPuzzle();
        }

        RefreshBoardState();
    }

    public void LoadPuzzle(SudokuPuzzleData puzzleData)
    {
        if (puzzleData == null)
        {
            Debug.LogWarning("Cannot load a null sudoku puzzle.", this);
            return;
        }

        CloseMathChallengeIfNeeded();
        currentState = SudukuGameState.Playing;
        isMathChallengeActive = false;

        SetHomeVisible(false);
        SetGameplayVisible(true);
        PrepareCells(false);

        for (int row = 0; row < BoardLength; row++)
        {
            for (int column = 0; column < BoardLength; column++)
            {
                puzzle[row, column] = puzzleData.GetPuzzleValue(row, column);
                solution[row, column] = puzzleData.GetSolutionValue(row, column);
            }
        }

        ApplyPuzzle();
        RefreshCompletedUnitCache();
        history.Clear();
        remainingHintCount = initialHintCount;
        RefreshHintCountDisplay();
        selectedNumber = EmptyValue;
        consecutiveWrongPlacements = 0;
        pendingMathChallengeCell = null;
        ClearPenaltyLocks();
        SetNoteMode(false);
        SelectCell(null);
        hasGeneratedPuzzle = true;
        hasCompletedCurrentPuzzle = false;
        completePuzzleAfterShineSweep = false;
        RefreshBoardState();
    }

    public void SetGameBackHandler(Action handler)
    {
        gameBackHandler = handler;
    }

    public void SetPuzzleCompletedHandler(Action handler)
    {
        puzzleCompletedHandler = handler;
    }

    public void HideGameplayForMenu()
    {
        CloseMathChallengeIfNeeded();
        currentState = SudukuGameState.Home;
        isMathChallengeActive = false;
        selectedNumber = EmptyValue;
        SetNoteMode(false);
        SelectCell(null);
        SetGameplayVisible(false);
    }

    private void EnterHome(bool closeMathChallenge)
    {
        if (closeMathChallenge)
        {
            CloseMathChallengeIfNeeded();
        }

        currentState = SudukuGameState.Home;
        isMathChallengeActive = false;
        selectedNumber = EmptyValue;
        SetNoteMode(false);
        SelectCell(null);

        SetHomeVisible(true);
        SetGameplayVisible(false);
    }

    private bool IsPlayingState()
    {
        return currentState == SudukuGameState.Playing;
    }

    [ContextMenu("Generate New Puzzle")]
    public void GenerateNewPuzzle()
    {
        PrepareCells(false);
        ClearBoard(solution);
        FillBoard(solution);
        CopyBoard(solution, puzzle);
        DigHolesWithUniqueSolution(puzzle, holesToDig);
        ApplyPuzzle();
        RefreshCompletedUnitCache();
        history.Clear();
        remainingHintCount = initialHintCount;
        RefreshHintCountDisplay();
        selectedNumber = EmptyValue;
        consecutiveWrongPlacements = 0;
        isMathChallengeActive = false;
        pendingMathChallengeCell = null;
        completePuzzleAfterShineSweep = false;
        ClearPenaltyLocks();
        SetNoteMode(false);
        SelectCell(null);
        hasGeneratedPuzzle = true;
        hasCompletedCurrentPuzzle = false;
    }

    public void InputNumber(int value)
    {
        if (!IsPlayingState())
        {
            return;
        }

        if (isMathChallengeActive)
        {
            return;
        }

        value = Mathf.Clamp(value, EmptyValue, BoardLength);
        selectedNumber = value;

        if (selectedCell != null && selectedCell.IsPenaltyLocked)
        {
            selectedCell.PlayPenaltyLockWiggle();
            RefreshBoardState();
            return;
        }

        if (isNoteMode)
        {
            InputNote(value);
            return;
        }

        if (selectedCell == null || selectedCell.IsFixed)
        {
            RefreshBoardState();
            return;
        }

        if (selectedCell.Value == value)
        {
            RefreshBoardState();
            return;
        }

        history.Push(new MoveRecord
        {
            Cell = selectedCell,
            PreviousValue = selectedCell.Value,
            NewValue = value,
            PreviousWasHint = selectedCell.IsHint,
            NewWasHint = false
        });

        SudukuCell changedCell = selectedCell;
        bool isWrongPlacement = IsWrongValue(selectedCell, value);
        selectedCell.SetValue(value, false, isWrongPlacement);
        RefreshBoardState();

        UpdateWrongPlacementStreak(value, isWrongPlacement, changedCell);
        if (isMathChallengeActive)
        {
            return;
        }

        TriggerNewlyCompletedUnits(changedCell);
        CheckPuzzleCompleted();
    }

    public void UndoLastMove()
    {
        if (!IsPlayingState())
        {
            return;
        }

        if (isMathChallengeActive)
        {
            return;
        }

        while (history.Count > 0)
        {
            MoveRecord move = history.Pop();
            if (move.Cell == null ||
                move.Cell.IsFixed ||
                move.Cell.Value != move.NewValue ||
                move.Cell.IsHint != move.NewWasHint)
            {
                continue;
            }

            if (move.Cell.IsPenaltyLocked)
            {
                move.Cell.PlayPenaltyLockWiggle();
                continue;
            }

            move.Cell.SetValue(move.PreviousValue, false, IsWrongValue(move.Cell, move.PreviousValue), move.PreviousWasHint);
            RefreshCompletedUnitCache();
            SelectCell(move.Cell);
            return;
        }
    }

    public void ToggleNoteMode()
    {
        if (!IsPlayingState())
        {
            return;
        }

        if (isMathChallengeActive)
        {
            return;
        }

        SetNoteMode(!isNoteMode);
    }

    private void PrepareMathChallengePopup()
    {
        if (mathChallengePopup == null)
        {
            MathChallengePopup[] foundPopups = FindObjectsByType<MathChallengePopup>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (foundPopups.Length > 0)
            {
                mathChallengePopup = foundPopups[0];
            }
        }

        if (mathChallengePopup == null)
        {
            Transform mathTransform = FindSceneTransformByName("Object_Math");
            if (mathTransform != null)
            {
                GameObject mathObject = mathTransform.gameObject;
                mathChallengePopup = mathObject.GetComponent<MathChallengePopup>();
                if (mathChallengePopup == null)
                {
                    mathChallengePopup = mathObject.AddComponent<MathChallengePopup>();
                }
            }
        }

        if (mathChallengePopup == null && mathChallengePrefab != null)
        {
            Transform parent = mathChallengeParent != null ? mathChallengeParent : transform.root;
            GameObject popupObject = Instantiate(mathChallengePrefab, parent);
            mathChallengePopup = popupObject.GetComponent<MathChallengePopup>();
            if (mathChallengePopup == null)
            {
                mathChallengePopup = popupObject.AddComponent<MathChallengePopup>();
            }
        }

        if (mathChallengePopup != null)
        {
            mathChallengePopup.Initialize(true);
        }
    }

    private static Transform FindSceneTransformByName(string objectName)
    {
        Transform[] transforms = FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (Transform foundTransform in transforms)
        {
            if (foundTransform.name == objectName)
            {
                return foundTransform;
            }
        }

        return null;
    }

    private void UpdateWrongPlacementStreak(int value, bool isWrongPlacement, SudukuCell changedCell)
    {
        if (value == EmptyValue)
        {
            return;
        }

        if (!isWrongPlacement)
        {
            consecutiveWrongPlacements = 0;
            return;
        }

        consecutiveWrongPlacements++;
        if (consecutiveWrongPlacements < wrongPlacementsBeforeMathChallenge)
        {
            return;
        }

        pendingMathChallengeCell = changedCell;
        OpenMathChallenge();
    }

    private void OpenMathChallenge()
    {
        if (mathChallengePopup == null)
        {
            PrepareMathChallengePopup();
        }

        if (mathChallengePopup == null)
        {
            Debug.LogWarning("SudukuGameController needs Object_Math or a Math Challenge Prefab to show the math challenge.", this);
            consecutiveWrongPlacements = 0;
            return;
        }

        isMathChallengeActive = true;
        stateBeforeMathChallenge = currentState;
        currentState = SudukuGameState.MathChallenge;
        consecutiveWrongPlacements = 0;
        mathChallengePopup.Show(HandleMathChallengeClosed);
    }

    private void HandleMathChallengeClosed(MathChallengeExitReason reason)
    {
        isMathChallengeActive = false;
        currentState = stateBeforeMathChallenge == SudukuGameState.MathChallenge ? SudukuGameState.Playing : stateBeforeMathChallenge;
        RefreshBoardState();

        if (reason == MathChallengeExitReason.Quit)
        {
            ApplyMathChallengeQuitPenalty();
        }

        pendingMathChallengeCell = null;
    }

    private void ApplyMathChallengeQuitPenalty()
    {
        switch (quitPenalty)
        {
            case MathChallengeQuitPenalty.LockInput:
                StartPenaltyCellLock(quitLockSeconds);
                break;
            case MathChallengeQuitPenalty.ShowAd:
                RequestMathChallengeQuitAd();
                break;
            case MathChallengeQuitPenalty.ShowAdThenLock:
                RequestMathChallengeQuitAd();
                StartPenaltyCellLock(quitLockSeconds);
                break;
        }
    }

    private void RequestMathChallengeQuitAd()
    {
        Debug.Log("Math challenge quit requested an ad. Connect this to the ad service when it is ready.", this);
    }

    private void StartPenaltyCellLock(float seconds)
    {
        if (seconds <= 0f || pendingMathChallengeCell == null)
        {
            return;
        }

        pendingMathChallengeCell.StartPenaltyLock(seconds);
    }

    private void InputNote(int value)
    {
        if (value == EmptyValue)
        {
            RefreshBoardState();
            return;
        }

        if (selectedCell == null || selectedCell.IsFixed || selectedCell.IsPenaltyLocked || selectedCell.Value != EmptyValue)
        {
            if (selectedCell != null && selectedCell.IsPenaltyLocked)
            {
                selectedCell.PlayPenaltyLockWiggle();
            }

            RefreshBoardState();
            return;
        }

        selectedCell.ToggleNote(value);
        RefreshBoardState();
    }

    private void SetNoteMode(bool enabled)
    {
        isNoteMode = enabled;
        RefreshNoteModeAnimation();
    }

    private void RefreshNoteModeAnimation()
    {
        CacheNoteModeSpine();
        string animationName = isNoteMode ? noteModeActiveAnimation : noteModeIdleAnimation;
        PlayNoteModeAnimation(animationName);
    }

    private void CacheNoteModeSpine()
    {
        if (noteModeButton == null)
        {
            return;
        }

        if (noteModeSkeletonGraphic == null)
        {
            noteModeSkeletonGraphic = noteModeButton.GetComponentInChildren<SkeletonGraphic>(true);
        }

        if (noteModeSkeletonAnimation == null)
        {
            noteModeSkeletonAnimation = noteModeButton.GetComponentInChildren<SkeletonAnimation>(true);
        }
    }

    private void PlayNoteModeAnimation(string animationName)
    {
        if (string.IsNullOrEmpty(animationName))
        {
            return;
        }

        if (noteModeSkeletonGraphic != null)
        {
            noteModeSkeletonGraphic.Initialize(false);
            if (HasSpineAnimation(noteModeSkeletonGraphic.AnimationState, animationName))
            {
                noteModeSkeletonGraphic.AnimationState.SetAnimation(0, animationName, true);
                return;
            }
        }

        if (noteModeSkeletonAnimation != null)
        {
            noteModeSkeletonAnimation.Initialize(false);
            if (HasSpineAnimation(noteModeSkeletonAnimation.AnimationState, animationName))
            {
                noteModeSkeletonAnimation.AnimationState.SetAnimation(0, animationName, true);
            }
        }
    }

    private static bool HasSpineAnimation(Spine.AnimationState animationState, string animationName)
    {
        return animationState?.Data?.SkeletonData?.FindAnimation(animationName) != null;
    }

    private void PrepareCells(bool forceRebuild)
    {
        Transform cellRoot = boardLayout != null ? boardLayout.transform : transform;
        SudukuCell[] foundCells = cellRoot.GetComponentsInChildren<SudukuCell>(true);

        if ((forceRebuild || foundCells.Length == 0) && boardLayout != null)
        {
            boardLayout.ClearGeneratedChildren();
            boardLayout.Rebuild();
            foundCells = cellRoot.GetComponentsInChildren<SudukuCell>(true);
        }

        foreach (SudukuCell cell in foundCells)
        {
            if (!TryGetCellPosition(cell, out int row, out int column))
            {
                continue;
            }

            cell.ConfigurePenaltyLock(boardLayout != null ? boardLayout.LockSkeletonDataAsset : null);
            cell.Initialize(row, column);
            cells[row, column] = cell;
        }
    }

    private void BindCellButtons()
    {
        foreach (SudukuCell cell in cells)
        {
            if (cell == null)
            {
                continue;
            }

            Button button = cell.GetComponent<Button>();
            if (button == null)
            {
                button = cell.gameObject.AddComponent<Button>();
            }

            SudukuCell capturedCell = cell;
            button.onClick.AddListener(() => SelectCell(capturedCell));
        }
    }

    private void BindControlButtons()
    {
        foreach (Button button in FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (button.GetComponent<SudukuCell>() != null)
            {
                continue;
            }

            if (mathChallengePopup != null && button.transform.IsChildOf(mathChallengePopup.transform))
            {
                continue;
            }

            if (button == noteModeButton || IsNoteModeButton(button))
            {
                noteModeButton = button;
                noteModeButton.onClick.AddListener(ToggleNoteMode);
                RefreshNoteModeAnimation();
                continue;
            }

            if (TryGetButtonNumber(button, out int value))
            {
                int capturedValue = value;
                button.onClick.AddListener(() => InputNumber(capturedValue));
                continue;
            }

            if (IsReturnButton(button))
            {
                ConfigureControlButtonAnimation(button, UIButtonAnimationMode.LegacyAnimation, ReturnIdleAnimationName, ReturnClickAnimationName);
                button.onClick.AddListener(UndoLastMove);
            }
            else if (IsEraseButton(button))
            {
                ConfigureControlButtonAnimation(button, UIButtonAnimationMode.Spine, EraseIdleAnimationName, EraseClickAnimationName);
                button.onClick.AddListener(() => InputNumber(EmptyValue));
            }
            else if (IsHintButton(button))
            {
                hintButton = button;
                hintAnimatedButton = ConfigureControlButtonAnimation(button, UIButtonAnimationMode.Spine, HintIdleAnimationName, HintClickAnimationName, false);
                CacheHintCountText(button.transform);
                CacheHintAdBadge(button.transform);
                RefreshHintCountDisplay();
                button.onClick.AddListener(UseHint);
            }
        }
    }

    private void PrepareGameFlow()
    {
        if (objectHome == null)
        {
            Transform homeTransform = FindSceneTransformByName(ObjectHomeName);
            if (homeTransform != null)
            {
                objectHome = homeTransform.gameObject;
            }
        }

        if (playButton == null)
        {
            Transform playTransform = objectHome != null
                ? FindDeepChild(objectHome.transform, ButtonPlayName)
                : FindSceneTransformByName(ButtonPlayName);

            playButton = GetOrCreateButton(playTransform);
        }

        if (backButton == null)
        {
            backButton = GetOrCreateButton(FindSceneTransformByName(IconBackName));
        }

        CacheAutoGameplayRoots();

        if (playButton != null)
        {
            playButton.onClick.AddListener(EnterPlaying);
        }

        if (backButton != null)
        {
            backButton.onClick.AddListener(HandleGameBackButton);
        }
    }

    private void HandleGameBackButton()
    {
        if (gameBackHandler != null)
        {
            gameBackHandler.Invoke();
            return;
        }

        EnterHome();
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

    private void SetHomeVisible(bool visible)
    {
        if (objectHome != null)
        {
            objectHome.SetActive(visible);
        }
    }

    private void SetGameplayVisible(bool visible)
    {
        if (gameplayRootObjects != null && gameplayRootObjects.Length > 0)
        {
            foreach (GameObject gameplayRoot in gameplayRootObjects)
            {
                if (gameplayRoot != null)
                {
                    gameplayRoot.SetActive(visible);
                }
            }

            return;
        }

        if (autoGameplayRootObjects.Count > 0)
        {
            foreach (GameObject gameplayRoot in autoGameplayRootObjects)
            {
                if (gameplayRoot != null)
                {
                    gameplayRoot.SetActive(visible);
                }
            }

            return;
        }

        if (objectHome == null || objectHome.transform.parent == null)
        {
            return;
        }
    }

    private void CacheAutoGameplayRoots()
    {
        autoGameplayRootObjects.Clear();

        if (objectHome == null || objectHome.transform.parent == null)
        {
            return;
        }

        Transform parent = objectHome.transform.parent;
        for (int index = 0; index < parent.childCount; index++)
        {
            Transform child = parent.GetChild(index);
            if (child == objectHome.transform || IsMathChallengeTransform(child) || IsControllerTransform(child) || IsMenuPageTransform(child))
            {
                continue;
            }

            if (child.gameObject.activeSelf)
            {
                autoGameplayRootObjects.Add(child.gameObject);
            }
        }
    }

    private bool IsMathChallengeTransform(Transform target)
    {
        return mathChallengePopup != null && target == mathChallengePopup.transform;
    }

    private bool IsControllerTransform(Transform target)
    {
        return transform == target || transform.IsChildOf(target);
    }

    private static bool IsMenuPageTransform(Transform target)
    {
        return target.name == "Daily_10_Levels" ||
               target.name == "ClassicDifficultyPage" ||
               target.name == "Object_WellDone" ||
               target.name.StartsWith("Levels_", StringComparison.Ordinal);
    }

    private void CloseMathChallengeIfNeeded()
    {
        if (mathChallengePopup != null)
        {
            mathChallengePopup.Hide();
        }

        pendingMathChallengeCell = null;
    }

    private static AnimatedUIButton ConfigureControlButtonAnimation(Button button, UIButtonAnimationMode mode, string idleAnimation, string clickAnimation)
    {
        return ConfigureControlButtonAnimation(button, mode, idleAnimation, clickAnimation, true);
    }

    private static AnimatedUIButton ConfigureControlButtonAnimation(Button button, UIButtonAnimationMode mode, string idleAnimation, string clickAnimation, bool playClickAutomatically)
    {
        AnimatedUIButton animatedButton = button.GetComponent<AnimatedUIButton>();
        if (animatedButton == null)
        {
            animatedButton = button.gameObject.AddComponent<AnimatedUIButton>();
        }

        animatedButton.Configure(mode, idleAnimation, clickAnimation, playClickAutomatically);
        return animatedButton;
    }

    private void SelectCell(SudukuCell cell)
    {
        if (!IsPlayingState() && cell != null)
        {
            return;
        }

        if (isMathChallengeActive && cell != null)
        {
            return;
        }

        if (cell != null && cell.IsPenaltyLocked)
        {
            cell.PlayPenaltyLockWiggle();
            return;
        }

        selectedCell = cell;
        selectedNumber = selectedCell != null ? selectedCell.Value : EmptyValue;
        RefreshBoardState();
    }

    private void UseHint()
    {
        if (!IsPlayingState())
        {
            return;
        }

        if (isMathChallengeActive || remainingHintCount <= 0)
        {
            if (remainingHintCount <= 0)
            {
                PlayHintButtonAnimation(HintWiggleAnimationName);
            }

            RefreshHintCountDisplay();
            return;
        }

        PlayHintButtonAnimation(HintClickAnimationName);

        SudukuCell targetCell = GetHintTargetCell();
        if (targetCell == null)
        {
            RefreshBoardState();
            return;
        }

        int answer = solution[targetCell.Row, targetCell.Column];
        if (answer == EmptyValue)
        {
            RefreshBoardState();
            return;
        }

        history.Push(new MoveRecord
        {
            Cell = targetCell,
            PreviousValue = targetCell.Value,
            NewValue = answer,
            PreviousWasHint = targetCell.IsHint,
            NewWasHint = true
        });

        remainingHintCount--;
        RefreshHintCountDisplay();
        targetCell.SetValue(answer, false, false, true);
        SelectCell(targetCell);
        targetCell.PlayShineSweep();
        consecutiveWrongPlacements = 0;
        TriggerNewlyCompletedUnits(targetCell);
        CheckPuzzleCompleted();
    }

    private SudukuCell GetHintTargetCell()
    {
        if (IsHintEligibleCell(selectedCell))
        {
            return selectedCell;
        }

        SudukuCell bestCell = null;
        int bestCandidateCount = int.MaxValue;
        for (int row = 0; row < BoardLength; row++)
        {
            for (int column = 0; column < BoardLength; column++)
            {
                SudukuCell cell = cells[row, column];
                if (!IsHintEligibleCell(cell))
                {
                    continue;
                }

                int candidateCount = CountCandidates(cell);
                if (candidateCount < bestCandidateCount)
                {
                    bestCell = cell;
                    bestCandidateCount = candidateCount;
                }
            }
        }

        return bestCell;
    }

    private static bool IsHintEligibleCell(SudukuCell cell)
    {
        return cell != null &&
               !cell.IsFixed &&
               !cell.IsPenaltyLocked &&
               cell.Value == EmptyValue;
    }

    private void CacheHintCountText(Transform hintButtonTransform)
    {
        if (hintCountText != null)
        {
            return;
        }

        Transform existingText = FindDeepChild(hintButtonTransform, HintCountTextName);
        if (existingText != null)
        {
            hintCountText = existingText.GetComponent<TMP_Text>();
        }

        if (hintCountText == null && hintButtonTransform != null)
        {
            hintCountText = CreateHintCountText(hintButtonTransform);
        }
    }

    private TMP_Text CreateHintCountText(Transform parent)
    {
        GameObject textObject = new GameObject(HintCountTextName, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(parent, false);

        RectTransform rectTransform = textObject.GetComponent<RectTransform>();
        rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
        rectTransform.anchoredPosition = new Vector2(18f, 18f);
        rectTransform.sizeDelta = new Vector2(42f, 28f);

        TMP_Text text = textObject.GetComponent<TMP_Text>();
        text.alignment = TextAlignmentOptions.Center;
        text.enableAutoSizing = true;
        text.fontSizeMin = 10f;
        text.fontSizeMax = 22f;
        text.color = Color.white;
        text.raycastTarget = false;
        return text;
    }

    private void CacheHintAdBadge(Transform hintButtonTransform)
    {
        if (hintAdBadge == null && hintButtonTransform != null)
        {
            Transform adBadgeTransform = FindDeepChild(hintButtonTransform, HintAdBadgeName);
            if (adBadgeTransform != null)
            {
                hintAdBadge = adBadgeTransform.gameObject;
            }
        }

        if (hintAdBadgeAnimation == null && hintAdBadge != null)
        {
            hintAdBadgeAnimation = hintAdBadge.GetComponent<UnityEngine.Animation>();
            if (hintAdBadgeAnimation == null)
            {
                hintAdBadgeAnimation = hintAdBadge.GetComponentInChildren<UnityEngine.Animation>(true);
            }
        }
    }

    private void RefreshHintCountDisplay()
    {
        if (hintCountText != null)
        {
            hintCountText.text = string.Format(hintCountFormat, remainingHintCount);
        }

        if (hintButton != null)
        {
            hintButton.interactable = true;
        }

        RefreshHintAdBadge();
    }

    private void RefreshHintAdBadge()
    {
        if (hintAdBadge == null && hintButton != null)
        {
            CacheHintAdBadge(hintButton.transform);
        }

        if (hintAdBadge == null)
        {
            return;
        }

        bool shouldShowAdBadge = remainingHintCount <= 0;
        if (hintAdBadge.activeSelf != shouldShowAdBadge)
        {
            hintAdBadge.SetActive(shouldShowAdBadge);
        }

        if (shouldShowAdBadge)
        {
            PlayHintAdBadgeWiggle();
        }
        else
        {
            StopHintAdBadgeWiggle();
        }
    }

    private void PlayHintAdBadgeWiggle()
    {
        if (hintAdBadgeAnimation == null)
        {
            CacheHintAdBadge(hintButton != null ? hintButton.transform : null);
        }

        if (hintAdBadgeAnimation == null)
        {
            return;
        }

        AnimationClip clip = hintAdBadgeAnimation.GetClip(HintAdBadgeWiggleAnimationName);
        if (clip == null)
        {
            return;
        }

        hintAdBadgeAnimation.wrapMode = WrapMode.Loop;
        UnityEngine.AnimationState state = hintAdBadgeAnimation[HintAdBadgeWiggleAnimationName];
        if (state != null)
        {
            state.wrapMode = WrapMode.Loop;
        }

        if (!hintAdBadgeAnimation.IsPlaying(HintAdBadgeWiggleAnimationName))
        {
            hintAdBadgeAnimation.Play(HintAdBadgeWiggleAnimationName);
        }
    }

    private void StopHintAdBadgeWiggle()
    {
        if (hintAdBadgeAnimation == null)
        {
            return;
        }

        if (hintAdBadgeAnimation.IsPlaying(HintAdBadgeWiggleAnimationName))
        {
            hintAdBadgeAnimation.Stop(HintAdBadgeWiggleAnimationName);
        }
    }

    private void PlayHintButtonAnimation(string animationName)
    {
        if (hintAnimatedButton == null && hintButton != null)
        {
            hintAnimatedButton = hintButton.GetComponent<AnimatedUIButton>();
        }

        if (hintAnimatedButton != null)
        {
            hintAnimatedButton.PlayOneShot(animationName);
        }
    }

    private int CountCandidates(SudukuCell cell)
    {
        int count = 0;
        for (int value = 1; value <= BoardLength; value++)
        {
            if (CanPlaceCandidate(cell, value))
            {
                count++;
            }
        }

        return count;
    }

    private bool CanPlaceCandidate(SudukuCell cell, int value)
    {
        if (cell == null || value < 1 || value > BoardLength)
        {
            return false;
        }

        for (int column = 0; column < BoardLength; column++)
        {
            SudukuCell rowCell = cells[cell.Row, column];
            if (rowCell != cell && rowCell != null && rowCell.Value == value)
            {
                return false;
            }
        }

        for (int row = 0; row < BoardLength; row++)
        {
            SudukuCell columnCell = cells[row, cell.Column];
            if (columnCell != cell && columnCell != null && columnCell.Value == value)
            {
                return false;
            }
        }

        int startRow = cell.Row / BoxLength * BoxLength;
        int startColumn = cell.Column / BoxLength * BoxLength;
        for (int rowOffset = 0; rowOffset < BoxLength; rowOffset++)
        {
            for (int columnOffset = 0; columnOffset < BoxLength; columnOffset++)
            {
                SudukuCell boxCell = cells[startRow + rowOffset, startColumn + columnOffset];
                if (boxCell != cell && boxCell != null && boxCell.Value == value)
                {
                    return false;
                }
            }
        }

        return true;
    }

    private void ClearPenaltyLocks()
    {
        foreach (SudukuCell cell in cells)
        {
            if (cell != null)
            {
                cell.ClearPenaltyLock();
            }
        }
    }

    private void RefreshBoardState()
    {
        for (int row = 0; row < BoardLength; row++)
        {
            for (int column = 0; column < BoardLength; column++)
            {
                SudukuCell cell = cells[row, column];
                if (cell == null)
                {
                    continue;
                }

                cell.SetWrong(IsWrongValue(cell, cell.Value), wrongNumberColor);
                cell.SetBackgroundColor(GetCellBackgroundColor(cell));
            }
        }
    }

    private void RefreshCompletedUnitCache()
    {
        for (int index = 0; index < BoardLength; index++)
        {
            completedRows[index] = IsRowComplete(index);
            completedColumns[index] = IsColumnComplete(index);
            completedBoxes[index] = IsBoxComplete(index);
        }
    }

    private void TriggerNewlyCompletedUnits(SudukuCell centerCell)
    {
        if (centerCell == null || centerCell.Value == EmptyValue || centerCell.IsWrong)
        {
            RefreshCompletedUnitCache();
            return;
        }

        List<SudukuCell> cellsToPlay = new List<SudukuCell>(BoardLength * 3);

        int row = centerCell.Row;
        int column = centerCell.Column;
        int box = GetBoxIndex(row, column);

        bool rowComplete = IsRowComplete(row);
        if (rowComplete && !completedRows[row])
        {
            AddRowCells(row, cellsToPlay);
        }

        completedRows[row] = rowComplete;

        bool columnComplete = IsColumnComplete(column);
        if (columnComplete && !completedColumns[column])
        {
            AddColumnCells(column, cellsToPlay);
        }

        completedColumns[column] = columnComplete;

        bool boxComplete = IsBoxComplete(box);
        if (boxComplete && !completedBoxes[box])
        {
            AddBoxCells(box, cellsToPlay);
        }

        completedBoxes[box] = boxComplete;

        if (cellsToPlay.Count == 0)
        {
            RefreshCompletedUnitCache();
            return;
        }

        SortCellsByDistanceFrom(centerCell, cellsToPlay);
        PlayShineSweepSequence(cellsToPlay);
        RefreshCompletedUnitCache();
    }

    private void PlayShineSweepSequence(List<SudukuCell> cellsToPlay)
    {
        if (shineSweepRoutine != null)
        {
            StopCoroutine(shineSweepRoutine);
        }

        shineSweepRoutine = StartCoroutine(PlayShineSweepSequenceRoutine(cellsToPlay));
    }

    private IEnumerator PlayShineSweepSequenceRoutine(List<SudukuCell> cellsToPlay)
    {
        for (int index = 0; index < cellsToPlay.Count; index++)
        {
            SudukuCell cell = cellsToPlay[index];
            if (cell != null)
            {
                cell.PlayShineSweep();
            }

            if (index >= cellsToPlay.Count - 1)
            {
                continue;
            }

            for (int frame = 0; frame < ShineSweepFrameStagger; frame++)
            {
                yield return null;
            }
        }

        float lastClipLength = GetLongestShineSweepClipLength(cellsToPlay);
        if (lastClipLength > 0f)
        {
            yield return new WaitForSeconds(lastClipLength);
        }

        shineSweepRoutine = null;
        CompletePuzzleIfWaitingForShineSweep();
    }

    private bool IsRowComplete(int row)
    {
        for (int column = 0; column < BoardLength; column++)
        {
            if (!IsCellComplete(cells[row, column]))
            {
                return false;
            }
        }

        return true;
    }

    private bool IsColumnComplete(int column)
    {
        for (int row = 0; row < BoardLength; row++)
        {
            if (!IsCellComplete(cells[row, column]))
            {
                return false;
            }
        }

        return true;
    }

    private bool IsBoxComplete(int box)
    {
        int startRow = box / BoxLength * BoxLength;
        int startColumn = box % BoxLength * BoxLength;

        for (int rowOffset = 0; rowOffset < BoxLength; rowOffset++)
        {
            for (int columnOffset = 0; columnOffset < BoxLength; columnOffset++)
            {
                if (!IsCellComplete(cells[startRow + rowOffset, startColumn + columnOffset]))
                {
                    return false;
                }
            }
        }

        return true;
    }

    private bool IsBoardComplete()
    {
        for (int row = 0; row < BoardLength; row++)
        {
            if (!IsRowComplete(row))
            {
                return false;
            }
        }

        return true;
    }

    private void CheckPuzzleCompleted()
    {
        if (hasCompletedCurrentPuzzle || !IsPlayingState() || !IsBoardComplete())
        {
            return;
        }

        hasCompletedCurrentPuzzle = true;
        if (shineSweepRoutine != null)
        {
            completePuzzleAfterShineSweep = true;
            return;
        }

        CompletePuzzle();
    }

    private void CompletePuzzleIfWaitingForShineSweep()
    {
        if (!completePuzzleAfterShineSweep)
        {
            return;
        }

        completePuzzleAfterShineSweep = false;
        CompletePuzzle();
    }

    private void CompletePuzzle()
    {
        puzzleCompletedHandler?.Invoke();
    }

    private static float GetLongestShineSweepClipLength(List<SudukuCell> cellsToPlay)
    {
        float longestLength = 0f;
        foreach (SudukuCell cell in cellsToPlay)
        {
            if (cell == null)
            {
                continue;
            }

            Animation animation = cell.GetComponent<Animation>();
            AnimationClip clip = animation != null ? animation.GetClip(CellShineSweepAnimationName) : null;
            if (clip != null && clip.length > longestLength)
            {
                longestLength = clip.length;
            }
        }

        return longestLength;
    }

    private bool IsCellComplete(SudukuCell cell)
    {
        return cell != null &&
               cell.Value != EmptyValue &&
               !IsWrongValue(cell, cell.Value);
    }

    private void AddRowCells(int row, List<SudukuCell> target)
    {
        for (int column = 0; column < BoardLength; column++)
        {
            AddCellIfMissing(cells[row, column], target);
        }
    }

    private void AddColumnCells(int column, List<SudukuCell> target)
    {
        for (int row = 0; row < BoardLength; row++)
        {
            AddCellIfMissing(cells[row, column], target);
        }
    }

    private void AddBoxCells(int box, List<SudukuCell> target)
    {
        int startRow = box / BoxLength * BoxLength;
        int startColumn = box % BoxLength * BoxLength;

        for (int rowOffset = 0; rowOffset < BoxLength; rowOffset++)
        {
            for (int columnOffset = 0; columnOffset < BoxLength; columnOffset++)
            {
                AddCellIfMissing(cells[startRow + rowOffset, startColumn + columnOffset], target);
            }
        }
    }

    private static void AddCellIfMissing(SudukuCell cell, List<SudukuCell> target)
    {
        if (cell != null && !target.Contains(cell))
        {
            target.Add(cell);
        }
    }

    private static void SortCellsByDistanceFrom(SudukuCell centerCell, List<SudukuCell> target)
    {
        target.Sort((first, second) =>
        {
            int firstDistance = GetCellDistance(centerCell, first);
            int secondDistance = GetCellDistance(centerCell, second);
            if (firstDistance != secondDistance)
            {
                return firstDistance.CompareTo(secondDistance);
            }

            int rowCompare = first.Row.CompareTo(second.Row);
            return rowCompare != 0 ? rowCompare : first.Column.CompareTo(second.Column);
        });
    }

    private static int GetCellDistance(SudukuCell centerCell, SudukuCell cell)
    {
        return Mathf.Abs(cell.Row - centerCell.Row) + Mathf.Abs(cell.Column - centerCell.Column);
    }

    private void ApplyPuzzle()
    {
        for (int row = 0; row < BoardLength; row++)
        {
            for (int column = 0; column < BoardLength; column++)
            {
                if (cells[row, column] == null)
                {
                    continue;
                }

                int value = puzzle[row, column];
                cells[row, column].SetValue(value, value != EmptyValue, false);
                cells[row, column].SetBackgroundColor(normalCellColor);
            }
        }
    }

    private Color GetCellBackgroundColor(SudukuCell cell)
    {
        if (selectedCell == null)
        {
            return IsSameSelectedNumber(cell) ? sameNumberCellColor : normalCellColor;
        }

        if (cell == selectedCell)
        {
            return selectedCellColor;
        }

        if (IsSameSelectedNumber(cell))
        {
            return sameNumberCellColor;
        }

        if (IsRelatedToSelectedCell(cell))
        {
            return relatedCellColor;
        }

        return normalCellColor;
    }

    private bool IsSameSelectedNumber(SudukuCell cell)
    {
        return selectedNumber != EmptyValue && cell.Value == selectedNumber;
    }

    private bool IsRelatedToSelectedCell(SudukuCell cell)
    {
        if (selectedCell == null)
        {
            return false;
        }

        return cell.Row == selectedCell.Row ||
               cell.Column == selectedCell.Column ||
               IsSameBox(cell, selectedCell);
    }

    private static bool IsSameBox(SudukuCell first, SudukuCell second)
    {
        return first.Row / BoxLength == second.Row / BoxLength &&
               first.Column / BoxLength == second.Column / BoxLength;
    }

    private static int GetBoxIndex(int row, int column)
    {
        return row / BoxLength * BoxLength + column / BoxLength;
    }

    private bool IsWrongValue(SudukuCell cell, int value)
    {
        return cell != null &&
               !cell.IsFixed &&
               value != EmptyValue &&
               solution[cell.Row, cell.Column] != EmptyValue &&
               value != solution[cell.Row, cell.Column];
    }

    private static bool TryGetCellPosition(SudukuCell cell, out int row, out int column)
    {
        row = cell.Row;
        column = cell.Column;

        if (IsInsideBoard(row, column) && (row != 0 || column != 0))
        {
            return true;
        }

        string[] parts = cell.name.Split('_');
        if (parts.Length >= 4 &&
            int.TryParse(parts[parts.Length - 2], out row) &&
            int.TryParse(parts[parts.Length - 1], out column))
        {
            return IsInsideBoard(row, column);
        }

        return IsInsideBoard(row, column);
    }

    private static bool TryGetButtonNumber(Button button, out int value)
    {
        value = EmptyValue;
        TMP_Text text = button.GetComponentInChildren<TMP_Text>(true);
        return text != null && int.TryParse(text.text, out value) && value >= 1 && value <= BoardLength;
    }

    private static bool IsReturnButton(Button button)
    {
        return button.name == ReturnButtonName;
    }

    private static bool IsEraseButton(Button button)
    {
        return button.name == EraseButtonName;
    }

    private static bool IsNoteModeButton(Button button)
    {
        return button.name == NotesButtonName;
    }

    private static bool IsHintButton(Button button)
    {
        return button.name == HintButtonName;
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

    private static bool IsInsideBoard(int row, int column)
    {
        return row >= 0 && row < BoardLength && column >= 0 && column < BoardLength;
    }

    private static void ClearBoard(int[,] board)
    {
        for (int row = 0; row < BoardLength; row++)
        {
            for (int column = 0; column < BoardLength; column++)
            {
                board[row, column] = EmptyValue;
            }
        }
    }

    private static void CopyBoard(int[,] source, int[,] target)
    {
        for (int row = 0; row < BoardLength; row++)
        {
            for (int column = 0; column < BoardLength; column++)
            {
                target[row, column] = source[row, column];
            }
        }
    }

    private static bool FillBoard(int[,] board)
    {
        for (int row = 0; row < BoardLength; row++)
        {
            for (int column = 0; column < BoardLength; column++)
            {
                if (board[row, column] != EmptyValue)
                {
                    continue;
                }

                List<int> candidates = CreateShuffledDigits();
                foreach (int value in candidates)
                {
                    if (!CanPlace(board, row, column, value))
                    {
                        continue;
                    }

                    board[row, column] = value;
                    if (FillBoard(board))
                    {
                        return true;
                    }
                }

                board[row, column] = EmptyValue;
                return false;
            }
        }

        return true;
    }

    private static void DigHolesWithUniqueSolution(int[,] board, int targetHoles)
    {
        List<int> positions = CreateShuffledPositions();
        int holes = 0;

        foreach (int position in positions)
        {
            if (holes >= targetHoles)
            {
                break;
            }

            int row = position / BoardLength;
            int column = position % BoardLength;
            int previousValue = board[row, column];

            board[row, column] = EmptyValue;
            if (CountSolutions(board, 2) == 1)
            {
                holes++;
            }
            else
            {
                board[row, column] = previousValue;
            }
        }
    }

    private static int CountSolutions(int[,] board, int maxSolutions)
    {
        int bestRow = -1;
        int bestColumn = -1;
        List<int> bestCandidates = null;

        for (int row = 0; row < BoardLength; row++)
        {
            for (int column = 0; column < BoardLength; column++)
            {
                if (board[row, column] != EmptyValue)
                {
                    continue;
                }

                List<int> candidates = GetCandidates(board, row, column);
                if (candidates.Count == 0)
                {
                    return 0;
                }

                if (bestCandidates == null || candidates.Count < bestCandidates.Count)
                {
                    bestRow = row;
                    bestColumn = column;
                    bestCandidates = candidates;
                }
            }
        }

        if (bestCandidates == null)
        {
            return 1;
        }

        int solutions = 0;
        foreach (int value in bestCandidates)
        {
            board[bestRow, bestColumn] = value;
            solutions += CountSolutions(board, maxSolutions);
            board[bestRow, bestColumn] = EmptyValue;

            if (solutions >= maxSolutions)
            {
                break;
            }
        }

        return solutions;
    }

    private static List<int> GetCandidates(int[,] board, int row, int column)
    {
        List<int> candidates = new List<int>(BoardLength);
        for (int value = 1; value <= BoardLength; value++)
        {
            if (CanPlace(board, row, column, value))
            {
                candidates.Add(value);
            }
        }

        return candidates;
    }

    private static bool CanPlace(int[,] board, int row, int column, int value)
    {
        for (int index = 0; index < BoardLength; index++)
        {
            if (board[row, index] == value || board[index, column] == value)
            {
                return false;
            }
        }

        int boxStartRow = row / BoxLength * BoxLength;
        int boxStartColumn = column / BoxLength * BoxLength;
        for (int boxRow = 0; boxRow < BoxLength; boxRow++)
        {
            for (int boxColumn = 0; boxColumn < BoxLength; boxColumn++)
            {
                if (board[boxStartRow + boxRow, boxStartColumn + boxColumn] == value)
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static List<int> CreateShuffledDigits()
    {
        List<int> digits = new List<int>(BoardLength);
        for (int value = 1; value <= BoardLength; value++)
        {
            digits.Add(value);
        }

        Shuffle(digits);
        return digits;
    }

    private static List<int> CreateShuffledPositions()
    {
        List<int> positions = new List<int>(BoardLength * BoardLength);
        for (int position = 0; position < BoardLength * BoardLength; position++)
        {
            positions.Add(position);
        }

        Shuffle(positions);
        return positions;
    }

    private static void Shuffle<T>(IList<T> values)
    {
        for (int index = values.Count - 1; index > 0; index--)
        {
            int randomIndex = UnityEngine.Random.Range(0, index + 1);
            T temp = values[index];
            values[index] = values[randomIndex];
            values[randomIndex] = temp;
        }
    }
}
