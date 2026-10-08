using UnityEngine;

public class FighterRig : MonoBehaviour
{
    #region Ayarlar

    public Fighter fighter;
    [Tooltip("Pozların güncellenme hızı. Düşürürsen daha 'stop-motion' görünür. Vuruş anları her zaman anında oturur.")]
    public float poseFps = 20f;

    [SerializeField, HideInInspector] Transform root, hips, torso, head, lSh, lEl, rSh, rEl, lHip, lKn, rHip, rKn;

    #endregion

    #region Poz tablosu: kalçaY, gövde, kafa, Lomuz, Ldirsek, Romuz, Rdirsek, Lkalça, Ldiz, Rkalça, Rdiz, kalçaEğimi

    const int HY = 0, TO = 1, HE = 2, LS = 3, LE = 4, RS = 5, RE = 6, LH = 7, LK = 8, RH = 9, RK = 10, HP = 11, N = 12;

    static readonly float[] IDLE = { 0.92f, 8, -6, -55, -95, -30, -115, -28, 32, 20, 28, 0 };
    static readonly float[] CROUCH = { 0.53f, 22, -15, -60, -100, -35, -115, -75, 115, -10, 85, 0 };
    static readonly float[] GUARD = { 0.90f, -4, 12, -80, -125, -70, -130, -22, 28, 22, 24, 0 };
    static readonly float[] CROUCH_GUARD = { 0.53f, 10, 10, -80, -125, -70, -130, -75, 115, -10, 85, 0 };
    static readonly float[] JUMP = { 0.95f, 10, -5, -60, -100, -40, -110, -80, 120, -60, 120, 0 };
    static readonly float[] PUNCH_WIND = { 0.90f, 2, -6, -55, -95, 15, -125, -28, 32, 20, 28, 0 };
    static readonly float[] PUNCH_HIT = { 0.88f, 22, -12, -50, -95, -88, -5, -40, 30, 30, 15, 0 };
    static readonly float[] KICK_WIND = { 0.95f, -8, -5, -45, -95, -20, -110, -15, 20, -75, 110, 0 };
    static readonly float[] KICK_HIT = { 0.95f, -18, 5, -35, -90, 10, -100, 5, 15, -98, 0, 0 };
    static readonly float[] CPUNCH_WIND = { 0.53f, 15, -15, -60, -100, 10, -125, -75, 115, -10, 85, 0 };
    static readonly float[] CPUNCH_HIT = { 0.53f, 30, -20, -55, -100, -85, -5, -75, 115, -10, 85, 0 };
    static readonly float[] SWEEP_WIND = { 0.50f, 25, -15, -60, -100, -35, -110, -70, 120, -40, 100, 0 };
    static readonly float[] SWEEP_HIT = { 0.40f, 35, -20, -70, -100, -40, -110, -60, 120, -85, 0, 0 };
    static readonly float[] APUNCH_WIND = { 0.95f, 5, -5, -60, -100, 20, -120, -80, 120, -60, 120, 0 };
    static readonly float[] APUNCH_HIT = { 0.95f, 25, -15, -60, -100, -60, -5, -80, 120, -60, 120, 0 };
    static readonly float[] AKICK_WIND = { 0.95f, 5, -5, -60, -100, -40, -110, -80, 120, -80, 120, 0 };
    static readonly float[] AKICK_HIT = { 0.95f, -12, 0, -60, -100, -40, -110, -80, 120, -70, 0, 0 };
    static readonly float[] HIT = { 0.90f, -25, -30, 20, -60, 30, -50, -20, 25, 25, 20, 0 };
    static readonly float[] CROUCH_HIT = { 0.53f, -10, -25, 20, -60, 30, -50, -75, 115, -10, 85, 0 };
    static readonly float[] AIR_HIT = { 0.95f, -40, -30, -150, -20, -160, -10, -40, 20, 30, 40, -20 };
    static readonly float[] DOWN = { 0.14f, 0, 10, -20, -20, -10, -30, -10, 20, 10, 30, -90 };
    static readonly float[] WIN = { 0.95f, -5, -15, -40, -100, -170, -10, -15, 10, 15, 10, 0 };

    // Kombo saldırısı (Color Rush) pozları
    static readonly float[] RUSHA_WIND = { 0.86f, 10, -8, 10, -120, -40, -110, -40, 45, 25, 30, 0 };
    static readonly float[] RUSHA_HIT = { 0.82f, 30, -14, -95, -8, -25, -125, -50, 45, 40, 20, 0 };
    static readonly float[] RUSHB_WIND = { 0.95f, -10, -5, -60, -100, -40, -110, -80, 110, -15, 20, 0 };
    static readonly float[] RUSHB_HIT = { 1.00f, -25, 8, -20, -60, 20, -60, -118, 0, 10, 10, -10 };
    static readonly float[] FIN_WIND = { 0.50f, 30, -15, -60, -110, 30, -140, -70, 110, -20, 90, 0 };
    static readonly float[] FIN_HIT = { 1.00f, -10, 15, -40, -100, -175, -5, -40, 60, 10, 20, 0 };

    #endregion

    #region Çalışma durumu

    readonly float[] cur = new float[N];
    readonly float[] tgt = new float[N];
    FState lastState;
    MoveData lastMove;
    int lastPhase = -1;
    float stepTimer;
    System.Func<Color, Material> materialFor;

    static readonly string[] MainPartNames = { "Chest", "Headband", "HeadbandTail", "UpperArm" };
    static readonly string[] PantsPartNames = { "Pelvis", "Thigh", "Shin" };
    Renderer[] mainParts, pantsParts;
    Color mainBase, pantsBase;
    bool partsReady;
    float lastGlow = -1f;
    int lastBeat = -1;
    float celebY, celebSpin;

    // Aksesuarlar (renge özel)
    readonly System.Collections.Generic.List<GameObject> accParts = new System.Collections.Generic.List<GameObject>();
    readonly System.Collections.Generic.List<Flow> flows = new System.Collections.Generic.List<Flow>();
    static readonly System.Collections.Generic.Dictionary<Color32, Material> accMats = new System.Collections.Generic.Dictionary<Color32, Material>();
    int accStyle = -1;
    float lastX, swayFwd, swayUp;

    /// <summary>Savrulan parça: hıza göre geriye kalkar, dururken hafifçe dalgalanır.</summary>
    class Flow
    {
        public Transform pivot;
        public float rest, gain, flutter, phase;
    }

    void Awake()
    {
        System.Array.Copy(IDLE, cur, N);
    }

    #endregion

    #region Kurulum

    public void Build(Color main, Color skin, System.Func<Color, Material> materialFactory)
    {
        materialFor = materialFactory;
        Color pants = new Color(main.r * 0.45f, main.g * 0.45f, main.b * 0.45f, 1f);
        Color glove = new Color(0.95f, 0.95f, 0.9f);
        Color dark = new Color(0.08f, 0.08f, 0.1f);

        root = new GameObject("Rig").transform;
        root.SetParent(transform, false);
        root.localRotation = Quaternion.Euler(0f, 90f, 0f);

        hips = Joint("Hips", root, new Vector3(0f, 0.92f, 0f));
        Box(hips, "Pelvis", new Vector3(0f, 0f, 0f), new Vector3(0.4f, 0.2f, 0.24f), pants);

        torso = Joint("Torso", hips, new Vector3(0f, 0.05f, 0f));
        Box(torso, "Belt", new Vector3(0f, 0.08f, 0f), new Vector3(0.42f, 0.1f, 0.25f), dark);
        Box(torso, "Chest", new Vector3(0f, 0.36f, 0f), new Vector3(0.48f, 0.46f, 0.26f), main);

        head = Joint("Head", torso, new Vector3(0f, 0.6f, 0f));
        Box(head, "Face", new Vector3(0f, 0.14f, 0f), new Vector3(0.22f, 0.26f, 0.24f), skin);
        Box(head, "Headband", new Vector3(0f, 0.21f, 0f), new Vector3(0.24f, 0.06f, 0.26f), main);
        Box(head, "HeadbandTail", new Vector3(0f, 0.2f, -0.18f), new Vector3(0.04f, 0.05f, 0.14f), main);
        Box(head, "EyeR", new Vector3(0.055f, 0.15f, 0.12f), new Vector3(0.035f, 0.035f, 0.01f), dark);
        Box(head, "EyeL", new Vector3(-0.055f, 0.15f, 0.12f), new Vector3(0.035f, 0.035f, 0.01f), dark);

        lSh = Joint("ShoulderL", torso, new Vector3(-0.31f, 0.52f, 0f));
        lEl = BuildArm(lSh, main, skin, glove);
        rSh = Joint("ShoulderR", torso, new Vector3(0.31f, 0.52f, 0f));
        rEl = BuildArm(rSh, main, skin, glove);

        lHip = Joint("HipL", hips, new Vector3(-0.12f, -0.05f, 0f));
        lKn = BuildLeg(lHip, pants, dark);
        rHip = Joint("HipR", hips, new Vector3(0.12f, -0.05f, 0f));
        rKn = BuildLeg(rHip, pants, dark);

        System.Array.Copy(IDLE, cur, N);
        Apply();
    }

    /// <summary>Oyun sırasında dövüşçünün rengini değiştirir (karakter seçimi). Asset materyallerine dokunmaz.</summary>
    public void Recolor(Color main)
    {
        if (!Application.isPlaying) return;
        EnsureParts();
        mainBase = main;
        pantsBase = new Color(main.r * 0.45f, main.g * 0.45f, main.b * 0.45f, 1f);
        lastGlow = -1f;
        ApplyTint(0f);
    }

    void EnsureParts()
    {
        if (partsReady || root == null) return;
        var main = new System.Collections.Generic.List<Renderer>();
        var pants = new System.Collections.Generic.List<Renderer>();
        foreach (var r in root.GetComponentsInChildren<Renderer>(true))
        {
            if (System.Array.IndexOf(MainPartNames, r.name) >= 0) main.Add(r);
            else if (System.Array.IndexOf(PantsPartNames, r.name) >= 0) pants.Add(r);
        }
        mainParts = main.ToArray();
        pantsParts = pants.ToArray();
        mainBase = mainParts.Length > 0 && mainParts[0].sharedMaterial != null ? mainParts[0].sharedMaterial.color : Color.white;
        pantsBase = pantsParts.Length > 0 && pantsParts[0].sharedMaterial != null ? pantsParts[0].sharedMaterial.color : Color.gray;
        partsReady = true;
    }

    void ApplyTint(float glow)
    {
        // renderer.material her renderer için bir kopya üretir; asset dosyası değişmez.
        Color m = Color.Lerp(mainBase, Color.white, glow);
        Color p = Color.Lerp(pantsBase, Color.white, glow * 0.5f);
        foreach (var r in mainParts) if (r != null) r.material.color = m;
        foreach (var r in pantsParts) if (r != null) r.material.color = p;
    }

    void UpdateGlow()
    {
        float glow = 0f;
        if (fighter.InComboRush) glow = 0.4f + 0.25f * Mathf.Sin(Time.unscaledTime * 40f);
        else if (fighter.ComboReady) glow = 0.2f + 0.15f * Mathf.Sin(Time.unscaledTime * 9f);
        if (glow <= 0f && lastGlow <= 0f) return;
        if (Mathf.Abs(glow - lastGlow) < 0.01f) return;
        EnsureParts();
        ApplyTint(glow);
        lastGlow = glow;
    }

    /// <summary>Renk + o renge özel aksesuar (seçim ekranı ve maç başında çağrılır).</summary>
    public void SetLook(int style, Color main)
    {
        Recolor(main);
        SetAccessory(style, main);
    }

    public void SetAccessory(int style, Color main)
    {
        if (!Application.isPlaying || root == null) return;
        style = ((style % 8) + 8) % 8;
        foreach (var go in accParts) if (go != null) Destroy(go);
        accParts.Clear();
        flows.Clear();
        accStyle = style;

        Color dark = new Color(0.08f, 0.08f, 0.1f);
        Color white = new Color(0.95f, 0.95f, 0.92f);
        Color light = Color.Lerp(main, Color.white, 0.25f);
        Color deep = new Color(main.r * 0.5f, main.g * 0.5f, main.b * 0.5f);

        switch (style)
        {
            case 0: // RED: uçuşan bandana uçları + el sargıları
                for (int i = 0; i < 2; i++)
                {
                    var pv = AccPivot(head, new Vector3(i == 0 ? -0.03f : 0.03f, 0.21f, -0.12f), -30f - i * 12f, 55f, 7f, i * 1.7f);
                    AccBox(pv, new Vector3(0f, 0f, -0.2f), new Vector3(0.045f, 0.03f, 0.4f - i * 0.08f), light);
                }
                foreach (var el in new[] { lEl, rEl })
                {
                    AccBox(el, new Vector3(0f, -0.2f, 0f), new Vector3(0.135f, 0.05f, 0.135f), white);
                    AccBox(el, new Vector3(0f, -0.08f, 0f), new Vector3(0.135f, 0.05f, 0.135f), white);
                }
                break;

            case 1: // BLUE: büyük boks eldivenleri + kask
                foreach (var el in new[] { lEl, rEl })
                {
                    AccBox(el, new Vector3(0f, -0.34f, 0.01f), new Vector3(0.23f, 0.21f, 0.24f), light);
                    AccBox(el, new Vector3(0f, -0.22f, 0f), new Vector3(0.19f, 0.05f, 0.19f), white);
                }
                AccBox(head, new Vector3(-0.122f, 0.13f, 0f), new Vector3(0.035f, 0.17f, 0.2f), deep);
                AccBox(head, new Vector3(0.122f, 0.13f, 0f), new Vector3(0.035f, 0.17f, 0.2f), deep);
                AccBox(head, new Vector3(0f, 0.285f, -0.01f), new Vector3(0.25f, 0.04f, 0.22f), deep);
                break;

            case 2: // GREEN: siyah kuşak + sarkan uçlar + tepe topuzu
                AccBox(torso, new Vector3(0f, 0.08f, 0f), new Vector3(0.45f, 0.09f, 0.275f), dark);
                for (int i = 0; i < 2; i++)
                {
                    var pv = AccPivot(torso, new Vector3(i == 0 ? -0.06f : 0.05f, 0.05f, 0.14f), 0f, -8f, 3f, i * 2.1f);
                    AccBox(pv, new Vector3(0f, -0.12f, 0.01f), new Vector3(0.05f, 0.24f, 0.02f), dark);
                }
                AccBox(head, new Vector3(0f, 0.31f, -0.05f), new Vector3(0.1f, 0.09f, 0.1f), dark);
                AccBox(head, new Vector3(0f, 0.27f, -0.05f), new Vector3(0.11f, 0.025f, 0.11f), light);
                break;

            case 3: // YELLOW: diken diken saç + dizlikler
                var hair = new Color(1f, 0.9f, 0.45f);
                for (int i = 0; i < 6; i++)
                {
                    float ax = (i % 3 - 1) * 28f, az = i < 3 ? -18f : 22f;
                    var sp = AccBox(head, new Vector3((i % 3 - 1) * 0.06f, 0.33f, i < 3 ? 0.03f : -0.06f), new Vector3(0.06f, 0.17f, 0.06f), hair);
                    sp.localRotation = Quaternion.Euler(az, 0f, -ax);
                }
                foreach (var kn in new[] { lKn, rKn })
                {
                    AccBox(kn, new Vector3(0f, -0.02f, 0.04f), new Vector3(0.19f, 0.13f, 0.18f), dark);
                    AccBox(kn, new Vector3(0f, -0.02f, 0.135f), new Vector3(0.12f, 0.03f, 0.01f), light);
                }
                break;

            case 4: // PURPLE: pelerin + ninja maskesi
                var cape = AccPivot(torso, new Vector3(0f, 0.58f, -0.15f), 6f, 45f, 5f, 0f);
                AccBox(cape, new Vector3(0f, -0.48f, 0f), new Vector3(0.48f, 0.96f, 0.03f), deep);
                AccBox(cape, new Vector3(0f, -0.48f, 0.017f), new Vector3(0.42f, 0.9f, 0.005f), light);
                AccBox(torso, new Vector3(0f, 0.58f, -0.13f), new Vector3(0.5f, 0.06f, 0.06f), dark);
                AccBox(head, new Vector3(0f, 0.07f, 0.005f), new Vector3(0.235f, 0.11f, 0.25f), new Color(0.12f, 0.1f, 0.16f));
                break;

            case 5: // ORANGE: zırh omuzluklar + altın şampiyonluk kemeri
                var metal = new Color(0.35f, 0.35f, 0.4f);
                foreach (var sh in new[] { lSh, rSh })
                {
                    AccBox(sh, new Vector3(0f, 0.03f, 0f), new Vector3(0.25f, 0.1f, 0.25f), metal);
                    AccBox(sh, new Vector3(0f, 0.085f, 0f), new Vector3(0.2f, 0.02f, 0.2f), light);
                }
                var gold = new Color(1f, 0.78f, 0.22f);
                AccBox(torso, new Vector3(0f, 0.08f, 0f), new Vector3(0.47f, 0.14f, 0.285f), gold);
                AccBox(torso, new Vector3(0f, 0.08f, 0.146f), new Vector3(0.17f, 0.13f, 0.02f), new Color(1f, 0.95f, 0.6f));
                AccBox(torso, new Vector3(0f, 0.08f, 0.158f), new Vector3(0.07f, 0.06f, 0.01f), main);
                break;

            case 6: // CYAN: parlayan vizör + kolluklar
                var glow = Color.Lerp(main, Color.white, 0.45f);
                AccBox(head, new Vector3(0f, 0.15f, 0.125f), new Vector3(0.25f, 0.06f, 0.03f), glow);
                AccBox(head, new Vector3(-0.125f, 0.15f, 0.02f), new Vector3(0.03f, 0.09f, 0.11f), dark);
                AccBox(head, new Vector3(0.125f, 0.15f, 0.02f), new Vector3(0.03f, 0.09f, 0.11f), dark);
                foreach (var el in new[] { lEl, rEl })
                {
                    AccBox(el, new Vector3(0f, -0.13f, 0f), new Vector3(0.145f, 0.2f, 0.145f), dark);
                    AccBox(el, new Vector3(0f, -0.1f, 0f), new Vector3(0.15f, 0.03f, 0.15f), glow);
                }
                break;

            default: // PINK: at kuyruğu + tozluklar
                var pinkHair = Color.Lerp(main, Color.white, 0.35f);
                AccBox(head, new Vector3(0f, 0.255f, -0.02f), new Vector3(0.245f, 0.06f, 0.24f), pinkHair);
                var tail = AccPivot(head, new Vector3(0f, 0.24f, -0.13f), -55f, 50f, 9f, 0.5f);
                AccBox(tail, new Vector3(0f, 0f, -0.12f), new Vector3(0.09f, 0.08f, 0.24f), pinkHair);
                AccBox(tail, new Vector3(0f, -0.01f, -0.27f), new Vector3(0.07f, 0.06f, 0.12f), pinkHair);
                AccBox(tail, new Vector3(0f, 0f, -0.01f), new Vector3(0.1f, 0.05f, 0.05f), white);
                foreach (var kn in new[] { lKn, rKn })
                {
                    AccBox(kn, new Vector3(0f, -0.27f, 0f), new Vector3(0.185f, 0.2f, 0.185f), white);
                    AccBox(kn, new Vector3(0f, -0.22f, 0f), new Vector3(0.19f, 0.035f, 0.19f), light);
                    AccBox(kn, new Vector3(0f, -0.32f, 0f), new Vector3(0.19f, 0.035f, 0.19f), light);
                }
                break;
        }
    }

    Transform AccPivot(Transform parent, Vector3 pos, float rest, float gain, float flutter, float phase)
    {
        var t = new GameObject("Acc_Pivot").transform;
        t.SetParent(parent, false);
        t.localPosition = pos;
        t.localRotation = Quaternion.Euler(rest, 0f, 0f);
        accParts.Add(t.gameObject);
        flows.Add(new Flow { pivot = t, rest = rest, gain = gain, flutter = flutter, phase = phase });
        return t;
    }

    Transform AccBox(Transform parent, Vector3 pos, Vector3 size, Color c)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = "Acc";
        ColorFighterUtil.Kill(go.GetComponent<Collider>());
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localScale = size;
        go.GetComponent<Renderer>().sharedMaterial = AccMaterial(c);
        accParts.Add(go);
        return go.transform;
    }

    Material AccMaterial(Color c)
    {
        Color32 key = c;
        if (accMats.TryGetValue(key, out var m) && m != null) return m;
        Shader sh = null;
        if (mainParts == null) EnsureParts();
        if (mainParts != null && mainParts.Length > 0 && mainParts[0] != null && mainParts[0].sharedMaterial != null)
            sh = mainParts[0].sharedMaterial.shader;
        if (sh == null) sh = Shader.Find("Universal Render Pipeline/Lit");
        if (sh == null) sh = Shader.Find("Standard");
        m = new Material(sh) { color = c };
        accMats[key] = m;
        return m;
    }

    void UpdateFlows()
    {
        if (flows.Count == 0) return;
        float dt = Mathf.Max(1e-4f, Time.deltaTime);
        float fwd = (fighter.X - lastX) / dt * fighter.Facing;
        lastX = fighter.X;
        bool air = fighter.Airborne;
        swayFwd = Mathf.Lerp(swayFwd, Mathf.Clamp(Mathf.Abs(fwd) / 4f, 0f, 1f), 1f - Mathf.Exp(-6f * dt));
        swayUp = Mathf.Lerp(swayUp, air ? 1f : 0f, 1f - Mathf.Exp(-5f * dt));
        float t = Time.time;
        foreach (var f in flows)
        {
            if (f.pivot == null) continue;
            float a = f.rest + f.gain * Mathf.Max(swayFwd, swayUp * 0.8f) + Mathf.Sin(t * 7f + f.phase) * f.flutter * (0.4f + swayFwd);
            f.pivot.localRotation = Quaternion.Euler(a, 0f, 0f);
        }
    }

    public void SetFacing(int facing)
    {
        if (root != null) root.localScale = new Vector3(1f, 1f, facing);
    }

    Transform BuildArm(Transform shoulder, Color sleeve, Color skin, Color glove)
    {
        Box(shoulder, "UpperArm", new Vector3(0f, -0.15f, 0f), new Vector3(0.15f, 0.32f, 0.15f), sleeve);
        var elbow = Joint("Elbow", shoulder, new Vector3(0f, -0.32f, 0f));
        Box(elbow, "Forearm", new Vector3(0f, -0.13f, 0f), new Vector3(0.12f, 0.27f, 0.12f), skin);
        Box(elbow, "Glove", new Vector3(0f, -0.32f, 0f), new Vector3(0.17f, 0.15f, 0.17f), glove);
        return elbow;
    }

    Transform BuildLeg(Transform hip, Color pants, Color shoe)
    {
        Box(hip, "Thigh", new Vector3(0f, -0.22f, 0f), new Vector3(0.18f, 0.46f, 0.18f), pants);
        var knee = Joint("Knee", hip, new Vector3(0f, -0.45f, 0f));
        Box(knee, "Shin", new Vector3(0f, -0.2f, 0f), new Vector3(0.16f, 0.42f, 0.16f), pants);
        Box(knee, "Shoe", new Vector3(0f, -0.43f, 0.06f), new Vector3(0.16f, 0.07f, 0.27f), shoe);
        return knee;
    }

    static Transform Joint(string name, Transform parent, Vector3 localPos)
    {
        var t = new GameObject(name).transform;
        t.SetParent(parent, false);
        t.localPosition = localPos;
        return t;
    }

    void Box(Transform parent, string name, Vector3 localPos, Vector3 size, Color c)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        ColorFighterUtil.Kill(go.GetComponent<Collider>());
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localScale = size;
        go.GetComponent<Renderer>().sharedMaterial = materialFor(c);
    }

    #endregion

    #region Animasyon

    void LateUpdate()
    {
        if (fighter == null || root == null) return;

        root.localScale = new Vector3(1f, 1f, fighter.Facing);

        // Spin Kick: hazırlık sırasında gövde bir tur döner.
        float spin = 0f, flip = 0f;
        var mv = fighter.CurrentMove;
        if (fighter.State == FState.Attack && mv != null && mv.pose == AttackPose.ComboRushB && fighter.StateFrame <= mv.startup)
            spin = 360f * fighter.StateFrame / Mathf.Max(1f, mv.startup + 1f);

        // Bitirici: adım boyunca dönüş / takla
        bool finishing = fighter.State == FState.Finisher && fighter.ActiveFinisher != null;
        if (finishing && !fighter.FinisherApproaching)
        {
            var b = fighter.CurrentBeat;
            float u = fighter.BeatProgress;
            float e = u * u * (3f - 2f * u);
            spin += b.spin * e;
            flip += b.flip * e;
        }
        if (Application.isPlaying) { UpdateGlow(); UpdateFlows(); }

        ComputeTarget(out bool snap);
        if (fighter.State == FState.Win) spin += celebSpin;

        var rot = Quaternion.Euler(0f, 90f + spin, 0f) * Quaternion.Euler(flip * fighter.Facing, 0f, 0f);
        root.localRotation = rot;
        // Takla kalça hizasında dönsün (ayak ucunda değil)
        Vector3 pivot = new Vector3(0f, 1f, 0f);
        Vector3 pivotOffset = Mathf.Abs(flip) > 0.01f ? pivot - rot * pivot : Vector3.zero;

        int beat = finishing ? fighter.BeatIndex : -1;
        bool changed = fighter.State != lastState || fighter.CurrentMove != lastMove || beat != lastBeat;
        lastState = fighter.State;
        lastMove = fighter.CurrentMove;
        lastBeat = beat;

        stepTimer += Time.deltaTime;
        if (snap || changed || finishing || stepTimer >= 1f / Mathf.Max(1f, poseFps))
        {
            float k = snap ? 1f : finishing ? 0.45f : 0.6f;
            for (int i = 0; i < N; i++) cur[i] = Mathf.Lerp(cur[i], tgt[i], k);
            stepTimer = 0f;
            Apply();
        }

        Vector3 shake = Vector3.zero;
        if (!MatchManager.Paused && FightFX.I != null && FightFX.I.Hitstop > 0 && fighter.StateFrame == 0 &&
            (fighter.State == FState.Hitstun || fighter.State == FState.Blockstun || fighter.State == FState.KO))
            shake.x = Random.Range(-0.05f, 0.05f);
        if (fighter.State == FState.Win) shake.y += celebY;
        root.localPosition = shake + pivotOffset;
    }

    void ComputeTarget(out bool snap)
    {
        snap = false;
        var f = fighter;
        switch (f.State)
        {
            case FState.Idle: Set(IDLE); Breathe(); break;
            case FState.Walk: Set(IDLE); WalkCycle(); break;
            case FState.Crouch: Set(f.HoldingBack ? CROUCH_GUARD : CROUCH); break;
            case FState.Guard: Set(GUARD); break;
            case FState.Jump: Set(JUMP); break;
            case FState.Attack: AttackTarget(f.CurrentMove, f.StateFrame, ref snap); break;
            case FState.Hitstun:
                snap = f.StateFrame <= 1;
                Set(f.Airborne ? AIR_HIT : f.HitWhileCrouching ? CROUCH_HIT : HIT);
                break;
            case FState.Blockstun:
                snap = f.StateFrame <= 1;
                Set(f.CrouchBlocking ? CROUCH_GUARD : GUARD);
                break;
            case FState.Knockdown: Set(f.StateFrame < 26 ? DOWN : CROUCH); break;
            case FState.KO:
                snap = f.StateFrame <= 1;
                Set(f.Airborne ? AIR_HIT : DOWN);
                break;
            case FState.Win:
                FinisherLibrary.Celebrate(f.styleIndex, Time.unscaledTime - f.WinTime, tgt, out celebY, out celebSpin);
                break;
            case FState.Finisher: FinisherTarget(ref snap); break;
        }
        if (f.State != FState.Win) { celebY = 0f; celebSpin = 0f; }
        if (f.State != FState.Attack) lastPhase = -1;
    }

    void FinisherTarget(ref bool snap)
    {
        var f = fighter;
        if (f.ActiveFinisher == null) { Set(IDLE); return; }
        if (f.FinisherApproaching) { Set(IDLE); WalkCycle(); return; }

        var b = f.CurrentBeat;
        float[] prev = f.BeatIndex > 0 ? f.ActiveFinisher.beats[f.BeatIndex - 1].pose : IDLE;
        if (b.snap)
        {
            Set(b.pose);
            snap = f.BeatFrame <= 1;
        }
        else Blend(prev, b.pose, Mathf.Clamp01(f.BeatProgress / 0.35f));
    }

    void AttackTarget(MoveData m, int frame, ref bool snap)
    {
        if (m == null) { Set(IDLE); return; }

        float[] wind, hit;
        switch (m.pose)
        {
            case AttackPose.StandKick: wind = KICK_WIND; hit = KICK_HIT; break;
            case AttackPose.CrouchPunch: wind = CPUNCH_WIND; hit = CPUNCH_HIT; break;
            case AttackPose.CrouchKick: wind = SWEEP_WIND; hit = SWEEP_HIT; break;
            case AttackPose.AirPunch: wind = APUNCH_WIND; hit = APUNCH_HIT; break;
            case AttackPose.AirKick: wind = AKICK_WIND; hit = AKICK_HIT; break;
            case AttackPose.ComboRushA: wind = RUSHA_WIND; hit = RUSHA_HIT; break;
            case AttackPose.ComboRushB: wind = RUSHB_WIND; hit = RUSHB_HIT; break;
            case AttackPose.ComboFinisher: wind = FIN_WIND; hit = FIN_HIT; break;
            default: wind = PUNCH_WIND; hit = PUNCH_HIT; break;
        }
        float[] rest = m.IsAir ? JUMP : m.IsCrouch ? CROUCH : IDLE;

        int phase;
        if (frame <= m.startup) { phase = 0; Set(wind); }
        else if (frame <= m.startup + m.active) { phase = 1; Set(hit); }
        else
        {
            phase = 2;
            float t = (frame - m.startup - m.active) / (float)Mathf.Max(1, m.recovery);
            Blend(hit, rest, t * t * (3f - 2f * t));
        }

        if (phase == 1 && lastPhase != 1) snap = true;
        lastPhase = phase;
    }

    void Breathe()
    {
        float s = Mathf.Sin(Time.time * 3.5f);
        tgt[HY] += s * 0.012f;
        tgt[TO] += s * 2f;
    }

    void WalkCycle()
    {
        float s = Mathf.Sin(Time.time * 10f);
        tgt[LH] += s * 20f;
        tgt[RH] -= s * 20f;
        tgt[LK] += Mathf.Max(0f, s) * 25f;
        tgt[RK] += Mathf.Max(0f, -s) * 25f;
        tgt[HY] -= Mathf.Abs(s) * 0.02f;
    }

    void Set(float[] src) { System.Array.Copy(src, tgt, N); }

    void Blend(float[] a, float[] b, float t)
    {
        for (int i = 0; i < N; i++) tgt[i] = Mathf.Lerp(a[i], b[i], t);
    }

    void Apply()
    {
        hips.localPosition = new Vector3(0f, cur[HY], 0f);
        hips.localRotation = Quaternion.Euler(cur[HP], 0f, 0f);
        torso.localRotation = Quaternion.Euler(cur[TO], 0f, 0f);
        head.localRotation = Quaternion.Euler(cur[HE], 0f, 0f);
        lSh.localRotation = Quaternion.Euler(cur[LS], 0f, 0f);
        lEl.localRotation = Quaternion.Euler(cur[LE], 0f, 0f);
        rSh.localRotation = Quaternion.Euler(cur[RS], 0f, 0f);
        rEl.localRotation = Quaternion.Euler(cur[RE], 0f, 0f);
        lHip.localRotation = Quaternion.Euler(cur[LH], 0f, 0f);
        lKn.localRotation = Quaternion.Euler(cur[LK], 0f, 0f);
        rHip.localRotation = Quaternion.Euler(cur[RH], 0f, 0f);
        rKn.localRotation = Quaternion.Euler(cur[RK], 0f, 0f);
    }

    #endregion
}