using CalamityOverhaul.Common;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.GameContent.BaseEntity;
using InnoVault.GameSystem;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.Graphics.CameraModifiers;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Melee
{
    /// 毁灭者之刃，三段连击，DestroyerSlash.fx
    internal class DestroyersBlade : ModItem, ICWRLoader
    {
        public override string Texture => CWRConstant.Item_Melee + "DestroyersBlade";
        [VaultLoaden(CWRConstant.Item_Melee + "DestroyersBladeGlow")]
        public static Asset<Texture2D> Glow = null;

        /// 三段连击计数
        private static int comboCounter;

        public override void SetDefaults() {
            Item.width = Item.height = 120;
            Item.damage = 190;
            Item.knockBack = 6;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.UseSound = null;
            Item.useTime = Item.useAnimation = 22;
            Item.DamageType = DamageClass.Melee;
            Item.noMelee = true;
            Item.noUseGraphic = true;
            Item.autoReuse = true;
            Item.rare = ItemRarityID.Purple;
            Item.value = Item.buyPrice(0, 1, 60, 5);
            Item.shoot = ModContent.ProjectileType<DestroyersBladeHeld>();
            Item.shootSpeed = 15;
            Item.CWR().BrutalWorldItem = true;
            //noMelee 武器需要手动允许近战词缀
            ItemOverride.ItemMeleePrefixDic[Type] = true;
        }

        public override bool CanUseItem(Player player) => player.ownedProjectileCounts[Item.shoot] == 0;

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source
            , Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            int combo = comboCounter % 3;
            float swingDir = comboCounter % 2 == 0 ? 1f : -1f;
            comboCounter++;
            Projectile.NewProjectile(source, player.Center, velocity, type
                , damage, knockback, player.whoAmI, combo, swingDir);
            return false;
        }

        public override void PostDrawInWorld(SpriteBatch spriteBatch, Color lightColor
            , Color alphaColor, float rotation, float scale, int whoAmI) {
            spriteBatch.Draw(Glow.Value, Item.Center - Main.screenPosition, null, Color.White
                , rotation, Glow.Value.Size() / 2, scale, SpriteEffects.None, 0);
        }
    }

    /// 毁灭者手持基类，三段+DestroyerSlash+Beam
    /// (EX 版已独立重做，见 DestroyersBladeEXs 文件夹)
    internal abstract class DestroyersBladeHeldBase : BaseHeldProj, IPrimitiveDrawable, IOverlayDrawable
    {
        /// 对应物品ID
        protected abstract int TargetItemID { get; }
        /// 刀身辉光贴图
        protected abstract Texture2D GlowTex { get; }
        /// EX形态(更快更大)
        protected virtual bool IsEX => false;

        /// 连击索引 0正 1反 2终结
        private ref float ComboIndex => ref Projectile.ai[0];
        /// 挥砍方向 ±1
        private ref float SwingDirAi => ref Projectile.ai[1];

        protected bool IsFinisher => ComboIndex >= 2f;

        //阶段时长(逻辑帧，攻速缩放)
        private float WindupTime => (IsFinisher ? 8f : 5f) - (IsEX ? 1f : 0f);
        private float SlashTime => (IsFinisher ? 9f : 7f) - (IsEX ? 1f : 0f);
        private float RecoverTime => (IsFinisher ? 15f : 12f) - (IsEX ? 2f : 0f);
        private float TotalTime => WindupTime + SlashTime + RecoverTime;
        //挥砍弧度
        private float SwingArc => IsFinisher ? 5.5f : 3.4f;
        //刀尖距离持握点的长度
        private float BladeReach => (IsEX ? 168f : 150f) * (IsFinisher ? 1.08f : 1f);
        //光束伤害系数
        private float BeamDamageMul => 1f;
        //光束数量
        private int BeamCount => IsFinisher ? (IsEX ? 5 : 3) : 1;

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
            if (CWRUtils.ArcSweepCulled(targetHitbox, hand, reach, 54f)) {
                return false;
            }
            int steps = GetAngularSteps(sweepCollisionEnd - sweepCollisionStart, reach, 24f, 64);
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
                Projectile.damage = (int)(Projectile.damage * 1.35f);
                Projectile.scale = IsEX ? 1.2f : 1.12f;
                if (!VaultUtils.isServer) {
                    //终结斩起手的机械蓄能声
                    SoundEngine.PlaySound(SoundID.Item22 with { Volume = 0.6f, Pitch = -0.4f, MaxInstances = 3 }, Owner.Center);
                }
            }
            else if (IsEX) {
                Projectile.scale = 1.06f;
            }
        }

        public override void AI() {
            slashVisualActive = false;
            sweepDamageActive = false;
            sweepCollisionStart = sweepCollisionEnd = currentRotation;
            if (Item.type != TargetItemID) {
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

                if (!slashSoundPlayed && toT >= SwingGatherEnd) {
                    slashSoundPlayed = true;
                    if (!VaultUtils.isServer) {
                        SoundEngine.PlaySound(SoundID.Item1 with { Pitch = -0.5f }, Owner.Center);
                        if (IsFinisher) {
                            SoundEngine.PlaySound(SoundID.Item71 with { Pitch = -0.3f, Volume = 0.95f }, Owner.Center);
                        }
                    }
                }

                PushTrailInterval(fromT, toT);

                if (!beamsFired && progress >= 0.70f) {
                    beamsFired = true;
                    FireBeams(slashRotation);
                }

                //刀刃熔渣火花
                if (!VaultUtils.isServer && Main.rand.NextBool(2)) {
                    Vector2 along = Owner.GetPlayerStabilityCenter()
                        + slashRotation.ToRotationVector2() * Main.rand.NextFloat(BladeReach * 0.5f, BladeReach);
                    Vector2 tangent = slashRotation.ToRotationVector2().RotatedBy(swingSign * MathHelper.PiOver2);
                    PRTLoader.NewParticle<PRT_Spark>(along, tangent * Main.rand.NextFloat(3f, 6f)
                        , Color.Lerp(Color.Red, Color.OrangeRed, Main.rand.NextFloat())
                        , Main.rand.NextFloat(0.6f, 1f)).Configure(false, 9);
                }
            }

            if (frameEnd <= WindupTime) {
                //蓄力回拉
                float t = frameEnd / WindupTime;
                currentRotation = MathHelper.Lerp(startAngle, ChamberAngle, EaseOutCubic(t));
                trailFade = 0f;
            }
            else if (frameEnd <= slashEnd) {
                //液压缓推后瞬间泄压，末端过冲回坐
                float t = (frameEnd - WindupTime) / SlashTime;
                currentRotation = GetSwingRotation(GetSwingProgress(t));
                trailFade = 1f;
            }
            else {
                //收势
                float t = (frameEnd - slashEnd) / RecoverTime;
                float hold = (IsFinisher ? 0.22f : 0.18f) - (IsEX ? 0.03f : 0f);
                float returnT = SmoothStep01((t - hold) / (1f - hold));
                float baseAngle = (startAngle + endAngle) * 0.5f;
                float guardAngle = baseAngle + swingSign * (IsFinisher ? 1.08f : 0.86f);
                currentRotation = MathHelper.Lerp(endAngle, guardAngle, returnT);
                trailFade = 1f - SmoothStep01(t);
                TrimTrailToRotation(currentRotation);
            }

            UpdatePlayerPose();
            Lighting.AddLight(Owner.GetPlayerStabilityCenter() + currentRotation.ToRotationVector2() * BladeReach * 0.7f
                , new Vector3(1f, 0.2f, 0.1f) * 0.7f);
            elapsed = frameEnd;
        }

        private float PullbackAngle => IsFinisher ? 0.62f : 0.45f;

        private float ChamberAngle => startAngle - swingSign * PullbackAngle;

        private float SwingGatherEnd => (IsFinisher ? 0.32f : 0.25f) - (IsEX ? 0.03f : 0f);

        private float SwingBurstEnd => IsFinisher ? 0.60f : 0.52f;

        private float GetSwingProgress(float t) {
            float gatherEnd = SwingGatherEnd;
            float creep = IsFinisher ? 0.12f : 0.05f;
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

            float outerRadius = (BladeReach + 12f) * Projectile.scale;
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

        private void FireBeams(float slashRotation) {
            if (!Projectile.IsOwnedByLocalPlayer()) {
                return;
            }
            int count = BeamCount;
            float spread = count > 1 ? 0.46f : 0f;
            Vector2 spawnPos = Owner.GetPlayerStabilityCenter() + slashRotation.ToRotationVector2() * BladeReach * 0.5f;
            for (int i = 0; i < count; i++) {
                float offset = count > 1 ? MathHelper.Lerp(-spread, spread, i / (float)(count - 1)) : 0f;
                Vector2 velocity = UnitToMouseV * Item.shootSpeed;
                Projectile.NewProjectile(Projectile.GetSource_FromAI(), spawnPos, velocity
                    , ModContent.ProjectileType<DestroyersBeam>(), (int)(Projectile.damage * BeamDamageMul)
                    , Projectile.knockBack / 2, Projectile.owner, ai1: IsEX ? 1f : 0f);
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
            if (!VaultUtils.isServer) {
                //金属撞击的火花飞溅
                SoundEngine.PlaySound(SoundID.NPCHit4 with { Volume = 0.5f, Pitch = -0.2f, MaxInstances = 3 }, target.Center);
                for (int i = 0; i < (IsFinisher ? 8 : 4); i++) {
                    PRTLoader.NewParticle<PRT_SparkAlpha>(target.Center, Main.rand.NextVector2Circular(6f, 6f)
                        , Main.rand.NextBool() ? Color.Red : Color.OrangeRed
                        , Main.rand.NextFloat(1f, 2f)).Configure(false, Main.rand.Next(10, 18));
                }
                if (IsFinisher) {
                    Color warm = new Color(255, 90, 40);
                    PRTLoader.NewParticle<PRT_MechExplosion>(target.Center, Main.rand.NextVector2Circular(1.5f, 1.5f)
                        , warm, IsEX ? 0.9f : 0.6f).Configure(Main.rand.Next(18, 28), warm);
                }
            }

            if (IsFinisher && CWRClientConfig.Instance.ScreenVibration) {
                var modifier = new PunchCameraModifier(target.Center
                    , currentRotation.ToRotationVector2(), IsEX ? 5f : 4f, 5f, 9, 800f, FullName);
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

            //挥砍残影
            float angleDelta = MathF.Abs(currentRotation - lastRotation);
            float strength = MathHelper.Clamp((angleDelta - 0.04f) / 0.72f, 0f, 1f);
            int smearCount = Math.Min(5, Math.Max(1, (int)MathF.Ceiling(angleDelta / 0.20f)));
            for (int i = 1; i <= smearCount && strength > 0f; i++) {
                float amount = i / (float)(smearCount + 1);
                float rot = MathHelper.Lerp(currentRotation, lastRotation, amount);
                Vector2 pos = hand + rot.ToRotationVector2() * dist - Main.screenPosition;
                Color trailColor = new Color(255, 60, 30) * (0.40f * strength * (1f - amount));
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
            Main.EntitySpriteDraw(GlowTex, drawPos, null, Color.White, currentRotation + rotOffset, GlowTex.Size() / 2f
                , Projectile.scale, effect, 0);
        }

        void IPrimitiveDrawable.DrawPrimitives() {
            if (trailCount < 3 || trailFade <= 0.02f) {
                return;
            }
            Effect effect = EffectLoader.DestroyerSlash?.Value;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            if (effect == null || noise == null) {
                return;
            }

            var bars = new VertexPositionColorTexture[trailCount * 2];
            Vector2 center = Owner.GetPlayerStabilityCenter();
            float outer = (BladeReach + 12f) * Projectile.scale;
            float inner = BladeReach * 0.26f;
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
            effect.Parameters["fadeAlpha"]?.SetValue(trailFade);
            effect.Parameters["heatBoost"]?.SetValue(IsFinisher ? 1f : (IsEX ? 0.45f : 0.25f));
            effect.Parameters["exMode"]?.SetValue(IsEX ? 1f : 0f);
            effect.Parameters["segCount"]?.SetValue(MathF.Max(5f, SwingArc * (IsEX ? 2.6f : 2.2f)));
            effect.Parameters["uNoiseTex"]?.SetValue(noise);
            foreach (EffectPass pass in effect.CurrentTechnique.Passes) {
                pass.Apply();
                device.DrawUserPrimitives(PrimitiveType.TriangleStrip, bars, 0, bars.Length - 2);
            }

            device.BlendState = origBlend;
            device.RasterizerState = origRaster;
        }
    }

    /// 毁灭者之刃手持挥砍
    internal class DestroyersBladeHeld : DestroyersBladeHeldBase
    {
        public override string Texture => CWRConstant.Item_Melee + "DestroyersBlade";
        public override LocalizedText DisplayName => ItemLoader.GetItem(ModContent.ItemType<DestroyersBlade>()).DisplayName;
        protected override int TargetItemID => ModContent.ItemType<DestroyersBlade>();
        protected override Texture2D GlowTex => DestroyersBlade.Glow.Value;
    }

    /// 毁灭者光束，DestroyerBeam.fx
    /// ai[1] 0普通 1EX
    internal class DestroyersBeam : ModProjectile, IPrimitiveDrawable
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private bool IsEX => Projectile.ai[1] > 0f;
        private ref float Init => ref Projectile.ai[0];

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 26;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults() {
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = 1;
            Projectile.width = 26;
            Projectile.height = 26;
            Projectile.timeLeft = 300;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.ignoreWater = true;
            Projectile.extraUpdates = 2;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
        }

        public override void AI() {
            if (Init == 0) {
                Init = 1;
                if (IsEX) {
                    Projectile.penetrate = 3;
                    Projectile.scale = 1.15f;
                }
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Item33 with { Volume = 0.4f, Pitch = 0.15f, MaxInstances = 5 }, Projectile.position);
                }
            }

            Projectile.rotation = Projectile.velocity.ToRotation();

            //沿途散落的电火花
            if (!VaultUtils.isServer && Main.rand.NextBool(9)) {
                Dust d = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(6f, 6f)
                    , DustID.RedTorch, -Projectile.velocity * 0.1f, 100, default, 1.1f);
                d.noGravity = true;
            }

            Lighting.AddLight(Projectile.Center, new Vector3(1f, 0.2f, 0.1f) * 1.2f * Main.essScale);
        }

        public override void OnKill(int timeLeft) {
            Projectile.Explode(IsEX ? 140 : 110, SoundID.Item14 with { Volume = 0.5f, Pitch = 0.1f, MaxInstances = 5 });
            if (Main.dedServ) {
                return;
            }
            Color warm = new Color(255, 90, 40);
            PRTLoader.NewParticle<PRT_MechExplosion>(Projectile.Center, Main.rand.NextVector2Circular(1f, 1f)
                , warm, IsEX ? 0.7f : 0.45f).Configure(Main.rand.Next(16, 26), warm);
            for (int i = 0; i < 8; i++) {
                PRTLoader.NewParticle<PRT_SparkAlpha>(Projectile.Center, Main.rand.NextVector2Circular(7f, 7f)
                    , Main.rand.NextBool() ? Color.Red : Color.OrangeRed
                    , Main.rand.NextFloat(1f, 2f)).Configure(false, Main.rand.Next(10, 16));
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            //弹头柔光与十字耀斑
            Texture2D glow = CWRAsset.SoftGlow.Value;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            Color coreColor = new Color(255, 70, 35);
            coreColor.A = 0;
            Main.EntitySpriteDraw(glow, drawPos, null, coreColor, 0f, glow.Size() / 2f
                , (IsEX ? 1.1f : 0.8f) * Projectile.scale, SpriteEffects.None, 0);

            Texture2D star = CWRAsset.StarTexture.Value;
            Color starColor = new Color(255, 160, 110);
            starColor.A = 0;
            Main.EntitySpriteDraw(star, drawPos, null, starColor * 0.8f, Projectile.rotation
                , star.Size() / 2f, 0.16f * Projectile.scale, SpriteEffects.None, 0);
            return false;
        }

        void IPrimitiveDrawable.DrawPrimitives() {
            Effect effect = EffectLoader.DestroyerBeam?.Value;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            if (effect == null || noise == null || Projectile.oldPos == null) {
                return;
            }

            //收集轨迹点，oldPos[0]最新
            int valid = 0;
            for (int i = 0; i < Projectile.oldPos.Length; i++) {
                if (Projectile.oldPos[i] == Vector2.Zero) {
                    break;
                }
                valid++;
            }
            if (valid < 3) {
                return;
            }

            float halfWidth = (IsEX ? 24f : 17f) * Projectile.scale;
            var bars = new VertexPositionColorTexture[valid * 2];
            for (int i = 0; i < valid; i++) {
                Vector2 pos = Projectile.oldPos[i] + Projectile.Size / 2f;
                Vector2 next = i == 0
                    ? Projectile.Center + Projectile.velocity
                    : Projectile.oldPos[i - 1] + Projectile.Size / 2f;
                Vector2 dir = (next - pos).SafeNormalize(Projectile.rotation.ToRotationVector2());
                Vector2 perp = dir.RotatedBy(MathHelper.PiOver2);

                float factor = 1f - i / (float)valid; //1=弹头 0=尾部
                float width = halfWidth * (0.35f + 0.65f * factor);
                bars[i * 2] = new VertexPositionColorTexture((pos + perp * width).ToVector3()
                    , Color.White, new Vector2(factor, 0f));
                bars[i * 2 + 1] = new VertexPositionColorTexture((pos - perp * width).ToVector3()
                    , Color.White, new Vector2(factor, 1f));
            }

            GraphicsDevice device = Main.graphics.GraphicsDevice;
            BlendState origBlend = device.BlendState;
            RasterizerState origRaster = device.RasterizerState;
            device.BlendState = BlendState.Additive;
            device.RasterizerState = RasterizerState.CullNone;

            effect.Parameters["transformMatrix"]?.SetValue(VaultUtils.GetTransfromMatrix());
            effect.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            effect.Parameters["fadeAlpha"]?.SetValue(1f);
            effect.Parameters["exMode"]?.SetValue(IsEX ? 1f : 0f);
            effect.Parameters["seed"]?.SetValue(Projectile.whoAmI * 0.137f % 1f);
            effect.Parameters["uNoiseTex"]?.SetValue(noise);
            foreach (EffectPass pass in effect.CurrentTechnique.Passes) {
                pass.Apply();
                device.DrawUserPrimitives(PrimitiveType.TriangleStrip, bars, 0, bars.Length - 2);
            }

            device.BlendState = origBlend;
            device.RasterizerState = origRaster;
        }

        public override void PostDraw(Color lightColor) => Lighting.AddLight(Projectile.Center, Color.Red.ToVector3() * 1.75f * Main.essScale);
    }
}
