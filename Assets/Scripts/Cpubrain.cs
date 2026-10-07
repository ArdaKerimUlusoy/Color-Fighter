using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 1P VS CPU modunda P2'yi oynatýr. Hile yapmaz: oyuncuyla ayný karakter, ayný hareketler,
/// ayný can. Sadece FighterInput'un sanal tuþlarýna basar; rakibi insan gibi gecikmeyle algýlar.
/// </summary>
public class CpuBrain : MonoBehaviour
{
    #region Ayarlar

    public Fighter self;

    [Range(0f, 1f), Tooltip("0 = kolay, 0.5 = ortalama bir oyuncu, 1 = zor.")]
    public float difficulty = 0.5f;

    #endregion

    #region Durum

    struct Seen
    {
        public FState state;
        public MoveData move;
        public int stateFrame;
        public float x, y;
    }

    readonly Queue<Seen> perception = new Queue<Seen>();
    readonly System.Random rng = new System.Random();
    FighterInput input;

    int holdDir, holdDirFrames, holdDown, holdDownFrames;
    int pressPunch, pressKick, pressCombo, pressUp;
    int actCooldown, planFrames;
    int reactedAttackFrame = -1, reactedJumpFrame = -1;
    MoveData reactedMove;
    bool blockingThisAttack, crouchBlockThisAttack;
    int lastSelfHealth;

    // Rakibi tanýma: ne sýk saldýrýyor, ne kadarý alçak (süpürme), yaklaþýyor mu
    float aggression, lowRatio = 0.3f, lastSeenX;
    MoveData lastSeenMove;
    int lastSeenFrame;
    bool counterReady;

    int ReactionFrames => Mathf.RoundToInt(Mathf.Lerp(22f, 10f, difficulty));  // ~0.25 sn ortalama insan tepkisi
    float BlockChance => Mathf.Lerp(0.3f, 0.8f, difficulty);
    float AntiAirChance => Mathf.Lerp(0.2f, 0.7f, difficulty);
    float CancelChance => Mathf.Lerp(0.2f, 0.75f, difficulty);
    bool Roll(float p) => rng.NextDouble() < p;
    int Range(int a, int b) => rng.Next(a, b);

    void Awake()
    {
        if (self == null) self = GetComponent<Fighter>();
    }

    void OnDisable()
    {
        ReleaseAll();
        perception.Clear();
    }

    #endregion

    #region Ana döngü

    void FixedUpdate()
    {
        if (self == null || self.opponent == null) return;
        if (input == null) input = self.input;
        if (input == null || !input.cpuControlled) return;
        if (MatchManager.Paused) return;
        if (FightFX.I != null && FightFX.I.Hitstop > 0) return;   // vuruþ donmasýnda herkes gibi bekle

        Perceive();

        if (!self.controlEnabled)
        {
            ReleaseAll();
            Emit();
            return;
        }

        TickHolds();
        if (perception.Count > ReactionFrames)
            Think(perception.Peek());
        Emit();
    }

    void Perceive()
    {
        var o = self.opponent;
        perception.Enqueue(new Seen { state = o.State, move = o.CurrentMove, stateFrame = o.StateFrame, x = o.X, y = o.Y });
        while (perception.Count > ReactionFrames + 1) perception.Dequeue();
    }

    #endregion

    #region Karar

    void Think(Seen o)
    {
        float dx = o.x - self.X;
        float dist = Mathf.Abs(dx);
        int toward = dx >= 0f ? 1 : -1;
        bool free = self.State == FState.Idle || self.State == FState.Walk || self.State == FState.Crouch || self.State == FState.Guard;

        if (actCooldown > 0) actCooldown--;
        Learn(o, dist);
        bool approaching = Mathf.Abs(o.x - self.X) < Mathf.Abs(lastSeenX - self.X) - 0.005f;
        lastSeenX = o.x;

        // Yediðin darbe tempoyu bozar: kýsa bir duraksama (insan gibi)
        if (self.Health < lastSelfHealth) actCooldown = Mathf.Max(actCooldown, Range(6, 16));
        lastSelfHealth = self.Health;

        // 1) Kendi isabetini kombola: jab isabet edince tekmeyle iptal et, kombo hazýrsa Color Rush'a baðla
        if (self.State == FState.Attack && self.CurrentMove != null && !self.CurrentMove.isSuper)
        {
            bool oppHurt = self.opponent.State == FState.Hitstun;   // kendi vuruþunun sonucunu hissedersin
            if (oppHurt && self.ComboReady && Roll(CancelChance)) Press(ref pressCombo);
            else if (oppHurt && self.CurrentMove.cancelable && Roll(CancelChance * 0.6f)) Press(ref pressKick);
            return;
        }
        if (self.State == FState.Blockstun) counterReady = true;
        if (!free) { holdDir = 0; return; }

        // 1b) Blok yaptýktan sonra rakip toparlanýrken karþý saldýrý
        if (counterReady)
        {
            counterReady = false;
            if (dist < 1.5f && Roll(Mathf.Lerp(0.35f, 0.85f, difficulty)))
            {
                Press(ref pressPunch);
                actCooldown = Range(10, 20);
                return;
            }
        }

        // 2) Rakip saldýrýyor: (bazen) blok yap. Her saldýrý için bir kez karar verilir.
        bool oppAttacking = o.state == FState.Attack && o.move != null && !o.move.IsAir;
        if (oppAttacking && dist < 2.8f)
        {
            if (reactedMove != o.move || o.stateFrame < reactedAttackFrame)
            {
                reactedMove = o.move;
                reactedAttackFrame = o.stateFrame;
                blockingThisAttack = Roll(BlockChance);
                crouchBlockThisAttack = o.move.level == HitLevel.Low || (o.move.level == HitLevel.Mid && Roll(0.3f));
            }
            if (blockingThisAttack)
            {
                Hold(-toward, 6);
                if (crouchBlockThisAttack) HoldDown(6);
                return;
            }
        }
        else reactedMove = null;

        // 3) Rakip üstüne zýplýyor: anti-air
        bool oppJumping = o.y > 0.3f && (o.state == FState.Jump || (o.state == FState.Attack && o.move != null && o.move.IsAir));
        if (oppJumping && dist < 2.6f)
        {
            if (reactedJumpFrame < 0)
            {
                reactedJumpFrame = 1;
                if (Roll(AntiAirChance)) { Press(ref pressKick); actCooldown = Range(14, 24); return; }
                if (Roll(BlockChance)) { Hold(-toward, 12); return; }
            }
        }
        else reactedJumpFrame = -1;

        // 4) Rakip ýskaladý ve toparlanýyor: cezalandýr
        bool oppRecovering = o.state == FState.Attack && o.move != null && o.stateFrame > o.move.startup + o.move.active;
        if (oppRecovering && dist < 1.7f && actCooldown <= 0 && Roll(Mathf.Lerp(0.2f, 0.7f, difficulty)))
        {
            Attack(dist, true);
            return;
        }

        // 4b) Üstüne yürüyen rakibi tekme menzilinde karþýla
        if (approaching && dist > 1.35f && dist < 2.05f && actCooldown <= 0 && Roll(Mathf.Lerp(0.03f, 0.09f, difficulty)))
        {
            Press(ref pressKick);
            actCooldown = Range(18, 32);
            return;
        }

        // 5) Kombo hakký varken yakýndaysa kullan
        if (self.ComboReady && dist < 1.9f && actCooldown <= 0 && Roll(0.08f + 0.1f * difficulty))
        {
            Press(ref pressCombo);
            actCooldown = Range(30, 50);
            return;
        }

        // 6) Nötr oyun: mesafe kontrolü ve dürtme
        if (--planFrames > 0) return;
        planFrames = Range(6, 16);

        // Blok tuttuktan sonra sýra saldýrýda
        // Saldýrgan rakibe karþý: orta mesafede bekle, yakýna gelince hýzlý jab ile kes
        // Saldýrgan rakibe karþý menzildeyken önceden blok tut (insanlarýn yaptýðý gibi)
        float guard = Mathf.Clamp01(aggression * 0.15f);
        if (dist < 2.2f && Roll(guard))
        {
            Hold(-toward, Range(8, 16));
            if (Roll(lowRatio)) HoldDown(Range(8, 16));
            return;
        }

        if (dist > 2.3f)
        {
            if (Roll(0.035f + 0.03f * difficulty) && Mathf.Abs(self.X) < Fighter.StageHalfWidth - 0.3f) { Jump(toward); return; }
            if (Roll(0.8f)) Hold(toward, Range(10, 24)); else Hold(0, Range(6, 14));
        }
        else if (dist > 1.25f)
        {
            if (actCooldown <= 0 && Roll(0.3f)) { Attack(dist, false); return; }
            float r = (float)rng.NextDouble();
            if (r < 0.55f) Hold(toward, Range(6, 14));
            else if (r < 0.75f) Hold(-toward, Range(5, 10));
            else Hold(0, Range(5, 12));
        }
        else
        {
            if (actCooldown <= 0 && Roll(0.55f)) { Attack(dist, false); return; }
            float r = (float)rng.NextDouble();
            if (r < 0.35f) { Hold(-toward, Range(4, 10)); HoldDown(Range(4, 10)); }   // çömelip bekle
            else if (r < 0.6f) Hold(-toward, Range(5, 10));
            else Hold(0, Range(4, 8));
        }

        // Duvara sýkýþtýysa bazen zýplayarak çýk
        if (Mathf.Abs(self.X) > Fighter.StageHalfWidth - 0.4f && Mathf.Sign(self.X) == -toward && Roll(0.15f)) Jump(toward);
    }

    void Learn(Seen o, float dist)
    {
        aggression *= 0.995f;   // yarý ömür ~2.3 sn
        bool newAttack = o.state == FState.Attack && o.move != null &&
                         (o.move != lastSeenMove || o.stateFrame < lastSeenFrame);
        if (newAttack && dist < 3f)
        {
            aggression += 1f;
            lowRatio = Mathf.Lerp(lowRatio, o.move.level == HitLevel.Low ? 1f : 0f, 0.2f);
        }
        lastSeenMove = o.state == FState.Attack ? o.move : null;
        lastSeenFrame = o.stateFrame;
    }

    void Attack(float dist, bool punish)
    {
        float r = (float)rng.NextDouble();
        if (dist > 1.25f)
        {
            Press(ref pressKick);                                   // uzun menzil: tekme
        }
        else if (r < 0.5f)
        {
            Press(ref pressPunch);                                  // jab
        }
        else if (r < 0.7f)
        {
            HoldDown(4); Press(ref pressPunch);                     // çömelik jab
        }
        else if (r < 0.88f || punish)
        {
            HoldDown(4); Press(ref pressKick);                      // süpürme (yere düþürür)
        }
        else
        {
            Press(ref pressKick);
        }
        actCooldown = Range(Mathf.RoundToInt(Mathf.Lerp(30, 12, difficulty)), Mathf.RoundToInt(Mathf.Lerp(55, 30, difficulty)));
        planFrames = 0;
    }

    void Jump(int dir)
    {
        Hold(dir, 4);
        Press(ref pressUp);
        actCooldown = Range(20, 35);
    }

    #endregion

    #region Sanal tuþlar

    void Hold(int dir, int frames) { holdDir = dir; holdDirFrames = frames; }
    void HoldDown(int frames) { holdDown = 1; holdDownFrames = frames; }

    // Tuþu kýsa süre basýlý tut (FighterInput yeni basýþý yakalasýn), sonra býrak
    static void Press(ref int counter) { if (counter <= -2) counter = 3; }

    void TickHolds()
    {
        if (holdDirFrames > 0 && --holdDirFrames == 0) holdDir = 0;
        if (holdDownFrames > 0 && --holdDownFrames == 0) holdDown = 0;
    }

    void Emit()
    {
        input.cpuLeft = holdDir < 0;
        input.cpuRight = holdDir > 0;
        input.cpuDown = holdDown > 0;
        input.cpuPunch = Step(ref pressPunch);
        input.cpuKick = Step(ref pressKick);
        input.cpuCombo = Step(ref pressCombo);
        input.cpuUp = Step(ref pressUp);
    }

    static bool Step(ref int counter)
    {
        bool held = counter > 0;
        if (counter > -2) counter--;
        return held;
    }

    void ReleaseAll()
    {
        holdDir = 0; holdDown = 0; holdDirFrames = holdDownFrames = 0;
        pressPunch = pressKick = pressCombo = pressUp = -2;
        planFrames = 0;
        if (input != null)
            input.cpuLeft = input.cpuRight = input.cpuDown = input.cpuUp = input.cpuPunch = input.cpuKick = input.cpuCombo = false;
    }

    #endregion
}