using CalamityOverhaul.Common;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.GameContent.BaseEntity;
using InnoVault.GameSystem;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.Graphics.CameraModifiers;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Melee
{
    /// <summary>
    /// 化境。四段连击，奇数段宽矛头横扫、偶数段换细矛尖突刺，逐段加重
    /// </summary>
    internal class Flawless : ModItem
    {
        public override string Texture => CWRConstant.Item_Melee + "Flawless";

        /// <summary>细矛尖形态的物品图，下一段是突刺时顶掉快捷栏图标</summary>
        [VaultLoaden(CWRConstant.Item_Melee + "Flawless2")]
        private static Asset<Texture2D> ThrustIcon = null;

        public override void SetDefaults() {
            Item.width = Item.height = 74;
            Item.damage = 920;
            Item.crit = 16;
            Item.knockBack = 7.5f;
            //真实冷却 = max(useTime, 握持弹幕总帧)。四段弹幕 29/26/29/30，取 26 让弹幕始终说话，不留白给帧
            Item.useAnimation = Item.useTime = 26;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.useTurn = false;
            Item.noMelee = true;
            Item.noUseGraphic = true;
            Item.autoReuse = true;
            Item.UseSound = null;
            Item.shoot = ModContent.ProjectileType<FlawlessHeld>();
            Item.shootSpeed = 10f;
            Item.value = Item.sellPrice(gold: 75);
            //旧稿写的 Violet 稀有度已从灾厄移除，取同档的纯绿
            Item.rare = CWRID.Rarity_PureGreen > 0 ? CWRID.Rarity_PureGreen : ItemRarityID.Purple;
            DamageClass trueMelee = CWRRef.GetTrueMeleeDamageClass();
            Item.DamageType = trueMelee == DamageClass.Default ? DamageClass.Melee : trueMelee;
            //noMelee 武器需要手动允许近战词缀
            ItemOverride.ItemMeleePrefixDic[Type] = true;
        }

        public override bool CanUseItem(Player player)
            => player.ownedProjectileCounts[ModContent.ProjectileType<FlawlessHeld>()] == 0;

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position
            , Vector2 velocity, int type, int damage, float knockback) {
            int step = player.GetModPlayer<FlawlessPlayer>().AdvanceCombo();
            Projectile.NewProjectile(source, player.Center, velocity, type, damage, knockback, player.whoAmI, step);
            return false;
        }

        public override bool PreDrawInInventory(SpriteBatch spriteBatch, Vector2 position, Rectangle frame
            , Color drawColor, Color itemColor, Vector2 origin, float scale) {
            //手持时按下一段的形态换图标，让玩家在快捷栏就看出下一击是扫还是刺
            if (ThrustIcon?.Value is not Texture2D tex
                || Main.LocalPlayer.HeldItem != Item
                || !FlawlessPlayer.NextStepIsThrust(Main.LocalPlayer)) {
                return true;
            }
            spriteBatch.Draw(tex, position, null, drawColor, 0f, tex.Size() * 0.5f, scale, SpriteEffects.None, 0f);
            return false;
        }

        public override void AddRecipes() {
            if (CWRID.AllValid(CWRID.Item_CosmiliteBar, CWRID.Item_CryonicBar)) {
                CreateRecipe()
                .AddIngredient(ItemID.NorthPole)
                .AddIngredient(CWRID.Item_CosmiliteBar, 14)
                .AddIngredient(CWRID.Item_CryonicBar, 10)
                .AddTile(TileID.LunarCraftingStation)
                .Register();
            }
            else {
                CreateRecipe()
                .AddIngredient(ItemID.NorthPole)
                .AddIngredient(ItemID.LunarBar, 14)
                .AddIngredient(ItemID.Ectoplasm, 12)
                .AddTile(TileID.LunarCraftingStation)
                .Register();
            }
        }
    }

    /// <summary>
    /// 化境的连击进度。挂在玩家身上而不是 ModItem 字段上，后者每帧会被 tML 重建冲掉
    /// </summary>
    internal class FlawlessPlayer : ModPlayer
    {
        /// <summary>下一击的段号</summary>
        internal int ComboStep;
        /// <summary>久未出手就回到第一段</summary>
        private int comboResetTimer;

        internal static bool NextStepIsThrust(Player player)
            => FlawlessHeld.IsThrustStep(player.GetModPlayer<FlawlessPlayer>().ComboStep);

        /// <summary>取当前段并推进</summary>
        internal int AdvanceCombo() {
            int step = ComboStep;
            ComboStep = (step + 1) % FlawlessHeld.ComboLength;
            comboResetTimer = 75;
            return step;
        }

        public override void PostUpdate() {
            if (comboResetTimer > 0 && --comboResetTimer == 0) {
                ComboStep = 0;
            }
        }
    }

    /// <summary>
    /// 化境手持。扫—刺—反扫—终结长刺；横扫用宽矛头贴图走弧线拖尾，突刺换细矛尖贴图走持距推进
    /// </summary>
    internal class FlawlessHeld : BaseHeldProj, IPrimitiveDrawable
    {
        public override string Texture => CWRConstant.Item_Melee + "FlawlessHeld";
        public override LocalizedText DisplayName => VaultUtils.GetLocalizedItemName<Flawless>();

        /// <summary>细矛尖形态，突刺段替换本体贴图</summary>
        [VaultLoaden(CWRConstant.Item_Melee + "FlawlessHeld2")]
        private static Asset<Texture2D> ThrustForm = null;

        internal const int ComboLength = 4;

        //霜晶色板：刃缘白、晶心青
        private static Color CrystalEdge => new(206, 246, 255);
        private static Color CrystalCore => new(88, 214, 235);

        /// 连击段 0扫 1刺 2反扫 3终结长刺
        private int ComboStep => (int)Projectile.ai[0] % ComboLength;

        internal static bool IsThrustStep(int step) => step % 2 == 1;

        private bool IsThrust => IsThrustStep(ComboStep);
        private bool IsFinisher => ComboStep == ComboLength - 1;

        //阶段时长（逻辑帧，吃近战攻速）
        private float WindupTime => ComboStep switch { 0 => 6f, 1 => 4f, 2 => 6f, _ => 9f };
        private float ActiveTime => ComboStep switch { 0 => 9f, 1 => 6f, 2 => 9f, _ => 7f };
        //顿帧从收势尾巴等量扣回，不许延长这一拍
        private float RecoverTime => (ComboStep == 1 ? 16f : 14f) - hitStopSpent;
        private float TotalTime => WindupTime + ActiveTime + RecoverTime;

        /// <summary>逐段递增的伤害系数，沿用旧稿的 1 / 1.15 / 1.25 / 1.55</summary>
        private float StepDamageScale => ComboStep switch { 0 => 1f, 1 => 1.15f, 2 => 1.25f, _ => 1.55f };

        //横扫弧度，反扫走得更开
        private float SweepArc => ComboStep == 2 ? 3.9f : 3.4f;
        //突刺顶点的持出距离
        private float StabReach => IsFinisher ? 178f : 116f;
        //矛刃判定长度，持握点→矛尖
        private const float BladeLength = 112f;
        //终结段的顿帧总预算
        private const float HitStopBudget = 4f;

        private float elapsed;
        private float speedMul = 1f;
        private int lockedDirection = 1;
        private int sweepSign = 1;
        /// 矛身指向
        private Vector2 bladeUnit;
        /// 突刺持距
        private float holdout;
        private float startAngle;
        private float endAngle;
        private float currentRotation;
        private float lastRotation;
        /// 判定闩锁，保证高攻速下也有整帧判定
        private bool damageActive;
        private bool damageWindowClosed;
        private bool strikeSoundPlayed;
        private float glintFade;
        private float trailFade;
        private int hitStopFrames;
        private float hitStopSpent;
        private readonly HashSet<int> hitNPCs = [];

        //横扫轨迹缓存，0 为最新
        private const int TrailMax = 56;
        private const int TrailSubdiv = 4;
        private readonly float[] trailRot = new float[TrailMax];
        private int trailCount;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 52;
            Projectile.friendly = true;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            //与物品同类，否则真近战加成算不进实际命中
            DamageClass trueMelee = CWRRef.GetTrueMeleeDamageClass();
            Projectile.DamageType = trueMelee == DamageClass.Default ? DamageClass.Melee : trueMelee;
            Projectile.penetrate = -1;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
            Projectile.ownerHitCheck = true;
            Projectile.timeLeft = 60;
            Projectile.CWR().NotSubjectToSpecialEffects = true;
            Projectile.CWR().PierceResist = true;
        }

        public override bool ShouldUpdatePosition() => false;

        public override bool? CanDamage() => damageActive;

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            if (!damageActive) {
                return false;
            }
            Vector2 hand = Owner.GetPlayerStabilityCenter();
            float reach = (holdout + BladeLength) * Projectile.scale;
            if (IsThrust) {
                float thrustPoint = 0f;
                return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size()
                    , hand, hand + bladeUnit * reach, 30f, ref thrustPoint);
            }

            //横扫按本帧扫过的角度补采样，避免高攻速下从怪身上跳过去
            float delta = currentRotation - lastRotation;
            int steps = Math.Clamp((int)MathF.Ceiling(MathF.Abs(delta) * reach / 26f), 1, 24);
            for (int i = 0; i <= steps; i++) {
                float rotation = MathHelper.Lerp(lastRotation, currentRotation, i / (float)steps);
                float collisionPoint = 0f;
                if (Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size()
                    , hand, hand + rotation.ToRotationVector2() * reach, 36f, ref collisionPoint)) {
                    return true;
                }
            }
            return false;
        }

        public override void Initialize() {
            Vector2 aimUnit = Projectile.velocity.SafeNormalize(Vector2.UnitX * Owner.direction);
            lockedDirection = Math.Sign(aimUnit.X) == 0 ? Owner.direction : Math.Sign(aimUnit.X);
            Owner.direction = lockedDirection;

            speedMul = Owner.GetWeaponAttackSpeed(Item);
            if (speedMul <= 0f) {
                speedMul = 1f;
            }

            //反扫回抽，其余顺势而下
            sweepSign = ComboStep == 2 ? -1 : 1;
            float baseAngle = aimUnit.ToRotation();
            startAngle = baseAngle - sweepSign * SweepArc * 0.5f;
            endAngle = baseAngle + sweepSign * SweepArc * 0.5f;
            currentRotation = lastRotation = IsThrust ? baseAngle : startAngle;
            bladeUnit = currentRotation.ToRotationVector2();

            Projectile.damage = (int)(Projectile.damage * StepDamageScale);
            if (IsFinisher) {
                Projectile.scale = 1.12f;
            }
        }

        public override void AI() {
            if (Item.type != ModContent.ItemType<Flawless>()) {
                Projectile.Kill();
                return;
            }

            //终结段命中顿帧，只维持姿态
            if (hitStopFrames > 0) {
                hitStopFrames--;
                UpdatePlayerPose();
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

            UpdateDamageWindow(activeEnd);
            bladeUnit = currentRotation.ToRotationVector2();
            UpdatePlayerPose();

            if (damageActive) {
                PlayStrikeSound();
                SpawnShards();
            }
            glintFade *= 0.82f;

            Lighting.AddLight(Owner.GetPlayerStabilityCenter() + bladeUnit * (holdout + BladeLength * 0.8f)
                , CrystalCore.ToVector3() * (IsFinisher ? 0.85f : 0.55f));
            elapsed += speedMul;
        }

        /// <summary>
        /// 闩锁判定窗。闭窗要求本帧已经开过窗，所以高攻速一跳跨过整个挥出相时窗口只是迟到，不会消失
        /// </summary>
        private void UpdateDamageWindow(float activeEnd) {
            if (damageActive) {
                if (elapsed >= activeEnd) {
                    damageActive = false;
                    damageWindowClosed = true;
                }
                return;
            }
            if (!damageWindowClosed && elapsed >= WindupTime) {
                damageActive = true;
                glintFade = 1f;
            }
        }

        private void ThrustMotion(float activeEnd) {
            if (elapsed < WindupTime) {
                //回抽，终结段抽得更深
                float t = elapsed / WindupTime;
                holdout = IsFinisher
                    ? MathHelper.Lerp(12f, -22f, MathF.Pow(t, 3f))
                    : MathHelper.Lerp(10f, -12f, MathF.Sin(t * MathHelper.PiOver2));
            }
            else if (elapsed < activeEnd) {
                //高次幂 ease-out，前几帧就把矛尖送到位
                float t = (elapsed - WindupTime) / ActiveTime;
                float eased = 1f - MathF.Pow(1f - t, IsFinisher ? 6.5f : 4.2f);
                holdout = MathHelper.Lerp(IsFinisher ? -22f : -12f, StabReach, eased);
            }
            else {
                //收矛
                float t = (elapsed - activeEnd) / RecoverTime;
                holdout = MathHelper.Lerp(StabReach, 10f, t * t * (3f - 2f * t));
            }
        }

        private void SweepMotion(float activeEnd) {
            holdout = 24f;
            if (elapsed < WindupTime) {
                //蓄势回拉
                float t = elapsed / WindupTime;
                currentRotation = startAngle - sweepSign * 0.26f * MathF.Sin(t * MathHelper.PiOver2);
                trailFade = 0f;
            }
            else if (elapsed < activeEnd) {
                float t = (elapsed - WindupTime) / ActiveTime;
                float eased = 1f - MathF.Pow(1f - t, ComboStep == 2 ? 4.2f : 3.5f);
                currentRotation = MathHelper.Lerp(startAngle, endAngle, eased);
                trailFade = 1f;
                PushTrailSamples();
            }
            else {
                //收势，刀光顺势褪掉
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

        private void PlayStrikeSound() {
            if (strikeSoundPlayed) {
                return;
            }
            strikeSoundPlayed = true;
            if (VaultUtils.isServer) {
                return;
            }
            SoundEngine.PlaySound(SoundID.Item1 with {
                Pitch = IsThrust ? 0.25f + ComboStep * 0.08f : -0.1f + ComboStep * 0.06f
            }, Owner.Center);
            if (IsFinisher) {
                SoundEngine.PlaySound(SoundID.Item28 with { Pitch = -0.35f, Volume = 0.75f }, Owner.Center);
            }
        }

        /// <summary>碎晶迸裂：横扫沿切向甩出，突刺顺矛身抛出</summary>
        private void SpawnShards() {
            if (VaultUtils.isServer || !Main.rand.NextBool(2)) {
                return;
            }
            Vector2 tip = Owner.GetPlayerStabilityCenter() + bladeUnit * (holdout + BladeLength * 0.85f);
            Vector2 shardVel = IsThrust
                ? bladeUnit.RotatedByRandom(0.35f) * Main.rand.NextFloat(3f, 7f)
                : bladeUnit.RotatedBy(sweepSign * MathHelper.PiOver2) * Main.rand.NextFloat(4f, 9f);
            PRTLoader.NewParticle<PRT_Spark>(tip + Main.rand.NextVector2Circular(6f, 6f), shardVel
                , Main.rand.NextBool(3) ? CrystalEdge : CrystalCore, Main.rand.NextFloat(0.7f, 1.1f))
                ?.Configure(false, Main.rand.Next(6, 11), Owner);
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
            //noMelee 武器要手动转发物品命中钩子，维持装备与饰品的近战联动
            if (hitNPCs.Add(target.whoAmI)) {
                ItemLoader.OnHitNPC(Item, Owner, target, hit, damageDone);
                NPCLoader.OnHitByItem(target, Owner, Item, hit, damageDone);
                PlayerLoader.OnHitNPC(Owner, target, hit, damageDone);
            }

            target.AddBuff(BuffID.Frostburn2, 240 + ComboStep * 60);

            if (IsFinisher && Projectile.numHits <= 1) {
                GrantFinisherHitStop();
            }

            if (VaultUtils.isServer) {
                return;
            }
            SoundEngine.PlaySound(SoundID.Item27 with {
                Volume = 0.55f,
                Pitch = IsFinisher ? -0.3f : 0.2f
            }, target.Center);

            int burst = IsFinisher ? 14 : 6;
            for (int i = 0; i < burst; i++) {
                Vector2 shardVel = Main.rand.NextVector2Unit() * Main.rand.NextFloat(4f, IsFinisher ? 13f : 9f);
                PRTLoader.NewParticle<PRT_Spark>(target.Center, shardVel
                    , Main.rand.NextBool(3) ? CrystalEdge : CrystalCore, Main.rand.NextFloat(0.8f, 1.5f))
                    ?.Configure(false, Main.rand.Next(8, 14), Owner);
            }
            if (IsFinisher) {
                for (int i = 0; i < 8; i++) {
                    PRTLoader.NewParticle<PRT_Light>(target.Center
                        , Main.rand.NextVector2Unit() * Main.rand.NextFloat(6f, 15f)
                        , CrystalCore * 0.9f, 0.3f)
                        ?.Configure(Main.rand.Next(12, 20), opacity: 1.1f, squishStrenght: 1.5f, hueShift: 0f);
                }
            }
        }

        /// <summary>终结段命中的滞帧，从收势尾巴里扣，总帧守恒</summary>
        private void GrantFinisherHitStop() {
            int grant = (int)MathF.Min(HitStopBudget - hitStopSpent, 4f);
            if (grant <= 0) {
                return;
            }
            hitStopFrames = grant;
            hitStopSpent += grant;

            if (!VaultUtils.isServer && CWRClientConfig.Instance.ScreenVibration) {
                PunchCameraModifier modifier = new(Projectile.Center, bladeUnit, 5f, 6f, 9, 800f, FullName);
                Main.instance.CameraModifiers.Add(modifier);
            }
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info)
            => target.AddBuff(BuffID.Frostburn2, 240);

        public override bool PreDraw(ref Color lightColor) {
            //突刺段换细矛尖，横扫段用宽矛头
            Texture2D tex = IsThrust && ThrustForm?.Value is Texture2D thrustTex ? thrustTex : TextureValue;
            Vector2 origin = tex.Size() * 0.5f;
            Vector2 hand = Owner.GetPlayerStabilityCenter();
            //贴图矛尖指向右上，沿矛身方向旋转
            float rot = currentRotation + MathHelper.PiOver4;
            SpriteEffects effect = SpriteEffects.None;
            if (lockedDirection < 0) {
                rot += MathHelper.PiOver2;
                effect = SpriteEffects.FlipHorizontally;
            }

            //残影：突刺沿矛身回溯，横扫沿弧线回溯
            if (damageActive) {
                for (int i = 1; i <= 3; i++) {
                    Vector2 ghostPos;
                    float ghostRot = rot;
                    if (IsThrust) {
                        float ghostHoldout = holdout - i * 20f;
                        if (ghostHoldout < -22f) {
                            continue;
                        }
                        ghostPos = hand + bladeUnit * ghostHoldout - Main.screenPosition;
                    }
                    else {
                        float lerpRot = MathHelper.Lerp(currentRotation, lastRotation, i / 4f);
                        ghostRot = lerpRot + MathHelper.PiOver4 + (lockedDirection < 0 ? MathHelper.PiOver2 : 0f);
                        ghostPos = hand + lerpRot.ToRotationVector2() * holdout - Main.screenPosition;
                    }
                    Color ghostColor = CrystalCore * (0.32f * (1f - i / 4f));
                    ghostColor.A = 0;
                    Main.EntitySpriteDraw(tex, ghostPos, null, ghostColor, ghostRot, origin
                        , Projectile.scale, effect, 0);
                }
            }

            Vector2 drawPos = hand + bladeUnit * holdout - Main.screenPosition;
            Main.EntitySpriteDraw(tex, drawPos, null, lightColor, rot, origin, Projectile.scale, effect, 0);

            //晶面掠光：挥出瞬间刃面过一道白，随后两三帧内退掉
            if (glintFade > 0.04f) {
                Color glint = CrystalEdge with { A = 0 } * (glintFade * 0.5f);
                Main.EntitySpriteDraw(tex, drawPos, null, glint, rot, origin
                    , Projectile.scale * 1.04f, effect, 0);
            }
            return false;
        }

        void IPrimitiveDrawable.DrawPrimitives() {
            if (IsThrust || trailCount < 3 || trailFade <= 0.02f) {
                return;
            }
            Effect effect = EffectLoader.GradientTrail?.Value;
            if (effect == null || CWRAsset.Flawless_Bar?.Value is not Texture2D gradient
                || CWRAsset.SlashFlatBlurHVMirror?.Value is not Texture2D baseImage
                || CWRAsset.Airflow?.Value is not Texture2D flow
                || CWRAsset.Extra_193?.Value is not Texture2D dissolve) {
                return;
            }

            //从持握点张开的扇形条带，uv.x 1=最新 0=尾，uv.y 0=外缘 1=内缘
            var bars = new VertexPositionColorTexture[trailCount * 2];
            Vector2 center = Owner.GetPlayerStabilityCenter();
            float outer = (holdout + BladeLength + 12f) * Projectile.scale;
            float inner = 48f * Projectile.scale;
            Color vertexColor = new(255, 255, 255, (byte)(MathHelper.Clamp(trailFade, 0f, 1f) * 255f));
            for (int i = 0; i < trailCount; i++) {
                float factor = 1f - i / (float)trailCount;
                Vector2 dir = trailRot[i].ToRotationVector2();
                bars[i * 2] = new VertexPositionColorTexture((center + dir * outer).ToVector3()
                    , vertexColor, new Vector2(factor, 0f));
                bars[i * 2 + 1] = new VertexPositionColorTexture((center + dir * inner).ToVector3()
                    , vertexColor, new Vector2(factor, 1f));
            }

            GraphicsDevice device = Main.graphics.GraphicsDevice;
            BlendState origBlend = device.BlendState;
            RasterizerState origRaster = device.RasterizerState;
            device.BlendState = BlendState.Additive;
            device.RasterizerState = RasterizerState.CullNone;

            effect.Parameters["transformMatrix"]?.SetValue(VaultUtils.GetTransfromMatrix());
            effect.Parameters["uTime"]?.SetValue((float)Main.timeForVisualEffects * 0.08f);
            effect.Parameters["uTimeG"]?.SetValue(Main.GlobalTimeWrappedHourly * 0.2f);
            effect.Parameters["udissolveS"]?.SetValue(1f);
            effect.Parameters["uBaseImage"]?.SetValue(baseImage);
            effect.Parameters["uFlow"]?.SetValue(flow);
            effect.Parameters["uGradient"]?.SetValue(gradient);
            effect.Parameters["uDissolve"]?.SetValue(dissolve);
            foreach (EffectPass pass in effect.CurrentTechnique.Passes) {
                pass.Apply();
                device.DrawUserPrimitives(PrimitiveType.TriangleStrip, bars, 0, bars.Length - 2);
            }

            device.BlendState = origBlend;
            device.RasterizerState = origRaster;
        }
    }
}
