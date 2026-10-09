using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

public class ArcadeVisuals : MonoBehaviour
{
    #region Ayarlar

    [Header("Post-processing (sadece salon kamerası)")]
    public bool postProcessing = true;
    [Tooltip("Neon tabelaların ve ekranın parlama gücü.")]
    [Range(0f, 3f)] public float bloomIntensity = 0.35f;
    [Tooltip("Bu parlaklığın üstündeki yerler parlar. Düşürürsen daha çok şey parlar.")]
    [Range(0f, 2f)] public float bloomThreshold = 1.1f;
    [Tooltip("Parlamanın ne kadar geniş yayılacağı.")]
    [Range(0f, 1f)] public float bloomScatter = 0.55f;
    [Range(0f, 1f)] public float vignette = 0.38f;
    [Range(-100f, 100f)] public float contrast = 14f;
    [Range(-100f, 100f)] public float saturation = 18f;
    [Tooltip("Kenarlarda hafif renk kayması (eski lens hissi).")]
    [Range(0f, 1f)] public float chromaticAberration = 0.12f;
    [Tooltip("Hafif film greni.")]
    [Range(0f, 1f)] public float filmGrain = 0.18f;

    [Header("Ekran ışığı")]
    [Tooltip("Kabin ekranının ışığı, ekranda ne varsa onun rengini ve parlaklığını alır.")]
    public bool reactiveScreenLight = true;
    public float lightBaseIntensity = 0.25f;
    [Tooltip("Ekran parladıkça ışığa eklenen güç (KO flaşında patlar).")]
    public float lightBrightnessBoost = 1.4f;
    public float lightRange = 2.4f;
    [Tooltip("Işığın ekrandaki değişime tepki hızı.")]
    public float lightResponse = 16f;
    [Tooltip("Ekran saniyede kaç kez örneklenir.")]
    public float samplesPerSecond = 20f;

    #endregion

    #region Otomatik ekleme

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Hook()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        Attach();
    }

    static void OnSceneLoaded(Scene s, LoadSceneMode mode) { Attach(); }

    static void Attach()
    {
        var boot = Object.FindAnyObjectByType<ArcadeBootstrap>();
        if (boot != null && boot.GetComponent<ArcadeVisuals>() == null)
            boot.gameObject.AddComponent<ArcadeVisuals>();
    }

    #endregion

    #region Durum

    Volume volume;
    VolumeProfile profile;
    Bloom bloom;
    Vignette vig;
    ColorAdjustments grading;
    ChromaticAberration chroma;
    FilmGrain grain;

    Light glow;
    RenderTexture source, mid, small;
    Color sampled = Color.black, current = Color.black;
    float sampleTimer;
    bool pending;

    #endregion

    #region Başlangıç

    void Start()
    {
        SetupPost();
        SetupScreenLight();
        SetupExtras();
    }

    void SetupExtras()
    {
        var mm = GetComponent<MatchManager>();
        if (mm == null || mm.p1 == null || mm.p2 == null) return;

        foreach (var f in new[] { mm.p1, mm.p2 })
            if (f.GetComponent<FighterTrails>() == null) f.gameObject.AddComponent<FighterTrails>();

        CabinetComboButton.AttachAll(transform, mm.p1, mm.p2);

        var arena = mm.p1.transform.parent;
        if (arena == null) return;
        for (int i = 0; i < StageSwitcher.Count; i++)
        {
            var g = arena.Find(StageSwitcher.Groups[i]);
            if (g == null || g.GetComponent<StageLife>() != null) continue;
            var life = g.gameObject.AddComponent<StageLife>();
            life.kind = (StageLife.Kind)i;
        }
    }

    void OnValidate()
    {
        ApplyPost();
    }

    void OnDestroy()
    {
        if (profile != null) Destroy(profile);
        if (mid != null) mid.Release();
        if (small != null) small.Release();
    }

    #endregion

    #region Post-processing

    void SetupPost()
    {
        var cam = Camera.main;
        if (cam != null) cam.GetUniversalAdditionalCameraData().renderPostProcessing = true;

        var go = new GameObject("ArcadeVisualsVolume");
        go.transform.SetParent(transform, false);
        volume = go.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.priority = 50f;

        profile = ScriptableObject.CreateInstance<VolumeProfile>();
        profile.name = "ArcadeVisualsProfile";
        volume.sharedProfile = profile;

        bloom = profile.Add<Bloom>(true);
        vig = profile.Add<Vignette>(true);
        grading = profile.Add<ColorAdjustments>(true);
        chroma = profile.Add<ChromaticAberration>(true);
        grain = profile.Add<FilmGrain>(true);

        ApplyPost();
    }

    void ApplyPost()
    {
        if (volume == null || bloom == null) return;
        volume.weight = postProcessing ? 1f : 0f;

        bloom.intensity.Override(bloomIntensity);
        bloom.threshold.Override(bloomThreshold);
        bloom.scatter.Override(bloomScatter);
        bloom.tint.Override(new Color(1f, 0.93f, 0.97f));
        bloom.highQualityFiltering.Override(true);

        vig.intensity.Override(vignette);
        vig.smoothness.Override(0.45f);
        vig.color.Override(new Color(0.02f, 0f, 0.04f));

        grading.contrast.Override(contrast);
        grading.saturation.Override(saturation);
        grading.postExposure.Override(-0.1f);
        grading.colorFilter.Override(new Color(1f, 0.97f, 1f));

        chroma.intensity.Override(chromaticAberration);

        grain.type.Override(FilmGrainLookup.Thin1);
        grain.intensity.Override(filmGrain);
        grain.response.Override(0.8f);
    }

    #endregion

    #region Ekran ışığı

    void SetupScreenLight()
    {
        foreach (var l in GetComponentsInChildren<Light>(true))
            if (l.name == "ScreenGlow") { glow = l; break; }

        var mm = GetComponent<MatchManager>();
        if (mm != null && mm.fightCam != null) source = mm.fightCam.targetTexture;
        if (glow == null || source == null) return;

        glow.range = Mathf.Max(glow.range, lightRange);
        mid = new RenderTexture(80, 60, 0, RenderTextureFormat.ARGB32) { filterMode = FilterMode.Bilinear, name = "ScreenGlowMid" };
        small = new RenderTexture(16, 12, 0, RenderTextureFormat.ARGB32) { filterMode = FilterMode.Bilinear, name = "ScreenGlowSmall" };
        mid.Create();
        small.Create();
    }

    void Update()
    {
        if (glow == null || small == null) return;

        if (!reactiveScreenLight)
        {
            glow.color = new Color(0.6f, 0.6f, 1f);
            glow.intensity = 0.8f;
            return;
        }

        float dt = Time.unscaledDeltaTime;
        sampleTimer -= dt;
        if (sampleTimer <= 0f && !pending)
        {
            sampleTimer = 1f / Mathf.Max(1f, samplesPerSecond);
            Sample();
        }

        current = Color.Lerp(current, sampled, 1f - Mathf.Exp(-lightResponse * dt));
        float level = current.maxColorComponent;
        Color hue = level > 0.02f ? current / level : new Color(0.6f, 0.6f, 1f);
        hue.a = 1f;
        glow.color = Color.Lerp(new Color(0.6f, 0.6f, 1f), hue, Mathf.Clamp01(level * 3f));
        glow.intensity = lightBaseIntensity + level * lightBrightnessBoost;
    }

    void Sample()
    {
        Graphics.Blit(source, mid);
        Graphics.Blit(mid, small);

        if (SystemInfo.supportsAsyncGPUReadback)
        {
            pending = true;
            AsyncGPUReadback.Request(small, 0, TextureFormat.RGBA32, OnReadback);
            return;
        }

        var prev = RenderTexture.active;
        RenderTexture.active = small;
        var tex = new Texture2D(small.width, small.height, TextureFormat.RGBA32, false);
        tex.ReadPixels(new Rect(0, 0, small.width, small.height), 0, 0);
        RenderTexture.active = prev;
        sampled = Average(tex.GetPixels32());
        Destroy(tex);
    }

    void OnReadback(AsyncGPUReadbackRequest req)
    {
        pending = false;
        if (this == null || req.hasError) return;
        sampled = Average(req.GetData<Color32>().ToArray());
    }

    static Color Average(Color32[] px)
    {
        if (px == null || px.Length == 0) return Color.black;
        float r = 0f, g = 0f, b = 0f;
        foreach (var p in px) { r += p.r; g += p.g; b += p.b; }
        float k = 1f / (px.Length * 255f);
        return new Color(r * k, g * k, b * k, 1f);
    }

    #endregion
}
