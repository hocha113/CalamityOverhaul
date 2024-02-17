using CalamityMod.Items;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using CalamityOverhaul.Content.RemakeItems.Core;
using System.Collections.Generic;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria;
using Microsoft.Xna.Framework;
using CalamityMod;
using CalamityOverhaul.Content.Items.Ranged;

namespace CalamityOverhaul.Content.RemakeItems.Ranged
{
    internal class RClockGatlignum : BaseRItem
    {
        public override int TargetID => ModContent.ItemType<CalamityMod.Items.Weapons.Ranged.ClockGatlignum>();
        public override int ProtogenesisID => ModContent.ItemType<ClockGatlignum>();
        public override void SetDefaults(Item item) {
            item.damage = 35;
            item.DamageType = DamageClass.Ranged;
            item.width = 66;
            item.height = 34;
            item.useTime = 3;
            item.useAnimation = 9;
            item.reuseDelay = 12;
            item.useLimitPerAnimation = 3;
            item.useStyle = ItemUseStyleID.Shoot;
            item.noMelee = true;
            item.knockBack = 3.75f;
            item.value = CalamityGlobalItem.Rarity8BuyPrice;
            item.rare = ItemRarityID.Yellow;
            item.UseSound = SoundID.Item31;
            item.autoReuse = true;
            item.shoot = ProjectileID.PurificationPowder;
            item.shootSpeed = 20f;
            item.useAmmo = AmmoID.Bullet;
            item.Calamity().canFirePointBlankShots = true;
            item.CWR().heldProjType = ModContent.ProjectileType<ClockGatlignumHeldProj>();
            item.CWR().hasHeldNoCanUseBool = true;
        }

        public override void ModifyTooltips(Item item, List<TooltipLine> tooltips) {
            CWRUtils.OnModifyTooltips(CWRMod.Instance, tooltips, "ClockGatlignum");
        }

        public override bool? On_Shoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source
            , Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            return false;
        }
    }
}
