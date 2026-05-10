using CalamityOverhaul.Common;
using CalamityOverhaul.Content.ADV.EntrustManager;
using CalamityOverhaul.Content.LegendWeapon.SHPCLegend;
using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.UI;
using CalamityOverhaul.Content.QuestLogs;
using CalamityOverhaul.Content.RAMSystems;
using InnoVault.UIHandles;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.Cyberwares.Implementation.CstmVisualEyes
{
    /// <summary>
    /// CSTM 视像义眼专属 HUD
    /// <br/>仅在玩家装备本义眼且<b>未</b>手持 SHPC 时显示，避免与 <see cref="SHPCUI"/> 的左下能量核心叠绘
    /// <br/>渲染内容刻意精简：
    /// <list type="bullet">
    ///   <item>左下小型"义眼核心"图标，作为该 RAM 弧条的来源标识</item>
    ///   <item>复用 <see cref="SHPCRenderer.DrawRAMBar"/> 绘制 RAM 弧条，保持与 SHPC HUD 同款视觉</item>
    /// </list>
    /// 自身不存任何外部状态，仅缓存一份用于平滑插值的本地显示值
    /// </summary>
    internal class CstmVisualEyeHUD : UIHandle
    {
        public static CstmVisualEyeHUD Instance => UIHandleLoader.GetUIHandleOfType<CstmVisualEyeHUD>();

        #region 显隐判定

        public override bool Active {
            get {
                Player p = Main.LocalPlayer;
                if (p == null || !p.active || p.dead) {
                    return false;
                }
                //没装备本义眼直接不显示，自我判定避免外部耦合
                if (CstmVisualEye.GetEquipped(p) == null) {
                    return false;
                }
                //手持 SHPC 时由 SHPCUI 接管左下角，本 HUD 主动让位避免双弧重叠
                Item held = p.GetItem();
                if (held != null && !held.IsAir && held.type == SHPCOverride.ID) {
                    return false;
                }
                //避让全屏 UI（与 SHPCUI 一致的回避策略）
                if (QuestLog.Instance?.visible == true) {
                    return false;
                }
                if (QuestManagerUI.Instance?.IsOpen == true) {
                    return false;
                }
                return true;
            }
        }

        #endregion

        #region 状态

        //全局时间，用于扫光与呼吸节奏，单位秒
        private float time;
        //平滑跟随 RAM 当前值，避免数值跳变带来的刺眼闪烁
        private float ramDisplayValue;

        #endregion

        #region 更新

        public override void Update() {
            time += 1f / 60f;

            //RAM 显示值平滑过渡，规则与 SHPCUI 保持一致以方便玩家在两个 HUD 间切换时不感到割裂
            ramDisplayValue = MathHelper.Lerp(ramDisplayValue, RamSystem.CurrentRam, 0.12f);
            if (MathF.Abs(ramDisplayValue - RamSystem.CurrentRam) < 0.01f) {
                ramDisplayValue = RamSystem.CurrentRam;
            }
        }

        #endregion

        #region 绘制

        public override void Draw(SpriteBatch sb) {
            Texture2D px = VaultAsset.placeholder2.Value;
            if (px == null) {
                return;
            }

            Vector2 corePos = GetCorePosition();
            const float globalAlpha = 1f;

            //先绘制小型义眼核心，作为 RAM 弧条的来源标识；置于弧条之下避免遮盖
            DrawEyeCore(sb, px, corePos, time, globalAlpha);

            //复用 SHPC HUD 同款 RAM 弧形条
            SHPCRenderer.DrawRAMBar(sb, px, corePos,
                ramDisplayValue, RamSystem.MaxRam, time, globalAlpha);
        }

        /// <summary>
        /// 与 SHPCUI 完全一致的核心位置，保证两套 HUD 在同一位置无缝切换
        /// </summary>
        private static Vector2 GetCorePosition() => new(96f, Main.screenHeight - 96f);

        /// <summary>
        /// 程序化绘制小型"义眼"核心：外环 + 巩膜盘 + 虹膜环 + 瞳孔
        /// <br/>保持与 SHPC 主色一致的青色基调，但通过竖向瞳孔和扫描线明确区分两者来源
        /// </summary>
        private static void DrawEyeCore(SpriteBatch sb, Texture2D px, Vector2 center,
            float time, float globalAlpha) {
            //核心整体半径较 SHPC 的 CoreRingR 略小，让弧条视觉权重更明显
            const float ringR = 14f;
            const float scleraR = 11f;
            const float irisR = 7f;
            const float pupilR = 2.6f;

            //投影
            SHPCRenderer.DrawDisc(sb, px, center + new Vector2(0f, 2f),
                ringR + 2f, 5f, SHPCTheme.ShadowDark * (0.45f * globalAlpha));

            //背景巩膜盘（底色，模拟"机械义眼"的金属背板）
            SHPCRenderer.DrawDisc(sb, px, center,
                scleraR, 2.5f, SHPCTheme.SlotBg * (0.95f * globalAlpha));

            //外环描边，固定权重区别于 SHPC 那种会随展开变亮的环
            float ringPulse = 0.55f + MathF.Sin(time * 1.6f) * 0.1f;
            Color ringCol = Color.Lerp(SHPCTheme.Border, SHPCTheme.BorderHi, ringPulse * 0.5f);
            SHPCRenderer.DrawArcStroke(sb, px, center, ringR, 0f, MathHelper.TwoPi,
                1.4f, ringCol * (ringPulse * globalAlpha));

            //虹膜：青色环带，叠加微弱呼吸
            float irisGlow = 0.65f + MathF.Sin(time * 2.2f) * 0.15f;
            SHPCRenderer.DrawDisc(sb, px, center,
                irisR, 2f, SHPCTheme.Cyan * (0.55f * irisGlow * globalAlpha));
            SHPCRenderer.DrawArcStroke(sb, px, center, irisR + 0.5f,
                0f, MathHelper.TwoPi, 0.9f, SHPCTheme.CyanHi * (0.85f * globalAlpha));

            //垂直瞳孔，是辨识度的关键：用一段竖直暗线模拟猫科义眼
            float pupilHeight = pupilR * 1.8f;
            SHPCRenderer.DrawLine(sb, px,
                center + new Vector2(0f, -pupilHeight),
                center + new Vector2(0f, pupilHeight),
                pupilR * 1.1f, SHPCTheme.ShadowDark * globalAlpha);
            //瞳孔高光
            SHPCRenderer.DrawDisc(sb, px, center + new Vector2(-0.6f, -pupilHeight * 0.55f),
                0.9f, 0.8f, SHPCTheme.CyanHi * (0.95f * globalAlpha));

            //缓慢横向扫描线，呼应"网络义眼实时分析"的设定
            float scanT = (time * 0.45f) % 1f;
            float scanY = MathHelper.Lerp(-scleraR + 1f, scleraR - 1f, scanT);
            float scanWidthHalf = MathF.Sqrt(MathF.Max(0f, scleraR * scleraR - scanY * scanY));
            if (scanWidthHalf > 0.5f) {
                SHPCRenderer.DrawLine(sb, px,
                    center + new Vector2(-scanWidthHalf, scanY),
                    center + new Vector2(scanWidthHalf, scanY),
                    1f, SHPCTheme.CyanHi * (0.35f * globalAlpha));
            }
        }

        #endregion
    }
}
