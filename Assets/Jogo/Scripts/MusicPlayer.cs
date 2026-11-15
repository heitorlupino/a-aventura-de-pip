using UnityEngine;

/// <summary>
/// Música de fundo chiptune original, sintetizada por código (melodia em onda quadrada,
/// baixo em onda triangular e bateria de ruído). Toca em loop no menu e durante o jogo,
/// abaixa na pausa e para no fim de jogo / vitória. Tecla N liga/desliga a música.
/// </summary>
public class MusicPlayer : MonoBehaviour
{
    [Range(0f, 1f)] public float volume = 0.45f;
    [Range(0f, 1f)] public float pausedVolumeMultiplier = 0.35f;
    public float fadeSpeed = 1.5f;

    const string MuteKey = "Plataforma2D_MusicaMudo";
    const int SampleRate = 22050;
    const float Bpm = 140f;

    static AudioClip cachedClip; // gerado uma vez e reaproveitado ao recarregar a fase

    AudioSource source;
    GameManager gm;
    float targetVolume;
    bool muted;

    // Melodia em notas MIDI, uma por colcheia (8 por compasso). 0 = segura a nota anterior, -1 = pausa.
    static readonly int[] Melody =
    {
        76, 0, 81, 0, 84, 83, 81, 76,   // Lá menor
        77, 0, 81, 0, 84, 0, 81, 0,     // Fá
        79, 0, 84, 0, 88, 86, 84, 79,   // Dó
        83, 0, 79, 0, 74, 0, -1, -1,    // Sol
        76, 76, 81, 0, 83, 84, 86, 0,   // Lá menor
        84, 0, 81, 0, 77, 0, 81, 0,     // Fá
        79, 0, 76, 79, 84, 0, 79, 0,    // Dó
        83, 0, 86, 0, 83, 0, 79, 0,     // Sol
    };

    // Nota fundamental do baixo em cada compasso.
    static readonly int[] BassRoots = { 45, 41, 48, 43, 45, 41, 48, 43 };

    void Awake()
    {
        source = gameObject.AddComponent<AudioSource>();
        source.loop = true;
        source.playOnAwake = false;
        source.volume = 0f;
        muted = PlayerPrefs.GetInt(MuteKey, 0) == 1;
    }

    void Start()
    {
        if (cachedClip == null) cachedClip = Compose();
        source.clip = cachedClip;
        source.Play();

        gm = GameManager.Instance;
        if (gm != null)
        {
            gm.StateChanged += OnStateChanged;
            OnStateChanged(gm.State);
        }
        else targetVolume = volume;
    }

    void OnDestroy()
    {
        if (gm != null) gm.StateChanged -= OnStateChanged;
    }

    void OnStateChanged(GameState state)
    {
        switch (state)
        {
            case GameState.Paused: targetVolume = volume * pausedVolumeMultiplier; break;
            case GameState.GameOver:
            case GameState.Victory: targetVolume = 0f; break; // deixa o jingle de fim tocar sozinho
            default: targetVolume = volume; break;
        }
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.N))
        {
            muted = !muted;
            PlayerPrefs.SetInt(MuteKey, muted ? 1 : 0);
        }

        float goal = muted ? 0f : targetVolume;
        // Tempo "unscaled" porque o jogo fica com timeScale = 0 nos menus.
        source.volume = Mathf.MoveTowards(source.volume, goal, fadeSpeed * Time.unscaledDeltaTime);
    }

    // ------------------------------------------------------------------ síntese

    static AudioClip Compose()
    {
        int stepSamples = Mathf.RoundToInt(SampleRate * 60f / Bpm / 2f); // colcheia
        int steps = Melody.Length;
        int total = stepSamples * steps;
        var data = new float[total];
        var rng = new System.Random(1234);

        // Melodia (onda quadrada com duty 25% e envelope simples)
        for (int i = 0; i < steps; i++)
        {
            int note = Melody[i];
            if (note <= 0) continue;
            int length = 1;
            while (i + length < steps && Melody[i + length] == 0) length++;
            AddNote(data, i * stepSamples, length * stepSamples, Freq(note), 0.10f, 0.25f, false);
        }

        // Baixo (triangular, alternando oitavas)
        int stepsPerBar = 8;
        for (int i = 0; i < steps; i++)
        {
            int root = BassRoots[(i / stepsPerBar) % BassRoots.Length];
            int note = (i % 2 == 0) ? root : root + 12;
            AddNote(data, i * stepSamples, stepSamples, Freq(note), 0.16f, 0.5f, true);
        }

        // Bateria: bumbo nos tempos 1 e 3, caixa nos tempos 2 e 4, chimbal em toda colcheia
        for (int i = 0; i < steps; i++)
        {
            int start = i * stepSamples;
            int inBar = i % stepsPerBar;
            if (inBar == 0 || inBar == 4) AddKick(data, start, 0.35f);
            if (inBar == 2 || inBar == 6) AddNoise(data, start, 0.11f, 0.12f, rng);
            AddNoise(data, start, 0.025f, inBar % 2 == 1 ? 0.05f : 0.03f, rng);
        }

        // Normaliza para evitar distorção
        float peak = 0.0001f;
        foreach (var s in data) peak = Mathf.Max(peak, Mathf.Abs(s));
        float gain = 0.9f / peak;
        for (int i = 0; i < total; i++) data[i] *= gain;

        var clip = AudioClip.Create("MusicaFundo", total, 1, SampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }

    static float Freq(int midi) => 440f * Mathf.Pow(2f, (midi - 69) / 12f);

    static void AddNote(float[] data, int start, int length, float freq, float vol, float duty, bool triangle)
    {
        int attack = SampleRate / 200;
        int release = Mathf.Min(length / 3, SampleRate / 25);
        for (int n = 0; n < length && start + n < data.Length; n++)
        {
            float phase = (n * freq / SampleRate) % 1f;
            float wave = triangle ? (4f * Mathf.Abs(phase - 0.5f) - 1f) : (phase < duty ? 1f : -1f);
            float env = 1f;
            if (n < attack) env = (float)n / attack;
            else env = Mathf.Lerp(1f, 0.6f, (float)n / length);
            if (n > length - release) env *= (float)(length - n) / release;
            data[start + n] += wave * vol * env;
        }
    }

    static void AddKick(float[] data, int start, float vol)
    {
        int length = (int)(SampleRate * 0.12f);
        float phase = 0f;
        for (int n = 0; n < length && start + n < data.Length; n++)
        {
            float t = (float)n / length;
            phase += Mathf.Lerp(150f, 45f, t) / SampleRate;
            data[start + n] += Mathf.Sin(phase * 2f * Mathf.PI) * vol * (1f - t);
        }
    }

    static void AddNoise(float[] data, int start, float seconds, float vol, System.Random rng)
    {
        int length = (int)(SampleRate * seconds);
        for (int n = 0; n < length && start + n < data.Length; n++)
        {
            float t = (float)n / length;
            data[start + n] += ((float)rng.NextDouble() * 2f - 1f) * vol * (1f - t) * (1f - t);
        }
    }
}
