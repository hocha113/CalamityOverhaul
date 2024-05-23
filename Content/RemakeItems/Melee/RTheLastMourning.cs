using CalamityMod;
using CalamityMod.CalPlayer;
using CalamityMod.Items;
using CalamityMod.Projectiles.Melee;
using CalamityMod.Rarities;
using CalamityOverhaul.Content.Items.Melee;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee;
using CalamityOverhaul.Content.RemakeItems.Core;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Melee
{
    internal class RTheLastMourning : BaseRItem
    {
        public override int TargetID => ModContent.ItemType<CalamityMod.Items.Weapons.Melee.TheLastMourning>();
        public override int ProtogenesisID => ModContent.ItemType<TheLastMourningEcType>();
        public override string TargetToolTipItemName => "TheLastMourningEcType";

        private bool InTureMelee;
        public override void SetStaticDefaults() {
            ItemID.Sets.ItemsThatAllowRepeatedRightClick[TargetID] = true;
        }

        public override void SetDefaults(Item item) {
            item.width = 94;
            item.height = 94;
            item.scale = 1.5f;
            item.DamageType = DamageClass.Melee;
            item.damage = 280;
            item.knockBack = 8.5f;
            item.useAnimation = 18;
            item.useTime = 18;
            item.autoReuse = true;
            item.useTurn = true;
            item.useStyle = ItemUseStyleID.Swing;
            item.UseSound = SoundID.Item1;
            item.value = CalamityGlobalItem.RarityPureGreenBuyPrice;
            item.rare = ModContent.RarityType<PureGreen>();
            item.Calamity().donorItem = true;
            item.shoot = ModContent.ProjectileType<MourningSkull>();
            item.shootSpeed = 15;
        }

        public override void ModifyWeaponDamage(Item item, Player player, ref StatModifier damage) {
            damage *= InTureMelee ? 1.75f : 1;
        }

        public override void ModifyWeaponKnockback(Item item, Player player, ref StatModifier knockback) {
            knockback *= InTureMelee ? 1.25f : 1;
        }

        public override bool? AltFunctionUse(Item item, Player player) => true;

        public override bool? UseItem(Item item, Player player) {
            item.useAnimation = item.useTime = 18;
            item.scale = 1f;
            InTureMelee = false;
            if (player.altFunctionUse == 2) {
                item.useAnimation = item.useTime = 15;
                item.scale = 1.5f;
                InTureMelee = true;
            }
            return base.UseItem(item, player);
        }

        public override bool? Shoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            if (player.altFunctionUse == 2) {
                return false;
            }
            if (Main.rand.NextBool(3)) {
                Projectile.NewProjectile(source, position + velocity.RotatedBy(Main.rand.NextFloat(-0.15f, 0.15f)) * Main.rand.Next(5)
                    , velocity.UnitVector() * 5, ModContent.ProjectileType<GhostSkull>()
                , damage, knockback, Main.myPlayer, 0f, Main.rand.Next(3), Main.rand.Next(3));
            }
            Projectile.NewProjectile(source, position, velocity, ModContent.ProjectileType<MourningSkull2>()
                , damage / 3, knockback, Main.myPlayer, 0f, Main.rand.Next(3));
            return false;
        }

        public override bool? On_OnHitNPC(Item item, Player player, NPC target, NPC.HitInfo hit, int damageDone) {
            if (player.altFunctionUse == 2) {
                CalamityPlayer.HorsemansBladeOnHit(player, target.whoAmI, item.damage, item.knockBack, 0, item.shoot);
                CalamityPlayer.HorsemansBladeOnHit(player, target.whoAmI, item.damage, item.knockBack, 1);
            }
            return false;
        }
    }
}
