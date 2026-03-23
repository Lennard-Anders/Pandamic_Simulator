// <copyright file="QuarantineBuildingIconBehavior.cs" company="dymanoid">Copyright (c) dymanoid. All rights reserved.</copyright>

namespace RealTime.Pandemic
{
    using System.Collections.Generic;
    using UnityEngine;

    /// <summary>
    /// Shows floating pandemic icons above infected residential buildings and highlighted hubs.
    /// Residential icons scale with infected residents; hubs use a separate warning symbol.
    /// </summary>
    internal sealed class QuarantineBuildingIconBehavior : MonoBehaviour
    {
        private const float IconHeight = 30f;
        private const float BaseIconCharSize = 1.2f;
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
            if (manager == null || !manager.AreWorldOverlaysEnabled())
            {
                ClearAll();
                return;
            }

            var infectedHomes = manager.InfectedBuildingIds;
            var hubs = manager.HubBuildingIds;
            if ((infectedHomes == null || infectedHomes.Count == 0) && (hubs == null || hubs.Count == 0))
            {
                ClearAll();
                return;
            }

            var buildingBuffer = BuildingManager.instance?.m_buildings?.m_buffer;
            if (buildingBuffer == null)
            {
                return;
            }

            var camera = Camera.main;
            var activeBuildings = new HashSet<ushort>();

            if (infectedHomes != null)
            {
                foreach (ushort buildingId in infectedHomes)
                {
                    UpdateBuildingIcon(
                        buildingId,
                        infectedCount: manager.GetInfectedCountInBuilding(buildingId),
                        isHub: false,
                        buildingBuffer: buildingBuffer,
                        camera: camera,
                        activeBuildings: activeBuildings);
                }
            }

            if (hubs != null)
            {
                foreach (ushort buildingId in hubs)
                {
                    UpdateBuildingIcon(
                        buildingId,
                        infectedCount: manager.GetCurrentInfectedCountAtBuilding(buildingId),
                        isHub: true,
                        buildingBuffer: buildingBuffer,
                        camera: camera,
                        activeBuildings: activeBuildings);
                }
            }

            removalBuffer.Clear();
            foreach (var kvp in iconObjects)
            {
                if (!activeBuildings.Contains(kvp.Key))
                {
                    removalBuffer.Add(kvp.Key);
                }
            }

            foreach (ushort buildingId in removalBuffer)
            {
                if (iconObjects.TryGetValue(buildingId, out GameObject go) && go != null)
                {
                    Destroy(go);
                }

                iconObjects.Remove(buildingId);
            }
        }

        private void UpdateBuildingIcon(
            ushort buildingId,
            int infectedCount,
            bool isHub,
            Building[] buildingBuffer,
            Camera camera,
            HashSet<ushort> activeBuildings)
        {
            if (buildingId == 0 || infectedCount <= 0 || buildingId >= buildingBuffer.Length)
            {
                return;
            }

            if ((buildingBuffer[buildingId].m_flags & Building.Flags.Created) == 0)
            {
                return;
            }

            activeBuildings.Add(buildingId);

            if (!iconObjects.TryGetValue(buildingId, out GameObject go) || go == null)
            {
                go = CreateIcon(buildingId);
                iconObjects[buildingId] = go;
            }

            TextMesh textMesh = go.GetComponent<TextMesh>();
            if (textMesh != null)
            {
                float scale = Mathf.Clamp(1f + ((infectedCount - 1) * 0.25f), 1f, 2.5f);
                textMesh.text = isHub ? "\u26A0" : "\u2623";
                textMesh.characterSize = BaseIconCharSize * scale;
                textMesh.color = isHub
                    ? new Color(1f, 0.2f, 0.2f, 1f)
                    : new Color(1f, 0.9f, 0f, 1f);
            }

            Vector3 position = buildingBuffer[buildingId].m_position;
            position.y += IconHeight;
            go.transform.position = position;

            if (camera != null)
            {
                go.transform.LookAt(camera.transform.position);
                go.transform.Rotate(0f, 180f, 0f);
            }
        }

        private GameObject CreateIcon(ushort buildingId)
        {
            var go = new GameObject("PandemicBuildingIcon_" + buildingId);
            go.hideFlags = HideFlags.HideAndDontSave;

            var textMesh = go.AddComponent<TextMesh>();
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

            var meshRenderer = go.GetComponent<MeshRenderer>();
            if (meshRenderer != null)
            {
                meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                meshRenderer.receiveShadows = false;
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
