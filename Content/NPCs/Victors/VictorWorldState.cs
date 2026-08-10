using System.IO;
using Terraria;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace CalamityOverhaul.Content.NPCs.Victors
{
    /// <summary>
    /// Victor 世界级状态；首次从传送门登场后转为正常城镇 NPC，
    /// 重生交还原版住房系统（<see cref="Victor.CanTownNPCSpawn"/>）
    /// </summary>
    internal class VictorWorldState : ModSystem
    {
        /// <summary>是否已首次从传送门登场（随世界存档持久化，主端权威）</summary>
        public static bool HasArrived { get; internal set; }

        public override void ClearWorld() => HasArrived = false;

        /// <summary>
        /// 旧档迁移：存档里已有活着的 Victor 说明早已登场；旧版生成时 homeless=false，
        /// 家可能被钉死在传送门落点，交给 QuickFindHome 重新校验（无效即转无家可归进分房流程）
        /// </summary>
        public override void OnWorldLoad() {
            if (VaultUtils.isClient || HasArrived) {
                return;
            }
            int victorType = ModContent.NPCType<Victor>();
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npc = Main.npc[i];
                if (npc?.active != true || npc.type != victorType) {
                    continue;
                }
                HasArrived = true;
                WorldGen.QuickFindHome(i);
                break;
            }
        }

        public override void SaveWorldData(TagCompound tag) {
            if (HasArrived) {
                tag[nameof(HasArrived)] = true;
            }
        }

        public override void LoadWorldData(TagCompound tag)
            => HasArrived = tag.TryGet(nameof(HasArrived), out bool arrived) && arrived;

        public override void NetSend(BinaryWriter writer) => writer.Write(HasArrived);

        public override void NetReceive(BinaryReader reader) => HasArrived = reader.ReadBoolean();
    }
}
