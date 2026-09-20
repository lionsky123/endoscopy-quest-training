using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using UnityEngine;

namespace BotanicalGardenQR.Panorama.Contracts
{
    public enum PanoramaSourceKind { Texture, StreamingAssetsPath }
    public sealed class PanoramaSource
    {
        PanoramaSource(PanoramaSourceKind kind, Texture texture, string path) { Kind=kind; Texture=texture; StreamingAssetsPath=path; }
        public PanoramaSourceKind Kind { get; }
        public Texture Texture { get; }
        public string StreamingAssetsPath { get; }
        public static PanoramaSource FromTexture(Texture texture)
        {
            if (texture == null) throw new ArgumentNullException(nameof(texture));
            return new PanoramaSource(PanoramaSourceKind.Texture, texture, null);
        }
        public static PanoramaSource FromStreamingAssetsPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("A StreamingAssets-relative path is required.", nameof(path));
            if (System.IO.Path.IsPathRooted(path)) throw new ArgumentException("The path must be relative to StreamingAssets.", nameof(path));
            var normalized=path.Trim().Replace('\\','/');
            if(normalized=="."||normalized==".."||normalized.StartsWith("./",StringComparison.Ordinal)||normalized.StartsWith("../",StringComparison.Ordinal)||normalized.Contains("/../"))
                throw new ArgumentException("The path must stay within StreamingAssets.",nameof(path));
            return new PanoramaSource(PanoramaSourceKind.StreamingAssetsPath, null, normalized);
        }
    }
    public sealed class PanoramaDefinition
    {
        // Shared by the background mesh and hand-held optical sampling.
        public const float RadiusMetres = 2.8f;
        readonly ReadOnlyCollection<PanoramaEnvironmentMomentDefinition> _environmentMoments;
        public IReadOnlyList<Texture> TeachingComparisons { get; }

        public PanoramaDefinition(PanoramaSource source, float initialYawDegrees = 0)
            : this(source, initialYawDegrees, Array.Empty<PanoramaEnvironmentMomentDefinition>()) { }

        public PanoramaDefinition(
            PanoramaSource source,
            float initialYawDegrees,
            IReadOnlyList<PanoramaEnvironmentMomentDefinition> environmentMoments,
            bool clinicalLearning = false,
            IReadOnlyList<Texture> teachingComparisons = null)
        {
            Source = source ?? throw new ArgumentNullException(nameof(source));
            InitialYawDegrees = initialYawDegrees;
            ClinicalLearning = clinicalLearning;
            var comparisons = new Texture[teachingComparisons?.Count ?? 0];
            for (int i = 0; i < comparisons.Length; i++) comparisons[i] = teachingComparisons[i];
            TeachingComparisons = Array.AsReadOnly(comparisons);
            if (environmentMoments == null) throw new ArgumentNullException(nameof(environmentMoments));
            var copy = new PanoramaEnvironmentMomentDefinition[environmentMoments.Count];
            for (var index = 0; index < copy.Length; index++)
                copy[index] = environmentMoments[index] ??
                              throw new ArgumentException("Environment moments cannot contain null entries.", nameof(environmentMoments));
            _environmentMoments = Array.AsReadOnly(copy);
        }

        public PanoramaSource Source { get; }
        public float InitialYawDegrees { get; }
        public bool ClinicalLearning { get; }
        public IReadOnlyList<PanoramaEnvironmentMomentDefinition> EnvironmentMoments => _environmentMoments;
    }
}
