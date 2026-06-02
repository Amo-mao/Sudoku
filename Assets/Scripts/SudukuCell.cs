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
    [SerializeField] private Color fixedNumberColor = new Color(0.16f, 0.16f, 0.16f, 1f);
    [SerializeField] private Color editableNumberColor = new Color(0.08f, 0.28f, 0.75f, 1f);

    public int Row { get; private set; }
    public int Column { get; private set; }
    public int Value { get; private set; }
    public bool IsFixed { get; private set; }

    public void Initialize(int row, int column)
    {
        Row = row;
        Column = column;
        SetValue(0, false);
        SetBackgroundColor(normalColor);
    }

    public void SetValue(int value, bool isFixed)
    {
        Value = Mathf.Clamp(value, 0, 9);
        IsFixed = isFixed;

        if (valueText == null)
        {
            valueText = GetComponentInChildren<TMP_Text>(true);
        }

        if (valueText == null)
        {
            return;
        }

        valueText.text = Value == 0 ? string.Empty : Value.ToString();
        valueText.color = IsFixed ? fixedNumberColor : editableNumberColor;
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
}
