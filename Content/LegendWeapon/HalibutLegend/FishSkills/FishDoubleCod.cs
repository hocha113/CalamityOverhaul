using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.HalibutLegend.FishSkills
{
    /// <summary>
    /// 双鳍鳕鱼技能，开火时周期性发射游动的鳕鱼
    /// </summary>
    internal class FishDoubleCod : FishSkill
    {
        public override int UnlockFishID => ItemID.DoubleCod;
        public override int DefaultCooldown => 18;
        public override int ResearchDuration => 60 * 15;

        private int shootCounter = 0;
        private static int ShootInterval => 6 - HalibutData.GetDomainLayer() / 4; //每6-4次开火触发一次

        public override bool? Shoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source,
            Vector2 position, Vector2 velocity, int type, int damage, float knockback) {

            shootCounter++;

            if (shootCounter >= ShootInterval && Cooldown <= 0) {
                shootCounter = 0;
                SetCooldown();

                //发射游动的鳕鱼
                SpawnDoubleCodFish(player, source, position, velocity, damage, knockback);
            }

            return null;
        }

        private void SpawnDoubleCodFish(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int damage, float knockback) {
            //发射数量随领域层数增加
            int fishCount = 2 + HalibutData.GetDomainLayer() / 4;

            for (int i = 0; i < fishCount; i++) {
                //扇形散射
                float spreadAngle = MathHelper.Lerp(-0.25f, 0.25f, i / (float)(fishCount - 1));
                Vector2 fishVelocity = velocity.RotatedBy(spreadAngle) * Main.rand.NextFloat(0.8f, 1.2f);

                int codProj = Projectile.NewProjectile(
                    source,
                    position,
                    fishVelocity,
                    ModContent.ProjectileType<DoubleCodProjectile>(),
                    (int)(damage * (0.6f + HalibutData.GetDomainLayer() * 0.15f)),
                    knockback * 0.8f,
                    player.whoAmI,
                    ai0: i / (float)fishCount //颜色偏移
                );

                if (codProj >= 0) {
                    Main.projectile[codProj].netUpdate = true;
                }
            }

            //发射音效
            SoundEngine.PlaySound(SoundID.Item8 with {
                Volume = 0.5f,
                Pitch = 0.2f
            }, position);

            SoundEngine.PlaySound(SoundID.Splash with {
                Volume = 0.4f,
                Pitch = 0.3f
            }, position);

            //发射水花效果
            SpawnLaunchEffect(position, velocity);
        }

        private void SpawnLaunchEffect(Vector2 position, Vector2 direction) {
            //水花飞溅
            for (int i = 0; i < 10; i++) {
                Vector2 velocity = direction.RotatedByRandom(0.5f) * Main.rand.NextFloat(0.3f, 0.8f);
                Dust splash = Dust.NewDustPerfect(
                    position,
                    DustID.Water,
                    velocity,
                    100,
                    new Color(100, 180, 255),
                    Main.rand.NextFloat(1.2f, 2f)
                );
                splash.noGravity = true;
                splash.fadeIn = 1.2f;
            }

            //蓝色水雾
            for (int i = 0; i < 8; i++) {
                BasePRT water = new PRT_Light(
                    position,
                    Main.rand.NextVector2Circular(4f, 4f),
                    0.6f,
                    new Color(120, 200, 255),
                    20,
                    1f,
                    hueShift: 0.01f
                );
                PRTLoader.AddParticle(water);
            }
        }
    }

    /// <summary>
    /// 双鳍鳕鱼弹幕
    /// </summary>
    internal class DoubleCodProjectile : ModProjectile
    {
        public override string Texture => "Terraria/Images/Item_" + ItemID.DoubleCod;

        private ref float Timer => ref Projectile.ai[1];

        private float swimPhase = 0f;
        private float bodyWave = 0f;
        private readonly List<Vector2> trailPositions = new();
        private const int MaxTrailLength = 15;

        //游动参数
        private const float MaxSpeed = 16f;
        private const float TurnSpeed = 0.03f;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 12;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 36;
            Projectile.height = 36;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Generic;
            Projectile.penetrate = 4;
            Projectile.timeLeft = 240;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.alpha = 0;
            Projectile.extraUpdates = 1;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
        }

        public override void AI() {
            Timer++;
            swimPhase += 0.3f;
            bodyWave += 0.25f;

            //游动的波浪运动
            Vector2 perpendicular = new Vector2(-Projectile.velocity.Y, Projectile.velocity.X).SafeNormalize(Vector2.Zero);
            float wave = (float)Math.Sin(swimPhase) * 1.5f;
            Projectile.velocity += perpendicular * wave * 0.08f;

            //轻微追踪最近的敌人
            if (Timer % 20 == 0) {
                NPC target = Projectile.Center.FindClosestNPC(500f);
                if (target != null) {
                    Vector2 toTarget = target.Center - Projectile.Center;
                    Vector2 desiredVelocity = toTarget.SafeNormalize(Vector2.Zero) * MaxSpeed;
                    Projectile.velocity = Vector2.Lerp(Projectile.velocity, desiredVelocity, TurnSpeed * 8f);
                }
            }

            //速度限制
            if (Projectile.velocity.Length() > MaxSpeed) {
                Projectile.velocity = Projectile.velocity.SafeNormalize(Vector2.Zero) * MaxSpeed;
            }

            //旋转朝向
            if (Projectile.velocity.LengthSquared() > 1f) {
                Projectile.rotation = MathHelper.Lerp(
                    Projectile.rotation,
                    Projectile.velocity.ToRotation() + MathHelper.PiOver4,
                    0.2f
                );
            }

            //更新拖尾
            UpdateTrail();

            //蓝色光照
            Lighting.AddLight(Projectile.Center, 0.3f, 0.5f, 0.8f);

            //游动气泡粒子
            if (Main.rand.NextBool(6)) {
                SpawnSwimBubble();
            }
        }

        private void UpdateTrail() {
            trailPositions.Insert(0, Projectile.Center);
            if (trailPositions.Count > MaxTrailLength) {
                trailPositions.RemoveAt(trailPositions.Count - 1);
            }
        }

        private void SpawnSwimBubble() {
            Dust bubble = Dust.NewDustPerfect(
                Projectile.Center + Main.rand.NextVector2Circular(12f, 12f),
                DustID.Water,
                -Projectile.velocity * 0.2f + Main.rand.NextVector2Circular(1f, 1f),
                100,
                new Color(150, 200, 255),
                Main.rand.NextFloat(0.8f, 1.3f)
            );
            bubble.noGravity = true;
            bubble.fadeIn = 1f;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            //击中水花
            for (int i = 0; i < 12; i++) {
                Vector2 velocity = Main.rand.NextVector2Circular(6f, 6f);
                Dust splash = Dust.NewDustPerfect(
                    Projectile.Center,
                    DustID.Water,
                    velocity,
                    100,
                    new Color(120, 200, 255),
                    Main.rand.NextFloat(1.5f, 2.5f)
                );
                splash.noGravity = true;
            }

            //蓝色能量爆发
            for (int i = 0; i < 8; i++) {
                float angle = MathHelper.TwoPi * i / 8f;
                Vector2 velocity = angle.ToRotationVector2() * 5f;
                Color waterColor = new Color(100, 180, 255);

                BasePRT hit_effect = new PRT_Light(
                    Projectile.Center,
                    velocity,
                    0.7f,
                    waterColor,
                    20,
                    1f,
                    hueShift: 0.02f
                );
                PRTLoader.AddParticle(hit_effect);
            }

            SoundEngine.PlaySound(SoundID.NPCHit25 with {
                Volume = 0.5f,
                Pitch = 0.3f
            }, Projectile.Center);

            //减少穿透
            Projectile.penetrate--;
            if (Projectile.penetrate <= 0) {
                Projectile.Kill();
            }
        }

        public override bool OnTileCollide(Vector2 oldVelocity) {
            //反弹
            if (Math.Abs(Projectile.velocity.X - oldVelocity.X) > float.Epsilon) {
                Projectile.velocity.X = -oldVelocity.X * 0.8f;
            }
            if (Math.Abs(Projectile.velocity.Y - oldVelocity.Y) > float.Epsilon) {
                Projectile.velocity.Y = -oldVelocity.Y * 0.8f;
            }

            Projectile.penetrate--;
            if (Projectile.penetrate <= 0) {
                Projectile.Kill();
            }

            SoundEngine.PlaySound(SoundID.Item10 with {
                Volume = 0.4f,
                Pitch = 0.2f
            }, Projectile.Center);

            //反弹水花
            for (int i = 0; i < 5; i++) {
                Dust splash = Dust.NewDustPerfect(
                    Projectile.Center,
                    DustID.Water,
                    Main.rand.NextVector2Circular(3f, 3f),
                    100,
                    new Color(120, 200, 255),
                    Main.rand.NextFloat(1f, 1.5f)
                );
                splash.noGravity = true;
            }

            return false;
        }

        public override void OnKill(int timeLeft) {
            //消失水花
            for (int i = 0; i < 20; i++) {
                Vector2 velocity = Main.rand.NextVector2Circular(8f, 8f);
                Dust splash = Dust.NewDustPerfect(
                    Projectile.Center,
                    DustID.Water,
                    velocity,
                    100,
                    new Color(120, 200, 255),
                    Main.rand.NextFloat(2f, 3f)
                );
                splash.noGravity = Main.rand.NextBool();
            }

            //蓝色能量爆发
            for (int i = 0; i < 15; i++) {
                Vector2 velocity = Main.rand.NextVector2Circular(6f, 6f);
                Color waterColor = new Color(100, 180, 255);

                BasePRT burst = new PRT_Light(
                    Projectile.Center,
                    velocity,
                    Main.rand.NextFloat(0.6f, 1f),
                    waterColor,
                    25,
                    1f,
                    hueShift: 0.02f
                );
                PRTLoader.AddParticle(burst);
            }

            SoundEngine.PlaySound(SoundID.Splash with {
                Volume = 0.6f,
                Pitch = 0.3f
            }, Projectile.Center);
        }

        public override bool PreDraw(ref Color lightColor) {
            SpriteBatch sb = Main.spriteBatch;
            Texture2D fishTex = TextureAssets.Item[ItemID.DoubleCod].Value;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            Vector2 origin = fishTex.Size() / 2f;

            //绘制游动拖尾
            DrawSwimTrail(sb, fishTex, origin);

            //绘制水流光晕
            for (int i = 0; i < 3; i++) {
                float glowScale = Projectile.scale * (1.1f + i * 0.1f);
                float glowAlpha = (1f - i * 0.3f) * 0.4f;
                Color glowColor = new Color(100, 180, 255, 0);

                sb.Draw(
                    fishTex,
                    drawPos,
                    null,
                    glowColor * glowAlpha,
                    Projectile.rotation,
                    origin,
                    glowScale,
                    SpriteEffects.None,
                    0
                );
            }

            //主体绘制蓝色水生色调
            Color mainColor = Color.Lerp(
                lightColor,
                new Color(150, 200, 255),
                0.5f
            );

            //身体波浪效果
            float bodyWaveScale = 1f + (float)Math.Sin(bodyWave) * 0.08f;
            Vector2 scale = new Vector2(Projectile.scale, Projectile.scale * bodyWaveScale);

            sb.Draw(
                fishTex,
                drawPos,
                null,
                mainColor,
                Projectile.rotation,
                origin,
                scale,
                SpriteEffects.None,
                0
            );

            //水流光效
            float shimmer = 0.4f + (float)Math.Sin(swimPhase * 0.7f) * 0.3f;
            sb.Draw(
                fishTex,
                drawPos,
                null,
                new Color(180, 220, 255, 0) * shimmer,
                Projectile.rotation,
                origin,
                scale * 1.05f,
                SpriteEffects.None,
                0
            );

            //白色高光（鳞片反光）
            sb.Draw(
                fishTex,
                drawPos,
                null,
                Color.White * 0.3f,
                Projectile.rotation,
                origin,
                scale * 0.9f,
                SpriteEffects.None,
                0
            );

            return false;
        }

        private void DrawSwimTrail(SpriteBatch sb, Texture2D texture, Vector2 origin) {
            if (trailPositions.Count < 2) return;

            for (int i = 1; i < trailPositions.Count; i++) {
                float progress = 1f - i / (float)trailPositions.Count;
                float trailAlpha = progress * 0.5f;
                float trailScale = Projectile.scale * MathHelper.Lerp(0.6f, 1f, progress);

                //水流渐变色
                Color trailColor = Color.Lerp(
                    new Color(80, 140, 200),
                    new Color(150, 200, 255),
                    progress
                ) * trailAlpha;

                Vector2 trailPos = trailPositions[i] - Main.screenPosition;

                //身体波浪
                float waveOffset = (float)Math.Sin(bodyWave - i * 0.2f) * 0.08f;
                Vector2 waveScale = new Vector2(trailScale, trailScale * (1f + waveOffset));

                sb.Draw(
                    texture,
                    trailPos,
                    null,
                    trailColor,
                    Projectile.rotation - i * 0.05f,
                    origin,
                    waveScale,
                    SpriteEffects.None,
                    0
                );
            }

            //绘制水流线条
            DrawWaterTrail(sb, trailPositions);
        }

        private void DrawWaterTrail(SpriteBatch sb, List<Vector2> positions) {
            if (positions.Count < 3) return;

            Texture2D lineTex = VaultAsset.placeholder2.Value;

            for (int i = 0; i < positions.Count - 1; i++) {
                float progress = 1f - i / (float)positions.Count;
                float lineAlpha = progress * 0.3f;

                Vector2 start = positions[i];
                Vector2 end = positions[i + 1];
                Vector2 diff = end - start;
                float length = diff.Length();

                if (length < 0.01f) continue;

                float rotation = diff.ToRotation();
                float width = 2f * progress;

                //水流蓝色
                Color waterColor = new Color(120, 200, 255, 0) * lineAlpha;

                sb.Draw(
                    lineTex,
                    start - Main.screenPosition,
                    new Rectangle(0, 0, 1, 1),
                    waterColor,
                    rotation,
                    Vector2.Zero,
                    new Vector2(length, width),
                    SpriteEffects.None,
                    0f
                );
            }
        }
    }
}
