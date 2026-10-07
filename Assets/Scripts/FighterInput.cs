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

    [Header("Gamepad (-1 = kapalı, 0 = ilk gamepad, 1 = ikinci...)")]
    public int gamepadIndex = -1;

    [Header("Bilgisayar (1P VS CPU)")]
    [Tooltip("Açıkken klavye/gamepad okunmaz; tuşlara CpuBrain basar.")]
    public bool cpuControlled;
    [HideInInspector] public bool cpuLeft, cpuRight, cpuUp, cpuDown, cpuPunch, cpuKick, cpuCombo;

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
        if (cpuControlled)
        {
            if (cpuLeft) h -= 1f;
            if (cpuRight) h += 1f;
            u = cpuUp; d = cpuDown; p = cpuPunch; k = cpuKick; c = cpuCombo;
        }
        else if (kb != null)
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

        if (!cpuControlled && gamepadIndex >= 0 && gamepadIndex < Gamepad.all.Count)
        {
            var gp = Gamepad.all[gamepadIndex];
            Vector2 s = gp.leftStick.ReadValue() + gp.dpad.ReadValue();
            if (s.x < -0.5f) h -= 1f;
            if (s.x > 0.5f) h += 1f;
            u |= s.y > 0.5f;
            d |= s.y < -0.5f;
            p |= gp.buttonWest.isPressed;
            k |= gp.buttonSouth.isPressed;
            ps |= gp.startButton.isPressed;
            c |= gp.buttonNorth.isPressed;
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

    /// <summary>Kombo tuşu ya da Punch+Kick birlikte basıldıysa true döner ve tüketir.</summary>
    public bool ConsumeCombo()
    {
        if (Consume(ref comboAt)) return true;
        if (Buffered(punchAt) && Buffered(kickAt))
        {
            punchAt = kickAt = -99f;
            return true;
        }
        return false;
    }

    public void ClearBuffers()
    {
        punchAt = kickAt = upAt = downAt = pauseAt = -99f;
        leftAt = rightAt = comboAt = -99f;
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