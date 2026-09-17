using System;
using BotanicalGardenQR.Collection.Frontend;
using BotanicalGardenQR.Fairy.Backend;
using BotanicalGardenQR.Fairy.Contracts;
using UnityEngine;

namespace BotanicalGardenQR.Bootstrap
{
    /// <summary>
    /// Composition-root adapter between collection presentation semantics and the
    /// application-global Fairy. Collection remains fully usable when Fairy is unavailable.
    /// </summary>
    internal sealed class VisitorCollectionFairyBinding : IDisposable
    {
        readonly CollectionWorldFrontend _collection;
        readonly FairyCompanionBinding _fairy;
        bool _disposed;
        FairyCompanionCueKind? _failedCue;

        internal VisitorCollectionFairyBinding(
            CollectionWorldFrontend collection,
            FairyCompanionBinding fairy)
        {
            _collection = collection != null ? collection : throw new ArgumentNullException(nameof(collection));
            _fairy = fairy ?? throw new ArgumentNullException(nameof(fairy));
            _collection.CompanionCueChanged += HandleCueChanged;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _collection.CompanionCueChanged -= HandleCueChanged;
            PresentBestEffort(FairyCompanionCue.Idle);
        }

        void HandleCueChanged(CollectionCompanionCue cue)
        {
            if (_disposed) return;
            try
            {
                PresentBestEffort(MapCue(cue));
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"Fairy ignored an invalid collection companion cue: {exception.Message}");
            }
        }

        void PresentBestEffort(FairyCompanionCue cue)
        {
            try
            {
                var result = _fairy.PresentCue(cue);
                if (!result.Succeeded && _failedCue != cue.Kind)
                {
                    _failedCue = cue.Kind;
                    Debug.LogWarning(
                        $"Fairy could not present collection cue '{cue.Kind}': " +
                        $"{result.FailureCode} ({result.DiagnosticTag}).");
                }
                if (result.Succeeded) _failedCue = null;
            }
            catch (Exception exception)
            {
                if (_failedCue != cue.Kind)
                    Debug.LogWarning($"Fairy collection cue failed without interrupting collection: {exception.Message}");
                _failedCue = cue.Kind;
            }
        }

        internal static FairyCompanionCue MapCue(CollectionCompanionCue cue)
        {
            switch (cue.Kind)
            {
                case CollectionCompanionCueKind.Idle:
                    return FairyCompanionCue.Idle;
                case CollectionCompanionCueKind.WaitAtArtifact:
                    return new FairyCompanionCue(
                        FairyCompanionCueKind.ArtifactWait,
                        cue.WorldPosition,
                        cue.DurationSeconds);
                case CollectionCompanionCueKind.ReturnWithArtifact:
                    return new FairyCompanionCue(
                        FairyCompanionCueKind.ArtifactReturn,
                        cue.WorldPosition,
                        cue.DurationSeconds);
                case CollectionCompanionCueKind.Celebrate:
                    return new FairyCompanionCue(
                        FairyCompanionCueKind.Celebrate,
                        cue.WorldPosition,
                        cue.DurationSeconds);
                default:
                    throw new ArgumentOutOfRangeException(nameof(cue), cue.Kind, "Unknown collection companion cue.");
            }
        }
    }
}
