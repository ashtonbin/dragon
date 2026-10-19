
using UnityEngine;
using UnityEngine.Rendering;

public class RetroWorldBuilder : MonoBehaviour
{
    private Material wall, floor, ceiling, metal;
    private Material blueGlow, orangeGlow;

    void Start()
    {
        CreateMaterials();
        BuildLevel();
        SetupLighting();
    }

    void CreateMaterials()
    {
        wall = MakeMaterial("Industrial Wall",
            new Color32(105, 116, 120, 255), 0);

        floor = MakeMaterial("Industrial Floor",
            new Color32(65, 72, 76, 255), 1);

        ceiling = MakeMaterial("Ceiling",
            new Color32(43, 48, 54, 255), 2);

        metal = MakeMaterial("Rusted Metal",
            new Color32(110, 77, 59, 255), 3);

        blueGlow = GlowMaterial(
            new Color(0.45f, 0.75f, 1f));

        orangeGlow = GlowMaterial(
            new Color(1f, 0.45f, 0.15f));
    }

    Material MakeMaterial(string name, Color32 baseColor, int style)
    {
        Texture2D texture = new Texture2D(
            32, 32, TextureFormat.RGBA32, false);

        texture.name = name + " Texture";
        texture.filterMode = FilterMode.Point;
        texture.wrapMode = TextureWrapMode.Repeat;

        Color32[] pixels = new Color32[32 * 32];

        for (int y = 0; y < 32; y++)
        {
            for (int x = 0; x < 32; x++)
            {
                int variation =
                    ((x * 17 + y * 29 + x * y * 3) % 19) - 9;

                if (style == 0)
                {
                    if (x % 16 == 0 || y % 16 == 0)
                        variation -= 35;

                    if (x % 16 == 4 && y % 16 == 4)
                        variation += 45;
                }
                else if (style == 1)
                {
                    if (x % 8 == 0 || y % 8 == 0)
                        variation -= 28;
                }
                else if (style == 2)
                {
                    if (x % 16 == 0 || y % 16 == 0)
                        variation -= 20;
                }
                else
                {
                    if ((x * 7 + y * 13) % 31 < 5)
                        variation -= 38;

                    if (x % 16 == 0)
                        variation -= 25;
                }

                pixels[y * 32 + x] = new Color32(
                    (byte)Mathf.Clamp(baseColor.r + variation, 0, 255),
                    (byte)Mathf.Clamp(baseColor.g + variation, 0, 255),
                    (byte)Mathf.Clamp(baseColor.b + variation, 0, 255),
                    255);
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply(false, true);

        Material material = new Material(
            Shader.Find("Universal Render Pipeline/Lit"));

        material.name = name;
        material.SetTexture("_BaseMap", texture);
        material.SetFloat("_Smoothness", 0.05f);

        return material;
    }

    Material GlowMaterial(Color color)
    {
        Material material = new Material(
            Shader.Find("Universal Render Pipeline/Unlit"));

        material.SetColor("_BaseColor", color);
        return material;
    }

    void Block(string name, Vector3 position,
        Vector3 size, Material material)
    {
        GameObject obj = GameObject.CreatePrimitive(
            PrimitiveType.Cube);

        obj.name = name;
        obj.transform.SetParent(transform, false);
        obj.transform.localPosition = position;
        obj.transform.localScale = size;

        MeshFilter filter = obj.GetComponent<MeshFilter>();
        Mesh mesh = Instantiate(filter.sharedMesh);

        Vector2[] uv = mesh.uv;
        Vector3[] normals = mesh.normals;

        for (int i = 0; i < uv.Length; i++)
        {
            Vector2 tiling;

            if (Mathf.Abs(normals[i].y) > 0.5f)
                tiling = new Vector2(size.x / 2f, size.z / 2f);
            else if (Mathf.Abs(normals[i].x) > 0.5f)
                tiling = new Vector2(size.z / 2f, size.y / 2f);
            else
                tiling = new Vector2(size.x / 2f, size.y / 2f);

            tiling.x = Mathf.Max(1f, tiling.x);
            tiling.y = Mathf.Max(1f, tiling.y);

            uv[i] = Vector2.Scale(uv[i], tiling);
        }

        mesh.uv = uv;
        filter.sharedMesh = mesh;

        MeshRenderer renderer = obj.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;

        obj.isStatic = true;
    }

    void BuildLevel()
    {
        // START ROOM
        Block("Start Floor", new Vector3(0, -0.25f, 0),
            new Vector3(12, 0.5f, 12), floor);

        Block("Start Ceiling", new Vector3(0, 4.15f, 0),
            new Vector3(12, 0.3f, 12), ceiling);

        Block("Back Wall", new Vector3(0, 2, -6),
            new Vector3(12, 4, 0.4f), wall);

        Block("Left Wall", new Vector3(-6, 2, 0),
            new Vector3(0.4f, 4, 12), wall);

        Block("Right Wall", new Vector3(6, 2, 0),
            new Vector3(0.4f, 4, 12), wall);

        Block("Exit Left", new Vector3(-4, 2, 6),
            new Vector3(4, 4, 0.4f), wall);

        Block("Exit Right", new Vector3(4, 2, 6),
            new Vector3(4, 4, 0.4f), wall);

        // MAIN CORRIDOR
        Block("Corridor Floor", new Vector3(0, -0.25f, 14),
            new Vector3(4, 0.5f, 16), floor);

        Block("Corridor Ceiling", new Vector3(0, 4.15f, 14),
            new Vector3(4, 0.3f, 16), ceiling);

        Block("Corridor Left", new Vector3(-2, 2, 14),
            new Vector3(0.4f, 4, 16), wall);

        Block("Corridor Right", new Vector3(2, 2, 14),
            new Vector3(0.4f, 4, 16), wall);

        // CENTRAL FACTORY
        Block("Factory Floor", new Vector3(0, -0.25f, 30),
            new Vector3(16, 0.5f, 16), floor);

        Block("Factory Ceiling", new Vector3(0, 4.15f, 30),
            new Vector3(16, 0.3f, 16), ceiling);

        Block("Entrance Left", new Vector3(-5, 2, 22),
            new Vector3(6, 4, 0.4f), wall);

        Block("Entrance Right", new Vector3(5, 2, 22),
            new Vector3(6, 4, 0.4f), wall);

        Block("Factory Back", new Vector3(0, 2, 38),
            new Vector3(16, 4, 0.4f), wall);

        // LEFT FACTORY DOORWAY
        Block("Factory Left A", new Vector3(-8, 2, 25),
            new Vector3(0.4f, 4, 6), wall);

        Block("Factory Left B", new Vector3(-8, 2, 35),
            new Vector3(0.4f, 4, 6), wall);

        Block("Left Door Header", new Vector3(-8, 3.5f, 30),
            new Vector3(0.4f, 1, 4), wall);

        // RIGHT FACTORY DOORWAY
        Block("Factory Right A", new Vector3(8, 2, 25),
            new Vector3(0.4f, 4, 6), wall);

        Block("Factory Right B", new Vector3(8, 2, 35),
            new Vector3(0.4f, 4, 6), wall);

        Block("Right Door Header", new Vector3(8, 3.5f, 30),
            new Vector3(0.4f, 1, 4), wall);

        // MACHINERY
        Block("Crate A", new Vector3(-4, 1, 28),
            new Vector3(2, 2, 2), metal);

        Block("Crate B", new Vector3(4, 0.75f, 32),
            new Vector3(2, 1.5f, 2), metal);

        Block("Large Machine", new Vector3(0, 1, 35),
            new Vector3(3, 2, 1.5f), metal);

        // PIPES
        Block("Left Pipe", new Vector3(-5.7f, 2.8f, 0),
            new Vector3(0.18f, 0.18f, 10), metal);

        Block("Corridor Pipe", new Vector3(-1.7f, 3.1f, 14),
            new Vector3(0.15f, 0.15f, 15), metal);

        Block("Factory Pipe", new Vector3(7.7f, 3, 30),
            new Vector3(0.2f, 0.2f, 14), metal);
    }

    void SetupLighting()
    {
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight =
            new Color(0.27f, 0.30f, 0.35f);

        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogColor =
            new Color(0.07f, 0.09f, 0.12f);
        RenderSettings.fogStartDistance = 8f;
        RenderSettings.fogEndDistance = 45f;

        Light[] lights = FindObjectsByType<Light>(
            FindObjectsSortMode.None);

        foreach (Light light in lights)
        {
            if (light.type == LightType.Directional)
            {
                light.intensity = 0.12f;
                light.shadows = LightShadows.None;
            }
        }

        AddLamp("Starting Light", new Vector3(0, 3.7f, 0),
            new Color(0.4f, 0.7f, 1f), 2.8f, 10f, blueGlow);

        AddLamp("Corridor Light", new Vector3(0, 3.7f, 14),
            new Color(0.4f, 0.7f, 1f), 2.5f, 9f, blueGlow);

        AddLamp("Factory Light", new Vector3(-3, 3.7f, 29),
            new Color(1f, 0.35f, 0.12f), 3f, 11f, orangeGlow);

        AddLamp("Factory Blue Light", new Vector3(4, 3.7f, 34),
            new Color(0.35f, 0.65f, 1f), 2f, 9f, blueGlow);
    }

    void AddLamp(string name, Vector3 position,
        Color color, float intensity, float range,
        Material panelMaterial)
    {
        Block(name + " Housing",
            position + Vector3.up * 0.18f,
            new Vector3(0.8f, 0.15f, 0.8f), metal);

        Block(name + " Panel", position,
            new Vector3(0.6f, 0.06f, 0.6f),
            panelMaterial);

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
