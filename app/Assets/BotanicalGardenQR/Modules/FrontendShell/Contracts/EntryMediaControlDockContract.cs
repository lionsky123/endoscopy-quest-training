using System;
using UnityEngine;

namespace BotanicalGardenQR.FrontendShell.Contracts
{
    public enum EntryMediaControlDockMode
    {
        Hidden,
        Narration,
        Video,
        Preparing,
        Error,
        Completed,
        NarrationCompleted,
        VideoPreparing
    }

    public readonly struct EntryMediaControlDockViewModel
    {
        public EntryMediaControlDockViewModel(
            EntryMediaControlDockMode mode,
            string title,
            bool isPlaying,
            bool canReplay,
            bool isMuted = false,
            float progress01 = -1f)
            : this(
                mode,
                title,
                isPlaying,
                canReplay,
                isMuted,
                progress01,
                mode == EntryMediaControlDockMode.Narration || mode == EntryMediaControlDockMode.Video,
                mode == EntryMediaControlDockMode.Narration || mode == EntryMediaControlDockMode.Video ||
                mode == EntryMediaControlDockMode.Completed || mode == EntryMediaControlDockMode.NarrationCompleted)
        {
        }

        public EntryMediaControlDockViewModel(
            EntryMediaControlDockMode mode,
            string title,
            bool isPlaying,
            bool canReplay,
            bool isMuted,
            float progress01,
            bool canPauseResume,
            bool canMute)
        {
            Mode = mode;
            Title = title ?? "";
            IsPlaying = isPlaying;
            CanReplay = canReplay;
            CanPauseResume = canPauseResume;
            CanMute = canMute;
            IsMuted = isMuted;
            HasProgress = progress01 >= 0f;
            Progress01 = HasProgress ? Mathf.Clamp01(progress01) : 0f;
        }

        public EntryMediaControlDockMode Mode { get; }

        public string Title { get; }

        public bool IsPlaying { get; }

        public bool CanReplay { get; }

        public bool CanPauseResume { get; }

        public bool CanMute { get; }

        public bool IsMuted { get; }

        public float Progress01 { get; }

        public bool HasProgress { get; }

        public static EntryMediaControlDockViewModel Hidden =>
            new EntryMediaControlDockViewModel(EntryMediaControlDockMode.Hidden, "", false, false);

        public static EntryMediaControlDockViewModel Narration(string title, bool isPlaying, bool canReplay, float progress01 = -1f)
        {
            return Narration(title, isPlaying, canReplay, false, progress01);
        }

        public static EntryMediaControlDockViewModel Narration(string title, bool isPlaying, bool canReplay, bool isMuted, float progress01 = -1f)
        {
            return new EntryMediaControlDockViewModel(
                EntryMediaControlDockMode.Narration,
                title,
                isPlaying,
                canReplay,
                isMuted,
                progress01);
        }

        public static EntryMediaControlDockViewModel Video(string title, bool isPlaying, bool canReplay, float progress01 = -1f)
        {
            return Video(title, isPlaying, canReplay, false, progress01);
        }

        public static EntryMediaControlDockViewModel Video(string title, bool isPlaying, bool canReplay, bool isMuted, float progress01 = -1f)
        {
            return new EntryMediaControlDockViewModel(
                EntryMediaControlDockMode.Video,
                title,
                isPlaying,
                canReplay,
                isMuted,
                progress01);
        }
    }

    public readonly struct EntryMediaControlDockCallbacks
    {
        public EntryMediaControlDockCallbacks(
            Action onPauseResume,
            Action onReplay,
            Action onToggleVolume)
        {
            OnPauseResume = onPauseResume;
            OnReplay = onReplay;
            OnToggleVolume = onToggleVolume;
        }

        public Action OnPauseResume { get; }

        public Action OnReplay { get; }

        public Action OnToggleVolume { get; }
    }

    public interface IEntryMediaControlDock
    {
        void Render(
            EntryMediaControlDockViewModel viewModel,
            EntryMediaControlDockCallbacks callbacks);

        void UpdateProgress(float progress01, bool hasProgress);
    }
}
