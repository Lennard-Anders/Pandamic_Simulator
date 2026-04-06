// <copyright file="QuarantineBuildingIconBehavior.cs" company="dymanoid">Copyright (c) dymanoid. All rights reserved.</copyright>

namespace RealTime.Pandemic
{
    using System.Collections.Generic;
    using UnityEngine;

    /// <summary>
    /// Shows floating pandemic icons above infected residential buildings and highlighted hubs.
    /// </summary>
    internal sealed class QuarantineBuildingIconBehavior : MonoBehaviour
    {
        private const float IconHeight = 30f;
        private const float BaseIconCharSize = 1.2f;
        private const int IconFontSize = 80;
        private const float MaxVisibleDistance = 1800f;
        private static readonly Color InfectedForegroundColor = new Color(1f, 0.84f, 0.18f, 1f);
        private static readonly Color InfectedOutlineColor = new Color(0.18f, 0.12f, 0.06f, 0.96f);
        private static readonly Color HubForegroundColor = new Color(1f, 0.34f, 0.16f, 1f);
        private static readonly Color HubOutlineColor = new Color(0.18f, 0.08f, 0.08f, 0.96f);
        private static readonly Color ShadowColor = new Color(0f, 0f, 0f, 0.52f);

        private readonly Dictionary<ushort, GameObject> iconObjects = new Dictionary<ushort, GameObject>();
        private readonly HashSet<ushort> visibleBuildings = new HashSet<ushort>();
        private readonly List<ushort> releaseBuffer = new List<ushort>();
        private readonly Stack<GameObject> pooledIcons = new Stack<GameObject>();

        private Font iconFont;
        private Material iconMaterial;
        private float nextRefreshTime;

        private void Awake()
        {
            iconFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
            iconMaterial = WorldGlyphIconFactory.CreateUnlitTextMaterial(iconFont);
        }

        private void LateUpdate()
        {
            PandemicManager manager = PandemicManager.Instance;
            if (manager == null || !manager.AreWorldOverlaysEnabled())
            {
                ReleaseAll();
                return;
            }

            if (Time.unscaledTime < nextRefreshTime)
            {
                return;
            }

            nextRefreshTime = Time.unscaledTime + manager.GetOverlayRefreshIntervalSeconds();

            Building[] buildingBuffer = BuildingManager.instance?.m_buildings?.m_buffer;
            if (buildingBuffer == null)
            {
                return;
            }

            Camera camera = Camera.main;
            visibleBuildings.Clear();
            UpdateIcons(manager.InfectedBuildingIds, buildingBuffer, camera, manager, isHub: false);
            UpdateIcons(manager.HubBuildingIds, buildingBuffer, camera, manager, isHub: true);

            releaseBuffer.Clear();
            foreach (KeyValuePair<ushort, GameObject> entry in iconObjects)
            {
                if (!visibleBuildings.Contains(entry.Key))
                {
                    releaseBuffer.Add(entry.Key);
                }
            }

            for (int i = 0; i < releaseBuffer.Count; i++)
            {
                ReleaseIcon(releaseBuffer[i]);
            }
        }

        private void UpdateIcons(IEnumerable<ushort> buildingIds, Building[] buildingBuffer, Camera camera, PandemicManager manager, bool isHub)
        {
            if (buildingIds == null)
            {
                return;
            }

            foreach (ushort buildingId in buildingIds)
            {
                if (buildingId == 0 || buildingId >= buildingBuffer.Length)
                {
                    continue;
                }

                int infectedCount = isHub
                    ? manager.GetCurrentInfectedCountAtBuilding(buildingId)
                    : manager.GetInfectedCountInBuilding(buildingId);
                if (infectedCount <= 0 || (buildingBuffer[buildingId].m_flags & Building.Flags.Created) == 0)
                {
                    continue;
                }

                Vector3 position = buildingBuffer[buildingId].m_position;
                position.y += IconHeight;
                if (!IsVisible(camera, position))
                {
                    continue;
                }

                visibleBuildings.Add(buildingId);
                if (!iconObjects.TryGetValue(buildingId, out GameObject icon) || icon == null)
                {
                    icon = AcquireIcon(buildingId);
                    iconObjects[buildingId] = icon;
                }

                WorldGlyphIcon glyphIcon = icon.GetComponent<WorldGlyphIcon>();
                if (glyphIcon != null)
                {
                    float scale = Mathf.Clamp(1f + ((infectedCount - 1) * 0.25f), 1f, 2.5f);
                    WorldGlyphIconFactory.ApplyStyle(
                        glyphIcon,
                        isHub ? "\u26A0" : "\u2623",
                        BaseIconCharSize * scale,
                        isHub ? HubForegroundColor : InfectedForegroundColor,
                        isHub ? HubOutlineColor : InfectedOutlineColor,
                        ShadowColor);
                }

                icon.transform.position = position;
                icon.SetActive(true);
                Billboard(icon.transform, camera);
            }
        }

        private GameObject AcquireIcon(ushort buildingId)
        {
            GameObject icon = pooledIcons.Count > 0 ? pooledIcons.Pop() : CreateIcon();
            icon.name = "PandemicBuildingIcon_" + buildingId;
            icon.SetActive(true);
            return icon;
        }

        private GameObject CreateIcon()
        {
            GameObject icon = WorldGlyphIconFactory.CreateIconRoot("PandemicBuildingIcon", iconFont, iconMaterial, IconFontSize);
            WorldGlyphIcon glyphIcon = icon.GetComponent<WorldGlyphIcon>();
            if (glyphIcon != null)
            {
                WorldGlyphIconFactory.ApplyStyle(glyphIcon, "\u2623", BaseIconCharSize, InfectedForegroundColor, InfectedOutlineColor, ShadowColor);
            }

            return icon;
        }

        private void ReleaseIcon(ushort buildingId)
        {
            if (!iconObjects.TryGetValue(buildingId, out GameObject icon) || icon == null)
            {
                iconObjects.Remove(buildingId);
                return;
            }

            icon.SetActive(false);
            pooledIcons.Push(icon);
            iconObjects.Remove(buildingId);
        }

        private void ReleaseAll()
        {
            releaseBuffer.Clear();
            foreach (ushort buildingId in iconObjects.Keys)
            {
                releaseBuffer.Add(buildingId);
            }

            for (int i = 0; i < releaseBuffer.Count; i++)
            {
                ReleaseIcon(releaseBuffer[i]);
            }
        }

        private static bool IsVisible(Camera camera, Vector3 position)
        {
            if (camera == null)
            {
                return true;
            }

            if ((camera.transform.position - position).sqrMagnitude > MaxVisibleDistance * MaxVisibleDistance)
            {
                return false;
            }

            Vector3 viewportPoint = camera.WorldToViewportPoint(position);
            return viewportPoint.z > 0f
                && viewportPoint.x >= -0.2f
                && viewportPoint.x <= 1.2f
                && viewportPoint.y >= -0.2f
                && viewportPoint.y <= 1.2f;
        }

        private static void Billboard(Transform transform, Camera camera)
        {
            if (camera == null)
            {
                return;
            }

            transform.LookAt(camera.transform.position);
            transform.Rotate(0f, 180f, 0f);
        }

        private void OnDestroy()
        {
            foreach (GameObject icon in iconObjects.Values)
            {
                if (icon != null)
                {
                    Destroy(icon);
                }
            }

            while (pooledIcons.Count > 0)
            {
                GameObject icon = pooledIcons.Pop();
                if (icon != null)
                {
                    Destroy(icon);
                }
            }

            if (iconMaterial != null)
            {
                Destroy(iconMaterial);
            }
        }
    }
}
