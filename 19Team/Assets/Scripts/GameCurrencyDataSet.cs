using UnityEngine;

[CreateAssetMenu(fileName = "GameCurrencyDataSet", menuName = "Game Data/Game Currency Data Set")]
public sealed class GameCurrencyDataSet : ScriptableObject
{
    [SerializeField] private int _maxPaws = GameCurrencyStore.MaxPaws;
    [SerializeField] private int _recoveryMinutes = 10;

    public int MaxPaws => _maxPaws;
    public int RecoveryMinutes => _recoveryMinutes;
    public int Paws => GameCurrencyStore.GetPaws();
    public int Bones => GameCurrencyStore.GetBones();
    public int SecondsUntilNextPaw => GameCurrencyStore.SecondsUntilNextPaw();
    public bool TryEnterGame() => GameCurrencyStore.TrySpendPaw(true);
}
