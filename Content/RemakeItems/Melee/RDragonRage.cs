using CalamityMod;
using CalamityMod.Items;
using CalamityMod.Rarities;
using CalamityMod.Sounds;
using CalamityOverhaul.Content.Items.Melee;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee.HeldProjectiles;
using CalamityOverhaul.Content.RemakeItems.Core;
using Microsoft.Xna.Framework;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Melee
{
    internal class RDragonRage : BaseRItem
    {
        public override int TargetID => ModContent.ItemType<CalamityMod.Items.Weapons.Melee.DragonRage>();
        public override int ProtogenesisID => ModContent.ItemType<DragonRageEcType>();
        public override string TargetToolTipItemName => "DragonRageEcType";

        public override void Load() {
            SetReadonlyTargetID = TargetID;
        }
        public override void SetStaticDefaults() {
            ItemID.Sets.ItemsThatAllowRepeatedRightClick[ModContent.ItemType<CalamityMod.Items.Weapons.Melee.DragonRage>()] = true;
        }

        public override void SetDefaults(Item item) {
            item.damage = 1075;
            item.knockBack = 7.5f;
            item.useAnimation = (item.useTime = 25);
            item.DamageType = ModContent.GetInstance<TrueMeleeNoSpeedDamageClass>();
            item.noMelee = true;
            item.channel = true;
            item.autoReuse = true;
            item.shootSpeed = 14f;
            item.shoot = ModContent.ProjectileType<RDragonRageStaff>();
            item.width = 128;
            item.height = 140;
            item.noUseGraphic = true;
            item.useStyle = ItemUseStyleID.Shoot;
            item.UseSound = SoundID.DD2_SkyDragonsFurySwing;
            item.value = CalamityGlobalItem.RarityVioletBuyPrice;
            item.rare = ModContent.RarityType<Violet>();
        }

        public override bool? Shoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            int proj = Projectile.NewProjectile(source, position, velocity, type, damage, knockback, player.whoAmI);
            if (player.altFunctionUse == 2) {
                SoundEngine.PlaySound(in CommonCalamitySounds.MeatySlashSound, player.Center);
                Main.projectile[proj].ai[1] = 1;
                Main.projectile[proj].scale = 0.5f;
            }
            return false;
        }

        public override bool? AltFunctionUse(Item item, Player player) {
            return true;
        }

        public override bool? UseItem(Item item, Player player) {
            return player.ownedProjectileCounts[item.shoot] == 0;
        }
    }
}
