using UnityEngine;

/// <summary>Checkpoint: ao ser tocado, vira o novo ponto de renascimento do jogador.</summary>
[RequireComponent(typeof(Collider2D))]
public class Checkpoint : MonoBehaviour
{
    public SpriteRenderer spriteRenderer;
    public Sprite inactiveSprite, activeSprite;

    bool active;

    void OnTriggerEnter2D(Collider2D other)
    {
        if (active) return;
        var player = other.GetComponent<PlayerController>();
        if (player == null) return;

        active = true;
        if (spriteRenderer != null) spriteRenderer.sprite = activeSprite;
        player.SetCheckpoint(transform.position + Vector3.up * 0.6f);
        GameManager.Instance?.PlaySfx(Sfx.Checkpoint);
    }
}
