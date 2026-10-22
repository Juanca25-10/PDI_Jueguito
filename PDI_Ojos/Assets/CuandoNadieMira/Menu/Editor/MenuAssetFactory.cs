using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.TextCore.LowLevel;

namespace CuandoNadieMira.Menu.EditorTools
{
    /// <summary>Crea materiales, fuentes TMP y el perfil de postproceso del menú.</summary>
    public static class MenuAssetFactory
    {
        public const string Root = "Assets/CuandoNadieMira/Menu";
        public const string MatFolder = Root + "/Materials";

        const string SilhouetteShader = "CuandoNadieMira/SilhouetteDissolve";

        /// <summary>Crea la carpeta (y sus padres) en el AssetDatabase si no existe.</summary>
        public static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path)!.Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        // --- Importación de modelos ------------------------------------------------------

        /// <summary>Ajusta la importación de los FBX: sin materiales embebidos y con malla legible (destellos).</summary>
        public static void ConfigureModelImporters()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { Root + "/Models" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (AssetImporter.GetAtPath(path) is not ModelImporter imp) continue;
                bool changed = imp.materialImportMode != ModelImporterMaterialImportMode.None || !imp.isReadable
                               || imp.importCameras || imp.importLights;
                imp.materialImportMode = ModelImporterMaterialImportMode.None;
                imp.isReadable = true;
                imp.importCameras = false;
                imp.importLights = false;
                imp.importAnimation = false;
                if (changed) imp.SaveAndReimport();
            }
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { Root + "/Models", Root + "/Textures" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.Contains("_nor_gl") || AssetImporter.GetAtPath(path) is not TextureImporter imp) continue;
                if (imp.textureType == TextureImporterType.NormalMap) continue;
                imp.textureType = TextureImporterType.NormalMap;
                imp.SaveAndReimport();
            }
        }

        public static Texture2D ModelTexture(string id, string map) =>
            AssetDatabase.LoadAssetAtPath<Texture2D>($"{Root}/Models/{id}/{id}_{map}.jpg");

        public static Texture2D SurfaceTexture(string id, string map) =>
            AssetDatabase.LoadAssetAtPath<Texture2D>($"{Root}/Textures/{id}/{id}_{map}.jpg");

        // --- Materiales -----------------------------------------------------------------

        /// <summary>Material de silueta para un modelo descargado (usa su albedo y normal).</summary>
        public static Material Silhouette(string name, string modelId, float silhouette, Color rim, Color? tint = null,
            float smoothness = 0.3f, float metallic = 0f)
        {
            var m = Create(name, SilhouetteShader);
            if (modelId != null)
            {
                var diff = ModelTexture(modelId, "diffuse");
                var nor = ModelTexture(modelId, "nor_gl");
                if (diff != null) m.SetTexture("_BaseMap", diff);
                if (nor != null) m.SetTexture("_BumpMap", nor);
            }
            m.SetColor("_BaseColor", tint ?? Color.white);
            m.SetFloat("_Silhouette", silhouette);
            m.SetColor("_RimColor", rim);
            m.SetFloat("_Smoothness", smoothness);
            m.SetFloat("_Metallic", metallic);
            m.SetFloat("_NoiseScale", 7f);
            return m;
        }

        public static Material Glass(string name)
        {
            var m = Create(name, "CuandoNadieMira/GlassDissolve");
            m.SetFloat("_Alpha", 0.05f);
            m.SetColor("_RimColor", new Color(0.55f, 0.58f, 0.62f));
            return m;
        }

        public static Material Lit(string name, Color color, float smoothness, float metallic = 0f,
            Texture2D baseMap = null, Texture2D normal = null, Vector2? tiling = null)
        {
            var m = Create(name, "Universal Render Pipeline/Lit");
            m.SetColor("_BaseColor", color);
            m.SetFloat("_Smoothness", smoothness);
            m.SetFloat("_Metallic", metallic);
            if (baseMap != null) m.SetTexture("_BaseMap", baseMap);
            if (normal != null)
            {
                m.SetTexture("_BumpMap", normal);
                m.EnableKeyword("_NORMALMAP");
            }
            if (tiling.HasValue)
            {
                m.SetTextureScale("_BaseMap", tiling.Value);
                m.SetTextureScale("_BumpMap", tiling.Value);
            }
            return m;
        }

        /// <summary>Unlit opaco con color HDR (llamas, vidrio del ventanal, rendijas de luz).</summary>
        public static Material Emissive(string name, Color hdr)
        {
            var m = Create(name, "Universal Render Pipeline/Unlit");
            m.SetColor("_BaseColor", hdr);
            return m;
        }

        public enum Blend { Alpha, Additive }

        /// <summary>Material transparente (partículas, niebla, huellas, haces de luz).</summary>
        public static Material Transparent(string name, string shader, Texture2D tex, Color color, Blend blend)
        {
            var m = Create(name, shader);
            if (tex != null) m.SetTexture("_BaseMap", tex);
            m.SetColor("_BaseColor", color);
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", blend == Blend.Additive ? 2f : 0f);
            m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", blend == Blend.Additive ? (float)BlendMode.One : (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            m.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0f);
            m.SetFloat("_Cull", 0f);
            m.SetOverrideTag("RenderType", "Transparent");
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            if (blend == Blend.Additive) m.EnableKeyword("_BLENDMODE_ADD");
            m.renderQueue = (int)RenderQueue.Transparent;
            return m;
        }

        static Material Create(string name, string shaderName)
        {
            EnsureFolder(MatFolder);
            string path = $"{MatFolder}/{name}.mat";
            var shader = Shader.Find(shaderName);
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(m, path);
            }
            else
            {
                m.shader = shader;
                m.shaderKeywords = new string[0];
            }
            EditorUtility.SetDirty(m);
            return m;
        }

        // --- Fuentes --------------------------------------------------------------------

        const string Glyphs = "ABCDEFGHIJKLMNÑOPQRSTUVWXYZabcdefghijklmnñopqrstuvwxyzÁÉÍÓÚÜáéíóúü0123456789 .,;:¿?¡!«»\"'()[]-–—·/%+&";

        public static TMP_FontAsset Font(string ttfName)
        {
            string fontsDir = Root + "/Fonts";
            string outPath = $"{fontsDir}/{ttfName} SDF.asset";
            var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(outPath);
            if (existing != null) return existing;

            var font = AssetDatabase.LoadAssetAtPath<Font>($"{fontsDir}/{ttfName}.ttf");
            var fa = TMP_FontAsset.CreateFontAsset(font, 90, 9, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, true);
            fa.name = ttfName + " SDF";
            AssetDatabase.CreateAsset(fa, outPath);
            fa.material.name = ttfName + " SDF Material";
            AssetDatabase.AddObjectToAsset(fa.material, fa);
            fa.atlasTextures[0].name = ttfName + " SDF Atlas";
            AssetDatabase.AddObjectToAsset(fa.atlasTextures[0], fa);
            fa.TryAddCharacters(Glyphs, out _);
            EditorUtility.SetDirty(fa);
            AssetDatabase.SaveAssets();
            return fa;
        }

        /// <summary>Material del título: textura de grietas sobre la cara de las letras y sombra suave.</summary>
        public static Material TitleMaterial(TMP_FontAsset font, Texture2D cracks)
        {
            EnsureFolder(MatFolder);
            string path = $"{MatFolder}/Title_Cracked.mat";
            AssetDatabase.DeleteAsset(path);
            var m = new Material(font.material) { name = "Title_Cracked" };
            m.SetTexture("_FaceTex", cracks);
            m.SetColor("_FaceColor", new Color(0.93f, 0.93f, 0.93f, 1f));
            m.SetColor("_UnderlayColor", new Color(0f, 0f, 0f, 0.75f));
            m.SetFloat("_UnderlayOffsetX", 0.3f);
            m.SetFloat("_UnderlayOffsetY", -0.4f);
            m.SetFloat("_UnderlaySoftness", 0.5f);
            m.EnableKeyword("UNDERLAY_ON");
            AssetDatabase.CreateAsset(m, path);
            return m;
        }

        // --- Postproceso ---------------------------------------------------------------

        public static VolumeProfile PostProfile()
        {
            string path = Root + "/Settings/MainMenu_PostProcess.asset";
            EnsureFolder(Root + "/Settings");
            AssetDatabase.DeleteAsset(path);
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, path);

            var tm = profile.Add<Tonemapping>(true);
            tm.mode.value = TonemappingMode.ACES;

            var ca = profile.Add<ColorAdjustments>();
            ca.saturation.Override(-100f);   // escala de grises (ver ColorAccent en el README)
            ca.contrast.Override(18f);
            ca.postExposure.Override(0.9f);

            var bloom = profile.Add<Bloom>();
            bloom.threshold.Override(1f);
            bloom.intensity.Override(0.6f);
            bloom.scatter.Override(0.7f);

            var vig = profile.Add<Vignette>();
            vig.intensity.Override(0.38f);
            vig.smoothness.Override(0.45f);

            var grain = profile.Add<FilmGrain>();
            grain.type.Override(FilmGrainLookup.Medium3);
            grain.intensity.Override(0.35f);
            grain.response.Override(0.8f);

            var dof = profile.Add<DepthOfField>();
            dof.mode.Override(DepthOfFieldMode.Gaussian);
            dof.gaussianStart.Override(8f);
            dof.gaussianEnd.Override(17f);
            dof.gaussianMaxRadius.Override(1f);

            var chroma = profile.Add<ChromaticAberration>();
            chroma.intensity.Override(0.05f);

            foreach (var c in profile.components) AssetDatabase.AddObjectToAsset(c, profile);
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
            return profile;
        }
    }
}
