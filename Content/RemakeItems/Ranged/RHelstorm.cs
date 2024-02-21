using CalamityMod.Items;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria;
using CalamityOverhaul.Content.Items.Ranged;
using CalamityMod;
using System.Collections.Generic;
using Terraria.DataStructures;
using Microsoft.Xna.Framework;

namespace CalamityOverhaul.Content.RemakeItems.Ranged
{
    internal class RHelstorm : BaseRItem
    {
        public override int TargetID => ModContent.ItemType<CalamityMod.Items.Weapons.Ranged.Helstorm>();
        public override int ProtogenesisID => ModContent.ItemType<Helstorm>();
        public override void SetDefaults(Item item) {
            item.damage = 31;
            item.DamageType = DamageClass.Ranged;
            item.width = 50;
            item.height = 24;
            item.useTime = 7;
            item.useAnimation = 7;
            item.useStyle = ItemUseStyleID.Shoot;
            item.knockBack = 2.5f;
            item.value = CalamityGlobalItem.Rarity8BuyPrice;
            item.rare = ItemRarityID.Yellow;
            item.UseSound = SoundID.Item11;
            item.autoReuse = true;
            item.noMelee = true;
            item.shoot = ProjectileID.PurificationPowder;
            item.shootSpeed = 11.5f;
            item.useAmmo = AmmoID.Bullet;
            item.Calamity().canFirePointBlankShots = true;
            item.SetHeldProj<HelstormHeldProj>();
        }

        public override void ModifyTooltips(Item item, List<TooltipLine> tooltips) {
            CWRUtils.OnModifyTooltips(CWRMod.Instance, tooltips, "Helstorm");
        }

        public override bool? On_Shoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source
            , Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            return false;
        }
    }
}
