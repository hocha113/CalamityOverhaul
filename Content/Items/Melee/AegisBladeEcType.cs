using CalamityMod.Items;
using CalamityMod.Projectiles.Melee;
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
    /// 庇护之刃
    /// </summary>
    internal class AegisBladeEcType : EctypeItem
    {
        public override string Texture => CWRConstant.Cay_Wap_Melee + "AegisBlade";
        public new string LocalizationCategory => "Items.Weapons.Melee";
        public override void SetStaticDefaults() {
            ItemID.Sets.ItemsThatAllowRepeatedRightClick[Item.type] = true;
        }

        public override void SetDefaults() {
            Item.width = 72;
            Item.height = 72;
            Item.scale = 0.9f;
            Item.damage = 108;
            Item.DamageType = DamageClass.Melee;
            Item.useAnimation = Item.useTime = 13;
            Item.useTurn = true;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.knockBack = 2.25f;
            Item.UseSound = SoundID.Item1;
            Item.autoReuse = true;
            Item.shootSpeed = 8f;
            Item.shoot = ProjectileID.PurificationPowder;
            Item.value = CalamityGlobalItem.RarityYellowBuyPrice;
            Item.rare = ItemRarityID.Yellow;
        }

        public override bool AltFunctionUse(Player player) {
            return true;
        }

        public override bool CanUseItem(Player player) {
            if (player.altFunctionUse == 2) {
                Item.noUseGraphic = true;
                Item.noMelee = true;
                Item.UseSound = SoundID.Item73;
                Item.shoot = ModContent.ProjectileType<AegisBladeProj>();
            }
            else {
                Item.noUseGraphic = false;
                Item.noMelee = false;
                Item.UseSound = SoundID.Item73;
                Item.shoot = ModContent.ProjectileType<AegisBeams>();
            }

            return player.ownedProjectileCounts[ModContent.ProjectileType<AegisBladeProj>()] == 0;
        }

        public override float UseSpeedMultiplier(Player player) {
            return player.altFunctionUse != 2 ? 1f : 1.33f;
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            int damages = damage;
            if (player.altFunctionUse == 2) {
                damages = (int)(damage * 3.3f);
            }
            else {
                damages = (int)(damage * 0.3f);
            }
            _ = Projectile.NewProjectile(source, position, velocity, Item.shoot, damages, knockback, player.whoAmI);
            return false;
        }

        public override void MeleeEffects(Player player, Rectangle hitbox) {
            if (Main.rand.NextBool(3)) {
                _ = Dust.NewDust(new Vector2(hitbox.X, hitbox.Y), hitbox.Width, hitbox.Height, DustID.GoldCoin, 0f, 0f, 0, new Color(255, Main.DiscoG, 53));
            }
        }

        public override void ModifyHitNPC(Player player, NPC target, ref NPC.HitModifiers modifiers) {
            modifiers.CritDamage *= 0.5f;
        }

        public override void OnHitNPC(Player player, NPC target, NPC.HitInfo hit, int damageDone) {
            IEntitySource source = player.GetSource_ItemUse(Item);
            _ = Projectile.NewProjectile(source, target.Center, Vector2.Zero, ModContent.ProjectileType<AegisBlast>(), Item.damage, Item.knockBack, Main.myPlayer);
        }

        public override void OnHitPvp(Player player, Player target, Player.HurtInfo hurtInfo) {
            IEntitySource source = player.GetSource_ItemUse(Item);
            _ = Projectile.NewProjectile(source, target.Center, Vector2.Zero, ModContent.ProjectileType<AegisBlast>(), Item.damage, Item.knockBack, Main.myPlayer);
        }
    }
}
