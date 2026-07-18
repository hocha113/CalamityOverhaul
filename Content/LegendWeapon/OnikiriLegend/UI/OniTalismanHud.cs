using CalamityOverhaul.Common;
using CalamityOverhaul.Content.UIs.HudStack;
using InnoVault.UIHandles;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.UI
{
    /// <summary>
    /// 封印札 HUD：手持鬼切时左下角悬一张随风摆的纸札。<br/>
    /// 札上墨批长度 = 总驾驭度；有鬼躁动时下缘焦边燃起鬼火青。点击开阖点鬼簿
    /// </summary>
    internal sealed class OniTalismanHud : UIHandle, ILocalizedModType, IBottomLeftHud
    {
        public string LocalizationCategory => "Legend.OnikiriText";
        public static OniTalismanHud Instance => UIHandleLoader.GetUIHandleOfType<OniTalismanHud>();

        public static LocalizedText HudTitle { get; private set; }
        public static LocalizedText HudHintFormat { get; private set; }
        public static LocalizedText HudDangerLine { get; private set; }
        public static LocalizedText VigorTitle { get; private set; }
        public static LocalizedText VigorValueFormat { get; private set; }

        public override void SetStaticDefaults() {
            HudTitle = this.GetLocalization(nameof(HudTitle), () => "封印札");
            HudHintFormat = this.GetLocalization(nameof(HudHintFormat), () => "{0} 或点击 开阖点鬼簿");
            HudDangerLine = this.GetLocalization(nameof(HudDangerLine), () => "札下起了青焰——有鬼躁动");
            VigorTitle = this.GetLocalization(nameof(VigorTitle), () => "气力");
            VigorValueFormat = this.GetLocalization(nameof(VigorValueFormat), () => "{0} / {1}");
        }

        #region 左下角 HUD 队列接入
        bool IBottomLeftHud.HudStackActive => Active;
        int IBottomLeftHud.HudStackOrder => 0;
        Vector2 IBottomLeftHud.HudStackAnchor => NaturalAnchor;
        float IBottomLeftHud.HudStackTopExtent => 12f;
        float IBottomLeftHud.HudStackBottomExtent => OnikiriUITheme.HudRopeLen + OnikiriUITheme.HudTalismanH + 14f;
        #endregion

        private float appear;
        private bool hover;
        private bool wasHovered;
        private readonly OniUIParticlePool particles = new(40);
        //气力墨脉:札旁横书一笔墨痕作气力计,共用本 HUD 锚点(数据层见 OniVigorData)
        private readonly OniVigorStroke vigor = new();
        private int emberTimer;
        //挂绳 Verlet:锚点随 HUD 队列避让移动时绳会带着滞后甩摆
        private readonly OniRope rope = new(5, OnikiriUITheme.HudRopeLen + 5f);
        //本帧札体姿态(绳末位置+摆角弹簧),Update 算好供 Draw/粒子/命中共用
        private Vector2 stripTopNow;
        private float stripRotNow;
        //札体摆角弹簧状态:长纸条的转动惯性来自这两个量,而非绳末段的瞬时方向
        private float stripRot;
        private float stripRotVel;
        //捏点的札面局部坐标(x=横向,y=沿札向下),悬停进入时记录
        private Vector2 gripLocal;
        //失去悬停后的帧计数:边缘打滑造成的快速重捏不重复响纸声
        private int hoverOffTicks = 60;

        /// <summary>绳结自然锚点(未避让)</summary>
        public static Vector2 NaturalAnchor => new(OnikiriUITheme.HudAnchorOffset.X,
            OnikiriUITheme.UIScreenH + OnikiriUITheme.HudAnchorOffset.Y);

        /// <summary>绳结锚点(经左下角 HUD 队列避让)</summary>
        public static Vector2 Anchor {
            get {
                OniTalismanHud inst = Instance;
                return inst == null ? NaturalAnchor : BottomLeftHudStack.ResolveAnchor(inst);
            }
        }

        private static bool LocalHolding() {
            if (Main.gameMenu || Main.dedServ) {
                return false;
            }
            Item item = Main.LocalPlayer?.GetItem();
            return item != null && item.Alives()
                && (item.type == ModContent.ItemType<OnikiriItem>());
        }

        public override bool Active => LocalHolding() || appear > 0.01f;

        public override void Update() {
            bool holding = LocalHolding();
            appear = MathHelper.Clamp(appear + (holding ? 0.07f : -0.09f), 0f, 1f);
            if (appear <= 0.01f) {
                hover = wasHovered = false;
                hoverOffTicks = Math.Min(hoverOffTicks + 1, 600);
                vigor.Reset();
                return;
            }
            particles.Update();

            bool danger = OniRegistry.InDanger;

            //挂绳推进:危态风更烈;悬停视为被手捏住,风息、阻尼加重,偶发拽动也止住
            Vector2 knot = Anchor;
            float windAmp = danger ? 0.18f : 0.09f;
            if (hover) {
                windAmp *= 0.2f;
            }
            rope.Update(knot, null, GlobalTimer, windAmp, endWeight: 0.5f, damping: hover ? 0.78f : 0.88f);
            if (danger && !hover && Main.rand.NextBool(140)) {
                rope.Nudge(Main.rand.NextFloat(0.6f, 1.3f) * (Main.rand.NextBool() ? 1f : -1f), Main.rand.NextFloat(0.5f));
            }
            //悬停牵引:绳末被拉向"让捏点贴住光标"的位置,绳长约束自然给出被拽住的弹性
            if (hover) {
                Vector2 gripSide = stripRot.ToRotationVector2();
                Vector2 gripDown = (MathHelper.PiOver2 + stripRot).ToRotationVector2();
                rope.PullEnd(MousePosition - gripSide * gripLocal.X - gripDown * gripLocal.Y, 0.22f);
            }
            stripTopNow = rope.End;

            //札体摆角:平时以整绳方向为基线做弹簧跟随(末段仅数像素,瞬时方向的噪声
            //会被 112px 札身放大成大幅甩摆);悬停时目标归零,入捏的一下"扶正"就是持握感
            float targetRot, stiffness, rotDamp;
            if (hover) {
                targetRot = 0f;
                stiffness = 0.16f;
                rotDamp = 0.70f;
            }
            else {
                targetRot = ((rope.End - knot).SafeNormalize(Vector2.UnitY).ToRotation() - MathHelper.PiOver2) * 1.15f;
                stiffness = 0.12f;
                rotDamp = 0.86f;
            }
            stripRotVel += (targetRot - stripRot) * stiffness;
            stripRotVel *= rotDamp;
            stripRot = MathHelper.Clamp(stripRot + stripRotVel, -0.55f, 0.55f);
            stripRotNow = stripRot;
            if (danger) {
                stripRotNow += (float)Math.Sin(GlobalTimer * 11f) * 0.010f;
            }

            //命中:光标变换进札面局部空间,与绘制同一姿态的 OBB(旧的轴对齐外包在摆角大时会漏判)
            float w = OnikiriUITheme.HudTalismanW;
            float h = OnikiriUITheme.HudTalismanH;
            Vector2 side = stripRotNow.ToRotationVector2();
            Vector2 down = (MathHelper.PiOver2 + stripRotNow).ToRotationVector2();
            Vector2 rel = MousePosition - stripTopNow;
            float localX = Vector2.Dot(rel, side);
            float localY = Vector2.Dot(rel, down);
            bool inStrip = localX >= -w * 0.5f - 5f && localX <= w * 0.5f + 5f
                && localY >= -3f && localY <= h + 5f;

            //框架命中盒/绘制位:旋转矩形的轴对齐外包
            Vector2 cornerA = stripTopNow - side * (w * 0.5f);
            Vector2 cornerB = stripTopNow + side * (w * 0.5f);
            Vector2 cornerC = cornerA + down * h;
            Vector2 cornerD = cornerB + down * h;
            float minX = MathF.Min(MathF.Min(cornerA.X, cornerB.X), MathF.Min(cornerC.X, cornerD.X));
            float maxX = MathF.Max(MathF.Max(cornerA.X, cornerB.X), MathF.Max(cornerC.X, cornerD.X));
            float minY = MathF.Min(MathF.Min(cornerA.Y, cornerB.Y), MathF.Min(cornerC.Y, cornerD.Y));
            float maxY = MathF.Max(MathF.Max(cornerA.Y, cornerB.Y), MathF.Max(cornerC.Y, cornerD.Y));
            DrawPosition = new Vector2(minX, minY);
            Size = new Vector2(maxX - minX, maxY - minY);
            UIHitBox = new Rectangle((int)minX, (int)minY, (int)(maxX - minX), (int)(maxY - minY));

            float registerOpen = OniRegisterUI.Instance?.OpenProgress ?? 0f;
            float riteOpen = OniEngraveRiteUI.Instance?.OpenProgress ?? 0f;
            bool uiCovered = registerOpen > 0.4f || riteOpen > 0.4f;
            //气力墨脉推进:点鬼簿开卷时也继续呼吸,只是不受理悬浮
            vigor.Update(player, knot, !uiCovered && appear > 0.5f, MousePosition);
            if (uiCovered) {
                hover = wasHovered = false;
                hoverOffTicks = Math.Min(hoverOffTicks + 1, 600);
                return;
            }

            hover = inStrip;
            //初捏:记下捏点并给一声很轻的纸响,不再推绳(推开会破坏"拿住"的感觉)
            if (hover && !wasHovered) {
                gripLocal = new Vector2(MathHelper.Clamp(localX, -w * 0.5f, w * 0.5f), MathHelper.Clamp(localY, 0f, h));
                if (hoverOffTicks > 8) {
                    SoundEngine.PlaySound(SoundID.MenuTick with { Volume = 0.3f, Pitch = 0.35f });
                }
            }
            hoverOffTicks = hover ? 0 : Math.Min(hoverOffTicks + 1, 600);
            wasHovered = hover;
            if (hover) {
                player.mouseInterface = true;
                if (keyLeftPressState == KeyPressState.Pressed) {
                    SoundEngine.PlaySound(CWRSound.ButtonZero with { Volume = 0.6f });
                    OniRegisterUI.Instance?.Toggle();
                }
            }

            //危态:札脚剥落鬼火余烬
            if (danger) {
                emberTimer++;
                if (emberTimer >= 26) {
                    emberTimer = 0;
                    Vector2 stripBottom = stripTopNow + (MathHelper.PiOver2 + stripRotNow).ToRotationVector2()
                        * OnikiriUITheme.HudTalismanH;
                    particles.SpawnEmber(stripBottom + Main.rand.NextVector2Circular(OnikiriUITheme.HudTalismanW * 0.4f, 2f));
                }
            }
        }

        public override void Draw(SpriteBatch sb) {
            if (appear <= 0.01f) {
                return;
            }
            float registerOpen = OniRegisterUI.Instance?.OpenProgress ?? 0f;
            float a = appear * (1f - registerOpen * 0.7f);
            if (a <= 0.01f) {
                return;
            }

            Texture2D pixel = VaultAsset.placeholder2.Value;
            Rectangle src = new(0, 0, 1, 1);
            Vector2 knot = Anchor;
            float rot = stripRotNow;
            //纸札顶部中点与姿态由 Verlet 绳末段决定
            Vector2 stripTop = stripTopNow;
            Vector2 down = (MathHelper.PiOver2 + rot).ToRotationVector2();
            float W = OnikiriUITheme.HudTalismanW;
            float H = OnikiriUITheme.HudTalismanH;

            //挂绳:上端渐隐(挂在看不见的地方),结点一枚朱菱,绳体为 Verlet 折线
            OniBrush.DrawGradientLine(sb, knot - new Vector2(0f, 26f), knot, OnikiriUITheme.Dark * 0f, OnikiriUITheme.Deep * (a * 0.8f), 1.3f);
            rope.Draw(sb, OnikiriUITheme.Deep * 0.88f, OnikiriUITheme.Deep * 0.62f, 1.3f, a);
            sb.Draw(pixel, knot, src, OnikiriUITheme.Seal * a, MathHelper.PiOver4 + rot * 0.4f, new Vector2(0.5f), new Vector2(4.6f), SpriteEffects.None, 0f);

            //札体:纸条质感(三段明暗/折角/压边/缓移光泽),危态时改走焚烧 shader
            Vector2 side = rot.ToRotationVector2();
            float mastery = MathHelper.Clamp(OniRegistry.TotalMastery, 0f, 1f);
            bool danger = OniRegistry.InDanger;
            bool paperByShader = danger && OniPaperBurnDraw.Available;
            if (paperByShader) {
                //焚烧量吃"距离失控有多近":总驾驭越低烧得越高。
                //只许舔掉下缘一小截——札是警示牌,不能被火吃掉存在感
                float burn = MathHelper.Clamp(0.09f + (1f - mastery) * 0.17f, 0f, 0.30f);
                OniPaperBurnDraw.Draw(sb, stripTop, rot, new Vector2(W, H), a * (hover ? 1.05f : 0.96f), burn, GlobalTimer);
            }
            else {
                OniBrush.DrawPaperStrip(sb, stripTop, rot, new Vector2(W, H), a * (hover ? 1.05f : 0.96f), GlobalTimer * 0.11f);
            }

            //札首小朱印
            OniBrush.DrawSealGlyph(sb, stripTop + down * 16f, 9.5f, a * 0.95f, rot);

            //墨批:自印下垂书一笔,长度=总驾驭度
            if (mastery > 0.02f) {
                Vector2 strokeStart = stripTop + down * 29f;
                Vector2 strokeEnd = stripTop + down * (29f + (H - 42f) * mastery);
                OniBrush.DrawTaperedSlash(sb, strokeStart, strokeEnd, 3.8f, 0.9f, a * 0.92f);
            }

            //危态:焚烧 shader 缺席时退回逐列手绘焦边
            if (danger && !paperByShader) {
                DrawCharredHem(sb, stripTop, down, side, W, H, a);
            }

            //气力墨脉:定在锚侧不随札摆,墨丝自札边垂下把两者缝在一起
            Vector2 vigorAttach = stripTop + down * (H * 0.62f) + side * (W * 0.5f - 2f);
            vigor.Draw(sb, a, vigorAttach, GlobalTimer, hover);

            particles.Draw(sb, a);

            //悬浮说明
            if (hover) {
                DrawHoverPanel(sb, a);
            }
        }

        /// <summary>札脚焦边:炭黑参差 + 数簇暖色火焰,跟随摆角</summary>
        private void DrawCharredHem(SpriteBatch sb, Vector2 stripTop, Vector2 down, Vector2 side, float w, float h, float a) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            Rectangle src = new(0, 0, 1, 1);
            float rot = stripRotNow;
            const int Cols = 10;
            float step = w / Cols;
            for (int i = 0; i < Cols; i++) {
                float x = -w * 0.5f + (i + 0.5f) * step;
                float hash = OniBrush.Hash01(i * 97 + 11);
                float charH = 3f + hash * 5f;
                Vector2 pos = stripTop + down * (h - charH * 0.5f) + side * x;
                sb.Draw(pixel, pos, src, OnikiriUITheme.Ink * (a * 0.92f), rot, new Vector2(0.5f), new Vector2(step + 0.6f, charH), SpriteEffects.None, 0f);
                sb.Draw(pixel, pos - down * (charH * 0.5f + 1f), src, OnikiriUITheme.Dark * (a * 0.7f), rot, new Vector2(0.5f), new Vector2(step + 0.6f, 2f), SpriteEffects.None, 0f);

                if (hash > 0.55f) {
                    float flick = (float)Math.Sin(GlobalTimer * (3.1f + hash * 2.6f) + i * 1.9f) * 0.5f + 0.5f;
                    float flameH = (3f + hash * 4.5f) * (0.4f + flick * 0.6f);
                    Vector2 flamePos = stripTop + down * (h - charH) + side * x;
                    sb.Draw(pixel, flamePos, src, OnikiriUITheme.BurnDim * (a * 0.55f * flick), rot, new Vector2(0.5f, 1f), new Vector2(step - 0.5f, flameH), SpriteEffects.None, 0f);
                    sb.Draw(pixel, flamePos, src, OnikiriUITheme.BurnHot * (a * 0.72f * flick), rot, new Vector2(0.5f, 1f), new Vector2(1.4f, flameH * 0.6f), SpriteEffects.None, 0f);
                }
            }
        }

        /// <summary>悬浮说明:小裱墨牌,题名/开簿键位/危态告警</summary>
        private void DrawHoverPanel(SpriteBatch sb, float a) {
            DynamicSpriteFont font = FontAssets.MouseText.Value;
            string keyName = CWRKeySystem.Legend_UIControl.ToTooltipString(CWRKeySystem.Notbound.Value);
            string title = HudTitle.Value;
            string hint = string.Format(HudHintFormat.Value, keyName);
            bool danger = OniRegistry.InDanger;
            string dangerLine = danger ? HudDangerLine.Value : null;

            float w = Math.Max(font.MeasureString(title).X * 0.82f, font.MeasureString(hint).X * 0.7f);
            if (dangerLine != null) {
                w = Math.Max(w, font.MeasureString(dangerLine).X * 0.7f);
            }
            float h = 44f + (dangerLine != null ? 18f : 0f);
            Vector2 mouse = MousePosition;
            Rectangle panel = new((int)mouse.X + 18, (int)mouse.Y - 8, (int)w + 22, (int)h);
            //不出屏
            if (panel.Right > OnikiriUITheme.UIScreenW - 8f) {
                panel.X = (int)(mouse.X - panel.Width - 14f);
            }

            Texture2D pixel = VaultAsset.placeholder2.Value;
            Rectangle src = new(0, 0, 1, 1);
            sb.Draw(pixel, new Rectangle(panel.X + 2, panel.Y + 3, panel.Width, panel.Height), src, new Color(8, 2, 5) * (a * 0.5f));
            sb.Draw(pixel, panel, src, OnikiriUITheme.Ink * (a * 0.95f));
            OniBrush.DrawTaperedSlash(sb, new Vector2(panel.X + 4f, panel.Y + 22f), new Vector2(panel.Right - 4f, panel.Y + 21f), 1.4f, 0.8f, a * 0.7f);

            Utils.DrawBorderString(sb, title, new Vector2(panel.X + 10f, panel.Y + 4f), OnikiriUITheme.HotWhite * a, 0.82f);
            Utils.DrawBorderString(sb, hint, new Vector2(panel.X + 10f, panel.Y + 26f), OnikiriUITheme.TextDim * a, 0.7f);
            if (dangerLine != null) {
                Utils.DrawBorderString(sb, dangerLine, new Vector2(panel.X + 10f, panel.Y + 44f), OnikiriUITheme.GhostFire * (a * 0.9f), 0.7f);
            }
        }
    }
}
