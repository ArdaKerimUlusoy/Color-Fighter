using UnityEngine;

public class MatchManager : MonoBehaviour
{
    public static bool Paused { get; private set; }

    #region Ayarlar

    public Fighter p1, p2;
    public Camera fightCam;
    public FightHUD hud;
    public Color p1Color = Color.red, p2Color = Color.blue;

    [Header("Kurallar")]
    public int roundsToWin = 2;
    public float roundTime = 60f;

    [Header("Mesafe")]
    public float startDistance = 3.2f;
    public float maxSeparation = 6f;
    public float bodyWidth = 0.6f;

    [Header("Karakter seçimi")]
    [Tooltip("Seçim ekranında iki dövüşçü arasındaki mesafe.")]
    public float selectSpacing = 5.2f;
    [Tooltip("İki oyuncu da hazır olduktan sonra maçın başlamasına kadar bekleme (sn).")]
    public float selectConfirmDelay = 1.0f;

    #endregion

    #region Durum

    enum Phase { Title, Intro, Fight, RoundOver, MatchOver, Select, Mode }
    Phase phase;
    float phaseTimer, timeLeft;
    int round, wins1, wins2;
    bool resultShown;
    Fighter roundWinner;
    Vector3 camPos;
    float timeScaleBeforePause = 1f;
    int pauseSelection;
    readonly int[] cursor = new int[2];
    readonly bool[] locked = new bool[2];
    float bothReadyTimer;
    FighterRig rig1, rig2;
    CpuBrain brain;
    bool vsCpu;
    int modeSel;
    float cpuPickTimer, cpuRollTimer;

    #endregion

    #region Başlangıç

    void Start()
    {
        Paused = false;
        p1.OnComboTaken += OnCombo;
        p2.OnComboTaken += OnCombo;
        p1.OnComboUnleashed += OnComboUnleashed;
        p2.OnComboUnleashed += OnComboUnleashed;
        if (FightFX.I != null) FightFX.I.OnFlash += hud.Flash;

        rig1 = p1.GetComponent<FighterRig>();
        rig2 = p2.GetComponent<FighterRig>();
        cursor[0] = FighterPalette.ClosestIndex(p1Color);
        cursor[1] = FighterPalette.ClosestIndex(p2Color);
        if (cursor[1] == cursor[0]) cursor[1] = (cursor[0] + 1) % FighterPalette.Count;
        hud.EnsureExtras(FighterPalette.All, p1.comboHitsRequired, p1.input.ComboKeyLabel, p2.input.ComboKeyLabel);

        brain = p2.GetComponent<CpuBrain>();
        if (brain == null) brain = p2.gameObject.AddComponent<CpuBrain>();
        brain.self = p2;
        SetCpu(false);
        ApplySelectedColors();

        ShowTitle();
        camPos = TargetCamPos();
    }

    void OnDestroy()
    {
        Paused = false;
        Time.timeScale = 1f;
    }

    void OnCombo(Fighter victim, int hits)
    {
        if (hits >= 2) hud.ShowCombo(victim == p2 ? 0 : 1, hits);
    }

    void OnComboUnleashed(Fighter f)
    {
        Color c = ColorOf(f);
        hud.ShowCenter("COLOR RUSH!", Color.Lerp(c, Color.white, 0.3f), 0.9f);
        hud.Flash(c, 0.45f);
    }

    #endregion

    #region Maç akışı

    void ShowTitle()
    {
        FightFX.I?.ResetState();
        phase = Phase.Title;
        phaseTimer = 0f;
        p1.ResetForRound(-startDistance * 0.5f);
        p2.ResetForRound(startDistance * 0.5f);
        timeLeft = roundTime;
        hud.ResetBars();
        // Seçim yarıda bırakıldıysa son onaylanan renklere dön.
        cursor[0] = FighterPalette.ClosestIndex(p1Color);
        cursor[1] = FighterPalette.ClosestIndex(p2Color);
        rig1.Recolor(p1Color);
        rig2.Recolor(p2Color);
        RecolorArena(p1Color, p2Color);
        SetCpu(false);
        hud.ShowSelect(false);
        hud.ShowMode(false);
        hud.ShowTitle(true);
    }

    /// <summary>1P VS CPU açıkken P2'yi bilgisayar oynar (klavyedeki 2P tuşları devre dışı).</summary>
    void SetCpu(bool on)
    {
        vsCpu = on;
        p2.input.cpuControlled = on;
        p2.input.ClearBuffers();
        if (brain != null) brain.enabled = on;
        hud.SetCpuMode(on, on ? "CPU" : p2.input.ComboKeyLabel);
    }

    void ShowMode()
    {
        FightFX.I?.ResetState();
        phase = Phase.Mode;
        phaseTimer = 0f;
        SetCpu(false);
        p1.input.ClearBuffers();
        p2.input.ClearBuffers();
        hud.SetModeSelection(modeSel);
        hud.ShowMode(true);
    }

    void UpdateMode()
    {
        if (phaseTimer < 0.2f) return;
        bool up = p1.input.ConsumeUp() | p2.input.ConsumeUp();
        bool down = p1.input.ConsumeDown() | p2.input.ConsumeDown();
        if (up || down)
        {
            modeSel = 1 - modeSel;
            hud.SetModeSelection(modeSel);
            FightFX.I?.PlayMenuMove();
        }
        if (p1.input.ConsumeKick() | p2.input.ConsumeKick())
        {
            FightFX.I?.PlayMenuMove();
            ShowTitle();
            return;
        }
        if (p1.input.ConsumePunch() | p2.input.ConsumePunch())
        {
            FightFX.I?.PlayMenuSelect();
            hud.ShowMode(false);
            SetCpu(modeSel == 1);
            ShowSelect();
        }
    }

    void ShowSelect()
    {
        FightFX.I?.ResetState();
        phase = Phase.Select;
        phaseTimer = 0f;
        bothReadyTimer = 0f;
        locked[0] = locked[1] = false;
        cpuPickTimer = cpuRollTimer = 0f;
        p1.ResetForRound(-selectSpacing * 0.5f);
        p2.ResetForRound(selectSpacing * 0.5f);
        p1.input.ClearBuffers();
        p2.input.ClearBuffers();
        rig1.Recolor(FighterPalette.All[cursor[0]].color);
        rig2.Recolor(FighterPalette.All[cursor[1]].color);
        PreviewArenaColors();
        hud.SetSelect(cursor[0], cursor[1], false, false, !vsCpu);
        hud.ShowSelect(true);
    }

    void UpdateSelect(float dt)
    {
        if (phaseTimer < 0.2f) return;
        SelectInput(0);
        if (vsCpu) CpuSelect(dt);
        else SelectInput(1);
        hud.SetSelect(cursor[0], cursor[1], locked[0], locked[1], !vsCpu || locked[0]);

        if (locked[0] && locked[1])
        {
            if (bothReadyTimer == 0f) hud.ShowCenter("GET READY!", new Color(1f, 0.85f, 0.2f), selectConfirmDelay);
            bothReadyTimer += dt;
            if (bothReadyTimer >= selectConfirmDelay)
            {
                ApplySelectedColors();
                hud.ShowSelect(false);
                FightFX.I?.PlayFight();
                StartMatch();
            }
        }
        else bothReadyTimer = 0f;
    }

    /// <summary>CPU, 1P rengini onaylayınca kısa bir "rulet" ile 1P'den farklı rastgele bir renk seçer.</summary>
    void CpuSelect(float dt)
    {
        if (!locked[0])
        {
            if (locked[1])
            {
                locked[1] = false;
                p2.ResetForRound(selectSpacing * 0.5f);
            }
            cpuPickTimer = cpuRollTimer = 0f;
            return;
        }
        if (locked[1]) return;

        cpuPickTimer += dt;
        cpuRollTimer -= dt;
        if (cpuRollTimer <= 0f || cursor[1] == cursor[0])
        {
            cpuRollTimer = 0.07f;
            // 1P'nin rengi (ve mümkünse şu anki renk) hariç rastgele bir renk
            var options = new System.Collections.Generic.List<int>();
            for (int i = 0; i < FighterPalette.Count; i++)
                if (i != cursor[0] && i != cursor[1]) options.Add(i);
            if (options.Count == 0)
                for (int i = 0; i < FighterPalette.Count; i++) if (i != cursor[0]) options.Add(i);
            int c = options[Random.Range(0, options.Count)];
            cursor[1] = c;
            rig2.Recolor(FighterPalette.All[c].color);
            PreviewArenaColors();
            FightFX.I?.PlayMenuMove();
        }
        if (cpuPickTimer >= 0.9f)
        {
            locked[1] = true;
            p2.SetWin();
            FightFX.I?.PlayMenuSelect();
        }
    }

    void SelectInput(int side)
    {
        var f = side == 0 ? p1 : p2;
        var input = f.input;
        int other = 1 - side;

        if (locked[side])
        {
            input.ConsumePunch(); input.ConsumeLeft(); input.ConsumeRight(); input.ConsumeUp(); input.ConsumeDown();
            if (input.ConsumeKick())
            {
                locked[side] = false;
                f.ResetForRound(side == 0 ? -selectSpacing * 0.5f : selectSpacing * 0.5f);
                FightFX.I?.PlayMenuMove();
            }
            return;
        }

        // Diğer oyuncunun üstünde durduğu renk atlanır: iki oyuncu aynı renge gelemez.
        int c = cursor[side], blocked = vsCpu ? -1 : cursor[other];
        if (input.ConsumeLeft()) c = MoveCursor(c, 0, blocked);
        if (input.ConsumeRight()) c = MoveCursor(c, 1, blocked);
        if (input.ConsumeUp()) c = MoveCursor(c, 2, blocked);
        if (input.ConsumeDown()) c = MoveCursor(c, 3, blocked);
        if (c != cursor[side])
        {
            cursor[side] = c;
            (side == 0 ? rig1 : rig2).Recolor(FighterPalette.All[c].color);
            PreviewArenaColors();
            FightFX.I?.PlayMenuMove();
        }

        input.ConsumeKick();
        if (input.ConsumePunch())
        {
            if (locked[other] && cursor[other] == cursor[side])
            {
                hud.SelectDenied(side);
                FightFX.I?.PlayBlip();
            }
            else
            {
                locked[side] = true;
                f.SetWin();
                FightFX.I?.PlayMenuSelect();
            }
        }
    }

    /// <summary>dir: 0 sol, 1 sağ, 2 yukarı, 3 aşağı. Engelli hücreye denk gelirse aynı yönde bir sonrakine geçer.</summary>
    static int MoveCursor(int c, int dir, int blocked)
    {
        int start = c;
        for (int k = 0; k < FighterPalette.Count; k++)
        {
            c = StepCursor(c, dir);
            if (c == start) return start;
            if (c != blocked) return c;
        }
        return start;
    }

    static int StepCursor(int c, int dir)
    {
        int n = FighterPalette.Count, cols = FighterPalette.Columns, rows = FighterPalette.Rows;
        switch (dir)
        {
            case 0: return c % cols == 0 ? Mathf.Min(c + cols - 1, n - 1) : c - 1;
            case 1: return c % cols == cols - 1 || c == n - 1 ? c - c % cols : c + 1;
            case 2: return c - cols >= 0 ? c - cols : Mathf.Min(c + cols * (rows - 1), n - 1);
            default: return c + cols < n ? c + cols : c % cols;
        }
    }

    void PreviewArenaColors()
    {
        RecolorArena(FighterPalette.All[cursor[0]].color, FighterPalette.All[cursor[1]].color);
    }

    void ApplySelectedColors()
    {
        var a = FighterPalette.All[cursor[0]];
        var b = FighterPalette.All[cursor[1]];
        p1Color = a.color; p2Color = b.color;
        p1.fighterName = a.name; p2.fighterName = vsCpu ? b.name + " CPU" : b.name;
        p1.mainColor = a.color; p2.mainColor = b.color;
        rig1.Recolor(a.color);
        rig2.Recolor(b.color);
        hud.SetPlayers(a.name, b.name, a.color, b.color);
        RecolorArena(p1Color, p2Color);
    }

    // Oyuncu renginde boyanan arena parçaları (köşe direkleri, arkadaki 1P/2P bayrakları, neonlar)
    Renderer[] arenaP1Parts, arenaP2Parts;

    void RecolorArena(Color c1, Color c2)
    {
        RecolorCabinet(c1, c2);
        if (arenaP1Parts == null)
        {
            var arena = p1.transform.parent;
            if (arena == null) return;
            var a = new System.Collections.Generic.List<Renderer>();
            var b = new System.Collections.Generic.List<Renderer>();
            foreach (var r in arena.GetComponentsInChildren<Renderer>(true))
            {
                string n = r.name;
                if (n == "NeonRed" || n == "CornerP1" || n == "BannerP1") a.Add(r);
                else if (n == "NeonBlue" || n == "CornerP2" || n == "BannerP2") b.Add(r);
                else if (n == "PillarNeon") (r.transform.localPosition.x < 0f ? a : b).Add(r);
            }
            arenaP1Parts = a.ToArray();
            arenaP2Parts = b.ToArray();
        }
        foreach (var r in arenaP1Parts) if (r != null) r.material.color = c1;
        foreach (var r in arenaP2Parts) if (r != null) r.material.color = c2;
    }

    // Kabindeki joystick/tuşlar ve kontrol panelindeki renk şeritleri
    CabinetControls cab1, cab2;
    Renderer[] stripes1, stripes2;

    void RecolorCabinet(Color c1, Color c2)
    {
        if (stripes1 == null)
        {
            foreach (var cc in GetComponentsInChildren<CabinetControls>(true))
            {
                if (cc.input == p1.input) cab1 = cc;
                else if (cc.input == p2.input) cab2 = cc;
            }
            var a = new System.Collections.Generic.List<Renderer>();
            var b = new System.Collections.Generic.List<Renderer>();
            foreach (var r in GetComponentsInChildren<Renderer>(true))
            {
                if (r.name == "StripeRed") a.Add(r);
                else if (r.name == "StripeBlue") b.Add(r);
            }
            stripes1 = a.ToArray();
            stripes2 = b.ToArray();
        }
        if (cab1 != null) cab1.SetColor(c1);
        if (cab2 != null) cab2.SetColor(c2);
        foreach (var r in stripes1) if (r != null) r.material.color = c1;
        foreach (var r in stripes2) if (r != null) r.material.color = c2;
    }

    void StartMatch()
    {
        FightFX.I?.ResetState();
        hud.ShowSelect(false);
        hud.ShowTitle(false);
        wins1 = wins2 = 0;
        round = 0;
        hud.SetWins(0, 0);
        NextRound();
    }

    void NextRound()
    {
        round++;
        p1.ResetForRound(-startDistance * 0.5f);
        p2.ResetForRound(startDistance * 0.5f);
        timeLeft = roundTime;
        phase = Phase.Intro;
        phaseTimer = 0f;
        resultShown = false;
        roundWinner = null;
        hud.ResetBars();
        bool final = wins1 == roundsToWin - 1 && wins2 == roundsToWin - 1;
        hud.ShowCenter(final ? "FINAL ROUND" : "ROUND " + round, new Color(1f, 0.85f, 0.2f), 0f);
        FightFX.I?.PlayBlip();
    }

    void FixedUpdate()
    {
        if (Paused) return;
        if (FightFX.I != null && FightFX.I.ConsumeHitstopFrame()) return;

        float dt = Time.fixedDeltaTime;
        phaseTimer += dt;

        p1.Tick(dt);
        p2.Tick(dt);
        ResolveBodies();
        p1.SyncTransform();
        p2.SyncTransform();

        switch (phase)
        {
            case Phase.Title:
                if (MainMenu.Active)
                {
                    // Ana menü / zoom sürerken kabindeki oyun tuşlara tepki vermez
                    p1.input.ClearBuffers();
                    p2.input.ClearBuffers();
                    phaseTimer = 0f;
                    break;
                }
                if (phaseTimer > 0.5f && (p1.input.ConsumePunch() | p2.input.ConsumePunch()))
                {
                    FightFX.I?.PlayMenuSelect();
                    ShowMode();
                }
                break;

            case Phase.Mode:
                UpdateMode();
                break;

            case Phase.Select:
                UpdateSelect(dt);
                break;

            case Phase.Intro:
                if (phaseTimer > 1.2f)
                {
                    hud.ShowCenter("FIGHT!", new Color(1f, 0.3f, 0.2f), 0.7f);
                    FightFX.I?.PlayFight();
                    SetControl(true);
                    phase = Phase.Fight;
                    phaseTimer = 0f;
                }
                break;

            case Phase.Fight:
                timeLeft = Mathf.Max(0f, timeLeft - dt);
                if (p1.Health <= 0 || p2.Health <= 0) EndRound(true);
                else if (timeLeft <= 0f) EndRound(false);
                break;

            case Phase.RoundOver:
                if (roundWinner != null && phaseTimer > 1.0f && roundWinner.State != FState.Win)
                    roundWinner.SetWin();
                if (!resultShown && phaseTimer > 1.6f)
                {
                    resultShown = true;
                    if (roundWinner != null) hud.ShowCenter(roundWinner.fighterName + " WINS", ColorOf(roundWinner), 0f);
                    else hud.ShowCenter("DRAW", Color.white, 0f);
                }
                if (phaseTimer > 3.4f)
                {
                    if (wins1 >= roundsToWin || wins2 >= roundsToWin) MatchOver();
                    else NextRound();
                }
                break;

            case Phase.MatchOver:
                if (phaseTimer > 1.5f && (p1.input.ConsumePunch() | p2.input.ConsumePunch()))
                    StartMatch();
                else if (phaseTimer > 1.5f && (p1.input.ConsumeKick() | p2.input.ConsumeKick()))
                {
                    FightFX.I?.PlayMenuSelect();
                    ShowSelect();
                }
                else if (phaseTimer > 10f)
                    ShowTitle();
                break;
        }
    }

    void EndRound(bool ko)
    {
        phase = Phase.RoundOver;
        phaseTimer = 0f;
        SetControl(false);

        roundWinner = p1.Health == p2.Health ? null : (p1.Health > p2.Health ? p1 : p2);
        if (roundWinner == p1) wins1++;
        else if (roundWinner == p2) wins2++;
        hud.SetWins(wins1, wins2);

        if (ko) hud.ShowKO();
        else hud.ShowCenter("TIME", Color.white, 0f);
        if (!ko) FightFX.I?.PlayBlip();
    }

    void MatchOver()
    {
        phase = Phase.MatchOver;
        phaseTimer = 0f;
        var w = wins1 > wins2 ? p1 : p2;
        hud.ShowCenter(w.fighterName + " WINS!\n<size=10>PUNCH: REMATCH   KICK: SELECT</size>", ColorOf(w), 0f);
    }

    void SetControl(bool on) { p1.controlEnabled = on; p2.controlEnabled = on; }

    Color ColorOf(Fighter f) { return f == p1 ? p1Color : p2Color; }

    #endregion

    #region Pause

    bool CanPause => phase == Phase.Intro || phase == Phase.Fight || phase == Phase.RoundOver;

    void Update()
    {
        if (MainMenu.Active) return;
        bool pausePressed = p1.input.ConsumePause() | p2.input.ConsumePause();

        if (!Paused)
        {
            if (pausePressed && phase == Phase.Select) { FightFX.I?.PlayMenuMove(); ShowMode(); }
            else if (pausePressed && phase == Phase.Mode) { FightFX.I?.PlayMenuMove(); ShowTitle(); }
            else if (pausePressed && CanPause) Pause();
            return;
        }

        if (pausePressed || p1.input.ConsumeKick() | p2.input.ConsumeKick())
        {
            Resume();
            return;
        }

        int count = hud.PauseOptionCount;
        if (p1.input.ConsumeUp() | p2.input.ConsumeUp())
        {
            pauseSelection = (pauseSelection + count - 1) % count;
            hud.SetPauseSelection(pauseSelection);
            FightFX.I?.PlayMenuMove();
        }
        if (p1.input.ConsumeDown() | p2.input.ConsumeDown())
        {
            pauseSelection = (pauseSelection + 1) % count;
            hud.SetPauseSelection(pauseSelection);
            FightFX.I?.PlayMenuMove();
        }
        if (p1.input.ConsumePunch() | p2.input.ConsumePunch())
        {
            FightFX.I?.PlayMenuSelect();
            int choice = pauseSelection;
            Resume();
            if (choice == 1) StartMatch();
            else if (choice == 2) ShowTitle();
        }
    }

    void Pause()
    {
        Paused = true;
        timeScaleBeforePause = Time.timeScale;
        Time.timeScale = 0f;
        pauseSelection = 0;
        p1.input.ClearBuffers();
        p2.input.ClearBuffers();
        hud.ShowPause(true);
        FightFX.I?.PlayMenuSelect();
    }

    void Resume()
    {
        Paused = false;
        Time.timeScale = timeScaleBeforePause;
        p1.input.ClearBuffers();
        p2.input.ClearBuffers();
        hud.ShowPause(false);
    }

    #endregion

    #region Gövde çarpışması

    void ResolveBodies()
    {
        float dx = p2.X - p1.X;
        bool bothSolid = p1.State != FState.KO && p2.State != FState.KO;
        if (bothSolid && Mathf.Abs(p1.Y - p2.Y) < 1.1f && Mathf.Abs(dx) < bodyWidth)
        {
            float dir = Mathf.Abs(dx) > 0.001f ? Mathf.Sign(dx) : p1.Facing;
            float overlap = bodyWidth - Mathf.Abs(dx);
            float before = p1.X;
            p1.Nudge(-dir * overlap * 0.5f);
            p2.Nudge(dir * (overlap - Mathf.Abs(p1.X - before)));
            float rem = bodyWidth - Mathf.Abs(p2.X - p1.X);
            if (rem > 0.0001f) p1.Nudge(-dir * rem);
        }

        float sep = p2.X - p1.X;
        if (Mathf.Abs(sep) > maxSeparation)
        {
            float excess = Mathf.Abs(sep) - maxSeparation;
            float s = Mathf.Sign(sep);
            p1.Nudge(s * excess * 0.5f);
            p2.Nudge(-s * excess * 0.5f);
        }
    }

    #endregion

    #region Kamera ve HUD

    public static Vector3 CameraPosFor(float x1, float x2, float yMax)
    {
        float mid = (x1 + x2) * 0.5f;
        float dist = Mathf.Abs(x2 - x1);
        mid = Mathf.Clamp(mid, -Fighter.StageHalfWidth + 1.5f, Fighter.StageHalfWidth - 1.5f);
        float y = 1.6f + yMax * 0.25f;
        float z = -(4.2f + dist * 0.75f);
        return new Vector3(mid, y, z);
    }

    public static readonly Quaternion CameraRotation = Quaternion.Euler(7f, 0f, 0f);

    Vector3 TargetCamPos() { return CameraPosFor(p1.X, p2.X, Mathf.Max(p1.Y, p2.Y)); }

    void LateUpdate()
    {
        if (!Paused)
        {
            camPos = Vector3.Lerp(camPos, TargetCamPos(), 1f - Mathf.Exp(-6f * Time.unscaledDeltaTime));
            Vector3 shake = FightFX.I != null ? FightFX.I.ShakeOffset : Vector3.zero;
            fightCam.transform.localPosition = camPos + shake;
            fightCam.transform.localRotation = CameraRotation;
        }

        hud.SetHealth(p1.Health / (float)p1.maxHealth, p2.Health / (float)p2.maxHealth);
        hud.SetComboMeter(0, p1.HitStreak, p1.ComboReady, p1.ComboReadyFraction, p1Color);
        hud.SetComboMeter(1, p2.HitStreak, p2.ComboReady, p2.ComboReadyFraction, p2Color);
        hud.SetTimer(Mathf.CeilToInt(timeLeft));
    }

    #endregion
}