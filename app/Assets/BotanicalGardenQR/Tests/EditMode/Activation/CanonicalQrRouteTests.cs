using System.Linq;
using BotanicalGardenQR.Activation.Contracts;
using BotanicalGardenQR.Configuration.Runtime;
using NUnit.Framework;
using UnityEditor;

namespace BotanicalGardenQR.Tests.EditMode.Activation
{
    public sealed class CanonicalQrRouteTests
    {
        const string RoutesPath =
            "Assets/BotanicalGardenQR/Content/Authoring/ContentEntryCatalog.asset";

        [TestCase("zone:entrance_001", "entry_welwitschia", "welwitschia")]
        [TestCase("plant:orchid_001", "entry_giant_saguaro", "giant_saguaro")]
        [TestCase("plant:bamboo_001", "entry_baobab", "baobab")]
        [TestCase("zone:tropical_house", "entry_bottle_tree", "bottle_tree")]
        [TestCase("plant:macrozamia_001", "entry_macrozamia", "macrozamia")]
        [TestCase("plant:ceiba_001", "entry_ceiba", "ceiba")]
        public void CanonicalPayload_RetainsMappingWhileFieldbookTestDisablesQr(
            string payload,
            string entryRouteId,
            string sceneId)
        {
            var routes = AssetDatabase.LoadAssetAtPath<ContentEntryCatalog>(RoutesPath);

            Assert.That(routes, Is.Not.Null);
            Assert.That(routes.TryResolve(RecognitionSourceKinds.Qr, payload, out _), Is.False);
            var route = routes.Routes.Single(value => value.EntryKind == RecognitionSourceKinds.Qr && value.EntryValue == payload);
            Assert.That(route.Enabled, Is.False);
            Assert.That(route.EntryRouteId, Is.EqualTo(entryRouteId));
            Assert.That(route.SerializedTargetSceneId, Is.EqualTo(sceneId));
        }

        [TestCase("zone:tropical_house_001")]
        [TestCase("plant:tropical_house")]
        public void RetiredPayload_RemainsRejected(string payload)
        {
            var routes = AssetDatabase.LoadAssetAtPath<ContentEntryCatalog>(RoutesPath);

            Assert.That(routes, Is.Not.Null);
            Assert.That(
                routes.TryResolve(new SourceKind("qr"), payload, out _),
                Is.False);
        }
    }
}
