using System;

namespace BotanicalGardenQR.Activation.Contracts
{
    public enum SpatialDataPermissionState
    {
        Unknown = 0,
        Requesting = 1,
        Granted = 2,
        Denied = 3,
        PermanentlyDenied = 4,
        Unsupported = 5
    }

    public enum SpatialDataPermissionCommandFailure
    {
        None = 0,
        Disposed = 1,
        RequestInProgress = 2,
        Unsupported = 3,
        PlatformFailure = 4
    }

    public readonly struct SpatialDataPermissionCommandResult
    {
        SpatialDataPermissionCommandResult(
            bool succeeded,
            SpatialDataPermissionCommandFailure failure)
        {
            Succeeded = succeeded;
            Failure = failure;
        }

        public bool Succeeded { get; }
        public SpatialDataPermissionCommandFailure Failure { get; }

        public static SpatialDataPermissionCommandResult Success =>
            new SpatialDataPermissionCommandResult(true, SpatialDataPermissionCommandFailure.None);

        public static SpatialDataPermissionCommandResult Reject(
            SpatialDataPermissionCommandFailure failure)
        {
            if (failure == SpatialDataPermissionCommandFailure.None)
                throw new ArgumentException(
                    "A rejected spatial-data permission command requires a failure code.",
                    nameof(failure));
            return new SpatialDataPermissionCommandResult(false, failure);
        }
    }

    /// <summary>
    /// Application contract for the one Meta Spatial Data permission. Platform
    /// permission strings, callbacks and lifecycle APIs remain behind its adapter.
    /// </summary>
    public interface ISpatialDataPermissionGate : IDisposable
    {
        SpatialDataPermissionState CurrentState { get; }
        SpatialDataPermissionCommandResult Request();
        SpatialDataPermissionCommandResult Refresh();
        IDisposable Observe(ISpatialDataPermissionStateSink sink);
    }

    public interface ISpatialDataPermissionStateSink
    {
        void OnSpatialDataPermissionStateChanged(SpatialDataPermissionState state);
    }
}
