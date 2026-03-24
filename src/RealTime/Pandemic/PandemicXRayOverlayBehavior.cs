// <copyright file="PandemicXRayOverlayBehavior.cs" company="dymanoid">Copyright (c) dymanoid. All rights reserved.</copyright>

namespace RealTime.Pandemic
{
    using UnityEngine;
    using UnityEngine.Rendering;

    /// <summary>
    /// Draws a city-wide X-Ray heatmap of infected citizens on a terrain-following mesh.
    /// </summary>
    internal sealed class PandemicXRayOverlayBehavior : MonoBehaviour
    {
        private const int Resolution = 128;
        private const int VertexResolution = Resolution + 1;
        private const float MapSize = 17280f;
        private const float MapHalfSize = MapSize * 0.5f;
        private const float OverlayHeightOffset = 8f;
        private const float RefreshInterval = 1f;
        private const float MinVisibleIntensity = 0.001f;

        private readonly float[] sourceGrid = new float[Resolution * Resolution];
        private readonly float[] smoothedGrid = new float[Resolution * Resolution];
        private readonly Color32[] vertexColors = new Color32[VertexResolution * VertexResolution];

        private GameObject overlayObject;
        private MeshFilter overlayFilter;
        private MeshRenderer overlayRenderer;
        private Mesh overlayMesh;
        private Material material;
        private float nextRefreshTime;
        private bool loggedMissingOverlayWarning;
        private bool loggedEmptyHeatmapWarning;
        private bool loggedTerrainFallbackWarning;

        private void Awake()
        {
            CreateOverlayMaterial();
            CreateOverlayObject();
            BuildOverlayMesh();
        }

        private void LateUpdate()
        {
            PandemicManager manager = PandemicManager.Instance;
            if (manager == null)
            {
                SetVisible(false);
                return;
            }

            PandemicXRayMode mode = manager.GetXRayMode();
            if (mode == PandemicXRayMode.Off)
            {
                loggedEmptyHeatmapWarning = false;
                SetVisible(false);
                return;
            }

            if (overlayRenderer == null || overlayMesh == null)
            {
                LogMissingOverlayWarningOnce();
                SetVisible(false);
                return;
            }

            if (Time.unscaledTime < nextRefreshTime)
            {
                return;
            }

            nextRefreshTime = Time.unscaledTime + RefreshInterval;

            int writtenCells = manager.PopulateHeatmapGrid(sourceGrid, mode);
            if (writtenCells == 0)
            {
                LogEmptyHeatmapWarningOnce(mode);
                SetVisible(false);
                return;
            }

            loggedEmptyHeatmapWarning = false;
            if (!UpdateVertexColors())
            {
                SetVisible(false);
                return;
            }

            SetVisible(true);
        }

        private void CreateOverlayMaterial()
        {
            Shader shader = Shader.Find("Sprites/Default")
                ?? Shader.Find("Unlit/Transparent")
                ?? Shader.Find("Legacy Shaders/Transparent/VertexLit")
                ?? Shader.Find("Particles/Alpha Blended")
                ?? Shader.Find("Diffuse");

            material = new Material(shader)
            {
                hideFlags = HideFlags.HideAndDontSave,
                renderQueue = 3100,
            };

            if (material.HasProperty("_MainTex"))
            {
                material.mainTexture = Texture2D.whiteTexture;
            }

            if (material.HasProperty("_Color"))
            {
                material.SetColor("_Color", Color.white);
            }

            if (material.HasProperty("_TintColor"))
            {
                material.SetColor("_TintColor", Color.white);
            }

            TryConfigureMaterialForOverlay(material);
        }

        private void CreateOverlayObject()
        {
            overlayObject = new GameObject("PandemicXRayOverlay")
            {
                hideFlags = HideFlags.HideAndDontSave,
            };
            overlayObject.transform.SetParent(transform, worldPositionStays: false);
            overlayObject.transform.localPosition = Vector3.zero;
            overlayObject.transform.localRotation = Quaternion.identity;
            overlayObject.transform.localScale = Vector3.one;

            overlayFilter = overlayObject.AddComponent<MeshFilter>();
            overlayRenderer = overlayObject.AddComponent<MeshRenderer>();
            overlayRenderer.material = material;
            overlayRenderer.shadowCastingMode = ShadowCastingMode.Off;
            overlayRenderer.receiveShadows = false;
            overlayRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            overlayRenderer.lightProbeUsage = LightProbeUsage.Off;
            overlayRenderer.enabled = false;
        }

        private void BuildOverlayMesh()
        {
            Vector3[] vertices = new Vector3[VertexResolution * VertexResolution];
            Vector2[] uv = new Vector2[VertexResolution * VertexResolution];
            int[] triangles = new int[Resolution * Resolution * 6];
            float cellSize = MapSize / Resolution;

            for (int z = 0; z < VertexResolution; z++)
            {
                float worldZ = -MapHalfSize + (z * cellSize);
                for (int x = 0; x < VertexResolution; x++)
                {
                    float worldX = -MapHalfSize + (x * cellSize);
                    int vertexIndex = (z * VertexResolution) + x;
                    vertices[vertexIndex] = new Vector3(worldX, SampleOverlayHeight(worldX, worldZ), worldZ);
                    uv[vertexIndex] = new Vector2((float)x / Resolution, (float)z / Resolution);
                    vertexColors[vertexIndex] = new Color32(0, 0, 0, 0);
                }
            }

            int triangleIndex = 0;
            for (int z = 0; z < Resolution; z++)
            {
                int rowStart = z * VertexResolution;
                int nextRowStart = (z + 1) * VertexResolution;
                for (int x = 0; x < Resolution; x++)
                {
                    int v00 = rowStart + x;
                    int v10 = v00 + 1;
                    int v01 = nextRowStart + x;
                    int v11 = v01 + 1;

                    triangles[triangleIndex++] = v00;
                    triangles[triangleIndex++] = v01;
                    triangles[triangleIndex++] = v10;
                    triangles[triangleIndex++] = v10;
                    triangles[triangleIndex++] = v01;
                    triangles[triangleIndex++] = v11;
                }
            }

            overlayMesh = new Mesh
            {
                name = "PandemicXRayOverlayMesh",
                hideFlags = HideFlags.HideAndDontSave,
                vertices = vertices,
                uv = uv,
                triangles = triangles,
                colors32 = vertexColors,
                bounds = new Bounds(Vector3.zero, new Vector3(MapSize, 4096f, MapSize)),
            };
            overlayMesh.MarkDynamic();

            if (overlayFilter != null)
            {
                overlayFilter.sharedMesh = overlayMesh;
            }
        }

        private bool UpdateVertexColors()
        {
            float maxValue = SmoothSourceGrid();
            if (maxValue <= 0f)
            {
                return false;
            }

            for (int z = 0; z < VertexResolution; z++)
            {
                for (int x = 0; x < VertexResolution; x++)
                {
                    float average = GetVertexIntensity(x, z);
                    float normalized = average > 0f ? Mathf.Clamp01(average / maxValue) : 0f;
                    vertexColors[(z * VertexResolution) + x] = EvaluateColor(normalized);
                }
            }

            overlayMesh.colors32 = vertexColors;
            return true;
        }

        private float SmoothSourceGrid()
        {
            float maxValue = 0f;
            for (int z = 0; z < Resolution; z++)
            {
                for (int x = 0; x < Resolution; x++)
                {
                    float total = 0f;
                    float weight = 0f;
                    for (int dz = -1; dz <= 1; dz++)
                    {
                        int nz = z + dz;
                        if (nz < 0 || nz >= Resolution)
                        {
                            continue;
                        }

                        for (int dx = -1; dx <= 1; dx++)
                        {
                            int nx = x + dx;
                            if (nx < 0 || nx >= Resolution)
                            {
                                continue;
                            }

                            float cellWeight = dx == 0 && dz == 0 ? 1.5f : 1f;
                            total += sourceGrid[(nz * Resolution) + nx] * cellWeight;
                            weight += cellWeight;
                        }
                    }

                    float smoothed = weight > 0f ? total / weight : 0f;
                    smoothedGrid[(z * Resolution) + x] = smoothed;
                    if (smoothed > maxValue)
                    {
                        maxValue = smoothed;
                    }
                }
            }

            return maxValue;
        }

        private float GetVertexIntensity(int vertexX, int vertexZ)
        {
            float total = 0f;
            int count = 0;

            for (int dz = -1; dz <= 0; dz++)
            {
                int cellZ = vertexZ + dz;
                if (cellZ < 0 || cellZ >= Resolution)
                {
                    continue;
                }

                for (int dx = -1; dx <= 0; dx++)
                {
                    int cellX = vertexX + dx;
                    if (cellX < 0 || cellX >= Resolution)
                    {
                        continue;
                    }

                    total += smoothedGrid[(cellZ * Resolution) + cellX];
                    count++;
                }
            }

            return count > 0 ? total / count : 0f;
        }

        private float SampleOverlayHeight(float worldX, float worldZ)
        {
            TerrainManager terrainManager = TerrainManager.instance;
            if (terrainManager == null)
            {
                LogTerrainFallbackWarningOnce();
                return OverlayHeightOffset;
            }

            return terrainManager.SampleRawHeightSmooth(new Vector3(worldX, 0f, worldZ)) + OverlayHeightOffset;
        }

        private static Color32 EvaluateColor(float normalizedValue)
        {
            float value = Mathf.Clamp01(normalizedValue);
            if (value <= MinVisibleIntensity)
            {
                return new Color32(0, 0, 0, 0);
            }

            float green = value < 0.5f
                ? Mathf.Lerp(0.95f, 1f, value / 0.5f)
                : Mathf.Lerp(1f, 0.15f, (value - 0.5f) / 0.5f);
            float alpha = Mathf.Lerp(0.08f, 0.82f, Mathf.Pow(value, 0.65f));
            return new Color(1f, green, 0.05f, alpha);
        }

        private void SetVisible(bool visible)
        {
            if (overlayRenderer != null)
            {
                overlayRenderer.enabled = visible;
            }
        }

        private void LogMissingOverlayWarningOnce()
        {
            if (loggedMissingOverlayWarning)
            {
                return;
            }

            loggedMissingOverlayWarning = true;
            Debug.LogWarning("[RealTime] X-Ray overlay is enabled, but the heatmap mesh renderer is not available.");
        }

        private void LogEmptyHeatmapWarningOnce(PandemicXRayMode mode)
        {
            if (loggedEmptyHeatmapWarning)
            {
                return;
            }

            loggedEmptyHeatmapWarning = true;
            Debug.LogWarning("[RealTime] X-Ray mode '" + mode + "' is enabled, but no heatmap cells were populated.");
        }

        private void LogTerrainFallbackWarningOnce()
        {
            if (loggedTerrainFallbackWarning)
            {
                return;
            }

            loggedTerrainFallbackWarning = true;
            Debug.LogWarning("[RealTime] TerrainManager is unavailable while building the X-Ray mesh. Falling back to a flat overlay.");
        }

        private static void TryConfigureMaterialForOverlay(Material overlayMaterial)
        {
            if (overlayMaterial == null)
            {
                return;
            }

            if (overlayMaterial.HasProperty("_Cull"))
            {
                overlayMaterial.SetInt("_Cull", (int)CullMode.Off);
            }

            if (overlayMaterial.HasProperty("_ZWrite"))
            {
                overlayMaterial.SetInt("_ZWrite", 0);
            }

            if (overlayMaterial.HasProperty("_SrcBlend"))
            {
                overlayMaterial.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            }

            if (overlayMaterial.HasProperty("_DstBlend"))
            {
                overlayMaterial.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            }

            if (overlayMaterial.HasProperty("_ZTest"))
            {
                overlayMaterial.SetInt("_ZTest", (int)CompareFunction.Always);
            }
        }

        private void OnDestroy()
        {
            if (overlayObject != null)
            {
                Destroy(overlayObject);
            }

            if (overlayMesh != null)
            {
                Destroy(overlayMesh);
            }

            if (material != null)
            {
                Destroy(material);
            }
        }
    }
}
