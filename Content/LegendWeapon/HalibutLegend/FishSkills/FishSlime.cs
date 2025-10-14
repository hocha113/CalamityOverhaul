using CalamityOverhaul.Content.UIs;
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
    /// <summary>
    /// 史莱姆鱼技能-生成粘性凝胶球
    /// </summary>
    internal class FishSlime : FishSkill
    {
        public override int UnlockFishID => ItemID.Slimefish;
        public override int DefaultCooldown => 90 - HalibutData.GetDomainLayer() * 6;

        //凝胶球生成计数器
        private int gelCounter = 0;
        private const int GelInterval = 10;

        //活跃的凝胶球追踪
        private static readonly List<int> ActiveGels = new();
        private static int MaxGels => 4 + HalibutData.GetDomainLayer();

        public override bool UpdateCooldown(HalibutPlayer halibutPlayer, Player player) {
            if (!Active(player)) {
                return false;
            }

            ShootState shootState = player.GetShootState();
            Vector2 velocity = player.velocity;
            Vector2 position = player.Center;

            if (velocity.LengthSquared() < 9) {
                return false;
            }

            gelCounter++;

            //周期性生成凝胶球
            if (gelCounter >= GelInterval && Cooldown <= 0) {
                gelCounter = 0;
                SetCooldown();

                CleanupInactiveGels();

                if (ActiveGels.Count < MaxGels) {
                    //生成凝胶球
                    Vector2 shootDir = velocity.SafeNormalize(Vector2.Zero);
                    Vector2 gelVelocity = shootDir * Main.rand.NextFloat(8f, 14f);
                    gelVelocity += Main.rand.NextVector2Circular(3f, 3f);

                    int gelProj = Projectile.NewProjectile(
                        shootState.Source,
                        position,
                        gelVelocity,
                        ModContent.ProjectileType<SlimeGelOrb>(),
                        (int)(shootState.WeaponDamage * (2f + HalibutData.GetDomainLayer() * 0.6f)),
                        shootState.WeaponKnockback * 1.5f,
                        player.whoAmI
                    );

                    if (gelProj >= 0) {
                        ActiveGels.Add(gelProj);

                        //凝胶生成音效
                        SoundEngine.PlaySound(SoundID.Item95 with {
                            Volume = 0.5f,
                            Pitch = 0.3f
                        }, position);

                        SoundEngine.PlaySound(SoundID.NPCHit1 with {
                            Volume = 0.4f,
                            Pitch = 0.5f
                        }, position);

                        //生成效果
                        SpawnGelCreateEffect(position);
                    }
                }
            }
            return true;
        }

        private static void CleanupInactiveGels() {
            ActiveGels.RemoveAll(id => {
                if (id < 0 || id >= Main.maxProjectiles) return true;
                Projectile proj = Main.projectile[id];
                return !proj.active || proj.type != ModContent.ProjectileType<SlimeGelOrb>();
            });
        }

        //凝胶生成效果
        private static void SpawnGelCreateEffect(Vector2 position) {
            //史莱姆粒子
            for (int i = 0; i < 12; i++) {
                Vector2 velocity = Main.rand.NextVector2Circular(4f, 4f);
                Dust gel = Dust.NewDustPerfect(
                    position,
                    DustID.TintableDust,
                    velocity,
                    100,
                    new Color(100, 200, 255),
                    Main.rand.NextFloat(1.2f, 2f)
                );
                gel.noGravity = true;
                gel.fadeIn = 1.2f;
            }
        }
    }

    /// <summary>
    /// 全局弹幕钩子
    /// </summary>
    internal class FishSlimeGlobalProj : GlobalProjectile
    {
        public override void OnHitNPC(Projectile projectile, NPC target, NPC.HitInfo hit, int damageDone) {
            if (projectile.owner.TryGetPlayer(out var player)
                && FishSkill.GetT<FishSlime>().Active(player)) {
                //在这个技能下攻击会附加减速效果
                target.AddBuff(BuffID.Slimed, 180 + HalibutData.GetDomainLayer() * 20);
            }
        }
    }

    /// <summary>
    /// 史莱姆凝胶球弹幕
    /// </summary>
    internal class SlimeGelOrb : ModProjectile
    {
        public override string Texture => CWRConstant.Placeholder;

        //凝胶状态
        private enum GelState
        {
            Floating,   //漂浮状态
            Attached,   //附着状态
            Exploding   //爆炸状态
        }

        private GelState State {
            get => (GelState)Projectile.ai[0];
            set => Projectile.ai[0] = (float)value;
        }

        private ref float GelLife => ref Projectile.ai[1];
        private ref float AttachedTargetID => ref Projectile.ai[2];

        //粘连系统
        private readonly List<int> ConnectedGels = new();
        private const float ConnectionRange = 180f;
        private const float AttachRange = 120f;

        //凝胶物理参数
        private const float Gravity = 0.25f;
        private const float Bounce = 0.6f;
        private const float AirFriction = 0.98f;
        private const float JellyOscillation = 0.15f;

        //凝胶粒子系统
        private readonly List<GelParticle> gelParticles = new();
        private const int MaxGelParticles = 40;

        //视觉效果
        private float jellySquash = 1f;
        private float jellyStretch = 1f;
        private float glowPulse = 0f;
        private float bubblePhase = 0f;
        private Vector2 lastVelocity = Vector2.Zero;

        //爆炸参数
        private const int MaxLifeTime = 600;
        private const int PreExplosionTime = 30;

        public override void SetDefaults() {
            Projectile.width = 36;
            Projectile.height = 36;
            Projectile.friendly = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = MaxLifeTime;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = false;
            Projectile.alpha = 0;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
        }

        public override void AI() {
            GelLife++;

            //状态机
            switch (State) {
                case GelState.Floating:
                    FloatingPhaseAI();
                    break;
                case GelState.Attached:
                    AttachedPhaseAI();
                    break;
                case GelState.Exploding:
                    ExplodingPhaseAI();
                    break;
            }

            //更新粘连关系
            UpdateConnections();

            //更新凝胶粒子
            UpdateGelParticles();

            //果冻震荡效果
            UpdateJellyPhysics();

            //辉光脉冲
            glowPulse = (float)Math.Sin(GelLife * 0.2f) * 0.3f + 0.7f;

            //气泡相位
            bubblePhase += 0.05f;
            if (bubblePhase > MathHelper.TwoPi) bubblePhase = 0f;

            //史莱姆蓝色照明
            float lightIntensity = 0.7f * (1f - Projectile.alpha / 255f);
            Lighting.AddLight(Projectile.Center,
                0.3f * lightIntensity,
                0.6f * lightIntensity,
                1.0f * lightIntensity);

            //接近生命终点时进入爆炸前兆
            if (Projectile.timeLeft <= PreExplosionTime && State != GelState.Exploding) {
                State = GelState.Exploding;

                //爆炸前兆音效
                SoundEngine.PlaySound(SoundID.NPCHit1 with {
                    Volume = 0.5f,
                    Pitch = 0.8f
                }, Projectile.Center);
            }
        }

        //漂浮状态AI
        private void FloatingPhaseAI() {
            //应用重力
            Projectile.velocity.Y += Gravity;

            //空气阻力
            Projectile.velocity *= AirFriction;

            //轻微漂浮效果
            float floatOscillation = (float)Math.Sin(GelLife * 0.1f) * 0.1f;
            Projectile.velocity.Y += floatOscillation;

            //生成凝胶粒子
            if (GelLife % 3 == 0 && gelParticles.Count < MaxGelParticles) {
                SpawnGelParticle();
            }

            //检测附着目标
            CheckAttachment();

            //粘性拉伸效果
            if (GelLife % 5 == 0) {
                SpawnStickyTrail();
            }
        }

        //附着状态AI
        private void AttachedPhaseAI() {
            int targetID = (int)AttachedTargetID;

            //检查附着目标是否有效
            if (targetID >= 0 && targetID < Main.maxNPCs) {
                NPC target = Main.npc[targetID];
                if (target.active) {
                    //跟随目标
                    Projectile.Center = Vector2.Lerp(Projectile.Center, target.Center, 0.15f);
                    Projectile.velocity = target.velocity * 0.8f;

                    //周期性造成伤害
                    if (GelLife % 30 == 0) {
                        target.SimpleStrikeNPC(Projectile.damage / 4, 0, false, 0f, null, false, 0f, true);

                        //附着伤害效果
                        SpawnAttachDamageEffect(target.Center);
                    }
                }
                else {
                    //目标死亡,返回漂浮状态
                    State = GelState.Floating;
                    AttachedTargetID = -1;
                }
            }
            else {
                //无效目标,返回漂浮状态
                State = GelState.Floating;
                AttachedTargetID = -1;
            }

            //附着时生成更少粒子
            if (GelLife % 6 == 0 && gelParticles.Count < MaxGelParticles) {
                SpawnGelParticle();
            }
        }

        //爆炸状态AI
        private void ExplodingPhaseAI() {
            //停止移动
            Projectile.velocity *= 0.9f;

            //脉冲效果加速
            glowPulse = (float)Math.Sin(GelLife * 0.8f) * 0.5f + 0.5f;

            //尺寸震荡
            float explosionProgress = 1f - Projectile.timeLeft / (float)PreExplosionTime;
            jellySquash = 1f + explosionProgress * 0.5f;

            //密集粒子
            if (gelParticles.Count < MaxGelParticles * 2) {
                SpawnGelParticle();
            }
        }

        //检测附着目标
        private void CheckAttachment() {
            if (State != GelState.Floating) return;

            //寻找最近的敌人
            NPC closestNPC = null;
            float closestDist = AttachRange;

            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npc = Main.npc[i];
                if (npc.active && npc.CanBeChasedBy() && !npc.friendly) {
                    float dist = Vector2.Distance(Projectile.Center, npc.Center);
                    if (dist < closestDist) {
                        closestDist = dist;
                        closestNPC = npc;
                    }
                }
            }

            if (closestNPC != null) {
                //附着到敌人
                State = GelState.Attached;
                AttachedTargetID = closestNPC.whoAmI;
                Projectile.tileCollide = false;

                //附着音效
                SoundEngine.PlaySound(SoundID.NPCHit1 with {
                    Volume = 0.6f,
                    Pitch = 0.4f
                }, Projectile.Center);

                //附着效果
                SpawnAttachEffect(closestNPC.Center);
            }
        }

        //更新连接关系
        private void UpdateConnections() {
            ConnectedGels.Clear();

            //查找附近的凝胶球
            for (int i = 0; i < Main.maxProjectiles; i++) {
                if (i == Projectile.whoAmI) continue;

                Projectile other = Main.projectile[i];
                if (!other.active || other.type != Projectile.type) continue;

                float dist = Vector2.Distance(Projectile.Center, other.Center);
                if (dist < ConnectionRange) {
                    ConnectedGels.Add(i);

                    //连接拉力
                    if (State == GelState.Floating) {
                        Vector2 toOther = other.Center - Projectile.Center;
                        float pullStrength = (1f - dist / ConnectionRange) * 0.05f;
                        Projectile.velocity += toOther.SafeNormalize(Vector2.Zero) * pullStrength;
                    }
                }
            }
        }

        //更新果冻物理效果
        private void UpdateJellyPhysics() {
            //计算速度变化
            Vector2 velocityChange = Projectile.velocity - lastVelocity;
            float velocityChangeLength = velocityChange.Length();

            //根据速度变化调整形变
            if (velocityChangeLength > 0.1f) {
                //压扁拉伸效果
                float impact = Math.Min(velocityChangeLength * 0.15f, 0.5f);
                Vector2 direction = velocityChange.SafeNormalize(Vector2.Zero);

                //沿运动方向拉伸,垂直方向压扁
                if (Math.Abs(direction.X) > Math.Abs(direction.Y)) {
                    jellyStretch = 1f + impact;
                    jellySquash = 1f - impact * 0.5f;
                }
                else {
                    jellyStretch = 1f - impact * 0.5f;
                    jellySquash = 1f + impact;
                }
            }
            else {
                //回弹到正常形状
                jellyStretch = MathHelper.Lerp(jellyStretch, 1f, 0.15f);
                jellySquash = MathHelper.Lerp(jellySquash, 1f, 0.15f);
            }

            //添加震荡
            float oscillation = (float)Math.Sin(GelLife * 0.3f) * JellyOscillation;
            jellyStretch += oscillation * 0.5f;
            jellySquash -= oscillation * 0.5f;

            lastVelocity = Projectile.velocity;
        }

        //生成凝胶粒子
        private void SpawnGelParticle() {
            Vector2 particlePos = Projectile.Center + Main.rand.NextVector2Circular(25f, 25f);
            Vector2 particleVel = Main.rand.NextVector2Circular(0.5f, 0.5f);

            GelParticle particle = new GelParticle {
                Position = particlePos,
                Velocity = particleVel,
                Size = Main.rand.NextFloat(0.8f, 1.5f),
                Life = 0,
                MaxLife = Main.rand.Next(40, 80),
                Rotation = Main.rand.NextFloat(MathHelper.TwoPi),
                Opacity = 0.7f,
                IsCore = Main.rand.NextBool(4)
            };

            gelParticles.Add(particle);
        }

        //更新凝胶粒子
        private void UpdateGelParticles() {
            for (int i = gelParticles.Count - 1; i >= 0; i--) {
                GelParticle p = gelParticles[i];
                p.Life++;

                //向中心吸引
                Vector2 toCenter = Projectile.Center - p.Position;
                float dist = toCenter.Length();
                if (dist > 5f) {
                    p.Velocity += toCenter.SafeNormalize(Vector2.Zero) * 0.08f;
                }

                p.Position += p.Velocity;
                p.Velocity *= 0.96f;

                //透明度衰减
                float lifeRatio = p.Life / (float)p.MaxLife;
                p.Opacity = (1f - lifeRatio) * 0.7f;

                //移除消逝的粒子
                if (p.Life >= p.MaxLife || p.Opacity <= 0.05f) {
                    gelParticles.RemoveAt(i);
                    continue;
                }

                gelParticles[i] = p;
            }
        }

        //生成粘性拖尾
        private void SpawnStickyTrail() {
            Dust trail = Dust.NewDustPerfect(
                Projectile.Center + Main.rand.NextVector2Circular(8f, 8f),
                DustID.TintableDust,
                -Projectile.velocity * 0.2f,
                100,
                new Color(100, 200, 255),
                Main.rand.NextFloat(0.8f, 1.5f)
            );
            trail.noGravity = true;
            trail.fadeIn = 1.1f;
        }

        //附着效果
        private void SpawnAttachEffect(Vector2 position) {
            for (int i = 0; i < 15; i++) {
                Vector2 velocity = Main.rand.NextVector2Circular(5f, 5f);
                Dust attach = Dust.NewDustPerfect(
                    position,
                    DustID.TintableDust,
                    velocity,
                    100,
                    new Color(100, 200, 255),
                    Main.rand.NextFloat(1.3f, 2.2f)
                );
                attach.noGravity = true;
                attach.fadeIn = 1.2f;
            }
        }

        //附着伤害效果
        private void SpawnAttachDamageEffect(Vector2 position) {
            for (int i = 0; i < 8; i++) {
                Vector2 velocity = Main.rand.NextVector2Circular(3f, 3f);
                Dust damage = Dust.NewDustPerfect(
                    position + Main.rand.NextVector2Circular(15f, 15f),
                    DustID.TintableDust,
                    velocity,
                    100,
                    new Color(100, 200, 255),
                    Main.rand.NextFloat(1f, 1.8f)
                );
                damage.noGravity = true;
            }
        }

        //碰撞处理
        public override bool OnTileCollide(Vector2 oldVelocity) {
            //果冻弹跳效果
            if (Math.Abs(Projectile.velocity.X - oldVelocity.X) > float.Epsilon) {
                Projectile.velocity.X = -oldVelocity.X * Bounce;
                jellyStretch = 1.3f;
                jellySquash = 0.7f;
            }
            if (Math.Abs(Projectile.velocity.Y - oldVelocity.Y) > float.Epsilon) {
                Projectile.velocity.Y = -oldVelocity.Y * Bounce;
                jellySquash = 1.3f;
                jellyStretch = 0.7f;
            }

            //弹跳粒子
            for (int i = 0; i < 5; i++) {
                Dust bounce = Dust.NewDustDirect(
                    Projectile.position,
                    Projectile.width,
                    Projectile.height,
                    DustID.TintableDust,
                    0, 0, 100,
                    new Color(100, 200, 255),
                    Main.rand.NextFloat(1f, 1.5f)
                );
                bounce.velocity = Main.rand.NextVector2Circular(3f, 3f);
                bounce.noGravity = true;
            }

            return false;
        }

        //击中NPC
        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            //附着到目标
            if (State == GelState.Floating) {
                State = GelState.Attached;
                AttachedTargetID = target.whoAmI;
                Projectile.tileCollide = false;

                SpawnAttachEffect(target.Center);
            }

            //附加减速
            target.AddBuff(BuffID.Slimed, 240);
        }

        //死亡时爆炸
        public override void OnKill(int timeLeft) {
            CreateGelExplosion();

            //爆炸音效
            SoundEngine.PlaySound(SoundID.NPCDeath1 with {
                Volume = 0.7f,
                Pitch = 0.3f
            }, Projectile.Center);

            SoundEngine.PlaySound(SoundID.Item95 with {
                Volume = 0.5f,
                Pitch = -0.2f
            }, Projectile.Center);
        }

        //创建凝胶爆炸
        private void CreateGelExplosion() {
            //爆炸伤害范围
            float explosionRadius = 120f + HalibutData.GetDomainLayer() * 15f;

            //对范围内敌人造成伤害
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npc = Main.npc[i];
                if (npc.active && npc.CanBeChasedBy() && !npc.friendly) {
                    float dist = Vector2.Distance(Projectile.Center, npc.Center);
                    if (dist < explosionRadius) {
                        //距离越近伤害越高
                        float damageRatio = 1f - dist / explosionRadius;
                        int explosionDamage = (int)(Projectile.damage * (0.5f + damageRatio * 0.5f));

                        npc.SimpleStrikeNPC(explosionDamage, 0, false, 5f, null, false, 0f, true);
                        npc.AddBuff(BuffID.Slimed, 300);
                    }
                }
            }

            //爆炸粒子效果
            int particleCount = 40 + HalibutData.GetDomainLayer() * 5;
            for (int i = 0; i < particleCount; i++) {
                float angle = MathHelper.TwoPi * i / particleCount;
                Vector2 velocity = angle.ToRotationVector2() * Main.rand.NextFloat(8f, 18f);

                Dust explosion = Dust.NewDustPerfect(
                    Projectile.Center,
                    DustID.TintableDust,
                    velocity,
                    100,
                    new Color(100, 200, 255),
                    Main.rand.NextFloat(2f, 3.5f)
                );
                explosion.noGravity = Main.rand.NextBool();
                explosion.fadeIn = 1.4f;
            }

            //爆炸冲击波
            for (int i = 0; i < 3; i++) {
                for (int j = 0; j < 20; j++) {
                    float angle = MathHelper.TwoPi * j / 20f;
                    float radius = 30f + i * 25f;
                    Vector2 spawnPos = Projectile.Center + angle.ToRotationVector2() * radius;

                    Dust ring = Dust.NewDustPerfect(
                        spawnPos,
                        DustID.TintableDust,
                        Vector2.Zero,
                        100,
                        new Color(120, 220, 255),
                        Main.rand.NextFloat(1.5f, 2.5f)
                    );
                    ring.velocity = angle.ToRotationVector2() * 5f;
                    ring.noGravity = true;
                }
            }

            //凝胶碎片
            for (int i = 0; i < 25; i++) {
                Vector2 velocity = Main.rand.NextVector2Circular(12f, 12f);
                Dust chunk = Dust.NewDustDirect(
                    Projectile.Center,
                    4, 4,
                    DustID.TintableDust,
                    0, 0, 100,
                    new Color(100, 200, 255),
                    Main.rand.NextFloat(1.5f, 3f)
                );
                chunk.velocity = velocity;
                chunk.noGravity = false;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            SpriteBatch sb = Main.spriteBatch;

            //绘制连接线
            DrawConnections(sb);

            //绘制凝胶粒子
            DrawGelParticles(sb);

            //绘制主体凝胶球
            DrawGelOrb(sb, lightColor);

            return false;
        }

        //绘制连接线
        private void DrawConnections(SpriteBatch sb) {
            if (ConnectedGels.Count == 0) return;

            Texture2D lineTex = DyeMachineAsset.SoftGlow;

            //使用加法混合
            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

            foreach (int gelID in ConnectedGels) {
                if (gelID >= Main.maxProjectiles) continue;
                Projectile other = Main.projectile[gelID];
                if (!other.active) continue;

                Vector2 start = Projectile.Center;
                Vector2 end = other.Center;
                Vector2 diff = end - start;
                float length = diff.Length();
                float rotation = diff.ToRotation();

                //连接强度基于距离
                float strength = 1f - length / ConnectionRange;
                Color connectionColor = new Color(100, 200, 255) * strength * 0.4f * glowPulse;

                //绘制粘性连接
                sb.Draw(
                    lineTex,
                    start - Main.screenPosition,
                    null,
                    connectionColor,
                    rotation,
                    new Vector2(0, lineTex.Height / 2f),
                    new Vector2(length / lineTex.Width, 0.03f * strength),
                    SpriteEffects.None,
                    0
                );
            }

            //恢复正常混合
            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
        }

        //绘制凝胶粒子
        private void DrawGelParticles(SpriteBatch sb) {
            Texture2D particleTex = CWRAsset.StarTexture_White.Value;

            //使用加法混合
            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

            foreach (var particle in gelParticles) {
                Vector2 drawPos = particle.Position - Main.screenPosition;
                Color particleColor = particle.IsCore
                    ? new Color(150, 230, 255) * particle.Opacity * 0.9f
                    : new Color(100, 200, 255) * particle.Opacity * 0.6f;

                sb.Draw(
                    particleTex,
                    drawPos,
                    null,
                    particleColor,
                    particle.Rotation,
                    particleTex.Size() / 2f,
                    particle.Size * 0.04f,
                    SpriteEffects.None,
                    0
                );
            }

            //恢复正常混合
            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
        }

        //绘制凝胶球主体
        private void DrawGelOrb(SpriteBatch sb, Color lightColor) {
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            Main.instance.LoadItem(ItemID.KingSlimeMask);
            Texture2D glowTex = DyeMachineAsset.SoftGlow;
            Texture2D orbTex = TextureAssets.Item[ItemID.KingSlimeMask].Value;

            Texture2D maskTex = DyeMachineAsset.SoftGlow;

            //爆炸前兆效果
            if (State == GelState.Exploding) {
                float explosionGlow = (float)Math.Sin(GelLife * 1.2f) * 0.5f + 0.5f;
                for (int i = 0; i < 3; i++) {
                    float scale = (1.25f + i * 0.08f) * jellySquash;
                    Color explosionColor = new Color(200, 230, 255) * explosionGlow * (1f - i * 0.3f) * 0.7f;

                    sb.Draw(
                        orbTex,
                        drawPos,
                        null,
                        explosionColor with { A = 0 },
                        GelLife * 0.1f + i,
                        orbTex.Size() / 2f,
                        scale,
                        SpriteEffects.None,
                        0
                    );
                }
            }

            //外层光晕
            Color outerGlow = Color.DarkSeaGreen * glowPulse * 0.5f;
            sb.Draw(
                maskTex,
                drawPos,
                null,
                outerGlow with { A = 0 },
                GelLife * 0.05f,
                maskTex.Size() / 2f,
                new Vector2(1.22f * jellyStretch, 1.22f * jellySquash),
                SpriteEffects.None,
                0
            );

            //中层凝胶主体
            Color gelColor = Color.LightGreen * glowPulse * 0.8f;
            sb.Draw(
                orbTex,
                drawPos,
                null,
                gelColor,
                GelLife * 0.08f,
                orbTex.Size() / 2f,
                new Vector2(1.18f * jellyStretch, 1.18f * jellySquash),
                SpriteEffects.None,
                0
            );

            //内核高光
            Color coreColor = new Color(180, 230, 255) * glowPulse;
            sb.Draw(
                glowTex,
                drawPos,
                null,
                coreColor with { A = 0 },
                GelLife * 0.15f,
                glowTex.Size() / 2f,
                new Vector2(1.12f * jellyStretch, 1.12f * jellySquash),
                SpriteEffects.None,
                0
            );

            //气泡效果
            for (int i = 0; i < 3; i++) {
                float bubbleOffset = (float)Math.Sin(bubblePhase + i * MathHelper.TwoPi / 3f) * 8f;
                Vector2 bubblePos = drawPos + new Vector2(bubbleOffset * 0.5f, bubbleOffset);
                Color bubbleColor = new Color(200, 240, 255) * glowPulse * 0.4f;

                sb.Draw(
                    glowTex,
                    bubblePos,
                    null,
                    bubbleColor with { A = 0 },
                    bubblePhase + i,
                    glowTex.Size() / 2f,
                    0.04f,
                    SpriteEffects.None,
                    0
                );
            }
        }
    }

    /// <summary>
    /// 凝胶粒子数据结构
    /// </summary>
    internal struct GelParticle
    {
        public Vector2 Position;
        public Vector2 Velocity;
        public float Size;
        public int Life;
        public int MaxLife;
        public float Rotation;
        public float Opacity;
        public bool IsCore;
    }
}
