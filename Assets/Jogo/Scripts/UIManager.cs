using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Interface: HUD (corações, moedas, pontos, tempo), telas de menu/pausa/fim
/// e feedback visual (flash de dano, textos pulsando, "pulo" dos números).
/// </summary>
public class UIManager : MonoBehaviour
{
    [Header("HUD")]
    public GameObject hud;
    public Text scoreText, coinsText, timeText;
    public Image[] heartIcons;
    public Sprite heartFull, heartEmpty;

    [Header("Telas")]
    public GameObject menuPanel, pausePanel, gameOverPanel, victoryPanel;
    public Text menuHighScoreText, gameOverInfoText, victoryInfoText;
    public Text[] pulsingTexts;

    [Header("Feedback")]
    public Image damageFlash;

    GameManager gm;
    float flashAlpha;
    int lastScore, lastCoins;

    void Start()
    {
        gm = GameManager.Instance;
        if (gm == null) { enabled = false; return; }

        gm.StateChanged += OnStateChanged;
        gm.StatsChanged += RefreshStats;
        gm.PlayerDamaged += OnPlayerDamaged;
        lastScore = gm.Score;
        lastCoins = gm.Coins;
        OnStateChanged(gm.State);
        RefreshStats();
    }

    void OnDestroy()
    {
        if (gm == null) return;
        gm.StateChanged -= OnStateChanged;
        gm.StatsChanged -= RefreshStats;
        gm.PlayerDamaged -= OnPlayerDamaged;
    }

    void Update()
    {
        float dt = Time.unscaledDeltaTime; // o jogo fica com timeScale = 0 nos menus

        if (timeText != null) timeText.text = FormatTime(gm.ElapsedTime);

        if (damageFlash != null)
        {
            flashAlpha = Mathf.MoveTowards(flashAlpha, 0f, dt * 1.5f);
            var c = damageFlash.color; c.a = flashAlpha; damageFlash.color = c;
        }

        float pulse = 0.45f + 0.55f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 3f));
        if (pulsingTexts != null)
            foreach (var t in pulsingTexts)
                if (t != null) { var c = t.color; c.a = pulse; t.color = c; }

        ScaleBack(scoreText, dt);
        ScaleBack(coinsText, dt);
    }

    static void ScaleBack(Text t, float dt)
    {
        if (t == null) return;
        t.rectTransform.localScale = Vector3.Lerp(t.rectTransform.localScale, Vector3.one, 12f * dt);
    }

    static void Punch(Text t)
    {
        if (t != null) t.rectTransform.localScale = Vector3.one * 1.3f;
    }

    void OnStateChanged(GameState state)
    {
        hud.SetActive(state == GameState.Playing || state == GameState.Paused);
        menuPanel.SetActive(state == GameState.Menu);
        pausePanel.SetActive(state == GameState.Paused);
        gameOverPanel.SetActive(state == GameState.GameOver);
        victoryPanel.SetActive(state == GameState.Victory);

        const string gold = "#FEE761";
        string record = gm.IsNewHighScore
            ? $"\n<color={gold}>NOVO RECORDE!</color>"
            : $"\nMelhor pontuação: <color={gold}>{gm.HighScore}</color>";

        if (state == GameState.Menu)
        {
            menuHighScoreText.text = $"Melhor pontuação: <color={gold}>{gm.HighScore}</color>";
        }
        else if (state == GameState.GameOver)
        {
            gameOverInfoText.text =
                $"Pontos: <color={gold}>{gm.Score}</color>\n" +
                $"Moedas: <color={gold}>{gm.Coins}/{gm.totalCoins}</color>{record}";
        }
        else if (state == GameState.Victory)
        {
            victoryInfoText.text =
                $"Pontos: <color={gold}>{gm.Score}</color>\n" +
                $"Moedas: <color={gold}>{gm.Coins}/{gm.totalCoins}</color>\n" +
                $"Tempo: <color={gold}>{FormatTime(gm.ElapsedTime)}</color>{record}";
        }
    }

    void RefreshStats()
    {
        if (gm.Score != lastScore) Punch(scoreText);
        if (gm.Coins != lastCoins) Punch(coinsText);
        lastScore = gm.Score;
        lastCoins = gm.Coins;

        scoreText.text = gm.Score.ToString("000000");
        coinsText.text = $"x {gm.Coins}/{gm.totalCoins}";

        if (heartIcons != null)
        {
            int slots = Mathf.Max(gm.Lives, gm.startingLives);
            for (int i = 0; i < heartIcons.Length; i++)
            {
                if (heartIcons[i] == null) continue;
                heartIcons[i].gameObject.SetActive(i < slots);
                heartIcons[i].sprite = i < gm.Lives ? heartFull : heartEmpty;
            }
        }
    }

    void OnPlayerDamaged() => flashAlpha = 0.45f;

    public static string FormatTime(float seconds)
    {
        int m = (int)(seconds / 60f);
        int s = (int)(seconds % 60f);
        return $"{m:00}:{s:00}";
    }
}
