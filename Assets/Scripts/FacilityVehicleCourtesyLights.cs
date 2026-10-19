using UnityEngine;

/// <summary>Small, door-operated cabin and equipment-bay lamps, independent of the headlights.</summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(20)]
public sealed class FacilityVehicleCourtesyLights : MonoBehaviour
{
    public FacilityDoor driverDoor;
    public FacilityDoor passengerDoor;
    public FacilityDoor trunkDoor;
    public Light[] cabinLights;
    public Light trunkLight;
    public Renderer[] cabinLampLenses;
    public Renderer[] trunkLampLenses;

    [Range(0f, .25f)] public float openThreshold = .02f;
    [ColorUsage(false, true)] public Color cabinLensEmission = new Color(.24f, .16f, .055f, 1f);
    [ColorUsage(false, true)] public Color trunkLensEmission = new Color(.24f, .16f, .055f, 1f);

    public bool CabinLightOn { get; private set; }
    public bool TrunkLightOn { get; private set; }

    private static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");
    private MaterialPropertyBlock lampProperties;

    private void Awake() => RefreshLights();
    private void OnEnable() => RefreshLights();
    private void LateUpdate() => RefreshLights();

    private void OnDisable()
    {
        CabinLightOn = TrunkLightOn = false;
        SetCabinLights(false);
        SetLamp(trunkLight, false);
        SetLenses(cabinLampLenses, Color.black);
        SetLenses(trunkLampLenses, Color.black);
    }

    /// <summary>Evaluate actual hinge travel, so lights stay on throughout closing and turn fully off when shut.</summary>
    public void RefreshLights()
    {
        CabinLightOn = IsOpen(driverDoor) || IsOpen(passengerDoor);
        TrunkLightOn = IsOpen(trunkDoor);
        SetCabinLights(CabinLightOn);
        SetLamp(trunkLight, TrunkLightOn);
        SetLenses(cabinLampLenses, CabinLightOn ? cabinLensEmission : Color.black);
        SetLenses(trunkLampLenses, TrunkLightOn ? trunkLensEmission : Color.black);
    }

    private bool IsOpen(FacilityDoor door) => door != null && door.OpenFraction > openThreshold;

    private void SetCabinLights(bool on)
    {
        if (cabinLights == null) return;
        foreach (Light lamp in cabinLights) SetLamp(lamp, on);
    }

    private static void SetLamp(Light lamp, bool on)
    {
        if (lamp == null) return;
        lamp.shadows = LightShadows.Soft;
        lamp.enabled = on;
    }

    private void SetLenses(Renderer[] lenses, Color emission)
    {
        if (lenses == null) return;
        if (lampProperties == null) lampProperties = new MaterialPropertyBlock();
        foreach (Renderer lens in lenses)
        {
            if (lens == null) continue;
            lampProperties.Clear();
            lens.GetPropertyBlock(lampProperties);
            lampProperties.SetColor(EmissionColor, emission);
            lens.SetPropertyBlock(lampProperties);
        }
    }
}
