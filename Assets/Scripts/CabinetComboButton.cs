using UnityEngine;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

public class CabinetComboButton : MonoBehaviour
{
    #region Ayarlar

    public FighterInput input;
    public Fighter fighter;
    public float pressDepth = 0.014f;

    static readonly Color Idle = new Color(0.62f, 0.46f, 0.08f);
    static readonly Color Gold = new Color(1f, 0.8f, 0.15f);
    static readonly Color Hot = new Color(2.2f, 1.8f, 0.7f);

    #endregion

    #region Durum

    [SerializeField, HideInInspector] Transform cap;
    [SerializeField, HideInInspector] Light glow;
    [SerializeField, HideInInspector] Text label;
    Vector3 capRest;
    Material capMat;

    #endregion

    #region Kurulum

    public static bool AttachAll(Transform arcadeRoot, Fighter p1, Fighter p2)
    {
        if (arcadeRoot == null || p1 == null || p2 == null) return false;
        bool added = false;
        foreach (var cc in arcadeRoot.GetComponentsInChildren<CabinetControls>(true))
        {
            var existing = cc.GetComponent<CabinetComboButton>();
            if (existing != null && existing.cap != null) continue;
            Fighter f = cc.input == p1.input ? p1 : cc.input == p2.input ? p2 : null;
            if (f == null || cc.punchButton == null || cc.kickButton == null) continue;
            var b = existing != null ? existing : cc.gameObject.AddComponent<CabinetComboButton>();
            b.input = cc.input;
            b.fighter = f;
            b.Build(cc.punchButton.localPosition, cc.kickButton.localPosition);
            added = true;
        }
        return added;
    }

    void Build(Vector3 punch, Vector3 kick)
    {
        foreach (var n in new[] { "ComboRing", "ComboButton", "ComboGlow", "ComboLabel" })
        {
            var old = transform.Find(n);
            if (old != null) ColorFighterUtil.Kill(old.gameObject);
        }

        Vector3 basePos = new Vector3((punch.x + kick.x) * 0.5f, punch.y, Mathf.Max(punch.z, kick.z) + 0.115f);

        var ring = Prim(PrimitiveType.Cylinder, transform, "ComboRing", basePos + new Vector3(0f, -0.006f, 0f), new Vector3(0.092f, 0.006f, 0.092f), Mat("ComboRing", new Color(0.03f, 0.03f, 0.03f)));

        cap = Prim(PrimitiveType.Cylinder, transform, "ComboButton", basePos, new Vector3(0.072f, 0.013f, 0.072f), Mat("ComboButton", Idle));

        var mark = Prim(PrimitiveType.Cube, cap, "ComboMark", new Vector3(0f, 1.02f, 0f), new Vector3(0.38f, 0.1f, 0.38f), Mat("ComboMark", new Color(0.15f, 0.08f, 0.02f)));
        mark.localRotation = Quaternion.Euler(0f, 45f, 0f);

        var lg = new GameObject("ComboGlow");
        lg.transform.SetParent(transform, false);
        lg.transform.localPosition = basePos + new Vector3(0f, 0.06f, -0.02f);
        glow = lg.AddComponent<Light>();
        glow.type = LightType.Point;
        glow.color = new Color(1f, 0.75f, 0.2f);
        glow.range = 0.35f;
        glow.intensity = 0f;

        BuildLabel(basePos + new Vector3(0f, 0.0015f, 0.068f));
    }

    void BuildLabel(Vector3 pos)
    {
        var go = new GameObject("ComboLabel", typeof(RectTransform));
        var rt = (RectTransform)go.transform;
        rt.SetParent(transform, false);
        rt.localPosition = pos;
        rt.localRotation = Quaternion.Euler(90f, 0f, 0f);
        rt.sizeDelta = new Vector2(240f, 50f);
        rt.localScale = Vector3.one * 0.0005f;
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        go.AddComponent<CanvasScaler>().dynamicPixelsPerUnit = 4f;

        var tgo = new GameObject("Text", typeof(RectTransform));
        var tr = (RectTransform)tgo.transform;
        tr.SetParent(rt, false);
        tr.anchorMin = Vector2.zero;
        tr.anchorMax = Vector2.one;
        tr.offsetMin = tr.offsetMax = Vector2.zero;
        label = tgo.AddComponent<Text>();
        label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        label.text = "COMBO";
        label.fontSize = 40;
        label.fontStyle = FontStyle.Bold;
        label.alignment = TextAnchor.MiddleCenter;
        label.color = Gold;
        label.horizontalOverflow = HorizontalWrapMode.Overflow;
        label.verticalOverflow = VerticalWrapMode.Overflow;
        label.raycastTarget = false;
        var o = tgo.AddComponent<Outline>();
        o.effectColor = Color.black;
        o.effectDistance = new Vector2(3f, -3f);
    }

    static Transform Prim(PrimitiveType type, Transform parent, string name, Vector3 pos, Vector3 scale, Material m)
    {
        var go = GameObject.CreatePrimitive(type);
        ColorFighterUtil.Kill(go.GetComponent<Collider>());
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localScale = scale;
        var r = go.GetComponent<Renderer>();
        r.sharedMaterial = m;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return go.transform;
    }

    static Material Mat(string name, Color c)
    {
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            string path = "Assets/ColorFighter_Generated/" + name + ".mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;
            if (!AssetDatabase.IsValidFolder("Assets/ColorFighter_Generated"))
                AssetDatabase.CreateFolder("Assets", "ColorFighter_Generated");
            var m = CFMaterials.Unlit(c);
            AssetDatabase.CreateAsset(m, path);
            return m;
        }
#endif
        return CFMaterials.Unlit(c);
    }

    #endregion

    #region Güncelleme

    void Start()
    {
        if (!Application.isPlaying || cap == null) return;
        capRest = cap.localPosition;
        capMat = cap.GetComponent<Renderer>().material;
    }

    void Update()
    {
        if (!Application.isPlaying || cap == null || input == null || capMat == null) return;

        bool held = input.ComboHeld;
        cap.localPosition = held ? capRest - Vector3.up * pressDepth : capRest;

        bool ready = fighter != null && fighter.ComboReady;
        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 9f);
        Color col = ready ? Color.Lerp(Gold, Hot, pulse) : Idle;
        if (held) col = Color.Lerp(col, Color.white, 0.5f);
        capMat.color = col;
        capMat.SetColor("_BaseColor", col);

        if (glow != null) glow.intensity = ready ? 0.6f + pulse * 1.4f : 0f;
        if (label != null) label.color = ready ? Color.Lerp(Gold, Color.white, pulse) : Gold;
    }

    #endregion
}

#if UNITY_EDITOR
[InitializeOnLoad]
static class CabinetComboButtonEditorSetup
{
    static double nextCheck;

    static CabinetComboButtonEditorSetup()
    {
        EditorApplication.delayCall += () => EnsureInOpenScene();
        EditorSceneManager.sceneOpened += (s, m) => EditorApplication.delayCall += () => EnsureInOpenScene();
        EditorApplication.hierarchyChanged += () =>
        {
            if (EditorApplication.timeSinceStartup < nextCheck) return;
            nextCheck = EditorApplication.timeSinceStartup + 1.0;
            EditorApplication.delayCall += () => EnsureInOpenScene();
        };
    }

    [MenuItem("Color Fighter/Kombo Butonlarını Kur")]
    static void Menu()
    {
        if (!EnsureInOpenScene())
            Debug.Log("Color Fighter: Kombo butonları zaten kurulu ya da sahnede kurulu bir kabin yok.");
    }

    static bool EnsureInOpenScene()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return false;
        var mm = Object.FindAnyObjectByType<MatchManager>();
        if (mm == null || mm.p1 == null || mm.p2 == null) return false;
        if (!CabinetComboButton.AttachAll(mm.transform, mm.p1, mm.p2)) return false;
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(mm.gameObject.scene);
        Debug.Log("Color Fighter: Kabine kombo butonları eklendi. Sahneyi kaydetmeyi unutma (Ctrl+S).");
        return true;
    }
}
#endif
