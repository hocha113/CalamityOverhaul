using CalamityOverhaul.Common;
using CalamityOverhaul.Content.PRTTypes;
using CalamityOverhaul.Content.Rarities;
using InnoVault.GameContent.BaseEntity;
using InnoVault.GameSystem;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Linq;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.Graphics.CameraModifiers;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Melee
{
    /// 龙藻巨刃，三段+绿藻剑气，右键翠龙之魂
    internal class DragonsScaleGreatsword : ModItem
    {
        public override string Texture => CWRConstant.Item_Melee + "DragonsScaleGreatsword";

        /// 三段连击计数
        private static int comboCounter;

        public override void SetStaticDefaults() => ItemID.Sets.ItemsThatAllowRepeatedRightClick[Type] = true;

        public override void SetDefaults() {
            Item.height = 54;
            Item.width = 54;
            Item.damage = 556;
            Item.DamageType = DamageClass.Melee;
            Item.useAnimation = Item.useTime = 17;
            Item.useTurn = false;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.noMelee = true;
            Item.noUseGraphic = true;
            Item.knockBack = 6.5f;
            Item.UseSound = null;
            Item.autoReuse = true;
            Item.value = Item.buyPrice(0, 4, 75, 0);
            Item.rare = ModContent.RarityType<GiltRarity>();
            Item.shoot = ModContent.ProjectileType<DragonsScaleGreatswordHeld>();
            Item.shootSpeed = 7f;
            //noMelee 武器需要手动允许近战词缀
            ItemOverride.ItemMeleePrefixDic[Type] = true;
        }

        public override bool AltFunctionUse(Player player) => player.CWR().CustomCooldownCounter <= 0;

        public override bool CanUseItem(Player player) {
            if (player.altFunctionUse == 2) {
                return player.ownedProjectileCounts[ModContent.ProjectileType<DragonSoulSerpent>()] == 0;
            }
            return player.ownedProjectileCounts[ModContent.ProjectileType<DragonsScaleGreatswordHeld>()] == 0;
        }

        public override void ModifyWeaponCrit(Player player, ref float crit) => crit += 3;

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source
            , Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            if (player.altFunctionUse == 2) {
                Projectile.NewProjectile(source, player.Center, velocity.UnitVector() * 9f
                    , ModContent.ProjectileType<DragonSoulSerpent>(), (int)(damage * 1.8f), knockback, player.whoAmI);
                player.CWR().CustomCooldownCounter = 240;
                comboCounter = 0;//召龙重置连击
                return false;
            }

            int combo = comboCounter % 3;
            float swingDir = comboCounter % 2 == 0 ? 1f : -1f;
            comboCounter++;
            Projectile.NewProjectile(source, player.Center, velocity, type
                , damage, knockback, player.whoAmI, combo, swingDir);
            return false;
        }

        public override void AddRecipes() {
            if (CWRID.Item_PerennialBar > 0 && CWRID.Item_UelibloomBar > 0) {
                CreateRecipe().
                AddIngredient(CWRID.Item_PerennialBar, 15).
                AddIngredient(CWRID.Item_UelibloomBar, 15).
                AddIngredient(ItemID.ChlorophyteBar, 15).
                AddTile(TileID.LunarCraftingStation).
                Register();
            }
        }
    }

    /// 龙藻巨刃手持，DragonSlashTrail+绿藻剑气
    internal class DragonsScaleGreatswordHeld : BaseHeldProj, IPrimitiveDrawable, IOverlayDrawable
    {
        public override string Texture => CWRConstant.Item_Melee + "DragonsScaleGreatsword";
        public override LocalizedText DisplayName => ItemLoader.GetItem(ModContent.ItemType<DragonsScaleGreatsword>()).DisplayName;

        /// 连击索引 0正 1反 2终结
        private ref float ComboIndex => ref Projectile.ai[0];
        /// 挥砍方向 ±1
        private ref float SwingDirAi => ref Projectile.ai[1];

        private bool IsFinisher => ComboIndex >= 2f;

        //阶段时长（逻辑帧，受攻速缩放）
        private float WindupTime => IsFinisher ? 10f : 7f;
        private float SlashTime => IsFinisher ? 11f : 8f;
        private float RecoverTime => IsFinisher ? 16f : 12f;
        private float TotalTime => WindupTime + SlashTime + RecoverTime;
        //挥砍弧度
        private float SwingArc => IsFinisher ? 5.6f : 3.4f;
        //刀尖距离持握点的长度
        private float BladeReach => IsFinisher ? 210f : 185f;

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
        private bool beamsFired;
        private float trailFade;

        //刀光按外缘弧长补点
        private const int TrailMax = 104;
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
            if (CWRUtils.ArcSweepCulled(targetHitbox, hand, reach, 54f)) {
                return false;
            }
            int steps = GetAngularSteps(sweepCollisionEnd - sweepCollisionStart, reach, 28f, 64);
            for (int i = 0; i <= steps; i++) {
                float rotation = MathHelper.Lerp(sweepCollisionStart, sweepCollisionEnd, i / (float)steps);
                Vector2 tip = hand + rotation.ToRotationVector2() * reach;
                float collisionPoint = 0f;
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

            if (IsFinisher) {
                Projectile.damage = (int)(Projectile.damage * 1.45f);
                Projectile.scale = 1.15f;
            }
        }

        public override void AI() {
            slashVisualActive = false;
            sweepDamageActive = false;
            sweepCollisionStart = sweepCollisionEnd = currentRotation;
            if (Item.type != ModContent.ItemType<DragonsScaleGreatsword>()) {
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
                float slashRotation = GetSwingRotation(GetSwingProgress(toT));

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
                        SoundEngine.PlaySound(SoundID.Item1 with { Pitch = -0.5f }, Owner.Center);
                        SoundEngine.PlaySound(SoundID.Item60 with { Volume = 0.5f, Pitch = -0.2f }, Owner.Center);
                        if (IsFinisher) {
                            SoundEngine.PlaySound(SoundID.Item71 with { Pitch = -0.2f, Volume = 0.9f }, Owner.Center);
                        }
                    }
                }

                PushTrailInterval(fromT, toT);

                if (!beamsFired && GetSwingProgress(toT) >= 0.76f) {
                    beamsFired = true;
                    FireBeams();
                }

                //刀刃藻光余烬
                if (!VaultUtils.isServer) {
                    Vector2 along = Owner.GetPlayerStabilityCenter()
                        + slashRotation.ToRotationVector2() * Main.rand.NextFloat(BladeReach * 0.5f, BladeReach);
                    Vector2 tangent = slashRotation.ToRotationVector2().RotatedBy(swingSign * MathHelper.PiOver2);
                    int dust = Dust.NewDust(along, 0, 0, DustID.JungleSpore, tangent.X * 3f, tangent.Y * 3f);
                    Main.dust[dust].noGravity = true;
                    Main.dust[dust].scale = Main.rand.NextFloat(0.8f, 1.8f);
                    if (Main.rand.NextBool(2)) {
                        PRTLoader.NewParticle<PRT_Spark>(along, tangent * Main.rand.NextFloat(2f, 5f)
                            , DragonSoulSerpent.kelpColor2, Main.rand.NextFloat(0.6f, 1f)).Configure(false, 9);
                    }
                }
            }

            if (frameEnd <= WindupTime) {
                //蓄力回拉
                float t = frameEnd / WindupTime;
                currentRotation = MathHelper.Lerp(startAngle, ChamberAngle, EaseOutCubic(t));
                trailFade = 0f;
            }
            else if (frameEnd <= slashEnd) {
                //缓推后爆发，末端过冲回坐
                float t = (frameEnd - WindupTime) / SlashTime;
                currentRotation = GetSwingRotation(GetSwingProgress(t));
                trailFade = 1f;
            }
            else {
                //收势
                float t = (frameEnd - slashEnd) / RecoverTime;
                float hold = IsFinisher ? 0.24f : 0.18f;
                float returnT = SmoothStep01((t - hold) / (1f - hold));
                float baseAngle = (startAngle + endAngle) * 0.5f;
                float guardAngle = baseAngle + swingSign * (IsFinisher ? 1.12f : 0.88f);
                currentRotation = MathHelper.Lerp(endAngle, guardAngle, returnT);
                trailFade = 1f - SmoothStep01(t);
                TrimTrailToRotation(currentRotation);
            }

            UpdatePlayerPose();
            Lighting.AddLight(Owner.GetPlayerStabilityCenter() + currentRotation.ToRotationVector2() * BladeReach * 0.7f
                , DragonSoulSerpent.kelpColor1.ToVector3() * 0.8f);
            elapsed = frameEnd;
        }

        private float PullbackAngle => IsFinisher ? 0.72f : 0.50f;

        private float ChamberAngle => startAngle - swingSign * PullbackAngle;

        private float SwingGatherEnd => IsFinisher ? 0.32f : 0.22f;

        private float SwingBurstEnd => IsFinisher ? 0.66f : 0.57f;

        private float GetSwingProgress(float t) {
            float gatherEnd = SwingGatherEnd;
            float creep = IsFinisher ? 0.14f : 0.05f;
            float burstEnd = SwingBurstEnd;
            float path = SwingArc + PullbackAngle;
            float overshoot = 1f + (IsFinisher ? 0.18f : 0.12f) / path;
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
            //终结斩跨过 PI，保留未包裹角度
            float delta = toRotation - fromRotation;
            if (delta * swingSign <= 0.0001f) {
                TrimTrailToRotation(toRotation);
                return;
            }

            float outerRadius = (BladeReach + 16f) * Projectile.scale;
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

        private void FireBeams() {
            if (!Projectile.IsOwnedByLocalPlayer()) {
                return;
            }
            int count = IsFinisher ? 3 : 1;
            int type = ModContent.ProjectileType<DragonsScaleGreatswordBeam>();
            Vector2 spawnPos = Owner.GetPlayerStabilityCenter() + UnitToMouseV * BladeReach * 0.3f;
            for (int i = 0; i < count; i++) {
                float off = count == 1 ? 0f : (i - (count - 1) / 2f) * 0.28f;
                Vector2 velocity = UnitToMouseV.RotatedBy(off) * Main.rand.NextFloat(8f, 9.5f);
                Projectile.NewProjectile(Projectile.GetSource_FromAI(), spawnPos, velocity
                    , type, (int)(Projectile.damage * 0.5f), 0f, Projectile.owner);
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
            target.AddBuff(BuffID.Poisoned, 1200);
            SpawnSporeConverge(target);

            if (!VaultUtils.isServer) {
                for (int i = 0; i < (IsFinisher ? 7 : 4); i++) {
                    PRTLoader.NewParticle<PRT_Spark>(target.Center, Main.rand.NextVector2Circular(5f, 5f)
                        , DragonSoulSerpent.kelpColor2, Main.rand.NextFloat(0.8f, 1.3f)).Configure(false, 12);
                }
            }

            if (IsFinisher && CWRClientConfig.Instance.ScreenVibration) {
                var modifier = new PunchCameraModifier(target.Center
                    , currentRotation.ToRotationVector2(), 4f, 5f, 9, 800f, FullName);
                Main.instance.CameraModifiers.Add(modifier);
            }
        }

        //真近战命中孳生毒藻雾，自四周向目标汇聚
        private void SpawnSporeConverge(NPC target) {
            if (CWRLoad.WormBodys.Contains(target.type) && !Main.rand.NextBool(5)) {
                return;
            }
            int type = ModContent.ProjectileType<SporeCloud>();
            if (Owner.ownedProjectileCounts[type] < 220) {
                for (int i = 0; i < 3; i++) {
                    Vector2 spanPos = target.Center + new Vector2(Main.rand.Next(-723, 724), Main.rand.Next(-553, 0));
                    int proj = Projectile.NewProjectile(Owner.GetSource_FromThis(), spanPos
                        , spanPos.To(target.Center).UnitVector() * Main.rand.Next(9, 13), type, Item.damage / 2, 0, Owner.whoAmI);
                    Main.projectile[proj].timeLeft = 120;
                    Main.projectile[proj].scale = 1.2f + Main.rand.NextFloat(0.3f);
                }
            }
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) => target.AddBuff(BuffID.Poisoned, 600);

        public override bool PreDraw(ref Color lightColor) {
            if (!slashVisualActive) {
                return false;
            }

            Texture2D tex = TextureValue;
            Vector2 origin = tex.Size() / 2f;
            Vector2 hand = Owner.GetPlayerStabilityCenter();
            float dist = BladeReach * 0.5f * Projectile.scale;
            GetBladeDrawOrientation(out SpriteEffects effect, out float rotOffset);

            //高速挥砍残影
            float angleDelta = MathF.Abs(currentRotation - lastRotation);
            float strength = MathHelper.Clamp((angleDelta - 0.04f) / 0.75f, 0f, 1f);
            int smearCount = Math.Min(5, Math.Max(1, (int)MathF.Ceiling(angleDelta / 0.22f)));
            for (int i = 1; i <= smearCount && strength > 0f; i++) {
                float amount = i / (float)(smearCount + 1);
                float rot = MathHelper.Lerp(currentRotation, lastRotation, amount);
                Vector2 pos = hand + rot.ToRotationVector2() * dist - Main.screenPosition;
                Color trailColor = DragonSoulSerpent.kelpColor2 * (0.42f * strength * (1f - amount));
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
            Effect effect = EffectLoader.DragonSlashTrail?.Value;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            if (effect == null || noise == null) {
                return;
            }

            var bars = new VertexPositionColorTexture[trailCount * 2];
            Vector2 center = Owner.GetPlayerStabilityCenter();
            float outer = (BladeReach + 16f) * Projectile.scale;
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
            effect.Parameters["uHeat"]?.SetValue(IsFinisher ? 1f : 0.3f);
            effect.Parameters["uNoiseTex"]?.SetValue(noise);
            foreach (EffectPass pass in effect.CurrentTechnique.Passes) {
                pass.Apply();
                device.DrawUserPrimitives(PrimitiveType.TriangleStrip, bars, 0, bars.Length - 2);
            }

            device.BlendState = origBlend;
            device.RasterizerState = origRaster;
        }
    }

    /// 螺旋绿藻剑气，DragonSporeBeam.fx
    internal class DragonsScaleGreatswordBeam : ModProjectile, IPrimitiveDrawable
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 14;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 1;
        }

        public override void SetDefaults() {
            Projectile.DamageType = DamageClass.Melee;
            Projectile.width = Projectile.height = 24;
            Projectile.penetrate = 1;
            Projectile.hostile = false;
            Projectile.friendly = true;
            Projectile.MaxUpdates = 3;
            Projectile.timeLeft = 120 * Projectile.MaxUpdates;
        }

        public override bool OnTileCollide(Vector2 oldVelocity) {
            for (int i = 0; i < 3; i++) {
                Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center, Main.rand.NextFloat(220 * CWRUtils.atoR, 320 * CWRUtils.atoR).ToRotationVector2() * Main.rand.Next(5, 11)
                    , ModContent.ProjectileType<SporeCloud>(), Projectile.damage / 3, Projectile.knockBack, Projectile.owner);
            }
            Projectile.Explode(32);
            return true;
        }

        public override void AI() {
            Projectile.ai[0]++;
            Projectile.scale += 0.0035f;
            Projectile.rotation = Projectile.velocity.ToRotation();

            int dust = Dust.NewDust(Projectile.position, Projectile.width, Projectile.height
                , DustID.JungleSpore, Projectile.velocity.X * 0.3f, Projectile.velocity.Y * 0.3f);
            Main.dust[dust].noGravity = true;
            if (Main.rand.NextBool(4)) {
                CWRUtils.SpanCycleDust(Projectile, DustID.JungleTorch, DustID.JungleTorch);
            }

            Lighting.AddLight(Projectile.Center, DragonSoulSerpent.kelpColor1.ToVector3() * 0.6f);

            if (Projectile.ai[0] > 20) {
                NPC target = Projectile.Center.FindClosestNPC(360, false, true);
                if (target is not null) {
                    Projectile.SmoothHomingBehavior(target.Center, 1f, 0.03f);
                }
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            for (int i = 0; i < Main.rand.Next(3, 6); i++) {
                Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center, Main.rand.NextVector2Unit() * Main.rand.Next(6, 9)
                    , ModContent.ProjectileType<SporeCloud>(), Projectile.damage / 3, Projectile.knockBack, Projectile.owner);
            }
            Projectile.Explode(42);
            target.AddBuff(BuffID.Poisoned, 1200);
            Projectile.Kill();
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
                Color color = Color.Lerp(DragonSoulSerpent.kelpColor1, DragonSoulSerpent.kelpColor2
                    , 1f / Projectile.oldPos.Length * k) * (0.5f * (1f - 1f / Projectile.oldPos.Length * k));
                color.A = 0;
                float slp = (0.4f + 0.5f * (Projectile.oldPos.Length - k) / Projectile.oldPos.Length) * Projectile.scale;
                Main.EntitySpriteDraw(glow, drawPos, null, color, 0f, glowOrigin, slp, SpriteEffects.None, 0);
            }
            return false;
        }

        void IPrimitiveDrawable.DrawPrimitives() {
            Effect effect = EffectLoader.DragonSporeBeam?.Value;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            if (effect == null || noise == null) {
                return;
            }

            float fadeIn = MathHelper.Clamp(Projectile.ai[0] / 12f, 0f, 1f);
            float fadeOut = MathHelper.Clamp(Projectile.timeLeft / 40f, 0f, 1f);
            float fade = fadeIn * fadeOut;
            if (fade <= 0.02f) {
                return;
            }

            Vector2 fwd = Projectile.rotation.ToRotationVector2();
            Vector2 perp = fwd.RotatedBy(MathHelper.PiOver2);
            Vector2 c = Projectile.Center;
            float halfW = 66f * Projectile.scale;
            float halfH = 26f * Projectile.scale;

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
            effect.Parameters["uSeed"]?.SetValue(Projectile.whoAmI * 0.77f % 10f);
            effect.Parameters["uFade"]?.SetValue(fade);
            effect.Parameters["uNoiseTex"]?.SetValue(noise);
            foreach (EffectPass pass in effect.CurrentTechnique.Passes) {
                pass.Apply();
                device.DrawUserPrimitives(PrimitiveType.TriangleStrip, quad, 0, 2);
            }

            device.BlendState = origBlend;
            device.RasterizerState = origRaster;
        }
    }

    /// 翠龙之魂，DragonSoulSerpent.fx
    internal class DragonSoulSerpent : ModProjectile, IPrimitiveDrawable
    {
        public override string Texture => CWRConstant.VaultPlaceholder;
        public static Color kelpColor1 => new Color(40, 130, 64);
        public static Color kelpColor2 => new Color(150, 235, 130);

        private ref float Time => ref Projectile.ai[0];
        /// 狂怒度 0~1
        private float rage;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 26;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 1;
        }

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 70;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.extraUpdates = 1;
            Projectile.timeLeft = 150 * 2;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 24;
            Projectile.CWR().HitAttribute.WormResistance = 0.4f;
        }

        public override void AI() {
            if (Time == 0) {
                Projectile.rotation = Projectile.velocity.ToRotation();
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Roar with { Volume = 0.8f, Pitch = 0.4f }, Projectile.Center);
                    SoundEngine.PlaySound(SoundID.DD2_WyvernDiveDown with { Volume = 0.7f, Pitch = -0.1f }, Projectile.Center);
                    for (int i = 0; i < 16; i++) {
                        float ang = MathHelper.TwoPi * i / 16f;
                        int dust = Dust.NewDust(Projectile.Center, 0, 0, DustID.JungleSpore
                            , ang.ToRotationVector2().X * 6f, ang.ToRotationVector2().Y * 6f);
                        Main.dust[dust].noGravity = true;
                        Main.dust[dust].scale = Main.rand.NextFloat(1.2f, 2f);
                    }
                }
            }

            //出闸加速，蛟龙腾空
            if (Time < 30) {
                Projectile.velocity *= 1.035f;
                Projectile.scale = MathHelper.Lerp(0.55f, 1f, Time / 30f);
            }
            if (Projectile.velocity.Length() > 15f) {
                Projectile.velocity = Projectile.velocity.UnitVector() * 15f;
            }

            //蜿蜒游动
            Projectile.velocity = Projectile.velocity.RotatedBy(MathF.Sin(Time * 0.22f) * 0.03f);

            //追猎锁定
            NPC target = Projectile.Center.FindClosestNPC(620f, true, chasedByNPC: npc => npc.CanBeChasedBy(Projectile));
            if (Time > 40 && target != null) {
                Projectile.SmoothHomingBehavior(target.Center, 1f, 0.07f);
                rage = MathHelper.Clamp(rage + 0.03f, 0f, 1f);
            }
            else {
                rage = MathHelper.Clamp(rage - 0.02f, 0f, 1f);
            }

            Projectile.rotation = Projectile.velocity.ToRotation();

            //沿途散播毒藻雾
            if (Projectile.IsOwnedByLocalPlayer() && Time % 12 == 0 && Time > 16) {
                int type = ModContent.ProjectileType<SporeCloud>();
                if (Main.player[Projectile.owner].ownedProjectileCounts[type] < 220) {
                    int proj = Projectile.NewProjectile(Projectile.GetSource_FromAI()
                        , Projectile.Center - Projectile.rotation.ToRotationVector2() * 60f
                        , Main.rand.NextVector2Circular(1.5f, 1.5f), type, Projectile.damage / 4, 0, Projectile.owner);
                    Main.projectile[proj].timeLeft = 90;
                }
            }

            //躯体藻尘与毒雾
            if (!VaultUtils.isServer) {
                Vector2 bodyPos = Projectile.Center - Projectile.rotation.ToRotationVector2() * Main.rand.NextFloat(0f, 170f) * Projectile.scale;
                int dust2 = Dust.NewDust(bodyPos, 0, 0, DustID.JungleSpore, 0f, 0f);
                Main.dust[dust2].noGravity = true;
                Main.dust[dust2].velocity = Main.rand.NextVector2Circular(1.5f, 1.5f);
                if (Time % 10 == 0) {
                    PRTLoader.NewParticle<PRT_ToxicMist>(bodyPos, -Projectile.velocity * 0.1f
                        , default, Main.rand.NextFloat(0.5f, 0.9f)).Configure(40, Main.rand.NextFloat(0.4f, 0.9f));
                }
            }

            Lighting.AddLight(Projectile.Center, kelpColor1.ToVector3() * (1.2f + rage * 0.8f));
            Time++;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(BuffID.Poisoned, 1200);

            //撕咬迸发毒藻雾
            if (Projectile.IsOwnedByLocalPlayer()) {
                int type = ModContent.ProjectileType<SporeCloud>();
                if (Main.player[Projectile.owner].ownedProjectileCounts[type] < 220) {
                    for (int i = 0; i < 3; i++) {
                        Projectile.NewProjectile(Projectile.GetSource_FromAI(), target.Center
                            , Main.rand.NextVector2Unit() * Main.rand.Next(5, 9), type, Projectile.damage / 4, 0, Projectile.owner);
                    }
                }
            }

            if (!VaultUtils.isServer) {
                for (int i = 0; i < 6; i++) {
                    PRTLoader.NewParticle<PRT_Spark>(target.Center, Main.rand.NextVector2Circular(6f, 6f)
                        , kelpColor2, Main.rand.NextFloat(0.8f, 1.3f)).Configure(false, 12);
                }
            }

            if (CWRClientConfig.Instance.ScreenVibration) {
                var modifier = new PunchCameraModifier(target.Center
                    , Projectile.rotation.ToRotationVector2(), 3f, 4f, 7, 700f, FullName);
                Main.instance.CameraModifiers.Add(modifier);
            }
        }

        public override void OnKill(int timeLeft) {
            //龙魂散逸成藻雾
            if (VaultUtils.isServer) {
                return;
            }
            for (int i = 0; i < 9; i++) {
                float ang = MathHelper.TwoPi * i / 9f;
                PRTLoader.NewParticle<PRT_ToxicMist>(Projectile.Center + ang.ToRotationVector2() * 24f
                    , ang.ToRotationVector2() * Main.rand.NextFloat(1.5f, 4f), default
                    , Main.rand.NextFloat(0.6f, 1.1f)).Configure(45, Main.rand.NextFloat(0.4f, 0.9f));
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
                Color color = Color.Lerp(kelpColor1, kelpColor2, 1f / Projectile.oldPos.Length * k)
                    * (0.55f * (1f - 1f / Projectile.oldPos.Length * k));
                color.A = 0;
                float slp = (0.6f + 0.6f * (Projectile.oldPos.Length - k) / Projectile.oldPos.Length) * Projectile.scale;
                Main.EntitySpriteDraw(glow, drawPos, null, color, 0f, glowOrigin, slp, SpriteEffects.None, 0);
            }

            //中心柔光晕
            Color coreColor = kelpColor2;
            coreColor.A = 0;
            Main.EntitySpriteDraw(glow, Projectile.Center - Main.screenPosition, null
                , coreColor * 0.8f, 0f, glowOrigin, 1.6f * Projectile.scale, SpriteEffects.None, 0);
            return false;
        }

        void IPrimitiveDrawable.DrawPrimitives() {
            Effect effect = EffectLoader.DragonSoulSerpent?.Value;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            if (effect == null || noise == null) {
                return;
            }

            float fadeIn = MathHelper.Clamp(Time / 20f, 0f, 1f);
            float fadeOut = MathHelper.Clamp(Projectile.timeLeft / 30f, 0f, 1f);
            float fade = fadeIn * fadeOut;
            if (fade <= 0.02f) {
                return;
            }

            Vector2 fwd = Projectile.rotation.ToRotationVector2();
            Vector2 perp = fwd.RotatedBy(MathHelper.PiOver2);
            Vector2 c = Projectile.Center;
            float halfW = 125f * Projectile.scale;
            float halfH = 58f * Projectile.scale;

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

    internal class SporeCloud : ModProjectile
    {
        public override string Texture => CWRConstant.Projectile_Melee + "SporeCloud";
        public override void SetDefaults() {
            Projectile.DamageType = DamageClass.Melee;
            Projectile.width = Projectile.height = 24;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.hostile = false;
            Projectile.friendly = true;
            Projectile.timeLeft = 60;
            Projectile.usesIDStaticNPCImmunity = true;
            Projectile.idStaticNPCHitCooldown = 25;
        }

        public override void AI() {
            Projectile.velocity *= 0.985f;
            Projectile.scale += 0.013f;
            float maxShaking = 20;
            Projectile.rotation += Math.Sign(Projectile.velocity.X) * 0.05f;
            if (Projectile.rotation > MathHelper.ToRadians(maxShaking))
                Projectile.rotation = MathHelper.ToRadians(maxShaking);
            if (Projectile.rotation < MathHelper.ToRadians(-maxShaking))
                Projectile.rotation = MathHelper.ToRadians(-maxShaking);
            VaultUtils.ClockFrame(ref Projectile.frame, 5, 3);
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(BuffID.Poisoned, 1200);
            Projectile.timeLeft -= 15;
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D value = TextureAssets.Projectile[Type].Value;
            Rectangle rectangle = value.GetRectangle(Projectile.frame, 4);
            Main.EntitySpriteDraw(value, Projectile.Center - Main.screenPosition, rectangle, lightColor * (Projectile.timeLeft / 30f)
                , Projectile.rotation, rectangle.Size() / 2, Projectile.scale * 0.8f, 0, 0);
            return false;
        }
    }
}
