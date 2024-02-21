using CalamityMod;
using CalamityMod.Items;
using CalamityMod.Projectiles.Ranged;
using CalamityMod.Rarities;
using CalamityOverhaul.Content.Items.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using CalamityOverhaul.Content.RemakeItems.Core;
using Microsoft.Xna.Framework;
using System.Collections.Generic;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Ranged
{
    internal class RCorinthPrime : BaseRItem
    {
        public override int TargetID => ModContent.ItemType<CalamityMod.Items.Weapons.Ranged.CorinthPrime>();
        public override int ProtogenesisID => ModContent.ItemType<CorinthPrime>();
        public override void SetDefaults(Item item) {
            item.damage = 140;
            item.DamageType = DamageClass.Ranged;
            item.width = 106;
            item.height = 42;
            item.useTime = 24;
            item.useAnimation = 24;
            item.useStyle = ItemUseStyleID.Shoot;
            item.noMelee = true;
            item.knockBack = 8f;
            item.value = CalamityGlobalItem.Rarity12BuyPrice;
            item.rare = ModContent.RarityType<Turquoise>();
            item.Calamity().donorItem = true;
            item.UseSound = SoundID.Item38;
            item.autoReuse = true;
            item.shoot = ProjectileID.PurificationPowder;
            item.shootSpeed = 12f;
            item.useAmmo = AmmoID.Bullet;
            item.shoot = ModContent.ProjectileType<RealmRavagerBullet>();
            item.Calamity().canFirePointBlankShots = true;
            item.SetHeldProj<CorinthPrimeHeldProj>();
        }

        public override bool? On_CanUseItem(Item item, Player player) {
            return false;
        }

        public override void ModifyTooltips(Item item, List<TooltipLine> tooltips) {
            CWRUtils.OnModifyTooltips(CWRMod.Instance, tooltips, "CorinthPrime");
        }

        public override bool? On_Shoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source
            , Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            return false;
        }
    }
}
