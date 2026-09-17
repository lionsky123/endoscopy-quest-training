using System;
using UnityEngine;

namespace BotanicalGardenQR.Fairy.Contracts
{
    public enum FairyDialogueReaction { Listening, Welcome, Wonder }
    public enum FairyIntent
    {
        Show,
        Hide,
        Interact
    }

    public enum FairyCompanionCueKind
    {
        Idle = 0,
        ArtifactWait = 1,
        ArtifactReturn = 2,
        Celebrate = 3,
        CoachAttention = 4,
        DialogueFocus = 5
    }

    /// <summary>
    /// A short-lived, already-resolved companion cue. Position is meaningful only
    /// for spatial cues; CoachAttention deliberately carries no target. The Fairy
    /// never receives an ArtifactId, SceneId, route, QR payload, or collection rule.
    /// </summary>
    public readonly struct FairyCompanionCue
    {
        public FairyCompanionCue(
            FairyCompanionCueKind kind,
            Vector3 worldPosition,
            float durationSeconds = 0f,
            FairyDialogueReaction dialogueReaction = FairyDialogueReaction.Listening)
        {
            if (!Enum.IsDefined(typeof(FairyCompanionCueKind), kind))
                throw new ArgumentOutOfRangeException(nameof(kind));
            var hasWorldTarget = kind != FairyCompanionCueKind.Idle &&
                                 kind != FairyCompanionCueKind.CoachAttention;
            if (kind != FairyCompanionCueKind.Idle && !IsFinite(worldPosition))
                throw new ArgumentOutOfRangeException(nameof(worldPosition));
            if (durationSeconds < 0f || float.IsNaN(durationSeconds) || float.IsInfinity(durationSeconds))
                throw new ArgumentOutOfRangeException(nameof(durationSeconds));
            if (kind == FairyCompanionCueKind.CoachAttention && durationSeconds <= 0f)
                throw new ArgumentOutOfRangeException(nameof(durationSeconds));

            Kind = kind;
            if (!Enum.IsDefined(typeof(FairyDialogueReaction), dialogueReaction))
                throw new ArgumentOutOfRangeException(nameof(dialogueReaction));
            DialogueReaction = dialogueReaction;
            WorldPosition = hasWorldTarget ? worldPosition : Vector3.zero;
            DurationSeconds = kind == FairyCompanionCueKind.Idle ? 0f : durationSeconds;
        }

        public FairyCompanionCueKind Kind { get; }
        public FairyDialogueReaction DialogueReaction { get; }
        public Vector3 WorldPosition { get; }
        public float DurationSeconds { get; }
        public static FairyCompanionCue Idle =>
            new FairyCompanionCue(FairyCompanionCueKind.Idle, Vector3.zero);
        public static FairyCompanionCue CoachAttention(float durationSeconds) =>
            new FairyCompanionCue(FairyCompanionCueKind.CoachAttention, Vector3.zero, durationSeconds);
        public static FairyCompanionCue DialogueFocus(Vector3 worldPosition,
            FairyDialogueReaction reaction = FairyDialogueReaction.Listening) =>
            new FairyCompanionCue(FairyCompanionCueKind.DialogueFocus, worldPosition, dialogueReaction: reaction);

        static bool IsFinite(Vector3 value)
            => IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);

        static bool IsFinite(float value)
            => !float.IsNaN(value) && !float.IsInfinity(value);
    }

}
