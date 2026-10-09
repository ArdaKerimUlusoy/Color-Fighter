using UnityEngine;
using UnityEngine.UI;

public class DemoBanner : MonoBehaviour
{
    #region Durum

    Text label, sub;
    Image strip;

    #endregion

    #region Kurulum

    public static DemoBanner Create(Camera fightCam)
    {
        var go = new GameObject("DemoBanner", typeof(RectTransform));
        var b = go.AddComponent<DemoBanner>();
        b.Build(fightCam);
        return b;
    }

    void Build(Camera cam)
    {
        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = cam;
        canvas.planeDistance = 0.46f;
        canvas.sortingOrder = 15;
        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(320f, 240f);
        scaler.matchWidthOrHeight = 0.5f;

        var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        var root = (RectTransform)transform;

        var s = NewRect("Strip", root, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 14f), new Vector2(0f, 40f));
        strip = s.gameObject.AddComponent<Image>();
        strip.color = new Color(0f, 0f, 0f, 0.55f);
        strip.raycastTarget = false;

        label = NewText(root, font, "DEMO PLAY", 14, new Vector2(0f, 26f), new Vector2(0f, 40f), new Color(1f, 0.85f, 0.2f));
        sub = NewText(root, font, "PRESS ANY BUTTON", 9, new Vector2(0f, 15f), new Vector2(0f, 26f), Color.white);

        gameObject.SetActive(false);
    }

    static RectTransform NewRect(string name, RectTransform parent, Vector2 aMin, Vector2 aMax, Vector2 oMin, Vector2 oMax)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var r = (RectTransform)go.transform;
        r.SetParent(parent, false);
        r.anchorMin = aMin; r.anchorMax = aMax;
        r.offsetMin = oMin; r.offsetMax = oMax;
        return r;
    }

    static Text NewText(RectTransform parent, Font font, string text, int size, Vector2 oMin, Vector2 oMax, Color c)
    {
        var r = NewRect(text, parent, new Vector2(0f, 0f), new Vector2(1f, 0f), oMin, oMax);
        var t = r.gameObject.AddComponent<Text>();
        t.font = font;
        t.text = text;
        t.fontSize = size;
        t.fontStyle = FontStyle.Bold;
        t.alignment = TextAnchor.MiddleCenter;
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

    #region Gösterim

    public void Show(bool on)
    {
        gameObject.SetActive(on);
    }

    void Update()
    {
        float t = Time.unscaledTime;
        label.enabled = Mathf.Repeat(t, 1.2f) < 0.85f;
        sub.enabled = Mathf.Repeat(t, 0.6f) < 0.4f;
    }

    #endregion
}
