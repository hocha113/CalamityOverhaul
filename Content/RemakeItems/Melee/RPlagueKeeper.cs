using CalamityMod.Items;
using CalamityOverhaul.Content.Items.Melee;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee.PlagueProj;
using CalamityOverhaul.Content.RemakeItems.Core;
using Microsoft.Xna.Framework;
using System.Collections.Generic;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Melee
{
    internal class RPlagueKeeper : BaseRItem
    {
        public override int TargetID => ModContent.ItemType<CalamityMod.Items.Weapons.Melee.PlagueKeeper>();
        public override int ProtogenesisID => ModContent.ItemType<PlagueKeeperEcType>();
        public override string TargetToolTipItemName => "PlagueKeeperEcType";


        public override void SetDefaults(Item item) {
            item.width = 74;
            item.damage = 80;
            item.DamageType = DamageClass.Melee;
            item.useAnimation = 20;
            item.useStyle = ItemUseStyleID.Swing;
            item.useTime = 20;
            item.useTurn = true;
            item.knockBack = 6f;
            item.UseSound = SoundID.Item1;
            item.autoReuse = true;
            item.height = 90;
            item.value = CalamityGlobalItem.Rarity10BuyPrice;
            item.rare = ItemRarityID.Red;
            item.shoot = ModContent.ProjectileType<PlagueBeeWave>();
            item.shootSpeed = 9f;
        }

        public override bool? Shoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            Projectile.NewProjectile(source, position, velocity, ModContent.ProjectileType<GouldBee>(), damage, 0, player.whoAmI);
            return base.Shoot(item, player, source, position, velocity, type, damage, knockback);
        }
    }
}
