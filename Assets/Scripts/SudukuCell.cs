using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
[AddComponentMenu("UI/Suduku Cell")]
public sealed class SudukuCell : MonoBehaviour
{
    private const string ShineSweepAnimationName = "Cell_ShineSweep";
    private const int NoteCount = 9;

    [SerializeField] private TMP_Text valueText;
    [SerializeField] private TMP_Text okValueText;
    [SerializeField] private RectTransform notesRoot;
    [SerializeField] private TMP_Text[] noteTexts = new TMP_Text[NoteCount];
    [SerializeField] private Image background;
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color selectedColor = new Color(0.83f, 0.91f, 1f, 1f);
    [SerializeField] private Color fixedNumberColor = new Color(0.16f, 0.16f, 0.16f, 1f);
    [SerializeField] private Color editableNumberColor = new Color(0.08f, 0.28f, 0.75f, 1f);
    [SerializeField] private Color okNumberColor = Color.white;
    [SerializeField] private Color noteNumberColor = new Color(0.28f, 0.36f, 0.48f, 1f);

    public int Row { get; private set; }
    public int Column { get; private set; }
    public int Value { get; private set; }
    public bool IsFixed { get; private set; }
    public bool IsWrong { get; private set; }

    private readonly bool[] notes = new bool[NoteCount + 1];

    private void Awake()
    {
        CacheTextReferences();
        ConfigureNotesLayout();
        ConfigureFeedbackLayout();
        ConfigureRaycastTargets();
    }

    public void Initialize(int row, int column)
    {
        Row = row;
        Column = column;
        CacheTextReferences();
        ConfigureNotesLayout();
        ConfigureFeedbackLayout();
        ConfigureRaycastTargets();
        SetValue(0, false);
        ClearNotes();
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

        CacheTextReferences();

        if (valueText == null)
        {
            return;
        }

        string displayText = Value == 0 ? string.Empty : Value.ToString();
        valueText.text = displayText;
        SyncOkValueText(displayText);
        if (Value != 0)
        {
            ClearNotes();
        }
        else
        {
            RefreshNotes();
        }

        RefreshNumberColor();
    }

    public void ToggleNote(int value)
    {
        if (value < 1 || value > NoteCount || IsFixed || Value != 0)
        {
            return;
        }

        ConfigureNotesLayout();
        notes[value] = !notes[value];
        RefreshNotes();
    }

    public void ClearNotes()
    {
        for (int value = 1; value <= NoteCount; value++)
        {
            notes[value] = false;
        }

        RefreshNotes();
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

    public void PlayShineSweep()
    {
        Animation animation = GetComponent<Animation>();
        if (animation == null)
        {
            return;
        }

        ConfigureFeedbackLayout();
        animation.Stop(ShineSweepAnimationName);
        animation.Play(ShineSweepAnimationName);
    }

    private void RefreshNumberColor()
    {
        RefreshNumberColor(new Color(0.85f, 0.12f, 0.12f, 1f));
    }

    private void RefreshNumberColor(Color wrongColor)
    {
        CacheTextReferences();

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

    private void CacheTextReferences()
    {
        if (valueText == null || valueText.transform.parent != transform)
        {
            Transform directText = transform.Find("Text (TMP)");
            if (directText != null)
            {
                valueText = directText.GetComponent<TMP_Text>();
            }
        }

        if (valueText == null || valueText.transform.parent != transform)
        {
            TMP_Text[] texts = GetComponentsInChildren<TMP_Text>(true);
            foreach (TMP_Text text in texts)
            {
                if (text.transform.parent == transform)
                {
                    valueText = text;
                    break;
                }
            }
        }

        if (okValueText == null)
        {
            Transform okText = transform.Find("Cell_ok_all/Text");
            if (okText != null)
            {
                okValueText = okText.GetComponent<TMP_Text>();
            }
        }

        AlignOkValueText();
    }

    private void ConfigureNotesLayout()
    {
        if (notesRoot == null)
        {
            Transform existingRoot = transform.Find("NotesRoot");
            if (existingRoot != null)
            {
                notesRoot = existingRoot as RectTransform;
            }
        }

        if (notesRoot == null)
        {
            GameObject rootObject = new GameObject("NotesRoot", typeof(RectTransform));
            rootObject.transform.SetParent(transform, false);
            notesRoot = rootObject.GetComponent<RectTransform>();
        }

        StretchToParent(notesRoot);
        notesRoot.SetAsLastSibling();

        if (noteTexts == null || noteTexts.Length != NoteCount)
        {
            noteTexts = new TMP_Text[NoteCount];
        }

        for (int value = 1; value <= NoteCount; value++)
        {
            int index = value - 1;
            if (noteTexts[index] == null)
            {
                Transform existingNote = notesRoot.Find($"Note_{value}");
                if (existingNote != null)
                {
                    noteTexts[index] = existingNote.GetComponent<TMP_Text>();
                }
            }

            if (noteTexts[index] == null)
            {
                noteTexts[index] = CreateNoteText(value);
            }

            ConfigureNoteTransform(noteTexts[index].rectTransform, index);
            ConfigureNoteTextStyle(noteTexts[index]);
        }

        RefreshNotes();
    }

    private TMP_Text CreateNoteText(int value)
    {
        GameObject noteObject = new GameObject($"Note_{value}", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        noteObject.transform.SetParent(notesRoot, false);
        return noteObject.GetComponent<TMP_Text>();
    }

    private void ConfigureNoteTransform(RectTransform rectTransform, int index)
    {
        int row = index / 3;
        int column = index % 3;

        rectTransform.anchorMin = new Vector2(column / 3f, 1f - (row + 1) / 3f);
        rectTransform.anchorMax = new Vector2((column + 1) / 3f, 1f - row / 3f);
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
        rectTransform.anchoredPosition = Vector2.zero;
        rectTransform.sizeDelta = Vector2.zero;
    }

    private void ConfigureNoteTextStyle(TMP_Text noteText)
    {
        noteText.alignment = TextAlignmentOptions.Center;
        noteText.enableAutoSizing = true;
        noteText.fontSizeMin = 8f;
        noteText.fontSizeMax = valueText != null ? Mathf.Max(12f, valueText.fontSize * 0.38f) : 18f;
        noteText.color = noteNumberColor;
        noteText.raycastTarget = false;

        if (valueText != null)
        {
            noteText.font = valueText.font;
            noteText.fontSharedMaterial = valueText.fontSharedMaterial;
        }
    }

    private void RefreshNotes()
    {
        if (notesRoot != null)
        {
            notesRoot.gameObject.SetActive(Value == 0);
        }

        if (noteTexts == null)
        {
            return;
        }

        for (int value = 1; value <= NoteCount; value++)
        {
            int index = value - 1;
            if (index >= noteTexts.Length || noteTexts[index] == null)
            {
                continue;
            }

            noteTexts[index].text = Value == 0 && notes[value] ? value.ToString() : string.Empty;
        }
    }

    private void SyncOkValueText(string displayText)
    {
        if (okValueText == null)
        {
            return;
        }

        okValueText.text = displayText;
        okValueText.color = okNumberColor;
        AlignOkValueText();
    }

    private void AlignOkValueText()
    {
        if (valueText == null || okValueText == null)
        {
            return;
        }

        RectTransform source = valueText.rectTransform;
        RectTransform target = okValueText.rectTransform;
        RectTransform targetParent = target.parent as RectTransform;
        if (source == null || target == null || targetParent == null)
        {
            return;
        }

        target.anchorMin = source.anchorMin;
        target.anchorMax = source.anchorMax;
        target.pivot = source.pivot;
        target.anchoredPosition = source.anchoredPosition;
        target.sizeDelta = source.sizeDelta;
        target.localScale = source.localScale;
        target.localRotation = Quaternion.Inverse(targetParent.rotation) * source.rotation;

        okValueText.font = valueText.font;
        okValueText.fontSize = valueText.fontSize;
        okValueText.fontStyle = valueText.fontStyle;
        okValueText.alignment = valueText.alignment;
        okValueText.enableAutoSizing = valueText.enableAutoSizing;
    }

    private void ConfigureFeedbackLayout()
    {
        RectTransform okAll = transform.Find("Cell_ok_all") as RectTransform;
        if (okAll == null)
        {
            return;
        }

        StretchToParent(okAll);

        RectTransform okCard = okAll.Find("Cell_ok") as RectTransform;
        if (okCard != null)
        {
            StretchToParent(okCard);
        }

        AlignOkValueText();
    }

    private static void StretchToParent(RectTransform rectTransform)
    {
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
        rectTransform.anchoredPosition = Vector2.zero;
        rectTransform.sizeDelta = Vector2.zero;
    }

    private void ConfigureRaycastTargets()
    {
        Image rootImage = GetComponent<Image>();
        if (rootImage != null)
        {
            rootImage.raycastTarget = true;
            background = rootImage;
        }

        Graphic[] childGraphics = GetComponentsInChildren<Graphic>(true);
        foreach (Graphic graphic in childGraphics)
        {
            if (graphic.gameObject == gameObject)
            {
                continue;
            }

            graphic.raycastTarget = false;
        }

        CanvasGroup[] childGroups = GetComponentsInChildren<CanvasGroup>(true);
        foreach (CanvasGroup group in childGroups)
        {
            if (group.gameObject == gameObject)
            {
                continue;
            }

            group.interactable = false;
            group.blocksRaycasts = false;
        }
    }
}
