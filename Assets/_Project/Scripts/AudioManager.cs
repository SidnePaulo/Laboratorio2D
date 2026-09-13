using UnityEngine;

public sealed class AudioManager : MonoBehaviour
{
    [SerializeField] private GameManager gameManager;
    [SerializeField] private AudioSource musicSource;
    [SerializeField] private AudioSource sfxSource;
    [SerializeField] private AudioClip musicClip;
    [SerializeField] private AudioClip coinClip;
    [SerializeField] private AudioClip barrelClip;

    private int lastCoinCount;

    private void OnEnable()
    {
        lastCoinCount = gameManager.CoinCount;
        gameManager.CoinsChanged += HandleCoinsChanged;
    }

    private void Start()
    {
        musicSource.clip = musicClip;
        musicSource.loop = true;
        musicSource.volume = 0.35f;
        musicSource.Play();
    }

    private void OnDisable()
    {
        gameManager.CoinsChanged -= HandleCoinsChanged;
    }

    private void HandleCoinsChanged(int count)
    {
        if (count > lastCoinCount)
        {
            sfxSource.PlayOneShot(coinClip, 0.8f);
        }

        lastCoinCount = count;
    }
}
