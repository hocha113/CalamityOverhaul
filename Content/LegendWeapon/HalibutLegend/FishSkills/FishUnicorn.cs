using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.HalibutLegend.FishSkills
{
    internal class FishUnicorn : FishSkill
    {
        public override int UnlockFishID => ItemID.UnicornFish;
        public override int DefaultCooldown => 10 * (15 - HalibutData.GetDomainLayer());
        public override int ResearchDuration => 60 * 14;

        public override bool? AltFunctionUse(Item item, Player player) => true;

        public override bool? CanUseItem(Item item, Player player) {
            if (player.altFunctionUse == 2) {
                //检查冷却
                if (Cooldown > 0) {
                    return false;
                }

                item.UseSound = null;
                Use(item, player);
                return false;
            }
            return null;
        }

        public override void Use(Item item, Player player) {
            //设置冷却
            SetCooldown();

            //计算水平冲刺方向（只考虑X轴方向）
            Vector2 mouseDirection = (Main.MouseWorld - player.Center).SafeNormalize(Vector2.UnitX * player.direction);
            float horizontalDirection = mouseDirection.X > 0 ? 1f : -1f;

            ShootState shootState = player.GetShootState();

            //生成独角兽鱼冲刺弹幕
            Projectile.NewProjectile(
                player.GetSource_ItemUse(item),
                player.Center,
                new Vector2(horizontalDirection * 28f, 0), //纯水平速度
                ModContent.ProjectileType<UnicornFishDashProj>(),
                (int)(shootState.WeaponDamage * (4.5f + HalibutData.GetDomainLayer() * 1.5f)),
                shootState.WeaponKnockback * 2.5f,
                player.whoAmI,
                ai0: 0,
                ai1: horizontalDirection
            );

            //播放冲刺音效
            SoundEngine.PlaySound(SoundID.Item1 with { Volume = 0.9f, Pitch = 0.3f }, player.Center);
            SoundEngine.PlaySound(SoundID.DD2_WyvernDiveDown with { Volume = 0.7f, Pitch = 0.5f }, player.Center);
        }
    }

    /// <summary>
    /// 独角兽鱼冲刺弹幕
    /// </summary>
    internal class UnicornFishDashProj : ModProjectile
    {
        public override string Texture => "Terraria/Images/Item_" + ItemID.UnicornFish;

        private ref float DashTimer => ref Projectile.ai[0];
        private ref float DashDirection => ref Projectile.ai[1];

        private Player Owner => Main.player[Projectile.owner];
        private const int DashDuration = 35; //冲刺持续时间
        private float rotation = 0f;
        private float scale = 1f;

        //彩虹粒子系统
        private readonly List<RainbowParticle> rainbowParticles = new();
        //星光粒子系统
        private readonly List<StarParticle> starParticles = new();

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 24;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 90;
            Projectile.height = 90;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = DashDuration;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 8;
        }

        public override void AI() {
            DashTimer++;

            if (!Owner.active || Owner.dead) {
                Projectile.Kill();
                return;
            }

            //玩家跟随弹幕（仅水平方向）
            Owner.Center = new Vector2(Projectile.Center.X, Owner.Center.Y);

            //免疫帧
            if (DashTimer < DashDuration - 6) {
                Owner.GivePlayerImmuneState(10);
            }

            //冲刺阶段动态速度（仅水平）
            if (DashTimer <= 15) {
                //加速阶段
                Projectile.velocity.X *= 1.1f;
                scale = MathHelper.Lerp(1f, 1.8f, DashTimer / 15f);
            }
            else if (DashTimer > DashDuration - 10) {
                //减速阶段
                Projectile.velocity.X *= 0.86f;
                scale = MathHelper.Lerp(1.8f, 1f, (DashTimer - (DashDuration - 10)) / 10f);
            }

            //旋转效果
            rotation += Projectile.velocity.Length() * 0.012f * DashDirection;

            //华丽的彩虹拖尾粒子
            if (Main.rand.NextBool(1)) {
                SpawnRainbowTrail();
            }

            //星光粒子效果
            if (Main.rand.NextBool(2)) {
                SpawnStarParticle();
            }

            //周期性冲击粒子
            if (DashTimer % 3 == 0) {
                SpawnImpactParticles();
            }

            //更新粒子系统
            UpdateParticles();

            //强烈照明
            float lightIntensity = 0.8f + (float)Math.Sin(DashTimer * 0.4f) * 0.2f;
            Lighting.AddLight(Projectile.Center, new Vector3(1f, 0.8f, 1.2f) * lightIntensity);

            Owner.Center = Projectile.Center;
            Projectile.rotation = Projectile.velocity.ToRotation();
            Owner.direction = Projectile.direction = Projectile.spriteDirection = Math.Sign(Projectile.velocity.X);
        }

        private void SpawnRainbowTrail() {
            //生成彩虹色拖尾粒子
            Color[] rainbowColors = new Color[] {
                new Color(255, 100, 150), //粉红
                new Color(255, 200, 100), //金黄
                new Color(150, 255, 150), //浅绿
                new Color(150, 200, 255), //浅蓝
                new Color(200, 150, 255)  //紫色
            };

            Color particleColor = rainbowColors[Main.rand.Next(rainbowColors.Length)];

            Vector2 trailPos = Projectile.Center + Main.rand.NextVector2Circular(40f, 40f);
            rainbowParticles.Add(new RainbowParticle {
                Position = trailPos,
                Velocity = -Projectile.velocity * Main.rand.NextFloat(0.3f, 0.6f) +
                          new Vector2(0, Main.rand.NextFloat(-3f, 3f)),
                Color = particleColor,
                Scale = Main.rand.NextFloat(1.8f, 3f),
                Life = 0,
                MaxLife = Main.rand.Next(25, 45),
                Rotation = Main.rand.NextFloat(MathHelper.TwoPi)
            });
        }

        private void SpawnStarParticle() {
            //生成星光粒子
            starParticles.Add(new StarParticle {
                Position = Projectile.Center + Main.rand.NextVector2Circular(35f, 35f),
                Velocity = Main.rand.NextVector2Circular(2f, 2f),
                Color = Color.Lerp(Color.White, new Color(255, 220, 150), Main.rand.NextFloat()),
                Scale = Main.rand.NextFloat(0.8f, 1.5f),
                Life = 0,
                MaxLife = Main.rand.Next(20, 35),
                Rotation = Main.rand.NextFloat(MathHelper.TwoPi),
                RotationSpeed = Main.rand.NextFloat(-0.2f, 0.2f)
            });
        }

        private void SpawnImpactParticles() {
            //向前方发射冲击粒子
            Vector2 forward = Projectile.velocity.SafeNormalize(Vector2.UnitX);

            for (int i = 0; i < 5; i++) {
                Vector2 particleVel = forward.RotatedByRandom(0.5f) * Main.rand.NextFloat(8f, 15f);

                Dust impact = Dust.NewDustPerfect(
                    Projectile.Center + forward * 45f,
                    DustID.RainbowMk2,
                    particleVel,
                    100,
                    Color.White,
                    Main.rand.NextFloat(1.5f, 2.5f)
                );
                impact.noGravity = true;
            }
        }

        private void UpdateParticles() {
            //更新彩虹粒子
            for (int i = rainbowParticles.Count - 1; i >= 0; i--) {
                RainbowParticle particle = rainbowParticles[i];
                particle.Life++;
                particle.Position += particle.Velocity;
                particle.Velocity *= 0.96f;
                particle.Rotation += 0.1f;

                if (particle.Life >= particle.MaxLife) {
                    rainbowParticles.RemoveAt(i);
                }
                else {
                    rainbowParticles[i] = particle;
                }
            }

            //更新星光粒子
            for (int i = starParticles.Count - 1; i >= 0; i--) {
                StarParticle particle = starParticles[i];
                particle.Life++;
                particle.Position += particle.Velocity;
                particle.Velocity *= 0.94f;
                particle.Rotation += particle.RotationSpeed;

                if (particle.Life >= particle.MaxLife) {
                    starParticles.RemoveAt(i);
                }
                else {
                    starParticles[i] = particle;
                }
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            //击中强力音效
            SoundEngine.PlaySound(SoundID.NPCHit4 with { Volume = 1f, Pitch = 0.2f }, target.Center);

            //击中彩虹爆炸特效
            for (int i = 0; i < 30; i++) {
                Vector2 burstVel = Main.rand.NextVector2Circular(12f, 12f);

                Color[] rainbowColors = new Color[] {
                    new Color(255, 100, 150),
                    new Color(255, 200, 100),
                    new Color(150, 255, 150),
                    new Color(150, 200, 255),
                    new Color(200, 150, 255)
                };

                Dust burst = Dust.NewDustPerfect(
                    target.Center,
                    DustID.RainbowMk2,
                    burstVel,
                    100,
                    rainbowColors[Main.rand.Next(rainbowColors.Length)],
                    Main.rand.NextFloat(1.8f, 3f)
                );
                burst.noGravity = Main.rand.NextBool();
            }

            //击退增强
            target.velocity += Projectile.velocity.SafeNormalize(Vector2.Zero) * 15f;
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D texture = TextureAssets.Item[ItemID.UnicornFish].Value;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            Vector2 origin = texture.Size() / 2f;
            float drawRotation = Projectile.velocity.ToRotation();
            if (Projectile.spriteDirection == 1) {
                drawRotation += MathHelper.PiOver4;
            }
            else {
                drawRotation -= MathHelper.PiOver4;
                drawRotation -= MathHelper.Pi;
            }

            SpriteEffects effects = DashDirection > 0 ? SpriteEffects.None : SpriteEffects.FlipHorizontally;

            //绘制彩虹粒子
            DrawRainbowParticles();

            //绘制星光粒子
            DrawStarParticles();

            //绘制华丽拖尾
            for (int i = 0; i < Projectile.oldPos.Length; i++) {
                if (Projectile.oldPos[i] == Vector2.Zero) continue;

                float trailProgress = 1f - i / (float)Projectile.oldPos.Length;
                Vector2 trailPos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;

                //彩虹色渐变
                Color[] rainbowColors = new Color[] {
                    new Color(255, 100, 150),
                    new Color(255, 200, 100),
                    new Color(150, 255, 150),
                    new Color(150, 200, 255),
                    new Color(200, 150, 255)
                };

                Color trailColor = rainbowColors[(int)(trailProgress * (rainbowColors.Length - 1))] * (trailProgress * 0.9f);

                Main.EntitySpriteDraw(
                    texture,
                    trailPos,
                    null,
                    trailColor,
                    drawRotation,
                    origin,
                    scale * trailProgress * 1.1f,
                    effects,
                    0
                );
            }

            //绘制主体
            Main.EntitySpriteDraw(
                texture,
                drawPos,
                null,
                Color.White,
                drawRotation,
                origin,
                scale,
                effects,
                0
            );

            return false;
        }

        private void DrawRainbowParticles() {
            Texture2D pixel = VaultAsset.placeholder2.Value;

            foreach (var particle in rainbowParticles) {
                float alpha = 1f - particle.Life / (float)particle.MaxLife;
                Vector2 drawPos = particle.Position - Main.screenPosition;

                Main.EntitySpriteDraw(
                    pixel,
                    drawPos,
                    new Rectangle(0, 0, 1, 1),
                    particle.Color * alpha,
                    particle.Rotation,
                    Vector2.Zero,
                    new Vector2(particle.Scale * 8f, particle.Scale * 8f),
                    SpriteEffects.None,
                    0
                );
            }
        }

        private void DrawStarParticles() {
            Texture2D pixel = VaultAsset.placeholder2.Value;

            foreach (var particle in starParticles) {
                float alpha = 1f - particle.Life / (float)particle.MaxLife;
                Vector2 drawPos = particle.Position - Main.screenPosition;

                //绘制十字星光
                Main.EntitySpriteDraw(
                    pixel,
                    drawPos,
                    new Rectangle(0, 0, 1, 1),
                    particle.Color * alpha,
                    particle.Rotation,
                    Vector2.Zero,
                    new Vector2(particle.Scale * 12f, particle.Scale * 2f),
                    SpriteEffects.None,
                    0
                );

                Main.EntitySpriteDraw(
                    pixel,
                    drawPos,
                    new Rectangle(0, 0, 1, 1),
                    particle.Color * alpha,
                    particle.Rotation + MathHelper.PiOver2,
                    Vector2.Zero,
                    new Vector2(particle.Scale * 12f, particle.Scale * 2f),
                    SpriteEffects.None,
                    0
                );
            }
        }

        public override void OnKill(int timeLeft) {
            //结束爆发特效
            for (int i = 0; i < 40; i++) {
                Vector2 velocity = Main.rand.NextVector2Circular(10f, 10f);

                Color[] rainbowColors = new Color[] {
                    new Color(255, 100, 150),
                    new Color(255, 200, 100),
                    new Color(150, 255, 150),
                    new Color(150, 200, 255),
                    new Color(200, 150, 255)
                };

                Dust end = Dust.NewDustPerfect(
                    Projectile.Center,
                    DustID.RainbowMk2,
                    velocity,
                    100,
                    rainbowColors[Main.rand.Next(rainbowColors.Length)],
                    Main.rand.NextFloat(1.5f, 2.5f)
                );
                end.noGravity = Main.rand.NextBool();
            }
        }
    }

    /// <summary>
    /// 彩虹粒子结构
    /// </summary>
    internal struct RainbowParticle
    {
        public Vector2 Position;
        public Vector2 Velocity;
        public Color Color;
        public float Scale;
        public int Life;
        public int MaxLife;
        public float Rotation;
    }

    /// <summary>
    /// 星光粒子结构
    /// </summary>
    internal struct StarParticle
    {
        public Vector2 Position;
        public Vector2 Velocity;
        public Color Color;
        public float Scale;
        public int Life;
        public int MaxLife;
        public float Rotation;
        public float RotationSpeed;
    }
}
