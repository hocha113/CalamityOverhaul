using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Buffs;
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
    /// 裂渊右键钳杀：张口掀起吸流把面前的敌人拽到钳口，高举过顶后狠狠砸向近前地面，
    /// 落点引爆空化并施加渊压。NPC 位移只在服务端写；Boss、免击退与蠕虫体节不受拖拽，只吃砸击
    /// </summary>
    internal class AbyssrendClampHeld : BaseHeldProj, IOverlayDrawable
    {
        public override string Texture => AbyssrendFX.ItemTexture;
        public override LocalizedText DisplayName => VaultUtils.GetLocalizedItemName<Abyssrend>();

        //吸 → 举 → 砸 → 落点停帧 → 收
        private const int SuckDur = 22;
        private const int RaiseDur = 8;
        private const int SlamDur = 5;
        private const int ImpactHold = 6;
        private const int RecoverDur = 10;
        private const int SuckEnd = SuckDur;
        private const int RaiseEnd = SuckEnd + RaiseDur;
        private const int SlamEnd = RaiseEnd + SlamDur;
        private const int ImpactEnd = SlamEnd + ImpactHold;
        private const int TotalFrames = ImpactEnd + RecoverDur;

        private const float HeldScale = 0.92f;
        private const float SuckRange = 360f;
        private const float GripRadius = 56f;
        private const int MaxCaught = 6;
        private const float SweepArc = 3.3f;
        private const int SuccessCooldown = 240;
        private const int MissCooldown = 90;

        private int facingDir = 1;
        private float aimLock;
        private float raiseAngle;
        private float slamAngle;
        private float currentRotation;
        private float lastRotation;
        private float holdout;
        private float bodyLean;
        private bool leanApplied;
        private Vector2 handPos;
        private Vector2 tipPos;
        private Vector2 impactPoint;
        private bool impactDone;
        private int hitstopTimer;
        //服务端专用：被吸住待砸的目标，客户端只看同步来的 NPC 位置
        private readonly List<int> caught = [];

        private int Timer {
            get => (int)Projectile.localAI[0];
            set => Projectile.localAI[0] = value;
        }

        private float BladeLenScaled => AbyssrendFX.BladeLength * HeldScale;
        private Vector2 JawPoint => handPos + currentRotation.ToRotationVector2() * (BladeLenScaled * 0.72f);

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 48;
            Projectile.friendly = true;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
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

            //朝左时统一到 [π/2, 3π/2] 邻域，抬举与下砸的插值才会走身前
            aimLock = Projectile.velocity.ToRotation();
            if (facingDir < 0 && aimLock < 0f) {
                aimLock += MathHelper.TwoPi;
            }
            raiseAngle = -MathHelper.PiOver2 - facingDir * 0.55f;
            if (facingDir < 0) {
                raiseAngle += MathHelper.TwoPi;
            }
            slamAngle = raiseAngle + facingDir * SweepArc;
            currentRotation = lastRotation = aimLock;

            if (Projectile.IsOwnedByLocalPlayer()) {
                Owner.GetModPlayer<AbyssrendPlayer>().SetClampCooldown(ZoneHasTarget() ? SuccessCooldown : MissCooldown);
            }
        }

        //施放瞬间身前是否有可追目标，只影响冷却判定
        private bool ZoneHasTarget() {
            Vector2 aimDir = aimLock.ToRotationVector2();
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npc = Main.npc[i];
                if (!npc.CanBeChasedBy(Projectile)) {
                    continue;
                }
                Vector2 to = npc.Center - Owner.Center;
                float dist = to.Length();
                if (dist > SuckRange) {
                    continue;
                }
                if (Vector2.Dot(to.SafeNormalize(Vector2.Zero), aimDir) < 0.3f && dist > 90f) {
                    continue;
                }
                return true;
            }
            return false;
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
            lastRotation = currentRotation;

            UpdatePose();
            HandleSuction();
            HandlePinned();
            HandleImpact();
            HandleAudioVisual();
            ApplyOwnerPose();

            if (Timer >= TotalFrames) {
                Projectile.Kill();
            }
        }

        private void UpdatePose() {
            if (Timer < SuckEnd) {
                float t = Timer / (float)SuckEnd;
                currentRotation = aimLock;
                holdout = -4f + 6f * MathF.Sin(t * MathHelper.Pi);
                bodyLean = 0.03f * SmoothStep01(t * 2f);
            }
            else if (Timer < RaiseEnd) {
                //高举过顶：越举越慢，顶点即蓄力
                float t = (Timer - SuckEnd) / (float)RaiseDur;
                currentRotation = MathHelper.Lerp(aimLock, raiseAngle, EaseOutCubic(t));
                holdout = MathHelper.Lerp(0f, -14f, t);
                bodyLean = MathHelper.Lerp(0.03f, -0.12f, SmoothStep01(t));
            }
            else if (Timer < SlamEnd) {
                //下砸：一路加速砸进落点，不减速
                float t = (Timer - RaiseEnd) / (float)SlamDur;
                float progress = t * t;
                currentRotation = MathHelper.Lerp(raiseAngle, slamAngle, progress);
                holdout = MathHelper.Lerp(-14f, 26f, t);
                bodyLean = MathHelper.Lerp(-0.12f, 0.14f, progress);
            }
            else if (Timer < ImpactEnd) {
                //落点停帧：埋在地里，带衰减震颤
                float t = (Timer - SlamEnd) / (float)ImpactHold;
                float decay = 1f - SmoothStep01(t);
                currentRotation = slamAngle + decay * 0.03f * MathF.Sin(Timer * 2.6f);
                holdout = 18f;
                bodyLean = MathHelper.Lerp(0.14f, 0.08f, t);
            }
            else {
                float t = (Timer - ImpactEnd) / (float)RecoverDur;
                const float still = 0.3f;
                float returnT = SmoothStep01((t - still) / (1f - still));
                currentRotation = MathHelper.Lerp(slamAngle, aimLock, returnT);
                holdout = MathHelper.Lerp(18f, 2f, returnT);
                bodyLean = MathHelper.Lerp(0.08f, 0f, returnT);
            }
            tipPos = handPos + currentRotation.ToRotationVector2() * (BladeLenScaled + holdout);
        }

        //吸流：位移只在服务端写。Boss、免击退、蠕虫体节不吸
        private void HandleSuction() {
            if (Main.netMode == NetmodeID.MultiplayerClient || Timer >= SuckEnd) {
                return;
            }
            Vector2 aimDir = currentRotation.ToRotationVector2();
            Vector2 jaw = JawPoint;
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npc = Main.npc[i];
                if (!npc.CanBeChasedBy(Projectile)) {
                    continue;
                }
                if (npc.boss || npc.knockBackResist <= 0f || npc.realLife >= 0) {
                    continue;
                }
                Vector2 to = npc.Center - handPos;
                float dist = to.Length();
                if (dist > SuckRange) {
                    continue;
                }
                if (Vector2.Dot(to.SafeNormalize(Vector2.Zero), aimDir) < 0.3f && dist > 90f) {
                    continue;
                }
                Vector2 pull = jaw - npc.Center;
                float pd = pull.Length();
                if (pd > 12f) {
                    float speed = MathF.Min(pd * 0.22f + 4f, 22f);
                    npc.velocity = Vector2.Lerp(npc.velocity, pull.SafeNormalize(Vector2.Zero) * speed, 0.35f);
                }
                else {
                    npc.velocity *= 0.4f;
                }
            }

            //吸流结束瞬间点名：钳口附近的目标被咬住，随刀举砸
            if (Timer == SuckEnd - 1) {
                caught.Clear();
                for (int i = 0; i < Main.maxNPCs && caught.Count < MaxCaught; i++) {
                    NPC npc = Main.npc[i];
                    if (!npc.CanBeChasedBy(Projectile)) {
                        continue;
                    }
                    if (npc.boss || npc.knockBackResist <= 0f || npc.realLife >= 0) {
                        continue;
                    }
                    if (npc.Center.Distance(jaw) <= GripRadius * 1.5f) {
                        caught.Add(npc.whoAmI);
                    }
                }
            }
        }

        //被咬住的目标钉在刀刃上，随举砸移动；砸落瞬间甩飞进地面
        private void HandlePinned() {
            if (Main.netMode == NetmodeID.MultiplayerClient || caught.Count == 0) {
                return;
            }
            if (Timer < SuckEnd || Timer >= SlamEnd) {
                return;
            }
            Vector2 bladeDir = currentRotation.ToRotationVector2();
            int slot = 0;
            foreach (int idx in caught) {
                NPC npc = Main.npc[idx];
                if (!npc.active || npc.dontTakeDamage || npc.friendly) {
                    continue;
                }
                npc.Center = tipPos - bladeDir * (12f + 16f * slot);
                npc.velocity = Vector2.Zero;
                slot++;
            }
        }

        private void HandleImpact() {
            if (impactDone || Timer < SlamEnd) {
                return;
            }
            impactDone = true;
            impactPoint = ProbeGround(tipPos);
            hitstopTimer = 4;

            //甩飞：砸向近前地面，向下带前，落地即止
            if (Main.netMode != NetmodeID.MultiplayerClient) {
                foreach (int idx in caught) {
                    NPC npc = Main.npc[idx];
                    if (!npc.active || npc.dontTakeDamage || npc.friendly) {
                        continue;
                    }
                    npc.velocity = new Vector2(facingDir * Main.rand.NextFloat(4f, 7f), 11f);
                    npc.AddBuff(ModContent.BuffType<AbyssalPressure>(), 300);
                }
            }

            SoundEngine.PlaySound(SoundID.DD2_MonkStaffGroundImpact with { Pitch = -0.15f, Volume = 0.9f }, impactPoint);
            SoundEngine.PlaySound(SoundID.NPCHit4 with { Pitch = -0.5f, Volume = 0.7f }, impactPoint);
            SoundEngine.PlaySound(SoundID.Item62 with { Pitch = -0.25f, Volume = 0.75f }, impactPoint);
            if (CWRClientConfig.Instance.ScreenVibration) {
                Main.instance.CameraModifiers.Add(new PunchCameraModifier(
                    impactPoint, Vector2.UnitY, 9f, 8f, 12, 700f, FullName));
            }

            if (Projectile.IsOwnedByLocalPlayer()) {
                int dmg = (int)(Projectile.damage * 2.6f);
                Projectile.NewProjectile(Owner.GetSource_ItemUse(Item), impactPoint, Vector2.Zero
                    , ModContent.ProjectileType<AbyssrendBurst>()
                    , dmg, Projectile.knockBack * 1.4f, Owner.whoAmI, ai0: 1f);
            }

            if (VaultUtils.isServer) {
                return;
            }
            for (int i = 0; i < 10; i++) {
                Vector2 dir = (-Vector2.UnitY).RotatedByRandom(0.9f);
                PRTLoader.NewParticle<PRT_AbyssGlob>(impactPoint + Main.rand.NextVector2Circular(14f, 6f)
                    , dir * Main.rand.NextFloat(3f, 8f)
                    , Color.Lerp(AbyssrendFX.Deep, AbyssrendFX.Body, Main.rand.NextFloat())
                    , Main.rand.NextFloat(0.45f, 0.8f))
                    .Configure(Main.rand.Next(14, 24));
            }
            for (int i = 0; i < 7; i++) {
                PRTLoader.NewParticle<PRT_AbyssSpark>(impactPoint
                    , (-Vector2.UnitY).RotatedByRandom(1.1f) * Main.rand.NextFloat(2.5f, 7f)
                    , AbyssrendFX.Cyan, Main.rand.NextFloat(0.9f, 1.4f))
                    .Configure(Main.rand.Next(10, 16));
            }
        }

        //从刀尖向下探最多 10 格，找到地表就把落点贴上去
        private static Vector2 ProbeGround(Vector2 from) {
            Vector2 probe = from;
            for (int i = 0; i < 10; i++) {
                Point tile = probe.ToTileCoordinates();
                if (!WorldGen.InWorld(tile.X, tile.Y, 10)) {
                    break;
                }
                if (WorldGen.SolidTile(tile.X, tile.Y)) {
                    probe.Y = tile.Y * 16f - 4f;
                    return probe;
                }
                probe.Y += 16f;
            }
            return from;
        }

        private void HandleAudioVisual() {
            if (Timer == 2) {
                SoundEngine.PlaySound(SoundID.SplashWeak with { Pitch = -0.2f, Volume = 0.55f }, handPos);
                SoundEngine.PlaySound(SoundID.Item85 with { Pitch = -0.3f, Volume = 0.4f }, handPos);
            }
            if (Timer < SuckEnd && Timer % 9 == 0 && Timer > 2) {
                SoundEngine.PlaySound(SoundID.SplashWeak with {
                    Pitch = -0.2f + Timer / (float)SuckEnd * 0.5f,
                    Volume = 0.4f,
                    MaxInstances = 3
                }, JawPoint);
            }
            if (Timer == SuckEnd) {
                SoundEngine.PlaySound(SoundID.NPCHit4 with { Pitch = -0.45f, Volume = 0.55f }, JawPoint);
            }
            if (Timer == RaiseEnd) {
                SoundEngine.PlaySound(SoundID.DD2_MonkStaffSwing with { Pitch = -0.4f, Volume = 0.7f }, handPos);
                SoundEngine.PlaySound(SoundID.Item71 with { Pitch = -0.3f, Volume = 0.5f }, handPos);
            }

            Lighting.AddLight(tipPos, 0.15f, 0.55f, 0.62f);

            if (VaultUtils.isServer || Timer >= SuckEnd || Timer % 2 != 0) {
                return;
            }
            //吸流粒子：从锥区各处涌向钳口
            Vector2 aimDir = currentRotation.ToRotationVector2();
            Vector2 jaw = JawPoint;
            for (int i = 0; i < 2; i++) {
                Vector2 pos = handPos + aimDir.RotatedByRandom(0.55f) * Main.rand.NextFloat(90f, SuckRange);
                Vector2 vel = (jaw - pos).SafeNormalize(Vector2.Zero) * Main.rand.NextFloat(7f, 13f);
                PRTLoader.NewParticle<PRT_AbyssGlob>(pos, vel
                    , Color.Lerp(AbyssrendFX.Deep, AbyssrendFX.Body, Main.rand.NextFloat())
                    , Main.rand.NextFloat(0.3f, 0.5f))
                    .Configure(Main.rand.Next(16, 26));
            }
        }

        private void ApplyOwnerPose() {
            Owner.ChangeDir(facingDir);
            Owner.heldProj = Projectile.whoAmI;
            Owner.itemTime = Owner.itemAnimation = 2;
            Player.CompositeArmStretchAmount stretch = Timer < SuckEnd || Timer >= ImpactEnd
                ? Player.CompositeArmStretchAmount.ThreeQuarters
                : Player.CompositeArmStretchAmount.Full;
            Owner.SetCompositeArmFront(true, stretch, currentRotation - MathHelper.PiOver2);
            //后手扣在杆上，双手抡砸
            Owner.SetCompositeArmBack(true, Player.CompositeArmStretchAmount.ThreeQuarters
                , currentRotation - MathHelper.PiOver2 + (facingDir * 0.28f));
            Projectile.Center = Vector2.Lerp(handPos, tipPos, 0.6f);
            Projectile.rotation = currentRotation;

            //吸流与举砸期间站稳脚跟
            if (Projectile.IsOwnedByLocalPlayer() && Timer < SlamEnd) {
                Owner.velocity.X *= Timer < SuckEnd ? 0.9f : 0.82f;
            }

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

        //伤害窗只开在下砸与落点前两帧
        public override bool? CanDamage() {
            if (Timer <= RaiseEnd || Timer > SlamEnd + 2) {
                return false;
            }
            return true;
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            if (CanDamage() != true) {
                return false;
            }
            //落点附近的冲击圈
            if (impactDone && targetHitbox.Distance(impactPoint) <= 84f) {
                return true;
            }
            //本帧扫过的整段弧，快砸不穿怪
            float reach = BladeLenScaled + holdout + 16f;
            float delta = currentRotation - lastRotation;
            int steps = Math.Clamp((int)MathF.Ceiling(MathF.Abs(delta) * reach / 28f), 1, 64);
            float collisionPoint = 0f;
            for (int i = 0; i <= steps; i++) {
                float rot = MathHelper.Lerp(lastRotation, currentRotation, i / (float)steps);
                Vector2 tip = handPos + rot.ToRotationVector2() * reach;
                if (Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size()
                    , handPos, tip, 52f, ref collisionPoint)) {
                    return true;
                }
            }
            return false;
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {
            modifiers.HitDirectionOverride = facingDir;
            modifiers.SourceDamage *= 1.6f;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(BuffID.Wet, 240);
            target.AddBuff(ModContent.BuffType<AbyssalPressure>(), 240);
            if (VaultUtils.isServer) {
                return;
            }
            for (int i = 0; i < 4; i++) {
                PRTLoader.NewParticle<PRT_AbyssSpark>(target.Center
                    , Main.rand.NextVector2Circular(4f, 4f)
                    , AbyssrendFX.Cyan, Main.rand.NextFloat(0.8f, 1.2f))
                    .Configure(10);
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            //吸流场：钳口处两瓣渐合的压场，举起后淡出
            if (Timer < RaiseEnd) {
                float progress = MathHelper.Clamp(Timer / (float)SuckEnd, 0f, 1f) * 0.85f;
                float fade = Timer < SuckEnd
                    ? 0.9f
                    : 0.9f * (1f - (Timer - SuckEnd) / (float)RaiseDur);
                AbyssrendFX.DrawCanvasTech("TechClamp", JawPoint, AbyssrendFX.QuadPx(96f)
                    , progress, fade);
            }
            return false;
        }

        void IOverlayDrawable.DrawOverlay(SpriteBatch spriteBatch) {
            Texture2D tex = TextureAssets.Projectile[Type]?.Value;
            if (tex == null) {
                return;
            }
            AbyssrendFX.ComputeBladeDrawXform(tex, currentRotation, facingDir, false
                , out Vector2 origin, out float rot, out SpriteEffects flip);
            Vector2 drawPos = handPos - Main.screenPosition;
            Color light = Lighting.GetColor((int)(handPos.X / 16f), (int)(handPos.Y / 16f));

            //举砸阶段的旋转残影
            float angleDelta = MathF.Abs(currentRotation - lastRotation);
            float strength = MathHelper.Clamp((angleDelta - 0.04f) / 0.6f, 0f, 1f);
            if (strength > 0f) {
                int smearCount = Math.Min(5, Math.Max(1, (int)MathF.Ceiling(angleDelta / 0.2f)));
                for (int i = 1; i <= smearCount; i++) {
                    float amount = i / (float)(smearCount + 1);
                    float sAng = MathHelper.Lerp(currentRotation, lastRotation, amount);
                    AbyssrendFX.ComputeBladeDrawXform(tex, sAng, facingDir, false
                        , out Vector2 gOrigin, out float gRot, out SpriteEffects gFlip);
                    Color smear = AbyssrendFX.Cyan * (0.4f * strength * (1f - amount));
                    smear.A = 0;
                    Main.EntitySpriteDraw(tex, drawPos, null, smear, gRot, gOrigin, HeldScale, gFlip, 0);
                }
            }

            Main.EntitySpriteDraw(tex, drawPos, null, light, rot, origin, HeldScale, flip, 0);
            float glowMul = Timer < SuckEnd ? 0.3f : (Timer < SlamEnd ? 0.45f : 0.15f);
            Color glow = AbyssrendFX.Cyan;
            glow.A = 0;
            Main.EntitySpriteDraw(tex, drawPos, null, glow * glowMul, rot, origin, HeldScale, flip, 0);
        }

        private static float EaseOutCubic(float value) {
            value = MathHelper.Clamp(value, 0f, 1f);
            return 1f - MathF.Pow(1f - value, 3f);
        }

        private static float SmoothStep01(float value) {
            value = MathHelper.Clamp(value, 0f, 1f);
            return value * value * (3f - 2f * value);
        }
    }
}
