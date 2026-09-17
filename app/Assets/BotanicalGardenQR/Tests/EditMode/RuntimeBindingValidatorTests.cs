using System;
using System.Collections.Generic;
using BotanicalGardenQR.Activation.Contracts;
using BotanicalGardenQR.Bootstrap;
using BotanicalGardenQR.Configuration.Runtime;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace BotanicalGardenQR.Tests.EditMode
{
    public sealed class RuntimeBindingValidatorTests
    {
        readonly List<UnityEngine.Object> _ownedObjects = new List<UnityEngine.Object>();

        [TearDown]
        public void TearDown()
        {
            for (var index = _ownedObjects.Count - 1; index >= 0; index--)
                if (_ownedObjects[index] != null) UnityEngine.Object.DestroyImmediate(_ownedObjects[index]);
            _ownedObjects.Clear();
        }

        [Test]
        public void MatchingEnabledAndBoundKinds_AreAccepted()
        {
            var options = Options(("qr", true));

            var sources = RuntimeBindingValidator.RecognitionSources(
                Providers("qr"),
                options.RecognitionAdapters);

            Assert.That(sources, Has.Count.EqualTo(1));
        }

        [Test]
        public void NoEnabledKind_IsRejected()
        {
            var options = Options(("qr", false));

            Assert.That(
                () => RuntimeBindingValidator.RecognitionSources(
                    Providers("qr"),
                    options.RecognitionAdapters),
                Throws.InvalidOperationException
                    .With.Message.Contains("at least one enabled"));
        }

        [Test]
        public void BoundKindThatIsNotEnabled_IsRejected()
        {
            var options = Options(("qr", true));

            Assert.That(
                () => RuntimeBindingValidator.RecognitionSources(
                    Providers("qr", "interaction"),
                    options.RecognitionAdapters),
                Throws.InvalidOperationException
                    .With.Message.Contains("not enabled"));
        }

        [Test]
        public void DuplicateBoundKind_IsRejected()
        {
            var options = Options(("qr", true));

            Assert.That(
                () => RuntimeBindingValidator.RecognitionSources(
                    Providers("qr", "qr"),
                    options.RecognitionAdapters),
                Throws.InvalidOperationException
                    .With.Message.Contains("duplicate SourceKind"));
        }

        [Test]
        public void DisabledKind_DoesNotRequireAProvider()
        {
            var options = Options(("qr", true), ("interaction", false));

            var sources = RuntimeBindingValidator.RecognitionSources(
                Providers("qr"),
                options.RecognitionAdapters);

            Assert.That(sources, Has.Count.EqualTo(1));
            Assert.That(sources[0].Kind, Is.EqualTo(new SourceKind("qr")));
        }

        RuntimeEnvironmentOptions Options(params (string kind, bool enabled)[] entries)
        {
            var options = ScriptableObject.CreateInstance<RuntimeEnvironmentOptions>();
            _ownedObjects.Add(options);
            var serialized = new SerializedObject(options);
            var adapters = serialized.FindProperty("_recognitionAdapters");
            adapters.arraySize = entries.Length;
            for (var index = 0; index < entries.Length; index++)
            {
                var entry = adapters.GetArrayElementAtIndex(index);
                entry.FindPropertyRelative("_sourceKind").stringValue = entries[index].kind;
                entry.FindPropertyRelative("_enabled").boolValue = entries[index].enabled;
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return options;
        }

        MonoBehaviour[] Providers(params string[] kinds)
        {
            var providers = new MonoBehaviour[kinds.Length];
            for (var index = 0; index < kinds.Length; index++)
            {
                var root = new GameObject($"RecognitionProvider-{index}");
                _ownedObjects.Add(root);
                var provider = root.AddComponent<FakeRecognitionSourceProvider>();
                provider.Configure(kinds[index]);
                providers[index] = provider;
            }
            return providers;
        }

        sealed class FakeRecognitionSourceProvider : MonoBehaviour, IRecognitionSourceProvider
        {
            SourceKind _kind;

            public void Configure(string kind) => _kind = new SourceKind(kind);
            public IRecognitionSource CreateSource() => new FakeRecognitionSource(_kind);
        }

        sealed class FakeRecognitionSource : IRecognitionSource
        {
            public FakeRecognitionSource(SourceKind kind) => Kind = kind;
            public SourceKind Kind { get; }
            public IDisposable Start(IRecognitionObservationSink sink) => EmptyRegistration.Instance;
        }

        sealed class EmptyRegistration : IDisposable
        {
            public static readonly EmptyRegistration Instance = new EmptyRegistration();
            public void Dispose() { }
        }
    }
}
