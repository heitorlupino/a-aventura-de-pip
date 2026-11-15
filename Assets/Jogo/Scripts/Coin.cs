/// <summary>Moeda: soma pontos ao ser coletada.</summary>
public class Coin : Pickup
{
    protected override void OnCollected() => GameManager.Instance?.CollectCoin();
}
