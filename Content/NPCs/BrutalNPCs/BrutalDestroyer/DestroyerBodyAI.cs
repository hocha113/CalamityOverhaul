using CalamityOverhaul.Content.DamageModify;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDestroyer.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDestroyer.Rendering;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.Common;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDestroyer
{
    internal class DestroyerBodyAI : BrutalNPCOverride, ICWRLoader
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
        private const float AerialPhaseThreshold = 900f;
        private const float Phase5AerialTimerValue = AerialPhaseThreshold;
        private const float Phase4AerialTimerValue = AerialPhaseThreshold * 0.5f;
        private const float AerialPhaseResetThreshold = AerialPhaseThreshold * 2f;
        protected float bodyCount;
        private bool IsBodyAlt => bodyCount % 2 == 0;
        /// <summary>体节比例0头→1尾，供充能波</summary>
        protected virtual float BodyFraction => MathHelper.Clamp(bodyCount / DestroyerHeadAI.BodyCount, 0f, 1f);
        private float LifeRatio => npc.life / (float)npc.lifeMax;
        private bool StartFlightPhase => LifeRatio < 0.5f;
        private bool Phase2 => LifeRatio < (CWRWorld.Death ? 0.4f : 0.25f);
        private bool Phase3 => LifeRatio < (CWRWorld.Death ? 0.2f : 0.1f);
        private bool IncreaseSpeed => Vector2.Distance(Target.Center, npc.Center) > 4000;
        private bool IncreaseSpeedMore => Vector2.Distance(Target.Center, npc.Center) > 6000;
        private bool FlyAtTarget => ai[3] >= AerialPhaseThreshold && StartFlightPhase;
        private NPC SegmentNPC => Main.npc[(int)npc.ai[1]];
        private float enrageScale;
        private int noFlyZoneBoxHeight;
        private int totalSegments;
        private bool skeletronAlive;
        private int mechdusaCurvedSpineSegmentIndex;
        private int mechdusaCurvedSpineSegments;
        private int time;
        protected int frame;
        //死亡演出冻相对前节姿态
        private bool deathFreezeCaptured;
        private Vector2 deathFrozenOffset;
        private float deathFrozenRotation;
        internal Player Target => npc.FindPlayer();
        #endregion
        void ICWRLoader.LoadData() {
            CWRMod.Instance.AddBossHeadTexture(CWRConstant.NPC + "BTD/BTD_Body", -1);
            iconIndex = ModContent.GetModBossHeadSlot(CWRConstant.NPC + "BTD/BTD_Body");
            CWRMod.Instance.AddBossHeadTexture(CWRConstant.NPC + "BTD/BTD_Body2", -1);
            iconIndex2 = ModContent.GetModBossHeadSlot(CWRConstant.NPC + "BTD/BTD_Body2");
        }

        public override void BossHeadSlot(ref int index) {
            index = IsBodyAlt ? iconIndex2 : iconIndex;
        }

        public override void BossHeadRotation(ref float rotation) => rotation = npc.rotation + MathHelper.Pi;

        public override bool CheckActive() => false;

        public override bool? CanBrutalOverride() {
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
                    continue;//只要身体
                }
                if (saveRealLifeIndex >= 0 && saveRealLifeIndex != body.realLife) {
                    continue;//非同头跳过
                }
                saveRealLifeIndex = body.realLife;
                bodyCount++;
                if (body == npc) {
                    break;//到自身停搜
                }
            }
        }

        public override bool AI() {
            //死亡演出保活+冻姿态，跳跟随
            if (HeadInDeathPerformance()) {
                HandleDeathPerformanceSegment();
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
            deathFreezeCaptured = false;

            SetMechQueenUp();
            UpdateFlightPhase();
            UpdateEnrageScale();
            UpdateAlpha();

            VaultUtils.ClockFrame(ref frame, 5, 3);

            int headIndex = FindHeadIndex((int)npc.ai[3]);
            if (headIndex >= 0 && headIndex < Main.maxNPCs) {
                npc.realLife = headIndex;
            }

            skeletronAlive = CheckSkeletronAlive();

            npc.timeLeft = 1800;//防自然脱战

            if (npc.localAI[3] == 0f) {
                AddBodyCount();
                npc.localAI[3] = skeletronAlive ? 1f : -1f;
                npc.netUpdate = true;
            }

            totalSegments = Main.getGoodWorld ? 100 : 80;
            bool spitLaserSpreads = CWRWorld.Death;
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

            //消失行为
            HandleDespawnBehavior(ref shouldFly, ref segmentVelocity);

            //冲刺帧
            Move(segmentVelocity);

            //高速火花+充能光照(客户端)
            if (!VaultUtils.isServer) {
                float segSpeed = (npc.position - npc.oldPosition).Length();
                if (segSpeed > 26f && Main.rand.NextBool(9)) {
                    DestroyerMotionFX.SpawnSegmentSpeedSparks(npc, MathHelper.Clamp(segSpeed / 50f, 0.5f, 1.3f));
                }
                float lightWave = DestroyerChargeWave.Read(npc.realLife, BodyFraction);
                if (lightWave > 0.05f) {
                    Lighting.AddLight(npc.Center, DestroyerMotionFX.HotOrange.ToVector3() * lightWave);
                }
            }

            DestroyerHeadAI.ForcedNetUpdating(npc);
            time++;
            return false;
        }

        /// <summary>读头视觉+本节充能波</summary>
        protected (MechBossVisualMode mode, float intensity, float progress) ReadSegmentVisual(int controllerId, out float wave) {
            var (mode, intensity, progress) = MechBossVisualState.Read(controllerId);
            wave = DestroyerChargeWave.Read(controllerId, BodyFraction);
            if (wave > 0.01f) {
                if (mode == MechBossVisualMode.Idle) {
                    mode = MechBossVisualMode.Warning;
                }
                intensity = Math.Max(intensity, wave);
                progress = Math.Max(progress, wave);
            }
            return (mode, intensity, progress);
        }

        //提取，免重复遍历
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

            return -1;//无有效头
        }

        /// <summary>头是否死亡演出(读ai[2])</summary>
        private bool HeadInDeathPerformance() {
            int headIndex = (int)npc.realLife;
            if (headIndex < 0 || headIndex >= Main.maxNPCs || Main.npc[headIndex].type != NPCID.TheDestroyer) {
                headIndex = FindHeadIndex((int)npc.ai[3]);
            }
            if (headIndex < 0 || headIndex >= Main.maxNPCs) {
                return false;
            }
            NPC head = Main.npc[headIndex];
            return head.active && head.type == NPCID.TheDestroyer
                && (int)head.ai[2] == (int)DestroyerStateIndex.Death;
        }

        /// <summary>死亡体节保活，冻相对前节</summary>
        private void HandleDeathPerformanceSegment() {
            npc.aiStyle = -1;

            int headIndex = FindHeadIndex((int)npc.ai[3]);
            if (headIndex >= 0 && headIndex < Main.maxNPCs) {
                npc.realLife = headIndex;
            }

            //锁血无敌无接触伤
            npc.dontTakeDamage = true;
            npc.damage = 0;
            if (npc.life < 1) {
                npc.life = 1;
            }
            npc.timeLeft = 60;

            VaultUtils.ClockFrame(ref frame, 5, 3);

            //冻相对前节偏移，保弯曲
            NPC seg = SegmentNPC;
            if (seg.Alives()) {
                if (!deathFreezeCaptured) {
                    deathFrozenOffset = npc.Center - seg.Center;
                    deathFrozenRotation = npc.rotation;
                    deathFreezeCaptured = true;
                }
                npc.velocity = Vector2.Zero;
                npc.Center = seg.Center + deathFrozenOffset;
                npc.rotation = deathFrozenRotation;
            }
            else {
                //前节不可用则原地
                npc.velocity = Vector2.Zero;
            }

            DestroyerHeadAI.ForcedNetUpdating(npc);
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

        private void UpdateEnrageScale() {
            enrageScale = CWRWorld.BossRush ? 1f : 0f;
            if (Main.IsItDay() || CWRWorld.BossRush) {
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

        //骷髅王在场结果帧戳共享：60 节体节同帧各查一次，全体共用一次扫描
        private static uint skeletronAliveFrame = uint.MaxValue;
        private static bool skeletronAliveCache;

        /// <summary>骷髅王存活</summary>
        private bool CheckSkeletronAlive() {
            if (!(CWRWorld.MasterMode && !CWRWorld.BossRush && npc.localAI[3] != -1f))
                return false;

            if (skeletronAliveFrame != Main.GameUpdateCount) {
                skeletronAliveFrame = Main.GameUpdateCount;
                skeletronAliveCache = false;
                foreach (var n in Main.ActiveNPCs) {
                    if (n.type == NPCID.SkeletronPrime) {
                        skeletronAliveCache = true;
                        break;
                    }
                }
            }
            return skeletronAliveCache;
        }

        /// <summary>禁飞区高度</summary>
        private int CalculateNoFlyZoneHeight() {
            int baseHeight = CWRWorld.MasterMode ? 1500 : 1800;
            return baseHeight - (CWRWorld.Death ? 400 : (int)(400f * (1f - LifeRatio)));
        }

        /// <summary>速度/加速度/转向</summary>
        private float CalculateSpeedModifiers(out float speed, out float turnSpeed, out float segmentVelocity) {
            speed = CWRWorld.MasterMode ? 0.2f : 0.1f;
            turnSpeed = CWRWorld.MasterMode ? 0.3f : 0.15f;
            segmentVelocity = FlyAtTarget ? (CWRWorld.MasterMode ? 22.5f : 15f) : (CWRWorld.MasterMode ? 30f : 20f);

            float segmentVelocityBoost = CWRWorld.Death ? (FlyAtTarget ? 4.5f : 6f) * (1f - LifeRatio) : (FlyAtTarget ? 3f : 4f) * (1f - LifeRatio);
            float speedBoost = CWRWorld.Death ? (FlyAtTarget ? 0.1125f : 0.15f) * (1f - LifeRatio) : (FlyAtTarget ? 0.075f : 0.1f) * (1f - LifeRatio);
            float turnSpeedBoost = CWRWorld.Death ? 0.18f * (1f - LifeRatio) : 0.12f * (1f - LifeRatio);

            segmentVelocity += segmentVelocityBoost;
            speed += speedBoost;
            turnSpeed += turnSpeedBoost;

            return IncreaseSpeedMore ? 2f : IncreaseSpeed ? 1.5f : 1f;
        }

        /// <summary>速度修正</summary>
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

        private void Move(float segmentVelocity) {
            float dampingInertia = 0.18f;
            float baseLengBySegment = 64;
            if (NPC.IsMechQueenUp) {
                baseLengBySegment = 24f;
                dampingInertia += 0.1f;
            }

            //段比例缩放
            int mechdusaSegmentScale = (int)(baseLengBySegment * npc.scale);

            Vector2 segmentTarget = SegmentNPC.Center - npc.Center;

            //曲线段调目标Y
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

            //接触/伤害速度
            float minimalContactDamageVelocity = segmentVelocity * 0.25f;
            float minimalDamageVelocity = segmentVelocity * 0.5f;
            float bodyAndTailVelocity = (npc.position - npc.oldPosition).Length();

            //按速设伤
            if (bodyAndTailVelocity <= minimalContactDamageVelocity) {
                npc.damage = 0;
            }
            else {
                float velocityDamageScalar = MathHelper.Clamp((bodyAndTailVelocity - minimalContactDamageVelocity) / minimalDamageVelocity, 0f, 1f);
                npc.damage = (int)MathHelper.Lerp(0f, npc.defDamage, velocityDamageScalar);
            }

            //锁环预警期整环无害，投技逃逸窗是可以穿环而出的真窗口
            if (HeadInCoilLock()) {
                npc.damage = 0;
            }
        }

        /// <summary>头是否处于投技锁环预警态(读同步的ai[2])</summary>
        private bool HeadInCoilLock() {
            int headIndex = npc.realLife;
            if (headIndex < 0 || headIndex >= Main.maxNPCs) {
                return false;
            }
            NPC head = Main.npc[headIndex];
            return head.active && head.type == NPCID.TheDestroyer
                && (int)head.ai[2] == (int)DestroyerStateIndex.CoilLock;
        }

        public override bool? On_ModifyIncomingHit(NPC npc, ref NPC.HitModifiers modifiers) {
            if (modifiers.DamageType == EndlessDamageClass.Instance) {
                //无尽伤害跳过后续减伤
                return false;
            }
            //出场减伤已移除，破土即可杀
            modifiers.FinalDamage /= 2f;
            return false;
        }

        public override bool? Draw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            Texture2D value = Body.Value;
            Texture2D value2 = Body_Glow.Value;
            Rectangle rectangle = value.GetRectangle(frame, 4);

            if (IsBodyAlt) {
                value = BodyAlt.Value;
                value2 = BodyAlt_Glow.Value;
                rectangle = value.GetRectangle();
            }

            //whoAmI种子错开脉冲相位
            float seed = (npc.whoAmI % 64) / 64f;

            Vector2 drawPos = npc.Center - Main.screenPosition;
            Vector2 origin = rectangle.Size() / 2;

            //头视觉+本节充能波
            int controllerId = (int)npc.realLife;
            var (visMode, visIntensity, visProgress) = ReadSegmentVisual(controllerId, out float wave);

            //外圈描边
            MechBossThermalRenderer.DrawOutlineHalo(spriteBatch, value, drawPos, rectangle,
                npc.rotation + MathHelper.Pi, origin, npc.scale, SpriteEffects.None,
                visMode, visIntensity, visProgress);

            //热感着色器，传帧UV防跨帧
            bool shaderApplied = MechBossThermalRenderer.BeginThermalShader(spriteBatch, value, rectangle,
                visMode, visIntensity, visProgress, seed);
            spriteBatch.Draw(value, drawPos, rectangle, drawColor,
                npc.rotation + MathHelper.Pi, origin, npc.scale, SpriteEffects.None, 0);
            if (shaderApplied) {
                MechBossThermalRenderer.EndThermalShader(spriteBatch);
            }

            //发光层独立
            spriteBatch.Draw(value2, drawPos, rectangle, Color.White,
                npc.rotation + MathHelper.Pi, origin, npc.scale, SpriteEffects.None, 0);

            //充能波白热叠加
            if (wave > 0.05f) {
                Color hot = new Color(255, 165, 75, 0) * wave;
                spriteBatch.Draw(value2, drawPos, rectangle, hot,
                    npc.rotation + MathHelper.Pi, origin, npc.scale * 1.04f, SpriteEffects.None, 0);
                spriteBatch.Draw(value, drawPos, rectangle, hot * 0.55f,
                    npc.rotation + MathHelper.Pi, origin, npc.scale, SpriteEffects.None, 0);
            }

            return false;
        }

        public override bool PostDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            return false;
        }
    }
}
