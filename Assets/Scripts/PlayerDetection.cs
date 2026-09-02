using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;
using UnityEngine.XR.Hands;

public class PlayerDetection : MonoBehaviour
{
    private bool playerNearby = false;

    private InputAction interactAction;

    [Header("Hand Gesture Settings")]
    [SerializeField] bool useHandGestures = true;
    [SerializeField] float pinchThreshold = 0.7f;

    [Header("Question / Answers")]
    public string heading = "Question";
    [TextArea(2, 4)]
    public string questionText = "What is a micro-grid used for?";
    public string[] answers = new string[4]
    {
        "Provide sustainable electricity",
        "Store irrigation water",
        "Expand the national grid",
        "Replace electricity meters"
    };
    [Range(0, 3)]
    public int correctAnswerIndex = 0;

    [Header("Panel Type")]
    [Tooltip("If true, this object opens the Dropdown panel instead of the MCQ buttons panel.")]
    public bool useDropdown = false;

    private XRHandSubsystem handSubsystem;
    private bool wasPinchingLastFrame = false;

    void Awake()
    {
        interactAction = new InputAction("Interact");
        interactAction.AddBinding("<Keyboard>/f");
        interactAction.AddBinding("<XRController>{RightHand}/primaryButton");
        interactAction.AddBinding("<XRController>{LeftHand}/primaryButton");
    }

    void OnEnable()  { interactAction.Enable(); }
    void OnDisable() { interactAction.Disable(); }

    void Start()
    {
        var subsystems = new System.Collections.Generic.List<XRHandSubsystem>();
        SubsystemManager.GetSubsystems(subsystems);
        if (subsystems.Count > 0)
            handSubsystem = subsystems[0];
        else
            Debug.LogWarning("No XRHandSubsystem found — hand gestures unavailable.");
    }

    void Update()
    {
        bool interactPressed = interactAction.WasPressedThisFrame();

        if (useHandGestures && handSubsystem != null)
            interactPressed |= DetectPinch();

        if (playerNearby)
            Debug.Log("Player nearby, interact: " + interactPressed);

        if (playerNearby && interactPressed)
            DialogueManager.Instance.Interact(this);
    }

    bool DetectPinch()
    {
        bool isPinching = CheckHandPinch(handSubsystem.rightHand)
                       || CheckHandPinch(handSubsystem.leftHand);

        bool justPinched = isPinching && !wasPinchingLastFrame;
        wasPinchingLastFrame = isPinching;
        return justPinched;
    }

    bool CheckHandPinch(XRHand hand)
    {
        if (!hand.isTracked) return false;

        if (hand.GetJoint(XRHandJointID.ThumbTip).TryGetPose(out Pose thumbTip) &&
            hand.GetJoint(XRHandJointID.IndexTip).TryGetPose(out Pose indexTip))
        {
            float distance = Vector3.Distance(thumbTip.position, indexTip.position);
            return distance < 0.02f;
        }
        return false;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
            playerNearby = true;
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
            playerNearby = false;
    }
}