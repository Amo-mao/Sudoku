using UnityEngine;
using UnityEngine.UI;

[ExecuteAlways]
[RequireComponent(typeof(RectTransform))]
[AddComponentMenu("UI/Suduku Board Layout")]
public sealed class SudukuBoardLayout : MonoBehaviour
{
    private const string GeneratedNamePrefix = "Generated_";

    [SerializeField] private GameObject cellPrefab;
    [SerializeField] private bool rebuildOnEnable;
    [SerializeField] private bool rebuildWhenValidated;

    [Header("Board Size")]
    [SerializeField, Min(1f)] private float boardSize = 671f;
    [SerializeField, Min(1f)] private float cellSize = 73f;
    [SerializeField, Min(0f)] private float thinLineSize = 1f;
    [SerializeField, Min(0f)] private float thickLineSize = 2f;
    [SerializeField, Min(0f)] private float outerLineSize = 2f;

    [Header("Colors")]
    [SerializeField] private Color thinLineColor = new Color(0.72f, 0.72f, 0.72f, 1f);
    [SerializeField] private Color thickLineColor = new Color(0.16f, 0.16f, 0.16f, 1f);
    [SerializeField] private Color outerLineColor = new Color(0.16f, 0.16f, 0.16f, 1f);
    [SerializeField] private Color cellColor = Color.white;

    private RectTransform rectTransform;

    private void OnEnable()
    {
        CacheRectTransform();

        if (rebuildOnEnable && cellPrefab != null)
        {
            Rebuild();
        }
    }

    private void OnValidate()
    {
        CacheRectTransform();
        boardSize = Mathf.Max(1f, boardSize);
        cellSize = Mathf.Max(1f, cellSize);
        thinLineSize = Mathf.Max(0f, thinLineSize);
        thickLineSize = Mathf.Max(0f, thickLineSize);
        outerLineSize = Mathf.Max(0f, outerLineSize);
        if (rectTransform != null)
        {
            rectTransform.sizeDelta = new Vector2(boardSize, boardSize);
        }

        if (rebuildWhenValidated && cellPrefab != null)
        {
            Rebuild();
        }
    }

    [ContextMenu("Apply 671 Board Size")]
    public void ApplyRecommended671Size()
    {
        boardSize = 671f;
        cellSize = 73f;
        thinLineSize = 1f;
        thickLineSize = 2f;
        outerLineSize = 2f;

        CacheRectTransform();
        if (rectTransform != null)
        {
            rectTransform.sizeDelta = new Vector2(boardSize, boardSize);
        }
    }

    [ContextMenu("Rebuild Board")]
    public void Rebuild()
    {
        if (cellPrefab == null)
        {
            Debug.LogWarning("SudukuBoardLayout needs a Cell Prefab before rebuilding.", this);
            return;
        }

        CacheRectTransform();
        ClearGeneratedChildren();
        if (rectTransform == null)
        {
            Debug.LogWarning("SudukuBoardLayout needs a RectTransform.", this);
            return;
        }

        rectTransform.sizeDelta = new Vector2(boardSize, boardSize);

        CreateCells();
        CreateGridLines();
    }

    [ContextMenu("Clear Generated Board")]
    public void ClearGeneratedChildren()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Transform child = transform.GetChild(i);
            if (!child.name.StartsWith(GeneratedNamePrefix))
            {
                continue;
            }

            if (Application.isPlaying)
            {
                Destroy(child.gameObject);
            }
            else
            {
                DestroyImmediate(child.gameObject);
            }
        }
    }

    private void CreateCells()
    {
        for (int row = 0; row < 9; row++)
        {
            for (int column = 0; column < 9; column++)
            {
                GameObject cell = Instantiate(cellPrefab, transform);
                cell.name = $"{GeneratedNamePrefix}Cell_{row}_{column}";

                RectTransform cellRect = cell.GetComponent<RectTransform>();
                if (cellRect == null)
                {
                    cellRect = cell.AddComponent<RectTransform>();
                }

                cellRect.anchorMin = new Vector2(0.5f, 0.5f);
                cellRect.anchorMax = new Vector2(0.5f, 0.5f);
                cellRect.pivot = new Vector2(0.5f, 0.5f);
                cellRect.sizeDelta = new Vector2(cellSize, cellSize);
                cellRect.anchoredPosition = new Vector2(GetCellCenterPosition(column), -GetCellCenterPosition(row));
                FitCellChildren(cellRect);

                SudukuCell sudukuCell = cell.GetComponent<SudukuCell>();
                if (sudukuCell == null)
                {
                    sudukuCell = cell.AddComponent<SudukuCell>();
                }

                sudukuCell.Initialize(row, column);

                Image image = cell.GetComponent<Image>();
                if (image != null)
                {
                    image.color = cellColor;
                }
            }
        }
    }

    private void CreateGridLines()
    {
        CreateGridLinesByType(GridLineType.Thin);
        CreateGridLinesByType(GridLineType.Thick);
        CreateGridLinesByType(GridLineType.Outer);
    }

    private void CreateGridLinesByType(GridLineType lineType)
    {
        for (int line = 0; line <= 9; line++)
        {
            if (GetLineType(line) != lineType)
            {
                continue;
            }

            float lineSize = GetLineSize(line);
            float center = GetLineCenterPosition(line);
            Color color = GetLineColor(line);

            CreateLine($"{GeneratedNamePrefix}VerticalLine_{line}", true, center, lineSize, color);
            CreateLine($"{GeneratedNamePrefix}HorizontalLine_{line}", false, -center, lineSize, color);
        }
    }

    private void FitCellChildren(RectTransform cellRect)
    {
        for (int i = 0; i < cellRect.childCount; i++)
        {
            RectTransform child = cellRect.GetChild(i) as RectTransform;
            if (child == null)
            {
                continue;
            }

            child.anchorMin = Vector2.zero;
            child.anchorMax = Vector2.one;
            child.pivot = new Vector2(0.5f, 0.5f);
            child.anchoredPosition = Vector2.zero;
            child.sizeDelta = Vector2.zero;
        }
    }

    private void CreateLine(string lineName, bool vertical, float anchoredAxisPosition, float lineSize, Color color)
    {
        GameObject line = new GameObject(lineName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        line.transform.SetParent(transform, false);

        RectTransform lineRect = line.GetComponent<RectTransform>();
        lineRect.anchorMin = new Vector2(0.5f, 0.5f);
        lineRect.anchorMax = new Vector2(0.5f, 0.5f);
        lineRect.pivot = new Vector2(0.5f, 0.5f);
        lineRect.sizeDelta = vertical ? new Vector2(lineSize, boardSize) : new Vector2(boardSize, lineSize);
        lineRect.anchoredPosition = vertical ? new Vector2(anchoredAxisPosition, 0f) : new Vector2(0f, anchoredAxisPosition);

        Image image = line.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;

        line.transform.SetAsLastSibling();
    }

    private float GetLineSize(int lineIndex)
    {
        if (lineIndex == 0 || lineIndex == 9)
        {
            return outerLineSize;
        }

        return lineIndex % 3 == 0 ? thickLineSize : thinLineSize;
    }

    private Color GetLineColor(int lineIndex)
    {
        if (lineIndex == 0 || lineIndex == 9)
        {
            return outerLineColor;
        }

        return lineIndex % 3 == 0 ? thickLineColor : thinLineColor;
    }

    private GridLineType GetLineType(int lineIndex)
    {
        if (lineIndex == 0 || lineIndex == 9)
        {
            return GridLineType.Outer;
        }

        return lineIndex % 3 == 0 ? GridLineType.Thick : GridLineType.Thin;
    }

    private float GetLineCenterPosition(int lineIndex)
    {
        float position = -boardSize * 0.5f;

        for (int line = 0; line < lineIndex; line++)
        {
            position += GetLineSize(line) + cellSize;
        }

        return position + GetLineSize(lineIndex) * 0.5f;
    }

    private float GetCellCenterPosition(int index)
    {
        float position = -boardSize * 0.5f + outerLineSize + cellSize * 0.5f;

        for (int i = 0; i < index; i++)
        {
            position += cellSize + GetGapAfterIndex(i);
        }

        return position;
    }

    private float GetGapAfterIndex(int index)
    {
        if (index < 0)
        {
            return 0f;
        }

        return (index + 1) % 3 == 0 ? thickLineSize : thinLineSize;
    }

    private void CacheRectTransform()
    {
        if (rectTransform == null)
        {
            rectTransform = GetComponent<RectTransform>();
        }
    }

    private enum GridLineType
    {
        Thin,
        Thick,
        Outer
    }
}
