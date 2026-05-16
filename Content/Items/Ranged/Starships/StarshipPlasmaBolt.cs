using CalamityOverhaul.Content.Buffs;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Ranged.Starships
{
    //群星巨舰主弹：高速等离子星弹，命中赋予超位崩解
    internal class StarshipPlasmaBolt : ModProjectile
    {
        public override string Texture => CWRConstant.Placeholder2;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 12;
            ProjectileID.Sets.TrailingMode[Type] = 2;
            ProjectileID.Sets.DrawScreenCheckFluff[Type] = 400;
        }

        public override void SetDefaults() {
            Projectile.width = 16;
            Projectile.height = 16;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.penetrate = 2;
            Projectile.timeLeft = 240;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
            Projectile.extraUpdates = 2;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 8;
        }

        public override void AI() {
            Projectile.rotation = Projectile.velocity.ToRotation();
            Projectile.localAI[0]++;

            Lighting.AddLight(Projectile.Center, 0.35f, 0.28f, 0.7f);

            if (Projectile.localAI[0] % 2 == 0 && Main.rand.NextBool(2)) {
                Vector2 sparkVel = -Projectile.velocity.SafeNormalize(Vector2.UnitX) * Main.rand.NextFloat(0.6f, 2.0f)
                    + Main.rand.NextVector2Circular(1.2f, 1.2f);
                Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.PinkStarfish, sparkVel, 120, default, 1.0f);
                d.noGravity = true;
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(ModContent.BuffType<HyperDisintegration>(), 240);
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            target.AddBuff(ModContent.BuffType<HyperDisintegration>(), 180);
        }

        public override bool PreDraw(ref Color lightColor) {
            SpriteBatch sb = Main.spriteBatch;
            Texture2D glow = CWRAsset.SoftGlow.Value;
            Texture2D star = CWRAsset.StarTexture.Value;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            float time = Main.GlobalTimeWrappedHourly;

            //尾迹
            for (int i = Projectile.oldPos.Length - 1; i >= 0; i--) {
                if (Projectile.oldPos[i] == Vector2.Zero) {
                    continue;
                }
                float f = (Projectile.oldPos.Length - i) / (float)Projectile.oldPos.Length;
                Vector2 p = Projectile.oldPos[i] + Projectile.Size * 0.5f - Main.screenPosition;
                Color trail = new Color(130, 90, 255, 0) * f * 0.55f;
                Vector2 scl = new Vector2(0.8f + f * 1.6f, 0.5f + f * 0.3f);
                sb.Draw(glow, p, null, trail, Projectile.rotation, glow.Size() * 0.5f, scl, SpriteEffects.None, 0);
            }

            //核心光团
            sb.Draw(glow, drawPos, null, new Color(160, 120, 255, 0) * 0.85f, 0f, glow.Size() * 0.5f, 0.8f, SpriteEffects.None, 0);
            sb.Draw(glow, drawPos, null, new Color(230, 220, 255, 0) * 0.9f, 0f, glow.Size() * 0.5f, 0.45f, SpriteEffects.None, 0);

            //星芒
            if (star != null) {
                sb.Draw(star, drawPos, null, new Color(180, 150, 255, 0) * 0.85f
                    , time * 6f, star.Size() * 0.5f, 0.55f, SpriteEffects.None, 0);
                sb.Draw(star, drawPos, null, Color.White with { A = 0 } * 0.6f
                    , -time * 4f, star.Size() * 0.5f, 0.28f, SpriteEffects.None, 0);
            }
            return false;
        }
    }
}
