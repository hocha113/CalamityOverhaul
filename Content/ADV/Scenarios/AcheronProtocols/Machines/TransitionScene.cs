using InnoVault.UIHandles;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.GameContent;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.ADV.Scenarios.AcheronProtocols.Machines
{
    internal class TransitionSceneDebug : UIHandle
    {
        public override LayersModeEnum LayersMode => LayersModeEnum.Mod_MenuLoad;
        public override float RenderPriority => 2;
        public override bool Active => true;
        public override void Update() {
            //var current = Main.keyState;
            //var previous = Main.oldKeyState;
            //if (current.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.D1)
            //  && !previous.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.D1)) {
            //  if (TransitionScene.IsShowing) {
            //      TransitionScene.Hide();
            //  }
            //  else {
            //      TransitionScene.Show();
            //  }
            //}
        }
    }

    /// <summary>

    /// 科幻风格过渡加载界面，灵感来自星际战士2的加载画面 全屏覆盖，带进度条、动态粒子、电路脉冲、数据流和底部滚动提示文字

    /// </summary>
    internal class TransitionScene : UIHandle, ILocalizedModType
    {
        public override LayersModeEnum LayersMode => LayersModeEnum.Mod_MenuLoad;
        public override float RenderPriority => 2;
        public string LocalizationCategory => "UI";

        public static TransitionScene Instance => UIHandleLoader.GetUIHandleOfType<TransitionScene>();

        #region 本地化

        public static LocalizedText LoadingText { get; private set; }
        public static LocalizedText InitializingText { get; private set; }
        public static LocalizedText[] HintTexts { get; private set; }

        //通信日志终端消息（循环滚动显示）
        private static readonly string[] TerminalMessages = [
            "> SYS BOOT    [OK]",
            "> NET INIT    [OK]",
            "> PROTOCOL    LINK",
            "> ENCRYPT     [OK]",
            "> DATA SYNC  [RUN]",
            "> CORE AUTH    ..",
            "> NODES : 1024",
            "> BANDWIDTH  MAX",
            "> CACHE    CLEAR",
            "> ACHERON ACTIVE",
            "> WARMACHINE [OK]",
            "> SCAN FIELD  [ON]",
        ];

        #endregion

        #region 状态控制

        private bool _active;
        public override bool Active => _active || fadeAlpha > 0.001f;

        private float fadeAlpha;
        private bool isFadingIn;
        private bool isFadingOut;

        /// <summary>

        /// 当前加载进度，0~1

        /// </summary>
        private float currentProgress;

        /// <summary>

        /// 目标进度（可由外部设置）

        /// </summary>
        private float targetProgress;

        /// <summary>

        /// 假进度模式下的自动递增速度

        /// </summary>
        private bool fakeProgressMode = true;
        private float fakeProgressSpeed = 0.003f;

        #endregion

        #region 动画计时器

        private float globalTime;
        private float scanLineTimer;
        private float circuitPulseTimer;
        private float dataStreamTimer;
        private float hexGridPhase;
        private float hologramFlicker;
        private float progressGlowPhase;
        private float warningPulse;
        private float glitchTimer;

        #endregion

        #region 均衡器可视化

        private readonly float[] eqBars = new float[14];
        private readonly float[] eqTargets = new float[14];
        private int eqUpdateTimer;

        #endregion

        #region 粒子系统

        private readonly List<TechParticle> particles = [];
        private int particleSpawnTimer;
        private readonly List<DataStreamLine> dataStreams = [];
        private int dataStreamSpawnTimer;
        private readonly List<CircuitNode> circuitNodes = [];
        private int circuitNodeSpawnTimer;
        private readonly List<HexCell> hexCells = [];
        private int hexCellSpawnTimer;

        #endregion

        #region 提示文字系统

        private int currentHintIndex;
        private float hintFadeProgress;
        private float hintDisplayTimer;
        private const float HintDisplayDuration = 200f;
        private const float HintFadeDuration = 25f;
        private enum HintState { FadeIn, Display, FadeOut }
        private HintState hintState = HintState.FadeIn;

        #endregion

        #region 进度条布局

        private const float ProgressBarWidth = 650f;
        private const float ProgressBarHeight = 10f;
        private const float ProgressBarY = 0.72f;

        #endregion

        #region 装饰几何元素

        private float aquilaRotation;
        private float outerRingRotation;
        private float innerRingRotation;

        #endregion

        public override void SetStaticDefaults() {
            LoadingText = this.GetLocalization(nameof(LoadingText), () => "数据链接中");
            InitializingText = this.GetLocalization(nameof(InitializingText), () => "正在初始化协议");

            HintTexts = [
                this.GetLocalization("Hint0", () => "不要问为什么杀异形，而是要问为何不杀"),
                this.GetLocalization("Hint1", () => "阿切隆协议是嘉登最高级别的应急方案之一"),
                this.GetLocalization("Hint2", () => "战术人形无所畏惧，因为恐惧本身就是他们的武器"),
                this.GetLocalization("Hint3", () => "嘉登的机械造物已经超越了现实的极限"),
                this.GetLocalization("Hint4", () => "没有大到无法接受的牺牲，没有小到可以原谅的背叛"),
                this.GetLocalization("Hint5", () => "任何足够先进的技术，皆与魔法无异"),
                //this.GetLocalization("Hint6", () => "灾厄之中蕴藏着超越一切的可能性"),
                this.GetLocalization("Hint7", () => "嘉登从不做无意义的事，每一步都是精密计算的结果"),
                this.GetLocalization("Hint8", () => "数据即是力量，知识即是武装"),
                this.GetLocalization("Hint9", () => "机械不会背叛，但操控机械的人或许会"),
            ];
        }

        #region 公开接口

        /// <summary>

        /// 显示加载界面

        /// </summary>
        /// <param name="useFakeProgress">是否使用假进度</param>
        public static void Show(bool useFakeProgress = true) {
            var inst = Instance;
            if (inst == null) return;
            inst._active = true;
            inst.isFadingIn = true;
            inst.isFadingOut = false;
            inst.fakeProgressMode = useFakeProgress;
            inst.currentProgress = 0f;
            inst.targetProgress = 0f;
            inst.ResetAnimations();
        }

        /// <summary>

        /// 隐藏加载界面（淡出）

        /// </summary>
        public static void Hide() {
            var inst = Instance;
            if (inst == null) return;
            inst.isFadingOut = true;
            inst.isFadingIn = false;
        }

        /// <summary>

        /// 设置目标进度（非假进度模式下使用）

        /// </summary>
        public static void SetProgress(float progress) {
            var inst = Instance;
            if (inst == null) return;
            inst.fakeProgressMode = false;
            inst.targetProgress = Math.Clamp(progress, 0f, 1f);
        }

        public static bool IsShowing => Instance?._active ?? false;

        #endregion

        private void ResetAnimations() {
            globalTime = 0f;
            scanLineTimer = 0f;
            circuitPulseTimer = 0f;
            dataStreamTimer = 0f;
            hexGridPhase = 0f;
            hologramFlicker = 0f;
            progressGlowPhase = 0f;
            warningPulse = 0f;
            particles.Clear();
            dataStreams.Clear();
            circuitNodes.Clear();
            hexCells.Clear();
            particleSpawnTimer = 0;
            dataStreamSpawnTimer = 0;
            circuitNodeSpawnTimer = 0;
            hexCellSpawnTimer = 0;
            currentHintIndex = Main.rand?.Next(HintTexts?.Length ?? 1) ?? 0;
            hintFadeProgress = 0f;
            hintDisplayTimer = 0f;
            hintState = HintState.FadeIn;
            aquilaRotation = 0f;
            outerRingRotation = 0f;
            innerRingRotation = 0f;
            glitchTimer = 0f;
            eqUpdateTimer = 0;
            Array.Clear(eqBars, 0, eqBars.Length);
            Array.Clear(eqTargets, 0, eqTargets.Length);
        }

        public override void Update() {
            float dt = 0.016f;
            globalTime += dt;

            //淡入淡出
            if (isFadingIn) {
                fadeAlpha = 1f;
                if (fadeAlpha >= 1f) {
                    fadeAlpha = 1f;
                    isFadingIn = false;
                }
            }

            if (isFadingOut) {
                fadeAlpha = 0;
                if (fadeAlpha <= 0f) {
                    fadeAlpha = 0f;
                    isFadingOut = false;
                    _active = false;
                    ResetAnimations();
                    return;
                }
            }

            if (fadeAlpha <= 0.001f) return;

            //动画计时器
            scanLineTimer += 0.035f;
            circuitPulseTimer += 0.02f;
            dataStreamTimer += 0.04f;
            hexGridPhase += 0.012f;
            hologramFlicker += 0.08f;
            progressGlowPhase += 0.06f;
            warningPulse += 0.05f;

            if (scanLineTimer > MathHelper.TwoPi) scanLineTimer -= MathHelper.TwoPi;
            if (circuitPulseTimer > MathHelper.TwoPi) circuitPulseTimer -= MathHelper.TwoPi;
            if (dataStreamTimer > MathHelper.TwoPi) dataStreamTimer -= MathHelper.TwoPi;
            if (hexGridPhase > MathHelper.TwoPi) hexGridPhase -= MathHelper.TwoPi;
            if (hologramFlicker > MathHelper.TwoPi) hologramFlicker -= MathHelper.TwoPi;

            //故障效果计时器
            glitchTimer += 0.15f;
            if (glitchTimer > MathHelper.TwoPi) glitchTimer -= MathHelper.TwoPi;

            //装饰元素旋转
            aquilaRotation += 0.002f;
            outerRingRotation += 0.008f;
            innerRingRotation -= 0.012f;

            //进度更新
            if (fakeProgressMode) {
                //假进度：缓慢递增，到90%减速，永远不会自行到100%
                float speedMod = currentProgress < 0.6f ? 1f : currentProgress < 0.85f ? 0.4f : 0.08f;
                currentProgress += fakeProgressSpeed * speedMod;
                currentProgress = Math.Min(currentProgress, 0.98f);
            }
            else {
                //真进度：平滑插值到目标值
                currentProgress += (targetProgress - currentProgress) * 0.08f;
                if (Math.Abs(currentProgress - targetProgress) < 0.001f)
                    currentProgress = targetProgress;
            }

            //更新粒子
            UpdateParticles();

            //更新均衡器条
            eqUpdateTimer++;
            if (eqUpdateTimer >= 8) {
                eqUpdateTimer = 0;
                for (int i = 0; i < eqTargets.Length; i++)
                    eqTargets[i] = Main.rand?.NextFloat(0.12f, 1f) ?? 0.5f;
            }
            for (int i = 0; i < eqBars.Length; i++)
                eqBars[i] = MathHelper.Lerp(eqBars[i], eqTargets[i], 0.2f);

            //更新提示文字
            UpdateHintText();
        }

        private void UpdateParticles() {
            //通用科技粒子
            particleSpawnTimer++;
            if (particleSpawnTimer >= 3 && particles.Count < 60) {
                particleSpawnTimer = 0;
                SpawnTechParticle();
            }
            for (int i = particles.Count - 1; i >= 0; i--) {
                if (particles[i].Update()) particles.RemoveAt(i);
            }

            //数据流线条
            dataStreamSpawnTimer++;
            if (dataStreamSpawnTimer >= 12 && dataStreams.Count < 20) {
                dataStreamSpawnTimer = 0;
                SpawnDataStream();
            }
            for (int i = dataStreams.Count - 1; i >= 0; i--) {
                if (dataStreams[i].Update()) dataStreams.RemoveAt(i);
            }

            //电路节点
            circuitNodeSpawnTimer++;
            if (circuitNodeSpawnTimer >= 30 && circuitNodes.Count < 10) {
                circuitNodeSpawnTimer = 0;
                SpawnCircuitNode();
            }
            for (int i = circuitNodes.Count - 1; i >= 0; i--) {
                if (circuitNodes[i].Update()) circuitNodes.RemoveAt(i);
            }

            //六边形网格高亮
            hexCellSpawnTimer++;
            if (hexCellSpawnTimer >= 8 && hexCells.Count < 25) {
                hexCellSpawnTimer = 0;
                SpawnHexCell();
            }
            for (int i = hexCells.Count - 1; i >= 0; i--) {
                if (hexCells[i].Update()) hexCells.RemoveAt(i);
            }
        }

        private void SpawnTechParticle() {
            int sw = Main.screenWidth;
            int sh = Main.screenHeight;
            //从屏幕边缘或中心区域生成
            Vector2 pos;
            Vector2 vel;
            int spawnType = Main.rand.Next(3);
            if (spawnType == 0) {
                //从底部上升
                pos = new Vector2(Main.rand.NextFloat(sw), sh + 10);
                vel = new Vector2(Main.rand.NextFloat(-0.3f, 0.3f), Main.rand.NextFloat(-2f, -0.8f));
            }
            else if (spawnType == 1) {
                //从侧面飘入
                bool left = Main.rand.NextBool();
                pos = new Vector2(left ? -10 : sw + 10, Main.rand.NextFloat(sh));
                vel = new Vector2(left ? Main.rand.NextFloat(0.5f, 1.5f) : Main.rand.NextFloat(-1.5f, -0.5f), Main.rand.NextFloat(-0.5f, 0.5f));
            }
            else {
                //在屏幕中生成
                pos = new Vector2(Main.rand.NextFloat(sw * 0.1f, sw * 0.9f), Main.rand.NextFloat(sh * 0.1f, sh * 0.9f));
                vel = Main.rand.NextFloat(MathHelper.TwoPi).ToRotationVector2() * Main.rand.NextFloat(0.2f, 0.8f);
            }

            Color baseColor = Main.rand.Next(4) switch {
                0 => new Color(60, 180, 255),
                1 => new Color(80, 220, 255),
                2 => new Color(40, 140, 200),
                _ => new Color(100, 200, 255)
            };

            float life = Main.rand.NextFloat(2f, 5f);
            particles.Add(new TechParticle {
                Position = pos,
                Velocity = vel,
                Life = life,
                MaxLife = life,
                Size = Main.rand.NextFloat(1.5f, 4f),
                Rotation = Main.rand.NextFloat(MathHelper.TwoPi),
                RotationSpeed = Main.rand.NextFloat(-0.03f, 0.03f),
                BaseColor = baseColor
            });
        }

        private void SpawnDataStream() {
            int sw = Main.screenWidth;
            int sh = Main.screenHeight;
            bool horizontal = Main.rand.NextBool();
            Vector2 start, end;
            if (horizontal) {
                float y = Main.rand.NextFloat(sh * 0.05f, sh * 0.95f);
                start = new Vector2(-20, y);
                end = new Vector2(sw + 20, y + Main.rand.NextFloat(-30, 30));
            }
            else {
                float x = Main.rand.NextFloat(sw * 0.05f, sw * 0.95f);
                start = new Vector2(x, -20);
                end = new Vector2(x + Main.rand.NextFloat(-30, 30), sh + 20);
            }

            float dsLife = Main.rand.NextFloat(1f, 2.5f);
            dataStreams.Add(new DataStreamLine {
                Start = start,
                End = end,
                Life = dsLife,
                MaxLife = dsLife,
                Progress = 0f,
                Speed = Main.rand.NextFloat(0.01f, 0.03f),
                Thickness = Main.rand.NextFloat(1f, 2.5f),
                LineColor = new Color(60, 180, 255) * Main.rand.NextFloat(0.15f, 0.4f)
            });
        }

        private void SpawnCircuitNode() {
            int sw = Main.screenWidth;
            int sh = Main.screenHeight;
            Vector2 pos = new(Main.rand.NextFloat(sw * 0.05f, sw * 0.95f), Main.rand.NextFloat(sh * 0.05f, sh * 0.95f));
            int branchCount = Main.rand.Next(2, 5);
            var branches = new List<Vector2>();
            for (int i = 0; i < branchCount; i++) {
                float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                float len = Main.rand.NextFloat(40, 120);
                //直角化：只水平或垂直延伸
                if (Main.rand.NextBool()) {
                    branches.Add(pos + new Vector2(Main.rand.NextBool() ? len : -len, 0));
                }
                else {
                    branches.Add(pos + new Vector2(0, Main.rand.NextBool() ? len : -len));
                }
            }
            float cnLife = Main.rand.NextFloat(2f, 4f);
            circuitNodes.Add(new CircuitNode {
                Center = pos,
                Branches = branches,
                Life = cnLife,
                MaxLife = cnLife,
                PulsePhase = Main.rand.NextFloat(MathHelper.TwoPi)
            });
        }

        private void SpawnHexCell() {
            int sw = Main.screenWidth;
            int sh = Main.screenHeight;
            float hcLife = Main.rand.NextFloat(1.5f, 3.5f);
            hexCells.Add(new HexCell {
                Position = new Vector2(Main.rand.NextFloat(sw), Main.rand.NextFloat(sh)),
                Size = Main.rand.NextFloat(20f, 50f),
                Life = hcLife,
                MaxLife = hcLife,
                Rotation = Main.rand.NextFloat(MathHelper.TwoPi),
                PulsePhase = Main.rand.NextFloat(MathHelper.TwoPi)
            });
        }

        private void UpdateHintText() {
            if (HintTexts == null || HintTexts.Length == 0) return;

            switch (hintState) {
                case HintState.FadeIn:
                    hintFadeProgress += 1f / HintFadeDuration;
                    if (hintFadeProgress >= 1f) {
                        hintFadeProgress = 1f;
                        hintState = HintState.Display;
                        hintDisplayTimer = 0f;
                    }
                    break;
                case HintState.Display:
                    hintDisplayTimer++;
                    if (hintDisplayTimer >= HintDisplayDuration) {
                        hintState = HintState.FadeOut;
                    }
                    break;
                case HintState.FadeOut:
                    hintFadeProgress -= 1f / HintFadeDuration;
                    if (hintFadeProgress <= 0f) {
                        hintFadeProgress = 0f;
                        currentHintIndex = (currentHintIndex + 1) % HintTexts.Length;
                        hintState = HintState.FadeIn;
                    }
                    break;
            }
        }

        #region 绘制

        //顶部标题栏高度
        private const float HeaderBarH = 44f;
        //底部状态栏高度
        private const float FooterBarH = 56f;
        //左右侧面板宽度比例
        private const float SidePanelRatio = 0.21f;

        //系统状态标签（左侧面板）
        private static readonly string[] StatusLabels = ["SYS CORE", "NET LINK", "ENCRYPT", "AUTH MOD", "DATA SYNC"];
        //对应状态类型：0=ONLINE 1=ACTIVE 2=LOADING 3=STANDBY
        private static readonly int[] StatusTypes = [0, 0, 1, 2, 3];

        public override void Draw(SpriteBatch spriteBatch) {
            if (fadeAlpha <= 0.001f) return;

            float alpha = fadeAlpha;
            int sw = Main.screenWidth;
            int sh = Main.screenHeight;
            Texture2D pixel = VaultAsset.placeholder2.Value;

            //不透明实色底层（整个加载界面的基础背景）
            DrawSolidBackground(spriteBatch, pixel, sw, sh, alpha);

            //背景六边形静态点阵纹理
            DrawBackgroundDotGrid(spriteBatch, pixel, sw, sh, alpha);

            //动态六边形网格高亮单元
            DrawHexCells(spriteBatch, pixel, alpha);

            //电路节点与分支脉冲
            DrawCircuitNodes(spriteBatch, pixel, alpha);

            //穿越屏幕的数据流线条
            DrawDataStreams(spriteBatch, pixel, alpha);

            //滚动扫描线
            DrawScanLine(spriteBatch, pixel, sw, sh, alpha);

            //顶部标题栏（最后绘制保证不被遮挡）
            DrawTopHeader(spriteBatch, pixel, sw, sh, alpha);

            //底部状态栏
            DrawBottomBar(spriteBatch, pixel, sw, sh, alpha);

            //左侧系统状态面板
            DrawLeftPanel(spriteBatch, pixel, sw, sh, alpha);

            //右侧通信日志面板
            DrawRightPanel(spriteBatch, pixel, sw, sh, alpha);

            //中心徽章
            DrawCenterEmblem(spriteBatch, pixel, sw, sh, alpha);

            //粒子
            DrawParticles(spriteBatch, pixel, alpha);

            //进度条区域（综合布局）
            DrawProgressArea(spriteBatch, pixel, sw, sh, alpha);

            //加载状态文字
            DrawLoadingText(spriteBatch, sw, sh, alpha);

            //底部提示文字（位于底栏内）
            DrawHintText(spriteBatch, sw, sh, alpha);

            //全息叠加与故障条
            DrawHologramOverlay(spriteBatch, pixel, sw, sh, alpha);

            //全屏L形角括号（最后叠加，保持清晰）
            DrawCornerBrackets(spriteBatch, pixel, sw, sh, alpha);

            //全屏边框线
            DrawFrameBorders(spriteBatch, pixel, sw, sh, alpha);
        }

        private void DrawSolidBackground(SpriteBatch sb, Texture2D pixel, int sw, int sh, float alpha) {
            //最底层纯色填充，保证背景完全不透明（外层淡入淡出由fadeAlpha控制）
            sb.Draw(pixel, new Rectangle(0, 0, sw, sh), new Rectangle(0, 0, 1, 1), new Color(3, 5, 12) * alpha);

            //竖向分区微弱脉动渐变叠加（制造空间深度感）
            int segs = 6;
            for (int i = 0; i < segs; i++) {
                float t = i / (float)segs;
                int y1 = (int)(t * sh);
                int y2 = (int)((i + 1f) / segs * sh);
                float pulse = MathF.Sin(circuitPulseTimer * 0.4f + t * 1.8f) * 0.5f + 0.5f;
                sb.Draw(pixel, new Rectangle(0, y1, sw, Math.Max(1, y2 - y1)), new Rectangle(0, 0, 1, 1),
                    new Color(6, 12, 28) * (alpha * 0.2f * pulse));
            }

            //CRT横向细条纹（固定间距，强化科技质感）
            for (int y = 0; y < sh; y += 3) {
                sb.Draw(pixel, new Rectangle(0, y, sw, 1), new Rectangle(0, 0, 1, 1), Color.Black * (alpha * 0.055f));
            }

            //偶发故障横条（通过glitchTimer驱动）
            float g = MathF.Sin(glitchTimer * 2.7f);
            if (g > 0.93f) {
                float gy = (MathF.Sin(glitchTimer * 141f) * 0.5f + 0.5f) * sh;
                int gh = (int)((g - 0.93f) / 0.07f * 5f) + 1;
                sb.Draw(pixel, new Rectangle(0, (int)gy, sw, gh), new Rectangle(0, 0, 1, 1),
                    new Color(40, 160, 255) * (alpha * 0.07f));
            }
        }

        private void DrawBackgroundDotGrid(SpriteBatch sb, Texture2D pixel, int sw, int sh, float alpha) {
            //极低亮度的静态六边形点阵，提供背景纹理感
            float cellW = 28f, cellH = 24f;
            int cols = (int)(sw / cellW) + 2;
            int rows = (int)(sh / cellH) + 2;
            Color dotColor = new Color(18, 50, 105) * (alpha * 0.16f);
            for (int row = 0; row < rows; row++) {
                for (int col = 0; col < cols; col++) {
                    float ox = row % 2 == 0 ? 0f : cellW * 0.5f;
                    float px = col * cellW + ox;
                    float py = row * cellH;
                    sb.Draw(pixel, new Rectangle((int)px, (int)py, 1, 1), new Rectangle(0, 0, 1, 1), dotColor);
                }
            }
        }

        private void DrawHexCells(SpriteBatch sb, Texture2D pixel, float alpha) {
            foreach (var hex in hexCells) {
                float lifeRatio = hex.Life / hex.MaxLife;
                float fadeIn = Math.Min(lifeRatio * 4f, 1f);
                float fadeOut = Math.Min((1f - lifeRatio) * 3f, 1f);
                float cellAlpha = fadeIn * fadeOut * alpha * 0.25f;

                float pulse = MathF.Sin(hex.PulsePhase + globalTime * 2f) * 0.5f + 0.5f;
                Color c = new Color(30, 100, 180) * (cellAlpha * (0.3f + pulse * 0.7f));

                //绘制六边形轮廓（用6条线段近似）
                int sides = 6;
                for (int i = 0; i < sides; i++) {
                    float a1 = hex.Rotation + MathHelper.TwoPi * i / sides;
                    float a2 = hex.Rotation + MathHelper.TwoPi * (i + 1) / sides;
                    Vector2 p1 = hex.Position + a1.ToRotationVector2() * hex.Size;
                    Vector2 p2 = hex.Position + a2.ToRotationVector2() * hex.Size;
                    DrawLine(sb, pixel, p1, p2, c, 1f);
                }
            }
        }

        private void DrawCircuitNodes(SpriteBatch sb, Texture2D pixel, float alpha) {
            foreach (var node in circuitNodes) {
                float lifeRatio = node.Life / node.MaxLife;
                float fadeIn = Math.Min(lifeRatio * 3f, 1f);
                float fadeOut = Math.Min((1f - lifeRatio) * 3f, 1f);
                float nodeAlpha = fadeIn * fadeOut * alpha;

                float pulse = MathF.Sin(node.PulsePhase + globalTime * 3f) * 0.5f + 0.5f;
                Color lineColor = new Color(50, 160, 240) * (nodeAlpha * 0.35f * (0.4f + pulse * 0.6f));
                Color nodeColor = new Color(80, 220, 255) * (nodeAlpha * 0.6f);

                //中心节点
                sb.Draw(pixel, node.Center, new Rectangle(0, 0, 1, 1), nodeColor,
                    0f, new Vector2(0.5f), new Vector2(4f), SpriteEffects.None, 0f);

                //分支线条（带脉冲点）
                foreach (var branch in node.Branches) {
                    DrawLine(sb, pixel, node.Center, branch, lineColor, 1.5f);

                    //脉冲光点沿分支移动
                    float pulsePos = (globalTime * 0.8f + node.PulsePhase) % 1f;
                    Vector2 pulsePoint = Vector2.Lerp(node.Center, branch, pulsePos);
                    sb.Draw(pixel, pulsePoint, new Rectangle(0, 0, 1, 1),
                        new Color(100, 220, 255) * (nodeAlpha * 0.8f * pulse),
                        0f, new Vector2(0.5f), new Vector2(3f), SpriteEffects.None, 0f);

                    //分支末端节点
                    sb.Draw(pixel, branch, new Rectangle(0, 0, 1, 1),
                        nodeColor * 0.5f, 0f, new Vector2(0.5f), new Vector2(2.5f), SpriteEffects.None, 0f);
                }
            }
        }

        private void DrawDataStreams(SpriteBatch sb, Texture2D pixel, float alpha) {
            foreach (var stream in dataStreams) {
                float lifeRatio = stream.Life / stream.MaxLife;
                float streamAlpha = lifeRatio * alpha;

                //流动的发光线段
                float headPos = stream.Progress;
                float tailPos = Math.Max(0, headPos - 0.15f);

                Vector2 dir = stream.End - stream.Start;
                Vector2 headPoint = stream.Start + dir * headPos;
                Vector2 tailPoint = stream.Start + dir * tailPos;

                Color headColor = stream.LineColor * streamAlpha;
                Color tailColor = stream.LineColor * (streamAlpha * 0.1f);

                //主线（暗底）
                DrawLine(sb, pixel, stream.Start, stream.End, stream.LineColor * (streamAlpha * 0.08f), stream.Thickness * 0.5f);

                //亮头
                DrawLine(sb, pixel, tailPoint, headPoint, headColor, stream.Thickness);

                //发光头部
                sb.Draw(pixel, headPoint, new Rectangle(0, 0, 1, 1),
                    new Color(100, 220, 255, 0) * streamAlpha,
                    0f, new Vector2(0.5f), new Vector2(stream.Thickness * 3f), SpriteEffects.None, 0f);
            }
        }

        private void DrawScanLine(SpriteBatch sb, Texture2D pixel, int sw, int sh, float alpha) {
            //主扫描线从上到下滚动，带向上拖影
            float scanY = sh * 0.5f + MathF.Sin(scanLineTimer) * sh * 0.46f;
            for (int i = -4; i <= 4; i++) {
                float y = scanY + i * 2.5f;
                if (y < 0 || y > sh) continue;
                float intensity = 1f - Math.Abs(i) / 5f;
                sb.Draw(pixel, new Rectangle(0, (int)y, sw, 2), new Rectangle(0, 0, 1, 1),
                    new Color(50, 160, 240) * (alpha * 0.055f * intensity));
            }

            //次级扫描线（方向相反，更暗）
            float scanY2 = sh * (scanLineTimer * 0.27f % 1f);
            for (int i = -2; i <= 2; i++) {
                float y = scanY2 + i * 2f;
                if (y < 0 || y > sh) continue;
                float intensity = 1f - Math.Abs(i) / 3f;
                sb.Draw(pixel, new Rectangle(0, (int)y, sw, 1), new Rectangle(0, 0, 1, 1),
                    new Color(30, 100, 180) * (alpha * 0.028f * intensity));
            }
        }

        private void DrawCenterEmblem(SpriteBatch sb, Texture2D pixel, int sw, int sh, float alpha) {
            Vector2 center = new(sw * 0.5f, sh * 0.42f);
            float baseAlpha = alpha * 0.4f;
            float pulse = MathF.Sin(circuitPulseTimer * 1.5f) * 0.5f + 0.5f;

            //外环（大齿轮样式）
            float outerRadius = 110f;
            int outerSegments = 64;
            int teeth = 16;
            Color outerColor = new Color(40, 120, 200) * (baseAlpha * (0.3f + pulse * 0.3f));
            for (int i = 0; i < outerSegments; i++) {
                float a1 = outerRingRotation + MathHelper.TwoPi * i / outerSegments;
                float a2 = outerRingRotation + MathHelper.TwoPi * (i + 1) / outerSegments;

                //齿轮齿的凸起
                float toothPhase = (float)i / outerSegments * teeth;
                float toothOffset = MathF.Sin(toothPhase * MathHelper.TwoPi) > 0.5f ? 8f : 0f;

                Vector2 p1 = center + a1.ToRotationVector2() * (outerRadius + toothOffset);
                Vector2 p2 = center + a2.ToRotationVector2() * (outerRadius + toothOffset);
                DrawLine(sb, pixel, p1, p2, outerColor, 2f);
            }

            //中环（虚线旋转）
            float midRadius = 80f;
            int midSegments = 48;
            Color midColor = new Color(60, 160, 240) * (baseAlpha * (0.4f + pulse * 0.2f));
            for (int i = 0; i < midSegments; i++) {
                if (i % 3 == 0) continue; //跳过产生虚线效果
                float a1 = innerRingRotation + MathHelper.TwoPi * i / midSegments;
                float a2 = innerRingRotation + MathHelper.TwoPi * (i + 1) / midSegments;
                Vector2 p1 = center + a1.ToRotationVector2() * midRadius;
                Vector2 p2 = center + a2.ToRotationVector2() * midRadius;
                DrawLine(sb, pixel, p1, p2, midColor, 1.5f);
            }

            //内环（实线）
            float innerRadius = 50f;
            int innerSegments = 40;
            Color innerColor = new Color(80, 200, 255) * (baseAlpha * (0.5f + pulse * 0.3f));
            for (int i = 0; i < innerSegments; i++) {
                float a1 = -outerRingRotation * 0.5f + MathHelper.TwoPi * i / innerSegments;
                float a2 = -outerRingRotation * 0.5f + MathHelper.TwoPi * (i + 1) / innerSegments;
                Vector2 p1 = center + a1.ToRotationVector2() * innerRadius;
                Vector2 p2 = center + a2.ToRotationVector2() * innerRadius;
                DrawLine(sb, pixel, p1, p2, innerColor, 1.5f);
            }

            //中心十字准星
            float crossSize = 20f;
            Color crossColor = new Color(100, 220, 255) * (baseAlpha * 0.7f);
            DrawLine(sb, pixel, center + new Vector2(-crossSize, 0), center + new Vector2(-6, 0), crossColor, 2f);
            DrawLine(sb, pixel, center + new Vector2(6, 0), center + new Vector2(crossSize, 0), crossColor, 2f);
            DrawLine(sb, pixel, center + new Vector2(0, -crossSize), center + new Vector2(0, -6), crossColor, 2f);
            DrawLine(sb, pixel, center + new Vector2(0, 6), center + new Vector2(0, crossSize), crossColor, 2f);

            //中心发光点
            if (CWRAsset.SoftGlow?.IsLoaded ?? false) {
                Texture2D glow = CWRAsset.SoftGlow.Value;
                float glowScale = 0.8f + pulse * 0.3f;
                Color glowColor = new Color(80, 200, 255, 0) * (baseAlpha * 0.6f);
                sb.Draw(glow, center, null, glowColor, 0f, glow.Size() / 2f, glowScale, SpriteEffects.None, 0f);
            }

            //四方向刻度标记
            for (int i = 0; i < 8; i++) {
                float angle = aquilaRotation + MathHelper.TwoPi * i / 8f;
                Vector2 inner2 = center + angle.ToRotationVector2() * (outerRadius + 12f);
                Vector2 outer2 = center + angle.ToRotationVector2() * (outerRadius + 22f);
                float tickAlpha = i % 2 == 0 ? 0.6f : 0.3f;
                float tickWidth = i % 2 == 0 ? 2f : 1f;
                DrawLine(sb, pixel, inner2, outer2, new Color(60, 160, 240) * (baseAlpha * tickAlpha), tickWidth);
            }

            //旋转数据标记环
            for (int i = 0; i < 12; i++) {
                float angle = outerRingRotation * 2f + MathHelper.TwoPi * i / 12f;
                Vector2 markPos = center + angle.ToRotationVector2() * (outerRadius + 30f);
                float markPulse = MathF.Sin(globalTime * 3f + i * 0.5f) * 0.5f + 0.5f;
                Color markColor = new Color(50, 150, 230) * (baseAlpha * 0.3f * markPulse);
                sb.Draw(pixel, markPos, new Rectangle(0, 0, 1, 1), markColor,
                    angle, new Vector2(0.5f), new Vector2(6f, 2f), SpriteEffects.None, 0f);
            }
        }

        private void DrawParticles(SpriteBatch sb, Texture2D pixel, float alpha) {
            foreach (var p in particles) {
                float lifeRatio = p.Life / p.MaxLife;
                float fadeIn = Math.Min(lifeRatio * 3f, 1f);
                float fadeOut = Math.Min((1f - lifeRatio) * 3f, 1f);
                float pAlpha = fadeIn * fadeOut * alpha;
                float size = p.Size * (0.6f + lifeRatio * 0.4f);

                Color color = p.BaseColor * pAlpha;
                sb.Draw(pixel, p.Position, new Rectangle(0, 0, 1, 1), color, p.Rotation,
                    new Vector2(0.5f), new Vector2(size), SpriteEffects.None, 0f);

                //辉光层
                sb.Draw(pixel, p.Position, new Rectangle(0, 0, 1, 1), color * 0.3f, p.Rotation,
                    new Vector2(0.5f), new Vector2(size * 2.5f), SpriteEffects.None, 0f);
            }
        }

        private void DrawProgressArea(SpriteBatch sb, Texture2D pixel, int sw, int sh, float alpha) {
            float barY = sh * ProgressBarY;
            float barX = (sw - ProgressBarWidth) * 0.5f;

            //进度区域外框背景（略大于进度条，增加视觉分量）
            Rectangle areaRect = new((int)(barX - 16), (int)(barY - 22), (int)(ProgressBarWidth + 32), (int)(ProgressBarHeight + 44));
            sb.Draw(pixel, areaRect, new Rectangle(0, 0, 1, 1), new Color(3, 7, 16) * (alpha * 0.72f));
            DrawBracketBorder(sb, pixel, areaRect, alpha * 0.55f);

            //进度条外框线
            Rectangle outerRect = new((int)(barX - 3), (int)(barY - 3), (int)(ProgressBarWidth + 6), (int)(ProgressBarHeight + 6));
            DrawRectBorder(sb, pixel, outerRect, new Color(30, 95, 195) * (alpha * 0.52f), 1);

            //轨道背景
            Rectangle trackRect = new((int)barX, (int)barY, (int)ProgressBarWidth, (int)ProgressBarHeight);
            sb.Draw(pixel, trackRect, new Rectangle(0, 0, 1, 1), new Color(7, 16, 33) * (alpha * 0.96f));

            //进度填充（逐像素渐变+流光）
            int fillWidth = (int)(ProgressBarWidth * currentProgress);
            if (fillWidth > 0) {
                for (int i = 0; i < fillWidth; i++) {
                    float t = i / ProgressBarWidth;
                    Color fillColor = Color.Lerp(new Color(20, 90, 190), new Color(65, 205, 255), t);
                    float shimmer = MathF.Sin(progressGlowPhase - t * 8f) * 0.5f + 0.5f;
                    fillColor = Color.Lerp(fillColor, new Color(135, 232, 255), shimmer * 0.26f);
                    sb.Draw(pixel, new Rectangle((int)barX + i, (int)barY, 1, (int)ProgressBarHeight),
                        new Rectangle(0, 0, 1, 1), fillColor * (alpha * 0.92f));
                }

                //进度头部发光点
                if (CWRAsset.SoftGlow?.IsLoaded ?? false) {
                    Texture2D glow = CWRAsset.SoftGlow.Value;
                    float glowPulse = MathF.Sin(progressGlowPhase * 2f) * 0.5f + 0.5f;
                    sb.Draw(glow, new Vector2(barX + fillWidth, barY + ProgressBarHeight * 0.5f), null,
                        new Color(75, 205, 255, 0) * (alpha * (0.32f + glowPulse * 0.32f)),
                        0f, glow.Size() / 2f, 0.21f, SpriteEffects.None, 0f);
                }
            }

            //刻度标记（10个）
            for (int i = 1; i < 10; i++) {
                float markX = barX + ProgressBarWidth * i / 10f;
                float markH = i == 5 ? 6f : 3.5f;
                sb.Draw(pixel, new Rectangle((int)markX, (int)(barY + ProgressBarHeight + 2), 1, (int)markH),
                    new Rectangle(0, 0, 1, 1), new Color(38, 105, 185) * (alpha * 0.38f));
            }

            //百分比文字
            string percentText = $"{(int)(currentProgress * 100)}%";
            Vector2 percentSize = FontAssets.MouseText.Value.MeasureString(percentText) * 0.9f;
            float colorPulse = MathF.Sin(globalTime * 2f) * 0.5f + 0.5f;
            Color percentColor = Color.Lerp(new Color(95, 172, 222), new Color(138, 232, 255), colorPulse);
            Utils.DrawBorderString(sb, percentText,
                new Vector2(barX + ProgressBarWidth + 14f, barY + ProgressBarHeight * 0.5f - percentSize.Y * 0.5f),
                percentColor * alpha, 0.9f);
        }

        private void DrawLoadingText(SpriteBatch sb, int sw, int sh, float alpha) {
            float barY = sh * ProgressBarY;

            //加载状态文字（进度条上方）
            string statusText = LoadingText.Value;
            //动态省略号
            int dots = (int)(globalTime * 4f) % 4;
            statusText += new string('.', dots);

            Vector2 statusSize = FontAssets.MouseText.Value.MeasureString(statusText) * 1f;
            Vector2 statusPos = new((sw - statusSize.X) * 0.5f, barY - 38f);

            //文字光晕
            Color glowColor = new Color(60, 180, 255) * (alpha * 0.2f);
            for (int i = 0; i < 4; i++) {
                float angle = MathHelper.TwoPi * i / 4f + globalTime;
                Vector2 off = angle.ToRotationVector2() * 2f;
                Utils.DrawBorderString(sb, statusText, statusPos + off, glowColor, 1f);
            }

            Color textColor = new Color(180, 220, 255) * alpha;
            Utils.DrawBorderString(sb, statusText, statusPos, textColor, 1f);

            //附加初始化文字
            if (currentProgress < 0.5f) {
                string initText = InitializingText.Value;
                int initDots = (int)(globalTime * 3f) % 4;
                initText += new string('.', initDots);
                Vector2 initSize = FontAssets.MouseText.Value.MeasureString(initText) * 0.8f;
                Vector2 initPos = new((sw - initSize.X) * 0.5f, barY + 24f);
                Color initColor = new Color(80, 160, 220) * (alpha * 0.5f * (1f - currentProgress * 2f));
                Utils.DrawBorderString(sb, initText, initPos, initColor, 0.8f);
            }
        }

        private void DrawHintText(SpriteBatch sb, int sw, int sh, float alpha) {
            if (HintTexts == null || HintTexts.Length == 0) return;

            string hint = HintTexts[currentHintIndex].Value;
            float hintAlpha = hintFadeProgress * alpha;
            string displayText = $"「{hint}」";

            Vector2 textSize = FontAssets.MouseText.Value.MeasureString(displayText) * 0.88f;
            //位于底部状态栏内垂直居中
            float textY = sh - FooterBarH * 0.5f - textSize.Y * 0.5f;
            Vector2 textPos = new((sw - textSize.X) * 0.5f, textY);

            //光晕底层
            Color glowColor = new Color(28, 95, 155) * (hintAlpha * 0.11f);
            for (int i = 0; i < 4; i++) {
                Vector2 off = (MathHelper.TwoPi * i / 4f).ToRotationVector2() * 1.5f;
                Utils.DrawBorderString(sb, displayText, textPos + off, glowColor, 0.88f);
            }

            //主文字（轻微闪烁）
            float textFlicker = 0.89f + MathF.Sin(globalTime * 5f) * 0.11f;
            Utils.DrawBorderString(sb, displayText, textPos,
                new Color(130, 175, 205) * (hintAlpha * textFlicker), 0.88f);
        }

        private void DrawHologramOverlay(SpriteBatch sb, Texture2D pixel, int sw, int sh, float alpha) {
            //全局全息闪烁叠加（偶发亮闪）
            float flicker = MathF.Sin(hologramFlicker * 1.5f) * 0.5f + 0.5f;
            if (flicker > 0.88f) {
                sb.Draw(pixel, new Rectangle(0, 0, sw, sh), new Rectangle(0, 0, 1, 1),
                    new Color(12, 45, 85) * (alpha * 0.025f * (flicker - 0.88f) * 8.33f));
            }

            //偶发宽幅故障横条（通过glitchTimer驱动，比背景故障条更宽更亮）
            if (MathF.Sin(glitchTimer * 4.1f) > 0.94f) {
                int gy = (int)((MathF.Sin(glitchTimer * 17f) * 0.5f + 0.5f) * sh);
                int gh = Main.rand?.Next(1, 6) ?? 2;
                sb.Draw(pixel, new Rectangle(0, gy, sw, gh), new Rectangle(0, 0, 1, 1),
                    new Color(55, 175, 255) * (alpha * 0.08f));
            }
        }

        private void DrawCornerBrackets(SpriteBatch sb, Texture2D pixel, int sw, int sh, float alpha) {
            float pulse = MathF.Sin(circuitPulseTimer * 1.6f) * 0.35f + 0.65f;
            Color cornerBright = new Color(50, 165, 240) * (alpha * pulse);
            Color cornerDim = new Color(30, 100, 178) * (alpha * pulse * 0.5f);
            float len = 58f;
            float thick = 2f;

            //左上（亮）
            DrawLine(sb, pixel, new Vector2(12, 12), new Vector2(12 + len, 12), cornerBright, thick);
            DrawLine(sb, pixel, new Vector2(12, 12), new Vector2(12, 12 + len), cornerBright, thick);
            sb.Draw(pixel, new Vector2(12, 12), new Rectangle(0, 0, 1, 1), cornerBright * 0.85f,
                MathHelper.PiOver4, new Vector2(0.5f), new Vector2(5f), SpriteEffects.None, 0f);

            //右上（亮）
            DrawLine(sb, pixel, new Vector2(sw - 12, 12), new Vector2(sw - 12 - len, 12), cornerBright, thick);
            DrawLine(sb, pixel, new Vector2(sw - 12, 12), new Vector2(sw - 12, 12 + len), cornerBright, thick);
            sb.Draw(pixel, new Vector2(sw - 12, 12), new Rectangle(0, 0, 1, 1), cornerBright * 0.85f,
                MathHelper.PiOver4, new Vector2(0.5f), new Vector2(5f), SpriteEffects.None, 0f);

            //左下（暗）
            DrawLine(sb, pixel, new Vector2(12, sh - 12), new Vector2(12 + len, sh - 12), cornerDim, thick);
            DrawLine(sb, pixel, new Vector2(12, sh - 12), new Vector2(12, sh - 12 - len), cornerDim, thick);

            //右下（暗）
            DrawLine(sb, pixel, new Vector2(sw - 12, sh - 12), new Vector2(sw - 12 - len, sh - 12), cornerDim, thick);
            DrawLine(sb, pixel, new Vector2(sw - 12, sh - 12), new Vector2(sw - 12, sh - 12 - len), cornerDim, thick);

            //角落延伸次级刻度线（L末端之后的短线段）
            float extLen = 22f;
            Color extColor = new Color(38, 136, 218) * (alpha * pulse * 0.32f);
            DrawLine(sb, pixel, new Vector2(12 + len + 4, 12), new Vector2(12 + len + 4 + extLen, 12), extColor, 1f);
            DrawLine(sb, pixel, new Vector2(12, 12 + len + 4), new Vector2(12, 12 + len + 4 + extLen), extColor, 1f);
            DrawLine(sb, pixel, new Vector2(sw - 12 - len - 4, 12), new Vector2(sw - 12 - len - 4 - extLen, 12), extColor, 1f);
            DrawLine(sb, pixel, new Vector2(sw - 12, 12 + len + 4), new Vector2(sw - 12, 12 + len + 4 + extLen), extColor, 1f);
        }

        private void DrawFrameBorders(SpriteBatch sb, Texture2D pixel, int sw, int sh, float alpha) {
            float pulse = MathF.Sin(circuitPulseTimer * 0.9f) * 0.5f + 0.5f;
            Color borderColor = new Color(28, 95, 175) * (alpha * (0.26f + pulse * 0.16f));

            //顶部双边框
            sb.Draw(pixel, new Rectangle(0, 0, sw, 2), new Rectangle(0, 0, 1, 1), borderColor);
            sb.Draw(pixel, new Rectangle(0, sh - 2, sw, 2), new Rectangle(0, 0, 1, 1), borderColor * 0.6f);

            //内缩次级线
            sb.Draw(pixel, new Rectangle(60, 5, sw - 120, 1), new Rectangle(0, 0, 1, 1), borderColor * 0.3f);
            sb.Draw(pixel, new Rectangle(60, sh - 6, sw - 120, 1), new Rectangle(0, 0, 1, 1), borderColor * 0.25f);

            //左右侧边（顶栏到底栏之间）
            int sideYS = (int)HeaderBarH + 4;
            int sideYE = sh - (int)FooterBarH - 4;
            sb.Draw(pixel, new Rectangle(0, sideYS, 1, sideYE - sideYS), new Rectangle(0, 0, 1, 1), borderColor * 0.28f);
            sb.Draw(pixel, new Rectangle(sw - 1, sideYS, 1, sideYE - sideYS), new Rectangle(0, 0, 1, 1), borderColor * 0.28f);
        }

        private void DrawTopHeader(SpriteBatch sb, Texture2D pixel, int sw, int sh, float alpha) {
            int bh = (int)HeaderBarH;

            //标题栏背景（略比底色更亮，形成层次）
            sb.Draw(pixel, new Rectangle(0, 0, sw, bh), new Rectangle(0, 0, 1, 1), new Color(5, 10, 22) * (alpha * 0.97f));

            //底部分割双线
            float lineAlpha = alpha * (0.52f + MathF.Sin(circuitPulseTimer) * 0.14f);
            sb.Draw(pixel, new Rectangle(0, bh - 2, sw, 2), new Rectangle(0, 0, 1, 1),
                new Color(0, 155, 235) * lineAlpha);
            sb.Draw(pixel, new Rectangle(0, bh, sw, 1), new Rectangle(0, 0, 1, 1),
                new Color(0, 55, 115) * (alpha * 0.42f));

            //左侧L形角括号
            DrawHorStrip(sb, pixel, 8, 8, 18, 2, new Color(0, 198, 255) * (alpha * 0.88f));
            DrawVerStrip(sb, pixel, 8, 8, 18, 2, new Color(0, 198, 255) * (alpha * 0.88f));

            //标题文字居中
            string title = "DRAEDON SYSTEMS  ·  ACHERON PROTOCOL";
            Vector2 titleSize = FontAssets.MouseText.Value.MeasureString(title) * 0.87f;
            float titleX = sw * 0.5f - titleSize.X * 0.5f;
            float titleY = bh * 0.5f - titleSize.Y * 0.5f;
            float flicker = 0.92f + MathF.Sin(hologramFlicker * 1.8f) * 0.08f;

            //文字光晕
            Color titleGlow = new Color(0, 95, 195) * (alpha * 0.18f);
            for (int i = 0; i < 3; i++) {
                Vector2 off = (MathHelper.TwoPi * i / 3f + globalTime * 0.55f).ToRotationVector2() * 1.4f;
                Utils.DrawBorderString(sb, title, new Vector2(titleX, titleY) + off, titleGlow, 0.87f);
            }
            Utils.DrawBorderString(sb, title, new Vector2(titleX, titleY),
                new Color(158, 228, 255) * (alpha * flicker), 0.87f);

            //右侧状态指示器
            DrawHeaderStatus(sb, pixel, sw, bh, alpha);
        }

        private void DrawHeaderStatus(SpriteBatch sb, Texture2D pixel, int sw, int bh, float alpha) {
            float blink = MathF.Sin(warningPulse * 2.5f) * 0.45f + 0.55f;
            float blink2 = MathF.Sin(warningPulse * 2.5f + MathHelper.PiOver2) * 0.45f + 0.55f;

            //ONLINE 指示点+标签
            int dotR = 3;
            Color onlineColor = new Color(38, 242, 135);
            sb.Draw(pixel, new Rectangle(sw - 192 - dotR, (int)(bh * 0.5f) - dotR, dotR * 2, dotR * 2),
                new Rectangle(0, 0, 1, 1), onlineColor * (alpha * blink));
            Utils.DrawBorderString(sb, "ONLINE",
                new Vector2(sw - 184f, bh * 0.5f - 7f),
                new Color(38, 218, 128) * (alpha * 0.7f), 0.65f);

            //竖向分隔线
            sb.Draw(pixel, new Rectangle(sw - 118, 10, 1, bh - 20), new Rectangle(0, 0, 1, 1),
                new Color(0, 75, 138) * (alpha * 0.42f));

            //SYNC 指示点+标签
            Color syncColor = new Color(0, 198, 255);
            sb.Draw(pixel, new Rectangle(sw - 102 - dotR, (int)(bh * 0.5f) - dotR, dotR * 2, dotR * 2),
                new Rectangle(0, 0, 1, 1), syncColor * (alpha * blink2));
            Utils.DrawBorderString(sb, "SYNC",
                new Vector2(sw - 90f, bh * 0.5f - 7f),
                new Color(0, 178, 242) * (alpha * 0.7f), 0.65f);

            //右侧L括号
            DrawHorStrip(sb, pixel, sw - 8 - 18, 8, 18, 2, new Color(0, 158, 218) * (alpha * 0.68f));
            DrawVerStrip(sb, pixel, sw - 10, 8, 18, 2, new Color(0, 158, 218) * (alpha * 0.68f));
        }

        private void DrawBottomBar(SpriteBatch sb, Texture2D pixel, int sw, int sh, float alpha) {
            int bh = (int)FooterBarH;
            int by = sh - bh;

            //底部栏背景
            sb.Draw(pixel, new Rectangle(0, by, sw, bh), new Rectangle(0, 0, 1, 1), new Color(4, 9, 20) * (alpha * 0.97f));

            //顶部分割双线
            float lineAlpha = alpha * (0.48f + MathF.Sin(circuitPulseTimer + MathHelper.Pi) * 0.11f);
            sb.Draw(pixel, new Rectangle(0, by, sw, 2), new Rectangle(0, 0, 1, 1),
                new Color(0, 138, 218) * lineAlpha);
            sb.Draw(pixel, new Rectangle(0, by - 1, sw, 1), new Rectangle(0, 0, 1, 1),
                new Color(0, 48, 108) * (alpha * 0.38f));
        }

        private void DrawLeftPanel(SpriteBatch sb, Texture2D pixel, int sw, int sh, float alpha) {
            int panelW = (int)(sw * SidePanelRatio);
            int panelX = 14;
            int panelY = (int)HeaderBarH + 10;
            int panelH = sh - (int)HeaderBarH - (int)FooterBarH - 20;

            //面板背景
            sb.Draw(pixel, new Rectangle(panelX, panelY, panelW, panelH), new Rectangle(0, 0, 1, 1),
                new Color(3, 8, 18) * (alpha * 0.87f));
            DrawBracketBorder(sb, pixel, new Rectangle(panelX, panelY, panelW, panelH), alpha);

            //标题
            Utils.DrawBorderString(sb, "SYS STATUS",
                new Vector2(panelX + 12f, panelY + 10f),
                new Color(0, 218, 255) * (alpha * 0.83f), 0.75f);

            //流动分割线
            DrawFlowLine(sb, pixel, panelX + 8, panelY + 30, panelW - 16, alpha * 0.7f);

            //系统状态条目
            DrawSystemStatusItems(sb, pixel, panelX, panelY, panelW, panelH, alpha);

            //底部信号波形监视器（占据面板底部区域）
            int waveY = panelY + panelH - 60;
            DrawSignalWave(sb, pixel, panelX + 12, waveY, panelW - 24, 44, alpha * 0.82f);
        }

        private void DrawSystemStatusItems(SpriteBatch sb, Texture2D pixel,
            int panelX, int panelY, int panelW, int panelH, float alpha) {
            int startY = panelY + 38;
            int itemH = 26;
            int barX = panelX + panelW - 72;
            int barW = 54;
            int barH = 6;

            for (int i = 0; i < StatusLabels.Length; i++) {
                int iy = startY + i * itemH;
                float itemPhase = globalTime * 2.5f + i * 1.1f;

                //根据状态类型确定颜色和标签文字
                Color statusColor;
                string statusTag;
                switch (StatusTypes[i]) {
                    case 0:
                        statusColor = new Color(38, 238, 128);
                        statusTag = "ONLINE";
                        break;
                    case 1:
                        statusColor = new Color(0, 198, 255);
                        statusTag = "ACTIVE";
                        break;
                    case 2:
                        float loadPulse = MathF.Sin(itemPhase) * 0.38f + 0.62f;
                        statusColor = new Color(175, 218, 255) * loadPulse;
                        statusTag = "LOADING";
                        break;
                    default:
                        statusColor = new Color(38, 78, 128);
                        statusTag = "STANDBY";
                        break;
                }

                //左侧状态圆点
                int dotR = 3;
                sb.Draw(pixel, new Rectangle(panelX + 10, iy + 5, dotR * 2, dotR * 2),
                    new Rectangle(0, 0, 1, 1), statusColor * (alpha * 0.88f));

                //项目标签
                Utils.DrawBorderString(sb, StatusLabels[i],
                    new Vector2(panelX + 20f, iy),
                    new Color(95, 155, 198) * (alpha * 0.76f), 0.64f);

                //进度条背景
                sb.Draw(pixel, new Rectangle(barX, iy + 3, barW, barH), new Rectangle(0, 0, 1, 1),
                    new Color(7, 16, 32) * (alpha * 0.92f));

                //进度条填充（各项目依次完成）
                float itemProgress = Math.Clamp(currentProgress * 1.25f - i * 0.12f, 0f, 1f);
                int fillW = (int)(barW * itemProgress);
                if (fillW > 0) {
                    sb.Draw(pixel, new Rectangle(barX, iy + 3, fillW, barH), new Rectangle(0, 0, 1, 1),
                        statusColor * (alpha * 0.72f));
                }

                //完成后显示状态标签
                if (itemProgress >= 0.95f) {
                    Utils.DrawBorderString(sb, statusTag,
                        new Vector2(barX + barW + 3f, iy - 1f),
                        statusColor * (alpha * 0.62f), 0.53f);
                }
            }
        }

        private void DrawSignalWave(SpriteBatch sb, Texture2D pixel, int x, int y, int totalW, int totalH, float alpha) {
            if (alpha < 0.01f) return;

            int n = eqBars.Length;
            //波形垂直中心线（0振幅基线位置）
            float baselineY = y + totalH * 0.62f;
            float ampRange = totalH * 0.36f;

            //区域背景
            sb.Draw(pixel, new Rectangle(x, y, totalW, totalH), new Rectangle(0, 0, 1, 1),
                new Color(2, 6, 15) * (alpha * 0.78f));

            //标签
            Utils.DrawBorderString(sb, "WAVEFORM",
                new Vector2(x + 2f, y + 1f),
                new Color(0, 155, 218) * (alpha * 0.52f), 0.46f);

            //水平基线
            sb.Draw(pixel, new Rectangle(x, (int)baselineY, totalW, 1), new Rectangle(0, 0, 1, 1),
                new Color(0, 78, 145) * (alpha * 0.55f));

            //计算各采样点的屏幕坐标
            //使用eqBars作为振幅数据，以正弦噪波叠加让静止时也有动感
            float[] ys = new float[n];
            for (int i = 0; i < n; i++) {
                //eqBars在[-1,1]范围内居中映射，偏移量用正弦底波叠加
                float amp = (eqBars[i] - 0.5f) * 2f;
                float noise = MathF.Sin(globalTime * 3.8f + i * 0.72f) * 0.08f;
                ys[i] = baselineY - (amp + noise) * ampRange;
            }

            //逐段绘制波形折线（带拖影填充）
            float stepX = (float)totalW / (n - 1);
            for (int i = 0; i < n - 1; i++) {
                float x0 = x + i * stepX;
                float x1 = x + (i + 1) * stepX;
                float y0 = ys[i];
                float y1 = ys[i + 1];

                //振幅越大越亮越偏青绿，振幅偏低偏蓝
                float t = (eqBars[i] + eqBars[i + 1]) * 0.5f;
                Color lineColor = Color.Lerp(new Color(0, 158, 255), new Color(28, 245, 195), t);

                //主折线段
                DrawLine(sb, pixel, new Vector2(x0, y0), new Vector2(x1, y1),
                    lineColor * (alpha * 0.88f), 1.5f);

                //向基线方向的半透明填充（制造示波器屏光效果）
                int fillSteps = Math.Max(1, (int)Math.Abs(y0 - baselineY) / 2);
                for (int f = 1; f <= fillSteps; f++) {
                    float ft = f / (float)(fillSteps + 1);
                    float fy = y0 + (baselineY - y0) * ft;
                    sb.Draw(pixel, new Vector2(x0, fy), new Rectangle(0, 0, 1, 1),
                        lineColor * (alpha * 0.045f * (1f - ft)), 0f, new Vector2(0.5f),
                        new Vector2(1.5f, 1f), SpriteEffects.None, 0f);
                }

                //采样节点高亮点
                float nodePulse = MathF.Sin(globalTime * 4.2f + i * 0.55f) * 0.28f + 0.72f;
                sb.Draw(pixel, new Vector2(x0, y0), new Rectangle(0, 0, 1, 1),
                    lineColor * (alpha * 0.72f * nodePulse), 0f, new Vector2(0.5f),
                    new Vector2(2.2f), SpriteEffects.None, 0f);
            }

            //末端节点
            float lastT = eqBars[n - 1];
            Color lastColor = Color.Lerp(new Color(0, 158, 255), new Color(28, 245, 195), lastT);
            float lastNodePulse = MathF.Sin(globalTime * 4.2f + (n - 1) * 0.55f) * 0.28f + 0.72f;
            sb.Draw(pixel, new Vector2(x + totalW, ys[n - 1]), new Rectangle(0, 0, 1, 1),
                lastColor * (alpha * 0.72f * lastNodePulse), 0f, new Vector2(0.5f),
                new Vector2(2.2f), SpriteEffects.None, 0f);

            //区域边框（薄线）
            sb.Draw(pixel, new Rectangle(x, y, totalW, 1), new Rectangle(0, 0, 1, 1),
                new Color(0, 88, 155) * (alpha * 0.42f));
            sb.Draw(pixel, new Rectangle(x, y + totalH - 1, totalW, 1), new Rectangle(0, 0, 1, 1),
                new Color(0, 55, 108) * (alpha * 0.32f));
        }

        private void DrawRightPanel(SpriteBatch sb, Texture2D pixel, int sw, int sh, float alpha) {
            int panelW = (int)(sw * SidePanelRatio);
            int panelX = sw - panelW - 14;
            int panelY = (int)HeaderBarH + 10;
            int panelH = sh - (int)HeaderBarH - (int)FooterBarH - 20;

            //面板背景
            sb.Draw(pixel, new Rectangle(panelX, panelY, panelW, panelH), new Rectangle(0, 0, 1, 1),
                new Color(3, 8, 18) * (alpha * 0.87f));
            DrawBracketBorder(sb, pixel, new Rectangle(panelX, panelY, panelW, panelH), alpha);

            //标题
            Utils.DrawBorderString(sb, "COMM LOG",
                new Vector2(panelX + 12f, panelY + 10f),
                new Color(0, 218, 255) * (alpha * 0.83f), 0.75f);

            //流动分割线
            DrawFlowLine(sb, pixel, panelX + 8, panelY + 30, panelW - 16, alpha * 0.7f);

            //终端滚动日志
            DrawTerminalLog(sb, panelX, panelY + 38, panelW, panelH - 46, alpha);
        }

        private void DrawTerminalLog(SpriteBatch sb, int panelX, int startY, int panelW, int panelH, float alpha) {
            if (TerminalMessages == null || TerminalMessages.Length == 0) return;

            int logX = panelX + 12;
            float lineH = 22f;
            int maxLines = (int)(panelH / lineH);

            //根据全局时间偏移模拟滚动
            float scrollOffset = globalTime * 0.1f;
            int baseIdx = (int)scrollOffset % TerminalMessages.Length;

            for (int i = 0; i < Math.Min(maxLines, TerminalMessages.Length); i++) {
                int msgIdx = (baseIdx + i) % TerminalMessages.Length;
                float lineY = startY + i * lineH;

                //最新行高亮闪烁，旧行逐渐变暗
                bool isNewest = i == Math.Min(maxLines, TerminalMessages.Length) - 1;
                float lineAlpha = isNewest
                    ? MathF.Sin(circuitPulseTimer * 3.2f) * 0.28f + 0.72f
                    : 0.28f + (1f - (float)i / maxLines) * 0.22f;

                Color textColor = isNewest
                    ? new Color(0, 215, 255) * (alpha * lineAlpha)
                    : new Color(55, 125, 175) * (alpha * lineAlpha);

                Utils.DrawBorderString(sb, TerminalMessages[msgIdx],
                    new Vector2(logX, lineY), textColor, 0.59f);

                //行左侧指示线（最新行高亮）
                Color lineAccent = isNewest
                    ? new Color(0, 175, 255) * (alpha * lineAlpha * 0.78f)
                    : new Color(12, 48, 88) * (alpha * 0.48f);
                sb.Draw(VaultAsset.placeholder2.Value,
                    new Rectangle(panelX + 4, (int)lineY + 2, 2, (int)lineH - 6), new Rectangle(0, 0, 1, 1), lineAccent);
            }
        }

        private void DrawBracketBorder(SpriteBatch sb, Texture2D pixel, Rectangle rect, float alpha) {
            Color edge = new Color(38, 188, 255) * (alpha * 0.83f);
            Color edgeDim = edge * 0.48f;
            int bl = 20;
            int bw = 2;

            //顶部全宽薄高光线（极细，增加精致感）
            sb.Draw(pixel, new Rectangle(rect.X, rect.Y, rect.Width, 1), new Rectangle(0, 0, 1, 1), edge * 0.33f);

            //四角L括号
            DrawHorStrip(sb, pixel, rect.X, rect.Y, bl, bw, edge);
            DrawVerStrip(sb, pixel, rect.X, rect.Y, bl, bw, edge);
            DrawHorStrip(sb, pixel, rect.Right - bl, rect.Y, bl, bw, edge);
            DrawVerStrip(sb, pixel, rect.Right - bw, rect.Y, bl, bw, edge);
            DrawHorStrip(sb, pixel, rect.X, rect.Bottom - bw, bl, bw, edgeDim);
            DrawVerStrip(sb, pixel, rect.X, rect.Bottom - bl, bl, bw, edgeDim);
            DrawHorStrip(sb, pixel, rect.Right - bl, rect.Bottom - bw, bl, bw, edgeDim);
            DrawVerStrip(sb, pixel, rect.Right - bw, rect.Bottom - bl, bl, bw, edgeDim);

            //顶部角落切口刻痕
            DrawHorStrip(sb, pixel, rect.X + bl + 2, rect.Y, 4, 1, edge * 0.32f);
            DrawHorStrip(sb, pixel, rect.Right - bl - 6, rect.Y, 4, 1, edge * 0.32f);
        }

        private void DrawFlowLine(SpriteBatch sb, Texture2D pixel, int x, int y, int w, float alpha) {
            //带亮点流动效果的水平分割线
            int segs = Math.Max(1, w / 3);
            for (int i = 0; i < segs; i++) {
                float t = i / (float)segs;
                float flow = (t + circuitPulseTimer * 0.23f) % 1f;
                float bright = MathF.Sin(flow * MathHelper.Pi) * 0.52f + 0.38f;
                Color c = new Color(28, 125, 215) * (alpha * bright);
                sb.Draw(pixel, new Rectangle(x + i * 3, y, 2, 1), new Rectangle(0, 0, 1, 1), c);
            }
        }

        private static void DrawHorStrip(SpriteBatch sb, Texture2D pixel, int x, int y, int w, int h, Color c) {
            sb.Draw(pixel, new Rectangle(x, y, w, h), new Rectangle(0, 0, 1, 1), c);
        }

        private static void DrawVerStrip(SpriteBatch sb, Texture2D pixel, int x, int y, int h, int w, Color c) {
            sb.Draw(pixel, new Rectangle(x, y, w, h), new Rectangle(0, 0, 1, 1), c);
        }

        #endregion

        #region 绘制工具

        private static void DrawLine(SpriteBatch sb, Texture2D pixel, Vector2 start, Vector2 end, Color color, float thickness) {
            Vector2 diff = end - start;
            float length = diff.Length();
            if (length < 0.5f) return;
            float angle = MathF.Atan2(diff.Y, diff.X);
            sb.Draw(pixel, start, new Rectangle(0, 0, 1, 1), color, angle,
                new Vector2(0f, 0.5f), new Vector2(length, thickness), SpriteEffects.None, 0f);
        }

        private static void DrawRectBorder(SpriteBatch sb, Texture2D pixel, Rectangle rect, Color color, int thickness) {
            sb.Draw(pixel, new Rectangle(rect.X, rect.Y, rect.Width, thickness), new Rectangle(0, 0, 1, 1), color);
            sb.Draw(pixel, new Rectangle(rect.X, rect.Bottom - thickness, rect.Width, thickness), new Rectangle(0, 0, 1, 1), color);
            sb.Draw(pixel, new Rectangle(rect.X, rect.Y, thickness, rect.Height), new Rectangle(0, 0, 1, 1), color);
            sb.Draw(pixel, new Rectangle(rect.Right - thickness, rect.Y, thickness, rect.Height), new Rectangle(0, 0, 1, 1), color);
        }

        #endregion

        #region 粒子数据结构

        private struct TechParticle
        {
            public Vector2 Position;
            public Vector2 Velocity;
            public float Life;
            public float MaxLife;
            public float Size;
            public float Rotation;
            public float RotationSpeed;
            public Color BaseColor;

            public bool Update() {
                Life -= 0.016f;
                Position += Velocity;
                Velocity *= 0.995f;
                Rotation += RotationSpeed;
                return Life <= 0f;
            }
        }

        private struct DataStreamLine
        {
            public Vector2 Start;
            public Vector2 End;
            public float Life;
            public float MaxLife;
            public float Progress;
            public float Speed;
            public float Thickness;
            public Color LineColor;

            public bool Update() {
                Life -= 0.016f;
                Progress += Speed;
                if (Progress > 1.15f) Progress = -0.15f;
                return Life <= 0f;
            }
        }

        private struct CircuitNode
        {
            public Vector2 Center;
            public List<Vector2> Branches;
            public float Life;
            public float MaxLife;
            public float PulsePhase;

            public bool Update() {
                Life -= 0.016f;
                return Life <= 0f;
            }
        }

        private struct HexCell
        {
            public Vector2 Position;
            public float Size;
            public float Life;
            public float MaxLife;
            public float Rotation;
            public float PulsePhase;

            public bool Update() {
                Life -= 0.016f;
                return Life <= 0f;
            }
        }

        #endregion
    }
}
