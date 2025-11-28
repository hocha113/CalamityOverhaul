using CalamityOverhaul.Content.ADV.ADVRewardPopups.Styles;
using CalamityOverhaul.Content.ADV.DialogueBoxs;
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

namespace CalamityOverhaul.Content.ADV.ADVRewardPopups
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
            Ocean,      //海洋风格（默认）
            Brimstone,  //硫磺火风格
            Draedon,    //嘉登科技风格
            Sulfsea     //硫磺海风格
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

        //样式系统
        private RewardStyle currentStyleType = RewardStyle.Ocean;
        private IRewardPopupStyle currentStyle;
        private readonly Dictionary<RewardStyle, IRewardPopupStyle> styleInstances = new();

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

            //初始化样式实例
            var inst = Instance;
            inst.styleInstances[RewardStyle.Ocean] = new OceanRewardStyle();
            inst.styleInstances[RewardStyle.Brimstone] = new BrimstoneRewardStyle();
            inst.styleInstances[RewardStyle.Draedon] = new DraedonRewardStyle();
            inst.styleInstances[RewardStyle.Sulfsea] = new SulfseaRewardStyle();
            inst.currentStyle = inst.styleInstances[RewardStyle.Ocean];
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

            //切换样式
            RewardStyle targetStyle = GetCurrentStyle();
            if (targetStyle != currentStyleType) {
                currentStyleType = targetStyle;
                if (styleInstances.TryGetValue(targetStyle, out var styleInstance)) {
                    currentStyle = styleInstance;
                    currentStyle.Reset();
                }
            }

            //初次播放音效
            SoundEngine.PlaySound(SoundID.Item4 with { Volume = 0.4f, Pitch = -0.2f });
        }

        private RewardStyle GetCurrentStyle() {
            if (current?.StyleProvider != null) {
                return current.StyleProvider();
            }
            return RewardStyle.Ocean; //默认海洋风格
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

            //更新样式动画
            if (currentStyle != null) {
                Vector2 basePos = ResolveBasePosition(new Vector2(Main.screenWidth / 2f, Main.screenHeight * 0.45f));
                currentStyle.Update(panelRect, Active, false);
                currentStyle.UpdateParticles(basePos, panelFade);
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

            float hoverGlow = isHovering ? 0.15f : 0f;

            //使用当前样式绘制面板
            if (currentStyle != null) {
                currentStyle.DrawPanel(spriteBatch, rect, alpha, hoverGlow);
                currentStyle.DrawFrame(spriteBatch, rect, alpha, hoverGlow);

                //绘制样式粒子
                currentStyle.GetParticles(out var particles);
                foreach (var particle in particles) {
                    if (particle is BubblePRT bubble) {
                        bubble.Draw(spriteBatch, alpha * 0.85f);
                    }
                    else if (particle is SeaStarPRT star) {
                        star.Draw(spriteBatch, alpha * 0.5f);
                    }
                    else if (particle is AshPRT ash) {
                        ash.Draw(spriteBatch, alpha * 0.7f);
                    }
                    else if (particle is FlameWispPRT wisp) {
                        wisp.Draw(spriteBatch, alpha * 0.8f);
                    }
                    else if (particle is EmberPRT ember) {
                        ember.Draw(spriteBatch, alpha * 0.95f);
                    }
                    else if (particle is CircuitNodePRT node) {
                        node.Draw(spriteBatch, alpha * 0.85f);
                    }
                    else if (particle is DraedonDataPRT data) {
                        data.Draw(spriteBatch, alpha * 0.75f);
                    }
                }
            }

            if (current != null) DrawRewardContent(spriteBatch, rect, alpha);
        }

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

            //从样式获取文字颜色
            Color nameGlow = currentStyle?.GetNameGlowColor(nameAlpha) ?? new Color(140, 230, 255) * (nameAlpha * 0.6f);
            Color nameColor = currentStyle?.GetNameColor(nameAlpha) ?? Color.White * nameAlpha;

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

                Color hintColor = currentStyle?.GetHintColor(hintAlpha, blink) ?? new Color(140, 230, 255) * hintAlpha;

                Utils.DrawBorderString(spriteBatch, hint, hp, hintColor, 0.7f);
            }
        }
    }
}
