using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Atari salonunda dolaþan ziyaretçiler: makinelere yürür, baþýnda oynar, sonra baþka makineye geçer.
/// Kabinlere/makinelere çarpmadan etrafýndan dolanýr. Salon elle düzenlense de çalýþýr (makineleri her açýlýþta bulur).
/// </summary>
public class ArcadeVisitors : MonoBehaviour
{
    #region Ayarlar

    public int count = 6;
    public float walkSpeed = 1.1f;
    [Tooltip("Ziyaretçilerin dolaþabileceði alan (oda içinde, XZ).")]
    public Vector2 areaMin = new Vector2(-5.5f, -4.7f), areaMax = new Vector2(5.5f, -0.6f);
    [Tooltip("Bizim kabinin hemen önündeki bu alana girmezler (zoom'da kameranýn önüne geçmesinler).")]
    public Vector2 keepOutMin = new Vector2(-0.8f, -2.4f), keepOutMax = new Vector2(0.8f, -0.3f);

    #endregion

    #region Durum

    class Spot
    {
        public Vector3 pos;
        public Vector3 face;   // bakýlacak nokta
        public bool playing;   // kabin/makine (oynanýr) mi, yoksa sadece bakma noktasý mý
        public Visitor taken;
    }

    class Visitor
    {
        public Transform root, legL, legR, armL, armR;
        public Spot target;
        public float stateTime, speed, phase;
        public bool atSpot;
        public float heading;
        public List<Vector2> path;
        public int pathIndex;
        public float stuckTime;
        public Vector2 lastPos;
    }

    readonly List<Spot> spots = new List<Spot>();
    readonly List<Visitor> visitors = new List<Visitor>();
    readonly List<Rect> obstacles = new List<Rect>();
    readonly System.Random rng = new System.Random();

    // Yol bulma ýzgarasý (25 cm hücreler)
    const float Cell = 0.25f;
    int gw, gh;
    bool[,] blocked;
    System.Func<Color, Material> mat;

    #endregion

    #region Kurulum

    /// <summary>ArcadeBootstrap çaðýrýr: sahnenin kökü (Arcade), oda ve materyal üretici.</summary>
    public void Init(Transform arcade, Transform room, System.Func<Color, Material> materialFactory)
    {
        mat = materialFactory;
        FindSpotsAndObstacles(arcade, room);
        BuildGrid();
        Color[] shirts =
        {
            new Color(0.85f, 0.25f, 0.3f), new Color(0.25f, 0.55f, 0.9f), new Color(0.95f, 0.75f, 0.2f),
            new Color(0.35f, 0.75f, 0.4f), new Color(0.6f, 0.35f, 0.85f), new Color(0.9f, 0.5f, 0.2f), new Color(0.85f, 0.85f, 0.85f),
        };
        Color[] skins = { new Color(0.96f, 0.78f, 0.62f), new Color(0.72f, 0.52f, 0.38f), new Color(0.85f, 0.65f, 0.5f), new Color(0.5f, 0.35f, 0.25f) };
        for (int i = 0; i < count; i++)
        {
            var v = BuildVisitor(i, shirts[i % shirts.Length], skins[rng.Next(skins.Length)]);
            // Baþlangýçta bazýlarý zaten bir makinede oynuyor, bazýlarý yürüyor
            var s = FreeSpot(null);
            if (s != null)
            {
                s.taken = v;
                v.target = s;
                if (i % 2 == 0 || !WalkableStart(out var p)) { v.root.position = s.pos; v.atSpot = true; v.stateTime = Range(2f, 12f); }
                else { v.root.position = p; v.atSpot = false; v.path = FindPath(new Vector2(p.x, p.z), new Vector2(s.pos.x, s.pos.z)); v.pathIndex = 0; }
            }
            visitors.Add(v);
        }
    }

    void FindSpotsAndObstacles(Transform arcade, Transform room)
    {
        spots.Clear();
        obstacles.Clear();
        var hall = room != null ? room.Find("ArcadeHall") : null;

        // Bizim kabin ve önündeki kamera alaný da engel (etrafýndan dolanýlýr)
        var cab = arcade != null ? arcade.Find("ArcadeCabinet") : null;
        if (cab != null) AddObstacle(cab, 0.2f);
        obstacles.Add(Rect.MinMaxRect(keepOutMin.x, keepOutMin.y, keepOutMax.x, keepOutMax.y));

        if (hall != null)
        {
            foreach (Transform t in hall)
            {
                if (!t.gameObject.activeInHierarchy) continue;
                string n = t.name;
                if (n.StartsWith("Cabinet"))
                {
                    AddObstacle(t, 0.22f);
                    AddSpot(t.TransformPoint(new Vector3(0f, 0f, -1.2f)), t.TransformPoint(new Vector3(0f, 1.2f, 0f)), true);
                }
                else if (n.StartsWith("ClawMachine"))
                {
                    AddObstacle(t, 0.22f);
                    AddSpot(t.TransformPoint(new Vector3(0f, 0f, -1.05f)), t.TransformPoint(new Vector3(0f, 1.2f, 0f)), true);
                }
                else if (n.StartsWith("AirHockey"))
                {
                    AddObstacle(t, 0.22f);
                    AddSpot(t.TransformPoint(new Vector3(-1.6f, 0f, 0f)), t.TransformPoint(new Vector3(0f, 0.7f, 0f)), true);
                    AddSpot(t.TransformPoint(new Vector3(1.6f, 0f, 0f)), t.TransformPoint(new Vector3(0f, 0.7f, 0f)), true);
                }
            }
        }

        // Ortalýkta durup etrafa bakýlacak birkaç nokta
        AddSpot(new Vector3(-2.6f, 0f, -3.2f), new Vector3(-2.6f, 1.5f, 0.5f), false);
        AddSpot(new Vector3(2.6f, 0f, -3.4f), new Vector3(2.6f, 1.5f, 0.5f), false);
        AddSpot(new Vector3(-1.6f, 0f, -1.7f), new Vector3(0f, 1.5f, 0f), false);   // yan kabini izleyen
        AddSpot(new Vector3(1.6f, 0f, -1.7f), new Vector3(0f, 1.5f, 0f), false);
    }

    void AddObstacle(Transform t, float pad)
    {
        bool any = false;
        Bounds b = default;
        foreach (var r in t.GetComponentsInChildren<Renderer>())
        {
            if (!any) { b = r.bounds; any = true; }
            else b.Encapsulate(r.bounds);
        }
        if (!any) return;
        obstacles.Add(Rect.MinMaxRect(b.min.x - pad, b.min.z - pad, b.max.x + pad, b.max.z + pad));
    }

    void AddSpot(Vector3 p, Vector3 look, bool playing)
    {
        p.y = 0f;
        var xz = new Vector2(p.x, p.z);
        if (xz.x < areaMin.x || xz.x > areaMax.x || xz.y < areaMin.y || xz.y > areaMax.y) return;
        if (InKeepOut(xz)) return;
        foreach (var o in obstacles) if (o.Contains(xz)) return;
        spots.Add(new Spot { pos = p, face = look, playing = playing });
    }

    bool InKeepOut(Vector2 p) => p.x > keepOutMin.x && p.x < keepOutMax.x && p.y > keepOutMin.y && p.y < keepOutMax.y;

    bool WalkableStart(out Vector3 p)
    {
        for (int k = 0; k < 20; k++)
        {
            var c = new Vector2(Range(areaMin.x, areaMax.x), Range(areaMin.y, areaMax.y));
            if (InKeepOut(c)) continue;
            bool blocked = false;
            foreach (var o in obstacles) if (o.Contains(c)) { blocked = true; break; }
            if (blocked) continue;
            p = new Vector3(c.x, 0f, c.y);
            return true;
        }
        p = Vector3.zero;
        return false;
    }

    Visitor BuildVisitor(int index, Color shirt, Color skin)
    {
        var v = new Visitor { speed = walkSpeed * Range(0.85f, 1.15f), phase = Range(0f, 6.28f) };
        v.root = new GameObject("Visitor " + (index + 1)).transform;
        v.root.SetParent(transform, false);

        var pants = new Color(0.12f, 0.12f, 0.18f);
        var hair = index % 3 == 0 ? new Color(0.35f, 0.2f, 0.1f) : new Color(0.07f, 0.06f, 0.05f);
        v.legL = Pivot(v.root, "LegL", new Vector3(-0.1f, 0.86f, 0f));
        Part(v.legL, new Vector3(0f, -0.43f, 0f), new Vector3(0.15f, 0.86f, 0.18f), pants);
        Part(v.legL, new Vector3(0f, -0.84f, 0.05f), new Vector3(0.16f, 0.06f, 0.26f), new Color(0.9f, 0.9f, 0.9f));
        v.legR = Pivot(v.root, "LegR", new Vector3(0.1f, 0.86f, 0f));
        Part(v.legR, new Vector3(0f, -0.43f, 0f), new Vector3(0.15f, 0.86f, 0.18f), pants);
        Part(v.legR, new Vector3(0f, -0.84f, 0.05f), new Vector3(0.16f, 0.06f, 0.26f), new Color(0.9f, 0.9f, 0.9f));

        Part(v.root, new Vector3(0f, 1.15f, 0f), new Vector3(0.44f, 0.58f, 0.24f), shirt);
        Part(v.root, new Vector3(0f, 1.6f, 0f), new Vector3(0.23f, 0.27f, 0.23f), skin);
        Part(v.root, new Vector3(0f, 1.76f, -0.01f), new Vector3(0.25f, 0.08f, 0.26f), hair);
        if (index % 2 == 1) Part(v.root, new Vector3(0f, 1.75f, 0.1f), new Vector3(0.26f, 0.03f, 0.14f), shirt);   // þapka siperi

        v.armL = Pivot(v.root, "ArmL", new Vector3(-0.28f, 1.38f, 0f));
        Part(v.armL, new Vector3(0f, -0.25f, 0f), new Vector3(0.11f, 0.5f, 0.11f), shirt);
        Part(v.armL, new Vector3(0f, -0.53f, 0f), new Vector3(0.1f, 0.1f, 0.1f), skin);
        v.armR = Pivot(v.root, "ArmR", new Vector3(0.28f, 1.38f, 0f));
        Part(v.armR, new Vector3(0f, -0.25f, 0f), new Vector3(0.11f, 0.5f, 0.11f), shirt);
        Part(v.armR, new Vector3(0f, -0.53f, 0f), new Vector3(0.1f, 0.1f, 0.1f), skin);
        return v;
    }

    static Transform Pivot(Transform parent, string name, Vector3 pos)
    {
        var t = new GameObject(name).transform;
        t.SetParent(parent, false);
        t.localPosition = pos;
        return t;
    }

    void Part(Transform parent, Vector3 pos, Vector3 size, Color c)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        ColorFighterUtil.Kill(go.GetComponent<Collider>());
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localScale = size;
        go.GetComponent<Renderer>().sharedMaterial = mat(c);
    }

    #endregion

    #region Davranýþ

    void Update()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f) return;
        float t = Time.time;
        foreach (var v in visitors)
        {
            if (v.atSpot) Idle(v, t, dt);
            else Walk(v, t, dt);
        }
    }

    void Idle(Visitor v, float t, float dt)
    {
        var s = v.target;
        Face(v, s != null ? s.face - v.root.position : Vector3.forward, dt, 6f);

        bool playing = s != null && s.playing;
        if (playing)
        {
            // Kollar panelde, tuþlara basýyor
            float mash = Mathf.Sin(t * 14f + v.phase) * 7f;
            v.armL.localRotation = Quaternion.Euler(-62f + mash, 0f, 6f);
            v.armR.localRotation = Quaternion.Euler(-62f - Mathf.Abs(mash), 0f, -6f);
            v.root.position = new Vector3(v.root.position.x, Mathf.Abs(Mathf.Sin(t * 2f + v.phase)) * 0.015f, v.root.position.z);
        }
        else
        {
            v.armL.localRotation = Quaternion.Euler(Mathf.Sin(t * 1.3f + v.phase) * 3f, 0f, 4f);
            v.armR.localRotation = Quaternion.Euler(-Mathf.Sin(t * 1.3f + v.phase) * 3f, 0f, -4f);
        }
        v.legL.localRotation = Quaternion.identity;
        v.legR.localRotation = Quaternion.identity;

        v.stateTime -= dt;
        if (v.stateTime <= 0f)
        {
            var next = FreeSpot(s);
            var path = next != null ? FindPath(new Vector2(v.root.position.x, v.root.position.z), new Vector2(next.pos.x, next.pos.z)) : null;
            if (path != null)
            {
                if (s != null) s.taken = null;
                next.taken = v;
                v.target = next;
                v.path = path;
                v.pathIndex = 0;
                v.atSpot = false;
            }
            else v.stateTime = Range(1f, 3f);
        }
    }

    void Walk(Visitor v, float t, float dt)
    {
        var s = v.target;
        if (s == null) { v.atSpot = true; v.stateTime = 1f; return; }

        Vector3 pos = v.root.position;
        Vector2 p = new Vector2(pos.x, pos.z);
        Vector2 goal = new Vector2(s.pos.x, s.pos.z);
        Vector2 to = goal - p;
        float dist = to.magnitude;
        if (dist < 0.06f)
        {
            v.root.position = new Vector3(goal.x, 0f, goal.y);
            v.atSpot = true;
            v.stateTime = s.playing ? Range(6f, 16f) : Range(2f, 5f);
            return;
        }

        // Rota üzerindeki bir sonraki noktaya git; önü açýksa ileriki noktalara doðrudan kestir
        if (v.path == null || v.path.Count == 0) v.path = FindPath(p, goal) ?? new List<Vector2> { goal };
        while (v.pathIndex < v.path.Count - 1 && LineClear(p, v.path[v.pathIndex + 1])) v.pathIndex++;
        Vector2 next = v.path[Mathf.Min(v.pathIndex, v.path.Count - 1)];
        if ((next - p).sqrMagnitude < 0.01f && v.pathIndex < v.path.Count - 1) { v.pathIndex++; next = v.path[v.pathIndex]; }
        Vector2 dir = next - p;
        if (dir.sqrMagnitude > 1e-6f) dir.Normalize();

        // Diðer ziyaretçilerle mesafe
        foreach (var o in visitors)
        {
            if (o == v) continue;
            Vector2 op = new Vector2(o.root.position.x, o.root.position.z);
            Vector2 d = p - op;
            float m = d.magnitude;
            if (m > 0.001f && m < 0.6f) dir += d / m * (0.6f - m) * 1.5f;
        }
        if (dir.sqrMagnitude > 1e-6f) dir.Normalize();

        Vector2 np = p + dir * v.speed * dt;
        np.x = Mathf.Clamp(np.x, areaMin.x, areaMax.x);
        np.y = Mathf.Clamp(np.y, areaMin.y, areaMax.y);
        // Engelin içine girme: girecekse duvar boyunca kay (x ya da z bileþeniyle)
        if (!IsBlocked(np) || IsBlocked(p)) p = np;
        else if (!IsBlocked(new Vector2(np.x, p.y))) p = new Vector2(np.x, p.y);
        else if (!IsBlocked(new Vector2(p.x, np.y))) p = new Vector2(p.x, np.y);

        // Ýlerleyemiyorsa rotayý baþtan hesapla
        if ((p - v.lastPos).sqrMagnitude < 0.0004f) v.stuckTime += dt; else v.stuckTime = 0f;
        v.lastPos = p;
        if (v.stuckTime > 1.5f)
        {
            v.stuckTime = 0f;
            v.path = FindPath(p, goal);
            v.pathIndex = 0;
            if (v.path == null) { v.atSpot = true; v.stateTime = 0.5f; return; }   // ulaþýlamýyor: yeni hedef seçsin
        }

        float cycle = t * 7.5f * v.speed + v.phase;
        float bob = Mathf.Abs(Mathf.Sin(cycle)) * 0.04f;
        v.root.position = new Vector3(p.x, bob, p.y);
        Face(v, new Vector3(dir.x, 0f, dir.y), dt, 8f);

        float swing = Mathf.Sin(cycle) * 28f;
        v.legL.localRotation = Quaternion.Euler(swing, 0f, 0f);
        v.legR.localRotation = Quaternion.Euler(-swing, 0f, 0f);
        v.armL.localRotation = Quaternion.Euler(-swing * 0.8f, 0f, 4f);
        v.armR.localRotation = Quaternion.Euler(swing * 0.8f, 0f, -4f);
    }

    void Face(Visitor v, Vector3 dir, float dt, float speed)
    {
        dir.y = 0f;
        if (dir.sqrMagnitude < 1e-6f) return;
        float want = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
        v.heading = Mathf.LerpAngle(v.heading, want, 1f - Mathf.Exp(-speed * dt));
        v.root.rotation = Quaternion.Euler(0f, v.heading, 0f);
    }

    #region Yol bulma

    void BuildGrid()
    {
        gw = Mathf.CeilToInt((areaMax.x - areaMin.x) / Cell) + 1;
        gh = Mathf.CeilToInt((areaMax.y - areaMin.y) / Cell) + 1;
        blocked = new bool[gw, gh];
        for (int x = 0; x < gw; x++)
            for (int z = 0; z < gh; z++)
            {
                var c = CellCenter(x, z);
                foreach (var o in obstacles) if (o.Contains(c)) { blocked[x, z] = true; break; }
            }
    }

    Vector2 CellCenter(int x, int z) => new Vector2(areaMin.x + x * Cell, areaMin.y + z * Cell);

    void CellOf(Vector2 p, out int x, out int z)
    {
        x = Mathf.Clamp(Mathf.RoundToInt((p.x - areaMin.x) / Cell), 0, gw - 1);
        z = Mathf.Clamp(Mathf.RoundToInt((p.y - areaMin.y) / Cell), 0, gh - 1);
    }

    bool IsBlocked(Vector2 p)
    {
        foreach (var o in obstacles) if (o.Contains(p)) return true;
        return false;
    }

    bool LineClear(Vector2 a, Vector2 b)
    {
        float len = Vector2.Distance(a, b);
        int steps = Mathf.Max(1, Mathf.CeilToInt(len / 0.1f));
        bool startBlocked = IsBlocked(a);
        for (int i = 1; i <= steps; i++)
        {
            var q = Vector2.Lerp(a, b, i / (float)steps);
            if (IsBlocked(q) && !(startBlocked && Vector2.Distance(q, a) < 0.4f)) return false;
        }
        return true;
    }

    /// <summary>Izgara üzerinde A*: engellerin etrafýndan dolanan rota (hücre merkezleri + tam hedef).</summary>
    List<Vector2> FindPath(Vector2 from, Vector2 to)
    {
        if (blocked == null) return new List<Vector2> { to };
        CellOf(from, out int sx, out int sz);
        CellOf(to, out int gx, out int gz);
        int N = gw * gh, start = sx + sz * gw, goal = gx + gz * gw;
        var g = new float[N];
        var came = new int[N];
        var closed = new bool[N];
        for (int i = 0; i < N; i++) { g[i] = float.MaxValue; came[i] = -1; }
        g[start] = 0f;
        var open = new List<int> { start };
        while (open.Count > 0)
        {
            int best = 0;
            float bestF = float.MaxValue;
            for (int i = 0; i < open.Count; i++)
            {
                int c = open[i];
                float f = g[c] + Heur(c % gw, c / gw, gx, gz);
                if (f < bestF) { bestF = f; best = i; }
            }
            int cur = open[best];
            open.RemoveAt(best);
            if (cur == goal) break;
            if (closed[cur]) continue;
            closed[cur] = true;
            int cx = cur % gw, cz = cur / gw;
            for (int dx = -1; dx <= 1; dx++)
                for (int dz = -1; dz <= 1; dz++)
                {
                    if (dx == 0 && dz == 0) continue;
                    int nx = cx + dx, nz = cz + dz;
                    if (nx < 0 || nz < 0 || nx >= gw || nz >= gh) continue;
                    int ni = nx + nz * gw;
                    if (closed[ni]) continue;
                    bool free = !blocked[nx, nz] || ni == goal || ni == start;
                    if (!free) continue;
                    if (dx != 0 && dz != 0 && (blocked[cx + dx, cz] || blocked[cx, cz + dz])) continue;   // köþeden kesme
                    float ng = g[cur] + (dx != 0 && dz != 0 ? 1.4142f : 1f);
                    if (ng < g[ni]) { g[ni] = ng; came[ni] = cur; open.Add(ni); }
                }
        }
        if (came[goal] < 0 && goal != start) return null;

        var path = new List<Vector2>();
        for (int c = goal; c != start && c >= 0; c = came[c]) path.Add(CellCenter(c % gw, c / gw));
        path.Reverse();
        if (path.Count > 0) path[path.Count - 1] = to; else path.Add(to);
        return path;
    }

    static float Heur(int x, int z, int gx, int gz)
    {
        int dx = Mathf.Abs(x - gx), dz = Mathf.Abs(z - gz);
        return Mathf.Max(dx, dz) + 0.4142f * Mathf.Min(dx, dz);
    }

    #endregion

    Spot FreeSpot(Spot except)
    {
        var free = new List<Spot>();
        foreach (var s in spots) if (s.taken == null && s != except) free.Add(s);
        if (free.Count == 0) return null;
        // Çoðunlukla oynanabilir makineleri seç
        var playable = free.FindAll(s => s.playing);
        var pool = playable.Count > 0 && rng.NextDouble() < 0.8 ? playable : free;
        return pool[rng.Next(pool.Count)];
    }

    float Range(float a, float b) => a + (float)rng.NextDouble() * (b - a);

    #endregion
}