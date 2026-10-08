using UnityEngine;

/// <summary>
/// Dövüþ sahneleri: FightArena altýndaki gruplardan yalnýzca seçileni açýk tutar.
/// 0 = boks ringi (ortak Floor/BackWall'u kullanýr), diðerleri kendi zemin ve arka planýyla gelir.
/// </summary>
public static class StageSwitcher
{
    public static readonly string[] Groups = { "BoxingArena", "StageRooftop", "StageDojo", "StageMarket" };
    public static readonly string[] Names = { "BOXING RING", "ROOFTOP", "DOJO", "NIGHT MARKET" };
    public static readonly string[] Descs =
    {
        "WORLD CHAMPIONSHIP UNDER THE SPOTLIGHTS",
        "CITY LIGHTS AT MIDNIGHT",
        "TRAIN WITH THE MASTERS",
        "STREET FOOD, NEON AND A HUNGRY CROWD",
    };
    static readonly Color[] Backgrounds =
    {
        new Color(0.08f, 0.03f, 0.12f),
        new Color(0.03f, 0.03f, 0.09f),
        new Color(0.1f, 0.06f, 0.04f),
        new Color(0.03f, 0.02f, 0.07f),
    };

    public static int Count => Groups.Length;
    public static int Current { get; private set; }

    /// <summary>Sahnede kurulu mu? (Eski kurulu sahnelerde ek sahneler Play'de eklenir.)</summary>
    public static bool Exists(Transform arena, int index)
    {
        return arena != null && index >= 0 && index < Count && arena.Find(Groups[index]) != null;
    }

    public static void Apply(Transform arena, int index, Camera cam)
    {
        if (arena == null) return;
        index = ((index % Count) + Count) % Count;
        if (!Exists(arena, index)) index = 0;

        for (int i = 0; i < Count; i++)
        {
            var g = arena.Find(Groups[i]);
            if (g != null && g.gameObject.activeSelf != (i == index)) g.gameObject.SetActive(i == index);
        }
        // Ring zemini ve koyu arka duvar sadece boks sahnesinde
        bool ring = index == 0;
        var floor = arena.Find("Floor");
        if (floor != null) floor.gameObject.SetActive(ring);
        var wall = arena.Find("BackWall");
        if (wall != null) wall.gameObject.SetActive(ring);

        if (cam != null) cam.backgroundColor = Backgrounds[index];
        Current = index;
    }
}