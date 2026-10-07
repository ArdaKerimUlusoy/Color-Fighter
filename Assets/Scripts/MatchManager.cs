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

    #endregion

    #region Durum

    enum Phase { Title, Intro, Fight, RoundOver, MatchOver }
    Phase phase;
    float phaseTimer, timeLeft;
    int round, wins1, wins2;
    bool resultShown;
    Fighter roundWinner;
    Vector3 camPos;
    float timeScaleBeforePause = 1f;
    int pauseSelection;

    #endregion

    #region Başlangıç

    void Start()
    {
        Paused = false;
        p1.OnComboTaken += OnCombo;
        p2.OnComboTaken += OnCombo;
        if (FightFX.I != null) FightFX.I.OnFlash += hud.Flash;
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
        hud.ShowTitle(true);
    }

    void StartMatch()
    {
        FightFX.I?.ResetState();
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
                if (phaseTimer > 0.5f && (p1.input.ConsumePunch() | p2.input.ConsumePunch()))
                {
                    FightFX.I?.PlayFight();
                    StartMatch();
                }
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

        hud.ShowCenter(ko ? "K.O." : "TIME", ko ? new Color(1f, 0.2f, 0.15f) : Color.white, 0f);
        if (!ko) FightFX.I?.PlayBlip();
    }

    void MatchOver()
    {
        phase = Phase.MatchOver;
        phaseTimer = 0f;
        var w = wins1 > wins2 ? p1 : p2;
        hud.ShowCenter(w.fighterName + " WINS!\n<size=10>PRESS PUNCH FOR REMATCH</size>", ColorOf(w), 0f);
    }

    void SetControl(bool on) { p1.controlEnabled = on; p2.controlEnabled = on; }

    Color ColorOf(Fighter f) { return f == p1 ? p1Color : p2Color; }

    #endregion

    #region Pause

    bool CanPause => phase == Phase.Intro || phase == Phase.Fight || phase == Phase.RoundOver;

    void Update()
    {
        bool pausePressed = p1.input.ConsumePause() | p2.input.ConsumePause();

        if (!Paused)
        {
            if (pausePressed && CanPause) Pause();
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
        hud.SetTimer(Mathf.CeilToInt(timeLeft));
    }

    #endregion
}
