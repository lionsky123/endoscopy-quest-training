using System;
using System.Collections.Generic;
using UnityEngine;

namespace BotanicalGardenQR.Fairy.Backend
{
    public enum FairyArrivalSound { Wake, Crack, Recoil, Stabilize, Connect, Appear, Cross, Step, Close }

    public sealed class FairyArrivalSoundEvents : MonoBehaviour
    {
        [SerializeField] AudioClip[] _clips;
        readonly HashSet<string> _played = new HashSet<string>();
        AudioSource _portal, _contact;
        Vector3 _door, _contactPosition;
        internal event Action<string, AudioClip, Vector3, float> Played;
        internal float DuckRemaining { get; private set; }
        internal void Initialize(Vector3 door)
        {
            if (_clips == null || _clips.Length != 9 || Array.Exists(_clips, clip => clip == null))
                throw new InvalidOperationException("Arrival requires nine configured event clips.");
            _door = _contactPosition = door;
            _portal = Source("ArrivalPortalSound", door);
            _contact = Source("ArrivalContactSound", door);
        }
        AudioSource Source(string label, Vector3 position)
        {
            var child = new GameObject(label); child.transform.SetParent(transform, false); child.transform.position = position;
            var source = child.AddComponent<AudioSource>(); source.playOnAwake = false;
            source.spatialBlend = .8f; source.minDistance = .65f; source.maxDistance = 8;
            source.dopplerLevel = 0; source.spatialize = true;
            child.AddComponent<MetaXRAudioSource>();
            source.rolloffMode = AudioRolloffMode.Linear;
            return source;
        }
        internal void Tick(float dt)
        {
            DuckRemaining = Mathf.Max(0, DuckRemaining - dt);
            _portal.transform.position = _door; _contact.transform.position = _contactPosition;
        }
        void LateUpdate()
        {
            if (_portal != null) _portal.transform.position = _door;
            if (_contact != null) _contact.transform.position = _contactPosition;
        }
        internal void Play(FairyArrivalSound sound, string key, Vector3? position = null, float volume = .65f)
        {
            if (!_played.Add(key)) return;
            var source = position.HasValue ? _contact : _portal;
            if (position.HasValue) { _contactPosition = position.Value; source.transform.position = _contactPosition; }
            else source.transform.position = _door;
            var clip = _clips[(int)sound];
            Played?.Invoke(key, clip, source.transform.position, volume);
            DuckRemaining = Mathf.Max(DuckRemaining, sound == FairyArrivalSound.Appear ? 1.1f : .45f);
            if (Application.isPlaying) source.PlayOneShot(clip, volume);
        }
        void OnDisable() { if (_portal != null) _portal.Stop(); if (_contact != null) _contact.Stop(); }
    }
}
