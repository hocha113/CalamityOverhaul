using CalamityOverhaul.Common;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.GameContent.BaseEntity;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.Graphics.CameraModifiers;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Melee.DestroyersBladeEXs
{
    /// <summary>
    /// 毁灭者之刃EX 沉重三拍挥砍。拍0/1 正反手重劈,拍2 终结巨新月。
    /// 节奏走收-爆-停:长蓄力回拉、2~3帧泄压爆发带过冲、硬停驻谷、收势回守。
    /// 命中顿帧从收势尾巴等量扣回,总帧守恒(轻拍20/终结28,与旧版持平)。
    /// ai[0]=拍号 ai[1]=挥向
    /// </summary>
    internal class DestroyersBladeEXHeld : BaseHeldProj, IPrimitiveDrawable, IOverlayDrawable
    {
        public override string Texture => CWRConstant.Item_Melee + "DestroyersBladeEX";
        public override LocalizedText DisplayName => ItemLoader.GetItem(ModContent.ItemType<DestroyersBladeEX>()).DisplayName;

        private ref float ComboIndex => ref Projectile.ai[0];
        private ref float SwingDirAi => ref Projectile.ai[1];

        private bool IsFinisher => ComboIndex >= 2f;
        private bool Empowered => Owner.GetModPlayer<DestroyerEXPlayer>().Empowered;

        //阶段时长(逻辑帧,攻速缩放)。轻拍 7+5+8=20,终结 10+6+12=28,总帧与旧版守恒
        private float WindupTime => IsFinisher ? 10f : 7f;
        private float SlashTime => IsFinisher ? 6f : 5f;
        private float RecoverTime => IsFinisher ? 12f : 8f;
        private float TotalTime => WindupTime + SlashTime + RecoverTime;
        //挥砍弧度:轻拍压椭圆弯月,终结才配巨新月
        private float SwingArc => IsFinisher ? 5.5f : 3.4f;
        //刀尖距持握点长度
        private float BladeReach => 150f * (IsFinisher ? 1.08f : 1f);
        //蓄力回拉角:比旧版更深,沉重感的主要来源
        private float PullbackAngle => IsFinisher ? 1.05f : 0.72f;

        //收-爆-停整形
        private float SwingGatherEnd => IsFinisher ? 0.34f : 0.30f;
        private float SwingBurstEnd => IsFinisher ? 0.60f : 0.62f;

        //命中顿帧预算(帧),从收势尾巴等量扣回
        private const float HitStopBudget = 4f;

        private float elapsed;
        private float speedMul = 1f;
        private int lockedDirection = 1;
        private int swingSign = 1;
        private float startAngle;
        private float endAngle;
        private float currentRotation;
        private float lastRotation;
        private float sweepCollisionStart;
        private float sweepCollisionEnd;
        private bool slashVisualActive;
        private bool sweepDamageActive;
        private bool slashSoundPlayed;
        private bool shotsFired;
        private float trailFade;
        private float hitStopFrames;
        private float hitStopSpent;
        private float bodyLean;

        //刀光按外缘弧长补点
        private const int TrailMax = 96;
        private const float TrailSampleSpacing = 18f;
        private readonly float[] trailRot = new float[TrailMax];
        private int trailCount;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 64;
            Projectile.friendly = true;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
            Projectile.ownerHitCheck = true;
            Projectile.timeLeft = 90;
        }

        public override bool ShouldUpdatePosition() => false;

        public override bool? CanDamage() => sweepDamageActive;

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            if (CanDamage() != true) {
                return false;
            }
            Vector2 hand = Owner.GetPlayerStabilityCenter();
            float reach = BladeReach * Projectile.scale;
            int steps = GetAngularSteps(sweepCollisionEnd - sweepCollisionStart, reach, 24f, 64);
            for (int i = 0; i <= steps; i++) {
                float rotation = MathHelper.Lerp(sweepCollisionStart, sweepCollisionEnd, i / (float)steps);
                Vector2 tip = hand + rotation.ToRotationVector2() * reach;
                float collisionPoint = 0f;
                //宽刃:线判定加厚,贴脸与擦刃都要咬住
                if (Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size()
                    , hand, tip, 54f, ref collisionPoint)) {
                    return true;
                }
            }
            return false;
        }

        public override void Initialize() {
            swingSign = Math.Sign(SwingDirAi);
            if (swingSign == 0) {
                swingSign = 1;
            }

            lockedDirection = Math.Sign(ToMouse.X);
            if (lockedDirection == 0) {
                lockedDirection = Owner.direction;
            }
            Owner.direction = lockedDirection;

            speedMul = Owner.GetWeaponAttackSpeed(Item);
            if (speedMul <= 0f) {
                speedMul = 1f;
            }

            float baseAngle = Projectile.velocity.ToRotation();
            startAngle = baseAngle - swingSign * SwingArc * 0.5f;
            endAngle = baseAngle + swingSign * SwingArc * 0.5f;
            currentRotation = lastRotation = startAngle;
            sweepCollisionStart = sweepCollisionEnd = startAngle;

            //出手打断潜行,各端同拍
            Owner.GetModPlayer<DestroyerEXPlayer>().NoteAttack();

            if (IsFinisher) {
                Projectile.damage = (int)(Projectile.damage * 1.4f);
                Projectile.scale = 1.22f;
                if (!VaultUtils.isServer) {
                    //终结斩起手:液压蓄能 + 低吼
                    SoundEngine.PlaySound(SoundID.Item22 with { Volume = 0.7f, Pitch = -0.5f, MaxInstances = 3 }, Owner.Center);
                    SoundEngine.PlaySound(SoundID.Item15 with { Volume = 0.5f, Pitch = -0.7f, MaxInstances = 2 }, Owner.Center);
                }
            }
            else {
                Projectile.scale = 1.06f;
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Item22 with { Volume = 0.4f, Pitch = -0.35f, MaxInstances = 3 }, Owner.Center);
                }
            }
        }

        public override void AI() {
            slashVisualActive = false;
            sweepDamageActive = false;
            sweepCollisionStart = sweepCollisionEnd = currentRotation;
            if (Item.type != ModContent.ItemType<DestroyersBladeEX>()) {
                Projectile.Kill();
                return;
            }

            //命中顿帧:几何全冻,时长记账后从收势扣回
            if (hitStopFrames > 0f) {
                hitStopFrames--;
                UpdatePlayerPose();
                ApplyBodyLean();
                return;
            }

            float effectiveTotal = TotalTime - hitStopSpent;
            if (elapsed >= effectiveTotal) {
                Projectile.Kill();
                return;
            }

            lastRotation = currentRotation;
            float frameEnd = MathF.Min(elapsed + speedMul, effectiveTotal);
            float slashEnd = WindupTime + SlashTime;
            float slashFromTime = MathF.Max(elapsed, WindupTime);
            float slashToTime = MathF.Min(frameEnd, slashEnd);

            if (slashToTime > slashFromTime) {
                //消费本帧与挥砍阶段的交集,高攻速跨阶段不漏刀
                slashVisualActive = true;
                float fromT = (slashFromTime - WindupTime) / SlashTime;
                float toT = (slashToTime - WindupTime) / SlashTime;
                float progress = GetSwingProgress(toT);
                float slashRotation = GetSwingRotation(progress);

                float damageFrom = MathF.Max(fromT, SwingGatherEnd);
                float damageTo = MathF.Min(toT, SwingBurstEnd);
                if (damageTo > damageFrom) {
                    sweepDamageActive = true;
                    sweepCollisionStart = GetSwingRotation(GetSwingProgress(damageFrom));
                    sweepCollisionEnd = GetSwingRotation(GetSwingProgress(damageTo));
                }

                if (!slashSoundPlayed && toT >= SwingGatherEnd) {
                    slashSoundPlayed = true;
                    if (!VaultUtils.isServer) {
                        //沉重泄压:钝重挥杖声压底,金属破空跟上
                        SoundEngine.PlaySound(SoundID.DD2_MonkStaffSwing with { Volume = 0.9f, Pitch = -0.25f, MaxInstances = 3 }, Owner.Center);
                        SoundEngine.PlaySound(SoundID.Item1 with { Volume = 0.8f, Pitch = -0.65f, MaxInstances = 3 }, Owner.Center);
                        if (IsFinisher) {
                            SoundEngine.PlaySound(SoundID.ForceRoar with { Volume = 0.4f, Pitch = -0.6f, MaxInstances = 2 }, Owner.Center);
                        }
                        Main.LocalPlayer?.CWR()?.GetScreenShake(IsFinisher ? 4f : 2f);
                    }
                }

                PushTrailInterval(fromT, toT);

                if (!shotsFired && progress >= 0.58f) {
                    shotsFired = true;
                    FireShots(slashRotation);
                }

                //刃缘熔渣火花
                if (!VaultUtils.isServer && Main.rand.NextBool(2)) {
                    Vector2 along = Owner.GetPlayerStabilityCenter()
                        + slashRotation.ToRotationVector2() * Main.rand.NextFloat(BladeReach * 0.5f, BladeReach);
                    Vector2 tangent = slashRotation.ToRotationVector2().RotatedBy(swingSign * MathHelper.PiOver2);
                    PRTLoader.NewParticle<PRT_Spark>(along, tangent * Main.rand.NextFloat(3f, 7f)
                        , Main.rand.NextBool(4) ? Color.White : new Color(255, 40, 30)
                        , Main.rand.NextFloat(0.6f, 1.1f)).Configure(false, 9);
                }
            }

            if (frameEnd <= WindupTime) {
                //深蓄力:重刃缓缓拖向身后,末端近静止(爆发前的憋劲)
                float t = frameEnd / WindupTime;
                currentRotation = MathHelper.Lerp(startAngle, ChamberAngle, EaseOutCubic(t));
                trailFade = 0f;
                bodyLean = IsFinisher ? -0.06f * SmoothStep01(t) : 0f;
            }
            else if (frameEnd <= slashEnd) {
                //泄压爆发:过冲后硬停回坐
                float t = (frameEnd - WindupTime) / SlashTime;
                currentRotation = GetSwingRotation(GetSwingProgress(t));
                trailFade = 1f;
                if (IsFinisher) {
                    bodyLean = MathHelper.Lerp(-0.06f, 0.09f, SmoothStep01((t - SwingGatherEnd) / 0.3f));
                }
            }
            else {
                //收势回守,顿帧扣掉的时长在这里兑现
                float t = (frameEnd - slashEnd) / RecoverTime;
                float hold = IsFinisher ? 0.30f : 0.24f;
                float returnT = SmoothStep01((t - hold) / (1f - hold));
                float baseAngle = (startAngle + endAngle) * 0.5f;
                float guardAngle = baseAngle + swingSign * (IsFinisher ? 1.1f : 0.9f);
                currentRotation = MathHelper.Lerp(endAngle, guardAngle, returnT);
                trailFade = 1f - SmoothStep01(t);
                bodyLean = IsFinisher ? MathHelper.Lerp(0.09f, 0f, SmoothStep01(t * 1.6f)) : 0f;
                TrimTrailToRotation(currentRotation);
            }

            UpdatePlayerPose();
            ApplyBodyLean();
            Lighting.AddLight(Owner.GetPlayerStabilityCenter() + currentRotation.ToRotationVector2() * BladeReach * 0.7f
                , new Vector3(0.9f, 0.12f, 0.08f));
            elapsed = frameEnd;
        }

        private float ChamberAngle => startAngle - swingSign * PullbackAngle;

        private float GetSwingProgress(float t) {
            float gatherEnd = SwingGatherEnd;
            float creep = IsFinisher ? 0.10f : 0.05f;
            float burstEnd = SwingBurstEnd;
            float path = SwingArc + PullbackAngle;
            float overshoot = 1f + (IsFinisher ? 0.16f : 0.12f) / path;
            if (t < gatherEnd) {
                return creep * SmoothStep01(t / gatherEnd);
            }
            if (t < burstEnd) {
                float burstT = (t - gatherEnd) / (burstEnd - gatherEnd);
                return MathHelper.Lerp(creep, overshoot, SmoothStep01(burstT));
            }
            return MathHelper.Lerp(overshoot, 1f, SmoothStep01((t - burstEnd) / (1f - burstEnd)));
        }

        private float GetSwingRotation(float progress)
            => MathHelper.Lerp(ChamberAngle, endAngle, progress);

        private static float EaseOutCubic(float value) {
            value = MathHelper.Clamp(value, 0f, 1f);
            return 1f - MathF.Pow(1f - value, 3f);
        }

        private static float SmoothStep01(float value) {
            value = MathHelper.Clamp(value, 0f, 1f);
            return value * value * (3f - 2f * value);
        }

        private static int GetAngularSteps(float delta, float radius, float targetSpacing, int maxSteps) {
            float arcLength = MathF.Abs(delta) * MathF.Max(radius, 1f);
            return Math.Clamp((int)MathF.Ceiling(arcLength / targetSpacing), 1, maxSteps);
        }

        private void PushTrailInterval(float fromT, float toT) {
            float forwardTo = MathF.Min(toT, SwingBurstEnd);
            if (forwardTo > fromT) {
                PushTrailSamples(GetSwingRotation(GetSwingProgress(fromT))
                    , GetSwingRotation(GetSwingProgress(forwardTo)));
            }
            if (toT > SwingBurstEnd) {
                TrimTrailToRotation(GetSwingRotation(GetSwingProgress(toT)));
            }
        }

        private void PushTrailSamples(float fromRotation, float toRotation) {
            //终结斩跨过 PI,保留未包裹角度
            float delta = toRotation - fromRotation;
            if (delta * swingSign <= 0.0001f) {
                TrimTrailToRotation(toRotation);
                return;
            }

            float outerRadius = (BladeReach + 22f) * Projectile.scale;
            bool appendStart = trailCount == 0;
            int steps = GetAngularSteps(delta, outerRadius, TrailSampleSpacing, TrailMax - 1);
            int retained = Math.Min(trailCount, TrailMax - steps);
            if (retained > 0) {
                Array.Copy(trailRot, 0, trailRot, steps, retained);
            }
            for (int i = 0; i < steps; i++) {
                float amount = 1f - i / (float)steps;
                trailRot[i] = MathHelper.Lerp(fromRotation, toRotation, amount);
            }
            trailCount = steps + retained;
            if (appendStart && trailCount < TrailMax) {
                trailRot[trailCount++] = fromRotation;
            }
        }

        private void TrimTrailToRotation(float rotation) {
            if (trailCount == 0) {
                return;
            }

            const float angleEpsilon = 0.0001f;
            int firstRetained = 0;
            while (firstRetained < trailCount
                && (trailRot[firstRetained] - rotation) * swingSign > angleEpsilon) {
                firstRetained++;
            }

            int retained = trailCount - firstRetained;
            bool headAlreadySampled = retained > 0
                && MathF.Abs(trailRot[firstRetained] - rotation) <= angleEpsilon;
            int targetOffset = headAlreadySampled ? 0 : 1;
            int copied = Math.Min(retained, TrailMax - targetOffset);
            if (copied > 0 && (firstRetained != targetOffset || firstRetained > 0)) {
                Array.Copy(trailRot, firstRetained, trailRot, targetOffset, copied);
            }

            trailRot[0] = rotation;
            trailCount = copied + targetOffset;
        }

        /// <summary>爆发帧的弹幕齐射:红白光束 + 影子弹幕,终结加量,歼灭态另出头颅</summary>
        private void FireShots(float slashRotation) {
            if (!Projectile.IsOwnedByLocalPlayer()) {
                return;
            }
            bool emp = Empowered;
            int combo = (int)ComboIndex;
            int beams = combo switch {
                0 => emp ? 2 : 1,
                1 => emp ? 1 : 0,
                _ => emp ? 2 : 1,
            };
            int shadows = combo switch {
                0 => emp ? 1 : 0,
                1 => emp ? 3 : 2,
                _ => emp ? 5 : 3,
            };

            Vector2 hand = Owner.GetPlayerStabilityCenter();
            Vector2 spawnPos = hand + slashRotation.ToRotationVector2() * BladeReach * 0.5f;
            float empFlag = emp ? 1f : 0f;

            for (int i = 0; i < beams; i++) {
                float offset = beams > 1 ? MathHelper.Lerp(-0.06f, 0.06f, i / (float)(beams - 1)) : 0f;
                Vector2 velocity = UnitToMouseV.RotatedBy(offset) * Item.shootSpeed;
                Projectile.NewProjectile(Projectile.GetSource_FromAI(), spawnPos, velocity
                    , ModContent.ProjectileType<DestroyersBeamEX>(), (int)(Projectile.damage * 0.5f)
                    , Projectile.knockBack * 0.4f, Projectile.owner, 0f, empFlag);
            }
            for (int i = 0; i < shadows; i++) {
                float offset = shadows > 1 ? MathHelper.Lerp(-0.4f, 0.4f, i / (float)(shadows - 1)) : 0f;
                Vector2 velocity = UnitToMouseV.RotatedBy(offset) * Item.shootSpeed * 0.62f;
                Projectile.NewProjectile(Projectile.GetSource_FromAI(), spawnPos, velocity
                    , ModContent.ProjectileType<DestroyerShadowBolt>(), (int)(Projectile.damage * 0.35f)
                    , Projectile.knockBack * 0.3f, Projectile.owner, 0f, empFlag);
            }
            if (emp && IsFinisher) {
                //歼灭协议终结斩:额外吐出毁灭者头颅
                Vector2 velocity = UnitToMouseV * 7f;
                Projectile.NewProjectile(Projectile.GetSource_FromAI(), hand - Vector2.UnitY * 20f, velocity
                    , ModContent.ProjectileType<DestroyerHeadMissile>(), (int)(Projectile.damage * 1.6f)
                    , Projectile.knockBack, Projectile.owner);
            }
        }

        private void UpdatePlayerPose() {
            Owner.heldProj = Projectile.whoAmI;
            Owner.direction = lockedDirection;
            Owner.itemTime = Owner.itemAnimation = 2;
            Owner.itemRotation = currentRotation;
            Owner.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.Full, currentRotation - MathHelper.PiOver2);
            Projectile.Center = Owner.GetPlayerStabilityCenter() + currentRotation.ToRotationVector2() * BladeReach * 0.55f;
            Projectile.timeLeft = 90;
        }

        /// <summary>终结斩全身参与:蓄力后仰、爆发前扑,支点钉在脚底</summary>
        private void ApplyBodyLean() {
            if (!IsFinisher) {
                return;
            }
            Owner.fullRotation = bodyLean * lockedDirection;
            Owner.fullRotationOrigin = new Vector2(Owner.width * 0.5f, Owner.height);
        }

        public override void OnKill(int timeLeft) {
            if (IsFinisher) {
                Owner.fullRotation = 0f;
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            //顿帧记账:预算内冻结几何,时长由收势扣回
            float want = IsFinisher ? 3f : 2f;
            float grant = MathF.Min(want, HitStopBudget - hitStopSpent);
            if (grant > 0f) {
                hitStopFrames += grant;
                hitStopSpent += grant;
            }

            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.NPCHit4 with { Volume = 0.6f, Pitch = -0.3f, MaxInstances = 3 }, target.Center);
                for (int i = 0; i < (IsFinisher ? 9 : 5); i++) {
                    PRTLoader.NewParticle<PRT_SparkAlpha>(target.Center, Main.rand.NextVector2Circular(7f, 7f)
                        , Main.rand.NextBool(4) ? Color.White : new Color(255, 40, 30)
                        , Main.rand.NextFloat(1f, 2f)).Configure(false, Main.rand.Next(10, 18));
                }
                //暗渣崩出:黑体材质的命中残料
                for (int i = 0; i < 3; i++) {
                    PRTLoader.NewParticle<PRT_GhostRainMist>(target.Center + Main.rand.NextVector2Circular(10f, 10f)
                        , Main.rand.NextVector2Circular(2.5f, 2f) - Vector2.UnitY * 0.6f
                        , new Color(14, 3, 5) * 0.8f, Main.rand.NextFloat(0.5f, 0.8f))?.Configure(Main.rand.Next(24, 40));
                }
                if (IsFinisher) {
                    Color warm = new Color(255, 70, 40);
                    PRTLoader.NewParticle<PRT_MechExplosion>(target.Center, Main.rand.NextVector2Circular(1.5f, 1.5f)
                        , warm, 0.9f).Configure(Main.rand.Next(18, 28), warm);
                }
            }

            if (IsFinisher && CWRClientConfig.Instance.ScreenVibration) {
                var modifier = new PunchCameraModifier(target.Center
                    , currentRotation.ToRotationVector2(), 5f, 5f, 9, 800f, FullName);
                Main.instance.CameraModifiers.Add(modifier);
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            if (!slashVisualActive) {
                return false;
            }

            Texture2D tex = TextureValue;
            Vector2 origin = tex.Size() / 2f;
            Vector2 hand = Owner.GetPlayerStabilityCenter();
            float dist = BladeReach * 0.5f * Projectile.scale;
            GetBladeDrawOrientation(out SpriteEffects effect, out float rotOffset);

            //挥砍残影:黑红双层,靠后的残影沉黑
            float angleDelta = MathF.Abs(currentRotation - lastRotation);
            float strength = MathHelper.Clamp((angleDelta - 0.04f) / 0.72f, 0f, 1f);
            int smearCount = Math.Min(6, Math.Max(1, (int)MathF.Ceiling(angleDelta / 0.18f)));
            for (int i = 1; i <= smearCount && strength > 0f; i++) {
                float amount = i / (float)(smearCount + 1);
                float rot = MathHelper.Lerp(currentRotation, lastRotation, amount);
                Vector2 pos = hand + rot.ToRotationVector2() * dist - Main.screenPosition;
                //前段残影泄红,后段沉入黑
                Color trailColor = amount < 0.45f
                    ? new Color(255, 45, 30) * (0.42f * strength * (1f - amount))
                    : new Color(30, 6, 10) * (0.55f * strength * (1f - amount));
                trailColor.A = 0;
                Main.EntitySpriteDraw(tex, pos, null, trailColor, rot + rotOffset, origin
                    , Projectile.scale, effect, 0);
            }
            return false;
        }

        private void GetBladeDrawOrientation(out SpriteEffects effect, out float rotOffset) {
            bool edgeFlip = swingSign * lockedDirection < 0;
            bool flipVertically = (lockedDirection < 0) != edgeFlip;
            effect = flipVertically ? SpriteEffects.FlipVertically : SpriteEffects.None;
            rotOffset = flipVertically ? -MathHelper.PiOver4 : MathHelper.PiOver4;
        }

        void IOverlayDrawable.DrawOverlay(SpriteBatch spriteBatch) {
            Texture2D tex = TextureValue;
            Vector2 origin = tex.Size() / 2f;
            Vector2 hand = Owner.GetPlayerStabilityCenter();
            float dist = BladeReach * 0.5f * Projectile.scale;
            GetBladeDrawOrientation(out SpriteEffects effect, out float rotOffset);

            //刀身本体 + 辉光层
            Color lightColor = Lighting.GetColor((int)(hand.X / 16f), (int)(hand.Y / 16f));
            Vector2 drawPos = hand + currentRotation.ToRotationVector2() * dist - Main.screenPosition;
            Main.EntitySpriteDraw(tex, drawPos, null, lightColor, currentRotation + rotOffset, origin
                , Projectile.scale, effect, 0);
            Texture2D glow = DestroyersBladeEX.Glow.Value;
            Main.EntitySpriteDraw(glow, drawPos, null, Color.White, currentRotation + rotOffset, glow.Size() / 2f
                , Projectile.scale, effect, 0);
        }

        void IPrimitiveDrawable.DrawPrimitives() {
            if (trailCount < 3 || trailFade <= 0.02f) {
                return;
            }
            Effect effect = EffectLoader.DestroyerEXSlash?.Value;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            if (effect == null || noise == null) {
                return;
            }

            var bars = new VertexPositionColorTexture[trailCount * 2];
            Vector2 center = Owner.GetPlayerStabilityCenter();
            float outer = (BladeReach + 22f) * Projectile.scale;
            float inner = BladeReach * 0.14f;
            float totalArc = 0f;
            for (int i = 1; i < trailCount; i++) {
                totalArc += MathF.Abs(trailRot[i - 1] - trailRot[i]);
            }
            float traveledArc = 0f;
            for (int i = 0; i < trailCount; i++) {
                if (i > 0) {
                    traveledArc += MathF.Abs(trailRot[i - 1] - trailRot[i]);
                }
                float factor = totalArc > 0.0001f
                    ? 1f - traveledArc / totalArc
                    : 1f - i / (float)Math.Max(trailCount - 1, 1);
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
            //噪声走 s1 寄存器,先绑贴图再 Apply
            device.Textures[1] = noise;
            device.SamplerStates[1] = SamplerState.LinearWrap;

            effect.Parameters["transformMatrix"]?.SetValue(VaultUtils.GetTransfromMatrix());
            effect.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            effect.Parameters["fadeAlpha"]?.SetValue(trailFade);
            effect.Parameters["empowerMix"]?.SetValue(Empowered ? 1f : 0f);
            effect.Parameters["segCount"]?.SetValue(MathF.Max(5f, SwingArc * 2.0f));
            foreach (EffectPass pass in effect.CurrentTechnique.Passes) {
                pass.Apply();
                device.DrawUserPrimitives(PrimitiveType.TriangleStrip, bars, 0, bars.Length - 2);
            }

            device.BlendState = origBlend;
            device.RasterizerState = origRaster;
        }
    }
}
