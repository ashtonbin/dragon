using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>A quiet wallet entry in menus opens a scene-owned desktop armory.</summary>
[DisallowMultipleComponent]
public sealed class FacilityShop : MonoBehaviour
{
    public bool IsOpen => window != null && window.activeSelf;
    public event Action Closed;

    FacilityGameSession session;
    GameObject window;
    Button launcher, closeButton, pistolButton, shotgunButton, akButton;
    Text launcherLabel, walletText, statusText, shotgunState, akState;
    Text shotgunBuyLabel, akBuyLabel;
    Font font;
    Sprite raised, pressed, hover, frame, glass, wallpaper;
    bool previousAudioPause, bound;
    readonly List<BlockedControl> blockedControls = new List<BlockedControl>();

    struct BlockedControl
    {
        public Selectable control;
        public bool interactable;
    }

    static readonly Color White = new Color(.88f, .90f, .91f);
    static readonly Color Muted = new Color(.56f, .60f, .62f);
    static readonly Color Gold = new Color(.91f, .70f, .38f);
    static readonly Color Green = new Color(.53f, .74f, .62f);
    static readonly Color Client = new Color(.085f, .098f, .112f);

    public void Bind(FacilityGameSession owner)
    {
        if (bound || owner == null || owner.canvas == null) return;
        session = owner;
        Text sourceText = owner.canvas.GetComponentInChildren<Text>(true);
        font = sourceText != null ? sourceText.font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        FacilityDesktopButton source = owner.startButton != null ? owner.startButton.GetComponent<FacilityDesktopButton>() : null;
        if (source != null) { raised = source.raisedSprite; pressed = source.pressedSprite; hover = source.hoverSprite; }
        foreach (Image image in owner.canvas.GetComponentsInChildren<Image>(true))
        {
            if (image.sprite == null) continue;
            if (image.sprite.name == "Window frame") frame = image.sprite;
            else if (image.sprite.name == "Glass title bar") glass = image.sprite;
            else if (image.sprite.name == "Desktop wallpaper") wallpaper = image.sprite;
        }
        Build(owner.canvas.transform);
        FacilityEconomy.Changed += Refresh;
        bound = true;
        Refresh();
        UpdateLauncher();
    }

    public void Show()
    {
        if (!bound || IsOpen || session == null) return;
        if (session.CurrentState == FacilityGameState.Playing || session.CurrentState == FacilityGameState.SpawnMenu || session.CurrentState == FacilityGameState.Placing)
            session.PauseGame();
        if (!MenuAvailable()) return;
        previousAudioPause = AudioListener.pause;
        window.SetActive(true);
        window.transform.SetAsLastSibling();
        launcher.gameObject.SetActive(false);
        BlockUnderlyingControls();
        AudioListener.pause = true;
        Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        statusText.text = "Complete a job with the cash and gold to earn " + FacilityEconomy.FormatCash(FacilityEconomy.HeistPayout) + ".";
        statusText.color = Muted;
        Refresh();
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(closeButton.gameObject);
    }

    public void Hide()
    {
        if (!IsOpen) return;
        window.SetActive(false);
        RestoreUnderlyingControls();
        AudioListener.pause = previousAudioPause;
        UpdateLauncher();
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(launcher.gameObject);
        Closed?.Invoke();
    }

    void BlockUnderlyingControls()
    {
        RestoreUnderlyingControls();
        foreach (Selectable control in session.canvas.GetComponentsInChildren<Selectable>(true))
        {
            if (control.transform.IsChildOf(window.transform)) continue;
            blockedControls.Add(new BlockedControl { control = control, interactable = control.interactable });
            // An opaque raycast blocker alone does not stop automatic keyboard/gamepad navigation.
            control.interactable = false;
        }
    }

    void RestoreUnderlyingControls()
    {
        foreach (BlockedControl blocked in blockedControls)
            if (blocked.control != null) blocked.control.interactable = blocked.interactable;
        blockedControls.Clear();
    }

    void Update() => UpdateLauncher();

    bool MenuAvailable() => session != null && (session.CurrentState == FacilityGameState.MainMenu || session.CurrentState == FacilityGameState.ModeSelect ||
        session.CurrentState == FacilityGameState.Paused || session.CurrentState == FacilityGameState.LevelComplete);

    void UpdateLauncher()
    {
        if (launcher != null) launcher.gameObject.SetActive(!IsOpen && MenuAvailable());
    }

    void Purchase(FacilityWeaponKind kind)
    {
        bool purchased = FacilityEconomy.TryPurchase(kind, out string reason);
        statusText.text = reason;
        statusText.color = purchased ? Green : Gold;
        Refresh();
    }

    void Refresh()
    {
        if (launcherLabel == null) return;
        launcherLabel.text = "SHOP   /   " + FacilityEconomy.CashLabel;
        walletText.text = FacilityEconomy.CashLabel;
        RefreshCard(shotgunButton, shotgunBuyLabel, shotgunState, FacilityWeaponKind.Shotgun);
        RefreshCard(akButton, akBuyLabel, akState, FacilityWeaponKind.AK47);
    }

    static void RefreshCard(Button button, Text buy, Text state, FacilityWeaponKind kind)
    {
        bool owned = FacilityEconomy.Owns(kind);
        int price = FacilityEconomy.Price(kind);
        button.interactable = !owned;
        buy.text = owned ? "OWNED / IN CAR" : "BUY / " + FacilityEconomy.FormatCash(price);
        buy.color = owned ? Green : White;
        state.text = owned ? "OWNED" : FacilityEconomy.Cash >= price ? "AVAILABLE" : "NEED " + FacilityEconomy.FormatCash(price - FacilityEconomy.Cash);
        state.color = owned ? Green : FacilityEconomy.Cash >= price ? Gold : Muted;
    }

    void Build(Transform parent)
    {
        launcher = ActionButton(parent, "Shop and Saved Cash", "SHOP / $0", new Rect(1020, 16, 236, 44), out launcherLabel);
        launcherLabel.fontSize = 13;
        launcher.onClick.AddListener(Show);
        window = Full(parent, "Armory Shop");
        Image backdrop = Box(window.transform, "Desktop", new Rect(0, 0, 1280, 720), new Color(.045f, .053f, .063f));
        if (wallpaper != null) { backdrop.sprite = wallpaper; backdrop.color = Color.white; }
        backdrop.raycastTarget = true;
        Box(window.transform, "Window shadow", new Rect(44, 51, 1200, 626), new Color(0, 0, 0, .45f));
        Image border = Box(window.transform, "Window frame", new Rect(36, 42, 1208, 626), new Color(.22f, .25f, .27f));
        if (frame != null) { border.sprite = frame; border.color = Color.white; border.type = Image.Type.Sliced; }
        Image title = Box(window.transform, "Glass title bar", new Rect(42, 48, 1196, 36), new Color(.27f, .30f, .33f));
        if (glass != null) { title.sprite = glass; title.color = Color.white; }
        Box(window.transform, "Title reflection", new Rect(44, 49, 1192, 14), new Color(1, 1, 1, .045f));
        Label(window.transform, "Window caption", "rob the bank / Armory & account", new Rect(59, 51, 980, 27), 13, White);
        closeButton = ActionButton(window.transform, "Close shop", "RETURN  /  ESC", new Rect(1057, 51, 163, 28), out Text closeLabel);
        closeLabel.fontSize = 11;
        closeButton.onClick.AddListener(Hide);
        Box(window.transform, "Client area", new Rect(45, 88, 1190, 570), Client);

        Label(window.transform, "Section", "THE ARMORY", new Rect(72, 111, 740, 24), 12, Gold);
        Label(window.transform, "Heading", "A better way back out.", new Rect(70, 145, 800, 50), 31, White);
        Label(window.transform, "Shop description", "Permanent weapons. Collect your loadout from the car.", new Rect(73, 202, 790, 28), 13, Muted);

        Box(window.transform, "Wallet backing", new Rect(940, 111, 264, 119), new Color(.061f, .075f, .085f));
        Box(window.transform, "Wallet top rule", new Rect(940, 111, 264, 2), Gold);
        Label(window.transform, "Wallet title", "SAVED CASH", new Rect(958, 122, 228, 22), 11, Muted);
        walletText = Label(window.transform, "Account balance", "$0", new Rect(956, 148, 232, 39), 29, Gold, TextAnchor.MiddleRight);
        Label(window.transform, "Wallet persistence", "ACCOUNT SAVED AUTOMATICALLY", new Rect(958, 197, 228, 18), 9, Muted, TextAnchor.MiddleRight);

        Transform pistolCard = Card(window.transform, 72, "01", "SILVER M1911", ".45 / EIGHT-ROUND SIDEARM", "Stainless steel. Walnut grips.\nPrecise fire. R to reload.", "OWNED", Green);
        pistolButton = ActionButton(pistolCard, "Pistol owned", "OWNED / IN CAR", new Rect(19, 267, 306, 42), out Text pistolLabel);
        pistolLabel.fontSize = 13; pistolLabel.color = Green; pistolButton.interactable = false;
        Transform shotgunCard = Card(window.transform, 468, "02", "PUMP SHOTGUN", "PUMP-ACTION / CLOSE RANGE", "Heavy spread. Hard impact.\nMake every shell count.", "LOCKED", Muted);
        shotgunState = shotgunCard.Find("Ownership").GetComponent<Text>();
        shotgunButton = ActionButton(shotgunCard, "Buy shotgun", "BUY / $5,000", new Rect(19, 267, 306, 42), out shotgunBuyLabel);
        shotgunBuyLabel.fontSize = 13; shotgunButton.onClick.AddListener(() => Purchase(FacilityWeaponKind.Shotgun));
        Transform akCard = Card(window.transform, 864, "03", "AK-47", "AUTOMATIC / 7.62 x 39", "Steel. Wood. Sustained power.\nControl the recoil. Own the room.", "LOCKED", Muted);
        akState = akCard.Find("Ownership").GetComponent<Text>();
        akButton = ActionButton(akCard, "Buy AK-47", "BUY / $10,000", new Rect(19, 267, 306, 42), out akBuyLabel);
        akBuyLabel.fontSize = 13; akButton.onClick.AddListener(() => Purchase(FacilityWeaponKind.AK47));
        DrawWeapon(pistolCard, FacilityWeaponKind.Pistol);
        DrawWeapon(shotgunCard, FacilityWeaponKind.Shotgun);
        DrawWeapon(akCard, FacilityWeaponKind.AK47);

        Box(window.transform, "Footer rule", new Rect(72, 595, 1136, 1), new Color(.23f, .26f, .28f));
        statusText = Label(window.transform, "Shop status", "", new Rect(73, 606, 1020, 27), 12, Muted);
        Label(window.transform, "Permanent inventory", "ALL SALES SAVED", new Rect(1040, 637, 168, 17), 9, Muted, TextAnchor.MiddleRight);
        Label(window.transform, "Footer hint", "Cash and gold secured. One job. $2,500.", new Rect(73, 637, 760, 17), 10, Muted);
        window.SetActive(false);
    }

    Transform Card(Transform parent, float x, string number, string name, string type, string description, string ownership, Color stateColor)
    {
        // Three evenly spaced 344px cards leave a generous 52px gutter between items.
        Transform card = Box(parent, name + " card", new Rect(x, 253, 344, 326), new Color(.112f, .129f, .144f)).transform;
        Box(card, "Card top edge", new Rect(0, 0, 344, 1), new Color(.31f, .34f, .36f));
        Label(card, "Item number", number, new Rect(19, 13, 48, 22), 11, Muted);
        Label(card, "Ownership", ownership, new Rect(79, 13, 246, 22), 10, stateColor, TextAnchor.MiddleRight);
        Label(card, "Name", name, new Rect(18, 153, 308, 34), 21, White);
        Label(card, "Type", type, new Rect(19, 189, 306, 20), 9, Gold);
        Label(card, "Description", description, new Rect(19, 217, 306, 42), 11, Muted, TextAnchor.UpperLeft);
        return card;
    }

    void DrawWeapon(Transform parent, FacilityWeaponKind kind)
    {
        // Small technical illustrations keep the shop sharp at every canvas resolution.
        Color steel = new Color(.37f, .42f, .44f), edge = new Color(.63f, .68f, .68f);
        Color dark = new Color(.12f, .15f, .17f), wood = new Color(.46f, .25f, .12f), woodLight = new Color(.62f, .36f, .17f);
        Transform art = Box(parent, "Weapon illustration", new Rect(18, 49, 308, 92), new Color(.075f, .091f, .104f)).transform;
        for (int i = 0; i < 7; i++) Box(art, "Grid", new Rect(14 + i * 46, 0, 1, 92), new Color(.16f, .19f, .21f, .38f));
        Box(art, "Technical baseline", new Rect(12, 72, 284, 1), new Color(.24f, .29f, .30f));
        Label(art, "Caliber", kind == FacilityWeaponKind.Pistol ? ".45 ACP" : kind == FacilityWeaponKind.Shotgun ? "12 GA" : "7.62", new Rect(12, 74, 280, 15), 8, Muted, TextAnchor.MiddleRight);
        if (kind == FacilityWeaponKind.Pistol)
        {
            steel=new Color(.69f,.73f,.77f);edge=new Color(.85f,.88f,.90f);
            Part(art, "Slide", 104, 22, 103, 17, steel);
            Part(art, "Slide highlight", 105, 22, 100, 2, edge);
            Part(art, "Muzzle", 205, 24, 8, 12, dark);
            Part(art, "Frame", 108, 38, 83, 7, steel);
            Part(art, "Grip", 108, 43, 29, 34, dark, -13);
            Part(art, "Grip panel", 113, 46, 17, 27, wood, -13);
            Part(art, "Trigger guard", 142, 43, 24, 19, steel);
            Part(art, "Trigger guard opening", 146, 44, 16, 13, new Color(.075f, .091f, .104f));
            Part(art, "Trigger", 151, 43, 3, 9, edge, 16);
            Part(art, "Front sight", 195, 18, 5, 5, edge);
            for (int i = 0; i < 5; i++) Part(art, "Slide serration", 111 + i * 4, 26, 1, 9, dark, -12);
        }
        else if (kind == FacilityWeaponKind.Shotgun)
        {
            Part(art, "Stock", 19, 39, 54, 17, wood, 7);
            Part(art, "Butt pad", 17, 37, 7, 23, dark, 7);
            Part(art, "Stock highlight", 26, 41, 39, 2, woodLight, 7);
            Part(art, "Receiver", 75, 30, 76, 22, steel);
            Part(art, "Ejection port", 111, 34, 27, 10, dark);
            Part(art, "Barrel", 145, 27, 137, 7, steel);
            Part(art, "Barrel highlight", 150, 27, 127, 1, edge);
            Part(art, "Magazine tube", 147, 41, 111, 7, dark);
            Part(art, "Pump grip", 166, 38, 60, 17, wood);
            for (int i = 0; i < 9; i++) Part(art, "Pump rib", 171 + i * 6, 39, 2, 15, woodLight);
            Part(art, "Trigger guard", 93, 51, 24, 13, dark);
            Part(art, "Trigger guard opening", 97, 51, 16, 9, new Color(.075f, .091f, .104f));
            Part(art, "Front bead", 272, 23, 3, 5, edge);
        }
        else
        {
            Part(art, "Stock", 14, 40, 53, 21, wood, 8);
            Part(art, "Stock edge", 14, 40, 53, 2, woodLight, 8);
            Part(art, "Butt plate", 13, 39, 5, 24, dark, 8);
            Part(art, "Receiver", 68, 30, 97, 22, steel);
            Part(art, "Dust cover", 70, 27, 89, 6, edge);
            Part(art, "Safety", 123, 37, 27, 2, edge, -13);
            Part(art, "Rivet", 82, 40, 3, 3, edge);
            Part(art, "Rivet", 154, 41, 3, 3, edge);
            Part(art, "Pistol grip", 89, 51, 17, 26, wood, -18);
            Part(art, "Trigger guard", 109, 51, 22, 11, dark);
            Part(art, "Trigger guard opening", 113, 51, 14, 7, new Color(.075f, .091f, .104f));
            Part(art, "Magazine top", 134, 51, 21, 18, dark, 5);
            Part(art, "Magazine curve", 138, 64, 23, 12, steel, 25);
            Part(art, "Magazine base", 144, 73, 25, 7, dark, 39);
            for (int i = 0; i < 3; i++) Part(art, "Magazine rib", 139 + i * 5, 54, 1, 17, edge, 8);
            Part(art, "Lower handguard", 165, 36, 48, 18, wood);
            Part(art, "Upper handguard", 166, 27, 43, 6, woodLight);
            Part(art, "Wood grain", 170, 44, 37, 1, woodLight, 3);
            Part(art, "Gas tube", 208, 27, 37, 5, steel);
            Part(art, "Barrel", 212, 39, 68, 5, steel);
            Part(art, "Front sight", 259, 25, 6, 16, dark);
            Part(art, "Sight post", 260, 22, 4, 5, edge);
            Part(art, "Muzzle brake", 280, 35, 12, 10, dark);
            Part(art, "Cleaning rod", 211, 48, 67, 1, edge);
        }
    }

    void Part(Transform parent, string name, float x, float y, float w, float h, Color color, float rotation = 0)
    {
        Image part = Box(parent, name, new Rect(x, y, w, h), color);
        part.rectTransform.localRotation = Quaternion.Euler(0, 0, rotation);
    }

    Button ActionButton(Transform parent, string name, string label, Rect bounds, out Text text)
    {
        Image face = Box(parent, name, bounds, raised != null ? Color.white : new Color(.24f, .28f, .31f));
        face.sprite = raised; face.type = Image.Type.Sliced; face.raycastTarget = true;
        Button button = face.gameObject.AddComponent<Button>(); button.targetGraphic = face;
        ColorBlock colors = ColorBlock.defaultColorBlock;
        colors.highlightedColor = colors.selectedColor = new Color(1.17f, 1.20f, 1.23f);
        colors.pressedColor = new Color(.77f, .83f, .85f); colors.disabledColor = new Color(.52f, .57f, .58f, .85f); colors.fadeDuration = .06f;
        button.colors = colors;
        text = Label(face.transform, "Label", label, new Rect(10, 0, bounds.width - 20, bounds.height), 14, White, TextAnchor.MiddleCenter);
        FacilityDesktopButton feedback = face.gameObject.AddComponent<FacilityDesktopButton>();
        feedback.face = face; feedback.raisedSprite = raised; feedback.pressedSprite = pressed; feedback.hoverSprite = hover;
        feedback.content = text.rectTransform;
        button.onClick.AddListener(feedback.Activate);
        return button;
    }

    static GameObject Full(Transform parent, string name)
    {
        var item = new GameObject(name, typeof(RectTransform)); item.transform.SetParent(parent, false);
        var rect = item.GetComponent<RectTransform>(); rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
        return item;
    }

    static Image Box(Transform parent, string name, Rect bounds, Color color)
    {
        var item = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)); item.transform.SetParent(parent, false);
        RectTransform rect = item.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition = new Vector2(bounds.x, -bounds.y); rect.sizeDelta = bounds.size;
        Image result = item.GetComponent<Image>(); result.color = color; result.raycastTarget = false;
        return result;
    }

    Text Label(Transform parent, string name, string value, Rect bounds, int size, Color color, TextAnchor alignment = TextAnchor.MiddleLeft)
    {
        var item = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text)); item.transform.SetParent(parent, false);
        RectTransform rect = item.GetComponent<RectTransform>(); rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition = new Vector2(bounds.x, -bounds.y); rect.sizeDelta = bounds.size;
        Text result = item.GetComponent<Text>(); result.text = value; result.font = font; result.fontSize = size; result.color = color;
        result.alignment = alignment; result.raycastTarget = false; result.resizeTextForBestFit = false;
        result.horizontalOverflow = HorizontalWrapMode.Wrap; result.verticalOverflow = VerticalWrapMode.Truncate;
        return result;
    }

    void OnDestroy()
    {
        RestoreUnderlyingControls();
        FacilityEconomy.Changed -= Refresh;
    }
}
