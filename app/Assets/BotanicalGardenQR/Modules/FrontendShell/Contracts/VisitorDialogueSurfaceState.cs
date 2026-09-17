using System;

namespace BotanicalGardenQR.FrontendShell.Contracts
{
    public enum VisitorDialogueExpression { Listening, Welcome, Wonder }
    public enum VisitorDialogueInputOrigin { Unspecified, GazeDwell, HandPoke }
    public enum VisitorDialogueOwner
    {
        Prologue = 0,
        Coach = 1,
        ToolPreparation = 2,
        Guidance = 3
    }

    public enum VisitorDialogueSurfaceMode
    {
        Hidden = 0,
        Dialogue = 1,
        Replay = 2
    }

    public enum VisitorDialogueIntentKind
    {
        Advance = 0,
        Replay = 1,
        Defer = 2,
        Dismiss = 3,
        ChoosePrimary = 4,
        ChooseSecondary = 5
    }

    public readonly struct VisitorDialogueContextId : IEquatable<VisitorDialogueContextId>
    {
        public VisitorDialogueContextId(string value) => Value = value?.Trim() ?? string.Empty;
        public string Value { get; }
        public bool IsValid => !string.IsNullOrWhiteSpace(Value);
        public bool Equals(VisitorDialogueContextId other) =>
            string.Equals(Value, other.Value, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is VisitorDialogueContextId other && Equals(other);
        public override int GetHashCode() => Value == null ? 0 : StringComparer.Ordinal.GetHashCode(Value);
        public static bool operator ==(VisitorDialogueContextId left, VisitorDialogueContextId right) => left.Equals(right);
        public static bool operator !=(VisitorDialogueContextId left, VisitorDialogueContextId right) => !left.Equals(right);
        public override string ToString() => Value ?? string.Empty;
    }

    public readonly struct VisitorDialogueIntent
    {
        public VisitorDialogueIntent(
            VisitorDialogueContextId context,
            VisitorDialogueOwner owner,
            VisitorDialogueIntentKind kind, long revision = -1,
            VisitorDialogueInputOrigin origin = VisitorDialogueInputOrigin.Unspecified)
        {
            if (!context.IsValid) throw new ArgumentException("A dialogue context is required.", nameof(context));
            if (!Enum.IsDefined(typeof(VisitorDialogueOwner), owner))
                throw new ArgumentOutOfRangeException(nameof(owner));
            if (!Enum.IsDefined(typeof(VisitorDialogueIntentKind), kind))
                throw new ArgumentOutOfRangeException(nameof(kind));
            Context = context;
            Owner = owner;
            Kind = kind;
            Revision = revision;
            Origin = origin;
        }

        public VisitorDialogueContextId Context { get; }
        public VisitorDialogueOwner Owner { get; }
        public VisitorDialogueIntentKind Kind { get; }
        public long Revision { get; }
        public VisitorDialogueInputOrigin Origin { get; }
    }

    public sealed class VisitorDialogueSurfaceState
    {
        public VisitorDialogueSurfaceState(
            long revision,
            VisitorDialogueContextId context,
            VisitorDialogueOwner owner,
            VisitorDialogueSurfaceMode mode,
            string chapter,
            string speaker,
            string body,
            int pageIndex,
            int pageCount,
            string actionHint = null,
            bool allowDefer = false,
            string primaryActionLabel = null, string secondaryActionLabel = null,
            bool allowRestart = true,
            VisitorDialogueIntentKind primaryIntent = VisitorDialogueIntentKind.Advance,
            VisitorDialogueIntentKind secondaryIntent = VisitorDialogueIntentKind.Replay,
            VisitorDialogueExpression expression = VisitorDialogueExpression.Listening)
        {
            if (revision < 0) throw new ArgumentOutOfRangeException(nameof(revision));
            if (!context.IsValid) throw new ArgumentException("A dialogue context is required.", nameof(context));
            if (!Enum.IsDefined(typeof(VisitorDialogueOwner), owner))
                throw new ArgumentOutOfRangeException(nameof(owner));
            if (!Enum.IsDefined(typeof(VisitorDialogueSurfaceMode), mode) || mode == VisitorDialogueSurfaceMode.Hidden)
                throw new ArgumentOutOfRangeException(nameof(mode));
            if (pageCount <= 0 || pageIndex < 0 || pageIndex >= pageCount)
                throw new ArgumentException("Dialogue page state is outside its authored range.");
            if (mode == VisitorDialogueSurfaceMode.Dialogue && string.IsNullOrWhiteSpace(body))
                throw new ArgumentException("An open dialogue page requires body copy.", nameof(body));

            AllowRestart = allowRestart;
            if (!Enum.IsDefined(typeof(VisitorDialogueIntentKind), primaryIntent) ||
                !Enum.IsDefined(typeof(VisitorDialogueIntentKind), secondaryIntent))
                throw new ArgumentException("Dialogue action intents must be defined.");
            PrimaryIntent = primaryIntent;
            SecondaryIntent = secondaryIntent;
            if (!Enum.IsDefined(typeof(VisitorDialogueExpression), expression))
                throw new ArgumentOutOfRangeException(nameof(expression));
            Expression = expression;
            Revision = revision;
            Context = context;
            Owner = owner;
            Mode = mode;
            Chapter = chapter?.Trim() ?? string.Empty;
            Speaker = speaker?.Trim() ?? string.Empty;
            Body = body?.Trim() ?? string.Empty;
            PageIndex = pageIndex;
            PageCount = pageCount;
            ActionHint = actionHint?.Trim() ?? string.Empty;
            AllowDefer = allowDefer;
            PrimaryActionLabel = primaryActionLabel?.Trim() ?? string.Empty;
            SecondaryActionLabel = secondaryActionLabel?.Trim() ?? string.Empty;
        }

        public bool AllowRestart { get; }
        public VisitorDialogueExpression Expression { get; }
        public VisitorDialogueIntentKind PrimaryIntent { get; }
        public VisitorDialogueIntentKind SecondaryIntent { get; }
        public long Revision { get; }
        public VisitorDialogueContextId Context { get; }
        public VisitorDialogueOwner Owner { get; }
        public VisitorDialogueSurfaceMode Mode { get; }
        public string Chapter { get; }
        public string Speaker { get; }
        public string Body { get; }
        public int PageIndex { get; }
        public int PageCount { get; }
        public string ActionHint { get; }
        public bool AllowDefer { get; }
        public string PrimaryActionLabel { get; }
        public string SecondaryActionLabel { get; }
        public bool IsFinalPage => PageIndex == PageCount - 1;
    }
}
