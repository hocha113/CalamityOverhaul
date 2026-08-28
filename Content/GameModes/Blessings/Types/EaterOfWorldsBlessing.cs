using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.Blessings.Types
{
    /// <summary>世界吞噬者·腐环：命中施加侵蚀，削减目标防御</summary>
    internal sealed class EaterOfWorldsBlessing : Blessing
    {
        public override int ProgressOrder => 30;

        public override int[] AnchorNPCTypes =>
            [NPCID.EaterofWorldsHead, NPCID.EaterofWorldsBody, NPCID.EaterofWorldsTail];

        public override string SigilPath =>
            "M72,24 Q88,42 80,64 Q70,86 46,82 Q22,78 20,54 Q19,32 40,24 M72,24 L64,38";

        //体节各自独立血量：最后一节死时残部清零才算讨伐成功
        public override bool IsBossFullyDown(NPC npc) {
            foreach (NPC other in Main.ActiveNPCs) {
                if (other.whoAmI == npc.whoAmI) {
                    continue;
                }
                if (other.type is NPCID.EaterofWorldsHead or NPCID.EaterofWorldsBody or NPCID.EaterofWorldsTail
                    && other.life > 0) {
                    return false;
                }
            }
            return true;
        }

        public override void OnHitNPC(BlessingPlayer bp, NPC target, in NPC.HitInfo hit, int damageDone) {
            if (target.friendly || target.dontTakeDamage) {
                return;
            }
            target.AddBuff(ModContent.BuffType<BlessErosionBuff>(), BlessingTuning.EaterErosionDuration);
        }
    }

    /// <summary>侵蚀：腐环祝福施加的减防状态</summary>
    internal sealed class BlessErosionBuff : ModBuff
    {
        public override string Texture => CWRConstant.VaultPlaceholder2;

        public override LocalizedText DisplayName
            => this.GetLocalization(nameof(DisplayName), () => "侵蚀");

        public override void SetStaticDefaults() {
            Main.debuff[Type] = true;
            Main.pvpBuff[Type] = false;
            Main.buffNoSave[Type] = true;
            Main.buffNoTimeDisplay[Type] = false;
        }
    }

    /// <summary>侵蚀削甲：判伤端按状态削减目标防御，全队受益</summary>
    internal sealed class BlessErosionNPC : GlobalNPC
    {
        public override void ModifyIncomingHit(NPC npc, ref NPC.HitModifiers modifiers) {
            if (npc.HasBuff<BlessErosionBuff>()) {
                modifiers.Defense.Flat -= BlessingTuning.EaterErosionDefense;
            }
        }
    }
}
