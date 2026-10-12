using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class StageLife : MonoBehaviour
{
    public enum Kind { Boxing, Rooftop, Dojo, Market }

    #region Ayarlar

    public Kind kind;

    [Header("Boks ringi")]
    [Tooltip("Spot ışıklarının gezinme açısı (derece).")]
    public float spotSweep = 9f;

    [Header("Çatı")]
    public bool helicopter = true;
    public float helicopterSpeed = 3.6f;
    public Vector2 helicopterWait = new Vector2(7f, 14f);

    [Header("Dojo")]
    public float lanternSway = 3.5f;
    public int petalCount = 22;

    [Header("Gece pazarı")]
    [Tooltip("Tencerelerden saniyede çıkan buhar bulutu sayısı.")]
    public float steamRate = 4f;
    [Range(0f, 1f)] public float steamAlpha = 0.22f;

    #endregion

    #region Durum

    Light[] spots = new Light[0];
    Quaternion[] spotBase = new Quaternion[0];

    Transform[] stars = new Transform[0];
    Vector3[] starBase = new Vector3[0];
    float[] starPhase = new float[0], starSpeed = new float[0];
    Transform heli, rotor, tailRotor;
    Renderer navRed, navGreen, strobe;
    float heliX, heliDir = 1f, heliWait = 3f;
    bool heliFlying;

    Transform[] lanterns = new Transform[0];
    Petal[] petals = new Petal[0];

    Vector3[] pots = new Vector3[0];
    readonly List<Puff> puffs = new List<Puff>();
    float steamTimer;
    Mesh sphere;
    MaterialPropertyBlock props;

    System.Random rng;

    class Petal
    {
        public Transform t;
        public Vector3 pos, spin;
        public float fall, swayPhase, rest;
    }

    class Puff
    {
        public Vector3 pos;
        public float age, life, size, drift;
    }

    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    static readonly int ColorId = Shader.PropertyToID("_Color");

    #endregion

    #region Kurulum

    void Start()
    {
        rng = new System.Random(GetInstanceID());
        props = new MaterialPropertyBlock();
        switch (kind)
        {
            case Kind.Boxing: SetupBoxing(); break;
            case Kind.Rooftop: SetupRooftop(); break;
            case Kind.Dojo: SetupDojo(); break;
            case Kind.Market: SetupMarket(); break;
        }
        SetupImpacts();
    }

    float R(float a, float b) { return a + (float)rng.NextDouble() * (b - a); }

    void SetupBoxing()
    {
        var list = new List<Light>();
        foreach (var l in GetComponentsInChildren<Light>(true))
            if (l.type == LightType.Spot) list.Add(l);
        spots = list.ToArray();
        spotBase = new Quaternion[spots.Length];
        for (int i = 0; i < spots.Length; i++) spotBase[i] = spots[i].transform.localRotation;
    }

    void SetupRooftop()
    {
        var list = new List<Transform>();
        foreach (var r in GetComponentsInChildren<Renderer>(true))
            if (r.name == "Star") list.Add(r.transform);
        stars = list.ToArray();
        starBase = new Vector3[stars.Length];
        starPhase = new float[stars.Length];
        starSpeed = new float[stars.Length];
        for (int i = 0; i < stars.Length; i++)
        {
            starBase[i] = stars[i].localScale;
            starPhase[i] = R(0f, 6.28f);
            starSpeed[i] = R(1.5f, 4.5f);
        }
        if (helicopter) BuildHelicopter();
    }

    void BuildHelicopter()
    {
        var body = CFMaterials.Unlit(new Color(0.07f, 0.07f, 0.1f));
        var glass = CFMaterials.Unlit(new Color(0.25f, 0.35f, 0.5f));
        heli = new GameObject("Helicopter").transform;
        heli.SetParent(transform, false);

        Part(heli, PrimitiveType.Cube, new Vector3(0f, 0f, 0f), new Vector3(1.5f, 0.65f, 0.75f), body);
        Part(heli, PrimitiveType.Cube, new Vector3(0.85f, -0.05f, 0f), new Vector3(0.45f, 0.5f, 0.7f), glass);
        Part(heli, PrimitiveType.Cube, new Vector3(-1.5f, 0.1f, 0f), new Vector3(1.6f, 0.16f, 0.16f), body);
        Part(heli, PrimitiveType.Cube, new Vector3(-2.25f, 0.35f, 0f), new Vector3(0.18f, 0.55f, 0.06f), body);
        Part(heli, PrimitiveType.Cube, new Vector3(0f, -0.48f, 0.3f), new Vector3(1.5f, 0.05f, 0.05f), body);
        Part(heli, PrimitiveType.Cube, new Vector3(0f, -0.48f, -0.3f), new Vector3(1.5f, 0.05f, 0.05f), body);
        Part(heli, PrimitiveType.Cube, new Vector3(0f, 0.42f, 0f), new Vector3(0.12f, 0.2f, 0.12f), body);

        rotor = new GameObject("Rotor").transform;
        rotor.SetParent(heli, false);
        rotor.localPosition = new Vector3(0f, 0.55f, 0f);
        Part(rotor, PrimitiveType.Cube, Vector3.zero, new Vector3(3.4f, 0.03f, 0.12f), body);
        Part(rotor, PrimitiveType.Cube, Vector3.zero, new Vector3(0.12f, 0.03f, 3.4f), body);

        tailRotor = new GameObject("TailRotor").transform;
        tailRotor.SetParent(heli, false);
        tailRotor.localPosition = new Vector3(-2.25f, 0.35f, -0.08f);
        Part(tailRotor, PrimitiveType.Cube, Vector3.zero, new Vector3(0.06f, 0.7f, 0.02f), body);

        navRed = Part(heli, PrimitiveType.Cube, new Vector3(0.2f, -0.2f, -0.4f), Vector3.one * 0.09f, CFMaterials.Unlit(new Color(1f, 0.1f, 0.1f))).GetComponent<Renderer>();
        navGreen = Part(heli, PrimitiveType.Cube, new Vector3(0.2f, -0.2f, 0.4f), Vector3.one * 0.09f, CFMaterials.Unlit(new Color(0.2f, 1f, 0.3f))).GetComponent<Renderer>();
        strobe = Part(heli, PrimitiveType.Cube, new Vector3(-2.3f, 0.65f, 0f), Vector3.one * 0.1f, CFMaterials.Unlit(Color.white)).GetComponent<Renderer>();

        heli.gameObject.SetActive(false);
    }

    void SetupDojo()
    {
        var parts = GetComponentsInChildren<Transform>(true);
        var list = new List<Transform>();
        foreach (var t in parts)
        {
            if (t.name != "Lantern") continue;
            Vector3 p = t.localPosition;
            var pivot = new GameObject("LanternPivot").transform;
            pivot.SetParent(transform, false);
            pivot.localPosition = p + new Vector3(0f, 2.2f, 0f);
            foreach (var o in parts)
            {
                if (o == null || o == transform) continue;
                bool piece = o.name == "Lantern" || o.name == "LanternCapT" || o.name == "LanternCapB" || o.name == "LanternCord" || o.name == "LanternLight";
                if (!piece || o.parent != transform) continue;
                if (Mathf.Abs(o.localPosition.x - p.x) < 0.05f && Mathf.Abs(o.localPosition.z - p.z) < 1f)
                    o.SetParent(pivot, true);
            }
            list.Add(pivot);
        }
        lanterns = list.ToArray();

        var pink = new[] { CFMaterials.Unlit(new Color(1f, 0.72f, 0.82f)), CFMaterials.Unlit(new Color(1f, 0.85f, 0.9f)) };
        petals = new Petal[Mathf.Max(0, petalCount)];
        for (int i = 0; i < petals.Length; i++)
        {
            var t = Part(transform, PrimitiveType.Cube, Vector3.zero, new Vector3(0.07f, 0.045f, 0.006f), pink[i % 2]);
            petals[i] = new Petal { t = t };
            RespawnPetal(petals[i], true);
        }
    }

    void RespawnPetal(Petal p, bool anywhere)
    {
        p.pos = new Vector3(R(-7.5f, 7.5f), anywhere ? R(0.2f, 4.6f) : R(4.2f, 4.8f), R(1.2f, 5.6f));
        p.fall = R(0.25f, 0.5f);
        p.swayPhase = R(0f, 6.28f);
        p.spin = new Vector3(R(-200f, 200f), R(-120f, 120f), R(-260f, 260f));
        p.rest = 0f;
        p.t.localPosition = p.pos;
    }

    void SetupMarket()
    {
        var list = new List<Vector3>();
        foreach (var r in GetComponentsInChildren<Renderer>(true))
            if (r.name == "Pot") list.Add(r.transform.localPosition + new Vector3(0f, 0.2f, 0f));
        pots = list.ToArray();
        sphere = CFMaterials.PrimitiveMesh(PrimitiveType.Sphere);
    }

    static Transform Part(Transform parent, PrimitiveType type, Vector3 pos, Vector3 scale, Material m)
    {
        var go = GameObject.CreatePrimitive(type);
        Destroy(go.GetComponent<Collider>());
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localScale = scale;
        var r = go.GetComponent<Renderer>();
        r.sharedMaterial = m;
        r.shadowCastingMode = ShadowCastingMode.Off;
        return go.transform;
    }

    #endregion

    #region Güncelleme

    void Update()
    {
        float t = Time.time, dt = Time.deltaTime;
        switch (kind)
        {
            case Kind.Boxing: UpdateBoxing(t); break;
            case Kind.Rooftop: UpdateRooftop(t, dt); break;
            case Kind.Dojo: UpdateDojo(t, dt); break;
            case Kind.Market: UpdateMarket(dt); break;
        }
        UpdateImpacts(t, dt);
    }

    void UpdateBoxing(float t)
    {
        for (int i = 0; i < spots.Length; i++)
        {
            if (spots[i] == null) continue;
            float s = i % 2 == 0 ? 1f : -1f;
            var sweep = Quaternion.Euler(Mathf.Sin(t * 0.55f + i) * spotSweep * 0.6f, Mathf.Sin(t * 0.37f + i * 2f) * spotSweep * s, 0f);
            spots[i].transform.localRotation = spotBase[i] * sweep;
        }
    }

    void UpdateRooftop(float t, float dt)
    {
        for (int i = 0; i < stars.Length; i++)
        {
            if (stars[i] == null) continue;
            float k = 0.5f + 0.5f * Mathf.Sin(t * starSpeed[i] + starPhase[i]);
            stars[i].localScale = starBase[i] * (0.45f + 0.75f * k * k);
        }

        if (heli == null) return;
        if (!heliFlying)
        {
            heliWait -= dt;
            if (heliWait > 0f) return;
            heliFlying = true;
            heliDir = rng.Next(2) == 0 ? 1f : -1f;
            heliX = -19f * heliDir;
            heli.gameObject.SetActive(true);
        }

        heliX += heliDir * helicopterSpeed * dt;
        float y = 5.1f + Mathf.Sin(t * 1.3f) * 0.12f;
        heli.localPosition = new Vector3(heliX, y, 18f);
        heli.localRotation = Quaternion.Euler(0f, heliDir > 0f ? 0f : 180f, -6f);
        rotor.localRotation = Quaternion.Euler(0f, t * 900f, 0f);
        tailRotor.localRotation = Quaternion.Euler(0f, 0f, t * 1400f);
        navRed.enabled = Mathf.Repeat(t, 1f) < 0.5f;
        navGreen.enabled = navRed.enabled;
        strobe.enabled = Mathf.Repeat(t, 1.2f) < 0.08f;

        if (Mathf.Abs(heliX) > 19.5f)
        {
            heliFlying = false;
            heliWait = R(helicopterWait.x, helicopterWait.y);
            heli.gameObject.SetActive(false);
        }
    }

    void UpdateDojo(float t, float dt)
    {
        for (int i = 0; i < lanterns.Length; i++)
        {
            if (lanterns[i] == null) continue;
            lanterns[i].localRotation = Quaternion.Euler(Mathf.Sin(t * 0.9f + i) * lanternSway * 0.5f, 0f, Mathf.Sin(t * 1.3f + i * 1.7f) * lanternSway);
        }

        foreach (var p in petals)
        {
            if (p.rest > 0f)
            {
                p.rest -= dt;
                if (p.rest <= 0f) RespawnPetal(p, false);
                continue;
            }
            p.pos.y -= p.fall * dt;
            p.pos.x += Mathf.Sin(t * 1.4f + p.swayPhase) * 0.35f * dt + 0.08f * dt;
            p.t.localPosition = p.pos;
            p.t.localRotation = Quaternion.Euler(p.spin * t);
            if (p.pos.y <= 0.01f)
            {
                p.pos.y = 0.01f;
                p.t.localPosition = p.pos;
                p.t.localRotation = Quaternion.Euler(90f, p.swayPhase * 57f, 0f);
                p.rest = R(1.5f, 4f);
            }
        }
    }

    void UpdateMarket(float dt)
    {
        var mat = CFMaterials.Transparent;
        if (mat == null || pots.Length == 0) return;

        steamTimer -= dt;
        while (steamTimer <= 0f)
        {
            steamTimer += 1f / Mathf.Max(0.1f, steamRate * pots.Length);
            var p = pots[rng.Next(pots.Length)];
            puffs.Add(new Puff { pos = p + new Vector3(R(-0.1f, 0.1f), 0f, R(-0.1f, 0.1f)), life = R(1.4f, 2.1f), size = R(0.14f, 0.22f), drift = R(-0.12f, 0.12f) });
        }

        var rp = new RenderParams(mat) { matProps = props, layer = gameObject.layer, shadowCastingMode = ShadowCastingMode.Off, receiveShadows = false };
        for (int i = puffs.Count - 1; i >= 0; i--)
        {
            var f = puffs[i];
            f.age += dt;
            float k = f.age / f.life;
            if (k >= 1f) { puffs.RemoveAt(i); continue; }
            f.pos += new Vector3(f.drift, 0.45f, 0f) * dt;
            float size = f.size * (1f + k * 2.2f);
            var c = new Color(1f, 0.97f, 0.95f, steamAlpha * Mathf.Sin(Mathf.PI * k));
            props.SetColor(BaseColorId, c);
            props.SetColor(ColorId, c);
            Graphics.RenderMesh(rp, sphere, 0, Matrix4x4.TRS(transform.TransformPoint(f.pos), Quaternion.identity, Vector3.one * size));
        }
    }

    #endregion
    #region Darbe tepkisi (dövüşçü kenara / yere çarpınca)

    static readonly List<StageLife> all = new List<StageLife>();

    [Header("Darbe tepkisi")]
    [Tooltip("Dövüşçü kenara savrulunca dekorun sarsılma gücü.")]
    public float impactAmount = 1f;

    class Shaker
    {
        public Transform t;
        public Vector3 basePos;
        public Quaternion baseRot;
        public float amp, phase;
        public Vector3 axis;
        public bool rotate;
    }

    class Loose
    {
        public Transform t;
        public Vector3 basePos, pos, vel, spin;
        public Quaternion baseRot;
        public bool flying, fallen;
    }

    class Bit
    {
        public Transform t;
        public Vector3 pos, vel;
        public float age, life;
    }

    readonly List<Shaker> shakers = new List<Shaker>();
    readonly List<Loose> loose = new List<Loose>();
    readonly List<Bit> bits = new List<Bit>();
    float[] lanternKick = new float[0];
    bool impactReady;

    void OnEnable()
    {
        if (!all.Contains(this)) all.Add(this);
        Fighter.OnStageImpact += Impact;
    }

    void OnDisable()
    {
        Fighter.OnStageImpact -= Impact;
        all.Remove(this);
    }

    /// <summary>Raunt başında: düşen kaseler yerine döner, sallanan her şey durur.</summary>
    public static void ResetAll()
    {
        foreach (var s in all) if (s != null) s.ResetImpacts();
    }

    void ResetImpacts()
    {
        foreach (var l in loose)
        {
            if (l.t == null) continue;
            l.flying = l.fallen = false;
            l.t.localPosition = l.basePos;
            l.t.localRotation = l.baseRot;
        }
        foreach (var s in shakers)
        {
            if (s.t == null) continue;
            s.amp = 0f;
            s.t.localPosition = s.basePos;
            s.t.localRotation = s.baseRot;
        }
        for (int i = 0; i < lanternKick.Length; i++) lanternKick[i] = 0f;
        foreach (var b in bits) if (b.t != null) Destroy(b.t.gameObject);
        bits.Clear();
    }

    /// <summary>Tepki verecek parçaları isimlerinden bulur (Start'ta, sahneye özel kurulumdan sonra).</summary>
    void SetupImpacts()
    {
        foreach (var t in GetComponentsInChildren<Transform>(true))
        {
            string n = t.name;
            switch (kind)
            {
                case Kind.Boxing:
                    if (n == "RopeL" || n == "RopeR") AddShaker(t, Vector3.right, false);
                    else if (n == "RopeBack") AddShaker(t, Vector3.forward, false);
                    else if (n == "CornerP1" || n == "CornerP2" || n == "Post") AddShaker(t, Vector3.right, false);
                    break;
                case Kind.Rooftop:
                    if (n == "BannerP1" || n == "BannerP2" || n == "FlagStripe") AddShaker(t, Vector3.up, true);
                    else if (n == "ACUnit" || n == "ACFan" || n == "ACVent" || n == "Antenna" || n == "AntennaBar" || n == "AntennaLight") AddShaker(t, Vector3.right, false);
                    break;
                case Kind.Dojo:
                    if (n == "Staff") AddShaker(t, Vector3.forward, true);
                    else if (n == "BannerP1" || n == "BannerP2" || n == "FlagStripe") AddShaker(t, Vector3.up, true);
                    else if (n == "ScrollPaper" || n == "Ink" || n == "Seal") AddShaker(t, Vector3.forward, true);
                    break;
                case Kind.Market:
                    if (n == "Bowl" || n == "Bottle") loose.Add(new Loose { t = t, basePos = t.localPosition, baseRot = t.localRotation });
                    else if (n == "Pot" || n == "SignBoard" || n == "Valance") AddShaker(t, Vector3.right, false);
                    else if (n == "BannerP1" || n == "BannerP2" || n == "FlagStripe") AddShaker(t, Vector3.up, true);
                    break;
            }
        }
        lanternKick = new float[lanterns.Length];
        impactReady = true;
    }

    void AddShaker(Transform t, Vector3 axis, bool rotate)
    {
        shakers.Add(new Shaker { t = t, basePos = t.localPosition, baseRot = t.localRotation, axis = axis, rotate = rotate, phase = R(0f, 6.28f) });
    }

    /// <summary>worldPos: çarpma noktası, strength: 0..1</summary>
    void Impact(Vector3 worldPos, float strength)
    {
        if (!impactReady || !isActiveAndEnabled) return;
        strength = Mathf.Clamp01(strength) * impactAmount;
        Vector3 p = transform.InverseTransformPoint(worldPos);
        bool side = Mathf.Abs(p.x) > Fighter.StageHalfWidth - 0.6f && p.y > 0.3f;   // kenara savrulma (yere düşme değil)

        // Yakındaki parçalar daha çok sallanır
        foreach (var s in shakers)
        {
            if (s.t == null) continue;
            float dx = Mathf.Abs(s.basePos.x - p.x);
            float near = Mathf.Clamp01(1.2f - dx / 6f);
            if (kind == Kind.Boxing && s.t.name != "RopeBack")
                near = Mathf.Sign(s.basePos.x) == Mathf.Sign(p.x) ? (side ? 1f : 0.4f) : 0.15f;
            s.amp = Mathf.Max(s.amp, strength * near);
        }

        switch (kind)
        {
            case Kind.Dojo:
                for (int i = 0; i < lanterns.Length; i++)
                {
                    if (lanterns[i] == null) continue;
                    float near = Mathf.Clamp01(1.3f - Mathf.Abs(lanterns[i].localPosition.x - p.x) / 6f);
                    lanternKick[i] = Mathf.Max(lanternKick[i], strength * near * 18f);
                }
                // Tavandan yaprak dökülsün
                int drop = Mathf.RoundToInt(strength * 8f);
                foreach (var pt in petals)
                {
                    if (drop <= 0) break;
                    if (pt.rest <= 0f && pt.pos.y < 3f) continue;
                    pt.pos = new Vector3(p.x + R(-2f, 2f), R(3.6f, 4.6f), R(1f, 4f));
                    pt.rest = 0f;
                    pt.fall = R(0.5f, 0.9f);
                    drop--;
                }
                Debris(p, strength, new Color(0.55f, 0.4f, 0.25f));
                break;

            case Kind.Market:
                // Kaseler ve şişeler tezgâhtan zıplayıp düşer
                foreach (var l in loose)
                {
                    if (l.t == null || l.flying || l.fallen) continue;
                    float dx = Mathf.Abs(l.basePos.x - p.x);
                    float chance = strength * Mathf.Clamp01(1.4f - dx / 4f);
                    if (rng.NextDouble() > chance) continue;
                    l.flying = true;
                    l.pos = l.t.localPosition;
                    l.vel = new Vector3(R(-0.8f, 0.8f), R(1.8f, 3.4f) * (0.6f + strength), R(-1.6f, -0.6f));
                    l.spin = new Vector3(R(-500f, 500f), R(-300f, 300f), R(-500f, 500f));
                }
                Debris(p, strength, new Color(0.45f, 0.45f, 0.5f));
                break;

            case Kind.Rooftop:
                Debris(p, strength, new Color(0.5f, 0.5f, 0.55f));
                break;

            default:
                Debris(p, strength * 0.6f, new Color(0.85f, 0.85f, 0.9f));
                break;
        }
    }

    /// <summary>Çarpma noktasından saçılan küçük parçalar (toz, çakıl, talaş).</summary>
    void Debris(Vector3 p, float strength, Color c)
    {
        int n = Mathf.RoundToInt(4 + strength * 8f);
        var m = CFMaterials.Unlit(c);
        for (int i = 0; i < n; i++)
        {
            var t = Part(transform, PrimitiveType.Cube, p, Vector3.one * R(0.04f, 0.09f), m);
            float dir = p.x > 0f ? -1f : 1f;
            bits.Add(new Bit { t = t, pos = new Vector3(p.x, Mathf.Max(0.05f, p.y - 0.6f), p.z), vel = new Vector3(dir * R(0.5f, 2.5f), R(1.5f, 4f) * (0.5f + strength), R(-1f, 1f)), life = R(0.6f, 1.1f) });
        }
    }

    void UpdateImpacts(float t, float dt)
    {
        if (!impactReady) return;

        foreach (var s in shakers)
        {
            if (s.t == null || s.amp <= 0.001f) continue;
            float w = Mathf.Sin(t * 26f + s.phase) * s.amp;
            if (s.rotate) s.t.localRotation = s.baseRot * Quaternion.AngleAxis(w * 14f, s.axis == Vector3.up ? Vector3.forward : s.axis);
            else s.t.localPosition = s.basePos + s.axis * w * 0.12f;
            s.amp = Mathf.MoveTowards(s.amp * Mathf.Exp(-3.5f * dt), 0f, dt * 0.02f);
            if (s.amp <= 0.001f) { s.t.localPosition = s.basePos; s.t.localRotation = s.baseRot; }
        }

        for (int i = 0; i < lanternKick.Length && i < lanterns.Length; i++)
        {
            if (lanterns[i] == null || lanternKick[i] <= 0.01f) continue;
            lanterns[i].localRotation *= Quaternion.Euler(0f, 0f, Mathf.Sin(t * 5.5f + i) * lanternKick[i]);
            lanternKick[i] *= Mathf.Exp(-1.2f * dt);
        }

        foreach (var l in loose)
        {
            if (l.t == null || !l.flying) continue;
            l.vel.y -= 14f * dt;
            l.pos += l.vel * dt;
            l.t.localRotation *= Quaternion.Euler(l.spin * dt);
            if (l.pos.y <= 0.06f && l.vel.y < 0f)
            {
                l.pos.y = 0.06f;
                if (Mathf.Abs(l.vel.y) > 1.5f) { l.vel.y *= -0.3f; l.vel.x *= 0.5f; l.vel.z *= 0.5f; l.spin *= 0.4f; }
                else
                {
                    l.flying = false;
                    l.fallen = true;
                    l.t.localRotation = Quaternion.Euler(90f, R(0f, 360f), 0f);
                }
            }
            l.t.localPosition = l.pos;
        }

        for (int i = bits.Count - 1; i >= 0; i--)
        {
            var b = bits[i];
            b.age += dt;
            if (b.t == null || b.age >= b.life) { if (b.t != null) Destroy(b.t.gameObject); bits.RemoveAt(i); continue; }
            b.vel.y -= 12f * dt;
            b.pos += b.vel * dt;
            if (b.pos.y < 0.02f) { b.pos.y = 0.02f; b.vel *= 0.4f; }
            b.t.localPosition = b.pos;
            b.t.localScale = Vector3.one * 0.07f * (1f - b.age / b.life);
        }
    }

    #endregion

}
