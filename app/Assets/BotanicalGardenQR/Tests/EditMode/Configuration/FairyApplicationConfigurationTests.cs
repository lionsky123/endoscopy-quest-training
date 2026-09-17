using System;
using System.Linq;
using System.Reflection;
using BotanicalGardenQR.Configuration.Runtime;
using BotanicalGardenQR.Fairy.Backend;
using BotanicalGardenQR.Fairy.Contracts;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace BotanicalGardenQR.Tests.EditMode.Configuration
{
    public sealed class FairyApplicationConfigurationTests
    {
        const string ProductionConfigurationPath =
            "Assets/BotanicalGardenQR/Content/Authoring/FairyApplicationConfiguration.asset";

        [Test]
        public void TryGet_RequiresExplicitGlobalPrefab()
        {
            var configuration = ScriptableObject.CreateInstance<FairyApplicationConfiguration>();
            try
            {
                Assert.That(configuration.TryGet(out var definition), Is.False);
                Assert.That(definition, Is.Null);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(configuration);
            }
        }

        [Test]
        public void TryGet_CreatesApplicationDefinitionWithoutSceneIdentity()
        {
            var configuration = ScriptableObject.CreateInstance<FairyApplicationConfiguration>();
            var prefab = new GameObject("GlobalFairyTestPrefab");
            var arrivalPrefab = new GameObject("GlobalFairyArrivalTestPrefab");
            arrivalPrefab.AddComponent<ParticleSystem>();
            var maskPrefab = new GameObject("GlobalFairyArrivalMaskTestPrefab");
            maskPrefab.AddComponent<MeshRenderer>();
            var flickerClip = AudioClip.Create("ArrivalFlickerTestClip", 64, 1, 44100, false);
            var malfunctionClip = AudioClip.Create("ArrivalMalfunctionTestClip", 64, 1, 44100, false);
            var companionEffect = new GameObject("FairyCompanionEffectTestPrefab");
            companionEffect.AddComponent<ParticleSystem>();
            var attentionClip = AudioClip.Create("CoachAttentionTestClip", 64, 1, 44100, false);
            var artifactWaitClip = AudioClip.Create("ArtifactWaitTestClip", 64, 1, 44100, false);
            var artifactReturnClip = AudioClip.Create("ArtifactReturnTestClip", 64, 1, 44100, false);
            var celebrateClip = AudioClip.Create("CelebrateTestClip", 64, 1, 44100, false);
            var idleLocomotionClip = AudioClip.Create("IdleLocomotionTestClip", 64, 1, 44100, false);
            try
            {
                SetField(configuration, "_prefab", prefab);
                SetField(configuration, "_behavior", FairyBehavior.Orbit);
                SetField(configuration, "_scale", 1.25f);
                SetField(configuration, "_arrivalEffectPrefab", arrivalPrefab);
                SetField(configuration, "_arrivalMaskPrefab", maskPrefab);
                SetField(configuration, "_companionCueEffectPrefab", companionEffect);
                SetField(configuration, "_companionCueEffectLocalOffset", new Vector3(0f, 0.4f, 0f));
                SetField(configuration, "_companionCueEffectLocalScale", 0.8f);
                SetField(configuration, "_coachAttentionClip", attentionClip);
                SetField(configuration, "_coachAttentionVolume", 0.5f);
                SetField(configuration, "_coachAttentionEffectSeconds", 0.45f);
                SetField(configuration, "_coachAttentionParticleBurst", 5);
                SetField(configuration, "_artifactWaitClip", artifactWaitClip);
                SetField(configuration, "_artifactWaitVolume", 0.3f);
                SetField(configuration, "_artifactWaitEffectSeconds", 0.35f);
                SetField(configuration, "_artifactWaitParticleBurst", 4);
                SetField(configuration, "_artifactReturnClip", artifactReturnClip);
                SetField(configuration, "_artifactReturnVolume", 0.6f);
                SetField(configuration, "_artifactReturnEffectSeconds", 0.65f);
                SetField(configuration, "_artifactReturnParticleBurst", 8);
                SetField(configuration, "_celebrateClip", celebrateClip);
                SetField(configuration, "_celebrateVolume", 0.7f);
                SetField(configuration, "_celebrateEffectSeconds", 1.1f);
                SetField(configuration, "_celebrateParticleBurst", 11);
                SetField(configuration, "_idleLocomotionClips", new[] { idleLocomotionClip });
                SetField(configuration, "_idleLocomotionVolume", 0.2f);
                SetField(configuration, "_idleLocomotionMinIntervalSeconds", 3f);
                SetField(configuration, "_idleLocomotionMaxIntervalSeconds", 8f);
                SetField(configuration, "_idleLocomotionEffectSeconds", 0.25f);
                SetField(configuration, "_idleLocomotionParticleBurst", 2);
                SetField(configuration, "_arrivalEffectDelaySeconds", 2f);
                SetField(configuration, "_arrivalRevealDelaySeconds", 0.3f);

                Assert.That(configuration.TryGet(out var definition), Is.True);
                Assert.That(definition.Prefab, Is.SameAs(prefab));
                Assert.That(definition.Behavior, Is.EqualTo(FairyBehavior.Orbit));
                Assert.That(definition.Scale, Is.EqualTo(1.25f));
                Assert.That(definition.ArrivalEffectPrefab, Is.SameAs(arrivalPrefab));
                Assert.That(definition.ArrivalMaskPrefab, Is.SameAs(maskPrefab));
                Assert.That(definition.CompanionFeedback.EffectPrefab, Is.SameAs(companionEffect));
                Assert.That(definition.CompanionFeedback.EffectLocalOffset,
                    Is.EqualTo(new Vector3(0f, 0.4f, 0f)));
                Assert.That(definition.CompanionFeedback.EffectLocalScale, Is.EqualTo(0.8f));
                Assert.That(definition.CompanionFeedback.CoachAttentionClip, Is.SameAs(attentionClip));
                Assert.That(definition.CompanionFeedback.CoachAttentionVolume, Is.EqualTo(0.5f));
                Assert.That(definition.CompanionFeedback.CoachAttentionEffectSeconds, Is.EqualTo(0.45f));
                Assert.That(definition.CompanionFeedback.CoachAttentionParticleBurst, Is.EqualTo(5));
                Assert.That(definition.CompanionFeedback.ArtifactWaitClip, Is.SameAs(artifactWaitClip));
                Assert.That(definition.CompanionFeedback.ArtifactWaitVolume, Is.EqualTo(0.3f));
                Assert.That(definition.CompanionFeedback.ArtifactWaitEffectSeconds, Is.EqualTo(0.35f));
                Assert.That(definition.CompanionFeedback.ArtifactWaitParticleBurst, Is.EqualTo(4));
                Assert.That(definition.CompanionFeedback.ArtifactReturnClip, Is.SameAs(artifactReturnClip));
                Assert.That(definition.CompanionFeedback.ArtifactReturnVolume, Is.EqualTo(0.6f));
                Assert.That(definition.CompanionFeedback.ArtifactReturnEffectSeconds, Is.EqualTo(0.65f));
                Assert.That(definition.CompanionFeedback.ArtifactReturnParticleBurst, Is.EqualTo(8));
                Assert.That(definition.CompanionFeedback.CelebrateClip, Is.SameAs(celebrateClip));
                Assert.That(definition.CompanionFeedback.CelebrateVolume, Is.EqualTo(0.7f));
                Assert.That(definition.CompanionFeedback.CelebrateEffectSeconds, Is.EqualTo(1.1f));
                Assert.That(definition.CompanionFeedback.CelebrateParticleBurst, Is.EqualTo(11));
                Assert.That(definition.CompanionFeedback.IdleLocomotionClips.Count, Is.EqualTo(1));
                Assert.That(definition.CompanionFeedback.IdleLocomotionClips[0], Is.SameAs(idleLocomotionClip));
                Assert.That(definition.CompanionFeedback.IdleLocomotionVolume, Is.EqualTo(0.2f));
                Assert.That(definition.CompanionFeedback.IdleLocomotionMinIntervalSeconds, Is.EqualTo(3f));
                Assert.That(definition.CompanionFeedback.IdleLocomotionMaxIntervalSeconds, Is.EqualTo(8f));
                Assert.That(definition.CompanionFeedback.IdleLocomotionEffectSeconds, Is.EqualTo(0.25f));
                Assert.That(definition.CompanionFeedback.IdleLocomotionParticleBurst, Is.EqualTo(2));
                Assert.That(definition.ArrivalEffectDelaySeconds, Is.EqualTo(2f));
                Assert.That(definition.ArrivalRevealDelaySeconds, Is.EqualTo(0.3f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(celebrateClip);
                UnityEngine.Object.DestroyImmediate(artifactReturnClip);
                UnityEngine.Object.DestroyImmediate(artifactWaitClip);
                UnityEngine.Object.DestroyImmediate(attentionClip);
                UnityEngine.Object.DestroyImmediate(idleLocomotionClip);
                UnityEngine.Object.DestroyImmediate(companionEffect);
                UnityEngine.Object.DestroyImmediate(malfunctionClip);
                UnityEngine.Object.DestroyImmediate(flickerClip);
                UnityEngine.Object.DestroyImmediate(maskPrefab);
                UnityEngine.Object.DestroyImmediate(arrivalPrefab);
                UnityEngine.Object.DestroyImmediate(prefab);
                UnityEngine.Object.DestroyImmediate(configuration);
            }
        }

        [Test]
        public void TryGet_RejectsArrivalMaskWithoutRenderer()
        {
            var configuration = ScriptableObject.CreateInstance<FairyApplicationConfiguration>();
            var prefab = new GameObject("GlobalFairyTestPrefab");
            var arrivalPrefab = new GameObject("GlobalFairyArrivalTestPrefab");
            arrivalPrefab.AddComponent<ParticleSystem>();
            var invalidMaskPrefab = new GameObject("InvalidArrivalMaskTestPrefab");
            try
            {
                SetField(configuration, "_prefab", prefab);
                SetField(configuration, "_arrivalEffectPrefab", arrivalPrefab);
                SetField(configuration, "_arrivalMaskPrefab", invalidMaskPrefab);

                Assert.That(configuration.TryGet(out var definition), Is.False);
                Assert.That(definition, Is.Null);
                Assert.That(configuration.IsValid(out var reason), Is.False);
                Assert.That(reason, Does.Contain("Renderer"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(invalidMaskPrefab);
                UnityEngine.Object.DestroyImmediate(arrivalPrefab);
                UnityEngine.Object.DestroyImmediate(prefab);
                UnityEngine.Object.DestroyImmediate(configuration);
            }
        }

        [Test]
        public void TryGet_RejectsArrivalPrefabWithoutRootParticles()
        {
            var configuration = ScriptableObject.CreateInstance<FairyApplicationConfiguration>();
            var prefab = new GameObject("GlobalFairyTestPrefab");
            var invalidArrivalPrefab = new GameObject("InvalidArrivalTestPrefab");
            try
            {
                SetField(configuration, "_prefab", prefab);
                SetField(configuration, "_arrivalEffectPrefab", invalidArrivalPrefab);

                Assert.That(configuration.TryGet(out var definition), Is.False);
                Assert.That(definition, Is.Null);
                Assert.That(configuration.IsValid(out var reason), Is.False);
                Assert.That(reason, Does.Contain("root ParticleSystem"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(invalidArrivalPrefab);
                UnityEngine.Object.DestroyImmediate(prefab);
                UnityEngine.Object.DestroyImmediate(configuration);
            }
        }

        [Test]
        public void ProductionArrivalAssets_DrawMaskBeforeShockwave()
        {
            var configuration = AssetDatabase.LoadAssetAtPath<FairyApplicationConfiguration>(
                ProductionConfigurationPath);

            Assert.That(configuration, Is.Not.Null);
            Assert.That(configuration.TryGet(out var definition), Is.True);

            var shell = definition.ArrivalMaskPrefab.transform.Find("FairyPassthroughShell");
            Assert.That(shell, Is.Not.Null);
            var maskQueues = MaterialRenderQueues(shell.GetComponentsInChildren<Renderer>(true));
            var arrivalEffectQueues = MaterialRenderQueues(
                definition.ArrivalEffectPrefab.GetComponentsInChildren<ParticleSystemRenderer>(true));

            Assert.That(maskQueues, Is.Not.Empty);
            Assert.That(arrivalEffectQueues, Is.Not.Empty);
            Assert.That(
                maskQueues.Max(),
                Is.LessThan(arrivalEffectQueues.Min()),
                "The Passthrough shell must hide the Fairy before the official arrival VFX draws on top of it.");
        }

        [Test]
        public void ProductionArrivalAssets_PreserveOfficialCompositeAndShellContract()
        {
            var configuration = AssetDatabase.LoadAssetAtPath<FairyApplicationConfiguration>(
                ProductionConfigurationPath);

            Assert.That(configuration, Is.Not.Null);
            Assert.That(configuration.TryGet(out var definition), Is.True);

            var arrivalEffect = definition.ArrivalEffectPrefab;
            Assert.That(arrivalEffect.name, Is.EqualTo("OppyArrivalEffect"));
            Assert.That(
                AssetDatabase.GetAssetPath(arrivalEffect),
                Is.EqualTo("Assets/BotanicalGardenQR/Content/Shared/Fairy/Arrival/OppyArrivalEffect.prefab"));

            var rootParticles = arrivalEffect.GetComponent<ParticleSystem>();
            var rootRenderer = arrivalEffect.GetComponent<ParticleSystemRenderer>();
            var glow = arrivalEffect.transform.Find("Glow");
            Assert.That(rootParticles, Is.Not.Null);
            Assert.That(rootRenderer, Is.Not.Null);
            Assert.That(glow, Is.Not.Null);

            var shockwaveRenderers = new[]
            {
                rootRenderer,
                glow.GetComponent<ParticleSystemRenderer>()
            };
            Assert.That(shockwaveRenderers.All(renderer => renderer != null), Is.True);
            var shockwaveMaterials = shockwaveRenderers
                .SelectMany(renderer => renderer.sharedMaterials)
                .Where(material => material != null)
                .Distinct()
                .ToArray();

            Assert.That(shockwaveMaterials, Is.Not.Empty);
            Assert.That(
                shockwaveMaterials.All(material => material.shader != null &&
                    material.shader.name == "TheWorldBeyond/VRFlashlight"),
                Is.True);
            Assert.That(
                shockwaveMaterials.All(material => material.shader.passCount == 1),
                Is.True,
                "The official WorldShockwave uses one alpha-reveal pass.");
            Assert.That(
                shockwaveMaterials.All(material => !material.HasProperty("_RiftColor")),
                Is.True,
                "The previous project-owned blue rift color must not return.");

            Assert.That(rootParticles.main.scalingMode, Is.EqualTo(ParticleSystemScalingMode.Local));
            Assert.That(arrivalEffect.transform.localScale, Is.EqualTo(Vector3.one));
            Assert.That(
                rootParticles.main.startSize.constantMax,
                Is.LessThanOrEqualTo(2f),
                "The cinematic arrival may widen around the guide, but its burst must remain within two metres.");

            var teleportTransform = arrivalEffect.transform.Find("OppyTeleportParticles");
            Assert.That(teleportTransform, Is.Not.Null);
            Assert.That(teleportTransform.localPosition, Is.EqualTo(Vector3.zero));
            Assert.That(teleportTransform.localScale, Is.EqualTo(Vector3.one * 0.1f));
            var teleportParticles = teleportTransform.GetComponent<ParticleSystem>();
            var teleportRenderer = teleportTransform.GetComponent<ParticleSystemRenderer>();
            Assert.That(teleportParticles, Is.Not.Null);
            Assert.That(teleportRenderer, Is.Not.Null);
            Assert.That(teleportParticles.main.loop, Is.False);
            Assert.That(teleportParticles.main.playOnAwake, Is.True);
            Assert.That(
                teleportParticles.main.startSize.constantMax,
                Is.EqualTo(10f).Within(0.001f),
                "The official 0.1 root scale and 10-unit particle size produce the approximately one-metre ring.");
            var teleportLifetime = teleportParticles.main.startLifetime;
            Assert.That(teleportLifetime.mode, Is.EqualTo(ParticleSystemCurveMode.TwoConstants));
            Assert.That(
                Mathf.Min(teleportLifetime.constantMin, teleportLifetime.constantMax),
                Is.EqualTo(0.3f).Within(0.001f));
            Assert.That(
                Mathf.Max(teleportLifetime.constantMin, teleportLifetime.constantMax),
                Is.EqualTo(0.5f).Within(0.001f));
            var teleportEmission = teleportParticles.emission;
            Assert.That(teleportEmission.burstCount, Is.EqualTo(1));
            Assert.That(
                teleportEmission.GetBurst(0).count.constantMax,
                Is.EqualTo(3f).Within(0.001f),
                "The local cyan portal must retain the official three-ring burst.");
            Assert.That(teleportParticles.colorOverLifetime.enabled, Is.True);
            AssertParticleMaterial(
                teleportRenderer,
                "OppyTeleportRing",
                "chargeRing",
                "TheWorldBeyond/OppyParticlesShader");

            var sparklesTransform = arrivalEffect.transform.Find("OppySparkles");
            Assert.That(sparklesTransform, Is.Not.Null);
            Assert.That(sparklesTransform.localPosition, Is.EqualTo(new Vector3(0f, 0.2f, 0f)));
            Assert.That(sparklesTransform.localScale, Is.EqualTo(Vector3.one));
            var sparkles = sparklesTransform.GetComponent<ParticleSystem>();
            var sparklesRenderer = sparklesTransform.GetComponent<ParticleSystemRenderer>();
            Assert.That(sparkles, Is.Not.Null);
            Assert.That(sparklesRenderer, Is.Not.Null);
            Assert.That(sparkles.main.loop, Is.True);
            Assert.That(sparkles.main.playOnAwake, Is.False);
            Assert.That(sparkles.emission.rateOverTime.constantMax, Is.EqualTo(7f).Within(0.001f));
            AssertParticleMaterial(
                sparklesRenderer,
                "OppySparkleMat",
                "StarParticles2",
                "TheWorldBeyond/OppyParticlesShader");

            var mask = definition.ArrivalMaskPrefab;
            Assert.That(mask.name, Is.EqualTo("FairyArrivalDiscoveryMask"));
            Assert.That(mask.GetComponent<FairyArrivalOtherWorldWindow>(), Is.Not.Null);
            Assert.That(
                AssetDatabase.GetAssetPath(mask),
                Is.EqualTo("Assets/BotanicalGardenQR/Content/Shared/Fairy/Arrival/FairyArrivalDiscoveryMask.prefab"));
            var shell = mask.transform.Find("FairyPassthroughShell");
            Assert.That(shell, Is.Not.Null);
            Assert.That(shell.localPosition, Is.EqualTo(new Vector3(0f, 0.3f, 0f)));
            Assert.That(shell.localScale, Is.EqualTo(Vector3.one * 0.8f));
            var maskRenderer = shell.GetComponentInChildren<Renderer>(true);
            Assert.That(maskRenderer, Is.Not.Null);
            var maskMaterial = maskRenderer.sharedMaterial;
            Assert.That(maskMaterial, Is.Not.Null);
            Assert.That(maskMaterial.shader.name, Is.EqualTo("BotanicalGardenQR/Fairy/OfficialPassthroughShell"));
            Assert.That(maskMaterial.renderQueue, Is.EqualTo(2999));
            Assert.That(maskMaterial.GetFloat("_Cull"), Is.EqualTo(2f));
            Assert.That(maskMaterial.GetFloat("_ZWrite"), Is.EqualTo(1f));
            Assert.That(maskMaterial.GetFloat("_BlendOpColor"), Is.EqualTo(2f));
            Assert.That(maskMaterial.GetFloat("_BlendOpAlpha"), Is.EqualTo(3f));

            var ambient = mask.GetComponent<AudioSource>();
            Assert.That(ambient, Is.Not.Null);
            Assert.That(ambient.clip, Is.Not.Null);
            Assert.That(ambient.playOnAwake, Is.False);
            Assert.That(ambient.loop, Is.True);
            Assert.That(ambient.spatialBlend, Is.EqualTo(1f));
            var otherWorld = mask.transform.Find("OppyOtherWorldEnvironment");
            Assert.That(otherWorld, Is.Not.Null);
            Assert.That(
                otherWorld.GetComponentsInChildren<Renderer>(true)
                    .Where(renderer => renderer.enabled)
                    .SelectMany(renderer => renderer.sharedMaterials)
                    .Where(material => material != null),
                Has.All.Matches<Material>(material =>
                    material.HasProperty("_PortalStencilComp") &&
                    material.GetFloat("_PortalStencilComp") == 3f),
                "The copied official forest must remain inside the local other-world window.");

            var portalStencil = mask.transform.Find("OtherWorldPortalStencil");
            Assert.That(portalStencil, Is.Not.Null);
            var portalStencilMaterial = portalStencil.GetComponent<MeshRenderer>().sharedMaterial;
            Assert.That(portalStencilMaterial, Is.Not.Null);
            Assert.That(
                portalStencilMaterial.shader.name,
                Is.EqualTo("BotanicalGardenQR/Fairy/OtherWorldPortalStencil"));
            Assert.That(portalStencilMaterial.mainTexture.name, Is.EqualTo("FlashlightGlow"));
            Assert.That(portalStencilMaterial.renderQueue, Is.EqualTo(1900));

            var aperture = mask.transform.Find("OppyDiscoveryFlashlightAperture");
            Assert.That(aperture, Is.Not.Null);
            Assert.That(aperture.GetComponent<FairyArrivalFlashlightAperture>(), Is.Not.Null);
            var apertureParent = aperture.Find("parent");
            Assert.That(apertureParent, Is.Not.Null);
            Assert.That(apertureParent.localPosition, Is.EqualTo(new Vector3(0.014f, 0f, 0f)));
            Assert.That(
                Quaternion.Angle(apertureParent.localRotation, Quaternion.Euler(90f, 0f, 0f)),
                Is.LessThan(0.01f));

            var lightVolumes = apertureParent.Find("LightVolumes");
            Assert.That(lightVolumes, Is.Not.Null);
            Assert.That(lightVolumes.childCount, Is.EqualTo(1),
                "The source Prefab stores one reference slice; VirtualFlashlight creates four at runtime.");
            var referenceSlice = lightVolumes.GetChild(0);
            Assert.That(referenceSlice.name, Is.EqualTo("LightVolumeA"));
            Assert.That(referenceSlice.localPosition, Is.EqualTo(new Vector3(0f, 0.2f, 0f)));
            Assert.That(referenceSlice.localScale, Is.EqualTo(Vector3.one * 0.3f));
            Assert.That(
                Quaternion.Angle(referenceSlice.localRotation, Quaternion.Euler(-90f, 0f, 0f)),
                Is.LessThan(0.01f));
            var sliceRenderer = referenceSlice.GetComponent<MeshRenderer>();
            Assert.That(sliceRenderer, Is.Not.Null);
            Assert.That(sliceRenderer.sharedMaterial.name, Is.EqualTo("VRFlashlight"));
            Assert.That(sliceRenderer.sharedMaterial.shader.name, Is.EqualTo("TheWorldBeyond/VRFlashlight"));
            Assert.That(sliceRenderer.sharedMaterial.mainTexture.name, Is.EqualTo("FlashlightGlow"));

            var lightcone = apertureParent.Find("LightconeMesh");
            Assert.That(lightcone, Is.Not.Null);
            Assert.That(lightcone.localPosition, Is.EqualTo(new Vector3(-0.014f, 0.697f, 0f)));
            Assert.That(lightcone.localScale, Is.EqualTo(Vector3.one * 0.5f));
            Assert.That(
                Quaternion.Angle(lightcone.localRotation, Quaternion.Euler(-180f, 0f, 0f)),
                Is.LessThan(0.01f));
            var lightconeRenderer = lightcone.GetComponent<MeshRenderer>();
            Assert.That(lightconeRenderer, Is.Not.Null);
            Assert.That(lightconeRenderer.sharedMaterial.name, Is.EqualTo("LightconeGlow"));
            Assert.That(lightconeRenderer.sharedMaterial.shader.name, Is.EqualTo("TheWorldBeyond/LightconeGlow"));
            Assert.That(lightconeRenderer.sharedMaterial.mainTexture.name, Is.EqualTo("FlashlightCloud"));

        }

        [Test]
        public void ProductionCompanionFeedback_UsesShortMonoCuesAndOneReusableSparklePrefab()
        {
            var configuration = AssetDatabase.LoadAssetAtPath<FairyApplicationConfiguration>(
                ProductionConfigurationPath);
            Assert.That(configuration, Is.Not.Null);
            Assert.That(configuration.TryGet(out var definition), Is.True);

            var feedback = definition.CompanionFeedback;
            Assert.That(feedback, Is.Not.Null);
            Assert.That(feedback.EffectPrefab.name, Is.EqualTo("OppySparkles"));
            Assert.That(
                AssetDatabase.GetAssetPath(feedback.EffectPrefab),
                Is.EqualTo(
                    "Assets/BotanicalGardenQR/Content/Shared/Fairy/Arrival/OppyParticles/OppySparkles.prefab"));
            var particles = feedback.EffectPrefab.GetComponent<ParticleSystem>();
            Assert.That(particles, Is.Not.Null);
            Assert.That(particles.main.loop, Is.True);
            Assert.That(particles.main.playOnAwake, Is.False);
            Assert.That(particles.emission.rateOverTime.constantMax, Is.EqualTo(7f).Within(0.001f));
            Assert.That(feedback.EffectLocalOffset, Is.EqualTo(new Vector3(0f, 0.4f, 0f)));
            Assert.That(feedback.EffectLocalScale, Is.EqualTo(1f));

            Assert.That(feedback.CoachAttentionClip.name, Is.EqualTo("SFX_WordCloud_Appear_01"));
            Assert.That(feedback.CoachAttentionClip.length, Is.InRange(0.1f, 0.5f));
            Assert.That(feedback.CoachAttentionVolume, Is.EqualTo(0.42f));
            Assert.That(feedback.CoachAttentionEffectSeconds, Is.EqualTo(0.55f));
            Assert.That(feedback.CoachAttentionParticleBurst, Is.EqualTo(6));
            Assert.That(feedback.ArtifactWaitClip.name, Is.EqualTo("SFX_WordCloud_Appear_02"));
            Assert.That(feedback.ArtifactWaitClip.length, Is.InRange(0.05f, 0.2f));
            Assert.That(feedback.ArtifactWaitVolume, Is.EqualTo(0.28f));
            Assert.That(feedback.ArtifactWaitEffectSeconds, Is.EqualTo(0.35f));
            Assert.That(feedback.ArtifactWaitParticleBurst, Is.EqualTo(4));
            Assert.That(feedback.ArtifactReturnClip.name,
                Is.EqualTo("SFX_WordCloud_WordScrunch_Answer"));
            Assert.That(feedback.ArtifactReturnClip.length, Is.InRange(0.4f, 0.7f));
            Assert.That(feedback.ArtifactReturnVolume, Is.EqualTo(0.52f));
            Assert.That(feedback.ArtifactReturnEffectSeconds, Is.EqualTo(0.65f));
            Assert.That(feedback.ArtifactReturnParticleBurst, Is.EqualTo(8));
            Assert.That(feedback.CelebrateClip.name,
                Is.EqualTo("UI_Panel_Wrist_Action_CompleteCheckMark"));
            Assert.That(feedback.CelebrateClip.length, Is.InRange(0.8f, 1.4f));
            Assert.That(feedback.CelebrateVolume, Is.EqualTo(0.82f));
            Assert.That(feedback.CelebrateEffectSeconds, Is.EqualTo(1.2f));
            Assert.That(feedback.CelebrateParticleBurst, Is.EqualTo(12));
            Assert.That(feedback.IdleLocomotionClips.Count, Is.EqualTo(14));
            Assert.That(
                feedback.IdleLocomotionClips.Select(clip => clip.name),
                Is.EqualTo(new[]
                {
                    "SFX_Berries_Squeak_01",
                    "SFX_Berries_Squeak_02",
                    "SFX_Berries_Squeak_03",
                    "SFX_Berries_Squeak_04",
                    "SFX_Berries_Squeak_05",
                    "SFX_Berries_Squeak_06",
                    "SFX_Berries_Squeak_07",
                    "SFX_Berries_Squeak_08",
                    "FairyVoice_01_Hello",
                    "FairyVoice_02_Explore",
                    "FairyVoice_03_Ready",
                    "FairyVoice_04_Great",
                    "FairyVoice_05_Discovery",
                    "FairyVoice_06_Rest"
                }));
            Assert.That(feedback.IdleLocomotionClips.All(clip => clip.length > 0f), Is.True);
            Assert.That(feedback.IdleLocomotionVolume, Is.EqualTo(0.42f));
            Assert.That(feedback.IdleLocomotionMinIntervalSeconds, Is.EqualTo(3f));
            Assert.That(feedback.IdleLocomotionMaxIntervalSeconds, Is.EqualTo(7f));
            Assert.That(feedback.IdleLocomotionEffectSeconds, Is.EqualTo(0.25f));
            Assert.That(feedback.IdleLocomotionParticleBurst, Is.EqualTo(2));

            var attentionPath = AssetDatabase.GetAssetPath(feedback.CoachAttentionClip);
            var artifactWaitPath = AssetDatabase.GetAssetPath(feedback.ArtifactWaitClip);
            var artifactReturnPath = AssetDatabase.GetAssetPath(feedback.ArtifactReturnClip);
            var celebratePath = AssetDatabase.GetAssetPath(feedback.CelebrateClip);
            var idlePaths = feedback.IdleLocomotionClips
                .Select(AssetDatabase.GetAssetPath)
                .ToArray();
            Assert.That(attentionPath,
                Does.EndWith("ReferenceAssets/SpatialLingo/Audio/SFX_WordCloud_Appear_01.wav"));
            Assert.That(artifactWaitPath,
                Does.EndWith("ReferenceAssets/SpatialLingo/Audio/SFX_WordCloud_Appear_02.wav"));
            Assert.That(artifactReturnPath,
                Does.EndWith("ReferenceAssets/SpatialLingo/Audio/SFX_WordCloud_WordScrunch_Answer.wav"));
            Assert.That(celebratePath,
                Does.EndWith("ReferenceAssets/FirstHand/Audio/UI_Panel_Wrist_Action_CompleteCheckMark.wav"));
            Assert.That((AssetImporter.GetAtPath(attentionPath) as AudioImporter)?.forceToMono, Is.True);
            Assert.That((AssetImporter.GetAtPath(artifactWaitPath) as AudioImporter)?.forceToMono, Is.True);
            Assert.That((AssetImporter.GetAtPath(artifactReturnPath) as AudioImporter)?.forceToMono, Is.True);
            Assert.That((AssetImporter.GetAtPath(celebratePath) as AudioImporter)?.forceToMono, Is.True);
            Assert.That(
                idlePaths.Take(8).All(path =>
                    path.Replace('\\', '/')
                        .StartsWith(
                            "Assets/BotanicalGardenQR/Experience/Bootstrap/Art/GamePresentation/ReferenceAssets/" +
                            "SpatialLingo/Audio/Squeaks/SFX_Berries_Squeak_",
                            StringComparison.Ordinal) &&
                    path.EndsWith(".wav", StringComparison.Ordinal)),
                Is.True);
            Assert.That(
                idlePaths.Skip(8).All(path =>
                    path.Replace('\\', '/')
                        .StartsWith(
                            "Assets/BotanicalGardenQR/Content/Shared/Fairy/Audio/OfflineVoice/FairyVoice_",
                            StringComparison.Ordinal) &&
                    path.EndsWith(".wav", StringComparison.Ordinal)),
                Is.True);
            Assert.That(
                idlePaths.All(path => (AssetImporter.GetAtPath(path) as AudioImporter)?.forceToMono == true),
                Is.True);
        }

        [Test]
        public void ProductionFairy_UsesGroundedOppyAndReferenceAnimationContract()
        {
            var configuration = AssetDatabase.LoadAssetAtPath<FairyApplicationConfiguration>(
                ProductionConfigurationPath);

            Assert.That(configuration, Is.Not.Null);
            Assert.That(configuration.TryGet(out var definition), Is.True);
            Assert.That(definition.Behavior, Is.EqualTo(FairyBehavior.Guide));
            Assert.That(definition.Prefab.name, Is.EqualTo("OppyFairyGuide"));

            var animator = definition.Prefab.GetComponentInChildren<Animator>(true);
            Assert.That(animator, Is.Not.Null);
            Assert.That(animator.avatar, Is.Not.Null);
            Assert.That(animator.avatar.isValid, Is.True);
            Assert.That(animator.applyRootMotion, Is.False);
            Assert.That(animator.runtimeAnimatorController, Is.Not.Null);
            Assert.That(Quaternion.Angle(animator.transform.localRotation, Quaternion.identity), Is.LessThan(0.01f));

            var parameterTypes = animator.parameters.ToDictionary(parameter => parameter.name, parameter => parameter.type);
            Assert.That(parameterTypes["Running"], Is.EqualTo(AnimatorControllerParameterType.Bool));
            Assert.That(parameterTypes["Jumping"], Is.EqualTo(AnimatorControllerParameterType.Trigger));
            Assert.That(parameterTypes["Wave"], Is.EqualTo(AnimatorControllerParameterType.Trigger));
            Assert.That(parameterTypes["Like"], Is.EqualTo(AnimatorControllerParameterType.Trigger));
            Assert.That(parameterTypes["PowerUp"], Is.EqualTo(AnimatorControllerParameterType.Trigger));
            Assert.That(parameterTypes["Wonder"], Is.EqualTo(AnimatorControllerParameterType.Bool));

            var clipNames = animator.runtimeAnimatorController.animationClips
                .Select(clip => clip.name)
                .ToArray();
            Assert.That(
                clipNames,
                Is.SupersetOf(new[]
                {
                    "idle_stand",
                    "walk_start",
                    "walk_loop",
                    "walk_stop",
                    "jump_intro",
                    "jump_outro",
                    "reaction_waveHand",
                    "reaction_like_start",
                    "reaction_like_loop",
                    "reaction_like_stop",
                    "reaction_wonder",
                    "reaction_powerUp"
                }));

            var controller = animator.runtimeAnimatorController as AnimatorController;
            Assert.That(controller, Is.Not.Null);
            var stateMachine = controller.layers.Single().stateMachine;
            var states = stateMachine.states.ToDictionary(item => item.state.name, item => item.state);
            var idle = states["Idle"];
            var wave = states["Wave"];
            var likeStart = states["Like Start"];
            var like = states["Like"];
            var likeStop = states["Like Stop"];
            var wonder = states["Wonder"];
            var jumpIntro = states["Jump Intro"];
            var jumpOutro = states["Jump Outro"];
            Assert.That(
                stateMachine.anyStateTransitions.Any(transition =>
                    transition.destinationState == jumpIntro &&
                    transition.conditions.Any(condition =>
                        condition.parameter == "Jumping" &&
                        condition.mode == AnimatorConditionMode.If)),
                Is.True);
            Assert.That(
                jumpIntro.transitions.Any(transition =>
                    transition.destinationState == jumpOutro &&
                    transition.hasExitTime &&
                    Mathf.Approximately(transition.exitTime, 1f)),
                Is.True);
            Assert.That(
                jumpOutro.transitions.Any(transition =>
                    transition.destinationState == idle && transition.hasExitTime),
                Is.True);
            Assert.That(
                idle.transitions.Any(transition =>
                    transition.destinationState == wonder &&
                    transition.conditions.Any(condition =>
                        condition.parameter == "Wonder" &&
                        condition.mode == AnimatorConditionMode.If)),
                Is.True);
            Assert.That(
                likeStart.transitions.Any(transition =>
                    transition.destinationState == like &&
                    transition.hasExitTime &&
                    Mathf.Approximately(transition.exitTime, 0.8125f)),
                Is.True);
            Assert.That(
                like.transitions.Any(transition =>
                    transition.destinationState == wonder &&
                    transition.conditions.Any(condition => condition.parameter == "Wonder")),
                Is.True);
            Assert.That(
                like.transitions.Any(transition =>
                    transition.destinationState == likeStop &&
                    transition.hasExitTime &&
                    Mathf.Approximately(transition.exitTime, 0.92424244f)),
                Is.True);
            Assert.That(
                wave.transitions.Any(transition =>
                    transition.destinationState == wonder &&
                    transition.conditions.Any(condition => condition.parameter == "Wonder")),
                Is.True);
            Assert.That(
                wonder.transitions.Any(transition =>
                    transition.destinationState == idle &&
                    !transition.hasExitTime &&
                    transition.conditions.Any(condition => condition.parameter == "Wonder" &&
                        condition.mode == AnimatorConditionMode.IfNot)),
                Is.True);

            var jumpClips = animator.runtimeAnimatorController.animationClips
                .Where(clip => clip.name == "jump_intro" || clip.name == "jump_outro")
                .ToArray();
            Assert.That(jumpClips, Has.Length.EqualTo(2));
            Assert.That(
                animator.runtimeAnimatorController.animationClips.All(clip => AnimationUtility.GetAnimationEvents(clip).Length == 0),
                Is.True,
                "Sample-only PlaySound events must not target the stripped VirtualPet runtime.");

            var renderers = definition.Prefab.GetComponentsInChildren<Renderer>(true);
            Assert.That(renderers, Is.Not.Empty);
            var materials = renderers
                .SelectMany(renderer => renderer.sharedMaterials)
                .Where(material => material != null)
                .ToArray();
            Assert.That(materials, Is.Not.Empty);
            Assert.That(materials.All(material => material.shader != null), Is.True);
            Assert.That(
                materials.All(material => material.shader.name == "TheWorldBeyond/ToonFoggyOppy"),
                Is.True);

            var missingScripts = definition.Prefab
                .GetComponentsInChildren<Transform>(true)
                .Sum(item => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(item.gameObject));
            Assert.That(missingScripts, Is.Zero);
            Assert.That(definition.Prefab.GetComponent("NavMeshAgent"), Is.Null);
        }

        static int[] MaterialRenderQueues(Renderer[] renderers)
            => renderers
                .SelectMany(renderer => renderer.sharedMaterials)
                .Where(material => material != null)
                .Select(material => material.renderQueue)
                .ToArray();

        static void AssertParticleMaterial(
            ParticleSystemRenderer renderer,
            string expectedMaterial,
            string expectedTexture,
            string expectedShader)
        {
            var material = renderer.sharedMaterial;
            Assert.That(material, Is.Not.Null);
            Assert.That(material.name, Is.EqualTo(expectedMaterial));
            Assert.That(material.shader, Is.Not.Null);
            Assert.That(material.shader.name, Is.EqualTo(expectedShader));
            Assert.That(material.shader.passCount, Is.EqualTo(1));
            Assert.That(material.mainTexture, Is.Not.Null);
            Assert.That(material.mainTexture.name, Is.EqualTo(expectedTexture));
        }

        static void SetField<T>(FairyApplicationConfiguration configuration, string name, T value)
        {
            var field = typeof(FairyApplicationConfiguration)
                .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            if (field == null)
                throw new InvalidOperationException($"Missing serialized field '{name}'.");
            field.SetValue(configuration, value);
        }
    }
}
