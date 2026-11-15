using UnityEngine;

/// <summary>Fundo com efeito parallax: move-se com uma fração do movimento da câmera.</summary>
[DefaultExecutionOrder(100)]
public class Parallax : MonoBehaviour
{
    [Range(0f, 1f)] public float factor = 0.5f;
    public float driftSpeed;

    Transform cam;
    Vector3 start, camStart;
    float drift;

    void Start()
    {
        cam = Camera.main != null ? Camera.main.transform : null;
        start = transform.position;
        if (cam != null) camStart = cam.position;
    }

    void LateUpdate()
    {
        if (cam == null) return;
        drift += driftSpeed * Time.deltaTime;
        Vector3 d = cam.position - camStart;
        transform.position = new Vector3(start.x + d.x * factor + drift, start.y + d.y * factor, start.z);
    }
}
