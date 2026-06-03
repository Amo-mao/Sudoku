using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
[AddComponentMenu("UI/Suduku Cell")]
public sealed class SudukuCell : MonoBehaviour
{
    [SerializeField] private TMP_Text valueText;
    [SerializeField] private Image background;
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color selectedColor = new Color(0.83f, 0.91f, 1f, 1f);
    [SerializeField] private Color fixedNumberColor = new Color(0.16f, 0.16f, 0.16f, 1f);
    [SerializeField] private Color editableNumberColor = new Color(0.08f, 0.28f, 0.75f, 1f);

    public int Row { get; private set; }
    public int Column { get; private set; }
    public int Value { get; private set; }
    public bool IsFixed { get; private set; }
    public bool IsWrong { get; private set; }

    public void Initialize(int row, int column)
    {
        Row = row;
        Column = column;
        SetValue(0, false);
        SetSelected(false);
    }

    public void SetValue(int value, bool isFixed)
    {
        SetValue(value, isFixed, false);
    }

    public void SetValue(int value, bool isFixed, bool isWrong)
    {
        Value = Mathf.Clamp(value, 0, 9);
        IsFixed = isFixed;
        IsWrong = Value != 0 && !IsFixed && isWrong;

        if (valueText == null)
        {
            valueText = GetComponentInChildren<TMP_Text>(true);
        }

        if (valueText == null)
        {
            return;
        }

        valueText.text = Value == 0 ? string.Empty : Value.ToString();
        RefreshNumberColor();
    }

    public void SetWrong(bool isWrong)
    {
        IsWrong = Value != 0 && !IsFixed && isWrong;
        RefreshNumberColor();
    }

    public void SetWrong(bool isWrong, Color wrongColor)
    {
        IsWrong = Value != 0 && !IsFixed && isWrong;
        RefreshNumberColor(wrongColor);
    }

    public void SetBackgroundColor(Color color)
    {
        if (background == null)
        {
            background = GetComponent<Image>();
        }

        if (background != null)
        {
            background.color = color;
        }
    }

    public void SetSelected(bool selected)
    {
        SetBackgroundColor(selected ? selectedColor : normalColor);
    }

    private void RefreshNumberColor()
    {
        RefreshNumberColor(new Color(0.85f, 0.12f, 0.12f, 1f));
    }

    private void RefreshNumberColor(Color wrongColor)
    {
        if (valueText == null)
        {
            valueText = GetComponentInChildren<TMP_Text>(true);
        }

        if (valueText == null)
        {
            return;
        }

        if (IsWrong)
        {
            valueText.color = wrongColor;
        }
        else
        {
            valueText.color = IsFixed ? fixedNumberColor : editableNumberColor;
        }
    }
}
