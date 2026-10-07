using UnityEngine;

public class CabinetControls : MonoBehaviour
{
    #region Ayarlar

    public FighterInput input;
    public Transform stick, punchButton, kickButton;
    public float tiltAngle = 22f;
    public float pressDepth = 0.014f;

    #endregion

    #region Durum

    Vector3 punchRest, kickRest;
    Material punchMat, kickMat;
    Color punchBase, kickBase;
    bool ready;

    void Start()
    {
        if (input == null || stick == null || punchButton == null || kickButton == null) return;
        punchRest = punchButton.localPosition;
        kickRest = kickButton.localPosition;
        punchMat = punchButton.GetComponent<Renderer>().material;
        kickMat = kickButton.GetComponent<Renderer>().material;
        punchBase = punchMat.color;
        kickBase = kickMat.color;
        ready = true;
    }

    #endregion

    #region Hareket

    void Update()
    {
        if (!ready) return;

        float v = input.Up ? 1f : input.Down ? -1f : 0f;
        var target = Quaternion.Euler(v * tiltAngle, 0f, -input.Horizontal * tiltAngle);
        stick.localRotation = Quaternion.Slerp(stick.localRotation, target, 1f - Mathf.Exp(-35f * Time.unscaledDeltaTime));

        Press(punchButton, punchRest, punchMat, punchBase, input.PunchHeld);
        Press(kickButton, kickRest, kickMat, kickBase, input.KickHeld);
    }

    void Press(Transform b, Vector3 rest, Material m, Color baseColor, bool held)
    {
        b.localPosition = held ? rest - Vector3.up * pressDepth : rest;
        m.color = held ? Color.Lerp(baseColor, Color.white, 0.45f) : baseColor;
    }

    #endregion
}
