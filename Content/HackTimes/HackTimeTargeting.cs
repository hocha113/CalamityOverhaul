using CalamityOverhaul.Common;
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
        //进入前缩放
        private float savedZoomTarget;

        /// <summary>光标下可骇入目标</summary>
        public static IHackTarget HoveredTarget { get; private set; }

        //兼容旧 API

        /// <summary>悬停物块 X，无则 -1</summary>
        public static int HoveredTileX => HoveredTarget is TileScannable t ? t.TileCoordX : -1;
        /// <summary>悬停物块 Y，无则 -1</summary>
        public static int HoveredTileY => HoveredTarget is TileScannable t ? t.TileCoordY : -1;
        /// <summary>悬停炮台，无则 null</summary>
        public static IHackableTurret HoveredTurret => HoveredTarget as IHackableTurret;
        /// <summary>悬停信号塔，无则 null</summary>
        public static IHackableSignalTower HoveredSignalTower => HoveredTarget as IHackableSignalTower;

        //拒弹窗节流，约 0.6 秒
        private static int accessDeniedCooldown;

        public override void ProcessTriggers(Terraria.GameInput.TriggersSet triggersSet) {
            if (Player.whoAmI != Main.myPlayer) return;
            if (Player.dead) return;

            if (accessDeniedCooldown > 0) accessDeniedCooldown--;

            if (CWRKeySystem.HackTime_Toggle != null && CWRKeySystem.HackTime_Toggle.JustPressed) {
                TryToggleHackTime(Player);
            }
        }

        /// <summary>按键切换，校验 HackTimeAccess</summary>
        public static void TryToggleHackTime(Player player) {
            //已激活可直接退出
            if (HackTime.Active) {
                HackTime.Toggle();
                return;
            }

            if (HackTimeAccess.CanUse(player)) {
                HackTime.Toggle();
                return;
            }

            //权限不足弹窗节流
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

        private void UpdateHoverDetection() {
            HoveredTarget = HackTargetType.DetectTopmostHover(Main.MouseWorld);
        }

        /// <summary>运镜偏移与缩放</summary>
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

            //首次进入记缩放
            if (!wasHackZoomActive) {
                savedZoomTarget = Main.GameZoomTarget;
                wasHackZoomActive = true;
            }

            if (HackTime.CameraOffset != Vector2.Zero) {
                Main.screenPosition += HackTime.CameraOffset;
            }

            //写入缩放，便于退出恢复
            float zoomBoost = HackTime.GetZoomBoost();
            Main.GameZoomTarget = MathHelper.Clamp(
                savedZoomTarget + zoomBoost, 0.1f, 10f);
        }
    }
}
