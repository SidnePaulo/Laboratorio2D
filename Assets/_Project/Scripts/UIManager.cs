using UnityEngine;
using UnityEngine.UI;

public sealed class UIManager : MonoBehaviour
{
    [SerializeField] private GameManager gameManager;
    [SerializeField] private Image coinIcon;
    [SerializeField] private Text coinText;

    private void OnEnable()
    {
        gameManager.CoinsChanged += UpdateCoinText;
        UpdateCoinText(gameManager.CoinCount);
    }

    private void OnDisable()
    {
        gameManager.CoinsChanged -= UpdateCoinText;
    }

    private void UpdateCoinText(int count)
    {
        coinText.text = count.ToString("D2");
    }
}
