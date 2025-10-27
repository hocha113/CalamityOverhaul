using InnoVault.GameContent.BaseEntity;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.HalibutLegend.FishSkills
{
    internal class FishPrismite : FishSkill
    {
        public override int UnlockFishID => ItemID.Prismite;
        public override int DefaultCooldown => 40; //较短的冷却时间以保持流畅性

        public override bool? Shoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source
            , Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            if (Cooldown > 0) {
                return null;
            }

            //发射主要的七彩矿石冲击波
            Vector2 shootVel = velocity.SafeNormalize(Vector2.UnitX) * 18f;
            
            //根据等级生成多个初始弹幕
            int proj = Projectile.NewProjectile(
                source,
                position,
                shootVel,
                ModContent.ProjectileType<PrismiteWaveProjectile>(),
                (int)(damage * 1.5f),
                knockback * 1.2f,
                player.whoAmI,
                0, //ai[0]: 代数（generation）
                0  //ai[1]: 颜色种子
            );

            if (proj >= 0 && proj < Main.maxProjectiles) {
                Main.projectile[proj].ai[1] = Main.rand.Next(7); //随机初始颜色
            }

            SetCooldown();
            SoundEngine.PlaySound(SoundID.Item105 with { Volume = 0.7f, Pitch = 0.3f }, position);
            
            return false;
        }
    }

    /// <summary>
    /// 七彩矿石冲击波弹幕
    /// </summary>
    internal class PrismiteWaveProjectile : BaseHeldProj
    {
        public override string Texture => CWRConstant.Placeholder;

        private const int MaxGeneration = 3; //最多分裂3代
        private const int MaxLifeTime = 180; //3秒生命周期
        
        private float scale = 1f;
        private readonly List<Vector2> trailPositions = new();
        private const int MaxTrailLength = 20;
        
        //七彩颜色方案 - 精心调配的渐变色
        private static readonly Color[] PrismColors = new Color[]
        {
            new Color(255, 100, 150), //玫瑰红
            new Color(255, 180, 80),  //橙金色
            new Color(255, 240, 100), //明黄色
            new Color(150, 255, 150), //薄荷绿
            new Color(100, 200, 255), //天蓝色
            new Color(180, 120, 255), //紫罗兰
            new Color(255, 120, 220)  //品红色
        };

        private Color primaryColor;
        private Color secondaryColor;
        private int generation;
        private int colorSeed;
        private float pulsePhase;

        public override void SetDefaults() {
            Projectile.width = 32;
            Projectile.height = 32;
            Projectile.friendly = true;
            Projectile.penetrate = 2 + (int)(HalibutData.GetLevel() / 5f); //根据等级增加穿透
            Projectile.timeLeft = MaxLifeTime;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = false;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 20;
            Projectile.extraUpdates = 2;
        }

        public override void AI() {
            //基础运动
            Projectile.velocity *= 0.99f; //轻微减速
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver4;
            pulsePhase += 0.15f;

            //记录拖尾
            trailPositions.Insert(0, Projectile.Center);
            if (trailPositions.Count > MaxTrailLength) {
                trailPositions.RemoveAt(trailPositions.Count - 1);
            }

            //缩放动画
            float lifeProgress = 1f - Projectile.timeLeft / (float)MaxLifeTime;
            if (lifeProgress < 0.15f) {
                scale = MathHelper.Lerp(0.5f, 1.3f, lifeProgress / 0.15f);
            }
            else if (lifeProgress > 0.85f) {
                scale = MathHelper.Lerp(1.3f, 0.8f, (lifeProgress - 0.85f) / 0.15f);
            }
            else {
                scale = 1.3f + lifeProgress * 0.3f;
            }

            //粒子效果
            if (Main.rand.NextBool(2)) {
                SpawnTrailDust();
            }

            //发光
            Lighting.AddLight(Projectile.Center, primaryColor.ToVector3() * 0.6f);
        }

        public override void Initialize() {
            generation = (int)Projectile.ai[0];
            colorSeed = (int)Projectile.ai[1];
            
            //设置颜色方案
            primaryColor = PrismColors[colorSeed % PrismColors.Length];
            secondaryColor = PrismColors[(colorSeed + 3) % PrismColors.Length]; //互补色

            //根据代数调整尺寸
            Projectile.scale = 1f - generation * 0.15f;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            SplitOnImpact(Projectile.Center);
            SpawnImpactEffect(Projectile.Center);
        }

        public override bool OnTileCollide(Vector2 oldVelocity) {
            //反弹逻辑
            if (Math.Abs(Projectile.velocity.X - oldVelocity.X) > float.Epsilon) {
                Projectile.velocity.X = -oldVelocity.X * 0.85f;
            }
            if (Math.Abs(Projectile.velocity.Y - oldVelocity.Y) > float.Epsilon) {
                Projectile.velocity.Y = -oldVelocity.Y * 0.85f;
            }

            SplitOnImpact(Projectile.Center);
            SpawnImpactEffect(Projectile.Center);
            
            SoundEngine.PlaySound(SoundID.Item27 with { Volume = 0.4f, Pitch = 0.2f }, Projectile.Center);
            
            return false; //不销毁，继续反弹
        }

        private void SplitOnImpact(Vector2 impactPos) {
            //只有在未达到最大代数时才分裂
            if (generation >= 2) {
                return;
            }

            int splitCount = 2 + HalibutData.GetDomainLayer() / 2;
            float baseAngle = Main.rand.NextFloat(MathHelper.TwoPi);
            for (int i = 0; i < splitCount; i++) {
                //均匀分布在圆周上，带点随机性
                float angle = baseAngle + (MathHelper.TwoPi * i / splitCount) + Main.rand.NextFloat(-0.2f, 0.2f);
                Vector2 splitVel = angle.ToRotationVector2() * Main.rand.NextFloat(10f, 14f);
                
                //新的颜色种子
                int newColorSeed = (colorSeed + i + 1) % PrismColors.Length;
                
                Projectile.NewProjectile(
                    Projectile.GetSource_FromThis(),
                    impactPos,
                    splitVel,
                    Projectile.type,
                    (int)(Projectile.damage * 0.65f), //伤害递减
                    Projectile.knockBack * 0.7f,
                    Projectile.owner,
                    generation + 1, //增加代数
                    newColorSeed
                );
            }
        }

        private void SpawnImpactEffect(Vector2 pos) {
            //爆炸式粒子效果
            for (int i = 0; i < 18; i++) {
                float angle = MathHelper.TwoPi * i / 18f;
                Vector2 dustVel = angle.ToRotationVector2() * Main.rand.NextFloat(3f, 8f);
                
                int dustType = Main.rand.Next(new int[] { 
                    DustID.RainbowMk2, 
                    DustID.PinkFairy, 
                    DustID.YellowStarDust 
                });
                
                int dust = Dust.NewDust(pos, 1, 1, dustType, dustVel.X, dustVel.Y, 100, primaryColor, 1.5f);
                Main.dust[dust].noGravity = true;
                Main.dust[dust].fadeIn = 1.3f;
            }
            
            //中心爆裂光
            for (int i = 0; i < 8; i++) {
                int dust = Dust.NewDust(pos, 1, 1, DustID.RainbowTorch, 0, 0, 0, Color.White, 2.0f);
                Main.dust[dust].velocity = Main.rand.NextVector2Circular(5f, 5f);
                Main.dust[dust].noGravity = true;
            }
        }

        private void SpawnTrailDust() {
            Color dustColor = Color.Lerp(primaryColor, secondaryColor, (float)Math.Sin(pulsePhase) * 0.5f + 0.5f);
            int dustType = Main.rand.NextBool() ? DustID.RainbowMk2 : DustID.PinkFairy;
            
            int dust = Dust.NewDust(Projectile.position, Projectile.width, Projectile.height, 
                dustType, 0, 0, 100, dustColor, 0.8f);
            Main.dust[dust].velocity *= 0.3f;
            Main.dust[dust].noGravity = true;
            Main.dust[dust].fadeIn = 0.9f;
        }

        public override bool PreDraw(ref Color lightColor) {
            DrawTrail();
            DrawPrismiteCore();
            return false;
        }

        private void DrawTrail() {
            if (trailPositions.Count < 2) {
                return;
            }

            Texture2D trailTex = VaultAsset.placeholder2.Value;
            
            for (int i = 0; i < trailPositions.Count - 1; i++) {
                float progress = i / (float)trailPositions.Count;
                float trailAlpha = (1f - progress);
                
                Vector2 start = trailPositions[i];
                Vector2 end = trailPositions[i + 1];
                Vector2 diff = end - start;
                float length = diff.Length();
                
                if (length < 0.1f) {
                    continue;
                }
                
                float trailRotation = diff.ToRotation();
                float width = scale * (12f - progress * 8f) * Projectile.scale;
                
                //颜色渐变
                Color trailColor = Color.Lerp(secondaryColor, primaryColor, progress);
                trailColor *= trailAlpha;
                
                Main.spriteBatch.Draw(
                    trailTex,
                    start - Main.screenPosition,
                    new Rectangle(0, 0, 1, 1),
                    trailColor,
                    trailRotation,
                    Vector2.Zero,
                    new Vector2(length, width),
                    SpriteEffects.None,
                    0f
                );
            }
        }

        private void DrawPrismiteCore() {
            //加载Prismite物品纹理
            Main.instance.LoadItem(ItemID.Prismite);
            Texture2D prismTex = Terraria.GameContent.TextureAssets.Item[ItemID.Prismite].Value;
            
            float pulse = (float)Math.Sin(pulsePhase) * 0.5f + 0.5f;
            float drawScale = scale * Projectile.scale * 0.8f;
            
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            Rectangle sourceRect = prismTex.Bounds;
            Vector2 origin = sourceRect.Size() * 0.5f;
            
            //多层绘制创造发光效果
            //外层辉光 - 次要颜色
            for (int i = 0; i < 3; i++) {
                float glowRotation = Projectile.rotation + i * MathHelper.TwoPi / 3f;
                float glowScale = drawScale * (1.4f + i * 0.15f);
                Color glowColor = secondaryColor * (0.3f - i * 0.08f);
                
                Main.spriteBatch.Draw(
                    prismTex,
                    drawPos,
                    sourceRect,
                    glowColor,
                    glowRotation,
                    origin,
                    glowScale,
                    SpriteEffects.None,
                    0f
                );
            }
            
            //中层 - 主要颜色
            Main.spriteBatch.Draw(
                prismTex,
                drawPos,
                sourceRect,
                primaryColor * 0.85f,
                Projectile.rotation,
                origin,
                drawScale * 1.1f,
                SpriteEffects.None,
                0f
            );
            
            //核心 - 白色高光
            Main.spriteBatch.Draw(
                prismTex,
                drawPos,
                sourceRect,
                Color.White * (0.6f + pulse * 0.3f),
                Projectile.rotation * 0.7f,
                origin,
                drawScale * 0.85f,
                SpriteEffects.None,
                0f
            );
            
            //顶层粒子闪光
            if (pulse > 0.7f) {
                Texture2D starTex = CWRAsset.StarTexture.Value;
                float starScale = drawScale * (pulse - 0.7f) * 2.5f;
                Color starColor = Color.Lerp(primaryColor, Color.White, pulse) * 0.5f;
                
                Main.spriteBatch.Draw(
                    starTex,
                    drawPos,
                    null,
                    starColor with { A = 0 },
                    Main.GlobalTimeWrappedHourly * 2f,
                    starTex.Size() / 2f,
                    starScale * 0.4f,
                    SpriteEffects.None,
                    0f
                );
            }
        }
    }
}
