using CalamityMod.Items;
using CalamityMod.Projectiles.Ranged;
using CalamityMod.Sounds;
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
    internal class RElementalBlaster : BaseRItem
    {
        public override int TargetID => ModContent.ItemType<CalamityMod.Items.Weapons.Ranged.ElementalBlaster>();
        public override int ProtogenesisID => ModContent.ItemType<ElementalBlaster>();
        public override void SetDefaults(Item item) {
            item.damage = 67;
            item.DamageType = DamageClass.Ranged;
            item.width = 104;
            item.height = 42;
            item.useTime = 6;
            item.useAnimation = 6;
            item.useStyle = ItemUseStyleID.Shoot;
            item.noMelee = true;
            item.knockBack = 1.75f;
            item.value = CalamityGlobalItem.Rarity11BuyPrice;
            item.rare = ItemRarityID.Purple;
            item.UseSound = CommonCalamitySounds.PlasmaBoltSound;
            item.autoReuse = true;
            item.shoot = ModContent.ProjectileType<RainbowBlast>();
            item.shootSpeed = 18f;
            item.CWR().heldProjType = ModContent.ProjectileType<ElementalBlasterHeldProj>();
            item.CWR().hasHeldNoCanUseBool = true;
        }

        public override void ModifyTooltips(Item item, List<TooltipLine> tooltips) {
            CWRUtils.OnModifyTooltips(CWRMod.Instance, tooltips, "ElementalBlaster");
        }

        public override bool? On_Shoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source
            , Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            return false;
        }
    }
}
