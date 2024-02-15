using CalamityMod.Items;
using CalamityMod.Projectiles.Ranged;
using CalamityMod.Rarities;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria;
using CalamityOverhaul.Content.Items.Ranged;
using CalamityMod;
using Microsoft.Xna.Framework;
using System.Collections.Generic;
using CalamityOverhaul.Content.Items;

namespace CalamityOverhaul.Content.RemakeItems.Ranged
{
    internal class RAlluvion : BaseRItem
    {
        public override int TargetID => ModContent.ItemType<CalamityMod.Items.Weapons.Ranged.Alluvion>();
        public override int ProtogenesisID => ModContent.ItemType<Alluvion>();
        public override void SetDefaults(Item item) {
            item.damage = 165;
            item.DamageType = DamageClass.Ranged;
            item.width = 62;
            item.height = 90;
            item.useTime = 15;
            item.useAnimation = 30;
            item.useStyle = ItemUseStyleID.Shoot;
            item.noMelee = true;
            item.knockBack = 4f;
            item.value = CalamityGlobalItem.RarityDarkBlueBuyPrice;
            item.UseSound = SoundID.Item5;
            item.autoReuse = true;
            item.shoot = ProjectileID.WoodenArrowFriendly;
            item.shootSpeed = 12f;
            item.useAmmo = AmmoID.Arrow;
            item.rare = ModContent.RarityType<DarkBlue>();
            item.Calamity().canFirePointBlankShots = true;
            item.CWR().hasHeldNoCanUseBool = true;
            item.CWR().heldProjType = ModContent.ProjectileType<AlluvionHeldProj>();
        }

        public override void ModifyTooltips(Item item, List<TooltipLine> tooltips) => CWRUtils.OnModifyTooltips(CWRMod.Instance, tooltips, "Alluvion");

        public override bool? On_Shoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source
            , Vector2 position, Vector2 velocity, int type, int damage, float knockback) => false;
    }
}
