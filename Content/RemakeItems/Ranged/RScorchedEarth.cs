using CalamityMod.Items;
using CalamityMod.Rarities;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using CalamityOverhaul.Content.RemakeItems.Core;
using System.Collections.Generic;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria;
using CalamityOverhaul.Content.Items.Ranged;
using CalamityMod.Projectiles.Ranged;
using Microsoft.Xna.Framework;
using CalamityMod;

namespace CalamityOverhaul.Content.RemakeItems.Ranged
{
    internal class RScorchedEarth : BaseRItem
    {
        public override int TargetID => ModContent.ItemType<CalamityMod.Items.Weapons.Ranged.ScorchedEarth>();
        public override int ProtogenesisID => ModContent.ItemType<ScorchedEarth>();
        public override void SetDefaults(Item item) {
            item.damage = 800;
            item.DamageType = DamageClass.Ranged;
            item.useTime = 88;
            item.useAnimation = 32;
            item.reuseDelay = 60;
            item.useLimitPerAnimation = 4;
            item.width = 104;
            item.height = 44;
            item.useStyle = ItemUseStyleID.Shoot;
            item.noMelee = true;
            item.knockBack = 8.7f;
            item.value = CalamityGlobalItem.Rarity14BuyPrice;
            item.rare = ModContent.RarityType<DarkBlue>();
            item.autoReuse = true;
            item.shootSpeed = 12.6f;
            item.shoot = ModContent.ProjectileType<ScorchedEarthRocket>();
            item.useAmmo = AmmoID.Rocket;
            item.Calamity().donorItem = true;
            item.SetHeldProj<ScorchedEarthHeldProj>();
        }

        public override void ModifyTooltips(Item item, List<TooltipLine> tooltips) => CWRUtils.OnModifyTooltips(CWRMod.Instance, tooltips, "ScorchedEarth");

        public override bool? On_Shoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source
            , Vector2 position, Vector2 velocity, int type, int damage, float knockback) => false;
    }
}
