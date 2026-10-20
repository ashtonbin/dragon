using System;
using UnityEngine;

public enum FacilityCharacterStyle { Staff, Security, Swat, Infected, EliteSwat }

/// <summary>Identifies the saved visual variant without changing health, movement or damage.</summary>
[DisallowMultipleComponent]
public sealed class FacilityCharacterAppearance : MonoBehaviour
{
    public FacilityCharacterStyle style;
    public int revision = 1;
    public Renderer[] appearanceRenderers;
    public FacilityCharacterPanelSource[] basePanels;
}

[Serializable]
public sealed class FacilityCharacterPanelSource
{
    public Renderer renderer;
    public Mesh mesh;
    public Material[] materials;
}
