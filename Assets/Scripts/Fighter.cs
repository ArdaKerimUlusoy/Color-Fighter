using UnityEngine;

public enum FState { Idle, Walk, Crouch, Guard, Jump, Attack, Hitstun, Blockstun, Knockdown, KO, Win }

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

    [Header("Saldırılar")]
    public MoveData standPunch = new MoveData {
        name = "Jab", pose = AttackPose.StandPunch,
        startup = 4, active = 3, recovery = 8, damage = 6, level = HitLevel.Mid,
        hitboxOffset = new Vector2(0.75f, 1.38f), hitboxSize = new Vector2(0.6f, 0.3f),
        hitstun = 14, blockstun = 10, hitstop = 6, pushback = 3f, lunge = 0.6f, cancelable = true };

    public MoveData standKick = new MoveData {
        name = "Kick", pose = AttackPose.StandKick,
        startup = 9, active = 4, recovery = 16, damage = 14, level = HitLevel.Mid,
        hitboxOffset = new Vector2(1.0f, 0.95f), hitboxSize = new Vector2(0.8f, 0.35f),
        hitstun = 20, blockstun = 14, hitstop = 10, pushback = 4.5f, lunge = 1.2f };

    public MoveData crouchPunch = new MoveData {
        name = "Crouch Jab", pose = AttackPose.CrouchPunch,
        startup = 5, active = 3, recovery = 9, damage = 5, level = HitLevel.Mid,
        hitboxOffset = new Vector2(0.7f, 0.95f), hitboxSize = new Vector2(0.6f, 0.3f),
        hitstun = 13, blockstun = 9, hitstop = 6, pushback = 2.5f, cancelable = true };

    public MoveData crouchKick = new MoveData {
        name = "Sweep", pose = AttackPose.CrouchKick,
        startup = 10, active = 4, recovery = 22, damage = 12, level = HitLevel.Low,
        hitboxOffset = new Vector2(0.9f, 0.2f), hitboxSize = new Vector2(0.9f, 0.3f),
        hitstun = 30, blockstun = 12, hitstop = 10, pushback = 2f, knockdown = true, launch = 3f };

    public MoveData airPunch = new MoveData {
        name = "Air Punch", pose = AttackPose.AirPunch,
        startup = 5, active = 8, recovery = 6, damage = 8, level = HitLevel.High,
        hitboxOffset = new Vector2(0.6f, 1.1f), hitboxSize = new Vector2(0.6f, 0.5f),
        hitstun = 16, blockstun = 12, hitstop = 7, pushback = 2.5f };

    public MoveData airKick = new MoveData {
        name = "Air Kick", pose = AttackPose.AirKick,
        startup = 7, active = 10, recovery = 6, damage = 12, level = HitLevel.High,
        hitboxOffset = new Vector2(0.7f, 0.6f), hitboxSize = new Vector2(0.7f, 0.45f),
        hitstun = 18, blockstun = 14, hitstop = 9, pushback = 3f };

    [Header("Bağlantılar")]
    public Fighter opponent;
    public FighterInput input;
    [HideInInspector] public bool controlEnabled;

    #endregion

    #region Durum

    public System.Action<Fighter, int> OnComboTaken;

    public FState State { get; private set; }
    public int StateFrame { get; private set; }
    public MoveData CurrentMove { get; private set; }
    public int Health { get; private set; }
    public int Facing { get; private set; } = 1;
    public int ComboCount { get; private set; }
    public bool CrouchBlocking { get; private set; }
    public bool HitWhileCrouching { get; private set; }

    public float X => pos.x;
    public float Y => pos.y;
    public bool Airborne => pos.y > 0.0001f;
    public bool HoldingBack => controlEnabled && input != null && input.Horizontal * Facing < -0.1f;
    public bool IsHittable => State != FState.Knockdown && State != FState.KO && State != FState.Win;

    Vector2 pos, vel;
    int stunFrames;
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
                if (!Airborne && --stunFrames <= 0) SetState(FState.Idle);
                break;

            case FState.Blockstun:
                if (--stunFrames <= 0) SetState(FState.Idle);
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
        if (f == m.startup + 1) FightFX.I?.OnWhiff(m);

        bool active = f > m.startup && f <= m.startup + m.active;
        if (active && !moveHasHit) TryHit(m);

        if (moveHasHit && m.cancelable && controlEnabled && f > m.startup && input.ConsumeKick())
        {
            StartMove(m.IsCrouch ? crouchKick : standKick);
            return;
        }

        if (f >= m.Total) SetState(Airborne ? FState.Jump : FState.Idle);
    }

    #endregion

    #region Fizik

    void Physics(float dt)
    {
        bool wasAir = Airborne;
        if (wasAir || vel.y > 0f) vel.y -= gravity * dt;
        pos += vel * dt;

        if (pos.y <= 0f)
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
            case FState.Hitstun:
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
        opponent.ReceiveHit(this, m, contact);
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

    public void ReceiveHit(Fighter attacker, MoveData m, Vector2 contact)
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
            FightFX.I?.OnBlock(fxPos, m);
            return;
        }

        bool juggle = State == FState.Hitstun;
        ComboCount = juggle ? ComboCount + 1 : 1;
        float scale = Mathf.Max(0.4f, 1f - 0.12f * (ComboCount - 1));
        Health = Mathf.Max(0, Health - Mathf.Max(1, Mathf.RoundToInt(m.damage * scale)));
        OnComboTaken?.Invoke(this, ComboCount);

        bool wasAir = Airborne;
        HitWhileCrouching = !wasAir && (State == FState.Crouch ||
            (State == FState.Attack && CurrentMove != null && CurrentMove.IsCrouch));

        SetState(FState.Hitstun);
        stunFrames = m.hitstun;
        vel.x = away * m.pushback;

        if (Health <= 0)
        {
            SetState(FState.KO);
            vel = new Vector2(away * 3.5f, 7.5f);
            FightFX.I?.OnKO(fxPos);
            return;
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

        FightFX.I?.OnHit(fxPos, m, ComboCount);
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
        CurrentMove = null;
        controlEnabled = false;
        Facing = x < 0f ? 1 : -1;
        SetState(FState.Idle);
        SyncTransform();
    }

    public void SetWin()
    {
        if (!Airborne && (State == FState.Idle || State == FState.Walk || State == FState.Crouch || State == FState.Guard))
            SetState(FState.Win);
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
