using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.HalibutLegend.FishSkills
{
    internal class FishDynamite : FishSkill
    {
        //使用反射加载灰度纹理，这个是一个光束纹理，大小高1024宽256，
        //光束朝正上方，适合用来复合一些光束一类的特效或者爆炸的旋转光束演出
        [VaultLoaden(CWRConstant.Masking)]
        public static Texture2D LightBeam = null;

        public override int UnlockFishID => ItemID.DynamiteFish;
        public override int DefaultCooldown => 60 * (20 - HalibutData.GetDomainLayer());

        public override bool? AltFunctionUse(Item item, Player player) => true;

        public override bool? CanUseItem(Item item, Player player) {
            if (player.altFunctionUse == 2) {
                if (Cooldown > 0) return false;
                item.UseSound = null;
                Use(item, player);
                return false;
            }
            return base.CanUseItem(item, player);
        }

        public override void Use(Item item, Player player) {
            SetCooldown();

            //计算投掷方向和速度
            Vector2 direction = (Main.MouseWorld - player.Center).SafeNormalize(Vector2.UnitX * player.direction);
            Vector2 velocity = direction * 18f; //投掷速度

            //生成雷管鱼弹幕
            int damage = 1;
            Projectile.NewProjectile(
                player.GetSource_ItemUse(item),
                player.Center + direction * 40f,
                velocity,
                ModContent.ProjectileType<DynamiteFishProjectile>(),
                damage,
                8f,
                player.whoAmI
            );

            //投掷音效
            SoundEngine.PlaySound(SoundID.Item1 with { Volume = 0.8f, Pitch = -0.3f }, player.Center);
        }
    }

    /// <summary>
    /// 雷管鱼弹幕 - 滞留型接触爆炸物
    /// </summary>
    internal class DynamiteFishProjectile : ModProjectile
    {
        [VaultLoaden(CWRConstant.Masking)]
        private static Asset<Texture2D> SoftGlow = null;
        public override string Texture => CWRConstant.Placeholder;
        private ref float State => ref Projectile.ai[0]; //0=飞行中, 1=已着陆
        private ref float DetonationTimer => ref Projectile.ai[1];
        private const int MaxLifeTime = 600; //10秒生命期
        private const int LandingTime = 30; //着陆稳定时间
        private const float ProximityDetectionRange = 200f; //感应范围
        private bool hasDetonated = false;
        private int warningPulseTimer = 0;
        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 12;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 40;
            Projectile.height = 40;
            Projectile.friendly = true; //初始不造成伤害
            Projectile.hostile = false;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = false;
            Projectile.penetrate = 1;
            Projectile.timeLeft = MaxLifeTime;
            Projectile.alpha = 0;
        }

        public override void AI() {
            Player owner = Main.player[Projectile.owner];

            //=== 状态机：飞行 -> 着陆 -> 待命 -> 引爆 ===

            if (State == 0) {
                //飞行状态
                FlightPhaseAI();
            }
            else if (State == 1) {
                //着陆状态
                LandedPhaseAI(owner);
            }

            //旋转效果
            if (State == 0) {
                Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;
            }
            else {
                Projectile.rotation += 0.05f;
            }

            //照明效果
            float lightIntensity = State == 1 ? (1f + (float)Math.Sin(warningPulseTimer * 0.2f) * 0.5f) : 0.6f;
            Lighting.AddLight(Projectile.Center,
                1.2f * lightIntensity,
                0.3f * lightIntensity,
                0.1f * lightIntensity);
        }

        private void FlightPhaseAI() {
            //重力
            Projectile.velocity.Y += 0.4f;
            if (Projectile.velocity.Y > 16f) {
                Projectile.velocity.Y = 16f;
            }

            //空气阻力
            Projectile.velocity.X *= 0.99f;

            //飞行轨迹粒子
            if (Main.rand.NextBool(3)) {
                SpawnTrailParticle();
            }

            //飞行尾迹尘埃
            if (Main.rand.NextBool(2)) {
                Dust dust = Dust.NewDustDirect(
                    Projectile.position,
                    Projectile.width,
                    Projectile.height,
                    DustID.Smoke,
                    -Projectile.velocity.X * 0.3f,
                    -Projectile.velocity.Y * 0.3f,
                    100,
                    new Color(255, 100, 50),
                    Main.rand.NextFloat(1f, 1.5f)
                );
                dust.noGravity = true;
            }
        }

        private void LandedPhaseAI(Player owner) {
            DetonationTimer++;
            warningPulseTimer++;

            //着陆后短暂稳定期
            if (DetonationTimer < LandingTime) {
                Projectile.velocity *= 0.8f;

                //着陆冲击波
                if (DetonationTimer == 1) {
                    SpawnLandingEffect();
                }
                return;
            }

            //完全停止
            Projectile.velocity = Vector2.Zero;

            //接近检测：寻找附近的敌人
            bool enemyNearby = false;
            float nearestDistance = ProximityDetectionRange;

            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npc = Main.npc[i];
                if (npc.active && !npc.friendly && npc.lifeMax > 5 && !npc.dontTakeDamage) {
                    float distance = Vector2.Distance(Projectile.Center, npc.Center);

                    if (distance < ProximityDetectionRange) {
                        enemyNearby = true;
                        if (distance < nearestDistance) {
                            nearestDistance = distance;
                        }
                    }
                }
            }

            //警告粒子效果（敌人接近时）
            if (enemyNearby) {
                //加速警告频率
                if (warningPulseTimer % (int)MathHelper.Lerp(15, 5, 1f - nearestDistance / ProximityDetectionRange) == 0) {
                    SpawnWarningPulse();
                }

                //敌人非常接近时立即引爆
                if (nearestDistance < ProximityDetectionRange * 0.4f) {
                    Detonate();
                }
            }
            else {
                //待命粒子效果（轻微脉冲）
                if (warningPulseTimer % 30 == 0) {
                    SpawnIdleParticle();
                }
            }

            //超时自动引爆
            if (Projectile.timeLeft < 60) {
                Detonate();
            }
        }

        private void SpawnTrailParticle() {
            var prt = PRTLoader.NewParticle<PRT_HellFlame>(
                Projectile.Center + Main.rand.NextVector2Circular(8f, 8f),
                -Projectile.velocity * 0.2f + Main.rand.NextVector2Circular(1f, 1f),
                Color.White,
                Main.rand.NextFloat(0.4f, 0.8f)
            );
            prt.ai[0] = 0; //上升模式
            prt.ai[1] = 0.8f;
            prt.ai[2] = 20;
            prt.ai[3] = 40;
        }

        private void SpawnLandingEffect() {
            //着陆冲击尘埃
            for (int i = 0; i < 20; i++) {
                float angle = MathHelper.TwoPi * i / 20f;
                Vector2 velocity = angle.ToRotationVector2() * Main.rand.NextFloat(2f, 5f);

                Dust dust = Dust.NewDustPerfect(
                    Projectile.Center,
                    DustID.Smoke,
                    velocity,
                    100,
                    new Color(150, 150, 150),
                    Main.rand.NextFloat(1.2f, 1.8f)
                );
                dust.noGravity = true;
            }

            //着陆音效
            SoundEngine.PlaySound(SoundID.Dig with { Volume = 0.5f, Pitch = -0.3f }, Projectile.Center);
        }

        private void SpawnWarningPulse() {
            //红色警告脉冲
            for (int i = 0; i < 8; i++) {
                float angle = MathHelper.TwoPi * i / 8f;
                Vector2 velocity = angle.ToRotationVector2() * Main.rand.NextFloat(1f, 3f);

                var prt = PRTLoader.NewParticle<PRT_HellFlame>(
                    Projectile.Center + angle.ToRotationVector2() * 30f,
                    velocity,
                    Color.White,
                    Main.rand.NextFloat(0.6f, 1.0f)
                );
                prt.ai[0] = 1; //爆炸扩散模式
                prt.ai[1] = 1.0f;
                prt.ai[2] = 15;
                prt.ai[3] = 30;
            }

            //警告音效
            SoundEngine.PlaySound(SoundID.MaxMana with { Volume = 0.3f, Pitch = 0.8f }, Projectile.Center);
        }

        private void SpawnIdleParticle() {
            //轻微待命粒子
            for (int i = 0; i < 4; i++) {
                float angle = MathHelper.TwoPi * i / 4f + Main.GlobalTimeWrappedHourly;
                Vector2 velocity = angle.ToRotationVector2() * 0.5f;

                var prt = PRTLoader.NewParticle<PRT_HellFlame>(
                    Projectile.Center + angle.ToRotationVector2() * 25f,
                    velocity,
                    Color.White,
                    0.5f
                );
                prt.ai[0] = 3; //环绕模式
                prt.ai[1] = 0.6f;
                prt.ai[2] = 20;
                prt.ai[3] = 40;
            }
        }

        private void Detonate() {
            if (hasDetonated) return;
            hasDetonated = true;

            Projectile.friendly = true;
            Projectile.penetrate = -1;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;

            //生成爆炸弹幕视觉效果
            Projectile.NewProjectile(
                Projectile.GetSource_FromThis(),
                Projectile.Center,
                Vector2.Zero,
                ModContent.ProjectileType<DynamiteExplosionEffect>(),
                0,
                0f,
                Projectile.owner
            );

            //大量爆炸粒子
            SpawnExplosionParticles();

            Projectile.damage = Main.player[Projectile.owner].GetShootState().WeaponDamage * (10 + HalibutData.GetDomainLayer() * 9);//实际爆炸伤害
            Projectile.Explode(350, default, false);

            //爆炸音效
            SoundEngine.PlaySound(SoundID.Item14 with { Volume = 1.3f, Pitch = -0.4f }, Projectile.Center);
            SoundEngine.PlaySound(SoundID.DD2_ExplosiveTrapExplode with { Volume = 1.0f, Pitch = -0.2f }, Projectile.Center);

            //延迟杀死弹幕以确保伤害判定
            Projectile.timeLeft = 3;
        }

        private void SpawnExplosionParticles() {
            //核心爆炸火焰粒子
            for (int i = 0; i < 100; i++) {
                float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                float speed = Main.rand.NextFloat(8f, 25f);
                Vector2 velocity = angle.ToRotationVector2() * speed;

                var prt = PRTLoader.NewParticle<PRT_HellFlame>(
                    Projectile.Center,
                    velocity,
                    Color.White,
                    Main.rand.NextFloat(1.2f, 2.5f)
                );
                prt.ai[0] = 1; //爆炸扩散模式
                prt.ai[1] = 2.0f;
                prt.ai[2] = 40;
                prt.ai[3] = 80;
            }

            //外层烟雾粒子
            for (int i = 0; i < 50; i++) {
                float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                float speed = Main.rand.NextFloat(5f, 15f);
                Vector2 velocity = angle.ToRotationVector2() * speed;

                Dust smoke = Dust.NewDustPerfect(
                    Projectile.Center,
                    DustID.Smoke,
                    velocity,
                    150,
                    new Color(80, 80, 80),
                    Main.rand.NextFloat(2f, 4f)
                );
                smoke.noGravity = true;
                smoke.velocity.Y -= 2f;
            }

            //火焰尘埃
            for (int i = 0; i < 80; i++) {
                float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                float speed = Main.rand.NextFloat(10f, 20f);
                Vector2 velocity = angle.ToRotationVector2() * speed;

                Dust fire = Dust.NewDustPerfect(
                    Projectile.Center,
                    DustID.Torch,
                    velocity,
                    100,
                    new Color(255, 150, 50),
                    Main.rand.NextFloat(1.5f, 2.5f)
                );
                fire.noGravity = true;
            }
        }

        public override bool OnTileCollide(Vector2 oldVelocity) {
            if (State == 0) {
                //碰撞后进入着陆状态
                State = 1;
                DetonationTimer = 0;

                //反弹效果（轻微）
                if (Math.Abs(Projectile.velocity.X - oldVelocity.X) > float.Epsilon) {
                    Projectile.velocity.X = -oldVelocity.X * 0.3f;
                }
                if (Math.Abs(Projectile.velocity.Y - oldVelocity.Y) > float.Epsilon) {
                    Projectile.velocity.Y = -oldVelocity.Y * 0.3f;
                }
            }
            return false;
        }

        public override void OnKill(int timeLeft) {
            Detonate();
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            //对敌人施加火焰debuff
            target.AddBuff(BuffID.OnFire3, 180);
        }

        public override bool PreDraw(ref Color lightColor) {
            Main.instance.LoadItem(ItemID.DynamiteFish);
            Texture2D fishTex = TextureAssets.Item[ItemID.DynamiteFish].Value;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            Rectangle sourceRect = fishTex.Frame(1, 1);
            Vector2 origin = sourceRect.Size() / 2f;

            SpriteEffects effects = Projectile.spriteDirection > 0 ? SpriteEffects.None : SpriteEffects.FlipHorizontally;

            //飞行阶段拖尾
            if (State == 0) {
                for (int i = 0; i < Projectile.oldPos.Length; i++) {
                    if (Projectile.oldPos[i] == Vector2.Zero) continue;

                    float trailAlpha = 1f - i / (float)Projectile.oldPos.Length;
                    Vector2 trailPos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;

                    Main.EntitySpriteDraw(
                        fishTex,
                        trailPos,
                        sourceRect,
                        new Color(255, 100, 50) * (trailAlpha * 0.5f),
                        Projectile.rotation,
                        origin,
                        Projectile.scale * (1f - i * 0.05f),
                        effects,
                        0
                    );
                }
            }

            //主体绘制
            Color mainColor = lightColor;

            //着陆状态的警告脉冲
            if (State == 1) {
                float pulse = (float)Math.Sin(warningPulseTimer * 0.2f) * 0.5f + 0.5f;
                mainColor = Color.Lerp(lightColor, new Color(255, 100, 50), pulse * 0.6f);
            }

            Main.EntitySpriteDraw(
                fishTex,
                drawPos,
                sourceRect,
                mainColor,
                Projectile.rotation,
                origin,
                Projectile.scale,
                effects,
                0
            );

            //着陆状态的发光效果
            if (State == 1 && SoftGlow?.Value != null) {
                float glowPulse = (float)Math.Sin(warningPulseTimer * 0.15f) * 0.5f + 0.5f;
                float glowScale = Projectile.scale * (1.2f + glowPulse * 0.3f);

                Main.EntitySpriteDraw(
                    SoftGlow.Value,
                    drawPos,
                    null,
                    new Color(255, 80, 30, 0) * (0.4f + glowPulse * 0.3f),
                    Projectile.rotation,
                    SoftGlow.Value.Size() / 2f,
                    glowScale,
                    SpriteEffects.None,
                    0
                );
            }

            return false;
        }
    }

    /// <summary>
    /// 雷管鱼爆炸特效弹幕
    /// </summary>
    internal class DynamiteExplosionEffect : ModProjectile
    {
        public override string Texture => CWRConstant.Placeholder;

        private const int EffectDuration = 45; //效果持续时间

        [VaultLoaden(CWRConstant.Masking)]
        private static Asset<Texture2D> SoftGlow = null;

        [VaultLoaden(CWRConstant.Masking)]
        private static Asset<Texture2D> StarTexture = null;

        public override void SetDefaults() {
            Projectile.width = 10;
            Projectile.height = 10;
            Projectile.friendly = false;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = EffectDuration;
            Projectile.alpha = 0;
        }

        public override void AI() {
            Projectile.scale = MathHelper.Lerp(0.5f, 3.0f, 1f - Projectile.timeLeft / (float)EffectDuration);
            Projectile.rotation += 0.3f;

            //强烈照明
            float lightProgress = 1f - Projectile.timeLeft / (float)EffectDuration;
            float lightIntensity = (float)Math.Sin(lightProgress * MathHelper.Pi) * 3f;
            Lighting.AddLight(Projectile.Center,
                2.0f * lightIntensity,
                0.8f * lightIntensity,
                0.3f * lightIntensity);
        }

        public override bool PreDraw(ref Color lightColor) {
            SpriteBatch sb = Main.spriteBatch;
            Vector2 center = Projectile.Center - Main.screenPosition;
            float progress = 1f - Projectile.timeLeft / (float)EffectDuration;
            float scale = Projectile.scale;

            //时间因子
            float time = Main.GlobalTimeWrappedHourly;
            float expandPulse = (float)Math.Sin(progress * MathHelper.Pi);
            float fadePulse = (float)Math.Sin(progress * MathHelper.PiOver2);

            //颜色定义
            Color coreColor = new Color(255, 240, 200, 0);   //核心：亮白黄
            Color innerColor = new Color(255, 180, 80, 0);   //内层：亮橙
            Color midColor = new Color(255, 120, 40, 0);     //中层：橙红
            Color outerColor = new Color(220, 60, 30, 0);    //外层：深红

            //=== 第1层：光束爆发（使用LightBeam） ===
            if (FishDynamite.LightBeam != null && progress < 0.6f) {
                Texture2D beam = FishDynamite.LightBeam;
                float beamAlpha = MathHelper.Lerp(1f, 0f, progress / 0.6f);

                //6条旋转光束
                for (int i = 0; i < 6; i++) {
                    float beamRotation = Projectile.rotation + i * MathHelper.TwoPi / 6f + time * 2f;
                    float beamScale = scale * (0.8f + expandPulse * 0.4f);
                    Color beamColor = Color.Lerp(coreColor, outerColor, i / 6f);
                    beamColor *= beamAlpha * 0.7f;

                    sb.Draw(
                        beam,
                        center,
                        null,
                        beamColor,
                        beamRotation,
                        new Vector2(beam.Width / 2f, beam.Height),
                        beamScale * 0.1f,
                        SpriteEffects.None,
                        0f
                    );
                }
            }

            //=== 第3层：主体火球 ===
            if (SoftGlow?.Value != null) {
                Texture2D glow = SoftGlow.Value;
                float glowScale = scale * (2.0f + expandPulse * 0.3f);

                //外层火焰
                sb.Draw(
                    glow,
                    center,
                    null,
                    outerColor * (fadePulse * 0.6f),
                    Projectile.rotation * 0.5f,
                    glow.Size() / 2f,
                    glowScale,
                    SpriteEffects.None,
                    0f
                );

                //中层
                sb.Draw(
                    glow,
                    center,
                    null,
                    midColor * (fadePulse * 0.8f),
                    -Projectile.rotation * 0.8f,
                    glow.Size() / 2f,
                    glowScale * 0.75f,
                    SpriteEffects.None,
                    0f
                );

                //内层
                sb.Draw(
                    glow,
                    center,
                    null,
                    innerColor * (fadePulse * 0.9f),
                    Projectile.rotation * 1.2f,
                    glow.Size() / 2f,
                    glowScale * 0.5f,
                    SpriteEffects.None,
                    0f
                );
            }

            //=== 第4层：核心高亮 ===
            if (SoftGlow?.Value != null && progress < 0.5f) {
                Texture2D core = SoftGlow.Value;
                float coreScale = scale * (0.8f + (float)Math.Sin(progress * MathHelper.Pi * 2f) * 0.4f);
                float coreAlpha = MathHelper.Lerp(1f, 0f, progress / 0.5f);

                sb.Draw(
                    core,
                    center,
                    null,
                    Color.White with { A = 0 } * coreAlpha,
                    Projectile.rotation * 2f,
                    core.Size() / 2f,
                    coreScale * 0.6f,
                    SpriteEffects.None,
                    0f
                );

                sb.Draw(
                    core,
                    center,
                    null,
                    coreColor * coreAlpha,
                    -Projectile.rotation * 2.5f,
                    core.Size() / 2f,
                    coreScale * 0.4f,
                    SpriteEffects.None,
                    0f
                );
            }

            //=== 第5层：星形闪光 ===
            if (StarTexture?.Value != null && progress < 0.4f) {
                Texture2D star = StarTexture.Value;
                float starAlpha = MathHelper.Lerp(1f, 0f, progress / 0.4f);

                for (int i = 0; i < 2; i++) {
                    float starRot = Projectile.rotation * (3f + i) + time * (4f + i * 2f);
                    float starScale = scale * (0.6f + i * 0.2f);

                    sb.Draw(
                        star,
                        center,
                        null,
                        coreColor * (starAlpha * (0.8f - i * 0.2f)),
                        starRot,
                        star.Size() / 2f,
                        starScale,
                        SpriteEffects.None,
                        0f
                    );
                }
            }

            //=== 第6层：外围粒子环 ===
            if (SoftGlow?.Value != null) {
                Texture2D particle = SoftGlow.Value;
                int particleCount = 16;
                float ringRadius = scale * 120f * expandPulse;

                for (int i = 0; i < particleCount; i++) {
                    float angle = MathHelper.TwoPi * i / particleCount + Projectile.rotation;
                    Vector2 particlePos = center + angle.ToRotationVector2() * ringRadius;
                    float particleScale = scale * 0.4f * fadePulse;

                    sb.Draw(
                        particle,
                        particlePos,
                        null,
                        outerColor * (fadePulse * 0.5f),
                        angle,
                        particle.Size() / 2f,
                        particleScale,
                        SpriteEffects.None,
                        0f
                    );
                }
            }

            return false;
        }
    }
}
