using BotanicalGardenQR.Experience.Contracts;
using UnityEngine;

namespace BotanicalGardenQR.Configuration.Runtime
{
    [CreateAssetMenu(menuName = "Botanical Garden QR/Content Scene", fileName = "ContentSceneConfig")]
    public sealed class ContentSceneConfig : ScriptableObject
    {
        [SerializeField] string _sceneId;
        [SerializeField] ContentSpec _content = new ContentSpec();
        [SerializeField] PresentationOverride _presentation = new PresentationOverride();

        public SceneId SceneId => new SceneId(_sceneId);
        public string SerializedSceneId => _sceneId ?? string.Empty;
        public ContentSpec Content => _content;
        public PresentationOverride Presentation => _presentation;
    }
}
