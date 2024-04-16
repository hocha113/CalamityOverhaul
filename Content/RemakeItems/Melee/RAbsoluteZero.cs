using CalamityMod.Buffs.StatDebuffs;
using CalamityMod.Items;
using CalamityOverhaul.Content.Items.Melee;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee;
using CalamityOverhaul.Content.RemakeItems.Core;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Melee
{
    internal class RAbsoluteZero : BaseRItem
    {
        public override int TargetID => ModContent.ItemType<CalamityMod.Items.Weapons.Melee.AbsoluteZero>();
        public override int ProtogenesisID => ModContent.ItemType<AbsoluteZeroEcType>();
        public override string TargetToolTipItemName => "AbsoluteZeroEcType";

        public override void SetDefaults(Item item) {
            item.damage = 75;
            item.DamageType = DamageClass.Melee;
            item.scale = 1.25f;
            item.width = 58;
            item.height = 58;
            item.useTime = 16;
            item.useAnimation = 16;
            item.useStyle = ItemUseStyleID.Swing;
            item.useTurn = false;
            item.knockBack = 2f;
            item.value = CalamityGlobalItem.Rarity8BuyPrice;
            item.rare = ItemRarityID.Yellow;
            item.UseSound = SoundID.Item1;
            item.autoReuse = true;
            item.shoot = ModContent.ProjectileType<DarkIceZeros>();
            item.shootSpeed = 7f;
        }

        public override bool? On_OnHitNPC(Item item, Player player, NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(BuffID.Frostburn2, 300);
            target.AddBuff(ModContent.BuffType<GlacialState>(), 60);
            var source = player.GetSource_ItemUse(item);
            int p = Projectile.NewProjectile(source, target.Center, Vector2.Zero, ModContent.ProjectileType<DarkIceZeros>(), item.damage, 12f, player.whoAmI);
            Main.projectile[p].timeLeft = 12;
            return false;
        }

        public override bool? On_OnHitPvp(Item item, Player player, Player target, Player.HurtInfo hurtInfo) {
            target.AddBuff(BuffID.Frostburn2, 300);
            target.AddBuff(ModContent.BuffType<GlacialState>(), 60);
            var source = player.GetSource_ItemUse(item);
            int p = Projectile.NewProjectile(source, target.Center, Vector2.Zero, ModContent.ProjectileType<DarkIceZeros>(), item.damage, 12f, player.whoAmI);
            Main.projectile[p].timeLeft = 12;
            return false;
        }
    }
}
