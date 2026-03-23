// <copyright file="PandemicXRayOverlayBehavior.cs" company="dymanoid">Copyright (c) dymanoid. All rights reserved.</copyright>

namespace RealTime.Pandemic
{
    using UnityEngine;

    /// <summary>
    /// Draws a city-wide X-Ray heatmap of infected citizens on a transparent quad.
    /// </summary>
    internal sealed class PandemicXRayOverlayBehavior : MonoBehaviour
    {
        private const int Resolution = 128;
        private const float MapSize = 17280f;
        private const float OverlayHeight = 260f;

        private readonly float[] sourceGrid = new float[Resolution * Resolution];
        private readonly float[] smoothedGrid = new float[Resolution * Resolution];
        private readonly Color32[] pixels = new Color32[Resolution * Resolution];

        private GameObject overlayObject;
        private MeshRenderer overlayRenderer;
        private Texture2D texture;
        private Material material;
        private float nextRefreshTime;

        private void Awake()
        {
            texture = new Texture2D(Resolution, Resolution, TextureFormat.ARGB32, mipmap: false);
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;

            var shader = Shader.Find("Unlit/Transparent") ?? Shader.Find("Sprites/Default");
            material = new Material(shader);
            material.mainTexture = texture;
            material.hideFlags = HideFlags.HideAndDontSave;

            overlayObject = GameObject.CreatePrimitive(PrimitiveType.Quad);
            overlayObject.name = "PandemicXRayOverlay";
            overlayObject.hideFlags = HideFlags.HideAndDontSave;
            overlayObject.transform.SetParent(transform, worldPositionStays: false);
            overlayObject.transform.localPosition = new Vector3(0f, OverlayHeight, 0f);
            overlayObject.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            overlayObject.transform.localScale = new Vector3(MapSize, MapSize, 1f);

            overlayRenderer = overlayObject.GetComponent<MeshRenderer>();
            if (overlayRenderer != null)
            {
                overlayRenderer.material = material;
                overlayRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                overlayRenderer.receiveShadows = false;
                overlayRenderer.enabled = false;
            }

            var collider = overlayObject.GetComponent<Collider>();
            if (collider != null)
            {
                Destroy(collider);
            }
        }

        private void LateUpdate()
        {
            var manager = PandemicManager.Instance;
            if (manager == null || overlayRenderer == null)
            {
                SetVisible(false);
                return;
            }

            PandemicXRayMode mode = manager.GetXRayMode();
            if (mode == PandemicXRayMode.Off || !manager.AreWorldOverlaysEnabled())
            {
                SetVisible(false);
                return;
            }

            if (Time.unscaledTime < nextRefreshTime)
            {
                return;
            }

            nextRefreshTime = Time.unscaledTime + 1f;
            if (manager.PopulateHeatmapGrid(sourceGrid, mode) == 0)
            {
                SetVisible(false);
                return;
            }

            BuildTexture();
            SetVisible(true);
        }

        private void BuildTexture()
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

            if (maxValue <= 0f)
            {
                SetVisible(false);
                return;
            }

            for (int i = 0; i < smoothedGrid.Length; i++)
            {
                float value = Mathf.Clamp01(smoothedGrid[i] / maxValue);
                if (value <= 0.001f)
                {
                    pixels[i] = new Color32(0, 0, 0, 0);
                    continue;
                }

                float red = 1f;
                float green = value < 0.5f
                    ? Mathf.Lerp(0f, 1f, value / 0.5f)
                    : Mathf.Lerp(1f, 0.15f, (value - 0.5f) / 0.5f);
                byte alpha = (byte)(Mathf.Clamp01(Mathf.Pow(value, 0.65f)) * 180f);
                pixels[i] = new Color(red, green, 0f, alpha / 255f);
            }

            texture.SetPixels32(pixels);
            texture.Apply(updateMipmaps: false, makeNoLongerReadable: false);
        }

        private void SetVisible(bool visible)
        {
            if (overlayRenderer != null)
            {
                overlayRenderer.enabled = visible;
            }
        }

        private void OnDestroy()
        {
            if (overlayObject != null)
            {
                Destroy(overlayObject);
            }

            if (material != null)
            {
                Destroy(material);
            }

            if (texture != null)
            {
                Destroy(texture);
            }
        }
    }
}
