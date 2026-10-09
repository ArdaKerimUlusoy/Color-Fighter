using System.Collections.Generic;
using UnityEngine;

public class MusicPlayer : MonoBehaviour
{
    public enum Mood { None, Menu, Select, Fight, Victory }

    #region Ayarlar

    [Range(0f, 1f)] public float volume = 0.3f;
    [Tooltip("Oyun duraklatılınca müziğin kısılma oranı.")]
    [Range(0f, 1f)] public float pausedFactor = 0.35f;
    public float fadeTime = 0.7f;
    [Tooltip("Spiker konuşurken müziğin kısılma oranı.")]
    [Range(0f, 1f)] public float duckFactor = 0.45f;

    #endregion

    #region Durum

    static MusicPlayer instance;
    readonly Dictionary<Mood, AudioClip> clips = new Dictionary<Mood, AudioClip>();
    AudioSource srcA, srcB, current;
    Mood mood = Mood.None;
    MatchManager match;

    const int Rate = 22050;

    #endregion

    #region Başlatma

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (instance != null) return;
        if (Object.FindAnyObjectByType<ArcadeBootstrap>() == null) return;
        var go = new GameObject("MusicPlayer");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<MusicPlayer>();
    }

    void Awake()
    {
        srcA = MakeSource();
        srcB = MakeSource();
        clips[Mood.Menu] = RenderMenu();
        clips[Mood.Select] = RenderSelect();
        clips[Mood.Fight] = RenderFight();
        clips[Mood.Victory] = RenderVictory();
    }

    AudioSource MakeSource()
    {
        var s = gameObject.AddComponent<AudioSource>();
        s.playOnAwake = false;
        s.spatialBlend = 0f;
        s.volume = 0f;
        s.ignoreListenerPause = true;
        return s;
    }

    #endregion

    #region Geçişler

    void Update()
    {
        var want = Wanted();
        if (want != mood) Switch(want);

        float target = volume * (MatchManager.Paused ? pausedFactor : 1f) * (Announcer.Speaking ? duckFactor : 1f);
        float step = Time.unscaledDeltaTime / Mathf.Max(0.05f, fadeTime);
        foreach (var s in new[] { srcA, srcB })
        {
            float t = s == current ? target : 0f;
            s.volume = Mathf.MoveTowards(s.volume, t, step * Mathf.Max(volume, 0.01f));
            if (s != current && s.volume <= 0f && s.isPlaying) s.Stop();
        }
    }

    Mood Wanted()
    {
        if (MainMenu.Active) return Mood.Menu;
        if (match == null) match = Object.FindAnyObjectByType<MatchManager>();
        return match != null ? match.MusicMood : Mood.Menu;
    }

    void Switch(Mood next)
    {
        mood = next;
        if (!clips.TryGetValue(next, out var clip) || clip == null) { current = null; return; }
        var free = current == srcA ? srcB : srcA;
        free.clip = clip;
        free.loop = next != Mood.Victory;
        free.volume = next == Mood.Victory ? volume : 0f;
        free.Play();
        current = free;
    }

    #endregion

    #region Parçalar

    AudioClip RenderMenu()
    {
        string mel =
            "A4 - - - C5 - E5 - | F5 - - - E5 - C5 - | E5 - - - G5 - - - | D5 - - - B4 - - - |" +
            "A4 - C5 - E5 - A5 - | G5 - F5 - E5 - C5 - | E5 - D5 - C5 - E5 - | D5 - - - - - - -";
        return Render("Music_Menu", 96, "Am F C G Am F C G", mel, 0, Bass.Half, Drums.Soft, 0.55f);
    }

    AudioClip RenderSelect()
    {
        string mel =
            "D5 - F5 A5 - F5 D5 - | D5 - F5 Bb5 - A5 F5 - | E5 - G5 C6 - G5 E5 - | C#5 - E5 A5 - G5 E5 - |" +
            "F5 - A5 D6 - A5 F5 - | F5 - Bb5 D6 - C6 Bb5 - | G5 - C6 E6 - D6 C6 - | C#6 - A5 - E5 - A5 -";
        return Render("Music_Select", 132, "Dm Bb C A Dm Bb C A", mel, -12, Bass.Bounce, Drums.Beat, 0.75f);
    }

    AudioClip RenderFight()
    {
        string mel =
            "E5 - G5 - B5 - A5 G5 | F#5 - G5 - E5 - - - | E5 - G5 - C6 - B5 A5 | B5 - A5 - F#5 - D5 - |" +
            "E5 - G5 - B5 - A5 G5 | F#5 G5 A5 B5 - - E6 - | D6 - C6 B5 C6 - G5 - | A5 - F#5 - D#5 - B4 - |" +
            "A5 - C6 - E6 - D6 C6 | B5 - A5 - E5 - - - | G5 - B5 - E6 - D6 B5 | D6 - B5 - G5 - - - |" +
            "C6 - B5 C6 E6 - C6 - | D6 - C6 D6 F#6 - D6 - | D#6 - B5 - F#5 - D#5 - | B5 - - - F#5 G5 A5 B5";
        return Render("Music_Fight", 150, "Em Em C D Em Em C B Am Am Em Em C D B B", mel, -12, Bass.Drive, Drums.Drive, 1f);
    }

    AudioClip RenderVictory()
    {
        string mel = "G4 C5 E5 G5 - E5 G5 C6 | - - - - - - - - | . . . . . . . .";
        return Render("Music_Victory", 140, "C C C", mel, 0, Bass.Half, Drums.None, 0f);
    }

    #endregion

    #region Sentez

    enum Bass { Half, Bounce, Drive }
    enum Drums { None, Soft, Beat, Drive }

    struct Note { public int midi, start, len; }

    static int ParseNote(string s)
    {
        string[] names = { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };
        int split = 0;
        while (split < s.Length && !char.IsDigit(s[split])) split++;
        string name = s.Substring(0, split).Replace("Bb", "A#").Replace("Eb", "D#").Replace("Ab", "G#").Replace("Db", "C#").Replace("Gb", "F#");
        int oct = int.Parse(s.Substring(split));
        int pc = System.Array.IndexOf(names, name);
        return 12 * (oct + 1) + Mathf.Max(0, pc);
    }

    static void ParseChord(string c, out int pc, out bool minor)
    {
        minor = c.EndsWith("m");
        string root = minor ? c.Substring(0, c.Length - 1) : c;
        root = root.Replace("Bb", "A#").Replace("Eb", "D#").Replace("Ab", "G#").Replace("Db", "C#").Replace("Gb", "F#");
        string[] names = { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };
        pc = Mathf.Max(0, System.Array.IndexOf(names, root));
    }

    static List<Note> ParseMelody(string mel, int bars, int transpose)
    {
        var notes = new List<Note>();
        var barTexts = mel.Split('|');
        int step = 0;
        Note? open = null;
        for (int b = 0; b < bars; b++)
        {
            var tokens = b < barTexts.Length ? barTexts[b].Split(new[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries) : new string[0];
            for (int i = 0; i < 8; i++, step++)
            {
                string tk = i < tokens.Length ? tokens[i] : ".";
                if (tk == "-")
                {
                    if (open.HasValue) { var n = open.Value; n.len++; open = n; }
                    continue;
                }
                if (open.HasValue) notes.Add(open.Value);
                open = null;
                if (tk == ".") continue;
                open = new Note { midi = ParseNote(tk) + transpose, start = step, len = 1 };
            }
        }
        if (open.HasValue) notes.Add(open.Value);
        return notes;
    }

    static float Freq(float midi) { return 440f * Mathf.Pow(2f, (midi - 69f) / 12f); }

    AudioClip Render(string name, float bpm, string chordText, string mel, int transpose, Bass bass, Drums drums, float arpLevel)
    {
        var chords = chordText.Split(new[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries);
        int bars = chords.Length;
        float stepSec = 60f / bpm / 2f;
        int stepLen = Mathf.RoundToInt(stepSec * Rate);
        int total = stepLen * 8 * bars;
        var buf = new float[total];
        var rng = new System.Random(name.GetHashCode());

        foreach (var n in ParseMelody(mel, bars, transpose))
            Voice(buf, n.start * stepLen, n.len * stepLen, Freq(n.midi), 0.25f, 0.16f, true);

        for (int b = 0; b < bars; b++)
        {
            ParseChord(chords[b], out int pc, out bool minor);
            int root = 36 + pc;
            if (root > 43) root -= 12;
            int third = minor ? 3 : 4;
            int barStart = b * 8 * stepLen;

            switch (bass)
            {
                case Bass.Half:
                    Triangle(buf, barStart, stepLen * 4, Freq(root), 0.3f);
                    Triangle(buf, barStart + stepLen * 4, stepLen * 4, Freq(root + 7), 0.26f);
                    break;
                case Bass.Bounce:
                    for (int i = 0; i < 8; i++) Triangle(buf, barStart + i * stepLen, stepLen, Freq(root + (i % 2 == 0 ? 0 : 12)), 0.3f);
                    break;
                case Bass.Drive:
                    for (int i = 0; i < 8; i++) Triangle(buf, barStart + i * stepLen, stepLen, Freq(root + (i % 4 == 3 ? 7 : i % 2 == 1 ? 12 : 0)), 0.32f);
                    break;
            }

            if (arpLevel > 0f)
            {
                int[] arp = { 0, third, 7, 12 };
                int sub = stepLen / 2;
                for (int i = 0; i < 16; i++)
                    Voice(buf, barStart + i * sub, sub, Freq(root + 24 + arp[i % 4]), 0.125f, 0.05f * arpLevel, false);
            }

            for (int beat = 0; beat < 8; beat++)
            {
                int at = barStart + beat * stepLen;
                switch (drums)
                {
                    case Drums.Soft:
                        if (beat == 0) Kick(buf, at, 0.35f);
                        if (beat % 2 == 1) Hat(buf, at, 0.04f, rng);
                        break;
                    case Drums.Beat:
                        if (beat == 0 || beat == 4) Kick(buf, at, 0.5f);
                        if (beat == 2 || beat == 6) Snare(buf, at, 0.28f, rng);
                        Hat(buf, at, beat % 2 == 0 ? 0.05f : 0.035f, rng);
                        break;
                    case Drums.Drive:
                        if (beat == 0 || beat == 3 || beat == 4) Kick(buf, at, 0.55f);
                        if (beat == 2 || beat == 6) Snare(buf, at, 0.32f, rng);
                        if (b % 4 == 3 && beat == 7) Snare(buf, at + stepLen / 2, 0.25f, rng);
                        Hat(buf, at, 0.045f, rng);
                        Hat(buf, at + stepLen / 2, 0.025f, rng);
                        break;
                }
            }
        }

        if (drums == Drums.None) Crash(buf, 0, 0.18f, rng);

        for (int i = 0; i < total; i++)
        {
            float s = (float)System.Math.Tanh(buf[i] * 1.3f) * 0.8f;
            buf[i] = Mathf.Round(s * 96f) / 96f;
        }

        var clip = AudioClip.Create(name, total, 1, Rate, false);
        clip.SetData(buf, 0);
        return clip;
    }

    static void Voice(float[] buf, int start, int len, float f, float duty, float vol, bool vibrato)
    {
        float ph = 0f;
        int rel = Mathf.Min(len / 3, Rate / 50);
        for (int i = 0; i < len && start + i < buf.Length; i++)
        {
            float t = i / (float)Rate;
            float ff = vibrato && t > 0.15f ? f * (1f + 0.006f * Mathf.Sin(t * 6f * Mathf.PI * 2f)) : f;
            ph += ff / Rate;
            ph -= Mathf.Floor(ph);
            float env = Mathf.Min(1f, i / (Rate * 0.004f));
            env *= Mathf.Lerp(1f, 0.7f, Mathf.Clamp01(t / 0.12f));
            if (i > len - rel) env *= (len - i) / (float)rel;
            buf[start + i] += (ph < duty ? 1f : -1f) * vol * env;
        }
    }

    static void Triangle(float[] buf, int start, int len, float f, float vol)
    {
        float ph = 0f;
        int rel = Mathf.Min(len / 4, Rate / 40);
        for (int i = 0; i < len && start + i < buf.Length; i++)
        {
            ph += f / Rate;
            ph -= Mathf.Floor(ph);
            float tri = 4f * Mathf.Abs(ph - 0.5f) - 1f;
            tri = Mathf.Round(tri * 8f) / 8f;
            float env = Mathf.Min(1f, i / (Rate * 0.003f));
            if (i > len - rel) env *= (len - i) / (float)rel;
            buf[start + i] += tri * vol * env;
        }
    }

    static void Kick(float[] buf, int start, float vol)
    {
        int len = Rate * 12 / 100;
        float ph = 0f;
        for (int i = 0; i < len && start + i < buf.Length; i++)
        {
            float p = i / (float)len;
            ph += Mathf.Lerp(150f, 42f, Mathf.Sqrt(p)) / Rate;
            buf[start + i] += Mathf.Sin(ph * Mathf.PI * 2f) * vol * (1f - p) * (1f - p);
        }
    }

    static void Snare(float[] buf, int start, float vol, System.Random rng)
    {
        int len = Rate / 9;
        float ph = 0f;
        for (int i = 0; i < len && start + i < buf.Length; i++)
        {
            float p = i / (float)len;
            ph += 185f / Rate;
            float n = (float)rng.NextDouble() * 2f - 1f;
            buf[start + i] += (n * 0.8f + Mathf.Sin(ph * Mathf.PI * 2f) * 0.4f) * vol * (1f - p) * (1f - p);
        }
    }

    static void Hat(float[] buf, int start, float vol, System.Random rng)
    {
        int len = Rate / 35;
        float last = 0f;
        for (int i = 0; i < len && start + i < buf.Length; i++)
        {
            float p = i / (float)len;
            float n = (float)rng.NextDouble() * 2f - 1f;
            buf[start + i] += (n - last) * 0.5f * vol * (1f - p);
            last = n;
        }
    }

    static void Crash(float[] buf, int start, float vol, System.Random rng)
    {
        int len = Mathf.Min(buf.Length - start, Rate * 2);
        float last = 0f;
        for (int i = 0; i < len; i++)
        {
            float p = i / (float)len;
            float n = (float)rng.NextDouble() * 2f - 1f;
            buf[start + i] += (n - last * 0.6f) * vol * Mathf.Pow(1f - p, 2.5f);
            last = n;
        }
    }

    #endregion
}
