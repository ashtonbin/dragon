using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>Explicitly skins the saved menu objects, retaining their session and button references.</summary>
public static class FacilityDesktopMenuAuthoring
{
    const string Root = "Assets/Generated/Interface/Desktop";
    static readonly Color TextColor = new Color(.88f,.89f,.91f);
    static readonly Color Muted = new Color(.61f,.63f,.66f);
    static readonly Color Client = new Color(.12f,.13f,.145f);
    static Sprite raised, pressed, hover, frame, shadow, wallpaper, glass;
    static Font font;

    [MenuItem("Tools/Facility/Apply Desktop Menus")]
    public static void Install()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || Lightmapping.isRunning) throw new InvalidOperationException("Leave Play mode and finish baking before editing menus.");
        Scene original = SceneManager.GetActiveScene();
        string originalPath = original.path;
        Recovery(original);
        if (!EditorSceneManager.SaveScene(original)) throw new IOException("Working scene save failed; menu changes stopped.");
        foreach (string path in EditorBuildSettings.scenes.Where(s=>s.enabled).Select(s=>s.path).ToArray())
        {
            Scene scene = SceneManager.GetActiveScene().path == path ? SceneManager.GetActiveScene() : EditorSceneManager.OpenScene(path);
            Recovery(scene);
            FacilityGameSession session = FacilitySceneGenerator.FindInScene<FacilityGameSession>(scene);
            if (session == null) continue;
            Apply(session);
            AssetDatabase.SaveAssets(); EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Could not save desktop menus in "+path);
        }
        EditorSceneManager.OpenScene(originalPath);
    }

    static void Recovery(Scene scene)
    {
        string path = AssetDatabase.GenerateUniqueAssetPath("Assets/Scenes/Recovery/BeforeDesktopMenus_"+DateTime.Now.ToString("yyyyMMdd_HHmmss")+".unity");
        if (!EditorSceneManager.SaveScene(scene,path,true)) throw new IOException("Recovery save failed; menu restyling stopped.");
    }

    public static void Apply(FacilityGameSession session)
    {
        Prepare();
        foreach (Button button in new[]{session.startButton,session.mainQuitButton,session.storyButton,session.sandboxButton,session.modeBackButton,
            session.resumeButton,session.restartButton,session.pauseQuitButton,session.deathRestartButton,session.deathQuitButton,
            session.observerSpawnButton,session.zombieSpawnButton,session.returnMainButton,session.completionQuitButton})
            button.gameObject.SetActive(true);
        Main(session); Mode(session); Dialog(session,false); Dialog(session,true); Browser(session); Complete(session);
        // Subtle neutral framing connects the in-game overlays to the same computer display.
        foreach (string name in new[]{"Health Backing","Health Track"})
        { Transform node=session.hud.transform.Find(name); if(node!=null) node.GetComponent<Image>().color=name=="Health Track"?new Color(.17f,.18f,.20f):new Color(.08f,.085f,.095f,.9f); }
        FacilityMenuAudioAuthoring.Apply(session);
        foreach (GameObject menu in Menus(session)) foreach (Button button in menu.GetComponentsInChildren<Button>(true)) StyleButton(button);
        EditorUtility.SetDirty(session);
    }

    public static GameObject[] Menus(FacilityGameSession session) => new[]{session.mainMenu,session.modeMenu,session.pauseMenu,session.deathMenu,session.spawnMenu,session.completionMenu};

    static Transform Begin(GameObject menu)
    {
        Transform old=menu.transform.Find("Desktop Skin"); if(old!=null) Object.DestroyImmediate(old.gameObject);
        Transform skin=Full(menu.transform,"Desktop Skin"); skin.SetAsFirstSibling(); return skin;
    }
    static void Main(FacilityGameSession s)
    {
        Transform skin=Begin(s.mainMenu);
        foreach(Image image in s.mainMenu.GetComponentsInChildren<Image>(true)) if(!image.transform.IsChildOf(skin)&&image.GetComponentInParent<Button>(true)==null) image.gameObject.SetActive(false);
        foreach(Text label in s.mainMenu.GetComponentsInChildren<Text>(true))
            if(label.GetComponentInParent<Button>(true)==null && label.name!="Game Title" && label.name!="Control Hint" && label.name!="Menu Hint") label.gameObject.SetActive(false);
        Desktop(skin,"Main menu"); Window(skin,s,new Rect(160,100,960,520),"rob the bank",FacilityDesktopWindowAction.Action.Quit);
        TextAt(s.mainMenu.transform.Find("Game Title"),204,225,800,62,44);
        Label(skin,"Welcome","Choose Start to select Story or Sandbox.",206,299,748,32,15,Muted);
        Place(s.startButton.GetComponent<RectTransform>(),204,385,240,54); Place(s.mainQuitButton.GetComponent<RectTransform>(),462,385,200,54);
        TextAt(s.mainMenu.transform.Find("Control Hint"),206,482,790,24,12);
        TextAt(s.mainMenu.transform.Find("Menu Hint"),206,510,790,24,12);
        AppIcon(skin,936,233,80);
        Status(skin,new Rect(160,100,960,520),"Ready","SINGLE PLAYER");
    }
    static void Mode(FacilityGameSession s)
    {
        Transform skin=Begin(s.modeMenu);
        foreach(Image image in s.modeMenu.GetComponentsInChildren<Image>(true)) if(!image.transform.IsChildOf(skin)&&image.GetComponentInParent<Button>(true)==null) image.gameObject.SetActive(false);
        Desktop(skin,"Choose mode"); Window(skin,s,new Rect(80,70,1120,580),"rob the bank — Select mode",FacilityDesktopWindowAction.Action.Back);
        TextAt(s.modeMenu.transform.Find("Game Title"),116,166,1048,48,30);
        Transform choose=s.modeMenu.transform.Find("Choose"); if(choose!=null) { choose.GetComponent<Text>().text="Select a mode"; TextAt(choose,118,224,900,26,14); }
        ModeCard(s.storyButton,116,272); ModeCard(s.sandboxButton,656,272);
        Place(s.modeBackButton.GetComponent<RectTransform>(),116,552,180,46);
        Status(skin,new Rect(80,70,1120,580),"Choose Story or Sandbox","ESC  BACK");
    }
    static void ModeCard(Button button,float x,float y)
    {
        Place(button.GetComponent<RectTransform>(),x,y,508,236);
        foreach(Image image in button.GetComponentsInChildren<Image>(true)) if(image.gameObject!=button.gameObject) image.gameObject.SetActive(false);
        TextAt(button.transform.Find("Name"),26,28,456,49,28);
        TextAt(button.transform.Find("Description"),28,104,452,100,16);
    }
    static void Dialog(FacilityGameSession s,bool dead)
    {
        GameObject menu=dead?s.deathMenu:s.pauseMenu; Transform skin=Begin(menu);
        Transform dim=menu.transform.Find("Dim World"); if(dim!=null) { dim.GetComponent<Image>().color=new Color(.015f,.018f,.022f,.77f); skin.SetSiblingIndex(1); }
        Rect bounds=new Rect(386,150,508,dead?376:420);
        Window(skin,s,bounds,dead?"rob the bank — Session interrupted":"rob the bank — Paused",dead?(FacilityDesktopWindowAction.Action?)null:FacilityDesktopWindowAction.Action.Resume);
        Transform panel=menu.transform.Find("Menu Panel"); Place(panel.GetComponent<RectTransform>(),bounds.x,bounds.y,bounds.width,bounds.height);
        panel.GetComponent<Image>().color=Color.clear; panel.GetComponent<Image>().raycastTarget=false;
        Transform accent=panel.Find("Top Accent"); if(accent!=null) accent.gameObject.SetActive(false);
        TextAt(panel.Find("Title"),32,66,444,43,27); TextAt(panel.Find("Hint"),34,112,440,30,14);
        if(dead)
        { Place(s.deathRestartButton.GetComponent<RectTransform>(),32,168,444,50); Place(s.deathQuitButton.GetComponent<RectTransform>(),32,232,444,50); }
        else
        { Place(s.resumeButton.GetComponent<RectTransform>(),32,163,444,48); Place(s.restartButton.GetComponent<RectTransform>(),32,224,444,48); Place(s.pauseQuitButton.GetComponent<RectTransform>(),32,285,444,48); TextAt(panel.Find("Escape Hint"),34,361,440,24,12); }
    }
    static void Browser(FacilityGameSession s)
    {
        Transform skin=Begin(s.spawnMenu); Window(skin,s,new Rect(40,72,1200,576),"rob the bank — Character browser",FacilityDesktopWindowAction.Action.CloseBrowser);
        Transform browser=s.spawnMenu.transform.Find("Browser"); browser.GetComponent<Image>().color=Color.clear; browser.GetComponent<Image>().raycastTarget=false;
        foreach(string name in new[]{"Top Rule","Header","Title","Close Hint","Category Accent","Footer Rule"}) { Transform item=browser.Find(name); if(item!=null) item.gameObject.SetActive(false); }
        Tint(browser,"Sidebar",new Color(.095f,.10f,.115f)); Tint(browser,"Category Selection",new Color(.23f,.25f,.28f)); Tint(browser,"Footer",new Color(.105f,.115f,.13f));
        Transform sidebar=browser.Find("Sidebar"); Place(sidebar.GetComponent<RectTransform>(),10,45,190,469);
        Place(browser.Find("Footer").GetComponent<RectTransform>(),10,514,1180,52);
        TextAt(browser.Find("Placement Help"),24,531,1140,24,12);
        TextAt(browser.Find("Browse Label"),24,72,152,24,11); TextAt(browser.Find("Characters"),28,126,145,25,15);
        Transform selected=browser.Find("Category Selection"); Place(selected.GetComponent<RectTransform>(),14,118,172,42);
        TextAt(browser.Find("Contents Label"),226,63,550,25,12);
        TextAt(browser.Find("Contents Hint"),226,98,900,35,21);
        TextAt(browser.Find("Live World Hint"),24,473,160,24,11);
        foreach(Button card in new[]{s.observerSpawnButton,s.zombieSpawnButton})
        {
            Tint(card.transform,"Portrait Backing",new Color(.11f,.12f,.14f)); Tint(card.transform,"Floor",new Color(.08f,.09f,.105f));
            TextAt(card.transform.Find("Name"),18,175,290,28,20); TextAt(card.transform.Find("Description"),18,209,290,25,12);
        }
        foreach(Text text in browser.GetComponentsInChildren<Text>(true)) if(text.color.g>text.color.r*1.25f) text.color=Muted;
    }
    static void Complete(FacilityGameSession s)
    {
        Transform skin=Begin(s.completionMenu);
        foreach(Image image in s.completionMenu.GetComponentsInChildren<Image>(true)) if(!image.transform.IsChildOf(skin)&&image.GetComponentInParent<Button>(true)==null) image.gameObject.SetActive(false);
        Desktop(skin,"Job complete"); Window(skin,s,new Rect(310,100,660,550),"rob the bank — Job complete",FacilityDesktopWindowAction.Action.MainMenu);
        TextAt(s.completionTitleText.transform,348,183,584,56,30); TextAt(s.completionBodyText.transform,350,260,580,88,18);
        Place(s.nextLevelButton.GetComponent<RectTransform>(),350,373,580,50); Place(s.returnMainButton.GetComponent<RectTransform>(),350,439,580,50); Place(s.completionQuitButton.GetComponent<RectTransform>(),350,505,580,46);
        Status(skin,new Rect(310,100,660,550),"Session complete","CASH RECOVERED");
    }

    static void Desktop(Transform parent,string current)
    {
        Image bg=Box(parent,"Desktop background",0,0,1280,720,Color.white); bg.sprite=wallpaper;
        Box(parent,"Taskbar dark rim",0,675,1280,45,new Color(.025f,.028f,.035f));
        Image bar=Box(parent,"Taskbar",0,677,1280,43,Color.white); bar.sprite=glass;
        Box(parent,"Taskbar top highlight",0,677,1280,1,new Color(.48f,.50f,.53f,.5f));
        AppIcon(parent,22,689,20);
        Label(parent,"Taskbar app","rob the bank",54,684,260,30,13,TextColor);
        Label(parent,"Taskbar status",current,898,684,360,30,12,Muted,TextAnchor.MiddleRight);
    }
    static void Window(Transform parent,FacilityGameSession session,Rect r,string title,FacilityDesktopWindowAction.Action? close)
    {
        Image drop=Box(parent,"Window shadow",r.x+7,r.y+9,r.width,r.height,Color.white); drop.sprite=shadow; drop.type=Image.Type.Sliced;
        Image border=Box(parent,"Window Frame",r.x,r.y,r.width,r.height,Color.white); border.sprite=frame; border.type=Image.Type.Sliced;
        Image top=Box(parent,"Glass title bar",r.x+6,r.y+6,r.width-12,36,Color.white); top.sprite=glass;
        Box(parent,"Title bar reflection",r.x+8,r.y+7,r.width-16,15,new Color(1,1,1,.055f));
        Box(parent,"Title bar lower rim",r.x+7,r.y+42,r.width-14,1,new Color(.035f,.038f,.045f));
        AppIcon(parent,r.x+17,r.y+16,14);
        Label(parent,"Window caption",title,r.x+42,r.y+8,r.width-112,29,13,new Color(.91f,.92f,.94f));
        Box(parent,"Client area",r.x+9,r.y+45,r.width-18,r.height-54,Client);
        if(close.HasValue)
        {
            Image image=Box(parent,"Close Window",r.x+r.width-57,r.y+7,40,27,Color.white); image.raycastTarget=true;
            Button button=image.gameObject.AddComponent<Button>(); button.targetGraphic=image;
            Text glyph=Label(button.transform,"Label","×",0,-1,40,27,18,TextColor,TextAnchor.MiddleCenter);
            glyph.verticalOverflow=VerticalWrapMode.Overflow;
            FacilityDesktopWindowAction action=image.gameObject.AddComponent<FacilityDesktopWindowAction>(); action.session=session; action.action=close.Value;
            FacilityDesktopButton feedback=image.gameObject.AddComponent<FacilityDesktopButton>();
            UnityEventTools.AddPersistentListener(button.onClick,feedback.Activate);
            UnityEventTools.AddPersistentListener(button.onClick,action.Activate);
        }
    }
    static void Status(Transform parent,Rect r,string left,string right)
    {
        Box(parent,"Status inset",r.x+11,r.y+r.height-39,r.width-22,27,new Color(.085f,.09f,.105f));
        Box(parent,"Status top highlight",r.x+12,r.y+r.height-39,r.width-24,1,new Color(.25f,.27f,.29f));
        Box(parent,"Ready indicator",r.x+24,r.y+r.height-27,5,5,new Color(.44f,.62f,.51f));
        Label(parent,"Status",left,r.x+39,r.y+r.height-36,r.width*.6f,22,11,Muted);
        Label(parent,"Status right",right,r.x+r.width*.6f,r.y+r.height-36,r.width*.4f-24,22,10,Muted,TextAnchor.MiddleRight);
    }
    static void AppIcon(Transform parent,float x,float y,float size)
    {
        Box(parent,"Application icon backing",x,y,size,size,new Color(.14f,.17f,.18f));
        Box(parent,"Bank icon roof",x+size*.14f,y+size*.17f,size*.72f,size*.10f,new Color(.68f,.72f,.72f));
        for(int i=0;i<3;i++) Box(parent,"Bank icon column",x+size*(.21f+i*.24f),y+size*.32f,size*.10f,size*.39f,new Color(.57f,.62f,.63f));
        Box(parent,"Bank icon base",x+size*.14f,y+size*.77f,size*.72f,size*.08f,new Color(.68f,.72f,.72f));
    }
    static void StyleButton(Button button)
    {
        Image face=button.targetGraphic as Image; if(face==null) return;
        face.sprite=raised; face.type=Image.Type.Sliced; face.color=Color.white; face.raycastTarget=true;
        foreach(Image image in button.GetComponentsInChildren<Image>(true))
            if(image!=face && (image.name=="Accent" || image.name=="Top Rule")) image.gameObject.SetActive(false);
        bool close=button.name=="Close Window";
        ColorBlock colors=ColorBlock.defaultColorBlock;
        colors.normalColor=close?new Color(.94f,.48f,.42f):Color.white;
        colors.highlightedColor=close?new Color(1.2f,.59f,.50f):new Color(1.16f,1.18f,1.20f);
        colors.selectedColor=colors.highlightedColor;
        colors.pressedColor=close?new Color(.83f,.35f,.29f):new Color(.84f,.86f,.88f);
        colors.disabledColor=new Color(.50f,.51f,.52f,.65f); colors.fadeDuration=.055f;
        button.colors=colors;
        FacilityDesktopButton feedback=button.GetComponent<FacilityDesktopButton>();
        feedback.face=face; feedback.raisedSprite=raised; feedback.pressedSprite=pressed; feedback.hoverSprite=hover;
        Text label=button.GetComponentsInChildren<Text>(true).FirstOrDefault(t=>t.name=="Label" || t.name=="Name");
        feedback.content=label!=null?label.rectTransform:null;
        foreach(Text text in button.GetComponentsInChildren<Text>(true))
        {
            text.font=font; text.color=text.name=="Description"?Muted:TextColor;
            if(text.name=="Label" && !close)
            { Place(text.rectTransform,14,0,button.GetComponent<RectTransform>().sizeDelta.x-28,button.GetComponent<RectTransform>().sizeDelta.y); text.alignment=TextAnchor.MiddleCenter; text.fontSize=18; }
            text.resizeTextForBestFit=false; text.raycastTarget=false;
        }
        EditorUtility.SetDirty(button); EditorUtility.SetDirty(feedback);
    }
    static void Tint(Transform parent,string name,Color color) { Transform node=parent.Find(name); if(node!=null) node.GetComponent<Image>().color=color; }
    static void TextAt(Transform node,float x,float y,float w,float h,int size)
    { if(node==null)return; Text text=node.GetComponent<Text>(); Place(text.rectTransform,x,y,w,h); text.font=font; text.fontSize=size; text.resizeTextForBestFit=false; if(text.color.r<.8f || text.color.g>.25f)text.color=Muted; if(node.name=="Game Title" || node.name=="Title")text.color=TextColor; text.horizontalOverflow=HorizontalWrapMode.Wrap; }
    static Transform Full(Transform parent,string name)
    { Transform item=new GameObject(name,typeof(RectTransform)).transform; item.SetParent(parent,false); RectTransform rect=item.GetComponent<RectTransform>(); rect.anchorMin=Vector2.zero; rect.anchorMax=Vector2.one; rect.offsetMin=rect.offsetMax=Vector2.zero; return item; }
    static void Place(RectTransform rect,float x,float y,float w,float h)
    { rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(0,1); rect.anchoredPosition=new Vector2(x,-y); rect.sizeDelta=new Vector2(w,h); }
    static Image Box(Transform parent,string name,float x,float y,float w,float h,Color color)
    { var item=new GameObject(name,typeof(RectTransform),typeof(CanvasRenderer),typeof(Image)); item.transform.SetParent(parent,false); Place(item.GetComponent<RectTransform>(),x,y,w,h); Image image=item.GetComponent<Image>(); image.color=color; image.raycastTarget=false; return image; }
    static Text Label(Transform parent,string name,string value,float x,float y,float w,float h,int size,Color color,TextAnchor align=TextAnchor.MiddleLeft)
    { var item=new GameObject(name,typeof(RectTransform),typeof(CanvasRenderer),typeof(Text)); item.transform.SetParent(parent,false); Text text=item.GetComponent<Text>(); Place(text.rectTransform,x,y,w,h); text.text=value; text.font=font; text.fontSize=size; text.color=color; text.alignment=align; text.horizontalOverflow=HorizontalWrapMode.Wrap; text.raycastTarget=false; return text; }

    static void Prepare()
    {
        Folder(Root); font=AssetDatabase.LoadAssetAtPath<Font>(FacilityHeistInterfaceAuthoring.FontPath);
        if(font==null) throw new InvalidOperationException("JetBrains Mono is missing.");
        raised=Sprite("Raised button",0); pressed=Sprite("Pressed button",1); hover=Sprite("Hover button",2);
        frame=Sprite("Window frame",3); shadow=Sprite("Window shadow",4); wallpaper=Sprite("Desktop wallpaper",5); glass=Sprite("Glass title bar",6);
    }

    public static void Capture()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Capture the saved menus outside Play mode.");
        FacilityGameSession session=FacilitySceneGenerator.FindInScene<FacilityGameSession>(SceneManager.GetActiveScene());
        Canvas canvas=session.canvas; GameObject[] menus=Menus(session);
        bool[] active=menus.Select(m=>m.activeSelf).ToArray(); bool hudActive=session.hud.activeSelf;
        bool objectiveActive=session.objectiveHud.activeSelf,placementActive=session.placementHud.activeSelf;
        CanvasScaler scaler=canvas.GetComponent<CanvasScaler>(); bool scalerEnabled=scaler.enabled;
        RenderMode mode=canvas.renderMode; Camera oldCamera=canvas.worldCamera; float plane=canvas.planeDistance,scaleFactor=canvas.scaleFactor;
        Vector3 position=canvas.transform.localPosition,scale=canvas.transform.localScale; Quaternion rotation=canvas.transform.localRotation;
        RenderTexture prior=RenderTexture.active; var target=new RenderTexture(1280,720,24,RenderTextureFormat.ARGB32);
        GameObject cameraObject=new GameObject("Temporary desktop preview camera",typeof(Camera)); Camera camera=cameraObject.GetComponent<Camera>();
        int[] layers=canvas.GetComponentsInChildren<Transform>(true).Select(t=>t.gameObject.layer).ToArray();
        Transform[] nodes=canvas.GetComponentsInChildren<Transform>(true);
        try
        {
            target.Create(); camera.enabled=false; camera.targetTexture=target; camera.orthographic=true; camera.orthographicSize=360;
            camera.transform.position=new Vector3(10000,10000,-10000); camera.nearClipPlane=.1f; camera.farClipPlane=100;
            camera.clearFlags=CameraClearFlags.SolidColor; camera.backgroundColor=new Color(.035f,.04f,.05f); camera.allowHDR=false;
            camera.cullingMask=1<<5;
            foreach(Transform node in nodes)node.gameObject.layer=5;
            scaler.enabled=false; canvas.renderMode=RenderMode.ScreenSpaceCamera; canvas.worldCamera=camera; canvas.planeDistance=10; canvas.scaleFactor=1;
            session.objectiveHud.SetActive(false); session.placementHud.SetActive(false);
            string directory=Path.GetFullPath(".codex/desktop-previews"); Directory.CreateDirectory(directory);
            string[] names={"01_main","02_modes","03_pause","04_death","05_spawn","06_complete"};
            for(int i=0;i<menus.Length;i++)
            {
                for(int j=0;j<menus.Length;j++)menus[j].SetActive(i==j);
                session.hud.SetActive(i==2||i==4);
                Canvas.ForceUpdateCanvases(); camera.Render(); RenderTexture.active=target;
                Texture2D shot=new Texture2D(1280,720,TextureFormat.RGB24,false);
                try { shot.ReadPixels(new Rect(0,0,1280,720),0,0); shot.Apply(); File.WriteAllBytes(Path.Combine(directory,names[i]+".png"),shot.EncodeToPNG()); }
                finally { Object.DestroyImmediate(shot); }
            }
        }
        finally
        {
            for(int i=0;i<menus.Length;i++)menus[i].SetActive(active[i]);
            session.hud.SetActive(hudActive); session.objectiveHud.SetActive(objectiveActive); session.placementHud.SetActive(placementActive);
            for(int i=0;i<nodes.Length;i++)nodes[i].gameObject.layer=layers[i];
            canvas.renderMode=mode; canvas.worldCamera=oldCamera; canvas.planeDistance=plane; canvas.scaleFactor=scaleFactor;
            canvas.transform.localPosition=position; canvas.transform.localRotation=rotation; canvas.transform.localScale=scale; scaler.enabled=scalerEnabled;
            RenderTexture.active=prior; target.Release(); Object.DestroyImmediate(target); Object.DestroyImmediate(cameraObject); Canvas.ForceUpdateCanvases();
        }
    }
    static void Folder(string path)
    { if(AssetDatabase.IsValidFolder(path))return; string parent=Path.GetDirectoryName(path).Replace('\\','/'); Folder(parent); AssetDatabase.CreateFolder(parent,Path.GetFileName(path)); }
    static Sprite Sprite(string name,int kind)
    {
        string path=Root+"/"+name+".asset";
        foreach(Object asset in AssetDatabase.LoadAllAssetsAtPath(path)) if(asset is Sprite existing)return existing;
        const int n=64;
        Texture2D texture=new Texture2D(n,n,TextureFormat.RGBA32,false){name=name,filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp};
        Color[] pixels=new Color[n*n];
        for(int y=0;y<n;y++)for(int x=0;x<n;x++)
        {
            float t=y/(float)(n-1); int edge=Mathf.Min(x,y,n-1-x,n-1-y);
            Color color;
            if(kind==5)
            { float glow=Mathf.Clamp01(1-Vector2.Distance(new Vector2(x/(float)n,t),new Vector2(.8f,.8f))); color=new Color(.045f,.05f,.058f)+new Color(.06f,.061f,.064f)*glow; color.a=1; }
            else if(kind==6) color=t>.52f?Color.Lerp(new Color(.35f,.37f,.40f),new Color(.45f,.47f,.50f),(t-.52f)/.48f):Color.Lerp(new Color(.18f,.20f,.225f),new Color(.29f,.31f,.34f),t/.52f);
            else if(kind==4)
            { color=new Color(0,0,0,Mathf.Clamp01(edge/8f)*.4f); }
            else
            {
                color=kind==3?Client:Color.Lerp(new Color(.20f,.215f,.24f),new Color(.39f,.41f,.44f),t);
                if(kind==1)color=Color.Lerp(new Color(.24f,.26f,.29f),new Color(.15f,.17f,.19f),t);
                if(kind==2)color*=1.10f;
                if(edge==0)color=new Color(.035f,.04f,.05f);
                else if(edge==1)
                { bool bright=x==1||y==n-2; if(kind==1)bright=!bright; color=bright?new Color(.58f,.60f,.63f):new Color(.10f,.115f,.135f); }
                else if(edge==2)color=kind==1?new Color(.11f,.12f,.14f):new Color(.28f,.30f,.33f);
                if(kind==3&&edge>2&&edge<6)color=new Color(.26f,.28f,.31f);
                float cx=Mathf.Clamp(x,4,n-5),cy=Mathf.Clamp(y,4,n-5);
                if(Vector2.Distance(new Vector2(x,y),new Vector2(cx,cy))>5)color.a=0;
            }
            pixels[y*n+x]=color;
        }
        texture.SetPixels(pixels); texture.Apply(false,true); AssetDatabase.CreateAsset(texture,path);
        Sprite sprite=UnityEngine.Sprite.Create(texture,new Rect(0,0,n,n),new Vector2(.5f,.5f),100,0,SpriteMeshType.FullRect,new Vector4(8,8,8,8));
        sprite.name=name; AssetDatabase.AddObjectToAsset(sprite,path); AssetDatabase.SaveAssets(); return sprite;
    }
}
