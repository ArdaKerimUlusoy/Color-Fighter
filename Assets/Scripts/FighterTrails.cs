using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class FighterTrails : MonoBehaviour
{
    #region Ayarlar

    [Header("Gölge")]
    public bool shadow = true;
    [Range(0f, 1f)] public float shadowAlpha = 0.5f;
    public Vector2 shadowSize = new Vector2(0.95f, 0.5f);

    [Header("Hayalet izi")]
    [Tooltip("COLOR RUSH kombo saldırısında ve finisher'larda arkada kalan renkli silüetler.")]
    public bool ghosts = true;
    [Tooltip("Kaç saniyede bir yeni silüet bırakılır.")]
    public float ghostInterval = 0.045f;
    [Tooltip("Bir silüetin kaybolma süresi.")]
    public float ghostLife = 0.3f;
    [Range(0f, 1f)] public float ghostAlpha = 0.45f;

    #endregion

    #region Durum

    struct Part
    {
        public Mesh mesh;
        public Matrix4x4 matrix;
    }

    class Ghost
    {
        public Part[] parts;
        public float age;
        public Color color;
    }

    Fighter fighter;
    Mesh disc;
    MaterialPropertyBlock props;
    float ghostTimer;
    readonly List<Ghost> live = new List<Ghost>();
    readonly List<Ghost> pool = new List<Ghost>();
    readonly List<MeshFilter> filters = new List<MeshFilter>();
    readonly List<Part> scratch = new List<Part>();
    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    static readonly int ColorId = Shader.PropertyToID("_Color");

    void Start()
    {
        fighter = GetComponent<Fighter>();
        props = new MaterialPropertyBlock();
        disc = CFMaterials.PrimitiveMesh(PrimitiveType.Cylinder);
    }

    #endregion

    #region Güncelleme

    void Update()
    {
        if (fighter == null) return;
        var mat = CFMaterials.Transparent;
        if (mat == null) return;

        if (ghosts && WantsGhost())
        {
            ghostTimer -= Time.deltaTime;
            if (ghostTimer <= 0f)
            {
                ghostTimer = ghostInterval;
                Snapshot();
            }
        }
        else ghostTimer = 0f;

        DrawGhosts(mat);
        if (shadow) DrawShadow(mat);
    }

    bool WantsGhost()
    {
        var f = fighter;
        return f.InComboRush || f.InFinisher;
    }

    void Snapshot()
    {
        GetComponentsInChildren(false, filters);
        scratch.Clear();
        foreach (var mf in filters)
        {
            if (mf == null || mf.sharedMesh == null) continue;
            var r = mf.GetComponent<MeshRenderer>();
            if (r == null || !r.enabled) continue;
            scratch.Add(new Part { mesh = mf.sharedMesh, matrix = mf.transform.localToWorldMatrix });
        }
        if (scratch.Count == 0) return;

        Ghost g;
        if (pool.Count > 0) { g = pool[pool.Count - 1]; pool.RemoveAt(pool.Count - 1); }
        else g = new Ghost();
        g.parts = scratch.ToArray();
        g.age = 0f;
        g.color = Color.Lerp(fighter.mainColor, Color.white, 0.35f);
        live.Add(g);
    }

    void DrawGhosts(Material mat)
    {
        float dt = Time.deltaTime;
        for (int i = live.Count - 1; i >= 0; i--)
        {
            var g = live[i];
            g.age += dt;
            float k = g.age / Mathf.Max(0.01f, ghostLife);
            if (k >= 1f)
            {
                live.RemoveAt(i);
                pool.Add(g);
                continue;
            }
            var c = g.color;
            c.a = ghostAlpha * (1f - k) * (1f - k);
            SetColor(c);
            var rp = Params(mat);
            foreach (var p in g.parts) Graphics.RenderMesh(rp, p.mesh, 0, p.matrix);
        }
    }

    void DrawShadow(Material mat)
    {
        var arena = transform.parent;
        if (arena == null || disc == null) return;
        float h = Mathf.Max(0f, fighter.Y);
        float k = 1f / (1f + h * 0.7f);
        Vector3 pos = arena.TransformPoint(new Vector3(fighter.X, 0.012f, 0f));
        var m = Matrix4x4.TRS(pos, arena.rotation, new Vector3(shadowSize.x * k, 0.001f, shadowSize.y * k));
        SetColor(new Color(0f, 0f, 0.02f, shadowAlpha * k));
        Graphics.RenderMesh(Params(mat), disc, 0, m);
    }

    void SetColor(Color c)
    {
        props.SetColor(BaseColorId, c);
        props.SetColor(ColorId, c);
    }

    RenderParams Params(Material mat)
    {
        return new RenderParams(mat)
        {
            matProps = props,
            layer = gameObject.layer,
            shadowCastingMode = ShadowCastingMode.Off,
            receiveShadows = false,
        };
    }

    #endregion
}

public static class CFMaterials
{
    #region Saydam materyal

    static Material transparent;
    static readonly Dictionary<PrimitiveType, Mesh> meshes = new Dictionary<PrimitiveType, Mesh>();

    public static Material Transparent
    {
        get
        {
            if (transparent != null) return transparent;
            var src = Resources.Load<Material>("ColorFighterFX");
            if (src != null)
            {
                transparent = new Material(src);
                return transparent;
            }
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) return null;
            transparent = new Material(shader) { name = "ColorFighterFX (runtime)" };
            transparent.SetFloat("_Surface", 1f);
            transparent.SetFloat("_Blend", 0f);
            transparent.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            transparent.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            transparent.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            transparent.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
            transparent.SetFloat("_ZWrite", 0f);
            transparent.SetOverrideTag("RenderType", "Transparent");
            transparent.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            transparent.renderQueue = (int)RenderQueue.Transparent;
            return transparent;
        }
    }

    public static Mesh PrimitiveMesh(PrimitiveType type)
    {
        if (meshes.TryGetValue(type, out var m) && m != null) return m;
        var go = GameObject.CreatePrimitive(type);
        m = go.GetComponent<MeshFilter>().sharedMesh;
        Object.Destroy(go);
        meshes[type] = m;
        return m;
    }

    public static Material Unlit(Color c)
    {
        var shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Unlit/Color");
        var m = new Material(shader) { color = c };
        m.SetColor("_BaseColor", c);
        return m;
    }

    #endregion
}
