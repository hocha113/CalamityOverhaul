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
    /// <summary>
    /// 灵液鱼技能，灵液感染与周期性射流
    /// </summary>
    internal class FishIchorn : FishSkill
    {
        public override int UnlockFishID => ItemID.Ichorfish;
        public override int DefaultCooldown => 120 - HalibutData.GetDomainLayer() * 8;

        //射流计数器
        private int streamCounter = 0;
        private const int StreamInterval = 12;

        public override bool? Shoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source,
            Vector2 position, Vector2 velocity, int type, int damage, float knockback) {

            streamCounter++;

            //周期性释放灵液射流
            if (streamCounter >= StreamInterval && Cooldown <= 0) {
                streamCounter = 0;
                SetCooldown();

                //发射灵液射流
                Vector2 shootDir = velocity.SafeNormalize(Vector2.Zero);
                float spreadBase = 0.15f;

                //根据领域层数增加射流数量和扩散
                int streamCount = 3 + HalibutData.GetDomainLayer() / 3;

                for (int i = 0; i < streamCount; i++) {
                    float spreadAngle = MathHelper.Lerp(-spreadBase, spreadBase, i / (float)Math.Max(1, streamCount - 1));
                    Vector2 streamVelocity = shootDir.RotatedBy(spreadAngle) * Main.rand.NextFloat(8f, 24f);
                    streamVelocity.Y -= 3;

                    Projectile.NewProjectile(
                        source,
                        position,
                        streamVelocity,
                        ModContent.ProjectileType<IchorStream>(),
                        (int)(damage * (2.5f + HalibutData.GetDomainLayer() * 0.5f)),
                        knockback * 1.5f,
                        player.whoAmI
                    );
                }

                //发射灵液射流音效
                SoundEngine.PlaySound(SoundID.Item95 with {
                    Volume = 0.6f,
                    Pitch = -0.3f
                }, position);

                SoundEngine.PlaySound(SoundID.Item17 with {
                    Volume = 0.4f,
                    Pitch = -0.5f
                }, position);
            }

            return null;
        }
    }

    /// <summary>
    /// 全局弹幕钩子，添加灵液感染效果
    /// </summary>
    internal class FishIchornGlobalProj : GlobalProjectile
    {
        public override void OnHitNPC(Projectile projectile, NPC target, NPC.HitInfo hit, int damageDone) {
            if (projectile.owner.TryGetPlayer(out var player)
                && FishSkill.GetT<FishIchorn>().Active(player)) {
                //在这个技能下攻击会附加灵液效果
                int buffDuration = 300 + HalibutData.GetDomainLayer() * 30;
                target.AddBuff(BuffID.Ichor, buffDuration);

                //灵液感染粒子效果
                SpawnIchorInfectionEffect(target.Center);
            }
        }

        private static void SpawnIchorInfectionEffect(Vector2 position) {
            //金黄色灵液爆发
            for (int i = 0; i < 8; i++) {
                Vector2 velocity = Main.rand.NextVector2Circular(4f, 4f);
                Dust ichor = Dust.NewDustPerfect(
                    position + Main.rand.NextVector2Circular(20f, 20f),
                    DustID.Ichor,
                    velocity,
                    100,
                    default,
                    Main.rand.NextFloat(1.2f, 1.8f)
                );
                ichor.noGravity = true;
                ichor.fadeIn = 1.1f;
            }
        }
    }

    /// <summary>
    /// 灵液射流弹幕，这里搓一下液体物理模拟玩玩
    /// </summary>
    internal class IchorStream : ModProjectile
    {
        public override string Texture => CWRConstant.Placeholder;

        //液体流动状态
        private enum FluidState
        {
            Streaming,  //射流状态
            Splashing   //溅射状态
        }

        private FluidState State {
            get => (FluidState)Projectile.ai[0];
            set => Projectile.ai[0] = (float)value;
        }

        private ref float StreamLife => ref Projectile.ai[1];

        //液体粒子系统
        private readonly List<IchorParticle> liquidParticles = new();
        private const int MaxParticles = 100;
        private int particleSpawnCounter = 0;

        //液体拖尾粒子系统
        private readonly List<IchorTrailParticle> trailParticles = new();
        private const int MaxTrailParticles = 60;

        //液体物理参数
        private const float Viscosity = 0.98f;        //粘度
        private const float Gravity = 0.35f;          //重力
        private const float SurfaceTension = 0.15f;   //表面张力
        private const float FluidDensity = 1.2f;      //液体密度

        //视觉效果
        private float glowPulse = 0f;
        private readonly List<Vector2> coreTrail = new();
        private const int MaxCoreTrail = 15;

        public override void SetDefaults() {
            Projectile.width = 32;
            Projectile.height = 32;
            Projectile.friendly = true;
            Projectile.penetrate = 3;
            Projectile.timeLeft = 180;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = false;
            Projectile.alpha = 255;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 15;
        }

        public override void AI() {
            StreamLife++;

            if (State == FluidState.Streaming) {
                StreamingPhaseAI();
            }
            else if (State == FluidState.Splashing) {
                SplashingPhaseAI();
            }

            //更新核心拖尾
            UpdateCoreTrail();

            //更新所有液体粒子
            UpdateLiquidParticles();

            //更新液体拖尾粒子
            UpdateTrailParticles();

            //辉光脉冲
            glowPulse = (float)Math.Sin(StreamLife * 0.3f) * 0.3f + 0.7f;

            //灵液金黄色照明
            float lightIntensity = 0.8f;
            Lighting.AddLight(Projectile.Center,
                1.0f * lightIntensity,
                0.8f * lightIntensity,
                0.2f * lightIntensity);
        }

        //射流状态AI
        private void StreamingPhaseAI() {
            //应用重力
            Projectile.velocity.Y += Gravity * 0.3f;

            //粘性阻力
            Projectile.velocity *= 0.995f;

            //生成液体粒子
            if (particleSpawnCounter++ % 2 == 0 && liquidParticles.Count < MaxParticles) {
                SpawnStreamParticle();
            }

            //生成液体拖尾粒子
            if (StreamLife % 2 == 0 && trailParticles.Count < MaxTrailParticles) {
                SpawnTrailParticle();
            }

            //周期性灵液残留
            if (StreamLife % 4 == 0) {
                SpawnIchorResidue();
            }

            //射流音效
            if (StreamLife % 20 == 0) {
                SoundEngine.PlaySound(SoundID.Item17 with {
                    Volume = 0.25f,
                    Pitch = -0.4f
                }, Projectile.Center);
            }

            //超时转换为溅射
            if (StreamLife > 120 || Projectile.velocity.Length() < 2f) {
                EnterSplashState();
            }
        }

        //溅射状态AI
        private void SplashingPhaseAI() {
            //快速消散
            Projectile.alpha += 15;
            if (Projectile.alpha >= 255) {
                Projectile.Kill();
            }

            //粒子继续更新但不生成新的
            Projectile.velocity *= 0.9f;
        }

        //生成射流液体粒子
        private void SpawnStreamParticle() {
            Vector2 baseVel = Projectile.velocity;
            Vector2 particleVel = baseVel + Main.rand.NextVector2Circular(2f, 2f);

            IchorParticle particle = new IchorParticle {
                Position = Projectile.Center + Main.rand.NextVector2Circular(8f, 8f),
                Velocity = particleVel * Main.rand.NextFloat(0.7f, 1.1f),
                Size = Main.rand.NextFloat(1.2f, 2.5f),
                Life = 0,
                MaxLife = Main.rand.Next(25, 45),
                Rotation = Main.rand.NextFloat(MathHelper.TwoPi),
                RotationSpeed = Main.rand.NextFloat(-0.15f, 0.15f),
                Opacity = 1f,
                IsSplash = false
            };

            liquidParticles.Add(particle);
        }

        //生成液体拖尾粒子-模拟液体流动的连续性
        private void SpawnTrailParticle() {
            //在射流后方生成连续的液滴
            Vector2 spawnPos = Projectile.Center - Projectile.velocity.SafeNormalize(Vector2.Zero) * Main.rand.NextFloat(10f, 25f);
            Vector2 particleVel = Projectile.velocity * Main.rand.NextFloat(0.5f, 0.8f);
            particleVel += Main.rand.NextVector2Circular(1.5f, 1.5f);

            IchorTrailParticle trail = new IchorTrailParticle {
                Position = spawnPos + Main.rand.NextVector2Circular(6f, 6f),
                Velocity = particleVel,
                Size = Main.rand.NextFloat(0.8f, 1.8f),
                Life = 0,
                MaxLife = Main.rand.Next(15, 30),
                Rotation = Main.rand.NextFloat(MathHelper.TwoPi),
                RotationSpeed = Main.rand.NextFloat(-0.2f, 0.2f),
                Opacity = 0.9f,
                StretchFactor = 1f
            };

            trailParticles.Add(trail);
        }

        //更新所有液体粒子
        private void UpdateLiquidParticles() {
            for (int i = liquidParticles.Count - 1; i >= 0; i--) {
                IchorParticle p = liquidParticles[i];
                p.Life++;

                //物理更新
                if (!p.IsSplash) {
                    //正常射流粒子
                    p.Velocity.Y += Gravity * FluidDensity;
                    p.Velocity *= Viscosity;

                    //表面张力-向射流中心吸引
                    Vector2 toCore = Projectile.Center - p.Position;
                    float distToCore = toCore.Length();
                    if (distToCore > 15f && distToCore < 60f) {
                        p.Velocity += toCore.SafeNormalize(Vector2.Zero) * SurfaceTension;
                    }
                }
                else {
                    //溅射粒子-更强重力
                    p.Velocity.Y += Gravity * 1.5f;
                    p.Velocity.X *= 0.98f;

                    //地面碰撞检测
                    if (Framing.GetTileSafely(p.Position.ToTileCoordinates()).HasTile) {
                        p.Velocity.Y *= -0.4f;
                        p.Velocity.X *= 0.7f;
                        if (Math.Abs(p.Velocity.Y) < 1f) {
                            p.Velocity.Y = 0;
                            p.Velocity.X *= 0.95f;
                        }
                    }
                }

                p.Position += p.Velocity;
                p.Rotation += p.RotationSpeed;

                //透明度衰减
                float lifeRatio = p.Life / (float)p.MaxLife;
                p.Opacity = 1f - lifeRatio;

                //尺寸变化
                if (p.IsSplash) {
                    p.Size *= 0.98f;
                }

                //移除消逝的粒子
                if (p.Life >= p.MaxLife || p.Opacity <= 0.05f) {
                    liquidParticles.RemoveAt(i);
                    continue;
                }

                liquidParticles[i] = p;
            }
        }

        //更新液体拖尾粒子
        private void UpdateTrailParticles() {
            for (int i = trailParticles.Count - 1; i >= 0; i--) {
                IchorTrailParticle p = trailParticles[i];
                p.Life++;

                //应用重力和粘性
                p.Velocity.Y += Gravity * FluidDensity * 1.2f;
                p.Velocity *= 0.97f;

                p.Position += p.Velocity;
                p.Rotation += p.RotationSpeed;

                //拉伸效果-根据速度动态调整
                float speed = p.Velocity.Length();
                p.StretchFactor = MathHelper.Lerp(1f, 2.5f, Math.Min(speed / 20f, 1f));

                //透明度衰减
                float lifeRatio = p.Life / (float)p.MaxLife;
                p.Opacity = (1f - lifeRatio) * 0.8f;

                //尺寸衰减
                p.Size *= 0.99f;

                //移除消逝的粒子
                if (p.Life >= p.MaxLife || p.Opacity <= 0.05f) {
                    trailParticles.RemoveAt(i);
                    continue;
                }

                trailParticles[i] = p;
            }
        }

        //进入溅射状态
        private void EnterSplashState() {
            State = FluidState.Splashing;
            Projectile.velocity *= 0.5f;
            Projectile.timeLeft = 60;
        }

        //碰撞处理-生成溅射效果
        public override bool OnTileCollide(Vector2 oldVelocity) {
            if (State == FluidState.Streaming) {
                CreateSplashEffect(Projectile.Center, oldVelocity);

                //溅射音效
                SoundEngine.PlaySound(SoundID.Item95 with {
                    Volume = 0.5f,
                    Pitch = 0.2f
                }, Projectile.Center);

                SoundEngine.PlaySound(SoundID.SplashWeak with {
                    Volume = 0.6f,
                    Pitch = -0.3f
                }, Projectile.Center);

                EnterSplashState();
                return false;
            }

            return true;
        }

        //击中NPC-生成溅射效果
        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            CreateSplashEffect(Projectile.Center, Projectile.velocity);

            //附加灵液效果
            target.AddBuff(BuffID.Ichor, 360 + HalibutData.GetDomainLayer() * 40);

            //击中音效
            SoundEngine.PlaySound(SoundID.NPCHit13 with {
                Volume = 0.5f,
                Pitch = -0.2f
            }, Projectile.Center);
        }

        //创建真实的液体溅射效果
        private void CreateSplashEffect(Vector2 hitPosition, Vector2 impactVelocity) {
            //计算溅射方向
            Vector2 normal = -impactVelocity.SafeNormalize(Vector2.Zero);
            float impactSpeed = impactVelocity.Length();

            //主溅射方向
            float mainAngle = normal.ToRotation();

            //根据冲击速度计算溅射粒子数量
            int splashCount = (int)MathHelper.Clamp(impactSpeed * 3f, 20, 60);

            for (int i = 0; i < splashCount; i++) {
                //溅射角度-半球形分布
                float spreadAngle = Main.rand.NextFloat(-MathHelper.PiOver2, MathHelper.PiOver2);
                float angle = mainAngle + spreadAngle;

                //溅射速度-符合物理的速度分布
                float speedRatio = 1f - Math.Abs(spreadAngle) / MathHelper.PiOver2;
                float speed = Main.rand.NextFloat(3f, 12f) * speedRatio * (impactSpeed / 20f);
                Vector2 velocity = angle.ToRotationVector2() * speed;

                //创建溅射粒子
                if (liquidParticles.Count < MaxParticles * 2) {
                    IchorParticle splash = new IchorParticle {
                        Position = hitPosition + Main.rand.NextVector2Circular(8f, 8f),
                        Velocity = velocity,
                        Size = Main.rand.NextFloat(1.5f, 3.2f),
                        Life = 0,
                        MaxLife = Main.rand.Next(30, 60),
                        Rotation = Main.rand.NextFloat(MathHelper.TwoPi),
                        RotationSpeed = Main.rand.NextFloat(-0.2f, 0.2f),
                        Opacity = 1f,
                        IsSplash = true
                    };
                    liquidParticles.Add(splash);
                }

                //原版灵液尘埃辅助
                if (i % 3 == 0) {
                    Dust ichor = Dust.NewDustPerfect(
                        hitPosition,
                        DustID.Ichor,
                        velocity * 0.6f,
                        100,
                        default,
                        Main.rand.NextFloat(1.2f, 2f)
                    );
                    ichor.noGravity = false;
                    ichor.fadeIn = 1.2f;
                }
            }

            //溅射核心-液体团块
            for (int i = 0; i < 8; i++) {
                float angle = mainAngle + Main.rand.NextFloat(-0.8f, 0.8f);
                Vector2 velocity = angle.ToRotationVector2() * Main.rand.NextFloat(5f, 10f);

                if (liquidParticles.Count < MaxParticles * 2) {
                    IchorParticle core = new IchorParticle {
                        Position = hitPosition,
                        Velocity = velocity,
                        Size = Main.rand.NextFloat(2.5f, 4.5f),
                        Life = 0,
                        MaxLife = Main.rand.Next(40, 70),
                        Rotation = Main.rand.NextFloat(MathHelper.TwoPi),
                        RotationSpeed = Main.rand.NextFloat(-0.25f, 0.25f),
                        Opacity = 1f,
                        IsSplash = true
                    };
                    liquidParticles.Add(core);
                }
            }

            //溅射环形冲击波
            CreateSplashRing(hitPosition, mainAngle, impactSpeed);
        }

        //创建溅射环形冲击波
        private void CreateSplashRing(Vector2 center, float direction, float intensity) {
            int ringCount = (int)(intensity * 0.8f);
            ringCount = Math.Clamp(ringCount, 15, 30);

            for (int i = 0; i < ringCount; i++) {
                float angle = direction + MathHelper.Lerp(-MathHelper.Pi, MathHelper.Pi, i / (float)ringCount);
                Vector2 velocity = angle.ToRotationVector2() * Main.rand.NextFloat(4f, 8f);

                Dust ring = Dust.NewDustPerfect(
                    center,
                    DustID.Ichor,
                    velocity,
                    100,
                    default,
                    Main.rand.NextFloat(1.5f, 2.5f)
                );
                ring.noGravity = true;
                ring.fadeIn = 1.3f;
            }
        }

        //生成灵液残留效果
        private void SpawnIchorResidue() {
            Dust residue = Dust.NewDustPerfect(
                Projectile.Center + Main.rand.NextVector2Circular(12f, 12f),
                DustID.Ichor,
                Vector2.Zero,
                100,
                default,
                Main.rand.NextFloat(1f, 1.8f)
            );
            residue.noGravity = true;
            residue.velocity = -Projectile.velocity * 0.1f + Main.rand.NextVector2Circular(0.5f, 0.5f);
            residue.fadeIn = 1.1f;
        }

        //更新核心拖尾
        private void UpdateCoreTrail() {
            coreTrail.Insert(0, Projectile.Center);
            if (coreTrail.Count > MaxCoreTrail) {
                coreTrail.RemoveAt(coreTrail.Count - 1);
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            if (State == FluidState.Splashing && Projectile.alpha > 200) {
                //溅射状态几乎完全透明,只绘制粒子
                DrawLiquidParticles();
                DrawTrailParticles();
                return false;
            }

            SpriteBatch sb = Main.spriteBatch;

            //绘制液体拖尾粒子
            DrawTrailParticles();

            //绘制液体粒子系统
            DrawLiquidParticles();

            //绘制核心射流
            if (State == FluidState.Streaming) {
                DrawStreamCore(sb);
            }

            return false;
        }

        //绘制液体拖尾粒子-创造液体流动感
        private void DrawTrailParticles() {
            SpriteBatch sb = Main.spriteBatch;
            Texture2D glowTex = CWRAsset.StarTexture_White.Value;
            Texture2D streamTex = CWRAsset.LightShot.Value;

            //使用加法混合
            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

            foreach (var particle in trailParticles) {
                Vector2 drawPos = particle.Position - Main.screenPosition;
                float rotation = particle.Velocity.ToRotation();

                //灵液金黄色
                Color ichorColor = new Color(255, 200, 50) * particle.Opacity * 0.7f;

                //拉伸的液滴形状
                Vector2 scale = new Vector2(particle.Size * 0.08f, particle.Size * 0.15f * particle.StretchFactor);

                //绘制拉伸液滴主体
                sb.Draw(
                    streamTex,
                    drawPos,
                    null,
                    ichorColor,
                    rotation,
                    streamTex.Size() / 2f,
                    scale,
                    SpriteEffects.None,
                    0
                );

                //液滴核心
                Color coreColor = new Color(255, 230, 100) * particle.Opacity * 0.6f;
                sb.Draw(
                    glowTex,
                    drawPos,
                    null,
                    coreColor,
                    particle.Rotation,
                    glowTex.Size() / 2f,
                    particle.Size * 0.05f,
                    SpriteEffects.None,
                    0
                );
            }

            //恢复正常混合
            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
        }

        //绘制液体粒子-使用高级混合和灰度图
        private void DrawLiquidParticles() {
            SpriteBatch sb = Main.spriteBatch;
            Texture2D glowTex = CWRAsset.StarTexture_White.Value;
            Texture2D maskTex = CWRAsset.LightShot.Value;

            //使用加法混合模式增强液体发光效果
            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

            foreach (var particle in liquidParticles) {
                Vector2 drawPos = particle.Position - Main.screenPosition;
                float scale = particle.Size * 0.08f;
                float rotation = particle.Rotation;

                //灵液金黄色
                Color ichorColor = new Color(255, 200, 50) * particle.Opacity * 0.8f;

                //绘制粒子主体-使用星形纹理
                sb.Draw(
                    glowTex,
                    drawPos,
                    null,
                    ichorColor * 0.9f,
                    rotation,
                    glowTex.Size() / 2f,
                    scale * 1.2f,
                    SpriteEffects.None,
                    0
                );

                //绘制粒子核心-更亮
                Color coreColor = new Color(255, 230, 100) * particle.Opacity;
                sb.Draw(
                    glowTex,
                    drawPos,
                    null,
                    coreColor * 0.7f,
                    rotation * 1.5f,
                    glowTex.Size() / 2f,
                    scale * 0.6f,
                    SpriteEffects.None,
                    0
                );

                //使用遮罩纹理增加液体质感
                if (!particle.IsSplash && particle.Size > 1.8f) {
                    Color maskColor = new Color(255, 180, 30) * particle.Opacity * 0.5f;
                    sb.Draw(
                        maskTex,
                        drawPos,
                        null,
                        maskColor,
                        rotation * 0.7f,
                        maskTex.Size() / 2f,
                        scale * 1.8f,
                        SpriteEffects.None,
                        0
                    );
                }
            }

            //恢复正常混合模式
            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
        }

        //绘制射流核心
        private void DrawStreamCore(SpriteBatch sb) {
            if (coreTrail.Count < 2) return;

            Texture2D coreTex = CWRAsset.LightShot.Value;

            //使用加法混合
            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

            //绘制核心拖尾
            for (int i = 0; i < coreTrail.Count - 1; i++) {
                float progress = 1f - i / (float)coreTrail.Count;
                Vector2 drawPos = coreTrail[i] - Main.screenPosition;

                //计算方向
                Vector2 toNext = coreTrail[i + 1] - coreTrail[i];
                float rotation = toNext.ToRotation();

                //核心颜色-金黄渐变
                Color coreColor = Color.Lerp(
                    new Color(255, 230, 100),
                    new Color(255, 180, 50),
                    progress
                ) * progress * glowPulse * 0.8f;

                float scale = progress * 0.1f;

                sb.Draw(
                    coreTex,
                    drawPos,
                    null,
                    coreColor,
                    rotation,
                    coreTex.Size() / 2f,
                    new Vector2(scale * 2.5f, scale * 1f),
                    SpriteEffects.None,
                    0
                );

                //外层光晕
                Color glowColor = new Color(255, 200, 80) * progress * glowPulse * 0.4f;
                sb.Draw(
                    coreTex,
                    drawPos,
                    null,
                    glowColor,
                    rotation,
                    coreTex.Size() / 2f,
                    new Vector2(scale * 4f, scale * 2f),
                    SpriteEffects.None,
                    0
                );
            }

            //绘制射流头部-最亮
            Vector2 headPos = Projectile.Center - Main.screenPosition;
            Color headColor = new Color(255, 240, 120) * glowPulse;

            sb.Draw(
                coreTex,
                headPos,
                null,
                headColor * 0.9f,
                Projectile.velocity.ToRotation(),
                coreTex.Size() / 2f,
                new Vector2(0.15f, 0.12f),
                SpriteEffects.None,
                0
            );

            //头部强光核心
            Texture2D starTex = CWRAsset.StarTexture_White.Value;
            Color starColor = new Color(255, 255, 200) * glowPulse * 0.8f;
            sb.Draw(
                starTex,
                headPos,
                null,
                starColor,
                StreamLife * 0.1f,
                starTex.Size() / 2f,
                0.08f,
                SpriteEffects.None,
                0
            );

            //恢复正常混合
            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
        }

        public override void OnKill(int timeLeft) {
            //死亡时残留溅射
            if (State == FluidState.Streaming) {
                CreateSplashEffect(Projectile.Center, Projectile.velocity * 0.5f);
            }

            //残留灵液效果
            for (int i = 0; i < 15; i++) {
                Dust residue = Dust.NewDustPerfect(
                    Projectile.Center + Main.rand.NextVector2Circular(20f, 20f),
                    DustID.Ichor,
                    Main.rand.NextVector2Circular(4f, 4f),
                    100,
                    default,
                    Main.rand.NextFloat(1.2f, 2f)
                );
                residue.noGravity = Main.rand.NextBool();
                residue.fadeIn = 1.1f;
            }
        }
    }

    /// <summary>
    /// 灵液粒子数据结构
    /// </summary>
    internal struct IchorParticle
    {
        public Vector2 Position;
        public Vector2 Velocity;
        public float Size;
        public int Life;
        public int MaxLife;
        public float Rotation;
        public float RotationSpeed;
        public float Opacity;
        public bool IsSplash;
    }

    /// <summary>
    /// 灵液拖尾粒子数据结构-专门用于模拟液体流动
    /// </summary>
    internal struct IchorTrailParticle
    {
        public Vector2 Position;
        public Vector2 Velocity;
        public float Size;
        public int Life;
        public int MaxLife;
        public float Rotation;
        public float RotationSpeed;
        public float Opacity;
        public float StretchFactor;
    }
}
