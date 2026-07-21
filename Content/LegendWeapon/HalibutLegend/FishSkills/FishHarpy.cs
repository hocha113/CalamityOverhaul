using InnoVault.GameContent.BaseEntity;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.HalibutLegend.FishSkills
{
    internal class FishHarpy : FishSkill
    {
        public override int UnlockFishID => ItemID.Harpyfish;
        public override int DefaultCooldown => 30 - HalibutData.GetDomainLayer() * 2;
        public override int ResearchDuration => 60 * 20;
        //活跃羽毛索引
        public static List<int> ActiveFeathers = new();
        private static int MaxFeathers => 5 + HalibutData.GetDomainLayer();

        public override bool? Shoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source,
            Vector2 position, Vector2 velocity, int type, int damage, float knockback) {

            if (Cooldown <= 0) {
                SetCooldown();
                CleanupInactiveFeathers();

                if (ActiveFeathers.Count < MaxFeathers) {
                    int featherProj = Projectile.NewProjectile(
                        source,
                        player.Center,
                        Vector2.Zero,
                        ModContent.ProjectileType<HarpyFeatherOrbit>(),
                        (int)(damage * (2 + HalibutData.GetDomainLayer() * 0.5)),
                        knockback * 0.25f,
                        player.whoAmI,
                        ai0: ActiveFeathers.Count
                    );

                    if (featherProj >= 0 && featherProj < Main.maxProjectiles) {
                        ActiveFeathers.Add(featherProj);
                        SpawnSummonEffect(player.Center);

                        SoundEngine.PlaySound(SoundID.Item32 with {
                            Volume = 0.5f,
                            Pitch = 0.2f + ActiveFeathers.Count * 0.04f
                        }, player.Center);

                        if (ActiveFeathers.Count >= MaxFeathers) {
                            NotifyFeathersToLaunch(player);
                        }
                    }
                }
            }

            return null;
        }

        private static void CleanupInactiveFeathers() {
            //ai[1] >= 4 覆盖 Launching 与 Fading，两者都不再占羽环位
            ActiveFeathers.RemoveAll(id => {
                if (!id.TryGetProjectile(out var proj)) return true;
                if (proj.type != ModContent.ProjectileType<HarpyFeatherOrbit>()) return true;
                if (proj.ai[1] >= 4) return true;
                return false;
            });
        }

        private void NotifyFeathersToLaunch(Player player) {
            SoundEngine.PlaySound(SoundID.Item30 with {
                Volume = 0.7f,
                Pitch = 0.6f
            }, player.Center);

            SoundEngine.PlaySound(SoundID.DD2_WitherBeastAuraPulse with {
                Volume = 0.5f,
                Pitch = 0.8f
            }, player.Center);

            FishHarpyVFX.ChargeCue(player.Center, 130f);
        }

        private void SpawnSummonEffect(Vector2 position) {
            //新羽自玩家身侧抽出
            FishHarpyVFX.DownBurst(position, -Vector2.UnitY, 3, 2.2f);
            for (int i = 0; i < 5; i++) {
                Dust air = Dust.NewDustPerfect(
                    position + Main.rand.NextVector2Circular(12f, 12f),
                    DustID.Cloud,
                    Main.rand.NextVector2Circular(1.6f, 1.6f),
                    150,
                    FishHarpyVFX.Cream,
                    Main.rand.NextFloat(1.0f, 1.6f)
                );
                air.noGravity = true;
                air.fadeIn = 1.05f;
            }
        }
    }

    internal class HarpyFeatherOrbit : BaseHeldProj
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.HarpyFeather;

        private enum FeatherState
        {
            Gathering,
            Floating,
            Orbiting,
            Charging,
            Launching,
            Fading
        }

        private FeatherState State {
            get => (FeatherState)Projectile.ai[1];
            set => Projectile.ai[1] = (float)value;
        }

        private ref float StateTimer => ref Projectile.localAI[0];
        private ref float GlobalOrbitAngle => ref Projectile.localAI[1];
        //飞出目标速度
        private ref float LaunchTargetSpeed => ref Projectile.ai[2];

        private const float orbitRadius = 140f;
        private float orbitSpeed = 0.03f;
        private const float MaxOrbitSpeed = 0.15f;

        private float floatPhase = 0f;
        private const float floatFrequency = 0.08f;
        //钟摆悬长
        private const float PendulumLength = 34f;

        private const int GatherDuration = 25;
        private const int FloatDuration = 35;
        private const int ChargeDuration = 30;
        private const float LaunchSpeed = 22f;

        private float glowIntensity = 0f;
        private float swayAngle = 0f;
        //钟摆相位与羽轴自旋（充能期的旋转拖影来源）
        private float pendulumPhase = 0f;
        private float spinPhase = 0f;
        private float spinRate = 0f;
        private int launchTicks = 0;
        //落叶飘行相位
        private float flutterPhase = 0f;

        private int launchCountdown = 0;
        private const int LaunchDelay = 20;

        private float SpeedT => LaunchTargetSpeed > 0f
            ? MathHelper.Clamp(Projectile.velocity.Length() / LaunchTargetSpeed, 0f, 1f) : 1f;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 10;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 16;
            Projectile.height = 16;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = 2;
            Projectile.timeLeft = 600;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 20;
            Projectile.alpha = 255;

            floatPhase = Main.rand.NextFloat(MathHelper.TwoPi);
            pendulumPhase = Main.rand.NextFloat(MathHelper.TwoPi);
        }

        public override void AI() {
            if (!Owner.active || Owner.dead) {
                Projectile.Kill();
                return;
            }

            bool skillActive = FishSkill.GetT<FishHarpy>().Active(Owner);

            if (!skillActive && State != FeatherState.Launching && State != FeatherState.Fading) {
                if (State == FeatherState.Orbiting || State == FeatherState.Charging) {
                    LaunchFeather(Owner);
                }
                else {
                    //聚合中被打断，收羽淡出而非瞬灭
                    State = FeatherState.Fading;
                    StateTimer = 0;
                }
                return;
            }

            StateTimer++;

            if (State == FeatherState.Orbiting) {
                int totalFeathers = GetActiveFeatherCount(Owner);
                int maxFeathers = 5 + HalibutData.GetDomainLayer();

                if (totalFeathers >= maxFeathers && StateTimer >= 30) {
                    SyncAllFeathersToCharging(Owner);
                }
            }

            switch (State) {
                case FeatherState.Gathering:
                    GatheringPhaseAI(Owner);
                    break;

                case FeatherState.Floating:
                    FloatingPhaseAI(Owner);
                    break;

                case FeatherState.Orbiting:
                    OrbitingPhaseAI(Owner);
                    break;

                case FeatherState.Charging:
                    ChargingPhaseAI(Owner);
                    break;

                case FeatherState.Launching:
                    LaunchingPhaseAI();
                    break;

                case FeatherState.Fading:
                    FadingPhaseAI();
                    break;
            }

            //暖金弱光
            float lightIntensity = glowIntensity * 0.3f;
            Lighting.AddLight(Projectile.Center,
                0.62f * lightIntensity,
                0.55f * lightIntensity,
                0.38f * lightIntensity);
        }

        private int GetActiveFeatherCount(Player owner) {
            int count = 0;
            for (int i = 0; i < Main.maxProjectiles; i++) {
                if (Main.projectile[i].active &&
                    Main.projectile[i].type == Projectile.type &&
                    Main.projectile[i].owner == owner.whoAmI &&
                    Main.projectile[i].ai[1] < 4) {
                    count++;
                }
            }
            return count;
        }

        private void SyncAllFeathersToCharging(Player owner) {
            for (int i = 0; i < Main.maxProjectiles; i++) {
                if (Main.projectile[i].active &&
                    Main.projectile[i].type == Projectile.type &&
                    Main.projectile[i].owner == owner.whoAmI &&
                    Main.projectile[i].ai[1] < 4) {

                    Main.projectile[i].ai[1] = (float)FeatherState.Charging;
                    Main.projectile[i].localAI[0] = 0;

                    if (Main.projectile[i].ModProjectile is HarpyFeatherOrbit feather) {
                        feather.launchCountdown = LaunchDelay;
                    }
                }
            }

            SoundEngine.PlaySound(SoundID.Item30 with {
                Volume = 0.7f,
                Pitch = 0.6f
            }, owner.Center);

            SoundEngine.PlaySound(SoundID.DD2_WitherBeastAuraPulse with {
                Volume = 0.5f,
                Pitch = 0.8f
            }, owner.Center);

            FishHarpyVFX.ChargeCue(owner.Center, orbitRadius * 0.92f);
        }

        private void GatheringPhaseAI(Player owner) {
            float progress = StateTimer / GatherDuration;

            int myIndex = GetMyFeatherIndex(owner);
            int totalFeathers = GetActiveFeatherCount(owner);
            float targetAngle = MathHelper.TwoPi * myIndex / Math.Max(totalFeathers, 1);

            Vector2 targetPos = owner.Center + targetAngle.ToRotationVector2() * orbitRadius;

            float easeProgress = VaultUtils.EaseOutSine(progress);

            Vector2 driftOffset = new Vector2(
                (float)Math.Sin(Main.GlobalTimeWrappedHourly * 2f + floatPhase) * 15f * (1f - easeProgress),
                (float)Math.Cos(Main.GlobalTimeWrappedHourly * 1.5f + floatPhase) * 12f * (1f - easeProgress)
            );

            Projectile.Center = Vector2.Lerp(Projectile.Center, targetPos + driftOffset, easeProgress * 0.3f);

            GlobalOrbitAngle = targetAngle;
            glowIntensity = MathHelper.Lerp(0f, 0.4f, progress);

            //materialize
            Projectile.alpha = (int)MathHelper.Max(0, Projectile.alpha - 18);
            Projectile.scale = MathHelper.Lerp(0.55f, 1f, EaseOutBack(progress));
            swayAngle = (float)Math.Sin(Main.GlobalTimeWrappedHourly * 3f + floatPhase) * 0.15f;

            if (Main.rand.NextBool(5)) {
                SpawnGatherParticle(owner);
            }

            if (StateTimer >= GatherDuration) {
                State = FeatherState.Floating;
                StateTimer = 0;

                SoundEngine.PlaySound(SoundID.Item32 with {
                    Volume = 0.3f,
                    Pitch = 0.3f
                }, Projectile.Center);
            }
        }

        private void FloatingPhaseAI(Player owner) {
            float progress = StateTimer / FloatDuration;

            int myIndex = GetMyFeatherIndex(owner);
            int totalFeathers = GetActiveFeatherCount(owner);
            float targetAngle = MathHelper.TwoPi * myIndex / Math.Max(totalFeathers, 1);

            GlobalOrbitAngle = MathHelper.Lerp(GlobalOrbitAngle, targetAngle, 0.08f);

            float currentRadius = orbitRadius + (float)Math.Sin(StateTimer * 0.05f + floatPhase) * 5f;
            Vector2 orbitPos = owner.Center + GlobalOrbitAngle.ToRotationVector2() * currentRadius;

            //钟摆飘浮
            pendulumPhase += floatFrequency;
            float theta = (float)Math.Sin(pendulumPhase) * 0.62f;
            Vector2 pivot = orbitPos - new Vector2(0f, PendulumLength);
            Vector2 targetPos = pivot + new Vector2((float)Math.Sin(theta), (float)Math.Cos(theta)) * PendulumLength;
            //慢沉浮叠加
            targetPos.Y += (float)Math.Sin(StateTimer * 0.03f + floatPhase) * 4f;

            Projectile.Center = Vector2.Lerp(Projectile.Center, targetPos, 0.25f);
            swayAngle = theta * 0.85f;

            glowIntensity = MathHelper.Lerp(0.4f, 0.6f, progress);

            //待机绒羽偶发剥落
            if (Main.rand.NextBool(46)) {
                FishHarpyVFX.DownBurst(Projectile.Center, Vector2.UnitY, 1, 0.8f);
            }
            if (Main.rand.NextBool(12)) {
                SpawnFloatParticle();
            }

            if (StateTimer >= FloatDuration) {
                State = FeatherState.Orbiting;
                StateTimer = 0;

                SoundEngine.PlaySound(SoundID.Item30 with {
                    Volume = 0.35f,
                    Pitch = 0.4f
                }, Projectile.Center);
            }
        }

        private void OrbitingPhaseAI(Player owner) {
            float timeProgress = MathHelper.Clamp(StateTimer / 60f, 0f, 1f);

            float speedProgress = VaultUtils.EaseInOutQuad(timeProgress);
            orbitSpeed = MathHelper.Lerp(0.03f, MaxOrbitSpeed, speedProgress);

            float radiusScale = MathHelper.Lerp(1f, 0.92f, MathHelper.Clamp(speedProgress, 0f, 1f));
            float radiusWave = (float)Math.Sin(StateTimer * 0.2f + floatPhase) * 6f * (1f - speedProgress * 0.5f);
            float currentRadius = orbitRadius * radiusScale + radiusWave;

            GlobalOrbitAngle -= orbitSpeed;

            //转速上来后离心力把摆幅甩平
            pendulumPhase += floatFrequency * (1f + speedProgress * 0.8f);
            float swingAmp = MathHelper.Lerp(0.62f, 0.16f, speedProgress);
            float theta = (float)Math.Sin(pendulumPhase) * swingAmp;
            Vector2 orbitPos = owner.Center + GlobalOrbitAngle.ToRotationVector2() * currentRadius;
            Vector2 pivot = orbitPos - new Vector2(0f, PendulumLength);
            Vector2 targetPos = pivot + new Vector2((float)Math.Sin(theta), (float)Math.Cos(theta)) * PendulumLength;

            Projectile.Center = Vector2.Lerp(Projectile.Center, targetPos, 0.35f);
            swayAngle = theta * 0.85f;

            glowIntensity = MathHelper.Lerp(0.6f, 0.8f, timeProgress);

            if (Main.rand.NextBool(7)) {
                SpawnOrbitParticle(timeProgress);
            }

            if (StateTimer % (int)MathHelper.Lerp(30, 15, timeProgress) == 0) {
                SoundEngine.PlaySound(SoundID.Item32 with {
                    Volume = 0.2f + 0.15f * timeProgress,
                    Pitch = 0.3f + timeProgress * 0.3f
                }, Projectile.Center);
            }
        }

        private void ChargingPhaseAI(Player owner) {
            float progress = StateTimer / ChargeDuration;

            orbitSpeed = MaxOrbitSpeed;

            //盘旋收紧
            float tighten = MathHelper.Clamp(StateTimer / (float)LaunchDelay, 0f, 1f);
            bool brace = launchCountdown <= 5;
            float radiusMul = MathHelper.Lerp(0.92f, 0.78f, VaultUtils.EaseInOutQuad(tighten));
            float radiusOsc = (float)Math.Sin(StateTimer * 0.6f + floatPhase) * 5f * (1f - tighten);
            float currentRadius = orbitRadius * radiusMul + radiusOsc - (brace ? 4f : 0f);

            GlobalOrbitAngle -= orbitSpeed;

            //羽轴自旋
            spinRate = MathHelper.Lerp(spinRate, brace ? 0.05f : 0.46f, 0.12f);
            spinPhase += spinRate;
            if (brace) {
                //收旋: 残余转角向 0 快速收敛, 消掉发射帧的姿态跳变
                spinPhase = MathHelper.WrapAngle(spinPhase) * 0.45f;
            }

            pendulumPhase += floatFrequency;
            float theta = brace ? 0f : (float)Math.Sin(pendulumPhase) * 0.10f;

            Vector2 targetPos = owner.Center + GlobalOrbitAngle.ToRotationVector2() * currentRadius;
            Projectile.Center = Vector2.Lerp(Projectile.Center, targetPos, 0.4f);
            swayAngle = theta;

            glowIntensity = 0.85f + (float)Math.Sin(StateTimer * 1.2f) * 0.1f;

            //向心气流，空气被吸进收紧的羽环
            if (Main.rand.NextBool()) {
                SpawnChargeParticle(owner.Center, progress);
            }

            //环带切向涟漪，收紧的可视化预告
            if (StateTimer % 10 == 0) {
                Vector2 tangent = (GlobalOrbitAngle - MathHelper.PiOver2).ToRotationVector2();
                FishHarpyVFX.AirRipple(Projectile.Center - tangent * 8f, tangent, 0.55f);
            }

            if (StateTimer % 6 == 0) {
                SoundEngine.PlaySound(SoundID.Item32 with {
                    Volume = 0.2f + progress * 0.3f,
                    Pitch = 0.5f + progress * 0.5f
                }, Projectile.Center);
            }

            launchCountdown--;
            if (launchCountdown <= 0) {
                LaunchFeather(owner);
            }
        }

        private void SpawnChargeParticle(Vector2 ownerCenter, float progress) {
            Vector2 toCenter = (ownerCenter - Projectile.Center).SafeNormalize(Vector2.Zero);
            Vector2 velocity = toCenter * Main.rand.NextFloat(2f, 5f) * progress;

            Dust charge = Dust.NewDustPerfect(
                Projectile.Center + Main.rand.NextVector2Circular(8f, 8f),
                DustID.Cloud,
                velocity,
                130,
                FishHarpyVFX.Cream,
                Main.rand.NextFloat(1.1f, 1.7f)
            );
            charge.noGravity = true;
            charge.fadeIn = 1.15f;
        }

        private void LaunchFeather(Player owner) {
            Vector2 launchDir = (GlobalOrbitAngle - MathHelper.PiOver2).ToRotationVector2();

            float speedBonus = orbitSpeed / MaxOrbitSpeed;
            float finalSpeed = LaunchSpeed * (1f + speedBonus * 0.4f);

            LaunchTargetSpeed = finalSpeed;
            Projectile.velocity = launchDir * finalSpeed * 0.55f;
            Projectile.tileCollide = true;
            Projectile.netUpdate = true;

            State = FeatherState.Launching;
            StateTimer = 0;
            launchTicks = 0;
            flutterPhase = Projectile.identity * 2.3999632f;

            SpawnLaunchEffect();

            if (Projectile.whoAmI == GetFirstFeatherID(owner)) {
                SoundEngine.PlaySound(SoundID.Item1 with {
                    Volume = 0.6f,
                    Pitch = 0.6f
                }, Projectile.Center);
                SoundEngine.PlaySound(SoundID.Item32 with {
                    Volume = 0.5f,
                    Pitch = 0.8f
                }, Projectile.Center);
            }
        }

        private void LaunchingPhaseAI() {
            launchTicks++;

            float speed = Projectile.velocity.Length();
            if (LaunchTargetSpeed > 0f && speed < LaunchTargetSpeed && launchTicks < 20) {
                //复利加速段
                speed = MathF.Min(speed * 1.085f, LaunchTargetSpeed);
                Projectile.velocity = Projectile.velocity.SafeNormalize(Vector2.UnitX) * speed;
            }
            else {
                Projectile.velocity *= 0.998f;
            }

            //落叶式飘行
            //加速段摆幅被抽直压平，全速后展开，随飞行缓慢衰减
            flutterPhase += 0.30f;
            float flutterEnvelope = MathF.Min(launchTicks / 14f, 1f) * MathF.Pow(0.9965f, launchTicks);
            float steer = MathF.Sin(flutterPhase) * 0.052f * flutterEnvelope;
            Projectile.velocity = Projectile.velocity.RotatedBy(steer);

            if (Projectile.velocity.LengthSquared() > 0.1f) {
                Projectile.rotation = Projectile.velocity.ToRotation();
            }

            glowIntensity = 0.8f;

            //尾后空气涟漪
            float speedT = SpeedT;
            int cadence = speedT > 0.8f ? 3 : 5;
            if (launchTicks % cadence == 0) {
                FishHarpyVFX.AirRipple(Projectile.Center - Projectile.velocity * 0.8f
                    , Projectile.velocity, 0.62f + 0.5f * speedT);
            }
            //偶发绒羽剥落
            if (Main.rand.NextBool(9)) {
                FishHarpyVFX.DownBurst(Projectile.Center, -Projectile.velocity, 1, 1.2f);
            }
        }

        private void FadingPhaseAI() {
            //收羽退场
            Projectile.velocity *= 0.9f;
            Projectile.alpha = (int)MathHelper.Min(255, Projectile.alpha + 20);
            Projectile.scale *= 0.965f;
            swayAngle *= 0.9f;
            glowIntensity *= 0.85f;

            if (StateTimer == 2) {
                FishHarpyVFX.DownBurst(Projectile.Center, Vector2.UnitY, 2, 1.4f);
                SoundEngine.PlaySound(SoundID.Item32 with {
                    Volume = 0.15f,
                    Pitch = 0.5f
                }, Projectile.Center);
            }

            if (Projectile.alpha >= 255) {
                Projectile.Kill();
            }
        }

        private int GetMyFeatherIndex(Player owner) {
            int index = 0;
            for (int i = 0; i < Main.maxProjectiles; i++) {
                if (Main.projectile[i].active &&
                    Main.projectile[i].type == Projectile.type &&
                    Main.projectile[i].owner == owner.whoAmI &&
                    Main.projectile[i].ai[1] < 4) {

                    if (Main.projectile[i].whoAmI == Projectile.whoAmI) {
                        return index;
                    }
                    index++;
                }
            }
            return 0;
        }

        private int GetFirstFeatherID(Player owner) {
            for (int i = 0; i < Main.maxProjectiles; i++) {
                if (Main.projectile[i].active &&
                    Main.projectile[i].type == Projectile.type &&
                    Main.projectile[i].owner == owner.whoAmI) {
                    return Main.projectile[i].whoAmI;
                }
            }
            return Projectile.whoAmI;
        }

        private void SpawnGatherParticle(Player owner) {
            Dust gather = Dust.NewDustPerfect(
                Projectile.Center + Main.rand.NextVector2Circular(12f, 12f),
                DustID.Cloud,
                (owner.Center - Projectile.Center).SafeNormalize(Vector2.Zero) * Main.rand.NextFloat(0.5f, 2f),
                140,
                FishHarpyVFX.Cream,
                Main.rand.NextFloat(0.8f, 1.3f)
            );
            gather.noGravity = true;
            gather.fadeIn = 1f;
        }

        private void SpawnFloatParticle() {
            Vector2 velocity = Main.rand.NextVector2Circular(1f, 1f);

            Dust float_ = Dust.NewDustPerfect(
                Projectile.Center + Main.rand.NextVector2Circular(8f, 8f),
                DustID.Cloud,
                velocity,
                150,
                FishHarpyVFX.Cream,
                Main.rand.NextFloat(0.7f, 1.2f)
            );
            float_.noGravity = true;
            float_.fadeIn = 1f;
            float_.alpha = 100;
        }

        private void SpawnOrbitParticle(float progress) {
            Vector2 tangentDir = new Vector2(
                -(float)Math.Sin(GlobalOrbitAngle),
                (float)Math.Cos(GlobalOrbitAngle)
            );

            Vector2 velocity = tangentDir * Main.rand.NextFloat(1f, 3f) * progress;

            Dust orbit = Dust.NewDustPerfect(
                Projectile.Center + Main.rand.NextVector2Circular(6f, 6f),
                DustID.Cloud,
                velocity,
                140,
                FishHarpyVFX.Cream,
                Main.rand.NextFloat(0.9f, 1.5f)
            );
            orbit.noGravity = true;
            orbit.fadeIn = 1.1f;
            orbit.alpha = 80;
        }

        private void SpawnLaunchEffect() {
            //出手瞬间
            Vector2 dir = Projectile.velocity.SafeNormalize(Vector2.UnitX);
            FishHarpyVFX.DownBurst(Projectile.Center, -Projectile.velocity, 3, 2.6f);
            FishHarpyVFX.AirRipple(Projectile.Center - dir * 6f, Projectile.velocity, 0.9f);

            for (int i = 0; i < 4; i++) {
                Dust air = Dust.NewDustPerfect(
                    Projectile.Center,
                    DustID.Cloud,
                    dir.RotatedByRandom(0.6f) * Main.rand.NextFloat(1.5f, 4f),
                    150,
                    FishHarpyVFX.Cream,
                    Main.rand.NextFloat(1.0f, 1.6f)
                );
                air.noGravity = true;
                air.fadeIn = 1.1f;
            }
        }

        public override bool OnTileCollide(Vector2 oldVelocity) {
            if (Math.Abs(Projectile.velocity.X - oldVelocity.X) > float.Epsilon) {
                Projectile.velocity.X = -oldVelocity.X * 0.6f;
            }
            if (Math.Abs(Projectile.velocity.Y - oldVelocity.Y) > float.Epsilon) {
                Projectile.velocity.Y = -oldVelocity.Y * 0.6f;
            }

            SoundEngine.PlaySound(SoundID.Item32 with {
                Volume = 0.3f,
                Pitch = 0.5f
            }, Projectile.Center);

            //擦墙掉绒羽 + 一小口气流尘
            FishHarpyVFX.DownBurst(Projectile.Center, -oldVelocity, 2, 2.0f);
            for (int i = 0; i < 2; i++) {
                Dust d = Dust.NewDustPerfect(
                    Projectile.Center,
                    DustID.Cloud,
                    Main.rand.NextVector2Circular(1.5f, 1.5f),
                    150,
                    FishHarpyVFX.Cream,
                    Main.rand.NextFloat(1f, 1.4f)
                );
                d.noGravity = true;
            }

            return false;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            //羽毛炸成绒羽小簇慢落
            FishHarpyVFX.DownBurst(Projectile.Center, -Projectile.velocity, 7, 3.4f);
            FishHarpyVFX.AirRipple(Projectile.Center, Projectile.velocity, 0.8f);

            //极轻质量被肉体咬掉一口速度，穿透后的飘行读作强弩之末
            if (State == FeatherState.Launching) {
                Projectile.velocity *= 0.85f;
                Projectile.netUpdate = true;
            }

            SoundEngine.PlaySound(SoundID.NPCHit5 with {
                Volume = 0.4f,
                Pitch = 0.4f
            }, Projectile.Center);
            SoundEngine.PlaySound(SoundID.Item32 with {
                Volume = 0.3f,
                Pitch = -0.1f
            }, Projectile.Center);
        }

        public override void OnKill(int timeLeft) {
            if (Main.dedServ) {
                return;
            }
            //穿透耗尽或超时消散的兜底残迹
            FishHarpyVFX.DownBurst(Projectile.Center, -Projectile.velocity, 3, 2.2f);
            //飞行中死亡才留落羽
            if (State == FeatherState.Launching) {
                FishHarpyVFX.FeatherRemnant(Projectile.Center, Projectile.velocity * 0.35f);
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            SpriteBatch sb = Main.spriteBatch;
            Texture2D featherTex = TextureAssets.Projectile[Projectile.type].Value;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            Rectangle sourceRect = featherTex.Frame(1, 1);
            Vector2 origin = sourceRect.Size() / 2f;

            float alpha = (255f - Projectile.alpha) / 255f;

            //羽色
            float goldT = State == FeatherState.Charging
                ? MathHelper.Clamp(StateTimer / 20f, 0f, 1f) * 0.45f
                : State == FeatherState.Launching ? 0.28f : 0f;
            Color body = Color.Lerp(Color.Lerp(lightColor, FishHarpyVFX.Cream, 0.32f), FishHarpyVFX.Gold, goldT) * alpha;

            float drawRotation;
            Vector2 bodyScale = new(Projectile.scale);
            float speedT = SpeedT;
            if (State == FeatherState.Launching) {
                drawRotation = Projectile.rotation - MathHelper.PiOver2;
                //加速拉直
                float roll = 0.82f + 0.18f * MathF.Cos(flutterPhase);
                bodyScale = new Vector2((1f - 0.16f * speedT) * roll, 1f + 0.5f * speedT) * Projectile.scale;
            }
            else {
                drawRotation = GlobalOrbitAngle - MathHelper.PiOver2 + swayAngle
                    + (State == FeatherState.Charging ? spinPhase : 0f);
                //末 6 帧箭在弦上
                if (State == FeatherState.Charging && launchCountdown <= 6) {
                    float poseBlend = MathHelper.Clamp((6 - launchCountdown) / 5f, 0f, 1f);
                    drawRotation -= MathHelper.PiOver2 * poseBlend;
                }
            }

            //残影层，全部压在本体之下
            if (State == FeatherState.Launching) {
                DrawLaunchSmear(sb, featherTex, sourceRect, origin, alpha, speedT);
            }
            else if (State == FeatherState.Charging) {
                DrawSpinSmear(sb, featherTex, sourceRect, origin, alpha, drawRotation);
            }
            else if (State == FeatherState.Orbiting) {
                DrawOrbitGhosts(sb, featherTex, sourceRect, origin, alpha, drawRotation);
            }

            //充能底光
            if (State == FeatherState.Charging && CWRAsset.SoftGlow?.Value != null) {
                Texture2D glow = CWRAsset.SoftGlow.Value;
                float glowA = 0.14f * MathHelper.Clamp(StateTimer / 15f, 0f, 1f) * alpha;
                sb.Draw(glow, drawPos, null, FishHarpyVFX.Gold with { A = 0 } * glowA, 0f,
                    glow.Size() / 2f, Projectile.scale * 0.9f, SpriteEffects.None, 0f);
            }

            sb.Draw(featherTex, drawPos, sourceRect, body, drawRotation, origin, bodyScale, SpriteEffects.None, 0);

            //出手过冲
            if (State == FeatherState.Launching && launchTicks <= 2) {
                sb.Draw(featherTex, drawPos, sourceRect, FishHarpyVFX.Cream with { A = 0 } * (0.3f * alpha),
                    drawRotation, origin, bodyScale * 1.04f, SpriteEffects.None, 0);
            }

            return false;
        }

        /// <summary>飞出速度拉伸残影链</summary>
        private void DrawLaunchSmear(SpriteBatch sb, Texture2D featherTex, Rectangle sourceRect,
            Vector2 origin, float alpha, float speedT) {
            for (int i = 1; i <= 5; i++) {
                if (i >= Projectile.oldPos.Length || Projectile.oldPos[i] == Vector2.Zero) continue;

                float k = 1f - i / 6f;
                float fade = MathF.Pow(k, 1.6f) * 0.38f * speedT * alpha;
                Vector2 ghostPos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                Vector2 gScale = new Vector2(1f - 0.2f * speedT, 1f + (0.5f + i * 0.12f) * speedT) * Projectile.scale;
                float rot = (i < Projectile.oldRot.Length && Projectile.oldRot[i] != 0f
                    ? Projectile.oldRot[i] : Projectile.rotation) - MathHelper.PiOver2;

                sb.Draw(featherTex, ghostPos, sourceRect, FishHarpyVFX.Cream * fade, rot,
                    origin, gScale, SpriteEffects.None, 0);
            }
        }

        /// <summary>充能旋转拖影</summary>
        private void DrawSpinSmear(SpriteBatch sb, Texture2D featherTex, Rectangle sourceRect,
            Vector2 origin, float alpha, float drawRotation) {
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            for (int i = 1; i <= 4; i++) {
                float fade = (0.32f - i * 0.07f) * alpha;
                if (fade <= 0.01f) continue;

                float rot = drawRotation - spinRate * i * 2.6f;
                sb.Draw(featherTex, drawPos, sourceRect, FishHarpyVFX.Cream * fade, rot,
                    origin, Projectile.scale * (1f - i * 0.015f), SpriteEffects.None, 0);
            }
        }

        /// <summary>环绕位置残影</summary>
        private void DrawOrbitGhosts(SpriteBatch sb, Texture2D featherTex, Rectangle sourceRect,
            Vector2 origin, float alpha, float drawRotation) {
            float spinT = MathHelper.Clamp(orbitSpeed / MaxOrbitSpeed, 0f, 1f);
            for (int i = 2; i <= 6; i += 2) {
                if (i >= Projectile.oldPos.Length || Projectile.oldPos[i] == Vector2.Zero) continue;

                float fade = (1f - i / 8f) * 0.22f * spinT * alpha;
                Vector2 ghostPos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;

                sb.Draw(featherTex, ghostPos, sourceRect, FishHarpyVFX.Cream * fade, drawRotation - i * 0.05f,
                    origin, Projectile.scale * 0.96f, SpriteEffects.None, 0);
            }
        }

        /// <summary>带过冲缓出</summary>
        private static float EaseOutBack(float x) {
            x = MathHelper.Clamp(x, 0f, 1f);
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            float xm = x - 1f;
            return 1f + c3 * xm * xm * xm + c1 * xm * xm;
        }
    }
}
