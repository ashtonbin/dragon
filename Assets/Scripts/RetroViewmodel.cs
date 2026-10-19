using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

/// <summary>Motion for an explicitly authored first-person weapon. Never constructs a weapon at runtime.</summary>
public class RetroViewmodel : MonoBehaviour
{
    // Kept for existing scene serialization and the facility material configuration tool.
    public Material darkMaterial;
    public Material steelMaterial;
    public Material accentMaterial;

    [Header("Authored shotgun")]
    public Transform authoredModel;
    public Transform pump;
    public Vector3 restingPosition = new Vector3(.29f, -.28f, .43f);
    public Vector3 restingEuler = new Vector3(0f, -4f, 0f);

    [Header("Motion")]
    public float bobSpeed = 11f;
    public float bobAmount = .012f;
    public float swayStrength = .0008f;

    private CharacterController controller;
    private float bobTime;
    private Vector2 smoothedMouse;
    private float recoil;
    private float recoilVelocity;
    private float kickTime = -10f;
    private Vector3 pumpHome;
    private readonly Dictionary<Transform, Vector3> pumpRestPositions = new Dictionary<Transform, Vector3>();
    private Vector3 kickOffset = new Vector3(0f, .060f, -.205f);
    private Vector3 kickEuler = new Vector3(-15f, .5f, 3.4f);

    public void SelectModel(Transform model, Transform selectedPump, Vector3 position, Vector3 euler,
        Vector3 recoilOffset, Vector3 recoilAngles)
    {
        // A weapon may be changed halfway through its rack. Never mistake that offset for its home.
        if (pump != null)
        {
            if (!pumpRestPositions.TryGetValue(pump, out Vector3 previousRest))
                pumpRestPositions[pump] = previousRest = pump.localPosition;
            pump.localPosition = previousRest;
        }
        authoredModel = model;
        pump = selectedPump;
        restingPosition = position;
        restingEuler = euler;
        kickOffset = recoilOffset;
        kickEuler = recoilAngles;
        recoil = recoilVelocity = 0f;
        kickTime = -10f;
        if (pump != null)
        {
            if (!pumpRestPositions.TryGetValue(pump, out pumpHome))
                pumpRestPositions[pump] = pumpHome = pump.localPosition;
            pump.localPosition = pumpHome;
        }
        if (model != null) { model.localPosition = position; model.localRotation = Quaternion.Euler(euler); }
    }

    private void Start()
    {
        controller = GetComponentInParent<CharacterController>();
        if (pump != null)
        {
            if (!pumpRestPositions.TryGetValue(pump, out pumpHome))
                pumpRestPositions[pump] = pumpHome = pump.localPosition;
        }
        if (authoredModel != null)
        {
            authoredModel.localPosition = restingPosition;
            authoredModel.localRotation = Quaternion.Euler(restingEuler);
        }
    }

    public void Kick()
    {
        recoil = 1f;
        recoilVelocity = 0f;
        kickTime = Time.time;
        // Immediate displacement matters: a smoothed target alone misses the sharp firing frame.
        if (authoredModel != null)
        {
            authoredModel.localPosition = restingPosition + kickOffset;
            authoredModel.localRotation = Quaternion.Euler(restingEuler + kickEuler);
        }
    }

    public void SetPumpTravel(float amount)
    {
        if (pump != null) pump.localPosition = pumpHome + Vector3.back * (.135f * Mathf.Clamp01(amount));
    }

    private void LateUpdate()
    {
        if (authoredModel == null) return;
        float movement = 0f;
        if (controller != null && controller.isGrounded)
        {
            Vector3 velocity = controller.velocity;
            velocity.y = 0f;
            movement = Mathf.Clamp01(velocity.magnitude / 6f);
        }
        bobTime = movement > .1f ? bobTime + Time.deltaTime * bobSpeed : 0f;
        Vector3 bob = new Vector3(Mathf.Sin(bobTime), Mathf.Abs(Mathf.Cos(bobTime)), 0f)
                      * (bobAmount * movement);
        Vector2 mouse = Mouse.current != null && Cursor.lockState == CursorLockMode.Locked
            ? Mouse.current.delta.ReadValue() : Vector2.zero;
        smoothedMouse = Vector2.Lerp(smoothedMouse, mouse, Mathf.Clamp01(Time.deltaTime * 12f));
        Vector3 sway = new Vector3(Mathf.Clamp(-smoothedMouse.x * swayStrength, -.035f, .035f),
            Mathf.Clamp(-smoothedMouse.y * swayStrength, -.035f, .035f), 0f);
        if (Time.time - kickTime > .045f) recoil = Mathf.SmoothDamp(recoil, 0f, ref recoilVelocity, .15f);
        authoredModel.localPosition = Vector3.Lerp(authoredModel.localPosition,
            restingPosition + bob + sway + kickOffset * recoil,
            Mathf.Clamp01(Time.deltaTime * 30f));
        Vector3 rotation = restingEuler + new Vector3(kickEuler.x * recoil +
            Mathf.Clamp(smoothedMouse.y * .04f, -2f, 2f),
            Mathf.Clamp(-smoothedMouse.x * .04f, -3f, 3f) + kickEuler.y * recoil, kickEuler.z * recoil);
        authoredModel.localRotation = Quaternion.Slerp(authoredModel.localRotation,
            Quaternion.Euler(rotation), Mathf.Clamp01(Time.deltaTime * 22f));
    }
}
