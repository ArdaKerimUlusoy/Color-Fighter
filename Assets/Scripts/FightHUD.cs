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

    // Oyun sırasında eklenen parçalar (önceden kurulmuş sahnelerde de çalışsın diye runtime'da oluşturulur).
    bool extrasBuilt;
    Text name1, name2, comboLabel1, comboLabel2;
    Image fillImg1, fillImg2;
    Image[] comboPips1, comboPips2;
    RectTransform comboTimer1, comboTimer2;
    string comboKey1 = "H", comboKey2 = ";";
    string kbKey1 = "H", kbKey2 = ";";
    Text controlsHint, selRule, padGuideHead;
    GameObject padGuide;
    string hintState = "";
    GameObject selectGroup;
    FighterPalette.Entry[] palette;
    Image[] cellFrames, cellBGs;
    Text[] cellTag1, cellTag2;
    Text selName1, selName2, selStatus1, selStatus2;
    int selCur1, selCur2;
    bool selLock1, selLock2;
    float deny1Until, deny2Until;
    bool selShowCur2 = true, cpuMode;
    Text koText;
    RectTransform koBand;
    Image koBandImg;
    float koStart = -1f;
    Text finisherText;
    float finisherShownAt = -10f;
    GameObject modeGroup;
    Text[] modeOptions;
    Text modeDesc;
    GameObject padsGroup;
    RectTransform padsColumns;
    RectTransform[] padIcons;
    Image[][] padTint;
    Text[] padLabels;
    float[] padX, padY;
    RectTransform[] kbIcons;
    Text[] kbLabels, kbSubs;
    float[] kbY = new float[3];
    int[] padSlot = new int[0];
    bool[] padHot = new bool[0];
    bool padsCpu;
    Text padsHead2, padsKey1, padsKey2, padsEmpty, padsModeLine;
    const int MaxPadIcons = 3;
    int modeSel;
    GameObject stageGroup;
    Text stageName, stageDesc, stageHint, stageTitle;
    RectTransform stagePipRow;
    Image[] stagePips;
    int stageIdx, stageCount;
    bool stageLocked, stageCpu;
    string stageLabel = "";
    static readonly string[] ModeLabels = { "1P  VS  2P", "1P  VS  CPU" };
    static readonly string[] ModeDescs = { "KEYBOARD OR GAMEPAD", "FIGHT THE COMPUTER" };

    #endregion

    #region Durum

    float target1 = 1f, target2 = 1f, chipv1 = 1f, chipv2 = 1f;
    float chipDelay1, chipDelay2;
    float centerHideAt = -1f, comboHide1, comboHide2, flashA;
    float titleShownAt;
    int pauseSelection;
    Color flashColor = Color.white;

    static readonly Color PipOff = new Color(0.25f, 0.25f, 0.3f);
    static readonly Color PipOn = new Color(1f, 0.85f, 0.2f);
    static readonly Color OptionOff = new Color(0.55f, 0.55f, 0.6f);
    static readonly Color Cursor1 = new Color(1f, 0.62f, 0.12f);
    static readonly Color Cursor2 = new Color(0.25f, 0.9f, 1f);
    static readonly Color CellIdle = new Color(0.22f, 0.2f, 0.3f);

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
        hint.text = "<color=#FFA020>1P</color> WASD + F G H      <color=#40E0FF>2P</color> ARROWS + K L ;      ESC PAUSE";
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
        HideKO();
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

    public void Flash() { flashColor = Color.white; flashA = 0.85f; }

    public void Flash(Color c, float alpha)
    {
        flashColor = c;
        flashA = Mathf.Max(flashA, alpha);
    }

    public void ShowTitle(bool on)
    {
        HideKO();
        titleGroup.SetActive(on);
        fightGroup.SetActive(!on);
        if (on)
        {
            if (padsGroup != null) padsGroup.SetActive(false);
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

    public void EnsurePauseOption(string label)
    {
        if (pauseLabels == null || pauseOptions == null || pauseOptions.Length == 0 || pauseGroup == null) return;
        foreach (var l in pauseLabels) if (l == label) return;

        var last = pauseOptions[pauseOptions.Length - 1];
        var copy = Instantiate(last.gameObject, last.transform.parent);
        copy.name = "Option" + pauseOptions.Length;
        var rt = (RectTransform)copy.transform;
        rt.offsetMin = last.rectTransform.offsetMin + new Vector2(0f, -20f);
        rt.offsetMax = last.rectTransform.offsetMax + new Vector2(0f, -20f);
        var text = copy.GetComponent<Text>();
        text.text = label;

        var root = pauseGroup.transform;
        foreach (var n in new[] { "Frame", "Panel" })
        {
            var r = root.Find(n) as RectTransform;
            if (r != null) r.offsetMin += new Vector2(0f, -22f);
        }
        var hint = root.Find("PauseHint") as RectTransform;
        if (hint != null)
        {
            hint.offsetMin += new Vector2(0f, -22f);
            hint.offsetMax += new Vector2(0f, -22f);
        }

        var labels = new string[pauseLabels.Length + 1];
        pauseLabels.CopyTo(labels, 0);
        labels[labels.Length - 1] = label;
        pauseLabels = labels;
        var opts = new Text[pauseOptions.Length + 1];
        pauseOptions.CopyTo(opts, 0);
        opts[opts.Length - 1] = text;
        pauseOptions = opts;
    }

    public void SetPauseSelection(int index)
    {
        pauseSelection = index;
        RefreshPauseOptions(true);
    }

    #endregion

    #region Ekstralar: oyuncu renkleri ve kombo sayacı

    /// <summary>Seçim ekranını ve kombo sayacını kurar. Birden fazla çağrılabilir.</summary>
    public void EnsureExtras(FighterPalette.Entry[] pal, int comboPips, string key1, string key2)
    {
        if (!string.IsNullOrEmpty(key1)) comboKey1 = kbKey1 = key1;
        if (!string.IsNullOrEmpty(key2)) comboKey2 = kbKey2 = key2;
        if (extrasBuilt) return;
        extrasBuilt = true;
        palette = pal;
        if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        var canvasRoot = GetComponent<RectTransform>();
        var fightRoot = (RectTransform)fightGroup.transform;

        name1 = FindText(fightRoot, "Name1");
        name2 = FindText(fightRoot, "Name2");
        fillImg1 = fill1 != null ? fill1.GetComponent<Image>() : null;
        fillImg2 = fill2 != null ? fill2.GetComponent<Image>() : null;

        BuildComboMeter(fightRoot, true, Mathf.Max(1, comboPips), out comboLabel1, out comboPips1, out comboTimer1);
        BuildComboMeter(fightRoot, false, Mathf.Max(1, comboPips), out comboLabel2, out comboPips2, out comboTimer2);

        var hint = titleGroup != null ? FindText((RectTransform)titleGroup.transform, "Controls") : null;
        var credit = titleGroup != null ? FindText((RectTransform)titleGroup.transform, "Credit") : null;
        if (hint != null)
        {
            // Kontrol yazısı: ekranın en altına, daha büyük ve net
            hint.text = "<color=#FFA020>1P</color> WASD + F G " + comboKey1 + "    <color=#40E0FF>2P</color> ARROWS + K L " + comboKey2 + "    ESC PAUSE";
            hint.color = Color.white;
            hint.fontSize = 10;
            var hr = hint.rectTransform;
            hr.offsetMin = new Vector2(0, 13); hr.offsetMax = new Vector2(0, 27);
            hint.GetComponent<Outline>().effectDistance = new Vector2(1f, -1f);

            if (credit != null)
            {
                credit.fontSize = 8;
                var cr = credit.rectTransform;
                cr.offsetMin = new Vector2(0, 2); cr.offsetMax = new Vector2(0, 12);
            }

            // Arka plana karışmasın: altına neredeyse opak koyu şerit
            var titleRoot = (RectTransform)titleGroup.transform;
            var band = Rect("ControlsBand", titleRoot, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 0), new Vector2(0, 30));
            Img(band, new Color(0.01f, 0.01f, 0.05f, 0.92f));
            Img(Rect("BandLine", band, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -1), Vector2.zero), new Color(1f, 0.85f, 0.2f, 0.9f));
            band.SetSiblingIndex(hint.transform.GetSiblingIndex());
            controlsHint = hint;
            BuildPadGuide(titleRoot);
        }

        BuildSelect(canvasRoot);
        if (titleGroup != null) selectGroup.transform.SetSiblingIndex(titleGroup.transform.GetSiblingIndex() + 1);
        BuildMode(canvasRoot);
        modeGroup.transform.SetSiblingIndex(selectGroup.transform.GetSiblingIndex() + 1);
        BuildStage(canvasRoot);
        stageGroup.transform.SetSiblingIndex(modeGroup.transform.GetSiblingIndex() + 1);
        BuildPads(canvasRoot);
        padsGroup.transform.SetSiblingIndex(stageGroup.transform.GetSiblingIndex() + 1);
        BuildKO(canvasRoot);

        // Bitirici adı: ekranın üstünde, sağlık barlarının altında
        finisherText = Txt(fightRoot, "FinisherName", new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -74), new Vector2(0, -54), 15, TextAnchor.MiddleCenter, Color.white);
        finisherText.fontStyle = FontStyle.BoldAndItalic;
        finisherText.GetComponent<Outline>().effectDistance = new Vector2(1.5f, -1.5f);
        finisherText.enabled = false;
        flash.transform.SetAsLastSibling();
    }

    public void SetPlayers(string n1, string n2, Color c1, Color c2)
    {
        if (name1 != null) { name1.text = n1; name1.color = c1; }
        if (name2 != null) { name2.text = n2; name2.color = c2; }
        if (fillImg1 != null) fillImg1.color = Color.Lerp(c1, Color.white, 0.15f);
        if (fillImg2 != null) fillImg2.color = Color.Lerp(c2, Color.white, 0.15f);
    }

    public void SetComboMeter(int side, int streak, bool ready, float readyFraction, Color c)
    {
        if (!extrasBuilt) return;
        var label = side == 0 ? comboLabel1 : comboLabel2;
        var pips = side == 0 ? comboPips1 : comboPips2;
        var timer = side == 0 ? comboTimer1 : comboTimer2;
        string key = side == 0 ? comboKey1 : comboKey2;

        if (ready)
        {
            bool blink = Mathf.Repeat(Time.unscaledTime, 0.4f) < 0.25f;
            label.text = "COMBO READY! [" + key + "]";
            label.color = blink ? Color.white : Color.Lerp(c, Color.white, 0.35f);
            foreach (var p in pips) p.enabled = false;
            timer.gameObject.SetActive(true);
            float v = Mathf.Clamp01(readyFraction);
            if (side == 0) { timer.anchorMin = new Vector2(0f, 0f); timer.anchorMax = new Vector2(v, 1f); }
            else { timer.anchorMin = new Vector2(1f - v, 0f); timer.anchorMax = new Vector2(1f, 1f); }
            timer.offsetMin = timer.offsetMax = Vector2.zero;
            timer.GetComponent<Image>().color = Color.Lerp(c, Color.white, 0.3f);
        }
        else
        {
            label.text = "COMBO";
            label.color = new Color(0.75f, 0.75f, 0.8f);
            for (int i = 0; i < pips.Length; i++)
            {
                pips[i].enabled = true;
                pips[i].color = i < streak ? Color.Lerp(c, Color.white, 0.25f) : PipOff;
            }
            timer.gameObject.SetActive(false);
        }
    }

    void BuildComboMeter(RectTransform root, bool left, int count, out Text label, out Image[] pips, out RectTransform timer)
    {
        Vector2 a = left ? new Vector2(0, 1) : new Vector2(1, 1);
        label = Txt(root, left ? "ComboLabel1" : "ComboLabel2", a, a,
            left ? new Vector2(8, -47) : new Vector2(-140, -47),
            left ? new Vector2(140, -38) : new Vector2(-8, -38),
            8, left ? TextAnchor.MiddleLeft : TextAnchor.MiddleRight, new Color(0.75f, 0.75f, 0.8f));
        label.text = "COMBO";

        pips = new Image[count];
        for (int i = 0; i < count; i++)
        {
            pips[i] = Img(Rect(left ? "ComboPip1" : "ComboPip2", root, a, a,
                left ? new Vector2(44 + i * 11, -45) : new Vector2(-53 - i * 11, -45),
                left ? new Vector2(53 + i * 11, -40) : new Vector2(-44 - i * 11, -40)), PipOff);
        }

        var track = Rect(left ? "ComboTimer1" : "ComboTimer2", root, a, a,
            left ? new Vector2(8, -51) : new Vector2(-100, -51),
            left ? new Vector2(100, -49) : new Vector2(-8, -49));
        timer = Rect("Fill", track, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        Img(timer, Color.white);
        timer.gameObject.SetActive(false);
    }

    static Text FindText(RectTransform root, string name)
    {
        foreach (var t in root.GetComponentsInChildren<Text>(true))
            if (t.name == name) return t;
        return null;
    }

    #endregion

    #region Karakter seçim ekranı

    void BuildSelect(RectTransform parent)
    {
        var root = Rect("SelectUI", parent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        selectGroup = root.gameObject;

        var head = Txt(root, "SelectTitle", new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -27), new Vector2(0, -5), 15, TextAnchor.MiddleCenter, new Color(1f, 0.85f, 0.2f));
        head.text = "SELECT YOUR COLOR";

        int n = palette.Length, cols = FighterPalette.Columns, rows = FighterPalette.Rows;
        const int cell = 30, gap = 7, top = -34;
        int gridW = cols * cell + (cols - 1) * gap;
        int gridH = rows * cell + (rows - 1) * gap;
        float x0 = -gridW * 0.5f;

        var top2 = new Vector2(0.5f, 1f);
        Img(Rect("GridPanel", root, top2, top2, new Vector2(x0 - 8, top - gridH - 30), new Vector2(-x0 + 8, top + 5)), new Color(0.03f, 0.01f, 0.08f, 0.72f));

        cellFrames = new Image[n];
        cellBGs = new Image[n];
        cellTag1 = new Text[n];
        cellTag2 = new Text[n];
        Color skin = new Color(0.96f, 0.78f, 0.62f);
        Color dark = new Color(0.08f, 0.08f, 0.1f);
        for (int i = 0; i < n; i++)
        {
            int col = i % cols, row = i / cols;
            float cx = x0 + col * (cell + gap);
            float cy = top - row * (cell + gap);
            var frame = Rect("Cell" + i, root, top2, top2, new Vector2(cx - 2, cy - cell - 2), new Vector2(cx + cell + 2, cy + 2));
            cellFrames[i] = Img(frame, CellIdle);
            var bg = Rect("BG", frame, Vector2.zero, Vector2.one, new Vector2(2, 2), new Vector2(-2, -2));
            cellBGs[i] = Img(bg, new Color(0.08f, 0.05f, 0.14f));

            // Mini portre: omuzlar, yüz, bandana, saç, gözler
            Color c = palette[i].color;
            var z = Vector2.zero;
            Img(Rect("Shoulders", bg, z, z, new Vector2(3, 0), new Vector2(23, 7)), c);
            Img(Rect("Face", bg, z, z, new Vector2(8, 7), new Vector2(18, 20)), skin);
            Img(Rect("Hair", bg, z, z, new Vector2(8, 20), new Vector2(18, 23)), dark);
            Img(Rect("Band", bg, z, z, new Vector2(7, 16), new Vector2(19, 19)), c);
            Img(Rect("Tail", bg, z, z, new Vector2(19, 15), new Vector2(23, 18)), c);
            Img(Rect("EyeL", bg, z, z, new Vector2(10, 12), new Vector2(12, 14)), dark);
            Img(Rect("EyeR", bg, z, z, new Vector2(14, 12), new Vector2(16, 14)), dark);

            cellTag1[i] = Txt(frame, "Tag1", new Vector2(0, 1), new Vector2(0, 1), new Vector2(-4, -6), new Vector2(14, 6), 8, TextAnchor.MiddleLeft, Cursor1);
            cellTag1[i].text = "1P";
            cellTag2[i] = Txt(frame, "Tag2", new Vector2(1, 0), new Vector2(1, 0), new Vector2(-14, -6), new Vector2(4, 6), 8, TextAnchor.MiddleRight, Cursor2);
            cellTag2[i].text = "2P";
        }

        var rule = selRule = Txt(root, "ComboRule", top2, top2, new Vector2(-120, top - gridH - 26), new Vector2(120, top - gridH - 6), 7, TextAnchor.MiddleCenter, new Color(0.85f, 0.85f, 0.9f));
        rule.text = "LAND 3 HITS IN A ROW = COMBO READY\nTHEN PRESS  1P: " + comboKey1 + "   2P: " + comboKey2;

        selName1 = Txt(root, "SelName1", Vector2.zero, Vector2.zero, new Vector2(8, 20), new Vector2(150, 38), 15, TextAnchor.MiddleLeft, Color.white);
        selStatus1 = Txt(root, "SelStatus1", Vector2.zero, Vector2.zero, new Vector2(8, 6), new Vector2(150, 18), 8, TextAnchor.MiddleLeft, Color.white);
        selName2 = Txt(root, "SelName2", new Vector2(1, 0), new Vector2(1, 0), new Vector2(-150, 20), new Vector2(-8, 38), 15, TextAnchor.MiddleRight, Color.white);
        selStatus2 = Txt(root, "SelStatus2", new Vector2(1, 0), new Vector2(1, 0), new Vector2(-150, 6), new Vector2(-8, 18), 8, TextAnchor.MiddleRight, Color.white);

        selectGroup.SetActive(false);
    }

    #region K.O. animasyonu

    void BuildKO(RectTransform parent)
    {
        var mid = new Vector2(0f, 0.5f);
        var mid2 = new Vector2(1f, 0.5f);
        koBand = Rect("KOBand", parent, mid, mid2, new Vector2(0, -1), new Vector2(0, 1));
        koBandImg = Img(koBand, new Color(0f, 0f, 0f, 0.7f));
        Img(Rect("KOBandTop", koBand, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -2), Vector2.zero), new Color(1f, 0.2f, 0.15f, 0.9f));
        Img(Rect("KOBandBottom", koBand, new Vector2(0, 0), new Vector2(1, 0), Vector2.zero, new Vector2(0, 2)), new Color(1f, 0.2f, 0.15f, 0.9f));

        koText = Txt(parent, "KOText", mid, mid2, new Vector2(0, -40), new Vector2(0, 40), 46, TextAnchor.MiddleCenter, new Color(1f, 0.2f, 0.15f));
        koText.text = "K.O.";
        koText.fontStyle = FontStyle.BoldAndItalic;
        var o = koText.GetComponent<Outline>();
        o.effectColor = new Color(0.15f, 0f, 0f, 1f);
        o.effectDistance = new Vector2(2.5f, -2.5f);
        var shadow = koText.gameObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(1f, 0.85f, 0.2f, 0.9f);
        shadow.effectDistance = new Vector2(-1.5f, 1.5f);

        flash.transform.SetAsLastSibling();
        HideKO();
    }

    /// <summary>K.O. yazısı ekrana çarparak gelir, sallanır, kırmızı-sarı yanıp söner.</summary>
    public void ShowKO()
    {
        if (koText == null) { ShowCenter("K.O.", new Color(1f, 0.2f, 0.15f), 0f); return; }
        centerText.enabled = false;
        koStart = Time.unscaledTime;
        koText.enabled = true;
        koBand.gameObject.SetActive(true);
        Flash(Color.white, 0.9f);
        AnimateKO();
    }

    /// <summary>Bitiricinin adı: oyuncu renginde, büyüyerek gelir, 1.8 sn kalır.</summary>
    public void ShowFinisherName(string name, Color c)
    {
        if (finisherText == null) return;
        finisherText.text = name;
        finisherText.color = Color.Lerp(c, Color.white, 0.25f);
        finisherText.enabled = true;
        finisherText.rectTransform.localScale = Vector3.one * 2.2f;
        finisherShownAt = Time.unscaledTime;
    }

    public void HideKO()
    {
        koStart = -1f;
        if (koText != null) koText.enabled = false;
        if (koBand != null) koBand.gameObject.SetActive(false);
    }

    void AnimateKO()
    {
        float t = Time.unscaledTime - koStart;
        const float slam = 0.16f;
        var r = koText.rectTransform;

        float scale, rot = 0f;
        Vector2 shake = Vector2.zero;
        if (t < slam)
        {
            float k = t / slam;
            scale = Mathf.Lerp(4f, 0.82f, k * k);          // uzaktan ekrana çarpar
            rot = Mathf.Lerp(-18f, 0f, k);
        }
        else
        {
            float u = t - slam;
            scale = 1f - 0.18f * Mathf.Exp(-7f * u) * Mathf.Cos(26f * u);   // yaylanarak oturur
            float shakeAmt = Mathf.Clamp01(1f - u / 0.4f) * 3f;
            shake = new Vector2(Random.Range(-shakeAmt, shakeAmt), Random.Range(-shakeAmt, shakeAmt));
        }
        r.localScale = Vector3.one * scale;
        r.localRotation = Quaternion.Euler(0f, 0f, rot);
        r.anchoredPosition = new Vector2(Mathf.Round(shake.x), Mathf.Round(shake.y));

        bool flicker = t < 0.9f && Mathf.Repeat(t, 0.12f) < 0.06f;
        koText.color = flicker ? new Color(1f, 0.85f, 0.2f) : new Color(1f, 0.2f, 0.15f);

        float h = Mathf.Lerp(1f, 30f, Mathf.Clamp01((t - 0.05f) / 0.18f));
        koBand.offsetMin = new Vector2(0, -h);
        koBand.offsetMax = new Vector2(0, h);
    }

    #endregion

    #region Mod seçimi

    void BuildMode(RectTransform parent)
    {
        var root = Rect("ModeUI", parent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        modeGroup = root.gameObject;
        var mid = new Vector2(0.5f, 0.5f);

        Img(Rect("Dim", root, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero), new Color(0f, 0f, 0.03f, 0.55f));
        Img(Rect("Frame", root, mid, mid, new Vector2(-96, -58), new Vector2(96, 64)), new Color(1f, 0.85f, 0.2f, 0.9f));
        Img(Rect("Panel", root, mid, mid, new Vector2(-94, -56), new Vector2(94, 62)), new Color(0.05f, 0.02f, 0.11f, 0.97f));

        var title = Txt(root, "ModeTitle", mid, mid, new Vector2(-94, 34), new Vector2(94, 60), 18, TextAnchor.MiddleCenter, new Color(1f, 0.85f, 0.2f));
        title.text = "SELECT MODE";

        modeOptions = new Text[ModeLabels.Length];
        for (int i = 0; i < ModeLabels.Length; i++)
        {
            float y = 14 - i * 24;
            modeOptions[i] = Txt(root, "Mode" + i, mid, mid, new Vector2(-94, y - 10), new Vector2(94, y + 10), 15, TextAnchor.MiddleCenter, OptionOff);
        }
        modeDesc = Txt(root, "ModeDesc", mid, mid, new Vector2(-94, -38), new Vector2(94, -26), 8, TextAnchor.MiddleCenter, new Color(0.75f, 0.85f, 1f));
        var hint = Txt(root, "ModeHint", mid, mid, new Vector2(-94, -54), new Vector2(94, -42), 8, TextAnchor.MiddleCenter, new Color(0.7f, 0.7f, 0.75f));
        hint.text = "UP/DOWN  -  PUNCH SELECT  -  ESC BACK";

        modeGroup.SetActive(false);
    }

    #region Sahne seçimi

    void BuildStage(RectTransform parent)
    {
        var root = Rect("StageUI", parent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        stageGroup = root.gameObject;

        // Üstte başlık şeridi; sahne arkada canlı görünsün diye ortası boş
        Img(Rect("TopBand", root, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -28), Vector2.zero), new Color(0.02f, 0.01f, 0.06f, 0.8f));
        Img(Rect("TopLine", root, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -29), new Vector2(0, -28)), new Color(1f, 0.85f, 0.2f, 0.9f));
        stageTitle = Txt(root, "StageTitle", new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -27), new Vector2(0, -3), 15, TextAnchor.MiddleCenter, new Color(1f, 0.85f, 0.2f));
        stageTitle.text = "SELECT STAGE";

        var bot = new Vector2(0.5f, 0f);
        Img(Rect("StageFrame", root, bot, bot, new Vector2(-118, 6), new Vector2(118, 70)), new Color(1f, 0.85f, 0.2f, 0.9f));
        Img(Rect("StagePanel", root, bot, bot, new Vector2(-116, 8), new Vector2(116, 68)), new Color(0.04f, 0.02f, 0.1f, 0.94f));
        stageName = Txt(root, "StageName", bot, bot, new Vector2(-116, 46), new Vector2(116, 66), 16, TextAnchor.MiddleCenter, Color.white);
        stageDesc = Txt(root, "StageDesc", bot, bot, new Vector2(-116, 35), new Vector2(116, 46), 8, TextAnchor.MiddleCenter, new Color(0.75f, 0.85f, 1f));
        stagePipRow = Rect("StagePips", root, bot, bot, new Vector2(-60, 25), new Vector2(60, 31));
        stageHint = Txt(root, "StageHint", bot, bot, new Vector2(-116, 10), new Vector2(116, 22), 7, TextAnchor.MiddleCenter, new Color(0.75f, 0.75f, 0.8f));
        stageGroup.SetActive(false);
    }

    public void ShowStage(bool on)
    {
        if (stageGroup == null) return;
        stageGroup.SetActive(on);
        if (on)
        {
            if (padsGroup != null) padsGroup.SetActive(false);
            titleGroup.SetActive(false);
            fightGroup.SetActive(false);
            if (selectGroup != null) selectGroup.SetActive(false);
            if (modeGroup != null) modeGroup.SetActive(false);
            centerText.enabled = false;
            RefreshStage();
        }
    }

    public void SetStage(int index, int count, string label, string desc, bool locked, bool cpu)
    {
        stageIdx = index;
        stageLabel = label;
        stageLocked = locked;
        stageCpu = cpu;
        if (stageDesc != null) stageDesc.text = desc;
        if (stagePipRow != null && (stagePips == null || stageCount != count))
        {
            for (int i = stagePipRow.childCount - 1; i >= 0; i--) Destroy(stagePipRow.GetChild(i).gameObject);
            stageCount = count;
            stagePips = new Image[count];
            const float w = 14f, gap = 6f;
            float x0 = -(count * w + (count - 1) * gap) * 0.5f;
            var mid = new Vector2(0.5f, 0.5f);
            for (int i = 0; i < count; i++)
            {
                float x = x0 + i * (w + gap);
                stagePips[i] = Img(Rect("Pip" + i, stagePipRow, mid, mid, new Vector2(x, -3), new Vector2(x + w, 3)), PipOff);
            }
        }
        if (stageGroup != null && stageGroup.activeSelf) RefreshStage();
    }

    void RefreshStage()
    {
        bool blink = Mathf.Repeat(Time.unscaledTime, 0.5f) < 0.35f;
        if (stageName != null)
        {
            stageName.text = stageLocked ? stageLabel : (blink ? "<  " + stageLabel + "  >" : "   " + stageLabel + "   ");
            stageName.color = stageLocked ? PipOn : Color.white;
        }
        if (stagePips != null)
            for (int i = 0; i < stagePips.Length; i++)
                if (stagePips[i] != null) stagePips[i].color = i == stageIdx ? PipOn : PipOff;
        if (stageHint != null)
            stageHint.text = stageLocked ? "GET READY!" :
                (stageCpu ? "1P: LEFT/RIGHT CHOOSE   PUNCH FIGHT!   KICK BACK" : "LEFT/RIGHT CHOOSE   PUNCH FIGHT!   KICK BACK");
    }

    #endregion

    #region Kol tuş anlatımı

    const string Sq = "<color=#FF80C8>\u25A1</color>", Cr = "<color=#70A8FF>X</color>", Ci = "<color=#FF6060>\u25CB</color>", Tr = "<color=#50F0A0>\u25B2</color>";

    /// <summary>Başlangıç ekranında, kolla oynayanlar için sade tuş şeridi: [□] PUNCH  [X] KICK  [▲] COMBO  [≡] PAUSE</summary>
    void BuildPadGuide(RectTransform titleRoot)
    {
        var bot = new Vector2(0.5f, 0f);
        var g = Rect("PadGuide", titleRoot, bot, bot, new Vector2(-122, 37), new Vector2(122, 57));
        padGuide = g.gameObject;
        Img(Rect("Panel", g, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero), new Color(0.01f, 0.01f, 0.05f, 0.85f));
        Img(Rect("Line", g, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -1), Vector2.zero), new Color(0.3f, 0.9f, 1f, 0.8f));

        // Solda kimin kolu olduğu (1P / 2P / 1P+2P)
        var tagBG = Rect("TagBG", g, new Vector2(0, 0), new Vector2(0, 1), new Vector2(0, 0), new Vector2(30, -1));
        Img(tagBG, new Color(0.3f, 0.9f, 1f, 0.9f));
        padGuideHead = Txt(tagBG, "Tag", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, 8, TextAnchor.MiddleCenter, new Color(0.02f, 0.02f, 0.08f));
        padGuideHead.GetComponent<Outline>().enabled = false;

        string[] glyph = { "\u25A1", "X", "\u25B2", "\u2261" };
        Color[] col = { new Color(1f, 0.5f, 0.78f), new Color(0.45f, 0.66f, 1f), new Color(0.32f, 0.94f, 0.62f), new Color(0.85f, 0.85f, 0.9f) };
        string[] label = { "PUNCH", "KICK", "COMBO", "PAUSE" };
        const float x0 = 36f, step = 52f;
        var left = new Vector2(0, 0.5f);
        for (int i = 0; i < glyph.Length; i++)
        {
            float x = x0 + i * step;
            // Yuvarlak tuş hissi: renkli çerçeve + koyu iç + sembol
            Img(Rect("Btn" + i, g, left, left, new Vector2(x, -7), new Vector2(x + 14, 7)), col[i]);
            Img(Rect("BtnIn" + i, g, left, left, new Vector2(x + 1, -6), new Vector2(x + 13, 6)), new Color(0.06f, 0.06f, 0.1f));
            var t = Txt(g, "Glyph" + i, left, left, new Vector2(x, -7), new Vector2(x + 14, 7), 9, TextAnchor.MiddleCenter, col[i]);
            t.text = glyph[i];
            t.GetComponent<Outline>().enabled = false;
            Txt(g, "Label" + i, left, left, new Vector2(x + 17, -6), new Vector2(x + 50, 6), 8, TextAnchor.MiddleLeft, Color.white).text = label[i];
        }
        padGuide.SetActive(false);
    }

    /// <summary>pad1/pad2: o oyuncunun kolu var mı. Başlangıç yazısı, kol rehberi, seçim ekranı ve kombo göstergesi buna göre değişir.</summary>
    public void SetControlHints(bool pad1, bool pad2, string key1, string key2, bool anyPad = false)
    {
        if (pressStart != null) pressStart.text = anyPad ? "PRESS PUNCH OR X TO START" : "PRESS PUNCH TO START";
        if (!string.IsNullOrEmpty(key1)) kbKey1 = key1;
        if (!string.IsNullOrEmpty(key2) && key2 != "CPU") kbKey2 = key2;
        bool cpu = key2 == "CPU";
        string state = pad1 + "|" + pad2 + "|" + cpu + "|" + kbKey1 + "|" + kbKey2;
        if (state == hintState) return;
        hintState = state;

        comboKey1 = pad1 ? Tr : kbKey1;
        comboKey2 = cpu ? "CPU" : (pad2 ? Tr : kbKey2);

        if (controlsHint != null)
        {
            controlsHint.supportRichText = true;
            string a = "<color=#FFA020>1P</color> " + (pad1 ? "GAMEPAD" : "WASD + F G " + kbKey1);
            string b = "<color=#40E0FF>2P</color> " + (pad2 ? "GAMEPAD" : "ARROWS + K L " + kbKey2);
            controlsHint.text = a + "      " + b + "      ESC PAUSE";
        }
        if (padGuide != null)
        {
            padGuide.SetActive(pad1 || pad2);
            padGuideHead.text = pad1 && pad2 ? "1P 2P" : pad1 ? "1P" : "2P";
        }
        if (selRule != null)
        {
            selRule.supportRichText = true;
            selRule.text = "LAND 3 HITS IN A ROW = COMBO READY\nTHEN PRESS  1P: " + (pad1 ? Tr : kbKey1) + "   " + (cpu ? "" : "2P: " + (pad2 ? Tr : kbKey2));
        }
    }

    #endregion

    #region Kol eşleştirme ekranı (FIFA tarzı)

    void BuildPads(RectTransform parent)
    {
        var root = Rect("PadsUI", parent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        padsGroup = root.gameObject;
        var mid = new Vector2(0.5f, 0.5f);
        Img(Rect("Dim", root, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero), new Color(0f, 0f, 0.03f, 0.72f));

        var title = Txt(root, "PadsTitle", mid, mid, new Vector2(-140, 68), new Vector2(140, 88), 15, TextAnchor.MiddleCenter, new Color(1f, 0.85f, 0.2f));
        title.text = "CONTROLLERS";
        padsModeLine = Txt(root, "PadsMode", mid, mid, new Vector2(-140, 59), new Vector2(140, 68), 7, TextAnchor.MiddleCenter, new Color(0.75f, 0.85f, 1f));

        // Üç sütun: 1P  |  boşta  |  2P (CPU modunda "CPU")
        float[] cx = { -94f, 0f, 94f };
        Color[] frame = { Cursor1, new Color(0.45f, 0.45f, 0.55f), Cursor2 };
        Color[] fill = { new Color(0.16f, 0.08f, 0.02f, 0.95f), new Color(0.05f, 0.04f, 0.1f, 0.95f), new Color(0.02f, 0.1f, 0.14f, 0.95f) };
        for (int i = 0; i < 3; i++)
        {
            float hw = i == 1 ? 42f : 44f;
            Img(Rect("ColFrame" + i, root, mid, mid, new Vector2(cx[i] - hw - 1, -59), new Vector2(cx[i] + hw + 1, 57)), frame[i]);
            Img(Rect("Col" + i, root, mid, mid, new Vector2(cx[i] - hw, -58), new Vector2(cx[i] + hw, 56)), fill[i]);
            Img(Rect("ColHeadBG" + i, root, mid, mid, new Vector2(cx[i] - hw, 40), new Vector2(cx[i] + hw, 56)), new Color(frame[i].r, frame[i].g, frame[i].b, 0.25f));
        }
        Txt(root, "Head1", mid, mid, new Vector2(-138, 40), new Vector2(-50, 56), 13, TextAnchor.MiddleCenter, Cursor1).text = "1P";
        Txt(root, "Head0", mid, mid, new Vector2(-42, 40), new Vector2(42, 56), 7, TextAnchor.MiddleCenter, new Color(0.6f, 0.6f, 0.7f)).text = "NOT PLAYING";
        padsHead2 = Txt(root, "Head2", mid, mid, new Vector2(50, 40), new Vector2(138, 56), 13, TextAnchor.MiddleCenter, Cursor2);

        padsColumns = Rect("PadIcons", root, mid, mid, Vector2.zero, Vector2.zero);
        padIcons = new RectTransform[MaxPadIcons];
        padTint = new Image[MaxPadIcons][];
        padLabels = new Text[MaxPadIcons];
        padX = new float[MaxPadIcons];
        padY = new float[MaxPadIcons];
        // Klavye ikonları: 0 = 1P sütunu, 1 = orta (kullanılmıyor), 2 = 2P sütunu
        kbIcons = new RectTransform[3];
        kbLabels = new Text[3];
        kbSubs = new Text[3];
        for (int k = 0; k < 3; k++)
        {
            kbIcons[k] = MakeKeyboardIcon(padsColumns, k, out kbLabels[k], out kbSubs[k]);
            kbIcons[k].anchoredPosition = new Vector2((k - 1) * 94f, 22f);
            kbIcons[k].gameObject.SetActive(false);
        }
        for (int i = 0; i < MaxPadIcons; i++)
        {
            padIcons[i] = MakePadIcon(padsColumns, i, out padTint[i], out padLabels[i]);
            padIcons[i].anchoredPosition = new Vector2(0f, 22f - i * 30f);
            padIcons[i].gameObject.SetActive(false);
        }
        padsEmpty = Txt(root, "PadsEmpty", mid, mid, new Vector2(-42, -20), new Vector2(42, 20), 7, TextAnchor.MiddleCenter, new Color(0.7f, 0.7f, 0.78f));
        padsEmpty.text = "CONNECT\nA GAMEPAD";

        padsKey1 = Txt(root, "Key1", mid, mid, new Vector2(-140, -74), new Vector2(140, -63), 7, TextAnchor.MiddleCenter, new Color(0.75f, 0.8f, 0.9f));
        padsKey2 = Txt(root, "Key2", mid, mid, new Vector2(50, -6), new Vector2(138, 6), 9, TextAnchor.MiddleCenter, new Color(0.75f, 0.75f, 0.8f));
        var hint = Txt(root, "PadsHint", mid, mid, new Vector2(-140, -90), new Vector2(140, -78), 7, TextAnchor.MiddleCenter, Color.white);
        hint.text = "PAD  < >  MOVE      X / PUNCH  START      O / KICK  BACK";
        padsGroup.SetActive(false);
    }

    /// <summary>Basit gamepad ikonu: gövde, iki tutamak, yön tuşu, analoglar, dört tuş, altında etiket.</summary>
    RectTransform MakePadIcon(RectTransform parent, int index, out Image[] tint, out Text label)
    {
        var mid = new Vector2(0.5f, 0.5f);
        var r = Rect("Pad" + index, parent, mid, mid, new Vector2(-20f, -14f), new Vector2(20f, 14f));
        Color c = new Color(0.8f, 0.8f, 0.85f), d = new Color(0.08f, 0.08f, 0.12f);
        Image V(string n, float x0, float y0, float x1, float y1, Color col) => Img(Rect(n, r, mid, mid, new Vector2(x0, y0), new Vector2(x1, y1)), col);
        var body = V("Body", -14, -3, 14, 8, c);
        var gl = V("GripL", -17, -10, -8, 5, c);
        var gr = V("GripR", 8, -10, 17, 5, c);
        V("DpadH", -14, 1, -8, 3, d);
        V("DpadV", -12, -1, -10, 5, d);
        V("BtnT", 9, 4, 11, 6, d);
        V("BtnL", 7, 1, 9, 3, d);
        V("BtnR", 11, 1, 13, 3, d);
        V("BtnB", 9, -2, 11, 0, d);
        V("StickL", -6, -3, -2, 1, d);
        V("StickR", 2, -3, 6, 1, d);
        V("Light", -4, 6, 4, 7, new Color(0.3f, 0.6f, 1f));
        tint = new[] { body, gl, gr };
        label = Txt(r, "Label", mid, mid, new Vector2(-24f, -21f), new Vector2(24f, -11f), 7, TextAnchor.MiddleCenter, Color.white);
        return r;
    }

    public void ShowPads(bool on)
    {
        if (padsGroup == null) return;
        padsGroup.SetActive(on);
        if (on)
        {
            titleGroup.SetActive(false);
            fightGroup.SetActive(false);
            if (selectGroup != null) selectGroup.SetActive(false);
            if (modeGroup != null) modeGroup.SetActive(false);
            if (stageGroup != null) stageGroup.SetActive(false);
            centerText.enabled = false;
            for (int i = 0; i < MaxPadIcons; i++) padX[i] = float.NaN;   // ilk karede yerine otursun
            RefreshPads();
        }
    }

    /// <summary>slots: her kolun yeri (0 = 1P, 1 = 2P, -1 boşta). hot: o kolda şu an tuşa basılıyor mu (hangi kol hangisi anlaşılsın).</summary>
    public void SetPads(int[] slots, bool[] hot, bool cpu)
    {
        padSlot = slots ?? new int[0];
        padHot = hot ?? new bool[0];
        padsCpu = cpu;
    }

    /// <summary>Klavye ikonu: çerçeve, üç sıra tuş, boşluk tuşu; altında etiket ve tuş listesi.</summary>
    RectTransform MakeKeyboardIcon(RectTransform parent, int index, out Text label, out Text sub)
    {
        var mid = new Vector2(0.5f, 0.5f);
        var r = Rect("Keyboard" + index, parent, mid, mid, new Vector2(-22f, -14f), new Vector2(22f, 14f));
        Color frame = new Color(0.8f, 0.8f, 0.85f), body = new Color(0.12f, 0.12f, 0.16f), key = new Color(0.85f, 0.85f, 0.9f);
        Img(Rect("Frame", r, mid, mid, new Vector2(-20, -6), new Vector2(20, 10)), frame);
        Img(Rect("Body", r, mid, mid, new Vector2(-19, -5), new Vector2(19, 9)), body);
        for (int row = 0; row < 3; row++)
        {
            int keys = row == 0 ? 9 : row == 1 ? 8 : 7;
            float w = 3f, gap = 1f, total = keys * w + (keys - 1) * gap, x0 = -total * 0.5f + row * 0.5f, y = 6f - row * 3.5f;
            for (int k = 0; k < keys; k++)
                Img(Rect("K", r, mid, mid, new Vector2(x0 + k * (w + gap), y - 1.5f), new Vector2(x0 + k * (w + gap) + w, y + 1f)), key);
        }
        Img(Rect("Space", r, mid, mid, new Vector2(-8, -3.5f), new Vector2(8, -1.5f)), key);
        label = Txt(r, "Label", mid, mid, new Vector2(-30f, -16f), new Vector2(30f, -7f), 7, TextAnchor.MiddleCenter, Color.white);
        sub = Txt(r, "Sub", mid, mid, new Vector2(-40f, -24f), new Vector2(40f, -16f), 6, TextAnchor.MiddleCenter, new Color(0.75f, 0.75f, 0.82f));
        return r;
    }

    void RefreshPads()
    {
        float dt = Time.unscaledDeltaTime;
        float ease = 1f - Mathf.Exp(-16f * dt);
        int n = Mathf.Min(padSlot.Length, MaxPadIcons);
        padsEmpty.enabled = n == 0;
        padsHead2.text = padsCpu ? "CPU" : "2P";
        padsHead2.color = padsCpu ? new Color(0.75f, 0.75f, 0.8f) : Cursor2;
        padsModeLine.text = padsCpu ? "1P VS CPU  -  CHOOSE YOUR CONTROLLER" : "1P VS 2P  -  CHOOSE YOUR CONTROLLERS";

        bool has1 = false, has2 = false;
        for (int i = 0; i < n; i++) { if (padSlot[i] == 0) has1 = true; else if (padSlot[i] == 1) has2 = true; }

        // Her sütunda yukarıdan aşağı sıra: önce kollar, ortada en altta kullanılmayan klavye
        int[] rows = new int[3];
        for (int i = 0; i < MaxPadIcons; i++)
        {
            bool on = i < n;
            padIcons[i].gameObject.SetActive(on);
            if (!on) { padX[i] = float.NaN; continue; }
            int sl = padSlot[i];
            int col = sl == 0 ? 0 : sl == 1 ? 2 : 1;
            float tx = (col - 1) * 94f, ty = 26f - rows[col]++ * 31f;
            if (float.IsNaN(padX[i])) { padX[i] = tx; padY[i] = ty; }
            padX[i] = Mathf.Lerp(padX[i], tx, ease);
            padY[i] = Mathf.Lerp(padY[i], ty, ease);
            bool hot = i < padHot.Length && padHot[i];
            padIcons[i].anchoredPosition = new Vector2(padX[i], padY[i] + (hot ? 1.5f : 0f));
            padIcons[i].localScale = Vector3.one * (hot ? 1.12f : 1f);
            Color c = sl == 0 ? Cursor1 : sl == 1 ? Cursor2 : new Color(0.72f, 0.72f, 0.78f);
            if (hot) c = Color.Lerp(c, Color.white, 0.55f);
            foreach (var img in padTint[i]) img.color = c;
            padLabels[i].text = "PAD " + (i + 1);
            padLabels[i].color = sl == -1 ? new Color(0.75f, 0.75f, 0.8f) : Color.white;
        }

        // Klavye: kolu olmayan oyuncunun sütununda; iki oyuncunun da kolu varsa (ya da CPU modunda 1P'nin) ortada, kullanılmıyor
        bool kb1 = !has1, kb2 = !padsCpu && !has2, kbMid = !kb1 && !kb2;
        bool[] show = { kb1, kbMid, kb2 };
        for (int k = 0; k < 3; k++)
        {
            kbIcons[k].gameObject.SetActive(show[k]);
            if (!show[k]) continue;
            float ty = 26f - rows[k]++ * 31f;
            kbY[k] = kbIcons[k].gameObject.activeSelf && kbY[k] != 0f ? Mathf.Lerp(kbY[k], ty, ease) : ty;
            kbIcons[k].anchoredPosition = new Vector2((k - 1) * 94f, kbY[k]);
        }
        kbLabels[0].text = "KEYBOARD"; kbSubs[0].text = "WASD + F G " + kbKey1;
        kbLabels[2].text = "KEYBOARD"; kbSubs[2].text = "ARROWS + K L " + kbKey2;
        kbLabels[1].text = "KEYBOARD"; kbSubs[1].text = "OFF";
        kbLabels[1].color = new Color(0.6f, 0.6f, 0.68f);

        padsKey1.text = kbMid ? "ALL PLAYERS ON GAMEPAD  -  KEYBOARD IS OFF" : (has1 || has2) ? "PLAYERS WITH A GAMEPAD DON'T USE THE KEYBOARD" : "NO PAD ASSIGNED  -  BOTH PLAYERS ON KEYBOARD";
        padsKey2.text = padsCpu ? "COMPUTER" : "";
    }

    #endregion

    public void ShowMode(bool on)
    {
        if (modeGroup == null) return;
        modeGroup.SetActive(on);
        if (on)
        {
            if (padsGroup != null) padsGroup.SetActive(false);
            titleGroup.SetActive(false);
            fightGroup.SetActive(false);
            if (selectGroup != null) selectGroup.SetActive(false);
            centerText.enabled = false;
            RefreshMode();
        }
    }

    public void SetModeSelection(int i) { modeSel = i; RefreshMode(); }

    void RefreshMode()
    {
        bool cursorOn = Mathf.Repeat(Time.unscaledTime, 0.5f) < 0.35f;
        for (int i = 0; i < modeOptions.Length; i++)
        {
            bool sel = i == modeSel;
            modeOptions[i].color = sel ? Color.white : OptionOff;
            modeOptions[i].text = sel && cursorOn ? "> " + ModeLabels[i] + " <" : ModeLabels[i];
        }
        modeDesc.text = ModeDescs[Mathf.Clamp(modeSel, 0, ModeDescs.Length - 1)];
    }

    /// <summary>CPU modunda seçim ekranı ve kombo sayacı "2P" yerine "CPU" gösterir.</summary>
    public void SetCpuMode(bool on, string key2)
    {
        cpuMode = on;
        if (!string.IsNullOrEmpty(key2) && key2 != "CPU") kbKey2 = key2;
        if (!string.IsNullOrEmpty(key2)) comboKey2 = key2;
        if (cellTag2 != null) foreach (var t in cellTag2) t.text = on ? "CPU" : "2P";
    }

    #endregion

    public void ShowSelect(bool on)
    {
        if (selectGroup == null) return;
        selectGroup.SetActive(on);
        if (on)
        {
            if (padsGroup != null) padsGroup.SetActive(false);
            titleGroup.SetActive(false);
            fightGroup.SetActive(false);
            centerText.enabled = false;
            deny1Until = deny2Until = 0f;
            RefreshSelect();
        }
    }

    public void SetSelect(int cur1, int cur2, bool lock1, bool lock2, bool showCur2 = true)
    {
        selCur1 = cur1; selCur2 = cur2;
        selLock1 = lock1; selLock2 = lock2;
        selShowCur2 = showCur2;
    }

    public void SelectDenied(int side)
    {
        if (side == 0) deny1Until = Time.unscaledTime + 0.6f;
        else deny2Until = Time.unscaledTime + 0.6f;
    }

    void RefreshSelect()
    {
        float now = Time.unscaledTime;
        bool blink = Mathf.Repeat(now, 0.5f) < 0.3f;
        for (int i = 0; i < cellFrames.Length; i++)
        {
            bool h1 = i == selCur1, h2 = selShowCur2 && i == selCur2;
            Color frame = CellIdle;
            if (h1 && h2) frame = blink ? Cursor1 : Cursor2;
            else if (h1) frame = selLock1 || blink ? Cursor1 : Color.Lerp(Cursor1, CellIdle, 0.5f);
            else if (h2) frame = selLock2 || blink ? Cursor2 : Color.Lerp(Cursor2, CellIdle, 0.5f);
            cellFrames[i].color = frame;

            bool taken = (selLock1 && h1) || (selLock2 && h2);
            cellBGs[i].color = taken ? Color.Lerp(new Color(0.08f, 0.05f, 0.14f), palette[i].color, 0.35f) : new Color(0.08f, 0.05f, 0.14f);
            cellTag1[i].enabled = h1;
            cellTag2[i].enabled = h2;
        }

        SetSelPanel(selName1, selStatus1, selCur1, selLock1, now < deny1Until, blink, "1P ");
        if (cpuMode && !selShowCur2)
        {
            selName2.text = "CPU";
            selName2.color = Color.white;
            selStatus2.text = "WAITING FOR 1P";
            selStatus2.color = new Color(0.7f, 0.7f, 0.75f);
        }
        else if (cpuMode && !selLock2)
        {
            SetSelPanel(selName2, selStatus2, selCur2, false, false, blink, "CPU ");
            selStatus2.text = "CHOOSING...";
        }
        else SetSelPanel(selName2, selStatus2, selCur2, selLock2, now < deny2Until, blink, cpuMode ? "CPU " : "2P ");
    }

    void SetSelPanel(Text name, Text status, int cur, bool locked, bool denied, bool blink, string tag)
    {
        var e = palette[Mathf.Clamp(cur, 0, palette.Length - 1)];
        bool left = tag == "1P ";
        name.text = left ? tag + e.name : e.name + " " + tag.Trim();
        name.color = Color.Lerp(e.color, Color.white, 0.15f);
        if (denied) { status.text = "TAKEN!"; status.color = new Color(1f, 0.3f, 0.25f); }
        else if (locked) { status.text = tag.StartsWith("CPU") ? "READY!" : "READY!  (KICK: BACK)"; status.color = new Color(1f, 0.85f, 0.2f); }
        else { status.text = blink ? "PRESS PUNCH" : ""; status.color = Color.white; }
    }

    #endregion

    #region Güncelleme

    void Update()
    {
        float dt = Time.unscaledDeltaTime;

        chipDelay1 -= dt; chipDelay2 -= dt;
        chipv1 = target1 > chipv1 ? target1 : (chipDelay1 <= 0f ? Mathf.MoveTowards(chipv1, target1, dt * 0.6f) : chipv1);
        chipv2 = target2 > chipv2 ? target2 : (chipDelay2 <= 0f ? Mathf.MoveTowards(chipv2, target2, dt * 0.6f) : chipv2);

        SetBar(fill1, target1, true); SetBar(chip1, chipv1, true);
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
        flash.color = new Color(flashColor.r, flashColor.g, flashColor.b, flashA);

        if (titleGroup.activeSelf) AnimateTitle();
        if (pauseGroup.activeSelf) RefreshPauseOptions(Mathf.Repeat(Time.unscaledTime, 0.5f) < 0.35f);
        if (selectGroup != null && selectGroup.activeSelf) RefreshSelect();
        if (modeGroup != null && modeGroup.activeSelf) RefreshMode();
        if (stageGroup != null && stageGroup.activeSelf) RefreshStage();
        if (padsGroup != null && padsGroup.activeSelf) RefreshPads();
        if (koStart >= 0f && koText != null) AnimateKO();
        if (finisherText != null && finisherText.enabled)
        {
            float ft = Time.unscaledTime - finisherShownAt;
            var fr = finisherText.rectTransform;
            fr.localScale = Vector3.Lerp(fr.localScale, Vector3.one, 1f - Mathf.Exp(-12f * Time.unscaledDeltaTime));
            if (ft > 1.8f) finisherText.enabled = false;
        }
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