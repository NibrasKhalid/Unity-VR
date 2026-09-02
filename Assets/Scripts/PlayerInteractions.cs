using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;
using UnityEngine.XR.Hands;

[RequireComponent(typeof(CharacterController))]
public class PlayerInteractions : MonoBehaviour
{
    public Camera playerCamera;
    public float walkSpeed = 2f;     
    public float runSpeed = 4f;       
    public float gravity = 10f;
    public float lookSpeed = 2f;
    public float defaultHeight = 2f;
    public bool canMove = true;

    [Header("Hand Gesture Movement")]
    public bool useHandGestureMovement = true;
    [Tooltip("Distance from fingertip to palm below which a finger counts as 'curled'.")]
    public float curlThreshold = 0.05f;

    private Vector3 moveDirection = Vector3.zero;
    private CharacterController characterController;

    private float defaultWalkSpeed;
    private float defaultRunSpeed;

    private InputAction moveAction;
    private InputAction lookAction;
    private InputAction runAction;

    private XRHandSubsystem handSubsystem;

    void Awake()
    {
        moveAction = new InputAction("Move", binding: "<Gamepad>/leftStick");
        lookAction = new InputAction("Look", binding: "<Gamepad>/rightStick");
        runAction  = new InputAction("Run",  binding: "<Gamepad>/leftStickPress");

        moveAction.AddCompositeBinding("2DVector")
            .With("Up",    "<Keyboard>/w")
            .With("Down",  "<Keyboard>/s")
            .With("Left",  "<Keyboard>/a")
            .With("Right", "<Keyboard>/d");

        lookAction.AddBinding("<Mouse>/delta");
        runAction.AddBinding("<Keyboard>/leftShift");

        // VR bindings
        moveAction.AddBinding("<XRController>{LeftHand}/thumbstick");
        lookAction.AddBinding("<XRController>{RightHand}/thumbstick");
        runAction.AddBinding("<XRController>{LeftHand}/thumbstickClicked");
    }

    void OnEnable()
    {
        moveAction.Enable();
        lookAction.Enable();
        runAction.Enable();
    }

    void OnDisable()
    {
        moveAction.Disable();
        lookAction.Disable();
        runAction.Disable();
    }

    void Start()
    {
        characterController = GetComponent<CharacterController>();
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        defaultWalkSpeed = walkSpeed;
        defaultRunSpeed  = runSpeed;

        var subsystems = new List<XRHandSubsystem>();
        SubsystemManager.GetSubsystems(subsystems);
        if (subsystems.Count > 0)
            handSubsystem = subsystems[0];
        else
            Debug.LogWarning("PlayerInteractions: No XRHandSubsystem found — hand gesture movement unavailable.");
    }

    void Update()
    {
        // Handle dialogue/dropdown open state FIRST, before any movement runs
        if (DialogueManager.IsOpen)
        {
            if (Cursor.lockState != CursorLockMode.None)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            return; // freeze player movement/look while panel is up
        }
        else if (Cursor.lockState != CursorLockMode.Locked)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        // Use headset direction for movement in VR
        Vector3 camForward = playerCamera.transform.forward;
        Vector3 camRight   = playerCamera.transform.right;

        camForward.y = 0;
        camRight.y   = 0;
        camForward.Normalize();
        camRight.Normalize();

        bool isRunning = runAction.IsPressed();
        float speed = isRunning ? runSpeed : walkSpeed;

        Vector2 moveInput = moveAction.ReadValue<Vector2>();

        // Hand-gesture movement: closed fist on either hand = move forward
        if (useHandGestureMovement && handSubsystem != null)
        {
            bool fistMoving = CheckHandFist(handSubsystem.rightHand)
                            || CheckHandFist(handSubsystem.leftHand);

            if (fistMoving && moveInput == Vector2.zero)
                moveInput = Vector2.up; // forward
        }

        float curSpeedX = canMove ? speed * moveInput.y : 0;
        float curSpeedY = canMove ? speed * moveInput.x : 0;

        float movementDirectionY = moveDirection.y;
        moveDirection = (camForward * curSpeedX) + (camRight * curSpeedY);
        moveDirection.y = movementDirectionY;

        if (characterController.isGrounded)
        {
            moveDirection.y = -0.5f; 
        }
        else
        {
            moveDirection.y -= gravity * Time.deltaTime;
        }

        characterController.Move(moveDirection * Time.deltaTime);

        if (canMove)
        {
            Vector2 lookInput = lookAction.ReadValue<Vector2>();
            transform.rotation *= Quaternion.Euler(0, lookInput.x * lookSpeed, 0);
        }
    }

    bool CheckHandFist(XRHand hand)
    {
        if (!hand.isTracked) return false;

        if (!hand.GetJoint(XRHandJointID.Palm).TryGetPose(out Pose palm))
            return false;

        XRHandJointID[] tips =
        {
            XRHandJointID.IndexTip,
            XRHandJointID.MiddleTip,
            XRHandJointID.RingTip,
            XRHandJointID.LittleTip
        };

        int curledCount = 0;
        foreach (var tipId in tips)
        {
            if (hand.GetJoint(tipId).TryGetPose(out Pose tip))
            {
                float dist = Vector3.Distance(tip.position, palm.position);
                if (dist < curlThreshold) curledCount++;
            }
        }

        // Require most fingers curled to count as a fist (avoids false positives)
        return curledCount >= 3;
    }
}