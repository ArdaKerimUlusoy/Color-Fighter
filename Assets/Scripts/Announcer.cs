using System.Collections.Generic;
using UnityEngine;

public class Announcer : MonoBehaviour
{
    #region Ayarlar

    [Range(0f, 1f)] public float volume = 1f;
    [Tooltip("Arka arkaya söylenen replikler arasında, yankı bitmeden bir sonrakine geçme payı (sn).")]
    public float overlap = 0.3f;

    #endregion

    #region Durum

    static Announcer instance;
    public static bool Muted;

    public static bool Speaking => instance != null && (instance.queue.Count > 0 || Time.unscaledTime < instance.speakingUntil);
    float speakingUntil;

    AudioSource src;
    readonly Queue<AudioClip> queue = new Queue<AudioClip>();
    readonly Dictionary<string, AudioClip> cache = new Dictionary<string, AudioClip>();
    float nextAt;

    static Announcer I
    {
        get
        {
            if (instance == null)
            {
                var go = new GameObject("Announcer");
                DontDestroyOnLoad(go);
                instance = go.AddComponent<Announcer>();
            }
            return instance;
        }
    }

    void Awake()
    {
        src = gameObject.AddComponent<AudioSource>();
        src.playOnAwake = false;
        src.spatialBlend = 0f;
        src.ignoreListenerPause = true;
    }

    #endregion

    #region Dış API

    public static void Say(string key, bool interrupt = false)
    {
        if (Muted || string.IsNullOrEmpty(key)) return;
        var a = I;
        var clip = a.Load(key);
        if (clip == null) return;
        if (interrupt) a.Stop();
        a.queue.Enqueue(clip);
    }

    public static void SayAfter(string key, float delay)
    {
        if (Muted || string.IsNullOrEmpty(key)) return;
        var a = I;
        var clip = a.Load(key);
        if (clip == null) return;
        a.nextAt = Mathf.Max(a.nextAt, Time.unscaledTime + delay);
        a.queue.Enqueue(clip);
    }

    public static void Clear()
    {
        if (instance != null) instance.Stop();
    }

    void Stop()
    {
        queue.Clear();
        src.Stop();
        nextAt = 0f;
    }

    AudioClip Load(string key)
    {
        if (cache.TryGetValue(key, out var c)) return c;
        c = Resources.Load<AudioClip>("Announcer/" + key);
        cache[key] = c;
        return c;
    }

    #endregion

    #region Oynatma

    void Update()
    {
        if (queue.Count == 0 || Time.unscaledTime < nextAt) return;
        var clip = queue.Dequeue();
        src.PlayOneShot(clip, volume);
        nextAt = Time.unscaledTime + Mathf.Max(0.15f, clip.length - overlap);
        speakingUntil = Time.unscaledTime + clip.length;
    }

    #endregion
}
