using CalamityOverhaul.Content.Items.Melee;
using CalamityOverhaul.Content.Items.Summon;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletronPrime;
using CalamityOverhaul.Content.RemakeItems.ModifyBag;
using CalamityOverhaul.OtherMods.InfernumMode;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent.ItemDropRules;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDestroyer
{
    internal class DestroyerHeadAI : CWRNPCOverride, ICWRLoader
    {
        #region Data
        public override int TargetID => NPCID.TheDestroyer;

        private ref float AI_State => ref npc.ai[0];
        private ref float AI_Timer => ref npc.ai[1];
        private ref float AI_SubState => ref npc.ai[2];
        private ref float AI_Counter => ref npc.ai[3];

        private Player targetPlayer;
        private List<NPC> bodySegments = new();
        private int frame;
        private int glowFrame;
        private bool openMouth;
        private int dontOpenMouthTime;
        private Vector2 targetPosition;
        private Vector2 moveVector;
        private float speed;
        private float turnSpeed;
        private bool isEnraged;
        private int dashCount;
        private float chargeProgress;
        private Vector2 dashDirection;

        [VaultLoaden(CWRConstant.NPC + "BTD/BTD_Head")]
        internal static Asset<Texture2D> HeadIcon = null;
        [VaultLoaden(CWRConstant.NPC + "BTD/Head")]
        internal static Asset<Texture2D> Head = null;
        [VaultLoaden(CWRConstant.NPC + "BTD/Head_Glow")]
        internal static Asset<Texture2D> Head_Glow = null;
        internal static int iconIndex;
        internal static int iconIndex_Void;

        internal const int StretchTime = 360;
        internal const int BodyCount = 80;

        private const int STATE_INTRO = 0;
        private const int STATE_IDLE_PATROL = 1;
        private const int STATE_LASER_BARRAGE = 2;
        private const int STATE_ENCIRCLE_TRAP = 3;
        private const int STATE_DASH_ASSAULT = 4;
        private const int STATE_DEATH_ANIMATION = 5;
        private const int STATE_DESPAWN = 6;

        #endregion

        #region 加载与初始化
        void ICWRLoader.LoadData() {
            CWRMod.Instance.AddBossHeadTexture(CWRConstant.NPC + "BTD/BTD_Head", -1);
            iconIndex = ModContent.GetModBossHeadSlot(CWRConstant.NPC + "BTD/BTD_Head");
            CWRMod.Instance.AddBossHeadTexture(CWRConstant.Placeholder, -1);
            iconIndex_Void = ModContent.GetModBossHeadSlot(CWRConstant.Placeholder);
        }

        public override void SetProperty() {
            if (CWRWorld.MachineRebellion) {
                npc.life = npc.lifeMax *= 32;
                npc.defDefense = npc.defense = 40;
                npc.defDamage = npc.damage *= 3;
                npc.scale = 1.2f; //稍微增大体型以增加压迫感
            }
        }

        public override bool? CanCWROverride() {
            return CWRWorld.MachineRebellion ? true : null;
        }
        #endregion

        #region 主要AI行为
        public override bool AI() {
            //核心状态检查
            if (CWRWorld.CanTimeFrozen()) {
                CWRNpc.DoTimeFrozen(npc);
                return false;
            }

            if (HeadPrimeAI.DontReform()) {
                return true;
            }

            CWRPlayer.TheDestroyer = npc.whoAmI;

            FindTarget();
            CheckEnrage();
            UpdateBodyList();
            if (InfernumRef.InfernumModeOpenState) {
                return true;
            }

            //状态机执行
            switch ((int)AI_State) {
                case STATE_INTRO:
                    ExecuteIntroState();
                    break;
                case STATE_IDLE_PATROL:
                    ExecutePatrolState();
                    break;
                case STATE_LASER_BARRAGE:
                    ExecuteLaserBarrageState();
                    break;
                case STATE_ENCIRCLE_TRAP:
                    ExecuteEncircleState();
                    break;
                case STATE_DASH_ASSAULT:
                    ExecuteDashAssaultState();
                    break;
                case STATE_DEATH_ANIMATION:
                    ExecuteDeathState();
                    break;
                case STATE_DESPAWN:
                    ExecuteDespawnState();
                    break;
                default:
                    AI_State = STATE_IDLE_PATROL;
                    break;
            }

            //物理与视觉更新
            UpdateMovement();
            HandleMouth();
            UpdateVisuals();

            //网络同步
            if (!VaultUtils.isClient && Main.GameUpdateCount % 10 == 0) {
                npc.netUpdate = true;
            }

            return false;
        }
        #endregion

        #region 状态机

        /// <summary>
        /// 进场状态
        /// </summary>
        private void ExecuteIntroState() {
            if (AI_Timer == 0) {
                //初始化身体
                if (!VaultUtils.isClient) {
                    SpawnBody();
                }
                SoundEngine.PlaySound(SoundID.Roar, npc.Center);
                npc.velocity = Vector2.UnitY * -20f; //向上冲出
            }

            AI_Timer++;

            //冲出地面一段距离后进入巡逻
            if (AI_Timer > 120) {
                SwitchState(STATE_IDLE_PATROL);
            }
        }

        /// <summary>
        /// 巡空：在玩家周围做立体椭圆轨迹盘旋，带有高度起伏
        /// </summary>
        private void ExecutePatrolState() {
            float patrolTime = AI_Timer * 0.015f;
            float horizontalRadius = 900f;
            float verticalRadius = 400f;

            //椭圆轨迹带高度变化，产生立体的盘旋感
            float offsetX = (float)Math.Cos(patrolTime) * horizontalRadius;
            float offsetY = (float)Math.Sin(patrolTime * 1.3f) * verticalRadius - 300f;
            targetPosition = targetPlayer.Center + new Vector2(offsetX, offsetY);

            //渐进加速：从刚切入巡逻时的慢速平滑加速到巡航速度
            float accelProgress = Math.Min(AI_Timer / 90f, 1f);
            speed = MathHelper.Lerp(10f, isEnraged ? 22f : 18f, accelProgress);
            turnSpeed = MathHelper.Lerp(0.2f, 0.5f, accelProgress);

            AI_Timer++;

            int patrolDuration = isEnraged ? 240 : 300;
            if (AI_Timer > patrolDuration) {
                int nextState = Main.rand.Next(3) switch {
                    0 => STATE_LASER_BARRAGE,
                    1 => STATE_ENCIRCLE_TRAP,
                    _ => STATE_DASH_ASSAULT
                };
                SwitchState(nextState);
            }
        }

        /// <summary>
        /// 协同所有体节进行有规律的激光射击
        /// </summary>
        private void ExecuteLaserBarrageState() {
            speed = 12f;
            turnSpeed = 0.6f;
            targetPosition = targetPlayer.Center + targetPlayer.velocity * 30f;

            AI_Timer++;
            int fireRate = isEnraged ? 3 : 5;
            int chargeTime = 60;
            int fireTime = 240;

            //阶段1：充能预警，体节沿身体依次发出红色粒子
            if (AI_Timer < chargeTime) {
                chargeProgress = AI_Timer / (float)chargeTime;
                if (!VaultUtils.isServer && AI_Timer % 4 == 0 && bodySegments.Count > 0) {
                    int warningIndex = (int)(chargeProgress * bodySegments.Count);
                    warningIndex = Math.Clamp(warningIndex, 0, bodySegments.Count - 1);
                    NPC warningSegment = bodySegments[warningIndex];
                    if (warningSegment.active) {
                        for (int i = 0; i < 3; i++) {
                            Dust dust = Dust.NewDustDirect(warningSegment.Center + Main.rand.NextVector2Circular(20, 20)
                                , 1, 1, DustID.FireworkFountain_Red, 0, 0, 100, default, 1.8f);
                            dust.noGravity = true;
                            dust.velocity = (warningSegment.Center - dust.position).SafeNormalize(Vector2.Zero) * 2f;
                        }
                    }
                }
            }
            //阶段2：波次射击
            else if (AI_Timer < fireTime) {
                chargeProgress = 0f;
                if (bodySegments.Count > 0 && AI_Timer % fireRate == 0) {
                    int segmentIndex = (int)((AI_Timer - chargeTime) / fireRate) % bodySegments.Count;
                    NPC segment = bodySegments[segmentIndex];
                    if (segment.active) {
                        FireLaser(segment, targetPlayer.Center);
                    }
                }
            }
            //阶段3：结束
            else {
                chargeProgress = 0f;
                SwitchState(STATE_IDLE_PATROL);
            }
        }

        /// <summary>
        /// 快速包围玩家，收缩包围圈，体节激光密度随半径缩小而递增
        /// </summary>
        private void ExecuteEncircleState() {
            //角速度随时间递增，产生加速旋转的压迫感
            float angularSpeed = MathHelper.Lerp(0.03f, isEnraged ? 0.08f : 0.06f, Math.Min(AI_Timer / 300f, 1f));
            float angle = AI_Timer * angularSpeed;

            //半径使用平滑的缓出曲线收缩
            float shrinkProgress = Math.Min(AI_Timer / 360f, 1f);
            float easeOut = 1f - (1f - shrinkProgress) * (1f - shrinkProgress);
            float radius = MathHelper.Lerp(1500f, 600f, easeOut);
            Vector2 offset = angle.ToRotationVector2() * radius;
            targetPosition = targetPlayer.Center + offset;

            speed = MathHelper.Lerp(28f, 40f, shrinkProgress);
            turnSpeed = MathHelper.Lerp(0.8f, 1.5f, shrinkProgress);

            AI_Timer++;

            //体节激光：半径越小开火越密集
            int baseFireChance = CWRWorld.Death ? 100 : 140;
            int fireChance = (int)(baseFireChance * (1f - easeOut * 0.6f));
            fireChance = Math.Max(fireChance, 20);

            if (AI_Timer > 60 && AI_Timer % 8 == 0 && bodySegments.Count > 0) {
                foreach (var segment in bodySegments) {
                    if (segment.active && Main.rand.NextBool(fireChance)) {
                        FireLaser(segment, targetPlayer.Center);
                    }
                }
            }

            if (AI_Timer > 400) {
                SwitchState(STATE_DASH_ASSAULT);
            }
        }

        private int MaxDashCount => isEnraged ? 5 : 3;
        private int ChargeTime => isEnraged ? 35 : 50;
        private int DashDuration => 35;
        private int CooldownTime => isEnraged ? 40 : 55;
        private float DashSpeed => isEnraged ? 55f : 42f;

        /// <summary>
        /// 连续冲刺：蓄力预警→爆发冲刺→减速回转，循环数次
        /// 子状态: 0=蓄力对准 1=冲刺 2=冷却回转
        /// </summary>
        private void ExecuteDashAssaultState() {
            switch ((int)AI_SubState) {
                case 0: //蓄力对准
                    ExecuteDashCharge();
                    break;
                case 1: //冲刺中
                    ExecuteDashRush();
                    break;
                case 2: //冷却回转
                    ExecuteDashCooldown();
                    break;
            }
        }

        private void ExecuteDashCharge() {
            //减速制动
            npc.velocity *= 0.92f;

            Vector2 toPlayer = targetPlayer.Center - npc.Center;
            dashDirection = toPlayer.SafeNormalize(Vector2.UnitY);

            //平滑转向对准目标
            float targetAngle = toPlayer.ToRotation() + MathHelper.PiOver2;
            npc.rotation = npc.rotation.AngleLerp(targetAngle, 0.15f);

            //蓄力进度
            chargeProgress = Math.Min(AI_Counter / (float)ChargeTime, 1f);

            //蓄力粒子：从体节向头部聚集
            if (!VaultUtils.isServer && AI_Counter % 3 == 0) {
                for (int i = 0; i < (int)(chargeProgress * 5) + 1; i++) {
                    Vector2 dustPos = npc.Center + Main.rand.NextVector2Circular(40, 40);
                    Dust dust = Dust.NewDustDirect(dustPos, 1, 1, DustID.FireworkFountain_Red, 0, 0, 100, default, 1.5f + chargeProgress);
                    dust.noGravity = true;
                    dust.velocity = (npc.Center - dustPos).SafeNormalize(Vector2.Zero) * (2f + chargeProgress * 4f);
                }
            }

            //蓄力后期震动
            if (chargeProgress > 0.6f && !VaultUtils.isServer) {
                float shakeMagnitude = (chargeProgress - 0.6f) * 5f;
                npc.Center += Main.rand.NextVector2Circular(shakeMagnitude, shakeMagnitude);
            }

            AI_Counter++;

            if (AI_Counter >= ChargeTime) {
                //蓄力完成，释放冲刺
                SoundEngine.PlaySound(SoundID.Roar with { Pitch = 0.2f }, npc.Center);
                moveVector = dashDirection * DashSpeed;
                npc.velocity = moveVector;
                chargeProgress = 0f;
                AI_SubState = 1;
                AI_Counter = 0;
                npc.netUpdate = true;
            }
        }

        private void ExecuteDashRush() {
            //保持冲刺速度，略微追踪玩家
            float trackingFactor = isEnraged ? 0.02f : 0.01f;
            Vector2 toPlayer = (targetPlayer.Center - npc.Center).SafeNormalize(Vector2.UnitY);
            npc.velocity = Vector2.Lerp(npc.velocity, toPlayer * DashSpeed, trackingFactor);
            npc.rotation = npc.velocity.ToRotation() + MathHelper.PiOver2;

            AI_Counter++;

            if (AI_Counter >= DashDuration) {
                AI_SubState = 2;
                AI_Counter = 0;
                dashCount++;
                npc.netUpdate = true;
            }
        }

        private void ExecuteDashCooldown() {
            //平滑减速
            npc.velocity *= 0.95f;

            //缓慢回转朝向玩家
            Vector2 toPlayer = targetPlayer.Center - npc.Center;
            float targetAngle = toPlayer.ToRotation() + MathHelper.PiOver2;
            npc.rotation = npc.rotation.AngleLerp(targetAngle, 0.05f);

            //以玩家上方为回归点
            targetPosition = targetPlayer.Center + new Vector2(0, -500);
            speed = 8f;
            turnSpeed = 0.3f;

            AI_Counter++;

            if (AI_Counter >= CooldownTime) {
                if (dashCount >= MaxDashCount) {
                    dashCount = 0;
                    SwitchState(STATE_IDLE_PATROL);
                }
                else {
                    //继续下一次冲刺蓄力
                    AI_SubState = 0;
                    AI_Counter = 0;
                    npc.netUpdate = true;
                }
            }
        }

        private void ExecuteDeathState() {
            npc.velocity *= 0.9f;
            npc.rotation += 0.05f;
            AI_Timer++;

            //爆炸特效
            if (AI_Timer % 5 == 0) {
                Vector2 randomPos = npc.Center + Main.rand.NextVector2Circular(100, 100);
                SoundEngine.PlaySound(SoundID.Item14, randomPos);
                Dust.NewDust(randomPos, 0, 0, DustID.Smoke, 0, 0, 100, default, 3f);
            }

            if (AI_Timer > 180) {
                npc.life = 0;
                npc.HitEffect();
                npc.checkDead();
            }
        }

        private static void SendDespawn() {
            if (VaultUtils.isSinglePlayer) {
                return;
            }
            var packet = CWRMod.Instance.GetPacket();
            packet.Write((byte)CWRMessageType.DespawnDestroyer);
            packet.Send();
        }

        internal static void HandleDespawn() {
            foreach (var n in Main.ActiveNPCs) {
                if (n.type == NPCID.TheDestroyer || n.type == NPCID.TheDestroyerBody
                    || n.type == NPCID.TheDestroyerTail || n.type == NPCID.Probe) {
                    n.life = 0;
                    n.HitEffect();
                    n.active = false;
                    n.netUpdate = true;
                }
            }
        }

        private void ExecuteDespawnState() {
            npc.velocity.Y = 82f;
            if (++AI_Timer > 180) {
                if (!VaultUtils.isClient) {
                    npc.active = false;
                    npc.netUpdate = true;
                    HandleDespawn();
                    SendDespawn();
                }
            }
            else {
                npc.dontTakeDamage = true;
            }
        }

        #endregion

        #region 辅助方法
        internal static void ForcedNetUpdating(NPC npc) {
            if (!VaultUtils.isServer || !npc.active || Main.GameUpdateCount % 80 != 0) {
                return;
            }

            foreach (var findPlayer in Main.ActivePlayers) {
                if (findPlayer.Distance(npc.position) < 1440) {
                    continue;
                }

                npc.SendNPCbasicData(findPlayer.whoAmI);
            }
        }

        private void SwitchState(int newState) {
            AI_State = newState;
            AI_Timer = 0;
            AI_SubState = 0;
            AI_Counter = 0;
            chargeProgress = 0f;
            if (newState != STATE_DASH_ASSAULT) {
                dashCount = 0;
            }
            npc.netUpdate = true;
        }

        private void FindTarget() {
            if (npc.target < 0 || npc.target >= 255 || !targetPlayer.Alives()) {
                npc.TargetClosest();
            }
            targetPlayer = Main.player[npc.target];

            if (!targetPlayer.Alives() && AI_State != STATE_DESPAWN) {
                SwitchState(STATE_DESPAWN);
            }
        }

        private void CheckEnrage() {
            isEnraged = npc.life < npc.lifeMax * 0.5f; //半血狂暴
        }

        private void UpdateBodyList() {
            if (Main.GameUpdateCount % 60 == 0) { //每秒更新一次身体列表
                bodySegments.Clear();
                foreach (var n in Main.ActiveNPCs) {
                    if ((n.type == NPCID.TheDestroyerBody || n.type == NPCID.TheDestroyerTail) && n.realLife == npc.whoAmI) {
                        bodySegments.Add(n);
                    }
                }
            }
        }

        private void UpdateMovement() {
            if (AI_State == STATE_DESPAWN) return;//脱战状态下不受常规物理控制
            if (AI_State == STATE_DASH_ASSAULT && AI_SubState == 1) return; //冲刺时不受常规物理控制

            //经典的蠕虫移动算法，但增加了平滑度
            float currentSpeed = npc.velocity.Length();
            Vector2 direction = targetPosition - npc.Center;
            float distance = direction.Length();
            direction.Normalize();

            if (currentSpeed < speed) {
                npc.velocity += direction * (speed / 20f); //加速度
            }

            //转向限制
            Vector2 desiredVelocity = direction * speed;
            npc.velocity = Vector2.Lerp(npc.velocity, desiredVelocity, turnSpeed / 20f);

            //强制最大速度
            if (npc.velocity.Length() > speed) {
                npc.velocity = npc.velocity.SafeNormalize(Vector2.Zero) * speed;
            }

            //旋转跟随速度
            npc.rotation = npc.velocity.ToRotation() + MathHelper.PiOver2;
        }

        private void FireLaser(NPC source, Vector2 target) {
            if (Main.netMode == NetmodeID.MultiplayerClient) return;
            float speed = 4;
            if (CWRWorld.Death) {
                speed += 2;
            }
            Vector2 velocity = (target - source.Center).SafeNormalize(Vector2.Zero) * speed;
            //我真的非常厌恶这些莫名其妙的伤害计算，泰拉的伤害计算就是一堆非常庞大的垃圾堆
            int damage = HeadPrimeAI.SetMultiplier(CWRRef.GetProjectileDamage(npc, ProjectileID.DeathLaser));
            Projectile.NewProjectile(source.GetSource_FromAI(), source.Center, velocity, ProjectileID.DeathLaser, damage, 0f, Main.myPlayer);

            //发射特效
            if (Main.netMode != NetmodeID.Server) {
                SoundEngine.PlaySound(SoundID.Item12, source.Center);
            }
        }

        private void SpawnBody() {
            int index = npc.whoAmI;
            int oldIndex;
            for (int i = 0; i < BodyCount; i++) { //增加体节数量
                oldIndex = index;
                index = NPC.NewNPC(npc.FromObjectGetParent(), (int)npc.Center.X, (int)npc.Center.Y
                    , i == (BodyCount - 1) ? NPCID.TheDestroyerTail : NPCID.TheDestroyerBody
                    , 0, ai0: oldIndex, ai1: index, ai2: 0, ai3: npc.whoAmI);
                Main.npc[index].realLife = npc.whoAmI;
                Main.npc[index].netUpdate = true;

                //强化体节属性
                if (CWRWorld.MachineRebellion) {
                    Main.npc[index].lifeMax = npc.lifeMax;
                    Main.npc[index].life = npc.life;
                    Main.npc[index].defense = 50;
                }
            }
        }

        private void HandleMouth() {
            VaultUtils.ClockFrame(ref glowFrame, 5, 3);

            //当速度方向与朝向玩家方向一致时张嘴
            float dotProduct = Vector2.Dot(npc.velocity.UnitVector(), npc.Center.To(targetPlayer.Center).UnitVector());
            float dist = npc.Distance(targetPlayer.Center);

            if (dist < 800 && dotProduct > 0.8f) {
                if (dontOpenMouthTime <= 0) openMouth = true;
            }
            else {
                openMouth = false;
            }

            if (openMouth) {
                if (frame < 3) frame++;
                dontOpenMouthTime = 60;
            }
            else {
                if (frame > 0) frame--;
            }

            if (dontOpenMouthTime > 0) dontOpenMouthTime--;
        }

        private void UpdateVisuals() {
            //身体发光
            Lighting.AddLight(npc.Center, 0.8f, 0.2f, 0.2f);
        }

        #endregion

        #region 绘制
        public override bool? Draw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            if (HeadPrimeAI.DontReform()) return true;

            Texture2D texture = Head.Value;
            Rectangle frameRec = texture.GetRectangle(frame, 4);
            Rectangle glowRec = texture.GetRectangle(glowFrame, 4);
            Vector2 origin = frameRec.Size() / 2;
            Vector2 mainPos = npc.Center - screenPos;

            //蓄力阶段特效
            if (AI_State == STATE_DASH_ASSAULT && AI_SubState == 0 && chargeProgress > 0.1f) {
                DrawChargeEffect(spriteBatch, mainPos);
            }

            //冲刺残影
            if (AI_State == STATE_DASH_ASSAULT && AI_SubState == 1) {
                for (int i = 0; i < npc.oldPos.Length; i++) {
                    if (npc.oldPos[i] == Vector2.Zero) continue;
                    float trailFade = 1f - i / (float)npc.oldPos.Length;
                    Vector2 drawPos = npc.oldPos[i] - screenPos + npc.Size / 2;
                    Color trailColor = Color.Lerp(Color.OrangeRed, Color.DarkRed, i / (float)npc.oldPos.Length) * (0.5f * trailFade);
                    float trailScale = npc.scale * (0.9f + 0.1f * trailFade);
                    spriteBatch.Draw(texture, drawPos, frameRec, trailColor, npc.rotation + MathHelper.Pi, origin, trailScale, SpriteEffects.None, 0f);
                }
            }

            //绘制本体
            spriteBatch.Draw(texture, mainPos, frameRec, drawColor, npc.rotation + MathHelper.Pi, origin, npc.scale, SpriteEffects.None, 0f);

            //绘制发光层
            spriteBatch.Draw(Head_Glow.Value, mainPos, glowRec, Color.White, npc.rotation + MathHelper.Pi, origin, npc.scale, SpriteEffects.None, 0f);

            return false;
        }

        /// <summary>
        /// 绘制冲刺蓄力阶段的视觉预警特效
        /// </summary>
        private void DrawChargeEffect(SpriteBatch spriteBatch, Vector2 drawPos) {
            Texture2D glowTex = CWRAsset.SoftGlow.Value;
            Texture2D circleTex = CWRAsset.DiffusionCircle.Value;
            Color chargeColor = Color.Lerp(Color.OrangeRed, Color.Red, chargeProgress);

            //切换到叠加混合
            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.AnisotropicClamp
                , DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

            //内圈光晕：随蓄力进度增大增亮
            float glowScale = 1.2f + chargeProgress * 2f;
            float glowAlpha = chargeProgress * 0.6f;
            Main.EntitySpriteDraw(glowTex, drawPos, null, chargeColor * glowAlpha
                , 0f, glowTex.Size() / 2f, glowScale, SpriteEffects.None, 0);

            //收缩圆环
            float circleScale = 2f - chargeProgress * 1.5f;
            float circleAlpha = chargeProgress * 0.5f;
            Main.EntitySpriteDraw(circleTex, drawPos, null, chargeColor * circleAlpha
                , Main.GlobalTimeWrappedHourly * 3f, circleTex.Size() / 2f, circleScale, SpriteEffects.None, 0);

            //瞄准方向指示线
            if (chargeProgress > 0.4f) {
                DrawAimIndicator(drawPos, chargeColor);
            }

            //恢复正常混合
            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState
                , DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
        }

        /// <summary>
        /// 绘制蓄力阶段的瞄准方向指示线
        /// </summary>
        private void DrawAimIndicator(Vector2 drawPos, Color baseColor) {
            Texture2D glowTex = CWRAsset.SoftGlow.Value;
            float lineLength = 1300f * (chargeProgress - 0.4f) / 0.6f;
            int segments = (int)(lineLength / 18f);
            float aimAlpha = (chargeProgress - 0.4f) / 0.6f;

            for (int i = 0; i < segments; i++) {
                float t = i / (float)Math.Max(segments, 1);
                Vector2 segPos = drawPos + dashDirection * (40f + t * lineLength);
                float segAlpha = aimAlpha * (1f - t) * 0.6f;
                float pulse = 0.7f + 0.3f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 8f + t * 6f);

                Main.EntitySpriteDraw(glowTex, segPos, null, baseColor * segAlpha * pulse
                    , 0f, glowTex.Size() / 2f, 0.25f * (1f - t * 0.5f), SpriteEffects.None, 0);
            }
        }

        public override bool PostDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            return HeadPrimeAI.DontReform();
        }
        #endregion

        #region 掉落物处理
        public override void ModifyNPCLoot(NPC thisNPC, NPCLoot npcLoot) {
            IItemDropRuleCondition condition = new DropInDeathMode();
            LeadingConditionRule rule = new LeadingConditionRule(condition);
            rule.SimpleAdd(ModContent.ItemType<DestroyersBlade>(), 4);
            rule.SimpleAdd(ModContent.ItemType<StaffoftheDestroyer>(), 4);
            rule.SimpleAdd(ModContent.ItemType<Observer>(), 4);
            npcLoot.Add(rule);
        }

        public override bool CheckActive() => false;
        #endregion

        #region 地图图标
        public override void BossHeadSlot(ref int index) {
            if (!HeadPrimeAI.DontReform()) {
                index = iconIndex;
            }
        }

        public override void BossHeadRotation(ref float rotation) {
            if (!HeadPrimeAI.DontReform()) {
                rotation = npc.rotation + MathHelper.Pi;
            }
        }

        public override void ModifyDrawNPCHeadBoss(ref float x, ref float y, ref int bossHeadId,
            ref byte alpha, ref float headScale, ref float rotation, ref SpriteEffects effects) {
            if (!HeadPrimeAI.DontReform()) {
                bossHeadId = iconIndex;
                rotation = npc.rotation + MathHelper.Pi;
            }
        }
        #endregion
    }
}

