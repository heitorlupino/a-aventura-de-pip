using System.Collections;
using UnityEngine;

/// <summary>
/// Base para itens coletáveis: animação de flutuar/girar (Transform)
/// e coleta ao encostar no jogador (trigger).
/// </summary>
[RequireComponent(typeof(Collider2D))]
public abstract class Pickup : MonoBehaviour
{
    public Transform visual;
    public float bobHeight = 0.1f;
    public float bobSpeed = 3f;
    public bool spin = true;
    public float spinSpeed = 4f;

    bool collected;
    Vector3 visualStart;

    protected virtual void Awake()
    {
        if (visual == null) visual = transform;
        visualStart = visual.localPosition;
    }

    protected virtual void Update()
    {
        if (collected) return;
        float phase = transform.position.x;
        visual.localPosition = visualStart + Vector3.up * Mathf.Sin(Time.time * bobSpeed + phase) * bobHeight;
        if (spin)
        {
            float sx = Mathf.Max(0.15f, Mathf.Abs(Mathf.Cos(Time.time * spinSpeed + phase)));
            visual.localScale = new Vector3(sx, 1f, 1f);
        }
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (collected || other.GetComponent<PlayerController>() == null) return;
        collected = true;
        GetComponent<Collider2D>().enabled = false;
        OnCollected();
        StartCoroutine(PopAndDestroy());
    }

    protected abstract void OnCollected();

    IEnumerator PopAndDestroy()
    {
        var sr = visual.GetComponent<SpriteRenderer>();
        Vector3 start = visual.localPosition;
        const float duration = 0.35f;
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float k = t / duration;
            visual.localPosition = start + Vector3.up * k * 0.8f;
            visual.localScale = Vector3.one * (1f + k * 0.5f);
            if (sr != null) { var c = sr.color; c.a = 1f - k; sr.color = c; }
            yield return null;
        }
        Destroy(gameObject);
    }
}
