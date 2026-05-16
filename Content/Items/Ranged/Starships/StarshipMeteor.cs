using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Ranged.Starships
{
    //终幕陨石雨：极强追踪，无视目标防御与伤害减免
    internal class StarshipMeteor : ModProjectile
    {
        public override string Texture => CWRConstant.Placeholder;

        //ai[0]：启动延迟帧；ai[1]：陨石雨总数（用于触发星系）
        private ref float StartDelay => ref Projectile.ai[0];
        private ref float TotalCount => ref Projectile.ai[1];

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 18;
            ProjectileID.Sets.TrailingMode[Type] = 2;
            ProjectileID.Sets.DrawScreenCheckFluff[Type] = 2000;
        }

        public override void SetDefaults() {
            Projectile.width = 40;
            Projectile.height = 40;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 420;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.ArmorPenetration = 10000;
            Projectile.extraUpdates = 1;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
        }

        public override void AI() {
            if (StartDelay > 0) {
                StartDelay--;
                Projectile.velocity = Vector2.Zero;
                Projectile.alpha = 255;
                return;
            }

            if (Projectile.alpha > 0) {
                Projectile.alpha = Math.Max(0, Projectile.alpha - 30);
                if (Projectile.alpha == 225) {
                    SoundEngine.PlaySound(SoundID.Item89 with { Volume = 0.35f, Pitch = 0.2f, MaxInstances = 5 }, Projectile.Center);
                }
            }

            NPC target = FindNearestEnemy(2400f);
            if (target != null) {
                Vector2 desired = (target.Center - Projectile.Center).SafeNormalize(Vector2.UnitY) * 22f;
                Projectile.velocity = Vector2.Lerp(Projectile.velocity, desired, 0.12f);
                Projectile.velocity = Projectile.velocity.SafeNormalize(Vector2.UnitY) * MathHelper.Clamp(Projectile.velocity.Length() + 0.6f, 8f, 32f);
            }
            else {
                Projectile.velocity.Y = Math.Min(Projectile.velocity.Y + 0.3f, 30f);
            }

            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;
            Lighting.AddLight(Projectile.Center, 1f, 0.65f, 0.35f);

            for (int i = 0; i < 2; i++) {
                Vector2 v = -Projectile.velocity.SafeNormalize(Vector2.UnitY) * Main.rand.NextFloat(2f, 6f)
                    + Main.rand.NextVector2Circular(3f, 3f);
                Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.GoldFlame, v, 60, default, 1.6f);
                d.noGravity = true;
            }
        }

        private NPC FindNearestEnemy(float range) {
            NPC best = null;
            float minDist = range * range;
            foreach (NPC n in Main.npc) {
                if (!n.CanBeChasedBy() || Vector2.DistanceSquared(n.Center, Projectile.Center) > minDist) {
                    continue;
                }
                minDist = Vector2.DistanceSquared(n.Center, Projectile.Center);
                best = n;
            }
            return best;
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {
            //无视防御与伤害减免
            modifiers.DefenseEffectiveness *= 0f;
            modifiers.FinalDamage.Flat += 0;
            modifiers.FinalDamage *= 1f;
            modifiers.DamageVariationScale *= 0f;
            //彻底忽略所有伤害减免：使用 SetMaxDamage 让它基本不受削弱
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            SoundEngine.PlaySound(SoundID.Item14 with { Volume = 0.8f, Pitch = -0.2f, MaxInstances = 6 }, Projectile.Center);
            CreateImpactFX();
        }

        public override void OnKill(int timeLeft) {
            CreateImpactFX();
        }

        private void CreateImpactFX() {
            for (int i = 0; i < 30; i++) {
                Vector2 v = Main.rand.NextVector2CircularEdge(8f, 8f);
                Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.GoldFlame, v, 30, default, 1.8f);
                d.noGravity = true;
            }
            for (int i = 0; i < 14; i++) {
                Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.Torch, Main.rand.NextVector2Circular(5f, 5f), 40, new Color(255, 200, 100), 1.5f);
                d.noGravity = true;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            if (Projectile.alpha >= 255) {
                return false;
            }

            SpriteBatch sb = Main.spriteBatch;
            Texture2D glow = CWRAsset.SoftGlow.Value;
            Texture2D star = CWRAsset.StarTexture.Value;
            Texture2D starWhite = CWRAsset.StarTexture_White.Value;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            float time = Main.GlobalTimeWrappedHourly;
            float pulse = 0.85f + (float)Math.Sin(time * 12f) * 0.15f;
            float fade = 1f - Projectile.alpha / 255f;

            //尾迹
            for (int i = Projectile.oldPos.Length - 1; i >= 0; i--) {
                if (Projectile.oldPos[i] == Vector2.Zero) {
                    continue;
                }
                float f = (Projectile.oldPos.Length - i) / (float)Projectile.oldPos.Length;
                Vector2 p = Projectile.oldPos[i] + Projectile.Size * 0.5f - Main.screenPosition;
                Color outer = new Color(255, 130, 70, 0) * f * 0.55f * fade;
                Color inner = new Color(255, 220, 150, 0) * f * 0.35f * fade;
                Vector2 scl = new Vector2(0.4f + f * 0.6f, 2.0f + f * 4.2f);
                sb.Draw(glow, p, null, outer, Projectile.rotation, glow.Size() * 0.5f, scl, SpriteEffects.None, 0);
                sb.Draw(glow, p, null, inner, Projectile.rotation, glow.Size() * 0.5f, scl * new Vector2(0.55f, 0.75f), SpriteEffects.None, 0);
            }

            //火头
            sb.Draw(glow, drawPos, null, new Color(255, 140, 70, 0) * 0.9f * fade * pulse, 0f, glow.Size() * 0.5f, 1.9f, SpriteEffects.None, 0);
            sb.Draw(glow, drawPos, null, new Color(255, 230, 170, 0) * 0.95f * fade, 0f, glow.Size() * 0.5f, 1.1f, SpriteEffects.None, 0);
            sb.Draw(glow, drawPos, null, Color.White with { A = 0 } * 0.85f * fade, 0f, glow.Size() * 0.5f, 0.55f, SpriteEffects.None, 0);

            if (star != null) {
                sb.Draw(star, drawPos, null, new Color(255, 200, 120, 0) * 0.85f * fade
                    , Projectile.rotation + time * 3f, star.Size() * 0.5f, 1.0f * pulse, SpriteEffects.None, 0);
            }
            if (starWhite != null) {
                sb.Draw(starWhite, drawPos, null, Color.White with { A = 0 } * 0.85f * fade
                    , -time * 5f, starWhite.Size() * 0.5f, 0.45f * pulse, SpriteEffects.None, 0);
            }
            return false;
        }
    }
}
