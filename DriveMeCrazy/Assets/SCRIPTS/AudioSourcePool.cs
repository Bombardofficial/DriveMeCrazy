using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Runtime-reusable AudioSource pool.
/// </summary>
public class AudioSourcePool : MonoBehaviour
{
    [Header("Pool Settings")]
    [Min(1)] public int initialSize = 16;
    public bool expandable = true;        // if true, pool grows when exhausted

    readonly Queue<AudioSource> _pool = new Queue<AudioSource>();

    void Awake()
    {
        // pre-warm
        for (int i = 0; i < initialSize; i++)
            _pool.Enqueue(CreateSource(i));
    }

    /* ---------- public API ---------- */
    public void Play3D(AudioClip clip, Vector3 pos, float volume = .7f, float pitch = 1f)
    {
        if (!clip) return;

        AudioSource src = GetSource();
        if (!src) return;                 // pool exhausted and not expandable

        src.transform.position = pos;
        src.clip = clip;
        src.volume = volume;
        src.pitch = pitch;
        src.Play();

        StartCoroutine(ReturnAfter(src, clip.length / Mathf.Max(0.01f, pitch)));
    }

    /* ---------- internals ---------- */
    AudioSource GetSource()
    {
        if (_pool.Count > 0) return _pool.Dequeue();
        return expandable ? CreateSource(_pool.Count) : null;
    }

    AudioSource CreateSource(int idx)
    {
        var go = new GameObject($"PooledAudioSource_{idx}");
        go.transform.SetParent(transform);
        var src = go.AddComponent<AudioSource>();
        src.playOnAwake = false;
        src.spatialBlend = 1f;           // full 3-D
        src.rolloffMode = AudioRolloffMode.Linear;
        src.minDistance = 1f;
        src.maxDistance = 30f;
        src.outputAudioMixerGroup = null; // hook up mixer group here if you use one
        return src;
    }

    IEnumerator ReturnAfter(AudioSource src, float delay)
    {
        yield return new WaitForSeconds(delay);
        src.Stop();
        src.clip = null;
        _pool.Enqueue(src);
    }
}
