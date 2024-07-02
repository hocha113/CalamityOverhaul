using CalamityOverhaul.Content.Projectiles.Weapons.Summon;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Buffs.Summon
{
    internal class SummonInFlint : ModBuff
    {
        public override string Texture => CWRConstant.Buff + "SummonInFlint";
        public override void SetStaticDefaults() {
            Main.buffNoTimeDisplay[Type] = true;
            Main.buffNoSave[Type] = true;
        }

        public override void Update(Player player, ref int buffIndex) {
            CWRPlayer modPlayer = player.CWR();
            if (player.ownedProjectileCounts[ModContent.ProjectileType<TheSpiritFlintProj>()] > 0) {
                modPlayer.FlintSummonBool = true;
            }
            if (!modPlayer.FlintSummonBool) {
                player.DelBuff(buffIndex);
                buffIndex--;
            }
            else {
                player.buffTime[buffIndex] = 18000;
            }
        }
    }
}
