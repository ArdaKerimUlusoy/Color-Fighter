using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

public partial class ArcadeBootstrap : MonoBehaviour
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

    /// <summary>Salonun elle düzenlenmiş hali: her objenin yeri/dönüşü/boyutu ve silinen objeler.</summary>
    [System.Serializable]
    public class HallItem
    {
        public string key;
        public bool active = true;
        public Vector3 position;
        public Quaternion rotation = Quaternion.identity;
        public Vector3 scale = Vector3.one;
        [Tooltip("Senin eklediğin/kopyaladığın objeler için: hangi varsayılan objeden çoğaltılacağı.")]
        public string source;
    }

    [SerializeField, HideInInspector] List<HallItem> hallLayout = new List<HallItem>();
    [SerializeField, HideInInspector] List<string> hallDeleted = new List<string>();
    [SerializeField, HideInInspector] bool hallLayoutSaved;

    public bool HasHallLayout => hallLayoutSaved;
    public int HallLayoutCount => hallLayout.Count;

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
        else UpgradeBuiltScene();

        if (Application.isPlaying) SetupMenuAndVisitors();
    }

    /// <summary>Play'de: salonda dolaşan ziyaretçiler + ana menü (PLAY / QUIT, kabine zoom).</summary>
    void SetupMenuAndVisitors()
    {
        var room = transform.Find("ArcadeRoom");
        if (room != null && transform.Find("ArcadeVisitors") == null)
        {
            var go = new GameObject("ArcadeVisitors");
            go.transform.SetParent(transform, false);
            go.AddComponent<ArcadeVisitors>().Init(transform, room, c => Mat(c, true));
        }
        if (GetComponent<MainMenu>() == null) gameObject.AddComponent<MainMenu>();
    }

    /// <summary>
    /// Sahne editörde eski sürümle kurulduysa, boks arenasını, ek dövüş sahnelerini ve atari salonunu Play sırasında ekler.
    /// (Kalıcı yapmak için Inspector'dan "Sahneyi Yeniden Kur".)
    /// </summary>
    void UpgradeBuiltScene()
    {
        if (!Application.isPlaying) return;
        var arena = transform.Find("FightArena");
        var room = transform.Find("ArcadeRoom");
        // Önceki sürümle kurulmuş sahnelerde ring eteğindeki logo yazıları kalmış olabilir; temizle.
        var boxing = arena != null ? arena.Find("BoxingArena") : null;
        if (boxing != null) KillChildren(boxing, "ApronLogo", "ApronLogoL", "ApronLogoR");

        bool arenaOld = arena != null && boxing == null;
        bool stagesOld = arena != null && (arena.Find(StageSwitcher.Groups[1]) == null || arena.Find(StageSwitcher.Groups[2]) == null || arena.Find(StageSwitcher.Groups[3]) == null);
        bool roomOld = room != null && room.Find("ArcadeHall") == null;
        if (!arenaOld && !stagesOld && !roomOld) return;
        FindShaders();
        matCache.Clear();
        if (arenaOld) SetupBoxingArena(arena);
        if (stagesOld) SetupExtraStages(arena);
        if (roomOld) SetupArcadeHall(room);
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
        if (GetComponent<MainMenu>() == null) gameObject.AddComponent<MainMenu>();
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
            input.punch = Key.F; input.kick = Key.G; input.combo = Key.H;
            input.gamepadIndex = 0;
        }
        else
        {
            input.left = Key.LeftArrow; input.right = Key.RightArrow; input.up = Key.UpArrow; input.down = Key.DownArrow;
            input.punch = Key.K; input.kick = Key.L; input.combo = Key.Semicolon;
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
        Prim(PrimitiveType.Cube, arena, "Floor", new Vector3(0f, -0.1f, 2f), new Vector3(24f, 0.2f, 10f), Mat(Color.white, true));
        Prim(PrimitiveType.Cube, arena, "BackWall", new Vector3(0f, 4f, 7f), new Vector3(30f, 8f, 0.5f), Mat(new Color(0.1f, 0.05f, 0.16f), true));

        SetupBoxingArena(arena);
        SetupExtraStages(arena);

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

    #region Arena: boks ringi (Street Fighter tarzı)

    const float RingHalfW = 5.6f, RingFront = -1.6f, RingBack = 2.8f;

    /// <summary>Arenayı boks ringine çevirir. Hem ilk kurulumda hem eski kurulu sahnelerde çalışır.</summary>
    void SetupBoxingArena(Transform arena)
    {
        // Eski arena parçaları (gün batımı, sütunlar, neon şeritler, önceki salon sahnesi)
        KillChildren(arena, "Sun", "Pillar", "PillarNeon", "NeonRed", "NeonBlue", "ArcadeStage", "BoxingArena");

        // Zemin = ring minderi (dövüşçüler y = 0'da durmaya devam eder)
        var floor = arena.Find("Floor");
        if (floor != null)
        {
            floor.localPosition = new Vector3(0f, -0.1f, (RingFront + RingBack) * 0.5f);
            floor.localScale = new Vector3(RingHalfW * 2f + 0.2f, 0.2f, RingBack - RingFront + 0.2f);
            floor.GetComponent<Renderer>().sharedMaterial = TexturedMat("RingCanvas", RingCanvas, Vector2.one);
        }
        var wall = arena.Find("BackWall");
        if (wall != null)
        {
            wall.localPosition = new Vector3(0f, 5f, 10.6f);
            wall.localScale = new Vector3(44f, 16f, 0.5f);
            wall.GetComponent<Renderer>().sharedMaterial = Mat(new Color(0.05f, 0.04f, 0.08f), true);
        }

        var g = Group("BoxingArena", Vector3.zero, arena);
        var amb = g.gameObject.AddComponent<ArcadeAmbience>();
        float midZ = (RingFront + RingBack) * 0.5f, depth = RingBack - RingFront;

        // Ring platformu (önden görünen etek + logo)
        Prim(PrimitiveType.Cube, g, "Apron", new Vector3(0f, -0.62f, midZ), new Vector3(RingHalfW * 2f + 0.3f, 1.0f, depth + 0.3f), Mat(new Color(0.07f, 0.1f, 0.3f), true));
        Prim(PrimitiveType.Cube, g, "ApronTrim", new Vector3(0f, -0.06f, RingFront - 0.16f), new Vector3(RingHalfW * 2f + 0.32f, 0.07f, 0.04f), Mat(new Color(0.95f, 0.95f, 0.95f), true));

        // Köşe direkleri: sol köşe 1P renginde, sağ köşe 2P renginde
        var post = Mat(new Color(0.55f, 0.56f, 0.6f), true);
        foreach (float z in new[] { RingFront, RingBack })
            foreach (float s in new[] { -1f, 1f })
            {
                Prim(PrimitiveType.Cylinder, g, "Post", new Vector3(s * RingHalfW, 0.75f, z), new Vector3(0.14f, 0.75f, 0.14f), post);
                Prim(PrimitiveType.Cube, g, s < 0 ? "CornerP1" : "CornerP2", new Vector3(s * RingHalfW, 0.95f, z), new Vector3(0.24f, 1.0f, 0.24f), Mat(s < 0 ? p1Color : p2Color, true));
            }

        // Halatlar: arka ve yanlar (önde halat yok ki dövüşçüleri kapatmasın)
        Color[] ropeColors = { new Color(0.85f, 0.15f, 0.15f), new Color(0.95f, 0.95f, 0.95f), new Color(0.15f, 0.3f, 0.85f) };
        float[] ropeY = { 1.35f, 0.95f, 0.55f };
        for (int i = 0; i < 3; i++)
        {
            var rm = Mat(ropeColors[i], true);
            Prim(PrimitiveType.Cube, g, "RopeBack", new Vector3(0f, ropeY[i], RingBack), new Vector3(RingHalfW * 2f, 0.06f, 0.06f), rm);
            Prim(PrimitiveType.Cube, g, "RopeL", new Vector3(-RingHalfW, ropeY[i], midZ), new Vector3(0.06f, 0.06f, depth), rm);
            Prim(PrimitiveType.Cube, g, "RopeR", new Vector3(RingHalfW, ropeY[i], midZ), new Vector3(0.06f, 0.06f, depth), rm);
        }

        // Ring kenarı zemini + arka LED reklam panosu
        Prim(PrimitiveType.Cube, g, "Ringside", new Vector3(0f, -1.1f, 4f), new Vector3(44f, 0.2f, 22f), Mat(new Color(0.05f, 0.045f, 0.06f), true));
        Prim(PrimitiveType.Cube, g, "Barrier", new Vector3(0f, -0.35f, 4.3f), new Vector3(30f, 1.4f, 0.3f), Mat(new Color(0.04f, 0.04f, 0.05f), true));
        Prim(PrimitiveType.Cube, g, "LedBoard", new Vector3(0f, 0.15f, 4.14f), new Vector3(30f, 0.32f, 0.02f), Mat(new Color(0.05f, 0.02f, 0.12f), false));
        var blinkLeds = new List<Text>();
        for (int i = -2; i <= 2; i++)
        {
            bool even = i % 2 == 0;
            var led = NeonSign(g, "LedText", even ? "COLOR FIGHTER" : "INSERT COIN", new Vector3(i * 4.2f, 0.15f, 4.12f), 0.0024f, 1600f, 140f,
                even ? new Color(1f, 0.85f, 0.2f) : new Color(0.3f, 0.95f, 1f), FontStyle.Bold);
            if (!even) blinkLeds.Add(led);
        }
        amb.blinkTexts = blinkLeds.ToArray();

        // Kademeli tribün + seyirci
        var crowd = new List<Transform>();
        var armsL = new List<Transform>();
        var armsR = new List<Transform>();
        var flashes = new List<Renderer>();
        Color[] shirts =
        {
            new Color(0.22f, 0.25f, 0.38f), new Color(0.38f, 0.16f, 0.2f), new Color(0.16f, 0.32f, 0.25f), new Color(0.35f, 0.3f, 0.16f),
            new Color(0.28f, 0.18f, 0.36f), new Color(0.45f, 0.45f, 0.48f), new Color(0.12f, 0.12f, 0.15f), new Color(0.5f, 0.22f, 0.1f),
        };
        Color[] skins = { new Color(0.8f, 0.64f, 0.5f), new Color(0.6f, 0.43f, 0.32f), new Color(0.72f, 0.55f, 0.42f), new Color(0.42f, 0.3f, 0.22f) };
        var rng = new System.Random(11);
        var tierMat = Mat(new Color(0.08f, 0.07f, 0.1f), true);
        for (int row = 0; row < 4; row++)
        {
            float z = 5.2f + row * 1.15f, top = -0.25f + row * 0.62f;
            Prim(PrimitiveType.Cube, g, "Tier", new Vector3(0f, (top - 1f) * 0.5f, z), new Vector3(30f, top + 1f, 1.15f), tierMat);
            for (float x = -10.2f + (row % 2) * 0.55f; x <= 10.2f; x += 1.1f)
            {
                float jx = (float)rng.NextDouble() * 0.3f - 0.15f;
                BuildSpectator(g, new Vector3(x + jx, top, z), shirts[rng.Next(shirts.Length)], skins[rng.Next(skins.Length)], out var root, out var al, out var ar);
                crowd.Add(root); armsL.Add(al); armsR.Add(ar);
                if (rng.NextDouble() < 0.18)
                {
                    var f = Prim(PrimitiveType.Quad, root, "CameraFlash", new Vector3(0.18f, 1.25f, -0.2f), Vector3.one * 0.16f, Mat(Color.white, false)).GetComponent<Renderer>();
                    f.enabled = false;
                    flashes.Add(f);
                }
            }
        }
        amb.crowd = crowd.ToArray();
        amb.crowdArmsL = armsL.ToArray();
        amb.crowdArmsR = armsR.ToArray();
        amb.cameraFlashes = flashes.ToArray();

        // Arka duvar: büyük şampiyona pankartı + oyuncu renginde köşe bayrakları
        Prim(PrimitiveType.Cube, g, "Banner", new Vector3(0f, 4.15f, 10.3f), new Vector3(9f, 1.4f, 0.05f), Mat(new Color(0.35f, 0.04f, 0.06f), true));
        Prim(PrimitiveType.Cube, g, "BannerTrim", new Vector3(0f, 3.48f, 10.28f), new Vector3(9f, 0.08f, 0.04f), Mat(new Color(1f, 0.8f, 0.25f), false));
        var title = NeonSign(g, "BannerTitle", "COLOR FIGHTER", new Vector3(0f, 4.35f, 10.26f), 0.0058f, 1400f, 160f, new Color(1f, 0.85f, 0.3f), FontStyle.BoldAndItalic);
        NeonSign(g, "BannerSub", "WORLD  CHAMPIONSHIP", new Vector3(0f, 3.8f, 10.26f), 0.0028f, 1600f, 160f, Color.white, FontStyle.Bold);
        amb.flickerTexts = new[] { title };
        foreach (float s in new[] { -1f, 1f })
        {
            Prim(PrimitiveType.Cube, g, s < 0 ? "BannerP1" : "BannerP2", new Vector3(s * 5.8f, 4.0f, 10.3f), new Vector3(1.3f, 1.8f, 0.05f), Mat(s < 0 ? p1Color : p2Color, true));
            Prim(PrimitiveType.Cube, g, "BannerStripe", new Vector3(s * 5.8f, 4.0f, 10.27f), new Vector3(0.22f, 1.8f, 0.02f), Mat(new Color(0.95f, 0.95f, 0.95f), true));
            NeonSign(g, "BannerTag", s < 0 ? "1P" : "2P", new Vector3(s * 5.8f, 4.45f, 10.25f), 0.0045f, 400f, 160f, Color.white, FontStyle.Bold);
        }

        // Tavandan ringe vuran spot ışıklar
        amb.lights = new[]
        {
            Spot(g, "SpotL", new Vector3(-2.6f, 6.5f, 0.2f), new Color(1f, 0.95f, 0.85f), 5f),
            Spot(g, "SpotR", new Vector3(2.6f, 6.5f, 0.2f), new Color(1f, 0.95f, 0.85f), 5f),
            StageLight(g, "CrowdGlow", new Vector3(0f, 3.5f, 6.5f), new Color(0.5f, 0.35f, 0.9f), 1.2f, 10f),
        };
    }

    Light Spot(Transform parent, string name, Vector3 pos, Color c, float intensity)
    {
        var t = Group(name, pos, parent);
        t.localRotation = Quaternion.Euler(90f, 0f, 0f);
        var l = t.gameObject.AddComponent<Light>();
        l.type = LightType.Spot;
        l.color = c;
        l.intensity = intensity;
        l.range = 12f;
        l.spotAngle = 55f;
        return l;
    }

    /// <summary>Ring minderi: açık gri-mavi kanvas, koyu kenar bandı, ortada daire.</summary>
    static Texture2D RingCanvas()
    {
        const int W = 160, H = 64;
        var px = new Color[W * H];
        var rng = new System.Random(3);
        var baseC = new Color(0.72f, 0.74f, 0.8f);
        var edge = new Color(0.22f, 0.26f, 0.5f);
        var logo = new Color(0.75f, 0.2f, 0.22f);
        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                float n = (float)rng.NextDouble() * 0.04f;
                Color c = new Color(baseC.r - n, baseC.g - n, baseC.b - n);
                if (x < 5 || x >= W - 5 || y < 4 || y >= H - 4) c = edge;
                float dx = (x - W * 0.5f) / 1.25f, dy = y - H * 0.5f;   // 2.5:1 gerilmeyi dengele
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                if (d > 12f && d < 15f) c = logo;
                px[y * W + x] = c;
            }
        var t = new Texture2D(W, H, TextureFormat.RGBA32, false);
        t.SetPixels(px);
        t.filterMode = FilterMode.Point;
        t.wrapMode = TextureWrapMode.Clamp;
        t.name = "RingCanvas";
        t.Apply();
        return t;
    }

    #endregion

    #region Oda: atari salonu

    static readonly Color[] NeonColors =
    {
        new Color(1f, 0.25f, 0.65f), new Color(0.2f, 0.9f, 1f), new Color(1f, 0.85f, 0.2f),
        new Color(0.35f, 1f, 0.45f), new Color(1f, 0.5f, 0.15f), new Color(0.7f, 0.35f, 1f),
    };

    /// <summary>Kabinin durduğu odayı dolu bir atari salonuna çevirir. Hem ilk kurulumda hem eski kurulu sahnelerde çalışır.</summary>
    void SetupArcadeHall(Transform room)
    {
        KillChildren(room, "NeighborL", "NeighborR", "NeighborScreenL", "NeighborScreenR", "ArcadeHall");

        var floor = room.Find("Floor");
        if (floor != null) floor.GetComponent<Renderer>().sharedMaterial = TexturedMat("ArcadeCarpet", ArcadeCarpet, new Vector2(5f, 5f));
        var wall = room.Find("Wall");
        if (wall != null) wall.GetComponent<Renderer>().sharedMaterial = Mat(new Color(0.07f, 0.05f, 0.11f), true);

        var g = Group("ArcadeHall", Vector3.zero, room);
        BuildHallContents(g);
        ApplyHallLayout(g);
    }

    /// <summary>Salonun varsayılan içeriği. Elle düzenleme kaydı bu yapının obje sırasına göre eşleşir; sırayı değiştirme.</summary>
    void BuildHallContents(Transform g)
    {
        var amb = g.gameObject.AddComponent<ArcadeAmbience>();
        var screens = new List<Renderer>();
        var sprites = new List<Transform>();

        // Duvarlar ve tavan: içe bakan tek yüzlü quad'lar (Scene görünümünde dışarıdan içi görünür)
        var wallMat = Mat(new Color(0.07f, 0.05f, 0.11f), true);
        Prim(PrimitiveType.Quad, g, "WallLeft", new Vector3(-6f, 2f, -2.55f), new Vector3(6.9f, 4f, 1f), wallMat).localRotation = Quaternion.Euler(0f, -90f, 0f);
        Prim(PrimitiveType.Quad, g, "WallRight", new Vector3(6f, 2f, -2.55f), new Vector3(6.9f, 4f, 1f), wallMat).localRotation = Quaternion.Euler(0f, 90f, 0f);
        Prim(PrimitiveType.Quad, g, "WallFront", new Vector3(0f, 2f, -6f), new Vector3(12f, 4f, 1f), wallMat).localRotation = Quaternion.Euler(0f, 180f, 0f);
        Prim(PrimitiveType.Quad, g, "Ceiling", new Vector3(0f, 3.8f, -2.55f), new Vector3(12f, 6.9f, 1f), Mat(new Color(0.04f, 0.03f, 0.06f), true)).localRotation = Quaternion.Euler(-90f, 0f, 0f);

        // Tavan ve duvar neonları
        Prim(PrimitiveType.Cube, g, "CeilNeonPink", new Vector3(0f, 3.74f, -0.4f), new Vector3(11.6f, 0.06f, 0.06f), Mat(NeonColors[0], false));
        Prim(PrimitiveType.Cube, g, "CeilNeonCyan", new Vector3(0f, 3.74f, -2.8f), new Vector3(11.6f, 0.06f, 0.06f), Mat(NeonColors[1], false));
        Prim(PrimitiveType.Cube, g, "CeilNeonPurple", new Vector3(0f, 3.74f, -5.2f), new Vector3(11.6f, 0.06f, 0.06f), Mat(NeonColors[5], false));
        Prim(PrimitiveType.Cube, g, "WallNeonBack", new Vector3(0f, 2.55f, 0.78f), new Vector3(12f, 0.05f, 0.05f), Mat(NeonColors[5], false));
        Prim(PrimitiveType.Cube, g, "WallNeonL", new Vector3(-5.97f, 2.6f, -2.55f), new Vector3(0.05f, 0.05f, 6.9f), Mat(NeonColors[0], false));
        Prim(PrimitiveType.Cube, g, "WallNeonR", new Vector3(5.97f, 2.6f, -2.55f), new Vector3(0.05f, 0.05f, 6.9f), Mat(NeonColors[1], false));

        // Arka sıra: kendi kabinimizin iki yanında gerçek kabinler
        int ci = 0;
        foreach (float x in new[] { -1.25f, 1.25f, -2.5f, 2.5f, -3.75f, 3.75f, -5f, 5f })
            BuildStageCabinet(g, new Vector3(x, 0f, 0.05f), NeonColors[ci++ % NeonColors.Length], screens, sprites, 0f);
        // Yan duvarlar boyunca içe bakan kabinler
        foreach (float z in new[] { -1.4f, -2.65f, -3.9f })
        {
            BuildStageCabinet(g, new Vector3(-5.45f, 0f, z), NeonColors[ci++ % NeonColors.Length], screens, sprites, -90f);
            BuildStageCabinet(g, new Vector3(5.45f, 0f, z), NeonColors[ci++ % NeonColors.Length], screens, sprites, 90f);
        }
        amb.screens = screens.ToArray();
        amb.screenSprites = sprites.ToArray();

        // Ön köşelerde pelüş makineleri, ortada hava hokeyi
        BuildClawMachine(g, new Vector3(-4.9f, 0f, -5.3f), NeonColors[0]);
        BuildClawMachine(g, new Vector3(4.9f, 0f, -5.3f), NeonColors[1]);
        BuildAirHockey(g, new Vector3(0f, 0f, -4.3f));

        // Yan kabinlerde oynayan iki oyuncu
        BuildSpectator(g, new Vector3(-4.55f, 0f, -2.65f), new Color(0.3f, 0.25f, 0.5f), new Color(0.85f, 0.66f, 0.52f), out var pl1, out var a1, out var b1);
        BuildSpectator(g, new Vector3(4.55f, 0f, -1.4f), new Color(0.5f, 0.2f, 0.22f), new Color(0.6f, 0.43f, 0.32f), out var pl2, out var a2, out var b2);
        amb.crowd = new[] { pl1, pl2 };
        amb.crowdArmsL = new[] { a1, a2 };
        amb.crowdArmsR = new[] { b1, b2 };

        // Neon tabelalar
        var pink = new Color(1f, 0.35f, 0.75f);
        var cyan = new Color(0.3f, 0.95f, 1f);
        var yellow = new Color(1f, 0.88f, 0.25f);
        var arcade = NeonSign(g, "SignArcade", "ARCADE", new Vector3(0f, 3.1f, 0.77f), 0.0048f, 1100f, 160f, pink, FontStyle.BoldAndItalic);
        var arcadeTubes = NeonFrame(g, "SignArcadeFrame", new Vector3(0f, 3.1f, 0.79f), new Vector2(2.9f, 0.75f), pink, 0f);
        var coin = NeonSign(g, "SignInsertCoin", "INSERT COIN", new Vector3(-3.4f, 3.1f, 0.77f), 0.0028f, 1100f, 160f, yellow, FontStyle.Bold);
        NeonSign(g, "SignHighScore", "HIGH SCORE", new Vector3(3.4f, 3.1f, 0.77f), 0.0028f, 1100f, 160f, cyan, FontStyle.Bold);
        NeonSign(g, "SignGameZone", "GAME ZONE", new Vector3(-5.95f, 3.15f, -2.55f), 0.0038f, 1100f, 160f, cyan, FontStyle.BoldAndItalic, -90f);
        NeonSign(g, "SignPlayNow", "PLAY NOW!", new Vector3(5.95f, 3.15f, -2.55f), 0.0038f, 1100f, 160f, pink, FontStyle.BoldAndItalic, 90f);
        NeonSign(g, "SignPrizes", "PRIZES", new Vector3(0f, 3.0f, -5.95f), 0.0045f, 1100f, 160f, yellow, FontStyle.BoldAndItalic, 180f);
        amb.flickerTexts = new[] { arcade };
        amb.flickerTubes = arcadeTubes;
        amb.blinkTexts = new[] { coin };

        amb.lights = new[]
        {
            StageLight(g, "HallPink", new Vector3(-3.2f, 3.2f, -1.6f), pink, 1.4f, 6f),
            StageLight(g, "HallCyan", new Vector3(3.2f, 3.2f, -1.6f), cyan, 1.4f, 6f),
            StageLight(g, "HallPurple", new Vector3(0f, 3.2f, -4.6f), new Color(0.65f, 0.4f, 1f), 1.4f, 7f),
        };
    }

    void BuildStageCabinet(Transform parent, Vector3 pos, Color neon, List<Renderer> screens, List<Transform> sprites, float yaw)
    {
        var cab = Group("Cabinet", pos, parent);
        cab.localRotation = Quaternion.Euler(0f, yaw, 0f);
        var body = Mat(new Color(0.07f, 0.06f, 0.1f), true);
        var black = Mat(new Color(0.02f, 0.02f, 0.03f), true);
        var dimNeon = Mat(Dim(neon, 0.75f), false);

        Prim(PrimitiveType.Cube, cab, "Lower", new Vector3(0f, 0.55f, 0f), new Vector3(1f, 1.1f, 0.85f), body);
        Prim(PrimitiveType.Cube, cab, "Upper", new Vector3(0f, 1.6f, 0.05f), new Vector3(1f, 1f, 0.75f), body);
        Prim(PrimitiveType.Cube, cab, "Top", new Vector3(0f, 2.22f, 0f), new Vector3(1f, 0.3f, 0.85f), body);
        Prim(PrimitiveType.Cube, cab, "Marquee", new Vector3(0f, 2.22f, -0.43f), new Vector3(0.9f, 0.22f, 0.02f), Mat(neon, false));
        Prim(PrimitiveType.Cube, cab, "SideArtL", new Vector3(-0.505f, 1.2f, 0f), new Vector3(0.02f, 2.2f, 0.8f), Mat(Dim(neon, 0.45f), true));
        Prim(PrimitiveType.Cube, cab, "SideArtR", new Vector3(0.505f, 1.2f, 0f), new Vector3(0.02f, 2.2f, 0.8f), Mat(Dim(neon, 0.45f), true));
        Prim(PrimitiveType.Cube, cab, "EdgeL", new Vector3(-0.49f, 1.2f, -0.44f), new Vector3(0.03f, 2.1f, 0.02f), dimNeon);
        Prim(PrimitiveType.Cube, cab, "EdgeR", new Vector3(0.49f, 1.2f, -0.44f), new Vector3(0.03f, 2.1f, 0.02f), dimNeon);

        Prim(PrimitiveType.Cube, cab, "Bezel", new Vector3(0f, 1.58f, -0.33f), new Vector3(0.84f, 0.66f, 0.02f), black);
        var screen = Prim(PrimitiveType.Quad, cab, "Screen", new Vector3(0f, 1.58f, -0.345f), new Vector3(0.72f, 0.54f, 1f), Mat(new Color(0.1f, 0.2f, 0.4f), false));
        screens.Add(screen.GetComponent<Renderer>());
        sprites.Add(Prim(PrimitiveType.Quad, cab, "ScreenSprite", new Vector3(0f, 1.58f, -0.35f), new Vector3(0.07f, 0.07f, 1f), Mat(new Color(1f, 1f, 0.9f), false)));

        Prim(PrimitiveType.Cube, cab, "Panel", new Vector3(0f, 1.05f, -0.6f), new Vector3(1f, 0.12f, 0.45f), body);
        Prim(PrimitiveType.Sphere, cab, "StickBall", new Vector3(-0.25f, 1.17f, -0.65f), Vector3.one * 0.08f, Mat(neon, true));
        Prim(PrimitiveType.Cube, cab, "Btn1", new Vector3(0.1f, 1.12f, -0.65f), new Vector3(0.07f, 0.03f, 0.07f), Mat(neon, false));
        Prim(PrimitiveType.Cube, cab, "Btn2", new Vector3(0.25f, 1.12f, -0.65f), new Vector3(0.07f, 0.03f, 0.07f), Mat(new Color(1f, 0.95f, 0.85f), false));

        Prim(PrimitiveType.Cube, cab, "CoinDoor", new Vector3(0f, 0.5f, -0.43f), new Vector3(0.3f, 0.28f, 0.02f), Mat(new Color(0.12f, 0.12f, 0.14f), true));
        Prim(PrimitiveType.Cube, cab, "CoinSlot", new Vector3(0f, 0.55f, -0.445f), new Vector3(0.03f, 0.07f, 0.01f), Mat(new Color(1f, 0.55f, 0.1f), false));
    }

    void BuildClawMachine(Transform parent, Vector3 pos, Color neon)
    {
        var m = Group("ClawMachine", pos, parent);
        m.localRotation = Quaternion.Euler(0f, 180f, 0f);   // ön yüzü salona baksın
        var body = Mat(Dim(neon, 0.6f), true);
        var frame = Mat(new Color(0.85f, 0.85f, 0.9f), true);
        Prim(PrimitiveType.Cube, m, "Base", new Vector3(0f, 0.45f, 0f), new Vector3(1.1f, 0.9f, 1.1f), body);
        Prim(PrimitiveType.Cube, m, "PrizeChute", new Vector3(0.3f, 0.5f, -0.555f), new Vector3(0.32f, 0.3f, 0.02f), Mat(new Color(0.03f, 0.03f, 0.04f), true));
        foreach (float x in new[] { -0.53f, 0.53f })
            foreach (float z in new[] { -0.53f, 0.53f })
                Prim(PrimitiveType.Cube, m, "GlassPost", new Vector3(x, 1.45f, z), new Vector3(0.04f, 1.1f, 0.04f), frame);
        Prim(PrimitiveType.Cube, m, "Roof", new Vector3(0f, 2.15f, 0f), new Vector3(1.12f, 0.3f, 1.12f), body);
        Prim(PrimitiveType.Cube, m, "RoofSign", new Vector3(0f, 2.15f, -0.565f), new Vector3(0.95f, 0.2f, 0.02f), Mat(neon, false));
        var rng = new System.Random(pos.x < 0f ? 5 : 9);
        for (int i = 0; i < 12; i++)
        {
            var c = NeonColors[rng.Next(NeonColors.Length)];
            float x = (float)rng.NextDouble() * 0.8f - 0.4f, z = (float)rng.NextDouble() * 0.8f - 0.4f;
            Prim(PrimitiveType.Sphere, m, "Prize", new Vector3(x, 0.98f + (i % 3) * 0.08f, z), Vector3.one * 0.18f, Mat(c, true));
        }
        Prim(PrimitiveType.Cube, m, "ClawRail", new Vector3(0f, 1.95f, 0f), new Vector3(0.9f, 0.03f, 0.03f), frame);
        Prim(PrimitiveType.Cube, m, "ClawCable", new Vector3(0.1f, 1.75f, 0f), new Vector3(0.015f, 0.4f, 0.015f), frame);
        Prim(PrimitiveType.Cube, m, "Claw", new Vector3(0.1f, 1.52f, 0f), new Vector3(0.12f, 0.08f, 0.12f), Mat(new Color(1f, 0.85f, 0.2f), true));
        Prim(PrimitiveType.Cube, m, "Joystick", new Vector3(-0.2f, 0.95f, -0.45f), new Vector3(0.05f, 0.12f, 0.05f), Mat(new Color(0.05f, 0.05f, 0.06f), true));
    }

    void BuildAirHockey(Transform parent, Vector3 pos)
    {
        var t = Group("AirHockey", pos, parent);
        var dark = Mat(new Color(0.08f, 0.08f, 0.1f), true);
        Prim(PrimitiveType.Cube, t, "Body", new Vector3(0f, 0.4f, 0f), new Vector3(2.2f, 0.5f, 1.2f), dark);
        Prim(PrimitiveType.Cube, t, "Surface", new Vector3(0f, 0.66f, 0f), new Vector3(2.0f, 0.02f, 1.0f), Mat(new Color(0.55f, 0.8f, 1f), true));
        Prim(PrimitiveType.Cube, t, "CenterLine", new Vector3(0f, 0.675f, 0f), new Vector3(0.03f, 0.01f, 1.0f), Mat(new Color(0.9f, 0.15f, 0.2f), false));
        var rail = Mat(new Color(0.9f, 0.15f, 0.2f), true);
        Prim(PrimitiveType.Cube, t, "RailN", new Vector3(0f, 0.7f, 0.55f), new Vector3(2.2f, 0.08f, 0.1f), rail);
        Prim(PrimitiveType.Cube, t, "RailS", new Vector3(0f, 0.7f, -0.55f), new Vector3(2.2f, 0.08f, 0.1f), rail);
        Prim(PrimitiveType.Cube, t, "RailW", new Vector3(-1.05f, 0.7f, 0f), new Vector3(0.1f, 0.08f, 1.2f), rail);
        Prim(PrimitiveType.Cube, t, "RailE", new Vector3(1.05f, 0.7f, 0f), new Vector3(0.1f, 0.08f, 1.2f), rail);
        Prim(PrimitiveType.Cylinder, t, "Puck", new Vector3(0.3f, 0.685f, 0.1f), new Vector3(0.1f, 0.01f, 0.1f), Mat(new Color(0.05f, 0.05f, 0.05f), true));
        Prim(PrimitiveType.Cylinder, t, "MalletA", new Vector3(-0.8f, 0.7f, 0f), new Vector3(0.14f, 0.03f, 0.14f), Mat(NeonColors[0], true));
        Prim(PrimitiveType.Cylinder, t, "MalletB", new Vector3(0.8f, 0.7f, -0.2f), new Vector3(0.14f, 0.03f, 0.14f), Mat(NeonColors[1], true));
        foreach (float x in new[] { -0.95f, 0.95f })
            foreach (float z in new[] { -0.5f, 0.5f })
                Prim(PrimitiveType.Cube, t, "Leg", new Vector3(x, 0.08f, z), new Vector3(0.1f, 0.16f, 0.1f), dark);
    }

    #endregion

    #region Salon düzeni kaydı (elle yapılan değişiklikler)

    /// <summary>
    /// Mevcut ArcadeHall'u kaydeder: her objenin yeri, dönüşü, boyutu, açık/kapalı durumu
    /// ve varsayılan yapıda olup senin sildiğin objeler. "Sahneyi Kur" bunu otomatik çağırır.
    /// </summary>
    public bool CaptureHallLayout()
    {
        var hall = transform.Find("ArcadeRoom/ArcadeHall");
        if (hall == null) return false;
        FindShaders();

        // Varsayılan salonu geçici kur, mevcut salondaki her objeyi ona eşleştir
        var probe = new GameObject("HallKeyProbe").transform;
        probe.SetParent(transform, false);
        BuildHallContents(probe);

        var items = new List<HallItem> { Item(".", hall, null) };
        var matched = new HashSet<string>();
        MatchTrees(hall, probe, "", items, matched);

        var all = new List<KeyValuePair<string, Transform>>();
        CollectKeys(probe, "", all);
        var deleted = all.Select(i => i.Key).Where(k => !matched.Contains(k)).ToList();

        probe.gameObject.SetActive(false);
        ColorFighterUtil.Kill(probe.gameObject);

        hallLayout = items;
        hallDeleted = deleted;
        hallLayoutSaved = true;
        return true;
    }

    public void ClearHallLayout()
    {
        hallLayout.Clear();
        hallDeleted.Clear();
        hallLayoutSaved = false;
    }

    static HallItem Item(string key, Transform t, string source)
    {
        return new HallItem
        {
            key = key,
            source = source,
            active = t.gameObject.activeSelf,
            position = t.localPosition,
            rotation = t.localRotation,
            scale = t.localScale,
        };
    }

    /// <summary>
    /// Mevcut objeleri varsayılanlara eşleştirir: aynı isim + aynı materyaller + en yakın varsayılan pozisyon.
    /// Sıraya güvenmez; ortadan bir obje silinse de diğerleri doğru tanınır.
    /// </summary>
    static void MatchTrees(Transform cur, Transform def, string prefix, List<HallItem> items, HashSet<string> matched)
    {
        var defs = new List<KeyValuePair<string, Transform>>();
        var counts = new Dictionary<string, int>();
        foreach (Transform d in def)
        {
            counts.TryGetValue(d.name, out int n);
            counts[d.name] = n + 1;
            defs.Add(new KeyValuePair<string, Transform>(prefix + d.name + "#" + n, d));
        }
        var defSig = defs.Select(d => Signature(d.Value)).ToArray();
        var used = new bool[defs.Count];
        int added = 0;

        foreach (Transform c in cur)
        {
            string sig = Signature(c);
            string baseName = BaseName(c.name);
            int best = -1, template = -1;
            float bestD = float.MaxValue, templD = float.MaxValue;
            for (int i = 0; i < defs.Count; i++)
            {
                var d = defs[i].Value;
                if (d.name != baseName) continue;
                float dist = (c.localPosition - d.localPosition).sqrMagnitude + (defSig[i] == sig ? 0f : 1e6f);
                if (dist < templD) { templD = dist; template = i; }
                if (!used[i] && c.name == d.name && dist < bestD) { bestD = dist; best = i; }
            }

            if (best >= 0)
            {
                used[best] = true;
                string key = defs[best].Key;
                matched.Add(key);
                items.Add(Item(key, c, null));
                MatchTrees(c, defs[best].Value, key + "/", items, matched);
            }
            else if (template >= 0)
            {
                // Senin eklediğin kopya (ör. "Cabinet (1)"): en benzer varsayılandan çoğaltılır
                items.Add(Item(prefix + c.name + "+" + added++, c, defs[template].Key));
            }
        }
    }

    static string BaseName(string n) => System.Text.RegularExpressions.Regex.Replace(n, @" \(\d+\)$", "");

    static string Signature(Transform t)
    {
        var sb = new System.Text.StringBuilder();
        foreach (var r in t.GetComponentsInChildren<Renderer>(true))
            sb.Append(r.sharedMaterial != null ? r.sharedMaterial.name : "-").Append('|');
        return sb.ToString();
    }

    void ApplyHallLayout(Transform hall)
    {
        if (!hallLayoutSaved) return;

        var items = new List<KeyValuePair<string, Transform>>();
        CollectKeys(hall, "", items);
        var map = new Dictionary<string, Transform> { { ".", hall } };
        foreach (var it in items) map[it.Key] = it.Value;

        // 1) Yer/dönüş/boyut; senin eklediğin kopyalar kaynak objeden çoğaltılır
        foreach (var item in hallLayout)
        {
            Transform t;
            if (!string.IsNullOrEmpty(item.source)) t = CloneFromSource(hall, item, map);
            else if (!map.TryGetValue(item.key, out t)) t = null;
            if (t == null) continue;
            t.localPosition = item.position;
            t.localRotation = item.rotation;
            t.localScale = item.scale;
            if (t.gameObject.activeSelf != item.active) t.gameObject.SetActive(item.active);
        }

        // 2) Sildiklerin
        foreach (var k in hallDeleted)
            if (map.TryGetValue(k, out var t) && t != null)
            {
                t.gameObject.SetActive(false);
                ColorFighterUtil.Kill(t.gameObject);
            }

        RefreshHallAmbience(hall);
    }

    Transform CloneFromSource(Transform hall, HallItem item, Dictionary<string, Transform> map)
    {
        if (!map.TryGetValue(item.source, out var template) || template == null) return null;
        int slash = item.key.LastIndexOf('/');
        string parentKey = slash >= 0 ? item.key.Substring(0, slash) : "";
        string segment = slash >= 0 ? item.key.Substring(slash + 1) : item.key;
        Transform parent = parentKey.Length == 0 ? hall : (map.TryGetValue(parentKey, out var p) ? p : null);
        if (parent == null) return null;

        var clone = Instantiate(template.gameObject, parent).transform;
        clone.name = segment.Substring(0, Mathf.Max(0, segment.LastIndexOf('+')));
        map[item.key] = clone;
        return clone;
    }

    /// <summary>Düzenlemeden sonra animasyon listelerini sahnede kalan objelerden yeniden toplar.</summary>
    static void RefreshHallAmbience(Transform hall)
    {
        var amb = hall.GetComponent<ArcadeAmbience>();
        if (amb == null) return;
        bool Alive(Component c) => c != null && c.gameObject.activeInHierarchy;

        var screens = new List<Renderer>();
        var sprites = new List<Transform>();
        foreach (var r in hall.GetComponentsInChildren<Renderer>(true))
        {
            if (!Alive(r)) continue;
            if (r.name == "Screen") screens.Add(r);
            else if (r.name == "ScreenSprite") sprites.Add(r.transform);
        }
        var crowd = new List<Transform>();
        var armsL = new List<Transform>();
        var armsR = new List<Transform>();
        foreach (var t in hall.GetComponentsInChildren<Transform>(true))
        {
            if (!Alive(t) || !t.name.StartsWith("Spectator")) continue;
            crowd.Add(t);
            armsL.Add(t.Find("ArmL"));
            armsR.Add(t.Find("ArmR"));
        }
        amb.screens = screens.ToArray();
        amb.screenSprites = sprites.ToArray();
        amb.crowd = crowd.ToArray();
        amb.crowdArmsL = armsL.ToArray();
        amb.crowdArmsR = armsR.ToArray();
        amb.flickerTexts = (amb.flickerTexts ?? new Text[0]).Where(Alive).ToArray();
        amb.flickerTubes = (amb.flickerTubes ?? new Renderer[0]).Where(Alive).ToArray();
        amb.blinkTexts = (amb.blinkTexts ?? new Text[0]).Where(Alive).ToArray();
        amb.blinkTubes = (amb.blinkTubes ?? new Renderer[0]).Where(Alive).ToArray();
        amb.lights = (amb.lights ?? new Light[0]).Where(Alive).ToArray();
    }

    /// <summary>Her objeye hiyerarşideki yerine göre sabit bir anahtar verir: "Cabinet#3/Marquee#0".</summary>
    static void CollectKeys(Transform t, string prefix, List<KeyValuePair<string, Transform>> output)
    {
        var counts = new Dictionary<string, int>();
        foreach (Transform c in t)
        {
            counts.TryGetValue(c.name, out int n);
            counts[c.name] = n + 1;
            string k = prefix + c.name + "#" + n;
            output.Add(new KeyValuePair<string, Transform>(k, c));
            CollectKeys(c, k + "/", output);
        }
    }

    #endregion

    #region Ortak dekor yardımcıları

    void BuildSpectator(Transform parent, Vector3 pos, Color shirt, Color skin, out Transform root, out Transform armL, out Transform armR)
    {
        root = Group("Spectator", pos, parent);
        var shirtMat = Mat(shirt, true);
        Prim(PrimitiveType.Cube, root, "Legs", new Vector3(0f, 0.42f, 0f), new Vector3(0.34f, 0.84f, 0.22f), Mat(new Color(0.1f, 0.1f, 0.14f), true));
        Prim(PrimitiveType.Cube, root, "Torso", new Vector3(0f, 1.12f, 0f), new Vector3(0.46f, 0.58f, 0.26f), shirtMat);
        Prim(PrimitiveType.Cube, root, "Head", new Vector3(0f, 1.58f, 0f), new Vector3(0.24f, 0.28f, 0.24f), Mat(skin, true));
        Prim(PrimitiveType.Cube, root, "Hair", new Vector3(0f, 1.74f, 0.01f), new Vector3(0.26f, 0.08f, 0.26f), Mat(new Color(0.08f, 0.06f, 0.05f), true));

        armL = Group("ArmL", new Vector3(-0.29f, 1.36f, 0f), root);
        Prim(PrimitiveType.Cube, armL, "Arm", new Vector3(0f, -0.25f, 0f), new Vector3(0.12f, 0.5f, 0.12f), shirtMat);
        armR = Group("ArmR", new Vector3(0.29f, 1.36f, 0f), root);
        Prim(PrimitiveType.Cube, armR, "Arm", new Vector3(0f, -0.25f, 0f), new Vector3(0.12f, 0.5f, 0.12f), shirtMat);
    }

    static Text NeonSign(Transform parent, string name, string text, Vector3 pos, float scale, float width, float height, Color c, FontStyle style, float yaw = 0f)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.localPosition = pos;
        rt.localRotation = Quaternion.Euler(0f, yaw, 0f);
        rt.sizeDelta = new Vector2(width, height);
        rt.localScale = Vector3.one * scale;

        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        go.AddComponent<CanvasScaler>().dynamicPixelsPerUnit = 3f;

        var tgo = new GameObject("Text", typeof(RectTransform));
        var tr = (RectTransform)tgo.transform;
        tr.SetParent(rt, false);
        tr.anchorMin = Vector2.zero;
        tr.anchorMax = Vector2.one;
        tr.offsetMin = tr.offsetMax = Vector2.zero;

        var t = tgo.AddComponent<Text>();
        t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        t.text = text;
        t.fontSize = 100;
        t.fontStyle = style;
        t.alignment = TextAnchor.MiddleCenter;
        t.lineSpacing = 0.9f;
        t.color = c;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.raycastTarget = false;
        var o = tgo.AddComponent<Outline>();
        o.effectColor = new Color(c.r, c.g, c.b, 0.45f);
        o.effectDistance = new Vector2(5f, -5f);
        return t;
    }

    Renderer[] NeonFrame(Transform parent, string name, Vector3 center, Vector2 size, Color c, float yaw)
    {
        var g = Group(name, center, parent);
        g.localRotation = Quaternion.Euler(0f, yaw, 0f);
        var m = Mat(c, false);
        const float th = 0.04f;
        return new[]
        {
            Prim(PrimitiveType.Cube, g, "Top", new Vector3(0f, size.y * 0.5f, 0f), new Vector3(size.x, th, th), m).GetComponent<Renderer>(),
            Prim(PrimitiveType.Cube, g, "Bottom", new Vector3(0f, -size.y * 0.5f, 0f), new Vector3(size.x, th, th), m).GetComponent<Renderer>(),
            Prim(PrimitiveType.Cube, g, "Left", new Vector3(-size.x * 0.5f, 0f, 0f), new Vector3(th, size.y, th), m).GetComponent<Renderer>(),
            Prim(PrimitiveType.Cube, g, "Right", new Vector3(size.x * 0.5f, 0f, 0f), new Vector3(th, size.y, th), m).GetComponent<Renderer>(),
        };
    }

    Light StageLight(Transform parent, string name, Vector3 pos, Color c, float intensity, float range)
    {
        var l = Group(name, pos, parent).gameObject.AddComponent<Light>();
        l.type = LightType.Point;
        l.color = c;
        l.intensity = intensity;
        l.range = range;
        return l;
    }

    /// <summary>Doku + lit materyal. Editörde asset olarak kaydedilir, Play'de bellekte oluşur.</summary>
    Material TexturedMat(string key, System.Func<Texture2D> makeTexture, Vector2 tiling)
    {
        var m = Asset(key + ".mat", () => new Material(litShader));
        m.shader = litShader;
        m.color = Color.white;
        m.mainTexture = Asset(key + ".asset", makeTexture);
        m.mainTextureScale = tiling;
        MarkDirty(m);
        return m;
    }

    static void KillChildren(Transform parent, params string[] names)
    {
        var doomed = new List<GameObject>();
        foreach (Transform c in parent)
            if (System.Array.IndexOf(names, c.name) >= 0) doomed.Add(c.gameObject);
        foreach (var go in doomed)
        {
            go.SetActive(false);   // Destroy kare sonuna kadar beklediği için hemen gizle
            ColorFighterUtil.Kill(go);
        }
    }

    /// <summary>Klasik atari salonu halısı: koyu zemin üstünde neon şekiller (döşenebilir).</summary>
    static Texture2D ArcadeCarpet()
    {
        const int S = 64;
        var bg = new Color(0.06f, 0.04f, 0.13f);
        var px = new Color[S * S];
        for (int i = 0; i < px.Length; i++) px[i] = bg;

        Color[] neon =
        {
            new Color(0.75f, 0.2f, 0.55f), new Color(0.15f, 0.6f, 0.75f),
            new Color(0.75f, 0.65f, 0.15f), new Color(0.45f, 0.25f, 0.8f),
        };
        var rng = new System.Random(7);
        void Put(int x, int y, Color c) { px[((y % S + S) % S) * S + ((x % S + S) % S)] = c; }

        for (int k = 0; k < 46; k++)
        {
            int x = rng.Next(S), y = rng.Next(S);
            var c = neon[rng.Next(neon.Length)];
            switch (rng.Next(4))
            {
                case 0:
                    Put(x, y, c); Put(x + 1, y, c); Put(x, y + 1, c); Put(x + 1, y + 1, c);
                    break;
                case 1:
                    for (int i = 0; i < 5; i++) Put(x + i, y + i, c);
                    break;
                case 2:
                    for (int i = 0; i < 8; i++) Put(x + i, y + ((i / 2) % 2 == 0 ? i % 2 : 1 - i % 2), c);
                    break;
                default:
                    Put(x + 1, y, c); Put(x, y + 1, c); Put(x + 2, y + 1, c); Put(x + 1, y + 2, c);
                    break;
            }
        }

        var t = new Texture2D(S, S, TextureFormat.RGBA32, false);
        t.SetPixels(px);
        t.filterMode = FilterMode.Point;
        t.wrapMode = TextureWrapMode.Repeat;
        t.name = "ArcadeCarpet";
        t.Apply();
        return t;
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


        var lamp = Group("CeilingLamp", new Vector3(0f, 2.6f, -1.3f), room).gameObject.AddComponent<Light>();
        lamp.type = LightType.Point;
        lamp.color = new Color(1f, 0.85f, 0.7f);
        lamp.intensity = 1.2f;
        lamp.range = 4.5f;

        SetupArcadeHall(room);
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
        // Salonu elle düzenlediysen, silmeden önce düzenini kaydet; yeni kurulumda aynen geri gelir.
        CaptureHallLayout();
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

            EditorGUILayout.Space(4f);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Salon Düzenini Kaydet"))
                {
                    Undo.RecordObject(b, "Salon düzenini kaydet");
                    if (!b.CaptureHallLayout()) Debug.LogWarning("ArcadeRoom/ArcadeHall bulunamadı, önce sahneyi kur.");
                    EditorUtility.SetDirty(b);
                }
                using (new EditorGUI.DisabledScope(!b.HasHallLayout))
                    if (GUILayout.Button("Varsayılan Salona Dön"))
                    {
                        Undo.RecordObject(b, "Salon düzenini sıfırla");
                        b.ClearHallLayout();
                        EditorUtility.SetDirty(b);
                    }
            }
        }

        EditorGUILayout.HelpBox(b.HasHallLayout
            ? "Kayıtlı salon düzeni var (" + b.HallLayoutCount + " obje). Sahneyi Kur, salonu bu düzende kurar."
            : "Salonu elle düzenleyebilirsin: Sahneyi Kur'a bastığında düzenin otomatik kaydedilir ve aynen geri gelir.",
            MessageType.None);

        EditorGUILayout.HelpBox(b.IsBuilt
            ? "Sahne kurulu. Objeler Hierarchy'de 'Arcade' altında. Yeniden Kur, salon (ArcadeHall) dışındaki objelere elle yaptığın değişiklikleri siler."
            : "Sahneyi Kur'a bas: kabin, arena, dövüşçüler ve arayüz Hierarchy'ye eklenir. Kurmadan Play'e basarsan her şey Play sırasında oluşturulur.",
            MessageType.Info);
    }
}
#endif