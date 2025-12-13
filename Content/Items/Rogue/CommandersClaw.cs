using CalamityMod;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Rogue
{
    internal class CommandersClaw : ModItem
    {
        public override string Texture => CWRConstant.Item_Rogue + "CommandersClaw";
        public override void SetDefaults() {
            Item.width = Item.height = 52;
            Item.damage = 82;
            Item.noMelee = true;
            Item.noUseGraphic = true;
            Item.useAnimation = Item.useTime = 39;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.knockBack = 7f;
            Item.UseSound = SoundID.Item1;
            Item.autoReuse = true;
            Item.shoot = ModContent.ProjectileType<CommandersClawThrow>();
            Item.shootSpeed = 17f;
            Item.DamageType = CWRRef.GetRogueDamageClass();
            Item.rare = ItemRarityID.Purple;
            Item.value = Item.buyPrice(0, 1, 65, 0);
            Item.CWR().DeathModeItem = true;
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source
            , Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            Projectile.NewProjectile(source, position, velocity, type, damage, knockback, player.whoAmI, player.Calamity().StealthStrikeAvailable() ? 1 : 0);
            return false;
        }
    }

    internal class CommandersClawThrow : ModProjectile
    {
        public override string Texture => CWRConstant.Item_Rogue + "CommandersClawThrow";
        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 8;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 1;
        }
        public override void SetDefaults() {
            Projectile.width = 20;
            Projectile.height = 20;
            Projectile.friendly = true;
            Projectile.ignoreWater = true;
            Projectile.penetrate = 6;
            Projectile.timeLeft = 900;
            Projectile.DamageType = CWRRef.GetRogueDamageClass();
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 50;
            Projectile.extraUpdates = 1;
            Projectile.CWR().Viscosity = true;
        }

        public override void AI() {
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver4;
            Projectile.spriteDirection = Projectile.velocity.X > 0 ? 1 : -1;
            if (Projectile.ai[0] > 0 && Projectile.ai[1] > 30) {
                NPC target = Projectile.Center.FindClosestNPC(800, false, true);
                if (target != null) {
                    Projectile.SmoothHomingBehavior(target.Center, 1, 0.2f);
                }
            }

            if (Projectile.ai[0] == 0 && ++Projectile.ai[1] > 60 && Projectile.velocity.Y < 60) {
                Projectile.velocity.Y += 1f;
            }

            Projectile.ai[1]++;
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D texture = TextureAssets.Projectile[Projectile.type].Value;

            for (int k = 0; k < Projectile.oldPos.Length; k++) {
                Vector2 drawPos = Projectile.oldPos[k] - Main.screenPosition + Projectile.Size / 2;
                Color color = Color.White * (float)((Projectile.oldPos.Length - k) / (float)Projectile.oldPos.Length / 2);
                Main.EntitySpriteDraw(texture, drawPos, null, color, Projectile.rotation + (Projectile.spriteDirection > 0 ? 0 : -MathHelper.PiOver2)
                , texture.Size() / 2, Projectile.scale, Projectile.spriteDirection > 0 ? SpriteEffects.None : SpriteEffects.FlipVertically);
            }

            Main.EntitySpriteDraw(texture, Projectile.Center - Main.screenPosition, null, lightColor
                , Projectile.rotation + (Projectile.spriteDirection > 0 ? 0 : -MathHelper.PiOver2)
                , texture.Size() / 2, Projectile.scale, Projectile.spriteDirection > 0 ? SpriteEffects.None : SpriteEffects.FlipVertically);
            return false;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (Projectile.ai[0] > 0 && Projectile.numHits == 0) {
                for (int i = 0; i < 8; i++) {
                    Vector2 ver = Projectile.velocity.RotatedBy(MathHelper.TwoPi / 8f * i);
                    Projectile.NewProjectile(Projectile.FromObjectGetParent(), target.Center, ver, ModContent.ProjectileType<PunisherGrenadeRogue>()
                    , (int)(Projectile.damage * 0.75f), Projectile.knockBack, Projectile.owner, Main.rand.NextBool() ? 0 : 1);
                }
                Projectile.numHits++;
            }
        }

        public override void OnKill(int timeLeft) {
            CWRUtils.SplashDust(Projectile, 121, DustID.FireworkFountain_Red, DustID.FireworkFountain_Red, 13, Color.White);
            Projectile.Explode();
        }
    }
}