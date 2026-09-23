# Endoscopy VR native startup isolation

Embedded from Meta MR Utility Kit 205.0.0, package fingerprint
2979546e717905c3acdd7cf71231f1a26851db9d. Original license and notices retained.

The production application uses VR and real tracked hands, not MRUK room capture,
QR trackables or MR world locking. The original BeforeSplashScreen callback ran
even in an empty scene and even when every MRUK scene object was disabled.

On this workstation both the production scene and an empty scene terminated with
0xc0000409 after CreateGlobalContext failed and initialization continued to the
tracking-space callback. Evidence: artifacts/production-play/baseline-*.log.

Core/Scripts/MRUK.Shared.cs now compiles the automatic initialization attribute
only with ENDOSCOPY_ENABLE_ARCHIVED_MR. The default VR editor and Android player
do not initialize this unused native service. Archived source/API stays available
for historical compilation; enabling historical MR requires explicit configuration
and its own validation. This does not disable Meta core or real hand tracking.

Keep this patch in the embedded package, never only in Library/PackageCache.
Revalidate tools/test_production_play.ps1 after any Meta package upgrade.

2026-09-23 Quest follow-up: gate MRUKGlobalContext.BeforeSceneLoad with the same archived-MR symbol. Guard UpdateGlobalContext with successful context creation, clear on release, and return on CreateGlobalContext failure. Device code92 repeatedly called SetBaseSpace without initialization; see docs/QUEST_LOBBY_DIAGNOSIS_20260923.md. Real tracked hands/Meta core remain enabled.
