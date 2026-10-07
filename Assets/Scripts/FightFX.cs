using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FightFX : MonoBehaviour
{
    public static FightFX I { get; private set; }

    #region Ayarlar

    public Shader unlitShader;

    [Header("Ekran sarsıntısı")]
    public float shakeDecay = 10f;
    public float shakeMultiplier = 1f;

    [Header("Hitstop")]
    [Tooltip("Tüm hitstop sürelerini çarpar. 0 = kapalı (farkı görmek için dene!).")]
    public float hitstopMultiplier = 1f;

    [Header("KO")]
    public float koSlowMoScale = 0.3f;
    public float koSlowMoSeconds = 1.4f;

    [Header("Ses")]
    [Range(0f, 1f)] public float volume = 0.7f;

    #endregion

    #region Durum

    public int Hitstop { get; private set; }
    public Vector3 ShakeOffset { get; private set; }
    public System.Action OnFlash;

    float shake;
    bool initialized;
    Transform sparkRoot;
    AudioSource[] sources;
    int nextSource;
    AudioClip sHitLight, sHitHeavy, sBlock, sWhiff, sKO, sLand, sBlip, sFight, sMenuMove, sMenuSelect;
    readonly List<Spark> sparks = new List<Spark>();
    readonly System.Random rng = new System.Random(1234);

    class Spark
    {
        public Transform flash;
        public Transform[] shards;
        public Vector3[] dirs;
        public Material mat;
        public Vector3 origin;
        public float t, life, size;
    }

    void Awake() { I = this; }

    void EnsureInit()
    {
        if (initialized) return;
        initialized = true;
        sparkRoot = new GameObject("Sparks").transform;
        sources = new AudioSource[6];
        for (int i = 0; i < sources.Length; i++)
        {
            sources[i] = gameObject.AddComponent<AudioSource>();
            sources[i].playOnAwake = false;
            sources[i].spatialBlend = 0f;
        }
        GenerateSounds();
    }

    void Start() { EnsureInit(); }

    #endregion

    #region Hitstop ve sarsıntı

    public bool ConsumeHitstopFrame()
    {
        if (Hitstop > 0) { Hitstop--; return true; }
        return false;
    }

    public void AddHitstop(int frames) { Hitstop = Mathf.Max(Hitstop, Mathf.RoundToInt(frames * hitstopMultiplier)); }
    public void Shake(float amount) { shake = Mathf.Max(shake, amount * shakeMultiplier); }

    public void ResetState()
    {
        StopAllCoroutines();
        Hitstop = 0;
        shake = 0f;
        Time.timeScale = 1f;
    }

    #endregion

    #region Olaylar

    public void OnHit(Vector3 p, MoveData m, int combo)
    {
        AddHitstop(m.hitstop);
        Shake(m.IsHeavy ? 0.12f : 0.05f);
        SpawnSpark(p, m.IsHeavy ? new Color(1f, 0.8f, 0.25f) : Color.white, m.IsHeavy ? 0.55f : 0.35f, m.IsHeavy ? 8 : 5);
        Play(m.IsHeavy ? sHitHeavy : sHitLight, 1f + Mathf.Min(combo - 1, 6) * 0.05f);
    }

    public void OnBlock(Vector3 p, MoveData m)
    {
        AddHitstop(Mathf.Max(3, m.hitstop - 2));
        Shake(0.025f);
        SpawnSpark(p, new Color(0.4f, 0.9f, 1f), 0.3f, 4);
        Play(sBlock, Random.Range(0.95f, 1.05f));
    }

    public void OnWhiff(MoveData m)
    {
        Play(sWhiff, m.IsHeavy ? 0.75f : 1.15f, 0.5f);
    }

    public void OnKO(Vector3 p)
    {
        AddHitstop(12);
        Shake(0.3f);
        SpawnSpark(p, Color.white, 0.9f, 12);
        Play(sHitHeavy, 0.8f);
        Play(sKO, 1f);
        OnFlash?.Invoke();
        StopAllCoroutines();
        StartCoroutine(SlowMo());
    }

    public void OnBodyLand(Vector3 p)
    {
        Shake(0.06f);
        SpawnSpark(p, new Color(0.6f, 0.55f, 0.6f), 0.35f, 6);
        Play(sLand, Random.Range(0.9f, 1.1f));
    }

    public void PlayBlip() { Play(sBlip, 1f); }
    public void PlayFight() { Play(sFight, 1f); }
    public void PlayMenuMove() { Play(sMenuMove, 1f); }
    public void PlayMenuSelect() { Play(sMenuSelect, 1f); }

    IEnumerator SlowMo()
    {
        Time.timeScale = koSlowMoScale;
        yield return new WaitForSecondsRealtime(koSlowMoSeconds);
        while (MatchManager.Paused) yield return null;
        Time.timeScale = 1f;
    }

    void OnDestroy() { Time.timeScale = 1f; }

    #endregion

    #region Kıvılcımlar

    void SpawnSpark(Vector3 p, Color c, float size, int count)
    {
        EnsureInit();
        var s = new Spark { origin = p, size = size, life = 0.2f };
        s.mat = new Material(unlitShader) { color = c };

        s.flash = MakeCube(s.mat);
        s.flash.position = p;
        s.flash.rotation = Quaternion.Euler(0f, 0f, 45f);

        s.shards = new Transform[count];
        s.dirs = new Vector3[count];
        for (int i = 0; i < count; i++)
        {
            float a = i * 360f / count + Random.Range(-15f, 15f);
            s.dirs[i] = new Vector3(Mathf.Cos(a * Mathf.Deg2Rad), Mathf.Sin(a * Mathf.Deg2Rad), 0f);
            s.shards[i] = MakeCube(s.mat);
            s.shards[i].rotation = Quaternion.Euler(0f, 0f, a - 90f);
        }
        sparks.Add(s);
        UpdateSpark(s);
    }

    Transform MakeCube(Material m)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Destroy(go.GetComponent<Collider>());
        var r = go.GetComponent<Renderer>();
        r.sharedMaterial = m;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        go.transform.SetParent(sparkRoot, false);
        return go.transform;
    }

    void UpdateSpark(Spark s)
    {
        float p = Mathf.Clamp01(s.t / s.life);
        float inv = 1f - p;
        s.flash.localScale = Vector3.one * s.size * (0.6f + p) * inv;
        s.flash.rotation = Quaternion.Euler(0f, 0f, 45f + p * 90f);
        float dist = s.size * 2.2f * (1f - inv * inv);
        for (int i = 0; i < s.shards.Length; i++)
        {
            s.shards[i].position = s.origin + s.dirs[i] * dist;
            s.shards[i].localScale = new Vector3(s.size * 0.09f, s.size * 0.55f * inv, 0.02f);
        }
    }

    void Update()
    {
        if (MatchManager.Paused) return;

        float dt = Time.unscaledDeltaTime;
        shake = Mathf.Lerp(shake, 0f, 1f - Mathf.Exp(-shakeDecay * dt));
        ShakeOffset = shake > 0.001f ? (Vector3)(Random.insideUnitCircle * shake) : Vector3.zero;

        for (int i = sparks.Count - 1; i >= 0; i--)
        {
            var s = sparks[i];
            s.t += dt;
            if (s.t >= s.life)
            {
                Destroy(s.flash.gameObject);
                foreach (var sh in s.shards) Destroy(sh.gameObject);
                Destroy(s.mat);
                sparks.RemoveAt(i);
            }
            else UpdateSpark(s);
        }
    }

    #endregion

    #region Ses

    void Play(AudioClip clip, float pitch, float vol = 1f)
    {
        EnsureInit();
        if (clip == null) return;
        var src = sources[nextSource];
        nextSource = (nextSource + 1) % sources.Length;
        src.pitch = pitch;
        src.PlayOneShot(clip, volume * vol);
    }

    const int Rate = 22050;

    float Noise() { return (float)rng.NextDouble() * 2f - 1f; }

    AudioClip Make(string name, float dur, System.Func<float, float, float> f)
    {
        int n = Mathf.CeilToInt(dur * Rate);
        var data = new float[n];
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate;
            float s = Mathf.Clamp(f(t, t / dur), -1f, 1f);
            data[i] = Mathf.Round(s * 16f) / 16f * 0.8f;
        }
        var clip = AudioClip.Create(name, n, 1, Rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    void GenerateSounds()
    {
        const float TAU = Mathf.PI * 2f;

        float ph = 0f;
        sHitLight = Make("HitLight", 0.12f, (t, p) =>
        {
            ph += Mathf.Lerp(220f, 60f, p) / Rate;
            float env = Mathf.Pow(1f - p, 3f);
            return (Noise() * 0.6f + Mathf.Sin(TAU * ph) * 0.8f) * env;
        });

        float ph2 = 0f, lp = 0f;
        sHitHeavy = Make("HitHeavy", 0.28f, (t, p) =>
        {
            ph2 += Mathf.Lerp(140f, 40f, p) / Rate;
            lp += (Noise() - lp) * 0.25f;
            float env = Mathf.Pow(1f - p, 2f);
            return (lp * 1.3f + Mathf.Sin(TAU * ph2) * 0.9f) * env;
        });

        sBlock = Make("Block", 0.07f, (t, p) =>
        {
            float env = Mathf.Pow(1f - p, 2f);
            return (Mathf.Sign(Mathf.Sin(TAU * 1400f * t)) * 0.4f + Noise() * 0.3f) * env;
        });

        float lp2 = 0f;
        sWhiff = Make("Whiff", 0.12f, (t, p) =>
        {
            lp2 += (Noise() - lp2) * 0.15f;
            return lp2 * Mathf.Sin(Mathf.PI * p) * 1.6f;
        });

        float ph3 = 0f;
        sKO = Make("KO", 0.7f, (t, p) =>
        {
            ph3 += Mathf.Lerp(520f, 70f, p) / Rate;
            return (Mathf.Sign(Mathf.Sin(TAU * ph3)) * 0.45f + Noise() * 0.1f) * (1f - p);
        });

        float lp3 = 0f;
        sLand = Make("Land", 0.1f, (t, p) =>
        {
            lp3 += (Noise() - lp3) * 0.1f;
            float env = 1f - p;
            return lp3 * env * env * 1.8f + Mathf.Sin(TAU * 60f * t) * env * 0.5f;
        });

        sBlip = Make("Blip", 0.12f, (t, p) => Mathf.Sign(Mathf.Sin(TAU * 988f * t)) * 0.35f * (1f - p));

        sFight = Make("Fight", 0.4f, (t, p) =>
        {
            float f = p < 0.33f ? 523f : p < 0.66f ? 659f : 1046f;
            return Mathf.Sign(Mathf.Sin(TAU * f * t)) * 0.35f * (1f - p * 0.5f);
        });

        sMenuMove = Make("MenuMove", 0.05f, (t, p) => Mathf.Sign(Mathf.Sin(TAU * 660f * t)) * 0.25f * (1f - p));

        sMenuSelect = Make("MenuSelect", 0.16f, (t, p) =>
        {
            float f = p < 0.5f ? 784f : 1175f;
            return Mathf.Sign(Mathf.Sin(TAU * f * t)) * 0.3f * (1f - p * 0.6f);
        });
    }

    #endregion
}
