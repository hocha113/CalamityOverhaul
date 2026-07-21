using CalamityOverhaul.Common;
using CalamityOverhaul.Content.EntrustManager;
using CalamityOverhaul.Content.HackTimes;
using CalamityOverhaul.Content.QuestLogs;
using CalamityOverhaul.Content.TimeFreezes;
using InnoVault.UIHandles;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace CalamityOverhaul.Content.Cyberwares.Skills
{
    /// <summary>雷达扇区运行时条目，仅本机</summary>
    internal sealed class CyberwareSkillRadialSector
    {
        public CyberwareSkillBase Skill;
        public Item SourceItem;
        public BaseCyberware SourceCyberware;
        //悬停强度 0~1
        public float HoverAmount;
        //可用灰度 0~1
        public float ReadyAmount;
        //选中高亮 0~1
        public float SelectedAmount;
    }

    /// <summary>
    /// 雷达+触发状态机
    /// <br/><see cref="CWRKeySystem.CyberwareRadial_Key"/> 按住开盘；<see cref="CWRKeySystem.CyberwareSkill_Key"/> 触发当前技
    /// <br/><see cref="CurrentSkillId"/> 存档，未装则 fallback 首个；HackTime 激活拒开盘
    /// </summary>
    internal class CyberwareSkillRadialController : ModPlayer
    {
        /// <summary>本机轻量单例视图</summary>
        public static CyberwareSkillRadialController LocalInstance =>
            Main.LocalPlayer?.GetModPlayer<CyberwareSkillRadialController>();

        /// <summary>雷达已开（子弹时间+UI）</summary>
        public bool IsOpen { get; private set; }

        /// <summary>展开进度 0~1</summary>
        public float OpenProgress { get; private set; }

        /// <summary>触发键按住且 Charge 类型</summary>
        public bool IsCharging { get; private set; }

        /// <summary>蓄力累计帧，IsCharging 时有效</summary>
        public int ChargeFrames { get; private set; }

        /// <summary>当前选中技能 id，空=未选/待自动</summary>
        public string CurrentSkillId { get; private set; } = string.Empty;

        /// <summary>屏幕锚点，中央偏下</summary>
        public Vector2 ScreenAnchor { get; private set; }

        /// <summary>Draw 阶段写回锚点，防窗口尺寸变化错位</summary>
        public void SetScreenAnchor(Vector2 anchor) => ScreenAnchor = anchor;

        /// <summary>扇区列表，IsOpen 期间稳定</summary>
        public IReadOnlyList<CyberwareSkillRadialSector> Sectors => sectors;

        /// <summary>悬停扇区索引，-1 无</summary>
        public int HoveredIndex { get; private set; } = -1;

        /// <summary>动画时钟，UI 扫光用</summary>
        public float Time { get; private set; }

        /// <summary>雷达几何常量，UI/命中共享</summary>
        public const float InnerRadius = 60f;
        public const float OuterRadius = 110f;
        public const float DeadZoneRadius = 36f;
        public const float IconRadius = (InnerRadius + OuterRadius) * 0.5f;

        /// <summary>锚点 Y 占屏高比例</summary>
        public const float ScreenAnchorYRatio = 0.72f;

        //WorldFreeze reason，重复调用幂等
        private const string FreezeReason = "CyberwareRadial";
        //展开/收起 lerp
        private const float OpenLerpRate = 0.22f;
        private const float CloseLerpRate = 0.28f;

        private readonly List<CyberwareSkillRadialSector> sectors = [];

        //本帧持有 WorldFreeze reason
        private bool freezeOwned;

        //当前技能快照，PostUpdate 开头刷新
        private CyberwareSkillBase resolvedCurrentSkill;
        private BaseCyberware resolvedCurrentCyberware;

        /// <summary>当前生效技能，可与 CurrentSkillId 不同步（fallback）</summary>
        public CyberwareSkillBase ResolvedCurrentSkill => resolvedCurrentSkill;

        /// <summary>ResolvedCurrentSkill 对应义体</summary>
        public BaseCyberware ResolvedCurrentCyberware => resolvedCurrentCyberware;

        public override void PostUpdate() {
            //仅本机
            if (Player.whoAmI != Main.myPlayer) {
                return;
            }
            Time += 1f / 60f;

            ScreenAnchor = new Vector2(
                Main.screenWidth * 0.5f,
                Main.screenHeight * ScreenAnchorYRatio);

            ResolveCurrentSkill();

            //需有主动义体
            if (!CanRadialBeShown()) {
                CancelChargeIfAny();
                if (IsOpen || OpenProgress > 0.01f) {
                    ForceCloseRadial();
                }
                UpdateOpenProgress();
                return;
            }

            HandleRadialKey();
            HandleSkillKey();
            HandleRadialMouse();
            UpdateOpenProgress();
        }

        public override void UpdateDead() {
            if (Player.whoAmI != Main.myPlayer) {
                return;
            }
            CancelChargeIfAny();
            if (IsOpen || OpenProgress > 0.01f) {
                ForceCloseRadial();
            }
            ReleaseFreezeIfOwned();
            UpdateOpenProgress();
        }

        public override void SaveData(TagCompound tag) {
            //只存手动选中 id
            if (!string.IsNullOrEmpty(CurrentSkillId)) {
                tag["CWR_Cyberware_CurrentSkillId"] = CurrentSkillId;
            }
        }

        public override void LoadData(TagCompound tag) {
            if (tag.TryGet<string>("CWR_Cyberware_CurrentSkillId", out var skillId)) {
                CurrentSkillId = skillId ?? string.Empty;
            }
        }

        /// <summary>CurrentSkillId→技能+义体；未装备 fallback 首个，不写回 id</summary>
        private void ResolveCurrentSkill() {
            resolvedCurrentSkill = null;
            resolvedCurrentCyberware = null;
            CyberwarePlayer cp = Player.GetModPlayer<CyberwarePlayer>();
            if (cp?.EquippedCyberwares == null) {
                return;
            }
            BaseCyberware first = null;
            for (int i = 0; i < CyberwarePlayer.SlotCount; i++) {
                if (cp.EquippedCyberwares[i]?.ModItem is not BaseCyberware c || c.ActiveSkill == null) {
                    continue;
                }
                first ??= c;
                if (string.Equals(c.ActiveSkill.Identifier, CurrentSkillId, StringComparison.Ordinal)) {
                    resolvedCurrentSkill = c.ActiveSkill;
                    resolvedCurrentCyberware = c;
                    return;
                }
            }
            //存档 id 未装备→临时 fallback，保留存档值
            if (first != null) {
                resolvedCurrentSkill = first.ActiveSkill;
                resolvedCurrentCyberware = first;
            }
        }

        /// <summary>存活、非全屏 UI、有主动技能</summary>
        private bool CanRadialBeShown() {
            if (Player == null || !Player.active || Player.dead) {
                return false;
            }
            if (QuestLog.Instance?.visible == true || QuestManagerUI.Instance?.IsOpen == true) {
                return false;
            }
            return resolvedCurrentSkill != null;
        }

        /// <summary>雷达键，按下开盘+时停，松开关</summary>
        private void HandleRadialKey() {
            ModKeybind key = CWRKeySystem.CyberwareRadial_Key;
            if (key == null) {
                return;
            }

            if (!IsOpen && key.JustPressed) {
                //蓄力中禁止开盘
                if (IsCharging) {
                    SoundEngine.PlaySound(SoundID.MenuTick with { Pitch = -0.5f, Volume = 0.4f });
                    return;
                }
                TryOpenRadial();
                return;
            }

            if (IsOpen && key.JustReleased) {
                CloseRadial(silentSound: false);
            }
        }

        /// <summary>触发键，开盘期间不响应</summary>
        private void HandleSkillKey() {
            ModKeybind key = CWRKeySystem.CyberwareSkill_Key;
            if (key == null) {
                return;
            }
            //开盘锁定触发键
            if (IsOpen) {
                return;
            }

            CyberwareSkillBase skill = resolvedCurrentSkill;
            //蓄力中技能消失→兜底取消，防 IsCharging 残留
            if (IsCharging && skill == null) {
                CancelChargeIfAny();
                return;
            }
            if (skill == null) {
                return;
            }

            if (!IsCharging && key.JustPressed) {
                if (!skill.IsReady) {
                    SoundEngine.PlaySound(SoundID.MenuTick with { Pitch = -0.4f, Volume = 0.45f }, Player.Center);
                    return;
                }
                switch (skill.Kind) {
                    case CyberwareSkillKind.Instant:
                        skill.OnInstantTrigger(Player);
                        break;
                    case CyberwareSkillKind.Toggle:
                        skill.OnToggleTrigger(Player);
                        break;
                    case CyberwareSkillKind.Charge:
                        IsCharging = true;
                        ChargeFrames = 0;
                        skill.RadialChargeRatio = 0f;
                        break;
                }
                return;
            }

            if (IsCharging) {
                //蓄力推进
                ChargeFrames++;
                int full = Math.Max(1, skill.FullChargeTicks);
                float ratio = MathHelper.Clamp(ChargeFrames / (float)full, 0f, 1f);
                skill.RadialChargeRatio = ratio;
                skill.OnChargeTick(Player, ratio);

                //松开 → 结算
                if (key.JustReleased) {
                    if (skill.IsReady) {
                        skill.OnChargeRelease(Player, ratio);
                    }
                    else {
                        skill.OnChargeCancel(Player);
                    }
                    IsCharging = false;
                    ChargeFrames = 0;
                    skill.RadialChargeRatio = 0f;
                    return;
                }

                //装备被卸下 / 技能在蓄力中变得不可用 → 取消
                if (resolvedCurrentSkill == null) {
                    CancelChargeIfAny();
                }
            }
        }

        /// <summary>雷达鼠标，悬停/左选/右关</summary>
        private void HandleRadialMouse() {
            if (!IsOpen) {
                return;
            }

            //mouseInterface 防穿透
            Player.mouseInterface = true;
            UIInputGuard.SuppressWeaponSwitch();

            //命中检测
            int newHover = HitTest();
            if (newHover != HoveredIndex) {
                HoveredIndex = newHover;
                if (HoveredIndex >= 0 && HoveredIndex < sectors.Count) {
                    SoundEngine.PlaySound(SoundID.MenuTick with { Pitch = 0.15f, Volume = 0.35f });
                }
            }

            //左键选中悬停扇区，雷达保持开
            if (Main.mouseLeft && Main.mouseLeftRelease
                && HoveredIndex >= 0 && HoveredIndex < sectors.Count) {
                CyberwareSkillRadialSector sec = sectors[HoveredIndex];
                string newId = sec.Skill.Identifier;
                if (!string.Equals(newId, CurrentSkillId, StringComparison.Ordinal)) {
                    CurrentSkillId = newId;
                    //重新解析以驱动 UI 高亮立即跟进
                    ResolveCurrentSkill();
                    SoundEngine.PlaySound(SoundID.MenuTick with { Pitch = 0.45f, Volume = 0.6f });
                }
                else {
                    SoundEngine.PlaySound(SoundID.MenuTick with { Pitch = 0.25f, Volume = 0.4f });
                }
                //消耗 mouseLeftRelease 防穿透
                Main.mouseLeftRelease = false;
            }

            //右键 → 立即关盘且不改动选择
            if (Main.mouseRight && Main.mouseRightRelease) {
                Main.mouseRightRelease = false;
                CloseRadial(silentSound: false);
                return;
            }

            //扇区 hover/ready/selected 平滑；selected 用 resolved 含 fallback
            string activeId = resolvedCurrentSkill?.Identifier ?? CurrentSkillId;
            for (int i = 0; i < sectors.Count; i++) {
                CyberwareSkillRadialSector sec = sectors[i];
                float hoverTarget = i == HoveredIndex ? 1f : 0f;
                sec.HoverAmount = MathHelper.Lerp(sec.HoverAmount, hoverTarget, 0.25f);
                sec.ReadyAmount = MathHelper.Lerp(sec.ReadyAmount, sec.Skill.IsReady ? 1f : 0f, 0.15f);
                bool isSelected = string.Equals(sec.Skill.Identifier, activeId, StringComparison.Ordinal);
                sec.SelectedAmount = MathHelper.Lerp(sec.SelectedAmount, isSelected ? 1f : 0f, 0.25f);
            }

            //装备卸下自动关盘
            if (resolvedCurrentSkill == null) {
                ForceCloseRadial();
            }
        }

        /// <summary>尝试开雷达</summary>
        private void TryOpenRadial() {
            //骇客时间激活拒绝开盘
            if (HackTime.Active) {
                SoundEngine.PlaySound(SoundID.MenuTick with { Pitch = -0.5f, Volume = 0.5f }, Player.Center);
                return;
            }

            List<CyberwareSkillRadialSector> built = BuildSectors();
            if (built.Count == 0) {
                return;
            }

            sectors.Clear();
            sectors.AddRange(built);
            IsOpen = true;
            HoveredIndex = -1;

            //单人 WorldFreeze 子弹时间
            AcquireFreezeIfNeeded();
            SoundEngine.PlaySound(SoundID.MenuOpen with { Pitch = 0.2f, Volume = 0.5f });
        }

        /// <summary>关盘，不改 CurrentSkillId</summary>
        private void CloseRadial(bool silentSound) {
            ReleaseFreezeIfOwned();
            IsOpen = false;
            HoveredIndex = -1;
            if (!silentSound) {
                SoundEngine.PlaySound(SoundID.MenuClose with { Pitch = -0.1f, Volume = 0.45f });
            }
        }

        /// <summary>强制关盘（死亡/卸装/全屏 UI）</summary>
        public void ForceCloseRadial() {
            ReleaseFreezeIfOwned();
            IsOpen = false;
            HoveredIndex = -1;
        }

        /// <summary>取消蓄力，幂等</summary>
        public void CancelChargeIfAny() {
            if (!IsCharging) {
                return;
            }
            //取消回调清粒子
            resolvedCurrentSkill?.OnChargeCancel(Player);
            if (resolvedCurrentSkill != null) {
                resolvedCurrentSkill.RadialChargeRatio = 0f;
            }
            IsCharging = false;
            ChargeFrames = 0;
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
            if (VaultUtils.isSinglePlayer)//仅单人时停
                WorldFreezeSystem.Activate(FreezeReason);
            freezeOwned = true;
        }

        private void ReleaseFreezeIfOwned() {
            if (!freezeOwned) {
                return;
            }
            WorldFreezeSystem.Deactivate(FreezeReason);
            freezeOwned = false;
        }

        /// <summary>极坐标命中，死区内 -1</summary>
        private int HitTest() {
            if (sectors.Count <= 0) {
                return -1;
            }
            Vector2 mouseScreen = new(Main.mouseX, Main.mouseY);
            Vector2 offset = mouseScreen - ScreenAnchor;
            float dist = offset.Length();
            //死区内不命中
            if (dist < DeadZoneRadius) {
                return -1;
            }
            if (sectors.Count == 1) {
                return 0;
            }
            float ang = MathF.Atan2(offset.Y, offset.X);
            //顶部对齐扇区 0
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
            int idx = (int)(shifted / sectorSize);
            if (idx < 0) {
                idx = 0;
            }
            if (idx >= count) {
                idx = count - 1;
            }
            return idx;
        }

        /// <summary>按槽位序构建扇区</summary>
        private List<CyberwareSkillRadialSector> BuildSectors() {
            List<CyberwareSkillRadialSector> result = [];
            CyberwarePlayer cp = Player.GetModPlayer<CyberwarePlayer>();
            if (cp?.EquippedCyberwares == null) {
                return result;
            }
            //开盘选中初值含 fallback
            string activeId = resolvedCurrentSkill?.Identifier ?? CurrentSkillId;
            for (int i = 0; i < CyberwarePlayer.SlotCount; i++) {
                Item item = cp.EquippedCyberwares[i];
                if (item?.ModItem is not BaseCyberware cyber || cyber.ActiveSkill == null) {
                    continue;
                }
                bool isSelected = string.Equals(cyber.ActiveSkill.Identifier, activeId, StringComparison.Ordinal);
                result.Add(new CyberwareSkillRadialSector {
                    Skill = cyber.ActiveSkill,
                    SourceItem = item,
                    SourceCyberware = cyber,
                    HoverAmount = 0f,
                    ReadyAmount = cyber.ActiveSkill.IsReady ? 1f : 0f,
                    SelectedAmount = isSelected ? 1f : 0f,
                });
            }
            return result;
        }

        /// <summary>扇区角度区间，屏幕系向右 0 向下正</summary>
        public void GetSectorAngles(int idx, out float aStart, out float aEnd) {
            int count = sectors.Count;
            if (count <= 0) {
                aStart = 0;
                aEnd = 0;
                return;
            }
            float sectorSize = MathHelper.TwoPi / count;
            float mid = -MathHelper.PiOver2 + idx * sectorSize;
            aStart = mid - sectorSize * 0.5f;
            aEnd = mid + sectorSize * 0.5f;
        }
    }
}
