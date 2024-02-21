using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria.ModLoader;
using Terraria;
using CalamityOverhaul.Content.Items.Ranged;
using CalamityMod.Items;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using Terraria.ID;
using CalamityMod;
using System.Collections.Generic;
using Terraria.DataStructures;
using Microsoft.Xna.Framework;

namespace CalamityOverhaul.Content.RemakeItems.Ranged
{
    internal class RCursedCapper : BaseRItem
    {
        public override int TargetID => ModContent.ItemType<CalamityMod.Items.Weapons.Ranged.CursedCapper>();
        public override int ProtogenesisID => ModContent.ItemType<CursedCapper>();
        public override void SetDefaults(Item item) {
            item.damage = 31;
            item.DamageType = DamageClass.Ranged;
            item.width = 52;
            item.height = 32;
            item.useTime = 10;
            item.useAnimation = 10;
            item.useStyle = ItemUseStyleID.Shoot;
            item.noMelee = true;
            item.knockBack = 2.25f;
            item.value = CalamityGlobalItem.Rarity4BuyPrice;
            item.rare = ItemRarityID.LightRed;
            item.UseSound = SoundID.Item41;
            item.autoReuse = true;
            item.shootSpeed = 14f;
            item.shoot = ProjectileID.CursedBullet;
            item.useAmmo = AmmoID.Bullet;
            item.Calamity().canFirePointBlankShots = true;
            item.SetHeldProj<CursedCapperHeldProj>();
        }

        public override void ModifyTooltips(Item item, List<TooltipLine> tooltips) {
            CWRUtils.OnModifyTooltips(CWRMod.Instance, tooltips, "CursedCapper");
        }

        public override bool? On_Shoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source
            , Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            return false;
        }
    }
}
