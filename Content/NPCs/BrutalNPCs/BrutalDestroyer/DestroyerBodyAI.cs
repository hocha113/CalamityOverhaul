using CalamityOverhaul.Content.DamageModify;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDestroyer.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDestroyer.Rendering;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletronPrime;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.Common;
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
        private const float AerialPhaseThreshold = 900f;
        private const float Phase5AerialTimerValue = AerialPhaseThreshold;
        private const float Phase4AerialTimerValue = AerialPhaseThreshold * 0.5f;
        private const float AerialPhaseResetThreshold = AerialPhaseThreshold * 2f;
        protected float bodyCount;
        private bool IsBodyAlt => bodyCount % 2 == 0;
        /// <summary>本节在整条蠕虫上的位置比例（0=贴近头部, 1=尾部），用于充能波读取</summary>
        protected virtual float BodyFraction => MathHelper.Clamp(bodyCount / DestroyerHeadAI.BodyCount, 0f, 1f);
        private float LifeRatio => npc.life / (float)npc.lifeMax;
        private bool StartFlightPhase => LifeRatio < 0.5f;
        private bool Phase2 => LifeRatio < (CWRWorld.Death ? 0.4f : 0.25f);
        private bool Phase3 => LifeRatio < (CWRWorld.Death ? 0.2f : 0.1f);
        private bool HasSpawnDR => ai[1] < DestroyerHeadAI.StretchTime && ai[1] > 60f;
        private bool IncreaseSpeed => Vector2.Distance(Target.Center, npc.Center) > 4000;
        private bool IncreaseSpeedMore => Vector2.Distance(Target.Center, npc.Center) > 6000;
        private bool FlyAtTarget => (ai[3] >= AerialPhaseThreshold && StartFlightPhase) || HasSpawnDR;
        private NPC SegmentNPC => Main.npc[(int)npc.ai[1]];
        private float enrageScale;
        private int noFlyZoneBoxHeight;
        private int totalSegments;
        private bool skeletronAlive;
        private int mechdusaCurvedSpineSegmentIndex;
        private int mechdusaCurvedSpineSegments;
        private int time;
        protected int frame;
        //死亡演出：冻结相对前一节的姿态，避免身体停摆时被通用算法迅速捋直
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
            if (!HeadPrimeAI.DontReform()) {
                index = IsBodyAlt ? iconIndex2 : iconIndex;
            }
        }

        public override void BossHeadRotation(ref float rotation) => rotation = npc.rotation + MathHelper.Pi;

        public override bool CheckActive() => false;

        public override bool? CanCWROverride() {
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

            //头部进入死亡演出：保活 + 冻结姿态，跳过常规跟随算法
            //（否则身体会被通用算法迅速捋直，且体节可能因前节暂时性问题被链式清理，只剩头部演出）
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

            npc.timeLeft = 1800;//愚蠢的自然脱战

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

            //调用消失行为逻辑
            HandleDespawnBehavior(ref shouldFly, ref segmentVelocity);

            //冲刺！冲刺！冲刺！冲！冲！冲！
            Move(segmentVelocity);

            //高速运动时沿途甩出火花 + 充能波动态光照（纯客户端视觉，屏幕外自动剔除）
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

        /// <summary>
        /// 读取头部共享视觉状态并叠加本节位置上的充能波，返回最终滤镜参数
        /// </summary>
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

        //提取方法，避免重复遍历
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

            return -1; //找不到有效头部
        }

        /// <summary>
        /// 头部是否正处于死亡演出阶段（读取头部经网络同步的状态索引 npc.ai[2]）
        /// </summary>
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

        /// <summary>
        /// 死亡演出期间的体节处理：强制保活、不可受伤、不造成接触伤害，并冻结相对前一节的姿态，
        /// 使整条蠕虫保持进入演出那一刻的弯曲形态、随头部一起静止，而不是被通用跟随算法捋成直线。
        /// </summary>
        private void HandleDeathPerformanceSegment() {
            npc.aiStyle = -1;

            int headIndex = FindHeadIndex((int)npc.ai[3]);
            if (headIndex >= 0 && headIndex < Main.maxNPCs) {
                npc.realLife = headIndex;
            }

            //保活：锁血、无敌、不造成接触伤害，防止被链式清理或撞死玩家
            npc.dontTakeDamage = true;
            npc.damage = 0;
            if (npc.life < 1) {
                npc.life = 1;
            }
            npc.timeLeft = 60;

            VaultUtils.ClockFrame(ref frame, 5, 3);

            //冻结相对前一节的偏移：逐节传导后整条蠕虫保持原弯曲形态，随头部平移/静止
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
                //前一节暂时不可用时原地保持，避免位置突变
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

        /// <summary>
        /// 检查吴克是否存活
        /// </summary>
        private bool CheckSkeletronAlive() {
            if (!(CWRWorld.MasterMode && !CWRWorld.BossRush && npc.localAI[3] != -1f))
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
            int baseHeight = CWRWorld.MasterMode ? 1500 : 1800;
            return baseHeight - (CWRWorld.Death ? 400 : (int)(400f * (1f - LifeRatio)));
        }

        /// <summary>
        /// 计算速度、加速度和转向速度
        /// </summary>
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
            if (HeadPrimeAI.DontReform()) {
                baseLengBySegment = 40f;
            }
            if (NPC.IsMechQueenUp) {
                baseLengBySegment = 24f;
                dampingInertia += 0.1f;
            }

            //计算段比例缩放
            int mechdusaSegmentScale = (int)(baseLengBySegment * npc.scale);

            Vector2 segmentTarget = SegmentNPC.Center - npc.Center;

            //如果当前为曲线段，调整目标点的Y坐标
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

            //计算最小接触速度和伤害速度
            float minimalContactDamageVelocity = segmentVelocity * 0.25f;
            float minimalDamageVelocity = segmentVelocity * 0.5f;
            float bodyAndTailVelocity = (npc.position - npc.oldPosition).Length();

            //根据速度设置伤害
            if (bodyAndTailVelocity <= minimalContactDamageVelocity) {
                npc.damage = 0;
            }
            else {
                float velocityDamageScalar = MathHelper.Clamp((bodyAndTailVelocity - minimalContactDamageVelocity) / minimalDamageVelocity, 0f, 1f);
                npc.damage = (int)MathHelper.Lerp(0f, npc.defDamage, velocityDamageScalar);
            }
        }

        public override bool? On_ModifyIncomingHit(NPC npc, ref NPC.HitModifiers modifiers) {
            if (modifiers.DamageType == EndlessDamageClass.Instance) {
                //我们希望无尽伤害类型不会受到其他代码的减伤影响，所以，如果是无尽伤害，那么就阻止后面所有代码的执行
                return false;
            }
            if (time < DestroyerHeadAI.StretchTime) {
                modifiers.FinalDamage /= 100f;
                modifiers.SetMaxDamage(82);
                return false;
            }
            modifiers.FinalDamage /= 2f;
            return false;
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

            //每节体节用一个稳定但不同的种子（whoAmI），让脉冲扫描带相位错开
            float seed = (npc.whoAmI % 64) / 64f;

            if (time < DestroyerHeadAI.StretchTime) {
                value = Body_Stingless.Value;
                Vector2 stinglessPos = npc.Center - Main.screenPosition;
                Vector2 stinglessOrigin = value.Size() / 2;

                //出场期间不绘制halo和着色器，避免和缩进特效冲突
                spriteBatch.Draw(value, stinglessPos, null, drawColor,
                    npc.rotation + MathHelper.Pi, stinglessOrigin, npc.scale, SpriteEffects.None, 0);
            }
            else {
                Vector2 drawPos = npc.Center - Main.screenPosition;
                Vector2 origin = rectangle.Size() / 2;

                //读取头部共享状态并叠加本节充能波——"电流沿躯体奔跑"的可见波
                int controllerId = (int)npc.realLife;
                var (visMode, visIntensity, visProgress) = ReadSegmentVisual(controllerId, out float wave);

                //外圈描边光环——夜晚时也能看清整条蠕虫的走向
                MechBossThermalRenderer.DrawOutlineHalo(spriteBatch, value, drawPos, rectangle,
                    npc.rotation + MathHelper.Pi, origin, npc.scale, SpriteEffects.None,
                    visMode, visIntensity, visProgress);

                //本体套机械热感着色器（传入当前帧UV范围，避免4帧贴图邻域采样跨帧）
                bool shaderApplied = MechBossThermalRenderer.BeginThermalShader(spriteBatch, value, rectangle,
                    visMode, visIntensity, visProgress, seed);
                spriteBatch.Draw(value, drawPos, rectangle, drawColor,
                    npc.rotation + MathHelper.Pi, origin, npc.scale, SpriteEffects.None, 0);
                if (shaderApplied) {
                    MechBossThermalRenderer.EndThermalShader(spriteBatch);
                }

                //发光层独立绘制以保留原始自发光
                spriteBatch.Draw(value2, drawPos, rectangle, Color.White,
                    npc.rotation + MathHelper.Pi, origin, npc.scale, SpriteEffects.None, 0);

                //充能波白热叠加：波峰处体节亮起（A=0 即加法叠色）
                if (wave > 0.05f) {
                    Color hot = new Color(255, 165, 75, 0) * wave;
                    spriteBatch.Draw(value2, drawPos, rectangle, hot,
                        npc.rotation + MathHelper.Pi, origin, npc.scale * 1.04f, SpriteEffects.None, 0);
                    spriteBatch.Draw(value, drawPos, rectangle, hot * 0.55f,
                        npc.rotation + MathHelper.Pi, origin, npc.scale, SpriteEffects.None, 0);
                }
            }

            return false;
        }

        public override bool PostDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            return HeadPrimeAI.DontReform();
        }
    }
}
