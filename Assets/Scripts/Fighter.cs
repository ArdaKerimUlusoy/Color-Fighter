using UnityEngine;

public enum FState { Idle, Walk, Crouch, Guard, Jump, Attack, Hitstun, Blockstun, Knockdown, KO, Win, Finisher }

public class Fighter : MonoBehaviour
{
    public const float StageHalfWidth = 4.5f;

    #region Ayarlar

    [Header("Kimlik")]
    public string fighterName = "RED";

    [Header("Hareket")]
    public float walkForward = 2.6f;
    public float walkBack = 2.0f;
    public float jumpVelocity = 9.5f;
    public float jumpForwardSpeed = 3.2f;
    public float gravity = 30f;
    public float groundFriction = 20f;

    [Header("Can")]
    public int maxHealth = 100;
    [Tooltip("Bu dövüşçünün aldığı hasar bu sayıyla çarpılır. Düşük = canlar daha yavaş azalır, raunt uzar.")]
    [Range(0.2f, 1.5f)] public float damageTaken = 0.5f;

    [Header("Saldırılar")]
    public MoveData standPunch = new MoveData
    {
        name = "Jab",
        pose = AttackPose.StandPunch,
        startup = 4,
        active = 3,
        recovery = 8,
        damage = 6,
        level = HitLevel.Mid,
        hitboxOffset = new Vector2(0.75f, 1.38f),
        hitboxSize = new Vector2(0.6f, 0.3f),
        hitstun = 14,
        blockstun = 10,
        hitstop = 6,
        pushback = 3f,
        lunge = 0.6f,
        cancelable = true
    };

    public MoveData standKick = new MoveData
    {
        name = "Kick",
        pose = AttackPose.StandKick,
        startup = 9,
        active = 4,
        recovery = 16,
        damage = 14,
        level = HitLevel.Mid,
        hitboxOffset = new Vector2(1.0f, 0.95f),
        hitboxSize = new Vector2(0.8f, 0.35f),
        hitstun = 20,
        blockstun = 14,
        hitstop = 10,
        pushback = 4.5f,
        lunge = 1.2f
    };

    public MoveData crouchPunch = new MoveData
    {
        name = "Crouch Jab",
        pose = AttackPose.CrouchPunch,
        startup = 5,
        active = 3,
        recovery = 9,
        damage = 5,
        level = HitLevel.Mid,
        hitboxOffset = new Vector2(0.7f, 0.95f),
        hitboxSize = new Vector2(0.6f, 0.3f),
        hitstun = 13,
        blockstun = 9,
        hitstop = 6,
        pushback = 2.5f,
        cancelable = true
    };

    public MoveData crouchKick = new MoveData
    {
        name = "Sweep",
        pose = AttackPose.CrouchKick,
        startup = 10,
        active = 4,
        recovery = 22,
        damage = 12,
        level = HitLevel.Low,
        hitboxOffset = new Vector2(0.9f, 0.2f),
        hitboxSize = new Vector2(0.9f, 0.3f),
        hitstun = 30,
        blockstun = 12,
        hitstop = 10,
        pushback = 2f,
        knockdown = true,
        launch = 3f
    };

    public MoveData airPunch = new MoveData
    {
        name = "Air Punch",
        pose = AttackPose.AirPunch,
        startup = 5,
        active = 8,
        recovery = 6,
        damage = 8,
        level = HitLevel.High,
        hitboxOffset = new Vector2(0.6f, 1.1f),
        hitboxSize = new Vector2(0.6f, 0.5f),
        hitstun = 16,
        blockstun = 12,
        hitstop = 7,
        pushback = 2.5f
    };

    public MoveData airKick = new MoveData
    {
        name = "Air Kick",
        pose = AttackPose.AirKick,
        startup = 7,
        active = 10,
        recovery = 6,
        damage = 12,
        level = HitLevel.High,
        hitboxOffset = new Vector2(0.7f, 0.6f),
        hitboxSize = new Vector2(0.7f, 0.45f),
        hitstun = 18,
        blockstun = 14,
        hitstop = 9,
        pushback = 3f
    };

    [Header("Kombo sistemi")]
    [Tooltip("Peş peşe kaç isabetli vuruştan sonra kombo hakkı açılır.")]
    public int comboHitsRequired = 3;
    [Tooltip("İki isabet arasında geçebilecek en uzun süre (frame). Aşılırsa seri sıfırlanır.")]
    public int comboChainWindow = 75;
    [Tooltip("Kombo hakkı açıldıktan sonra kullanılabileceği süre (frame).")]
    public int comboReadyFrames = 360;
    [Tooltip("Bitiriş aparkatında yukarı zıplama hızı.")]
    public float finisherHop = 5.5f;

    public MoveData comboRush1 = new MoveData
    {
        name = "Color Rush",
        pose = AttackPose.ComboRushA,
        startup = 3,
        active = 4,
        recovery = 12,
        damage = 9,
        level = HitLevel.Mid,
        hitboxOffset = new Vector2(0.8f, 1.2f),
        hitboxSize = new Vector2(0.8f, 0.5f),
        hitstun = 26,
        blockstun = 18,
        hitstop = 7,
        pushback = 0.6f,
        lunge = 9f,
        isSuper = true
    };

    public MoveData comboRush2 = new MoveData
    {
        name = "Spin Kick",
        pose = AttackPose.ComboRushB,
        startup = 6,
        active = 4,
        recovery = 12,
        damage = 9,
        level = HitLevel.Mid,
        hitboxOffset = new Vector2(0.95f, 1.2f),
        hitboxSize = new Vector2(0.9f, 0.5f),
        hitstun = 26,
        blockstun = 18,
        hitstop = 8,
        pushback = 0.8f,
        lunge = 3f,
        isSuper = true
    };

    public MoveData comboFinisher = new MoveData
    {
        name = "Burst Uppercut",
        pose = AttackPose.ComboFinisher,
        startup = 6,
        active = 6,
        recovery = 26,
        damage = 20,
        level = HitLevel.Mid,
        hitboxOffset = new Vector2(0.75f, 1.4f),
        hitboxSize = new Vector2(1.0f, 1.1f),
        hitstun = 40,
        blockstun = 20,
        hitstop = 16,
        pushback = 4f,
        lunge = 4f,
        knockdown = true,
        launch = 9f,
        isSuper = true
    };

    [Header("Bağlantılar")]
    public Fighter opponent;
    public FighterInput input;
    [HideInInspector] public bool controlEnabled;
    [HideInInspector] public Color mainColor = Color.white;

    [Header("Bitirici ve zafer")]
    [Tooltip("Son (K.O.) vuruşu, rengine özel sinematik bitirici harekete dönüşür.")]
    public bool finishers = true;
    [Tooltip("Karakter stili = palet sırası (0 RED ... 7 PINK). Bitirici ve sevinç animasyonunu seçer.")]
    public int styleIndex;

    #endregion

    #region Durum

    public System.Action<Fighter, int> OnComboTaken;
    public System.Action<Fighter> OnComboReady, OnComboUnleashed;
    /// <summary>Bitirici başladı (saldıran, kurban).</summary>
    public System.Action<Fighter, Fighter> OnFinisherStart;

    public FState State { get; private set; }
    public int StateFrame { get; private set; }
    public MoveData CurrentMove { get; private set; }
    public int Health { get; private set; }
    public int Facing { get; private set; } = 1;
    public int ComboCount { get; private set; }
    public bool CrouchBlocking { get; private set; }
    public bool HitWhileCrouching { get; private set; }
    public int HitStreak { get; private set; }
    public bool ComboReady { get; private set; }
    public float ComboReadyFraction => ComboReady ? readyLeft / (float)Mathf.Max(1, comboReadyFrames) : 0f;
    public bool InComboRush => State == FState.Attack && CurrentMove != null && CurrentMove.isSuper;

    // Bitirici durumu (FighterRig okur)
    public bool InFinisher => State == FState.Finisher;
    public bool BeingFinished { get; private set; }
    public FinisherLibrary.Finisher ActiveFinisher { get; private set; }
    public int BeatIndex { get; private set; }
    public int BeatFrame { get; private set; }
    public bool FinisherApproaching { get; private set; }
    public float WinTime { get; private set; }
    Fighter finisherVictim;
    bool beatHitDone;

    public float X => pos.x;
    public float Y => pos.y;
    public bool Airborne => pos.y > 0.0001f;
    public bool HoldingBack => controlEnabled && input != null && input.Horizontal * Facing < -0.1f;
    public bool IsHittable => State != FState.Knockdown && State != FState.KO && State != FState.Win && State != FState.Finisher && !BeingFinished;

    Vector2 pos, vel;
    int stunFrames;
    int streakTimer, readyLeft;
    bool moveHasHit, airAttackUsed;

    void Awake()
    {
        Health = maxHealth;
        pos = new Vector2(transform.localPosition.x, 0f);
    }

    #endregion

    #region Ana döngü

    public void Tick(float dt)
    {
        StateFrame++;
        TickComboTimers();
        switch (State)
        {
            case FState.Idle:
            case FState.Walk:
            case FState.Crouch:
            case FState.Guard:
                Neutral();
                break;

            case FState.Jump:
                if (controlEnabled && !airAttackUsed)
                {
                    if (input.ConsumePunch()) { airAttackUsed = true; StartMove(airPunch); }
                    else if (input.ConsumeKick()) { airAttackUsed = true; StartMove(airKick); }
                }
                break;

            case FState.Attack:
                AttackTick();
                break;

            case FState.Hitstun:
                if (BeingFinished) break;   // bitirici bitene kadar sersem kalır
                if (!Airborne && --stunFrames <= 0) SetState(FState.Idle);
                break;

            case FState.Blockstun:
                if (--stunFrames <= 0) SetState(FState.Idle);
                break;

            case FState.Finisher:
                FinisherTick();
                break;

            case FState.Knockdown:
                if (StateFrame >= 40) SetState(FState.Idle);
                break;
        }
        Physics(dt);
    }

    void Neutral()
    {
        if (!controlEnabled)
        {
            if (State == FState.Walk) vel.x = 0f;
            ChangeState(FState.Idle);
            return;
        }

        UpdateFacing();
        float h = input.Horizontal;
        float rel = h * Facing;
        bool down = input.Down;

        if (ComboReady && input.ConsumeCombo()) { StartComboRush(); return; }
        if (input.Up) { DoJump(h); return; }
        if (input.ConsumePunch()) { StartMove(down ? crouchPunch : standPunch); return; }
        if (input.ConsumeKick()) { StartMove(down ? crouchKick : standKick); return; }

        if (down)
        {
            if (State == FState.Walk) vel.x = 0f;
            ChangeState(FState.Crouch);
            return;
        }

        if (rel < -0.1f && OpponentThreatening())
        {
            if (State == FState.Walk) vel.x = 0f;
            ChangeState(FState.Guard);
            return;
        }

        if (Mathf.Abs(h) > 0.1f)
        {
            vel.x = h * (rel > 0f ? walkForward : walkBack);
            ChangeState(FState.Walk);
        }
        else
        {
            if (State == FState.Walk) vel.x = 0f;
            ChangeState(FState.Idle);
        }
    }

    bool OpponentThreatening()
    {
        return opponent != null && opponent.State == FState.Attack && Mathf.Abs(opponent.pos.x - pos.x) < 3f;
    }

    void DoJump(float h)
    {
        vel.y = jumpVelocity;
        vel.x = Mathf.Abs(h) > 0.1f ? Mathf.Sign(h) * jumpForwardSpeed : 0f;
        airAttackUsed = false;
        SetState(FState.Jump);
    }

    void StartMove(MoveData m)
    {
        CurrentMove = m;
        moveHasHit = false;
        if (!m.IsAir) vel.x = 0f;
        SetState(FState.Attack);
    }

    void AttackTick()
    {
        var m = CurrentMove;
        int f = StateFrame;

        if (!m.IsAir && !moveHasHit && f <= m.startup + m.active) vel.x = Facing * m.lunge;
        if (m == comboFinisher && f == m.startup + 1) vel.y = finisherHop;
        if (f == m.startup + 1) FightFX.I?.OnWhiff(m);

        bool active = f > m.startup && f <= m.startup + m.active;
        if (active && !moveHasHit) TryHit(m);

        if (m.isSuper)
        {
            // Color Rush zinciri: her parça isabet ederse (ya da bloklanırsa) bir sonrakine otomatik geçer.
            MoveData next = m == comboRush1 ? comboRush2 : m == comboRush2 ? comboFinisher : null;
            if (next != null && moveHasHit && f >= m.startup + m.active + 2)
            {
                UpdateFacing();
                StartMove(next);
                return;
            }
        }
        else if (moveHasHit && ComboReady && controlEnabled && !m.IsAir && f > m.startup && input.ConsumeCombo())
        {
            StartComboRush();
            return;
        }

        if (moveHasHit && m.cancelable && controlEnabled && f > m.startup && input.ConsumeKick())
        {
            StartMove(m.IsCrouch ? crouchKick : standKick);
            return;
        }

        if (f >= m.Total) SetState(Airborne ? FState.Jump : FState.Idle);
    }

    #endregion

    #region Kombo sistemi

    void TickComboTimers()
    {
        if (streakTimer > 0 && --streakTimer == 0) HitStreak = 0;
        if (ComboReady && controlEnabled && --readyLeft <= 0) { ComboReady = false; readyLeft = 0; }
    }

    void RegisterHit(bool landed)
    {
        if (!landed) { HitStreak = 0; streakTimer = 0; return; }
        HitStreak++;
        streakTimer = comboChainWindow;
        if (!ComboReady && HitStreak >= comboHitsRequired)
        {
            ComboReady = true;
            readyLeft = comboReadyFrames;
            HitStreak = 0;
            streakTimer = 0;
            FightFX.I?.OnComboReady(ToWorld(pos + new Vector2(0f, 1.2f)), mainColor);
            OnComboReady?.Invoke(this);
        }
    }

    void StartComboRush()
    {
        ComboReady = false;
        readyLeft = 0;
        HitStreak = 0;
        streakTimer = 0;
        UpdateFacing();
        StartMove(comboRush1);
        FightFX.I?.OnComboUnleash(ToWorld(pos + new Vector2(0f, 1.1f)), mainColor);
        OnComboUnleashed?.Invoke(this);
    }

    #endregion

    #region Bitirici

    void StartFinisher(Fighter victim)
    {
        finisherVictim = victim;
        ActiveFinisher = FinisherLibrary.Get(styleIndex);
        BeatIndex = 0;
        BeatFrame = 0;
        beatHitDone = false;
        controlEnabled = false;
        CurrentMove = null;
        UpdateFacing();
        // Rakip uzaktaysa önce yanına koş
        FinisherApproaching = Mathf.Abs(victim.pos.x - pos.x) > 1.05f;
        if (!FinisherApproaching) vel.x = 0f;
        SetState(FState.Finisher);
        FightFX.I?.OnFinisherStart(ToWorld(pos + new Vector2(0f, 1.1f)), mainColor);
        OnFinisherStart?.Invoke(this, victim);
    }

    public FinisherLibrary.Beat CurrentBeat => ActiveFinisher.beats[Mathf.Clamp(BeatIndex, 0, ActiveFinisher.beats.Length - 1)];
    public float BeatProgress => Mathf.Clamp01(BeatFrame / (float)Mathf.Max(1, CurrentBeat.frames));

    void FinisherTick()
    {
        var v = finisherVictim;
        if (ActiveFinisher == null || v == null) { SetState(FState.Idle); return; }

        if (FinisherApproaching)
        {
            float dx = v.pos.x - pos.x;
            if (Mathf.Abs(dx) <= 0.95f || StateFrame > 40) { FinisherApproaching = false; vel.x = 0f; }
            else { vel.x = Mathf.Sign(dx) * 5f; return; }
        }

        var b = CurrentBeat;
        if (BeatFrame == 0)
        {
            if (b.dash != 0f) vel.x = Facing * b.dash;
            if (b.hop > 0f) vel.y = b.hop;
            if (b.hit > 0) FightFX.I?.OnWhiff(standKick);
        }
        BeatFrame++;

        if (b.hit > 0 && !beatHitDone && BeatFrame >= Mathf.Max(1, Mathf.RoundToInt(b.frames * b.hitAt)))
        {
            beatHitDone = true;
            if (b.hit == 2) v.FinisherKO(this, b.knock, b.launch);
            else v.FinisherHit(this, b.knock);
        }

        // Rakibin içine girme (içinden geçen adımlar hariç; arkasında yer yoksa onlar da önünde durur)
        bool roomBehind = v.pos.x * Facing < StageHalfWidth - 0.8f;
        if ((!b.through || !roomBehind) && !v.Airborne)
        {
            float gap = (v.pos.x - pos.x) * Facing;
            if (gap < 0.6f && gap > -0.2f) { pos.x = v.pos.x - Facing * 0.6f; if (vel.x * Facing > 0f) vel.x = 0f; }
        }

        if (BeatFrame >= b.frames)
        {
            BeatIndex++;
            BeatFrame = 0;
            beatHitDone = false;
            if (BeatIndex >= ActiveFinisher.beats.Length)
            {
                ActiveFinisher = null;
                finisherVictim = null;
                vel.x = 0f;
                SetState(Airborne ? FState.Jump : FState.Idle);
            }
            else if (!Airborne && CurrentBeat.dash == 0f) vel.x = 0f;
        }
    }

    /// <summary>Bitiricinin ara vuruşu: kurban sarsılır ama düşmez.</summary>
    void FinisherHit(Fighter attacker, float knock)
    {
        float away = Mathf.Abs(pos.x - attacker.pos.x) < 0.01f ? attacker.Facing : Mathf.Sign(pos.x - attacker.pos.x);
        SetState(FState.Hitstun);
        HitWhileCrouching = false;
        vel.x = away * knock;
        ComboCount++;
        OnComboTaken?.Invoke(this, ComboCount);
        FightFX.I?.OnFinisherHit(ToWorld(pos + new Vector2(-away * 0.3f, 1.25f)), attacker.mainColor);
    }

    /// <summary>Bitiricinin son vuruşu: asıl K.O.</summary>
    void FinisherKO(Fighter attacker, float knock, float launch)
    {
        float away = Mathf.Abs(pos.x - attacker.pos.x) < 0.01f ? attacker.Facing : Mathf.Sign(pos.x - attacker.pos.x);
        BeingFinished = false;
        ComboCount++;
        OnComboTaken?.Invoke(this, ComboCount);
        SetState(FState.KO);
        input?.Rumble(0.9f, 1f, 0.6f);
        vel = new Vector2(away * Mathf.Max(1.5f, knock), Mathf.Max(2.5f, launch));
        Vector3 fx = ToWorld(pos + new Vector2(-away * 0.3f, 1.2f));
        FightFX.I?.OnFinisherHit(fx, attacker.mainColor);
        FightFX.I?.OnKO(fx);
    }

    #endregion

    #region Fizik

    void Physics(float dt)
    {
        bool wasAir = Airborne;
        if (wasAir || vel.y > 0f) vel.y -= gravity * dt;
        pos += vel * dt;

        // Eşik Airborne ile aynı: y, 0 ile 0.0001 arasında kalırsa iniş kaçmasın.
        if (pos.y <= 0.0001f)
        {
            pos.y = 0f;
            if (wasAir) { vel.y = 0f; Land(); }
            else if (vel.y < 0f) vel.y = 0f;
        }

        if (!Airborne && State != FState.Walk)
            vel.x = Mathf.MoveTowards(vel.x, 0f, groundFriction * dt);

        pos.x = Mathf.Clamp(pos.x, -StageHalfWidth, StageHalfWidth);
    }

    void Land()
    {
        switch (State)
        {
            case FState.Jump:
                vel.x = 0f; SetState(FState.Idle); UpdateFacing();
                break;
            case FState.Attack:
                if (CurrentMove != null && CurrentMove.IsAir) { vel.x = 0f; SetState(FState.Idle); UpdateFacing(); }
                break;
            case FState.Finisher:
                vel.x = 0f;
                break;
            case FState.Hitstun:
                if (BeingFinished) { vel.x = 0f; break; }
                SetState(FState.Knockdown);
                FightFX.I?.OnBodyLand(ToWorld(pos));
                break;
            case FState.KO:
                FightFX.I?.OnBodyLand(ToWorld(pos));
                break;
        }
    }

    void UpdateFacing()
    {
        if (opponent == null) return;
        float dx = opponent.pos.x - pos.x;
        if (Mathf.Abs(dx) > 0.05f) Facing = dx > 0f ? 1 : -1;
    }

    #endregion

    #region Vuruş sistemi

    void TryHit(MoveData m)
    {
        if (opponent == null || !opponent.IsHittable) return;
        Rect hit = HitboxRect(m);
        Rect hurt = opponent.HurtboxRect();
        if (!hit.Overlaps(hurt)) return;

        moveHasHit = true;
        Vector2 contact = new Vector2(
            (Mathf.Max(hit.xMin, hurt.xMin) + Mathf.Min(hit.xMax, hurt.xMax)) * 0.5f,
            (Mathf.Max(hit.yMin, hurt.yMin) + Mathf.Min(hit.yMax, hurt.yMax)) * 0.5f);
        bool landed = opponent.ReceiveHit(this, m, contact);
        if (!m.isSuper) RegisterHit(landed);
    }

    public Rect HitboxRect(MoveData m)
    {
        Vector2 c = pos + new Vector2(m.hitboxOffset.x * Facing, m.hitboxOffset.y);
        return new Rect(c - m.hitboxSize * 0.5f, m.hitboxSize);
    }

    public Rect HurtboxRect()
    {
        const float w = 0.7f;
        float bottom = 0f, top = 1.8f;
        bool crouched = State == FState.Crouch
            || (State == FState.Blockstun && CrouchBlocking)
            || (State == FState.Attack && CurrentMove != null && CurrentMove.IsCrouch)
            || (State == FState.Hitstun && HitWhileCrouching && !Airborne);
        if (crouched) top = 1.15f;
        else if (Airborne) { bottom = 0.3f; top = 1.75f; }
        return new Rect(pos.x - w * 0.5f, pos.y + bottom, w, top - bottom);
    }

    public bool ReceiveHit(Fighter attacker, MoveData m, Vector2 contact)
    {
        float away = Mathf.Abs(pos.x - attacker.pos.x) < 0.01f ? attacker.Facing : Mathf.Sign(pos.x - attacker.pos.x);
        Vector3 fxPos = ToWorld(contact);

        bool canBlock = controlEnabled && !Airborne &&
            (State == FState.Idle || State == FState.Walk || State == FState.Crouch ||
             State == FState.Guard || State == FState.Blockstun);
        bool back = input.Horizontal * away > 0.1f;
        bool crouch = input.Down;
        bool blocked = canBlock && back &&
            (m.level == HitLevel.Mid ||
             (m.level == HitLevel.Low && crouch) ||
             (m.level == HitLevel.High && !crouch));

        if (blocked)
        {
            CrouchBlocking = crouch;
            SetState(FState.Blockstun);
            stunFrames = m.blockstun;
            vel.x = away * m.pushback;
            PushAttackerIfCornered(attacker, m.pushback, away);
            if (m.isSuper) Health = Mathf.Max(1, Health - Mathf.Max(1, Mathf.RoundToInt(m.damage * 0.25f * damageTaken)));
            FightFX.I?.OnBlock(fxPos, m);
            return false;
        }

        HitStreak = 0;
        streakTimer = 0;
        bool juggle = State == FState.Hitstun;
        ComboCount = juggle ? ComboCount + 1 : 1;
        float scale = m.isSuper ? 1f : Mathf.Max(0.4f, 1f - 0.12f * (ComboCount - 1));
        Health = Mathf.Max(0, Health - Mathf.Max(1, Mathf.RoundToInt(m.damage * scale * damageTaken)));
        input?.Rumble(m.IsHeavy || m.isSuper ? 0.5f : 0.2f, m.IsHeavy || m.isSuper ? 0.8f : 0.4f, m.IsHeavy ? 0.18f : 0.1f);
        OnComboTaken?.Invoke(this, ComboCount);

        bool wasAir = Airborne;
        HitWhileCrouching = !wasAir && (State == FState.Crouch ||
            (State == FState.Attack && CurrentMove != null && CurrentMove.IsCrouch));

        SetState(FState.Hitstun);
        stunFrames = m.hitstun;
        vel.x = away * m.pushback;

        if (Health <= 0)
        {
            if (attacker.finishers && attacker.State != FState.Finisher && attacker.opponent == this)
            {
                // Son vuruş: K.O. yerine saldıranın rengine özel bitirici başlar
                BeingFinished = true;
                controlEnabled = false;
                vel = new Vector2(away * 0.8f, Airborne ? Mathf.Min(vel.y, 2f) : 0f);
                FightFX.I?.OnHit(fxPos, m, ComboCount, attacker.mainColor);
                attacker.StartFinisher(this);
                return true;
            }
            SetState(FState.KO);
            input?.Rumble(0.9f, 1f, 0.6f);
            vel = new Vector2(away * 3.5f, 7.5f);
            FightFX.I?.OnKO(fxPos);
            return true;
        }

        if (wasAir || m.knockdown || m.launch > 0f)
        {
            vel.y = wasAir ? Mathf.Max(1.5f, 5.5f - ComboCount * 0.8f) : Mathf.Max(m.launch, 3f);
            vel.x = away * Mathf.Max(m.pushback, 2f);
        }
        else
        {
            PushAttackerIfCornered(attacker, m.pushback, away);
        }

        if (m.isSuper) FightFX.I?.OnHit(fxPos, m, ComboCount, attacker.mainColor);
        else FightFX.I?.OnHit(fxPos, m, ComboCount);
        return true;
    }

    void PushAttackerIfCornered(Fighter attacker, float push, float away)
    {
        if (Mathf.Abs(pos.x) > StageHalfWidth - 0.4f && !attacker.Airborne)
            attacker.vel.x = -away * push * 0.8f;
    }

    #endregion

    #region Yardımcılar

    public void ResetForRound(float x)
    {
        pos = new Vector2(x, 0f);
        vel = Vector2.zero;
        Health = maxHealth;
        ComboCount = 0;
        BeingFinished = false;
        ActiveFinisher = null;
        finisherVictim = null;
        FinisherApproaching = false;
        HitStreak = 0;
        streakTimer = 0;
        ComboReady = false;
        readyLeft = 0;
        CurrentMove = null;
        controlEnabled = false;
        Facing = x < 0f ? 1 : -1;
        SetState(FState.Idle);
        SyncTransform();
    }

    public void SetWin()
    {
        if (!Airborne && (State == FState.Idle || State == FState.Walk || State == FState.Crouch || State == FState.Guard))
        {
            SetState(FState.Win);
            WinTime = Time.unscaledTime;
        }
    }

    public void Nudge(float dx) { pos.x = Mathf.Clamp(pos.x + dx, -StageHalfWidth, StageHalfWidth); }

    public void SyncTransform() { transform.localPosition = new Vector3(pos.x, pos.y, 0f); }

    void SetState(FState s) { State = s; StateFrame = 0; }
    void ChangeState(FState s) { if (State != s) SetState(s); }

    Vector3 ToWorld(Vector2 p)
    {
        var local = new Vector3(p.x, p.y, -0.4f);
        return transform.parent != null ? transform.parent.TransformPoint(local) : local;
    }

    void OnDrawGizmos()
    {
        if (!Application.isPlaying) return;
        Gizmos.color = new Color(0f, 1f, 0f, 0.8f);
        DrawRect(HurtboxRect());
        if (State == FState.Attack && CurrentMove != null &&
            StateFrame > CurrentMove.startup && StateFrame <= CurrentMove.startup + CurrentMove.active)
        {
            Gizmos.color = Color.red;
            DrawRect(HitboxRect(CurrentMove));
        }
    }

    void DrawRect(Rect r) { Gizmos.DrawWireCube(ToWorld(r.center), new Vector3(r.width, r.height, 0.05f)); }

    #endregion
}