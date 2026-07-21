using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Magic.Pandemoniums
{
    /// <summary>深渊血色镰刀</summary>
    internal class PandemoniumScythe : ModProjectile
    {
        public override string Texture => CWRConstant.Projectile_Melee + "BalefulSickle";
        [VaultLoaden(CWRConstant.Projectile_Melee + "BalefulSickle")]
        private static Asset<Texture2D> ScytheTex = null;

        private NPC target;
        private float searchCooldown = 0;
        private ref float Wave => ref Projectile.ai[0];
        private ref float SpiralPhase => ref Projectile.ai[1];
        private ref float TrailTimer => ref Projectile.localAI[0];
        private ref float HomingMode => ref Projectile.localAI[1]; //0=普通 1=弱追踪 2=强追踪

        private Vector2[] oldPositions = new Vector2[20];
        private float[] oldRotations = new float[20];

        private float spiralRadius = 0;
        private float spiralSpeed = 0.15f;

        public override void SetDefaults() {
            Projectile.width = 48;
            Projectile.height = 48;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Magic;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = 4;
            Projectile.timeLeft = 360;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 10;
            Projectile.extraUpdates = 1;
        }

        public override void AI() {
            if (TrailTimer == 0) {
                for (int i = 0; i < oldPositions.Length; i++) {
                    oldPositions[i] = Projectile.Center;
                }
                spiralRadius = 50f + Wave * 30f;
            }
            TrailTimer++;

            for (int i = oldPositions.Length - 1; i > 0; i--) {
                oldPositions[i] = oldPositions[i - 1];
                oldRotations[i] = oldRotations[i - 1];
            }
            oldPositions[0] = Projectile.Center;
            oldRotations[0] = Projectile.rotation;

            float spiralAmount = (float)Math.Sin(TrailTimer * spiralSpeed + SpiralPhase) * spiralRadius;
            Vector2 spiralOffset = Projectile.velocity.RotatedBy(MathHelper.PiOver2).SafeNormalize(Vector2.Zero) * spiralAmount;

            Projectile.velocity = Projectile.velocity.RotatedBy(spiralAmount * 0.001f);

            Projectile.rotation += 0.5f * Math.Sign(Projectile.velocity.X != 0 ? Projectile.velocity.X : 1);

            if (HomingMode >= 1) {
                if (searchCooldown <= 0) {
                    target = FindTarget();
                    searchCooldown = 12;
                }
                else {
                    searchCooldown--;
                }

                if (target != null && target.active && !target.dontTakeDamage) {
                    Vector2 direction = (target.Center - Projectile.Center).SafeNormalize(Vector2.Zero);

                    float homingStrength = HomingMode == 2 ? 0.15f : 0.08f;
                    float targetSpeed = HomingMode == 2 ? 22f : 18f;
                    targetSpeed += Wave * 2f;

                    Projectile.velocity = Vector2.Lerp(Projectile.velocity, direction * targetSpeed, homingStrength);
                }
                else {
                    Projectile.velocity *= 0.98f;
                }
            }
            else {
                Projectile.velocity *= 0.99f;
            }

            if (Main.rand.NextBool(2)) {
                Color dustColor = HomingMode == 2 ? Color.OrangeRed : Color.Red;
                Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.Blood, Projectile.velocity * -0.15f, 100, dustColor, 1.5f);
                d.noGravity = true;
                d.fadeIn = 1.2f;
            }

            if (Main.rand.NextBool(4)) {
                Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.Shadowflame, Main.rand.NextVector2Circular(2f, 2f), 100, Color.DarkRed, 0.8f);
                d.noGravity = true;
            }

            if (HomingMode == 2 && Main.rand.NextBool(6)) {
                Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.Torch, Main.rand.NextVector2Circular(1f, 1f), 100, Color.Orange, 1f);
                d.noGravity = true;
            }

            Lighting.AddLight(Projectile.Center, 1.0f + HomingMode * 0.5f, 0.2f, 0.3f);
        }

        private NPC FindTarget() {
            NPC closest = null;
            float maxDist = HomingMode == 2 ? 1200f : 900f;//强追踪搜更远

            foreach (NPC npc in Main.npc) {
                if (npc.CanBeChasedBy(this)) {
                    float dist = Projectile.Distance(npc.Center);
                    if (dist < maxDist) {
                        maxDist = dist;
                        closest = npc;
                    }
                }
            }
            return closest;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(BuffID.Ichor, 240);
            target.AddBuff(BuffID.ShadowFlame, 180);

            SoundEngine.PlaySound(SoundID.Item71 with { Volume = 0.6f, Pitch = 0.3f }, Projectile.position);

            int burstCount = HomingMode == 2 ? 18 : 12;
            for (int i = 0; i < burstCount; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(6f, 6f);
                Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.Blood, vel, 100, default, 1.8f);
                d.noGravity = true;
            }

            //强追踪命中再炸一圈小镰
            if (HomingMode == 2 && Projectile.owner == Main.myPlayer && Main.player[Projectile.owner].ownedProjectileCounts[Type] < 220 && Projectile.ai[2] < 4) {
                for (int i = 0; i < 6; i++) {
                    float angle = MathHelper.TwoPi * i / 6f;
                    Vector2 shockVel = angle.ToRotationVector2() * 8f;
                    Projectile.NewProjectile(
                        Projectile.GetSource_FromThis(),
                        Projectile.Center,
                        shockVel,
                        ModContent.ProjectileType<PandemoniumScythe>(),
                        (int)(Projectile.damage * 0.4f),
                        Projectile.knockBack * 0.5f,
                        Projectile.owner,
                        Wave,
                        0,
                        999
                    );
                }
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            SpriteBatch sb = Main.spriteBatch;
            Texture2D texture = ScytheTex.Value;

            Color trail1 = HomingMode == 2 ? new Color(255, 140, 60, 0) : new Color(180, 20, 40, 0);
            Color trail2 = HomingMode == 2 ? new Color(255, 180, 80, 0) : new Color(255, 80, 60, 0);

            for (int i = 1; i < oldPositions.Length; i++) {
                float progress = 1f - i / (float)oldPositions.Length;
                Color trailColor = Color.Lerp(trail1, trail2, progress) * progress * 0.8f;
                float trailScale = Projectile.scale * progress * 1.2f;

                sb.Draw(texture, oldPositions[i] - Main.screenPosition, null, trailColor, oldRotations[i],
                    texture.Size() / 2, trailScale, SpriteEffects.None, 0f);
            }

            Color mainColor = Projectile.GetAlpha(lightColor);
            sb.Draw(texture, Projectile.Center - Main.screenPosition, null, mainColor, Projectile.rotation,
                texture.Size() / 2, Projectile.scale, SpriteEffects.None, 0f);

            float glowIntensity = HomingMode == 2 ? 1.0f : 0.8f;
            Color glowColor = HomingMode == 2 ?
                new Color(255, 140, 80, 0) * glowIntensity :
                new Color(255, 100, 80, 0) * glowIntensity;

            sb.Draw(texture, Projectile.Center - Main.screenPosition, null, glowColor, Projectile.rotation,
                texture.Size() / 2, Projectile.scale * 1.1f, SpriteEffects.None, 0f);

            return false;
        }
    }
}
