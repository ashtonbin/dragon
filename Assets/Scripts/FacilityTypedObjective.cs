using UnityEngine;
using UnityEngine.UI;

/// <summary>Mission terminal text; only advances when the simulation runs.</summary>
[DisallowMultipleComponent]
public sealed class FacilityTypedObjective : MonoBehaviour
{
    public Text titleText, bodyText, counterText, cursorText;
    public AudioSource typingSource;
    public AudioClip typingClick;
    public int TypingClicksPlayed { get; private set; }
    [Min(8f)] public float charactersPerSecond = 35f;
    public string Objective { get; private set; } = "";
    public bool FinishedTyping => visibleCharacters >= Objective.Length;
    private int visibleCharacters;
    private float typeProgress, cursorTime, nextClickAt;

    public void SetObjective(string title, string body, int step, int total)
    {
        Objective = body ?? "";
        visibleCharacters = 0;
        typeProgress = cursorTime = 0f;
        nextClickAt = 0f;
        if (titleText != null) titleText.text = title;
        if (bodyText != null) bodyText.text = "";
        if (counterText != null) counterText.text = step.ToString("00") + " / " + total.ToString("00");
        if (cursorText != null) cursorText.text = "_";
    }

    private void Update()
    {
        if (Time.timeScale <= 0f) return;
        cursorTime += Time.deltaTime;
        if (cursorText != null) cursorText.enabled = cursorTime % .9f < .55f;
        if (FinishedTyping) return;
        typeProgress += Time.deltaTime * charactersPerSecond;
        int count = Mathf.Min(Objective.Length, Mathf.FloorToInt(typeProgress));
        if (count == visibleCharacters) return;
        bool typedGlyph = false;
        for (int i = visibleCharacters; i < count; i++)
            if (!char.IsWhiteSpace(Objective[i])) { typedGlyph = true; break; }
        visibleCharacters = count;
        if (bodyText != null) bodyText.text = Objective.Substring(0, count);
        // Limit overlapping clicks if a slow frame reveals several letters at once.
        if (typedGlyph && typingSource != null && typingClick != null && Time.time >= nextClickAt)
        {
            typingSource.pitch = .97f + (visibleCharacters % 5) * .015f;
            typingSource.PlayOneShot(typingClick);
            TypingClicksPlayed++;
            nextClickAt = Time.time + .035f;
        }
    }
}
