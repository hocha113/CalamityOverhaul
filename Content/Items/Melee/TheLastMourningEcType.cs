using CalamityMod;
using CalamityMod.CalPlayer;
using CalamityMod.Items;
using CalamityMod.Projectiles.Melee;
using CalamityMod.Rarities;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Melee
{
    /// <summary>
    /// 末晓之殇
    /// </summary>
    internal class TheLastMourningEcType : EctypeItem
    {
        public override string Texture => CWRConstant.Cay_Wap_Melee + "TheLastMourning";
        public new string LocalizationCategory => "Items.Weapons.Melee";
        private bool InTureMelee;
        public override void SetStaticDefaults() {
            ItemID.Sets.ItemsThatAllowRepeatedRightClick[Type] = true;
        }

        public override void SetDefaults() {
            Item.width = 94;
            Item.height = 94;
            Item.scale = 1.5f;
            Item.DamageType = DamageClass.Melee;
            Item.damage = 360;
            Item.knockBack = 8.5f;
            Item.useAnimation = 18;
            Item.useTime = 18;
            Item.autoReuse = true;
            Item.useTurn = true;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.UseSound = SoundID.Item1;
            Item.value = CalamityGlobalItem.Rarity13BuyPrice;
            Item.rare = ModContent.RarityType<PureGreen>();
            Item.Calamity().donorItem = true;
            Item.shoot = ModContent.ProjectileType<MourningSkull>();
            Item.shootSpeed = 15;

        }

        public override bool AltFunctionUse(Player player) {
            return true;
        }

        public override void ModifyWeaponDamage(Player player, ref StatModifier damage) {
            damage *= InTureMelee ? 1.5f : 1;
        }

        public override void ModifyWeaponKnockback(Player player, ref StatModifier knockback) {
            knockback *= InTureMelee ? 1.25f : 1;
        }

        public override bool? UseItem(Player player) {
            Item.useAnimation = Item.useTime = 18;
            Item.scale = 1f;
            InTureMelee = false;
            if (player.altFunctionUse == 2) {
                Item.useAnimation = Item.useTime = 15;
                Item.scale = 1.5f;
                InTureMelee = true;
            }

            return base.UseItem(player);
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            if (player.altFunctionUse == 2) {
                return false;
            }
            if (Main.rand.NextBool(3)) {
                _ = Projectile.NewProjectile(source, position + (velocity.RotatedBy(Main.rand.NextFloat(-0.15f, 0.15f)) * Main.rand.Next(5))
                    , velocity.UnitVector() * 5, ModContent.ProjectileType<GhostSkull>()
                , damage, knockback, Main.myPlayer, 0f, Main.rand.Next(3), Main.rand.Next(3));
            }
            _ = Projectile.NewProjectile(source, position, velocity, ModContent.ProjectileType<MourningSkull2>()
                , damage / 3, knockback, Main.myPlayer, 0f, Main.rand.Next(3));
            return false;
        }

        public override void ModifyHitNPC(Player player, NPC target, ref NPC.HitModifiers modifiers) {
            modifiers.CritDamage *= 0.5f;
        }
        public override void OnHitNPC(Player player, NPC target, NPC.HitInfo hit, int damageDone) {
            if (player.altFunctionUse == 2) {
                CalamityPlayer.HorsemansBladeOnHit(player, target.whoAmI, Item.damage, Item.knockBack, 0, Item.shoot);
                CalamityPlayer.HorsemansBladeOnHit(player, target.whoAmI, Item.damage, Item.knockBack, 1);
            }
        }

        public override void OnHitPvp(Player player, Player target, Player.HurtInfo hurtInfo) {
            if (player.altFunctionUse == 2) {
                CalamityPlayer.HorsemansBladeOnHit(player, -1, Item.damage, Item.knockBack, 0, Item.shoot);
                CalamityPlayer.HorsemansBladeOnHit(player, -1, Item.damage, Item.knockBack, 1);
            }
        }

        public override void MeleeEffects(Player player, Rectangle hitbox) {
            if (Main.rand.NextBool(5)) {
                int dustType = 5;
                switch (Main.rand.Next(3)) {
                    case 0:
                        dustType = 5;
                        break;
                    case 1:
                        dustType = 6;
                        break;
                    case 2:
                        dustType = 174;
                        break;
                    default:
                        break;
                }
                int dust = Dust.NewDust(new Vector2(hitbox.X, hitbox.Y), hitbox.Width, hitbox.Height, dustType, player.direction * 2, 0f, 150, default, 1.3f);
                Main.dust[dust].velocity *= 0.2f;
            }
        }
    }
}
