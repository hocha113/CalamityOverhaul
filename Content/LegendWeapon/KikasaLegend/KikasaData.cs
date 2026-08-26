using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaTalismans;
using CalamityOverhaul.Content.LegendWeapon.TrialQuests;
using System.Collections.Generic;
using Terraria;
using Terraria.ModLoader.IO;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend
{
    /// <summary>
    /// 鬼伞传奇成长数据：等级由沉宴试炼路线推进，随伞存档/联机。<br/>
    /// 符位表 2026-08 迁 <see cref="KikasaTalismanPlayer"/>（他模 SetDefaults 重造物品
    /// 会清光物品级数据，玩家侧才是可靠宿主），本类只余成长字段与旧档遗产读取
    /// </summary>
    internal class KikasaData : LegendData
    {
        internal override IReadOnlyList<LegendTrialDefinition> TrialDefinitions
            => LegendTrialRouteCatalog.KikasaProgression;

        public override int TargetLevel => GetVersionedTrialTargetLevel();

        /// <summary>旧档物品侧符位表（只读遗产，进世界收编用；新档恒 null）</summary>
        internal KikasaTalismanStore LegacyTalismans { get; private set; }

        /// <summary>旧档物品侧修订号，多把伞收编时取最新</summary>
        internal uint LegacyEditRevision { get; private set; }

        public static KikasaData TryGet(Item item) {
            if (item == null || item.IsAir) {
                return null;
            }
            return item.CWR()?.LegendData as KikasaData;
        }

        //SaveData 不覆写：物品侧不再写符位/实例键（Kikasa:InstanceId、Kikasa:EditRevision、
        //KikasaFu:*），旧档残留键成墓碑，随下一次存档自然消失
        public override void LoadData(Item item, TagCompound tag) {
            base.LoadData(item, tag);
            //旧档遗产：只读进收编暂存，修订号供多把伞取舍
            LegacyEditRevision = tag.TryGet("Kikasa:EditRevision", out long revision)
                && revision >= 0 && revision <= uint.MaxValue
                ? (uint)revision : 0u;
            if (tag.ContainsKey("KikasaFu:Slots")) {
                KikasaTalismanStore store = new();
                store.LoadData(tag);
                LegacyTalismans = store.HungCount > 0 ? store : null;
            }
        }
    }
}
