using CalamityOverhaul.Common;
using CalamityOverhaul.Content.QuestLogs;
using CalamityOverhaul.Content.ADV.EntrustManager;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

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
    }

    /// <summary>
    /// 义体技能雷达的核心状态机，所有输入/状态管理都集中在本类，<see cref="CyberwareSkillRadialUI"/>
    /// 仅负责把当前状态可视化
    /// <list type="bullet">
    ///   <item>按下 <see cref="CWRKeySystem.CyberwareSkill_Key"/> 打开雷达；按住期间显示扇区</item>
    ///   <item>鼠标方向决定悬停扇区，无需把鼠标移到目标位置</item>
    ///   <item>松开按键时按悬停扇区的类型决定触发方式：Instant 立即释放、Toggle 切换、Charge 按蓄力比例释放</item>
    ///   <item>Charge 类技能在悬停期间持续累积蓄力，切换扇区即清零；外部强制取消亦会清零</item>
    ///   <item>雷达打开期间向 <see cref="TimeGear"/> 注册 0.35 缓速档位，关闭即注销</item>
    ///   <item>仅装备 1 个主动义体时不弹出雷达，按键直接对该唯一技能进行触发</item>
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
        /// 当前雷达是否处于"已打开"逻辑状态
        /// </summary>
        public bool IsOpen { get; private set; }

        /// <summary>
        /// 0~1 的展开进度，由 <see cref="UpdateOpenProgress"/> 平滑推进
        /// </summary>
        public float OpenProgress { get; private set; }

        /// <summary>
        /// 当前雷达的屏幕锚点（每帧重新计算为玩家中心稍上方）
        /// </summary>
        public Vector2 ScreenAnchor { get; private set; }

        /// <summary>
        /// 当前装载的扇区列表，仅在 <see cref="IsOpen"/> 期间稳定
        /// </summary>
        public IReadOnlyList<CyberwareSkillRadialSector> Sectors => sectors;

        /// <summary>
        /// 当前悬停的扇区索引，-1 表示未命中任何扇区
        /// </summary>
        public int HoveredIndex { get; private set; } = -1;

        /// <summary>
        /// 当前悬停时长（实时帧数），切换扇区会清零
        /// </summary>
        public int HoverFrames { get; private set; }

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

        //时缓档位标识，避免硬编码字符串散落
        private const string TimeGearKey = "CyberwareRadial";
        //雷达全开后的时缓系数：0.35 让世界明显减速但仍保留战斗紧张感
        private const float TimeScaleAtFullOpen = 0.35f;
        //每帧展开/收起的 lerp 强度
        private const float OpenLerpRate = 0.22f;
        private const float CloseLerpRate = 0.28f;

        private readonly List<CyberwareSkillRadialSector> sectors = [];

        public override void PostUpdate() {
            //本控制器只负责本机玩家的雷达状态，远程玩家不参与
            if (Player.whoAmI != Main.myPlayer) {
                return;
            }
            Time += 1f / 60f;

            //每帧将雷达锚点重新对齐到玩家身上，让雷达"贴身"
            ScreenAnchor = Player.Center - Main.screenPosition;

            //玩家死亡 / 失去所有可释放义体 / 全屏 UI 介入时强制收起
            if (!CanRadialBeShown()) {
                if (IsOpen || OpenProgress > 0.01f) {
                    ForceClose();
                }
                UpdateOpenProgress();
                return;
            }

            //输入推进：JustPressed 决定是否开盘，JustReleased 决定结算
            ModKeybind keybind = CWRKeySystem.CyberwareSkill_Key;
            if (keybind != null) {
                if (!IsOpen && keybind.JustPressed) {
                    TryOpenOrTriggerSingle();
                }
                //雷达打开后实时刷新悬停 / 蓄力 / 鼠标占用
                if (IsOpen) {
                    UpdateOpenedFrame();
                }
                //松开瞬间结算
                if (IsOpen && keybind.JustReleased) {
                    ResolveAndClose();
                }
            }

            UpdateOpenProgress();
            UpdateTimeSlow();
        }

        /// <summary>
        /// 雷达是否应允许显示：玩家存活、未在全屏面板、且至少有一个义体提供主动技能
        /// </summary>
        private bool CanRadialBeShown() {
            if (Player == null || !Player.active || Player.dead) {
                return false;
            }
            if (QuestLog.Instance?.visible == true || QuestManagerUI.Instance?.IsOpen == true) {
                return false;
            }
            return HasAnyActiveSkill();
        }

        /// <summary>
        /// 玩家是否装备了至少一个 <see cref="BaseCyberware.ActiveSkill"/> 不为 null 的义体
        /// </summary>
        private bool HasAnyActiveSkill() {
            CyberwarePlayer cp = Player.GetModPlayer<CyberwarePlayer>();
            if (cp?.EquippedCyberwares == null) {
                return false;
            }
            for (int i = 0; i < CyberwarePlayer.SlotCount; i++) {
                if (cp.EquippedCyberwares[i]?.ModItem is BaseCyberware c && c.ActiveSkill != null) {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// 玩家刚按下按键：根据可用技能数量决定开盘还是直接触发
        /// </summary>
        private void TryOpenOrTriggerSingle() {
            List<CyberwareSkillRadialSector> built = BuildSectors();
            if (built.Count == 0) {
                return;
            }

            //单技能直触：仅当唯一的技能是 Instant 或 Toggle 时跳过雷达
            //Charge 类即便只有一个，也需要打开雷达以承接"按住-松开"语义
            if (built.Count == 1 && built[0].Skill.Kind != CyberwareSkillKind.Charge) {
                Player p = Player;
                CyberwareSkillBase only = built[0].Skill;
                if (only.IsReady) {
                    switch (only.Kind) {
                        case CyberwareSkillKind.Instant:
                            only.OnInstantTrigger(p);
                            break;
                        case CyberwareSkillKind.Toggle:
                            only.OnToggleTrigger(p);
                            break;
                    }
                }
                else {
                    //就绪条件不满足时给出短促失败反馈
                    SoundEngine.PlaySound(SoundID.MenuTick with { Pitch = -0.4f, Volume = 0.45f }, p.Center);
                }
                return;
            }

            //正常开盘
            sectors.Clear();
            sectors.AddRange(built);
            IsOpen = true;
            //Charge 单技能开盘时，自动把该扇区视为已悬停，避免玩家"看到 UI 时还在转圈"
            HoveredIndex = sectors.Count == 1 ? 0 : -1;
            HoverFrames = 0;
            //开盘音效
            SoundEngine.PlaySound(SoundID.MenuOpen with { Pitch = 0.2f, Volume = 0.5f });
        }

        /// <summary>
        /// 雷达已打开期间每帧调用：悬停检测 + 蓄力推进 + 鼠标占用 + 平滑参数
        /// </summary>
        private void UpdateOpenedFrame() {
            //鼠标占用：阻止背包左右键穿透到背景物品与世界交互
            Player.mouseInterface = true;
            Player.CWR().DontSwitchWeaponTime = 2;

            //命中检测
            int newHover = HitTest();
            if (newHover != HoveredIndex) {
                //切换扇区：取消旧扇区上正在累积的蓄力
                CancelHoveredCharge();
                HoveredIndex = newHover;
                HoverFrames = 0;
                if (HoveredIndex >= 0 && HoveredIndex < sectors.Count) {
                    //悬停时给一个短促的 hover 音效
                    SoundEngine.PlaySound(SoundID.MenuTick with { Pitch = 0.15f, Volume = 0.35f });
                }
            }

            //蓄力推进：仅对 Charge 且可用的扇区
            if (HoveredIndex >= 0 && HoveredIndex < sectors.Count) {
                CyberwareSkillBase skill = sectors[HoveredIndex].Skill;
                if (skill.Kind == CyberwareSkillKind.Charge && skill.IsReady) {
                    HoverFrames++;
                    int full = Math.Max(1, skill.FullChargeTicks);
                    float ratio = MathHelper.Clamp(HoverFrames / (float)full, 0f, 1f);
                    skill.RadialChargeRatio = ratio;
                    skill.OnChargeTick(Player, ratio);
                }
            }

            //每帧推进扇区的平滑悬停 / 可用过渡量
            for (int i = 0; i < sectors.Count; i++) {
                CyberwareSkillRadialSector sec = sectors[i];
                float hoverTarget = i == HoveredIndex ? 1f : 0f;
                sec.HoverAmount = MathHelper.Lerp(sec.HoverAmount, hoverTarget, 0.25f);
                sec.ReadyAmount = MathHelper.Lerp(sec.ReadyAmount, sec.Skill.IsReady ? 1f : 0f, 0.15f);
            }

            //失去关键前置条件（比如装备被卸下）时强制取消
            if (!HasAnyActiveSkill()) {
                ForceClose();
            }
        }

        /// <summary>
        /// 玩家松开按键瞬间：把当前悬停扇区按其类型触发后立刻收起雷达
        /// </summary>
        private void ResolveAndClose() {
            if (HoveredIndex >= 0 && HoveredIndex < sectors.Count) {
                CyberwareSkillBase skill = sectors[HoveredIndex].Skill;
                if (skill.IsReady) {
                    switch (skill.Kind) {
                        case CyberwareSkillKind.Instant:
                            skill.OnInstantTrigger(Player);
                            break;
                        case CyberwareSkillKind.Toggle:
                            skill.OnToggleTrigger(Player);
                            break;
                        case CyberwareSkillKind.Charge:
                            skill.OnChargeRelease(Player, skill.RadialChargeRatio);
                            break;
                    }
                }
                else if (skill.Kind == CyberwareSkillKind.Charge) {
                    //蓄力中途变成不可用（例如玩家在悬停期间离地），按取消处理
                    skill.OnChargeCancel(Player);
                }
            }
            CleanupAfterClose();
            IsOpen = false;
            HoveredIndex = -1;
            HoverFrames = 0;
            SoundEngine.PlaySound(SoundID.MenuClose with { Pitch = -0.1f, Volume = 0.45f });
        }

        /// <summary>
        /// 强制收起雷达，不触发任何技能效果（仅清理蓄力残留与扇区数据）
        /// </summary>
        public void ForceClose() {
            CancelHoveredCharge();
            CleanupAfterClose();
            IsOpen = false;
            HoveredIndex = -1;
            HoverFrames = 0;
        }

        private void CancelHoveredCharge() {
            if (HoveredIndex < 0 || HoveredIndex >= sectors.Count) {
                return;
            }
            CyberwareSkillBase skill = sectors[HoveredIndex].Skill;
            if (skill.Kind == CyberwareSkillKind.Charge) {
                skill.RadialChargeRatio = 0f;
                skill.OnChargeCancel(Player);
            }
        }

        private void CleanupAfterClose() {
            //收起前把所有扇区的蓄力状态清零，防止下一次开盘时残留进度
            for (int i = 0; i < sectors.Count; i++) {
                sectors[i].Skill.RadialChargeRatio = 0f;
            }
        }

        private void UpdateOpenProgress() {
            float target = IsOpen ? 1f : 0f;
            float rate = IsOpen ? OpenLerpRate : CloseLerpRate;
            OpenProgress = MathHelper.Lerp(OpenProgress, target, rate);
            if (MathF.Abs(OpenProgress - target) < 0.003f) {
                OpenProgress = target;
            }
        }

        private void UpdateTimeSlow() {
            //展开进度 < 0.05 时取消时缓档位；否则线性插值缓速强度
            if (OpenProgress < 0.05f) {
                TimeGear.Unregister(TimeGearKey);
                return;
            }
            float scale = MathHelper.Lerp(1f, TimeScaleAtFullOpen, OpenProgress);
            TimeGear.Register(TimeGearKey, scale);
        }

        /// <summary>
        /// 极坐标命中检测：给定鼠标到锚点的偏移，返回扇区索引或 -1
        /// <br/>单扇区情况下永远返回 0（无需鼠标方向输入）
        /// </summary>
        private int HitTest() {
            if (sectors.Count <= 0) {
                return -1;
            }
            if (sectors.Count == 1) {
                return 0;
            }
            Vector2 mouseScreen = new(Main.mouseX, Main.mouseY);
            Vector2 offset = mouseScreen - ScreenAnchor;
            float dist = offset.Length();
            //死区内不判断任何扇区，允许玩家"按下不动"取消选择
            if (dist < DeadZoneRadius) {
                return -1;
            }
            float ang = MathF.Atan2(offset.Y, offset.X);
            //旋转使顶部对应扇区 0：减去 -PiOver2 即可让正上方落在扇区起点
            float normalized = ang + MathHelper.PiOver2;
            while (normalized < 0) {
                normalized += MathHelper.TwoPi;
            }
            while (normalized >= MathHelper.TwoPi) {
                normalized -= MathHelper.TwoPi;
            }
            int count = sectors.Count;
            float sectorSize = MathHelper.TwoPi / count;
            //让每个扇区"以中线为中心"覆盖一个 sectorSize 的范围，避免初始扇区有偏移
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
        /// 扫描装备的义体，构建当前帧的扇区列表
        /// <br/>结果顺序按槽位下标稳定，避免玩家穿戴顺序变化导致雷达方向反复横跳
        /// </summary>
        private List<CyberwareSkillRadialSector> BuildSectors() {
            List<CyberwareSkillRadialSector> result = [];
            CyberwarePlayer cp = Player.GetModPlayer<CyberwarePlayer>();
            if (cp?.EquippedCyberwares == null) {
                return result;
            }
            for (int i = 0; i < CyberwarePlayer.SlotCount; i++) {
                Item item = cp.EquippedCyberwares[i];
                if (item?.ModItem is not BaseCyberware cyber || cyber.ActiveSkill == null) {
                    continue;
                }
                result.Add(new CyberwareSkillRadialSector {
                    Skill = cyber.ActiveSkill,
                    SourceItem = item,
                    SourceCyberware = cyber,
                    HoverAmount = 0f,
                    ReadyAmount = cyber.ActiveSkill.IsReady ? 1f : 0f,
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
            //扇区中线对齐"-PiOver2 + idx*sectorSize"，半宽决定开口
            float mid = -MathHelper.PiOver2 + idx * sectorSize;
            aStart = mid - sectorSize * 0.5f;
            aEnd = mid + sectorSize * 0.5f;
        }
    }
}
