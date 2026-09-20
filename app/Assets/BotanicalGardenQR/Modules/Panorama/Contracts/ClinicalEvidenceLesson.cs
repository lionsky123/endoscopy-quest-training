using System;
using UnityEngine;

namespace BotanicalGardenQR.Panorama.Contracts
{
    [Serializable]
    public sealed class ClinicalEvidenceLesson
    {
        public ClinicalEvidenceTopic[] topics;
        public void Validate()
        {
            if (topics == null || topics.Length != 3) throw new InvalidOperationException("The first lesson requires three evidence topics.");
            foreach (var topic in topics)
            {
                if (topic == null || string.IsNullOrWhiteSpace(topic.image) || string.IsNullOrWhiteSpace(topic.question) ||
                    topic.regions == null || topic.regions.Length < 2 || topic.regions.Length > 8 ||
                    topic.requiredMask <= 0 || topic.requiredMask >= (1 << topic.regions.Length) ||
                    topic.hints == null || topic.hints.Length < 2)
                    throw new InvalidOperationException("Incomplete evidence topic.");
                foreach (var region in topic.regions)
                    if (region == null || region.bounds == null || region.bounds.width <= 0 || region.bounds.height <= 0 ||
                        region.bounds.x < 0 || region.bounds.y < 0 || region.bounds.x + region.bounds.width > 1 || region.bounds.y + region.bounds.height > 1)
                        throw new InvalidOperationException("Evidence regions must fit the case image.");
            }
        }
    }
    [Serializable]
    public sealed class ClinicalEvidenceTopic
    {
        public string title, image, method, question, explanation;
        public Vector2 panoramaUv;
        public int requiredMask;
        public bool conforms;
        public string[] hints;
        public ClinicalEvidenceRegion[] regions;
        public ClinicalEvidenceLabel[] labels;
    }
    [Serializable]
    public sealed class ClinicalEvidenceRegion { public string name; public ClinicalEvidenceBounds bounds; }
    [Serializable]
    public sealed class ClinicalEvidenceBounds
    {
        // Explicit JSON fields avoid Unity Rect's private, version-dependent serialization names.
        public float x, y, width, height;
        public Rect ToRect() => new Rect(x, y, width, height);
    }
    [Serializable]
    public sealed class ClinicalEvidenceLabel { public string text; public Vector2 position; }
    public enum ClinicalEvidencePhase { Seek, Method, Question, Feedback, Finished }

    // Semantic progress belongs to the lesson, not to canvas visibility or a pointer callback.
    public sealed class ClinicalEvidenceSession
    {
        public ClinicalEvidenceLesson Definition { get; }
        public int TopicIndex { get; private set; }
        public int SelectedMask { get; private set; }
        public int Verdict { get; private set; } = -1;
        public int Attempts { get; private set; }
        public int HintLevel { get; private set; }
        public int CompletedCount { get; private set; }
        public int SkippedCount { get; private set; }
        public int SkippedTopicMask { get; private set; }
        public int ResolvedCount => CompletedCount + SkippedCount;
        public bool LastSubmissionIncorrect { get; private set; }
        public ClinicalEvidencePhase Phase { get; private set; }
        public ClinicalEvidenceTopic Topic => Definition.topics[TopicIndex];
        public bool CanSubmit => Phase == ClinicalEvidencePhase.Question && SelectedMask != 0 && Verdict >= 0;
        bool _reviewingMethod;
        public ClinicalEvidenceSession(ClinicalEvidenceLesson definition) { definition.Validate(); Definition = definition; Reset(); }
        public void Reset()
        {
            TopicIndex = CompletedCount = Attempts = HintLevel = SelectedMask = 0;
            SkippedCount = SkippedTopicMask = 0;
            Verdict = -1; _reviewingMethod = false; LastSubmissionIncorrect = false; Phase = ClinicalEvidencePhase.Seek;
        }
        public bool Locate()
        {
            if (Phase != ClinicalEvidencePhase.Seek) return false;
            Phase = ClinicalEvidencePhase.Method; return true;
        }
        public void ReviewMethod()
        {
            if (Phase != ClinicalEvidencePhase.Question) return;
            _reviewingMethod = true; Phase = ClinicalEvidencePhase.Method;
        }
        public void Ask()
        {
            if (Phase != ClinicalEvidencePhase.Method) return;
            Phase = ClinicalEvidencePhase.Question; SelectedMask = 0; Verdict = -1;
            LastSubmissionIncorrect = false;
            if (!_reviewingMethod) Attempts = HintLevel = 0;
            _reviewingMethod = false;
        }
        public void SelectRegion(int index)
        {
            if (Phase == ClinicalEvidencePhase.Question && index >= 0 && index < Topic.regions.Length)
                SelectedMask ^= 1 << index;
        }
        public void SelectVerdict(bool conforms) { if (Phase == ClinicalEvidencePhase.Question) Verdict = conforms ? 1 : 0; }
        public void Hint() { if (Phase == ClinicalEvidencePhase.Question) HintLevel = Math.Min(HintLevel + 1, Topic.hints.Length); }
        public bool Submit()
        {
            if (!CanSubmit) return false;
            Attempts++;
            if (SelectedMask != Topic.requiredMask || (Verdict == 1) != Topic.conforms)
            {
                Hint(); LastSubmissionIncorrect = true; return false;
            }
            LastSubmissionIncorrect = false; CompletedCount++; Phase = ClinicalEvidencePhase.Feedback; return true;
        }
        public bool Skip()
        {
            if (Phase != ClinicalEvidencePhase.Question) return false;
            SkippedCount++; SkippedTopicMask |= 1 << TopicIndex;
            LastSubmissionIncorrect = false; Phase = ClinicalEvidencePhase.Feedback;
            return true;
        }
        public bool Continue()
        {
            if (Phase != ClinicalEvidencePhase.Feedback) return false;
            if (ResolvedCount == Definition.topics.Length) { Phase = ClinicalEvidencePhase.Finished; return true; }
            TopicIndex++; Phase = ClinicalEvidencePhase.Seek;
            SelectedMask = Attempts = HintLevel = 0; Verdict = -1;
            LastSubmissionIncorrect = false;
            return false;
        }
        public static Vector3 PanoramaDirection(Vector2 uv, float yaw)
        {
            var lon = (uv.x - .5f) * 2 * Mathf.PI;
            var lat = (uv.y - .5f) * Mathf.PI;
            return Quaternion.Euler(0, yaw, 0) * new Vector3(Mathf.Cos(lat) * Mathf.Sin(lon), Mathf.Sin(lat), Mathf.Cos(lat) * Mathf.Cos(lon));
        }
    }
}
