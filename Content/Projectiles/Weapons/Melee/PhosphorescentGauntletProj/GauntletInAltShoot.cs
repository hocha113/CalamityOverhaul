using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Melee.PhosphorescentGauntletProj
{
    internal class GauntletInAltShoot : ModProjectile
    {
        public override string Texture => CWRConstant.Cay_Wap_Melee + "PhosphorescentGauntlet";
        public override void AutoStaticDefaults() => AutoProj.AutoStaticDefaults(this);
        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailingMode[Type] = 2;
            ProjectileID.Sets.TrailCacheLength[Type] = 8;
        }

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 122;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = false;
            Projectile.extraUpdates = 3;
            Projectile.timeLeft = 120 * Projectile.MaxUpdates;
        }

        public override void AI() {
            Lighting.AddLight(Projectile.Center, Color.Gold.ToVector3() * 3);
            Projectile.rotation = Projectile.velocity.ToRotation();
            if (Projectile.ai[0] == 0) {
                SpanDust(Projectile, 1);
            }
            if (!VaultUtils.isServer) {
                Vector2 pos = Projectile.Center + Main.rand.NextVector2Unit() * Main.rand.Next(3, Projectile.width);

                PRT_Gauntlet glow = new PRT_Gauntlet(pos, Projectile.velocity, Projectile.scale * Main.rand.NextFloat(1, 1.2f), Main.rand.Next(15, 22), 1);
                PRTLoader.AddParticle(glow);
                SpanDust(pos);
            }

            Projectile.ai[0]++;
        }

        public void SpanDust(Vector2 origPos) {
            if (Main.dedServ) {
                return;
            }
            for (int i = 0; i < 12; i++) {
                Vector2 offset = Vector2.UnitX * (0f - Projectile.width) / 2f;
                offset += -Vector2.UnitY.RotatedBy(i * VaultUtils.PiOver6) * new Vector2(8f, 16f);
                offset = offset.RotatedBy(Projectile.rotation);
                int electric = Dust.NewDust(origPos, 0, 0, DustID.JungleTorch, 0f, 0f, 160);
                Main.dust[electric].scale = 1.1f;
                Main.dust[electric].noGravity = true;
                Main.dust[electric].position = origPos + offset;
                Main.dust[electric].velocity = Projectile.velocity * 0.1f;
                Main.dust[electric].velocity = Vector2.Normalize(origPos - Projectile.velocity * 3f - Main.dust[electric].position) * 1.25f;
            }
        }

        public static void SpanDust(Projectile projectile, float salce) {
            if (Main.dedServ) {
                return;
            }
            Vector2 vr = projectile.velocity.UnitVector();
            Vector2 topLeft = projectile.Center + vr.RotatedBy(-MathHelper.PiOver2) * 40f * salce;
            Vector2 top = projectile.Center + vr * 70f * salce;
            Vector2 topRight = projectile.Center + vr.RotatedBy(MathHelper.PiOver2) * 40f * salce;
            foreach (Vector2 spawnPosition in CWRRef.BezierCurveGetPoints(50, topLeft, top, topRight)) {
                Dust sulphurousAcid = Dust.NewDustPerfect(spawnPosition + vr * 16f, DustID.JungleTorch);
                sulphurousAcid.velocity = vr * 4f;
                sulphurousAcid.noGravity = true;
                sulphurousAcid.scale = 1.2f * salce;
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            SoundStyle sound = SoundID.Item88;
            sound.Volume = 3;
            SoundEngine.PlaySound(sound, Projectile.position);
            SpanDust(Projectile, 2);
            CWRNpc cwrNPC = target.CWR();
            cwrNPC.PhosphorescentGauntletHitCount++;
            int type = ModContent.ProjectileType<EXGauntlet>();
            if (cwrNPC.PhosphorescentGauntletHitCount > 6 && Main.player[Projectile.owner].ownedProjectileCounts[type] <= 0) {
                Vector2 randomRotVr = Main.rand.NextFloat(MathHelper.TwoPi).ToRotationVector2();
                Projectile.NewProjectile(Projectile.FromObjectGetParent(), target.Center + randomRotVr * 1600
                    , randomRotVr.RotatedBy(MathHelper.Pi) * 15, type, Projectile.damage * 2, 0, Projectile.owner, target.Center.X, target.Center.Y);
                Projectile.NewProjectile(Projectile.FromObjectGetParent(), target.Center + randomRotVr * -1600
                    , randomRotVr * 15, type, Projectile.damage * 2, 0, Projectile.owner, target.Center.X, target.Center.Y);
                cwrNPC.PhosphorescentGauntletHitCount = 0;
            }
            else {
                float random = Main.rand.NextFloat(MathHelper.TwoPi);
                for (int i = 0; i < 6; i++) {
                    float rot = MathHelper.TwoPi / 6 * i + random;
                    Projectile.NewProjectile(Projectile.FromObjectGetParent(), Projectile.Center, rot.ToRotationVector2() * 15
                        , ModContent.ProjectileType<GauntletDerive>(), Projectile.damage / 2, 2, Projectile.owner, ai2: target.whoAmI);
                }
            }
        }

        public override bool OnTileCollide(Vector2 oldVelocity) => Projectile.timeLeft <= 60 && base.OnTileCollide(oldVelocity);

        public override bool PreDraw(ref Color lightColor) {
            Texture2D value = TextureAssets.Projectile[Type].Value;
            float drawRot = Projectile.rotation + MathHelper.PiOver4 + MathHelper.Pi + (Projectile.velocity.X > 0 ? MathHelper.PiOver2 : 0);
            Main.EntitySpriteDraw(value, Projectile.Center - Main.screenPosition, null, Color.White, drawRot
                , value.Size() / 2, Projectile.scale, Projectile.velocity.X > 0 ? SpriteEffects.FlipHorizontally : SpriteEffects.None, 0);
            return false;
        }
    }
}
