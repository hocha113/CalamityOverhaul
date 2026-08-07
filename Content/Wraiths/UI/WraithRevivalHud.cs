using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Wraiths.Core;
using CalamityOverhaul.Content.Wraiths.Deaths;
using CalamityOverhaul.Content.Wraiths.Runtime;
using InnoVault.UIHandles;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Graphics;
using System;
using Terraria;
using Terraria.GameContent;
using Terraria.GameInput;

namespace CalamityOverhaul.Content.Wraiths.UI
{
    /// <summary>
    /// 复苏进度 HUD，屏幕正上方居中，不占用左下角鬼切 HUD 簇。<br/>
    /// 情境显示：数值变化后约 3 秒淡出；复苏进入危险区（≥0.7）常显直至跌出；
    /// "下一次役使将满格"时强制显示预警；夺身演出期间隐藏。<br/>
    /// 进度条由 WraithRevivalHud shader 绘制（墨色→血色，前沿撕裂，危险脉冲）
    /// </summary>
    internal sealed class WraithRevivalHud : UIHandle
    {
        public static WraithRevivalHud Instance
            => UIHandleLoader.GetUIHandleOfType<WraithRevivalHud>();

        private const float BarW = 210f;
        private const float BarH = 16f;
        //数值变化后的展示窗口（3 秒）
        private const int ChangeShowTicks = 180;
        //appear=0 → 完全收进屏幕上方；appear=1 → 完全滑入
        private float appear;

        private static WraithPlayer LocalWraith {
            get {
                if (Main.gameMenu || Main.dedServ) { return null; }
                Player p = Main.LocalPlayer;
                return p != null && p.active ? p.GetModPlayer<WraithPlayer>() : null;
            }
        }

        private static bool CanShow(WraithPlayer state)
            => state != null && WraithAbilityService.IsOnikiriHeld(Main.LocalPlayer)
                && !string.IsNullOrEmpty(state.EquippedWraithKey)
                && !WraithRevivalDeath.IsSeized(Main.LocalPlayer);

        /// <summary>下一次役使就会满格夺身</summary>
        private static bool NextUseFills(WraithPlayer state) {
            if (!WraithRegistry.TryGetUsable(state.EquippedWraithKey, out WraithDefinition definition)
                || definition.RevivalCost <= 0f) {
                return false;
            }
            return state.EquippedRevival + definition.RevivalCost >= 1f - 0.0001f;
        }

        private static bool WantShow(WraithPlayer state) {
            float value = state.EquippedRevival;
            if (value <= 0.005f) {
                return false;
            }
            return state.RevivalChangedTimer < ChangeShowTicks
                || value >= WraithPlayer.RevivalDangerLine
                || NextUseFills(state);
        }

        public override bool Active {
            get {
                WraithPlayer wp = LocalWraith;
                return CanShow(wp) && (WantShow(wp) || appear > 0.01f);
            }
        }

        public override void Update() {
            WraithPlayer wp = LocalWraith;
            if (!CanShow(wp)) {
                appear = 0f;
                return;
            }
            float target = WantShow(wp) ? 1f : 0f;
            appear += (target - appear) * (target > appear ? 0.12f : 0.07f);
            appear = MathHelper.Clamp(appear, 0f, 1f);
        }

        public override void Draw(SpriteBatch sb) {
            if (appear < 0.01f) { return; }
            WraithPlayer wp = LocalWraith;
            if (wp == null) { return; }

            Effect effect = EffectLoader.WraithRevivalHud?.Value;
            if (effect == null) { return; }

            float revival = wp.EquippedRevival;
            bool brink = NextUseFills(wp);
            float screenW = PlayerInput.RealScreenWidth / Main.UIScale;
            float barX = (screenW - BarW) * 0.5f;
            float barY = -BarH + appear * (BarH + 14f) + 100;

            float danger = MathHelper.Clamp(
                (revival - WraithPlayer.RevivalDangerLine) / (1f - WraithPlayer.RevivalDangerLine),
                0f, 1f);
            float pulse = 0.5f + 0.5f * MathF.Sin(GlobalTimer * 9.8f);
            //满格预警：脉冲不落谷，进度条持续泛血
            float dangerPulse = brink ? 0.55f + 0.45f * pulse : danger * pulse;

            effect.Parameters["transformMatrix"]?.SetValue(Main.UIScaleMatrix);
            effect.Parameters["uTime"]?.SetValue(GlobalTimer);
            effect.Parameters["uProgress"]?.SetValue(revival);
            effect.Parameters["uDangerPulse"]?.SetValue(dangerPulse);
            effect.Parameters["uColInk"]?.SetValue(new Vector3(0.07f, 0.047f, 0.086f));
            effect.Parameters["uColBlood"]?.SetValue(new Vector3(0.63f, 0.078f, 0.118f));
            effect.Parameters["uNoiseTex"]?.SetValue(CWRAsset.NoiseSoft01?.Value);

            var destRect = new Rectangle((int)barX, (int)barY, (int)BarW, (int)BarH);
            sb.End();
            sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, effect, Main.UIScaleMatrix);
            sb.Draw(TextureAssets.MagicPixel.Value, destRect, Color.White);
            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, RasterizerState.CullCounterClockwise, null, Main.UIScaleMatrix);

            DynamicSpriteFont font = FontAssets.MouseText.Value;
            string ghostName = WraithRegistry.TryGetUsable(wp.EquippedWraithKey,
                out WraithDefinition definition) ? definition.DisplayName.Value : string.Empty;
            string label = string.IsNullOrEmpty(ghostName)
                ? $"{(int)(revival * 100)}%"
                : $"{ghostName} · {(int)(revival * 100)}%";
            Vector2 labelSize = font.MeasureString(label) * 0.52f;
            Vector2 labelPos = new((screenW - labelSize.X) * 0.5f, barY - labelSize.Y - 2f);
            Color labelColor = brink
                ? Color.Lerp(new Color(168, 42, 55), new Color(232, 78, 66), pulse)
                : new Color(168, 42, 55);
            Utils.DrawBorderString(sb, label, labelPos, labelColor * appear, 0.52f);

            //满格预警：条下悬一行血字
            if (brink) {
                string warn = WraithSystemText.RevivalHudBrink.Value;
                Vector2 warnSize = font.MeasureString(warn) * 0.58f;
                Vector2 warnPos = new((screenW - warnSize.X) * 0.5f, barY + BarH + 4f);
                Utils.DrawBorderString(sb, warn, warnPos,
                    Color.Lerp(new Color(190, 46, 52), new Color(255, 96, 74), pulse) * appear, 0.58f);
            }
        }
    }
}
