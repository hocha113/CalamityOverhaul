using CalamityOverhaul.Common;
using InnoVault.GameContent.BaseEntity;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
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
    /// 裂渊右键钳杀。ai[1] 目标 NPC whoAmI，生成包已带上，避免事后填写赶不上同步。
    /// 节奏：后撤张口 → 4 帧咬合 → 挤压颤 → 静锁 → 空化 → 后坐。小怪会被拉到钳口；Boss 不位移只吃压伤
    /// </summary>
    internal class AbyssrendClampHeld : BaseHeldProj, IOverlayDrawable
    {
        public override string Texture => AbyssrendFX.ItemTexture;
        public override LocalizedText DisplayName => VaultUtils.GetLocalizedItemName<Abyssrend>();

        private const int LungeDur = 12;
        private const int SnapDur = 4;
        private const int CrushDur = 36;
        private const int LockDur = 12;
        private const int RecoverDur = 12;
        private const int SnapAt = LungeDur;
        private const int CrushAt = SnapAt + SnapDur;
        private const int LockAt = CrushAt + CrushDur;
        private const int BurstAt = LockAt + LockDur;
        private const int TotalFrames = BurstAt + RecoverDur;
        private const int HitCooldown = 8;
        private const float HeldScale = 0.92f;
        private const int SuccessCooldown = 240;
        private const int MissCooldown = 90;

        private int facingDir = 1;
        private float mainAngle;
        private float holdout;
        private float cock;
        private float drawScale = HeldScale;
        private float bodyLean;
        private float jawProgress;
        private float fieldFade = 0.5f;
        private float glowMul;
        private Vector2 handPos;
        private Vector2 pincerPos;
        private bool burstFired;
        private bool snapFired;
        private bool missed;
        private bool leanApplied;
        private int crushHits;
        private int hitstopTimer;

        private int Timer {
            get => (int)Projectile.localAI[0];
            set => Projectile.localAI[0] = value;
        }

        private int TargetIndex => (int)Projectile.ai[1];

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 48;
            Projectile.friendly = true;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = HitCooldown;
            Projectile.ownerHitCheck = false;
            Projectile.timeLeft = TotalFrames + 8;
            Projectile.CWR().NotSubjectToSpecialEffects = true;
        }

        public override bool ShouldUpdatePosition() => false;

        public override void Initialize() {
            facingDir = Math.Sign(Projectile.velocity.X);
            if (facingDir == 0) {
                facingDir = Owner.direction;
            }
            missed = !TryGetTarget(out _);
            if (Projectile.IsOwnedByLocalPlayer()) {
                Owner.GetModPlayer<AbyssrendPlayer>().SetClampCooldown(missed ? MissCooldown : SuccessCooldown);
            }
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
            handPos = Owner.GetPlayerStabilityCenter();

            if (missed || !TryGetTarget(out NPC target)) {
                MissAI();
                return;
            }

            CrushAI(target);

            if (Timer >= TotalFrames) {
                Projectile.Kill();
            }
        }

        private void MissAI() {
            Vector2 dir = Projectile.velocity.SafeNormalize(Vector2.UnitX * facingDir);
            EvaluatePose(Timer, 90f, out float reach);
            mainAngle = dir.ToRotation() + cock;
            pincerPos = handPos + dir * reach;
            ApplyOwnerPose();
            if (Timer == SnapAt) {
                SoundEngine.PlaySound(SoundID.Item1 with { Pitch = -0.15f, Volume = 0.45f }, handPos);
            }
            if (Timer == CrushAt) {
                SoundEngine.PlaySound(SoundID.Item1 with { Pitch = -0.45f, Volume = 0.55f }, handPos);
            }
            if (Timer >= CrushAt + 8) {
                Projectile.Kill();
            }
        }

        private void CrushAI(NPC target) {
            Vector2 to = target.Center - handPos;
            facingDir = Math.Sign(to.X);
            if (facingDir == 0) {
                facingDir = Owner.direction;
            }

            float dist = MathHelper.Clamp(to.Length(), 48f, AbyssrendFX.BladeLength * HeldScale + 24f);
            EvaluatePose(Timer, dist, out float reach);
            mainAngle = to.ToRotation() + cock;
            pincerPos = handPos + mainAngle.ToRotationVector2() * reach;

            bool canDrag = !target.boss && target.knockBackResist > 0f && target.realLife < 0;
            if (Main.netMode != NetmodeID.MultiplayerClient && canDrag && Timer >= SnapAt && Timer < BurstAt) {
                Vector2 dest = handPos + to.SafeNormalize(Vector2.UnitX * facingDir) * 70f;
                target.Center = Vector2.Lerp(target.Center, dest, 0.22f);
                target.velocity *= 0.35f;
            }

            if (Timer >= SnapAt && Timer < BurstAt) {
                Owner.velocity *= 0.72f;
            }

            ApplyOwnerPose();
            HandleSnap(target);
            HandleBurst(target);
            HandleClampFx(target);
        }

        private void EvaluatePose(int timer, float dist, out float reach) {
            float blade = AbyssrendFX.BladeLength * HeldScale;
            if (timer < SnapAt) {
                float t = timer / (float)Math.Max(LungeDur, 1);
                t = SmoothStep01(t);
                holdout = MathHelper.Lerp(8f, -30f, t);
                cock = MathHelper.Lerp(0f, -0.38f * facingDir, t);
                drawScale = MathHelper.Lerp(HeldScale, HeldScale * 1.08f, t);
                bodyLean = -0.09f * t;
                jawProgress = 0.08f * t;
                fieldFade = 0.45f + 0.2f * t;
                glowMul = 0.08f + 0.06f * t;
                reach = dist * MathHelper.Lerp(0.62f, 0.48f, t);
                return;
            }
            if (timer < CrushAt) {
                float t = (timer - SnapAt) / (float)Math.Max(SnapDur, 1);
                t = MathHelper.Clamp(t, 0f, 1f);
                const float overshoot = 1.18f;
                float slam = t < 0.7f
                    ? overshoot * SmoothStep01(t / 0.7f)
                    : MathHelper.Lerp(overshoot, 1f, SmoothStep01((t - 0.7f) / 0.3f));
                holdout = MathHelper.Lerp(-30f, 20f, slam);
                cock = MathHelper.Lerp(-0.38f * facingDir, 0f, SmoothStep01(t));
                drawScale = HeldScale * MathHelper.Lerp(1.08f, 1.18f, SmoothStep01(MathF.Min(t * 1.4f, 1f)));
                bodyLean = MathHelper.Lerp(-0.09f, 0.12f, SmoothStep01(t));
                jawProgress = slam;
                fieldFade = 1f;
                glowMul = 0.55f;
                reach = MathHelper.Lerp(dist * 0.48f, MathF.Min(dist * 0.82f, blade + 18f), slam);
                return;
            }
            if (timer < LockAt) {
                float t = (timer - CrushAt) / (float)Math.Max(CrushDur, 1);
                float decay = 1f - SmoothStep01(t);
                float tremble = decay * 0.035f * MathF.Sin(timer * 2.4f);
                holdout = 16f + tremble * 18f;
                cock = tremble * facingDir;
                drawScale = HeldScale * (1.04f + decay * 0.03f * MathF.Sin(timer * 1.7f));
                bodyLean = 0.10f + tremble;
                jawProgress = 1f;
                fieldFade = 0.88f - 0.28f * t;
                glowMul = 0.22f * decay + 0.08f;
                reach = MathF.Min(dist * 0.78f, blade + 12f);
                return;
            }
            if (timer < BurstAt) {
                holdout = 14f;
                cock = 0f;
                drawScale = HeldScale;
                bodyLean = 0.08f;
                jawProgress = 1f;
                fieldFade = 0.42f;
                glowMul = 0.05f;
                reach = MathF.Min(dist * 0.76f, blade + 8f);
                return;
            }
            float rec = (timer - BurstAt) / (float)Math.Max(RecoverDur, 1);
            rec = SmoothStep01(MathHelper.Clamp(rec, 0f, 1f));
            if (rec < 0.28f) {
                float kick = SmoothStep01(rec / 0.28f);
                holdout = MathHelper.Lerp(14f, -22f, kick);
                bodyLean = MathHelper.Lerp(0.08f, -0.04f, kick);
                glowMul = 0.4f;
            }
            else {
                float back = SmoothStep01((rec - 0.28f) / 0.72f);
                holdout = MathHelper.Lerp(-22f, 4f, back);
                bodyLean = MathHelper.Lerp(-0.04f, 0f, back);
                glowMul = MathHelper.Lerp(0.4f, 0f, back);
            }
            cock = 0f;
            drawScale = HeldScale;
            jawProgress = 1f;
            fieldFade = MathHelper.Lerp(0.42f, 0f, rec);
            reach = MathF.Min(dist * 0.7f, blade) + holdout * 0.15f;
        }

        private void ApplyOwnerPose() {
            Owner.ChangeDir(facingDir);
            Owner.heldProj = Projectile.whoAmI;
            Owner.itemTime = Owner.itemAnimation = 2;
            Player.CompositeArmStretchAmount stretch = Timer < SnapAt || Timer >= BurstAt
                ? Player.CompositeArmStretchAmount.ThreeQuarters
                : Player.CompositeArmStretchAmount.Full;
            Owner.SetCompositeArmFront(true, stretch, mainAngle - MathHelper.PiOver2);
            Projectile.Center = pincerPos;
            Projectile.rotation = mainAngle;
            if (MathF.Abs(bodyLean) > 0.001f || leanApplied) {
                Owner.fullRotation = bodyLean * facingDir;
                Owner.fullRotationOrigin = new Vector2(Owner.width * 0.5f, Owner.height);
                leanApplied = true;
            }
        }

        public override void OnKill(int timeLeft) {
            if (leanApplied) {
                Owner.fullRotation = 0f;
                leanApplied = false;
            }
        }

        private void HandleSnap(NPC target) {
            if (Timer == SnapAt) {
                SoundEngine.PlaySound(SoundID.DD2_MonkStaffSwing with { Pitch = -0.25f, Volume = 0.6f }, handPos);
            }
            if (snapFired || Timer < CrushAt) {
                return;
            }
            snapFired = true;
            hitstopTimer = 3;
            SoundEngine.PlaySound(SoundID.NPCHit4 with { Pitch = -0.45f, Volume = 0.7f }, target.Center);
            SoundEngine.PlaySound(SoundID.Item85 with { Pitch = -0.2f, Volume = 0.55f }, target.Center);
            SoundEngine.PlaySound(SoundID.DD2_MonkStaffGroundImpact with { Pitch = -0.2f, Volume = 0.55f }, target.Center);
            if (CWRClientConfig.Instance.ScreenVibration) {
                Main.instance.CameraModifiers.Add(new PunchCameraModifier(
                    target.Center, mainAngle.ToRotationVector2(), 7.2f, 7f, 10, 620f, FullName));
            }
            if (VaultUtils.isServer) {
                return;
            }
            for (int i = 0; i < 8; i++) {
                PRTLoader.NewParticle<PRT_AbyssSpark>(target.Center
                    , Main.rand.NextVector2Circular(6f, 6f)
                    , AbyssrendFX.Cyan, Main.rand.NextFloat(0.9f, 1.4f))
                    .Configure(Main.rand.Next(8, 14));
            }
        }

        private void HandleBurst(NPC target) {
            if (burstFired || Timer < BurstAt) {
                return;
            }
            burstFired = true;
            FireBurst(target.Center);
        }

        private void HandleClampFx(NPC target) {
            Lighting.AddLight(pincerPos, 0.15f, 0.55f, 0.62f);
            if (VaultUtils.isServer) {
                return;
            }
            if (Timer >= CrushAt && Timer < LockAt && Timer % 3 == 0) {
                PRTLoader.NewParticle<PRT_AbyssGlob>(target.Center + Main.rand.NextVector2Circular(12f, 12f)
                    , Main.rand.NextVector2Circular(1.6f, 1.6f)
                    , Color.Lerp(AbyssrendFX.Deep, AbyssrendFX.Body, Main.rand.NextFloat())
                    , Main.rand.NextFloat(0.4f, 0.7f))
                    .Configure(Main.rand.Next(12, 20));
            }
            if (Timer >= CrushAt && Timer < LockAt && Timer % 10 == 0) {
                SoundEngine.PlaySound(SoundID.SplashWeak with {
                    Pitch = -0.3f + crushHits * 0.04f,
                    Volume = 0.35f,
                    MaxInstances = 3
                }, target.Center);
            }
        }

        private void FireBurst(Vector2 at) {
            SoundEngine.PlaySound(SoundID.Item62 with { Pitch = -0.25f, Volume = 0.75f }, at);
            SoundEngine.PlaySound(SoundID.Item85 with { Pitch = -0.45f, Volume = 0.8f }, at);
            if (CWRClientConfig.Instance.ScreenVibration) {
                Main.instance.CameraModifiers.Add(new PunchCameraModifier(
                    at, Vector2.UnitY, 8.5f, 8f, 12, 700f, FullName));
            }
            if (!Projectile.IsOwnedByLocalPlayer()) {
                return;
            }
            int dmg = (int)(Projectile.damage * 2.6f);
            Projectile.NewProjectile(Owner.GetSource_ItemUse(Item), at, Vector2.Zero
                , ModContent.ProjectileType<AbyssrendBurst>()
                , dmg, Projectile.knockBack * 1.4f, Owner.whoAmI, ai0: 1f);
        }

        public override bool? CanDamage() {
            if (missed || Timer < SnapAt || Timer >= BurstAt) {
                return false;
            }
            return true;
        }

        public override bool? CanHitNPC(NPC target) {
            if (missed || target.whoAmI != TargetIndex) {
                return false;
            }
            if (target.realLife >= 0 && TargetIndex != target.realLife && target.whoAmI != target.realLife) {
                return false;
            }
            return null;
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            if (CanDamage() != true) {
                return false;
            }
            return targetHitbox.Distance(pincerPos) <= 64f;
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {
            modifiers.HitDirectionOverride = facingDir;
            modifiers.SourceDamage *= Timer < CrushAt ? 0.85f : 0.42f;
            modifiers.Knockback *= 0f;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            crushHits++;
            target.AddBuff(BuffID.Wet, 240);
            if (VaultUtils.isServer) {
                return;
            }
            PRTLoader.NewParticle<PRT_AbyssSpark>(pincerPos
                , Main.rand.NextVector2Circular(3f, 3f)
                , AbyssrendFX.Cyan, Main.rand.NextFloat(0.8f, 1.2f))
                .Configure(10);
        }

        public override bool PreDraw(ref Color lightColor) {
            if (!missed && TryGetTarget(out NPC target) && Timer < BurstAt) {
                float jaw = MathHelper.Lerp(1.38f, 0.86f, jawProgress);
                float radius = (MathF.Max(target.width, target.height) * 0.9f + 40f) * jaw;
                AbyssrendFX.DrawCanvasTech("TechClamp", target.Center, AbyssrendFX.QuadPx(radius)
                    , jawProgress, fieldFade);
            }
            return false;
        }

        void IOverlayDrawable.DrawOverlay(SpriteBatch spriteBatch) {
            Texture2D tex = TextureAssets.Projectile[Type]?.Value;
            if (tex == null) {
                return;
            }
            AbyssrendFX.ComputeBladeDrawXform(tex, mainAngle, facingDir, false
                , out Vector2 origin, out float rot, out SpriteEffects flip);
            Vector2 drawPos = handPos + mainAngle.ToRotationVector2() * holdout - Main.screenPosition;
            Color light = Lighting.GetColor((int)(handPos.X / 16f), (int)(handPos.Y / 16f));

            if (Timer >= SnapAt && Timer < CrushAt) {
                Color trail = AbyssrendFX.Cyan * 0.18f;
                trail.A = 0;
                Vector2 along = mainAngle.ToRotationVector2();
                for (int i = 1; i <= 2; i++) {
                    Vector2 ghostPos = drawPos - along * (i * 16f);
                    Main.EntitySpriteDraw(tex, ghostPos, null, trail * (1f - i / 3f), rot, origin
                        , drawScale, flip, 0);
                }
            }

            Main.EntitySpriteDraw(tex, drawPos, null, light, rot, origin, drawScale, flip, 0);
            if (glowMul > 0.02f) {
                Color glow = AbyssrendFX.Cyan;
                glow.A = 0;
                Main.EntitySpriteDraw(tex, drawPos, null, glow * glowMul, rot, origin, drawScale, flip, 0);
            }
        }

        private bool TryGetTarget(out NPC npc) {
            npc = null;
            int idx = TargetIndex;
            if (idx < 0 || idx >= Main.maxNPCs) {
                return false;
            }
            npc = Main.npc[idx];
            if (!npc.active || npc.dontTakeDamage || npc.friendly) {
                return false;
            }
            if (npc.realLife >= 0) {
                npc = Main.npc[npc.realLife];
                if (!npc.active) {
                    return false;
                }
            }
            return true;
        }

        /// <summary>准星锥内最近可追目标，优先贴鼠标。无目标返回 -1</summary>
        public static int FindTarget(Player player) {
            Vector2 origin = player.Center;
            Vector2 aim = (Main.MouseWorld - origin).SafeNormalize(Vector2.UnitX);
            int best = -1;
            float bestScore = float.MaxValue;
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npc = Main.npc[i];
                if (!npc.CanBeChasedBy(player)) {
                    continue;
                }
                int id = npc.realLife >= 0 ? npc.realLife : npc.whoAmI;
                Vector2 center = Main.npc[id].Center;
                Vector2 to = center - origin;
                float dist = to.Length();
                if (dist > 300f) {
                    continue;
                }
                float dot = Vector2.Dot(to.SafeNormalize(Vector2.Zero), aim);
                if (dot < 0.25f && dist > 90f) {
                    continue;
                }
                float mouse = Vector2.Distance(center, Main.MouseWorld);
                float score = dist * 0.35f + mouse * 0.65f - dot * 40f;
                if (score < bestScore) {
                    bestScore = score;
                    best = id;
                }
            }
            return best;
        }

        private static float SmoothStep01(float value) {
            value = MathHelper.Clamp(value, 0f, 1f);
            return value * value * (3f - 2f * value);
        }
    }
}
