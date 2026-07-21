using CalamityOverhaul.Content.LegendWeapon.TrialQuests;
using CalamityOverhaul.Content.Wraiths.Core;
using System.Collections.Generic;
using System.IO;
using Terraria;
using Terraria.ModLoader.IO;

namespace CalamityOverhaul.Content.LegendWeapon.OnikiriLegend
{
    /// <summary>
    /// 鬼切的传奇数据：点鬼簿绑定层 + 试炼路线。每把刀实例各持一份 <see cref="WraithProgressStore"/>，
    /// 随物品存档与联机同步；试炼进度驱动 <see cref="LegendData.Level"/>
    /// </summary>
    internal class OnikiriData : LegendData
    {
        internal override IReadOnlyList<LegendTrialDefinition> TrialDefinitions
            => LegendTrialRouteCatalog.OnikiriProgression;

        public override int TargetLevel => GetVersionedTrialTargetLevel();

        //存档标记:区分"存过档(即使记录全默认)"与"本功能之前的老刀"。
        //老档兼容语义:带 InitTag 的刀读档回放存档值;无 InitTag 的刀吃下方出厂表。
        //标记曾为 "OnikiriWraiths:Init"(认主收敛试验期):键名升位使该批测试刀读档时
        //重播出厂表,簿面统一回到演示期钦定态——认主叙事已按用户钦定回滚,不留中间态存档
        private const string InitTag = "OnikiriWraiths:Init2";

        /// <summary>
        /// 出厂铭刻名单：新刀按此表落 Bound + 驾驭度，数值为演示期钦定值（点鬼簿的正典样貌）。
        /// 认主收敛（0.15~0.35 低位起步）已按用户钦定回滚——簿面不做"认主前"压制
        /// </summary>
        private static readonly (string Key, float Mastery)[] FactoryEngravings = [
            ("NoFace", 0.86f),
            ("LanternBoy", 0.58f),
            ("CrimsonBride", 0.16f),
            ("StandIn", 0.77f),
            ("HeadlessShade", 0.28f),
            ("GhostHand", 0.45f),
        ];

        /// <summary>本刀的厉鬼绑定进度</summary>
        public WraithProgressStore Wraiths { get; private set; } = new();

        public OnikiriData() {
            SeedFactoryState();
        }

        /// <summary>克隆链深拷：每把刀必须各持一份点鬼簿，绝不共写</summary>
        public override LegendData Clone(Item item) {
            OnikiriData clone = (OnikiriData)base.Clone(item);
            clone.Wraiths = new WraithProgressStore();
            clone.Wraiths.CopyFrom(Wraiths);
            return clone;
        }

        /// <summary>从 Item 取鬼切数据，非鬼切或空物品返回 null</summary>
        public static OnikiriData TryGet(Item item) {
            if (item == null || item.IsAir) {
                return null;
            }
            return item.CWR()?.LegendData as OnikiriData;
        }

        /// <summary>
        /// 出厂态：名录鬼先按定义的初始绑定状态落记录（井中鸣=Sealed），
        /// 再对出厂名单盖 Bound + 驾驭度
        /// </summary>
        private void SeedFactoryState() {
            Wraiths.Clear();
            foreach (WraithDefinition definition in WraithRegistry.All) {
                if (definition.HiddenFromCatalog) {
                    continue;
                }
                Wraiths.GetOrCreate(definition.Key).State = definition.InitialBindState;
            }
            foreach ((string key, float mastery) in FactoryEngravings) {
                WraithProgressRecord record = Wraiths.GetOrCreate(key);
                record.State = WraithBindState.Bound;
                record.Mastery = mastery;
            }
            Wraiths.BumpVersion();
        }

        public override void SaveData(Item item, TagCompound tag) {
            base.SaveData(item, tag);
            tag[InitTag] = true;
            Wraiths.SaveData(tag);
        }

        public override void LoadData(Item item, TagCompound tag) {
            base.LoadData(item, tag);
            if (tag.ContainsKey(InitTag)) {
                Wraiths.LoadData(tag);
                //补种老档缺失的定义初始态(只补缺失绝不覆盖):存档后新加的鬼、
                //以及生来封印者(井中鸣)在旧刀上也封得住
                Wraiths.SeedMissingStates();
            }
            else {
                SeedFactoryState();
            }
        }

        public override void SendLegend(Item item, BinaryWriter writer) => Wraiths.NetSend(writer);

        public override void ReceiveLegend(Item item, BinaryReader reader) => Wraiths.NetReceive(reader);
    }
}
