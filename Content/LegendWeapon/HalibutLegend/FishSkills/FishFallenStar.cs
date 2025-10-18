using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.HalibutLegend.FishSkills
{
    internal class FishFallenStar : FishSkill
    {
        public override int UnlockFishID => ItemID.FallenStarfish;
        public override int DefaultCooldown => 39 - HalibutData.GetDomainLayer() * 3;
        public override int ResearchDuration => 60 * 12;
        // 星星管理系统
        private static int consecutiveShots = 0; // 连续射击计数
        private static int ShotsForStarRain => 15 - HalibutData.GetDomainLayer(); // 每20-10次射击触发一次星雨

        public override bool? Shoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source,
            Vector2 position, Vector2 velocity, int type, int damage, float knockback) {

            if (Cooldown <= 0) {
                SetCooldown();

                // 发射螺旋星星
                Vector2 direction = velocity.SafeNormalize(Vector2.Zero);
                int starDamage = (int)(damage * (0.55f + HalibutData.GetDomainLayer() * 0.15f));

                // 生成主星星弹幕
                int mainStar = Projectile.NewProjectile(
                    source,
                    position,
                    direction * velocity.Length(),
                    ModContent.ProjectileType<SpiralStarProjectile>(),
                    starDamage,
                    knockback * 0.5f,
                    player.whoAmI,
                    ai0: 0 // 主星星
                );

                // 生成两个伴随的小星星（螺旋围绕主星星）
                for (int i = 0; i < 2; i++) {
                    float angleOffset = (i == 0 ? 1 : -1) * MathHelper.PiOver2;
                    Projectile.NewProjectile(
                        source,
                        position,
                        direction * velocity.Length(),
                        ModContent.ProjectileType<SpiralStarProjectile>(),
                        (int)(starDamage * 0.75f),
                        knockback * 0.5f,
                        player.whoAmI,
                        ai0: mainStar, // 传递主星星ID
                        ai1: angleOffset // 初始角度偏移
                    );
                }

                // 星星发射音效
                SoundEngine.PlaySound(SoundID.Item9 with {
                    Volume = 0.6f,
                    Pitch = 0.5f
                }, position);

                // 连续射击计数
                consecutiveShots++;

                // 检查是否触发星雨
                if (consecutiveShots >= ShotsForStarRain) {
                    consecutiveShots = 0;
                    TriggerStarRain(player, source, damage);
                }

                // 发射粒子效果
                SpawnShootEffect(position, direction);
            }

            return null;
        }

        /// <summary>
        /// 触发天降星雨
        /// </summary>
        private void TriggerStarRain(Player player, EntitySource_ItemUse_WithAmmo source, int baseDamage) {
            // 在鼠标周围区域生成多个下落星星
            Vector2 targetArea = Main.MouseWorld;
            int starCount = 5 + HalibutData.GetLevel() / 2; // 5-10个星星

            for (int i = 0; i < starCount; i++) {
                // 随机分散在目标区域上方
                Vector2 spawnPos = targetArea + new Vector2(
                    Main.rand.NextFloat(-400f, 400f),
                    Main.rand.NextFloat(-800f, -600f)
                );

                // 计算指向目标区域的速度
                Vector2 toTarget = (targetArea + Main.rand.NextVector2Circular(100f, 100f) - spawnPos).SafeNormalize(Vector2.Zero);
                Vector2 velocity = toTarget * Main.rand.NextFloat(12f, 18f);

                // 延迟生成（制造星雨效果）
                int delay = i * 3;

                Projectile.NewProjectile(
                    source,
                    spawnPos,
                    velocity,
                    ModContent.ProjectileType<FallingStarProjectile>(),
                    (int)(baseDamage * (1.5 + HalibutData.GetDomainLayer() * 0.3) * 3.00),
                    8f,
                    player.whoAmI,
                    ai0: delay
                );
            }

            // 星雨触发音效
            SoundEngine.PlaySound(SoundID.Item88 with {
                Volume = 0.8f,
                Pitch = 0.3f
            }, targetArea);

            // 目标区域指示特效
            SpawnStarRainIndicator(targetArea);
        }

        private void SpawnShootEffect(Vector2 position, Vector2 direction) {
            // 发射时的星星粒子
            for (int i = 0; i < 8; i++) {
                Vector2 velocity = direction.RotatedByRandom(0.3f) * Main.rand.NextFloat(2f, 6f);

                Dust star = Dust.NewDustPerfect(
                    position,
                    DustID.YellowStarDust,
                    velocity,
                    100,
                    new Color(255, 255, 150),
                    Main.rand.NextFloat(1.2f, 1.8f)
                );
                star.noGravity = true;
                star.fadeIn = 1.2f;
            }
        }

        private void SpawnStarRainIndicator(Vector2 position) {
            // 天降星雨的目标区域指示
            for (int i = 0; i < 30; i++) {
                float angle = MathHelper.TwoPi * i / 30f;
                Vector2 velocity = angle.ToRotationVector2() * Main.rand.NextFloat(3f, 8f);

                Dust indicator = Dust.NewDustPerfect(
                    position,
                    DustID.YellowStarDust,
                    velocity,
                    100,
                    new Color(255, 240, 100),
                    Main.rand.NextFloat(1.5f, 2.5f)
                );
                indicator.noGravity = true;
                indicator.fadeIn = 1.4f;
            }
        }
    }

    /// <summary>
    /// 螺旋星星弹幕，主星星和伴随星星
    /// </summary>
    internal class SpiralStarProjectile : ModProjectile
    {
        public override string Texture => "Terraria/Images/Item_" + ItemID.FallenStar;

        private ref float MainStarID => ref Projectile.ai[0];
        private ref float AngleOffset => ref Projectile.ai[1];

        private bool IsMainStar => MainStarID == 0;
        private float spiralAngle = 0f;
        private const float SpiralRadius = 40f;
        private const float SpiralSpeed = 0.15f;

        [VaultLoaden(CWRConstant.Masking)]
        private static Asset<Texture2D> SoftGlow = null;

        [VaultLoaden(CWRConstant.Masking)]
        private static Asset<Texture2D> StarTexture = null;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 15;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
            Main.projFrames[Projectile.type] = 1;
        }

        public override void SetDefaults() {
            Projectile.width = 20;
            Projectile.height = 20;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
            Projectile.penetrate = IsMainStar ? 3 : 2;
            Projectile.timeLeft = 300;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 10;

            spiralAngle = AngleOffset;
        }

        public override void AI() {
            if (IsMainStar) {
                // 主星星：直线前进
                MainStarAI();
            }
            else {
                // 伴随星星：螺旋围绕主星星
                CompanionStarAI();
            }

            // 旋转
            Projectile.rotation += 0.2f;

            // 照明
            Lighting.AddLight(Projectile.Center, 1.0f, 1.0f, 0.6f);
        }

        private void MainStarAI() {
            // 轻微速度衰减
            Projectile.velocity *= 0.995f;

            // 轻微波动
            float wave = (float)Math.Sin(Main.GlobalTimeWrappedHourly * 4f) * 0.5f;
            Vector2 perpendicular = Projectile.velocity.SafeNormalize(Vector2.Zero).RotatedBy(MathHelper.PiOver2);
            Projectile.velocity += perpendicular * wave * 0.1f;

            // 星星轨迹粒子
            if (Main.rand.NextBool(3)) {
                SpawnTrailParticle();
            }
        }

        private void CompanionStarAI() {
            // 检查主星星是否存在
            if (MainStarID < 0 || MainStarID >= Main.maxProjectiles || !Main.projectile[(int)MainStarID].active) {
                Projectile.Kill();
                return;
            }

            Projectile mainStar = Main.projectile[(int)MainStarID];

            // 螺旋角度递增
            spiralAngle += SpiralSpeed;

            // 计算螺旋位置
            Vector2 forwardDir = mainStar.velocity.SafeNormalize(Vector2.Zero);
            Vector2 rightDir = forwardDir.RotatedBy(MathHelper.PiOver2);

            float radiusWave = SpiralRadius * (1f + (float)Math.Sin(spiralAngle * 2f) * 0.2f);
            Vector2 offset = new Vector2(
                (float)Math.Cos(spiralAngle) * radiusWave,
                (float)Math.Sin(spiralAngle) * radiusWave
            );

            // 转换到世界坐标系
            Vector2 targetPos = mainStar.Center + forwardDir * offset.X + rightDir * offset.Y;

            // 平滑移动到目标位置
            Projectile.velocity = (targetPos - Projectile.Center) * 0.3f;

            // 伴随星星轨迹粒子
            if (Main.rand.NextBool(4)) {
                SpawnCompanionTrailParticle();
            }
        }

        private void SpawnTrailParticle() {
            Dust trail = Dust.NewDustPerfect(
                Projectile.Center + Main.rand.NextVector2Circular(5f, 5f),
                DustID.YellowStarDust,
                -Projectile.velocity * Main.rand.NextFloat(0.1f, 0.3f),
                100,
                new Color(255, 255, 150),
                Main.rand.NextFloat(1f, 1.5f)
            );
            trail.noGravity = true;
            trail.fadeIn = 1.1f;
        }

        private void SpawnCompanionTrailParticle() {
            Dust trail = Dust.NewDustPerfect(
                Projectile.Center,
                DustID.YellowStarDust,
                -Projectile.velocity * Main.rand.NextFloat(0.2f, 0.4f),
                100,
                new Color(255, 240, 100),
                Main.rand.NextFloat(0.8f, 1.2f)
            );
            trail.noGravity = true;
            trail.fadeIn = 1f;
            trail.alpha = 100;
        }

        public override void OnKill(int timeLeft) {
            // 星星消失特效
            for (int i = 0; i < 15; i++) {
                Vector2 velocity = Main.rand.NextVector2Circular(5f, 5f);

                Dust explode = Dust.NewDustPerfect(
                    Projectile.Center,
                    DustID.YellowStarDust,
                    velocity,
                    100,
                    new Color(255, 255, 150),
                    Main.rand.NextFloat(1.2f, 2f)
                );
                explode.noGravity = true;
                explode.fadeIn = 1.3f;
            }

            // 消失音效
            SoundEngine.PlaySound(SoundID.Item10 with {
                Volume = 0.4f,
                Pitch = 0.6f
            }, Projectile.Center);
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            // 击中星星粒子
            for (int i = 0; i < 8; i++) {
                Vector2 velocity = Main.rand.NextVector2Circular(4f, 4f);

                Dust hitStar = Dust.NewDustPerfect(
                    Projectile.Center,
                    DustID.YellowStarDust,
                    velocity,
                    100,
                    new Color(255, 255, 100),
                    Main.rand.NextFloat(1.2f, 1.8f)
                );
                hitStar.noGravity = true;
                hitStar.fadeIn = 1.2f;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            SpriteBatch sb = Main.spriteBatch;
            Texture2D starTex = TextureAssets.Item[ItemID.FallenStar].Value;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            Rectangle sourceRect = starTex.GetRectangle(((int)(Main.GameUpdateCount % 8)), 8);
            Vector2 origin = sourceRect.Size() / 2f;

            float alpha = (255f - Projectile.alpha) / 255f;
            float scale = Projectile.scale * (IsMainStar ? 1.2f : 0.8f);

            // ===== 绘制星星轨迹 =====
            DrawStarTrail(sb, starTex, sourceRect, origin, alpha);

            // ===== 绘制外层辉光 =====
            if (SoftGlow?.Value != null) {
                Texture2D glow = SoftGlow.Value;
                float glowScale = scale * (1.2f + (float)Math.Sin(Main.GlobalTimeWrappedHourly * 6f) * 0.2f);
                float glowAlpha = alpha * 0.6f;

                sb.Draw(
                    glow,
                    drawPos,
                    null,
                    new Color(255, 255, 150, 0) * glowAlpha,
                    Projectile.rotation,
                    glow.Size() / 2f,
                    glowScale,
                    SpriteEffects.None,
                    0f
                );
            }

            // ===== 绘制主体星星 =====
            Color starColor = Color.Lerp(lightColor, Color.White, 0.7f);

            sb.Draw(
                starTex,
                drawPos,
                sourceRect,
                starColor * alpha,
                Projectile.rotation,
                origin,
                scale,
                SpriteEffects.None,
                0
            );

            // 发光覆盖层
            sb.Draw(
                starTex,
                drawPos,
                sourceRect,
                new Color(255, 255, 200) * (alpha * 0.5f),
                Projectile.rotation,
                origin,
                scale * 1.05f,
                SpriteEffects.None,
                0
            );

            // ===== 绘制星形闪光 =====
            if (StarTexture?.Value != null) {
                Texture2D star = StarTexture.Value;
                float starPulse = (float)Math.Sin(Main.GlobalTimeWrappedHourly * 8f) * 0.5f + 0.5f;

                sb.Draw(
                    star,
                    drawPos,
                    null,
                    new Color(255, 255, 150, 0) * (alpha * starPulse * 0.6f),
                    Projectile.rotation * 2f,
                    star.Size() / 2f,
                    scale * 0.6f,
                    SpriteEffects.None,
                    0f
                );
            }

            return false;
        }

        private void DrawStarTrail(SpriteBatch sb, Texture2D starTex, Rectangle sourceRect, Vector2 origin, float alpha) {
            for (int i = 0; i < Projectile.oldPos.Length; i++) {
                if (Projectile.oldPos[i] == Vector2.Zero) continue;

                float trailProgress = 1f - i / (float)Projectile.oldPos.Length;
                float trailAlpha = trailProgress * alpha * 0.5f;
                float trailScale = Projectile.scale * (IsMainStar ? 1.2f : 0.8f) * MathHelper.Lerp(0.8f, 1f, trailProgress);

                Vector2 trailPos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;

                Color trailColor = Color.Lerp(
                    new Color(255, 255, 100),
                    new Color(255, 255, 200),
                    trailProgress
                ) * trailAlpha;

                sb.Draw(
                    starTex,
                    trailPos,
                    sourceRect,
                    trailColor,
                    Projectile.rotation - i * 0.1f,
                    origin,
                    trailScale,
                    SpriteEffects.None,
                    0
                );
            }
        }
    }

    /// <summary>
    /// 天降星星弹幕
    /// </summary>
    internal class FallingStarProjectile : ModProjectile
    {
        public override string Texture => "Terraria/Images/Item_" + ItemID.FallenStar;

        private ref float SpawnDelay => ref Projectile.ai[0];
        private bool hasSpawned = false;
        private float trailIntensity = 0f;

        [VaultLoaden(CWRConstant.Masking)]
        private static Asset<Texture2D> SoftGlow = null;

        [VaultLoaden(CWRConstant.Masking)]
        private static Asset<Texture2D> StarTexture = null;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 20;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 24;
            Projectile.height = 24;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 300;
            Projectile.alpha = 255; // 初始完全透明
        }

        public override void AI() {
            // 延迟生成
            if (SpawnDelay > 0) {
                SpawnDelay--;
                Projectile.velocity = Vector2.Zero;
                return;
            }

            if (!hasSpawned) {
                hasSpawned = true;

                // 生成音效
                SoundEngine.PlaySound(SoundID.Item9 with {
                    Volume = 0.4f,
                    Pitch = 0.7f
                }, Projectile.Center);

                // 生成特效
                for (int i = 0; i < 12; i++) {
                    Vector2 velocity = Main.rand.NextVector2Circular(3f, 3f);

                    Dust spawn = Dust.NewDustPerfect(
                        Projectile.Center,
                        DustID.YellowStarDust,
                        velocity,
                        100,
                        new Color(255, 255, 150),
                        Main.rand.NextFloat(1.5f, 2.2f)
                    );
                    spawn.noGravity = true;
                    spawn.fadeIn = 1.3f;
                }
            }

            // 淡入
            if (Projectile.alpha > 0) {
                Projectile.alpha -= 15;
                if (Projectile.alpha < 0) Projectile.alpha = 0;
            }

            // 加速下落
            Projectile.velocity.Y += 0.3f;
            if (Projectile.velocity.Y > 20f) {
                Projectile.velocity.Y = 20f;
            }

            // 轨迹强度增加
            trailIntensity = MathHelper.Lerp(trailIntensity, 1f, 0.1f);

            // 旋转
            Projectile.rotation += 0.3f;

            // 强烈照明
            Lighting.AddLight(Projectile.Center, 1.2f, 1.2f, 0.8f);

            // 下落轨迹粒子
            if (Main.rand.NextBool(2)) {
                SpawnFallingTrailParticle();
            }
        }

        private void SpawnFallingTrailParticle() {
            Dust trail = Dust.NewDustPerfect(
                Projectile.Center + Main.rand.NextVector2Circular(8f, 8f),
                DustID.YellowStarDust,
                -Projectile.velocity * Main.rand.NextFloat(0.1f, 0.3f),
                100,
                new Color(255, 255, 150),
                Main.rand.NextFloat(1.2f, 2f)
            );
            trail.noGravity = true;
            trail.fadeIn = 1.2f;
        }

        public override void OnKill(int timeLeft) {
            // 坠落撞击特效
            for (int i = 0; i < 25; i++) {
                float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                Vector2 velocity = angle.ToRotationVector2() * Main.rand.NextFloat(3f, 10f);

                Dust impact = Dust.NewDustPerfect(
                    Projectile.Center,
                    DustID.YellowStarDust,
                    velocity,
                    100,
                    new Color(255, 255, 100),
                    Main.rand.NextFloat(1.5f, 2.5f)
                );
                impact.noGravity = true;
                impact.fadeIn = 1.4f;
            }

            // 撞击波纹
            for (int i = 0; i < 12; i++) {
                float angle = MathHelper.TwoPi * i / 12f;
                Vector2 velocity = angle.ToRotationVector2() * 5f;

                Dust ripple = Dust.NewDustPerfect(
                    Projectile.Center,
                    DustID.YellowStarDust,
                    velocity,
                    100,
                    new Color(255, 240, 100),
                    Main.rand.NextFloat(2f, 3f)
                );
                ripple.noGravity = true;
                ripple.fadeIn = 1.5f;
            }

            // 撞击音效
            SoundEngine.PlaySound(SoundID.Item10 with {
                Volume = 0.7f,
                Pitch = 0.3f
            }, Projectile.Center);
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            // 击中额外粒子
            for (int i = 0; i < 12; i++) {
                Vector2 velocity = Main.rand.NextVector2Circular(6f, 6f);

                Dust hitStar = Dust.NewDustPerfect(
                    Projectile.Center,
                    DustID.YellowStarDust,
                    velocity,
                    100,
                    new Color(255, 255, 100),
                    Main.rand.NextFloat(1.5f, 2.2f)
                );
                hitStar.noGravity = true;
                hitStar.fadeIn = 1.3f;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            SpriteBatch sb = Main.spriteBatch;
            Texture2D starTex = TextureAssets.Item[ItemID.FallenStar].Value;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            Rectangle sourceRect = starTex.GetRectangle(((int)(Main.GameUpdateCount % 8)), 8);
            Vector2 origin = sourceRect.Size() / 2f;

            float alpha = (255f - Projectile.alpha) / 255f;
            float scale = Projectile.scale * 1.3f;

            // ===== 绘制强烈的坠落轨迹 =====
            DrawFallingTrail(sb, starTex, sourceRect, origin, alpha);

            // ===== 绘制外层强烈辉光 =====
            if (SoftGlow?.Value != null) {
                Texture2D glow = SoftGlow.Value;
                float glowScale = scale * (1.5f + (float)Math.Sin(Main.GlobalTimeWrappedHourly * 10f) * 0.3f);
                float glowAlpha = alpha * trailIntensity * 0.8f;

                sb.Draw(
                    glow,
                    drawPos,
                    null,
                    new Color(255, 255, 100, 0) * glowAlpha,
                    Projectile.rotation,
                    glow.Size() / 2f,
                    glowScale,
                    SpriteEffects.None,
                    0f
                );
            }

            // ===== 绘制主体星星 =====
            Color starColor = Color.Lerp(lightColor, new Color(255, 255, 150), 0.9f);

            sb.Draw(
                starTex,
                drawPos,
                sourceRect,
                starColor * alpha,
                Projectile.rotation,
                origin,
                scale,
                SpriteEffects.None,
                0
            );

            // 强烈发光覆盖层
            sb.Draw(
                starTex,
                drawPos,
                sourceRect,
                Color.White * (alpha * 0.7f),
                Projectile.rotation,
                origin,
                scale * 1.1f,
                SpriteEffects.None,
                0
            );

            // ===== 绘制多层星形闪光 =====
            if (StarTexture?.Value != null) {
                Texture2D star = StarTexture.Value;

                for (int i = 0; i < 2; i++) {
                    float starPulse = (float)Math.Sin(Main.GlobalTimeWrappedHourly * (10f + i * 2f)) * 0.5f + 0.5f;
                    float starRot = Projectile.rotation * (2f + i);

                    sb.Draw(
                        star,
                        drawPos,
                        null,
                        new Color(255, 255, 100, 0) * (alpha * starPulse * trailIntensity * 0.7f),
                        starRot,
                        star.Size() / 2f,
                        scale * (0.6f + i * 0.2f),
                        SpriteEffects.None,
                        0f
                    );
                }
            }

            return false;
        }

        private void DrawFallingTrail(SpriteBatch sb, Texture2D starTex, Rectangle sourceRect, Vector2 origin, float alpha) {
            for (int i = 0; i < Projectile.oldPos.Length; i++) {
                if (Projectile.oldPos[i] == Vector2.Zero) continue;

                float trailProgress = 1f - i / (float)Projectile.oldPos.Length;
                float trailAlpha = trailProgress * alpha * trailIntensity * 0.6f;
                float trailScale = Projectile.scale * 1.3f * MathHelper.Lerp(0.7f, 1f, trailProgress);

                Vector2 trailPos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;

                // 渐变颜色：从亮黄到橙黄
                Color trailColor = Color.Lerp(
                    new Color(255, 200, 100),
                    new Color(255, 255, 150),
                    trailProgress
                ) * trailAlpha;

                sb.Draw(
                    starTex,
                    trailPos,
                    sourceRect,
                    trailColor,
                    Projectile.rotation - i * 0.15f,
                    origin,
                    trailScale,
                    SpriteEffects.None,
                    0
                );
            }
        }
    }
}
