using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
[AddComponentMenu("UI/Math Challenge Popup")]
public sealed class MathChallengePopup : MonoBehaviour
{
    private const string OpenAnimationName = "Math_open";
    private const string PanelTextName = "Panel_Text";
    private const string AnswerPadName = "Math_an";
    private const string SubmitButtonName = "Button_Su";
    private const int AnswerCharacterLimit = 4;

    [SerializeField] private TMP_Text questionText;
    [SerializeField] private TMP_InputField answerInput;
    [SerializeField] private Button submitButton;
    [SerializeField] private bool createMissingInputAtRuntime = true;
    [SerializeField, Min(1)] private int minOperand = 1;
    [SerializeField, Min(2)] private int maxOperand = 20;

    private Animator animator;
    private int expectedAnswer;
    private string currentAnswer = string.Empty;
    private Action completedCallback;
    private bool initialized;

    public bool IsOpen { get; private set; }

    private void Awake()
    {
        Initialize(true);
    }

    private void Update()
    {
        if (!IsOpen || answerInput == null || answerInput.isFocused)
        {
            return;
        }

        for (int digit = 0; digit <= 9; digit++)
        {
            KeyCode alphaKey = (KeyCode)((int)KeyCode.Alpha0 + digit);
            KeyCode keypadKey = (KeyCode)((int)KeyCode.Keypad0 + digit);
            if (Input.GetKeyDown(alphaKey) || Input.GetKeyDown(keypadKey))
            {
                AppendAnswerDigit(digit);
                return;
            }
        }

        if (Input.GetKeyDown(KeyCode.Backspace) || Input.GetKeyDown(KeyCode.Delete))
        {
            BackspaceAnswer();
        }
        else if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
        {
            SubmitAnswer();
        }
    }

    public void Initialize(bool hideWhenInitialized)
    {
        if (!initialized)
        {
            CacheReferences();
            BindButtons();
            initialized = true;
        }

        if (hideWhenInitialized)
        {
            HideImmediate();
        }
    }

    public void Show(Action onCompleted)
    {
        Initialize(false);

        completedCallback = onCompleted;
        currentAnswer = string.Empty;
        GenerateQuestion();

        gameObject.SetActive(true);
        IsOpen = true;

        if (answerInput != null)
        {
            answerInput.SetTextWithoutNotify(string.Empty);
            FocusAnswerInput();
        }

        if (animator != null)
        {
            animator.Play(OpenAnimationName, 0, 0f);
        }
    }

    public void SetAnswerText(string answer)
    {
        currentAnswer = answer == null ? string.Empty : answer.Trim();
    }

    public void AppendAnswerDigit(int digit)
    {
        if (digit < 0 || digit > 9)
        {
            return;
        }

        currentAnswer += digit.ToString();
        ApplyCurrentAnswerToInput();
    }

    public void BackspaceAnswer()
    {
        if (currentAnswer.Length == 0)
        {
            return;
        }

        currentAnswer = currentAnswer.Substring(0, currentAnswer.Length - 1);
        ApplyCurrentAnswerToInput();
    }

    public void SubmitAnswer()
    {
        string submittedAnswer = answerInput != null ? answerInput.text.Trim() : currentAnswer.Trim();
        if (!int.TryParse(submittedAnswer, out int value) || value != expectedAnswer)
        {
            currentAnswer = string.Empty;
            if (answerInput != null)
            {
                answerInput.SetTextWithoutNotify(string.Empty);
                FocusAnswerInput();
            }

            return;
        }

        HideImmediate();
        Action callback = completedCallback;
        completedCallback = null;
        callback?.Invoke();
    }

    private void HideImmediate()
    {
        IsOpen = false;
        gameObject.SetActive(false);
    }

    private void GenerateQuestion()
    {
        int left = UnityEngine.Random.Range(minOperand, maxOperand + 1);
        int right = UnityEngine.Random.Range(minOperand, maxOperand + 1);
        int operation = UnityEngine.Random.Range(0, 3);

        switch (operation)
        {
            case 0:
                expectedAnswer = left + right;
                SetQuestion($"{left} + {right} = ?");
                break;
            case 1:
                if (right > left)
                {
                    int temp = left;
                    left = right;
                    right = temp;
                }

                expectedAnswer = left - right;
                SetQuestion($"{left} - {right} = ?");
                break;
            default:
                left = UnityEngine.Random.Range(2, 10);
                right = UnityEngine.Random.Range(2, 10);
                expectedAnswer = left * right;
                SetQuestion($"{left} x {right} = ?");
                break;
        }
    }

    private void SetQuestion(string question)
    {
        if (questionText != null)
        {
            questionText.text = question;
        }
    }

    private void ApplyCurrentAnswerToInput()
    {
        if (answerInput == null)
        {
            return;
        }

        answerInput.SetTextWithoutNotify(currentAnswer);
        answerInput.caretPosition = answerInput.text.Length;
        FocusAnswerInput();
    }

    private void FocusAnswerInput()
    {
        if (answerInput == null)
        {
            return;
        }

        answerInput.Select();
        answerInput.ActivateInputField();
        StartCoroutine(FocusAnswerInputNextFrame());
    }

    private IEnumerator FocusAnswerInputNextFrame()
    {
        yield return null;

        if (IsOpen && answerInput != null)
        {
            answerInput.Select();
            answerInput.ActivateInputField();
            answerInput.caretPosition = answerInput.text.Length;
        }
    }

    private void CacheReferences()
    {
        if (animator == null)
        {
            animator = GetComponent<Animator>();
        }

        if (submitButton == null)
        {
            Transform submitTransform = FindDeepChild(transform, SubmitButtonName);
            if (submitTransform != null)
            {
                submitButton = submitTransform.GetComponent<Button>();
            }
        }

        if (answerInput == null)
        {
            answerInput = GetComponentInChildren<TMP_InputField>(true);
        }

        Transform panelText = FindDeepChild(transform, PanelTextName);
        if (panelText != null)
        {
            if (questionText == null)
            {
                TMP_Text[] texts = panelText.GetComponentsInChildren<TMP_Text>(true);
                if (texts.Length > 0)
                {
                    questionText = texts[0];
                }
            }

            EnsurePanelTextContent(panelText);
        }

        ConfigureAnswerInput();
    }

    private void EnsurePanelTextContent(Transform panelText)
    {
        if (questionText == null)
        {
            questionText = CreateQuestionText(panelText);
        }

        if (answerInput == null)
        {
            answerInput = FindAnswerInput(panelText);
        }

        if (answerInput == null && createMissingInputAtRuntime)
        {
            answerInput = CreateAnswerInput(panelText, questionText);
        }
    }

    private void ConfigureAnswerInput()
    {
        if (answerInput == null)
        {
            return;
        }

        answerInput.contentType = TMP_InputField.ContentType.IntegerNumber;
        answerInput.lineType = TMP_InputField.LineType.SingleLine;
        answerInput.characterLimit = AnswerCharacterLimit;
        answerInput.keyboardType = TouchScreenKeyboardType.NumberPad;
        answerInput.customCaretColor = true;
        answerInput.caretColor = new Color(0.08f, 0.28f, 0.75f, 1f);
        answerInput.caretWidth = 3;
        answerInput.selectionColor = new Color(0.62f, 0.82f, 1f, 0.45f);
        if (answerInput.textComponent != null)
        {
            answerInput.textComponent.alignment = TextAlignmentOptions.Center;
        }
    }

    private static TMP_Text CreatePanelText(Transform parent, string name, Vector2 anchoredPosition, float fontSize)
    {
        GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(parent, false);

        RectTransform rectTransform = textObject.GetComponent<RectTransform>();
        rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
        rectTransform.anchoredPosition = anchoredPosition;
        rectTransform.sizeDelta = new Vector2(460f, 70f);

        TMP_Text text = textObject.GetComponent<TMP_Text>();
        text.alignment = TextAlignmentOptions.Center;
        text.fontSize = fontSize;
        text.color = new Color(0.16f, 0.16f, 0.16f, 1f);
        text.raycastTarget = false;
        return text;
    }

    public static TMP_InputField CreateAnswerInput(Transform parent, TMP_Text styleSource)
    {
        TMP_InputField existingInput = FindAnswerInput(parent);
        if (existingInput != null)
        {
            return existingInput;
        }

        GameObject inputObject = new GameObject("Answer_Input", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(TMP_InputField));
        inputObject.transform.SetParent(parent, false);

        RectTransform inputRect = inputObject.GetComponent<RectTransform>();
        inputRect.anchorMin = new Vector2(0.5f, 0.5f);
        inputRect.anchorMax = new Vector2(0.5f, 0.5f);
        inputRect.pivot = new Vector2(0.5f, 0.5f);
        inputRect.anchoredPosition = new Vector2(0f, -58f);
        inputRect.sizeDelta = new Vector2(300f, 76f);

        Image background = inputObject.GetComponent<Image>();
        background.color = new Color(1f, 1f, 1f, 0.05f);

        GameObject textAreaObject = new GameObject("Text Area", typeof(RectTransform), typeof(RectMask2D));
        textAreaObject.transform.SetParent(inputObject.transform, false);
        RectTransform textAreaRect = textAreaObject.GetComponent<RectTransform>();
        textAreaRect.anchorMin = Vector2.zero;
        textAreaRect.anchorMax = Vector2.one;
        textAreaRect.pivot = new Vector2(0.5f, 0.5f);
        textAreaRect.anchoredPosition = Vector2.zero;
        textAreaRect.sizeDelta = new Vector2(-24f, -12f);

        GameObject underlineObject = new GameObject("Underline", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        underlineObject.transform.SetParent(inputObject.transform, false);
        RectTransform underlineRect = underlineObject.GetComponent<RectTransform>();
        underlineRect.anchorMin = new Vector2(0f, 0f);
        underlineRect.anchorMax = new Vector2(1f, 0f);
        underlineRect.pivot = new Vector2(0.5f, 0f);
        underlineRect.anchoredPosition = Vector2.zero;
        underlineRect.sizeDelta = new Vector2(0f, 4f);

        Image underline = underlineObject.GetComponent<Image>();
        underline.color = new Color(0.08f, 0.28f, 0.75f, 0.75f);
        underline.raycastTarget = false;

        TMP_Text placeholder = CreateInputText(textAreaObject.transform, "Placeholder", styleSource, 34f);
        placeholder.text = "Tap to answer";
        placeholder.color = new Color(0.16f, 0.16f, 0.16f, 0.42f);

        TMP_Text inputText = CreateInputText(textAreaObject.transform, "Text", styleSource, 38f);
        inputText.text = string.Empty;
        inputText.color = new Color(0.16f, 0.16f, 0.16f, 1f);

        TMP_InputField inputField = inputObject.GetComponent<TMP_InputField>();
        inputField.targetGraphic = background;
        inputField.textViewport = textAreaRect;
        inputField.textComponent = inputText;
        inputField.placeholder = placeholder;
        inputField.contentType = TMP_InputField.ContentType.IntegerNumber;
        inputField.lineType = TMP_InputField.LineType.SingleLine;
        inputField.characterLimit = AnswerCharacterLimit;
        inputField.keyboardType = TouchScreenKeyboardType.NumberPad;
        inputField.customCaretColor = true;
        inputField.caretColor = new Color(0.08f, 0.28f, 0.75f, 1f);
        inputField.caretWidth = 3;
        inputField.selectionColor = new Color(0.62f, 0.82f, 1f, 0.45f);
        return inputField;
    }

    public static void ApplyDefaultInputHintStyle(TMP_InputField inputField)
    {
        if (inputField == null)
        {
            return;
        }

        if (inputField.targetGraphic is Image background)
        {
            background.color = new Color(1f, 1f, 1f, 0.05f);
        }

        inputField.customCaretColor = true;
        inputField.caretColor = new Color(0.08f, 0.28f, 0.75f, 1f);
        inputField.caretWidth = 3;
        inputField.selectionColor = new Color(0.62f, 0.82f, 1f, 0.45f);

        if (inputField.placeholder is TMP_Text placeholder)
        {
            placeholder.text = "Tap to answer";
            placeholder.color = new Color(0.16f, 0.16f, 0.16f, 0.42f);
        }

        EnsureUnderline(inputField.transform);
    }

    private static void EnsureUnderline(Transform inputTransform)
    {
        Transform existingUnderline = inputTransform.Find("Underline");
        if (existingUnderline != null)
        {
            return;
        }

        GameObject underlineObject = new GameObject("Underline", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        underlineObject.transform.SetParent(inputTransform, false);
        RectTransform underlineRect = underlineObject.GetComponent<RectTransform>();
        underlineRect.anchorMin = new Vector2(0f, 0f);
        underlineRect.anchorMax = new Vector2(1f, 0f);
        underlineRect.pivot = new Vector2(0.5f, 0f);
        underlineRect.anchoredPosition = Vector2.zero;
        underlineRect.sizeDelta = new Vector2(0f, 4f);

        Image underline = underlineObject.GetComponent<Image>();
        underline.color = new Color(0.08f, 0.28f, 0.75f, 0.75f);
        underline.raycastTarget = false;
    }

    public static TMP_Text CreateQuestionText(Transform parent)
    {
        TMP_Text existingText = FindQuestionText(parent);
        return existingText != null ? existingText : CreatePanelText(parent, "Question_Text", new Vector2(0f, 48f), 50f);
    }

    public static Transform FindPanelText(Transform root)
    {
        return FindDeepChild(root, PanelTextName);
    }

    public static TMP_InputField FindAnswerInput(Transform parent)
    {
        if (parent == null)
        {
            return null;
        }

        Transform existingInput = FindDeepChild(parent, "Answer_Input");
        if (existingInput != null && existingInput.TryGetComponent(out TMP_InputField inputField))
        {
            return inputField;
        }

        return parent.GetComponentInChildren<TMP_InputField>(true);
    }

    public static TMP_Text FindQuestionText(Transform panelText)
    {
        if (panelText == null)
        {
            return null;
        }

        TMP_Text[] texts = panelText.GetComponentsInChildren<TMP_Text>(true);
        foreach (TMP_Text text in texts)
        {
            if (text.GetComponentInParent<TMP_InputField>(true) == null)
            {
                return text;
            }
        }

        return null;
    }

    private static TMP_Text CreateInputText(Transform parent, string name, TMP_Text styleSource, float fontSize)
    {
        GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(parent, false);

        RectTransform rectTransform = textObject.GetComponent<RectTransform>();
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
        rectTransform.anchoredPosition = Vector2.zero;
        rectTransform.sizeDelta = Vector2.zero;

        TMP_Text text = textObject.GetComponent<TMP_Text>();
        if (styleSource != null)
        {
            text.font = styleSource.font;
            text.fontSharedMaterial = styleSource.fontSharedMaterial;
        }

        text.alignment = TextAlignmentOptions.Center;
        text.fontSize = fontSize;
        text.raycastTarget = false;
        return text;
    }

    private void BindButtons()
    {
        if (submitButton != null)
        {
            submitButton.onClick.AddListener(SubmitAnswer);
        }

        if (answerInput != null)
        {
            answerInput.onValueChanged.AddListener(SetAnswerText);
            answerInput.onSubmit.AddListener(_ => SubmitAnswer());
        }

        Transform answerPad = FindDeepChild(transform, AnswerPadName);
        if (answerPad == null)
        {
            return;
        }

        foreach (Button button in answerPad.GetComponentsInChildren<Button>(true))
        {
            TMP_Text text = button.GetComponentInChildren<TMP_Text>(true);
            if (text == null)
            {
                continue;
            }

            string label = text.text.Trim();
            if (int.TryParse(label, out int digit) && digit >= 0 && digit <= 9)
            {
                int capturedDigit = digit;
                button.onClick.AddListener(() => AppendAnswerDigit(capturedDigit));
            }
            else if (string.Equals(label, "delete", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(label, "back", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(label, "clear", StringComparison.OrdinalIgnoreCase))
            {
                button.onClick.AddListener(BackspaceAnswer);
            }
        }
    }

    private static Transform FindDeepChild(Transform parent, string childName)
    {
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
