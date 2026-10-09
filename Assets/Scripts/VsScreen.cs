using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

public class VsScreen : MonoBehaviour
{
    #region Ayarlar

    [Tooltip("VS ekranının toplam süresi (sn). Yumrukla geçilebilir.")]
    public float duration = 4.6f;
    [Tooltip("Paneller kayarak geldikten sonra VS yazısının çarptığı an (sn).")]
    public float slamTime = 0.6f;
    [Range(0f, 1f)] public float volume = 0.8f;

    #endregion

    #region Durum

    public bool Playing { get; private set; }
    public bool Done => Playing && t >= duration;

    float t;
    bool slammed, swooshed;

    CanvasGroup group;
    RectTransform panel1, panel2, name1Rt, name2Rt, vsRt, stageRt;
    Vector2 name1Base, name2Base, tag1Base, tag2Base;
    Image fill1, fill2, edge1, edge2, flash;
    Text name1, name2, tag1, tag2, vsText, stageText;
    RectTransform[] stripes1, stripes2;
    AudioSource audioSrc;
    AudioClip swoosh, boom;
    Font font;

    const int PortraitLayer = 31;
    RawImage port1, port2;
    Vector2 port1Base, port2Base;
    Camera pcam1, pcam2;
    Light plight1, plight2;
    RenderTexture prt1, prt2;
    Fighter fa, fb;
    int layerA, layerB;
    bool posed;

    #endregion

    #region Kurulum

    public static VsScreen Create(Camera fightCam)
    {
        var go = new GameObject("VsScreen", typeof(RectTransform));
        var vs = go.AddComponent<VsScreen>();
        vs.Build(fightCam);
        return vs;
    }

    void Build(Camera cam)
    {
        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = cam;
        canvas.planeDistance = 0.45f;
        canvas.sortingOrder = 20;
        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(320f, 240f);
        scaler.matchWidthOrHeight = 0.5f;
        group = gameObject.AddComponent<CanvasGroup>();
        group.blocksRaycasts = false;
        group.interactable = false;

        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        var root = (RectTransform)transform;

        Img(Rect("Black", root, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero), Color.black);

        panel1 = BuildPanel(root, true, out fill1, out edge1, out stripes1);
        panel2 = BuildPanel(root, false, out fill2, out edge2, out stripes2);

        port1 = Raw(Rect("Portrait1", root, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(6, 80), new Vector2(126, 230)));
        port2 = Raw(Rect("Portrait2", root, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-126, -216), new Vector2(-6, -66)));
        port1Base = port1.rectTransform.anchoredPosition;
        port2Base = port2.rectTransform.anchoredPosition;

        tag1 = Txt(root, "Tag1", new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(14, 62), new Vector2(160, 78), 11, TextAnchor.MiddleLeft, Color.white);
        tag2 = Txt(root, "Tag2", new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-160, -28), new Vector2(-14, -14), 11, TextAnchor.MiddleRight, Color.white);

        name1 = Txt(root, "Name1", new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(12, 30), new Vector2(200, 62), 26, TextAnchor.MiddleLeft, Color.white);
        name2 = Txt(root, "Name2", new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-200, -62), new Vector2(-12, -30), 26, TextAnchor.MiddleRight, Color.white);
        name1Rt = name1.rectTransform;
        name2Rt = name2.rectTransform;
        name1Base = name1Rt.anchoredPosition;
        name2Base = name2Rt.anchoredPosition;
        tag1Base = tag1.rectTransform.anchoredPosition;
        tag2Base = tag2.rectTransform.anchoredPosition;
        foreach (var n in new[] { name1, name2 }) n.fontStyle = FontStyle.BoldAndItalic;

        vsText = Txt(root, "VS", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-60, -34), new Vector2(60, 34), 52, TextAnchor.MiddleCenter, new Color(1f, 0.85f, 0.2f));
        vsText.text = "VS";
        vsText.fontStyle = FontStyle.BoldAndItalic;
        var vsOutline = vsText.GetComponent<Outline>();
        vsOutline.effectDistance = new Vector2(2f, -2f);
        var vsShadow = vsText.gameObject.AddComponent<Shadow>();
        vsShadow.effectColor = new Color(0.8f, 0.1f, 0.1f, 1f);
        vsShadow.effectDistance = new Vector2(3f, -3f);
        vsRt = vsText.rectTransform;

        stageText = Txt(root, "Stage", new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0, 6), new Vector2(0, 20), 9, TextAnchor.MiddleCenter, new Color(1f, 0.85f, 0.2f));
        stageRt = stageText.rectTransform;

        flash = Img(Rect("Flash", root, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero), new Color(1f, 1f, 1f, 0f));

        audioSrc = gameObject.AddComponent<AudioSource>();
        audioSrc.playOnAwake = false;
        audioSrc.spatialBlend = 0f;
        MakeSounds();

        gameObject.SetActive(false);
    }

    RectTransform BuildPanel(RectTransform root, bool left, out Image fill, out Image edge, out RectTransform[] stripes)
    {
        var holder = Rect(left ? "Panel1" : "Panel2", root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        holder.sizeDelta = new Vector2(420f, 520f);
        holder.pivot = new Vector2(left ? 1f : 0f, 0.5f);
        holder.localRotation = Quaternion.Euler(0f, 0f, 14f);

        fill = Img(Rect("Fill", holder, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero), Color.gray);

        stripes = new RectTransform[6];
        for (int i = 0; i < stripes.Length; i++)
        {
            float y = -200f + i * 75f;
            var s = Rect("Stripe" + i, holder, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            s.sizeDelta = new Vector2(140f + (i % 3) * 60f, 3f + (i % 2) * 3f);
            s.anchoredPosition = new Vector2(0f, y);
            Img(s, new Color(1f, 1f, 1f, 0.13f));
            stripes[i] = s;
        }

        var e = Rect("Edge", holder, new Vector2(left ? 1f : 0f, 0f), new Vector2(left ? 1f : 0f, 1f), Vector2.zero, Vector2.zero);
        e.sizeDelta = new Vector2(5f, 0f);
        edge = Img(e, Color.white);
        return holder;
    }

    #endregion

    #region Oynatma

    public void Play(string n1, string n2, Color c1, Color c2, string label1, string label2, string stage, Fighter a = null, Fighter b = null)
    {
        gameObject.SetActive(true);
        Playing = true;
        t = 0f;
        slammed = swooshed = false;

        name1.text = n1;
        name2.text = n2;
        tag1.text = label1;
        tag2.text = label2;
        tag1.color = Color.Lerp(c1, Color.white, 0.55f);
        tag2.color = Color.Lerp(c2, Color.white, 0.55f);
        stageText.text = "STAGE  -  " + stage;

        fill1.color = Shade(c1, 0.55f);
        fill2.color = Shade(c2, 0.55f);
        edge1.color = Color.Lerp(c1, Color.white, 0.5f);
        edge2.color = Color.Lerp(c2, Color.white, 0.5f);

        group.alpha = 1f;
        flash.color = new Color(1f, 1f, 1f, 0f);
        SetupPortraits(a, b);
        Animate();
    }

    public void Skip()
    {
        if (Playing) t = Mathf.Max(t, duration - 0.2f);
    }

    public void Hide()
    {
        Playing = false;
        if (pcam1 != null) pcam1.enabled = false;
        if (pcam2 != null) pcam2.enabled = false;
        if (plight1 != null) plight1.enabled = false;
        if (plight2 != null) plight2.enabled = false;
        if (fa != null) SetLayer(fa.transform, layerA);
        if (fb != null) SetLayer(fb.transform, layerB);
        fa = fb = null;
        gameObject.SetActive(false);
    }

    void Update()
    {
        if (!Playing) return;
        t += Time.unscaledDeltaTime;
        Animate();
    }

    void Animate()
    {
        float inK = EaseOut(Mathf.Clamp01(t / 0.35f));
        panel1.anchoredPosition = new Vector2(Mathf.Lerp(-260f, 0f, inK), 0f);
        panel2.anchoredPosition = new Vector2(Mathf.Lerp(260f, 0f, inK), 0f);

        float nameK = EaseOut(Mathf.Clamp01((t - 0.15f) / 0.35f));
        float drift = Mathf.Round(Mathf.Sin(t * 2.2f) * 1.5f);
        float slide = Mathf.Round(Mathf.Lerp(220f, 0f, nameK));
        name1Rt.anchoredPosition = name1Base + new Vector2(-slide + drift, 0f);
        name2Rt.anchoredPosition = name2Base + new Vector2(slide - drift, 0f);
        tag1.rectTransform.anchoredPosition = tag1Base + new Vector2(-slide, 0f);
        tag2.rectTransform.anchoredPosition = tag2Base + new Vector2(slide, 0f);

        for (int i = 0; i < stripes1.Length; i++)
        {
            float speed = 120f + i * 35f;
            float x = Mathf.Repeat(t * speed + i * 57f, 420f) - 210f;
            stripes1[i].anchoredPosition = new Vector2(x, stripes1[i].anchoredPosition.y);
            stripes2[i].anchoredPosition = new Vector2(-x, stripes2[i].anchoredPosition.y);
        }

        port1.rectTransform.anchoredPosition = port1Base + new Vector2(-slide * 1.2f, 0f);
        port2.rectTransform.anchoredPosition = port2Base + new Vector2(slide * 1.2f, 0f);
        if (!posed && t >= slamTime + 0.15f)
        {
            posed = true;
            if (fa != null) fa.SetWin();
            if (fb != null) fb.SetWin();
        }

        if (!swooshed && t > 0.02f) { swooshed = true; PlaySound(swoosh, 1f); }

        if (t < slamTime)
        {
            vsText.enabled = false;
        }
        else
        {
            vsText.enabled = true;
            float sk = Mathf.Clamp01((t - slamTime) / 0.12f);
            float scale = Mathf.Lerp(3.2f, 1f, sk * sk);
            float shake = t - slamTime < 0.35f ? (0.35f - (t - slamTime)) * 10f : 0.6f;
            vsRt.localScale = Vector3.one * scale;
            vsRt.anchoredPosition = new Vector2(Mathf.Round(Random.Range(-shake, shake)), Mathf.Round(Random.Range(-shake, shake)));
            if (!slammed && sk >= 1f)
            {
                slammed = true;
                flash.color = new Color(1f, 1f, 1f, 0.85f);
                PlaySound(boom, 1f);
                FightFX.I?.Shake(0.12f);
            }
        }

        var fc = flash.color;
        fc.a = Mathf.MoveTowards(fc.a, 0f, Time.unscaledDeltaTime * 3.5f);
        flash.color = fc;

        stageText.enabled = t > slamTime + 0.25f && Mathf.Repeat(t, 0.8f) < 0.6f;

        float outK = Mathf.Clamp01((t - (duration - 0.25f)) / 0.25f);
        group.alpha = 1f - outK;
    }

    #endregion

    #region Karakter portreleri

    void SetupPortraits(Fighter a, Fighter b)
    {
        posed = false;
        bool ok = a != null && b != null && a.transform.parent != null;
        port1.enabled = port2.enabled = ok;
        if (!ok) return;

        if (pcam1 == null)
        {
            var parent = a.transform.parent;
            prt1 = MakeRT();
            prt2 = MakeRT();
            pcam1 = MakeCam("VsPortraitCam1", prt1, parent, out plight1);
            pcam2 = MakeCam("VsPortraitCam2", prt2, parent, out plight2);
            port1.texture = prt1;
            port2.texture = prt2;
        }

        fa = a;
        fb = b;
        layerA = a.gameObject.layer;
        layerB = b.gameObject.layer;
        SetLayer(a.transform, PortraitLayer);
        SetLayer(b.transform, PortraitLayer);

        var f1 = fill1.color;
        var f2 = fill2.color;
        pcam1.backgroundColor = new Color(f1.r, f1.g, f1.b, 0f);
        pcam2.backgroundColor = new Color(f2.r, f2.g, f2.b, 0f);
        Aim(pcam1, plight1, a);
        Aim(pcam2, plight2, b);
        pcam1.enabled = pcam2.enabled = true;
        plight1.enabled = plight2.enabled = true;
    }

    static void Aim(Camera c, Light l, Fighter f)
    {
        var p = f.transform.localPosition;
        c.transform.localPosition = new Vector3(p.x, 1.05f, -3.6f);
        c.transform.localRotation = Quaternion.LookRotation(new Vector3(p.x, 1.0f, 0f) - c.transform.localPosition);
        l.transform.localPosition = new Vector3(p.x + f.Facing * 1.2f, 2.2f, -1.6f);
    }

    static RenderTexture MakeRT()
    {
        var r = new RenderTexture(120, 150, 24) { filterMode = FilterMode.Point, name = "VsPortrait" };
        r.Create();
        return r;
    }

    static Camera MakeCam(string name, RenderTexture rt, Transform parent, out Light light)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var c = go.AddComponent<Camera>();
        c.targetTexture = rt;
        c.clearFlags = CameraClearFlags.SolidColor;
        c.cullingMask = 1 << PortraitLayer;
        c.fieldOfView = 34f;
        c.nearClipPlane = 0.1f;
        c.farClipPlane = 20f;
        c.allowHDR = false;
        c.allowMSAA = false;
        c.depth = -10f;
        c.enabled = false;
        var data = c.GetUniversalAdditionalCameraData();
        data.renderPostProcessing = false;
        data.renderShadows = false;

        var lg = new GameObject(name + "Light");
        lg.transform.SetParent(parent, false);
        light = lg.AddComponent<Light>();
        light.type = LightType.Point;
        light.range = 4f;
        light.intensity = 1.6f;
        light.color = new Color(1f, 0.95f, 0.9f);
        light.cullingMask = 1 << PortraitLayer;
        light.enabled = false;
        return c;
    }

    static void SetLayer(Transform t, int layer)
    {
        t.gameObject.layer = layer;
        for (int i = 0; i < t.childCount; i++) SetLayer(t.GetChild(i), layer);
    }

    static RawImage Raw(RectTransform r)
    {
        var img = r.gameObject.AddComponent<RawImage>();
        img.raycastTarget = false;
        img.color = Color.white;
        return img;
    }

    static float EaseOut(float k) { return 1f - (1f - k) * (1f - k) * (1f - k); }

    static Color Shade(Color c, float k) { return new Color(c.r * k, c.g * k, c.b * k, 1f); }

    #endregion

    #region Ses

    void PlaySound(AudioClip clip, float pitch)
    {
        if (clip == null) return;
        audioSrc.pitch = pitch;
        audioSrc.PlayOneShot(clip, volume);
    }

    void MakeSounds()
    {
        const int rate = 22050;
        var rng = new System.Random(77);

        int n = rate * 3 / 10;
        var a = new float[n];
        float lp = 0f;
        for (int i = 0; i < n; i++)
        {
            float p = i / (float)n;
            lp += ((float)rng.NextDouble() * 2f - 1f - lp) * Mathf.Lerp(0.04f, 0.35f, p);
            a[i] = Mathf.Round(lp * Mathf.Sin(Mathf.PI * p) * 1.8f * 16f) / 16f * 0.8f;
        }
        swoosh = AudioClip.Create("VsSwoosh", n, 1, rate, false);
        swoosh.SetData(a, 0);

        n = rate * 6 / 10;
        var b = new float[n];
        float ph = 0f, lp2 = 0f;
        for (int i = 0; i < n; i++)
        {
            float p = i / (float)n;
            ph += Mathf.Lerp(150f, 38f, p) / rate;
            lp2 += ((float)rng.NextDouble() * 2f - 1f - lp2) * 0.2f;
            float env = Mathf.Pow(1f - p, 2.2f);
            float s = (Mathf.Sin(ph * Mathf.PI * 2f) * 0.95f + lp2 * (p < 0.15f ? 1.4f : 0.4f)) * env;
            b[i] = Mathf.Round(Mathf.Clamp(s, -1f, 1f) * 16f) / 16f * 0.85f;
        }
        boom = AudioClip.Create("VsBoom", n, 1, rate, false);
        boom.SetData(b, 0);
    }

    #endregion

    #region UI yardımcıları

    static RectTransform Rect(string name, RectTransform parent, Vector2 aMin, Vector2 aMax, Vector2 oMin, Vector2 oMax)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var r = (RectTransform)go.transform;
        r.SetParent(parent, false);
        r.anchorMin = aMin; r.anchorMax = aMax;
        r.offsetMin = oMin; r.offsetMax = oMax;
        return r;
    }

    static Image Img(RectTransform r, Color c)
    {
        var img = r.gameObject.AddComponent<Image>();
        img.color = c;
        img.raycastTarget = false;
        return img;
    }

    Text Txt(RectTransform parent, string name, Vector2 aMin, Vector2 aMax, Vector2 oMin, Vector2 oMax, int size, TextAnchor align, Color c)
    {
        var r = Rect(name, parent, aMin, aMax, oMin, oMax);
        var t = r.gameObject.AddComponent<Text>();
        t.font = font;
        t.fontSize = size;
        t.fontStyle = FontStyle.Bold;
        t.alignment = align;
        t.color = c;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.raycastTarget = false;
        var o = r.gameObject.AddComponent<Outline>();
        o.effectColor = Color.black;
        o.effectDistance = new Vector2(1f, -1f);
        return t;
    }

    #endregion
}
