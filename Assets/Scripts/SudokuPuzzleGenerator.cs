using System.Collections.Generic;
using UnityEngine;

public static class SudokuPuzzleGenerator
{
    private const int BoardLength = 9;
    private const int BoxLength = 3;
    private const int EmptyValue = 0;

    public static SudokuPuzzleData Generate(int holes, int seed)
    {
        Random.State previousState = Random.state;
        Random.InitState(seed);

        int[,] solution = new int[BoardLength, BoardLength];
        int[,] puzzle = new int[BoardLength, BoardLength];
        FillBoard(solution);
        CopyBoard(solution, puzzle);
        DigHolesWithUniqueSolution(puzzle, Mathf.Clamp(holes, 20, 64));

        Random.state = previousState;

        SudokuPuzzleData data = new SudokuPuzzleData
        {
            Holes = holes
        };

        for (int row = 0; row < BoardLength; row++)
        {
            for (int column = 0; column < BoardLength; column++)
            {
                data.SetPuzzleValue(row, column, puzzle[row, column]);
                data.SetSolutionValue(row, column, solution[row, column]);
            }
        }

        return data;
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
