using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
[AddComponentMenu("UI/Suduku Game Controller")]
public sealed class SudukuGameController : MonoBehaviour
{
    private const int BoardLength = 9;
    private const int BoxLength = 3;
    private const int EmptyValue = 0;

    [SerializeField] private SudukuBoardLayout boardLayout;
    [SerializeField, Range(20, 64)] private int holesToDig = 45;
    [SerializeField] private bool generateOnStart = true;

    [Header("Number Colors")]
    [SerializeField] private Color wrongNumberColor = new Color(0.85f, 0.12f, 0.12f, 1f);

    [Header("Cell Highlight Colors")]
    [SerializeField] private Color normalCellColor = Color.white;
    [SerializeField] private Color relatedCellColor = new Color(0.9f, 0.95f, 1f, 1f);
    [SerializeField] private Color sameNumberCellColor = new Color(1f, 0.93f, 0.72f, 1f);
    [SerializeField] private Color selectedCellColor = new Color(0.62f, 0.82f, 1f, 1f);

    private readonly SudukuCell[,] cells = new SudukuCell[BoardLength, BoardLength];
    private readonly int[,] solution = new int[BoardLength, BoardLength];
    private readonly int[,] puzzle = new int[BoardLength, BoardLength];
    private readonly Stack<MoveRecord> history = new Stack<MoveRecord>();

    private SudukuCell selectedCell;
    private int selectedNumber;

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
        history.Clear();
        selectedNumber = EmptyValue;
        SelectCell(null);
    }

    public void InputNumber(int value)
    {
        value = Mathf.Clamp(value, EmptyValue, BoardLength);
        selectedNumber = value;

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

        selectedCell.SetValue(value, false, IsWrongValue(selectedCell, value));
        RefreshBoardState();
    }

    public void UndoLastMove()
    {
        while (history.Count > 0)
        {
            MoveRecord move = history.Pop();
            if (move.Cell == null || move.Cell.IsFixed || move.Cell.Value != move.NewValue)
            {
                continue;
            }

            move.Cell.SetValue(move.PreviousValue, false, IsWrongValue(move.Cell, move.PreviousValue));
            SelectCell(move.Cell);
            return;
        }
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
        return HasImageSpriteNamed(button, "返回");
    }

    private static bool IsEraseButton(Button button)
    {
        return HasImageSpriteNamed(button, "橡皮擦");
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
