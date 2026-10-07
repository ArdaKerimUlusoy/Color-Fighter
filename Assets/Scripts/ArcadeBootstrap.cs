using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

public class ArcadeBootstrap : MonoBehaviour
{
    #region Ayarlar

    [Header("Shaderlar (boşsa otomatik bulunur)")]
    public Shader litShader;
    public Shader unlitShader;
    public Shader crtShader;

    [Header("Atari ekranı çözünürlüğü")]
    public int screenWidth = 320;
    public int screenHeight = 240;

    [Header("Oyuncular")]
    public Color p1Color = new Color(0.9f, 0.12f, 0.12f);
    public Color p2Color = new Color(0.15f, 0.35f, 1f);

    [SerializeField, HideInInspector] bool built;

    public bool IsBuilt => built && GetComponent<MatchManager>() != null;

    const string GeneratedFolder = "Assets/ColorFighter_Generated";
    const float StartX = 1.6f;

    readonly Dictionary<string, Material> matCache = new Dictionary<string, Material>();
    Fighter f1, f2;

    #endregion

    #region Unity olayları

    void Reset() { FindShaders(); }
    void OnValidate() { FindShaders(); }

    void Awake()
    {
        Time.fixedDeltaTime = 1f / 60f;
        ApplyRenderSettings();
        if (!IsBuilt)
        {
            FindShaders();
            Build();
        }
    }

    void FindShaders()
    {
        bool srp = GraphicsSettings.currentRenderPipeline != null;
        if (litShader == null) litShader = srp ? FirstShader("Universal Render Pipeline/Lit", "Standard") : Shader.Find("Standard");
        if (unlitShader == null) unlitShader = srp ? FirstShader("Universal Render Pipeline/Unlit", "Unlit/Color") : Shader.Find("Unlit/Color");
        if (crtShader == null) crtShader = Shader.Find("Arcade/CRTScreen");
    }

    static Shader FirstShader(params string[] names)
    {
        foreach (var n in names)
        {
            var s = Shader.Find(n);
            if (s != null) return s;
        }
        return null;
    }

    #endregion

    #region Kurulum

    void Build()
    {
        matCache.Clear();

        var arena = Group("FightArena", new Vector3(1000f, 0f, 0f));
        BuildArena(arena);

        var rt = ScreenTexture();

        var fightCam = new GameObject("FightCamera").AddComponent<Camera>();
        fightCam.transform.SetParent(arena, false);
        fightCam.transform.localPosition = MatchManager.CameraPosFor(-StartX, StartX, 0f);
        fightCam.transform.localRotation = MatchManager.CameraRotation;
        fightCam.targetTexture = rt;
        fightCam.fieldOfView = 35f;
        fightCam.nearClipPlane = 0.1f;
        fightCam.farClipPlane = 40f;
        fightCam.clearFlags = CameraClearFlags.SolidColor;
        fightCam.backgroundColor = new Color(0.08f, 0.03f, 0.12f);

        var fx = Group("FightFX", Vector3.zero).gameObject.AddComponent<FightFX>();
        fx.unlitShader = unlitShader;

        f1 = MakeFighter("RED", p1Color, new Color(0.96f, 0.78f, 0.62f), arena, true);
        f2 = MakeFighter("BLUE", p2Color, new Color(0.72f, 0.52f, 0.38f), arena, false);
        f1.opponent = f2;
        f2.opponent = f1;
        f1.ResetForRound(-StartX);
        f2.ResetForRound(StartX);
        f1.GetComponent<FighterRig>().SetFacing(1);
        f2.GetComponent<FighterRig>().SetFacing(-1);

        var hud = Group("FightHUD", Vector3.zero).gameObject.AddComponent<FightHUD>();
        hud.Build(fightCam, f1.fighterName, f2.fighterName, p1Color, p2Color, 2);

        var mm = gameObject.AddComponent<MatchManager>();
        mm.p1 = f1; mm.p2 = f2;
        mm.fightCam = fightCam;
        mm.hud = hud;
        mm.p1Color = p1Color; mm.p2Color = p2Color;
        mm.roundsToWin = 2;
        mm.startDistance = StartX * 2f;

        BuildCabinet(rt);
        BuildRoom();
        SetupMainCamera();
        ApplyRenderSettings();

        built = true;
    }

    void ClearBuilt()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
            ColorFighterUtil.Kill(transform.GetChild(i).gameObject);
        var mm = GetComponent<MatchManager>();
        if (mm != null) ColorFighterUtil.Kill(mm);
        built = false;
    }

    Fighter MakeFighter(string name, Color main, Color skin, Transform arena, bool isP1)
    {
        var go = new GameObject(name + " Fighter");
        go.transform.SetParent(arena, false);

        var input = go.AddComponent<FighterInput>();
        if (isP1)
        {
            input.left = Key.A; input.right = Key.D; input.up = Key.W; input.down = Key.S;
            input.punch = Key.F; input.kick = Key.G;
            input.gamepadIndex = 0;
        }
        else
        {
            input.left = Key.LeftArrow; input.right = Key.RightArrow; input.up = Key.UpArrow; input.down = Key.DownArrow;
            input.punch = Key.K; input.kick = Key.L;
            input.gamepadIndex = 1;
        }
        input.pause = Key.Escape;

        var f = go.AddComponent<Fighter>();
        f.fighterName = name;
        f.input = input;

        var rig = go.AddComponent<FighterRig>();
        rig.fighter = f;
        rig.Build(main, skin, c => Mat(c, true));
        return f;
    }

    #endregion

    #region Arena

    void BuildArena(Transform arena)
    {
        var floorMat = Asset("Floor.mat", () => new Material(litShader));
        floorMat.shader = litShader;
        floorMat.color = Color.white;
        floorMat.mainTexture = Asset("FloorChecker.asset", () => Checker(new Color(0.22f, 0.1f, 0.28f), new Color(0.34f, 0.17f, 0.4f)));
        floorMat.mainTextureScale = new Vector2(12f, 5f);
        MarkDirty(floorMat);
        Prim(PrimitiveType.Cube, arena, "Floor", new Vector3(0f, -0.1f, 2f), new Vector3(24f, 0.2f, 10f), floorMat);

        Prim(PrimitiveType.Cube, arena, "BackWall", new Vector3(0f, 4f, 7f), new Vector3(30f, 8f, 0.5f), Mat(new Color(0.1f, 0.05f, 0.16f), true));
        Prim(PrimitiveType.Sphere, arena, "Sun", new Vector3(0f, 3.2f, 6.7f), new Vector3(3.2f, 3.2f, 0.1f), Mat(new Color(1f, 0.45f, 0.2f), false));

        Prim(PrimitiveType.Cube, arena, "NeonRed", new Vector3(-6.5f, 1.4f, 6.7f), new Vector3(11f, 0.1f, 0.1f), Mat(p1Color, false));
        Prim(PrimitiveType.Cube, arena, "NeonBlue", new Vector3(6.5f, 1.4f, 6.7f), new Vector3(11f, 0.1f, 0.1f), Mat(p2Color, false));

        var pillarMat = Mat(new Color(0.16f, 0.08f, 0.22f), true);
        foreach (float x in new[] { -8f, -5f, 5f, 8f })
        {
            Prim(PrimitiveType.Cube, arena, "Pillar", new Vector3(x, 2.5f, 5f), new Vector3(0.6f, 5f, 0.6f), pillarMat);
            Prim(PrimitiveType.Cube, arena, "PillarNeon", new Vector3(x, 4.4f, 4.68f), new Vector3(0.5f, 0.12f, 0.05f), Mat(x < 0 ? p1Color : p2Color, false));
        }

        var sun = FindSun();
        if (sun == null)
        {
            sun = Group("Directional Light", Vector3.zero).gameObject.AddComponent<Light>();
            sun.type = LightType.Directional;
        }
        RenderSettings.sun = sun;
        sun.transform.rotation = Quaternion.Euler(45f, -30f, 0f);
        sun.intensity = 1f;
        sun.color = new Color(1f, 0.93f, 0.85f);
    }

    static Light FindSun()
    {
        if (RenderSettings.sun != null) return RenderSettings.sun;
        var go = GameObject.Find("Directional Light");
        return go != null ? go.GetComponent<Light>() : null;
    }

    #endregion

    #region Atari kabini

    void BuildCabinet(RenderTexture rt)
    {
        var cab = Group("ArcadeCabinet", Vector3.zero);
        var bodyMat = Mat(new Color(0.07f, 0.07f, 0.09f), true);
        var black = Mat(new Color(0.02f, 0.02f, 0.02f), true);

        Prim(PrimitiveType.Cube, cab, "Lower", new Vector3(0f, 0.5f, 0f), new Vector3(1f, 1f, 0.8f), bodyMat);
        Prim(PrimitiveType.Cube, cab, "Upper", new Vector3(0f, 1.55f, 0.05f), new Vector3(1f, 1.1f, 0.7f), bodyMat);
        Prim(PrimitiveType.Cube, cab, "Top", new Vector3(0f, 2.22f, 0f), new Vector3(1f, 0.24f, 0.8f), bodyMat);
        Prim(PrimitiveType.Cube, cab, "SideL", new Vector3(-0.52f, 1.17f, -0.02f), new Vector3(0.04f, 2.34f, 0.86f), Mat(Dim(p1Color, 0.8f), true));
        Prim(PrimitiveType.Cube, cab, "SideR", new Vector3(0.52f, 1.17f, -0.02f), new Vector3(0.04f, 2.34f, 0.86f), Mat(Dim(p2Color, 0.8f), true));

        Prim(PrimitiveType.Cube, cab, "MarqueeRed", new Vector3(-0.245f, 2.22f, -0.405f), new Vector3(0.48f, 0.18f, 0.02f), Mat(p1Color, false));
        Prim(PrimitiveType.Cube, cab, "MarqueeBlue", new Vector3(0.245f, 2.22f, -0.405f), new Vector3(0.48f, 0.18f, 0.02f), Mat(p2Color, false));
        BuildMarqueeText(cab);

        Prim(PrimitiveType.Cube, cab, "Bezel", new Vector3(0f, 1.52f, -0.31f), new Vector3(0.86f, 0.7f, 0.02f), black);
        var screenShader = crtShader != null ? crtShader : unlitShader;
        if (crtShader == null) Debug.LogWarning("CRT shader bulunamadı, düz ekran kullanılıyor.");
        var screenMat = Asset("CRTScreen.mat", () => new Material(screenShader));
        screenMat.shader = screenShader;
        screenMat.mainTexture = rt;
        MarkDirty(screenMat);
        Prim(PrimitiveType.Quad, cab, "Screen", new Vector3(0f, 1.52f, -0.322f), new Vector3(0.76f, 0.57f, 1f), screenMat);

        var glow = Group("ScreenGlow", new Vector3(0f, 1.5f, -0.7f), cab).gameObject.AddComponent<Light>();
        glow.type = LightType.Point;
        glow.color = new Color(0.6f, 0.6f, 1f);
        glow.intensity = 0.8f;
        glow.range = 1.6f;

        Prim(PrimitiveType.Cube, cab, "CoinDoor", new Vector3(0f, 0.55f, -0.405f), new Vector3(0.34f, 0.3f, 0.02f), Mat(new Color(0.12f, 0.12f, 0.14f), true));
        var coin = Mat(new Color(1f, 0.55f, 0.1f), false);
        Prim(PrimitiveType.Cube, cab, "CoinSlotL", new Vector3(-0.07f, 0.6f, -0.418f), new Vector3(0.03f, 0.07f, 0.01f), coin);
        Prim(PrimitiveType.Cube, cab, "CoinSlotR", new Vector3(0.07f, 0.6f, -0.418f), new Vector3(0.03f, 0.07f, 0.01f), coin);

        Prim(PrimitiveType.Cube, cab, "PanelBase", new Vector3(0f, 0.9f, -0.55f), new Vector3(1f, 0.2f, 0.5f), bodyMat);
        var panel = Group("ControlPanel", new Vector3(0f, 1.04f, -0.56f), cab);
        panel.localRotation = Quaternion.Euler(-12f, 0f, 0f);
        Prim(PrimitiveType.Cube, panel, "PanelTop", Vector3.zero, new Vector3(1.02f, 0.06f, 0.5f), Mat(new Color(0.85f, 0.85f, 0.88f), true));
        Prim(PrimitiveType.Cube, panel, "StripeRed", new Vector3(-0.255f, 0.031f, 0.2f), new Vector3(0.5f, 0.003f, 0.05f), Mat(p1Color, true));
        Prim(PrimitiveType.Cube, panel, "StripeBlue", new Vector3(0.255f, 0.031f, 0.2f), new Vector3(0.5f, 0.003f, 0.05f), Mat(p2Color, true));

        BuildPlayerControls(panel, "P1", f1.input, p1Color, -0.36f, -0.2f, -0.08f);
        BuildPlayerControls(panel, "P2", f2.input, p2Color, 0.36f, 0.08f, 0.2f);
    }

    void BuildMarqueeText(Transform cab)
    {
        var go = new GameObject("MarqueeText", typeof(RectTransform));
        var rt = (RectTransform)go.transform;
        rt.SetParent(cab, false);
        rt.localPosition = new Vector3(0f, 2.22f, -0.418f);
        rt.sizeDelta = new Vector2(960f, 360f);
        rt.localScale = Vector3.one * 0.0005f;

        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        go.AddComponent<CanvasScaler>().dynamicPixelsPerUnit = 3f;

        var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        MarqueeWord(rt, font, "COLOR", 0f, 0.5f);
        MarqueeWord(rt, font, "FIGHTER", 0.5f, 1f);
    }

    static void MarqueeWord(RectTransform parent, Font font, string word, float xMin, float xMax)
    {
        var go = new GameObject(word, typeof(RectTransform));
        var r = (RectTransform)go.transform;
        r.SetParent(parent, false);
        r.anchorMin = new Vector2(xMin, 0f);
        r.anchorMax = new Vector2(xMax, 1f);
        r.offsetMin = r.offsetMax = Vector2.zero;

        var t = go.AddComponent<Text>();
        t.font = font;
        t.text = word;
        t.fontSize = 100;
        t.fontStyle = FontStyle.BoldAndItalic;
        t.alignment = TextAnchor.MiddleCenter;
        t.color = Color.white;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.raycastTarget = false;
        var o = go.AddComponent<Outline>();
        o.effectColor = new Color(0f, 0f, 0f, 0.9f);
        o.effectDistance = new Vector2(5f, -5f);
    }

    void BuildPlayerControls(Transform panel, string label, FighterInput input, Color c, float stickX, float punchX, float kickX)
    {
        var dark = Mat(new Color(0.05f, 0.05f, 0.05f), true);
        var group = Group(label + " Controls", Vector3.zero, panel);

        Prim(PrimitiveType.Cylinder, group, "StickBase", new Vector3(stickX, 0.032f, 0f), new Vector3(0.07f, 0.004f, 0.07f), dark);
        var pivot = Group("Stick", new Vector3(stickX, 0.03f, 0f), group);
        Prim(PrimitiveType.Cylinder, pivot, "Shaft", new Vector3(0f, 0.055f, 0f), new Vector3(0.018f, 0.055f, 0.018f), dark);
        Prim(PrimitiveType.Sphere, pivot, "Ball", new Vector3(0f, 0.12f, 0f), Vector3.one * 0.06f, Mat(c, true));

        var punch = Prim(PrimitiveType.Cylinder, group, "PunchButton", new Vector3(punchX, 0.04f, -0.03f), new Vector3(0.055f, 0.012f, 0.055f), Mat(c, true));
        var kick = Prim(PrimitiveType.Cylinder, group, "KickButton", new Vector3(kickX, 0.04f, 0f), new Vector3(0.055f, 0.012f, 0.055f), Mat(c, true));

        var ctrl = group.gameObject.AddComponent<CabinetControls>();
        ctrl.input = input;
        ctrl.stick = pivot;
        ctrl.punchButton = punch;
        ctrl.kickButton = kick;
    }

    #endregion

    #region Salon ve kamera

    void BuildRoom()
    {
        var room = Group("ArcadeRoom", Vector3.zero);
        Prim(PrimitiveType.Cube, room, "Floor", new Vector3(0f, -0.05f, 0f), new Vector3(12f, 0.1f, 12f), Mat(new Color(0.06f, 0.05f, 0.08f), true));
        Prim(PrimitiveType.Cube, room, "Wall", new Vector3(0f, 2f, 0.9f), new Vector3(12f, 4f, 0.2f), Mat(new Color(0.09f, 0.06f, 0.12f), true));

        var body = Mat(new Color(0.05f, 0.05f, 0.07f), true);
        Prim(PrimitiveType.Cube, room, "NeighborL", new Vector3(-1.3f, 1.1f, 0.05f), new Vector3(1f, 2.2f, 0.8f), body);
        Prim(PrimitiveType.Quad, room, "NeighborScreenL", new Vector3(-1.3f, 1.5f, -0.36f), new Vector3(0.7f, 0.5f, 1f), Mat(new Color(0.12f, 0.2f, 0.35f), false));
        Prim(PrimitiveType.Cube, room, "NeighborR", new Vector3(1.3f, 1.1f, 0.05f), new Vector3(1f, 2.2f, 0.8f), body);
        Prim(PrimitiveType.Quad, room, "NeighborScreenR", new Vector3(1.3f, 1.5f, -0.36f), new Vector3(0.7f, 0.5f, 1f), Mat(new Color(0.3f, 0.12f, 0.3f), false));

        var lamp = Group("CeilingLamp", new Vector3(0f, 2.6f, -1.3f), room).gameObject.AddComponent<Light>();
        lamp.type = LightType.Point;
        lamp.color = new Color(1f, 0.85f, 0.7f);
        lamp.intensity = 1.2f;
        lamp.range = 4.5f;
    }

    static void ApplyRenderSettings()
    {
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.16f, 0.13f, 0.2f);
    }

    void SetupMainCamera()
    {
        var cam = Camera.main;
        if (cam == null)
        {
            var go = new GameObject("Main Camera") { tag = "MainCamera" };
            cam = go.AddComponent<Camera>();
        }
        if (cam.GetComponent<AudioListener>() == null) cam.gameObject.AddComponent<AudioListener>();

        cam.transform.position = new Vector3(0f, 1.52f, -1.8f);
        cam.transform.LookAt(new Vector3(0f, 1.33f, -0.3f));
        cam.fieldOfView = 45f;
        cam.nearClipPlane = 0.05f;
        cam.farClipPlane = 30f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = Color.black;
        if (cam.GetComponent<CameraSway>() == null) cam.gameObject.AddComponent<CameraSway>();
        MarkDirty(cam.gameObject);
    }

    #endregion

    #region Materyal ve asset

    Material Mat(Color c, bool lit)
    {
        string key = (lit ? "Lit_" : "Unlit_") + ColorUtility.ToHtmlStringRGB(c);
        if (matCache.TryGetValue(key, out var cached)) return cached;
        var shader = lit ? litShader : unlitShader;
        var m = Asset(key + ".mat", () => new Material(shader));
        m.shader = shader;
        m.color = c;
        MarkDirty(m);
        matCache[key] = m;
        return m;
    }

    RenderTexture ScreenTexture()
    {
        var rt = Asset("ArcadeScreen.renderTexture", () => new RenderTexture(screenWidth, screenHeight, 24));
        if (rt.width != screenWidth || rt.height != screenHeight)
        {
            rt.Release();
            rt.width = screenWidth;
            rt.height = screenHeight;
        }
        rt.filterMode = FilterMode.Point;
        rt.name = "ArcadeScreen";
        MarkDirty(rt);
        return rt;
    }

    T Asset<T>(string fileName, System.Func<T> create) where T : Object
    {
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            if (!AssetDatabase.IsValidFolder(GeneratedFolder))
                AssetDatabase.CreateFolder("Assets", "ColorFighter_Generated");
            string path = GeneratedFolder + "/" + fileName;
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null) return existing;
            var created = create();
            AssetDatabase.CreateAsset(created, path);
            return created;
        }
#endif
        return create();
    }

    static void MarkDirty(Object o)
    {
#if UNITY_EDITOR
        if (!Application.isPlaying && o != null) EditorUtility.SetDirty(o);
#endif
    }

    #endregion

    #region Yardımcılar

    Transform Group(string name, Vector3 localPos, Transform parent = null)
    {
        var t = new GameObject(name).transform;
        t.SetParent(parent != null ? parent : transform, false);
        t.localPosition = localPos;
        return t;
    }

    static Transform Prim(PrimitiveType type, Transform parent, string name, Vector3 localPos, Vector3 scale, Material m)
    {
        var go = GameObject.CreatePrimitive(type);
        go.name = name;
        ColorFighterUtil.Kill(go.GetComponent<Collider>());
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localScale = scale;
        go.GetComponent<Renderer>().sharedMaterial = m;
        return go.transform;
    }

    static Color Dim(Color c, float k) { return new Color(c.r * k, c.g * k, c.b * k, 1f); }

    static Texture2D Checker(Color a, Color b)
    {
        var t = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        t.SetPixels(new[] { a, b, b, a });
        t.filterMode = FilterMode.Point;
        t.wrapMode = TextureWrapMode.Repeat;
        t.Apply();
        return t;
    }

    #endregion

    #region Editör

#if UNITY_EDITOR
    public void BuildInEditor()
    {
        if (Application.isPlaying) return;
        FindShaders();
        ClearBuilt();
        Build();
        AssetDatabase.SaveAssets();
        EditorUtility.SetDirty(this);
        EditorSceneManager.MarkSceneDirty(gameObject.scene);
    }

    public void ClearInEditor()
    {
        if (Application.isPlaying) return;
        ClearBuilt();
        EditorUtility.SetDirty(this);
        EditorSceneManager.MarkSceneDirty(gameObject.scene);
    }

    [MenuItem("Color Fighter/Sahneyi Kur")]
    static void MenuBuild()
    {
        var b = Object.FindAnyObjectByType<ArcadeBootstrap>();
        if (b == null)
        {
            var go = new GameObject("Arcade");
            b = go.AddComponent<ArcadeBootstrap>();
        }
        b.BuildInEditor();
        Selection.activeGameObject = b.gameObject;
    }
#endif

    #endregion
}

#if UNITY_EDITOR
[CustomEditor(typeof(ArcadeBootstrap))]
public class ArcadeBootstrapEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        var b = (ArcadeBootstrap)target;

        EditorGUILayout.Space(8f);
        using (new EditorGUI.DisabledScope(Application.isPlaying))
        {
            if (GUILayout.Button(b.IsBuilt ? "Sahneyi Yeniden Kur" : "Sahneyi Kur", GUILayout.Height(34f)))
                b.BuildInEditor();
            if (b.IsBuilt && GUILayout.Button("Sahneyi Temizle"))
                b.ClearInEditor();
        }

        EditorGUILayout.HelpBox(b.IsBuilt
            ? "Sahne kurulu. Objeler Hierarchy'de 'Arcade' altında. Yeniden Kur, bu objelere elle yaptığın değişiklikleri siler."
            : "Sahneyi Kur'a bas: kabin, arena, dövüşçüler ve arayüz Hierarchy'ye eklenir. Kurmadan Play'e basarsan her şey Play sırasında oluşturulur.",
            MessageType.Info);
    }
}
#endif
