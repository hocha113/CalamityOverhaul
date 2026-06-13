using CalamityOverhaul.Content.PRTTypes;
using CalamityOverhaul.Content.UIs.SupertableUIs;
using InnoVault.GameContent.BaseEntity;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Melee
{
    internal class DawnshatterAzure : ModItem
    {
        public override string Texture => CWRConstant.Item_Melee + "DawnshatterAzure";

        private int comboCounter;
        private int comboResetTimer;

        public override void SetStaticDefaults() {
            Main.RegisterItemAnimation(Item.type, new DrawAnimationVertical(5, 4));
            ItemID.Sets.AnimatesAsSoul[Type] = true;
            ItemID.Sets.ItemsThatAllowRepeatedRightClick[Type] = true;
        }

        public override void SetDefaults() {
            Item.height = Item.width = 54;
            Item.damage = 11200;
            Item.DamageType = DamageClass.Melee;
            Item.noMelee = true;
            Item.noUseGraphic = true;
            Item.useTime = Item.useAnimation = 18;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.knockBack = 7.5f;
            Item.UseSound = null;
            Item.autoReuse = true;
            Item.value = Item.buyPrice(6, 23, 75, 0);
            Item.rare = CWRID.Rarity_DarkOrange;
            Item.shoot = ModContent.ProjectileType<DawnshatterSpearThrust>();
            Item.shootSpeed = 1f;
            Item.CWR().OmigaSnyContent = SupertableRecipeData.FullItems_DawnshatterAzure;
        }

        public override bool AltFunctionUse(Player player) => true;

        public override void ModifyWeaponCrit(Player player, ref float crit) => crit += 20;

        public override void HoldItem(Player player) {
            //连击计时器递减
            if (comboResetTimer > 0) {
                comboResetTimer--;
                if (comboResetTimer <= 0) {
                    comboCounter = 0;
                }
            }
        }

        public override bool CanUseItem(Player player) {
            return player.ownedProjectileCounts[ModContent.ProjectileType<DawnshatterSpearThrust>()] == 0
                && player.ownedProjectileCounts[ModContent.ProjectileType<DawnshatterChargeDash>()] == 0;
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            if (player.altFunctionUse == 2) {
                //右键蓄力突进
                SoundEngine.PlaySound("CalamityMod/Sounds/Custom/Yharon/YharonRoarShort".GetSound() with { Volume = 0.5f, Pitch = -0.1f }, player.Center);
                Projectile.NewProjectile(source, position, velocity, ModContent.ProjectileType<DawnshatterChargeDash>()
                    , (int)(damage * 2f), knockback * 1.5f, player.whoAmI);

                //大招后重置连击
                comboCounter = 0;
                comboResetTimer = 0;
                return false;
            }

            //左键刺击传连击阶段
            float thrustPitch = 0.1f + (comboCounter % 3) * 0.15f;
            SoundEngine.PlaySound(SoundID.Item1 with { Volume = 0.9f, Pitch = thrustPitch }, player.Center);

            Projectile.NewProjectile(source, position, velocity, type, damage, knockback, player.whoAmI, comboCounter);

            //连击计数+重置计时
            comboCounter++;
            comboResetTimer = 90;

            return false;
        }
    }

    /// 破晓青蓄力突进大招
    internal class DawnshatterChargeDash : BaseHeldProj
    {
        public override LocalizedText DisplayName => VaultUtils.GetLocalizedItemName<DawnshatterAzure>();
        public override string Texture => CWRConstant.Item_Melee + "DawnshatterAzure";

        private enum DashPhase
        {
            Charging,
            Dashing,
            Exploding,
            Recovery
        }

        private DashPhase currentPhase;
        private Vector2 dashDirection;
        private float chargeProgress;
        private int phaseTimer;
        private float screenShakeIntensity;
        private int hitEnemyCount;

        private const int ChargeDuration = 35;
        private const int DashDuration = 30;
        private const int ExplodeDuration = 20;
        private const int RecoveryDuration = 15;
        private const float MaxDashSpeed = 45f;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 160;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.timeLeft = ChargeDuration + DashDuration + ExplodeDuration + RecoveryDuration;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 8;
        }

        public override void AI() {
            VaultUtils.ClockFrame(ref Projectile.frame, 3, 3);
            SetHeld();
            phaseTimer++;

            //阶段切换
            if (currentPhase == DashPhase.Charging && phaseTimer >= ChargeDuration) {
                EnterDashPhase();
            }
            else if (currentPhase == DashPhase.Dashing && phaseTimer >= DashDuration) {
                EnterExplodePhase();
            }
            else if (currentPhase == DashPhase.Exploding && phaseTimer >= ExplodeDuration) {
                EnterRecoveryPhase();
            }

            //执行对应阶段逻辑
            switch (currentPhase) {
                case DashPhase.Charging:
                    UpdateCharging();
                    break;
                case DashPhase.Dashing:
                    UpdateDashing();
                    break;
                case DashPhase.Exploding:
                    UpdateExploding();
                    break;
                case DashPhase.Recovery:
                    UpdateRecovery();
                    break;
            }

            Projectile.rotation = dashDirection.ToRotation();
            SetDirection();

            //屏幕震动
            if (screenShakeIntensity > 0) {
                Owner.CWR().ScreenShakeValue = screenShakeIntensity;
                screenShakeIntensity *= 0.9f;
            }

            //强光照
            float lightIntensity = currentPhase == DashPhase.Dashing ? 2f : chargeProgress * 1.5f;
            Lighting.AddLight(Projectile.Center, new Vector3(1.5f, 1.2f, 0.5f) * lightIntensity);
        }

        //蓄力积蓄能量
        private void UpdateCharging() {
            chargeProgress = CWRUtils.EaseOutCubic(phaseTimer / (float)ChargeDuration);
            dashDirection = Projectile.velocity.SafeNormalize(Vector2.Zero);

            //长枪前方蓄力
            float chargeDistance = MathHelper.Lerp(50f, 80f, chargeProgress);
            Projectile.Center = Owner.MountedCenter + dashDirection * chargeDistance;

            //强制减速
            Owner.velocity *= 0.7f;

            //能量环绕效果
            SpawnChargeRings();

            //能量粒子向长枪汇聚
            if (Main.rand.NextBool()) {
                SpawnConvergingEnergy();
            }

            //蓄力音效循环
            if (phaseTimer % 10 == 0) {
                SoundEngine.PlaySound(SoundID.DD2_EtherianPortalSpawnEnemy with { Volume = 0.4f * chargeProgress, Pitch = chargeProgress * 0.5f }, Projectile.Center);
            }

            //蓄力完成预警
            if (phaseTimer == ChargeDuration - 5) {
                SoundEngine.PlaySound("CalamityMod/Sounds/Custom/Yharon/YharonRoarShort".GetSound() with { Volume = 0.8f, Pitch = -0.3f }, Owner.Center);
                SpawnChargeCompleteEffect();
            }
        }

        //进入突进
        private void EnterDashPhase() {
            currentPhase = DashPhase.Dashing;
            phaseTimer = 0;
            dashDirection = Projectile.velocity.SafeNormalize(Vector2.Zero);

            SoundEngine.PlaySound(SoundID.DD2_WyvernDiveDown with { Volume = 1f, Pitch = -0.2f }, Owner.Center);
            SoundEngine.PlaySound(SoundID.DD2_ExplosiveTrapExplode with { Volume = 0.8f, Pitch = 0.3f }, Owner.Center);

            //启动爆发
            SpawnDashStartExplosion();
            screenShakeIntensity = 15f;
        }

        //突进：先加速后减速
        private void UpdateDashing() {
            float dashProgress = phaseTimer / (float)DashDuration;

            //先加速后减速曲线
            float speedCurve;
            if (dashProgress < 0.3f) {
                speedCurve = CWRUtils.EaseOutCubic(dashProgress / 0.3f);
            }
            else {
                speedCurve = 1f - CWRUtils.EaseInQuad((dashProgress - 0.3f) / 0.7f) * 0.4f;
            }

            float dashSpeed = MaxDashSpeed * speedCurve;

            if (Projectile.IsOwnedByLocalPlayer()) {
                Owner.velocity = dashDirection * dashSpeed;
                Owner.GivePlayerImmuneState(3, false);
            }

            //长枪保持在前方
            Projectile.Center = Owner.MountedCenter + dashDirection * 120f;

            //拖尾
            SpawnDashTrail(dashProgress);

            //每隔一段时间播放冲击音效
            if (phaseTimer % 5 == 0) {
                SoundEngine.PlaySound(SoundID.DD2_BetsyWindAttack with { Volume = 0.5f, Pitch = 0.3f }, Projectile.Center);
            }

            //持续的小型爆炸
            if (phaseTimer % 3 == 0) {
                SpawnDashMiniExplosion();
            }
        }

        //进入爆炸阶段
        private void EnterExplodePhase() {
            currentPhase = DashPhase.Exploding;
            phaseTimer = 0;

            //停止移动
            if (Projectile.IsOwnedByLocalPlayer()) {
                Owner.velocity *= 0.2f;
            }

        //终极爆炸
            SpawnUltimateExplosion();
            screenShakeIntensity = 25f;

            //音效组合
            SoundEngine.PlaySound("CalamityMod/Sounds/Custom/Yharon/YharonFireOrb".GetSound() with { Volume = 1f, Pitch = -0.4f }, Projectile.Center);
            SoundEngine.PlaySound(SoundID.DD2_ExplosiveTrapExplode with { Volume = 0.9f, Pitch = -0.2f }, Projectile.Center);
        }

        //爆炸阶段
        private void UpdateExploding() {
            float explodeProgress = phaseTimer / (float)ExplodeDuration;

            //长枪在爆炸中心旋转
            Projectile.rotation += 0.5f * (1f - explodeProgress);
            Projectile.Center = Owner.MountedCenter + dashDirection * MathHelper.Lerp(120f, 80f, explodeProgress);

            //持续的爆炸效果
            if (phaseTimer % 2 == 0) {
                SpawnContinuousExplosion(explodeProgress);
            }

            //减速
            if (Projectile.IsOwnedByLocalPlayer()) {
                Owner.velocity *= 0.85f;
            }
        }

        //进入恢复阶段
        private void EnterRecoveryPhase() {
            currentPhase = DashPhase.Recovery;
            phaseTimer = 0;
        }

        //恢复阶段
        private void UpdateRecovery() {
            float recoveryProgress = phaseTimer / (float)RecoveryDuration;
            float pullbackDistance = MathHelper.Lerp(80f, 45f, CWRUtils.EaseInQuad(recoveryProgress));
            Projectile.Center = Owner.MountedCenter + dashDirection * pullbackDistance;

            if (Projectile.IsOwnedByLocalPlayer()) {
                Owner.velocity *= 0.88f;
            }
        }

        //蓄力能量环
        private void SpawnChargeRings() {
            if (phaseTimer % 3 != 0) return;

            float ringRadius = 60f + chargeProgress * 80f;
            int segments = 16;

            for (int i = 0; i < segments; i++) {
                float angle = MathHelper.TwoPi * i / segments + phaseTimer * 0.1f;
                Vector2 ringPos = Projectile.Center + angle.ToRotationVector2() * ringRadius;

                PRTLoader.NewParticle<PRT_Light>(ringPos, Vector2.Zero
                    , VaultUtils.MultiStepColorLerp(Main.rand.NextFloat(), Color.Gold, Color.Orange, Color.Red)
                    , Main.rand.NextFloat(0.8f, 1.2f)).Configure(15, opacity: 0.4f, squishStrenght: 1f, _entity: Owner, _followingRateRatio: 1f);
            }
        }

        //能量汇聚
        private void SpawnConvergingEnergy() {
            Vector2 spawnPos = Projectile.Center + Main.rand.NextVector2Circular(200f * (1f - chargeProgress), 200f * (1f - chargeProgress));
            Vector2 velocity = (Projectile.Center - spawnPos).SafeNormalize(Vector2.Zero) * 8f * chargeProgress;

            PRTLoader.NewParticle<PRT_Light>(spawnPos, velocity
                , VaultUtils.MultiStepColorLerp(Main.rand.NextFloat(), Color.Yellow, Color.Orange, Color.Red)
                , Main.rand.NextFloat(1f, 1.8f)).Configure(25, opacity: 0.5f, squishStrenght: 1.3f);

            int dust = Dust.NewDust(spawnPos, 1, 1, DustID.GoldCoin, velocity.X, velocity.Y, 100, default, 2f);
            Main.dust[dust].noGravity = true;
        }

        //蓄力完成特效
        private void SpawnChargeCompleteEffect() {
            for (int i = 0; i < 80; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(15f, 15f);

                PRTLoader.NewParticle<PRT_Light>(Projectile.Center, vel
                    , VaultUtils.MultiStepColorLerp(Main.rand.NextFloat(), Color.Red, Color.Orange, Color.Yellow, Color.White)
                    , Main.rand.NextFloat(1.5f, 2.5f)).Configure(30, opacity: 0.7f, squishStrenght: 1.8f);
            }

            //冲击波
            for (int ring = 0; ring < 3; ring++) {
                for (int i = 0; i < 24; i++) {
                    float angle = MathHelper.TwoPi * i / 24f;
                    Vector2 shockPos = Projectile.Center + angle.ToRotationVector2() * (100f + ring * 40f);

                    int dust = Dust.NewDust(shockPos, 1, 1, DustID.FireworkFountain_Yellow, 0, 0, 100, default, 3.5f);
                    Main.dust[dust].noGravity = true;
                    Main.dust[dust].velocity = angle.ToRotationVector2() * 15f;
                }
            }
        }

        //突进开始爆炸
        private void SpawnDashStartExplosion() {
            for (int i = 0; i < 120; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(25f, 25f);

                PRTLoader.NewParticle<PRT_Light>(Owner.Center, vel
                    , VaultUtils.MultiStepColorLerp(Main.rand.NextFloat(), Color.Red, Color.OrangeRed, Color.Orange, Color.Yellow)
                    , Main.rand.NextFloat(2f, 3.5f)).Configure(40, opacity: 0.8f, squishStrenght: 2f);
            }

            //大范围火焰爆发
            for (int i = 0; i < 200; i++) {
                int dust = Dust.NewDust(Owner.Center, 1, 1, DustID.Torch, 0, 0, 100, default, Main.rand.NextFloat(3f, 5f));
                Main.dust[dust].velocity = Main.rand.NextVector2Circular(20f, 20f);
                Main.dust[dust].noGravity = true;
            }
        }

        //突进拖尾
        private void SpawnDashTrail(float dashProgress) {
            //烈焰拖尾
            for (int i = 0; i < 6; i++) {
                Vector2 trailPos = Projectile.Center + Main.rand.NextVector2Circular(40f, 40f) - dashDirection * i * 30f;
                Vector2 trailVel = -dashDirection * Main.rand.NextFloat(5f, 15f);

                PRTLoader.NewParticle<PRT_Light>(trailPos, trailVel
                    , VaultUtils.MultiStepColorLerp(Main.rand.NextFloat(), Color.Red, Color.Orange, Color.Yellow)
                    , Main.rand.NextFloat(2f, 3.5f)).Configure(20, opacity: 0.6f, squishStrenght: 1.8f);
            }

            //金色能量流
            if (Main.rand.NextBool(2)) {
                Vector2 energyPos = Projectile.Center + Main.rand.NextVector2Circular(50f, 50f);
                int dust = Dust.NewDust(energyPos, 1, 1, DustID.GoldCoin, -dashDirection.X * 10f, -dashDirection.Y * 10f, 100, default, 3f);
                Main.dust[dust].noGravity = true;
                Main.dust[dust].fadeIn = 2f;
            }

            //螺旋火焰
            float spiralAngle = phaseTimer * 0.4f;
            for (int i = 0; i < 3; i++) {
                float angle = spiralAngle + i * MathHelper.TwoPi / 3f;
                Vector2 spiralPos = Projectile.Center + angle.ToRotationVector2() * 60f;

                int dust = Dust.NewDust(spiralPos, 1, 1, DustID.Torch, -dashDirection.X * 8f, -dashDirection.Y * 8f, 100, default, 2.5f);
                Main.dust[dust].noGravity = true;
            }
        }

        //突进小型爆炸
        private void SpawnDashMiniExplosion() {
            for (int i = 0; i < 25; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(12f, 12f);

                PRTLoader.NewParticle<PRT_Light>(Projectile.Center, vel
                    , VaultUtils.MultiStepColorLerp(Main.rand.NextFloat(), Color.OrangeRed, Color.Orange, Color.Yellow)
                    , Main.rand.NextFloat(1.2f, 2f)).Configure(15, opacity: 0.5f, squishStrenght: 1.5f);
            }

            //额外火焰弹
            if (Projectile.IsOwnedByLocalPlayer() && Main.rand.NextBool(3)) {
                for (int i = 0; i < 4; i++) {
                    float angle = MathHelper.PiOver2 * i;
                    Vector2 vel = (dashDirection.ToRotation() + angle).ToRotationVector2() * 12f;

                    Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center, vel
                        , ModContent.ProjectileType<DawnshatterFireball>(), (int)(Projectile.damage * 0.8f), 5f, Projectile.owner);
                }
            }
        }

        //终极爆炸
        private void SpawnUltimateExplosion() {
            //超大范围粒子爆发
            for (int i = 0; i < 300; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(35f, 35f);

                PRTLoader.NewParticle<PRT_Light>(Projectile.Center, vel
                    , VaultUtils.MultiStepColorLerp(Main.rand.NextFloat(), Color.Red, Color.OrangeRed, Color.Orange, Color.Yellow, Color.White)
                    , Main.rand.NextFloat(2.5f, 4.5f)).Configure(60, opacity: 0.9f, squishStrenght: 2.5f);
            }

            //多层冲击波
            for (int ring = 0; ring < 8; ring++) {
                int segments = 36;
                for (int i = 0; i < segments; i++) {
                    float angle = MathHelper.TwoPi * i / segments;
                    Vector2 shockPos = Projectile.Center + angle.ToRotationVector2() * (120f + ring * 60f);

                    int dust = Dust.NewDust(shockPos, 1, 1, DustID.FireworkFountain_Yellow, 0, 0, 100, default, 4f);
                    Main.dust[dust].noGravity = true;
                    Main.dust[dust].velocity = angle.ToRotationVector2() * 20f;
                    Main.dust[dust].fadeIn = 3f;
                }
            }

            //超大范围尘埃
            for (int i = 0; i < 400; i++) {
                int dustType = Main.rand.NextBool() ? DustID.Torch : DustID.FireworkFountain_Red;
                int dust = Dust.NewDust(Projectile.Center, 1, 1, dustType, 0, 0, 100, default, Main.rand.NextFloat(4f, 7f));
                Main.dust[dust].velocity = Main.rand.NextVector2Circular(30f, 30f);
                Main.dust[dust].noGravity = true;
            }

            //大量火焰弹
            if (Projectile.IsOwnedByLocalPlayer()) {
                for (int i = 0; i < 36; i++) {
                    float angle = MathHelper.TwoPi * i / 36f;
                    Vector2 vel = angle.ToRotationVector2() * Main.rand.NextFloat(15f, 25f);

                    Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center, vel
                        , ModContent.ProjectileType<DawnshatterFireball>(), Projectile.damage, 6f, Projectile.owner);
                }
            }
        }

        //持续爆炸
        private void SpawnContinuousExplosion(float progress) {
            for (int i = 0; i < 15; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(20f, 20f) * (1f - progress);

                PRTLoader.NewParticle<PRT_Light>(Projectile.Center, vel
                    , VaultUtils.MultiStepColorLerp(Main.rand.NextFloat(), Color.Red, Color.Orange, Color.Yellow)
                    , Main.rand.NextFloat(1.8f, 3f)).Configure(25, opacity: 0.6f, squishStrenght: 1.8f);
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            hitEnemyCount++;

            //OnFire3+Daybreak debuff
            target.AddBuff(BuffID.OnFire3, 600);
            target.AddBuff(BuffID.Daybreak, 480);
            target.AddBuff(BuffID.Ichor, 360);

            //突进超强击退
            if (currentPhase == DashPhase.Dashing) {
                target.velocity += dashDirection * 35f;

            //每5敌人生成爆炸
                if (hitEnemyCount % 5 == 0) {
                    SpawnHitExplosion(target.Center);
                }
            }

            //命中粒子
            for (int i = 0; i < 30; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(12f, 12f);

                PRTLoader.NewParticle<PRT_Light>(target.Center, vel
                    , VaultUtils.MultiStepColorLerp(Main.rand.NextFloat(), Color.Red, Color.Orange, Color.Yellow)
                    , Main.rand.NextFloat(1.5f, 2.5f)).Configure(20, opacity: 0.6f, squishStrenght: 1.5f);
            }

            SoundEngine.PlaySound(SoundID.DD2_MonkStaffSwing with { Volume = 0.6f, Pitch = 0.1f }, target.Center);
        }

        //命中爆炸
        private void SpawnHitExplosion(Vector2 position) {
            for (int i = 0; i < 80; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(18f, 18f);

                PRTLoader.NewParticle<PRT_Light>(position, vel
                    , VaultUtils.MultiStepColorLerp(Main.rand.NextFloat(), Color.Red, Color.OrangeRed, Color.Orange, Color.Yellow)
                    , Main.rand.NextFloat(1.8f, 3f)).Configure(35, opacity: 0.7f, squishStrenght: 2f);
            }

            if (Projectile.IsOwnedByLocalPlayer()) {
                for (int i = 0; i < 12; i++) {
                    float angle = MathHelper.TwoPi * i / 12f;
                    Vector2 velocity = angle.ToRotationVector2() * 15f;

                    Projectile.NewProjectile(Projectile.GetSource_FromThis(), position, velocity
                        , ModContent.ProjectileType<DawnshatterFireball>(), (int)(Projectile.damage * 0.7f), 5f, Projectile.owner);
                }
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D texture = TextureAssets.Projectile[Type].Value;
            float drawRotation = Projectile.rotation + (Owner.direction > 0 ? MathHelper.PiOver4 : MathHelper.PiOver4 * 3);
            Vector2 drawPosition = Projectile.Center - Main.screenPosition;
            Vector2 origin = VaultUtils.GetOrig(texture, 4);
            SpriteEffects effects = Owner.direction > 0 ? SpriteEffects.None : SpriteEffects.FlipHorizontally;

            //阶段发光强度
            float glowIntensity = 0f;
            if (currentPhase == DashPhase.Charging) {
                glowIntensity = chargeProgress * 1.5f;
            }
            else if (currentPhase == DashPhase.Dashing || currentPhase == DashPhase.Exploding) {
                glowIntensity = 2f;
            }
            else {
                glowIntensity = 1f - phaseTimer / (float)RecoveryDuration;
            }

            //多层发光
            for (int i = 0; i < 5; i++) {
                float layerScale = 0.75f + i * 0.08f;
                float layerAlpha = (0.5f - i * 0.08f) * glowIntensity;
                Color glowColor = VaultUtils.MultiStepColorLerp(i / 5f, Color.Red, Color.OrangeRed, Color.Orange, Color.Yellow, Color.White) with { A = 0 };
                glowColor *= layerAlpha;

                Main.EntitySpriteDraw(texture, drawPosition, texture.GetRectangle(Projectile.frame, 4), glowColor
                    , drawRotation, origin, Projectile.scale * layerScale, effects, 0);
            }

            //主体
            Color drawColor = Projectile.GetAlpha(lightColor);
            if (currentPhase == DashPhase.Dashing) {
                drawColor = Color.Lerp(drawColor, Color.White, 0.5f);
            }

            Main.EntitySpriteDraw(texture, drawPosition, texture.GetRectangle(Projectile.frame, 4), drawColor
                , drawRotation, origin, Projectile.scale * 0.7f, effects, 0);

            //能量光晕
            if (glowIntensity > 0.5f) {
                Color energyColor = new Color(255, 180, 60, 0) * glowIntensity * 0.8f;
                Main.EntitySpriteDraw(texture, drawPosition, texture.GetRectangle(Projectile.frame, 4), energyColor
                    , drawRotation, origin, Projectile.scale * 0.85f, effects, 0);
            }

            return false;
        }
    }

    /// 破晓青火焰弹，重力+弱追踪
    internal class DawnshatterFireball : ModProjectile
    {
        public override string Texture => CWRConstant.Placeholder;

        private float scale = 1f;
        private int trailCounter;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 12;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 32;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = 6;
            Projectile.timeLeft = 180;
            Projectile.alpha = 0;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = false;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 12;
        }

        public override void AI() {
            //重力+阻力
            Projectile.velocity.Y += 0.12f;
            Projectile.velocity *= 0.995f;

            //旋转
            Projectile.rotation += Projectile.velocity.Length() * 0.05f;

            //脉冲缩放
            scale = 1f + (float)System.Math.Sin(Projectile.timeLeft * 0.2f) * 0.15f;

            //火焰拖尾
            if (Main.rand.NextBool()) {
                SpawnTrailEffect();
            }

            //弱追踪
            if (Projectile.timeLeft > 40 && Projectile.timeLeft % 10 == 0) {
                NPC target = Projectile.Center.FindClosestNPC(300f);
                if (target != null) {
                    Vector2 toTarget = target.Center - Projectile.Center;
                    float targetAngle = toTarget.ToRotation();
                    float currentAngle = Projectile.velocity.ToRotation();

                    //平滑转向
                    float angleDiff = MathHelper.WrapAngle(targetAngle - currentAngle);
                    Projectile.velocity = Projectile.velocity.RotatedBy(angleDiff * 0.1f);
                    Projectile.velocity = Projectile.velocity.SafeNormalize(Vector2.Zero) * MathHelper.Lerp(Projectile.velocity.Length(), 14f, 0.05f);
                }
            }

            //淡出效果
            if (Projectile.timeLeft < 40) {
                Projectile.alpha += 6;
            }

            //添加光照
            Lighting.AddLight(Projectile.Center, new Vector3(1.2f, 0.8f, 0.3f) * scale);

            //环绕粒子
            trailCounter++;
            if (trailCounter % 3 == 0) {
                SpawnOrbitParticle();
            }
        }

        //拖尾特效
        private void SpawnTrailEffect() {
            Vector2 trailPos = Projectile.Center - Projectile.velocity * 0.5f + Main.rand.NextVector2Circular(5f, 5f);

            PRTLoader.NewParticle<PRT_Light>(trailPos, -Projectile.velocity * 0.3f
                , VaultUtils.MultiStepColorLerp(Main.rand.NextFloat(), Color.Red, Color.Orange, Color.Yellow)
                , Main.rand.NextFloat(0.8f, 1.5f)).Configure(15, 0.4f, 1.2f);

            int dustType = Main.rand.NextBool() ? DustID.Torch : DustID.FireworkFountain_Yellow;
            int dust = Dust.NewDust(trailPos, 1, 1, dustType, -Projectile.velocity.X * 0.3f, -Projectile.velocity.Y * 0.3f, 100, default, 1.5f);
            Main.dust[dust].noGravity = true;
        }

        //环绕粒子
        private void SpawnOrbitParticle() {
            float angle = trailCounter * 0.3f;
            Vector2 offset = angle.ToRotationVector2() * 15f * scale;
            Vector2 particlePos = Projectile.Center + offset;

            int dust = Dust.NewDust(particlePos, 1, 1, DustID.GoldCoin, 0, 0, 100, default, 1.2f);
            Main.dust[dust].noGravity = true;
            Main.dust[dust].velocity = Projectile.velocity * 0.2f;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            //OnFire3+Daybreak debuff
            target.AddBuff(BuffID.OnFire3, 300);
            target.AddBuff(BuffID.Daybreak, 240);

            Projectile.penetrate--;

            if (Projectile.penetrate <= 0) {
                Projectile.Kill();
                return;
            }

            //命中爆发
            SpawnHitBurst(target.Center);

            //命中音效
            SoundEngine.PlaySound("CalamityMod/Sounds/Custom/Yharon/YharonFireOrb".GetSound() with { Volume = 0.4f, Pitch = 0.5f }, target.Center);

            //反弹并减速
            Projectile.velocity *= -0.7f;
            Projectile.velocity = Projectile.velocity.RotatedByRandom(0.5f);

            //穿透剩多则换目标
            if (Projectile.penetrate > 2) {
                NPC newTarget = Projectile.Center.FindClosestNPC(400f, false, true, new System.Collections.Generic.HashSet<NPC> { target });
                if (newTarget != null) {
                    Vector2 toNewTarget = (newTarget.Center - Projectile.Center).SafeNormalize(Vector2.Zero) * 12f;
                    Projectile.velocity = Vector2.Lerp(Projectile.velocity, toNewTarget, 0.3f);
                }
            }
        }

        //命中爆发
        private void SpawnHitBurst(Vector2 position) {
            for (int i = 0; i < 15; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(8f, 8f);

                PRTLoader.NewParticle<PRT_Light>(position, vel
                    , VaultUtils.MultiStepColorLerp(Main.rand.NextFloat(), Color.OrangeRed, Color.Orange, Color.Yellow)
                    , Main.rand.NextFloat(1f, 1.8f)).Configure(20, 0.5f, 1.3f);
            }

            for (int i = 0; i < 10; i++) {
                int dust = Dust.NewDust(position, 1, 1, DustID.Torch, 0, 0, 100, default, Main.rand.NextFloat(1.5f, 2.5f));
                Main.dust[dust].velocity = Main.rand.NextVector2Circular(6f, 6f);
                Main.dust[dust].noGravity = true;
            }
        }

        public override bool OnTileCollide(Vector2 oldVelocity) {
            //碰撞时弹跳
            if (System.Math.Abs(Projectile.velocity.X - oldVelocity.X) > float.Epsilon) {
                Projectile.velocity.X = -oldVelocity.X * 0.7f;
            }
            if (System.Math.Abs(Projectile.velocity.Y - oldVelocity.Y) > float.Epsilon) {
                Projectile.velocity.Y = -oldVelocity.Y * 0.7f;
            }

            SoundEngine.PlaySound(SoundID.Dig with { Volume = 0.4f, Pitch = 0.4f }, Projectile.Center);

            //碰撞粒子
            for (int i = 0; i < 8; i++) {
                int dust = Dust.NewDust(Projectile.Center, 1, 1, DustID.Torch, 0, 0, 100, default, 1.5f);
                Main.dust[dust].velocity = Main.rand.NextVector2Circular(4f, 4f);
                Main.dust[dust].noGravity = true;
            }

            Projectile.penetrate--;
            return Projectile.penetrate <= 0;
        }

        public override void OnKill(int timeLeft) {
            //爆炸音效
            SoundEngine.PlaySound(SoundID.DD2_ExplosiveTrapExplode with { Volume = 0.5f, Pitch = 0.4f }, Projectile.Center);

            //爆炸粒子
            for (int i = 0; i < 35; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(12f, 12f);

                PRTLoader.NewParticle<PRT_Light>(Projectile.Center, vel
                    , VaultUtils.MultiStepColorLerp(Main.rand.NextFloat(), Color.Red, Color.Orange, Color.Yellow, Color.White)
                    , Main.rand.NextFloat(1.5f, 2.5f)).Configure(30, 0.6f, 1.8f);
            }

            //火焰尘埃
            for (int i = 0; i < 40; i++) {
                int dustType = Main.rand.NextBool() ? DustID.Torch : DustID.FireworkFountain_Red;
                int dust = Dust.NewDust(Projectile.Center, 1, 1, dustType, 0, 0, 100, default, Main.rand.NextFloat(2f, 3.5f));
                Main.dust[dust].velocity = Main.rand.NextVector2Circular(10f, 10f);
                Main.dust[dust].noGravity = true;
            }

            //金色爆炸光效
            for (int i = 0; i < 20; i++) {
                int dust = Dust.NewDust(Projectile.Center, 1, 1, DustID.GoldCoin, 0, 0, 100, default, 2.5f);
                Main.dust[dust].velocity = Main.rand.NextVector2Circular(8f, 8f);
                Main.dust[dust].noGravity = true;
                Main.dust[dust].fadeIn = 1.5f;
            }

            //冲击波
            for (int i = 0; i < 16; i++) {
                float angle = MathHelper.TwoPi * i / 16f;
                Vector2 shockPos = Projectile.Center + angle.ToRotationVector2() * 40f;

                int dust = Dust.NewDust(shockPos, 1, 1, DustID.FireworkFountain_Yellow, 0, 0, 100, default, 2f);
                Main.dust[dust].velocity = angle.ToRotationVector2() * 6f;
                Main.dust[dust].noGravity = true;
            }
        }

        public override Color? GetAlpha(Color lightColor) {
            //火球自发光
            float intensity = 1f - Projectile.alpha / 255f;
            return VaultUtils.MultiStepColorLerp(0.5f, Color.OrangeRed, Color.Orange, Color.Yellow) * intensity;
        }
    }

    /// 破晓青连击刺击手持
    internal class DawnshatterSpearThrust : BaseHeldProj
    {
        public override LocalizedText DisplayName => VaultUtils.GetLocalizedItemName<DawnshatterAzure>();
        public override string Texture => CWRConstant.Item_Melee + "DawnshatterAzure";
        private int comboStage;//连击阶段
        private bool spawnedShockwave;
        private float energyIntensity;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 124;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.timeLeft = 38;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.ownerHitCheck = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 15;
        }

        public override void AI() {
            VaultUtils.ClockFrame(ref Projectile.frame, 4, 3);
            SetHeld();

            int maxTime = 38;
            float progress = 1f - Projectile.timeLeft / (float)maxTime;

            //连击阶段
            comboStage = (int)Projectile.ai[0] % 3;

            //激进刺击曲线
            float thrustProgress;
            if (progress < 0.35f) {
                //前35%爆发前刺
                thrustProgress = CWRUtils.EaseOutExpo(progress / 0.35f);
            }
            else if (progress < 0.6f) {
                //中25%短暂停顿
                thrustProgress = 1f + (float)Math.Sin((progress - 0.35f) / 0.25f * MathHelper.Pi) * 0.15f;
            }
            else {
                //后40%快速回收
                thrustProgress = 1f - CWRUtils.EaseInCubic((progress - 0.6f) / 0.4f);
            }

            //连击段调刺距
            float maxDistance = 200f + comboStage * 30f;
            float currentDistance = MathHelper.Lerp(0, maxDistance, thrustProgress);

            Projectile.velocity = Projectile.velocity.SafeNormalize(Vector2.Zero);
            Projectile.Center = Owner.MountedCenter + Projectile.velocity * currentDistance;
            Projectile.rotation = Projectile.velocity.ToRotation();
            SetDirection();

            //能量强度随时间变化
            energyIntensity = (float)Math.Sin(progress * MathHelper.Pi) * (1f + comboStage * 0.3f);

            //持续粒子
            if (progress < 0.7f) {
                SpawnContinuousEffects(progress);
            }

            //刺击最远爆发
            if (Projectile.timeLeft == maxTime / 2 && !spawnedShockwave) {
                SpawnThrustExplosion(comboStage);
                spawnedShockwave = true;
            }

            //第三段额外能量波
            if (comboStage == 2 && progress > 0.3f && progress < 0.4f) {
                if (Main.rand.NextBool(3)) {
                    SpawnEnergyWave();
                }
            }

            //光照
            Lighting.AddLight(Projectile.Center, new Vector3(1f, 0.8f, 0.3f) * energyIntensity);
        }

        //刺击持续粒子
        private void SpawnContinuousEffects(float progress) {
            Vector2 tipPos = Projectile.Center + Projectile.velocity * 60f;

            //主火焰拖尾
            if (Main.rand.NextBool(2)) {
                Vector2 particlePos = Projectile.Center + Projectile.velocity * Main.rand.NextFloat(20f, 80f);
                Vector2 particleVel = -Projectile.velocity * Main.rand.NextFloat(2f, 6f);

                PRTLoader.NewParticle<PRT_Light>(particlePos, particleVel
                    , VaultUtils.MultiStepColorLerp(Main.rand.NextFloat(), Color.OrangeRed, Color.Gold, Color.White)
                    , Main.rand.NextFloat(0.8f, 1.5f)).Configure(15, opacity: 0.5f, squishStrenght: 1.2f);
            }

            //能量光点环绕
            if (Main.rand.NextBool(3)) {
                float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                Vector2 offset = angle.ToRotationVector2() * Main.rand.NextFloat(30f, 60f);
                Vector2 particlePos = Projectile.Center + offset;

                PRTLoader.NewParticle<PRT_Light>(particlePos, Vector2.Zero
                    , Color.Gold, Main.rand.NextFloat(0.5f, 1f)).Configure(12, opacity: 0.3f, squishStrenght: 1f, _entity: Owner, _followingRateRatio: 0.8f);
            }

            //烈焰螺旋
            for (int i = 0; i < 2; i++) {
                float spiralAngle = progress * MathHelper.TwoPi * 3f + i * MathHelper.Pi;
                Vector2 spiralOffset = spiralAngle.ToRotationVector2() * 40f;
                Vector2 spiralPos = Projectile.Center + Projectile.velocity * 40f + spiralOffset;

                int dust = Dust.NewDust(spiralPos, 1, 1, DustID.Torch, 0, 0, 100, default, 1.8f);
                Main.dust[dust].noGravity = true;
                Main.dust[dust].velocity = -Projectile.velocity * 2f;
            }

            //金色能量流
            if (Main.rand.NextBool(4)) {
                Vector2 energyPos = tipPos + Main.rand.NextVector2Circular(15f, 15f);
                int dust = Dust.NewDust(energyPos, 1, 1, DustID.GoldCoin, 0, 0, 100, default, Main.rand.NextFloat(1.5f, 2.5f));
                Main.dust[dust].noGravity = true;
                Main.dust[dust].velocity = Main.rand.NextVector2Circular(3f, 3f);
                Main.dust[dust].fadeIn = 1.5f;
            }
        }

        //刺击爆发特效
        private void SpawnThrustExplosion(int stage) {
            Vector2 tipPos = Projectile.Center + Projectile.velocity * 70f;

            SoundEngine.PlaySound("CalamityMod/Sounds/Custom/Yharon/YharonFireOrb".GetSound() with { Volume = 0.7f, Pitch = -0.2f + stage * 0.1f }, tipPos);

            //大范围火焰爆发
            int particleCount = 30 + stage * 15;
            for (int i = 0; i < particleCount; i++) {
                float angle = MathHelper.TwoPi * i / particleCount;
                Vector2 vel = angle.ToRotationVector2() * Main.rand.NextFloat(6f, 14f);

                PRTLoader.NewParticle<PRT_Light>(tipPos, vel
                    , VaultUtils.MultiStepColorLerp(Main.rand.NextFloat(), Color.Red, Color.Orange, Color.Yellow)
                    , Main.rand.NextFloat(1.2f, 2f)).Configure(25, opacity: 0.6f, squishStrenght: 1.5f);
            }

            //金色冲击波
            for (int i = 0; i < 3; i++) {
                float radius = 40f + i * 20f;
                int segments = 24;
                for (int j = 0; j < segments; j++) {
                    float angle = MathHelper.TwoPi * j / segments;
                    Vector2 shockPos = tipPos + angle.ToRotationVector2() * radius;

                    int dust = Dust.NewDust(shockPos, 1, 1, DustID.GoldCoin, 0, 0, 100, default, 2.5f);
                    Main.dust[dust].noGravity = true;
                    Main.dust[dust].velocity = angle.ToRotationVector2() * 8f;
                }
            }

            //烈焰旋涡
            for (int i = 0; i < 60; i++) {
                Vector2 randVel = Main.rand.NextVector2Circular(12f, 12f);
                int dust = Dust.NewDust(tipPos, 1, 1, DustID.Torch, randVel.X, randVel.Y, 100, default, Main.rand.NextFloat(2f, 3.5f));
                Main.dust[dust].noGravity = true;
            }

            //第三段超大爆炸
            if (stage == 2) {
                SpawnFinaleExplosion(tipPos);
            }

            //生成火焰弹
            if (Projectile.IsOwnedByLocalPlayer()) {
                int projectileCount = 3 + stage * 2;
                for (int i = 0; i < projectileCount; i++) {
                    float spreadAngle = MathHelper.TwoPi * i / projectileCount;
                    Vector2 fireballVel = (Projectile.velocity.ToRotation() + spreadAngle).ToRotationVector2() * Main.rand.NextFloat(10f, 15f);

                    Projectile.NewProjectile(Projectile.GetSource_FromThis(), tipPos, fireballVel
                        , ModContent.ProjectileType<DawnshatterFireball>(), (int)(Projectile.damage * 0.6f), 3f, Projectile.owner);
                }
            }
        }

        //第三段终结爆炸
        private void SpawnFinaleExplosion(Vector2 position) {
            SoundEngine.PlaySound("CalamityMod/Sounds/Custom/Yharon/YharonRoarShort".GetSound() with { Volume = 0.8f, Pitch = 0.1f }, position);

            //超大范围爆炸粒子
            for (int i = 0; i < 100; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(20f, 20f);

                PRTLoader.NewParticle<PRT_Light>(position, vel
                    , VaultUtils.MultiStepColorLerp(Main.rand.NextFloat(), Color.Red, Color.OrangeRed, Color.Gold, Color.White)
                    , Main.rand.NextFloat(1.5f, 2.8f)).Configure(35, opacity: 0.8f, squishStrenght: 1.8f);
            }

            //环形冲击波扩散
            for (int ring = 0; ring < 5; ring++) {
                for (int i = 0; i < 32; i++) {
                    float angle = MathHelper.TwoPi * i / 32f;
                    Vector2 shockPos = position + angle.ToRotationVector2() * (80f + ring * 30f);

                    int dust = Dust.NewDust(shockPos, 1, 1, DustID.FireworkFountain_Yellow, 0, 0, 100, default, 3f);
                    Main.dust[dust].noGravity = true;
                    Main.dust[dust].velocity = angle.ToRotationVector2() * 12f;
                    Main.dust[dust].fadeIn = 2f;
                }
            }
        }

        //能量波
        private void SpawnEnergyWave() {
            Vector2 wavePos = Projectile.Center + Projectile.velocity * Main.rand.NextFloat(30f, 70f);

            PRTLoader.NewParticle<PRT_Light>(wavePos, Projectile.velocity * 5f
                , Color.Cyan, Main.rand.NextFloat(1f, 1.5f)).Configure(20, opacity: 0.5f, squishStrenght: 1.3f);

            int dust = Dust.NewDust(wavePos, 1, 1, DustID.Electric, 0, 0, 100, default, 2f);
            Main.dust[dust].noGravity = true;
            Main.dust[dust].velocity = Projectile.velocity * 6f;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(BuffID.OnFire3, 360);
            target.AddBuff(BuffID.Daybreak, 300);

            //强击退
            if (comboStage == 2) {
                target.velocity += Projectile.velocity * 25f;
            }

            //命中音效
            SoundEngine.PlaySound(SoundID.DD2_MonkStaffSwing with { Volume = 0.6f, Pitch = 0.2f + comboStage * 0.15f }, target.Center);

            //命中粒子
            for (int i = 0; i < 20 + comboStage * 10; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(8f, 8f);

                PRTLoader.NewParticle<PRT_Light>(target.Center, vel
                    , VaultUtils.MultiStepColorLerp(Main.rand.NextFloat(), Color.OrangeRed, Color.Gold)
                    , Main.rand.NextFloat(0.8f, 1.5f)).Configure(18, opacity: 0.5f, squishStrenght: 1.2f);
            }

            //第三段范围爆炸
            if (comboStage == 2 && Projectile.IsOwnedByLocalPlayer()) {
                SpawnHitExplosion(target.Center);
            }
        }

        //命中爆炸
        private void SpawnHitExplosion(Vector2 position) {
            for (int i = 0; i < 50; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(15f, 15f);

                PRTLoader.NewParticle<PRT_Light>(position, vel
                    , VaultUtils.MultiStepColorLerp(Main.rand.NextFloat(), Color.Red, Color.Orange, Color.Yellow)
                    , Main.rand.NextFloat(1.2f, 2f)).Configure(30, opacity: 0.6f, squishStrenght: 1.5f);
            }

            //额外伤害弹幕
            if (Projectile.IsOwnedByLocalPlayer()) {
                for (int i = 0; i < 8; i++) {
                    float angle = MathHelper.TwoPi * i / 8f;
                    Vector2 vel = angle.ToRotationVector2() * 12f;

                    Projectile.NewProjectile(Projectile.GetSource_FromThis(), position, vel
                        , ModContent.ProjectileType<DawnshatterFireball>(), Projectile.damage / 2, 4f, Projectile.owner);
                }
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D texture = TextureAssets.Projectile[Type].Value;
            float drawRotation = Projectile.rotation + (Owner.direction > 0 ? MathHelper.PiOver4 : MathHelper.PiOver4 * 3);
            Vector2 drawPosition = Projectile.Center - Main.screenPosition - Projectile.velocity.UnitVector() * 70;
            Vector2 origin = VaultUtils.GetOrig(texture, 4);
            SpriteEffects effects = Owner.direction > 0 ? SpriteEffects.None : SpriteEffects.FlipHorizontally;

            //多层发光效果
            for (int i = 0; i < 3; i++) {
                float glowScale = 0.75f + i * 0.05f;
                float glowAlpha = (0.4f - i * 0.1f) * energyIntensity;
                Color glowColor = VaultUtils.MultiStepColorLerp(i / 3f, Color.Red, Color.Orange, Color.Yellow) with { A = 0 };
                glowColor *= glowAlpha;

                Main.EntitySpriteDraw(texture, drawPosition, texture.GetRectangle(Projectile.frame, 4), glowColor
                    , drawRotation, origin, Projectile.scale * glowScale, effects, 0);
            }

            //主体绘制
            Color drawColor = Projectile.GetAlpha(lightColor);
            Main.EntitySpriteDraw(texture, drawPosition, texture.GetRectangle(Projectile.frame, 4), drawColor
                , drawRotation, origin, Projectile.scale * 0.7f, effects, 0);

            //额外的能量光晕
            Color energyColor = new Color(255, 200, 50, 0) * energyIntensity * 0.6f;
            Main.EntitySpriteDraw(texture, drawPosition, texture.GetRectangle(Projectile.frame, 4), energyColor
                , drawRotation, origin, Projectile.scale * 0.78f, effects, 0);

            return false;
        }
    }
}
