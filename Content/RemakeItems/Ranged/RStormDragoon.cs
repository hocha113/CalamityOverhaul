using CalamityMod;
using CalamityMod.Items;
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
    internal class RStormDragoon : BaseRItem
    {
        public override int TargetID => ModContent.ItemType<CalamityMod.Items.Weapons.Ranged.StormDragoon>();
        public override int ProtogenesisID => ModContent.ItemType<StormDragoon>();
        public override void SetDefaults(Item item) {
            item.damage = 66;
            item.DamageType = DamageClass.Ranged;
            item.width = 74;
            item.height = 34;
            item.useTime = 2;
            item.useAnimation = 20;
            item.reuseDelay = 10;
            item.useLimitPerAnimation = 10;
            item.useStyle = ItemUseStyleID.Shoot;
            item.noMelee = true;
            item.knockBack = 3.25f;
            item.value = CalamityGlobalItem.Rarity12BuyPrice;
            item.rare = ModContent.RarityType<Turquoise>();
            item.UseSound = SoundID.Item31;
            item.autoReuse = true;
            item.shoot = ProjectileID.PurificationPowder;
            item.shootSpeed = 18f;
            item.useAmmo = AmmoID.Bullet;
            item.Calamity().canFirePointBlankShots = true;
            item.CWR().hasHeldNoCanUseBool = true;
            item.CWR().heldProjType = ModContent.ProjectileType<StormDragoonHeldProj>();
        }

        public override void ModifyTooltips(Item item, List<TooltipLine> tooltips) => CWRUtils.OnModifyTooltips(CWRMod.Instance, tooltips, "StormDragoon");

        public override bool? On_Shoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source
            , Vector2 position, Vector2 velocity, int type, int damage, float knockback) => false;
    }
}
