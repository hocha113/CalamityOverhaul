using CalamityMod;
using CalamityMod.Items.Weapons.Melee;
using CalamityMod.Particles;
using CalamityMod.Projectiles.Melee;
using CalamityMod.Sounds;
using CalamityOverhaul.Content.RemakeItems.Melee.ArkoftheOverride.Elements;
using InnoVault.GameContent.BaseEntity;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using static CalamityMod.CalamityUtils;
using static Terraria.ModLoader.ModContent;

namespace CalamityOverhaul.Content.RemakeItems.Melee.ArkoftheOverride.Cosmos
{
    internal class ModifyArkoftheCosmos : CWRItemOverride
    {
        public override int TargetID => ItemType<ArkoftheCosmos>();
        public override bool CanLoadLocalization => false;
        public override void SetDefaults(Item item) => item.DamageType = DamageClass.Melee;
        public override bool? On_CanUseItem(Item item, Player player) => player.ownedProjectileCounts[ProjectileType<ModifyCosmosSwungBladeHeld>()] == 0;
        public override bool? Shoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source
            , Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            ArkoftheCosmos arkofthe = item.ModItem as ArkoftheCosmos;
            if (player.altFunctionUse == 2) {
                if (arkofthe.Charge > 0 && player.controlUp) {
                    float angle = velocity.ToRotation();
                    Projectile.NewProjectile(source, player.Center + angle.ToRotationVector2() * 90f, velocity, ProjectileType<ArkoftheCosmosBlast>()
                        , (int)(damage * arkofthe.Charge * ArkoftheCosmos.chargeDamageMultiplier * ArkoftheCosmos.blastDamageMultiplier), 0, player.whoAmI, arkofthe.Charge);

                    if (Main.LocalPlayer.Calamity().GeneralScreenShakePower < 3)
                        Main.LocalPlayer.Calamity().GeneralScreenShakePower = 3;

                    arkofthe.Charge = 0;
                }
                else if (!Main.projectile.Any(n => n.active && n.owner == player.whoAmI
                && (n.type == ProjectileType<ArkoftheAncientsParryHoldout>()
                || n.type == ProjectileType<TrueArkoftheAncientsParryHoldout>()
                || n.type == ProjectileType<ArkoftheElementsParryHoldout>()
                || n.type == ProjectileType<ModifyCosmosParryHoldout>())))
                    Projectile.NewProjectile(source, player.Center, velocity
                        , ProjectileType<ModifyCosmosParryHoldout>(), damage, 0, player.whoAmI, 0, 0);

                return false;
            }

            if (arkofthe.Charge > 0)
                damage = (int)(ArkoftheCosmos.chargeDamageMultiplier * damage);

            float scissorState = arkofthe.Combo == 4 ? 2 : arkofthe.Combo % 2;

            Projectile.NewProjectile(source, player.Center, velocity, ProjectileType<ModifyCosmosSwungBladeHeld>()
                , damage, knockback, player.whoAmI, scissorState, arkofthe.Charge);

            if (scissorState != 2) {
                Projectile.NewProjectile(source, player.Center + Utils.SafeNormalize(velocity, Vector2.Zero) * 20, velocity * 1.4f
                    , ProjectileType<RendingNeedle>(), (int)(damage * ArkoftheCosmos.NeedleDamageMultiplier), knockback, player.whoAmI);
            }

            arkofthe.Combo += 1;
            if (arkofthe.Combo > 4)
                arkofthe.Combo = 0;

            arkofthe.Charge--;
            if (arkofthe.Charge < 0)
                arkofthe.Charge = 0;

            return false;
        }
    }

    internal class ModifyCosmosParryHoldout : ModifyElementsParryHoldout
    {
        public override void GeneralParryEffects() {
            ArkoftheCosmos sword = (Owner.HeldItem.ModItem as ArkoftheCosmos);
            if (sword != null) {
                sword.Charge = ArkoftheCosmos.MaxCharge;
                sword.Combo = 0f;
            }
            SoundEngine.PlaySound(SoundID.DD2_WitherBeastCrystalImpact);
            SoundEngine.PlaySound(CommonCalamitySounds.ScissorGuillotineSnapSound
                with { Volume = CommonCalamitySounds.ScissorGuillotineSnapSound.Volume * 1.3f }, Projectile.Center);

            CombatText.NewText(Projectile.Hitbox, new Color(111, 247, 200), GetTextValue("Misc.ArkParry"), true);

            for (int i = 0; i < 5; i++) {
                Vector2 particleDispalce = Main.rand.NextVector2Circular(Owner.Hitbox.Width * 2f, Owner.Hitbox.Height * 1.2f);
                float particleScale = Main.rand.NextFloat(0.5f, 1.4f);
                Particle shine = new FlareShine(OwnerConter + particleDispalce, particleDispalce * 0.01f, Color.White, Color.Red, 0f, new Vector2(0.6f, 1f) * particleScale, new Vector2(1.5f, 2.7f) * particleScale, 20 + Main.rand.Next(6), bloomScale: 3f, spawnDelay: Main.rand.Next(7) * 2);
                GeneralParticleHandler.SpawnParticle(shine);
            }

            AlreadyParried = 1f;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
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
            Projectile.netUpdate = true;
            Projectile.netSpam = 0;
        }

        protected override void DrawScissorBlades(Color lightColor) {
            Texture2D frontBlade = ArkoftheAsset.SunderingScissorsLeft.Value;
            Texture2D frontBladeGlow = ArkoftheAsset.SunderingScissorsLeftGlow.Value;
            Texture2D backBlade = ArkoftheAsset.SunderingScissorsRight.Value;
            Texture2D backBladeGlow = ArkoftheAsset.SunderingScissorsRightGlow.Value;

            float snippingRotation = Projectile.rotation + MathHelper.PiOver4;

            float drawRotation = MathHelper.Lerp(snippingRotation - MathHelper.PiOver4, snippingRotation, RotationRatio());
            float drawRotationBack = MathHelper.Lerp(snippingRotation + MathHelper.PiOver4, snippingRotation, RotationRatio());

            Vector2 drawOrigin = new Vector2(33, 86);
            Vector2 drawOriginBack = new Vector2(44f, 86);
            Vector2 drawPosition = OwnerConter + Projectile.velocity * 15 + Projectile.velocity * ThrustDisplaceRatio() * 50f;

            //绘制刀片和光晕
            ModifyElementsSwungHeld.DrawScissorBlade(backBlade, backBladeGlow, drawPosition, drawOriginBack, drawRotationBack, lightColor, Projectile.scale);
            ModifyElementsSwungHeld.DrawScissorBlade(frontBlade, frontBladeGlow, drawPosition, drawOrigin, drawRotation, lightColor, Projectile.scale);
        }

        public override void OnKill(int timeLeft) => SoundEngine.PlaySound(SoundID.Item35 with { Volume = SoundID.Item35.Volume * 2f });
    }

    internal class ModifyCosmosConstellation : BaseHeldProj, ICWRLoader
    {
        public override LocalizedText DisplayName => ProjectileLoader.GetProjectile(ProjectileType<ArkoftheCosmosConstellation>()).DisplayName;
        public override string Texture => ProjectileLoader.GetProjectile(ProjectileType<ArkoftheCosmosConstellation>()).Texture;
        public float Timer => Projectile.ai[0] - Projectile.timeLeft;
        private const float ConstellationSwapTime = 15;
        public List<Particle> Particles;
        Vector2 AnchorStart => Owner.GetPlayerStabilityCenter();
        Vector2 AnchorEnd => InMousePos;
        public Vector2 SizeVector => AnchorStart.To(AnchorEnd).UnitVector()
            * MathHelper.Clamp((AnchorEnd - AnchorStart).Length(), 0, ArkoftheCosmos.MaxThrowReach);
        private static FieldInfo particleTypesField;
        private static Dictionary<Type, int> particleTypes;
        void ICWRLoader.LoadData() {
            particleTypesField = typeof(GeneralParticleHandler).GetField("particleTypes", BindingFlags.Static | BindingFlags.NonPublic);
            particleTypes = (Dictionary<Type, int>)particleTypesField.GetValue(null);
        }
        void ICWRLoader.UnLoadData() {
            particleTypesField = null;
            particleTypes = null;
        }
        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 1;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }
        public override void SetDefaults() {
            Projectile.DamageType = DamageClass.Melee;
            Projectile.width = Projectile.height = 8;
            Projectile.tileCollide = false;
            Projectile.friendly = true;
            Projectile.penetrate = -1;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = (int)ConstellationSwapTime;
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            float collisionPoint = 0f;
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size()
                , Projectile.Center, Projectile.Center + SizeVector, 30f, ref collisionPoint);
        }

        public void BootlegSpawnParticle(Particle particle) {
            Particles.Add(particle);
            particle.Type = particleTypes[particle.GetType()];
        }

        public override void AI() {
            //初始化粒子列表
            Particles ??= [];

            //更新弹幕位置
            Projectile.Center = AnchorStart;

            //本地玩家控制逻辑
            HandleLocalPlayerInput();

            //检查玩家存活状态
            if (!Owner.Alives()) {
                Projectile.Kill();
                return;
            }

            //定时生成星座效果
            if (ShouldGenerateConstellation()) {
                GenerateConstellation();
            }

            //计算移动方向
            Vector2 moveDirection = CalculateMoveDirection();

            //更新所有粒子状态
            UpdateParticles(moveDirection);

            //清理过期粒子
            Particles.RemoveAll(particle => particle?.Time >= particle.Lifetime && particle.SetLifetime);
        }

        private void HandleLocalPlayerInput() {
            if (Projectile.IsOwnedByLocalPlayer() && !DownLeft && Projectile.timeLeft > 20) {
                Projectile.timeLeft = 20;
                NetUpdate();
            }
        }

        private bool ShouldGenerateConstellation() {
            return Timer % ConstellationSwapTime == 0 && Projectile.timeLeft >= 20 && !VaultUtils.isServer;
        }

        private void GenerateConstellation() {
            Particles.Clear();

            float hue = Main.rand.NextFloat();
            Vector2 previousStar = AnchorStart;

            //生成起始星
            CreateStar(previousStar, Color.White, Color.Plum);

            //生成星座主体
            for (float i = Main.rand.NextFloat(0.2f, 0.5f); i < 1; i += Main.rand.NextFloat(0.2f, 0.5f)) {
                hue = (hue + 0.16f) % 1;
                Color starColor = Main.hslToRgb(hue, 1, 0.8f);

                Vector2 offset = GetRandomOffset();
                Vector2 starPosition = AnchorStart + SizeVector * i + offset;

                //创建星星和连线
                CreateStarAndLine(starPosition, starColor, ref previousStar);

                //随机生成额外星星
                if (Main.rand.NextBool(3)) {
                    hue = (hue + 0.16f) % 1;
                    starColor = Main.hslToRgb(hue, 1, 0.8f);

                    offset = GetRandomOffset();
                    starPosition = AnchorStart + SizeVector * i + offset;

                    CreateStarAndLine(starPosition, starColor, ref previousStar);
                }
            }

            //生成结束星
            hue = (hue + 0.16f) % 1;
            CreateStarAndLine(AnchorStart + SizeVector, Main.hslToRgb(hue, 1, 0.8f), ref previousStar);
        }

        private Vector2 GetRandomOffset() {
            return Main.rand.NextFloat(-50f, 50f) *
                   Utils.SafeNormalize(SizeVector.RotatedBy(MathHelper.PiOver2), Vector2.Zero);
        }

        private void CreateStar(Vector2 position, Color primaryColor, Color secondaryColor) {
            Particle star = new GenericSparkle(
                position,
                Vector2.Zero,
                primaryColor,
                secondaryColor,
                Main.rand.NextFloat(1f, 1.5f),
                20,
                0f,
                3f
            );
            BootlegSpawnParticle(star);
        }

        private void CreateStarAndLine(Vector2 starPosition, Color color, ref Vector2 previousStar) {
            CreateStar(starPosition, Color.White, color);

            Particle line = new BloomLineVFX(
                previousStar,
                starPosition - previousStar,
                0.8f,
                color * 0.75f,
                20,
                true,
                true
            );
            BootlegSpawnParticle(line);

            previousStar = starPosition;
        }

        private Vector2 CalculateMoveDirection() {
            return Timer > Projectile.oldPos.Length ?
                   Projectile.position - Projectile.oldPos[0] :
                   Vector2.Zero;
        }

        private void UpdateParticles(Vector2 moveDirection) {
            foreach (Particle particle in Particles) {
                if (particle == null) continue;

                particle.Position += particle.Velocity + moveDirection;
                particle.Time++;

                //更新粒子颜色
                Vector3 color = Main.rgbToHsl(particle.Color);
                particle.Color = Main.hslToRgb(color.X + 0.02f, color.Y, color.Z);

                particle.Update();
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            if (Particles != null) {
                Main.spriteBatch.EnterShaderRegion(BlendState.Additive);
                foreach (Particle particle in Particles)
                    particle.CustomDraw(Main.spriteBatch);

                Main.spriteBatch.ExitShaderRegion();
            }
            return false;
        }
    }

    internal class ModifyCosmosSwungBladeHeld : ModifyElementsSwungHeld
    {
        public override LocalizedText DisplayName => ProjectileLoader.GetProjectile(ProjectileType<ArkoftheCosmosSwungBlade>()).DisplayName;
        public override string Texture => ProjectileLoader.GetProjectile(ProjectileType<ArkoftheCosmosSwungBlade>()).Texture;
        public override int TargetItem => ItemType<ArkoftheCosmos>();
        private Particle smear;
        public override float SwingMultiplication => 1f;//这个武器不适合吃攻速
        public override float MaxSwingTime => (SwirlSwing ? 55 : 35) * SwingMultiplication;
        public bool SwirlSwing => Combo == 1;
        public ref float HasFired => ref Projectile.localAI[0];
        public override float MaxThrowTime => 140 * SwingMultiplication;
        public override float SnapWindowStart => 0.35f;
        public CurveSegment startup;
        public CurveSegment swing;
        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 10;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults() {
            base.SetDefaults();
            swing = new CurveSegment(EasingType.SineOut, 0.1f, 0.25f, 0.75f);
            startup = new CurveSegment(EasingType.SineIn, 0f, 0f, 0.25f);
            shoot = new CurveSegment(EasingType.PolyIn, 0f, 1f, -0.2f, 3);
            remain = new CurveSegment(EasingType.Linear, SnapWindowStart, 0.8f, 0f);
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            float bladeLength = 172f * Projectile.scale;

            if (Thrown) {
                bool mainCollision = Collision.CheckAABBvAABBCollision(targetHitbox.TopLeft(), targetHitbox.Size(), Projectile.Center - Vector2.One * bladeLength / 2f, Vector2.One * bladeLength);
                if (Combo == 2f)
                    return mainCollision;

                else {
                    Vector2 thrownBladeStart = Vector2.SmoothStep(OwnerConter, Projectile.Center, MathHelper.Clamp(SnapEndCompletion + 0.25f, 0f, 1f));
                    bool thrownScissorCollision = Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(), thrownBladeStart, thrownBladeStart + direction * bladeLength);
                    return mainCollision || thrownScissorCollision;
                }
            }

            float collisionPoint = 0f;
            Vector2 holdPoint = DistanceFromPlayer.Length() * Projectile.rotation.ToRotationVector2();

            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(), OwnerConter + holdPoint
                , OwnerConter + holdPoint + Projectile.rotation.ToRotationVector2() * bladeLength, 24, ref collisionPoint);
        }

        internal float SwirlRatio() => PiecewiseAnimation(SwingCompletion, [startup, swing]);

        public override void Initialize() {
            Projectile.timeLeft = Thrown ? (int)MaxThrowTime : (int)MaxSwingTime;
            SoundStyle sound = (Charge > 0 || Thrown) ? CommonCalamitySounds.LouderPhantomPhoenix : SoundID.Item71;
            SoundEngine.PlaySound(sound, Projectile.Center);
            direction = Projectile.velocity.UnitVector();
            Projectile.velocity = direction;
            Projectile.rotation = direction.ToRotation();

            if (SwirlSwing) {
                Projectile.localNPCHitCooldown = (int)(Projectile.localNPCHitCooldown / 4f);
            }

            Projectile.netUpdate = true;
            Projectile.netSpam = 0;
        }

        public override void AI() {
            HandleOwnerVisuals(); //让主人看起来像个正经拿剑的人

            if (!Thrown) {
                HandleMeleeBehavior(); //近战模式：开始表演光剑杂技
            }
            else {
                HandleThrownBehavior(); //投掷模式：星际飞镖导航系统v1.0
            }
        }

        private void HandleOwnerVisuals() {
            //以下代码让主人正确持剑
            SetHeld();
            SetDirection();
            Owner.itemRotation = Projectile.rotation;

            //向左转向时的π魔法（不要问为什么，问就是数学之美）
            if (Owner.direction != 1) {
                Owner.itemRotation -= MathHelper.Pi;
            }
            Owner.itemRotation = MathHelper.WrapAngle(Owner.itemRotation);
        }

        private void HandleMeleeBehavior() {
            Projectile.Center = OwnerConter + DistanceFromPlayer; //剑不离身是基本礼仪

            if (!SwirlSwing) {
                //普通挥动：老年太极剑法
                Projectile.rotation = Projectile.velocity.ToRotation() +
                    MathHelper.Lerp(
                        SwingWidth / 2 * SwingDirection,
                        -SwingWidth / 2 * SwingDirection,
                        SwingRatio()
                    );
            }
            else {
                HandleSwirlAttack(); //死亡龙卷风模式启动
            }

            Projectile.scale = 1.2f +
                (MathF.Sin(SwingRatio() * MathHelper.Pi) * 0.6f) +
                (Charge / 10f) * 0.2f;
        }

        private void HandleSwirlAttack() {
            //旋转参数会让实习生怀疑人生
            float startRot = (MathHelper.Pi - MathHelper.PiOver4) * SwingDirection;
            float endRot = -(MathHelper.TwoPi + MathHelper.PiOver4 * 1.5f) * SwingDirection;
            Projectile.rotation = Projectile.velocity.ToRotation() +
                MathHelper.Lerp(startRot, endRot, SwirlRatio());

            DoParticleEffects(true); //GPU：你不要过来啊！

            //生成能量弹的玄学条件（原始注释说要-1，那就当祖宗规矩供着）
            bool shouldSpawnBolt = Owner.whoAmI == Main.myPlayer &&
                (Projectile.timeLeft - 1) % Math.Ceiling(MaxSwingTime / ArkoftheCosmos.SwirlBoltAmount) == 0f;

            if (shouldSpawnBolt) {
                //微调角度防止射到脚趾（产品经理的奇怪需求）
                float adjustedBlastRotation = Projectile.rotation -
                    MathHelper.PiOver4 * 1.15f * Owner.direction;

                var source = Projectile.GetSource_FromThis();
                Projectile blast = Projectile.NewProjectileDirect(
                    source,
                    OwnerConter + adjustedBlastRotation.ToRotationVector2() * 10f,
                    adjustedBlastRotation.ToRotationVector2() * 20f,
                    ProjectileType<EonBolt>(),
                    (int)(ArkoftheCosmos.SwirlBoltDamageMultiplier / ArkoftheCosmos.SwirlBoltAmount * Projectile.damage),
                    0f,
                    Owner.whoAmI,
                    0.55f,
                    MathHelper.Pi * 0.05f
                );
                blast.timeLeft = 100; //写在代码块里显得很专业
            }
        }

        private void HandleThrownBehavior() {
            //生成星座链的精确时刻（误差0.005以内）
            if (ShouldSpawnConstellationChain()) {
                Particle pulse = new PulseRing(
                    Projectile.Center,
                    Vector2.Zero,
                    Color.OrangeRed,
                    0.05f,
                    1.8f,
                    8
                );
                GeneralParticleHandler.SpawnParticle(pulse);
                SoundEngine.PlaySound(SoundID.Item4); //经典音效不能少

                Projectile chain = Projectile.NewProjectileDirect(
                    Projectile.GetSource_FromThis(),
                    OwnerConter,
                    Vector2.Zero,
                    ProjectileType<ModifyCosmosConstellation>(),
                    (int)(Projectile.damage * ArkoftheCosmos.chainDamageMultiplier),
                    0,
                    Owner.whoAmI,
                    (int)(Projectile.timeLeft / 2f)
                );
                chain.timeLeft = (int)(Projectile.timeLeft / 2f); //时间对半砍
                chain.netUpdate = true;
            }

            UpdateProjectilePosition(); //飞镖的玄学移动公式
            Projectile.rotation -= MathHelper.PiOver4 * 0.3f; //旋转起来才专业
            Projectile.scale = 1f + ThrowScaleRatio() * 0.5f;

            //抓取窗口期的精确判断（误差0.005是祖传参数）
            if (Math.Abs(ThrowCompletion - SnapWindowEnd) <= 0.005f) {
                direction = Projectile.Center - OwnerConter;
            }

            //进入抓取后的运动逻辑
            if (ThrowCompletion > SnapWindowEnd) {
                Projectile.Center = OwnerConter + direction * ThrowRatio();
            }

            HandleSnapMechanics(); //处理抓取时的酷炫特效
            DoParticleEffects(false);

            //回归时的好莱坞级动画
            if (Combo == 3f) {
                HandleReturnAnimation();
            }
        }

        private bool ShouldSpawnConstellationChain() {
            return Math.Abs(ThrowCompletion - SnapWindowStart + 0.1f) <= 0.005f && ChanceMissed == 0f && Projectile.IsOwnedByLocalPlayer();
        }

        private void UpdateProjectilePosition() {
            //25% Lerp + 75% MoveTowards = 100% 魔法
            Projectile.Center = Vector2.Lerp(
                Projectile.Center,
                Owner.Calamity().mouseWorld,
                0.025f * ThrowRatio()
            );
            Projectile.Center = Projectile.Center.MoveTowards(
                Owner.Calamity().mouseWorld,
                20f * ThrowRatio()
            );

            //防止飞出银河系（虽然理论上应该可以）
            if ((Projectile.Center - OwnerConter).Length() > ArkoftheCosmos.MaxThrowReach) {
                Projectile.Center = OwnerConter +
                    Owner.DirectionTo(Projectile.Center) * ArkoftheCosmos.MaxThrowReach;
            }
        }

        private void HandleSnapMechanics() {
            bool shouldTriggerSnap = !OwnerCanShoot &&
                                    Combo == 2 &&
                                    ThrowCompletion >= (SnapWindowStart - 0.1f) &&
                                    ThrowCompletion < SnapWindowEnd &&
                                    ChanceMissed == 0f;

            if (shouldTriggerSnap) {
                //生成粒子
                Particle snapSpark = new GenericSparkle(
                    Projectile.Center,
                    Owner.velocity - Utils.SafeNormalize(Projectile.velocity, Vector2.Zero),
                    Color.White,
                    Color.OrangeRed,
                    Main.rand.NextFloat(1f, 2f),
                    10 + Main.rand.Next(10),
                    0.1f,
                    3f
                );
                GeneralParticleHandler.SpawnParticle(snapSpark);

                //屏幕震动
                if (Main.LocalPlayer.Calamity().GeneralScreenShakePower < 3) {
                    Main.LocalPlayer.Calamity().GeneralScreenShakePower = 3;
                }

                if (Owner.whoAmI == Main.myPlayer) {
                    //生成三连发能量弹（因为三是个神圣的数字）
                    float rotationOffset = MathHelper.TwoPi * Main.rand.NextFloat();
                    for (int i = 0; i < 3; i++) {
                        var source = Projectile.GetSource_FromThis();
                        Projectile blast = Projectile.NewProjectileDirect(
                            source,
                            Projectile.Center + (MathHelper.TwoPi * (i / 3f) + rotationOffset).ToRotationVector2() * 30f,
                            (MathHelper.TwoPi * (i / 3f) + rotationOffset).ToRotationVector2() * 20f,
                            ProjectileType<EonBolt>(),
                            (int)(ArkoftheCosmos.SnapBoltsDamageMultiplier * Projectile.damage),
                            0f,
                            Owner.whoAmI,
                            0.55f,
                            MathHelper.Pi * 0.05f
                        );
                        blast.timeLeft = 100;
                    }

                    //清除免疫
                    Array.Clear(Projectile.localNPCImmunity, 0, Main.maxNPCs);
                }

                //状态更新（这段是给三个月后的自己看的）
                Combo = 3f;
                direction = Projectile.Center - OwnerConter;
                Projectile.velocity = Projectile.rotation.ToRotationVector2();
                Projectile.timeLeft = (int)SnapEndTime;
                Projectile.localNPCHitCooldown = (int)SnapEndTime;
            }
            else if (!OwnerCanShoot && Combo == 2 && ChanceMissed == 0f) {
                ChanceMissed = 1f; //标记抓取失败（程序员：这是特性不是bug）
            }
        }

        private void HandleReturnAnimation() {
            //缓动曲线：看起来贵就对了
            float curveDownGently = MathHelper.Lerp(
                1f,
                0.8f,
                1f - MathF.Sqrt(1f - MathF.Pow(SnapEndCompletion, 2f))
            );
            Projectile.Center = OwnerConter + direction * curveDownGently;
            Projectile.scale = 1.5f;

            //旋转逻辑应该刻在Sistine Chapel天花板上
            float orientateProperly = MathF.Sqrt(1f -
                MathF.Pow(MathHelper.Clamp(SnapEndCompletion + 0.2f, 0f, 1f) - 1f, 2f));
            float extraRotations = (direction.ToRotation() + MathHelper.PiOver4 >
                                  Projectile.velocity.ToRotation()) ?
                                  -MathHelper.TwoPi :
                                  0f;
            Projectile.rotation = MathHelper.Lerp(
                Projectile.velocity.ToRotation(),
                direction.ToRotation() + extraRotations,
                orientateProperly
            );
        }

        public void DoParticleEffects(bool swirlSwing) {
            if (swirlSwing) {
                HandleSwirlSwingEffects();
            }
            else {
                HandleThrowEffects();
            }
        }

        private void HandleSwirlSwingEffects() {
            Projectile.scale = CalculateSwirlScale();
            Color currentColor = CalculateSwirlColor();

            if (smear == null) {
                smear = CreateCircularSmear(currentColor, Projectile.scale * 2.4f);
                GeneralParticleHandler.SpawnParticle(smear);
            }
            else {
                UpdateSmear(smear, currentColor, Projectile.scale);
            }

            if (Main.rand.NextBool()) {
                SpawnCritSpark(currentColor);
            }

            float opacity = CalculateSwirlOpacity();
            float scaleFactor = CalculateSwirlScaleFactor();

            if (Main.rand.NextBool()) {
                SpawnHeavySmokeParticles(opacity, scaleFactor);
            }
        }

        private void HandleThrowEffects() {
            Color smearColor = CalculateThrowColor();
            float opacity = CalculateThrowOpacity();

            if (smear == null) {
                smear = CreateThrowSmear(smearColor, opacity, Projectile.scale * 1.7f);
                GeneralParticleHandler.SpawnParticle(smear);
            }
            else {
                UpdateThrowSmear(smear, smearColor, opacity, Projectile.scale);
            }

            if (Combo == 2f) {
                if (Main.rand.NextBool()) {
                    SpawnThrowCritSpark();
                }

                opacity = 0.25f;
                float scaleFactor = 0.7f;

                if (Main.rand.NextBool()) {
                    SpawnThrowHeavySmokeParticles(opacity, scaleFactor);
                }
            }
        }

        private float CalculateSwirlScale() {
            return 1.6f + ((float)Math.Sin(SwirlRatio() * MathHelper.Pi) * 1f) + (Charge / 10f) * 0.05f;
        }

        private Color CalculateSwirlColor() {
            return Color.Chocolate * (MathHelper.Clamp((float)Math.Sin((SwirlRatio() - 0.2f) * MathHelper.Pi), 0f, 1f) * 0.8f);
        }

        private CircularSmearSmokeyVFX CreateCircularSmear(Color color, float scale) {
            return new CircularSmearSmokeyVFX(OwnerConter, color, Projectile.rotation, scale);
        }

        private void UpdateSmear(Particle smear, Color color, float scale) {
            smear.Rotation = Projectile.rotation + MathHelper.PiOver4 + (Owner.direction < 0 ? MathHelper.PiOver4 * 4f : 0f);
            smear.Time = 0;
            smear.Position = OwnerConter;
            smear.Scale = MathHelper.Lerp(2.6f, 3.5f, (scale - 1.6f) / 1f);
            smear.Color = color;
        }

        private void SpawnCritSpark(Color color) {
            float maxDistance = Projectile.scale * 78f;
            Vector2 distance = Main.rand.NextVector2Circular(maxDistance, maxDistance);
            Vector2 angularVelocity = Utils.SafeNormalize(distance.RotatedBy(MathHelper.PiOver2 * Owner.direction), Vector2.Zero) * 2 * (1f + distance.Length() / 15f);
            Particle glitter = new CritSpark(OwnerConter + distance, Owner.velocity + angularVelocity, Main.rand.NextBool(3) ? Color.Turquoise : Color.Coral, color, 1f + 1 * (distance.Length() / maxDistance), 10, 0.05f, 3f);
            GeneralParticleHandler.SpawnParticle(glitter);
        }

        private float CalculateSwirlOpacity() {
            return MathHelper.Clamp(MathHelper.Clamp((float)Math.Sin((SwirlRatio() - 0.2f) * MathHelper.Pi), 0f, 1f) * 2f, 0, 1) * 0.25f;
        }

        private float CalculateSwirlScaleFactor() {
            return MathHelper.Clamp(MathHelper.Clamp((float)Math.Sin((SwirlRatio() - 0.2f) * MathHelper.Pi), 0f, 1f), 0, 1);
        }

        private void SpawnHeavySmokeParticles(float opacity, float scaleFactor) {
            for (float i = 0f; i <= 1; i += 0.5f) {
                Vector2 smokepos = OwnerConter + (Projectile.rotation.ToRotationVector2() * (30 + 50 * i) * Projectile.scale) + Projectile.rotation.ToRotationVector2().RotatedBy(-MathHelper.PiOver2) * 30f * scaleFactor * Main.rand.NextFloat();
                Vector2 smokespeed = Projectile.rotation.ToRotationVector2().RotatedBy(-MathHelper.PiOver2 * Owner.direction) * 20f * scaleFactor + Owner.velocity;

                Particle smoke = new HeavySmokeParticle(smokepos, smokespeed, Color.Lerp(Color.DodgerBlue, Color.MediumVioletRed, i), 6 + Main.rand.Next(5), scaleFactor * Main.rand.NextFloat(2.8f, 3.1f), opacity + Main.rand.NextFloat(0f, 0.2f), 0f, false, 0, true);
                GeneralParticleHandler.SpawnParticle(smoke);

                if (Main.rand.NextBool(3)) {
                    Particle smokeGlow = new HeavySmokeParticle(smokepos, smokespeed, Main.rand.NextBool(5) ? Color.Gold : Color.Chocolate, 5, scaleFactor * Main.rand.NextFloat(2f, 2.4f), opacity * 2.5f, 0f, true, 0.004f, true);
                    GeneralParticleHandler.SpawnParticle(smokeGlow);
                }
            }
        }

        private Color CalculateThrowColor() {
            return Main.hslToRgb(((SwingTimer - MaxSwingTime * 0.5f) / (MaxSwingTime * 0.5f)) * 0.15f, 1, 0.8f);
        }

        private float CalculateThrowOpacity() {
            return (Combo == 3f ? (float)Math.Sin(SnapEndCompletion * MathHelper.PiOver2 + MathHelper.PiOver2) : (float)Math.Sin(ThrowCompletion * MathHelper.Pi)) * 0.5f;
        }

        private Particle CreateThrowSmear(Color color, float opacity, float scale) {
            if (Charge <= 0)
                return new TrientCircularSmear(Projectile.Center, color * opacity, Projectile.rotation, scale);
            else
                return new CircularSmearSmokeyVFX(Projectile.Center, color * opacity, Projectile.rotation, scale);
        }

        private void UpdateThrowSmear(Particle smear, Color color, float opacity, float scale) {
            smear.Rotation = Projectile.rotation - 3.5f * MathHelper.PiOver4;
            smear.Time = 0;
            smear.Position = Projectile.Center;
            smear.Scale = scale * 1.65f;
            smear.Color = color * opacity;
        }

        private void SpawnThrowCritSpark() {
            float maxDistance = Projectile.scale * 78f;
            Vector2 distance = Main.rand.NextVector2Circular(maxDistance, maxDistance);
            Vector2 angularVelocity = Utils.SafeNormalize(distance.RotatedBy(-MathHelper.PiOver2), Vector2.Zero) * 2 * (1f + distance.Length() / 15f);
            Color glitterColor = Main.hslToRgb(Main.rand.NextFloat(), 1, 0.5f);
            Particle glitter = new CritSpark(Projectile.Center + distance, Owner.velocity + angularVelocity, Color.White, glitterColor, 1f + 1 * (distance.Length() / maxDistance), 10, 0.05f, 3f);
            GeneralParticleHandler.SpawnParticle(glitter);
        }

        private void SpawnThrowHeavySmokeParticles(float opacity, float scaleFactor) {
            for (float i = 0.5f; i <= 1; i += 0.5f) {
                Vector2 smokepos = Projectile.Center + (Projectile.rotation.ToRotationVector2() * (60 * i) * Projectile.scale) + Projectile.rotation.ToRotationVector2().RotatedBy(-MathHelper.PiOver2) * 30f * scaleFactor * Main.rand.NextFloat();
                Vector2 smokespeed = Projectile.rotation.ToRotationVector2().RotatedBy(MathHelper.PiOver2) * 20f * scaleFactor + Owner.velocity;

                Particle smoke = new HeavySmokeParticle(smokepos, smokespeed, Color.Lerp(Color.DodgerBlue, Color.MediumVioletRed, i), 10 + Main.rand.Next(5), scaleFactor * Main.rand.NextFloat(2.8f, 3.1f), opacity + Main.rand.NextFloat(0f, 0.2f), 0f, false, 0, true);
                GeneralParticleHandler.SpawnParticle(smoke);

                if (Main.rand.NextBool(3)) {
                    Particle smokeGlow = new HeavySmokeParticle(smokepos, smokespeed, Main.rand.NextBool(5) ? Color.Gold : Color.Chocolate, 7, scaleFactor * Main.rand.NextFloat(2f, 2.4f), opacity * 2.5f, 0f, true, 0.004f, true);
                    GeneralParticleHandler.SpawnParticle(smokeGlow);
                }
            }
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {
            if (Combo == 3f)
                modifiers.SourceDamage *= ArkoftheElements.snapDamageMultiplier;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            for (int i = 0; i < 5; i++) {
                Vector2 particleSpeed = Utils.SafeNormalize(target.Center - Projectile.Center, Vector2.One).RotatedByRandom(MathHelper.PiOver4 * 0.8f) * Main.rand.NextFloat(3.6f, 8f);
                Particle energyLeak = new SquishyLightParticle(target.Center, particleSpeed, Main.rand.NextFloat(0.3f, 0.6f), Color.OrangeRed, 60, 2, 2.5f, hueShift: 0.06f);
                GeneralParticleHandler.SpawnParticle(energyLeak);
            }

            if (Combo == 3f) {
                SoundEngine.PlaySound(CommonCalamitySounds.ScissorGuillotineSnapSound with { Volume = CommonCalamitySounds.ScissorGuillotineSnapSound.Volume * 1.3f }, Projectile.Center);
            }
        }

        public override void OnKill(int timeLeft) {
            if (Combo == 3f) {
                if (Main.LocalPlayer.Calamity().GeneralScreenShakePower < 3)
                    Main.LocalPlayer.Calamity().GeneralScreenShakePower = 3;

                SoundEngine.PlaySound(SoundID.Item84, Projectile.Center);

                Vector2 sliceDirection = Utils.SafeNormalize(direction, Vector2.One) * 40;
                Particle SliceLine = new LineVFX(Projectile.Center - sliceDirection, sliceDirection * 2f, 0.2f, Color.Orange * 0.7f, expansion: 250f) {
                    Lifetime = 10
                };
                GeneralParticleHandler.SpawnParticle(SliceLine);

            }
        }

        //辅助方法：绘制残影
        private void DrawAfterimages(Texture2D afterimage, Vector2 position, float rotation, Vector2 origin, float scale, SpriteEffects flip, int trailLength, float charge) {
            for (int i = 1; i < trailLength; ++i) {
                Color color = Main.hslToRgb((i / (float)trailLength) * 0.1f, 1, 0.6f + (charge > 0 ? 0.3f : 0f));
                float afterimageRotation = rotation + MathHelper.PiOver4 + (Owner.direction < 0 ? MathHelper.PiOver2 : 0f);
                Main.EntitySpriteDraw(afterimage, position, null, color * 0.15f, afterimageRotation, origin, scale - 0.2f * (i / (float)trailLength), flip, 0);
            }
        }

        //辅助方法：绘制拖尾效果
        private void DrawSmearEffect(Texture2D smear, Vector2 position, float swingCompletion, float swingDirection, float swingTimer, float maxSwingTime, float combo, float scale) {
            Main.spriteBatch.End();
            Main.spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.Additive, Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);

            float opacity = (float)Math.Sin(swingCompletion * MathHelper.Pi);
            float rotation = (-MathHelper.PiOver4 * 0.5f + MathHelper.PiOver4 * 0.5f * swingCompletion + (combo == 1f ? MathHelper.PiOver4 : 0)) * swingDirection;
            Color smearColor = Main.hslToRgb(((swingTimer - maxSwingTime * 0.5f) / (maxSwingTime * 0.5f)) * 0.15f + (combo == 1f ? 0.85f : 0f), 1, 0.6f);

            Main.EntitySpriteDraw(smear, position, null, smearColor * 0.5f * opacity, Projectile.velocity.ToRotation() + MathHelper.Pi + rotation, smear.Size() / 2f, scale * 2.3f, SpriteEffects.None, 0);

            Main.spriteBatch.End();
            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
        }

        //辅助方法：绘制剪刀刀片
        private static void DrawScissorBlade(Texture2D blade, Texture2D glowmask, Vector2 position
            , float rotation, Vector2 origin, Color lightColor, SpriteEffects flip, float scale) {
            Main.EntitySpriteDraw(blade, position, null, lightColor, rotation, origin, scale, flip, 0);
            Main.EntitySpriteDraw(glowmask, position, null, Color.Lerp(lightColor, Color.White, 0.75f), rotation, origin, scale, flip, 0);
        }

        public override void DrawSingleSwungScissorBlade(Color lightColor) {
            Texture2D sword = Combo == 0 ? ArkoftheAsset.SunderingScissorsRight.Value : ArkoftheAsset.SunderingScissorsLeft.Value;
            Texture2D glowmask = Combo == 0 ? ArkoftheAsset.SunderingScissorsRightGlow.Value : ArkoftheAsset.SunderingScissorsLeftGlow.Value;

            bool flipped = Owner.direction < 0;
            SpriteEffects flip = flipped ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
            float extraAngle = flipped ? MathHelper.PiOver2 : 0f;

            float drawRotation = Projectile.rotation + MathHelper.PiOver4 + extraAngle;
            Vector2 drawOrigin = new Vector2(flipped ? sword.Width : 0f, sword.Height);
            Vector2 drawOffset = OwnerConter + Projectile.rotation.ToRotationVector2() * 10f - Main.screenPosition;

            if (CWRRef.GetAfterimages() && SwingTimer > ProjectileID.Sets.TrailCacheLength[Projectile.type] && Combo == 0f) {
                DrawAfterimages(glowmask, drawOffset, Projectile.rotation, drawOrigin, Projectile.scale, flip, Projectile.oldRot.Length, Charge);
            }

            DrawScissorBlade(sword, glowmask, drawOffset, drawRotation, drawOrigin, lightColor, flip, Projectile.scale);

            if (SwingCompletion > 0.5f && Combo == 0f) {
                DrawSmearEffect(ArkoftheAsset.TrientCircularSmear.Value, OwnerConter - Main.screenPosition, SwingCompletion, SwingDirection, SwingTimer, MaxSwingTime, Combo, Projectile.scale);
            }
        }

        public override void DrawSwungScissors(Color lightColor) {
            Texture2D frontBlade = ArkoftheAsset.SunderingScissorsLeft.Value;
            Texture2D frontBladeGlow = ArkoftheAsset.SunderingScissorsLeftGlow.Value;
            Texture2D backBlade = ArkoftheAsset.SunderingScissorsRight.Value;
            Texture2D backBladeGlow = ArkoftheAsset.SunderingScissorsRightGlow.Value;

            bool flipped = Owner.direction < 0;
            SpriteEffects flip = flipped ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
            float extraAngle = flipped ? MathHelper.PiOver2 : 0f;

            float drawRotation = Projectile.rotation + MathHelper.PiOver4 + extraAngle;
            Vector2 drawOrigin = new Vector2(flipped ? frontBlade.Width : 0f, frontBlade.Height);
            Vector2 drawOffset = OwnerConter + Projectile.rotation.ToRotationVector2() * 10f - Main.screenPosition;

            Vector2 backScissorOrigin = new Vector2(flipped ? 90 : 44f, 86);
            Vector2 backScissorDrawPosition = OwnerConter + Projectile.rotation.ToRotationVector2() * 10f + (Projectile.rotation.ToRotationVector2() * 56f + (Projectile.rotation - MathHelper.PiOver2).ToRotationVector2() * 11 * Owner.direction) * Projectile.scale - Main.screenPosition;

            if (CWRRef.GetAfterimages() && SwingTimer > ProjectileID.Sets.TrailCacheLength[Projectile.type]) {
                DrawAfterimages(ArkoftheAsset.SunderingScissorsGlow.Value, drawOffset, Projectile.rotation, drawOrigin, Projectile.scale, flip, Projectile.oldRot.Length, Charge);
            }

            DrawScissorBlade(backBlade, backBladeGlow, backScissorDrawPosition, drawRotation, backScissorOrigin, lightColor, flip, Projectile.scale);
            DrawScissorBlade(frontBlade, frontBladeGlow, drawOffset, drawRotation, drawOrigin, lightColor, flip, Projectile.scale);

            if (SwingCompletion > 0.5f && Combo == 0f) {
                DrawSmearEffect(ArkoftheAsset.TrientCircularSmear.Value, OwnerConter - Main.screenPosition, SwingCompletion, SwingDirection, SwingTimer, MaxSwingTime, Combo, Projectile.scale);
            }
        }

        public override void DrawSingleThrownScissorBlade(Color lightColor) {
            Texture2D sword = ArkoftheAsset.SunderingScissorsLeft.Value;
            Texture2D glowmask = ArkoftheAsset.SunderingScissorsLeftGlow.Value;

            if (Combo == 3f) {
                Texture2D thrownSword = ArkoftheAsset.SunderingScissorsRight.Value;
                Texture2D thrownGlowmask = ArkoftheAsset.SunderingScissorsRightGlow.Value;

                Vector2 drawPos2 = Vector2.SmoothStep(OwnerConter, Projectile.Center, MathHelper.Clamp(SnapEndCompletion + 0.25f, 0f, 1f));
                float drawRotation2 = direction.ToRotation() + MathHelper.PiOver4;
                Vector2 drawOrigin2 = new Vector2(44, 86);

                DrawScissorBlade(thrownSword, thrownGlowmask, drawPos2 - Main.screenPosition, drawRotation2, drawOrigin2, lightColor, SpriteEffects.None, Projectile.scale);
            }

            Vector2 drawPos = Projectile.Center;
            float drawRotation = Projectile.rotation + MathHelper.PiOver4;
            Vector2 drawOrigin = new Vector2(32, 86);

            DrawScissorBlade(sword, glowmask, drawPos - Main.screenPosition, drawRotation, drawOrigin, lightColor, SpriteEffects.None, Projectile.scale);
        }

        public override void DrawThrownScissors(Color lightColor) {
            Texture2D frontBlade = ArkoftheAsset.SunderingScissorsLeft.Value;
            Texture2D frontBladeGlow = ArkoftheAsset.SunderingScissorsLeftGlow.Value;
            Texture2D backBlade = ArkoftheAsset.SunderingScissorsRight.Value;
            Texture2D backBladeGlow = ArkoftheAsset.SunderingScissorsRightGlow.Value;

            Vector2 drawPos = Projectile.Center;
            Vector2 drawOrigin = new Vector2(32, 86);
            float drawRotation = Projectile.rotation + MathHelper.PiOver4;

            Vector2 drawOrigin2 = new Vector2(44, 86);
            float drawRotation2 = Projectile.rotation + MathHelper.Lerp(MathHelper.PiOver4, MathHelper.PiOver2 * 1.33f, MathHelper.Clamp(ThrowCompletion * 2f, 0f, 1f));

            if (Combo == 3f)
                drawRotation2 = Projectile.rotation + MathHelper.Lerp(MathHelper.PiOver2 * 1.33f, MathHelper.PiOver4, MathHelper.Clamp(SnapEndCompletion + 0.5f, 0f, 1f));

            DrawScissorBlade(backBlade, backBladeGlow, drawPos - Main.screenPosition, drawRotation2, drawOrigin2, lightColor, SpriteEffects.None, Projectile.scale);
            DrawScissorBlade(frontBlade, frontBladeGlow, drawPos - Main.screenPosition, drawRotation, drawOrigin, lightColor, SpriteEffects.None, Projectile.scale);
        }
    }
}
