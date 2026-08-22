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
    /// 结印几只就竖排几条，整簇一起进退：任一只满足条件即滑入
    /// 数值刚变化（约 3 秒）、进入危险区（≥0.7）、或下一次役使就会满格；
    /// 夺身演出期间隐藏。<br/>
    /// 进度条由 WraithRevivalHud shader 绘制（墨色→血色，前沿撕裂，危险脉冲）
    /// </summary>
    internal sealed class WraithRevivalHud : UIHandle
    {
        public static WraithRevivalHud Instance
            => UIHandleLoader.GetUIHandleOfType<WraithRevivalHud>();

        private const float BarW = 210f;
        private const float BarH = 14f;
        /// <summary>一行（名讳 + 条）的纵距</summary>
        private const float RowPitch = 30f;
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
                && state.EquippedCount > 0
                && !WraithRevivalDeath.IsSeized(Main.LocalPlayer);

        /// <summary>这只鬼下一次役使就会满格夺身</summary>
        private static bool NextUseFills(WraithPlayer state, string key) {
            if (!WraithRegistry.TryGetUsable(key, out WraithDefinition definition)
                || definition.RevivalCost <= 0f) {
                return false;
            }
            return state.GetRevival(key) + definition.RevivalCost >= 1f - 0.0001f;
        }

        /// <summary>整簇一起进退：任一只值得报，就把盘上的都摊开给玩家比。</summary>
        private static bool WantShow(WraithPlayer state) {
            bool anyProgress = false;
            bool urgent = false;
            foreach (string key in state.EquippedKeys) {
                float value = state.GetRevival(key);
                if (value > 0.005f) {
                    anyProgress = true;
                }
                if (value >= WraithPlayer.RevivalDangerLine || NextUseFills(state, key)) {
                    urgent = true;
                }
            }
            if (!anyProgress) {
                return false;
            }
            return urgent || state.RevivalChangedTimer < ChangeShowTicks;
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

            float screenW = PlayerInput.RealScreenWidth / Main.UIScale;
            float barX = (screenW - BarW) * 0.5f;
            //整簇自屏顶滑入，行距固定，条数变化不挪已在位的行
            float stackTop = -RowPitch + appear * (RowPitch + 14f) + 100f;
            float pulse = 0.5f + 0.5f * MathF.Sin(GlobalTimer * 9.8f);
            DynamicSpriteFont font = FontAssets.MouseText.Value;

            int row = 0;
            bool anyBrink = false;
            float lastBarBottom = stackTop;
            for (int slot = 0; slot < WraithPlayer.SlotCount; slot++) {
                string key = wp.SlotKey(slot);
                if (string.IsNullOrEmpty(key)
                    || !WraithRegistry.TryGetUsable(key, out WraithDefinition definition)) {
                    continue;
                }
                float revival = wp.GetRevival(key);
                bool brink = NextUseFills(wp, key);
                anyBrink |= brink;
                float barY = stackTop + row * RowPitch;
                lastBarBottom = barY + BarH;
                row++;

                DrawBar(sb, effect, barX, barY, revival, brink, pulse);

                string label = $"{definition.DisplayName.Value} · {(int)(revival * 100)}%";
                Vector2 labelSize = font.MeasureString(label) * 0.52f;
                Vector2 labelPos = new((screenW - labelSize.X) * 0.5f, barY - labelSize.Y - 1f);
                Color labelColor = brink
                    ? Color.Lerp(new Color(168, 42, 55), new Color(232, 78, 66), pulse)
                    : new Color(168, 42, 55);
                Utils.DrawBorderString(sb, label, labelPos, labelColor * appear, 0.52f);
            }

            //满格预警：整簇底下悬一行血字，几只将醒都只报一次
            if (anyBrink) {
                string warn = WraithSystemText.RevivalHudBrink.Value;
                Vector2 warnSize = font.MeasureString(warn) * 0.58f;
                Vector2 warnPos = new((screenW - warnSize.X) * 0.5f, lastBarBottom + 4f);
                Utils.DrawBorderString(sb, warn, warnPos,
                    Color.Lerp(new Color(190, 46, 52), new Color(255, 96, 74), pulse) * appear, 0.58f);
            }
        }

        /// <summary>一条复苏条；每条自带一次批切换，shader 参数逐条不同。</summary>
        private void DrawBar(SpriteBatch sb, Effect effect, float barX, float barY,
            float revival, bool brink, float pulse) {
            float danger = MathHelper.Clamp(
                (revival - WraithPlayer.RevivalDangerLine) / (1f - WraithPlayer.RevivalDangerLine),
                0f, 1f);
            //满格预警：脉冲不落谷，进度条持续泛血
            float dangerPulse = brink ? 0.55f + 0.45f * pulse : danger * pulse;

            effect.Parameters["transformMatrix"]?.SetValue(Main.UIScaleMatrix);
            effect.Parameters["uTime"]?.SetValue(GlobalTimer);
            effect.Parameters["uProgress"]?.SetValue(revival);
            effect.Parameters["uDangerPulse"]?.SetValue(dangerPulse);
            effect.Parameters["uColInk"]?.SetValue(new Vector3(0.07f, 0.047f, 0.086f));
            effect.Parameters["uColBlood"]?.SetValue(new Vector3(0.63f, 0.078f, 0.118f));
            effect.Parameters["uNoiseTex"]?.SetValue(CWRAsset.NoiseSoft01?.Value);

            Rectangle destRect = new((int)barX, (int)barY, (int)BarW, (int)BarH);
            sb.End();
            sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, effect, Main.UIScaleMatrix);
            sb.Draw(TextureAssets.MagicPixel.Value, destRect, Color.White);
            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, RasterizerState.CullCounterClockwise, null, Main.UIScaleMatrix);
        }
    }
}
