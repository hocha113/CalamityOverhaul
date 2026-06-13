using CalamityOverhaul.Common;
using CalamityOverhaul.Content.ADV.Scenarios.VoidColonys.GlitchWraith;
using CalamityOverhaul.Content.HackTimes.Scannables;
using CalamityOverhaul.Content.UIs.NotificationPopup;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.HackTimes
{
    /// <summary>骇客时间目标选择与运镜</summary>
    internal class HackTimeTargeting : ModPlayer
    {
        //是否接管缩放
        private bool wasHackZoomActive;
        //进入前保存的缩放目标
        private float savedZoomTarget;

        /// <summary>光标下可骇入目标</summary>
        public static IHackTarget HoveredTarget { get; private set; }

        //兼容旧 API

        /// <summary>当前悬停的可扫描物块 X，无悬停物块时返回 -1</summary>
        public static int HoveredTileX => HoveredTarget is TileScannable t ? t.TileCoordX : -1;
        /// <summary>当前悬停的可扫描物块 Y，无悬停物块时返回 -1</summary>
        public static int HoveredTileY => HoveredTarget is TileScannable t ? t.TileCoordY : -1;
        /// <summary>当前悬停的灵异 Actor，无悬停灵异时返回 null</summary>
        public static GlitchWraithActor HoveredWraith => HoveredTarget as GlitchWraithActor;
        /// <summary>当前悬停的可骇入炮台，无悬停炮台时返回 null</summary>
        public static IHackableTurret HoveredTurret => HoveredTarget as IHackableTurret;
        /// <summary>当前悬停的可骇入信号塔，无悬停信号塔时返回 null</summary>
        public static IHackableSignalTower HoveredSignalTower => HoveredTarget as IHackableSignalTower;

        //权限拒绝弹窗节流，约 0.6 秒
        private static int accessDeniedCooldown;

        public override void ProcessTriggers(Terraria.GameInput.TriggersSet triggersSet) {
            if (Player.whoAmI != Main.myPlayer) return;
            if (Player.dead) return;

            if (accessDeniedCooldown > 0) accessDeniedCooldown--;

            if (CWRKeySystem.HackTime_Toggle != null && CWRKeySystem.HackTime_Toggle.JustPressed) {
                TryToggleHackTime(Player);
            }
        }

        /// <summary>按键切换骇客时间，校验 HackTimeAccess</summary>
        public static void TryToggleHackTime(Player player) {
            //已激活时允许退出
            if (HackTime.Active) {
                HackTime.Toggle();
                return;
            }

            if (HackTimeAccess.CanUse(player)) {
                HackTime.Toggle();
                return;
            }

            //权限不足弹窗，短冷却节流
            if (accessDeniedCooldown <= 0) {
                NotificationPopupSystem.Add(new HackTimeAccessDeniedEntry());
                accessDeniedCooldown = 36;
            }
        }

        public override void PostUpdate() {
            if (Player.whoAmI != Main.myPlayer) return;
            if (!HackTime.Active) {
                HoveredTarget = null;
                return;
            }
            UpdateHoverDetection();
        }

        /// <summary>按 HoverPriority 检测悬停目标</summary>
        private void UpdateHoverDetection() {
            HoveredTarget = HackTargetType.DetectTopmostHover(Main.MouseWorld);
        }

        /// <summary>骇客时间运镜偏移与缩放</summary>
        public override void ModifyScreenPosition() {
            bool needControl = HackTime.Active || HackTime.Intensity >= 0.001f;

            if (!needControl) {
                //退出后恢复缩放
                if (wasHackZoomActive) {
                    Main.GameZoomTarget = savedZoomTarget;
                    wasHackZoomActive = false;
                }
                return;
            }

            //首次进入保存缩放
            if (!wasHackZoomActive) {
                savedZoomTarget = Main.GameZoomTarget;
                wasHackZoomActive = true;
            }

            //应用运镜偏移
            if (HackTime.CameraOffset != Vector2.Zero) {
                Main.screenPosition += HackTime.CameraOffset;
            }

            //set 缩放便于退出恢复
            float zoomBoost = HackTime.GetZoomBoost();
            Main.GameZoomTarget = MathHelper.Clamp(
                savedZoomTarget + zoomBoost, 0.1f, 10f);
        }
    }
}
