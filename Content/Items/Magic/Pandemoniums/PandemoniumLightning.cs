using CalamityMod.Dusts;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Magic.Pandemoniums
{
    /// <summary>
    /// 闪电链，会在敌人之间跳跃的闪电球
    /// </summary>
    internal class PandemoniumLightning : ModProjectile
    {
        public override string Texture => CWRConstant.Placeholder;

        private ref float ChainCount => ref Projectile.ai[0];
        private ref float TierLevel => ref Projectile.ai[1];
        private ref float SearchCooldown => ref Projectile.localAI[1];

        private NPC currentTarget;
        private List<int> hitNPCs = new List<int>();
        private const int MaxChainCount = 8;
        private const int TrailLength = 20;
        public override void SetStaticDefaults() {
            //启用拖尾
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = TrailLength;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }
        public override void SetDefaults() {
            Projectile.width = 30;
            Projectile.height = 30;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Magic;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 300;
            Projectile.alpha = 255;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
        }

        public override void AI() {
            //淡入效果
            if (Projectile.alpha > 0) {
                Projectile.alpha -= 15;
                if (Projectile.alpha < 0) Projectile.alpha = 0;
            }

            //寻找目标
            if (SearchCooldown <= 0) {
                currentTarget = FindNextTarget();
                SearchCooldown = 10;
            }
            else {
                SearchCooldown--;
            }

            //追踪目标
            if (currentTarget != null && currentTarget.active && !currentTarget.dontTakeDamage) {
                Vector2 toTarget = currentTarget.Center - Projectile.Center;
                float distance = toTarget.Length();

                if (distance < 50f) {
                    //接近目标，准备跳跃
                    ChainToNextTarget();
                }
                else {
                    //追踪目标
                    float speed = 16f + TierLevel * 2f;
                    Projectile.velocity = Vector2.Lerp(Projectile.velocity, toTarget.SafeNormalize(Vector2.Zero) * speed, 0.15f);
                }
            }
            else {
                //没有目标，缓慢减速
                Projectile.velocity *= 0.95f;
                if (Projectile.velocity.Length() < 0.5f) {
                    Projectile.Kill();
                }
            }

            Projectile.rotation = Projectile.velocity.ToRotation();

            //电弧粒子效果
            if (Main.rand.NextBool(2)) {
                Dust d = Dust.NewDustPerfect(Projectile.Center, (int)CalamityDusts.Brimstone, Main.rand.NextVector2Circular(2f, 2f), 100, Color.Cyan, 1.2f);
                d.noGravity = true;
            }

            //闪电特效
            if (Main.rand.NextBool(5)) {
                Vector2 lightningPos = Projectile.Center + Main.rand.NextVector2Circular(40f, 40f);
                Dust d = Dust.NewDustPerfect(lightningPos, (int)CalamityDusts.Brimstone, Vector2.Zero, 100, Color.White, 1.5f);
                d.noGravity = true;
                d.fadeIn = 1.3f;
            }

            Lighting.AddLight(Projectile.Center, 0.8f, 1.2f, 1.5f);
            Projectile.scale = 0.5f;
        }

        private NPC FindNextTarget() {
            NPC closest = null;
            float maxDist = 600f + TierLevel * 100f;

            foreach (NPC npc in Main.npc) {
                if (npc.CanBeChasedBy(this) && !hitNPCs.Contains(npc.whoAmI)) {
                    float dist = Projectile.Distance(npc.Center);
                    if (dist < maxDist) {
                        maxDist = dist;
                        closest = npc;
                    }
                }
            }
            return closest;
        }

        private void ChainToNextTarget() {
            if (currentTarget == null || !currentTarget.active) return;

            //记录已命中的NPC
            hitNPCs.Add(currentTarget.whoAmI);

            //造成伤害
            Projectile.Center = currentTarget.Center;
            Projectile.Damage();

            //应用debuff
            currentTarget.AddBuff(BuffID.Electrified, 180);

            //闪电链效果
            SoundEngine.PlaySound(SoundID.Item93 with { Volume = 0.6f, Pitch = 0.2f }, Projectile.Center);

            //生成闪电弧视觉效果
            SpawnLightningArc();

            ChainCount++;

            //检查是否还能继续跳跃
            if (ChainCount >= MaxChainCount) {
                //达到最大跳跃次数，爆炸
                ExplodeWithLightning();
                Projectile.Kill();
                return;
            }

            //寻找下一个目标
            NPC nextTarget = FindNextTarget();
            if (nextTarget != null) {
                //跳向下一个目标
                Vector2 direction = (nextTarget.Center - Projectile.Center).SafeNormalize(Vector2.Zero);
                Projectile.velocity = direction * (18f + TierLevel * 2f);
                currentTarget = nextTarget;
                SearchCooldown = 0;
            }
            else {
                //没有下一个目标，爆炸
                ExplodeWithLightning();
                Projectile.Kill();
            }
        }

        private void SpawnLightningArc() {
            //生成闪电弧特效
            for (int i = 0; i < 20; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(8f, 8f);
                Dust d = Dust.NewDustPerfect(Projectile.Center, (int)CalamityDusts.Brimstone, vel, 100, Color.Cyan, Main.rand.NextFloat(1.5f, 2.5f));
                d.noGravity = true;
            }

            //生成闪电射线
            for (int i = 0; i < 6; i++) {
                float angle = MathHelper.TwoPi * i / 6f;
                Vector2 lineEnd = Projectile.Center + angle.ToRotationVector2() * 120f;

                //用粒子模拟闪电线
                for (int j = 0; j < 10; j++) {
                    float progress = j / 10f;
                    Vector2 linePos = Vector2.Lerp(Projectile.Center, lineEnd, progress);
                    linePos += Main.rand.NextVector2Circular(10f, 10f);

                    Dust d = Dust.NewDustPerfect(linePos, (int)CalamityDusts.Brimstone, Vector2.Zero, 100, Color.White, 0.8f);
                    d.noGravity = true;
                    d.fadeIn = 0.5f;
                }
            }
        }

        private void ExplodeWithLightning() {
            //最终爆炸效果
            SoundEngine.PlaySound(SoundID.Item122 with { Volume = 1.0f, Pitch = -0.2f }, Projectile.Center);

            //大范围闪电爆发
            for (int i = 0; i < 40; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(12f, 12f);
                Dust d = Dust.NewDustPerfect(Projectile.Center, (int)CalamityDusts.Brimstone, vel, 100, Color.Cyan, Main.rand.NextFloat(2f, 3f));
                d.noGravity = true;
            }

            //生成电弧环
            for (int i = 0; i < 30; i++) {
                float angle = MathHelper.TwoPi * i / 30f;
                Vector2 vel = angle.ToRotationVector2() * Main.rand.NextFloat(10f, 15f);
                Dust d = Dust.NewDustPerfect(Projectile.Center, (int)CalamityDusts.Brimstone, vel, 100, Color.White, 1.5f);
                d.noGravity = true;
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            //伤害随跳跃次数递减
            float damageMult = 1f - (ChainCount * 0.1f);
            hit.Damage = (int)(hit.Damage * Math.Max(damageMult, 0.3f));
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D glow = CWRAsset.StarTexture.Value;
            float time = Main.GlobalTimeWrappedHourly;
            float pulse = (float)Math.Sin(time * 30f) * 0.5f + 0.5f;
            float alpha = (255 - Projectile.alpha) / 255f;

            // 绘制拖尾效果
            DrawTrail(glow, alpha, time);

            // 绘制主体
            Vector2 drawPos = Projectile.Center - Main.screenPosition;

            Main.spriteBatch.Draw(glow, drawPos, null, Color.Red with { A = 0 } * 0.6f * alpha, time * 13f, glow.Size() / 2, Projectile.scale * 2.0f, 0, 0);
            Main.spriteBatch.Draw(glow, drawPos, null, Color.OrangeRed with { A = 0 } * 0.8f * alpha, -time * 12f, glow.Size() / 2, Projectile.scale * (1.5f + pulse * 0.3f), 0, 0);
            Main.spriteBatch.Draw(glow, drawPos, null, Color.DarkRed with { A = 0 } * alpha, time * 14f, glow.Size() / 2, Projectile.scale * (1.0f + pulse * 0.5f), 0, 0);

            //绘制电弧核心
            Main.spriteBatch.Draw(glow, drawPos, null, Color.White with { A = 0 } * 0.8f * alpha, 0, glow.Size() / 2, Projectile.scale * 0.5f, 0, 0);

            return false;
        }

        private void DrawTrail(Texture2D texture, float alpha, float time) {
            // 绘制多层拖尾效果
            for (int i = 0; i < Projectile.oldPos.Length; i++) {
                if (Projectile.oldPos[i] == Vector2.Zero) continue;

                float progress = i / (float)Projectile.oldPos.Length;
                float trailAlpha = (1f - progress) * alpha;
                float trailScale = Projectile.scale * (1f - progress * 0.5f);

                Vector2 trailPos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                float trailRotation = Projectile.oldRot[i];

                // 外层拖尾 - 青色电弧
                Color outerColor = Color.Lerp(Color.OrangeRed, Color.DarkRed, progress) with { A = 0 };
                Main.spriteBatch.Draw(
                    texture,
                    trailPos,
                    null,
                    outerColor * trailAlpha * 0.5f,
                    trailRotation + time * 5f,
                    texture.Size() / 2,
                    trailScale * 1.8f,
                    0,
                    0
                );

                // 中层拖尾 - 白色闪光
                Color middleColor = Color.Lerp(Color.OrangeRed, Color.DarkRed, progress) with { A = 0 };
                Main.spriteBatch.Draw(
                    texture,
                    trailPos,
                    null,
                    middleColor * trailAlpha * 0.7f,
                    -trailRotation - time * 6f,
                    texture.Size() / 2,
                    trailScale * 1.2f,
                    0,
                    0
                );

                // 内层拖尾 - 亮蓝核心
                Color innerColor = Color.Lerp(Color.IndianRed, Color.DarkRed, progress) with { A = 0 };
                Main.spriteBatch.Draw(
                    texture,
                    trailPos,
                    null,
                    innerColor * trailAlpha * 0.9f,
                    trailRotation * 2f + time * 8f,
                    texture.Size() / 2,
                    trailScale * 0.8f,
                    0,
                    0
                );

                // 闪电特效 - 偶尔的强光脉冲
                if (i % 3 == 0) {
                    float sparkPulse = (float)Math.Sin(time * 40f + i) * 0.5f + 0.5f;
                    Main.spriteBatch.Draw(
                        texture,
                        trailPos,
                        null,
                        Color.White with { A = 0 } * trailAlpha * sparkPulse * 0.6f,
                        time * 15f + i,
                        texture.Size() / 2,
                        trailScale * 0.5f,
                        0,
                        0
                    );
                }
            }
        }
    }
}
