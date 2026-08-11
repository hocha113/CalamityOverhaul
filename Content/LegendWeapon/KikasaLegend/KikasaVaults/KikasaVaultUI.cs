using CalamityOverhaul.Common;
using CalamityOverhaul.Content.UIs.UIEffect;
using InnoVault.UIHandles;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.GameInput;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaVaults
{
    /// <summary>
    /// 湖窗：血湖藏品界面。旧世界的湿纸被撕开一道口子，口子里湖水涨起——
    /// 沉物按深浅悬在血水里漂，悬停时它浮起凝出真身、气泡一串升向水面、
    /// 水线在它正上方泛沫发亮；点击提取，槽位留一个小旋涡。
    /// 持鬼伞按 <see cref="CWRKeySystem.Legend_UIControl"/> 开阖，湖水退落时自行合上。
    /// </summary>
    internal class KikasaVaultUI : UIHandle, ILocalizedModType
    {
        public string LocalizationCategory => "Legend.KikasaText";

        public static KikasaVaultUI Instance => UIHandleLoader.GetUIHandleOfType<KikasaVaultUI>();

        public static LocalizedText Title { get; private set; }
        public static LocalizedText CountFormat { get; private set; }
        public static LocalizedText ExtractHintFormat { get; private set; }
        public static LocalizedText IdleHintFormat { get; private set; }
        public static LocalizedText EmptyHint { get; private set; }

        public override void SetStaticDefaults() {
            Title = this.GetLocalization(nameof(Title), () => "湖 藏");
            CountFormat = this.GetLocalization(nameof(CountFormat), () => "沉物 {0} / {1}");
            ExtractHintFormat = this.GetLocalization(nameof(ExtractHintFormat), () => "点击取回 {0}");
            IdleHintFormat = this.GetLocalization(nameof(IdleHintFormat), () => "持物按 {0} 沉入 · 点击窗外合上");
            EmptyHint = this.GetLocalization(nameof(EmptyHint), () => "湖底空着，只有水声");
        }

        public override bool Active => IsOpen || OpenProgress > 0.01f;

        public override bool CloseOnEscape => true;

        public override Terraria.Audio.SoundStyle? OpenSound
            => SoundID.SplashWeak with { Volume = 0.5f, Pitch = -0.5f };

        public override Terraria.Audio.SoundStyle? CloseSound
            => SoundID.SplashWeak with { Volume = 0.4f, Pitch = -0.75f };

        //==================== 伞章 ====================
        //归一 [-1,1] 空间：圆拱伞盖 + 四瓣荷缘；顶针、中棒弯钩与两根斜骨

        private const string SealCanopy =
            "M -0.92 0.14 C -0.55 -0.66 0.55 -0.66 0.92 0.14 "
            + "Q 0.66 0.03 0.46 0.15 Q 0.23 0.02 0 0.15 "
            + "Q -0.23 0.02 -0.46 0.15 Q -0.66 0.03 -0.92 0.14";

        private const string SealFrame =
            "M 0 -0.62 L 0 0.88 Q 0.02 1.0 0.2 0.92 "
            + "M 0 -0.44 L -0.58 0.02 M 0 -0.44 L 0.58 0.02";

        //==================== 窗内小件 ====================

        //提取后槽位残留的小旋涡
        private struct DrainFx
        {
            public Vector2 Pos;
            public int Timer;
        }

        //窗内微粒：悬停物的上浮气泡 / 提取时溅起的血珠
        private struct UiMote
        {
            public Vector2 Pos;
            public Vector2 Vel;
            public float Phase;
            public float Size;
            public int Life;
            public int MaxLife;
            public bool Bubble;
        }

        private const int DrainLife = 20;
        private const int MoteCap = 56;

        private Rectangle panelRect;
        private float scrollOffset;
        private float scrollTarget;
        private int hoverIndex = -1;
        private readonly List<float> hoverLerp = [];
        private readonly List<DrainFx> drains = [];
        private readonly List<UiMote> motes = [];
        //面板活性与提取脉冲，悬停/提取/涨水时湖水更躁
        private float stir;
        private float stirPulse;
        //悬停列血光的平滑值与记忆位置（移开后光渐熄而不是瞬灭）
        private float hoverGlowSmooth;
        private float lastHoverX01 = -1f;
        private int frame;

        protected override void OnOpen() {
            Main.playerInventory = false;
            scrollOffset = scrollTarget = 0f;
            hoverIndex = -1;
            hoverLerp.Clear();
            drains.Clear();
            motes.Clear();
            stir = 0.6f;
            stirPulse = 0f;
            hoverGlowSmooth = 0f;
            lastHoverX01 = -1f;
        }

        private KikasaVaultPlayer Vault => player.GetModPlayer<KikasaVaultPlayer>();

        //==================== 布局 ====================

        private void LayoutPanel(float a) {
            float w = KikasaVaultTheme.PanelW;
            float h = KikasaVaultTheme.PanelH;
            float cx = KikasaVaultTheme.UIScreenW * 0.5f;
            float cy = KikasaVaultTheme.UIScreenH * KikasaVaultTheme.PanelCenterYRatio;
            //开阖余韵：未开满时整窗略沉
            float slide = (1f - a) * 14f;
            panelRect = new Rectangle(
                (int)(cx - w * 0.5f), (int)(cy - h * 0.5f + slide), (int)w, (int)h);
        }

        /// <summary>水位落定后的水面像素 Y，槽位锚在它上</summary>
        private float WaterTopFinal => panelRect.Y + panelRect.Height * KikasaVaultTheme.WaterLineY;

        /// <summary>开窗动画中的当前水位 uv：涨水前快后慢，末段轻微冒头再落定</summary>
        private static float WaterUv(float a) {
            float t = MathHelper.Clamp((a - 0.18f) / 0.72f, 0f, 1f);
            float ease = 1f - MathF.Pow(1f - t, 3f);
            float y = MathHelper.Lerp(0.96f, KikasaVaultTheme.WaterLineY, ease);
            y -= MathF.Sin(MathHelper.Clamp((t - 0.55f) / 0.45f, 0f, 1f) * MathHelper.Pi) * 0.018f;
            return y;
        }

        /// <summary>第 i 件沉物的静置中心（含平滑滚动，未含漂浮）</summary>
        private Vector2 SlotCenterRaw(int index) {
            int row = index / KikasaVaultTheme.SlotsPerRow;
            int col = index % KikasaVaultTheme.SlotsPerRow;
            float startX = panelRect.Center.X
                - (KikasaVaultTheme.SlotsPerRow - 1) * KikasaVaultTheme.SlotSpacingX * 0.5f;
            float y = WaterTopFinal + 56f + row * KikasaVaultTheme.SlotSpacingY - scrollOffset;
            return new Vector2(startX + col * KikasaVaultTheme.SlotSpacingX, y);
        }

        /// <summary>滚动视口上下缘的淡出（贴水线与近窗底），代替会截断悬停浮起的剪刀裁剪</summary>
        private float SlotFade(float y) {
            float top = WaterTopFinal + 12f;
            float bottom = panelRect.Bottom - 40f;
            float f = MathHelper.Clamp((y - top) / 26f, 0f, 1f);
            f *= MathHelper.Clamp((bottom - y) / 26f, 0f, 1f);
            return f;
        }

        private float MaxScrollOffset(int count) {
            int rows = (count + KikasaVaultTheme.SlotsPerRow - 1) / KikasaVaultTheme.SlotsPerRow;
            return MathF.Max(0f, (rows - KikasaVaultTheme.VisibleRows) * KikasaVaultTheme.SlotSpacingY);
        }

        //==================== 更新 ====================

        public override void Update() {
            float a = OpenProgress;
            if (a < 0.01f) {
                return;
            }
            frame++;
            KikasaVaultPlayer vault = Vault;

            if (IsOpen && (!vault.LakeReady || player.dead || !player.active)) {
                //湖退了人也不该还扒着湖窗
                Close();
            }

            LayoutPanel(a);
            List<Item> stored = vault.Stored;

            //悬停衰减表与存物列表对齐
            while (hoverLerp.Count < stored.Count) {
                hoverLerp.Add(0f);
            }
            if (hoverLerp.Count > stored.Count) {
                hoverLerp.RemoveRange(stored.Count, hoverLerp.Count - stored.Count);
            }

            Vector2 mouse = KikasaVaultTheme.UIMouse;
            bool overPanel = panelRect.Contains(mouse.ToPoint());
            bool inputAvailable = IsOpen && a > 0.9f;

            if (IsOpen && overPanel) {
                player.mouseInterface = true;
                UIInputGuard.SuppressWeaponSwitch();
            }

            //平滑滚动：滚轮拨行，位移逐帧跟进
            float maxOffset = MaxScrollOffset(stored.Count);
            scrollTarget = MathHelper.Clamp(scrollTarget, 0f, maxOffset);
            if (inputAvailable && overPanel && maxOffset > 0f) {
                int delta = PlayerInput.ScrollWheelDeltaForUI;
                if (delta != 0) {
                    scrollTarget = MathHelper.Clamp(
                        scrollTarget - Math.Sign(delta) * KikasaVaultTheme.SlotSpacingY, 0f, maxOffset);
                    PlayerInput.LockVanillaMouseScroll("CalamityOverhaul/KikasaVault");
                }
            }
            scrollOffset = MathHelper.Lerp(scrollOffset, scrollTarget, 0.22f);
            if (MathF.Abs(scrollOffset - scrollTarget) < 0.4f) {
                scrollOffset = scrollTarget;
            }

            //悬停判定：只认视口内没被淡出的槽位
            float waterPixY = panelRect.Y + panelRect.Height * WaterUv(a);
            int newHover = -1;
            if (inputAvailable && overPanel) {
                float half = (KikasaVaultTheme.SlotFit + 16f) * 0.5f;
                for (int i = 0; i < stored.Count; i++) {
                    Vector2 c = SlotCenterRaw(i);
                    if (SlotFade(c.Y) < 0.5f || c.Y < waterPixY + 14f) {
                        continue;
                    }
                    Rectangle hit = new((int)(c.X - half), (int)(c.Y - half),
                        (int)(half * 2f), (int)(half * 2f));
                    if (hit.Contains(mouse.ToPoint())) {
                        newHover = i;
                        break;
                    }
                }
            }
            if (newHover != hoverIndex && newHover >= 0) {
                SoundEngine.PlaySound(SoundID.MenuTick with { Volume = 0.35f, Pitch = -0.4f });
            }
            hoverIndex = newHover;
            for (int i = 0; i < hoverLerp.Count; i++) {
                float target = i == hoverIndex ? 1f : 0f;
                hoverLerp[i] = MathHelper.Lerp(hoverLerp[i], target, 0.18f);
            }

            //悬停列血光：位置记忆 + 光强平滑，移开后渐熄
            if (hoverIndex >= 0) {
                Vector2 hc = SlotCenterRaw(hoverIndex);
                lastHoverX01 = (hc.X - panelRect.X) / panelRect.Width;
            }
            hoverGlowSmooth = MathHelper.Lerp(hoverGlowSmooth, hoverIndex >= 0 ? 1f : 0f, 0.14f);

            //悬停物冒泡：一串气泡从它顶上升向水面
            if (hoverIndex >= 0 && frame % 5 == 0 && motes.Count < MoteCap) {
                Vector2 c = SlotCenterRaw(hoverIndex);
                motes.Add(new UiMote {
                    Pos = c + new Vector2(Main.rand.NextFloat(-10f, 10f), -KikasaVaultTheme.SlotFit * 0.4f),
                    Vel = new Vector2(0f, -Main.rand.NextFloat(0.55f, 0.95f)),
                    Phase = Main.rand.NextFloat(MathHelper.TwoPi),
                    Size = Main.rand.NextFloat(1.6f, 3.2f),
                    MaxLife = 120,
                    Bubble = true,
                });
            }

            //点击：面板内提取，面板外合窗
            if (inputAvailable && keyLeftPressState == KeyPressState.Pressed) {
                if (hoverIndex >= 0) {
                    Vector2 slotPos = SlotCenterRaw(hoverIndex);
                    if (vault.BeginExtract(hoverIndex)) {
                        drains.Add(new DrainFx { Pos = slotPos });
                        BurstDroplets(slotPos);
                        hoverLerp.RemoveAt(hoverIndex);
                        hoverIndex = -1;
                        stirPulse = 1f;
                        SoundEngine.PlaySound(SoundID.SplashWeak with { Volume = 0.55f, Pitch = 0.05f });
                        //演出是主角：合窗让位，旋涡与血珠在淡出里收尾，
                        //破水点就在脚边，窗不合上正好挡在它前面
                        Close();
                    }
                }
                else if (!overPanel) {
                    Close();
                }
            }

            UpdateMotes(waterPixY);

            for (int i = drains.Count - 1; i >= 0; i--) {
                DrainFx fx = drains[i];
                fx.Timer++;
                if (fx.Timer >= DrainLife) {
                    drains.RemoveAt(i);
                }
                else {
                    drains[i] = fx;
                }
            }

            //活性：悬停微躁、提取脉冲、开合期涨退水最烈
            float rest = hoverIndex >= 0 ? 0.30f : 0.10f;
            stir = MathHelper.Lerp(stir, rest, 0.07f);
            stirPulse *= 0.93f;
        }

        private void BurstDroplets(Vector2 from) {
            for (int i = 0; i < 7 && motes.Count < MoteCap; i++) {
                float ang = -MathHelper.PiOver2 + Main.rand.NextFloat(-0.9f, 0.9f);
                motes.Add(new UiMote {
                    Pos = from + new Vector2(Main.rand.NextFloat(-6f, 6f), 0f),
                    Vel = ang.ToRotationVector2() * Main.rand.NextFloat(1.4f, 3.0f),
                    Phase = Main.rand.NextFloat(MathHelper.TwoPi),
                    Size = Main.rand.NextFloat(1.4f, 2.4f),
                    MaxLife = Main.rand.Next(20, 30),
                    Bubble = false,
                });
            }
        }

        private void UpdateMotes(float waterPixY) {
            for (int i = motes.Count - 1; i >= 0; i--) {
                UiMote m = motes[i];
                m.Life++;
                if (m.Bubble) {
                    //气泡摇着升，到水面即破
                    m.Vel.Y = MathF.Max(m.Vel.Y - 0.012f, -1.5f);
                    m.Pos.X += MathF.Sin(m.Life * 0.22f + m.Phase) * 0.35f;
                    m.Pos.Y += m.Vel.Y;
                    if (m.Pos.Y <= waterPixY + 3f || m.Life >= m.MaxLife) {
                        motes.RemoveAt(i);
                        continue;
                    }
                }
                else {
                    //血珠抛起回落
                    m.Vel.Y += 0.16f;
                    m.Pos += m.Vel;
                    if (m.Life >= m.MaxLife || m.Pos.Y > panelRect.Bottom - 8f) {
                        motes.RemoveAt(i);
                        continue;
                    }
                }
                motes[i] = m;
            }
        }

        //==================== 绘制 ====================

        public override void Draw(SpriteBatch spriteBatch) {
            float a = OpenProgress;
            if (a < 0.01f) {
                return;
            }
            KikasaVaultPlayer vault = Vault;
            List<Item> stored = vault.Stored;
            float time = Main.GlobalTimeWrappedHourly;
            float waterUv = WaterUv(a);
            float waterPixY = panelRect.Y + panelRect.Height * waterUv;
            //涨退水时窗里最躁
            float effStir = MathHelper.Clamp(stir + stirPulse + (1f - a) * 0.75f, 0f, 1f);

            //1 面板：撕开的湿纸口子，水在里面涨
            KikasaVaultRenderer.DrawPanel(spriteBatch, panelRect, a, effStir,
                open: a, waterY: waterUv,
                hoverX01: hoverGlowSmooth > 0.02f ? lastHoverX01 : -1f,
                hoverGlow: hoverGlowSmooth);

            //2 窗头：伞章随撕开描完自己，题字与沉物计数跟着显影
            float chromeA = MathHelper.Clamp((a - 0.35f) / 0.5f, 0f, 1f);
            DynamicSpriteFont font = FontAssets.MouseText.Value;
            if (chromeA > 0.02f) {
                DrawSeal(spriteBatch, new Vector2(panelRect.X + 40f, panelRect.Y + 42f),
                    17f, chromeA, time, reveal: chromeA);
                Utils.DrawBorderString(spriteBatch, Title.Value,
                    new Vector2(panelRect.X + 66f, panelRect.Y + 26f),
                    KikasaVaultTheme.Text * chromeA, 1.02f);
                string count = string.Format(CountFormat.Value, stored.Count, KikasaVaultPlayer.Capacity);
                Vector2 countSize = font.MeasureString(count) * 0.85f;
                Utils.DrawBorderString(spriteBatch, count,
                    new Vector2(panelRect.Right - 22f - countSize.X, panelRect.Y + 30f),
                    KikasaVaultTheme.TextDim * chromeA, 0.85f);
            }

            //3 沉物：血水态按深浅漂浮，涨水漫过才显形；悬停浮起凝实
            List<(Vector2 pos, int stack, float alpha)> stackLabels = null;
            if (stored.Count > 0) {
                bool shaderOk = KikasaVaultRenderer.BeginItemBatch(spriteBatch, out Effect formEffect);
                float depthSpan = MathF.Max(panelRect.Bottom - WaterTopFinal, 1f);
                for (int i = 0; i < stored.Count; i++) {
                    Vector2 c = SlotCenterRaw(i);
                    float fade = SlotFade(c.Y);
                    //水没漫过它就还没显形；退水时同样先失去它
                    float reveal = MathHelper.Clamp((c.Y - waterPixY) / 16f, 0f, 1f);
                    float alpha = fade * reveal * a;
                    if (alpha <= 0.02f) {
                        continue;
                    }
                    Item item = stored[i];
                    float hover = i < hoverLerp.Count ? hoverLerp[i] : 0f;
                    float depth01 = MathHelper.Clamp((c.Y - WaterTopFinal) / depthSpan, 0f, 1f);
                    //漂浮相位错开；悬停轻轻托起
                    float bob = MathF.Sin(time * 1.35f + i * 1.71f) * 2.2f * (1f - hover * 0.6f);
                    Vector2 pos = c + new Vector2(0f, bob - hover * 6f);
                    //越深越沉入血水，悬停凝向真身
                    float form = MathHelper.Clamp(
                        MathHelper.Lerp(0.72f, 0.86f, depth01) - hover * 0.55f, 0.05f, 1f);
                    float itemAlpha = alpha * MathHelper.Lerp(1f, 0.84f, depth01 * (1f - hover));
                    KikasaVaultRenderer.DrawFormItem(spriteBatch, formEffect, shaderOk,
                        item.type, pos, form, i * 2.39f + 0.7f, itemAlpha);
                    if (item.stack > 1) {
                        stackLabels ??= [];
                        stackLabels.Add((pos + new Vector2(10f, 8f), item.stack, alpha));
                    }
                }
                KikasaVaultRenderer.EndItemBatch(spriteBatch);
            }

            //4 加色小件——真加色批源因子是 SourceAlpha，A 必须随强度走
            KikasaVaultRenderer.BeginAdditive(spriteBatch);
            //悬停列的水面泛沫浮圈：它正上方的水在等它
            if (hoverIndex >= 0 && hoverIndex < stored.Count) {
                float hover = hoverLerp[hoverIndex];
                Vector2 hc = SlotCenterRaw(hoverIndex);
                float breath = KikasaVaultTheme.Breath(time, hoverIndex, 3.1f);
                KikasaVaultRenderer.DrawRing(spriteBatch, new Vector2(hc.X, waterPixY),
                    20f + breath * 4f, 6f, KikasaVaultTheme.Foam * (0.34f * hover * a));
                //物件身侧的水光衬底
                KikasaVaultRenderer.DrawGlowDot(spriteBatch, hc + new Vector2(0f, -hover * 6f),
                    KikasaVaultTheme.SlotFit * 0.62f, KikasaVaultTheme.Blood * (0.16f * hover * a));
            }
            foreach (DrainFx fx in drains) {
                float t = fx.Timer / (float)DrainLife;
                float r = MathHelper.Lerp(20f, 3f, t * t);
                KikasaVaultRenderer.DrawRing(spriteBatch, fx.Pos,
                    r, r * 0.4f, KikasaVaultTheme.Foam * (0.4f * (1f - t) * a));
            }
            //气泡
            foreach (UiMote m in motes) {
                if (!m.Bubble) {
                    continue;
                }
                float la = MathHelper.Clamp(1f - m.Life / (float)m.MaxLife, 0f, 1f);
                KikasaVaultRenderer.DrawGlowDot(spriteBatch, m.Pos, m.Size,
                    KikasaVaultTheme.Foam * (0.36f * la * a));
            }
            //水线上两点游光，湖面在呼吸
            for (int k = 0; k < 2; k++) {
                float drift = (time * (0.05f + k * 0.023f) + k * 0.5f) % 1f;
                float gx = MathHelper.Lerp(panelRect.Left + 24f, panelRect.Right - 24f,
                    k == 0 ? drift : 1f - drift);
                float ga = KikasaVaultTheme.Breath(time, k * 3.7f, 2.4f);
                KikasaVaultRenderer.DrawGlowDot(spriteBatch, new Vector2(gx, waterPixY),
                    7f, KikasaVaultTheme.Foam * (0.16f * ga * a));
            }
            KikasaVaultRenderer.RestoreUIBatch(spriteBatch);

            //5 文字与细件层
            //血珠（普通混合的暗色小streak，不走加色）
            foreach (UiMote m in motes) {
                if (m.Bubble) {
                    continue;
                }
                float la = MathHelper.Clamp(1f - m.Life / (float)m.MaxLife, 0f, 1f);
                KikasaVaultRenderer.DrawLine(spriteBatch, m.Pos, m.Pos - m.Vel * 1.6f,
                    m.Size, KikasaVaultTheme.Blood * (0.7f * la * a));
            }

            //叠数
            if (stackLabels != null) {
                foreach ((Vector2 pos, int stack, float la) in stackLabels) {
                    Utils.DrawBorderString(spriteBatch, stack.ToString(), pos,
                        KikasaVaultTheme.Text * (0.9f * la), 0.78f);
                }
            }

            //滚动指示：右缘细朱迹
            float maxOffset = MaxScrollOffset(stored.Count);
            if (maxOffset > 0f) {
                int rows = (stored.Count + KikasaVaultTheme.SlotsPerRow - 1) / KikasaVaultTheme.SlotsPerRow;
                float x = panelRect.Right - 10f;
                float top = WaterTopFinal + 18f;
                float span = panelRect.Bottom - 26f - top;
                float viewRow = scrollOffset / KikasaVaultTheme.SlotSpacingY;
                for (int r = 0; r < rows; r++) {
                    float y = top + span * r / Math.Max(rows - 1, 1);
                    float inView = MathHelper.Clamp(1.2f - MathF.Abs(r - viewRow - 0.5f), 0f, 1f);
                    Color tick = Color.Lerp(KikasaVaultTheme.TextDim, KikasaVaultTheme.Foam, inView);
                    KikasaVaultRenderer.DrawLine(spriteBatch,
                        new Vector2(x - MathHelper.Lerp(3f, 5.5f, inView), y), new Vector2(x, y),
                        1.4f, tick * ((0.35f + 0.4f * inView) * a));
                }
            }

            //悬停名牌：贴着物件浮在水里，不用低头找页脚
            if (hoverIndex >= 0 && hoverIndex < stored.Count) {
                Item hovered = stored[hoverIndex];
                float hover = hoverLerp[hoverIndex];
                string name = hovered.AffixName();
                if (hovered.stack > 1) {
                    name += $" ×{hovered.stack}";
                }
                Vector2 size = font.MeasureString(name) * 0.85f;
                Vector2 hc = SlotCenterRaw(hoverIndex);
                float tagX = MathHelper.Clamp(hc.X - size.X * 0.5f,
                    panelRect.X + 12f, panelRect.Right - 12f - size.X);
                float tagY = hc.Y - KikasaVaultTheme.SlotFit * 0.5f - 26f;
                Utils.DrawBorderString(spriteBatch, name, new Vector2(tagX, tagY),
                    KikasaVaultTheme.Text * (hover * a), 0.85f);
            }

            //6 页脚：悬停时让位名牌，只留操作提示
            string footer;
            Color footerColor;
            if (hoverIndex >= 0 && hoverIndex < stored.Count) {
                footer = string.Format(ExtractHintFormat.Value, string.Empty).Trim();
                footerColor = KikasaVaultTheme.Foam;
            }
            else if (stored.Count == 0) {
                float breathe = 0.6f + 0.3f * KikasaVaultTheme.Breath(time, 1.7f, 1.6f);
                Vector2 eSize = font.MeasureString(EmptyHint.Value) * 0.9f;
                Utils.DrawBorderString(spriteBatch, EmptyHint.Value,
                    new Vector2(panelRect.Center.X - eSize.X * 0.5f,
                        waterPixY + (panelRect.Bottom - waterPixY) * 0.42f),
                    KikasaVaultTheme.TextDim * (breathe * a), 0.9f);
                footer = string.Format(IdleHintFormat.Value,
                    CWRKeySystem.Kikasa_Sink.ToTooltipString(CWRKeySystem.Notbound.Value));
                footerColor = KikasaVaultTheme.TextDim;
            }
            else {
                footer = string.Format(IdleHintFormat.Value,
                    CWRKeySystem.Kikasa_Sink.ToTooltipString(CWRKeySystem.Notbound.Value));
                footerColor = KikasaVaultTheme.TextDim;
            }
            if (!string.IsNullOrEmpty(footer)) {
                Vector2 fSize = font.MeasureString(footer) * 0.85f;
                Utils.DrawBorderString(spriteBatch, footer,
                    new Vector2(panelRect.Center.X - fSize.X * 0.5f, panelRect.Bottom - 34f),
                    footerColor * (MathHelper.Clamp((a - 0.5f) / 0.4f, 0f, 1f)), 0.85f);
            }
        }

        //伞章：伞骨淡线垫底，伞盖粗笔带亮芯，笔序随 reveal 揭示；伞面一段掠光缓巡

        private static void DrawSeal(SpriteBatch sb, Vector2 center, float scale,
            float alpha, float time, float reveal) {
            SvgPath canopy = SvgPathPen.Path(SealCanopy);
            SvgPath frame = SvgPathPen.Path(SealFrame);
            SvgPathPen.Stroke(sb, frame, center, scale, 0f,
                KikasaVaultTheme.TextDim, 1.2f, alpha * 0.85f, 0f, reveal);
            SvgPathPen.Stroke(sb, canopy, center, scale, 0f,
                KikasaVaultTheme.Blood, 2.4f, alpha, 0f, reveal, core: KikasaVaultTheme.Foam);
            if (reveal >= 0.995f) {
                SvgPathPen.StrokeRunner(sb, canopy, center, scale, 0f,
                    KikasaVaultTheme.Foam, 2.6f, alpha * 0.5f, time * 0.07f, 0.10f);
            }
        }
    }
}
