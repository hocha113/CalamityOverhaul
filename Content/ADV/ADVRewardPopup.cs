using CalamityOverhaul.Content.ADV.UIEffect;
using InnoVault.UIHandles;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.ADV
{
    internal class ADVRewardPopup : UIHandle, ILocalizedModType
    {
        public string LocalizationCategory => "ADV";

        public static ADVRewardPopup Instance => UIHandleLoader.GetUIHandleOfType<ADVRewardPopup>();

        /// <summary>
        /// 奖励弹窗风格枚举
        /// </summary>
        public enum RewardStyle
        {
            Ocean,      // 海洋风格（默认）
            Brimstone,  // 硫磺火风格
            Draedon     // 嘉登科技风格
        }

        public class RewardEntry
        {
            public int ItemId;
            public int Stack;
            public string CustomText;
            public bool Given;
            public float Appear;
            public float Hold;
            public int AppearDuration;
            public int HoldDuration;
            public int GiveDuration;
            public bool RequireClick;
            public Func<Vector2> AnchorProvider;
            public Vector2 Offset;
            /// <summary>
            /// 风格提供器，如果为null则使用默认风格
            /// </summary>
            public Func<RewardStyle> StyleProvider;
        }

        private readonly Queue<RewardEntry> queue = new();
        private RewardEntry current;
        private int stateTimer;
        private bool givingOut = false;
        private float panelFade = 0f;
        private float panelScale = 0f;
        private bool justOpened = false;

        //海洋风格动画变量
        private float wavePhase = 0f;
        private float abyssPulse = 0f;
        private float panelPulse = 0f;
        private readonly List<Bubble> bubbles = new();
        private readonly List<Star> stars = new();
        private int bubbleTimer;
        private int starTimer;

        //硫磺火风格动画变量
        private float flameTimer = 0f;
        private float emberGlowTimer = 0f;
        private float heatWavePhase = 0f;
        private float infernoPulse = 0f;
        private readonly List<EmberPRT> embers = new();
        private int emberSpawnTimer = 0;
        private readonly List<AshPRT> ashes = new();
        private int ashSpawnTimer = 0;
        private readonly List<FlameWispPRT> flameWisps = new();
        private int wispSpawnTimer = 0;
        private const float ParticleSideMargin = 30f;

        //嘉登科技风格动画变量
        private float draedonScanLineTimer = 0f;
        private float draedonHologramFlicker = 0f;
        private float draedonCircuitPulse = 0f;
        private float draedonDataStream = 0f;
        private float draedonHexGridPhase = 0f;
        private readonly List<DraedonDataPRT> draedonDataParticles = new();
        private int draedonDataParticleTimer = 0;
        private readonly List<CircuitNodePRT> draedonCircuitNodes = new();
        private int draedonCircuitNodeTimer = 0;
        private const float DraedonParticleMargin = 30f;

        //面板区域缓存和鼠标悬停状态
        private Rectangle panelRect;
        private bool isHovering = false;
        public static int DefaultAppearDuration = 24;
        public static int DefaultHoldDuration = 50;
        public static int DefaultGiveDuration = 16;
        public static event Action<RewardEntry> OnRewardGiven;

        //位置平滑过渡相关
        private Vector2 cachedAnchorPosition;
        private Vector2 currentDisplayPosition;
        private bool isDialogueClosing = false;
        private float positionTransitionProgress = 0f;
        private const float PositionTransitionSpeed = 0.08f;

        //本地化文本
        protected static LocalizedText ClickToReceive;
        protected static LocalizedText ClickToContinue;
        protected static LocalizedText ClickOrWaitToContinue;

        public override bool Active => current != null || queue.Count > 0 || panelFade > 0.01f;

        public override void SetStaticDefaults() {
            ClickToReceive = this.GetLocalization(nameof(ClickToReceive), () => "点击领取");
            ClickToContinue = this.GetLocalization(nameof(ClickToContinue), () => "点击继续");
            ClickOrWaitToContinue = this.GetLocalization(nameof(ClickOrWaitToContinue), () => "点击/等待继续");
        }

        public static void ConfigureDefaults(int? appear = null, int? hold = null, int? give = null) {
            if (appear.HasValue && appear.Value > 0) {
                DefaultAppearDuration = appear.Value;
            }
            if (hold.HasValue) {
                DefaultHoldDuration = hold.Value;
            }
            if (give.HasValue && give.Value > 0) {
                DefaultGiveDuration = give.Value;
            }
        }

        public static void ShowReward(int itemId, int stack = 1, string text = null,
            int? appearDuration = null, int? holdDuration = null, int? giveDuration = null, bool requireClick = false,
            Func<Vector2> anchorProvider = null, Vector2? offset = null, Func<RewardStyle> styleProvider = null) {
            var inst = Instance;

            if (text == null && itemId > ItemID.None) {
                text = itemId < ItemID.Count ?
                    Language.GetText("ItemName." + ItemID.Search.GetName(itemId)).Value
                    : ItemLoader.GetItem(itemId).GetLocalization("DisplayName").Value;
            }

            inst.queue.Enqueue(new RewardEntry {
                ItemId = itemId,
                Stack = stack <= 0 ? 1 : stack,
                CustomText = text,
                AppearDuration = appearDuration.HasValue && appearDuration.Value > 0 ? appearDuration.Value : DefaultAppearDuration,
                HoldDuration = holdDuration.HasValue ? holdDuration.Value : DefaultHoldDuration,
                GiveDuration = giveDuration.HasValue && giveDuration.Value > 0 ? giveDuration.Value : DefaultGiveDuration,
                RequireClick = requireClick,
                AnchorProvider = anchorProvider,
                Offset = offset ?? Vector2.Zero,
                StyleProvider = styleProvider
            });
        }

        public static void ShowRewards(IEnumerable<(int itemId, int stack, string text, int? appear, int? hold, int? give, bool requireClick, Func<Vector2> anchor, Vector2? offset, Func<RewardStyle> styleProvider)> rewards) {
            foreach (var r in rewards) {
                ShowReward(r.itemId, r.stack, r.text, r.appear, r.hold, r.give, r.requireClick, r.anchor, r.offset, r.styleProvider);
            }
        }

        private void StartNext() {
            if (current != null) {
                return;
            }
            if (queue.Count == 0) {
                return;
            }
            current = queue.Dequeue();
            stateTimer = 0;
            givingOut = false;
            current.Appear = 0f;
            current.Hold = 0f;
            justOpened = true;

            //重置位置过渡状态
            isDialogueClosing = false;
            positionTransitionProgress = 0f;
            cachedAnchorPosition = Vector2.Zero;
            currentDisplayPosition = Vector2.Zero;

            //清空粒子
            ClearParticles();

            //初次播放音效
            SoundEngine.PlaySound(SoundID.Item4 with { Volume = 0.4f, Pitch = -0.2f });
        }

        private void ClearParticles() {
            bubbles.Clear();
            stars.Clear();
            embers.Clear();
            ashes.Clear();
            flameWisps.Clear();
            draedonDataParticles.Clear();
            draedonCircuitNodes.Clear();
        }

        private RewardStyle GetCurrentStyle() {
            if (current?.StyleProvider != null) {
                return current.StyleProvider();
            }
            return RewardStyle.Ocean; // 默认海洋风格
        }

        public override void Update() {
            if (!givingOut && current != null) {
                float appearT = current.AppearDuration > 0 ? Math.Clamp(stateTimer / (float)current.AppearDuration, 0f, 1f) : 1f;
                if (appearT >= 1f) {
                    current.Hold++;
                    bool autoReady = false;
                    if (current.HoldDuration >= 0 && current.Hold >= current.HoldDuration) autoReady = true;
                    //只有在鼠标悬停在面板上且点击时才触发
                    bool click = isHovering && keyLeftPressState == KeyPressState.Pressed;
                    if (autoReady && !current.RequireClick || click && appearT >= 0.95f) {
                        givingOut = true;
                        stateTimer = 0;
                        SoundEngine.PlaySound(SoundID.MenuOpen with { Volume = 0.5f, Pitch = 0.3f });
                    }
                }
            }
        }

        public new void LogicUpdate() {
            //更新通用动画计时器
            wavePhase += 0.02f;
            abyssPulse += 0.013f;
            panelPulse += 0.025f;
            if (wavePhase > MathHelper.TwoPi) wavePhase -= MathHelper.TwoPi;
            if (abyssPulse > MathHelper.TwoPi) abyssPulse -= MathHelper.TwoPi;
            if (panelPulse > MathHelper.TwoPi) panelPulse -= MathHelper.TwoPi;

            //硫磺火动画计时器
            flameTimer += 0.045f;
            emberGlowTimer += 0.038f;
            heatWavePhase += 0.025f;
            infernoPulse += 0.012f;
            if (flameTimer > MathHelper.TwoPi) flameTimer -= MathHelper.TwoPi;
            if (emberGlowTimer > MathHelper.TwoPi) emberGlowTimer -= MathHelper.TwoPi;
            if (heatWavePhase > MathHelper.TwoPi) heatWavePhase -= MathHelper.TwoPi;
            if (infernoPulse > MathHelper.TwoPi) infernoPulse -= MathHelper.TwoPi;

            //嘉登科技动画计时器
            draedonScanLineTimer += 0.048f;
            draedonHologramFlicker += 0.12f;
            draedonCircuitPulse += 0.025f;
            draedonDataStream += 0.055f;
            draedonHexGridPhase += 0.015f;
            if (draedonScanLineTimer > MathHelper.TwoPi) draedonScanLineTimer -= MathHelper.TwoPi;
            if (draedonHologramFlicker > MathHelper.TwoPi) draedonHologramFlicker -= MathHelper.TwoPi;
            if (draedonCircuitPulse > MathHelper.TwoPi) draedonCircuitPulse -= MathHelper.TwoPi;
            if (draedonDataStream > MathHelper.TwoPi) draedonDataStream -= MathHelper.TwoPi;
            if (draedonHexGridPhase > MathHelper.TwoPi) draedonHexGridPhase -= MathHelper.TwoPi;

            if (current == null && queue.Count > 0) {
                StartNext();
            }
            if (current == null) {
                if (panelFade > 0f) {
                    panelFade -= 0.08f;
                    if (panelFade < 0f) panelFade = 0f;
                }
                isHovering = false;
                return;
            }
            if (panelFade < 1f) {
                panelFade += 0.12f;
                if (panelFade > 1f) panelFade = 1f;
            }
            if (justOpened) {
                panelScale = 0.6f;
                justOpened = false;
            }
            panelScale = MathHelper.Lerp(panelScale, 1f, 0.18f);
            stateTimer++;
            if (!givingOut) {
                float appearT = current.AppearDuration > 0 ? Math.Clamp(stateTimer / (float)current.AppearDuration, 0f, 1f) : 1f;
                current.Appear = appearT;
                if (appearT >= 1f) {
                    current.Hold++;
                }
            }
            else {
                float t = current.GiveDuration > 0 ? Math.Clamp(stateTimer / (float)current.GiveDuration, 0f, 1f) : 1f;
                current.Appear = 1f - t;
                if (t >= 1f) {
                    GiveCurrent();
                    current = null;
                    StartNext();
                }
            }

            //检测鼠标悬停状态
            Vector2 mousePos = new Vector2(Main.mouseX, Main.mouseY);
            isHovering = panelRect.Contains(mousePos.ToPoint());

            //只有在悬停时才阻止鼠标交互穿透
            if (isHovering && Active) {
                player.mouseInterface = true;
            }

            //根据风格更新粒子
            Vector2 basePos = ResolveBasePosition(new Vector2(Main.screenWidth / 2f, Main.screenHeight * 0.45f));
            RewardStyle style = GetCurrentStyle();

            if (style == RewardStyle.Ocean) {
                UpdateOceanParticles(basePos);
            }
            else if (style == RewardStyle.Brimstone) {
                UpdateBrimstoneParticles(basePos);
            }
            else if (style == RewardStyle.Draedon) {
                UpdateDraedonParticles(basePos);
            }
        }

        private void UpdateOceanParticles(Vector2 basePos) {
            bubbleTimer++;
            if (panelFade > 0.6f && bubbleTimer >= 8 && bubbles.Count < 20) {
                bubbleTimer = 0;
                bubbles.Add(new Bubble(basePos + new Vector2(Main.rand.NextFloat(-80f, 80f), 40f)));
            }
            starTimer++;
            if (panelFade > 0.6f && starTimer >= 18 && stars.Count < 12) {
                starTimer = 0;
                stars.Add(new Star(basePos + new Vector2(Main.rand.NextFloat(-100f, 100f), Main.rand.NextFloat(-60f, 20f))));
            }
            for (int i = bubbles.Count - 1; i >= 0; i--) if (bubbles[i].Update()) bubbles.RemoveAt(i);
            for (int i = stars.Count - 1; i >= 0; i--) if (stars[i].Update()) stars.RemoveAt(i);
        }

        private void UpdateBrimstoneParticles(Vector2 basePos) {
            //余烬粒子生成
            emberSpawnTimer++;
            if (panelFade > 0.6f && emberSpawnTimer >= 8 && embers.Count < 35) {
                emberSpawnTimer = 0;
                float xPos = Main.rand.NextFloat(basePos.X - 120f + ParticleSideMargin, basePos.X + 120f - ParticleSideMargin);
                Vector2 startPos = new(xPos, basePos.Y + 66f - 5f);
                embers.Add(new EmberPRT(startPos));
            }
            for (int i = embers.Count - 1; i >= 0; i--) {
                if (embers[i].Update(basePos)) {
                    embers.RemoveAt(i);
                }
            }

            //灰烬粒子生成
            ashSpawnTimer++;
            if (panelFade > 0.6f && ashSpawnTimer >= 12 && ashes.Count < 25) {
                ashSpawnTimer = 0;
                float xPos = Main.rand.NextFloat(basePos.X - 120f + ParticleSideMargin, basePos.X + 120f - ParticleSideMargin);
                Vector2 startPos = new(xPos, basePos.Y + 66f);
                ashes.Add(new AshPRT(startPos));
            }
            for (int i = ashes.Count - 1; i >= 0; i--) {
                if (ashes[i].Update(basePos)) {
                    ashes.RemoveAt(i);
                }
            }

            //火焰精灵生成
            wispSpawnTimer++;
            if (panelFade > 0.6f && wispSpawnTimer >= 45 && flameWisps.Count < 8) {
                wispSpawnTimer = 0;
                Vector2 startPos = new(
                    Main.rand.NextFloat(basePos.X - 80f, basePos.X + 80f),
                    Main.rand.NextFloat(basePos.Y - 40f, basePos.Y + 40f)
                );
                flameWisps.Add(new FlameWispPRT(startPos));
            }
            for (int i = flameWisps.Count - 1; i >= 0; i--) {
                if (flameWisps[i].Update(basePos)) {
                    flameWisps.RemoveAt(i);
                }
            }
        }

        private void UpdateDraedonParticles(Vector2 basePos) {
            //数据粒子生成
            draedonDataParticleTimer++;
            if (panelFade > 0.6f && draedonDataParticleTimer >= 15 && draedonDataParticles.Count < 18) {
                draedonDataParticleTimer = 0;
                float xPos = Main.rand.NextFloat(basePos.X - 100f + DraedonParticleMargin, basePos.X + 100f - DraedonParticleMargin);
                Vector2 startPos = new(xPos, basePos.Y + Main.rand.NextFloat(-40f, 40f));
                draedonDataParticles.Add(new DraedonDataPRT(startPos));
            }
            for (int i = draedonDataParticles.Count - 1; i >= 0; i--) {
                if (draedonDataParticles[i].Update(basePos)) {
                    draedonDataParticles.RemoveAt(i);
                }
            }

            //电路节点生成
            draedonCircuitNodeTimer++;
            if (panelFade > 0.6f && draedonCircuitNodeTimer >= 30 && draedonCircuitNodes.Count < 10) {
                draedonCircuitNodeTimer = 0;
                Vector2 startPos = new(
                    Main.rand.NextFloat(basePos.X - 90f, basePos.X + 90f),
                    Main.rand.NextFloat(basePos.Y - 50f, basePos.Y + 50f)
                );
                draedonCircuitNodes.Add(new CircuitNodePRT(startPos));
            }
            for (int i = draedonCircuitNodes.Count - 1; i >= 0; i--) {
                if (draedonCircuitNodes[i].Update()) {
                    draedonCircuitNodes.RemoveAt(i);
                }
            }
        }

        private void GiveCurrent() {
            if (current == null || current.Given) return;
            var plr = Main.LocalPlayer;
            if (plr != null && plr.active) {
                int stack = current.Stack <= 0 ? 1 : current.Stack;
                var source = plr.GetSource_GiftOrReward();
                plr.QuickSpawnItem(source, current.ItemId, stack);
                SoundEngine.PlaySound(SoundID.Grab with { Volume = 0.55f, Pitch = 0.15f });
            }
            current.Given = true;
            OnRewardGiven?.Invoke(current);
        }

        private Vector2 ResolveBasePosition(Vector2 panelCenter) {
            //优先使用自定义锚点提供器
            if (current?.AnchorProvider != null) {
                Vector2 providedPos = current.AnchorProvider() + current.Offset;

                //检测对话框是否正在关闭
                if (DialogueUIRegistry.Current != null) {
                    var currentBox = DialogueUIRegistry.Current;

                    //如果对话框刚开始关闭，缓存当前位置
                    if (currentBox.closing && !isDialogueClosing) {
                        isDialogueClosing = true;
                        cachedAnchorPosition = providedPos;
                        positionTransitionProgress = 0f;
                    }

                    //对话框关闭中，使用缓存位置并平滑过渡到屏幕中心
                    if (isDialogueClosing) {
                        positionTransitionProgress += PositionTransitionSpeed;
                        positionTransitionProgress = Math.Clamp(positionTransitionProgress, 0f, 1f);

                        //使用缓动函数平滑过渡
                        float easeProgress = CWRUtils.EaseOutCubic(positionTransitionProgress);
                        currentDisplayPosition = Vector2.Lerp(cachedAnchorPosition, panelCenter, easeProgress);
                        return currentDisplayPosition;
                    }
                    else {
                        //对话框正常显示，直接使用提供的位置
                        currentDisplayPosition = providedPos;
                        return providedPos;
                    }
                }

                return providedPos;
            }

            //如果没有自定义锚点，尝试使用对话框位置
            if (DialogueUIRegistry.Current != null) {
                var rect = DialogueUIRegistry.Current.GetPanelRect();
                if (rect != Rectangle.Empty) {
                    Vector2 dialoguePos = new Vector2(rect.Center.X, rect.Y - 60f);

                    //同样处理对话框关闭的情况
                    var currentBox = DialogueUIRegistry.Current;

                    if (currentBox.closing && !isDialogueClosing) {
                        isDialogueClosing = true;
                        cachedAnchorPosition = dialoguePos;
                        positionTransitionProgress = 0f;
                    }

                    if (isDialogueClosing) {
                        positionTransitionProgress += PositionTransitionSpeed;
                        positionTransitionProgress = Math.Clamp(positionTransitionProgress, 0f, 1f);

                        float easeProgress = CWRUtils.EaseOutCubic(positionTransitionProgress);
                        currentDisplayPosition = Vector2.Lerp(cachedAnchorPosition, panelCenter, easeProgress);
                        return currentDisplayPosition;
                    }

                    return dialoguePos;
                }
            }

            //默认返回屏幕中心
            return panelCenter;
        }

        public override void Draw(SpriteBatch spriteBatch) {
            if (panelFade <= 0f) return;
            if (current == null && queue.Count == 0 && panelFade <= 0.01f) return;

            Vector2 screenCenter = new(Main.screenWidth / 2f, Main.screenHeight * 0.45f);
            Vector2 anchor = ResolveBasePosition(screenCenter);
            float alpha = panelFade;
            Vector2 panelSize = new(240f, 132f);
            float slideIn = 1f - (float)Math.Pow(1f - panelFade, 3f);
            Vector2 drawPos = anchor - panelSize / 2f;
            drawPos.Y -= MathHelper.Lerp(80f, 0f, slideIn);
            Rectangle rect = new((int)drawPos.X, (int)drawPos.Y, (int)panelSize.X, (int)panelSize.Y);

            //缓存面板矩形供鼠标检测使用
            panelRect = rect;

            //根据风格绘制
            RewardStyle style = GetCurrentStyle();
            if (style == RewardStyle.Ocean) {
                DrawOceanStyle(spriteBatch, rect, alpha);
            }
            else if (style == RewardStyle.Brimstone) {
                DrawBrimstoneStyle(spriteBatch, rect, alpha);
            }
            else if (style == RewardStyle.Draedon) {
                DrawDraedonStyle(spriteBatch, rect, alpha);
            }

            if (current != null) DrawRewardContent(spriteBatch, rect, alpha);
        }

        #region 海洋风格绘制
        private void DrawOceanStyle(SpriteBatch spriteBatch, Rectangle rect, float alpha) {
            Texture2D px = VaultAsset.placeholder2.Value;
            float hoverGlow = isHovering ? 0.15f : 0f;

            //深海渐层背景条
            int segs = 26;
            for (int i = 0; i < segs; i++) {
                float t = i / (float)segs;
                float t2 = (i + 1) / (float)segs;
                int y1 = rect.Y + (int)(t * rect.Height);
                int y2 = rect.Y + (int)(t2 * rect.Height);
                int height = Math.Max(1, y2 - y1);

                Rectangle r = new(rect.X, y1, rect.Width, height);

                Color abyssDeep = new Color(2, 10, 18);
                Color abyssMid = new Color(6, 32, 48);
                Color bioEdge = new Color(12, 80, 110);
                float breathing = (float)Math.Sin(abyssPulse) * 0.5f + 0.5f;
                Color blendBase = Color.Lerp(abyssDeep, abyssMid, (float)Math.Sin(panelPulse * 0.5f + t * 1.4f) * 0.5f + 0.5f);
                Color c = Color.Lerp(blendBase, bioEdge, t * 0.55f * (0.4f + breathing * 0.6f));
                c *= alpha * (0.92f + hoverGlow);
                spriteBatch.Draw(px, r, new Rectangle(0, 0, 1, 1), c);
            }

            //波浪横线
            DrawOceanWaveLines(spriteBatch, rect, alpha * 0.65f);

            //内边微光
            Rectangle inner = rect;
            inner.Inflate(-6, -6);
            spriteBatch.Draw(px, inner, new Rectangle(0, 0, 1, 1), new Color(30, 120, 150) * (alpha * (0.08f + hoverGlow * 0.5f) * (0.4f + (float)Math.Sin(panelPulse * 1.3f) * 0.6f)));

            //描边与角标
            DrawOceanFrame(spriteBatch, rect, alpha, hoverGlow);

            //气泡与星光
            foreach (var b in bubbles) b.Draw(spriteBatch, alpha * 0.85f);
            foreach (var s in stars) s.Draw(spriteBatch, alpha * 0.5f);
        }

        private void DrawOceanFrame(SpriteBatch sb, Rectangle rect, float alpha, float hoverGlow = 0f) {
            Texture2D px = VaultAsset.placeholder2.Value;
            Color edge = new Color(70, 180, 230) * (alpha * (0.85f + hoverGlow * 0.3f));
            sb.Draw(px, new Rectangle(rect.X, rect.Y, rect.Width, 2), new Rectangle(0, 0, 1, 1), edge);
            sb.Draw(px, new Rectangle(rect.X, rect.Bottom - 2, rect.Width, 2), new Rectangle(0, 0, 1, 1), edge * 0.7f);
            sb.Draw(px, new Rectangle(rect.X, rect.Y, 2, rect.Height), new Rectangle(0, 0, 1, 1), edge * 0.85f);
            sb.Draw(px, new Rectangle(rect.Right - 2, rect.Y, 2, rect.Height), new Rectangle(0, 0, 1, 1), edge * 0.85f);
            DrawOceanCornerStar(sb, new Vector2(rect.X + 10, rect.Y + 10), alpha * (0.9f + hoverGlow * 0.4f));
            DrawOceanCornerStar(sb, new Vector2(rect.Right - 10, rect.Y + 10), alpha * (0.9f + hoverGlow * 0.4f));
            DrawOceanCornerStar(sb, new Vector2(rect.X + 10, rect.Bottom - 10), alpha * (0.6f + hoverGlow * 0.3f));
            DrawOceanCornerStar(sb, new Vector2(rect.Right - 10, rect.Bottom - 10), alpha * (0.6f + hoverGlow * 0.3f));
        }

        private void DrawOceanWaveLines(SpriteBatch sb, Rectangle rect, float alpha) {
            Texture2D px = VaultAsset.placeholder2.Value;
            int bands = 4;
            for (int i = 0; i < bands; i++) {
                float t = i / (float)bands;
                float y = rect.Y + 18 + t * (rect.Height - 36);
                float amp = 5f + (float)Math.Sin((wavePhase + t) * 2f) * 3.2f;
                float thickness = 2f;
                int segments = 38;
                Vector2 prev = Vector2.Zero;
                for (int s = 0; s <= segments; s++) {
                    float p = s / (float)segments;
                    float localY = y + (float)Math.Sin(wavePhase * 2f + p * MathHelper.TwoPi * 1.1f + t) * amp;
                    Vector2 point = new(rect.X + 8 + p * (rect.Width - 16), localY);
                    if (s > 0) {
                        Vector2 diff = point - prev;
                        float len = diff.Length();
                        if (len > 0.01f) {
                            float rot = diff.ToRotation();
                            Color c = new Color(30, 120, 170) * (alpha * 0.06f);
                            sb.Draw(px, prev, new Rectangle(0, 0, 1, 1), c, rot, Vector2.Zero, new Vector2(len, thickness), SpriteEffects.None, 0f);
                        }
                    }
                    prev = point;
                }
            }
        }

        private static void DrawOceanCornerStar(SpriteBatch sb, Vector2 pos, float a) {
            Texture2D px = VaultAsset.placeholder2.Value;
            float size = 5f;
            Color c = new Color(150, 230, 255) * a;
            sb.Draw(px, pos, new Rectangle(0, 0, 1, 1), c, 0f, new Vector2(0.5f, 0.5f), new Vector2(size, size * 0.26f), SpriteEffects.None, 0f);
            sb.Draw(px, pos, new Rectangle(0, 0, 1, 1), c * 0.8f, MathHelper.PiOver2, new Vector2(0.5f, 0.5f), new Vector2(size, size * 0.26f), SpriteEffects.None, 0f);
        }
        #endregion

        #region 硫磺火风格绘制
        private void DrawBrimstoneStyle(SpriteBatch spriteBatch, Rectangle rect, float alpha) {
            Texture2D px = VaultAsset.placeholder2.Value;
            float hoverGlow = isHovering ? 0.15f : 0f;

            //渐变背景 - 硫磺火深红色
            int segments = 35;
            for (int i = 0; i < segments; i++) {
                float t = i / (float)segments;
                float t2 = (i + 1) / (float)segments;
                int y1 = rect.Y + (int)(t * rect.Height);
                int y2 = rect.Y + (int)(t2 * rect.Height);
                Rectangle r = new(rect.X, y1, rect.Width, Math.Max(1, y2 - y1));

                //硫磺火色调：深红到暗橙
                Color brimstoneDeep = new Color(25, 5, 5);
                Color brimstoneMid = new Color(80, 15, 10);
                Color brimstoneHot = new Color(140, 35, 20);

                float breathing = (float)Math.Sin(infernoPulse * 1.5f) * 0.5f + 0.5f;
                float flameWave = (float)Math.Sin(flameTimer * 0.6f + t * 2.2f) * 0.5f + 0.5f;

                Color baseColor = Color.Lerp(brimstoneDeep, brimstoneMid, flameWave);
                Color finalColor = Color.Lerp(baseColor, brimstoneHot, t * 0.5f * (0.3f + breathing * 0.7f));
                finalColor *= alpha * (0.92f + hoverGlow);

                spriteBatch.Draw(px, r, new Rectangle(0, 0, 1, 1), finalColor);
            }

            //火焰脉冲叠加层
            float pulseBrightness = (float)Math.Sin(infernoPulse * 1.8f) * 0.5f + 0.5f;
            Color pulseOverlay = new Color(120, 25, 15) * (alpha * 0.25f * pulseBrightness);
            spriteBatch.Draw(px, rect, new Rectangle(0, 0, 1, 1), pulseOverlay);

            //热浪扭曲效果层
            DrawBrimstoneHeatWave(spriteBatch, rect, alpha * 0.85f);

            //内发光
            float glowPulse = (float)Math.Sin(emberGlowTimer * 1.5f) * 0.5f + 0.5f;
            Rectangle inner = rect;
            inner.Inflate(-7, -7);
            spriteBatch.Draw(px, inner, new Rectangle(0, 0, 1, 1), new Color(180, 60, 30) * (alpha * (0.12f + hoverGlow * 0.5f) * (0.5f + glowPulse * 0.5f)));

            //绘制火焰边框
            DrawBrimstoneFrame(spriteBatch, rect, alpha, glowPulse, hoverGlow);

            //绘制粒子（从后到前）
            foreach (var ash in ashes) {
                ash.Draw(spriteBatch, alpha * 0.7f);
            }
            foreach (var wisp in flameWisps) {
                wisp.Draw(spriteBatch, alpha * 0.8f);
            }
            foreach (var ember in embers) {
                ember.Draw(spriteBatch, alpha * 0.95f);
            }
        }

        private void DrawBrimstoneHeatWave(SpriteBatch sb, Rectangle rect, float alpha) {
            Texture2D px = VaultAsset.placeholder2.Value;
            int waveCount = 8;
            for (int i = 0; i < waveCount; i++) {
                float t = i / (float)waveCount;
                float baseY = rect.Y + 25 + t * (rect.Height - 50);
                float amplitude = 5f + (float)Math.Sin((heatWavePhase + t * 1.2f) * 2.5f) * 3.5f;
                float thickness = 1.8f;

                int segments = 50;
                Vector2 prevPoint = Vector2.Zero;
                for (int s = 0; s <= segments; s++) {
                    float progress = s / (float)segments;
                    float waveY = baseY + (float)Math.Sin(heatWavePhase * 3f + progress * MathHelper.TwoPi * 1.5f + t * 2f) * amplitude;
                    Vector2 point = new(rect.X + 12 + progress * (rect.Width - 24), waveY);

                    if (s > 0) {
                        Vector2 diff = point - prevPoint;
                        float len = diff.Length();
                        if (len > 0.01f) {
                            float rot = diff.ToRotation();
                            Color waveColor = new Color(180, 60, 30) * (alpha * 0.08f);
                            sb.Draw(px, prevPoint, new Rectangle(0, 0, 1, 1), waveColor, rot, Vector2.Zero, new Vector2(len, thickness), SpriteEffects.None, 0f);
                        }
                    }
                    prevPoint = point;
                }
            }
        }

        private void DrawBrimstoneFrame(SpriteBatch sb, Rectangle rect, float alpha, float pulse, float hoverGlow = 0f) {
            Texture2D px = VaultAsset.placeholder2.Value;

            //外框 - 火焰橙红
            Color outerEdge = Color.Lerp(new Color(180, 60, 30), new Color(255, 140, 70), pulse) * (alpha * (0.85f + hoverGlow * 0.3f));
            sb.Draw(px, new Rectangle(rect.X, rect.Y, rect.Width, 3), new Rectangle(0, 0, 1, 1), outerEdge);
            sb.Draw(px, new Rectangle(rect.X, rect.Bottom - 3, rect.Width, 3), new Rectangle(0, 0, 1, 1), outerEdge * 0.75f);
            sb.Draw(px, new Rectangle(rect.X, rect.Y, 3, rect.Height), new Rectangle(0, 0, 1, 1), outerEdge * 0.9f);
            sb.Draw(px, new Rectangle(rect.Right - 3, rect.Y, 3, rect.Height), new Rectangle(0, 0, 1, 1), outerEdge * 0.9f);

            //内框发光
            Rectangle inner = rect;
            inner.Inflate(-6, -6);
            Color innerGlow = new Color(220, 100, 50) * (alpha * (0.22f + hoverGlow * 0.5f) * pulse);
            sb.Draw(px, new Rectangle(inner.X, inner.Y, inner.Width, 1), new Rectangle(0, 0, 1, 1), innerGlow);
            sb.Draw(px, new Rectangle(inner.X, inner.Bottom - 1, inner.Width, 1), new Rectangle(0, 0, 1, 1), innerGlow * 0.7f);
            sb.Draw(px, new Rectangle(inner.X, inner.Y, 1, inner.Height), new Rectangle(0, 0, 1, 1), innerGlow * 0.85f);
            sb.Draw(px, new Rectangle(inner.Right - 1, inner.Y, 1, inner.Height), new Rectangle(0, 0, 1, 1), innerGlow * 0.85f);

            //角落火焰标记
            DrawFlameMark(sb, new Vector2(rect.X + 12, rect.Y + 12), alpha * (0.95f + hoverGlow * 0.4f));
            DrawFlameMark(sb, new Vector2(rect.Right - 12, rect.Y + 12), alpha * (0.95f + hoverGlow * 0.4f));
            DrawFlameMark(sb, new Vector2(rect.X + 12, rect.Bottom - 12), alpha * (0.65f + hoverGlow * 0.3f));
            DrawFlameMark(sb, new Vector2(rect.Right - 12, rect.Bottom - 12), alpha * (0.65f + hoverGlow * 0.3f));
        }

        private static void DrawFlameMark(SpriteBatch sb, Vector2 pos, float alpha) {
            Texture2D px = VaultAsset.placeholder2.Value;
            float size = 6f;
            Color flameColor = new Color(255, 150, 70) * alpha;

            //交叉火焰标记
            sb.Draw(px, pos, new Rectangle(0, 0, 1, 1), flameColor, 0f, new Vector2(0.5f, 0.5f), new Vector2(size * 1.2f, size * 0.3f), SpriteEffects.None, 0f);
            sb.Draw(px, pos, new Rectangle(0, 0, 1, 1), flameColor * 0.85f, MathHelper.PiOver2, new Vector2(0.5f, 0.5f), new Vector2(size * 1.2f, size * 0.3f), SpriteEffects.None, 0f);
            sb.Draw(px, pos, new Rectangle(0, 0, 1, 1), flameColor * 0.7f, MathHelper.PiOver4, new Vector2(0.5f, 0.5f), new Vector2(size * 0.9f, size * 0.25f), SpriteEffects.None, 0f);
            sb.Draw(px, pos, new Rectangle(0, 0, 1, 1), flameColor * 0.7f, -MathHelper.PiOver4, new Vector2(0.5f, 0.5f), new Vector2(size * 0.9f, size * 0.25f), SpriteEffects.None, 0f);
        }
        #endregion

        #region 嘉登科技风格绘制
        private void DrawDraedonStyle(SpriteBatch spriteBatch, Rectangle rect, float alpha) {
            Texture2D px = VaultAsset.placeholder2.Value;
            float hoverGlow = isHovering ? 0.15f : 0f;

            //主背景渐变 - 深蓝科技色调
            int segments = 30;
            for (int i = 0; i < segments; i++) {
                float t = i / (float)segments;
                float t2 = (i + 1) / (float)segments;
                int y1 = rect.Y + (int)(t * rect.Height);
                int y2 = rect.Y + (int)(t2 * rect.Height);
                Rectangle r = new(rect.X, y1, rect.Width, Math.Max(1, y2 - y1));

                //嘉登色调：深蓝到亮蓝
                Color techDark = new Color(8, 12, 22);
                Color techMid = new Color(18, 28, 42);
                Color techBright = new Color(35, 55, 85);

                float pulse = (float)Math.Sin(draedonCircuitPulse * 0.6f + t * 2.0f) * 0.5f + 0.5f;
                Color baseColor = Color.Lerp(techDark, techMid, pulse);
                Color finalColor = Color.Lerp(baseColor, techBright, t * 0.45f);
                finalColor *= alpha * (0.92f + hoverGlow);

                spriteBatch.Draw(px, r, new Rectangle(0, 0, 1, 1), finalColor);
            }

            //全息闪烁覆盖层
            float flicker = (float)Math.Sin(draedonHologramFlicker * 1.5f) * 0.5f + 0.5f;
            Color hologramOverlay = new Color(15, 30, 45) * (alpha * 0.25f * flicker);
            spriteBatch.Draw(px, rect, new Rectangle(0, 0, 1, 1), hologramOverlay);

            //六角网格纹理
            DrawDraedonHexGrid(spriteBatch, rect, alpha * 0.85f);

            //扫描线效果
            DrawDraedonScanLines(spriteBatch, rect, alpha * 0.9f);

            //电路脉冲内发光
            float innerPulse = (float)Math.Sin(draedonCircuitPulse * 1.3f) * 0.5f + 0.5f;
            Rectangle inner = rect;
            inner.Inflate(-5, -5);
            spriteBatch.Draw(px, inner, new Rectangle(0, 0, 1, 1), new Color(40, 180, 255) * (alpha * (0.12f + hoverGlow * 0.5f) * innerPulse));

            //科技边框
            DrawDraedonFrame(spriteBatch, rect, alpha, innerPulse, hoverGlow);

            //绘制粒子
            foreach (var node in draedonCircuitNodes) {
                node.Draw(spriteBatch, alpha * 0.85f);
            }
            foreach (var particle in draedonDataParticles) {
                particle.Draw(spriteBatch, alpha * 0.75f);
            }
        }

        private void DrawDraedonHexGrid(SpriteBatch sb, Rectangle rect, float alpha) {
            Texture2D px = VaultAsset.placeholder2.Value;
            int hexRows = 6;
            float hexHeight = rect.Height / (float)hexRows;

            for (int row = 0; row < hexRows; row++) {
                float t = row / (float)hexRows;
                float y = rect.Y + row * hexHeight;
                float phase = draedonHexGridPhase + t * MathHelper.Pi;
                float brightness = (float)Math.Sin(phase) * 0.5f + 0.5f;

                Color gridColor = new Color(25, 90, 140) * (alpha * 0.04f * brightness);
                sb.Draw(px, new Rectangle(rect.X + 10, (int)y, rect.Width - 20, 1), new Rectangle(0, 0, 1, 1), gridColor);
            }
        }

        private void DrawDraedonScanLines(SpriteBatch sb, Rectangle rect, float alpha) {
            Texture2D px = VaultAsset.placeholder2.Value;
            float scanY = rect.Y + (float)Math.Sin(draedonScanLineTimer) * 0.5f * rect.Height + rect.Height * 0.5f;

            for (int i = -2; i <= 2; i++) {
                float offsetY = scanY + i * 3f;
                if (offsetY < rect.Y || offsetY > rect.Bottom) {
                    continue;
                }

                float intensity = 1f - Math.Abs(i) * 0.3f;
                Color scanColor = new Color(60, 180, 255) * (alpha * 0.15f * intensity);
                sb.Draw(px, new Rectangle(rect.X + 8, (int)offsetY, rect.Width - 16, 2), new Rectangle(0, 0, 1, 1), scanColor);
            }
        }

        private void DrawDraedonFrame(SpriteBatch sb, Rectangle rect, float alpha, float pulse, float hoverGlow = 0f) {
            Texture2D px = VaultAsset.placeholder2.Value;

            //外框 - 科技蓝
            Color techEdge = Color.Lerp(new Color(40, 160, 240), new Color(80, 200, 255), pulse) * (alpha * (0.85f + hoverGlow * 0.3f));
            sb.Draw(px, new Rectangle(rect.X, rect.Y, rect.Width, 3), new Rectangle(0, 0, 1, 1), techEdge);
            sb.Draw(px, new Rectangle(rect.X, rect.Bottom - 3, rect.Width, 3), new Rectangle(0, 0, 1, 1), techEdge * 0.75f);
            sb.Draw(px, new Rectangle(rect.X, rect.Y, 3, rect.Height), new Rectangle(0, 0, 1, 1), techEdge * 0.9f);
            sb.Draw(px, new Rectangle(rect.Right - 3, rect.Y, 3, rect.Height), new Rectangle(0, 0, 1, 1), techEdge * 0.9f);

            //内框发光
            Rectangle inner = rect;
            inner.Inflate(-6, -6);
            Color innerGlow = new Color(100, 200, 255) * (alpha * (0.22f + hoverGlow * 0.5f) * pulse);
            sb.Draw(px, new Rectangle(inner.X, inner.Y, inner.Width, 1), new Rectangle(0, 0, 1, 1), innerGlow);
            sb.Draw(px, new Rectangle(inner.X, inner.Bottom - 1, inner.Width, 1), new Rectangle(0, 0, 1, 1), innerGlow * 0.7f);
            sb.Draw(px, new Rectangle(inner.X, inner.Y, 1, inner.Height), new Rectangle(0, 0, 1, 1), innerGlow * 0.9f);
            sb.Draw(px, new Rectangle(inner.Right - 1, inner.Y, 1, inner.Height), new Rectangle(0, 0, 1, 1), innerGlow * 0.9f);

            //角落电路标记
            DrawDraedonCircuitMark(sb, new Vector2(rect.X + 12, rect.Y + 12), alpha * (0.95f + hoverGlow * 0.4f));
            DrawDraedonCircuitMark(sb, new Vector2(rect.Right - 12, rect.Y + 12), alpha * (0.95f + hoverGlow * 0.4f));
            DrawDraedonCircuitMark(sb, new Vector2(rect.X + 12, rect.Bottom - 12), alpha * (0.65f + hoverGlow * 0.3f));
            DrawDraedonCircuitMark(sb, new Vector2(rect.Right - 12, rect.Bottom - 12), alpha * (0.65f + hoverGlow * 0.3f));
        }

        private static void DrawDraedonCircuitMark(SpriteBatch sb, Vector2 pos, float alpha) {
            Texture2D px = VaultAsset.placeholder2.Value;
            float size = 6f;
            Color techColor = new Color(100, 220, 255) * alpha;

            //电路标记
            sb.Draw(px, pos, new Rectangle(0, 0, 1, 1), techColor, 0f, new Vector2(0.5f, 0.5f), new Vector2(size, size * 0.2f), SpriteEffects.None, 0f);
            sb.Draw(px, pos, new Rectangle(0, 0, 1, 1), techColor * 0.85f, MathHelper.PiOver2, new Vector2(0.5f, 0.5f), new Vector2(size, size * 0.2f), SpriteEffects.None, 0f);
            sb.Draw(px, pos, new Rectangle(0, 0, 1, 1), techColor * 0.6f, 0f, new Vector2(0.5f, 0.5f), new Vector2(size * 0.4f, size * 0.4f), SpriteEffects.None, 0f);
        }
        #endregion

        private void DrawRewardContent(SpriteBatch spriteBatch, Rectangle rect, float alpha) {
            float a = current.Appear;
            float ease = (float)Math.Sin(a * MathHelper.PiOver2);
            float iconScaleEase = MathHelper.Lerp(0.35f, 1f, ease);
            float iconAlpha = Math.Clamp(a, 0f, 1f);
            Vector2 iconCenter = new(rect.Center.X, rect.Center.Y - 12f);
            Item item = new();
            item.SetDefaults(current.ItemId);
            Main.instance.LoadItem(current.ItemId);
            Texture2D tex = TextureAssets.Item[item.type].Value;

            float bounce = (float)Math.Sin(MathHelper.Clamp(ease * 1.2f, 0f, 1f) * MathHelper.Pi) * 0.08f;
            float itemScale = (iconScaleEase + bounce) * panelScale;
            float floatOff = (float)Math.Sin(Main.GlobalTimeWrappedHourly * 3.2f + a) * 4f * iconAlpha;
            Vector2 floatPos = iconCenter + new Vector2(0, floatOff);

            VaultUtils.SimpleDrawItem(spriteBatch, current.ItemId, floatPos, 10, itemScale * 6, 0, Color.White * (iconAlpha * alpha));
            if (current.Stack > 1) {
                Vector2 hs = FontAssets.MouseText.Value.MeasureString(current.Stack.ToString());
                Utils.DrawBorderStringFourWay(spriteBatch, FontAssets.MouseText.Value, current.Stack.ToString()
                , floatPos.X - hs.X / 2, floatPos.Y + hs.Y, Color.White * (iconAlpha * alpha), Color.Black, new Vector2(0.2f), 0.8f);
            }

            string name = current.CustomText;
            if (string.IsNullOrEmpty(name)) name = item.Name;
            var font = FontAssets.MouseText.Value;
            Vector2 hs2 = font.MeasureString(name) * 0.8f;
            Vector2 hsOffset = new Vector2(hs2.X / -2, hs2.Y / 2);
            Vector2 namePos = iconCenter + new Vector2(0, 40f);
            float nameAlpha = iconAlpha * alpha;

            //根据风格调整文字颜色
            RewardStyle style = GetCurrentStyle();
            Color nameGlow, nameColor;

            if (style == RewardStyle.Brimstone) {
                nameGlow = new Color(255, 150, 80) * (nameAlpha * 0.6f);
                nameColor = new Color(255, 220, 200) * nameAlpha;
            }
            else if (style == RewardStyle.Draedon) {
                nameGlow = new Color(80, 220, 255) * (nameAlpha * 0.8f);
                nameColor = Color.White * nameAlpha;
            }
            else {
                nameGlow = new Color(140, 230, 255) * (nameAlpha * 0.6f);
                nameColor = Color.White * nameAlpha;
            }

            for (int i = 0; i < 4; i++) {
                float ang = MathHelper.TwoPi * i / 4f;
                Vector2 off = ang.ToRotationVector2() * 1.7f;
                Utils.DrawBorderString(spriteBatch, name, namePos + off + hsOffset, nameGlow * 0.55f, 0.8f);
            }

            Utils.DrawBorderString(spriteBatch, name, namePos + hsOffset, nameColor, 0.8f);

            if (a >= 1f) {
                string hint;
                if (current.RequireClick) {
                    hint = ClickToReceive.Value;
                }
                else if (current.HoldDuration < 0) {
                    hint = ClickToContinue.Value;
                }
                else {
                    hint = ClickOrWaitToContinue.Value;
                }
                Vector2 hs = font.MeasureString(hint) * 0.6f;
                Vector2 hp = new(rect.Right - hs.X - 12f, rect.Bottom - hs.Y - 10f);
                float blink = (float)Math.Sin(Main.GlobalTimeWrappedHourly * 6f) * 0.5f + 0.5f;
                //悬停时提示文字更亮
                float hintAlpha = isHovering ? blink * alpha * 1.2f : blink * alpha * 0.7f;

                Color hintColor;
                if (style == RewardStyle.Brimstone) {
                    hintColor = new Color(255, 160, 90) * hintAlpha;
                }
                else if (style == RewardStyle.Draedon) {
                    hintColor = new Color(80, 220, 255) * hintAlpha;
                }
                else {
                    hintColor = new Color(140, 230, 255) * hintAlpha;
                }

                Utils.DrawBorderString(spriteBatch, hint, hp, hintColor, 0.7f);
            }
        }

        //去他妈的吧，最好别动这些粒子，我写得很累

        #region 海洋粒子类
        private class Bubble(Vector2 p)
        {
            public Vector2 Pos = p;
            public float R = Main.rand.NextFloat(3f, 7f);
            public float Life = 0f;
            public float MaxLife = Main.rand.NextFloat(60f, 120f);
            public float Seed = Main.rand.NextFloat(10f);

            public bool Update() {
                Life++;
                Pos.Y -= 0.6f + (float)Math.Sin(Life * 0.05f + Seed) * 0.2f;
                Pos.X += (float)Math.Sin(Life * 0.04f + Seed) * 0.5f;
                if (Life >= MaxLife) return true;
                return false;
            }

            public void Draw(SpriteBatch sb, float a) {
                Texture2D px = VaultAsset.placeholder2.Value;
                float t = Life / MaxLife;
                float fade = (float)Math.Sin(t * MathHelper.Pi);
                float scale = R * (0.8f + (float)Math.Sin((Life + Seed * 15f) * 0.08f) * 0.2f);
                Color core = new Color(140, 230, 255) * (a * 0.4f * fade);
                Color rim = new Color(30, 100, 150) * (a * 0.25f * fade);
                sb.Draw(px, Pos, new Rectangle(0, 0, 1, 1), rim, 0f, new Vector2(0.5f, 0.5f), new Vector2(scale * 1.6f, scale * 0.5f), SpriteEffects.None, 0f);
                sb.Draw(px, Pos, new Rectangle(0, 0, 1, 1), core, 0f, new Vector2(0.5f, 0.5f), new Vector2(scale, scale), SpriteEffects.None, 0f);
            }
        }

        private class Star(Vector2 p)
        {
            public Vector2 Pos = p;
            public float Life = 0f;
            public float MaxLife = Main.rand.NextFloat(70f, 140f);
            public float Seed = Main.rand.NextFloat(10f);

            public bool Update() {
                Life++;
                if (Life >= MaxLife) return true;
                return false;
            }

            public void Draw(SpriteBatch sb, float a) {
                Texture2D px = VaultAsset.placeholder2.Value;
                float t = Life / MaxLife;
                float fade = (float)Math.Sin(t * MathHelper.Pi) * a;
                float scale = 2.5f + (float)Math.Sin((Life + Seed * 30f) * 0.1f) * 1.2f;
                Color c = Color.Gold * (0.6f * fade);
                sb.Draw(px, Pos, new Rectangle(0, 0, 1, 1), c, 0f, new Vector2(0.5f, 0.5f), new Vector2(scale, scale * 0.3f), SpriteEffects.None, 0f);
                sb.Draw(px, Pos, new Rectangle(0, 0, 1, 1), c * 0.8f, MathHelper.PiOver2, new Vector2(0.5f, 0.5f), new Vector2(scale, scale * 0.3f), SpriteEffects.None, 0f);
            }
        }
        #endregion
    }
}
