using CalamityMod.Items.Weapons.Melee;
using CalamityMod.Particles;
using CalamityOverhaul.Common;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.Graphics.CameraModifiers;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.MurasamaLegend.MurasamaProj
{
    internal class MuraGroundSmash : MuraTriggerDash
    {
        public override string Texture => CWRConstant.Cay_Wap_Melee + "Murasama";

        private enum GroundSmashState
        {
            ChargeUp,      //蓄力阶段
            Descending,    //下落阶段
            Impact,        //冲击阶段
            Recovery       //恢复阶段
        }

        private GroundSmashState State {
            get => (GroundSmashState)Projectile.ai[0];
            set => Projectile.ai[0] = (float)value;
        }

        private ref float StateTimer => ref Projectile.ai[1];
        private ref float ChargeIntensity => ref Projectile.localAI[0];

        //阶段持续时间
        private const int ChargeUpDuration = 25;
        private const int MaxDescendTime = 90;
        private const int ImpactDuration = 30;
        private const int RecoveryDuration = 20;

        //视觉特效变量
        private float auraIntensity = 0f;
        private readonly List<LightningParticle> lightningEffects = new();
        private readonly List<ShockwaveRing> shockwaves = new();
        private Vector2 impactPosition = Vector2.Zero;
        private bool hasImpacted = false;
        private float swordGlowIntensity = 0f;

        public override void SetStaticDefaults() {
            base.SetStaticDefaults();
            ProjectileID.Sets.TrailCacheLength[Type] = 12;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults() {
            base.SetDefaults();
            Projectile.timeLeft = ChargeUpDuration + MaxDescendTime + ImpactDuration + RecoveryDuration;
            Projectile.penetrate = -1;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
        }

        public override bool? CanDamage() {
            //只在下落和冲击阶段造成伤害
            return State == GroundSmashState.Descending || State == GroundSmashState.Impact;
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {
            base.ModifyHitNPC(target, ref modifiers);
        }

        public override void AI() {
            //禁止在下砸期间左键攻击
            if (Owner.ItemAnimationActive && Owner.HeldItem.type == ModContent.ItemType<Murasama>()) {
                Owner.itemAnimation = 0;
                Owner.itemTime = 0;
            }

            Lighting.AddLight(Projectile.Center,
                new Vector3(1f, 0.2f, 0.2f) * auraIntensity * 1.5f);

            StateTimer++;

            switch (State) {
                case GroundSmashState.ChargeUp:
                    ChargeUpPhase();
                    break;
                case GroundSmashState.Descending:
                    DescendingPhase();
                    break;
                case GroundSmashState.Impact:
                    ImpactPhase();
                    break;
                case GroundSmashState.Recovery:
                    RecoveryPhase();
                    break;
            }

            //更新所有特效
            UpdateEffects();

            //让玩家跟随弹幕
            if (State != GroundSmashState.Recovery) {
                Owner.Center = Vector2.Lerp(Owner.Center, Projectile.Center, 0.25f);
                Owner.velocity = Vector2.Zero;
            }
        }

        private void ChargeUpPhase() {
            float progress = StateTimer / ChargeUpDuration;

            //玩家在空中停滞并抬起
            Owner.velocity = Vector2.Zero;
            Owner.position.Y -= 0.5f;

            //蓄力强度增长
            ChargeIntensity = CWRUtils.EaseOutCubic(progress);
            auraIntensity = ChargeIntensity;
            swordGlowIntensity = ChargeIntensity * 1.2f;

            //刀向后上方抬起
            float liftAngle = MathHelper.Lerp(0f, -MathHelper.PiOver2 * 1.3f, ChargeIntensity);
            Projectile.rotation = liftAngle;
            Projectile.Center = Owner.Center + new Vector2(0, -30 * ChargeIntensity);

            //设置玩家手臂姿势
            float armAngle = MathHelper.ToRadians(-100 - 30 * ChargeIntensity);
            Owner.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.Full, armAngle * Owner.direction);
            Owner.SetCompositeArmBack(true, Player.CompositeArmStretchAmount.Full, armAngle * Owner.direction);

            //给予无敌帧
            Owner.GivePlayerImmuneState(10);

            //生成蓄力粒子
            if (StateTimer % 3 == 0) {
                SpawnChargeParticles();
            }

            //蓄力音效
            if (StateTimer == 1) {
                SoundEngine.PlaySound(SoundID.DD2_WitherBeastAuraPulse with {
                    Volume = 0.6f,
                    Pitch = -0.4f
                }, Projectile.Center);
            }

            if (StateTimer % 10 == 0 && StateTimer < ChargeUpDuration - 5) {
                SoundEngine.PlaySound(SoundID.Item15 with {
                    Volume = 0.4f * progress,
                    Pitch = -0.5f + progress * 0.3f
                }, Projectile.Center);
            }

            //完成蓄力
            if (StateTimer >= ChargeUpDuration) {
                State = GroundSmashState.Descending;
                StateTimer = 0;

                SoundEngine.PlaySound(SoundID.DD2_MonkStaffSwing with {
                    Volume = 0.9f,
                    Pitch = -0.3f
                }, Projectile.Center);

                SoundEngine.PlaySound(Murasama.BigSwing with {
                    Volume = 0.8f,
                    Pitch = -0.2f
                }, Projectile.Center);

                //初始化下落速度
                Projectile.velocity = new Vector2(0, 0.5f);
            }
        }

        private void DescendingPhase() {
            //加速下落
            Projectile.velocity.Y += 1.2f;

            //限制最大下落速度，级别越高速度越快
            float maxSpeed = 25f + MurasamaOverride.GetLevel(Item) * 2f;
            if (Projectile.velocity.Y > maxSpeed) {
                Projectile.velocity.Y = maxSpeed;
            }

            //旋转刀身
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;

            //增强光晕
            float speedRatio = Math.Abs(Projectile.velocity.Y) / maxSpeed;
            auraIntensity = 0.8f + speedRatio * 0.5f;
            swordGlowIntensity = 1f + speedRatio * 0.8f;

            //生成下落轨迹特效
            SpawnDescendTrail();

            //设置玩家手臂姿势 - 握剑向下
            Owner.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.Full,
                MathHelper.ToRadians(90) * Owner.direction);
            Owner.SetCompositeArmBack(true, Player.CompositeArmStretchAmount.Full,
                MathHelper.ToRadians(90) * Owner.direction);

            Owner.GivePlayerImmuneState(8);

            //持续音效
            if (StateTimer % 20 == 0) {
                SoundEngine.PlaySound(SoundID.Item1 with {
                    Volume = 0.5f,
                    Pitch = 0.3f + speedRatio * 0.2f
                }, Projectile.Center);
            }

            //超时或玩家取消
            if (StateTimer > MaxDescendTime) {
                TriggerImpact();
            }
        }

        private void ImpactPhase() {
            if (StateTimer == 1) {
                ExecuteImpact();
            }

            //保持在冲击位置
            Projectile.velocity = Vector2.Zero;
            Projectile.Center = impactPosition;
            Projectile.rotation = MathHelper.PiOver2;

            //冲击余波
            float progress = StateTimer / (float)ImpactDuration;
            auraIntensity = (1f - progress) * 1.5f;
            swordGlowIntensity = (1f - progress) * 2f;

            //持续生成冲击波
            if (StateTimer % 5 == 0 && StateTimer < ImpactDuration - 10) {
                SpawnImpactRing();
            }

            //设置玩家姿势
            Owner.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.Full, 0);
            Owner.SetCompositeArmBack(true, Player.CompositeArmStretchAmount.Full, 0);

            Owner.GivePlayerImmuneState(15);

            if (StateTimer >= ImpactDuration) {
                State = GroundSmashState.Recovery;
                StateTimer = 0;
            }
        }

        private void RecoveryPhase() {
            float progress = StateTimer / (float)RecoveryDuration;

            //逐渐恢复控制
            Projectile.velocity *= 0.9f;
            auraIntensity = (1f - progress) * 0.3f;
            swordGlowIntensity = (1f - progress) * 0.5f;

            //玩家获得向上的推力
            if (StateTimer == 1 && Projectile.IsOwnedByLocalPlayer()) {
                float upwardForce = -12f - MurasamaOverride.GetLevel(Item) * 1.2f;
                Owner.velocity = new Vector2(Owner.velocity.X * 0.5f, upwardForce);
            }

            if (StateTimer >= RecoveryDuration) {
                Projectile.Kill();
            }
        }

        private void TriggerImpact() {
            State = GroundSmashState.Impact;
            StateTimer = 0;
            hasImpacted = true;
            impactPosition = Projectile.Center;
        }

        private void ExecuteImpact() {
            //巨大爆炸音效
            SoundEngine.PlaySound(SoundID.Item14 with {
                Volume = 1.2f,
                Pitch = -0.6f
            }, impactPosition);

            SoundEngine.PlaySound(Murasama.InorganicHit with {
                Volume = 1f,
                Pitch = -0.4f
            }, impactPosition);

            SoundEngine.PlaySound(SoundID.DD2_MonkStaffGroundImpact with {
                Volume = 0.9f
            }, impactPosition);

            //屏幕震动
            if (CWRServerConfig.Instance.ScreenVibration) {
                PunchCameraModifier modifier = new PunchCameraModifier(
                    impactPosition,
                    new Vector2(0, Main.rand.NextFloat(-3, 3)),
                    16f,
                    20f,
                    30,
                    1200f,
                    FullName
                );
                Main.instance.CameraModifiers.Add(modifier);
            }

            //生成冲击特效
            CreateMassiveImpactEffect();

            //造成范围伤害
            Projectile.Explode(300 + MurasamaOverride.GetLevel(Item) * 30);

            //生成地面裂痕（视觉效果）
            SpawnGroundCracks();
        }

        private void CreateMassiveImpactEffect() {
            int level = MurasamaOverride.GetLevel(Item);
            int particleCount = 60 + level * 10;

            //主爆炸粒子
            for (int i = 0; i < particleCount; i++) {
                float angle = MathHelper.TwoPi * i / particleCount;
                Vector2 velocity = angle.ToRotationVector2() * Main.rand.NextFloat(10f, 25f);

                AltSparkParticle spark = new AltSparkParticle(
                    impactPosition,
                    velocity,
                    false,
                    Main.rand.Next(20, 35),
                    Main.rand.NextFloat(2f, 4.5f),
                    Main.rand.NextBool(2) ? Color.Red : Color.DarkRed
                );
                GeneralParticleHandler.SpawnParticle(spark);
            }

            //冲击波环
            for (int i = 0; i < 5; i++) {
                shockwaves.Add(new ShockwaveRing(impactPosition, 50f + i * 30f, 15 + i * 5));
            }

            //向上喷射的碎片
            for (int i = 0; i < 60; i++) {
                Vector2 velocity = new Vector2(
                    Main.rand.NextFloat(-15f, 15f),
                    Main.rand.NextFloat(-20f, -8f)
                );

                int dustType = Main.rand.NextBool() ? DustID.Smoke : DustID.Torch;
                Dust debris = Dust.NewDustPerfect(
                    impactPosition,
                    dustType,
                    velocity,
                    100,
                    Main.rand.NextBool() ? Color.Red : Color.OrangeRed,
                    Main.rand.NextFloat(2f, 3.5f)
                );
                debris.noGravity = Main.rand.NextBool();
            }

            //闪电效果
            for (int i = 0; i < 8; i++) {
                float angle = MathHelper.TwoPi * i / 8f;
                Vector2 direction = angle.ToRotationVector2();
                lightningEffects.Add(new LightningParticle(
                    impactPosition,
                    direction,
                    Main.rand.Next(150, 250),
                    Main.rand.Next(25, 40)
                ));
            }

            //火焰柱效果
            for (int i = 0; i < 3; i++) {
                for (int j = 0; j < 30; j++) {
                    float angle = MathHelper.TwoPi * j / 30f;
                    float radius = 80f + i * 50f;
                    Vector2 pos = impactPosition + angle.ToRotationVector2() * radius;

                    Dust fire = Dust.NewDustPerfect(
                        pos,
                        DustID.Torch,
                        new Vector2(0, Main.rand.NextFloat(-10f, -5f)),
                        100,
                        Color.OrangeRed,
                        Main.rand.NextFloat(2f, 3.5f)
                    );
                    fire.noGravity = true;
                }
            }
        }

        private void SpawnGroundCracks() {
            //生成地面裂痕尘埃效果
            for (int i = 0; i < 80; i++) {
                float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                float distance = Main.rand.NextFloat(50f, 300f);
                Vector2 pos = impactPosition + angle.ToRotationVector2() * distance;

                Dust crack = Dust.NewDustPerfect(
                    pos,
                    DustID.Smoke,
                    Vector2.Zero,
                    100,
                    new Color(80, 80, 80),
                    Main.rand.NextFloat(1.5f, 2.5f)
                );
                crack.noGravity = false;
            }
        }

        private void SpawnChargeParticles() {
            //蓄力阶段的能量聚集粒子
            for (int i = 0; i < 3; i++) {
                float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                float distance = Main.rand.NextFloat(80f, 150f);
                Vector2 spawnPos = Projectile.Center + angle.ToRotationVector2() * distance;
                Vector2 velocity = (Projectile.Center - spawnPos).SafeNormalize(Vector2.Zero) *
                                  Main.rand.NextFloat(3f, 7f);

                AltSparkParticle spark = new AltSparkParticle(
                    spawnPos,
                    velocity,
                    false,
                    Main.rand.Next(15, 25),
                    Main.rand.NextFloat(1.5f, 2.5f),
                    Color.Lerp(Color.Red, Color.OrangeRed, Main.rand.NextFloat())
                );
                GeneralParticleHandler.SpawnParticle(spark);
            }
        }

        private void SpawnDescendTrail() {
            if (StateTimer % 2 != 0) return;

            //下落轨迹粒子
            for (int i = 0; i < 3; i++) {
                Vector2 offset = Main.rand.NextVector2Circular(22f, 22f);
                AltSparkParticle spark = new AltSparkParticle(
                    Projectile.Center + offset,
                    -Projectile.velocity * Main.rand.NextFloat(0.3f, 0.6f),
                    false,
                    Main.rand.Next(10, 20),
                    Main.rand.NextFloat(2f, 3.5f),
                    Main.rand.NextBool() ? Color.Red : Color.Crimson
                );
                GeneralParticleHandler.SpawnParticle(spark);
            }

            //空气扭曲效果
            if (StateTimer % 4 == 0) {
                Dust trail = Dust.NewDustPerfect(
                    Projectile.Center + Main.rand.NextVector2Circular(20f, 20f),
                    DustID.Smoke,
                    -Projectile.velocity * 0.2f,
                    100,
                    new Color(120, 120, 120, 100),
                    Main.rand.NextFloat(1.5f, 2.5f)
                );
                trail.noGravity = true;
            }
        }

        private void SpawnImpactRing() {
            shockwaves.Add(new ShockwaveRing(impactPosition, 80f, 20));
        }

        private void UpdateEffects() {
            //更新闪电效果
            for (int i = lightningEffects.Count - 1; i >= 0; i--) {
                lightningEffects[i].Update();
                if (lightningEffects[i].Life >= lightningEffects[i].MaxLife) {
                    lightningEffects.RemoveAt(i);
                }
            }

            //更新冲击波
            for (int i = shockwaves.Count - 1; i >= 0; i--) {
                shockwaves[i].Update();
                if (shockwaves[i].ShouldRemove()) {
                    shockwaves.RemoveAt(i);
                }
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            //击中敌人时的特效
            for (int i = 0; i < 15; i++) {
                Vector2 velocity = Main.rand.NextVector2Circular(8f, 8f);
                AltSparkParticle spark = new AltSparkParticle(
                    target.Center,
                    velocity,
                    false,
                    Main.rand.Next(10, 18),
                    Main.rand.NextFloat(1.5f, 2.5f),
                    Main.rand.NextBool() ? Color.Red : Color.DarkRed
                );
                GeneralParticleHandler.SpawnParticle(spark);
            }
            TriggerImpact();
        }

        public override bool OnTileCollide(Vector2 oldVelocity) {
            if (State == GroundSmashState.Descending && !hasImpacted) {
                TriggerImpact();
            }
            return false;
        }

        public override bool PreDraw(ref Color lightColor) {
            if (Projectile.timeLeft > 290) return false;

            SpriteBatch sb = Main.spriteBatch;
            Texture2D texture = CWRUtils.GetT2DValue(Texture);
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            Rectangle frame = texture.GetRectangle(Projectile.frame, 13);
            Vector2 origin = VaultUtils.GetOrig(texture, 13);

            //绘制刀身发光层
            if (swordGlowIntensity > 0.1f) {
                for (int i = 0; i < 4; i++) {
                    float glowScale = 1f + swordGlowIntensity * 0.15f * (1f + i * 0.2f);
                    float glowAlpha = swordGlowIntensity * (1f - i * 0.2f) * 0.4f;
                    Color glowColor = Color.Lerp(Color.Red, Color.OrangeRed, i / 4f) * glowAlpha;

                    sb.Draw(
                        texture,
                        drawPos,
                        frame,
                        glowColor,
                        Projectile.rotation,
                        origin,
                        Projectile.scale * glowScale,
                        SpriteEffects.None,
                        0
                    );
                }
            }

            //绘制拖尾
            if (State == GroundSmashState.Descending) {
                for (int i = 0; i < Projectile.oldPos.Length; i++) {
                    if (i >= Projectile.oldPos.Length - 1) continue;

                    float progress = i / (float)Projectile.oldPos.Length;
                    float trailAlpha = (1f - progress) * 0.6f;
                    Color trailColor = Color.Lerp(Color.Red, Color.Crimson, progress) * trailAlpha;

                    sb.Draw(
                        texture,
                        Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition,
                        frame,
                        trailColor,
                        Projectile.oldRot[i],
                        origin,
                        Projectile.scale * (1f - progress * 0.3f),
                        SpriteEffects.None,
                        0
                    );
                }
            }

            //绘制主体
            sb.Draw(
                texture,
                drawPos,
                frame,
                lightColor,
                Projectile.rotation,
                origin,
                Projectile.scale,
                SpriteEffects.None,
                0
            );

            //绘制特效
            DrawEffects(sb);

            return false;
        }

        private void DrawEffects(SpriteBatch sb) {
            //绘制闪电
            foreach (var lightning in lightningEffects) {
                lightning.Draw(sb);
            }

            //绘制冲击波
            foreach (var shockwave in shockwaves) {
                shockwave.Draw(sb);
            }
        }
    }

    #region 特效类
    ///<summary>
    ///闪电效果
    ///</summary>
    internal class LightningParticle
    {
        public Vector2 StartPos;
        public Vector2 Direction;
        public float Length;
        public int MaxLife;
        public int Life;
        public List<Vector2> Points = new();

        public LightningParticle(Vector2 start, Vector2 direction, float length, int life) {
            StartPos = start;
            Direction = direction.SafeNormalize(Vector2.UnitX);
            Length = length;
            MaxLife = life;
            Life = 0;
            GeneratePoints();
        }

        private void GeneratePoints() {
            int segments = (int)(Length / 15f);
            Vector2 currentPos = StartPos;
            Points.Add(currentPos);

            for (int i = 0; i < segments; i++) {
                float segmentLength = Length / segments;
                Vector2 offset = Direction.RotatedByRandom(0.5f) * segmentLength;
                offset += Main.rand.NextVector2Circular(8f, 8f);
                currentPos += offset;
                Points.Add(currentPos);
            }
        }

        public void Update() {
            Life++;
        }

        public void Draw(SpriteBatch sb) {
            float alpha = 1f - (Life / (float)MaxLife);
            if (alpha <= 0.05f) return;

            Texture2D pixel = VaultAsset.placeholder2.Value;

            for (int i = 0; i < Points.Count - 1; i++) {
                Vector2 start = Points[i];
                Vector2 end = Points[i + 1];
                Vector2 diff = end - start;
                float length = diff.Length();
                float rotation = diff.ToRotation();

                Color color = Color.Lerp(Color.Red, Color.Yellow, Main.rand.NextFloat()) * alpha * 0.8f;

                sb.Draw(
                    pixel,
                    start - Main.screenPosition,
                    null,
                    color,
                    rotation,
                    Vector2.Zero,
                    new Vector2(length, 3f),
                    SpriteEffects.None,
                    0f
                );
            }
        }
    }

    ///<summary>
    ///冲击波环
    ///</summary>
    internal class ShockwaveRing
    {
        public Vector2 Center;
        public float Radius;
        public float MaxRadius;
        public int Life;
        public int MaxLife;
        public Color RingColor;

        public ShockwaveRing(Vector2 center, float maxRadius, int life) {
            Center = center;
            Radius = 0f;
            MaxRadius = maxRadius;
            Life = 0;
            MaxLife = life;
            RingColor = Color.Lerp(Color.Red, Color.OrangeRed, Main.rand.NextFloat());
        }

        public void Update() {
            Life++;
            float progress = Life / (float)MaxLife;
            Radius = MathHelper.Lerp(0f, MaxRadius, CWRUtils.EaseOutQuad(progress));
        }

        public bool ShouldRemove() => Life >= MaxLife;

        public void Draw(SpriteBatch sb) {
            float alpha = 1f - (Life / (float)MaxLife);
            if (alpha <= 0.05f) return;

            Texture2D pixel = VaultAsset.placeholder2.Value;
            int segments = 64;
            float angleStep = MathHelper.TwoPi / segments;

            for (int i = 0; i < segments; i++) {
                float angle1 = i * angleStep;
                float angle2 = (i + 1) * angleStep;

                Vector2 p1 = Center + angle1.ToRotationVector2() * Radius;
                Vector2 p2 = Center + angle2.ToRotationVector2() * Radius;

                Vector2 diff = p2 - p1;
                float length = diff.Length();
                float rotation = diff.ToRotation();

                Color color = RingColor * alpha * 0.6f;

                sb.Draw(
                    pixel,
                    p1 - Main.screenPosition,
                    null,
                    color,
                    rotation,
                    Vector2.Zero,
                    new Vector2(length, 3f + alpha * 2f),
                    SpriteEffects.None,
                    0f
                );
            }
        }
    }
    #endregion
}
