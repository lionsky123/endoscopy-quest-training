using System;
using System.Collections.Generic;
using BotanicalGardenQR.MapNavigation.Contracts;
using UnityEngine;
using TMPro;

namespace BotanicalGardenQR.MapNavigation.Frontend
{
    public interface IMapRoutePresentation : IDisposable
    {
        void Present(IReadOnlyList<MapPosition> path, bool routeVisible, bool targetVisible, string targetTitle);
    }

    /// <summary>Owns only route visuals. Positions are immutable world samples from the map session.</summary>
    public static class MapRoutePresentationFactory
    {
        public static IMapRoutePresentation Create(Transform parent, Material material, TMP_FontAsset font, Transform viewer = null) => new RoutePresentation(parent, material, font, viewer);
        sealed class RoutePresentation : IMapRoutePresentation
        {
            readonly GameObject _root;
            readonly LineRenderer _line, _target, _beam;
            readonly TextMeshPro _label;
            readonly Transform _viewer;
            IReadOnlyList<MapPosition> _path;
            public RoutePresentation(Transform parent, Material material, TMP_FontAsset font, Transform viewer)
            {
                if (material == null)
                    throw new ArgumentNullException(nameof(material));
                if (font == null) throw new ArgumentNullException(nameof(font));
                _root = new GameObject("MapRoutePresentation");
                _root.transform.SetParent(parent, false);
                _line = CreateLine("CurrentLeg", material);
                _target = CreateLine("Destination", material);
                _target.loop = true;
                _beam = CreateLine("DestinationBeacon", material);
                _beam.widthMultiplier = .07f;
                _viewer = viewer;
                var labelObject = new GameObject("DestinationTitle");
                labelObject.transform.SetParent(_root.transform, false);
                _label = labelObject.AddComponent<TextMeshPro>();
                _label.font = font;
                _label.fontSize = 16;
                _label.alignment = TextAlignmentOptions.Center;
                _label.rectTransform.sizeDelta = new Vector2(22, 4);
                _label.transform.localScale = Vector3.one * .1f;
                _label.color = new Color(.85f, 1f, .9f);
                _label.richText = false;
                _label.raycastTarget = false;
                _root.SetActive(false);
            }

            LineRenderer CreateLine(string name, Material material)
            {
                var child = new GameObject(name);
                child.transform.SetParent(_root.transform, false);
                var line = child.AddComponent<LineRenderer>();
                line.sharedMaterial = material;
                line.useWorldSpace = true;
                line.widthMultiplier = 0.035f;
                line.numCornerVertices = 2;
                line.numCapVertices = 2;
                line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                line.receiveShadows = false;
                return line;
            }

            public void Present(IReadOnlyList<MapPosition> path, bool routeVisible, bool targetVisible, string targetTitle)
            {
                if (_root == null)
                    return;
                var valid = path != null && path.Count > 1;
                _root.SetActive(valid && (routeVisible || targetVisible));
                _line.gameObject.SetActive(routeVisible);
                _target.gameObject.SetActive(targetVisible);
                _beam.gameObject.SetActive(targetVisible);
                _label.gameObject.SetActive(targetVisible);
                _label.text = targetTitle ?? string.Empty;
                FaceViewer();
                if (ReferenceEquals(_path, path) || path == null || path.Count < 2)
                    return;
                _path = path;
                var points = new Vector3[path.Count];
                for (int i = 0; i < path.Count; i++)
                    points[i] = new Vector3(path[i].x, path[i].y + 0.08f, path[i].z);
                _line.positionCount = points.Length;
                _line.SetPositions(points);
                var end = points[points.Length - 1];
                _beam.positionCount = 2;
                _beam.SetPositions(new[] { end, end + Vector3.up * 1.5f });
                _label.transform.position = end + Vector3.up * 1.7f;
                var circle = new Vector3[32];
                for (int i = 0; i < circle.Length; i++)
                {
                    var a = i * Mathf.PI * 2 / circle.Length;
                    circle[i] = end + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * 0.35f;
                }

                _target.positionCount = circle.Length;
                _target.SetPositions(circle);
                FaceViewer();
            }

            void FaceViewer()
            {
                if (_viewer == null) return;
                var direction = _label.transform.position - _viewer.position;
                if (direction.sqrMagnitude > .001f)
                    _label.transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
            }

            public void Dispose()
            {
                if (_root == null)
                    return;
                if (Application.isPlaying)
                    UnityEngine.Object.Destroy(_root);
                else
                    UnityEngine.Object.DestroyImmediate(_root);
            }
        }
    }
}
