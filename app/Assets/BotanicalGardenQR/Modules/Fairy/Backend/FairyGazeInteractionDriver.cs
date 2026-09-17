using System;
using UnityEngine;

namespace BotanicalGardenQR.Fairy.Backend
{
    /// <summary>
    /// Fairy-owned gaze adapter. It deliberately does not route through the
    /// exhibit shell: the fairy is a visitor-runtime companion with its own
    /// interaction lifecycle and typed Interact command.
    /// </summary>
    [DisallowMultipleComponent]
    internal sealed class FairyGazeInteractionDriver : MonoBehaviour
    {
        readonly RaycastHit[] _hits = new RaycastHit[8];
        Transform _viewer;
        FairyInteractionController _interaction;
        float _dwellSeconds = 0.9f;
        float _focusedSeconds;
        bool _activated;

        internal void Initialize(Transform viewer, FairyInteractionController interaction)
        {
            _viewer = viewer != null ? viewer : throw new ArgumentNullException(nameof(viewer));
            _interaction = interaction ?? throw new ArgumentNullException(nameof(interaction));
            var collider = GetComponent<Collider>();
            if (collider == null)
            {
                var target = gameObject.AddComponent<SphereCollider>();
                target.isTrigger = true;
                target.radius = 0.28f;
            }
        }

        void LateUpdate()
        {
            if (_viewer == null || _interaction == null || !isActiveAndEnabled)
                return;

            var ray = new Ray(_viewer.position, _viewer.forward);
            var hitCount = Physics.SphereCastNonAlloc(
                ray,
                0.018f,
                _hits,
                4f,
                ~0,
                QueryTriggerInteraction.Collide);
            var focused = false;
            for (var index = 0; index < hitCount; index++)
            {
                var hit = _hits[index];
                if (hit.collider == null || hit.collider.GetComponentInParent<FairyGazeInteractionDriver>() != this)
                    continue;
                focused = true;
                break;
            }

            if (!focused)
            {
                _focusedSeconds = 0f;
                _activated = false;
                return;
            }

            if (_activated) return;
            _focusedSeconds += Time.unscaledDeltaTime;
            if (_focusedSeconds < _dwellSeconds) return;
            _activated = true;
            _interaction.Interact();
        }
    }
}
