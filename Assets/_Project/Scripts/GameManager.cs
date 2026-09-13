using System;
using UnityEngine;

public sealed class GameManager : MonoBehaviour
{
    [SerializeField] private Coin[] coins;

    public event Action<int> CoinsChanged;

    public int CoinCount { get; private set; }

    private void OnEnable()
    {
        foreach (Coin coin in coins)
        {
            if (coin != null)
            {
                coin.Collected += HandleCoinCollected;
            }
        }
    }

    private void Start()
    {
        CoinsChanged?.Invoke(CoinCount);
    }

    private void OnDisable()
    {
        foreach (Coin coin in coins)
        {
            if (coin != null)
            {
                coin.Collected -= HandleCoinCollected;
            }
        }
    }

    private void HandleCoinCollected(Coin coin)
    {
        CoinCount++;
        CoinsChanged?.Invoke(CoinCount);
    }
}
