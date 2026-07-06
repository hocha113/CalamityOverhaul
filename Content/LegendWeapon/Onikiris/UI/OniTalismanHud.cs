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

namespace CalamityOverhaul.Content.LegendWeapon.Onikiris.UI
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

        public override void SetStaticDefaults() {
            HudTitle = this.GetLocalization(nameof(HudTitle), () => "封印札");
            HudHintFormat = this.GetLocalization(nameof(HudHintFormat), () => "{0} 或点击 开阖点鬼簿");
            HudDangerLine = this.GetLocalization(nameof(HudDangerLine), () => "札下起了青焰——有鬼躁动");
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
        private int emberTimer;
        //挂绳 Verlet:锚点随 HUD 队列避让移动时绳会带着滞后甩摆
        private readonly OniRope rope = new(5, OnikiriUITheme.HudRopeLen + 5f);
        //本帧札体姿态(由绳末段决定),Update 算好供 Draw/粒子共用
        private Vector2 stripTopNow;
        private float stripRotNow;

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
                hover = false;
                return;
            }
            particles.Update();

            bool danger = OniRegistry.InDanger;

            //挂绳推进:危态风更烈,偶尔整根绳被"什么东西"拽一下
            Vector2 knot = Anchor;
            rope.Update(knot, null, GlobalTimer, danger ? 0.45f : 0.24f, endWeight: 0.5f);
            if (danger && Main.rand.NextBool(140)) {
                rope.Nudge(Main.rand.NextFloat(1.0f, 2.2f) * (Main.rand.NextBool() ? 1f : -1f), Main.rand.NextFloat(0.5f));
            }
            stripTopNow = rope.End;
            stripRotNow = rope.EndRotation - MathHelper.PiOver2;
            if (danger) {
                stripRotNow += (float)Math.Sin(GlobalTimer * 11f) * 0.010f;
            }

            //命中盒:纸札的轴对齐外包(摆角小,近似矩形足够)
            Rectangle strip = new((int)(stripTopNow.X - OnikiriUITheme.HudTalismanW * 0.5f - 4f), (int)stripTopNow.Y,
                (int)OnikiriUITheme.HudTalismanW + 8, (int)OnikiriUITheme.HudTalismanH + 4);
            DrawPosition = strip.Location.ToVector2();
            Size = strip.Size();
            UIHitBox = strip;

            float registerOpen = OniRegisterUI.Instance?.OpenProgress ?? 0f;
            float riteOpen = OniEngraveRiteUI.Instance?.OpenProgress ?? 0f;
            if (registerOpen > 0.4f || riteOpen > 0.4f) {
                hover = wasHovered = false;
                return;
            }

            hover = strip.Contains(MousePosition.ToPoint());
            //拂过纸札:绳吃一记小冲量
            if (hover && !wasHovered) {
                rope.Nudge(Main.rand.NextFloat(0.6f, 1.2f) * (Main.rand.NextBool() ? 1f : -1f));
            }
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
            particles.Draw(sb, a);

            //悬浮说明
            if (hover) {
                DrawHoverPanel(sb, a);
            }
        }

        /// <summary>札脚焦边:炭黑参差 + 数簇青焰,跟随摆角</summary>
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
                    sb.Draw(pixel, flamePos, src, OnikiriUITheme.GhostDim * (a * 0.55f * flick), rot, new Vector2(0.5f, 1f), new Vector2(step - 0.5f, flameH), SpriteEffects.None, 0f);
                    sb.Draw(pixel, flamePos, src, OnikiriUITheme.GhostFire * (a * 0.72f * flick), rot, new Vector2(0.5f, 1f), new Vector2(1.4f, flameH * 0.6f), SpriteEffects.None, 0f);
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
