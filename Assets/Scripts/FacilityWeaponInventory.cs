using UnityEngine;
using UnityEngine.InputSystem;

public enum FacilityWeaponKind { Fists, Pistol, Shotgun, AK47 }

/// <summary>Only changes ownership and visibility of explicitly authored weapons.</summary>
[DefaultExecutionOrder(-250), DisallowMultipleComponent]
public sealed class FacilityWeaponInventory : MonoBehaviour
{
    public RetroFPSController player;
    public FacilityShotgun shotgun;
    public FacilityPistol pistol;
    public FacilityAK47 ak47;
    public FacilityFists fists;
    public Transform shotgunModel, pistolModel, fistsModel;
    public Transform ak47Model;
    public RetroViewmodel viewmodel;
    public bool HasPistol { get; private set; }
    public bool HasShotgun { get; private set; }
    public bool HasAK47 { get; private set; }
    public FacilityWeaponKind SelectedWeapon { get; private set; }
    public string CurrentName => SelectedWeapon == FacilityWeaponKind.Fists ? "FISTS" : SelectedWeapon == FacilityWeaponKind.Pistol ? "M1911" : SelectedWeapon == FacilityWeaponKind.Shotgun ? "PUMP SHOTGUN" : "AK-47";
    public bool IsVisible => visible;
    private bool visible = true, configured;

    private void Awake()
    {
        if (player == null) player = GetComponent<RetroFPSController>();
        if (viewmodel == null) viewmodel = GetComponentInChildren<RetroViewmodel>();
        if (!configured) ConfigureLoadout(true);
    }

    public void ConfigureLoadout(bool sandbox)
    {
        configured = true;
        HasPistol = sandbox;
        HasShotgun = sandbox;
        HasAK47 = sandbox;
        if (pistol != null) pistol.ResetForRun();
        if (ak47 != null) ak47.ResetForRun();
        Equip(sandbox ? FacilityWeaponKind.Shotgun : FacilityWeaponKind.Fists);
    }

    public void UnlockPistol() { HasPistol = true; Equip(FacilityWeaponKind.Pistol); }
    public void UnlockShotgun() { HasShotgun = true; Equip(FacilityWeaponKind.Shotgun); }
    public void UnlockAK47() { HasAK47 = true; Equip(FacilityWeaponKind.AK47); }

    /// <summary>Owned firearms travel in the sedan. A new run starts with empty hands until its trunk is opened.</summary>
    public void RetrieveSedanLoadout()
    {
        HasPistol = true;
        HasShotgun = FacilityEconomy.OwnsShotgun;
        HasAK47 = FacilityEconomy.OwnsAK47;
        Equip(HasAK47 ? FacilityWeaponKind.AK47 : HasShotgun ? FacilityWeaponKind.Shotgun : FacilityWeaponKind.Pistol);
    }

    public bool Equip(FacilityWeaponKind kind)
    {
        if ((kind == FacilityWeaponKind.Pistol && !HasPistol) || (kind == FacilityWeaponKind.Shotgun && !HasShotgun) ||
            (kind == FacilityWeaponKind.AK47 && (!HasAK47 || ak47 == null || ak47Model == null))) return false;
        bool changed = SelectedWeapon != kind;
        if (SelectedWeapon == FacilityWeaponKind.Pistol && changed && pistol != null) pistol.CancelReload();
        if (SelectedWeapon == FacilityWeaponKind.AK47 && kind != SelectedWeapon && ak47 != null) ak47.CancelReload();
        SelectedWeapon = kind;
        if (kind == FacilityWeaponKind.Pistol && changed && pistol != null) pistol.RequireFreshTrigger();
        if (viewmodel != null)
        {
            if (kind == FacilityWeaponKind.AK47)
                viewmodel.SelectModel(ak47Model, null, new Vector3(.24f, -.275f, .36f), new Vector3(0f, -5f, 0f),
                    new Vector3(0f, .032f, -.115f), new Vector3(-8.5f, .8f, 2.1f));
            else if (kind == FacilityWeaponKind.Shotgun)
                viewmodel.SelectModel(shotgunModel, shotgun != null ? shotgun.pump : null,
                    new Vector3(.29f, -.28f, .43f), new Vector3(0f, -4f, 0f),
                    new Vector3(0f, .060f, -.205f), new Vector3(-15f, .5f, 3.4f));
            else if (kind == FacilityWeaponKind.Pistol)
                viewmodel.SelectModel(pistolModel, null, new Vector3(.18f, -.21f, .42f), new Vector3(0f, -5f, 0f),
                    new Vector3(0f, .044f, -.115f), new Vector3(-14f, .7f, 1.7f));
            else
                viewmodel.SelectModel(fistsModel, null, new Vector3(0f, -.24f, .32f), Vector3.zero,
                    new Vector3(0f, .010f, -.025f), new Vector3(-2f, 0f, 0f));
        }
        RefreshVisible();
        return true;
    }

    public void SetVisible(bool value)
    {
        if (!value && pistol != null) pistol.CancelReload();
        visible = value; RefreshVisible();
    }
    public bool CanUse(FacilityWeaponKind kind) => visible && SelectedWeapon == kind && FacilityGameSession.GameplayWeaponAllowed;

    private void RefreshVisible()
    {
        if (shotgunModel != null) shotgunModel.gameObject.SetActive(visible && SelectedWeapon == FacilityWeaponKind.Shotgun);
        if (pistolModel != null) pistolModel.gameObject.SetActive(visible && SelectedWeapon == FacilityWeaponKind.Pistol);
        if (fistsModel != null) fistsModel.gameObject.SetActive(visible && SelectedWeapon == FacilityWeaponKind.Fists);
        if (ak47Model != null) ak47Model.gameObject.SetActive(visible && SelectedWeapon == FacilityWeaponKind.AK47);
    }

    private void Update()
    {
        if (!visible || !FacilityGameSession.GameplayWeaponAllowed) return;
        Keyboard keys = Keyboard.current;
        if (keys != null)
        {
            if (keys.digit1Key.wasPressedThisFrame) Equip(FacilityWeaponKind.Fists);
            else if (keys.digit2Key.wasPressedThisFrame) Equip(FacilityWeaponKind.Pistol);
            else if (keys.digit3Key.wasPressedThisFrame) Equip(FacilityWeaponKind.Shotgun);
            else if (keys.digit4Key.wasPressedThisFrame) Equip(FacilityWeaponKind.AK47);
        }
        if (Mouse.current == null) return;
        float scroll = Mouse.current.scroll.ReadValue().y;
        if (Mathf.Abs(scroll) < .1f) return;
        int direction = scroll > 0f ? 1 : -1;
        for (int i = 1; i <= 4; i++)
        {
            FacilityWeaponKind next = (FacilityWeaponKind)(((int)SelectedWeapon + i * direction + 8) % 4);
            if (Equip(next)) break;
        }
    }

    private void OnGUI()
    {
        if (!visible || SelectedWeapon == FacilityWeaponKind.Shotgun || !FacilityGameSession.GameplayWeaponAllowed || Cursor.lockState != CursorLockMode.Locked) return;
        Color previous = GUI.color;
        GUI.color = new Color(.84f, .87f, .85f, .46f);
        GUI.DrawTexture(new Rect(Screen.width * .5f - 1f, Screen.height * .5f - 1f, 2f, 2f), Texture2D.whiteTexture);
        GUI.color = previous;
    }
}
