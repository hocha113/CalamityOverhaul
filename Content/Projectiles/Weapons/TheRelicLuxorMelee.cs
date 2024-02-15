using CalamityOverhaul.Common;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons
{
    internal class TheRelicLuxorMelee : ModProjectile
    {
        public override string Texture => CWRConstant.Projectile + "TheRelicLuxorMeleeProj";

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailingMode[Type] = 2;
            ProjectileID.Sets.TrailCacheLength[Type] = 8;
        }

        public override void SetDefaults() {
            Projectile.width = 20;
            Projectile.height = 20;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1;
            Projectile.alpha = 0;
            Projectile.timeLeft = 300;
            Projectile.tileCollide = false;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
        }

        public int Status { get => (int)Projectile.ai[0]; set => Projectile.ai[0] = value; }
        public int OwnerIndex { get => (int)Projectile.ai[1]; set => Projectile.ai[1] = value; }

        public override void AI() {
            if (Status == 0) {
                Projectile ownerProj = CWRUtils.GetProjectileInstance(OwnerIndex);
                if (ownerProj != null && Projectile.timeLeft >= 270) {
                    Projectile.velocity = Projectile.Center.To(ownerProj.Center);
                }
                else {
                    if (Projectile.timeLeft < 120)
                        Projectile.velocity *= 0.99f;
                    else if (Projectile.velocity.LengthSquared() < 25 * 25)
                        Projectile.velocity *= 1.02f;
                }
            }
            if (Status == 1) {

            }

            Projectile.rotation += 0.35f;
            if (Status == 1)
                Projectile.rotation += 0.15f;
            Lighting.AddLight(Projectile.Center, Color.Red.ToVector3());

            if (Projectile.timeLeft >= 270) {
                Projectile.alpha += 10;
            }
            if (Projectile.timeLeft <= 30) {
                Projectile.alpha -= 10;
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            for (int i = 0; i < 12; i++) {
                Vector2 vr = Vector2.UnitX * (0f - Projectile.width) / 2f;
                vr += -Vector2.UnitY.RotatedBy(i * CWRUtils.PiOver6) * new Vector2(8f, 16f);
                vr = vr.RotatedBy(Projectile.rotation - MathHelper.PiOver2);
                int dust = Dust.NewDust(Projectile.Center, 0, 0, DustID.LifeDrain, 0f, 0f, 160);
                Main.dust[dust].scale = 1.1f;
                Main.dust[dust].noGravity = true;
                Main.dust[dust].position = Projectile.Center + vr;
                Main.dust[dust].velocity = Projectile.velocity * 0.1f;
                Main.dust[dust].velocity =
                    Vector2.Normalize(Projectile.Center - Projectile.velocity * 3f - Main.dust[dust].position) * 1.25f;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D texture = CWRUtils.GetT2DValue(Texture);
            Color color = Color.White;
            float alp = Projectile.alpha / 255f;
            float slp = (1 + MathF.Sin(MathHelper.ToRadians(Main.GlobalTimeWrappedHourly)) * 0.2f) * Projectile.scale;

            Main.EntitySpriteDraw(
                texture,
                Projectile.Center - Main.screenPosition,
                null,
                color * alp,
                Projectile.rotation,
                CWRUtils.GetOrig(texture),
                slp,
                SpriteEffects.None,
                0
                );
            //for (int i = 0; i < Projectile.oldPos.Length; i++)
            //{
            //    Main.EntitySpriteDraw(
            //        texture,
            //        Projectile.oldPos[i] - Main.screenPosition
            //        + Projectile.position.To(Projectile.Center),
            //        null,
            //        Color.White * (alp - i * 0.1f) * 0.25f,
            //        Projectile.rotation,
            //        DrawUtils.GetOrig(texture),
            //        slp - i * 0.1f,
            //        SpriteEffects.None,
            //        0
            //    );
            //}
            return false;
        }

        public override void DrawBehind(int index, List<int> behindNPCsAndTiles
            , List<int> behindNPCs, List<int> behindProjectiles, List<int> overPlayers, List<int> overWiresUI) {
            overPlayers.Add(index);
        }
    }
}
