using UnityEngine;

/// <summary>Keeps a depth-tested world material bound to Unity's dynamic font atlas.</summary>
[RequireComponent(typeof(TextMesh))]
public sealed class FacilityWorldText : MonoBehaviour
{
    TextMesh label;
    Renderer surface;
    MaterialPropertyBlock properties;
    void OnEnable()
    {
        label = GetComponent<TextMesh>(); surface = GetComponent<Renderer>();
        properties = new MaterialPropertyBlock();
        Font.textureRebuilt += Rebind;
        Refresh();
    }
    void Start() => Refresh();
    void OnDisable() => Font.textureRebuilt -= Rebind;
    void Rebind(Font font) { if (label != null && label.font == font) Refresh(); }
    void Refresh()
    {
        if (label == null || label.font == null || surface == null) return;
        surface.GetPropertyBlock(properties);
        properties.SetTexture("_MainTex", label.font.material.mainTexture);
        surface.SetPropertyBlock(properties);
    }
}
