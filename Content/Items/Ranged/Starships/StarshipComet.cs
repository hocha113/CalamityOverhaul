using CalamityOverhaul.Content.Buffs;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Ranged.Starships
{
    //特殊弹药：彗星束，命中沿途释放若干悬浮光点
    internal class StarshipComet : ModProjectile
    {
        public override string Texture => CWRConstant.Placeholder2;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 20;
            ProjectileID.Sets.TrailingMode[Type] = 2;
            ProjectileID.Sets.DrawScreenCheckFluff[Type] = 800;
        }

        public override void SetDefaults() {
            Projectile.width = 40;
            Projectile.height = 40;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 240;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.extraUpdates = 2;
            Projectile.usesIDStaticNPCImmunity = true;
            Projectile.idStaticNPCHitCooldown = 20;
        }

        public override void AI() {
            Projectile.rotation = Projectile.velocity.ToRotation();
            Projectile.localAI[0]++;

            Lighting.AddLight(Projectile.Center, 1.0f, 0.65f, 0.35f);

            //沿途投放悬浮光点
            if (Projectile.IsOwnedByLocalPlayer() && Projectile.localAI[0] % 10 == 0) {
                Vector2 dropPos = Projectile.Center + Main.rand.NextVector2Circular(40f, 40f);
                Projectile.NewProjectile(Projectile.FromObjectGetParent(), dropPos, Main.rand.NextVector2Circular(1.5f, 1.5f)
                    , ModContent.ProjectileType<StarshipCometOrb>()
                    , Projectile.damage / 3, Projectile.knockBack * 0.5f, Projectile.owner);
            }

            //火花特效
            for (int i = 0; i < 2; i++) {
                Vector2 sparkVel = -Projectile.velocity.SafeNormalize(Vector2.UnitX) * Main.rand.NextFloat(1f, 4f)
                    + Main.rand.NextVector2Circular(3f, 3f);
                Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.GoldFlame, sparkVel, 80, default, 1.5f);
                d.noGravity = true;
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(ModContent.BuffType<HyperDisintegration>(), 600);
        }

        public override bool PreDraw(ref Color lightColor) {
            SpriteBatch sb = Main.spriteBatch;
            Texture2D glow = CWRAsset.SoftGlow.Value;
            Texture2D star = CWRAsset.StarTexture.Value;
            Texture2D starWhite = CWRAsset.StarTexture_White.Value;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            float time = Main.GlobalTimeWrappedHourly;
            float pulse = 0.9f + (float)Math.Sin(time * 10f) * 0.15f;

            //长尾迹
            for (int i = Projectile.oldPos.Length - 1; i >= 0; i--) {
                if (Projectile.oldPos[i] == Vector2.Zero) {
                    continue;
                }
                float f = (Projectile.oldPos.Length - i) / (float)Projectile.oldPos.Length;
                Vector2 p = Projectile.oldPos[i] + Projectile.Size * 0.5f - Main.screenPosition;
                Color outer = new Color(255, 120, 60, 0) * f * 0.6f;
                Color inner = new Color(255, 220, 140, 0) * f * 0.35f;
                Vector2 scl = new Vector2(1.0f + f * 2.2f, 0.7f + f * 0.3f);
                sb.Draw(glow, p, null, outer, Projectile.rotation, glow.Size() * 0.5f, scl, SpriteEffects.None, 0);
                sb.Draw(glow, p, null, inner, Projectile.rotation, glow.Size() * 0.5f, scl * 0.6f, SpriteEffects.None, 0);
            }

            //彗核
            sb.Draw(glow, drawPos, null, new Color(255, 150, 80, 0) * 0.85f * pulse, 0f, glow.Size() * 0.5f, 1.6f, SpriteEffects.None, 0);
            sb.Draw(glow, drawPos, null, new Color(255, 230, 180, 0) * 0.95f, 0f, glow.Size() * 0.5f, 1.0f, SpriteEffects.None, 0);
            sb.Draw(glow, drawPos, null, Color.White with { A = 0 } * 0.9f, 0f, glow.Size() * 0.5f, 0.55f, SpriteEffects.None, 0);

            if (star != null) {
                sb.Draw(star, drawPos, null, new Color(255, 180, 90, 0) * 0.9f
                    , time * 4f, star.Size() * 0.5f, 1.1f * pulse, SpriteEffects.None, 0);
            }
            if (starWhite != null) {
                sb.Draw(starWhite, drawPos, null, Color.White with { A = 0 } * 0.85f
                    , -time * 6f, starWhite.Size() * 0.5f, 0.48f * pulse, SpriteEffects.None, 0);
            }
            return false;
        }
    }

    //悬浮光点，短暂延迟后爆炸
    internal class StarshipCometOrb : ModProjectile
    {
        public override string Texture => CWRConstant.Placeholder2;

        public override void SetDefaults() {
            Projectile.width = 22;
            Projectile.height = 22;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 90;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesIDStaticNPCImmunity = true;
            Projectile.idStaticNPCHitCooldown = 15;
        }

        public override void AI() {
            Projectile.velocity *= 0.92f;
            Projectile.localAI[0]++;

            Lighting.AddLight(Projectile.Center, 0.7f, 0.5f, 0.3f);

            if (Projectile.timeLeft == 20) {
                SoundEngine.PlaySound(SoundID.Item14 with { Volume = 0.35f, Pitch = 0.4f, MaxInstances = 6 }, Projectile.Center);
            }
        }

        public override bool? CanDamage() => Projectile.timeLeft < 18;

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(ModContent.BuffType<HyperDisintegration>(), 240);
        }

        public override void OnKill(int timeLeft) {
            for (int i = 0; i < 20; i++) {
                Vector2 v = Main.rand.NextVector2CircularEdge(5f, 5f);
                Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.GoldFlame, v, 60, default, 1.6f);
                d.noGravity = true;
            }
            for (int i = 0; i < 12; i++) {
                Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.YellowStarDust
                    , Main.rand.NextVector2Circular(3f, 3f), 40, default, 1.4f);
                d.noGravity = true;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            SpriteBatch sb = Main.spriteBatch;
            Texture2D glow = CWRAsset.SoftGlow.Value;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            float life = Projectile.timeLeft / 90f;
            float pulse = 0.7f + (float)Math.Sin(Main.GlobalTimeWrappedHourly * 14f + Projectile.whoAmI * 0.5f) * 0.3f;

            //即将爆炸时闪烁变红
            Color baseCol = life < 0.25f
                ? Color.Lerp(new Color(255, 220, 120, 0), new Color(255, 80, 40, 0), 1f - life / 0.25f)
                : new Color(255, 220, 120, 0);

            sb.Draw(glow, drawPos, null, baseCol * pulse * 0.85f, 0f, glow.Size() * 0.5f, 1.1f, SpriteEffects.None, 0);
            sb.Draw(glow, drawPos, null, Color.White with { A = 0 } * pulse * 0.65f, 0f, glow.Size() * 0.5f, 0.55f, SpriteEffects.None, 0);
            return false;
        }
    }
}
