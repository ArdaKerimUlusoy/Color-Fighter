using UnityEngine;

/// <summary>Karakter seçim ekranındaki renkler. Karakterler aynı, sadece renk değişir.</summary>
public static class FighterPalette
{
    public struct Entry
    {
        public string name;
        public Color color;
        public Entry(string name, Color color) { this.name = name; this.color = color; }
    }

    public const int Columns = 4;

    public static readonly Entry[] All =
    {
        new Entry("RED",    new Color(0.90f, 0.12f, 0.12f)),
        new Entry("BLUE",   new Color(0.15f, 0.35f, 1.00f)),
        new Entry("GREEN",  new Color(0.15f, 0.78f, 0.25f)),
        new Entry("YELLOW", new Color(1.00f, 0.84f, 0.10f)),
        new Entry("PURPLE", new Color(0.60f, 0.22f, 0.92f)),
        new Entry("ORANGE", new Color(1.00f, 0.50f, 0.08f)),
        new Entry("CYAN",   new Color(0.10f, 0.85f, 0.95f)),
        new Entry("PINK",   new Color(1.00f, 0.38f, 0.72f)),
    };

    public static int Count => All.Length;
    public static int Rows => (All.Length + Columns - 1) / Columns;

    /// <summary>Verilen renge en yakın palet girişinin indeksi.</summary>
    public static int ClosestIndex(Color c)
    {
        int best = 0;
        float bestD = float.MaxValue;
        for (int i = 0; i < All.Length; i++)
        {
            var e = All[i].color;
            float d = (e.r - c.r) * (e.r - c.r) + (e.g - c.g) * (e.g - c.g) + (e.b - c.b) * (e.b - c.b);
            if (d < bestD) { bestD = d; best = i; }
        }
        return best;
    }
}
