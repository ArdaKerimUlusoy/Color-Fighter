using UnityEngine;

public class InsertCoin : MonoBehaviour
{
    #region Ayarlar

    [Tooltip("Jetonun deliğe ulaşma süresi (sn).")]
    public float flightTime = 0.55f;
    [Range(0f, 1f)] public float volume = 0.85f;

    #endregion

    #region Durum

    Transform slot, coin;
    Renderer slotRenderer;
    Material slotMat;
    Color slotBase;
    Light slotLight;
    Vector3 start, end;
    float t;
    bool running, clinked, credited, coinShown;
    AudioSource audioSrc;
    AudioClip clink, credit;

    #endregion

    #region Dış API

    public static void Play(Transform arcadeRoot, float delay = 0f)
    {
        if (arcadeRoot == null) return;
        var ic = arcadeRoot.GetComponent<InsertCoin>();
        if (ic == null) ic = arcadeRoot.gameObject.AddComponent<InsertCoin>();
        ic.Begin(delay);
    }

    public static bool FindSlot(Transform arcadeRoot, out Transform slot)
    {
        slot = null;
        if (arcadeRoot == null) return false;
        foreach (var tr in arcadeRoot.GetComponentsInChildren<Transform>(true))
        {
            if (tr.name == "CoinSlotR" && tr.parent != null && tr.parent.name == "ArcadeCabinet") { slot = tr; return true; }
            if (slot == null && (tr.name == "CoinSlotR" || tr.name == "CoinSlot")) slot = tr;
        }
        return slot != null;
    }

    #endregion

    #region Oynatma

    void Begin(float delay)
    {
        if (audioSrc == null) Setup();
        t = -Mathf.Max(0f, delay);
        running = true;
        clinked = credited = coinShown = false;
        if (coin != null) coin.gameObject.SetActive(false);
    }

    void Setup()
    {
        FindSlot(transform, out slot);

        audioSrc = gameObject.AddComponent<AudioSource>();
        audioSrc.playOnAwake = false;
        audioSrc.spatialBlend = 0f;
        MakeSounds();

        if (slot == null) return;

        slotRenderer = slot.GetComponent<Renderer>();
        if (slotRenderer != null)
        {
            slotMat = slotRenderer.material;
            slotBase = slotMat.color;
        }

        var lightGo = new GameObject("CoinSlotLight");
        lightGo.transform.SetParent(slot.parent, false);
        lightGo.transform.position = slot.position - slot.parent.forward * 0.15f;
        slotLight = lightGo.AddComponent<Light>();
        slotLight.type = LightType.Point;
        slotLight.color = new Color(1f, 0.6f, 0.15f);
        slotLight.range = 0.9f;
        slotLight.intensity = 0f;

        var c = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Destroy(c.GetComponent<Collider>());
        c.name = "Coin";
        coin = c.transform;
        coin.SetParent(slot.parent, false);
        coin.localScale = new Vector3(0.055f, 0.004f, 0.055f);
        var cr = c.GetComponent<Renderer>();
        cr.sharedMaterial = CFMaterials.Unlit(new Color(1f, 0.82f, 0.3f));
        cr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        c.SetActive(false);

        var fwd = slot.parent.forward;
        end = slot.position;
        start = end - fwd * 0.7f + Vector3.up * 0.35f + slot.parent.right * 0.25f;
    }

    void Update()
    {
        if (!running) return;
        t += Time.unscaledDeltaTime;
        if (t < 0f) return;

        if (slot == null)
        {
            if (!credited) { credited = true; PlaySound(credit, 1f); }
            running = false;
            return;
        }

        if (!coinShown && coin != null)
        {
            coinShown = true;
            coin.gameObject.SetActive(true);
            coin.position = start;
        }

        if (coin != null && coin.gameObject.activeSelf)
        {
            float k = Mathf.Clamp01(t / flightTime);
            float e = 1f - (1f - k) * (1f - k);
            Vector3 p = Vector3.Lerp(start, end, e) + Vector3.up * Mathf.Sin(Mathf.PI * k) * 0.18f;
            coin.position = p;
            coin.rotation = slot.parent.rotation * Quaternion.Euler(90f, 0f, Mathf.Lerp(0f, 720f, e));
            if (k >= 1f)
            {
                coin.gameObject.SetActive(false);
                if (!clinked) { clinked = true; PlaySound(clink, 1f); }
            }
        }

        if (!credited && t >= flightTime + 0.12f)
        {
            credited = true;
            PlaySound(credit, 1f);
        }

        float glow = t >= flightTime ? Mathf.Clamp01(1f - (t - flightTime) / 0.9f) : 0f;
        if (slotMat != null) slotMat.color = Color.Lerp(slotBase, new Color(1.8f, 1.5f, 0.8f), glow);
        if (slotLight != null) slotLight.intensity = glow * 2.5f;

        if (t > flightTime + 1.2f) running = false;
    }

    #endregion

    #region Ses

    void PlaySound(AudioClip clip, float pitch)
    {
        if (clip == null || audioSrc == null) return;
        audioSrc.pitch = pitch;
        audioSrc.PlayOneShot(clip, volume);
    }

    void MakeSounds()
    {
        const int rate = 22050;
        const float TAU = Mathf.PI * 2f;

        int n = rate * 35 / 100;
        var a = new float[n];
        for (int i = 0; i < n; i++)
        {
            float tt = i / (float)rate, p = i / (float)n;
            float env = Mathf.Exp(-p * 7f);
            float s = Mathf.Sin(TAU * 2350f * tt) * 0.45f + Mathf.Sin(TAU * 3720f * tt) * 0.3f + Mathf.Sin(TAU * 5180f * tt) * 0.15f;
            if (tt > 0.07f) s += (Mathf.Sin(TAU * 2350f * (tt - 0.07f)) * 0.35f) * Mathf.Exp(-(tt - 0.07f) * 25f);
            a[i] = Mathf.Round(Mathf.Clamp(s * env, -1f, 1f) * 16f) / 16f * 0.8f;
        }
        clink = AudioClip.Create("CoinClink", n, 1, rate, false);
        clink.SetData(a, 0);

        n = rate * 45 / 100;
        var b = new float[n];
        for (int i = 0; i < n; i++)
        {
            float tt = i / (float)rate;
            float f = tt < 0.09f ? 988f : tt < 0.18f ? 1319f : 1976f;
            float env = tt < 0.18f ? 1f : Mathf.Max(0f, 1f - (tt - 0.18f) / 0.27f);
            b[i] = Mathf.Sign(Mathf.Sin(TAU * f * tt)) * 0.3f * env;
        }
        credit = AudioClip.Create("CoinCredit", n, 1, rate, false);
        credit.SetData(b, 0);
    }

    #endregion
}
