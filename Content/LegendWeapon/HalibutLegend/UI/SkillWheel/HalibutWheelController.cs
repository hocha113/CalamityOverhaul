using CalamityOverhaul.Content.HackTimes;
using CalamityOverhaul.Content.LegendWeapon.HalibutLegend.UI.Atlas;
using CalamityOverhaul.Content.UIs.RadialWheels;
using InnoVault.UIHandles;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.HalibutLegend.UI.SkillWheel
{
    /// <summary>
    /// 轮盘扇区的运行时条目，仅供本机使用
    /// </summary>
    internal sealed class HalibutWheelSector
    {
        public FishSkill Skill;
        //平滑跟随的悬停强度
        public float HoverAmount;
        //平滑跟随的"当前选中"高亮强度
        public float SelectedAmount;
    }

    /// <summary>
    /// 技能轮盘状态机，视觉见 <see cref="HalibutSkillWheel"/>
    /// 按住 Halibut_SkillWheel 开盘 + 子弹时间，松键或左键选定；右键关盘
    /// </summary>
    internal class HalibutWheelController : ModPlayer, IRadialWheel
    {
        /// <summary>
        /// 本机玩家的轻量单例视图
        /// </summary>
        public static HalibutWheelController LocalInstance =>
            Main.LocalPlayer?.GetModPlayer<HalibutWheelController>();

        /// <summary>
        /// 轮盘是否已打开
        /// </summary>
        public bool IsOpen { get; private set; }

        /// <summary>
        /// 0~1 的展开进度
        /// </summary>
        public float OpenProgress { get; private set; }

        /// <summary>
        /// 当前悬停的扇区索引，-1 表示未命中
        /// </summary>
        public int HoveredIndex { get; private set; } = -1;

        /// <summary>
        /// Hub 排布后的轮盘中心，命中与绘制共用
        /// </summary>
        public Vector2 ScreenAnchor { get; private set; }

        /// <summary>
        /// 全局动画时间
        /// </summary>
        public float Time { get; private set; }

        /// <summary>
        /// 当前装载的扇区列表，仅在 <see cref="IsOpen"/> 期间稳定
        /// </summary>
        public IReadOnlyList<HalibutWheelSector> Sectors => sectors;

        private readonly List<HalibutWheelSector> sectors = [];
        private const float OpenLerpRate = 0.22f;
        private const float CloseLerpRate = 0.28f;

        #region 转盘契约

        string IRadialWheel.WheelId => "Halibut";
        bool IRadialWheel.WheelIsOpen => IsOpen;
        bool IRadialWheel.WheelCanOpen => CanWheelBeShown() && !HackTime.Active;
        //武器盘排在义体盘之上
        int IRadialWheel.WheelStackOrder => 10;
        float IRadialWheel.WheelFootprintRadius => HalibutTheme.WheelOuterR + 12f;
        void IRadialWheel.WheelSetCenter(Vector2 center) => ScreenAnchor = center;
        void IRadialWheel.WheelOpen(bool silent) => OpenWheel(silent);
        void IRadialWheel.WheelClose(bool silent) => CloseWheel(silent);

        /// <summary>松键提交焦点盘的悬停技能</summary>
        void IRadialWheel.WheelCommitHovered() {
            if (!IsOpen || HoveredIndex < 0 || HoveredIndex >= sectors.Count) {
                return;
            }
            SelectSkill(sectors[HoveredIndex].Skill);
        }

        #endregion

        public override void PostUpdate() {
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

        private HalibutSave Save => Player.GetModPlayer<HalibutSave>();

        /// <summary>
        /// 轮盘可显示、存活+手持鱼+栏非空+图鉴关
        /// </summary>
        private bool CanWheelBeShown() {
            if (Player == null || !Player.active || Player.dead) {
                return false;
            }
            if (!Player.TryGetOverride<HalibutPlayer>(out var halibutPlayer) || !halibutPlayer.HeldHalibut) {
                return false;
            }
            if (HalibutAtlas.Instance?.IsOpen == true) {
                return false;
            }
            return Save.loadout.Count > 0;
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
                RefreshSectorStates();
                return;
            }

            int newHover = HitTest();
            if (newHover != HoveredIndex) {
                HoveredIndex = newHover;
                if (HoveredIndex >= 0 && HoveredIndex < sectors.Count) {
                    SoundEngine.PlaySound(SoundID.MenuTick with { Pitch = 0.15f, Volume = 0.35f });
                }
            }

            //左键点选（轮盘保持开启，可以继续切换）
            if (Main.mouseLeft && Main.mouseLeftRelease
                && HoveredIndex >= 0 && HoveredIndex < sectors.Count) {
                SelectSkill(sectors[HoveredIndex].Skill);
                Main.mouseLeftRelease = false;
            }

            //右键 = 全部收起且不做改动
            if (Main.mouseRight && Main.mouseRightRelease) {
                Main.mouseRightRelease = false;
                RadialWheelHub.CloseAll(silent: false);
                return;
            }

            RefreshSectorStates();
        }

        /// <summary>平滑推进各扇区的过渡量</summary>
        private void RefreshSectorStates() {
            FishSkill current = Save.FishSkill;
            for (int i = 0; i < sectors.Count; i++) {
                HalibutWheelSector sec = sectors[i];
                sec.HoverAmount = MathHelper.Lerp(sec.HoverAmount, i == HoveredIndex ? 1f : 0f, 0.25f);
                sec.SelectedAmount = MathHelper.Lerp(sec.SelectedAmount, sec.Skill == current ? 1f : 0f, 0.25f);
            }
        }

        /// <summary>
        /// 选定技能、写存档+头顶切换演出
        /// </summary>
        public void SelectSkill(FishSkill skill) {
            if (skill == null) {
                return;
            }
            var save = Save;
            if (save.FishSkill == skill) {
                SoundEngine.PlaySound(SoundID.MenuTick with { Pitch = 0.25f, Volume = 0.4f });
                return;
            }
            save.FishSkill = skill;
            SkillRender.SwitchingSkill = skill;
            SkillRender.SwitchAnimProgress = 0f;
            SkillRender.SwitchAnimTimer = 0;
            HalibutHud.Instance?.NotifySkillSwitched();//深渊之眼眨眼 + 徽章闪光
            SoundEngine.PlaySound(SoundID.MenuTick with { Pitch = 0.45f, Volume = 0.6f });
        }

        private void OpenWheel(bool silent) {
            var save = Save;
            sectors.Clear();
            foreach (FishSkill skill in save.loadout) {
                if (skill == null) {
                    continue;
                }
                sectors.Add(new HalibutWheelSector {
                    Skill = skill,
                    HoverAmount = 0f,
                    SelectedAmount = skill == save.FishSkill ? 1f : 0f,
                });
            }
            if (sectors.Count == 0) {
                return;
            }
            IsOpen = true;
            HoveredIndex = -1;
            if (!silent) {
                SoundEngine.PlaySound(SoundID.MenuOpen with { Pitch = -0.25f, Volume = 0.55f });
                SoundEngine.PlaySound(SoundID.SplashWeak with { Pitch = 0.3f, Volume = 0.5f });
            }
        }

        private void CloseWheel(bool silent) {
            IsOpen = false;
            HoveredIndex = -1;
            if (!silent) {
                SoundEngine.PlaySound(SoundID.MenuClose with { Pitch = -0.1f, Volume = 0.45f });
            }
        }

        /// <summary>强制关盘，死亡/换武/全屏UI介入</summary>
        public void ForceCloseWheel() => CloseWheel(silent: true);

        private void UpdateOpenProgress() {
            float target = IsOpen ? 1f : 0f;
            float rate = IsOpen ? OpenLerpRate : CloseLerpRate;
            OpenProgress = MathHelper.Lerp(OpenProgress, target, rate);
            if (MathF.Abs(OpenProgress - target) < 0.003f) {
                OpenProgress = target;
            }
        }

        /// <summary>
        /// 极坐标命中、扇区索引或-1
        /// </summary>
        private int HitTest()
            => RadialWheelHub.HitTest(ScreenAnchor, sectors.Count, HalibutTheme.WheelDeadZoneR);

        /// <summary>
        /// 给指定扇区索引返回其覆盖的角度区间（首扇区中线朝正上方）
        /// </summary>
        public void GetSectorAngles(int idx, out float aStart, out float aEnd)
            => RadialWheelHub.GetSectorAngles(idx, sectors.Count
                , HalibutTheme.WheelSectorGap, out aStart, out aEnd);
    }
}
