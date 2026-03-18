// <copyright file="InfectedCitizenTrailBehavior.cs" company="dymanoid">Copyright (c) dymanoid. All rights reserved.</copyright>

namespace RealTime.Pandemic
{
    using System.Collections.Generic;
    using UnityEngine;

    /// <summary>
    /// A MonoBehaviour that draws a red movement trail behind each infected citizen instance
    /// using Unity LineRenderer components, which are visible at all camera angles.
    /// </summary>
    internal sealed class InfectedCitizenTrailBehavior : MonoBehaviour
    {
        private const int MaxTrailPoints = 30;
        private const float MinDistanceSq = 4f;    // record a new point every ~2 world units
        private const float TrailWidth = 1.2f;
        private const float TrailElevation = 1.5f; // lift above ground so it isn't buried

        private class CitizenTrail
        {
            public LineRenderer Renderer;
            public readonly List<Vector3> Points = new List<Vector3>();
        }

        private readonly Dictionary<ushort, CitizenTrail> trails = new Dictionary<ushort, CitizenTrail>();
        private readonly List<ushort> removalBuffer = new List<ushort>();
        private Material trailMaterial;

        private void Awake()
        {
            var shader = Shader.Find("Particles/Additive") ?? Shader.Find("Legacy Shaders/Particles/Additive") ?? Shader.Find("Sprites/Default");
            trailMaterial = new Material(shader ?? Shader.Find("Diffuse"));
            trailMaterial.color = new Color(1f, 0.1f, 0.05f, 0.85f);
            trailMaterial.hideFlags = HideFlags.HideAndDontSave;
        }

        private void Update()
        {
            var manager = PandemicManager.Instance;
            if (manager == null)
            {
                ClearAllTrails();
                return;
            }

            var citizenManager = CitizenManager.instance;
            if (citizenManager == null)
            {
                return;
            }

            var instances = citizenManager.m_instances.m_buffer;

            // Collect currently active infected instance IDs into a set for fast lookup
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
                pos.y += TrailElevation;

                if (!trails.TryGetValue(instanceId, out var trail))
                {
                    trail = CreateTrail(instanceId);
                    trails[instanceId] = trail;
                }

                var pts = trail.Points;
                if (pts.Count == 0 || (pts[pts.Count - 1] - pos).sqrMagnitude > MinDistanceSq)
                {
                    pts.Add(pos);
                    if (pts.Count > MaxTrailPoints)
                    {
                        pts.RemoveAt(0);
                    }
                }

                trail.Renderer.positionCount = pts.Count;
                trail.Renderer.SetPositions(pts.ToArray());
            }

            // Remove trails for citizens who are no longer infected / visible
            removalBuffer.Clear();
            foreach (var kvp in trails)
            {
                if (!activeIds.Contains(kvp.Key))
                {
                    removalBuffer.Add(kvp.Key);
                }
            }

            foreach (ushort id in removalBuffer)
            {
                if (trails.TryGetValue(id, out var t) && t.Renderer != null)
                {
                    Destroy(t.Renderer.gameObject);
                }

                trails.Remove(id);
            }
        }

        private CitizenTrail CreateTrail(ushort instanceId)
        {
            var go = new GameObject("InfectedTrail_" + instanceId);
            go.hideFlags = HideFlags.HideAndDontSave;
            var lr = go.AddComponent<LineRenderer>();
            lr.material = trailMaterial;
            lr.startWidth = TrailWidth;
            lr.endWidth = TrailWidth * 0.3f;
            lr.startColor = new Color(1f, 0.1f, 0.05f, 0.9f);
            lr.endColor = new Color(1f, 0.4f, 0.1f, 0.1f);
            lr.useWorldSpace = true;
            lr.positionCount = 0;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
            return new CitizenTrail { Renderer = lr };
        }

        private void ClearAllTrails()
        {
            foreach (var kvp in trails)
            {
                if (kvp.Value.Renderer != null)
                {
                    Destroy(kvp.Value.Renderer.gameObject);
                }
            }

            trails.Clear();
        }

        private void OnDestroy()
        {
            ClearAllTrails();
            if (trailMaterial != null)
            {
                Destroy(trailMaterial);
            }
        }
    }
}
