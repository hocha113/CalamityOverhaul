using CalamityOverhaul.Common;
using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaDomains;
using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaServants.KikasaEye;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaServants.KikasaBrain
{
    /// <summary>
    /// 鬼奴·湖水版克苏鲁之脑。一切节拍由心跳驱动：本体按心缩/心舒脉动呼吸，
    /// 闷鼓心跳声与闪现出手全落在心跳拍上。出水为"湖底心音"四拍
    /// （水下心跳预兆→破水浪冠→升起凝实（凝块逐颗归轨）→觉醒拍假身裂出）。
    /// 签名机制为心跳节拍闪现环绕：绕目标定环连跳四拍，闪现前落点汇聚涟漪、
    /// 旧位留一瞬血形残壳；两具低凝实假身镜像常驻（纯本地表现、Seed 确定性分布），
    /// 假身的"攻击"在接触前碎成血珠——真伤害只来自真身与凝块。
    /// 攻击为闪现环绕接触压迫与献祭爆（按规则消耗一颗血凝块卫星换范围爆发，冷却重凝）。
    /// 联机契约同基准：闪现落点 owner 裁决盖 netUpdate 章，假身与凝块轨道
    /// 全部由状态+Seed 各端本地重建，绝不逐个同步；生命线只有 owner 判
    /// </summary>
    internal class KikasaBrainServant : ModProjectile, IKikasaServant
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        //==================== 可调基数（占位初值，验收再调）====================

        /// <summary>闪现环绕本体接触基伤（召唤加成前）</summary>
        internal const int BodyDamage = 460;

        /// <summary>血凝块卫星接触/献祭爆基伤（召唤加成前）</summary>
        internal const int ClotDamage = 250;

        //==================== 状态 ====================

        private const int StateEmerge = 0;
        private const int StateFollow = 1;
        private const int StateBlink = 2;
        private const int StateSacrifice = 3;
        private const int StateDissolve = 4;

        private int State { get => (int)Projectile.ai[0]; set => Projectile.ai[0] = value; }
        private ref float StateTimer => ref Projectile.ai[1];
        /// <summary>状态内子参数：献祭=被选中的凝块槽位，其余状态未用</summary>
        private ref float StateParam => ref Projectile.ai[2];

        //==================== 时序 ====================

        //出水：水下心音→破水→升起凝实→觉醒（假身裂出）
        private const int OmenFrames = 40;
        private const int RiseEnd = 80;
        private const int SettleEnd = 92;
        private const int AwakenFrame = 94;
        private const int EmergeTotal = 112;

        /// <summary>跟随态心跳周期（约 66bpm 的沉稳心律）</summary>
        private const int FollowBeatPeriod = 54;

        //闪现环绕：预备收缩→四个心跳拍循环（汇聚预告→跳位→压迫窗）→退场闪回
        private const int BlinkPrelude = 18;
        private const int BlinkCycleLen = 26;
        private const int BlinkFireTick = 10;
        private const int BlinkCount = 4;
        private const int BlinkExitTick = BlinkPrelude + BlinkCycleLen * BlinkCount;
        private const int BlinkRecoverEnd = BlinkExitTick + 8;
        private const float BlinkRingRadius = 150f;
        /// <summary>连跳环角步长：五角星式错位环绕，四跳绕满一圈半</summary>
        private const float BlinkRingStep = 2.51f;

        //献祭爆：深度心缩挤压凝块→释放拍（凝块脱轨出膛）→舒张回摆
        private const int SacrificeRelease = 30;
        private const int SacrificeEnd = 52;

        private const int DissolveFrames = 58;

        //==================== 血凝块卫星 ====================

        private const int ClotSlots = 4;
        private const float OrbitRadius = 88f;
        /// <summary>献祭后凝块重凝帧数</summary>
        private const int RegrowFrames = 300;
        /// <summary>重凝收尾的可见凝聚段</summary>
        private const int RegrowVisual = 40;

        //==================== 本地表现量（不入同步，节拍闩防快照回卷重播）====================

        private int frameTick;
        private int frameIndex;
        private int attackCooldown;
        private int attackIndex;
        private int lastSeenState = -1;
        private bool breachDone;
        private bool awakenDone;
        private int awakenFlash;
        private bool dissolveSplashed;

        //心跳引擎：pulseClock=距上一拍的帧数，驱动心缩曲线/血脉潮红/轨道涌动
        private int pulseClock = 99;
        private float lastBeatStrength;
        private float dubPending;
        private float veinFlush;
        private float orbitKick;
        private float materializePulse;

        //闪现环绕的本地演出量
        private int telegraphedBlink = -1;
        private int lastBlinkFired = -1;
        private bool blinkBaseSet;
        private float blinkBaseAngle;
        private Vector2 blinkDest;
        private Vector2 blinkRingCenter;

        //献祭的本地演出量
        private bool sacrificeLaunched;
        private float tighten;
        private float sacrificeSpin;

        //凝块轨道（各端按状态+Seed 本地重建）
        private float orbitClock;
        private float blinkHuddle;
        private readonly int[] clotRegrow = new int[ClotSlots];
        private readonly Vector2[] clotPos = new Vector2[ClotSlots];
        private readonly float[] clotAng = new float[ClotSlots];
        private readonly bool[] clotArrived = new bool[ClotSlots];
        private readonly bool[] clotPopped = new bool[ClotSlots];
        private bool clotPosInit;

        //假身镜像（纯本地表现）
        private float mirageReveal;
        private readonly Vector2[] miragePos = new Vector2[2];
        private readonly float[] mirageAlpha = new float[2];
        private readonly int[] mirageLungeTimer = new int[2];
        private readonly int[] mirageLungePhase = new int[2] { -1, -1 };
        private readonly bool[] mirageCrumbled = new bool[2];

        /// <summary>血形残壳：闪现旧位留下的一瞬空壳，几拍后碎成血珠</summary>
        private class ShellSnap
        {
            public Vector2 Pos;
            public float Rot;
            public int Frame;
            public float Scale;
            public int Age;
            public bool Crumbled;
        }
        private readonly List<ShellSnap> shells = new();
        /// <summary>上一帧的身位：远端可能先收包再跑 AI，捕壳要用包改位前的旧位</summary>
        private Vector2 prevCenter;

        //血系配色随观看域鬼雨异化冷化，与湖系同族
        private static Color BloodMain => KikasaDomain.CoolTint(new(237, 77, 69), new(126, 158, 164));
        private static Color BloodDeep => KikasaDomain.CoolTint(new(140, 32, 30), new(84, 104, 110));
        private static Color VeinGlow => KikasaDomain.CoolTint(new(246, 133, 112), new(176, 200, 204));
        private static Color MistBlood => KikasaDomain.CoolTint(new(58, 18, 20), new(52, 62, 66));

        private Player Owner => Main.player[Projectile.owner];

        /// <summary>连续量抖动的确定性相位，各端一致（9.1：不掷 Main.rand）</summary>
        private float Seed => Projectile.identity * 0.7391f;

        //==================== 召唤入口 ====================

        /// <summary>KikasaServantIndex 登记的召唤委托；emergeAt.Y = 湖面</summary>
        internal static void Summon(Player owner, Vector2 emergeAt) {
            if (owner.whoAmI != Main.myPlayer) {
                return;
            }
            int damage = (int)owner.GetTotalDamage(DamageClass.Summon).ApplyTo(BodyDamage);
            Projectile.NewProjectile(owner.GetSource_Misc("KikasaServant"),
                emergeAt + new Vector2(0f, 60f), Vector2.Zero,
                ModContent.ProjectileType<KikasaBrainServant>(), damage, 7f, owner.whoAmI);
        }

        //==================== 定义 ====================

        public override void SetStaticDefaults() {
            Main.projPet[Projectile.type] = true;
            ProjectileID.Sets.MinionSacrificable[Projectile.type] = false;
            //残壳与假身可能离本体半屏远，出屏也要画
            ProjectileID.Sets.DrawScreenCheckFluff[Projectile.type] = 1200;
        }

        public override void SetDefaults() {
            Projectile.width = 140;
            Projectile.height = 100;
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

        /// <summary>
        /// 接触伤害窗：闪现环绕只开在每拍跳位后的压迫段（本体裹上来的那几帧），
        /// 献祭全程开凝块卫星的搅动接触；其余常态 false
        /// </summary>
        public override bool? CanDamage() {
            if (State == StateBlink) {
                return BlinkMenaceWindow((int)StateTimer) ? null : false;
            }
            if (State == StateSacrifice) {
                return null;
            }
            return false;
        }

        /// <summary>闪现压迫窗判定：四拍循环内、跳位后的后半拍</summary>
        private static bool BlinkMenaceWindow(int t) {
            if (t < BlinkPrelude) {
                return false;
            }
            int k = (t - BlinkPrelude) / BlinkCycleLen;
            int ct = (t - BlinkPrelude) % BlinkCycleLen;
            return k < BlinkCount && ct >= BlinkFireTick;
        }

        /// <summary>命中体：闪现窗=本体大身板，献祭=各颗在轨凝块的圆域</summary>
        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            if (State == StateBlink) {
                return projHitbox.Intersects(targetHitbox);
            }
            if (State == StateSacrifice && clotPosInit) {
                for (int k = 0; k < ClotSlots; k++) {
                    if (clotRegrow[k] > 0) {
                        continue;
                    }
                    if (targetHitbox.Intersects(Utils.CenteredRectangle(clotPos[k], new Vector2(44f)))) {
                        return true;
                    }
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
            //还没破水就要收场：水下什么都没露出来，不走溶解演出
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

            //生命线：只有 owner 裁决——服务器无领域状态（既定契约），
            //迟入场客户端首份快照前也会误判；其余端只跟 owner 的同步包换场
            if (authority && State != StateDissolve && !LakeHealthy(owner, domain)) {
                BeginDissolve();
            }

            Projectile.timeLeft = 180;
            //伤害随召唤加成逐帧刷新：献祭态结算凝块价，其余结算本体价
            Projectile.damage = (int)owner.GetTotalDamage(DamageClass.Summon)
                .ApplyTo(State == StateSacrifice ? ClotDamage : BodyDamage);

            //换场清闩：远端可能靠收包换场而非本地同拍转场，残闩会吞掉新场节拍
            if (State != lastSeenState) {
                lastSeenState = State;
                telegraphedBlink = -1;
                lastBlinkFired = -1;
                blinkBaseSet = false;
                sacrificeLaunched = false;
                if (State == StateDissolve) {
                    dissolveSplashed = false;
                }
            }

            //心跳引擎节拍推进：dub 回声拍 + 潮红衰减
            pulseClock++;
            if (pulseClock == 9 && dubPending > 0f) {
                SoundEngine.PlaySound(SoundID.DD2_MonkStaffGroundImpact with {
                    Volume = 0.17f * dubPending,
                    Pitch = -0.7f,
                    MaxInstances = 2
                }, Projectile.Center);
                dubPending = 0f;
            }
            veinFlush *= 0.9f;
            materializePulse *= 0.85f;

            StateTimer++;
            switch (State) {
                case StateEmerge: UpdateEmerge(domain); break;
                case StateFollow: UpdateFollow(owner, domain, authority); break;
                case StateBlink: UpdateBlink(owner, authority); break;
                case StateSacrifice: UpdateSacrifice(owner, authority); break;
                case StateDissolve: UpdateDissolve(domain, authority); break;
            }

            UpdateOrbit();
            if (!Main.dedServ) {
                UpdateMirages(owner);
                UpdateShells();
            }
            UpdateFrames();
            if (attackCooldown > 0) {
                attackCooldown--;
            }

            //本体微倾：脑没有转向，只随横移轻轻侧身
            float tilt = MathHelper.Clamp(Projectile.velocity.X * 0.014f, -0.3f, 0.3f);
            Projectile.rotation = Projectile.rotation.AngleLerp(tilt, 0.12f);

            float glow = CurrentAlpha() * (0.4f + 0.7f * veinFlush);
            if (glow > 0.02f) {
                Lighting.AddLight(Projectile.Center, 0.42f * glow, 0.10f * glow, 0.09f * glow);
            }
            prevCenter = Projectile.Center;
        }

        private static bool LakeHealthy(Player owner, KikasaDomainPlayer domain)
            => !owner.dead && domain.AnyActive
            && domain.Phase != KikasaDomainPhase.Closing
            && domain.RiseT >= 0.9f;

        //==================== 心跳引擎 ====================

        /// <summary>
        /// 落一记心跳拍：重置心缩相位、闷鼓两层音（lub，9 帧后自动补 dub 回声）、
        /// 血脉潮红、凝块轨道涌动。underwater=水下闷音变体
        /// </summary>
        private void HeartbeatFX(float strength, bool underwater = false) {
            pulseClock = 0;
            lastBeatStrength = strength;
            dubPending = strength * (underwater ? 0.6f : 1f);
            veinFlush = MathF.Max(veinFlush, MathHelper.Clamp(strength, 0f, 1.3f));
            orbitKick = MathF.Min(orbitKick + 0.4f * strength, 1.6f);

            float vol = 0.3f * MathF.Min(strength, 1.3f) * (underwater ? 0.7f : 1f);
            SoundEngine.PlaySound(SoundID.DD2_MonkStaffGroundImpact with {
                Volume = vol,
                Pitch = underwater ? -1f : -0.9f,
                MaxInstances = 2
            }, Projectile.Center);
            SoundEngine.PlaySound(SoundID.NPCHit9 with {
                Volume = 0.2f * MathF.Min(strength, 1.3f),
                Pitch = -0.75f,
                MaxInstances = 2
            }, Projectile.Center);
        }

        /// <summary>心缩/心舒缩放曲线：lub 深缩、dub 浅缩、其后缓慢舒张回充</summary>
        private float PulseFactor(int clock) {
            float s = MathF.Min(lastBeatStrength, 1.2f);
            float dip = 0f;
            if (clock >= 0 && clock <= 6) {
                dip = 0.11f * MathF.Sin(clock / 6f * MathHelper.Pi) * s;
            }
            else if (clock >= 9 && clock <= 15) {
                dip = 0.055f * MathF.Sin((clock - 9) / 6f * MathHelper.Pi) * s;
            }
            float swell = 0.02f * (1f - MathF.Exp(-MathF.Max(clock, 0) / 30f));
            return 1f - dip + swell;
        }

        //==================== 出水：湖底心音 ====================

        private void UpdateEmerge(KikasaDomainPlayer domain) {
            float lakeY = domain.LakeWorldY;
            int t = (int)StateTimer;
            bool viewed = ViewedOwner;
            Vector2 surface = new(Projectile.Center.X, lakeY);

            if (t < OmenFrames) {
                //水下待命：先让湖面自己"心跳"起来——闷响与同心涟漪按拍走
                Projectile.velocity = Vector2.Zero;
                if (t == 10 || t == 32) {
                    HeartbeatFX(t == 10 ? 0.55f : 0.7f, underwater: true);
                    if (viewed) {
                        KikasaDomainDeco.RippleAt(surface, t == 10 ? 0.7f : 1.0f);
                    }
                }
                //dub 回声拍的小圈（lub 后 9 帧）
                if (viewed && t == 19) {
                    KikasaDomainDeco.RippleAt(surface, 0.4f);
                }
                return;
            }

            if (!breachDone) {
                //破水拍：一帧起速 + 浪冠 + 从水里闷出来的低吼，心跳在这拍第一次露出水面
                breachDone = true;
                Projectile.velocity = new Vector2(0f, -9.8f);
                HeartbeatFX(1.1f);
                SoundEngine.PlaySound(SoundID.Roar with { Volume = 0.42f, Pitch = -0.85f, MaxInstances = 2 }, Projectile.Center);
                if (viewed) {
                    BreachBurst(surface);
                }
            }

            //升起：起速后指数衰减，前快后慢
            Projectile.velocity.Y *= 0.94f;
            Projectile.velocity.X = 0f;

            if (viewed && t < RiseEnd) {
                //身上的血水成帘往下淌，落点连环小涟漪
                if (t % 2 == 0) {
                    PRTLoader.NewParticle<PRT_GhostRainDrop>(
                        Projectile.Center + new Vector2(Main.rand.NextFloat(-56f, 56f), Main.rand.NextFloat(8f, 40f)),
                        new Vector2(Main.rand.NextFloat(-0.4f, 0.4f), Main.rand.NextFloat(2.4f, 3.8f)),
                        BloodMain * Main.rand.NextFloat(0.4f, 0.6f),
                        Main.rand.NextFloat(0.45f, 0.7f))
                        ?.Configure(Main.rand.Next(14, 26), 0f);
                }
                if (t % 5 == 3) {
                    KikasaDomainDeco.RippleAt(
                        new Vector2(Projectile.Center.X + Main.rand.NextFloat(-30f, 30f), lakeY), 0.35f);
                }
            }

            //升起期的两记心跳：一记比一记近、比一记响
            if (t == 58 || t == 82) {
                HeartbeatFX(t == 58 ? 0.85f : 1f);
                if (viewed) {
                    KikasaDomainDeco.RippleAt(surface, 0.5f);
                }
            }

            if (!awakenDone && t >= AwakenFrame) {
                //觉醒拍：第一记完整 lub-dub 落定，两具假身从真身里裂出来
                awakenDone = true;
                awakenFlash = 10;
                HeartbeatFX(1.3f);
                SoundEngine.PlaySound(SoundID.NPCHit13 with { Volume = 0.4f, Pitch = -0.7f, MaxInstances = 2 }, Projectile.Center);
                if (viewed) {
                    KikasaDomainDeco.RippleAt(surface, 0.6f);
                    ShakeViewer(2f);
                    //裂出瞬间的血膜拉丝
                    for (int i = 0; i < 10; i++) {
                        PRTLoader.NewParticle<PRT_KikasaBloodGlob>(
                            Projectile.Center + Main.rand.NextVector2Circular(40f, 30f),
                            Main.rand.NextVector2CircularEdge(2.6f, 1.8f),
                            BloodMain * 0.55f, Main.rand.NextFloat(0.4f, 0.65f))
                            ?.Configure(Main.rand.Next(16, 28));
                    }
                }
            }

            if (t >= EmergeTotal) {
                //转场确定性（纯计时），各端同拍；owner 盖章纠偏
                State = StateFollow;
                StateTimer = 0;
                StateParam = 0;
                attackCooldown = 40;
                Projectile.netUpdate = Main.myPlayer == Projectile.owner;
            }
        }

        /// <summary>破水浪冠：环涟漪 + 扇形血珠 + 水柱束 + 血雾；凝块胚也在这拍甩出水面</summary>
        private void BreachBurst(Vector2 hit) {
            KikasaDomainDeco.RippleAt(hit, 2.3f);
            KikasaDomainDeco.RippleAt(hit + new Vector2(42f, 0f), 1.0f);
            KikasaDomainDeco.RippleAt(hit - new Vector2(38f, 0f), 0.95f);
            KikasaDomainDeco.SplashAt(hit + new Vector2(-18f, 0f), 12);
            KikasaDomainDeco.SplashAt(hit + new Vector2(18f, 0f), 12);

            //浪冠扇形血珠
            for (int i = 0; i < 24; i++) {
                float angle = -MathHelper.Pi * (0.12f + 0.76f * i / 23f);
                PRTLoader.NewParticle<PRT_KikasaBloodGlob>(
                    hit + new Vector2(Main.rand.NextFloat(-30f, 30f), -4f),
                    angle.ToRotationVector2() * Main.rand.NextFloat(3.2f, 7.8f),
                    Main.rand.NextBool(3) ? BloodDeep : BloodMain,
                    Main.rand.NextFloat(0.5f, 0.9f))?.Configure(Main.rand.Next(22, 38));
            }
            //四道凝块胚的斜抛水线：预告随后归轨的卫星
            for (int k = 0; k < ClotSlots; k++) {
                float side = k % 2 == 0 ? 1f : -1f;
                for (int i = 0; i < 4; i++) {
                    PRTLoader.NewParticle<PRT_KikasaBloodGlob>(
                        hit + new Vector2(side * (10f + k * 6f), -6f),
                        new Vector2(side * Main.rand.NextFloat(1.5f, 3f), -Main.rand.NextFloat(6f, 10f)),
                        BloodDeep, Main.rand.NextFloat(0.5f, 0.8f))?.Configure(Main.rand.Next(26, 40), 0.3f);
                }
            }
            for (int i = 0; i < 3; i++) {
                PRTLoader.NewParticle<PRT_GhostRainMist>(
                    hit + new Vector2(Main.rand.NextFloat(-34f, 34f), -10f),
                    new Vector2(Main.rand.NextFloat(-0.3f, 0.3f), -Main.rand.NextFloat(0.3f, 0.7f)),
                    MistBlood * 0.8f, Main.rand.NextFloat(0.7f, 1.05f))
                    ?.Configure(Main.rand.Next(60, 100));
            }

            //重拍层由破水那记心跳给，这里只补水声
            SoundEngine.PlaySound(SoundID.SplashWeak with { Volume = 1f, Pitch = -0.35f, MaxInstances = 2 }, hit);
            ShakeViewer(5f);
        }

        //==================== 跟随 ====================

        private void UpdateFollow(Player owner, KikasaDomainPlayer domain, bool authority) {
            int target = FindTarget(owner);

            //悬在主人侧上方，水母式呼吸浮动
            Vector2 anchor = owner.Center + new Vector2(-owner.direction * 110f, -138f);
            anchor.Y += MathF.Sin(Main.GlobalTimeWrappedHourly * 1.6f + Seed) * 8f;
            anchor.X += MathF.Sin(Main.GlobalTimeWrappedHourly * 0.9f + Seed * 2f) * 6f;

            Vector2 to = anchor - Projectile.Center;
            if (to.Length() > 2400f) {
                //跟丢硬贴回：连贴回都走闪现语法——旧位留壳，新位直接显形
                if (!Main.dedServ) {
                    AddShellAt(Projectile.Center);
                }
                Projectile.Center = anchor;
                Projectile.velocity = Vector2.Zero;
                Projectile.netUpdate = authority;
                return;
            }
            Vector2 desired = to * 0.08f;
            const float maxSpeed = 15f;
            if (desired.Length() > maxSpeed) {
                desired = desired.SafeNormalize(Vector2.Zero) * maxSpeed;
            }
            Projectile.velocity = Vector2.Lerp(Projectile.velocity, desired, 0.12f);

            //沉稳心律：约 66bpm，一拍一记闷鼓；贴近湖面时心跳会震出涟漪
            //（首拍压后 20 帧，别跟转场余韵撞在一起）
            if ((int)StateTimer % FollowBeatPeriod == 20) {
                HeartbeatFX(0.85f);
                if (ViewedOwner && MathF.Abs(domain.LakeWorldY - Projectile.Center.Y) < 220f) {
                    KikasaDomainDeco.RippleAt(new Vector2(Projectile.Center.X, domain.LakeWorldY), 0.45f);
                }
            }

            //轮廓下缘偶发凝珠滴落
            if (!Main.dedServ && Main.rand.NextBool(26)) {
                PRTLoader.NewParticle<PRT_GhostRainDrop>(
                    Projectile.Center + new Vector2(Main.rand.NextFloat(-46f, 46f), Main.rand.NextFloat(22f, 40f)),
                    new Vector2(0f, Main.rand.NextFloat(0.5f, 1.2f)),
                    BloodMain * Main.rand.NextFloat(0.4f, 0.55f),
                    Main.rand.NextFloat(0.35f, 0.6f))?.Configure(Main.rand.Next(20, 34), 0f);
            }

            //出手裁决：闪现环绕与献祭爆交替（凝块不足两颗就不献祭）；owner 盖章
            if (target >= 0 && attackCooldown <= 0 && StateTimer > 30) {
                attackIndex++;
                bool sacrifice = attackIndex % 2 == 0 && AliveClotCount() >= 2;
                if (sacrifice) {
                    State = StateSacrifice;
                    StateParam = LowestAliveClot();
                }
                else {
                    State = StateBlink;
                    StateParam = 0;
                }
                StateTimer = 0;
                Projectile.netUpdate = authority;
            }
        }

        //==================== 闪现环绕 ====================

        private void UpdateBlink(Player owner, bool authority) {
            int t = (int)StateTimer;
            int target = FindTarget(owner);

            if (t < BlinkPrelude) {
                //预备收缩：憋一口长气，目标没了就收势
                if (target < 0) {
                    EndAttack(authority, 45);
                    return;
                }
                Projectile.velocity *= 0.88f;
                //蓄势血珠向体心收拢，72% 后静默——爆发前的吸气
                if (!Main.dedServ && t < BlinkPrelude * 0.72f && t % 3 == 1) {
                    Vector2 from = Projectile.Center + Main.rand.NextVector2Unit() * Main.rand.NextFloat(60f, 110f);
                    PRTLoader.NewParticle<PRT_KikasaBloodGlob>(from,
                        (Projectile.Center - from) * 0.14f,
                        BloodMain * 0.5f, Main.rand.NextFloat(0.3f, 0.5f))?.Configure(9);
                }
                return;
            }

            //环角基准：由相对位取定（位置量已同步，各端一致）
            if (!blinkBaseSet) {
                blinkBaseSet = true;
                Vector2 refCenter = target >= 0 ? Main.npc[target].Center : Owner.Center;
                blinkBaseAngle = (Projectile.Center - refCenter).ToRotation();
            }

            //四拍循环
            if (t < BlinkExitTick) {
                int k = (t - BlinkPrelude) / BlinkCycleLen;
                int ct = (t - BlinkPrelude) % BlinkCycleLen;

                //拍首取落点：冻结在预告开始帧——预告要诚实
                if (telegraphedBlink < k) {
                    if (target < 0) {
                        EndAttack(authority, 60);
                        return;
                    }
                    telegraphedBlink = k;
                    blinkRingCenter = Main.npc[target].Center;
                    blinkDest = blinkRingCenter
                        + (blinkBaseAngle + k * BlinkRingStep).ToRotationVector2() * BlinkRingRadius;
                }

                if (ct < BlinkFireTick) {
                    //汇聚预告：落点处血珠向心收拢，本体屏息微颤
                    Projectile.velocity *= 0.9f;
                    if (!Main.dedServ && ct % 2 == 0) {
                        Vector2 from = blinkDest + Main.rand.NextVector2Unit() * Main.rand.NextFloat(40f, 76f);
                        PRTLoader.NewParticle<PRT_KikasaBloodGlob>(from,
                            (blinkDest - from) * 0.2f,
                            BloodMain * 0.55f, Main.rand.NextFloat(0.3f, 0.5f))?.Configure(8);
                    }
                    return;
                }

                if (lastBlinkFired < k) {
                    //跳位拍：旧位留壳、新位显形，心跳一拍比一拍急
                    lastBlinkFired = k;
                    FireBlink(blinkDest, (blinkRingCenter - blinkDest).SafeNormalize(Vector2.UnitY) * 6f,
                        authority, 1.05f + k * 0.05f);
                }

                //压迫段：贴着目标缓缓裹上去，速度自然泄劲
                Projectile.velocity *= 0.94f;
                return;
            }

            if (lastBlinkFired < BlinkCount) {
                //退场闪回：跳回主人身侧，无伤害窗
                lastBlinkFired = BlinkCount;
                Vector2 home = owner.Center + new Vector2(-owner.direction * 110f, -138f);
                FireBlink(home, Vector2.Zero, authority, 0.9f);
            }

            Projectile.velocity *= 0.9f;
            if (t >= BlinkRecoverEnd) {
                EndAttack(authority, 150);
            }
        }

        /// <summary>执行一次跳位：残壳、位移、显形收缩、心跳拍；owner 对落点盖章</summary>
        private void FireBlink(Vector2 dest, Vector2 newVelocity, bool authority, float beatStrength) {
            //旧位取上一帧身位：远端此帧可能已被同步包拽到落点，直接取 Center 会把壳留在新位
            Vector2 from = prevCenter == Vector2.Zero ? Projectile.Center : prevCenter;
            if (!Main.dedServ && Vector2.Distance(from, dest) > 30f) {
                AddShellAt(from);
            }
            Projectile.Center = dest;
            Projectile.velocity = newVelocity;
            Projectile.netUpdate = authority;
            materializePulse = 1f;
            HeartbeatFX(beatStrength);
            SoundEngine.PlaySound(SoundID.SplashWeak with { Volume = 0.3f, Pitch = 0.15f, MaxInstances = 3 }, dest);
            if (!Main.dedServ) {
                //显形：血珠被吸进新位的身体里
                for (int i = 0; i < 7; i++) {
                    Vector2 suck = dest + Main.rand.NextVector2Unit() * Main.rand.NextFloat(30f, 66f);
                    PRTLoader.NewParticle<PRT_KikasaBloodGlob>(suck,
                        (dest - suck) * 0.22f,
                        BloodMain * 0.6f, Main.rand.NextFloat(0.35f, 0.55f))?.Configure(7);
                }
                PRTLoader.NewParticle<PRT_DWave>(dest, Vector2.Zero, VeinGlow, 0.05f)
                    ?.Configure(new Vector2(1f, 1f), 0f, 0.22f, 8);
            }
            if (ViewedOwner) {
                ShakeViewer(0.9f);
            }
        }

        //==================== 献祭爆 ====================

        private void UpdateSacrifice(Player owner, bool authority) {
            int t = (int)StateTimer;
            int slot = Math.Clamp((int)StateParam, 0, ClotSlots - 1);
            int target = FindTarget(owner);

            if (t < SacrificeRelease) {
                //深度心缩：整颗脑攥紧，被选中的凝块拽向体心；跳一拍的死寂
                Projectile.velocity *= 0.9f;
                if (target < 0 && t < SacrificeRelease - 6) {
                    //目标没了且凝块还没挤出去：不白费一颗
                    EndAttack(authority, 50);
                    return;
                }
                //挤压期的向心血珠，72% 后静默
                if (!Main.dedServ && t < SacrificeRelease * 0.72f && t % 3 == 0 && clotPosInit) {
                    Vector2 mouth = clotPos[slot];
                    Vector2 from = mouth + Main.rand.NextVector2Unit() * Main.rand.NextFloat(36f, 80f);
                    PRTLoader.NewParticle<PRT_KikasaBloodGlob>(from,
                        (mouth - from) * 0.16f,
                        BloodDeep * 0.6f, Main.rand.NextFloat(0.28f, 0.48f))?.Configure(9);
                }
                return;
            }

            if (!sacrificeLaunched) {
                //释放拍：猛地一记舒张，凝块脱轨出膛；心跳最重的一记
                sacrificeLaunched = true;
                if (target < 0) {
                    EndAttack(authority, 50);
                    return;
                }
                NPC npc = Main.npc[target];
                Vector2 from = clotPosInit ? clotPos[slot] : Projectile.Center;
                Vector2 aim = (npc.Center + npc.velocity * 10f - from).SafeNormalize(-Vector2.UnitY);

                clotRegrow[slot] = RegrowFrames;
                materializePulse = 1f;
                HeartbeatFX(1.4f);
                //后坐：知重量者先退半步
                Projectile.velocity = -aim * 3.5f;
                Projectile.netUpdate = authority;
                SoundEngine.PlaySound(SoundID.Item95 with { Volume = 0.5f, Pitch = -0.4f, MaxInstances = 2 }, from);
                SoundEngine.PlaySound(SoundID.DD2_WyvernDiveDown with { Volume = 0.4f, Pitch = -0.5f, MaxInstances = 2 }, from);
                if (!Main.dedServ) {
                    //出膛喷吐
                    for (int i = 0; i < 8; i++) {
                        PRTLoader.NewParticle<PRT_KikasaBloodGlob>(from + Main.rand.NextVector2Circular(6f, 6f),
                            aim.RotatedByRandom(0.3f) * Main.rand.NextFloat(2.5f, 7f),
                            Main.rand.NextBool(3) ? BloodDeep : BloodMain,
                            Main.rand.NextFloat(0.4f, 0.7f))?.Configure(Main.rand.Next(14, 24));
                    }
                    PRTLoader.NewParticle<PRT_DWave>(from, Vector2.Zero, BloodDeep, 0.07f)
                        ?.Configure(new Vector2(0.6f, 1f), aim.ToRotation(), 0.24f, 9);
                }
                if (ViewedOwner) {
                    ShakeViewer(3f);
                }
                //凝块弹体只在 owner 端生成，spawn 参数自带全部初值
                if (authority) {
                    int damage = (int)owner.GetTotalDamage(DamageClass.Summon).ApplyTo(ClotDamage);
                    Projectile.NewProjectile(Projectile.GetSource_FromAI(), from, aim * 17f,
                        ModContent.ProjectileType<KikasaBrainClot>(), damage, 4f,
                        Projectile.owner, target);
                }
                return;
            }

            //舒张回摆
            Projectile.velocity *= 0.93f;
            if (t >= SacrificeEnd) {
                EndAttack(authority, 190);
            }
        }

        private void EndAttack(bool authority, int cooldown) {
            State = StateFollow;
            StateTimer = 0;
            StateParam = 0;
            attackCooldown = cooldown;
            Projectile.netUpdate = authority;
        }

        //==================== 溶解回湖 ====================

        private void UpdateDissolve(KikasaDomainPlayer domain, bool authority) {
            int t = (int)StateTimer;
            bool lakeAlive = domain.AnyActive && domain.RiseT > 0.5f;
            float lakeY = domain.LakeWorldY;

            if (lakeAlive) {
                //沉回湖里
                Projectile.velocity.X *= 0.92f;
                Projectile.velocity.Y = MathF.Min(Projectile.velocity.Y + 0.22f, 7f);
            }
            else {
                //湖已不在：原地化水
                Projectile.velocity *= 0.9f;
            }

            //心脏正在停跳：拍与拍之间越拉越长、越敲越轻，最后一记几乎听不见
            if (t == 10) {
                HeartbeatFX(0.7f);
            }
            else if (t == 34) {
                HeartbeatFX(0.45f);
            }
            else if (t == 52) {
                HeartbeatFX(0.22f);
            }

            //在轨凝块失去心跳供压，错拍碎成血珠
            if (!Main.dedServ && clotPosInit) {
                for (int k = 0; k < ClotSlots; k++) {
                    if (!clotPopped[k] && clotRegrow[k] <= 0 && t >= 8 + k * 5) {
                        clotPopped[k] = true;
                        for (int i = 0; i < 5; i++) {
                            PRTLoader.NewParticle<PRT_KikasaBloodGlob>(
                                clotPos[k] + Main.rand.NextVector2Circular(8f, 8f),
                                Main.rand.NextVector2Circular(1.6f, 1.6f) + Vector2.UnitY * 1.2f,
                                BloodMain * 0.55f, Main.rand.NextFloat(0.32f, 0.55f))
                                ?.Configure(Main.rand.Next(14, 24), 0.3f);
                        }
                        SoundEngine.PlaySound(SoundID.Drip with { Volume = 0.35f, Pitch = -0.4f, MaxInstances = 2 }, clotPos[k]);
                    }
                }
            }

            //过水线拍（一次）
            if (lakeAlive && !dissolveSplashed && Projectile.Center.Y >= lakeY) {
                dissolveSplashed = true;
                SoundEngine.PlaySound(SoundID.SplashWeak with { Volume = 0.7f, Pitch = -0.35f, MaxInstances = 2 }, Projectile.Center);
                if (ViewedOwner) {
                    Vector2 hit = new(Projectile.Center.X, lakeY);
                    KikasaDomainDeco.SplashAt(hit, 10);
                    KikasaDomainDeco.RippleAt(hit, 1.4f);
                    ShakeViewer(2f);
                }
            }

            //边沉边化成血珠
            if (!Main.dedServ && t % 2 == 0 && CurrentAlpha() > 0.15f) {
                PRTLoader.NewParticle<PRT_GhostRainDrop>(
                    Projectile.Center + Main.rand.NextVector2Circular(50f, 34f),
                    new Vector2(Main.rand.NextFloat(-0.6f, 0.6f), Main.rand.NextFloat(1.5f, 3f)),
                    BloodMain * 0.5f, Main.rand.NextFloat(0.35f, 0.6f))
                    ?.Configure(Main.rand.Next(12, 22), 0f);
            }

            //owner 到点收场；远端多给 10 帧等 kill 包，兜底自杀
            if (authority && t >= DissolveFrames) {
                Projectile.Kill();
            }
            else if (!authority && t >= DissolveFrames + 10) {
                Projectile.Kill();
            }
        }

        //==================== 血凝块卫星轨道（各端本地重建）====================

        private void UpdateOrbit() {
            //重凝倒计时
            for (int k = 0; k < ClotSlots; k++) {
                if (clotRegrow[k] > 0) {
                    clotRegrow[k]--;
                }
            }

            //心跳涌动：每记搏动把血推过轨道，凝块随之加速一阵
            orbitKick *= 0.92f;
            orbitClock += 0.024f + orbitKick * 0.035f;

            //闪现态收拢贴身，跟着本体一起跳
            float huddleTarget = State == StateBlink ? 1f : 0f;
            blinkHuddle += (huddleTarget - blinkHuddle) * 0.15f;

            //献祭态被选中的凝块被拽向体心、转速抬升
            if (State == StateSacrifice && !sacrificeLaunched) {
                tighten = MathF.Min(StateTimer / (float)SacrificeRelease, 1f);
                sacrificeSpin += tighten * 0.09f;
            }
            else {
                tighten *= 0.85f;
                if (tighten < 0.02f) {
                    sacrificeSpin *= 0.9f;
                }
            }

            int tightSlot = State == StateSacrifice ? Math.Clamp((int)StateParam, 0, ClotSlots - 1) : -1;
            for (int k = 0; k < ClotSlots; k++) {
                float ang = orbitClock + Seed * 2.3f + k * MathHelper.TwoPi / ClotSlots;
                float radius = OrbitRadius + 7f * MathF.Sin(Main.GlobalTimeWrappedHourly * 1.3f + k * 1.9f);
                radius = MathHelper.Lerp(radius, 60f, blinkHuddle);
                if (k == tightSlot) {
                    ang += sacrificeSpin;
                    radius = MathHelper.Lerp(radius, 40f, tighten);
                }
                clotAng[k] = ang;
                Vector2 off = new(MathF.Cos(ang) * radius, MathF.Sin(ang) * radius * 0.62f);
                clotPos[k] = Projectile.Center + off;

                //出水期从水下爬进轨道
                float et = EmergeClotT(k);
                if (et < 1f) {
                    clotPos[k] = Vector2.Lerp(clotPos[k] + new Vector2(0f, 120f), clotPos[k], SmoothStep01(et));
                }
                else if (!clotArrived[k] && State == StateEmerge) {
                    //归轨拍：一声小小的湿咬合
                    clotArrived[k] = true;
                    SoundEngine.PlaySound(SoundID.NPCHit9 with { Volume = 0.22f, Pitch = -0.3f, MaxInstances = 2 }, clotPos[k]);
                }

                //重凝收尾：血珠向槽位汇聚
                if (!Main.dedServ && clotRegrow[k] > 0 && clotRegrow[k] <= RegrowVisual && clotRegrow[k] % 4 == 0) {
                    Vector2 from = clotPos[k] + Main.rand.NextVector2Unit() * Main.rand.NextFloat(24f, 50f);
                    PRTLoader.NewParticle<PRT_KikasaBloodGlob>(from,
                        (clotPos[k] - from) * 0.18f,
                        BloodDeep * 0.55f, Main.rand.NextFloat(0.26f, 0.42f))?.Configure(8);
                }
            }
            clotPosInit = true;
        }

        /// <summary>出水期凝块归轨进度：破水后错拍逐颗爬升</summary>
        private float EmergeClotT(int k) {
            if (State != StateEmerge) {
                return 1f;
            }
            return MathHelper.Clamp((StateTimer - 48f - k * 8f) / 28f, 0f, 1f);
        }

        private int AliveClotCount() {
            int n = 0;
            for (int k = 0; k < ClotSlots; k++) {
                if (clotRegrow[k] <= 0) {
                    n++;
                }
            }
            return n;
        }

        private int LowestAliveClot() {
            for (int k = 0; k < ClotSlots; k++) {
                if (clotRegrow[k] <= 0) {
                    return k;
                }
            }
            return 0;
        }

        //==================== 假身镜像（纯本地表现）====================

        private void UpdateMirages(Player owner) {
            //觉醒后才有假身；溶解时最先碎掉
            if (State == StateEmerge) {
                mirageReveal = awakenDone ? MathF.Min(mirageReveal + 1f / 16f, 1f) : 0f;
            }
            else if (State == StateDissolve) {
                for (int i = 0; i < 2; i++) {
                    if (!mirageCrumbled[i] && StateTimer >= 4 + i * 3) {
                        mirageCrumbled[i] = true;
                        MirageCrumble(miragePos[i]);
                    }
                }
                mirageReveal = MathF.Max(mirageReveal - 0.2f, 0f);
            }
            else {
                mirageReveal = MathF.Min(mirageReveal + 1f / 16f, 1f);
            }

            int target = FindTarget(owner);
            for (int i = 0; i < 2; i++) {
                Vector2 basePos = MirageBasePos(i, target);
                float wantAlpha = 0.5f * mirageReveal;

                //跟随态的虚张声势：假身按各自的节拍间歇扑向猎物，接触前碎成血珠再回位重凝
                if (State == StateFollow && mirageReveal >= 1f && target >= 0) {
                    if (mirageLungePhase[i] < 0) {
                        if (mirageLungeTimer[i] <= 0) {
                            //首次装填：两具假身错开出手节拍
                            mirageLungeTimer[i] = MirageLungeInterval(i) + (i + 1) * 47;
                        }
                        else if (--mirageLungeTimer[i] <= 0) {
                            mirageLungePhase[i] = 0;
                        }
                    }
                }
                else if (mirageLungePhase[i] >= 0 && mirageLungePhase[i] < 22) {
                    //换场打断未完成的扑击
                    mirageLungePhase[i] = -1;
                    mirageLungeTimer[i] = MirageLungeInterval(i);
                }

                if (mirageLungePhase[i] >= 0) {
                    int lp = mirageLungePhase[i]++;
                    if (lp < 22 && target >= 0) {
                        //扑近：pow 曲线迟发猛扑，在目标身前 70 像素处止步
                        Vector2 stop = Main.npc[target].Center
                            - (Main.npc[target].Center - basePos).SafeNormalize(Vector2.UnitX) * 70f;
                        miragePos[i] = Vector2.Lerp(basePos, stop, MathF.Pow(lp / 22f, 3f));
                        if (lp == 21) {
                            MirageCrumble(miragePos[i]);
                        }
                    }
                    else if (lp < 30) {
                        //碎裂后的空窗
                        wantAlpha = 0f;
                    }
                    else if (lp < 42) {
                        //回到位重新凝出来
                        miragePos[i] = basePos;
                        wantAlpha = 0.5f * mirageReveal * ((lp - 30) / 12f);
                    }
                    else {
                        mirageLungePhase[i] = -1;
                        mirageLungeTimer[i] = MirageLungeInterval(i);
                    }
                    if (lp < 22) {
                        mirageAlpha[i] = wantAlpha;
                        continue;
                    }
                }
                else {
                    //常态贴向确定性基准位：闪现拍上会硬跳，读作一起闪现
                    miragePos[i] = miragePos[i] == Vector2.Zero
                        ? basePos : Vector2.Lerp(miragePos[i], basePos, 0.45f);
                }
                mirageAlpha[i] += (wantAlpha - mirageAlpha[i]) * 0.3f;
            }

            //觉醒前假身蜷在真身体内：觉醒帧起沿阻尼滑向各自基准位，读作"从真身裂出"
            if (State == StateEmerge && !awakenDone) {
                miragePos[0] = miragePos[1] = Projectile.Center;
            }
        }

        /// <summary>假身基准位：常态绕真身对称慢旋；闪现拍则镜像到目标环上——三影围心，只有一颗是真的</summary>
        private Vector2 MirageBasePos(int i, int target) {
            if (State == StateBlink && lastBlinkFired >= 0 && lastBlinkFired < BlinkCount) {
                float ang = blinkBaseAngle + lastBlinkFired * BlinkRingStep
                    + MathHelper.TwoPi / 3f * (i + 1);
                return blinkRingCenter + ang.ToRotationVector2() * BlinkRingRadius;
            }
            float baseAng = Seed * 1.9f + MathF.Sin(Main.GlobalTimeWrappedHourly * 0.5f + i * 2.6f) * 0.6f
                + i * MathHelper.Pi + MathHelper.PiOver2;
            float radius = 112f + 10f * MathF.Sin(Main.GlobalTimeWrappedHourly * 0.8f + i * 1.7f);
            return Projectile.Center + new Vector2(MathF.Cos(baseAng) * radius, MathF.Sin(baseAng) * radius * 0.8f);
        }

        private int MirageLungeInterval(int i)
            => 160 + ((int)(Seed * 977f) + i * 53) % 110;

        /// <summary>假身碎裂：血水镜像散成一蓬珠，虚张声势的谢幕</summary>
        private void MirageCrumble(Vector2 pos) {
            for (int j = 0; j < 8; j++) {
                PRTLoader.NewParticle<PRT_KikasaBloodGlob>(
                    pos + Main.rand.NextVector2Circular(30f, 24f),
                    Main.rand.NextVector2Circular(2.2f, 2.2f),
                    BloodMain * 0.5f, Main.rand.NextFloat(0.32f, 0.55f))
                    ?.Configure(Main.rand.Next(14, 26), 0.25f);
            }
            SoundEngine.PlaySound(SoundID.NPCHit9 with { Volume = 0.22f, Pitch = -0.15f, MaxInstances = 2 }, pos);
        }

        //==================== 血形残壳 ====================

        /// <summary>在指定位置留一具残壳：保持这一帧的姿态，几拍后碎裂</summary>
        private void AddShellAt(Vector2 at) {
            shells.Add(new ShellSnap {
                Pos = at,
                Rot = Projectile.rotation,
                Frame = frameIndex + (AgitatedFrames() ? 4 : 0),
                Scale = BodyScale(),
            });
            if (shells.Count > 4) {
                shells.RemoveAt(0);
            }
        }

        private void UpdateShells() {
            for (int i = shells.Count - 1; i >= 0; i--) {
                ShellSnap s = shells[i];
                s.Age++;
                if (!s.Crumbled && s.Age >= 9) {
                    //壳撑不住形：碎成向下的一蓬血珠
                    s.Crumbled = true;
                    for (int j = 0; j < 6; j++) {
                        PRTLoader.NewParticle<PRT_KikasaBloodGlob>(
                            s.Pos + Main.rand.NextVector2Circular(44f, 32f),
                            new Vector2(Main.rand.NextFloat(-1f, 1f), Main.rand.NextFloat(0.6f, 2.2f)),
                            BloodDeep * 0.55f, Main.rand.NextFloat(0.35f, 0.6f))
                            ?.Configure(Main.rand.Next(14, 24), 0.3f);
                    }
                    SoundEngine.PlaySound(SoundID.NPCHit13 with { Volume = 0.25f, Pitch = -0.5f, MaxInstances = 2 }, s.Pos);
                }
                if (s.Age > 16) {
                    shells.RemoveAt(i);
                }
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
            if (awakenFlash > 0) {
                awakenFlash--;
            }
            if (++frameTick >= (AgitatedFrames() ? 4 : 7)) {
                frameTick = 0;
                frameIndex = (frameIndex + 1) % 4;
            }
        }

        /// <summary>裸脑帧窗：压迫窗/献祭挤压后段/觉醒闪帧/溶解前段用二阶段狰狞相</summary>
        private bool AgitatedFrames() {
            if (awakenFlash > 0) {
                return true;
            }
            return State switch {
                StateBlink => BlinkMenaceWindow((int)StateTimer),
                StateSacrifice => StateTimer > 12,
                StateDissolve => StateTimer < 30,
                _ => false,
            };
        }

        private bool ViewedOwner
            => KikasaDomain.Viewed != null && KikasaDomain.Viewed.Player.whoAmI == Projectile.owner;

        private static void ShakeViewer(float amount)
            => Main.LocalPlayer?.CWR()?.GetScreenShake(amount);

        private static float SmoothStep01(float t) => t * t * (3f - 2f * t);

        //==================== 表现参数 ====================

        private float CurrentAlpha() {
            int t = (int)StateTimer;
            return State switch {
                StateEmerge => t < OmenFrames ? 0f : MathHelper.Clamp((t - OmenFrames) / 5f, 0f, 1f),
                StateDissolve => MathHelper.Clamp((DissolveFrames - t) / 14f, 0f, 1f),
                _ => 1f,
            };
        }

        /// <summary>uForm：1=全血水 0=真身；常态半沉呼吸，出水自上而下凝实，溶解回血水</summary>
        private float CurrentForm() {
            int t = (int)StateTimer;
            float steady = 0.34f + MathF.Sin(Main.GlobalTimeWrappedHourly * 2.4f + Seed) * 0.05f;
            return State switch {
                StateEmerge => t < OmenFrames
                    ? 1f
                    : MathHelper.Lerp(1f, steady, SmoothStep01(MathHelper.Clamp((t - OmenFrames) / (float)(RiseEnd - OmenFrames), 0f, 1f))),
                StateDissolve => MathHelper.Clamp(steady + t / (float)DissolveFrames * 0.3f, 0f, 1f),
                _ => steady,
            };
        }

        /// <summary>uScanMode：出水期自上而下扫描凝实，落定后渐变回噪声斑驳半沉态</summary>
        private float CurrentScanMode() {
            if (State != StateEmerge) {
                return 0f;
            }
            int t = (int)StateTimer;
            if (t <= RiseEnd) {
                return 1f;
            }
            return 1f - MathHelper.Clamp((t - RiseEnd) / (float)(SettleEnd - RiseEnd), 0f, 1f);
        }

        private float CurrentDissolve()
            => State == StateDissolve
                ? MathF.Pow(MathHelper.Clamp(StateTimer / 44f, 0f, 1f), 0.9f)
                : 0f;

        /// <summary>本体缩放：心缩曲线 × 状态修正（预备收缩/深度心缩/显形反冲/破水过冲）</summary>
        private float BodyScale() {
            float scale = 0.9f * PulseFactor(pulseClock);
            int t = (int)StateTimer;
            if (State == StateBlink && t < BlinkPrelude) {
                scale *= 1f - 0.14f * MathF.Pow(t / (float)BlinkPrelude, 3f);
            }
            else if (State == StateSacrifice && t < SacrificeRelease) {
                scale *= 1f - 0.18f * MathF.Pow(t / (float)SacrificeRelease, 2f);
            }
            else if (State == StateEmerge && t >= OmenFrames && t < OmenFrames + 12) {
                scale *= 1f + 0.1f * (1f - (t - OmenFrames) / 12f);
            }
            scale *= 1f + 0.12f * materializePulse;
            return scale;
        }

        //==================== 绘制 ====================

        public override bool PreDraw(ref Color lightColor) {
            Main.instance.LoadNPC(NPCID.BrainofCthulhu);
            Main.instance.LoadNPC(NPCID.Creeper);
            Texture2D brainTex = TextureAssets.Npc[NPCID.BrainofCthulhu]?.Value;
            Texture2D clotTex = TextureAssets.Npc[NPCID.Creeper]?.Value;
            if (brainTex == null || clotTex == null) {
                return false;
            }
            int frameH = brainTex.Height / Main.npcFrameCount[NPCID.BrainofCthulhu];
            Rectangle bodyFrame = new(0, frameH * (frameIndex + (AgitatedFrames() ? 4 : 0)), brainTex.Width, frameH);
            Rectangle clotFrame = new(0, 0, clotTex.Width, clotTex.Height);
            float alpha = CurrentAlpha();
            SpriteBatch sb = Main.spriteBatch;

            //残壳：主批直接画，深血半透，保持跳位那一帧的姿态
            DrawShells(sb, brainTex, frameH);

            //血湖材质批：后景凝块→假身→本体→前景凝块
            if (alpha > 0.01f) {
                DrawBloodForms(sb, brainTex, bodyFrame, clotTex, clotFrame, alpha);
            }

            //加色层：水下心音/汇聚预告/血脉潮红/献祭挤压
            DrawGlow(sb, alpha);

            return false;
        }

        private void DrawShells(SpriteBatch sb, Texture2D tex, int frameH) {
            foreach (ShellSnap s in shells) {
                float fade = 1f - s.Age / 16f;
                if (fade <= 0f) {
                    continue;
                }
                Rectangle frame = new(0, frameH * s.Frame, tex.Width, frameH);
                //壳在原位撑住形又微微涨开，随即失压
                float swell = s.Scale * (1f + s.Age * 0.006f);
                sb.Draw(tex, s.Pos - Main.screenPosition, frame,
                    BloodDeep * (0.5f * fade), s.Rot, frame.Size() * 0.5f, swell, SpriteEffects.None, 0f);
            }
        }

        private void DrawBloodForms(SpriteBatch sb, Texture2D brainTex, Rectangle bodyFrame,
            Texture2D clotTex, Rectangle clotFrame, float alpha) {
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

            //逐份套参：seed 区分蚀纹，uForm 区分凝实度
            void ApplyForm(Texture2D tex, Rectangle frame, float seed, float uForm, float dissolve, float scan) {
                form.Parameters["uSeed"]?.SetValue(seed);
                form.Parameters["uForm"]?.SetValue(uForm);
                form.Parameters["uDissolve"]?.SetValue(dissolve);
                form.Parameters["uScanMode"]?.SetValue(scan);
                form.Parameters["uUvRect"]?.SetValue(new Vector4(
                    frame.X / (float)tex.Width, frame.Y / (float)tex.Height,
                    frame.Width / (float)tex.Width, frame.Height / (float)tex.Height));
                form.Parameters["uTexel"]?.SetValue(new Vector2(1f / tex.Width, 1f / tex.Height));
                form.Parameters["uAspect"]?.SetValue(frame.Width / (float)frame.Height);
                form.CurrentTechnique.Passes[0].Apply();
            }

            //凝块（后景半圈）
            void DrawClot(int k, bool front) {
                if (clotRegrow[k] > RegrowVisual || clotPopped[k]) {
                    return;
                }
                if (MathF.Sin(clotAng[k]) > 0f != front) {
                    return;
                }
                float et = EmergeClotT(k);
                float regrowScale = clotRegrow[k] > 0 ? 1f - clotRegrow[k] / (float)RegrowVisual : 1f;
                float cAlpha = alpha * et * regrowScale;
                if (cAlpha <= 0.02f) {
                    return;
                }
                float cScale = 0.82f * regrowScale * (1f + 0.05f * MathF.Sin(orbitClock * 4f + k * 2.2f));
                float rot = clotAng[k];
                Color color;
                if (shaderOk) {
                    ApplyForm(clotTex, clotFrame, Seed + 3.1f + k * 1.7f,
                        0.5f + 0.05f * MathF.Sin(Main.GlobalTimeWrappedHourly * 2f + k),
                        CurrentDissolve(), 0f);
                    color = new Color(255, 255, 255, (byte)(cAlpha * 255f));
                }
                else {
                    color = Color.Lerp(Color.White, BloodMain, 0.55f) * cAlpha;
                }
                sb.Draw(clotTex, clotPos[k] - Main.screenPosition, clotFrame, color,
                    rot, clotFrame.Size() * 0.5f, cScale, SpriteEffects.None, 0f);
            }

            if (clotPosInit) {
                for (int k = 0; k < ClotSlots; k++) {
                    DrawClot(k, front: false);
                }
            }

            //假身：低凝实血水镜像（uForm 拉高），错拍脉动
            if (!Main.dedServ && mirageReveal > 0.02f) {
                for (int i = 0; i < 2; i++) {
                    float mAlpha = mirageAlpha[i] * alpha;
                    if (mAlpha <= 0.03f || miragePos[i] == Vector2.Zero) {
                        continue;
                    }
                    float mScale = 0.85f * PulseFactor(pulseClock - 6 * (i + 1));
                    Color color;
                    if (shaderOk) {
                        ApplyForm(brainTex, bodyFrame, Seed + 7.7f * (i + 1),
                            0.86f + MathF.Sin(Main.GlobalTimeWrappedHourly * 2.2f + i * 2f) * 0.04f,
                            CurrentDissolve(), 0f);
                        color = new Color(255, 255, 255, (byte)(mAlpha * 255f));
                    }
                    else {
                        color = BloodMain * (mAlpha * 0.8f);
                    }
                    sb.Draw(brainTex, miragePos[i] - Main.screenPosition, bodyFrame, color,
                        -Projectile.rotation * 0.6f, bodyFrame.Size() * 0.5f, mScale,
                        i == 0 ? SpriteEffects.FlipHorizontally : SpriteEffects.None, 0f);
                }
            }

            //真身
            if (alpha > 0.01f) {
                Color bodyColor;
                if (shaderOk) {
                    ApplyForm(brainTex, bodyFrame, Seed, CurrentForm(), CurrentDissolve(), CurrentScanMode());
                    bodyColor = new Color(255, 255, 255, (byte)(alpha * 255f));
                }
                else {
                    bodyColor = Color.Lerp(Color.White, BloodMain, 0.55f) * alpha;
                }
                sb.Draw(brainTex, Projectile.Center - Main.screenPosition, bodyFrame, bodyColor,
                    Projectile.rotation, bodyFrame.Size() * 0.5f, BodyScale(), SpriteEffects.None, 0f);
            }

            //凝块（前景半圈）压在本体上，轨道有前后
            if (clotPosInit) {
                for (int k = 0; k < ClotSlots; k++) {
                    DrawClot(k, front: true);
                }
            }

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
        }

        private void DrawGlow(SpriteBatch sb, float alpha) {
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            Texture2D ring = CWRAsset.DiffusionCircle?.Value;
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

            //水下心音：湖面下的血光随每记闷响涨缩——看不见形，先看见搏动
            if (State == StateEmerge && t < OmenFrames) {
                float ot = t / (float)OmenFrames;
                Vector2 pos = new(Projectile.Center.X, domain.LakeWorldY + MathHelper.Lerp(46f, 10f, ot));
                float a = 0.16f + 0.4f * veinFlush;
                float r = (30f + 26f * ot) * (1f + 0.2f * veinFlush);
                EnsureBegin();
                sb.Draw(glow, pos - Main.screenPosition, null, VeinGlow * a, 0f,
                    gOrigin, new Vector2(r * 2.8f / glow.Width, r * 1.1f / glow.Height), SpriteEffects.None, 0f);
            }

            //闪现汇聚预告：真落点亮环收拢，两处假落点淡一档——三影同摆，虚实难辨
            if (State == StateBlink && telegraphedBlink >= 0 && lastBlinkFired < telegraphedBlink + 1
                && t >= BlinkPrelude && t < BlinkExitTick) {
                int ct = (t - BlinkPrelude) % BlinkCycleLen;
                if (ct < BlinkFireTick) {
                    float p = ct / (float)BlinkFireTick;
                    float a = MathF.Sin(p * MathHelper.Pi) * 0.55f;
                    EnsureBegin();
                    for (int i = 0; i < 3; i++) {
                        Vector2 pos = i == 0 ? blinkDest
                            : blinkRingCenter + (blinkBaseAngle + telegraphedBlink * BlinkRingStep
                                + MathHelper.TwoPi / 3f * i).ToRotationVector2() * BlinkRingRadius;
                        float mul = i == 0 ? 1f : 0.38f;
                        float r = MathHelper.Lerp(64f, 20f, p);
                        if (ring != null) {
                            sb.Draw(ring, pos - Main.screenPosition, null, BloodMain * (a * 0.7f * mul), 0f,
                                ring.Size() * 0.5f, new Vector2(r * 2f / ring.Width), SpriteEffects.None, 0f);
                        }
                        sb.Draw(glow, pos - Main.screenPosition, null, VeinGlow * (a * mul), 0f,
                            gOrigin, new Vector2(r * 1.4f / glow.Width), SpriteEffects.None, 0f);
                    }
                }
            }

            //血脉潮红：每记心跳整个轮廓潮热一阵，假身与凝块跟着淡一层
            if (veinFlush > 0.04f && alpha > 0.05f) {
                Main.instance.LoadNPC(NPCID.BrainofCthulhu);
                Texture2D brainTex = TextureAssets.Npc[NPCID.BrainofCthulhu]?.Value;
                if (brainTex != null) {
                    EnsureBegin();
                    int frameH = brainTex.Height / Main.npcFrameCount[NPCID.BrainofCthulhu];
                    Rectangle frame = new(0, frameH * (frameIndex + (AgitatedFrames() ? 4 : 0)), brainTex.Width, frameH);
                    sb.Draw(brainTex, Projectile.Center - Main.screenPosition, frame,
                        (VeinGlow with { A = 0 }) * (veinFlush * 0.28f * alpha), Projectile.rotation,
                        frame.Size() * 0.5f, BodyScale(), SpriteEffects.None, 0f);
                    if (!Main.dedServ) {
                        for (int i = 0; i < 2; i++) {
                            if (mirageAlpha[i] > 0.05f && miragePos[i] != Vector2.Zero) {
                                sb.Draw(brainTex, miragePos[i] - Main.screenPosition, frame,
                                    (VeinGlow with { A = 0 }) * (veinFlush * 0.1f * alpha), -Projectile.rotation * 0.6f,
                                    frame.Size() * 0.5f, 0.85f,
                                    i == 0 ? SpriteEffects.FlipHorizontally : SpriteEffects.None, 0f);
                            }
                        }
                    }
                }
            }

            //献祭挤压：被选中的凝块积光，临爆前 72% 收针静默改为坍缩
            if (State == StateSacrifice && !sacrificeLaunched && clotPosInit && tighten > 0.05f) {
                int slot = Math.Clamp((int)StateParam, 0, ClotSlots - 1);
                EnsureBegin();
                float collapse = t >= SacrificeRelease - 6 ? 0.5f : 1f;
                float r = (10f + 26f * tighten) * collapse;
                sb.Draw(glow, clotPos[slot] - Main.screenPosition, null,
                    VeinGlow * (0.55f * tighten), 0f, gOrigin,
                    new Vector2(r * 2f / glow.Width), SpriteEffects.None, 0f);
                //向心挤压流线：各向异性拉长、密度随挤压，72% 后静默
                if (tighten < 0.72f) {
                    for (int i = 0; i < 6; i++) {
                        float phase = (Main.GlobalTimeWrappedHourly * 1.1f + i / 6f + Seed * 0.13f) % 1f;
                        float ang = Seed + i * MathHelper.TwoPi / 6f;
                        float dist = MathHelper.Lerp(78f, 14f, phase);
                        Vector2 pos = clotPos[slot] + ang.ToRotationVector2() * dist;
                        float a = tighten * 0.4f * MathF.Sin(phase * MathHelper.Pi);
                        sb.Draw(glow, pos - Main.screenPosition, null, BloodMain * a, ang,
                            gOrigin, new Vector2(24f / glow.Width * 2.2f, 7f / glow.Height), SpriteEffects.None, 0f);
                    }
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
            //压迫命中的溅血（OnHit 只在 owner 端跑，队友看粒子余韵即可）
            if (Main.dedServ) {
                return;
            }
            for (int i = 0; i < 8; i++) {
                PRTLoader.NewParticle<PRT_KikasaBloodGlob>(
                    target.Center + Main.rand.NextVector2Circular(20f, 20f),
                    Projectile.velocity * 0.25f + Main.rand.NextVector2Circular(2.5f, 2.5f),
                    BloodMain * 0.6f, Main.rand.NextFloat(0.4f, 0.7f))
                    ?.Configure(Main.rand.Next(14, 24), Main.rand.NextFloat(-0.5f, 0.5f));
            }
            SoundEngine.PlaySound(SoundID.NPCHit1 with { Volume = 0.6f, Pitch = -0.3f, MaxInstances = 3 }, target.Center);
        }

        public override void OnKill(int timeLeft) {
            //谢幕残珠：本体与在轨凝块各留一口血水
            if (Main.dedServ) {
                return;
            }
            for (int i = 0; i < 10; i++) {
                PRTLoader.NewParticle<PRT_GhostRainDrop>(
                    Projectile.Center + Main.rand.NextVector2Circular(50f, 36f),
                    new Vector2(Main.rand.NextFloat(-1.2f, 1.2f), Main.rand.NextFloat(0.5f, 2.8f)),
                    BloodMain * 0.5f, Main.rand.NextFloat(0.35f, 0.6f))
                    ?.Configure(Main.rand.Next(14, 26), 0f);
            }
            if (clotPosInit) {
                for (int k = 0; k < ClotSlots; k++) {
                    if (clotRegrow[k] > 0 || clotPopped[k]) {
                        continue;
                    }
                    for (int i = 0; i < 3; i++) {
                        PRTLoader.NewParticle<PRT_KikasaBloodGlob>(
                            clotPos[k] + Main.rand.NextVector2Circular(8f, 8f),
                            Main.rand.NextVector2Circular(1.4f, 1.4f) + Vector2.UnitY,
                            BloodDeep * 0.55f, Main.rand.NextFloat(0.3f, 0.5f))
                            ?.Configure(Main.rand.Next(14, 22), 0.3f);
                    }
                }
            }
            PRTLoader.NewParticle<PRT_GhostRainMist>(Projectile.Center,
                new Vector2(0f, -0.25f), MistBlood * 0.7f, Main.rand.NextFloat(0.6f, 0.9f))
                ?.Configure(Main.rand.Next(50, 80));
        }
    }
}
