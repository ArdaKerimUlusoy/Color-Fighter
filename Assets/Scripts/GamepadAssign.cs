using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Gamepad'leri oyunculara dağıtır: her kol 1P'ye, 2P'ye ya da ortada (boşta) olabilir.
/// Takılan kollar ortada başlar; CONTROLLERS ekranında ◄/► ile oyunculara taşınır.
/// Kolu olan oyuncu klavyeyle oynamaz (FighterInput), kolu olmayan oyuncu klavyeyle oynar.
/// </summary>
public static class GamepadAssign
{
    public const int None = -1;

    static readonly Dictionary<int, int> slots = new Dictionary<int, int>();   // deviceId -> 0 (1P), 1 (2P), -1 boşta

    /// <summary>Bu oyuncuya (0 = 1P, 1 = 2P) atanmış ilk bağlı kol, yoksa null.</summary>
    public static Gamepad PadFor(int player)
    {
        if (player < 0) return null;
        foreach (var gp in Gamepad.all)
            if (gp != null && SlotOf(gp) == player) return gp;
        return null;
    }

    public static int SlotOf(Gamepad gp)
    {
        if (gp == null) return None;
        return slots.TryGetValue(gp.deviceId, out int s) ? s : None;
    }

    /// <summary>Kolu sola (1P'ye doğru) ya da sağa (2P'ye doğru) taşı: 1P ◄ boşta ► 2P</summary>
    public static void Move(Gamepad gp, int dir)
    {
        if (gp == null || dir == 0) return;
        int s = SlotOf(gp);
        int pos = s == 0 ? 0 : s == 1 ? 2 : 1;   // 0 = 1P, 1 = boşta, 2 = 2P
        pos = Mathf.Clamp(pos + (dir < 0 ? -1 : 1), 0, 2);
        Set(gp, pos == 0 ? 0 : pos == 2 ? 1 : None);
    }

    /// <summary>Kolu doğrudan bir yuvaya koy (0 = 1P, 1 = 2P, -1 boşta). O yuvadaki diğer kol ortaya döner.</summary>
    public static void Set(Gamepad gp, int slot)
    {
        if (gp == null) return;
        if (slot != 0 && slot != 1) slot = None;
        if (slot != None)
            foreach (var other in Gamepad.all)
                if (other != null && other != gp && SlotOf(other) == slot) slots[other.deviceId] = None;
        slots[gp.deviceId] = slot;
    }

    /// <summary>Kısa ad: PS5 / PS4 / XBOX / PAD (ekranda gösterilmez, sadece bilgi için).</summary>
    public static string ShortName(Gamepad gp)
    {
        if (gp == null) return "-";
        string t = gp.GetType().Name + " " + gp.displayName;
        if (t.Contains("DualSense")) return "PS5";
        if (t.Contains("DualShock")) return "PS4";
        if (t.Contains("XInput") || t.Contains("Xbox")) return "XBOX";
        if (t.Contains("Switch")) return "SWITCH";
        return "PAD";
    }

    /// <summary>Kısa titreşim (destekleyen kollarda). Oyuncunun kolu yoksa bir şey yapmaz.</summary>
    public static void Rumble(int player, float low, float high, float seconds)
    {
        RumblePad(PadFor(player), low, high, seconds);
    }

    /// <summary>Belirli bir kolu kısa süre titret (atanmamış olsa bile).</summary>
    public static void RumblePad(Gamepad gp, float low, float high, float seconds)
    {
        if (gp == null) return;
        gp.SetMotorSpeeds(low, high);
        RumbleStopper.Schedule(gp, seconds);
    }

    /// <summary>Titreşimi süre dolunca kapatan küçük yardımcı.</summary>
    class RumbleStopper : MonoBehaviour
    {
        static RumbleStopper inst;
        readonly Dictionary<Gamepad, float> until = new Dictionary<Gamepad, float>();

        public static void Schedule(Gamepad gp, float seconds)
        {
            if (inst == null)
            {
                var go = new GameObject("GamepadRumble") { hideFlags = HideFlags.HideAndDontSave };
                Object.DontDestroyOnLoad(go);
                inst = go.AddComponent<RumbleStopper>();
            }
            inst.until[gp] = Time.unscaledTime + seconds;
        }

        void Update()
        {
            if (until.Count == 0) return;
            var done = new List<Gamepad>();
            foreach (var kv in until)
                if (kv.Key == null || Time.unscaledTime >= kv.Value) done.Add(kv.Key);
            foreach (var gp in done)
            {
                if (gp != null) gp.SetMotorSpeeds(0f, 0f);
                until.Remove(gp);
            }
        }

        void OnDisable()
        {
            foreach (var gp in Gamepad.all) gp?.ResetHaptics();
        }
    }
}