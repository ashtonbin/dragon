using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Authors a single editable adult character; nothing is generated automatically in Play mode.</summary>
public static class HumanoidModelAuthoring
{
    private const string Root = "Assets/Generated/Combat/Character";
    private static readonly Dictionary<string, Mesh> meshes = new Dictionary<string, Mesh>();
    private static Material cloth, trim, skin, eyes, hair, flesh, blood, bone, barBack, barFill;

    public static FacilityHumanoid Build(Vector3 position, Transform player, Transform parent)
    {
        Folder(Root); Folder(Root + "/Meshes"); Folder(Root + "/Materials"); Folder(Root + "/Audio");
        cloth = Material("Coverall - faded sage", new Color(.28f, .34f, .29f));
        trim = Material("Boots and utility belt", new Color(.075f, .085f, .08f));
        skin = Material("Warm skin", new Color(.65f, .46f, .35f));
        eyes = Material("Black eyes", new Color(.008f, .006f, .005f));
        hair = Material("Cropped dark hair", new Color(.07f, .055f, .045f));
        flesh = Material("Exposed tissue", new Color(.34f, .045f, .055f));
        blood = Material("Dark wet blood", new Color(.27f, .008f, .015f), .16f);
        bone = Material("Bone fragments", new Color(.61f, .45f, .31f));
        barBack = Material("Healthbar dark border", new Color(.075f, .075f, .07f));
        barFill = Material("Healthbar muted fill", new Color(.35f, .26f, .22f));
        ApplyCharacterReadability();

        GameObject root = new GameObject("The Observer - adult maintenance worker");
        root.transform.SetParent(parent, false);
        root.transform.position = position;
        if (player != null)
        {
            Vector3 toward = player.position - position; toward.y = 0;
            if (toward.sqrMagnitude > .01f) root.transform.rotation = Quaternion.LookRotation(toward);
        }
        FacilityHumanoid actor = root.AddComponent<FacilityHumanoid>();
        actor.player = player;
        CharacterController motor = root.AddComponent<CharacterController>();
        motor.height = 1.78f; motor.radius = .27f; motor.center = new Vector3(0, .89f, 0);
        motor.stepOffset = .24f; motor.skinWidth = .035f; motor.slopeLimit = 45;
        motor.minMoveDistance = 0f;
        actor.motor = motor;
        Transform bodyRoot = new GameObject("Body").transform;
        bodyRoot.SetParent(root.transform, false);
        List<Rigidbody> bodies = new List<Rigidbody>();
        List<Collider> colliders = new List<Collider>();
        List<FacilityHitRegion> regions = new List<FacilityHitRegion>();

        Rigidbody pelvis = Body("Pelvis", bodyRoot, new Vector3(0, .84f, 0), new Vector3(.34f, .22f, .25f), 9f, null, Vector3.zero, bodies, colliders);
        Piece("Utility belt", pelvis.transform, new Vector3(0, .105f, 0), new Vector3(.375f, .065f, .28f), trim, BeveledBox());
        Piece("Metal belt buckle", pelvis.transform, new Vector3(0, .105f, .147f), new Vector3(.055f, .045f, .015f), bone, Box());
        FacilityHitRegion hipsRegion = SurfaceRegion(pelvis, new Vector3(.34f, .19f, .25f), cloth, false);
        Describe(hipsRegion, actor, FacilityBodyRegion.Pelvis, null);
        regions.Add(hipsRegion);
        Rigidbody chest = Body("Chest", bodyRoot, new Vector3(0, 1.17f, 0), new Vector3(.40f, .43f, .235f), 16f, pelvis, new Vector3(0, -.22f, 0), bodies, colliders);
        FacilityHitRegion chestRegion = SurfaceRegion(chest, new Vector3(.44f, .47f, .27f), cloth, true);
        Describe(chestRegion, actor, FacilityBodyRegion.Torso, hipsRegion);
        regions.Add(chestRegion);
        // A real seam, collar and simple pockets anchor the coverall's silhouette.
        Piece("Centre closure", chest.transform, new Vector3(0, 0, .144f), new Vector3(.018f, .43f, .012f), trim, Box());
        Piece("Collar left", chest.transform, new Vector3(-.075f, .227f, .06f), new Vector3(.1f, .045f, .15f), cloth, BeveledBox()).localRotation = Quaternion.Euler(0, 0, -12);
        Piece("Collar right", chest.transform, new Vector3(.075f, .227f, .06f), new Vector3(.1f, .045f, .15f), cloth, BeveledBox()).localRotation = Quaternion.Euler(0, 0, 12);
        Transform neckCover = Piece("Neck", chest.transform, new Vector3(0, .3f, 0), new Vector3(.14f, .13f, .135f), skin, Ellipsoid());

        Rigidbody head = Body("Head", bodyRoot, new Vector3(0, 1.63f, .005f), new Vector3(.26f, .32f, .255f), 4f, chest, new Vector3(0, -.14f, 0), bodies, colliders, true);
        FacilityHitRegion headRegion = HeadSurface(head);
        Describe(headRegion, actor, FacilityBodyRegion.Head, chestRegion);
        headRegion.parentStump = TornStump("Open neck after head destruction", chest.transform, new Vector3(0, .30f, 0), true);
        headRegion.parentStump.transform.localScale = new Vector3(1.04f, 1.2f, 1.04f);
        headRegion.cutCovers = new[] { neckCover.gameObject };
        regions.Add(headRegion);
        Piece("Cropped hair cap", head.transform, new Vector3(0, .092f, -.018f), new Vector3(.263f, .15f, .23f), hair, Ellipsoid());
        Piece("Left ear", head.transform, new Vector3(-.133f, -.005f, 0), new Vector3(.055f, .085f, .055f), skin, Ellipsoid());
        Piece("Right ear", head.transform, new Vector3(.133f, -.005f, 0), new Vector3(.055f, .085f, .055f), skin, Ellipsoid());
        Piece("Left black eye", head.transform, new Vector3(-.055f, .022f, .119f), new Vector3(.032f, .04f, .014f), eyes, Ellipsoid());
        Piece("Right black eye", head.transform, new Vector3(.055f, .022f, .119f), new Vector3(.032f, .04f, .014f), eyes, Ellipsoid());
        Piece("Nose", head.transform, new Vector3(0, -.018f, .13f), new Vector3(.041f, .048f, .046f), skin, Ellipsoid());
        Piece("Quiet mouth", head.transform, new Vector3(0, -.071f, .112f), new Vector3(.047f, .007f, .008f), hair, BeveledBox());
        // No giant head, cartoon eyes or infant proportions: a soft face on an adult 1.8m body.

        Transform[] upperArms = new Transform[2], forearms = new Transform[2], thighs = new Transform[2], shins = new Transform[2];
        CharacterJoint[] forearmJoints = new CharacterJoint[2];
        GameObject[] upperArmStumps = new GameObject[2], forearmStumps = new GameObject[2];
        for (int i = 0; i < 2; i++)
        {
            float sign = i == 0 ? -1f : 1f;
            string side = i == 0 ? "L" : "R";
            Rigidbody arm = Body("Upper Arm " + side, bodyRoot, new Vector3(sign * .29f, 1.39f, 0), new Vector3(.15f, .29f, .17f), 2.2f, chest, Vector3.zero, bodies, colliders, false, new Vector3(0, -.145f, 0));
            FacilityHitRegion armRegion = SurfaceRegion(arm, new Vector3(.16f, .29f, .18f), cloth, false, new Vector3(0, -.145f, 0));
            Describe(armRegion, actor, i == 0 ? FacilityBodyRegion.UpperArmLeft : FacilityBodyRegion.UpperArmRight, chestRegion);
            armRegion.parentStump = TornStump("Open shoulder " + side, chest.transform, new Vector3(sign * .29f, .22f, 0), false);
            armRegion.detachedStump = TornStump("Detached arm shoulder " + side, arm.transform, Vector3.zero, true);
            regions.Add(armRegion);
            Rigidbody forearm = Body("Forearm " + side, bodyRoot, new Vector3(sign * .29f, 1.10f, 0), new Vector3(.125f, .28f, .14f), 1.5f, arm, Vector3.zero, bodies, colliders, false, new Vector3(0, -.14f, 0));
            BoxCollider forearmCollider = forearm.GetComponent<BoxCollider>();
            forearmCollider.center = new Vector3(0, -.185f, .012f);
            forearmCollider.size = new Vector3(.13f, .37f, .15f); // Includes the hand, avoiding fingers falling through the floor.
            FacilityHitRegion forearmRegion = SurfaceRegion(forearm, new Vector3(.135f, .28f, .15f), cloth, false, new Vector3(0, -.14f, 0));
            Describe(forearmRegion, actor, i == 0 ? FacilityBodyRegion.ForearmLeft : FacilityBodyRegion.ForearmRight, armRegion);
            regions.Add(forearmRegion);
            Piece("Sleeve cuff", forearm.transform, new Vector3(0, -.255f, 0), new Vector3(.145f, .045f, .16f), trim, BeveledBox());
            Piece("Hand", forearm.transform, new Vector3(0, -.32f, .015f), new Vector3(.105f, .12f, .105f), skin, Ellipsoid());
            Piece("Thumb", forearm.transform, new Vector3(-sign * .051f, -.31f, .037f), new Vector3(.045f, .07f, .05f), skin, Ellipsoid());
            upperArmStumps[i] = TornStump("Severed sleeve stump", arm.transform, new Vector3(0, -.294f, 0), false);
            forearmStumps[i] = TornStump("Detached forearm torn end", forearm.transform, new Vector3(0, .006f, 0), true);
            forearmRegion.parentStump = upperArmStumps[i];
            forearmRegion.detachedStump = forearmStumps[i];
            forearmJoints[i] = forearm.GetComponent<CharacterJoint>();
            Rigidbody thigh = Body("Thigh " + side, bodyRoot, new Vector3(sign * .105f, .78f, 0), new Vector3(.15f, .34f, .19f), 5.5f, pelvis, Vector3.zero, bodies, colliders, false, new Vector3(0, -.17f, 0));
            FacilityHitRegion thighRegion = SurfaceRegion(thigh, new Vector3(.17f, .34f, .21f), cloth, false, new Vector3(0, -.17f, 0));
            Describe(thighRegion, actor, i == 0 ? FacilityBodyRegion.ThighLeft : FacilityBodyRegion.ThighRight, hipsRegion);
            thighRegion.parentStump = TornStump("Open hip " + side, pelvis.transform, new Vector3(sign * .105f, -.06f, 0), false);
            thighRegion.parentStump.transform.localScale = new Vector3(1.15f, 1, 1.25f);
            thighRegion.detachedStump = TornStump("Detached thigh torn hip " + side, thigh.transform, Vector3.zero, true);
            regions.Add(thighRegion);
            Rigidbody shin = Body("Shin " + side, bodyRoot, new Vector3(sign * .105f, .44f, 0), new Vector3(.13f, .31f, .145f), 3.2f, thigh, Vector3.zero, bodies, colliders, false, new Vector3(0, -.155f, 0));
            BoxCollider shinCollider = shin.GetComponent<BoxCollider>();
            shinCollider.center = new Vector3(0, -.217f, .035f);
            shinCollider.size = new Vector3(.165f, .43f, .27f); // Boots support the physical corpse instead of clipping below the floor.
            FacilityHitRegion shinRegion = SurfaceRegion(shin, new Vector3(.145f, .31f, .165f), cloth, false, new Vector3(0, -.155f, 0));
            Describe(shinRegion, actor, i == 0 ? FacilityBodyRegion.ShinLeft : FacilityBodyRegion.ShinRight, thighRegion);
            shinRegion.parentStump = TornStump("Open knee " + side, thigh.transform, new Vector3(0, -.34f, 0), false);
            shinRegion.detachedStump = TornStump("Detached shin torn knee " + side, shin.transform, Vector3.zero, true);
            regions.Add(shinRegion);
            Piece("Work boot", shin.transform, new Vector3(0, -.35f, .045f), new Vector3(.17f, .16f, .29f), trim, BeveledBox());
            Piece("Boot sole", shin.transform, new Vector3(0, -.412f, .045f), new Vector3(.18f, .035f, .30f), trim, BeveledBox());
            upperArms[i] = arm.transform; forearms[i] = forearm.transform; thighs[i] = thigh.transform; shins[i] = shin.transform;
        }
        actor.head = head.transform; actor.chest = chest.transform; actor.pelvis = pelvis.transform;
        actor.bodies = bodies.ToArray(); actor.bodyColliders = colliders.ToArray(); actor.hitRegions = regions.ToArray();
        actor.upperArms = upperArms; actor.forearms = forearms; actor.thighs = thighs; actor.shins = shins;
        actor.forearmJoints = forearmJoints; actor.upperArmStumps = upperArmStumps; actor.forearmStumps = forearmStumps;
        actor.gore = root.AddComponent<FacilityGorePool>();
        actor.gore.dropletMesh = Ellipsoid(); actor.gore.chunkMesh = Fragment(); actor.gore.stainMesh = Stain();
        actor.gore.bloodMaterial = blood; actor.gore.fleshMaterial = flesh; actor.gore.boneMaterial = bone;
        List<Collider> ignore = new List<Collider>(colliders) { motor };
        if (player != null)
        {
            CharacterController playerMotor = player.GetComponentInParent<CharacterController>();
            if (playerMotor != null) ignore.Add(playerMotor);
        }
        actor.gore.ownerColliders = ignore.ToArray();
        actor.voice = root.AddComponent<AudioSource>();
        actor.voice.playOnAwake = false; actor.voice.spatialBlend = 1; actor.voice.minDistance = 2; actor.voice.maxDistance = 18;
        actor.voice.rolloffMode = AudioRolloffMode.Linear; actor.voice.dopplerLevel = 0;
        actor.hurtSound = Voice("Impact exhale", .26f, false); actor.deathSound = Voice("Final exhale", .5f, true);
        actor.voice.clip = actor.hurtSound;
        GameObject impactAudio = new GameObject("Tissue impact audio"); impactAudio.transform.SetParent(root.transform, false);
        actor.gore.impactSource = impactAudio.AddComponent<AudioSource>();
        actor.gore.impactSource.playOnAwake = false; actor.gore.impactSource.spatialBlend = 1;
        actor.gore.impactSource.minDistance = 1.5f; actor.gore.impactSource.maxDistance = 20;
        actor.gore.impactSource.rolloffMode = AudioRolloffMode.Linear; actor.gore.impactSource.dopplerLevel = 0;
        actor.gore.impactSource.volume = .82f;
        actor.gore.impactClip = WetImpact(); actor.gore.impactSource.clip = actor.gore.impactClip;
        actor.contactShadow = ContactShadow(root.transform);
        BuildHealthBar(actor, player);
        FacilityAudioAuthoring.ConfigureActor(actor);
        FacilityBankAuthoring.Expression(actor);
        AssetDatabase.SaveAssets();
        return actor;
    }

    private static void Describe(FacilityHitRegion region, FacilityHumanoid owner, FacilityBodyRegion kind, FacilityHitRegion parent)
    {
        region.owner = owner; region.kind = kind; region.parentRegion = parent;
        region.joint = region.GetComponent<CharacterJoint>();
    }

    private static Rigidbody Body(string name, Transform parent, Vector3 position, Vector3 size, float mass, Rigidbody connected, Vector3 anchor,
        List<Rigidbody> bodies, List<Collider> colliders, bool round = false, Vector3 colliderCenter = default)
    {
        GameObject part = new GameObject(name); part.transform.SetParent(parent, false); part.transform.localPosition = position;
        Collider collider;
        if (round)
        {
            SphereCollider sphere = part.AddComponent<SphereCollider>(); sphere.radius = .135f; collider = sphere;
        }
        else
        {
            BoxCollider box = part.AddComponent<BoxCollider>(); box.size = size; box.center = colliderCenter; collider = box;
        }
        Rigidbody body = part.AddComponent<Rigidbody>(); body.mass = mass; body.isKinematic = true;
        body.linearDamping = .12f; body.angularDamping = .6f; body.maxAngularVelocity = 14f;
        body.solverIterations = 12; body.solverVelocityIterations = 4;
        if (connected != null)
        {
            CharacterJoint joint = part.AddComponent<CharacterJoint>(); joint.connectedBody = connected;
            joint.anchor = anchor; joint.axis = Vector3.right; joint.swingAxis = Vector3.forward;
            joint.lowTwistLimit = new SoftJointLimit { limit = -35 };
            joint.highTwistLimit = new SoftJointLimit { limit = 35 };
            joint.swing1Limit = new SoftJointLimit { limit = name.StartsWith("Shin", StringComparison.Ordinal) ? 60 : 45 };
            joint.swing2Limit = new SoftJointLimit { limit = 30 };
            joint.enableProjection = true; joint.projectionDistance = .04f; joint.projectionAngle = 15;
            joint.enableCollision = false; joint.enablePreprocessing = false;
        }
        bodies.Add(body); colliders.Add(collider); return body;
    }

    private static FacilityHitRegion SurfaceRegion(Rigidbody body, Vector3 size, Material clothing, bool tiledFront, Vector3 centre = default)
    {
        FacilityHitRegion region = body.gameObject.AddComponent<FacilityHitRegion>(); region.body = body; region.regionName = body.name;
        // The core is slightly recessed. Removing an outer panel leaves a genuine change of silhouette/depth.
        Vector3 coreSize = Vector3.Scale(size, new Vector3(.84f, .92f, tiledFront ? .60f : .75f));
        Piece("Recessed tissue core", body.transform, centre, coreSize, flesh, BeveledBox());
        List<Renderer> patches = new List<Renderer>(); List<GameObject> beds = new List<GameObject>();
        int columns = tiledFront ? 3 : 1, rows = 2;
        for (int face = 0; face < 2; face++)
            for (int row = 0; row < rows; row++)
                for (int column = 0; column < columns; column++)
                {
                    float faceSign = face == 0 ? 1f : -1f;
                    Vector3 patchSize = new Vector3(size.x / columns + .002f, size.y / rows + .002f, .022f);
                    Vector3 offset = centre + new Vector3(-size.x * .5f + (column + .5f) * size.x / columns, -size.y * .5f + (row + .5f) * size.y / rows, faceSign * (size.z * .5f - .003f));
                    Transform patch = Piece("Removable outer panel " + patches.Count, body.transform, offset, patchSize, clothing, BeveledBox());
                    patches.Add(patch.GetComponent<Renderer>());
                    float depth = tiledFront ? .058f : Mathf.Min(.032f, size.z * .18f);
                    Vector3 lipPosition = offset + Vector3.forward * faceSign * .011f;
                    Transform wound = Piece("Open torn cavity " + beds.Count, body.transform, lipPosition, new Vector3(patchSize.x, patchSize.y, depth), blood, TornRim());
                    if (faceSign < 0) wound.localRotation = Quaternion.Euler(0, 180, 0);
                    wound.gameObject.SetActive(false); beds.Add(wound.gameObject);
                }
        for (int side = -1; side <= 1; side += 2)
        {
            Transform patch = Piece("Removable side panel " + side, body.transform, centre + Vector3.right * side * (size.x * .5f - .007f), new Vector3(.025f, size.y, size.z), clothing, BeveledBox());
            patches.Add(patch.GetComponent<Renderer>()); beds.Add(null);
        }
        Piece("Top seam", body.transform, centre + Vector3.up * (size.y * .5f - .006f), new Vector3(size.x, .022f, size.z), clothing, BeveledBox());
        Piece("Bottom seam", body.transform, centre - Vector3.up * (size.y * .5f - .006f), new Vector3(size.x, .022f, size.z), clothing, BeveledBox());
        region.outerPatches = patches.ToArray(); region.woundBeds = beds.ToArray(); return region;
    }

    private static FacilityHitRegion HeadSurface(Rigidbody body)
    {
        FacilityHitRegion region = body.gameObject.AddComponent<FacilityHitRegion>(); region.body = body; region.regionName = "Head";
        Piece("Head tissue core", body.transform, Vector3.zero, new Vector3(.245f, .302f, .241f), flesh, Ellipsoid());
        region.outerPatches = new Renderer[4]; region.woundBeds = new GameObject[4];
        for (int i = 0; i < 4; i++)
        {
            Transform quarter = Piece("Removable head quadrant " + i, body.transform, Vector3.zero, new Vector3(.26f, .32f, .255f), skin, SphereQuarter());
            quarter.localRotation = Quaternion.Euler(0, 90f * i, 0);
            region.outerPatches[i] = quarter.GetComponent<Renderer>();
        }
        return region;
    }

    private static GameObject TornStump(string name, Transform parent, Vector3 position, bool up)
    {
        GameObject root = new GameObject(name); root.transform.SetParent(parent, false); root.transform.localPosition = position;
        float sign = up ? 1f : -1f;
        Piece("Torn muscle", root.transform, Vector3.zero, new Vector3(.133f, .055f, .145f), flesh, Fragment());
        Piece("Dark open centre", root.transform, new Vector3(0, sign * .023f, .004f), new Vector3(.089f, .012f, .09f), blood, Ellipsoid());
        Piece("Broken bone", root.transform, new Vector3(.012f, sign * .038f, -.006f), new Vector3(.028f, .074f, .032f), bone, Fragment());
        root.SetActive(false); return root;
    }

    private static void BuildHealthBar(FacilityHumanoid actor, Transform player)
    {
        Transform root = new GameObject("Subtle injury bar").transform; root.SetParent(actor.transform, false); root.localPosition = new Vector3(0, 1.98f, 0);
        Transform back = Piece("Dark border", root, Vector3.zero, new Vector3(.39f, .027f, .006f), barBack, Box());
        Transform fill = Piece("Remaining health", root, new Vector3(0, 0, -.004f), new Vector3(.36f, .012f, .006f), barFill, Box());
        SubtleEnemyHealthBar bar = root.gameObject.AddComponent<SubtleEnemyHealthBar>(); bar.owner = actor; bar.player = player; bar.fill = fill;
        bar.barRenderers = new[] { back.GetComponent<Renderer>(), fill.GetComponent<Renderer>() };
        foreach (Renderer renderer in bar.barRenderers) { renderer.enabled = false; renderer.shadowCastingMode = ShadowCastingMode.Off; }
    }

    private static Transform ContactShadow(Transform parent)
    {
        string path = Root + "/Materials/Subtle contact shadow.mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            string texturePath = Root + "/Materials/Soft contact opacity.asset";
            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
            if (texture == null)
            {
                const int size = 32;
                texture = new Texture2D(size, size, TextureFormat.RGBA32, false, true) { name = "Soft contact opacity", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
                Color32[] pixels = new Color32[size * size];
                for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
                {
                    float radius = new Vector2((x + .5f) / size * 2 - 1, (y + .5f) / size * 2 - 1).magnitude;
                    pixels[y * size + x] = new Color32(0, 0, 0, (byte)(Mathf.Pow(Mathf.Clamp01(1 - radius), 1.4f) * 72));
                }
                texture.SetPixels32(pixels); texture.Apply(false, true); AssetDatabase.CreateAsset(texture, texturePath);
            }
            material = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { name = "Subtle contact shadow" };
            material.SetTexture("_BaseMap", texture); material.SetColor("_BaseColor", Color.white);
            material.SetFloat("_Surface", 1); material.SetFloat("_Blend", 0); material.SetFloat("_ZWrite", 0);
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha); material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_Cull", (float)CullMode.Off); material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.SetOverrideTag("RenderType", "Transparent"); material.renderQueue = (int)RenderQueue.Transparent;
            AssetDatabase.CreateAsset(material, path);
        }
        Transform shadow = Piece("Soft boot contact", parent, new Vector3(0, .008f, .015f), new Vector3(.8f, .62f, 1), material, Stain());
        shadow.localRotation = Quaternion.Euler(90, 0, 0);
        Renderer renderer = shadow.GetComponent<Renderer>(); renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
        return shadow;
    }

    private static Transform Piece(string name, Transform parent, Vector3 position, Vector3 size, Material material, Mesh mesh)
    {
        GameObject piece = new GameObject(name); piece.transform.SetParent(parent, false); piece.transform.localPosition = position; piece.transform.localScale = size;
        piece.AddComponent<MeshFilter>().sharedMesh = mesh;
        MeshRenderer renderer = piece.AddComponent<MeshRenderer>(); renderer.sharedMaterial = material;
        renderer.lightProbeUsage = LightProbeUsage.BlendProbes; renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        return piece.transform;
    }

    private static Material Material(string name, Color color, float smoothness = .02f)
    {
        string path = Root + "/Materials/" + name + ".mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material != null) return material;
        material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
        material.SetColor("_BaseColor", color); material.SetFloat("_Smoothness", smoothness); material.SetFloat("_Metallic", 0);
        material.SetFloat("_EnvironmentReflections", 0); material.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
        material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
        AssetDatabase.CreateAsset(material, path); return material;
    }

    /// <summary>Explicit character-only correction, invoked by the authoring operation rather than the asset getter.</summary>
    public static void ApplyCharacterReadability()
    {
        ReadableMaterial("Coverall - faded sage", new Color(.40f, .45f, .37f), .055f);
        ReadableMaterial("Warm skin", new Color(.69f, .5f, .39f), .04f);
        ReadableMaterial("Boots and utility belt", new Color(.09f, .1f, .09f), .055f);
        ReadableMaterial("Exposed tissue", new Color(.46f, .045f, .055f), .11f);
        ReadableMaterial("Dark wet blood", new Color(.36f, .008f, .015f), .095f);
        ReadableMaterial("Bone fragments", new Color(.62f, .47f, .33f), .06f);
    }

    private static void ReadableMaterial(string name, Color baseColor, float emissionScale)
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(Root + "/Materials/" + name + ".mat");
        if (material == null) return;
        material.SetColor("_BaseColor", baseColor);
        material.SetColor("_EmissionColor", baseColor * emissionScale);
        material.EnableKeyword("_EMISSION");
        material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
        EditorUtility.SetDirty(material);
    }

    private static Mesh Box() => Cached("Unit box", () =>
    {
        GameObject primitive = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Mesh result = UnityEngine.Object.Instantiate(primitive.GetComponent<MeshFilter>().sharedMesh);
        UnityEngine.Object.DestroyImmediate(primitive); return result;
    });

    private static Mesh BeveledBox() => Cached("Eight sided chamfered block", () =>
    {
        Vector2[] ring = { new Vector2(-.38f,-.5f), new Vector2(.38f,-.5f), new Vector2(.5f,-.38f), new Vector2(.5f,.38f), new Vector2(.38f,.5f), new Vector2(-.38f,.5f), new Vector2(-.5f,.38f), new Vector2(-.5f,-.38f) };
        List<Vector3> vertices = new List<Vector3>(); List<int> triangles = new List<int>();
        for (int i = 0; i < 8; i++)
        {
            Vector2 a = ring[i], b = ring[(i + 1) % 8]; int start = vertices.Count;
            vertices.Add(new Vector3(a.x, -.5f, a.y)); vertices.Add(new Vector3(b.x, -.5f, b.y)); vertices.Add(new Vector3(b.x, .5f, b.y)); vertices.Add(new Vector3(a.x, .5f, a.y));
            triangles.AddRange(new[] { start, start + 2, start + 1, start, start + 3, start + 2 });
        }
        for (int side = 0; side < 2; side++)
        {
            int centre = vertices.Count; vertices.Add(new Vector3(0, side == 0 ? -.5f : .5f, 0));
            for (int i = 0; i < 8; i++) vertices.Add(new Vector3(ring[i].x, side == 0 ? -.5f : .5f, ring[i].y));
            for (int i = 0; i < 8; i++)
            {
                int a = centre + 1 + i, b = centre + 1 + (i + 1) % 8;
                triangles.AddRange(side == 0 ? new[] { centre, a, b } : new[] { centre, b, a });
            }
        }
        return Make(vertices.ToArray(), triangles.ToArray());
    });

    private static Mesh Ellipsoid() => Cached("Rounded low poly form", () => SphereMesh(0, 360, 16, 12));
    private static Mesh SphereQuarter() => Cached("Head surface quadrant", () => SphereMesh(0, 90, 4, 12));
    private static Mesh SphereMesh(float start, float sweep, int sides, int rings)
    {
        List<Vector3> vertices = new List<Vector3>(); List<int> triangles = new List<int>();
        for (int y = 0; y <= rings; y++)
        {
            float polar = Mathf.PI * y / rings;
            for (int x = 0; x <= sides; x++)
            {
                float angle = (start + sweep * x / sides) * Mathf.Deg2Rad;
                vertices.Add(new Vector3(Mathf.Sin(angle) * Mathf.Sin(polar), Mathf.Cos(polar), Mathf.Cos(angle) * Mathf.Sin(polar)) * .5f);
            }
        }
        for (int y = 0; y < rings; y++)
            for (int x = 0; x < sides; x++)
            {
                int a = y * (sides + 1) + x, b = a + sides + 1;
                triangles.AddRange(new[] { a, b, a + 1, a + 1, b, b + 1 });
            }
        return Make(vertices.ToArray(), triangles.ToArray());
    }

    private static Mesh Stain() => Cached("Rounded pooled blood splash V2", () =>
    {
        var vertices = new List<Vector3>();
        var triangles = new List<int>();
        const int perimeter = 32;
        Vector2[] pool = new Vector2[perimeter];
        for (int i = 0; i < perimeter; i++)
        {
            float angle = i * Mathf.PI * 2f / perimeter;
            // Broad, smoothly changing lobes form a continuous pool; no alternating spikes or star points.
            float radius = .355f * (1f + .10f * Mathf.Sin(angle * 3f + .4f)
                + .045f * Mathf.Cos(angle * 5f - .7f) + .025f * Mathf.Sin(angle * 7f + .3f));
            pool[i] = new Vector2(Mathf.Cos(angle) * radius * 1.03f, Mathf.Sin(angle) * radius * .93f);
        }
        AppendSplashIsland(vertices, triangles, Vector2.zero, pool);
        // Three small, rounded satellite drops break up the edge without becoming a radial burst.
        Vector2[] centres = { new Vector2(.428f, .142f), new Vector2(-.414f, -.203f), new Vector2(.165f, -.431f) };
        float[] radii = { .026f, .022f, .017f };
        for (int drop = 0; drop < centres.Length; drop++)
        {
            var rim = new Vector2[8];
            for (int i = 0; i < rim.Length; i++)
            {
                float angle = i * Mathf.PI * 2f / rim.Length;
                float radius = radii[drop] * (1f + .06f * Mathf.Sin(angle * 3f + drop));
                rim[i] = new Vector2(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius * .88f);
            }
            AppendSplashIsland(vertices, triangles, centres[drop], rim);
        }
        // All vertices stay inside a 0.5 m radius, including the satellites, for reliable surface fitting.
        return Make(vertices.ToArray(), triangles.ToArray());
    });

    private static void AppendSplashIsland(List<Vector3> vertices, List<int> triangles, Vector2 centre, Vector2[] rim)
    {
        // Separate front/back vertices preserve opposed normals instead of cancelling them on a shared fan.
        for (int side = 0; side < 2; side++)
        {
            int start = vertices.Count;
            vertices.Add(new Vector3(centre.x, centre.y, 0f));
            foreach (Vector2 point in rim) vertices.Add(new Vector3(centre.x + point.x, centre.y + point.y, 0f));
            for (int i = 0; i < rim.Length; i++)
            {
                int a = start + 1 + i, b = start + 1 + (i + 1) % rim.Length;
                triangles.Add(start);
                triangles.Add(side == 0 ? a : b);
                triangles.Add(side == 0 ? b : a);
            }
        }
    }

    private static Mesh Fragment() => Cached("Ragged tissue fragment", () =>
    {
        Vector3[] points = {
            new Vector3(-.37f, -.32f, -.23f), new Vector3(.42f, -.38f, -.3f),
            new Vector3(.32f, -.26f, .42f), new Vector3(-.44f, -.36f, .28f),
            new Vector3(-.21f, .42f, -.34f), new Vector3(.33f, .29f, -.14f),
            new Vector3(.28f, .46f, .29f), new Vector3(-.39f, .31f, .38f)
        };
        int[] indices = { 0,2,1,0,3,2, 4,5,6,4,6,7, 0,1,5,0,5,4, 1,2,6,1,6,5, 2,3,7,2,7,6, 3,0,4,3,4,7 };
        // Individual triangles keep the torn silhouette faceted rather than looking like smooth red marbles.
        Vector3[] vertices = new Vector3[indices.Length]; int[] triangles = new int[indices.Length];
        for (int i = 0; i < indices.Length; i++) vertices[i] = points[indices[i]];
        for (int i = 0; i < indices.Length; i += 3) { triangles[i] = i; triangles[i + 1] = i + 2; triangles[i + 2] = i + 1; }
        return Make(vertices, triangles);
    });

    private static Mesh TornRim() => Cached("Deep irregular wound rim", () =>
    {
        Vector2[] outer = {
            new Vector2(-.5f,-.5f), new Vector2(0,-.5f), new Vector2(.5f,-.5f), new Vector2(.5f,0),
            new Vector2(.5f,.5f), new Vector2(0,.5f), new Vector2(-.5f,.5f), new Vector2(-.5f,0)
        };
        Vector2[] inner = {
            new Vector2(-.33f,-.39f), new Vector2(.06f,-.3f), new Vector2(.39f,-.37f), new Vector2(.32f,.06f),
            new Vector2(.38f,.35f), new Vector2(-.06f,.41f), new Vector2(-.37f,.32f), new Vector2(-.42f,-.06f)
        };
        List<Vector3> vertices = new List<Vector3>(); List<int> triangles = new List<int>();
        for (int i = 0; i < outer.Length; i++)
        {
            int next = (i + 1) % outer.Length, start = vertices.Count;
            vertices.Add(new Vector3(outer[i].x, outer[i].y, 0));
            vertices.Add(new Vector3(outer[next].x, outer[next].y, 0));
            vertices.Add(new Vector3(inner[next].x, inner[next].y, -1));
            vertices.Add(new Vector3(inner[i].x, inner[i].y, -1));
            triangles.AddRange(new[] { start, start + 1, start + 2, start, start + 2, start + 3 });
        }
        // The middle stays genuinely open; the recessed body core is visible through this mesh.
        return Make(vertices.ToArray(), triangles.ToArray());
    });

    private static Mesh Cached(string name, Func<Mesh> create)
    {
        if (meshes.TryGetValue(name, out Mesh cached) && cached != null) return cached;
        string path = Root + "/Meshes/" + name + ".asset";
        Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (mesh == null) { mesh = create(); mesh.name = name; AssetDatabase.CreateAsset(mesh, path); }
        meshes[name] = mesh; return mesh;
    }

    private static Mesh Make(Vector3[] vertices, int[] triangles)
    {
        Mesh mesh = new Mesh { vertices = vertices, triangles = triangles };
        Vector2[] uv = new Vector2[vertices.Length]; for (int i = 0; i < vertices.Length; i++) uv[i] = new Vector2(vertices[i].x + .5f, vertices[i].y + .5f);
        mesh.uv = uv; mesh.RecalculateNormals(); mesh.RecalculateBounds(); return mesh;
    }

    private static AudioClip Voice(string name, float seconds, bool dying)
    {
        string path = Root + "/Audio/" + name + ".wav";
        AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path); if (clip != null) return clip;
        const int rate = 22050; int count = Mathf.RoundToInt(seconds * rate); uint random = 8917;
        using (BinaryWriter writer = new BinaryWriter(File.Open(path, FileMode.CreateNew)))
        {
            writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); writer.Write(36 + count * 2); writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));
            writer.Write(16); writer.Write((short)1); writer.Write((short)1); writer.Write(rate); writer.Write(rate * 2); writer.Write((short)2); writer.Write((short)16);
            writer.Write(System.Text.Encoding.ASCII.GetBytes("data")); writer.Write(count * 2);
            float lowNoise = 0;
            for (int i = 0; i < count; i++)
            {
                random ^= random << 13; random ^= random >> 17; random ^= random << 5;
                float noise = (random & 65535) / 32768f - 1f; lowNoise = Mathf.Lerp(lowNoise, noise, .12f);
                float t = i / (float)rate, normalized = t / seconds;
                float envelope = Mathf.Min(1, t * 50) * Mathf.Pow(1 - normalized, 1.5f);
                float tone = Mathf.Sin(t * (dying ? 540 : 740) - t * t * 230) * .12f;
                float sample = (lowNoise * .7f + tone) * envelope;
                writer.Write((short)Mathf.Clamp(sample * 22000, short.MinValue, short.MaxValue));
            }
        }
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        AudioImporter importer = AssetImporter.GetAtPath(path) as AudioImporter;
        if (importer != null)
        {
            AudioImporterSampleSettings settings = importer.defaultSampleSettings; settings.loadType = AudioClipLoadType.DecompressOnLoad; settings.compressionFormat = AudioCompressionFormat.PCM;
            importer.defaultSampleSettings = settings; importer.forceToMono = true; importer.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
    }

    private static AudioClip WetImpact()
    {
        const string name = "Wet tissue impact";
        string path = Root + "/Audio/" + name + ".wav";
        AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path); if (clip != null) return clip;
        const int rate = 22050;
        const float duration = .28f;
        int count = Mathf.RoundToInt(rate * duration);
        float[] samples = new float[count];
        uint random = 510819;
        float filtered = 0, previous = 0, maximum = .001f;
        for (int i = 0; i < count; i++)
        {
            random ^= random << 13; random ^= random >> 17; random ^= random << 5;
            float noise = (random & 65535) / 32768f - 1f;
            filtered = Mathf.Lerp(filtered, noise, .21f);
            float t = i / (float)rate;
            float transient = Mathf.Min(1, t * 800f);
            float low = Mathf.Sin(Mathf.PI * 2f * (72f * t - 46f * t * t)) * Mathf.Exp(-t * 24f);
            float grain = Mathf.Exp(-Mathf.Pow((t - .025f) / .014f, 2))
                + .72f * Mathf.Exp(-Mathf.Pow((t - .064f) / .024f, 2))
                + .36f * Mathf.Exp(-Mathf.Pow((t - .12f) / .036f, 2));
            float crunch = (noise - previous) * .055f * Mathf.Exp(-t * 48f);
            float sample = (low * .5f + filtered * grain * .77f + crunch) * transient * Mathf.Clamp01((duration - t) * 24f);
            samples[i] = sample; maximum = Mathf.Max(maximum, Mathf.Abs(sample)); previous = noise;
        }
        using (BinaryWriter writer = new BinaryWriter(File.Open(path, FileMode.CreateNew)))
        {
            writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); writer.Write(36 + count * 2); writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));
            writer.Write(16); writer.Write((short)1); writer.Write((short)1); writer.Write(rate); writer.Write(rate * 2); writer.Write((short)2); writer.Write((short)16);
            writer.Write(System.Text.Encoding.ASCII.GetBytes("data")); writer.Write(count * 2);
            foreach (float sample in samples) writer.Write((short)(sample / maximum * .8f * short.MaxValue));
        }
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        AudioImporter importer = AssetImporter.GetAtPath(path) as AudioImporter;
        if (importer != null)
        {
            AudioImporterSampleSettings settings = importer.defaultSampleSettings;
            settings.loadType = AudioClipLoadType.DecompressOnLoad; settings.compressionFormat = AudioCompressionFormat.PCM;
            importer.defaultSampleSettings = settings; importer.forceToMono = true; importer.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
    }

    private static void Folder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = path.Substring(0, path.LastIndexOf('/')); Folder(parent);
        AssetDatabase.CreateFolder(parent, path.Substring(path.LastIndexOf('/') + 1));
    }
}
