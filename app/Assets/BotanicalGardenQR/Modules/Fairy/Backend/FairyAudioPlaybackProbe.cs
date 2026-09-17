using System;
using System.Collections;
using UnityEngine;

namespace BotanicalGardenQR.Fairy.Backend
{
    /// <summary>Bounded signal observation, not a claim of speaker audibility.</summary>
    internal static class FairyAudioPlaybackProbe
    {
        internal static IEnumerator Observe(AudioSource source, AudioClip clip, Transform listener, string cue)
        {
            if (clip == null) yield break;
            var start = AudioSettings.dspTime;
            var deadline = Time.realtimeSinceStartup + .45f;
            var samples = new float[256];
            var peak = 0f;
            var playing = false;
            while (source != null && source.isActiveAndEnabled && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
                if (source == null || !source.isActiveAndEnabled) yield break;
                playing |= source.isPlaying;
                source.GetOutputData(samples, 0);
                foreach (var sample in samples) peak = Mathf.Max(peak, Mathf.Abs(sample));
            }
            if (source == null) yield break;
            var distance = listener != null ? Vector3.Distance(source.transform.position, listener.position) : -1f;
            Debug.Log(FormattableString.Invariant(
                $"[FAIRY_AUDIO_PROBE] cue={cue}; clip={clip.name}; loaded={clip.loadState}; played={playing}; source_peak={peak:F6}; dsp_advanced={AudioSettings.dspTime > start}; distance={distance:F2}; volume={source.volume:F3}; muted={source.mute}; virtual={source.isVirtual}; spatialize={source.spatialize}; plugin={AudioSettings.GetSpatializerPluginName()}; listener_pause={AudioListener.pause}; listener_volume={AudioListener.volume:F2}"));
        }
    }
}
