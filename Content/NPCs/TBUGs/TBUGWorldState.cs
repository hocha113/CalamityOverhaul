using System.IO;
using Terraria;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace CalamityOverhaul.Content.NPCs.TBUGs
{
    /// <summary>
    /// TBUG 世界级状态；首次从裂缝登场后转为正常城镇 NPC，
    /// 重生交还原版住房系统（<see cref="TBUG.CanTownNPCSpawn"/>）
    /// </summary>
    internal class TBUGWorldState : ModSystem
    {
        /// <summary>是否已首次从裂缝登场（随世界存档持久化，主端权威）</summary>
        public static bool HasArrived { get; internal set; }

        public override void ClearWorld() => HasArrived = false;

        /// <summary>兜底迁移：存档里已有活着的 TBUG 说明早已登场</summary>
        public override void OnWorldLoad() {
            if (VaultUtils.isClient || HasArrived) {
                return;
            }
            int tbugType = ModContent.NPCType<TBUG>();
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npc = Main.npc[i];
                if (npc?.active != true || npc.type != tbugType) {
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
