using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Accessories.JusticeUnveileds
{
    /// <summary>
    /// 十字架环绕玩家弹幕
    /// </summary>
    internal class JusticeUnveiledCross : ModProjectile
    {
        public override string Texture => CWRConstant.Item_Accessorie + "JusticeUnveiled";
        private int crossIndex;
        private float rotation;
        private float spawnProgress = 0f; //出现进度
        private float pulsePhase = 0f; //脉动相位
        private int particleTimer = 0; //粒子生成计时器

        public override void SetStaticDefaults() {
            Main.projFrames[Type] = 1;
        }

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 32;
            Projectile.friendly = false;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = int.MaxValue;
        }

        public override void AI() {
            Player player = Main.player[Projectile.owner];
            if (!player.active || player.dead || !player.CWR().IsJusticeUnveiled) {
                //添加消失特效
                SpawnDespawnEffect();
                Projectile.Kill();
                return;
            }

            crossIndex = (int)Projectile.ai[0];

            //出现动画（前30帧）
            if (spawnProgress < 1f) {
                spawnProgress += 0.033f; //30帧完成
                if (spawnProgress > 1f) spawnProgress = 1f;

                //播放出现音效
                if (spawnProgress >= 0.98f && Projectile.owner == Main.myPlayer) {
                    SoundEngine.PlaySound(SoundID.Item29 with {
                        Volume = 0.4f,
                        Pitch = 0.3f + crossIndex * 0.1f
                    }, Projectile.Center);
                }
            }

            //使用缓动函数实现平滑的出现效果
            float appearEase = CWRUtils.EaseOutBack(spawnProgress);

            //旋转效果 - 根据索引添加相位差
            rotation += 0.06f + crossIndex * 0.01f;
            pulsePhase += 0.12f;

            //计算环绕位置 - 添加轻微的波动效果
            float baseDistance = 60f;
            float distanceWave = (float)Math.Sin(Main.GameUpdateCount * 0.03f + crossIndex * MathHelper.PiOver2) * 5f;
            float distance = (baseDistance + distanceWave) * appearEase;

            float baseAngle = MathHelper.TwoPi / 5f * crossIndex;
            float angle = baseAngle + Main.GameUpdateCount * 0.02f;

            //添加轻微的上下浮动
            float verticalOffset = (float)Math.Sin(Main.GameUpdateCount * 0.04f + crossIndex * MathHelper.Pi) * 3f * appearEase;
            Vector2 targetPos = player.Center + angle.ToRotationVector2() * distance;
            targetPos.Y += verticalOffset;

            //平滑移动到目标位置
            Projectile.Center = Vector2.Lerp(Projectile.Center, targetPos, 0.2f);

            //生成环绕粒子效果
            particleTimer++;
            if (spawnProgress >= 1f && particleTimer % 8 == 0) {
                SpawnTrailParticles(player);
            }

            //定期生成光环粒子
            if (Main.rand.NextBool(15) && spawnProgress >= 1f) {
                SpawnAuraParticle();
            }

            //检测玩家按下Up键
            if (player.whoAmI == Main.myPlayer && JusticeUnveiled.justUp && player.CWR().JusticeUnveiledCooldown <= 0) {
                if (player.CWR().JusticeUnveiledCharges > 0 && crossIndex == player.CWR().JusticeUnveiledCharges) {
                    NPC target = player.Center.FindClosestNPC(1200, false);
                    if (target != null) {
                        player.CWR().JusticeUnveiledCharges--;
                        if (player.CWR().JusticeUnveiledCharges < 0) {
                            player.CWR().JusticeUnveiledCharges = 0;
                        }
                        player.CWR().JusticeUnveiledCooldown = 2;

                        //发射前的特效
                        SpawnLaunchEffect(target.Center);

                        Projectile.Kill();
                        ShootState shootState = player.GetShootState();
                        Projectile.NewProjectile(player.FromObjectGetParent()
                            , target.Center + new Vector2(0, -1120), new Vector2(0, 6)
                            , ModContent.ProjectileType<DivineJustice>(), shootState.WeaponDamage, 2, player.whoAmI, target.whoAmI);
                        SoundEngine.PlaySound(SoundID.Item9 with { Volume = 0.6f }, player.Center);
                    }
                }
            }

            //增强发光效果
            float lightPulse = (float)Math.Sin(pulsePhase) * 0.4f + 0.6f;
            float lightIntensity = lightPulse * appearEase;
            Lighting.AddLight(Projectile.Center,
                1.0f * lightIntensity,
                0.8f * lightIntensity,
                0.3f * lightIntensity);
        }

        /// <summary>
        /// 生成轨迹粒子
        /// </summary>
        private void SpawnTrailParticles(Player player) {
            Vector2 toPlayer = Projectile.Center.To(player.Center);
            float angle = toPlayer.ToRotation();

            for (int i = 0; i < 2; i++) {
                Vector2 particleVel = angle.ToRotationVector2().RotatedByRandom(0.3f) * Main.rand.NextFloat(1f, 3f);
                Dust trail = Dust.NewDustPerfect(
                    Projectile.Center + Main.rand.NextVector2Circular(8f, 8f),
                    DustID.GoldCoin,
                    particleVel,
                    0,
                    default,
                    Main.rand.NextFloat(0.6f, 1.0f)
                );
                trail.noGravity = true;
                trail.fadeIn = 0.6f;
            }
        }

        /// <summary>
        /// 生成光环粒子
        /// </summary>
        private void SpawnAuraParticle() {
            float angle = Main.rand.NextFloat(MathHelper.TwoPi);
            Vector2 offset = angle.ToRotationVector2() * Main.rand.NextFloat(15f, 25f);
            Vector2 velocity = -offset.SafeNormalize(Vector2.Zero) * Main.rand.NextFloat(1f, 2f);

            Dust aura = Dust.NewDustPerfect(
                Projectile.Center + offset,
                DustID.Electric,
                velocity,
                0,
                Color.Gold,
                Main.rand.NextFloat(0.8f, 1.2f)
            );
            aura.noGravity = true;
            aura.fadeIn = 0.8f;
        }

        /// <summary>
        /// 生成发射特效
        /// </summary>
        private void SpawnLaunchEffect(Vector2 targetPos) {
            //爆发粒子
            for (int i = 0; i < 20; i++) {
                Vector2 velocity = Main.rand.NextVector2Circular(8f, 8f);
                Dust launch = Dust.NewDustPerfect(
                    Projectile.Center,
                    DustID.GoldCoin,
                    velocity,
                    0,
                    default,
                    Main.rand.NextFloat(1.2f, 1.8f)
                );
                launch.noGravity = true;
            }

            //指向目标的粒子束
            Vector2 toTarget = Projectile.Center.To(targetPos);
            float distance = toTarget.Length();
            int particleCount = (int)(distance / 30f);

            for (int i = 0; i < particleCount; i++) {
                float progress = i / (float)particleCount;
                Vector2 pos = Vector2.Lerp(Projectile.Center, targetPos, progress);

                Dust beam = Dust.NewDustPerfect(
                    pos,
                    DustID.Electric,
                    Vector2.Zero,
                    0,
                    Color.Gold,
                    Main.rand.NextFloat(0.8f, 1.2f)
                );
                beam.noGravity = true;
                beam.fadeIn = 1.0f;
            }
        }

        /// <summary>
        /// 生成消失特效
        /// </summary>
        private void SpawnDespawnEffect() {
            if (VaultUtils.isServer) return;

            for (int i = 0; i < 12; i++) {
                float angle = MathHelper.TwoPi * i / 12f;
                Vector2 velocity = angle.ToRotationVector2() * Main.rand.NextFloat(2f, 5f);

                Dust despawn = Dust.NewDustPerfect(
                    Projectile.Center,
                    DustID.GoldCoin,
                    velocity,
                    0,
                    default,
                    Main.rand.NextFloat(1.0f, 1.5f)
                );
                despawn.noGravity = true;
                despawn.fadeIn = 0.8f;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D texture = CWRUtils.GetT2DValue(Texture);
            Vector2 drawPos = Projectile.Center - Main.screenPosition;

            //脉动缩放效果
            float pulseScale = 1f + (float)Math.Sin(pulsePhase) * 0.15f;
            float baseScale = 0.5f * spawnProgress;
            float scale = baseScale * pulseScale;

            //计算透明度
            float alpha = spawnProgress * 0.9f;

            //绘制多层发光效果
            Color glowColor = Color.Gold * 0.6f * alpha;
            glowColor.A = 0;

            //外层光晕（最亮）
            for (int i = 0; i < 3; i++) {
                float offset = i * 0.15f;
                float glowScale = scale * (1f + offset);
                float glowAlpha = (1f - offset * 0.5f) * alpha;

                Main.spriteBatch.Draw(
                    texture,
                    drawPos,
                    null,
                    glowColor * glowAlpha,
                    rotation + i * 0.2f,
                    texture.Size() / 2f,
                    glowScale,
                    SpriteEffects.None,
                    0
                );
            }

            //中层脉动光环
            float ringScale = scale * (1f + (float)Math.Sin(pulsePhase * 1.5f) * 0.2f);
            Color ringColor = Color.Lerp(Color.Gold, Color.Yellow, 0.5f) * alpha;
            ringColor.A = 0;

            Main.spriteBatch.Draw(
                texture,
                drawPos,
                null,
                ringColor * 0.7f,
                -rotation * 0.5f,
                texture.Size() / 2f,
                ringScale,
                SpriteEffects.None,
                0
            );

            //主体十字架
            Main.spriteBatch.Draw(
                texture,
                drawPos,
                null,
                Color.White * alpha,
                rotation,
                texture.Size() / 2f,
                scale * 0.85f,
                SpriteEffects.None,
                0
            );

            //内核高亮
            Color coreColor = Color.White with { A = 0 };
            float coreScale = scale * 0.3f * (1f + (float)Math.Sin(pulsePhase * 2f) * 0.3f);
            Main.spriteBatch.Draw(
                texture,
                drawPos,
                null,
                coreColor * alpha * 0.8f,
                rotation * 2f,
                texture.Size() / 2f,
                coreScale,
                SpriteEffects.None,
                0
            );

            return false;
        }
    }
}
