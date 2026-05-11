using CalamityOverhaul.Common;
using CalamityOverhaul.Content.ADV.EntrustManager;
using CalamityOverhaul.Content.HackTimes;
using CalamityOverhaul.Content.QuestLogs;
using CalamityOverhaul.Content.TimeFreezes;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace CalamityOverhaul.Content.Cyberwares.Skills
{
    /// <summary>
    /// 雷达扇区的运行时条目，仅供本机使用
    /// </summary>
    internal sealed class CyberwareSkillRadialSector
    {
        public CyberwareSkillBase Skill;
        public Item SourceItem;
        public BaseCyberware SourceCyberware;
        //平滑跟随的悬停强度（0 = 未悬停，1 = 完全悬停），用于绘制扇区高亮
        public float HoverAmount;
        //平滑跟随的"可用 / 不可用"灰度，用于扇区灰显过渡
        public float ReadyAmount;
        //平滑跟随的"当前选中"高亮强度（0 = 不是当前选中，1 = 是）
        public float SelectedAmount;
    }

    /// <summary>
    /// 义体技能雷达 + 触发的核心状态机（双键模型 v2）
    /// <list type="bullet">
    ///   <item><b>雷达键</b> <see cref="CWRKeySystem.CyberwareRadial_Key"/>：按住打开雷达进入子弹时间，
    ///         鼠标移到扇区上 <b>左键单击</b> 即可把对应义体设为"当前技能"；
    ///         右键关闭雷达不做任何改动；松开雷达键也会关闭</item>
    ///   <item><b>触发键</b> <see cref="CWRKeySystem.CyberwareSkill_Key"/>：直接触发当前选中的义体技能；
    ///         不会打开雷达 / 不会进入子弹时间。瞄点取触发键按下的真实鼠标位置，
    ///         <b>方向类技能（如单分子线）从此不再受雷达鼠标占用影响</b></item>
    ///   <item>蓄力类技能（Charge）由触发键独立承接 —— 按下进入蓄力，松开结算</item>
    ///   <item>当前选中的技能通过 <see cref="CurrentSkillId"/> 持久化到角色存档；
    ///         若存档中的义体未装备，则回退到首个已装备的可用义体</item>
    ///   <item>骇客时间已激活时拒绝开盘，避免两个子弹时间打架</item>
    /// </list>
    /// </summary>
    internal class CyberwareSkillRadialController : ModPlayer
    {
        /// <summary>
        /// 本类是本机玩家的轻量单例视图，远程玩家不会维护这些字段
        /// </summary>
        public static CyberwareSkillRadialController LocalInstance =>
            Main.LocalPlayer?.GetModPlayer<CyberwareSkillRadialController>();

        /// <summary>
        /// 雷达是否已打开（子弹时间窗口期 + UI 显示）
        /// </summary>
        public bool IsOpen { get; private set; }

        /// <summary>
        /// 0~1 的展开进度，由 <see cref="UpdateOpenProgress"/> 平滑推进
        /// </summary>
        public float OpenProgress { get; private set; }

        /// <summary>
        /// 触发键正按住，且当前技能是 Charge 类型时为 true；
        /// 蓄力进度通过 <see cref="ChargeFrames"/> 反映
        /// </summary>
        public bool IsCharging { get; private set; }

        /// <summary>
        /// 当前正在蓄力的累计帧数（仅 <see cref="IsCharging"/> 为 true 时有意义）
        /// </summary>
        public int ChargeFrames { get; private set; }

        /// <summary>
        /// 当前选中的义体技能的稳定标识。空字符串表示未选 / 待自动选择
        /// </summary>
        public string CurrentSkillId { get; private set; } = string.Empty;

        /// <summary>
        /// 当前雷达的屏幕锚点（固定为屏幕坐标系下的中央偏下）
        /// </summary>
        public Vector2 ScreenAnchor { get; private set; }

        /// <summary>
        /// 让 UI Draw 阶段同步写回锚点，避免 ModPlayer.PostUpdate 与 Main.Draw 之间
        /// 因任何潜在尺寸变化（窗口拖拽 / 全屏切换帧）而错位
        /// </summary>
        public void SetScreenAnchor(Vector2 anchor) => ScreenAnchor = anchor;

        /// <summary>
        /// 当前装载的扇区列表，仅在 <see cref="IsOpen"/> 期间稳定
        /// </summary>
        public IReadOnlyList<CyberwareSkillRadialSector> Sectors => sectors;

        /// <summary>
        /// 当前悬停的扇区索引，-1 表示未命中任何扇区
        /// </summary>
        public int HoveredIndex { get; private set; } = -1;

        /// <summary>
        /// 全局动画 time，由本类驱动，UI 据此推进扫光等效果
        /// </summary>
        public float Time { get; private set; }

        /// <summary>
        /// 雷达半径几何常量，UI 绘制与命中检测共享
        /// </summary>
        public const float InnerRadius = 60f;
        public const float OuterRadius = 110f;
        public const float DeadZoneRadius = 36f;
        public const float IconRadius = (InnerRadius + OuterRadius) * 0.5f;

        /// <summary>
        /// 锚点 Y 相对屏幕高度的位置比例
        /// </summary>
        public const float ScreenAnchorYRatio = 0.72f;

        //WorldFreezeSystem 的 reason 标签：同名重复调用幂等
        private const string FreezeReason = "CyberwareRadial";
        //每帧展开/收起的 lerp 强度
        private const float OpenLerpRate = 0.22f;
        private const float CloseLerpRate = 0.28f;

        private readonly List<CyberwareSkillRadialSector> sectors = [];

        //本帧是否由本控制器持有 WorldFreezeSystem 的 reason
        private bool freezeOwned;

        //当前帧解析出的"当前技能"快照，避免在同一帧反复扫装备
        //在 PostUpdate 开头刷新
        private CyberwareSkillBase resolvedCurrentSkill;
        private BaseCyberware resolvedCurrentCyberware;

        /// <summary>
        /// 当前帧实际生效的"当前技能"，可能与 <see cref="CurrentSkillId"/> 不同步
        /// （例如保存的 id 已不在装备槽里，会自动 fallback 到首个可用义体）
        /// </summary>
        public CyberwareSkillBase ResolvedCurrentSkill => resolvedCurrentSkill;

        /// <summary>
        /// 与 <see cref="ResolvedCurrentSkill"/> 对应的义体本体
        /// </summary>
        public BaseCyberware ResolvedCurrentCyberware => resolvedCurrentCyberware;

        public override void PostUpdate() {
            //本控制器只负责本机玩家的雷达状态，远程玩家不参与
            if (Player.whoAmI != Main.myPlayer) {
                return;
            }
            Time += 1f / 60f;

            ScreenAnchor = new Vector2(
                Main.screenWidth * 0.5f,
                Main.screenHeight * ScreenAnchorYRatio);

            ResolveCurrentSkill();

            //雷达 / 蓄力都需要"有任何主动义体"作为前置条件
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
            //仅保存"上次玩家手动选中的技能 id"，不保存运行时状态
            if (!string.IsNullOrEmpty(CurrentSkillId)) {
                tag["CWR_Cyberware_CurrentSkillId"] = CurrentSkillId;
            }
        }

        public override void LoadData(TagCompound tag) {
            if (tag.ContainsKey("CWR_Cyberware_CurrentSkillId")) {
                CurrentSkillId = tag.GetString("CWR_Cyberware_CurrentSkillId") ?? string.Empty;
            }
        }

        /// <summary>
        /// 把保存的 <see cref="CurrentSkillId"/> 解析为当前实际生效的技能 + 其所属义体
        /// <list type="bullet">
        ///   <item>id 命中已装备义体 → 使用该技能</item>
        ///   <item>id 不存在 / 不在装备槽 → fallback 到首个有 <see cref="BaseCyberware.ActiveSkill"/> 的义体（不写回 id，保留用户的偏好）</item>
        ///   <item>玩家没装备任何主动义体 → 返回 null</item>
        /// </list>
        /// </summary>
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
            //保存的 id 不在装备里 → 临时 fallback；保存值不变，等玩家重新装回时仍生效
            if (first != null) {
                resolvedCurrentSkill = first.ActiveSkill;
                resolvedCurrentCyberware = first;
            }
        }

        /// <summary>
        /// 雷达是否允许显示：玩家存活、未在全屏面板、且至少有一个义体提供主动技能
        /// </summary>
        private bool CanRadialBeShown() {
            if (Player == null || !Player.active || Player.dead) {
                return false;
            }
            if (QuestLog.Instance?.visible == true || QuestManagerUI.Instance?.IsOpen == true) {
                return false;
            }
            return resolvedCurrentSkill != null;
        }

        /// <summary>
        /// 处理雷达键：按下 → 开盘 + 子弹时间；松开 → 关盘
        /// </summary>
        private void HandleRadialKey() {
            ModKeybind key = CWRKeySystem.CyberwareRadial_Key;
            if (key == null) {
                return;
            }

            if (!IsOpen && key.JustPressed) {
                //蓄力期间禁止开盘，避免两个状态机互相打架
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

        /// <summary>
        /// 处理触发键：触发当前选中的技能；雷达开盘期间不响应（避免误触）
        /// </summary>
        private void HandleSkillKey() {
            ModKeybind key = CWRKeySystem.CyberwareSkill_Key;
            if (key == null) {
                return;
            }
            //雷达开盘期间，触发键被锁定（玩家应该先关闭雷达）
            if (IsOpen) {
                return;
            }

            CyberwareSkillBase skill = resolvedCurrentSkill;
            //蓄力中但技能突然消失（玩家把当前义体卸了 / 切换槽位）→ 立即兜底取消，
            //不能直接 return，否则 IsCharging 残留导致下一次按键无响应
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

        /// <summary>
        /// 雷达开盘期间的鼠标输入：悬停 + 左键选中 + 右键关盘
        /// </summary>
        private void HandleRadialMouse() {
            if (!IsOpen) {
                return;
            }

            //鼠标占用：阻止背包左右键穿透到背景物品与世界交互
            Player.mouseInterface = true;
            Player.CWR().DontSwitchWeaponTime = 2;

            //命中检测
            int newHover = HitTest();
            if (newHover != HoveredIndex) {
                HoveredIndex = newHover;
                if (HoveredIndex >= 0 && HoveredIndex < sectors.Count) {
                    SoundEngine.PlaySound(SoundID.MenuTick with { Pitch = 0.15f, Volume = 0.35f });
                }
            }

            //左键单击 → 选中悬停扇区（雷达保持开启，玩家可以继续切换）
            //Main.mouseLeftRelease 是 Terraria 标准的"上一帧鼠标未按"标记，
            //配合 Main.mouseLeft 判定 just-pressed，无需手动跟踪 prev 状态
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
                //消耗本帧的"刚按下"标志，避免点选同时被穿透到背景物品操作
                Main.mouseLeftRelease = false;
            }

            //右键 → 立即关盘且不改动选择
            if (Main.mouseRight && Main.mouseRightRelease) {
                Main.mouseRightRelease = false;
                CloseRadial(silentSound: false);
                return;
            }

            //每帧推进扇区的平滑悬停 / 可用 / 选中过渡量
            //selected 用"当前实际生效的技能"做判定（包含 id 为空时的 fallback），
            //避免存档 id 缺失时雷达不显示任何"选中"标记的视觉空白
            string activeId = resolvedCurrentSkill?.Identifier ?? CurrentSkillId;
            for (int i = 0; i < sectors.Count; i++) {
                CyberwareSkillRadialSector sec = sectors[i];
                float hoverTarget = i == HoveredIndex ? 1f : 0f;
                sec.HoverAmount = MathHelper.Lerp(sec.HoverAmount, hoverTarget, 0.25f);
                sec.ReadyAmount = MathHelper.Lerp(sec.ReadyAmount, sec.Skill.IsReady ? 1f : 0f, 0.15f);
                bool isSelected = string.Equals(sec.Skill.Identifier, activeId, StringComparison.Ordinal);
                sec.SelectedAmount = MathHelper.Lerp(sec.SelectedAmount, isSelected ? 1f : 0f, 0.25f);
            }

            //装备在雷达开启中被卸下 → 自动关
            if (resolvedCurrentSkill == null) {
                ForceCloseRadial();
            }
        }

        /// <summary>
        /// 尝试打开雷达
        /// </summary>
        private void TryOpenRadial() {
            //已存在更高优先级的子弹时间时拒绝开盘
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

            //请求世界冻结：单人下进入子弹时间；多人下被忽略，仅余 UI
            AcquireFreezeIfNeeded();
            SoundEngine.PlaySound(SoundID.MenuOpen with { Pitch = 0.2f, Volume = 0.5f });
        }

        /// <summary>
        /// 关闭雷达（玩家主动 / 释放键 / 右键），不修改 CurrentSkillId
        /// </summary>
        private void CloseRadial(bool silentSound) {
            ReleaseFreezeIfOwned();
            IsOpen = false;
            HoveredIndex = -1;
            if (!silentSound) {
                SoundEngine.PlaySound(SoundID.MenuClose with { Pitch = -0.1f, Volume = 0.45f });
            }
        }

        /// <summary>
        /// 强制关闭雷达，用于死亡 / 卸下装备 / 全屏 UI 介入等异常路径
        /// </summary>
        public void ForceCloseRadial() {
            ReleaseFreezeIfOwned();
            IsOpen = false;
            HoveredIndex = -1;
        }

        /// <summary>
        /// 取消蓄力（外部调用 + 异常路径都会走到这里），幂等
        /// </summary>
        public void CancelChargeIfAny() {
            if (!IsCharging) {
                return;
            }
            //尽量调用一次取消回调，让技能侧清理粒子等资源
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

        /// <summary>
        /// 极坐标命中检测：给定鼠标到锚点的偏移，返回扇区索引或 -1
        /// <br/>单扇区情况下要求鼠标至少越过死区（玩家有"什么都不选"的选项）
        /// </summary>
        private int HitTest() {
            if (sectors.Count <= 0) {
                return -1;
            }
            Vector2 mouseScreen = new(Main.mouseX, Main.mouseY);
            Vector2 offset = mouseScreen - ScreenAnchor;
            float dist = offset.Length();
            //死区内不判定任何扇区，玩家把鼠标拉回中心 = 取消选择
            if (dist < DeadZoneRadius) {
                return -1;
            }
            if (sectors.Count == 1) {
                return 0;
            }
            float ang = MathF.Atan2(offset.Y, offset.X);
            //旋转使顶部对应扇区 0
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

        /// <summary>
        /// 扫描装备的义体，构建当前帧的扇区列表，顺序按槽位下标稳定
        /// </summary>
        private List<CyberwareSkillRadialSector> BuildSectors() {
            List<CyberwareSkillRadialSector> result = [];
            CyberwarePlayer cp = Player.GetModPlayer<CyberwarePlayer>();
            if (cp?.EquippedCyberwares == null) {
                return result;
            }
            //开盘瞬间的"选中"初值同样基于已解析的当前技能，让 fallback 路径也能高亮
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

        /// <summary>
        /// 给指定扇区索引返回其覆盖的角度区间（屏幕坐标系，向右为 0，向下为正）
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
            aStart = mid - sectorSize * 0.5f;
            aEnd = mid + sectorSize * 0.5f;
        }
    }
}
