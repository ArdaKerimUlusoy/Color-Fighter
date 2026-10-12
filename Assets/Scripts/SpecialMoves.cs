using UnityEngine;

/// <summary>
/// Her rengin kendine özel hareketi (↓ → + YUMRUK) ve fırlatma hareketleri.
/// Stil = palet sırası: 0 RED, 1 BLUE, 2 GREEN, 3 YELLOW, 4 PURPLE, 5 ORANGE, 6 CYAN, 7 PINK.
/// Pozlar FighterRig tablosuyla aynı: {kalçaY, gövde, kafa, Lomuz, Ldirsek, Romuz, Rdirsek, Lkalça, Ldiz, Rkalça, Rdiz, kalçaEğimi}
/// </summary>
public static class SpecialMoves
{
    public enum Kind { FlameFist, ShoulderCharge, CycloneKick, ThunderKnee, ShadowStep, MeteorSlam, PlasmaShot, SakuraFlip }

    public static readonly string[] Names =
    {
        "FLAME FIST", "SHOULDER CHARGE", "CYCLONE KICK", "THUNDER KNEE",
        "SHADOW STEP", "METEOR SLAM", "PLASMA SHOT", "SAKURA FLIP",
    };

    /// <summary>Hareketin kısa açıklaması (tutorial kartında).</summary>
    public static readonly string[] Descs =
    {
        "LUNGING FIRE PUNCH", "DASHING SHOULDER TACKLE", "SPINNING MULTI-HIT KICK", "JUMPING ANTI-AIR KNEE",
        "TELEPORT BEHIND + BACKFIST", "LEAP AND GROUND SLAM (LOW)", "ENERGY PROJECTILE", "SOMERSAULT ANTI-AIR KICK",
    };

    public static string NameOf(int style) => Names[Mod(style)];
    public static string DescOf(int style) => Descs[Mod(style)];
    static int Mod(int s) => ((s % 8) + 8) % 8;

    /// <summary>Mermi hızı (m/sn) ve vuruşu (PLASMA SHOT).</summary>
    public const float ProjectileSpeed = 6.5f;

    public static MoveData Create(int style, Color main)
    {
        var k = (Kind)Mod(style);
        MoveData m;
        switch (k)
        {
            case Kind.FlameFist:
                m = new MoveData
                {
                    startup = 9, active = 6, recovery = 18, damage = 14, level = HitLevel.Mid,
                    hitboxOffset = new Vector2(0.95f, 1.35f), hitboxSize = new Vector2(0.85f, 0.45f),
                    hitstun = 30, blockstun = 16, hitstop = 12, pushback = 5f, lunge = 7f, knockdown = true, launch = 4f,
                };
                m.windPose = new float[] { 0.86f, -5, -5, -55, -95, 30, -130, -35, 40, 25, 30, 0 };
                m.hitPose = new float[] { 0.80f, 30, -15, -40, -90, -92, 0, -55, 50, 45, 10, 0 };
                m.fxColor = new Color(1f, 0.45f, 0.1f);
                break;

            case Kind.ShoulderCharge:
                m = new MoveData
                {
                    startup = 8, active = 12, recovery = 16, damage = 12, level = HitLevel.Mid,
                    hitboxOffset = new Vector2(0.6f, 1.3f), hitboxSize = new Vector2(0.75f, 0.9f),
                    hitstun = 26, blockstun = 18, hitstop = 10, pushback = 6f, lunge = 9f, knockdown = true, launch = 3f,
                };
                m.windPose = new float[] { 0.85f, 25, -10, -70, -110, -40, -120, -40, 45, 20, 35, 0 };
                m.hitPose = new float[] { 0.82f, 40, -20, -20, -100, -30, -120, -60, 40, 35, 20, 0 };
                m.fxColor = new Color(0.45f, 0.7f, 1f);
                break;

            case Kind.CycloneKick:
                m = new MoveData
                {
                    startup = 6, active = 18, recovery = 14, damage = 5, level = HitLevel.Mid,
                    hitboxOffset = new Vector2(0.75f, 1.0f), hitboxSize = new Vector2(1.1f, 0.6f),
                    hitstun = 20, blockstun = 10, hitstop = 6, pushback = 1.2f, lunge = 4f,
                };
                m.hits = 3; m.hitEvery = 6; m.spin = 720f; m.hop = 3.5f;
                m.windPose = new float[] { 0.95f, -5, -5, -45, -95, -20, -110, -15, 20, -75, 110, 0 };
                m.hitPose = new float[] { 1.0f, -15, 5, -20, -60, -20, -60, -10, 10, -95, 0, 0 };
                m.fxColor = new Color(0.4f, 1f, 0.5f);
                break;

            case Kind.ThunderKnee:
                m = new MoveData
                {
                    startup = 5, active = 10, recovery = 16, damage = 13, level = HitLevel.Mid,
                    hitboxOffset = new Vector2(0.6f, 1.5f), hitboxSize = new Vector2(0.65f, 0.75f),
                    hitstun = 30, blockstun = 14, hitstop = 10, pushback = 3f, lunge = 4.5f, knockdown = true, launch = 6f,
                };
                m.hop = 7f;
                m.windPose = new float[] { 0.6f, 20, -10, -60, -100, -35, -110, -70, 110, -15, 90, 0 };
                m.hitPose = new float[] { 1.0f, -10, 10, -40, -100, -40, -100, -100, 120, 20, 30, 0 };
                m.fxColor = new Color(1f, 0.95f, 0.3f);
                break;

            case Kind.ShadowStep:
                // 1-14. frame: kaybolur (dokunulmaz), 14. frame rakibin arkasında belirir, sonra ters yumruk
                m = new MoveData
                {
                    startup = 18, active = 4, recovery = 14, damage = 10, level = HitLevel.Mid,
                    hitboxOffset = new Vector2(0.75f, 1.4f), hitboxSize = new Vector2(0.65f, 0.4f),
                    hitstun = 22, blockstun = 12, hitstop = 9, pushback = 3f,
                };
                m.windPose = new float[] { 0.90f, 2, -6, -55, -95, 15, -125, -28, 32, 20, 28, 0 };
                m.hitPose = new float[] { 0.88f, -10, -10, -50, -95, -75, -10, -40, 30, 30, 15, 0 };
                m.fxColor = new Color(0.7f, 0.35f, 1f);
                break;

            case Kind.MeteorSlam:
                // Zıplar, inişte yere vurur: aşağı (LOW) vuruş, çömelerek bloklanır
                m = new MoveData
                {
                    startup = 27, active = 8, recovery = 18, damage = 15, level = HitLevel.Low,
                    hitboxOffset = new Vector2(0.55f, 0.3f), hitboxSize = new Vector2(2.0f, 0.7f),
                    hitstun = 30, blockstun = 14, hitstop = 12, pushback = 3.5f, lunge = 3.5f, knockdown = true, launch = 4f,
                };
                m.hop = 8f;
                m.windPose = new float[] { 0.95f, -15, -5, -170, -10, -170, -10, -60, 100, -60, 100, 0 };
                m.hitPose = new float[] { 0.55f, 35, -15, -40, -20, -40, -20, -70, 110, -10, 85, 0 };
                m.fxColor = new Color(1f, 0.6f, 0.15f);
                break;

            case Kind.PlasmaShot:
                // Vuruşu mermi yapar (hareketin kendi hitbox'ı yok)
                m = new MoveData
                {
                    startup = 10, active = 2, recovery = 22, damage = 0, level = HitLevel.Mid,
                    hitboxOffset = Vector2.zero, hitboxSize = Vector2.zero,
                    hitstun = 0, blockstun = 0, hitstop = 0, pushback = 0f,
                };
                m.windPose = new float[] { 0.88f, -10, -5, -40, -120, -40, -120, -30, 35, 25, 25, 0 };
                m.hitPose = new float[] { 0.86f, 15, -8, -88, -10, -88, -10, -35, 35, 30, 20, 0 };
                m.fxColor = new Color(0.3f, 0.95f, 1f);
                break;

            default: // SakuraFlip
                m = new MoveData
                {
                    startup = 4, active = 10, recovery = 18, damage = 12, level = HitLevel.Mid,
                    hitboxOffset = new Vector2(0.7f, 1.6f), hitboxSize = new Vector2(0.9f, 1.0f),
                    hitstun = 30, blockstun = 14, hitstop = 10, pushback = 3f, lunge = 1.5f, knockdown = true, launch = 7f,
                };
                m.hop = 9f; m.flip = -360f;
                m.windPose = new float[] { 0.70f, 15, -10, -60, -100, -40, -110, -60, 100, -20, 80, 0 };
                m.hitPose = new float[] { 0.95f, -25, 5, -35, -90, 10, -100, 5, 15, -120, 0, 0 };
                m.fxColor = new Color(1f, 0.6f, 0.8f);
                break;
        }
        m.name = Names[(int)k];
        m.pose = AttackPose.Special;
        m.isSpecial = true;
        m.specialKind = (int)k;
        return m;
    }

    /// <summary>PLASMA SHOT mermisinin vuruş değerleri.</summary>
    public static MoveData ProjectileHit(Color c)
    {
        var m = new MoveData
        {
            name = "Plasma", pose = AttackPose.Special, startup = 0, active = 1, recovery = 0, damage = 9, level = HitLevel.Mid,
            hitstun = 18, blockstun = 14, hitstop = 7, pushback = 3f,
        };
        m.isSpecial = true;
        m.isProjectile = true;
        m.specialKind = (int)Kind.PlasmaShot;
        m.fxColor = c;
        return m;
    }

    #region Fırlatma

    /// <summary>Tutma: rakibi yakalayıp kısa süre tutar (aktif süre = tutma süresi, sonunda fırlatır).</summary>
    public static MoveData ThrowGrab()
    {
        var m = new MoveData { name = "Throw", pose = AttackPose.Throw, startup = 3, active = 20, recovery = 1, damage = 16, hitstop = 10, pushback = 4f };
        m.isThrow = true;
        m.windPose = new float[] { 0.90f, 15, -5, -80, -40, -80, -40, -30, 35, 25, 25, 0 };
        m.hitPose = new float[] { 0.88f, 5, -5, -60, -90, -60, -90, -30, 35, 25, 25, 0 };
        return m;
    }

    /// <summary>Savurma: tutulan rakibi fırlatma hareketi.</summary>
    public static MoveData ThrowToss()
    {
        var m = new MoveData { name = "Toss", pose = AttackPose.Throw, startup = 1, active = 4, recovery = 16 };
        m.isThrow = true;
        m.windPose = new float[] { 0.88f, 5, -5, -60, -90, -60, -90, -30, 35, 25, 25, 0 };
        m.hitPose = new float[] { 0.86f, 35, -15, -120, -20, -120, -20, -50, 50, 40, 15, 0 };
        return m;
    }

    /// <summary>Boşa tutma: menzil dışında ya da tutulamayan rakipte; cezalandırılabilir.</summary>
    public static MoveData ThrowWhiff()
    {
        var m = new MoveData { name = "Throw Miss", pose = AttackPose.Throw, startup = 3, active = 3, recovery = 20 };
        m.isThrow = true;
        m.windPose = new float[] { 0.90f, 15, -5, -80, -40, -80, -40, -30, 35, 25, 25, 0 };
        m.hitPose = new float[] { 0.88f, 25, -10, -90, -10, -90, -10, -40, 40, 30, 20, 0 };
        return m;
    }

    #endregion
}
