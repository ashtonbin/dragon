using UnityEngine;

/// <summary>Varies saved shirt materials without instantiating materials or changing trousers/tissue.</summary>
[DisallowMultipleComponent]
public sealed class FacilityObserverOutfit : MonoBehaviour
{
    public Renderer[] shirtRenderers;
    public Material[] shirtPalette;
    public int paletteIndex;
    public bool varyOnSpawn = true;
    public int CurrentPaletteIndex { get; private set; }
    private static int nextPalette;

    private void Awake()
    {
        ApplyPalette(varyOnSpawn ? nextPalette++ : paletteIndex);
    }

    public void ApplyPalette(int index)
    {
        if (shirtPalette == null || shirtPalette.Length == 0) return;
        CurrentPaletteIndex = ((index % shirtPalette.Length) + shirtPalette.Length) % shirtPalette.Length;
        Material shirt = shirtPalette[CurrentPaletteIndex];
        if (shirt == null || shirtRenderers == null) return;
        foreach (Renderer renderer in shirtRenderers) if (renderer != null) renderer.sharedMaterial = shirt;
    }
}
