
using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;

public class FacilityExpansion : MonoBehaviour
{
    private Material wall, floor, ceiling, metal;
    private Material darkSteel, hazard, glass, glow;

    IEnumerator Start()
    {
        // Wait for the original world to be generated.
        yield return null;

        if (!LoadMaterials())
        {
            Debug.LogError(
                "FacilityExpansion: Existing world materials not found. " +
                "Attach this script to the same World object " +
                "as RetroWorldBuilder.");
            yield break;
        }

        CreateNewMaterials();

        BuildResearchLab();
        BuildMaintenanceBay();
        SetupNewLights();

        Debug.Log("Facility expansion generated successfully.");
    }

    bool LoadMaterials()
    {
        wall = FindMaterial("Back Wall");
        floor = FindMaterial("Start Floor");
        ceiling = FindMaterial("Start Ceiling");
        metal = FindMaterial("Crate A");

        return wall != null && floor != null &&
               ceiling != null && metal != null;
    }

    Material FindMaterial(string objectName)
    {
        Transform child = transform.Find(objectName);

        if (child == null)
            return null;

        MeshRenderer renderer = child.GetComponent<MeshRenderer>();
        return renderer != null ? renderer.sharedMaterial : null;
    }

    Material MakeMaterial(string name, Color color, bool unlit = false)
    {
        string shader = unlit
            ? "Universal Render Pipeline/Unlit"
            : "Universal Render Pipeline/Lit";

        Material material = new Material(Shader.Find(shader));
        material.name = name;
        material.SetColor("_BaseColor", color);

        if (!unlit)
            material.SetFloat("_Smoothness", 0.08f);

        return material;
    }

    void CreateNewMaterials()
    {
        darkSteel = MakeMaterial("Dark Steel",
            new Color(0.10f, 0.13f, 0.16f));

        hazard = MakeMaterial("Hazard Orange",
            new Color(0.65f, 0.29f, 0.07f));

        glass = MakeMaterial("Monitor Surface",
            new Color(0.03f, 0.12f, 0.16f));

        glow = MakeMaterial("Research Lamp",
            new Color(0.42f, 0.75f, 0.88f), true);
    }

    GameObject Block(
        string name,
        Vector3 position,
        Vector3 size,
        Material material,
        bool collision = true)
    {
        GameObject obj = GameObject.CreatePrimitive(
            PrimitiveType.Cube);

        obj.name = name;
        obj.transform.SetParent(transform, false);
        obj.transform.localPosition = position;
        obj.transform.localScale = size;

        MeshRenderer renderer = obj.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;

        if (!collision)
        {
            Collider col = obj.GetComponent<Collider>();
            if (col != null)
                Destroy(col);
        }

        obj.isStatic = true;
        return obj;
    }

    void BuildResearchLab()
    {
        // CONNECTION TO FACTORY
        Block("Lab Passage Floor",
            new Vector3(-10, -0.25f, 30),
            new Vector3(4, 0.5f, 4), floor);

        Block("Lab Passage Ceiling",
            new Vector3(-10, 4.15f, 30),
            new Vector3(4, 0.3f, 4), ceiling);

        Block("Lab Passage Front",
            new Vector3(-10, 2, 28),
            new Vector3(4, 4, 0.4f), wall);

        Block("Lab Passage Back",
            new Vector3(-10, 2, 32),
            new Vector3(4, 4, 0.4f), wall);

        // MAIN LAB ROOM (16 x 16)
        Block("Research Floor",
            new Vector3(-20, -0.25f, 30),
            new Vector3(16, 0.5f, 16), floor);

        Block("Research Ceiling",
            new Vector3(-20, 6.45f, 30),
            new Vector3(16, 0.3f, 16), ceiling);

        Block("Research Far Wall",
            new Vector3(-28, 3.15f, 30),
            new Vector3(0.4f, 6.3f, 16), wall);

        Block("Research Front Wall",
            new Vector3(-20, 3.15f, 22),
            new Vector3(16, 6.3f, 0.4f), wall);

        Block("Research Back Wall",
            new Vector3(-20, 3.15f, 38),
            new Vector3(16, 6.3f, 0.4f), wall);

        // Entrance opening on right wall
        Block("Research Entrance A",
            new Vector3(-12, 3.15f, 25),
            new Vector3(0.4f, 6.3f, 6), wall);

        Block("Research Entrance B",
            new Vector3(-12, 3.15f, 35),
            new Vector3(0.4f, 6.3f, 6), wall);

        Block("Research Entrance Header",
            new Vector3(-12, 4.65f, 30),
            new Vector3(0.4f, 3.3f, 4), wall);

        // LAB WORKSTATIONS
        for (int i = 0; i < 2; i++)
        {
            float x = -24f + i * 6f;

            Block("Laboratory Desk",
                new Vector3(x, 0.75f, 25),
                new Vector3(3, 0.18f, 1.3f), darkSteel);

            Block("Desk Base",
                new Vector3(x, 0.36f, 25),
                new Vector3(2.4f, 0.72f, 0.9f), metal);

            Block("Lab Monitor",
                new Vector3(x, 1.25f, 25.3f),
                new Vector3(0.9f, 0.7f, 0.12f), darkSteel);

            Block("Monitor Screen",
                new Vector3(x, 1.25f, 25.22f),
                new Vector3(0.72f, 0.52f, 0.03f),
                glass, false);
        }

        // ELEVATED WALKWAY
        Block("Research Upper Platform",
            new Vector3(-24, 2.15f, 35),
            new Vector3(7.7f, 0.3f, 5.7f), darkSteel);

        // Walkable sloping ramp
        GameObject ramp = Block("Research Access Ramp",
            new Vector3(-17.25f, 1.14f, 35),
            new Vector3(6.8f, 0.3f, 2.0f), metal);

        // Rises towards the elevated platform.
        ramp.transform.localRotation =
            Quaternion.Euler(0, 0, -19.4f);

        // Guard rails along the upper platform
        Block("Upper Back Rail",
            new Vector3(-24, 2.95f, 37.6f),
            new Vector3(7.2f, 1.2f, 0.12f), darkSteel);

        Block("Upper Left Rail",
            new Vector3(-27.65f, 2.95f, 35),
            new Vector3(0.12f, 1.2f, 5.3f), darkSteel);

        Block("Upper Front Rail",
            new Vector3(-24, 2.95f, 32.4f),
            new Vector3(7.2f, 1.2f, 0.12f), darkSteel);

        // Decorative overhead structural beam
        Block("Research Ceiling Beam",
            new Vector3(-20, 6.0f, 30),
            new Vector3(15.5f, 0.25f, 0.35f),
            darkSteel, false);
    }

    void BuildMaintenanceBay()
    {
        // CONNECTION TO FACTORY
        Block("Bay Passage Floor",
            new Vector3(10, -0.25f, 30),
            new Vector3(4, 0.5f, 4), floor);

        Block("Bay Passage Ceiling",
            new Vector3(10, 4.15f, 30),
            new Vector3(4, 0.3f, 4), ceiling);

        Block("Bay Passage Front",
            new Vector3(10, 2, 28),
            new Vector3(4, 4, 0.4f), wall);

        Block("Bay Passage Back",
            new Vector3(10, 2, 32),
            new Vector3(4, 4, 0.4f), wall);

        // MAINTENANCE ROOM
        Block("Bay Floor",
            new Vector3(20, -0.25f, 30),
            new Vector3(16, 0.5f, 16), floor);

        Block("Bay Ceiling",
            new Vector3(20, 5.45f, 30),
            new Vector3(16, 0.3f, 16), ceiling);

        Block("Bay Far Wall",
            new Vector3(28, 2.65f, 30),
            new Vector3(0.4f, 5.3f, 16), wall);

        Block("Bay Front Wall",
            new Vector3(20, 2.65f, 22),
            new Vector3(16, 5.3f, 0.4f), wall);

        Block("Bay Back Wall",
            new Vector3(20, 2.65f, 38),
            new Vector3(16, 5.3f, 0.4f), wall);

        // Doorway
        Block("Bay Entrance A",
            new Vector3(12, 2.65f, 25),
            new Vector3(0.4f, 5.3f, 6), wall);

        Block("Bay Entrance B",
            new Vector3(12, 2.65f, 35),
            new Vector3(0.4f, 5.3f, 6), wall);

        Block("Bay Entrance Header",
            new Vector3(12, 4.15f, 30),
            new Vector3(0.4f, 2.3f, 4), wall);

        // HEAVY MACHINERY
        Block("Generator Base",
            new Vector3(21, 0.65f, 33),
            new Vector3(4.5f, 1.3f, 2.5f), darkSteel);

        Block("Generator Housing",
            new Vector3(21, 1.75f, 33),
            new Vector3(3.7f, 1.0f, 2.0f), metal);

        Block("Generator Top",
            new Vector3(21, 2.35f, 33),
            new Vector3(4.1f, 0.25f, 2.2f), darkSteel);

        // Storage crates
        Block("Storage Crate A",
            new Vector3(16, 0.6f, 25),
            new Vector3(1.2f, 1.2f, 1.2f), metal);

        Block("Storage Crate B",
            new Vector3(25, 0.9f, 27),
            new Vector3(1.8f, 1.8f, 1.8f), metal);

        // Hazard floor markings
        for (int i = 0; i < 5; i++)
        {
            Block("Maintenance Hazard Stripe",
                new Vector3(18f + i * 1.4f, 0.012f, 29),
                new Vector3(0.65f, 0.012f, 0.16f),
                hazard, false);
        }

        // Overhead pipes
        for (int i = 0; i < 3; i++)
        {
            float x = 16f + i * 4f;

            Block("Maintenance Ceiling Pipe",
                new Vector3(x, 4.85f, 30),
                new Vector3(0.18f, 0.18f, 15),
                metal, false);
        }
    }

    void SetupNewLights()
    {
        AddLight("Research Blue Light",
            new Vector3(-21, 5.8f, 29),
            new Color(0.4f, 0.75f, 1f),
            3.2f, 13f);

        AddLight("Maintenance Emergency Light",
            new Vector3(20, 4.9f, 29),
            new Color(1f, 0.36f, 0.15f),
            3.0f, 13f);
    }

    void AddLight(
        string name,
        Vector3 position,
        Color color,
        float intensity,
        float range)
    {
        Block(name + " Panel", position,
            new Vector3(0.7f, 0.06f, 1.2f),
            glow, false);

        GameObject obj = new GameObject(name);
        obj.transform.SetParent(transform, false);
        obj.transform.localPosition = position;

        Light light = obj.AddComponent<Light>();

        light.type = LightType.Point;
        light.color = color;
        light.intensity = intensity;
        light.range = range;
        light.shadows = LightShadows.None;
    }
}
