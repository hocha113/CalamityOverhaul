using CalamityOverhaul.Common;
using CalamityOverhaul.Content.EntrustManager;
using CalamityOverhaul.Content.HackTimes;
using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaDomains;
using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaServants;
using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaVaults;
using CalamityOverhaul.Content.QuestLogs;
using CalamityOverhaul.Content.UIs.RadialWheels;
using InnoVault.UIHandles;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.UI.ServantWheel
{
    /// <summary>鬼伞转盘扇区条目，一席一扇，仅本机</summary>
    internal sealed class KikasaWheelSector
    {
        /// <summary>对应影位 0..2</summary>
        public int SeatIndex;
        //悬停强度 0~1
        public float HoverAmount;
        //出战令高亮 0~1（未收起的驻影席）
        public float OrderAmount;
    }

    /// <summary>
    /// 鬼伞·沉影快捷转盘状态机，视觉见 <see cref="KikasaServantWheelUI"/>。
    /// <br/>开关键与排布归 <see cref="RadialWheelHub"/>（与比目鱼/SHPC/义体共键）；
    /// 扇区=三席影位，松键/点击翻转该席召/收；中心伞章=全席齐令。
    /// 编成（谁坐哪席）在湖心景里改，转盘只管出战与收起，湖未就绪时受理成候令，绝不无声
    /// </summary>
    internal class KikasaServantWheelController : ModPlayer, IRadialWheel
    {
        /// <summary>本机玩家的轻量单例视图</summary>
        public static KikasaServantWheelController LocalInstance =>
            Main.LocalPlayer?.GetModPlayer<KikasaServantWheelController>();

        /// <summary>转盘已展开</summary>
        public bool IsOpen { get; private set; }

        /// <summary>展开进度 0~1</summary>
        public float OpenProgress { get; private set; }

        /// <summary>悬停扇区索引，-1 无</summary>
        public int HoveredIndex { get; private set; } = -1;

        /// <summary>光标是否落在中心伞章上</summary>
        public bool CenterHovered { get; private set; }

        /// <summary>中心伞章悬停强度 0~1</summary>
        public float CenterHoverAmount { get; private set; }

        /// <summary>Hub 排布后的中心，命中与绘制共用</summary>
        public Vector2 ScreenAnchor { get; private set; }

        /// <summary>动画时钟，UI 涟漪用</summary>
        public float Time { get; private set; }

        /// <summary>扇区列表，恒三席</summary>
        public IReadOnlyList<KikasaWheelSector> Sectors => sectors;

        /// <summary>中心死区半径（伞章命中）</summary>
        public const float DeadZoneRadius = 34f;

        /// <summary>扇区之间的角度间隙</summary>
        public const float SectorGap = 0.06f;

        /// <summary>涟漪环带内缘</summary>
        public const float WheelInnerR = 56f;

        /// <summary>涟漪环带外缘</summary>
        public const float WheelOuterR = 112f;

        //展开/收起 lerp
        private const float OpenLerpRate = 0.22f;
        private const float CloseLerpRate = 0.28f;

        private readonly List<KikasaWheelSector> sectors = [];

        #region 转盘契约

        string IRadialWheel.WheelId => "KikasaServant";
        bool IRadialWheel.WheelIsOpen => IsOpen;
        bool IRadialWheel.WheelCanOpen => CanWheelBeShown() && !HackTime.Active;
        //武器盘一档，与 SHPC 盘同层（持有武器互斥，不会同帧齐开）
        int IRadialWheel.WheelStackOrder => 10;
        float IRadialWheel.WheelFootprintRadius => WheelOuterR + 24f;
        void IRadialWheel.WheelSetCenter(Vector2 center) => ScreenAnchor = center;
        void IRadialWheel.WheelOpen(bool silent) => OpenWheel(silent);
        void IRadialWheel.WheelClose(bool silent) => CloseWheel(silent);

        /// <summary>松键提交：席扇翻转召/收，中心伞章提交全席齐令</summary>
        void IRadialWheel.WheelCommitHovered() {
            if (!IsOpen) {
                return;
            }
            if (CenterHovered) {
                ToggleAllSeats();
                return;
            }
            if (HoveredIndex >= 0 && HoveredIndex < sectors.Count) {
                ToggleSeat(sectors[HoveredIndex].SeatIndex);
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

        /// <summary>存活 + （持鬼伞或本机域激活）+ 无全屏 UI 遮场</summary>
        private bool CanWheelBeShown() {
            if (Player == null || !Player.active || Player.dead) {
                return false;
            }
            Item held = Player.GetItem();
            bool holding = held != null && !held.IsAir
                && held.type == ModContent.ItemType<KikasaItem>();
            //血湖开着时不持伞也能号令，鬼奴的生命线本就挂在湖上
            if (!holding && !Player.GetModPlayer<KikasaDomainPlayer>().AnyActive) {
                return false;
            }
            if (QuestLog.Instance?.IsOpen == true || QuestManagerUI.Instance?.IsOpen == true) {
                return false;
            }
            //湖心景全屏铺开时不叠转盘，编成在屏里做，转盘是战时的手
            if (Panorama.KikasaPanoramaUI.Instance?.IsOpen == true) {
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
                    SoundEngine.PlaySound(SoundID.MenuTick with { Pitch = -0.1f, Volume = 0.3f });
                }
            }

            //左键落令，转盘保持开启可继续切换
            if (Main.mouseLeft && Main.mouseLeftRelease) {
                if (CenterHovered) {
                    Main.mouseLeftRelease = false;
                    ToggleAllSeats();
                }
                else if (HoveredIndex >= 0 && HoveredIndex < sectors.Count) {
                    Main.mouseLeftRelease = false;
                    ToggleSeat(sectors[HoveredIndex].SeatIndex);
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

        /// <summary>扇区高亮平滑：出战令跟 SlotHeld 真值走</summary>
        private void RefreshSectorStates() {
            KikasaServantPlayer servant = Player.GetModPlayer<KikasaServantPlayer>();
            for (int i = 0; i < sectors.Count; i++) {
                KikasaWheelSector sec = sectors[i];
                bool ordered = servant.SlotKeyAt(sec.SeatIndex) != 0
                    && !servant.SlotHeldAt(sec.SeatIndex);
                sec.HoverAmount = MathHelper.Lerp(sec.HoverAmount, i == HoveredIndex ? 1f : 0f, 0.25f);
                sec.OrderAmount = MathHelper.Lerp(sec.OrderAmount, ordered ? 1f : 0f, 0.25f);
            }
        }

        /// <summary>湖此刻是否托得住鬼奴（就绪=召令立即出水，否则成候令）</summary>
        public bool LakeReadyNow => Player.GetModPlayer<KikasaVaultPlayer>().LakeReady;

        /// <summary>
        /// 翻转单席召/收。空席拒绝出声；召令在湖未就绪时受理成候令
        /// （湖起自动出水），确认音区分三种结果，点击必有可听的回应
        /// </summary>
        private void ToggleSeat(int seatIndex) {
            KikasaServantPlayer servant = Player.GetModPlayer<KikasaServantPlayer>();
            if (!servant.ToggleSlotHeld(seatIndex, out bool nowHeld)) {
                PlayRejectSound();
                return;
            }
            PlayOrderSound(nowHeld);
        }

        /// <summary>全席齐令：有任一出战席则全收，否则全召；空盘拒绝出声</summary>
        private void ToggleAllSeats() {
            KikasaServantPlayer servant = Player.GetModPlayer<KikasaServantPlayer>();
            if (servant.FilledSlotCount <= 0) {
                PlayRejectSound();
                return;
            }
            bool anyOut = servant.ActiveSlotCount > 0;
            bool changed = false;
            for (int i = 0; i < KikasaServantPlayer.SlotCount; i++) {
                if (servant.SlotKeyAt(i) == 0) {
                    continue;
                }
                if (servant.SlotHeldAt(i) == anyOut) {
                    changed |= servant.ToggleSlotHeld(i, out _);
                }
            }
            if (changed) {
                PlayOrderSound(nowHeld: anyOut);
            }
        }

        /// <summary>令下的确认拍：收=回湖低响，召=破水；候令（湖未就绪）走更闷的一记水滴</summary>
        private void PlayOrderSound(bool nowHeld) {
            if (nowHeld) {
                SoundEngine.PlaySound(SoundID.SplashWeak with {
                    Pitch = -0.7f, Volume = 0.45f, MaxInstances = 2
                }, Player.Center);
                return;
            }
            if (LakeReadyNow) {
                SoundEngine.PlaySound(SoundID.SplashWeak with {
                    Pitch = -0.25f, Volume = 0.5f, MaxInstances = 2
                }, Player.Center);
            }
            else {
                //候令：湖未就绪，先把令收下，闷水滴与面板状态行一起把"为什么没出来"说清
                SoundEngine.PlaySound(SoundID.Drip with {
                    Pitch = -0.85f, Volume = 0.55f, MaxInstances = 2
                }, Player.Center);
            }
        }

        private void OpenWheel(bool silent) {
            sectors.Clear();
            KikasaServantPlayer servant = Player.GetModPlayer<KikasaServantPlayer>();
            for (int i = 0; i < KikasaServantPlayer.SlotCount; i++) {
                bool ordered = servant.SlotKeyAt(i) != 0 && !servant.SlotHeldAt(i);
                sectors.Add(new KikasaWheelSector {
                    SeatIndex = i,
                    HoverAmount = 0f,
                    OrderAmount = ordered ? 1f : 0f,
                });
            }

            IsOpen = true;
            HoveredIndex = -1;
            CenterHovered = false;
            CenterHoverAmount = 0f;
            if (!silent) {
                SoundEngine.PlaySound(SoundID.SplashWeak with { Pitch = -0.5f, Volume = 0.4f });
            }
        }

        private void CloseWheel(bool silent) {
            IsOpen = false;
            HoveredIndex = -1;
            CenterHovered = false;
            if (!silent) {
                SoundEngine.PlaySound(SoundID.Drip with { Pitch = -0.4f, Volume = 0.4f });
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
