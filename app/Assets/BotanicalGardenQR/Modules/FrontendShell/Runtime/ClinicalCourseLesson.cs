using System;
using UnityEngine;

namespace BotanicalGardenQR.FrontendShell.Runtime
{
    [Serializable] public sealed class ClinicalCourseCatalog
    {
        public ClinicalCourseLesson[] lessons;
        public static ClinicalCourseCatalog Load()
        {
            var asset = Resources.Load<TextAsset>("ClinicalCourse/course");
            if (!asset) throw new InvalidOperationException("Missing ClinicalCourse/course.json");
            var catalog = JsonUtility.FromJson<ClinicalCourseCatalog>(asset.text);
            if (catalog?.lessons == null || catalog.lessons.Length != 5) throw new InvalidOperationException("Expected five later lessons.");
            var ids = new System.Collections.Generic.HashSet<string>();
            foreach (var lesson in catalog.lessons)
            {
                if (!FrontendShell.Contracts.ClinicalCourseScenes.IsLaterLesson(lesson.sceneId) || !ids.Add(lesson.sceneId))
                    throw new InvalidOperationException("Unknown or duplicate clinical lesson.");
                lesson.Validate();
            }
            return catalog;
        }
        public ClinicalCourseLesson Find(string id) => Array.Find(lessons, lesson => lesson.sceneId == id);
    }
    [Serializable] public sealed class ClinicalCourseLesson
    {
        public string sceneId, title, introduction;
        public int station;
        public ClinicalCourseStep[] steps;
        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(title) || steps == null || steps.Length == 0) throw new InvalidOperationException("Empty lesson.");
            foreach (var step in steps)
            {
                if (step.mode == "sequence")
                {
                    if (step.sequenceItems == null || step.sequenceItems.Length != 5 || step.sequenceOrder == null || step.sequenceOrder.Length != 5)
                        throw new InvalidOperationException("A sequence task needs five process cards.");
                    var order = new System.Collections.Generic.HashSet<int>(step.sequenceOrder);
                    if (order.Count != 5 || !order.SetEquals(new[] { 0, 1, 2, 3, 4 })) throw new InvalidOperationException("Invalid process order.");
                    continue;
                }
                if (step.options == null || step.options.Length != 3 || step.correct < 0 || step.correct >= 3)
                    throw new InvalidOperationException("Each task requires three choices and a valid answer.");
                if (step.mode != "image" && step.mode != "video" && step.mode != "evidence" && step.mode != "model" && step.mode != "ledger")
                    throw new InvalidOperationException("Unknown media mode: " + step.mode);
                if ((step.mode == "image" || step.mode == "video" || step.mode == "model") && string.IsNullOrEmpty(step.media))
                    throw new InvalidOperationException("Missing media path.");
                if (step.mode == "evidence" && (step.cards == null || step.cards.Length != 3))
                    throw new InvalidOperationException("Evidence task needs three traceable cards.");
            }
        }
    }
    [Serializable] public sealed class ClinicalCourseStep
    {
        public string mode, title, media, credit, prompt, explanation, hint, body;
        public string[] options;
        public string[] sequenceItems;
        public int[] sequenceOrder;
        public ClinicalCourseCard[] cards;
        public int correct;
    }
    [Serializable] public sealed class ClinicalCourseCard { public string title, body; }
    public enum ClinicalCoursePhase { Introduction, Task, Feedback, Finished }

    // Media, input and UI cannot silently award an answer. Skips and correct answers stay separate.
    public sealed class ClinicalCourseSession
    {
        public ClinicalCourseLesson Lesson { get; }
        public ClinicalCoursePhase Phase { get; private set; }
        public int Index { get; private set; }
        public int Selected { get; private set; } = -1;
        public int CorrectCount { get; private set; }
        public int SkippedCount { get; private set; }
        public int EvidenceMask { get; private set; }
        public bool LastIncorrect { get; private set; }
        public bool LastSkipped { get; private set; }
        public ClinicalCourseStep Step => Lesson.steps[Index];
        readonly System.Collections.Generic.List<int> _sequence = new System.Collections.Generic.List<int>();
        public System.Collections.Generic.IReadOnlyList<int> Sequence => _sequence;
        public bool CanSubmit => Phase == ClinicalCoursePhase.Task && (Step.mode == "sequence" ? _sequence.Count == 5 : Selected >= 0);
        public void ToggleSequence(int index)
        {
            if (Phase != ClinicalCoursePhase.Task || Step.mode != "sequence" || index < 0 || index >= 5) return;
            if (!_sequence.Remove(index)) _sequence.Add(index);
            LastIncorrect = false;
        }
        public ClinicalCourseSession(ClinicalCourseLesson lesson) { lesson.Validate(); Lesson = lesson; }
        public void Select(int index) { if (Phase == ClinicalCoursePhase.Task && index >= 0 && index < 3) { Selected = index; LastIncorrect = false; } }
        public void ReadEvidence(int index) { if (Phase == ClinicalCoursePhase.Task && index >= 0 && index < 3) EvidenceMask |= 1 << index; }
        public bool Submit()
        {
            if (!CanSubmit || (Step.mode == "evidence" && EvidenceMask != 7)) return false;
            LastIncorrect = Step.mode == "sequence" ? !System.Linq.Enumerable.SequenceEqual(_sequence, Step.sequenceOrder) : Selected != Step.correct;
            if (LastIncorrect) return false;
            CorrectCount++; LastSkipped = false; Phase = ClinicalCoursePhase.Feedback; return true;
        }
        public void Skip()
        {
            if (Phase != ClinicalCoursePhase.Task) return;
            SkippedCount++; LastSkipped = true; Phase = ClinicalCoursePhase.Feedback;
            Continue();
        }
        public void Continue()
        {
            if (Phase == ClinicalCoursePhase.Introduction) { Phase = ClinicalCoursePhase.Task; return; }
            if (Phase != ClinicalCoursePhase.Feedback) return;
            if (Index + 1 == Lesson.steps.Length) { Phase = ClinicalCoursePhase.Finished; return; }
            Index++; Selected = -1; _sequence.Clear(); EvidenceMask = 0; LastIncorrect = LastSkipped = false; Phase = ClinicalCoursePhase.Task;
        }
    }
}
