using UnityEngine;

public sealed class Coin : MonoBehaviour
{
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.GetComponent<PlayerController>() != null)
        {
            Destroy(gameObject);
        }
    }
}
