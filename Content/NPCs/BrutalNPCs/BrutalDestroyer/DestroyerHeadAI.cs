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
        /// 巡空
        /// </summary>
        private void ExecutePatrolState() {
            //移动逻辑：在玩家上方或下方盘旋
            float patrolRadius = 800f;
            float timeFactor = Main.GameUpdateCount * 0.02f;
            Vector2 offset = new Vector2((float)Math.Cos(timeFactor), (float)Math.Sin(timeFactor) * 0.5f) * patrolRadius;
            targetPosition = targetPlayer.Center + offset;

            //速度控制
            speed = 18f;
            turnSpeed = 0.4f;

            //状态切换逻辑
            AI_Timer++;
            if (AI_Timer > 300) {
                //随机选择下一个攻击状态
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
            //减速以便稳定射击
            speed = 10f;
            turnSpeed = 0.8f;
            targetPosition = targetPlayer.Center + targetPlayer.velocity * 30f; //预判移动

            AI_Timer++;
            int fireRate = isEnraged ? 3 : 5; //狂暴状态射速更快

            //阶段1：充能预警
            if (AI_Timer < 60) {
                if (AI_Timer % 10 == 0) {
                    Dust.NewDust(npc.Center, npc.width, npc.height, DustID.FireworkFountain_Red, 0, 0, 100, default, 2f);
                }
            }
            //阶段2：波次射击
            else if (AI_Timer < 240) {
                if (AI_Timer % fireRate == 0) {
                    //这里的逻辑是让体节轮流发射，形成波浪感
                    int segmentIndex = (int)((AI_Timer - 60) / fireRate) % bodySegments.Count;
                    if (segmentIndex < bodySegments.Count) {
                        NPC segment = bodySegments[segmentIndex];
                        if (segment.active) {
                            FireLaser(segment, targetPlayer.Center);
                        }
                    }
                }
            }
            //阶段3：结束
            else {
                SwitchState(STATE_IDLE_PATROL);
            }
        }

        /// <summary>
        /// 快速包围玩家，收缩包围圈
        /// </summary>
        private void ExecuteEncircleState() {
            //计算环绕目标点
            float angle = AI_Timer * 0.05f;
            float radius = MathHelper.Lerp(1600f, 800f, Math.Min(AI_Timer / 300f, 1f)); //半径逐渐缩小
            Vector2 offset = angle.ToRotationVector2() * radius;
            targetPosition = targetPlayer.Center + offset;

            //极高的移动速度和转向能力
            speed = 35f;
            turnSpeed = 1.2f;

            AI_Timer++;

            int randNum = 160;
            if (CWRWorld.Death) {
                randNum -= 40;
            }
            //在环绕过程中，体节向圆心发射激光
            if (AI_Timer > 60 && AI_Timer % 10 == 0) {
                foreach (var segment in bodySegments) {
                    if (Main.rand.NextBool(randNum)) { //随机体节开火
                        FireLaser(segment, targetPlayer.Center);
                    }
                }
            }

            if (AI_Timer > 360) {
                SwitchState(STATE_DASH_ASSAULT); //环绕结束后直接接冲刺
            }
        }

        /// <summary>
        /// 连续冲刺
        /// </summary>
        private void ExecuteDashAssaultState() {
            //子状态管理
            //0: 瞄准/蓄力
            //1: 冲刺
            //2: 减速/回转

            switch ((int)AI_SubState) {
                case 0: //瞄准
                    speed = 5f;
                    turnSpeed = 2.5f; //极快转向对准玩家
                    Vector2 toPlayer = targetPlayer.Center - npc.Center;
                    targetPosition = targetPlayer.Center + toPlayer.SafeNormalize(Vector2.Zero) * 200f; //稍微越过玩家一点

                    //强制头部指向玩家
                    float targetAngle = toPlayer.ToRotation() + MathHelper.PiOver2;
                    npc.rotation = npc.rotation.AngleLerp(targetAngle, 0.2f);

                    AI_Counter++;
                    if (AI_Counter > 40) { //蓄力时间
                        AI_SubState = 1;
                        AI_Counter = 0;
                        SoundEngine.PlaySound(SoundID.Roar, npc.Center);
                        //冲刺向量确定
                        moveVector = toPlayer.SafeNormalize(Vector2.Zero) * (isEnraged ? 55f : 45f);
                    }
                    break;

                case 1: //冲刺
                    npc.velocity = moveVector;
                    npc.rotation = npc.velocity.ToRotation() + MathHelper.PiOver2;

                    AI_Counter++;
                    if (AI_Counter > 40) { //冲刺持续时间
                        AI_SubState = 2;
                        AI_Counter = 0;
                    }
                    break;

                case 2: //冷却/准备下一次
                    npc.velocity *= 0.99f; //摩擦力减速
                    AI_Counter++;
                    if (AI_Counter > 60) {
                        AI_SubState = 0;
                        AI_Counter = 0;

                        //检查是否结束冲刺阶段
                        if (AI_Timer > 4) { //已经冲刺了4次
                            SwitchState(STATE_IDLE_PATROL);
                        }
                        else {
                            AI_Timer++; //记录冲刺次数
                        }
                    }
                    break;
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

            //在冲刺时绘制残影
            if (AI_State == STATE_DASH_ASSAULT && AI_SubState == 1) {
                for (int i = 1; i < npc.oldPos.Length; i += 2) {
                    Vector2 drawPos = npc.oldPos[i] - screenPos + npc.Size / 2;
                    Color trailColor = Color.Red * 0.5f * (1f - i / (float)npc.oldPos.Length);
                    spriteBatch.Draw(texture, drawPos, frameRec, trailColor, npc.rotation + MathHelper.Pi, origin, npc.scale, SpriteEffects.None, 0f);
                }
            }

            //绘制本体
            spriteBatch.Draw(texture, npc.Center - screenPos, frameRec, drawColor, npc.rotation + MathHelper.Pi, origin, npc.scale, SpriteEffects.None, 0f);

            //绘制发光层
            spriteBatch.Draw(Head_Glow.Value, npc.Center - screenPos, glowRec, Color.White, npc.rotation + MathHelper.Pi, origin, npc.scale, SpriteEffects.None, 0f);

            return false;
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

