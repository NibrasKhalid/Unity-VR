using UnityEngine;
using TMPro;
using System.Reflection;
using System.Collections.Generic;

public class DropDown : MonoBehaviour
{
    [SerializeField] private float popupHeight = 150f;

    [Header("Answer Settings")]
    [SerializeField] private int correctIndex = 0;
    [SerializeField] private Color correctColor = Color.green;
    [SerializeField] private Color wrongColor = Color.red;
    [SerializeField] private Color defaultColor = Color.white;

    private TMP_Dropdown dropdown;
    private FieldInfo dropdownListField;

    void Awake()
    {
        dropdown = GetComponent<TMP_Dropdown>();

        dropdownListField = typeof(TMP_Dropdown).GetField(
            "m_Dropdown",
            BindingFlags.NonPublic | BindingFlags.Instance);

        if (dropdownListField == null)
            Debug.LogError("DropDown: could not find m_Dropdown field via reflection — TMP version mismatch?");
    }

    void LateUpdate()
    {
        if (dropdown == null || !dropdown.IsExpanded || dropdownListField == null) return;

        GameObject listGO = dropdownListField.GetValue(dropdown) as GameObject;
        if (listGO == null) return;

        RectTransform templateRect = listGO.transform as RectTransform;
        if (templateRect == null) return;

        if (templateRect.parent != transform)
            templateRect.SetParent(transform, false);

        templateRect.localRotation = Quaternion.identity;
        templateRect.localScale = Vector3.one;

        templateRect.anchorMin = new Vector2(0f, 0f);
        templateRect.anchorMax = new Vector2(1f, 0f);
        templateRect.pivot = new Vector2(0.5f, 1f);

        templateRect.offsetMax = new Vector2(0f, 0f);
        templateRect.offsetMin = new Vector2(0f, -popupHeight);
    }

    public void DropdownSample(int index)
    {
        bool isCorrect = index == correctIndex;

        // Color the closed caption text (no text changes, just color)
        dropdown.captionText.color = isCorrect ? correctColor : wrongColor;

        if (!isCorrect)
        {
            // Reopen the list so the user sees both their wrong pick and the right one
            dropdown.Show();
            ColorItems(index);
        }
    }

    private void ColorItems(int wrongIndex)
    {
        if (dropdownListField == null) return;

        GameObject listGO = dropdownListField.GetValue(dropdown) as GameObject;
        if (listGO == null) return;

        // Find all item labels inside the list (each item's TMP_Text child)
        List<TMP_Text> itemTexts = new List<TMP_Text>();
        foreach (Transform child in listGO.GetComponentInChildren<TMP_Text>().transform.parent.parent)
        {
            TMP_Text t = child.GetComponentInChildren<TMP_Text>();
            if (t != null) itemTexts.Add(t);
        }

        for (int i = 0; i < itemTexts.Count; i++)
        {
            if (i == correctIndex)
                itemTexts[i].color = correctColor;
            else if (i == wrongIndex)
                itemTexts[i].color = wrongColor;
            else
                itemTexts[i].color = defaultColor;
        }
    }
}