// <copyright file="InfectedCitizenIconBehavior.cs" company="dymanoid">Copyright (c) dymanoid. All rights reserved.</copyright>

namespace RealTime.Pandemic
{
    using System.Collections.Generic;
    using UnityEngine;

    /// <summary>
    /// Shows floating biohazard icons above infected citizens with pooled world objects.
    /// </summary>
    internal sealed class InfectedCitizenIconBehavior : MonoBehaviour
    {
        private const float HeadHeight = 7f;
        private const float IconCharSize = 0.56f;
        private const int IconFontSize = 60;
        private const float MaxVisibleDistance = 900f;
        private static readonly Color ForegroundColor = new Color(1f, 0.82f, 0.18f, 1f);
        private static readonly Color OutlineColor = new Color(0.16f, 0.12f, 0.06f, 0.96f);
        private static readonly Color ShadowColor = new Color(0f, 0f, 0f, 0.48f);

        private readonly Dictionary<ushort, GameObject> iconObjects = new Dictionary<ushort, GameObject>();
        private readonly HashSet<ushort> visibleIds = new HashSet<ushort>();
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

            CitizenManager citizenManager = CitizenManager.instance;
            if (citizenManager == null)
            {
                return;
            }

            Camera camera = Camera.main;
            CitizenInstance[] instances = citizenManager.m_instances.m_buffer;
            visibleIds.Clear();

            foreach (ushort instanceId in manager.GetInfectedCitizenInstanceIds())
            {
                if (instanceId == 0 || instanceId >= instances.Length)
                {
                    continue;
                }

                if ((instances[instanceId].m_flags & CitizenInstance.Flags.Created) == 0)
                {
                    continue;
                }

                Vector3 position = instances[instanceId].m_frame0.m_position;
                position.y += HeadHeight;
                if (!IsVisible(camera, position))
                {
                    continue;
                }

                visibleIds.Add(instanceId);
                if (!iconObjects.TryGetValue(instanceId, out GameObject icon) || icon == null)
                {
                    icon = AcquireIcon(instanceId);
                    iconObjects[instanceId] = icon;
                }

                icon.transform.position = position;
                icon.SetActive(true);
                Billboard(icon.transform, camera);
            }

            releaseBuffer.Clear();
            foreach (KeyValuePair<ushort, GameObject> entry in iconObjects)
            {
                if (!visibleIds.Contains(entry.Key))
                {
                    releaseBuffer.Add(entry.Key);
                }
            }

            for (int i = 0; i < releaseBuffer.Count; i++)
            {
                ReleaseIcon(releaseBuffer[i]);
            }
        }

        private GameObject AcquireIcon(ushort instanceId)
        {
            GameObject icon = pooledIcons.Count > 0 ? pooledIcons.Pop() : CreateIcon();
            icon.name = "InfectedIcon_" + instanceId;
            icon.SetActive(true);
            return icon;
        }

        private GameObject CreateIcon()
        {
            GameObject icon = WorldGlyphIconFactory.CreateIconRoot("InfectedIcon", iconFont, iconMaterial, IconFontSize);
            WorldGlyphIcon glyphIcon = icon.GetComponent<WorldGlyphIcon>();
            if (glyphIcon != null)
            {
                WorldGlyphIconFactory.ApplyStyle(glyphIcon, "\u2623", IconCharSize, ForegroundColor, OutlineColor, ShadowColor);
            }

            return icon;
        }

        private void ReleaseIcon(ushort instanceId)
        {
            if (!iconObjects.TryGetValue(instanceId, out GameObject icon) || icon == null)
            {
                iconObjects.Remove(instanceId);
                return;
            }

            icon.SetActive(false);
            pooledIcons.Push(icon);
            iconObjects.Remove(instanceId);
        }

        private void ReleaseAll()
        {
            releaseBuffer.Clear();
            foreach (ushort instanceId in iconObjects.Keys)
            {
                releaseBuffer.Add(instanceId);
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
                && viewportPoint.x >= -0.15f
                && viewportPoint.x <= 1.15f
                && viewportPoint.y >= -0.15f
                && viewportPoint.y <= 1.15f;
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
