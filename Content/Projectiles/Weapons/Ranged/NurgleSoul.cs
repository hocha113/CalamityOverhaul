using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged
{
    internal class NurgleSoul : ModProjectile
    {
        public override string Texture => CWRConstant.Projectile + "NurgleSoul";
        private NPC Target => Main.npc[(int)Projectile.ai[0]];
        private Vector2 point = Vector2.Zero;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 6;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 1;
        }

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 40;
            Projectile.friendly = true;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 1380;
        }

        public override void AI() {
            VaultUtils.ClockFrame(ref Projectile.frame, 5, 5);
            Lighting.AddLight(Projectile.Center, Color.GreenYellow.ToVector3() * Projectile.scale);
            if (Projectile.ai[0] == 0) {
                SpanDust();
            }

            Projectile.rotation = Projectile.velocity.ToRotation();
            NPC npc = Projectile.Center.FindClosestNPC(1900);
            if (npc != null) {
                if (Projectile.timeLeft == 110) {
                    Projectile.velocity = Projectile.velocity.UnitVector() * 23;
                }
                _ = Projectile.Center.To(npc.Center).LengthSquared() > 80000
                    ? Projectile.ChasingBehavior(npc.Center, 23)
                    : Projectile.SmoothHomingBehavior(npc.Center, 1.001f, 0.15f);
            }
            else {
                Player player = VaultUtils.FindClosestPlayer(Projectile.Center);
                if (player != null) {
                    Vector2 toPlayer = player.Center - Projectile.Center;
                    float distanceToPlayer = toPlayer.Length();
                    float maxFollowDistance = 300f;

                    if (distanceToPlayer < maxFollowDistance) {
                        if (point == Vector2.Zero) {
                            point = Projectile.Center + CWRUtils.randVr(133, 202);
                        }
                        Projectile.SmoothHomingBehavior(point, 1.001f, 0.25f);
                        if (Main.rand.NextBool(1600)) {
                            point = Projectile.Center + CWRUtils.randVr(533, 1202);
                        }
                    }
                    else {
                        float followSpeed = 4f;
                        Vector2 followDirection = toPlayer.SafeNormalize(Vector2.Zero);
                        Projectile.velocity = followDirection * followSpeed;
                    }
                }
            }
            Projectile.ai[0]++;
        }

        private void SpanDust() {
            for (int i = 0; i < 5; i++) {
                int brimDust = Dust.NewDust(new Vector2(Projectile.position.X, Projectile.position.Y), Projectile.width, Projectile.height, DustID.GemEmerald, 0f, 0f, 100, default, 2f);
                Main.dust[brimDust].velocity *= 3f;
                if (Main.rand.NextBool()) {
                    Main.dust[brimDust].scale = 0.5f;
                    Main.dust[brimDust].fadeIn = 1f + (Main.rand.Next(10) * 0.1f);
                }
            }
            for (int j = 0; j < 10; j++) {
                int brimDust2 = Dust.NewDust(new Vector2(Projectile.position.X, Projectile.position.Y), Projectile.width, Projectile.height, DustID.GemEmerald, 0f, 0f, 100, default, 3f);
                Main.dust[brimDust2].noGravity = true;
                Main.dust[brimDust2].velocity *= 5f;
                brimDust2 = Dust.NewDust(new Vector2(Projectile.position.X, Projectile.position.Y), Projectile.width, Projectile.height, DustID.GemEmerald, 0f, 0f, 100, default, 2f);
                Main.dust[brimDust2].velocity *= 2f;
            }
        }

        public override void OnKill(int timeLeft) {
            SpanDust();
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D value = TextureAssets.Projectile[Type].Value;
            Main.EntitySpriteDraw(value, Projectile.Center - Main.screenPosition, value.GetRectangle(Projectile.frame, 6)
                , Color.White, Projectile.rotation, VaultUtils.GetOrig(value, 6), Projectile.scale, Projectile.velocity.X > 0 ? SpriteEffects.None : SpriteEffects.FlipVertically, 0f);
            for (int i = 0; i < Projectile.oldPos.Length; i++) {
                Main.EntitySpriteDraw(value, Projectile.oldPos[i] - Main.screenPosition + (Projectile.Size / 2), value.GetRectangle(Projectile.frame, 6)
                , Color.White * ((6 - i) / 16f), Projectile.rotation, VaultUtils.GetOrig(value, 6), Projectile.scale, Projectile.velocity.X > 0 ? SpriteEffects.None : SpriteEffects.FlipVertically, 0f);
            }
            return false;
        }
    }
}
