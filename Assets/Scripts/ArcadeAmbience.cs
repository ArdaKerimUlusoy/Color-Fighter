using UnityEngine;
using UnityEngine.UI;

/// <summary>Dekoru canlandırır: kabin ekranları, neon tabelalar, seyirciler, foto flaşları, ışıklar.
/// Hem kabin odasında hem dövüş arenasında birer tane bulunur.</summary>
public class ArcadeAmbience : MonoBehaviour
{
    static readonly System.Collections.Generic.List<ArcadeAmbience> all = new System.Collections.Generic.List<ArcadeAmbience>();

    /// <summary>Tüm sahnelerdeki seyirciler tezahürat yapar (KO, kombo saldırısı).</summary>
    public static void CheerAll(float seconds)
    {
        foreach (var a in all) if (a != null) a.Cheer(seconds);
    }

    #region Referanslar

    public Renderer[] screens;
    public Transform[] screenSprites;
    public Transform[] crowd;
    public Transform[] crowdArmsL, crowdArmsR;
    public Text[] blinkTexts;
    public Renderer[] blinkTubes;
    public Text[] flickerTexts;
    public Renderer[] flickerTubes;
    public Light[] lights;
    public Renderer[] cameraFlashes;

    #endregion

    #region Durum

    // Boş dizilerle başlar: Start bir sebeple yarıda kalsa bile Update çökmez.
    Material[] screenMats = new Material[0];
    float[] screenHue = new float[0], crowdPhase = new float[0], lightBase = new float[0];
    Vector3[] crowdBase = new Vector3[0];
    Vector2[] spritePos = new Vector2[0], spriteVel = new Vector2[0];
    Color[] flickerTextBase = new Color[0];
    float cheerUntil, flickerUntil, nextFlicker;
    Renderer activeFlash;
    float flashOffAt;

    void Awake() { all.Add(this); }
    void OnDestroy() { all.Remove(this); }

    void Start()
    {
        var rng = new System.Random(42);
        if (screenSprites == null) screenSprites = new Transform[0];
        if (crowd == null) crowd = new Transform[0];
        if (lights == null) lights = new Light[0];
        if (flickerTexts == null) flickerTexts = new Text[0];
        int n = screens != null ? screens.Length : 0;
        screenMats = new Material[n];
        screenHue = new float[n];
        for (int i = 0; i < n; i++)
        {
            if (screens[i] == null) continue;
            screenMats[i] = screens[i].material; // kopya: asset değişmez
            screenHue[i] = (float)rng.NextDouble();
        }

        int s = screenSprites != null ? screenSprites.Length : 0;
        spritePos = new Vector2[s];
        spriteVel = new Vector2[s];
        for (int i = 0; i < s; i++)
        {
            spritePos[i] = new Vector2((float)rng.NextDouble() * 0.5f - 0.25f, (float)rng.NextDouble() * 0.36f - 0.18f);
            spriteVel[i] = new Vector2(rng.NextDouble() > 0.5 ? 0.35f : -0.35f, rng.NextDouble() > 0.5 ? 0.27f : -0.27f);
        }

        int c = crowd != null ? crowd.Length : 0;
        crowdBase = new Vector3[c];
        crowdPhase = new float[c];
        for (int i = 0; i < c; i++)
        {
            if (crowd[i] == null) continue;   // silinmiş/boş referansı atla
            crowdBase[i] = crowd[i].localPosition;
            crowdPhase[i] = (float)rng.NextDouble() * 6.28f;
        }

        int l = lights != null ? lights.Length : 0;
        lightBase = new float[l];
        for (int i = 0; i < l; i++) lightBase[i] = lights[i] != null ? lights[i].intensity : 0f;

        int f = flickerTexts != null ? flickerTexts.Length : 0;
        flickerTextBase = new Color[f];
        for (int i = 0; i < f; i++) flickerTextBase[i] = flickerTexts[i] != null ? flickerTexts[i].color : Color.white;

        nextFlicker = Time.time + 3f;
    }

    #endregion

    #region Olaylar

    /// <summary>Seyirciler kollarını kaldırıp zıplar (KO, kombo saldırısı).</summary>
    public void Cheer(float seconds)
    {
        cheerUntil = Mathf.Max(cheerUntil, Time.time + seconds);
    }

    #endregion

    #region Animasyon

    void Update()
    {
        float t = Time.time;
        float dt = Time.deltaTime;

        // Kabin ekranları: "attract mode" renk döngüsü
        for (int i = 0; i < screenMats.Length; i++)
        {
            if (screenMats[i] == null) continue;
            float hue = Mathf.Repeat(screenHue[i] + t * 0.04f, 1f);
            float v = 0.3f + 0.08f * Mathf.Sin(t * 3f + i);
            screenMats[i].color = Color.HSVToRGB(hue, 0.65f, v);
        }

        // Ekranda seken küçük "sprite"
        for (int i = 0; i < spritePos.Length; i++)
        {
            if (screenSprites[i] == null) continue;
            var p = spritePos[i] + spriteVel[i] * dt;
            if (Mathf.Abs(p.x) > 0.3f) { spriteVel[i].x = -spriteVel[i].x; p.x = Mathf.Clamp(p.x, -0.3f, 0.3f); }
            if (Mathf.Abs(p.y) > 0.21f) { spriteVel[i].y = -spriteVel[i].y; p.y = Mathf.Clamp(p.y, -0.21f, 0.21f); }
            spritePos[i] = p;
            var lp = screenSprites[i].localPosition;
            screenSprites[i].localPosition = new Vector3(p.x, 1.58f + p.y, lp.z);
        }

        // Seyirciler
        bool cheering = t < cheerUntil;
        for (int i = 0; i < crowdBase.Length; i++)
        {
            if (crowd[i] == null) continue;
            float ph = crowdPhase[i];
            float bob = cheering ? Mathf.Abs(Mathf.Sin(t * 9f + ph)) * 0.22f : Mathf.Abs(Mathf.Sin(t * 2.2f + ph)) * 0.03f;
            crowd[i].localPosition = crowdBase[i] + Vector3.up * bob;
            float arm = cheering ? 150f + Mathf.Sin(t * 12f + ph) * 15f : 8f + Mathf.Sin(t * 1.5f + ph) * 4f;
            if (crowdArmsL != null && i < crowdArmsL.Length && crowdArmsL[i] != null) crowdArmsL[i].localRotation = Quaternion.Euler(0f, 0f, -arm);
            if (crowdArmsR != null && i < crowdArmsR.Length && crowdArmsR[i] != null) crowdArmsR[i].localRotation = Quaternion.Euler(0f, 0f, arm);
        }

        // "INSERT COIN" yanıp söner
        bool on = Mathf.Repeat(t, 1.2f) < 0.8f;
        if (blinkTexts != null) foreach (var b in blinkTexts) if (b != null) b.enabled = on;
        if (blinkTubes != null) foreach (var b in blinkTubes) if (b != null) b.enabled = on;

        // Ana neon tabela arada bir titrer
        if (t > nextFlicker)
        {
            flickerUntil = t + Random.Range(0.06f, 0.18f);
            nextFlicker = t + Random.Range(2.5f, 6f);
        }
        bool dim = t < flickerUntil && Mathf.Repeat(t * 30f, 1f) < 0.5f;
        for (int i = 0; i < flickerTextBase.Length; i++)
            if (flickerTexts[i] != null) flickerTexts[i].color = dim ? flickerTextBase[i] * 0.35f : flickerTextBase[i];
        if (flickerTubes != null) foreach (var r in flickerTubes) if (r != null) r.enabled = !dim;

        // Tribünde foto flaşları (tezahüratta daha sık)
        if (cameraFlashes != null && cameraFlashes.Length > 0)
        {
            if (activeFlash != null && t > flashOffAt) { activeFlash.enabled = false; activeFlash = null; }
            if (activeFlash == null && Random.value < (cheering ? 0.35f : 0.04f))
            {
                activeFlash = cameraFlashes[Random.Range(0, cameraFlashes.Length)];
                if (activeFlash != null) { activeFlash.enabled = true; flashOffAt = t + 0.06f; }
            }
        }

        // Işıklar hafifçe nefes alır
        for (int i = 0; i < lightBase.Length; i++)
            if (lights[i] != null) lights[i].intensity = lightBase[i] * (0.85f + 0.15f * Mathf.Sin(t * 1.3f + i * 2f));
    }

    #endregion
}