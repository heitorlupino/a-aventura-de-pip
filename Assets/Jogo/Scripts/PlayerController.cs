using UnityEngine;

/// <summary>
/// Controle do jogador: entrada (Input Manager: teclado e controle), movimento com física (Rigidbody2D),
/// pulo com coyote time / jump buffer, dano, morte, checkpoint e animação por estados.
/// </summary>
[RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
public class PlayerController : MonoBehaviour
{
    public enum PlayerState { Idle, Running, Jumping, Falling, Hurt, Dead }

    [Header("Movimento")]
    public float moveSpeed = 7f;
    public float acceleration = 70f;
    public float deceleration = 80f;
    [Range(0f, 1f)] public float airControl = 0.75f;

    [Header("Pulo")]
    public float jumpVelocity = 15f;
    public float coyoteTime = 0.1f;
    public float jumpBufferTime = 0.12f;
    [Range(0f, 1f)] public float jumpCutMultiplier = 0.5f;
    public float stompBounce = 11f;
    public float maxFallSpeed = 20f;

    [Header("Dano")]
    public float invincibleDuration = 1.5f;
    public float hurtDuration = 0.35f;
    public Vector2 knockback = new Vector2(6f, 8f);
    public float fallLimitY = -4f;

    [Header("Visual")]
    public Transform visual;
    public Sprite[] idleFrames;
    public Sprite[] runFrames;
    public Sprite jumpSprite, fallSprite;
    public float idleFrameTime = 0.5f;
    public float runFrameTime = 0.09f;

    public PlayerState State { get; private set; } = PlayerState.Idle;
    public bool IsGrounded { get; private set; }

    Rigidbody2D rb;
    Collider2D col;
    SpriteRenderer sr;
    readonly ContactPoint2D[] contacts = new ContactPoint2D[16];

    float moveInput;
    float lastGroundedTime = -10f, lastJumpPressedTime = -10f;
    float invincibleUntil, hurtUntil, animTimer;
    bool facingRight = true, wasGrounded;
    Vector3 respawnPoint, visualBaseScale;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        col = GetComponent<Collider2D>();
        if (visual == null) visual = transform;
        sr = visual.GetComponent<SpriteRenderer>();
        visualBaseScale = visual.localScale;
        respawnPoint = transform.position;
    }

    static bool IsPlaying => GameManager.Instance == null || GameManager.Instance.State == GameState.Playing;

    void Update()
    {
        if (!IsPlaying) return;

        if (State == PlayerState.Dead)
        {
            visual.Rotate(0f, 0f, 360f * Time.deltaTime);
            return;
        }

        // "Horizontal" = A/D, setas e analógico do controle.
        moveInput = Input.GetAxisRaw("Horizontal");

        if (JumpPressed())
            lastJumpPressedTime = Time.time;

        // Pulo variável: soltar o botão cedo corta a subida.
        if (JumpReleased() && rb.linearVelocity.y > 0f)
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, rb.linearVelocity.y * jumpCutMultiplier);

        if (transform.position.y < fallLimitY)
            FellOutOfLevel();

        UpdateVisuals();
    }

    // "Jump" = Espaço e botão A do controle; W e seta para cima também pulam.
    static bool JumpPressed() =>
        Input.GetButtonDown("Jump") || Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow);

    static bool JumpReleased() =>
        (Input.GetButtonUp("Jump") || Input.GetKeyUp(KeyCode.W) || Input.GetKeyUp(KeyCode.UpArrow))
        && !(Input.GetButton("Jump") || Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow));

    void FixedUpdate()
    {
        if (State == PlayerState.Dead) return;

        IsGrounded = CheckGrounded();
        if (IsGrounded) lastGroundedTime = Time.time;
        if (IsGrounded && !wasGrounded) Squash(new Vector2(1.2f, 0.8f)); // aterrissou
        wasGrounded = IsGrounded;

        bool hurt = Time.time < hurtUntil;
        Vector2 v = rb.linearVelocity;

        if (!hurt)
        {
            float target = moveInput * moveSpeed;
            float rate = Mathf.Abs(target) > 0.01f ? acceleration : deceleration;
            if (!IsGrounded) rate *= airControl;
            v.x = Mathf.MoveTowards(v.x, target, rate * Time.fixedDeltaTime);

            bool jumpBuffered = Time.time - lastJumpPressedTime <= jumpBufferTime;
            bool canJump = Time.time - lastGroundedTime <= coyoteTime;
            if (jumpBuffered && canJump)
            {
                v.y = jumpVelocity;
                lastJumpPressedTime = -10f;
                lastGroundedTime = -10f;
                IsGrounded = false;
                GameManager.Instance?.PlaySfx(Sfx.Jump);
                Squash(new Vector2(0.75f, 1.25f));
            }
        }

        v.y = Mathf.Max(v.y, -maxFallSpeed);
        rb.linearVelocity = v;
        UpdateState();
    }

    /// <summary>Está no chão se algum contato tiver a normal apontando para cima.</summary>
    bool CheckGrounded()
    {
        if (rb.linearVelocity.y > 0.5f) return false;
        int count = rb.GetContacts(contacts);
        for (int i = 0; i < count; i++)
            if (contacts[i].normal.y > 0.6f) return true;
        return false;
    }

    void UpdateState()
    {
        if (State == PlayerState.Dead) return;
        if (Time.time < hurtUntil) { State = PlayerState.Hurt; return; }

        if (IsGrounded)
            State = Mathf.Abs(rb.linearVelocity.x) > 0.2f ? PlayerState.Running : PlayerState.Idle;
        else
            State = rb.linearVelocity.y > 0.1f ? PlayerState.Jumping : PlayerState.Falling;
    }

    void UpdateVisuals()
    {
        if (moveInput > 0.1f) facingRight = true;
        else if (moveInput < -0.1f) facingRight = false;
        sr.flipX = !facingRight;

        animTimer += Time.deltaTime;
        switch (State)
        {
            case PlayerState.Running: sr.sprite = Frame(runFrames, runFrameTime); break;
            case PlayerState.Jumping: sr.sprite = jumpSprite; break;
            case PlayerState.Falling:
            case PlayerState.Hurt: sr.sprite = fallSprite; break;
            default: sr.sprite = Frame(idleFrames, idleFrameTime); break;
        }

        // Pisca enquanto está invencível.
        bool invincible = Time.time < invincibleUntil;
        sr.enabled = !invincible || Mathf.Repeat(Time.time, 0.15f) > 0.06f;
        sr.color = State == PlayerState.Hurt ? new Color(1f, 0.5f, 0.5f) : Color.white;

        // Volta suavemente do "squash & stretch".
        visual.localScale = Vector3.Lerp(visual.localScale, visualBaseScale, 12f * Time.deltaTime);
    }

    Sprite Frame(Sprite[] frames, float frameTime)
    {
        if (frames == null || frames.Length == 0) return sr.sprite;
        return frames[(int)(animTimer / frameTime) % frames.Length];
    }

    void Squash(Vector2 amount)
    {
        visual.localScale = new Vector3(visualBaseScale.x * amount.x, visualBaseScale.y * amount.y, visualBaseScale.z);
    }

    // ---------- Interações chamadas por outros objetos ----------

    /// <summary>True se o jogador está caindo em cima do inimigo.</summary>
    public bool IsStomping(Collider2D enemyCollider)
    {
        return State != PlayerState.Dead
               && rb.linearVelocity.y <= 0.5f
               && col.bounds.min.y >= enemyCollider.bounds.center.y - 0.1f;
    }

    public void Bounce()
    {
        rb.linearVelocity = new Vector2(rb.linearVelocity.x, stompBounce);
        lastGroundedTime = -10f;
        Squash(new Vector2(0.8f, 1.2f));
    }

    public void SetCheckpoint(Vector3 position) => respawnPoint = position;

    public void TakeDamage(Vector2 sourcePosition)
    {
        if (State == PlayerState.Dead || Time.time < invincibleUntil || !IsPlaying) return;

        bool alive = GameManager.Instance == null || GameManager.Instance.DamagePlayer();
        CameraFollow.Shake(0.25f, 0.25f);

        if (!alive) { Die(); return; }

        invincibleUntil = Time.time + invincibleDuration;
        hurtUntil = Time.time + hurtDuration;
        State = PlayerState.Hurt;

        float dir = Mathf.Sign(transform.position.x - sourcePosition.x);
        rb.linearVelocity = new Vector2(dir * knockback.x, knockback.y);
    }

    void FellOutOfLevel()
    {
        bool alive = GameManager.Instance == null || GameManager.Instance.DamagePlayer();
        CameraFollow.Shake(0.3f, 0.3f);

        if (alive) Respawn();
        else
        {
            State = PlayerState.Dead;
            rb.simulated = false;
            sr.enabled = false;
        }
    }

    void Respawn()
    {
        transform.position = respawnPoint;
        rb.linearVelocity = Vector2.zero;
        invincibleUntil = Time.time + invincibleDuration;
        hurtUntil = 0f;
        State = PlayerState.Idle;
    }

    void Die()
    {
        State = PlayerState.Dead;
        col.enabled = false; // atravessa o chão e cai para fora da tela
        rb.linearVelocity = new Vector2(0f, 12f);
        sr.enabled = true;
        sr.sprite = fallSprite;
        sr.color = new Color(1f, 0.5f, 0.5f);
    }
}
