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
    internal class RDaemonsFlame : BaseRItem
    {
        public override int TargetID => ModContent.ItemType<CalamityMod.Items.Weapons.Ranged.DaemonsFlame>();
        public override int ProtogenesisID => ModContent.ItemType<DaemonsFlame>();
        public override void Load() {
            SetReadonlyTargetID = TargetID;
        }
        public override void SetDefaults(Item item) {
            item.damage = 150;
            item.width = 62;
            item.height = 128;
            item.useTime = 12;
            item.useAnimation = 12;
            item.useStyle = ItemUseStyleID.Shoot;
            item.knockBack = 4f;
            item.UseSound = SoundID.Item5;
            item.noMelee = true;
            item.noUseGraphic = true;
            item.DamageType = DamageClass.Ranged;
            item.channel = true;
            item.autoReuse = true;
            item.shoot = ModContent.ProjectileType<DaemonsFlameHeldProj>();
            item.shootSpeed = 20f;
            item.useAmmo = AmmoID.Arrow;
            item.value = CalamityGlobalItem.Rarity13BuyPrice;
            item.rare = ModContent.RarityType<PureGreen>();
            item.Calamity().canFirePointBlankShots = true;
        }

        public override void ModifyTooltips(Item item, List<TooltipLine> tooltips) {
            CWRUtils.OnModifyTooltips(CWRMod.Instance, tooltips, "DaemonsFlame");
        }

        public override bool? UseItem(Item item, Player player) {
            if (CWRUtils.RemakeByItem<CalamityMod.Items.Weapons.Ranged.DaemonsFlame>(item)) {
                if (player.ownedProjectileCounts[item.shoot] > 0)
                    return false;
            }
            return base.UseItem(item, player);
        }

        public override bool? Shoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            Projectile.NewProjectile(source, position.X, position.Y, velocity.X, velocity.Y, ModContent.ProjectileType<DaemonsFlameHeldProj>(), damage, knockback, player.whoAmI);
            return false;
        }
    }
}
