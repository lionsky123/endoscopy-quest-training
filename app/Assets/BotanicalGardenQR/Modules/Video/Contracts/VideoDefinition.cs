using System;
using UnityEngine.Video;

namespace BotanicalGardenQR.Video.Contracts
{
    public enum VideoSourceKind { VideoClip, StreamingAssetsPath }

    public sealed class VideoSource
    {
        VideoSource(VideoSourceKind kind, VideoClip clip, string streamingAssetsPath)
        { Kind = kind; Clip = clip; StreamingAssetsPath = streamingAssetsPath; }

        public VideoSourceKind Kind { get; }
        public VideoClip Clip { get; }
        public string StreamingAssetsPath { get; }

        public static VideoSource FromClip(VideoClip clip)
        {
            if (clip == null) throw new ArgumentNullException(nameof(clip));
            return new VideoSource(VideoSourceKind.VideoClip, clip, null);
        }

        public static VideoSource FromStreamingAssetsPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("A StreamingAssets-relative path is required.", nameof(path));
            if (System.IO.Path.IsPathRooted(path)) throw new ArgumentException("The path must be relative to StreamingAssets.", nameof(path));
            var normalized = path.Trim().Replace('\\', '/');
            if (normalized == "." || normalized == ".." || normalized.StartsWith("./", StringComparison.Ordinal) || normalized.StartsWith("../", StringComparison.Ordinal) || normalized.Contains("/../"))
                throw new ArgumentException("The path must stay within StreamingAssets.", nameof(path));
            return new VideoSource(VideoSourceKind.StreamingAssetsPath, null, normalized);
        }
    }

    public sealed class VideoDefinition
    {
        public VideoDefinition(VideoSource source, bool loop = false) { Source = source ?? throw new ArgumentNullException(nameof(source)); Loop = loop; }
        public VideoSource Source { get; }
        public bool Loop { get; }
    }
}
