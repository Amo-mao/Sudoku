using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Spine.Unity;

[DisallowMultipleComponent]
[AddComponentMenu("UI/Suduku Game Controller")]
public sealed class SudukuGameController : MonoBehaviour
{
    private const int BoardLength = 9;
    private const int BoxLength = 3;
    private const int EmptyValue = 0;
    private const int ShineSweepFrameStagger = 15;

    [SerializeField] private SudukuBoardLayout boardLayout;
    [SerializeField, Range(20, 64)] private int holesToDig = 45;
    [SerializeField] private bool generateOnStart = true;

    [Header("Number Colors")]
    [SerializeField] private Color wrongNumberColor = new Color(0.85f, 0.12f, 0.12f, 1f);

    [Header("Note Mode")]
    [SerializeField] private Button noteModeButton;
    [SerializeField] private SkeletonGraphic noteModeSkeletonGraphic;
    [SerializeField] private SkeletonAnimation noteModeSkeletonAnimation;
    [SerializeField] private string noteModeIdleAnimation = "idle";
    [SerializeField] private string noteModeActiveAnimation = "activate";

    [Header("Math Challenge")]
    [SerializeField] private MathChallengePopup mathChallengePopup;
    [SerializeField] private GameObject mathChallengePrefab;
    [SerializeField] private Transform mathChallengeParent;
    [SerializeField, Min(1)] private int wrongPlacementsBeforeMathChallenge = 2;

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

    private SudukuCell selectedCell;
    private int selectedNumber;
    private int consecutiveWrongPlacements;
    private bool isMathChallengeActive;
    private bool isNoteMode;
    private Coroutine shineSweepRoutine;

    private struct MoveRecord
    {
        public SudukuCell Cell;
        public int PreviousValue;
        public int NewValue;
    }

    private void Awake()
    {
        if (boardLayout == null)
        {
            boardLayout = GetComponent<SudukuBoardLayout>();
        }

        PrepareCells(true);
        PrepareMathChallengePopup();
        BindCellButtons();
        BindControlButtons();
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
        if (generateOnStart)
        {
            GenerateNewPuzzle();
        }
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
        selectedNumber = EmptyValue;
        consecutiveWrongPlacements = 0;
        isMathChallengeActive = false;
        SetNoteMode(false);
        SelectCell(null);
    }

    public void InputNumber(int value)
    {
        if (isMathChallengeActive)
        {
            return;
        }

        value = Mathf.Clamp(value, EmptyValue, BoardLength);
        selectedNumber = value;

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
            NewValue = value
        });

        SudukuCell changedCell = selectedCell;
        bool isWrongPlacement = IsWrongValue(selectedCell, value);
        selectedCell.SetValue(value, false, isWrongPlacement);
        RefreshBoardState();

        UpdateWrongPlacementStreak(value, isWrongPlacement);
        if (isMathChallengeActive)
        {
            return;
        }

        TriggerNewlyCompletedUnits(changedCell);
    }

    public void UndoLastMove()
    {
        if (isMathChallengeActive)
        {
            return;
        }

        while (history.Count > 0)
        {
            MoveRecord move = history.Pop();
            if (move.Cell == null || move.Cell.IsFixed || move.Cell.Value != move.NewValue)
            {
                continue;
            }

            move.Cell.SetValue(move.PreviousValue, false, IsWrongValue(move.Cell, move.PreviousValue));
            RefreshCompletedUnitCache();
            SelectCell(move.Cell);
            return;
        }
    }

    public void ToggleNoteMode()
    {
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

    private void UpdateWrongPlacementStreak(int value, bool isWrongPlacement)
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
        consecutiveWrongPlacements = 0;
        mathChallengePopup.Show(ResumeAfterMathChallenge);
    }

    private void ResumeAfterMathChallenge()
    {
        isMathChallengeActive = false;
        RefreshBoardState();
    }

    private void InputNote(int value)
    {
        if (value == EmptyValue)
        {
            RefreshBoardState();
            return;
        }

        if (selectedCell == null || selectedCell.IsFixed || selectedCell.Value != EmptyValue)
        {
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
                button.onClick.AddListener(UndoLastMove);
            }
            else if (IsEraseButton(button))
            {
                button.onClick.AddListener(() => InputNumber(EmptyValue));
            }
        }
    }

    private void SelectCell(SudukuCell cell)
    {
        selectedCell = cell;
        selectedNumber = selectedCell != null ? selectedCell.Value : EmptyValue;
        RefreshBoardState();
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

        shineSweepRoutine = null;
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
        return HasImageSpriteNamed(button, "返回") || HasButtonText(button, "return");
    }

    private static bool IsEraseButton(Button button)
    {
        return HasImageSpriteNamed(button, "橡皮擦") || HasButtonText(button, "erase");
    }

    private static bool IsNoteModeButton(Button button)
    {
        return button.name == "Button (2)" ||
               HasButtonText(button, "note") ||
               HasButtonText(button, "notes");
    }

    private static bool HasButtonText(Button button, string expectedText)
    {
        TMP_Text text = button.GetComponentInChildren<TMP_Text>(true);
        return text != null && string.Equals(text.text.Trim(), expectedText, System.StringComparison.OrdinalIgnoreCase);
    }

    private static bool HasImageSpriteNamed(Button button, string spriteName)
    {
        Image[] images = button.GetComponentsInChildren<Image>(true);
        foreach (Image image in images)
        {
            if (image.sprite != null && image.sprite.name.Contains(spriteName))
            {
                return true;
            }
        }

        return false;
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
            int randomIndex = Random.Range(0, index + 1);
            T temp = values[index];
            values[index] = values[randomIndex];
            values[randomIndex] = temp;
        }
    }
}
