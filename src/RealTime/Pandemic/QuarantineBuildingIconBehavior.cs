// <copyright file="QuarantineBuildingIconBehavior.cs" company="dymanoid">Copyright (c) dymanoid. All rights reserved.</copyright>

namespace RealTime.Pandemic
{
    using System.Collections.Generic;
    using UnityEngine;

    /// <summary>
    /// A MonoBehaviour that shows a floating quarantine icon above residential buildings
    /// that have more than one infected resident (hotspots).
    /// Uses a TextMesh billboard so it always faces the camera.
    /// </summary>
    internal sealed class QuarantineBuildingIconBehavior : MonoBehaviour
    {
        // Height in world units above the building position
        private const float IconHeight = 30f;
        private const float IconCharSize = 1.2f;
        private const int IconFontSize = 80;

        private readonly Dictionary<ushort, GameObject> iconObjects = new Dictionary<ushort, GameObject>();
        private readonly List<ushort> removalBuffer = new List<ushort>();

        private Font iconFont;

        private void Awake()
        {
            iconFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
        }

        private void LateUpdate()
        {
            var manager = PandemicManager.Instance;
            if (manager == null)
            {
                ClearAll();
                return;
            }

            var hotspots = manager.HotspotBuildingIds;
            if (hotspots == null || hotspots.Count == 0)
            {
                ClearAll();
                return;
            }

            var buildingBuffer = BuildingManager.instance?.m_buildings?.m_buffer;
            if (buildingBuffer == null)
            {
                return;
            }

            var cam = Camera.main;

            foreach (ushort buildingId in hotspots)
            {
                if (buildingId == 0 || buildingId >= buildingBuffer.Length)
                {
                    continue;
                }

                if ((buildingBuffer[buildingId].m_flags & Building.Flags.Created) == 0)
                {
                    continue;
                }

                Vector3 pos = buildingBuffer[buildingId].m_position;
                pos.y += IconHeight;

                if (!iconObjects.TryGetValue(buildingId, out var go) || go == null)
                {
                    go = CreateIcon(buildingId);
                    iconObjects[buildingId] = go;
                }

                go.transform.position = pos;

                if (cam != null)
                {
                    go.transform.LookAt(cam.transform.position);
                    go.transform.Rotate(0f, 180f, 0f);
                }
            }

            // Remove icons for buildings that are no longer hotspots
            removalBuffer.Clear();
            foreach (var kvp in iconObjects)
            {
                if (!hotspots.Contains(kvp.Key))
                {
                    removalBuffer.Add(kvp.Key);
                }
            }

            foreach (ushort id in removalBuffer)
            {
                if (iconObjects.TryGetValue(id, out var go) && go != null)
                {
                    Destroy(go);
                }

                iconObjects.Remove(id);
            }
        }

        private GameObject CreateIcon(ushort buildingId)
        {
            var go = new GameObject("QuarantineIcon_" + buildingId);
            go.hideFlags = HideFlags.HideAndDontSave;

            var tm = go.AddComponent<TextMesh>();
            // ☢ radioactive/hazard mark — visually distinct from citizen ☣ (biohazard, yellow)
            tm.text = "\u2622";
            tm.fontSize = IconFontSize;
            tm.characterSize = IconCharSize;
            tm.color = new Color(1f, 0f, 0f, 1f); // solid red
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;

            if (iconFont != null)
            {
                tm.font = iconFont;
            }

            var mr = go.GetComponent<MeshRenderer>();
            if (mr != null)
            {
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mr.receiveShadows = false;
            }

            return go;
        }

        private void ClearAll()
        {
            foreach (var kvp in iconObjects)
            {
                if (kvp.Value != null)
                {
                    Destroy(kvp.Value);
                }
            }

            iconObjects.Clear();
        }

        private void OnDestroy()
        {
            ClearAll();
        }
    }
}
