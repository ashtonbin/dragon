using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Explicitly authors the mode selector and mission interface into the existing canvas.</summary>
public static class FacilityModeUIAuthoring
{
    static readonly Color Ink = new Color(.018f, .029f, .037f, 1f);
    static readonly Color Card = new Color(.065f, .105f, .123f, 1);
    static readonly Color White = new Color(.86f, .90f, .89f);
    static readonly Color Muted = new Color(.47f, .61f, .64f);
    static readonly Color Blue = new Color(.39f, .73f, .79f);
    static readonly Color Amber = new Color(.92f, .57f, .26f);

    public static void Install(FacilityGameSession session)
    {
        Transform root = session.canvas.transform;
        foreach (string name in new[] { "Choose Mode", "Job Complete", "Mission Terminal", "Interaction Prompt" })
        { Transform old = root.Find(name); if (old != null) Object.DestroyImmediate(old.gameObject); }
        Transform oldWeapon = session.hud.transform.Find("Weapon Selection");
        if (oldWeapon != null) Object.DestroyImmediate(oldWeapon.gameObject);
        session.modeMenu = Full(root, "Choose Mode");
        Transform mode = session.modeMenu.transform;
        Box(mode, "Backdrop", 0, 0, 1280, 720, Ink);
        Box(mode, "Title Accent", 80, 67, 44, 3, Blue);
        Label(mode, "Game Title", "rob the bank", 80, 95, 1110, 72, 51, White);
        Label(mode, "Choose", "CHOOSE YOUR WAY IN", 84, 191, 800, 28, 14, Muted);
        session.storyButton = Button(mode, "Story", "STORY", "01 / NIGHT DEPOSIT\nA sedan. A vault. A way back out.", 84, Amber);
        session.sandboxButton = Button(mode, "Sandbox", "SANDBOX", "THE FACILITY\nExplore, spawn characters, try your weapons.", 655, Blue);
        session.modeBackButton = Action(mode, "Back", "Back", 84, 591, 220, 51);

        session.completionMenu = Full(root, "Job Complete");
        Transform complete = session.completionMenu.transform;
        Box(complete, "Backdrop", 0, 0, 1280, 720, Ink);
        Box(complete, "Accent", 332, 171, 616, 3, Amber);
        session.completionTitleText = Label(complete, "Title", "LEVEL 01 COMPLETE", 332, 201, 616, 65, 36, White);
        session.completionBodyText = Label(complete, "Result", "Cash and gold secured. +$2,500 banked.", 336, 285, 608, 85, 20, Muted);
        session.nextLevelButton = Action(complete, "Next Level", "Next level", 336, 393, 608, 58);
        session.returnMainButton = Action(complete, "Main Menu", "Main menu", 336, 467, 608, 58);
        session.completionQuitButton = Action(complete, "Quit", "Quit", 336, 541, 608, 48);

        session.objectiveHud = Full(root, "Mission Terminal");
        Transform terminal = Box(session.objectiveHud.transform, "Terminal", 870, 24, 386, 150, new Color(.016f, .03f, .037f, .84f)).transform;
        RectTransform anchor = terminal.GetComponent<RectTransform>();
        anchor.anchorMin = anchor.anchorMax = Vector2.one; anchor.anchoredPosition = new Vector2(-410, -24);
        Box(terminal, "Rule", 0, 0, 386, 2, Amber);
        Box(terminal, "Left Tick", 0, 0, 2, 14, Amber);
        FacilityTypedObjective typed = session.objectiveHud.AddComponent<FacilityTypedObjective>();
        typed.titleText = Label(terminal, "Title", "NIGHT DEPOSIT", 16, 10, 280, 25, 12, Amber);
        typed.counterText = Label(terminal, "Sequence", "01 / 05", 292, 10, 78, 25, 11, Muted, TextAnchor.MiddleRight);
        typed.bodyText = Label(terminal, "Typed Objective", "", 16, 44, 354, 77, 19, White, TextAnchor.UpperLeft);
        typed.bodyText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        typed.cursorText = Label(terminal, "Cursor", "_", 16, 116, 50, 22, 18, Amber);
        GameObject interact = Full(root, "Interaction Prompt");
        Text interaction = Label(interact.transform, "Interaction", "", 330, 570, 620, 45, 20, White, TextAnchor.MiddleCenter);
        interaction.gameObject.AddComponent<Shadow>().effectDistance = new Vector2(1, -1);
        RectTransform interactRect = interaction.GetComponent<RectTransform>();
        interactRect.anchorMin = interactRect.anchorMax = new Vector2(.5f, 0); interactRect.anchoredPosition = new Vector2(-310, 150);
        session.weaponNameText = Label(session.hud.transform, "Weapon Selection", "FISTS", 25, 670, 770, 28, 12, White);
        RectTransform weaponRect = session.weaponNameText.GetComponent<RectTransform>();
        weaponRect.anchorMin = weaponRect.anchorMax = Vector2.zero; weaponRect.anchoredPosition = new Vector2(25, 47);
        Transform controls = session.hud.transform.Find("Controls");
        if (controls != null) session.controlHintText = controls.GetComponent<Text>();
        Transform oldMainHint = session.mainMenu.transform.Find("Menu Hint");
        if (oldMainHint != null) oldMainHint.GetComponent<Text>().text = "1 / 2 / 3  WEAPONS    ESC  PAUSE";
        session.modeMenu.SetActive(false); session.completionMenu.SetActive(false); session.objectiveHud.SetActive(false);
        EditorUtility.SetDirty(session);
    }

    public static void BindStory(FacilityGameSession session, FacilityStoryDirector director)
    {
        director.typedObjective = session.objectiveHud.GetComponent<FacilityTypedObjective>();
        director.interactionText = session.canvas.transform.Find("Interaction Prompt/Interaction").GetComponent<Text>();
        EditorUtility.SetDirty(director);
    }

    public static void FinishPanels(FacilityGameSession session)
    {
        foreach (GameObject panel in new[] { session.modeMenu, session.completionMenu })
        {
            if (panel == null) continue;
            Transform backdrop = panel.transform.Find("Backdrop");
            if (backdrop == null) continue;
            Image image = backdrop.GetComponent<Image>();
            if (image == null) continue;
            Color color = image.color; color.a = 1f; image.color = color;
            EditorUtility.SetDirty(image);
        }
    }

    static Button Button(Transform parent, string name, string title, string description, float x, Color accent)
    {
        Transform card = Box(parent, name, x, 271, 540, 246, Card).transform;
        Box(card, "Accent", 0, 0, 540, 3, accent);
        Label(card, "Name", title, 29, 27, 482, 58, 34, White);
        Label(card, "Description", description, 31, 113, 478, 89, 18, Muted);
        return Configure(card.gameObject);
    }
    static Button Action(Transform parent, string name, string title, float x, float y, float w, float h)
    { GameObject go = Box(parent, name, x, y, w, h, Card); Label(go.transform, "Label", title, 22, 0, w - 44, h, 22, White); return Configure(go); }
    static Button Configure(GameObject go)
    {
        Button button = go.AddComponent<Button>(); button.targetGraphic = go.GetComponent<Image>();
        button.targetGraphic.raycastTarget = true;
        ColorBlock colors = button.colors; colors.highlightedColor = new Color(1.55f, 1.55f, 1.55f); colors.selectedColor = colors.highlightedColor;
        colors.pressedColor = new Color(.7f, .85f, .9f); colors.fadeDuration = .08f; button.colors = colors;
        return button;
    }
    static GameObject Full(Transform parent, string name)
    { GameObject go = new GameObject(name, typeof(RectTransform)); go.transform.SetParent(parent, false); var r = go.GetComponent<RectTransform>(); r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero; return go; }
    static void Place(RectTransform r, float x, float y, float w, float h)
    { r.anchorMin = r.anchorMax = new Vector2(0, 1); r.pivot = new Vector2(0, 1); r.anchoredPosition = new Vector2(x, -y); r.sizeDelta = new Vector2(w, h); }
    static GameObject Box(Transform parent, string name, float x, float y, float w, float h, Color color)
    { GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)); go.transform.SetParent(parent, false); Place(go.GetComponent<RectTransform>(), x, y, w, h); var im = go.GetComponent<Image>(); im.color = color; im.raycastTarget = false; return go; }
    static Text Label(Transform parent, string name, string text, float x, float y, float w, float h, int size, Color color, TextAnchor alignment = TextAnchor.MiddleLeft)
    { GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text)); go.transform.SetParent(parent, false); Place(go.GetComponent<RectTransform>(), x, y, w, h); Text t = go.GetComponent<Text>(); t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); t.fontSize = size; t.color = color; t.text = text; t.alignment = alignment; t.raycastTarget = false; t.horizontalOverflow = HorizontalWrapMode.Wrap; t.verticalOverflow = VerticalWrapMode.Truncate; return t; }
}
