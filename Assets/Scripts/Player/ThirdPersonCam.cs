using UnityEngine;
using UnityEngine.InputSystem;

public class ThirdPersonCam : MonoBehaviour
{
    [Header("References")]
    public Transform cameraRoot;
    public Transform orientation;
    public Transform playerModel;

    [Header("Camera Settings")]
    public float distance = 3f;
    public float mouseSensitivity = 2.5f;
    public float minPitch = -25f;
    public float maxPitch = 55f;

    [Header("Player Model")]
    public float modelTurnSpeed = 12f;

    [Header("Input")]
    public InputActionReference lookAction;
    public InputActionReference moveAction;

    private float yaw;
    private float pitch;

    private Vector2 lookInput;
    private Vector2 moveInput;

    private void OnEnable()
    {
        if (lookAction != null) lookAction.action.Enable();
        if (moveAction != null) moveAction.action.Enable();
    }

    private void OnDisable()
    {
        if (lookAction != null) lookAction.action.Disable();
        if (moveAction != null) moveAction.action.Disable();
    }

    private void LateUpdate()
    {
        ReadInput();
        RotateCamera();
        RotateModel();
    }

    private void ReadInput()
    {
        lookInput = lookAction?.action?.ReadValue<Vector2>() ?? Vector2.zero;
        moveInput = moveAction?.action?.ReadValue<Vector2>() ?? Vector2.zero;
    }

    private void RotateCamera()
    {
        yaw += lookInput.x * mouseSensitivity;
        pitch -= lookInput.y * mouseSensitivity;
        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);

        cameraRoot.rotation = Quaternion.Euler(pitch, yaw, 0f);

        transform.position = cameraRoot.position - cameraRoot.forward * distance;

        transform.LookAt(cameraRoot.position);

        if (orientation != null)
            orientation.rotation = Quaternion.Euler(0f, yaw, 0f);
    }

    private void RotateModel()
    {
        if (playerModel == null || orientation == null) return;

        Vector3 inputDir = orientation.forward * moveInput.y + orientation.right * moveInput.x;

        if (inputDir.sqrMagnitude > 0.01f)
        {
            playerModel.forward = Vector3.Slerp(
                playerModel.forward,
                inputDir.normalized,
                Time.deltaTime * modelTurnSpeed);
        }
    }
}