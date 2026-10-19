using UnityEngine;

/// <summary>Rebinds the dynamic legacy font atlas to the saved URP sign material.</summary>
[ExecuteAlways]
[DisallowMultipleComponent]
public sealed class FacilitySignFont : MonoBehaviour
{
    public Font font;
    public Material material;

    private const string Characters = " !\"#$%&'()*+,-./0123456789:;<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[\\]^_`abcdefghijklmnopqrstuvwxyz{|}~";

    private void OnEnable()
    {
        Font.textureRebuilt += OnTextureRebuilt;
        Refresh();
    }

    public void Refresh()
    {
        if (font == null || material == null)
            return;
        font.RequestCharactersInTexture(Characters, 64, FontStyle.Normal);
        BindTexture();
    }

    private void OnTextureRebuilt(Font rebuiltFont)
    {
        if (rebuiltFont == font)
            BindTexture();
    }

    private void BindTexture()
    {
        if (font != null && material != null && font.material != null)
            material.SetTexture("_MainTex", font.material.mainTexture);
    }

    private void OnDisable()
    {
        Font.textureRebuilt -= OnTextureRebuilt;
    }
}
