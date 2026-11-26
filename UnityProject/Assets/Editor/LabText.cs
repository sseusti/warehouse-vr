using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace Warehouse.Editor
{
    /// <summary>
    /// World-space text for the lab: TextMeshPro with a static Noto Sans Bold SDF font
    /// (Latin, Cyrillic, digits and the symbols the room uses) and a dark outline,
    /// so labels stay sharp in the headset and readable on any background.
    /// </summary>
    public static class LabText
    {
        const string EssentialsFolder = "Assets/TextMesh Pro";
        const string SourceFontPath = "Assets/Fonts/NotoSans-Bold.ttf";
        const string FontAssetPath = "Assets/Fonts/NotoSans-Bold SDF.asset";

        // Sampling 90 with padding 9 keeps the 10 % padding ratio the TMP guidance recommends.
        const int SamplingPointSize = 90;
        const int Padding = 9;

        const string Characters =
            " !\"#%&'()*+,-./0123456789:;<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[]_abcdefghijklmnopqrstuvwxyz" +
            "АБВГДЕЁЖЗИЙКЛМНОПРСТУФХЦЧШЩЪЫЬЭЮЯабвгдеёжзийклмнопрстуфхцчшщъыьэюя" +
            // Noto Sans has no arrows; texts avoid them.
            "«»–—·…×°²³₀№";

        static readonly Color Outline = new(0.08f, 0.09f, 0.11f, 1f);

        /// <summary>Imports TextMeshPro Essential Resources once; run before the first scene build.</summary>
        [MenuItem("Warehouse/Import Text Resources")]
        public static void ImportEssentials()
        {
            if (AssetDatabase.IsValidFolder(EssentialsFolder))
                return;

            TMP_PackageResourceImporter.ImportResources(true, false, false);
            AssetDatabase.Refresh();
        }

        /// <summary>The shared font asset; created on first use and made the TMP default.</summary>
        public static TMP_FontAsset Font()
        {
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
            if (font == null)
                font = CreateFont();

            if (TMP_Settings.instance != null && TMP_Settings.defaultFontAsset != font)
            {
                TMP_Settings.defaultFontAsset = font;
                EditorUtility.SetDirty(TMP_Settings.instance);
            }
            return font;
        }

        static TMP_FontAsset CreateFont()
        {
            var source = AssetDatabase.LoadAssetAtPath<Font>(SourceFontPath);
            var font = TMP_FontAsset.CreateFontAsset(source, SamplingPointSize, Padding, GlyphRenderMode.SDFAA,
                1024, 1024, AtlasPopulationMode.Dynamic, enableMultiAtlasSupport: false);
            font.name = Path.GetFileNameWithoutExtension(FontAssetPath);
            if (!font.TryAddCharacters(Characters, out string missing))
                Debug.LogWarning($"Warehouse font is missing characters: {missing}");

            AssetDatabase.CreateAsset(font, FontAssetPath);
            Texture2D atlas = font.atlasTextures[0];
            atlas.name = font.name + " Atlas";
            AssetDatabase.AddObjectToAsset(atlas, font);

            // Mobile SDF shader: cheaper on Pico 4, still supports the outline.
            ShaderUtilities.GetShaderPropertyIDs();
            var material = new Material(Shader.Find("TextMeshPro/Mobile/Distance Field"))
            {
                name = font.name + " Material",
            };
            material.SetTexture(ShaderUtilities.ID_MainTex, atlas);
            material.SetFloat(ShaderUtilities.ID_TextureWidth, atlas.width);
            material.SetFloat(ShaderUtilities.ID_TextureHeight, atlas.height);
            material.SetFloat(ShaderUtilities.ID_GradientScale, Padding + 1);
            material.SetFloat(ShaderUtilities.ID_WeightNormal, font.normalStyle);
            material.SetFloat(ShaderUtilities.ID_WeightBold, font.boldStyle);
            material.EnableKeyword(ShaderUtilities.Keyword_Outline);
            material.SetFloat(ShaderUtilities.ID_OutlineWidth, 0.22f);
            material.SetColor(ShaderUtilities.ID_OutlineColor, Outline);
            material.SetFloat(ShaderUtilities.ID_FaceDilate, 0.15f);
            font.material = material;
            AssetDatabase.AddObjectToAsset(material, font);

            // All glyphs are baked: nothing is generated or shipped by accident at runtime.
            font.atlasPopulationMode = AtlasPopulationMode.Static;
            EditorUtility.SetDirty(font);
            AssetDatabase.SaveAssets();
            return font;
        }

        /// <summary>Creates a world-space label that faces along +Z, readable from the -Z side.</summary>
        public static TextMeshPro Create(string name, string text, Vector3 position, float fontSize, Color color,
            Transform parent = null, TextAlignmentOptions alignment = TextAlignmentOptions.Center, float yaw = 0f)
        {
            var label = new GameObject(name).AddComponent<TextMeshPro>();
            label.transform.SetParent(parent, false);
            label.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
            label.font = Font();
            label.fontSharedMaterial = label.font.material;
            label.text = text;
            label.fontSize = fontSize;
            label.color = color;
            label.alignment = alignment;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.rectTransform.sizeDelta = new Vector2(4f, 1f);
            return label;
        }
    }
}
