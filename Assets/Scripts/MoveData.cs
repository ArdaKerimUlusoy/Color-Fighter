using UnityEngine;

public enum HitLevel { Mid, Low, High }

public enum AttackPose { StandPunch, StandKick, CrouchPunch, CrouchKick, AirPunch, AirKick, ComboRushA, ComboRushB, ComboFinisher, Special, Throw }

[System.Serializable]
public class MoveData
{
    #region Kimlik

    public string name = "Move";
    public AttackPose pose;

    #endregion

    #region Frame data

    [Header("Frame data (1 frame = 1/60 sn)")]
    [Tooltip("Tuşa basıştan vuruşun çıkmasına kadar geçen frame. Düşük = hızlı.")]
    public int startup = 4;
    [Tooltip("Hitbox'ın açık kaldığı frame sayısı.")]
    public int active = 3;
    [Tooltip("Vuruştan sonra savunmasız kalınan frame. Yüksek = ıskalarsan cezalandırılırsın.")]
    public int recovery = 8;

    #endregion

    #region Vuruş

    [Header("Vuruş")]
    public int damage = 6;
    [Tooltip("Mid = her blok tutar, Low = çömelik blok tutar, High = ayakta blok tutar.")]
    public HitLevel level = HitLevel.Mid;
    [Tooltip("Ayak hizasına göre (x = ileri, y = yukarı).")]
    public Vector2 hitboxOffset = new Vector2(0.7f, 1.3f);
    public Vector2 hitboxSize = new Vector2(0.6f, 0.3f);

    #endregion

    #region His

    [Header("His")]
    [Tooltip("Rakibin vuruş sonrası kontrolsüz kaldığı frame. Kalan active + recovery'den büyükse kombo bağlanır.")]
    public int hitstun = 14;
    public int blockstun = 10;
    [Tooltip("Vuruş anında oyunun donduğu frame sayısı. Ağırlık hissini bu verir.")]
    public int hitstop = 6;
    [Tooltip("Rakibi geri itme hızı.")]
    public float pushback = 3f;
    [Tooltip("Saldırırken öne kayma hızı.")]
    public float lunge = 0f;
    [Tooltip("Vurursa rakip yere düşer.")]
    public bool knockdown = false;
    [Tooltip("Rakibi havaya kaldırma hızı (0 = yok).")]
    public float launch = 0f;
    [Tooltip("İsabet ederse tekme ile iptal edilebilir (kombo).")]
    public bool cancelable = false;
    [Tooltip("Kombo saldırısı (Color Rush) parçası: hasar ölçeklenmez, bloklansa da biraz hasar verir.")]
    public bool isSuper = false;

    #endregion

    #region Özel hareket / fırlatma (kodla kurulur, sahneye kaydedilmez)

    /// <summary>Özel hareketin kendi pozları (12 değer, FighterRig tablosu). Boşsa saldırı türüne göre seçilir.</summary>
    [System.NonSerialized] public float[] windPose, hitPose;
    /// <summary>Hazırlık + aktif süresince gövdenin toplam dönüşü (derece) ve takla açısı.</summary>
    [System.NonSerialized] public float spin, flip;
    /// <summary>Hareket başında yukarı zıplama hızı (0 = yerde kalır).</summary>
    [System.NonSerialized] public float hop;
    /// <summary>Çok vuruşlu hareket: toplam isabet sayısı ve isabetler arası frame.</summary>
    [System.NonSerialized] public int hits = 1, hitEvery = 6;
    [System.NonSerialized] public bool isSpecial, isThrow, isProjectile;
    /// <summary>Özel hareketin türü (SpecialMoves.Kind).</summary>
    [System.NonSerialized] public int specialKind;
    /// <summary>Efekt rengi (alev, plazma, yaprak...).</summary>
    [System.NonSerialized] public Color fxColor = Color.white;

    #endregion

    #region Kısayollar

    public int Total => startup + active + recovery;
    public bool IsAir => pose == AttackPose.AirPunch || pose == AttackPose.AirKick;
    public bool IsCrouch => pose == AttackPose.CrouchPunch || pose == AttackPose.CrouchKick;
    public bool IsHeavy => hitstop >= 9;

    #endregion
}
