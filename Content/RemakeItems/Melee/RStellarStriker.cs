using CalamityMod.Items;
using CalamityOverhaul.Content.Items.Melee;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee;
using CalamityOverhaul.Content.RemakeItems.Core;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Melee
{
    internal class RStellarStriker : BaseRItem
    {
        public override int TargetID => ModContent.ItemType<CalamityMod.Items.Weapons.Melee.StellarStriker>();
        public override int ProtogenesisID => ModContent.ItemType<StellarStrikerEcType>();
        public override string TargetToolTipItemName => "StellarStrikerEcType";

        public override void SetStaticDefaults() {
            ItemID.Sets.ItemsThatAllowRepeatedRightClick[TargetID] = true;
        }

        public override void SetDefaults(Item item) {
            item.width = 90;
            item.height = 100;
            item.scale = 1.5f;
            item.damage = 480;
            item.DamageType = DamageClass.Melee;
            item.useAnimation = 20;
            item.useStyle = ItemUseStyleID.Swing;
            item.useTime = 20;
            item.useTurn = true;
            item.knockBack = 7.75f;
            item.UseSound = SoundID.Item1;
            item.autoReuse = true;
            item.value = CalamityGlobalItem.RarityPurpleBuyPrice;
            item.rare = ItemRarityID.Red;
            item.shoot = ProjectileID.LunarFlare;
            item.shootSpeed = 12f;
        }

        public override bool? AltFunctionUse(Item item, Player player) {
            return true;
        }

        public override bool? UseItem(Item item, Player player) {
            item.useAnimation = item.useTime = 15;
            item.scale = 1f;
            if (player.altFunctionUse == 2) {
                item.useAnimation = item.useTime = 20;
                item.scale = 1.5f;
            }
            return base.UseItem(item, player);
        }

        public override bool? Shoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            if (player.altFunctionUse == 2) {
                return false;
            }
            SoundEngine.PlaySound(SoundID.Item88, player.Center);
            Projectile.NewProjectile(source, position, velocity, ModContent.ProjectileType<StellarStrikerBeam>()
                , damage / 3, knockback, Main.myPlayer, 0f, Main.rand.Next(3));
            return false;
        }

        public override bool? On_OnHitNPC(Item item, Player player, NPC target, NPC.HitInfo hit, int damageDone) {
            if (player.altFunctionUse == 2) {
                StellarStrikerEcType.SpawnFlares(item, player, item.knockBack, item.damage, hit.Crit);
            }
            return false;
        }

        public override bool? On_OnHitPvp(Item item, Player player, Player target, Player.HurtInfo hurtInfo) {
            if (player.whoAmI == Main.myPlayer && player.altFunctionUse == 2) {
                StellarStrikerEcType.SpawnFlares(item, player, item.knockBack, item.damage, true);
            }
            return false;
        }
    }
}
