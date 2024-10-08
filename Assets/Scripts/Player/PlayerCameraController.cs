using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(PlayerInput))]
public class PlayerCameraController : MonoBehaviour
{
    [SerializeField] PlayerConfig config;

    [Header("Input Actions")]
    [SerializeField] private InputActionReference look;
    [SerializeField] private InputActionReference zoomADS;

    // HEADBOB
    private Vector3 cameraStartPos;
    private bool inReset = false;

    // ZOOM
    private Coroutine zoomCoroutine;

    // GLOBAL
    private PlayerInput input;
    private Camera playerCamera;
    private float rotation;

    private void Awake()
    {
        playerCamera = GetComponentInChildren<Camera>();
        cameraStartPos = playerCamera.transform.localPosition;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void Start()
    {
        playerCamera.fieldOfView = config.DefaultFOV;

        if(config.ZoomEnabled)
        {
            zoomADS.action.started += _ => ZoomIn();
            zoomADS.action.canceled += _ => ZoomOut();
        }
    }

    private void Update()
    {
        if (config.HeadBobEnabled)
        {
            ProcessHeadBob();
        }
        ProcessLook(look.action.ReadValue<Vector2>());
    }

    private void ProcessLook(Vector2 input)
    {
        float mouseX = input.x;
        float mouseY = input.y;
        rotation -= mouseY * Time.deltaTime * config.YSensitivity;
        rotation = Mathf.Clamp(rotation, -config.UpperLookLimit, config.LowerLookLimit);
        playerCamera.transform.localRotation = Quaternion.Euler(rotation, 0, 0); // Not transform.Rotate because it can't be clamped
        transform.Rotate(Vector3.up * mouseX * Time.deltaTime * config.XSensitivity); //rotates both camera and player
    }

    private IEnumerator ResetCameraPosition()
    {
        inReset = true;
        float timeElapsed = 0;
        while(!PlayerMovementController.isMoving && timeElapsed < config.ResetTime)
        {
            playerCamera.transform.localPosition = Vector3.Lerp(playerCamera.transform.localPosition, cameraStartPos, timeElapsed / config.ResetTime);
            timeElapsed += Time.deltaTime;
            yield return null;
        }
        playerCamera.transform.localPosition = cameraStartPos;
        inReset = false;
    }

    private void ProcessHeadBob()
    {
        if (!PlayerMovementController.isMoving || !PlayerMovementController.isGrounded)
        {
            if(!inReset && playerCamera.transform.localPosition != cameraStartPos)
            {
                StartCoroutine(ResetCameraPosition());
            }
            return;
        }

        float frequency;
        float amplitude;
        if (PlayerMovementController.isCrouching)
        {
            frequency = config.CrouchHeadbobFreq;
            amplitude = config.CrouchHeadbobAmplitude;
        }
        else if (PlayerMovementController.isSprinting)
        {
            frequency = config.SprintHeadbobFreq;
            amplitude = config.SprintHeadbobAmplitude;
        }
        else
        {
            frequency = config.WalkHeadbobFreq;
            amplitude = config.WalkHeadbobAmplitude;
        }

        var bobbing = Vector3.zero;
        bobbing.y = Mathf.Sin(Time.time * frequency) * amplitude; // Time.time is needed for looping headbob and stopping camera from flying away
        bobbing.x = Mathf.Cos(Time.time * frequency / 2) * amplitude / 2;
        playerCamera.transform.localPosition += bobbing * Time.deltaTime;
    }

    private void ZoomIn()
    {
        if (zoomCoroutine != null)
        {
            StopCoroutine(zoomCoroutine);
            zoomCoroutine = null;
        }
        zoomCoroutine = StartCoroutine(ZoomTransition(true));
    }

    private void ZoomOut()
    {
        if (zoomCoroutine != null)
        {
            StopCoroutine(zoomCoroutine);
            zoomCoroutine = null;
        }
        zoomCoroutine = StartCoroutine(ZoomTransition(false));
    }

    private IEnumerator ZoomTransition(bool isZoomIn)
    {
        float targetFOV;
        float transitionTime;
        if(isZoomIn)
        {
            targetFOV = config.ZoomFOV;
            transitionTime = config.ZoomTransitionTime * (playerCamera.fieldOfView / config.DefaultFOV);
        }
        else
        {
            targetFOV = config.DefaultFOV;
            transitionTime = config.ZoomTransitionTime * (playerCamera.fieldOfView / config.ZoomFOV);
        }

        float elapsedTime = 0;
        while(elapsedTime < transitionTime)
        {
            elapsedTime += Time.deltaTime;
            playerCamera.fieldOfView = Mathf.Lerp(playerCamera.fieldOfView, targetFOV, elapsedTime / transitionTime);
            yield return null;
        }
        playerCamera.fieldOfView = targetFOV;
        zoomCoroutine = null;
    }
}
