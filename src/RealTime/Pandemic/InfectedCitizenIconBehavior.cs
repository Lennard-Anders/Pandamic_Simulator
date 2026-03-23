// <copyright file="InfectedCitizenIconBehavior.cs" company="dymanoid">Copyright (c) dymanoid. All rights reserved.</copyright>

namespace RealTime.Pandemic
{
    using System.Collections.Generic;
    using UnityEngine;

    /// <summary>
    /// A MonoBehaviour that shows a floating biohazard icon above each infected citizen.
    /// Uses a TextMesh billboard so it always faces the camera and is visible at all angles.
    /// </summary>
    internal sealed class InfectedCitizenIconBehavior : MonoBehaviour
    {
        private const float HeadHeight = 7f;    // world units above ground
        private const float IconCharSize = 0.5f;
        private const int IconFontSize = 60;

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

            var citizenManager = CitizenManager.instance;
            if (citizenManager == null)
            {
                return;
            }

            var instances = citizenManager.m_instances.m_buffer;
            var activeIds = new HashSet<ushort>();

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

                activeIds.Add(instanceId);

                Vector3 pos = instances[instanceId].m_frame0.m_position;
                pos.y += HeadHeight;

                if (!iconObjects.TryGetValue(instanceId, out var go) || go == null)
                {
                    go = CreateIcon(instanceId);
                    iconObjects[instanceId] = go;
                }

                go.transform.position = pos;

                // Billboard: rotate to always face the camera
                var cam = Camera.main;
                if (cam != null)
                {
                    go.transform.LookAt(cam.transform.position);
                    go.transform.Rotate(0f, 180f, 0f);
                }
            }

            removalBuffer.Clear();
            foreach (var kvp in iconObjects)
            {
                if (!activeIds.Contains(kvp.Key))
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

        private GameObject CreateIcon(ushort instanceId)
        {
            var go = new GameObject("InfectedIcon_" + instanceId);
            go.hideFlags = HideFlags.HideAndDontSave;

            var tm = go.AddComponent<TextMesh>();
            tm.text = "\u2623"; // ☣ biohazard
            tm.fontSize = IconFontSize;
            tm.characterSize = IconCharSize;
            tm.color = new Color(1f, 0.9f, 0f, 1f); // bright yellow
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
