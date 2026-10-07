using UnityEngine;
using UnityEngine.UI;

public class FightHUD : MonoBehaviour
{
    #region Referanslar

    [SerializeField, HideInInspector] RectTransform fill1, fill2, chip1, chip2;
    [SerializeField, HideInInspector] Text timerText, centerText, combo1, combo2;
    [SerializeField, HideInInspector] Image flash;
    [SerializeField, HideInInspector] Image[] pips1, pips2;
    [SerializeField, HideInInspector] GameObject fightGroup, titleGroup, pauseGroup;
    [SerializeField, HideInInspector] Text titleTop, titleBottom, pressStart;
    [SerializeField, HideInInspector] Text[] pauseOptions;
    [SerializeField, HideInInspector] string[] pauseLabels;
    Font font;

    #endregion

    #region Durum

    float target1 = 1f, target2 = 1f, chipv1 = 1f, chipv2 = 1f;
    float chipDelay1, chipDelay2;
    float centerHideAt = -1f, comboHide1, comboHide2, flashA;
    float titleShownAt;
    int pauseSelection;

    static readonly Color PipOff = new Color(0.25f, 0.25f, 0.3f);
    static readonly Color PipOn = new Color(1f, 0.85f, 0.2f);
    static readonly Color OptionOff = new Color(0.55f, 0.55f, 0.6f);

    #endregion

    #region Kurulum

    public void Build(Camera cam, string n1, string n2, Color c1, Color c2, int roundsToWin)
    {
        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = cam;
        canvas.planeDistance = 0.5f;
        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(320f, 240f);
        scaler.matchWidthOrHeight = 0.5f;

        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        var canvasRoot = GetComponent<RectTransform>();

        var root = Rect("FightUI", canvasRoot, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        fightGroup = root.gameObject;

        BuildBar(root, true, c1, out fill1, out chip1);
        BuildBar(root, false, c2, out fill2, out chip2);

        Txt(root, "Name1", new Vector2(0, 1), new Vector2(0, 1), new Vector2(8, -36), new Vector2(140, -24), 10, TextAnchor.UpperLeft, c1).text = n1;
        Txt(root, "Name2", new Vector2(1, 1), new Vector2(1, 1), new Vector2(-140, -36), new Vector2(-8, -24), 10, TextAnchor.UpperRight, c2).text = n2;

        pips1 = new Image[roundsToWin];
        pips2 = new Image[roundsToWin];
        for (int i = 0; i < roundsToWin; i++)
        {
            pips1[i] = Img(Rect("Pip1", root, new Vector2(0, 1), new Vector2(0, 1),
                new Vector2(133 - i * 10, -33), new Vector2(140 - i * 10, -26)), PipOff);
            pips2[i] = Img(Rect("Pip2", root, new Vector2(1, 1), new Vector2(1, 1),
                new Vector2(-140 + i * 10, -33), new Vector2(-133 + i * 10, -26)), PipOff);
        }

        timerText = Txt(root, "Timer", new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(-20, -30), new Vector2(20, -6), 18, TextAnchor.MiddleCenter, Color.white);
        timerText.text = "60";
        combo1 = Txt(root, "Combo1", new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(8, 10), new Vector2(150, 40), 14, TextAnchor.MiddleLeft, new Color(1f, 0.85f, 0.2f));
        combo2 = Txt(root, "Combo2", new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-150, 10), new Vector2(-8, 40), 14, TextAnchor.MiddleRight, new Color(1f, 0.85f, 0.2f));
        combo1.enabled = combo2.enabled = false;

        centerText = Txt(canvasRoot, "Center", new Vector2(0, 0.5f), new Vector2(1, 0.5f), new Vector2(0, -40), new Vector2(0, 40), 30, TextAnchor.MiddleCenter, Color.yellow);
        centerText.enabled = false;

        BuildTitle(canvasRoot, c1, c2);
        BuildPause(canvasRoot);

        flash = Img(Rect("Flash", canvasRoot, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero), new Color(1, 1, 1, 0));
    }

    void BuildTitle(RectTransform parent, Color c1, Color c2)
    {
        var root = Rect("TitleUI", parent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        titleGroup = root.gameObject;

        Img(Rect("Dim", root, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero), new Color(0f, 0f, 0f, 0.45f));
        Img(Rect("BandRed", root, new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 2), new Vector2(0, 74)), new Color(c1.r, c1.g, c1.b, 0.35f));
        Img(Rect("BandBlue", root, new Vector2(0.5f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0, 2), new Vector2(0, 74)), new Color(c2.r, c2.g, c2.b, 0.35f));

        titleTop = Txt(root, "TitleColor", new Vector2(0, 0.5f), new Vector2(1, 0.5f), new Vector2(0, 38), new Vector2(0, 74), 34, TextAnchor.MiddleCenter, Color.Lerp(c1, Color.white, 0.2f));
        titleTop.text = "COLOR";
        titleBottom = Txt(root, "TitleFighter", new Vector2(0, 0.5f), new Vector2(1, 0.5f), new Vector2(0, 2), new Vector2(0, 38), 34, TextAnchor.MiddleCenter, Color.Lerp(c2, Color.white, 0.2f));
        titleBottom.text = "FIGHTER";
        foreach (var t in new[] { titleTop, titleBottom })
            t.GetComponent<Outline>().effectDistance = new Vector2(2f, -2f);

        pressStart = Txt(root, "PressStart", new Vector2(0, 0.5f), new Vector2(1, 0.5f), new Vector2(0, -44), new Vector2(0, -24), 14, TextAnchor.MiddleCenter, Color.white);
        pressStart.text = "PRESS PUNCH TO START";

        var hint = Txt(root, "Controls", new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 22), new Vector2(0, 36), 9, TextAnchor.MiddleCenter, new Color(0.8f, 0.8f, 0.85f));
        hint.text = "<color=#FF5050>RED</color> WASD + F G      <color=#5A8CFF>BLUE</color> ARROWS + K L      ESC PAUSE";
        var credit = Txt(root, "Credit", new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 6), new Vector2(0, 18), 9, TextAnchor.MiddleCenter, new Color(1f, 0.85f, 0.2f));
        credit.text = "CREDIT 02";

        titleGroup.SetActive(false);
    }

    void BuildPause(RectTransform parent)
    {
        var root = Rect("PauseUI", parent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        pauseGroup = root.gameObject;

        Img(Rect("Dim", root, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero), new Color(0f, 0f, 0.05f, 0.7f));
        Img(Rect("Frame", root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-90, -52), new Vector2(90, 62)), new Color(1f, 1f, 1f, 0.9f));
        Img(Rect("Panel", root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-88, -50), new Vector2(88, 60)), new Color(0.06f, 0.03f, 0.12f, 1f));

        var title = Txt(root, "Paused", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-88, 30), new Vector2(88, 58), 22, TextAnchor.MiddleCenter, new Color(1f, 0.85f, 0.2f));
        title.text = "PAUSED";

        pauseLabels = new[] { "RESUME", "RESTART MATCH", "QUIT TO TITLE" };
        pauseOptions = new Text[pauseLabels.Length];
        for (int i = 0; i < pauseLabels.Length; i++)
        {
            float y = 8 - i * 20;
            pauseOptions[i] = Txt(root, "Option" + i, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-88, y - 9), new Vector2(88, y + 9), 13, TextAnchor.MiddleCenter, OptionOff);
            pauseOptions[i].text = pauseLabels[i];
        }

        var hint = Txt(root, "PauseHint", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-88, -50), new Vector2(88, -38), 8, TextAnchor.MiddleCenter, new Color(0.7f, 0.7f, 0.75f));
        hint.text = "UP/DOWN  -  PUNCH SELECT  -  ESC BACK";

        pauseGroup.SetActive(false);
    }

    #endregion

    #region Dış API

    public void SetHealth(float a, float b)
    {
        if (a < target1) chipDelay1 = 0.5f;
        if (b < target2) chipDelay2 = 0.5f;
        target1 = a; target2 = b;
    }

    public void ResetBars() { target1 = target2 = chipv1 = chipv2 = 1f; }

    public void SetTimer(int seconds) { timerText.text = Mathf.Clamp(seconds, 0, 99).ToString("00"); }

    public void ShowCenter(string text, Color c, float duration)
    {
        centerText.text = text;
        centerText.color = c;
        centerText.enabled = true;
        centerText.rectTransform.localScale = Vector3.one * 1.8f;
        centerHideAt = duration > 0f ? Time.unscaledTime + duration : -1f;
    }

    public void HideCenter() { centerText.enabled = false; }

    public void ShowCombo(int side, int hits)
    {
        var t = side == 0 ? combo1 : combo2;
        t.text = hits + " HITS!";
        t.enabled = true;
        t.rectTransform.localScale = Vector3.one * 1.5f;
        if (side == 0) comboHide1 = Time.unscaledTime + 1.2f; else comboHide2 = Time.unscaledTime + 1.2f;
    }

    public void SetWins(int w1, int w2)
    {
        for (int i = 0; i < pips1.Length; i++) pips1[i].color = i < w1 ? PipOn : PipOff;
        for (int i = 0; i < pips2.Length; i++) pips2[i].color = i < w2 ? PipOn : PipOff;
    }

    public void Flash() { flashA = 0.85f; }

    public void ShowTitle(bool on)
    {
        titleGroup.SetActive(on);
        fightGroup.SetActive(!on);
        if (on)
        {
            centerText.enabled = false;
            titleShownAt = Time.unscaledTime;
        }
    }

    public void ShowPause(bool on)
    {
        pauseGroup.SetActive(on);
        if (on) SetPauseSelection(0);
    }

    public int PauseOptionCount => pauseLabels.Length;

    public void SetPauseSelection(int index)
    {
        pauseSelection = index;
        RefreshPauseOptions(true);
    }

    #endregion

    #region Güncelleme

    void Update()
    {
        float dt = Time.unscaledDeltaTime;

        chipDelay1 -= dt; chipDelay2 -= dt;
        chipv1 = target1 > chipv1 ? target1 : (chipDelay1 <= 0f ? Mathf.MoveTowards(chipv1, target1, dt * 0.6f) : chipv1);
        chipv2 = target2 > chipv2 ? target2 : (chipDelay2 <= 0f ? Mathf.MoveTowards(chipv2, target2, dt * 0.6f) : chipv2);

        SetBar(fill1, target1, true);  SetBar(chip1, chipv1, true);
        SetBar(fill2, target2, false); SetBar(chip2, chipv2, false);

        if (centerText.enabled)
        {
            var s = centerText.rectTransform;
            s.localScale = Vector3.Lerp(s.localScale, Vector3.one, 1f - Mathf.Exp(-14f * dt));
            if (centerHideAt > 0f && Time.unscaledTime > centerHideAt) centerText.enabled = false;
        }
        UpdateCombo(combo1, comboHide1, dt);
        UpdateCombo(combo2, comboHide2, dt);

        flashA = Mathf.MoveTowards(flashA, 0f, dt * 2.5f);
        flash.color = new Color(1f, 1f, 1f, flashA);

        if (titleGroup.activeSelf) AnimateTitle();
        if (pauseGroup.activeSelf) RefreshPauseOptions(Mathf.Repeat(Time.unscaledTime, 0.5f) < 0.35f);
    }

    void AnimateTitle()
    {
        float t = Time.unscaledTime - titleShownAt;
        float inTop = Mathf.Clamp01(t / 0.35f);
        float inBottom = Mathf.Clamp01((t - 0.2f) / 0.35f);
        float bob = Mathf.Sin(Time.unscaledTime * 2f) * 2f;
        titleTop.rectTransform.anchoredPosition = new Vector2(Mathf.Round(-320f * (1f - inTop) * (1f - inTop)), Mathf.Round(56f + bob));
        titleBottom.rectTransform.anchoredPosition = new Vector2(Mathf.Round(320f * (1f - inBottom) * (1f - inBottom)), Mathf.Round(20f - bob));
        pressStart.enabled = t > 0.8f && Mathf.Repeat(Time.unscaledTime, 0.9f) < 0.6f;
    }

    void RefreshPauseOptions(bool cursorVisible)
    {
        for (int i = 0; i < pauseOptions.Length; i++)
        {
            bool sel = i == pauseSelection;
            pauseOptions[i].color = sel ? Color.white : OptionOff;
            pauseOptions[i].text = sel && cursorVisible ? "> " + pauseLabels[i] + " <" : pauseLabels[i];
        }
    }

    void UpdateCombo(Text t, float hideAt, float dt)
    {
        if (!t.enabled) return;
        t.rectTransform.localScale = Vector3.Lerp(t.rectTransform.localScale, Vector3.one, 1f - Mathf.Exp(-14f * dt));
        if (Time.unscaledTime > hideAt) t.enabled = false;
    }

    static void SetBar(RectTransform r, float v, bool left)
    {
        v = Mathf.Clamp01(v);
        if (left) { r.anchorMin = new Vector2(0f, 0f); r.anchorMax = new Vector2(v, 1f); }
        else { r.anchorMin = new Vector2(1f - v, 0f); r.anchorMax = new Vector2(1f, 1f); }
        r.offsetMin = r.offsetMax = Vector2.zero;
    }

    #endregion

    #region UI yardımcıları

    void BuildBar(RectTransform parent, bool left, Color c, out RectTransform fill, out RectTransform chip)
    {
        Vector2 a = left ? new Vector2(0, 1) : new Vector2(1, 1);
        var frame = Rect(left ? "Bar1" : "Bar2", parent, a, a,
            left ? new Vector2(8, -22) : new Vector2(-140, -22),
            left ? new Vector2(140, -10) : new Vector2(-8, -10));
        Img(frame, Color.white);
        var bg = Rect("BG", frame, Vector2.zero, Vector2.one, new Vector2(1, 1), new Vector2(-1, -1));
        Img(bg, new Color(0.25f, 0.05f, 0.05f));
        chip = Rect("Chip", bg, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        Img(chip, new Color(1f, 1f, 1f, 0.9f));
        fill = Rect("Fill", bg, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        Img(fill, Color.Lerp(c, Color.white, 0.15f));
    }

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
