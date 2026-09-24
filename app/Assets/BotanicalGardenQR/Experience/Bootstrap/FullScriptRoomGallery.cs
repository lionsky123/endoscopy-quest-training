using System;
using System.Collections.Generic;
using System.Linq;
using BotanicalGardenQR.Experience.Application;
using BotanicalGardenQR.Experience.Contracts;
using BotanicalGardenQR.FrontendShell.Runtime;
using Oculus.Interaction.Input;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static BotanicalGardenQR.FrontendShell.Runtime.ClinicalPanelStyle;

namespace BotanicalGardenQR.Bootstrap
{
    internal sealed partial class FullScriptRoomVisit
    {
        sealed class GalleryCard
        {
            public Button Button;
            public RectTransform Shadow;
            public CanvasGroup Group;
            public CanvasGroup ShadowGroup;
            public string RoomId;
            public float SlotOffsetDegrees;
        }

        const string GalleryObjectName = "FullScriptRoomGallery";
        const float GalleryPanelScale = .00072f;
        const float GalleryRadius = .72f;
        const float GalleryHandleForward = .72f;
        const float GalleryCardStepDegrees = 27f;
        const float GallerySelectionRearmDelay = .25f;
        readonly FullScriptRoomGalleryDragState _roomGalleryDrag = new FullScriptRoomGalleryDragState();
        readonly List<GalleryCard> _roomGalleryCards = new List<GalleryCard>();
        RectTransform _roomGalleryHandle;
        Image _roomGalleryHandleSurface;
        TMP_Text _roomGalleryHandleLabel;
        TMP_Text _roomGalleryInstruction;
        RawImage _roomGalleryTutorialGesture;
        Button _roomGalleryTutorialAction;
        float _roomGalleryRotation;
        float _roomGallerySelectionEnableAt;

        internal bool RoomGalleryCanSelect => !_roomGalleryDrag.IsDragging && Time.unscaledTime >= _roomGallerySelectionEnableAt;
        internal bool RoomGalleryIsDragging => _roomGalleryDrag.IsDragging;

        void ShowRoomGallery()
        {
            _owner.GalleryTutorial.Begin();
            _owner.GalleryTutorial.Skip();
            _roomGalleryCards.Clear();
            _roomGalleryDrag.Cancel();
            _roomGalleryRotation = 0;
            _roomGallerySelectionEnableAt = 0;
            _panel = new GameObject(GalleryObjectName, typeof(RectTransform), typeof(Canvas));
            var board = (RectTransform)_panel.transform;
            var forward = Vector3.ProjectOnPlane(_viewer.forward, Vector3.up).normalized;
            if (forward.sqrMagnitude < .1f) forward = Vector3.forward;
            board.SetPositionAndRotation(_viewer.position + forward * 1.32f, Quaternion.LookRotation(forward, Vector3.up));
            board.localScale = Vector3.one * GalleryPanelScale;
            board.sizeDelta = new Vector2(2900, 1720);
            var canvas = _panel.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = _viewer.GetComponent<Camera>();
            canvas.sortingOrder = 130;

            var header = Rect(board, "RoomGalleryHeader", 0, 665, 1540, 180);
            Fill(header, ClinicalPanelStyle.Shell, 20);
            Outline(Rect(header, "HeaderEdge", 0, 0, 1540, 180),
                ClinicalPanelStyle.ShellEdge, 20, 2);
            Fill(Rect(header, "HeaderHighlight", 0, 82, 1450, 2), new Color(1f, 1f, 1f, .22f), 1);
            var eyebrow = Label(header, _font, "RoomGalleryEyebrow", -280, 71, 900, 30, 20);
            eyebrow.text = "房间导览";
            eyebrow.color = ClinicalPanelStyle.Confirmation;
            var roomTitle = Label(header, _font, "RoomGalleryTitle", -280, 34, 900, 78, 56);
            roomTitle.alignment = TextAlignmentOptions.MidlineLeft;
            roomTitle.fontStyle = FontStyles.Bold;
            roomTitle.text = "选择房间";
            roomTitle.color = ClinicalPanelStyle.ShellText;
            var currentName = _owner.Definition.FindRoom(RoomId)?.displayName ?? RoomId;
            var nextIndex = _owner.Session.MainlineIndex + 1;
            var nextRoom = !_owner.Session.IsFinished && nextIndex < _owner.Definition.mainlineRoomIds.Length
                ? _owner.Definition.FindRoom(_owner.Definition.mainlineRoomIds[nextIndex])?.displayName
                : null;
            var summary = Label(header, _font, "RoomGallerySummary", 420, 0, 800, 112, 40);
            summary.alignment = TextAlignmentOptions.MidlineLeft;
            summary.text = nextRoom == null
                ? "当前房间：" + currentName + "\n训练场景示意"
                : "当前房间：" + currentName + "\n推荐下一站：" + nextRoom + "　 · 　训练场景示意";
            summary.color = ClinicalPanelStyle.ShellText;

            var tapHint = Label(board, _font, "WalkingHint", -280, 610, 900, 44, 36);
            tapHint.alignment = TextAlignmentOptions.MidlineLeft;
            tapHint.color = ClinicalPanelStyle.ShellText;
            tapHint.text = "轻触两侧换图 · 轻触图片进入";
            _roomGalleryInstruction = tapHint;

            var rail = Rect(board, "RoomGalleryHandleRail", 0, -400, 740, 140);
            rail.localPosition = new Vector3(0, rail.localPosition.y, -GalleryHandleForward / GalleryPanelScale + 14);
            Fill(rail, ClinicalPanelStyle.Shell, 26);
            Outline(Rect(rail, "RailEdge", 0, 0, 740, 140), ClinicalPanelStyle.ShellEdge, 26, 2);
            var previous = Button(rail, _font, "GalleryPrevious", "上一张", -306, 0, 112, 98,
                () => QueueStationaryAction(() => StepGallery(-1)));
            var next = Button(rail, _font, "GalleryNext", "下一张", 306, 0, 112, 98,
                () => QueueStationaryAction(() => StepGallery(1)));

            _roomGalleryHandle = Rect(board, "RoomGalleryDragHandle", 0, -400, 450, 108);
            _roomGalleryHandle.localPosition = new Vector3(0, _roomGalleryHandle.localPosition.y,
                -GalleryHandleForward / GalleryPanelScale);
            _roomGalleryHandleSurface = Fill(_roomGalleryHandle, ClinicalPanelStyle.SurfaceSubtle, 22);
            Outline(Rect(_roomGalleryHandle, "HandleRim", 0, 0, 450, 108), ClinicalPanelStyle.Border, 22, 3);
            Fill(Rect(_roomGalleryHandle, "HandleTopLight", 0, 48, 380, 2), new Color(1f, 1f, 1f, .46f), 1);
            _roomGalleryHandleLabel = Label(_roomGalleryHandle, _font, "RoomGalleryDragInstruction", -22, 0, 335, 76, 29);
            _roomGalleryHandleLabel.alignment = TextAlignmentOptions.Center;
            _roomGalleryHandleLabel.text = "捏住滑动";
            _roomGalleryHandleLabel.color = ClinicalPanelStyle.TextPrimary;
            _roomGalleryHandleLabel.fontStyle = FontStyles.Bold;
            var gesture = Rect(_roomGalleryHandle, "TutorialGesture", 178, 0, 68, 68);
            _roomGalleryTutorialGesture = gesture.gameObject.AddComponent<RawImage>();
            _roomGalleryTutorialGesture.texture = Resources.Load<Texture2D>("FullScriptRooms/RoomGallery/gallery-gesture-tutorial-v2");
            _roomGalleryTutorialGesture.raycastTarget = false;
            if (!_roomGalleryTutorialGesture.texture)
                Debug.LogWarning("[FullScript] Gallery gesture illustration is missing.");
            _roomGalleryTutorialAction = Button(board, _font, "GalleryTutorialAction", "", 0, -565, 280, 58,
                () => QueueStationaryAction(ToggleGalleryTutorial), primary: false);
            var tutorialActionRect = (RectTransform)_roomGalleryTutorialAction.transform;
            tutorialActionRect.localPosition = new Vector3(tutorialActionRect.localPosition.x,
                tutorialActionRect.localPosition.y, -GalleryHandleForward / GalleryPanelScale);
            UpdateGalleryTutorialAction();

            var destinations = _owner.Destinations().ToArray();
            var ordered = destinations
                .OrderBy(destination => destination.Kind == ClinicalActDestinationKind.ContinueMainline ? 0 : 1)
                .ThenBy(destination => Array.IndexOf(_owner.Definition.rooms, _owner.Definition.FindRoom(destination.RoomId)))
                .ToArray();
            var atlas = Resources.Load<Texture2D>("FullScriptRooms/RoomGallery/room-preview-atlas-v1");
            for (var i = 0; i < ordered.Length; i++)
                CreateGalleryCard(board, ordered[i], i, atlas);

            ClinicalNearTouch.Bind(board, () => InputAllowed && RoomGalleryCanSelect);
            foreach (var card in _roomGalleryCards)
                card.Button.GetComponent<ClinicalNearTouch>().RequireIntentionalPress(.12f, .28f);
            UpdateGalleryHandle(near: false, dragging: false);
            PositionGalleryCards();
        }

        void CreateGalleryCard(RectTransform board, ClinicalActDestination destination, int index, Texture2D atlas)
        {
            var room = _owner.Definition.FindRoom(destination.RoomId);
            if (room == null) return;

            var id = destination.RoomId;
            var isRecommended = destination.Kind == ClinicalActDestinationKind.ContinueMainline;
            var shadow = Rect(board, "CardDepth_" + id, 0, 0, 420, 548);
            Fill(shadow, new Color(.05f, .04f, .10f, .3f), 22);
            var cardRect = Rect(board, "Travel_" + id, 0, 0, 420, 548);
            var baseColor = isRecommended ? ClinicalPanelStyle.Accent : ClinicalPanelStyle.SurfaceRaised;
            var rimColor = isRecommended ? new Color(.78f, .73f, .96f, 1f) : ClinicalPanelStyle.Border;
            var cardSurface = Fill(cardRect, baseColor, 22);
            cardSurface.raycastTarget = true;
            var card = cardRect.gameObject.AddComponent<Button>();
            card.targetGraphic = cardSurface;
            card.navigation = new Navigation { mode = Navigation.Mode.None };
            card.transition = Selectable.Transition.None;
            card.onClick.AddListener(() => QueueStationaryAction(() =>
                {
                    if (InputAllowed && RoomGalleryCanSelect)
                    {
                        if (_owner.RequestRoom(id))
                        {
                            _owner.GalleryTutorial.ObserveSelection(freshPoke: true, dragging: false);
                            UpdateGalleryTutorialAction();
                        }
                    }
                }));
            var label = Label(cardRect, _font, "Label", 0, -168, 390, 58, isRecommended ? 34 : 30);
            label.text = room.displayName;
            label.fontStyle = FontStyles.Bold;
            label.color = isRecommended ? ClinicalPanelStyle.ShellText : ClinicalPanelStyle.TextPrimary;
            label.alignment = TextAlignmentOptions.Center;

            var previewRect = Rect(card.transform, "RoomPreviewImage", 0, 74, 360, 360);
            var preview = previewRect.gameObject.AddComponent<RawImage>();
            preview.raycastTarget = false;
            preview.texture = atlas;
            preview.uvRect = PreviewUv(RoomPreviewIndex(id));
            if (!atlas)
                preview.color = ClinicalPanelStyle.Shell;
            var previewRim = Outline(Rect(card.transform, "RoomPreviewRim", 0, 74, 368, 368), rimColor, 10, 3);
            Fill(Rect(card.transform, "CardUpperGlint", 0, 265, 344, 2),
                isRecommended ? new Color(.85f, .80f, .98f, .7f) : new Color(1f, 1f, 1f, .72f), 1);

            var status = Label(card.transform, _font, "RoomCardStatus", 0, -232, 390, 36, 22);
            status.alignment = TextAlignmentOptions.Center;
            status.color = isRecommended ? ClinicalPanelStyle.ShellText : ClinicalPanelStyle.Muted;
            status.text = destination.Kind switch
            {
                ClinicalActDestinationKind.ContinueMainline => "推荐下一站",
                ClinicalActDestinationKind.ReviewVisitedChapter => "已到访 · 可回看",
                ClinicalActDestinationKind.ReturnToLobby => "返回大厅",
                _ => "可选房间"
            };
            var progressRect = Rect(card.transform, "PressProgress", 0, -268, 356, 5);
            var progress = Fill(progressRect, new Color(ClinicalPanelStyle.Confirmation.r,
                ClinicalPanelStyle.Confirmation.g, ClinicalPanelStyle.Confirmation.b, 0), 2);
            progressRect.pivot = new Vector2(0, .5f);
            progressRect.anchoredPosition = new Vector2(-178, -268);
            cardRect.gameObject.AddComponent<FullScriptGalleryCardFeedback>().Initialize(cardSurface, previewRim, progress, baseColor, rimColor);

            var offsetIndex = index == 0 ? 0 : ((index + 1) / 2) * (index % 2 == 1 ? -1 : 1);
            _roomGalleryCards.Add(new GalleryCard
            {
                Button = card,
                Shadow = shadow,
                Group = cardRect.gameObject.AddComponent<CanvasGroup>(),
                ShadowGroup = shadow.gameObject.AddComponent<CanvasGroup>(),
                RoomId = id,
                SlotOffsetDegrees = offsetIndex * GalleryCardStepDegrees
            });
        }

        void TickRoomGalleryDrag()
        {
            if (!_panel || _panel.name != GalleryObjectName || !_roomGalleryHandle)
            {
                _roomGalleryDrag.Cancel();
                return;
            }

            var hands = _owner.TrackedHands;
            var handlePoint = _roomGalleryHandle.position;
            var nearHandle = false;
            var wasDragging = _roomGalleryDrag.IsDragging;
            // The hand source is the capability gate. Also poll it in Unity Play when
            // XR Link supplies real tracked hands; desktop pointer input is never read.
            if (!Application.isPlaying)
            {
                _roomGalleryDrag.Cancel();
                _owner.GalleryTutorial.OnTrackingLost();
                if (wasDragging) _roomGallerySelectionEnableAt = Time.unscaledTime + GallerySelectionRearmDelay;
                UpdateGalleryHandle(near: false, dragging: false);
                PositionGalleryCards();
                return;
            }

            if (wasDragging)
            {
                var activeHand = _roomGalleryDrag.ActiveHand;
                if (TryReadGalleryHand(hands, activeHand, out var pinchPoint, out var pinching))
                {
                    nearHandle = (pinchPoint - handlePoint).sqrMagnitude <=
                                 FullScriptRoomGalleryDragState.HandleRadius * FullScriptRoomGalleryDragState.HandleRadius;
                    if (pinching)
                        _owner.GalleryTutorial.ObserveHandle(tracked: true, nearHandle: nearHandle, pinching: true);
                    else
                        _owner.GalleryTutorial.ObserveRelease(tracked: true, pinching: false);
                    if (_roomGalleryDrag.Sample(activeHand, true, pinching, pinchPoint,
                            _panel.transform.right, out var deltaDegrees))
                    {
                        _roomGalleryRotation += deltaDegrees;
                        if (pinching)
                            _owner.GalleryTutorial.ObserveMovement(tracked: true, pinching: true, rotationDegrees: deltaDegrees);
                    }
                }
                else
                {
                    _roomGalleryDrag.Sample(activeHand, false, false, default,
                        _panel.transform.right, out _);
                    _owner.GalleryTutorial.OnTrackingLost();
                }

                if (wasDragging && !_roomGalleryDrag.IsDragging)
                    _roomGallerySelectionEnableAt = Time.unscaledTime + GallerySelectionRearmDelay;
            }
            else
            {
                var trackedAny = false;
                var pinchingNearHandle = false;
                for (var i = 0; i < hands.Length; i++)
                {
                    if (!TryReadGalleryHand(hands, i, out var pinchPoint, out var pinching)) continue;
                    trackedAny = true;
                    var near = (pinchPoint - handlePoint).sqrMagnitude <=
                               FullScriptRoomGalleryDragState.HandleRadius * FullScriptRoomGalleryDragState.HandleRadius;
                    nearHandle |= near;
                    pinchingNearHandle |= near && pinching;
                    if (_roomGalleryDrag.TryBegin(i, true, pinching, pinchPoint, handlePoint))
                    {
                        nearHandle = true;
                        break;
                    }
                }
                _owner.GalleryTutorial.ObserveHandle(trackedAny, nearHandle, pinchingNearHandle);
            }

            UpdateGalleryHandle(nearHandle, _roomGalleryDrag.IsDragging);
            UpdateGalleryTutorialAction();
            PositionGalleryCards();
        }

        void StepGallery(int direction)
        {
            if (!InputAllowed || !RoomGalleryCanSelect || direction == 0) return;
            _roomGalleryRotation += Mathf.Sign(direction) * GalleryCardStepDegrees;
            _roomGallerySelectionEnableAt = Time.unscaledTime + GallerySelectionRearmDelay;
            _owner.GalleryTutorial.Skip();
            UpdateGalleryTutorialAction();
            UpdateGalleryHandle(near: false, dragging: false);
            PositionGalleryCards();
        }

        internal static bool TryReadGalleryHand(IHand[] hands, int index, out Vector3 pinchPoint, out bool pinching)
        {
            pinchPoint = default;
            pinching = false;
            if (hands == null || index < 0 || index >= hands.Length) return false;
            var hand = hands[index];
            if (hand == null || hand is UnityEngine.Object handObject && !handObject) return false;
            var trackedHand = hand is HandRef handReference ? handReference.Hand : hand;
            if (trackedHand == null || trackedHand is UnityEngine.Object trackedObject && !trackedObject ||
                !trackedHand.IsConnected || !trackedHand.IsTrackedDataValid ||
                !trackedHand.GetJointPose(HandJointId.HandIndexTip, out var tip) || !Finite(tip.position)) return false;
            pinching = trackedHand.GetIndexFingerIsPinching();
            pinchPoint = pinching && trackedHand.GetJointPose(HandJointId.HandThumbTip, out var thumb) && Finite(thumb.position)
                ? (tip.position + thumb.position) * .5f : tip.position;
            return true;
        }

        void UpdateGalleryHandle(bool near, bool dragging)
        {
            if (!_roomGalleryHandleSurface || !_roomGalleryHandleLabel) return;
            var tutorialInstruction = _owner.GalleryTutorial.Instruction;
            _roomGalleryHandleSurface.color = dragging
                ? new Color(.40f, .34f, .76f, 1f)
                : near ? ClinicalPanelStyle.ChoiceHover : ClinicalPanelStyle.SurfaceSubtle;
            _roomGalleryHandleLabel.text = dragging ? "保持捏住 · 左右移动" : near ? "捏住这里 · 左右移动" : "捏住滑动";
            _roomGalleryHandleLabel.color = dragging ? ClinicalPanelStyle.ShellText : ClinicalPanelStyle.TextPrimary;
            if (_roomGalleryInstruction)
            {
                _roomGalleryInstruction.text = !string.IsNullOrEmpty(tutorialInstruction)
                    ? tutorialInstruction
                    : "轻触两侧换图 · 轻触图片进入";
                _roomGalleryInstruction.color = ClinicalPanelStyle.ShellText;
            }
            if (_roomGalleryTutorialGesture)
            {
                var cell = GalleryGestureIndex(_owner.GalleryTutorial.Step);
                var show = cell >= 0 && _roomGalleryTutorialGesture.texture;
                _roomGalleryTutorialGesture.gameObject.SetActive(show);
                if (show) _roomGalleryTutorialGesture.uvRect = GalleryGestureUv(cell);
            }
        }

        void ToggleGalleryTutorial()
        {
            if (!InputAllowed) return;
            var step = _owner.GalleryTutorial.Step;
            if (step == FullScriptGalleryTutorialStep.Completed || step == FullScriptGalleryTutorialStep.Skipped ||
                step == FullScriptGalleryTutorialStep.NotStarted)
                _owner.GalleryTutorial.Replay();
            else
                _owner.GalleryTutorial.Skip();
            UpdateGalleryTutorialAction();
            UpdateGalleryHandle(near: false, dragging: _roomGalleryDrag.IsDragging);
        }

        void UpdateGalleryTutorialAction()
        {
            if (!_roomGalleryTutorialAction) return;
            var step = _owner.GalleryTutorial.Step;
            var label = _roomGalleryTutorialAction.GetComponentInChildren<TMP_Text>();
            if (label)
                label.text = step == FullScriptGalleryTutorialStep.Completed ||
                             step == FullScriptGalleryTutorialStep.Skipped
                    ? "拖动演示"
                    : "退出演示";
        }

        internal static int GalleryGestureIndex(FullScriptGalleryTutorialStep step) => step switch
        {
            FullScriptGalleryTutorialStep.MoveToHandle => 0,
            FullScriptGalleryTutorialStep.PinchHandle => 1,
            FullScriptGalleryTutorialStep.DragRoom => 2,
            FullScriptGalleryTutorialStep.ReleaseHandle => 3,
            FullScriptGalleryTutorialStep.SelectRoom => 4,
            _ => -1
        };

        internal static Rect GalleryGestureUv(FullScriptGalleryTutorialStep step)
        {
            var index = GalleryGestureIndex(step);
            return index < 0 ? UnityEngine.Rect.zero : GalleryGestureUv(index);
        }

        static Rect GalleryGestureUv(int index)
        {
            const float cellWidth = 1f / 3f;
            const float cellHeight = 1f / 2f;
            var row = index / 3;
            return new Rect((index % 3) * cellWidth, 1f - (row + 1) * cellHeight,
                cellWidth, cellHeight);
        }

        void PositionGalleryCards()
        {
            if (!_panel || _panel.name != GalleryObjectName) return;
            var viewerLocalPosition = _panel.transform.InverseTransformPoint(_viewer.position);
            foreach (var galleryCard in _roomGalleryCards)
            {
                if (!galleryCard.Button) continue;
                var angle = 180f + galleryCard.SlotOffsetDegrees + _roomGalleryRotation;
                var radians = angle * Mathf.Deg2Rad;
                var x = Mathf.Sin(radians) * GalleryRadius / GalleryPanelScale;
                var z = Mathf.Cos(radians) * GalleryRadius / GalleryPanelScale;
                var sideAngle = Mathf.Abs(Mathf.DeltaAngle(180f, angle));
                var card = galleryCard.Button.transform;
                card.localPosition = new Vector3(x, -45, z);
                var awayFromViewer = card.localPosition - viewerLocalPosition;
                awayFromViewer.y = 0;
                card.localRotation = Quaternion.LookRotation(awayFromViewer.normalized, Vector3.up);
                var scale = Mathf.Lerp(.64f, 1f, 1f - Mathf.Clamp01(sideAngle / 82f));
                card.localScale = Vector3.one * scale;
                // Keep the current room and its immediate neighbours readable.
                // Distant cards made the gallery look crowded and could still catch a poke.
                var prominence = sideAngle <= 35f ? 1f :
                    Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((55f - sideAngle) / 20f));
                galleryCard.Group.alpha = prominence;
                galleryCard.Group.blocksRaycasts = prominence >= .65f;
                galleryCard.Group.interactable = prominence >= .65f;
                galleryCard.ShadowGroup.alpha = prominence;
                galleryCard.Shadow.localPosition = card.localPosition + card.localRotation * new Vector3(0, -12, 12);
                galleryCard.Shadow.localRotation = card.localRotation;
                galleryCard.Shadow.localScale = card.localScale;
                galleryCard.Button.interactable = prominence >= .65f &&
                    (!_owner.Session.IsFinished || _owner.Session.HasVisited(galleryCard.RoomId));
            }
        }

        static int RoomPreviewIndex(string roomId)
        {
            switch (roomId)
            {
                case "R00_LOBBY": return 0;
                case "R01_OFFICE": return 1;
                case "R02_STORAGE": return 2;
                case "R03_WAITING": return 3;
                case "R04_GI": return 4;
                case "R04_RESP": return 5;
                case "R05_REPROCESSING": return 6;
                default: return 0;
            }
        }

        static Rect PreviewUv(int index)
        {
            const float size = 1254f;
            var column = index % 3;
            var row = index / 3;
            var left = new[] { 20f, 435f, 849f }[column] / size;
            var right = new[] { 403f, 817f, 1233f }[column] / size;
            var top = new[] { 20f, 435f, 850f }[row] / size;
            var bottom = new[] { 403f, 817f, 1233f }[row] / size;
            return new Rect(left, 1f - bottom, right - left, bottom - top);
        }

        static bool Finite(Vector3 value)
            => !float.IsNaN(value.x) && !float.IsInfinity(value.x) &&
               !float.IsNaN(value.y) && !float.IsInfinity(value.y) &&
               !float.IsNaN(value.z) && !float.IsInfinity(value.z);
    }
}
