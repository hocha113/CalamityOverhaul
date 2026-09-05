using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Armors.Reset
{
    /// <summary>
    /// 熔岩套：头盔防御 +7 暴击 +10%，胸甲防御 +7 伤害 +15%，护胫防御 +6 召唤栏 +3；
    /// 套装奖励为所有攻击附带 6 秒着火、免疫着火了，伤害敌人时 40% 概率引发武器面板 1.5 倍的爆炸
    /// </summary>
    internal class GsMoltenArmor : GsResetArmorScheme
    {
        public override int[] HeadIDs => [ItemID.MoltenHelmet];
        public override int BodyID => ItemID.MoltenBreastplate;
        public override int LegsID => ItemID.MoltenGreaves;

        protected override string HeadLineFallback => "7 more defense and 10% increased critical strike chance";
        protected override string BodyLineFallback => "7 more defense and 15% increased damage";
        protected override string LegsLineFallback => "6 more defense and +3 minion slots";
        protected override string SetBonusLineFallback =>
            "All attacks set enemies on fire for 6 seconds and you are immune to On Fire!; damaging an enemy has a 40% chance to trigger an explosion dealing 1.5x your weapon's damage";

        public override void UpdateHead(Player player, Item item) {
            player.statDefense += 7;
            player.GetCritChance(DamageClass.Generic) += 10f;
        }

        public override void UpdateBody(Player player, Item item) {
            player.statDefense += 7;
            player.GetDamage(DamageClass.Generic) += 0.15f;
        }

        public override void UpdateLegs(Player player, Item item) {
            player.statDefense += 6;
            player.maxMinions += 3;
        }

        public override void UpdateSetBonus(Player player, GodSmithArmorPlayer state) {
            player.buffImmune[BuffID.OnFire] = true;
            player.buffImmune[BuffID.OnFire3] = true;
        }

        public override bool IsOwnEndowProj(Projectile proj) => proj.type == ModContent.ProjectileType<GsArmorBlastProj>();

        public override void OnEndowHitNPC(Player player, GodSmithArmorPlayer state, NPC target,
            in NPC.HitInfo hit, int damageDone, Projectile sourceProj) {
            target.AddBuff(BuffID.OnFire, 360);
            if (Main.rand.Next(100) >= 40) {
                return;
            }
            int damage = (int)(WeaponPanelDamage(player, hit) * 1.5f);
            SpawnBlast(player, target.Center, damage, 140f, "GodSmithMoltenEndow");
        }
    }
}
