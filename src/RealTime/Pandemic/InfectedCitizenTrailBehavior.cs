// <copyright file="InfectedCitizenTrailBehavior.cs" company="dymanoid">Copyright (c) dymanoid. All rights reserved.</copyright>

namespace RealTime.Pandemic
{
    using System.Collections.Generic;
    using UnityEngine;

    /// <summary>
    /// Draws red movement trails behind infected citizens with pooled renderers and adaptive sampling.
    /// </summary>
    internal sealed class InfectedCitizenTrailBehavior : MonoBehaviour
    {
        private const int MaxTrailPoints = 30;
        private const float TrailWidth = 1.2f;
        private const float TrailElevation = 1.5f;
        private const float MaxVisibleDistance = 1000f;

        private sealed class CitizenTrail
        {
            public LineRenderer Renderer;

            public readonly List<Vector3> Points = new List<Vector3>(MaxTrailPoints);
        }

        private readonly Dictionary<ushort, CitizenTrail> trails = new Dictionary<ushort, CitizenTrail>();
        private readonly HashSet<ushort> activeIds = new HashSet<ushort>();
        private readonly List<ushort> releaseBuffer = new List<ushort>();
        private readonly Stack<LineRenderer> pooledRenderers = new Stack<LineRenderer>();
        private Material trailMaterial;
        private float nextRefreshTime;

        private void Awake()
        {
            Shader shader = Shader.Find("Particles/Additive") ?? Shader.Find("Legacy Shaders/Particles/Additive") ?? Shader.Find("Sprites/Default");
            trailMaterial = new Material(shader ?? Shader.Find("Diffuse"));
            trailMaterial.color = new Color(1f, 0.1f, 0.05f, 0.85f);
            trailMaterial.hideFlags = HideFlags.HideAndDontSave;
        }

        private void Update()
        {
            PandemicManager manager = PandemicManager.Instance;
            if (manager == null || !manager.AreWorldOverlaysEnabled())
            {
                ReleaseAllTrails();
                return;
            }

            if (Time.unscaledTime < nextRefreshTime)
            {
                return;
            }

            nextRefreshTime = Time.unscaledTime + manager.GetTrailRefreshIntervalSeconds();

            CitizenManager citizenManager = CitizenManager.instance;
            if (citizenManager == null)
            {
                return;
            }

            Camera camera = Camera.main;
            CitizenInstance[] instances = citizenManager.m_instances.m_buffer;
            activeIds.Clear();
            float minDistanceSq = GetMinDistanceSq(manager.GetPerformanceTier());

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
                Vector3 position = instances[instanceId].m_frame0.m_position;
                position.y += TrailElevation;

                if (!trails.TryGetValue(instanceId, out CitizenTrail trail))
                {
                    trail = new CitizenTrail
                    {
                        Renderer = AcquireRenderer(instanceId),
                    };
                    trails[instanceId] = trail;
                }

                List<Vector3> points = trail.Points;
                if (points.Count == 0 || (points[points.Count - 1] - position).sqrMagnitude >= minDistanceSq)
                {
                    points.Add(position);
                    if (points.Count > MaxTrailPoints)
                    {
                        points.RemoveAt(0);
                    }
                }

                LineRenderer renderer = trail.Renderer;
                if (renderer == null)
                {
                    renderer = AcquireRenderer(instanceId);
                    trail.Renderer = renderer;
                }

                bool visible = IsVisible(camera, position);
                renderer.gameObject.SetActive(visible);
                if (!visible)
                {
                    continue;
                }

                renderer.positionCount = points.Count;
                for (int i = 0; i < points.Count; i++)
                {
                    renderer.SetPosition(i, points[i]);
                }
            }

            releaseBuffer.Clear();
            foreach (ushort instanceId in trails.Keys)
            {
                if (!activeIds.Contains(instanceId))
                {
                    releaseBuffer.Add(instanceId);
                }
            }

            for (int i = 0; i < releaseBuffer.Count; i++)
            {
                ReleaseTrail(releaseBuffer[i]);
            }
        }

        private LineRenderer AcquireRenderer(ushort instanceId)
        {
            LineRenderer renderer = pooledRenderers.Count > 0 ? pooledRenderers.Pop() : CreateRenderer();
            renderer.gameObject.name = "InfectedTrail_" + instanceId;
            renderer.positionCount = 0;
            renderer.gameObject.SetActive(true);
            return renderer;
        }

        private LineRenderer CreateRenderer()
        {
            GameObject gameObject = new GameObject("InfectedTrail");
            gameObject.hideFlags = HideFlags.HideAndDontSave;
            LineRenderer renderer = gameObject.AddComponent<LineRenderer>();
            renderer.material = trailMaterial;
            renderer.startWidth = TrailWidth;
            renderer.endWidth = TrailWidth * 0.3f;
            renderer.startColor = new Color(1f, 0.1f, 0.05f, 0.9f);
            renderer.endColor = new Color(1f, 0.4f, 0.1f, 0.1f);
            renderer.useWorldSpace = true;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return renderer;
        }

        private void ReleaseTrail(ushort instanceId)
        {
            if (!trails.TryGetValue(instanceId, out CitizenTrail trail))
            {
                return;
            }

            if (trail.Renderer != null)
            {
                trail.Renderer.positionCount = 0;
                trail.Renderer.gameObject.SetActive(false);
                pooledRenderers.Push(trail.Renderer);
            }

            trail.Points.Clear();
            trails.Remove(instanceId);
        }

        private void ReleaseAllTrails()
        {
            releaseBuffer.Clear();
            foreach (ushort instanceId in trails.Keys)
            {
                releaseBuffer.Add(instanceId);
            }

            for (int i = 0; i < releaseBuffer.Count; i++)
            {
                ReleaseTrail(releaseBuffer[i]);
            }
        }

        private static float GetMinDistanceSq(PandemicPerformanceTier tier)
        {
            switch (tier)
            {
                case PandemicPerformanceTier.Heavy:
                    return 16f;
                case PandemicPerformanceTier.Extreme:
                    return 36f;
                default:
                    return 4f;
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
                && viewportPoint.x >= -0.25f
                && viewportPoint.x <= 1.25f
                && viewportPoint.y >= -0.25f
                && viewportPoint.y <= 1.25f;
        }

        private void OnDestroy()
        {
            ReleaseAllTrails();
            while (pooledRenderers.Count > 0)
            {
                LineRenderer renderer = pooledRenderers.Pop();
                if (renderer != null)
                {
                    Destroy(renderer.gameObject);
                }
            }

            if (trailMaterial != null)
            {
                Destroy(trailMaterial);
            }
        }
    }
}
