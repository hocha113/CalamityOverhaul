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
        private readonly OniUIParticlePool particles = new(40);
        private int emberTimer;

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

        /// <summary>纸札摆角(钟摆),危态时叠一层高频细颤</summary>
        private float SwayAngle {
            get {
                float sway = (float)Math.Sin(GlobalTimer * 1.35f) * 0.055f
                    + (float)Math.Sin(GlobalTimer * 0.53f + 1.7f) * 0.03f;
                if (OniRegistry.InDanger) {
                    sway += (float)Math.Sin(GlobalTimer * 11f) * 0.012f;
                }
                return sway;
            }
        }

        public override void Update() {
            bool holding = LocalHolding();
            appear = MathHelper.Clamp(appear + (holding ? 0.07f : -0.09f), 0f, 1f);
            if (appear <= 0.01f) {
                hover = false;
                return;
            }
            particles.Update();

            //命中盒:纸札的轴对齐外包(摆角小,近似矩形足够)
            Vector2 knot = Anchor;
            Vector2 stripTop = knot + new Vector2(0f, OnikiriUITheme.HudRopeLen);
            Rectangle strip = new((int)(stripTop.X - OnikiriUITheme.HudTalismanW * 0.5f - 4f), (int)stripTop.Y,
                (int)OnikiriUITheme.HudTalismanW + 8, (int)OnikiriUITheme.HudTalismanH + 4);
            DrawPosition = strip.Location.ToVector2();
            Size = strip.Size();
            UIHitBox = strip;

            float registerOpen = OniRegisterUI.Instance?.OpenProgress ?? 0f;
            float riteOpen = OniEngraveRiteUI.Instance?.OpenProgress ?? 0f;
            if (registerOpen > 0.4f || riteOpen > 0.4f) {
                hover = false;
                return;
            }

            hover = strip.Contains(MousePosition.ToPoint());
            if (hover) {
                player.mouseInterface = true;
                if (keyLeftPressState == KeyPressState.Pressed) {
                    SoundEngine.PlaySound(CWRSound.ButtonZero with { Volume = 0.6f });
                    OniRegisterUI.Instance?.Toggle();
                }
            }

            //危态:札脚剥落鬼火余烬
            if (OniRegistry.InDanger) {
                emberTimer++;
                if (emberTimer >= 26) {
                    emberTimer = 0;
                    Vector2 stripBottom = stripTop + SwayAngle.ToRotationVector2().RotatedBy(MathHelper.PiOver2)
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
            float rot = SwayAngle;
            //纸札顶部中点(挂在绳下),札体沿摆角向下
            Vector2 stripTop = knot + new Vector2(0f, OnikiriUITheme.HudRopeLen);
            Vector2 down = (MathHelper.PiOver2 + rot).ToRotationVector2();
            float W = OnikiriUITheme.HudTalismanW;
            float H = OnikiriUITheme.HudTalismanH;

            //挂绳:上端渐隐(挂在看不见的地方),结点一枚朱菱
            OniBrush.DrawGradientLine(sb, knot - new Vector2(0f, 26f), knot, OnikiriUITheme.Dark * 0f, OnikiriUITheme.Deep * (a * 0.8f), 1.3f);
            OniBrush.DrawGradientLine(sb, knot, stripTop, OnikiriUITheme.Deep * (a * 0.85f), OnikiriUITheme.Deep * (a * 0.6f), 1.3f);
            sb.Draw(pixel, knot, src, OnikiriUITheme.Seal * a, MathHelper.PiOver4 + rot * 0.4f, new Vector2(0.5f), new Vector2(4.6f), SpriteEffects.None, 0f);

            //札体:阴影/纸面/上折角/边线(全部绕 stripTop 随摆角旋转)
            Vector2 stripCenter = stripTop + down * (H * 0.5f);
            sb.Draw(pixel, stripCenter + new Vector2(1.5f, 2f), src, OnikiriUITheme.Dark * (a * 0.5f), rot, new Vector2(0.5f), new Vector2(W, H), SpriteEffects.None, 0f);
            sb.Draw(pixel, stripCenter, src, OnikiriUITheme.Paper * (a * (hover ? 0.98f : 0.9f)), rot, new Vector2(0.5f), new Vector2(W, H), SpriteEffects.None, 0f);
            sb.Draw(pixel, stripTop + down * 3f, src, OnikiriUITheme.TextDim * (a * 0.5f), rot, new Vector2(0.5f, 0.5f), new Vector2(W, 6f), SpriteEffects.None, 0f);
            //左右侧沿各一线深红压边
            Vector2 side = rot.ToRotationVector2();
            sb.Draw(pixel, stripCenter - side * (W * 0.5f - 1f), src, OnikiriUITheme.Deep * (a * 0.5f), rot + MathHelper.PiOver2, new Vector2(0.5f), new Vector2(H, 1.4f), SpriteEffects.None, 0f);
            sb.Draw(pixel, stripCenter + side * (W * 0.5f - 1f), src, OnikiriUITheme.Deep * (a * 0.5f), rot + MathHelper.PiOver2, new Vector2(0.5f), new Vector2(H, 1.4f), SpriteEffects.None, 0f);

            //札首小朱印
            OniBrush.DrawSealGlyph(sb, stripTop + down * 15f, 8.5f, a * 0.95f, rot);

            //墨批:自印下垂书一笔,长度=总驾驭度;危态时笔尾渗绯
            float mastery = MathHelper.Clamp(OniRegistry.TotalMastery, 0f, 1f);
            bool danger = OniRegistry.InDanger;
            if (mastery > 0.02f) {
                Vector2 strokeStart = stripTop + down * 26f;
                Vector2 strokeEnd = stripTop + down * (26f + (H - 36f) * mastery);
                OniBrush.DrawTaperedSlash(sb, strokeStart, strokeEnd, 3.4f, 0.8f, a * 0.92f);
            }

            //危态:札脚焦边 + 青焰(随札体旋转,逐列手绘)
            if (danger) {
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
            float rot = SwayAngle;
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
