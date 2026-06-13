using CalamityOverhaul.Common;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.GameContent.BaseEntity;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Melee.StormGoddessSpears
{
    /// 风暴女神之矛手持：三段连击突刺/横扫/上挑，电弧刀光 StormSlashTrail.fx
    internal class StormGoddessSpearHeld : BaseHeldProj, IPrimitiveDrawable
    {
        public override string Texture => CWRConstant.Projectile_Melee + "StormGoddessSpearProj";
        public override LocalizedText DisplayName => VaultUtils.GetLocalizedItemName<StormGoddessSpear>();

        private const int FrameCount = 8;

        /// 连击索引 0刺 1扫 2挑
        private int ComboCounter => (int)Projectile.ai[0] % 3;

        private bool IsThrust => ComboCounter == 0;

        //阶段时长(逻辑帧，攻速缩放)
        private float WindupTime => ComboCounter switch { 0 => 3f, 1 => 5f, _ => 6f };
        private float ActiveTime => ComboCounter switch { 0 => 6f, 1 => 11f, _ => 12f };
        private float RecoverTime => ComboCounter switch { 0 => 7f, 1 => 7f, _ => 8f };
        private float TotalTime => WindupTime + ActiveTime + RecoverTime;
        //横扫/上挑的挥舞弧度
        private float SwingArc => ComboCounter == 2 ? 3.9f : 3.5f;
        //突刺顶点的突出距离
        private const float StabReach = 150f;
        //矛刃判定长度（从持握点向矛尖延伸）
        private const float BladeLength = 135f;

        /// 闪电颜色(白蓝系)
        private int lightningColorStyle = 1;

        private float elapsed;
        private float speedMul = 1f;
        private int lockedDirection = 1;
        private int swingSign = 1;
        /// 矛身指向
        private Vector2 bladeUnit;
        /// 突刺持距
        private float holdout;
        private float startAngle;
        private float endAngle;
        private float currentRotation;
        private float lastRotation;
        private bool swingSoundPlayed;
        private bool hasSpawnedLightning;
        private float trailFade;
        private readonly HashSet<int> hitNPCs = [];

        //刀光轨迹缓存(横扫/上挑)
        private const int TrailMax = 56;
        private const int TrailSubdiv = 4;
        private readonly float[] trailRot = new float[TrailMax];
        private int trailCount;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 50;
            Projectile.friendly = true;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
            Projectile.ownerHitCheck = true;
            Projectile.timeLeft = 60;
            Projectile.scale = 0.85f;
            Projectile.CWR().NotSubjectToSpecialEffects = true;
            Projectile.CWR().PierceResist = true;
        }

        public override bool ShouldUpdatePosition() => false;

        public override bool? CanDamage() => elapsed >= WindupTime && elapsed <= WindupTime + ActiveTime + 1f;

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            if (CanDamage() != true) {
                return false;
            }
            Vector2 hand = Owner.GetPlayerStabilityCenter();
            Vector2 tip = hand + bladeUnit * (holdout + BladeLength) * Projectile.scale;
            float collisionPoint = 0f;
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size()
                , hand, tip, 32f, ref collisionPoint);
        }

        public override void Initialize() {
            //循环颜色风格（统一为白蓝色系）
            lightningColorStyle = ComboCounter % 3 + 1;

            Vector2 aimUnit = Projectile.velocity.SafeNormalize(Vector2.UnitX * Owner.direction);
            lockedDirection = Math.Sign(aimUnit.X) == 0 ? Owner.direction : Math.Sign(aimUnit.X);
            Owner.direction = lockedDirection;

            speedMul = Owner.GetWeaponAttackSpeed(Item);
            if (speedMul <= 0f) {
                speedMul = 1f;
            }

            float baseAngle = aimUnit.ToRotation();
            //横扫顺势而下，上挑逆势而起
            swingSign = ComboCounter == 2 ? -1 : 1;
            startAngle = baseAngle - swingSign * SwingArc * 0.5f;
            endAngle = baseAngle + swingSign * SwingArc * 0.5f;
            currentRotation = lastRotation = IsThrust ? baseAngle : startAngle;
            bladeUnit = currentRotation.ToRotationVector2();
        }

        public override void AI() {
            if (Item.type != ModContent.ItemType<StormGoddessSpear>()) {
                Projectile.Kill();
                return;
            }
            if (elapsed >= TotalTime) {
                Projectile.Kill();
                return;
            }

            lastRotation = currentRotation;
            float activeEnd = WindupTime + ActiveTime;

            if (IsThrust) {
                ThrustMotion(activeEnd);
            }
            else {
                SweepMotion(activeEnd);
            }

            bladeUnit = currentRotation.ToRotationVector2();
            UpdatePlayerPose();
            VaultUtils.ClockFrame(ref Projectile.frame, 5, FrameCount - 1);

            //在挥舞过程中生成电火花轨迹
            if (CanDamage() == true && elapsed % 5f < speedMul && !VaultUtils.isServer) {
                Vector2 tipPos = Owner.GetPlayerStabilityCenter() + bladeUnit * (holdout + BladeLength * 0.9f);
                Color particleColor = GetLightningColorForStyle(lightningColorStyle);
                PRTLoader.NewParticle<PRT_Spark>(tipPos + Main.rand.NextVector2Circular(3, 3), Main.rand.NextVector2Unit() * Main.rand.NextFloat(2f, 5f), particleColor * 0.7f, 0.8f).Configure(false, Main.rand.Next(3, 5), Owner);
            }

            Lighting.AddLight(Owner.GetPlayerStabilityCenter() + bladeUnit * (holdout + BladeLength * 0.8f)
                , GetLightningColorForStyle(lightningColorStyle).ToVector3() * 0.5f);
            elapsed += speedMul;
        }

        private void ThrustMotion(float activeEnd) {
            if (elapsed < WindupTime) {
                //短促回拉
                float t = elapsed / WindupTime;
                holdout = MathHelper.Lerp(10f, -14f, MathF.Sin(t * MathHelper.PiOver2));
            }
            else if (elapsed < activeEnd) {
                //迅捷突刺
                float t = (elapsed - WindupTime) / ActiveTime;
                float eased = 1f - MathF.Pow(1f - t, 3.8f);
                holdout = MathHelper.Lerp(-14f, StabReach, eased);

                PlaySwingSound();
                TryFireLightning(t, 0.5f);

                //突刺过程中的精细电火花
                if (!VaultUtils.isServer && Main.rand.NextBool(2)) {
                    Vector2 sparkPos = Owner.GetPlayerStabilityCenter() + bladeUnit * (holdout + BladeLength * 0.7f);
                    PRTLoader.NewParticle<PRT_Spark>(sparkPos, bladeUnit * Main.rand.NextFloat(2f, 5f), GetLightningColorForStyle(lightningColorStyle), 0.8f).Configure(false, 4, Owner);
                }
            }
            else {
                //收矛
                float t = (elapsed - activeEnd) / RecoverTime;
                holdout = MathHelper.Lerp(StabReach, 8f, t * t * (3f - 2f * t));
            }
        }

        private void SweepMotion(float activeEnd) {
            holdout = 26f;
            if (elapsed < WindupTime) {
                //蓄力回拉
                float t = elapsed / WindupTime;
                currentRotation = startAngle - swingSign * 0.22f * MathF.Sin(t * MathHelper.PiOver2);
                trailFade = 0f;
            }
            else if (elapsed < activeEnd) {
                //ease-out 扫击
                float t = (elapsed - WindupTime) / ActiveTime;
                float eased = 1f - MathF.Pow(1f - t, ComboCounter == 2 ? 4.2f : 3.5f);
                currentRotation = MathHelper.Lerp(startAngle, endAngle, eased);
                trailFade = 1f;
                PushTrailSamples();

                PlaySwingSound();
                TryFireLightning(t, 0.4f);
            }
            else {
                //收势：矛停住，电弧收缩渐隐
                float t = (elapsed - activeEnd) / RecoverTime;
                currentRotation = endAngle;
                trailFade = 1f - t;
                PushTrailSamples();
            }
        }

        private void PushTrailSamples() {
            for (int s = TrailSubdiv - 1; s >= 0; s--) {
                float rot = MathHelper.Lerp(currentRotation, lastRotation, s / (float)TrailSubdiv);
                for (int i = Math.Min(trailCount, TrailMax - 1); i > 0; i--) {
                    trailRot[i] = trailRot[i - 1];
                }
                trailRot[0] = rot;
                if (trailCount < TrailMax) {
                    trailCount++;
                }
            }
        }

        private void PlaySwingSound() {
            if (swingSoundPlayed) {
                return;
            }
            swingSoundPlayed = true;
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item1 with {
                    Pitch = 0.1f + ComboCounter * 0.12f
                }, Owner.Center);
            }
        }

        private void TryFireLightning(float t, float fireAt) {
            if (hasSpawnedLightning || t < fireAt) {
                return;
            }
            hasSpawnedLightning = true;
            FireLightningPattern();
        }

        private void FireLightningPattern() {
            if (!Projectile.IsOwnedByLocalPlayer()) {
                return;
            }

            Vector2 aim = UnitToMouseV * Item.shootSpeed;

            //根据不同连击发射不同效果的闪电
            if (ComboCounter == 0) {
                //第一击：单个精准闪电（细长型）
                SpawnPlayerLightning(
                    aim,
                    1f,
                    lightningColorStyle,
                    false,
                    widthScale: 0.7f //70%宽度
                );
            }
            else if (ComboCounter == 1) {
                //第二击三道扇形闪电
                for (int i = -1; i <= 1; i++) {
                    //黄金角分布
                    float angle = i * 0.25f * (1f + MathF.Abs(i) * 0.2f);
                    Vector2 velocity = aim.RotatedBy(angle);

                    SpawnPlayerLightning(
                        velocity,
                        0.65f,
                        lightningColorStyle,
                        true,
                        widthScale: 0.65f, //65%宽度
                        speedScale: 0.9f
                    );
                }
            }
            else if (ComboCounter == 2) {
                bool hasAdrenaline = Owner.GetPlayerAdrenalineMode();
                int count = hasAdrenaline ? 7 : 0;
                float damageMultiplier = hasAdrenaline ? 0.85f : 0.6f;

                for (int i = 0; i < count; i++) {
                    float progress = i / (float)count;
                    float spiralAngle = MathHelper.TwoPi * progress + progress * MathHelper.PiOver4;
                    float radiusOffset = 0.8f + progress * 0.4f; //螺旋扩散

                    Vector2 velocity = spiralAngle.ToRotationVector2() * Item.shootSpeed * radiusOffset;

                    SpawnPlayerLightning(
                        velocity,
                        damageMultiplier,
                        lightningColorStyle,
                        true,
                        widthScale: 0.6f, //60%宽度
                        speedScale: 0.85f + progress * 0.3f //渐进速度
                    );
                }

                //冲击波粒子
                if (!VaultUtils.isServer) {
                    SpawnShockwaveParticles();
                }

                //播放音效
                SoundEngine.PlaySound(SoundID.DD2_LightningBugZap with {
                    Volume = 0.6f,
                    Pitch = -0.3f
                }, LightningSpawnPos);
            }
        }

        /// 闪电/冲击波生成点
        private Vector2 LightningSpawnPos => Owner.GetPlayerStabilityCenter() + UnitToMouseV * 60f;

        /// 生成玩家闪电
        private void SpawnPlayerLightning(
            Vector2 velocity,
            float damageMultiplier,
            int colorStyle,
            bool disableHoming,
            float widthScale = 1f,
            float speedScale = 1f) {

            //ai[2]编码宽度: colorStyle + 1000*widthScale
            int ai2Value = colorStyle;
            if (disableHoming) ai2Value += 100;
            ai2Value += (int)(1000 * widthScale); //编码宽度缩放

            Projectile.NewProjectile(
                Owner.GetSource_ItemUse(Item),
                LightningSpawnPos,
                velocity * speedScale,
                ModContent.ProjectileType<StormLightning>(),
                (int)(Projectile.damage * damageMultiplier),
                Projectile.knockBack * 0.7f,
                Owner.whoAmI,
                ai0: 0,
                ai1: 0,
                ai2: ai2Value
            );
        }

        /// 冲击波粒子
        private void SpawnShockwaveParticles() {
            Color particleColor = GetLightningColorForStyle(lightningColorStyle);
            Vector2 spawnPos = LightningSpawnPos;

            //环形冲击波
            for (int i = 0; i < 12; i++) {
                float angle = MathHelper.TwoPi * i / 12f;
                Vector2 velocity = angle.ToRotationVector2() * Main.rand.NextFloat(12f, 20f);
                PRTLoader.NewParticle<PRT_Light>(
                    spawnPos,
                    velocity,
                    particleColor * 0.8f,
                    0.35f
                ).Configure(Main.rand.Next(15, 25), opacity: 1.2f, squishStrenght: 1.6f, hueShift: 0f);
            }

            //向上爆发的粒子
            for (int i = 0; i < 8; i++) {
                float angle = Main.rand.NextFloat(-MathHelper.PiOver4, MathHelper.PiOver4) - MathHelper.PiOver2;
                Vector2 velocity = angle.ToRotationVector2() * Main.rand.NextFloat(15f, 28f);
                PRTLoader.NewParticle<PRT_Spark>(spawnPos, velocity, particleColor * 0.9f, 1.4f).Configure(false, Main.rand.Next(12, 20), Owner);
            }
        }

        /// 闪电颜色(白蓝系)
        private Color GetLightningColorForStyle(int style) {
            return style switch {
                1 => new Color(200, 230, 255), //亮白蓝（第一击）
                2 => new Color(150, 200, 255), //中蓝白（第二击）
                3 => new Color(100, 180, 255), //深蓝白（第三击）
                _ => new Color(180, 220, 255)  //默认白蓝
            };
        }

        private void UpdatePlayerPose() {
            Owner.heldProj = Projectile.whoAmI;
            Owner.direction = lockedDirection;
            Owner.itemTime = Owner.itemAnimation = 2;
            Owner.itemRotation = (bladeUnit * Owner.direction).ToRotation();
            Owner.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.Full, currentRotation - MathHelper.PiOver2);
            Projectile.Center = Owner.GetPlayerStabilityCenter() + bladeUnit * (holdout + BladeLength * 0.5f);
            Projectile.rotation = currentRotation;
            Projectile.timeLeft = 60;
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {
            modifiers.HitDirectionOverride = bladeUnit.X > 0 ? 1 : -1;
            if (target.IsWormBody()) {
                modifiers.FinalDamage *= 0.425f;
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            //转发物品命中钩子
            if (hitNPCs.Add(target.whoAmI)) {
                ItemLoader.OnHitNPC(Item, Owner, target, hit, damageDone);
                NPCLoader.OnHitByItem(target, Owner, Item, hit, damageDone);
                PlayerLoader.OnHitNPC(Owner, target, hit, damageDone);
            }

            //添加电击效果
            target.AddBuff(BuffID.Electrified, 120);

            //暴击时生成额外的电弧
            if (hit.Crit) {
                SpawnCriticalArcs(target);
            }

            //生成命中粒子
            if (!VaultUtils.isServer && Main.rand.NextBool(2)) {
                SpawnHitParticles(target);
            }
        }

        /// 暴击电弧
        private void SpawnCriticalArcs(NPC target) {
            if (!Projectile.IsOwnedByLocalPlayer()) return;

            int arcCount = Owner.GetPlayerAdrenalineMode() ? 3 : 2;

            for (int i = 0; i < arcCount; i++) {
                float angle = MathHelper.TwoPi * i / arcCount + Main.rand.NextFloat(MathHelper.TwoPi);
                Vector2 velocity = angle.ToRotationVector2() * Main.rand.NextFloat(10f, 16f);

                Projectile arc = Projectile.NewProjectileDirect(
                    Owner.GetSource_ItemUse(Item),
                    target.Center,
                    velocity,
                    ModContent.ProjectileType<StormArc>(),
                    (int)(Projectile.damage * 0.35f),
                    Projectile.knockBack * 0.4f,
                    Owner.whoAmI
                );

                arc.timeLeft = 25;
                arc.penetrate = 2;
                arc.tileCollide = true;
            }
        }

        /// 命中粒子
        private void SpawnHitParticles(NPC target) {
            Color particleColor = GetLightningColorForStyle(lightningColorStyle);

            for (int i = 0; i < Main.rand.Next(4, 8); i++) {
                Vector2 velocity = Main.rand.NextVector2Unit() * Main.rand.NextFloat(6f, 14f);
                PRTLoader.NewParticle<PRT_Light>(
                    target.Center + Main.rand.NextVector2Circular(target.width * 0.3f, target.height * 0.3f),
                    velocity,
                    particleColor * 0.9f,
                    0.25f
                ).Configure(Main.rand.Next(8, 15), opacity: 1f, squishStrenght: 1.5f, hueShift: Main.rand.NextFloat(-0.05f, 0.05f));
            }

            //音效概率播放
            if (Main.rand.NextBool(3)) {
                SoundEngine.PlaySound(SoundID.Item94 with {
                    Volume = 0.4f,
                    Pitch = 0.2f
                }, target.Center);
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = TextureValue;
            Rectangle rect = tex.GetRectangle(Projectile.frame, FrameCount);
            Vector2 origin = rect.Size() / 2f;
            Vector2 hand = Owner.GetPlayerStabilityCenter();
            //贴图矛尖指向右上，沿矛身方向旋转
            float rot = currentRotation + MathHelper.PiOver4;
            SpriteEffects effect = SpriteEffects.None;
            if (lockedDirection < 0) {
                rot += MathHelper.PiOver2;
                effect = SpriteEffects.FlipHorizontally;
            }

            //攻击阶段的残影
            if (CanDamage() == true) {
                Color ghostColor = GetLightningColorForStyle(lightningColorStyle);
                for (int i = 1; i <= 3; i++) {
                    Vector2 pos;
                    float ghostRot = rot;
                    if (IsThrust) {
                        float ghostHoldout = holdout - i * 14f;
                        if (ghostHoldout < -14f) {
                            continue;
                        }
                        pos = hand + bladeUnit * ghostHoldout - Main.screenPosition;
                    }
                    else {
                        float lerpRot = MathHelper.Lerp(currentRotation, lastRotation, i / 4f);
                        ghostRot = lerpRot + MathHelper.PiOver4 + (lockedDirection < 0 ? MathHelper.PiOver2 : 0f);
                        pos = hand + lerpRot.ToRotationVector2() * holdout - Main.screenPosition;
                    }
                    Color trailColor = ghostColor * (0.3f * (1f - i / 4f));
                    trailColor.A = 0;
                    Main.EntitySpriteDraw(tex, pos, rect, trailColor, ghostRot, origin, Projectile.scale, effect, 0);
                }
            }

            //矛体本体
            Vector2 drawPos = hand + bladeUnit * holdout - Main.screenPosition;
            Main.EntitySpriteDraw(tex, drawPos, rect, lightColor, rot, origin, Projectile.scale, effect, 0);
            return false;
        }

        void IPrimitiveDrawable.DrawPrimitives() {
            if (IsThrust || trailCount < 3 || trailFade <= 0.02f) {
                return;
            }
            Effect effect = EffectLoader.StormSlashTrail?.Value;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            if (effect == null || noise == null) {
                return;
            }

            var bars = new VertexPositionColorTexture[trailCount * 2];
            Vector2 center = Owner.GetPlayerStabilityCenter();
            //矛尖延伸约半张贴图对角
            float outer = holdout + 142f;
            float inner = 56f;
            for (int i = 0; i < trailCount; i++) {
                float factor = 1f - i / (float)trailCount;
                Vector2 dir = trailRot[i].ToRotationVector2();
                bars[i * 2] = new VertexPositionColorTexture((center + dir * outer).ToVector3()
                    , Color.White, new Vector2(factor, 0f));
                bars[i * 2 + 1] = new VertexPositionColorTexture((center + dir * inner).ToVector3()
                    , Color.White, new Vector2(factor, 1f));
            }

            GraphicsDevice device = Main.graphics.GraphicsDevice;
            BlendState origBlend = device.BlendState;
            RasterizerState origRaster = device.RasterizerState;
            device.BlendState = BlendState.AlphaBlend;
            device.RasterizerState = RasterizerState.CullNone;

            effect.Parameters["transformMatrix"]?.SetValue(VaultUtils.GetTransfromMatrix());
            effect.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            effect.Parameters["uFade"]?.SetValue(trailFade);
            effect.Parameters["uHeat"]?.SetValue(ComboCounter == 2 ? 1f : 0.45f);
            effect.Parameters["uNoiseTex"]?.SetValue(noise);
            foreach (EffectPass pass in effect.CurrentTechnique.Passes) {
                pass.Apply();
                device.DrawUserPrimitives(PrimitiveType.TriangleStrip, bars, 0, bars.Length - 2);
            }

            device.BlendState = origBlend;
            device.RasterizerState = origRaster;
        }
    }
}
