using System;
using UnityEngine;

namespace BotanicalGardenQR.Fairy.Backend
{
    internal sealed class FairyInteractionController : MonoBehaviour
    {
        Action<bool> _onInteracted;
        bool _enlarged;

        internal void Initialize(Action<bool> onInteracted)
            => _onInteracted = onInteracted ?? throw new ArgumentNullException(nameof(onInteracted));

        // Platform input adapters call the typed controller intent; this method owns the fairy response.
        internal void Interact()
        {
            if (_onInteracted == null) return;
            _enlarged = !_enlarged;
            _onInteracted(_enlarged);
        }

        internal void Dispose() => _onInteracted = null;
    }
}
