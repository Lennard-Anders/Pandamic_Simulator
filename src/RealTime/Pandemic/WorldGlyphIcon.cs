// <copyright file="WorldGlyphIcon.cs" company="dymanoid">Copyright (c) dymanoid. All rights reserved.</copyright>

namespace RealTime.Pandemic
{
    using UnityEngine;
    using UnityEngine.Rendering;

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1812:AvoidUninstantiatedInternalClasses", Justification = "Used as a Unity component")]
    internal sealed class WorldGlyphIcon : MonoBehaviour
    {
        public TextMesh Foreground { get; set; }

        public TextMesh Shadow { get; set; }

        public TextMesh[] Outlines { get; set; }
    }

    internal static class WorldGlyphIconFactory
    {
        private const float OutlineScale = 1.06f;
        private const float OutlineOffsetFactor = 0.14f;
        private const float ShadowScale = 1.02f;
        private const float ShadowOffsetFactor = 0.18f;
        private const int ShadowSortingOrder = 0;
        private const int OutlineSortingOrder = 1;
        private const int ForegroundSortingOrder = 2;

        internal static Material CreateUnlitTextMaterial(Font font)
        {
            Material baseMaterial = font?.material;
            Shader shader = Shader.Find("GUI/Text Shader")
                ?? baseMaterial?.shader
                ?? Shader.Find("Unlit/Transparent")
                ?? Shader.Find("Sprites/Default");
            Material material = baseMaterial != null ? new Material(baseMaterial) : new Material(shader);
            material.hideFlags = HideFlags.HideAndDontSave;

            if (shader != null)
            {
                material.shader = shader;
            }

            if (baseMaterial?.mainTexture != null)
            {
                material.mainTexture = baseMaterial.mainTexture;
            }

            material.renderQueue = 3100;

            if (material.HasProperty("_Cull"))
            {
                material.SetInt("_Cull", (int)CullMode.Off);
            }

            if (material.HasProperty("_ZWrite"))
            {
                material.SetInt("_ZWrite", 0);
            }

            if (material.HasProperty("_SrcBlend"))
            {
                material.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            }

            if (material.HasProperty("_DstBlend"))
            {
                material.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            }

            return material;
        }

        internal static GameObject CreateIconRoot(string rootName, Font font, Material material, int fontSize)
        {
            var root = new GameObject(rootName);
            root.hideFlags = HideFlags.HideAndDontSave;

            WorldGlyphIcon icon = root.AddComponent<WorldGlyphIcon>();
            icon.Shadow = CreateGlyph(root.transform, "Shadow", font, material, fontSize, ShadowSortingOrder);
            icon.Outlines = new[]
            {
                CreateGlyph(root.transform, "OutlineLeft", font, material, fontSize, OutlineSortingOrder),
                CreateGlyph(root.transform, "OutlineRight", font, material, fontSize, OutlineSortingOrder),
                CreateGlyph(root.transform, "OutlineUp", font, material, fontSize, OutlineSortingOrder),
                CreateGlyph(root.transform, "OutlineDown", font, material, fontSize, OutlineSortingOrder),
            };
            icon.Foreground = CreateGlyph(root.transform, "Foreground", font, material, fontSize, ForegroundSortingOrder);
            return root;
        }

        internal static void ApplyStyle(
            WorldGlyphIcon icon,
            string glyph,
            float characterSize,
            Color foregroundColor,
            Color outlineColor,
            Color shadowColor)
        {
            if (icon == null)
            {
                return;
            }

            float outlineOffset = characterSize * OutlineOffsetFactor;
            float shadowOffset = characterSize * ShadowOffsetFactor;

            ConfigureGlyph(icon.Shadow, glyph, characterSize * ShadowScale, shadowColor, new Vector3(shadowOffset, -shadowOffset, 0f));
            ConfigureGlyph(icon.Foreground, glyph, characterSize, foregroundColor, Vector3.zero);

            if (icon.Outlines != null && icon.Outlines.Length >= 4)
            {
                ConfigureGlyph(icon.Outlines[0], glyph, characterSize * OutlineScale, outlineColor, new Vector3(-outlineOffset, 0f, 0f));
                ConfigureGlyph(icon.Outlines[1], glyph, characterSize * OutlineScale, outlineColor, new Vector3(outlineOffset, 0f, 0f));
                ConfigureGlyph(icon.Outlines[2], glyph, characterSize * OutlineScale, outlineColor, new Vector3(0f, outlineOffset, 0f));
                ConfigureGlyph(icon.Outlines[3], glyph, characterSize * OutlineScale, outlineColor, new Vector3(0f, -outlineOffset, 0f));
            }
        }

        private static TextMesh CreateGlyph(Transform parent, string name, Font font, Material material, int fontSize, int sortingOrder)
        {
            var glyphObject = new GameObject(name);
            glyphObject.hideFlags = HideFlags.HideAndDontSave;
            glyphObject.transform.SetParent(parent, false);

            TextMesh textMesh = glyphObject.AddComponent<TextMesh>();
            textMesh.anchor = TextAnchor.MiddleCenter;
            textMesh.alignment = TextAlignment.Center;
            textMesh.fontSize = fontSize;
            textMesh.characterSize = 1f;
            if (font != null)
            {
                textMesh.font = font;
            }

            MeshRenderer meshRenderer = glyphObject.GetComponent<MeshRenderer>();
            if (meshRenderer != null)
            {
                meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
                meshRenderer.receiveShadows = false;
                meshRenderer.sortingOrder = sortingOrder;
                if (material != null)
                {
                    meshRenderer.sharedMaterial = material;
                }
            }

            return textMesh;
        }

        private static void ConfigureGlyph(TextMesh textMesh, string glyph, float characterSize, Color color, Vector3 localPosition)
        {
            if (textMesh == null)
            {
                return;
            }

            textMesh.text = glyph;
            textMesh.characterSize = characterSize;
            textMesh.color = color;
            textMesh.transform.localPosition = localPosition;
        }
    }
}
