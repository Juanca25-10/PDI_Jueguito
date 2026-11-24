using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using B = CuandoNadieMira.Menu.EditorTools.MenuSceneBuilder;

namespace CuandoNadieMira.Menu.EditorTools
{
    /// <summary>
    /// Construye las pantallas que siguen al menú principal: Calibración y Preparando la experiencia (escena "Carga"),
    /// las dos con la pantalla de excepción "Se perdió el seguimiento". Cada una parte del mismo salón del museo
    /// que el menú principal (se copia MainMenu.unity y se le quita la placa), así el paso entre pantallas es continuo.
    /// Menú: Cuando Nadie Mira → Construir menú completo.
    /// </summary>
    public static class MenuFlowBuilder
    {
        const string Root = MenuAssetFactory.Root;
        const string MainMenuPath = Root + "/Scenes/MainMenu.unity";
        const string CalibrationPath = Root + "/Scenes/Calibracion.unity";
        const string LoadingPath = Root + "/Scenes/Carga.unity";
        const string GameScenePath = "Assets/Scenes/Juego_Estatuas.unity";

        static readonly string[] IconNames =
        {
            "Eye", "EyeClosed", "Gear", "Door", "Chevron", "ChevronLeft", "Home", "Frame", "Camera", "Chair", "Head", "Person",
            "Play", "Retry", "Warning", "Signal", "Check", "Cross", "Move", "Sun", "Moon", "Mouse", "Pause", "Bust",
        };

        internal static Dictionary<string, Sprite> icons;
        internal static Material emberMat, titleMat, feedMat;
        internal static Sprite ring, headGuide;

        // ==========================================================================================
        // Entradas
        // ==========================================================================================

        [MenuItem("Cuando Nadie Mira/Construir menú completo (todas las pantallas)", priority = 0)]
        public static void BuildAll()
        {
            B.Build();
            BuildFlowScenes();
        }

        [MenuItem("Cuando Nadie Mira/Construir solo Calibración y Carga", priority = 1)]
        public static void BuildFlowScenes()
        {
            if (!File.Exists(MainMenuPath)) B.Build();
            LoadShared();
            BuildCalibration();
            BuildLoading();
            ConfigureBuildSettings();
            AssetDatabase.SaveAssets();
            EditorSceneManager.OpenScene(MainMenuPath, OpenSceneMode.Single);
            Debug.Log("[Cuando Nadie Mira] Calibración y Preparando la experiencia construidas.");
        }

        /// <summary>Para la línea de comandos: Unity -batchmode -executeMethod ...MenuFlowBuilder.BuildAllBatch</summary>
        public static void BuildAllBatch()
        {
            try
            {
                BuildAll();
                EditorApplication.Exit(0);
            }
            catch (System.Exception e)
            {
                Debug.LogException(e);
                EditorApplication.Exit(1);
            }
        }

        [MenuItem("Cuando Nadie Mira/Borrar calibración guardada", priority = 20)]
        static void ClearCalibration()
        {
            CalibrationStore.Clear();
            Debug.Log("[Cuando Nadie Mira] Calibración borrada de PlayerPrefs.");
        }

        // ==========================================================================================
        // Recursos compartidos
        // ==========================================================================================

        internal static void LoadShared()
        {
            B.fontTitle = MenuAssetFactory.Font("IMFellEnglish");
            B.fontBody = MenuAssetFactory.Font("CormorantGaramond");
            B.fontCaps = MenuAssetFactory.Font("Cinzel");
            B.sprFill = SpriteOr("UI_RoundedFill", MenuTextureFactory.RoundedFill);
            B.sprOutline = SpriteOr("UI_RoundedOutline", MenuTextureFactory.RoundedOutline);
            B.sprOrnament = SpriteOr("UI_Ornament", MenuTextureFactory.Ornament);
            B.sprPill = SpriteOr("UI_Pill", MenuTextureFactory.Pill);
            B.sprCircle = SpriteOr("UI_Circle", MenuTextureFactory.Circle);
            ring = MenuTextureFactory.Ring();
            headGuide = MenuTextureFactory.HeadGuide();
            icons = new Dictionary<string, Sprite>();
            foreach (var n in IconNames) icons[n] = SpriteOr("Icon_" + n, () => MenuTextureFactory.Icon(n));

            emberMat = AssetDatabase.LoadAssetAtPath<Material>(MenuAssetFactory.MatFolder + "/FX_Ember.mat");
            titleMat = AssetDatabase.LoadAssetAtPath<Material>(MenuAssetFactory.MatFolder + "/Title_Cracked.mat");

            string feedPath = MenuAssetFactory.MatFolder + "/UI_CameraFeed.mat";
            feedMat = AssetDatabase.LoadAssetAtPath<Material>(feedPath);
            if (feedMat == null)
            {
                feedMat = new Material(Shader.Find("CuandoNadieMira/UI Grayscale")) { name = "UI_CameraFeed" };
                AssetDatabase.CreateAsset(feedMat, feedPath);
            }
        }

        internal static Sprite SpriteOr(string name, System.Func<Sprite> make)
        {
            var s = AssetDatabase.LoadAssetAtPath<Sprite>($"{MenuTextureFactory.Folder}/{name}.png");
            return s != null ? s : make();
        }

        // ==========================================================================================
        // Salón base: copia del menú principal sin la placa ni sus sistemas
        // ==========================================================================================

        struct Hall
        {
            public Transform ui;
            public Camera uiCam;
            public SceneTransition transition;
            public MenuCameraRig rig;
        }

        static Hall OpenHall(float yaw)
        {
            EditorSceneManager.OpenScene(MainMenuPath, OpenSceneMode.Single);
            var h = new Hall
            {
                ui = GameObject.Find("UI").transform,
                rig = Object.FindAnyObjectByType<MenuCameraRig>(),
                transition = Object.FindAnyObjectByType<SceneTransition>(),
            };
            h.uiCam = h.rig.GetComponentsInChildren<Camera>().First(c => c.name == "UICamera");

            Object.DestroyImmediate(h.ui.Find("Canvas_WorldPlate").gameObject);
            var overlay = h.ui.Find("Canvas_Overlay");
            foreach (Transform child in overlay.Cast<Transform>().ToArray())
                if (child.name != "SoundSubtitle") Object.DestroyImmediate(child.gameObject);

            var systems = GameObject.Find("_Systems");
            Object.DestroyImmediate(systems.GetComponent<MenuController>());
            Object.DestroyImmediate(systems.GetComponent<PanelFlipper>());

            // Otro punto de vista del mismo salón
            h.rig.transform.rotation = Quaternion.Euler(-1.5f, yaw, 0f);
            return h;
        }

        static RectTransform NewCanvas(Hall h, string name)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            go.layer = B.UILayer;
            go.transform.SetParent(h.ui, false);
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = h.uiCam;
            canvas.planeDistance = 0.45f;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;

            // Velo oscuro sobre el salón: el museo se sigue viendo, pero el texto se lee bien sobre las luces del fondo
            var scrim = new GameObject("Scrim", typeof(RectTransform), typeof(Image));
            scrim.layer = B.UILayer;
            var srt = (RectTransform)scrim.transform;
            srt.SetParent(go.transform, false);
            srt.anchorMin = Vector2.zero;
            srt.anchorMax = Vector2.one;
            srt.offsetMin = srt.offsetMax = Vector2.zero;
            var simg = scrim.GetComponent<Image>();
            simg.color = new Color(0f, 0f, 0f, 0.62f);
            simg.raycastTarget = false;
            return (RectTransform)go.transform;
        }

        /// <summary>Contenedor de tamaño cero en el centro: sus hijos usan coordenadas desde el centro de la pantalla.</summary>
        internal static RectTransform Container(string name, Transform parent, Vector2 pos, bool canvasGroup = false)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = B.UILayer;
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = Vector2.zero;
            rt.anchoredPosition = pos;
            if (canvasGroup) go.AddComponent<CanvasGroup>();
            return rt;
        }

        internal static TMP_Text Label(string name, Transform parent, TMP_FontAsset font, string text, float size, float x, float y, float w, float h,
            TextAlignmentOptions align = TextAlignmentOptions.MidlineLeft, float gray = 0.93f, FontStyles style = FontStyles.Normal)
        {
            var t = B.Text(name, parent, font, text, size, new Vector2(x, y), new Vector2(w, h));
            t.alignment = align;
            t.color = new Color(gray, gray, gray);
            t.fontStyle = style;
            return t;
        }

        internal static TMP_Text Title(string name, Transform parent, string text, float size, float y, float h)
        {
            var t = Label(name, parent, B.fontTitle, text, size, -800f, y, 1600f, h, TextAlignmentOptions.Center);
            t.fontSharedMaterial = titleMat;
            t.characterSpacing = -3f;
            t.lineSpacing = -28f;
            t.horizontalMapping = TextureMappingOptions.Paragraph;
            t.verticalMapping = TextureMappingOptions.Paragraph;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            return t;
        }

        internal static Image Ornament(Transform parent, float y)
        {
            var o = B.Img("Ornament", parent, B.sprOrnament, new Vector2(-260f, y), new Vector2(520f, 16f), new Color(1f, 1f, 1f, 0.45f));
            o.raycastTarget = false;
            return o;
        }

        internal static Image Bar(string name, Transform parent, float x, float y, float w, float h, float bgAlpha, out Image fill)
        {
            var bg = B.Img(name, parent, B.sprFill, new Vector2(x, y), new Vector2(w, h), new Color(1f, 1f, 1f, bgAlpha));
            bg.type = Image.Type.Sliced;
            bg.raycastTarget = false;
            fill = B.Img("Fill", bg.rectTransform, B.sprFill, Vector2.zero, new Vector2(w, h), new Color(1f, 1f, 1f, 0.9f));
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = 0;
            fill.fillAmount = 0f;
            fill.raycastTarget = false;
            return bg;
        }

        internal static Image Icon(string name, Transform parent, string icon, float x, float y, float size, float alpha = 0.92f)
        {
            var i = B.Img(name, parent, icons[icon], new Vector2(x, y), new Vector2(size, size), new Color(1f, 1f, 1f, alpha));
            i.raycastTarget = false;
            return i;
        }

        static MenuButton Btn(Transform parent, string label, string icon, bool chevron, float w, float h, float fontSize)
        {
            var b = B.Button(parent, label, icon != null ? icons[icon] : null, chevron ? icons["Chevron"] : null,
                new Vector2(-w * 0.5f, 0f), new Vector2(w, h), false, emberMat, null, fontSize);
            return b;
        }

        /// <summary>Indicador con punto (y opcionalmente icono) + texto.</summary>
        static StatusChip Chip(Transform parent, string name, float x, float y, float w, string text, bool withIcon = false, bool boxed = false)
        {
            var root = Container(name, parent, Vector2.zero);
            if (boxed)
            {
                var box = B.Img("Box", root, B.sprFill, new Vector2(x, y), new Vector2(w, 58f), new Color(0f, 0f, 0f, 0.45f));
                box.type = Image.Type.Sliced;
                box.raycastTarget = false;
                var border = B.Img("Border", root, B.sprOutline, new Vector2(x, y), new Vector2(w, 58f), new Color(1f, 1f, 1f, 0.3f));
                border.type = Image.Type.Sliced;
                border.raycastTarget = false;
            }
            float pad = boxed ? 16f : 0f;
            Image dot = null, icon = null;
            if (withIcon)
            {
                var ringImg = B.Img("Ring", root, ring, new Vector2(x + pad, y), new Vector2(36f, 36f), new Color(1f, 1f, 1f, 0.6f));
                ringImg.raycastTarget = false;
                icon = Icon("Icon", root, "Check", x + pad + 6f, y, 24f);
            }
            else
            {
                dot = B.Img("Dot", root, B.sprCircle, new Vector2(x + pad, y), new Vector2(14f, 14f), Color.white);
                dot.raycastTarget = false;
            }
            var label = Label("Label", root, B.fontBody, text, 28f, x + pad + (withIcon ? 48f : 26f), y, w - pad * 2f - 40f, 40f);
            var chip = root.gameObject.AddComponent<StatusChip>();
            chip.Configure(dot, icon, label, icons["Check"], ring, icons["Cross"]);
            return chip;
        }

        static void FlowSystems(out GameObject flow, out EyeTrackingClient client, bool drawBoxes)
        {
            flow = new GameObject("_Flow");
            client = flow.AddComponent<EyeTrackingClient>();
            B.SetField(client, "drawBoxes", drawBoxes);
        }

        static void Save(string path)
        {
            var scene = EditorSceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, path);
        }

        // ==========================================================================================
        // Calibración
        // ==========================================================================================

        static void BuildCalibration()
        {
            var hall = OpenHall(0f);   // la ventana iluminada del salón queda entre las dos columnas
            var c = NewCanvas(hall, "Canvas_Calibracion");
            FlowSystems(out var flow, out var client, drawBoxes: true);
            var ctrl = flow.AddComponent<CalibrationController>();

            // Encabezado
            Title("Title", c, "CALIBRACIÓN", 104f, 404f, 130f);
            Label("Subtitle", c, B.fontBody, "Ajusta tu mirada antes de entrar al museo", 36f, -600f, 336f, 1200f, 50f, TextAlignmentOptions.Center, 0.82f);
            Ornament(c, 304f);
            MenuSceneBuilder.BuildWebcamIndicator(c, icons["Camera"], WebcamStatusIndicator.Source.Detector);

            // --- Marco de la cámara (izquierda) ---
            const float px = -800f, pw = 740f, ph = 440f, py = 40f;
            var panel = Container("CameraPanel", c, Vector2.zero);
            var bg = B.Img("Backdrop", panel, B.sprFill, new Vector2(px, py), new Vector2(pw, ph), new Color(0f, 0f, 0f, 0.6f));
            bg.type = Image.Type.Sliced;
            bg.raycastTarget = false;

            var feedGo = new GameObject("Feed", typeof(RectTransform), typeof(RawImage));
            feedGo.layer = B.UILayer;
            var feedImg = feedGo.GetComponent<RawImage>();
            feedImg.rectTransform.SetParent(panel, false);
            B.Place(feedImg.rectTransform, new Vector2(px + 6f, py), new Vector2(pw - 12f, ph - 12f));
            feedImg.material = feedMat;
            feedImg.raycastTarget = false;
            feedImg.color = new Color(1f, 1f, 1f, 0f);

            var scan = B.Img("ScanLine", panel, null, new Vector2(px + 6f, py), new Vector2(pw - 12f, 2f), new Color(1f, 1f, 1f, 0.18f));
            scan.raycastTarget = false;
            var scanHolder = Container("ScanArea", panel, new Vector2(px + pw * 0.5f, py));
            ((RectTransform)scanHolder).sizeDelta = new Vector2(pw, ph);
            scan.rectTransform.SetParent(scanHolder, false);
            scan.rectTransform.anchorMin = scan.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            scan.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            scan.rectTransform.anchoredPosition = Vector2.zero;

            var guide = B.Img("HeadGuide", panel, headGuide, new Vector2(px + pw * 0.5f - 140f, py - 38f), new Vector2(280f, 350f), new Color(1f, 1f, 1f, 0.4f));
            guide.raycastTarget = false;

            // Esquinas del marco
            const float inset = 16f, arm = 48f, thick = 3f;
            foreach (var (sx, sy) in new[] { (0, 1), (1, 1), (0, -1), (1, -1) })
            {
                float cx = sx == 0 ? px + inset : px + pw - inset;
                float cy = py + sy * (ph * 0.5f - inset);
                float dir = sx == 0 ? 1f : -1f;
                var hBar = B.Img("Corner", panel, null, new Vector2(dir > 0 ? cx : cx - arm, cy), new Vector2(arm, thick), new Color(1f, 1f, 1f, 0.85f));
                var vBar = B.Img("Corner", panel, null, new Vector2(dir > 0 ? cx : cx - thick, cy - sy * arm * 0.5f), new Vector2(thick, arm), new Color(1f, 1f, 1f, 0.85f));
                hBar.raycastTarget = vBar.raycastTarget = false;
            }
            var border = B.Img("Border", panel, B.sprOutline, new Vector2(px, py), new Vector2(pw, ph), new Color(1f, 1f, 1f, 0.22f));
            border.type = Image.Type.Sliced;
            border.raycastTarget = false;

            var placeholder = Label("Message", panel, B.fontBody, "Encendiendo la cámara...", 34f, px + 60f, py, pw - 120f, 220f, TextAlignmentOptions.Center, 0.86f, FontStyles.Italic);
            var countdown = Label("Countdown", panel, B.fontTitle, "", 170f, px, py, pw, ph, TextAlignmentOptions.Center);
            countdown.fontSharedMaterial = titleMat;
            countdown.textWrappingMode = TextWrappingModes.NoWrap;
            countdown.enableAutoSizing = true;      // los números van grandes; palabras como "Ojos cerrados" se ajustan al marco
            countdown.fontSizeMin = 40f;
            countdown.fontSizeMax = 170f;
            countdown.margin = new Vector4(30f, 0f, 30f, 0f);

            var feedView = panel.gameObject.AddComponent<CameraFeedView>();
            feedView.Configure(feedImg, guide, placeholder, scan.rectTransform);

            // Indicadores bajo el marco
            var faceChip = Chip(c, "Chip_Rostro", px + 40f, py - ph * 0.5f - 38f, 320f, "Rostro detectado");
            var stableChip = Chip(c, "Chip_Estable", px + 380f, py - ph * 0.5f - 38f, 320f, "Cámara estable");

            // Barra de lectura de ojos
            float my = py - ph * 0.5f - 88f;
            var meterLabel = Label("MeterLabel", c, B.fontBody, "Lectura de tus ojos", 26f, px, my, 250f, 40f, gray: 0.75f);
            var meterBg = Bar("EyeMeter", c, px + 250f, my, 380f, 10f, 0.14f, out var meterFill);
            var closeMark = B.Img("CloseMark", meterBg.rectTransform, null, Vector2.zero, new Vector2(2f, 26f), new Color(1f, 1f, 1f, 0.9f));
            var openMark = B.Img("OpenMark", meterBg.rectTransform, null, Vector2.zero, new Vector2(2f, 18f), new Color(1f, 1f, 1f, 0.5f));
            closeMark.raycastTarget = openMark.raycastTarget = false;
            var meterValue = Label("MeterValue", c, B.fontBody, "--", 26f, px + 640f, my, 100f, 40f, TextAlignmentOptions.MidlineRight, 0.85f);
            var meter = meterBg.gameObject.AddComponent<EyeMeter>();
            meter.Configure(meterFill, closeMark.rectTransform, openMark.rectTransform, meterLabel, meterValue);

            // --- Pasos (derecha) ---
            const float rx = 60f, rw = 720f;
            var colBg = B.Img("ColumnBackdrop", c, B.sprFill, new Vector2(rx - 30f, 0f), new Vector2(rw + 60f, 580f), new Color(0f, 0f, 0f, 0.5f));
            colBg.type = Image.Type.Sliced;
            colBg.raycastTarget = false;
            var steps = Container("Steps", c, Vector2.zero, canvasGroup: true);
            var stepLabel = Label("StepLabel", steps, B.fontBody, "Paso 1 de 3", 30f, rx, 248f, 400f, 40f, gray: 0.85f);
            var segments = new Image[3];
            for (int i = 0; i < 3; i++) Bar("Segment" + (i + 1), steps, rx + i * 245f, 214f, 225f, 6f, 0.16f, out segments[i]);

            (string icon, string text)[] stepDefs =
            {
                ("Chair", "Siéntate frente a la cámara, a un brazo de distancia."),
                ("Eye", "Mira al centro del marco con los ojos abiertos."),
                ("Head", "Cierra los ojos entre las dos campanas, con la cabeza recta."),
            };
            var rows = new CanvasGroup[3];
            var numbers = new TMP_Text[3];
            var checks = new Image[3];
            for (int i = 0; i < 3; i++)
            {
                float y = 136f - i * 104f;
                var row = Container("Step" + (i + 1), steps, Vector2.zero, canvasGroup: true);
                rows[i] = row.GetComponent<CanvasGroup>();
                var r = B.Img("Ring", row, ring, new Vector2(rx, y), new Vector2(58f, 58f), new Color(1f, 1f, 1f, 0.85f));
                r.raycastTarget = false;
                numbers[i] = Label("Number", row, B.fontCaps, (i + 1).ToString(), 30f, rx, y + 2f, 58f, 58f, TextAlignmentOptions.Center);
                checks[i] = Icon("Check", row, "Check", rx + 9f, y, 40f);
                checks[i].enabled = false;
                Icon("Icon", row, stepDefs[i].icon, rx + 84f, y, 56f, 0.9f);
                Label("Text", row, B.fontBody, stepDefs[i].text, 31f, rx + 160f, y, rw - 160f, 96f);
            }

            // --- Resultados (reemplazan a los pasos) ---
            var results = Container("Results", c, Vector2.zero, canvasGroup: true);
            var resHeader = Label("Header", results, B.fontCaps, "CALIBRACIÓN COMPLETADA", 36f, rx, 248f, rw, 50f);
            Icon("HeaderCheck", results, "Check", rx + rw - 60f, 248f, 46f);
            (string label, string name)[] resRows =
            {
                ("Ojos abiertos  ·  P(cerrados) media", "Open"),
                ("Ojos cerrados  ·  P(cerrados) media", "Closed"),
                ("Calidad de la calibración", "Quality"),
                ("Prueba de parpadeo", "Test"),
            };
            var resValues = new TMP_Text[4];
            for (int i = 0; i < 4; i++)
            {
                float y = 172f - i * 62f;
                Label("Label_" + resRows[i].name, results, B.fontBody, resRows[i].label, 29f, rx, y, 430f, 44f, gray: 0.72f);
                resValues[i] = Label("Value_" + resRows[i].name, results, B.fontBody, "-", 31f, rx + 430f, y, rw - 430f, 44f, TextAlignmentOptions.MidlineRight);
                resValues[i].fontWeight = FontWeight.SemiBold;
                var line = B.Img("Divider", results, null, new Vector2(rx, y - 31f), new Vector2(rw, 1f), new Color(1f, 1f, 1f, 0.12f));
                line.raycastTarget = false;
            }
            var resLive = Label("Live", results, B.fontBody, "", 30f, rx, -86f, rw, 44f, gray: 0.88f, style: FontStyles.Italic);

            // --- Instrucción, ayuda y progreso de la fase ---
            var instruction = Label("Instruction", c, B.fontBody, "", 33f, rx, -160f, rw, 50f);
            instruction.fontWeight = FontWeight.SemiBold;
            var hint = Label("Hint", c, B.fontBody, "", 27f, rx, -212f, rw, 62f, gray: 0.74f, style: FontStyles.Italic);
            Bar("PhaseBar", c, rx, -258f, rw, 4f, 0.12f, out var phaseFill);

            // --- Botones (apilados y centrados) ---
            var buttons = Container("Buttons", c, new Vector2(0f, -330f));
            var btnStart = Btn(buttons, "Iniciar calibración", "Play", true, 580f, 76f, 38f);
            var btnUseSaved = Btn(buttons, "Usar mi calibración y entrar", "Check", false, 500f, 62f, 32f);
            var btnEnter = Btn(buttons, "Entrar al museo", "Eye", true, 580f, 76f, 38f);
            var btnRepeat = Btn(buttons, "Repetir calibración", "Retry", false, 440f, 62f, 32f);
            var btnBack = Btn(buttons, "Volver al menú", "ChevronLeft", false, 440f, 62f, 32f);
            var btnCancel = Btn(buttons, "Cancelar", "Cross", false, 320f, 62f, 32f);
            var btnRetryCam = Btn(buttons, "Reintentar cámara", "Retry", false, 440f, 62f, 32f);
            var btnSkip = Btn(buttons, "Omitir prueba", "Chevron", false, 360f, 62f, 32f);
            var btnCamera = Btn(buttons, "Cámara: -", "Camera", false, 500f, 62f, 30f);
            btnStart.name = "Btn_IniciarCalibracion";
            btnEnter.name = "Btn_EntrarMuseo";

            // --- Pie de pantalla ---
            var saved = Label("SavedInfo", c, B.fontBody, "", 25f, 0f, 0f, 900f, 40f, gray: 0.68f, style: FontStyles.Italic);
            B.Anchor(saved.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(64f, 34f));
            var keys = Label("KeyHint", c, B.fontBody, "Enter: elegir   ·   Esc: volver", 25f, 0f, 0f, 500f, 40f, TextAlignmentOptions.MidlineRight, 0.6f);
            B.Anchor(keys.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-64f, 34f));

            var lost = BuildTrackingLost(c);
            c.gameObject.AddComponent<TextScaleApplier>();

            // Conexiones
            B.SetField(ctrl, "client", client);
            B.SetField(ctrl, "transition", hall.transition);
            B.SetField(ctrl, "lostPanel", lost);
            B.SetField(ctrl, "feed", feedView);
            B.SetField(ctrl, "faceChip", faceChip);
            B.SetField(ctrl, "stableChip", stableChip);
            B.SetField(ctrl, "countdown", countdown);
            B.SetField(ctrl, "stepLabel", stepLabel);
            SetArray(ctrl, "stepSegments", segments);
            SetArray(ctrl, "stepRows", rows);
            SetArray(ctrl, "stepNumbers", numbers);
            SetArray(ctrl, "stepChecks", checks);
            B.SetField(ctrl, "stepsGroup", steps.GetComponent<CanvasGroup>());
            B.SetField(ctrl, "instruction", instruction);
            B.SetField(ctrl, "hint", hint);
            B.SetField(ctrl, "savedInfo", saved);
            B.SetField(ctrl, "phaseBar", phaseFill);
            B.SetField(ctrl, "eyeMeter", meter);
            B.SetField(ctrl, "resultsGroup", results.GetComponent<CanvasGroup>());
            B.SetField(ctrl, "resultsHeader", resHeader);
            B.SetField(ctrl, "resOpen", resValues[0]);
            B.SetField(ctrl, "resClosed", resValues[1]);
            B.SetField(ctrl, "resQuality", resValues[2]);
            B.SetField(ctrl, "resTest", resValues[3]);
            B.SetField(ctrl, "resLive", resLive);
            B.SetField(ctrl, "buttonsArea", buttons);
            B.SetField(ctrl, "btnStart", btnStart);
            B.SetField(ctrl, "btnUseSaved", btnUseSaved);
            B.SetField(ctrl, "btnEnter", btnEnter);
            B.SetField(ctrl, "btnRepeat", btnRepeat);
            B.SetField(ctrl, "btnBack", btnBack);
            B.SetField(ctrl, "btnCancel", btnCancel);
            B.SetField(ctrl, "btnRetryCamera", btnRetryCam);
            B.SetField(ctrl, "btnSkipTest", btnSkip);
            B.SetField(ctrl, "btnCamera", btnCamera);

            Save(CalibrationPath);
        }

        // ==========================================================================================
        // Preparando la experiencia
        // ==========================================================================================

        static void BuildLoading()
        {
            var hall = OpenHall(6f);
            var c = NewCanvas(hall, "Canvas_Carga");
            FlowSystems(out var flow, out var client, drawBoxes: false);
            var ctrl = flow.AddComponent<LoadingController>();

            Title("Title", c, "PREPARANDO\nLA EXPERIENCIA", 100f, 350f, 240f);
            Label("Subtitle", c, B.fontBody, "El museo está observando", 38f, -600f, 200f, 1200f, 52f, TextAlignmentOptions.Center, 0.82f);
            Ornament(c, 168f);

            // Progreso real de la carga
            const float x0 = -420f, w = 840f;
            var progressLabel = Label("ProgressLabel", c, B.fontBody, "Comprobando cámara y calibración...", 31f, x0, 122f, 560f, 44f);
            var progressValue = Label("ProgressValue", c, B.fontBody, "", 31f, x0 + w - 200f, 122f, 200f, 44f, TextAlignmentOptions.MidlineRight);
            Bar("ProgressBar", c, x0, 86f, w, 12f, 0.14f, out var progressFill);

            // Reglas del juego
            (string icon, string text)[] rules =
            {
                ("Eye", "Mantén los ojos abiertos para detener las esculturas."),
                ("EyeClosed", "Si cierras los ojos, ellas avanzarán."),
                ("Frame", "Si pierdes el encuadre, la experiencia se pausará."),
            };
            for (int i = 0; i < rules.Length; i++)
            {
                float y = 12f - i * 82f;
                var box = B.Img("RuleBox", c, B.sprOutline, new Vector2(x0, y), new Vector2(66f, 66f), new Color(1f, 1f, 1f, 0.4f));
                box.type = Image.Type.Sliced;
                box.raycastTarget = false;
                Icon("RuleIcon", c, rules[i].icon, x0 + 11f, y, 44f);
                Label("Rule", c, B.fontBody, rules[i].text, 31f, x0 + 96f, y, w - 96f, 70f);
                if (i < rules.Length - 1)
                {
                    var line = B.Img("Divider", c, null, new Vector2(x0 + 96f, y - 41f), new Vector2(w - 96f, 1f), new Color(1f, 1f, 1f, 0.1f));
                    line.raycastTarget = false;
                }
            }

            // Comprobaciones reales
            var camCheck = Chip(c, "Check_Camara", -520f, -248f, 320f, "Buscando cámara", withIcon: true, boxed: true);
            var calCheck = Chip(c, "Check_Calibracion", -170f, -248f, 340f, "Calibración completada", withIcon: true, boxed: true);
            var trkCheck = Chip(c, "Check_Seguimiento", 200f, -248f, 320f, "Seguimiento en espera", withIcon: true, boxed: true);

            var status = Label("Status", c, B.fontBody, "", 30f, -700f, -312f, 1400f, 44f, TextAlignmentOptions.Center, 0.85f, FontStyles.Italic);

            var buttons = Container("Choices", c, new Vector2(0f, -374f));
            var btnEnter = Btn(buttons, "Entrar de todos modos", "Eye", false, 420f, 64f, 32f);
            var btnCalibrate = Btn(buttons, "Calibrar", "Gear", false, 270f, 64f, 32f);
            var btnBack = Btn(buttons, "Volver al menú", "ChevronLeft", false, 340f, 64f, 32f);

            var tip = Label("Tip", c, B.fontBody, "", 27f, -800f, -438f, 1600f, 40f, TextAlignmentOptions.Center, 0.7f, FontStyles.Italic);

            var lost = BuildTrackingLost(c);
            c.gameObject.AddComponent<TextScaleApplier>();

            B.SetField(ctrl, "client", client);
            B.SetField(ctrl, "transition", hall.transition);
            B.SetField(ctrl, "lostPanel", lost);
            B.SetField(ctrl, "progressLabel", progressLabel);
            B.SetField(ctrl, "progressValue", progressValue);
            B.SetField(ctrl, "progressFill", progressFill);
            B.SetField(ctrl, "cameraCheck", camCheck);
            B.SetField(ctrl, "calibrationCheck", calCheck);
            B.SetField(ctrl, "trackingCheck", trkCheck);
            B.SetField(ctrl, "status", status);
            B.SetField(ctrl, "tip", tip);
            B.SetField(ctrl, "btnEnterAnyway", btnEnter);
            B.SetField(ctrl, "btnCalibrate", btnCalibrate);
            B.SetField(ctrl, "btnBack", btnBack);

            Save(LoadingPath);
        }

        // ==========================================================================================
        // Se perdió el seguimiento (compartida)
        // ==========================================================================================

        static TrackingLostPanel BuildTrackingLost(RectTransform canvas)
        {
            var root = new GameObject("Panel_SeguimientoPerdido", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
            root.layer = B.UILayer;
            var rt = (RectTransform)root.transform;
            rt.SetParent(canvas, false);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            var dim = root.GetComponent<Image>();
            dim.color = new Color(0.02f, 0.02f, 0.02f, 0.985f);   // también bloquea los clics a lo que hay detrás

            var p = Container("Content", rt, Vector2.zero);
            Title("Title", p, "SE PERDIÓ\nEL SEGUIMIENTO", 92f, 348f, 220f);
            var sub = Label("Subtitle", p, B.fontBody, "No se detecta tu rostro con suficiente estabilidad", 33f, -700f, 204f, 1400f, 50f, TextAlignmentOptions.Center, 0.85f, FontStyles.Italic);
            Ornament(p, 172f);

            Icon("Warning", p, "Warning", -400f, 62f, 150f);
            var divider = B.Img("Divider", p, null, new Vector2(-200f, 62f), new Vector2(1f, 170f), new Color(1f, 1f, 1f, 0.18f));
            divider.raycastTarget = false;
            (string icon, string text)[] statusDefs = { ("Camera", "Cámara: -"), ("Person", "Rostro: -"), ("Signal", "Confianza: -") };
            var chips = new StatusChip[3];
            for (int i = 0; i < 3; i++)
            {
                float y = 118f - i * 56f;
                Icon("StatusIcon", p, statusDefs[i].icon, -160f, y, 40f);
                chips[i] = Chip(p, "Status" + i, -100f, y, 500f, statusDefs[i].text);
            }

            var line = B.Img("Line", p, null, new Vector2(-480f, -60f), new Vector2(960f, 1f), new Color(1f, 1f, 1f, 0.15f));
            line.raycastTarget = false;
            (string icon, string text)[] tips =
            {
                ("Person", "Vuelve a colocarte\nfrente a la cámara"),
                ("Move", "Mantén la cabeza\ndentro del encuadre"),
                ("Sun", "Evita giros bruscos\no poca iluminación"),
            };
            for (int i = 0; i < tips.Length; i++)
            {
                float x = -480f + i * 330f;
                Icon("TipIcon", p, tips[i].icon, x, -116f, 46f, 0.8f);
                Label("Tip", p, B.fontBody, tips[i].text, 25f, x + 60f, -116f, 260f, 72f, gray: 0.78f);
            }

            var retry = Btn(p, "Reintentar calibración", "Retry", true, 580f, 70f, 34f);
            var resume = Btn(p, "Continuar cuando esté listo", "Play", false, 580f, 70f, 34f);
            var menu = Btn(p, "Volver al menú", "Door", false, 580f, 70f, 34f);
            ((RectTransform)retry.transform).anchoredPosition = new Vector2(-290f, -218f);
            ((RectTransform)resume.transform).anchoredPosition = new Vector2(-290f, -302f);
            ((RectTransform)menu.transform).anchoredPosition = new Vector2(-290f, -386f);
            resume.gameObject.AddComponent<CanvasGroup>();

            var panel = root.AddComponent<TrackingLostPanel>();
            panel.Configure(root.GetComponent<CanvasGroup>(), sub, chips[0], chips[1], chips[2], retry, resume, menu);
            return panel;
        }

        // ==========================================================================================
        // Build Settings: menú primero, luego Calibración, Carga y el juego
        // ==========================================================================================

        static void ConfigureBuildSettings()
        {
            var ordered = new List<string> { MainMenuPath, CalibrationPath, LoadingPath };
            if (File.Exists(GameScenePath)) ordered.Add(GameScenePath);
            var rest = EditorBuildSettings.scenes.Where(s => !ordered.Contains(s.path));
            EditorBuildSettings.scenes = ordered.Select(p => new EditorBuildSettingsScene(p, true)).Concat(rest).ToArray();
        }

        static void SetArray<T>(Object target, string field, T[] values) where T : Object
        {
            var so = new SerializedObject(target);
            var prop = so.FindProperty(field);
            prop.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) prop.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
