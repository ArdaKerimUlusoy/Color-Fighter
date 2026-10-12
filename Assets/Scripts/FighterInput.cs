using UnityEngine;
using UnityEngine.InputSystem;

public class FighterInput : MonoBehaviour
{
    #region Ayarlar

    [Header("Klavye (P1: WASD + F/G/H, P2: Oklar + K/L/Ş)")]
    public Key left = Key.A;
    public Key right = Key.D;
    public Key up = Key.W;
    public Key down = Key.S;
    public Key punch = Key.F;
    public Key kick = Key.G;
    public Key pause = Key.Escape;
    [Tooltip("Kombo saldırısı tuşu. None ise punch tuşuna göre otomatik seçilir (F -> H, K -> Ş/;). Punch+Kick birlikte de çalışır.")]
    public Key combo = Key.None;

    [Header("Oyuncu (0 = 1P, 1 = 2P). Kollar CONTROLLERS ekranında ◄/► ile oyunculara atanır; kolu olan oyuncu klavyeyle oynamaz.")]
    [Tooltip("Hangi oyuncunun kolunu okuyacağı. -1 = gamepad kapalı.")]
    public int gamepadIndex = -1;

    [Header("Bilgisayar (1P VS CPU)")]
    [Tooltip("Açıkken klavye/gamepad okunmaz; tuşlara CpuBrain basar.")]
    public bool cpuControlled;
    [Tooltip("Menülerde kol PlayStation düzeninde çalışır: X onay, O geri. MatchManager ayarlar.")]
    [System.NonSerialized] public bool menuMode;
    [HideInInspector] public bool cpuLeft, cpuRight, cpuUp, cpuDown, cpuPunch, cpuKick, cpuCombo;
    /// <summary>CPU için: özel hareket isteği (↓ → + yumruk yerine doğrudan).</summary>
    [HideInInspector] public bool cpuSpecial;

    [Header("Input buffer (saniye)")]
    public float bufferTime = 0.15f;

    #endregion

    #region Durum

    public float Horizontal { get; private set; }
    public bool Up { get; private set; }
    public bool Down { get; private set; }
    public bool PunchHeld { get; private set; }
    public bool KickHeld { get; private set; }
    public bool ComboHeld { get; private set; }

    float punchAt = -99f, kickAt = -99f, upAt = -99f, downAt = -99f, pauseAt = -99f;
    float leftAt = -99f, rightAt = -99f, comboAt = -99f;
    bool prevUp, prevDown, prevPunch, prevKick, prevPause, prevLeft, prevRight, prevCombo;
    float specialAt = -99f;
    bool prevSpecial;

    // Yön geçmişi: özel hareket (↓ → + yumruk) algılamak için. Her yön değişimi zamanıyla saklanır.
    struct DirSample { public float t; public int x; public bool d; }
    const int HistSize = 16;
    readonly DirSample[] hist = new DirSample[HistSize];
    int histHead, histCount, lastDirX = 99;
    bool lastDirD;

    void Awake()
    {
        // Eski (önceden kurulmuş) sahnelerde combo tuşu kayıtlı değildir; oyuncuya göre tamamla.
        if (combo == Key.None)
        {
            if (punch == Key.F) combo = Key.H;
            else if (punch == Key.K) combo = Key.Semicolon;
        }
    }

    #endregion

    #region Okuma

    void Update()
    {
        float h = 0f;
        bool u = false, d = false, p = false, k = false, ps = false, c = false;

        var kb = Keyboard.current;
        // Kolu olan oyuncu klavyeyle oynamaz (ESC her zaman çalışır)
        var gp = cpuControlled ? null : GamepadAssign.PadFor(gamepadIndex);
        if (cpuControlled)
        {
            if (cpuLeft) h -= 1f;
            if (cpuRight) h += 1f;
            u = cpuUp; d = cpuDown; p = cpuPunch; k = cpuKick; c = cpuCombo;
        }
        else if (kb != null && gp == null)
        {
            if (kb[left].isPressed) h -= 1f;
            if (kb[right].isPressed) h += 1f;
            u |= kb[up].isPressed;
            d |= kb[down].isPressed;
            p |= kb[punch].isPressed;
            k |= kb[kick].isPressed;
            ps |= kb[pause].isPressed;
            if (combo != Key.None) c |= kb[combo].isPressed;
        }

        if (kb != null && !cpuControlled) ps |= kb[pause].isPressed;
        if (gp != null)
        {
            Vector2 s = gp.leftStick.ReadValue() + gp.dpad.ReadValue();
            if (s.x < -0.5f) h -= 1f;
            if (s.x > 0.5f) h += 1f;
            u |= s.y > 0.5f;
            d |= s.y < -0.5f;
            if (menuMode)
            {
                // Menü: X (South) / Options = onay (punch), O (East) = geri (kick), Create = ESC
                p |= gp.buttonSouth.isPressed || gp.startButton.isPressed;
                k |= gp.buttonEast.isPressed;
                ps |= gp.selectButton.isPressed;   // Create = ESC (geri)
            }
            else
            {
                // Dövüş: Kare = yumruk, X ve O = tekme, Üçgen = kombo, Options = pause
                p |= gp.buttonWest.isPressed;
                k |= gp.buttonSouth.isPressed || gp.buttonEast.isPressed;
                c |= gp.buttonNorth.isPressed;
                ps |= gp.startButton.isPressed || gp.selectButton.isPressed;   // Options / Create = ESC
            }
        }

        Horizontal = Mathf.Clamp(h, -1f, 1f);
        Up = u;
        Down = d;
        PunchHeld = p;
        KickHeld = k;
        ComboHeld = c;
        bool l = Horizontal < -0.1f, r = Horizontal > 0.1f;

        float now = Time.unscaledTime;
        if (u && !prevUp) upAt = now;
        if (d && !prevDown) downAt = now;
        if (p && !prevPunch) punchAt = now;
        if (k && !prevKick) kickAt = now;
        if (ps && !prevPause) pauseAt = now;
        if (l && !prevLeft) leftAt = now;
        if (r && !prevRight) rightAt = now;
        if (c && !prevCombo) comboAt = now;
        prevUp = u; prevDown = d; prevPunch = p; prevKick = k; prevPause = ps;
        prevLeft = l; prevRight = r; prevCombo = c;

        bool sp = cpuControlled && cpuSpecial;
        if (sp && !prevSpecial) specialAt = now;
        prevSpecial = sp;

        int dx = l ? -1 : r ? 1 : 0;
        if (dx != lastDirX || d != lastDirD)
        {
            hist[histHead] = new DirSample { t = now, x = dx, d = d };
            histHead = (histHead + 1) % HistSize;
            histCount = Mathf.Min(histCount + 1, HistSize);
            lastDirX = dx;
            lastDirD = d;
        }
    }

    #endregion

    #region Tüketme

    bool Consume(ref float pressedAt)
    {
        if (Buffered(pressedAt))
        {
            pressedAt = -99f;
            return true;
        }
        return false;
    }

    bool Buffered(float pressedAt) => Time.unscaledTime - pressedAt <= bufferTime;

    public bool ConsumePunch() => Consume(ref punchAt);
    public bool ConsumeKick() => Consume(ref kickAt);
    public bool ConsumeUp() => Consume(ref upAt);
    public bool ConsumeDown() => Consume(ref downAt);
    public bool ConsumePause() => Consume(ref pauseAt);
    public bool ConsumeLeft() => Consume(ref leftAt);
    public bool ConsumeRight() => Consume(ref rightAt);

    #region Özel hareket ve fırlatma girişi

    /// <summary>Yumruk tuşu tampon süresi içinde basıldı mı (tüketmeden).</summary>
    public bool PunchBuffered => Buffered(punchAt);

    /// <summary>
    /// ↓ sonra → (yüzünün döndüğü yön) girildi mi? Çapraz (↘) şart değil.
    /// İleri yön son 0.35 sn içinde verilmiş (ya da hâlâ basılı) olmalı, aşağı ondan en fazla 0.45 sn önce.
    /// </summary>
    public bool MotionDownForward(int facing)
    {
        float now = Time.unscaledTime;
        for (int k = 0; k < histCount; k++)
        {
            var s = Hist(k);
            float endT = k == 0 ? now : Hist(k - 1).t;   // bu yönün bırakıldığı an
            if (now - endT > 0.3f) return false;          // ileri çok önce bırakılmış
            if (s.x != facing || s.d) continue;           // ileri (aşağısız) değil
            // Bu ileri basışından hemen önce aşağı var mı?
            for (int j = k + 1; j < histCount; j++)
            {
                var e = Hist(j);
                float eEnd = Hist(j - 1).t;                // aşağının bırakıldığı an
                if (s.t - eEnd > 0.45f) break;
                if (e.d) return true;
            }
            return false;
        }
        return false;
    }

    /// <summary>k = 0 en yeni yön kaydı.</summary>
    DirSample Hist(int k) => hist[(histHead - 1 - k + 2 * HistSize) % HistSize];

    /// <summary>Özel hareket: ↓ → + yumruk (ya da CPU isteği). Doğruysa yumruğu tüketir.</summary>
    public bool ConsumeSpecial(int facing)
    {
        if (Consume(ref specialAt)) { punchAt = -99f; return true; }
        if (Buffered(punchAt) && MotionDownForward(facing))
        {
            punchAt = -99f;
            histCount = 0;   // aynı hareket iki kez tetiklenmesin
            return true;
        }
        return false;
    }

    /// <summary>Fırlatma: yumruk + tekme birlikte (aralarında en fazla ~4 frame). Doğruysa ikisini de tüketir.</summary>
    public bool ConsumeThrow()
    {
        if (Buffered(punchAt) && Buffered(kickAt) && Mathf.Abs(punchAt - kickAt) <= 0.07f)
        {
            punchAt = kickAt = -99f;
            return true;
        }
        return false;
    }

    /// <summary>Yumruk ya da tekmeden yalnız biri çok yeni basıldı: ikincisi gelirse fırlatma olabilir, kısa süre bekle.</summary>
    public bool WaitingForThrowPair(float wait = 0.05f)
    {
        bool p = Buffered(punchAt), k = Buffered(kickAt);
        if (p == k) return false;
        float t = p ? punchAt : kickAt;
        return Time.unscaledTime - t < wait;
    }

    #endregion

    /// <summary>Sadece kombo tuşuna basıldıysa true döner ve tüketir.</summary>
    public bool ConsumeCombo()
    {
        return Consume(ref comboAt);
    }

    /// <summary>Bu oyuncunun kolu varsa kısa titreşim.</summary>
    public void Rumble(float low, float high, float seconds)
    {
        if (cpuControlled) return;
        GamepadAssign.Rumble(gamepadIndex, low, high, seconds);
    }

    /// <summary>Bu oyuncuya atanmış kol (yoksa null).</summary>
    public Gamepad Pad => cpuControlled ? null : GamepadAssign.PadFor(gamepadIndex);

    public void ClearBuffers()
    {
        punchAt = kickAt = upAt = downAt = pauseAt = -99f;
        leftAt = rightAt = comboAt = specialAt = -99f;
    }

    /// <summary>Kombo tuşunun ekranda gösterilecek adı (klavye düzenine göre, ör. TR'de "Ş").</summary>
    public string ComboKeyLabel
    {
        get
        {
            if (combo == Key.None) return "P+K";
            var kb = Keyboard.current;
            string n = kb != null ? kb[combo].displayName : null;
            if (string.IsNullOrEmpty(n)) n = combo == Key.Semicolon ? ";" : combo.ToString();
            return n.ToUpperInvariant();
        }
    }

    #endregion
}