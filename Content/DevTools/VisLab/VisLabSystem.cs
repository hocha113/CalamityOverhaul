#if DEBUG
using InnoVault.PRT;
using InnoVault.UIHandles;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.DevTools.VisLab
{
    /// <summary>
    /// 游戏内视觉快照台:job 驱动 spawn → 逐帧抓屏 → 裁剪 → 帧条+stats 落盘,
    /// 输出与离线沙盒同一契约(.vissandbox\out\&lt;job&gt;\strip.png + stats.json)。<br/>
    /// 双门控:DEBUG 构建 + .vissandbox 目录存在(开发机);仅单人模式。
    /// 会话状态用 static：单人开发工具,不涉及多端同步。
    /// </summary>
    internal class VisLabSystem : ModSystem
    {
        private enum Phase { Idle, Warmup, Running, Finalize }

        private static Phase phase = Phase.Idle;
        private static VisLabJob job;
        private static string jobName;
        private static int tick;
        private static int projType = -1;
        private static int prtID = -1;
        private static UIHandle uiHandle;
        private static bool uiWasOpen;
        private static int targetProj = -1;
        /// <summary>捕获中心(世界坐标);follow 时逐帧刷到弹幕最后活跃位置</summary>
        private static Vector2 anchorWorld;
        private static Point cropSize;
        private static Color[] baselineFull;
        private static Color[] contextFull;
        private static readonly List<Color[]> crops = [];
        private static readonly List<Rectangle> rects = [];
        private static int screenW;
        private static int screenH;
        private static bool wantBaseline;
        private static bool wantCapture;
        private static bool prevHideUI;

        public static string Root => Path.Combine(Main.SavePath, "ModSources", "CalamityOverhaul", ".vissandbox");
        public static bool DevMachine => Directory.Exists(Root);
        public static bool Busy => phase != Phase.Idle;

        public override void Load() {
            if (!Main.dedServ) {
                Main.OnPostDraw += CaptureHook;
            }
        }

        public override void Unload() {
            if (!Main.dedServ) {
                Main.OnPostDraw -= CaptureHook;
            }
        }

        //═══════════════ 会话入口 ═══════════════

        public static bool TryStart(string name, out string error) {
            error = null;
            if (!DevMachine) {
                error = "非开发机(.vissandbox 不存在)";
                return false;
            }
            if (Main.netMode != NetmodeID.SinglePlayer) {
                error = "仅单人模式可用";
                return false;
            }
            if (Busy) {
                error = "已有会话进行中,/vlab stop 可中止";
                return false;
            }
            string path = Path.Combine(Root, "jobs", name + ".json");
            if (!File.Exists(path)) {
                error = "job 不存在: " + path;
                return false;
            }
            try {
                job = VisLabJob.Load(path);
            } catch (Exception ex) {
                error = "job 解析失败: " + ex.Message;
                return false;
            }
            if (string.IsNullOrEmpty(job.Kind)) {
                error = "缺 kind 字段(离线 shader job 请走 run.ps1)";
                return false;
            }

            projType = prtID = -1;
            uiHandle = null;
            switch (job.Kind.ToLowerInvariant()) {
                case "proj":
                    projType = job.ResolveProjType(out error);
                    if (projType < 0) {
                        return false;
                    }
                    break;
                case "prt":
                    prtID = job.ResolvePrtID(out error);
                    if (prtID < 0) {
                        return false;
                    }
                    break;
                case "ui":
                    uiHandle = job.ResolveUI(out error);
                    if (uiHandle == null) {
                        return false;
                    }
                    break;
                default:
                    error = "未知 kind: " + job.Kind;
                    return false;
            }

            jobName = name;
            tick = 0;
            targetProj = -1;
            crops.Clear();
            rects.Clear();
            baselineFull = null;
            contextFull = null;
            wantBaseline = wantCapture = false;
            prevHideUI = Main.hideUI;
            phase = Phase.Warmup;
            return true;
        }

        public static void Stop(string reason) {
            if (phase == Phase.Idle) {
                return;
            }
            RestoreScene();
            ClearBuffers();
            phase = Phase.Idle;
            Main.NewText("[VisLab] 中止: " + reason, Color.IndianRed);
        }

        //═══════════════ 状态机(更新相) ═══════════════

        public override void PostUpdateEverything() {
            if (phase == Phase.Idle || Main.gameMenu) {
                return;
            }
            ScenePrep();
            switch (phase) {
                case Phase.Warmup:
                    tick++;
                    if (tick >= job.Warmup && baselineFull == null) {
                        wantBaseline = true; //本帧绘制末尾抓基线
                    }
                    if (baselineFull != null) {
                        SpawnTarget();
                        tick = 0;
                        phase = Phase.Running;
                    }
                    break;
                case Phase.Running:
                    //弹幕死亡后不中断，余韵痕迹本就是要看的东西
                    if (job.Follow && targetProj >= 0 && Main.projectile[targetProj].active) {
                        anchorWorld = Main.projectile[targetProj].Center;
                    }
                    if (crops.Count < job.Frames && !wantCapture && tick % Math.Max(1, job.Interval) == 0) {
                        wantCapture = true;
                    }
                    tick++;
                    if (crops.Count >= job.Frames) {
                        phase = Phase.Finalize;
                    }
                    break;
                case Phase.Finalize:
                    Finish();
                    break;
            }
        }

        private static void ScenePrep() {
            bool uiJob = uiHandle != null;
            Main.hideUI = !uiJob && job.HideUI;
            Player player = Main.LocalPlayer;
            if (job.LockPlayer) {
                player.velocity = Vector2.Zero;
            }
            if (job.GodMode) {
                player.immune = true;
                player.immuneTime = 30;
            }
            if (job.FloodLight) {
                Vector2 center = anchorWorld == Vector2.Zero ? player.Center + job.OffsetVec() : anchorWorld;
                int half = Math.Min(job.Margin + 160, 500);
                for (int dx = -half; dx <= half; dx += 16) {
                    for (int dy = -half; dy <= half; dy += 16) {
                        Lighting.AddLight(center + new Vector2(dx, dy), 1f, 1f, 1f);
                    }
                }
            }
        }

        private static void SpawnTarget() {
            Player player = Main.LocalPlayer;
            Vector2 pos = player.Center + job.OffsetVec();
            anchorWorld = pos;
            switch (job.Kind.ToLowerInvariant()) {
                case "proj": {
                    float a0 = job.Ai is { Length: >= 1 } ? job.Ai[0] : 0f;
                    float a1 = job.Ai is { Length: >= 2 } ? job.Ai[1] : 0f;
                    float a2 = job.Ai is { Length: >= 3 } ? job.Ai[2] : 0f;
                    targetProj = Projectile.NewProjectile(player.GetSource_Misc("VisLab"), pos, job.VelocityVec(),
                        projType, job.Damage, job.Knockback, Main.myPlayer, a0, a1, a2);
                    Projectile proj = Main.projectile[targetProj];
                    cropSize = new Point(proj.width + job.Margin * 2, proj.height + job.Margin * 2);
                    break;
                }
                case "prt": {
                    for (int i = 0; i < job.Count; i++) {
                        Vector2 vel = job.VelocityVec() + Main.rand.NextVector2Circular(job.Spread, job.Spread);
                        PRTLoader.NewParticle(prtID, pos + Main.rand.NextVector2Circular(8f, 8f), vel, job.ColorValue(), job.Scale);
                    }
                    cropSize = new Point(job.Margin * 2, job.Margin * 2);
                    break;
                }
                case "ui": {
                    uiWasOpen = uiHandle.IsOpen;
                    if (!uiWasOpen) {
                        uiHandle.Open();
                    }
                    InjectFields();
                    cropSize = Point.Zero; //全屏
                    break;
                }
            }
        }

        //═══════════════ 抓帧(绘制相末尾,backbuffer 已完整) ═══════════════

        private static void CaptureHook(GameTime gameTime) {
            if (phase == Phase.Idle || (!wantBaseline && !wantCapture) || Main.gameMenu) {
                return;
            }
            GraphicsDevice device = Main.instance.GraphicsDevice;
            int w = device.PresentationParameters.BackBufferWidth;
            int h = device.PresentationParameters.BackBufferHeight;
            Color[] full = new Color[w * h];
            try {
                device.GetBackBufferData(full);
            } catch (Exception ex) {
                Stop("抓帧失败: " + ex.Message);
                return;
            }

            if (wantBaseline) {
                wantBaseline = false;
                baselineFull = full;
                screenW = w;
                screenH = h;
                return;
            }

            wantCapture = false;
            if (w != screenW || h != screenH) {
                Stop("分辨率中途变化");
                return;
            }
            Rectangle rect = ComputeCropRect(w, h);
            crops.Add(ExtractRect(full, w, rect));
            rects.Add(rect);
            contextFull ??= full;
        }

        private static Rectangle ComputeCropRect(int w, int h) {
            if (cropSize == Point.Zero) {
                return new Rectangle(0, 0, w, h);
            }
            //世界→屏幕走游戏视图矩阵(含 zoom);UI 层无缩放但 ui 类走全屏分支
            Vector2 screen = Vector2.Transform(anchorWorld - Main.screenPosition, Main.GameViewMatrix.TransformationMatrix);
            int cw = Math.Min(cropSize.X, w);
            int ch = Math.Min(cropSize.Y, h);
            int x = Math.Clamp((int)screen.X - cw / 2, 0, w - cw);
            int y = Math.Clamp((int)screen.Y - ch / 2, 0, h - ch);
            return new Rectangle(x, y, cw, ch);
        }

        private static Color[] ExtractRect(Color[] full, int fullW, Rectangle rect) {
            Color[] outData = new Color[rect.Width * rect.Height];
            for (int row = 0; row < rect.Height; row++) {
                Array.Copy(full, (rect.Y + row) * fullW + rect.X, outData, row * rect.Width, rect.Width);
            }
            return outData;
        }

        //═══════════════ 落盘与统计 ═══════════════

        private static void Finish() {
            string outDir = Path.Combine(Root, "out", jobName);
            List<string> flags = [];
            string result = "FAIL";
            try {
                Directory.CreateDirectory(outDir);
                GraphicsDevice device = Main.instance.GraphicsDevice;

                var cells = new List<Dictionary<string, object>>();
                var coverages = new List<double>();
                var uniques = new List<int>();
                var motions = new List<double>();
                for (int i = 0; i < crops.Count; i++) {
                    Color[] baseCrop = ExtractRect(baselineFull, screenW, rects[i]);
                    (double cov, int uniq, double luma) = Measure(crops[i], baseCrop);
                    coverages.Add(cov);
                    uniques.Add(uniq);
                    string file = "f" + i + ".png";
                    SavePng(device, crops[i], rects[i].Width, rects[i].Height, Path.Combine(outDir, file));
                    cells.Add(new Dictionary<string, object> {
                        ["frame"] = i,
                        ["tick"] = i * job.Interval,
                        ["coverage"] = Math.Round(cov, 4),
                        ["uniqueChanged"] = uniq,
                        ["meanLuma"] = Math.Round(luma, 4),
                        ["rect"] = new[] { rects[i].X, rects[i].Y, rects[i].Width, rects[i].Height },
                        ["file"] = file
                    });
                    if (i > 0 && rects[i].Width == rects[i - 1].Width && rects[i].Height == rects[i - 1].Height) {
                        motions.Add(Math.Round(Motion(crops[i - 1], crops[i]), 4));
                    }
                }
                if (contextFull != null) {
                    SavePng(device, contextFull, screenW, screenH, Path.Combine(outDir, "context.png"));
                }
                ComposeStrip(device, Path.Combine(outDir, "strip.png"));

                //三条红旗,与离线沙盒同语义
                if (coverages.Count > 0 && coverages.Max() < 0.01) {
                    flags.Add("NOTHING_DRAWN: max coverage " + (coverages.Max() * 100).ToString("0.00") + "% < 1%");
                }
                for (int i = 0; i < coverages.Count; i++) {
                    if (coverages[i] >= 0.01 && uniques[i] <= 2) {
                        flags.Add("SINGLE_COLOR: frame=" + i + " unique=" + uniques[i]);
                    }
                }
                if (crops.Count > 1 && motions.Count > 0 && flags.Count == 0 && motions.All(m => m < 0.0001)) {
                    flags.Add("STATIC: frames advance but no pixel moves");
                }
                result = flags.Count > 0 ? "FLAG" : "PASS";

                var doc = new Dictionary<string, object> {
                    ["job"] = jobName,
                    ["kind"] = job.Kind,
                    ["type"] = job.Type,
                    ["frames"] = job.Frames,
                    ["interval"] = job.Interval,
                    ["screen"] = new[] { screenW, screenH },
                    ["cells"] = cells,
                    ["motion"] = motions,
                    ["flags"] = flags,
                    ["result"] = result,
                    ["strip"] = "strip.png",
                    ["context"] = "context.png"
                };
                if (uiHandle != null) {
                    doc["uiActiveDuringCapture"] = uiHandle.Active;
                    if (!uiHandle.Active) {
                        flags.Add("UI_INACTIVE: Open() 后 Active 仍为 false,该 UI 由游戏状态门控,需真实触发");
                        doc["flags"] = flags;
                        doc["result"] = result = "FLAG";
                    }
                }
                File.WriteAllText(Path.Combine(outDir, "stats.json"),
                    JsonSerializer.Serialize(doc, new JsonSerializerOptions { WriteIndented = true }));
            } catch (Exception ex) {
                Main.NewText("[VisLab] 落盘失败: " + ex.Message, Color.IndianRed);
            } finally {
                RestoreScene();
                ClearBuffers();
                phase = Phase.Idle;
            }
            Main.NewText("[VisLab] " + jobName + " RESULT " + result + " (" + flags.Count + " 红旗)", result == "PASS" ? Color.LightGreen : Color.Orange);
            foreach (string f in flags) {
                Main.NewText("  " + f, Color.Orange);
            }
            Main.NewText("  -> " + outDir, Color.LightGray);
        }

        private static void ComposeStrip(GraphicsDevice device, string path) {
            if (crops.Count == 0) {
                return;
            }
            int cw = rects[0].Width, ch = rects[0].Height;
            const int gap = 4, cap = 4096;
            float scale = Math.Min(1f, (cap - gap * (crops.Count - 1)) / (float)(cw * crops.Count));
            scale = Math.Min(scale, cap / (float)ch);
            int cellW = Math.Max(1, (int)(cw * scale));
            int cellH = Math.Max(1, (int)(ch * scale));
            int gw = cellW * crops.Count + gap * (crops.Count - 1);

            var cellTex = new List<Texture2D>();
            try {
                foreach (Color[] crop in crops) {
                    //尺寸不一致的帧(裁剪被屏幕边界压缩)跳过缩放差异,直接按各自尺寸建纹理
                    int w = cellTex.Count < rects.Count ? rects[cellTex.Count].Width : cw;
                    int h = cellTex.Count < rects.Count ? rects[cellTex.Count].Height : ch;
                    Texture2D tex = new Texture2D(device, w, h);
                    tex.SetData(crop);
                    cellTex.Add(tex);
                }
                using RenderTarget2D composite = new RenderTarget2D(device, gw, cellH, false, SurfaceFormat.Color, DepthFormat.None, 0, RenderTargetUsage.PreserveContents);
                using SpriteBatch sb = new SpriteBatch(device);
                device.SetRenderTarget(composite);
                device.Clear(new Color(18, 18, 18, 255));
                sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone);
                for (int i = 0; i < cellTex.Count; i++) {
                    sb.Draw(cellTex[i], new Rectangle(i * (cellW + gap), 0, cellW, cellH), Color.White);
                }
                sb.End();
                device.SetRenderTarget(null);
                using FileStream fs = File.Create(path);
                composite.SaveAsPng(fs, gw, cellH);
            } finally {
                foreach (Texture2D tex in cellTex) {
                    tex.Dispose();
                }
            }
        }

        private static void SavePng(GraphicsDevice device, Color[] data, int w, int h, string path) {
            using Texture2D tex = new Texture2D(device, w, h);
            tex.SetData(data);
            using FileStream fs = File.Create(path);
            tex.SaveAsPng(fs, w, h);
        }

        private static (double coverage, int unique, double luma) Measure(Color[] frame, Color[] baseline) {
            int changed = 0;
            double luma = 0;
            HashSet<int> uniqueSet = [];
            for (int i = 0; i < frame.Length; i++) {
                Color c = frame[i];
                luma += (0.299 * c.R + 0.587 * c.G + 0.114 * c.B) / 255.0;
                Color b = baseline[i];
                if (Math.Abs(c.R - b.R) > 8 || Math.Abs(c.G - b.G) > 8 || Math.Abs(c.B - b.B) > 8) {
                    changed++;
                    uniqueSet.Add(((c.R >> 4) << 8) | ((c.G >> 4) << 4) | (c.B >> 4));
                }
            }
            return (changed / (double)frame.Length, uniqueSet.Count, luma / frame.Length);
        }

        private static double Motion(Color[] a, Color[] b) {
            int moved = 0;
            int len = Math.Min(a.Length, b.Length);
            for (int i = 0; i < len; i++) {
                if (Math.Abs(a[i].R - b[i].R) > 2 || Math.Abs(a[i].G - b[i].G) > 2 || Math.Abs(a[i].B - b[i].B) > 2) {
                    moved++;
                }
            }
            return moved / (double)len;
        }

        //═══════════════ 复原 ═══════════════

        private static void RestoreScene() {
            Main.hideUI = prevHideUI;
            if (uiHandle != null && !uiWasOpen) {
                uiHandle.Close();
            }
        }

        private static void ClearBuffers() {
            crops.Clear();
            rects.Clear();
            baselineFull = null;
            contextFull = null;
            uiHandle = null;
            targetProj = -1;
            wantBaseline = wantCapture = false;
        }

        private static void InjectFields() {
            if (job.Fields == null || uiHandle == null) {
                return;
            }
            Type t = uiHandle.GetType();
            const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
            foreach ((string key, JsonElement val) in job.Fields) {
                try {
                    FieldInfo field = t.GetField(key, flags);
                    if (field != null) {
                        field.SetValue(uiHandle, ConvertJson(val, field.FieldType));
                        continue;
                    }
                    PropertyInfo prop = t.GetProperty(key, flags);
                    if (prop is { CanWrite: true }) {
                        prop.SetValue(uiHandle, ConvertJson(val, prop.PropertyType));
                        continue;
                    }
                    Main.NewText("[VisLab] 字段不存在: " + key, Color.Orange);
                } catch (Exception ex) {
                    Main.NewText("[VisLab] 注入失败 " + key + ": " + ex.Message, Color.Orange);
                }
            }
        }

        private static object ConvertJson(JsonElement val, Type target) {
            if (target == typeof(float)) {
                return (float)val.GetDouble();
            }
            if (target == typeof(int)) {
                return val.GetInt32();
            }
            if (target == typeof(bool)) {
                return val.GetBoolean();
            }
            if (target == typeof(string)) {
                return val.GetString();
            }
            if (target == typeof(double)) {
                return val.GetDouble();
            }
            if (target == typeof(Vector2)) {
                float[] arr = val.EnumerateArray().Select(e => (float)e.GetDouble()).ToArray();
                return new Vector2(arr[0], arr[1]);
            }
            throw new InvalidDataException("不支持的注入类型 " + target.Name);
        }
    }
}
#endif
