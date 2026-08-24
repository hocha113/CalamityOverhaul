using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Buffs;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.GameContent.BaseEntity;
using InnoVault.GameSystem;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.Graphics.CameraModifiers;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Melee.WeaverGrievanceses
{
    /// 怨念编织者，三段+右键冲刺
    internal class WeaverGrievances : ModItem
    {
        public override string Texture => CWRConstant.Item_Melee + "WeaverGrievances";

        /// 三段连击计数
        private static int comboCounter;

        public override void SetStaticDefaults() => ItemID.Sets.ItemsThatAllowRepeatedRightClick[Type] = true;

        public override void SetDefaults() {
            Item.height = 154;
            Item.width = 154;
            Item.damage = 455;
            Item.DamageType = DamageClass.Melee;
            Item.useAnimation = Item.useTime = 26;
            Item.scale = 1;
            Item.useTurn = false;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.noMelee = true;
            Item.noUseGraphic = true;
            Item.knockBack = 7.5f;
            Item.UseSound = null;
            Item.autoReuse = true;
            Item.value = Item.buyPrice(13, 53, 75, 0);
            Item.rare = ItemRarityID.Red;
            Item.crit = 8;
            Item.shoot = ModContent.ProjectileType<WeaverGrievancesHeld>();
            Item.shootSpeed = 18f;
            //noMelee 武器需要手动允许近战词缀
            ItemOverride.ItemMeleePrefixDic[Type] = true;
        }

        internal static void SpwanInOwnerDust(Player player) {
            if (Main.dedServ) {
                return;
            }
            Vector2 handOffset = Main.OffsetsPlayerOnhand[player.bodyFrame.Y / 56] * 2f;
            if (player.direction != 1) {
                handOffset.X = player.bodyFrame.Width - handOffset.X;
            }
            if (player.gravDir != 1f) {
                handOffset.Y = player.bodyFrame.Height - handOffset.Y;
            }

            handOffset -= new Vector2(player.bodyFrame.Width - player.width, player.bodyFrame.Height - player.height) / 2f;
            Vector2 rotatedHandPosition = player.RotatedRelativePoint(player.position + handOffset, true);
            for (int i = 0; i < 4; i++) {
                Dust dust = Dust.NewDustDirect(player.Center, 0, 0, DustID.RedTorch, 0f, 0f, 150, default, 1.3f);
                dust.position = rotatedHandPosition;
                dust.velocity = Vector2.Zero;
                dust.noGravity = true;
                dust.fadeIn = 1f;
                dust.velocity += player.velocity;
                dust.position += Utils.RandomVector2(Main.rand, -4f, 4f);
                dust.scale += Main.rand.NextFloat();
            }
        }

        public override bool AltFunctionUse(Player player) => player.CWR().CustomCooldownCounter <= 0;

        public override bool CanUseItem(Player player) {
            if (player.altFunctionUse == 2) {
                return player.ownedProjectileCounts[ModContent.ProjectileType<WeaverGrievancesDash>()] == 0;
            }
            return player.ownedProjectileCounts[ModContent.ProjectileType<WeaverGrievancesHeld>()] == 0;
        }

        public override void ModifyWeaponCrit(Player player, ref float crit) => crit += 6;

        public override void ModifyTooltips(List<TooltipLine> tooltips) => CWRUtils.SetItemLegendContentTops(ref tooltips, Name);

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source
            , Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            if (player.altFunctionUse == 2) {
                float _swingDir = velocity.X > 0 ? 2.2f : -2.2f;
                Projectile.NewProjectile(source, position, velocity
                    , ModContent.ProjectileType<WeaverGrievancesDash>(), damage, knockback, player.whoAmI, ai1: _swingDir);
                comboCounter = 0;//冲刺重置连击
                return false;
            }

            int combo = comboCounter % 3;
            float swingDir = comboCounter % 2 == 0 ? 1f : -1f;
            comboCounter++;
            Projectile.NewProjectile(source, player.Center, velocity, type
                , damage, knockback, player.whoAmI, combo, swingDir);
            return false;
        }

    }

    /// 怨念编织者手持，WeaverSlashTrail+怨灵之爪
    internal class WeaverGrievancesHeld : BaseHeldProj, IPrimitiveDrawable, IOverlayDrawable
    {
        public override string Texture => CWRConstant.Item_Melee + "WeaverGrievances";
        public override LocalizedText DisplayName => ItemLoader.GetItem(ModContent.ItemType<WeaverGrievances>()).DisplayName;

        /// 连击索引 0正 1反 2终结
        private ref float ComboIndex => ref Projectile.ai[0];
        /// 挥砍方向 ±1
        private ref float SwingDirAi => ref Projectile.ai[1];

        private bool IsFinisher => ComboIndex >= 2f;

        //阶段时长（逻辑帧，受攻速缩放）
        private float WindupTime => IsFinisher ? 8f : 5f;
        private float SlashTime => IsFinisher ? 10f : 8f;
        private float RecoverTime => IsFinisher ? 16f : 13f;
        private float TotalTime => WindupTime + SlashTime + RecoverTime;
        //挥砍弧度
        private float SwingArc => IsFinisher ? 5.4f : 3.3f;
        //刀尖距离持握点的长度
        private float BladeReach => IsFinisher ? 215f : 195f;

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
        private bool wraithsFired;
        private float trailFade;

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
            int steps = GetAngularSteps(sweepCollisionEnd - sweepCollisionStart, reach, 28f, 64);
            for (int i = 0; i <= steps; i++) {
                float rotation = MathHelper.Lerp(sweepCollisionStart, sweepCollisionEnd, i / (float)steps);
                Vector2 tip = hand + rotation.ToRotationVector2() * reach;
                float collisionPoint = 0f;
                if (Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size()
                    , hand, tip, 52f, ref collisionPoint)) {
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

            if (IsFinisher) {
                Projectile.damage = (int)(Projectile.damage * 1.4f);
                Projectile.scale = 1.12f;
                if (!VaultUtils.isServer) {
                    //终结斩蓄力时的怨灵低鸣
                    SoundEngine.PlaySound(SoundID.NPCDeath39 with { Volume = 0.35f, Pitch = 0.25f, MaxInstances = 3 }, Owner.Center);
                }
            }
        }

        public override void AI() {
            slashVisualActive = false;
            sweepDamageActive = false;
            sweepCollisionStart = sweepCollisionEnd = currentRotation;
            if (Item.type != ModContent.ItemType<WeaverGrievances>()) {
                Projectile.Kill();
                return;
            }
            if (elapsed >= TotalTime) {
                Projectile.Kill();
                return;
            }

            lastRotation = currentRotation;
            float slashEnd = WindupTime + SlashTime;

            if (elapsed < WindupTime) {
                //蓄力回拉
                float t = MathHelper.Clamp((elapsed + speedMul) / WindupTime, 0f, 1f);
                currentRotation = MathHelper.Lerp(startAngle, ChamberAngle, EaseOutCubic(t));
                trailFade = 0f;
            }
            else if (elapsed < slashEnd) {
                //缓推后爆发，末端轻过冲回坐
                slashVisualActive = true;
                float previousT = MathHelper.Clamp((elapsed - WindupTime) / SlashTime, 0f, 1f);
                float t = MathHelper.Clamp((elapsed - WindupTime + speedMul) / SlashTime, 0f, 1f);
                float progress = GetSwingProgress(t);
                currentRotation = GetSwingRotation(progress);
                trailFade = 1f;

                float damageFrom = MathF.Max(previousT, SwingGatherEnd);
                float damageTo = MathF.Min(t, SwingBurstEnd);
                if (damageTo > damageFrom) {
                    sweepDamageActive = true;
                    sweepCollisionStart = GetSwingRotation(GetSwingProgress(damageFrom));
                    sweepCollisionEnd = GetSwingRotation(GetSwingProgress(damageTo));
                }

                if (!slashSoundPlayed && t >= SwingGatherEnd) {
                    slashSoundPlayed = true;
                    if (!VaultUtils.isServer) {
                        float pitch = IsFinisher ? -0.45f : -0.18f;
                        SoundEngine.PlaySound(SoundID.Item1 with { Pitch = pitch }, Owner.Center);
                        if (IsFinisher) {
                            SoundEngine.PlaySound(SoundID.Item71 with { Pitch = -0.35f, Volume = 0.9f }, Owner.Center);
                        }
                    }
                }

                PushTrailSamples();

                if (!wraithsFired && progress >= 0.72f) {
                    wraithsFired = true;
                    FireWraiths();
                }

                //刀刃灵魂余烬
                if (!VaultUtils.isServer && Main.rand.NextBool(2)) {
                    Vector2 along = Owner.GetPlayerStabilityCenter()
                        + currentRotation.ToRotationVector2() * Main.rand.NextFloat(BladeReach * 0.55f, BladeReach);
                    Vector2 tangent = currentRotation.ToRotationVector2().RotatedBy(swingSign * MathHelper.PiOver2);
                    PRTLoader.NewParticle<PRT_Spark>(along, tangent * Main.rand.NextFloat(2f, 5f)
                        , WeaverBeam.sloudColor2, Main.rand.NextFloat(0.6f, 1f)).Configure(false, 9);
                }
            }
            else {
                //收势
                float t = MathHelper.Clamp((elapsed - slashEnd + speedMul) / RecoverTime, 0f, 1f);
                float hold = IsFinisher ? 0.20f : 0.14f;
                float returnT = SmoothStep01((t - hold) / (1f - hold));
                float baseAngle = (startAngle + endAngle) * 0.5f;
                float guardAngle = baseAngle + swingSign * (IsFinisher ? 1.05f : 0.82f);
                currentRotation = MathHelper.Lerp(endAngle, guardAngle, returnT);
                trailFade = 1f - SmoothStep01(t);
                TrimTrailToCurrentRotation();
            }

            UpdatePlayerPose();
            Lighting.AddLight(Owner.GetPlayerStabilityCenter() + currentRotation.ToRotationVector2() * BladeReach * 0.7f
                , WeaverBeam.sloudColor1.ToVector3() * 0.8f);
            elapsed += speedMul;
        }

        private float PullbackAngle => IsFinisher ? 0.55f : 0.38f;

        private float ChamberAngle => startAngle - swingSign * PullbackAngle;

        private float SwingGatherEnd => IsFinisher ? 0.28f : 0.20f;

        private float SwingBurstEnd => IsFinisher ? 0.62f : 0.55f;

        private float GetSwingProgress(float t) {
            float gatherEnd = SwingGatherEnd;
            float creep = IsFinisher ? 0.12f : 0.04f;
            float burstEnd = SwingBurstEnd;
            float path = SwingArc + PullbackAngle;
            float overshoot = 1f + (IsFinisher ? 0.14f : 0.10f) / path;
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

        private void PushTrailSamples() {
            //终结斩跨过 PI，保留未包裹角度
            float delta = currentRotation - lastRotation;
            if (delta * swingSign <= 0.0001f) {
                TrimTrailToCurrentRotation();
                return;
            }

            float outerRadius = (BladeReach + 14f) * Projectile.scale;
            bool appendStart = trailCount == 0;
            int steps = GetAngularSteps(delta, outerRadius, TrailSampleSpacing, TrailMax - 1);
            int retained = Math.Min(trailCount, TrailMax - steps);
            if (retained > 0) {
                Array.Copy(trailRot, 0, trailRot, steps, retained);
            }
            for (int i = 0; i < steps; i++) {
                float amount = 1f - i / (float)steps;
                trailRot[i] = MathHelper.Lerp(lastRotation, currentRotation, amount);
            }
            trailCount = steps + retained;
            if (appendStart && trailCount < TrailMax) {
                trailRot[trailCount++] = lastRotation;
            }
        }

        private void TrimTrailToCurrentRotation() {
            if (trailCount == 0) {
                return;
            }

            const float angleEpsilon = 0.0001f;
            int firstRetained = 0;
            while (firstRetained < trailCount
                && (trailRot[firstRetained] - currentRotation) * swingSign > angleEpsilon) {
                firstRetained++;
            }

            int retained = trailCount - firstRetained;
            bool headAlreadySampled = retained > 0
                && MathF.Abs(trailRot[firstRetained] - currentRotation) <= angleEpsilon;
            int targetOffset = headAlreadySampled ? 0 : 1;
            int copied = Math.Min(retained, TrailMax - targetOffset);
            if (copied > 0 && (firstRetained != targetOffset || firstRetained > 0)) {
                Array.Copy(trailRot, firstRetained, trailRot, targetOffset, copied);
            }

            trailRot[0] = currentRotation;
            trailCount = copied + targetOffset;
        }

        private void FireWraiths() {
            if (!Projectile.IsOwnedByLocalPlayer()) {
                return;
            }
            int count = IsFinisher ? 6 : 3;
            float spread = IsFinisher ? 0.5f : 0.32f;
            Vector2 spawnPos = Owner.GetPlayerStabilityCenter() + currentRotation.ToRotationVector2() * BladeReach * 0.55f;
            for (int i = 0; i < count; i++) {
                Vector2 velocity = UnitToMouseV.RotatedByRandom(spread)
                    * Main.rand.NextFloat(16.6f, 28f);
                Projectile.NewProjectile(Projectile.GetSource_FromAI(), spawnPos, velocity
                    , ModContent.ProjectileType<WeaverBeam>(), (int)(Projectile.damage * 0.45f)
                    , Projectile.knockBack / 2, Projectile.owner, 0, 0, i);
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

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(ModContent.BuffType<SoulBurning>(), 300);

            if (!VaultUtils.isServer) {
                for (int i = 0; i < (IsFinisher ? 7 : 4); i++) {
                    PRTLoader.NewParticle<PRT_Spark>(target.Center, Main.rand.NextVector2Circular(5f, 5f)
                        , WeaverBeam.sloudColor2, Main.rand.NextFloat(0.8f, 1.3f)).Configure(false, 12);
                }
            }

            if (IsFinisher && CWRClientConfig.Instance.ScreenVibration) {
                var modifier = new PunchCameraModifier(target.Center
                    , currentRotation.ToRotationVector2(), 4f, 5f, 9, 800f, FullName);
                Main.instance.CameraModifiers.Add(modifier);
            }
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) => target.AddBuff(ModContent.BuffType<SoulBurning>(), 300);

        public override bool PreDraw(ref Color lightColor) {
            if (!slashVisualActive) {
                return false;
            }

            Texture2D tex = TextureValue;
            Vector2 origin = tex.Size() / 2f;
            Vector2 hand = Owner.GetPlayerStabilityCenter();
            float dist = BladeReach * 0.5f * Projectile.scale;
            GetBladeDrawOrientation(out SpriteEffects effect, out float rotOffset);

            //挥砍残影
            float angleDelta = MathF.Abs(currentRotation - lastRotation);
            float strength = MathHelper.Clamp((angleDelta - 0.04f) / 0.75f, 0f, 1f);
            int smearCount = Math.Min(5, Math.Max(1, (int)MathF.Ceiling(angleDelta / 0.22f)));
            for (int i = 1; i <= smearCount && strength > 0f; i++) {
                float amount = i / (float)(smearCount + 1);
                float rot = MathHelper.Lerp(currentRotation, lastRotation, amount);
                Vector2 pos = hand + rot.ToRotationVector2() * dist - Main.screenPosition;
                Color trailColor = WeaverBeam.sloudColor2 * (0.42f * strength * (1f - amount));
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

            Color lightColor = Lighting.GetColor((int)(hand.X / 16f), (int)(hand.Y / 16f));
            Vector2 drawPos = hand + currentRotation.ToRotationVector2() * dist - Main.screenPosition;
            Main.EntitySpriteDraw(tex, drawPos, null, lightColor, currentRotation + rotOffset, origin
                , Projectile.scale, effect, 0);
        }

        void IPrimitiveDrawable.DrawPrimitives() {
            if (trailCount < 3 || trailFade <= 0.02f) {
                return;
            }
            Effect effect = EffectLoader.WeaverSlashTrail?.Value;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            if (effect == null || noise == null) {
                return;
            }

            var bars = new VertexPositionColorTexture[trailCount * 2];
            Vector2 center = Owner.GetPlayerStabilityCenter();
            float outer = (BladeReach + 14f) * Projectile.scale;
            float inner = BladeReach * 0.30f;
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

            effect.Parameters["transformMatrix"]?.SetValue(VaultUtils.GetTransfromMatrix());
            effect.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            effect.Parameters["uFade"]?.SetValue(trailFade);
            effect.Parameters["uHeat"]?.SetValue(IsFinisher ? 1f : 0.25f);
            effect.Parameters["uNoiseTex"]?.SetValue(noise);
            foreach (EffectPass pass in effect.CurrentTechnique.Passes) {
                pass.Apply();
                device.DrawUserPrimitives(PrimitiveType.TriangleStrip, bars, 0, bars.Length - 2);
            }

            device.BlendState = origBlend;
            device.RasterizerState = origRaster;
        }
    }

    /// 怨念编织者冲刺，WeaverSoulVortex.fx
    internal class WeaverGrievancesDash : BaseHeldProj, IPrimitiveDrawable
    {
        public override string Texture => CWRConstant.Item_Melee + "WeaverGrievances";
        private ref float Time => ref Projectile.ai[0];
        private ref float SwingDir => ref Projectile.ai[1];

        private int DirSignSpin => Math.Sign(SwingDir) == 0 ? 1 : Math.Sign(SwingDir);

        //风车刀尖旋转半径
        private const float WheelRadius = 165f;
        private float spinAngle;
        private float spinSpeed;
        private float vortexFade;

        //风车环形刀光缓存
        private const int TrailMax = 45;
        private const int TrailSubdiv = 3;
        private readonly float[] trailRot = new float[TrailMax];
        private int trailCount;

        public override void SetDefaults() {
            Projectile.ignoreWater = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.height = 140;
            Projectile.width = 140;
            Projectile.friendly = true;
            Projectile.scale = 1f;
            Projectile.penetrate = 1;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 20;
        }

        public override bool? CanDamage() => null;

        public override bool ShouldUpdatePosition() => false;

        public override void AI() {
            CWRPlayer modPlayer = Owner.CWR();
            modPlayer.IsRotatingDuringDash = true;

            //初始化冲刺效果
            if (Time == 0) {
                modPlayer.PendingDashVelocity = Projectile.velocity.UnitVector() * 23;
                spinAngle = Projectile.velocity.ToRotation();

                if (Projectile.IsOwnedByLocalPlayer()) {
                    Projectile.NewProjectile(Projectile.GetSource_FromThis(), Owner.Bottom
                        , Vector2.Zero, ModContent.ProjectileType<WeaverExplode>(), 100, 2, Projectile.owner);
                }

                //生成烟雾和魂焰粒子
                for (int k = 0; k < 7; k++) {
                    Vector2 randomVelocity = Owner.velocity.RotatedByRandom(MathHelper.ToRadians(7)) * (1f - Main.rand.NextFloat(0.3f));
                    Dust.NewDust(Owner.Bottom, 0, 0, DustID.Smoke, randomVelocity.X * 0.5f, randomVelocity.Y * 0.5f);
                    Dust.NewDust(Owner.Bottom, 0, 0, DustID.InfernoFork, randomVelocity.X * 0.5f, randomVelocity.Y * 0.5f);
                }

                CreateEllipseDust(Projectile.velocity, Projectile.Center, 13, 1.2f, 0.8f);

                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.DD2_WyvernDiveDown with { Volume = 0.9f, Pitch = -0.25f }, Projectile.position);
                    SoundEngine.PlaySound(SoundID.NPCDeath39 with { Volume = 0.5f, Pitch = -0.5f, MaxInstances = 3 }, Projectile.position);
                }
            }

            //风车旋转随速
            spinSpeed = (0.38f + MathHelper.Clamp(Owner.velocity.Length() / 200f, 0f, 0.1f)) * DirSignSpin;
            float lastSpin = spinAngle;
            spinAngle += spinSpeed;
            PushTrailSamples(lastSpin);

            //怨魂涡流淡入
            vortexFade = MathHelper.Clamp(Time / 6f, 0f, 1f);

            //周期性怨魂尾迹
            if (!VaultUtils.isServer) {
                if (Time % 3 == 0) {
                    PRTLoader.NewParticle<PRT_SoulFire>(Projectile.Center + VaultUtils.RandVr(40f)
                        , -Owner.velocity * 0.15f, default, Main.rand.NextFloat(0.4f, 0.8f));
                }
                Dust d = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(50, 50)
                    , DustID.RedTorch, Vector2.Zero, Scale: 1f);
                d.noGravity = true;
            }

            if (Time < 20) {
                Owner.GivePlayerImmuneState(6);
            }

            modPlayer.RotationDirection = (int)SwingDir;

            Projectile.Center = Owner.GetPlayerStabilityCenter();
            Projectile.rotation = Owner.fullRotation;

            if (Time < 10) {
                Projectile.scale += 0.04f;
            }
            else if (Projectile.scale > 1f) {
                Projectile.scale -= 0.02f;
            }

            Owner.heldProj = Projectile.whoAmI;
            Owner.ChangeDir(Projectile.velocity.X < 0 ? -1 : 1);
            Owner.itemRotation = Projectile.rotation * Owner.direction;
            Owner.itemTime = 2;
            Owner.itemAnimation = 2;

            Lighting.AddLight(Projectile.position, WeaverBeam.sloudColor1.ToVector3() * 1.2f);

            if (Time > 8 && Owner.velocity.Length() < 5 || DownLeft) {
                Projectile.Kill();
            }

            Time++;
        }

        private void PushTrailSamples(float lastSpin) {
            for (int s = TrailSubdiv - 1; s >= 0; s--) {
                float rot = MathHelper.Lerp(spinAngle, lastSpin, s / (float)TrailSubdiv);
                for (int i = Math.Min(trailCount, TrailMax - 1); i > 0; i--) {
                    trailRot[i] = trailRot[i - 1];
                }
                trailRot[0] = rot;
                if (trailCount < TrailMax) {
                    trailCount++;
                }
            }
        }

        private void CreateEllipseDust(Vector2 velocity, Vector2 center, float scale, float ellipseFactorX, float ellipseFactorY) {
            Vector2 velocityDirection = velocity.SafeNormalize(Vector2.Zero);
            float angle = (float)Math.Atan2(velocityDirection.Y, velocityDirection.X);

            for (int i = 0; i <= 360; i += 3) {
                float radian = MathHelper.ToRadians(i);
                Vector2 dustOffset = new Vector2(
                    MathF.Cos(radian) * ellipseFactorX,
                    MathF.Sin(radian) * ellipseFactorY
                ) * scale;
                dustOffset = dustOffset.RotatedBy(angle - MathHelper.PiOver2);

                int dustIndex = Dust.NewDust(center, 0, 0, DustID.FireworkFountain_Red, dustOffset.X, dustOffset.Y, 0, Color.White, 2f);
                Main.dust[dustIndex].noGravity = true;
                Main.dust[dustIndex].position = center + dustOffset;
                Main.dust[dustIndex].velocity = dustOffset;
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            Projectile.Explode();
            target.AddBuff(ModContent.BuffType<SoulBurning>(), 300);
            if (Time < 4) {
                Owner.GivePlayerImmuneState(16);
                CombatText.NewText(target.Hitbox, Color.Gold, "Perfect Dodge!!!", true);
            }
        }

        public override void OnKill(int timeLeft) {
            CWRPlayer modPlayer = Owner.CWR();
            modPlayer.IsRotatingDuringDash = false;
            modPlayer.RotationResetCounter = 15;
            modPlayer.DashCooldownCounter = 95;
            modPlayer.CustomCooldownCounter = 90;
            if (Main.zenithWorld) {
                modPlayer.CustomCooldownCounter = 2;
            }

            //怨魂散逸，掩盖涡流消失的突变
            if (!VaultUtils.isServer) {
                for (int i = 0; i < 10; i++) {
                    float ang = MathHelper.TwoPi * i / 10f;
                    PRTLoader.NewParticle<PRT_SoulFire>(Projectile.Center + ang.ToRotationVector2() * 30f
                        , ang.ToRotationVector2() * Main.rand.NextFloat(2f, 5f), default, Main.rand.NextFloat(0.4f, 0.9f));
                }
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            //怨魂风车
            Texture2D tex = TextureValue;
            Vector2 origin = tex.Size() / 2f;
            Vector2 center = Projectile.Center - Main.screenPosition;
            float dist = WheelRadius * 0.5f * Projectile.scale;

            for (int k = 5; k >= 1; k--) {
                float ang = spinAngle - k * spinSpeed * 1.6f;
                Vector2 pos = center + ang.ToRotationVector2() * dist;
                Color color = WeaverBeam.sloudColor2 * (0.4f * (1f - k / 6f));
                color.A = 0;
                Main.EntitySpriteDraw(tex, pos, null, color, ang + MathHelper.PiOver4, origin
                    , Projectile.scale * 0.92f, SpriteEffects.None, 0);
            }

            Vector2 bladePos = center + spinAngle.ToRotationVector2() * dist;
            Main.EntitySpriteDraw(tex, bladePos, null, lightColor, spinAngle + MathHelper.PiOver4, origin
                , Projectile.scale * 0.92f, SpriteEffects.None, 0);
            return false;
        }

        void IPrimitiveDrawable.DrawPrimitives() {
            if (vortexFade <= 0.02f) {
                return;
            }
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            if (noise == null) {
                return;
            }

            GraphicsDevice device = Main.graphics.GraphicsDevice;
            BlendState origBlend = device.BlendState;
            RasterizerState origRaster = device.RasterizerState;
            device.BlendState = BlendState.AlphaBlend;
            device.RasterizerState = RasterizerState.CullNone;

            //怨魂涡流
            Effect vortex = EffectLoader.WeaverSoulVortex?.Value;
            if (vortex != null) {
                float half = 230f * Projectile.scale;
                Vector2 c = Projectile.Center;
                var quad = new VertexPositionColorTexture[4];
                quad[0] = new VertexPositionColorTexture((c + new Vector2(-half, -half)).ToVector3(), Color.White, new Vector2(0, 0));
                quad[1] = new VertexPositionColorTexture((c + new Vector2(half, -half)).ToVector3(), Color.White, new Vector2(1, 0));
                quad[2] = new VertexPositionColorTexture((c + new Vector2(-half, half)).ToVector3(), Color.White, new Vector2(0, 1));
                quad[3] = new VertexPositionColorTexture((c + new Vector2(half, half)).ToVector3(), Color.White, new Vector2(1, 1));

                vortex.Parameters["transformMatrix"]?.SetValue(VaultUtils.GetTransfromMatrix());
                vortex.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
                vortex.Parameters["uFade"]?.SetValue(vortexFade);
                vortex.Parameters["uSpinDir"]?.SetValue((float)DirSignSpin);
                vortex.Parameters["uNoiseTex"]?.SetValue(noise);
                foreach (EffectPass pass in vortex.CurrentTechnique.Passes) {
                    pass.Apply();
                    device.DrawUserPrimitives(PrimitiveType.TriangleStrip, quad, 0, 2);
                }
            }

            //风车环形刀光
            Effect slash = EffectLoader.WeaverSlashTrail?.Value;
            if (slash != null && trailCount >= 3) {
                var bars = new VertexPositionColorTexture[trailCount * 2];
                Vector2 center = Projectile.Center;
                float outer = (WheelRadius + 16f) * Projectile.scale;
                float inner = WheelRadius * 0.42f * Projectile.scale;
                for (int i = 0; i < trailCount; i++) {
                    float factor = 1f - i / (float)trailCount;
                    Vector2 dir = trailRot[i].ToRotationVector2();
                    bars[i * 2] = new VertexPositionColorTexture((center + dir * outer).ToVector3()
                        , Color.White, new Vector2(factor, 0f));
                    bars[i * 2 + 1] = new VertexPositionColorTexture((center + dir * inner).ToVector3()
                        , Color.White, new Vector2(factor, 1f));
                }

                slash.Parameters["transformMatrix"]?.SetValue(VaultUtils.GetTransfromMatrix());
                slash.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
                slash.Parameters["uFade"]?.SetValue(vortexFade * 0.85f);
                slash.Parameters["uHeat"]?.SetValue(0.6f);
                slash.Parameters["uNoiseTex"]?.SetValue(noise);
                foreach (EffectPass pass in slash.CurrentTechnique.Passes) {
                    pass.Apply();
                    device.DrawUserPrimitives(PrimitiveType.TriangleStrip, bars, 0, bars.Length - 2);
                }
            }

            device.BlendState = origBlend;
            device.RasterizerState = origRaster;
        }
    }

    internal class WeaverExplode : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;
        public override void SetDefaults() {
            Projectile.width = 82;
            Projectile.height = 82;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 60;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
        }

        public override void AI() {
            Vector2 pos = Projectile.Center + VaultUtils.RandVr(Projectile.width / 2);
            if (Main.player[Projectile.owner].ZoneDungeon) {
                PRTLoader.NewParticle<PRT_SoulFire>(pos, new Vector2(0, -Main.rand.Next(2, 4)), default, Main.rand.NextFloat(0.3f, 1));
            }
            else {
                PRTLoader.NewParticle<PRT_HellFire>(pos, new Vector2(0, -Main.rand.Next(2, 4)), default, Main.rand.NextFloat(0.3f, 1));
            }
        }
    }

    /// 怨灵之爪，WeaverWraith.fx
    internal class WeaverBeam : ModProjectile, IPrimitiveDrawable
    {
        public override string Texture => CWRConstant.VaultPlaceholder;
        public static Color sloudColor1 => new Color(100, 43, 69);
        public static Color sloudColor2 => new Color(200, 111, 145);

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 32;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 1;
        }

        public override void SetDefaults() {
            Projectile.width = 62;
            Projectile.height = 62;
            Projectile.timeLeft = 300;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.ignoreWater = true;
            Projectile.tileCollide = false;
            Projectile.extraUpdates = 4;
            Projectile.penetrate = -1;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
            Projectile.CWR().HitAttribute.WormResistance = 0.4f;
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {
            if (target.type == CWRID.NPC_DevourerofGodsHead || CWRLoad.ExoMechAresSegments.Contains(target.type)) {
                modifiers.FinalDamage /= 2;
            }
        }

        public override void AI() {
            if (Projectile.ai[0] == 0 && Projectile.ai[1] >= 0) {
                SoundEngine.PlaySound(SoundID.DD2_BetsyFireballShot with { Volume = 0.5f }, Projectile.position);
                Projectile.rotation = Projectile.velocity.ToRotation();
                Projectile.ai[0] = 1;
            }

            if (Projectile.ai[1] < 160) {
                if (Projectile.ai[1] >= 60) {
                    Projectile.scale -= 0.004f;
                }
                else {
                    if (Main.zenithWorld) {
                        CartePRTEffect();
                    }
                    Projectile.scale += 0.016f;
                }

                if (Projectile.alpha <= 155) {
                    Projectile.alpha += 2;
                }

                Projectile.velocity *= 0.98f;
                if (Projectile.velocity.Length() > 16) {
                    Projectile.velocity *= 0.98f;
                }

                if (Main.zenithWorld) {//在天顶世界中追踪敌人
                    NPC target = Projectile.Center.FindClosestNPC(300f, true, chasedByNPC: npc => npc.CanBeChasedBy(Projectile));
                    if (target != null) {
                        Projectile.SmoothHomingBehavior(target.Center, 1.02f, 0.12f);
                    }
                }
            }
            else {
                Projectile.SmoothHomingBehavior(Main.player[Projectile.owner].Center, 1, 0.01f);
            }

            //怨魂面向飞行方向
            Projectile.rotation = Projectile.velocity.ToRotation();

            if (Projectile.ai[1] == 162) {
                if (Main.player[Projectile.owner].ZoneDungeon || Main.player[Projectile.owner].ZoneUnderworldHeight) {
                    Projectile.NewProjectile(Projectile.FromObjectGetParent(), Projectile.Center, Vector2.Zero
                    , ModContent.ProjectileType<WeaverExplode>(), Projectile.damage, 0, Projectile.owner);
                }
            }

            if (Projectile.ai[1] == 160) {
                if (Projectile.ai[2] == 0) {
                    SoundStyle sound = SoundID.NPCDeath39;
                    sound.MaxInstances = 6;
                    sound.Pitch = -0.6f;
                    sound.Volume = 0.6f;
                    SoundEngine.PlaySound(sound, Projectile.Center);
                }
                Projectile.velocity = Projectile.Center.To(Main.player[Projectile.owner].Center).RotatedByRandom(0.6f).UnitVector() * 16;
                Projectile.damage /= 2;//再度减少伤害
                Vector2 velocityDirection = Projectile.velocity.SafeNormalize(Vector2.Zero);
                float angle = (float)Math.Atan2(velocityDirection.Y, velocityDirection.X);
                float ellipseFactorX = 1.2f;  //X轴的缩放，控制椭圆的宽度
                float ellipseFactorY = 0.8f;  //Y轴的缩放，控制椭圆的高度
                for (int i = 0; i <= 360; i += 3) {
                    //计算粒子在椭圆轨迹上的位置
                    float radian = MathHelper.ToRadians(i);
                    Vector2 vr = new Vector2(MathF.Cos(radian) * ellipseFactorX, MathF.Sin(radian) * ellipseFactorY) * 3;
                    vr = vr.RotatedBy(angle - MathHelper.PiOver2);
                    //新建粒子，并设置其位置、速度
                    int num = Dust.NewDust(Projectile.Center, Projectile.width, Projectile.height, DustID.RedTorch, vr.X, vr.Y, 0, Color.White, 2f);
                    Main.dust[num].noGravity = true;  //不受重力影响
                    Main.dust[num].position = Projectile.Center + vr;  //设置粒子在轨迹上的位置
                    Main.dust[num].velocity = vr;  //设置粒子的速度方向
                }
            }

            Lighting.AddLight(Projectile.Center, sloudColor1.ToVector3() * 1.75f * Main.essScale);
            Projectile.ai[1]++;
        }

        private void CartePRTEffect() {
            int particleCount = 6; //粒子数量
            float arcAngle = MathHelper.Pi; //圆弧的角度范围，MathHelper.Pi 表示半圆
            Vector2 baseDirection = Projectile.velocity.SafeNormalize(Vector2.UnitX); //基准方向

            for (int i = 0; i < particleCount; i++) {
                //根据粒子索引计算角度
                float angleOffset = -arcAngle / 2 + arcAngle * (i / (float)(particleCount - 1));
                Vector2 direction = baseDirection.RotatedBy(angleOffset); //基准方向旋转到新角度
                Vector2 spawnPos = Projectile.Center;
                Vector2 ver = -direction * Main.rand.NextFloat(0.6f, 0.9f);
                float slp = Main.rand.NextFloat(1f, 1.2f);
                PRTLoader.NewParticle<PRT_Spark>(spawnPos, ver, sloudColor1, slp).Configure(false, 8);
            }
        }

        public override void OnKill(int timeLeft) {
            if (Main.dedServ) {
                return;
            }
            for (int i = 0; i < 6; i++) {
                PRTLoader.NewParticle<PRT_Spark>(Projectile.Center, Main.rand.NextVector2Circular(4f, 4f)
                    , sloudColor2, Main.rand.NextFloat(0.7f, 1.1f)).Configure(false, 10);
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            //柔光残影拖尾
            Texture2D glow = CWRAsset.SoftGlow.Value;
            Vector2 glowOrigin = glow.Size() / 2f;
            for (int k = 0; k < Projectile.oldPos.Length; k++) {
                if (Projectile.oldPos[k] == Vector2.Zero) {
                    continue;
                }
                Vector2 drawPos = Projectile.oldPos[k] - Main.screenPosition + Projectile.Size / 2;
                Color color = Projectile.GetAlpha(Color.Lerp(sloudColor1, sloudColor2, 1f / Projectile.oldPos.Length * k)
                    * (1f - 1f / Projectile.oldPos.Length * k));
                color.A = 0;
                float slp = (0.5f + 0.5f * (Projectile.oldPos.Length - k) / Projectile.oldPos.Length) * Projectile.scale;
                Main.EntitySpriteDraw(glow, drawPos, null, color, 0f, glowOrigin, slp, SpriteEffects.None, 0);
            }

            //中心柔光晕
            Color coreColor = sloudColor2;
            coreColor.A = 0;
            Main.EntitySpriteDraw(glow, Projectile.Center - Main.screenPosition, null
                , coreColor * 0.85f, 0f, glowOrigin, 1.4f * Projectile.scale, SpriteEffects.None, 0);
            return false;
        }

        void IPrimitiveDrawable.DrawPrimitives() {
            Effect effect = EffectLoader.WeaverWraith?.Value;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            if (effect == null || noise == null) {
                return;
            }

            float fadeIn = MathHelper.Clamp(Projectile.ai[1] / 18f, 0f, 1f);
            float dim = 1f - Projectile.alpha / 255f * 0.4f;
            float fade = fadeIn * dim;
            if (fade <= 0.02f) {
                return;
            }

            //回归阶段陷入狂怒
            float rage = MathHelper.Clamp((Projectile.ai[1] - 140f) / 20f, 0f, 1f);

            Vector2 fwd = Projectile.rotation.ToRotationVector2();
            Vector2 perp = fwd.RotatedBy(MathHelper.PiOver2);
            Vector2 c = Projectile.Center;
            float halfW = 52f * Projectile.scale;
            float halfH = 38f * Projectile.scale;

            var quad = new VertexPositionColorTexture[4];
            quad[0] = new VertexPositionColorTexture((c - fwd * halfW - perp * halfH).ToVector3(), Color.White, new Vector2(0, 0));
            quad[1] = new VertexPositionColorTexture((c + fwd * halfW - perp * halfH).ToVector3(), Color.White, new Vector2(1, 0));
            quad[2] = new VertexPositionColorTexture((c - fwd * halfW + perp * halfH).ToVector3(), Color.White, new Vector2(0, 1));
            quad[3] = new VertexPositionColorTexture((c + fwd * halfW + perp * halfH).ToVector3(), Color.White, new Vector2(1, 1));

            GraphicsDevice device = Main.graphics.GraphicsDevice;
            BlendState origBlend = device.BlendState;
            RasterizerState origRaster = device.RasterizerState;
            device.BlendState = BlendState.AlphaBlend;
            device.RasterizerState = RasterizerState.CullNone;

            effect.Parameters["transformMatrix"]?.SetValue(VaultUtils.GetTransfromMatrix());
            effect.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            effect.Parameters["uSeed"]?.SetValue(Projectile.whoAmI * 0.61f % 10f);
            effect.Parameters["uFade"]?.SetValue(fade);
            effect.Parameters["uRage"]?.SetValue(rage);
            effect.Parameters["uNoiseTex"]?.SetValue(noise);
            foreach (EffectPass pass in effect.CurrentTechnique.Passes) {
                pass.Apply();
                device.DrawUserPrimitives(PrimitiveType.TriangleStrip, quad, 0, 2);
            }

            device.BlendState = origBlend;
            device.RasterizerState = origRaster;
        }
    }
}
