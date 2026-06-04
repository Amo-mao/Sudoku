using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class MathChallengePopupEditor
{
    private const string MenuPath = "Tools/Sudoku/Create Editable Answer Input";
    private const string SubmitButtonName = "Button_Su";

    [MenuItem(MenuPath, true)]
    private static bool CanCreateEditableAnswerInput()
    {
        return Selection.activeGameObject != null;
    }

    [MenuItem(MenuPath)]
    private static void CreateEditableAnswerInput()
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
            Debug.Log($"Created editable Answer_Input in prefab: {prefabPath}");
            return;
        }

        GameObject root = FindObjectMathRoot(selected);
        Undo.RegisterFullObjectHierarchyUndo(root, "Create Editable Answer Input");
        TMP_InputField inputField = SetupPopup(root);
        Selection.activeGameObject = inputField.gameObject;
        EditorUtility.SetDirty(root);
        Debug.Log("Created editable Answer_Input in the selected Object_Math hierarchy.");
    }

    private static TMP_InputField SetupPopup(GameObject root)
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

        TMP_InputField inputField = MathChallengePopup.CreateAnswerInput(panelText, questionText);
        MathChallengePopup.ApplyDefaultInputHintStyle(inputField);
        Button submitButton = FindDeepChild(root.transform, SubmitButtonName)?.GetComponent<Button>();

        AssignPopupReferences(popup, questionText, inputField, submitButton);
        EditorUtility.SetDirty(popup);
        EditorUtility.SetDirty(inputField);
        return inputField;
    }

    private static void AssignPopupReferences(
        MathChallengePopup popup,
        TMP_Text questionText,
        TMP_InputField inputField,
        Button submitButton)
    {
        SerializedObject serializedPopup = new SerializedObject(popup);
        serializedPopup.FindProperty("questionText").objectReferenceValue = questionText;
        serializedPopup.FindProperty("answerInput").objectReferenceValue = inputField;
        serializedPopup.FindProperty("submitButton").objectReferenceValue = submitButton;
        serializedPopup.FindProperty("createMissingInputAtRuntime").boolValue = false;
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
