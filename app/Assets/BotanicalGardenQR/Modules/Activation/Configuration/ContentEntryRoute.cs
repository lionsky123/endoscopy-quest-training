using System;
using BotanicalGardenQR.Activation.Contracts;
using BotanicalGardenQR.Experience.Contracts;
using BotanicalGardenQR.SpatialHost.Contracts;
using UnityEngine;

namespace BotanicalGardenQR.Configuration.Runtime
{
    /// <summary>
    /// Binds one external entry fact to one content SceneId.
    /// Content resources remain owned by ContentSceneConfig.
    /// </summary>
    [Serializable]
    public sealed class ContentEntryRoute
    {
        [SerializeField] string _entryRouteId;
        [SerializeField] string _entryKind;
        [SerializeField] string _entryValue;
        [SerializeField] string _mapPointId;
        [SerializeField] string _targetSceneId;
        [SerializeField] DisplayProfile _displayProfile;
        [SerializeField] bool _enabled = true;

        public string EntryRouteId => _entryRouteId ?? string.Empty;
        public string SerializedEntryKind => _entryKind ?? string.Empty;
        public SourceKind EntryKind => new SourceKind(_entryKind);
        public string EntryValue => _entryValue ?? string.Empty;
        public string MapPointId => _mapPointId ?? string.Empty;
        public SceneId TargetSceneId => new SceneId(_targetSceneId);
        public string SerializedTargetSceneId => _targetSceneId ?? string.Empty;
        public DisplayProfile DisplayProfile => _displayProfile;
        public bool Enabled => _enabled;
    }
}
