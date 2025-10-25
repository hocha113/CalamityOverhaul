using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Magic.Pandemoniums
{
    ///<summary>
    ///深渊血色镰刀
    ///</summary>
    internal class PandemoniumScythe : ModProjectile
    {
        public override string Texture => CWRConstant.Projectile_Melee + "BalefulSickle";

        private NPC target;
        private float searchCooldown = 0;
        private ref float Wave => ref Projectile.ai[0];
        private ref float TrailTimer => ref Projectile.ai[1];

        //拖尾效果
        private Vector2[] oldPositions = new Vector2[20];
        private float[] oldRotations = new float[20];

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
            Projectile.localNPCHitCooldown = 12;
            Projectile.extraUpdates = 1;
        }

        public override void AI() {
            //初始化拖尾数组
            if (TrailTimer == 0) {
                for (int i = 0; i < oldPositions.Length; i++) {
                    oldPositions[i] = Projectile.Center;
                }
            }
            TrailTimer++;

            //更新拖尾位置
            for (int i = oldPositions.Length - 1; i > 0; i--) {
                oldPositions[i] = oldPositions[i - 1];
                oldRotations[i] = oldRotations[i - 1];
            }
            oldPositions[0] = Projectile.Center;
            oldRotations[0] = Projectile.rotation;

            //螺旋运动
            float spiralAmount = (float)Math.Sin(TrailTimer * 0.1f) * 2f;
            Projectile.velocity = Projectile.velocity.RotatedBy(spiralAmount * 0.02f);

            Projectile.rotation += 0.5f * Math.Sign(Projectile.velocity.X != 0 ? Projectile.velocity.X : 1);

            //寻找目标
            if (searchCooldown <= 0) {
                target = FindTarget();
                searchCooldown = 12;
            }
            else {
                searchCooldown--;
            }

            //追踪目标（第二波更强）
            if (target != null && target.active && !target.dontTakeDamage) {
                Vector2 direction = (target.Center - Projectile.Center).SafeNormalize(Vector2.Zero);
                float homingStrength = Wave == 1 ? 0.08f : 0.12f;
                float targetSpeed = Wave == 1 ? 18f : 22f;
                Projectile.velocity = Vector2.Lerp(Projectile.velocity, direction * targetSpeed, homingStrength);
            }
            else {
                Projectile.velocity *= 0.99f;
            }

            //增强粒子效果
            if (Main.rand.NextBool(2)) {
                Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.Blood, Projectile.velocity * -0.15f, 100, default, 1.5f);
                d.noGravity = true;
                d.fadeIn = 1.2f;
            }

            if (Main.rand.NextBool(4)) {
                Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.Shadowflame, Main.rand.NextVector2Circular(2f, 2f), 100, Color.DarkRed, 0.8f);
                d.noGravity = true;
            }

            Lighting.AddLight(Projectile.Center, 1.0f, 0.2f, 0.3f);
        }

        private NPC FindTarget() {
            NPC closest = null;
            float maxDist = 900f;
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

            //命中爆发效果
            for (int i = 0; i < 12; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(6f, 6f);
                Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.Blood, vel, 100, default, 1.8f);
                d.noGravity = true;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            SpriteBatch sb = Main.spriteBatch;
            Texture2D texture = ModContent.Request<Texture2D>(Texture).Value;

            //绘制拖尾
            for (int i = 1; i < oldPositions.Length; i++) {
                float progress = 1f - i / (float)oldPositions.Length;
                Color trailColor = Color.Lerp(new Color(180, 20, 40, 0), new Color(255, 80, 60, 0), progress) * progress * 0.8f;
                float trailScale = Projectile.scale * progress * 1.2f;

                sb.Draw(texture, oldPositions[i] - Main.screenPosition, null, trailColor, oldRotations[i],
                    texture.Size() / 2, trailScale, SpriteEffects.None, 0f);
            }

            //绘制主体
            Color mainColor = Projectile.GetAlpha(lightColor);
            sb.Draw(texture, Projectile.Center - Main.screenPosition, null, mainColor, Projectile.rotation,
                texture.Size() / 2, Projectile.scale, SpriteEffects.None, 0f);

            //绘制辉光
            Color glowColor = new Color(255, 100, 80, 0) * 0.8f;
            sb.Draw(texture, Projectile.Center - Main.screenPosition, null, glowColor, Projectile.rotation,
                texture.Size() / 2, Projectile.scale * 1.1f, SpriteEffects.None, 0f);

            return false;
        }
    }
}
