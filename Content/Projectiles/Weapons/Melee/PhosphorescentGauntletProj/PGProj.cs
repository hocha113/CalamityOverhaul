using CalamityMod.DataStructures;
using CalamityMod.Dusts;
using CalamityOverhaul.Content.Particles.Core;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Melee.PhosphorescentGauntletProj
{
    internal class PGProj : ModProjectile
    {
        public override string Texture => CWRConstant.Cay_Wap_Melee + "PhosphorescentGauntlet";

        private int Time;

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
            Projectile.MaxUpdates = 2;
            Projectile.timeLeft = 120 * Projectile.MaxUpdates;
        }

        public override void AI() {
            Lighting.AddLight(Projectile.Center, Color.Gold.ToVector3() * 3);
            Projectile.rotation = Projectile.velocity.ToRotation();
            if (Time == 0) {
                SpanDust(Projectile, 1);
            }
            if (!CWRUtils.isServer) {
                Vector2 pos = Projectile.Center + Main.rand.NextVector2Unit() * Main.rand.Next(3, Projectile.width);

                PGGlow glow = new PGGlow(pos, Projectile.velocity, Projectile.scale * Main.rand.NextFloat(1, 1.2f), Main.rand.Next(15, 22), 1);
                DRKLoader.AddParticle(glow);
                SpanDust(pos);
            }

            Time++;
        }

        public void SpanDust(Vector2 origPos) {
            for (int i = 0; i < 12; i++) {
                Vector2 offset = Vector2.UnitX * (0f - Projectile.width) / 2f;
                offset += -Vector2.UnitY.RotatedBy(i * CWRUtils.PiOver6) * new Vector2(8f, 16f);
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
            if (!Main.dedServ) {
                Vector2 vr = projectile.velocity.UnitVector();
                Vector2 topLeft = projectile.Center + vr.RotatedBy(-MathHelper.PiOver2) * 40f * salce;
                Vector2 top = projectile.Center + vr * 70f * salce;
                Vector2 topRight = projectile.Center + vr.RotatedBy(MathHelper.PiOver2) * 40f * salce;
                foreach (Vector2 spawnPosition in new BezierCurve(topLeft, top, topRight).GetPoints(50)) {
                    Dust sulphurousAcid = Dust.NewDustPerfect(spawnPosition + vr * 16f, (int)CalamityDusts.Nightwither);
                    sulphurousAcid.velocity = vr * 4f;
                    sulphurousAcid.noGravity = true;
                    sulphurousAcid.scale = 1.2f * salce;
                }
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            SoundStyle sound = SoundID.Item88 with {
                Volume = 3
            };
            SoundEngine.PlaySound(sound, Projectile.position);
            SpanDust(Projectile, 2);
            CWRNpc cwrNPC = target.CWR();
            cwrNPC.PhosphorescentGauntletOnHitNum++;
            if (cwrNPC.PhosphorescentGauntletOnHitNum > 6) {
                Vector2 randomRotVr = Main.rand.NextFloat(MathHelper.TwoPi).ToRotationVector2();
                Projectile.NewProjectile(Projectile.parent(), target.Center + randomRotVr * 1600
                    , randomRotVr.RotatedBy(MathHelper.Pi) * 15, ModContent.ProjectileType<SupPGProj>()
                    , Projectile.damage * 2, 0, Projectile.owner, target.Center.X, target.Center.Y);
                Projectile.NewProjectile(Projectile.parent(), target.Center + randomRotVr * -1600
                    , randomRotVr * 15, ModContent.ProjectileType<SupPGProj>()
                    , Projectile.damage * 2, 0, Projectile.owner, target.Center.X, target.Center.Y);
                cwrNPC.PhosphorescentGauntletOnHitNum = 0;
            }
        }

        public override void OnKill(int timeLeft) {
            if (Projectile.IsOwnedByLocalPlayer()) {
                float random = Main.rand.NextFloat(MathHelper.TwoPi);
                for (int i = 0; i < 6; i++) {
                    float rot = MathHelper.TwoPi / 6 * i + random;
                    Projectile.NewProjectile(Projectile.parent(), Projectile.Center, rot.ToRotationVector2() * 15, ModContent.ProjectileType<PGDeriveProj>(), Projectile.damage / 2, 2, Projectile.owner);
                }
            }
        }

        public override bool OnTileCollide(Vector2 oldVelocity) {
            return Projectile.timeLeft > 60 ? false : base.OnTileCollide(oldVelocity);
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D value = TextureAssets.Projectile[Type].Value;
            Main.EntitySpriteDraw(value, Projectile.Center - Main.screenPosition, null, Color.White, Projectile.rotation + MathHelper.PiOver4 + MathHelper.Pi + (Projectile.velocity.X > 0 ? MathHelper.PiOver2 : 0)
                , value.Size() / 2, Projectile.scale, Projectile.velocity.X > 0 ? SpriteEffects.FlipHorizontally : SpriteEffects.None, 0);
            return false;
        }
    }
}
