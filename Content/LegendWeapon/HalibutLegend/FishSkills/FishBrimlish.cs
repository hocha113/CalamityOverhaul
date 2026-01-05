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
    /// 硫磺火鱼技能，开火时召唤硫磺火鱼在身后喷射火焰
    /// </summary>
    internal class FishBrimlish : FishSkill
    {
        public override int UnlockFishID => CWRID.Item_Brimlish;
        public override int DefaultCooldown => 20;
        public override int ResearchDuration => 60 * 14;

        private int shootCounter = 0;
        private static int ShootInterval => 8 - HalibutData.GetDomainLayer() / 3; //每8次开火触发一次

        public override bool? Shoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source,
            Vector2 position, Vector2 velocity, int type, int damage, float knockback) {

            shootCounter++;

            if (shootCounter >= ShootInterval && Cooldown <= 0) {
                shootCounter = 0;
                SetCooldown();

                //在玩家身后召唤硫磺火鱼
                SpawnBrimfishSpitter(player, source, damage, knockback);
            }

            return null;
        }

        private void SpawnBrimfishSpitter(Player player, EntitySource_ItemUse_WithAmmo source, int damage, float knockback) {
            //在玩家后方生成
            Vector2 behindPlayer = player.Center - new Vector2(player.direction * 120f, 60f);

            int brimfishProj = Projectile.NewProjectile(
                source,
                behindPlayer,
                Vector2.Zero,
                ModContent.ProjectileType<BrimfishSpitterProjectile>(),
                (int)(damage * (0.8f + HalibutData.GetDomainLayer() * 0.2f)),
                knockback,
                player.whoAmI
            );

            if (brimfishProj >= 0) {
                Main.projectile[brimfishProj].netUpdate = true;
            }

            //硫磺火召唤音效
            SoundEngine.PlaySound(SoundID.DD2_BetsyFireballShot with {
                Volume = 0.5f,
                Pitch = -0.3f
            }, behindPlayer);
        }
    }

    /// <summary>
    /// 硫磺火鱼喷射器弹幕
    /// </summary>
    internal class BrimfishSpitterProjectile : ModProjectile
    {
        public override string Texture => "CalamityMod/Items/Fishing/BrimstoneCragCatches/Brimlish";
        public override bool IsLoadingEnabled(Mod mod) => CWRRef.Has;

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
        private readonly List<Vector2> trailPositions = new();
        private const int MaxTrailLength = 12;

        //状态持续时间
        private const int AppearDuration = 15;
        private const int ChargeDuration = 25;
        private const int SpitDuration = 40;
        private const int FadeDuration = 20;

        //攻击参数
        private const float SearchRange = 1200f;
        private static int FlameCount => 6 + HalibutData.GetDomainLayer() / 2; //喷射火焰数量

        public override void SetStaticDefaults() {
            Main.projFrames[Projectile.type] = 1;
        }

        public override void SetDefaults() {
            Projectile.width = 40;
            Projectile.height = 40;
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

        public override bool? CanDamage() => false; //鱼本身不造成伤害，只有火焰造成伤害

        public override void AI() {
            Player owner = Main.player[Projectile.owner];
            if (!owner.active || owner.dead) {
                Projectile.Kill();
                return;
            }

            StateTimer++;
            pulsePhase += 0.2f;

            //状态机
            switch (State) {
                case FishState.Appearing:
                    AppearingBehavior(owner);
                    break;
                case FishState.Charging:
                    ChargingBehavior(owner);
                    break;
                case FishState.Spitting:
                    SpittingBehavior(owner);
                    break;
                case FishState.Fading:
                    FadingBehavior();
                    break;
            }

            //更新拖尾
            UpdateTrail();

            //硫磺火环境光照
            float pulse = (float)Math.Sin(pulsePhase) * 0.3f + 0.7f;
            Lighting.AddLight(Projectile.Center, 0.8f * pulse * glowIntensity, 0.2f * pulse * glowIntensity, 0.1f * pulse * glowIntensity);

            //硫磺火环境粒子
            if (glowIntensity > 0.3f && Main.rand.NextBool(4)) {
                SpawnBrimstoneAmbient();
            }

            //旋转朝向目标
            if (State == FishState.Charging || State == FishState.Spitting) {
                if (IsTargetValid()) {
                    NPC target = Main.npc[targetNPCID];
                    Vector2 toTarget = target.Center - Projectile.Center;
                    Projectile.rotation = MathHelper.Lerp(
                        Projectile.rotation,
                        toTarget.ToRotation() + MathHelper.PiOver4,
                        0.15f
                    );
                }
            }
        }

        private void AppearingBehavior(Player owner) {
            float progress = StateTimer / AppearDuration;

            //淡入
            Projectile.alpha = (int)(255 * (1f - progress));
            glowIntensity = progress;
            Projectile.scale = progress;

            //轻微漂浮
            float floatY = (float)Math.Sin(pulsePhase * 0.8f) * 2f;
            Projectile.Center = Projectile.Center + new Vector2(0, floatY * 0.1f);

            //出现时粒子效果
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

            //轻微漂浮
            float floatY = (float)Math.Sin(pulsePhase * 1.2f) * 3f;
            Projectile.Center = Projectile.Center + new Vector2(0, floatY * 0.1f);

            //鱼嘴逐渐张开效果（通过缩放模拟）
            Projectile.scale = 1f + progress * 0.3f;

            //蓄力时持续生成硫磺火粒子
            if (Main.rand.NextBool(2)) {
                SpawnChargeDust();
            }

            //蓄力音效
            if (StateTimer % 10 == 0) {
                SoundEngine.PlaySound(SoundID.DD2_BetsyFlameBreath with {
                    Volume = 0.3f * progress,
                    Pitch = -0.5f + progress * 0.3f
                }, Projectile.Center);
            }

            if (StateTimer >= ChargeDuration) {
                State = FishState.Spitting;
                StateTimer = 0;

                //开始喷射
                SpitBrimstoneFlames(owner);

                //喷射音效
                SoundEngine.PlaySound(SoundID.Item74 with {
                    Volume = 0.9f,
                    Pitch = -0.3f
                }, Projectile.Center);
            }
        }

        private void SpittingBehavior(Player owner) {
            float progress = StateTimer / SpitDuration;

            //喷射时保持强烈发光
            glowIntensity = 1f - progress * 0.3f;

            //喷射时后坐力效果
            if (IsTargetValid()) {
                NPC target = Main.npc[targetNPCID];
                Vector2 toTarget = target.Center - Projectile.Center;
                Vector2 recoil = -toTarget.SafeNormalize(Vector2.Zero) * (1f - progress) * 2f;
                Projectile.Center += recoil * 0.1f;
            }

            //持续漂浮
            float floatY = (float)Math.Sin(pulsePhase) * 2f;
            Projectile.Center = Projectile.Center + new Vector2(0, floatY * 0.05f);

            //喷射时持续生成火焰粒子
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
            Projectile.scale = 1f - progress * 0.5f;

            //缓慢下沉
            Projectile.velocity.Y += 0.2f;

            if (StateTimer >= FadeDuration) {
                Projectile.Kill();
            }
        }

        private void SpitBrimstoneFlames(Player owner) {
            if (!IsTargetValid() || Main.myPlayer != Projectile.owner) return;

            NPC target = Main.npc[targetNPCID];

            //从鱼嘴位置喷射
            Vector2 mouthPos = Projectile.Center + Projectile.rotation.ToRotationVector2() * 20f;
            Vector2 toTarget = (target.Center - mouthPos).SafeNormalize(Vector2.Zero);

            //喷射扇形火焰
            for (int i = 0; i < FlameCount; i++) {
                float spreadAngle = MathHelper.Lerp(-0.5f, 0.5f, i / (float)(FlameCount - 1));
                Vector2 velocity = toTarget.RotatedBy(spreadAngle) * Main.rand.NextFloat(12f, 18f);

                int proj = Projectile.NewProjectile(
                    Projectile.GetSource_FromThis(),
                    mouthPos,
                    velocity,
                    ModContent.ProjectileType<BrimstoneFlameProjectile>(),
                    Projectile.damage,
                    2f,
                    Projectile.owner
                );
                Main.projectile[proj].friendly = true;
            }

            //喷射爆发特效
            for (int i = 0; i < 40; i++) {
                Vector2 velocity = toTarget.RotatedByRandom(0.8f) * Main.rand.NextFloat(8f, 20f);
                Dust brimstone = Dust.NewDustPerfect(
                    mouthPos,
                    CWRID.Dust_Brimstone,
                    velocity,
                    0,
                    default,
                    Main.rand.NextFloat(2.5f, 4f)
                );
                brimstone.noGravity = true;
                brimstone.fadeIn = 1.6f;
            }

            //火焰核心爆发
            for (int i = 0; i < 30; i++) {
                Vector2 velocity = toTarget.RotatedByRandom(0.6f) * Main.rand.NextFloat(6f, 16f);
                Dust fire = Dust.NewDustPerfect(
                    mouthPos,
                    DustID.Torch,
                    velocity,
                    0,
                    Color.Red,
                    Main.rand.NextFloat(2.5f, 4f)
                );
                fire.noGravity = true;
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
        private void SpawnBrimstoneAmbient() {
            Dust brimstone = Dust.NewDustPerfect(
                Projectile.Center + Main.rand.NextVector2Circular(20f, 20f),
                CWRID.Dust_Brimstone,
                Main.rand.NextVector2Circular(1.5f, 1.5f),
                0,
                default,
                Main.rand.NextFloat(1f, 1.5f)
            );
            brimstone.noGravity = true;
        }

        private void SpawnAppearDust() {
            for (int i = 0; i < 2; i++) {
                Vector2 velocity = Main.rand.NextVector2Circular(3f, 3f);
                Dust brimstone = Dust.NewDustPerfect(
                    Projectile.Center + Main.rand.NextVector2Circular(15f, 15f),
                    CWRID.Dust_Brimstone,
                    velocity,
                    0,
                    default,
                    Main.rand.NextFloat(1.5f, 2f)
                );
                brimstone.noGravity = true;
            }
        }

        private void SpawnChargeDust() {
            //鱼嘴位置
            Vector2 mouthPos = Projectile.Center + Projectile.rotation.ToRotationVector2() * 15f;

            for (int i = 0; i < 3; i++) {
                Vector2 velocity = -Projectile.rotation.ToRotationVector2().RotatedByRandom(0.3f) * Main.rand.NextFloat(2f, 5f);
                Dust brimstone = Dust.NewDustPerfect(
                    mouthPos + Main.rand.NextVector2Circular(10f, 10f),
                    CWRID.Dust_Brimstone,
                    velocity,
                    0,
                    default,
                    Main.rand.NextFloat(2f, 3f)
                );
                brimstone.noGravity = true;
                brimstone.fadeIn = 1.5f;
            }

            //红色火焰核心
            if (Main.rand.NextBool()) {
                Dust fire = Dust.NewDustPerfect(
                    mouthPos,
                    DustID.Torch,
                    -Projectile.rotation.ToRotationVector2() * Main.rand.NextFloat(1f, 3f),
                    0,
                    Color.Red,
                    Main.rand.NextFloat(1.5f, 2.5f)
                );
                fire.noGravity = true;
            }
        }

        private void SpawnSpitEffect() {
            Vector2 mouthPos = Projectile.Center + Projectile.rotation.ToRotationVector2() * 20f;

            for (int i = 0; i < 2; i++) {
                Vector2 velocity = Projectile.rotation.ToRotationVector2().RotatedByRandom(0.4f) * Main.rand.NextFloat(6f, 12f);
                Dust brimstone = Dust.NewDustPerfect(
                    mouthPos,
                    CWRID.Dust_Brimstone,
                    velocity,
                    0,
                    default,
                    Main.rand.NextFloat(2f, 3.5f)
                );
                brimstone.noGravity = true;
                brimstone.fadeIn = 1.4f;
            }

            //火焰
            if (Main.rand.NextBool()) {
                Dust fire = Dust.NewDustPerfect(
                    mouthPos,
                    DustID.Torch,
                    Projectile.rotation.ToRotationVector2() * Main.rand.NextFloat(4f, 8f),
                    0,
                    Color.Red,
                    Main.rand.NextFloat(2f, 3f)
                );
                fire.noGravity = true;
            }
        }

        public override void OnKill(int timeLeft) {
            //消散效果
            for (int i = 0; i < 30; i++) {
                Vector2 velocity = Main.rand.NextVector2Circular(6f, 6f);
                Dust brimstone = Dust.NewDustPerfect(
                    Projectile.Center,
                    CWRID.Dust_Brimstone,
                    velocity,
                    0,
                    default,
                    Main.rand.NextFloat(2f, 3.5f)
                );
                brimstone.noGravity = true;
            }

            //火焰余烬
            for (int i = 0; i < 15; i++) {
                Dust fire = Dust.NewDustPerfect(
                    Projectile.Center,
                    DustID.Torch,
                    Main.rand.NextVector2Circular(5f, 5f),
                    0,
                    Color.Red,
                    Main.rand.NextFloat(1.5f, 2.5f)
                );
                fire.noGravity = true;
            }

            SoundEngine.PlaySound(SoundID.Item74 with {
                Volume = 0.5f,
                Pitch = -0.5f
            }, Projectile.Center);
        }

        public override bool PreDraw(ref Color lightColor) {
            SpriteBatch sb = Main.spriteBatch;
            Texture2D fishTex = CWRUtils.GetT2DAsset(Texture).Value;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            Vector2 origin = fishTex.Size() / 2f;
            float drawRot = Projectile.rotation;
            float alpha = (255f - Projectile.alpha) / 255f;

            //绘制硫磺火拖尾
            DrawBrimstoneTrail(sb, fishTex, origin, alpha);

            //硫磺火发光层
            if (glowIntensity > 0.5f) {
                for (int i = 0; i < 4; i++) {
                    float glowScale = Projectile.scale * (1.2f + i * 0.15f);
                    float glowAlpha = (glowIntensity - 0.5f) * (1f - i * 0.25f) * 0.7f * alpha;

                    sb.Draw(
                        fishTex,
                        drawPos,
                        null,
                        new Color(255, 100, 50, 0) * glowAlpha,
                        drawRot,
                        origin,
                        glowScale,
                        SpriteEffects.None,
                        0
                    );
                }
            }

            //蓄力辉光效果
            if (State == FishState.Charging) {
                float chargeGlow = ChargeProgress;
                for (int i = 0; i < 3; i++) {
                    float chargeScale = Projectile.scale * (1.3f + i * 0.2f);
                    float chargeAlpha = chargeGlow * (1f - i * 0.3f) * 0.6f * alpha;

                    sb.Draw(
                        fishTex,
                        drawPos,
                        null,
                        new Color(255, 80, 40, 0) * chargeAlpha,
                        drawRot,
                        origin,
                        chargeScale,
                        SpriteEffects.None,
                        0
                    );
                }
            }

            //主体绘制 - 硫磺火红橙色调
            Color mainColor = Color.Lerp(
                lightColor,
                new Color(255, 120, 60),
                glowIntensity * 0.6f
            );

            sb.Draw(
                fishTex,
                drawPos,
                null,
                mainColor * alpha,
                drawRot,
                origin,
                Projectile.scale,
                SpriteEffects.None,
                0
            );

            //脉冲硫磺火效果
            float pulseIntensity = 0.5f + (float)Math.Sin(pulsePhase) * 0.3f;
            sb.Draw(
                fishTex,
                drawPos,
                null,
                new Color(255, 140, 70, 0) * pulseIntensity * glowIntensity * alpha,
                drawRot,
                origin,
                Projectile.scale * 1.1f,
                SpriteEffects.None,
                0
            );

            //白热核心
            if (glowIntensity > 0.7f) {
                sb.Draw(
                    fishTex,
                    drawPos,
                    null,
                    Color.White * glowIntensity * 0.5f * alpha,
                    drawRot,
                    origin,
                    Projectile.scale * 0.8f,
                    SpriteEffects.None,
                    0
                );
            }

            return false;
        }

        private void DrawBrimstoneTrail(SpriteBatch sb, Texture2D texture, Vector2 origin, float alpha) {
            if (trailPositions.Count < 2) return;

            for (int i = 1; i < trailPositions.Count; i++) {
                float progress = 1f - i / (float)trailPositions.Count;
                float trailAlpha = progress * alpha * 0.6f;
                float trailScale = Projectile.scale * MathHelper.Lerp(0.6f, 1f, progress);

                //硫磺火渐变色
                Color trailColor = Color.Lerp(
                    new Color(200, 60, 30),
                    new Color(255, 140, 70),
                    progress
                ) * trailAlpha;

                Vector2 trailPos = trailPositions[i] - Main.screenPosition;

                sb.Draw(
                    texture,
                    trailPos,
                    null,
                    trailColor,
                    Projectile.rotation - i * 0.08f,
                    origin,
                    trailScale,
                    SpriteEffects.None,
                    0
                );
            }
        }
    }

    /// <summary>
    /// 硫磺火焰弹幕
    /// </summary>
    internal class BrimstoneFlameProjectile : ModProjectile
    {
        public override string Texture => CWRConstant.Placeholder;

        private ref float Timer => ref Projectile.ai[0];
        private float rotationSpeed = 0f;

        public override void SetDefaults() {
            Projectile.width = 18;
            Projectile.height = 18;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Generic;
            Projectile.penetrate = 3;
            Projectile.timeLeft = 120;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = false;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 20;

            rotationSpeed = Main.rand.NextFloat(-0.3f, 0.3f);
        }

        public override void AI() {
            Timer++;

            //减速
            Projectile.velocity *= 0.98f;

            //轻微追踪最近的敌人
            if (Timer % 15 == 0 && Timer < 60) {
                NPC target = Projectile.Center.FindClosestNPC(400f);
                if (target != null) {
                    Vector2 toTarget = target.Center - Projectile.Center;
                    Projectile.velocity += toTarget.SafeNormalize(Vector2.Zero) * 0.8f;

                    if (Projectile.velocity.Length() > 20f) {
                        Projectile.velocity = Projectile.velocity.SafeNormalize(Vector2.Zero) * 20f;
                    }
                }
            }

            //旋转
            Projectile.rotation += rotationSpeed;

            //硫磺火光照
            Lighting.AddLight(Projectile.Center, 0.8f, 0.2f, 0.1f);

            //硫磺火粒子轨迹
            if (Main.rand.NextBool(2)) {
                Dust brimstone = Dust.NewDustDirect(
                    Projectile.position,
                    Projectile.width,
                    Projectile.height,
                    CWRID.Dust_Brimstone,
                    0, 0, 0,
                    default,
                    Main.rand.NextFloat(1.5f, 2.5f)
                );
                brimstone.velocity = -Projectile.velocity * 0.3f;
                brimstone.noGravity = true;
            }

            //火焰尾迹
            if (Main.rand.NextBool()) {
                Dust fire = Dust.NewDustDirect(
                    Projectile.position,
                    Projectile.width,
                    Projectile.height,
                    DustID.Torch,
                    0, 0, 0,
                    Color.Red,
                    Main.rand.NextFloat(1.2f, 2f)
                );
                fire.velocity = -Projectile.velocity * 0.2f;
                fire.noGravity = true;
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            //硫磺火击中爆发
            for (int i = 0; i < 15; i++) {
                Vector2 velocity = Main.rand.NextVector2Circular(6f, 6f);
                Dust brimstone = Dust.NewDustPerfect(
                    Projectile.Center,
                    CWRID.Dust_Brimstone,
                    velocity,
                    0,
                    default,
                    Main.rand.NextFloat(1.5f, 2.5f)
                );
                brimstone.noGravity = true;
            }

            //火焰爆发
            for (int i = 0; i < 8; i++) {
                Dust fire = Dust.NewDustPerfect(
                    Projectile.Center,
                    DustID.Torch,
                    Main.rand.NextVector2Circular(5f, 5f),
                    0,
                    Color.Red,
                    Main.rand.NextFloat(1.5f, 2f)
                );
                fire.noGravity = true;
            }

            SoundEngine.PlaySound(SoundID.Item74 with {
                Volume = 0.4f,
                Pitch = 0.2f
            }, Projectile.Center);
        }

        public override void OnKill(int timeLeft) {
            //消散爆发
            for (int i = 0; i < 20; i++) {
                Vector2 velocity = Main.rand.NextVector2Circular(8f, 8f);
                Dust brimstone = Dust.NewDustPerfect(
                    Projectile.Center,
                    CWRID.Dust_Brimstone,
                    velocity,
                    0,
                    default,
                    Main.rand.NextFloat(2f, 3f)
                );
                brimstone.noGravity = true;
            }

            //火焰
            for (int i = 0; i < 10; i++) {
                Dust fire = Dust.NewDustPerfect(
                    Projectile.Center,
                    DustID.Torch,
                    Main.rand.NextVector2Circular(6f, 6f),
                    0,
                    Color.Red,
                    Main.rand.NextFloat(1.8f, 2.5f)
                );
                fire.noGravity = true;
            }

            SoundEngine.PlaySound(SoundID.Item74 with {
                Volume = 0.5f,
                Pitch = -0.2f
            }, Projectile.Center);
        }

        public override bool PreDraw(ref Color lightColor) {
            //简单的发光球体绘制
            Texture2D glowTex = CWRAsset.SoftGlow.Value;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;

            //外层硫磺火辉光
            for (int i = 0; i < 3; i++) {
                float scale = 0.4f + i * 0.15f;
                float alpha = (1f - i * 0.3f) * 0.8f;

                Main.spriteBatch.Draw(
                    glowTex,
                    drawPos,
                    null,
                    new Color(255, 100, 50, 0) * alpha,
                    Projectile.rotation,
                    glowTex.Size() / 2f,
                    scale,
                    SpriteEffects.None,
                    0
                );
            }

            //核心亮点
            Main.spriteBatch.Draw(
                glowTex,
                drawPos,
                null,
                Color.White with { A = 0 } * 0.9f,
                Projectile.rotation,
                glowTex.Size() / 2f,
                0.25f,
                SpriteEffects.None,
                0
            );

            return false;
        }
    }
}
