using System;
using BotanicalGardenQR.Activation.Contracts;
using UnityEngine;

namespace BotanicalGardenQR.Activation.Runtime
{
    public sealed class ViewerGazeFocusQueryProvider : IRecognitionFocusQueryProvider
    {
        readonly Func<SpatialEvidence?> _viewerPose;
        readonly float _angularPaddingDegrees;

        public ViewerGazeFocusQueryProvider(
            Func<SpatialEvidence?> viewerPose,
            float angularPaddingDegrees)
        {
            _viewerPose = viewerPose ?? throw new ArgumentNullException(nameof(viewerPose));
            if (float.IsNaN(angularPaddingDegrees) || float.IsInfinity(angularPaddingDegrees) ||
                angularPaddingDegrees < 0f || angularPaddingDegrees > 10f)
                throw new ArgumentOutOfRangeException(nameof(angularPaddingDegrees));
            _angularPaddingDegrees = angularPaddingDegrees;
        }

        public bool TryGetQuery(out RecognitionFocusQuery query)
        {
            var viewerEvidence = _viewerPose();
            if (!viewerEvidence.HasValue || !viewerEvidence.Value.PoseIsValid)
            {
                query = default;
                return false;
            }

            var viewerForward = viewerEvidence.Value.Rotation * Vector3.forward;
            if (!IsUsableDirection(viewerForward))
            {
                query = default;
                return false;
            }

            query = new RecognitionFocusQuery(
                new Ray(viewerEvidence.Value.Position, viewerForward),
                _angularPaddingDegrees);
            return true;
        }

        static bool IsUsableDirection(Vector3 direction)
            => direction.sqrMagnitude > 0.000001f &&
               !float.IsNaN(direction.x) && !float.IsInfinity(direction.x) &&
               !float.IsNaN(direction.y) && !float.IsInfinity(direction.y) &&
               !float.IsNaN(direction.z) && !float.IsInfinity(direction.z);
    }
}
