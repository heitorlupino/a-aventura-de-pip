using UnityEngine;

/// <summary>Câmera que segue o jogador suavemente, presa aos limites da fase, com tremida.</summary>
[RequireComponent(typeof(Camera))]
public class CameraFollow : MonoBehaviour
{
    public Transform target;
    public Vector2 offset = new Vector2(1.5f, 1f);
    public float smoothTime = 0.15f;

    [Header("Limites da fase")]
    public float levelMinX;
    public float levelMaxX = 100f;
    public float levelMinY;
    public float levelMaxY = 20f;

    static float shakeTime, shakeStrength;

    Camera cam;
    Vector3 basePosition, velocity;

    public static void Shake(float strength, float duration)
    {
        shakeStrength = strength;
        shakeTime = duration;
    }

    void Awake() => cam = GetComponent<Camera>();

    void Start()
    {
        basePosition = DesiredPosition();
        transform.position = basePosition;
    }

    void LateUpdate()
    {
        if (target == null) return;

        basePosition = Vector3.SmoothDamp(basePosition, DesiredPosition(), ref velocity, smoothTime);

        Vector3 shake = Vector3.zero;
        if (shakeTime > 0f)
        {
            shakeTime -= Time.unscaledDeltaTime;
            shake = (Vector3)(Random.insideUnitCircle * shakeStrength);
        }
        transform.position = basePosition + shake;
    }

    Vector3 DesiredPosition()
    {
        Vector3 p = target != null ? target.position : transform.position;
        float halfH = cam.orthographicSize;
        float halfW = halfH * cam.aspect;
        float x = Mathf.Clamp(p.x + offset.x, levelMinX + halfW, Mathf.Max(levelMinX + halfW, levelMaxX - halfW));
        float y = Mathf.Clamp(p.y + offset.y, levelMinY + halfH, Mathf.Max(levelMinY + halfH, levelMaxY - halfH));
        return new Vector3(x, y, transform.position.z);
    }
}
