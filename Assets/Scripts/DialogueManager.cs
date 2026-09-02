using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class DialogueManager : MonoBehaviour
{
    public static DialogueManager Instance { get; private set; }

    public static bool IsOpen { get; private set; }

    [Header("MCQ UI References")]
    public GameObject dialoguePanel;
    public TMP_Text headingText;
    public TMP_Text questionText;
    public Button[] answerButtons;      

    [Header("Dropdown UI References")]
    public GameObject dropdownPanel;
    public TMP_Text dropdownHeadingText;
    public TMP_Text dropdownQuestionText;

    [Header("VR Settings")]
    public Transform playerCamera;
    public float panelDistance = 1.5f;

    [Header("Feedback")]
    public Color correctColor = Color.green;
    public Color wrongColor = Color.red;
    public float feedbackDuration = 0.8f;

    private TMP_Text[] answerTexts;
    private Image[] answerImages;
    private Color[] defaultColors;

    private PlayerDetection currentSource;
    private GameObject activePanel; 
    private bool isOpen = false;
    private bool answering = false;

    void Awake()
    {
        // Singleton pattern to ensure only one instance of DialogueManager exists
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        answerTexts = new TMP_Text[answerButtons.Length];
        answerImages = new Image[answerButtons.Length];
        defaultColors = new Color[answerButtons.Length];

        for (int i = 0; i < answerButtons.Length; i++)
        {
            int idx = i;
            answerTexts[i] = answerButtons[i].GetComponentInChildren<TMP_Text>();
            answerImages[i] = answerButtons[i].GetComponent<Image>();
            if (answerImages[i] != null) defaultColors[i] = answerImages[i].color;

            answerButtons[i].onClick.AddListener(() => OnAnswerSelected(idx));
        }
    }

    void Start()
    {
        // Ensure both panels are hidden at the start
        if (dialoguePanel != null) dialoguePanel.SetActive(false);
        if (dropdownPanel != null) dropdownPanel.SetActive(false);
    }

    public void Interact(PlayerDetection source)
    {
        // If the dialogue is already open and the same source is interacting, close it
        if (isOpen && currentSource == source)
        {
            Close();
            return;
        }

        Open(source);
    }

    void Open(PlayerDetection source)
    {
        // close whatever might already be open first
        if (dialoguePanel != null) dialoguePanel.SetActive(false);
        if (dropdownPanel != null) dropdownPanel.SetActive(false);

        currentSource = source;
        answering = false;

        if (source.useDropdown)
        {
            activePanel = dropdownPanel;
            if (dropdownHeadingText != null) dropdownHeadingText.text = source.heading;
            if (dropdownQuestionText != null) dropdownQuestionText.text = source.questionText;
        }
        else
        {
            activePanel = dialoguePanel;
            PopulateContent(source);
        }

        if (activePanel == null)
        {
            Debug.LogError("DialogueManager: panel not assigned for this interaction type!");
            return;
        }

        PositionPanel(activePanel);
        activePanel.SetActive(true);
        isOpen = true;
        IsOpen = true;
    }

    public void Close()
    {
        // Reset the state of the dialogue manager
        if (activePanel != null) activePanel.SetActive(false);
        activePanel = null;
        isOpen = false;
        IsOpen = false;
        currentSource = null;
    }

    void PositionPanel(GameObject panel)
    {
        // Position the panel in front of the player at a fixed distance
        Vector3 flatForward = playerCamera.forward;
        flatForward.y = 0;
        flatForward.Normalize();

        Vector3 spawnPos = playerCamera.position + flatForward * panelDistance;
        spawnPos.y = playerCamera.position.y;

        panel.transform.position = spawnPos;
        panel.transform.rotation = Quaternion.LookRotation(flatForward);
    }

    void PopulateContent(PlayerDetection source)
    {
        // Populate the UI elements with the content from the PlayerDetection source
        if (headingText != null) headingText.text = source.heading;
        if (questionText != null) questionText.text = source.questionText;

        for (int i = 0; i < answerButtons.Length; i++)
        {
            bool hasAnswer = i < source.answers.Length;
            answerButtons[i].gameObject.SetActive(hasAnswer);
            if (!hasAnswer) continue;

            answerTexts[i].text = source.answers[i];
            if (answerImages[i] != null) answerImages[i].color = defaultColors[i];
            answerButtons[i].interactable = true;
        }
    }

    void OnAnswerSelected(int index)
    {
       // Handle the answer selection logic
        if (answering || currentSource == null) return;
        answering = true;

        for (int i = 0; i < answerButtons.Length; i++)
        {
            answerButtons[i].interactable = false;
            if (answerImages[i] == null) continue;

            answerImages[i].color = (i == currentSource.correctAnswerIndex)
                ? correctColor
                : wrongColor;
        }
    }
}