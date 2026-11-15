using System.Collections.Generic;
using UnityEngine;

public enum Sfx { Jump, Coin, Stomp, Hurt, Life, Checkpoint, Victory, GameOver }

/// <summary>
/// Gera efeitos sonoros "8-bit" por código (onda quadrada),
/// assim o projeto não depende de arquivos de áudio.
/// </summary>
public static class SfxLibrary
{
    const int SampleRate = 44100;
    static readonly Dictionary<Sfx, AudioClip> cache = new Dictionary<Sfx, AudioClip>();

    public static AudioClip Get(Sfx sfx)
    {
        if (cache.TryGetValue(sfx, out var clip) && clip != null) return clip;

        switch (sfx)
        {
            case Sfx.Jump: clip = Sweep("pulo", 300f, 700f, 0.14f, 0.12f); break;
            case Sfx.Coin: clip = Notes("moeda", new[] { 988f, 1319f }, 0.07f, 0.12f); break;
            case Sfx.Stomp: clip = Sweep("pisao", 420f, 110f, 0.12f, 0.16f); break;
            case Sfx.Hurt: clip = Sweep("dano", 520f, 90f, 0.3f, 0.16f); break;
            case Sfx.Life: clip = Notes("vida", new[] { 523f, 659f, 784f, 1047f }, 0.08f, 0.12f); break;
            case Sfx.Checkpoint: clip = Notes("checkpoint", new[] { 659f, 880f }, 0.1f, 0.12f); break;
            case Sfx.Victory: clip = Notes("vitoria", new[] { 523f, 659f, 784f, 1047f, 784f, 1047f }, 0.12f, 0.14f); break;
            default: clip = Notes("gameover", new[] { 392f, 330f, 262f, 196f }, 0.2f, 0.14f); break;
        }
        cache[sfx] = clip;
        return clip;
    }

    static AudioClip Sweep(string name, float fromHz, float toHz, float duration, float volume)
    {
        int n = Mathf.CeilToInt(SampleRate * duration);
        var data = new float[n];
        float phase = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / n;
            phase += Mathf.Lerp(fromHz, toHz, t) / SampleRate;
            data[i] = Square(phase) * volume * (1f - t);
        }
        return Make(name, data);
    }

    static AudioClip Notes(string name, float[] freqs, float noteDuration, float volume)
    {
        int per = Mathf.CeilToInt(SampleRate * noteDuration);
        var data = new float[per * freqs.Length];
        float phase = 0f;
        for (int k = 0; k < freqs.Length; k++)
        {
            for (int i = 0; i < per; i++)
            {
                float t = (float)i / per;
                phase += freqs[k] / SampleRate;
                data[k * per + i] = Square(phase) * volume * (1f - 0.7f * t);
            }
        }
        return Make(name, data);
    }

    static float Square(float phase) => (phase % 1f) < 0.5f ? 1f : -1f;

    static AudioClip Make(string name, float[] data)
    {
        var clip = AudioClip.Create(name, data.Length, 1, SampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }
}
