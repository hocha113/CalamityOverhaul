using CalamityOverhaul.Content.Wraiths.Core;
using System.IO;
using Terraria;
using Terraria.ModLoader.IO;

namespace CalamityOverhaul.Content.LegendWeapon.OnikiriLegend
{
    /// <summary>
    /// 鬼切的传奇数据：点鬼簿绑定层。每把刀实例各持一份 <see cref="WraithProgressStore"/>，
    /// 随物品存档与联机同步；无试炼路线（TargetLevel 恒 0），传奇升级流程天然不触发
    /// </summary>
    internal class OnikiriData : LegendData
    {
        //存档标记:区分"存过档(即使记录全默认)"与"本功能之前的老刀"。
        //老档兼容语义:带 InitTag 的刀读档回放存档值(演示期的高驾驭就此保留,不强行收敛);
        //无 InitTag 的老刀与新刀一样吃下方出厂表
        private const string InitTag = "OnikiriWraiths:Init";

        /// <summary>
        /// 出厂铭刻名单：新刀按此表落 Bound + 驾驭度。
        /// 认主叙事（刀认你，鬼未必认你）：契约在而威信失，出厂驾驭度收敛至认主前低位
        /// 0.15~0.35（保留演示期的个体差异排序），重续契约后才跃升至 RenewedMastery
        /// </summary>
        private static readonly (string Key, float Mastery)[] FactoryEngravings = [
            ("NoFace", 0.35f),
            ("LanternBoy", 0.27f),
            ("CrimsonBride", 0.15f),
            ("StandIn", 0.31f),
            ("HeadlessShade", 0.18f),
            ("GhostHand", 0.22f),
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
