using CalamityMod.Items;
using CalamityMod.Projectiles.Healing;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Melee
{
    /// <summary>
    /// 沙之刃
    /// </summary>
    internal class BurntSienna : EctypeItem
    {
        public override string Texture => CWRConstant.Cay_Wap_Melee + "BurntSienna";

        public override void SetDefaults() {
            Item.width = 42;
            Item.damage = 32;
            Item.DamageType = DamageClass.Melee;
            Item.useAnimation = Item.useTime = 21;
            Item.useTurn = true;
            Item.useStyle = 1;
            Item.knockBack = 5.5f;
            Item.UseSound = SoundID.Item1;
            Item.autoReuse = true;
            Item.height = 54;
            Item.value = CalamityGlobalItem.RarityBlueBuyPrice;
            Item.rare = 1;
            Item.shoot = ModContent.ProjectileType<BurntSiennaProj>();
            Item.shootSpeed = 7f;

        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            Item.initialize();
            Item.CWR().ai[0]++;
            if (Item.CWR().ai[0] > 3) {
                for (int i = 0; i < 3; i++) {
                    Vector2 vr = velocity.RotatedBy(MathHelper.ToRadians(Main.rand.NextFloat(-15, 15))) * Main.rand.NextFloat(0.75f, 1.12f);
                    int proj = Projectile.NewProjectile(
                        source,
                        position,
                        vr,
                        ProjectileID.SandBallGun,
                        Item.damage / 2,
                        Item.knockBack,
                        player.whoAmI
                        );
                    Main.projectile[proj].hostile = false;
                    Main.projectile[proj].friendly = true;
                }
                Item.CWR().ai[0] = 0;
            }

            return false;
        }

        public override void OnHitNPC(Player player, NPC target, NPC.HitInfo hit, int damageDone) {
            IEntitySource source = player.GetSource_ItemUse(Item);
            if (target.life <= 0 && !player.moonLeech) {
                float randomSpeedX = Main.rand.Next(3);
                float randomSpeedY = Main.rand.Next(3, 5);
                Projectile.NewProjectile(source, target.Center.X, target.Center.Y, 0f - randomSpeedX, 0f - randomSpeedY, ModContent.ProjectileType<BurntSiennaProj>(), 0, 0f, player.whoAmI, player.whoAmI);
                Projectile.NewProjectile(source, target.Center.X, target.Center.Y, randomSpeedX, 0f - randomSpeedY, ModContent.ProjectileType<BurntSiennaProj>(), 0, 0f, player.whoAmI, player.whoAmI);
                Projectile.NewProjectile(source, target.Center.X, target.Center.Y, 0f, 0f - randomSpeedY, ModContent.ProjectileType<BurntSiennaProj>(), 0, 0f, player.whoAmI, player.whoAmI);
            }
        }

        public override void OnHitPvp(Player player, Player target, Player.HurtInfo hurtInfo) {
            IEntitySource source = player.GetSource_ItemUse(Item);
            if (target.statLife <= 0 && !player.moonLeech) {
                float randomSpeedX = Main.rand.Next(3);
                float randomSpeedY = Main.rand.Next(3, 5);
                Projectile.NewProjectile(source, target.Center.X, target.Center.Y, 0f - randomSpeedX, 0f - randomSpeedY, ModContent.ProjectileType<BurntSiennaProj>(), 0, 0f, player.whoAmI, player.whoAmI);
                Projectile.NewProjectile(source, target.Center.X, target.Center.Y, randomSpeedX, 0f - randomSpeedY, ModContent.ProjectileType<BurntSiennaProj>(), 0, 0f, player.whoAmI, player.whoAmI);
                Projectile.NewProjectile(source, target.Center.X, target.Center.Y, 0f, 0f - randomSpeedY, ModContent.ProjectileType<BurntSiennaProj>(), 0, 0f, player.whoAmI, player.whoAmI);
            }
        }

        public override void MeleeEffects(Player player, Rectangle hitbox) {
            if (Main.rand.NextBool(5)) {
                Dust.NewDust(new Vector2(hitbox.X, hitbox.Y), hitbox.Width, hitbox.Height, 246);
            }
        }
    }
}
