using InnoVault.GameContent.BaseEntity;
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
    /// 寒霜鲦鱼技能，开火时周期性召唤并向敌人喷射雪花
    /// </summary>
    internal class FishFrostMinnow : FishSkill
    {
        public override int UnlockFishID => ItemID.FrostMinnow;
        public override int DefaultCooldown => (int)(90 - HalibutData.GetDomainLayer() * 4.5);
        public override int ResearchDuration => 60 * 16;
        public override bool? Shoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source,
            Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            if (Cooldown <= 0) {
                SetCooldown();

                //在玩家侧方召唤寒霜鲦鱼
                SpawnFrostMinnowSpitter(player, source, damage, knockback);
            }

            return null;
        }

        private static void SpawnFrostMinnowSpitter(Player player, EntitySource_ItemUse_WithAmmo source, int damage, float knockback) {
            //在玩家侧方生成
            float sideOffset = player.direction * 100f;
            Vector2 spawnPos = player.Center + new Vector2(sideOffset, -80f);

            int frostProj = Projectile.NewProjectile(
                source,
                spawnPos,
                Vector2.Zero,
                ModContent.ProjectileType<FrostMinnowSpitterProjectile>(),
                (int)(damage * (0.8f + HalibutData.GetDomainLayer() * 0.2f)),
                knockback,
                player.whoAmI
            );

            if (frostProj >= 0) {
                Main.projectile[frostProj].netUpdate = true;
            }

            //寒霜召唤音效
            SoundEngine.PlaySound(SoundID.Item28 with {
                Volume = 0.5f,
                Pitch = -0.4f
            }, spawnPos);

            SoundEngine.PlaySound(SoundID.Item30 with {
                Volume = 0.4f,
                Pitch = 0.3f
            }, spawnPos);
        }
    }

    /// <summary>
    /// 寒霜鲦鱼喷射器弹幕
    /// </summary>
    internal class FrostMinnowSpitterProjectile : BaseHeldProj
    {
        public override string Texture => "Terraria/Images/Item_" + ItemID.FrostMinnow;

        private enum FishState
        {
            Appearing,   //出现
            Charging,    //蓄力
            Spitting,    //喷射
            Fading       //消失
        }

        private ref float StateRaw => ref Projectile.ai[0];
        private ref float StateTimer => ref Projectile.ai[1];
        private ref float ChargeProgress => ref Projectile.localAI[0];

        private FishState State {
            get => (FishState)StateRaw;
            set => StateRaw = (float)value;
        }

        private int targetNPCID = -1;
        private float glowIntensity = 0f;
        private float pulsePhase = 0f;
        private float frostAura = 0f;
        private readonly List<Vector2> trailPositions = new();
        private const int MaxTrailLength = 12;

        //状态持续时间
        private const int AppearDuration = 18;
        private const int ChargeDuration = 28;
        private const int SpitDuration = 45;
        private const int FadeDuration = 22;

        //攻击参数
        private const float SearchRange = 1400f;
        private static int SnowflakeCount => 6 + HalibutData.GetDomainLayer() / 2; //喷射雪花数量

        public override void SetStaticDefaults() {
            Main.projFrames[Projectile.type] = 1;
        }

        public override void SetDefaults() {
            Projectile.width = 38;
            Projectile.height = 38;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Generic;
            Projectile.penetrate = -1;
            Projectile.timeLeft = AppearDuration + ChargeDuration + SpitDuration + FadeDuration + 10;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.alpha = 255;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
        }

        public override bool? CanDamage() => false; //鱼本身不造成伤害，只有雪花造成伤害

        public override void AI() {
            if (!Owner.active || Owner.dead) {
                Projectile.Kill();
                return;
            }

            StateTimer++;
            pulsePhase += 0.18f;
            frostAura += 0.12f;

            //状态机
            switch (State) {
                case FishState.Appearing:
                    AppearingBehavior(Owner);
                    break;
                case FishState.Charging:
                    ChargingBehavior(Owner);
                    break;
                case FishState.Spitting:
                    SpittingBehavior(Owner);
                    break;
                case FishState.Fading:
                    FadingBehavior();
                    break;
            }

            //更新拖尾
            UpdateTrail();

            //寒霜环境光照 - 冰蓝色
            float pulse = (float)Math.Sin(pulsePhase) * 0.3f + 0.7f;
            Lighting.AddLight(Projectile.Center, 0.4f * pulse * glowIntensity, 0.8f * pulse * glowIntensity, 1.2f * pulse * glowIntensity);

            //寒霜环境粒子
            if (glowIntensity > 0.3f && Main.rand.NextBool(5)) {
                SpawnFrostAmbient();
            }

            //旋转朝向目标
            if (State == FishState.Charging || State == FishState.Spitting) {
                if (IsTargetValid()) {
                    NPC target = Main.npc[targetNPCID];
                    Vector2 toTarget = target.Center - Projectile.Center;
                    Projectile.rotation = toTarget.ToRotation();
                }
                else {
                    Vector2 toTarget = InMousePos - Projectile.Center;
                    Projectile.rotation = toTarget.ToRotation();
                }
            }
        }

        private void AppearingBehavior(Player owner) {
            float progress = StateTimer / AppearDuration;

            //淡入
            Projectile.alpha = (int)(255 * (1f - progress));
            glowIntensity = progress;
            Projectile.scale = progress * 0.8f;

            //冰晶凝聚效果
            float floatY = (float)Math.Sin(pulsePhase * 1.2f) * 2.5f;
            Projectile.Center = Projectile.Center + new Vector2(0, floatY * 0.1f);

            //出现时冰晶粒子
            if (Main.rand.NextBool(3)) {
                SpawnAppearDust();
            }

            if (StateTimer >= AppearDuration) {
                State = FishState.Charging;
                StateTimer = 0;

                //搜索目标
                NPC target = owner.Center.FindClosestNPC(SearchRange);
                if (target != null) {
                    targetNPCID = target.whoAmI;
                }
            }
        }

        private void ChargingBehavior(Player owner) {
            float progress = StateTimer / ChargeDuration;
            ChargeProgress = progress;

            //蓄力时发光强度增加
            glowIntensity = 0.6f + progress * 0.4f;

            //寒气凝聚效果
            float floatY = (float)Math.Sin(pulsePhase * 1.5f) * 3f;
            Projectile.Center = Projectile.Center + new Vector2(0, floatY * 0.08f);

            //冰晶逐渐聚集
            Projectile.scale = 0.8f + progress * 0.4f;

            //蓄力时持续生成寒霜粒子
            if (Main.rand.NextBool(2)) {
                SpawnChargeDust();
            }

            //蓄力音效 - 寒冰凝聚
            if (StateTimer % 12 == 0) {
                SoundEngine.PlaySound(SoundID.Item30 with {
                    Volume = 0.3f * progress,
                    Pitch = -0.6f + progress * 0.4f
                }, Projectile.Center);
            }

            if (StateTimer >= ChargeDuration) {
                State = FishState.Spitting;
                StateTimer = 0;

                //开始喷射
                SpitFrostSnowflakes(owner);

                //喷射音效
                SoundEngine.PlaySound(SoundID.Item28 with {
                    Volume = 0.9f,
                    Pitch = -0.2f
                }, Projectile.Center);

                SoundEngine.PlaySound(SoundID.Item120 with {
                    Volume = 0.7f,
                    Pitch = 0.3f
                }, Projectile.Center);
            }
        }

        private void SpittingBehavior(Player owner) {
            float progress = StateTimer / SpitDuration;

            //喷射时保持强烈发光
            glowIntensity = 1f - progress * 0.3f;

            //喷射时寒气扩散
            frostAura = 1f - progress * 0.5f;

            //喷射时后坐力效果
            if (IsTargetValid()) {
                NPC target = Main.npc[targetNPCID];
                Vector2 toTarget = target.Center - Projectile.Center;
                Vector2 recoil = -toTarget.SafeNormalize(Vector2.Zero) * (1f - progress) * 1.8f;
                Projectile.Center += recoil * 0.08f;
            }
            else {
                Vector2 toTarget = InMousePos - Projectile.Center;
                Vector2 recoil = -toTarget.SafeNormalize(Vector2.Zero) * (1f - progress) * 1.8f;
                Projectile.Center += recoil * 0.08f;
            }

            //持续漂浮
            float floatY = (float)Math.Sin(pulsePhase) * 2f;
            Projectile.Center = Projectile.Center + new Vector2(0, floatY * 0.05f);

            //喷射时持续生成冰晶粒子
            if (Main.rand.NextBool(2)) {
                SpawnSpitEffect();
            }

            if (StateTimer >= SpitDuration) {
                State = FishState.Fading;
                StateTimer = 0;
            }
        }

        private void FadingBehavior() {
            float progress = StateTimer / FadeDuration;

            //淡出
            Projectile.alpha = (int)(255 * progress);
            glowIntensity = 1f - progress;
            Projectile.scale = 1.2f - progress * 0.6f;

            //化作冰雾消散
            Projectile.velocity.Y -= 0.15f;

            if (StateTimer >= FadeDuration) {
                Projectile.Kill();
            }
        }

        private void SpitFrostSnowflakes(Player owner) {
            if (Main.myPlayer != Projectile.owner) return;


            Vector2 targetCenter = InMousePos;
            if (IsTargetValid()) {
                NPC target = Main.npc[targetNPCID];
                targetCenter = target.Center;
            }

            //从鱼嘴位置喷射
            Vector2 mouthPos = Projectile.Center + Projectile.rotation.ToRotationVector2() * 18f;
            Vector2 toTarget = (targetCenter - mouthPos).SafeNormalize(Vector2.Zero);

            //喷射扇形雪花
            for (int i = 0; i < SnowflakeCount; i++) {
                float spreadAngle = MathHelper.Lerp(-0.6f, 0.6f, i / (float)(SnowflakeCount - 1));
                Vector2 velocity = toTarget.RotatedBy(spreadAngle) * Main.rand.NextFloat(10f, 16f);

                int proj = Projectile.NewProjectile(
                    Projectile.GetSource_FromThis(),
                    mouthPos,
                    velocity,
                    ModContent.ProjectileType<FrostSnowflakeProjectile>(),
                    Projectile.damage,
                    2f,
                    Projectile.owner
                );
                Main.projectile[proj].friendly = true;
            }

            //喷射爆发特效 - 冰晶爆发
            for (int i = 0; i < 50; i++) {
                Vector2 velocity = toTarget.RotatedByRandom(0.9f) * Main.rand.NextFloat(6f, 18f);
                Dust frost = Dust.NewDustPerfect(
                    mouthPos,
                    DustID.IceTorch,
                    velocity,
                    0,
                    new Color(200, 230, 255),
                    Main.rand.NextFloat(2f, 3.5f)
                );
                frost.noGravity = true;
                frost.fadeIn = 1.5f;
            }

            //冰雪爆发
            for (int i = 0; i < 35; i++) {
                Vector2 velocity = toTarget.RotatedByRandom(0.7f) * Main.rand.NextFloat(5f, 14f);
                Dust snow = Dust.NewDustPerfect(
                    mouthPos,
                    DustID.SnowflakeIce,
                    velocity,
                    0,
                    default,
                    Main.rand.NextFloat(2.5f, 4f)
                );
                snow.noGravity = true;
            }

            //寒霜冲击环
            for (int i = 0; i < 25; i++) {
                float angle = MathHelper.TwoPi * i / 25f;
                Vector2 velocity = angle.ToRotationVector2() * 9f;
                Dust ring = Dust.NewDustPerfect(
                    mouthPos,
                    DustID.IceTorch,
                    velocity,
                    0,
                    new Color(180, 220, 255),
                    Main.rand.NextFloat(2f, 3f)
                );
                ring.noGravity = true;
            }
        }

        private void UpdateTrail() {
            trailPositions.Insert(0, Projectile.Center);
            if (trailPositions.Count > MaxTrailLength) {
                trailPositions.RemoveAt(trailPositions.Count - 1);
            }
        }

        private bool IsTargetValid() {
            if (targetNPCID < 0 || targetNPCID >= Main.maxNPCs) return false;
            NPC target = Main.npc[targetNPCID];
            return target.active && target.CanBeChasedBy();
        }

        //特效方法
        private void SpawnFrostAmbient() {
            Dust frost = Dust.NewDustPerfect(
                Projectile.Center + Main.rand.NextVector2Circular(25f, 25f),
                DustID.IceTorch,
                Main.rand.NextVector2Circular(1.5f, 1.5f),
                0,
                new Color(200, 230, 255),
                Main.rand.NextFloat(1f, 1.5f)
            );
            frost.noGravity = true;
        }

        private void SpawnAppearDust() {
            for (int i = 0; i < 3; i++) {
                Vector2 velocity = Main.rand.NextVector2Circular(3f, 3f);
                Dust frost = Dust.NewDustPerfect(
                    Projectile.Center + Main.rand.NextVector2Circular(20f, 20f),
                    DustID.IceTorch,
                    velocity,
                    0,
                    new Color(180, 220, 255),
                    Main.rand.NextFloat(1.5f, 2.5f)
                );
                frost.noGravity = true;
            }

            //雪花
            if (Main.rand.NextBool()) {
                Dust snow = Dust.NewDustPerfect(
                    Projectile.Center,
                    DustID.SnowflakeIce,
                    Main.rand.NextVector2Circular(2f, 2f),
                    0,
                    default,
                    Main.rand.NextFloat(1.2f, 2f)
                );
                snow.noGravity = true;
            }
        }

        private void SpawnChargeDust() {
            //鱼嘴位置
            Vector2 mouthPos = Projectile.Center + Projectile.rotation.ToRotationVector2() * 15f;

            for (int i = 0; i < 3; i++) {
                Vector2 velocity = -Projectile.rotation.ToRotationVector2().RotatedByRandom(0.35f) * Main.rand.NextFloat(2f, 5f);
                Dust frost = Dust.NewDustPerfect(
                    mouthPos + Main.rand.NextVector2Circular(12f, 12f),
                    DustID.IceTorch,
                    velocity,
                    0,
                    new Color(200, 230, 255),
                    Main.rand.NextFloat(2f, 3.5f)
                );
                frost.noGravity = true;
                frost.fadeIn = 1.6f;
            }

            //雪花核心
            if (Main.rand.NextBool()) {
                Dust snow = Dust.NewDustPerfect(
                    mouthPos,
                    DustID.SnowflakeIce,
                    -Projectile.rotation.ToRotationVector2() * Main.rand.NextFloat(1f, 3f),
                    0,
                    default,
                    Main.rand.NextFloat(1.8f, 3f)
                );
                snow.noGravity = true;
            }
        }

        private void SpawnSpitEffect() {
            Vector2 mouthPos = Projectile.Center + Projectile.rotation.ToRotationVector2() * 20f;

            for (int i = 0; i < 2; i++) {
                Vector2 velocity = Projectile.rotation.ToRotationVector2().RotatedByRandom(0.5f) * Main.rand.NextFloat(5f, 11f);
                Dust frost = Dust.NewDustPerfect(
                    mouthPos,
                    DustID.IceTorch,
                    velocity,
                    0,
                    new Color(200, 230, 255),
                    Main.rand.NextFloat(2f, 3.5f)
                );
                frost.noGravity = true;
                frost.fadeIn = 1.5f;
            }

            //雪花
            if (Main.rand.NextBool()) {
                Dust snow = Dust.NewDustPerfect(
                    mouthPos,
                    DustID.SnowflakeIce,
                    Projectile.rotation.ToRotationVector2() * Main.rand.NextFloat(4f, 9f),
                    0,
                    default,
                    Main.rand.NextFloat(2.5f, 3.5f)
                );
                snow.noGravity = true;
            }
        }

        public override void OnKill(int timeLeft) {
            //消散效果 - 化作冰雾
            for (int i = 0; i < 40; i++) {
                Vector2 velocity = Main.rand.NextVector2Circular(7f, 7f);
                Dust frost = Dust.NewDustPerfect(
                    Projectile.Center,
                    DustID.IceTorch,
                    velocity,
                    0,
                    new Color(200, 230, 255),
                    Main.rand.NextFloat(2f, 4f)
                );
                frost.noGravity = true;
            }

            //雪花飘散
            for (int i = 0; i < 25; i++) {
                Dust snow = Dust.NewDustPerfect(
                    Projectile.Center,
                    DustID.SnowflakeIce,
                    Main.rand.NextVector2Circular(6f, 6f),
                    0,
                    default,
                    Main.rand.NextFloat(2f, 3.5f)
                );
                snow.noGravity = true;
            }

            SoundEngine.PlaySound(SoundID.Item30 with {
                Volume = 0.6f,
                Pitch = -0.5f
            }, Projectile.Center);
        }

        public override bool PreDraw(ref Color lightColor) {
            SpriteBatch sb = Main.spriteBatch;
            Texture2D fishTex = TextureAssets.Item[ItemID.FrostMinnow].Value;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            Vector2 origin = fishTex.Size() / 2f;
            bool dir = Projectile.rotation.ToRotationVector2().X > 0;
            SpriteEffects spriteEffects = dir ? SpriteEffects.None : SpriteEffects.FlipVertically;
            float drawRot = Projectile.rotation + (dir ? MathHelper.PiOver4 : -MathHelper.PiOver4);

            float alpha = (255f - Projectile.alpha) / 255f;

            //绘制寒霜拖尾
            DrawFrostTrail(sb, fishTex, origin, alpha);

            //寒霜光晕层 - 冰蓝色
            if (glowIntensity > 0.5f) {
                for (int i = 0; i < 4; i++) {
                    float glowScale = Projectile.scale * (1.2f + i * 0.18f);
                    float glowAlpha = (glowIntensity - 0.5f) * (1f - i * 0.25f) * 0.8f * alpha;

                    sb.Draw(
                        fishTex,
                        drawPos,
                        null,
                        new Color(100, 180, 255, 0) * glowAlpha,
                        drawRot,
                        origin,
                        glowScale,
                        spriteEffects,
                        0
                    );
                }
            }

            //蓄力寒气效果
            if (State == FishState.Charging) {
                float chargeGlow = ChargeProgress;
                for (int i = 0; i < 3; i++) {
                    float chargeScale = Projectile.scale * (1.4f + i * 0.25f);
                    float chargeAlpha = chargeGlow * (1f - i * 0.3f) * 0.7f * alpha;

                    sb.Draw(
                        fishTex,
                        drawPos,
                        null,
                        new Color(150, 220, 255, 0) * chargeAlpha,
                        drawRot,
                        origin,
                        chargeScale,
                        spriteEffects,
                        0
                    );
                }
            }

            //寒霜光环效果
            if (frostAura > 0.5f) {
                float auraScale = Projectile.scale * (1.5f + (float)Math.Sin(frostAura * 3f) * 0.2f);
                sb.Draw(
                    fishTex,
                    drawPos,
                    null,
                    new Color(180, 230, 255, 0) * frostAura * 0.5f * alpha,
                    drawRot,
                    origin,
                    auraScale,
                    spriteEffects,
                    0
                );
            }

            //主体绘制 - 冰霜蓝色调
            Color mainColor = Color.Lerp(
                lightColor,
                new Color(180, 220, 255),
                glowIntensity * 0.7f
            );

            sb.Draw(
                fishTex,
                drawPos,
                null,
                mainColor * alpha,
                drawRot,
                origin,
                Projectile.scale,
                spriteEffects,
                0
            );

            //脉冲冰霜效果
            float pulseIntensity = 0.5f + (float)Math.Sin(pulsePhase) * 0.3f;
            sb.Draw(
                fishTex,
                drawPos,
                null,
                new Color(200, 240, 255, 0) * pulseIntensity * glowIntensity * alpha,
                drawRot,
                origin,
                Projectile.scale * 1.15f,
                spriteEffects,
                0
            );

            //白色寒冰核心
            if (glowIntensity > 0.7f) {
                sb.Draw(
                    fishTex,
                    drawPos,
                    null,
                    Color.White * glowIntensity * 0.6f * alpha,
                    drawRot,
                    origin,
                    Projectile.scale * 0.85f,
                    spriteEffects,
                    0
                );
            }

            return false;
        }

        private void DrawFrostTrail(SpriteBatch sb, Texture2D texture, Vector2 origin, float alpha) {
            if (trailPositions.Count < 2) return;
            bool dir = Projectile.rotation.ToRotationVector2().X > 0;
            SpriteEffects spriteEffects = dir ? SpriteEffects.None : SpriteEffects.FlipVertically;
            float drawRot = Projectile.rotation + (dir ? MathHelper.PiOver4 : -MathHelper.PiOver4);
            for (int i = 1; i < trailPositions.Count; i++) {
                float progress = 1f - i / (float)trailPositions.Count;
                float trailAlpha = progress * alpha * 0.6f;
                float trailScale = Projectile.scale * MathHelper.Lerp(0.6f, 1f, progress);

                //寒霜渐变色 - 冰蓝到白色
                Color trailColor = Color.Lerp(
                    new Color(120, 180, 220),
                    new Color(200, 240, 255),
                    progress
                ) * trailAlpha;

                Vector2 trailPos = trailPositions[i] - Main.screenPosition;

                sb.Draw(
                    texture,
                    trailPos,
                    null,
                    trailColor,
                    drawRot - i * 0.08f,
                    origin,
                    trailScale,
                    spriteEffects,
                    0
                );
            }
        }
    }

    /// <summary>
    /// 寒霜雪花弹幕
    /// </summary>
    internal class FrostSnowflakeProjectile : ModProjectile
    {
        public override string Texture => CWRConstant.Placeholder;

        private ref float Timer => ref Projectile.ai[0];
        private float rotationSpeed = 0f;
        private float freezeIntensity = 0f;

        public override void SetDefaults() {
            Projectile.width = 20;
            Projectile.height = 20;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Generic;
            Projectile.penetrate = 3;
            Projectile.timeLeft = 140;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = false;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 20;

            rotationSpeed = Main.rand.NextFloat(-0.25f, 0.25f);
        }

        public override void AI() {
            Timer++;

            //减速
            Projectile.velocity *= 0.985f;

            //轻微下坠
            Projectile.velocity.Y += 0.08f;

            //追踪最近的敌人
            if (Timer % 18 == 0 && Timer < 70) {
                NPC target = Projectile.Center.FindClosestNPC(450f);
                if (target != null) {
                    Vector2 toTarget = target.Center - Projectile.Center;
                    Projectile.velocity += toTarget.SafeNormalize(Vector2.Zero) * 0.9f;

                    if (Projectile.velocity.Length() > 18f) {
                        Projectile.velocity = Projectile.velocity.SafeNormalize(Vector2.Zero) * 18f;
                    }
                }
            }

            //旋转
            Projectile.rotation += rotationSpeed;

            //寒霜光照
            Lighting.AddLight(Projectile.Center, 0.4f, 0.7f, 1f);

            //冰晶轨迹
            if (Main.rand.NextBool(3)) {
                Dust frost = Dust.NewDustDirect(
                    Projectile.position,
                    Projectile.width,
                    Projectile.height,
                    DustID.IceTorch,
                    0, 0, 0,
                    new Color(200, 230, 255),
                    Main.rand.NextFloat(1.2f, 2f)
                );
                frost.velocity = -Projectile.velocity * 0.3f;
                frost.noGravity = true;
            }

            //雪花尾迹
            if (Main.rand.NextBool(2)) {
                Dust snow = Dust.NewDustDirect(
                    Projectile.position,
                    Projectile.width,
                    Projectile.height,
                    DustID.SnowflakeIce,
                    0, 0, 0,
                    default,
                    Main.rand.NextFloat(1.5f, 2.5f)
                );
                snow.velocity = -Projectile.velocity * 0.2f;
                snow.noGravity = true;
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            //冰冻敌人
            target.AddBuff(BuffID.Frostburn, 180);

            //寒霜击中爆发
            for (int i = 0; i < 18; i++) {
                Vector2 velocity = Main.rand.NextVector2Circular(7f, 7f);
                Dust frost = Dust.NewDustPerfect(
                    Projectile.Center,
                    DustID.IceTorch,
                    velocity,
                    0,
                    new Color(200, 230, 255),
                    Main.rand.NextFloat(1.5f, 3f)
                );
                frost.noGravity = true;
            }

            //雪花爆发
            for (int i = 0; i < 10; i++) {
                Dust snow = Dust.NewDustPerfect(
                    Projectile.Center,
                    DustID.SnowflakeIce,
                    Main.rand.NextVector2Circular(6f, 6f),
                    0,
                    default,
                    Main.rand.NextFloat(2f, 3f)
                );
                snow.noGravity = true;
            }

            SoundEngine.PlaySound(SoundID.Item30 with {
                Volume = 0.5f,
                Pitch = 0.3f
            }, Projectile.Center);
        }

        public override void OnKill(int timeLeft) {
            //消散爆发 - 冰雾
            for (int i = 0; i < 25; i++) {
                Vector2 velocity = Main.rand.NextVector2Circular(9f, 9f);
                Dust frost = Dust.NewDustPerfect(
                    Projectile.Center,
                    DustID.IceTorch,
                    velocity,
                    0,
                    new Color(200, 230, 255),
                    Main.rand.NextFloat(2f, 3.5f)
                );
                frost.noGravity = true;
            }

            //雪花
            for (int i = 0; i < 12; i++) {
                Dust snow = Dust.NewDustPerfect(
                    Projectile.Center,
                    DustID.SnowflakeIce,
                    Main.rand.NextVector2Circular(7f, 7f),
                    0,
                    default,
                    Main.rand.NextFloat(2.5f, 3.5f)
                );
                snow.noGravity = true;
            }

            SoundEngine.PlaySound(SoundID.Item27 with {
                Volume = 0.5f,
                Pitch = -0.3f
            }, Projectile.Center);
        }

        public override bool PreDraw(ref Color lightColor) {
            //雪花形状绘制
            Texture2D glowTex = CWRAsset.SoftGlow.Value;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;

            //外层冰霜光晕
            for (int i = 0; i < 3; i++) {
                float scale = 0.45f + i * 0.2f;
                float alpha = (1f - i * 0.3f) * 0.9f;

                Main.spriteBatch.Draw(
                    glowTex,
                    drawPos,
                    null,
                    new Color(100, 180, 255, 0) * alpha,
                    Projectile.rotation,
                    glowTex.Size() / 2f,
                    scale,
                    SpriteEffects.None,
                    0
                );

                //交叉旋转形成雪花形状
                Main.spriteBatch.Draw(
                    glowTex,
                    drawPos,
                    null,
                    new Color(150, 220, 255, 0) * alpha,
                    Projectile.rotation + MathHelper.PiOver2,
                    glowTex.Size() / 2f,
                    scale * 0.8f,
                    SpriteEffects.None,
                    0
                );
            }

            //核心亮点
            Main.spriteBatch.Draw(
                glowTex,
                drawPos,
                null,
                Color.White with { A = 0 } * 0.95f,
                Projectile.rotation,
                glowTex.Size() / 2f,
                0.28f,
                SpriteEffects.None,
                0
            );

            return false;
        }
    }
}
