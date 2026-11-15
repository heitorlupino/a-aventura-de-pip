using UnityEngine;

/// <summary>Perigo (espinhos): causa dano enquanto o jogador estiver encostando.</summary>
[RequireComponent(typeof(Collider2D))]
public class Hazard : MonoBehaviour
{
    void OnTriggerStay2D(Collider2D other)
    {
        var player = other.GetComponent<PlayerController>();
        if (player != null) player.TakeDamage(transform.position);
    }
}
