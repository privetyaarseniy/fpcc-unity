using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController), typeof(PlayerInput))]
public class PlayerMovementController : MonoBehaviour
{
    [SerializeField] private PlayerConfig config;

    [Header("Input Actions")]
    [SerializeField] private InputActionReference movement;
    [SerializeField] private InputActionReference sprint;
    [SerializeField] private InputActionReference jump;
    [SerializeField] private InputActionReference crouch;

    // MOVING
    public static bool isMoving { get; private set; } = false;
    public static bool isSprinting { get; private set; } = false;

    // JUMPING
    public static bool isGrounded { get; private set; }
    private float lastJumpAttemptTime;
    private bool isTryingToJump = false;

    // CROUCHING
    public static bool inTransition { get; private set; } = false;
    public static bool isCrouching { get; private set; } = false;

    private float standingHeight;
    private Vector3 standingCenter;

    // GLOBAL
    private CharacterController controller;
    private PlayerInput input;
    private Vector3 playerVelocity = Vector3.zero;
    private float playerYVelocity = 0;


    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        input = GetComponent<PlayerInput>();
        standingHeight = controller.height;
        standingCenter = controller.center;
    }

    private void Start()
    {
        if (config.JumpEnabled)
        {
            jump.action.performed += _ => ProcessJump();
        }
        if (config.CrouchEnabled)
        {
            crouch.action.performed += _ => ProcessCrouch();
        }
    }

    private void Update()
    {
        isGrounded = controller.isGrounded;

        ApplyHorizontalMovement(movement.action.ReadValue<Vector2>());
        ApplyVerticalMovement();
        ApplySlopeSliding();
        controller.Move(playerVelocity * Time.deltaTime);
    }

    private void ApplyHorizontalMovement(Vector2 input)
    {
        Vector3 moveDirection = Vector3.zero;
        moveDirection.x = input.x;
        moveDirection.z = input.y;
        if (moveDirection.x != 0 || moveDirection.z != 0)
        {
            isMoving = true;
        }
        else
        {
            playerVelocity = Vector3.zero;
            isMoving = false;
            return;
        }

        isSprinting = false;
        Vector3 motion;
        if (isCrouching)
        {
            motion = transform.TransformDirection(moveDirection) * config.CrouchSpeed;
        }
        else if (config.SprintEnabled && sprint.action.inProgress && moveDirection.z > 0)
        {
            motion = transform.TransformDirection(moveDirection) * config.SprintSpeed;
            isSprinting = true;
        }
        else
        {
            motion = transform.TransformDirection(moveDirection) * config.WalkSpeed;
        }

        if (!isGrounded)
        {
            motion *= config.MidairMovementModifier;
        }
        playerVelocity = motion;
    }

    private void ProcessJump()
    {
        isTryingToJump = true;
        lastJumpAttemptTime = Time.time;
    }

    private void ApplyJump()
    {
        bool wasTryingToJump = Time.time - lastJumpAttemptTime <= config.JumpBufferTime;
        bool isOrWasTryingToJump = wasTryingToJump || isTryingToJump;
        bool canJump = isGrounded;
        if (isOrWasTryingToJump && canJump)
        {
            playerYVelocity = Mathf.Sqrt(config.JumpHeight * -3f * config.Downforce);
        }
        isTryingToJump = false;
    }

    private void ApplyVerticalMovement()
    {
        ApplyJump();
        playerYVelocity += config.Downforce * Time.deltaTime;
        if (isGrounded && playerYVelocity < -2f)
        {
            playerYVelocity = -2f;
        }
        playerVelocity.y = playerYVelocity;
    }

    private void ApplySlopeSliding()
    {
        if (config.SlopeSlidingEnabled && isGrounded && Physics.Raycast(gameObject.transform.position, Vector3.down, out RaycastHit hit, 2f) && Vector3.Angle(hit.normal, Vector3.up) >= controller.slopeLimit)
        {
            var normal = hit.normal;
            playerVelocity += new Vector3(normal.x, -normal.y, normal.z) * config.SlopeSpeed;
        }
    }

    private void ProcessCrouch()
    {
        if (isGrounded)
        {
            StartCoroutine(CrouchTransition());
        }
    }

    private IEnumerator CrouchTransition()
    {
        if (isCrouching && Physics.Raycast(transform.position + new Vector3(0, controller.height / 2, 0), Vector3.up, 1f))
        {
            yield break;
        }

        inTransition = true;
        float timeElapsed = 0f;
        float targetHeight = isCrouching ? standingHeight : config.CrouchingHeight;
        float currentHeight = controller.height;
        Vector3 targetCenter = isCrouching ? standingCenter : config.CrouchingCenter;
        Vector3 currentCenter = controller.center;

        while (timeElapsed < config.CrouchTransitionTime)
        {
            controller.height = Mathf.Lerp(currentHeight, targetHeight, timeElapsed / config.CrouchTransitionTime);
            controller.center = Vector3.Lerp(currentCenter, targetCenter, timeElapsed / config.CrouchTransitionTime);
            timeElapsed += Time.deltaTime;
            yield return null;
        }
        controller.height = targetHeight;
        controller.center = targetCenter;

        isCrouching = !isCrouching;
        inTransition = false;
    }
}
