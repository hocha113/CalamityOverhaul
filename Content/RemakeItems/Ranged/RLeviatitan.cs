using CalamityMod.Items;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria;
using CalamityOverhaul.Content.Items.Ranged;
using CalamityMod.Projectiles.Ranged;
using CalamityMod;
using System.Collections.Generic;
using Terraria.DataStructures;
using Microsoft.Xna.Framework;

namespace CalamityOverhaul.Content.RemakeItems.Ranged
{
    internal class RLeviatitan : BaseRItem
    {
        public override int TargetID => ModContent.ItemType<CalamityMod.Items.Weapons.Ranged.Leviatitan>();
        public override int ProtogenesisID => ModContent.ItemType<Leviatitan>();
        public override void SetDefaults(Item item) {
            item.damage = 80;
            item.DamageType = DamageClass.Ranged;
            item.width = 82;
            item.height = 28;
            item.useTime = 9;
            item.useAnimation = 9;
            item.useStyle = ItemUseStyleID.Shoot;
            item.noMelee = true;
            item.knockBack = 5f;
            item.value = CalamityGlobalItem.Rarity7BuyPrice;
            item.rare = ItemRarityID.Lime;
            item.UseSound = SoundID.Item92;
            item.autoReuse = true;
            item.shoot = ModContent.ProjectileType<AquaBlast>();
            item.shootSpeed = 12f;
            item.useAmmo = AmmoID.Bullet;
            item.Calamity().canFirePointBlankShots = true;
            item.SetHeldProj<LeviatitanHeldProj>();
        }

        public override void ModifyTooltips(Item item, List<TooltipLine> tooltips) {
            CWRUtils.OnModifyTooltips(CWRMod.Instance, tooltips, "Leviatitan");
        }

        public override bool? On_Shoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source
            , Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            return false;
        }
    }
}
