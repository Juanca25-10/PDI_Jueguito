using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace CuandoNadieMira.Menu.EditorTools
{
    /// <summary>
    /// Genera por código las texturas y sprites del menú (iconos lineales, bordes, ruido, grietas...)
    /// y los guarda como PNG para que el equipo pueda editarlos después.
    /// </summary>
    public static class MenuTextureFactory
    {
        public const string Folder = "Assets/CuandoNadieMira/Menu/Textures/Generated";

        // --- API -----------------------------------------------------------------

        public static Texture2D Cookie() => Save("Cookie_CandleNoise", 256, 256, (u, v) =>
        {
            float r = Vector2.Distance(new Vector2(u, v), new Vector2(0.5f, 0.5f)) * 2f;
            float fall = Mathf.Clamp01(1f - SS(0.55f, 1f, r));
            float n = Fbm(u * 5f, v * 5f, 4);
            float g = fall * Mathf.Lerp(0.65f, 1f, n);
            return new Color(g, g, g, g);
        }, imp =>
        {
            imp.textureType = TextureImporterType.Cookie;
            imp.alphaSource = TextureImporterAlphaSource.FromGrayScale;
            imp.wrapMode = TextureWrapMode.Clamp;
        });

        public static Texture2D Cracks() => Save("Title_Cracks", 512, 256, (u, v) =>
        {
            float e = VoronoiEdge(u * 9f, v * 4.5f);
            float e2 = VoronoiEdge(u * 23f + 3.1f, v * 11f + 1.7f);
            float crack = 1f - SS(0f, 0.045f, e);    // 1 sobre la grieta
            float fine = 1f - SS(0f, 0.03f, e2);
            float wear = Mathf.Lerp(0.78f, 1f, Fbm(u * 14f, v * 7f, 3));
            float g = Mathf.Lerp(0.18f, 1f, (1f - crack * 0.8f) * (1f - fine * 0.45f) * wear);
            return new Color(g, g, g, 1f);
        }, imp => imp.wrapMode = TextureWrapMode.Repeat);

        public static Texture2D Dot() => Save("Particle_Dot", 64, 64, (u, v) =>
        {
            float r = Vector2.Distance(new Vector2(u, v), new Vector2(0.5f, 0.5f)) * 2f;
            float a = Mathf.Pow(Mathf.Clamp01(1f - r), 2.2f);
            return new Color(1f, 1f, 1f, a);
        }, imp => imp.wrapMode = TextureWrapMode.Clamp);

        public static Texture2D Footprint() => Save("Footprint_Dust", 128, 256, (u, v) =>
        {
            // Huella estilizada: planta ovalada + talón
            float sole = Ellipse(u, v, 0.5f, 0.62f, 0.3f, 0.3f);
            float heel = Ellipse(u, v, 0.5f, 0.2f, 0.24f, 0.16f);
            float shape = Mathf.Max(sole, heel);
            float grain = Mathf.Lerp(0.35f, 1f, Fbm(u * 18f, v * 18f, 3));
            return new Color(1f, 1f, 1f, shape * grain);
        }, imp => imp.wrapMode = TextureWrapMode.Clamp);

        public static Texture2D FogNoise() => Save("Fog_Noise", 256, 256, (u, v) =>
        {
            float n = Fbm(u * 4f, v * 4f, 5, tile: 4f);
            float a = SS(0.35f, 0.85f, n);
            return new Color(1f, 1f, 1f, a);
        }, imp => imp.wrapMode = TextureWrapMode.Repeat);

        public static Texture2D Shaft() => Save("Light_Shaft", 64, 256, (u, v) =>
        {
            float edge = Mathf.Sin(u * Mathf.PI);
            float a = edge * edge * Mathf.Pow(v, 1.6f) * Mathf.Lerp(0.7f, 1f, Fbm(u * 3f, v * 9f, 3));
            return new Color(1f, 1f, 1f, a);
        }, imp => imp.wrapMode = TextureWrapMode.Clamp);

        public static Texture2D Paper() => Save("Paper", 256, 256, (u, v) =>
        {
            float g = Mathf.Lerp(0.7f, 0.82f, Fbm(u * 10f, v * 10f, 4));
            float lines = (Mathf.Repeat(v * 22f, 1f) < 0.04f && u > 0.12f && u < 0.88f && v > 0.1f && v < 0.9f) ? 0.8f : 1f;
            return new Color(g * lines, g * lines, g * lines, 1f);
        }, imp => imp.wrapMode = TextureWrapMode.Clamp);

        public static Sprite RoundedFill() => SaveSprite("UI_RoundedFill", 64, 64, (u, v) =>
            new Color(1f, 1f, 1f, RoundRect(u * 64f, v * 64f, 64f, 64f, 7f, fill: true)), 16);

        public static Sprite RoundedOutline() => SaveSprite("UI_RoundedOutline", 64, 64, (u, v) =>
            new Color(1f, 1f, 1f, RoundRect(u * 64f, v * 64f, 64f, 64f, 7f, fill: false)), 16);

        public static Sprite Ornament() => SaveSprite("UI_Ornament", 512, 32, (u, v) =>
        {
            float x = u * 512f, y = v * 32f;
            float line = 1f - SS(0.5f, 1.4f, Mathf.Abs(y - 16f));
            float fade = SS(0f, 0.15f, u) * SS(1f, 0.6f, u);
            float diamond = 1f - SS(5f, 6.5f, Mathf.Abs(x - 40f) + Mathf.Abs(y - 16f));
            return new Color(1f, 1f, 1f, Mathf.Max(line * fade, diamond));
        }, 0);

        // Ojo del cursor: contorno almendrado con un leve halo
        public static Sprite EyeAlmond() => SaveSprite("Cursor_EyeAlmond", 128, 64, (u, v) =>
        {
            float x = (u - 0.5f) * 2f, y = (v - 0.5f) * 2f;
            float h = 0.78f * (1f - x * x);                        // media altura del párpado en x
            float d = (Mathf.Abs(y) - h) / Mathf.Sqrt(1f + Mathf.Pow(2f * 0.78f * x, 2f));
            float stroke = 1f - SS(0.025f, 0.06f, Mathf.Abs(d));
            float glow = (1f - SS(0f, 0.35f, Mathf.Abs(d))) * 0.25f;
            float lash = Mathf.Abs(x) < 0.85f && Mathf.Abs(x) > 0.1f && y > h && y < h + 0.18f &&
                         Mathf.Repeat(x * 4.5f + 0.5f, 1f) < 0.12f ? 0.6f : 0f;   // pestañas superiores
            return new Color(1f, 0.98f, 0.95f, Mathf.Clamp01(Mathf.Max(stroke, glow) + lash * (1f - SS(0f, 0.18f, y - h))));
        }, 0);

        // Iris: anillo con estrías radiales
        public static Sprite Iris() => SaveSprite("Cursor_Iris", 64, 64, (u, v) =>
        {
            Vector2 p = new(u - 0.5f, v - 0.5f);
            float r = p.magnitude * 2f;
            float ang = Mathf.Atan2(p.y, p.x);
            float ring = SS(0.42f, 0.5f, r) * (1f - SS(0.9f, 0.98f, r));
            float streaks = 0.65f + 0.35f * Mathf.Abs(Mathf.Sin(ang * 9f));
            float edge = 1f - SS(0.02f, 0.06f, Mathf.Abs(r - 0.96f));
            return new Color(0.92f, 0.92f, 0.92f, Mathf.Clamp01(ring * streaks * 0.85f + edge));
        }, 0);

        public static Sprite Circle() => SaveSprite("UI_Circle", 64, 64, (u, v) =>
        {
            float r = Vector2.Distance(new Vector2(u, v), new Vector2(0.5f, 0.5f)) * 2f;
            return new Color(1f, 1f, 1f, 1f - SS(0.9f, 0.98f, r));
        }, 0);

        // Cápsula para el interruptor (borde redondeado completo)
        public static Sprite Pill() => SaveSprite("UI_Pill", 64, 32, (u, v) =>
            new Color(1f, 1f, 1f, RoundRect(u * 64f, v * 32f, 64f, 32f, 14f, fill: true)), 15);

        public static Sprite Icon(string name) => SaveSprite("Icon_" + name, 128, 128, (u, v) =>
        {
            float d = IconDistance(name, new Vector2(u, v) * 128f);
            return new Color(1f, 1f, 1f, 1f - SS(3.2f, 5f, d));
        }, 0);

        // smoothstep estilo GLSL (Mathf.SmoothStep interpola entre dos valores, no es lo mismo)
        static float SS(float e0, float e1, float x)
        {
            float t = Mathf.Clamp01((x - e0) / (e1 - e0));
            return t * t * (3f - 2f * t);
        }

        // --- Iconos (SDF de trazos) -------------------------------------------------

        static float IconDistance(string name, Vector2 p)
        {
            switch (name)
            {
                case "Eye":
                {
                    float lids = Mathf.Min(Arc(p, new Vector2(64, 30), 56, 50, 130), Arc(p, new Vector2(64, 98), 56, 230, 310));
                    return Mathf.Min(lids, Mathf.Abs(Vector2.Distance(p, new Vector2(64, 64)) - 15f));
                }
                case "EyeClosed":
                {
                    float lid = Arc(p, new Vector2(64, 98), 56, 230, 310);
                    float lashes = Mathf.Min(Seg(p, new Vector2(64, 42), new Vector2(64, 30)),
                        Mathf.Min(Seg(p, new Vector2(42, 47), new Vector2(34, 37)), Seg(p, new Vector2(86, 47), new Vector2(94, 37))));
                    float slash = Seg(p, new Vector2(24, 24), new Vector2(104, 104));
                    return Mathf.Min(Mathf.Min(lid, lashes), slash);
                }
                case "Gear":
                {
                    Vector2 c = new(64, 64);
                    float ring = Mathf.Abs(Vector2.Distance(p, c) - 30f);
                    float hub = Mathf.Abs(Vector2.Distance(p, c) - 12f);
                    float teeth = 999f;
                    for (int i = 0; i < 8; i++)
                    {
                        float a = i * Mathf.PI / 4f;
                        Vector2 dir = new(Mathf.Cos(a), Mathf.Sin(a));
                        teeth = Mathf.Min(teeth, Seg(p, c + dir * 30f, c + dir * 44f));
                    }
                    return Mathf.Min(Mathf.Min(ring, hub), teeth);
                }
                case "Gamepad":
                {
                    float body = Mathf.Abs(RoundRectDist(p, new Vector2(64, 62), new Vector2(46, 24), 20f));
                    float cross = Mathf.Min(Seg(p, new Vector2(34, 62), new Vector2(50, 62)), Seg(p, new Vector2(42, 54), new Vector2(42, 70)));
                    float btns = Mathf.Min(Mathf.Abs(Vector2.Distance(p, new Vector2(84, 68)) - 3f), Mathf.Abs(Vector2.Distance(p, new Vector2(94, 58)) - 3f));
                    return Mathf.Min(Mathf.Min(body, cross), btns);
                }
                case "Book":
                {
                    float left = Mathf.Min(Seg(p, new Vector2(64, 34), new Vector2(64, 96)),
                        Mathf.Min(Seg(p, new Vector2(64, 34), new Vector2(22, 40)), Mathf.Min(Seg(p, new Vector2(22, 40), new Vector2(22, 100)), Seg(p, new Vector2(22, 100), new Vector2(64, 96)))));
                    float right = Mathf.Min(Seg(p, new Vector2(64, 34), new Vector2(106, 40)),
                        Mathf.Min(Seg(p, new Vector2(106, 40), new Vector2(106, 100)), Seg(p, new Vector2(106, 100), new Vector2(64, 96))));
                    return Mathf.Min(left, right);
                }
                case "Door":
                {
                    float frame = Mathf.Min(Seg(p, new Vector2(36, 20), new Vector2(36, 108)),
                        Mathf.Min(Seg(p, new Vector2(36, 108), new Vector2(92, 108)), Seg(p, new Vector2(92, 108), new Vector2(92, 20))));
                    float leaf = Mathf.Min(Seg(p, new Vector2(36, 20), new Vector2(72, 30)), Mathf.Min(Seg(p, new Vector2(72, 30), new Vector2(72, 102)), Seg(p, new Vector2(72, 102), new Vector2(36, 108))));
                    float knob = Mathf.Abs(Vector2.Distance(p, new Vector2(64, 62)) - 3f);
                    return Mathf.Min(Mathf.Min(frame, leaf), knob);
                }
                case "Home":
                {
                    float roof = Mathf.Min(Seg(p, new Vector2(22, 62), new Vector2(64, 102)), Seg(p, new Vector2(64, 102), new Vector2(106, 62)));
                    float walls = Mathf.Min(Seg(p, new Vector2(34, 70), new Vector2(34, 26)),
                        Mathf.Min(Seg(p, new Vector2(34, 26), new Vector2(94, 26)), Seg(p, new Vector2(94, 26), new Vector2(94, 70))));
                    float door = Mathf.Min(Seg(p, new Vector2(56, 26), new Vector2(56, 52)),
                        Mathf.Min(Seg(p, new Vector2(56, 52), new Vector2(72, 52)), Seg(p, new Vector2(72, 52), new Vector2(72, 26))));
                    return Mathf.Min(Mathf.Min(roof, walls), door);
                }
                case "ChevronLeft":
                    return Mathf.Min(Seg(p, new Vector2(78, 36), new Vector2(50, 64)), Seg(p, new Vector2(50, 64), new Vector2(78, 92)));
                case "Chevron":
                    return Mathf.Min(Seg(p, new Vector2(50, 36), new Vector2(78, 64)), Seg(p, new Vector2(78, 64), new Vector2(50, 92)));
                case "Frame":
                {
                    float d = 999f;
                    Vector2[] corners = { new(24, 24), new(104, 24), new(104, 104), new(24, 104) };
                    Vector2[] dirs = { new(1, 1), new(-1, 1), new(-1, -1), new(1, -1) };
                    for (int i = 0; i < 4; i++)
                    {
                        d = Mathf.Min(d, Seg(p, corners[i], corners[i] + new Vector2(dirs[i].x * 22, 0)));
                        d = Mathf.Min(d, Seg(p, corners[i], corners[i] + new Vector2(0, dirs[i].y * 22)));
                    }
                    return d;
                }
                case "Key":
                    return Mathf.Abs(RoundRectDist(p, new Vector2(64, 64), new Vector2(40, 28), 8f));
                default:
                    return 999f;
            }
        }

        static float Seg(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 pa = p - a, ba = b - a;
            float h = Mathf.Clamp01(Vector2.Dot(pa, ba) / Vector2.Dot(ba, ba));
            return (pa - ba * h).magnitude;
        }

        static float Arc(Vector2 p, Vector2 c, float r, float fromDeg, float toDeg)
        {
            float best = 999f;
            const int steps = 48;
            Vector2 prev = c + Polar(r, fromDeg);
            for (int i = 1; i <= steps; i++)
            {
                Vector2 next = c + Polar(r, Mathf.Lerp(fromDeg, toDeg, i / (float)steps));
                best = Mathf.Min(best, Seg(p, prev, next));
                prev = next;
            }
            return best;
        }

        static Vector2 Polar(float r, float deg) => new(Mathf.Cos(deg * Mathf.Deg2Rad) * r, Mathf.Sin(deg * Mathf.Deg2Rad) * r);

        static float RoundRectDist(Vector2 p, Vector2 c, Vector2 half, float r)
        {
            Vector2 q = new(Mathf.Abs(p.x - c.x) - half.x + r, Mathf.Abs(p.y - c.y) - half.y + r);
            return new Vector2(Mathf.Max(q.x, 0), Mathf.Max(q.y, 0)).magnitude + Mathf.Min(Mathf.Max(q.x, q.y), 0f) - r;
        }

        static float RoundRect(float x, float y, float w, float h, float r, bool fill)
        {
            float d = RoundRectDist(new Vector2(x, y), new Vector2(w * 0.5f, h * 0.5f), new Vector2(w * 0.5f - 1.5f, h * 0.5f - 1.5f), r);
            return fill ? 1f - SS(-0.5f, 0.8f, d) : 1f - SS(0.6f, 1.6f, Mathf.Abs(d + 1f));
        }

        static float Ellipse(float u, float v, float cx, float cy, float rx, float ry)
        {
            float d = Mathf.Sqrt(Mathf.Pow((u - cx) / rx, 2f) + Mathf.Pow((v - cy) / ry, 2f));
            return 1f - SS(0.7f, 1f, d);
        }

        // --- Ruido ---------------------------------------------------------------------

        static float Fbm(float x, float y, int octaves, float tile = 0f)
        {
            float sum = 0f, amp = 0.5f, freq = 1f, norm = 0f;
            for (int i = 0; i < octaves; i++)
            {
                float n = tile > 0f ? TileNoise(x * freq, y * freq, tile * freq) : Mathf.PerlinNoise(x * freq + 13.7f * i, y * freq + 7.3f * i);
                sum += n * amp;
                norm += amp;
                amp *= 0.5f;
                freq *= 2f;
            }
            return sum / norm;
        }

        static float TileNoise(float x, float y, float period)
        {
            float u = x / period, v = y / period;
            float a = Mathf.PerlinNoise(x, y), b = Mathf.PerlinNoise(x - period, y);
            float c = Mathf.PerlinNoise(x, y - period), d = Mathf.PerlinNoise(x - period, y - period);
            u = Mathf.Repeat(u, 1f); v = Mathf.Repeat(v, 1f);
            return Mathf.Lerp(Mathf.Lerp(a, b, u), Mathf.Lerp(c, d, u), v);
        }

        static float VoronoiEdge(float x, float y)
        {
            Vector2 p = new(x, y);
            Vector2Int i = new(Mathf.FloorToInt(x), Mathf.FloorToInt(y));
            float f1 = 99f, f2 = 99f;
            for (int oy = -1; oy <= 1; oy++)
            for (int ox = -1; ox <= 1; ox++)
            {
                Vector2Int g = i + new Vector2Int(ox, oy);
                Vector2 pt = g + Hash2(g);
                float d = Vector2.Distance(p, pt);
                if (d < f1) { f2 = f1; f1 = d; }
                else if (d < f2) f2 = d;
            }
            return f2 - f1;
        }

        static Vector2 Hash2(Vector2Int g)
        {
            float a = Mathf.Sin(g.x * 127.1f + g.y * 311.7f) * 43758.5453f;
            float b = Mathf.Sin(g.x * 269.5f + g.y * 183.3f) * 43758.5453f;
            return new Vector2(a - Mathf.Floor(a), b - Mathf.Floor(b));
        }

        // --- Guardado e importación ------------------------------------------------------

        static Texture2D Save(string name, int w, int h, Func<float, float, Color> pixel, Action<TextureImporter> setup)
        {
            string path = Write(name, w, h, pixel);
            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            imp.textureType = TextureImporterType.Default;
            imp.alphaIsTransparency = true;
            imp.mipmapEnabled = true;
            setup?.Invoke(imp);
            imp.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        static Sprite SaveSprite(string name, int w, int h, Func<float, float, Color> pixel, int border, Vector2? pivot = null)
        {
            string path = Write(name, w, h, pixel);
            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            imp.textureType = TextureImporterType.Sprite;
            imp.spriteImportMode = SpriteImportMode.Single;
            imp.alphaIsTransparency = true;
            imp.mipmapEnabled = false;
            imp.wrapMode = TextureWrapMode.Clamp;
            var settings = new TextureImporterSettings();
            imp.ReadTextureSettings(settings);
            settings.spriteBorder = new Vector4(border, border, border, border);
            settings.spritePivot = pivot ?? new Vector2(0.5f, 0.5f);
            settings.spriteAlignment = (int)SpriteAlignment.Custom;
            settings.spriteMeshType = SpriteMeshType.FullRect;
            imp.SetTextureSettings(settings);
            imp.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        static string Write(string name, int w, int h, Func<float, float, Color> pixel)
        {
            Directory.CreateDirectory(Folder);
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            var px = new Color[w * h];
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                px[y * w + x] = pixel((x + 0.5f) / w, (y + 0.5f) / h);
            tex.SetPixels(px);
            tex.Apply();
            string path = $"{Folder}/{name}.png";
            File.WriteAllBytes(path, tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            return path;
        }
    }
}
