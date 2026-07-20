using CalamityOverhaul.Common;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
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
    internal class FishofCthulu : FishSkill
    {
        public override int UnlockFishID => ItemID.TheFishofCthulu;
        public override int DefaultCooldown => 60 * 11 - HalibutData.GetDomainLayer() / 2;
        public override int ResearchDuration => 60 * 25;

        /// <summary>
        /// 每次射击生成的眼球数量
        /// </summary>
        private int EyesPerShot => 1 + HalibutData.GetDomainLayer() / 3; //1-4个眼球

        public override bool? Shoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source,
            Vector2 position, Vector2 velocity, int type, int damage, float knockback) {

            //检查技能是否在冷却中
            if (Cooldown > 0) {
                return null;
            }

            //生成多个眼球
            for (int i = 0; i < EyesPerShot; i++) {
                //计算随机偏移角度
                float angleOffset = MathHelper.Lerp(-0.4f, 0.4f, i / (float)Math.Max(1, EyesPerShot - 1));
                Vector2 eyeVelocity = velocity.RotatedBy(angleOffset) * Main.rand.NextFloat(0.9f, 1.1f);

                //生成眼球
                int proj = Projectile.NewProjectile(
                    source,
                    position + Main.rand.NextVector2Circular(30f, 30f),
                    eyeVelocity,
                    ModContent.ProjectileType<CthulhuEye>(),
                    (int)(damage * (1.6f + HalibutData.GetDomainLayer() * 0.4f)),
                    knockback * 0.6f,
                    player.whoAmI,
                    ai0: i //个体索引
                );
            }

            //播放克苏鲁之眼召唤音效
            SoundEngine.PlaySound(SoundID.NPCHit1 with {
                Volume = 0.7f,
                Pitch = -0.5f,
                MaxInstances = 3
            }, position);

            //召唤特效
            SpawnSummonEffect(position);

            SetCooldown();

            return null;
        }

        private static void SpawnSummonEffect(Vector2 position) {
            if (Main.dedServ) {
                return;
            }
            //暗隙涌雾：召唤点先见暗，眼球各自的入场展开接管后续
            FishCthuluVFX.MistPuff(position, 4, 1.1f);
            FishCthuluVFX.BloodSpray(position, -Vector2.UnitY, 3, 3f);
            FishCthuluVFX.DarkRing(position, Vector2.UnitX, 0.8f);
        }
    }

    /// <summary>
    /// 克苏鲁之眼弹幕，具有追踪、冲刺和环绕能力。<br/>
    /// 演出：深渊血肉之眼，凝视期只有瞳孔追踪与雾丝游动的安静，
    /// 变形撕膜露齿是定帧英雄时刻，冲刺拖暗绸带尾流
    /// </summary>
    internal class CthulhuEye : ModProjectile, IPrimitiveDrawable
    {
        public override string Texture => "Terraria/Images/NPC_" + NPCID.EyeofCthulhu;

        private ref float EyeID => ref Projectile.ai[0];
        private ref float AIState => ref Projectile.ai[1];
        private ref float AITimer => ref Projectile.ai[2];

        //追踪目标
        private int targetNPC = -1;

        //环绕参数
        private float orbitAngle = 0f;
        private float orbitRadius = 0f;
        private float randOrbitRadius = 0f;
        private bool isOrbiting = false;
        private int orbitDuration = 0; //环绕持续时间

        //冲刺参数
        private bool isDashing = false;
        private Vector2 dashDirection = Vector2.Zero;
        private float dashSpeed = 0f;
        private int dashCooldown = 0;
        private int totalDashes = 0; //总冲刺次数

        //朝向和旋转
        private float desiredRotation = 0f;
        private float rotationSpeed = 0.2f;

        //智能决策参数
        private int noActionTimer = 0; //无有效行动计时器
        private const int MaxNoActionTime = 180; //最大无行动时间（3秒）
        private const int MinOrbitTime = 60; //最小环绕时间（1秒）
        private const int MaxOrbitTime = 150; //最大环绕时间（2.5秒）

        //动画参数
        private float frameTransition = 0f; //帧过渡进度 (0-1)
        private int targetMinFrame = 0; //目标最小帧
        private const float TransitionSpeed = 0.15f; //过渡速度
        private const int PreDashTime = 12; //冲刺前蓄力时间（帧）
        private const int PostDashTime = 20; //冲刺后恢复时间（帧）

        //==== 演出状态（纯视觉，不入网络）====
        private const int MaterializeTime = 14; //入场展开帧数
        private const int RibbonMaxPts = 22;
        private int spawnTimer; //入场计时
        private int irisFlash; //虹膜过冲闪帧，≤2 帧
        private int tearHold; //撕膜定帧：獠牙初帧冻结
        private bool tearDone; //本次蓄力是否已撕膜
        private float pupilStrain; //瞳孔紧张度 0..1，蓄力/冲刺散大
        private float stretchAlong = 1f; //沿朝向挤压拉伸（蓄力压缩/冲刺拉伸）
        private float wispPhase; //雾丝公转相位
        private Vector2 pupilTremor; //凝视微颤偏移
        private float ribbonFade; //冲刺绸带整体透明度包络
        private readonly List<Vector2> ribbonPts = new(RibbonMaxPts + 2);

        //状态枚举
        private enum EyeState
        {
            Seeking,      //寻找目标
            Orbiting,     //环绕目标
            PreDash,      //冲刺前蓄力
            Dashing,      //冲刺攻击
            PostDash,     //冲刺后恢复
            Returning     //返回环绕
        }

        //眼球瞳孔旋转
        private float pupilRotation = 0f;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 15;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
            Main.projFrames[Projectile.type] = 6;
        }

        public override void SetDefaults() {
            Projectile.width = 40;
            Projectile.height = 40;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 600;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 30;

            //初始化环绕参数
            orbitAngle = EyeID * MathHelper.TwoPi / 4f;
            randOrbitRadius = Main.rand.NextFloat(-20f, 20f);
            wispPhase = Main.rand.NextFloat(MathHelper.TwoPi);
        }

        public override bool? CanDamage() => AIState != (int)EyeState.Orbiting;

        public override void AI() {
            AITimer++;
            noActionTimer++;

            if (dashCooldown > 0) {
                dashCooldown--;
            }

            //状态机
            EyeState currentState = (EyeState)AIState;
            switch (currentState) {
                case EyeState.Seeking:
                    targetMinFrame = 0;
                    SeekingAI();
                    break;
                case EyeState.Orbiting:
                    targetMinFrame = 0;
                    OrbitingAI();
                    break;
                case EyeState.PreDash:
                    targetMinFrame = tearDone ? 3 : 0; //撕膜前仍是完整的眼，撕膜瞬间硬切獠牙帧组
                    PreDashAI();
                    break;
                case EyeState.Dashing:
                    targetMinFrame = 3; //保持露齿
                    DashingAI();
                    break;
                case EyeState.PostDash:
                    targetMinFrame = 3; //保持露齿一小段时间
                    PostDashAI();
                    break;
                case EyeState.Returning:
                    targetMinFrame = 0; //眼膜重新合拢
                    ReturningAI();
                    break;
            }

            //无行动超时保护 - 强制冲刺
            if (noActionTimer > MaxNoActionTime && currentState == EyeState.Orbiting) {
                if (targetNPC >= 0 && Main.npc[targetNPC].active) {
                    StartDash(Main.npc[targetNPC], true); //强制冲刺
                }
            }

            //平滑更新旋转
            UpdateRotation();

            //更新瞳孔朝向
            UpdatePupilRotation();

            //平滑更新帧动画
            UpdateFrameTransition();

            //演出计时与包络
            UpdateVisualEnvelopes();

            //粒子层：凝视期只有低频雾丝脱落，安静是观察期的灵魂；冲刺喷发独立
            if (!VaultUtils.isServer) {
                if (isDashing) {
                    if (Main.rand.NextBool(2)) {
                        SpawnDashParticles();
                    }
                }
                else if (Main.rand.NextBool(13)) {
                    SpawnIdleMist();
                }
            }

            //淡出效果
            if (Projectile.timeLeft < 30) {
                Projectile.alpha = (int)((1f - Projectile.timeLeft / 30f) * 255);
            }
        }

        /// <summary>入场/退场/闪帧/挤压拉伸/绸带的逐帧演出簿记</summary>
        private void UpdateVisualEnvelopes() {
            if (spawnTimer < 240) {
                spawnTimer++;
            }
            if (irisFlash > 0) {
                irisFlash--;
            }

            //挤压拉伸：蓄力沿冲刺向压缩，冲刺随速度拉长，其余回弹
            float stretchTarget = 1f;
            if (AIState == (float)EyeState.PreDash) {
                stretchTarget = 0.88f;
            }
            else if (isDashing) {
                stretchTarget = 1f + MathHelper.Clamp(Projectile.velocity.Length() / 26f, 0f, 1f) * 0.30f;
            }
            else if (AIState == (float)EyeState.PostDash) {
                stretchTarget = 0.95f;
            }
            stretchAlong = MathHelper.Lerp(stretchAlong, stretchTarget, 0.28f);

            //瞳孔紧张度：蓄力/冲刺散大，凝视期回落
            float strainTarget = AIState == (float)EyeState.PreDash || isDashing ? 1f
                : AIState == (float)EyeState.PostDash ? 0.45f : 0f;
            pupilStrain = MathHelper.Lerp(pupilStrain, strainTarget, 0.16f);

            //雾丝公转：缓慢基速 + 移动耦合
            wispPhase += 0.006f + Projectile.velocity.Length() * 0.0004f;

            if (VaultUtils.isServer) {
                return;
            }

            //虹膜微光的暗红点光：唯一光源，闪帧时略强
            float lightMul = irisFlash > 0 ? 0.30f : 0.09f;
            Lighting.AddLight(Projectile.Center, lightMul, lightMul * 0.16f, lightMul * 0.18f);

            //入场：暗雾中展开眼睑
            if (spawnTimer == 1) {
                FishCthuluVFX.MistPuff(Projectile.Center, 3, 0.9f);
                FishCthuluVFX.DarkRing(Projectile.Center, Projectile.velocity, 0.5f);
            }
            //退场：闭睑前释放雾丝，禁 pop-out
            if (Projectile.timeLeft == 28) {
                FishCthuluVFX.MistPuff(Projectile.Center, 3, 0.9f);
            }

            UpdateRibbon();
        }

        /// <summary>冲刺绸带点列：冲刺期录头，结束后尾端先蚀 + 渐隐（残迹比冲刺活得久）</summary>
        private void UpdateRibbon() {
            if (isDashing) {
                ribbonFade = Math.Min(1f, ribbonFade + 0.25f);
                //头点锚在体后：绸带从眼球身后拖出，不盖脸
                Vector2 head = Projectile.Center
                    - Projectile.velocity.SafeNormalize(Vector2.UnitX) * 16f * Projectile.scale;
                if (ribbonPts.Count == 0 || Vector2.DistanceSquared(ribbonPts[0], head) > 16f) {
                    ribbonPts.Insert(0, head);
                }
                else {
                    ribbonPts[0] = head;
                }
                if (ribbonPts.Count > RibbonMaxPts) {
                    ribbonPts.RemoveAt(ribbonPts.Count - 1);
                }
            }
            else {
                ribbonFade = Math.Max(0f, ribbonFade - 0.05f);
                if (ribbonPts.Count > 0) {
                    ribbonPts.RemoveAt(ribbonPts.Count - 1);
                    if (ribbonPts.Count > 12) {
                        ribbonPts.RemoveAt(ribbonPts.Count - 1);
                    }
                }
                if (ribbonFade <= 0f && ribbonPts.Count > 0) {
                    ribbonPts.Clear();
                }
            }
        }

        /// <summary>帧过渡 tick：撕膜后定格獠牙初帧，其余状态平滑过渡</summary>
        private void UpdateFrameTransition() {
            //撕膜定帧：獠牙初帧冻结数帧，变形读作一次性事件而非渐变
            if (tearHold > 0) {
                tearHold--;
                frameTransition = 1f;
                Projectile.frame = 3;
                return;
            }

            //计算当前帧过渡进度
            float targetTransition = targetMinFrame / 3f; //0或1 (因为minFrame是0或3)
            frameTransition = MathHelper.Lerp(frameTransition, targetTransition, TransitionSpeed);

            //根据过渡进度计算实际的最小帧
            int actualMinFrame = (int)Math.Round(frameTransition * 3);

            //更新帧动画（在minFrame和minFrame+2之间循环）
            VaultUtils.ClockFrame(ref Projectile.frame, 5, actualMinFrame + 2, actualMinFrame);
        }

        private void SeekingAI() {
            //寻找目标阶段
            if (targetNPC == -1 || !Main.npc[targetNPC].active || !Main.npc[targetNPC].CanBeChasedBy()) {
                var npc = Projectile.Center.FindClosestNPC(1000f);
                if (npc != null && npc.CanBeChasedBy()) {
                    targetNPC = npc.whoAmI;
                }
            }

            if (targetNPC != -1) {
                //找到目标，进入环绕状态
                AIState = (float)EyeState.Orbiting;
                AITimer = 0;
                orbitDuration = 0;
                isOrbiting = true;

                //播放锁定音效
                SoundEngine.PlaySound(SoundID.NPCHit1 with {
                    Volume = 0.4f,
                    Pitch = 0.3f
                }, Projectile.Center);
            }
            else {
                //没有目标时缓慢移动并逐渐减速
                Projectile.velocity *= 0.98f;

                //设置朝向为速度方向
                if (Projectile.velocity.LengthSquared() > 1f) {
                    desiredRotation = Projectile.velocity.ToRotation();
                }
            }
        }

        private void OrbitingAI() {
            //环绕目标阶段
            if (targetNPC < 0 || !Main.npc[targetNPC].active || !Main.npc[targetNPC].CanBeChasedBy()) {
                //目标丢失，返回寻找状态
                AIState = (float)EyeState.Seeking;
                targetNPC = -1;
                isOrbiting = false;
                return;
            }

            NPC target = Main.npc[targetNPC];
            orbitDuration++;

            //环绕角度递增，速度随领域等级提升
            float orbitSpeed = 0.08f + HalibutData.GetDomainLayer() * 0.01f;
            orbitAngle += orbitSpeed;

            orbitRadius = target.width / 2f + 40f + randOrbitRadius;
            //计算环绕位置
            Vector2 idealPosition = target.Center + orbitAngle.ToRotationVector2() * orbitRadius;

            //计算到理想位置的向量
            Vector2 toIdeal = idealPosition - Projectile.Center;
            float distance = toIdeal.Length();

            //平滑移动到环绕位置，使用更自然的速度曲线
            if (distance > 20f) {
                float targetSpeed = Math.Min(distance * 0.2f, 16f);
                Vector2 targetVelocity = toIdeal.SafeNormalize(Vector2.Zero) * targetSpeed;
                Projectile.velocity = Vector2.Lerp(Projectile.velocity, targetVelocity, 0.2f);
            }
            else {
                //接近理想位置时减速
                Projectile.velocity *= 0.95f;
            }

            //设置朝向为面向目标
            desiredRotation = (target.Center - Projectile.Center).ToRotation();

            //智能冲刺决策
            if (ShouldDash(target)) {
                StartDash(target);
            }
        }

        /// <summary>
        /// 冲刺前蓄力阶段
        /// </summary>
        private void PreDashAI() {
            AITimer++;

            //蓄力期间减速并调整朝向
            Projectile.velocity *= 0.88f;

            //保持朝向冲刺方向
            desiredRotation = dashDirection.ToRotation();

            //撕膜拍：蓄力中点眼膜裂开露齿，定帧 + 碎屑 + 微型咆哮
            if (!tearDone && AITimer >= PreDashTime / 2) {
                tearDone = true;
                frameTransition = 1f;
                tearHold = 8;
                irisFlash = 2;
                if (!VaultUtils.isServer) {
                    FishCthuluVFX.FleshBurst(Projectile.Center + dashDirection * 10f, dashDirection, 7);
                    FishCthuluVFX.BloodSpray(Projectile.Center, dashDirection, 3, 4.5f);
                    FishCthuluVFX.MistPuff(Projectile.Center, 2, 0.8f);
                    SoundEngine.PlaySound(SoundID.ForceRoar with {
                        Volume = 0.28f,
                        Pitch = 0.62f,
                        MaxInstances = 3
                    }, Projectile.Center);
                    SoundEngine.PlaySound(SoundID.NPCHit18 with {
                        Volume = 0.5f,
                        Pitch = -0.3f
                    }, Projectile.Center);
                }
            }

            //蓄力完成，进入冲刺
            if (AITimer >= PreDashTime) {
                AIState = (float)EyeState.Dashing;
                AITimer = 0;
                isDashing = true;

                //播放冲刺开始音效
                SoundEngine.PlaySound(SoundID.DD2_WyvernDiveDown with {
                    Volume = 0.5f,
                    Pitch = 0.3f
                }, Projectile.Center);
            }
        }

        /// <summary>
        /// 智能判断是否应该冲刺
        /// </summary>
        private bool ShouldDash(NPC target) {
            //冷却中不能冲刺
            if (dashCooldown > 0) {
                return false;
            }

            //刚开始环绕，等待一段时间
            if (AITimer < 20) {
                return false;
            }

            float distanceToTarget = Vector2.Distance(Projectile.Center, target.Center);

            //距离太近或太远都不冲刺
            if (distanceToTarget < 80f || distanceToTarget > 450f) {
                return false;
            }

            //计算冲刺概率，考虑多个因素
            float dashChance = CalculateDashChance(target, distanceToTarget);

            //使用概率决定
            return Main.rand.NextFloat() < dashChance;
        }

        /// <summary>
        /// 计算冲刺概率
        /// </summary>
        private float CalculateDashChance(NPC target, float distanceToTarget) {
            float baseChance = 0.02f; //基础概率 2%

            //环绕时间越长，冲刺概率越高
            if (orbitDuration > MinOrbitTime) {
                float orbitBonus = Math.Min((orbitDuration - MinOrbitTime) / 90f, 0.5f);
                baseChance += orbitBonus;
            }

            //强制冲刺条件：环绕时间过长
            if (orbitDuration > MaxOrbitTime) {
                return 1.0f; //100%冲刺
            }

            //距离因素：最佳冲刺距离（150-300）时概率更高
            if (distanceToTarget > 150f && distanceToTarget < 300f) {
                baseChance += 0.15f;
            }

            //目标移动速度因素：目标移动越快，冲刺概率越高
            float targetSpeed = target.velocity.Length();
            if (targetSpeed > 5f) {
                baseChance += Math.Min(targetSpeed / 50f, 0.1f);
            }

            //冲刺次数因素：冲刺次数少时更倾向于冲刺
            if (totalDashes < 3) {
                baseChance += 0.05f;
            }

            //领域等级加成
            baseChance += HalibutData.GetDomainLayer() * 0.01f;

            //位置因素：当在目标后方时更容易冲刺
            Vector2 toTarget = target.Center - Projectile.Center;
            float alignmentWithVelocity = Vector2.Dot(toTarget.SafeNormalize(Vector2.Zero), target.velocity.SafeNormalize(Vector2.Zero));
            if (alignmentWithVelocity > 0.5f) { //在目标前进方向前方
                baseChance += 0.1f;
            }

            return Math.Clamp(baseChance, 0f, 1f);
        }

        private void StartDash(NPC target, bool forced = false) {
            AIState = (float)EyeState.PreDash; //先进入蓄力状态
            AITimer = 0;
            totalDashes++;
            noActionTimer = 0; //重置无行动计时器
            tearDone = false; //本次蓄力的撕膜拍待触发

            //计算冲刺方向（预判目标移动）
            float predictionFactor = forced ? 25f : 20f; //强制冲刺时预判更多
            Vector2 predictedPos = target.Center + target.velocity * predictionFactor;
            dashDirection = (predictedPos - Projectile.Center).SafeNormalize(Vector2.Zero);

            //初始冲刺速度，强制冲刺时更快
            dashSpeed = (forced ? 26f : 22f) + HalibutData.GetDomainLayer() * 2f;

            //设置朝向为冲刺方向
            desiredRotation = dashDirection.ToRotation();

            //播放冲刺音效
            SoundEngine.PlaySound(SoundID.NPCHit1 with {
                Volume = forced ? 0.8f : 0.6f,
                Pitch = forced ? 0.7f : 0.5f
            }, Projectile.Center);

            //重置冷却，强制冲刺后冷却更长
            dashCooldown = (forced ? 110 : 90) - HalibutData.GetDomainLayer() * 6;

            //重置环绕持续时间
            orbitDuration = 0;
        }

        private void DashingAI() {
            //冲刺攻击阶段，持续30帧
            AITimer++;

            if (AITimer < 30) {
                //加速阶段（前10帧）
                if (AITimer < 10) {
                    dashSpeed *= 1.08f;
                }
                //维持高速阶段（10-20帧）
                else if (AITimer < 20) {
                    dashSpeed *= 0.99f;
                }
                //减速阶段（20-30帧）
                else {
                    dashSpeed *= 0.92f;
                }

                //应用冲刺速度
                Vector2 targetVelocity = dashDirection * dashSpeed;
                Projectile.velocity = Vector2.Lerp(Projectile.velocity, targetVelocity, 0.3f);

                //保持朝向为冲刺方向
                desiredRotation = dashDirection.ToRotation();
            }
            else {
                //冲刺结束，进入后摇恢复状态
                AIState = (float)EyeState.PostDash;
                AITimer = 0;
                isDashing = false;
            }
        }

        /// <summary>
        /// 冲刺后恢复阶段
        /// </summary>
        private void PostDashAI() {
            AITimer++;

            //快速减速
            Projectile.velocity *= 0.90f;

            //恢复完成，进入返回状态
            if (AITimer >= PostDashTime) {
                AIState = (float)EyeState.Returning;
                AITimer = 0;
            }
        }

        private void ReturningAI() {
            //返回环绕状态
            if (targetNPC < 0 || !Main.npc[targetNPC].active || !Main.npc[targetNPC].CanBeChasedBy()) {
                AIState = (float)EyeState.Seeking;
                targetNPC = -1;
                return;
            }

            NPC target = Main.npc[targetNPC];

            orbitRadius = target.width / 2f + 40f + randOrbitRadius;
            //计算目标环绕位置
            Vector2 orbitPosition = target.Center + orbitAngle.ToRotationVector2() * orbitRadius;
            Vector2 toOrbit = orbitPosition - Projectile.Center;
            float distanceToOrbit = toOrbit.Length();

            //根据距离调整速度
            float returnSpeed;
            if (distanceToOrbit > 200f) {
                //距离较远时快速返回
                returnSpeed = Math.Min(distanceToOrbit * 0.15f, 18f);
            }
            else if (distanceToOrbit > 80f) {
                //中等距离时中速
                returnSpeed = Math.Min(distanceToOrbit * 0.12f, 12f);
            }
            else {
                //接近目标位置时减速
                returnSpeed = Math.Min(distanceToOrbit * 0.1f, 8f);
            }

            Vector2 targetVelocity = toOrbit.SafeNormalize(Vector2.Zero) * returnSpeed;
            Projectile.velocity = Vector2.Lerp(Projectile.velocity, targetVelocity, 0.15f);

            //设置朝向为面向目标
            desiredRotation = (target.Center - Projectile.Center).ToRotation();

            orbitRadius = target.width / 2f + 40f + randOrbitRadius;
            //距离目标较近且速度较低时重新进入环绕
            if (distanceToOrbit < orbitRadius * 1.2f && Projectile.velocity.Length() < 10f) {
                AIState = (float)EyeState.Orbiting;
                AITimer = 0;
                orbitDuration = 0;
                isOrbiting = true;
                noActionTimer = 0; //重置无行动计时器
            }

            //超时保护，避免永久停留在返回状态
            if (AITimer > 120) {
                AIState = (float)EyeState.Orbiting;
                AITimer = 0;
                orbitDuration = 0;
                isOrbiting = true;
            }
        }

        private void UpdateRotation() {
            //平滑插值旋转角度
            float angleDiff = MathHelper.WrapAngle(desiredRotation - Projectile.rotation);

            //根据状态调整旋转速度
            float currentRotSpeed = rotationSpeed;
            if (isDashing) {
                currentRotSpeed = 0.4f; //冲刺时更快转向
            }
            else if (isOrbiting) {
                currentRotSpeed = 0.15f; //环绕时较慢转向，更优雅
            }

            //应用旋转
            Projectile.rotation += angleDiff * currentRotSpeed;
            Projectile.rotation = MathHelper.WrapAngle(Projectile.rotation);
        }

        private void UpdatePupilRotation() {
            //瞳孔焦点：最近的敌人或鼠标
            Vector2 focus = targetNPC >= 0 && Main.npc[targetNPC].active
                ? Main.npc[targetNPC].Center
                : Main.MouseWorld;
            float targetRot = (focus - Projectile.Center).ToRotation();

            //跳视：大偏差瞬时到位、小偏差缓跟，读作活物眼动而非匀速云台
            float diff = MathHelper.WrapAngle(targetRot - pupilRotation);
            if (Math.Abs(diff) > 0.55f) {
                pupilRotation = targetRot;
            }
            else {
                pupilRotation += diff * 0.30f;
            }

            //凝视微颤：环绕观察期偶发的眼神颤动
            if (AIState == (float)EyeState.Orbiting && Main.rand.NextBool(26)) {
                pupilTremor = Main.rand.NextVector2Circular(1.7f, 1.7f);
            }
            pupilTremor *= 0.86f;
        }

        /// <summary>凝视期脱落的雾丝：从体缘剥离，切向缓漂</summary>
        private void SpawnIdleMist() {
            Vector2 off = Main.rand.NextVector2Unit() * Main.rand.NextFloat(14f, 24f);
            PRTLoader.NewParticle<PRT_FishCthuluMist>(Projectile.Center + off
                , off.SafeNormalize(Vector2.UnitX).RotatedBy(MathHelper.PiOver2) * Main.rand.NextFloat(0.2f, 0.5f)
                , FishCthuluVFX.VoidMist, Main.rand.NextFloat(0.5f, 0.8f))
                ?.Configure(Main.rand.Next(40, 60));
        }

        /// <summary>冲刺喷发：尾端雾缕回卷 + 偶发暗血珠甩落，绸带承担主尾流</summary>
        private void SpawnDashParticles() {
            Vector2 tail = Projectile.Center - Projectile.velocity.SafeNormalize(Vector2.UnitX) * 18f;
            PRTLoader.NewParticle<PRT_FishCthuluMist>(tail + Main.rand.NextVector2Circular(8f, 8f)
                , -Projectile.velocity * Main.rand.NextFloat(0.06f, 0.12f)
                , FishCthuluVFX.VoidMist, Main.rand.NextFloat(0.55f, 0.85f))
                ?.Configure(Main.rand.Next(22, 34));
            if (Main.rand.NextBool(3)) {
                PRTLoader.NewParticle<PRT_HeartcarverDroplet>(tail
                    , -Projectile.velocity.RotatedByRandom(0.4f) * 0.15f
                    , Color.Lerp(FishCthuluVFX.FleshMid, FishCthuluVFX.FleshDark, Main.rand.NextFloat(0.5f))
                    , Main.rand.NextFloat(0.5f, 0.75f))?.Configure(Main.rand.Next(14, 22));
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            //击中效果
            SoundEngine.PlaySound(SoundID.NPCHit1 with {
                Pitch = 0.2f
            }, Projectile.Center);

            //重置无行动计时器（击中算有效行动）
            noActionTimer = 0;

            //撕咬迸发：暗血飞沫锥 + 眼膜碎屑 + 雾涌 + 定向暗环
            irisFlash = 2;
            if (!VaultUtils.isServer) {
                Vector2 biteDir = isDashing || AIState == (float)EyeState.PreDash
                    ? dashDirection
                    : (target.Center - Projectile.Center).SafeNormalize(Vector2.UnitX);
                FishCthuluVFX.BloodSpray(target.Center, biteDir, 6, 7f);
                FishCthuluVFX.FleshBurst(target.Center, biteDir, 3);
                FishCthuluVFX.MistPuff(target.Center, 2, 0.9f, biteDir * 1.2f);
                FishCthuluVFX.DarkRing(target.Center, biteDir, 0.7f);
                SoundEngine.PlaySound(SoundID.NPCHit18 with {
                    Volume = 0.45f,
                    Pitch = -0.1f
                }, target.Center);
            }

            //冲刺击中时造成debuff
            if (isDashing || AIState == (float)EyeState.PreDash || AIState == (float)EyeState.PostDash) {
                target.AddBuff(BuffID.ShadowFlame, 180);
            }

            //击中后如果在冲刺相关状态，立即进入后摇恢复状态
            if (isDashing) {
                AIState = (float)EyeState.PostDash;
                isDashing = false;
                AITimer = 0;
            }
            else if (AIState == (float)EyeState.PreDash) {
                //蓄力时被打断，直接进入返回
                AIState = (float)EyeState.Returning;
                AITimer = 0;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            //加载眼球纹理
            Main.instance.LoadNPC(NPCID.EyeofCthulhu);
            Texture2D texture = TextureAssets.Npc[NPCID.EyeofCthulhu].Value;

            //计算纹理参数
            int frameHeight = texture.Height / Main.npcFrameCount[NPCID.EyeofCthulhu];
            Rectangle sourceRect = new Rectangle(0, Projectile.frame * frameHeight, texture.Width, frameHeight);
            Vector2 origin = sourceRect.Size() / 2f;

            float fadeAlpha = 1f - Projectile.alpha / 255f;
            Vector2 mainDrawPos = Projectile.Center - Main.screenPosition;

            //入场/退场包络
            float matT = MathHelper.Clamp(spawnTimer / (float)MaterializeTime, 0f, 1f);
            float deathT = Projectile.timeLeft < 30 ? 1f - Projectile.timeLeft / 30f : 0f;

            //夹心下层：两条雾丝画在眼球之下
            DrawVoidWisps(mainDrawPos, fadeAlpha, matT, true);

            //冲刺残影：暗肉色半透明鬼影，只在冲刺三态出现，凝视期静止无残影
            bool dashPhases = isDashing || AIState == (float)EyeState.PreDash || AIState == (float)EyeState.PostDash;
            if (dashPhases) {
                for (int i = 2; i < Projectile.oldPos.Length; i += 3) {
                    if (Projectile.oldPos[i] == Vector2.Zero) {
                        continue;
                    }
                    float p = 1f - i / (float)Projectile.oldPos.Length;
                    Vector2 ghostPos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                    Color ghostCol = (FishCthuluVFX.FleshDark with { A = 150 }) * (p * 0.42f * fadeAlpha);
                    Main.EntitySpriteDraw(texture, ghostPos, sourceRect, ghostCol
                        , Projectile.oldRot[i] - MathHelper.PiOver2, origin
                        , Projectile.scale * 0.6f * (0.72f + p * 0.24f), SpriteEffects.None, 0);
                }
            }

            //蓄力后坐偏移：撕膜期附带微幅颤抖
            Vector2 anticipationOff = Vector2.Zero;
            if (AIState == (float)EyeState.PreDash) {
                float preT = MathHelper.Clamp(AITimer / (float)PreDashTime, 0f, 1f);
                anticipationOff = -dashDirection * 5f * preT;
                if (tearHold > 0) {
                    anticipationOff += Main.rand.NextVector2Circular(0.8f, 0.8f);
                }
            }

            //本体：压暗偏紫的血肉瞳体 + 挤压拉伸
            Vector2 squash = ComputeBodySquash(matT, deathT);
            Color bodyCol = new Color((int)(lightColor.R * 0.70f), (int)(lightColor.G * 0.58f)
                , (int)(lightColor.B * 0.76f), 255) * fadeAlpha;
            Main.EntitySpriteDraw(texture, mainDrawPos + anticipationOff, sourceRect, bodyCol
                , Projectile.rotation - MathHelper.PiOver2, origin
                , new Vector2(0.6f * squash.X, 0.6f * squash.Y) * Projectile.scale, SpriteEffects.None, 0);

            //夹心上层：一条低透明雾丝盖在眼球上，形成体积包裹
            DrawVoidWisps(mainDrawPos, fadeAlpha, matT, false);

            //瞳孔与虹膜：追踪是凝视的灵魂；撕膜露齿后眼已成巨口，瞳孔随过渡淡出
            float pupilFade = MathHelper.Clamp(1f - frameTransition * 1.6f, 0f, 1f);
            DrawPupil(mainDrawPos + anticipationOff, fadeAlpha * matT * (1f - deathT) * pupilFade);

            //撕膜/撕咬过冲：≤2 帧的虹膜色尖刺闪，唯一允许的瞬时亮点
            if (irisFlash > 0) {
                Texture2D tear = CWRAsset.TearSpread01?.Value;
                if (tear != null) {
                    Main.EntitySpriteDraw(tear, mainDrawPos + anticipationOff, null
                        , (FishCthuluVFX.IrisRed with { A = 0 }) * (0.75f * fadeAlpha)
                        , pupilRotation, tear.Size() / 2f, 0.55f * Projectile.scale, SpriteEffects.None, 0);
                }
            }

            return false;
        }

        /// <summary>体缩放向量：x=横向 y=沿朝向；呼吸 + 蓄力压缩/冲刺拉伸 + 开闭睑</summary>
        private Vector2 ComputeBodySquash(float matT, float deathT) {
            float breath = MathF.Sin(Main.GlobalTimeWrappedHourly * 2.2f + Projectile.whoAmI * 1.71f) * 0.03f;
            float along = stretchAlong * (1f - breath);
            float across = (1f - (stretchAlong - 1f) * 0.6f) * (1f + breath);

            //入场：眼睑从缝隙弹开
            across *= MathHelper.Lerp(0.10f, 1f, FishCthuluVFX.EaseOutBack(matT));
            along *= MathHelper.Lerp(1.12f, 1f, FishCthuluVFX.SmoothStep01(matT));

            //退场：合拢成缝再消失，禁 pop-out
            if (deathT > 0f) {
                across *= MathHelper.Lerp(1f, 0.07f, FishCthuluVFX.SmoothStep01(deathT));
                along *= 1f + 0.08f * deathT;
            }
            return new Vector2(across, along);
        }

        /// <summary>虚空雾丝：贴体公转的暗紫雾带，under 画体下两条、体上一条低透明</summary>
        private void DrawVoidWisps(Vector2 drawPos, float fade, float matT, bool underLayer) {
            Texture2D smoke = CWRAsset.SmokeSheet01?.Value;
            if (smoke == null || fade <= 0.01f || matT <= 0.05f) {
                return;
            }
            float t = Main.GlobalTimeWrappedHourly;
            int wispCount = underLayer ? 2 : 1;
            for (int i = 0; i < wispCount; i++) {
                int idx = underLayer ? i : 2;
                float phase = wispPhase + idx * (MathHelper.TwoPi / 3f);
                float radius = (24f + MathF.Sin(t * 0.8f + idx * 2.1f) * 7f) * Projectile.scale * matT;
                Vector2 off = phase.ToRotationVector2() * radius;
                //冲刺时雾丝被拖到体后
                if (isDashing) {
                    off = off * 0.5f - Projectile.velocity.SafeNormalize(Vector2.Zero) * 16f;
                }
                int frameIdx = (Projectile.whoAmI + idx * 7) % 4;
                int frameSize = smoke.Width / 2;
                Rectangle fr = new(frameIdx % 2 * frameSize, frameIdx / 2 * frameSize, frameSize, frameSize);
                Vector2 fo = fr.Size() / 2f;
                float alpha = (underLayer ? 0.35f : 0.20f) * fade * matT;
                float rot = phase * 0.5f + t * 0.25f;
                float scl = (0.16f + MathF.Sin(t * 1.1f + idx) * 0.02f) * Projectile.scale;
                //外圈更暗更大 + 中层：两层异径异色压暗，不发光
                Main.EntitySpriteDraw(smoke, drawPos + off, fr, FishCthuluVFX.VoidDark * (alpha * 0.7f)
                    , rot * 0.9f, fo, scl * 1.3f, SpriteEffects.None, 0);
                Main.EntitySpriteDraw(smoke, drawPos + off, fr, FishCthuluVFX.VoidMist * alpha
                    , rot, fo, scl, SpriteEffects.None, 0);
            }
        }

        /// <summary>瞳孔（近黑实心）+ 虹膜微光（小尺度暗红加色），偏移实时追踪焦点</summary>
        private void DrawPupil(Vector2 drawPos, float fade) {
            Texture2D dot = CWRAsset.Extra_98?.Value;
            if (dot == null || fade <= 0.01f) {
                return;
            }
            Vector2 facing = Projectile.rotation.ToRotationVector2();
            Vector2 anchor = drawPos + facing * 13f * Projectile.scale;
            Vector2 fine = pupilRotation.ToRotationVector2() * (3.5f + pupilStrain * 3f) + pupilTremor;
            Vector2 pupilPos = anchor + fine;
            Vector2 o = dot.Size() * 0.5f;

            //虹膜微光：呼吸脉动的暗红，紧张与闪帧时增幅，永不到白
            float pulse = 0.85f + MathF.Sin(Main.GlobalTimeWrappedHourly * 2.6f + Projectile.whoAmI) * 0.15f;
            float irisAlpha = (0.30f + pupilStrain * 0.18f + (irisFlash > 0 ? 0.55f : 0f)) * pulse * fade;
            Main.EntitySpriteDraw(dot, pupilPos, null, (FishCthuluVFX.IrisRed with { A = 0 }) * irisAlpha
                , 0f, o, 0.34f + pupilStrain * 0.05f, SpriteEffects.None, 0);

            //瞳墨：紧张时散大
            Main.EntitySpriteDraw(dot, pupilPos, null, FishCthuluVFX.PupilInk * (0.92f * fade)
                , 0f, o, 0.16f + pupilStrain * 0.06f, SpriteEffects.None, 0);
        }

        void IPrimitiveDrawable.DrawPrimitives() {
            if (Main.dedServ || ribbonPts.Count < 3 || ribbonFade <= 0.01f) {
                return;
            }
            Effect fx = FishCthuluAssets.FishCthuluRibbon;
            if (fx == null || CWRAsset.PerlinNoise?.Value == null) {
                return;
            }

            float fadeAlpha = 1f - Projectile.alpha / 255f;
            int count = ribbonPts.Count;
            float maxWidth = 15f * Projectile.scale;
            var verts = new VertexPositionColorTexture[count * 2];
            for (int i = 0; i < count; i++) {
                float t = i / (float)(count - 1);
                Vector2 tangent = i < count - 1
                    ? (ribbonPts[i] - ribbonPts[i + 1]).SafeNormalize(Vector2.UnitX)
                    : (ribbonPts[i - 1] - ribbonPts[i]).SafeNormalize(Vector2.UnitX);
                Vector2 normal = new(-tangent.Y, tangent.X);
                float width = maxWidth * (0.55f + 0.45f * MathHelper.Clamp(t / 0.14f, 0f, 1f))
                    * MathF.Pow(1f - t, 0.78f);
                verts[i * 2] = new VertexPositionColorTexture((ribbonPts[i] + normal * width).ToVector3()
                    , Color.White, new Vector2(t, 0f));
                verts[i * 2 + 1] = new VertexPositionColorTexture((ribbonPts[i] - normal * width).ToVector3()
                    , Color.White, new Vector2(t, 1f));
            }

            GraphicsDevice device = Main.instance.GraphicsDevice;
            BlendState prevBlend = device.BlendState;
            RasterizerState prevRaster = device.RasterizerState;
            device.BlendState = BlendState.AlphaBlend;
            device.RasterizerState = RasterizerState.CullNone;

            FishCthuluVFX.ApplyRibbon(fx, Projectile.whoAmI * 0.61f % 1f, ribbonFade * fadeAlpha);
            foreach (EffectPass pass in fx.CurrentTechnique.Passes) {
                pass.Apply();
                device.DrawUserPrimitives(PrimitiveType.TriangleStrip, verts, 0, verts.Length - 2);
            }

            device.BlendState = prevBlend;
            device.RasterizerState = prevRaster;
        }
    }
}
