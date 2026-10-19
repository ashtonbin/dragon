using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

/// <summary>Asset-backed Challenger lamps, grille, machined trim and a complete visible leather cabin.</summary>
public static class FacilityHellcatDetails
{
    private const string Root = "Assets/Generated/Hellcat";
    private static Material paint, fasciaPaint, black, graphite, chrome, leather, rubber, lens, halo, projectorWhite, red, amber, stitch, display, worldText;

    public static void Build(Transform car, Material silverPaint = null, Material stripedPaint = null)
    {
        if (car == null) throw new ArgumentNullException(nameof(car));
        Ensure(Root + "/Meshes"); Ensure(Root + "/Materials");
        foreach (string name in new[] { "Challenger precision exterior details", "Challenger cabin" })
        {
            Transform old = car.Find(name);
            if (old != null) Object.DestroyImmediate(old.gameObject);
        }
        paint = silverPaint != null ? silverPaint : Mat("Detail silver metallic", new Color(.62f,.66f,.70f), .65f,.78f);
        fasciaPaint=stripedPaint!=null?stripedPaint:paint;
        black = Mat("Fascia deep black recess", new Color(.007f,.009f,.012f), .12f,.26f);
        graphite = Mat("Satin graphite mouldings", new Color(.035f,.041f,.048f), .44f,.38f);
        chrome = Mat("Machined aluminium bright edges", new Color(.53f,.58f,.63f), .93f,.83f);
        leather = Mat("Charcoal perforated Nappa leather", new Color(.022f,.025f,.028f), .02f,.27f);
        LeatherGrain(leather);
        rubber = Mat("Soft touch dashboard and seals", new Color(.015f,.019f,.022f), 0,.21f);
        lens = Mat("Smoked projector glass", new Color(.045f,.082f,.12f), .76f,.98f);
        halo = Mat("Warm white LED halo", new Color(.91f,.925f,.88f), .15f,.71f, new Color(1.05f,1.02f,.83f));
        projectorWhite = Mat("Warm illuminated xenon optical glass",new Color(.96f,.96f,.88f),.12f,.88f,new Color(2.2f,2.16f,1.94f));
        red = Mat("Ruby red tail lamp optics", new Color(.43f,.007f,.012f), .28f,.77f);
        amber = Mat("Amber side marker optics", new Color(.67f,.20f,.004f), .18f,.72f);
        stitch = Mat("Pewter leather stitching", new Color(.27f,.29f,.29f), .04f,.18f);
        display = Mat("Ice blue instrument backlight", new Color(.055f,.25f,.33f), .10f,.45f);
        Transform exterior = Group(car, "Challenger precision exterior details");
        Font lettering=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");worldText=WorldTextMaterial(lettering);
        FacilitySignFont fontBinder=exterior.gameObject.AddComponent<FacilitySignFont>();fontBinder.font=lettering;fontBinder.material=worldText;fontBinder.Refresh();
        Front(exterior); Rear(exterior); SideTrim(exterior);
        Transform spoiler=Group(exterior,"Hellcat rear deck spoiler");
        Box(spoiler,"Satin black low rear deck lip",new Vector3(0,1.065f,-2.304f),new Vector3(1.806f,.071f,.191f),graphite,.034f);
        Box(spoiler,"Spoiler trailing raised blade",new Vector3(0,1.092f,-2.379f),new Vector3(1.774f,.026f,.049f),graphite,.012f);
        Cabin(Group(car, "Challenger cabin"));
    }

    private static void Front(Transform parent)
    {
        Transform fascia = Group(parent, "Four projector Hellcat front fascia");
        // A single moulded, continuously shaded painted skin surrounds every opening.
        Shape(fascia,"Continuous compound curved silver front bumper",FrontBumperSkin(),Vector3.zero,fasciaPaint);
        Shape(fascia,"Upper grille recessed black backing",Panel("Front upper black pocket",1.946f,.285f,.05f),new Vector3(0,.793f,2.457f),black);
        Shape(fascia,"Upper grille black inner rolled border",Frame("Front upper dark inner border",1.946f,.285f,.052f,.014f,.025f),new Vector3(0,.793f,2.490f),graphite);

        Geo upper = new Geo();
        // The reference has a finely subdivided horizontal rectangular mesh.
        for(int row=0;row<8;row++)
        {
            float y=.685f+row*.029f;
            upper.Box(new Vector3(0,y,2.497f),new Vector3(1.10f,.008f,.012f));
            for(int col=0;col<35;col++)
                upper.Box(new Vector3(-.54f+col*.032f+(row%2)*.016f,y+.011f,2.496f),new Vector3(.006f,.020f,.013f));
        }
        Shape(fascia,"Individual upper grille lattice bars",Save("Upper rectangular grille lattice",upper),Vector3.zero,graphite);
        for(int side=-1;side<=1;side+=2)
        {
            Projector(fascia,side*.829f,.793f,false);
            Projector(fascia,side*.607f,.793f,true);
        }
        Shape(fascia,"Lower grille recessed cavity",Panel("Lower intake backing",1.480f,.285f,.071f),new Vector3(0,.422f,2.461f),black);
        Shape(fascia,"Lower grille satin internal rim",Frame("Lower intake rolled surround",1.480f,.285f,.060f,.010f,.030f),new Vector3(0,.422f,2.483f),black);
        Geo lower = new Geo();
        for(int row=0;row<8;row++) for(int col=0;col<42;col++)
        {
            float x=-.713f+col*.034f+(row%2)*.017f, y=.302f+row*.034f;
            if(Mathf.Abs(x)>.713f || (Mathf.Abs(x)>.65f && (y<.335f||y>.52f)))continue;
            lower.Hex(new Vector3(x,y,2.489f),.0190f,.0024f,.008f);
        }
        Shape(fascia,"Open honeycomb lower grille mesh",Save("Lower intake hexagonal grille",lower),Vector3.zero,graphite);
        for(int side=-1;side<=1;side+=2)
        {
            Transform corner=Group(fascia,"Outer brake cooling duct "+side);
            corner.localPosition=new Vector3(side*.892f,.424f,2.456f);
            Shape(corner,"Recessed open black duct",Panel("Corner duct face",.132f,.269f,.041f),Vector3.zero,black);
            Shape(corner,"Rolled brake duct rim",Frame("Corner duct rim",.138f,.277f,.043f,.009f,.018f),new Vector3(0,0,.014f),black);
            Geo baffles=new Geo();
            for(int n=0;n<6;n++)baffles.Box(new Vector3(0,-.100f+n*.038f,.015f),new Vector3(.115f,.008f,.018f));
            Shape(corner,"Six duct cooling vanes",Save("Corner duct six vanes",baffles),Vector3.zero,graphite);
        }
        // A smooth extruded blade, with the side fins returning underneath each fender.
        Box(fascia,"Black full width front splitter",new Vector3(0,.195f,2.444f),new Vector3(2.075f,.033f,.220f),graphite,.016f);
        for(int side=-1;side<=1;side+=2)
        {
            Box(fascia,"Splitter side wing "+side,new Vector3(side*1.002f,.202f,2.237f),new Vector3(.092f,.040f,.490f),graphite,.019f);
            Box(fascia,"Splitter wing silver fastener "+side,new Vector3(side*.974f,.225f,2.454f),new Vector3(.009f,.004f,.009f),chrome,.002f);
        }
        Label(fascia,"SRT machined grille badge","SRT",new Vector3(.354f,.759f,2.518f),Quaternion.identity,.047f,chrome,true);
        CatBadge(fascia,"Small snarling cat grille emblem",new Vector3(.469f,.760f,2.519f),Quaternion.identity,.40f,chrome);
    }

    private static void Projector(Transform parent,float x,float y,bool airCatcher)
    {
        Transform light=Group(parent,(airCatcher?"Air catcher inner":"Bi xenon outer")+" projector "+x.ToString("0.000",System.Globalization.CultureInfo.InvariantCulture));
        light.localPosition=new Vector3(x,y,2.494f);
        Shape(light,"Deep round lamp bucket",Disc("Headlamp dark recessed cylinder",.106f,.030f,64),new Vector3(0,0,-.014f),black);
        Shape(light,"Satin lamp bezel outer return",Torus("Headlamp outer machined rim",.103f,.0060f,72,10),new Vector3(0,0,.002f),graphite);
        Shape(light,"Thin polished LED bezel",Torus("Headlamp polished LED rim",.0948f,.0027f,72,8),new Vector3(0,0,.012f),chrome);
        Shape(light,"Continuous hollow warm LED halo",Torus("Headlamp glowing hollow halo",.0883f,.0044f,96,10),new Vector3(0,0,.015f),halo);
        Shape(light,"Inner projector aluminium ring",Torus("Headlamp inner projector ring",.0705f,.0035f,64,8),new Vector3(0,0,.005f),chrome);
        if(airCatcher)
        {
            Shape(light,"Functional hollow air catcher throat",Disc("Hollow air catcher throat",.067f,.025f,64),new Vector3(0,0,-.014f),black);
            Shape(light,"Air catcher venturi inner rim",Torus("Air catcher inner bevel",.055f,.005f,64,8),new Vector3(0,0,-.006f),graphite);
            Geo ribs=new Geo();
            for(int n=0;n<7;n++)ribs.Box(new Vector3((n-3)*.013f,0,-.012f),new Vector3(.003f,.087f,.009f));
            Shape(light,"Deep air catcher vertical vanes",Save("Air catcher seven intake vanes",ribs),Vector3.zero,graphite);
        }
        else
        {
            Shape(light,"Illuminated convex xenon projector glass",Ellipsoid("Headlamp convex optical lens",new Vector3(.067f,.067f,.018f),40,20),new Vector3(0,0,.006f),projectorWhite);
            Geo marks=new Geo();
            for(int n=0;n<7;n++)
            {
                float a=(17+n*8)*Mathf.Deg2Rad;
                marks.Box(new Vector3(Mathf.Cos(a)*.057f,Mathf.Sin(a)*.057f,.025f),new Vector3(.0035f,.009f,.001f));
            }
            Shape(light,"Etched projector optical fluting",Save("Projector lens optical marks",marks),Vector3.zero,halo);
        }
    }

    private static Mesh FrontBumperSkin()
    {
        Geo skin=new Geo();
        SkinRing(skin,new Vector2(0,.793f),2.042f,.434f,.093f,1.945f,.286f,.051f);
        SkinRing(skin,new Vector2(0,.424f),1.638f,.428f,.092f,1.477f,.276f,.061f);
        for(int side=-1;side<=1;side+=2)
            SkinRing(skin,new Vector2(side*.891f,.424f),.268f,.428f,.072f,.135f,.272f,.042f);
        skin.UseHorizontalStripeUV();
        return Save("Continuous moulded Challenger front bumper skin",skin);
    }
    private static void SkinRing(Geo skin,Vector2 center,float outerWidth,float outerHeight,float outerRadius,float innerWidth,float innerHeight,float innerRadius)
    {
        Vector2[] outer=RoundOutline(outerWidth,outerHeight,outerRadius),inner=RoundOutline(innerWidth,innerHeight,innerRadius);
        const int spans=8;
        for(int edge=0;edge<outer.Length;edge++)
        {
            int next=(edge+1)%outer.Length;
            for(int span=0;span<spans;span++)
            {
                float t0=span/(float)spans,t1=(span+1)/(float)spans;
                Vector2 a=center+Vector2.Lerp(inner[edge],outer[edge],t0),b=center+Vector2.Lerp(inner[next],outer[next],t0);
                Vector2 c=center+Vector2.Lerp(inner[next],outer[next],t1),d=center+Vector2.Lerp(inner[edge],outer[edge],t1);
                skin.Quad(SkinPoint(a),SkinPoint(b),SkinPoint(c),SkinPoint(d),SkinNormal(a),SkinNormal(b),SkinNormal(c),SkinNormal(d));
            }
            // Rounded painted lip rolls away from the observer into each recessed opening.
            Vector2 i0=center+inner[edge],i1=center+inner[next];Vector3 p0=SkinPoint(i0),p1=SkinPoint(i1);
            Vector3 inward0=(Vector3)((i0-center).normalized)*.006f,inward1=(Vector3)((i1-center).normalized)*.006f;
            Vector3 q0=p0+inward0-Vector3.forward*.030f,q1=p1+inward1-Vector3.forward*.030f;
            skin.Quad(p0,p1,q1,q0);
        }
    }
    private static Vector3 SkinPoint(Vector2 at){return new Vector3(at.x,at.y,SkinZ(at.x,at.y));}
    private static Vector3 SkinNormal(Vector2 at)
    {
        const float d=.0005f;
        float dx=(SkinZ(at.x+d,at.y)-SkinZ(at.x-d,at.y))/(d*2),dy=(SkinZ(at.x,at.y+d)-SkinZ(at.x,at.y-d))/(d*2);
        return new Vector3(-dx,-dy,1).normalized;
    }
    private static float SkinZ(float x,float y)
    {
        float top=Mathf.Clamp01((y-.934f)/.076f),bottom=Mathf.Clamp01((.300f-y)/.090f);
        float corner=Mathf.SmoothStep(0,1,Mathf.Clamp01((Mathf.Abs(x)-.940f)/.082f));
        float bridge=Mathf.Exp(-Mathf.Pow((y-.611f)/.094f,2))*.014f;
        float grille=Mathf.Exp(-Mathf.Pow((y-.800f)/.118f,2))*.018f;
        return 2.513f+bridge-grille-.109f*top*top-.067f*bottom*bottom-.121f*corner*(1-.88f*top);
    }

    private static void Rear(Transform parent)
    {
        Transform rear=Group(parent,"Challenger full width rear light assembly");
        rear.localPosition=new Vector3(0,0,-2.489f); rear.localRotation=Quaternion.Euler(0,180,0);
        Box(rear,"Rear fascia silver rolled top",new Vector3(0,.967f,-.022f),new Vector3(1.94f,.060f,.118f),paint,.024f);
        Shape(rear,"Full width gloss black deck panel",Panel("Rear black deck panel",1.940f,.269f,.047f),new Vector3(0,.805f,.007f),black);
        Shape(rear,"Deck panel polished black rim",Frame("Rear dark deck surround",1.96f,.288f,.053f,.012f,.022f),new Vector3(0,.805f,.013f),graphite);
        for(int side=-1;side<=1;side+=2)
        {
            Transform lamp=Group(rear,"Signature red rectangular tail loop "+side);lamp.localPosition=new Vector3(side*.514f,.804f,.020f);
            Shape(lamp,"Red smoked rectangular lens cavity",Panel("Rear smoked taillamp",.823f,.195f,.039f),Vector3.zero,black);
            Shape(lamp,"Unbroken ruby perimeter light guide",Frame("Rear red optical loop",.819f,.192f,.038f,.017f,.010f),new Vector3(0,0,.007f),red);
            Shape(lamp,"Thin inner optical loop",Frame("Rear inner red optical loop",.762f,.140f,.024f,.004f,.004f),new Vector3(0,0,.009f),red);
            Geo lensRibs=new Geo();
            for(int n=0;n<42;n++)lensRibs.Box(new Vector3(-.369f+n*.018f,0,.010f),new Vector3(.004f,.075f,.003f));
            Shape(lamp,"Individual fine taillamp optic ribs",Save("Rear taillamp prismatic ribs",lensRibs),Vector3.zero,graphite);
            Box(rear,"Rear reverse light "+side,new Vector3(side*.106f,.802f,.026f),new Vector3(.041f,.077f,.006f),lens,.008f);
        }
        Label(rear,"SRT rear deck emblem","SRT",new Vector3(0,.807f,.039f),Quaternion.identity,.038f,chrome,true);
        Box(rear,"Rear silver bumper main form",new Vector3(0,.548f,-.035f),new Vector3(1.980f,.277f,.135f),paint,.062f);
        Box(rear,"Rear bumper shadow separation",new Vector3(0,.398f,-.002f),new Vector3(1.88f,.017f,.040f),black,.007f);
        Box(rear,"Graphite rear diffuser lower valance",new Vector3(0,.293f,-.041f),new Vector3(1.925f,.189f,.183f),graphite,.050f);
        Box(rear,"Inset number plate black surround",new Vector3(0,.520f,.039f),new Vector3(.390f,.157f,.027f),graphite,.017f);
        Box(rear,"Brushed dark registration plate",new Vector3(0,.520f,.055f),new Vector3(.348f,.124f,.009f),black,.007f);
        Label(rear,"SRT rear license plate","SRT / 707",new Vector3(0,.520f,.062f),Quaternion.identity,.025f,chrome,false);
        for(int side=-1;side<=1;side+=2)
        {
            Transform exhaust=Group(rear,"Rectangular polished exhaust tip "+side);exhaust.localPosition=new Vector3(side*.743f,.281f,.052f);
            Shape(exhaust,"Rolled rectangular chrome exhaust outlet",Frame("Rectangular chrome exhaust tip",.252f,.112f,.024f,.016f,.052f),Vector3.zero,chrome);
            Shape(exhaust,"Visible dark exhaust bore",Panel("Black exhaust bore",.219f,.079f,.014f),new Vector3(0,0,-.044f),black);
            Box(rear,"Rear red corner reflector "+side,new Vector3(side*.910f,.451f,.017f),new Vector3(.081f,.033f,.011f),red,.011f);
            Box(rear,"Rear diffuser outer fin "+side,new Vector3(side*.439f,.228f,-.026f),new Vector3(.017f,.089f,.215f),black,.008f);
        }
        Box(rear,"Centre diffuser stabiliser",new Vector3(0,.222f,-.021f),new Vector3(.017f,.088f,.236f),black,.008f);
        // Deck lip is attached to the independently hinged lid by the caller where appropriate.
    }

    private static void SideTrim(Transform parent)
    {
        for(int side=-1;side<=1;side+=2)
        {
            Box(parent,"Amber front fender side marker "+side,new Vector3(side*1.006f,.529f,1.980f),new Vector3(.014f,.033f,.166f),amber,.006f);
            Box(parent,"Marker black rubber gasket "+side,new Vector3(side*1.000f,.527f,1.979f),new Vector3(.013f,.045f,.181f),black,.006f);
            Box(parent,"Rear quarter red side marker "+side,new Vector3(side*.983f,.568f,-2.161f),new Vector3(.014f,.027f,.137f),red,.005f);
            Box(parent,"Door handle recessed pocket "+side,new Vector3(side*.958f,.985f,-.660f),new Vector3(.012f,.047f,.153f),black,.005f);
            Box(parent,"Flush silver door release handle "+side,new Vector3(side*.970f,.992f,-.657f),new Vector3(.024f,.027f,.128f),paint,.012f);
            Box(parent,"Door handle key barrel "+side,new Vector3(side*.983f,.991f,-.699f),new Vector3(.003f,.010f,.011f),chrome,.001f);
            Transform mirror=Group(parent,"Sculpted body colour door mirror "+side);
            Tube(mirror,"Tapered mirror mounting arm "+side,new Vector3(side*.905f,1.077f,.758f),new Vector3(side*1.015f,1.143f,.697f),.022f,.028f,graphite);
            Shape(mirror,"Silver aerodynamic mirror shell",Ellipsoid("Mirror silver ellipsoid",new Vector3(.084f,.065f,.113f),32,16),new Vector3(side*1.005f,1.166f,.696f),paint);
            Shape(mirror,"Black mirror glass gasket",Ellipsoid("Mirror rear black seal",new Vector3(.076f,.050f,.013f),32,12),new Vector3(side*1.009f,1.165f,.602f),black);
            Shape(mirror,"Curved reflective mirror glass",Ellipsoid("Mirror reflective glass",new Vector3(.070f,.043f,.006f),32,12),new Vector3(side*1.009f,1.165f,.593f),lens);
            Quaternion face=Quaternion.Euler(0,side*90,0);
            CatBadge(parent,"Black Hellcat front fender emblem "+side,new Vector3(side*.964f,.888f,.969f),face,.62f,black,side>0);
            Box(parent,"Satin lower sill blade "+side,new Vector3(side*.944f,.240f,-.029f),new Vector3(.094f,.045f,2.105f),graphite,.021f);
        }
        Transform cap=Group(parent,"Driver side machined fuel filler cap");
        cap.localPosition=new Vector3(-.955f,1.004f,-1.536f);cap.localRotation=Quaternion.Euler(0,-90,0);
        Shape(cap,"Fuel cap mounting recess",Disc("Fuel cap black mounting ring",.078f,.007f,64),Vector3.zero,black);
        Shape(cap,"Fuel cap graphite brushed face",Disc("Fuel cap graphite face",.067f,.009f,64),new Vector3(0,0,.005f),graphite);
        Shape(cap,"Fuel cap silver retaining rim",Torus("Fuel cap silver retaining ring",.069f,.003f,64,8),new Vector3(0,0,.012f),chrome);
        Geo bolts=new Geo();
        for(int n=0;n<6;n++)
        {
            float a=n*Mathf.PI/3;bolts.Cylinder(new Vector3(Mathf.Cos(a)*.054f,Mathf.Sin(a)*.054f,.012f),.0030f,.002f,8);
        }
        Shape(cap,"Six recessed fuel cap bolts",Save("Fuel cap fasteners",bolts),Vector3.zero,chrome);
        Label(cap,"Fuel cap engraved word","FUEL",new Vector3(0,0,.0135f),Quaternion.identity,.016f,chrome,false);
    }

    private static void Cabin(Transform parent)
    {
        Box(parent,"Fitted charcoal cabin carpet",new Vector3(0,.445f,-.070f),new Vector3(1.75f,.085f,2.56f),rubber,.035f);
        Geo seams=new Geo();
        for(int side=-1;side<=1;side+=2)
        {
            Transform seat=Group(parent,"Bolstered Nappa leather front bucket seat "+side);seat.localPosition=new Vector3(side*.422f,0,-.200f);
            Box(seat,"Visible metal seat base",new Vector3(0,.535f,-.065f),new Vector3(.382f,.076f,.516f),graphite,.025f);
            Box(seat,"Perforated seat cushion",new Vector3(0,.637f,-.050f),new Vector3(.438f,.134f,.565f),leather,.058f);
            for(int edge=-1;edge<=1;edge+=2)
            {
                Transform bolster=Box(seat,"Raised cushion side bolster "+edge,new Vector3(edge*.198f,.682f,-.075f),new Vector3(.103f,.122f,.535f),leather,.045f);
                bolster.localRotation=Quaternion.Euler(0,edge*-3,edge*-9);
            }
            Transform back=Group(seat,"Angled sculpted seat back");back.localPosition=new Vector3(0,.714f,-.286f);back.localRotation=Quaternion.Euler(-13,0,0);
            Box(back,"Bucket back shell",new Vector3(0,.234f,0),new Vector3(.456f,.522f,.130f),leather,.060f);
            Box(back,"Recessed seat back centre pad",new Vector3(0,.232f,.070f),new Vector3(.285f,.398f,.037f),leather,.017f);
            for(int edge=-1;edge<=1;edge+=2)Box(back,"Raised torso side wing "+edge,new Vector3(edge*.181f,.229f,.068f),new Vector3(.112f,.460f,.118f),leather,.052f);
            for(int n=0;n<2;n++)Tube(back,"Headrest metal stem "+n,new Vector3((n==0?-.069f:.069f),.465f,0),new Vector3((n==0?-.069f:.069f),.546f,0),.009f,.009f,chrome);
            Box(back,"Rounded leather head restraint",new Vector3(0,.566f,0),new Vector3(.273f,.193f,.139f),leather,.060f);
            Label(back,"SRT embroidered headrest logo","SRT",new Vector3(0,.562f,.071f),Quaternion.identity,.028f,stitch,true);
            for(int n=0;n<8;n++)
                Box(back,"Perforated centre horizontal stitch "+n,new Vector3(0,.085f+n*.040f,.091f),new Vector3(.234f,.0015f,.002f),stitch,.0005f);
            for(int edge=-1;edge<=1;edge+=2)
            {
                for(int n=0;n<25;n++)
                {
                    float y=.045f+n*.016f;
                    Vector3 at=seat.localPosition + back.localPosition + back.localRotation*new Vector3(edge*.148f,y,.094f);
                    seams.Box(at,new Vector3(.0018f,.0065f,.0018f));
                }
                for(int n=0;n<29;n++)seams.Box(seat.localPosition+new Vector3(edge*.154f,.708f,-.279f+n*.016f),new Vector3(.0018f,.0015f,.006f));
            }
            Box(parent,"Inner door card "+side,new Vector3(side*.870f,.801f,-.060f),new Vector3(.070f,.366f,1.430f),rubber,.032f);
            Box(parent,"Padded door armrest "+side,new Vector3(side*.827f,.790f,-.179f),new Vector3(.106f,.074f,.611f),leather,.034f);
            Box(parent,"Brushed interior release handle "+side,new Vector3(side*.823f,.934f,.288f),new Vector3(.018f,.020f,.127f),chrome,.008f);
            Box(parent,"Seat belt buckle red release "+side,new Vector3(side*.161f,.686f,-.260f),new Vector3(.038f,.046f,.056f),red,.009f);
            Tube(parent,"Front seat shoulder belt "+side,new Vector3(side*.799f,1.145f,-.752f),new Vector3(side*.773f,.633f,-.436f),.013f,.013f,black);
        }
        Shape(parent,"Fine double stitched leather seams",Save("Nappa seat double stitching",seams),Vector3.zero,stitch);
        Box(parent,"Rear leather bench cushion",new Vector3(0,.660f,-.979f),new Vector3(1.421f,.123f,.413f),leather,.052f);
        Transform rearBack=Box(parent,"Rear leather bench backrest",new Vector3(0,.908f,-1.170f),new Vector3(1.448f,.433f,.125f),leather,.055f);rearBack.localRotation=Quaternion.Euler(-13,0,0);
        for(int side=-1;side<=1;side+=2)
            Box(parent,"Rear leather head restraint "+side,new Vector3(side*.451f,1.154f,-1.186f),new Vector3(.260f,.155f,.112f),leather,.048f);

        Box(parent,"Sculpted soft touch full width dashboard",new Vector3(0,1.013f,.749f),new Vector3(1.645f,.182f,.363f),rubber,.070f);
        Box(parent,"Lower dashboard shaped knee panel",new Vector3(0,.866f,.760f),new Vector3(1.553f,.170f,.227f),rubber,.048f);
        Box(parent,"Satin dashboard horizontal trim",new Vector3(0,1.016f,.561f),new Vector3(1.465f,.013f,.012f),graphite,.004f);
        Transform instruments=Group(parent,"Twin analog instrument binnacle");instruments.localPosition=new Vector3(-.425f,1.064f,.566f);instruments.localRotation=Quaternion.Euler(0,180,0);
        Box(instruments,"Curved instrument hood",new Vector3(0,.053f,-.011f),new Vector3(.420f,.137f,.177f),rubber,.052f);
        for(int side=-1;side<=1;side+=2)
        {
            Shape(instruments,"Recessed black analog dial "+side,Disc("Cabin black dial",.070f,.006f,48),new Vector3(side*.094f,0,.067f),black);
            Shape(instruments,"Machined analog gauge ring "+side,Torus("Cabin analog dial rim",.071f,.003f,48,8),new Vector3(side*.094f,0,.072f),chrome);
            Geo ticks=new Geo();
            for(int n=0;n<24;n++)
            {
                float a=(-225+n*270f/23)*Mathf.Deg2Rad;
                Vector3 p=new Vector3(side*.094f+Mathf.Cos(a)*.057f,Mathf.Sin(a)*.057f,.077f);
                ticks.Line(p,p+new Vector3(-Mathf.Cos(a),-Mathf.Sin(a),0)*.007f,.0012f,6);
            }
            Shape(instruments,"Blue white dial graduations "+side,Save("Cabin gauge ticks "+side,ticks),Vector3.zero,display);
            Tube(instruments,"Red analog dial needle "+side,new Vector3(side*.094f,0,.080f),new Vector3(side*.094f-.035f,-.029f,.080f),.0015f,.0007f,red);
        }
        Box(instruments,"Digital vehicle information readout",new Vector3(0,0,.078f),new Vector3(.028f,.049f,.003f),display,.002f);

        Transform wheel=Group(parent,"Leather flat bottom SRT steering wheel");wheel.localPosition=new Vector3(-.432f,1.020f,.386f);wheel.localRotation=Quaternion.Euler(-15,0,0);
        Shape(wheel,"Hand stitched leather steering rim",Torus("Leather steering wheel rim",.163f,.013f,80,10),Vector3.zero,leather);
        Shape(wheel,"Brushed inner steering rim accent",Torus("Steering wheel aluminium inner trim",.148f,.002f,80,8),new Vector3(0,0,-.001f),graphite);
        Box(wheel,"Steering wheel lower flat grip",new Vector3(0,-.147f,0),new Vector3(.155f,.025f,.025f),leather,.011f);
        for(int side=-1;side<=1;side+=2)
        {
            Tube(wheel,"Steering side spoke "+side,new Vector3(side*.035f,0,0),new Vector3(side*.146f,.008f,0),.018f,.012f,graphite);
            Box(wheel,"Steering spoke control buttons "+side,new Vector3(side*.099f,.012f,-.016f),new Vector3(.037f,.032f,.006f),black,.004f);
        }
        Tube(wheel,"Steering lower spoke",new Vector3(0,-.032f,0),new Vector3(0,-.144f,0),.022f,.012f,graphite);
        Box(wheel,"SRT leather steering wheel airbag",Vector3.zero,new Vector3(.123f,.089f,.054f),leather,.027f);
        Label(wheel,"Steering wheel silver SRT badge","SRT",new Vector3(0,0,-.029f),Quaternion.Euler(0,180,0),.023f,chrome,true);
        Tube(parent,"Steering column",new Vector3(-.432f,.996f,.429f),new Vector3(-.432f,.866f,.691f),.029f,.043f,black);
        Tube(parent,"Indicator stalk",new Vector3(-.455f,1.005f,.475f),new Vector3(-.615f,1.015f,.443f),.008f,.010f,black);

        Box(parent,"Angled centre console tunnel",new Vector3(0,.676f,-.043f),new Vector3(.264f,.248f,.981f),rubber,.055f);
        Box(parent,"Padded centre armrest",new Vector3(0,.809f,-.382f),new Vector3(.242f,.079f,.353f),leather,.034f);
        Box(parent,"Centre touchscreen silver bezel",new Vector3(.130f,1.005f,.551f),new Vector3(.231f,.142f,.016f),graphite,.014f);
        Box(parent,"Dark glossy infotainment screen",new Vector3(.130f,1.005f,.540f),new Vector3(.205f,.115f,.006f),black,.005f);
        Box(parent,"Infotainment soft blue status line",new Vector3(.130f,.974f,.535f),new Vector3(.155f,.003f,.002f),display,.001f);
        for(int side=-1;side<=1;side+=2)
        {
            Box(parent,"Outer dashboard air vent "+side,new Vector3(side*.720f,1.021f,.584f),new Vector3(.103f,.080f,.018f),black,.012f);
            Geo vanes=new Geo();for(int n=0;n<5;n++)vanes.Box(new Vector3(0,-.028f+n*.014f,0),new Vector3(.081f,.003f,.005f));
            Shape(parent,"Dashboard vent five metal vanes "+side,Save("Cabin air vent vanes",vanes),new Vector3(side*.720f,1.021f,.573f),graphite);
        }
        Box(parent,"Satin gear selector surround",new Vector3(0,.810f,.105f),new Vector3(.161f,.014f,.240f),graphite,.020f);
        Box(parent,"Gear selector leather boot",new Vector3(0,.828f,.126f),new Vector3(.090f,.038f,.107f),leather,.017f);
        Tube(parent,"Short automatic selector stem",new Vector3(0,.837f,.125f),new Vector3(0,.913f,.111f),.012f,.011f,chrome);
        Box(parent,"Leather T handle gear selector",new Vector3(0,.917f,.110f),new Vector3(.096f,.043f,.054f),leather,.020f);
        for(int n=0;n<2;n++)
        {
            Transform cup=Shape(parent,"Cup holder rolled rim "+n,Torus("Console cup holder rim",.042f,.004f,40,8),new Vector3(0,.810f,-.075f-n*.108f),graphite);cup.localRotation=Quaternion.Euler(90,0,0);
            Transform inset=Shape(parent,"Deep black cup holder "+n,Disc("Console cup holder bottom",.039f,.005f,40),new Vector3(0,.805f,-.075f-n*.108f),black);inset.localRotation=Quaternion.Euler(90,0,0);
        }
        Box(parent,"Interior rear view mirror",new Vector3(0,1.355f,.561f),new Vector3(.263f,.079f,.028f),black,.015f);
        Box(parent,"Interior rear view mirror reflective face",new Vector3(0,1.355f,.544f),new Vector3(.239f,.059f,.005f),lens,.012f);
        Tube(parent,"Interior mirror stem",new Vector3(0,1.393f,.585f),new Vector3(0,1.374f,.573f),.010f,.010f,black);
    }

    private static void CatBadge(Transform parent,string name,Vector3 position,Quaternion rotation,float scale,Material material,bool mirror=false)
    {
        Transform badge=Group(parent,name);badge.localPosition=position;badge.localRotation=rotation;badge.localScale=new Vector3(mirror?-scale:scale,scale,scale);
        Vector2[] p={new Vector2(-.072f,-.036f),new Vector2(-.076f,.018f),new Vector2(-.054f,.079f),new Vector2(-.029f,.053f),new Vector2(-.002f,.073f),new Vector2(.023f,.045f),new Vector2(.016f,.028f),new Vector2(.058f,.022f),new Vector2(.079f,.002f),new Vector2(.054f,-.012f),new Vector2(.073f,-.028f),new Vector2(.038f,-.052f),new Vector2(.006f,-.062f),new Vector2(-.020f,-.045f),new Vector2(-.034f,-.071f),new Vector2(-.052f,-.052f)};
        Geo cat=new Geo();cat.Polygon(p,.0035f);Shape(badge,"Raised snarling cat silhouette",Save("Hellcat sculpted cat silhouette",cat),Vector3.zero,material);
        Geo cuts=new Geo();
        cuts.Line(new Vector3(-.021f,.023f,.004f),new Vector3(.013f,.014f,.004f),.0035f,6);
        cuts.Line(new Vector3(.023f,-.009f,.004f),new Vector3(.054f,-.017f,.004f),.0030f,6);
        cuts.Line(new Vector3(-.031f,-.017f,.004f),new Vector3(.004f,-.031f,.004f),.0025f,6);
        Shape(badge,"Engraved eye and snarling jaw cuts",Save("Hellcat badge engraved cuts",cuts),Vector3.zero,material==black?chrome:black);
    }

    private static Material Mat(string name,Color color,float metallic,float smoothness,Color? emission=null)
    {
        string path=Root+"/Materials/"+name+".mat";Material m=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(m==null){m=new Material(Shader.Find("Universal Render Pipeline/Lit")){name=name};AssetDatabase.CreateAsset(m,path);}
        m.SetColor("_BaseColor",color);m.SetFloat("_Metallic",metallic);m.SetFloat("_Smoothness",smoothness);m.enableInstancing=true;
        if(emission.HasValue){m.EnableKeyword("_EMISSION");m.SetColor("_EmissionColor",emission.Value);}
        else{m.DisableKeyword("_EMISSION");m.SetColor("_EmissionColor",Color.black);}
        m.globalIlluminationFlags=emission.HasValue?MaterialGlobalIlluminationFlags.RealtimeEmissive:MaterialGlobalIlluminationFlags.None;EditorUtility.SetDirty(m);return m;
    }

    public static Material UpdateLetteringMaterial()
    {
        Ensure(Root+"/Materials");
        worldText=WorldTextMaterial(Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"));
        return worldText;
    }

    private static Material WorldTextMaterial(Font font)
    {
        const string path=Root+"/Materials/Hellcat depth tested physical lettering.mat";
        Material material=AssetDatabase.LoadAssetAtPath<Material>(path);
        Shader shader=Shader.Find("Facility/Vehicle Lettering")??Shader.Find("Facility/Sign Text");
        if(shader==null)throw new InvalidOperationException("A depth tested vehicle lettering shader is required for Hellcat lettering.");
        if(material==null)
        {
            material=new Material(shader){name="Hellcat depth tested physical lettering"};AssetDatabase.CreateAsset(material,path);
        }
        material.shader=shader;material.DisableKeyword("_EMISSION");material.DisableKeyword("_RECEIVE_SHADOWS_OFF");material.globalIlluminationFlags=MaterialGlobalIlluminationFlags.EmissiveIsBlack;
        material.SetColor("_BaseColor",Color.white);material.SetTexture("_MainTex",font.material.mainTexture);EditorUtility.SetDirty(material);return material;
    }

    private static void LeatherGrain(Material material)
    {
        Ensure(Root+"/Textures");string path=Root+"/Textures/Nappa fine pebble normal.png";
        Texture2D map=AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if(map==null)
        {
            const int size=256;Texture2D generated=new Texture2D(size,size,TextureFormat.RGB24,false,true);
            Color[] pixels=new Color[size*size];
            for(int y=0;y<size;y++)for(int x=0;x<size;x++)
            {
                float a=LeatherHeight(x+1,y)-LeatherHeight(x-1,y),b=LeatherHeight(x,y+1)-LeatherHeight(x,y-1);
                Vector3 n=new Vector3(-a*.65f,-b*.65f,1).normalized;pixels[y*size+x]=new Color(n.x*.5f+.5f,n.y*.5f+.5f,n.z*.5f+.5f);
            }
            generated.SetPixels(pixels);generated.Apply();File.WriteAllBytes(path,generated.EncodeToPNG());Object.DestroyImmediate(generated);
            AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
            TextureImporter importer=(TextureImporter)AssetImporter.GetAtPath(path);importer.textureType=TextureImporterType.NormalMap;importer.sRGBTexture=false;importer.filterMode=FilterMode.Trilinear;importer.wrapMode=TextureWrapMode.Repeat;importer.mipmapEnabled=true;importer.textureCompression=TextureImporterCompression.CompressedHQ;importer.SaveAndReimport();
            map=AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
        material.SetTexture("_BumpMap",map);material.SetFloat("_BumpScale",.24f);material.EnableKeyword("_NORMALMAP");material.SetTextureScale("_BaseMap",new Vector2(3,3));EditorUtility.SetDirty(material);
    }
    private static float LeatherHeight(int x,int y)
    {
        float px=(x+256)%256,py=(y+256)%256;
        return Mathf.PerlinNoise(px*.45f,py*.45f)*.65f+Mathf.PerlinNoise(px*.91f+37,py*.91f+81)*.35f;
    }

    private static Transform Group(Transform parent,string name){GameObject o=new GameObject(name);o.transform.SetParent(parent,false);return o.transform;}
    private static Transform Shape(Transform parent,string name,Mesh mesh,Vector3 position,Material material)
    {
        GameObject o=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));o.transform.SetParent(parent,false);o.transform.localPosition=position;
        o.GetComponent<MeshFilter>().sharedMesh=mesh;MeshRenderer r=o.GetComponent<MeshRenderer>();r.sharedMaterial=material;
        r.shadowCastingMode=ShadowCastingMode.On;r.receiveShadows=true;r.lightProbeUsage=LightProbeUsage.BlendProbes;r.reflectionProbeUsage=ReflectionProbeUsage.BlendProbes;
        return o.transform;
    }
    private static Transform Box(Transform parent,string name,Vector3 pos,Vector3 size,Material mat,float bevel)
    {return Shape(parent,name,RoundedBox(size,bevel),pos,mat);}
    private static void Tube(Transform parent,string name,Vector3 a,Vector3 b,float r0,float r1,Material mat)
    {
        Geo g=new Geo();g.Tube(a,b,r0,r1,12);Shape(parent,name,Save(name+" geometry",g),Vector3.zero,mat);
    }
    private static void Label(Transform parent,string name,string value,Vector3 pos,Quaternion rotation,float height,Material material,bool italic)
    {
        GameObject o=new GameObject(name,typeof(TextMesh));o.transform.SetParent(parent,false);o.transform.localPosition=pos;o.transform.localRotation=rotation*Quaternion.Euler(0,180,0);
        TextMesh text=o.GetComponent<TextMesh>();text.text=value;text.anchor=TextAnchor.MiddleCenter;text.alignment=TextAlignment.Center;text.fontSize=100;text.characterSize=height*.145f;text.fontStyle=italic?FontStyle.BoldAndItalic:FontStyle.Bold;
        text.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");text.color=material.GetColor("_BaseColor");
        text.font.RequestCharactersInTexture(value,text.fontSize,text.fontStyle);worldText.SetTexture("_MainTex",text.font.material.mainTexture);
        MeshRenderer renderer=o.GetComponent<MeshRenderer>();renderer.sharedMaterial=worldText;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
        o.AddComponent<FacilityWorldText>();
    }
    private static Mesh Save(string name,Geo g)
    {
        Mesh created=g.Mesh(name);string safe=name.Replace('/','-').Replace('\\','-');string path=Root+"/Meshes/"+safe+".asset";
        Mesh saved=AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if(saved==null){AssetDatabase.CreateAsset(created,path);return created;}
        EditorUtility.CopySerialized(created,saved);Object.DestroyImmediate(created);EditorUtility.SetDirty(saved);return saved;
    }
    private static Mesh RoundedBox(Vector3 size,float bevel)
    {
        string name="Detail rounded box "+size.x.ToString("0.0000",System.Globalization.CultureInfo.InvariantCulture)+" "+size.y.ToString("0.0000",System.Globalization.CultureInfo.InvariantCulture)+" "+size.z.ToString("0.0000",System.Globalization.CultureInfo.InvariantCulture)+" "+bevel.ToString("0.0000",System.Globalization.CultureInfo.InvariantCulture);
        Mesh known=AssetDatabase.LoadAssetAtPath<Mesh>(Root+"/Meshes/"+name+".asset");if(known!=null)return known;
        Geo g=new Geo();Vector3 half=size*.5f;float r=Mathf.Min(bevel,Mathf.Min(half.x,Mathf.Min(half.y,half.z))*.99f);Vector3 inset=half-Vector3.one*r;
        int steps=6;
        for(int face=0;face<6;face++)
        {
            Vector3 normal=face==0?Vector3.right:face==1?Vector3.left:face==2?Vector3.up:face==3?Vector3.down:face==4?Vector3.forward:Vector3.back;
            Vector3 axisU=face<2?Vector3.forward:Vector3.right;Vector3 axisV=Vector3.Cross(normal,axisU);
            for(int y=0;y<steps;y++)for(int x=0;x<steps;x++)
            {
                Vector3[] p=new Vector3[4];Vector3[] normals=new Vector3[4];
                for(int q=0;q<4;q++)
                {
                    float u=((x+(q==1||q==2?1:0))/(float)steps)*2-1,v=((y+(q>=2?1:0))/(float)steps)*2-1;
                    Vector3 raw=Vector3.Scale(normal+axisU*u+axisV*v,half);
                    Vector3 c=new Vector3(Mathf.Clamp(raw.x,-inset.x,inset.x),Mathf.Clamp(raw.y,-inset.y,inset.y),Mathf.Clamp(raw.z,-inset.z,inset.z));
                    normals[q]=(raw-c).normalized;p[q]=c+normals[q]*r;
                }
                g.Quad(p[0],p[1],p[2],p[3],normals[0],normals[1],normals[2],normals[3]);
            }
        }
        return Save(name,g);
    }
    private static Mesh Panel(string name,float width,float height,float radius)
    {
        Geo g=new Geo();Vector2[] outline=RoundOutline(width,height,radius);for(int i=0;i<outline.Length;i++)g.Triangle(Vector3.zero,outline[i],outline[(i+1)%outline.Length],Vector3.forward);
        return Save(name,g);
    }
    private static Mesh Frame(string name,float width,float height,float radius,float thickness,float depth)
    {
        Vector2[] outer=RoundOutline(width,height,radius),inner=RoundOutline(width-thickness*2,height-thickness*2,Mathf.Max(.002f,radius-thickness));Geo g=new Geo();
        for(int i=0;i<outer.Length;i++)
        {
            int j=(i+1)%outer.Length;Vector3 a=outer[i],b=outer[j],c=inner[j],d=inner[i];
            g.Quad(a,b,c,d,Vector3.forward);g.Quad(a-Vector3.forward*depth,b-Vector3.forward*depth,b,a);g.Quad(d,c,c-Vector3.forward*depth,d-Vector3.forward*depth);
        }
        return Save(name,g);
    }
    private static Vector2[] RoundOutline(float width,float height,float radius)
    {
        radius=Mathf.Min(radius,Mathf.Min(width,height)*.49f);List<Vector2> p=new List<Vector2>();
        for(int corner=0;corner<4;corner++)
        {
            float cx=corner==0||corner==3?width*.5f-radius:-width*.5f+radius,cy=corner<2?height*.5f-radius:-height*.5f+radius;
            for(int n=0;n<=12;n++){float a=(corner*90+n*90f/12)*Mathf.Deg2Rad;p.Add(new Vector2(cx+Mathf.Cos(a)*radius,cy+Mathf.Sin(a)*radius));}
        }
        return p.ToArray();
    }
    private static Mesh Torus(string name,float radius,float tube,int sides,int rings)
    {
        Geo g=new Geo();
        for(int s=0;s<sides;s++)for(int r=0;r<rings;r++)
        {
            Vector3[] p=new Vector3[4],n=new Vector3[4];
            for(int q=0;q<4;q++)
            {
                float a=(s+(q==1||q==2?1:0))*Mathf.PI*2/sides,b=(r+(q>=2?1:0))*Mathf.PI*2/rings;
                Vector3 radial=new Vector3(Mathf.Cos(a),Mathf.Sin(a),0);n[q]=radial*Mathf.Cos(b)+Vector3.forward*Mathf.Sin(b);p[q]=radial*radius+n[q]*tube;
            }
            g.Quad(p[0],p[1],p[2],p[3],n[0],n[1],n[2],n[3]);
        }
        return Save(name,g);
    }
    private static Mesh Disc(string name,float radius,float depth,int sides){Geo g=new Geo();g.Cylinder(Vector3.zero,radius,depth,sides);return Save(name,g);}
    private static Mesh Ellipsoid(string name,Vector3 radii,int slices,int stacks)
    {
        Geo g=new Geo();
        for(int y=0;y<stacks;y++)for(int x=0;x<slices;x++)
        {
            Vector3[] p=new Vector3[4],n=new Vector3[4];
            for(int q=0;q<4;q++)
            {
                float a=(x+(q==1||q==2?1:0))*Mathf.PI*2/slices,b=-Mathf.PI*.5f+(y+(q>=2?1:0))*Mathf.PI/stacks;
                Vector3 unit=new Vector3(Mathf.Cos(b)*Mathf.Cos(a),Mathf.Sin(b),Mathf.Cos(b)*Mathf.Sin(a));p[q]=Vector3.Scale(unit,radii);n[q]=new Vector3(unit.x/radii.x,unit.y/radii.y,unit.z/radii.z).normalized;
            }
            g.Quad(p[0],p[1],p[2],p[3],n[0],n[1],n[2],n[3]);
        }
        return Save(name,g);
    }
    private static void Ensure(string path)
    {
        if(AssetDatabase.IsValidFolder(path))return;string parent=Path.GetDirectoryName(path).Replace('\\','/');Ensure(parent);AssetDatabase.CreateFolder(parent,Path.GetFileName(path));
    }

    private sealed class Geo
    {
        private readonly List<Vector3> v=new List<Vector3>(),normals=new List<Vector3>();
        private readonly List<Vector2> uv=new List<Vector2>();private readonly List<int> tris=new List<int>();
        public void Quad(Vector3 a,Vector3 b,Vector3 c,Vector3 d){Vector3 n=Vector3.Cross(b-a,c-a).normalized;Quad(a,b,c,d,n);}
        public void Quad(Vector3 a,Vector3 b,Vector3 c,Vector3 d,Vector3 n){Quad(a,b,c,d,n,n,n,n);}
        public void Quad(Vector3 a,Vector3 b,Vector3 c,Vector3 d,Vector3 na,Vector3 nb,Vector3 nc,Vector3 nd)
        {
            int at=v.Count;v.AddRange(new[]{a,b,c,d});normals.AddRange(new[]{na,nb,nc,nd});uv.AddRange(new[]{new Vector2(0,0),new Vector2(1,0),new Vector2(1,1),new Vector2(0,1)});
            Vector3 actual=Vector3.Cross(b-a,c-a);bool forward=Vector3.Dot(actual,na+nb+nc+nd)>=0;
            if(forward)tris.AddRange(new[]{at,at+1,at+2,at,at+2,at+3});else tris.AddRange(new[]{at,at+2,at+1,at,at+3,at+2});
        }
        public void Triangle(Vector3 a,Vector3 b,Vector3 c,Vector3 n)
        {
            int at=v.Count;v.AddRange(new[]{a,b,c});normals.AddRange(new[]{n,n,n});uv.AddRange(new[]{Vector2.zero,Vector2.right,Vector2.up});
            if(Vector3.Dot(Vector3.Cross(b-a,c-a),n)>=0)tris.AddRange(new[]{at,at+1,at+2});else tris.AddRange(new[]{at,at+2,at+1});
        }
        public void Box(Vector3 c,Vector3 size)
        {
            Vector3 h=size*.5f;Vector3 a=c+new Vector3(-h.x,-h.y,-h.z),b=c+new Vector3(h.x,-h.y,-h.z),cc=c+new Vector3(h.x,h.y,-h.z),d=c+new Vector3(-h.x,h.y,-h.z);
            Vector3 e=c+new Vector3(-h.x,-h.y,h.z),f=c+new Vector3(h.x,-h.y,h.z),gg=c+new Vector3(h.x,h.y,h.z),hh=c+new Vector3(-h.x,h.y,h.z);
            Quad(a,d,cc,b,Vector3.back);Quad(e,f,gg,hh,Vector3.forward);Quad(a,e,hh,d,Vector3.left);Quad(b,cc,gg,f,Vector3.right);Quad(d,hh,gg,cc,Vector3.up);Quad(a,b,f,e,Vector3.down);
        }
        public void Cylinder(Vector3 center,float radius,float depth,int count)
        {
            for(int i=0;i<count;i++)
            {
                float a=i*Mathf.PI*2/count,b=(i+1)*Mathf.PI*2/count;Vector3 na=new Vector3(Mathf.Cos(a),Mathf.Sin(a),0),nb=new Vector3(Mathf.Cos(b),Mathf.Sin(b),0);
                Vector3 va=center+na*radius,vb=center+nb*radius;Quad(va,vb,vb-Vector3.forward*depth,va-Vector3.forward*depth,na,nb,nb,na);
                Triangle(center,va,vb,Vector3.forward);Triangle(center-Vector3.forward*depth,vb-Vector3.forward*depth,va-Vector3.forward*depth,Vector3.back);
            }
        }
        public void Hex(Vector3 center,float radius,float thickness,float depth)
        {
            for(int i=0;i<6;i++)
            {
                float a=(i*60+30)*Mathf.Deg2Rad,b=((i+1)*60+30)*Mathf.Deg2Rad;Vector3 na=new Vector3(Mathf.Cos(a),Mathf.Sin(a),0),nb=new Vector3(Mathf.Cos(b),Mathf.Sin(b),0);
                Vector3 va=center+na*radius,vb=center+nb*radius,ia=center+na*(radius-thickness),ib=center+nb*(radius-thickness);
                Quad(va,vb,ib,ia,Vector3.forward);Quad(ia,ib,ib-Vector3.forward*depth,ia-Vector3.forward*depth);
            }
        }
        public void Line(Vector3 a,Vector3 b,float radius,int sides){Tube(a,b,radius,radius,sides);}
        public void Tube(Vector3 a,Vector3 b,float r0,float r1,int sides)
        {
            Vector3 axis=(b-a).normalized,u=Vector3.Cross(axis,Mathf.Abs(axis.y)>.95f?Vector3.right:Vector3.up).normalized,w=Vector3.Cross(axis,u).normalized;
            for(int i=0;i<sides;i++)
            {
                float p=i*Mathf.PI*2/sides,q=(i+1)*Mathf.PI*2/sides;Vector3 n0=u*Mathf.Cos(p)+w*Mathf.Sin(p),n1=u*Mathf.Cos(q)+w*Mathf.Sin(q);
                Quad(a+n0*r0,a+n1*r0,b+n1*r1,b+n0*r1,n0,n1,n1,n0);Triangle(a,a+n1*r0,a+n0*r0,-axis);Triangle(b,b+n0*r1,b+n1*r1,axis);
            }
        }
        public void Polygon(Vector2[] points,float depth)
        {
            List<int> remaining=new List<int>();float area=0;for(int i=0;i<points.Length;i++){remaining.Add(i);Vector2 b=points[(i+1)%points.Length];area+=points[i].x*b.y-b.x*points[i].y;}
            if(area<0)remaining.Reverse();int guard=0;
            while(remaining.Count>2&&guard++<points.Length*points.Length)
            {
                bool found=false;
                for(int i=0;i<remaining.Count;i++)
                {
                    int ia=remaining[(i+remaining.Count-1)%remaining.Count],ib=remaining[i],ic=remaining[(i+1)%remaining.Count];Vector2 a=points[ia],b=points[ib],c=points[ic];
                    if(Cross(b-a,c-b)<=0)continue;bool inside=false;
                    foreach(int test in remaining){if(test==ia||test==ib||test==ic)continue;Vector2 p=points[test];if(Cross(b-a,p-a)>=0&&Cross(c-b,p-b)>=0&&Cross(a-c,p-c)>=0){inside=true;break;}}
                    if(inside)continue;Triangle(a,b,c,Vector3.forward);Triangle((Vector3)c-Vector3.forward*depth,(Vector3)b-Vector3.forward*depth,(Vector3)a-Vector3.forward*depth,Vector3.back);remaining.RemoveAt(i);found=true;break;
                }
                if(!found)break;
            }
            for(int i=0;i<points.Length;i++){Vector3 a=points[i],b=points[(i+1)%points.Length];Quad(a,b,b-Vector3.forward*depth,a-Vector3.forward*depth);}
        }
        private static float Cross(Vector2 a,Vector2 b){return a.x*b.y-a.y*b.x;}
        public void UseHorizontalStripeUV(){for(int i=0;i<v.Count;i++)uv[i]=new Vector2((v[i].x+1.05f)/2.1f,.5f);}
        public Mesh Mesh(string name)
        {
            Mesh m=new Mesh{name=name,indexFormat=v.Count>65535?IndexFormat.UInt32:IndexFormat.UInt16};m.SetVertices(v);m.SetNormals(normals);m.SetUVs(0,uv);m.SetTriangles(tris,0);m.RecalculateBounds();return m;
        }
    }
}
