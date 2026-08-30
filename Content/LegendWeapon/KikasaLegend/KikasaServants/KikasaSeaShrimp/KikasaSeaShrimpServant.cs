using CalamityOverhaul.Common;
using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaDomains;
using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaServants.KikasaEye;
using CalamityOverhaul.Content.NPCs.SeaShrimp.Core;
using CalamityOverhaul.Content.NPCs.SeaShrimp.Kinematics;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaServants.KikasaSeaShrimp
{
    /// <summary>
    /// 鬼奴·湖水版渊晶海虾。单弹幕内部装配一只小型部件虾：
    /// 脊链（头+3体节+尾扇）+ 双螯二骨 IK（复用 boss 的 <see cref="TwoBoneIK"/>）+
    /// 六足程序化划桨 + verlet 触角，部件贴图借 boss 素材、血水衣着色。
    /// 出场为倒跃亮螯：泡群预兆后自湖面跃出，空中亮螯翻身落定。
    /// 攻击一为空泡拳（单螯前刺打出血空泡，短延迟爆缩内爆），
    /// 攻击二为尾扇齐射（弓身卷曲，尾扇甩出一排血水弹）。
    /// 联机同世吞契约：状态走 ai[0..2]（相位与出拳侧打包进 StateParam）、
    /// owner 转场盖 netUpdate 章、骨架各端本地重建、生命线只有 owner 判
    /// </summary>
    internal class KikasaSeaShrimpServant : ModProjectile, IKikasaServant
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        //==================== 可调基数（占位初值，验收再调）====================

        /// <summary>螯击接触基伤（召唤加成前）</summary>
        internal const int ClawDamage = 720;

        /// <summary>空泡爆缩基伤（召唤加成前），空泡弹幕消费</summary>
        internal const int OrbDamage = 400;

        /// <summary>尾扇水弹基伤（召唤加成前），水弹弹幕消费</summary>
        internal const int BoltDamage = 360;

        //==================== 部件贴图（借 boss 素材，锚点常量与 boss 渲染器同值）====================

        [VaultLoaden(CWRConstant.NPC + "SeaShrimp/SeaShrimpHead")]
        private static Asset<Texture2D> HeadTex = null;
        [VaultLoaden(CWRConstant.NPC + "SeaShrimp/SeaShrimpBodySegment1")]
        private static Asset<Texture2D> Seg1Tex = null;
        [VaultLoaden(CWRConstant.NPC + "SeaShrimp/SeaShrimpBodySegment2")]
        private static Asset<Texture2D> Seg2Tex = null;
        [VaultLoaden(CWRConstant.NPC + "SeaShrimp/SeaShrimpBodySegment3")]
        private static Asset<Texture2D> Seg3Tex = null;
        [VaultLoaden(CWRConstant.NPC + "SeaShrimp/SeaShrimpTailFan")]
        private static Asset<Texture2D> TailTex = null;
        [VaultLoaden(CWRConstant.NPC + "SeaShrimp/SeaShrimpClaw")]
        private static Asset<Texture2D> ClawTex = null;
        [VaultLoaden(CWRConstant.NPC + "SeaShrimp/SeaShrimpClawArm1")]
        private static Asset<Texture2D> Arm1Tex = null;
        [VaultLoaden(CWRConstant.NPC + "SeaShrimp/SeaShrimpClawArm2")]
        private static Asset<Texture2D> Arm2Tex = null;
        [VaultLoaden(CWRConstant.NPC + "SeaShrimp/SeaShrimpLeg1")]
        private static Asset<Texture2D> Leg1Tex = null;
        [VaultLoaden(CWRConstant.NPC + "SeaShrimp/SeaShrimpLeg2")]
        private static Asset<Texture2D> Leg2Tex = null;
        [VaultLoaden(CWRConstant.NPC + "SeaShrimp/SeaShrimpLeg3")]
        private static Asset<Texture2D> Leg3Tex = null;

        //臂节/螯贴图锚点（2x 像素坐标，与 SeaShrimpRenderer 同值；螯轴角终裁 -2.52）
        private static readonly Vector2 Arm1Anchor = new(25f, 10f);
        private const float Arm1AxisLen = 102f;
        private static readonly Vector2 Arm2Anchor = new(28f, 10f);
        private const float Arm2AxisLen = 88f;
        private static readonly Vector2 ClawAnchor = new(40f, 96f);
        private const float ClawTexAxis = -2.52f;
        private static readonly Vector2[] LegHip = [new(32f, 5f), new(38f, 6f), new(45f, 4f)];
        private static readonly Vector2[] LegTip = [new(4f, 15f), new(5f, 18f), new(5f, 13f)];

        //==================== 骨架尺寸（boss 调参 × MiniScale）====================

        /// <summary>整体缩放：留一个总旋钮给验收（骨长/节距/贴图一起走）</summary>
        internal const float MiniScale = 1f;
        private const int NodeCount = 5;
        private static float ArmBone1 => SeaShrimpDirector.ArmBone1 * MiniScale;
        private static float ArmBone2 => SeaShrimpDirector.ArmBone2 * MiniScale;

        //==================== 状态 ====================

        private const int StateEmerge = 0;
        private const int StateFollow = 1;
        private const int StatePunch = 2;
        private const int StateTailVolley = 3;
        private const int StateDissolve = 4;

        private int State { get => (int)Projectile.ai[0]; set => Projectile.ai[0] = value; }
        private ref float StateTimer => ref Projectile.ai[1];
        /// <summary>状态内子参数：出水期=起跳横向符号；攻击期=相位(低3位)+出拳侧(第4位)</summary>
        private ref float StateParam => ref Projectile.ai[2];

        private int Phase => (int)StateParam & 7;
        private int PunchArm => ((int)StateParam >> 3) & 1;

        //==================== 时序 ====================

        //倒跃出水：泡群预兆→跃出翻身→落定
        private const int OmenEnd = 28;
        private const int LeapEnd = 64;
        private const int EmergeTotal = 96;
        private const int EmergeTimeout = 240;

        //空泡拳：对准收臂→出拳→回守
        private const int PunchAim = 20;
        private const int PunchStrike = 12;
        private const int PunchRecover = 18;
        /// <summary>空泡爆缩延迟与半径（鬼奴缩档）</summary>
        private const int OrbDelay = 24;
        private const float OrbRadius = 108f;

        //尾扇齐射：弓身卷曲→甩尾放扇→回摆
        private const int CurlFrames = 22;
        private const int VolleyFire = 14;
        private const int VolleyRecover = 16;
        private const int BoltsPerVolley = 5;

        private const int DissolveTotal = 64;

        //==================== 骨架数据（各端本地重建）====================

        private readonly Vector2[] nodePos = new Vector2[NodeCount];
        /// <summary>节前向角（指向行进方向），贴图上方=前向、绘制加 PiOver2</summary>
        private readonly float[] nodeDir = new float[NodeCount];
        private readonly float[] wetness = new float[NodeCount];
        private readonly bool[] belowWater = new bool[NodeCount];

        private readonly TwoBoneIK[] arms = [
            new(SeaShrimpDirector.ArmBone1 * MiniScale, SeaShrimpDirector.ArmBone2 * MiniScale),
            new(SeaShrimpDirector.ArmBone1 * MiniScale, SeaShrimpDirector.ArmBone2 * MiniScale),
        ];
        private readonly TwoBoneSolve[] armSolves = new TwoBoneSolve[2];
        private readonly float[] clawRot = new float[2];
        private readonly float[] clawOpen = new float[2];

        private readonly ShrimpVerletStrand[] antennae = [new(5, 96f * MiniScale), new(5, 88f * MiniScale)];

        //双螯空间抓握（守位挪抓，骨架文法的迷你版）
        private readonly Vector2[] gripPos = new Vector2[2];
        private readonly Vector2[] gripFrom = new Vector2[2];
        private readonly Vector2[] gripTo = new Vector2[2];
        private readonly float[] gripT = [-1f, -1f];
        private readonly bool[] gripInit = new bool[2];
        private int gripTick;
        private const int GripCycle = 36;
        private const int GripLurch = 10;

        private bool built;
        private float wavePhase;
        /// <summary>尾扇张合 0..1（平滑）</summary>
        private float tailFlare = 0.35f;
        /// <summary>脊椎卷曲 -1..1（尾弹蓄力的 C 卷）</summary>
        private float spineCurl;

        //==================== 本地表现量（不入同步）====================

        private int attackCooldown;
        private int attackIndex;
        private int lastSeenState = -1;
        private bool launchDone;
        private bool punchImpulsed;
        private bool orbFired;
        private bool volleyFired;
        /// <summary>螯尖蓄光 0..1</summary>
        private float clawCharge;

        private Player Owner => Main.player[Projectile.owner];

        /// <summary>连续量抖动的确定性相位，各端一致（不掷 Main.rand）</summary>
        private float Seed => Projectile.identity * 0.7391f;

        //==================== 召唤入口 ====================

        /// <summary>KikasaServantIndex 登记的召唤委托；emergeAt.Y = 湖面（跃出点）</summary>
        internal static void Summon(Player owner, Vector2 emergeAt) {
            if (owner.whoAmI != Main.myPlayer) {
                return;
            }
            int damage = (int)owner.GetTotalDamage(DamageClass.Summon).ApplyTo(ClawDamage);
            float dir = MathF.Sign(owner.Center.X - emergeAt.X);
            if (dir == 0f) {
                dir = owner.direction;
            }
            //起点悬在湖面下，泡群预兆后跃出
            Projectile.NewProjectile(owner.GetSource_Misc("KikasaServant"),
                emergeAt + new Vector2(0f, 70f), Vector2.Zero,
                ModContent.ProjectileType<KikasaSeaShrimpServant>(), damage, 8f, owner.whoAmI,
                ai2: dir);
        }

        //==================== 定义 ====================

        public override void SetStaticDefaults() {
            Main.projPet[Projectile.type] = true;
            ProjectileID.Sets.MinionSacrificable[Projectile.type] = false;
            ProjectileID.Sets.DrawScreenCheckFluff[Projectile.type] = 900;
        }

        public override void SetDefaults() {
            Projectile.width = 90;
            Projectile.height = 66;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Summon;
            Projectile.minion = true;
            Projectile.minionSlots = 0f;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.netImportant = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 24;
            Projectile.timeLeft = 180;
        }

        public override bool MinionContactDamage() => true;

        /// <summary>接触伤害只开在出拳伸展窗，与可见的螯刺严格对齐</summary>
        public override bool? CanDamage()
            => State == StatePunch && Phase == 1 ? null : false;

        /// <summary>螯击命中：出拳臂的腕→螯尖线碰撞</summary>
        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            if (!built) {
                return false;
            }
            int arm = PunchArm;
            Vector2 wrist = armSolves[arm].Wrist;
            Vector2 tip = ClawTip(arm);
            float _ = 0f;
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(),
                wrist, tip + (tip - wrist).SafeNormalize(Vector2.UnitX) * 14f, 30f, ref _);
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
            //还没跃出就要收场：什么都没露出来，不演谢幕
            if (State == StateEmerge && StateTimer < OmenEnd) {
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

            if (authority && State != StateDissolve && !LakeHealthy(owner, domain)) {
                BeginDissolve();
            }

            Projectile.timeLeft = 180;
            Projectile.damage = (int)owner.GetTotalDamage(DamageClass.Summon).ApplyTo(ClawDamage);

            //换场清闩
            if (State != lastSeenState) {
                lastSeenState = State;
                punchImpulsed = false;
                orbFired = false;
                volleyFired = false;
                clawCharge = 0f;
            }

            if (!built) {
                RebuildSkeleton(Projectile.Center, -MathHelper.PiOver2);
            }

            StateTimer++;
            switch (State) {
                case StateEmerge: UpdateEmerge(owner, domain); break;
                case StateFollow: UpdateFollow(owner, authority); break;
                case StatePunch: UpdatePunch(owner, authority); break;
                case StateTailVolley: UpdateTailVolley(owner, authority); break;
                case StateDissolve: UpdateDissolve(domain, authority); break;
            }

            UpdateSkeleton(domain);
            UpdateDrips();
            clawCharge = MathF.Max(0f, clawCharge - 0.04f);
            if (attackCooldown > 0) {
                attackCooldown--;
            }

            //晶簇常燃微光（鬼奴变调：血里透一点冷）
            Lighting.AddLight(nodePos[0], 0.16f, 0.08f, 0.14f);
        }

        private static bool LakeHealthy(Player owner, KikasaDomainPlayer domain)
            => !owner.dead && domain.AnyActive
            && domain.Phase != KikasaDomainPhase.Closing
            && domain.RiseT >= 0.9f;

        //==================== 倒跃出水 ====================

        private void UpdateEmerge(Player owner, KikasaDomainPlayer domain) {
            float lakeY = domain.LakeWorldY;
            int t = (int)StateTimer;
            bool viewed = ViewedOwner;
            float dir = MathF.Sign(StateParam) == 0f ? 1f : MathF.Sign(StateParam);
            Vector2 surface = new(Projectile.Center.X, lakeY);

            if (t < OmenEnd) {
                //泡群预兆：一串血泡自深处升到水面破掉，涟漪渐密
                Projectile.velocity = Vector2.Zero;
                if (viewed) {
                    if (t % 4 == 1) {
                        KikasaDomainDeco.RippleAt(
                            surface + new Vector2(Main.rand.NextFloat(-20f, 20f), 0f),
                            0.25f + t / (float)OmenEnd * 0.4f);
                    }
                    if (!Main.dedServ && t % 3 == 0) {
                        PRTLoader.NewParticle<PRT_KikasaBloodGlob>(
                            surface + new Vector2(Main.rand.NextFloat(-18f, 18f), Main.rand.NextFloat(4f, 26f)),
                            new Vector2(0f, -Main.rand.NextFloat(1f, 2.2f)),
                            BloodMain * 0.5f, Main.rand.NextFloat(0.25f, 0.45f))?.Configure(Main.rand.Next(10, 18));
                    }
                    if (t == 9 || t == 22) {
                        SoundEngine.PlaySound(SoundID.Drip with {
                            Volume = 0.5f,
                            Pitch = t == 9 ? -0.55f : -0.2f,
                            MaxInstances = 2
                        }, surface);
                        ShakeViewer(t == 9 ? 0.7f : 1.1f);
                    }
                }
                return;
            }

            if (!launchDone) {
                //跃出拍：整虾破水而出，双螯张开亮相
                launchDone = true;
                Projectile.velocity = new Vector2(dir * 5f, -15f);
                tailFlare = 1f;
                for (int a = 0; a < 2; a++) {
                    clawOpen[a] = 1f;
                    arms[a].Impulse(new Vector2(dir * 3f, -6f));
                }
                SoundEngine.PlaySound(SoundID.SplashWeak with { Volume = 1f, Pitch = -0.2f, MaxInstances = 2 }, surface);
                SoundEngine.PlaySound(SoundID.NPCHit4 with { Volume = 0.5f, Pitch = 0.3f, MaxInstances = 2 }, surface);
                if (viewed) {
                    LeapBurst(surface);
                }
            }

            if (t <= LeapEnd) {
                //空中抛物线：跃出后被重量拽回，落回途中翻身（头随速度向转）
                Projectile.velocity.X *= 0.985f;
                Projectile.velocity.Y = MathF.Min(Projectile.velocity.Y + 0.32f, 12f);
                if (Projectile.velocity.LengthSquared() > 4f) {
                    nodeDir[0] = nodeDir[0].AngleTowards(Projectile.velocity.ToRotation(), 0.07f);
                }
                //触角甩尾冲量
                if (t % 8 == 0) {
                    for (int s = 0; s < 2; s++) {
                        antennae[s].Nudge(Projectile.velocity * 0.18f);
                    }
                }
            }
            else {
                //落定：悬到主人侧上方的守位
                Vector2 anchor = owner.Center + new Vector2(-owner.direction * 150f, -34f);
                Vector2 want = (anchor - Projectile.Center) * 0.07f;
                if (want.Length() > 10f) {
                    want = want.SafeNormalize(Vector2.Zero) * 10f;
                }
                Projectile.velocity = Vector2.Lerp(Projectile.velocity, want, 0.09f);
            }

            if (t >= EmergeTotal || t > EmergeTimeout) {
                State = StateFollow;
                StateTimer = 0;
                StateParam = 0;
                attackCooldown = 42;
                Projectile.netUpdate = Main.myPlayer == Projectile.owner;
            }
        }

        /// <summary>跃出浪冠：竖直水柱血珠 + 扩散环</summary>
        private void LeapBurst(Vector2 hit) {
            KikasaDomainDeco.RippleAt(hit, 2.2f);
            KikasaDomainDeco.SplashAt(hit, 12);
            for (int i = 0; i < 15; i++) {
                float angle = -MathHelper.PiOver2 + Main.rand.NextFloat(-0.6f, 0.6f);
                PRTLoader.NewParticle<PRT_KikasaBloodGlob>(
                    hit + new Vector2(Main.rand.NextFloat(-20f, 20f), -4f),
                    angle.ToRotationVector2() * Main.rand.NextFloat(3.5f, 9f),
                    Main.rand.NextBool(3) ? BloodDeep : BloodMain,
                    Main.rand.NextFloat(0.4f, 0.75f))?.Configure(Main.rand.Next(22, 36));
            }
            PRTLoader.NewParticle<PRT_DWave>(hit, Vector2.Zero, BloodDeep, 0.1f)
                ?.Configure(new Vector2(0.4f, 1f), -MathHelper.PiOver2, 0.4f, 11);
            ShakeViewer(4.5f);
        }

        //==================== 悬游跟随 ====================

        private void UpdateFollow(Player owner, bool authority) {
            //守位悬游：主人侧上方缓慢起伏，双螯抓着屏幕平面挪
            Vector2 anchor = owner.Center + new Vector2(-owner.direction * 150f, -34f);
            float w = (float)StateTimer * 0.03f + Seed;
            anchor += new Vector2(MathF.Sin(w) * 56f, MathF.Sin(w * 1.7f + Seed) * 26f);

            Vector2 to = anchor - Projectile.Center;
            if (to.Length() > 2400f) {
                Projectile.Center = anchor;
                Projectile.velocity = Vector2.Zero;
                RebuildSkeleton(anchor, owner.direction > 0 ? 0f : MathHelper.Pi);
                Projectile.netUpdate = authority;
                return;
            }
            float maxSpeed = to.Length() > 1200f ? 22f : 10f;
            Vector2 desired = to * 0.08f;
            if (desired.Length() > maxSpeed) {
                desired = desired.SafeNormalize(Vector2.Zero) * maxSpeed;
            }
            Projectile.velocity = Vector2.Lerp(Projectile.velocity, desired, 0.11f);

            //朝向：有猎物盯猎物，没有盯主人面朝的方向
            int target = FindTarget(owner);
            float wantHeading;
            if (target >= 0) {
                wantHeading = (Main.npc[target].Center - nodePos[0]).ToRotation();
            }
            else if (Projectile.velocity.Length() > 6f) {
                wantHeading = Projectile.velocity.ToRotation();
            }
            else {
                wantHeading = owner.direction > 0 ? 0f : MathHelper.Pi;
            }
            nodeDir[0] = nodeDir[0].AngleTowards(wantHeading, MathHelper.Pi / 40f);

            tailFlare = MathHelper.Lerp(tailFlare, 0.35f + 0.15f * MathF.Sin(w * 2.3f), 0.1f);
            spineCurl = MathHelper.Lerp(spineCurl, 0f, 0.12f);

            //出手裁决：空泡拳与尾扇齐射交替，owner 盖章
            if (target >= 0 && attackCooldown <= 0 && StateTimer > 40) {
                attackIndex++;
                int nextState = attackIndex % 2 == 1 ? StatePunch : StateTailVolley;
                //出拳侧按目标在体轴哪一边挑，打包进 StateParam 随包走
                int arm = 0;
                if (nextState == StatePunch) {
                    Vector2 lat = Lateral(0);
                    arm = Vector2.Dot(Main.npc[target].Center - nodePos[0], lat) >= 0f ? 0 : 1;
                }
                State = nextState;
                StateTimer = 0;
                StateParam = arm << 3;
                Projectile.netUpdate = authority;
            }
        }

        //==================== 空泡拳 ====================

        private void UpdatePunch(Player owner, bool authority) {
            int t = (int)StateTimer;
            int phase = Phase;
            int arm = PunchArm;
            int target = FindTarget(owner);
            Vector2 aimPos = target >= 0
                ? Main.npc[target].Center + Main.npc[target].velocity * 6f
                : nodePos[0] + nodeDir[0].ToRotationVector2() * 400f;
            Vector2 aimDir = (aimPos - nodePos[0]).SafeNormalize(Vector2.UnitX);

            void NextPhase(int next) {
                StateParam = next | arm << 3;
                StateTimer = 0;
                Projectile.netUpdate = authority;
            }

            if (phase == 0) {
                //对准收臂：头转向猎物，出拳螯后收张钳，螯尖聚血光
                if (target < 0) {
                    EndAttack(authority, 45);
                    return;
                }
                nodeDir[0] = nodeDir[0].AngleTowards(aimDir.ToRotation(), 0.12f);
                Projectile.velocity *= 0.9f;
                clawCharge = MathHelper.Clamp(t / (float)PunchAim, 0f, 1f);

                if (t == 2) {
                    SoundEngine.PlaySound(SoundID.NPCHit4 with { Volume = 0.4f, Pitch = -0.2f, MaxInstances = 2 }, nodePos[0]);
                }
                //蓄势血珠向螯尖汇聚，75% 后静默
                if (!Main.dedServ && t < PunchAim * 0.75f && t % 3 == 0) {
                    Vector2 tip = ClawTip(arm);
                    Vector2 from = tip + Main.rand.NextVector2Unit() * Main.rand.NextFloat(30f, 70f);
                    PRTLoader.NewParticle<PRT_KikasaBloodGlob>(from, (tip - from) * 0.14f,
                        BloodMain * 0.55f, Main.rand.NextFloat(0.25f, 0.45f))?.Configure(9);
                }
                if (t >= PunchAim) {
                    NextPhase(1);
                }
                return;
            }

            if (phase == 1) {
                //出拳：腕弹簧一记冲量弹出，伸展帧打出空泡
                Projectile.velocity *= 0.92f;
                if (!punchImpulsed) {
                    punchImpulsed = true;
                    arms[arm].Impulse(aimDir * 30f);
                    SoundEngine.PlaySound(SoundID.Item71 with { Volume = 0.6f, Pitch = -0.1f, MaxInstances = 2 }, nodePos[0]);
                    if (ViewedOwner) {
                        ShakeViewer(2.5f);
                    }
                }
                if (t == 4 && !orbFired) {
                    orbFired = true;
                    Vector2 tip = ClawTip(arm);
                    SoundEngine.PlaySound(SoundID.Item85 with { Volume = 0.55f, Pitch = 0.2f, MaxInstances = 2 }, tip);
                    if (!Main.dedServ) {
                        //出拳水花锥
                        for (int i = 0; i < 7; i++) {
                            PRTLoader.NewParticle<PRT_KikasaBloodGlob>(tip + Main.rand.NextVector2Circular(6f, 6f),
                                aimDir.RotatedByRandom(0.5f) * Main.rand.NextFloat(2.5f, 7f),
                                BloodMain, Main.rand.NextFloat(0.35f, 0.6f))?.Configure(Main.rand.Next(12, 22));
                        }
                    }
                    if (authority) {
                        int damage = (int)owner.GetTotalDamage(DamageClass.Summon).ApplyTo(OrbDamage);
                        Projectile.NewProjectile(Projectile.GetSource_FromAI(), tip + aimDir * 8f,
                            aimDir * 6.5f, ModContent.ProjectileType<KikasaCavitationOrb>(),
                            damage, 4f, Projectile.owner, OrbDelay, OrbRadius);
                    }
                }
                if (t >= PunchStrike) {
                    NextPhase(2);
                }
                return;
            }

            //回守
            Projectile.velocity *= 0.94f;
            if (t >= PunchRecover) {
                EndAttack(authority, 110);
            }
        }

        //==================== 尾扇齐射 ====================

        private void UpdateTailVolley(Player owner, bool authority) {
            int t = (int)StateTimer;
            int phase = Phase;
            int target = FindTarget(owner);
            Vector2 aimPos = target >= 0
                ? Main.npc[target].Center + Main.npc[target].velocity * 7f
                : nodePos[0] - nodeDir[0].ToRotationVector2() * 400f;

            void NextPhase(int next) {
                StateParam = next;
                StateTimer = 0;
                Projectile.netUpdate = authority;
            }

            if (phase == 0) {
                //弓身卷曲：头转开、尾扇对准猎物，脊椎卷成 C
                if (target < 0) {
                    EndAttack(authority, 45);
                    return;
                }
                float wantHeading = (nodePos[0] - aimPos).ToRotation();
                nodeDir[0] = nodeDir[0].AngleTowards(wantHeading, 0.1f);
                Projectile.velocity *= 0.9f;
                float progress = MathHelper.Clamp(t / (float)CurlFrames, 0f, 1f);
                spineCurl = progress * 0.9f;
                tailFlare = MathHelper.Lerp(tailFlare, 1f, 0.14f);

                if (t == 2) {
                    SoundEngine.PlaySound(SoundID.Item95 with { Volume = 0.35f, Pitch = -0.6f, MaxInstances = 2 }, nodePos[NodeCount - 1]);
                }
                if (!Main.dedServ && t % 4 == 1) {
                    PRTLoader.NewParticle<PRT_KikasaBloodGlob>(
                        nodePos[NodeCount - 1] + Main.rand.NextVector2Circular(12f, 12f),
                        new Vector2(0f, -Main.rand.NextFloat(0.5f, 1.4f)),
                        BloodMain * 0.5f, Main.rand.NextFloat(0.25f, 0.45f))?.Configure(Main.rand.Next(10, 18));
                }
                if (t >= CurlFrames) {
                    NextPhase(1);
                }
                return;
            }

            if (phase == 1) {
                //甩尾放扇：尾扇一记鞭甩，扇形水弹出膛，反冲把身体向前推
                if (t == 2 && !volleyFired) {
                    volleyFired = true;
                    Vector2 tail = nodePos[NodeCount - 1];
                    Vector2 aimDir = (aimPos - tail).SafeNormalize(Vector2.UnitX);
                    tailFlare = 0.1f;
                    spineCurl = -0.3f;
                    Projectile.velocity += nodeDir[0].ToRotationVector2() * 7f;
                    Projectile.netUpdate = authority;

                    SoundEngine.PlaySound(SoundID.Item95 with { Volume = 0.6f, Pitch = -0.15f, MaxInstances = 3 }, tail);
                    SoundEngine.PlaySound(SoundID.SplashWeak with { Volume = 0.6f, Pitch = 0.1f, MaxInstances = 3 }, tail);
                    if (ViewedOwner) {
                        ShakeViewer(2f);
                    }
                    if (!Main.dedServ) {
                        for (int i = 0; i < 8; i++) {
                            PRTLoader.NewParticle<PRT_KikasaBloodGlob>(tail + Main.rand.NextVector2Circular(8f, 8f),
                                aimDir.RotatedByRandom(0.6f) * Main.rand.NextFloat(2f, 6f),
                                BloodMain, Main.rand.NextFloat(0.3f, 0.55f))?.Configure(Main.rand.Next(12, 22));
                        }
                        PRTLoader.NewParticle<PRT_DWave>(tail + aimDir * 10f, Vector2.Zero,
                            BloodDeep, 0.08f)?.Configure(new Vector2(0.55f, 1f), aimDir.ToRotation(), 0.26f, 9);
                    }
                    for (int s = 0; s < 2; s++) {
                        antennae[s].Nudge(-aimDir * 4f);
                    }
                    if (Main.myPlayer == Projectile.owner) {
                        int damage = (int)owner.GetTotalDamage(DamageClass.Summon).ApplyTo(BoltDamage);
                        for (int k = 0; k < BoltsPerVolley; k++) {
                            float off = (k - BoltsPerVolley / 2) * 0.24f + Main.rand.NextFloat(-0.03f, 0.03f);
                            Vector2 vel = aimDir.RotatedBy(off) * 12f;
                            Projectile.NewProjectile(Projectile.GetSource_FromAI(), tail, vel,
                                ModContent.ProjectileType<KikasaShrimpWaterBolt>(), damage, 2f, Projectile.owner);
                        }
                    }
                }
                if (t >= VolleyFire) {
                    NextPhase(2);
                }
                return;
            }

            //回摆
            Projectile.velocity *= 0.93f;
            spineCurl = MathHelper.Lerp(spineCurl, 0f, 0.14f);
            if (t >= VolleyRecover) {
                EndAttack(authority, 100);
            }
        }

        private void EndAttack(bool authority, int cooldown) {
            State = StateFollow;
            StateTimer = 0;
            StateParam = 0;
            attackCooldown = cooldown;
            Projectile.netUpdate = authority;
        }

        //==================== 溶解遣返 ====================

        private void UpdateDissolve(KikasaDomainPlayer domain, bool authority) {
            int t = (int)StateTimer;
            bool lakeAlive = domain.AnyActive && domain.RiseT > 0.5f;

            if (lakeAlive) {
                Projectile.velocity.X *= 0.94f;
                Projectile.velocity.Y = MathF.Min(Projectile.velocity.Y + 0.22f, 7f);
            }
            else {
                Projectile.velocity *= 0.9f;
            }
            //爪与尾先松：溶解期钳口滑开、尾扇收拢
            clawOpen[0] = MathHelper.Lerp(clawOpen[0], 0.6f, 0.06f);
            clawOpen[1] = MathHelper.Lerp(clawOpen[1], 0.6f, 0.06f);
            tailFlare = MathHelper.Lerp(tailFlare, 0.1f, 0.06f);

            if (!Main.dedServ && t % 3 == 0) {
                int i = Main.rand.Next(NodeCount);
                float dissolve = PartDissolve(i);
                if (dissolve is > 0.05f and < 0.9f) {
                    PRTLoader.NewParticle<PRT_KikasaBloodGlob>(
                        nodePos[i] + Main.rand.NextVector2Circular(16f, 12f),
                        new Vector2(Main.rand.NextFloat(-0.6f, 0.6f), Main.rand.NextFloat(1.2f, 2.4f)),
                        BloodMain * 0.5f, Main.rand.NextFloat(0.3f, 0.55f))
                        ?.Configure(Main.rand.Next(14, 24));
                }
            }

            if (authority && t >= DissolveTotal) {
                Projectile.Kill();
            }
            else if (!authority && t >= DissolveTotal + 10) {
                Projectile.Kill();
            }
        }

        /// <summary>逐部件溶解进度：尾先化、头最后（部件序=节序，臂随头）</summary>
        private float PartDissolve(int nodeIndex) {
            if (State != StateDissolve) {
                return 0f;
            }
            float start = (NodeCount - 1 - nodeIndex) * 5f;
            return MathHelper.Clamp((StateTimer - start) / 30f, 0f, 1f);
        }

        //==================== 骨架推进 ====================

        /// <summary>硬重建：沿朝向反向铺直脊链，臂/触角归位</summary>
        private void RebuildSkeleton(Vector2 headPos, float heading) {
            built = true;
            nodePos[0] = headPos;
            nodeDir[0] = heading;
            for (int i = 1; i < NodeCount; i++) {
                nodeDir[i] = heading;
                nodePos[i] = nodePos[i - 1] - heading.ToRotationVector2()
                    * (SeaShrimpDirector.SpineGaps[i - 1] * MiniScale);
            }
            float lakeY = Owner?.active == true && Owner.TryGetModPlayer(out KikasaDomainPlayer domain)
                ? domain.LakeWorldY : float.MaxValue;
            for (int i = 0; i < NodeCount; i++) {
                belowWater[i] = nodePos[i].Y >= lakeY;
                wetness[i] = 1f;
            }
            for (int a = 0; a < 2; a++) {
                Vector2 want = GuardWristWant(a);
                arms[a].Snap(want);
                armSolves[a] = arms[a].Solve(ShoulderWorld(a), want,
                    SeaShrimpDirector.ArmSpring, SeaShrimpDirector.ArmDamping, a == 0 ? 1f : -1f);
                clawRot[a] = nodeDir[0];
                gripInit[a] = false;
                gripT[a] = -1f;
            }
            for (int s = 0; s < 2; s++) {
                antennae[s].WarmStart(AntennaAnchor(s), AntennaRestDir(s));
            }
        }

        private void UpdateSkeleton(KikasaDomainPlayer domain) {
            //本帧渲染位 = Center + velocity（AI 在位移积分前跑）
            Vector2 head = Projectile.Center + Projectile.velocity;
            if (Vector2.Distance(nodePos[0], head) > 300f) {
                RebuildSkeleton(head, Projectile.velocity.LengthSquared() > 1f
                    ? Projectile.velocity.ToRotation() : nodeDir[0]);
                return;
            }
            nodePos[0] = head;

            float speed = Projectile.velocity.Length();
            wavePhase += speed * 0.05f;
            SolveSpine(speed);
            SolveArms();
            for (int s = 0; s < 2; s++) {
                antennae[s].Update(AntennaAnchor(s), AntennaRestDir(s),
                    Main.GlobalTimeWrappedHourly, Seed + s * 2.61f, true);
            }
            UpdateNodeCrossings(domain);
        }

        /// <summary>脊链求解：位置跟随与姿态期望（卷曲/游波）按卷曲强度混合，弯角硬钳制</summary>
        private void SolveSpine(float speed) {
            float curl = MathHelper.Clamp(spineCurl, -1f, 1f);
            float poseWeight = 0.3f + 0.6f * MathF.Min(1f, MathF.Abs(curl) * 1.6f);
            float speedFactor = MathHelper.Clamp(speed / 9f, 0.15f, 1.4f);

            for (int i = 1; i < NodeCount; i++) {
                Vector2 front = nodePos[i - 1];
                float frontDir = nodeDir[i - 1];
                float gap = SeaShrimpDirector.SpineGaps[i - 1] * MiniScale;

                Vector2 toFront = front - nodePos[i];
                float natural = toFront.LengthSquared() < 0.01f ? frontDir : toFront.ToRotation();

                float curlOff = curl * SeaShrimpDirector.CurlPerJoint;
                float waveOff = MathF.Sin(wavePhase - i * SeaShrimpDirector.CrawlWaveStep)
                    * SeaShrimpDirector.CrawlWaveAmp * speedFactor;
                float posed = frontDir + curlOff + waveOff;

                float blended = natural + MathHelper.WrapAngle(posed - natural) * poseWeight;
                float rel = MathHelper.Clamp(MathHelper.WrapAngle(blended - frontDir),
                    -SeaShrimpDirector.SpineMaxBend, SeaShrimpDirector.SpineMaxBend);
                float wantDir = frontDir + rel;

                nodeDir[i] = nodeDir[i].AngleLerp(wantDir, SeaShrimpDirector.SpineTurnRate);
                nodePos[i] = front - nodeDir[i].ToRotationVector2() * gap;
            }
        }

        /// <summary>
        /// 双螯求解：守位=空间抓握（迷你版骨架文法，双手错半拍抓屏幕平面）；
        /// 出拳臂按相位走收臂→弹出→回守；螯体姿态抓握指抓点、出招沿前臂
        /// </summary>
        private void SolveArms() {
            gripTick = (gripTick + 1) % GripCycle;
            bool punching = State == StatePunch;
            int punchArm = PunchArm;
            int phase = Phase;

            for (int a = 0; a < 2; a++) {
                Vector2 shoulder = ShoulderWorld(a);
                bool thisPunching = punching && a == punchArm;
                Vector2 want;
                float open;
                float spring = SeaShrimpDirector.ArmSpring;
                float damping = SeaShrimpDirector.ArmDamping;
                Vector2 aimDir = nodeDir[0].ToRotationVector2();

                if (thisPunching && phase == 0) {
                    //收臂蓄势：腕缩到肩后侧，钳口大张
                    want = shoulder - aimDir * (26f * MiniScale) + Lateral(a) * (14f * MiniScale);
                    open = 0.9f;
                    spring = 0.3f;
                    gripInit[a] = false;
                }
                else if (thisPunching && phase == 1) {
                    //出拳：腕目标推到极限伸展，弹簧冲量在状态里给
                    want = shoulder + aimDir * (SeaShrimpDirector.PunchReach * 0.62f * MiniScale);
                    open = 0.05f;
                    spring = 0.34f;
                    damping = 0.8f;
                    gripInit[a] = false;
                }
                else if (thisPunching) {
                    //回守：先松后归
                    want = GuardWristWant(a);
                    open = 0.3f;
                    gripInit[a] = false;
                }
                else {
                    //守位抓握
                    want = UpdateGrip(a, shoulder, out bool lurching);
                    open = lurching ? 0.8f : 0.1f;
                    spring = 0.3f;
                    damping = 0.74f;
                }

                armSolves[a] = arms[a].Solve(shoulder, want, spring, damping, a == 0 ? 1f : -1f);

                float wantRot;
                if (!thisPunching && gripInit[a] && gripT[a] < 0f) {
                    wantRot = (gripPos[a] - armSolves[a].Wrist).SafeNormalize(aimDir).ToRotation();
                }
                else {
                    wantRot = armSolves[a].ForeDir.ToRotation();
                }
                clawRot[a] = clawRot[a].AngleLerp(wantRot, 0.24f);
                clawOpen[a] = MathHelper.Lerp(clawOpen[a], MathHelper.Clamp(open, 0f, 1f), 0.24f);
            }
        }

        /// <summary>守位抓握推进：节拍帧挪抓、其余时间抓点钉死（骨架 UpdateGrip 的迷你版）</summary>
        private Vector2 UpdateGrip(int armIndex, Vector2 shoulder, out bool lurching) {
            if (!gripInit[armIndex]) {
                gripInit[armIndex] = true;
                gripPos[armIndex] = RestGrip(armIndex);
                gripT[armIndex] = -1f;
            }

            int myBeat = armIndex * (GripCycle / 2);
            if (gripTick == myBeat && gripT[armIndex] < 0f) {
                gripFrom[armIndex] = gripPos[armIndex];
                gripTo[armIndex] = RestGrip(armIndex);
                if (Vector2.DistanceSquared(gripFrom[armIndex], gripTo[armIndex]) > 16f * 16f) {
                    gripT[armIndex] = 0f;
                }
            }

            if (gripT[armIndex] >= 0f) {
                gripT[armIndex] += 1f / GripLurch;
                if (gripT[armIndex] >= 1f) {
                    gripT[armIndex] = -1f;
                    gripPos[armIndex] = gripTo[armIndex];
                }
                else {
                    float t = gripT[armIndex];
                    float ease = t * t * (3f - 2f * t);
                    gripPos[armIndex] = Vector2.Lerp(gripFrom[armIndex], gripTo[armIndex], ease);
                }
                lurching = gripT[armIndex] >= 0f;
            }
            else {
                lurching = false;
            }

            //抓点失效：被甩超臂展一截或落到头后侧，立刻换抓新位
            float maxReach = ArmBone1 + ArmBone2;
            bool tooFar = Vector2.Distance(gripPos[armIndex], shoulder) > maxReach * 1.15f;
            bool behindHead = Vector2.Dot(gripPos[armIndex] - nodePos[0], nodeDir[0].ToRotationVector2()) < 8f;
            if (tooFar || behindHead) {
                gripPos[armIndex] = RestGrip(armIndex);
                gripT[armIndex] = -1f;
            }

            Vector2 toGrip = (gripPos[armIndex] - shoulder).SafeNormalize(nodeDir[0].ToRotationVector2());
            return gripPos[armIndex] - toGrip * (50f * MiniScale);
        }

        /// <summary>本臂的休息抓点：头前两侧，带确定性微偏</summary>
        private Vector2 RestGrip(int armIndex) {
            Vector2 forward = nodeDir[0].ToRotationVector2();
            float wob = MathF.Sin(Seed * 3.1f + armIndex * 2.7f + wavePhase * 0.5f) * 8f;
            return nodePos[0] + forward * ((100f + wob) * MiniScale)
                + Lateral(armIndex) * (96f * MiniScale);
        }

        /// <summary>抓握不可用时的收拢腕点：折叠在头前两侧 + 呼吸微摆</summary>
        private Vector2 GuardWristWant(int armIndex) {
            Vector2 forward = nodeDir[0].ToRotationVector2();
            float breathe = MathF.Sin(Main.GlobalTimeWrappedHourly * 1.4f + Seed + armIndex * 2.3f) * 5f;
            return ShoulderWorld(armIndex)
                + forward * (50f * MiniScale)
                + Lateral(armIndex) * ((20f + breathe) * MiniScale);
        }

        /// <summary>臂的体侧方向：0=前向顺转 90°，1=逆转</summary>
        private Vector2 Lateral(int armIndex)
            => nodeDir[0].ToRotationVector2().RotatedBy(MathHelper.PiOver2 * (armIndex == 0 ? 1f : -1f));

        /// <summary>肩锚：头前部两侧对称</summary>
        private Vector2 ShoulderWorld(int armIndex) {
            Vector2 forward = nodeDir[0].ToRotationVector2();
            return nodePos[0] + forward * (SeaShrimpDirector.ShoulderForward * MiniScale)
                + Lateral(armIndex) * (SeaShrimpDirector.ShoulderSide * MiniScale);
        }

        /// <summary>螯尖世界位（掌根锚→钳口 ≈70px×缩放）</summary>
        private Vector2 ClawTip(int armIndex)
            => armSolves[armIndex].Wrist + clawRot[armIndex].ToRotationVector2() * (70f * MiniScale);

        private Vector2 AntennaAnchor(int side) {
            Vector2 forward = nodeDir[0].ToRotationVector2();
            return nodePos[0] + forward * (78f * MiniScale)
                + Lateral(side) * ((side == 0 ? 7f : 8f) * MiniScale);
        }

        private Vector2 AntennaRestDir(int side) {
            Vector2 forward = nodeDir[0].ToRotationVector2();
            return Vector2.Normalize(forward + Lateral(side) * 0.55f);
        }

        /// <summary>逐节过水线（双向）：水花帧内限量；出水节湿度拉满</summary>
        private void UpdateNodeCrossings(KikasaDomainPlayer domain) {
            float lakeY = domain.LakeWorldY;
            bool lakeAlive = domain.AnyActive && domain.RiseT > 0.5f;
            bool viewed = ViewedOwner;
            int fxBudget = 2;

            for (int i = 0; i < NodeCount; i++) {
                bool below = nodePos[i].Y >= lakeY;
                if (below != belowWater[i]) {
                    belowWater[i] = below;
                    wetness[i] = 1f;
                    if (lakeAlive && viewed && fxBudget > 0) {
                        fxBudget--;
                        Vector2 hit = new(nodePos[i].X, lakeY);
                        KikasaDomainDeco.RippleAt(hit, i == 0 ? 0.8f : 0.45f);
                        for (int k = 0; k < 3; k++) {
                            PRTLoader.NewParticle<PRT_KikasaBloodGlob>(
                                hit + new Vector2(Main.rand.NextFloat(-12f, 12f), -3f),
                                new Vector2(Main.rand.NextFloat(-1.2f, 1.2f), -Main.rand.NextFloat(2f, 4f)),
                                BloodMain * 0.6f, Main.rand.NextFloat(0.3f, 0.55f))
                                ?.Configure(Main.rand.Next(14, 24));
                        }
                    }
                }
                wetness[i] = below ? 1f : MathF.Max(0f, wetness[i] - 0.012f);
            }
        }

        /// <summary>湿度驱动滴落：壳缝淌血珠</summary>
        private void UpdateDrips() {
            if (Main.dedServ) {
                return;
            }
            int budget = 2;
            for (int k = 0; k < 3 && budget > 0; k++) {
                int i = Main.rand.Next(NodeCount);
                if (belowWater[i] || wetness[i] < 0.1f) {
                    continue;
                }
                if (Main.rand.NextFloat() > wetness[i] * 0.4f) {
                    continue;
                }
                budget--;
                PRTLoader.NewParticle<PRT_KikasaBloodGlob>(
                    nodePos[i] + Main.rand.NextVector2Circular(20f, 14f),
                    new Vector2(Projectile.velocity.X * 0.05f, Main.rand.NextFloat(0.8f, 1.8f)),
                    (Main.rand.NextBool(3) ? BloodDeep : BloodMain) * Main.rand.NextFloat(0.45f, 0.6f),
                    Main.rand.NextFloat(0.3f, 0.55f))?.Configure(Main.rand.Next(16, 28), 0.3f);
            }
        }

        //==================== 公共小件 ====================

        private int FindTarget(Player owner) {
            if (owner.HasMinionAttackTargetNPC) {
                NPC picked = Main.npc[owner.MinionAttackTargetNPC];
                if (picked.CanBeChasedBy(Projectile)
                    && Vector2.Distance(picked.Center, owner.Center) < 1600f) {
                    return picked.whoAmI;
                }
            }
            int best = -1;
            float bestDist = 1100f;
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

        internal bool ViewedOwner
            => KikasaDomain.Viewed != null && KikasaDomain.Viewed.Player.whoAmI == Projectile.owner;

        private static void ShakeViewer(float amount)
            => Main.LocalPlayer?.CWR()?.GetScreenShake(amount);

        //==================== 血系配色（CoolTint 家族，蔷薇晶做次要点缀）====================

        internal static Color BloodDark => KikasaDomain.CoolTint(new(64, 12, 14), new(38, 48, 52));
        internal static Color BloodDeep => KikasaDomain.CoolTint(new(140, 32, 30), new(84, 104, 110));
        internal static Color BloodMain => KikasaDomain.CoolTint(new(237, 77, 69), new(126, 158, 164));
        internal static Color BloodBright => KikasaDomain.CoolTint(new(246, 133, 112), new(176, 200, 204));
        /// <summary>蔷薇晶：boss 晶蓝在血湖里的变调，只给晶簇部位</summary>
        internal static Color GhostCrystal => KikasaDomain.CoolTint(new(224, 110, 146), new(140, 160, 200));

        //==================== 绘制 ====================

        public override bool PreDraw(ref Color lightColor) {
            if (!built || HeadTex == null) {
                return false;
            }
            SpriteBatch sb = Main.spriteBatch;

            //远侧装饰层：远触角、远排足（暗一档压后）
            DrawAntenna(sb, 1, 0.6f);
            DrawLegRow(sb, lightColor, row: 1, dark: 0.5f);

            //体链：血湖材质逐部件（尾→头）
            DrawSpineParts(sb, lightColor);

            //近侧装饰层
            DrawLegRow(sb, lightColor, row: 0, dark: 0.85f);
            DrawAntenna(sb, 0, 0.95f);

            //双螯压最上层
            DrawArms(sb, lightColor);

            //辉光层：晶簇蔷薇光 / 湿面反光 / 螯尖蓄光
            DrawGlowLayer(sb);

            return false;
        }

        /// <summary>ItemForm 着色器上参并画一发（血水衣公共路径）</summary>
        private void DrawFormPart(SpriteBatch sb, Effect form, bool shaderOk, Texture2D tex,
            Vector2 worldPos, float rotation, Vector2 origin, Vector2 scale, SpriteEffects fx,
            float wet, float dissolve, int seedIdx, Color fallback) {
            if (tex == null || dissolve >= 1f) {
                return;
            }
            Color color;
            if (shaderOk) {
                float segForm = MathHelper.Clamp(0.30f + wet * 0.16f
                    + MathF.Sin(Main.GlobalTimeWrappedHourly * 2.6f + Seed + seedIdx * 0.8f) * 0.04f, 0f, 0.6f);
                if (State == StateEmerge) {
                    float condense = MathHelper.Clamp(((int)StateTimer - OmenEnd - seedIdx * 2f) / 34f, 0f, 1f);
                    segForm = MathHelper.Lerp(0.9f, segForm, condense * condense * (3f - 2f * condense));
                }
                form.Parameters["uSeed"]?.SetValue(Seed + seedIdx * 1.7f);
                form.Parameters["uForm"]?.SetValue(segForm);
                form.Parameters["uDissolve"]?.SetValue(dissolve);
                form.Parameters["uUvRect"]?.SetValue(new Vector4(0f, 0f, 1f, 1f));
                form.Parameters["uTexel"]?.SetValue(new Vector2(1f / tex.Width, 1f / tex.Height));
                form.Parameters["uAspect"]?.SetValue(tex.Width / (float)tex.Height);
                form.CurrentTechnique.Passes[0].Apply();
                color = Color.White;
            }
            else {
                color = fallback * (1f - dissolve);
            }
            sb.Draw(tex, worldPos - Main.screenPosition, null, color, rotation, origin, scale, fx, 0f);
        }

        /// <summary>体链部件：贴图上方=前向，绘制加 PiOver2；尾扇前缘锚咬进体节3</summary>
        private void DrawSpineParts(SpriteBatch sb, Color lightColor) {
            Effect form = EffectLoader.KikasaItemForm?.Value;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            bool shaderOk = form != null && noise != null;
            Color fallback = Color.Lerp(lightColor, BloodMain, 0.5f);

            sb.End();
            sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.PointClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
            if (shaderOk) {
                Main.instance.GraphicsDevice.Textures[1] = noise;
                Main.instance.GraphicsDevice.SamplerStates[1] = SamplerState.LinearWrap;
                form.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
                form.Parameters["uScanMode"]?.SetValue(0f);
            }

            //尾扇：前缘锚 + 张合横缩
            Texture2D tail = TailTex?.Value;
            if (tail != null) {
                DrawFormPart(sb, form, shaderOk, tail, nodePos[4], nodeDir[4] + MathHelper.PiOver2,
                    new Vector2(83f, 16f), new Vector2((0.72f + 0.42f * tailFlare) * MiniScale, MiniScale),
                    SpriteEffects.None, wetness[4], PartDissolve(4), 4, fallback);
            }
            DrawFormPart(sb, form, shaderOk, Seg3Tex?.Value, nodePos[3], nodeDir[3] + MathHelper.PiOver2,
                (Seg3Tex?.Value?.Size() ?? Vector2.One) * 0.5f, new Vector2(MiniScale), SpriteEffects.None,
                wetness[3], PartDissolve(3), 3, fallback);
            DrawFormPart(sb, form, shaderOk, Seg2Tex?.Value, nodePos[2], nodeDir[2] + MathHelper.PiOver2,
                (Seg2Tex?.Value?.Size() ?? Vector2.One) * 0.5f, new Vector2(MiniScale), SpriteEffects.None,
                wetness[2], PartDissolve(2), 2, fallback);
            DrawFormPart(sb, form, shaderOk, Seg1Tex?.Value, nodePos[1], nodeDir[1] + MathHelper.PiOver2,
                (Seg1Tex?.Value?.Size() ?? Vector2.One) * 0.5f, new Vector2(MiniScale), SpriteEffects.None,
                wetness[1], PartDissolve(1), 1, fallback);
            DrawFormPart(sb, form, shaderOk, HeadTex?.Value, nodePos[0], nodeDir[0] + MathHelper.PiOver2,
                (HeadTex?.Value?.Size() ?? Vector2.One) * 0.5f, new Vector2(MiniScale), SpriteEffects.None,
                wetness[0], PartDissolve(0), 0, fallback);

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
        }

        /// <summary>
        /// 双螯：臂节1（肩→肘）→臂节2（肘→腕）→螯体（腕锚承窝）。
        /// 近侧臂（0）整条水平镜像：镜像锚点（w-x）、镜像轴角（π-axis）、开合反号，
        /// 双钳对称咬向前方中线（与 boss 渲染器同裁决）
        /// </summary>
        private void DrawArms(SpriteBatch sb, Color lightColor) {
            Effect form = EffectLoader.KikasaItemForm?.Value;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            bool shaderOk = form != null && noise != null;
            Texture2D arm1 = Arm1Tex?.Value;
            Texture2D arm2 = Arm2Tex?.Value;
            Texture2D claw = ClawTex?.Value;
            if (arm1 == null || arm2 == null || claw == null) {
                return;
            }
            float headDissolve = PartDissolve(0);
            if (headDissolve >= 1f) {
                return;
            }

            sb.End();
            sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.PointClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
            if (shaderOk) {
                Main.instance.GraphicsDevice.Textures[1] = noise;
                Main.instance.GraphicsDevice.SamplerStates[1] = SamplerState.LinearWrap;
                form.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
                form.Parameters["uScanMode"]?.SetValue(0f);
            }

            //远螯（1）先画，近螯（0）压上
            for (int a = 1; a >= 0; a--) {
                TwoBoneSolve solve = armSolves[a];
                bool mirror = a == 0;
                SpriteEffects fx = mirror ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
                float dark = a == 0 ? 1f : 0.72f;
                Color fallback = Color.Lerp(lightColor, BloodMain, 0.5f)
                    .MultiplyRGB(new Color(dark, dark, dark));

                DrawFormPart(sb, form, shaderOk, arm1, solve.Shoulder,
                    solve.UpperDir.ToRotation() - MathHelper.PiOver2, Arm1Anchor,
                    new Vector2(MiniScale, ArmBone1 / Arm1AxisLen), fx,
                    wetness[0], headDissolve, 5 + a, fallback);
                DrawFormPart(sb, form, shaderOk, arm2, solve.Elbow,
                    solve.ForeDir.ToRotation() - MathHelper.PiOver2, Arm2Anchor,
                    new Vector2(MiniScale, ArmBone2 / Arm2AxisLen), fx,
                    wetness[0], headDissolve, 7 + a, fallback);

                float texAxis = mirror ? MathHelper.Pi - ClawTexAxis : ClawTexAxis;
                Vector2 anchor = mirror ? new Vector2(claw.Width - ClawAnchor.X, ClawAnchor.Y) : ClawAnchor;
                float open = clawOpen[a] * 0.34f * (mirror ? -1f : 1f);
                DrawFormPart(sb, form, shaderOk, claw, solve.Wrist,
                    clawRot[a] - texAxis + open, anchor, new Vector2(MiniScale), fx,
                    wetness[0], headDissolve, 9 + a, fallback);
            }

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
        }

        /// <summary>一排划桨足：髋锚在体节腹侧，足端正弦划水（表现层，无步态钉地）</summary>
        private void DrawLegRow(SpriteBatch sb, Color lightColor, int row, float dark) {
            float bodyDissolve = PartDissolve(1);
            if (bodyDissolve >= 1f) {
                return;
            }
            Color darkMul = new(dark, dark, dark);
            for (int station = 0; station < 3; station++) {
                Texture2D tex = station switch {
                    0 => Leg1Tex?.Value,
                    1 => Leg2Tex?.Value,
                    _ => Leg3Tex?.Value,
                };
                if (tex == null) {
                    continue;
                }
                //髋：节腹侧；足：向腹划桨（行相沿站位与排别错开）
                Vector2 node = nodePos[station];
                float dir = nodeDir[station];
                Vector2 down = dir.ToRotationVector2().RotatedBy(MathHelper.PiOver2);
                Vector2 hip = node + down * (12f * MiniScale)
                    - dir.ToRotationVector2() * (station * 6f * MiniScale);
                float paddle = MathF.Sin(Main.GlobalTimeWrappedHourly * 5.2f
                    + station * 0.9f + row * MathHelper.Pi * 0.5f + Seed);
                Vector2 footDir = down.RotatedBy(paddle * 0.45f - 0.15f);
                Vector2 foot = hip + footDir * (32f * MiniScale);

                Vector2 hipPx = LegHip[station];
                Vector2 tipPx = LegTip[station];
                Vector2 axisPx = tipPx - hipPx;
                float axisLen = axisPx.Length();
                float axisAngle = axisPx.ToRotation();
                Vector2 toFoot = foot - hip;
                float dist = toFoot.Length();
                if (dist < 4f) {
                    continue;
                }
                float rotation = toFoot.ToRotation() - axisAngle;
                float stretch = MathHelper.Clamp(dist / axisLen, 0.8f, 1.28f) * MiniScale;
                Color color = Color.Lerp(lightColor, BloodDeep, 0.55f)
                    .MultiplyRGB(darkMul) * (1f - bodyDissolve);
                sb.Draw(tex, hip - Main.screenPosition, null, color, rotation,
                    hipPx, stretch, SpriteEffects.None, 0f);
            }
        }

        /// <summary>触角：verlet 折线，逐段渐细，尖端泛蔷薇晶</summary>
        private void DrawAntenna(SpriteBatch sb, int side, float alpha) {
            Texture2D pixel = VaultAsset.placeholder2?.Value;
            if (pixel == null) {
                return;
            }
            float headDissolve = PartDissolve(0);
            if (headDissolve >= 1f) {
                return;
            }
            alpha *= 1f - headDissolve;
            ShrimpVerletStrand strand = antennae[side];
            Color rootColor = BloodDark * alpha;
            Color tipColor = GhostCrystal * (alpha * 0.8f);
            int n = strand.Count;
            for (int i = 0; i < n - 1; i++) {
                Vector2 a = strand[i];
                Vector2 b = strand[i + 1];
                Vector2 d = b - a;
                float len = d.Length();
                if (len < 0.01f) {
                    continue;
                }
                float t = i / (float)(n - 1);
                float thickness = MathHelper.Lerp(4f, 1.3f, t) * MiniScale;
                sb.Draw(pixel, a - Main.screenPosition, new Rectangle(0, 0, 1, 1),
                    Color.Lerp(rootColor, tipColor, t * t),
                    d.ToRotation(), new Vector2(0f, 0.5f), new Vector2(len + 0.6f, thickness),
                    SpriteEffects.None, 0f);
            }
        }

        /// <summary>辉光层：晶簇蔷薇光点 + 湿面反光 + 螯尖蓄光</summary>
        private void DrawGlowLayer(SpriteBatch sb) {
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (glow == null) {
                return;
            }
            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

            Vector2 gOrigin = glow.Size() * 0.5f;
            float pulse = 0.72f + 0.28f * MathF.Sin(Main.GlobalTimeWrappedHourly * 3.1f + Seed);
            float fade = 1f - PartDissolve(0);

            void Spot(Vector2 pos, float radius, float strength) {
                sb.Draw(glow, pos - Main.screenPosition, null,
                    (GhostCrystal with { A = 0 }) * (strength * pulse * fade), 0f,
                    gOrigin, new Vector2(radius * 2f / glow.Width), SpriteEffects.None, 0f);
            }

            Vector2 headFwd = nodeDir[0].ToRotationVector2();
            //复眼 / 须冠主晶 / 尾扇双晶 / 双螯尖：与 boss 同一套晶位，色走蔷薇
            Spot(nodePos[0] + headFwd * (14f * MiniScale), 22f * MiniScale, 0.4f);
            Spot(nodePos[0] - headFwd * (58f * MiniScale), 28f * MiniScale, 0.5f);
            Spot(nodePos[4] + nodeDir[4].ToRotationVector2() * (10f * MiniScale), 24f * MiniScale, 0.4f);
            for (int a = 0; a < 2; a++) {
                Spot(ClawTip(a), 15f * MiniScale, a == 0 ? 0.38f : 0.26f);
            }

            //螯尖蓄光：空泡拳蓄势的血光聚焦
            if (clawCharge > 0.05f) {
                Vector2 tip = ClawTip(PunchArm);
                float r = (10f + 20f * clawCharge) * MiniScale;
                sb.Draw(glow, tip - Main.screenPosition, null,
                    (BloodBright with { A = 0 }) * (0.55f * clawCharge), 0f,
                    gOrigin, new Vector2(r * 2f / glow.Width), SpriteEffects.None, 0f);
            }

            //湿面反光：头与体节薄薄一层
            for (int i = 0; i < NodeCount; i++) {
                if (wetness[i] < 0.45f) {
                    continue;
                }
                Spot(nodePos[i], 20f * MiniScale, 0.08f * wetness[i]);
            }

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
        }

        //==================== 命中与谢幕 ====================

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (Main.dedServ) {
                return;
            }
            //螯击穿体：血珠锥 + 晶脆声
            Vector2 dir = (target.Center - nodePos[0]).SafeNormalize(Vector2.UnitX);
            for (int i = 0; i < 9; i++) {
                PRTLoader.NewParticle<PRT_KikasaBloodGlob>(
                    target.Center + Main.rand.NextVector2Circular(20f, 20f),
                    dir.RotatedByRandom(0.7f) * Main.rand.NextFloat(2f, 6f),
                    Main.rand.NextBool(4) ? GhostCrystal : BloodMain * 0.65f,
                    Main.rand.NextFloat(0.4f, 0.7f))?.Configure(Main.rand.Next(14, 26));
            }
            SoundEngine.PlaySound(SoundID.NPCHit4 with { Volume = 0.65f, Pitch = -0.1f, MaxInstances = 3 }, target.Center);
            if (ViewedOwner) {
                ShakeViewer(2.2f);
            }
        }

        public override void OnKill(int timeLeft) {
            if (Main.dedServ || !built) {
                return;
            }
            //谢幕残珠沿脊散
            for (int i = 0; i < NodeCount; i++) {
                PRTLoader.NewParticle<PRT_KikasaBloodGlob>(
                    nodePos[i] + Main.rand.NextVector2Circular(12f, 12f),
                    new Vector2(Main.rand.NextFloat(-1f, 1f), Main.rand.NextFloat(0.5f, 2.2f)),
                    BloodMain * 0.5f, Main.rand.NextFloat(0.35f, 0.6f))
                    ?.Configure(Main.rand.Next(14, 24));
            }
        }
    }
}
