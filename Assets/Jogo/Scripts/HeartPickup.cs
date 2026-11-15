/// <summary>Coração: dá uma vida extra ao ser coletado.</summary>
public class HeartPickup : Pickup
{
    protected override void Awake()
    {
        spin = false;
        base.Awake();
    }

    protected override void OnCollected() => GameManager.Instance?.AddLife();
}
