using CalamityOverhaul.Common;
using CalamityOverhaul.Content.EntrustManager;
using CalamityOverhaul.Content.HackTimes;
using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Cyberspaces;
using CalamityOverhaul.Content.QuestLogs;
using CalamityOverhaul.Content.UIs.RadialWheels;
using InnoVault.UIHandles;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.UI.DomainWheel
{
    /// <summary>领域转盘扇区条目，仅本机</summary>
    internal sealed class SHPCDomainWheelSector
    {
        /// <summary>对应领域层数 1..3</summary>
        public int Layer;
        //悬停强度 0~1
        public float HoverAmount;
        //当前所在层高亮 0~1
        public float SelectedAmount;
    }

    /// <summary>
    /// SHPC 领域快捷转盘状态机，视觉见 <see cref="SHPCDomainWheelUI"/>
    /// <br/>开关键与排布归 <see cref="RadialWheelHub"/>；扇区选层，中心进骇客时间
    /// </summary>
    internal class SHPCDomainWheelController : ModPlayer, IRadialWheel
    {
        /// <summary>本机玩家的轻量单例视图</summary>
        public static SHPCDomainWheelController LocalInstance =>
            Main.LocalPlayer?.GetModPlayer<SHPCDomainWheelController>();

        /// <summary>转盘已展开</summary>
        public bool IsOpen { get; private set; }

        /// <summary>展开进度 0~1</summary>
        public float OpenProgress { get; private set; }

        /// <summary>悬停扇区索引，-1 无</summary>
        public int HoveredIndex { get; private set; } = -1;

        /// <summary>光标是否落在中心骇客按钮上</summary>
        public bool CenterHovered { get; private set; }

        /// <summary>中心按钮悬停强度 0~1</summary>
        public float CenterHoverAmount { get; private set; }

        /// <summary>Hub 排布后的中心，命中与绘制共用</summary>
        public Vector2 ScreenAnchor { get; private set; }

        /// <summary>动画时钟，UI 扫光用</summary>
        public float Time { get; private set; }

        /// <summary>扇区列表，IsOpen 期间稳定</summary>
        public IReadOnlyList<SHPCDomainWheelSector> Sectors => sectors;

        /// <summary>中心死区半径，与义体转盘同口径</summary>
        public const float DeadZoneRadius = 36f;

        //扇区之间的角度间隙
        public const float SectorGap = 0.045f;

        //展开/收起 lerp
        private const float OpenLerpRate = 0.22f;
        private const float CloseLerpRate = 0.28f;

        private readonly List<SHPCDomainWheelSector> sectors = [];

        #region 转盘契约

        string IRadialWheel.WheelId => "SHPCDomain";
        bool IRadialWheel.WheelIsOpen => IsOpen;
        bool IRadialWheel.WheelCanOpen => CanWheelBeShown() && !HackTime.Active;
        //武器盘排在义体盘之上
        int IRadialWheel.WheelStackOrder => 10;
        float IRadialWheel.WheelFootprintRadius => SHPCTheme.ButtonOuterR + 22f;
        void IRadialWheel.WheelSetCenter(Vector2 center) => ScreenAnchor = center;
        void IRadialWheel.WheelOpen(bool silent) => OpenWheel(silent);
        void IRadialWheel.WheelClose(bool silent) => CloseWheel(silent);

        /// <summary>松键提交：只认层扇区，中心骇客键必须显式点击</summary>
        void IRadialWheel.WheelCommitHovered() {
            if (!IsOpen || CenterHovered) {
                return;
            }
            if (HoveredIndex >= 0 && HoveredIndex < sectors.Count) {
                ApplyLayer(sectors[HoveredIndex].Layer);
            }
        }

        #endregion

        public override void PostUpdate() {
            //仅本机
            if (Player.whoAmI != Main.myPlayer || Main.dedServ) {
                return;
            }
            Time += 1f / 60f;
            //登记进 Hub 并取回本帧排布中心
            ScreenAnchor = RadialWheelHub.ResolveCenter(this);

            if (!CanWheelBeShown() || HackTime.Active) {
                if (IsOpen || OpenProgress > 0.01f) {
                    ForceCloseWheel();
                }
                UpdateOpenProgress();
                return;
            }

            HandleWheelMouse();
            UpdateOpenProgress();
        }

        public override void UpdateDead() {
            if (Player.whoAmI != Main.myPlayer) {
                return;
            }
            if (IsOpen || OpenProgress > 0.01f) {
                ForceCloseWheel();
            }
            UpdateOpenProgress();
        }

        /// <summary>存活 + 手持 SHPC + 无全屏 UI</summary>
        private bool CanWheelBeShown() {
            if (Player == null || !Player.active || Player.dead) {
                return false;
            }
            Item held = Player.GetItem();
            if (held == null || held.IsAir || held.type != SHPCOverride.ID) {
                return false;
            }
            if (QuestLog.Instance?.visible == true || QuestManagerUI.Instance?.IsOpen == true) {
                return false;
            }
            return true;
        }

        private void HandleWheelMouse() {
            if (!IsOpen) {
                return;
            }

            //mouseInterface 防穿透，非焦点盘同样要挡，否则点击会漏进世界
            Player.mouseInterface = true;
            UIInputGuard.SuppressWeaponSwitch();

            //光标归了别的盘：清空悬停，不吃点击
            if (!RadialWheelHub.IsFocused(this)) {
                HoveredIndex = -1;
                CenterHovered = false;
                RefreshSectorStates();
                CenterHoverAmount = MathHelper.Lerp(CenterHoverAmount, 0f, 0.25f);
                return;
            }

            bool newCenter = RadialWheelHub.IsCenterHovered(ScreenAnchor, DeadZoneRadius);
            int newHover = newCenter ? -1
                : RadialWheelHub.HitTest(ScreenAnchor, sectors.Count, DeadZoneRadius);
            if (newHover != HoveredIndex || newCenter != CenterHovered) {
                HoveredIndex = newHover;
                CenterHovered = newCenter;
                if (newCenter || HoveredIndex >= 0) {
                    SoundEngine.PlaySound(SoundID.MenuTick with { Pitch = 0.15f, Volume = 0.35f });
                }
            }

            //左键落子，转盘保持开启可继续切换
            if (Main.mouseLeft && Main.mouseLeftRelease) {
                if (CenterHovered) {
                    Main.mouseLeftRelease = false;
                    TriggerHackTime();
                    return;
                }
                if (HoveredIndex >= 0 && HoveredIndex < sectors.Count) {
                    Main.mouseLeftRelease = false;
                    ApplyLayer(sectors[HoveredIndex].Layer);
                }
            }

            //右键 = 全部收起且不做改动
            if (Main.mouseRight && Main.mouseRightRelease) {
                Main.mouseRightRelease = false;
                RadialWheelHub.CloseAll(silent: false);
                return;
            }

            RefreshSectorStates();
            CenterHoverAmount = MathHelper.Lerp(CenterHoverAmount, CenterHovered ? 1f : 0f, 0.25f);
        }

        /// <summary>扇区高亮平滑</summary>
        private void RefreshSectorStates() {
            int activeLayer = Cyberspace.Active ? Cyberspace.CurrentLayer : 0;
            for (int i = 0; i < sectors.Count; i++) {
                SHPCDomainWheelSector sec = sectors[i];
                sec.HoverAmount = MathHelper.Lerp(sec.HoverAmount, i == HoveredIndex ? 1f : 0f, 0.25f);
                sec.SelectedAmount = MathHelper.Lerp(sec.SelectedAmount,
                    sec.Layer == activeLayer ? 1f : 0f, 0.25f);
            }
        }

        /// <summary>该层此刻是否可点：崩溃锁定拒全部，当前层永远可点（用于关闭）</summary>
        public bool IsLayerReady(int layer) {
            if (Cyberspace.IsCrashLockedOut) {
                return false;
            }
            if (Cyberspace.Active && Cyberspace.CurrentLayer == layer) {
                return true;
            }
            return Cyberspace.CanAffordLayer(layer);
        }

        /// <summary>
        /// 中心骇客按钮是否可点：除准入外还要求骇客键已绑定——
        /// 骇客时间里转盘开不了，中心只能进不能出，未绑键放行就是单程门
        /// </summary>
        public bool IsHackReady() {
            if (!HackTimeAccess.CanUse(Player)) {
                return false;
            }
            return !CWRKeySystem.IsKeybindUnbound(CWRKeySystem.HackTime_Toggle);
        }

        /// <summary>选层：未开则先开，已在该层则关闭领域</summary>
        private void ApplyLayer(int layer) {
            if (Cyberspace.IsCrashLockedOut) {
                PlayRejectSound();
                return;
            }
            //再点一次当前层 = 收起领域，转盘上没有独立开关位
            if (Cyberspace.Active && Cyberspace.CurrentLayer == layer) {
                Cyberspace.Deactivate();
                SoundEngine.PlaySound(SoundID.MenuTick with { Pitch = -0.15f, Volume = 0.5f });
                return;
            }
            if (!Cyberspace.CanAffordLayer(layer)) {
                PlayRejectSound();
                return;
            }
            //未开领域先激活，随后再切到目标层，与 SHPCCyberPanel 的点击链一致
            if (!Cyberspace.Active) {
                Cyberspace.Activate(Player);
            }
            Cyberspace.SetLayer(layer, Player);
            SoundEngine.PlaySound(SoundID.MenuTick with { Pitch = 0.45f, Volume = 0.6f });
        }

        /// <summary>
        /// 中心按钮：先切骇客时间再静默收起全部盘
        /// <br/>时停 reason 是引用集，先接上 HackTime 再放掉转盘的，就不会在同一帧解冻又重冻
        /// </summary>
        private void TriggerHackTime() {
            if (!IsHackReady()) {
                PlayRejectSound();
                return;
            }
            HackTimeTargeting.TryToggleHackTime(Player);
            RadialWheelHub.CloseAll(silent: true);
        }

        private void OpenWheel(bool silent) {
            sectors.Clear();
            int activeLayer = Cyberspace.Active ? Cyberspace.CurrentLayer : 0;
            for (int layer = 1; layer <= Cyberspace.MaxLayerCount; layer++) {
                sectors.Add(new SHPCDomainWheelSector {
                    Layer = layer,
                    HoverAmount = 0f,
                    SelectedAmount = layer == activeLayer ? 1f : 0f,
                });
            }

            IsOpen = true;
            HoveredIndex = -1;
            CenterHovered = false;
            CenterHoverAmount = 0f;
            if (!silent) {
                SoundEngine.PlaySound(SoundID.MenuOpen with { Pitch = 0.2f, Volume = 0.5f });
            }
        }

        private void CloseWheel(bool silent) {
            IsOpen = false;
            HoveredIndex = -1;
            CenterHovered = false;
            if (!silent) {
                SoundEngine.PlaySound(SoundID.MenuClose with { Pitch = -0.1f, Volume = 0.45f });
            }
        }

        /// <summary>强制关盘，死亡/换武/全屏 UI 介入</summary>
        public void ForceCloseWheel() => CloseWheel(silent: true);

        private void PlayRejectSound()
            => SoundEngine.PlaySound(SoundID.MenuTick with { Pitch = -0.5f, Volume = 0.5f }, Player.Center);

        private void UpdateOpenProgress() {
            float target = IsOpen ? 1f : 0f;
            float rate = IsOpen ? OpenLerpRate : CloseLerpRate;
            OpenProgress = MathHelper.Lerp(OpenProgress, target, rate);
            if (MathF.Abs(OpenProgress - target) < 0.003f) {
                OpenProgress = target;
            }
            if (!IsOpen) {
                CenterHoverAmount = MathHelper.Lerp(CenterHoverAmount, 0f, CloseLerpRate);
            }
        }

        /// <summary>扇区角度区间，首扇区中线朝正上方</summary>
        public void GetSectorAngles(int idx, out float aStart, out float aEnd)
            => RadialWheelHub.GetSectorAngles(idx, sectors.Count, SectorGap, out aStart, out aEnd);
    }
}
