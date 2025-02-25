using CalamityMod;
using CalamityMod.Particles;
using CalamityOverhaul.Common;
using InnoVault.GameContent.BaseEntity;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Melee.RebelBladeProj
{
    internal class RebelBladeFlyAttcke : BaseHeldProj
    {
        public override string Texture => CWRConstant.Item_Melee + "RebelBlade";

        private Color tillColor = Color.White;
        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailingMode[Projectile.type] = 3;
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 8;
        }
        public override void SetDefaults() {
            Projectile.width = Projectile.height = 45;
            Projectile.timeLeft = 200;
            Projectile.knockBack = 2;
            Projectile.friendly = true;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1;
            Projectile.extraUpdates = 1;
        }

        public override void AI() {
            Projectile.Calamity().timesPierced = 0;

            if (Projectile.localAI[1] <= 0) {
                Projectile.rotation = Projectile.velocity.ToRotation();
            }

            if (Projectile.localAI[0] > 0 || Projectile.localAI[1] > 0) {
                tillColor = Color.Red;
            }

            if (!DownRight) {
                Projectile.tileCollide = false;
                tillColor = Color.CadetBlue;
                Projectile.ChasingBehavior(Owner.Center, 23);
                if (Projectile.Distance(Owner.Center) < 80) {
                    Projectile.Kill();
                }
            }
            else if (Projectile.localAI[1] <= 0) {
                tillColor = Color.Yellow;
                Projectile.tileCollide = true;
                Projectile.timeLeft = 200;
                Owner.itemRotation = (Projectile.velocity * Projectile.direction).ToRotation();
                Vector2 mousePos = ToMouse + Owner.GetPlayerStabilityCenter();
                Vector2 ver = Projectile.Center.To(mousePos);
                if (Projectile.IsOwnedByLocalPlayer()) {
                    Projectile.ai[0] += Main.rand.Next(1, 3);
                    Projectile.netUpdate = true;//肮脏的手段——HoCha113, 2024-06-02 02:37
                }
                if (Projectile.ai[0] > 30) {
                    SoundEngine.PlaySound(SoundID.Item7, Projectile.Center);
                    Projectile.velocity = ver.UnitVector() * 45;
                    Projectile.ai[0] = 0;
                }
                Projectile.velocity *= 0.98f;
                if (ver.Length() < 16) {
                    Projectile.velocity = Projectile.velocity.RotatedByRandom(0.9f);
                }
            }

            if (Projectile.localAI[0] > 0) {
                Projectile.localAI[0]--;
            }
            if (Projectile.localAI[1] > 0) {
                Projectile.localAI[1]--;
            }

            float rot = (MathHelper.PiOver2 * SafeGravDir - Owner.Center.To(Projectile.Center).ToRotation()) * DirSign * SafeGravDir;
            float rot2 = (MathHelper.PiOver2 * SafeGravDir - MathHelper.ToRadians(DirSign > 0 ? -20 : 200)) * DirSign * SafeGravDir;
            Owner.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.Full, rot * -DirSign);
            Owner.SetCompositeArmBack(true, Player.CompositeArmStretchAmount.Full, rot2 * -DirSign);
            Owner.direction = Owner.Center.To(Projectile.Center).X > 0 ? 1 : -1;

            Lighting.AddLight(Projectile.Center, tillColor.ToVector3() * 2.2f);
        }

        private void HitEffet(Vector2 returnVer) {
            if (Projectile.localAI[0] <= 0) {
                Projectile.localAI[0] = 12;
                Projectile.localAI[1] = 12;
                Projectile.rotation = (-Projectile.velocity).ToRotation();
                Vector2 splatterDirection = returnVer.SafeNormalize(Vector2.UnitY);
                for (int j = 0; j < 3; j++) {
                    float sparkScale = Main.rand.NextFloat(1.2f, 2.33f);
                    int sparkLifetime = Main.rand.Next(22, 36);
                    Color sparkColor = Color.Lerp(Color.Silver, Color.Gold, Main.rand.NextFloat(0.7f));
                    Vector2 sparkVelocity = splatterDirection.RotatedByRandom(0.9f) * Main.rand.NextFloat(19f, 34.5f);
                    SparkParticle spark = new SparkParticle(Projectile.Center, sparkVelocity, true, sparkLifetime, sparkScale, sparkColor);
                    GeneralParticleHandler.SpawnParticle(spark);
                }
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            HitEffet(Projectile.velocity);
            if (Projectile.damage < Projectile.originalDamage * 5) {
                Projectile.damage += 15;
            }
            Projectile.velocity = Projectile.velocity.RotatedByRandom(0.6f);
            Projectile.rotation = Projectile.velocity.ToRotation();
        }

        public override bool OnTileCollide(Vector2 oldVelocity) {
            Projectile.timeLeft = 30;
            Projectile.velocity = -oldVelocity;
            Projectile.DigByTile(CWRSound.HitTheSteel with { MaxInstances = 3, Volume = 0.5f });
            HitEffet(Projectile.velocity);
            return false;
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D texture = TextureAssets.Projectile[Projectile.type].Value;
            Rectangle rectangle = CWRUtils.GetRec(texture);
            Vector2 drawOrigin = rectangle.Size() / 2;

            for (int k = 0; k < Projectile.oldPos.Length; k++) {
                Vector2 drawPos = Projectile.oldPos[k] - Main.screenPosition + Projectile.Size / 2;
                Color color = lightColor * (float)((Projectile.oldPos.Length - k) / (float)Projectile.oldPos.Length / 2);
                Main.EntitySpriteDraw(texture, drawPos, rectangle, color, Projectile.oldRot[k] + MathHelper.PiOver4, drawOrigin, Projectile.scale, SpriteEffects.None, 0);
            }
            Main.EntitySpriteDraw(texture, Projectile.Center - Main.screenPosition, null, lightColor
                , Projectile.rotation + MathHelper.PiOver4, drawOrigin, Projectile.scale, SpriteEffects.None, 0);
            return false;
        }
    }
}
