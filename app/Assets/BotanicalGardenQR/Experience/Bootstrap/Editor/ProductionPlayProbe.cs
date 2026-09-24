using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.XR;
using TMPro;
using BotanicalGardenQR.FrontendShell.Contracts;
using BotanicalGardenQR.VisitorCoach.Frontend;
using Oculus.Interaction;

namespace BotanicalGardenQR.Bootstrap.Editor
{
    // Enters real Play mode: does not construct the runtime or fake XR samples.
    [InitializeOnLoad]
    public static class ProductionPlayProbe
    {
        const string Key = "Endoscopy.ProductionPlayProbe.";
        static double _entered;
        static int _routeScreenPhase;
        static double _routeScreenPhaseAt;
        static int _routeRoomIndex;
        static readonly string[] RouteScreenRooms =
            { "R02_STORAGE", "R03_WAITING", "R04_GI", "R04_RESP", "R05_REPROCESSING", "R01_OFFICE" };
        static ProductionPlayProbe()
        {
            EditorApplication.playModeStateChanged += Changed;
            EditorApplication.update += Tick;
        }

        public static void Run()
        {
            var args = Environment.GetCommandLineArgs();
            var index = Array.IndexOf(args, "-endoscopyProbeOutput");
            if (index < 0 || index + 1 >= args.Length) throw new ArgumentException("Probe output required.");
            var path = Path.GetFullPath(args[index + 1]);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var empty = args.Contains("-endoscopyProbeEmpty");
            var missingInitialRoom = args.Contains("-endoscopyProbeMissingInitialRoom");
            var routeScreens = args.Contains("-endoscopyProbeRouteScreens");
            if (empty && missingInitialRoom)
                throw new ArgumentException("Empty-scene and missing-initial-room probes cannot run together.");
            SessionState.SetString(Key + "Output", path);
            SessionState.SetBool(Key + "Empty", empty);
            SessionState.SetBool(Key + "MissingInitialRoom", missingInitialRoom);
            SessionState.SetBool(Key + "RouteScreens", routeScreens);
            _routeScreenPhase = 0;
            _routeScreenPhaseAt = 0;
            _routeRoomIndex = 0;
            SessionState.SetBool(Key + "Running", true);
            SessionState.SetBool(Key + "Passed", false);
            File.WriteAllText(path, "Preparing real Play mode\n");
            if (missingInitialRoom)
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            if (empty)
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            else
                EditorSceneManager.OpenScene("Assets/BotanicalGardenQR/Scenes/Visitor/BotanicalGardenVisitor.unity");
            EditorApplication.EnterPlaymode();
        }

        static void Changed(PlayModeStateChange state)
        {
            if (!SessionState.GetBool(Key + "Running", false)) return;
            Append(state.ToString());
            if (state == PlayModeStateChange.EnteredPlayMode) _entered = EditorApplication.timeSinceStartup;
            if (state == PlayModeStateChange.EnteredEditMode)
            {
                var passed = SessionState.GetBool(Key + "Passed", false);
                SessionState.SetBool(Key + "Running", false);
                Append(passed ? "PASS" : "FAIL");
                EditorApplication.Exit(passed ? 0 : 2);
            }
        }

        static void Tick()
        {
            if (!SessionState.GetBool(Key + "Running", false) || !EditorApplication.isPlaying || _entered <= 0 ||
                EditorApplication.timeSinceStartup - _entered < 12) return;
            if (SessionState.GetBool(Key + "RouteScreens", false))
            {
                try { TickRouteScreens(); }
                catch (Exception error) { FailRouteScreens(error.ToString()); }
                return;
            }
            _entered = 0;
            var empty = SessionState.GetBool(Key + "Empty", false);
            var objects = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (SessionState.GetBool(Key + "MissingInitialRoom", false))
            {
                var failureInstaller = objects.OfType<VisitorInstaller>().SingleOrDefault();
                var failureRuntime = failureInstaller ? failureInstaller.Journey : null;
                var failureCurtain = GameObject.Find("FullScriptTransitionCurtain");
                var retry = GameObject.Find("RetryRoom");
                var failureMessage = failureCurtain ? failureCurtain.GetComponentsInChildren<TMP_Text>(true).FirstOrDefault(text => text.name == "Message") : null;
                var errorShown = failureMessage && failureMessage.text.Contains("大厅暂时无法加载") && failureMessage.text.Contains("请轻触按钮重试");
                var noLobby = GameObject.Find("LobbyPanorama(Clone)") == null;
                var failureLegacy = objects.Any(component => component &&
                    (component.GetType().Name == "VisitorProloguePresenter" || component.GetType().Name == "VisitorHandReadinessAdapter"));
                var failurePassed = failureInstaller && failureRuntime != null && failureRuntime.Visit == null && !failureRuntime.InputAllowed &&
                    retry && retry.activeInHierarchy && failureCurtain && failureCurtain.activeInHierarchy && errorShown && noLobby && !failureLegacy;
                Append($"initialRoomFailure={failurePassed}; retryVisible={retry && retry.activeInHierarchy}; curtainVisible={failureCurtain && failureCurtain.activeInHierarchy}; " +
                    $"learnerMessage={failureMessage?.text}; panoramaMissing={noLobby}; legacyOpening={failureLegacy}; graphics={SystemInfo.graphicsDeviceType}");
                if (failurePassed)
                {
                    var camera = failureInstaller.CreateValidatedBindings().Platform.Viewer.GetComponent<Camera>();
                    Capture(camera, Path.ChangeExtension(SessionState.GetString(Key + "Output", ""), "png"));
                }
                SessionState.SetBool(Key + "Passed", failurePassed);
                EditorApplication.ExitPlaymode();
                return;
            }
            var lobby = GameObject.Find("LobbyPanorama(Clone)") != null && !objects.Any(o => o && o.GetType().Name == "GaussianSplatRenderer");
            var curtain = GameObject.Find("FullScriptTransitionCurtain");
            var installer=objects.OfType<VisitorInstaller>().SingleOrDefault();
            var guide=objects.OfType<VisitorCoachPresenter>().SingleOrDefault();
            var ready=installer?.Journey?.InputAllowed==true &&
                guide?.CurrentState?.Owner==VisitorDialogueOwner.Guidance &&
                installer.Journey.Visit.Panel==null;
            var previewNotice=guide?.CurrentState?.Body.Contains("当前仅显示场景预览")==true;
            var previewStateCorrect=XRSettings.isDeviceActive?!previewNotice:previewNotice;
            if(guide && installer)
            {
                var button=guide.GetComponentsInChildren<UnityEngine.UI.Button>(true)
                    .FirstOrDefault(candidate=>candidate.name=="Continue");
                var pointable=button?button.GetComponent<VisitorDialoguePointableTarget>():null;
                var poke=button?button.GetComponent<PokeInteractable>():null;
                Append($"welcomeButton={button?.isActiveAndEnabled}/{button?.IsInteractable()}; "+
                    $"pointable={pointable?.isActiveAndEnabled}; poke={poke?.isActiveAndEnabled}; "+
                    $"buttonDistance={(button ? Vector3.Distance(installer.CreateValidatedBindings().Platform.Viewer.position,button.transform.position):float.NaN):F3}");
                var activeSurfaces=UnityEngine.Object.FindObjectsByType<PokeInteractable>(
                        FindObjectsInactive.Exclude,FindObjectsSortMode.None)
                    .OrderBy(surface=>Vector3.Distance(installer.CreateValidatedBindings().Platform.Viewer.position,
                        surface.transform.position)).Take(16)
                    .Select(surface=>$"{surface.name}:{Vector3.Distance(installer.CreateValidatedBindings().Platform.Viewer.position,surface.transform.position):F3}");
                Append("nearestPokeSurfaces="+string.Join(", ",activeSurfaces));
            }
            var legacy=objects.Any(o=>o && (o.GetType().Name=="VisitorProloguePresenter" || o.GetType().Name=="VisitorHandReadinessAdapter"));
            var passed = empty || (lobby && !curtain && ready && !legacy && previewStateCorrect);
            Append($"empty={empty}; activePanorama={lobby}; curtainVisible={curtain != null}; briefReady={ready}; legacyOpening={legacy}; " +
                $"xrDisplayActive={XRSettings.isDeviceActive}; previewNoticeVisible={previewNotice}; previewStateCorrect={previewStateCorrect}; graphics={SystemInfo.graphicsDeviceType}");
            if(passed && !empty)
            {
                var camera=installer.CreateValidatedBindings().Platform.Viewer.GetComponent<Camera>();
                var output=Path.ChangeExtension(SessionState.GetString(Key+"Output",""),"png");
                Capture(camera,output);
            }
            SessionState.SetBool(Key + "Passed", passed);
            EditorApplication.ExitPlaymode();
        }

        static void TickRouteScreens()
        {
            var output = SessionState.GetString(Key + "Output", "");
            var elapsed = EditorApplication.timeSinceStartup - _entered;
            if (elapsed > 240) { FailRouteScreens("The no-XR route screen capture timed out."); return; }
            var objects = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            var installer = objects.OfType<VisitorInstaller>().SingleOrDefault();
            var journey = installer ? installer.Journey : null;
            var guide = objects.OfType<VisitorCoachPresenter>().SingleOrDefault();
            if (journey == null || !installer || !guide) return;
            var camera = installer.CreateValidatedBindings().Platform.Viewer.GetComponent<Camera>();
            var panel = journey.Visit?.Panel;

            if (_routeScreenPhase == 0)
            {
                if (!journey.InputAllowed || guide.CurrentState?.Owner != VisitorDialogueOwner.Guidance) return;
                SaveRouteScreen(camera, "01-lobby-welcome");
                if (!PokeDialoguePrimary(guide)) throw new InvalidOperationException("Could not exercise the welcome hand-poke surface.");
                NextRouteScreenPhase(1);
                return;
            }
            if (_routeScreenPhase == 1)
            {
                var gallery = GameObject.Find("FullScriptRoomGallery");
                if (!gallery) { RequireRoutePhaseTimeout("Lobby gallery did not appear.", 10); return; }
                if (guide.gameObject.activeSelf)
                { RequireRoutePhaseTimeout("The welcome dialogue did not finish its fade before the gallery capture.", 5); return; }
                SaveRouteScreen(camera, "02-lobby-gallery");
                if (!InvokeButton(gallery, "Travel_R01_OFFICE")) throw new InvalidOperationException("Recommended office gallery card is unavailable.");
                NextRouteScreenPhase(2);
                return;
            }
            if (_routeScreenPhase == 2)
            {
                if (journey.Visit?.RoomId != "R01_OFFICE" || journey.LoadingStage != "Active" ||
                    guide.CurrentState?.Owner != VisitorDialogueOwner.Guidance)
                {
                    var selectionState = $"currentRoom={journey.Session.CurrentRoomId}; stage={journey.LoadingStage}; " +
                                         $"input={journey.InputAllowed}; doorOpen={journey.Visit?.DoorOpen}; " +
                                         $"dragging={journey.Visit?.RoomGalleryIsDragging}; " +
                                         $"canSelect={journey.Visit?.RoomGalleryCanSelect}; " +
                                         $"pendingAction={journey.Visit?.HasPendingStationaryAction}; " +
                                         $"panel={(journey.Visit?.Panel ? journey.Visit.Panel.name : "none")}; " +
                                         $"guide={guide.CurrentState?.Owner}";
                    RequireRoutePhaseTimeout("The office entry guide did not become ready: " + selectionState, 30); return;
                }
                SaveRouteScreen(camera, "03-office-entry-guide");
                NextRouteScreenPhase(8);
                return;
            }
            if (_routeScreenPhase == 8)
            {
                if (EditorApplication.timeSinceStartup - _routeScreenPhaseAt < .4) return;
                if (guide.CurrentState?.Owner != VisitorDialogueOwner.Guidance || !journey.InputAllowed)
                { RequireRoutePhaseTimeout("The office entry guide became unavailable before its input delay elapsed.", 5); return; }
                if (!PokeDialoguePrimary(guide)) throw new InvalidOperationException("Could not exercise the office entry hand-poke surface.");
                Append($"officeEntryPoke: inputAllowed={journey.InputAllowed}; guidanceStillVisible={guide.CurrentState?.Owner == VisitorDialogueOwner.Guidance}; panel={(journey.Visit?.Panel ? journey.Visit.Panel.name : "none")}");
                NextRouteScreenPhase(3);
                return;
            }
            if (EditorApplication.timeSinceStartup - _routeScreenPhaseAt < .25) return;
            if (_routeScreenPhase == 3)
            {
                if (guide.gameObject.activeSelf)
                { RequireRoutePhaseTimeout("The office entry dialogue did not finish its fade before the topic selector capture.", 5); return; }
                RequirePanel(journey, "RoomThemeSelector");
                SaveRouteScreen(camera, "04-office-topic-selector");
                if (!InvokeButton(panel, "Theme_0")) throw new InvalidOperationException("Office room observation is unavailable.");
                NextRouteScreenPhase(15);
                return;
            }
            if (_routeScreenPhase == 15)
            {
                if (!panel || panel.name != "StationaryScriptPanel")
                { RequireRoutePhaseTimeout("The office observation task did not open.", 10); return; }
                SaveRouteScreen(camera, "04a-office-task-panel");
                if (!InvokeButton(panel, "ChooseRoomTheme")) throw new InvalidOperationException("Could not return to the office topics.");
                NextRouteScreenPhase(16);
                return;
            }
            if (_routeScreenPhase == 16)
            {
                if (!panel || panel.name != "RoomThemeSelector")
                { RequireRoutePhaseTimeout("Office topic choices did not return from the task.", 10); return; }
                if (!InvokeButton(panel, "Theme_1")) throw new InvalidOperationException("Office electronic-record topic is unavailable.");
                NextRouteScreenPhase(4);
                return;
            }
            if (_routeScreenPhase == 4)
            {
                if (journey.LoadingStage != "Active" || !panel || panel.name != "OfficeRecordsExpanded")
                { RequireRoutePhaseTimeout("The office computer did not open after its observation transition.", 20); return; }
                SaveRouteScreen(camera, "05-office-electronic-records");
                if (!InvokeButton(panel, "OpenDocuments")) throw new InvalidOperationException("The office document action is unavailable.");
                NextRouteScreenPhase(7);
                return;
            }
            if (_routeScreenPhase == 7)
            {
                RequirePanel(journey, "OfficeRecordsExpanded");
                if (!panel.transform.Find("DocumentImage")) throw new InvalidOperationException("The selected office document image is missing.");
                SaveRouteScreen(camera, "06-office-document-image");
                if (!InvokeButton(panel, "ZoomDocument")) throw new InvalidOperationException("The office document reading action is unavailable.");
                NextRouteScreenPhase(13);
                return;
            }
            if (_routeScreenPhase == 13)
            {
                RequirePanel(journey, "OfficeRecordsExpanded");
                if (panel.transform.Find("DocumentGalleryControls") || !panel.transform.Find("DocumentImage"))
                    throw new InvalidOperationException("Focused office document reading did not replace the choice menu.");
                SaveRouteScreen(camera, "06a-office-document-reading");
                if (!InvokeButton(panel, "ZoomDocument")) throw new InvalidOperationException("Could not return to office document choices.");
                NextRouteScreenPhase(14);
                return;
            }
            if (_routeScreenPhase == 14)
            {
                RequirePanel(journey, "OfficeRecordsExpanded");
                if (!panel.transform.Find("DocumentGalleryControls"))
                    throw new InvalidOperationException("Office document choices did not return from focused reading.");
                if (!journey.Visit.TryOpen(FullScriptRoomCatalog.Door))
                    throw new InvalidOperationException("Could not return from the office to the production room gallery.");
                _routeRoomIndex = 0;
                NextRouteScreenPhase(9);
                return;
            }
            if (_routeScreenPhase == 9)
            {
                var gallery = GameObject.Find("FullScriptRoomGallery");
                if (!gallery) { RequireRoutePhaseTimeout("The room gallery did not return after leaving a room.", 10); return; }
                if (guide.gameObject.activeSelf)
                { RequireRoutePhaseTimeout("A room entry dialogue did not finish its fade before the gallery capture.", 5); return; }
                var roomId = RouteScreenRooms[_routeRoomIndex];
                SaveRouteScreen(camera, RouteScreenStem(_routeRoomIndex, roomId, "gallery"));
                if (!InvokeButton(gallery, "Travel_" + roomId))
                    throw new InvalidOperationException("Room gallery card is unavailable: " + roomId);
                NextRouteScreenPhase(10);
                return;
            }
            if (_routeScreenPhase == 10)
            {
                var roomId = RouteScreenRooms[_routeRoomIndex];
                if (journey.Visit?.RoomId != roomId || journey.LoadingStage != "Active" ||
                    guide.CurrentState?.Owner != VisitorDialogueOwner.Guidance)
                { RequireRoutePhaseTimeout("The entry guide did not become ready for " + roomId + ".", 35); return; }
                SaveRouteScreen(camera, RouteScreenStem(_routeRoomIndex, roomId, "entry-guide"));
                NextRouteScreenPhase(11);
                return;
            }
            if (_routeScreenPhase == 11)
            {
                if (EditorApplication.timeSinceStartup - _routeScreenPhaseAt < .4) return;
                if (guide.CurrentState?.Owner != VisitorDialogueOwner.Guidance || !journey.InputAllowed)
                { RequireRoutePhaseTimeout("The entry guide became unavailable before its input delay elapsed.", 5); return; }
                if (!PokeDialoguePrimary(guide))
                    throw new InvalidOperationException("Could not exercise the entry hand-poke surface in " + RouteScreenRooms[_routeRoomIndex] + ".");
                NextRouteScreenPhase(12);
                return;
            }
            if (_routeScreenPhase == 12)
            {
                var roomId = RouteScreenRooms[_routeRoomIndex];
                panel = journey.Visit?.Panel;
                if (journey.Visit?.RoomId != roomId || journey.LoadingStage != "Active" || !panel)
                { RequireRoutePhaseTimeout("The first task choice did not appear in " + roomId + ".", 30); return; }
                if (guide.gameObject.activeSelf)
                { RequireRoutePhaseTimeout("The room entry dialogue did not finish its fade before the task choice capture.", 5); return; }
                SaveRouteScreen(camera, RouteScreenStem(_routeRoomIndex, roomId, "first-choice"));
                Append("roomSurface=" + roomId + "; panel=" + panel.name);
                if (!journey.Visit.TryOpen(FullScriptRoomCatalog.Door))
                    throw new InvalidOperationException("Could not return to the gallery from " + roomId + ".");
                _routeRoomIndex++;
                if (_routeRoomIndex >= RouteScreenRooms.Length)
                {
                    CompleteRouteScreens(output);
                    return;
                }
                NextRouteScreenPhase(9);
            }
        }

        static string RouteScreenStem(int index, string roomId, string view)
        {
            var viewOffset = view == "gallery" ? 0 : view == "entry-guide" ? 1 : 2;
            return (7 + index * 3 + viewOffset).ToString("D2") + "-" + roomId.ToLowerInvariant().Replace('_', '-') + "-" + view;
        }

        static void RequirePanel(FullScriptJourneyRuntime journey, string expected)
        {
            var panel = journey.Visit?.Panel;
            if (!panel || panel.name != expected)
                throw new InvalidOperationException($"Expected {expected}; found {(panel ? panel.name : "no panel")}.");
        }

        static bool InvokeButton(GameObject root, string name)
        {
            if (!root) return false;
            var button = root.GetComponentsInChildren<UnityEngine.UI.Button>(true).SingleOrDefault(candidate => candidate && candidate.name == name);
            if (!button || !button.isActiveAndEnabled || !button.IsInteractable()) return false;
            button.onClick.Invoke();
            return true;
        }

        static bool PokeDialoguePrimary(VisitorCoachPresenter guide)
        {
            var button = guide.GetComponentsInChildren<UnityEngine.UI.Button>(true).FirstOrDefault(candidate => candidate && candidate.name == "Continue");
            var target = button ? button.GetComponent<VisitorDialoguePointableTarget>() : null;
            var rect = button ? button.transform as RectTransform : null;
            var sample = typeof(VisitorDialoguePointableTarget).GetMethod("SampleTrackedFinger", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            if (!target || !rect || sample == null || Mathf.Abs(rect.lossyScale.z) < .000001f) return false;
            foreach (var depth in new[] { .04f, .015f, 0f, -.005f })
            {
                var local = new Vector3(0, 0, -depth / rect.lossyScale.z);
                sample.Invoke(target, new object[] { 0, rect.TransformPoint(local) });
            }
            return true;
        }

        static void SaveRouteScreen(Camera camera, string name)
        {
            var output = SessionState.GetString(Key + "Output", "");
            var folder = Path.GetDirectoryName(output);
            var stem = Path.GetFileNameWithoutExtension(output);
            var path = Path.Combine(folder, stem + "-" + name + ".png");
            Capture(camera, path);
            Append("screen=" + path);
        }

        static void NextRouteScreenPhase(int phase)
        {
            _routeScreenPhase = phase;
            _routeScreenPhaseAt = EditorApplication.timeSinceStartup;
        }

        static void RequireRoutePhaseTimeout(string message, double seconds)
        {
            if (EditorApplication.timeSinceStartup - _routeScreenPhaseAt > seconds)
                throw new TimeoutException(message);
        }

        static void CompleteRouteScreens(string output)
        {
            var notes = Path.Combine(Path.GetDirectoryName(output), Path.GetFileNameWithoutExtension(output) + "-capture-notes.txt");
            File.WriteAllText(notes,
                "Unity Editor Play / Android target / Vulkan / XR display inactive. Images come from the production visitor scene and runtime room loader. " +
                "The probe feeds a synthetic tracked-finger path through the production dialogue hit test, invokes visible Unity UI buttons for room selection and office records, and requests room changes through the production visit API; it does not validate real hand tracking, headset optics, or Quest performance.\n");
            Append("routeScreens=PASS; captureNotes=" + notes);
            SessionState.SetBool(Key + "Passed", true);
            EditorApplication.ExitPlaymode();
        }

        static void FailRouteScreens(string reason)
        {
            Append("routeScreens=FAIL; " + reason);
            SessionState.SetBool(Key + "Passed", false);
            EditorApplication.ExitPlaymode();
        }

        static void Append(string text)
        {
            File.AppendAllText(SessionState.GetString(Key + "Output", ""), DateTime.UtcNow.ToString("O") + " " + text + "\n");
        }
        internal static void Capture(Camera camera,string path)
        {
            Canvas.ForceUpdateCanvases();
            var target=new RenderTexture(1440,1080,24);
            var previous=RenderTexture.active;var oldTarget=camera.targetTexture;
            var image=new Texture2D(1440,1080,TextureFormat.RGB24,false);
            try
            {
                target.Create();camera.targetTexture=target;
                camera.Render();camera.Render();RenderTexture.active=target;
                image.ReadPixels(new Rect(0,0,1440,1080),0,0);image.Apply();
                File.WriteAllBytes(path,image.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture=oldTarget;RenderTexture.active=previous;
                target.Release();UnityEngine.Object.DestroyImmediate(target);UnityEngine.Object.DestroyImmediate(image);
            }
        }
    }
}
