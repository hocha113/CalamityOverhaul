using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Armors.Reset
{
    /// <summary>
    /// 黑曜石套：头盔防御 +4 伤害 +7%，衣防御 +3 暴击 +5%，裤防御 +3 魔耗 -15%；
    /// 套装奖励为完全免疫岩浆，伤害敌人时 30% 概率引发武器面板 1.1 倍的爆炸
    /// </summary>
    internal class GsObsidianArmor : GsResetArmorScheme
    {
        public override int[] HeadIDs => [ItemID.ObsidianHelm];
        public override int BodyID => ItemID.ObsidianShirt;
        public override int LegsID => ItemID.ObsidianPants;

        protected override string HeadLineFallback => "4 more defense and 7% increased damage";
        protected override string BodyLineFallback => "3 more defense and 5% increased critical strike chance";
        protected override string LegsLineFallback => "3 more defense and 15% reduced mana usage";
        protected override string SetBonusLineFallback =>
            "Immune to lava; damaging an enemy has a 30% chance to trigger an explosion dealing 1.1x your weapon's damage";

        public override void UpdateHead(Player player, Item item) {
            player.statDefense += 4;
            player.GetDamage(DamageClass.Generic) += 0.07f;
        }

        public override void UpdateBody(Player player, Item item) {
            player.statDefense += 3;
            player.GetCritChance(DamageClass.Generic) += 5f;
        }

        public override void UpdateLegs(Player player, Item item) {
            player.statDefense += 3;
            player.manaCost -= 0.15f;
        }

        public override void UpdateSetBonus(Player player, GodSmithArmorPlayer state) {
            player.lavaImmune = true;
            player.fireWalk = true;
        }

        public override bool IsOwnEndowProj(Projectile proj) => proj.type == ModContent.ProjectileType<GsArmorBlastProj>();

        public override void OnEndowHitNPC(Player player, GodSmithArmorPlayer state, NPC target,
            in NPC.HitInfo hit, int damageDone, Projectile sourceProj) {
            if (Main.rand.Next(100) >= 30) {
                return;
            }
            int damage = (int)(WeaponPanelDamage(player, hit) * 1.1f);
            SpawnBlast(player, target.Center, damage, 110f, "GodSmithObsidianEndow");
        }
    }
}
