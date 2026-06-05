using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class MathChallengePopupEditor
{
    private const string MenuPath = "Tools/Sudoku/Bind Math Challenge Popup";
    private const string SubmitButtonName = "Button_Su";
    private const string QuitButtonName = "Button_Quit";

    [MenuItem(MenuPath, true)]
    private static bool CanBindMathChallengePopup()
    {
        return Selection.activeGameObject != null;
    }

    [MenuItem(MenuPath)]
    private static void BindMathChallengePopup()
    {
        GameObject selected = Selection.activeGameObject;
        string prefabPath = AssetDatabase.GetAssetPath(selected);

        if (!string.IsNullOrEmpty(prefabPath) && prefabPath.EndsWith(".prefab"))
        {
            GameObject prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                SetupPopup(prefabRoot);
                PrefabUtility.SaveAsPrefabAsset(prefabRoot, prefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }

            AssetDatabase.Refresh();
            Debug.Log($"Bound MathChallengePopup references in prefab: {prefabPath}");
            return;
        }

        GameObject root = FindObjectMathRoot(selected);
        Undo.RegisterFullObjectHierarchyUndo(root, "Bind Math Challenge Popup");
        SetupPopup(root);
        EditorUtility.SetDirty(root);
        Debug.Log("Bound MathChallengePopup references in the selected Object_Math hierarchy.");
    }

    private static void SetupPopup(GameObject root)
    {
        if (root == null)
        {
            throw new MissingReferenceException("Select Object_Math or one of its children first.");
        }

        Transform panelText = MathChallengePopup.FindPanelText(root.transform);
        if (panelText == null)
        {
            throw new MissingReferenceException("Object_Math needs a child named Panel_Text.");
        }

        MathChallengePopup popup = root.GetComponent<MathChallengePopup>();
        if (popup == null)
        {
            popup = root.AddComponent<MathChallengePopup>();
        }

        TMP_Text questionText = MathChallengePopup.FindQuestionText(panelText);
        if (questionText == null)
        {
            questionText = MathChallengePopup.CreateQuestionText(panelText);
        }

        TMP_Text answerPlaceholderText = MathChallengePopup.FindAnswerPlaceholderText(root.transform);
        if (answerPlaceholderText == null)
        {
            throw new MissingReferenceException("Object_Math needs Answer_Input/TextEnter.");
        }

        TMP_Text answerValueText = MathChallengePopup.FindOrCreateAnswerValueText(root.transform, answerPlaceholderText);
        TMP_InputField answerInputField = MathChallengePopup.FindOrCreateAnswerInputField(root.transform, answerPlaceholderText, answerValueText);
        Button submitButton = FindDeepChild(root.transform, SubmitButtonName)?.GetComponent<Button>();
        Button quitButton = FindDeepChild(root.transform, QuitButtonName)?.GetComponent<Button>();

        AssignPopupReferences(popup, questionText, answerInputField, answerPlaceholderText, answerValueText, submitButton, quitButton);
        EditorUtility.SetDirty(popup);
        if (answerInputField != null)
        {
            EditorUtility.SetDirty(answerInputField);
        }

        if (answerValueText != null)
        {
            EditorUtility.SetDirty(answerValueText);
        }
    }

    private static void AssignPopupReferences(
        MathChallengePopup popup,
        TMP_Text questionText,
        TMP_InputField answerInputField,
        TMP_Text answerPlaceholderText,
        TMP_Text answerValueText,
        Button submitButton,
        Button quitButton)
    {
        SerializedObject serializedPopup = new SerializedObject(popup);
        serializedPopup.FindProperty("questionText").objectReferenceValue = questionText;
        serializedPopup.FindProperty("answerInputField").objectReferenceValue = answerInputField;
        serializedPopup.FindProperty("answerPlaceholderText").objectReferenceValue = answerPlaceholderText;
        serializedPopup.FindProperty("answerValueText").objectReferenceValue = answerValueText;
        serializedPopup.FindProperty("submitButton").objectReferenceValue = submitButton;
        serializedPopup.FindProperty("quitButton").objectReferenceValue = quitButton;
        serializedPopup.ApplyModifiedProperties();
    }

    private static GameObject FindObjectMathRoot(GameObject selected)
    {
        Transform current = selected.transform;
        while (current != null)
        {
            if (current.name == "Object_Math")
            {
                return current.gameObject;
            }

            current = current.parent;
        }

        return selected;
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
