using CalamityOverhaul.Common;
using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaDomains;
using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaServants.KikasaEye;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaServants.KikasaPrime
{
    /// <summary>
    /// 鬼奴·湖水版机械骷髅王，四械刑架。单弹幕内部模拟"头 + 四条工具臂"：
    /// 头位权威同步，四臂在各端按锚点 + 摆动相位做弹簧摆模拟（链条下垂有重量），
    /// 臂链用原版 Arm_Bone_2 骨节沿悬链弧分三节铺出。
    /// 出水演出为"四件工具先后破水举起（锯-钳-炮-镭射逐件就位），头最后升起点睛"；
    /// 攻击走工具轮换：锯突刺撕扯 → 炮迫击血雷 → 钳突刺夹合 → 镭射双发短脉冲，
    /// 同一时刻只有一件工具主攻。溶解与出场呼应：工具逐件熄火坠湖、头最后沉没。
    /// 联机同基准契约：转场规则确定性、owner 盖 netUpdate 章，节拍闩防快照回卷，
    /// 生命线只有 owner 判，子弹幕只在 owner 端生成
    /// </summary>
    internal class KikasaPrimeServant : ModProjectile, IKikasaServant
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        //==================== 可调基数（占位初值，验收再调）====================

        /// <summary>臂击/接触基伤（召唤加成前）</summary>
        internal const int ArmDamage = 640;

        /// <summary>炮弹与激光基伤（召唤加成前），由子弹幕消费</summary>
        internal const int ShellDamage = 350;

        //==================== 状态 ====================

        private const int StateEmerge = 0;
        private const int StateFollow = 1;
        private const int StateSawDart = 2;
        private const int StateViceDart = 3;
        private const int StateMortar = 4;
        private const int StateLaser = 5;
        private const int StateDissolve = 6;

        private int State { get => (int)Projectile.ai[0]; set => Projectile.ai[0] = value; }
        private ref float StateTimer => ref Projectile.ai[1];
        /// <summary>状态内子相位号（出水/溶解不用，攻击态为阶段序号）</summary>
        private ref float StateParam => ref Projectile.ai[2];

        //==================== 臂编制 ====================

        private const int ArmCount = 4;
        private const int ArmSaw = 0;
        private const int ArmVice = 1;
        private const int ArmCannon = 2;
        private const int ArmLaser = 3;

        /// <summary>悬挂队形：锯/钳贴身低垂，炮/镭射外侧高挂，四械刑架</summary>
        private static readonly Vector2[] RestOffset = {
            new(-66f, 96f),
            new(66f, 96f),
            new(-160f, 22f),
            new(160f, 22f),
        };

        private static int ArmNpcType(int i) => i switch {
            ArmSaw => NPCID.PrimeSaw,
            ArmVice => NPCID.PrimeVice,
            ArmCannon => NPCID.PrimeCannon,
            _ => NPCID.PrimeLaser,
        };

        //==================== 时序 ====================

        //出水：涟漪预兆→四件工具按拍破水举起→静默→头破水升起→觉醒扫光
        private const int OmenFrames = 18;
        private const int ToolGap = 24;
        private const int HeadBreachFrame = 122;
        private const int AwakenFrame = 162;
        private const int EmergeTotal = 186;

        //突刺：蓄力回缩 tell→一帧弹出→撕扯/夹合→咔哒收回
        private const int DartWindup = 26;
        private const int DartExtendFrames = 10;
        private const int SawGrindFrames = 20;
        private const int ViceClampFrames = 12;
        private const int DartRetractFrames = 18;
        private const float DartLaunchSpeed = 46f;
        private const float DartMaxReach = 470f;

        //迫击：炮臂就位→三发点射不同落点→泄压回摆
        private const int MortarPoseFrames = 20;
        private const int MortarShotGap = 16;
        private const int MortarShots = 3;
        private const int MortarVolleyEnd = 4 + MortarShotGap * MortarShots;
        private const int MortarRecoverFrames = 20;

        //镭射：滑步→双发短脉冲→反向滑步→再双发→收势
        private const int LaserStrafeFrames = 12;
        private const int LaserVolleyFrames = 16;
        private const int LaserRecoverFrames = 14;

        //溶解：工具逐件熄火坠湖，头最后沉没
        private const int ToolDropStart = 8;
        private const int ToolDropGap = 14;
        private const int HeadSinkFrame = 64;
        private const int DissolveTotal = 118;

        //==================== 臂模拟数据（各端本地重建，头位由同步纠偏）====================

        private readonly Vector2[] armPos = new Vector2[ArmCount];
        private readonly Vector2[] armVel = new Vector2[ArmCount];
        /// <summary>工具旋转（贴图约定：rotation=0 工具口朝下，瞄准 = 方向角 - PiOver2）</summary>
        private readonly float[] armRot = new float[ArmCount];
        /// <summary>工具湿度：过水线拉满、出水后淌干，驱动滴落与材质血水度</summary>
        private readonly float[] wetness = new float[ArmCount];
        private readonly bool[] belowWater = new bool[ArmCount];
        private bool armsInit;
        private Vector2 headSim;

        //==================== 本地表现量（不入同步，节拍闩防快照回卷）====================

        private int headFrameTick;
        private int headFrameIndex;
        private int sawFrameTick;
        private int sawFrameIndex;
        private int attackCooldown;
        private int attackIndex;
        private int lastSeenState = -1;
        /// <summary>状态内已入过的最高相位（单调闩，快照回卷不重播入场拍）</summary>
        private int phaseReached = -1;
        private int emergeToolRevealed = -1;
        private bool emergeBreachDone;
        private bool emergeAwakened;
        private int lastShellFired = -1;
        private int lastPulseFired = -1;
        private int dissolveToolDropped = -1;
        private readonly bool[] dissolveToolSplashed = new bool[ArmCount];
        private bool dissolveHeadSplashed;
        /// <summary>突刺链条绷直颤动余帧</summary>
        private int dartTautVibe;
        private bool dartReachThunked;
        private Vector2 dartAim = -Vector2.UnitY;
        /// <summary>本次突刺的目标伸展长度：近敌短出手，别越过目标去磨空气</summary>
        private float dartReach = DartMaxReach;
        /// <summary>炮口余温计时，绘制层热光衰减用</summary>
        private int cannonHeat;

        //==================== 配色（血系随观看域冷化，机械点缀色只做次要层）====================

        private static Color BloodMain => KikasaDomain.CoolTint(new(237, 77, 69), new(126, 158, 164));
        private static Color BloodDeep => KikasaDomain.CoolTint(new(140, 32, 30), new(84, 104, 110));
        private static Color MistBlood => KikasaDomain.CoolTint(new(58, 18, 20), new(52, 62, 66));
        private static Color FoamGlow => KikasaDomain.CoolTint(new(246, 133, 112), new(176, 200, 204));
        /// <summary>金属研磨火星：机械身份的次要点缀层</summary>
        private static readonly Color SparkHot = new(255, 168, 92);

        private Player Owner => Main.player[Projectile.owner];

        /// <summary>连续量抖动的确定性相位，各端一致（不掷 Main.rand）</summary>
        private float Seed => Projectile.identity * 0.7391f;

        //==================== 召唤入口 ====================

        /// <summary>KikasaServantIndex 登记的召唤委托；emergeAt.Y = 湖面（头的破水点）</summary>
        internal static void Summon(Player owner, Vector2 emergeAt) {
            if (owner.whoAmI != Main.myPlayer) {
                return;
            }
            int damage = (int)owner.GetTotalDamage(DamageClass.Summon).ApplyTo(ArmDamage);
            Projectile.NewProjectile(owner.GetSource_Misc("KikasaServant"),
                emergeAt + new Vector2(0f, 96f), Vector2.Zero,
                ModContent.ProjectileType<KikasaPrimeServant>(), damage, 8f, owner.whoAmI);
        }

        //==================== 定义 ====================

        public override void SetStaticDefaults() {
            Main.projPet[Projectile.type] = true;
            ProjectileID.Sets.MinionSacrificable[Projectile.type] = false;
            //臂链伸出去远超 hitbox，头出屏也要画
            ProjectileID.Sets.DrawScreenCheckFluff[Projectile.type] = 1000;
        }

        public override void SetDefaults() {
            Projectile.width = 84;
            Projectile.height = 84;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Summon;
            Projectile.minion = true;
            Projectile.minionSlots = 0f;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.netImportant = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 20;
            Projectile.timeLeft = 180;
        }

        public override bool MinionContactDamage() => true;

        /// <summary>接触伤害只开在突刺的弹出与撕扯/夹合窗，与可见的臂击严格对齐</summary>
        public override bool? CanDamage() {
            if (State != StateSawDart && State != StateViceDart) {
                return false;
            }
            int phase = (int)StateParam;
            return phase == 1 || phase == 2 ? null : false;
        }

        /// <summary>臂击命中：只查正在突刺那条臂的肩→工具线段</summary>
        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            if (!armsInit) {
                return false;
            }
            int arm = State == StateSawDart ? ArmSaw : State == StateViceDart ? ArmVice : -1;
            if (arm < 0) {
                return false;
            }
            float _ = 0f;
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(),
                ShoulderWorld(arm), armPos[arm], 42f, ref _);
        }

        public override bool? CanCutTiles() => false;

        //==================== 遣返 ====================

        public bool IsDismissing => State == StateDissolve;

        public void BeginDismiss() {
            if (Main.myPlayer == Projectile.owner && State != StateDissolve) {
                BeginDissolve();
            }
        }

        private void BeginDissolve() {
            //第一件工具都还没破水就要收场：什么都没露出来，不演谢幕
            if (State == StateEmerge && StateTimer < OmenFrames) {
                Projectile.Kill();
                return;
            }
            State = StateDissolve;
            StateTimer = 0;
            StateParam = 0;
            Projectile.netUpdate = Main.myPlayer == Projectile.owner;
        }

        //==================== 推进 ====================

        public override void AI() {
            Player owner = Owner;
            if (owner == null || !owner.active) {
                Projectile.Kill();
                return;
            }
            bool authority = Main.myPlayer == Projectile.owner;
            KikasaDomainPlayer domain = owner.GetModPlayer<KikasaDomainPlayer>();

            //生命线：湖塌/收域/主人死亡 → 溶解回湖。只有 owner 裁决
            //服务器无领域状态（恒 Closed 是既定契约），别处判会当场误杀；
            //其余端只跟 owner 的同步包换场
            if (authority && State != StateDissolve && !LakeHealthy(owner, domain)) {
                BeginDissolve();
            }

            Projectile.timeLeft = 180;
            Projectile.damage = (int)owner.GetTotalDamage(DamageClass.Summon).ApplyTo(ArmDamage);

            //换场清闩：远端可能靠收包换场而非本地同拍转场，残闩会吞掉新场节拍
            if (State != lastSeenState) {
                lastSeenState = State;
                phaseReached = -1;
                lastShellFired = -1;
                lastPulseFired = -1;
                dartTautVibe = 0;
                dartReachThunked = false;
                //进入任何战斗态即视为全件已亮相（迟入场的客户端没经历出水，别把工具画没了）
                if (State >= StateFollow && State <= StateLaser) {
                    emergeToolRevealed = ArmCount - 1;
                    emergeBreachDone = true;
                }
                if (State == StateDissolve) {
                    dissolveToolDropped = -1;
                    Array.Clear(dissolveToolSplashed, 0, ArmCount);
                    dissolveHeadSplashed = false;
                }
            }

            StateTimer++;
            switch (State) {
                case StateEmerge: UpdateEmerge(domain); break;
                case StateFollow: UpdateFollow(owner, authority); break;
                case StateSawDart:
                case StateViceDart: UpdateDart(owner, domain, authority); break;
                case StateMortar: UpdateMortar(owner, authority); break;
                case StateLaser: UpdateLaser(owner, authority); break;
                case StateDissolve: UpdateDissolve(domain, authority); break;
            }

            UpdateArms(domain);
            UpdateFrames();
            UpdateJointDrips(domain);
            if (attackCooldown > 0) {
                attackCooldown--;
            }
            if (cannonHeat > 0) {
                cannonHeat--;
            }
            if (dartTautVibe > 0) {
                dartTautVibe--;
            }

            float glow = HeadAlpha() * 0.5f;
            if (glow > 0.02f) {
                Lighting.AddLight(Projectile.Center, 0.4f * glow, 0.1f * glow, 0.08f * glow);
                for (int i = 0; i < ArmCount; i++) {
                    if (ToolAlpha(i) > 0.3f) {
                        Lighting.AddLight(armPos[i], 0.18f, 0.05f, 0.04f);
                    }
                }
            }
        }

        private static bool LakeHealthy(Player owner, KikasaDomainPlayer domain)
            => !owner.dead && domain.AnyActive
            && domain.Phase != KikasaDomainPhase.Closing
            && domain.RiseT >= 0.9f;

        //==================== 出水：四械刑架逐件亮相 ====================

        private int ToolBeat(int i) => OmenFrames + i * ToolGap;

        /// <summary>出水期工具的举起持位点：破水点两侧、水面之上一截</summary>
        private Vector2 ToolHoldPoint(int i, float lakeY)
            => new(Projectile.Center.X + RestOffset[i].X * 0.85f, lakeY - 44f);

        private void UpdateEmerge(KikasaDomainPlayer domain) {
            float lakeY = domain.LakeWorldY;
            int t = (int)StateTimer;
            bool viewed = ViewedOwner;
            //破水前钉在水下待命；起速后只走指数衰减，别叠双重阻尼把升程吃掉
            if (!emergeBreachDone) {
                Projectile.velocity *= 0.9f;
            }

            //每件工具破水前 6 帧：落点处涟漪收拢预兆
            if (viewed) {
                for (int i = 0; i < ArmCount; i++) {
                    int beat = ToolBeat(i);
                    if (t >= beat - 6 && t < beat && (beat - t) % 3 == 1) {
                        KikasaDomainDeco.RippleAt(
                            new Vector2(ToolHoldPoint(i, lakeY).X, lakeY), 0.45f);
                    }
                }
                if (t == 3 || t == 11) {
                    SoundEngine.PlaySound(SoundID.Drip with {
                        Volume = 0.45f,
                        Pitch = t == 3 ? -0.5f : -0.2f,
                        MaxInstances = 2
                    }, new Vector2(Projectile.Center.X, lakeY));
                }
            }

            //工具破水拍（单调闩：快照回卷不重播）
            for (int i = 0; i < ArmCount; i++) {
                if (t >= ToolBeat(i) && emergeToolRevealed < i) {
                    emergeToolRevealed = i;
                    ToolRevealBeat(i, lakeY, viewed);
                }
            }

            //头破水：中间压轴，浪冠量级盖过四件工具
            if (t >= HeadBreachFrame && !emergeBreachDone) {
                emergeBreachDone = true;
                Projectile.velocity = new Vector2(0f, -12.5f);
                SoundEngine.PlaySound(SoundID.Roar with { Volume = 0.6f, Pitch = -0.7f, MaxInstances = 2 }, Projectile.Center);
                if (viewed) {
                    HeadBreachBurst(new Vector2(Projectile.Center.X, lakeY));
                }
            }
            if (emergeBreachDone) {
                //升起：一帧起速后指数衰减，禁匀速
                Projectile.velocity.Y *= 0.945f;
                Projectile.velocity.X = 0f;
            }

            //觉醒拍：目镜扫光亮起，四臂绷正列队
            if (t >= AwakenFrame && !emergeAwakened) {
                emergeAwakened = true;
                SoundEngine.PlaySound(SoundID.Item37 with { Volume = 0.6f, Pitch = -0.1f, MaxInstances = 2 }, Projectile.Center);
                SoundEngine.PlaySound(SoundID.MaxMana with { Volume = 0.5f, Pitch = -0.6f, MaxInstances = 2 }, Projectile.Center);
                for (int i = 0; i < ArmCount; i++) {
                    armVel[i] += (RestTarget(i) - armPos[i]) * 0.16f + new Vector2(0f, -1.6f);
                }
                if (viewed) {
                    ShakeViewer(1.8f);
                }
            }

            //升起期身上血水成帘往下淌
            if (!Main.dedServ && emergeBreachDone && t < AwakenFrame && t % 2 == 0) {
                PRTLoader.NewParticle<PRT_GhostRainDrop>(
                    Projectile.Center + new Vector2(Main.rand.NextFloat(-38f, 38f), Main.rand.NextFloat(0f, 30f)),
                    new Vector2(Main.rand.NextFloat(-0.4f, 0.4f), Main.rand.NextFloat(2.4f, 3.8f)),
                    BloodMain * Main.rand.NextFloat(0.4f, 0.6f),
                    Main.rand.NextFloat(0.45f, 0.7f))?.Configure(Main.rand.Next(14, 26), 0f);
            }

            if (t >= EmergeTotal) {
                //转场确定性（纯计时），owner 盖章纠偏
                State = StateFollow;
                StateTimer = 0;
                StateParam = 0;
                attackCooldown = 40;
                Projectile.netUpdate = Main.myPlayer == Projectile.owner;
            }
        }

        /// <summary>单件工具的破水就位拍：水花 + 就位清脆机械音，音高逐件抬升</summary>
        private void ToolRevealBeat(int i, float lakeY, bool viewed) {
            Vector2 spot = new(ToolHoldPoint(i, lakeY).X, lakeY);
            wetness[i] = 1f;
            SoundEngine.PlaySound(SoundID.SplashWeak with { Volume = 0.65f, Pitch = -0.35f, MaxInstances = 3 }, spot);
            SoundEngine.PlaySound(SoundID.Item37 with {
                Volume = 0.62f,
                Pitch = -0.45f + i * 0.16f,
                MaxInstances = 3
            }, spot);
            //锯的就位附一声短促起转
            if (i == ArmSaw) {
                SoundEngine.PlaySound(SoundID.Item22 with { Volume = 0.4f, Pitch = -0.3f, MaxInstances = 2 }, spot);
            }
            if (!viewed) {
                return;
            }
            KikasaDomainDeco.RippleAt(spot, 1.25f);
            KikasaDomainDeco.SplashAt(spot, 8);
            for (int k = 0; k < 8; k++) {
                PRTLoader.NewParticle<PRT_KikasaBloodGlob>(
                    spot + new Vector2(Main.rand.NextFloat(-16f, 16f), -4f),
                    new Vector2(Main.rand.NextFloat(-1.6f, 1.6f), -Main.rand.NextFloat(3f, 6.5f)),
                    Main.rand.NextBool(3) ? BloodDeep : BloodMain,
                    Main.rand.NextFloat(0.4f, 0.7f))?.Configure(Main.rand.Next(18, 30));
            }
            ShakeViewer(0.9f);
        }

        /// <summary>头压轴破水的浪冠：大环涟漪 + 扇形血珠 + 血雾，四件工具只是前菜</summary>
        private void HeadBreachBurst(Vector2 hit) {
            KikasaDomainDeco.RippleAt(hit, 2.6f);
            KikasaDomainDeco.RippleAt(hit + new Vector2(44f, 0f), 1.1f);
            KikasaDomainDeco.RippleAt(hit - new Vector2(40f, 0f), 1.0f);
            KikasaDomainDeco.SplashAt(hit + new Vector2(-16f, 0f), 13);
            KikasaDomainDeco.SplashAt(hit + new Vector2(16f, 0f), 13);

            for (int i = 0; i < 26; i++) {
                float angle = -MathHelper.Pi * (0.12f + 0.76f * i / 25f);
                PRTLoader.NewParticle<PRT_KikasaBloodGlob>(
                    hit + new Vector2(Main.rand.NextFloat(-30f, 30f), -4f),
                    angle.ToRotationVector2() * Main.rand.NextFloat(3.4f, 8f),
                    Main.rand.NextBool(3) ? BloodDeep : BloodMain,
                    Main.rand.NextFloat(0.5f, 0.9f))?.Configure(Main.rand.Next(22, 38));
            }
            for (int i = 0; i < 9; i++) {
                PRTLoader.NewParticle<PRT_KikasaBloodGlob>(
                    hit + new Vector2(Main.rand.NextFloat(-9f, 9f), -6f),
                    new Vector2(Main.rand.NextFloat(-0.9f, 0.9f), -Main.rand.NextFloat(8.5f, 13.5f)),
                    BloodMain * 0.9f, Main.rand.NextFloat(0.55f, 0.95f))?.Configure(Main.rand.Next(34, 52));
            }
            for (int i = 0; i < 3; i++) {
                PRTLoader.NewParticle<PRT_GhostRainMist>(
                    hit + new Vector2(Main.rand.NextFloat(-34f, 34f), -10f),
                    new Vector2(Main.rand.NextFloat(-0.3f, 0.3f), -Main.rand.NextFloat(0.35f, 0.8f)),
                    MistBlood * 0.85f, Main.rand.NextFloat(0.75f, 1.05f))?.Configure(Main.rand.Next(64, 100));
            }
            PRTLoader.NewParticle<PRT_DWave>(hit, Vector2.Zero, BloodDeep, 0.09f)
                ?.Configure(new Vector2(0.5f, 1f), -MathHelper.PiOver2, 0.36f, 11);

            SoundEngine.PlaySound(SoundID.SplashWeak with { Volume = 1f, Pitch = -0.4f, MaxInstances = 2 }, hit);
            SoundEngine.PlaySound(SoundID.DD2_MonkStaffGroundImpact with { Volume = 0.55f, Pitch = -0.7f, MaxInstances = 1 }, hit);
            ShakeViewer(5.5f);
        }

        //==================== 跟随 ====================

        private void UpdateFollow(Player owner, bool authority) {
            //中枢缓浮在主人侧上方，呼吸浮动
            Vector2 anchor = owner.Center + new Vector2(-owner.direction * 96f, -150f);
            anchor.Y += MathF.Sin(Main.GlobalTimeWrappedHourly * 1.7f + Seed) * 7f;
            anchor.X += MathF.Sin(Main.GlobalTimeWrappedHourly * 1.1f + Seed * 2f) * 5f;

            Vector2 to = anchor - Projectile.Center;
            if (to.Length() > 2400f) {
                //跟丢硬贴回，别在半个地图外晃链条
                Projectile.Center = anchor;
                Projectile.velocity = Vector2.Zero;
                RebuildArms(anchor);
                Projectile.netUpdate = authority;
                return;
            }
            Vector2 desired = to * 0.07f;
            const float maxSpeed = 15f;
            if (desired.Length() > maxSpeed) {
                desired = desired.SafeNormalize(Vector2.Zero) * maxSpeed;
            }
            Projectile.velocity = Vector2.Lerp(Projectile.velocity, desired, 0.11f);

            //出手裁决：工具轮换（锯→炮→钳→镭射），规则各端一致，owner 盖章
            int target = FindTarget(owner);
            if (target >= 0 && attackCooldown <= 0 && StateTimer > 30) {
                attackIndex++;
                State = (attackIndex % 4) switch {
                    1 => StateSawDart,
                    2 => StateMortar,
                    3 => StateViceDart,
                    _ => StateLaser,
                };
                StateTimer = 0;
                StateParam = 0;
                Projectile.netUpdate = authority;
            }
        }

        private void EndAttack(bool authority, int cooldown) {
            State = StateFollow;
            StateTimer = 0;
            StateParam = 0;
            attackCooldown = cooldown;
            Projectile.netUpdate = authority;
        }

        private void NextPhase(int next, bool authority) {
            StateParam = next;
            StateTimer = 0;
            Projectile.netUpdate = authority;
        }

        /// <summary>相位入场拍（单调闩）：返回 true 表示本端第一次踏进该相位</summary>
        private bool EnterPhaseOnce() {
            int p = (int)StateParam;
            if (p <= phaseReached) {
                return false;
            }
            phaseReached = p;
            return true;
        }

        //==================== 锯/钳伸缩臂突刺 ====================

        private void UpdateDart(Player owner, KikasaDomainPlayer domain, bool authority) {
            int arm = State == StateSawDart ? ArmSaw : ArmVice;
            int phase = (int)StateParam;
            int t = (int)StateTimer;
            int target = FindTarget(owner);
            //中枢突刺期持位微刹，臂是主角
            Projectile.velocity *= 0.92f;

            if (phase == 0) {
                //蓄力回缩 tell：臂链收着劲，工具口咬向目标
                if (EnterPhaseOnce()) {
                    ToolStartupHiss(arm);
                }
                if (target < 0) {
                    EndAttack(authority, 45);
                    return;
                }
                Vector2 aimPos = Main.npc[target].Center + Main.npc[target].velocity * 8f;
                dartAim = (aimPos - armPos[arm]).SafeNormalize(Vector2.UnitY);

                //蓄势收拢血珠，72% 后静默，弹出前的吸气
                if (!Main.dedServ && t < DartWindup * 0.72f && t % 3 == 1) {
                    Vector2 from = armPos[arm] + Main.rand.NextVector2Unit() * Main.rand.NextFloat(40f, 84f);
                    PRTLoader.NewParticle<PRT_GhostRainDrop>(from, (armPos[arm] - from) * 0.16f,
                        BloodMain * 0.5f, Main.rand.NextFloat(0.3f, 0.5f))?.Configure(8, 0f);
                }
                //锯提前起转
                if (arm == ArmSaw && t == 6) {
                    SoundEngine.PlaySound(SoundID.Item22 with { Volume = 0.5f, Pitch = -0.15f, MaxInstances = 2 }, armPos[arm]);
                }
                if (t >= DartWindup) {
                    NextPhase(1, authority);
                }
                return;
            }

            if (phase == 1) {
                //一帧弹出：臂链甩向目标数倍臂长
                if (EnterPhaseOnce()) {
                    dartReach = DartMaxReach;
                    if (target >= 0) {
                        Vector2 aimPos = Main.npc[target].Center + Main.npc[target].velocity * 6f;
                        dartAim = (aimPos - armPos[arm]).SafeNormalize(Vector2.UnitY);
                        //链长按敌距收口：工具口正好啃在目标身上，不越过去磨空气
                        dartReach = MathHelper.Clamp(
                            Vector2.Distance(aimPos, ShoulderWorld(arm)) + 26f, 150f, DartMaxReach);
                    }
                    armVel[arm] = dartAim * DartLaunchSpeed;
                    dartReachThunked = false;
                    SoundEngine.PlaySound(SoundID.DD2_WyvernDiveDown with { Volume = 0.6f, Pitch = 0.1f, MaxInstances = 3 }, armPos[arm]);
                    SoundEngine.PlaySound(SoundID.Item37 with { Volume = 0.5f, Pitch = 0.2f, MaxInstances = 3 }, armPos[arm]);
                    if (ViewedOwner) {
                        ShakeViewer(2.5f);
                    }
                }
                if (t >= DartExtendFrames) {
                    NextPhase(2, authority);
                }
                return;
            }

            if (phase == 2) {
                //撕扯/夹合窗
                if (EnterPhaseOnce()) {
                    dartTautVibe = 12;
                    if (arm == ArmVice) {
                        //钳一次夹合：闷重的液压咬合
                        SoundEngine.PlaySound(SoundID.Item37 with { Volume = 0.85f, Pitch = -0.55f, MaxInstances = 2 }, armPos[arm]);
                        SoundEngine.PlaySound(SoundID.NPCHit4 with { Volume = 0.6f, Pitch = -0.35f, MaxInstances = 2 }, armPos[arm]);
                        if (ViewedOwner) {
                            ShakeViewer(2f);
                        }
                    }
                }
                //锯撕扯的研磨声与火星
                if (arm == ArmSaw) {
                    if (t % 8 == 2) {
                        SoundEngine.PlaySound(SoundID.Item22 with { Volume = 0.42f, Pitch = 0.25f, MaxInstances = 2 }, armPos[arm]);
                    }
                    if (!Main.dedServ && t % 2 == 0) {
                        PRTLoader.NewParticle<PRT_Spark>(
                            armPos[arm] + Main.rand.NextVector2Circular(16f, 16f),
                            dartAim.RotatedBy(Main.rand.NextFloat(-1.1f, 1.1f)) * Main.rand.NextFloat(3f, 8f),
                            Color.Lerp(SparkHot, Color.White, Main.rand.NextFloat(0.5f)),
                            Main.rand.NextFloat(0.7f, 1.2f))?.Configure(true, Main.rand.Next(10, 18));
                    }
                    //锯口甩血
                    if (!Main.dedServ && t % 3 == 1) {
                        PRTLoader.NewParticle<PRT_KikasaBloodGlob>(
                            armPos[arm] + Main.rand.NextVector2Circular(14f, 14f),
                            dartAim.RotatedBy(Main.rand.NextFloat(-0.7f, 0.7f)) * Main.rand.NextFloat(2f, 5f),
                            BloodMain * 0.55f, Main.rand.NextFloat(0.35f, 0.6f))?.Configure(Main.rand.Next(14, 24));
                    }
                    //在湖面上方撕扯：血点子打在水面
                    if (ViewedOwner && armPos[arm].Y < domain.LakeWorldY
                        && domain.LakeWorldY - armPos[arm].Y < 340f && t % 6 == 3) {
                        KikasaDomainDeco.RippleAt(new Vector2(armPos[arm].X, domain.LakeWorldY), 0.4f);
                    }
                }
                int hold = arm == ArmSaw ? SawGrindFrames : ViceClampFrames;
                if (t >= hold) {
                    NextPhase(3, authority);
                }
                return;
            }

            //咔哒收回：棘轮三响
            if (t == 4 || t == 10 || t == 15) {
                SoundEngine.PlaySound(SoundID.Item37 with {
                    Volume = 0.35f,
                    Pitch = 0.35f - t * 0.02f,
                    MaxInstances = 3
                }, armPos[arm]);
            }
            if (t >= DartRetractFrames) {
                EndAttack(authority, 95);
            }
        }

        //==================== 炮臂迫击 ====================

        private void UpdateMortar(Player owner, bool authority) {
            int phase = (int)StateParam;
            int t = (int)StateTimer;
            int target = FindTarget(owner);
            Projectile.velocity *= 0.93f;

            if (phase == 0) {
                //炮臂就位：液压抬管
                if (EnterPhaseOnce()) {
                    ToolStartupHiss(ArmCannon);
                    SoundEngine.PlaySound(SoundID.Item13 with { Volume = 0.42f, Pitch = -0.6f, MaxInstances = 2 }, armPos[ArmCannon]);
                }
                if (target < 0) {
                    EndAttack(authority, 45);
                    return;
                }
                if (t >= MortarPoseFrames) {
                    NextPhase(1, authority);
                }
                return;
            }

            if (phase == 1) {
                //三发点射不同落点：炮口下压后坐逐发读出重量
                int shotIndex = (t - 4) / MortarShotGap;
                if (t >= 4 && (t - 4) % MortarShotGap == 0 && shotIndex < MortarShots
                    && lastShellFired < shotIndex) {
                    lastShellFired = shotIndex;
                    FireMortarShell(owner, shotIndex, target, authority);
                }
                if (t >= MortarVolleyEnd) {
                    NextPhase(2, authority);
                }
                return;
            }

            //泄压回摆：炮口喷一口蒸汽血雾
            if (EnterPhaseOnce() && !Main.dedServ) {
                SoundEngine.PlaySound(SoundID.Item13 with { Volume = 0.35f, Pitch = 0.1f, MaxInstances = 2 }, armPos[ArmCannon]);
                PRTLoader.NewParticle<PRT_GhostRainMist>(armPos[ArmCannon] + new Vector2(0f, -10f),
                    new Vector2(0f, -0.7f), MistBlood * 0.8f, 0.7f)?.Configure(40);
            }
            if (t >= MortarRecoverFrames) {
                EndAttack(authority, 120);
            }
        }

        /// <summary>迫击开火拍：后坐 + 闷响 + 血雷弹（只在 owner 端生成，spawn 参数自带全部初值）</summary>
        private void FireMortarShell(Player owner, int shotIndex, int target, bool authority) {
            Vector2 aim = MortarAimDir(target);
            Vector2 muzzle = armPos[ArmCannon] + aim * 26f;

            //炮口下压后坐：知重量者先退半步
            armVel[ArmCannon] -= aim * 9f;
            cannonHeat = 30;
            SoundEngine.PlaySound(SoundID.Item61 with { Volume = 0.75f, Pitch = -0.4f + shotIndex * 0.06f, MaxInstances = 3 }, muzzle);
            SoundEngine.PlaySound(SoundID.Item95 with { Volume = 0.35f, Pitch = -0.5f, MaxInstances = 3 }, muzzle);
            if (!Main.dedServ) {
                //出膛：蒸汽血雾 + 锥形血珠
                PRTLoader.NewParticle<PRT_GhostRainMist>(muzzle, aim * 1.2f, MistBlood * 0.85f, 0.65f)?.Configure(34);
                for (int i = 0; i < 6; i++) {
                    PRTLoader.NewParticle<PRT_KikasaBloodGlob>(muzzle + Main.rand.NextVector2Circular(4f, 4f),
                        aim.RotatedByRandom(0.3f) * Main.rand.NextFloat(3f, 7f),
                        Main.rand.NextBool(3) ? BloodDeep : BloodMain,
                        Main.rand.NextFloat(0.35f, 0.6f))?.Configure(Main.rand.Next(14, 24));
                }
                PRTLoader.NewParticle<PRT_DWave>(muzzle + aim * 6f, Vector2.Zero, BloodDeep, 0.06f)
                    ?.Configure(new Vector2(0.55f, 1f), aim.ToRotation(), 0.2f, 8);
            }
            if (ViewedOwner) {
                ShakeViewer(2f);
            }

            if (!authority || target < 0) {
                return;
            }
            //弹道解算：先定"必须有的弧顶高度"再反推初速与滞空
            //目标再低也保证一段明显的迫击弧线，绝不平射
            NPC npc = Main.npc[target];
            const float gravity = 0.42f;
            float spreadX = (shotIndex - 1) * 120f;
            Vector2 landing = npc.Center + new Vector2(spreadX + npc.velocity.X * 18f, 0f);
            float dy = landing.Y - muzzle.Y;
            //基础弧能 92 ≈ 弧顶高出炮口 110px；目标在上方时按需加注
            float vy = -MathF.Sqrt(MathF.Max(-2f * gravity * dy, 0f) + 92f);
            float flight = (-vy + MathF.Sqrt(MathF.Max(vy * vy + 2f * gravity * dy, 1f))) / gravity;
            float vx = MathHelper.Clamp((landing.X - muzzle.X) / flight, -17f, 17f);
            int damage = (int)owner.GetTotalDamage(DamageClass.Summon).ApplyTo(ShellDamage);
            Projectile.NewProjectile(Projectile.GetSource_FromAI(), muzzle, new Vector2(vx, vy),
                ModContent.ProjectileType<KikasaPrimeMortar>(), damage, 6f, Projectile.owner);
        }

        /// <summary>炮管瞄向：朝目标一侧上扬 60° 上下的迫击姿态</summary>
        private Vector2 MortarAimDir(int target) {
            float side = 1f;
            if (target >= 0) {
                side = MathF.Sign(Main.npc[target].Center.X - Projectile.Center.X);
                if (side == 0f) {
                    side = 1f;
                }
            }
            return new Vector2(side * MathF.Cos(1.05f), -MathF.Sin(1.05f));
        }

        //==================== 镭射臂点射 ====================

        private void UpdateLaser(Player owner, bool authority) {
            int phase = (int)StateParam;
            int t = (int)StateTimer;
            int target = FindTarget(owner);

            if (phase == 0 || phase == 2) {
                //快速平移走位：一帧定横速，硬刹收尾
                if (EnterPhaseOnce()) {
                    float side = target >= 0
                        ? MathF.Sign(Main.npc[target].Center.X - Projectile.Center.X)
                        : owner.direction;
                    if (side == 0f) {
                        side = 1f;
                    }
                    //先撤半步再切回，两段滑步方向相反
                    float dir = phase == 0 ? -side : side;
                    Projectile.velocity = new Vector2(dir * 13f, Projectile.velocity.Y * 0.4f);
                    //拉栓音，脉冲前的机械应答
                    SoundEngine.PlaySound(SoundID.Unlock with { Volume = 0.5f, Pitch = 0.3f, MaxInstances = 3 }, armPos[ArmLaser]);
                    if (phase == 0) {
                        ToolStartupHiss(ArmLaser);
                    }
                }
                Projectile.velocity.X *= t < 7 ? 0.98f : 0.82f;
                if (target < 0 && phase == 0) {
                    EndAttack(authority, 45);
                    return;
                }
                if (t >= LaserStrafeFrames) {
                    NextPhase(phase + 1, authority);
                }
                return;
            }

            if (phase == 1 || phase == 3) {
                //双发短脉冲：滑行余势中点射
                Projectile.velocity *= 0.9f;
                int baseIndex = phase == 1 ? 0 : 2;
                int local = t == 4 ? 0 : t == 10 ? 1 : -1;
                if (local >= 0 && lastPulseFired < baseIndex + local) {
                    lastPulseFired = baseIndex + local;
                    FireLaserPulse(owner, target, authority);
                }
                if (t >= LaserVolleyFrames) {
                    NextPhase(phase + 1, authority);
                }
                return;
            }

            //收势
            Projectile.velocity *= 0.88f;
            if (t >= LaserRecoverFrames) {
                EndAttack(authority, 105);
            }
        }

        /// <summary>短脉冲开火拍：细快血弹（owner 端生成），臂小幅后坐</summary>
        private void FireLaserPulse(Player owner, int target, bool authority) {
            Vector2 aimPos = target >= 0
                ? Main.npc[target].Center + Main.npc[target].velocity * 6f
                : armPos[ArmLaser] + (armRot[ArmLaser] + MathHelper.PiOver2).ToRotationVector2() * 300f;
            Vector2 aim = (aimPos - armPos[ArmLaser]).SafeNormalize(Vector2.UnitX);
            Vector2 muzzle = armPos[ArmLaser] + aim * 24f;

            armVel[ArmLaser] -= aim * 3.5f;
            SoundEngine.PlaySound(SoundID.Item33 with { Volume = 0.4f, Pitch = 0.45f, MaxInstances = 3 }, muzzle);
            SoundEngine.PlaySound(SoundID.Item95 with { Volume = 0.3f, Pitch = 0.1f, MaxInstances = 3 }, muzzle);
            if (!Main.dedServ) {
                for (int i = 0; i < 4; i++) {
                    PRTLoader.NewParticle<PRT_Spark>(muzzle,
                        aim.RotatedBy(Main.rand.NextFloat(-0.3f, 0.3f)) * Main.rand.NextFloat(4f, 9f),
                        Color.Lerp(FoamGlow, Color.White, Main.rand.NextFloat(0.4f)),
                        Main.rand.NextFloat(0.6f, 1f))?.Configure(false, Main.rand.Next(8, 14));
                }
            }
            if (ViewedOwner) {
                ShakeViewer(0.8f);
            }

            if (authority) {
                int damage = (int)owner.GetTotalDamage(DamageClass.Summon).ApplyTo(ShellDamage);
                Projectile.NewProjectile(Projectile.GetSource_FromAI(), muzzle, aim * 26f,
                    ModContent.ProjectileType<KikasaPrimeLaserBolt>(), damage, 2f, Projectile.owner);
            }
        }

        /// <summary>工具启动的蒸汽血雾 + 液压嘶声（机械件的开机仪式）</summary>
        private void ToolStartupHiss(int arm) {
            SoundEngine.PlaySound(SoundID.Item13 with { Volume = 0.32f, Pitch = -0.35f, MaxInstances = 3 }, armPos[arm]);
            if (Main.dedServ) {
                return;
            }
            for (int i = 0; i < 2; i++) {
                PRTLoader.NewParticle<PRT_GhostRainMist>(
                    armPos[arm] + Main.rand.NextVector2Circular(10f, 10f),
                    new Vector2(Main.rand.NextFloat(-0.4f, 0.4f), -Main.rand.NextFloat(0.5f, 1f)),
                    MistBlood * 0.8f, Main.rand.NextFloat(0.5f, 0.75f))?.Configure(Main.rand.Next(30, 50));
            }
        }

        //==================== 溶解：工具逐件熄火坠湖，头最后沉没 ====================

        private int ToolDropFrame(int i) => ToolDropStart + i * ToolDropGap;

        private void UpdateDissolve(KikasaDomainPlayer domain, bool authority) {
            int t = (int)StateTimer;
            bool lakeAlive = domain.AnyActive && domain.RiseT > 0.5f;
            float lakeY = domain.LakeWorldY;

            //工具按出场同序熄火（单调闩）：火花一嘬、链条卸劲
            for (int i = 0; i < ArmCount; i++) {
                if (t >= ToolDropFrame(i) && dissolveToolDropped < i) {
                    dissolveToolDropped = i;
                    //出水中途被收场：没亮过相的工具只推进闩，不演熄火
                    if (i > emergeToolRevealed) {
                        continue;
                    }
                    SoundEngine.PlaySound(SoundID.Item13 with { Volume = 0.35f, Pitch = -0.7f + i * 0.1f, MaxInstances = 3 }, armPos[i]);
                    SoundEngine.PlaySound(SoundID.Item37 with { Volume = 0.3f, Pitch = -0.6f, MaxInstances = 3 }, armPos[i]);
                    if (!Main.dedServ) {
                        for (int k = 0; k < 3; k++) {
                            PRTLoader.NewParticle<PRT_Spark>(armPos[i] + Main.rand.NextVector2Circular(10f, 10f),
                                Main.rand.NextVector2Circular(2.5f, 2.5f),
                                Color.Lerp(SparkHot, BloodMain, 0.5f),
                                Main.rand.NextFloat(0.5f, 0.9f))?.Configure(true, Main.rand.Next(10, 16));
                        }
                        PRTLoader.NewParticle<PRT_GhostRainMist>(armPos[i], new Vector2(0f, -0.5f),
                            MistBlood * 0.75f, 0.6f)?.Configure(36);
                    }
                }
            }

            //头最后沉没
            if (t >= HeadSinkFrame) {
                if (lakeAlive) {
                    Projectile.velocity.X *= 0.92f;
                    Projectile.velocity.Y = MathF.Min(Projectile.velocity.Y + 0.26f, 8f);
                }
                else {
                    Projectile.velocity *= 0.9f;
                }
                //过水线拍（一次）
                if (lakeAlive && !dissolveHeadSplashed && Projectile.Center.Y >= lakeY) {
                    dissolveHeadSplashed = true;
                    SoundEngine.PlaySound(SoundID.SplashWeak with { Volume = 0.7f, Pitch = -0.35f, MaxInstances = 2 }, Projectile.Center);
                    if (ViewedOwner) {
                        Vector2 hit = new(Projectile.Center.X, lakeY);
                        KikasaDomainDeco.SplashAt(hit, 10);
                        KikasaDomainDeco.RippleAt(hit, 1.4f);
                        ShakeViewer(2f);
                    }
                }
            }
            else {
                Projectile.velocity *= 0.9f;
            }

            //边沉边化血珠
            if (!Main.dedServ && t % 3 == 0 && HeadAlpha() > 0.15f) {
                PRTLoader.NewParticle<PRT_KikasaBloodGlob>(
                    Projectile.Center + Main.rand.NextVector2Circular(30f, 30f),
                    new Vector2(Main.rand.NextFloat(-0.6f, 0.6f), Main.rand.NextFloat(1.5f, 3f)),
                    BloodMain * 0.5f, Main.rand.NextFloat(0.35f, 0.6f))?.Configure(Main.rand.Next(12, 22), 0f);
            }

            //owner 到点收场；远端 +10 帧兜底自杀
            if (authority && t >= DissolveTotal) {
                Projectile.Kill();
            }
            else if (!authority && t >= DissolveTotal + 10) {
                Projectile.Kill();
            }
        }

        //==================== 四臂模拟（各端本地弹簧摆，链条重量的来源）====================

        private void RebuildArms(Vector2 head) {
            armsInit = true;
            headSim = head;
            for (int i = 0; i < ArmCount; i++) {
                armPos[i] = head + RestOffset[i];
                armVel[i] = Vector2.Zero;
                armRot[i] = 0f;
                belowWater[i] = true;
                wetness[i] = 1f;
            }
        }

        /// <summary>肩锚点：左臂挂左肩、右臂挂右肩</summary>
        private Vector2 ShoulderWorld(int i)
            => headSim + new Vector2(RestOffset[i].X < 0f ? -30f : 30f, 18f);

        /// <summary>悬挂队形目标：呼吸摆动 + 头移动时的滞后拖行（链条的重量读数）</summary>
        private Vector2 RestTarget(int i) {
            float time = Main.GlobalTimeWrappedHourly;
            float ph = Seed + i * 1.917f;
            Vector2 sway = new(MathF.Sin(time * 1.35f + ph) * 13f, MathF.Sin(time * 2.05f + ph * 1.31f) * 8f);
            return headSim + RestOffset[i] + sway - Projectile.velocity * new Vector2(2.6f, 1.4f);
        }

        private void UpdateArms(KikasaDomainPlayer domain) {
            Vector2 head = Projectile.Center + Projectile.velocity;
            //硬纠：同步包把头拽走半屏，臂直接重建防抽搐
            if (!armsInit || Vector2.Distance(headSim, head) > 240f) {
                RebuildArms(head);
                return;
            }
            headSim = head;
            float lakeY = domain.LakeWorldY;
            int t = (int)StateTimer;
            int dartArm = State == StateSawDart ? ArmSaw : State == StateViceDart ? ArmVice : -1;

            for (int i = 0; i < ArmCount; i++) {
                Vector2 target = RestTarget(i);
                float k = 0.11f;
                float damp = 0.86f;
                bool ballistic = false;
                bool falling = false;
                float wantRot = MathF.Sin(Main.GlobalTimeWrappedHourly * 1.35f + Seed + i * 1.917f) * 0.07f;
                float rotRate = 0.14f;

                switch (State) {
                    case StateEmerge: {
                        int beat = ToolBeat(i);
                        if (t < beat) {
                            //待命：沉在破水点水下
                            target = new Vector2(ToolHoldPoint(i, lakeY).X, lakeY + 46f);
                            k = 0.3f;
                            damp = 0.7f;
                        }
                        else if (t < HeadBreachFrame) {
                            //破水举起持位：强弹簧自带过冲，机械弹出的脆劲
                            target = ToolHoldPoint(i, lakeY);
                            k = 0.34f;
                            damp = 0.78f;
                        }
                        //头起后并入队形（默认 RestTarget）
                        break;
                    }
                    case StateSawDart:
                    case StateViceDart: {
                        if (i == dartArm) {
                            int phase = (int)StateParam;
                            if (phase == 0) {
                                //回缩蓄力：贴着肩窝收劲
                                target = ShoulderWorld(i) - dartAim * 34f + new Vector2(0f, -6f);
                                k = 0.22f;
                                damp = 0.8f;
                                wantRot = dartAim.ToRotation() - MathHelper.PiOver2;
                                rotRate = 0.4f;
                            }
                            else if (phase == 1) {
                                //弹出段：纯弹道，链条放到底
                                ballistic = true;
                                wantRot = dartAim.ToRotation() - MathHelper.PiOver2;
                                rotRate = 0.5f;
                            }
                            else if (phase == 2) {
                                //撕扯/夹合：钉在伸展位，锯缓慢啃向目标
                                target = armPos[i] + dartAim * (i == ArmSaw ? 1.4f : 0.4f);
                                k = 0.3f;
                                damp = 0.72f;
                                wantRot = dartAim.ToRotation() - MathHelper.PiOver2;
                                rotRate = 0.45f;
                            }
                            else {
                                //收回：弹簧回巢，自带过冲
                                k = 0.16f;
                                damp = 0.8f;
                            }
                        }
                        break;
                    }
                    case StateMortar: {
                        if (i == ArmCannon) {
                            Vector2 aim = MortarAimDir(FindTarget(Owner));
                            target = headSim + new Vector2(MathF.Sign(aim.X) * 96f, 40f);
                            k = 0.18f;
                            damp = 0.8f;
                            wantRot = aim.ToRotation() - MathHelper.PiOver2;
                            rotRate = 0.22f;
                        }
                        break;
                    }
                    case StateLaser: {
                        if (i == ArmLaser) {
                            int target2 = FindTarget(Owner);
                            Vector2 aimPos = target2 >= 0 ? Main.npc[target2].Center
                                : headSim + new Vector2(Owner.direction * 300f, 0f);
                            Vector2 aim = (aimPos - armPos[i]).SafeNormalize(Vector2.UnitX);
                            target = headSim + new Vector2(MathF.Sign(aim.X) * 118f, -4f);
                            k = 0.2f;
                            damp = 0.78f;
                            wantRot = aim.ToRotation() - MathHelper.PiOver2;
                            rotRate = 0.35f;
                        }
                        break;
                    }
                    case StateDissolve: {
                        if (dissolveToolDropped >= i) {
                            //熄火：链条卸劲，工具自由落体坠湖
                            falling = true;
                        }
                        break;
                    }
                }

                if (ballistic) {
                    //弹出段只管飞，链长放到本次伸展量就哐当勒停
                    armVel[i] *= 0.995f;
                    armPos[i] += armVel[i];
                    Vector2 fromShoulder = armPos[i] - ShoulderWorld(i);
                    float reach = fromShoulder.Length();
                    if (reach > dartReach) {
                        armPos[i] = ShoulderWorld(i) + fromShoulder.SafeNormalize(Vector2.UnitY) * dartReach;
                        armVel[i] *= 0.2f;
                        if (!dartReachThunked) {
                            dartReachThunked = true;
                            dartTautVibe = 12;
                            SoundEngine.PlaySound(SoundID.Item37 with { Volume = 0.55f, Pitch = -0.2f, MaxInstances = 2 }, armPos[i]);
                        }
                    }
                }
                else if (falling) {
                    armVel[i].X *= 0.96f;
                    armVel[i].Y = MathF.Min(armVel[i].Y + 0.34f, 11f);
                    armPos[i] += armVel[i];
                    //坠湖水花（每件一次；没亮过相的工具不响）
                    bool lakeAlive = domain.AnyActive && domain.RiseT > 0.5f;
                    if (lakeAlive && i <= emergeToolRevealed
                        && !dissolveToolSplashed[i] && armPos[i].Y >= lakeY) {
                        dissolveToolSplashed[i] = true;
                        SoundEngine.PlaySound(SoundID.SplashWeak with { Volume = 0.5f, Pitch = -0.25f + i * 0.06f, MaxInstances = 3 }, armPos[i]);
                        if (ViewedOwner) {
                            Vector2 hit = new(armPos[i].X, lakeY);
                            KikasaDomainDeco.SplashAt(hit, 7);
                            KikasaDomainDeco.RippleAt(hit, 0.9f);
                        }
                    }
                }
                else {
                    armVel[i] = (armVel[i] + (target - armPos[i]) * k) * damp;
                    armPos[i] += armVel[i];
                }

                armRot[i] = armRot[i].AngleLerp(wantRot, rotRate);
            }

            UpdateToolCrossings(domain);
        }

        /// <summary>工具过水线（双向）：水花帧内限量，出水拉满湿度</summary>
        private void UpdateToolCrossings(KikasaDomainPlayer domain) {
            float lakeY = domain.LakeWorldY;
            bool lakeAlive = domain.AnyActive && domain.RiseT > 0.5f;
            bool viewed = ViewedOwner;
            int fxBudget = 2;

            for (int i = 0; i < ArmCount; i++) {
                bool below = armPos[i].Y >= lakeY;
                if (below != belowWater[i]) {
                    belowWater[i] = below;
                    wetness[i] = 1f;
                    //出场/溶解各自的专拍已经放过水花，这里只补常规过线
                    if (State != StateEmerge && State != StateDissolve
                        && lakeAlive && viewed && fxBudget > 0) {
                        fxBudget--;
                        Vector2 hit = new(armPos[i].X, lakeY);
                        KikasaDomainDeco.RippleAt(hit, 0.5f);
                        for (int kk = 0; kk < 2; kk++) {
                            PRTLoader.NewParticle<PRT_KikasaBloodGlob>(
                                hit + new Vector2(Main.rand.NextFloat(-10f, 10f), -3f),
                                new Vector2(Main.rand.NextFloat(-1.1f, 1.1f), -Main.rand.NextFloat(2f, 4f)),
                                BloodMain * 0.6f, Main.rand.NextFloat(0.35f, 0.55f))?.Configure(Main.rand.Next(14, 24));
                        }
                    }
                }
                wetness[i] = below ? 1f : MathF.Max(0f, wetness[i] - 0.01f);
            }
        }

        /// <summary>关节与链条缝隙渗血：帧内预算错拍滴落，刚出水的工具淌得最凶</summary>
        private void UpdateJointDrips(KikasaDomainPlayer domain) {
            if (Main.dedServ || !armsInit) {
                return;
            }
            int budget = 2;
            for (int k = 0; k < 3 && budget > 0; k++) {
                int i = Main.rand.Next(ArmCount);
                if (belowWater[i] || ToolAlpha(i) < 0.4f) {
                    continue;
                }
                //湿度即概率，常态也留一点关节渗血的底噪
                if (Main.rand.NextFloat() > 0.1f + wetness[i] * 0.4f) {
                    continue;
                }
                budget--;
                //滴点在链条中段或工具口
                Vector2 pos = Main.rand.NextBool()
                    ? Vector2.Lerp(ShoulderWorld(i), armPos[i], Main.rand.NextFloat(0.3f, 0.8f))
                    : armPos[i] + Main.rand.NextVector2Circular(14f, 12f);
                PRTLoader.NewParticle<PRT_KikasaBloodGlob>(pos,
                    new Vector2(Projectile.velocity.X * 0.05f, Main.rand.NextFloat(0.7f, 1.6f)),
                    (Main.rand.NextBool(3) ? BloodDeep : BloodMain) * Main.rand.NextFloat(0.45f, 0.6f),
                    Main.rand.NextFloat(0.3f, 0.55f))?.Configure(Main.rand.Next(18, 32), 0.3f);
            }
        }

        //==================== 公共小件 ====================

        private int FindTarget(Player owner) {
            if (owner.HasMinionAttackTargetNPC) {
                NPC picked = Main.npc[owner.MinionAttackTargetNPC];
                if (picked.CanBeChasedBy(Projectile)
                    && Vector2.Distance(picked.Center, owner.Center) < 1500f) {
                    return picked.whoAmI;
                }
            }
            int best = -1;
            float bestDist = 1050f;
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npc = Main.npc[i];
                if (npc?.active != true || !npc.CanBeChasedBy(Projectile)) {
                    continue;
                }
                float dist = Vector2.Distance(npc.Center, owner.Center);
                if (dist < bestDist) {
                    bestDist = dist;
                    best = i;
                }
            }
            return best;
        }

        private void UpdateFrames() {
            //头：常态 0/1 慢速交替，出手窗亮 2 号狰狞面
            bool rage = State switch {
                StateSawDart or StateViceDart => (int)StateParam is 1 or 2,
                StateMortar => (int)StateParam == 1,
                StateLaser => (int)StateParam is 1 or 3,
                StateEmerge => StateTimer >= AwakenFrame,
                _ => false,
            };
            if (rage) {
                headFrameIndex = 2;
            }
            else {
                if (headFrameIndex > 1) {
                    headFrameIndex = 0;
                }
                if (++headFrameTick >= 12) {
                    headFrameTick = 0;
                    headFrameIndex = (headFrameIndex + 1) % 2;
                }
            }

            //锯：出手期高速旋转，闲时缓转
            bool sawActive = State == StateSawDart
                || (State == StateEmerge && StateTimer >= ToolBeat(ArmSaw) && StateTimer < ToolBeat(ArmSaw) + 16);
            if (++sawFrameTick >= (sawActive ? 2 : 14)) {
                sawFrameTick = 0;
                sawFrameIndex = (sawFrameIndex + 1) % 2;
            }

            //头姿态：轻微顺速度倾斜，绝不旋颅
            float wantTilt = MathHelper.Clamp(Projectile.velocity.X * 0.014f, -0.14f, 0.14f);
            Projectile.rotation = Projectile.rotation.AngleLerp(wantTilt, 0.12f);
        }

        internal bool ViewedOwner
            => KikasaDomain.Viewed != null && KikasaDomain.Viewed.Player.whoAmI == Projectile.owner;

        private static void ShakeViewer(float amount)
            => Main.LocalPlayer?.CWR()?.GetScreenShake(amount);

        //==================== 表现参数 ====================

        private float HeadAlpha() {
            int t = (int)StateTimer;
            return State switch {
                StateEmerge => t < HeadBreachFrame ? 0f : MathHelper.Clamp((t - HeadBreachFrame) / 5f, 0f, 1f),
                //出水中途被收场：没露过面的头不许在溶解里凭空闪现
                StateDissolve => emergeBreachDone ? MathHelper.Clamp((DissolveTotal - t) / 14f, 0f, 1f) : 0f,
                _ => 1f,
            };
        }

        private float ToolAlpha(int i) {
            int t = (int)StateTimer;
            if (State == StateEmerge) {
                return MathHelper.Clamp((t - ToolBeat(i)) / 5f, 0f, 1f);
            }
            if (State == StateDissolve) {
                //出水中途被收场：还没亮相的工具不参加谢幕
                if (i > emergeToolRevealed) {
                    return 0f;
                }
                float byTimer = 1f - MathHelper.Clamp((t - ToolDropFrame(i) - 24f) / 20f, 0f, 1f);
                //坠进湖里按深度隐没
                KikasaDomainPlayer domain = Owner?.GetModPlayer<KikasaDomainPlayer>();
                if (domain != null && domain.AnyActive && domain.RiseT > 0.5f) {
                    float byDepth = 1f - MathHelper.Clamp((armPos[i].Y - domain.LakeWorldY) / 56f, 0f, 1f);
                    return MathF.Min(byTimer, byDepth);
                }
                return byTimer;
            }
            return 1f;
        }

        /// <summary>头的血水化：金属件血水化程度低，出水期自上而下扫描凝实</summary>
        private float HeadForm() {
            float steady = 0.2f + MathF.Sin(Main.GlobalTimeWrappedHourly * 2.7f + Seed) * 0.04f;
            if (State == StateEmerge) {
                int t = (int)StateTimer;
                if (t < HeadBreachFrame) {
                    return 1f;
                }
                float k = MathHelper.Clamp((t - HeadBreachFrame) / (float)(AwakenFrame - HeadBreachFrame), 0f, 1f);
                return MathHelper.Lerp(1f, steady, k * k * (3f - 2f * k));
            }
            if (State == StateDissolve) {
                return MathHelper.Clamp(steady + StateTimer / DissolveTotal * 0.4f, 0f, 1f);
            }
            return steady;
        }

        private float HeadScanMode() {
            if (State != StateEmerge) {
                return 0f;
            }
            int t = (int)StateTimer;
            if (t < HeadBreachFrame) {
                return 1f;
            }
            return 1f - MathHelper.Clamp((t - HeadBreachFrame) / 30f, 0f, 1f);
        }

        private float HeadDissolve()
            => State == StateDissolve
                ? MathF.Pow(MathHelper.Clamp((StateTimer - HeadSinkFrame) / 42f, 0f, 1f), 0.9f)
                : 0f;

        private float ToolDissolve(int i)
            => State == StateDissolve
                ? MathHelper.Clamp((StateTimer - ToolDropFrame(i)) / 30f, 0f, 1f)
                : 0f;

        /// <summary>目镜扫光进度：跟随态周期性慢扫，觉醒拍一次亮扫；<0 表示熄灭</summary>
        private float ScanSweepT() {
            if (State == StateEmerge) {
                int t = (int)StateTimer;
                if (t >= AwakenFrame) {
                    float p = (t - AwakenFrame) / 26f;
                    return p <= 1f ? p : -1f;
                }
                return -1f;
            }
            if (State == StateFollow) {
                float cycle = ((int)StateTimer + Projectile.identity * 37) % 260;
                return cycle < 46f ? cycle / 46f : -1f;
            }
            return -1f;
        }

        //==================== 绘制 ====================

        public override bool PreDraw(ref Color lightColor) {
            if (!armsInit) {
                return false;
            }
            SpriteBatch sb = Main.spriteBatch;
            //身体批：链→工具→头，血湖材质着色器逐件换参
            DrawBodyPieces(sb, lightColor);
            //加色层：预兆血光、目镜扫光、炮口余温、脉冲预闪
            DrawGlowLayer(sb);
            return false;
        }

        private static Vector2 Bezier(Vector2 a, Vector2 c, Vector2 b, float t)
            => Vector2.Lerp(Vector2.Lerp(a, c, t), Vector2.Lerp(c, b, t), t);

        private void DrawBodyPieces(SpriteBatch sb, Color lightColor) {
            Effect form = EffectLoader.KikasaItemForm?.Value;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            bool shaderOk = form != null && noise != null;

            sb.End();
            sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.PointClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
            if (shaderOk) {
                Main.instance.GraphicsDevice.Textures[1] = noise;
                Main.instance.GraphicsDevice.SamplerStates[1] = SamplerState.LinearWrap;
                form.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            }

            //链在最底
            for (int i = 0; i < ArmCount; i++) {
                float alpha = ToolAlpha(i);
                if (alpha > 0.02f) {
                    DrawChainArm(sb, form, shaderOk, i, alpha, lightColor);
                }
            }
            //工具压链上
            for (int i = 0; i < ArmCount; i++) {
                float alpha = ToolAlpha(i);
                if (alpha > 0.02f) {
                    DrawTool(sb, form, shaderOk, i, alpha, lightColor);
                }
            }
            //头压顶
            float headAlpha = HeadAlpha();
            if (headAlpha > 0.02f) {
                DrawHead(sb, form, shaderOk, headAlpha, lightColor);
            }

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
        }

        /// <summary>臂链：原版 Arm_Bone_2 骨节沿悬链弧分三节铺出；突刺绷直时高频颤动</summary>
        private void DrawChainArm(SpriteBatch sb, Effect form, bool shaderOk, int i, float alpha, Color lightColor) {
            Texture2D bone = TextureAssets.BoneArm2?.Value;
            if (bone == null) {
                return;
            }
            Vector2 s = ShoulderWorld(i);
            Vector2 a = armPos[i];
            float dist = Vector2.Distance(s, a);
            //悬链弧：链越松垂度越大，突刺伸展时自然绷直
            float restLen = RestOffset[i].Length() * 1.18f;
            float sag = 9f + MathHelper.Clamp(restLen - dist, 0f, 110f) * 0.55f;
            Vector2 mid = (s + a) * 0.5f + new Vector2(0f, sag);
            //绷直颤动：拉满后链条打的那个战栗
            int dartArm = State == StateSawDart ? ArmSaw : State == StateViceDart ? ArmVice : -1;
            if (dartTautVibe > 0 && i == dartArm) {
                Vector2 perp = (a - s).SafeNormalize(Vector2.UnitY).RotatedBy(MathHelper.PiOver2);
                mid += perp * MathF.Sin(StateTimer * 2.9f + Seed) * (dartTautVibe / 12f) * 6f;
            }

            float dissolve = ToolDissolve(i);
            const int segs = 3;
            Vector2 prev = s;
            for (int k = 1; k <= segs; k++) {
                Vector2 p = Bezier(s, mid, a, k / (float)segs);
                Vector2 c = (prev + p) * 0.5f;
                Vector2 dir = p - prev;
                float len = dir.Length();
                if (len < 2f) {
                    prev = p;
                    continue;
                }
                float rot = dir.ToRotation() + MathHelper.PiOver2;
                Vector2 scale = new(0.92f, len / bone.Height * 1.12f);

                Color color;
                if (shaderOk) {
                    //链条缝隙湿血：血水化略高于金属件
                    float chainForm = MathHelper.Clamp(0.3f + wetness[i] * 0.14f
                        + MathF.Sin(Main.GlobalTimeWrappedHourly * 2.2f + Seed + i * 0.9f + k * 0.6f) * 0.04f, 0f, 0.6f);
                    form.Parameters["uSeed"]?.SetValue(Seed + i * 2.3f + k * 0.71f);
                    form.Parameters["uForm"]?.SetValue(chainForm);
                    form.Parameters["uDissolve"]?.SetValue(dissolve);
                    form.Parameters["uScanMode"]?.SetValue(0f);
                    form.Parameters["uUvRect"]?.SetValue(new Vector4(0f, 0f, 1f, 1f));
                    form.Parameters["uTexel"]?.SetValue(new Vector2(1f / bone.Width, 1f / bone.Height));
                    form.Parameters["uAspect"]?.SetValue(bone.Width / (float)bone.Height);
                    form.CurrentTechnique.Passes[0].Apply();
                    color = new Color(255, 255, 255, (byte)(alpha * 255f));
                }
                else {
                    color = Color.Lerp(lightColor, BloodMain, 0.5f) * (alpha * (1f - dissolve));
                }
                sb.Draw(bone, c - Main.screenPosition, null, color, rot,
                    bone.Size() * 0.5f, scale, SpriteEffects.None, 0f);
                prev = p;
            }
        }

        private void DrawTool(SpriteBatch sb, Effect form, bool shaderOk, int i, float alpha, Color lightColor) {
            int npcType = ArmNpcType(i);
            Main.instance.LoadNPC(npcType);
            Texture2D tex = TextureAssets.Npc[npcType]?.Value;
            if (tex == null) {
                return;
            }
            int frameCount = Main.npcFrameCount[npcType];
            int frameIndex = i switch {
                ArmSaw => sawFrameIndex % frameCount,
                //钳只在夹合两拍咬住
                ArmVice => State == StateViceDart && (int)StateParam == 2 && StateTimer < 7 ? 1 % frameCount : 0,
                _ => 0,
            };
            int frameH = tex.Height / frameCount;
            Rectangle frame = new(0, frameH * frameIndex, tex.Width, frameH);
            //左侧臂镜像（原版同款按侧翻面）
            SpriteEffects flip = RestOffset[i].X < 0f ? SpriteEffects.FlipHorizontally : SpriteEffects.None;

            float dissolve = ToolDissolve(i);
            Color color;
            if (shaderOk) {
                //金属件血水化程度低：uForm 压低，湿度只短暂抬一点
                float toolForm = MathHelper.Clamp(0.14f + wetness[i] * 0.18f
                    + MathF.Sin(Main.GlobalTimeWrappedHourly * 2.6f + Seed + i * 1.4f) * 0.03f, 0f, 0.5f);
                form.Parameters["uSeed"]?.SetValue(Seed + i * 3.1f);
                form.Parameters["uForm"]?.SetValue(toolForm);
                form.Parameters["uDissolve"]?.SetValue(dissolve);
                form.Parameters["uScanMode"]?.SetValue(0f);
                form.Parameters["uUvRect"]?.SetValue(new Vector4(
                    frame.X / (float)tex.Width, frame.Y / (float)tex.Height,
                    frame.Width / (float)tex.Width, frame.Height / (float)tex.Height));
                form.Parameters["uTexel"]?.SetValue(new Vector2(1f / tex.Width, 1f / tex.Height));
                form.Parameters["uAspect"]?.SetValue(frame.Width / (float)frame.Height);
                form.CurrentTechnique.Passes[0].Apply();
                color = new Color(255, 255, 255, (byte)(alpha * 255f));
            }
            else {
                color = Color.Lerp(lightColor, BloodMain, 0.5f) * (alpha * (1f - dissolve));
            }
            sb.Draw(tex, armPos[i] - Main.screenPosition, frame, color, armRot[i],
                frame.Size() * 0.5f, 0.94f, flip, 0f);
        }

        private void DrawHead(SpriteBatch sb, Effect form, bool shaderOk, float alpha, Color lightColor) {
            Main.instance.LoadNPC(NPCID.SkeletronPrime);
            Texture2D tex = TextureAssets.Npc[NPCID.SkeletronPrime]?.Value;
            if (tex == null) {
                return;
            }
            int frameH = tex.Height / Main.npcFrameCount[NPCID.SkeletronPrime];
            Rectangle frame = new(0, frameH * headFrameIndex, tex.Width, frameH);

            Color color;
            if (shaderOk) {
                form.Parameters["uSeed"]?.SetValue(Seed);
                form.Parameters["uForm"]?.SetValue(HeadForm());
                form.Parameters["uDissolve"]?.SetValue(HeadDissolve());
                form.Parameters["uScanMode"]?.SetValue(HeadScanMode());
                form.Parameters["uUvRect"]?.SetValue(new Vector4(
                    frame.X / (float)tex.Width, frame.Y / (float)tex.Height,
                    frame.Width / (float)tex.Width, frame.Height / (float)tex.Height));
                form.Parameters["uTexel"]?.SetValue(new Vector2(1f / tex.Width, 1f / tex.Height));
                form.Parameters["uAspect"]?.SetValue(frame.Width / (float)frame.Height);
                form.CurrentTechnique.Passes[0].Apply();
                color = new Color(255, 255, 255, (byte)(alpha * 255f));
            }
            else {
                color = Color.Lerp(lightColor, BloodMain, 0.5f) * alpha;
            }
            sb.Draw(tex, Projectile.Center - Main.screenPosition, frame, color, Projectile.rotation,
                frame.Size() * 0.5f, 0.92f, SpriteEffects.None, 0f);
        }

        /// <summary>加色层：出水预兆血光、目镜扫光、锯口灼光、炮口余温、脉冲预闪</summary>
        private void DrawGlowLayer(SpriteBatch sb) {
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (glow == null) {
                return;
            }
            KikasaDomainPlayer domain = Owner?.GetModPlayer<KikasaDomainPlayer>();
            if (domain == null) {
                return;
            }

            bool begun = false;
            Vector2 gOrigin = glow.Size() * 0.5f;
            void EnsureBegin() {
                if (!begun) {
                    sb.End();
                    sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
                        DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
                    begun = true;
                }
            }

            int t = (int)StateTimer;

            //出水预兆：头的破水点水下血光自深处上浮
            if (State == StateEmerge && t < HeadBreachFrame) {
                float ot = MathHelper.Clamp(t / (float)HeadBreachFrame, 0f, 1f);
                float ease = 1f - (1f - ot) * (1f - ot);
                Vector2 pos = new(Projectile.Center.X, domain.LakeWorldY + MathHelper.Lerp(56f, 10f, ease));
                float r = 30f + 26f * ease;
                EnsureBegin();
                sb.Draw(glow, pos - Main.screenPosition, null, FoamGlow * (0.4f * ease), 0f,
                    gOrigin, new Vector2(r * 2.8f / glow.Width, r * 1.05f / glow.Height), SpriteEffects.None, 0f);
            }

            //目镜扫光：中枢的红目自头下扫过一道细光锥
            float sweep = ScanSweepT();
            float headAlpha = HeadAlpha();
            if (sweep >= 0f && headAlpha > 0.5f) {
                EnsureBegin();
                Vector2 eye = Projectile.Center + new Vector2(0f, 8f);
                float ang = MathHelper.PiOver2 + MathHelper.Lerp(-0.62f, 0.62f, sweep);
                float bright = MathF.Sin(sweep * MathHelper.Pi);
                bool awaken = State == StateEmerge;
                float beamLen = awaken ? 180f : 130f;
                float a = (awaken ? 0.55f : 0.3f) * bright;
                sb.Draw(glow, eye + ang.ToRotationVector2() * beamLen * 0.5f - Main.screenPosition, null,
                    FoamGlow * a, ang, gOrigin,
                    new Vector2(beamLen * 2f / glow.Width, 13f / glow.Height), SpriteEffects.None, 0f);
                sb.Draw(glow, eye - Main.screenPosition, null, BloodMain * (a * 1.1f), 0f,
                    gOrigin, new Vector2(20f / glow.Width * 2f), SpriteEffects.None, 0f);
            }
            //常燃目镜红点
            if (headAlpha > 0.3f) {
                EnsureBegin();
                float pulse = 0.3f + 0.1f * MathF.Sin(Main.GlobalTimeWrappedHourly * 4.2f + Seed);
                sb.Draw(glow, Projectile.Center + new Vector2(0f, 8f) - Main.screenPosition, null,
                    BloodMain * (pulse * headAlpha), 0f, gOrigin,
                    new Vector2(13f * 2f / glow.Width), SpriteEffects.None, 0f);
            }

            //锯撕扯窗的灼光
            if (State == StateSawDart && (int)StateParam == 2 && ToolAlpha(ArmSaw) > 0.5f) {
                EnsureBegin();
                sb.Draw(glow, armPos[ArmSaw] - Main.screenPosition, null,
                    new Color(SparkHot.R, SparkHot.G, SparkHot.B) * 0.4f, 0f, gOrigin,
                    new Vector2(30f * 2f / glow.Width), SpriteEffects.None, 0f);
            }

            //炮口余温衰减
            if (cannonHeat > 0 && ToolAlpha(ArmCannon) > 0.5f) {
                EnsureBegin();
                float heat = cannonHeat / 30f;
                Vector2 muzzle = armPos[ArmCannon] + (armRot[ArmCannon] + MathHelper.PiOver2).ToRotationVector2() * 24f;
                sb.Draw(glow, muzzle - Main.screenPosition, null, FoamGlow * (0.45f * heat), 0f,
                    gOrigin, new Vector2(16f * 2f / glow.Width), SpriteEffects.None, 0f);
            }

            //脉冲预闪：拉栓后、出弹前那两帧的积光
            if (State == StateLaser && (int)StateParam is 1 or 3 && ToolAlpha(ArmLaser) > 0.5f) {
                int local = t <= 4 ? t : t <= 10 ? t - 6 : -1;
                if (local is >= 0 and <= 4) {
                    EnsureBegin();
                    float k = local / 4f;
                    Vector2 muzzle = armPos[ArmLaser] + (armRot[ArmLaser] + MathHelper.PiOver2).ToRotationVector2() * 24f;
                    sb.Draw(glow, muzzle - Main.screenPosition, null, FoamGlow * (0.5f * k), 0f,
                        gOrigin, new Vector2((6f + 10f * k) * 2f / glow.Width), SpriteEffects.None, 0f);
                }
            }

            if (begun) {
                sb.End();
                sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                    DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
            }
        }

        //==================== 命中与谢幕 ====================

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            //臂击命中（OnHit 只在 owner 端跑，队友看溅血弹幕层即可）
            if (Main.dedServ) {
                return;
            }
            bool saw = State == StateSawDart;
            for (int i = 0; i < (saw ? 6 : 10); i++) {
                PRTLoader.NewParticle<PRT_KikasaBloodGlob>(
                    target.Center + Main.rand.NextVector2Circular(20f, 20f),
                    dartAim * (saw ? 2.5f : 4f) + Main.rand.NextVector2Circular(2.6f, 2.6f),
                    BloodMain * 0.6f, Main.rand.NextFloat(0.4f, 0.7f))
                    ?.Configure(Main.rand.Next(14, 26), Main.rand.NextFloat(-0.5f, 0.5f));
            }
            SoundEngine.PlaySound(SoundID.NPCHit4 with {
                Volume = saw ? 0.5f : 0.7f,
                Pitch = saw ? 0.15f : -0.45f,
                MaxInstances = 3
            }, target.Center);
            if (!saw) {
                //钳的一口重咬
                SoundEngine.PlaySound(SoundID.NPCHit1 with { Volume = 0.6f, Pitch = -0.5f, MaxInstances = 2 }, target.Center);
                if (ViewedOwner) {
                    ShakeViewer(2.5f);
                }
            }
        }

        public override void OnKill(int timeLeft) {
            //谢幕残珠：头与四件工具各留一口血水
            if (Main.dedServ || !armsInit) {
                return;
            }
            for (int i = 0; i < 8; i++) {
                PRTLoader.NewParticle<PRT_KikasaBloodGlob>(
                    Projectile.Center + Main.rand.NextVector2Circular(28f, 28f),
                    new Vector2(Main.rand.NextFloat(-1.2f, 1.2f), Main.rand.NextFloat(0.5f, 2.6f)),
                    BloodMain * 0.5f, Main.rand.NextFloat(0.35f, 0.6f))?.Configure(Main.rand.Next(14, 26), 0f);
            }
            for (int i = 0; i < ArmCount; i++) {
                for (int k = 0; k < 3; k++) {
                    PRTLoader.NewParticle<PRT_KikasaBloodGlob>(
                        armPos[i] + Main.rand.NextVector2Circular(14f, 14f),
                        new Vector2(Main.rand.NextFloat(-1f, 1f), Main.rand.NextFloat(0.5f, 2.2f)),
                        BloodDeep * 0.5f, Main.rand.NextFloat(0.3f, 0.55f))?.Configure(Main.rand.Next(12, 24), 0f);
                }
            }
            PRTLoader.NewParticle<PRT_GhostRainMist>(Projectile.Center,
                new Vector2(0f, -0.25f), MistBlood * 0.7f, Main.rand.NextFloat(0.65f, 0.95f))
                ?.Configure(Main.rand.Next(50, 80));
        }
    }
}
