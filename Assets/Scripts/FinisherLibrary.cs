using UnityEngine;

/// <summary>
/// Her rengin (karakterin) kendine özel bitirici hareketi ve zafer sevinci.
/// Ýndeks = FighterPalette sýrasý: 0 RED, 1 BLUE, 2 GREEN, 3 YELLOW, 4 PURPLE, 5 ORANGE, 6 CYAN, 7 PINK.
/// Pozlar FighterRig formatýnda: { kalçaY, gövde, kafa, Lomuz, Ldirsek, Romuz, Rdirsek, Lkalça, Ldiz, Rkalça, Rdiz, kalçaEðimi }
///   omuz: 0 aþaðý sarkýk, -90 öne düz, -180 yukarý | dirsek: 0 düz, -95 bükük | kalça: negatif = bacak öne | diz: pozitif = bükük
/// </summary>
public static class FinisherLibrary
{
    /// <summary>Bitiricinin bir adýmý.</summary>
    public struct Beat
    {
        public float[] pose;
        public int frames;
        public float dash;     // adým baþýnda ileri hýz (m/sn)
        public float hop;      // adým baþýnda yukarý hýz
        public float spin;     // adým boyunca dikey eksende dönüþ (derece)
        public float flip;     // adým boyunca takla (derece, + öne)
        public int hit;        // 0 yok, 1 vuruþ, 2 son vuruþ (K.O.)
        public float hitAt;    // adýmýn yüzde kaçýnda vurur (0-1)
        public float knock;    // rakibi geri itme
        public float launch;   // son vuruþta rakibi havaya kaldýrma
        public bool through;   // rakibin içinden geçebilir
        public bool snap;      // poz anýnda otursun
    }

    public class Finisher
    {
        public string name;
        public Beat[] beats;
    }

    #region Pozlar

    static readonly float[] GUARD = { 0.92f, 8, -6, -55, -95, -30, -115, -28, 32, 20, 28, 0 };
    static readonly float[] JAB_L = { 0.88f, 18, -10, -92, -5, -40, -110, -35, 30, 25, 20, 0 };
    static readonly float[] JAB_R = { 0.88f, 22, -12, -50, -95, -90, -5, -40, 30, 30, 15, 0 };
    static readonly float[] WIND_R = { 0.90f, 0, -6, -55, -95, 20, -130, -28, 32, 20, 28, 0 };
    static readonly float[] POWER_R = { 0.82f, 34, -15, -30, -100, -94, 0, -58, 42, 48, 8, 0 };
    static readonly float[] COIL = { 0.54f, 26, -15, -60, -110, 30, -140, -72, 112, -20, 90, 0 };
    static readonly float[] UPPERCUT = { 1.00f, -10, 15, -40, -100, -176, -5, -40, 60, 10, 20, 0 };
    static readonly float[] SPIN_WIND = { 0.95f, -5, -5, -70, -110, -70, -110, -40, 60, -30, 60, 0 };
    static readonly float[] ROUNDHOUSE = { 0.98f, -20, 5, -30, -80, 10, -90, 0, 10, -102, 5, 0 };
    static readonly float[] HIGH_L = { 1.00f, -22, 8, -20, -60, 20, -60, -120, 0, 10, 10, -10 };
    static readonly float[] HIGH_R = { 1.00f, -22, 8, 20, -60, -20, -60, 10, 10, -120, 0, -10 };
    static readonly float[] TUCK = { 0.95f, 30, -10, -80, -120, -80, -120, -100, 130, -100, 130, 0 };
    static readonly float[] AXE_UP = { 1.00f, -15, 10, -150, -20, -40, -90, -10, 10, -170, 0, 0 };
    static readonly float[] AXE_DOWN = { 0.82f, 32, -10, -40, -90, -40, -90, -20, 20, -55, 0, 0 };
    static readonly float[] KNEE = { 0.95f, -5, 5, -80, -110, -80, -110, -20, 25, -112, 140, 0 };
    static readonly float[] ELBOW = { 0.90f, 26, -10, -60, -100, -100, -145, -35, 30, 30, 15, 0 };
    static readonly float[] HAMMER_UP = { 0.98f, -15, 15, -176, -30, -176, -30, -20, 25, 20, 25, 0 };
    static readonly float[] HAMMER_DN = { 0.84f, 36, -15, -82, -40, -82, -40, -40, 40, 35, 20, 0 };
    static readonly float[] DASH = { 0.80f, 42, -20, 25, -60, 35, -50, -60, 60, 40, 30, 0 };
    static readonly float[] SLASH = { 0.86f, 15, -5, -96, -5, 30, -60, -50, 50, 40, 30, 0 };
    static readonly float[] COOL = { 0.93f, -2, -12, -10, -20, -10, -20, -5, 5, 5, 5, 0 };
    static readonly float[] BACKFIST = { 0.90f, -8, -5, -60, -100, -75, -8, -25, 30, 20, 28, 0 };
    static readonly float[] FLYKICK = { 0.95f, -25, 5, -60, -100, -40, -90, -10, 45, -96, 0, -15 };

    #endregion

    #region Bitiriciler

    static Beat B(float[] pose, int frames) => new Beat { pose = pose, frames = frames, hitAt = 0.3f, knock = 0.6f };
    static Beat Hit(float[] pose, int frames, float knock = 0.6f) => new Beat { pose = pose, frames = frames, hit = 1, hitAt = 0.3f, knock = knock, snap = true };
    static Beat Final(float[] pose, int frames, float knock, float launch) => new Beat { pose = pose, frames = frames, hit = 2, hitAt = 0.3f, knock = knock, launch = launch, snap = true };

    public static readonly Finisher[] Finishers =
    {
        // 0 RED — Inferno Rush: ateþli yumruk yaðmuru, sonunda dev düz yumruk
        new Finisher { name = "INFERNO RUSH", beats = new[] {
            B(WIND_R, 8),
            Hit(JAB_L, 6), Hit(JAB_R, 6), Hit(JAB_L, 6), Hit(JAB_R, 6), Hit(JAB_L, 5), Hit(JAB_R, 5),
            B(WIND_R, 12),
            WithDash(Final(POWER_R, 22, 6f, 6f), 4f),
            B(POWER_R, 20), B(GUARD, 16) } },

        // 1 BLUE — Tidal Spin: iki dönen tekme, sonra dönerek yükselen tekme
        new Finisher { name = "TIDAL SPIN", beats = new[] {
            B(SPIN_WIND, 10),
            WithSpin(Hit(ROUNDHOUSE, 16, 0.4f), 360f),
            WithSpin(Hit(ROUNDHOUSE, 16, 0.4f), 360f),
            B(COIL, 10),
            WithHop(WithSpin(Final(HIGH_R, 22, 3f, 9f), 360f), 7.5f),
            B(TUCK, 24), B(GUARD, 14) } },

        // 2 GREEN — Jade Dragon: çömelip burgu gibi dönerek yükselen aparkat
        new Finisher { name = "JADE DRAGON", beats = new[] {
            B(COIL, 18),
            WithHop(WithSpin(Hit(UPPERCUT, 8, 0.2f), 360f), 9.5f),
            WithSpin(Final(UPPERCUT, 16, 1.5f, 11f), 360f),
            B(TUCK, 22), B(GUARD, 18) } },

        // 3 YELLOW — Thunder Drop: yüksek zýplayýp takla, yýldýrým gibi topuk indirme
        new Finisher { name = "THUNDER DROP", beats = new[] {
            B(COIL, 12),
            WithDash(WithHop(WithFlip(B(TUCK, 26), 360f), 10f), 1.5f),
            B(AXE_UP, 8),
            Final(AXE_DOWN, 18, 2f, 3.5f),
            B(AXE_DOWN, 18), B(GUARD, 16) } },

        // 4 PURPLE — Phantom Strike: göz açýp kapayana kadar içinden geçer, arkasý dönük bekler, rakip yýðýlýr
        new Finisher { name = "PHANTOM STRIKE", beats = new[] {
            B(DASH, 14),
            WithThrough(WithDash(Hit(DASH, 9, 0.1f), 15f)),
            B(SLASH, 32),
            Final(COOL, 26, 1.5f, 4f),
            B(COOL, 24) } },

        // 5 ORANGE — Meteor Hammer: diz, dirsek, sonra iki elle yukarýdan çekiç
        new Finisher { name = "METEOR HAMMER", beats = new[] {
            WithDash(Hit(KNEE, 11), 3f),
            Hit(ELBOW, 11),
            WithHop(B(HAMMER_UP, 18), 6f),
            Final(HAMMER_DN, 16, 2f, 3f),
            B(HAMMER_DN, 22), B(GUARD, 14) } },

        // 6 CYAN — Cyclone Kicks: üç yüksek tekme, sonra ters takla tekmesi
        new Finisher { name = "CYCLONE KICKS", beats = new[] {
            WithDash(Hit(HIGH_L, 10), 2f),
            Hit(HIGH_R, 10), Hit(HIGH_L, 10),
            B(COIL, 9),
            WithHop(WithFlip(Final(HIGH_R, 26, 3f, 8f), -360f), 8f),
            B(TUCK, 18), B(GUARD, 14) } },

        // 7 PINK — Star Kick: piruet, ters yumruk, sonra uçan tekme
        new Finisher { name = "STAR KICK", beats = new[] {
            WithSpin(B(SPIN_WIND, 14), 360f),
            Hit(BACKFIST, 9),
            B(COIL, 10),
            WithDash(WithHop(Final(FLYKICK, 24, 5f, 7f), 6f), 6f),
            B(TUCK, 16), B(GUARD, 14) } },
    };

    static Beat WithDash(Beat b, float v) { b.dash = v; return b; }
    static Beat WithHop(Beat b, float v) { b.hop = v; return b; }
    static Beat WithSpin(Beat b, float deg) { b.spin = deg; return b; }
    static Beat WithFlip(Beat b, float deg) { b.flip = deg; return b; }
    static Beat WithThrough(Beat b) { b.through = true; return b; }

    public static Finisher Get(int style) => Finishers[Mathf.Abs(style) % Finishers.Length];

    #endregion

    #region Zafer sevinçleri

    public static readonly string[] CelebrationNames =
    {
        "FIST PUMP", "ARMS CROSSED", "BOW", "JUMP CHEER", "VICTORY SPIN", "FLEX", "SHADOW BOX", "WAVE & TWIRL",
    };

    /// <summary>
    /// Zafer animasyonu: t saniyede pozu yazar, kök için yukarý ofset ve dönüþ verir.
    /// </summary>
    public static void Celebrate(int style, float t, float[] p, out float rootY, out float rootSpin)
    {
        rootY = 0f;
        rootSpin = 0f;
        System.Array.Copy(COOL, p, p.Length);
        style = Mathf.Abs(style) % Finishers.Length;
        float s;
        switch (style)
        {
            case 0: // RED: yumruk sallama
                s = Mathf.Sin(t * 9f);
                p[0] = 0.93f + Mathf.Max(0f, s) * 0.03f; p[1] = -8f; p[2] = -18f;
                p[5] = Mathf.Lerp(-120f, -172f, 0.5f + 0.5f * s); p[6] = Mathf.Lerp(-95f, -10f, 0.5f + 0.5f * s);
                p[3] = -40f; p[4] = -100f;
                break;
            case 1: // BLUE: kollar baðlý, havalý baþ sallama
                p[3] = -72f; p[4] = -125f; p[5] = -68f; p[6] = -128f;
                p[2] = -8f + Mathf.Sin(t * 3f) * 7f;
                p[1] = -6f; p[7] = -12f + Mathf.Sin(t * 1.2f) * 4f; p[9] = 8f;
                break;
            case 2: // GREEN: dövüþ selamý (eðilme) + yumruk avuç içinde
                float c = Mathf.Repeat(t, 3f);
                float bow = c < 1.2f ? Mathf.Sin(Mathf.PI * c / 1.2f) : 0f;
                p[1] = bow * 50f; p[2] = bow * 20f;
                p[3] = -80f; p[4] = -95f; p[5] = -80f; p[6] = -95f;
                break;
            case 3: // YELLOW: zýplayarak iki kolla tezahürat
                float hop = Mathf.Abs(Mathf.Sin(t * Mathf.PI / 0.55f));
                rootY = hop * 0.32f;
                p[3] = -165f + Mathf.Sin(t * 14f) * 8f; p[4] = -10f; p[5] = -165f - Mathf.Sin(t * 14f) * 8f; p[6] = -10f;
                p[7] = -hop * 40f; p[8] = hop * 70f; p[9] = -hop * 40f; p[10] = hop * 70f;
                p[2] = -20f;
                break;
            case 4: // PURPLE: kollar yukarý çapraz, yavaþ dönüþ
                rootSpin = t * 140f;
                p[3] = -140f; p[4] = -20f; p[5] = -140f; p[6] = -20f; p[2] = -22f; p[1] = -10f;
                break;
            case 5: // ORANGE: pazý gösterme, sýrayla kas pompalama
                s = Mathf.Sin(t * 6f);
                p[3] = -120f; p[4] = -120f + Mathf.Max(0f, s) * 20f;
                p[5] = -120f; p[6] = -120f + Mathf.Max(0f, -s) * 20f;
                p[1] = -12f; p[2] = -10f; p[0] = 0.9f;
                p[7] = -20f; p[8] = 25f; p[9] = 15f; p[10] = 25f;
                break;
            case 6: // CYAN: yerinde gölge boksu
                float ph = Mathf.Repeat(t * 4f, 2f);
                bool left = ph < 1f;
                float k = Mathf.Sin(Mathf.PI * Mathf.Repeat(ph, 1f));
                System.Array.Copy(GUARD, p, p.Length);
                if (left) { p[3] = Mathf.Lerp(-55f, -92f, k); p[4] = Mathf.Lerp(-95f, -5f, k); }
                else { p[5] = Mathf.Lerp(-30f, -90f, k); p[6] = Mathf.Lerp(-115f, -5f, k); }
                rootY = Mathf.Abs(Mathf.Sin(t * 9f)) * 0.04f;
                break;
            default: // PINK: el sallama + arada piruet
                float cyc = Mathf.Repeat(t, 2.6f);
                p[5] = -165f; p[6] = -15f - (0.5f + 0.5f * Mathf.Sin(t * 12f)) * 45f;
                p[3] = -30f; p[4] = -60f; p[2] = -15f;
                if (cyc > 2.0f)
                {
                    float u = (cyc - 2.0f) / 0.6f;
                    rootSpin = 360f * u * u * (3f - 2f * u);
                    rootY = Mathf.Sin(Mathf.PI * u) * 0.12f;
                    p[7] = -30f; p[8] = 60f;
                }
                break;
        }
    }

    #endregion
}