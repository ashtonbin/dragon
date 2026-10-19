using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public static class FacilityBankValidation
{
    public static string Validate(Scene scene)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Validate the saved bank in Edit mode.");
        FacilityStoryDirector director = FacilitySceneGenerator.FindInScene<FacilityStoryDirector>(scene);
        if (director == null) throw new InvalidOperationException("Open the bank story scene.");
        Physics.SyncTransforms();
        Vector3[] samples = { new Vector3(0,1,-5.5f),new Vector3(0,1,-3.7f),new Vector3(-10,1,-5.5f),new Vector3(10,1,-5.5f),new Vector3(0,1,-7.2f) };
        foreach (Vector3 sample in samples)
        {
            int floors = 0;
            foreach (RaycastHit hit in Physics.RaycastAll(sample,Vector3.down,1.1f,~0,QueryTriggerInteraction.Ignore))
                if (hit.normal.y > .9f && Mathf.Abs(hit.point.y) < .02f) floors++;
            if (floors != 1) throw new InvalidOperationException("Overlapping or missing entrance ground at "+sample+": "+floors+" surfaces.");
        }
        foreach (FacilityKnifeGuard guard in director.guards)
        {
            Vector3 at = guard.transform.position;
            foreach (Collider collider in Physics.OverlapCapsule(at+Vector3.up*.32f,at+Vector3.up*1.46f,.275f,~0,QueryTriggerInteraction.Ignore))
                if (!collider.transform.IsChildOf(guard.transform)) throw new InvalidOperationException(guard.name+" overlaps "+collider.name+" at its saved placement.");
        }
        ValidateTextDepth();
        return "Bank placement validation passed: five entrance samples have one ground surface each; all five NPC capsules clear; POLICE font renders visibly and is completely occluded by opaque cover.";
    }
    static void ValidateTextDepth()
    {
        GameObject temporary = new GameObject("Temporary world text validation");
        RenderTexture target = new RenderTexture(512,256,24,RenderTextureFormat.ARGB32);
        RenderTexture previous = RenderTexture.active;
        bool fog = RenderSettings.fog;
        Material lettering = null, opaque = null;
        try
        {
            RenderSettings.fog = false; target.Create();
            GameObject cameraObject = new GameObject("Diagnostic camera",typeof(Camera)); cameraObject.transform.SetParent(temporary.transform);
            Camera camera = cameraObject.GetComponent<Camera>(); camera.enabled = false;
            camera.transform.position = new Vector3(100,2,100); camera.orthographic = true; camera.orthographicSize = 1;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black; camera.targetTexture = target; camera.allowHDR = false;
            GameObject labelObject = new GameObject("Police font sample",typeof(TextMesh)); labelObject.transform.SetParent(temporary.transform);
            labelObject.transform.position = new Vector3(100,2,104);
            TextMesh label = labelObject.GetComponent<TextMesh>(); label.font = AssetDatabase.LoadAssetAtPath<Font>(FacilityHeistInterfaceAuthoring.FontPath);
            label.text = "POLICE"; label.fontSize = 64; label.characterSize = .28f; label.anchor = TextAnchor.MiddleCenter;
            label.font.RequestCharactersInTexture(label.text,label.fontSize);
            lettering = new Material(AssetDatabase.LoadAssetAtPath<Material>("Assets/Generated/Bank/Materials/Police depth tested text.mat"));
            lettering.SetTexture("_MainTex",label.font.material.mainTexture); label.GetComponent<MeshRenderer>().sharedMaterial = lettering;
            int visible = Capture(camera,target,"bank-police-visible");
            GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube); wall.transform.SetParent(temporary.transform);
            wall.transform.position = new Vector3(100,2,102); wall.transform.localScale = new Vector3(5,3,.2f);
            opaque = new Material(Shader.Find("Universal Render Pipeline/Unlit")); opaque.SetColor("_BaseColor",new Color(.04f,.10f,.16f)); wall.GetComponent<Renderer>().sharedMaterial = opaque;
            int occluded = Capture(camera,target,"bank-police-occluded");
            if (visible < 30 || occluded > 2) throw new InvalidOperationException("World POLICE depth check failed: visible white pixels="+visible+", covered="+occluded);
        }
        finally
        {
            RenderSettings.fog = fog; RenderTexture.active = previous; Object.DestroyImmediate(temporary);
            if (lettering != null) Object.DestroyImmediate(lettering); if (opaque != null) Object.DestroyImmediate(opaque);
            target.Release(); Object.DestroyImmediate(target);
        }
    }
    static int Capture(Camera camera,RenderTexture target,string name)
    {
        camera.Render(); RenderTexture.active = target;
        Texture2D image = new Texture2D(target.width,target.height,TextureFormat.RGB24,false);
        try
        {
            image.ReadPixels(new Rect(0,0,target.width,target.height),0,0); image.Apply();
            File.WriteAllBytes(Path.Combine(".codex",name+".png"),image.EncodeToPNG());
            int white=0; foreach (Color32 pixel in image.GetPixels32()) if (pixel.r>180 && pixel.g>180 && pixel.b>180) white++;
            return white;
        }
        finally { Object.DestroyImmediate(image); }
    }
}
