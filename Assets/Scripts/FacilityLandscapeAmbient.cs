using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// A saved open-sky diffuse light probe for the meadow beyond the bank's indoor probe volume.
/// This supplies incident sky light to Lit materials; emission and direct-light shadows are unchanged.
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
public sealed class FacilityLandscapeAmbient : MonoBehaviour
{
    public Renderer[] surfaces;
    public Color skyAmbient = new Color(.17f, .21f, .235f);
    public Color upperSky = new Color(.075f, .09f, .11f);
    public Color horizonBounce = new Color(.018f, .026f, .017f);
    MaterialPropertyBlock block;

    void OnEnable() { Refresh(); }
    void OnValidate() { Refresh(); }

    public void Refresh()
    {
        if (surfaces == null) return;
        if (block == null) block = new MaterialPropertyBlock();
        var probe = new SphericalHarmonicsL2();
        probe.AddAmbientLight(skyAmbient);
        probe.AddDirectionalLight(Vector3.up, upperSky, 1f);
        probe.AddDirectionalLight(Vector3.down, horizonBounce, 1f);
        var packed = new SHCoefficients(probe);
        foreach (Renderer surface in surfaces)
        {
            if (surface == null) continue;
            surface.lightProbeUsage = LightProbeUsage.CustomProvided;
            surface.GetPropertyBlock(block);
            // The packed form is the same one used by the installed Unity rendering package.
            block.SetVector("unity_SHAr", packed.SHAr); block.SetVector("unity_SHAg", packed.SHAg); block.SetVector("unity_SHAb", packed.SHAb);
            block.SetVector("unity_SHBr", packed.SHBr); block.SetVector("unity_SHBg", packed.SHBg); block.SetVector("unity_SHBb", packed.SHBb);
            block.SetVector("unity_SHC", packed.SHC); block.SetVector("unity_ProbesOcclusion", Vector4.one);
            surface.SetPropertyBlock(block);
        }
    }
}
