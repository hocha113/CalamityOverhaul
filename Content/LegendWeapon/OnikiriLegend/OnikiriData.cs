using CalamityOverhaul.Content.LegendWeapon.TrialQuests;
using CalamityOverhaul.Content.Wraiths.Core;
using System.Collections.Generic;
using System.IO;
using Terraria;
using Terraria.ModLoader.IO;

namespace CalamityOverhaul.Content.LegendWeapon.OnikiriLegend
{
    /// <summary>
    /// 鬼切传奇数据,每刀一份 <see cref="WraithProgressStore"/>;
    /// 试炼进度驱动 <see cref="LegendData.Level"/>
    /// </summary>
    internal class OnikiriData : LegendData
    {
        internal override IReadOnlyList<LegendTrialDefinition> TrialDefinitions
            => LegendTrialRouteCatalog.OnikiriProgression;

        public override int TargetLevel => GetVersionedTrialTargetLevel();

        //InitTag 区分已存档与功能前老刀;无标吃出厂表
        //曾用 Init1,升位 Init2 使测试刀重播出厂
        private const string InitTag = "OnikiriWraiths:Init2";

        /// <summary>出厂铭刻,Bound+驾驭度</summary>
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

        /// <summary>深拷,每刀各持一份簿</summary>
        public override LegendData Clone(Item item) {
            OnikiriData clone = (OnikiriData)base.Clone(item);
            clone.Wraiths = new WraithProgressStore();
            clone.Wraiths.CopyFrom(Wraiths);
            return clone;
        }

        /// <summary>取鬼切数据,非鬼切/空则 null</summary>
        public static OnikiriData TryGet(Item item) {
            if (item == null || item.IsAir) {
                return null;
            }
            return item.CWR()?.LegendData as OnikiriData;
        }

        /// <summary>出厂态,先 InitialBindState 再盖 Bound+驾驭</summary>
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
