using CalamityOverhaul.Common;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.HalibutLegend.FishSkills
{
    /// <summary>
    /// 史莱姆鱼技能，生成粘性凝胶球
    /// </summary>
    internal class FishSlime : FishSkill
    {
        public override int UnlockFishID => ItemID.Slimefish;
        public override int DefaultCooldown => 90 - HalibutData.GetDomainLayer() * 6;
        public override int ResearchDuration => 60 * 16;
        //凝胶球生成计数器
        private int gelCounter = 0;
        private const int GelInterval = 10;

        //活跃的凝胶球追踪
        private static readonly List<int> ActiveGels = new();
        private static int MaxGels => 4 + HalibutData.GetDomainLayer();

        public override bool UpdateCooldown(HalibutPlayer halibutPlayer, Player player) {
            if (!Active(player)) {
                return false;
            }

            ShootState shootState = player.GetShootState();
            Vector2 velocity = player.velocity;
            Vector2 position = player.Center;

            if (velocity.LengthSquared() < 9) {
                return false;
            }

            gelCounter++;

            //周期性生成凝胶球
            if (gelCounter >= GelInterval && Cooldown <= 0) {
                gelCounter = 0;
                SetCooldown();

                CleanupInactiveGels();

                if (ActiveGels.Count < MaxGels) {
                    //生成凝胶球
                    Vector2 shootDir = velocity.SafeNormalize(Vector2.Zero);
                    Vector2 gelVelocity = shootDir * Main.rand.NextFloat(8f, 14f);
                    gelVelocity += Main.rand.NextVector2Circular(3f, 3f);

                    int gelProj = Projectile.NewProjectile(
                        shootState.Source,
                        position,
                        gelVelocity,
                        ModContent.ProjectileType<SlimeGelOrb>(),
                        (int)(shootState.WeaponDamage * (1.6f + HalibutData.GetDomainLayer() * 0.4f)),
                        shootState.WeaponKnockback * 1.5f,
                        player.whoAmI
                    );

                    if (gelProj >= 0) {
                        ActiveGels.Add(gelProj);

                        //凝胶生成音效
                        SoundEngine.PlaySound(SoundID.Item95 with {
                            Volume = 0.5f,
                            Pitch = 0.3f
                        }, position);

                        SoundEngine.PlaySound(SoundID.NPCHit1 with {
                            Volume = 0.4f,
                            Pitch = 0.5f
                        }, position);

                        //生成效果
                        SpawnGelCreateEffect(position, gelVelocity);
                    }
                }
            }
            return true;
        }

        private static void CleanupInactiveGels() {
            ActiveGels.RemoveAll(id => {
                if (id < 0 || id >= Main.maxProjectiles) return true;
                Projectile proj = Main.projectile[id];
                return !proj.active || proj.type != ModContent.ProjectileType<SlimeGelOrb>();
            });
        }

        //凝胶出手效果
        private static void SpawnGelCreateEffect(Vector2 position, Vector2 gelVelocity) {
            if (Main.dedServ) {
                return;
            }
            Vector2 back = -gelVelocity.SafeNormalize(Vector2.UnitY);
            for (int i = 0; i < 4; i++) {
                FishSlimeVFX.Droplet(position + Main.rand.NextVector2Circular(6f, 6f)
                    , back.RotatedByRandom(0.6f) * Main.rand.NextFloat(1f, 3f) + gelVelocity * 0.12f
                    , Main.rand.NextFloat(0.5f, 0.85f));
            }
            for (int i = 0; i < 3; i++) {
                Dust gel = Dust.NewDustPerfect(position, DustID.TintableDust
                    , Main.rand.NextVector2Circular(3f, 3f), 120, FishSlimeVFX.GelBody, Main.rand.NextFloat(1f, 1.5f));
                gel.noGravity = true;
            }
        }
    }

    /// <summary>全局钩子，Halibut 攻击附加减速</summary>
    internal class FishSlimeGlobalProj : GlobalProjectile
    {
        public override void OnHitNPC(Projectile projectile, NPC target, NPC.HitInfo hit, int damageDone) {
            if (projectile.owner.TryGetPlayer(out var player)
                && FishSkill.GetT<FishSlime>().Active(player)) {
                //在这个技能下攻击会附加减速效果
                target.AddBuff(BuffID.Slimed, 180 + HalibutData.GetDomainLayer() * 20);
            }
        }
    }

    /// <summary>
    /// 史莱姆凝胶球弹幕，半透明果冻 blob（shader quad 本体）+ 果冻阻尼震荡形变 +
    /// 发射/凝胶间拉丝（拉长-绷断-回缩三拍）+ 附着宿主随动 wobble
    /// </summary>
    internal class SlimeGelOrb : ModProjectile, IPrimitiveDrawable
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        //凝胶状态
        private enum GelState
        {
            Floating,   //漂浮状态
            Attached,   //附着状态
            Exploding   //爆炸状态
        }

        private GelState State {
            get => (GelState)Projectile.ai[0];
            set => Projectile.ai[0] = (float)value;
        }

        private ref float GelLife => ref Projectile.ai[1];
        private ref float AttachedTargetID => ref Projectile.ai[2];

        //追踪目标系统
        private int trackingTargetID = -1;
        private const float TrackingRange = 120f; //追踪范围
        private const float TrackingForce = 0.3f; //追踪力度基础值
        private const float MaxTrackingSpeed = 16f; //最大追踪速度
        private int bounceCount = 0; //弹跳计数
        private const int MaxBounces = 5; //最大弹跳次数后追踪力增强

        //粘连系统
        private readonly List<int> ConnectedGels = new();
        private const float ConnectionRange = 180f;
        private const float AttachRange = 120f;

        //凝胶物理参数
        private const float Gravity = 0.25f;
        private const float Bounce = 0.6f;
        private const float AirFriction = 0.98f;

        //果冻形变，主轴角
        private float deformAngle;
        private float stretchAxis = 1f;
        private float squashAxis = 1f;
        private float wobbleAmp;
        private float wobblePhase;
        private Vector2 lastVelocity = Vector2.Zero;
        private Vector2 lastTargetVel = Vector2.Zero;
        //内部高光方向，默认左上光源
        private Vector2 highlightDir = new(-0.45f, -0.55f);
        //附着点体表偏移，多球分点粘附
        private Vector2 anchorOffset = Vector2.Zero;

        //发射拉丝，玩家→球，拉长到极限绷断
        private bool launchStrandAlive = true;
        private const float LaunchStrandSnapLen = 190f;
        //上帧连接集，检测凝胶间拉丝绷断
        private readonly HashSet<int> prevConnected = new();

        //爆炸参数
        private const int MaxLifeTime = 600;
        private const int PreExplosionTime = 30;

        //爆前充能 0..1
        private float ChargeT => State == GelState.Exploding
            ? MathHelper.Clamp(1f - Projectile.timeLeft / (float)PreExplosionTime, 0f, 1f) : 0f;
        //本体半径 px
        private float BlobRadius => 20f * MathHelper.Clamp(GelLife / 6f, 0.25f, 1f) * (1f + ChargeT * 0.32f);
        private float Seed => Projectile.identity * 0.173f;

        public override void SetDefaults() {
            Projectile.width = 36;
            Projectile.height = 36;
            Projectile.friendly = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = MaxLifeTime;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = false;
            Projectile.alpha = 0;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
        }
        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {
            if (target.IsWormBody()) {
                modifiers.FinalDamage *= 0.7f;
            }
            if (target.type == CWRID.NPC_DevourerofGodsHead || target.type == CWRID.NPC_DevourerofGodsTail) {
                modifiers.FinalDamage *= 1.43f;
            }
        }

        public override void AI() {
            GelLife++;

            //更新追踪目标
            UpdateTracking();

            switch (State) {
                case GelState.Floating:
                    FloatingPhaseAI();
                    break;
                case GelState.Attached:
                    AttachedPhaseAI();
                    break;
                case GelState.Exploding:
                    ExplodingPhaseAI();
                    break;
            }

            //更新粘连关系
            UpdateConnections();

            //发射拉丝存续
            UpdateLaunchStrand();

            //果冻震荡效果
            UpdateJellyPhysics();

            //内部高光，追踪时偏向目标
            UpdateHighlight();

            //半透明凝胶的微弱蓝光，不做灯泡
            float lightIntensity = 0.9f * (1f - Projectile.alpha / 255f);
            Lighting.AddLight(Projectile.Center,
                0.08f * lightIntensity,
                0.18f * lightIntensity,
                0.34f * lightIntensity);

            //接近生命终点时进入爆炸前兆
            if (Projectile.timeLeft <= PreExplosionTime && State != GelState.Exploding) {
                State = GelState.Exploding;

                //爆炸前兆音效
                SoundEngine.PlaySound(SoundID.NPCHit1 with {
                    Volume = 0.5f,
                    Pitch = 0.8f
                }, Projectile.Center);
            }
        }

        //更新追踪系统
        private void UpdateTracking() {
            if (trackingTargetID >= 0 && trackingTargetID < Main.maxNPCs) {
                NPC target = Main.npc[trackingTargetID];
                if (!target.active || !target.CanBeChasedBy() || target.friendly) {
                    trackingTargetID = -1;
                }
                else {
                    float distToTarget = Vector2.Distance(Projectile.Center, target.Center);
                    if (distToTarget > TrackingRange * 1.5f) {
                        //目标太远，丢失锁定
                        trackingTargetID = -1;
                    }
                }
            }

            //如果没有追踪目标，寻找新目标
            if (trackingTargetID < 0 && State == GelState.Floating) {
                FindTrackingTarget();
            }
        }

        //寻找追踪目标
        private void FindTrackingTarget() {
            NPC closestNPC = Projectile.Center.FindClosestNPC(TrackingRange);

            if (closestNPC != null) {
                trackingTargetID = closestNPC.whoAmI;

                //锁定反馈，果冻抖一下 + 单滴甩落
                ExciteWobble(0.16f, closestNPC.Center - Projectile.Center);
                if (!Main.dedServ) {
                    FishSlimeVFX.Droplet(Projectile.Center, Main.rand.NextVector2Circular(1.5f, 1f)
                        , Main.rand.NextFloat(0.5f, 0.7f));
                }
            }
        }

        //漂浮 tick
        private void FloatingPhaseAI() {
            //应用重力
            Projectile.velocity.Y += Gravity;

            //空气阻力
            Projectile.velocity *= AirFriction;

            //轻微漂浮效果
            float floatOscillation = (float)Math.Sin(GelLife * 0.1f) * 0.1f;
            Projectile.velocity.Y += floatOscillation;

            //追踪AI - 自然的追踪行为
            if (trackingTargetID >= 0 && trackingTargetID < Main.maxNPCs) {
                NPC target = Main.npc[trackingTargetID];
                if (target.active) {
                    Vector2 toTarget = target.Center - Projectile.Center;
                    float distance = toTarget.Length();

                    if (distance > 20f) {
                        //计算追踪强度 - 距离越近追踪越强
                        float distanceRatio = 1f - Math.Min(distance / TrackingRange, 1f);

                        //弹跳次数越多，追踪力越强
                        float bounceBonus = Math.Min(bounceCount * 0.15f, 0.6f);

                        //下落时追踪力增强
                        float fallBonus = Projectile.velocity.Y > 0 ? 0.3f : 0f;

                        //综合追踪力度
                        float trackingStrength = TrackingForce * (0.6f + distanceRatio * 0.4f + bounceBonus + fallBonus);

                        //计算追踪加速度
                        Vector2 trackingAccel = toTarget.SafeNormalize(Vector2.Zero) * trackingStrength;

                        //追踪，加速度叠层
                        Projectile.velocity += trackingAccel;

                        //限制最大速度，但允许重力和弹跳的自然速度
                        float currentSpeed = Projectile.velocity.Length();
                        if (currentSpeed > MaxTrackingSpeed) {
                            Projectile.velocity = Projectile.velocity.SafeNormalize(Vector2.Zero) *
                                MathHelper.Lerp(currentSpeed, MaxTrackingSpeed, 0.1f);
                        }
                    }
                }
            }

            //高速甩珠
            if (!Main.dedServ && Projectile.velocity.Length() > 8.5f && Main.rand.NextBool(7)) {
                FishSlimeVFX.Droplet(Projectile.Center + Main.rand.NextVector2Circular(6f, 6f)
                    , -Projectile.velocity * 0.15f + Main.rand.NextVector2Circular(0.8f, 0.8f)
                    , Main.rand.NextFloat(0.45f, 0.7f));
            }

            //检测附着目标
            CheckAttachment();

            if (Projectile.localAI[0] > 0) {
                Projectile.localAI[0]--;
            }
        }

        //附着 tick
        private void AttachedPhaseAI() {
            int targetID = (int)AttachedTargetID;

            if (targetID >= 0 && targetID < Main.maxNPCs) {
                NPC target = Main.npc[targetID];
                if (target.active) {
                    //跟随目标体表锚点
                    Projectile.Center = Vector2.Lerp(Projectile.Center, target.Center + anchorOffset, 0.15f);
                    Projectile.velocity = target.velocity * 0.8f;

                    //宿主速度突变激发 wobble
                    Vector2 hostDv = target.velocity - lastTargetVel;
                    if (hostDv.Length() > 2.2f) {
                        ExciteWobble(Math.Min(hostDv.Length() * 0.06f, 0.4f), hostDv);
                    }
                    lastTargetVel = target.velocity;

                    //宿主高速移动时凝胶被甩出滴液
                    if (!Main.dedServ && target.velocity.Length() > 5f && Main.rand.NextBool(10)) {
                        FishSlimeVFX.Droplet(Projectile.Center + new Vector2(0f, BlobRadius * 0.5f)
                            , target.velocity * 0.25f + new Vector2(0f, 0.6f), Main.rand.NextFloat(0.5f, 0.75f));
                    }

                    //周期性造成伤害
                    if (GelLife % 30 == 0) {
                        target.SimpleStrikeNPC(Projectile.damage / 4, 0, false, 0f, null, false, 0f, true);

                        //伤害 tick
                        ExciteWobble(0.2f, anchorOffset);
                        if (!Main.dedServ) {
                            FishSlimeVFX.GelBurst(Projectile.Center, anchorOffset, 2, 2.4f);
                        }
                    }
                }
                else {
                    //目标死亡,返回漂浮状态
                    State = GelState.Floating;
                    AttachedTargetID = -1;
                    trackingTargetID = -1; //清除追踪
                }
            }
            else {
                //无效目标,返回漂浮状态
                State = GelState.Floating;
                AttachedTargetID = -1;
                trackingTargetID = -1;
            }
        }

        //爆炸 tick
        private void ExplodingPhaseAI() {
            //停止移动
            Projectile.velocity *= 0.9f;

            //内压上升，震荡越来越急
            wobblePhase += 0.3f * ChargeT;
            wobbleAmp = Math.Max(wobbleAmp, 0.1f + ChargeT * 0.18f);

            //表面渗出小滴，将破未破
            if (!Main.dedServ && Main.rand.NextBool(6)) {
                Vector2 rim = Main.rand.NextVector2Unit() * BlobRadius * 0.8f;
                FishSlimeVFX.Droplet(Projectile.Center + rim, rim * 0.03f + new Vector2(0f, -0.5f)
                    , Main.rand.NextFloat(0.4f, 0.6f));
            }
        }

        //检测附着目标
        private void CheckAttachment() {
            if (State != GelState.Floating) return;

            //优先尝试附着到追踪目标
            if (trackingTargetID >= 0 && trackingTargetID < Main.maxNPCs) {
                NPC trackTarget = Main.npc[trackingTargetID];
                if (trackTarget.active) {
                    float distToTrack = Vector2.Distance(Projectile.Center, trackTarget.Center);
                    if (distToTrack < AttachRange) {
                        AttachToTarget(trackTarget);
                        return;
                    }
                }
            }

            //寻找最近的敌人
            NPC closestNPC = null;
            float closestDist = AttachRange;

            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npc = Main.npc[i];
                if (npc.active && npc.CanBeChasedBy() && !npc.friendly) {
                    float dist = Vector2.Distance(Projectile.Center, npc.Center);
                    if (dist < closestDist) {
                        closestDist = dist;
                        closestNPC = npc;
                    }
                }
            }

            if (closestNPC != null) {
                AttachToTarget(closestNPC);
            }
        }

        //附着到目标
        private void AttachToTarget(NPC target) {
            State = GelState.Attached;
            AttachedTargetID = target.whoAmI;
            Projectile.tileCollide = false;
            trackingTargetID = -1; //清除追踪
            lastTargetVel = target.velocity;

            //体表锚点，入射方向一侧的种子偏移点
            Vector2 inDir = (Projectile.Center - target.Center).SafeNormalize(Main.rand.NextVector2Unit());
            anchorOffset = inDir.RotatedByRandom(0.7f) * new Vector2(target.width, target.height) * 0.34f;

            //附着音效
            SoundEngine.PlaySound(SoundID.NPCHit1 with {
                Volume = 0.6f,
                Pitch = 0.4f
            }, Projectile.Center);

            //拍击上靶，压成饼 + 接触点溅珠环
            ExciteWobble(0.5f, anchorOffset);
            if (!Main.dedServ) {
                FishSlimeVFX.GelBurst(target.Center + anchorOffset, anchorOffset, 6, 4f);
            }
            //发射拉丝若还挂着，此刻绷断
            SnapLaunchStrand();
        }

        //更新连接关系
        private void UpdateConnections() {
            ConnectedGels.Clear();

            //查找附近的凝胶球
            for (int i = 0; i < Main.maxProjectiles; i++) {
                if (i == Projectile.whoAmI) continue;

                Projectile other = Main.projectile[i];
                if (!other.active || other.type != Projectile.type) continue;

                float dist = Vector2.Distance(Projectile.Center, other.Center);
                if (dist < ConnectionRange) {
                    ConnectedGels.Add(i);

                    //连接拉力
                    if (State == GelState.Floating) {
                        Vector2 toOther = other.Center - Projectile.Center;
                        float pullStrength = (1f - dist / ConnectionRange) * 0.05f;
                        Projectile.velocity += toOther.SafeNormalize(Vector2.Zero) * pullStrength;
                    }
                }
            }

            //绷断检测
            if (!Main.dedServ) {
                foreach (int id in prevConnected) {
                    if (ConnectedGels.Contains(id) || Projectile.whoAmI > id) continue;
                    Projectile other = Main.projectile[id];
                    if (!other.active || other.type != Projectile.type) continue;
                    float dist = Vector2.Distance(Projectile.Center, other.Center);
                    if (dist < ConnectionRange * 1.4f) {
                        FishSlimeVFX.SpawnStrandSnap(Projectile.Center, other.Center, 0.25f);
                    }
                }
            }
            prevConnected.Clear();
            foreach (int id in ConnectedGels) {
                prevConnected.Add(id);
            }
        }

        //发射拉丝，拉长到极限或状态切换时绷断
        private void UpdateLaunchStrand() {
            if (!launchStrandAlive) {
                return;
            }
            Player owner = Main.player[Projectile.owner];
            if (!owner.active || owner.dead) {
                launchStrandAlive = false;
                return;
            }
            float dist = Vector2.Distance(owner.Center, Projectile.Center);
            if (dist > LaunchStrandSnapLen || GelLife > 30f || State != GelState.Floating) {
                SnapLaunchStrand();
            }
        }

        private void SnapLaunchStrand() {
            if (!launchStrandAlive) {
                return;
            }
            launchStrandAlive = false;
            Player owner = Main.player[Projectile.owner];
            if (!Main.dedServ && owner.active) {
                float dist = Vector2.Distance(owner.Center, Projectile.Center);
                FishSlimeVFX.SpawnStrandSnap(owner.Center, Projectile.Center
                    , 1f - MathHelper.Clamp(dist / LaunchStrandSnapLen, 0f, 1f));
                //断丝的回弹传进球体
                ExciteWobble(0.2f, Projectile.Center - owner.Center);
            }
        }

        //冲击注入震荡能量
        private void ExciteWobble(float amp, Vector2 axis) {
            if (amp <= wobbleAmp * 0.6f) {
                return; //弱冲击不打断强震荡
            }
            wobbleAmp = Math.Min(amp, 0.55f);
            wobblePhase = 0f;
            if (axis.LengthSquared() > 0.01f && State != GelState.Attached) {
                //形变主轴取冲击方向的垂直，砸地=横宽竖扁，撞墙=竖长横扁
                deformAngle = axis.ToRotation() + MathHelper.PiOver2;
            }
        }

        //更新果冻物理，主轴对齐
        private void UpdateJellyPhysics() {
            //速度突变自动激发（追踪急转、连接拉拽）
            Vector2 dv = Projectile.velocity - lastVelocity;
            float impact = dv.Length();
            if (impact > 1.2f && State != GelState.Attached) {
                ExciteWobble(Math.Min(impact * 0.05f, 0.5f), dv);
            }

            float speed = Projectile.velocity.Length();
            if (State == GelState.Attached) {
                //贴附
                deformAngle = deformAngle.AngleLerp(anchorOffset.ToRotation() + MathHelper.PiOver2, 0.2f);
            }
            else if (speed > 3f) {
                //飞行，主轴缓慢回贴速度方向
                deformAngle = deformAngle.AngleLerp(Projectile.velocity.ToRotation(), 0.14f);
            }

            //基础形变量，速度拉伸 / 附着压饼
            float baseStretch = State == GelState.Floating ? Math.Min(speed * 0.02f, 0.3f) : 0f;
            float attachFlat = State == GelState.Attached ? 0.22f : 0f;

            //阻尼震荡，主轴与垂轴反相
            wobblePhase += 0.52f;
            wobbleAmp *= 0.915f;
            float w = wobbleAmp * MathF.Cos(wobblePhase);

            //待机呼吸，活物的静默签名
            float breath = MathF.Sin(GelLife * 0.085f + Projectile.identity % 10 * 0.7f) * 0.022f;

            stretchAxis = 1f + baseStretch + attachFlat + w + breath;
            squashAxis = 1f - baseStretch * 0.62f - attachFlat * 0.6f - w - breath;
            lastVelocity = Projectile.velocity;
        }

        //内部高光方向
        private void UpdateHighlight() {
            Vector2 hlTarget = new(-0.45f, -0.55f);
            if (trackingTargetID >= 0 && trackingTargetID < Main.maxNPCs) {
                NPC target = Main.npc[trackingTargetID];
                if (target.active) {
                    Vector2 toTarget = (target.Center - Projectile.Center).SafeNormalize(Vector2.Zero);
                    hlTarget = Vector2.Lerp(hlTarget, toTarget, 0.55f);
                }
            }
            highlightDir = Vector2.Lerp(highlightDir, hlTarget.SafeNormalize(new Vector2(-0.7f, -0.7f)), 0.1f);
        }

        //碰撞分支
        public override bool OnTileCollide(Vector2 oldVelocity) {
            //增加弹跳计数
            bounceCount++;

            //果冻弹跳效果
            if (Math.Abs(Projectile.velocity.X - oldVelocity.X) > float.Epsilon) {
                Projectile.velocity.X = -oldVelocity.X * Bounce;

                //弹跳时如果有追踪目标，调整X方向朝向目标
                if (trackingTargetID >= 0 && trackingTargetID < Main.maxNPCs) {
                    NPC target = Main.npc[trackingTargetID];
                    if (target.active) {
                        float toTargetX = target.Center.X - Projectile.Center.X;
                        if (Math.Sign(toTargetX) != Math.Sign(Projectile.velocity.X)) {
                            //反转X速度方向使其朝向目标
                            Projectile.velocity.X = -Projectile.velocity.X * 0.8f;
                        }
                    }
                }
            }
            if (Math.Abs(Projectile.velocity.Y - oldVelocity.Y) > float.Epsilon) {
                Projectile.velocity.Y = -oldVelocity.Y * Bounce;

                //弹跳时如果有追踪目标，给一点朝向目标的水平速度
                if (trackingTargetID >= 0 && trackingTargetID < Main.maxNPCs) {
                    NPC target = Main.npc[trackingTargetID];
                    if (target.active) {
                        Vector2 toTarget = target.Center - Projectile.Center;
                        float horizontalBoost = Math.Sign(toTarget.X) * Math.Min(Math.Abs(toTarget.X) * 0.02f, 3f);
                        Projectile.velocity.X += horizontalBoost;
                    }
                }
            }

            //落地三拍
            float impactSpeed = oldVelocity.Length();
            ExciteWobble(MathHelper.Clamp(impactSpeed * 0.055f, 0.14f, 0.55f), oldVelocity - Projectile.velocity);
            if (!Main.dedServ) {
                int drops = impactSpeed > 7f ? 3 : 2;
                Vector2 reflectDir = (Projectile.velocity - oldVelocity).SafeNormalize(-Vector2.UnitY);
                for (int i = 0; i < drops; i++) {
                    FishSlimeVFX.Droplet(Projectile.Center + Main.rand.NextVector2Circular(8f, 8f)
                        , reflectDir.RotatedByRandom(0.9f) * Main.rand.NextFloat(1f, 2.6f)
                        , Main.rand.NextFloat(0.45f, 0.8f));
                }
                Dust d = Dust.NewDustPerfect(Projectile.Center + oldVelocity.SafeNormalize(Vector2.UnitY) * 12f
                    , DustID.TintableDust, Vector2.Zero, 130, FishSlimeVFX.GelBody, Main.rand.NextFloat(1f, 1.4f));
                d.noGravity = true;
            }

            if (Projectile.localAI[0] <= 1) {
                //弹跳音效，弹跳次数多时音调变化
                SoundEngine.PlaySound(SoundID.Item95 with {
                    Volume = 0.3f,
                    Pitch = 0.1f + Math.Min(bounceCount * 0.1f, 0.4f)
                }, Projectile.Center);
                Projectile.localAI[0] = 30;
            }

            return false;
        }

        //击中NPC
        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            //附着到目标
            if (State == GelState.Floating) {
                AttachToTarget(target);
            }

            //附加减速
            target.AddBuff(BuffID.Slimed, 240);
        }

        //死亡时爆炸
        public override void OnKill(int timeLeft) {
            CreateGelExplosion();

            //爆炸音效
            SoundEngine.PlaySound(SoundID.NPCDeath1 with {
                Volume = 0.7f,
                Pitch = 0.3f
            }, Projectile.Center);

            SoundEngine.PlaySound(SoundID.Item95 with {
                Volume = 0.5f,
                Pitch = -0.2f
            }, Projectile.Center);
        }

        //创建凝胶爆炸
        private void CreateGelExplosion() {
            //爆炸伤害范围
            float explosionRadius = 120f + HalibutData.GetDomainLayer() * 15f;

            //对范围内敌人造成伤害
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npc = Main.npc[i];
                if (npc.active && npc.CanBeChasedBy() && !npc.friendly) {
                    float dist = Vector2.Distance(Projectile.Center, npc.Center);
                    if (dist < explosionRadius) {
                        //距离越近伤害越高
                        float damageRatio = 1f - dist / explosionRadius;
                        int explosionDamage = (int)(Projectile.damage * (0.5f + damageRatio * 0.5f));

                        npc.SimpleStrikeNPC(explosionDamage, 0, false, 5f, null, false, 0f, true);
                        npc.AddBuff(BuffID.Slimed, 300);
                    }
                }
            }

            //爆裂演出
            FishSlimeVFX.GelPop(Projectile.Center, 1f + HalibutData.GetDomainLayer() * 0.08f);
        }

        //附着时整体压到宿主身后
        public override void DrawBehind(int index, List<int> behindNPCsAndTiles, List<int> behindNPCs
            , List<int> behindProjectiles, List<int> overPlayers, List<int> overWiresUI) {
            if (State == GelState.Attached) {
                behindNPCs.Add(index);
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            SpriteBatch sb = Main.spriteBatch;
            float time = Main.GlobalTimeWrappedHourly;

            //发射拉丝，随距离绷紧变细
            if (launchStrandAlive) {
                Player owner = Main.player[Projectile.owner];
                if (owner.active) {
                    float dist = Vector2.Distance(owner.Center, Projectile.Center);
                    float slack = 1f - MathHelper.Clamp(dist / LaunchStrandSnapLen, 0f, 1f);
                    FishSlimeVFX.DrawStrand(sb, owner.Center, Projectile.Center, slack, 0.85f, time, Seed);
                }
            }

            //凝胶连丝，小 id 方绘制避免双画
            foreach (int gelID in ConnectedGels) {
                if (gelID >= Main.maxProjectiles || Projectile.whoAmI > gelID) continue;
                Projectile other = Main.projectile[gelID];
                if (!other.active) continue;
                float dist = Vector2.Distance(Projectile.Center, other.Center);
                float slack = 1f - dist / ConnectionRange;
                float alpha = MathHelper.Clamp(slack * 2.2f, 0.15f, 0.75f);
                FishSlimeVFX.DrawStrand(sb, Projectile.Center, other.Center, slack, alpha, time, Seed + gelID * 0.31f);
            }

            //附着夹心底层
            //Extra_98 真 alpha 血滴形错相叠两张，读作从宿主背面渗出的凝胶团
            if (State == GelState.Attached) {
                Texture2D blob = CWRAsset.Extra_98?.Value;
                if (blob != null) {
                    Vector2 dp = Projectile.Center - Main.screenPosition;
                    float s = BlobRadius * 2.6f / blob.Width;
                    sb.Draw(blob, dp, null, FishSlimeVFX.GelDeep * 0.55f, deformAngle + 0.5f, blob.Size() * 0.5f
                        , s, SpriteEffects.None, 0);
                    sb.Draw(blob, dp, null, FishSlimeVFX.GelBody * 0.32f, deformAngle - 0.9f, blob.Size() * 0.5f
                        , s * 0.82f, SpriteEffects.None, 0);
                }
            }

            return false;
        }

        //blob 本体，实体层图元 quad
        void IPrimitiveDrawable.DrawPrimitives() {
            if (!Projectile.active) {
                return;
            }
            Effect fx = FishSlimeAssets.FishSlimeBlob;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            if (fx == null || noise == null) {
                DrawBlobFallback();
                return;
            }

            GraphicsDevice device = Main.instance.GraphicsDevice;
            float r = BlobRadius;
            Vector2 axis = deformAngle.ToRotationVector2();
            Vector2 perp = new(-axis.Y, axis.X);
            Vector2 c = Projectile.Center;
            Vector2 ea = axis * (r * 1.25f * stretchAxis);
            Vector2 eb = perp * (r * 1.25f * squashAxis);

            //高光方向转入形变局部系，随 quad 一起被压扁拉伸
            Vector2 hlLocal = new(Vector2.Dot(highlightDir, axis), Vector2.Dot(highlightDir, perp));

            //环境光轻染半透明体
            Color light = Lighting.GetColor(Projectile.Center.ToTileCoordinates());
            Color tint = Color.Lerp(Color.White, light, 0.4f);

            fx.Parameters["transformMatrix"]?.SetValue(VaultUtils.GetTransfromMatrix());
            fx.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            fx.Parameters["uSeed"]?.SetValue(Seed);
            fx.Parameters["uWobble"]?.SetValue(MathHelper.Clamp(wobbleAmp * 1.8f, 0f, 1f));
            fx.Parameters["uCharge"]?.SetValue(ChargeT);
            fx.Parameters["uAlpha"]?.SetValue(MathHelper.Clamp(GelLife / 8f, 0f, 1f));
            fx.Parameters["uHighlightDir"]?.SetValue(hlLocal);
            fx.Parameters["uColDeep"]?.SetValue(FishSlimeVFX.GelDeep.ToVector3());
            fx.Parameters["uColBody"]?.SetValue(FishSlimeVFX.GelBody.ToVector3());
            fx.Parameters["uColBright"]?.SetValue(FishSlimeVFX.GelBright.ToVector3());
            fx.Parameters["uNoiseTex"]?.SetValue(noise);

            BlendState prevBlend = device.BlendState;
            RasterizerState prevRaster = device.RasterizerState;
            DepthStencilState prevDepth = device.DepthStencilState;
            device.BlendState = BlendState.AlphaBlend;
            device.RasterizerState = RasterizerState.CullNone;
            device.DepthStencilState = DepthStencilState.None;

            VertexPositionColorTexture[] verts = new VertexPositionColorTexture[4];
            verts[0] = new VertexPositionColorTexture(new Vector3(c.X - ea.X - eb.X, c.Y - ea.Y - eb.Y, 0f), tint, new Vector2(0f, 0f));
            verts[1] = new VertexPositionColorTexture(new Vector3(c.X + ea.X - eb.X, c.Y + ea.Y - eb.Y, 0f), tint, new Vector2(1f, 0f));
            verts[2] = new VertexPositionColorTexture(new Vector3(c.X - ea.X + eb.X, c.Y - ea.Y + eb.Y, 0f), tint, new Vector2(0f, 1f));
            verts[3] = new VertexPositionColorTexture(new Vector3(c.X + ea.X + eb.X, c.Y + ea.Y + eb.Y, 0f), tint, new Vector2(1f, 1f));

            foreach (EffectPass pass in fx.CurrentTechnique.Passes) {
                pass.Apply();
                device.DrawUserPrimitives(PrimitiveType.TriangleStrip, verts, 0, 2);
            }

            device.BlendState = prevBlend;
            device.RasterizerState = prevRaster;
            device.DepthStencilState = prevDepth;
        }

        //shader 未就绪的降级
        private void DrawBlobFallback() {
            Texture2D blob = CWRAsset.Extra_98?.Value;
            if (blob == null) {
                return;
            }
            SpriteBatch sb = Main.spriteBatch;
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp
                , DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

            float r = BlobRadius;
            Vector2 dp = Projectile.Center - Main.screenPosition;
            Vector2 scaleVec = new Vector2(stretchAxis, squashAxis) * (r * 2.6f / blob.Width);
            Vector2 origin = blob.Size() * 0.5f;
            //两张错转的液滴形叠成有机块，暗底压轮廓、体色铺中层
            sb.Draw(blob, dp, null, FishSlimeVFX.GelDeep * 0.8f, deformAngle + 0.6f, origin, scaleVec * 1.12f, SpriteEffects.None, 0);
            sb.Draw(blob, dp, null, FishSlimeVFX.GelBody * 0.75f, deformAngle - 0.8f, origin, scaleVec * 0.95f, SpriteEffects.None, 0);
            sb.Draw(blob, dp + highlightDir * r * 0.4f, null, FishSlimeVFX.GelSheen * 0.7f, 0f
                , origin, scaleVec.Length() * 0.2f, SpriteEffects.None, 0);

            sb.End();
        }
    }
}
