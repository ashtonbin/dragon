using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

/// <summary>Builds ordinary, editable uGUI objects. Called only by the explicit menu installer.</summary>
public static class FacilityMenuAuthoring
{
    static readonly Color Ink = new Color(.028f, .043f, .055f, 1);
    static readonly Color Panel = new Color(.055f, .077f, .09f, .98f);
    static readonly Color Card = new Color(.085f, .115f, .13f, 1);
    static readonly Color Line = new Color(.19f, .26f, .28f, 1);
    static readonly Color White = new Color(.87f, .9f, .89f, 1);
    static readonly Color Muted = new Color(.46f, .57f, .59f, 1);
    static readonly Color Blue = new Color(.42f, .7f, .75f, 1);
    static readonly Color Orange = new Color(.83f, .38f, .17f, 1);

    public static void Build(Transform parent, FacilityGameSession session)
    {
        GameObject canvasObject = new GameObject("Facility Interface", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(parent, false);
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 30;
        session.canvas = canvas;
        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280, 720);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = .5f;
        Transform root = canvasObject.transform;

        session.hud = FullRoot(root, "HUD");
        BuildHud(session.hud.transform, session);
        session.placementHud = FullRoot(root, "Placement HUD");
        Rect(session.placementHud.transform, "Placement Backing", 360, 598, 560, 66, new Color(.025f, .042f, .05f, .92f));
        Rect(session.placementHud.transform, "Placement Accent", 360, 598, 560, 2, Blue);
        session.placementText = Label(session.placementHud.transform, "Selected Character", "Place The Observer", 378, 606, 524, 27, 18, White, TextAnchor.MiddleCenter);
        Label(session.placementHud.transform, "Placement Controls", "LMB  PLACE    RMB  CANCEL    TAB  BROWSE", 378, 634, 524, 21, 12, Muted, TextAnchor.MiddleCenter);
        GameObject damage = FullRoot(root, "Damage Flash");
        session.damageOverlay = damage.AddComponent<Image>();
        session.damageOverlay.color = new Color(.68f, .015f, .015f, 0);
        session.damageOverlay.raycastTarget = false;

        session.mainMenu = FullRoot(root, "Main Menu");
        BuildMain(session.mainMenu.transform, session);
        session.spawnMenu = FullRoot(root, "Spawn Menu");
        BuildSpawn(session.spawnMenu.transform, session);
        session.pauseMenu = FullRoot(root, "Pause Menu");
        BuildPause(session.pauseMenu.transform, session, false);
        session.deathMenu = FullRoot(root, "Death Menu");
        BuildPause(session.deathMenu.transform, session, true);

        session.mainMenu.SetActive(true);
        session.hud.SetActive(false);
        session.placementHud.SetActive(false);
        session.spawnMenu.SetActive(false);
        session.pauseMenu.SetActive(false);
        session.deathMenu.SetActive(false);

        if (Object.FindFirstObjectByType<EventSystem>(FindObjectsInactive.Include) == null)
        {
            GameObject events = new GameObject("Menu Event System", typeof(EventSystem), typeof(InputSystemUIInputModule));
            events.transform.SetParent(parent, false);
            events.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
        }
        FacilityModeUIAuthoring.Install(session);
        FacilityHeistInterfaceAuthoring.Install(session);
        EditorUtility.SetDirty(session);
    }

    static void BuildMain(Transform root, FacilityGameSession session)
    {
        // A quiet, graphic interpretation of the facility: planted columns and a distant doorway.
        // Every element is a normal editable RectTransform; no image or per-frame drawing is needed.
        Rect(root, "Backdrop", 0, 0, 1280, 720, Ink, true);
        Rect(root, "Back Wall", 632, 116, 648, 506, new Color(.038f, .065f, .079f));
        Rect(root, "Ceiling", 632, 0, 648, 117, new Color(.023f, .036f, .045f));
        Rect(root, "Floor", 632, 622, 648, 98, new Color(.018f, .03f, .038f));
        Rect(root, "Door Recess", 917, 237, 208, 385, new Color(.017f, .035f, .044f));
        Rect(root, "Door", 938, 259, 167, 363, new Color(.029f, .055f, .065f));
        Rect(root, "Door Jamb Left", 916, 237, 12, 385, Line * .67f);
        Rect(root, "Door Header", 916, 237, 209, 10, Line * .67f);
        Rect(root, "Door Jamb Right", 1115, 237, 10, 385, Line * .67f);
        Rect(root, "Door Centre Seam", 1020, 260, 2, 362, new Color(.018f, .038f, .047f));
        Rect(root, "Fluorescent Housing", 926, 204, 190, 12, new Color(.10f, .16f, .18f));
        Rect(root, "Fluorescent Strip", 936, 207, 170, 3, new Color(.30f, .48f, .51f));
        Rect(root, "Left Column", 726, 105, 38, 526, new Color(.035f, .055f, .066f));
        Rect(root, "Left Column Face", 733, 105, 5, 526, new Color(.09f, .13f, .145f));
        Rect(root, "Right Column", 1200, 105, 44, 526, new Color(.03f, .048f, .06f));
        Rect(root, "Right Column Face", 1200, 105, 5, 526, new Color(.10f, .14f, .15f));
        Rect(root, "Lower Service Pipe", 764, 542, 152, 5, new Color(.09f, .13f, .14f));
        Rect(root, "Upper Service Pipe", 764, 553, 152, 3, new Color(.075f, .11f, .12f));
        Rect(root, "Emergency Plate", 1162, 351, 18, 50, new Color(.105f, .077f, .065f));
        Rect(root, "Emergency Lamp", 1167, 359, 7, 30, Orange * .66f);
        // The opaque left sheet gives the title and controls a calm, consistent contrast.
        Rect(root, "Title Ground", 0, 0, 654, 720, Ink);
        Rect(root, "Title Rule", 80, 70, 44, 3, Blue);
        Label(root, "Game Title", "rob the bank", 80, 98, 1110, 88, 62, White);
        session.startButton = Action(root, "Start", "Start", 84, 334, 306, 62, true);
        session.mainQuitButton = Action(root, "Quit", "Quit", 84, 410, 306, 62, false);
        Label(root, "Control Hint", "WASD  MOVE    MOUSE  LOOK    LEFT CLICK  FIRE", 84, 590, 540, 24, 13, Muted);
        Label(root, "Menu Hint", "TAB  SPAWN MENU    ESC  PAUSE", 84, 618, 460, 24, 13, Muted);
        Rect(root, "Footer Rule", 84, 671, 1112, 1, Line * .6f);
        Label(root, "Prototype Tag", "FACILITY PROTOTYPE", 84, 681, 400, 20, 11, Muted);
    }

    static void BuildHud(Transform root, FacilityGameSession session)
    {
        Rect(root, "Health Backing", 24, 24, 188, 62, new Color(.025f, .042f, .05f, .86f));
        Rect(root, "Health Accent", 24, 24, 3, 62, Blue);
        Label(root, "Health Label", "HP", 40, 34, 62, 22, 13, Muted);
        session.hpText = Label(root, "Health Value", "3 / 3", 105, 31, 91, 25, 19, White, TextAnchor.MiddleRight);
        Rect(root, "Health Track", 40, 65, 156, 6, new Color(.11f, .16f, .17f));
        session.healthFill = Rect(root, "Health Fill", 40, 65, 156, 6, Blue).GetComponent<Image>();
        session.healthFill.sprite = FlatSprite();
        session.healthFill.type = Image.Type.Filled;
        session.healthFill.fillMethod = Image.FillMethod.Horizontal;
        session.healthFill.fillOrigin = 0;
        session.healthFill.fillAmount = 1;
        Text hint = Label(root, "Controls", "TAB  SPAWN     ESC  PAUSE", 945, 673, 310, 26, 13, new Color(.66f, .74f, .75f, .8f), TextAnchor.MiddleRight);
        Shadow shadow = hint.gameObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(0, 0, 0, .7f);
        shadow.effectDistance = new Vector2(1, -1);
    }

    static void BuildSpawn(Transform root, FacilityGameSession session)
    {
        // The outer margins deliberately remain open to the world. The browser owns the pointer
        // until a card is selected; placement returns the pointer to the centre of the view.
        Transform window = Rect(root, "Browser", 40, 72, 1200, 576, Panel, true).transform;
        Rect(window, "Top Rule", 0, 0, 1200, 3, Blue);
        Rect(window, "Header", 0, 3, 1200, 61, new Color(.065f, .095f, .11f));
        Label(window, "Title", "SPAWN MENU", 24, 17, 600, 30, 22, White);
        Label(window, "Close Hint", "TAB  CLOSE", 990, 22, 182, 24, 13, Muted, TextAnchor.MiddleRight);
        Rect(window, "Sidebar", 0, 64, 200, 450, new Color(.04f, .061f, .074f));
        Label(window, "Browse Label", "BROWSE", 24, 86, 152, 24, 11, Muted);
        Rect(window, "Category Selection", 14, 126, 172, 42, new Color(.10f, .18f, .21f));
        Rect(window, "Category Accent", 14, 126, 3, 42, Blue);
        Label(window, "Characters", "Characters", 28, 134, 145, 25, 16, White);
        Label(window, "Live World Hint", "World remains live", 24, 473, 160, 24, 12, Muted);
        Label(window, "Contents Label", "CHARACTERS", 226, 88, 550, 25, 13, Muted);
        Label(window, "Contents Hint", "Choose who enters the facility.", 226, 119, 800, 30, 21, White);
        session.observerSpawnButton = CharacterCard(window, "Observer Card", "The Observer", "Quiet. Watches you until provoked.", 226, false);
        session.zombieSpawnButton = CharacterCard(window, "Zombie Card", "Zombie", "Hostile. Approaches and attacks.", 578, true);
        session.spawnSelectionText = Label(window, "Selection", "Select a character to place.", 226, 443, 930, 28, 17, Blue);
        session.spawnStatusText = Label(window, "Status", "", 226, 475, 930, 23, 13, Muted);
        Rect(window, "Footer", 0, 514, 1200, 62, new Color(.04f, .061f, .074f));
        Rect(window, "Footer Rule", 0, 514, 1200, 1, Line);
        Label(window, "Placement Help", "Select a character. Click a clear floor to place. Right click cancels. Tab closes.", 24, 531, 1152, 30, 14, Muted);
    }

    static Button CharacterCard(Transform parent, string objectName, string title, string description, float x, bool zombie)
    {
        GameObject card = Rect(parent, objectName, x, 166, 326, 250, Card, true);
        Button button = ConfigureButton(card);
        // Purpose-built silhouette thumbnails: a recognizable standing humanoid, framed like an
        // item in a sandbox browser, rather than an unrelated stock icon or a runtime screenshot.
        Rect(card.transform, "Portrait Backing", 12, 12, 302, 149, new Color(.045f, .074f, .086f));
        Rect(card.transform, "Floor", 12, 146, 302, 15, new Color(.035f, .057f, .066f));
        Color suit = zombie ? new Color(.38f, .43f, .28f) : new Color(.38f, .5f, .45f);
        Color skin = zombie ? new Color(.49f, .53f, .38f) : new Color(.69f, .66f, .54f);
        Rect(card.transform, "Figure Legs Left", 142, 107, 15, 40, suit * .8f);
        Rect(card.transform, "Figure Legs Right", 166, 107, 15, 40, suit * .8f);
        Rect(card.transform, "Figure Body", 137, 63, 49, 49, suit);
        Rect(card.transform, "Figure Left Arm", 122, zombie ? 66 : 70, 13, zombie ? 29 : 46, suit * .9f);
        Rect(card.transform, "Figure Right Arm", 188, zombie ? 66 : 70, 13, zombie ? 29 : 46, suit * .9f);
        Rect(card.transform, "Figure Head", 144, 30, 35, 34, skin);
        Rect(card.transform, "Left Eye", 150, 41, 6, 8, zombie ? Orange * .7f : Ink);
        Rect(card.transform, "Right Eye", 167, 41, 6, 8, zombie ? Orange * .7f : Ink);
        if (zombie)
        {
            Rect(card.transform, "Torn Sleeve", 188, 86, 13, 9, new Color(.30f, .14f, .10f));
            Rect(card.transform, "Wound", 162, 81, 14, 5, new Color(.28f, .12f, .09f));
        }
        Label(card.transform, "Name", title, 18, 175, 290, 28, 21, White);
        Label(card.transform, "Description", description, 18, 209, 290, 25, 13, Muted);
        return button;
    }

    static void BuildPause(Transform root, FacilityGameSession session, bool dead)
    {
        Rect(root, "Dim World", 0, 0, 1280, 720, new Color(.012f, .022f, .029f, .82f), true);
        Transform panel = Rect(root, "Menu Panel", 404, 150, 472, dead ? 390 : 436, Panel, true).transform;
        Rect(panel, "Top Accent", 0, 0, 472, 3, dead ? Orange : Blue);
        Label(panel, "Title", dead ? "YOU DIED" : "PAUSED", 36, 31, 400, 55, 36, White);
        Label(panel, "Hint", dead ? "you arent finished yet" : "Take a breath.", 38, 92, 400, 27, 15, dead ? new Color(.95f, .12f, .10f) : Muted);
        if (dead)
        {
            session.deathRestartButton = Action(panel, "Restart", "Restart", 36, 163, 400, 59, true);
            session.deathQuitButton = Action(panel, "Quit", "Quit", 36, 238, 400, 59, false);
        }
        else
        {
            session.resumeButton = Action(panel, "Resume", "Resume", 36, 151, 400, 59, true);
            session.restartButton = Action(panel, "Restart", "Restart", 36, 224, 400, 59, false);
            session.pauseQuitButton = Action(panel, "Quit", "Quit", 36, 297, 400, 59, false);
            Label(panel, "Escape Hint", "ESC  RESUME", 36, 382, 400, 22, 12, Muted);
        }
    }

    static Button Action(Transform parent, string name, string label, float x, float y, float width, float height, bool primary)
    {
        GameObject go = Rect(parent, name, x, y, width, height, primary ? new Color(.12f, .22f, .25f) : Card, true);
        Rect(go.transform, "Accent", 0, 0, 3, height, primary ? Blue : Line);
        Label(go.transform, "Label", label, 22, 0, width - 44, height, 24, White);
        return ConfigureButton(go);
    }

    static Button ConfigureButton(GameObject go)
    {
        Button button = go.AddComponent<Button>();
        button.targetGraphic = go.GetComponent<Image>();
        ColorBlock colors = ColorBlock.defaultColorBlock;
        colors.normalColor = Color.white;
        // ColorTint multiplies the authored panel, so values above one provide a clear, restrained
        // hover and keyboard selection state while preserving the panel's blue-grey hue.
        colors.highlightedColor = new Color(1.45f, 1.45f, 1.45f, 1);
        colors.selectedColor = new Color(1.55f, 1.55f, 1.55f, 1);
        colors.pressedColor = new Color(.7f, .85f, .9f, 1);
        colors.disabledColor = new Color(.45f, .45f, .45f, .6f);
        colors.colorMultiplier = 1;
        colors.fadeDuration = .08f;
        button.colors = colors;
        Navigation navigation = button.navigation;
        navigation.mode = Navigation.Mode.Automatic;
        button.navigation = navigation;
        return button;
    }

    static GameObject FullRoot(Transform parent, string name)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        return go;
    }

    static GameObject Rect(Transform parent, string name, float x, float y, float width, float height, Color color, bool raycast = false)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        Place(go.GetComponent<RectTransform>(), x, y, width, height);
        Image image = go.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = raycast;
        return go;
    }

    static Text Label(Transform parent, string name, string value, float x, float y, float width, float height, int size, Color color, TextAnchor alignment = TextAnchor.MiddleLeft)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        go.transform.SetParent(parent, false);
        Place(go.GetComponent<RectTransform>(), x, y, width, height);
        Text text = go.GetComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.text = value;
        text.fontSize = size;
        text.color = color;
        text.alignment = alignment;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Truncate;
        text.raycastTarget = false;
        return text;
    }

    static void Place(RectTransform rect, float x, float y, float width, float height)
    {
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition = new Vector2(x, -y);
        rect.sizeDelta = new Vector2(width, height);
    }

    static Sprite FlatSprite()
    {
        const string path = FacilityAssetLibrary.Root + "/Textures/InterfaceFlatWhite.asset";
        foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(path))
            if (asset is Sprite existing) return existing;
        new FacilityAssetLibrary();
        Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (texture == null)
        {
            texture = new Texture2D(2, 2, TextureFormat.RGBA32, false)
            {
                name = "Interface Flat White", filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp
            };
            texture.SetPixels(new[] { Color.white, Color.white, Color.white, Color.white });
            texture.Apply(false, true);
            AssetDatabase.CreateAsset(texture, path);
        }
        Sprite sprite = Sprite.Create(texture, new UnityEngine.Rect(0, 0, 2, 2), new Vector2(.5f, .5f), 100);
        sprite.name = "Interface Flat White";
        AssetDatabase.AddObjectToAsset(sprite, path);
        AssetDatabase.SaveAssets();
        return sprite;
    }
}
