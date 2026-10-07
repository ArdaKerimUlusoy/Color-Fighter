using UnityEngine;

public class CameraSway : MonoBehaviour
{
    #region Ayarlar

    public float amount = 0.008f;
    public float speed = 0.35f;

    #endregion

    #region Salınım

    Vector3 basePos;
    Quaternion baseRot;

    void Start()
    {
        basePos = transform.position;
        baseRot = transform.rotation;
    }

    void LateUpdate()
    {
        float t = Time.unscaledTime * speed;
        transform.position = basePos + new Vector3(Mathf.Sin(t * 1.3f) * amount, Mathf.Sin(t * 2.1f) * amount * 0.6f, 0f);
        transform.rotation = baseRot * Quaternion.Euler(Mathf.Sin(t * 1.7f) * 0.2f, Mathf.Sin(t) * 0.3f, 0f);
    }

    #endregion
}
