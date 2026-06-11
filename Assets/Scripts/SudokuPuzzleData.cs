using System;

[Serializable]
public sealed class SudokuPuzzleData
{
    public const int BoardLength = 9;

    public int[] Puzzle = new int[BoardLength * BoardLength];
    public int[] Solution = new int[BoardLength * BoardLength];
    public int Holes;

    public int GetPuzzleValue(int row, int column)
    {
        return Puzzle[GetIndex(row, column)];
    }

    public int GetSolutionValue(int row, int column)
    {
        return Solution[GetIndex(row, column)];
    }

    public void SetPuzzleValue(int row, int column, int value)
    {
        Puzzle[GetIndex(row, column)] = value;
    }

    public void SetSolutionValue(int row, int column, int value)
    {
        Solution[GetIndex(row, column)] = value;
    }

    private static int GetIndex(int row, int column)
    {
        return row * BoardLength + column;
    }
}

[Serializable]
public sealed class SudokuLevelProgressData
{
    public int[] Values = new int[SudokuPuzzleData.BoardLength * SudokuPuzzleData.BoardLength];
    public bool[] Hints = new bool[SudokuPuzzleData.BoardLength * SudokuPuzzleData.BoardLength];
    public int[] Notes = new int[SudokuPuzzleData.BoardLength * SudokuPuzzleData.BoardLength];
    public int RemainingHintCount;
    public int ConsecutiveWrongPlacements;
}
