using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Asset-backed, true round 20-inch split-spoke wheels for the widebody Challenger.</summary>
public static class FacilityHellcatWheels
{
    const string Folder = "Assets/Generated/Hellcat";
    const float TireRadius = .385f;

    /// <summary>Car coordinates: X right, Y up, Z front. Tire track 1.73m; overall tire bounds X +/-1.0225, Y .005-.775.</summary>
    public static void Build(Transform car)
    {
        EnsureFolder(Folder + "/Meshes");
        EnsureFolder(Folder + "/Materials");
        EnsureFolder(Folder + "/Textures");
        Transform previous = car.Find("Hellcat widebody wheels");
        if (previous != null) UnityEngine.Object.DestroyImmediate(previous.gameObject);
        Transform root = Child(car, "Hellcat widebody wheels");

        Material rubber = Material("HC Tire satin carbon rubber", new Color(.036f, .040f, .044f), .04f, .17f);
        Material cut = Material("HC Tire tread recesses", new Color(.007f, .009f, .011f), 0f, .08f);
        Material alloy = Material("HC Wheel black forged alloy", new Color(.029f, .034f, .042f), .91f, .69f);
        Material edge = Material("HC Wheel fine machined graphite edges", new Color(.085f, .092f, .106f), .94f, .74f);
        Material rotor = Material("HC Brake brushed carbon iron", new Color(.29f, .30f, .31f), .89f, .47f);
        Material rotorLine = Material("HC Brake concentric wear lines", new Color(.17f, .18f, .19f), .88f, .44f);
        Material caliper = Material("HC Brembo charcoal powdercoat", new Color(.075f, .083f, .090f), .55f, .30f);
        Material fastener = Material("HC Wheel titanium fasteners", new Color(.24f, .255f, .28f), .92f, .62f);
        Material black = Material("HC Brake vent interiors", new Color(.009f, .011f, .014f), .30f, .15f);
        Material tireLetters = Material("HC Tire molded sidewall markings", new Color(.16f, .17f, .18f), .01f, .13f);
        tireLetters.SetTexture("_BaseMap", TireLetterTexture());
        tireLetters.SetFloat("_AlphaClip", 1f);
        tireLetters.SetFloat("_Cutoff", .38f);
        tireLetters.EnableKeyword("_ALPHATEST_ON");
        tireLetters.renderQueue = 2450;
        Material capLetters = Material("HC SRT center cap printing", new Color(.59f, .61f, .64f), .75f, .52f);
        capLetters.SetTexture("_BaseMap", CenterCapTexture());
        capLetters.SetFloat("_AlphaClip", 1f);
        capLetters.SetFloat("_Cutoff", .35f);
        capLetters.EnableKeyword("_ALPHATEST_ON");
        capLetters.renderQueue = 2450;

        // Rounded low-profile 305 section, with four independent tread ribs and three physical rain channels.
        Vector2[] tireProfile = {
            new Vector2(-.118f,.259f),new Vector2(-.137f,.273f),new Vector2(-.150f,.300f),
            new Vector2(-.157f,.335f),new Vector2(-.156f,.354f),new Vector2(-.147f,.374f),
            new Vector2(-.130f,.383f),new Vector2(-.114f,.385f),new Vector2(-.086f,.385f),
            new Vector2(-.083f,.378f),new Vector2(-.075f,.378f),new Vector2(-.072f,.385f),
            new Vector2(-.037f,.385f),new Vector2(-.034f,.379f),new Vector2(-.026f,.379f),
            new Vector2(-.023f,.385f),new Vector2(.023f,.385f),new Vector2(.026f,.379f),
            new Vector2(.034f,.379f),new Vector2(.037f,.385f),new Vector2(.072f,.385f),
            new Vector2(.075f,.378f),new Vector2(.083f,.378f),new Vector2(.086f,.385f),
            new Vector2(.114f,.385f),new Vector2(.130f,.383f),new Vector2(.147f,.374f),
            new Vector2(.156f,.354f),new Vector2(.157f,.335f),new Vector2(.150f,.300f),
            new Vector2(.137f,.273f),new Vector2(.118f,.259f)
        };
        Mesh tireMesh = SaveMesh("HC 305 35 ZR20 sculpted tire", Lathe(tireProfile, 64));
        Mesh letterMesh = SaveMesh("HC Molded sidewall lettering surface", Sidewall());
        Builder sipes = new Builder();
        float[,] bands = { {-.123f,-.090f}, {-.070f,-.040f}, {-.022f,.022f}, {.040f,.070f}, {.090f,.123f} };
        for (int i = 0; i < 48; i++)
            for (int band = 0; band < 5; band++)
            {
                float a = i * Mathf.PI * 2f / 48f + (band % 2) * .035f;
                float direction = band < 2 ? 1f : -1f;
                float a1 = a + .042f * direction;
                float x0 = bands[band, 0], x1 = bands[band, 1];
                sipes.Quad(Radial(x0, TireRadius + .00035f, a - .003f), Radial(x0, TireRadius + .00035f, a + .003f),
                    Radial(x1, TireRadius + .00035f, a1 + .003f), Radial(x1, TireRadius + .00035f, a1 - .003f));
            }
        Mesh sipeMesh = SaveMesh("HC Directional summer tread transverse sipes", sipes.ToMesh());

        // The inside barrel remains open: the complete ventilated brake can be seen between the spokes.
        Vector2[] rimProfile = {
            new Vector2(-.117f,.235f),new Vector2(-.124f,.247f),new Vector2(-.121f,.259f),
            new Vector2(-.105f,.265f),new Vector2(.113f,.265f),new Vector2(.141f,.263f),
            new Vector2(.149f,.258f),new Vector2(.150f,.249f),new Vector2(.143f,.245f),
            new Vector2(.129f,.248f),new Vector2(-.103f,.246f),new Vector2(-.111f,.235f)
        };
        Builder alloyMesh = new Builder();
        alloyMesh.Append(Lathe(rimProfile, 64));
        // Five paired, swept Y spokes. Chamfered faces catch light without turning into silver wheels.
        for (int spoke = 0; spoke < 5; spoke++)
        {
            float angle = spoke * Mathf.PI * 2f / 5f + .19f;
            for (int branch = -1; branch <= 1; branch += 2)
            {
                Vector2[] outline = {
                    Polar2(.067f,angle + branch*.050f), Polar2(.112f,angle + branch*.095f),
                    Polar2(.251f,angle + branch*.211f), Polar2(.254f,angle + branch*.276f),
                    Polar2(.145f,angle + branch*.221f), Polar2(.077f,angle + branch*.181f)
                };
                alloyMesh.Append(BevelExtrusion(outline, .074f, .139f, .0017f));
            }
        }
        alloyMesh.Append(Lathe(new[] { new Vector2(.104f,.066f),new Vector2(.143f,.071f),new Vector2(.153f,.062f),new Vector2(.154f,.001f),new Vector2(.104f,.001f) }, 48));
        Mesh rimMesh = SaveMesh("HC Black Devil five split spoke forged alloy", alloyMesh.ToMesh());

        Builder highlights = new Builder();
        highlights.Append(Lathe(new[] { new Vector2(.1493f,.2485f),new Vector2(.1502f,.2503f),new Vector2(.1491f,.2510f) }, 64));
        highlights.Append(Lathe(new[] { new Vector2(.1545f,.029f),new Vector2(.1560f,.029f),new Vector2(.1570f,.027f),new Vector2(.1570f,.0258f) }, 40));
        Mesh edgeMesh = SaveMesh("HC Wheel subtle graphite lip and center cap edge", highlights.ToMesh());
        Mesh capMesh = SaveMesh("HC SRT wheel center cap lettering", FlatDisc(.1577f, .0245f, 32));

        Builder discs = new Builder();
        for (int side = -1; side <= 1; side += 2)
        {
            float x0 = Mathf.Min(side * .006f,side * .013f), x1 = Mathf.Max(side * .006f,side * .013f);
            discs.Append(Lathe(new[] { new Vector2(x0,.077f),new Vector2(x0,.222f),new Vector2(x1,.222f),new Vector2(x1,.077f),new Vector2(x0,.077f) },64));
        }
        discs.Append(Lathe(new[] {new Vector2(-.020f,.075f),new Vector2(.030f,.075f),new Vector2(.030f,.043f),new Vector2(-.020f,.043f)},40));
        for (int i = 0; i < 32; i++)
        {
            float a = i * Mathf.PI * 2f / 32f;
            Vector3[] vane = {Radial(-.006f,.095f,a),Radial(-.006f,.219f,a+.032f),Radial(.006f,.219f,a+.032f),Radial(.006f,.095f,a)};
            discs.Quad(vane[0],vane[1],vane[2],vane[3]);
            discs.Quad(vane[3],vane[2],vane[1],vane[0]);
        }
        Mesh discMesh = SaveMesh("HC Two piece 390mm ventilated brake rotors",discs.ToMesh());
        Builder wear = new Builder();
        for (int ring = 0; ring < 5; ring++)
        {
            float r=.108f+ring*.023f;
            wear.Append(Lathe(new[] {new Vector2(.0132f,r+.00055f),new Vector2(.0132f,r)},32));
        }
        Mesh wearMesh = SaveMesh("HC Fine concentric rotor machining",wear.ToMesh());
        Builder holes = new Builder();
        for (int row = 0; row < 2; row++)
            for (int i=0;i<16;i++)
            {
                float a=i*Mathf.PI*2f/16f+row*.10f;
                Vector3 c=Radial(.0135f,.156f+row*.039f,a);
                holes.Append(FlatDisc(0f,.0028f,8),Matrix4x4.Translate(c));
            }
        // Recesses in the five lug seats make the real six-sided fasteners sit inside the hub.
        for (int i=0;i<5;i++)
        {
            Vector3 c=Radial(.1548f,.048f,i*Mathf.PI*2f/5f);
            holes.Append(FlatDisc(0,.0095f,12),Matrix4x4.Translate(c));
        }
        Mesh holeMesh=SaveMesh("HC Rotor drilling and recessed lug seats",holes.ToMesh());

        Builder calipers = new Builder();
        calipers.Append(BevelExtrusion(new[] {
            new Vector2(-.078f,-.168f),new Vector2(-.083f,-.204f),new Vector2(-.057f,-.227f),
            new Vector2(.057f,-.227f),new Vector2(.083f,-.204f),new Vector2(.078f,-.168f),
            new Vector2(.044f,-.154f),new Vector2(-.044f,-.154f)},-.043f,.054f,.005f));
        calipers.Append(BevelExtrusion(new[] {new Vector2(-.037f,-.232f),new Vector2(.037f,-.232f),new Vector2(.046f,-.214f),new Vector2(-.046f,-.214f)},-.032f,.041f,.002f));
        Mesh caliperMesh = SaveMesh("HC Six piston sculpted charcoal brake calipers",calipers.ToMesh());
        Builder nuts = new Builder();
        for (int i=0;i<5;i++)
        {
            Vector3 c=Radial(0,.048f,i*Mathf.PI*2f/5f);
            nuts.Append(Lathe(new[] {new Vector2(.1548f,.0065f),new Vector2(.163f,.0065f),new Vector2(.165f,.0054f),new Vector2(.165f,.0001f)},6),Matrix4x4.Translate(c));
        }
        foreach(float y in new[]{-.055f,.055f})
            nuts.Append(Lathe(new[] {new Vector2(.0545f,.004f),new Vector2(.056f,.004f),new Vector2(.0565f,.001f)},10),Matrix4x4.Translate(new Vector3(0,y,-.193f)));
        Mesh nutMesh = SaveMesh("HC Five exposed titanium hex lugs and caliper pins",nuts.ToMesh());

        for (int axle = 0; axle < 2; axle++)
            for (int side = -1; side <= 1; side += 2)
            {
                Transform wheel = Child(root,(axle==0?"Front":"Rear") + (side<0?" left":" right") + " 305 35 ZR20");
                wheel.localPosition = new Vector3(side*.865f,.390f,axle==0?1.48f:-1.48f);
                wheel.localRotation = Quaternion.Euler(0,side<0?180f:0f,0);
                Piece(wheel,"Rounded carbon rubber and physical rain channels",tireMesh,rubber);
                Piece(wheel,"Directional summer tread fine cut sipes",sipeMesh,cut);
                Piece(wheel,"Molded P ZERO sidewall type",letterMesh,tireLetters);
                Piece(wheel,"Black forged five split spokes and deep barrel",rimMesh,alloy);
                Piece(wheel,"Fine rim lip and center cap chamfer",edgeMesh,edge);
                Piece(wheel,"SRT center cap signature",capMesh,capLetters);
                Piece(wheel,"Ventilated cast iron two piece brake discs",discMesh,rotor);
                Piece(wheel,"Concentric machined rotor wear",wearMesh,rotorLine);
                Piece(wheel,"Drilled rotor recesses and lug wells",holeMesh,black);
                Piece(wheel,"Six piston fixed Brembo caliper",caliperMesh,caliper);
                Piece(wheel,"Hex lugs and caliper hardware",nutMesh,fastener);
            }
    }

    static Transform Child(Transform parent,string name)
    {
        Transform t=new GameObject(name).transform;t.SetParent(parent,false);return t;
    }

    static void Piece(Transform parent,string name,Mesh mesh,Material material)
    {
        Transform t=Child(parent,name);
        t.gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;
        MeshRenderer r=t.gameObject.AddComponent<MeshRenderer>();r.sharedMaterial=material;
        r.shadowCastingMode=ShadowCastingMode.On;r.receiveShadows=true;
        r.lightProbeUsage=LightProbeUsage.BlendProbes;r.reflectionProbeUsage=ReflectionProbeUsage.BlendProbes;
    }

    static Material Material(string name,Color color,float metallic,float smoothness)
    {
        string path=Folder+"/Materials/"+name+".mat";
        Material m=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(m==null){m=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(m,path);}
        m.name=name;m.SetColor("_BaseColor",color);m.SetFloat("_Metallic",metallic);m.SetFloat("_Smoothness",smoothness);
        EditorUtility.SetDirty(m);return m;
    }

    static Mesh SaveMesh(string name,Mesh mesh)
    {
        string path=Folder+"/Meshes/"+name+".asset";mesh.name=name;
        Mesh old=AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if(old!=null){EditorUtility.CopySerialized(mesh,old);UnityEngine.Object.DestroyImmediate(mesh);EditorUtility.SetDirty(old);return old;}
        AssetDatabase.CreateAsset(mesh,path);return mesh;
    }

    static void EnsureFolder(string path)
    {
        if(AssetDatabase.IsValidFolder(path))return;
        string parent=Path.GetDirectoryName(path).Replace('\\','/');EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent,Path.GetFileName(path));
    }

    static Vector3 Radial(float x,float radius,float angle){return new Vector3(x,Mathf.Cos(angle)*radius,Mathf.Sin(angle)*radius);}
    static Vector2 Polar2(float r,float a){return new Vector2(Mathf.Cos(a)*r,Mathf.Sin(a)*r);}

    static Mesh Lathe(Vector2[] profile,int segments)
    {
        var v=new List<Vector3>();var n=new List<Vector3>();var uv=new List<Vector2>();var t=new List<int>();
        for(int row=0;row<profile.Length;row++)
        {
            Vector2 before=profile[Mathf.Max(0,row-1)],after=profile[Mathf.Min(profile.Length-1,row+1)];
            Vector2 tangent=(after-before).normalized;
            // Profile travels from inboard bead, across the tread, to the outboard bead.
            Vector2 normal=new Vector2(-tangent.y,tangent.x);
            for(int i=0;i<=segments;i++)
            {
                float a=i*Mathf.PI*2f/segments;
                v.Add(Radial(profile[row].x,profile[row].y,a));
                n.Add(new Vector3(normal.x,normal.y*Mathf.Cos(a),normal.y*Mathf.Sin(a)));
                uv.Add(new Vector2((float)i/segments,(float)row/(profile.Length-1)));
            }
        }
        for(int row=0;row<profile.Length-1;row++)for(int i=0;i<segments;i++)
        {
            int a=row*(segments+1)+i,b=a+segments+1;
            t.Add(a);t.Add(b+1);t.Add(b);t.Add(a);t.Add(a+1);t.Add(b+1);
        }
        Mesh m=new Mesh();m.SetVertices(v);m.SetNormals(n);m.SetUVs(0,uv);m.SetTriangles(t,0);m.RecalculateBounds();return m;
    }

    static Mesh Sidewall()
    {
        Mesh m=Lathe(new[]{new Vector2(.1569f,.351f),new Vector2(.1576f,.338f),new Vector2(.1575f,.324f),new Vector2(.1515f,.301f)},64);
        Vector3[] v=m.vertices;Vector2[] uv=new Vector2[v.Length];
        for(int i=0;i<v.Length;i++)uv[i]=new Vector2(.5f+v[i].z/.77f,.5f+v[i].y/.77f);
        m.uv=uv;return m;
    }

    static Mesh FlatDisc(float x,float radius,int segments)
    {
        Builder b=new Builder();Vector3 c=new Vector3(x,0,0);
        for(int i=0;i<segments;i++)
        {
            float a=i*Mathf.PI*2f/segments,a1=(i+1)*Mathf.PI*2f/segments;
            Vector3 p=Radial(x,radius,a),q=Radial(x,radius,a1);
            b.Triangle(c,p,q,new Vector2(.5f,.5f),new Vector2(.5f+p.z/(radius*2),.5f+p.y/(radius*2)),new Vector2(.5f+q.z/(radius*2),.5f+q.y/(radius*2)));
        }
        return b.ToMesh();
    }

    static Mesh BevelExtrusion(Vector2[] poly,float back,float front,float bevel)
    {
        // Normalize polygon winding so both left and right swept branches have outward-facing front faces.
        float area=0;for(int i=0;i<poly.Length;i++){Vector2 a=poly[i],b=poly[(i+1)%poly.Length];area+=a.x*b.y-b.x*a.y;}
        if(area<0){poly=(Vector2[])poly.Clone();Array.Reverse(poly);}
        Vector2 center=Vector2.zero;foreach(Vector2 p in poly)center+=p;center/=poly.Length;
        Builder builder=new Builder();
        for(int i=0;i<poly.Length;i++)
        {
            int j=(i+1)%poly.Length;
            Vector2 ia=poly[i]+(center-poly[i]).normalized*bevel,ib=poly[j]+(center-poly[j]).normalized*bevel;
            Vector3 a=new Vector3(back,poly[i].x,poly[i].y),b=new Vector3(back,poly[j].x,poly[j].y);
            Vector3 c=new Vector3(front-bevel,poly[j].x,poly[j].y),d=new Vector3(front-bevel,poly[i].x,poly[i].y);
            Vector3 e=new Vector3(front,ia.x,ia.y),f=new Vector3(front,ib.x,ib.y);
            builder.Quad(a,b,c,d);builder.Quad(d,c,f,e);
            builder.Triangle(new Vector3(front,center.x,center.y),e,f);
            builder.Triangle(new Vector3(back,center.x,center.y),b,a);
        }
        return builder.ToMesh();
    }

    sealed class Builder
    {
        readonly List<Vector3> v=new List<Vector3>();readonly List<Vector3> n=new List<Vector3>();
        readonly List<Vector2> uv=new List<Vector2>();readonly List<int> t=new List<int>();
        public void Triangle(Vector3 a,Vector3 b,Vector3 c){Triangle(a,b,c,Vector2.zero,Vector2.right,Vector2.up);}
        public void Triangle(Vector3 a,Vector3 b,Vector3 c,Vector2 ua,Vector2 ub,Vector2 uc)
        {
            int start=v.Count;Vector3 normal=Vector3.Cross(b-a,c-a).normalized;
            v.Add(a);v.Add(b);v.Add(c);n.Add(normal);n.Add(normal);n.Add(normal);uv.Add(ua);uv.Add(ub);uv.Add(uc);
            t.Add(start);t.Add(start+1);t.Add(start+2);
        }
        public void Quad(Vector3 a,Vector3 b,Vector3 c,Vector3 d){Triangle(a,b,c);Triangle(a,c,d);}
        public void Append(Mesh mesh){Append(mesh,Matrix4x4.identity);}
        public void Append(Mesh mesh,Matrix4x4 transform)
        {
            int start=v.Count;Vector3[] mv=mesh.vertices,mn=mesh.normals;Vector2[] mu=mesh.uv;
            for(int i=0;i<mv.Length;i++){v.Add(transform.MultiplyPoint3x4(mv[i]));n.Add(transform.MultiplyVector(mn[i]).normalized);uv.Add(mu.Length>i?mu[i]:Vector2.zero);}
            foreach(int index in mesh.triangles)t.Add(start+index);
            UnityEngine.Object.DestroyImmediate(mesh);
        }
        public Mesh ToMesh(){Mesh m=new Mesh();if(v.Count>65535)m.indexFormat=IndexFormat.UInt32;m.SetVertices(v);m.SetNormals(n);m.SetUVs(0,uv);m.SetTriangles(t,0);m.RecalculateBounds();return m;}
    }

    // Small molded lettering is baked onto one shared transparent atlas rather than hundreds of text components.
    static Texture2D TireLetterTexture()
    {
        const int size=1024;Color32[] pixels=new Color32[size*size];
        ArcText(pixels,size,"P ZERO",0,.330f,.0037f);
        ArcText(pixels,size,"PIRELLI",Mathf.PI,.330f,.0032f);
        ArcText(pixels,size,"305/35 ZR20",-Mathf.PI*.5f,.330f,.0019f);
        ArcText(pixels,size,"RADIAL TUBELESS",Mathf.PI*.5f,.330f,.0017f);
        return SaveTexture("HC Molded tire sidewall lettering",size,pixels);
    }

    static Texture2D CenterCapTexture()
    {
        const int size=256;Color32[] pixels=new Color32[size*size];string text="SRT";
        for(int ch=0;ch<text.Length;ch++)
        {
            string glyph=Glyph(text[ch]);
            for(int row=0;row<7;row++)for(int col=0;col<5;col++)if(glyph[row*5+col]=='1')
            {
                int x=43+ch*58+col*9+(6-row)*3,y=94+(6-row)*9;
                for(int yy=0;yy<9;yy++)for(int xx=0;xx<9;xx++)pixels[(y+yy)*size+x+xx]=new Color32(255,255,255,255);
            }
        }
        return SaveTexture("HC SRT wheel center cap",size,pixels);
    }

    static void ArcText(Color32[] pixels,int size,string text,float angle,float radius,float scale)
    {
        float width=(text.Length*6-1)*scale;
        for(int ch=0;ch<text.Length;ch++)
        {
            string glyph=Glyph(text[ch]);
            for(int row=0;row<7;row++)for(int col=0;col<5;col++)if(glyph[row*5+col]=='1')
            {
                float offset=(ch*6+col)*scale-width*.5f;
                float a=angle+offset/radius;
                float r=radius+(3-row)*scale;
                for(int sy=0;sy<3;sy++)for(int sx=0;sx<3;sx++)
                {
                    float ap=a+(sx-1)*scale/(3*radius),rp=r+(sy-1)*scale/3f;
                    int x=Mathf.RoundToInt((.5f+Mathf.Sin(ap)*rp/.77f)*size);
                    int y=Mathf.RoundToInt((.5f+Mathf.Cos(ap)*rp/.77f)*size);
                    int brush=Mathf.Max(1,Mathf.RoundToInt(scale*size/.77f/5));
                    for(int dy=-brush;dy<=brush;dy++)for(int dx=-brush;dx<=brush;dx++)
                        if(x+dx>=0&&x+dx<size&&y+dy>=0&&y+dy<size)pixels[(y+dy)*size+x+dx]=new Color32(255,255,255,255);
                }
            }
        }
    }

    static Texture2D SaveTexture(string name,int size,Color32[] pixels)
    {
        Texture2D texture=new Texture2D(size,size,TextureFormat.RGBA32,false);texture.SetPixels32(pixels);texture.Apply();
        string path=Folder+"/Textures/"+name+".png";File.WriteAllBytes(path,texture.EncodeToPNG());UnityEngine.Object.DestroyImmediate(texture);
        AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceUpdate);
        TextureImporter importer=AssetImporter.GetAtPath(path) as TextureImporter;
        if(importer!=null){importer.alphaIsTransparency=true;importer.mipmapEnabled=true;importer.wrapMode=TextureWrapMode.Clamp;importer.filterMode=FilterMode.Trilinear;importer.textureCompression=TextureImporterCompression.Uncompressed;importer.SaveAndReimport();}
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    static string Glyph(char c)
    {
        switch(c)
        {
            case 'A':return "01110100011000111111100011000110001";
            case 'B':return "11110100011000111110100011000111110";
            case 'D':return "11110100011000110001100011000111110";
            case 'E':return "11111100001000011110100001000011111";
            case 'I':return "11111001000010000100001000010011111";
            case 'L':return "10000100001000010000100001000011111";
            case 'O':return "01110100011000110001100011000101110";
            case 'P':return "11110100011000111110100001000010000";
            case 'R':return "11110100011000111110101001001010001";
            case 'S':return "01111100001000001110000010000111110";
            case 'T':return "11111001000010000100001000010000100";
            case 'U':return "10001100011000110001100011000101110";
            case 'Z':return "11111000010001000100010001000011111";
            case '0':return "01110100011001110101110011000101110";
            case '2':return "01110100010000100010001000100011111";
            case '3':return "11110000010000101110000010000111110";
            case '5':return "11111100001000011110000010000111110";
            case '/':return "00001000010001000100010001000010000";
            default:return "00000000000000000000000000000000000";
        }
    }
}
