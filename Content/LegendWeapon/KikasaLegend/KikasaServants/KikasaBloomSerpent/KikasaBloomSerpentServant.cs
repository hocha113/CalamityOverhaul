using CalamityOverhaul.Common;
using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaDomains;
using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaServants.KikasaEye;
using CalamityOverhaul.Content.NPCs;
using CalamityOverhaul.Content.NPCs.BloomsandSerpents;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaServants.KikasaBloomSerpent
{
    /// <summary>
    /// 鬼奴·湖水版荒花沙蟒。单弹幕内部模拟一条短链沙蟒（头+10体+尾），
    /// 把血湖当沙海游：贴主人低位宽幅巡曳，链体力学复用 <see cref="SerpentChainMath"/>
    /// （颈紧尾松刚度梯度 / 颈段弯角钳制 / 蓄力聚拢与肌肉行波节距），与 boss 同源手感。
    /// 出场为血砂喷泉：湖面先鼓沙涌，蟒身破泉而出。
    /// 攻击一为掠沙冲撞（蓄力后撤→爆冲穿身，尾迹掀血砂帘），
    /// 攻击二为花刺涟漪（盘身立起，脉冲头→尾扫过红花节，逐朵喷水化钉刺扇）。
    /// 联机同世吞契约：状态走 ai[0..2]、owner 转场盖 netUpdate 章、
    /// 链体各端本地重建、生命线只有 owner 判
    /// </summary>
    internal class KikasaBloomSerpentServant : ModProjectile, IKikasaServant
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        //==================== 可调基数（占位初值，验收再调）====================

        /// <summary>掠沙冲撞接触基伤（召唤加成前）</summary>
        internal const int RamDamage = 460;

        /// <summary>水化钉刺基伤（召唤加成前），钉刺弹幕消费</summary>
        internal const int NeedleDamage = 240;

        //==================== 链体尺寸 ====================

        internal const int SegCount = 12;
        internal const float DrawScale = 1f;
        /// <summary>节距 = boss 节距同值（短链原尺寸，读作幼体不是缩皮）</summary>
        internal const float SegSpacing = 70f;

        /// <summary>红花节：链序 3/6/9（与 boss 的 ordinal%3==2 同律，头尾恒否）</summary>
        internal static bool IsFlowerSeg(int i)
            => i > 0 && i < SegCount - 1 && (i - 1) % 3 == 2;

        //==================== 状态 ====================

        private const int StateEmerge = 0;
        private const int StateFollow = 1;
        private const int StateDashRam = 2;
        private const int StateNeedleRipple = 3;
        private const int StateDissolve = 4;

        private int State { get => (int)Projectile.ai[0]; set => Projectile.ai[0] = value; }
        private ref float StateTimer => ref Projectile.ai[1];
        /// <summary>状态内子参数：出水期=起跳横向符号；攻击期=相位号</summary>
        private ref float StateParam => ref Projectile.ai[2];

        //==================== 时序 ====================

        //血砂喷泉出水：沙涌预兆→破泉拍→S 形爬升→落定
        private const int OmenEnd = 32;
        private const int RiseEnd = 82;
        private const int EmergeTotal = 104;
        private const int EmergeTimeout = 260;

        //掠沙冲撞：蓄力后撤→爆冲穿身→硬刹收势
        private const int DashWindup = 26;
        private const int DashActive = 30;
        private const int DashBrake = 20;

        //花刺涟漪：盘身立起→脉冲逐朵齐射→回摆
        private const int CoilFrames = 26;
        private const int RippleFrames = 34;
        private const int RippleTravel = 28;
        private const int RippleRecover = 16;
        private const int NeedlesPerFlower = 3;

        private const int DissolvePerSegGap = 3;
        private const int DissolveSegFrames = 22;
        private const int DissolveTotal = (SegCount - 1) * DissolvePerSegGap + DissolveSegFrames + 10;

        //==================== 链体数据（各端本地重建，头位置由同步纠偏）====================

        private readonly Vector2[] spine = new Vector2[SegCount];
        /// <summary>蠕虫约定旋转（行进方向角 + PiOver2）；BSS 贴图前方朝下，绘制时减 π</summary>
        private readonly float[] segRot = new float[SegCount];
        /// <summary>节湿度：过水线拉满、出水后衰减，驱动滴落与材质血水度</summary>
        private readonly float[] wetness = new float[SegCount];
        private readonly bool[] belowWater = new bool[SegCount];
        private bool spineInit;

        //==================== 链体力学声明量（本地表现，状态逐帧声明）====================

        /// <summary>蓄力聚拢 0..1：颈段收得最紧、沿身衰减</summary>
        private float gatherLevel;
        /// <summary>行进肌肉波：种类 / 波龄 / 振幅（出手放大、急刹压缩）</summary>
        private int waveKind = SerpentChainMath.WaveNone;
        private float waveAge;
        private float waveAmp;

        //==================== 本地表现量（不入同步）====================

        private int attackCooldown;
        private int attackIndex;
        private int lastSeenState = -1;
        private bool launchDone;
        private bool dashRoared;
        private bool brakeDusted;
        /// <summary>涟漪已开火红花位掩码（换场清零）</summary>
        private int rippleFiredMask;
        /// <summary>红花辉光 0..1（盘身蓄势与脉冲扫过时拉高）</summary>
        private float bloomGlow;
        /// <summary>齐射/蓄力期头部朝向锁（NaN=不锁，方向角语义）</summary>
        private float lockedHeadRot = float.NaN;

        private Player Owner => Main.player[Projectile.owner];

        /// <summary>连续量抖动的确定性相位，各端一致（不掷 Main.rand）</summary>
        private float Seed => Projectile.identity * 0.7391f;

        //==================== 召唤入口 ====================

        /// <summary>KikasaServantIndex 登记的召唤委托；emergeAt.Y = 湖面（沙泉涌起点）</summary>
        internal static void Summon(Player owner, Vector2 emergeAt) {
            if (owner.whoAmI != Main.myPlayer) {
                return;
            }
            int damage = (int)owner.GetTotalDamage(DamageClass.Summon).ApplyTo(RamDamage);
            float dir = MathF.Sign(owner.Center.X - emergeAt.X);
            if (dir == 0f) {
                dir = owner.direction;
            }
            //起点在泉眼正下方湖里，蟒身垂在涌点下待喷
            Projectile.NewProjectile(owner.GetSource_Misc("KikasaServant"),
                emergeAt + new Vector2(0f, 56f), Vector2.Zero,
                ModContent.ProjectileType<KikasaBloomSerpentServant>(), damage, 7f, owner.whoAmI,
                ai2: dir);
        }

        //==================== 定义 ====================

        public override void SetStaticDefaults() {
            Main.projPet[Projectile.type] = true;
            ProjectileID.Sets.MinionSacrificable[Projectile.type] = false;
            ProjectileID.Sets.DrawScreenCheckFluff[Projectile.type] = 1200;
        }

        public override void SetDefaults() {
            Projectile.width = 40;
            Projectile.height = 40;
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

        /// <summary>接触伤害只开在爆冲窗，与可见的冲撞严格对齐</summary>
        public override bool? CanDamage()
            => State == StateDashRam && (int)StateParam == 1 ? null : false;

        /// <summary>多节命中：相邻脊柱点两两线碰撞</summary>
        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            if (!spineInit) {
                return false;
            }
            float _ = 0f;
            for (int i = 1; i < SegCount; i++) {
                if (Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(),
                    spine[i - 1], spine[i], 22f, ref _)) {
                    return true;
                }
            }
            return false;
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
            //泉还没喷就要收场：什么都没露出来，不演谢幕
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

            //生命线：只有 owner 裁决，服务器无领域状态（既定契约）
            if (authority && State != StateDissolve && !LakeHealthy(owner, domain)) {
                BeginDissolve();
            }

            Projectile.timeLeft = 180;
            Projectile.damage = (int)owner.GetTotalDamage(DamageClass.Summon).ApplyTo(RamDamage);

            //换场清闩：远端可能靠收包换场，残闩会吞掉新场节拍
            if (State != lastSeenState) {
                lastSeenState = State;
                dashRoared = false;
                brakeDusted = false;
                rippleFiredMask = 0;
                lockedHeadRot = float.NaN;
                gatherLevel = 0f;
            }

            if (!spineInit) {
                RebuildChain(-Vector2.UnitY);
            }

            StateTimer++;
            switch (State) {
                case StateEmerge: UpdateEmerge(owner, domain); break;
                case StateFollow: UpdateFollow(owner, authority); break;
                case StateDashRam: UpdateDashRam(owner, authority); break;
                case StateNeedleRipple: UpdateNeedleRipple(owner, authority); break;
                case StateDissolve: UpdateDissolve(domain, authority); break;
            }

            UpdateChain(domain);
            UpdateDrips();
            bloomGlow = MathF.Max(0f, bloomGlow - 0.03f);
            if (attackCooldown > 0) {
                attackCooldown--;
            }

            //沿链补光：血红里掺一丝绯花
            for (int i = 0; i < SegCount; i += 4) {
                Lighting.AddLight(spine[i], 0.20f, 0.07f, 0.08f);
            }
        }

        private static bool LakeHealthy(Player owner, KikasaDomainPlayer domain)
            => !owner.dead && domain.AnyActive
            && domain.Phase != KikasaDomainPhase.Closing
            && domain.RiseT >= 0.9f;

        //==================== 血砂喷泉出水 ====================

        private void UpdateEmerge(Player owner, KikasaDomainPlayer domain) {
            float lakeY = domain.LakeWorldY;
            int t = (int)StateTimer;
            bool viewed = ViewedOwner;
            float dir = MathF.Sign(StateParam) == 0f ? 1f : MathF.Sign(StateParam);
            Vector2 fount = new(Projectile.Center.X, lakeY);

            if (t < OmenEnd) {
                //沙涌预兆：泉眼水面鼓包冒沙，涟漪一圈圈挤出去
                Projectile.velocity = Vector2.Zero;
                if (viewed) {
                    if (t % 5 == 2) {
                        KikasaDomainDeco.RippleAt(fount, 0.3f + t / (float)OmenEnd * 0.5f);
                    }
                    if (!Main.dedServ && t % 3 == 1) {
                        //水面跳沙：泉眼下有东西在拱
                        Dust d = Dust.NewDustPerfect(fount + new Vector2(Main.rand.NextFloat(-26f, 26f), -2f),
                            DustID.Sand, new Vector2(Main.rand.NextFloat(-0.8f, 0.8f), -Main.rand.NextFloat(1f, 2.6f)),
                            110, default, Main.rand.NextFloat(0.8f, 1.2f));
                        d.velocity.Y -= 0.5f;
                    }
                    if (t == 10 || t == 24) {
                        SoundEngine.PlaySound(SoundID.WormDig with {
                            Volume = 0.5f,
                            Pitch = t == 10 ? -0.5f : -0.2f,
                            MaxInstances = 2
                        }, fount);
                        ShakeViewer(t == 10 ? 0.8f : 1.3f);
                    }
                }
                return;
            }

            if (!launchDone) {
                //破泉拍：沙泉一柱冲天，蟒头带着仰角自泉心挤出
                launchDone = true;
                Projectile.velocity = new Vector2(dir * 4.2f, -17.5f);
                BssVfx.Roar(Projectile.Center, 0.15f, 0.7f);
                if (viewed) {
                    FountainBurst(fount);
                }
            }

            if (t <= RiseEnd) {
                //S 形爬升：纵向指数衰减，横向正弦蜿蜒渐入
                float riseT = t - OmenEnd;
                float weaveIn = MathHelper.Clamp(riseT / 12f, 0f, 1f);
                Projectile.velocity.Y = -17.5f * MathF.Exp(-0.05f * riseT);
                Projectile.velocity.X = dir * 4.2f * MathF.Exp(-0.03f * riseT)
                    + MathF.Sin(riseT * 0.15f + Seed) * 4f * weaveIn;
            }
            else {
                //落定：弯向主人侧下方的低位悬点（沙蟒贴地性子，鬼奴版贴水）
                Vector2 anchor = owner.Center + new Vector2(-owner.direction * 155f, 30f);
                Vector2 want = (anchor - Projectile.Center) * 0.06f;
                if (want.Length() > 11f) {
                    want = want.SafeNormalize(Vector2.Zero) * 11f;
                }
                Projectile.velocity = Vector2.Lerp(Projectile.velocity, want, 0.08f);
            }

            if (t >= EmergeTotal || t > EmergeTimeout) {
                State = StateFollow;
                StateTimer = 0;
                StateParam = 0;
                attackCooldown = 40;
                Projectile.netUpdate = Main.myPlayer == Projectile.owner;
            }
        }

        /// <summary>破泉浪冠：沙尘喷泉掺血珠扇，泉眼涟漪连爆</summary>
        private void FountainBurst(Vector2 hit) {
            KikasaDomainDeco.RippleAt(hit, 2.2f);
            KikasaDomainDeco.RippleAt(hit + new Vector2(46f, 0f), 0.9f);
            KikasaDomainDeco.RippleAt(hit - new Vector2(46f, 0f), 0.9f);
            KikasaDomainDeco.SplashAt(hit, 10);
            BssVfx.SandBurst(hit, 1.3f);

            //血珠混在沙柱里向上抛
            for (int i = 0; i < 16; i++) {
                float angle = -MathHelper.PiOver2 + Main.rand.NextFloat(-0.7f, 0.7f);
                PRTLoader.NewParticle<PRT_KikasaBloodGlob>(
                    hit + new Vector2(Main.rand.NextFloat(-24f, 24f), -4f),
                    angle.ToRotationVector2() * Main.rand.NextFloat(3f, 8.5f),
                    Main.rand.NextBool(3) ? BloodDeep : BloodMain,
                    Main.rand.NextFloat(0.45f, 0.8f))?.Configure(Main.rand.Next(22, 38));
            }
            PRTLoader.NewParticle<PRT_DWave>(hit, Vector2.Zero, BloodDeep, 0.1f)
                ?.Configure(new Vector2(0.4f, 1f), -MathHelper.PiOver2, 0.4f, 11);

            SoundEngine.PlaySound(SoundID.SplashWeak with { Volume = 1f, Pitch = -0.3f, MaxInstances = 2 }, hit);
            ShakeViewer(5f);
        }

        //==================== 贴水巡曳跟随 ====================

        private void UpdateFollow(Player owner, bool authority) {
            //低位宽幅巡曳锚：横长竖扁的利萨如，读作贴着沙面游
            Vector2 anchor = owner.Center + new Vector2(-owner.direction * 150f, 34f);
            float w = (float)StateTimer * 0.026f + Seed;
            anchor += new Vector2(MathF.Sin(w) * 195f, MathF.Sin(w * 2f + Seed * 2f) * 30f);

            Vector2 to = anchor - Projectile.Center;
            if (to.Length() > 2400f) {
                Projectile.Center = anchor;
                Projectile.velocity = Vector2.Zero;
                RebuildChain(Vector2.UnitX * owner.direction);
                Projectile.netUpdate = authority;
                return;
            }
            float maxSpeed = to.Length() > 1400f ? 24f : 14f;
            Vector2 desired = to * 0.07f;
            if (desired.Length() > maxSpeed) {
                desired = desired.SafeNormalize(Vector2.Zero) * maxSpeed;
            }
            Projectile.velocity = Vector2.Lerp(Projectile.velocity, desired, 0.10f);
            //高频小摆蜿蜒叠在大巡曳上
            Projectile.velocity = Projectile.velocity.RotatedBy(
                MathF.Sin((float)StateTimer * 0.16f + Seed * 3f) * 0.05f);
            //沙蟒不许死停
            if (Projectile.velocity.Length() < 2.4f) {
                Projectile.velocity += (w * 2.1f).ToRotationVector2() * 0.45f;
            }

            //快游时体表渗沙（干沙身份的常提醒）
            if (!Main.dedServ && Projectile.velocity.Length() > 7f && Main.rand.NextBool(5)) {
                BssVfx.SandTrickle(spine[Main.rand.Next(SegCount)], 0.8f);
            }

            //出手裁决：掠沙冲撞与花刺涟漪交替，owner 盖章
            int target = FindTarget(owner);
            if (target >= 0 && attackCooldown <= 0 && StateTimer > 40) {
                attackIndex++;
                State = attackIndex % 2 == 1 ? StateDashRam : StateNeedleRipple;
                StateTimer = 0;
                StateParam = 0;
                Projectile.netUpdate = authority;
            }
        }

        //==================== 掠沙冲撞 ====================

        private void UpdateDashRam(Player owner, bool authority) {
            int t = (int)StateTimer;
            int phase = (int)StateParam;
            int target = FindTarget(owner);

            void NextPhase(int next) {
                StateParam = next;
                StateTimer = 0;
                Projectile.netUpdate = authority;
            }

            if (phase == 0) {
                //蓄力后撤：头锁猎物、全身向后坐，聚拢波把身体向头收拢上膛
                if (target < 0) {
                    EndAttack(authority, 45);
                    return;
                }
                NPC npc = Main.npc[target];
                Vector2 aim = npc.Center + npc.velocity * 9f;
                Vector2 aimDir = (aim - Projectile.Center).SafeNormalize(Vector2.UnitX);
                lockedHeadRot = float.IsNaN(lockedHeadRot) ? aimDir.ToRotation()
                    : lockedHeadRot.AngleTowards(aimDir.ToRotation(), 0.22f);

                float progress = MathHelper.Clamp(t / (float)DashWindup, 0f, 1f);
                float late = MathF.Pow(progress, 6f);
                Projectile.velocity = Vector2.Lerp(Projectile.velocity, -aimDir * (2.5f + 12f * late), 0.25f);
                gatherLevel = progress;

                if (t == 2) {
                    SoundEngine.PlaySound(SoundID.WormDig with { Volume = 0.45f, Pitch = -0.35f, MaxInstances = 2 }, Projectile.Center);
                }
                //蓄势渗沙：越憋越凶，72% 后静默
                if (!Main.dedServ && t < DashWindup * 0.72f && t % 3 == 1) {
                    BssVfx.SandTrickle(spine[Main.rand.Next(SegCount / 2)], 1.2f);
                }

                if (t >= DashWindup) {
                    //起跳一帧定速穿向预测点
                    Projectile.velocity = aimDir * 25f;
                    gatherLevel = 0f;
                    PulseGapWave(SerpentChainMath.WaveRelease, 0.55f);
                    NextPhase(1);
                    if (!dashRoared) {
                        dashRoared = true;
                        BssVfx.Roar(Projectile.Center, 0.05f, 0.8f);
                        SoundEngine.PlaySound(SoundID.DD2_WyvernDiveDown with { Volume = 0.7f, Pitch = -0.1f, MaxInstances = 2 }, Projectile.Center);
                    }
                    if (ViewedOwner) {
                        ShakeViewer(3f);
                    }
                }
                return;
            }

            if (phase == 1) {
                //爆冲穿身：复利续力，尾迹掀血砂帘
                lockedHeadRot = float.NaN;
                Projectile.velocity *= 1.014f;
                if (Projectile.velocity.Length() > 38f) {
                    Projectile.velocity = Projectile.velocity.SafeNormalize(Vector2.UnitX) * 38f;
                }
                if (!Main.dedServ) {
                    //沙帘：头后错拍抛沙 + 血珠，红花节沿途落瓣
                    for (int k = 0; k < 2; k++) {
                        Dust d = Dust.NewDustPerfect(
                            Projectile.Center - Projectile.velocity * Main.rand.NextFloat(0.4f, 1.4f)
                                + Main.rand.NextVector2Circular(14f, 14f),
                            DustID.Sand,
                            new Vector2(Main.rand.NextFloat(-1.5f, 1.5f), -Main.rand.NextFloat(1f, 3.4f)),
                            100, default, Main.rand.NextFloat(1f, 1.5f));
                        d.velocity.Y -= 0.6f;
                    }
                    if (Main.rand.NextBool(3)) {
                        PRTLoader.NewParticle<PRT_KikasaBloodGlob>(
                            Projectile.Center + Main.rand.NextVector2Circular(16f, 16f),
                            -Projectile.velocity * 0.1f + Main.rand.NextVector2Circular(1.2f, 1.2f),
                            BloodMain * 0.55f, Main.rand.NextFloat(0.35f, 0.6f))?.Configure(Main.rand.Next(14, 24));
                    }
                    if (Main.rand.NextBool(4)) {
                        int flower = 3 + Main.rand.Next(3) * 3;
                        BssVfx.PetalDrift(spine[flower], -Projectile.velocity * 0.08f
                            + Main.rand.NextVector2Circular(1.4f, 1f));
                    }
                }

                //穿过目标或窗口耗尽：硬刹收势
                bool passed = target >= 0 && t > 8
                    && Vector2.Dot(Main.npc[target].Center - Projectile.Center,
                        Projectile.velocity.SafeNormalize(Vector2.UnitX)) < -60f;
                if (passed || t > DashActive) {
                    PulseGapWave(SerpentChainMath.WavePress, 0.45f);
                    NextPhase(2);
                }
                return;
            }

            //硬刹收势：读出分量，身体向刹住的头追压
            Projectile.velocity *= t <= 5 ? 0.72f : 0.9f;
            if (!brakeDusted) {
                brakeDusted = true;
                if (!Main.dedServ && ViewedOwner) {
                    BssVfx.SandBurst(spine[0], 0.7f);
                }
            }
            if (t >= DashBrake) {
                EndAttack(authority, 110);
            }
        }

        //==================== 花刺涟漪 ====================

        private void UpdateNeedleRipple(Player owner, bool authority) {
            int t = (int)StateTimer;
            int phase = (int)StateParam;
            int target = FindTarget(owner);
            Vector2 aimPos = target >= 0
                ? Main.npc[target].Center + Main.npc[target].velocity * 6f
                : Projectile.Center + (float.IsNaN(lockedHeadRot) ? Vector2.UnitX : lockedHeadRot.ToRotationVector2()) * 400f;

            void NextPhase(int next) {
                StateParam = next;
                StateTimer = 0;
                Projectile.netUpdate = authority;
            }

            if (phase == 0) {
                //盘身立起：减速悬住，头锁猎物，红花蓄光抖瓣
                if (target < 0) {
                    EndAttack(authority, 45);
                    return;
                }
                Vector2 aimDir = (aimPos - Projectile.Center).SafeNormalize(Vector2.UnitX);
                lockedHeadRot = float.IsNaN(lockedHeadRot) ? aimDir.ToRotation()
                    : lockedHeadRot.AngleTowards(aimDir.ToRotation(), 0.2f);
                Projectile.velocity *= 0.86f;
                Projectile.velocity = Projectile.velocity.RotatedBy(
                    MathF.Sin(t * 0.4f + Seed) * 0.08f);
                gatherLevel = MathHelper.Clamp(t / (float)CoilFrames, 0f, 1f) * 0.5f;
                bloomGlow = MathF.Max(bloomGlow, t / (float)CoilFrames);

                if (t == 2) {
                    SoundEngine.PlaySound(SoundID.Grass with { Volume = 0.6f, Pitch = -0.3f, MaxInstances = 2 }, Projectile.Center);
                }
                if (!Main.dedServ && t % 6 == 1) {
                    int flower = 3 + Main.rand.Next(3) * 3;
                    BssVfx.PetalDrift(spine[flower] + Main.rand.NextVector2Circular(8f, 8f),
                        new Vector2(Main.rand.NextFloat(-0.8f, 0.8f), -0.6f));
                }
                if (t >= CoilFrames) {
                    NextPhase(1);
                }
                return;
            }

            if (phase == 1) {
                //脉冲头→尾扫过红花节，扫到即喷一扇水化钉刺
                Projectile.velocity *= 0.94f;
                bloomGlow = 1f;
                float pulse = MathHelper.Clamp(t / (float)RippleTravel, 0f, 1f);
                for (int i = 0; i < SegCount; i++) {
                    if (!IsFlowerSeg(i) || (rippleFiredMask & 1 << i) != 0) {
                        continue;
                    }
                    float frac = i / (float)(SegCount - 1);
                    if (pulse < frac) {
                        continue;
                    }
                    rippleFiredMask |= 1 << i;
                    FireFlowerFan(owner, i, aimPos, authority);
                }
                if (t >= RippleFrames) {
                    NextPhase(2);
                }
                return;
            }

            //回摆
            Projectile.velocity *= 0.92f;
            if (t >= RippleRecover) {
                EndAttack(authority, 95);
            }
        }

        /// <summary>单朵红花的钉刺扇：owner 端生成，各端演花爆</summary>
        private void FireFlowerFan(Player owner, int segIndex, Vector2 aimPos, bool authority) {
            Vector2 from = spine[segIndex];
            Vector2 aimDir = (aimPos - from).SafeNormalize(Vector2.UnitX);

            SoundEngine.PlaySound(SoundID.Item17 with {
                Volume = 0.45f,
                Pitch = -0.2f + segIndex * 0.04f,
                MaxInstances = 3
            }, from);
            if (!Main.dedServ) {
                for (int i = 0; i < 4; i++) {
                    BssVfx.PetalDrift(from + Main.rand.NextVector2Circular(8f, 8f),
                        aimDir.RotatedByRandom(0.9f) * Main.rand.NextFloat(1f, 2.4f));
                }
                for (int i = 0; i < 3; i++) {
                    PRTLoader.NewParticle<PRT_KikasaBloodGlob>(from,
                        aimDir.RotatedByRandom(0.5f) * Main.rand.NextFloat(2f, 5f),
                        BloodMain * 0.6f, Main.rand.NextFloat(0.3f, 0.5f))?.Configure(Main.rand.Next(12, 20));
                }
            }

            if (!authority) {
                return;
            }
            int damage = (int)owner.GetTotalDamage(DamageClass.Summon).ApplyTo(NeedleDamage);
            for (int k = 0; k < NeedlesPerFlower; k++) {
                float off = (k - NeedlesPerFlower / 2) * 0.17f + Main.rand.NextFloat(-0.04f, 0.04f);
                Vector2 vel = aimDir.RotatedBy(off) * 12.5f;
                //钉刺带一点上抛偏置，配合弹体后段微坠走弧线
                vel.Y -= 0.8f;
                Projectile.NewProjectile(Projectile.GetSource_FromAI(), from, vel,
                    ModContent.ProjectileType<KikasaBloomNeedle>(), damage, 2f, Projectile.owner);
            }
        }

        private void EndAttack(bool authority, int cooldown) {
            State = StateFollow;
            StateTimer = 0;
            StateParam = 0;
            lockedHeadRot = float.NaN;
            gatherLevel = 0f;
            attackCooldown = cooldown;
            Projectile.netUpdate = authority;
        }

        //==================== 溶解遣返 ====================

        private void UpdateDissolve(KikasaDomainPlayer domain, bool authority) {
            int t = (int)StateTimer;
            bool lakeAlive = domain.AnyActive && domain.RiseT > 0.5f;

            if (lakeAlive) {
                //头先沉，链体跟着一节节穿回水里
                Projectile.velocity.X *= 0.94f;
                Projectile.velocity.Y = MathF.Min(Projectile.velocity.Y + 0.24f, 8f);
            }
            else {
                Projectile.velocity *= 0.9f;
            }

            //化水残珠与碎沙沿链错拍，红花位落瓣
            if (!Main.dedServ && t % 3 == 0) {
                int i = Main.rand.Next(SegCount);
                float dissolve = SegDissolve(i);
                if (dissolve is > 0.1f and < 0.9f) {
                    PRTLoader.NewParticle<PRT_KikasaBloodGlob>(
                        spine[i] + Main.rand.NextVector2Circular(16f, 16f),
                        new Vector2(Main.rand.NextFloat(-0.6f, 0.6f), Main.rand.NextFloat(1.2f, 2.6f)),
                        BloodMain * 0.5f, Main.rand.NextFloat(0.35f, 0.6f))
                        ?.Configure(Main.rand.Next(14, 24));
                    BssVfx.SandTrickle(spine[i], 1.4f);
                    if (IsFlowerSeg(i) && Main.rand.NextBool(2)) {
                        BssVfx.PetalDrift(spine[i], Main.rand.NextVector2Circular(1.4f, 1f));
                    }
                }
            }

            if (authority && t >= DissolveTotal) {
                Projectile.Kill();
            }
            else if (!authority && t >= DissolveTotal + 10) {
                Projectile.Kill();
            }
        }

        /// <summary>逐节溶解进度：尾先化、头最后</summary>
        private float SegDissolve(int i) {
            if (State != StateDissolve) {
                return 0f;
            }
            float start = (SegCount - 1 - i) * DissolvePerSegGap;
            return MathHelper.Clamp((StateTimer - start) / DissolveSegFrames, 0f, 1f);
        }

        //==================== 链体推进（阻尼追踪 + SerpentChainMath 力学）====================

        /// <summary>头位硬纠或初始化时沿指定方向直线重建，防链体抽搐</summary>
        private void RebuildChain(Vector2 headDir) {
            spineInit = true;
            Vector2 head = Projectile.Center;
            Vector2 back = -headDir.SafeNormalize(Vector2.UnitX);
            float wormRot = headDir.ToRotation() + MathHelper.PiOver2;
            float lakeY = Owner?.active == true && Owner.TryGetModPlayer(out KikasaDomainPlayer domain)
                ? domain.LakeWorldY : float.MaxValue;
            for (int i = 0; i < SegCount; i++) {
                spine[i] = head + back * (i * SegSpacing);
                segRot[i] = wormRot;
                belowWater[i] = spine[i].Y >= lakeY;
                wetness[i] = 1f;
            }
        }

        /// <summary>触发一记行进肌肉波（出手释放/急刹追压）</summary>
        private void PulseGapWave(int kind, float amp) {
            waveKind = kind;
            waveAge = 0f;
            waveAmp = amp;
        }

        private void UpdateChain(KikasaDomainPlayer domain) {
            //本帧渲染位 = Center + velocity（AI 在位移积分前跑）
            Vector2 head = Projectile.Center + Projectile.velocity;

            if (Vector2.Distance(spine[0], head) > 140f) {
                //硬纠检测：同步包把头拽走半屏，直线重建
                RebuildChain(Projectile.velocity.SafeNormalize(Vector2.UnitX));
                return;
            }

            spine[0] = head;
            if (!float.IsNaN(lockedHeadRot)) {
                segRot[0] = lockedHeadRot + MathHelper.PiOver2;
            }
            else if (Projectile.velocity.Length() > 0.5f) {
                segRot[0] = Projectile.velocity.ToRotation() + MathHelper.PiOver2;
            }

            //肌肉波推龄，窗口耗尽自净
            if (waveKind != SerpentChainMath.WaveNone) {
                waveAge++;
                if (waveAge > SegCount * 2.2f + 26f) {
                    waveKind = SerpentChainMath.WaveNone;
                }
            }
            float headSpeed = Projectile.velocity.Length();

            //每节独立追前节：刚度梯度（颈紧尾松）+ 颈段弯角钳制 + 三层节距语言
            for (int i = 1; i < SegCount; i++) {
                Vector2 front = spine[i - 1];
                Vector2 segmentTarget = front - spine[i];

                //前邻转角带动：刚度按链序渐变，boss 同源
                if (segRot[i - 1] != segRot[i]) {
                    float stiffness = SerpentChainMath.StiffnessFactor(i, SegCount);
                    segmentTarget = segmentTarget.RotatedBy(
                        MathHelper.WrapAngle(segRot[i - 1] - segRot[i]) * stiffness);
                    segmentTarget = segmentTarget.MoveTowards(
                        (segRot[i - 1] - segRot[i]).ToRotationVector2(), 1f);
                }

                //颈段弯角硬钳制：相对前邻体轴的折角超限即圆化
                float maxBend = SerpentChainMath.MaxBendAngle(i);
                if (maxBend < MathHelper.Pi && segmentTarget.LengthSquared() > 1f) {
                    float frontAxis = segRot[i - 1] - MathHelper.PiOver2;
                    float bend = MathHelper.WrapAngle(segmentTarget.ToRotation() - frontAxis);
                    if (MathF.Abs(bend) > maxBend) {
                        float clamped = frontAxis + MathF.Sign(bend) * maxBend;
                        segmentTarget = clamped.ToRotationVector2() * segmentTarget.Length();
                    }
                }

                segRot[i] = segmentTarget.ToRotation() + MathHelper.PiOver2;

                //节距三层语言：蓄力聚拢 × 肌肉行波 × 高速拉伸
                float gap = SegSpacing;
                gap *= SerpentChainMath.GatherFactor(i, gatherLevel);
                gap *= SerpentChainMath.GapWaveFactor(i, waveKind, waveAge, waveAmp);
                gap *= SerpentChainMath.SpeedStretchFactor(headSpeed);
                spine[i] = front - segmentTarget.SafeNormalize(Vector2.Zero) * gap;
            }

            UpdateSegmentCrossings(domain);
        }

        /// <summary>逐节过水线（双向）：水花帧内限量、音效只给第一个；出水节湿度拉满</summary>
        private void UpdateSegmentCrossings(KikasaDomainPlayer domain) {
            float lakeY = domain.LakeWorldY;
            bool lakeAlive = domain.AnyActive && domain.RiseT > 0.5f;
            bool viewed = ViewedOwner;
            int fxBudget = 2;
            bool soundLeft = true;

            for (int i = 0; i < SegCount; i++) {
                bool below = spine[i].Y >= lakeY;
                if (below != belowWater[i]) {
                    belowWater[i] = below;
                    wetness[i] = 1f;
                    if (lakeAlive && viewed && fxBudget > 0) {
                        fxBudget--;
                        Vector2 hit = new(spine[i].X, lakeY);
                        KikasaDomainDeco.RippleAt(hit, i == 0 ? 0.8f : 0.45f);
                        for (int k = 0; k < 3; k++) {
                            PRTLoader.NewParticle<PRT_KikasaBloodGlob>(
                                hit + new Vector2(Main.rand.NextFloat(-12f, 12f), -3f),
                                new Vector2(Main.rand.NextFloat(-1.2f, 1.2f), -Main.rand.NextFloat(2f, 4.2f)),
                                BloodMain * 0.6f, Main.rand.NextFloat(0.35f, 0.55f))
                                ?.Configure(Main.rand.Next(14, 24));
                        }
                        if (soundLeft) {
                            soundLeft = false;
                            SoundEngine.PlaySound(SoundID.SplashWeak with {
                                Volume = 0.4f,
                                Pitch = -0.3f + i * 0.02f,
                                MaxInstances = 3
                            }, hit);
                        }
                    }
                }
                //水下恒湿，出水后慢慢淌干（沙身淌得比世吞快，干沙吸水）
                wetness[i] = below ? 1f : MathF.Max(0f, wetness[i] - 0.014f);
            }
        }

        /// <summary>湿度驱动滴落：湿节淌血珠，将干的节改渗沙</summary>
        private void UpdateDrips() {
            if (Main.dedServ) {
                return;
            }
            int budget = 2;
            for (int k = 0; k < 3 && budget > 0; k++) {
                int i = Main.rand.Next(SegCount);
                if (belowWater[i]) {
                    continue;
                }
                if (wetness[i] > 0.35f) {
                    if (Main.rand.NextFloat() > wetness[i] * 0.4f) {
                        continue;
                    }
                    budget--;
                    PRTLoader.NewParticle<PRT_KikasaBloodGlob>(
                        spine[i] + Main.rand.NextVector2Circular(18f, 14f),
                        new Vector2(Projectile.velocity.X * 0.05f, Main.rand.NextFloat(0.8f, 1.8f)),
                        (Main.rand.NextBool(3) ? BloodDeep : BloodMain) * Main.rand.NextFloat(0.45f, 0.6f),
                        Main.rand.NextFloat(0.35f, 0.55f))?.Configure(Main.rand.Next(16, 28), 0.3f);
                }
                else if (wetness[i] > 0.05f && Main.rand.NextBool(3)) {
                    budget--;
                    BssVfx.SandTrickle(spine[i], 0.7f);
                }
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

        //==================== 血系配色（CoolTint 家族，绯花红做次要点缀）====================

        private static Color BloodDeep => KikasaDomain.CoolTint(new(140, 32, 30), new(84, 104, 110));
        private static Color BloodMain => KikasaDomain.CoolTint(new(237, 77, 69), new(126, 158, 164));
        private static Color BloodBright => KikasaDomain.CoolTint(new(246, 133, 112), new(176, 200, 204));
        /// <summary>湖化绯花红：红花节辉光的鬼奴变调</summary>
        internal static Color GhostBloom => KikasaDomain.CoolTint(new(214, 62, 74), new(140, 120, 150));

        //==================== 绘制 ====================

        private int SegNpcType(int i)
            => i == 0 ? ModContent.NPCType<BssHead>()
            : i == SegCount - 1 ? ModContent.NPCType<BssTail>()
            : ModContent.NPCType<BssBody>();

        private void GetSegDraw(int i, out Texture2D tex, out Rectangle frame) {
            int type = SegNpcType(i);
            Main.instance.LoadNPC(type);
            tex = TextureAssets.Npc[type].Value;
            int frames = Math.Max(1, Main.npcFrameCount[type]);
            int frameHeight = tex.Height / frames;
            int style = frames > 1 ? SerpentChainMath.BodyStyleIndex(i - 1, IsFlowerSeg(i)) : 0;
            frame = new Rectangle(0, style * frameHeight, tex.Width, frameHeight);
        }

        public override bool PreDraw(ref Color lightColor) {
            if (!spineInit) {
                return false;
            }
            SpriteBatch sb = Main.spriteBatch;

            //本体：血湖材质逐节
            DrawChain(sb, lightColor);

            //辉光层：出水预兆沙下暖光 / 湿面反光 / 红花蓄光
            DrawGlowLayer(sb);

            return false;
        }

        private void DrawChain(SpriteBatch sb, Color lightColor) {
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
                form.Parameters["uScanMode"]?.SetValue(0f);
            }

            int emergeT = State == StateEmerge ? (int)StateTimer : int.MaxValue;

            //尾→头，头压顶层
            for (int i = SegCount - 1; i >= 0; i--) {
                float dissolve = SegDissolve(i);
                if (dissolve >= 1f) {
                    continue;
                }
                GetSegDraw(i, out Texture2D tex, out Rectangle frame);
                Vector2 pos = spine[i] - Main.screenPosition;
                //BSS 贴图前方朝下：蠕虫约定旋转（dir+PiOver2）减 π 落到贴图约定
                float rot = segRot[i] - MathHelper.Pi;

                Color color;
                if (shaderOk) {
                    //出水期从全血水错拍凝实：尾节比头晚醒
                    float steady = MathHelper.Clamp(0.28f + wetness[i] * 0.16f
                        + MathF.Sin(Main.GlobalTimeWrappedHourly * 2.6f + Seed + i * 0.8f) * 0.04f, 0f, 0.6f);
                    float segForm = steady;
                    if (emergeT != int.MaxValue) {
                        float condense = MathHelper.Clamp((emergeT - OmenEnd - i * 2f) / 38f, 0f, 1f);
                        segForm = MathHelper.Lerp(0.9f, steady, condense * condense * (3f - 2f * condense));
                    }
                    form.Parameters["uSeed"]?.SetValue(Seed + i * 1.7f);
                    form.Parameters["uForm"]?.SetValue(segForm);
                    form.Parameters["uDissolve"]?.SetValue(dissolve);
                    form.Parameters["uUvRect"]?.SetValue(new Vector4(
                        frame.X / (float)tex.Width, frame.Y / (float)tex.Height,
                        frame.Width / (float)tex.Width, frame.Height / (float)tex.Height));
                    form.Parameters["uTexel"]?.SetValue(new Vector2(1f / tex.Width, 1f / tex.Height));
                    form.Parameters["uAspect"]?.SetValue(frame.Width / (float)frame.Height);
                    form.CurrentTechnique.Passes[0].Apply();
                    color = Color.White;
                }
                else {
                    color = Color.Lerp(lightColor, BloodMain, 0.5f) * (1f - dissolve);
                }

                sb.Draw(tex, pos, frame, color, rot, frame.Size() * 0.5f, DrawScale, SpriteEffects.None, 0f);
                if (i == 0) {
                    BssJawDraw.Draw(sb, spine[0], rot, BssJawDraw.IdleOpen(Main.GlobalTimeWrappedHourly * 3f),
                        color, Main.screenPosition, DrawScale);
                }
            }

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
        }

        /// <summary>辉光层：出水预兆沙下暖光 + 湿节水膜反光 + 红花蓄光</summary>
        private void DrawGlowLayer(SpriteBatch sb) {
            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

            //湿节水膜：刚出水的节泛一层薄反光，读作液体不是贴纸
            Color sheen = BloodBright with { A = 0 };
            for (int i = SegCount - 1; i >= 0; i--) {
                if (wetness[i] < 0.45f || SegDissolve(i) >= 1f) {
                    continue;
                }
                GetSegDraw(i, out Texture2D tex, out Rectangle frame);
                float a = 0.10f * wetness[i] * (1f - SegDissolve(i));
                sb.Draw(tex, spine[i] - Main.screenPosition, frame, sheen * a,
                    segRot[i] - MathHelper.Pi, frame.Size() * 0.5f, DrawScale * 1.03f, SpriteEffects.None, 0f);
            }

            //红花蓄光：盘身与涟漪期红花节泛绯
            if (bloomGlow > 0.03f) {
                Color bloom = GhostBloom with { A = 0 };
                for (int i = 0; i < SegCount; i++) {
                    if (!IsFlowerSeg(i) || SegDissolve(i) >= 1f) {
                        continue;
                    }
                    GetSegDraw(i, out Texture2D tex, out Rectangle frame);
                    float pulse = 0.75f + 0.25f * MathF.Sin(Main.GlobalTimeWrappedHourly * 14f + i * 0.9f);
                    sb.Draw(tex, spine[i] - Main.screenPosition, frame,
                        bloom * (0.5f * bloomGlow * pulse * (1f - SegDissolve(i))),
                        segRot[i] - MathHelper.Pi, frame.Size() * 0.5f, DrawScale * 1.06f, SpriteEffects.None, 0f);
                    Lighting.AddLight(spine[i], GhostBloom.ToVector3() * 0.25f * bloomGlow);
                }
            }

            //出水预兆：泉眼下暖沙光自深处贴上来
            Texture2D softGlow = CWRAsset.SoftGlow?.Value;
            if (softGlow != null && State == StateEmerge && StateTimer < OmenEnd && ViewedOwner
                && Owner.TryGetModPlayer(out KikasaDomainPlayer domain)) {
                float ot = MathHelper.Clamp(StateTimer / (float)OmenEnd, 0f, 1f);
                float ease = 1f - (1f - ot) * (1f - ot);
                Vector2 pos = new(Projectile.Center.X, domain.LakeWorldY + MathHelper.Lerp(42f, 6f, ease));
                float r = 36f + 28f * ease;
                Color glowC = KikasaDomain.CoolTint(new(226, 168, 110), new(150, 170, 180));
                sb.Draw(softGlow, pos - Main.screenPosition, null, glowC * (0.38f * ease), 0f,
                    softGlow.Size() * 0.5f,
                    new Vector2(r * 3.2f / softGlow.Width, r * 0.8f / softGlow.Height), SpriteEffects.None, 0f);
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
            //穿体：沙爆掺血珠，红花抖落几瓣
            BssVfx.SandBurst(target.Center, 0.6f);
            for (int i = 0; i < 8; i++) {
                PRTLoader.NewParticle<PRT_KikasaBloodGlob>(
                    target.Center + Main.rand.NextVector2Circular(22f, 22f),
                    Projectile.velocity * 0.2f + Main.rand.NextVector2Circular(2.4f, 2.4f),
                    BloodMain * 0.6f, Main.rand.NextFloat(0.4f, 0.7f))
                    ?.Configure(Main.rand.Next(16, 26), Main.rand.NextFloat(-0.4f, 0.4f));
            }
            for (int i = 0; i < 3; i++) {
                BssVfx.PetalDrift(target.Center + Main.rand.NextVector2Circular(14f, 14f),
                    Projectile.velocity * 0.1f + Main.rand.NextVector2Circular(1.6f, 1.2f));
            }
            SoundEngine.PlaySound(SoundID.NPCHit1 with { Volume = 0.7f, Pitch = -0.3f, MaxInstances = 3 }, target.Center);
            if (ViewedOwner) {
                ShakeViewer(2f);
            }
        }

        public override void OnKill(int timeLeft) {
            if (Main.dedServ || !spineInit) {
                return;
            }
            //谢幕残珠沿链散，红花位各落一撮瓣
            for (int i = 0; i < SegCount; i += 2) {
                PRTLoader.NewParticle<PRT_KikasaBloodGlob>(
                    spine[i] + Main.rand.NextVector2Circular(12f, 12f),
                    new Vector2(Main.rand.NextFloat(-1f, 1f), Main.rand.NextFloat(0.5f, 2.2f)),
                    BloodMain * 0.5f, Main.rand.NextFloat(0.35f, 0.6f))
                    ?.Configure(Main.rand.Next(14, 24));
            }
            for (int i = 0; i < SegCount; i++) {
                if (IsFlowerSeg(i)) {
                    BssVfx.PetalDrift(spine[i], Main.rand.NextVector2Circular(1.6f, 1.2f));
                }
            }
        }
    }
}
