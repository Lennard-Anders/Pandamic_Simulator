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

        private readonly Dictionary<ushort, GameObject> iconObjects = new Dictionary<ushort, GameObject>();
        private readonly HashSet<ushort> visibleBuildings = new HashSet<ushort>();
        private readonly List<ushort> releaseBuffer = new List<ushort>();
        private readonly Stack<GameObject> pooledIcons = new Stack<GameObject>();

        private Font iconFont;
        private float nextRefreshTime;

        private void Awake()
        {
            iconFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
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

                TextMesh textMesh = icon.GetComponent<TextMesh>();
                if (textMesh != null)
                {
                    float scale = Mathf.Clamp(1f + ((infectedCount - 1) * 0.25f), 1f, 2.5f);
                    textMesh.text = isHub ? "\u26A0" : "\u2623";
                    textMesh.characterSize = BaseIconCharSize * scale;
                    textMesh.color = isHub ? new Color(1f, 0.2f, 0.2f, 1f) : new Color(1f, 0.9f, 0f, 1f);
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
            var go = new GameObject("PandemicBuildingIcon");
            go.hideFlags = HideFlags.HideAndDontSave;

            TextMesh textMesh = go.AddComponent<TextMesh>();
            textMesh.text = "\u2623";
            textMesh.fontSize = IconFontSize;
            textMesh.characterSize = BaseIconCharSize;
            textMesh.color = new Color(1f, 0.9f, 0f, 1f);
            textMesh.anchor = TextAnchor.MiddleCenter;
            textMesh.alignment = TextAlignment.Center;

            if (iconFont != null)
            {
                textMesh.font = iconFont;
            }

            MeshRenderer meshRenderer = go.GetComponent<MeshRenderer>();
            if (meshRenderer != null)
            {
                meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                meshRenderer.receiveShadows = false;
            }

            return go;
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
        }
    }
}
