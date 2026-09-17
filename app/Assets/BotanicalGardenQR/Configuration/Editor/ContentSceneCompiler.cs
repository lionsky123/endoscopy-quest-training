using System;
using System.Collections.Generic;
using System.IO;
using BotanicalGardenQR.Configuration.Runtime;
using BotanicalGardenQR.Experience.Contracts;
using BotanicalGardenQR.KnowledgeMiniGame.Contracts;
using BotanicalGardenQR.Model.Contracts;
using BotanicalGardenQR.Panorama.Contracts;
using BotanicalGardenQR.PhysicalAugmentation.Contracts;
using BotanicalGardenQR.SpatialHost.Contracts;
using UnityEditor;
using UnityEngine;

namespace BotanicalGardenQR.Configuration.Editor
{
    internal static class ContentSceneCompiler
    {
        public static bool TryCompile(
            ContentSceneConfig source,
            GlobalUiDefaults defaults,
            ICollection<ConfigurationIssue> issues,
            out ScenePackage package)
        {
            package = null;
            var path = AssetDatabase.GetAssetPath(source);
            if (source == null)
            {
                issues.Add(new ConfigurationIssue("CFG001", path, "Content scene asset is null."));
                return false;
            }

            var issueCount = issues.Count;
            ValidateSceneId(source.SerializedSceneId, path, issues);
            ValidateContent(source.Content, path, issues);
            ValidatePresentation(defaults, source.Presentation, path, issues);
            if (issues.Count != issueCount)
                return false;

            try
            {
                var pages = BuildPageSequence(source.Content);
                package = new ScenePackage(
                    source.SerializedSceneId,
                    source.Content,
                    ResolvePresentation(defaults, source.Presentation),
                    pages);
                ValidateCompiledPageSequence(source.Content, package.AvailablePages, path, issues);
            }
            catch (Exception exception)
            {
                issues.Add(new ConfigurationIssue("CFG099", path, $"Compilation failed: {exception.Message}"));
                package = null;
            }

            return package != null && issues.Count == issueCount;
        }

        static void ValidateSceneId(string value, string path, ICollection<ConfigurationIssue> issues)
        {
            try { _ = new SceneId(value); }
            catch (Exception exception) { issues.Add(new ConfigurationIssue("CFG002", path, exception.Message)); }
        }

        static void ValidateContent(ContentSpec content, string path, ICollection<ConfigurationIssue> issues)
        {
            if (content == null)
            {
                issues.Add(new ConfigurationIssue("CFG003", path, "Content is required."));
                return;
            }

            if (string.IsNullOrWhiteSpace(content.Title))
                issues.Add(new ConfigurationIssue("CFG004", path, "Visitor title is required."));
            if (string.IsNullOrWhiteSpace(content.Subtitle))
                issues.Add(new ConfigurationIssue("CFG005", path, "Visitor subtitle is required."));

            ValidateVideo(content.Video, path, issues);
            ValidatePanorama(content.Panorama, path, issues);
            ValidateImageRing(content, path, issues);
            ValidateKnowledgeMiniGame(content.KnowledgeMiniGame, path, issues);
            ValidateModel(content.Model, path, issues);
            if (content.Narration != null && content.Narration.Clip == null)
                issues.Add(new ConfigurationIssue("CFG030", path, "Narration requires an AudioClip."));
            if (content.Effect != null && content.Effect.Prefab == null)
                issues.Add(new ConfigurationIssue("CFG032", path, "Effect requires its own prefab."));
            var physicalPointIds = new HashSet<PhysicalAugmentationPointId>();
            for (var index = 0; index < content.SerializedPhysicalAugmentationPointIds.Count; index++)
            {
                try
                {
                    var pointId = new PhysicalAugmentationPointId(
                        content.SerializedPhysicalAugmentationPointIds[index]);
                    if (!physicalPointIds.Add(pointId))
                        issues.Add(new ConfigurationIssue(
                            "CFG034",
                            path,
                            $"Physical Augmentation point association '{pointId}' is duplicated."));
                }
                catch (Exception exception)
                {
                    issues.Add(new ConfigurationIssue(
                        "CFG033",
                        path,
                        $"Physical Augmentation point association {index} is invalid: {exception.Message}"));
                }
            }
        }

        static void ValidateVideo(VideoContentSpec video, string path, ICollection<ConfigurationIssue> issues)
        {
            if (video == null) return;
            var hasClip = video.Clip != null;
            var hasPath = !string.IsNullOrWhiteSpace(video.StreamingAssetsPath);
            if (hasClip == hasPath)
            {
                issues.Add(new ConfigurationIssue("CFG010", path, "Video must specify exactly one source: VideoClip or StreamingAssets-relative path."));
                return;
            }
            if (hasPath) ValidateStreamingAsset(video.StreamingAssetsPath, "video", path, "CFG011", issues);
        }

        static void ValidatePanorama(PanoramaContentSpec panorama, string path, ICollection<ConfigurationIssue> issues)
        {
            if (panorama == null) return;
            var hasTexture = panorama.Texture != null;
            var hasPath = !string.IsNullOrWhiteSpace(panorama.StreamingAssetsPath);
            if (hasTexture == hasPath)
            {
                issues.Add(new ConfigurationIssue("CFG020", path, "Panorama must specify exactly one source: Texture or StreamingAssets-relative path."));
                return;
            }
            if (hasPath) ValidateStreamingAsset(panorama.StreamingAssetsPath, "panorama", path, "CFG021", issues);
            if (!IsFinite(panorama.InitialYawDegrees))
                issues.Add(new ConfigurationIssue("CFG022", path, "Panorama initial yaw must be finite."));

            if (panorama.EnvironmentMoments.Count > 4)
                issues.Add(new ConfigurationIssue("CFG023", path, "Panorama supports at most four environment moments."));
            var momentIds = new HashSet<string>(StringComparer.Ordinal);
            for (var index = 0; index < panorama.EnvironmentMoments.Count; index++)
            {
                PanoramaEnvironmentMomentProfile profile = panorama.EnvironmentMoments[index];
                if (profile == null)
                {
                    issues.Add(new ConfigurationIssue("CFG024", path, $"Panorama environment moment {index} is missing."));
                    continue;
                }
                if (!profile.IsValid(out var reason))
                    issues.Add(new ConfigurationIssue("CFG025", path, reason));
                if (!momentIds.Add(profile.MomentId))
                    issues.Add(new ConfigurationIssue("CFG026", path, $"Panorama environment moment ID '{profile.MomentId}' is duplicated."));
            }
        }

        static void ValidateImageRing(ContentSpec content, string path, ICollection<ConfigurationIssue> issues)
        {
            var imageRing = content.ImageRing;
            if (imageRing == null) return;
            if (content.Panorama == null)
                issues.Add(new ConfigurationIssue("CFG080", path, "ImageRing requires Panorama because Panorama owns its visitor entry."));
            if (imageRing.Items.Count < 3 || imageRing.Items.Count > 8)
            {
                issues.Add(new ConfigurationIssue("CFG081", path, "ImageRing requires 3 to 8 items."));
                return;
            }

            var textureIds = new HashSet<int>();
            var audioIds = new HashSet<int>();
            for (var index = 0; index < imageRing.Items.Count; index++)
            {
                var item = imageRing.Items[index];
                if (item == null)
                {
                    issues.Add(new ConfigurationIssue("CFG082", path, $"ImageRing item {index} is null."));
                    continue;
                }
                if (item.Image == null)
                    issues.Add(new ConfigurationIssue("CFG083", path, $"ImageRing item {index} requires a Texture2D."));
                else if (!textureIds.Add(item.Image.GetInstanceID()))
                    issues.Add(new ConfigurationIssue("CFG084", path, $"ImageRing item {index} reuses a texture; textures must be unique per scene."));
                if (item.Audio == null)
                    issues.Add(new ConfigurationIssue("CFG087", path, $"ImageRing item {index} requires an AudioClip."));
                else if (!audioIds.Add(item.Audio.GetInstanceID()))
                    issues.Add(new ConfigurationIssue("CFG088", path, $"ImageRing item {index} reuses an audio clip; audio clips must be unique per scene."));
                if (string.IsNullOrWhiteSpace(item.Title))
                    issues.Add(new ConfigurationIssue("CFG085", path, $"ImageRing item {index} requires a title."));
                if (string.IsNullOrWhiteSpace(item.Description))
                    issues.Add(new ConfigurationIssue("CFG086", path, $"ImageRing item {index} requires a description."));
            }
        }

        static void ValidateModel(ModelContentSpec model, string path, ICollection<ConfigurationIssue> issues)
        {
            if (model == null) return;
            switch (model.SourceKind)
            {
                case ModelSourceKind.Prefab:
                    if (model.Prefab == null) issues.Add(new ConfigurationIssue("CFG040", path, "Prefab model source requires a prefab."));
                    if (model.GlbAsset != null || !string.IsNullOrWhiteSpace(model.GlbStreamingAssetsPath)) issues.Add(new ConfigurationIssue("CFG041", path, "Prefab model source cannot also define a GLB source."));
                    break;
                case ModelSourceKind.Glb:
                    var hasAsset = model.GlbAsset != null;
                    var hasPath = !string.IsNullOrWhiteSpace(model.GlbStreamingAssetsPath);
                    if (hasAsset == hasPath) issues.Add(new ConfigurationIssue("CFG042", path, "GLB model source requires exactly one imported GLB asset or StreamingAssets-relative path."));
                    if (model.Prefab != null) issues.Add(new ConfigurationIssue("CFG043", path, "GLB model source cannot also define a prefab."));
                    if (hasPath) ValidateStreamingAsset(model.GlbStreamingAssetsPath, "model", path, "CFG044", issues);
                    break;
                default:
                    issues.Add(new ConfigurationIssue("CFG045", path, $"Unsupported model source kind '{model.SourceKind}'."));
                    break;
            }

            if (!IsFinite(model.LocalPosition) || !IsFinite(model.LocalEulerAngles) || !IsPositiveFinite(model.LocalScale))
                issues.Add(new ConfigurationIssue("CFG046", path, "Model transform must use finite values and a positive local scale."));
            if (!IsFinite(model.RotationDegreesPerSecond) || model.RotationDegreesPerSecond < 0f)
                issues.Add(new ConfigurationIssue("CFG047", path, "Model rotation speed must be finite and non-negative."));
            if (!IsFinite(model.BobAmplitude) || model.BobAmplitude < 0f)
                issues.Add(new ConfigurationIssue("CFG054", path, "Model bob amplitude must be finite and non-negative."));
            if (!IsFinite(model.BobFrequency) || model.BobFrequency < 0f)
                issues.Add(new ConfigurationIssue("CFG055", path, "Model bob frequency must be finite and non-negative."));

            var animation = model.Animation;
            if (animation == null) return;
            if (animation.Driver == ModelAnimationDriver.AnimatorController)
            {
                if (animation.Controller == null) issues.Add(new ConfigurationIssue("CFG048", path, "AnimatorController animation requires a controller."));
                if (string.IsNullOrWhiteSpace(animation.InitialState)) issues.Add(new ConfigurationIssue("CFG049", path, "AnimatorController animation requires an initial state."));
                if (animation.Clip != null) issues.Add(new ConfigurationIssue("CFG050", path, "AnimatorController animation cannot also define a clip."));
            }
            else if (animation.Driver == ModelAnimationDriver.AnimationClip)
            {
                if (animation.Clip == null) issues.Add(new ConfigurationIssue("CFG051", path, "AnimationClip animation requires a clip."));
                if (animation.Controller != null || !string.IsNullOrWhiteSpace(animation.InitialState)) issues.Add(new ConfigurationIssue("CFG052", path, "AnimationClip animation cannot define controller state."));
            }
            else
            {
                issues.Add(new ConfigurationIssue("CFG053", path, $"Unsupported animation driver '{animation.Driver}'."));
            }
        }

        static void ValidateKnowledgeMiniGame(
            KnowledgeMiniGameContentSpec miniGame,
            string path,
            ICollection<ConfigurationIssue> issues)
        {
            if (miniGame == null) return;
            if (miniGame.Questions.Count < KnowledgeMiniGameDefinition.MinimumQuestionCount ||
                miniGame.Questions.Count > KnowledgeMiniGameDefinition.MaximumQuestionCount)
            {
                issues.Add(new ConfigurationIssue(
                    "CFG090",
                    path,
                    $"KnowledgeMiniGame requires {KnowledgeMiniGameDefinition.MinimumQuestionCount} to {KnowledgeMiniGameDefinition.MaximumQuestionCount} questions."));
                return;
            }

            for (var questionIndex = 0; questionIndex < miniGame.Questions.Count; questionIndex++)
            {
                var question = miniGame.Questions[questionIndex];
                if (question == null)
                {
                    issues.Add(new ConfigurationIssue("CFG091", path, $"KnowledgeMiniGame question {questionIndex + 1} is null."));
                    continue;
                }
                ValidateKnowledgeMiniGameQuestion(question, questionIndex, path, issues);
            }
        }

        static void ValidateKnowledgeMiniGameQuestion(
            KnowledgeMiniGameQuestionContentSpec question,
            int questionIndex,
            string path,
            ICollection<ConfigurationIssue> issues)
        {
            var label = $"KnowledgeMiniGame question {questionIndex + 1}";
            if (string.IsNullOrWhiteSpace(question.Question))
                issues.Add(new ConfigurationIssue("CFG092", path, $"{label} requires text."));
            if (question.Options.Count < KnowledgeMiniGameDefinition.MinimumOptionCount ||
                question.Options.Count > KnowledgeMiniGameDefinition.MaximumOptionCount)
            {
                issues.Add(new ConfigurationIssue(
                    "CFG093",
                    path,
                    $"{label} requires {KnowledgeMiniGameDefinition.MinimumOptionCount} to {KnowledgeMiniGameDefinition.MaximumOptionCount} options."));
                return;
            }

            var answerIds = new HashSet<string>(StringComparer.Ordinal);
            for (var optionIndex = 0; optionIndex < question.Options.Count; optionIndex++)
            {
                var option = question.Options[optionIndex];
                if (option == null)
                {
                    issues.Add(new ConfigurationIssue("CFG094", path, $"{label} option {optionIndex + 1} is null."));
                    continue;
                }
                if (string.IsNullOrWhiteSpace(option.AnswerId) ||
                    !string.Equals(option.AnswerId, option.AnswerId.Trim(), StringComparison.Ordinal))
                    issues.Add(new ConfigurationIssue("CFG095", path, $"{label} option {optionIndex + 1} requires a canonical answer ID."));
                else if (!answerIds.Add(option.AnswerId))
                    issues.Add(new ConfigurationIssue("CFG096", path, $"{label} answer ID '{option.AnswerId}' is duplicated."));
                if (string.IsNullOrWhiteSpace(option.Text))
                    issues.Add(new ConfigurationIssue("CFG097", path, $"{label} option {optionIndex + 1} requires text."));
            }

            if (string.IsNullOrWhiteSpace(question.CorrectAnswerId) ||
                !string.Equals(question.CorrectAnswerId, question.CorrectAnswerId.Trim(), StringComparison.Ordinal) ||
                !answerIds.Contains(question.CorrectAnswerId))
                issues.Add(new ConfigurationIssue("CFG098", path, $"{label} correct answer ID must match one configured option."));
            if (string.IsNullOrWhiteSpace(question.SuccessExplanation))
                issues.Add(new ConfigurationIssue("CFG099", path, $"{label} requires a success explanation."));
            if (string.IsNullOrWhiteSpace(question.RetryHint))
                issues.Add(new ConfigurationIssue("CFG100", path, $"{label} requires a retry hint."));
        }

        static void ValidatePresentation(GlobalUiDefaults defaults, PresentationOverride overrides, string path, ICollection<ConfigurationIssue> issues)
        {
            if (defaults == null)
            {
                issues.Add(new ConfigurationIssue("CFG060", path, "GlobalUiDefaults is required."));
                return;
            }

            var resolved = ResolvePresentation(defaults, overrides);
            if (!IsPositiveFinite(resolved.TitleStyle.FontSize) || !IsPositiveFinite(resolved.TitleStyle.Width))
                issues.Add(new ConfigurationIssue("CFG061", path, "Resolved title font size and width must be positive and finite."));
            if (!IsPositiveFinite(resolved.SubtitleStyle.FontSize) || !IsPositiveFinite(resolved.SubtitleStyle.Width))
                issues.Add(new ConfigurationIssue("CFG062", path, "Resolved subtitle font size and width must be positive and finite."));
            if (!IsPositiveFinite(resolved.Layout.PanelSize) || !IsFinite(resolved.Layout.PanelOffset) || !IsFinite(resolved.Layout.Spacing) || resolved.Layout.Spacing < 0f)
                issues.Add(new ConfigurationIssue("CFG063", path, "Resolved panel size must be positive; offset and spacing must be finite; spacing cannot be negative."));
            if (!IsFinite(resolved.Animation.TransitionSeconds) || resolved.Animation.TransitionSeconds < 0f)
                issues.Add(new ConfigurationIssue("CFG064", path, "Resolved transition duration must be finite and non-negative."));
        }

        static PresentationSpec ResolvePresentation(GlobalUiDefaults defaults, PresentationOverride overrides)
        {
            var title = Resolve(defaults.TitleStyle, overrides?.TitleStyle);
            var subtitle = Resolve(defaults.SubtitleStyle, overrides?.SubtitleStyle);
            var layout = Resolve(defaults.Layout, overrides?.Layout);
            var animation = Resolve(defaults.Animation, overrides?.Animation);
            return new PresentationSpec(title, subtitle, layout, animation);
        }

        static TextStyleSpec Resolve(TextStyleSpec fallback, TextStyleOverride value)
            => value == null ? fallback : new TextStyleSpec(
                value.FontSize.HasValue ? value.FontSize.Value : fallback.FontSize,
                value.Color.HasValue ? value.Color.Value : fallback.Color,
                value.Position.HasValue ? value.Position.Value : fallback.Position,
                value.Width.HasValue ? value.Width.Value : fallback.Width);

        static LayoutSpec Resolve(LayoutSpec fallback, LayoutOverride value)
            => value == null ? fallback : new LayoutSpec(
                value.PanelSize.HasValue ? value.PanelSize.Value : fallback.PanelSize,
                value.PanelOffset.HasValue ? value.PanelOffset.Value : fallback.PanelOffset,
                value.Spacing.HasValue ? value.Spacing.Value : fallback.Spacing);

        static AnimationSpec Resolve(AnimationSpec fallback, AnimationOverride value)
            => value == null ? fallback : new AnimationSpec(
                value.TransitionSeconds.HasValue ? value.TransitionSeconds.Value : fallback.TransitionSeconds);

        static FeaturePageId[] BuildPageSequence(ContentSpec content)
        {
            var pages = new List<FeaturePageId>(3);
            if (content.Panorama != null) pages.Add(FeaturePageId.Panorama);
            if (content.Video != null) pages.Add(FeaturePageId.Video);
            if (content.Model != null) pages.Add(FeaturePageId.Model);
            return pages.ToArray();
        }

        static void ValidateCompiledPageSequence(ContentSpec content, IReadOnlyList<FeaturePageId> pages, string path, ICollection<ConfigurationIssue> issues)
        {
            var expected = BuildPageSequence(content);
            if (pages.Count != expected.Length)
            {
                issues.Add(new ConfigurationIssue("CFG070", path, "Compiled foreground page sequence does not match configured page capabilities."));
                return;
            }
            var seen = new HashSet<FeaturePageId>();
            for (var index = 0; index < pages.Count; index++)
            {
                if (pages[index] != expected[index] || !seen.Add(pages[index]))
                {
                    issues.Add(new ConfigurationIssue("CFG071", path, "Foreground pages must be unique and use the canonical Main-to-one-feature sequence."));
                    return;
                }
            }
        }

        static void ValidateStreamingAsset(string relativePath, string label, string assetPath, string code, ICollection<ConfigurationIssue> issues)
        {
            var normalized = relativePath.Trim().Replace('\\', '/');
            if (Path.IsPathRooted(normalized) || normalized == "." || normalized == ".." || normalized.StartsWith("./", StringComparison.Ordinal) || normalized.StartsWith("../", StringComparison.Ordinal) || normalized.Contains("/../"))
            {
                issues.Add(new ConfigurationIssue(code, assetPath, $"The {label} path must stay within StreamingAssets."));
                return;
            }
            var fullPath = Path.GetFullPath(Path.Combine(Application.streamingAssetsPath, normalized));
            var root = Path.GetFullPath(Application.streamingAssetsPath) + Path.DirectorySeparatorChar;
            if (!fullPath.StartsWith(root, StringComparison.OrdinalIgnoreCase) || !File.Exists(fullPath))
                issues.Add(new ConfigurationIssue(code, assetPath, $"The {label} StreamingAssets file does not exist: '{normalized}'."));
        }

        static bool IsFinite(Vector2 value) => IsFinite(value.x) && IsFinite(value.y);
        static bool IsFinite(Vector3 value) => IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
        static bool IsPositiveFinite(Vector2 value) => IsPositiveFinite(value.x) && IsPositiveFinite(value.y);
        static bool IsPositiveFinite(Vector3 value) => IsPositiveFinite(value.x) && IsPositiveFinite(value.y) && IsPositiveFinite(value.z);
        static bool IsPositiveFinite(float value) => value > 0f && IsFinite(value);
        static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
