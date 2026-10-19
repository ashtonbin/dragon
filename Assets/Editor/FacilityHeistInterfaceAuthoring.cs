using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Explicitly fits the saved interface to JetBrains Mono and wires the mission's quiet key clicks.</summary>
public static class FacilityHeistInterfaceAuthoring
{
    public const string FontPath = "Assets/Fonts/JetBrainsMono/JetBrainsMono-Regular.ttf";
    const string AudioPath = "Assets/Generated/Interface/Audio/Terminal Key Click.wav";

    public static void Install(FacilityGameSession session)
    {
        Font regular = AssetDatabase.LoadAssetAtPath<Font>(FontPath);
        Font bold = AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/JetBrainsMono/JetBrainsMono-Bold.ttf");
        if (regular == null) throw new InvalidOperationException("The saved JetBrains Mono font must be imported before upgrading the interface.");
        foreach (Text text in session.canvas.GetComponentsInChildren<Text>(true))
        {
            text.font = text.fontStyle == FontStyle.Bold && bold != null ? bold : regular;
            text.fontStyle = FontStyle.Normal;
            // Retain the authored size where it fits, with a small allowance for the wider mono face.
            if (text.alignment != TextAnchor.UpperLeft && text.rectTransform.rect.height < text.fontSize * 2.5f)
            {
                text.resizeTextForBestFit = true;
                text.resizeTextMinSize = Mathf.Max(9, text.fontSize - 3);
                text.resizeTextMaxSize = text.fontSize;
            }
            EditorUtility.SetDirty(text);
        }
        foreach (FacilityTypedObjective terminal in session.canvas.GetComponentsInChildren<FacilityTypedObjective>(true))
        {
            Transform backing = terminal.transform.Find("Terminal");
            if (backing != null)
            {
                RectTransform backingRect = backing.GetComponent<RectTransform>();
                backingRect.sizeDelta = new Vector2(backingRect.sizeDelta.x, 176);
                if (terminal.bodyText != null)
                {
                    RectTransform body = terminal.bodyText.rectTransform;
                    body.sizeDelta = new Vector2(body.sizeDelta.x, 102);
                    terminal.bodyText.resizeTextForBestFit = false;
                }
                if (terminal.cursorText != null)
                    terminal.cursorText.rectTransform.anchoredPosition = new Vector2(16, -148);
            }
            AudioSource source = terminal.GetComponent<AudioSource>();
            if (source == null) source = terminal.gameObject.AddComponent<AudioSource>();
            source.clip = Click(); source.playOnAwake = false; source.loop = false;
            source.volume = .075f; source.spatialBlend = 0f; source.priority = 70;
            source.dopplerLevel = 0f; source.ignoreListenerPause = false;
            terminal.typingSource = source; terminal.typingClick = source.clip;
            EditorUtility.SetDirty(source); EditorUtility.SetDirty(terminal);
        }
        FacilityStoryDirector director = session.storyDirector;
        if (director != null)
        {
            Transform old = session.objectiveHud.transform.Find("Heist Status");
            if (old != null) UnityEngine.Object.DestroyImmediate(old.gameObject);
            GameObject panel = new GameObject("Heist Status", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            panel.transform.SetParent(session.objectiveHud.transform, false);
            var rect = panel.GetComponent<RectTransform>(); rect.anchorMin = rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0, 1); rect.anchoredPosition = new Vector2(-410, -212); rect.sizeDelta = new Vector2(386, 88);
            Image backing = panel.GetComponent<Image>(); backing.color = new Color(.016f, .03f, .037f, .87f); backing.raycastTarget = false;
            GameObject label = new GameObject("Drill and Reinforcements", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            label.transform.SetParent(panel.transform, false);
            var labelRect = label.GetComponent<RectTransform>(); labelRect.anchorMin = Vector2.zero; labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(16, 9); labelRect.offsetMax = new Vector2(-16, -9);
            Text text = label.GetComponent<Text>(); text.font = regular; text.fontSize = 14;
            text.color = new Color(.78f, .85f, .84f); text.alignment = TextAnchor.MiddleLeft;
            text.horizontalOverflow = HorizontalWrapMode.Wrap; text.verticalOverflow = VerticalWrapMode.Truncate;
            text.raycastTarget = false; text.text = "DRILL IN SEDAN\nSECURITY QUIET";
            director.heistStatusText = text;
            EditorUtility.SetDirty(director);
        }
        FacilityBankAuthoring.ApplyInterface(session);
        FacilityDesktopMenuAuthoring.Apply(session);
        EditorUtility.SetDirty(session);
    }

    public static AudioClip Click()
    {
        AudioClip existing = AssetDatabase.LoadAssetAtPath<AudioClip>(AudioPath);
        if (existing != null) return existing;
        Directory.CreateDirectory(Path.GetDirectoryName(AudioPath));
        const int rate = 22050, count = 662;
        var random = new System.Random(9173);
        using (var writer = new BinaryWriter(File.Create(AudioPath)))
        {
            writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); writer.Write(36 + count * 2);
            writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt ")); writer.Write(16); writer.Write((short)1); writer.Write((short)1);
            writer.Write(rate); writer.Write(rate * 2); writer.Write((short)2); writer.Write((short)16);
            writer.Write(System.Text.Encoding.ASCII.GetBytes("data")); writer.Write(count * 2);
            double noiseMemory = 0;
            for (int i = 0; i < count; i++)
            {
                double t = i / (double)rate;
                noiseMemory = noiseMemory * .38 + (random.NextDouble() * 2 - 1) * .62;
                double attack = Math.Min(1, i / 11.0), tail = Math.Min(1, (count - 1 - i) / 55.0);
                double sample = (Math.Sin(t * 2 * Math.PI * 930) * .28 + Math.Sin(t * 2 * Math.PI * 1730) * .12 + noiseMemory * .35) * Math.Exp(-t * 205) * attack * tail;
                writer.Write((short)(sample * 24000));
            }
        }
        AssetDatabase.ImportAsset(AudioPath, ImportAssetOptions.ForceSynchronousImport);
        AudioImporter importer = (AudioImporter)AssetImporter.GetAtPath(AudioPath);
        AudioImporterSampleSettings settings = importer.defaultSampleSettings;
        settings.loadType = AudioClipLoadType.DecompressOnLoad; settings.compressionFormat = AudioCompressionFormat.PCM; settings.preloadAudioData = true;
        importer.defaultSampleSettings = settings; importer.forceToMono = true; importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<AudioClip>(AudioPath);
    }
}
