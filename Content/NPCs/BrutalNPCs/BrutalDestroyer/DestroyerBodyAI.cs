using CalamityMod;
using CalamityMod.NPCs;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletronPrime;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDestroyer
{
    internal class DestroyerBodyAI : CWRNPCOverride, ICWRLoader
    {
        #region Data
        public override int TargetID => NPCID.TheDestroyerBody;
        [VaultLoaden(CWRConstant.NPC + "BTD/Body_Stingless")]
        internal static Asset<Texture2D> Body_Stingless = null;
        [VaultLoaden(CWRConstant.NPC + "BTD/Body")]
        internal static Asset<Texture2D> Body = null;
        [VaultLoaden(CWRConstant.NPC + "BTD/Body_Glow")]
        internal static Asset<Texture2D> Body_Glow = null;
        [VaultLoaden(CWRConstant.NPC + "BTD/BodyAlt")]
        internal static Asset<Texture2D> BodyAlt = null;
        [VaultLoaden(CWRConstant.NPC + "BTD/BodyAlt_Glow")]
        internal static Asset<Texture2D> BodyAlt_Glow = null;
        [VaultLoaden(CWRConstant.NPC + "BTD/Tail")]
        internal static Asset<Texture2D> Tail = null;
        [VaultLoaden(CWRConstant.NPC + "BTD/Tail_Glow")]
        internal static Asset<Texture2D> Tail_Glow = null;
        private static int iconIndex;
        private static int iconIndex2;
        private const float BeamWarningDuration = 120f;
        private const float AerialPhaseThreshold = 900f;
        private const float ExtremeModeBeamThreshold = 600f;
        private const float PhaseShiftWarningDuration = 180f;
        private const float Phase5AerialTimerValue = AerialPhaseThreshold;
        private const float Phase4AerialTimerValue = AerialPhaseThreshold * 0.5f;
        private const float AerialPhaseResetThreshold = AerialPhaseThreshold * 2f;
        private const float AerialWarningStartThreshold = AerialPhaseThreshold - PhaseShiftWarningDuration;
        private const float GroundWarningStartThreshold = AerialPhaseResetThreshold - PhaseShiftWarningDuration;
        private float bodyCount;
        private bool IsBodyAlt => bodyCount % 2 == 0;
        private float LifeRatio => npc.life / (float)npc.lifeMax;
        private bool StartFlightPhase => LifeRatio < 0.5f;
        private bool Phase2 => LifeRatio < (DestroyerHeadAI.Death ? 0.4f : 0.25f);
        private bool Phase3 => LifeRatio < (DestroyerHeadAI.Death ? 0.2f : 0.1f);
        private bool HasSpawnDR => ai[1] < DestroyerHeadAI.StretchTime && ai[1] > 60f;
        private bool IncreaseSpeed => Vector2.Distance(Target.Center, npc.Center) > 4000;
        private bool IncreaseSpeedMore => Vector2.Distance(Target.Center, npc.Center) > 6000;
        private bool FlyAtTarget => (ai[3] >= AerialPhaseThreshold && StartFlightPhase) || HasSpawnDR;
        private NPC SegmentNPC => Main.npc[(int)npc.ai[1]];
        private CalamityGlobalNPC calNPC => npc.Calamity();
        private float enrageScale;
        private int noFlyZoneBoxHeight;
        private int totalSegments;
        private bool skeletronAlive;
        private int mechdusaCurvedSpineSegmentIndex;
        private int mechdusaCurvedSpineSegments;
        private float phaseTransitionColorAmount;
        private int time;
        protected int frame;
        internal Player Target => npc.FindPlayer();
        #endregion
        void ICWRLoader.LoadData() {
            CWRMod.Instance.AddBossHeadTexture(CWRConstant.NPC + "BTD/BTD_Body", -1);
            iconIndex = ModContent.GetModBossHeadSlot(CWRConstant.NPC + "BTD/BTD_Body");
            CWRMod.Instance.AddBossHeadTexture(CWRConstant.NPC + "BTD/BTD_Body2", -1);
            iconIndex2 = ModContent.GetModBossHeadSlot(CWRConstant.NPC + "BTD/BTD_Body2");
        }

        public override void BossHeadSlot(ref int index) {
            if (!HeadPrimeAI.DontReform()) {
                index = IsBodyAlt ? iconIndex2 : iconIndex;
            }
        }

        public override void BossHeadRotation(ref float rotation) => rotation = npc.rotation + MathHelper.Pi;

        public override bool CheckActive() => false;

        public override bool? CanCWROverride() {
            if (CWRWorld.MachineRebellion) {
                return true;
            }
            return null;
        }

        public override void SetProperty() {
            npc.aiStyle = -1;
        }

        private void AddBodyCount() {
            bodyCount = 0;
            int saveRealLifeIndex = -1;
            foreach (var body in Main.ActiveNPCs) {
                if (body.type != NPCID.TheDestroyerBody) {
                    continue;//只寻找身体
                }
                if (saveRealLifeIndex >= 0 && saveRealLifeIndex != body.realLife) {
                    continue;//根据缓存的头部索引对比判断这些身体是否来自同一个头部，否则跳过
                }
                saveRealLifeIndex = body.realLife;
                bodyCount++;
                if (body == npc) {
                    break;//指针跳到自己这里后结束搜索
                }
            }
        }

        public override bool AI() {
            if (CWRWorld.CanTimeFrozen()) {
                CWRNpc.DoTimeFrozen(npc);
                return false;
            }

            if (!SegmentNPC.Alives()) {
                npc.life = 0;
                npc.HitEffect();
                npc.checkDead();
                npc.active = false;
                npc.netUpdate = true;
                return false;
            }

            npc.aiStyle = -1;

            SetMechQueenUp();
            UpdateDRIncrease();
            UpdateFlightPhase();
            phaseTransitionColorAmount = CalculatePhaseTransitionColorAmount();
            UpdateEnrageScale();
            UpdateAlpha();
            VaultUtils.ClockFrame(ref frame, 5, 3);

            int headIndex = FindHeadIndex((int)npc.ai[3]);
            if (headIndex >= 0 && headIndex < Main.maxNPCs) {
                npc.realLife = headIndex;
            }

            skeletronAlive = CheckSkeletronAlive();

            npc.timeLeft = 1800;//愚蠢的自然脱战

            if (npc.localAI[3] == 0f) {
                AddBodyCount();
                npc.localAI[3] = skeletronAlive ? 1f : -1f;
                npc.netUpdate = true;
            }

            totalSegments = Main.getGoodWorld ? 100 : 80;
            bool spitLaserSpreads = DestroyerHeadAI.Death;
            float speed, turnSpeed, segmentVelocity, velocityMultiplier;
            noFlyZoneBoxHeight = 0;

            if (skeletronAlive) {
                ai[3] = 0f;
                totalSegments = Main.getGoodWorld ? 75 : 60;
                spitLaserSpreads = false;
                noFlyZoneBoxHeight = 2000;
                segmentVelocity = 0;
            }
            else {
                noFlyZoneBoxHeight = CalculateNoFlyZoneHeight();
                velocityMultiplier = CalculateSpeedModifiers(out speed, out turnSpeed, out segmentVelocity);
                ApplySpeedModifiers(ref speed, ref turnSpeed, ref segmentVelocity, velocityMultiplier);
            }

            if (npc.type == NPCID.TheDestroyerBody) {
                HandleProbeRegeneration();
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

        // 提取方法，避免重复遍历
        public static int FindHeadIndex(int possibleIndex) {
            if (possibleIndex >= 0f && possibleIndex < Main.maxNPCs) {
                if (Main.npc[possibleIndex].active && Main.npc[possibleIndex].type == NPCID.TheDestroyer) {
                    return possibleIndex;
                }
            }

            foreach (var n in Main.ActiveNPCs) {
                if (n.type == NPCID.TheDestroyer) {
                    return n.whoAmI;
                }
            }

            return -1; // 找不到有效头部
        }

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

        private void UpdateDRIncrease() {
            if (ai[1] < DestroyerHeadAI.StretchTime) {
                ai[1]++;
            }
            npc.Calamity().newAI[1] = 1200;
            npc.Calamity().CurrentlyIncreasingDefenseOrDR = false;
        }

        private void UpdateFlightPhase() {
            if (StartFlightPhase) {
                ai[3] += 1f;
            }

            float flightPhaseTimerSetValue = Phase3 ? Phase5AerialTimerValue : Phase2 ? Phase4AerialTimerValue : 0f;
            if (ai[3] < flightPhaseTimerSetValue) {
                ai[3] = flightPhaseTimerSetValue;
            }

            if (ai[3] >= AerialPhaseResetThreshold) {
                ai[3] = flightPhaseTimerSetValue;
            }
        }

        private float CalculatePhaseTransitionColorAmount() {
            if (HasSpawnDR || Phase3) {
                return 1f;
            }

            if (ai[3] >= GroundWarningStartThreshold) {
                return MathHelper.Clamp(1f - (ai[3] - GroundWarningStartThreshold) / PhaseShiftWarningDuration, 0f, 1f);
            }

            if (ai[3] >= AerialWarningStartThreshold) {
                return MathHelper.Clamp((ai[3] - AerialWarningStartThreshold) / PhaseShiftWarningDuration, 0f, 1f);
            }

            return 0f;
        }

        private void UpdateEnrageScale() {
            enrageScale = DestroyerHeadAI.BossRush ? 1f : 0f;
            if (Main.IsItDay() || DestroyerHeadAI.BossRush) {
                npc.Calamity().CurrentlyEnraged = !DestroyerHeadAI.BossRush;
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
            if (!(DestroyerHeadAI.MasterMode && !DestroyerHeadAI.BossRush && npc.localAI[3] != -1f))
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
            int baseHeight = DestroyerHeadAI.MasterMode ? 1500 : 1800;
            return baseHeight - (DestroyerHeadAI.Death ? 400 : (int)(400f * (1f - LifeRatio)));
        }

        /// <summary>
        /// 计算速度、加速度和转向速度
        /// </summary>
        private float CalculateSpeedModifiers(out float speed, out float turnSpeed, out float segmentVelocity) {
            speed = DestroyerHeadAI.MasterMode ? 0.2f : 0.1f;
            turnSpeed = DestroyerHeadAI.MasterMode ? 0.3f : 0.15f;
            segmentVelocity = FlyAtTarget ? (DestroyerHeadAI.MasterMode ? 22.5f : 15f) : (DestroyerHeadAI.MasterMode ? 30f : 20f);

            float segmentVelocityBoost = DestroyerHeadAI.Death ? (FlyAtTarget ? 4.5f : 6f) * (1f - LifeRatio) : (FlyAtTarget ? 3f : 4f) * (1f - LifeRatio);
            float speedBoost = DestroyerHeadAI.Death ? (FlyAtTarget ? 0.1125f : 0.15f) * (1f - LifeRatio) : (FlyAtTarget ? 0.075f : 0.1f) * (1f - LifeRatio);
            float turnSpeedBoost = DestroyerHeadAI.Death ? 0.18f * (1f - LifeRatio) : 0.12f * (1f - LifeRatio);

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
                float speedMultiplier = Phase3 ? 1.8f : Phase2 ? 1.65f : 1.5f;
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

            if (enrageScale > 0f && !DestroyerHeadAI.BossRush) {
                ai[2] = Math.Min(ai[2] + 1f, 480f);
            }
            else {
                ai[2] = Math.Max(ai[2] - 1f, 0f);
            }

            if (!DestroyerHeadAI.MasterMode || !probeLaunched) {
                return;
            }

            npc.localAI[2] += 1f;
            if (npc.localAI[2] < 600f) {
                return;
            }

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
            NetAISend();
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
            if (npc.position.Y <= Target.position.Y) {
                return false;
            }

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
            if (npc.type == NPCID.TheDestroyerBody) {
                return;
            }

            Vector3 groundColor = new Vector3(0.3f, 0.1f, 0.05f);
            Vector3 flightColor = new Vector3(0.05f, 0.1f, 0.3f);
            Vector3 segmentColor = Vector3.Lerp(groundColor, flightColor, phaseTransitionColorAmount);
            Vector3 telegraphColor = groundColor;
            float telegraphProgress = 0f;

            float telegraphGateValue = ExtremeModeBeamThreshold - BeamWarningDuration;

            if (npc.type == NPCID.TheDestroyer && spitLaserSpreads && ai[0] > telegraphGateValue) {
                telegraphColor = GetTelegraphColor(calNPC.destroyerLaserColor);
                telegraphProgress = MathHelper.Clamp((ai[0] - telegraphGateValue) / BeamWarningDuration, 0f, 1f);
            }
            else if (npc.type == NPCID.TheDestroyerBody) {
                float shootProjectileTime = DestroyerHeadAI.BossRush ? 270f : 450f;
                float bodySegmentTime = npc.ai[0] * 30f;
                float shootProjectileGateValue = bodySegmentTime + shootProjectileTime;
                float bodyTelegraphGateValue = shootProjectileGateValue - BeamWarningDuration;

                if (ai[0] > bodyTelegraphGateValue) {
                    telegraphColor = GetTelegraphColor(calNPC.destroyerLaserColor);
                    telegraphProgress = MathHelper.Clamp((ai[0] - bodyTelegraphGateValue) / BeamWarningDuration, 0f, 1f);
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

        private static Vector3 GetTelegraphColor(int destroyerLaserColor) {
            return destroyerLaserColor switch {
                1 => new Vector3(0.1f, 0.3f, 0.05f),
                2 => new Vector3(0.05f, 0.2f, 0.2f),
                _ => new Vector3(0.3f, 0.1f, 0.05f)
            };
        }

        private void Move(float segmentVelocity) {
            float dampingInertia = 0.18f;
            float baseLengBySegment = 64;
            if (HeadPrimeAI.DontReform()) {
                baseLengBySegment = 40f;
            }
            if (NPC.IsMechQueenUp) {
                baseLengBySegment = 24f;
                dampingInertia += 0.1f;
            }

            // 计算段比例缩放
            int mechdusaSegmentScale = (int)(baseLengBySegment * npc.scale);

            Vector2 segmentTarget = SegmentNPC.Center - npc.Center;

            // 如果当前为曲线段，调整目标点的Y坐标
            if (mechdusaCurvedSpineSegmentIndex > 0) {
                float absoluteTileOffset = mechdusaSegmentScale - mechdusaSegmentScale * ((mechdusaCurvedSpineSegmentIndex - 1f) * 0.1f);
                absoluteTileOffset = MathHelper.Clamp(absoluteTileOffset, 0f, mechdusaSegmentScale);

                segmentTarget.Y -= absoluteTileOffset;
            }

            if (SegmentNPC.rotation != npc.rotation) {
                segmentTarget = segmentTarget.RotatedBy(MathHelper.WrapAngle(SegmentNPC.rotation - npc.rotation) * dampingInertia);
                segmentTarget = segmentTarget.MoveTowards((SegmentNPC.rotation - npc.rotation).ToRotationVector2(), 1f);
            }

            npc.velocity = Vector2.Zero;
            npc.rotation = segmentTarget.ToRotation() + MathHelper.PiOver2;
            npc.Center = SegmentNPC.Center - segmentTarget.SafeNormalize(Vector2.Zero) * mechdusaSegmentScale;

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
            Rectangle rectangle = value.GetRectangle(frame, 4);

            if (IsBodyAlt) {
                value = BodyAlt.Value;
                value2 = BodyAlt_Glow.Value;
                rectangle = value.GetRectangle();
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
