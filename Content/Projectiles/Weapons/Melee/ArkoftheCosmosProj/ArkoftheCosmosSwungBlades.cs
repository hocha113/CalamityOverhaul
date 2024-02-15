using CalamityMod;
using CalamityMod.Particles;
using CalamityMod.Projectiles.Melee;
using CalamityMod.Sounds;
using CalamityOverhaul.Content.Items.Melee;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.IO;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Melee.ArkoftheCosmosProj
{
    /// <summary>
    /// 刀刃弹幕
    /// </summary>
    internal class ArkoftheCosmosSwungBlades : ModProjectile
    {
        private bool initialized;

        private Vector2 direction = Vector2.Zero;

        private Particle smear;

        private float SwingWidth = MathF.PI * 3f / 4f;

        public const float MaxThrowTime = 140f;

        public float ThrowReach;

        public const float SnapWindowStart = 0.2f;

        public const float SnapWindowEnd = 0.75f;

        public CalamityUtils.CurveSegment anticipation = new CalamityUtils.CurveSegment(CalamityUtils.EasingType.ExpOut, 0f, 0f, 0.15f);

        public CalamityUtils.CurveSegment thrust = new CalamityUtils.CurveSegment(CalamityUtils.EasingType.PolyInOut, 0.1f, 0.15f, 0.85f, 3);

        public CalamityUtils.CurveSegment hold = new CalamityUtils.CurveSegment(CalamityUtils.EasingType.Linear, 0.5f, 1f, 0.2f);

        public CalamityUtils.CurveSegment startup = new CalamityUtils.CurveSegment(CalamityUtils.EasingType.SineIn, 0f, 0f, 0.25f);

        public CalamityUtils.CurveSegment swing = new CalamityUtils.CurveSegment(CalamityUtils.EasingType.SineOut, 0.1f, 0.25f, 0.75f);

        public CalamityUtils.CurveSegment shoot = new CalamityUtils.CurveSegment(CalamityUtils.EasingType.PolyIn, 0f, 1f, -0.2f, 3);

        public CalamityUtils.CurveSegment remain = new CalamityUtils.CurveSegment(CalamityUtils.EasingType.Linear, 0.2f, 0.8f, 0f);

        public CalamityUtils.CurveSegment retract = new CalamityUtils.CurveSegment(CalamityUtils.EasingType.SineIn, 0.75f, 1f, -1f);

        public CalamityUtils.CurveSegment sizeCurve = new CalamityUtils.CurveSegment(CalamityUtils.EasingType.SineBump, 0f, 0f, 1f);

        public new string LocalizationCategory => "Projectiles.Melee";

        public override string Texture => "CalamityMod/Projectiles/Melee/SunderingScissorsRight";

        public ref float Combo => ref Projectile.ai[0];

        public ref float Charge => ref Projectile.ai[1];

        public Player Owner => CWRUtils.GetPlayerInstance(Projectile.owner);

        public float MaxSwingTime => SwirlSwing ? 55 : 35;

        public int SwingDirection {
            get {
                float combo = Combo;
                if (combo != 0f) {
                    if (combo == 1f) {
                        return -1 * Math.Sign(direction.X);
                    }

                    return 0;
                }

                return Math.Sign(direction.X);
            }
        }

        public bool SwirlSwing => Combo == 1f;

        public Vector2 DistanceFromPlayer => direction * 30f;

        public float SwingTimer => MaxSwingTime - Projectile.timeLeft;

        public float SwingCompletion => SwingTimer / MaxSwingTime;

        public ref float HasFired => ref Projectile.localAI[0];

        private bool OwnerCanShoot {
            get {
                if (Owner.channel && !Owner.noItems && !Owner.CCed) {
                    //需要考虑到，这个弹幕会被重制物品发射。也会被原模组物品发射
                    return Owner.HeldItem.type == ModContent.ItemType<ArkoftheCosmos>()
                        || Owner.HeldItem.type == ModContent.ItemType<CalamityMod.Items.Weapons.Melee.ArkoftheCosmos>();
                }

                return false;
            }
        }

        public bool Thrown {
            get {
                if (Combo != 2f) {
                    return Combo == 3f;
                }

                return true;
            }
        }

        public float ThrowTimer => 140f - Projectile.timeLeft;

        public float ThrowCompletion => ThrowTimer / 140f;

        public float SnapEndTime => 35f;

        public float SnapEndCompletion => (SnapEndTime - Projectile.timeLeft) / SnapEndTime;

        public ref float ChanceMissed => ref Projectile.localAI[1];

        public bool shootBool = true;

        private ArkoftheCosmos arkoftheCosmos => Owner.HeldItem.ModItem as ArkoftheCosmos;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 10;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults() {
            Projectile.DamageType = DamageClass.MeleeNoSpeed;
            Projectile.width = Projectile.height = 60;
            Projectile.width = Projectile.height = 60;
            Projectile.tileCollide = false;
            Projectile.friendly = true;
            Projectile.penetrate = -1;
            Projectile.extraUpdates = 1;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = Thrown ? 10 : (int)MaxSwingTime;
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            float num = 172f * Projectile.scale;
            if (Thrown) {
                bool flag = Collision.CheckAABBvAABBCollision(targetHitbox.TopLeft(), targetHitbox.Size(), Projectile.Center - Vector2.One * num / 2f, Vector2.One * num);
                if (Combo == 2f) {
                    return flag;
                }

                Vector2 vector = Vector2.SmoothStep(Owner.Center, Projectile.Center, MathHelper.Clamp(SnapEndCompletion + 0.25f, 0f, 1f));
                bool flag2 = Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(), vector, vector + direction * num);
                return flag || flag2;
            }

            float collisionPoint = 0f;
            Vector2 vector2 = DistanceFromPlayer.Length() * Projectile.rotation.ToRotationVector2();
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(), Owner.Center + vector2, Owner.Center + vector2 + Projectile.rotation.ToRotationVector2() * num, 24f, ref collisionPoint);
        }

        internal float SwingRatio() {
            return CalamityUtils.PiecewiseAnimation(SwingCompletion, anticipation, thrust, hold);
        }

        internal float SwirlRatio() {
            return CalamityUtils.PiecewiseAnimation(SwingCompletion, startup, swing);
        }

        internal float ThrowRatio() {
            return CalamityUtils.PiecewiseAnimation(ThrowCompletion, shoot, remain, retract);
        }

        internal float ThrowScaleRatio() {
            return CalamityUtils.PiecewiseAnimation(ThrowCompletion, sizeCurve);
        }

        /// <summary>
        /// 发射旋转星弹幕
        /// </summary>
        public void ShootConstellations(int mode = 0) {
            if (shootBool) {
                if (mode == 0) {
                    for (int i = 0; i < 7; i++) {
                        if (3 == i) continue;

                        Vector2 toMou = Owner.Center.To(Main.MouseWorld);
                        Vector2 vr = toMou.RotatedBy(MathHelper.ToRadians(60 - i * 20)).UnitVector();
                        Projectile.NewProjectileDirect(
                        Projectile.GetSource_FromThis(),
                        Owner.Center + vr * 20f,
                        vr * 20f,
                        ModContent.ProjectileType<EonBolts>(),
                        (int)(ArkoftheCosmos.SwirlBoltDamageMultiplier / ArkoftheCosmos.SwirlBoltAmount * Projectile.damage),
                        0f,
                        Owner.whoAmI,
                        0.55f,
                        MathF.PI / 20f
                        )
                        .timeLeft = 100;
                    }
                }
                if (mode == 1) {
                    if (arkoftheCosmos.Charge > 0) {
                        float randomRotOffset = Main.rand.NextFloat(MathHelper.TwoPi);
                        for (int i = 0; i < 13; i++) {
                            Vector2 vr = (MathHelper.TwoPi / 13 * i + randomRotOffset).ToRotationVector2();
                            Projectile.NewProjectileDirect(
                            Projectile.GetSource_FromThis(),
                            Owner.Center + vr * 30f,
                            vr * 12f,
                            ModContent.ProjectileType<SlaughterEonBolts>(),
                            (int)(ArkoftheCosmos.SwirlBoltDamageMultiplier / ArkoftheCosmos.SwirlBoltAmount * Projectile.damage),
                            0f,
                            Owner.whoAmI,
                            0.55f,
                            MathF.PI / 20f
                            );
                        }
                    }
                }
            }
            shootBool = false;
        }

        public override void AI() {
            if (Owner == null) {
                Projectile.Kill();
                return;
            }

            if (arkoftheCosmos == null) {
                Projectile.Kill();
                return;
            }

            if (!initialized) {
                Projectile.timeLeft = Thrown ? 140 : (int)MaxSwingTime;
                SoundStyle style = Charge > 0f || Thrown ? CommonCalamitySounds.LouderPhantomPhoenix : SoundID.Item71;
                SoundEngine.PlaySound(in style, Projectile.Center);
                direction = Projectile.velocity;
                direction.Normalize();
                Projectile.velocity = direction;
                Projectile.rotation = direction.ToRotation();
                if (SwirlSwing) {
                    Projectile.localNPCHitCooldown = (int)(Projectile.localNPCHitCooldown / 4f);
                }

                initialized = true;
                Projectile.netUpdate = true;
                Projectile.netSpam = 0;
            }

            if (!Thrown) {
                Projectile.Center = Owner.Center + DistanceFromPlayer;
                if (!SwirlSwing) {
                    float offsetRot = MathHelper.Lerp(SwingWidth / 2f * SwingDirection, (0f - SwingWidth) / 2f * SwingDirection, SwingRatio());
                    if (Projectile.IsOwnedByLocalPlayer())
                        ShootConstellations(0);
                    Projectile.rotation = Projectile.velocity.ToRotation() + offsetRot;
                }
                else {
                    float value = MathF.PI * 3f / 4f * SwingDirection;
                    float value2 = -7.46128273f * SwingDirection;
                    Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.Lerp(value, value2, SwirlRatio());
                    DoParticleEffects(swirlSwing: true);
                    if (Projectile.IsOwnedByLocalPlayer())
                        ShootConstellations(1);
                }

                Projectile.scale = 1.2f + (float)Math.Sin(SwingRatio() * MathF.PI) * 0.6f + Charge / 10f * 0.2f;
            }
            else {
                if (Math.Abs(ThrowCompletion - 0.2f + 0.1f) <= 0.005f && ChanceMissed == 0f && Projectile.IsOwnedByLocalPlayer()) {
                    GeneralParticleHandler.SpawnParticle(new PulseRing(Projectile.Center, Vector2.Zero, Color.OrangeRed, 0.05f, 1.8f, 8));
                    SoundEngine.PlaySound(in SoundID.Item4);
                    Projectile.NewProjectileDirect(
                        Projectile.GetSource_FromThis(),
                        Owner.Center,
                        Vector2.Zero,
                        ModContent.ProjectileType<ArkoftheCosmosConstellations>(),
                        (int)(Projectile.damage * ArkoftheCosmos.chainDamageMultiplier),
                        0f,
                        Owner.whoAmI,
                        (int)(Projectile.timeLeft / 2f)
                        )
                        .timeLeft = (int)(Projectile.timeLeft / 2f);//发射制造星链特效的弹幕
                }

                Projectile.Center = Vector2.Lerp(Projectile.Center, Owner.Calamity().mouseWorld, 0.025f * ThrowRatio());
                Projectile.Center = CWRUtils.InPosMoveTowards(Projectile.Center, Owner.Calamity().mouseWorld, 20f * ThrowRatio());
                if ((Projectile.Center - Owner.Center).Length() > ArkoftheCosmos.MaxThrowReach)//如果刀刃抛出的距离超过了武器的最大设置范围
                {
                    float lengs = ArkoftheCosmos.MaxThrowReach;
                    if (Projectile.IsOwnedByLocalPlayer())//修改为根据玩家到鼠标的距离来限制刀刃的抛出
                    {
                        lengs = Owner.Center.To(Main.MouseWorld).Length();
                    }
                    Projectile.Center = Owner.Center + Owner.DirectionTo(Projectile.Center) * lengs;
                }

                Projectile.rotation -= 213f / 904f;
                Projectile.scale = 1f + ThrowScaleRatio() * 0.5f;
                if (Math.Abs(ThrowCompletion - 0.75f) <= 0.005f) {
                    direction = Projectile.Center - Owner.Center;
                }

                if (ThrowCompletion > 0.75f) {
                    Projectile.Center = Owner.Center + direction * ThrowRatio();
                }

                if (!OwnerCanShoot && Combo == 2f && ThrowCompletion >= 0.1f && ThrowCompletion < 0.75f && ChanceMissed == 0f) {
                    GeneralParticleHandler.SpawnParticle(new GenericSparkle(Projectile.Center, Owner.velocity - Projectile.velocity.SafeNormalize(Vector2.Zero), Color.White, Color.OrangeRed, Main.rand.NextFloat(1f, 2f), 10 + Main.rand.Next(10), 0.1f, 3f));
                    if (Main.LocalPlayer.Calamity().GeneralScreenShakePower < 3f) {
                        Main.LocalPlayer.Calamity().GeneralScreenShakePower = 3f;
                    }

                    if (Owner.whoAmI == Main.myPlayer) {
                        float num = MathF.PI * 2f * Main.rand.NextFloat();
                        for (int i = 0; i < 3; i++) {
                            Projectile.NewProjectileDirect(Projectile.GetSource_FromThis(), Projectile.Center + (MathF.PI * 2f * (i / 3f) + num).ToRotationVector2() * 30f, (MathF.PI * 2f * (i / 3f) + num).ToRotationVector2() * 20f, ModContent.ProjectileType<EonBolt>(), (int)(ArkoftheCosmos.SnapBoltsDamageMultiplier * Projectile.damage), 0f, Owner.whoAmI, 0.55f, MathF.PI / 20f).timeLeft = 100;
                        }

                        for (int j = 0; j < Main.maxNPCs; j++) {
                            Projectile.localNPCImmunity[j] = 0;
                        }
                    }

                    Combo = 3f;
                    direction = Projectile.Center - Owner.Center;
                    Projectile.velocity = Projectile.rotation.ToRotationVector2();
                    Projectile.timeLeft = (int)SnapEndTime;
                    Projectile.localNPCHitCooldown = (int)SnapEndTime;
                }
                else if (!OwnerCanShoot && Combo == 2f && ChanceMissed == 0f) {
                    ChanceMissed = 1f;
                }

                if (Combo == 3f) {
                    float num2 = MathHelper.Lerp(1f, 0.8f, 1f - (float)Math.Sqrt(1f - (float)Math.Pow(SnapEndCompletion, 2.0)));
                    Projectile.Center = Owner.Center + direction * num2;
                    Projectile.scale = 1.5f;
                    float amount = MathF.Sqrt(1f - MathF.Pow(MathHelper.Clamp(SnapEndCompletion + 0.2f, 0f, 1f) - 1f, 2.0f));
                    float num3 = direction.ToRotation() + MathF.PI / 4f > Projectile.velocity.ToRotation() ? MathF.PI * -2f : 0f;
                    Projectile.rotation = MathHelper.Lerp(Projectile.velocity.ToRotation(), direction.ToRotation() + num3, amount);
                }

                DoParticleEffects(swirlSwing: false);
            }

            Owner.heldProj = Projectile.whoAmI;
            Owner.direction = Math.Sign(Projectile.velocity.X);
            Owner.itemRotation = Projectile.rotation;
            if (Owner.direction != 1) {
                Owner.itemRotation -= MathF.PI;
            }

            Owner.itemRotation = MathHelper.WrapAngle(Owner.itemRotation);
        }

        public void DoParticleEffects(bool swirlSwing) {
            if (swirlSwing) {
                Projectile.scale = 1.6f + (float)Math.Sin(SwirlRatio() * MathF.PI) * 1f + Charge / 10f * 0.05f;
                Color color = Color.Chocolate * (MathHelper.Clamp((float)Math.Sin((SwirlRatio() - 0.2f) * MathF.PI), 0f, 1f) * 0.8f);
                if (smear == null) {
                    smear = new CircularSmearSmokeyVFX(Owner.Center, color, Projectile.rotation, Projectile.scale * 2.4f);
                    GeneralParticleHandler.SpawnParticle(smear);
                }
                else {
                    smear.Rotation = Projectile.rotation + MathF.PI / 4f + (Owner.direction < 0 ? MathF.PI : 0f);
                    smear.Time = 0;
                    smear.Position = Owner.Center;
                    smear.Scale = MathHelper.Lerp(2.6f, 3.5f, (Projectile.scale - 1.6f) / 1f);
                    smear.Color = color;
                }

                if (Main.rand.NextBool()) {
                    float num = Projectile.scale * 78f;
                    Vector2 vector = Main.rand.NextVector2Circular(num, num);
                    Vector2 vector2 = vector.RotatedBy(MathF.PI / 2f * Owner.direction).SafeNormalize(Vector2.Zero) * 2f * (1f + vector.Length() / 15f);
                    GeneralParticleHandler.SpawnParticle(new CritSpark(Owner.Center + vector, Owner.velocity + vector2, Main.rand.Next(3) == 0 ? Color.Turquoise : Color.Coral, color, 1f + 1f * (vector.Length() / num), 10, 0.05f, 3f));
                }

                float num2 = MathHelper.Clamp(MathHelper.Clamp((float)Math.Sin((SwirlRatio() - 0.2f) * MathF.PI), 0f, 1f) * 2f, 0f, 1f) * 0.25f;
                float num3 = MathHelper.Clamp(MathHelper.Clamp((float)Math.Sin((SwirlRatio() - 0.2f) * MathF.PI), 0f, 1f), 0f, 1f);
                if (!Main.rand.NextBool()) {
                    return;
                }

                for (float num4 = 0f; num4 <= 1f; num4 += 0.5f) {
                    Vector2 position = Owner.Center + Projectile.rotation.ToRotationVector2() * (30f + 50f * num4) * Projectile.scale + Projectile.rotation.ToRotationVector2().RotatedBy(-1.5707963705062866) * 30f * num3 * Main.rand.NextFloat();
                    Vector2 velocity = Projectile.rotation.ToRotationVector2().RotatedBy(-MathF.PI / 2f * Owner.direction) * 20f * num3 + Owner.velocity;
                    GeneralParticleHandler.SpawnParticle(new HeavySmokeParticle(position, velocity, Color.Lerp(Color.DodgerBlue, Color.MediumVioletRed, num4), 6 + Main.rand.Next(5), num3 * Main.rand.NextFloat(2.8f, 3.1f), num2 + Main.rand.NextFloat(0f, 0.2f), 0f, glowing: false, 0f, required: true));
                    if (Main.rand.NextBool(3)) {
                        GeneralParticleHandler.SpawnParticle(new HeavySmokeParticle(position, velocity, Main.rand.NextBool(5) ? Color.Gold : Color.Chocolate, 5, num3 * Main.rand.NextFloat(2f, 2.4f), num2 * 2.5f, 0f, glowing: true, 0.004f, required: true));
                    }
                }

                return;
            }

            Color color2 = Main.hslToRgb((SwingTimer - MaxSwingTime * 0.5f) / (MaxSwingTime * 0.5f) * 0.15f, 1f, 0.8f);
            float num5 = (Combo == 3f ? (float)Math.Sin(SnapEndCompletion * (MathF.PI / 2f) + MathF.PI / 2f) : (float)Math.Sin(ThrowCompletion * MathF.PI)) * 0.5f;
            if (smear == null) {
                if (Charge <= 0f) {
                    smear = new TrientCircularSmear(Projectile.Center, color2 * num5, Projectile.rotation, Projectile.scale * 1.7f);
                }
                else {
                    smear = new CircularSmearSmokeyVFX(Projectile.Center, color2 * num5, Projectile.rotation, Projectile.scale * 1.7f);
                }

                GeneralParticleHandler.SpawnParticle(smear);
            }
            else {
                smear.Rotation = Projectile.rotation - MathF.PI * 7f / 8f;
                smear.Time = 0;
                smear.Position = Projectile.Center;
                smear.Scale = Projectile.scale * 1.65f;
                smear.Color = color2 * num5;
            }

            if (Combo != 2f) {
                return;
            }

            if (Main.rand.NextBool()) {
                float num6 = Projectile.scale * 78f;
                Vector2 vector3 = Main.rand.NextVector2Circular(num6, num6);
                Vector2 vector4 = vector3.RotatedBy(-1.5707963705062866).SafeNormalize(Vector2.Zero) * 2f * (1f + vector3.Length() / 15f);
                Color bloom = Main.hslToRgb(Main.rand.NextFloat(), 1f, 0.5f);
                GeneralParticleHandler.SpawnParticle(new CritSpark(Projectile.Center + vector3, Owner.velocity + vector4, Color.White, bloom, 1f + 1f * (vector3.Length() / num6), 10, 0.05f, 3f));
            }

            num5 = 0.25f;
            float num7 = 0.7f;
            if (!Main.rand.NextBool()) {
                return;
            }

            for (float num8 = 0.5f; num8 <= 1f; num8 += 0.5f) {
                Vector2 position2 = Projectile.Center + Projectile.rotation.ToRotationVector2() * (60f * num8) * Projectile.scale + Projectile.rotation.ToRotationVector2().RotatedBy(-1.5707963705062866) * 30f * num7 * Main.rand.NextFloat();
                Vector2 velocity2 = Projectile.rotation.ToRotationVector2().RotatedBy(1.5707963705062866) * 20f * num7 + Owner.velocity;
                GeneralParticleHandler.SpawnParticle(new HeavySmokeParticle(position2, velocity2, Color.Lerp(Color.DodgerBlue, Color.MediumVioletRed, num8), 10 + Main.rand.Next(5), num7 * Main.rand.NextFloat(2.8f, 3.1f), num5 + Main.rand.NextFloat(0f, 0.2f), 0f, glowing: false, 0f, required: true));
                if (Main.rand.NextBool(3)) {
                    GeneralParticleHandler.SpawnParticle(new HeavySmokeParticle(position2, velocity2, Main.rand.Next(5) == 0 ? Color.Gold : Color.Chocolate, 7, num7 * Main.rand.NextFloat(2f, 2.4f), num5 * 2.5f, 0f, glowing: true, 0.004f, required: true));
                }
            }
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {
            if (Combo == 3f) {
                modifiers.SourceDamage *= CalamityMod.Items.Weapons.Melee.ArkoftheElements.snapDamageMultiplier;
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            for (int i = 0; i < 5; i++) {
                Vector2 velocity = (target.Center - Projectile.Center).SafeNormalize(Vector2.One).RotatedByRandom(0.62831854820251465) * Main.rand.NextFloat(3.6f, 8f);
                GeneralParticleHandler.SpawnParticle(new SquishyLightParticle(target.Center, velocity, Main.rand.NextFloat(0.3f, 0.6f), Color.OrangeRed, 60, 2f, 2.5f, 3f, 0.06f));
            }

            if (Combo != 3f) {
                return;
            }

            SoundStyle style = CommonCalamitySounds.ScissorGuillotineSnapSound;
            style.Volume = CommonCalamitySounds.ScissorGuillotineSnapSound.Volume * 1.3f;
            SoundEngine.PlaySound(in style, Projectile.Center);
            if (Charge <= 1f) {
                if (arkoftheCosmos != null) {
                    arkoftheCosmos.Charge = 2f;
                }
            }
        }

        public override void OnKill(int timeLeft) {
            if (Combo == 3f) {
                if (Main.LocalPlayer.Calamity().GeneralScreenShakePower < 3f) {
                    Main.LocalPlayer.Calamity().GeneralScreenShakePower = 3f;
                }

                SoundEngine.PlaySound(in SoundID.Item84, Projectile.Center);
                Vector2 vector = direction.SafeNormalize(Vector2.One) * 40f;
                GeneralParticleHandler.SpawnParticle(new LineVFX(Projectile.Center - vector, vector * 2f, 0.2f, Color.Orange * 0.7f, concave: false, telegraph: false, 250f)
                {
                    Lifetime = 10
                });
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            if (!Thrown) {
                if (Charge > 0f) {
                    DrawSwungScissors(lightColor);
                }
                else {
                    DrawSingleSwungScissorBlade(lightColor);
                }
            }
            else if (Charge > 0f) {
                DrawThrownScissors(lightColor);
            }
            else {
                DrawSingleThrownScissorBlade(lightColor);
            }

            return false;
        }

        public void DrawSingleSwungScissorBlade(Color lightColor) {
            Texture2D value = ModContent.Request<Texture2D>(Combo == 0f ? "CalamityMod/Projectiles/Melee/SunderingScissorsRight" : "CalamityMod/Projectiles/Melee/SunderingScissorsLeft").Value;
            Texture2D value2 = ModContent.Request<Texture2D>(Combo == 0f ? "CalamityMod/Projectiles/Melee/SunderingScissorsRightGlow" : "CalamityMod/Projectiles/Melee/SunderingScissorsLeftGlow").Value;
            bool flag = Owner.direction < 0;
            SpriteEffects effects = flag ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
            float num = Owner.direction < 0 ? MathF.PI / 2f : 0f;
            float rotation = Projectile.rotation;
            float num2 = MathF.PI / 4f;
            float rotation2 = Projectile.rotation + num2 + num;
            Vector2 origin = new Vector2(flag ? value.Width : 0f, value.Height);
            Vector2 position = Owner.Center + rotation.ToRotationVector2() * 10f - Main.screenPosition;
            if (CalamityConfig.Instance.Afterimages && SwingTimer > ProjectileID.Sets.TrailCacheLength[Projectile.type] && Combo == 0f) {
                for (int i = 1; i < Projectile.oldRot.Length; i++) {
                    Color color = Main.hslToRgb(i / (float)Projectile.oldRot.Length * 0.1f, 1f, 0.6f + (Charge > 0f ? 0.3f : 0f));
                    float rotation3 = Projectile.oldRot[i] + num2 + num;
                    Main.spriteBatch.Draw(value2, position, null, color * 0.05f, rotation3, origin, Projectile.scale - 0.2f * (i / (float)Projectile.oldRot.Length), effects, 0f);
                }
            }

            Main.EntitySpriteDraw(value, position, null, lightColor, rotation2, origin, Projectile.scale, effects);
            Main.EntitySpriteDraw(value2, position, null, Color.Lerp(lightColor, Color.White, 0.75f), rotation2, origin, Projectile.scale, effects);
            if (SwingCompletion > 0.5f && Combo == 0f) {
                Texture2D value3 = ModContent.Request<Texture2D>("CalamityMod/Particles/TrientCircularSmear").Value;
                Main.spriteBatch.End();
                Main.spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.Additive, Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
                float num3 = (float)Math.Sin(SwingCompletion * MathF.PI);
                float num4 = (-MathF.PI / 8f + MathF.PI / 8f * SwingCompletion + (Combo == 1f ? MathF.PI / 4f : 0f)) * SwingDirection;
                Color color2 = Main.hslToRgb((SwingTimer - MaxSwingTime * 0.5f) / (MaxSwingTime * 0.5f) * 0.15f + (Combo == 1f ? 0.85f : 0f), 1f, 0.6f);
                Main.EntitySpriteDraw(value3, Owner.Center - Main.screenPosition, null, color2 * 0.5f * num3, Projectile.velocity.ToRotation() + MathF.PI + num4, value3.Size() / 2f, Projectile.scale * 2.3f, SpriteEffects.None);
                Main.spriteBatch.End();
                Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
            }
        }

        public void DrawSwungScissors(Color lightColor) {
            Texture2D value = ModContent.Request<Texture2D>("CalamityMod/Projectiles/Melee/SunderingScissorsLeft").Value;
            Texture2D value2 = ModContent.Request<Texture2D>("CalamityMod/Projectiles/Melee/SunderingScissorsLeftGlow").Value;
            Texture2D value3 = ModContent.Request<Texture2D>("CalamityMod/Projectiles/Melee/SunderingScissorsRight").Value;
            Texture2D value4 = ModContent.Request<Texture2D>("CalamityMod/Projectiles/Melee/SunderingScissorsRightGlow").Value;
            bool flag = Owner.direction < 0;
            SpriteEffects effects = flag ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
            float num = flag ? MathF.PI / 2f : 0f;
            float rotation = Projectile.rotation;
            float num2 = MathF.PI / 4f;
            float rotation2 = Projectile.rotation + num2 + num;
            Vector2 origin = new Vector2(flag ? value.Width : 0f, value.Height);
            Vector2 position = Owner.Center + rotation.ToRotationVector2() * 10f - Main.screenPosition;
            Vector2 origin2 = new Vector2(flag ? 90f : 44f, 86f);
            Vector2 position2 = Owner.Center + rotation.ToRotationVector2() * 10f + (rotation.ToRotationVector2() * 56f + (rotation - MathF.PI / 2f).ToRotationVector2() * 11f * Owner.direction) * Projectile.scale - Main.screenPosition;
            if (CalamityConfig.Instance.Afterimages && SwingTimer > ProjectileID.Sets.TrailCacheLength[Projectile.type]) {
                Texture2D value5 = ModContent.Request<Texture2D>("CalamityMod/Projectiles/Melee/SunderingScissorsGlow").Value;
                for (int i = 1; i < Projectile.oldRot.Length; i++) {
                    Color color = Main.hslToRgb(i / (float)Projectile.oldRot.Length * 0.1f, 1f, 0.6f + (Charge > 0f ? 0.3f : 0f));
                    float rotation3 = Projectile.oldRot[i] + num2 + num;
                    Main.EntitySpriteDraw(value5, position, null, color * 0.15f, rotation3, origin, Projectile.scale - 0.2f * (i / (float)Projectile.oldRot.Length), effects);
                }
            }

            Main.EntitySpriteDraw(value3, position2, null, lightColor, rotation2, origin2, Projectile.scale, effects);
            Main.EntitySpriteDraw(value4, position2, null, Color.Lerp(lightColor, Color.White, 0.75f), rotation2, origin2, Projectile.scale, effects);
            Main.EntitySpriteDraw(value, position, null, lightColor, rotation2, origin, Projectile.scale, effects);
            Main.EntitySpriteDraw(value2, position, null, Color.Lerp(lightColor, Color.White, 0.75f), rotation2, origin, Projectile.scale, effects);
            if (SwingCompletion > 0.5f && Combo == 0f) {
                Texture2D value6 = ModContent.Request<Texture2D>("CalamityMod/Particles/TrientCircularSmear").Value;
                Main.spriteBatch.End();
                Main.spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.Additive, Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
                float num3 = (float)Math.Sin(SwingCompletion * MathF.PI);
                float num4 = (-MathF.PI / 8f + MathF.PI / 8f * SwingCompletion + (Combo == 1f ? MathF.PI / 4f : 0f)) * SwingDirection;
                Color color2 = Main.hslToRgb((SwingTimer - MaxSwingTime * 0.5f) / (MaxSwingTime * 0.5f) * 0.15f + (Combo == 1f ? 0.85f : 0f), 1f, 0.6f);
                Main.EntitySpriteDraw(value6, Owner.Center - Main.screenPosition, null, color2 * 0.5f * num3, Projectile.velocity.ToRotation() + MathF.PI + num4, value6.Size() / 2f, Projectile.scale * 2.3f, SpriteEffects.None);
                Main.spriteBatch.End();
                Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
            }
        }

        public void DrawSingleThrownScissorBlade(Color lightColor) {
            Texture2D value = ModContent.Request<Texture2D>("CalamityMod/Projectiles/Melee/SunderingScissorsLeft").Value;
            Texture2D value2 = ModContent.Request<Texture2D>("CalamityMod/Projectiles/Melee/SunderingScissorsLeftGlow").Value;
            if (Combo == 3f) {
                Texture2D value3 = ModContent.Request<Texture2D>("CalamityMod/Projectiles/Melee/SunderingScissorsRight").Value;
                Texture2D value4 = ModContent.Request<Texture2D>("CalamityMod/Projectiles/Melee/SunderingScissorsRightGlow").Value;
                Vector2 vector = Vector2.SmoothStep(Owner.Center, Projectile.Center, MathHelper.Clamp(SnapEndCompletion + 0.25f, 0f, 1f));
                float rotation = direction.ToRotation() + MathF.PI / 4f;
                Vector2 origin = new Vector2(44f, 86f);
                Main.EntitySpriteDraw(value3, vector - Main.screenPosition, null, lightColor, rotation, origin, Projectile.scale, SpriteEffects.None);
                Main.EntitySpriteDraw(value4, vector - Main.screenPosition, null, Color.Lerp(lightColor, Color.White, 0.75f), rotation, origin, Projectile.scale, SpriteEffects.None);
            }

            Vector2 center = Projectile.Center;
            float rotation2 = Projectile.rotation + MathF.PI / 4f;
            Vector2 origin2 = new Vector2(32f, 86f);
            Main.EntitySpriteDraw(value, center - Main.screenPosition, null, lightColor, rotation2, origin2, Projectile.scale, SpriteEffects.None);
            Main.EntitySpriteDraw(value2, center - Main.screenPosition, null, Color.Lerp(lightColor, Color.White, 0.75f), rotation2, origin2, Projectile.scale, SpriteEffects.None);
        }

        public void DrawThrownScissors(Color lightColor) {
            Texture2D value = ModContent.Request<Texture2D>("CalamityMod/Projectiles/Melee/SunderingScissorsLeft").Value;
            Texture2D value2 = ModContent.Request<Texture2D>("CalamityMod/Projectiles/Melee/SunderingScissorsLeftGlow").Value;
            Texture2D value3 = ModContent.Request<Texture2D>("CalamityMod/Projectiles/Melee/SunderingScissorsRight").Value;
            Texture2D value4 = ModContent.Request<Texture2D>("CalamityMod/Projectiles/Melee/SunderingScissorsRightGlow").Value;
            Vector2 center = Projectile.Center;
            Vector2 origin = new Vector2(32f, 86f);
            float rotation = Projectile.rotation + MathF.PI / 4f;
            Vector2 origin2 = new Vector2(44f, 86f);
            float rotation2 = Projectile.rotation + MathHelper.Lerp(MathF.PI / 4f, MathF.PI * 133f / 200f, MathHelper.Clamp(ThrowCompletion * 2f, 0f, 1f));
            if (Combo == 3f) {
                rotation2 = Projectile.rotation + MathHelper.Lerp(MathF.PI * 133f / 200f, MathF.PI / 4f, MathHelper.Clamp(SnapEndCompletion + 0.5f, 0f, 1f));
            }

            Main.EntitySpriteDraw(value3, center - Main.screenPosition, null, lightColor, rotation2, origin2, Projectile.scale, SpriteEffects.None);
            Main.EntitySpriteDraw(value4, center - Main.screenPosition, null, Color.Lerp(lightColor, Color.White, 0.75f), rotation2, origin2, Projectile.scale, SpriteEffects.None);
            Main.EntitySpriteDraw(value, center - Main.screenPosition, null, lightColor, rotation, origin, Projectile.scale, SpriteEffects.None);
            Main.EntitySpriteDraw(value2, center - Main.screenPosition, null, Color.Lerp(lightColor, Color.White, 0.75f), rotation, origin, Projectile.scale, SpriteEffects.None);
        }

        public override void SendExtraAI(BinaryWriter writer) {
            writer.Write(initialized);
            writer.WriteVector2(direction);
            writer.Write(ChanceMissed);
            writer.Write(ThrowReach);
        }

        public override void ReceiveExtraAI(BinaryReader reader) {
            initialized = reader.ReadBoolean();
            direction = reader.ReadVector2();
            ChanceMissed = reader.ReadSingle();
            ThrowReach = reader.ReadSingle();
        }
    }
}
