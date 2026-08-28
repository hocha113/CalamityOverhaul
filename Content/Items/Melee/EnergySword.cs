using CalamityOverhaul.Common;
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
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Melee
{
    internal class EnergySword : ModItem
    {
        public override string Texture => CWRConstant.Item_Melee + "EnergySword";

        /// <summary>三段连击计数，决定下一次挥砍的招式</summary>
        private int comboCounter;
        /// <summary>连击重置计时器，过久未挥砍则回到第一段</summary>
        private int comboResetTimer;

        public override void SetDefaults() {
            Item.height = 44;
            Item.width = 44;
            Item.damage = 12;
            Item.DamageType = DamageClass.Melee;
            Item.useAnimation = Item.useTime = 20;
            Item.scale = 1;
            Item.useTurn = true;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.noMelee = true;
            Item.noUseGraphic = true;
            Item.knockBack = 2.5f;
            Item.UseSound = null;
            Item.autoReuse = true;
            Item.value = Item.buyPrice(0, 0, 75, 0);
            Item.rare = ItemRarityID.Green;
            Item.shoot = ModContent.ProjectileType<EnergySwordHeld>();
            Item.shootSpeed = 12f;
            Item.SetItemUsesCharge(true);
            Item.SetItemMaxCharge(40);
            //noMelee 武器需要手动允许近战词缀
            ItemOverride.ItemMeleePrefixDic[Type] = true;
        }

        public override bool CanUseItem(Player player) {
            float charge = Item.GetItemCharge() - 0.12f;
            if (charge < 0) {
                charge = 0;
            }
            Item.SetItemCharge(charge);
            return player.ownedProjectileCounts[Item.shoot] == 0;
        }

        public override void HoldItem(Player player) {
            if (comboResetTimer > 0 && --comboResetTimer == 0) {
                comboCounter = 0;
            }
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source
            , Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            int combo = comboCounter % 3;
            float swingDir = comboCounter % 2 == 0 ? 1f : -1f;
            comboCounter++;
            comboResetTimer = 75;
            Projectile.NewProjectile(source, player.Center, velocity, type
                , damage, knockback, player.whoAmI, combo, swingDir);
            return false;
        }

        public override void AddRecipes() {
            if (CWRID.DubiousCircuitryAvailable) {
                CreateRecipe().
                AddIngredient(CWRID.Item_DubiousPlating, 5).
                AddIngredient(CWRID.Item_MysteriousCircuitry, 4).
                AddRecipeGroup(CWRCrafted.TinBarGroup, 2).
                AddRecipeGroup(CWRCrafted.GoldBarGroup, 2).
                AddTile(TileID.Anvils).
                Register();
            }
            else {
                CreateRecipe().
                AddRecipeGroup(CWRCrafted.TinBarGroup, 2).
                AddRecipeGroup(CWRCrafted.GoldBarGroup, 2).
                AddTile(TileID.Anvils).
                Register();
            }
        }
    }

    /// <summary>
    /// 能量剑手持弹幕
    /// <br/>三段连击: 正手斩 → 反手斩 → 能量重斩，挥砍中段若充能足够则射出能量光束
    /// <br/>刀光由 EnergySlashTrail.fx 渲染，亮度随充能变化
    /// </summary>
    internal class EnergySwordHeld : BaseHeldProj, IPrimitiveDrawable, IOverlayDrawable
    {
        public override string Texture => CWRConstant.Item_Melee + "EnergySword";
        public override LocalizedText DisplayName => VaultUtils.GetLocalizedItemName<EnergySword>();

        /// <summary>连击索引: 0=正手斩 1=反手斩 2=能量重斩</summary>
        private ref float ComboIndex => ref Projectile.ai[0];
        /// <summary>挥砍方向符号 ±1</summary>
        private ref float SwingDirAi => ref Projectile.ai[1];

        private bool IsFinisher => ComboIndex >= 2f;

        //阶段时长（逻辑帧，受攻速缩放）
        private float WindupTime => IsFinisher ? 6f : 4f;
        private float SlashTime => IsFinisher ? 8f : 6f;
        private float RecoverTime => IsFinisher ? 10f : 9f;
        private float TotalTime => WindupTime + SlashTime + RecoverTime;
        //挥砍弧度
        private float SwingArc => IsFinisher ? 4.4f : 3.1f;
        //刀尖距离持握点的长度
        private float BladeReach => IsFinisher ? 98f : 86f;

        private static readonly Color EnergyRed = new(255, 84, 60);
        private static readonly Color EnergyHot = new(255, 206, 160);

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
        private bool beamFired;
        private float trailFade;
        private readonly HashSet<int> hitNPCs = [];

        //刀光按外缘弧长补点
        private const int TrailMax = 64;
        private const float TrailSampleSpacing = 10f;
        private readonly float[] trailRot = new float[TrailMax];
        private int trailCount;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 46;
            Projectile.friendly = true;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
            Projectile.ownerHitCheck = true;
            Projectile.timeLeft = 60;
            Projectile.CWR().NotSubjectToSpecialEffects = true;
            Projectile.CWR().PierceResist = true;
        }

        public override bool ShouldUpdatePosition() => false;

        public override bool? CanDamage() => sweepDamageActive;

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            if (CanDamage() != true) {
                return false;
            }
            Vector2 hand = Owner.GetPlayerStabilityCenter();
            float reach = BladeReach * Projectile.scale;
            if (CWRUtils.ArcSweepCulled(targetHitbox, hand, reach, 30f)) {
                return false;
            }
            int steps = GetAngularSteps(sweepCollisionEnd - sweepCollisionStart, reach, 28f, 64);
            for (int i = 0; i <= steps; i++) {
                float rotation = MathHelper.Lerp(sweepCollisionStart, sweepCollisionEnd, i / (float)steps);
                Vector2 tip = hand + rotation.ToRotationVector2() * reach;
                float collisionPoint = 0f;
                if (Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size()
                    , hand, tip, 30f, ref collisionPoint)) {
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

            lockedDirection = Math.Sign(Projectile.velocity.X);
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
                Projectile.damage = (int)(Projectile.damage * 1.25f);
                Projectile.scale = 1.1f;
            }
        }

        public override void AI() {
            slashVisualActive = false;
            sweepDamageActive = false;
            sweepCollisionStart = sweepCollisionEnd = currentRotation;
            if (Item.type != ModContent.ItemType<EnergySword>()) {
                Projectile.Kill();
                return;
            }
            if (elapsed >= TotalTime) {
                Projectile.Kill();
                return;
            }

            lastRotation = currentRotation;
            float frameEnd = MathF.Min(elapsed + speedMul, TotalTime);
            float slashEnd = WindupTime + SlashTime;
            float slashFromTime = MathF.Max(elapsed, WindupTime);
            float slashToTime = MathF.Min(frameEnd, slashEnd);

            if (slashToTime > slashFromTime) {
                //消费本帧与挥砍阶段的交集，避免高攻速跨阶段时漏刀。
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

                PushTrailInterval(fromT, toT);

                if (!slashSoundPlayed && toT >= SwingGatherEnd) {
                    slashSoundPlayed = true;
                    if (!VaultUtils.isServer) {
                        SoundEngine.PlaySound(SoundID.Item1 with {
                            Pitch = 0.15f + ComboIndex * 0.12f
                        }, Owner.Center);
                        if (IsFinisher && HasCharge) {
                            SoundEngine.PlaySound(SoundID.Item15 with { Volume = 0.5f, Pitch = 0.4f }, Owner.Center);
                        }
                    }
                }

                if (!beamFired && progress >= 0.70f) {
                    beamFired = true;
                    FireBeam();
                }

                //刀刃能量粒子
                if (!VaultUtils.isServer && Main.rand.NextBool(2)) {
                    Vector2 along = Owner.GetPlayerStabilityCenter()
                        + slashRotation.ToRotationVector2() * Main.rand.NextFloat(BladeReach * 0.5f, BladeReach);
                    Vector2 tangent = slashRotation.ToRotationVector2().RotatedBy(swingSign * MathHelper.PiOver2);
                    PRTLoader.NewParticle<PRT_Spark>(along, tangent * Main.rand.NextFloat(2f, 5f)
                        , Color.Lerp(EnergyRed, EnergyHot, Main.rand.NextFloat()), Main.rand.NextFloat(0.5f, 0.9f)).Configure(false, 8);
                }
            }

            if (frameEnd <= WindupTime) {
                //蓄力回拉
                float t = frameEnd / WindupTime;
                currentRotation = MathHelper.Lerp(startAngle, ChamberAngle, EaseOutCubic(t));
                trailFade = 0f;
            }
            else if (frameEnd <= slashEnd) {
                //缓推后爆发，末端轻过冲回坐
                float t = (frameEnd - WindupTime) / SlashTime;
                currentRotation = GetSwingRotation(GetSwingProgress(t));
                trailFade = 1f;
            }
            else {
                //收势
                float t = (frameEnd - slashEnd) / RecoverTime;
                float returnT = SmoothStep01((t - RecoverHold) / (1f - RecoverHold));
                float baseAngle = (startAngle + endAngle) * 0.5f;
                float guardAngle = baseAngle + swingSign * GuardAngle;
                currentRotation = MathHelper.Lerp(endAngle, guardAngle, returnT);
                trailFade = 1f - SmoothStep01(t);
                TrimTrailToRotation(currentRotation);
            }

            UpdatePlayerPose();
            Lighting.AddLight(Owner.GetPlayerStabilityCenter() + currentRotation.ToRotationVector2() * BladeReach * 0.7f
                , EnergyRed.ToVector3() * (0.25f + ChargeRatio * 0.4f));
            elapsed = frameEnd;
        }

        private float PullbackAngle => IsFinisher ? 0.36f : 0.24f;

        private float ChamberAngle => startAngle - swingSign * PullbackAngle;

        private float SwingGatherEnd => IsFinisher ? 0.23f : 0.16f;

        private float SwingBurstEnd => IsFinisher ? 0.56f : 0.48f;

        private float SwingCreep => IsFinisher ? 0.09f : 0.05f;

        private float SwingOvershoot => IsFinisher ? 0.10f : 0.07f;

        private float GuardAngle => IsFinisher ? 0.92f : 0.72f;

        private float RecoverHold => IsFinisher ? 0.12f : 0.08f;

        private float GetSwingProgress(float t) {
            float path = SwingArc + PullbackAngle;
            float overshoot = 1f + SwingOvershoot / path;
            if (t < SwingGatherEnd) {
                return SwingCreep * SmoothStep01(t / SwingGatherEnd);
            }
            if (t < SwingBurstEnd) {
                float burstT = (t - SwingGatherEnd) / (SwingBurstEnd - SwingGatherEnd);
                return MathHelper.Lerp(SwingCreep, overshoot, SmoothStep01(burstT));
            }
            return MathHelper.Lerp(overshoot, 1f
                , SmoothStep01((t - SwingBurstEnd) / (1f - SwingBurstEnd)));
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
            float delta = toRotation - fromRotation;
            if (delta * swingSign <= 0.0001f) {
                TrimTrailToRotation(toRotation);
                return;
            }

            float outerRadius = (BladeReach + 8f) * Projectile.scale;
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

        /// <summary>充能比例0~1，光效强度</summary>
        private float ChargeRatio => MathHelper.Clamp(Item.GetItemCharge() / 40f, 0f, 1f);

        private bool HasCharge => Item.GetItemCharge() >= 0.2f;

        private void FireBeam() {
            if (!Projectile.IsOwnedByLocalPlayer() || !HasCharge) {
                return;
            }

            Vector2 spawnPos = Owner.GetPlayerStabilityCenter() + UnitToMouseV * BladeReach * 0.6f;
            int proj = Projectile.NewProjectile(Owner.GetSource_ItemUse(Item), spawnPos
                , UnitToMouseV * Item.shootSpeed, ProjectileID.MiniRetinaLaser
                , Projectile.damage, Projectile.knockBack, Owner.whoAmI);
            Main.projectile[proj].DamageType = DamageClass.Melee;
            Main.projectile[proj].penetrate = 6;
            Main.projectile[proj].usesLocalNPCImmunity = true;
            Main.projectile[proj].localNPCHitCooldown = -1;
            Main.projectile[proj].netUpdate = true;

            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item12 with { Volume = 0.45f, Pitch = 0.35f }, spawnPos);
            }
        }

        private void UpdatePlayerPose() {
            Owner.heldProj = Projectile.whoAmI;
            Owner.direction = lockedDirection;
            Owner.itemTime = Owner.itemAnimation = 2;
            Owner.itemRotation = currentRotation;
            Owner.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.Full, currentRotation - MathHelper.PiOver2);
            Projectile.Center = Owner.GetPlayerStabilityCenter() + currentRotation.ToRotationVector2() * BladeReach * 0.5f;
            Projectile.timeLeft = 60;
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {
            modifiers.HitDirectionOverride = currentRotation.ToRotationVector2().X > 0 ? 1 : -1;
            if (target.IsWormBody()) {
                modifiers.FinalDamage *= 0.425f;
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            //转发物品命中钩子，维持装备与饰品的近战联动
            if (hitNPCs.Add(target.whoAmI)) {
                ItemLoader.OnHitNPC(Item, Owner, target, hit, damageDone);
                NPCLoader.OnHitByItem(target, Owner, Item, hit, damageDone);
                PlayerLoader.OnHitNPC(Owner, target, hit, damageDone);
            }

            if (!VaultUtils.isServer) {
                for (int i = 0; i < (IsFinisher ? 6 : 3); i++) {
                    PRTLoader.NewParticle<PRT_Spark>(target.Center, Main.rand.NextVector2Circular(5f, 5f)
                        , Color.Lerp(EnergyRed, EnergyHot, Main.rand.NextFloat()), Main.rand.NextFloat(0.7f, 1.2f)).Configure(false, 10);
                }
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

            float angleDelta = MathF.Abs(currentRotation - lastRotation);
            float strength = MathHelper.Clamp((angleDelta - 0.04f) / 0.75f, 0f, 1f);
            int smearCount = Math.Min(5, Math.Max(1, (int)MathF.Ceiling(angleDelta / 0.22f)));
            for (int i = 1; i <= smearCount && strength > 0f; i++) {
                float amount = i / (float)(smearCount + 1);
                float rot = MathHelper.Lerp(currentRotation, lastRotation, amount);
                Vector2 pos = hand + rot.ToRotationVector2() * dist - Main.screenPosition;
                Color trailColor = EnergyRed
                    * (0.42f * strength * (1f - amount) * (0.4f + ChargeRatio * 0.6f));
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
            spriteBatch.Draw(tex, drawPos, null, lightColor, currentRotation + rotOffset, origin
                , Projectile.scale, effect, 0);

            Color glow = EnergyRed * (0.25f + ChargeRatio * 0.45f);
            glow.A = 0;
            spriteBatch.Draw(tex, drawPos, null, glow, currentRotation + rotOffset, origin
                , Projectile.scale * 1.06f, effect, 0);
        }

        void IPrimitiveDrawable.DrawPrimitives() {
            if (trailCount < 3 || trailFade <= 0.02f) {
                return;
            }
            Effect effect = EffectLoader.EnergySlashTrail?.Value;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            if (effect == null || noise == null) {
                return;
            }

            var bars = new VertexPositionColorTexture[trailCount * 2];
            Vector2 center = Owner.GetPlayerStabilityCenter();
            float outer = (BladeReach + 8f) * Projectile.scale;
            float inner = BladeReach * 0.35f * Projectile.scale;
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
            effect.Parameters["uHeat"]?.SetValue(MathHelper.Clamp(ChargeRatio * 0.85f + (IsFinisher ? 0.3f : 0f), 0f, 1f));
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
