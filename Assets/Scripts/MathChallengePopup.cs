using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public enum MathChallengeExitReason
{
    Solved,
    Quit
}

[DisallowMultipleComponent]
[AddComponentMenu("UI/Math Challenge Popup")]
public sealed class MathChallengePopup : MonoBehaviour
{
    private const string OpenAnimationName = "Math_open";
    private const string PanelTextName = "Panel_Text";
    private const string AnswerPadName = "Math_an";
    private const string AnswerDisplayName = "Answer_Input";
    private const string AnswerPlaceholderTextName = "TextEnter";
    private const string AnswerValueTextName = "Answer_Text";
    private const string SubmitButtonName = "Button_Su";
    private const string QuitButtonName = "Button_Quit";
    private const string AnswerPlaceholder = "Enter your answer";
    private const int AnswerCharacterLimit = 4;

    [SerializeField] private TMP_Text questionText;
    [SerializeField] private TMP_InputField answerInputField;
    [SerializeField] private TMP_Text answerPlaceholderText;
    [SerializeField] private TMP_Text answerValueText;
    [SerializeField] private Button submitButton;
    [SerializeField] private Button quitButton;
    [SerializeField] private Color placeholderColor = new Color(0.75f, 0.75f, 0.75f, 1f);
    [SerializeField, Min(1)] private int minOperand = 1;
    [SerializeField, Min(2)] private int maxOperand = 20;

    private Animator animator;
    private int expectedAnswer;
    private string currentAnswer = string.Empty;
    private Action<MathChallengeExitReason> completedCallback;
    private bool initialized;
    private bool isUpdatingAnswerInput;

    public bool IsOpen { get; private set; }

    private void Awake()
    {
        Initialize(true);
    }

    private void Update()
    {
        if (!IsOpen)
        {
            return;
        }

        if (answerInputField != null && answerInputField.isFocused)
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                QuitChallenge();
            }

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
        else if (Input.GetKeyDown(KeyCode.Escape))
        {
            QuitChallenge();
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

        RefreshAnswerText();

        if (hideWhenInitialized)
        {
            HideImmediate();
        }
    }

    public void Show(Action<MathChallengeExitReason> onCompleted)
    {
        Initialize(false);

        completedCallback = onCompleted;
        currentAnswer = string.Empty;
        GenerateQuestion();
        SetAnswerInputTextWithoutNotify(currentAnswer);
        RefreshAnswerText();

        gameObject.SetActive(true);
        IsOpen = true;
        ActivateAnswerInput();

        if (animator != null)
        {
            animator.Play(OpenAnimationName, 0, 0f);
        }
    }

    public void Show(Action onCompleted)
    {
        Show(reason =>
        {
            if (reason == MathChallengeExitReason.Solved)
            {
                onCompleted?.Invoke();
            }
        });
    }

    public void SetAnswerText(string answer)
    {
        string sanitizedAnswer = SanitizeAnswer(answer);
        currentAnswer = sanitizedAnswer;
        SetAnswerInputTextWithoutNotify(currentAnswer);
        RefreshAnswerText();
    }

    public void AppendAnswerDigit(int digit)
    {
        if (digit < 0 || digit > 9 || currentAnswer.Length >= AnswerCharacterLimit)
        {
            return;
        }

        currentAnswer += digit.ToString();
        SetAnswerInputTextWithoutNotify(currentAnswer);
        RefreshAnswerText();
    }

    public void BackspaceAnswer()
    {
        if (currentAnswer.Length == 0)
        {
            return;
        }

        currentAnswer = currentAnswer.Substring(0, currentAnswer.Length - 1);
        SetAnswerInputTextWithoutNotify(currentAnswer);
        RefreshAnswerText();
    }

    public void SubmitAnswer()
    {
        string submittedAnswer = currentAnswer.Trim();
        if (!int.TryParse(submittedAnswer, out int value) || value != expectedAnswer)
        {
            currentAnswer = string.Empty;
            SetAnswerInputTextWithoutNotify(currentAnswer);
            RefreshAnswerText();
            ActivateAnswerInput();
            return;
        }

        Complete(MathChallengeExitReason.Solved);
    }

    public void QuitChallenge()
    {
        Complete(MathChallengeExitReason.Quit);
    }

    public void Hide()
    {
        completedCallback = null;
        HideImmediate();
    }

    private void Complete(MathChallengeExitReason reason)
    {
        HideImmediate();
        Action<MathChallengeExitReason> callback = completedCallback;
        completedCallback = null;
        callback?.Invoke(reason);
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

    private void RefreshAnswerText()
    {
        if (answerPlaceholderText == null && answerValueText == null)
        {
            return;
        }

        bool hasAnswer = !string.IsNullOrEmpty(currentAnswer);
        if (answerInputField == null && answerPlaceholderText != null)
        {
            answerPlaceholderText.gameObject.SetActive(!hasAnswer);
            answerPlaceholderText.text = AnswerPlaceholder;
            answerPlaceholderText.color = placeholderColor;
        }

        if (answerValueText != null)
        {
            answerValueText.gameObject.SetActive(answerInputField != null || hasAnswer);
            answerValueText.text = hasAnswer ? currentAnswer : string.Empty;
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
            submitButton = FindDeepChild(transform, SubmitButtonName)?.GetComponent<Button>();
        }

        if (quitButton == null)
        {
            quitButton = FindDeepChild(transform, QuitButtonName)?.GetComponent<Button>();
        }

        Transform panelText = FindDeepChild(transform, PanelTextName);
        if (panelText != null && questionText == null)
        {
            questionText = FindQuestionText(panelText);
        }

        if (questionText == null && panelText != null)
        {
            questionText = CreateQuestionText(panelText);
        }

        if (answerPlaceholderText == null)
        {
            answerPlaceholderText = FindAnswerPlaceholderText(transform);
        }

        if (answerValueText == null)
        {
            answerValueText = FindOrCreateAnswerValueText(transform, answerPlaceholderText);
        }

        if (answerInputField == null)
        {
            answerInputField = FindOrCreateAnswerInputField(transform, answerPlaceholderText, answerValueText);
        }

        ConfigureAnswerInputField();
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

    public static TMP_Text FindQuestionText(Transform panelText)
    {
        if (panelText == null)
        {
            return null;
        }

        TMP_Text namedText = FindDeepChild(panelText, "Question_Text")?.GetComponent<TMP_Text>();
        if (namedText != null)
        {
            return namedText;
        }

        TMP_Text[] texts = panelText.GetComponentsInChildren<TMP_Text>(true);
        return texts.Length > 0 ? texts[0] : null;
    }

    public static TMP_Text FindAnswerPlaceholderText(Transform root)
    {
        if (root == null)
        {
            return null;
        }

        TMP_Text namedText = FindDeepChild(root, AnswerPlaceholderTextName)?.GetComponent<TMP_Text>();
        if (namedText != null)
        {
            return namedText;
        }

        Transform answerDisplay = FindDeepChild(root, AnswerDisplayName);
        return answerDisplay != null ? answerDisplay.GetComponentInChildren<TMP_Text>(true) : null;
    }

    public static TMP_InputField FindOrCreateAnswerInputField(Transform root, TMP_Text placeholderText, TMP_Text valueText)
    {
        if (root == null)
        {
            return null;
        }

        Transform answerDisplay = FindDeepChild(root, AnswerDisplayName);
        if (answerDisplay == null)
        {
            return null;
        }

        TMP_InputField inputField = answerDisplay.GetComponent<TMP_InputField>();
        if (inputField == null)
        {
            inputField = answerDisplay.gameObject.AddComponent<TMP_InputField>();
        }

        Image targetImage = answerDisplay.GetComponent<Image>();
        if (targetImage == null)
        {
            targetImage = answerDisplay.gameObject.AddComponent<Image>();
            targetImage.color = new Color(1f, 1f, 1f, 0f);
        }

        targetImage.raycastTarget = true;
        inputField.targetGraphic = targetImage;
        inputField.placeholder = placeholderText;
        inputField.textComponent = valueText;
        return inputField;
    }

    public static TMP_Text FindOrCreateAnswerValueText(Transform root, TMP_Text styleSource)
    {
        if (root == null)
        {
            return null;
        }

        TMP_Text existingText = FindDeepChild(root, AnswerValueTextName)?.GetComponent<TMP_Text>();
        if (existingText != null)
        {
            return existingText;
        }

        Transform answerDisplay = FindDeepChild(root, AnswerDisplayName);
        if (answerDisplay == null)
        {
            return null;
        }

        GameObject answerObject = new GameObject(AnswerValueTextName, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        answerObject.transform.SetParent(answerDisplay, false);
        TMP_Text answerText = answerObject.GetComponent<TMP_Text>();
        ConfigureAnswerValueText(answerText, styleSource);
        answerText.gameObject.SetActive(false);
        return answerText;
    }

    private static void ConfigureAnswerValueText(TMP_Text answerText, TMP_Text styleSource)
    {
        RectTransform rectTransform = answerText.rectTransform;
        rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
        rectTransform.anchoredPosition = styleSource != null
            ? styleSource.rectTransform.anchoredPosition
            : new Vector2(-51f, 0f);
        rectTransform.sizeDelta = styleSource != null
            ? styleSource.rectTransform.sizeDelta
            : new Vector2(200f, 50f);

        if (styleSource != null)
        {
            answerText.font = styleSource.font;
            answerText.fontSharedMaterial = styleSource.fontSharedMaterial;
            answerText.fontSize = styleSource.fontSize;
            answerText.fontStyle = styleSource.fontStyle;
            answerText.alignment = styleSource.alignment;
            answerText.enableAutoSizing = styleSource.enableAutoSizing;
        }
        else
        {
            answerText.fontSize = 24f;
            answerText.alignment = TextAlignmentOptions.Center;
        }

        answerText.text = string.Empty;
        answerText.raycastTarget = true;
    }

    private void ConfigureAnswerInputField()
    {
        if (answerInputField == null)
        {
            return;
        }

        answerInputField.contentType = TMP_InputField.ContentType.IntegerNumber;
        answerInputField.keyboardType = TouchScreenKeyboardType.NumberPad;
        answerInputField.characterLimit = AnswerCharacterLimit;
        answerInputField.lineType = TMP_InputField.LineType.SingleLine;
        answerInputField.shouldHideMobileInput = false;
        answerInputField.onValueChanged.RemoveListener(HandleAnswerInputChanged);
        answerInputField.onSubmit.RemoveListener(HandleAnswerInputSubmitted);
        answerInputField.onValueChanged.AddListener(HandleAnswerInputChanged);
        answerInputField.onSubmit.AddListener(HandleAnswerInputSubmitted);

        if (answerPlaceholderText != null)
        {
            answerPlaceholderText.text = AnswerPlaceholder;
            answerPlaceholderText.color = placeholderColor;
            answerPlaceholderText.raycastTarget = true;
        }

        if (answerValueText != null)
        {
            answerValueText.gameObject.SetActive(true);
            answerValueText.raycastTarget = true;
        }

        SetAnswerInputTextWithoutNotify(currentAnswer);
    }

    private void HandleAnswerInputChanged(string answer)
    {
        if (isUpdatingAnswerInput)
        {
            return;
        }

        string sanitizedAnswer = SanitizeAnswer(answer);
        currentAnswer = sanitizedAnswer;
        if (answer != sanitizedAnswer)
        {
            SetAnswerInputTextWithoutNotify(sanitizedAnswer);
        }

        RefreshAnswerText();
    }

    private void HandleAnswerInputSubmitted(string answer)
    {
        SetAnswerText(answer);
        SubmitAnswer();
    }

    private void SetAnswerInputTextWithoutNotify(string answer)
    {
        if (answerInputField == null)
        {
            return;
        }

        isUpdatingAnswerInput = true;
        answerInputField.SetTextWithoutNotify(answer ?? string.Empty);
        answerInputField.ForceLabelUpdate();
        isUpdatingAnswerInput = false;
    }

    private void ActivateAnswerInput()
    {
        if (answerInputField == null || !isActiveAndEnabled)
        {
            return;
        }

        answerInputField.ActivateInputField();
    }

    private static string SanitizeAnswer(string answer)
    {
        if (string.IsNullOrWhiteSpace(answer))
        {
            return string.Empty;
        }

        char[] digits = new char[Mathf.Min(answer.Length, AnswerCharacterLimit)];
        int digitCount = 0;
        for (int index = 0; index < answer.Length && digitCount < AnswerCharacterLimit; index++)
        {
            char character = answer[index];
            if (character >= '0' && character <= '9')
            {
                digits[digitCount] = character;
                digitCount++;
            }
        }

        return digitCount == 0 ? string.Empty : new string(digits, 0, digitCount);
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

    private void BindButtons()
    {
        if (submitButton != null)
        {
            submitButton.onClick.AddListener(SubmitAnswer);
        }

        if (quitButton != null)
        {
            quitButton.onClick.AddListener(QuitChallenge);
        }

        Transform answerPad = FindDeepChild(transform, AnswerPadName);
        if (answerPad == null)
        {
            return;
        }

        foreach (Button button in answerPad.GetComponentsInChildren<Button>(true))
        {
            if (button == submitButton || button == quitButton)
            {
                continue;
            }

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
}
