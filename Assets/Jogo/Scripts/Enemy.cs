using System.Collections;
using UnityEngine;

/// <summary>
/// Inimigo que patrulha entre dois limites (Rigidbody2D cinemático).
/// Morre se o jogador pular em cima; caso contrário, causa dano.
/// </summary>
[RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
public class Enemy : MonoBehaviour
{
    public enum EnemyState { Patrolling, Dead }

    public float speed = 2f;
    public float leftLimit;
    public float rightLimit;
    public Sprite frame1, frame2, deadSprite;
    public float frameTime = 0.25f;

    public EnemyState State { get; private set; } = EnemyState.Patrolling;
    public bool IsAlive => State != EnemyState.Dead;

    Rigidbody2D rb;
    Collider2D col;
    SpriteRenderer sr;
    int direction = -1;
    float animTimer;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        col = GetComponent<Collider2D>();
        sr = GetComponentInChildren<SpriteRenderer>();
        rb.bodyType = RigidbodyType2D.Kinematic;

        if (Mathf.Approximately(leftLimit, rightLimit))
        {
            leftLimit = transform.position.x - 2f;
            rightLimit = transform.position.x + 2f;
        }
    }

    void FixedUpdate()
    {
        if (!IsAlive) return;

        Vector2 p = rb.position;
        p.x += direction * speed * Time.fixedDeltaTime;
        if (p.x <= leftLimit) { p.x = leftLimit; direction = 1; }
        else if (p.x >= rightLimit) { p.x = rightLimit; direction = -1; }
        rb.MovePosition(p);
    }

    void Update()
    {
        if (!IsAlive) return;
        animTimer += Time.deltaTime;
        sr.sprite = ((int)(animTimer / frameTime) % 2 == 0) ? frame1 : frame2;
        sr.flipX = direction > 0;
    }

    void OnTriggerStay2D(Collider2D other)
    {
        if (!IsAlive) return;
        var player = other.GetComponent<PlayerController>();
        if (player == null) return;

        if (player.IsStomping(col))
        {
            player.Bounce();
            Die();
        }
        else
        {
            player.TakeDamage(transform.position);
        }
    }

    public void Die()
    {
        State = EnemyState.Dead;
        col.enabled = false;
        sr.sprite = deadSprite;
        GameManager.Instance?.EnemyDefeated();
        CameraFollow.Shake(0.1f, 0.1f);
        StartCoroutine(FadeAndDestroy());
    }

    IEnumerator FadeAndDestroy()
    {
        const float duration = 0.6f;
        float t = 0f;
        Color c = sr.color;
        while (t < duration)
        {
            t += Time.deltaTime;
            c.a = 1f - t / duration;
            sr.color = c;
            yield return null;
        }
        Destroy(gameObject);
    }
}
