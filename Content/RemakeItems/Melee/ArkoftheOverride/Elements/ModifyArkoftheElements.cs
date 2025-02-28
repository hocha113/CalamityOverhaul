using CalamityMod;
using CalamityMod.Buffs.DamageOverTime;
using CalamityMod.Items.Weapons.Melee;
using CalamityMod.Particles;
using CalamityMod.Projectiles.Melee;
using CalamityMod.Sounds;
using CalamityOverhaul.Content.RemakeItems.Core;
using InnoVault.GameContent.BaseEntity;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.IO;
using System.Linq;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using static CalamityMod.CalamityUtils;
using static Terraria.ModLoader.ModContent;

namespace CalamityOverhaul.Content.RemakeItems.Melee.ArkoftheOverride.Elements
{
    internal class ModifyArkoftheElements : ItemOverride
    {
        public override int TargetID => ItemType<ArkoftheElements>();
        public override bool CanLoadLocalization => false;
        public override void SetDefaults(Item item) => item.DamageType = DamageClass.Melee;
        public override bool? On_CanUseItem(Item item, Player player) => player.ownedProjectileCounts[ProjectileType<ModifyElementsSwungHeld>()] == 0;
        public override bool? Shoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source
            , Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            ArkoftheElements arkofthe = item.ModItem as ArkoftheElements;
            if (player.altFunctionUse == 2) {
                if (arkofthe.Charge > 0 && player.controlUp) {
                    float angle = velocity.ToRotation();
                    Projectile.NewProjectile(source, player.Center + angle.ToRotationVector2() * 90f, velocity, ProjectileType<ArkoftheElementsSnapBlast>()
                        , (int)(damage * arkofthe.Charge * ArkoftheElements.chargeDamageMultiplier * ArkoftheElements.blastDamageMultiplier), 0, player.whoAmI);

                    if (Main.LocalPlayer.Calamity().GeneralScreenShakePower < 3)
                        Main.LocalPlayer.Calamity().GeneralScreenShakePower = 3;

                    arkofthe.Charge = 0;
                }
                else if (!Main.projectile.Any(n => n.active && n.owner == player.whoAmI
                && (n.type == ProjectileType<ArkoftheAncientsParryHoldout>()
                || n.type == ProjectileType<TrueArkoftheAncientsParryHoldout>()
                || n.type == ProjectileType<ArkoftheCosmosParryHoldout>()//前面的几个是检测远古方舟的
                || n.type == ProjectileType<ModifyElementsParryHoldout>()))) {
                    Projectile.NewProjectile(source, player.Center, velocity, ProjectileType<ModifyElementsParryHoldout>(), damage, 0, player.whoAmI, 0, 0);
                }

                return false;
            }

            if (arkofthe.Charge > 0)
                damage = (int)(ArkoftheElements.chargeDamageMultiplier * damage);
            float scissorState = arkofthe.Combo == ArkoftheElements.ComboLength ? 2 : arkofthe.Combo % 2;

            Projectile.NewProjectile(source, player.Center, velocity, ProjectileType<ModifyElementsSwungHeld>(), damage, knockback, player.whoAmI, scissorState, arkofthe.Charge);

            arkofthe.Combo += 1;
            if (arkofthe.Combo > ArkoftheElements.ComboLength)
                arkofthe.Combo = 0;

            if (scissorState == 1f) {
                float empoweredNeedles = arkofthe.Charge > 0 ? 1f : 0f;
                Projectile.NewProjectile(source, player.Center + Utils.SafeNormalize(velocity, Vector2.Zero) * 20, velocity * 2.8f, ProjectileType<SolarNeedle>()
                    , (int)(damage * ArkoftheElements.needleDamageMultiplier), knockback, player.whoAmI, empoweredNeedles);


                Vector2 Shift = Utils.SafeNormalize(velocity.RotatedBy(MathHelper.PiOver2), Vector2.Zero) * 20;

                Projectile.NewProjectile(source, player.Center + Shift, velocity.RotatedBy(MathHelper.PiOver4 * 0.3f), ProjectileType<ElementalGlassStar>()
                    , (int)(damage * ArkoftheElements.glassStarDamageMultiplier), knockback, player.whoAmI);
                Projectile.NewProjectile(source, player.Center + Shift * 1.2f, velocity.RotatedBy(MathHelper.PiOver4 * 0.4f) * 0.8f, ProjectileType<ElementalGlassStar>()
                    , (int)(damage * ArkoftheElements.glassStarDamageMultiplier), knockback, player.whoAmI);


                Projectile.NewProjectile(source, player.Center - Shift, velocity.RotatedBy(-MathHelper.PiOver4 * 0.3f), ProjectileType<ElementalGlassStar>()
                    , (int)(damage * ArkoftheElements.glassStarDamageMultiplier), knockback, player.whoAmI);
                Projectile.NewProjectile(source, player.Center - Shift * 1.2f, velocity.RotatedBy(-MathHelper.PiOver4 * 0.4f) * 0.8f, ProjectileType<ElementalGlassStar>()
                    , (int)(damage * ArkoftheElements.glassStarDamageMultiplier), knockback, player.whoAmI);
            }

            arkofthe.Charge--;
            if (arkofthe.Charge < 0)
                arkofthe.Charge = 0;

            return false;
        }
    }

    internal class ModifyElementsParryHoldout : BaseHeldProj
    {
        public override LocalizedText DisplayName => ProjectileLoader.GetProjectile(ProjectileType<ArkoftheElementsParryHoldout>()).DisplayName;
        public override string Texture => ProjectileLoader.GetProjectile(ProjectileType<ArkoftheElementsParryHoldout>()).Texture;
        public Vector2 OwnerConter => Owner.GetPlayerStabilityCenter();
        public float SwingMultiplication => 1 / Owner.GetWeaponAttackSpeed(Item);
        public const float MaxTime = 340;
        public const float ParryTime = 15;
        public Vector2 DistanceFromPlayer => Projectile.velocity * 10 + Projectile.velocity * 10 * ThrustDisplaceRatio();
        public float Timer => MaxTime - Projectile.timeLeft;
        public float ParryProgress => (MaxTime - Projectile.timeLeft) / ParryTime;
        public ref float AlreadyParried => ref Projectile.ai[1];
        public CurveSegment anticipation = new CurveSegment(EasingType.SineBump, 0f, 0.2f, -0.05f);
        public CurveSegment thrust = new CurveSegment(EasingType.PolyInOut, 0.2f, 0.2f, 0.8f, 2);
        public CurveSegment retract = new CurveSegment(EasingType.CircIn, 0.7f, 1f, -0.1f);
        public CurveSegment openMore = new CurveSegment(EasingType.SineBump, 0f, 0f, -0.15f);
        public CurveSegment close = new CurveSegment(EasingType.PolyIn, 0.3f, 0f, 1f, 4);
        public CurveSegment stayClosed = new CurveSegment(EasingType.Linear, 0.5f, 1f, 0f);
        public override void SetDefaults() {
            Projectile.DamageType = DamageClass.MeleeNoSpeed;
            Projectile.width = Projectile.height = 75;
            Projectile.width = Projectile.height = 75;
            Projectile.tileCollide = false;
            Projectile.friendly = true;
            Projectile.penetrate = -1;
            Projectile.noEnchantmentVisuals = true;
        }

        public override bool? CanDamage() => Timer <= ParryTime && AlreadyParried == 0f;

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            float collisionPoint = 0f;
            float bladeLength = 142f * Projectile.scale;
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(), OwnerConter + DistanceFromPlayer
                , OwnerConter + DistanceFromPlayer + (Projectile.velocity * bladeLength), 44, ref collisionPoint);
        }

        public virtual void GeneralParryEffects() {
            if (Item.ModItem is ArkoftheElements sword) {
                sword.Charge = 10f;
                sword.Combo = 0f;
            }

            SoundEngine.PlaySound(SoundID.DD2_WitherBeastCrystalImpact);
            SoundEngine.PlaySound(CommonCalamitySounds.ScissorGuillotineSnapSound
                with { Volume = CommonCalamitySounds.ScissorGuillotineSnapSound.Volume * 1.3f }, Projectile.Center);

            CombatText.NewText(Projectile.Hitbox, new Color(111, 247, 200), GetTextValue("Misc.ArkParry"), true);

            for (int i = 0; i < 5; i++) {
                Vector2 particleDispalce = Main.rand.NextVector2Circular(Owner.Hitbox.Width * 2f, Owner.Hitbox.Height * 1.2f);
                float particleScale = Main.rand.NextFloat(0.5f, 1.4f);
                Particle shine = new FlareShine(OwnerConter + particleDispalce, particleDispalce * 0.01f
                    , Color.White, Color.Red, 0f, new Vector2(0.6f, 1f) * particleScale, new Vector2(1.5f, 2.7f) * particleScale
                    , 20 + Main.rand.Next(6), bloomScale: 3f, spawnDelay: Main.rand.Next(7) * 2);
                GeneralParticleHandler.SpawnParticle(shine);
            }

            AlreadyParried = 1f;
            NetUpdate();
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(BuffType<ElementalMix>(), 90);
            if (AlreadyParried > 0)
                return;

            GeneralParryEffects();

            if (target.damage > 0) {
                int arkParryIFrames = Owner.ComputeParryIFrames();
                Owner.GiveUniversalIFrames(arkParryIFrames, false);
            }

            Vector2 particleOrigin = target.Hitbox.Size().Length() < 140 ? target.Center : Projectile.Center + Projectile.rotation.ToRotationVector2() * 60f;
            Particle spark = new GenericSparkle(particleOrigin, Vector2.Zero, Color.White, Color.HotPink, 1.2f, 35, 0.1f, 2);
            GeneralParticleHandler.SpawnParticle(spark);

            for (int i = 0; i < 10; i++) {
                Vector2 particleSpeed = Main.rand.NextVector2CircularEdge(1, 1) * Main.rand.NextFloat(2.6f, 4f);
                Particle energyLeak = new SquishyLightParticle(particleOrigin, particleSpeed, Main.rand.NextFloat(0.3f, 0.6f), Color.Cyan, 60, 1, 1.5f, hueShift: 0.02f);
                GeneralParticleHandler.SpawnParticle(energyLeak);
            }
        }

        public override void Initialize() {
            Projectile.timeLeft = (int)MaxTime;
            SoundEngine.PlaySound(SoundID.Item84 with { Volume = SoundID.Item84.Volume * 0.3f }, Projectile.Center);
            Projectile.velocity = UnitToMouseV;
            Projectile.rotation = Projectile.velocity.ToRotation();
            NetUpdate();
            Projectile.netSpam = 0;
        }

        public override void AI() {
            // 更新投射物位置和缩放
            Projectile.Center = OwnerConter + DistanceFromPlayer;
            Projectile.scale = 1.4f + ThrustDisplaceRatio() * 0.2f;

            // 如果已经过了反击时间，直接返回
            if (Timer > ParryTime) {
                return;
            }

            // 检查与敌方投射物的碰撞
            CheckProjectileCollision();

            // 设置持有状态
            SetHeld();

            // 控制玩家方向与旋转
            UpdateOwnerDirectionAndRotation();

            // 如果已经进行过反击，增加反击计数
            if (AlreadyParried > 0) {
                AlreadyParried++;
                NetUpdate();
            }
        }

        // 检查与敌方投射物的碰撞
        private void CheckProjectileCollision() {
            float collisionPoint = 0f;
            float bladeLength = 142f * Projectile.scale;

            for (int k = 0; k < Main.maxProjectiles; k++) {
                Projectile proj = Main.projectile[k];

                // 如果投射物不满足条件，跳过
                if (!proj.active || !proj.hostile || proj.damage <= 1 || proj.velocity.Length() * (proj.extraUpdates + 1) <= 1f ||
                    proj.Size.Length() >= 300) {
                    continue;
                }

                // 检查碰撞
                if (Collision.CheckAABBvLineCollision(proj.Hitbox.TopLeft(), proj.Hitbox.Size(),
                    OwnerConter + DistanceFromPlayer, OwnerConter + DistanceFromPlayer + (Projectile.velocity * bladeLength),
                    24, ref collisionPoint)) {

                    // 触发反击效果
                    if (AlreadyParried == 0) {
                        GeneralParryEffects();
                        ApplyKnockback(proj);
                    }

                    // 设置投射物的伤害减免
                    ApplyDamageReduction(proj);

                    break;
                }
            }
        }

        // 反击时，应用击退效果
        private void ApplyKnockback(Projectile proj) {
            if (Owner.velocity.Y != 0) {
                Owner.velocity += Utils.SafeNormalize(OwnerConter - proj.Center, Vector2.Zero) * 2;
            }
        }

        // 应用伤害减免
        private void ApplyDamageReduction(Projectile proj) {
            var calamity = proj.Calamity();
            if (calamity.flatDR < 100) {
                calamity.flatDR = 100;
            }
            if (calamity.flatDRTimer < 60) {
                calamity.flatDRTimer = 60;
            }
        }

        // 更新玩家的方向和旋转
        private void UpdateOwnerDirectionAndRotation() {
            Owner.ChangeDir(Math.Sign(Projectile.velocity.X));
            Owner.itemRotation = Projectile.rotation;

            if (Owner.direction != 1) {
                Owner.itemRotation -= MathHelper.Pi;
            }

            Owner.itemRotation = MathHelper.WrapAngle(Owner.itemRotation);
        }

        internal float ThrustDisplaceRatio() => PiecewiseAnimation(ParryProgress, [anticipation, thrust, retract]);
        internal float RotationRatio() => PiecewiseAnimation(ParryProgress, [openMore, close, stayClosed]);

        public override bool PreDraw(ref Color lightColor) {
            // 绘制生命条背景和前景
            if (Timer > ParryTime) {
                DrawParryBar(lightColor);
                return false;
            }

            // 绘制剪刀刀片
            DrawScissorBlades(lightColor);

            return false;
        }

        // 绘制生命条背景和前景
        protected void DrawParryBar(Color lightColor) {
            var barBG = CWRAsset.GenericBarBack.Value;
            var barFG = CWRAsset.GenericBarFront.Value;
            Vector2 drawPos = OwnerConter - Main.screenPosition + new Vector2(0, -36) - barBG.Size() / 2;
            Rectangle frame = new Rectangle(0, 0, (int)((Timer - ParryTime) / (MaxTime - ParryTime) * barFG.Width), barFG.Height);
            float opacity = Timer <= ParryTime + 25f ? (Timer - ParryTime) / 25f : (MaxTime - Timer <= 8) ? Projectile.timeLeft / 8f : 1f;
            Color color = Main.hslToRgb((float)Math.Sin(Main.GlobalTimeWrappedHourly * 1.2f) * 0.05f + 0.08f, 1, 0.65f + (float)Math.Sin(Main.GlobalTimeWrappedHourly * 7f) * 0.1f);

            Main.spriteBatch.Draw(barBG, drawPos, color * opacity);
            Main.spriteBatch.Draw(barFG, drawPos, frame, color * opacity * 0.8f);
        }

        // 绘制剪刀刀片
        protected virtual void DrawScissorBlades(Color lightColor) {
            Texture2D frontBlade = ArkoftheAsset.RendingScissorsRight.Value;
            Texture2D frontBladeGlow = ArkoftheAsset.RendingScissorsRightGlow.Value;
            Texture2D backBlade = ArkoftheAsset.RendingScissorsLeft.Value;
            Texture2D backBladeGlow = ArkoftheAsset.RendingScissorsLeftGlow.Value;

            // 计算旋转角度
            float snippingRotation = Projectile.rotation + MathHelper.PiOver4;
            float snippingRotationBack = Projectile.rotation + MathHelper.PiOver4 * 1.75f;

            float drawRotation = MathHelper.Lerp(snippingRotation + MathHelper.PiOver4, snippingRotation, RotationRatio());
            float drawRotationBack = MathHelper.Lerp(snippingRotationBack - MathHelper.PiOver4, snippingRotationBack, RotationRatio());

            // 计算绘制原点
            Vector2 drawOrigin = new Vector2(51, 86);
            Vector2 drawOriginBack = new Vector2(22, 109);

            // 计算绘制位置
            Vector2 drawPosition = OwnerConter + Projectile.velocity * 15 + Projectile.velocity * ThrustDisplaceRatio() * 50f;

            // 绘制刀片和光晕
            ModifyElementsSwungHeld.DrawScissorBlade(backBlade, backBladeGlow, drawPosition, drawOriginBack, drawRotationBack, lightColor, Projectile.scale);
            ModifyElementsSwungHeld.DrawScissorBlade(frontBlade, frontBladeGlow, drawPosition, drawOrigin, drawRotation, lightColor, Projectile.scale);
        }

        public override void OnKill(int timeLeft) => SoundEngine.PlaySound(SoundID.Item35 with { Volume = SoundID.Item35.Volume * 2f });
    }

    internal class ModifyElementsSwungHeld : BaseHeldProj
    {
        public override LocalizedText DisplayName => ProjectileLoader.GetProjectile(ProjectileType<ArkoftheElementsSwungBlade>()).DisplayName;
        public override string Texture => ProjectileLoader.GetProjectile(ProjectileType<ArkoftheElementsSwungBlade>()).Texture;
        public virtual int TargetItem => ItemType<ArkoftheElements>();
        public Vector2 OwnerConter => Owner.GetPlayerStabilityCenter();
        public float SwingMultiplication => 1 / Owner.GetWeaponAttackSpeed(Item);
        public Vector2 direction = Vector2.Zero;
        public ref float Combo => ref Projectile.ai[0];
        public ref float Charge => ref Projectile.ai[1];
        public virtual float MaxSwingTime => 35 * SwingMultiplication;
        public const float SwingWidth = MathHelper.PiOver2 * 1.5f;
        public Vector2 DistanceFromPlayer => direction * 30;
        public float SwingTimer => MaxSwingTime - Projectile.timeLeft / SwingMultiplication;
        public float SwingCompletion => SwingTimer / MaxSwingTime;
        public bool OwnerCanShoot => !Owner.CantUseHoldout() && Item.type == TargetItem;
        public bool Thrown => Combo == 2 || Combo == 3;
        public virtual float MaxThrowTime => 80;
        public virtual float ThrowReachMax => 500;
        public virtual float ThrowReachMin => 200;
        public float ThrowReach;
        public float ThrowTimer => MaxThrowTime - Projectile.timeLeft;
        public float ThrowCompletion => ThrowTimer / MaxThrowTime;
        public virtual float SnapWindowStart => 0.25f;
        public float SnapWindowEnd = 0.75f;
        public float SnapEndTime => MaxThrowTime - (MaxThrowTime * SnapWindowEnd);
        public float SnapEndCompletion => (SnapEndTime - Projectile.timeLeft) / SnapEndTime;
        public ref float ChanceMissed => ref Projectile.localAI[1];
        public CurveSegment anticipation;
        public CurveSegment thrust;
        public CurveSegment hold;
        public CurveSegment shoot;
        public CurveSegment remain;
        public CurveSegment goback;
        public CurveSegment sizeCurve;
        public int SwingDirection {
            get {
                return Combo switch {
                    0 => 1 * Math.Sign(direction.X),
                    1 => -1 * Math.Sign(direction.X),
                    _ => 0,
                };
            }
        }
        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 10;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults() {
            Projectile.DamageType = GetInstance<TrueMeleeDamageClass>();
            Projectile.width = Projectile.height = 60;
            Projectile.width = Projectile.height = 60;
            Projectile.tileCollide = false;
            Projectile.friendly = true;
            Projectile.penetrate = -1;
            Projectile.extraUpdates = 1;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = Thrown ? 10 : (int)MaxSwingTime;
            anticipation = new CurveSegment(EasingType.ExpOut, 0f, 0f, 0.15f);
            thrust = new CurveSegment(EasingType.PolyInOut, 0.1f, 0.15f, 0.85f, 3);
            hold = new CurveSegment(EasingType.Linear, 0.5f, 1f, 0.2f);
            shoot = new CurveSegment(EasingType.CircOut, 0f, 0f, 1f);
            remain = new CurveSegment(EasingType.Linear, SnapWindowStart, 1f, 0f);
            goback = new CurveSegment(EasingType.CircIn, SnapWindowEnd, 1f, -1f);
            sizeCurve = new CurveSegment(EasingType.SineBump, 0f, 0f, 1f);
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            float bladeLength = 142f * Projectile.scale;

            if (Thrown) {
                bool mainCollision = Collision.CheckAABBvAABBCollision(targetHitbox.TopLeft(), targetHitbox.Size()
                    , Projectile.Center - Vector2.One * bladeLength / 2f, Vector2.One * bladeLength);
                if (Combo == 2f) {
                    return mainCollision;
                }

                else {
                    Vector2 thrownBladeStart = Vector2.SmoothStep(OwnerConter, Projectile.Center, MathHelper.Clamp(SnapEndCompletion + 0.25f, 0f, 1f));
                    bool thrownScissorCollision = Collision.CheckAABBvLineCollision(targetHitbox.TopLeft()
                        , targetHitbox.Size(), thrownBladeStart, thrownBladeStart + direction * bladeLength);
                    return mainCollision || thrownScissorCollision;
                }
            }

            float collisionPoint = 0f;
            Vector2 holdPoint = DistanceFromPlayer.Length() * Projectile.rotation.ToRotationVector2();

            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(), OwnerConter + holdPoint
                , OwnerConter + holdPoint + Projectile.rotation.ToRotationVector2() * bladeLength, 24, ref collisionPoint);
        }

        internal float SwingRatio() => PiecewiseAnimation(SwingCompletion, [anticipation, thrust, hold]);
        internal float ThrowRatio() => PiecewiseAnimation(ThrowCompletion, [shoot, remain, goback]);
        internal float ThrowScaleRatio() => PiecewiseAnimation(ThrowCompletion, [sizeCurve]);

        public override void Initialize() {
            Projectile.timeLeft = Thrown ? (int)MaxThrowTime : (int)MaxSwingTime;
            SoundStyle sound = (Charge > 0 || Thrown) ? CommonCalamitySounds.LouderPhantomPhoenix : SoundID.Item71;
            SoundEngine.PlaySound(sound, Projectile.Center);
            direction = Projectile.velocity;
            direction.Normalize();
            Projectile.rotation = direction.ToRotation();

            if (Thrown) {
                ThrowReach = MathHelper.Clamp(ToMouse.Length(), ThrowReachMin, ThrowReachMax);
            }

            Projectile.netUpdate = true;
            Projectile.netSpam = 0;
        }

        public override void AI() {
            // 如果当前没有投掷
            if (!Thrown) {
                // 设置Projectile的中心位置为玩家中心加上偏移量
                SetProjectilePosition();

                // 计算Projectile的旋转角度
                UpdateProjectileRotation();

                // 计算Projectile的缩放
                UpdateProjectileScale();
            }
            else {
                // 计算并生成火花特效
                CreateSparkleEffect();

                // 在特定条件下生成脉冲特效并播放音效
                HandlePulseEffect();

                // 设置投掷物的位置和旋转
                UpdateProjectileThrow();

                // 处理Combo为2时的miss机会
                HandleMissedChance();

                // 如果Combo为3，进行投掷物轨迹的平滑过渡
                HandleCombo3();
            }

            // 设置玩家的朝向和旋转
            UpdatePlayerRotation();
        }

        private void SetProjectilePosition() {
            Projectile.Center = OwnerConter + DistanceFromPlayer;
        }

        private void UpdateProjectileRotation() {
            Projectile.rotation = Projectile.velocity.ToRotation() +
                                  MathHelper.Lerp(SwingWidth / 2 * SwingDirection, -SwingWidth / 2 * SwingDirection, SwingRatio()) -
                                  (Combo == 1 ? MathHelper.PiOver4 : 0f);
        }

        private void UpdateProjectileScale() {
            Projectile.scale = 1.2f + ((float)Math.Sin(SwingRatio() * MathHelper.Pi) * 0.6f) + (Charge / 10f) * 0.2f;
        }

        // 创建火花特效
        private void CreateSparkleEffect() {
            Vector2 sparklePosition = Projectile.Center + Projectile.rotation.ToRotationVector2() * 90 * Projectile.scale +
                                      (Projectile.rotation - MathHelper.PiOver2).ToRotationVector2() * 20 * Projectile.scale;
            Particle sparkle = new CritSpark(sparklePosition, Projectile.rotation.ToRotationVector2() * 7f, Color.White, Color.OrangeRed,
                                             Main.rand.NextFloat(1f, 2f), 10 + Main.rand.Next(10), 0.1f, 3f, Main.rand.NextFloat(0f, 0.01f));
            GeneralParticleHandler.SpawnParticle(sparkle);
        }

        // 处理脉冲特效
        private void HandlePulseEffect() {
            if (Math.Abs(ThrowCompletion - SnapWindowStart + 0.1f) <= 0.005f && ChanceMissed == 0f && Main.myPlayer == Owner.whoAmI) {
                Particle pulse = new PulseRing(Projectile.Center, Vector2.Zero, Color.OrangeRed, 0.05f, 1.8f, 8);
                GeneralParticleHandler.SpawnParticle(pulse);
                SoundEngine.PlaySound(SoundID.Item4);
            }
        }

        // 更新投掷物的位置和旋转
        private void UpdateProjectileThrow() {
            Projectile.Center = OwnerConter + direction * ThrowRatio() * ThrowReach;
            Projectile.rotation -= MathHelper.PiOver4 * 0.3f;
            Projectile.scale = 1f + ThrowScaleRatio() * 0.5f;
        }

        // 处理Combo为2时的miss机会
        private void HandleMissedChance() {
            if (!OwnerCanShoot && Combo == 2 && ThrowCompletion >= (SnapWindowStart - 0.1f) && ThrowCompletion < SnapWindowEnd && ChanceMissed == 0f) {
                Particle snapSpark = new GenericSparkle(Projectile.Center, Owner.velocity
                    - Utils.SafeNormalize(Projectile.velocity, Vector2.Zero), Color.White, Color.OrangeRed,
                    Main.rand.NextFloat(1f, 2f), 10 + Main.rand.Next(10), 0.1f, 3f);
                GeneralParticleHandler.SpawnParticle(snapSpark);

                // 增加屏幕震动效果
                if (Main.LocalPlayer.Calamity().GeneralScreenShakePower < 3) {
                    Main.LocalPlayer.Calamity().GeneralScreenShakePower = 3;
                }

                // 清除对NPC的免疫
                for (int i = 0; i < Main.maxNPCs; ++i) {
                    Projectile.localNPCImmunity[i] = 0;
                }

                // 设置Combo为3并更新投掷物的属性
                Combo = 3f;
                Projectile.velocity = Projectile.rotation.ToRotationVector2();
                Projectile.timeLeft = (int)SnapEndTime;
                Projectile.localNPCHitCooldown = (int)SnapEndTime;
            }
            else if (!OwnerCanShoot && Combo == 2 && ChanceMissed == 0f) {
                ChanceMissed = 1f;
            }
        }

        // 处理Combo为3时的投掷物轨迹过渡
        private void HandleCombo3() {
            if (Combo == 3f) {
                float curveDownGently = MathHelper.Lerp(1f, 0.8f, 1f - (float)Math.Sqrt(1f - (float)Math.Pow(SnapEndCompletion, 2f)));
                Projectile.Center = OwnerConter + direction * ThrowReach * curveDownGently;
                Projectile.scale = 1.5f;

                float orientateProperly = (float)Math.Sqrt(1f - (float)Math.Pow(MathHelper.Clamp(SnapEndCompletion + 0.2f, 0f, 1f) - 1f, 2f));

                float extraRotations = (direction.ToRotation() + MathHelper.PiOver4 > Projectile.velocity.ToRotation()) ? -MathHelper.TwoPi : 0f;

                Projectile.rotation = MathHelper.Lerp(Projectile.velocity.ToRotation(), direction.ToRotation() + MathHelper.PiOver4 * 0.2f + extraRotations, orientateProperly);
            }
        }

        // 更新玩家的朝向和旋转
        private void UpdatePlayerRotation() {
            SetHeld();
            Owner.ChangeDir(Math.Sign(Projectile.velocity.X));
            Owner.itemRotation = Projectile.rotation;
            if (Owner.direction != 1) {
                Owner.itemRotation -= MathHelper.Pi;
            }
            Owner.itemRotation = MathHelper.WrapAngle(Owner.itemRotation);
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {
            if (Combo == 3f) {
                modifiers.SourceDamage *= ArkoftheElements.snapDamageMultiplier;
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(BuffType<ElementalMix>(), 60);
            for (int i = 0; i < 5; i++) {
                Vector2 particleSpeed = Utils.SafeNormalize(target.Center - Projectile.Center, Vector2.One).RotatedByRandom(MathHelper.PiOver4 * 0.8f) * Main.rand.NextFloat(3.6f, 8f);
                Particle energyLeak = new SquishyLightParticle(target.Center, particleSpeed, Main.rand.NextFloat(0.3f, 0.6f), Color.OrangeRed, 60, 2, 2.5f, hueShift: 0.06f);
                GeneralParticleHandler.SpawnParticle(energyLeak);
            }

            if (Combo == 3f)
                SoundEngine.PlaySound(CommonCalamitySounds.ScissorGuillotineSnapSound with { Volume = CommonCalamitySounds.ScissorGuillotineSnapSound.Volume * 1.3f }, Projectile.Center);
        }

        public override void OnKill(int timeLeft) {
            if (Combo == 3f) {
                if (Main.LocalPlayer.Calamity().GeneralScreenShakePower < 3)
                    Main.LocalPlayer.Calamity().GeneralScreenShakePower = 3;

                SoundEngine.PlaySound(SoundID.Item84, Projectile.Center);

                Vector2 sliceDirection = direction * 40;
                Particle SliceLine = new LineVFX(Projectile.Center - sliceDirection, sliceDirection * 2f, 0.2f, Color.Orange * 0.7f, expansion: 250f) {
                    Lifetime = 10
                };
                GeneralParticleHandler.SpawnParticle(SliceLine);

            }
        }

        public override bool PreDraw(ref Color lightColor) {
            if (!Thrown) {
                if (Charge > 0)
                    DrawSwungScissors(lightColor);
                else
                    DrawSingleSwungScissorBlade(lightColor);
            }
            else {
                if (Charge > 0)
                    DrawThrownScissors(lightColor);
                else
                    DrawSingleThrownScissorBlade(lightColor);
            }
            return false;
        }

        internal static void DrawBladeWithGlow(Texture2D blade, Texture2D glow, Vector2 position, Vector2 origin, float rotation, SpriteEffects flip, Color lightColor, float scale, float opacityFactor = 1f) {
            Main.EntitySpriteDraw(blade, position, null, lightColor, rotation, origin, scale, flip, 0);
            Main.EntitySpriteDraw(glow, position, null, Color.Lerp(lightColor, Color.White, 0.75f), rotation, origin, scale, flip, 0);
        }

        // 处理绘制单个投掷剪刀的通用方法
        internal static void DrawScissorBlade(Texture2D bladeTexture, Texture2D glowMask, Vector2 position, Vector2 origin, float rotation, Color lightColor, float scale, SpriteEffects flip = SpriteEffects.None) {
            Main.EntitySpriteDraw(bladeTexture, position - Main.screenPosition, null, lightColor, rotation, origin, scale, flip, 0);
            Main.EntitySpriteDraw(glowMask, position - Main.screenPosition, null, Color.Lerp(lightColor, Color.White, 0.75f), rotation, origin, scale, flip, 0);
        }

        private void DrawAfterimages(Texture2D glowmask, Vector2 position, Vector2 origin, float angleShift, float extraAngle, SpriteEffects flip, Color lightColor, float scale) {
            if (CalamityConfig.Instance.Afterimages && SwingTimer > ProjectileID.Sets.TrailCacheLength[Projectile.type]) {
                for (int i = 0; i < Projectile.oldRot.Length; ++i) {
                    Color color = Main.hslToRgb((i / (float)Projectile.oldRot.Length) * 0.1f, 1, 0.6f + (Charge > 0 ? 0.3f : 0f));
                    float afterimageRotation = Projectile.oldRot[i] + angleShift + extraAngle;

                    Main.EntitySpriteDraw(glowmask, position, null, color * 0.15f, afterimageRotation, origin, scale - 0.2f * ((i / (float)Projectile.oldRot.Length)), flip, 0);
                }
            }
        }

        private void DrawSingleScissorBlade(Color lightColor, Texture2D sword, Texture2D glowmask, Vector2 drawOrigin, Vector2 drawOffset, float drawRotation, float angleShift, float extraAngle, SpriteEffects flip) {
            // 绘制 afterimages（如果有的话）
            DrawAfterimages(glowmask, drawOffset, drawOrigin, angleShift, extraAngle, flip, lightColor, Projectile.scale);

            // 绘制刀刃本身和光晕效果
            DrawBladeWithGlow(sword, glowmask, drawOffset, drawOrigin, drawRotation, flip, lightColor, Projectile.scale);
        }

        private void DrawSmearEffect(float opacity) {
            Texture2D smear = ArkoftheAsset.TrientCircularSmear.Value;

            Main.spriteBatch.End();
            Main.spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.Additive, Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);

            float rotation = (-MathHelper.PiOver4 * 0.5f + MathHelper.PiOver4 * 0.5f * SwingCompletion + (Combo == 1f ? MathHelper.PiOver4 : 0)) * SwingDirection;
            Color smearColor = Main.hslToRgb(((SwingTimer - MaxSwingTime * 0.5f) / (MaxSwingTime * 0.5f)) * 0.15f + ((Combo == 1f) ? 0.85f : 0f), 1, 0.6f);

            Main.EntitySpriteDraw(smear, OwnerConter - Main.screenPosition, null, smearColor * 0.5f * opacity, Projectile.velocity.ToRotation() + MathHelper.Pi + rotation, smear.Size() / 2f, Projectile.scale * 2.3f, SpriteEffects.None, 0);

            Main.spriteBatch.End();
            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
        }

        public virtual void DrawSingleSwungScissorBlade(Color lightColor) {
            // 确定刀刃纹理和光效纹理
            Texture2D sword = Combo == 0 ? ArkoftheAsset.RendingScissorsRight.Value : ArkoftheAsset.RendingScissorsLeft.Value;
            Texture2D glowmask = Combo == 0 ? ArkoftheAsset.RendingScissorsRightGlow.Value : ArkoftheAsset.RendingScissorsLeftGlow.Value;

            // 计算翻转和角度
            bool flipped = Owner.direction < 0;
            SpriteEffects flip = flipped ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
            float extraAngle = flipped ? MathHelper.PiOver2 : 0f;
            float drawAngle = Projectile.rotation;
            float angleShift = Combo == 0 ? MathHelper.PiOver4 : MathHelper.PiOver2;
            float drawRotation = Projectile.rotation + angleShift + extraAngle;

            // 计算原点和偏移
            Vector2 drawOrigin = new Vector2(Combo == 1 ? sword.Width / 2f : flipped ? sword.Width : 0f, sword.Height);
            Vector2 drawOffset = OwnerConter + drawAngle.ToRotationVector2() * 10f - Main.screenPosition;

            // 绘制单个刀刃
            DrawSingleScissorBlade(lightColor, sword, glowmask, drawOrigin, drawOffset, drawRotation, angleShift, extraAngle, flip);

            // 如果摆动完成度超过50%，绘制smear效果
            if (SwingCompletion > 0.5f) {
                float opacity = (float)Math.Sin(SwingCompletion * MathHelper.Pi);
                DrawSmearEffect(opacity);
            }
        }

        public virtual void DrawSwungScissors(Color lightColor) {

            // 确定前后刀刃纹理
            Texture2D frontBlade = ArkoftheAsset.RendingScissorsRight.Value;
            Texture2D frontBladeGlow = ArkoftheAsset.RendingScissorsRightGlow.Value;
            Texture2D backBlade = ArkoftheAsset.RendingScissorsLeft.Value;
            Texture2D backBladeGlow = ArkoftheAsset.RendingScissorsLeftGlow.Value;

            bool flipped = Owner.direction < 0;
            SpriteEffects flip = flipped ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
            float extraAngle = flipped ? MathHelper.PiOver2 : 0f;
            float drawAngle = Projectile.rotation;
            float angleShift = (Combo == 0 || !flipped) ? MathHelper.PiOver4 : MathHelper.PiOver2 * 1.5f;
            float drawRotation = Projectile.rotation + angleShift + extraAngle;
            float functionalDrawAngle = drawAngle + (Combo == 1f && flipped ? MathHelper.PiOver2 : 0f);
            Vector2 drawOrigin = new Vector2(flipped ? frontBlade.Width : 0f, frontBlade.Height);
            Vector2 drawOffset = OwnerConter + drawAngle.ToRotationVector2() * 10f - Main.screenPosition;
            Vector2 backScissorOrigin = new Vector2(flipped ? 11 : 20f, 109);
            Vector2 backScissorDrawPosition = OwnerConter + drawAngle.ToRotationVector2() * 10f
                + functionalDrawAngle.ToRotationVector2() * 70f * Projectile.scale - Main.screenPosition;
            float backScissorRotation = drawRotation + (Combo == 1 ? (!flipped ? MathHelper.PiOver4 * 0.75f : MathHelper.PiOver4 * -0.75f) : 0f);

            // 绘制前后刀刃（带光效）
            DrawBladeWithGlow(backBlade, backBladeGlow, backScissorDrawPosition, backScissorOrigin, backScissorRotation, flip, lightColor, Projectile.scale);
            DrawBladeWithGlow(frontBlade, frontBladeGlow, drawOffset, drawOrigin, drawRotation, flip, lightColor, Projectile.scale);

            // 如果摆动完成度超过50%，绘制smear效果
            if (SwingCompletion > 0.5f) {
                float opacity = (float)Math.Sin(SwingCompletion * MathHelper.Pi);
                DrawSmearEffect(opacity);
            }
        }

        // 绘制单个投掷剪刀刀片
        public virtual void DrawSingleThrownScissorBlade(Color lightColor) {
            // 判断是否有不同的刀片样式
            Texture2D sword = ArkoftheAsset.RendingScissorsRight.Value;
            Texture2D glowmask = ArkoftheAsset.RendingScissorsRightGlow.Value;
            if (Combo == 3f) {
                sword = ArkoftheAsset.RendingScissorsLeft.Value;
                glowmask = ArkoftheAsset.RendingScissorsLeftGlow.Value;
            }

            // 计算绘制位置和角度
            Vector2 drawPos = Combo == 3f ? Vector2.SmoothStep(OwnerConter, Projectile.Center, MathHelper.Clamp(SnapEndCompletion + 0.25f, 0f, 1f)) : Projectile.Center;
            Vector2 drawOrigin = Combo == 3f ? new Vector2(22, 109) : new Vector2(51, 86);
            float drawRotation = Combo == 3f ? direction.ToRotation() + MathHelper.PiOver2 : Projectile.rotation + MathHelper.PiOver4;

            // 绘制刀片和光晕
            DrawScissorBlade(sword, glowmask, drawPos, drawOrigin, drawRotation, lightColor, Projectile.scale);

            // 绘制拖尾效果
            if (Combo == 3f || ThrowCompletion > 0.5f) {
                Texture2D smear = ArkoftheAsset.TrientCircularSmear.Value;
                Main.spriteBatch.End();
                Main.spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.Additive, Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);

                float opacity = Combo == 3f ? (float)Math.Sin(SnapEndCompletion * MathHelper.PiOver2 + MathHelper.PiOver2) : (float)Math.Sin(ThrowCompletion * MathHelper.Pi);
                float rotation = drawRotation + MathHelper.PiOver2 * 1.5f;
                Color smearColor = Main.hslToRgb(((SwingTimer - MaxSwingTime * 0.5f) / (MaxSwingTime * 0.5f)) * 0.15f, 1, 0.6f);

                Main.EntitySpriteDraw(smear, Projectile.Center - Main.screenPosition, null, smearColor * 0.5f * opacity, rotation, smear.Size() / 2f, Projectile.scale * 1.4f, SpriteEffects.None, 0);

                Main.spriteBatch.End();
                Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
            }
        }

        // 绘制投掷剪刀的通用方法
        public virtual void DrawThrownScissors(Color lightColor) {
            Texture2D frontBlade = ArkoftheAsset.RendingScissorsRight.Value;
            Texture2D frontBladeGlow = ArkoftheAsset.RendingScissorsRightGlow.Value;
            Texture2D backBlade = ArkoftheAsset.RendingScissorsLeft.Value;
            Texture2D backBladeGlow = ArkoftheAsset.RendingScissorsLeftGlow.Value;

            // 计算前后刀片绘制的位置和角度
            Vector2 drawPos = Projectile.Center;
            Vector2 frontOrigin = new Vector2(51, 86);
            float frontRotation = Projectile.rotation + MathHelper.PiOver4;

            Vector2 backOrigin = new Vector2(22, 109);
            float backRotation = Projectile.rotation + MathHelper.Lerp(0f, -MathHelper.PiOver4 * 0.33f, MathHelper.Clamp(ThrowCompletion * 2f, 0f, 1f));

            if (Combo == 3f) {
                backRotation = Projectile.rotation + MathHelper.Lerp(-MathHelper.PiOver4 * 0.33f, MathHelper.PiOver2 * 0.85f, MathHelper.Clamp(SnapEndCompletion + 0.5f, 0f, 1f));
            }

            // 绘制前后刀片及光晕
            DrawScissorBlade(backBlade, backBladeGlow, drawPos, backOrigin, backRotation, lightColor, Projectile.scale);
            DrawScissorBlade(frontBlade, frontBladeGlow, drawPos, frontOrigin, frontRotation, lightColor, Projectile.scale);

            // 绘制拖尾效果
            if (Combo == 3f || ThrowCompletion > 0.5f) {
                Texture2D smear = ArkoftheAsset.TrientCircularSmear.Value;
                Main.spriteBatch.End();
                Main.spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.Additive, Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);

                float opacity = Combo == 3f ? (float)Math.Sin(SnapEndCompletion * MathHelper.PiOver2 + MathHelper.PiOver2) : (float)Math.Sin(ThrowCompletion * MathHelper.Pi);
                float rotation = frontRotation + MathHelper.PiOver2 * 1.5f;
                Color smearColor = Main.hslToRgb(((SwingTimer - MaxSwingTime * 0.5f) / (MaxSwingTime * 0.5f)) * 0.15f, 1, 0.6f);

                Main.EntitySpriteDraw(smear, Projectile.Center - Main.screenPosition, null, smearColor * 0.5f * opacity, rotation, smear.Size() / 2f, Projectile.scale * 1.4f, SpriteEffects.None, 0);

                Main.spriteBatch.End();
                Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
            }
        }

        public override void NetHeldSend(BinaryWriter writer) {
            writer.WriteVector2(direction);
            writer.Write(ChanceMissed);
            writer.Write(ThrowReach);
        }

        public override void NetHeldReceive(BinaryReader reader) {
            direction = reader.ReadVector2();
            ChanceMissed = reader.ReadSingle();
            ThrowReach = reader.ReadSingle();
        }
    }
}
