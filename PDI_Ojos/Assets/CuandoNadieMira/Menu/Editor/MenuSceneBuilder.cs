using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace CuandoNadieMira.Menu.EditorTools
{
    /// <summary>
    /// Construye la escena completa del menú principal (salón, piezas, luces, UI y sistemas).
    /// Menú: Cuando Nadie Mira → Construir menú principal. Se puede volver a ejecutar cuando se quiera.
    /// </summary>
    public static class MenuSceneBuilder
    {
        const string Root = MenuAssetFactory.Root;
        const string ScenePath = Root + "/Scenes/MainMenu.unity";
        const int UILayer = 5;

        // Paleta (la saturación final la quita el postproceso)
        static readonly Color RimCold = new(0.42f, 0.46f, 0.52f);
        static readonly Color RimSoft = new(0.22f, 0.24f, 0.27f);
        static readonly Color CandleColor = new(1f, 0.9f, 0.78f);
        static readonly Color MoonColor = new(0.78f, 0.85f, 1f);

        // Estado de la construcción
        static Transform env, exhibitsRoot, fadeRoot, anchorsRoot, lightingRoot, atmosphereRoot;
        static readonly List<MenuCandle> candles = new();
        static readonly List<GameObject> lowQualityObjects = new();
        static Material matWall, matColumn, matFloor, matDark, matBronze, matWood, matVelvet, matFlame, matPedestal;
        static TMP_FontAsset fontTitle, fontBody, fontCaps;
        static Sprite sprFill, sprOutline, sprOrnament, sprPill, sprCircle;
        static Material sparkleMat;

        [MenuItem("Cuando Nadie Mira/Construir menú principal")]
        public static void Build()
        {
            candles.Clear();
            lowQualityObjects.Clear();

            // 1. Recursos
            MenuAssetFactory.ConfigureModelImporters();
            var cookie = MenuTextureFactory.Cookie();
            var cracks = MenuTextureFactory.Cracks();
            var dot = MenuTextureFactory.Dot();
            var footprintTex = MenuTextureFactory.Footprint();
            var fogTex = MenuTextureFactory.FogNoise();
            var shaftTex = MenuTextureFactory.Shaft();
            var paperTex = MenuTextureFactory.Paper();
            sprFill = MenuTextureFactory.RoundedFill();
            sprOutline = MenuTextureFactory.RoundedOutline();
            sprOrnament = MenuTextureFactory.Ornament();
            sprPill = MenuTextureFactory.Pill();
            sprCircle = MenuTextureFactory.Circle();
            var cursorSprites = (eye: MenuTextureFactory.EyeAlmond(), iris: MenuTextureFactory.Iris());
            var icons = new Dictionary<string, Sprite>();
            foreach (var n in new[] { "Eye", "EyeClosed", "Gear", "Gamepad", "Book", "Door", "Chevron", "ChevronLeft", "Home", "Frame", "Key" })
                icons[n] = MenuTextureFactory.Icon(n);

            fontTitle = MenuAssetFactory.Font("IMFellEnglish");
            fontBody = MenuAssetFactory.Font("CormorantGaramond");
            fontCaps = MenuAssetFactory.Font("Cinzel");
            var titleMat = MenuAssetFactory.TitleMaterial(fontTitle, cracks);

            CreateMaterials(paperTex);
            sparkleMat = MenuAssetFactory.Transparent("FX_Sparkle", "Universal Render Pipeline/Particles/Unlit", dot,
                new Color(1.5f, 1.55f, 1.65f, 0.9f), MenuAssetFactory.Blend.Additive);
            var emberMat = MenuAssetFactory.Transparent("FX_Ember", "Universal Render Pipeline/Particles/Unlit", dot,
                new Color(1f, 1f, 1f, 0.9f), MenuAssetFactory.Blend.Additive);
            var dustMat = MenuAssetFactory.Transparent("FX_Dust", "Universal Render Pipeline/Particles/Unlit", dot,
                new Color(1.4f, 1.4f, 1.5f, 0.35f), MenuAssetFactory.Blend.Additive);
            var fogMat = MenuAssetFactory.Transparent("FX_FogLayer", "Universal Render Pipeline/Particles/Unlit", fogTex,
                new Color(0.55f, 0.57f, 0.6f, 0.16f), MenuAssetFactory.Blend.Alpha);
            var shaftMat = MenuAssetFactory.Transparent("FX_LightShaft", "Universal Render Pipeline/Particles/Unlit", shaftTex,
                new Color(0.6f, 0.65f, 0.75f, 0.13f), MenuAssetFactory.Blend.Additive);
            var footprintMat = MenuAssetFactory.Transparent("FX_Footprint", "Universal Render Pipeline/Unlit", footprintTex,
                new Color(0.75f, 0.75f, 0.75f, 0.55f), MenuAssetFactory.Blend.Alpha);
            var profile = MenuAssetFactory.PostProfile();

            // 2. Escena nueva
            MenuAssetFactory.EnsureFolder(Root + "/Scenes");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            SetupRenderSettings();

            env = new GameObject("Environment").transform;
            var arch = Child("Architecture", env);
            exhibitsRoot = Child("Exhibits", env);
            fadeRoot = Child("FadeableProps", env);
            anchorsRoot = Child("Anchors", fadeRoot);
            lightingRoot = Child("Lighting", env);
            atmosphereRoot = Child("Atmosphere", env);

            BuildArchitecture(arch);
            var windowParts = BuildWindow(arch, shaftMat);
            BuildStaticFurniture();

            // 3. Sistemas base (las piezas necesitan el pool de destellos)
            var systems = new GameObject("_Systems");
            var sparkles = systems.AddComponent<SparkleEmitter>();
            SetField(sparkles, "sparkleMaterial", sparkleMat);
            var fadeDirector = systems.AddComponent<FadeDirector>();
            var audio = systems.AddComponent<MenuAudio>();
            var musicGo = new GameObject("Music");
            musicGo.transform.SetParent(systems.transform, false);
            musicGo.AddComponent<MenuMusic>();
            var controller = systems.AddComponent<MenuController>();
            var transition = systems.AddComponent<SceneTransition>();
            var flipper = systems.AddComponent<PanelFlipper>();

            // 4. Piezas del museo (una por botón)
            var exhibits = BuildExhibits();
            var fadeables = BuildFadeables(sparkles);
            fadeDirector.Configure(fadeables, footprintMat, 0f);
            BuildCandles();
            BuildAtmosphere(fogMat, dustMat);

            // 5. Cámara
            var rigGo = new GameObject("CameraRig");
            rigGo.transform.SetPositionAndRotation(new Vector3(0f, 1.62f, -1.6f), Quaternion.Euler(-1.5f, 0f, 0f));
            var rig = rigGo.AddComponent<MenuCameraRig>();
            var cam = CreateCamera(rigGo.transform, out var uiCam);
            var cursorLightGo = new GameObject("CursorLight");
            cursorLightGo.transform.SetParent(cam.transform, false);
            cursorLightGo.transform.localPosition = new Vector3(0.12f, -0.08f, 0f);
            var cursorSpot = cursorLightGo.AddComponent<Light>();
            cursorSpot.type = LightType.Spot;
            cursorSpot.spotAngle = 35f;
            cursorSpot.innerSpotAngle = 12f;
            cursorSpot.range = 30f;
            cursorSpot.intensity = 6f;
            cursorSpot.color = CandleColor;
            cursorSpot.cookie = cookie;
            cursorSpot.shadows = LightShadows.None;
            var cursorLight = cursorLightGo.AddComponent<CursorLight>();
            SetField(cursorLight, "cam", cam);

            // Volumen global de postproceso
            var volumeGo = new GameObject("PostProcess");
            var volume = volumeGo.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = profile;

            // 6. UI
            var ui = new GameObject("UI").transform;
            var plate = BuildWorldPlate(ui, rigGo.transform, uiCam, titleMat, icons, emberMat, exhibits, out var frontGroup,
                out var backGroup, out var mainButtons, out var plateMessage, out var titleLines, out var branches, out var panels);
            var overlay = BuildOverlay(ui, uiCam, icons, out var subtitleLabel);
            var screenOverlay = BuildScreenOverlay(ui, cursorSprites.eye, cursorSprites.iris, sprCircle, out var blackout);
            BuildEventSystem(ui);

            // 7. Conexiones
            SetField(rig, "uiPlate", plate);
            SetField(rig, "volume", volume);
            SetField(audio, "subtitleLabel", subtitleLabel);
            transition.Configure(rig, cam, blackout, candles.ToArray(), exhibits[0].FocusPoint);
            flipper.Configure(rig, frontGroup, backGroup, new[] { panels.controls, panels.accessibility, panels.credits, panels.exit });
            controller.Configure(mainButtons, rig, flipper, transition, plateMessage, lowQualityObjects.ToArray());
            controller.ConfigurePanels(panels.controls, panels.accessibility, panels.credits, panels.exit, panels.creditsContent,
                panels.controlsButtons, panels.accessibilityButtons, panels.creditsButtons, panels.exitButtons,
                panels.homeButtons, panels.accessibilityBack, panels.optionHelp);
            var title = plate.gameObject.AddComponent<TitleAnimator>();
            title.Configure(titleLines, branches, mainButtons[0], windowParts.light, windowParts.glass);

            foreach (var a in Object.FindObjectsByType<AmbientSound>(FindObjectsSortMode.None)) SetField(a, "cursorLight", cursorLight);

            // 8. Guardar y registrar en Build Settings
            EditorSceneManager.SaveScene(scene, ScenePath);
            AddToBuildSettings(ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log("[Cuando Nadie Mira] Menú principal construido en " + ScenePath);
        }

        // ============================================================================
        // Ambiente
        // ============================================================================

        static void SetupRenderSettings()
        {
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.12f, 0.12f, 0.13f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogDensity = 0.024f;
            RenderSettings.fogColor = new Color(0.025f, 0.025f, 0.028f);
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;
            RenderSettings.reflectionIntensity = 0.6f;
        }

        static void CreateMaterials(Texture2D paperTex)
        {
            var marbleD = MenuAssetFactory.SurfaceTexture("marble_tiles", "diffuse");
            var marbleN = MenuAssetFactory.SurfaceTexture("marble_tiles", "nor_gl");
            var plasterD = MenuAssetFactory.SurfaceTexture("plastered_wall_04", "diffuse");
            var plasterN = MenuAssetFactory.SurfaceTexture("plastered_wall_04", "nor_gl");
            var velvetD = MenuAssetFactory.SurfaceTexture("velour_velvet", "diffuse");
            var velvetN = MenuAssetFactory.SurfaceTexture("velour_velvet", "nor_gl");

            matFloor = MenuAssetFactory.Lit("Floor_Marble", new Color(0.42f, 0.42f, 0.42f), 0.9f, 0f, marbleD, marbleN, new Vector2(7f, 16f));
            matWall = MenuAssetFactory.Lit("Wall_Plaster", new Color(0.3f, 0.3f, 0.3f), 0.15f, 0f, plasterD, plasterN, new Vector2(4f, 2f));
            matColumn = MenuAssetFactory.Lit("Column_Plaster", new Color(0.45f, 0.45f, 0.45f), 0.25f, 0f, plasterD, plasterN, new Vector2(1f, 3f));
            matPedestal = MenuAssetFactory.Lit("Pedestal_Marble", new Color(0.3f, 0.3f, 0.3f), 0.7f, 0f, marbleD, marbleN, new Vector2(0.6f, 0.6f));
            matDark = MenuAssetFactory.Lit("Dark_Matte", new Color(0.03f, 0.03f, 0.03f), 0.1f);
            matBronze = MenuAssetFactory.Lit("Bronze_Aged", new Color(0.3f, 0.27f, 0.22f), 0.42f, 1f);
            matWood = MenuAssetFactory.Lit("Wood_Dark", new Color(0.1f, 0.08f, 0.07f), 0.35f);
            matVelvet = MenuAssetFactory.Lit("Velvet_Rope", new Color(0.35f, 0.3f, 0.3f), 0.2f, 0f, velvetD, velvetN, new Vector2(1f, 8f));
            matFlame = MenuAssetFactory.Emissive("Candle_Flame", new Color(5f, 4.6f, 4f));
            MenuAssetFactory.Lit("Paper", Color.white, 0.1f, 0f, paperTex);
        }

        static void BuildArchitecture(Transform arch)
        {
            // Piso de mármol pulido
            var floor = Prim(PrimitiveType.Plane, "Floor", arch, new Vector3(0f, 0f, 12f), Vector3.zero, new Vector3(1.4f, 1f, 3.4f), matFloor);
            floor.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            Prim(PrimitiveType.Cube, "Ceiling", arch, new Vector3(0f, 9.1f, 12f), Vector3.zero, new Vector3(14.5f, 0.2f, 34f), matDark);

            // Muro izquierdo y muro derecho con vano para la puerta lateral
            Prim(PrimitiveType.Cube, "Wall_Left", arch, new Vector3(-7.25f, 4.5f, 12f), Vector3.zero, new Vector3(0.5f, 9f, 34f), matWall);
            Prim(PrimitiveType.Cube, "Wall_Right_A", arch, new Vector3(7.25f, 4.5f, 3.7f), Vector3.zero, new Vector3(0.5f, 9f, 17.4f), matWall);
            Prim(PrimitiveType.Cube, "Wall_Right_B", arch, new Vector3(7.25f, 4.5f, 21.7f), Vector3.zero, new Vector3(0.5f, 9f, 14.6f), matWall);
            Prim(PrimitiveType.Cube, "Wall_Right_Top", arch, new Vector3(7.25f, 6.3f, 13.4f), Vector3.zero, new Vector3(0.5f, 5.4f, 2f), matWall);
            Prim(PrimitiveType.Cube, "Wall_Front", arch, new Vector3(0f, 4.5f, -5f), Vector3.zero, new Vector3(14.5f, 9f, 0.5f), matDark);

            // Muro del fondo: vano de la puerta (abajo) y ventanal (arriba)
            Prim(PrimitiveType.Cube, "Wall_Back_L", arch, new Vector3(-4.25f, 4.5f, 28.25f), Vector3.zero, new Vector3(5.5f, 9f, 0.5f), matWall);
            Prim(PrimitiveType.Cube, "Wall_Back_R", arch, new Vector3(4.25f, 4.5f, 28.25f), Vector3.zero, new Vector3(5.5f, 9f, 0.5f), matWall);
            Prim(PrimitiveType.Cube, "Wall_Back_Mid", arch, new Vector3(0f, 4.3f, 28.25f), Vector3.zero, new Vector3(3f, 1.4f, 0.5f), matWall);
            Prim(PrimitiveType.Cube, "Wall_Back_Top", arch, new Vector3(0f, 8.6f, 28.25f), Vector3.zero, new Vector3(3f, 0.8f, 0.5f), matWall);

            // Pasillo oscuro tras la puerta del fondo
            Prim(PrimitiveType.Cube, "Corridor_Floor", arch, new Vector3(0f, -0.05f, 31.5f), Vector3.zero, new Vector3(3f, 0.1f, 6f), matFloor);
            Prim(PrimitiveType.Cube, "Corridor_L", arch, new Vector3(-1.75f, 1.8f, 31.5f), Vector3.zero, new Vector3(0.5f, 3.6f, 6f), matWall);
            Prim(PrimitiveType.Cube, "Corridor_R", arch, new Vector3(1.75f, 1.8f, 31.5f), Vector3.zero, new Vector3(0.5f, 3.6f, 6f), matWall);
            Prim(PrimitiveType.Cube, "Corridor_End", arch, new Vector3(0f, 1.8f, 34.6f), Vector3.zero, new Vector3(3.5f, 3.6f, 0.3f), matDark);
            Prim(PrimitiveType.Cube, "Corridor_Top", arch, new Vector3(0f, 3.7f, 31.5f), Vector3.zero, new Vector3(3.5f, 0.2f, 6f), matDark);

            // Arco de medio punto sobre la puerta
            var archMesh = ProceduralMeshes.Arch(1.5f, 1.95f, 0.6f);
            MeshObject("Arch_Door", arch, archMesh, new Vector3(0f, 3.6f, 27.95f), Quaternion.identity, Vector3.one, matColumn);

            // Columnas con basa y capitel
            foreach (float x in new[] { -5.2f, 5.2f })
            foreach (float z in new[] { 2f, 8f, 14f, 20f })
            {
                var col = Child($"Column_{(x < 0 ? "L" : "R")}_{z}", arch);
                Prim(PrimitiveType.Cylinder, "Shaft", col, new Vector3(x, 4.5f, z), Vector3.zero, new Vector3(0.66f, 4.3f, 0.66f), matColumn);
                Prim(PrimitiveType.Cube, "Base", col, new Vector3(x, 0.2f, z), Vector3.zero, new Vector3(0.95f, 0.4f, 0.95f), matColumn);
                Prim(PrimitiveType.Cube, "Capital", col, new Vector3(x, 8.75f, z), Vector3.zero, new Vector3(0.95f, 0.5f, 0.95f), matColumn);
            }

            // Arcos entre columnas a lo largo del salón
            var sideArch = ProceduralMeshes.Arch(2.6f, 3.0f, 0.5f);
            foreach (float x in new[] { -5.2f, 5.2f })
            foreach (float z in new[] { 5f, 11f, 17f })
                MeshObject("Arch_Side", arch, sideArch, new Vector3(x, 6f, z), Quaternion.Euler(0f, 90f, 0f), Vector3.one, matColumn);

            // Balcón del segundo nivel con barandal
            var balcony = Child("Balcony", arch);
            Prim(PrimitiveType.Cube, "Platform", balcony, new Vector3(0f, 4.15f, 22.6f), Vector3.zero, new Vector3(14f, 0.3f, 3.2f), matColumn);
            Prim(PrimitiveType.Cube, "Rail", balcony, new Vector3(0f, 5.25f, 21.05f), Vector3.zero, new Vector3(14f, 0.07f, 0.1f), matColumn);
            Prim(PrimitiveType.Cube, "RailBase", balcony, new Vector3(0f, 4.36f, 21.05f), Vector3.zero, new Vector3(14f, 0.12f, 0.14f), matColumn);
            for (float x = -6.8f; x <= 6.8f; x += 0.38f)
                Prim(PrimitiveType.Cylinder, "Baluster", balcony, new Vector3(x, 4.8f, 21.05f), Vector3.zero, new Vector3(0.06f, 0.42f, 0.06f), matColumn)
                    .GetComponent<Collider>().enabled = false;
        }

        struct WindowParts { public Light light; public Renderer glass; }

        static WindowParts BuildWindow(Transform arch, Material shaftMat)
        {
            var w = Child("Window", arch);
            var glassMat = MenuAssetFactory.Emissive("Window_Glass", new Color(0.9f, 1f, 1.15f));
            var glass = Prim(PrimitiveType.Quad, "Glass", w, new Vector3(0f, 6.6f, 28.45f), Vector3.zero, new Vector3(3f, 3.2f, 1f), glassMat);
            glass.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            Object.DestroyImmediate(glass.GetComponent<Collider>());
            var archTop = ProceduralMeshes.Arch(0f, 1.5f, 0.05f, 20);
            MeshObject("GlassArch", w, archTop, new Vector3(0f, 8.2f, 28.47f), Quaternion.identity, new Vector3(1f, 0.5f, 1f), glassMat);
            // Parteluces
            Prim(PrimitiveType.Cube, "Mullion_V", w, new Vector3(0f, 6.6f, 28.3f), Vector3.zero, new Vector3(0.09f, 3.2f, 0.12f), matDark);
            Prim(PrimitiveType.Cube, "Mullion_H1", w, new Vector3(0f, 5.9f, 28.3f), Vector3.zero, new Vector3(3f, 0.07f, 0.12f), matDark);
            Prim(PrimitiveType.Cube, "Mullion_H2", w, new Vector3(0f, 7.3f, 28.3f), Vector3.zero, new Vector3(3f, 0.07f, 0.12f), matDark);

            // Luz fría del ventanal, con sombras (1 de 2 luces con sombra)
            var lightGo = new GameObject("WindowLight");
            lightGo.transform.SetParent(lightingRoot, false);
            lightGo.transform.position = new Vector3(0f, 7.6f, 31f);
            lightGo.transform.LookAt(new Vector3(0f, 0f, 13f));
            var l = lightGo.AddComponent<Light>();
            l.type = LightType.Spot;
            l.color = MoonColor;
            l.intensity = 60f;
            l.range = 32f;
            l.spotAngle = 48f;
            l.innerSpotAngle = 20f;
            l.shadows = LightShadows.Soft;
            l.shadowStrength = 0.9f;

            // Haces de luz falsos (dos planos cruzados con textura degradada)
            var shafts = Child("LightShafts", atmosphereRoot);
            lowQualityObjects.Add(shafts.gameObject);
            Vector3 from = new(0f, 7f, 28.3f), to = new(0f, 0f, 15f);
            Vector3 mid = (from + to) * 0.5f;
            float len = Vector3.Distance(from, to);
            for (int i = 0; i < 2; i++)
            {
                var q = Prim(PrimitiveType.Quad, "Shaft_" + i, shafts, mid, Vector3.zero, new Vector3(3.2f, len, 1f), shaftMat);
                Object.DestroyImmediate(q.GetComponent<Collider>());
                q.transform.rotation = Quaternion.LookRotation(to - from) * Quaternion.Euler(90f, i * 90f, 0f);
                q.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            }
            return new WindowParts { light = l, glass = glass.GetComponent<Renderer>() };
        }

        static void BuildStaticFurniture()
        {
            var furniture = Child("Furniture", env);
            var benchMat = MenuAssetFactory.Silhouette("SIL_Bench", "painted_wooden_bench", 0.7f, RimSoft);
            Model("painted_wooden_bench", furniture, new Vector3(-0.3f, 0f, 10.5f), 90f, 0.5f, benchMat, "Bench_A", width: 1.8f);
            Model("painted_wooden_bench", furniture, new Vector3(0.4f, 0f, 15.5f), 90f, 0.5f, benchMat, "Bench_B", width: 1.8f);

            // Postes con cordón de terciopelo a ambos lados del pasillo central
            foreach (float x in new[] { -2.3f, 2.3f })
            {
                Vector3 prev = default;
                for (int i = 0; i < 5; i++)
                {
                    Vector3 p = new(x, 0f, 6f + i * 2.6f);
                    Stanchion(furniture, p);
                    if (i > 0) Rope(furniture, prev + Vector3.up * 0.95f, p + Vector3.up * 0.95f);
                    prev = p;
                }
            }
        }

        static void Stanchion(Transform parent, Vector3 p)
        {
            var s = Child("Stanchion", parent);
            Prim(PrimitiveType.Cylinder, "Post", s, p + Vector3.up * 0.5f, Vector3.zero, new Vector3(0.05f, 0.5f, 0.05f), matBronze);
            Prim(PrimitiveType.Cylinder, "Base", s, p + Vector3.up * 0.015f, Vector3.zero, new Vector3(0.28f, 0.015f, 0.28f), matBronze);
            Prim(PrimitiveType.Sphere, "Top", s, p + Vector3.up * 1.0f, Vector3.zero, Vector3.one * 0.08f, matBronze);
        }

        static void Rope(Transform parent, Vector3 a, Vector3 b)
        {
            var go = new GameObject("VelvetRope");
            go.transform.SetParent(parent, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.sharedMaterial = matVelvet;
            lr.widthMultiplier = 0.035f;
            lr.numCapVertices = 2;
            lr.textureMode = LineTextureMode.Tile;
            const int n = 14;
            lr.positionCount = n;
            for (int i = 0; i < n; i++)
            {
                float t = i / (n - 1f);
                lr.SetPosition(i, Vector3.Lerp(a, b, t) + Vector3.down * (Mathf.Sin(t * Mathf.PI) * 0.22f));
            }
        }

        // ============================================================================
        // Piezas asociadas a los botones
        // ============================================================================

        static MuseumExhibit[] BuildExhibits()
        {
            var result = new MuseumExhibit[5];

            // --- Vitrina con la figura de orfebrería (Iniciar experiencia) ---
            var vit = Child("Vitrine_Tunjo", exhibitsRoot);
            vit.position = new Vector3(2.45f, 0f, 3.4f);
            vit.rotation = Quaternion.Euler(0f, 34f, 0f);
            Prim(PrimitiveType.Cube, "Pedestal", vit, vit.TransformPoint(new Vector3(0f, 0.5f, 0f)), vit.eulerAngles, new Vector3(0.75f, 1f, 0.75f), matPedestal);
            Prim(PrimitiveType.Cube, "Cap", vit, vit.TransformPoint(new Vector3(0f, 2.06f, 0f)), vit.eulerAngles, new Vector3(0.78f, 0.08f, 0.78f), matPedestal);
            var glassMat = MenuAssetFactory.Glass("Vitrine_Glass");
            var glass = Prim(PrimitiveType.Cube, "Glass", vit, vit.TransformPoint(new Vector3(0f, 1.51f, 0f)), vit.eulerAngles, new Vector3(0.72f, 1.02f, 0.72f), glassMat);
            glass.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            var goldMat = MenuAssetFactory.Silhouette("SIL_Tunjo_Gold", null, 0.35f, new Color(0.6f, 0.58f, 0.5f),
                new Color(0.75f, 0.62f, 0.35f), 0.7f, 1f);
            var tunjo = MeshObject("Tunjo", vit, ProceduralMeshes.Tunjo(), vit.TransformPoint(new Vector3(0f, 1.02f, 0f)),
                vit.rotation, Vector3.one * 1.55f, goldMat);
            var topLight = SpotLight("Vitrine_TopLight", vit, vit.TransformPoint(new Vector3(0f, 2.0f, 0f)), Quaternion.Euler(90f, 0f, 0f),
                new Color(1f, 0.97f, 0.9f), 14f, 3f, 55f, LightShadows.Soft);   // 2 de 2 luces con sombra
            var focusVit = Child("Focus", vit);
            focusVit.position = tunjo.transform.position + Vector3.up * 0.3f;
            result[0] = vit.gameObject.AddComponent<MuseumExhibit>();
            result[0].Configure(ExhibitAnimation.VitrineSweep, new[] { topLight }, focusVit);
            result[0].SetAnimationRefs(glassRenderer: glass.GetComponent<Renderer>());
            Ambient(vit, MenuSound.Breath);

            // --- Cuadro de La Llorona y espejo antiguo (Calibración) ---
            var paintingRoot = Child("Painting_Llorona", exhibitsRoot);
            paintingRoot.position = new Vector3(-3.3f, 0f, 5.2f);
            paintingRoot.rotation = Quaternion.Euler(0f, -32f, 0f);
            Prim(PrimitiveType.Cube, "Partition", paintingRoot, paintingRoot.TransformPoint(new Vector3(0f, 2.5f, 0.2f)), paintingRoot.eulerAngles,
                new Vector3(3.4f, 5f, 0.25f), matWall);
            // Los modelos descargados miran hacia +Z: se giran 180° para que den la cara a la cámara
            var frameMat = MenuAssetFactory.Silhouette("SIL_Frame_Gold", "fancy_picture_frame_02", 0.45f, RimCold, new Color(0.8f, 0.7f, 0.5f), 0.5f, 0.6f);
            var frame = Model("fancy_picture_frame_02", paintingRoot, paintingRoot.TransformPoint(new Vector3(0f, 0.75f, 0f)), paintingRoot.eulerAngles.y + 180f, 3.3f, frameMat, "Frame");
            var inst = frame.transform.GetChild(0);
            inst.localScale = Vector3.Scale(inst.localScale, new Vector3(0.62f, 1f, 1f));   // formato vertical para el retrato
            var paintTex = AssetDatabase.LoadAssetAtPath<Texture2D>(MenuTextureFactory.Folder + "/Painting_Llorona.png");
            var canvasMat = MenuAssetFactory.Lit("Painting_Canvas", new Color(0.9f, 0.9f, 0.9f), 0.25f, 0f, paintTex);
            Renderer canvasRenderer = null;
            foreach (var r in frame.GetComponentsInChildren<Renderer>())
                if (r.name.EndsWith("_canvas")) { r.sharedMaterial = canvasMat; canvasRenderer = r; }
            var paintCenter = canvasRenderer != null ? canvasRenderer.bounds.center : WorldBounds(frame).center;
            var paintLight = SpotLight("Painting_Light", paintingRoot, paintingRoot.TransformPoint(new Vector3(0f, 4.6f, -1.6f)), Quaternion.identity,
                CandleColor, 16f, 7f, 45f, LightShadows.None);
            paintLight.transform.LookAt(paintCenter);

            var mirrorRoot = Child("Mirror_Antique", exhibitsRoot);
            mirrorRoot.position = new Vector3(-4.62f, 0.75f, 13.6f);
            mirrorRoot.rotation = Quaternion.Euler(0f, -18f, 0f);
            var mirrorFrameMat = MenuAssetFactory.Silhouette("SIL_Mirror", "ornate_mirror_01", 0.5f, RimCold, null, 0.6f, 0.3f);
            var mirror = Model("ornate_mirror_01", mirrorRoot, mirrorRoot.position, mirrorRoot.eulerAngles.y + 180f, 1.7f, mirrorFrameMat, "MirrorFrame");
            var mb = WorldBounds(mirror);
            var mirrorGlassMat = MenuAssetFactory.Lit("Mirror_Glass", new Color(0.05f, 0.05f, 0.055f), 0.97f, 1f);
            var mirrorGlass = Prim(PrimitiveType.Quad, "MirrorGlass", mirrorRoot, mb.center + mirrorRoot.forward * -0.035f, mirrorRoot.eulerAngles,
                new Vector3(Mathf.Max(mb.size.x, mb.size.z) * 0.55f, mb.size.y * 0.7f, 1f), mirrorGlassMat);
            Object.DestroyImmediate(mirrorGlass.GetComponent<Collider>());
            var ghostMat = MenuAssetFactory.Silhouette("SIL_Ghost", "gothic_statue", 1f, new Color(0.7f, 0.74f, 0.8f));
            var ghost = Model("gothic_statue", mirrorRoot, mirrorGlass.transform.position + mirrorRoot.forward * -0.03f - Vector3.up * mb.size.y * 0.32f,
                mirrorRoot.eulerAngles.y + 180f, mb.size.y * 0.62f, ghostMat, "Ghost");
            ghost.transform.localScale = new Vector3(1f, 1f, 0.06f);
            var mirrorLight = SpotLight("Mirror_Light", mirrorRoot, mb.center + mirrorRoot.forward * -2.2f + Vector3.up * 1.6f, Quaternion.identity,
                CandleColor, 4f, 5f, 35f, LightShadows.None);
            mirrorLight.transform.LookAt(mb.center);
            var focusMirror = Child("Focus", mirrorRoot);
            focusMirror.position = mb.center;
            result[1] = mirrorRoot.gameObject.AddComponent<MuseumExhibit>();
            result[1].Configure(ExhibitAnimation.MirrorGhost, new[] { mirrorLight, paintLight }, focusMirror);
            result[1].SetAnimationRefs(ghostObject: ghost);
            Ambient(paintingRoot, MenuSound.Breath);

            // --- Placa informativa de bronce (Controles) ---
            var plaqueRoot = Child("Plaque_Bronze", exhibitsRoot);
            plaqueRoot.position = new Vector3(1.35f, 0f, 2.6f);
            plaqueRoot.rotation = Quaternion.Euler(0f, 25f, 0f);
            Prim(PrimitiveType.Cube, "Stand", plaqueRoot, plaqueRoot.TransformPoint(new Vector3(0f, 0.5f, 0f)), plaqueRoot.eulerAngles, new Vector3(0.08f, 1f, 0.08f), matBronze);
            var plaque = Prim(PrimitiveType.Cube, "Plaque", plaqueRoot, plaqueRoot.TransformPoint(new Vector3(0f, 1.05f, 0f)),
                plaqueRoot.eulerAngles + new Vector3(35f, 0f, 0f), new Vector3(0.5f, 0.34f, 0.025f), matBronze);
            var plaqueText = new GameObject("Engraving").AddComponent<TextMeshPro>();
            plaqueText.transform.SetParent(plaque.transform, false);
            plaqueText.transform.localPosition = new Vector3(0f, 0f, -0.6f);
            plaqueText.transform.localScale = new Vector3(1f / 0.5f, 1f / 0.34f, 1f) * 0.1f;
            plaqueText.rectTransform.sizeDelta = new Vector2(4.4f, 3f);
            plaqueText.font = fontCaps;
            plaqueText.fontSize = 2.3f;
            plaqueText.alignment = TextAlignmentOptions.Center;
            plaqueText.color = new Color(1f, 0.96f, 0.86f);
            plaqueText.text = "TUNJO VOTIVO\n<size=70%>Orfebrería muisca\nAltiplano cundiboyacense\nOfrenda de oro a los dioses</size>";
            var plaqueLight = SpotLight("Plaque_Light", plaqueRoot, plaqueRoot.TransformPoint(new Vector3(0f, 2.2f, -0.9f)), Quaternion.identity,
                CandleColor, 1.6f, 4f, 30f, LightShadows.None);
            plaqueLight.transform.LookAt(plaque.transform.position);
            result[2] = plaqueRoot.gameObject.AddComponent<MuseumExhibit>();
            result[2].Configure(ExhibitAnimation.PlaqueReveal, new[] { plaqueLight }, plaque.transform);
            result[2].SetAnimationRefs(plaque: plaqueText);

            // --- Libro de visitantes sobre un atril (Créditos) ---
            var lectern = Child("Guestbook_Lectern", exhibitsRoot);
            lectern.position = new Vector3(1.4f, 0f, 8.2f);
            lectern.rotation = Quaternion.Euler(0f, 8f, 0f);
            Prim(PrimitiveType.Cube, "Column", lectern, lectern.TransformPoint(new Vector3(0f, 0.55f, 0f)), lectern.eulerAngles, new Vector3(0.12f, 1.1f, 0.12f), matWood);
            Prim(PrimitiveType.Cube, "Foot", lectern, lectern.TransformPoint(new Vector3(0f, 0.03f, 0f)), lectern.eulerAngles, new Vector3(0.5f, 0.06f, 0.4f), matWood);
            var top = Child("Top", lectern);
            top.position = lectern.TransformPoint(new Vector3(0f, 1.12f, 0f));
            top.rotation = lectern.rotation * Quaternion.Euler(-22f, 0f, 0f);
            Prim(PrimitiveType.Cube, "Desk", top, top.position, top.eulerAngles, new Vector3(0.6f, 0.04f, 0.42f), matWood);
            var paper = AssetDatabase.LoadAssetAtPath<Material>(MenuAssetFactory.MatFolder + "/Paper.mat");
            Prim(PrimitiveType.Cube, "Page_L", top, top.TransformPoint(new Vector3(-0.13f, 0.03f, 0f)), top.eulerAngles + new Vector3(0, 0, 4f), new Vector3(0.25f, 0.015f, 0.34f), paper);
            Prim(PrimitiveType.Cube, "Page_R", top, top.TransformPoint(new Vector3(0.13f, 0.03f, 0f)), top.eulerAngles + new Vector3(0, 0, -4f), new Vector3(0.25f, 0.015f, 0.34f), paper);
            var pagePivot = Child("TurningPage", top);
            pagePivot.position = top.TransformPoint(new Vector3(0f, 0.042f, 0f));
            pagePivot.rotation = top.rotation;
            var page = Prim(PrimitiveType.Cube, "Page", pagePivot, pagePivot.TransformPoint(new Vector3(0.125f, 0f, 0f)), pagePivot.eulerAngles, new Vector3(0.25f, 0.003f, 0.33f), paper);
            Object.DestroyImmediate(page.GetComponent<Collider>());
            var bookLight = SpotLight("Guestbook_Light", lectern, lectern.TransformPoint(new Vector3(0f, 3f, -1f)), Quaternion.identity,
                CandleColor, 4f, 5f, 30f, LightShadows.None);
            bookLight.transform.LookAt(top.position);
            result[3] = lectern.gameObject.AddComponent<MuseumExhibit>();
            result[3].Configure(ExhibitAnimation.GuestbookPage, new[] { bookLight }, top);
            result[3].SetAnimationRefs(pageTransform: pagePivot);

            // --- Puerta lateral entreabierta (Salir) ---
            var doorRoot = Child("Door_Side", exhibitsRoot);
            doorRoot.position = new Vector3(7.15f, 0f, 13.35f);
            doorRoot.rotation = Quaternion.Euler(0f, -90f, 0f);
            var doorMat = MenuAssetFactory.Silhouette("SIL_Door", "large_castle_door", 0.6f, RimSoft);
            var door = Model("large_castle_door", doorRoot, doorRoot.position, -90f, 3.5f, doorMat, "Door", width: 1.85f);
            // Hoja derecha entreabierta: gira sobre su bisagra hacia el salón
            foreach (var r in door.GetComponentsInChildren<Renderer>())
            {
                if (!r.name.EndsWith("_right")) continue;
                var lb = r.bounds;
                r.transform.RotateAround(new Vector3(lb.center.x, 0f, lb.max.z), Vector3.up, 14f);
            }
            var slitMat = MenuAssetFactory.Emissive("Door_Slit", new Color(2.6f, 2.5f, 2.3f));
            var slit = Prim(PrimitiveType.Quad, "LightSlit", doorRoot, new Vector3(7.35f, 1.75f, 14.32f), new Vector3(0f, 90f, 0f), new Vector3(0.09f, 3.4f, 1f), slitMat);
            Object.DestroyImmediate(slit.GetComponent<Collider>());
            var doorLight = SpotLight("Door_Light", doorRoot, new Vector3(8.6f, 1.6f, 14.3f), Quaternion.Euler(0f, -90f, 0f), CandleColor, 8f, 9f, 26f, LightShadows.None);
            result[4] = doorRoot.gameObject.AddComponent<MuseumExhibit>();
            var focusDoor = Child("Focus", doorRoot);
            focusDoor.position = new Vector3(7f, 1.7f, 13.4f);
            result[4].Configure(ExhibitAnimation.DoorClose, new[] { doorLight }, focusDoor);
            result[4].SetAnimationRefs(slit: slit.transform, slitLight: doorLight);
            Ambient(doorRoot, MenuSound.Hum);

            return result;
        }

        // ============================================================================
        // Piezas desvanecibles
        // ============================================================================

        static FadeableProp[] BuildFadeables(SparkleEmitter sparkles)
        {
            var list = new List<FadeableProp>();
            var statueMat = MenuAssetFactory.Silhouette("SIL_Statue", "gothic_statue", 0.92f, RimCold);
            var bustMat = MenuAssetFactory.Silhouette("SIL_Bust", "marble_bust_01", 0.82f, RimCold);
            var horseMat = MenuAssetFactory.Silhouette("SIL_Horse", "horse_statue_01", 0.85f, RimCold);
            var lionMat = MenuAssetFactory.Silhouette("SIL_Lion", "lion_head", 0.85f, RimSoft);
            var smallFrameMat = MenuAssetFactory.Silhouette("SIL_FrameSmall", "fancy_picture_frame_01", 0.8f, RimSoft);
            var smallCanvasMat = MenuAssetFactory.Silhouette("SIL_CanvasSmall", null, 0.55f, RimSoft);
            smallCanvasMat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(MenuTextureFactory.Folder + "/Painting_Llorona.png"));
            var vaseMat = MenuAssetFactory.Silhouette("SIL_Vase", "antique_ceramic_vase_01", 0.85f, RimSoft);
            var cabinetMat = MenuAssetFactory.Silhouette("SIL_Cabinet", "GothicCabinet_01", 0.88f, RimSoft);
            var clockMat = MenuAssetFactory.Silhouette("SIL_Clock", "vintage_grandfather_clock_01", 0.88f, RimSoft);
            var chairMat = MenuAssetFactory.Silhouette("SIL_Chair", "ArmChair_01", 0.88f, RimSoft);
            var chandelierMat = MenuAssetFactory.Silhouette("SIL_Chandelier", "Chandelier_01", 0.8f, RimSoft, null, 0.5f, 0.8f);
            var pedestalSil = MenuAssetFactory.Silhouette("SIL_Pedestal", null, 0.75f, RimSoft, new Color(0.35f, 0.35f, 0.35f));

            // Figura del balcón, a contraluz del ventanal
            list.Add(Fade(Model("gothic_statue", fadeRoot, new Vector3(0.9f, 4.3f, 22.3f), 180f, 1.8f, statueMat, "Figure_Balcony"),
                sparkles, new Vector3(-0.7f, 0f, 6.8f), new Vector3(1.1f, 0f, 9.5f)));
            // Figura al fondo del pasillo
            list.Add(Fade(Model("gothic_statue", fadeRoot, new Vector3(0f, 0f, 31.2f), 180f, 1.85f, statueMat, "Figure_Corridor"),
                sparkles, new Vector3(0.2f, 0f, 19f), new Vector3(-0.4f, 0f, 24f)));
            // Busto sobre pedestal
            list.Add(Fade(OnPedestal("marble_bust_01", new Vector3(-4.1f, 0f, 17f), 160f, 0.65f, bustMat, pedestalSil, "Bust_Pedestal"),
                sparkles, new Vector3(-3.4f, 0f, 11.5f)));
            list.Add(Fade(OnPedestal("horse_statue_01", new Vector3(4.1f, 0f, 18f), 220f, 0.9f, horseMat, pedestalSil, "Horse_Pedestal"),
                sparkles, new Vector3(3.6f, 0f, 11.8f)));
            list.Add(Fade(OnPedestal("antique_ceramic_vase_01", new Vector3(-2.9f, 0f, 22f), 0f, 0.7f, vaseMat, pedestalSil, "Vase_Pedestal"),
                sparkles, new Vector3(-2.6f, 0f, 15.5f)));
            // Cabeza de león en el muro izquierdo
            list.Add(Fade(Model("lion_head", fadeRoot, new Vector3(-6.95f, 2.8f, 19.5f), 90f, 0.7f, lionMat, "LionHead_Wall"),
                sparkles, new Vector3(-6.95f, 2.6f, 11.2f)));
            // Cuadros pequeños
            var frameL = Model("fancy_picture_frame_01", fadeRoot, new Vector3(-6.95f, 1.6f, 9.3f), 90f, 1.1f, smallFrameMat, "Frame_Small_L");
            var frameR = Model("fancy_picture_frame_01", fadeRoot, new Vector3(6.95f, 1.7f, 19.5f), -90f, 1.2f, smallFrameMat, "Frame_Small_R");
            foreach (var r in frameL.GetComponentsInChildren<Renderer>().Concat(frameR.GetComponentsInChildren<Renderer>()))
                if (r.name.EndsWith("_canvas")) r.sharedMaterial = smallCanvasMat;
            list.Add(Fade(frameL, sparkles, new Vector3(-6.95f, 1.5f, 6.2f)));
            list.Add(Fade(frameR, sparkles, new Vector3(6.95f, 1.6f, 8.4f)));
            // Muebles y candelabro
            list.Add(Fade(Model("GothicCabinet_01", fadeRoot, new Vector3(6.35f, 0f, 24.2f), -90f, 2.2f, cabinetMat, "Cabinet"),
                sparkles, new Vector3(6.3f, 0f, 16.8f)));
            list.Add(Fade(Model("vintage_grandfather_clock_01", fadeRoot, new Vector3(-6.45f, 0f, 24.5f), 90f, 2.3f, clockMat, "GrandfatherClock"),
                sparkles, new Vector3(-6.4f, 0f, 16.5f)));
            list.Add(Fade(Model("ArmChair_01", fadeRoot, new Vector3(-1.9f, 0f, 19.8f), 155f, 1.0f, chairMat, "ArmChair"),
                sparkles, new Vector3(-1.4f, 0f, 12.6f)));
            list.Add(Fade(Model("Chandelier_01", fadeRoot, new Vector3(0f, 6.2f, 16f), 0f, 1.6f, chandelierMat, "Chandelier"),
                sparkles, new Vector3(0f, 5.8f, 11.5f)));

            foreach (var p in list) Ambient(p.transform, p.name.Contains("Clock") ? MenuSound.Hum : MenuSound.Drip);
            return list.ToArray();
        }

        static GameObject OnPedestal(string id, Vector3 pos, float yaw, float height, Material mat, Material pedestalMat, string name)
        {
            var root = new GameObject(name);
            root.transform.SetParent(fadeRoot, false);
            root.transform.position = pos;
            var ped = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ped.name = "Pedestal";
            ped.transform.SetParent(root.transform, false);
            ped.transform.localPosition = new Vector3(0f, 0.55f, 0f);
            ped.transform.localScale = new Vector3(0.6f, 1.1f, 0.6f);
            ped.GetComponent<Renderer>().sharedMaterial = pedestalMat;
            var m = Model(id, root.transform, pos + Vector3.up * 1.1f, yaw, height, mat, id);
            m.transform.SetParent(root.transform, true);
            return root;
        }

        static FadeableProp Fade(GameObject go, SparkleEmitter sparkles, params Vector3[] anchorPositions)
        {
            var anchors = new Transform[anchorPositions.Length];
            for (int i = 0; i < anchors.Length; i++)
            {
                var a = new GameObject($"{go.name}_Anchor{i}").transform;
                a.SetParent(anchorsRoot, false);
                Vector3 p = anchorPositions[i];
                // Conserva la altura del objeto si el ancla está en el piso y el objeto no
                if (Mathf.Approximately(p.y, 0f) && go.transform.position.y > 0.5f && go.transform.position.y < 4f) p.y = go.transform.position.y;
                a.position = p;
                // Los objetos de pared conservan su orientación; el resto gira un poco al reaparecer
                bool wall = Mathf.Abs(p.x) > 6.5f;
                a.rotation = wall ? go.transform.rotation : Quaternion.Euler(0f, go.transform.eulerAngles.y + Random.Range(-15f, 15f), 0f);
                anchors[i] = a;
            }
            var prop = go.AddComponent<FadeableProp>();
            prop.Configure(go.GetComponentsInChildren<Renderer>(), anchors, sparkles);
            // Todas las piezas desvanecibles comparten el shader de silueta y disolución
            foreach (var r in go.GetComponentsInChildren<Renderer>())
                if (r.sharedMaterial != null && r.sharedMaterial.shader.name != "CuandoNadieMira/SilhouetteDissolve")
                    r.sharedMaterial = MenuAssetFactory.Silhouette("SIL_Pedestal", null, 0.75f, RimSoft, new Color(0.35f, 0.35f, 0.35f));
            return prop;
        }

        // ============================================================================
        // Velas, atmósfera y sonido
        // ============================================================================

        static void BuildCandles()
        {
            var holderMat = MenuAssetFactory.Silhouette("SIL_Candlestick", "wooden_candlestick", 0.75f, RimSoft, null, 0.4f, 0.5f);
            var waxMat = MenuAssetFactory.Lit("Candle_Wax", new Color(0.75f, 0.74f, 0.7f), 0.3f);
            var root = Child("Candles", lightingRoot);
            Vector3[] spots =
            {
                new(-1.5f, 0f, 3.4f), new(0.9f, 0f, 4.6f), new(-4.6f, 0f, 6.8f), new(4.6f, 0f, 6.6f),
                new(-2.5f, 0f, 12.2f), new(2.9f, 0f, 13.4f), new(-1.3f, 0f, 21.5f), new(1.6f, 0f, 21.2f),
                new(-3f, 5.3f, 21.05f), new(3.2f, 5.3f, 21.05f),
            };
            for (int i = 0; i < spots.Length; i++)
            {
                var c = Child("Candle_" + i, root);
                c.position = spots[i];
                float baseY = spots[i].y;
                if (baseY < 0.1f)
                {
                    Prim(PrimitiveType.Cylinder, "Stand", c, spots[i] + Vector3.up * 0.6f, Vector3.zero, new Vector3(0.06f, 0.6f, 0.06f), matBronze);
                    Prim(PrimitiveType.Cylinder, "Foot", c, spots[i] + Vector3.up * 0.02f, Vector3.zero, new Vector3(0.3f, 0.02f, 0.3f), matBronze);
                    baseY = 1.2f;
                }
                var holder = Model("wooden_candlestick", c, new Vector3(spots[i].x, baseY, spots[i].z), 0f, 0.32f, holderMat, "Holder");
                float topY = WorldBounds(holder).max.y;
                var wax = Prim(PrimitiveType.Cylinder, "Wax", c, new Vector3(spots[i].x, topY + 0.08f, spots[i].z), Vector3.zero, new Vector3(0.045f, 0.08f, 0.045f), waxMat);
                Object.DestroyImmediate(wax.GetComponent<Collider>());
                var flame = Prim(PrimitiveType.Sphere, "Flame", c, new Vector3(spots[i].x, topY + 0.2f, spots[i].z), Vector3.zero, new Vector3(0.022f, 0.055f, 0.022f), matFlame);
                Object.DestroyImmediate(flame.GetComponent<Collider>());
                flame.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
                var lightGo = new GameObject("Light");
                lightGo.transform.SetParent(c, false);
                lightGo.transform.position = flame.transform.position + Vector3.up * 0.05f;
                var l = lightGo.AddComponent<Light>();
                l.type = LightType.Point;
                l.color = CandleColor;
                l.range = 4.8f;
                l.intensity = 3.2f;
                l.shadows = LightShadows.None;
                var candle = c.gameObject.AddComponent<MenuCandle>();
                candle.Configure(l, flame.transform);
                candles.Add(candle);
            }

            // Luz tenue en el pasillo y sonda de reflejos para el mármol
            var corridor = new GameObject("CorridorLight").AddComponent<Light>();
            corridor.transform.SetParent(lightingRoot, false);
            corridor.transform.position = new Vector3(0f, 3.2f, 33.5f);
            corridor.type = LightType.Point;
            corridor.range = 6f;
            corridor.intensity = 2.2f;
            corridor.color = MoonColor;

            var probeGo = new GameObject("ReflectionProbe");
            probeGo.transform.SetParent(lightingRoot, false);
            probeGo.transform.position = new Vector3(0f, 2f, 12f);
            var probe = probeGo.AddComponent<ReflectionProbe>();
            probe.mode = ReflectionProbeMode.Realtime;
            probe.refreshMode = ReflectionProbeRefreshMode.OnAwake;
            probe.timeSlicingMode = ReflectionProbeTimeSlicingMode.IndividualFaces;
            probe.size = new Vector3(14f, 9f, 34f);
            probe.center = new Vector3(0f, 2.5f, 0f);
            probe.boxProjection = true;
            probe.resolution = 128;
            probe.intensity = 1f;
            lowQualityObjects.Add(probeGo);
        }

        static void BuildAtmosphere(Material fogMat, Material dustMat)
        {
            // Niebla baja: planos con ruido que se desplaza
            var fog = Child("LowFog", atmosphereRoot);
            lowQualityObjects.Add(fog.gameObject);
            for (int i = 0; i < 3; i++)
            {
                var q = Prim(PrimitiveType.Quad, "FogLayer_" + i, fog, new Vector3(0f, 0.12f + i * 0.14f, 12f), new Vector3(90f, 0f, 0f), new Vector3(14f, 30f, 1f), fogMat);
                Object.DestroyImmediate(q.GetComponent<Collider>());
                q.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
                var scroll = q.AddComponent<UVScroller>();
                SetField(scroll, "speed", new Vector2(0.006f * (i + 1), 0.003f * (i % 2 == 0 ? 1 : -1)));
                SetField(scroll, "tiling", new Vector2(2f + i, 4f + i));
            }

            // Polvo suspendido en el haz del ventanal
            var dustGo = new GameObject("Dust");
            dustGo.transform.SetParent(atmosphereRoot, false);
            dustGo.transform.position = new Vector3(0f, 3.5f, 18f);
            lowQualityObjects.Add(dustGo);
            var ps = dustGo.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(8f, 12f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.008f, 0.02f);
            main.startSpeed = 0.02f;
            main.maxParticles = 180;
            main.prewarm = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var emission = ps.emission;
            emission.rateOverTime = 16f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(6f, 7f, 14f);
            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.05f;
            noise.frequency = 0.3f;
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.3f), new GradientAlphaKey(1f, 0.7f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
            var r = dustGo.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = dustMat;
            r.shadowCastingMode = ShadowCastingMode.Off;
        }

        static void Ambient(Transform t, MenuSound sound)
        {
            var go = new GameObject("AmbientSound");
            go.transform.SetParent(t, false);
            go.AddComponent<AudioSource>();
            go.AddComponent<AmbientSound>().Configure(sound, null);
        }

        // ============================================================================
        // Cámara
        // ============================================================================

        static Camera CreateCamera(Transform rig, out Camera uiCam)
        {
            var camGo = new GameObject("MainCamera") { tag = "MainCamera" };
            camGo.transform.SetParent(rig, false);
            var cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = 52f;
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 80f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            cam.cullingMask = ~(1 << UILayer);
            camGo.AddComponent<AudioListener>();
            var data = camGo.AddComponent<UniversalAdditionalCameraData>();
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.FastApproximateAntialiasing;

            // Cámara superpuesta solo para la UI: queda nítida sobre la profundidad de campo
            var uiGo = new GameObject("UICamera");
            uiGo.transform.SetParent(camGo.transform, false);
            uiCam = uiGo.AddComponent<Camera>();
            uiCam.fieldOfView = cam.fieldOfView;
            uiCam.nearClipPlane = 0.05f;
            uiCam.farClipPlane = 10f;
            uiCam.cullingMask = 1 << UILayer;
            var uiData = uiGo.AddComponent<UniversalAdditionalCameraData>();
            uiData.renderType = CameraRenderType.Overlay;
            uiData.renderPostProcessing = false;
            data.cameraStack.Add(uiCam);
            return cam;
        }

        // ============================================================================
        // UI: placa en World Space
        // ============================================================================

        struct Panels
        {
            public GameObject controls, accessibility, credits, exit;
            public RectTransform creditsContent;
            public MenuButton[] controlsButtons, accessibilityButtons, creditsButtons, exitButtons, homeButtons;
            public MenuButton accessibilityBack;
            public TMP_Text optionHelp;
        }

        static Transform BuildWorldPlate(Transform uiRoot, Transform rig, Camera uiCam, Material titleMat, Dictionary<string, Sprite> icons,
            Material emberMat, MuseumExhibit[] exhibits, out CanvasGroup front, out CanvasGroup back, out MenuButton[] mainButtons,
            out TMP_Text message, out TMP_Text[] titleLines, out Transform branches, out Panels panels)
        {
            var plateGo = new GameObject("Canvas_WorldPlate", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
            plateGo.layer = UILayer;
            plateGo.transform.SetParent(uiRoot, false);
            var canvas = plateGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = uiCam;
            var rt = (RectTransform)plateGo.transform;
            rt.sizeDelta = new Vector2(1000f, 1300f);
            rt.localScale = Vector3.one * 0.001f;
            rt.position = rig.position + rig.rotation * new Vector3(-0.74f, 0.06f, 1.7f);
            rt.rotation = rig.rotation * Quaternion.Euler(0f, -4f, 0f);

            front = Group("Front", rt);
            back = Group("Back", rt);
            back.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);

            // Título en dos líneas con ramas secas que brotan de la O
            var t1 = Text("Title_Cuando", front.transform, fontTitle, "CUANDO", 150f, new Vector2(-480f, 545f), new Vector2(960f, 160f));
            var t2 = Text("Title_NadieMira", front.transform, fontTitle, "NADIE MIRA", 150f, new Vector2(-480f, 405f), new Vector2(960f, 160f));
            foreach (var t in new[] { t1, t2 })
            {
                t.fontSharedMaterial = titleMat;
                t.characterSpacing = -4f;
                t.horizontalMapping = TextureMappingOptions.Paragraph;
                t.verticalMapping = TextureMappingOptions.Paragraph;
            }
            titleLines = new[] { t1, t2 };
            branches = BuildBranches(t1);

            var sub = Text("Subtitle", front.transform, fontBody, "Museo interactivo de mitos y leyendas de Colombia", 38f, new Vector2(-474f, 300f), new Vector2(940f, 50f));
            sub.color = new Color(0.8f, 0.8f, 0.8f);
            var orn = Img("Ornament", front.transform, sprOrnament, new Vector2(-474f, 262f), new Vector2(560f, 18f), new Color(1f, 1f, 1f, 0.45f));
            orn.raycastTarget = false;

            string[] labels = { "Iniciar experiencia", "Calibración", "Controles", "Créditos", "Salir" };
            string[] iconNames = { "Eye", "Gear", "Gamepad", "Book", "Door" };
            mainButtons = new MenuButton[5];
            for (int i = 0; i < 5; i++)
                mainButtons[i] = Button(front.transform, labels[i], icons[iconNames[i]], i == 0 ? icons["Chevron"] : null,
                    new Vector2(-474f, 175f - i * 96f), new Vector2(620f, 80f), i == 0, emberMat, exhibits[i]);

            message = Text("PlateMessage", front.transform, fontBody, "", 32f, new Vector2(-474f, -330f), new Vector2(900f, 120f));
            message.fontStyle = FontStyles.Italic;
            message.color = new Color(0.85f, 0.85f, 0.85f);

            panels = BuildBackPanels(back.transform, icons, emberMat);
            return rt;
        }

        static Transform BuildBranches(TMP_Text title)
        {
            title.ForceMeshUpdate();
            var info = title.textInfo;
            int index = title.text.IndexOf('O');
            var ch = info.characterInfo[Mathf.Clamp(index, 0, info.characterCount - 1)];
            Vector3 top = new((ch.topLeft.x + ch.topRight.x) * 0.5f + 6f, ch.topLeft.y - 6f, 0f);

            var root = new GameObject("Branches");
            root.layer = UILayer;
            root.transform.SetParent(title.transform, false);
            root.transform.localPosition = top;
            var mat = MenuAssetFactory.Emissive("Title_Branches", new Color(0.8f, 0.8f, 0.8f));
            var rng = new System.Random(7);
            void Grow(Vector3 start, float angle, float length, int depth, float width)
            {
                if (depth == 0) return;
                var go = new GameObject("Branch");
                go.layer = UILayer;
                go.transform.SetParent(root.transform, false);
                var lr = go.AddComponent<LineRenderer>();
                lr.useWorldSpace = false;
                lr.sharedMaterial = mat;
                lr.shadowCastingMode = ShadowCastingMode.Off;
                lr.positionCount = 4;
                Vector3 dir = new(Mathf.Sin(angle * Mathf.Deg2Rad), Mathf.Cos(angle * Mathf.Deg2Rad), 0f);
                Vector3 side = new(dir.y, -dir.x, 0f);
                Vector3 end = start + dir * length;
                for (int i = 0; i < 4; i++)
                {
                    float t = i / 3f;
                    float wobble = (float)(rng.NextDouble() - 0.5) * length * 0.18f * Mathf.Sin(t * Mathf.PI);
                    lr.SetPosition(i, Vector3.Lerp(start, end, t) + side * wobble);
                }
                lr.widthCurve = AnimationCurve.Linear(0f, width, 1f, width * 0.45f);
                lr.widthMultiplier = 0.001f;
                int kids = depth > 2 ? 2 : (rng.NextDouble() > 0.4 ? 2 : 1);
                for (int k = 0; k < kids; k++)
                {
                    float spread = (k == 0 ? -1f : 1f) * (22f + (float)rng.NextDouble() * 22f);
                    float at = 0.55f + (float)rng.NextDouble() * 0.45f;
                    Grow(Vector3.Lerp(start, end, at), angle + spread, length * (0.55f + (float)rng.NextDouble() * 0.2f), depth - 1, width * 0.6f);
                }
            }
            Grow(Vector3.zero, 18f, 62f, 4, 3.2f);
            Grow(Vector3.zero, -25f, 40f, 3, 2.4f);
            return root.transform;
        }

        static MenuButton Button(Transform parent, string label, Sprite icon, Sprite chevron, Vector2 pos, Vector2 size, bool blink,
            Material emberMat, MuseumExhibit exhibit, float fontSize = 40f)
        {
            var go = new GameObject("Btn_" + label.Split(':')[0].Replace(" ", ""), typeof(RectTransform), typeof(Image));
            go.layer = UILayer;
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            Place(rt, pos, size);
            var bg = go.GetComponent<Image>();
            bg.sprite = sprFill;
            bg.type = Image.Type.Sliced;
            bg.color = new Color(0f, 0f, 0f, 0.42f);

            var border = Img("Border", rt, sprOutline, Vector2.zero, size, new Color(1f, 1f, 1f, 0.4f));
            border.type = Image.Type.Sliced;
            border.raycastTarget = false;

            var content = new GameObject("Content", typeof(RectTransform));
            content.layer = UILayer;
            var crt = (RectTransform)content.transform;
            crt.SetParent(rt, false);
            Place(crt, Vector2.zero, size);

            Image iconImg = null;
            if (icon != null)
            {
                iconImg = Img("Icon", crt, icon, new Vector2(22f, 0f), new Vector2(46f, 46f), new Color(1f, 1f, 1f, 0.92f));
                iconImg.raycastTarget = false;
            }
            var text = Text("Label", crt, fontBody, label, fontSize, new Vector2(icon != null ? 92f : 30f, 0f), new Vector2(size.x - 130f, size.y));
            text.alignment = TextAlignmentOptions.MidlineLeft;
            text.fontWeight = FontWeight.SemiBold;
            text.color = new Color(0.93f, 0.93f, 0.93f);

            if (chevron != null)
            {
                var ch = Img("Chevron", rt, chevron, new Vector2(size.x - 52f, 0f), new Vector2(36f, 36f), new Color(1f, 1f, 1f, 0.85f));
                ch.raycastTarget = false;
            }

            var orn = Img("Ornament", rt, sprOrnament, new Vector2(icon != null ? 92f : 30f, -size.y * 0.5f + 12f), new Vector2(size.x - 150f, 12f), new Color(1f, 1f, 1f, 0.75f));
            orn.type = Image.Type.Filled;
            orn.fillMethod = Image.FillMethod.Horizontal;
            orn.fillOrigin = 0;
            orn.fillAmount = 0f;
            orn.raycastTarget = false;

            // Brasas blancas que suben desde el borde inferior
            var emberGo = new GameObject("Embers");
            emberGo.layer = UILayer;
            emberGo.transform.SetParent(rt, false);
            emberGo.transform.localPosition = new Vector3(size.x * 0.5f, -size.y * 0.5f, -2f);
            var ps = emberGo.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.loop = true;
            main.playOnAwake = true;
            main.scalingMode = ParticleSystemScalingMode.Shape;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1.2f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.003f, 0.007f);
            main.startSpeed = 0f;
            main.maxParticles = 24;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var em = ps.emission;
            em.rateOverTime = 0f;
            var sh = ps.shape;
            sh.shapeType = ParticleSystemShapeType.Box;
            sh.scale = new Vector3(size.x * 0.9f, 2f, 1f);
            var vel = ps.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.World;
            vel.x = new ParticleSystem.MinMaxCurve(-0.01f, 0.01f);
            vel.y = new ParticleSystem.MinMaxCurve(0.03f, 0.08f);
            vel.z = new ParticleSystem.MinMaxCurve(0f, 0f);
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
            var pr = emberGo.GetComponent<ParticleSystemRenderer>();
            pr.sharedMaterial = emberMat;
            pr.shadowCastingMode = ShadowCastingMode.Off;

            var mb = go.AddComponent<MenuButton>();
            mb.Configure(text, crt, iconImg != null ? iconImg.rectTransform : null, border, orn, ps, blink, exhibit);
            mb.targetGraphic = bg;
            return mb;
        }

        static Panels BuildBackPanels(Transform back, Dictionary<string, Sprite> icons, Material emberMat)
        {
            var p = new Panels();

            GameObject Panel(string name, string header)
            {
                var go = new GameObject(name, typeof(RectTransform));
                go.layer = UILayer;
                var rt = (RectTransform)go.transform;
                rt.SetParent(back, false);
                Place(rt, new Vector2(-490f, 0f), new Vector2(980f, 1280f));
                var bg = Img("Backdrop", rt, sprFill, Vector2.zero, new Vector2(980f, 1280f), new Color(0f, 0f, 0f, 0.62f));
                bg.type = Image.Type.Sliced;
                var border = Img("Border", rt, sprOutline, Vector2.zero, new Vector2(980f, 1280f), new Color(1f, 1f, 1f, 0.35f));
                border.type = Image.Type.Sliced;
                border.raycastTarget = false;
                var h = Text("Header", rt, fontCaps, header, 64f, new Vector2(60f, 540f), new Vector2(860f, 90f));
                h.color = new Color(0.92f, 0.92f, 0.92f);
                var orn = Img("Ornament", rt, sprOrnament, new Vector2(60f, 486f), new Vector2(520f, 16f), new Color(1f, 1f, 1f, 0.45f));
                orn.raycastTarget = false;
                go.SetActive(false);
                return go;
            }

            // --- Controles ---
            p.controls = Panel("Panel_Controls", "CONTROLES");
            var c = (RectTransform)p.controls.transform;
            (string icon, string action, string result)[] rows =
            {
                ("Eye", "Abrir los ojos", "Las esculturas se detienen."),
                ("EyeClosed", "Cerrar los ojos", "Las esculturas avanzan."),
                ("Frame", "Salir del encuadre", "La experiencia se pausa."),
                ("Key", "Esc  ·  Botón B", "Volver al menú."),
                ("Gamepad", "Flechas / Stick  ·  Enter / A", "Moverse y elegir en el menú."),
            };
            for (int i = 0; i < rows.Length; i++)
            {
                float y = 390f - i * 118f;
                var ic = Img("RowIcon", c, icons[rows[i].icon], new Vector2(62f, y), new Vector2(58f, 58f), new Color(1f, 1f, 1f, 0.9f));
                ic.raycastTarget = false;
                var a = Text("RowAction", c, fontBody, rows[i].action, 38f, new Vector2(150f, y + 20f), new Vector2(760f, 46f));
                a.fontWeight = FontWeight.SemiBold;
                var r = Text("RowResult", c, fontBody, rows[i].result, 32f, new Vector2(150f, y - 22f), new Vector2(760f, 42f));
                r.color = new Color(0.72f, 0.72f, 0.72f);
                r.fontStyle = FontStyles.Italic;
            }
            var homes = new List<MenuButton>();
            // Botón de Inicio: siempre en el mismo sitio (arriba a la derecha) en todas las pantallas
            MenuButton Home(RectTransform panel)
            {
                var h = Button(panel, "Inicio", icons["Home"], null, new Vector2(670f, 545f), new Vector2(260f, 70f), false, emberMat, null, 32f);
                h.name = "Btn_Inicio";
                var esc = Text("EscHint", h.transform, fontCaps, "Esc", 18f, new Vector2(196f, 0f), new Vector2(48f, 30f));
                esc.color = new Color(1f, 1f, 1f, 0.5f);
                esc.alignment = TextAlignmentOptions.Center;
                var escBox = Img("EscKey", h.transform, sprOutline, new Vector2(196f, 0f), new Vector2(48f, 30f), new Color(1f, 1f, 1f, 0.35f));
                escBox.type = Image.Type.Sliced;
                escBox.raycastTarget = false;
                homes.Add(h);
                return h;
            }

            p.controlsButtons = new[]
            {
                Button(c, "Accesibilidad", icons["Gear"], null, new Vector2(60f, -380f), new Vector2(560f, 76f), false, emberMat, null, 36f),
                Home(c),
            };

            // --- Accesibilidad: secciones, controles visibles y ayuda contextual ---
            p.accessibility = Panel("Panel_Accessibility", "ACCESIBILIDAD");
            var acc = (RectTransform)p.accessibility.transform;
            var keys = Text("KeyHint", acc, fontBody, "Izquierda / derecha: ajustar   ·   Enter: cambiar   ·   Esc: volver al inicio", 24f, new Vector2(60f, 448f), new Vector2(860f, 34f));
            keys.color = new Color(0.65f, 0.65f, 0.65f);

            void Section(string title, float y)
            {
                var s = Text("Section_" + title, acc, fontCaps, title, 22f, new Vector2(64f, y), new Vector2(800f, 30f));
                s.color = new Color(0.62f, 0.62f, 0.62f);
                s.characterSpacing = 6f;
            }

            var optionList = new List<MenuButton>();
            Section("MOVIMIENTO Y LUZ", 398f);
            optionList.Add(Option(acc, "Reducir movimiento", OptionKind.Toggle, "reduceMotion", "La cámara se mueve al 20 %. Útil si el balanceo te marea.", 340f));
            optionList.Add(Option(acc, "Desactivar destellos", OptionKind.Toggle, "disableSparkles", "Cambia los destellos y relámpagos por fundidos suaves. Recomendado si eres sensible a la luz.", 264f));
            Section("SONIDO", 200f);
            optionList.Add(Option(acc, "Volumen general", OptionKind.Slider, "master", "Volumen de todo el juego.", 142f));
            optionList.Add(Option(acc, "Música", OptionKind.Slider, "music", "Volumen de la música ambiental.", 66f));
            optionList.Add(Option(acc, "Efectos", OptionKind.Slider, "sfx", "Volumen de los efectos de la interfaz y del museo.", -10f));
            optionList.Add(Option(acc, "Subtítulos de sonidos", OptionKind.Toggle, "subtitles", "Muestra en texto los sonidos importantes, como el trueno o el latido.", -86f));
            Section("LECTURA Y RENDIMIENTO", -150f);
            optionList.Add(Option(acc, "Tamaño de texto", OptionKind.Choice, "textSize", "Agranda todos los textos del menú.", -208f));
            optionList.Add(Option(acc, "Calidad baja", OptionKind.Toggle, "lowQuality", "Quita niebla, polvo y reflejos para ganar fluidez en equipos modestos.", -284f));

            var helpBox = Img("HelpBox", acc, sprFill, new Vector2(60f, -386f), new Vector2(860f, 84f), new Color(1f, 1f, 1f, 0.06f));
            helpBox.type = Image.Type.Sliced;
            helpBox.raycastTarget = false;
            p.optionHelp = Text("HelpText", acc, fontBody, "", 28f, new Vector2(84f, -386f), new Vector2(812f, 80f));
            p.optionHelp.fontStyle = FontStyles.Italic;
            p.optionHelp.color = new Color(0.82f, 0.82f, 0.82f);

            p.accessibilityBack = Button(acc, "Volver a Controles", icons["ChevronLeft"], null, new Vector2(60f, -500f), new Vector2(460f, 72f), false, emberMat, null, 32f);
            optionList.Add(p.accessibilityBack);
            optionList.Add(Home(acc));
            p.accessibilityButtons = optionList.ToArray();

            // --- Créditos (desplazamiento vertical lento) ---
            p.credits = Panel("Panel_Credits", "CRÉDITOS");
            var cr = (RectTransform)p.credits.transform;
            // Mask (stencil) en lugar de RectMask2D: este último recorta mal cuando la placa está girada 180°
            var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
            viewport.layer = UILayer;
            viewport.GetComponent<Image>().raycastTarget = false;
            viewport.GetComponent<Mask>().showMaskGraphic = false;
            var vrt = (RectTransform)viewport.transform;
            vrt.SetParent(cr, false);
            Place(vrt, new Vector2(60f, 30f), new Vector2(860f, 840f));
            var content = Text("Content", vrt, fontBody, CreditsText(), 34f, new Vector2(0f, 0f), new Vector2(860f, 2000f));
            content.alignment = TextAlignmentOptions.Top;
            content.rectTransform.pivot = new Vector2(0f, 1f);
            content.rectTransform.anchoredPosition = new Vector2(0f, 420f);
            p.creditsContent = content.rectTransform;
            p.creditsButtons = new[] { Home(cr) };

            // --- Salir (confirmación) ---
            p.exit = Panel("Panel_Exit", "SALIR");
            var ex = (RectTransform)p.exit.transform;
            var q = Text("Question", ex, fontBody, "¿Seguro que quieres dejar de mirar?", 52f, new Vector2(60f, 120f), new Vector2(860f, 160f));
            q.fontStyle = FontStyles.Italic;
            q.alignment = TextAlignmentOptions.Center;
            p.exitButtons = new[]
            {
                Button(ex, "Quedarme", icons["Eye"], null, new Vector2(80f, -80f), new Vector2(390f, 84f), false, emberMat, null, 38f),
                Button(ex, "Salir", icons["Door"], null, new Vector2(510f, -80f), new Vector2(390f, 84f), false, emberMat, null, 38f),
                Home(ex),
            };
            p.homeButtons = homes.ToArray();
            return p;
        }

        /// <summary>Fila de opción: etiqueta a la izquierda y su control visible a la derecha.</summary>
        static OptionRow Option(RectTransform parent, string label, OptionKind kind, string key, string help, float y)
        {
            var size = new Vector2(860f, 66f);
            var go = new GameObject("Opt_" + key, typeof(RectTransform), typeof(Image));
            go.layer = UILayer;
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            Place(rt, new Vector2(60f, y), size);
            var bg = go.GetComponent<Image>();
            bg.sprite = sprFill;
            bg.type = Image.Type.Sliced;
            bg.color = new Color(0f, 0f, 0f, 0.35f);

            var border = Img("Border", rt, sprOutline, Vector2.zero, size, new Color(1f, 1f, 1f, 0.4f));
            border.type = Image.Type.Sliced;
            border.raycastTarget = false;
            var content = new GameObject("Content", typeof(RectTransform));
            content.layer = UILayer;
            var crt = (RectTransform)content.transform;
            crt.SetParent(rt, false);
            Place(crt, Vector2.zero, size);
            var text = Text("Label", crt, fontBody, label, 33f, new Vector2(28f, 0f), new Vector2(420f, size.y));
            text.fontWeight = FontWeight.SemiBold;
            text.color = new Color(0.93f, 0.93f, 0.93f);
            var orn = Img("Ornament", rt, sprOrnament, new Vector2(28f, -size.y * 0.5f + 10f), new Vector2(300f, 10f), new Color(1f, 1f, 1f, 0.7f));
            orn.type = Image.Type.Filled;
            orn.fillMethod = Image.FillMethod.Horizontal;
            orn.fillAmount = 0f;
            orn.raycastTarget = false;

            TMP_Text value = null;
            Image track = null;
            RectTransform knob = null, area = null;
            Image[] bar = null, marks = null;
            TMP_Text[] choices = null;
            switch (kind)
            {
                case OptionKind.Toggle:
                    value = Text("Value", rt, fontBody, "No", 28f, new Vector2(640f, 0f), new Vector2(80f, 40f));
                    value.alignment = TextAlignmentOptions.MidlineRight;
                    track = Img("Track", rt, sprPill, new Vector2(736f, 0f), new Vector2(96f, 40f), new Color(1f, 1f, 1f, 0.12f));
                    track.type = Image.Type.Sliced;
                    track.raycastTarget = false;
                    var k = Img("Knob", track.rectTransform, sprCircle, new Vector2(4f, 0f), new Vector2(32f, 32f), Color.white);
                    k.raycastTarget = false;
                    knob = k.rectTransform;
                    break;
                case OptionKind.Slider:
                    var areaGo = new GameObject("Bar", typeof(RectTransform));
                    areaGo.layer = UILayer;
                    area = (RectTransform)areaGo.transform;
                    area.SetParent(rt, false);
                    Place(area, new Vector2(470f, 0f), new Vector2(298f, 30f));
                    bar = new Image[10];
                    for (int i = 0; i < 10; i++)
                    {
                        bar[i] = Img("Seg" + i, area, null, new Vector2(i * 30f, 0f), new Vector2(24f, 12f + i * 1.6f), Color.white);
                        bar[i].raycastTarget = false;
                    }
                    value = Text("Value", rt, fontBody, "80 %", 28f, new Vector2(772f, 0f), new Vector2(72f, 40f));
                    value.alignment = TextAlignmentOptions.MidlineRight;
                    break;
                case OptionKind.Choice:
                    choices = new TMP_Text[2];
                    marks = new Image[2];
                    string[] names = { "100 %", "125 %" };
                    for (int i = 0; i < 2; i++)
                    {
                        choices[i] = Text("Choice" + i, rt, fontBody, names[i], 28f + i * 4f, new Vector2(600f + i * 128f, 4f), new Vector2(110f, 40f));
                        choices[i].alignment = TextAlignmentOptions.Center;
                        marks[i] = Img("Mark" + i, rt, null, new Vector2(620f + i * 128f, -20f), new Vector2(70f, 2f), Color.white);
                        marks[i].raycastTarget = false;
                    }
                    break;
            }

            var row = go.AddComponent<OptionRow>();
            row.Configure(text, crt, null, border, orn, null, false, null);
            row.ConfigureOption(kind, key, help, value, track, knob, bar, area, choices, marks);
            row.targetGraphic = bg;
            return row;
        }

        static string CreditsText()
        {
            var lines = System.IO.File.Exists(Root + "/credits_models.txt") ? System.IO.File.ReadAllLines(Root + "/credits_models.txt") : new string[0];
            var models = string.Join("\n", lines.Select(l => l.Split('|')[0].Trim() + "  ·  " + l.Split('|')[1].Trim()));
            return "<b>CUANDO NADIE MIRA</b>\nMuseo interactivo de mitos y leyendas de Colombia\n\n" +
                   "<b>Equipo</b>\n[Nombre]\n[Nombre]\n[Nombre]\n[Nombre]\n\n" +
                   "<b>Curso</b>\n[Curso]\n\n<b>Universidad</b>\n[Universidad]\n\n" +
                   "<b>Fuentes tipográficas</b> (SIL Open Font License)\nIM Fell English · Igino Marini\nCormorant Garamond · Christian Thalmann\nCinzel · Natanael Gama\n\n" +
                   "<b>Modelos y texturas</b> (CC0, Poly Haven)\n" + models + "\n\n" +
                   "<b>Sonido</b>\nSíntesis procedural en tiempo real\n\n<i>Gracias por mirar.</i>";
        }

        // ============================================================================
        // UI: overlays
        // ============================================================================

        static Transform BuildOverlay(Transform uiRoot, Camera uiCam, Dictionary<string, Sprite> icons, out TMP_Text subtitle)
        {
            var go = new GameObject("Canvas_Overlay", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            go.layer = UILayer;
            go.transform.SetParent(uiRoot, false);
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = uiCam;
            canvas.planeDistance = 0.5f;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            var rt = (RectTransform)go.transform;

            var tagline = Text("Tagline", rt, fontBody, "Usa tu mirada para detener lo que se mueve en la oscuridad.", 27f, Vector2.zero, new Vector2(900f, 40f));
            Anchor(tagline.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(70f, 46f));
            tagline.color = new Color(0.75f, 0.75f, 0.75f);
            tagline.fontStyle = FontStyles.Italic;

            var key = Img("EnterKey", rt, sprOutline, Vector2.zero, new Vector2(84f, 38f), new Color(1f, 1f, 1f, 0.7f));
            key.type = Image.Type.Sliced;
            key.raycastTarget = false;
            Anchor(key.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 0f), new Vector2(-150f, 42f));
            var keyText = Text("Label", key.rectTransform, fontCaps, "Enter", 20f, Vector2.zero, new Vector2(84f, 38f));
            keyText.alignment = TextAlignmentOptions.Center;
            Place(keyText.rectTransform, new Vector2(0f, 0f), new Vector2(84f, 38f));
            var hint = Text("EnterHint", rt, fontBody, "Presiona Enter para comenzar", 26f, Vector2.zero, new Vector2(420f, 40f));
            Anchor(hint.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 0f), new Vector2(-50f, 42f));
            hint.color = new Color(0.78f, 0.78f, 0.78f);

            subtitle = Text("SoundSubtitle", rt, fontBody, "", 32f, Vector2.zero, new Vector2(1200f, 50f));
            Anchor(subtitle.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 120f));
            subtitle.alignment = TextAlignmentOptions.Center;
            subtitle.fontStyle = FontStyles.Italic;
            return rt;
        }

        static Transform BuildScreenOverlay(Transform uiRoot, Sprite eye, Sprite iris, Sprite circle, out Image blackout)
        {
            var go = new GameObject("Canvas_Screen", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            go.layer = UILayer;
            go.transform.SetParent(uiRoot, false);
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            var rt = (RectTransform)go.transform;

            blackout = new GameObject("Blackout", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            blackout.gameObject.layer = UILayer;
            blackout.rectTransform.SetParent(rt, false);
            blackout.rectTransform.anchorMin = Vector2.zero;
            blackout.rectTransform.anchorMax = Vector2.one;
            blackout.rectTransform.offsetMin = blackout.rectTransform.offsetMax = Vector2.zero;
            blackout.color = Color.black;
            blackout.raycastTarget = false;

            // Estela de motas (detrás del ojo)
            var motes = new Image[10];
            for (int i = 0; i < motes.Length; i++)
            {
                motes[i] = Img("Mote" + i, rt, circle, Vector2.zero, new Vector2(5f, 5f), new Color(1f, 0.97f, 0.92f, 0f));
                Centered(motes[i].rectTransform);
            }

            // Cursor de ojo: contorno, iris y pupila dentro de un contenedor que se cierra al parpadear
            var eyeRoot = new GameObject("CursorEye", typeof(RectTransform)).GetComponent<RectTransform>();
            eyeRoot.gameObject.layer = UILayer;
            eyeRoot.SetParent(rt, false);
            Centered(eyeRoot);
            eyeRoot.sizeDelta = new Vector2(56f, 28f);
            var lids = new GameObject("Lids", typeof(RectTransform), typeof(RectMask2D)).GetComponent<RectTransform>();
            lids.gameObject.layer = UILayer;
            lids.SetParent(eyeRoot, false);
            Centered(lids);
            lids.sizeDelta = new Vector2(56f, 28f);
            var outline = Img("Almond", lids, eye, Vector2.zero, new Vector2(56f, 28f), Color.white);
            var irisImg = Img("Iris", lids, iris, Vector2.zero, new Vector2(20f, 20f), Color.white);
            var pupilImg = Img("Pupil", lids, circle, Vector2.zero, new Vector2(7f, 7f), new Color(1f, 1f, 1f, 0.95f));
            foreach (var i in new[] { outline, irisImg, pupilImg }) Centered(i.rectTransform);

            var cursor = go.AddComponent<CursorEye>();
            cursor.Configure(canvas, eyeRoot, lids, irisImg.rectTransform, pupilImg.rectTransform, motes);
            return rt;
        }

        static void Centered(RectTransform r)
        {
            r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f);
            r.pivot = new Vector2(0.5f, 0.5f);
            r.anchoredPosition = Vector2.zero;
            if (r.TryGetComponent<Graphic>(out var g)) g.raycastTarget = false;
        }

        static void BuildEventSystem(Transform uiRoot)
        {
            var go = new GameObject("EventSystem", typeof(EventSystem));
            go.transform.SetParent(uiRoot, false);
            var module = go.AddComponent<InputSystemUIInputModule>();
            module.AssignDefaultActions();
        }

        // ============================================================================
        // Utilidades
        // ============================================================================

        static Transform Child(string name, Transform parent)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            return t;
        }

        static GameObject Prim(PrimitiveType type, string name, Transform parent, Vector3 worldPos, Vector3 euler, Vector3 scale, Material mat)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, true);
            go.transform.SetPositionAndRotation(worldPos, Quaternion.Euler(euler));
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            return go;
        }

        static GameObject MeshObject(string name, Transform parent, Mesh mesh, Vector3 pos, Quaternion rot, Vector3 scale, Material mat)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, true);
            go.transform.SetPositionAndRotation(pos, rot);
            go.transform.localScale = scale;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
            return go;
        }

        static Light SpotLight(string name, Transform parent, Vector3 pos, Quaternion rot, Color color, float intensity, float range, float angle, LightShadows shadows)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, true);
            go.transform.SetPositionAndRotation(pos, rot);
            var l = go.AddComponent<Light>();
            l.type = LightType.Spot;
            l.color = color;
            l.intensity = intensity;
            l.range = range;
            l.spotAngle = angle;
            l.innerSpotAngle = angle * 0.5f;
            l.shadows = shadows;
            return l;
        }

        /// <summary>
        /// Instancia un FBX descargado, lo escala a <paramref name="height"/> (o a <paramref name="width"/>)
        /// y deja su base centrada en <paramref name="pos"/>. Añade un BoxCollider para la luz del cursor.
        /// </summary>
        static GameObject Model(string id, Transform parent, Vector3 pos, float yaw, float height, Material mat, string name, float width = 0f)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>($"{Root}/Models/{id}/{id}.fbx");
            var wrapper = new GameObject(name);
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(asset);
            inst.transform.SetParent(wrapper.transform, false);
            foreach (var r in inst.GetComponentsInChildren<Renderer>())
                r.sharedMaterials = Enumerable.Repeat(mat, r.sharedMaterials.Length).ToArray();

            var b = WorldBounds(inst);
            float s = width > 0f ? width / Mathf.Max(b.size.x, b.size.z) : height / Mathf.Max(1e-4f, b.size.y);
            inst.transform.localScale *= s;
            b = WorldBounds(inst);
            inst.transform.localPosition -= new Vector3(b.center.x, b.min.y, b.center.z);
            b = WorldBounds(inst);
            var box = wrapper.AddComponent<BoxCollider>();
            box.center = b.center;
            box.size = b.size;

            wrapper.transform.SetParent(parent, true);
            wrapper.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));
            return wrapper;
        }

        static Bounds WorldBounds(GameObject go)
        {
            var rends = go.GetComponentsInChildren<Renderer>();
            if (rends.Length == 0) return new Bounds(go.transform.position, Vector3.zero);
            var b = rends[0].bounds;
            foreach (var r in rends) b.Encapsulate(r.bounds);
            return b;
        }

        static CanvasGroup Group(string name, RectTransform parent)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasGroup));
            go.layer = UILayer;
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            return go.GetComponent<CanvasGroup>();
        }

        // Ancla al centro del padre y pivote a la izquierda: pos.x es el borde izquierdo
        static void Place(RectTransform rt, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0f, 0.5f);
            if (rt.parent is RectTransform prt && prt.GetComponent<Canvas>() == null && prt.name != "Front" && prt.name != "Back")
            {
                // Hijos de un rectángulo con pivote izquierdo: se anclan a su borde izquierdo
                rt.anchorMin = rt.anchorMax = new Vector2(0f, 0.5f);
            }
            rt.sizeDelta = size;
            rt.anchoredPosition = pos;
        }

        static void Anchor(RectTransform rt, Vector2 anchor, Vector2 pivot, Vector2 pos)
        {
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.anchoredPosition = pos;
        }

        static TMP_Text Text(string name, Transform parent, TMP_FontAsset font, string text, float size, Vector2 pos, Vector2 box)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = UILayer;
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            var t = go.AddComponent<TextMeshProUGUI>();
            t.font = font;
            t.text = text;
            t.fontSize = size;
            t.color = Color.white;
            t.alignment = TextAlignmentOptions.MidlineLeft;
            t.textWrappingMode = TextWrappingModes.Normal;
            t.raycastTarget = false;
            Place(rt, pos, box);
            return t;
        }

        static Image Img(string name, Transform parent, Sprite sprite, Vector2 pos, Vector2 size, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.layer = UILayer;
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            Place(rt, pos, size);
            return img;
        }

        static void SetField(Object target, string field, object value)
        {
            var so = new SerializedObject(target);
            var prop = so.FindProperty(field);
            if (prop == null) { Debug.LogWarning($"Campo {field} no encontrado en {target.GetType().Name}"); return; }
            switch (value)
            {
                case Object o: prop.objectReferenceValue = o; break;
                case Vector2 v: prop.vector2Value = v; break;
                case float f: prop.floatValue = f; break;
                case bool b: prop.boolValue = b; break;
                case string s: prop.stringValue = s; break;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void AddToBuildSettings(string path)
        {
            var scenes = EditorBuildSettings.scenes.ToList();
            if (scenes.Any(s => s.path == path)) return;
            scenes.Add(new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
