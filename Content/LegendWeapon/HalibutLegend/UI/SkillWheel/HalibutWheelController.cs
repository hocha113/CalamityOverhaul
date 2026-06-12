using CalamityOverhaul.Common;
using CalamityOverhaul.Content.HackTimes;
using CalamityOverhaul.Content.LegendWeapon.HalibutLegend.UI.Atlas;
using CalamityOverhaul.Content.TimeFreezes;
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
    /// 比目鱼技能轮盘的核心状态机（视觉层见 <see cref="HalibutSkillWheel"/>）
    /// <list type="bullet">
    ///   <item>按住 <see cref="CWRKeySystem.Halibut_SkillWheel"/> 开盘并进入子弹时间（单人），
    ///         鼠标甩向扇区，松开按键即选定该技能；期间左键点击也可选定</item>
    ///   <item>右键关闭轮盘不做任何改动；光标退回中心死区 = 不选择</item>
    ///   <item><see cref="CWRKeySystem.Halibut_Skill_L"/> / <see cref="CWRKeySystem.Halibut_Skill_R"/>
    ///         在轮盘关闭时仍可循环切换装备栏技能</item>
    /// </list>
    /// </summary>
    internal class HalibutWheelController : ModPlayer
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
        /// 轮盘屏幕锚点（屏幕中央）
        /// </summary>
        public Vector2 ScreenAnchor { get; private set; }

        /// <summary>
        /// 由视图Draw阶段写回锚点，避免跨阶段窗口尺寸变化造成错位
        /// </summary>
        public void SetScreenAnchor(Vector2 anchor) => ScreenAnchor = anchor;

        /// <summary>
        /// 全局动画时间
        /// </summary>
        public float Time { get; private set; }

        /// <summary>
        /// 当前装载的扇区列表，仅在 <see cref="IsOpen"/> 期间稳定
        /// </summary>
        public IReadOnlyList<HalibutWheelSector> Sectors => sectors;

        private readonly List<HalibutWheelSector> sectors = [];
        private bool freezeOwned;
        private const string FreezeReason = "HalibutWheel";
        private const float OpenLerpRate = 0.22f;
        private const float CloseLerpRate = 0.28f;

        public override void PostUpdate() {
            if (Player.whoAmI != Main.myPlayer || Main.dedServ) {
                return;
            }
            Time += 1f / 60f;
            //PostUpdate运行在逻辑帧（非UI层），必须使用UI空间换算
            ScreenAnchor = new Vector2(HalibutTheme.UIScreenW * 0.5f,
                HalibutTheme.UIScreenH * HalibutTheme.WheelAnchorYRatio);

            if (!CanWheelBeShown()) {
                if (IsOpen || OpenProgress > 0.01f) {
                    ForceCloseWheel();
                }
                UpdateOpenProgress();
                return;
            }

            HandleWheelKey();
            HandleCycleKeys();
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
        /// 轮盘允许显示：存活、手持比目鱼、装备栏非空、图鉴未打开
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

        private void HandleWheelKey() {
            ModKeybind key = CWRKeySystem.Halibut_SkillWheel;
            if (key == null) {
                return;
            }
            if (!IsOpen && key.JustPressed) {
                TryOpenWheel();
                return;
            }
            if (IsOpen && key.JustReleased) {
                //松键 = 确认选择：有悬停扇区则选定它
                if (HoveredIndex >= 0 && HoveredIndex < sectors.Count) {
                    SelectSkill(sectors[HoveredIndex].Skill);
                }
                CloseWheel(silentSound: false);
            }
        }

        /// <summary>
        /// 轮盘关闭时的左右循环切换
        /// </summary>
        private void HandleCycleKeys() {
            if (IsOpen) {
                return;
            }
            bool left = CWRKeySystem.Halibut_Skill_L.JustPressed;
            bool right = CWRKeySystem.Halibut_Skill_R.JustPressed;
            if (!left && !right) {
                return;
            }
            var save = Save;
            if (save.loadout.Count == 0) {
                return;
            }
            int currentIndex = save.FishSkill != null ? save.loadout.IndexOf(save.FishSkill) : -1;
            int newIndex;
            if (left) {
                newIndex = currentIndex <= 0 ? save.loadout.Count - 1 : currentIndex - 1;
            }
            else {
                newIndex = currentIndex >= save.loadout.Count - 1 ? 0 : currentIndex + 1;
            }
            FishSkill target = save.loadout[newIndex];
            if (target != null && target != save.FishSkill) {
                SelectSkill(target);
            }
        }

        private void HandleWheelMouse() {
            if (!IsOpen) {
                return;
            }
            Player.mouseInterface = true;
            Player.CWR().DontSwitchWeaponTime = 2;

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

            //右键 = 立即关盘不做改动
            if (Main.mouseRight && Main.mouseRightRelease) {
                Main.mouseRightRelease = false;
                CloseWheel(silentSound: false);
                return;
            }

            //平滑推进各扇区的过渡量
            FishSkill current = Save.FishSkill;
            for (int i = 0; i < sectors.Count; i++) {
                HalibutWheelSector sec = sectors[i];
                sec.HoverAmount = MathHelper.Lerp(sec.HoverAmount, i == HoveredIndex ? 1f : 0f, 0.25f);
                sec.SelectedAmount = MathHelper.Lerp(sec.SelectedAmount, sec.Skill == current ? 1f : 0f, 0.25f);
            }
        }

        /// <summary>
        /// 选定技能：写入存档数据并触发头顶切换演出
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

        private void TryOpenWheel() {
            //骇客时间等更高优先级的子弹时间激活时拒绝开盘
            if (HackTime.Active) {
                SoundEngine.PlaySound(SoundID.MenuTick with { Pitch = -0.5f, Volume = 0.5f }, Player.Center);
                return;
            }
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
            AcquireFreezeIfNeeded();
            SoundEngine.PlaySound(SoundID.MenuOpen with { Pitch = -0.25f, Volume = 0.55f });
            SoundEngine.PlaySound(SoundID.SplashWeak with { Pitch = 0.3f, Volume = 0.5f });
        }

        private void CloseWheel(bool silentSound) {
            ReleaseFreezeIfOwned();
            IsOpen = false;
            HoveredIndex = -1;
            if (!silentSound) {
                SoundEngine.PlaySound(SoundID.MenuClose with { Pitch = -0.1f, Volume = 0.45f });
            }
        }

        /// <summary>
        /// 强制关盘，用于死亡 / 切换武器 / 全屏UI介入等异常路径
        /// </summary>
        public void ForceCloseWheel() {
            ReleaseFreezeIfOwned();
            IsOpen = false;
            HoveredIndex = -1;
        }

        private void UpdateOpenProgress() {
            float target = IsOpen ? 1f : 0f;
            float rate = IsOpen ? OpenLerpRate : CloseLerpRate;
            OpenProgress = MathHelper.Lerp(OpenProgress, target, rate);
            if (MathF.Abs(OpenProgress - target) < 0.003f) {
                OpenProgress = target;
            }
        }

        private void AcquireFreezeIfNeeded() {
            if (freezeOwned) {
                return;
            }
            if (VaultUtils.isSinglePlayer) {//只在单人模式生效世界冻结
                WorldFreezeSystem.Activate(FreezeReason);
            }
            freezeOwned = true;
        }

        private void ReleaseFreezeIfOwned() {
            if (!freezeOwned) {
                return;
            }
            WorldFreezeSystem.Deactivate(FreezeReason);
            freezeOwned = false;
        }

        /// <summary>
        /// 极坐标命中检测：返回扇区索引或 -1
        /// </summary>
        private int HitTest() {
            if (sectors.Count <= 0) {
                return -1;
            }
            Vector2 offset = HalibutTheme.UIMouse - ScreenAnchor;
            float dist = offset.Length();
            if (dist < HalibutTheme.WheelDeadZoneR) {
                return -1;//死区内 = 不选择
            }
            if (sectors.Count == 1) {
                return 0;
            }
            float ang = MathF.Atan2(offset.Y, offset.X);
            float normalized = ang + MathHelper.PiOver2;
            while (normalized < 0) {
                normalized += MathHelper.TwoPi;
            }
            while (normalized >= MathHelper.TwoPi) {
                normalized -= MathHelper.TwoPi;
            }
            int count = sectors.Count;
            float sectorSize = MathHelper.TwoPi / count;
            float shifted = normalized + sectorSize * 0.5f;
            if (shifted >= MathHelper.TwoPi) {
                shifted -= MathHelper.TwoPi;
            }
            return Math.Clamp((int)(shifted / sectorSize), 0, count - 1);
        }

        /// <summary>
        /// 给指定扇区索引返回其覆盖的角度区间（首扇区中线朝正上方）
        /// </summary>
        public void GetSectorAngles(int idx, out float aStart, out float aEnd) {
            int count = sectors.Count;
            if (count <= 0) {
                aStart = 0;
                aEnd = 0;
                return;
            }
            float sectorSize = MathHelper.TwoPi / count;
            float mid = -MathHelper.PiOver2 + idx * sectorSize;
            aStart = mid - sectorSize * 0.5f + HalibutTheme.WheelSectorGap * 0.5f;
            aEnd = mid + sectorSize * 0.5f - HalibutTheme.WheelSectorGap * 0.5f;
        }
    }
}
