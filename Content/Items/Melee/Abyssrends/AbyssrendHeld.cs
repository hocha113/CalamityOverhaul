using CalamityOverhaul.Common;
using InnoVault.GameContent.BaseEntity;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.Graphics.CameraModifiers;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Melee.Abyssrends
{
    /// <summary>
    /// 裂渊持握。四拍：戳、侧斩、反斩、重戳。戳走持距，斩走弧角，都是收-爆-停。
    /// ai[0] 拍号 ai[1] 斩向
    /// </summary>
    internal class AbyssrendHeld : BaseHeldProj, IPrimitiveDrawable, IOverlayDrawable
    {
        public override string Texture => AbyssrendFX.ItemTexture;
        public override LocalizedText DisplayName => VaultUtils.GetLocalizedItemName<Abyssrend>();

        private const int PhaseRaise = 0;
        private const int PhaseHold = 1;
        private const int PhaseSlash = 2;
        private const int PhaseRecover = 3;
        private const float HeldScale = 0.88f;
        private const int TrailCap = 18;

        private int raiseDur = 5;
        private int holdDur = 1;
        private int slashDur = 6;
        private int recoverDur = 8;
        private int totalDur = 20;
        private float raiseBack = 1.15f;
        private float follow = 0.85f;
        private float stabPull = -24f;
        private float stabReach = 42f;

        private float baseAngle;
        private float swingDir = 1f;
        private int facingDir = 1;
        private float mainAngle;
        private float holdout;
        private float bodyLean;
        private Vector2 mainTip;
        private Vector2 handPos;
        private float trailFade = 1f;
        private int flashTimer;
        private int hitstopTimer;
        private bool hitstopApplied;
        private bool currentFired;
        private bool slashSoundPlayed;
        private readonly float[] trailRot = new float[TrailCap];
        private int trailCount;
        private readonly HashSet<int> hitNPCs = [];
        private bool leanApplied;
        private bool EdgeFlip => !IsThrust && swingDir * facingDir < 0;

        private int ComboStage => Math.Clamp((int)Projectile.ai[0], 0, 3);
        private bool IsThrust => ComboStage == 0 || ComboStage == 3;
        private bool IsFinisher => ComboStage == 3;

        private int Timer {
            get => (int)Projectile.localAI[0];
            set => Projectile.localAI[0] = value;
        }

        private int CurrentPhase {
            get {
                if (Timer <= raiseDur) {
                    return PhaseRaise;
                }
                if (Timer <= raiseDur + holdDur) {
                    return PhaseHold;
                }
                if (Timer <= raiseDur + holdDur + slashDur) {
                    return PhaseSlash;
                }
                return PhaseRecover;
            }
        }

        private float BladeLen => AbyssrendFX.BladeLength * HeldScale;
        private Vector2 Hand => Owner.GetPlayerStabilityCenter();
        private float ArcStart => baseAngle - (swingDir * raiseBack);
        private float ArcEnd => baseAngle + (swingDir * follow);

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 56;
            Projectile.friendly = true;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
            Projectile.ownerHitCheck = true;
            Projectile.timeLeft = 90;
            Projectile.CWR().NotSubjectToSpecialEffects = true;
            Projectile.CWR().PierceResist = true;
        }

        public override bool ShouldUpdatePosition() => false;

        public override void Initialize() {
            InitStage();
        }

        private void InitStage() {
            baseAngle = Projectile.velocity.ToRotation();
            float cos = MathF.Cos(baseAngle);
            facingDir = MathF.Abs(cos) < 0.05f ? Owner.direction : Math.Sign(cos);
            swingDir = Projectile.ai[1] >= 0f ? 1f : -1f;

            float speed = Owner.GetWeaponAttackSpeed(Item);
            if (speed <= 0f) {
                speed = 1f;
            }
            int D(int frames) => Math.Max(1, (int)MathF.Round(frames / speed));

            switch (ComboStage) {
                case 0:
                    raiseDur = D(5);
                    holdDur = D(1);
                    slashDur = D(6);
                    recoverDur = D(8);
                    stabPull = -22f;
                    stabReach = 38f;
                    break;
                case 1:
                    raiseDur = D(6);
                    holdDur = D(2);
                    slashDur = D(3);
                    recoverDur = D(9);
                    raiseBack = 1.12f;
                    follow = 0.82f;
                    break;
                case 2:
                    raiseDur = D(6);
                    holdDur = D(2);
                    slashDur = D(3);
                    recoverDur = D(9);
                    raiseBack = 1.18f;
                    follow = 0.88f;
                    break;
                default:
                    raiseDur = D(7);
                    holdDur = D(2);
                    slashDur = D(7);
                    recoverDur = D(10);
                    stabPull = -30f;
                    stabReach = 56f;
                    break;
            }
            totalDur = raiseDur + holdDur + slashDur + recoverDur;
        }

        public override void AI() {
            if (Item.type != ModContent.ItemType<Abyssrend>() || Owner.dead || !Owner.active) {
                Projectile.Kill();
                return;
            }

            if (hitstopTimer > 0) {
                hitstopTimer--;
            }
            else {
                Timer++;
            }
            if (flashTimer > 0) {
                flashTimer--;
            }

            int phase = CurrentPhase;
            UpdateTransform(phase);
            UpdatePlayerPose(phase);
            ApplyBodyLean();
            HandlePhaseEvents(phase);
            HandleParticles(phase);
            HandleLight(phase);

            if (Timer >= totalDur) {
                Projectile.Kill();
            }
        }

        private void UpdateTransform(int phase) {
            handPos = Hand;
            Vector2 dir = baseAngle.ToRotationVector2();

            if (IsThrust) {
                UpdateThrustTransform(phase, dir);
                return;
            }

            float chamber = ArcStart - (swingDir * 0.06f);
            float liftFrom = chamber + (swingDir * raiseBack * 0.5f);
            switch (phase) {
                case PhaseRaise: {
                    float t = Timer / (float)Math.Max(raiseDur, 1);
                    mainAngle = MathHelper.Lerp(liftFrom, chamber, EaseOutCubic(t));
                    holdout = MathHelper.Lerp(6f, -10f, t);
                    bodyLean = -0.05f * EaseOutCubic(t);
                    trailFade = 1f;
                    break;
                }
                case PhaseHold:
                    mainAngle = chamber;
                    holdout = -10f;
                    bodyLean = -0.05f;
                    break;
                case PhaseSlash: {
                    float t = (Timer - raiseDur - holdDur) / (float)Math.Max(slashDur, 1);
                    t = MathHelper.Clamp(t, 0f, 1f);
                    const float overshoot = 1.14f;
                    float progress = t < 0.62f
                        ? overshoot * SmoothStep01(t / 0.62f)
                        : MathHelper.Lerp(overshoot, 1f, SmoothStep01((t - 0.62f) / 0.38f));
                    mainAngle = MathHelper.Lerp(chamber, ArcEnd, progress);
                    holdout = MathHelper.Lerp(-10f, 22f, SmoothStep01(MathF.Min(t * 1.5f, 1f)));
                    bodyLean = MathHelper.Lerp(-0.05f, 0.07f, SmoothStep01(t));
                    RecordTrail(mainAngle);
                    trailFade = 1f;
                    break;
                }
                default: {
                    float t = (Timer - raiseDur - holdDur - slashDur) / (float)Math.Max(recoverDur, 1);
                    const float still = 0.30f;
                    if (t < still) {
                        mainAngle = ArcEnd;
                        holdout = 22f;
                        bodyLean = 0.07f;
                    }
                    else {
                        float returnT = SmoothStep01((t - still) / (1f - still));
                        mainAngle = MathHelper.Lerp(ArcEnd, baseAngle + (swingDir * 0.2f), returnT);
                        holdout = MathHelper.Lerp(22f, 6f, returnT);
                        bodyLean = MathHelper.Lerp(0.07f, 0f, returnT);
                    }
                    trailFade *= 0.80f;
                    break;
                }
            }
            mainTip = handPos + mainAngle.ToRotationVector2() * (BladeLen + holdout);
        }

        private void UpdateThrustTransform(int phase, Vector2 dir) {
            mainAngle = baseAngle;
            bodyLean = 0f;
            switch (phase) {
                case PhaseRaise: {
                    float t = Timer / (float)Math.Max(raiseDur, 1);
                    holdout = MathHelper.Lerp(10f, stabPull, EaseOutCubic(t));
                    if (IsFinisher) {
                        bodyLean = -0.07f * EaseOutCubic(t);
                    }
                    trailFade = 1f;
                    break;
                }
                case PhaseHold:
                    holdout = stabPull;
                    if (IsFinisher) {
                        bodyLean = -0.07f;
                    }
                    break;
                case PhaseSlash: {
                    float t = (Timer - raiseDur - holdDur) / (float)Math.Max(slashDur, 1);
                    float burst = 1f - MathF.Pow(1f - MathHelper.Clamp(t, 0f, 1f), 4.2f);
                    holdout = MathHelper.Lerp(stabPull, stabReach, burst);
                    if (IsFinisher) {
                        bodyLean = MathHelper.Lerp(-0.07f, 0.10f, SmoothStep01(t));
                    }
                    RecordTrail(mainAngle);
                    trailFade = 1f;
                    break;
                }
                default: {
                    float t = (Timer - raiseDur - holdDur - slashDur) / (float)Math.Max(recoverDur, 1);
                    const float still = 0.22f;
                    if (t < still) {
                        holdout = stabReach;
                        if (IsFinisher) {
                            bodyLean = 0.10f;
                        }
                    }
                    else {
                        float returnT = SmoothStep01((t - still) / (1f - still));
                        holdout = MathHelper.Lerp(stabReach, 8f, returnT);
                        if (IsFinisher) {
                            bodyLean = MathHelper.Lerp(0.10f, 0f, returnT);
                        }
                    }
                    trailFade *= 0.84f;
                    break;
                }
            }
            mainTip = handPos + dir * (BladeLen + holdout);
        }

        private void RecordTrail(float rot) {
            if (trailCount < TrailCap) {
                trailRot[trailCount++] = rot;
                return;
            }
            for (int i = 0; i < TrailCap - 1; i++) {
                trailRot[i] = trailRot[i + 1];
            }
            trailRot[TrailCap - 1] = rot;
        }

        private void UpdatePlayerPose(int phase) {
            Owner.ChangeDir(facingDir);
            Owner.heldProj = Projectile.whoAmI;
            Owner.itemTime = Owner.itemAnimation = 2;
            Player.CompositeArmStretchAmount stretch = phase is PhaseRaise or PhaseRecover
                ? Player.CompositeArmStretchAmount.ThreeQuarters
                : Player.CompositeArmStretchAmount.Full;
            Owner.SetCompositeArmFront(true, stretch, mainAngle - MathHelper.PiOver2);
            Owner.itemRotation = (mainAngle.ToRotationVector2() * facingDir).ToRotation();
            Projectile.Center = Vector2.Lerp(handPos, mainTip, 0.55f);
            Projectile.rotation = mainAngle;
            Projectile.timeLeft = 90;
        }

        private void ApplyBodyLean() {
            if (MathF.Abs(bodyLean) < 0.001f && !leanApplied) {
                return;
            }
            Owner.fullRotation = bodyLean * facingDir;
            Owner.fullRotationOrigin = new Vector2(Owner.width * 0.5f, Owner.height);
            leanApplied = true;
        }

        public override void OnKill(int timeLeft) {
            if (leanApplied) {
                Owner.fullRotation = 0f;
                leanApplied = false;
            }
        }

        private void HandlePhaseEvents(int phase) {
            if (phase == PhaseHold && flashTimer < 2) {
                flashTimer = 8;
            }

            if (phase == PhaseSlash && !slashSoundPlayed) {
                slashSoundPlayed = true;
                if (IsThrust) {
                    SoundEngine.PlaySound(SoundID.Item71 with {
                        Pitch = IsFinisher ? -0.15f : 0.22f,
                        Volume = IsFinisher ? 0.7f : 0.5f
                    }, handPos);
                    SoundEngine.PlaySound(SoundID.SplashWeak with { Pitch = 0.4f, Volume = 0.45f }, handPos);
                }
                else {
                    SoundEngine.PlaySound(SoundID.DD2_MonkStaffSwing with {
                        Pitch = ComboStage == 2 ? -0.05f : 0.18f,
                        Volume = 0.55f
                    }, handPos);
                    SoundEngine.PlaySound(SoundID.Item85 with { Pitch = 0.35f, Volume = 0.35f }, handPos);
                    if (CWRClientConfig.Instance.ScreenVibration) {
                        Vector2 tang = mainAngle.ToRotationVector2().RotatedBy(swingDir * MathHelper.PiOver2);
                        Main.instance.CameraModifiers.Add(new PunchCameraModifier(
                            handPos, tang, 3.4f, 5f, 6, 400f, FullName));
                    }
                }
            }

            if (phase == PhaseSlash && !currentFired) {
                float t = (Timer - raiseDur - holdDur) / (float)Math.Max(slashDur, 1);
                if (t >= 0.35f) {
                    currentFired = true;
                    FireUndercurrent();
                }
            }
        }

        private void FireUndercurrent() {
            if (!Projectile.IsOwnedByLocalPlayer()) {
                return;
            }
            Vector2 dir = mainAngle.ToRotationVector2();
            Vector2 spawn = mainTip - dir * 8f;
            int shots = IsFinisher ? 2 : 1;
            int dmg = (int)(Projectile.damage * 0.55f);
            for (int i = 0; i < shots; i++) {
                float spread = shots == 1 ? 0f : (i == 0 ? -0.18f : 0.18f);
                Vector2 vel = dir.RotatedBy(spread) * (13.5f + ComboStage * 0.8f);
                int target = AbyssrendUndercurrent.FindTarget(spawn, Owner, 720f);
                Projectile.NewProjectile(Owner.GetSource_ItemUse(Item), spawn, vel
                    , ModContent.ProjectileType<AbyssrendUndercurrent>()
                    , dmg, Projectile.knockBack * 0.35f, Owner.whoAmI
                    , ai0: Main.rand.NextFloat(1000f), ai1: target);
            }
        }

        private void HandleParticles(int phase) {
            if (VaultUtils.isServer || phase != PhaseSlash) {
                return;
            }
            if (Timer % 2 != 0) {
                return;
            }
            Vector2 along = mainAngle.ToRotationVector2();
            PRTLoader.NewParticle<PRT_AbyssGlob>(mainTip + Main.rand.NextVector2Circular(6f, 6f)
                , along.RotatedByRandom(0.4f) * Main.rand.NextFloat(1.2f, 3.4f)
                , Color.Lerp(AbyssrendFX.Deep, AbyssrendFX.Body, Main.rand.NextFloat())
                , Main.rand.NextFloat(0.35f, 0.6f))
                .Configure(Main.rand.Next(10, 16));
            if (Main.rand.NextBool(2)) {
                PRTLoader.NewParticle<PRT_AbyssSpark>(mainTip
                    , along.RotatedByRandom(0.8f) * Main.rand.NextFloat(1.5f, 4f)
                    , AbyssrendFX.Cyan, Main.rand.NextFloat(0.7f, 1.2f))
                    .Configure(Main.rand.Next(8, 14));
            }
        }

        private void HandleLight(int phase) {
            float mul = phase == PhaseSlash ? 1.1f : 0.55f;
            Lighting.AddLight(mainTip, 0.12f * mul, 0.45f * mul, 0.55f * mul);
        }

        public override bool? CanDamage() => CurrentPhase == PhaseSlash ? true : false;

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            if (CurrentPhase != PhaseSlash) {
                return false;
            }
            if (targetHitbox.Distance(handPos) <= (IsThrust ? 42f : 50f)) {
                return true;
            }
            float width = IsThrust ? 38f : 50f;
            float collisionPoint = 0f;
            Vector2 tip = mainTip + mainAngle.ToRotationVector2() * 14f;
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size()
                , handPos, tip, width, ref collisionPoint);
        }

        public override void CutTiles() {
            if (CurrentPhase != PhaseSlash) {
                return;
            }
            Vector2 tip = mainTip + (mainTip - handPos).SafeNormalize(Vector2.Zero) * 14f;
            Utils.PlotTileLine(handPos, tip, IsThrust ? 36f : 48f, DelegateMethods.CutTiles);
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {
            modifiers.HitDirectionOverride = facingDir;
            modifiers.SourceDamage *= ComboStage switch {
                0 => 1.05f,
                1 => 0.92f,
                2 => 0.96f,
                _ => 1.38f
            };
            if (target.IsWormBody()) {
                modifiers.FinalDamage *= 0.45f;
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (hitNPCs.Add(target.whoAmI)) {
                ItemLoader.OnHitNPC(Item, Owner, target, hit, damageDone);
                NPCLoader.OnHitByItem(target, Owner, Item, hit, damageDone);
                PlayerLoader.OnHitNPC(Owner, target, hit, damageDone);
            }
            target.AddBuff(BuffID.Wet, 180);

            if (!hitstopApplied && CurrentPhase == PhaseSlash) {
                hitstopApplied = true;
                int n = IsFinisher ? 2 : 1;
                hitstopTimer = n;
                totalDur = Math.Max(raiseDur + holdDur + slashDur + 1, totalDur - n);
            }

            if (CWRClientConfig.Instance.ScreenVibration) {
                float punch = IsFinisher ? 5.5f : (IsThrust ? 2.4f : 3.8f);
                Main.instance.CameraModifiers.Add(new PunchCameraModifier(
                    target.Center, mainAngle.ToRotationVector2(), punch, 6f, IsFinisher ? 8 : 6, 480f, FullName));
            }

            if (VaultUtils.isServer) {
                return;
            }
            for (int i = 0; i < 5; i++) {
                PRTLoader.NewParticle<PRT_AbyssGlob>(target.Center
                    , Main.rand.NextVector2Circular(4.5f, 4.5f)
                    , Color.Lerp(AbyssrendFX.Deep, AbyssrendFX.Body, Main.rand.NextFloat())
                    , Main.rand.NextFloat(0.4f, 0.7f))
                    .Configure(Main.rand.Next(12, 18));
            }
            for (int i = 0; i < 4; i++) {
                PRTLoader.NewParticle<PRT_AbyssSpark>(target.Center
                    , Main.rand.NextVector2Circular(5f, 5f)
                    , AbyssrendFX.Cyan, Main.rand.NextFloat(0.8f, 1.3f))
                    .Configure(Main.rand.Next(8, 14));
            }
        }

        public override bool PreDraw(ref Color lightColor) => false;

        void IOverlayDrawable.DrawOverlay(SpriteBatch spriteBatch) {
            Texture2D tex = TextureAssets.Projectile[Type]?.Value;
            if (tex == null) {
                return;
            }
            AbyssrendFX.ComputeBladeDrawXform(tex, mainAngle, facingDir, EdgeFlip
                , out Vector2 origin, out float rot, out SpriteEffects flip);
            Vector2 drawPos = handPos - Main.screenPosition;
            Color light = Lighting.GetColor((int)(handPos.X / 16f), (int)(handPos.Y / 16f));
            float scale = HeldScale * Projectile.scale;

            if (CurrentPhase == PhaseSlash && IsThrust) {
                Color trail = AbyssrendFX.Cyan * 0.16f;
                trail.A = 0;
                for (int i = 1; i <= 2; i++) {
                    float ghostAng = mainAngle;
                    AbyssrendFX.ComputeBladeDrawXform(tex, ghostAng, facingDir, EdgeFlip
                        , out Vector2 gOrigin, out float gRot, out SpriteEffects gFlip);
                    Vector2 ghostPos = handPos + baseAngle.ToRotationVector2() * (-i * 12f) - Main.screenPosition;
                    Main.EntitySpriteDraw(tex, ghostPos, null, trail * (1f - i / 3f), gRot, gOrigin, scale, gFlip, 0);
                }
            }

            Main.EntitySpriteDraw(tex, drawPos, null, light, rot, origin, scale, flip, 0);
            Color glow = AbyssrendFX.Cyan;
            glow.A = 0;
            float glowMul = CurrentPhase == PhaseSlash ? 0.35f : 0.18f;
            Main.EntitySpriteDraw(tex, drawPos, null, glow * glowMul, rot, origin, scale, flip, 0);
        }

        void IPrimitiveDrawable.DrawPrimitives() {
            if (trailFade <= 0.02f) {
                return;
            }
            if (IsThrust) {
                if (CurrentPhase is not PhaseSlash and not PhaseRecover) {
                    return;
                }
                Vector2 dir = baseAngle.ToRotationVector2();
                Vector2[] path = new Vector2[3];
                path[0] = handPos + dir * MathF.Max(0f, holdout * 0.2f);
                path[1] = Vector2.Lerp(handPos, mainTip, 0.45f);
                path[2] = mainTip;
                float width = IsFinisher ? 26f : 20f;
                AbyssrendFX.DrawPathStrip(path, 3, _ => width * trailFade, trailFade);
                return;
            }
            if (trailCount < 3) {
                return;
            }
            float outer = BladeLen + holdout + 18f;
            float inner = BladeLen * 0.42f;
            AbyssrendFX.DrawArcStrip(handPos, trailRot, trailCount, inner, outer, trailFade);
        }

        private static float EaseOutCubic(float t) => 1f - MathF.Pow(1f - t, 3f);
        private static float SmoothStep01(float value) {
            value = MathHelper.Clamp(value, 0f, 1f);
            return value * value * (3f - 2f * value);
        }
    }
}
