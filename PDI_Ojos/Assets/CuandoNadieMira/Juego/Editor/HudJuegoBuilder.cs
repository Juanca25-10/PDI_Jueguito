using System.IO;
using System.Linq;
using CuandoNadieMira.Menu;
using CuandoNadieMira.Menu.EditorTools;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using B = CuandoNadieMira.Menu.EditorTools.MenuSceneBuilder;
using F = CuandoNadieMira.Menu.EditorTools.MenuFlowBuilder;

namespace CuandoNadieMira.Juego.EditorTools
{
    /// <summary>
    /// Construye el HUD del juego (prefab HUD_Museo) con las fuentes, iconos y estilo del menú, lo coloca en
    /// Juego_Estatuas y oculta el HUD anterior (CanvasHUD y el panel de CanvasParpados; los párpados siguen).
    /// No borra nada de la escena: para volver al HUD anterior basta con activarlo y quitar HUD_Museo.
    /// </summary>
    public static class HudJuegoBuilder
    {
        const string GameScenePath = "Assets/Scenes/Juego_Estatuas.unity";
        const string Folder = "Assets/CuandoNadieMira/Juego";
        const string PrefabPath = Folder + "/Prefabs/HUD_Museo.prefab";
        const string FilmMatPath = "Assets/shaders/Cuando Nadie Mira_Pelicula Antigua 1.mat";
        const string FeedMatPath = MenuAssetFactory.MatFolder + "/UI_CameraFeedHud.mat";

        static readonly Color Panel = new(0f, 0f, 0f, 0.46f);
        static Sprite eyeAlmond, iris, gamepad;
        static Material feedMat;

        [MenuItem("Cuando Nadie Mira/Construir HUD del juego")]
        public static void Build()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            BuildInternal();
        }

        /// <summary>Para -batchmode -executeMethod.</summary>
        public static void BuildBatch() => BuildInternal();

        static void BuildInternal()
        {
            F.LoadShared();
            eyeAlmond = F.SpriteOr("Cursor_EyeAlmond", MenuTextureFactory.EyeAlmond);
            iris = F.SpriteOr("Cursor_Iris", MenuTextureFactory.Iris);
            gamepad = F.SpriteOr("Icon_Gamepad", () => MenuTextureFactory.Icon("Gamepad"));
            feedMat = FeedMaterial();

            EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);
            var scene = SceneManager.GetActiveScene();
            foreach (var go in scene.GetRootGameObjects().Where(g => g.name == "HUD_Museo")) Object.DestroyImmediate(go);

            var hud = BuildHud(TimeOutSprite());

            Directory.CreateDirectory(Path.GetDirectoryName(PrefabPath));
            PrefabUtility.SaveAsPrefabAssetAndConnect(hud, PrefabPath, InteractionMode.AutomatedAction);

            // HUD anterior: se oculta, no se borra (los párpados de CanvasParpados siguen funcionando)
            foreach (var root in scene.GetRootGameObjects())
            {
                if (root.name == "CanvasHUD") root.SetActive(false);
                if (root.name == "CanvasParpados")
                {
                    var panel = root.transform.Find("PanelContenedor");
                    if (panel != null) panel.gameObject.SetActive(false);
                }
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("[Cuando Nadie Mira] HUD del juego construido: " + PrefabPath);
        }

        static Sprite TimeOutSprite()
        {
            var old = Object.FindObjectsByType<HudJuego>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault();
            return old != null && old.imagenDerrota != null ? old.imagenDerrota.sprite : null;
        }

        static Material FeedMaterial()
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>(FeedMatPath);
            if (m == null)
            {
                m = new Material(Shader.Find("CuandoNadieMira/UI Grayscale")) { name = "UI_CameraFeedHud" };
                AssetDatabase.CreateAsset(m, FeedMatPath);
            }
            // El HUD (Overlay) no pasa por el post-proceso: solo se aclaran los medios para que la cara se lea
            m.SetFloat("_Gamma", 0.7f);
            m.SetFloat("_Gain", 1.25f);
            m.SetFloat("_Contrast", 1.15f);
            m.SetFloat("_Vignette", 0.3f);
            EditorUtility.SetDirty(m);
            return m;
        }

        // ==========================================================================================
        // HUD
        // ==========================================================================================

        static GameObject BuildHud(Sprite timeOut)
        {
            var go = new GameObject("HUD_Museo", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            go.layer = B.UILayer;
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;     // encima de los párpados y sin el post-proceso de la cámara
            canvas.sortingOrder = 10;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            var c = (RectTransform)go.transform;

            var audio = new GameObject("Audio");
            audio.transform.SetParent(c, false);
            audio.AddComponent<MenuAudio>();

            var root = B.Group("Contenido", c);
            root.blocksRaycasts = false;

            var hud = go.AddComponent<HudMuseo>();
            B.SetField(hud, "root", root);
            B.SetField(hud, "safeArea", (RectTransform)root.transform);
            B.SetField(hud, "film", AssetDatabase.LoadAssetAtPath<Material>(FilmMatPath));

            // Orden de dibujo: lo de abajo queda encima
            var veil = Stretched("VeloSinRostro", root.transform, null, new Color(0f, 0f, 0f, 0.55f), group: true);
            B.SetField(hud, "faceVeil", veil.GetComponent<CanvasGroup>());

            BuildObjective(root.transform, hud);
            var dawn = go.AddComponent<HudAmanecer>();
            BuildDawn(root.transform, dawn);
            BuildPresences(root.transform, dawn);
            BuildEyes(root.transform);
            BuildControls(root.transform);
            BuildPrompt(root.transform, hud);
            BuildBanner(root.transform, hud);
            BuildFace(root.transform, hud);

            var final = BuildFinal(c, timeOut);
            BuildPause(c, final);

            var black = Stretched("Negro", c, null, Color.black, group: true);
            B.SetField(hud, "black", black.GetComponent<CanvasGroup>());
            return go;
        }

        // ---------------------------------------------------------------- objetivo (arriba a la izquierda)
        static void BuildObjective(Transform parent, HudMuseo hud)
        {
            var box = Corner("Objetivo", parent, new Vector2(0f, 1f), new Vector2(36f, -26f), new Vector2(470f, 152f), true);
            Backdrop(box);
            Kicker("Titulo", box, "OBJETIVO", 22f, -16f, 300f);
            TopImg("Ornamento", box, B.sprOrnament, 22f, -42f, 280f, 14f, 0.5f);
            var text = TopText("Texto", box, B.fontBody, "", 23f, 22f, -58f, 430f, 84f, TextAlignmentOptions.TopLeft, 0.9f);
            text.lineSpacing = -8f;
            B.SetField(hud, "objective", box.GetComponent<CanvasGroup>());
            B.SetField(hud, "objectiveText", text);
        }

        // ---------------------------------------------------------------- hasta el amanecer (arriba al centro)
        static void BuildDawn(Transform parent, HudAmanecer dawn)
        {
            var box = Corner("Amanecer", parent, new Vector2(0.5f, 1f), new Vector2(0f, -26f), new Vector2(820f, 126f), true);
            Backdrop(box);
            var kicker = F.Label("Titulo", box, B.fontCaps, "HASTA EL AMANECER", 16f, 24f, 36f, 260f, 26f, TextAlignmentOptions.MidlineLeft, 0.62f);
            kicker.characterSpacing = 6f;
            var clock = F.Label("Reloj", box, B.fontTitle, "00:00", 46f, 260f, 36f, 300f, 56f, TextAlignmentOptions.Center);
            var pct = F.Label("Porcentaje", box, B.fontCaps, "0 %", 18f, 536f, 36f, 260f, 26f, TextAlignmentOptions.MidlineRight, 0.7f);

            var moon = F.Icon("Luna", box, "Moon", 22f, -8f, 40f);
            var sun = F.Icon("Sol", box, "Sun", 758f, -8f, 40f);
            var bar = F.Bar("Barra", box, 76f, -8f, 668f, 8f, 0.14f, out var fill);
            var tick = B.Img("Marca", bar.rectTransform, B.sprFill, Vector2.zero, new Vector2(2f, 20f), new Color(1f, 1f, 1f, 0.45f));
            tick.raycastTarget = false;
            var head = B.Img("Cabeza", bar.rectTransform, B.sprCircle, Vector2.zero, new Vector2(16f, 16f), Color.white);
            head.raycastTarget = false;
            var trend = F.Label("Tendencia", box, B.fontBody, "", 21f, 160f, -42f, 500f, 30f, TextAlignmentOptions.Center, 0.78f, FontStyles.Italic);

            B.SetField(dawn, "dawn", box.GetComponent<CanvasGroup>());
            B.SetField(dawn, "fill", fill);
            B.SetField(dawn, "head", head);
            B.SetField(dawn, "moon", moon);
            B.SetField(dawn, "sun", sun);
            B.SetField(dawn, "bar", bar.rectTransform);
            B.SetField(dawn, "clock", clock);
            B.SetField(dawn, "percent", pct);
            B.SetField(dawn, "trend", trend);
            B.SetField(dawn, "tickTemplate", tick);
        }

        // ---------------------------------------------------------------- presencias (arriba a la derecha)
        static void BuildPresences(Transform parent, HudAmanecer dawn)
        {
            const float rowH = 50f, top = 66f;
            var box = Corner("Presencias", parent, new Vector2(1f, 1f), new Vector2(-36f, -26f), new Vector2(360f, top + rowH + 6f), true);
            Backdrop(box);
            Kicker("Titulo", box, "PRESENCIAS", 22f, -16f, 300f);
            TopImg("Ornamento", box, B.sprOrnament, 22f, -42f, 260f, 14f, 0.5f);

            var row = new GameObject("Fila", typeof(RectTransform));
            row.layer = B.UILayer;
            var rrt = (RectTransform)row.transform;
            rrt.SetParent(box, false);
            rrt.anchorMin = rrt.anchorMax = rrt.pivot = new Vector2(0f, 1f);
            rrt.sizeDelta = new Vector2(360f, rowH);
            rrt.anchoredPosition = new Vector2(0f, -top);
            F.Label("Nombre", rrt, B.fontBody, "Estatua", 24f, 22f, 9f, 200f, 28f, TextAlignmentOptions.MidlineLeft, 0.92f)
                .fontWeight = FontWeight.SemiBold;
            F.Label("Estado", rrt, B.fontBody, "en su sitio", 17f, 22f, -13f, 200f, 20f, TextAlignmentOptions.MidlineLeft, 0.7f, FontStyles.Italic);
            var pip = B.Img("Marca", rrt, B.sprCircle, new Vector2(232f, 0f), new Vector2(13f, 13f), Color.white);
            pip.raycastTarget = false;

            B.SetField(dawn, "presences", box.GetComponent<CanvasGroup>());
            B.SetField(dawn, "presencesBox", box);
            B.SetField(dawn, "rowTemplate", rrt);
            B.SetField(dawn, "pipFull", B.sprCircle);
            B.SetField(dawn, "pipEmpty", F.ring);
            B.SetField(dawn, "rowHeight", rowH);
            B.SetField(dawn, "rowsTop", top);
        }

        // ---------------------------------------------------------------- lectura de ojos (abajo a la izquierda)
        static void BuildEyes(Transform parent)
        {
            var box = Corner("LecturaOjos", parent, new Vector2(0f, 0f), new Vector2(36f, 36f), new Vector2(586f, 204f), true);
            Backdrop(box);
            var glow = Stretched("Destello", box, B.sprFill, new Color(1f, 1f, 1f, 0.02f));
            var ojos = box.gameObject.AddComponent<HudOjos>();

            // Tu cámara (la imagen que manda ojos_server al puerto 5006)
            var portrait = Child("Retrato", box, new Vector2(16f, 0f), new Vector2(232f, 174f), true);
            Stretched("Fondo", portrait, B.sprFill, new Color(0f, 0f, 0f, 0.6f));
            F.Icon("SinImagen", portrait, "Camera", 94f, 16f, 44f, 0.35f);
            F.Label("SinImagenTexto", portrait, B.fontBody, "sin imagen", 18f, 0f, -26f, 232f, 24f, TextAlignmentOptions.Center, 0.45f, FontStyles.Italic);
            var feed = new GameObject("Camara", typeof(RectTransform), typeof(RawImage));
            feed.layer = B.UILayer;
            var frt = (RectTransform)feed.transform;
            frt.SetParent(portrait, false);
            StretchRect(frt);
            var raw = feed.GetComponent<RawImage>();
            raw.material = feedMat;
            raw.raycastTarget = false;
            raw.enabled = false;                                     // VistaCamara la enciende con la primera imagen
            var view = feed.AddComponent<VistaCamara>();
            view.destino = raw;
            Stretched("Borde", portrait, B.sprOutline, new Color(1f, 1f, 1f, 0.35f));

            // Modo teclado: en lugar de la cámara, la tecla que cierra los ojos
            var kb = Child("Teclado", box, new Vector2(16f, 0f), new Vector2(232f, 174f), true);
            Stretched("Fondo", kb, B.sprFill, new Color(1f, 1f, 1f, 0.04f));
            Stretched("Borde", kb, B.sprOutline, new Color(1f, 1f, 1f, 0.25f));
            var key = KeyCap(kb, "C", 76f, 14f, 80f, out _);
            F.Label("Ayuda", kb, B.fontBody, "mantén para cerrar", 19f, 0f, -56f, 232f, 26f, TextAlignmentOptions.Center, 0.7f, FontStyles.Italic);

            const float x0 = 266f;
            var header = F.Label("Titulo", box, B.fontCaps, "LECTURA DE OJOS", 15f, x0, 80f, 290f, 24f, TextAlignmentOptions.MidlineLeft, 0.62f);
            header.characterSpacing = 5f;
            var dot = B.Img("EnVivo", box, B.sprCircle, new Vector2(556f, 80f), new Vector2(10f, 10f), Color.white);
            dot.raycastTarget = false;

            var eye = Child("Ojo", box, new Vector2(x0, 36f), new Vector2(96f, 48f), false);
            var outline = B.Img("Contorno", eye, eyeAlmond, Vector2.zero, new Vector2(96f, 48f), Color.white);
            outline.raycastTarget = false;
            var irisImg = B.Img("Iris", eye, iris, new Vector2(33f, 0f), new Vector2(30f, 30f), Color.white);
            irisImg.raycastTarget = false;

            var state = F.Label("Estado", box, B.fontTitle, "Abiertos", 40f, x0 + 110f, 44f, 210f, 48f);
            var detail = F.Label("Detalle", box, B.fontBody, "", 19f, x0 + 112f, 11f, 210f, 26f, TextAlignmentOptions.MidlineLeft, 0.7f, FontStyles.Italic);
            detail.textWrappingMode = TextWrappingModes.NoWrap;
            detail.overflowMode = TextOverflowModes.Ellipsis;

            var barBg = F.Bar("BarraP", box, x0, -24f, 236f, 8f, 0.15f, out var pFill);
            var closeMark = B.Img("UmbralCierre", barBg.rectTransform, B.sprFill, Vector2.zero, new Vector2(3f, 20f), new Color(1f, 1f, 1f, 0.9f));
            var openMark = B.Img("UmbralApertura", barBg.rectTransform, B.sprFill, Vector2.zero, new Vector2(3f, 20f), new Color(1f, 1f, 1f, 0.45f));
            closeMark.raycastTarget = openMark.raycastTarget = false;
            var value = F.Label("ValorP", box, B.fontCaps, "--", 17f, x0 + 246f, -24f, 70f, 24f, TextAlignmentOptions.MidlineLeft, 0.85f);
            var meter = box.gameObject.AddComponent<EyeMeter>();
            meter.Configure(pFill, closeMark.rectTransform, openMark.rectTransform, null, value);

            var thresholds = F.Label("Umbrales", box, B.fontBody, "", 18f, x0, -48f, 310f, 24f, TextAlignmentOptions.MidlineLeft, 0.55f);
            var calIcon = F.Icon("CalibracionIcono", box, "Check", x0 - 2f, -78f, 24f, 0.8f);
            var cal = F.Label("Calibracion", box, B.fontBody, "", 19f, x0 + 28f, -78f, 170f, 26f, TextAlignmentOptions.MidlineLeft, 0.78f);
            var blinks = F.Label("Parpadeos", box, B.fontBody, "", 19f, x0 + 170f, -78f, 136f, 26f, TextAlignmentOptions.MidlineRight, 0.6f);

            B.SetField(ojos, "group", box.GetComponent<CanvasGroup>());
            B.SetField(ojos, "eye", eye);
            B.SetField(ojos, "eyeOutline", outline);
            B.SetField(ojos, "iris", irisImg);
            B.SetField(ojos, "state", state);
            B.SetField(ojos, "detail", detail);
            B.SetField(ojos, "thresholds", thresholds);
            B.SetField(ojos, "calibration", cal);
            B.SetField(ojos, "blinks", blinks);
            B.SetField(ojos, "header", header);
            B.SetField(ojos, "calibrationIcon", calIcon);
            B.SetField(ojos, "liveDot", dot);
            B.SetField(ojos, "iconOk", F.icons["Check"]);
            B.SetField(ojos, "iconWarning", F.icons["Warning"]);
            B.SetField(ojos, "meter", meter);
            B.SetField(ojos, "portrait", portrait.GetComponent<CanvasGroup>());
            B.SetField(ojos, "keyboardCard", kb.GetComponent<CanvasGroup>());
            B.SetField(ojos, "keyboardKey", key);
            B.SetField(ojos, "stateGlow", glow);
        }

        // ---------------------------------------------------------------- controles (abajo a la derecha)
        static void BuildControls(Transform parent)
        {
            var holder = Corner("Controles", parent, new Vector2(1f, 0f), new Vector2(-36f, 36f), Vector2.zero, false);
            var ctl = holder.gameObject.AddComponent<HudControles>();

            var explore = ControlsPanel(holder, "Explorar", "CONTROLES  ·  EXPLORAR");
            Row(explore, "Caminar", "W", "A", "S", "D");
            Row(explore, "Correr", "Shift");
            Row(explore, "Saltar", "Espacio");
            Row(explore, "Mirar", "@Mouse");
            var interact = Row(explore, "Comenzar el turno (en el escritorio)", "E");
            Row(explore, "Pausa", "Esc");
            Row(explore, "Ocultar controles", "H");

            var play = ControlsPanel(holder, "Turno", "CONTROLES  ·  TURNO");
            Row(play, "Cierra los ojos: la noche avanza", "@EyeClosed");
            Row(play, "Ábrelos: ellas retroceden", "@Eye");
            var kbRow = Row(play, "Mantén: ojos cerrados (modo teclado)", "C");
            Row(play, "Mirar alrededor", "@Mouse");
            Row(play, "Pausa", "Esc");
            Row(play, "Ocultar controles", "H");

            var compact = Corner("Compacto", holder, new Vector2(1f, 0f), Vector2.zero, new Vector2(212f, 54f), true);
            Backdrop(compact);
            KeyCap(compact, "H", 14f, 0f, 34f, out _);
            F.Label("Texto", compact, B.fontBody, "Controles", 23f, 62f, 0f, 140f, 34f, TextAlignmentOptions.MidlineLeft, 0.85f);

            B.SetField(ctl, "explore", explore.GetComponent<CanvasGroup>());
            B.SetField(ctl, "play", play.GetComponent<CanvasGroup>());
            B.SetField(ctl, "compact", compact.GetComponent<CanvasGroup>());
            B.SetField(ctl, "keyboardRow", kbRow.row.gameObject);
            B.SetField(ctl, "interactKey", interact.key);
            B.SetField(ctl, "closeKey", kbRow.key);
        }

        static RectTransform ControlsPanel(Transform parent, string name, string title)
        {
            var p = Corner(name, parent, new Vector2(1f, 0f), Vector2.zero, new Vector2(420f, 0f), true);
            var layout = p.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(22, 22, 16, 14);
            layout.spacing = 2f;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            var fitter = p.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var bg = Stretched("Fondo", p, B.sprFill, Panel);
            bg.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            var border = Stretched("Borde", p, B.sprOutline, new Color(1f, 1f, 1f, 0.22f));
            border.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;

            var head = NewRect("Titulo", p);
            head.gameObject.AddComponent<LayoutElement>().preferredHeight = 34f;
            var t = F.Label("Texto", head, B.fontCaps, title, 15f, 0f, 2f, 376f, 26f, TextAlignmentOptions.MidlineLeft, 0.62f);
            t.characterSpacing = 5f;
            return p;
        }

        struct RowRef { public RectTransform row; public TMP_Text key; }

        /// <summary>Fila de controles: teclas dibujadas (o "@Icono") y lo que hacen.</summary>
        static RowRef Row(Transform parent, string label, params string[] keys)
        {
            var rt = NewRect("Fila_" + label.Split(' ')[0].Split(':')[0], parent);
            rt.gameObject.AddComponent<LayoutElement>().preferredHeight = 44f;
            B.Place(rt, Vector2.zero, new Vector2(560f, 44f));
            float x = 0f;
            TMP_Text last = null;
            foreach (var k in keys)
            {
                if (k.StartsWith("@")) { F.Icon("Icono", rt, k.Substring(1), x - 2f, 0f, 36f, 0.9f); x += 40f; continue; }
                last = KeyCap(rt, k, x, 0f, 34f, out float w);
                x += w + 5f;
            }
            F.Label("Texto", rt, B.fontBody, label, 22f, x + 10f, 0f, 380f - x, 36f, TextAlignmentOptions.MidlineLeft, 0.9f)
                .textWrappingMode = TextWrappingModes.NoWrap;
            return new RowRef { row = rt, key = last };
        }

        /// <summary>Una tecla dibujada con el borde del menú.</summary>
        static TMP_Text KeyCap(Transform parent, string key, float x, float y, float h, out float width)
        {
            width = Mathf.Max(h, h * 0.55f + key.Length * h * 0.34f);
            var bg = B.Img("Tecla_" + key, parent, B.sprFill, new Vector2(x, y), new Vector2(width, h), new Color(1f, 1f, 1f, 0.08f));
            bg.type = Image.Type.Sliced;
            bg.raycastTarget = false;
            var border = B.Img("Borde", bg.rectTransform, B.sprOutline, Vector2.zero, new Vector2(width, h), new Color(1f, 1f, 1f, 0.6f));
            border.type = Image.Type.Sliced;
            border.raycastTarget = false;
            var t = F.Label("Letra", bg.rectTransform, B.fontCaps, key, h * 0.42f, 0f, 1f, width, h, TextAlignmentOptions.Center, 0.95f);
            t.textWrappingMode = TextWrappingModes.NoWrap;
            return t;
        }

        // ---------------------------------------------------------------- aviso para empezar el turno
        static void BuildPrompt(Transform parent, HudMuseo hud)
        {
            var g = Corner("AvisoInicio", parent, new Vector2(0.5f, 0f), new Vector2(0f, 250f), Vector2.zero, true);
            var box = F.Container("Caja", g, Vector2.zero);
            var bg = B.Img("Fondo", box, B.sprFill, new Vector2(-310f, 0f), new Vector2(620f, 96f), new Color(0f, 0f, 0f, 0.58f));
            bg.type = Image.Type.Sliced;
            bg.raycastTarget = false;
            var br = B.Img("Borde", box, B.sprOutline, new Vector2(-310f, 0f), new Vector2(620f, 96f), new Color(1f, 1f, 1f, 0.42f));
            br.type = Image.Type.Sliced;
            br.raycastTarget = false;
            var key = KeyCap(box, "E", -286f, 0f, 58f, out _);
            F.Label("Texto", box, B.fontTitle, "Comenzar el turno", 36f, -206f, 14f, 500f, 44f);
            F.Label("Sub", box, B.fontBody, "Tu puesto: el escritorio del guarda", 21f, -204f, -22f, 500f, 28f, TextAlignmentOptions.MidlineLeft, 0.7f, FontStyles.Italic);

            B.SetField(hud, "prompt", g.GetComponent<CanvasGroup>());
            B.SetField(hud, "promptBox", box);
            B.SetField(hud, "promptKey", key);
        }

        // ---------------------------------------------------------------- carteles de fase
        static void BuildBanner(Transform parent, HudMuseo hud)
        {
            var g = F.Container("Cartel", parent, new Vector2(0f, 200f), true);
            var title = F.Title("Titulo", g, "Comienza el turno", 92f, 20f, 130f);
            F.Ornament(g, -50f);
            var sub = F.Label("Sub", g, B.fontBody, "", 30f, -600f, -92f, 1200f, 44f, TextAlignmentOptions.Center, 0.88f, FontStyles.Italic);
            B.SetField(hud, "banner", g.GetComponent<CanvasGroup>());
            B.SetField(hud, "bannerTitle", title);
            B.SetField(hud, "bannerSub", sub);
        }

        // ---------------------------------------------------------------- rostro perdido / recuperado
        static void BuildFace(Transform parent, HudMuseo hud)
        {
            var lost = F.Container("SinRostro", parent, new Vector2(0f, 40f), true);
            var bg = B.Img("Fondo", lost, B.sprFill, new Vector2(-440f, 0f), new Vector2(880f, 340f), new Color(0f, 0f, 0f, 0.62f));
            bg.type = Image.Type.Sliced;
            bg.raycastTarget = false;
            var br = B.Img("Borde", lost, B.sprOutline, new Vector2(-440f, 0f), new Vector2(880f, 340f), new Color(1f, 1f, 1f, 0.35f));
            br.type = Image.Type.Sliced;
            br.raycastTarget = false;
            F.Icon("Icono", lost, "EyeClosed", -30f, 116f, 60f, 0.9f);
            F.Label("Titulo", lost, B.fontTitle, "Te perdimos de vista", 54f, -420f, 56f, 840f, 64f, TextAlignmentOptions.Center);
            F.Ornament(lost, 14f);
            var reason = F.Label("Motivo", lost, B.fontBody, "", 30f, -420f, -28f, 840f, 40f, TextAlignmentOptions.Center, 0.92f);
            var advice = F.Label("Consejo", lost, B.fontBody, "", 24f, -420f, -70f, 840f, 36f, TextAlignmentOptions.Center, 0.68f, FontStyles.Italic);
            var chip = B.Img("Pausa", lost, B.sprFill, new Vector2(-180f, -128f), new Vector2(360f, 44f), new Color(1f, 1f, 1f, 0.06f));
            chip.type = Image.Type.Sliced;
            chip.raycastTarget = false;
            var chipBr = B.Img("Borde", chip.rectTransform, B.sprOutline, Vector2.zero, new Vector2(360f, 44f), new Color(1f, 1f, 1f, 0.4f));
            chipBr.type = Image.Type.Sliced;
            chipBr.raycastTarget = false;
            var pauseText = F.Label("Texto", chip.rectTransform, B.fontCaps, "LA PARTIDA ESTÁ EN PAUSA", 15f, 0f, 1f, 360f, 44f, TextAlignmentOptions.Center, 0.85f);
            pauseText.characterSpacing = 4f;

            var back = F.Container("RostroRecuperado", parent, new Vector2(0f, 40f), true);
            var bbg = B.Img("Fondo", back, B.sprFill, new Vector2(-270f, 0f), new Vector2(540f, 112f), new Color(0f, 0f, 0f, 0.6f));
            bbg.type = Image.Type.Sliced;
            bbg.raycastTarget = false;
            var bbr = B.Img("Borde", back, B.sprOutline, new Vector2(-270f, 0f), new Vector2(540f, 112f), new Color(1f, 1f, 1f, 0.35f));
            bbr.type = Image.Type.Sliced;
            bbr.raycastTarget = false;
            F.Icon("Icono", back, "Eye", -244f, 0f, 52f, 0.95f);
            F.Label("Titulo", back, B.fontTitle, "Te volvemos a ver", 40f, -176f, 14f, 420f, 48f);
            F.Label("Sub", back, B.fontBody, "La noche continúa.", 22f, -174f, -24f, 420f, 30f, TextAlignmentOptions.MidlineLeft, 0.7f, FontStyles.Italic);

            B.SetField(hud, "faceLost", lost.GetComponent<CanvasGroup>());
            B.SetField(hud, "faceBack", back.GetComponent<CanvasGroup>());
            B.SetField(hud, "faceReason", reason);
            B.SetField(hud, "faceAdvice", advice);
        }

        // ---------------------------------------------------------------- victoria y derrota
        static HudFinal BuildFinal(RectTransform canvas, Sprite timeOut)
        {
            var rootRt = NewRect("Final", canvas);                    // solo contenedor; cada pantalla lleva su grupo
            StretchRect(rootRt);
            var fin = rootRt.gameObject.AddComponent<HudFinal>();

            // Victoria
            var v = B.Group("Victoria", rootRt);
            Stretched("Fondo", v.transform, null, new Color(0f, 0f, 0f, 0.82f));
            var vc = F.Container("Centro", v.transform, Vector2.zero);
            F.Icon("Sol", vc, "Sun", -40f, 306f, 80f, 0.95f);
            Caps("Hora", vc, "06:00  ·  FIN DEL TURNO", 19f, -400f, 236f, 800f, TextAlignmentOptions.Center);
            F.Title("Titulo", vc, "Amaneció", 140f, 140f, 170f);
            F.Ornament(vc, 40f);
            F.Label("Sub", vc, B.fontBody, "Las estatuas vuelven a su sitio. Sobreviviste a la noche.", 32f, -600f, -10f, 1200f, 44f,
                TextAlignmentOptions.Center, 0.88f, FontStyles.Italic);
            var vStats = Stats(vc, new[] { -450f, -150f, 150f }, TextAlignmentOptions.Center, -118f);
            var vAgain = Button(vc, "Otra noche", "Retry", -380f, -268f, 360f);
            var vMenu = Button(vc, "Menú principal", "Home", 20f, -268f, 360f);
            F.Label("Ayuda", vc, B.fontBody, "Pulsa R para jugar otra noche", 21f, -400f, -346f, 800f, 30f, TextAlignmentOptions.Center, 0.55f, FontStyles.Italic);

            // Derrota
            var d = B.Group("Derrota", rootRt);
            Stretched("Fondo", d.transform, null, new Color(0f, 0f, 0f, 0.86f));
            var dc = F.Container("Centro", d.transform, Vector2.zero);
            var frame = Child("Marco", dc, new Vector2(-820f, 0f), new Vector2(460f, 600f), false);
            Stretched("Fondo", frame, B.sprFill, new Color(0f, 0f, 0f, 0.6f));
            var img = new GameObject("Imagen", typeof(RectTransform), typeof(Image));
            img.layer = B.UILayer;
            var irt = (RectTransform)img.transform;
            irt.SetParent(frame, false);
            irt.anchorMin = Vector2.zero;
            irt.anchorMax = Vector2.one;
            irt.offsetMin = new Vector2(14f, 14f);
            irt.offsetMax = new Vector2(-14f, -14f);
            var dImg = img.GetComponent<Image>();
            dImg.preserveAspect = true;
            dImg.raycastTarget = false;
            dImg.sprite = timeOut;
            dImg.material = feedMat;                                  // en blanco y negro, como el resto del HUD
            Stretched("Borde", frame, B.sprOutline, new Color(1f, 1f, 1f, 0.45f));

            var dt = F.Container("Texto", dc, Vector2.zero);
            var kicker = Caps("Hora", dt, "FIN DEL TURNO", 19f, -300f, 232f, 1000f, TextAlignmentOptions.MidlineLeft);
            var title = F.Label("Titulo", dt, B.fontTitle, "La noche se hizo eterna", 104f, -300f, 142f, 1100f, 140f, TextAlignmentOptions.MidlineLeft);
            title.fontSharedMaterial = F.titleMat;
            title.characterSpacing = -3f;
            title.textWrappingMode = TextWrappingModes.NoWrap;
            title.enableAutoSizing = true;
            title.fontSizeMin = 56f;
            title.fontSizeMax = 104f;
            var orn = B.Img("Ornamento", dt, B.sprOrnament, new Vector2(-300f, 52f), new Vector2(520f, 16f), new Color(1f, 1f, 1f, 0.45f));
            orn.raycastTarget = false;
            var sub = F.Label("Sub", dt, B.fontBody, "", 32f, -300f, -8f, 1000f, 84f, TextAlignmentOptions.TopLeft, 0.88f, FontStyles.Italic);
            var dStats = Stats(dt, new[] { -300f, 20f, 340f }, TextAlignmentOptions.MidlineLeft, -122f);
            var dAgain = Button(dt, "Reintentar", "Retry", -300f, -268f, 380f);
            var dMenu = Button(dt, "Menú principal", "Home", 100f, -268f, 380f);
            F.Label("Ayuda", dt, B.fontBody, "Pulsa R para reintentar", 21f, -300f, -346f, 800f, 30f, TextAlignmentOptions.MidlineLeft, 0.55f, FontStyles.Italic);

            B.SetField(fin, "victory", v);
            B.SetField(fin, "vDuration", vStats[0]);
            B.SetField(fin, "vClosed", vStats[1]);
            B.SetField(fin, "vBlinks", vStats[2]);
            B.SetField(fin, "vAgain", vAgain);
            B.SetField(fin, "vMenu", vMenu);
            B.SetField(fin, "defeat", d);
            B.SetField(fin, "dKicker", kicker);
            B.SetField(fin, "dTitle", title);
            B.SetField(fin, "dSub", sub);
            B.SetField(fin, "dDuration", dStats[0]);
            B.SetField(fin, "dClosed", dStats[1]);
            B.SetField(fin, "dBlinks", dStats[2]);
            B.SetField(fin, "dImage", dImg);
            B.SetField(fin, "dFrame", frame.gameObject);
            B.SetField(fin, "dText", dt);
            B.SetField(fin, "timeOutImage", timeOut);
            B.SetField(fin, "dAgain", dAgain);
            B.SetField(fin, "dMenu", dMenu);
            return fin;
        }

        static TMP_Text[] Stats(Transform parent, float[] xs, TextAlignmentOptions align, float y)
        {
            string[] labels = { "DURACIÓN DEL TURNO", "CON LOS OJOS CERRADOS", "PARPADEOS" };
            var values = new TMP_Text[3];
            for (int i = 0; i < 3; i++)
            {
                values[i] = F.Label("Valor_" + i, parent, B.fontTitle, "--", 54f, xs[i], y + 20f, 300f, 62f, align);
                Caps("Etiqueta_" + i, parent, labels[i], 14f, xs[i], y - 30f, 300f, align);
                if (i > 0)
                {
                    float sx = align == TextAlignmentOptions.Center ? xs[i] : xs[i] - 22f;
                    var sep = B.Img("Separador_" + i, parent, B.sprFill, new Vector2(sx, y), new Vector2(2f, 74f), new Color(1f, 1f, 1f, 0.22f));
                    sep.raycastTarget = false;
                }
            }
            return values;
        }

        // ---------------------------------------------------------------- pausa
        static void BuildPause(RectTransform canvas, HudFinal final)
        {
            var g = B.Group("Pausa", canvas);
            var pause = g.gameObject.AddComponent<HudPausa>();
            Stretched("Fondo", g.transform, null, new Color(0f, 0f, 0f, 0.8f));

            var buttons = F.Container("Botones", g.transform, Vector2.zero, true);
            F.Icon("Icono", buttons, "Pause", -30f, 318f, 60f, 0.9f);
            F.Title("Titulo", buttons, "Pausa", 120f, 222f, 150f);
            F.Ornament(buttons, 130f);
            var resume = Button(buttons, "Continuar", "Play", -260f, 40f, 520f);
            var controls = Button(buttons, "Controles", null, -260f, -60f, 520f, gamepad);
            var recal = Button(buttons, "Recalibrar los ojos", "Eye", -260f, -160f, 520f);
            var menu = Button(buttons, "Menú principal", "Home", -260f, -260f, 520f);
            F.Label("Ayuda", buttons, B.fontBody, "Esc para continuar  ·  la lectura de ojos sigue activa", 22f, -500f, -344f, 1000f, 30f,
                TextAlignmentOptions.Center, 0.55f, FontStyles.Italic);

            var panel = F.Container("PanelControles", g.transform, Vector2.zero, true);
            F.Title("Titulo", panel, "Controles", 92f, 300f, 120f);
            F.Ornament(panel, 228f);
            var colA = Child("Explorar", panel, new Vector2(-585f, 0f), new Vector2(600f, 400f), false);
            var colB = Child("Turno", panel, new Vector2(135f, 0f), new Vector2(600f, 400f), false);
            Column(colA, "EXPLORAR EL MUSEO", new[]
            {
                ("Caminar", new[] { "W", "A", "S", "D" }), ("Correr", new[] { "Shift" }), ("Saltar", new[] { "Espacio" }),
                ("Mirar", new[] { "@Mouse" }), ("Comenzar el turno, junto al escritorio", new[] { "E" }),
                ("Mostrar u ocultar los controles", new[] { "H" }), ("Pausa", new[] { "Esc" }),
            });
            Column(colB, "DURANTE EL TURNO", new[]
            {
                ("Ojos cerrados: la noche avanza, y ellas también", new[] { "@EyeClosed" }),
                ("Ojos abiertos un momento: retroceden", new[] { "@Eye" }),
                ("Si no te vemos, la partida se pausa sola", new[] { "@Warning" }),
                ("Mirar alrededor", new[] { "@Mouse" }),
                ("Modo teclado: mantener = ojos cerrados", new[] { "C" }),
                ("Reintentar al terminar", new[] { "R" }),
                ("Pausa", new[] { "Esc" }),
            });
            var back = Button(panel, "Volver", "ChevronLeft", -180f, -330f, 360f, chevron: false);

            B.SetField(pause, "group", g);
            B.SetField(pause, "buttons", buttons.GetComponent<CanvasGroup>());
            B.SetField(pause, "controls", panel.GetComponent<CanvasGroup>());
            B.SetField(pause, "resume", resume);
            B.SetField(pause, "showControls", controls);
            B.SetField(pause, "recalibrate", recal);
            B.SetField(pause, "mainMenu", menu);
            B.SetField(pause, "back", back);
            B.SetField(pause, "final", final);
        }

        static void Column(RectTransform col, string title, (string label, string[] keys)[] rows)
        {
            Caps("Titulo", col, title, 16f, 0f, 170f, 600f, TextAlignmentOptions.MidlineLeft);
            for (int i = 0; i < rows.Length; i++)
            {
                var r = Row(col, rows[i].label, rows[i].keys);
                Object.DestroyImmediate(r.row.GetComponent<LayoutElement>());
                B.Place(r.row, new Vector2(0f, 118f - i * 50f), new Vector2(600f, 44f));
                var text = r.row.Find("Texto") as RectTransform;
                if (text != null) text.sizeDelta = new Vector2(600f - text.anchoredPosition.x, text.sizeDelta.y);
            }
        }

        // ==========================================================================================
        // Ayudantes
        // ==========================================================================================

        static MenuButton Button(Transform parent, string label, string icon, float x, float y, float w, Sprite iconSprite = null, bool chevron = true)
        {
            var b = B.Button(parent, label, iconSprite != null ? iconSprite : icon != null ? F.icons[icon] : null, chevron ? F.icons["Chevron"] : null,
                new Vector2(x, y), new Vector2(w, 80f), false, null, null, 32f);
            var embers = b.transform.Find("Embers");                // las brasas son partículas: no se ven en un canvas Overlay
            if (embers != null) Object.DestroyImmediate(embers.gameObject);
            return b;
        }

        static TMP_Text Caps(string name, Transform parent, string text, float size, float x, float y, float w, TextAlignmentOptions align)
        {
            var t = F.Label(name, parent, B.fontCaps, text, size, x, y, w, 28f, align, 0.65f);
            t.characterSpacing = 5f;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            return t;
        }

        static TMP_Text Kicker(string name, RectTransform parent, string text, float x, float y, float w)
        {
            var t = TopText(name, parent, B.fontCaps, text, 15f, x, y, w, 24f, TextAlignmentOptions.MidlineLeft, 0.62f);
            t.characterSpacing = 6f;
            return t;
        }

        static TMP_Text TopText(string name, Transform parent, TMP_FontAsset font, string text, float size, float x, float y, float w, float h,
            TextAlignmentOptions align, float gray)
        {
            var t = F.Label(name, parent, font, text, size, 0f, 0f, w, h, align, gray);
            TopLeft(t.rectTransform, x, y);
            return t;
        }

        static Image TopImg(string name, Transform parent, Sprite s, float x, float y, float w, float h, float alpha)
        {
            var i = B.Img(name, parent, s, Vector2.zero, new Vector2(w, h), new Color(1f, 1f, 1f, alpha));
            i.raycastTarget = false;
            TopLeft(i.rectTransform, x, y);
            return i;
        }

        static void TopLeft(RectTransform rt, float x, float y)
        {
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(x, y);
        }

        static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = B.UILayer;
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            return rt;
        }

        /// <summary>Rectángulo anclado a una esquina (o borde) de la pantalla; pivote en esa misma esquina.</summary>
        static RectTransform Corner(string name, Transform parent, Vector2 anchor, Vector2 pos, Vector2 size, bool group)
        {
            var rt = NewRect(name, parent);
            rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
            rt.sizeDelta = size;
            rt.anchoredPosition = pos;
            if (group) rt.gameObject.AddComponent<CanvasGroup>();
            return rt;
        }

        /// <summary>Hijo con el sistema de coordenadas de B.Place (borde izquierdo, centro vertical del padre).</summary>
        static RectTransform Child(string name, Transform parent, Vector2 pos, Vector2 size, bool group)
        {
            var rt = NewRect(name, parent);
            B.Place(rt, pos, size);
            if (group) rt.gameObject.AddComponent<CanvasGroup>();
            return rt;
        }

        static void StretchRect(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        static Image Stretched(string name, Transform parent, Sprite sprite, Color color, bool group = false)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.layer = B.UILayer;
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            StretchRect(rt);
            var img = go.GetComponent<Image>();
            img.sprite = sprite;
            img.type = sprite != null ? Image.Type.Sliced : Image.Type.Simple;
            img.color = color;
            img.raycastTarget = false;
            if (group) go.AddComponent<CanvasGroup>();
            return img;
        }

        static void Backdrop(RectTransform box)
        {
            Stretched("Fondo", box, B.sprFill, Panel);
            Stretched("Borde", box, B.sprOutline, new Color(1f, 1f, 1f, 0.22f));
        }
    }
}
