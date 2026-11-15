using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Estados globais do jogo (máquina de estados).</summary>
public enum GameState { Menu, Playing, Paused, GameOver, Victory }

/// <summary>
/// Controla o fluxo do jogo: estados, pontuação, vidas, moedas, tempo,
/// recorde (PlayerPrefs) e efeitos sonoros.
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    // Quando o jogador reinicia, a fase recarrega direto no estado "Playing".
    static bool startPlayingOnLoad;

    [Header("Regras")]
    public int startingLives = 3;
    public int maxLives = 5;
    public int coinValue = 10;
    public int enemyValue = 50;
    public int lifeBonusOnVictory = 100;
    [Tooltip("Preenchido automaticamente pelo montador da fase.")]
    public int totalCoins;

    public GameState State { get; private set; } = GameState.Menu;
    public int Score { get; private set; }
    public int Lives { get; private set; }
    public int Coins { get; private set; }
    public float ElapsedTime { get; private set; }
    public bool IsNewHighScore { get; private set; }
    public int HighScore => PlayerPrefs.GetInt(HighScoreKey, 0);

    const string HighScoreKey = "Plataforma2D_Recorde";

    public event Action<GameState> StateChanged;
    public event Action StatsChanged;
    public event Action PlayerDamaged;

    AudioSource audioSource;

    void Awake()
    {
        Instance = this;
        audioSource = GetComponent<AudioSource>();
        audioSource.playOnAwake = false;
        Lives = startingLives;

        // Música de fundo (adicionada automaticamente se ainda não existir).
        if (GetComponent<MusicPlayer>() == null) gameObject.AddComponent<MusicPlayer>();
    }

    void Start()
    {
        SetState(startPlayingOnLoad ? GameState.Playing : GameState.Menu);
        startPlayingOnLoad = false;
        StatsChanged?.Invoke();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        Time.timeScale = 1f;
    }

    void Update()
    {
        // Entradas pelo Input Manager (teclado e controle).
        bool confirm = Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)
                       || Input.GetKeyDown(KeyCode.JoystickButton7);
        bool pause = Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.P)
                     || Input.GetKeyDown(KeyCode.JoystickButton7);
        bool restart = Input.GetKeyDown(KeyCode.R);
        bool toMenu = Input.GetKeyDown(KeyCode.M);

        switch (State)
        {
            case GameState.Menu:
                if (confirm) SetState(GameState.Playing);
                break;

            case GameState.Playing:
                ElapsedTime += Time.deltaTime;
                if (pause) SetState(GameState.Paused);
                break;

            case GameState.Paused:
                if (pause) SetState(GameState.Playing);
                else if (restart) ReloadLevel(true);
                else if (toMenu) ReloadLevel(false);
                break;

            case GameState.GameOver:
            case GameState.Victory:
                if (confirm || restart) ReloadLevel(true);
                else if (toMenu) ReloadLevel(false);
                break;
        }
    }

    public void SetState(GameState newState)
    {
        State = newState;
        // Pausa a física e as animações fora do estado "Playing".
        Time.timeScale = newState == GameState.Playing ? 1f : 0f;

        if (newState == GameState.GameOver || newState == GameState.Victory)
            SaveHighScore();

        StateChanged?.Invoke(newState);
    }

    // ---------- Pontuação / vidas ----------

    public void AddScore(int amount)
    {
        Score += amount;
        StatsChanged?.Invoke();
    }

    public void CollectCoin()
    {
        Coins++;
        Score += coinValue;
        PlaySfx(Sfx.Coin);
        StatsChanged?.Invoke();
    }

    public void EnemyDefeated()
    {
        Score += enemyValue;
        PlaySfx(Sfx.Stomp);
        StatsChanged?.Invoke();
    }

    /// <summary>Ganha uma vida. Se já estiver no máximo, ganha pontos.</summary>
    public void AddLife()
    {
        if (Lives >= maxLives)
        {
            AddScore(100);
            PlaySfx(Sfx.Coin);
            return;
        }
        Lives++;
        PlaySfx(Sfx.Life);
        StatsChanged?.Invoke();
    }

    /// <summary>Tira uma vida. Retorna true se o jogador ainda está vivo.</summary>
    public bool DamagePlayer()
    {
        if (State != GameState.Playing) return Lives > 0;

        Lives = Mathf.Max(0, Lives - 1);
        PlaySfx(Sfx.Hurt);
        PlayerDamaged?.Invoke();
        StatsChanged?.Invoke();

        if (Lives <= 0) StartCoroutine(GameOverRoutine());
        return Lives > 0;
    }

    IEnumerator GameOverRoutine()
    {
        // Espera a animação de morte antes de mostrar a tela de Game Over.
        yield return new WaitForSeconds(1.2f);
        PlaySfx(Sfx.GameOver);
        SetState(GameState.GameOver);
    }

    public void Win()
    {
        if (State != GameState.Playing) return;
        Score += Lives * lifeBonusOnVictory;
        PlaySfx(Sfx.Victory);
        StatsChanged?.Invoke();
        SetState(GameState.Victory);
    }

    void SaveHighScore()
    {
        IsNewHighScore = Score > HighScore;
        if (IsNewHighScore)
        {
            PlayerPrefs.SetInt(HighScoreKey, Score);
            PlayerPrefs.Save();
        }
    }

    public void ReloadLevel(bool playImmediately)
    {
        startPlayingOnLoad = playImmediately;
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    public void PlaySfx(Sfx sfx)
    {
        if (audioSource != null) audioSource.PlayOneShot(SfxLibrary.Get(sfx));
    }
}
