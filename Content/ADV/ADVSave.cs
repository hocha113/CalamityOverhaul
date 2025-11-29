using System.IO;
using System.Reflection;
using Terraria;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace CalamityOverhaul.Content.ADV
{
    /// <summary>
    /// 老公爵交互状态枚举
    /// </summary>
    public enum OldDukeInteractionState
    {
        /// <summary>未遇见</summary>
        NotMet = 0,
        /// <summary>已遇见但未做选择</summary>
        Met = 1,
        /// <summary>接受合作</summary>
        AcceptedCooperation = 2,
        /// <summary>拒绝合作（可重新选择）</summary>
        DeclinedCooperation = 3,
        /// <summary>选择战斗（永久战斗）</summary>
        ChoseToFight = 4
    }

    public class ADVSave
    {
        public bool HasCaughtHalibut;
        public bool FirstMet;
        public bool DyeProtest;
        public bool FishoilQuestDeclined;
        public bool FishoilQuestAccepted;
        public bool FishoilQuestCompleted;
        public bool FirstResurrectionWarning;
        public bool QueenBeeGift;
        public bool SkeletronGift;
        public bool EyeOfCthulhuGift;
        public bool KingSlimeGift;
        public bool CrabulonGift;
        public bool PerforatorGift;
        public bool HiveMindGift;
        public bool WallOfFleshGift;
        public bool SlimeGodGift;
        public bool CryogenGift;
        public bool BrimstoneElementalGift;
        public bool AquaticScourgeGift;
        public bool CalamitasCloneGift;
        public bool PlanteraGift;
        public bool GolemGift;
        public bool HellGift;
        public bool MoonLordGift;
        public bool LeviathanGift;
        public bool PlaguebringerGift;
        public bool ProvidenceGift;
        public bool DevourerOfGodsGift;
        public bool YharonGift;
        public bool SupremeCalamitasGift;
        public bool FirstMetSupCal;
        public bool SupCalChoseToFight;
        public bool SupCalMoonLordReward;
        public bool SupCalDefeat;
        public bool SupCalQuestAccepted;//玩家是否接受了任务
        public bool SupCalQuestDeclined;//玩家是否拒绝了任务
        public bool SupCalQuestReward;//玩家是否完成了任务（击杀了Providence）
        public bool SupCalQuestRewardSceneComplete;//任务完成后的奖励场景是否已播放
        public bool SupCalDoGQuestAccepted;//玩家是否接受了神明吞噬者任务
        public bool SupCalDoGQuestReward; //玩家是否完成了神明吞噬者任务
        public bool SupCalDoGQuestRewardSceneComplete; //神明吞噬者奖励场景是否已播放
        public bool SupCalDoGQuestDeclined; //玩家是否拒绝了神明吞噬者任务
        public bool SupCalYharonQuestReward;
        public bool SupCalYharonQuestAccepted;
        public bool SupCalYharonQuestDeclined;
        public bool SupCalYharonQuestRewardSceneComplete;
        public bool EternalBlazingNowTriggered;
        public bool EternalBlazingNowChoice1;
        public bool EternalBlazingNowChoice2;
        public bool GiveBlazingBud;//是否已获得燃烧芽(众神之怒适配)
        public bool EternalBlazingNow;//是否达成永恒燃烧的现在结局
        public bool HelenInterferenceTriggered;//海伦劝阻场景是否已触发
        public bool HelenInterferenceContinue;//选择继续委托
        public bool HelenInterferenceStop;//选择中止委托
        public bool DeploySignaltowerQuestAccepted;//玩家是否接受了信号塔部署任务
        public bool DeploySignaltowerQuestDeclined;//玩家是否拒绝了信号塔部署任务
        public bool DeploySignaltowerFirstTowerBuilt;//玩家是否已搭建第一座信号塔
        public bool DeploySignaltowerQuestCompleted;//玩家是否完成了信号塔部署任务
        public bool UseConstructionBlueprint;//玩家是否使用了建筑蓝图QET
        public bool FristExoMechdusaSum;//玩家是否第一次触发机甲嘉登场景
        public bool ExoMechEndingDialogue;//玩家是否观看过机甲嘉登的结束对话场景
        public bool ExoMechSecondDefeat;//玩家是否观看过机甲嘉登的第二次战败对话
        public bool ExoMechThirdDefeat;//玩家是否观看过机甲嘉登的第三次战败对话
        public int ExoMechDefeatCount;//玩家击败机甲的次数

        /// <summary>
        /// 老公爵交互状态
        /// </summary>
        public int OldDukeInteraction;

        /// <summary>
        /// 获取老公爵交互状态
        /// </summary>
        public OldDukeInteractionState OldDukeState {
            get => (OldDukeInteractionState)OldDukeInteraction;
            set => OldDukeInteraction = (int)value;
        }

        /// <summary>
        /// 是否首次遇见老公爵
        /// </summary>
        public bool FirstMetOldDuke => OldDukeState != OldDukeInteractionState.NotMet;

        /// <summary>
        /// 是否接受了与老公爵的合作
        /// </summary>
        public bool OldDukeCooperationAccepted => OldDukeState == OldDukeInteractionState.AcceptedCooperation;

        /// <summary>
        /// 是否拒绝了与老公爵的合作（但可重新选择）
        /// </summary>
        public bool OldDukeCooperationDeclined => OldDukeState == OldDukeInteractionState.DeclinedCooperation;

        /// <summary>
        /// 是否选择与老公爵战斗（永久战斗状态）
        /// </summary>
        public bool OldDukeChoseToFight => OldDukeState == OldDukeInteractionState.ChoseToFight;

        /// <summary>
        /// 是否可以重新触发老公爵对话（拒绝合作但未战斗）
        /// </summary>
        public bool CanRetriggerOldDukeDialogue => OldDukeState == OldDukeInteractionState.DeclinedCooperation;

        public virtual TagCompound SaveData() {
            TagCompound tag = [];

            //使用反射自动保存所有公共字段
            FieldInfo[] fields = GetType().GetFields(BindingFlags.Public | BindingFlags.Instance);
            foreach (FieldInfo field in fields) {
                //处理bool类型字段
                if (field.FieldType == typeof(bool)) {
                    tag[field.Name] = field.GetValue(this);
                }
                //处理int类型字段
                else if (field.FieldType == typeof(int)) {
                    tag[field.Name] = field.GetValue(this);
                }
            }

            return tag;
        }

        public virtual void LoadData(TagCompound tag) {
            //使用反射自动加载所有公共字段
            FieldInfo[] fields = GetType().GetFields(BindingFlags.Public | BindingFlags.Instance);
            foreach (FieldInfo field in fields) {
                //处理bool类型字段
                if (field.FieldType == typeof(bool)) {
                    if (tag.TryGet(field.Name, out bool boolValue)) {
                        field.SetValue(this, boolValue);
                    }
                }
                //处理int类型字段
                else if (field.FieldType == typeof(int)) {
                    if (tag.TryGet(field.Name, out int intValue)) {
                        field.SetValue(this, intValue);
                    }
                }
            }
        }

        public void SendEbnData(Player player) {
            if (VaultUtils.isSinglePlayer) {
                return;
            }
            ModPacket modPacket = CWRMod.Instance.GetPacket();
            modPacket.Write((byte)CWRMessageType.EbnTag);
            modPacket.Write(player.whoAmI);
            modPacket.Write(EternalBlazingNow);
            modPacket.Send();
        }

        internal static void NetHandle(CWRMessageType type, BinaryReader reader, int whoAmI) {
            if (type == CWRMessageType.EbnTag) {
                int playerIndex = reader.ReadInt32();
                bool eternalBlazingNow = reader.ReadBoolean();
                if (!playerIndex.TryGetPlayer(out var player)) {
                    return;
                }
                if (!player.TryGetADVSave(out var save)) {
                    return;
                }
                save.EternalBlazingNow = eternalBlazingNow;
                if (!VaultUtils.isServer) {
                    return;
                }
                ModPacket modPacket = CWRMod.Instance.GetPacket();
                modPacket.Write((byte)CWRMessageType.EbnTag);
                modPacket.Write(player.whoAmI);
                modPacket.Write(eternalBlazingNow);
                modPacket.Send(-1, whoAmI);
            }
        }
    }
}
