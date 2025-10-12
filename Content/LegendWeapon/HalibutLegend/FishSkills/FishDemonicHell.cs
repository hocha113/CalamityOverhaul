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
    internal class FishDemonicHell : FishSkill
    {
        public override int UnlockFishID => ItemID.DemonicHellfish;
        public override int DefaultCooldown => 60 * 25; //25s
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
            //SetCooldown();
            //在玩家前方生成法阵（与鼠标方向）
            Vector2 dir = (Main.MouseWorld - player.Center).SafeNormalize(Vector2.UnitX * player.direction);
            Vector2 spawnPos = player.Center + dir * 160f; //距离玩家 160
            Projectile.NewProjectile(player.GetSource_ItemUse(item), spawnPos, dir,
                ModContent.ProjectileType<HellRitualCircle>(), 0, 0f, player.whoAmI, ai0: player.direction);
            
            // 生成初始召唤粒子效果
            SpawnSummonParticles(spawnPos);
            
            SoundEngine.PlaySound(SoundID.DD2_EtherianPortalOpen with { Volume = 0.8f, Pitch = -0.7f }, spawnPos);
            SoundEngine.PlaySound(SoundID.Item74 with { Volume = 0.6f, Pitch = -0.4f }, spawnPos);
        }

        private void SpawnSummonParticles(Vector2 position) {
            // 召唤时的地狱火焰涌现效果
            for (int i = 0; i < 30; i++) {
                float angle = MathHelper.TwoPi * i / 30f;
                Vector2 velocity = angle.ToRotationVector2() * Main.rand.NextFloat(2f, 5f);

                var prt = PRTLoader.NewParticle<PRT_HellFlame>(
                    position + angle.ToRotationVector2() * Main.rand.NextFloat(50f, 100f),
                    velocity,
                    Color.White,
                    0.8f
                );
                prt.ai[0] = 3;
                prt.ai[1] = 1.2f;
            }
        }
    }

    /// <summary>
    /// 地狱法阵，充能并发射地狱炎爆
    /// </summary>
    internal class HellRitualCircle : ModProjectile
    {
        public override string Texture => CWRConstant.Placeholder;
        private Player Owner => Main.player[Projectile.owner];
        private ref float ChargeTimer => ref Projectile.ai[0];
        private const int ChargeTime = 60; //1s 充能
        private const int FadeTime = 20; //消散
        private float progress => MathHelper.Clamp(ChargeTimer / ChargeTime, 0f, 1f);
        
        [VaultLoaden(CWRConstant.Masking)]
        private static Asset<Texture2D> StarTexture = null;
        
        [VaultLoaden(CWRConstant.Masking)]
        private static Asset<Texture2D> SoftGlow = null;
        
        [VaultLoaden(CWRConstant.Masking)]
        private static Asset<Texture2D> Extra_193 = null;

        public override void SetDefaults() {
            Projectile.width = 300;
            Projectile.height = 300;
            Projectile.friendly = false;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = ChargeTime + FadeTime + 2;
        }
        
        public override void AI() {
            if (!Owner.active) { 
                Projectile.Kill(); 
                return; 
            }
            
            ChargeTimer++;
            
            // 充能过程中持续生成粒子
            if (ChargeTimer < ChargeTime) {
                SpawnChargeParticles();
            }
            
            if (ChargeTimer == ChargeTime) {
                FireBlast();
            }
            
            // 照明效果
            float lightIntensity = progress * 2.5f;
            Lighting.AddLight(Projectile.Center, 
                1.2f * lightIntensity, 
                0.4f * lightIntensity, 
                0.2f * lightIntensity);
        }

        private void SpawnChargeParticles() {
            // 每3帧生成一组粒子
            if (Main.rand.NextBool(3)) {
                // 向心聚集的地狱火焰
                float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                float distance = Main.rand.NextFloat(150f, 250f);
                Vector2 spawnPos = Projectile.Center + angle.ToRotationVector2() * distance;
                Vector2 velocity = (Projectile.Center - spawnPos).SafeNormalize(Vector2.Zero) * Main.rand.NextFloat(3f, 6f);
                
                var prt = PRTLoader.NewParticle<PRT_HellFlame>(
                    spawnPos,
                    velocity,
                    Color.White,
                    Main.rand.NextFloat(0.6f, 1.2f)
                );
                prt.ai[0] = 0;
                prt.ai[1] = 1.0f;
                prt.ai[2] = 40;
                prt.ai[3] = 80;
            }
            
            // 法阵边缘的火焰环
            if (Main.rand.NextBool(2)) {
                float ringAngle = Main.rand.NextFloat(MathHelper.TwoPi);
                float ringRadius = 140f + (float)Math.Sin(Main.GlobalTimeWrappedHourly * 3f) * 10f;
                Vector2 ringPos = Projectile.Center + ringAngle.ToRotationVector2() * ringRadius;
                
                var prt = PRTLoader.NewParticle<PRT_HellFlame>(
                    ringPos,
                    Main.rand.NextVector2Circular(1f, 1f),
                    Color.White,
                    Main.rand.NextFloat(0.5f, 0.9f)
                );
                prt.ai[0] = 3;
                prt.ai[1] = 0.8f;
                prt.ai[2] = 30;
                prt.ai[3] = 60;
            }
        }

        private void FireBlast() {
            // 发射主爆炸弹幕
            Vector2 dir = (Main.MouseWorld - Projectile.Center).SafeNormalize(Vector2.UnitY);
            int damage = (int)(Owner.GetShootState().WeaponDamage * 6.5f);
            Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center, dir * 6f,
                ModContent.ProjectileType<HellFireBlast>(), damage, 6f, Owner.whoAmI);
            
            // 发射时的粒子爆发
            for (int i = 0; i < 50; i++) {
                float angle = MathHelper.TwoPi * i / 50f;
                Vector2 velocity = angle.ToRotationVector2() * Main.rand.NextFloat(4f, 10f);

                var prt = PRTLoader.NewParticle<PRT_HellFlame>(
                    Projectile.Center,
                    velocity,
                    Color.White,
                    Main.rand.NextFloat(1.0f, 1.6f)
                );
                prt.ai[0] = 1;
                prt.ai[1] = 1.5f;
                prt.ai[2] = 50;
                prt.ai[3] = 100;
            }
            
            // 环形冲击尘埃
            for (int i = 0; i < 80; i++) {
                float ang = MathHelper.TwoPi * i / 80f;
                Vector2 vel = ang.ToRotationVector2() * Main.rand.NextFloat(6f, 14f);
                Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.Torch, vel, 150, new Color(255, 100, 20), Main.rand.NextFloat(1.3f, 2.2f));
                d.noGravity = true; 
                d.fadeIn = 1.2f;
            }
            
            SoundEngine.PlaySound(SoundID.DD2_BetsyFireballImpact with { Volume = 0.9f, Pitch = -0.2f }, Projectile.Center);
            SoundEngine.PlaySound(SoundID.Item62 with { Volume = 0.7f, Pitch = 0.2f }, Projectile.Center);
        }

        public override bool PreDraw(ref Color lightColor) {
            SpriteBatch sb = Main.spriteBatch;
            Texture2D pixel = TextureAssets.MagicPixel.Value;
            Vector2 center = Projectile.Center - Main.screenPosition;
            
            float baseScale = 1f + (float)Math.Sin(Main.GlobalTimeWrappedHourly * 4f) * 0.05f;
            float glow = (float)Math.Sin(Main.GlobalTimeWrappedHourly * 6f) * 0.5f + 0.5f;
            float p = progress;

            // 基础颜色方案
            Color hellCore = new Color(255, 200, 80);      // 明亮核心
            Color hellMid = new Color(255, 90, 30);        // 中层橙红
            Color hellEdge = new Color(200, 40, 20);       // 边缘深红
            Color hellDark = new Color(120, 20, 40);       // 暗紫红

            // 多层旋转圆环（地狱风格）
            DrawHellRing(sb, pixel, center, 160f, 8f, hellEdge, p, 0.8f, 12);
            DrawHellRing(sb, pixel, center, 160f * p, 12f, Color.Lerp(hellCore, hellMid, p) * p, p, 0.4f, 8);
            DrawHellRing(sb, pixel, center, 140f, 3f, hellDark, p, 1.2f, 6);
            DrawHellRing(sb, pixel, center, 120f * p, 6f, Color.Lerp(hellCore, hellEdge, p * 0.7f) * p, p, -1.0f, 10);

            // 五芒星魔法阵（核心图案）
            DrawPentagram(sb, pixel, center, 100f, 4f, Color.Lerp(hellMid, hellCore, glow) * p, 
                Main.GlobalTimeWrappedHourly * 0.5f);
            DrawPentagram(sb, pixel, center, 85f, 3f, Color.Lerp(hellEdge, hellMid, glow) * p * 0.8f, 
                -Main.GlobalTimeWrappedHourly * 0.7f);

            // 外层符文圆环
            DrawRuneCircle(sb, pixel, center, 130f, p, hellMid, hellEdge);
            
            // 内层复杂几何图案
            DrawHexagon(sb, pixel, center, 70f, 3f, Color.Lerp(hellCore, hellMid, p * 0.5f) * p, 
                Main.GlobalTimeWrappedHourly * 0.6f);
            DrawHexagon(sb, pixel, center, 60f, 2f, hellEdge * p * 0.7f, 
                -Main.GlobalTimeWrappedHourly * 0.8f);

            // 径向地狱符文（使用纹理）
            if (StarTexture?.Value != null) {
                DrawRadialRunes(sb, StarTexture.Value, center, 110f, p, hellCore, hellMid);
            }

            // 内核脉冲（多层辉光）
            DrawCoreGlow(sb, pixel, center, p, baseScale, hellCore, hellMid, hellEdge);

            // 充能完成时的额外闪光
            if (p > 0.9f) {
                DrawChargeFlash(sb, pixel, center, p, hellCore);
            }

            return false;
        }

        private void DrawHellRing(SpriteBatch sb, Texture2D pixel, Vector2 center, float radius, 
            float thickness, Color c, float progress, float rotSpeed, int pulseSegments) {
            int segments = 180;
            float angleStep = MathHelper.TwoPi / segments;
            float rotOffset = Main.GlobalTimeWrappedHourly * rotSpeed;
            
            for (int i = 0; i < segments; i++) {
                float ang = i * angleStep + rotOffset;
                Vector2 pos = center + ang.ToRotationVector2() * radius;
                
                // 分段脉冲效果
                float segmentIndex = (ang / MathHelper.TwoPi) * pulseSegments;
                float pulse = (float)Math.Sin(segmentIndex * MathHelper.Pi + progress * 15f);
                float energyFlow = (float)Math.Sin(ang * 4f - Main.GlobalTimeWrappedHourly * 5f);
                
                float intensity = 0.4f + 0.4f * (pulse * 0.5f + 0.5f) + 0.2f * (energyFlow * 0.5f + 0.5f);
                Color col = c * intensity * progress;
                
                sb.Draw(pixel, pos, new Rectangle(0, 0, 1, 1), col, ang, 
                    Vector2.Zero, new Vector2(thickness, 2f), SpriteEffects.None, 0f);
            }
        }

        private void DrawPentagram(SpriteBatch sb, Texture2D pixel, Vector2 center, float radius, 
            float thickness, Color col, float rot) {
            // 绘制五芒星（经典地狱符号）
            int points = 5;
            Vector2[] vertices = new Vector2[points * 2];
            
            for (int i = 0; i < points; i++) {
                float angle = rot + i * MathHelper.TwoPi / points - MathHelper.PiOver2;
                vertices[i * 2] = center + angle.ToRotationVector2() * radius;
            }
            
            // 连接顶点形成五芒星（每个顶点连接到第二个下一个顶点）
            for (int i = 0; i < points; i++) {
                int next = (i * 2) % points;
                DrawLine(sb, pixel, vertices[i * 2], vertices[next * 2], thickness, col);
            }
        }

        private void DrawHexagon(SpriteBatch sb, Texture2D pixel, Vector2 center, float radius, 
            float thickness, Color col, float rot) {
            DrawPolygon(sb, pixel, center, 6, radius, thickness, col, rot);
        }

        private void DrawRuneCircle(SpriteBatch sb, Texture2D pixel, Vector2 center, float radius, 
            float progress, Color innerCol, Color outerCol) {
            int runeCount = 12;
            float runeSize = 8f;
            
            for (int i = 0; i < runeCount; i++) {
                float angle = Main.GlobalTimeWrappedHourly * 0.8f + i * MathHelper.TwoPi / runeCount;
                float pulsePhase = angle * 2f + progress * 10f;
                float pulse = (float)Math.Sin(pulsePhase) * 0.5f + 0.5f;
                
                Vector2 runePos = center + angle.ToRotationVector2() * radius;
                Color runeColor = Color.Lerp(outerCol, innerCol, pulse) * progress;
                
                // 绘制符文形状（简化为菱形）
                DrawDiamond(sb, pixel, runePos, runeSize * (0.8f + pulse * 0.4f), runeColor, angle);
            }
        }

        private void DrawDiamond(SpriteBatch sb, Texture2D pixel, Vector2 center, float size, Color col, float rot) {
            Vector2[] points = new Vector2[4];
            for (int i = 0; i < 4; i++) {
                float angle = rot + i * MathHelper.PiOver2;
                points[i] = center + angle.ToRotationVector2() * size;
            }
            for (int i = 0; i < 4; i++) {
                DrawLine(sb, pixel, points[i], points[(i + 1) % 4], 2f, col);
            }
        }

        private void DrawRadialRunes(SpriteBatch sb, Texture2D runeTex, Vector2 center, float radius, 
            float progress, Color innerCol, Color outerCol) {
            int count = 8;
            for (int i = 0; i < count; i++) {
                float ang = Main.GlobalTimeWrappedHourly * 1.2f + i * MathHelper.TwoPi / count;
                float distOffset = (float)Math.Sin(ang * 3f + progress * 6f) * 12f;
                Vector2 pos = center + ang.ToRotationVector2() * (radius + distOffset * progress);
                
                float colorPhase = (float)Math.Sin(ang * 2f + progress * 10f) * 0.5f + 0.5f;
                Color runeColor = Color.Lerp(outerCol, innerCol, colorPhase) * progress * 0.9f;
                runeColor.A = 0;
                float scale = 0.15f + 0.1f * (float)Math.Sin(ang * 2f + progress * 8f);
                sb.Draw(runeTex, pos, null, runeColor, ang + MathHelper.PiOver2, 
                    runeTex.Size() / 2f, scale, SpriteEffects.None, 0f);
            }
        }

        private void DrawCoreGlow(SpriteBatch sb, Texture2D pixel, Vector2 center, float progress, 
            float baseScale, Color core, Color mid, Color edge) {
            // 多层核心辉光
            for (int i = 0; i < 4; i++) {
                float layerScale = (0.2f + i * 0.15f + progress * 0.3f) * baseScale;
                float layerIntensity = 0.6f - i * 0.12f;
                Color layerColor = i < 2 ? Color.Lerp(core, mid, i / 2f) : Color.Lerp(mid, edge, (i - 2) / 2f);
                
                float pulse = (float)Math.Sin(Main.GlobalTimeWrappedHourly * (8f - i * 1.5f)) * 0.15f + 0.85f;
                
                sb.Draw(pixel, center, new Rectangle(0, 0, 1, 1), 
                    layerColor * (layerIntensity * progress * pulse),
                    0f, Vector2.Zero, 
                    new Vector2(250f * layerScale, 250f * layerScale * 0.8f), 
                    SpriteEffects.None, 0f);
            }
        }

        private void DrawChargeFlash(SpriteBatch sb, Texture2D pixel, Vector2 center, float progress, Color core) {
            // 充能完成的强烈闪光
            float flashIntensity = (progress - 0.9f) / 0.1f;
            float flashPulse = (float)Math.Sin(flashIntensity * MathHelper.Pi);
            
            sb.Draw(pixel, center, new Rectangle(0, 0, 1, 1), 
                core * (flashPulse * 0.8f),
                0f, Vector2.Zero, 
                new Vector2(400f, 400f) * flashPulse, 
                SpriteEffects.None, 0f);
        }

        private void DrawPolygon(SpriteBatch sb, Texture2D pixel, Vector2 center, int sides, 
            float radius, float thickness, Color col, float rot) {
            if (sides < 3) return;
            Vector2 prev = center + (rot).ToRotationVector2() * radius;
            for (int i = 1; i <= sides; i++) {
                float ang = rot + i * MathHelper.TwoPi / sides;
                Vector2 curr = center + ang.ToRotationVector2() * radius;
                DrawLine(sb, pixel, prev, curr, thickness, col);
                prev = curr;
            }
        }

        private void DrawLine(SpriteBatch sb, Texture2D pixel, Vector2 start, Vector2 end, 
            float thickness, Color col) {
            Vector2 diff = end - start;
            float len = diff.Length();
            float rot = diff.ToRotation();
            sb.Draw(pixel, start, new Rectangle(0, 0, 1, 1), col, rot, 
                Vector2.Zero, new Vector2(len, thickness), SpriteEffects.None, 0f);
        }
    }

    /// <summary>
    /// 地狱炎爆弹幕：向前飞行后在较小延迟内爆炸，生成大量火焰粒子
    /// </summary>
    internal class HellFireBlast : ModProjectile
    {
        public override string Texture => CWRConstant.Placeholder;
        private const int FlyTime = 24;
        
        [VaultLoaden(CWRConstant.Masking)]
        private static Asset<Texture2D> SoftGlow = null;

        public override void SetDefaults() {
            Projectile.width = 60;
            Projectile.height = 60;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 90;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
        }
        
        public override void AI() {
            if (Projectile.timeLeft == 90) {
                // 初始爆闪
                SpawnInitialBurst();
            }
            
            float life = 90 - Projectile.timeLeft;
            
            if (life < FlyTime) {
                // 飞行阶段
                Projectile.scale = 0.6f + life / FlyTime * 0.8f;
                Projectile.velocity *= 1.02f;
                
                // 飞行轨迹粒子
                if (Main.rand.NextBool(2)) {
                    SpawnTrailParticle();
                }
            }
            else {
                // 减速并扩散
                Projectile.velocity *= 0.94f;
                Projectile.scale *= 1.01f;
                
                // 预爆炸粒子
                if (Main.rand.NextBool(3)) {
                    SpawnPreExplosionParticle();
                }
                
                if (Projectile.timeLeft == 40) {
                    Explode();
                }
            }
            
            Lighting.AddLight(Projectile.Center, 1.6f, 0.6f, 0.2f);
        }

        private void SpawnInitialBurst() {
            for (int i = 0; i < 30; i++) {
                Vector2 v = Main.rand.NextVector2Circular(8f, 8f);
                var d = Dust.NewDustPerfect(Projectile.Center, DustID.Torch, v, 150, 
                    new Color(255, 120, 40), Main.rand.NextFloat(1.2f, 1.8f));
                d.noGravity = true;
            }
            
            // 初始地狱火焰粒子
            for (int i = 0; i < 12; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(4f, 4f);
                var prt = PRTLoader.NewParticle<PRT_HellFlame>(
                    Projectile.Center,
                    vel,
                    Color.White,
                    Main.rand.NextFloat(0.8f, 1.2f)
                );
                prt.ai[0] = 1;
                prt.ai[1] = 1.3f;
            }
        }

        private void SpawnTrailParticle() {
            var prt = PRTLoader.NewParticle<PRT_HellFlame>(
                Projectile.Center + Main.rand.NextVector2Circular(10f, 10f),
                -Projectile.velocity * 0.3f + Main.rand.NextVector2Circular(1f, 1f),
                Color.White,
                Main.rand.NextFloat(0.6f, 1.0f)
            );
            prt.ai[0] = 2;
            prt.ai[1] = 1.0f;
            prt.ai[2] = 30;
            prt.ai[3] = 60;
        }

        private void SpawnPreExplosionParticle() {
            var prt = PRTLoader.NewParticle<PRT_HellFlame>(
                Projectile.Center + Main.rand.NextVector2Circular(20f, 20f),
                Main.rand.NextVector2Circular(2f, 2f),
                Color.White,
                Main.rand.NextFloat(0.8f, 1.4f)
            );
            prt.ai[0] = 0;
            prt.ai[1] = 1.2f;
            prt.ai[2] = 20;
            prt.ai[3] = 40;
        }

        private void Explode() {
            // 伤害区域扩大
            Projectile.width = Projectile.height = 220;
            Projectile.position -= new Vector2(110);
            
            // 大量地狱火焰粒子（主要视觉效果）
            for (int i = 0; i < 80; i++) {
                float ang = Main.rand.NextFloat(MathHelper.TwoPi);
                float spd = Main.rand.NextFloat(4f, 18f);
                Vector2 vel = ang.ToRotationVector2() * spd;
                
                var prt = PRTLoader.NewParticle<PRT_HellFlame>(
                    Projectile.Center,
                    vel,
                    Color.White,
                    Main.rand.NextFloat(1.0f, 2.0f)
                );
                prt.ai[0] = 1;
                prt.ai[1] = 1.8f;
                prt.ai[2] = 60;
                prt.ai[3] = 120;
            }
            
            // 额外的螺旋火焰
            for (int i = 0; i < 30; i++) {
                float ang = Main.rand.NextFloat(MathHelper.TwoPi);
                float spd = Main.rand.NextFloat(6f, 12f);
                Vector2 vel = ang.ToRotationVector2() * spd;

                var prt = PRTLoader.NewParticle<PRT_HellFlame>(
                    Projectile.Center,
                    vel,
                    Color.White,
                    Main.rand.NextFloat(0.8f, 1.5f)
                );
                prt.ai[0] = 2;
                prt.ai[1] = 1.5f;
                prt.ai[2] = 50;
                prt.ai[3] = 100;
            }
            
            // 传统尘埃效果
            for (int i = 0; i < 140; i++) {
                float ang = Main.rand.NextFloat(MathHelper.TwoPi);
                float spd = Main.rand.NextFloat(4f, 18f);
                Vector2 vel = ang.ToRotationVector2() * spd;
                var d = Dust.NewDustPerfect(Projectile.Center, DustID.Torch, vel, 80, 
                    new Color(255, 140, 50), Main.rand.NextFloat(1.4f, 2.4f));
                d.noGravity = true; 
                d.fadeIn = 1.3f;
                
                if (i % 6 == 0) {
                    Dust d2 = Dust.NewDustPerfect(Projectile.Center, DustID.Smoke, vel * 0.4f, 200, 
                        default, Main.rand.NextFloat(1.8f, 3f));
                    d2.velocity.Y -= 1f; 
                    d2.noGravity = true; 
                    d2.color = new Color(60, 20, 10);
                }
            }
            
            SoundEngine.PlaySound(SoundID.Item14 with { Volume = 1.2f, Pitch = -0.5f }, Projectile.Center);
        }
        public override bool PreDraw(ref Color lightColor) {
            SpriteBatch sb = Main.spriteBatch;
            Texture2D pixel = TextureAssets.MagicPixel.Value;
            Vector2 center = Projectile.Center - Main.screenPosition;
            
            float pulse = (float)Math.Sin(Main.GlobalTimeWrappedHourly * 12f) * 0.5f + 0.5f;
            float scale = Projectile.scale;
            
            // 多层辉光绘制
            Color coreColor = new Color(255, 200, 80, 0);
            Color midColor = new Color(255, 100, 30, 0);
            Color edgeColor = new Color(200, 40, 20, 0);
            
            // 外层扩散
            for (int i = 0; i < 5; i++) {
                float rot = MathHelper.TwoPi * i / 5f + Main.GlobalTimeWrappedHourly * 0.4f;
                Vector2 off = rot.ToRotationVector2() * 18f * scale;
                sb.Draw(pixel, center + off, new Rectangle(0, 0, 1, 1), 
                    edgeColor * 0.4f, 0f, Vector2.Zero, 
                    new Vector2(180f * scale, 180f * scale), SpriteEffects.None, 0f);
            }
            
            // 中层火焰
            sb.Draw(pixel, center, new Rectangle(0, 0, 1, 1), 
                midColor * 0.7f, 0f, Vector2.Zero, 
                new Vector2(140f * scale, 140f * scale), SpriteEffects.None, 0f);
            
            // 核心高亮
            sb.Draw(pixel, center, new Rectangle(0, 0, 1, 1), 
                coreColor * 0.9f, 0f, Vector2.Zero, 
                new Vector2(100f * scale * (1f + pulse * 0.2f), 100f * scale * (1f + pulse * 0.2f)), 
                SpriteEffects.None, 0f);
            
            // 旋转光束
            for (int i = 0; i < 4; i++) {
                float beamRot = Main.GlobalTimeWrappedHourly * 2f + i * MathHelper.PiOver2;
                Vector2 beamScale = new Vector2(120f * scale, 8f * scale);
                sb.Draw(pixel, center, new Rectangle(0, 0, 1, 1), 
                    coreColor * (0.5f * pulse), beamRot, Vector2.Zero, 
                    beamScale, SpriteEffects.None, 0f);
            }
            
            return false;
        }
        
        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            // 附加地狱火灼烧
            target.AddBuff(BuffID.OnFire3, 300);
        }
    }
}
