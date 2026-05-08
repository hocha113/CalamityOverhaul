using CalamityOverhaul.Common;
using CalamityOverhaul.Content.ADV.EntrustManager;
using CalamityOverhaul.Content.ADV.Scenarios;
using CalamityOverhaul.Content.ADV.Scenarios.Shepel;
using CalamityOverhaul.Content.Cyberwares.UIs;
using CalamityOverhaul.Content.HackTimes;
using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Cyberspaces;
using CalamityOverhaul.Content.QuestLogs;
using CalamityOverhaul.Content.RAMSystems;
using InnoVault.UIHandles;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.UI
{
    /// <summary>
    /// SHPC启动HUD主入口
    /// 屏幕左下角能量核心，点击后向右上方扇形展开多个交互按钮
    /// 仅在玩家手持SHPC时显示，避让全屏UI
    /// 全部使用程序化绘制，不依赖纹理资产
    /// </summary>
    internal class SHPCUI : UIHandle, ILocalizedModType
    {
        public string LocalizationCategory => "UI";
        public static SHPCUI Instance => UIHandleLoader.GetUIHandleOfType<SHPCUI>();
        internal const int CyberDomainSectorIndex = 0;
        internal const int ModifySectorIndex = 1;
        internal const int CyberwareSectorIndex = 2;
        internal const int TalkSectorIndex = 3;

        #region 本地化

        public static LocalizedText CyberDomain_Title { get; private set; }
        public static LocalizedText CyberDomain_Subtitle { get; private set; }
        public static LocalizedText CyberDomain_Description { get; private set; }
        public static LocalizedText Cyberware_Title { get; private set; }
        public static LocalizedText Cyberware_Subtitle { get; private set; }
        public static LocalizedText Cyberware_Description { get; private set; }
        public static LocalizedText Cyberware_Status_Open { get; private set; }
        public static LocalizedText Cyberware_Status_Closed { get; private set; }
        public static LocalizedText Status_Title { get; private set; }
        public static LocalizedText Status_Subtitle { get; private set; }
        public static LocalizedText Status_Description { get; private set; }
        public static LocalizedText Status_OK { get; private set; }
        public static LocalizedText Talk_Title { get; private set; }
        public static LocalizedText Talk_Subtitle { get; private set; }
        public static LocalizedText Talk_Description { get; private set; }
        public static LocalizedText Talk_Status_Busy { get; private set; }
        public static LocalizedText Talk_Status_Ready { get; private set; }
        public static LocalizedText Modify_Title { get; private set; }
        public static LocalizedText Modify_Subtitle { get; private set; }
        public static LocalizedText Modify_Description { get; private set; }
        public static LocalizedText Modify_SlotEmpty { get; private set; }
        public static LocalizedText Modify_Install { get; private set; }
        public static LocalizedText Modify_Unequip { get; private set; }
        public static LocalizedText Modify_Equipped { get; private set; }
        public static LocalizedText Modify_NoMatch { get; private set; }
        public static LocalizedText Modify_Close { get; private set; }
        public static LocalizedText Modify_InstallSlot { get; private set; }
        public static LocalizedText Modify_Current { get; private set; }
        public static LocalizedText Modify_Slot_Barrel { get; private set; }
        public static LocalizedText Modify_Slot_Optic { get; private set; }
        public static LocalizedText Modify_Slot_Power { get; private set; }
        public static LocalizedText Modify_Slot_Stock { get; private set; }
        public static LocalizedText Modify_Slot_Grip { get; private set; }
        public static LocalizedText Modify_Slot_Frame { get; private set; }
        public static LocalizedText Modify_Preset_A { get; private set; }
        public static LocalizedText Modify_Preset_B { get; private set; }
        public static LocalizedText Modify_Preset_C { get; private set; }
        public static LocalizedText State_On { get; private set; }
        public static LocalizedText State_Off { get; private set; }
        public static LocalizedText State_Layer { get; private set; }
        public static LocalizedText Cyber_PanelTitle { get; private set; }
        public static LocalizedText Cyber_PanelSubtitle { get; private set; }
        public static LocalizedText Cyber_BtnActivate { get; private set; }
        public static LocalizedText Cyber_BtnDeactivate { get; private set; }
        public static LocalizedText Cyber_LayerLabel { get; private set; }
        public static LocalizedText Cyber_StateOnline { get; private set; }
        public static LocalizedText Cyber_StateOffline { get; private set; }
        public static LocalizedText Cyber_Hint { get; private set; }
        public static LocalizedText Cyber_Layer1_Title { get; private set; }
        public static LocalizedText Cyber_Layer2_Title { get; private set; }
        public static LocalizedText Cyber_Layer3_Title { get; private set; }
        public static LocalizedText Cyber_NoSkill { get; private set; }
        public static LocalizedText Cyber_LayerBaseFooter { get; private set; }
        public static LocalizedText Cyber_SkillUnlocked { get; private set; }
        public static LocalizedText Cyber_SkillLocked { get; private set; }
        public static LocalizedText Cyber_KeyUnbound { get; private set; }
        public static LocalizedText Cyber_Skill_Teleport_Name { get; private set; }
        public static LocalizedText Cyber_Skill_Teleport_Desc { get; private set; }
        public static LocalizedText Cyber_Skill_Restart_Name { get; private set; }
        public static LocalizedText Cyber_Skill_Restart_Desc { get; private set; }
        public static LocalizedText Cyber_Skill_Banish_Name { get; private set; }
        public static LocalizedText Cyber_Skill_Banish_Desc { get; private set; }
        public static LocalizedText Cyber_Skill_Freeze_Name { get; private set; }
        public static LocalizedText Cyber_Skill_Freeze_Desc { get; private set; }
        public static LocalizedText Cyber_DrainHeader { get; private set; }
        public static LocalizedText Cyber_DrainPerSec { get; private set; }
        public static LocalizedText Cyber_SustainTime { get; private set; }
        public static LocalizedText Cyber_SustainInfinite { get; private set; }

        public override void SetStaticDefaults() {
            CyberDomain_Title = this.GetLocalization(nameof(CyberDomain_Title), () => "CYBER DOMAIN");
            CyberDomain_Subtitle = this.GetLocalization(nameof(CyberDomain_Subtitle), () => "Domain Control");
            CyberDomain_Description = this.GetLocalization(nameof(CyberDomain_Description), () => "Deploy and manage multi-layer cyberspace");
            Cyberware_Title = this.GetLocalization(nameof(Cyberware_Title), () => "CYBERWARE");
            Cyberware_Subtitle = this.GetLocalization(nameof(Cyberware_Subtitle), () => "Body Augmentation");
            Cyberware_Description = this.GetLocalization(nameof(Cyberware_Description), () => "Open cyberware augmentation manager");
            Cyberware_Status_Open = this.GetLocalization(nameof(Cyberware_Status_Open), () => "OPEN");
            Cyberware_Status_Closed = this.GetLocalization(nameof(Cyberware_Status_Closed), () => "CLOSED");
            Status_Title = this.GetLocalization(nameof(Status_Title), () => "STATUS");
            Status_Subtitle = this.GetLocalization(nameof(Status_Subtitle), () => "System Diagnostics");
            Status_Description = this.GetLocalization(nameof(Status_Description), () => "View current overload and cooldown info");
            Status_OK = this.GetLocalization(nameof(Status_OK), () => "OK");
            Talk_Title = this.GetLocalization(nameof(Talk_Title), () => "TALK");
            Talk_Subtitle = this.GetLocalization(nameof(Talk_Subtitle), () => "Neural Link");
            Talk_Description = this.GetLocalization(nameof(Talk_Description), () => "Open a direct channel to SHPC");
            Talk_Status_Busy = this.GetLocalization(nameof(Talk_Status_Busy), () => "BUSY");
            Talk_Status_Ready = this.GetLocalization(nameof(Talk_Status_Ready), () => "READY");
            Modify_Title = this.GetLocalization(nameof(Modify_Title), () => "MODIFY");
            Modify_Subtitle = this.GetLocalization(nameof(Modify_Subtitle), () => "Gun Augmentation");
            Modify_Description = this.GetLocalization(nameof(Modify_Description), () => "Install modification parts into SHPC chassis");
            Modify_SlotEmpty = this.GetLocalization(nameof(Modify_SlotEmpty), () => "EMPTY");
            Modify_Install = this.GetLocalization(nameof(Modify_Install), () => "INSTALL");
            Modify_Unequip = this.GetLocalization(nameof(Modify_Unequip), () => "UNEQUIP");
            Modify_Equipped = this.GetLocalization(nameof(Modify_Equipped), () => "EQUIPPED");
            Modify_NoMatch = this.GetLocalization(nameof(Modify_NoMatch), () => "// no compatible modules in inventory");
            Modify_Close = this.GetLocalization(nameof(Modify_Close), () => "CLOSE");
            Modify_InstallSlot = this.GetLocalization(nameof(Modify_InstallSlot), () => "INSTALL \u00b7 {0}");
            Modify_Current = this.GetLocalization(nameof(Modify_Current), () => "CURRENT: {0}");
            Modify_Slot_Barrel = this.GetLocalization(nameof(Modify_Slot_Barrel), () => "BARREL");
            Modify_Slot_Optic = this.GetLocalization(nameof(Modify_Slot_Optic), () => "OPTIC");
            Modify_Slot_Power = this.GetLocalization(nameof(Modify_Slot_Power), () => "POWER");
            Modify_Slot_Stock = this.GetLocalization(nameof(Modify_Slot_Stock), () => "STOCK");
            Modify_Slot_Grip = this.GetLocalization(nameof(Modify_Slot_Grip), () => "GRIP");
            Modify_Slot_Frame = this.GetLocalization(nameof(Modify_Slot_Frame), () => "FRAME");
            Modify_Preset_A = this.GetLocalization(nameof(Modify_Preset_A), () => "PRESET-A");
            Modify_Preset_B = this.GetLocalization(nameof(Modify_Preset_B), () => "PRESET-B");
            Modify_Preset_C = this.GetLocalization(nameof(Modify_Preset_C), () => "PRESET-C");
            State_On = this.GetLocalization(nameof(State_On), () => "ON");
            State_Off = this.GetLocalization(nameof(State_Off), () => "OFF");
            State_Layer = this.GetLocalization(nameof(State_Layer), () => "L");
            Cyber_PanelTitle = this.GetLocalization(nameof(Cyber_PanelTitle), () => "CYBER DOMAIN");
            Cyber_PanelSubtitle = this.GetLocalization(nameof(Cyber_PanelSubtitle), () => "BLACKWALL ACCESS");
            Cyber_BtnActivate = this.GetLocalization(nameof(Cyber_BtnActivate), () => "ACTIVATE");
            Cyber_BtnDeactivate = this.GetLocalization(nameof(Cyber_BtnDeactivate), () => "DEACTIVATE");
            Cyber_LayerLabel = this.GetLocalization(nameof(Cyber_LayerLabel), () => "LAYER");
            Cyber_StateOnline = this.GetLocalization(nameof(Cyber_StateOnline), () => "ONLINE");
            Cyber_StateOffline = this.GetLocalization(nameof(Cyber_StateOffline), () => "OFFLINE");
            Cyber_Hint = this.GetLocalization(nameof(Cyber_Hint), () => "[{0}] Toggle domain / Click ring segment to switch layer");
            Cyber_Layer1_Title = this.GetLocalization(nameof(Cyber_Layer1_Title), () => "BASE ACCESS");
            Cyber_Layer2_Title = this.GetLocalization(nameof(Cyber_Layer2_Title), () => "DEEP DIVE");
            Cyber_Layer3_Title = this.GetLocalization(nameof(Cyber_Layer3_Title), () => "BLACKWALL BREACH");
            Cyber_NoSkill = this.GetLocalization(nameof(Cyber_NoSkill), () => "// No exclusive skills.");
            Cyber_LayerBaseFooter = this.GetLocalization(nameof(Cyber_LayerBaseFooter), () => "Foundation layer — establishes the domain field.");
            Cyber_SkillUnlocked = this.GetLocalization(nameof(Cyber_SkillUnlocked), () => "[#] UNLOCKED");
            Cyber_SkillLocked = this.GetLocalization(nameof(Cyber_SkillLocked), () => "[X] Requires Layer {0}");
            Cyber_KeyUnbound = this.GetLocalization(nameof(Cyber_KeyUnbound), () => "?");
            Cyber_Skill_Teleport_Name = this.GetLocalization(nameof(Cyber_Skill_Teleport_Name), () => "RIFT TELEPORT");
            Cyber_Skill_Teleport_Desc = this.GetLocalization(nameof(Cyber_Skill_Teleport_Desc), () => "Tear a dimensional rift to the cursor and reform inside the domain.");
            Cyber_Skill_Restart_Name = this.GetLocalization(nameof(Cyber_Skill_Restart_Name), () => "SYSTEM REBOOT");
            Cyber_Skill_Restart_Desc = this.GetLocalization(nameof(Cyber_Skill_Restart_Desc), () => "Collapse the domain into a singularity and burst back fully restored.");
            Cyber_Skill_Banish_Name = this.GetLocalization(nameof(Cyber_Skill_Banish_Name), () => "CYBER BANISH");
            Cyber_Skill_Banish_Desc = this.GetLocalization(nameof(Cyber_Skill_Banish_Desc), () => "Send the foe under the cursor into deep cyberspace.");
            Cyber_Skill_Freeze_Name = this.GetLocalization(nameof(Cyber_Skill_Freeze_Name), () => "DOMAIN FREEZE");
            Cyber_Skill_Freeze_Desc = this.GetLocalization(nameof(Cyber_Skill_Freeze_Desc), () => "Lock every hostile entity inside the domain in place.");
            Cyber_DrainHeader = this.GetLocalization(nameof(Cyber_DrainHeader), () => "RAM DRAIN");
            Cyber_DrainPerSec = this.GetLocalization(nameof(Cyber_DrainPerSec), () => "-{0}/s");
            Cyber_SustainTime = this.GetLocalization(nameof(Cyber_SustainTime), () => "SUSTAIN ~{0}s");
            Cyber_SustainInfinite = this.GetLocalization(nameof(Cyber_SustainInfinite), () => "SUSTAIN \u221E");
        }

        #endregion

        #region 显隐与生命周期

        public override bool Active {
            get {
                Player p = Main.LocalPlayer;
                if (p == null || !p.active || p.dead) {
                    return false;
                }
                //仅在手持SHPC时显示
                Item held = p.GetItem();
                if (held == null || held.IsAir || held.type != SHPCOverride.ID) {
                    return false;
                }
                //避让全屏UI
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

        //供 SHPCModPanel 调用：打开三级模块选择面板
        public void OpenModuleSelect(int slotIdx) {
            if (slotIdx < 0 || slotIdx >= 6) {
                pinnedModuleSlot = -1;
                return;
            }
            pinnedModuleSlot = slotIdx;
            SHPCModuleSelectPanel.ScrollReset();
        }

        public void CloseModuleSelect() {
            pinnedModuleSlot = -1;
        }

        public int PinnedModuleSlot => pinnedModuleSlot;

        /// <summary>
        /// 处理三级模块选择面板的左键点击
        /// </summary>
        private void HandleModuleSelectClick(Player owner) {
            if (pinnedModuleSlot < 0) {
                return;
            }
            if (moduleHover == SHPCModuleSelectPanel.HitKind.Close) {
                pinnedModuleSlot = -1;
                return;
            }
            SHPCPlayer sp = SHPCPlayer.Get(owner);
            if (moduleHover == SHPCModuleSelectPanel.HitKind.Unequip) {
                Item taken = sp.TakeModule(pinnedModuleSlot);
                if (taken != null && !taken.IsAir) {
                    owner.QuickSpawnItem(owner.GetSource_Misc("SHPCModule"), taken, taken.stack);
                }
                return;
            }
            if (moduleHover == SHPCModuleSelectPanel.HitKind.Row) {
                int rowIdx = moduleLayout.HoveredRow;
                System.Collections.Generic.IReadOnlyList<Item> list =
                    SHPCModuleSelectPanel.CurrentCandidates;
                if (rowIdx < 0 || rowIdx >= list.Count) {
                    return;
                }
                Item picked = list[rowIdx];
                Item swapped = sp.PutModule(pinnedModuleSlot, picked);
                //从背包移除一个 picked
                int found = -1;
                for (int i = 0; i < owner.inventory.Length; i++) {
                    if (owner.inventory[i] == picked) {
                        found = i;
                        break;
                    }
                }
                if (found >= 0) {
                    owner.inventory[found].stack--;
                    if (owner.inventory[found].stack <= 0) {
                        owner.inventory[found].TurnToAir();
                    }
                }
                if (swapped != null && !swapped.IsAir) {
                    owner.QuickSpawnItem(owner.GetSource_Misc("SHPCModule"), swapped, swapped.stack);
                }
                return;
            }
        }

        //供外部代码（如教程系统）强制展开操作面板
        public void ForceExpand() {
            if (expanded) return;
            expanded = true;
            clickFlash = 1f;
            corePulse = 1f;
            SoundEngine.PlaySound(SoundID.MenuTick);
        }

        //供外部代码（如教程系统）只读查询当前固定二级面板锁定的扇区索引，-1表示未锁定
        public int PinnedSector => pinnedSector;
        //当前操作面板是否已展开（用于教程系统判定玩家是否已点击核心）
        public bool IsExpanded => expanded || expandProgress > 0.6f;

        #region 状态

        //展开进度，0为收起仅显示核心，1为完全展开
        private float expandProgress;
        //目标展开状态
        private bool expanded;
        //核心悬停强度，平滑跟随
        private float coreHoverAmt;
        //核心点击瞬闪强度，逐帧衰减
        private float clickFlash;
        //核心呼吸脉冲，点击时短暂提升
        private float corePulse;

        //当前悬停的扇区索引，-1表示无
        private int hoveredSector = -1;
        //当前选中的扇区索引，-1表示无
        private int selectedSector = -1;
        //每个扇区的悬停平滑强度
        private float[] hoverAmts;
        //每个扇区的选中平滑强度
        private float[] selectAmts;
        //二级信息面板的不透明度，平滑跟随
        private float infoPanelProgress;
        //当前用于展示的信息面板按钮索引，-1表示无
        private int infoButtonIdx = -1;
        //当前固定二级面板锁定的按钮索引，-1表示无
        private int pinnedSector = -1;
        //固定二级面板的不透明度，平滑跟随
        private float pinnedPanelProgress;
        //缓存上一帧赛博面板悬停的子控件
        private SHPCCyberPanel.HitKind cyberHover;
        //缓存上一帧改造面板悬停的插槽
        private SHPCModPanel.HitKind modHover;
        //当前固定的三级模块选择面板对应槽位索引，-1为未开启
        private int pinnedModuleSlot = -1;
        //三级模块选择面板的不透明度，平滑跟随
        private float modulePanelProgress;
        //缓存上一帧三级模块选择面板悬停命中
        private SHPCModuleSelectPanel.HitKind moduleHover;
        //三级模块选择面板布局缓存（每帧重建）
        private SHPCModuleSelectPanel.Layout moduleLayout;

        //全局时间，单位秒
        private float time;

        //RAM值平滑显示量，跟随 CWRRamSystem.CurrentRam
        private float ramDisplayValue;

        //按钮配置列表
        private List<SHPCButtonDef> buttons;

        #endregion

        #region 按钮初始化

        /// <summary>
        /// 首次进入或按需重建按钮配置
        /// 当前为占位实现，等待具体功能接入后再细化
        /// </summary>
        private void EnsureButtons() {
            if (buttons != null) {
                return;
            }
            buttons = new List<SHPCButtonDef>(4) {
                new SHPCButtonDef {
                    Title = () => CyberDomain_Title.Value,
                    Subtitle = () => CyberDomain_Subtitle.Value,
                    Description = () => CyberDomain_Description.Value,
                    Glyph = "D",
                    Enabled = () => true,
                    StatusValue = () => Cyberspace.Active
                        ? Cyberspace.CurrentLayer / (float)Cyberspace.MaxLayerCount
                        : -1f,
                    StatusText = () => Cyberspace.Active
                        ? State_Layer.Value + Cyberspace.CurrentLayer
                        : State_Off.Value,
                    OnClick = null,
                    UsesFixedPanel = true,
                },
                new SHPCButtonDef {
                    Title = () => Modify_Title.Value,
                    Subtitle = () => Modify_Subtitle.Value,
                    Description = () => Modify_Description.Value,
                    Glyph = "M",
                    Enabled = () => true,
                    StatusValue = () => -1f,
                    StatusText = () => "0/6",
                    OnClick = null,
                    UsesFixedPanel = true,
                },
                new SHPCButtonDef {
                    Title = () => Cyberware_Title.Value,
                    Subtitle = () => Cyberware_Subtitle.Value,
                    Description = () => Cyberware_Description.Value,
                    Glyph = "C",
                    Enabled = () => true,
                    StatusValue = () => 0f,
                    StatusText = () => CyberwareUI.Instance?.Active == true
                        ? Cyberware_Status_Open.Value
                        : Cyberware_Status_Closed.Value,
                    OnClick = () => CyberwareUI.Instance?.Toggle(),
                },
                new SHPCButtonDef {
                    Title = () => Talk_Title.Value,
                    Subtitle = () => Talk_Subtitle.Value,
                    Description = () => Talk_Description.Value,
                    Glyph = "T",
                    Enabled = () => true,
                    StatusValue = () => -1f,
                    StatusText = () => ScenarioManager.IsActive()
                        ? Talk_Status_Busy.Value
                        : Talk_Status_Ready.Value,
                    OnClick = () => SHPCDialogueRouter.TryStart(Main.LocalPlayer),
                },
            };
            hoverAmts = new float[buttons.Count];
            selectAmts = new float[buttons.Count];
        }

        #endregion

        #region 工具

        //取得核心位置
        private static Vector2 GetCorePosition() => new(96f, Main.screenHeight - 96f);

        //单按钮的角度区间
        private static void GetSectorAngles(int idx, int count, out float aStart, out float aEnd) {
            float total = SHPCTheme.FanEnd - SHPCTheme.FanStart;
            float gap = SHPCTheme.ButtonGap;
            float perAngle = (total - gap * (count - 1)) / count;
            aStart = SHPCTheme.FanStart + idx * (perAngle + gap);
            aEnd = aStart + perAngle;
        }

        //极坐标命中检测，给定鼠标到核心的偏移
        //返回-1表示未命中扇区，0..count-1表示命中索引
        //isCore通过out返回是否命中核心
        private int HitTest(Vector2 mouseOffset, out bool isCore) {
            isCore = false;
            float dist = mouseOffset.Length();
            //核心点击容差略大于实际外环
            if (dist <= SHPCTheme.CoreRingR + 6f) {
                isCore = true;
                return -1;
            }
            //仅在展开进度达到一定阈值后才允许点击扇区
            if (expandProgress < 0.6f) {
                return -1;
            }
            if (dist < SHPCTheme.ButtonInnerR - 4f || dist > SHPCTheme.ButtonOuterR + 4f) {
                return -1;
            }
            float ang = MathF.Atan2(mouseOffset.Y, mouseOffset.X);
            //角度容差，避免缝隙难点
            const float edgeTol = 0.025f;
            int count = buttons.Count;
            for (int i = 0; i < count; i++) {
                GetSectorAngles(i, count, out float a0, out float a1);
                if (ang >= a0 - edgeTol && ang <= a1 + edgeTol) {
                    return i;
                }
            }
            return -1;
        }

        //计算指定按钮固定二级面板的锚点与中线方向
        //为了避免面板从不同角度的按钮顶出时遮挡其他UI，这里采用与按钮位置无关的固定锚点
        //（在扇形右侧、底边对齐核心高度），连续处于同一位置不随点击跳变
        private void GetFixedPanelAnchor(int idx, out Vector2 anchor, out float midA) {
            //中线使用水平向右，面板只会沿水平方向滑入
            midA = 0f;
            Vector2 corePos = GetCorePosition();
            //不同面板高度不同，用各自的高度计算纵向偏移保证底部对齐核心
            float panelH = idx == ModifySectorIndex ? SHPCModPanel.PanelH : SHPCCyberPanel.PanelH;
            anchor = new Vector2(
                corePos.X + SHPCTheme.ButtonOuterR + 2f,
                corePos.Y - panelH * 0.5f + 6f);
        }

        #endregion

        #region 更新

        public override void Update() {
            EnsureButtons();
            time += 1f / 60f;

            //平滑跟随RAM当前值
            ramDisplayValue = MathHelper.Lerp(ramDisplayValue, RamSystem.CurrentRam, 0.12f);
            if (MathF.Abs(ramDisplayValue - RamSystem.CurrentRam) < 0.01f) {
                ramDisplayValue = RamSystem.CurrentRam;
            }

            //展开进度推进
            float targetExpand = expanded ? 1f : 0f;
            expandProgress = MathHelper.Lerp(expandProgress, targetExpand, expanded ? 0.18f : 0.22f);
            if (MathF.Abs(expandProgress - targetExpand) < 0.002f) {
                expandProgress = targetExpand;
            }

            //衰减瞬态值
            clickFlash *= 0.88f;
            if (clickFlash < 0.01f) {
                clickFlash = 0f;
            }
            corePulse *= 0.92f;
            if (corePulse < 0.01f) {
                corePulse = 0f;
            }

            //命中检测
            Vector2 corePos = GetCorePosition();
            Vector2 offset = MousePosition - corePos;
            int hit = HitTest(offset, out bool isCore);
            hoveredSector = hit;
            coreHoverAmt = MathHelper.Lerp(coreHoverAmt, isCore ? 1f : 0f, 0.2f);

            //平滑悬停与选中强度
            for (int i = 0; i < hoverAmts.Length; i++) {
                float ht = i == hoveredSector ? 1f : 0f;
                hoverAmts[i] = MathHelper.Lerp(hoverAmts[i], ht, 0.25f);
                float st = i == selectedSector ? 1f : 0f;
                selectAmts[i] = MathHelper.Lerp(selectAmts[i], st, 0.18f);
            }

            //固定二级面板进度与子控件命中
            float pinnedTarget = pinnedSector >= 0 && expandProgress > 0.7f ? 1f : 0f;
            pinnedPanelProgress = MathHelper.Lerp(pinnedPanelProgress, pinnedTarget, 0.22f);
            if (pinnedSector < 0 && pinnedPanelProgress < 0.02f) {
                pinnedPanelProgress = 0f;
            }
            //三级模块面板进度：仅在二级面板为 Modify 且已选中插槽时打开
            float moduleTarget = (pinnedSector == ModifySectorIndex && pinnedModuleSlot >= 0
                && pinnedPanelProgress > 0.6f) ? 1f : 0f;
            modulePanelProgress = MathHelper.Lerp(modulePanelProgress, moduleTarget, 0.22f);
            if (pinnedSector != ModifySectorIndex) {
                pinnedModuleSlot = -1;
            }
            cyberHover = SHPCCyberPanel.HitKind.None;
            modHover = SHPCModPanel.HitKind.None;
            SHPCCyberPanel.Layout cyberLayout = default;
            SHPCModPanel.Layout modLayout = default;
            bool cyberPanelHit = false;
            bool cyberPanelVisible = false;
            if (pinnedSector >= 0 && pinnedSector < buttons.Count
                && buttons[pinnedSector].UsesFixedPanel && pinnedPanelProgress > 0.4f) {
                GetFixedPanelAnchor(pinnedSector, out Vector2 panelAnchor, out float panelMidA);
                if (pinnedSector == CyberDomainSectorIndex) {
                    cyberLayout = SHPCCyberPanel.Compute(panelAnchor, panelMidA, pinnedPanelProgress);
                    cyberHover = SHPCCyberPanel.HitTest(cyberLayout, MousePosition);
                    cyberPanelHit = cyberLayout.Panel.Contains((int)MousePosition.X, (int)MousePosition.Y)
                        || cyberHover == SHPCCyberPanel.HitKind.Skill1
                        || cyberHover == SHPCCyberPanel.HitKind.Skill2
                        || cyberHover == SHPCCyberPanel.HitKind.Skill3;
                    cyberPanelVisible = true;
                }
                else if (pinnedSector == ModifySectorIndex) {
                    modLayout = SHPCModPanel.Compute(panelAnchor, panelMidA, pinnedPanelProgress);
                    modHover = SHPCModPanel.HitTest(modLayout, MousePosition);
                    cyberPanelHit = modLayout.Panel.Contains((int)MousePosition.X, (int)MousePosition.Y);

                    //三级模块选择面板：叠在 mod 面板右侧（复用 outDir偏移）
                    if (pinnedModuleSlot >= 0 && modulePanelProgress > 0.4f) {
                        Vector2 outDir = SHPCRenderer.AngleDir(panelMidA);
                        Vector2 modAnchor = panelAnchor + outDir * (SHPCModPanel.PanelW + 12f);
                        moduleLayout = SHPCModuleSelectPanel.Compute(modAnchor, panelMidA, modulePanelProgress);
                        SHPCModuleSelectPanel.RefreshCandidates((Modules.SHPCSlotCategory)pinnedModuleSlot);
                        moduleHover = SHPCModuleSelectPanel.HitTest(ref moduleLayout, MousePosition,
                            SHPCModuleSelectPanel.CurrentCandidates.Count);
                        if (moduleLayout.Panel.Contains((int)MousePosition.X, (int)MousePosition.Y)) {
                            cyberPanelHit = true;
                            if (moduleLayout.ListArea.Contains((int)MousePosition.X, (int)MousePosition.Y)) {
                                SHPCModuleSelectPanel.HandleScroll();
                            }
                        }
                        //三级面板存在时屏蔽二级插槽点击，仅保留 hover 插槽索引同步提示（可点击同一插槽关闭）
                    }
                }
            }
            //每帧推进赛博面板段位悬停延展进度，面板不可见时强制衰减
            SHPCCyberPanel.UpdateHover(cyberHover, cyberPanelVisible);

            //信息面板逻辑：仅在光标悬停按钮时显示（tooltip语义），锁定面板期间隐藏以免重叠
            float targetInfoAlpha = 0f;
            if (hoveredSector >= 0 && expandProgress > 0.7f && pinnedSector < 0) {
                targetInfoAlpha = 1f;
                infoButtonIdx = hoveredSector;
            }
            infoPanelProgress = MathHelper.Lerp(infoPanelProgress, targetInfoAlpha, 0.25f);
            if (infoPanelProgress < 0.02f) {
                infoButtonIdx = -1;
            }

            //领域快捷键：在手持SHPC且HUD可见时热键切换领域开关
            //骇客时间激活期间禁止切换领域状态
            if (!HackTime.Active && CWRKeySystem.Legend_Domain != null && CWRKeySystem.Legend_Domain.JustPressed) {
                Cyberspace.Toggle(player);
            }

            //鼠标交互占用
            bool inHotArea = isCore || hit >= 0 ||
                (offset.Length() < SHPCTheme.ButtonOuterR + 8f && expandProgress > 0.4f) ||
                cyberPanelHit;
            if (inHotArea) {
                player.mouseInterface = true;
                player.CWR().DontSwitchWeaponTime = 2;
            }

            //左键处理
            if (keyLeftPressState == KeyPressState.Pressed) {
                if (cyberHover != SHPCCyberPanel.HitKind.None) {
                    //三级面板悬停区仅用于阻止收起，不响应点击
                    bool isSkillHover = cyberHover == SHPCCyberPanel.HitKind.Skill1
                        || cyberHover == SHPCCyberPanel.HitKind.Skill2
                        || cyberHover == SHPCCyberPanel.HitKind.Skill3;
                    if (!isSkillHover) {
                        SHPCCyberPanel.HandleClick(cyberHover, player);
                        SoundEngine.PlaySound(SoundID.MenuTick);
                    }
                }
                else if (pinnedModuleSlot >= 0 && moduleHover != SHPCModuleSelectPanel.HitKind.None) {
                    HandleModuleSelectClick(player);
                    SoundEngine.PlaySound(SoundID.MenuTick);
                }
                else if (modHover != SHPCModPanel.HitKind.None) {
                    SHPCModPanel.HandleClick(modHover, player);
                    SoundEngine.PlaySound(SoundID.MenuTick);
                }
                else if (isCore) {
                    expanded = !expanded;
                    clickFlash = 1f;
                    corePulse = 1f;
                    if (!expanded) {
                        pinnedSector = -1;
                    }
                    SoundEngine.PlaySound(SoundID.MenuTick);
                }
                else if (hit >= 0 && expandProgress > 0.6f) {
                    SHPCButtonDef def = buttons[hit];
                    bool enabled = def.Enabled?.Invoke() ?? true;
                    if (enabled) {
                        selectedSector = hit;
                        if (def.UsesFixedPanel) {
                            //切换锁定状态：同一按钮再点击则收起
                            bool wasOpen = pinnedSector == hit;
                            pinnedSector = wasOpen ? -1 : hit;
                            //开启固定面板时联动关闭义体UI
                            if (!wasOpen && CyberwareUI.Instance?.Active == true) {
                                CyberwareUI.Instance.Toggle();//考虑到这个义体界面可能在其他地方也有所使用，这里的关联关闭可能不妥，后续如果有需要再调整为更合理的交互方式
                            }
                        }
                        else if (pinnedSector >= 0 && pinnedSector != hit) {
                            pinnedSector = -1;
                        }
                        def.OnClick?.Invoke();
                        SoundEngine.PlaySound(SoundID.MenuTick);
                    }
                    else {
                        SoundEngine.PlaySound(SoundID.MenuClose);
                    }
                }
                else if (!cyberPanelHit) {
                    //面板外点击不取消锁定，仅需保证收起总HUD时同步清除
                }
            }

            //总HUD收起同步收起锁定面板
            if (!expanded) {
                pinnedSector = -1;
            }

            //更新命中盒，便于上层屏蔽世界点击
            int hitR = (int)(SHPCTheme.ButtonOuterR + 12f);
            UIHitBox = new Rectangle(
                (int)(corePos.X - hitR), (int)(corePos.Y - hitR),
                hitR * 2, hitR * 2);
        }

        #endregion

        #region 绘制

        public override void Draw(SpriteBatch sb) {
            if (buttons == null) {
                return;
            }
            Texture2D px = VaultAsset.placeholder2.Value;
            if (px == null) {
                return;
            }

            Vector2 corePos = GetCorePosition();
            const float globalAlpha = 1f;

            //先绘制连接线，置于扇区下方
            int count = buttons.Count;
            for (int i = 0; i < count; i++) {
                GetSectorAngles(i, count, out float a0, out float a1);
                float midA = (a0 + a1) * 0.5f;
                SHPCRenderer.DrawConnector(sb, px, corePos, midA, expandProgress,
                    hoverAmts[i], globalAlpha);
            }

            //扇形按钮
            for (int i = 0; i < count; i++) {
                GetSectorAngles(i, count, out float a0, out float a1);
                SHPCButtonDef def = buttons[i];
                bool enabled = def.Enabled?.Invoke() ?? true;
                float status = def.StatusValue?.Invoke() ?? -1f;
                SHPCRenderer.DrawSector(sb, px, corePos, a0, a1, expandProgress,
                    hoverAmts[i], selectAmts[i], enabled,
                    MathHelper.Clamp(status, 0f, 1f),
                    def.Glyph, time, globalAlpha);
            }

            //RAM弧形条（始终显示，不受展开状态影响）
            SHPCRenderer.DrawRAMBar(sb, px, corePos,
                ramDisplayValue, RamSystem.MaxRam, time, globalAlpha);

            //核心
            SHPCRenderer.DrawCore(sb, px, corePos, expandProgress,
                coreHoverAmt, corePulse, clickFlash, time, globalAlpha);

            //二级信息面板，最后绘制保证置顶
            if (infoButtonIdx >= 0 && infoButtonIdx < buttons.Count && infoPanelProgress > 0.02f) {
                SHPCButtonDef def = buttons[infoButtonIdx];
                SHPCRenderer.DrawInfoPanel(sb, px, MousePosition,
                    infoPanelProgress, globalAlpha,
                    def.Title?.Invoke(), def.Subtitle?.Invoke(), def.Description?.Invoke(),
                    def.StatusText?.Invoke());
            }

            //固定二级面板
            if (pinnedSector >= 0 && pinnedSector < buttons.Count && pinnedPanelProgress > 0.02f) {
                GetFixedPanelAnchor(pinnedSector, out Vector2 fAnchor, out float fMidA);
                if (pinnedSector == CyberDomainSectorIndex) {
                    SHPCCyberPanel.Layout layout = SHPCCyberPanel.Compute(fAnchor, fMidA, pinnedPanelProgress);
                    SHPCCyberPanel.Draw(sb, px, layout, pinnedPanelProgress, globalAlpha, cyberHover);
                }
                else if (pinnedSector == ModifySectorIndex) {
                    SHPCModPanel.Layout layout = SHPCModPanel.Compute(fAnchor, fMidA, pinnedPanelProgress);

                    //三级模块选择面板
                    if (pinnedModuleSlot >= 0 && modulePanelProgress > 0.02f) {
                        Vector2 outDir = SHPCRenderer.AngleDir(fMidA);
                        Vector2 modAnchor = fAnchor + outDir * (SHPCModPanel.PanelW + 12f);
                        SHPCModuleSelectPanel.Layout modLayout = SHPCModuleSelectPanel.Compute(
                            modAnchor, fMidA, modulePanelProgress);
                        SHPCModuleSelectPanel.RefreshCandidates(
                            (Modules.SHPCSlotCategory)pinnedModuleSlot);
                        //重新 HitTest 以同步 HoveredRow（不影响点击逻辑）
                        SHPCModuleSelectPanel.HitKind hover = SHPCModuleSelectPanel.HitTest(
                            ref modLayout, MousePosition,
                            SHPCModuleSelectPanel.CurrentCandidates.Count);
                        Item equipped = Main.LocalPlayer.GetModPlayer<SHPCPlayer>().GetModule(pinnedModuleSlot);
                        SHPCModuleSelectPanel.Draw(sb, px, modLayout,
                            modulePanelProgress, globalAlpha, hover,
                            pinnedModuleSlot, equipped);
                    }

                    SHPCModPanel.Draw(sb, px, layout, pinnedPanelProgress, globalAlpha, modHover);
                }
            }
        }

        #endregion
    }
}
