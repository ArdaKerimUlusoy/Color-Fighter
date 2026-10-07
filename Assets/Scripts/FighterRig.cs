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

    static readonly float[] IDLE        = { 0.92f,   8,   -6,  -55,  -95,  -30, -115,   -28,   32,    20,   28,    0 };
    static readonly float[] CROUCH      = { 0.53f,  22,  -15,  -60, -100,  -35, -115,   -75,  115,   -10,   85,    0 };
    static readonly float[] GUARD       = { 0.90f,  -4,   12,  -80, -125,  -70, -130,   -22,   28,    22,   24,    0 };
    static readonly float[] CROUCH_GUARD= { 0.53f,  10,   10,  -80, -125,  -70, -130,   -75,  115,   -10,   85,    0 };
    static readonly float[] JUMP        = { 0.95f,  10,   -5,  -60, -100,  -40, -110,   -80,  120,   -60,  120,    0 };
    static readonly float[] PUNCH_WIND  = { 0.90f,   2,   -6,  -55,  -95,   15, -125,   -28,   32,    20,   28,    0 };
    static readonly float[] PUNCH_HIT   = { 0.88f,  22,  -12,  -50,  -95,  -88,   -5,   -40,   30,    30,   15,    0 };
    static readonly float[] KICK_WIND   = { 0.95f,  -8,   -5,  -45,  -95,  -20, -110,   -15,   20,   -75,  110,    0 };
    static readonly float[] KICK_HIT    = { 0.95f, -18,    5,  -35,  -90,   10, -100,     5,   15,   -98,    0,    0 };
    static readonly float[] CPUNCH_WIND = { 0.53f,  15,  -15,  -60, -100,   10, -125,   -75,  115,   -10,   85,    0 };
    static readonly float[] CPUNCH_HIT  = { 0.53f,  30,  -20,  -55, -100,  -85,   -5,   -75,  115,   -10,   85,    0 };
    static readonly float[] SWEEP_WIND  = { 0.50f,  25,  -15,  -60, -100,  -35, -110,   -70,  120,   -40,  100,    0 };
    static readonly float[] SWEEP_HIT   = { 0.40f,  35,  -20,  -70, -100,  -40, -110,   -60,  120,   -85,    0,    0 };
    static readonly float[] APUNCH_WIND = { 0.95f,   5,   -5,  -60, -100,   20, -120,   -80,  120,   -60,  120,    0 };
    static readonly float[] APUNCH_HIT  = { 0.95f,  25,  -15,  -60, -100,  -60,   -5,   -80,  120,   -60,  120,    0 };
    static readonly float[] AKICK_WIND  = { 0.95f,   5,   -5,  -60, -100,  -40, -110,   -80,  120,   -80,  120,    0 };
    static readonly float[] AKICK_HIT   = { 0.95f, -12,    0,  -60, -100,  -40, -110,   -80,  120,   -70,    0,    0 };
    static readonly float[] HIT         = { 0.90f, -25,  -30,   20,  -60,   30,  -50,   -20,   25,    25,   20,    0 };
    static readonly float[] CROUCH_HIT  = { 0.53f, -10,  -25,   20,  -60,   30,  -50,   -75,  115,   -10,   85,    0 };
    static readonly float[] AIR_HIT     = { 0.95f, -40,  -30, -150,  -20, -160,  -10,   -40,   20,    30,   40,  -20 };
    static readonly float[] DOWN        = { 0.14f,   0,   10,  -20,  -20,  -10,  -30,   -10,   20,    10,   30,  -90 };
    static readonly float[] WIN         = { 0.95f,  -5,  -15,  -40, -100, -170,  -10,   -15,   10,    15,   10,    0 };

    #endregion

    #region Çalışma durumu

    readonly float[] cur = new float[N];
    readonly float[] tgt = new float[N];
    FState lastState;
    MoveData lastMove;
    int lastPhase = -1;
    float stepTimer;
    System.Func<Color, Material> materialFor;

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

        ComputeTarget(out bool snap);
        bool changed = fighter.State != lastState || fighter.CurrentMove != lastMove;
        lastState = fighter.State;
        lastMove = fighter.CurrentMove;

        stepTimer += Time.deltaTime;
        if (snap || changed || stepTimer >= 1f / Mathf.Max(1f, poseFps))
        {
            float k = snap ? 1f : 0.6f;
            for (int i = 0; i < N; i++) cur[i] = Mathf.Lerp(cur[i], tgt[i], k);
            stepTimer = 0f;
            Apply();
        }

        Vector3 shake = Vector3.zero;
        if (!MatchManager.Paused && FightFX.I != null && FightFX.I.Hitstop > 0 && fighter.StateFrame == 0 &&
            (fighter.State == FState.Hitstun || fighter.State == FState.Blockstun || fighter.State == FState.KO))
            shake.x = Random.Range(-0.05f, 0.05f);
        root.localPosition = shake;
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
            case FState.Win: Set(WIN); break;
        }
        if (f.State != FState.Attack) lastPhase = -1;
    }

    void AttackTarget(MoveData m, int frame, ref bool snap)
    {
        if (m == null) { Set(IDLE); return; }

        float[] wind, hit;
        switch (m.pose)
        {
            case AttackPose.StandKick:   wind = KICK_WIND;   hit = KICK_HIT;   break;
            case AttackPose.CrouchPunch: wind = CPUNCH_WIND; hit = CPUNCH_HIT; break;
            case AttackPose.CrouchKick:  wind = SWEEP_WIND;  hit = SWEEP_HIT;  break;
            case AttackPose.AirPunch:    wind = APUNCH_WIND; hit = APUNCH_HIT; break;
            case AttackPose.AirKick:     wind = AKICK_WIND;  hit = AKICK_HIT;  break;
            default:                     wind = PUNCH_WIND;  hit = PUNCH_HIT;  break;
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
