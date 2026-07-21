using CalamityOverhaul.Content.EntrustManager;
using CalamityOverhaul.Content.LegendWeapon.SHPCLegend;
using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.UI;
using CalamityOverhaul.Content.QuestLogs;
using CalamityOverhaul.Content.RAMSystems;
using CalamityOverhaul.Content.UIs.HudStack;
using InnoVault.UIHandles;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.Cyberwares.Implementation.CstmVisualEyes
{
    /// <summary>
    /// CSTM 视像义眼 HUD，左下 RAM 弧条+义眼核心
    /// <br/>装备且未持 SHPC 时显示，复用 SHPCRenderer.DrawRAMBar
    /// </summary>
    internal class CstmVisualEyeHUD : UIHandle, IBottomLeftHud
    {
        public static CstmVisualEyeHUD Instance => UIHandleLoader.GetUIHandleOfType<CstmVisualEyeHUD>();

        #region 显隐判定

        public override bool Active {
            get {
                Player p = Main.LocalPlayer;
                if (p == null || !p.active || p.dead) {
                    return false;
                }
                if (CstmVisualEye.GetEquipped(p) == null) {
                    return false;
                }
                //持 SHPC 时让位 SHPCUI
                Item held = p.GetItem();
                if (held != null && !held.IsAir && held.type == SHPCOverride.ID) {
                    return false;
                }
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

        #region 左下角 HUD 队列接入
        //被动小 HUD，order 10，主武器同屏时上移避让
        bool IBottomLeftHud.HudStackActive => Active;
        int IBottomLeftHud.HudStackOrder => 10;
        Vector2 IBottomLeftHud.HudStackAnchor => NaturalCorePosition;
        //上下覆盖弧条与核心
        float IBottomLeftHud.HudStackTopExtent => 60f;
        float IBottomLeftHud.HudStackBottomExtent => 60f;
        #endregion

        #region 状态

        //扫光/呼吸节奏，秒
        private float time;
        //平滑 RAM 读数
        private float ramDisplayValue;

        #endregion

        #region 更新

        public override void Update() {
            time += 1f / 60f;

            //同 SHPCUI 平滑规则
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

            DrawEyeCore(sb, px, corePos, time, globalAlpha);

            SHPCRenderer.DrawRAMBar(sb, px, corePos,
                ramDisplayValue, RamSystem.MaxRam, time, globalAlpha);
        }

        //同 SHPCUI 锚点，UI 空间高度防高 UIScale 漂移
        private static Vector2 NaturalCorePosition => new(96f, BottomLeftHudStack.UIScreenH - 96f);

        /// <summary>核心位置，左下 HUD 队列避让</summary>
        private static Vector2 GetCorePosition() {
            CstmVisualEyeHUD inst = Instance;
            return inst == null ? NaturalCorePosition : BottomLeftHudStack.ResolveAnchor(inst);
        }

        /// <summary>程序化义眼，外环+巩膜+虹膜+竖瞳+扫描线</summary>
        private static void DrawEyeCore(SpriteBatch sb, Texture2D px, Vector2 center,
            float time, float globalAlpha) {
            //略小于 SHPC CoreRingR
            const float ringR = 14f;
            const float scleraR = 11f;
            const float irisR = 7f;
            const float pupilR = 2.6f;

            SHPCRenderer.DrawDisc(sb, px, center + new Vector2(0f, 2f),
                ringR + 2f, 5f, SHPCTheme.ShadowDark * (0.45f * globalAlpha));

            SHPCRenderer.DrawDisc(sb, px, center,
                scleraR, 2.5f, SHPCTheme.SlotBg * (0.95f * globalAlpha));

            float ringPulse = 0.55f + MathF.Sin(time * 1.6f) * 0.1f;
            Color ringCol = Color.Lerp(SHPCTheme.Border, SHPCTheme.BorderHi, ringPulse * 0.5f);
            SHPCRenderer.DrawArcStroke(sb, px, center, ringR, 0f, MathHelper.TwoPi,
                1.4f, ringCol * (ringPulse * globalAlpha));

            //虹膜呼吸
            float irisGlow = 0.65f + MathF.Sin(time * 2.2f) * 0.15f;
            SHPCRenderer.DrawDisc(sb, px, center,
                irisR, 2f, SHPCTheme.Cyan * (0.55f * irisGlow * globalAlpha));
            SHPCRenderer.DrawArcStroke(sb, px, center, irisR + 0.5f,
                0f, MathHelper.TwoPi, 0.9f, SHPCTheme.CyanHi * (0.85f * globalAlpha));

            //竖瞳
            float pupilHeight = pupilR * 1.8f;
            SHPCRenderer.DrawLine(sb, px,
                center + new Vector2(0f, -pupilHeight),
                center + new Vector2(0f, pupilHeight),
                pupilR * 1.1f, SHPCTheme.ShadowDark * globalAlpha);
            SHPCRenderer.DrawDisc(sb, px, center + new Vector2(-0.6f, -pupilHeight * 0.55f),
                0.9f, 0.8f, SHPCTheme.CyanHi * (0.95f * globalAlpha));

            //横向扫描线
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
