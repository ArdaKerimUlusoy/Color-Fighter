using UnityEngine;
using UnityEngine.InputSystem;

public class FighterInput : MonoBehaviour
{
    #region Ayarlar

    [Header("Klavye (P1: WASD + F/G, P2: Oklar + K/L)")]
    public Key left = Key.A;
    public Key right = Key.D;
    public Key up = Key.W;
    public Key down = Key.S;
    public Key punch = Key.F;
    public Key kick = Key.G;
    public Key pause = Key.Escape;

    [Header("Gamepad (-1 = kapalı, 0 = ilk gamepad, 1 = ikinci...)")]
    public int gamepadIndex = -1;

    [Header("Input buffer (saniye)")]
    public float bufferTime = 0.15f;

    #endregion

    #region Durum

    public float Horizontal { get; private set; }
    public bool Up { get; private set; }
    public bool Down { get; private set; }
    public bool PunchHeld { get; private set; }
    public bool KickHeld { get; private set; }

    float punchAt = -99f, kickAt = -99f, upAt = -99f, downAt = -99f, pauseAt = -99f;
    bool prevUp, prevDown, prevPunch, prevKick, prevPause;

    #endregion

    #region Okuma

    void Update()
    {
        float h = 0f;
        bool u = false, d = false, p = false, k = false, ps = false;

        var kb = Keyboard.current;
        if (kb != null)
        {
            if (kb[left].isPressed) h -= 1f;
            if (kb[right].isPressed) h += 1f;
            u |= kb[up].isPressed;
            d |= kb[down].isPressed;
            p |= kb[punch].isPressed;
            k |= kb[kick].isPressed;
            ps |= kb[pause].isPressed;
        }

        if (gamepadIndex >= 0 && gamepadIndex < Gamepad.all.Count)
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
        }

        Horizontal = Mathf.Clamp(h, -1f, 1f);
        Up = u;
        Down = d;
        PunchHeld = p;
        KickHeld = k;

        float now = Time.unscaledTime;
        if (u && !prevUp) upAt = now;
        if (d && !prevDown) downAt = now;
        if (p && !prevPunch) punchAt = now;
        if (k && !prevKick) kickAt = now;
        if (ps && !prevPause) pauseAt = now;
        prevUp = u; prevDown = d; prevPunch = p; prevKick = k; prevPause = ps;
    }

    #endregion

    #region Tüketme

    bool Consume(ref float pressedAt)
    {
        if (Time.unscaledTime - pressedAt <= bufferTime)
        {
            pressedAt = -99f;
            return true;
        }
        return false;
    }

    public bool ConsumePunch() => Consume(ref punchAt);
    public bool ConsumeKick() => Consume(ref kickAt);
    public bool ConsumeUp() => Consume(ref upAt);
    public bool ConsumeDown() => Consume(ref downAt);
    public bool ConsumePause() => Consume(ref pauseAt);

    public void ClearBuffers()
    {
        punchAt = kickAt = upAt = downAt = pauseAt = -99f;
    }

    #endregion
}
