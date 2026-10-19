
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class RetroFPSController : MonoBehaviour
{
    public Transform cameraPivot;

    [Header("Movement")]
    public float walkSpeed = 6f;
    public float sprintSpeed = 10f;
    public float jumpHeight = 1.2f;
    public float gravity = -22f;

    [Header("Camera")]
    public float sensitivity = 0.12f;

    private CharacterController controller;
    private float verticalSpeed;
    private float cameraPitch;
    private float recoilPitch;
    private float recoilYaw;
    private float recoilPitchVelocity;
    private float recoilYawVelocity;
    private float recoilStarted;
    private bool applyingRecoil;
    public float CameraRecoilDegrees => recoilPitch;

    /// <summary>Clear the shoulder spring before an external camera placement or teleport.</summary>
    public void ResetRecoil()
    {
        applyingRecoil = false;
        recoilPitch = recoilYaw = recoilPitchVelocity = recoilYawVelocity = 0f;
    }

    /// <summary>A short shoulder kick layered over the player's aim, without changing movement or mouse sensitivity.</summary>
    public void AddRecoil(float pitchDegrees, float yawDegrees)
    {
        if (cameraPivot == null) return;
        if (!applyingRecoil)
        {
            cameraPitch = cameraPivot.localEulerAngles.x;
            if (cameraPitch > 180f) cameraPitch -= 360f;
        }
        recoilPitch = Mathf.Min(7f, recoilPitch + pitchDegrees);
        recoilYaw += yawDegrees;
        recoilPitchVelocity = recoilYawVelocity = 0f;
        recoilStarted = Time.time;
        applyingRecoil = true;
        cameraPivot.localRotation = Quaternion.Euler(Mathf.Clamp(cameraPitch - recoilPitch, -89f, 89f), recoilYaw, 0f);
    }

    void LateUpdate()
    {
        if (!applyingRecoil || cameraPivot == null) return;
        // Hold the first impulse for a readable frame, then let the shoulder absorb the weighted return.
        if (Time.time - recoilStarted > .045f)
        {
            recoilPitch = Mathf.SmoothDamp(recoilPitch, 0f, ref recoilPitchVelocity, .16f);
            recoilYaw = Mathf.SmoothDamp(recoilYaw, 0f, ref recoilYawVelocity, .12f);
        }
        cameraPivot.localRotation = Quaternion.Euler(Mathf.Clamp(cameraPitch - recoilPitch, -89f, 89f), recoilYaw, 0f);
        if (recoilPitch < .004f && Mathf.Abs(recoilYaw) < .004f)
        {
            applyingRecoil = false;
            recoilPitch = recoilYaw = 0f;
            cameraPivot.localRotation = Quaternion.Euler(cameraPitch, 0f, 0f);
        }
    }

    void Awake()
    {
        controller = GetComponent<CharacterController>();

        if (cameraPivot == null)
        {
            Camera childCamera = GetComponentInChildren<Camera>();
            if (childCamera != null)
                cameraPivot = childCamera.transform;
        }
    }

    void Start()
    {
        if (FacilityGameSession.GameplayInputAllowed) LockCursor();
    }

    void Update()
    {
        if (!FacilityGameSession.GameplayInputAllowed) return;
        Keyboard keyboard = Keyboard.current;
        Mouse mouse = Mouse.current;

        if (keyboard == null)
            return;

        if (FacilityGameSession.Instance == null && keyboard.escapeKey.wasPressedThisFrame)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        if (mouse != null &&
            mouse.leftButton.wasPressedThisFrame &&
            Cursor.lockState != CursorLockMode.Locked)
        {
            LockCursor();
        }

        if (mouse != null &&
            cameraPivot != null &&
            Cursor.lockState == CursorLockMode.Locked)
        {
            Vector2 look = mouse.delta.ReadValue() * sensitivity;

            transform.Rotate(0f, look.x, 0f);

            cameraPitch = Mathf.Clamp(
                cameraPitch - look.y, -85f, 85f
            );

            cameraPivot.localRotation =
                Quaternion.Euler(cameraPitch, 0f, 0f);
        }

        float x = 0f;
        float z = 0f;

        if (keyboard.aKey.isPressed) x--;
        if (keyboard.dKey.isPressed) x++;
        if (keyboard.sKey.isPressed) z--;
        if (keyboard.wKey.isPressed) z++;

        Vector3 direction =
            (transform.right * x + transform.forward * z)
            .normalized;

        bool sprinting = keyboard.leftShiftKey.isPressed;
        float speed = sprinting ? sprintSpeed : walkSpeed;

        if (controller.isGrounded && verticalSpeed < 0f)
            verticalSpeed = -2f;

        if (controller.isGrounded &&
            keyboard.spaceKey.wasPressedThisFrame)
        {
            verticalSpeed = Mathf.Sqrt(
                jumpHeight * -2f * gravity
            );
        }

        verticalSpeed += gravity * Time.deltaTime;

        Vector3 velocity =
            direction * speed +
            Vector3.up * verticalSpeed;

        controller.Move(velocity * Time.deltaTime);
    }

    void LockCursor()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }
}
