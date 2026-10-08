using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Boks ringine ek dövüş sahneleri: gece çatı katı, dojo, gece pazarı.
/// Her biri FightArena altında kendi grubunda kurulur; StageSwitcher hangisinin açık olacağını seçer.
/// Dövüşçüler hep z = 0 düzleminde, y = 0'da durur; kamera z ≈ -6.6 .. -8.7 arasından bakar.
/// </summary>
public partial class ArcadeBootstrap
{
    #region Ek sahneler

    readonly Dictionary<string, Texture2D> stageTexCache = new Dictionary<string, Texture2D>();

    void SetupExtraStages(Transform arena)
    {
        KillChildren(arena, StageSwitcher.Groups[1], StageSwitcher.Groups[2], StageSwitcher.Groups[3]);
        var roof = Group(StageSwitcher.Groups[1], Vector3.zero, arena);
        BuildRooftop(roof);
        var dojo = Group(StageSwitcher.Groups[2], Vector3.zero, arena);
        BuildDojo(dojo);
        var market = Group(StageSwitcher.Groups[3], Vector3.zero, arena);
        BuildMarket(market);
        roof.gameObject.SetActive(false);
        dojo.gameObject.SetActive(false);
        market.gameObject.SetActive(false);
    }

    /// <summary>Dokulu materyal (lit ya da unlit). Aynı doku farklı tekrar oranlarıyla kullanılabilir.</summary>
    Material StageMat(string matKey, string texKey, System.Func<Texture2D> make, Vector2 tiling, bool lit, Color tint)
    {
        var shader = lit ? litShader : unlitShader;
        var m = Asset("Stage_" + matKey + ".mat", () => new Material(shader));
        m.shader = shader;
        m.color = tint;
        if (!stageTexCache.TryGetValue(texKey, out var tex) || tex == null)
        {
            tex = Asset("Stage_" + texKey + ".asset", make);
            stageTexCache[texKey] = tex;
        }
        m.mainTexture = tex;
        m.mainTextureScale = tiling;
        MarkDirty(m);
        return m;
    }

    /// <summary>Oyuncu renginde boyanan bayrak (MatchManager "BannerP1/BannerP2" adlı parçaları boyar).</summary>
    void PlayerFlag(Transform g, Vector3 basePos, float poleH, bool p1)
    {
        var pole = Mat(new Color(0.3f, 0.3f, 0.33f), true);
        Prim(PrimitiveType.Cube, g, "FlagPole", basePos + new Vector3(0f, poleH * 0.5f, 0f), new Vector3(0.06f, poleH, 0.06f), pole);
        float dir = p1 ? 1f : -1f;   // bayrak sahnenin ortasına doğru açılır
        Prim(PrimitiveType.Cube, g, p1 ? "BannerP1" : "BannerP2", basePos + new Vector3(dir * 0.42f, poleH - 0.4f, 0f), new Vector3(0.78f, 0.62f, 0.03f), Mat(p1 ? p1Color : p2Color, true));
        Prim(PrimitiveType.Cube, g, "FlagStripe", basePos + new Vector3(dir * 0.42f, poleH - 0.4f, -0.02f), new Vector3(0.78f, 0.1f, 0.01f), Mat(new Color(0.95f, 0.95f, 0.95f), true));
        NeonSign(g, "FlagTag", p1 ? "1P" : "2P", basePos + new Vector3(dir * 0.42f, poleH - 0.25f, -0.03f), 0.0025f, 300f, 120f, Color.white, FontStyle.Bold);
    }

    /// <summary>Bina blokları (pencereli cephe dokusu, unlit = gece ışıl ışıl). z: en yakın ön cephe.</summary>
    void BuildSkyline(Transform g, float z, float xMin, float xMax, float minH, float maxH, int seed, float tint)
    {
        var rng = new System.Random(seed);
        float x = xMin;
        int n = 0;
        while (x < xMax)
        {
            float w = 3f + (float)rng.Next(0, 4);            // 3..6 m
            float h = minH + (float)rng.NextDouble() * (maxH - minH);
            h = Mathf.Round(h);
            bool warm = rng.Next(2) == 0;
            float depth = 3f + (float)rng.NextDouble() * 2f;
            float bottom = -12f;
            var m = StageMat("City" + (warm ? "A" : "B") + "_" + w + "x" + (h - bottom) + "_" + tint.ToString("0.00"),
                warm ? "CityWinA" : "CityWinB", warm ? (System.Func<Texture2D>)CityWindowsWarm : CityWindowsCool,
                new Vector2(w / 2f, (h - bottom) / 4f), false, new Color(tint, tint, tint));
            float zz = z + (float)rng.NextDouble() * 2.5f + depth * 0.5f;   // z = ön cephe
            Prim(PrimitiveType.Cube, g, "Building", new Vector3(x + w * 0.5f, (h + bottom) * 0.5f, zz), new Vector3(w, h - bottom, depth), m);
            // Çatı kenarı
            Prim(PrimitiveType.Cube, g, "BuildingTop", new Vector3(x + w * 0.5f, h + 0.08f, zz), new Vector3(w + 0.1f, 0.16f, depth + 0.1f), Mat(new Color(0.05f * tint * 2f, 0.05f * tint * 2f, 0.08f * tint * 2f), false));
            x += w + 0.4f + (float)rng.NextDouble() * 1.2f;
            n++;
        }
    }

    #endregion

    #region Çatı katı (gece)

    void BuildRooftop(Transform g)
    {
        var amb = g.gameObject.AddComponent<ArcadeAmbience>();

        // Zemin: beton çatı (z -4 .. 6) + arkada korkuluk duvarı
        Prim(PrimitiveType.Cube, g, "RoofFloor", new Vector3(0f, -0.1f, 1f), new Vector3(40f, 0.2f, 10f),
            StageMat("RoofFloor", "RoofConcrete", RoofConcrete, new Vector2(10f, 2.5f), true, Color.white));
        var wallM = Mat(new Color(0.24f, 0.24f, 0.28f), true);
        Prim(PrimitiveType.Cube, g, "Parapet", new Vector3(0f, 0.38f, 6.1f), new Vector3(40f, 0.96f, 0.3f), wallM);
        Prim(PrimitiveType.Cube, g, "ParapetCap", new Vector3(0f, 0.88f, 6.1f), new Vector3(40.2f, 0.07f, 0.42f), Mat(new Color(0.42f, 0.42f, 0.46f), true));
        // Zemindeki boya çizgileri / helikopter pisti işareti gibi şerit
        var paint = Mat(new Color(0.75f, 0.62f, 0.15f), true);
        Prim(PrimitiveType.Cube, g, "PaintL", new Vector3(-4.7f, 0.002f, 1.2f), new Vector3(0.12f, 0.01f, 5.5f), paint);
        Prim(PrimitiveType.Cube, g, "PaintR", new Vector3(4.7f, 0.002f, 1.2f), new Vector3(0.12f, 0.01f, 5.5f), paint);

        // Gökyüzü: degrade arka plan + ay + yıldızlar
        var sky = Prim(PrimitiveType.Quad, g, "Sky", new Vector3(0f, 8f, 30f), new Vector3(90f, 32f, 1f),
            StageMat("NightSky", "NightSky", NightSky, Vector2.one, false, Color.white));
        sky.localRotation = Quaternion.identity;
        Prim(PrimitiveType.Sphere, g, "Moon", new Vector3(-9f, 6.6f, 29f), Vector3.one * 2.2f, Mat(new Color(0.96f, 0.94f, 0.82f), false));
        Prim(PrimitiveType.Sphere, g, "MoonShade", new Vector3(-8.55f, 6.85f, 28.2f), Vector3.one * 1.7f, Mat(new Color(0.06f, 0.06f, 0.16f), false));
        var rng = new System.Random(21);
        var starM = Mat(new Color(0.9f, 0.9f, 1f), false);
        for (int i = 0; i < 46; i++)
        {
            float sx = (float)rng.NextDouble() * 50f - 25f, sy = 3.2f + (float)rng.NextDouble() * 6.5f;
            float s = 0.05f + (float)rng.NextDouble() * 0.07f;
            Prim(PrimitiveType.Cube, g, "Star", new Vector3(sx, sy, 29.5f), new Vector3(s, s, 0.02f), starM);
        }

        // Şehir silueti: önde yakın binalar, arkada daha soluk uzak binalar
        BuildSkyline(g, 23f, -30f, 30f, 4f, 9f, 7, 0.55f);
        BuildSkyline(g, 14f, -26f, 26f, -1f, 4f, 3, 1f);

        // Arka binanın üstünde neon reklam panosu
        var boardM = Mat(new Color(0.04f, 0.03f, 0.07f), true);
        var legM = Mat(new Color(0.18f, 0.18f, 0.22f), true);
        Prim(PrimitiveType.Cube, g, "BillboardBuilding", new Vector3(3.5f, -5.3f, 14.2f), new Vector3(7f, 13.4f, 2f),
            StageMat("BillboardBuilding", "CityWinB", CityWindowsCool, new Vector2(3.5f, 3.35f), false, Color.white));
        Prim(PrimitiveType.Cube, g, "BillboardLegL", new Vector3(1.2f, 2.6f, 13.2f), new Vector3(0.15f, 2.4f, 0.15f), legM);
        Prim(PrimitiveType.Cube, g, "BillboardLegR", new Vector3(5.8f, 2.6f, 13.2f), new Vector3(0.15f, 2.4f, 0.15f), legM);
        Prim(PrimitiveType.Cube, g, "Billboard", new Vector3(3.5f, 4.1f, 13.1f), new Vector3(5.6f, 1.6f, 0.12f), boardM);
        var tube = NeonFrame(g, "BillboardNeon", new Vector3(3.5f, 4.1f, 13.0f), new Vector2(5.4f, 1.4f), new Color(0.2f, 0.95f, 1f), 0f);
        var bt = NeonSign(g, "BillboardText", "NEON NIGHTS", new Vector3(3.5f, 4.18f, 12.98f), 0.0042f, 1250f, 200f, new Color(1f, 0.3f, 0.75f), FontStyle.BoldAndItalic);
        var bs = NeonSign(g, "BillboardSub", "OPEN ALL NIGHT", new Vector3(3.5f, 3.68f, 12.98f), 0.0022f, 1600f, 120f, new Color(1f, 0.9f, 0.3f), FontStyle.Bold);
        amb.flickerTexts = new[] { bt };
        amb.flickerTubes = tube;
        amb.blinkTexts = new[] { bs };

        // Su deposu (sol arka)
        var wood = Mat(new Color(0.36f, 0.22f, 0.13f), true);
        var iron = Mat(new Color(0.15f, 0.15f, 0.17f), true);
        Vector3 wt = new Vector3(-5.2f, 0f, 4.6f);
        foreach (float lx in new[] { -0.55f, 0.55f })
            foreach (float lz in new[] { -0.45f, 0.45f })
                Prim(PrimitiveType.Cube, g, "TowerLeg", wt + new Vector3(lx, 0.8f, lz), new Vector3(0.09f, 1.6f, 0.09f), iron);
        Prim(PrimitiveType.Cube, g, "TowerDeck", wt + new Vector3(0f, 1.62f, 0f), new Vector3(1.5f, 0.06f, 1.3f), iron);
        Prim(PrimitiveType.Cylinder, g, "TowerTank", wt + new Vector3(0f, 2.3f, 0f), new Vector3(1.3f, 0.65f, 1.3f), wood);
        Prim(PrimitiveType.Cylinder, g, "TowerBand1", wt + new Vector3(0f, 2.0f, 0f), new Vector3(1.33f, 0.03f, 1.33f), iron);
        Prim(PrimitiveType.Cylinder, g, "TowerBand2", wt + new Vector3(0f, 2.6f, 0f), new Vector3(1.33f, 0.03f, 1.33f), iron);
        Prim(PrimitiveType.Cylinder, g, "TowerRoof", wt + new Vector3(0f, 3.0f, 0f), new Vector3(1.4f, 0.06f, 1.4f), iron);
        Prim(PrimitiveType.Cylinder, g, "TowerRoof2", wt + new Vector3(0f, 3.12f, 0f), new Vector3(0.8f, 0.07f, 0.8f), iron);
        Prim(PrimitiveType.Cylinder, g, "TowerRoof3", wt + new Vector3(0f, 3.24f, 0f), new Vector3(0.3f, 0.06f, 0.3f), iron);

        // Klima üniteleri (sağ arka)
        var acM = Mat(new Color(0.55f, 0.56f, 0.6f), true);
        var grill = Mat(new Color(0.12f, 0.12f, 0.14f), true);
        foreach (var p in new[] { new Vector3(4.4f, 0f, 4.4f), new Vector3(6.1f, 0f, 3.6f) })
        {
            Prim(PrimitiveType.Cube, g, "ACUnit", p + new Vector3(0f, 0.5f, 0f), new Vector3(1.3f, 1.0f, 1.0f), acM);
            Prim(PrimitiveType.Cylinder, g, "ACFan", p + new Vector3(0f, 1.01f, 0f), new Vector3(0.8f, 0.02f, 0.8f), grill);
            Prim(PrimitiveType.Cube, g, "ACVent", p + new Vector3(0f, 0.45f, -0.505f), new Vector3(1.0f, 0.6f, 0.01f), grill);
        }

        // Anten: tepesinde yanıp sönen kırmızı ışık
        Prim(PrimitiveType.Cube, g, "Antenna", new Vector3(7.2f, 1.9f, 5.4f), new Vector3(0.07f, 3.8f, 0.07f), iron);
        Prim(PrimitiveType.Cube, g, "AntennaBar", new Vector3(7.2f, 3.2f, 5.4f), new Vector3(0.7f, 0.04f, 0.04f), iron);
        var bulb = Prim(PrimitiveType.Sphere, g, "AntennaLight", new Vector3(7.2f, 3.85f, 5.4f), Vector3.one * 0.16f, Mat(new Color(1f, 0.1f, 0.1f), false)).GetComponent<Renderer>();
        amb.blinkTubes = new[] { bulb };

        // Oyuncu bayrakları
        PlayerFlag(g, new Vector3(-6.4f, 0f, 5.5f), 3.1f, true);
        PlayerFlag(g, new Vector3(6.4f, 0f, 5.5f), 3.1f, false);

        // Işık: soğuk ay ışığı + neon yansıması
        amb.lights = new[]
        {
            StageLight(g, "MoonLight", new Vector3(-2f, 5f, -2.5f), new Color(0.55f, 0.65f, 1f), 1.4f, 15f),
            StageLight(g, "NeonGlow", new Vector3(3.5f, 3.5f, 9f), new Color(1f, 0.35f, 0.8f), 1.6f, 9f),
            StageLight(g, "RoofLamp", new Vector3(-5.2f, 1.4f, 3.2f), new Color(1f, 0.75f, 0.4f), 1.2f, 5f),
        };
    }

    #endregion

    #region Dojo

    void BuildDojo(Transform g)
    {
        var amb = g.gameObject.AddComponent<ArcadeAmbience>();
        var darkWood = Mat(new Color(0.2f, 0.11f, 0.06f), true);
        var midWood = Mat(new Color(0.42f, 0.26f, 0.14f), true);

        // Ahşap döşeme
        Prim(PrimitiveType.Cube, g, "DojoFloor", new Vector3(0f, -0.1f, 2f), new Vector3(40f, 0.2f, 12f),
            StageMat("DojoFloor", "WoodPlanks", WoodPlanks, new Vector2(13f, 4f), true, Color.white));
        // Dövüş alanını çevreleyen kırmızı-beyaz minder sınırı
        Prim(PrimitiveType.Cube, g, "MatBorderBack", new Vector3(0f, 0.004f, 1.6f), new Vector3(10.4f, 0.01f, 0.12f), Mat(new Color(0.75f, 0.12f, 0.1f), true));
        Prim(PrimitiveType.Cube, g, "MatBorderFront", new Vector3(0f, 0.004f, -1.6f), new Vector3(10.4f, 0.01f, 0.12f), Mat(new Color(0.75f, 0.12f, 0.1f), true));

        // Arka duvar: arkadan aydınlatılmış shoji panelleri + ahşap direkler
        const float wz = 7.6f;
        Prim(PrimitiveType.Cube, g, "Shoji", new Vector3(0f, 1.9f, wz), new Vector3(40f, 3.8f, 0.1f),
            StageMat("Shoji", "Shoji", ShojiPaper, new Vector2(13.33f, 1.4f), false, new Color(1f, 0.88f, 0.68f)));
        Prim(PrimitiveType.Cube, g, "Lintel", new Vector3(0f, 3.92f, wz - 0.1f), new Vector3(40f, 0.28f, 0.22f), darkWood);
        Prim(PrimitiveType.Cube, g, "UpperWall", new Vector3(0f, 5.2f, wz + 0.05f), new Vector3(40f, 2.4f, 0.1f), Mat(new Color(0.55f, 0.45f, 0.32f), true));
        Prim(PrimitiveType.Cube, g, "Baseboard", new Vector3(0f, 0.1f, wz - 0.08f), new Vector3(40f, 0.2f, 0.12f), darkWood);
        for (float x = -18f; x <= 18.01f; x += 3f)
            Prim(PrimitiveType.Cube, g, "Post", new Vector3(x, 2.6f, wz - 0.12f), new Vector3(0.26f, 5.2f, 0.26f), darkWood);

        // Ortada asılı parşömen (soyut fırça darbeleri) + üstünde dojo levhası
        Prim(PrimitiveType.Cube, g, "ScrollPaper", new Vector3(0f, 2.15f, wz - 0.12f), new Vector3(0.9f, 2.1f, 0.02f), Mat(new Color(0.93f, 0.89f, 0.78f), true));
        Prim(PrimitiveType.Cube, g, "ScrollRodTop", new Vector3(0f, 3.22f, wz - 0.14f), new Vector3(1.02f, 0.06f, 0.06f), darkWood);
        Prim(PrimitiveType.Cube, g, "ScrollRodBottom", new Vector3(0f, 1.08f, wz - 0.14f), new Vector3(1.02f, 0.06f, 0.06f), darkWood);
        var ink = Mat(new Color(0.06f, 0.05f, 0.05f), true);
        var s1 = Prim(PrimitiveType.Cube, g, "Ink", new Vector3(0f, 2.7f, wz - 0.14f), new Vector3(0.5f, 0.07f, 0.01f), ink); s1.localRotation = Quaternion.Euler(0f, 0f, -6f);
        Prim(PrimitiveType.Cube, g, "Ink", new Vector3(0.02f, 2.25f, wz - 0.14f), new Vector3(0.07f, 0.85f, 0.01f), ink);
        var s3 = Prim(PrimitiveType.Cube, g, "Ink", new Vector3(-0.14f, 1.95f, wz - 0.14f), new Vector3(0.3f, 0.06f, 0.01f), ink); s3.localRotation = Quaternion.Euler(0f, 0f, 35f);
        var s4 = Prim(PrimitiveType.Cube, g, "Ink", new Vector3(0.16f, 1.95f, wz - 0.14f), new Vector3(0.3f, 0.06f, 0.01f), ink); s4.localRotation = Quaternion.Euler(0f, 0f, -35f);
        Prim(PrimitiveType.Cube, g, "Seal", new Vector3(0.25f, 1.4f, wz - 0.14f), new Vector3(0.1f, 0.1f, 0.01f), Mat(new Color(0.8f, 0.1f, 0.08f), true));
        Prim(PrimitiveType.Cube, g, "Plaque", new Vector3(0f, 3.92f, wz - 0.24f), new Vector3(2.6f, 0.42f, 0.05f), Mat(new Color(0.12f, 0.07f, 0.04f), true));
        var plaque = NeonSign(g, "PlaqueText", "COLOR  DOJO", new Vector3(0f, 3.92f, wz - 0.28f), 0.0024f, 1000f, 150f, new Color(1f, 0.8f, 0.35f), FontStyle.Bold);

        // Kağıt fenerler
        var lights = new List<Light>();
        Color[] lantern = { new Color(1f, 0.38f, 0.25f), new Color(1f, 0.85f, 0.55f) };
        float[] lx = { -3.4f, 3.4f, -8f, 8f };
        for (int i = 0; i < lx.Length; i++)
        {
            var p = new Vector3(lx[i], 3.0f, 5.6f);
            Prim(PrimitiveType.Cube, g, "LanternCord", p + new Vector3(0f, 1.2f, 0f), new Vector3(0.02f, 2f, 0.02f), darkWood);
            Prim(PrimitiveType.Cylinder, g, "LanternCapT", p + new Vector3(0f, 0.36f, 0f), new Vector3(0.32f, 0.04f, 0.32f), darkWood);
            Prim(PrimitiveType.Cylinder, g, "LanternCapB", p + new Vector3(0f, -0.36f, 0f), new Vector3(0.32f, 0.04f, 0.32f), darkWood);
            Prim(PrimitiveType.Sphere, g, "Lantern", p, new Vector3(0.55f, 0.75f, 0.55f), Mat(lantern[i % 2], false));
            if (i < 2) lights.Add(StageLight(g, "LanternLight", p + new Vector3(0f, -0.2f, -0.8f), new Color(1f, 0.7f, 0.4f), 1.5f, 7f));
        }

        // Taiko davulu (sol) ve silah rafı (sağ)
        var drumP = new Vector3(-5.6f, 0f, 5.4f);
        Prim(PrimitiveType.Cube, g, "DrumStandL", drumP + new Vector3(-0.45f, 0.4f, 0f), new Vector3(0.08f, 0.8f, 0.5f), darkWood);
        Prim(PrimitiveType.Cube, g, "DrumStandR", drumP + new Vector3(0.45f, 0.4f, 0f), new Vector3(0.08f, 0.8f, 0.5f), darkWood);
        var drum = Prim(PrimitiveType.Cylinder, g, "Drum", drumP + new Vector3(0f, 1.05f, 0f), new Vector3(1.0f, 0.38f, 1.0f), Mat(new Color(0.5f, 0.18f, 0.08f), true));
        drum.localRotation = Quaternion.Euler(90f, 0f, 0f);
        var face = Prim(PrimitiveType.Cylinder, g, "DrumFace", drumP + new Vector3(0f, 1.05f, -0.39f), new Vector3(0.86f, 0.01f, 0.86f), Mat(new Color(0.9f, 0.84f, 0.68f), true));
        face.localRotation = Quaternion.Euler(90f, 0f, 0f);
        var mon = Prim(PrimitiveType.Cylinder, g, "DrumMon", drumP + new Vector3(0f, 1.05f, -0.4f), new Vector3(0.3f, 0.01f, 0.3f), Mat(new Color(0.15f, 0.1f, 0.08f), true));
        mon.localRotation = Quaternion.Euler(90f, 0f, 0f);

        var rackP = new Vector3(5.6f, 0f, 6.9f);
        Prim(PrimitiveType.Cube, g, "RackPostL", rackP + new Vector3(-0.8f, 0.9f, 0f), new Vector3(0.1f, 1.8f, 0.1f), midWood);
        Prim(PrimitiveType.Cube, g, "RackPostR", rackP + new Vector3(0.8f, 0.9f, 0f), new Vector3(0.1f, 1.8f, 0.1f), midWood);
        Prim(PrimitiveType.Cube, g, "RackBar", rackP + new Vector3(0f, 1.55f, 0f), new Vector3(1.7f, 0.08f, 0.12f), midWood);
        Prim(PrimitiveType.Cube, g, "RackBar", rackP + new Vector3(0f, 0.35f, 0f), new Vector3(1.7f, 0.08f, 0.12f), midWood);
        for (int i = 0; i < 5; i++)
        {
            var st = Prim(PrimitiveType.Cube, g, "Staff", rackP + new Vector3(-0.6f + i * 0.3f, 1.0f, -0.1f), new Vector3(0.05f, 2.0f, 0.05f), i % 2 == 0 ? midWood : darkWood);
            st.localRotation = Quaternion.Euler(0f, 0f, 4f - i * 2f);
        }

        // Kenarda izleyen öğrenciler (beyaz gi + siyah kuşak) — KO'da tezahürat yaparlar
        var crowd = new List<Transform>();
        var armsL = new List<Transform>();
        var armsR = new List<Transform>();
        Color gi = new Color(0.92f, 0.92f, 0.88f);
        Color[] skins = { new Color(0.8f, 0.64f, 0.5f), new Color(0.6f, 0.43f, 0.32f), new Color(0.72f, 0.55f, 0.42f), new Color(0.42f, 0.3f, 0.22f) };
        float[] sx = { -3.6f, -2.4f, -1.3f, 1.3f, 2.4f, 3.6f };
        var belt = Mat(new Color(0.06f, 0.06f, 0.07f), true);
        for (int i = 0; i < sx.Length; i++)
        {
            BuildSpectator(g, new Vector3(sx[i], 0f, 6.6f), gi, skins[i % skins.Length], out var root, out var al, out var ar);
            Prim(PrimitiveType.Cube, root, "Belt", new Vector3(0f, 0.86f, 0f), new Vector3(0.48f, 0.07f, 0.28f), belt);
            crowd.Add(root); armsL.Add(al); armsR.Add(ar);
        }
        amb.crowd = crowd.ToArray();
        amb.crowdArmsL = armsL.ToArray();
        amb.crowdArmsR = armsR.ToArray();

        // Oyuncu renginde nobori sancakları
        PlayerFlag(g, new Vector3(-7.2f, 0f, 6.2f), 3.2f, true);
        PlayerFlag(g, new Vector3(7.2f, 0f, 6.2f), 3.2f, false);

        lights.Add(StageLight(g, "WarmFill", new Vector3(0f, 4f, -2f), new Color(1f, 0.86f, 0.65f), 1.0f, 14f));
        amb.lights = lights.ToArray();
        amb.flickerTexts = new[] { plaque };
    }

    #endregion

    #region Gece pazarı

    void BuildMarket(Transform g)
    {
        var amb = g.gameObject.AddComponent<ArcadeAmbience>();

        // Taş döşeli sokak
        Prim(PrimitiveType.Cube, g, "StreetFloor", new Vector3(0f, -0.1f, 4f), new Vector3(44f, 0.2f, 16f),
            StageMat("StreetFloor", "StoneTiles", StoneTiles, new Vector2(11f, 4f), true, Color.white));
        Prim(PrimitiveType.Cube, g, "Curb", new Vector3(0f, 0.06f, 7.4f), new Vector3(44f, 0.12f, 0.3f), Mat(new Color(0.35f, 0.35f, 0.38f), true));

        // Arkadaki binalar + tabelalar
        BuildSkyline(g, 11.5f, -26f, 26f, 6f, 9f, 13, 0.85f);
        var neonTexts = new List<Text>
        {
            NeonSign(g, "SignKaraoke", "KARAOKE", new Vector3(-6.5f, 4.6f, 11.3f), 0.0045f, 900f, 160f, new Color(1f, 0.3f, 0.75f), FontStyle.BoldAndItalic),
            NeonSign(g, "SignHotel", "HOTEL", new Vector3(7.5f, 5.0f, 11.3f), 0.0045f, 700f, 160f, new Color(0.3f, 0.95f, 1f), FontStyle.Bold),
        };
        var open24 = NeonSign(g, "Sign24", "OPEN 24H", new Vector3(0.5f, 4.4f, 11.3f), 0.0032f, 900f, 140f, new Color(0.4f, 1f, 0.45f), FontStyle.Bold);
        amb.flickerTexts = neonTexts.ToArray();

        // Tezgâhlar: çizgili tente, tabela, satıcı, ürünler
        string[] signs = { "RAMEN", "TACOS", "BOBA TEA", "DUMPLINGS" };
        Color[] stripeA = { new Color(0.85f, 0.15f, 0.12f), new Color(0.15f, 0.6f, 0.25f), new Color(0.2f, 0.4f, 0.9f), new Color(0.95f, 0.7f, 0.1f) };
        Color[] signC = { new Color(1f, 0.85f, 0.3f), new Color(1f, 0.5f, 0.2f), new Color(0.7f, 0.5f, 1f), new Color(1f, 1f, 0.9f) };
        float[] stallX = { -6.2f, -2.1f, 2.1f, 6.2f };
        var counterM = Mat(new Color(0.4f, 0.25f, 0.14f), true);
        var postM = Mat(new Color(0.2f, 0.18f, 0.18f), true);
        var vendors = new List<Transform>();
        var vArmsL = new List<Transform>();
        var vArmsR = new List<Transform>();
        Color[] skins = { new Color(0.8f, 0.64f, 0.5f), new Color(0.6f, 0.43f, 0.32f), new Color(0.72f, 0.55f, 0.42f), new Color(0.42f, 0.3f, 0.22f) };
        var bulbs = new List<Renderer>();
        for (int i = 0; i < stallX.Length; i++)
        {
            var p = new Vector3(stallX[i], 0f, 6.0f);
            var awning = StageMat("Awning" + i, "Stripes" + i, MakeStripes(stripeA[i]), new Vector2(4f, 1f), true, Color.white);
            Prim(PrimitiveType.Cube, g, "Counter", p + new Vector3(0f, 0.5f, -0.3f), new Vector3(3.0f, 1.0f, 0.9f), counterM);
            Prim(PrimitiveType.Cube, g, "CounterFront", p + new Vector3(0f, 0.5f, -0.76f), new Vector3(3.0f, 0.7f, 0.02f), Mat(stripeA[i] * 0.7f, true));
            Prim(PrimitiveType.Cube, g, "CounterTop", p + new Vector3(0f, 1.02f, -0.3f), new Vector3(3.1f, 0.05f, 1.0f), Mat(new Color(0.55f, 0.55f, 0.58f), true));
            foreach (float px in new[] { -1.5f, 1.5f })
            {
                Prim(PrimitiveType.Cube, g, "StallPost", p + new Vector3(px, 1.25f, -0.8f), new Vector3(0.07f, 2.5f, 0.07f), postM);
                Prim(PrimitiveType.Cube, g, "StallPost", p + new Vector3(px, 1.4f, 0.5f), new Vector3(0.07f, 2.8f, 0.07f), postM);
            }
            var aw = Prim(PrimitiveType.Cube, g, "Awning", p + new Vector3(0f, 2.62f, -0.15f), new Vector3(3.3f, 0.05f, 1.55f), awning);
            aw.localRotation = Quaternion.Euler(-14f, 0f, 0f);
            Prim(PrimitiveType.Cube, g, "Valance", p + new Vector3(0f, 2.32f, -0.95f), new Vector3(3.3f, 0.3f, 0.03f), awning);
            Prim(PrimitiveType.Cube, g, "SignBoard", p + new Vector3(0f, 3.1f, 0.4f), new Vector3(2.2f, 0.45f, 0.06f), Mat(new Color(0.06f, 0.04f, 0.08f), true));
            NeonSign(g, "StallSign", signs[i], p + new Vector3(0f, 3.1f, 0.36f), 0.0028f, 750f, 140f, signC[i], FontStyle.Bold);
            var b = Prim(PrimitiveType.Sphere, g, "StallBulb", p + new Vector3(0f, 2.15f, -0.5f), Vector3.one * 0.14f, Mat(new Color(1f, 0.85f, 0.5f), false));
            // Tezgâh üstü: tencere, kaseler, şişeler
            Prim(PrimitiveType.Cylinder, g, "Pot", p + new Vector3(-0.8f, 1.2f, -0.2f), new Vector3(0.45f, 0.17f, 0.45f), Mat(new Color(0.6f, 0.6f, 0.65f), true));
            Prim(PrimitiveType.Cube, g, "Bowl", p + new Vector3(0.1f, 1.09f, -0.45f), new Vector3(0.22f, 0.09f, 0.22f), Mat(new Color(0.95f, 0.95f, 0.92f), true));
            Prim(PrimitiveType.Cube, g, "Bowl", p + new Vector3(0.4f, 1.09f, -0.45f), new Vector3(0.22f, 0.09f, 0.22f), Mat(new Color(0.95f, 0.95f, 0.92f), true));
            Prim(PrimitiveType.Cube, g, "Bottle", p + new Vector3(0.95f, 1.2f, -0.3f), new Vector3(0.08f, 0.3f, 0.08f), Mat(signC[i], true));
            // Satıcı tezgâhın arkasında
            BuildSpectator(g, p + new Vector3(0.35f, 0f, 0.15f), new Color(0.9f, 0.9f, 0.88f), skins[i % skins.Length], out var vr, out var val, out var var2);
            vendors.Add(vr); vArmsL.Add(val); vArmsR.Add(var2);
            bulbs.Add(b.GetComponent<Renderer>());
        }

        // Sokak boyunca gerili renkli ampuller (iki sıra) — yarısı yanıp söner
        var blink = new List<Renderer>();
        Color[] bulbC = { new Color(1f, 0.3f, 0.3f), new Color(1f, 0.85f, 0.3f), new Color(0.35f, 1f, 0.5f), new Color(0.35f, 0.7f, 1f), new Color(1f, 0.45f, 0.9f) };
        var wire = Mat(new Color(0.05f, 0.05f, 0.05f), true);
        StringLights(g, 3.4f, 3.25f, 0.35f, -13f, 13f, 0.65f, bulbC, wire, blink);
        StringLights(g, 8.6f, 4.55f, 0.4f, -16f, 16f, 0.8f, bulbC, wire, blink);
        amb.blinkTubes = blink.ToArray();
        amb.blinkTexts = new[] { open24 };

        // Kalabalık: tezgâhların önünde dövüşü izleyenler
        var crowd = new List<Transform>(vendors);
        var armsL = new List<Transform>(vArmsL);
        var armsR = new List<Transform>(vArmsR);
        Color[] shirts =
        {
            new Color(0.22f, 0.25f, 0.38f), new Color(0.38f, 0.16f, 0.2f), new Color(0.16f, 0.32f, 0.25f), new Color(0.35f, 0.3f, 0.16f),
            new Color(0.28f, 0.18f, 0.36f), new Color(0.45f, 0.45f, 0.48f), new Color(0.12f, 0.12f, 0.15f), new Color(0.5f, 0.22f, 0.1f),
        };
        var rng = new System.Random(33);
        for (float x = -9.5f; x <= 9.5f; x += 1.15f)
        {
            if (Mathf.Abs(x) < 0.7f) continue;   // ortada boşluk: ara sokak görünsün
            float jz = (float)rng.NextDouble() * 0.6f;
            BuildSpectator(g, new Vector3(x + (float)rng.NextDouble() * 0.3f, 0f, 3.9f + jz), shirts[rng.Next(shirts.Length)], skins[rng.Next(skins.Length)], out var root, out var al, out var ar);
            crowd.Add(root); armsL.Add(al); armsR.Add(ar);
        }
        amb.crowd = crowd.ToArray();
        amb.crowdArmsL = armsL.ToArray();
        amb.crowdArmsR = armsR.ToArray();

        // Oyuncu renginde asılı sancaklar
        PlayerFlag(g, new Vector3(-8.6f, 0f, 3.2f), 3.0f, true);
        PlayerFlag(g, new Vector3(8.6f, 0f, 3.2f), 3.0f, false);

        amb.lights = new[]
        {
            StageLight(g, "StallLightL", new Vector3(-4.2f, 2.4f, 4.4f), new Color(1f, 0.65f, 0.35f), 1.8f, 8f),
            StageLight(g, "StallLightR", new Vector3(4.2f, 2.4f, 4.4f), new Color(1f, 0.65f, 0.35f), 1.8f, 8f),
            StageLight(g, "StreetFill", new Vector3(0f, 3.5f, -2.5f), new Color(1f, 0.6f, 0.85f), 1.0f, 13f),
        };
    }

    /// <summary>İki uç arasında hafif sarkan kablo + renkli ampuller.</summary>
    void StringLights(Transform g, float z, float y, float sag, float x0, float x1, float step, Color[] colors, Material wire, List<Renderer> blink)
    {
        int n = Mathf.Max(2, Mathf.RoundToInt((x1 - x0) / step));
        float Y(float x) { float t = (x - x0) / (x1 - x0); return y - sag * 4f * t * (1f - t); }
        for (int i = 0; i <= n; i++)
        {
            float x = Mathf.Lerp(x0, x1, i / (float)n);
            if (i < n)
            {
                float xn = Mathf.Lerp(x0, x1, (i + 1) / (float)n);
                float ya = Y(x), yb = Y(xn);
                var seg = Prim(PrimitiveType.Cube, g, "Wire", new Vector3((x + xn) * 0.5f, (ya + yb) * 0.5f + 0.06f, z), new Vector3(xn - x, 0.015f, 0.015f), wire);
                seg.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(yb - ya, xn - x) * Mathf.Rad2Deg);
            }
            var b = Prim(PrimitiveType.Sphere, g, "Bulb", new Vector3(x, Y(x), z), Vector3.one * 0.11f, Mat(colors[i % colors.Length], false)).GetComponent<Renderer>();
            if (i % 3 == 1) blink.Add(b);
        }
    }

    #endregion

    #region Sahne dokuları

    static Texture2D NewTex(string name, int w, int h, Color[] px, TextureWrapMode wrap)
    {
        var t = new Texture2D(w, h, TextureFormat.RGBA32, false);
        t.SetPixels(px);
        t.filterMode = FilterMode.Point;
        t.wrapMode = wrap;
        t.name = name;
        t.Apply();
        return t;
    }

    /// <summary>Beton çatı: gri gürültü, derz çizgileri, lekeler.</summary>
    static Texture2D RoofConcrete()
    {
        const int S = 64;
        var px = new Color[S * S];
        var rng = new System.Random(5);
        for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
            {
                float n = (float)rng.NextDouble() * 0.05f;
                float v = 0.36f - n;
                float sx = x - 40f, sy = y - 20f;
                if (sx * sx + sy * sy < 60f) v -= 0.05f;   // leke
                if (x % 32 == 0 || y % 32 == 0) v = 0.24f;
                px[y * S + x] = new Color(v, v, v * 1.06f);
            }
        return NewTex("RoofConcrete", S, S, px, TextureWrapMode.Repeat);
    }

    static Texture2D CityWindowsWarm() => CityWindows(17, new Color(1f, 0.82f, 0.48f));
    static Texture2D CityWindowsCool() => CityWindows(29, new Color(0.6f, 0.82f, 1f));

    /// <summary>Gece bina cephesi: koyu duvar, rastgele yanan pencereler (4 x 8 pencere / karo).</summary>
    static Texture2D CityWindows(int seed, Color lit)
    {
        const int W = 32, H = 64;
        var px = new Color[W * H];
        var rng = new System.Random(seed);
        var wall = new Color(0.05f, 0.055f, 0.09f);
        var off = new Color(0.08f, 0.09f, 0.14f);
        for (int i = 0; i < px.Length; i++) px[i] = wall;
        for (int cy = 0; cy < 8; cy++)
            for (int cx = 0; cx < 4; cx++)
            {
                bool on = rng.NextDouble() < 0.42;
                float k = 0.65f + (float)rng.NextDouble() * 0.35f;
                Color c = on ? new Color(lit.r * k, lit.g * k, lit.b * k) : off;
                for (int y = 2; y < 7; y++)
                    for (int x = 2; x < 6; x++)
                        px[(cy * 8 + y) * W + cx * 8 + x] = c;
            }
        return NewTex("CityWindows" + seed, W, H, px, TextureWrapMode.Repeat);
    }

    /// <summary>Gece gökyüzü: ufukta mor-pembe şehir ışığı, yukarıda lacivert.</summary>
    static Texture2D NightSky()
    {
        const int W = 4, H = 64;
        var px = new Color[W * H];
        var horizon = new Color(0.42f, 0.14f, 0.32f);
        var mid = new Color(0.12f, 0.06f, 0.22f);
        var top = new Color(0.02f, 0.02f, 0.07f);
        for (int y = 0; y < H; y++)
        {
            float v = y / (H - 1f);
            Color c = v < 0.26f ? Color.Lerp(horizon, mid, v / 0.26f) : Color.Lerp(mid, top, Mathf.Clamp01((v - 0.26f) / 0.3f));
            for (int x = 0; x < W; x++) px[y * W + x] = c;
        }
        var t = NewTex("NightSky", W, H, px, TextureWrapMode.Clamp);
        t.filterMode = FilterMode.Bilinear;
        return t;
    }

    /// <summary>Ahşap döşeme: x yönünde uzanan, ucu kaydırmalı tahtalar.</summary>
    static Texture2D WoodPlanks()
    {
        const int S = 64;
        var px = new Color[S * S];
        var rng = new System.Random(8);
        var shade = new float[8, 2];
        for (int r = 0; r < 8; r++) for (int s = 0; s < 2; s++) shade[r, s] = 0.85f + (float)rng.NextDouble() * 0.3f;
        var baseC = new Color(0.62f, 0.4f, 0.22f);
        for (int y = 0; y < S; y++)
        {
            int row = y / 8;
            for (int x = 0; x < S; x++)
            {
                int xo = (x + row * 13) % S;
                int seg = xo / 32;
                float k = shade[row, seg] * (0.95f + (float)rng.NextDouble() * 0.06f);
                if ((y + (xo / 5)) % 4 == 0) k *= 0.94f;   // damar
                Color c = new Color(baseC.r * k, baseC.g * k, baseC.b * k);
                if (y % 8 == 0 || xo % 32 == 0) c = new Color(0.22f, 0.13f, 0.07f);
                px[y * S + x] = c;
            }
        }
        return NewTex("WoodPlanks", S, S, px, TextureWrapMode.Repeat);
    }

    /// <summary>Shoji: krem pirinç kâğıdı + koyu ahşap kafes.</summary>
    static Texture2D ShojiPaper()
    {
        const int S = 64;
        var px = new Color[S * S];
        var rng = new System.Random(4);
        var frame = new Color(0.28f, 0.17f, 0.09f);
        for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
            {
                float n = (float)rng.NextDouble() * 0.04f;
                Color c = new Color(0.97f - n, 0.94f - n, 0.86f - n);
                bool edge = x < 2 || x >= S - 2 || y < 2 || y >= S - 2;
                bool grid = x % 16 == 0 || y % 21 == 0;
                if (edge || grid) c = frame;
                px[y * S + x] = c;
            }
        return NewTex("ShojiPaper", S, S, px, TextureWrapMode.Repeat);
    }

    /// <summary>Gece sokağı: koyu taş parke, sıra sıra kaydırmalı.</summary>
    static Texture2D StoneTiles()
    {
        const int S = 64;
        var px = new Color[S * S];
        var rng = new System.Random(12);
        var tone = new float[16];
        for (int i = 0; i < tone.Length; i++) tone[i] = 0.8f + (float)rng.NextDouble() * 0.35f;
        for (int y = 0; y < S; y++)
        {
            int row = y / 16;
            for (int x = 0; x < S; x++)
            {
                int xo = (x + (row % 2) * 8) % S;
                int col = xo / 16;
                float k = tone[(row * 4 + col) % tone.Length] - (float)rng.NextDouble() * 0.05f;
                Color c = new Color(0.2f * k, 0.2f * k, 0.23f * k);
                if (y % 16 == 0 || xo % 16 == 0) c = new Color(0.07f, 0.07f, 0.08f);
                px[y * S + x] = c;
            }
        }
        return NewTex("StoneTiles", S, S, px, TextureWrapMode.Repeat);
    }

    /// <summary>Beyaz + renkli dikey şeritli tente dokusu.</summary>
    static System.Func<Texture2D> MakeStripes(Color c)
    {
        return () =>
        {
            const int W = 16, H = 4;
            var px = new Color[W * H];
            var white = new Color(0.95f, 0.93f, 0.88f);
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                    px[y * W + x] = (x / 4) % 2 == 0 ? c : white;
            return NewTex("Stripes", W, H, px, TextureWrapMode.Repeat);
        };
    }

    #endregion
}