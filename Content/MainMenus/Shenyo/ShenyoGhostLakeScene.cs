using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Narrative;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.MainMenus.Shenyo
{
    /// <summary>
    /// 鬼湖夜雨场景渲染器：湖景天穹（<c>ShenyoMenuLake</c> TechLake，含湖面雨砸溅环）打底，
    /// 沈幽雨水幽灵立影按远近三组绘制（<c>ShenyoMenuGhost</c>，倒影翻面先画、潮雾带隔行），
    /// 雨幕（TechRain）按远/中/近三次插层——远雨止于水线被立影遮挡、近雨盖顶，
    /// 整幅送入湿屏合成（<c>ShenyoMenuWet</c>，镜头水珠折射+溅击）；风暴脉动 gust 全链同拍。<br/>
    /// 立影循环走「汇聚成形→驻立→坠散入湖→缺席」的黑雨拍子（承 <see cref="Scenarios.Shenyo.ShenyoPortraitRainRenderer"/> 的入场语言），
    /// 右侧大近影为常驻锚。着色器或噪声缺席时退化为 CPU 渐变+压色立影，绝不黑屏。<br/>
    /// 调用契约：进入 <see cref="Draw"/> 时批次已结束，返回时不留任何已开启批次。
    /// </summary>
    internal static class ShenyoGhostLakeScene
    {
        private enum FigurePhase
        {
            Absent,   //缺席
            Reform,   //黑雨汇聚
            Formed,   //驻立
            Dissolve, //坠散入湖
        }

        private sealed class FigureState
        {
            public FigurePhase Phase;
            public int Timer;
            public int Duration = 1;
            public float Form;
            public float EyeGlow;
            public int BlinkTimer;
            public int NextBlink;
        }

        //潮雾带：两行贴片把远/中/近三组立影隔出纵深
        private readonly struct MistRow(float y, int count, float widthFrac, float heightFrac,
            float alpha, float parallax, float driftSpeed, float seed)
        {
            public readonly float Y = y;
            public readonly int Count = count;
            public readonly float WidthFrac = widthFrac;
            public readonly float HeightFrac = heightFrac;
            public readonly float Alpha = alpha;
            public readonly float Parallax = parallax;
            public readonly float DriftSpeed = driftSpeed;
            public readonly float Seed = seed;
        }

        private static readonly MistRow[] MistRows = [
            new(y: 0.648f, count: 4, widthFrac: 0.42f, heightFrac: 0.085f,
                alpha: 0.085f, parallax: 0.30f, driftSpeed: 0.0045f, seed: 0.37f),
            new(y: 0.740f, count: 3, widthFrac: 0.58f, heightFrac: 0.125f,
                alpha: 0.065f, parallax: 0.55f, driftSpeed: 0.0080f, seed: 0.71f),
        ];

        private static readonly FigureState[] states = BuildStates();
        private static readonly Vector4[] feetBuffer = new Vector4[8];

        //叠影群：常驻不散，只记汇聚进度与入场延迟；绘制按深度升序（远者先画才叠得对）
        private static readonly float[] crowdForm = new float[ShenyoMenuTheme.Crowd.Length];
        private static readonly int[] crowdDelay = new int[ShenyoMenuTheme.Crowd.Length];
        private static readonly int[] crowdOrder = BuildCrowdOrder();
        private const int CrowdReformDuration = 150;

        private static int[] BuildCrowdOrder() {
            var order = new int[ShenyoMenuTheme.Crowd.Length];
            for (int i = 0; i < order.Length; i++) {
                order[i] = i;
            }
            Array.Sort(order, (a, b) =>
                ShenyoMenuTheme.Crowd[a].Depth.CompareTo(ShenyoMenuTheme.Crowd[b].Depth));
            return order;
        }

        //鼠标牵引视差，指数平滑，分量 -1~1
        private static Vector2 parallax;
        private static int tickCount;

        //雷闪包络与二击回响、光先声后
        private static float flash;
        private static int flashEcho;
        private static int thunderTimer = 900;
        private static int thunderSoundDelay;

        //风暴脉动：慢涌打底+随机阵风冲顶，雨幕/湿屏/湖面溅击同源呼吸
        private static float gust = 0.4f;
        private static float gustSurge;
        private static int gustTimer = 300;

        private static RenderTarget2D sceneTarget;
        private static bool targetFault;

        private static FigureState[] BuildStates() {
            var array = new FigureState[ShenyoMenuTheme.Figures.Length];
            for (int i = 0; i < array.Length; i++) {
                array[i] = new FigureState();
            }
            return array;
        }

        /// <summary>主题选中时复位：立影按远→近错拍重新汇聚，首拍雷酝酿中</summary>
        internal static void Reset() {
            parallax = Vector2.Zero;
            flash = 0f;
            flashEcho = 0;
            thunderSoundDelay = 0;
            thunderTimer = 600 + Main.rand.Next(600);
            gust = 0.4f;
            gustSurge = 0f;
            gustTimer = 240 + Main.rand.Next(360);
            for (int i = 0; i < states.Length; i++) {
                FigureState st = states[i];
                st.Phase = FigurePhase.Absent;
                st.Timer = 12 + i * 12 + Main.rand.Next(9);
                st.Duration = 1;
                st.Form = 0f;
                st.EyeGlow = 0f;
                st.BlinkTimer = 0;
                st.NextBlink = 360 + Main.rand.Next(540);
            }
            for (int i = 0; i < crowdForm.Length; i++) {
                crowdForm[i] = 0f;
                crowdDelay[i] = 4 + i * 7 % 60 + Main.rand.Next(7);
            }
        }

        /// <summary>释放场景RT（主题切走/卸载）</summary>
        internal static void Release() {
            sceneTarget.SafeDispose();
            sceneTarget = null;
            targetFault = false;
        }

        /// <summary>固定 60tick 推进：视差平滑、立影相位机、目芒眨闪、远雷调度</summary>
        internal static void Tick() {
            tickCount++;

            float nx = 0f, ny = 0f;
            if (Main.hasFocus && Main.screenWidth > 0 && Main.screenHeight > 0) {
                nx = MathHelper.Clamp(Main.mouseX / (float)Main.screenWidth * 2f - 1f, -1f, 1f);
                ny = MathHelper.Clamp(Main.mouseY / (float)Main.screenHeight * 2f - 1f, -1f, 1f);
            }
            parallax += (new Vector2(nx, ny) - parallax) * 0.045f;

            for (int i = 0; i < states.Length; i++) {
                TickFigure(i);
            }
            for (int i = 0; i < crowdForm.Length; i++) {
                if (crowdDelay[i] > 0) {
                    crowdDelay[i]--;
                }
                else if (crowdForm[i] < 1f) {
                    crowdForm[i] = MathF.Min(crowdForm[i] + 1f / CrowdReformDuration, 1f);
                }
            }

            //远雷：光起→数拍后二击回响→再数拍后闷雷落地
            if (--thunderTimer <= 0) {
                thunderTimer = 1080 + Main.rand.Next(1320);
                flash = 1f;
                flashEcho = Main.rand.Next(9, 16);
                thunderSoundDelay = Main.rand.Next(15, 40);
            }
            flash *= 0.86f;
            if (flashEcho > 0 && --flashEcho == 0) {
                flash = MathHelper.Max(flash, 0.55f);
            }
            if (thunderSoundDelay > 0 && --thunderSoundDelay == 0) {
                SoundEngine.PlaySound(SoundID.Thunder with {
                    Volume = 0.38f,
                    Pitch = -0.42f + Main.rand.NextFloat(0.22f),
                    MaxInstances = 2,
                });
            }

            //风暴脉动：双正弦慢涌打底，阵风冲顶后指数回落
            if (--gustTimer <= 0) {
                gustTimer = 420 + Main.rand.Next(780);
                gustSurge = 0.72f + Main.rand.NextFloat(0.28f);
            }
            gustSurge *= 0.99f;
            float swell = 0.5f + 0.30f * MathF.Sin(tickCount * 0.0041f)
                + 0.20f * MathF.Sin(tickCount * 0.0013f + 1.7f);
            gust = MathHelper.Clamp(swell * 0.5f + gustSurge, 0.15f, 1f);
        }

        private static void TickFigure(int i) {
            ShenyoMenuTheme.FigureDef def = ShenyoMenuTheme.Figures[i];
            FigureState st = states[i];

            switch (st.Phase) {
                case FigurePhase.Absent:
                    st.Form = 0f;
                    if (--st.Timer <= 0) {
                        st.Phase = FigurePhase.Reform;
                        st.Duration = 130 + Main.rand.Next(50);
                        st.Timer = st.Duration;
                    }
                    break;
                case FigurePhase.Reform:
                    st.Form = 1f - st.Timer / (float)st.Duration;
                    if (--st.Timer <= 0) {
                        st.Phase = FigurePhase.Formed;
                        st.Form = 1f;
                        //锚影常驻不散
                        st.Timer = def.Anchor ? int.MaxValue / 2 : 700 + Main.rand.Next(900);
                    }
                    break;
                case FigurePhase.Formed:
                    st.Form = 1f;
                    if (--st.Timer <= 0) {
                        st.Phase = FigurePhase.Dissolve;
                        st.Duration = 95;
                        st.Timer = st.Duration;
                    }
                    break;
                case FigurePhase.Dissolve:
                    //uForm 回落=灌注线下沉，读作立影沉回湖里
                    st.Form = st.Timer / (float)st.Duration;
                    if (--st.Timer <= 0) {
                        st.Phase = FigurePhase.Absent;
                        st.Form = 0f;
                        st.Timer = 150 + Main.rand.Next(190);
                    }
                    break;
            }

            //目芒：成形后才睁眼，慢呼吸+偶发眨灭；远影黯近影亮
            float target = 0f;
            bool eyesOpen = st.Phase == FigurePhase.Formed
                || (st.Phase == FigurePhase.Reform && st.Form > 0.9f);
            if (eyesOpen) {
                if (--st.NextBlink <= 0) {
                    st.BlinkTimer = 12;
                    st.NextBlink = 360 + Main.rand.Next(540);
                }
                if (st.BlinkTimer > 0) {
                    st.BlinkTimer--;
                }
                else {
                    //近影目芒压低：贴近镜头的凝视靠"小而稳"发怵，远影靠亮点破雾
                    float baseGlow = 0.44f - 0.12f * def.Depth;
                    target = baseGlow * (0.82f + 0.18f * MathF.Sin(tickCount * 0.015f + i * 2.1f));
                }
            }
            st.EyeGlow += (target - st.EyeGlow) * 0.15f;
        }

        /// <summary>整幅场景绘制；进入时批次须已 End，返回时不留开启批次</summary>
        internal static void Draw(SpriteBatch spriteBatch, float fade) {
            GraphicsDevice graphicsDevice = Main.instance?.GraphicsDevice;
            if (graphicsDevice == null) {
                return;
            }
            int vpW = graphicsDevice.Viewport.Width;
            int vpH = graphicsDevice.Viewport.Height;
            if (vpW <= 0 || vpH <= 0) {
                return;
            }

            Effect lake = EffectLoader.ShenyoMenuLake?.Value;
            Effect ghost = EffectLoader.ShenyoMenuGhost?.Value;
            Effect wet = EffectLoader.ShenyoMenuWet?.Value;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            Texture2D white = VaultAsset.placeholder2?.Value;
            Texture2D portrait = ADVAsset.Shenyo;
            bool portraitReady = portrait != null && !portrait.IsDisposed;

            if (lake == null || ghost == null || noise == null || white == null || !portraitReady) {
                DrawFallback(spriteBatch, vpW, vpH, portrait, portraitReady, white, fade);
                return;
            }

            //湿屏可用时先离屏合幅，再经水珠折射上屏；RT失败退直绘（无湿屏）
            bool useWet = wet != null && EnsureTarget(graphicsDevice, vpW, vpH);
            if (useWet) {
                UnbindTextures(graphicsDevice);
                graphicsDevice.SetRenderTarget(sceneTarget);
                graphicsDevice.Clear(Color.Black);
            }

            float time = (float)Main.timeForVisualEffects * 0.016f;
            DrawLakePass(spriteBatch, graphicsDevice, lake, noise, white, vpW, vpH, time, fade);
            DrawCrowd(spriteBatch, graphicsDevice, ghost, noise, portrait, vpW, vpH, time, fade);
            //远雨幕：细/慢/暗，止于水线——立影身后也在下雨，纵深由遮挡读出
            DrawRainPass(spriteBatch, graphicsDevice, lake, noise, white, vpW, vpH, time, fade,
                new Vector4(1.65f, 0.70f, 0.45f, 0.00f), ShenyoMenuTheme.HorizonY + 0.02f);
            DrawFigureGroup(spriteBatch, graphicsDevice, ghost, noise, portrait, vpW, vpH, time, fade, 0f, 0.30f);
            DrawMistRow(spriteBatch, MistRows[0], vpW, vpH, time, fade);
            DrawFigureGroup(spriteBatch, graphicsDevice, ghost, noise, portrait, vpW, vpH, time, fade, 0.30f, 0.55f);
            //中雨幕：插在中影与近影之间
            DrawRainPass(spriteBatch, graphicsDevice, lake, noise, white, vpW, vpH, time, fade,
                new Vector4(1.25f, 0.85f, 0.65f, 0.02f), ShenyoMenuTheme.HorizonY + 0.20f);
            DrawMistRow(spriteBatch, MistRows[1], vpW, vpH, time, fade);
            DrawFigureGroup(spriteBatch, graphicsDevice, ghost, noise, portrait, vpW, vpH, time, fade, 0.55f, 1.01f);
            //近雨幕：粗/快/亮，全屏盖顶
            DrawRainPass(spriteBatch, graphicsDevice, lake, noise, white, vpW, vpH, time, fade,
                new Vector4(1.00f, 1.15f, 1.00f, 0.03f), 1.30f);

            if (useWet) {
                UnbindTextures(graphicsDevice);
                graphicsDevice.SetRenderTarget(null);
                DrawWetComposite(spriteBatch, graphicsDevice, wet, noise, vpW, vpH, time, fade);
            }
            UnbindTextures(graphicsDevice);
        }

        private static bool EnsureTarget(GraphicsDevice graphicsDevice, int width, int height) {
            if (targetFault) {
                return false;
            }
            if (sceneTarget != null && !sceneTarget.IsDisposed
                && sceneTarget.Width == width && sceneTarget.Height == height) {
                return true;
            }
            sceneTarget.SafeDispose();
            sceneTarget = null;
            try {
                sceneTarget = new RenderTarget2D(graphicsDevice, width, height, false,
                    SurfaceFormat.Color, DepthFormat.None, 0, RenderTargetUsage.DiscardContents);
                return true;
            } catch (Exception exception) {
                targetFault = true;
                CWRMod.Instance?.Logger.Warn($"[ShenyoMenu] 场景RT创建失败，湿屏层停用: {exception.Message}");
                return false;
            }
        }

        //====== 湖景打底（Opaque 整幅铺满）======
        private static void DrawLakePass(SpriteBatch spriteBatch, GraphicsDevice graphicsDevice,
            Effect lake, Texture2D noise, Texture2D white, int vpW, int vpH, float time, float fade) {

            spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.Opaque,
                SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone);
            try {
                graphicsDevice.Textures[1] = noise;
                graphicsDevice.SamplerStates[1] = SamplerState.LinearWrap;
                SetLakeParams(lake, vpW, vpH, time, fade);
                lake.CurrentTechnique = lake.Techniques["TechLake"];
                lake.CurrentTechnique.Passes[0].Apply();
                spriteBatch.Draw(white, new Rectangle(0, 0, vpW, vpH), Color.White);
            } finally {
                spriteBatch.End();
            }
        }

        //====== 雨幕层（预乘 AlphaBlend）：cfg=频率/速度/透明度/斜度，bottom=下缘软截止 ======
        private static void DrawRainPass(SpriteBatch spriteBatch, GraphicsDevice graphicsDevice,
            Effect lake, Texture2D noise, Texture2D white, int vpW, int vpH, float time, float fade,
            Vector4 cfg, float bottom) {

            spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend,
                SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone);
            try {
                graphicsDevice.Textures[1] = noise;
                graphicsDevice.SamplerStates[1] = SamplerState.LinearWrap;
                SetLakeParams(lake, vpW, vpH, time, fade);
                lake.Parameters["uRainCfg"]?.SetValue(cfg);
                lake.Parameters["uRainBottom"]?.SetValue(bottom);
                lake.CurrentTechnique = lake.Techniques["TechRain"];
                lake.CurrentTechnique.Passes[0].Apply();
                spriteBatch.Draw(white, new Rectangle(0, 0, vpW, vpH), Color.White);
            } finally {
                spriteBatch.End();
            }
        }

        //共享参数化 shader 的 uniform 是设备全局状态：两个 technique 的调用点都全参数重设
        private static void SetLakeParams(Effect lake, int vpW, int vpH, float time, float fade) {
            lake.Parameters["uTime"]?.SetValue(time);
            lake.Parameters["uIntensity"]?.SetValue(fade);
            lake.Parameters["uScreenSize"]?.SetValue(new Vector2(vpW, vpH));
            lake.Parameters["uParallax"]?.SetValue(parallax * ShenyoMenuTheme.ParallaxMax);
            lake.Parameters["uFlash"]?.SetValue(flash);
            lake.Parameters["uGust"]?.SetValue(gust);
            lake.Parameters["uHorizon"]?.SetValue(ShenyoMenuTheme.HorizonY);
            lake.Parameters["uMoonUv"]?.SetValue(ShenyoMenuTheme.MoonUv);
            FillFeetBuffer(fade);
            lake.Parameters["uFeet"]?.SetValue(feetBuffer);
        }

        private static void FillFeetBuffer(float fade) {
            float parX = parallax.X * ShenyoMenuTheme.ParallaxMax.X;
            for (int i = 0; i < feetBuffer.Length; i++) {
                if (i >= ShenyoMenuTheme.Figures.Length) {
                    feetBuffer[i] = new Vector4(0.5f, 0.5f, 0f, 0.01f);
                    continue;
                }
                ShenyoMenuTheme.FigureDef def = ShenyoMenuTheme.Figures[i];
                float x = def.X + parX * ShenyoMenuTheme.FigureParallax(def.Depth);
                feetBuffer[i] = new Vector4(x, ShenyoMenuTheme.FigureFeetY(def.Depth),
                    states[i].Form * fade, ShenyoMenuTheme.FigureRingRadius(def.Depth));
            }
        }

        //====== 叠影群：水线处一排交叠微影，重模糊高潮雾、常驻不散、零星淡眼 ======
        private static void DrawCrowd(SpriteBatch spriteBatch, GraphicsDevice graphicsDevice,
            Effect ghost, Texture2D noise, Texture2D portrait, int vpW, int vpH, float time, float fade) {

            spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend,
                SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone);
            try {
                graphicsDevice.Textures[1] = noise;
                graphicsDevice.SamplerStates[1] = SamplerState.LinearWrap;
                ghost.CurrentTechnique = ghost.Techniques["TechGhost"];

                foreach (int i in crowdOrder) {
                    float form = crowdForm[i];
                    if (form <= 0.003f) {
                        continue;
                    }
                    ShenyoMenuTheme.CrowdDef def = ShenyoMenuTheme.Crowd[i];
                    float heightPx = ShenyoMenuTheme.FigureHeight(def.Depth) * vpH;
                    float scale = heightPx / portrait.Height;
                    float widthPx = portrait.Width * scale;
                    float xUv = def.X + parallax.X * ShenyoMenuTheme.ParallaxMax.X
                        * ShenyoMenuTheme.FigureParallax(def.Depth);
                    float xPx = xUv * vpW;
                    float feetPx = ShenyoMenuTheme.FigureFeetY(def.Depth) * vpH;
                    SpriteEffects flip = def.Flip ? SpriteEffects.FlipHorizontally : SpriteEffects.None;

                    //淡眼慢呼吸；大多数为无目的纯剪影
                    float eyeGlow = def.EyeMul <= 0f ? 0f
                        : def.EyeMul * 0.42f * (0.80f + 0.20f * MathF.Sin(tickCount * 0.011f + i * 1.7f));
                    float blur = ShenyoMenuTheme.BlurTexels(2.2f, scale);
                    //雾化封顶：群影必须比地平雾暗一档，否则融底隐形
                    float haze = MathHelper.Clamp(ShenyoMenuTheme.FigureHaze(def.Depth) + 0.06f, 0f, 0.62f);
                    Vector2 moonDir = MoonDirTex(xPx, feetPx - heightPx * 0.62f, vpW, vpH, def.Flip);

                    SetGhostParams(ghost, time, form, 0f, haze, reflect: 0f,
                        alpha: def.Alpha * fade, wobble: 1.2f, seed: 20f + i * 1.91f,
                        eyeGlow, moonDir, blur);
                    ghost.CurrentTechnique.Passes[0].Apply();
                    spriteBatch.Draw(portrait, new Vector2(xPx - widthPx * 0.5f, feetPx - heightPx), null,
                        Color.White, 0f, Vector2.Zero, scale, flip, 0f);
                }
            } finally {
                spriteBatch.End();
            }
        }

        //====== 立影分组绘制：倒影先画、重影错版垫底、本体压上；Immediate 逐影全参数上载 ======
        private static void DrawFigureGroup(SpriteBatch spriteBatch, GraphicsDevice graphicsDevice,
            Effect ghost, Texture2D noise, Texture2D portrait, int vpW, int vpH,
            float time, float fade, float depthMin, float depthMax) {

            spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend,
                SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone);
            try {
                graphicsDevice.Textures[1] = noise;
                graphicsDevice.SamplerStates[1] = SamplerState.LinearWrap;
                ghost.CurrentTechnique = ghost.Techniques["TechGhost"];

                for (int i = 0; i < ShenyoMenuTheme.Figures.Length; i++) {
                    ShenyoMenuTheme.FigureDef def = ShenyoMenuTheme.Figures[i];
                    if (def.Depth < depthMin || def.Depth >= depthMax) {
                        continue;
                    }
                    FigureState st = states[i];
                    if (st.Form <= 0.003f) {
                        continue;
                    }

                    float heightPx = ShenyoMenuTheme.FigureHeight(def.Depth) * vpH;
                    float scale = heightPx / portrait.Height;
                    float widthPx = portrait.Width * scale;
                    float xUv = def.X + parallax.X * ShenyoMenuTheme.ParallaxMax.X
                        * ShenyoMenuTheme.FigureParallax(def.Depth);
                    float xPx = xUv * vpW;
                    float feetPx = ShenyoMenuTheme.FigureFeetY(def.Depth) * vpH;

                    SpriteEffects flip = def.Flip ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
                    //缘光光向：逐影指向溺月（翻面镜像u轴，倒影再翻v轴）
                    Vector2 moonDir = MoonDirTex(xPx, feetPx - heightPx * 0.62f, vpW, vpH, def.Flip);
                    Vector2 moonDirRefl = new(moonDir.X, -moonDir.Y);
                    float wobble = ShenyoMenuTheme.FigureWobble(def.Depth);
                    float haze = ShenyoMenuTheme.FigureHaze(def.Depth);
                    float seed = i * 1.37f + 0.7f;
                    float blur = ShenyoMenuTheme.BlurTexels(ShenyoMenuTheme.FigureBlurPx(def.Depth), scale);
                    Vector2 bodyPos = new(xPx - widthPx * 0.5f, feetPx - heightPx);

                    //倒影（足点在屏内才有得画）
                    if (feetPx < vpH - 2f) {
                        SetGhostParams(ghost, time, st.Form, 0f, haze + 0.10f, reflect: 1f,
                            alpha: 0.85f * fade, wobble, seed, st.EyeGlow * 0.35f, moonDirRefl, blur);
                        ghost.CurrentTechnique.Passes[0].Apply();
                        spriteBatch.Draw(portrait, new Vector2(xPx - widthPx * 0.5f, feetPx), null,
                            Color.White, 0f, Vector2.Zero, new Vector2(scale, scale * 0.82f),
                            flip | SpriteEffects.FlipVertically, 0f);
                    }

                    //重影错版：远/中影的第二重曝光缓慢漂移，锚影保持清晰不叠
                    if (!def.Anchor && def.Depth < 0.55f) {
                        bool far = def.Depth < 0.30f;
                        float echoAmp = far ? 2.2f : 1.4f;
                        Vector2 echoOff = new(
                            MathF.Sin(time * 0.9f + i * 2.3f) * echoAmp,
                            MathF.Cos(time * 0.7f + i * 1.9f) * echoAmp * 0.6f - echoAmp * 0.4f);
                        SetGhostParams(ghost, time, st.Form, 0f,
                            MathHelper.Clamp(haze + 0.15f, 0f, 0.85f), reflect: 0f,
                            alpha: (far ? 0.34f : 0.20f) * st.Form * fade, wobble, seed + 7.7f,
                            eyeGlow: 0f, moonDir, blur * 1.5f);
                        ghost.CurrentTechnique.Passes[0].Apply();
                        spriteBatch.Draw(portrait, bodyPos + echoOff, null,
                            Color.White, 0f, Vector2.Zero, scale, flip, 0f);
                    }

                    //本体
                    SetGhostParams(ghost, time, st.Form, def.Clarity, haze, reflect: 0f,
                        alpha: fade, wobble, seed, st.EyeGlow, moonDir, blur);
                    ghost.CurrentTechnique.Passes[0].Apply();
                    spriteBatch.Draw(portrait, bodyPos, null,
                        Color.White, 0f, Vector2.Zero, scale, flip, 0f);
                }
            } finally {
                spriteBatch.End();
            }
        }

        private static void SetGhostParams(Effect ghost, float time, float form, float clarity,
            float haze, float reflect, float alpha, float wobble, float seed, float eyeGlow,
            Vector2 moonDir, float blur) {
            ghost.Parameters["uTime"]?.SetValue(time);
            ghost.Parameters["uForm"]?.SetValue(form);
            ghost.Parameters["uClarity"]?.SetValue(clarity);
            ghost.Parameters["uHaze"]?.SetValue(MathHelper.Clamp(haze, 0f, 1f));
            ghost.Parameters["uReflect"]?.SetValue(reflect);
            ghost.Parameters["uAlpha"]?.SetValue(alpha);
            ghost.Parameters["uWobble"]?.SetValue(wobble);
            ghost.Parameters["uSeed"]?.SetValue(seed);
            ghost.Parameters["uTexel"]?.SetValue(ShenyoMenuTheme.PortraitTexel);
            ghost.Parameters["uEyeUv"]?.SetValue(ShenyoMenuTheme.EyeUv);
            ghost.Parameters["uEyeSep"]?.SetValue(ShenyoMenuTheme.EyeSep);
            ghost.Parameters["uEyeGlow"]?.SetValue(eyeGlow);
            ghost.Parameters["uMoonDir"]?.SetValue(moonDir);
            ghost.Parameters["uBlur"]?.SetValue(blur);
        }

        //指向溺月的纹理空间光向：翻面镜像u轴；倒影另由调用侧翻v
        private static Vector2 MoonDirTex(float xPx, float chestPx, int vpW, int vpH, bool flip) {
            Vector2 dir = new(ShenyoMenuTheme.MoonUv.X * vpW - xPx, ShenyoMenuTheme.MoonUv.Y * vpH - chestPx);
            if (dir.LengthSquared() < 1f) {
                dir = new Vector2(0f, -1f);
            }
            dir.Normalize();
            return new Vector2(flip ? -dir.X : dir.X, dir.Y);
        }

        //====== 潮雾带：Fog 真alpha贴片慢漂，普通批（Immediate 粘滞着色器不可染指）======
        private static void DrawMistRow(SpriteBatch spriteBatch, MistRow row,
            int vpW, int vpH, float time, float fade) {

            Texture2D fog = CWRAsset.Fog?.Value;
            if (fog == null) {
                return;
            }

            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend,
                SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone);
            try {
                Vector2 origin = fog.Size() * 0.5f;
                float parPx = parallax.X * ShenyoMenuTheme.ParallaxMax.X * row.Parallax * vpW;
                Vector2 puffScale = new(vpW * row.WidthFrac / fog.Width, vpH * row.HeightFrac / fog.Height);
                for (int k = 0; k < row.Count; k++) {
                    //环带漂移：-0.25~1.25 回绕，出画再进画
                    float xCycle = (row.Seed + k / (float)row.Count + time * row.DriftSpeed) % 1f;
                    float xPx = (xCycle * 1.5f - 0.25f) * vpW + parPx;
                    float yPx = (row.Y + MathF.Sin(time * 0.11f + k * 2.3f + row.Seed * 9f) * 0.006f) * vpH;
                    //同屏多片必须逐片镜像，否则读成同一张贴纸盖三遍
                    SpriteEffects mirror = ((k + (int)(row.Seed * 10f)) & 1) == 0
                        ? SpriteEffects.None : SpriteEffects.FlipHorizontally;
                    spriteBatch.Draw(fog, new Vector2(xPx, yPx), null,
                        ShenyoMenuTheme.MistTint * (row.Alpha * fade), 0f, origin, puffScale, mirror, 0f);
                }
            } finally {
                spriteBatch.End();
            }
        }

        //====== 湿屏合成：场景RT经水珠折射整幅上屏 ======
        private static void DrawWetComposite(SpriteBatch spriteBatch, GraphicsDevice graphicsDevice,
            Effect wet, Texture2D noise, int vpW, int vpH, float time, float fade) {

            spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.Opaque,
                SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone);
            try {
                graphicsDevice.Textures[1] = noise;
                graphicsDevice.SamplerStates[1] = SamplerState.LinearWrap;
                wet.Parameters["uTime"]?.SetValue(time);
                wet.Parameters["uScreenSize"]?.SetValue(new Vector2(vpW, vpH));
                //湿度随风暴脉动呼吸：阵风越猛镜头越湿、溅击越密
                wet.Parameters["uWet"]?.SetValue(0.70f + 0.30f * gust);
                wet.Parameters["uFlash"]?.SetValue(flash);
                wet.Parameters["uGust"]?.SetValue(gust);
                wet.Parameters["uIntensity"]?.SetValue(fade);
                wet.CurrentTechnique = wet.Techniques["TechWet"];
                wet.CurrentTechnique.Passes[0].Apply();
                spriteBatch.Draw(sceneTarget, new Rectangle(0, 0, vpW, vpH), Color.White);
            } finally {
                spriteBatch.End();
            }
        }

        //====== CPU 回退：渐变天水+压色立影+目点，无着色器也不黑屏 ======
        private static void DrawFallback(SpriteBatch spriteBatch, int vpW, int vpH,
            Texture2D portrait, bool portraitReady, Texture2D white, float fade) {

            if (white == null) {
                return;
            }

            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend,
                SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone);
            try {
                //天水双段渐变条带
                const int Strips = 24;
                int horizonPx = (int)(ShenyoMenuTheme.HorizonY * vpH);
                for (int i = 0; i < Strips; i++) {
                    float t0 = i / (float)Strips;
                    float t1 = (i + 1f) / Strips;
                    int y0 = (int)(t0 * vpH);
                    int height = Math.Max(1, (int)(t1 * vpH) - y0);
                    Color color;
                    if (y0 < horizonPx) {
                        float k = MathF.Pow(y0 / (float)horizonPx, 1.3f);
                        color = Color.Lerp(ShenyoMenuTheme.FallbackSkyTop, ShenyoMenuTheme.FallbackSkyHorizon, k);
                    }
                    else {
                        float k = MathHelper.Clamp((y0 - horizonPx) / (float)Math.Max(vpH - horizonPx, 1), 0f, 1f);
                        color = Color.Lerp(ShenyoMenuTheme.FallbackSkyHorizon, ShenyoMenuTheme.FallbackWaterDeep,
                            MathF.Pow(k, 0.6f));
                    }
                    spriteBatch.Draw(white, new Rectangle(0, y0, vpW, height), color * fade);
                }

                //溺月：黑底辉光图走 A=0 加色语义
                Texture2D glow = CWRAsset.SoftGlow?.Value;
                if (glow != null) {
                    Vector2 moonPos = new(ShenyoMenuTheme.MoonUv.X * vpW, ShenyoMenuTheme.MoonUv.Y * vpH);
                    Color moonCol = new(150, 165, 170, 0);
                    spriteBatch.Draw(glow, moonPos, null, moonCol * fade, 0f,
                        glow.Size() * 0.5f, vpH * 0.0055f, SpriteEffects.None, 0f);
                }

                if (!portraitReady) {
                    return;
                }

                //立影：压色本体+翻面淡倒影+目点
                foreach (ShenyoMenuTheme.FigureDef def in ShenyoMenuTheme.Figures) {
                    float heightPx = ShenyoMenuTheme.FigureHeight(def.Depth) * vpH;
                    float scale = heightPx / portrait.Height;
                    float widthPx = portrait.Width * scale;
                    float xPx = def.X * vpW - widthPx * 0.5f;
                    float feetPx = ShenyoMenuTheme.FigureFeetY(def.Depth) * vpH;
                    SpriteEffects flip = def.Flip ? SpriteEffects.FlipHorizontally : SpriteEffects.None;

                    if (feetPx < vpH - 2f) {
                        spriteBatch.Draw(portrait, new Vector2(xPx, feetPx), null,
                            ShenyoMenuTheme.FallbackMurk * (0.30f * fade), 0f, Vector2.Zero,
                            new Vector2(scale, scale * 0.82f), flip | SpriteEffects.FlipVertically, 0f);
                    }
                    spriteBatch.Draw(portrait, new Vector2(xPx, feetPx - heightPx), null,
                        ShenyoMenuTheme.FallbackMurk * fade, 0f, Vector2.Zero, scale, flip, 0f);

                    //目点：翻面时眼位横向镜像
                    float eyeU = def.Flip ? 1f - ShenyoMenuTheme.EyeUv.X : ShenyoMenuTheme.EyeUv.X;
                    float eyeY = feetPx - heightPx + ShenyoMenuTheme.EyeUv.Y * portrait.Height * scale;
                    float dotSize = MathF.Max(1.5f, heightPx * 0.006f);
                    for (int e = -1; e <= 1; e += 2) {
                        float eyeX = xPx + (eyeU + e * ShenyoMenuTheme.EyeSep) * portrait.Width * scale;
                        spriteBatch.Draw(white, new Vector2(eyeX, eyeY), null,
                            ShenyoMenuTheme.AccentWater * (0.75f * fade), 0f, new Vector2(0.5f),
                            dotSize, SpriteEffects.None, 0f);
                    }
                }
            } finally {
                spriteBatch.End();
            }
        }

        private static void UnbindTextures(GraphicsDevice graphicsDevice) {
            graphicsDevice.Textures[0] = null;
            graphicsDevice.Textures[1] = null;
        }
    }

    internal sealed class ShenyoGhostLakeSceneLoader : ICWRLoader
    {
        void ICWRLoader.UnLoadData() => ShenyoGhostLakeScene.Release();
    }
}
