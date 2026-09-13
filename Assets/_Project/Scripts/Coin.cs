using System;
using UnityEngine;

public sealed class Coin : MonoBehaviour
{
    public event Action<Coin> Collected;

    private bool isCollected;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (isCollected || !other.CompareTag("Player"))
        {
            return;
        }

        isCollected = true;
        Collected?.Invoke(this);
        gameObject.SetActive(false);
        Destroy(gameObject);
    }
}
