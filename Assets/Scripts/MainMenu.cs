using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Ana menü: kamera atari salonunu geniş açıdan gösterir, ekranda PLAY ve QUIT var.
/// PLAY'e basınca kamera bizim kabinin ekranına zoom yapar ve oyun başlar. QUIT oyundan çıkar.
/// </summary>
public class MainMenu : MonoBehaviour
{
    /// <summary>Menü/zoom sürerken true. MatchManager bu sırada oyun girdisini yok sayar.</summary>
    public static bool Active { get; private set; }

    #region Ayarlar

    [Tooltip("Kapalıysa oyun doğrudan kabin ekranından başlar (oyun sahnesi böyle olmalı).")]
    public bool showMainMenu = true;

    [Tooltip("Boş değilse: PLAY'de zoom yapılırken bu sahne arka planda yüklenir ve zoom bitince geçilir " +
             "(MainMenu sahnesi). Boşsa zoom bittikten sonra aynı sahnede oyun başlar.")]
    public string gameScene = "";

    // Menü sahnesinden gelindiyse oyun sahnesi menüyü tekrar göstermez
    static bool cameFromMenu;
    // Oyundan ESC ile dönülüyorsa menü sahnesi kabinden geriye çekilerek açılır
    static bool returning;

    [Header("Menü kamerası")]
    public Vector3 menuCameraPosition = new Vector3(0f, 2.05f, -5.55f);
    public Vector3 menuLookAt = new Vector3(0f, 1.25f, 0f);
    public float menuFov = 58f;
    [Tooltip("Menüdeyken kameranın hafif süzülme miktarı.")]
    public float driftAmount = 0.35f;

    [Header("Fare ile bakma")]
    [Tooltip("Menüde kamera fareyi (ya da kolun sağ analoğunu) takip ederek salonun içinde döner.")]
    public bool mouseLook = true;
    [Tooltip("Fare ekranın sağ/sol kenarındayken kameranın dönme açısı (derece).")]
    public float lookYaw = 30f;
    [Tooltip("Fare ekranın üst/alt kenarındayken kameranın dönme açısı (derece).")]
    public float lookPitch = 12f;
    [Tooltip("Kameranın baktığı yöne doğru hafif kayma miktarı (m).")]
    public float lookShift = 0.25f;
    [Tooltip("Takip hızı. Düşük = daha yavaş ve süzülerek.")]
    public float lookSmoothing = 3.5f;

    [Header("Geçiş")]
    [Tooltip("Jeton atıldıktan sonra kameranın kabin ekranına yükselme süresi (sn).")]
    public float screenZoomDuration = 3f;
    [Tooltip("ESC ile ana menüye dönerken kameranın kabinden geriye çekilme süresi (sn).")]
    public float zoomOutDuration = 1.8f;

    [Header("Jeton çekimi")]
    [Tooltip("PLAY'den sonra kameranın jeton deliğine yaklaşma süresi (sn).")]
    public float coinShotIn = 1.3f;
    [Tooltip("Jeton atılırken kameranın jeton deliğinde bekleme süresi (sn).")]
    public float coinShotHold = 1.1f;
    [Tooltip("Kameranın jeton deliğine uzaklığı (m).")]
    public float coinShotDistance = 1.25f;
    public float coinShotFov = 42f;

    #endregion

    #region Durum

    enum State { Menu, CoinShot, Zooming, Loading, Done, ZoomOut }
    State state = State.Done;

    Camera cam;
    CameraSway sway;
    Vector3 gamePos, startPos, coinPos;
    Quaternion gameRot, startRot, coinRot;
    float gameFov, startFov, zoomT, menuTime;
    Vector2 look;

    GameObject ui;
    CanvasGroup uiGroup;
    RectTransform[] buttons;
    Image[] frames, fills;
    Text[] labels;
    int selected;
    AsyncOperation loadOp;

    static readonly string[] Labels = { "PLAY", "QUIT" };
    static readonly Color[] Neon = { new Color(0.25f, 0.95f, 1f), new Color(1f, 0.35f, 0.75f) };

    #endregion

    #region Başlangıç

    void Awake()
    {
        if (cameFromMenu) { cameFromMenu = false; showMainMenu = false; }
        cam = Camera.main;
        if (!showMainMenu || cam == null) { Active = false; state = State.Done; return; }

        // Oyun kamerasının yerini hatırla (zoom buraya biter)
        gamePos = cam.transform.position;
        gameRot = cam.transform.rotation;
        gameFov = cam.fieldOfView;

        sway = cam.GetComponent<CameraSway>();
        if (sway != null) sway.enabled = false;   // menüde kamerayı biz yönetiyoruz

        Active = true;
        BuildUI();
        if (returning)
        {
            returning = false;
            BeginZoomOut();
        }
        else
        {
            state = State.Menu;
            ApplyMenuPose(0f);
        }
    }

    public static void ReturnToMenu()
    {
        if (Active) return;
        Time.timeScale = 1f;
        const string menuScene = "MainMenu";
        if (SceneManager.GetActiveScene().name != menuScene && Application.CanStreamedLevelBeLoaded(menuScene))
        {
            returning = true;
            SceneManager.LoadScene(menuScene);
            return;
        }
        var mm = Object.FindAnyObjectByType<MainMenu>();
        if (mm != null) mm.ReopenHere();
    }

    void ReopenHere()
    {
        cam = Camera.main;
        if (cam == null) return;
        gamePos = cam.transform.position;
        gameRot = cam.transform.rotation;
        gameFov = cam.fieldOfView;
        sway = cam.GetComponent<CameraSway>();
        if (sway != null) sway.enabled = false;
        loadOp = null;
        Active = true;
        if (ui == null) BuildUI();
        BeginZoomOut();
    }

    void BeginZoomOut()
    {
        startPos = cam.transform.position;
        startRot = cam.transform.rotation;
        startFov = cam.fieldOfView;
        zoomT = 0f;
        menuTime = 0f;
        look = Vector2.zero;
        selected = 0;
        if (uiGroup != null) uiGroup.alpha = 0f;
        state = State.ZoomOut;
    }

    void OnDestroy()
    {
        if (state != State.Done) Active = false;
    }

    #endregion

    #region Döngü

    void Update()
    {
        if (state == State.Menu)
        {
            menuTime += Time.unscaledDeltaTime;
            ApplyMenuPose(menuTime);
            HandleMenuInput();
            AnimateButtons();
        }
        else if (state == State.CoinShot)
        {
            zoomT += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(zoomT / Mathf.Max(0.1f, coinShotIn));
            float e = Smoother(k);
            cam.transform.SetPositionAndRotation(Vector3.Lerp(startPos, coinPos, e), Quaternion.Slerp(startRot, coinRot, e));
            cam.fieldOfView = Mathf.Lerp(startFov, coinShotFov, e);
            if (uiGroup != null) uiGroup.alpha = Mathf.Clamp01(1f - k * 3f);

            if (zoomT >= coinShotIn + coinShotHold)
            {
                startPos = cam.transform.position;
                startRot = cam.transform.rotation;
                startFov = cam.fieldOfView;
                zoomT = 0f;
                state = State.Zooming;
            }
        }
        else if (state == State.ZoomOut)
        {
            zoomT += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(zoomT / Mathf.Max(0.1f, zoomOutDuration));
            float e = Smoother(k);
            Vector3 target = menuCameraPosition;
            cam.transform.SetPositionAndRotation(Vector3.Lerp(startPos, target, e), Quaternion.Slerp(startRot, Quaternion.LookRotation(menuLookAt - target), e));
            cam.fieldOfView = Mathf.Lerp(startFov, menuFov, e);
            if (uiGroup != null) uiGroup.alpha = Mathf.Clamp01((k - 0.55f) / 0.45f);
            AnimateButtons();
            if (k >= 1f)
            {
                menuTime = 0f;
                state = State.Menu;
            }
        }
        else if (state == State.Zooming)
        {
            zoomT += Time.unscaledDeltaTime / Mathf.Max(0.1f, screenZoomDuration);
            float k = Mathf.Clamp01(zoomT);
            float e = k * k * k * (k * (k * 6f - 15f) + 10f);   // smootherstep: yavaş başla, hızlan, yumuşak dur

            Vector3 pos = Vector3.Lerp(startPos, gamePos, e) + Vector3.up * Mathf.Sin(Mathf.PI * e) * 0.12f;
            cam.transform.SetPositionAndRotation(pos, Quaternion.Slerp(startRot, gameRot, e));
            cam.fieldOfView = Mathf.Lerp(startFov, gameFov, e);

            if (uiGroup != null) uiGroup.alpha = Mathf.Clamp01(1f - k * 4f);

            if (k >= 1f) FinishZoom();
        }
    }

    void ApplyMenuPose(float t)
    {
        Vector3 drift = new Vector3(Mathf.Sin(t * 0.17f) * driftAmount, Mathf.Sin(t * 0.23f) * driftAmount * 0.15f, Mathf.Sin(t * 0.11f) * driftAmount * 0.3f);
        Vector3 pos = menuCameraPosition + drift;
        Quaternion baseRot = Quaternion.LookRotation(menuLookAt - pos);

        if (mouseLook && t > 0f)
        {
            Vector2 aim = LookInput();
            Vector2 want = new Vector2(aim.x * lookYaw, -aim.y * lookPitch);
            look = Vector2.Lerp(look, want, 1f - Mathf.Exp(-lookSmoothing * Time.unscaledDeltaTime));
        }

        float yawK = lookYaw > 0f ? look.x / lookYaw : 0f;
        pos += (baseRot * Vector3.right) * (yawK * lookShift);
        Quaternion rot = Quaternion.AngleAxis(look.x, Vector3.up) * baseRot * Quaternion.AngleAxis(look.y, Vector3.right);
        cam.transform.SetPositionAndRotation(pos, rot);
        cam.fieldOfView = menuFov;
    }

    static Vector2 LookInput()
    {
        var gp = Gamepad.current;
        if (gp != null)
        {
            Vector2 stick = gp.rightStick.ReadValue();
            if (stick.sqrMagnitude > 0.02f) return Vector2.ClampMagnitude(stick, 1f);
        }
        var mouse = Mouse.current;
        if (mouse == null || Screen.width <= 0 || Screen.height <= 0) return Vector2.zero;
        Vector2 mp = mouse.position.ReadValue();
        float nx = Mathf.Clamp(mp.x / Screen.width * 2f - 1f, -1f, 1f);
        float ny = Mathf.Clamp(mp.y / Screen.height * 2f - 1f, -1f, 1f);
        return new Vector2(nx, ny);
    }

    static float Smoother(float k) { return k * k * k * (k * (k * 6f - 15f) + 10f); }

    void StartZoom()
    {
        startPos = cam.transform.position;
        startRot = cam.transform.rotation;
        startFov = cam.fieldOfView;
        zoomT = 0f;

        if (InsertCoin.FindSlot(transform, out var slot))
        {
            Vector3 fwd = slot.parent != null ? slot.parent.forward : Vector3.forward;
            coinPos = slot.position - fwd * coinShotDistance + Vector3.up * 0.32f;
            coinRot = Quaternion.LookRotation(slot.position + Vector3.up * 0.04f - coinPos);
            state = State.CoinShot;
            InsertCoin.Play(transform, coinShotIn * 0.8f);
            return;
        }

        InsertCoin.Play(transform);
        state = State.Zooming;
    }

    void FinishZoom()
    {
        cam.transform.SetPositionAndRotation(gamePos, gameRot);
        cam.fieldOfView = gameFov;

        if (loadOp != null)
        {
            // Oyun sahnesi arkada yüklendi: kamera artık tam kabinde, iki sahnede de aynı görüntü -> geç
            if (uiGroup != null) uiGroup.alpha = 0f;
            state = State.Loading;
            cameFromMenu = true;
            loadOp.allowSceneActivation = true;
            return;
        }

        if (sway != null) sway.enabled = true;   // Start'ı şimdi çalışır, doğru yeri baz alır
        if (ui != null) Destroy(ui);
        state = State.Done;
        Active = false;
    }

    #endregion

    #region Girdi

    void HandleMenuInput()
    {
        int before = selected;

        var mouse = Mouse.current;
        if (mouse != null)
        {
            Vector2 mp = mouse.position.ReadValue();
            for (int i = 0; i < buttons.Length; i++)
            {
                if (!RectTransformUtility.RectangleContainsScreenPoint(buttons[i], mp, null)) continue;
                selected = i;
                if (mouse.leftButton.wasPressedThisFrame) { Activate(i); return; }
            }
        }

        var kb = Keyboard.current;
        if (kb != null)
        {
            if (kb.upArrowKey.wasPressedThisFrame || kb.wKey.wasPressedThisFrame) selected = (selected + Labels.Length - 1) % Labels.Length;
            if (kb.downArrowKey.wasPressedThisFrame || kb.sKey.wasPressedThisFrame) selected = (selected + 1) % Labels.Length;
            if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame ||
                kb.fKey.wasPressedThisFrame || kb.kKey.wasPressedThisFrame)
            { Activate(selected); return; }
        }

        var gp = Gamepad.current;
        if (gp != null)
        {
            if (gp.dpad.up.wasPressedThisFrame || gp.leftStick.up.wasPressedThisFrame) selected = (selected + Labels.Length - 1) % Labels.Length;
            if (gp.dpad.down.wasPressedThisFrame || gp.leftStick.down.wasPressedThisFrame) selected = (selected + 1) % Labels.Length;
            if (gp.buttonSouth.wasPressedThisFrame || gp.startButton.wasPressedThisFrame) { Activate(selected); return; }
        }

        if (selected != before) FightFX.I?.PlayMenuMove();
    }

    void Activate(int i)
    {
        if (i == 0)
        {
            FightFX.I?.PlayMenuSelect();
            if (!string.IsNullOrEmpty(gameScene) && Application.CanStreamedLevelBeLoaded(gameScene))
            {
                loadOp = SceneManager.LoadSceneAsync(gameScene);
                loadOp.allowSceneActivation = false;   // zoom bitene kadar beklesin
            }
            else if (!string.IsNullOrEmpty(gameScene))
                Debug.LogWarning("'" + gameScene + "' sahnesi Build Settings'te yok; oyun bu sahnede başlıyor. Color Fighter > Main Menu Sahnesini Kur ile düzelt.");
            StartZoom();
        }
        else
        {
            FightFX.I?.PlayMenuSelect();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }

    #endregion

    #region Arayüz

    void BuildUI()
    {
        ui = new GameObject("MainMenuUI", typeof(RectTransform));
        var canvas = ui.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        var scaler = ui.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        uiGroup = ui.AddComponent<CanvasGroup>();

        var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        var root = (RectTransform)ui.transform;

        buttons = new RectTransform[Labels.Length];
        frames = new Image[Labels.Length];
        fills = new Image[Labels.Length];
        labels = new Text[Labels.Length];
        for (int i = 0; i < Labels.Length; i++)
        {
            var b = NewRect("Button" + Labels[i], root, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 300f - i * 130f), new Vector2(440f, 104f));
            buttons[i] = b;
            frames[i] = b.gameObject.AddComponent<Image>();
            frames[i].raycastTarget = false;

            var fill = NewRect("Fill", b, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            fill.offsetMin = new Vector2(6f, 6f);
            fill.offsetMax = new Vector2(-6f, -6f);
            fills[i] = fill.gameObject.AddComponent<Image>();
            fills[i].raycastTarget = false;

            var lr = NewRect("Label", b, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var t = lr.gameObject.AddComponent<Text>();
            t.font = font;
            t.text = Labels[i];
            t.fontSize = 60;
            t.fontStyle = FontStyle.BoldAndItalic;
            t.alignment = TextAnchor.MiddleCenter;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            var o = lr.gameObject.AddComponent<Outline>();
            o.effectColor = new Color(0f, 0f, 0f, 0.85f);
            o.effectDistance = new Vector2(3f, -3f);
            labels[i] = t;
        }
        AnimateButtons();
    }

    void AnimateButtons()
    {
        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 5f);
        for (int i = 0; i < buttons.Length; i++)
        {
            bool sel = i == selected;
            Color n = Neon[i];
            frames[i].color = sel ? Color.Lerp(n, Color.white, 0.25f * pulse) : new Color(n.r, n.g, n.b, 0.45f);
            fills[i].color = sel ? new Color(n.r * 0.25f, n.g * 0.25f, n.b * 0.25f, 0.92f) : new Color(0.03f, 0.02f, 0.08f, 0.82f);
            labels[i].color = sel ? Color.white : new Color(0.75f, 0.75f, 0.8f);
            float s = sel ? 1.06f + 0.02f * pulse : 1f;
            buttons[i].localScale = Vector3.Lerp(buttons[i].localScale, Vector3.one * s, 1f - Mathf.Exp(-14f * Time.unscaledDeltaTime));
        }
    }

    static RectTransform NewRect(string name, RectTransform parent, Vector2 aMin, Vector2 aMax, Vector2 pos, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var r = (RectTransform)go.transform;
        r.SetParent(parent, false);
        r.anchorMin = aMin;
        r.anchorMax = aMax;
        r.anchoredPosition = pos;
        r.sizeDelta = size;
        return r;
    }

    #endregion
}

#if UNITY_EDITOR
/// <summary>Üst menü: Color Fighter > Main Menu Sahnesini Kur</summary>
public static class MainMenuSceneBuilder
{
    const string MenuScenePath = "Assets/Scenes/MainMenu.unity";
    const string DefaultGamePath = "Assets/Scenes/SampleScene.unity";

    [UnityEditor.MenuItem("Color Fighter/Main Menu Sahnesini Kur")]
    static void Build()
    {
        if (Application.isPlaying) return;
        if (!UnityEditor.SceneManagement.EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        // Oyun sahnesi: açık sahne (menü sahnesi değilse) ya da SampleScene
        string gamePath = SceneManager.GetActiveScene().path;
        if (string.IsNullOrEmpty(gamePath) || gamePath == MenuScenePath) gamePath = DefaultGamePath;
        if (UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEditor.SceneAsset>(gamePath) == null)
        {
            UnityEditor.EditorUtility.DisplayDialog("Color Fighter", "Oyun sahnesi bulunamadı: " + gamePath, "Tamam");
            return;
        }

        // 1) Oyun sahnesini hazırla: kurulu olsun, menü kapalı (doğrudan kabinden başlar)
        var game = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(gamePath);
        var boot = Object.FindAnyObjectByType<ArcadeBootstrap>();
        if (boot == null) boot = new GameObject("Arcade").AddComponent<ArcadeBootstrap>();
        if (!boot.IsBuilt) boot.BuildInEditor();
        else boot.CaptureHallLayout();   // elle düzenlenmiş salon kaydı güncel olsun
        var mm = boot.GetComponent<MainMenu>();
        if (mm == null) mm = boot.gameObject.AddComponent<MainMenu>();
        mm.showMainMenu = false;
        mm.gameScene = "";
        UnityEditor.EditorUtility.SetDirty(mm);
        UnityEditor.EditorUtility.SetDirty(boot);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(game);
        UnityEditor.SceneManagement.EditorSceneManager.SaveScene(game);

        // 2) Menü sahnesi = oyun sahnesinin birebir kopyası (aynı salon, ışık, kabin) + menü açık
        if (UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEditor.SceneAsset>(MenuScenePath) != null)
            UnityEditor.AssetDatabase.DeleteAsset(MenuScenePath);
        UnityEditor.AssetDatabase.CopyAsset(gamePath, MenuScenePath);
        var menu = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(MenuScenePath);
        var mboot = Object.FindAnyObjectByType<ArcadeBootstrap>();
        var mmm = mboot.GetComponent<MainMenu>();
        mmm.showMainMenu = true;
        mmm.gameScene = System.IO.Path.GetFileNameWithoutExtension(gamePath);
        UnityEditor.EditorUtility.SetDirty(mmm);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(menu);
        UnityEditor.SceneManagement.EditorSceneManager.SaveScene(menu);

        // 3) Build Settings: önce menü, sonra oyun
        var list = new System.Collections.Generic.List<UnityEditor.EditorBuildSettingsScene>
        {
            new UnityEditor.EditorBuildSettingsScene(MenuScenePath, true),
            new UnityEditor.EditorBuildSettingsScene(gamePath, true),
        };
        foreach (var sc in UnityEditor.EditorBuildSettings.scenes)
            if (sc.path != MenuScenePath && sc.path != gamePath) list.Add(sc);
        UnityEditor.EditorBuildSettings.scenes = list.ToArray();

        UnityEditor.EditorUtility.DisplayDialog("Color Fighter",
            "MainMenu sahnesi kuruldu ve açıldı.\n\n" +
            "• Play'e bu sahnede basarsan: salon + PLAY/QUIT, PLAY'de kabine zoom ve oyun sahnesine geçiş.\n" +
            "• " + System.IO.Path.GetFileNameWithoutExtension(gamePath) + " artık doğrudan kabin ekranından başlar.\n" +
            "• Build Settings: MainMenu (0), " + System.IO.Path.GetFileNameWithoutExtension(gamePath) + " (1).\n\n" +
            "Salonu değiştirirsen bu menüden tekrar kurman yeterli.", "Tamam");
    }
}
#endif