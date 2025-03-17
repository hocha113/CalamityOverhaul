using CalamityMod;
using CalamityMod.Events;
using CalamityMod.NPCs;
using CalamityMod.Particles;
using CalamityMod.Projectiles.Boss;
using CalamityMod.World;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletronPrime;
using CalamityOverhaul.Content.NPCs.Core;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDestroyer
{
    internal class DestroyerBodyAI : NPCOverride, ICWRLoader
    {
        #region Data
        public override int TargetID => NPCID.TheDestroyerBody;
        internal static Asset<Texture2D> Body_Stingless;
        internal static Asset<Texture2D> Body;
        internal static Asset<Texture2D> Body_Glow;
        internal static Asset<Texture2D> BodyAlt;
        internal static Asset<Texture2D> BodyAlt_Glow;
        internal static Asset<Texture2D> Tail;
        internal static Asset<Texture2D> Tail_Glow;
        internal static Asset<Texture2D> Head;
        internal static Asset<Texture2D> Head_Glow;
        private static int iconIndex;
        private const float BeamWarningDuration = 120f;
        private const float SparkWarningDuration = 30f;
        private const float AerialPhaseThreshold = 900f;
        private const float ExtremeModeBeamThreshold = 600f;
        private const float PhaseShiftWarningDuration = 180f;
        private const float DamageReductionIncreaseDuration = 600f;
        private const float Phase5AerialTimerValue = AerialPhaseThreshold;
        private const float Phase4AerialTimerValue = AerialPhaseThreshold * 0.5f;
        private const float AerialPhaseResetThreshold = AerialPhaseThreshold * 2f;
        private const float AerialWarningStartThreshold = AerialPhaseThreshold - PhaseShiftWarningDuration;
        private const float GroundWarningStartThreshold = AerialPhaseResetThreshold - PhaseShiftWarningDuration;
        private bool BossRush => BossRushEvent.BossRushActive;
        private bool MasterMode => Main.masterMode || BossRush;
        private bool Death => CalamityWorld.death || BossRush;
        private float LifeRatio => npc.life / (float)npc.lifeMax;
        private bool StartFlightPhase => LifeRatio < 0.5f;
        private bool Phase2 => LifeRatio < 0.85f || MasterMode;
        private bool Phase3 => LifeRatio < 0.7f || MasterMode;
        private bool Phase4 => LifeRatio < (Death ? 0.4f : 0.25f);
        private bool Phase5 => LifeRatio < (Death ? 0.2f : 0.1f);
        private bool HasSpawnDR => calNPC.newAI[1] < DamageReductionIncreaseDuration && calNPC.newAI[1] > 60f;
        private bool IncreaseSpeed => Vector2.Distance(Target.Center, npc.Center) > CalamityGlobalNPC.CatchUpDistance200Tiles;
        private bool IncreaseSpeedMore => Vector2.Distance(Target.Center, npc.Center) > CalamityGlobalNPC.CatchUpDistance350Tiles;
        private bool FlyAtTarget => (calNPC.newAI[3] >= AerialPhaseThreshold && StartFlightPhase) || HasSpawnDR;
        private bool AbleToFireLaser => calNPC.destroyerLaserColor != -1;
        private NPC SegmentNPC => Main.npc[(int)npc.ai[1]];
        private float enrageScale;
        private int noFlyZoneBoxHeight;
        private int totalSegments;
        private bool skeletronAlive;
        private int mechdusaCurvedSpineSegmentIndex;
        private int mechdusaCurvedSpineSegments;
        private float phaseTransitionColorAmount;
        private int time;
        protected int frame;
        internal Player Target {
            get {
                if (npc.target < 0 || npc.target == Main.maxPlayers || Main.player[npc.target].dead || !Main.player[npc.target].active) {
                    npc.TargetClosest();
                }
                return Main.player[npc.target];
            }
        }
        #endregion
        void ICWRLoader.LoadData() {
            CWRMod.Instance.AddBossHeadTexture(CWRConstant.NPC + "BTD/BTD_Body", -1);
            iconIndex = ModContent.GetModBossHeadSlot(CWRConstant.NPC + "BTD/BTD_Body");
        }
        void ICWRLoader.LoadAsset() {
            Body_Stingless = CWRUtils.GetT2DAsset(CWRConstant.NPC + "BTD/Body_Stingless");
            Body = CWRUtils.GetT2DAsset(CWRConstant.NPC + "BTD/Body");
            Body_Glow = CWRUtils.GetT2DAsset(CWRConstant.NPC + "BTD/Body_Glow");
            BodyAlt = CWRUtils.GetT2DAsset(CWRConstant.NPC + "BTD/BodyAlt");
            BodyAlt_Glow = CWRUtils.GetT2DAsset(CWRConstant.NPC + "BTD/BodyAlt_Glow");
            Tail = CWRUtils.GetT2DAsset(CWRConstant.NPC + "BTD/Tail");
            Tail_Glow = CWRUtils.GetT2DAsset(CWRConstant.NPC + "BTD/Tail_Glow");
        }
        void ICWRLoader.UnLoadData() {
            Body_Stingless = null;
            Body = null;
            Body_Glow = null;
            BodyAlt = null;
            BodyAlt_Glow = null;
            Tail = null;
            Tail_Glow = null;
        }

        public override void BossHeadSlot(ref int index) {
            if (!HeadPrimeAI.DontReform()) {
                index = iconIndex;
            }
        }

        public override void BossHeadRotation(ref float rotation) => rotation = npc.rotation + MathHelper.Pi;

        public override bool CheckActive() => false;

        private void SetMechQueenUp() {
            mechdusaCurvedSpineSegmentIndex = 0;
            mechdusaCurvedSpineSegments = 10;
            if (NPC.IsMechQueenUp) {
                int mechdusaIndex = (int)npc.ai[1];
                while (mechdusaIndex > 0 && mechdusaIndex < Main.maxNPCs) {
                    if (Main.npc[mechdusaIndex].active && Main.npc[mechdusaIndex].type >= NPCID.TheDestroyer
                        && Main.npc[mechdusaIndex].type <= NPCID.TheDestroyerTail) {
                        mechdusaCurvedSpineSegmentIndex++;
                        if (mechdusaCurvedSpineSegmentIndex >= mechdusaCurvedSpineSegments) {
                            mechdusaCurvedSpineSegmentIndex = 0;
                            break;
                        }

                        mechdusaIndex = (int)Main.npc[mechdusaIndex].ai[1];
                        continue;
                    }

                    mechdusaCurvedSpineSegmentIndex = 0;
                    break;
                }
                if (npc.width > 64) {
                    npc.width = 64;
                }
                if (npc.height > 64) {
                    npc.height = 64;
                }
                if (npc.scale > 2) {
                    npc.scale = 2;
                }
            }
        }

        public override bool? CanOverride() {
            if (CWRWorld.MachineRebellion) {
                return true;
            }
            return base.CanOverride();
        }

        public override bool AI() {
            if (!SegmentNPC.Alives()) {
                npc.life = 0;
                npc.HitEffect();
                npc.checkDead();
                npc.active = false;
                npc.netUpdate = true;
                return false;
            }

            SetMechQueenUp();
            UpdateDRIncrease();
            UpdateFlightPhase();
            phaseTransitionColorAmount = CalculatePhaseTransitionColorAmount();
            UpdateEnrageScale();
            UpdateAlpha();
            CWRUtils.ClockFrame(ref frame, 5, 3);

            if (npc.ai[3] > 0f) {
                npc.realLife = (int)npc.ai[3];
            }

            skeletronAlive = CheckSkeletronAlive();

            npc.timeLeft = 1800;//愚蠢的自然脱战

            if (npc.localAI[3] == 0f) {
                npc.localAI[3] = skeletronAlive ? 1f : -1f;
                npc.SyncExtraAI();
            }

            totalSegments = Main.getGoodWorld ? 100 : 80;
            bool spitLaserSpreads = Death;
            float speed, turnSpeed, segmentVelocity, velocityMultiplier;
            noFlyZoneBoxHeight = 0;

            if (skeletronAlive) {
                calNPC.newAI[3] = 0f;
                totalSegments = Main.getGoodWorld ? 75 : 60;
                spitLaserSpreads = false;
                noFlyZoneBoxHeight = 2000;
                speed = turnSpeed = segmentVelocity = 0;
            }
            else {
                noFlyZoneBoxHeight = CalculateNoFlyZoneHeight();
                velocityMultiplier = CalculateSpeedModifiers(out speed, out turnSpeed, out segmentVelocity);
                ApplySpeedModifiers(ref speed, ref turnSpeed, ref segmentVelocity, velocityMultiplier);
            }

            if (npc.type == NPCID.TheDestroyerBody) {
                HandleProbeRegeneration();
                HandleDestroyerLaser();
            }

            if (npc.life > SegmentNPC.life) {
                npc.life = SegmentNPC.life;
            }

            bool shouldFly = ShouldFly();

            if (shouldFly) {
                npc.localAI[1] = 0f;
            }
            else {
                npc.localAI[1] = 1f;
            }

            // 调用光照逻辑
            HandleLighting(spitLaserSpreads);

            // 调用消失行为逻辑
            HandleDespawnBehavior(ref shouldFly, ref segmentVelocity);

            //冲刺！冲刺！冲刺！冲！冲！冲！
            Move(segmentVelocity);

            DestroyerHeadAI.ForcedNetUpdating(npc);
            time++;
            return false;
        }

        private void UpdateDRIncrease() {
            if (calNPC.newAI[1] < DamageReductionIncreaseDuration) {
                calNPC.newAI[1] += 1f;
            }

            calNPC.CurrentlyIncreasingDefenseOrDR = calNPC.newAI[1] < DamageReductionIncreaseDuration;
        }

        private void UpdateFlightPhase() {
            if (StartFlightPhase) {
                calNPC.newAI[3] += 1f;
            }

            float flightPhaseTimerSetValue = Phase5 ? Phase5AerialTimerValue : Phase4 ? Phase4AerialTimerValue : 0f;
            if (calNPC.newAI[3] < flightPhaseTimerSetValue) {
                calNPC.newAI[3] = flightPhaseTimerSetValue;
            }

            if (calNPC.newAI[3] >= AerialPhaseResetThreshold) {
                calNPC.newAI[3] = flightPhaseTimerSetValue;
            }
        }

        private float CalculatePhaseTransitionColorAmount() {
            if (HasSpawnDR || Phase5)
                return 1f;

            if (calNPC.newAI[3] >= GroundWarningStartThreshold)
                return MathHelper.Clamp(1f - (calNPC.newAI[3] - GroundWarningStartThreshold) / PhaseShiftWarningDuration, 0f, 1f);

            if (calNPC.newAI[3] >= AerialWarningStartThreshold)
                return MathHelper.Clamp((calNPC.newAI[3] - AerialWarningStartThreshold) / PhaseShiftWarningDuration, 0f, 1f);

            return 0f;
        }

        private void UpdateEnrageScale() {
            enrageScale = BossRush ? 1f : 0f;
            if (Main.IsItDay() || BossRush) {
                calNPC.CurrentlyEnraged = !BossRush;
                enrageScale += 2f;
            }
        }

        private void UpdateAlpha() {
            if (SegmentNPC.alpha < 128) {
                if (npc.alpha != 0) {
                    for (int i = 0; i < 2; i++) {
                        int spawnDust = Dust.NewDust(npc.position, npc.width, npc.height, DustID.TheDestroyer, 0f, 0f, 100, default, 2f);
                        Main.dust[spawnDust].noGravity = true;
                        Main.dust[spawnDust].noLight = true;
                    }
                }

                npc.alpha -= 42;
                if (npc.alpha < 0)
                    npc.alpha = 0;
            }
        }

        /// <summary>
        /// 检查吴克是否存活
        /// </summary>
        private bool CheckSkeletronAlive() {
            if (!(MasterMode && !BossRush && npc.localAI[3] != -1f))
                return false;

            for (int i = 0; i < Main.maxNPCs; i++) {
                if (Main.npc[i].active && Main.npc[i].type == NPCID.SkeletronPrime)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// 计算无飞行区域的高度
        /// </summary>
        private int CalculateNoFlyZoneHeight() {
            int baseHeight = MasterMode ? 1500 : 1800;
            return baseHeight - (Death ? 400 : (int)(400f * (1f - LifeRatio)));
        }

        /// <summary>
        /// 计算速度、加速度和转向速度
        /// </summary>
        private float CalculateSpeedModifiers(out float speed, out float turnSpeed, out float segmentVelocity) {
            speed = MasterMode ? 0.2f : 0.1f;
            turnSpeed = MasterMode ? 0.3f : 0.15f;
            segmentVelocity = FlyAtTarget ? (MasterMode ? 22.5f : 15f) : (MasterMode ? 30f : 20f);

            float segmentVelocityBoost = Death ? (FlyAtTarget ? 4.5f : 6f) * (1f - LifeRatio) : (FlyAtTarget ? 3f : 4f) * (1f - LifeRatio);
            float speedBoost = Death ? (FlyAtTarget ? 0.1125f : 0.15f) * (1f - LifeRatio) : (FlyAtTarget ? 0.075f : 0.1f) * (1f - LifeRatio);
            float turnSpeedBoost = Death ? 0.18f * (1f - LifeRatio) : 0.12f * (1f - LifeRatio);

            segmentVelocity += segmentVelocityBoost;
            speed += speedBoost;
            turnSpeed += turnSpeedBoost;

            return IncreaseSpeedMore ? 2f : IncreaseSpeed ? 1.5f : 1f;
        }

        /// <summary>
        /// 应用速度修正
        /// </summary>
        private void ApplySpeedModifiers(ref float speed, ref float turnSpeed, ref float segmentVelocity, float velocityMultiplier) {
            segmentVelocity += 5f * enrageScale;
            speed += 0.05f * enrageScale;
            turnSpeed += 0.075f * enrageScale;

            if (FlyAtTarget) {
                float speedMultiplier = Phase5 ? 1.8f : Phase4 ? 1.65f : 1.5f;
                speed *= speedMultiplier;
            }

            segmentVelocity *= velocityMultiplier;
            speed *= velocityMultiplier;
            turnSpeed *= velocityMultiplier;

            if (Main.getGoodWorld) {
                segmentVelocity *= 1.2f;
                speed *= 1.2f;
                turnSpeed *= 1.2f;
            }
        }

        /// <summary>
        /// 处理探测器的生成
        /// </summary>
        private void HandleProbeRegeneration() {
            bool probeLaunched = npc.ai[2] == 1f;

            if (enrageScale > 0f && !BossRush)
                calNPC.newAI[2] = Math.Min(calNPC.newAI[2] + 1f, 480f);
            else
                calNPC.newAI[2] = Math.Max(calNPC.newAI[2] - 1f, 0f);

            if (MasterMode && probeLaunched) {
                npc.localAI[2] += 1f;
                if (npc.localAI[2] >= 600f) {
                    int maxProbes = 40;
                    bool regenerateProbeSegment = NPC.CountNPCS(NPCID.Probe) < maxProbes;

                    if (regenerateProbeSegment) {
                        int maxNPCs = totalSegments + maxProbes;
                        int numNPCs = 0;
                        for (int i = 0; i < Main.maxNPCs; i++) {
                            if (Main.npc[i].active) {
                                numNPCs++;
                                if (numNPCs >= maxNPCs) {
                                    regenerateProbeSegment = false;
                                    break;
                                }
                            }
                        }
                    }

                    if (regenerateProbeSegment) {
                        npc.ai[2] = 0f;
                        npc.netUpdate = true;
                    }

                    npc.localAI[2] = 0f;
                    npc.SyncVanillaLocalAI();
                }
            }
        }

        /// <summary>
        /// 处理摧毁者的激光发射逻辑
        /// </summary>
        private void HandleDestroyerLaser() {
            if (calNPC.destroyerLaserColor == -1 && npc.ai[2] != 1f) {
                if (Main.rand.NextBool(MasterMode ? 200 / (Phase5 ? 4 : Phase4 ? 3 : 2) : 200)) {
                    calNPC.destroyerLaserColor = Main.rand.Next(Phase3 ? 4 : Phase2 ? 3 : 2);
                    if (calNPC.newAI[2] > 0f || BossRush)
                        calNPC.destroyerLaserColor = 2;

                    npc.SyncDestroyerLaserColor();
                }
            }

            if (npc.ai[2] == 1f && AbleToFireLaser) {
                calNPC.destroyerLaserColor = -1;
                npc.SyncDestroyerLaserColor();
            }

            float shootProjectileTime = Death ? (MasterMode ? (Phase5 ? 120f : Phase4 ? 150f : 180f) : 270f) : (MasterMode ? (Phase5 ? 150f : Phase4 ? 210f : 270f) : 450f);
            float bodySegmentTime = npc.ai[0] * (MasterMode ? 20f : 30f);
            float shootProjectileGateValue = bodySegmentTime + shootProjectileTime;

            if (AbleToFireLaser)
                calNPC.newAI[0] += (calNPC.newAI[0] > shootProjectileGateValue - BeamWarningDuration) ? 1f : 2f;

            if (Main.netMode != NetmodeID.MultiplayerClient && calNPC.newAI[0] % 20f == 10f && AbleToFireLaser) {
                npc.SyncExtraAI();
            }

            HandleLaserShooting(shootProjectileGateValue, shootProjectileTime, bodySegmentTime);
        }

        private void HandleLaserShooting(float shootProjectileGateValue, float shootProjectileTime, float bodySegmentTime) {
            Color telegraphColor = Color.Transparent;
            switch (calNPC.destroyerLaserColor) {
                case 0:
                    telegraphColor = Color.Red;
                    break;
                case 1:
                    telegraphColor = Color.Green;
                    break;
                case 2:
                    telegraphColor = Color.Cyan;
                    break;
            }

            if (calNPC.newAI[0] == shootProjectileGateValue - BeamWarningDuration) {
                Particle telegraph = new DestroyerReticleTelegraph(
                    npc,
                    telegraphColor,
                    1.5f,
                    0.15f,
                    (int)BeamWarningDuration);
                GeneralParticleHandler.SpawnParticle(telegraph);
            }

            if (calNPC.newAI[0] == shootProjectileGateValue - SparkWarningDuration) {
                Particle spark = new DestroyerSparkTelegraph(
                    npc,
                    telegraphColor * 2f,
                    Color.White,
                    3f,
                    30,
                    Main.rand.NextFloat(MathHelper.ToRadians(3f)) * Main.rand.NextBool().ToDirectionInt());
                GeneralParticleHandler.SpawnParticle(spark);
            }
            // 判断是否可以射击
            if (calNPC.newAI[0] < shootProjectileGateValue || !AbleToFireLaser)
                return;

            // 更新激光发射时间和目标
            UpdateLaserTimingAndTarget(totalSegments, shootProjectileTime, bodySegmentTime, BeamWarningDuration, MasterMode);

            // 检查是否可以命中目标
            if (!Collision.CanHit(npc.position, npc.width, npc.height, Target.position, Target.width, Target.height))
                return;

            // 激光射击逻辑
            float projectileSpeed = (MasterMode ? 4.5f : 3.5f) + Main.rand.NextFloat() * 1.5f + enrageScale;
            int projectileType = GetProjectileType(calNPC.destroyerLaserColor);
            Vector2 projectileVelocity = (Target.Center - npc.Center).SafeNormalize(Vector2.UnitY) * projectileSpeed;
            Vector2 projectileSpawn = npc.Center + projectileVelocity.SafeNormalize(Vector2.UnitY) * 100f;

            int damage = npc.GetProjectileDamage(projectileType);
            damage = AdjustDamageForEarlyHardmode(damage);

            if (Main.netMode != NetmodeID.MultiplayerClient) {
                int proj = Projectile.NewProjectile(npc.GetSource_FromAI(), projectileSpawn, projectileVelocity, projectileType, damage, 0f, Main.myPlayer, 1f, 0f);
                Main.projectile[proj].timeLeft = 1200;
            }

            npc.netUpdate = true;

            // 重置激光颜色
            calNPC.destroyerLaserColor = -1;
            npc.SyncDestroyerLaserColor();
        }

        private void UpdateLaserTimingAndTarget(int totalSegments, float shootProjectileTime, float bodySegmentTime, float LaserTelegraphTime, bool masterMode) {
            int numProbeSegments = CountActiveSegments(npc.type);
            float lerpAmount = MathHelper.Clamp(numProbeSegments / (float)totalSegments, 0f, 1f);
            float laserShootTimeBonus = (int)MathHelper.Lerp(0f, (shootProjectileTime + bodySegmentTime * lerpAmount) - LaserTelegraphTime, 1f - lerpAmount);
            calNPC.newAI[0] = laserShootTimeBonus;
            npc.SyncExtraAI();
            npc.TargetClosest();
        }

        private int CountActiveSegments(int npcType) {
            int count = 0;
            for (int i = 0; i < Main.maxNPCs; i++) {
                if (Main.npc[i].active && Main.npc[i].type == npcType && Main.npc[i].ai[2] == 0f)
                    count++;
            }
            return count;
        }

        private int GetProjectileType(int destroyerLaserColor) {
            return destroyerLaserColor switch {
                1 => ModContent.ProjectileType<DestroyerCursedLaser>(),
                2 => ModContent.ProjectileType<DestroyerElectricLaser>(),
                _ => ProjectileID.DeathLaser
            };
        }

        private int AdjustDamageForEarlyHardmode(int damage) {
            if (CalamityConfig.Instance.EarlyHardmodeProgressionRework && !BossRushEvent.BossRushActive) {
                double firstMechMultiplier = CalamityGlobalNPC.EarlyHardmodeProgressionReworkFirstMechStatMultiplier_Expert;
                double secondMechMultiplier = CalamityGlobalNPC.EarlyHardmodeProgressionReworkSecondMechStatMultiplier_Expert;
                if (!NPC.downedMechBossAny)
                    damage = (int)(damage * firstMechMultiplier);
                else if ((!NPC.downedMechBoss1 && !NPC.downedMechBoss2) || (!NPC.downedMechBoss2 && !NPC.downedMechBoss3) || (!NPC.downedMechBoss3 && !NPC.downedMechBoss1))
                    damage = (int)(damage * secondMechMultiplier);
            }
            return damage;
        }

        private bool ShouldFly() {
            int tilePosX = Math.Max((int)(npc.position.X / 16f) - 1, 0);
            int tileWidthPosX = Math.Min((int)((npc.position.X + npc.width) / 16f) + 2, Main.maxTilesX);
            int tilePosY = Math.Max((int)(npc.position.Y / 16f) - 1, 0);
            int tileWidthPosY = Math.Min((int)((npc.position.Y + npc.height) / 16f) + 2, Main.maxTilesY);

            if (!FlyAtTarget && CheckCollisionWithTiles(tilePosX, tileWidthPosX, tilePosY, tileWidthPosY)) {
                return true;
            }

            if (npc.type == NPCID.TheDestroyer && CheckNoFlyZones(noFlyZoneBoxHeight)) {
                return true;
            }

            return false;
        }

        private bool CheckCollisionWithTiles(int tilePosX, int tileWidthPosX, int tilePosY, int tileWidthPosY) {
            for (int x = tilePosX; x < tileWidthPosX; x++) {
                for (int y = tilePosY; y < tileWidthPosY; y++) {
                    Tile tile = Main.tile[x, y];
                    if (tile != null &&
                        (tile.HasUnactuatedTile && (Main.tileSolid[tile.TileType] ||
                        (Main.tileSolidTop[tile.TileType] && tile.TileFrameY == 0))) || tile.LiquidAmount > 64) {
                        Vector2 tilePos = new Vector2(x * 16, y * 16);
                        if (npc.Hitbox.Intersects(new Rectangle((int)tilePos.X, (int)tilePos.Y, 16, 16))) {
                            return true;
                        }
                    }
                }
            }
            return false;
        }

        private bool CheckNoFlyZones(int noFlyZoneBoxHeight) {
            if (npc.position.Y <= Target.position.Y) return false;

            Rectangle npcRectangle = npc.Hitbox;
            int noFlyZoneRadius = 1000;

            for (int i = 0; i < Main.maxPlayers; i++) {
                if (!Main.player[i].active) continue;

                Rectangle noFlyZone = new Rectangle(
                    (int)Main.player[i].position.X - noFlyZoneRadius,
                    (int)Main.player[i].position.Y - noFlyZoneRadius,
                    noFlyZoneRadius * 2,
                    noFlyZoneBoxHeight
                );

                if (npcRectangle.Intersects(noFlyZone)) {
                    return false;
                }
            }
            return true;
        }

        private void HandleLighting(bool spitLaserSpreads) {
            if (npc.type == NPCID.TheDestroyerBody && calNPC.destroyerLaserColor == -1)
                return;

            Vector3 lightColor = Color.Red.ToVector3();
            Vector3 groundColor = new Vector3(0.3f, 0.1f, 0.05f);
            Vector3 flightColor = new Vector3(0.05f, 0.1f, 0.3f);
            Vector3 segmentColor = Vector3.Lerp(groundColor, flightColor, phaseTransitionColorAmount);
            Vector3 telegraphColor = groundColor;
            float telegraphProgress = 0f;

            if (calNPC.destroyerLaserColor != -1) {
                float telegraphGateValue = ExtremeModeBeamThreshold - BeamWarningDuration;

                if (npc.type == NPCID.TheDestroyer && spitLaserSpreads && calNPC.newAI[0] > telegraphGateValue) {
                    telegraphColor = GetTelegraphColor(calNPC.destroyerLaserColor);
                    telegraphProgress = MathHelper.Clamp((calNPC.newAI[0] - telegraphGateValue) / BeamWarningDuration, 0f, 1f);
                }
                else if (npc.type == NPCID.TheDestroyerBody) {
                    float shootProjectileTime = (CalamityWorld.death || BossRushEvent.BossRushActive) ? 270f : 450f;
                    float bodySegmentTime = npc.ai[0] * 30f;
                    float shootProjectileGateValue = bodySegmentTime + shootProjectileTime;
                    float bodyTelegraphGateValue = shootProjectileGateValue - BeamWarningDuration;

                    if (calNPC.newAI[0] > bodyTelegraphGateValue) {
                        telegraphColor = GetTelegraphColor(calNPC.destroyerLaserColor);
                        telegraphProgress = MathHelper.Clamp((calNPC.newAI[0] - bodyTelegraphGateValue) / BeamWarningDuration, 0f, 1f);
                    }
                }
            }

            Lighting.AddLight(npc.Center, Vector3.Lerp(segmentColor, telegraphColor * 2f, telegraphProgress));
        }

        private void HandleDespawnBehavior(ref bool shouldFly, ref float segmentVelocity) {
            bool oblivionWasAlive = npc.localAI[3] == 1f && !skeletronAlive;
            bool oblivionFightDespawn = (skeletronAlive && LifeRatio < 0.75f) || oblivionWasAlive;

            if (Target.dead || oblivionFightDespawn) {
                shouldFly = false;
                npc.velocity.Y += 2f;

                if (npc.position.Y > Main.worldSurface * 16D) {
                    npc.velocity.Y += 2f;
                    segmentVelocity *= 2f;
                }

                if (npc.position.Y > Main.rockLayer * 16D) {
                    for (int n = 0; n < Main.maxNPCs; n++) {
                        if (Main.npc[n].aiStyle == npc.aiStyle)
                            Main.npc[n].active = false;
                    }
                }
            }
        }

        private Vector3 GetTelegraphColor(int destroyerLaserColor) {
            return destroyerLaserColor switch {
                1 => new Vector3(0.1f, 0.3f, 0.05f),
                2 => new Vector3(0.05f, 0.2f, 0.2f),
                _ => new Vector3(0.3f, 0.1f, 0.05f) // Default ground color
            };
        }

        private void Move(float segmentVelocity) {
            // 获取NPC和目标的中心点
            Vector2 npcCenter = npc.Center;
            Vector2 targetCenter = Target.Center;

            // 对中心点进行16像素网格对齐
            npcCenter.X = (int)(npcCenter.X / 16f) * 16;
            npcCenter.Y = (int)(npcCenter.Y / 16f) * 16;
            targetCenter.X = (int)(targetCenter.X / 16f) * 16 - npcCenter.X;
            targetCenter.Y = (int)(targetCenter.Y / 16f) * 16 - npcCenter.Y;

            float baseLengBySegment = 64;
            if (HeadPrimeAI.DontReform()) {
                baseLengBySegment = 40f;
            }
            if (NPC.IsMechQueenUp) {
                baseLengBySegment = 24f;
            }
            // 计算段比例缩放
            int mechdusaSegmentScale = (int)(baseLengBySegment * npc.scale);

            Vector2 segmentTarget = SegmentNPC.Center - npc.Center;

            // 如果当前为曲线段，调整目标点的Y坐标
            if (mechdusaCurvedSpineSegmentIndex > 0) {
                float absoluteTileOffset = mechdusaSegmentScale - mechdusaSegmentScale * ((mechdusaCurvedSpineSegmentIndex - 1f) * 0.1f);
                absoluteTileOffset = MathHelper.Clamp(absoluteTileOffset, 0f, mechdusaSegmentScale);

                segmentTarget.Y += absoluteTileOffset;
            }

            // 更新旋转方向
            npc.rotation = (float)Math.Atan2(segmentTarget.Y, segmentTarget.X) + MathHelper.PiOver2;

            // 调整目标距离
            float targetTileDist = (segmentTarget.Length() - mechdusaSegmentScale) / segmentTarget.Length();
            segmentTarget *= targetTileDist;

            // 更新NPC位置
            npc.velocity = Vector2.Zero;
            npc.position += segmentTarget;

            // 计算最小接触速度和伤害速度
            float minimalContactDamageVelocity = segmentVelocity * 0.25f;
            float minimalDamageVelocity = segmentVelocity * 0.5f;
            float bodyAndTailVelocity = (npc.position - npc.oldPosition).Length();

            // 根据速度设置伤害
            if (bodyAndTailVelocity <= minimalContactDamageVelocity) {
                npc.damage = 0;
            }
            else {
                float velocityDamageScalar = MathHelper.Clamp((bodyAndTailVelocity - minimalContactDamageVelocity) / minimalDamageVelocity, 0f, 1f);
                npc.damage = (int)MathHelper.Lerp(0f, npc.defDamage, velocityDamageScalar);
            }
        }

        public override void ModifyHitByItem(Player player, Item item, ref NPC.HitModifiers modifiers) {
            if (time < DestroyerHeadAI.StretchTime) {
                modifiers.FinalDamage /= 100f;
                modifiers.SetMaxDamage(90);
            }
        }

        public override void ModifyHitByProjectile(Projectile projectile, ref NPC.HitModifiers modifiers) {
            if (time < DestroyerHeadAI.StretchTime) {
                modifiers.FinalDamage /= 100f;
                modifiers.SetMaxDamage(82);
            }
        }

        public override bool? Draw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            if (HeadPrimeAI.DontReform()) {
                return true;
            }

            Texture2D value = Body.Value;
            Texture2D value2 = Body_Glow.Value;
            Rectangle rectangle = CWRUtils.GetRec(value, frame, 4);

            if (npc.whoAmI % 2 == 0) {
                value = BodyAlt.Value;
                value2 = BodyAlt_Glow.Value;
                rectangle = CWRUtils.GetRec(value);
            }

            if (time < DestroyerHeadAI.StretchTime) {
                value = Body_Stingless.Value;
                spriteBatch.Draw(value, npc.Center - Main.screenPosition
                , null, drawColor, npc.rotation + MathHelper.Pi, value.Size() / 2, npc.scale, SpriteEffects.None, 0);
            }
            else {
                spriteBatch.Draw(value, npc.Center - Main.screenPosition
                , rectangle, drawColor, npc.rotation + MathHelper.Pi, rectangle.Size() / 2, npc.scale, SpriteEffects.None, 0);
                spriteBatch.Draw(value2, npc.Center - Main.screenPosition
                , rectangle, Color.White, npc.rotation + MathHelper.Pi, rectangle.Size() / 2, npc.scale, SpriteEffects.None, 0);
            }

            return false;
        }

        public override bool PostDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            return HeadPrimeAI.DontReform();
        }
    }
}
