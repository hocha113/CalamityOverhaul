using CalamityOverhaul.Common;
using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaDomains;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaServants.KikasaCultist
{
    /// <summary>
    /// 鬼奴·湖水版拜月教邪教徒。血湖之水凝成的仪式法师，湖面即祭坛：
    /// 出水为"先有祭坛后有祭司"，湖面先铭亮一整圈符文环，他从环心升起、悬袍滴水、合掌开坛。
    /// 战斗循环为水面符文法阵（点名单阵/绕身三阵，铭刻满溢即喷发）与三元素轮转施法
    /// （血冰簇弹/血火双追/血雷缓行球）交替；每种攻击绑定一种可读祷姿（原版帧 + 光效附加层）。
    /// 位移是短滑步闪现：化作一蓬血珠侧滑半米重凝，距离短、频率低、无假身。
    /// 联机契约同克眼基准：状态机走 ai[0..2] 各端同推，owner 转场盖 netUpdate 章，
    /// 子弹幕只在 owner 端生成且 spawn 自带全部初值，节拍闩防快照回卷，生命线只有 owner 判
    /// </summary>
    internal class KikasaCultistServant : ModProjectile, IKikasaServant
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        //==================== 可调基数（占位初值，验收再调）====================

        /// <summary>接触基伤（召唤加成前）。法师不近身，接触窗常闭，此值仅作弹幕基础字段</summary>
        internal const int ContactDamage = 780;

        /// <summary>法术基伤（召唤加成前），法阵喷发与三元素共用</summary>
        internal const int SpellDamage = 420;

        //==================== 状态 ====================

        private const int StateEmerge = 0;
        private const int StateFollow = 1;
        private const int StateRuneRite = 2;
        private const int StateElementCast = 3;
        private const int StateDissolve = 4;

        private int State { get => (int)Projectile.ai[0]; set => Projectile.ai[0] = value; }
        private ref float StateTimer => ref Projectile.ai[1];
        /// <summary>状态内参数：法阵=模式(0 点名单阵/1 绕身三阵)，元素施法=元素号(0 冰/1 火/2 雷)</summary>
        private ref float StateParam => ref Projectile.ai[2];

        //==================== 时序 ====================

        //出水：符文环逐字点亮→环心破水→升起凝实→合掌开坛
        private const int EmergeRuneCount = 10;
        private const int OmenFrames = 56;
        private const int RiseEnd = 104;
        private const int SettleEnd = 116;
        private const int AwakenFrame = 116;
        private const int EmergeTotal = 134;

        //法阵仪式：刹停→祷姿起手→铭刻→合袖静默→（法阵自爆发）→回摆
        private const int RiteBrakeEnd = 8;
        private const int RiteTellEnd = 22;
        private const int RiteSpawnFrame = 23;
        private const int RiteInscribeEnd = 87;
        private const int RiteSilenceEnd = 99;
        private const int RiteWaitEnd = 140;
        private const int RiteRecoverEnd = 162;

        //元素施法：刹停→展袖图腾→蓄力（72% 后静默）→释放→回摆
        private const int CastBrakeEnd = 8;
        private const int CastTellEnd = 24;
        private const int CastGatherEnd = 52;
        private const int CastSilenceEnd = 60;
        private const int CastRecoverEnd = 83;

        private const int DissolveFrames = 56;

        //滑步闪现：距离短、频率低
        private const float SlideTriggerDist = 360f;
        private const float SlideMaxStep = 230f;
        private const int SlideCooldownFrames = 100;

        //==================== 帧表 ====================

        //16 帧竖排帧带。0-2 合掌祷姿由待机动画与原版 ai[0]==2 锁帧证实；
        //其余帧带按素材推测划分，姿态读数主要由光效附加层承担，帧带边界待游戏内验收校正
        private const int FrameIdleEnd = 2;
        private const int FrameRaiseStart = 3, FrameRaiseEnd = 7;
        private const int FramePointStart = 8, FramePointEnd = 10;
        private const int FrameSpreadStart = 11, FrameSpreadEnd = 15;

        /// <summary>祷姿：每种攻击绑定一种可读姿态</summary>
        private enum Pose
        {
            /// <summary>合掌低语（待机/合袖静默）</summary>
            Idle,
            /// <summary>抬双臂过顶（三阵轮爆起手）</summary>
            Raise,
            /// <summary>单手前指（点名单阵）</summary>
            Point,
            /// <summary>袖袍展开（元素轮转）</summary>
            Spread,
        }

        //==================== 本地表现量（不入同步，节拍闩防快照回卷重播）====================

        private int frameTick;
        private int frameIndex;
        private Pose lastPose = Pose.Idle;
        private int attackCooldown;
        private int attackIndex;
        private int elementCycle;
        private int lastSeenState = -1;
        private bool breachDone;
        private bool awakenDone;
        private int lastLitRune = -1;
        private int lastRiteBurst = -1;
        private bool releaseDone;
        private bool dissolveSplashed;
        private int slideCooldown;
        private int recondenseTimer;
        private Vector2 lastCenter;
        private int faceDir = 1;
        /// <summary>本次仪式的法阵位置缓存（演出层袖口流光用，逐帧重收集）</summary>
        private readonly Vector2[] riteCirclePos = new Vector2[3];
        private int riteCircleCount;

        //==================== 配色（血系随观看域鬼雨异化冷化；元素点缀只做次要层）====================

        internal static Color BloodMain => KikasaDomain.CoolTint(new(237, 77, 69), new(126, 158, 164));
        internal static Color BloodDeep => KikasaDomain.CoolTint(new(140, 32, 30), new(84, 104, 110));
        internal static Color FoamGlow => KikasaDomain.CoolTint(new(246, 133, 112), new(176, 200, 204));
        internal static Color MistBlood => KikasaDomain.CoolTint(new(58, 18, 20), new(52, 62, 66));
        /// <summary>符文亮芯：近白的血沫色</summary>
        internal static Color RuneCore => KikasaDomain.CoolTint(new(255, 214, 196), new(214, 228, 230));

        internal static Color IceTint => KikasaDomain.CoolTint(new(172, 216, 232), new(150, 185, 195));
        internal static Color FireTint => KikasaDomain.CoolTint(new(255, 148, 64), new(196, 172, 148));
        internal static Color ThunderTint => KikasaDomain.CoolTint(new(186, 148, 255), new(168, 174, 210));

        private Player Owner => Main.player[Projectile.owner];

        /// <summary>连续量抖动的确定性相位，各端一致（不掷 Main.rand）</summary>
        private float Seed => Projectile.identity * 0.7391f;

        //==================== 召唤入口 ====================

        /// <summary>KikasaServantIndex 登记的召唤委托；emergeAt.Y = 湖面（符文环中心）</summary>
        internal static void Summon(Player owner, Vector2 emergeAt) {
            if (owner.whoAmI != Main.myPlayer) {
                return;
            }
            int damage = (int)owner.GetTotalDamage(DamageClass.Summon).ApplyTo(ContactDamage);
            //环心水下待命，环刻满才升起
            Projectile.NewProjectile(owner.GetSource_Misc("KikasaServant"),
                emergeAt + new Vector2(0f, 34f), Vector2.Zero,
                ModContent.ProjectileType<KikasaCultistServant>(), damage, 4f, owner.whoAmI);
        }

        //==================== 定义 ====================

        public override void SetStaticDefaults() {
            Main.projPet[Projectile.type] = true;
            ProjectileID.Sets.MinionSacrificable[Projectile.type] = false;
            //符文环与姿态光效超出 hitbox 一大截
            ProjectileID.Sets.DrawScreenCheckFluff[Projectile.type] = 480;
        }

        public override void SetDefaults() {
            Projectile.width = 38;
            Projectile.height = 62;
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

        public override bool MinionContactDamage() => false;

        /// <summary>仪式法师不近身：没有可见的接触攻击拍，接触窗恒闭</summary>
        public override bool? CanDamage() => false;

        public override bool? CanCutTiles() => false;

        //==================== 遣返 ====================

        public bool IsDismissing => State == StateDissolve;

        public void BeginDismiss() {
            if (Main.myPlayer == Projectile.owner && State != StateDissolve) {
                BeginDissolve();
            }
        }

        private void BeginDissolve() {
            //环还没刻满、人还没破水：什么都没露出来，不演谢幕
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

            //生命线：湖塌/收域/主人死亡→溶解回湖。只有 owner 裁决
            //服务器无领域状态（恒 Closed 是既定契约），别处判会当场误杀；其余端只跟包换场
            if (authority && State != StateDissolve && !LakeHealthy(owner, domain)) {
                BeginDissolve();
            }

            Projectile.timeLeft = 180;
            //伤害随召唤加成逐帧刷新（接触窗恒闭，字段仅作基准）
            Projectile.damage = (int)owner.GetTotalDamage(DamageClass.Summon).ApplyTo(ContactDamage);

            //换场清闩：远端可能靠收包换场而非本地同拍转场，残闩会吞掉新场节拍
            if (State != lastSeenState) {
                lastSeenState = State;
                lastRiteBurst = -1;
                releaseDone = false;
                if (State == StateDissolve) {
                    dissolveSplashed = false;
                    lastLitRune = -1;
                }
            }

            //滑步/同步包位置突变：原地散珠、新位重凝（远端靠这条播出同款演出）
            if (lastCenter != Vector2.Zero && State != StateEmerge && State != StateDissolve) {
                float jump = Vector2.Distance(lastCenter, Projectile.Center);
                if (jump > 110f && jump < 900f) {
                    PlaySlideFx(lastCenter, Projectile.Center);
                }
            }
            lastCenter = Projectile.Center;

            StateTimer++;
            switch (State) {
                case StateEmerge: UpdateEmerge(owner, domain); break;
                case StateFollow: UpdateFollow(owner, domain, authority); break;
                case StateRuneRite: UpdateRuneRite(owner, domain, authority); break;
                case StateElementCast: UpdateElementCast(owner, authority); break;
                case StateDissolve: UpdateDissolve(domain, authority); break;
            }

            UpdateFrames();
            if (attackCooldown > 0) {
                attackCooldown--;
            }
            if (slideCooldown > 0) {
                slideCooldown--;
            }
            if (recondenseTimer > 0) {
                recondenseTimer--;
            }

            //悬袍轻摆：随横速微倾
            Projectile.rotation = MathHelper.Clamp(Projectile.velocity.X * 0.024f, -0.12f, 0.12f);

            float glow = CurrentAlpha() * 0.5f;
            if (glow > 0.02f) {
                Lighting.AddLight(Projectile.Center, 0.40f * glow, 0.10f * glow, 0.10f * glow);
            }
        }

        private static bool LakeHealthy(Player owner, KikasaDomainPlayer domain)
            => !owner.dead && domain.AnyActive
            && domain.Phase != KikasaDomainPhase.Closing
            && domain.RiseT >= 0.9f;

        //==================== 出水：先有祭坛，后有祭司 ====================

        private void UpdateEmerge(Player owner, KikasaDomainPlayer domain) {
            float lakeY = domain.LakeWorldY;
            int t = (int)StateTimer;
            bool viewed = ViewedOwner;
            Vector2 ringCenter = new(Projectile.Center.X, lakeY);

            if (t < OmenFrames) {
                //铭环期：人在水下不露面，湖面符文一字一字亮起
                Projectile.velocity = Vector2.Zero;
                int lit = (int)(RingLitT(t) * EmergeRuneCount);
                if (lit > lastLitRune && lit <= EmergeRuneCount) {
                    lastLitRune = lit;
                    //每字一声清音，音高逐字爬升，铭刻在推进
                    SoundEngine.PlaySound(SoundID.Item29 with {
                        Volume = 0.3f,
                        Pitch = -0.7f + lit * 0.055f,
                        MaxInstances = 3
                    }, ringCenter);
                    if (viewed) {
                        float angle = -MathHelper.PiOver2 + (lit - 1) / (float)EmergeRuneCount * MathHelper.TwoPi;
                        KikasaDomainDeco.RippleAt(KikasaCultistRunes.RingSlot(ringCenter, EmergeRingRadius, angle), 0.34f);
                    }
                }
                if (t == 6) {
                    SoundEngine.PlaySound(SoundID.Drip with { Volume = 0.5f, Pitch = -0.75f, MaxInstances = 2 }, ringCenter);
                }
                return;
            }

            if (!breachDone) {
                //破水拍：环刻满的一帧，环形喷帘同时起，祭司自环心一帧起速升出
                breachDone = true;
                Projectile.velocity = new Vector2(0f, -9.5f);
                SoundEngine.PlaySound(SoundID.DD2_DarkMageSummonSkeleton with { Volume = 0.85f, Pitch = -0.3f, MaxInstances = 2 }, ringCenter);
                SoundEngine.PlaySound(SoundID.SplashWeak with { Volume = 1f, Pitch = -0.3f, MaxInstances = 2 }, ringCenter);
                SoundEngine.PlaySound(SoundID.DD2_MonkStaffGroundImpact with { Volume = 0.45f, Pitch = -0.7f, MaxInstances = 1 }, ringCenter);
                if (viewed) {
                    BreachRingBurst(ringCenter);
                }
            }

            //升起：指数衰减，前快后慢
            Projectile.velocity.Y *= 0.94f;
            Projectile.velocity.X = 0f;

            if (viewed && t < RiseEnd && t % 2 == 0) {
                //悬袍滴水成帘
                Vector2 dropPos = Projectile.Center + new Vector2(
                    Main.rand.NextFloat(-16f, 16f), Main.rand.NextFloat(14f, 30f));
                PRTLoader.NewParticle<PRT_GhostRainDrop>(dropPos,
                    new Vector2(Main.rand.NextFloat(-0.3f, 0.3f), Main.rand.NextFloat(2.2f, 3.6f)),
                    BloodMain * Main.rand.NextFloat(0.4f, 0.6f),
                    Main.rand.NextFloat(0.4f, 0.65f))?.Configure(Main.rand.Next(14, 26), 0f);
                if (t % 6 == 2) {
                    KikasaDomainDeco.RippleAt(new Vector2(Projectile.Center.X + Main.rand.NextFloat(-14f, 14f), lakeY), 0.3f);
                }
            }

            if (!awakenDone && t >= AwakenFrame) {
                //开坛拍：合掌、面具下一闪、符文环收拢成掠身光柱
                awakenDone = true;
                SoundEngine.PlaySound(SoundID.DD2_DarkMageCastHeal with { Volume = 0.6f, Pitch = -0.5f, MaxInstances = 2 }, Projectile.Center);
                SoundEngine.PlaySound(SoundID.Drip with { Volume = 0.5f, Pitch = -0.4f, MaxInstances = 2 }, Projectile.Center);
                if (viewed) {
                    PRTLoader.NewParticle<PRT_DWave>(Projectile.Center, Vector2.Zero, FoamGlow, 0.08f)
                        ?.Configure(new Vector2(1f, 1f), 0f, 0.26f, 10);
                    //环收拢：一圈血珠自环位向环心上卷
                    for (int i = 0; i < EmergeRuneCount; i++) {
                        float angle = -MathHelper.PiOver2 + i / (float)EmergeRuneCount * MathHelper.TwoPi;
                        Vector2 slot = KikasaCultistRunes.RingSlot(ringCenter, EmergeRingRadius, angle);
                        PRTLoader.NewParticle<PRT_GhostRainDrop>(slot,
                            (Projectile.Center - slot).SafeNormalize(-Vector2.UnitY) * Main.rand.NextFloat(4f, 7f),
                            FoamGlow * 0.6f, Main.rand.NextFloat(0.35f, 0.55f))?.Configure(Main.rand.Next(12, 20), 0f);
                    }
                    ShakeViewer(2f);
                }
            }

            //升起期低头合掌，开坛后面向主人
            faceDir = t < AwakenFrame ? faceDir : owner.Center.X >= Projectile.Center.X ? 1 : -1;

            if (t >= EmergeTotal) {
                //转场确定性（纯计时），owner 盖章纠偏
                State = StateFollow;
                StateTimer = 0;
                StateParam = 0;
                attackCooldown = 40;
                Projectile.netUpdate = Main.myPlayer == Projectile.owner;
            }
        }

        internal const float EmergeRingRadius = 92f;

        /// <summary>出水铭环进度（0~1），绘制层与节拍共用同一条曲线</summary>
        private static float RingLitT(int t) => MathHelper.Clamp(t / (float)(OmenFrames - 6), 0f, 1f);

        /// <summary>环形喷帘：环位一圈小水柱同时起，环心一柱最高，破的是"坛"不是点</summary>
        private void BreachRingBurst(Vector2 center) {
            KikasaDomainDeco.RippleAt(center, 2.2f);
            KikasaDomainDeco.SplashAt(center, 12);
            for (int i = 0; i < 4; i++) {
                float angle = -MathHelper.PiOver2 + i / 4f * MathHelper.TwoPi;
                Vector2 slot = KikasaCultistRunes.RingSlot(center, EmergeRingRadius, angle);
                KikasaDomainDeco.RippleAt(slot, 0.9f);
                KikasaDomainDeco.SplashAt(slot, 5);
            }
            //环上均匀的血珠喷帘 + 环心高柱
            for (int i = 0; i < 20; i++) {
                float angle = i / 20f * MathHelper.TwoPi;
                Vector2 slot = KikasaCultistRunes.RingSlot(center, EmergeRingRadius * Main.rand.NextFloat(0.85f, 1f), angle);
                PRTLoader.NewParticle<PRT_GhostRainDrop>(slot + new Vector2(0f, -2f),
                    new Vector2(Main.rand.NextFloat(-0.6f, 0.6f), -Main.rand.NextFloat(4.5f, 7.5f)),
                    BloodMain * Main.rand.NextFloat(0.45f, 0.65f),
                    Main.rand.NextFloat(0.45f, 0.7f))?.Configure(Main.rand.Next(22, 36), Main.rand.NextFloat(-0.3f, 0.3f));
            }
            for (int i = 0; i < 8; i++) {
                PRTLoader.NewParticle<PRT_GhostRainDrop>(center + new Vector2(Main.rand.NextFloat(-8f, 8f), -4f),
                    new Vector2(Main.rand.NextFloat(-0.7f, 0.7f), -Main.rand.NextFloat(9f, 12.5f)),
                    FoamGlow * Main.rand.NextFloat(0.5f, 0.7f),
                    Main.rand.NextFloat(0.5f, 0.8f))?.Configure(Main.rand.Next(32, 48), Main.rand.NextFloat(-0.25f, 0.25f));
            }
            for (int i = 0; i < 3; i++) {
                PRTLoader.NewParticle<PRT_GhostRainMist>(
                    center + new Vector2(Main.rand.NextFloat(-40f, 40f), -8f),
                    new Vector2(Main.rand.NextFloat(-0.3f, 0.3f), -Main.rand.NextFloat(0.3f, 0.6f)),
                    MistBlood * 0.8f, Main.rand.NextFloat(0.7f, 1f))?.Configure(Main.rand.Next(60, 95));
            }
            ShakeViewer(5f);
        }

        //==================== 跟随与滑步 ====================

        private void UpdateFollow(Player owner, KikasaDomainPlayer domain, bool authority) {
            int target = FindTarget(owner);

            //悬在主人侧上方，比克眼更高更远，他是站在后排的祭司
            Vector2 anchor = owner.Center + new Vector2(-owner.direction * 110f, -148f);
            anchor.Y += MathF.Sin(Main.GlobalTimeWrappedHourly * 1.7f + Seed) * 7f;
            anchor.X += MathF.Sin(Main.GlobalTimeWrappedHourly * 1.1f + Seed * 2f) * 5f;

            Vector2 to = anchor - Projectile.Center;
            float dist = to.Length();
            if (dist > 2400f) {
                //跟丢硬贴回
                Projectile.Center = anchor;
                Projectile.velocity = Vector2.Zero;
                Projectile.netUpdate = authority;
                return;
            }

            //短滑步闪现：跟不上就化珠滑半步，不加速狂追，仪式间的从容挪步。
            //只在 owner 端裁决（盖章瞬移），远端靠位置突变检测播同款演出
            if (authority && dist > SlideTriggerDist && slideCooldown <= 0) {
                slideCooldown = SlideCooldownFrames;
                Vector2 step = to.SafeNormalize(Vector2.UnitX) * MathF.Min(dist * 0.6f, SlideMaxStep);
                Vector2 from = Projectile.Center;
                Projectile.Center += step;
                Projectile.velocity = to.SafeNormalize(Vector2.UnitX) * 2f;
                Projectile.netUpdate = true;
                PlaySlideFx(from, Projectile.Center);
                lastCenter = Projectile.Center;
            }
            else {
                //常态悬浮：缓速漂近
                Vector2 desired = to * 0.06f;
                const float maxSpeed = 12f;
                if (desired.Length() > maxSpeed) {
                    desired = desired.SafeNormalize(Vector2.Zero) * maxSpeed;
                }
                Projectile.velocity = Vector2.Lerp(Projectile.velocity, desired, 0.11f);
            }

            //有猎物盯猎物，闲着看主人
            Vector2 look = target >= 0 ? Main.npc[target].Center : owner.Center;
            faceDir = look.X >= Projectile.Center.X ? 1 : -1;

            //袍尖偶发凝珠滴落
            if (!Main.dedServ && Main.rand.NextBool(26)) {
                Vector2 hem = Projectile.Center + new Vector2(Main.rand.NextFloat(-14f, 14f), Main.rand.NextFloat(20f, 32f));
                PRTLoader.NewParticle<PRT_GhostRainDrop>(hem,
                    new Vector2(0f, Main.rand.NextFloat(0.5f, 1.1f)),
                    BloodMain * Main.rand.NextFloat(0.4f, 0.55f),
                    Main.rand.NextFloat(0.32f, 0.55f))?.Configure(Main.rand.Next(20, 34), 0f);
            }
            //偶发极轻的低语（确定性帧点，各端位置衰减）
            if ((int)StateTimer % 260 == 200) {
                SoundEngine.PlaySound(SoundID.DD2_DarkMageCastHeal with { Volume = 0.14f, Pitch = -0.9f, MaxInstances = 1 }, Projectile.Center);
            }

            //出手裁决：法阵与元素轮转按 1:2 编排；规则确定性，owner 盖章。
            //目标太高时法阵水柱够不着，让给会飞的元素弹
            if (target >= 0 && attackCooldown <= 0 && StateTimer > 32) {
                NPC npc = Main.npc[target];
                bool targetLow = npc.Center.Y > domain.LakeWorldY - 300f;
                attackIndex++;
                if (attackIndex % 3 == 1 && targetLow) {
                    State = StateRuneRite;
                    //远敌点名脚下单阵，近敌绕身三阵轮爆
                    StateParam = MathF.Abs(npc.Center.X - Projectile.Center.X) > 240f ? 0f : 1f;
                }
                else {
                    State = StateElementCast;
                    StateParam = elementCycle % 3;
                    elementCycle++;
                }
                StateTimer = 0;
                Projectile.netUpdate = authority;
            }
        }

        /// <summary>滑步演出：原位血珠沿滑向散开、新位反向收拢重凝，无假身残留</summary>
        private void PlaySlideFx(Vector2 from, Vector2 to) {
            recondenseTimer = 12;
            SoundEngine.PlaySound(SoundID.SplashWeak with { Volume = 0.35f, Pitch = 0.25f, MaxInstances = 2 }, to);
            SoundEngine.PlaySound(SoundID.Drip with { Volume = 0.45f, Pitch = -0.2f, MaxInstances = 2 }, from);
            if (Main.dedServ) {
                return;
            }
            Vector2 dir = (to - from).SafeNormalize(Vector2.UnitX);
            //原位：一蓬沿滑向拉散的血珠
            for (int i = 0; i < 12; i++) {
                PRTLoader.NewParticle<PRT_GhostRainDrop>(
                    from + Main.rand.NextVector2Circular(14f, 22f),
                    dir * Main.rand.NextFloat(3f, 7.5f) + Main.rand.NextVector2Circular(1.1f, 1.1f),
                    BloodMain * Main.rand.NextFloat(0.4f, 0.6f),
                    Main.rand.NextFloat(0.35f, 0.6f))?.Configure(Main.rand.Next(10, 18), 0f);
            }
            PRTLoader.NewParticle<PRT_GhostRainMist>(from, dir * 0.6f, MistBlood * 0.7f,
                Main.rand.NextFloat(0.5f, 0.7f))?.Configure(Main.rand.Next(26, 40));
            //新位：几粒反向收拢，读出"重凝"
            for (int i = 0; i < 6; i++) {
                Vector2 outer = to + Main.rand.NextVector2Unit() * Main.rand.NextFloat(22f, 40f);
                PRTLoader.NewParticle<PRT_GhostRainDrop>(outer,
                    (to - outer) * 0.14f,
                    FoamGlow * Main.rand.NextFloat(0.4f, 0.55f),
                    Main.rand.NextFloat(0.3f, 0.5f))?.Configure(9, 0f);
            }
        }

        //==================== 攻击一：水面符文法阵 ====================

        private void UpdateRuneRite(Player owner, KikasaDomainPlayer domain, bool authority) {
            int t = (int)StateTimer;
            bool triple = (int)StateParam == 1;
            int target = FindTarget(owner);

            if (t <= RiteBrakeEnd) {
                Projectile.velocity *= 0.85f;
                if (target >= 0) {
                    faceDir = Main.npc[target].Center.X >= Projectile.Center.X ? 1 : -1;
                }
                return;
            }

            if (t <= RiteTellEnd) {
                //祷姿起手：点名=单手前指，三阵=抬双臂过顶；指尖/臂弧光在绘制层
                Projectile.velocity *= 0.9f;
                if (t == RiteBrakeEnd + 2) {
                    SoundEngine.PlaySound(SoundID.DD2_DarkMageCastHeal with { Volume = 0.5f, Pitch = -0.6f, MaxInstances = 2 }, Projectile.Center);
                }
                return;
            }

            if (t == RiteSpawnFrame) {
                //法阵只在 owner 端生成，spawn 自带全部初值（爆发帧/错拍号/点名目标）
                if (target < 0 && authority) {
                    EndAttack(authority, 60);
                    return;
                }
                if (authority) {
                    int damage = (int)owner.GetTotalDamage(DamageClass.Summon).ApplyTo(SpellDamage);
                    float lakeY = domain.LakeWorldY;
                    if (triple) {
                        for (int i = 0; i < 3; i++) {
                            Vector2 pos = new(Projectile.Center.X + (i - 1) * 150f, lakeY);
                            Projectile.NewProjectile(Projectile.GetSource_FromAI(), pos, Vector2.Zero,
                                ModContent.ProjectileType<KikasaCultistRuneCircle>(), damage, 4f,
                                Projectile.owner, KikasaCultistRuneCircle.BurstAtFrames, i, 0f);
                        }
                    }
                    else {
                        float x = MathHelper.Clamp(Main.npc[target].Center.X,
                            Projectile.Center.X - 760f, Projectile.Center.X + 760f);
                        Projectile.NewProjectile(Projectile.GetSource_FromAI(), new Vector2(x, lakeY), Vector2.Zero,
                            ModContent.ProjectileType<KikasaCultistRuneCircle>(), damage, 4f,
                            Projectile.owner, KikasaCultistRuneCircle.BurstAtFrames, 0f, target + 1);
                    }
                }
                return;
            }

            if (t <= RiteInscribeEnd) {
                //铭刻期：悬定低语，袖口向法阵淌出流光（绘制层）；逐帧收集己方法阵位置
                Projectile.velocity *= 0.92f;
                CollectRiteCircles();
                if (target >= 0 && !triple) {
                    faceDir = Main.npc[target].Center.X >= Projectile.Center.X ? 1 : -1;
                }
                return;
            }

            if (t <= RiteSilenceEnd) {
                //合袖静默：流光断、姿态回合掌，喷发前的吸气
                Projectile.velocity *= 0.9f;
                return;
            }

            if (t <= RiteWaitEnd) {
                //喷发期：法阵各自起爆，本体按确定性拍点微微上浮后坐
                CollectRiteCircles();
                int burstIndex = (t - (int)RiteSilenceEnd - 1) / KikasaCultistRuneCircle.BurstStagger;
                int burstCount = triple ? 3 : 1;
                if (burstIndex < burstCount && burstIndex > lastRiteBurst
                    && (t - RiteSilenceEnd - 1) % KikasaCultistRuneCircle.BurstStagger == 0) {
                    lastRiteBurst = burstIndex;
                    Projectile.velocity.Y -= 1.6f;
                    if (ViewedOwner) {
                        ShakeViewer(1.6f);
                    }
                }
                Projectile.velocity *= 0.93f;
                return;
            }

            if (t >= RiteRecoverEnd) {
                EndAttack(authority, 160);
            }
            else {
                Projectile.velocity *= 0.94f;
            }
        }

        /// <summary>收集场上属于本次仪式的法阵位置（演出层流光锚点）</summary>
        private void CollectRiteCircles() {
            riteCircleCount = 0;
            for (int i = 0; i < Main.maxProjectiles && riteCircleCount < 3; i++) {
                Projectile proj = Main.projectile[i];
                if (proj?.active == true && proj.owner == Projectile.owner
                    && proj.ModProjectile is KikasaCultistRuneCircle) {
                    riteCirclePos[riteCircleCount++] = proj.Center;
                }
            }
        }

        //==================== 攻击二：三元素轮转 ====================

        private void UpdateElementCast(Player owner, bool authority) {
            int t = (int)StateTimer;
            int element = (int)StateParam;
            int target = FindTarget(owner);
            Vector2 aimPos = target >= 0 ? Main.npc[target].Center : Projectile.Center + new Vector2(faceDir * 300f, -40f);
            Vector2 aim = (aimPos - Projectile.Center).SafeNormalize(-Vector2.UnitY);

            if (t <= CastBrakeEnd) {
                Projectile.velocity *= 0.85f;
                faceDir = aimPos.X >= Projectile.Center.X ? 1 : -1;
                if (target < 0) {
                    EndAttack(authority, 50);
                }
                return;
            }

            if (t <= CastTellEnd) {
                //展袖 tell：头顶元素图腾一闪（绘制层），低吟预告本轮元素
                Projectile.velocity *= 0.9f;
                faceDir = aimPos.X >= Projectile.Center.X ? 1 : -1;
                if (t == CastBrakeEnd + 2) {
                    SoundEngine.PlaySound(SoundID.DD2_DarkMageCastHeal with {
                        Volume = 0.45f,
                        Pitch = -0.65f + element * 0.18f,
                        MaxInstances = 2
                    }, Projectile.Center);
                }
                return;
            }

            if (t <= CastGatherEnd) {
                //蓄力：身体微微后倾，元素专属凝聚演出在绘制层与粒子层；72% 后静默由 GatherLevel 截断
                Projectile.velocity = Vector2.Lerp(Projectile.velocity, -aim * 1.1f, 0.08f);
                faceDir = aimPos.X >= Projectile.Center.X ? 1 : -1;
                SpawnGatherParticles(element, aim);
                return;
            }

            if (t <= CastSilenceEnd) {
                //静默拍：凝聚粒子全断，袖内只剩将熄的光
                Projectile.velocity *= 0.8f;
                return;
            }

            if (!releaseDone) {
                //释放拍：一帧出手 + 后坐退步；子弹幕只在 owner 端生成，spawn 自带全部初值
                releaseDone = true;
                Projectile.velocity -= aim * 3.5f;
                PlayReleaseBeat(element, aim);
                if (authority) {
                    int damage = (int)owner.GetTotalDamage(DamageClass.Summon).ApplyTo(SpellDamage);
                    ReleaseElement(element, target, aim, damage);
                }
                if (ViewedOwner) {
                    ShakeViewer(2.5f);
                }
                return;
            }

            if (t >= CastRecoverEnd) {
                EndAttack(authority, 115);
            }
            else {
                Projectile.velocity *= 0.92f;
            }
        }

        /// <summary>蓄力期粒子：各元素一种"向心"读法，72% 后全部静默（爆发前的吸气）</summary>
        private void SpawnGatherParticles(int element, Vector2 aim) {
            if (Main.dedServ) {
                return;
            }
            float gather = GatherLevel();
            if (gather >= 0.72f || gather <= 0f) {
                return;
            }
            int t = (int)StateTimer;
            if (element == 0 && t % 4 == 1) {
                //冰：细小冰尘向扇形晶位飘去
                Vector2 slot = IceGhostPos(t % 5, aim);
                Vector2 from = slot + Main.rand.NextVector2Unit() * Main.rand.NextFloat(20f, 44f);
                PRTLoader.NewParticle<PRT_GhostRainDrop>(from, (slot - from) * 0.16f,
                    IceTint * 0.5f, Main.rand.NextFloat(0.25f, 0.4f))?.Configure(8, 0f);
            }
            else if (element == 1 && t % 3 == 1) {
                //火：暖芒被吸进双袖火种
                Vector2 hand = SleevePos(Main.rand.NextBool() ? 1 : -1);
                Vector2 from = hand + Main.rand.NextVector2Unit() * Main.rand.NextFloat(30f, 70f);
                PRTLoader.NewParticle<PRT_Spark>(from, (hand - from) * 0.13f,
                    Color.Lerp(FireTint, RuneCore, Main.rand.NextFloat(0.4f)),
                    Main.rand.NextFloat(0.7f, 1.1f))?.Configure(false, 12);
            }
            else if (element == 2 && t % 5 == 1) {
                //雷：头顶雷种周身跳细火花
                Vector2 seat = Projectile.Center + new Vector2(0f, -48f);
                PRTLoader.NewParticle<PRT_Spark>(seat + Main.rand.NextVector2Circular(16f, 12f),
                    Main.rand.NextVector2Circular(1.4f, 1.4f),
                    Color.Lerp(ThunderTint, RuneCore, Main.rand.NextFloat(0.5f)),
                    Main.rand.NextFloat(0.6f, 1f))?.Configure(false, 10);
            }
        }

        /// <summary>释放帧的声与光（各端都播，音量随距离衰减）</summary>
        private void PlayReleaseBeat(int element, Vector2 aim) {
            switch (element) {
                case 0:
                    SoundEngine.PlaySound(SoundID.Item27 with { Volume = 0.7f, Pitch = -0.15f, MaxInstances = 3 }, Projectile.Center);
                    SoundEngine.PlaySound(SoundID.Item29 with { Volume = 0.4f, Pitch = 0.2f, MaxInstances = 3 }, Projectile.Center);
                    break;
                case 1:
                    SoundEngine.PlaySound(SoundID.Item34 with { Volume = 0.65f, Pitch = -0.2f, MaxInstances = 3 }, Projectile.Center);
                    SoundEngine.PlaySound(SoundID.DD2_BetsyFireballShot with { Volume = 0.5f, Pitch = -0.35f, MaxInstances = 2 }, Projectile.Center);
                    break;
                default:
                    SoundEngine.PlaySound(SoundID.Item93 with { Volume = 0.6f, Pitch = -0.3f, MaxInstances = 2 }, Projectile.Center);
                    SoundEngine.PlaySound(SoundID.Item122 with { Volume = 0.4f, Pitch = -0.5f, MaxInstances = 2 }, Projectile.Center);
                    break;
            }
            if (Main.dedServ) {
                return;
            }
            //出手烟花：元素色小簇
            Color tint = element == 0 ? IceTint : element == 1 ? FireTint : ThunderTint;
            for (int i = 0; i < 7; i++) {
                PRTLoader.NewParticle<PRT_GhostRainDrop>(
                    Projectile.Center + aim * 26f + Main.rand.NextVector2Circular(8f, 8f),
                    aim.RotatedByRandom(0.5f) * Main.rand.NextFloat(2f, 5f),
                    Color.Lerp(BloodMain, tint, 0.5f) * 0.6f,
                    Main.rand.NextFloat(0.3f, 0.5f))?.Configure(Main.rand.Next(10, 18), 0f);
            }
        }

        /// <summary>释放：冰=扇形五晶齐射；火=成对螺旋双球；雷=一颗缓行雷球</summary>
        private void ReleaseElement(int element, int target, Vector2 aim, int damage) {
            IEntitySource source = Projectile.GetSource_FromAI();
            if (element == 0) {
                for (int i = 0; i < 5; i++) {
                    Vector2 pos = IceGhostPos(i, aim);
                    Vector2 dir = aim.RotatedBy((i - 2) * 0.21f);
                    Projectile.NewProjectile(source, pos, dir * 19f,
                        ModContent.ProjectileType<KikasaCultistIceShard>(), damage, 3f, Projectile.owner);
                }
            }
            else if (element == 1) {
                //双球同点出膛共享一条螺旋轴（真 DNA 缠绕），相位差 π；两袖火种在释放帧汇于前手
                Vector2 pos = SleevePos(1);
                for (int i = 0; i < 2; i++) {
                    Projectile.NewProjectile(source, pos, aim * 8f,
                        ModContent.ProjectileType<KikasaCultistFireOrb>(), damage, 3f, Projectile.owner,
                        0f, i, target + 1);
                }
            }
            else {
                Projectile.NewProjectile(source, Projectile.Center + new Vector2(0f, -48f), aim * KikasaCultistThunderOrb.DriftSpeed,
                    ModContent.ProjectileType<KikasaCultistThunderOrb>(), damage, 3f, Projectile.owner);
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
                //袖袍一合，缓缓沉回湖里
                Projectile.velocity.X *= 0.9f;
                Projectile.velocity.Y = MathF.Min(Projectile.velocity.Y + 0.22f, 7.5f);
            }
            else {
                //湖已不在：原地化作符文碎屑
                Projectile.velocity *= 0.9f;
            }

            if (t == 2) {
                SoundEngine.PlaySound(SoundID.DD2_DarkMageCastHeal with { Volume = 0.45f, Pitch = -0.8f, MaxInstances = 2 }, Projectile.Center);
            }

            //过水线拍（一次）
            if (lakeAlive && !dissolveSplashed && Projectile.Center.Y >= lakeY) {
                dissolveSplashed = true;
                SoundEngine.PlaySound(SoundID.SplashWeak with { Volume = 0.65f, Pitch = -0.35f, MaxInstances = 2 }, Projectile.Center);
                if (ViewedOwner) {
                    Vector2 hit = new(Projectile.Center.X, lakeY);
                    KikasaDomainDeco.SplashAt(hit, 9);
                    KikasaDomainDeco.RippleAt(hit, 1.2f);
                    ShakeViewer(1.8f);
                }
            }

            //边沉边化：血珠下坠 + 符文碎屑上飘剥落
            if (!Main.dedServ && CurrentAlpha() > 0.15f) {
                if (t % 2 == 0) {
                    PRTLoader.NewParticle<PRT_GhostRainDrop>(
                        Projectile.Center + Main.rand.NextVector2Circular(18f, 28f),
                        new Vector2(Main.rand.NextFloat(-0.5f, 0.5f), Main.rand.NextFloat(1.4f, 2.8f)),
                        BloodMain * 0.5f, Main.rand.NextFloat(0.35f, 0.55f))?.Configure(Main.rand.Next(12, 22), 0f);
                }
                if (t % 5 == 1) {
                    PRTLoader.NewParticle<PRT_Sparkle>(
                        Projectile.Center + Main.rand.NextVector2Circular(16f, 26f),
                        new Vector2(Main.rand.NextFloat(-0.4f, 0.4f), -Main.rand.NextFloat(0.4f, 1f)),
                        FoamGlow, Main.rand.NextFloat(0.24f, 0.4f))
                        ?.Configure(FoamGlow * 0.5f, Main.rand.Next(18, 30), 0.05f, 0.7f);
                }
            }

            //owner 到点收场；远端多给 10 帧等 kill 包，兜底自杀
            if (authority && t >= DissolveFrames) {
                Projectile.Kill();
            }
            else if (!authority && t >= DissolveFrames + 10) {
                Projectile.Kill();
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

        /// <summary>袖手位：side=+1 前手 / -1 后手（相对面向）</summary>
        private Vector2 SleevePos(int side)
            => Projectile.Center + new Vector2(faceDir * (side > 0 ? 20f : -6f), side > 0 ? -2f : 4f);

        /// <summary>指尖位：单手前指的光锚</summary>
        private Vector2 FingerPos() => Projectile.Center + new Vector2(faceDir * 24f, -8f);

        /// <summary>冰晶虚影阵列槽位：身前扇形五点，随当前瞄准摆动</summary>
        private Vector2 IceGhostPos(int index, Vector2 aim)
            => Projectile.Center + aim.RotatedBy((index - 2) * 0.21f) * 74f;

        /// <summary>元素蓄力进度 0~1（Tell 结束起算）</summary>
        private float GatherLevel() {
            if (State != StateElementCast) {
                return 0f;
            }
            int t = (int)StateTimer;
            if (t <= CastTellEnd || t > CastSilenceEnd) {
                return 0f;
            }
            if (t <= CastGatherEnd) {
                return (t - CastTellEnd) / (float)(CastGatherEnd - CastTellEnd);
            }
            //静默拍维持满值，粒子层自己按 0.72 截断
            return 1f;
        }

        /// <summary>当前祷姿：由状态与拍点确定性推导（本地表现量）</summary>
        private Pose CurrentPose() {
            int t = (int)StateTimer;
            return State switch {
                StateRuneRite when t > RiteBrakeEnd && t <= RiteInscribeEnd
                    => (int)StateParam == 1 ? Pose.Raise : Pose.Point,
                StateElementCast when t > CastBrakeEnd && t <= CastSilenceEnd + 4 => Pose.Spread,
                _ => Pose.Idle,
            };
        }

        private void UpdateFrames() {
            Pose pose = CurrentPose();
            if (pose != lastPose) {
                lastPose = pose;
                frameTick = 0;
                frameIndex = pose switch {
                    Pose.Raise => FrameRaiseStart,
                    Pose.Point => FramePointStart,
                    Pose.Spread => FrameSpreadStart,
                    _ => 0,
                };
            }
            //合掌循环慢摆；姿态帧带单向推进后锁尾帧
            int speed = pose == Pose.Idle ? 8 : 4;
            if (++frameTick >= speed) {
                frameTick = 0;
                switch (pose) {
                    case Pose.Idle:
                        frameIndex = frameIndex >= FrameIdleEnd ? 0 : frameIndex + 1;
                        break;
                    case Pose.Raise:
                        frameIndex = Math.Min(frameIndex + 1, FrameRaiseEnd);
                        break;
                    case Pose.Point:
                        frameIndex = Math.Min(frameIndex + 1, FramePointEnd);
                        break;
                    case Pose.Spread:
                        frameIndex = Math.Min(frameIndex + 1, FrameSpreadEnd);
                        break;
                }
            }
        }

        internal bool ViewedOwner
            => KikasaDomain.Viewed != null && KikasaDomain.Viewed.Player.whoAmI == Projectile.owner;

        private static void ShakeViewer(float amount)
            => Main.LocalPlayer?.CWR()?.GetScreenShake(amount);

        //==================== 表现参数 ====================

        private float CurrentAlpha() {
            int t = (int)StateTimer;
            return State switch {
                StateEmerge => t < OmenFrames ? 0f : MathHelper.Clamp((t - OmenFrames) / 4f, 0f, 1f),
                StateDissolve => MathHelper.Clamp((DissolveFrames - t) / 12f, 0f, 1f),
                _ => 1f,
            };
        }

        /// <summary>uForm：1=全血水 0=真身；常态半沉呼吸，滑步重凝短暂冲高</summary>
        private float CurrentForm() {
            int t = (int)StateTimer;
            float steady = 0.34f + MathF.Sin(Main.GlobalTimeWrappedHourly * 2.7f + Seed) * 0.05f;
            steady += recondenseTimer / 12f * 0.5f;
            return State switch {
                StateEmerge => t < OmenFrames
                    ? 1f
                    : MathHelper.Lerp(1f, steady, SmoothStep01(MathHelper.Clamp((t - OmenFrames) / (float)(RiseEnd - OmenFrames), 0f, 1f))),
                StateDissolve => MathHelper.Clamp(steady + t / (float)DissolveFrames * 0.3f, 0f, 1f),
                _ => MathHelper.Clamp(steady, 0f, 1f),
            };
        }

        /// <summary>uScanMode：出水期自上而下凝实扫描，落定渐回噪声斑驳半沉态</summary>
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

        private float BodyScale() {
            float scale = 1f;
            int t = (int)StateTimer;
            if (State == StateEmerge && t >= OmenFrames && t < OmenFrames + 10) {
                //破水过冲
                scale *= 1f + 0.06f * (1f - (t - OmenFrames) / 10f);
            }
            else if (State == StateElementCast) {
                scale *= 1f + 0.04f * GatherLevel();
            }
            return scale;
        }

        private static float SmoothStep01(float t) => t * t * (3f - 2f * t);

        //==================== 绘制 ====================

        public override bool PreDraw(ref Color lightColor) {
            Main.instance.LoadNPC(NPCID.CultistBoss);
            Texture2D tex = TextureAssets.Npc[NPCID.CultistBoss]?.Value;
            if (tex == null) {
                return false;
            }
            int frameH = tex.Height / Main.npcFrameCount[NPCID.CultistBoss];
            Rectangle frame = new(0, frameH * Math.Clamp(frameIndex, 0, Main.npcFrameCount[NPCID.CultistBoss] - 1), tex.Width, frameH);

            float alpha = CurrentAlpha();
            SpriteBatch sb = Main.spriteBatch;

            //本体：血湖材质
            if (alpha > 0.01f) {
                DrawBody(sb, tex, frame, alpha);
            }

            //加色层：符文环 / 祷姿光效 / 元素图腾与蓄力 / 水下血光
            DrawGlow(sb, alpha);

            return false;
        }

        private void DrawBody(SpriteBatch sb, Texture2D tex, Rectangle frame, float alpha) {
            Effect form = EffectLoader.KikasaItemForm?.Value;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            bool shaderOk = form != null && noise != null;

            sb.End();
            sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.PointClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

            Color color;
            if (shaderOk) {
                Main.instance.GraphicsDevice.Textures[1] = noise;
                Main.instance.GraphicsDevice.SamplerStates[1] = SamplerState.LinearWrap;
                form.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
                form.Parameters["uSeed"]?.SetValue(Seed);
                form.Parameters["uForm"]?.SetValue(CurrentForm());
                form.Parameters["uDissolve"]?.SetValue(CurrentDissolve());
                form.Parameters["uScanMode"]?.SetValue(CurrentScanMode());
                form.Parameters["uUvRect"]?.SetValue(new Vector4(
                    frame.X / (float)tex.Width, frame.Y / (float)tex.Height,
                    frame.Width / (float)tex.Width, frame.Height / (float)tex.Height));
                form.Parameters["uTexel"]?.SetValue(new Vector2(1f / tex.Width, 1f / tex.Height));
                form.Parameters["uAspect"]?.SetValue(frame.Width / (float)frame.Height);
                form.CurrentTechnique.Passes[0].Apply();
                color = new Color(255, 255, 255, (byte)(alpha * 255f));
            }
            else {
                //无着色器回退：CPU 血染
                color = Color.Lerp(Color.White, BloodMain, 0.55f) * alpha;
            }

            //原版贴图默认朝向按左处理，面向右翻转（待游戏内验收校正）
            SpriteEffects flip = faceDir > 0 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
            sb.Draw(tex, Projectile.Center - Main.screenPosition, frame, color,
                Projectile.rotation, frame.Size() * 0.5f, BodyScale(), flip, 0f);

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
        }

        private void DrawGlow(SpriteBatch sb, float alpha) {
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
            float spin = Main.GlobalTimeWrappedHourly * 0.8f + Seed;

            //出水符文环 + 环心水下血光
            if (State == StateEmerge) {
                EnsureBegin();
                Vector2 ringCenter = new(Projectile.Center.X, domain.LakeWorldY);
                float litT = RingLitT(Math.Min(t, OmenFrames));
                float ringAlpha;
                if (t < AwakenFrame) {
                    ringAlpha = 0.9f;
                }
                else {
                    //开坛拍：环收拢成掠身光柱后熄灭
                    float k = MathHelper.Clamp((t - AwakenFrame) / 12f, 0f, 1f);
                    ringAlpha = (1f - k) * 1.2f;
                    float r = MathHelper.Lerp(EmergeRingRadius, 6f, SmoothStep01(k));
                    KikasaCultistRunes.DrawWaterRing(sb, ringCenter, r, EmergeRuneCount,
                        1f, spin, Seed, BloodMain, RuneCore, ringAlpha);
                    //收拢光柱：环心向上扫过身体的一道亮带
                    float beam = MathF.Sin(k * MathHelper.Pi);
                    sb.Draw(glow, new Vector2(ringCenter.X, Projectile.Center.Y) - Main.screenPosition, null,
                        FoamGlow * (0.55f * beam), 0f, gOrigin,
                        new Vector2(26f * 2f / glow.Width, 170f * 2f / glow.Height), SpriteEffects.None, 0f);
                }
                if (t < AwakenFrame) {
                    KikasaCultistRunes.DrawWaterRing(sb, ringCenter, EmergeRingRadius, EmergeRuneCount,
                        litT, spin, Seed, BloodMain, RuneCore, ringAlpha);
                    //水下血光自深处上浮，随铭刻进度变宽变亮
                    Vector2 pos = new(ringCenter.X, domain.LakeWorldY + MathHelper.Lerp(46f, 10f, litT));
                    sb.Draw(glow, pos - Main.screenPosition, null, FoamGlow * (0.4f * litT), 0f,
                        gOrigin, new Vector2(EmergeRingRadius * 2.2f / glow.Width, 30f * 2f / glow.Height), SpriteEffects.None, 0f);
                }
            }

            //溶解符文环：环在身体周围重现、反向游光，随身沉没渐暗
            if (State == StateDissolve) {
                EnsureBegin();
                float k = MathHelper.Clamp(t / 12f, 0f, 1f);
                float fade = 1f - MathHelper.Clamp((t - 34f) / 20f, 0f, 1f);
                Vector2 ringCenter = Projectile.Center + new Vector2(0f, 24f);
                KikasaCultistRunes.DrawWaterRing(sb, ringCenter, EmergeRingRadius * 0.8f, EmergeRuneCount,
                    k, -spin, Seed, BloodMain, RuneCore, 0.8f * fade);
            }

            //祷姿光效附加层：姿态即 tell
            if (alpha > 0.1f) {
                EnsureBegin();
                DrawPoseGlow(sb, glow, gOrigin, t);
            }

            if (begun) {
                sb.End();
                sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                    DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
            }

            //元素蓄力有独立的批管理（冰晶虚影要更亮的白芯）
            if (State == StateElementCast && alpha > 0.1f) {
                DrawGatherLayer(sb, glow, gOrigin);
            }
        }

        /// <summary>祷姿附加光：合掌微光 / 指尖点光 / 臂弧 / 袖缘横光。调用方已开加色批</summary>
        private void DrawPoseGlow(SpriteBatch sb, Texture2D glow, Vector2 gOrigin, int t) {
            Pose pose = CurrentPose();
            float breath = 0.5f + 0.5f * MathF.Sin(Main.GlobalTimeWrappedHourly * 2.4f + Seed);

            //面具下的常燃微光：低语的读法
            sb.Draw(glow, Projectile.Center + new Vector2(faceDir * 4f, -16f) - Main.screenPosition, null,
                FoamGlow * (0.16f + 0.1f * breath), 0f, gOrigin,
                new Vector2(9f * 2f / glow.Width), SpriteEffects.None, 0f);

            if (pose == Pose.Point) {
                //单手前指：指尖亮点 + 指向的短流光
                float k = MathHelper.Clamp((t - RiteBrakeEnd) / 8f, 0f, 1f);
                Vector2 finger = FingerPos();
                sb.Draw(glow, finger - Main.screenPosition, null, RuneCore * (0.7f * k), 0f,
                    gOrigin, new Vector2(7f * 2f / glow.Width), SpriteEffects.None, 0f);
                sb.Draw(glow, finger + new Vector2(faceDir * 20f, 0f) - Main.screenPosition, null,
                    BloodMain * (0.4f * k), 0f, gOrigin,
                    new Vector2(30f * 2f / glow.Width, 5f * 2f / glow.Height), SpriteEffects.None, 0f);
            }
            else if (pose == Pose.Raise) {
                //抬双臂过顶：头顶两道臂弧光
                float k = MathHelper.Clamp((t - RiteBrakeEnd) / 10f, 0f, 1f);
                for (int s = -1; s <= 1; s += 2) {
                    Vector2 arc = Projectile.Center + new Vector2(s * 16f, -36f);
                    sb.Draw(glow, arc - Main.screenPosition, null, BloodMain * (0.45f * k),
                        s * 0.6f, gOrigin,
                        new Vector2(22f * 2f / glow.Width, 5f * 2f / glow.Height), SpriteEffects.None, 0f);
                }
                sb.Draw(glow, Projectile.Center + new Vector2(0f, -46f) - Main.screenPosition, null,
                    RuneCore * (0.5f * k * breath), 0f, gOrigin,
                    new Vector2(10f * 2f / glow.Width), SpriteEffects.None, 0f);
            }
            else if (pose == Pose.Spread) {
                //袖袍展开：两袖横向光缘
                float k = MathHelper.Clamp((t - CastBrakeEnd) / 8f, 0f, 1f);
                for (int s = -1; s <= 1; s += 2) {
                    Vector2 edge = Projectile.Center + new Vector2(s * 24f, 2f);
                    sb.Draw(glow, edge - Main.screenPosition, null, BloodMain * (0.4f * k), 0f,
                        gOrigin, new Vector2(26f * 2f / glow.Width, 4.5f * 2f / glow.Height), SpriteEffects.None, 0f);
                }
            }

            //铭刻期：袖口向法阵淌出流光（确定性相位，各端一致）
            if (State == StateRuneRite && t > RiteSpawnFrame && t <= RiteInscribeEnd && riteCircleCount > 0) {
                Vector2 hand = (int)StateParam == 1
                    ? Projectile.Center + new Vector2(0f, -42f) : FingerPos();
                for (int c = 0; c < riteCircleCount; c++) {
                    Vector2 anchor = riteCirclePos[c] + new Vector2(0f, -14f);
                    for (int k = 0; k < 3; k++) {
                        float phase = (Main.GlobalTimeWrappedHourly * 0.7f + k / 3f + Seed * 0.31f + c * 0.17f) % 1f;
                        Vector2 pos = Vector2.Lerp(hand, anchor, phase);
                        float a = MathF.Sin(phase * MathHelper.Pi) * 0.4f;
                        Vector2 dir = anchor - hand;
                        sb.Draw(glow, pos - Main.screenPosition, null, BloodMain * a,
                            dir.ToRotation(), gOrigin,
                            new Vector2(22f * 2f / glow.Width, 4f * 2f / glow.Height), SpriteEffects.None, 0f);
                    }
                }
            }

            //元素图腾：展袖拍在头顶一闪，预告本轮元素
            if (State == StateElementCast && t > CastBrakeEnd + 2 && t <= CastTellEnd + 6) {
                float f = MathHelper.Clamp((t - CastBrakeEnd - 2f) / (CastTellEnd + 6f - CastBrakeEnd - 2f), 0f, 1f);
                float a = MathF.Sin(f * MathHelper.Pi);
                DrawElementTotem(sb, glow, gOrigin, (int)StateParam, Projectile.Center + new Vector2(0f, -54f), a);
            }
        }

        /// <summary>头顶元素图腾：冰=三叉晶 / 火=双点绕环 / 雷=折线闪。调用方已开加色批</summary>
        private void DrawElementTotem(SpriteBatch sb, Texture2D glow, Vector2 gOrigin, int element, Vector2 seat, float a) {
            if (a <= 0.03f) {
                return;
            }
            if (element == 0) {
                for (int i = -1; i <= 1; i++) {
                    float ang = -MathHelper.PiOver2 + i * 0.5f;
                    Vector2 tip = seat + ang.ToRotationVector2() * 10f;
                    sb.Draw(glow, tip - Main.screenPosition, null, IceTint * (0.75f * a), ang,
                        gOrigin, new Vector2(14f * 2f / glow.Width, 3.4f * 2f / glow.Height), SpriteEffects.None, 0f);
                }
                sb.Draw(glow, seat - Main.screenPosition, null, RuneCore * (0.5f * a), 0f,
                    gOrigin, new Vector2(6f * 2f / glow.Width), SpriteEffects.None, 0f);
            }
            else if (element == 1) {
                float orbit = Main.GlobalTimeWrappedHourly * 6f + Seed;
                for (int i = 0; i < 2; i++) {
                    Vector2 p = seat + (orbit + i * MathHelper.Pi).ToRotationVector2() * 9f;
                    sb.Draw(glow, p - Main.screenPosition, null, FireTint * (0.8f * a), 0f,
                        gOrigin, new Vector2(6.5f * 2f / glow.Width), SpriteEffects.None, 0f);
                }
                sb.Draw(glow, seat - Main.screenPosition, null, FireTint * (0.35f * a), 0f,
                    gOrigin, new Vector2(14f * 2f / glow.Width), SpriteEffects.None, 0f);
            }
            else {
                //三段折线闪电
                Vector2 prev = seat + new Vector2(-8f, -10f);
                for (int i = 1; i <= 3; i++) {
                    float jitter = (KikasaCultistRunes.Hash01(Seed * 9f + i * 3.3f + (int)(Main.GlobalTimeWrappedHourly * 8f)) - 0.5f) * 10f;
                    Vector2 next = seat + new Vector2(-8f + i * 6f + jitter * 0.4f, -10f + i * 7f + jitter);
                    Vector2 mid = (prev + next) * 0.5f;
                    Vector2 dir = next - prev;
                    sb.Draw(glow, mid - Main.screenPosition, null, ThunderTint * (0.8f * a),
                        dir.ToRotation(), gOrigin,
                        new Vector2(dir.Length() * 1.1f / glow.Width * 2f, 3f * 2f / glow.Height), SpriteEffects.None, 0f);
                    prev = next;
                }
            }
        }

        /// <summary>元素蓄力层：冰晶虚影阵列 / 双袖火种 / 头顶雷球。自管加色批</summary>
        private void DrawGatherLayer(SpriteBatch sb, Texture2D glow, Vector2 gOrigin) {
            float gather = GatherLevel();
            if (gather <= 0.02f) {
                return;
            }
            int element = (int)StateParam;
            int t = (int)StateTimer;
            Player owner = Owner;
            int target = FindTarget(owner);
            Vector2 aimPos = target >= 0 ? Main.npc[target].Center : Projectile.Center + new Vector2(faceDir * 300f, -40f);
            Vector2 aim = (aimPos - Projectile.Center).SafeNormalize(-Vector2.UnitY);

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

            //静默拍收缩：释放前光核缩小 40%（爆发前吸气的视觉半句）
            float silence = t > CastGatherEnd ? 1f - 0.4f * MathHelper.Clamp((t - CastGatherEnd) / 6f, 0f, 1f) : 1f;

            if (element == 0) {
                //冰晶虚影阵列：逐枚从雾影到晶体清晰
                for (int i = 0; i < 5; i++) {
                    float show = MathHelper.Clamp(gather * 5f - i, 0f, 1f);
                    if (show <= 0f) {
                        continue;
                    }
                    Vector2 pos = IceGhostPos(i, aim);
                    float ang = aim.RotatedBy((i - 2) * 0.21f).ToRotation();
                    float len = MathHelper.Lerp(6f, 15f, show) * silence;
                    //菱形：纵横两枚交叠的拉伸光
                    sb.Draw(glow, pos - Main.screenPosition, null, IceTint * (0.65f * show), ang,
                        gOrigin, new Vector2(len * 2f / glow.Width, 4f * 2f / glow.Height), SpriteEffects.None, 0f);
                    sb.Draw(glow, pos - Main.screenPosition, null, IceTint * (0.35f * show), ang,
                        gOrigin, new Vector2(len * 0.5f * 2f / glow.Width, 7f * 2f / glow.Height), SpriteEffects.None, 0f);
                    if (show >= 1f) {
                        //凝成瞬间的白芯闪
                        sb.Draw(glow, pos - Main.screenPosition, null, RuneCore * (0.45f * silence), ang,
                            gOrigin, new Vector2(4f * 2f / glow.Width), SpriteEffects.None, 0f);
                    }
                }
            }
            else if (element == 1) {
                //双袖火种：反相呼吸生长
                for (int s = 0; s < 2; s++) {
                    Vector2 hand = SleevePos(s == 0 ? 1 : -1);
                    float wob = 1f + 0.12f * MathF.Sin(Main.GlobalTimeWrappedHourly * 9f + s * MathHelper.Pi + Seed);
                    float r = MathHelper.Lerp(3f, 9f, gather) * wob * silence;
                    sb.Draw(glow, hand - Main.screenPosition, null, FireTint * (0.75f * gather), 0f,
                        gOrigin, new Vector2(r * 2f / glow.Width), SpriteEffects.None, 0f);
                    sb.Draw(glow, hand - Main.screenPosition, null, BloodMain * (0.4f * gather), 0f,
                        gOrigin, new Vector2(r * 2.4f * 2f / glow.Width), SpriteEffects.None, 0f);
                }
            }
            else {
                //头顶雷球：生长的紫电核 + 确定性抖动的短弧
                Vector2 seat = Projectile.Center + new Vector2(0f, -48f);
                float r = MathHelper.Lerp(4f, 12f, gather) * silence;
                sb.Draw(glow, seat - Main.screenPosition, null, ThunderTint * (0.7f * gather), 0f,
                    gOrigin, new Vector2(r * 2f / glow.Width), SpriteEffects.None, 0f);
                sb.Draw(glow, seat - Main.screenPosition, null, RuneCore * (0.4f * gather * silence), 0f,
                    gOrigin, new Vector2(r * 0.45f * 2f / glow.Width), SpriteEffects.None, 0f);
                int jitterSeed = (int)(Main.GlobalTimeWrappedHourly * 12f);
                for (int i = 0; i < 3; i++) {
                    float h = KikasaCultistRunes.Hash01(jitterSeed * 1.7f + i * 5.9f + Seed);
                    float ang = h * MathHelper.TwoPi;
                    Vector2 tip = seat + ang.ToRotationVector2() * (r + 8f + h * 8f);
                    Vector2 mid = (seat + tip) * 0.5f;
                    sb.Draw(glow, mid - Main.screenPosition, null, ThunderTint * (0.5f * gather),
                        (tip - seat).ToRotation(), gOrigin,
                        new Vector2((tip - seat).Length() * 1.05f * 2f / glow.Width, 2.6f * 2f / glow.Height), SpriteEffects.None, 0f);
                }
            }

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
        }

        //==================== 谢幕 ====================

        public override void OnKill(int timeLeft) {
            //谢幕残珠：溶解尾拍或异常移除都留一口血水与几片符屑
            if (Main.dedServ) {
                return;
            }
            for (int i = 0; i < 9; i++) {
                PRTLoader.NewParticle<PRT_GhostRainDrop>(
                    Projectile.Center + Main.rand.NextVector2Circular(20f, 30f),
                    new Vector2(Main.rand.NextFloat(-1f, 1f), Main.rand.NextFloat(0.5f, 2.6f)),
                    BloodMain * 0.5f, Main.rand.NextFloat(0.35f, 0.6f))?.Configure(Main.rand.Next(14, 26), 0f);
            }
            for (int i = 0; i < 4; i++) {
                PRTLoader.NewParticle<PRT_Sparkle>(
                    Projectile.Center + Main.rand.NextVector2Circular(16f, 24f),
                    new Vector2(Main.rand.NextFloat(-0.5f, 0.5f), -Main.rand.NextFloat(0.3f, 0.9f)),
                    FoamGlow, Main.rand.NextFloat(0.22f, 0.38f))
                    ?.Configure(FoamGlow * 0.5f, Main.rand.Next(16, 28), 0.05f, 0.6f);
            }
            PRTLoader.NewParticle<PRT_GhostRainMist>(Projectile.Center,
                new Vector2(0f, -0.25f), MistBlood * 0.7f, Main.rand.NextFloat(0.6f, 0.9f))
                ?.Configure(Main.rand.Next(50, 80));
        }
    }
}
