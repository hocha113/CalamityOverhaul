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

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaServants.KikasaQueenBee
{
    /// <summary>
    /// 鬼奴·湖水版血巢蜂后。血湖之水凝成的蜂后随从：
    /// 出水四拍（蜂巢涌泡预兆→破水浪冠→血水升起凝实→抖翅甩水觉醒），
    /// 签名机制为全横幅定高耙扫——锁定目标高度后拉出屏外距离，折返做贯穿
    /// 战场宽度的车道式横扫，多次来回，每次折返有减速-悬停-转身-振翅蓄力 tell；
    /// 副攻为腹部泼出的短命追踪血蜂群与螫针上抛重力雨。
    /// 联机契约同克眼基准：状态机各端同推，owner 转场盖 netUpdate 章，
    /// 子弹幕只在 owner 端生成，节拍闩防快照回卷，生命线只有 owner 判
    /// </summary>
    internal class KikasaQueenBeeServant : ModProjectile, IKikasaServant
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        //==================== 可调基数（占位初值，验收再调）====================

        /// <summary>耙扫接触基伤（召唤加成前）</summary>
        internal const int RakeDamage = 480;

        /// <summary>血蜂与螫针基伤（召唤加成前），由子弹幕消费</summary>
        internal const int SwarmDamage = 250;

        //==================== 状态 ====================

        private const int StateEmerge = 0;
        private const int StateFollow = 1;
        private const int StateRake = 2;
        private const int StateBees = 3;
        private const int StateStingers = 4;
        private const int StateDissolve = 5;

        private int State { get => (int)Projectile.ai[0]; set => Projectile.ai[0] = value; }
        private ref float StateTimer => ref Projectile.ai[1];
        /// <summary>状态内子参数：耙扫=趟数×10+相位，血蜂=已泼窝数，其余为普通相位号</summary>
        private ref float StateParam => ref Projectile.ai[2];

        //耙扫相位（编码进 StateParam 的个位）
        private const int RakeReposition = 0;
        private const int RakeTell = 1;
        private const int RakeSweep = 2;
        private const int RakeBrake = 3;

        private int RakePass => (int)StateParam / 10;
        private int RakePhase => (int)StateParam % 10;

        //==================== 时序 ====================

        //出水：涌泡预兆→破水→升起凝实→抖翅觉醒→落定
        private const int OmenFrames = 26;
        private const int RiseEnd = 60;
        private const int WingShakeFrame = 66;
        private const int ScanSettleEnd = 74;
        private const int EmergeTotal = 88;

        //耙扫：首趟先拉出屏外，之后每趟折返 tell→一帧起速→横扫→硬刹
        private const float StagingDist = 1120f;
        private const int RepositionMax = 66;
        private const int TellFirst = 34;
        private const int TellNext = 26;
        private const int SweepMax = 85;
        private const float SweepEndMargin = 1060f;
        private const int BrakeFrames = 14;
        private const int RakePassCount = 3;

        //血蜂：刹停→腹部鼓动蓄力（72% 后静默）→三窝连泼→回摆
        private const int BeesBrakeEnd = 8;
        private const int BeesChargeEnd = 30;
        private const int BeeBurstGap = 9;
        private const int BeeBurstCount = 3;
        private const int BeesPerBurst = 3;
        private const int BeesFireEnd = BeesChargeEnd + BeeBurstGap * BeeBurstCount;
        private const int BeesRecoverEnd = BeesFireEnd + 16;
        /// <summary>场上自有血蜂的数量预算封顶</summary>
        private const int BeeBudgetCap = 10;

        //螫针：刹停仰身→尾部蓄力（72% 后静默）→一拍上抛→目送螫针雨→回摆
        private const int StingBrakeEnd = 10;
        private const int StingChargeEnd = 32;
        private const int StingWatchEnd = 88;
        private const int StingRecoverEnd = 102;
        private const int StingerFanCount = 7;

        private const int DissolveFrames = 46;

        //==================== 本地表现量（不入同步，节拍闩防快照回卷重播）====================

        private int frameTick;
        private int frameIndex;
        private int attackCooldown;
        private int attackIndex;
        private int lastSeenState = -1;
        private bool breachDone;
        private bool wingShakeDone;
        //耙扫节拍闩全用趟号单调比较：快照回卷不会把同一趟的节拍再放一遍
        private int lastTellBuzzed = -1;
        private int lastSweepLaunched = -1;
        private int lastBrakeFlung = -1;
        /// <summary>首趟拉出侧向的闩（0=未定），防目标横穿时锚点左右横跳</summary>
        private float repoSide;
        private bool tossDone;
        private int lastBeeBurst = -1;
        private bool dissolveSplashed;
        /// <summary>朝向：-1 原生面左，+1 翻转面右</summary>
        private int faceDir = -1;
        /// <summary>抖翅觉醒的余闪帧</summary>
        private int wingFlashTimer;
        /// <summary>悬停滴蜜的落水微涟漪日程（纯本地演出）</summary>
        private readonly List<Vector2> pendingRipples = new();

        //==================== 血色板（随观看域鬼雨异化冷化，蜂蜜琥珀只做次要点缀）====================

        private static Color BloodTint => KikasaDomain.CoolTint(new(237, 77, 69), new(126, 158, 164));
        private static Color BloodDeep => KikasaDomain.CoolTint(new(140, 32, 30), new(84, 104, 110));
        private static Color FoamGlow => KikasaDomain.CoolTint(new(246, 133, 112), new(176, 200, 204));
        private static Color MistBlood => KikasaDomain.CoolTint(new(58, 18, 20), new(52, 62, 66));
        /// <summary>血蜜琥珀：腹部蜜光与蜂翅微光的专属次要色</summary>
        private static Color HoneyGlow => KikasaDomain.CoolTint(new(243, 156, 74), new(168, 190, 194));

        private Player Owner => Main.player[Projectile.owner];

        /// <summary>连续量抖动的确定性相位，各端一致（9.1：不掷 Main.rand）</summary>
        private float Seed => Projectile.identity * 0.7391f;

        //==================== 召唤入口 ====================

        /// <summary>KikasaServantIndex 登记的召唤委托；emergeAt.Y = 湖面</summary>
        internal static void Summon(Player owner, Vector2 emergeAt) {
            if (owner.whoAmI != Main.myPlayer) {
                return;
            }
            int damage = (int)owner.GetTotalDamage(DamageClass.Summon).ApplyTo(RakeDamage);
            Projectile.NewProjectile(owner.GetSource_Misc("KikasaServant"),
                emergeAt + new Vector2(0f, 30f), Vector2.Zero,
                ModContent.ProjectileType<KikasaQueenBeeServant>(), damage, 7f, owner.whoAmI);
        }

        //==================== 定义 ====================

        public override void SetStaticDefaults() {
            Main.projPet[Projectile.type] = true;
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 10;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
            ProjectileID.Sets.MinionSacrificable[Projectile.type] = false;
            //耙扫会拉到屏外再折返，出屏也要画
            ProjectileID.Sets.DrawScreenCheckFluff[Projectile.type] = 800;
        }

        public override void SetDefaults() {
            Projectile.width = 108;
            Projectile.height = 80;
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

        /// <summary>接触伤害只开在耙扫横扫窗，与可见的车道冲锋严格对齐</summary>
        public override bool? CanDamage()
            => State == StateRake && RakePhase == RakeSweep ? null : false;

        public override bool? CanCutTiles() => false;

        //==================== 遣返 ====================

        public bool IsDismissing => State == StateDissolve;

        public void BeginDismiss() {
            if (Main.myPlayer == Projectile.owner && State != StateDissolve) {
                BeginDissolve();
            }
        }

        private void BeginDissolve() {
            //破水前后几帧内收场：本体还没显形，不走溶解演出——
            //否则透明度会从淡入半途跳满，水面凭空闪出一只蜂后再化掉
            if (State == StateEmerge && StateTimer < OmenFrames + 4) {
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

            //生命线：湖塌/收域/主人死亡 → 溶解回湖。只有 owner 裁决——
            //服务器无领域状态（恒 Closed 是既定契约），别处判会当场误杀
            if (authority && State != StateDissolve && !LakeHealthy(owner, domain)) {
                BeginDissolve();
            }

            Projectile.timeLeft = 180;
            //伤害随召唤加成逐帧刷新，命中在 owner 端结算
            Projectile.damage = (int)owner.GetTotalDamage(DamageClass.Summon).ApplyTo(RakeDamage);

            //换场清闩：远端可能靠收包切状态而非本地同拍转场
            if (State != lastSeenState) {
                lastSeenState = State;
                lastBeeBurst = -1;
                lastTellBuzzed = -1;
                lastSweepLaunched = -1;
                lastBrakeFlung = -1;
                repoSide = 0f;
                tossDone = false;
                if (State == StateDissolve) {
                    dissolveSplashed = false;
                }
            }

            StateTimer++;
            switch (State) {
                case StateEmerge: UpdateEmerge(domain); break;
                case StateFollow: UpdateFollow(owner, domain, authority); break;
                case StateRake: UpdateRake(owner, domain, authority); break;
                case StateBees: UpdateBees(owner, authority); break;
                case StateStingers: UpdateStingers(owner, authority); break;
                case StateDissolve: UpdateDissolve(domain, authority); break;
            }

            UpdateFrames();
            UpdateWingMist();
            UpdatePendingRipples(domain);
            if (attackCooldown > 0) {
                attackCooldown--;
            }
            if (wingFlashTimer > 0) {
                wingFlashTimer--;
            }

            float glow = CurrentAlpha() * 0.5f;
            if (glow > 0.02f) {
                Lighting.AddLight(Projectile.Center, 0.4f * glow, 0.13f * glow, 0.08f * glow);
            }
        }

        private static bool LakeHealthy(Player owner, KikasaDomainPlayer domain)
            => !owner.dead && domain.AnyActive
            && domain.Phase != KikasaDomainPhase.Closing
            && domain.RiseT >= 0.9f;

        //==================== 出水 ====================

        private void UpdateEmerge(KikasaDomainPlayer domain) {
            float lakeY = domain.LakeWorldY;
            int t = (int)StateTimer;
            bool viewed = ViewedOwner;

            if (t < OmenFrames) {
                //水下待命：巢在湖底苏醒，涌泡与收拢的涟漪是预兆
                Projectile.velocity = Vector2.Zero;
                if (viewed) {
                    if (t % 5 == 2) {
                        float converge = 1f - t / (float)OmenFrames;
                        float side = t / 5 % 2 == 0 ? 1f : -1f;
                        KikasaDomainDeco.RippleAt(
                            new Vector2(Projectile.Center.X + side * converge * 46f, lakeY),
                            0.35f + (1f - converge) * 0.5f);
                    }
                    //蜂巢涌泡：水线冒起细密碎泡，一阵密过一阵
                    if (t % 4 == 1) {
                        PRTLoader.NewParticle<PRT_GhostRainDrop>(
                            new Vector2(Projectile.Center.X + Main.rand.NextFloat(-30f, 30f), lakeY - 2f),
                            new Vector2(Main.rand.NextFloat(-0.4f, 0.4f), -Main.rand.NextFloat(1f, 2.2f + t * 0.05f)),
                            FoamGlow * Main.rand.NextFloat(0.3f, 0.45f),
                            Main.rand.NextFloat(0.3f, 0.5f))
                            ?.Configure(Main.rand.Next(12, 20), 0f);
                    }
                    if (t == 6 || t == 18) {
                        SoundEngine.PlaySound(SoundID.Drip with {
                            Volume = 0.45f,
                            Pitch = t == 6 ? -0.5f : -0.15f,
                            MaxInstances = 2
                        }, new Vector2(Projectile.Center.X, lakeY));
                    }
                    //闷在水下的嗡鸣，越涨越近
                    if (t == 12) {
                        SoundEngine.PlaySound(SoundID.Item97 with { Volume = 0.3f, Pitch = -0.9f, MaxInstances = 2 },
                            new Vector2(Projectile.Center.X, lakeY));
                    }
                }
                return;
            }

            if (!breachDone) {
                //破水拍：一帧起速 + 浪冠 + 蜂后嗡吼
                breachDone = true;
                Projectile.velocity = new Vector2(0f, -12.4f);
                SoundEngine.PlaySound(SoundID.Roar with { Volume = 0.42f, Pitch = -0.25f, MaxInstances = 2 }, Projectile.Center);
                SoundEngine.PlaySound(SoundID.Zombie125 with { Volume = 0.7f, Pitch = -0.3f, MaxInstances = 2 }, Projectile.Center);
                if (viewed) {
                    BreachBurst(new Vector2(Projectile.Center.X, lakeY));
                }
            }

            //升起：起速后指数衰减，前快后慢，禁匀速
            Projectile.velocity.Y *= 0.955f;
            Projectile.velocity.X = 0f;
            Projectile.rotation = Projectile.rotation.AngleLerp(0f, 0.2f);

            if (viewed && t < RiseEnd) {
                //身上的血水成帘往下淌，落点连环小涟漪
                if (t % 2 == 0) {
                    Vector2 dropPos = Projectile.Center + new Vector2(
                        Main.rand.NextFloat(-40f, 40f), Main.rand.NextFloat(4f, 28f));
                    PRTLoader.NewParticle<PRT_GhostRainDrop>(dropPos,
                        new Vector2(Main.rand.NextFloat(-0.4f, 0.4f), Main.rand.NextFloat(2.4f, 3.8f)),
                        BloodTint * Main.rand.NextFloat(0.4f, 0.6f),
                        Main.rand.NextFloat(0.45f, 0.7f))
                        ?.Configure(Main.rand.Next(14, 26), 0f);
                }
                if (t % 5 == 3) {
                    KikasaDomainDeco.RippleAt(
                        new Vector2(Projectile.Center.X + Main.rand.NextFloat(-24f, 24f), lakeY), 0.35f);
                }
            }

            if (!wingShakeDone && t >= WingShakeFrame) {
                //觉醒拍：抖翅甩水——湿虫甩干翅膀，血珠横着排开
                wingShakeDone = true;
                wingFlashTimer = 10;
                SoundEngine.PlaySound(SoundID.Zombie125 with { Volume = 0.5f, Pitch = 0.15f, MaxInstances = 2 }, Projectile.Center);
                if (viewed) {
                    for (int i = 0; i < 14; i++) {
                        float side = i % 2 == 0 ? 1f : -1f;
                        Vector2 wing = Projectile.Center + new Vector2(side * Main.rand.NextFloat(16f, 34f), -Main.rand.NextFloat(10f, 26f));
                        PRTLoader.NewParticle<PRT_KikasaBloodGlob>(wing,
                            new Vector2(side * Main.rand.NextFloat(2.6f, 5.4f), -Main.rand.NextFloat(0.4f, 1.6f)),
                            Main.rand.NextBool(3) ? BloodDeep : BloodTint,
                            Main.rand.NextFloat(0.35f, 0.6f))?.Configure(Main.rand.Next(16, 28));
                    }
                    KikasaDomainDeco.RippleAt(new Vector2(Projectile.Center.X, lakeY), 0.5f);
                    ShakeViewer(1.6f);
                }
            }

            //觉醒后视线转向猎物
            if (t >= WingShakeFrame) {
                int target = FindTarget(Owner);
                Vector2 look = target >= 0 ? Main.npc[target].Center : Owner.Center;
                FaceX(look.X);
            }

            if (t >= EmergeTotal) {
                //转场确定性（纯计时），各端同拍；owner 盖章纠偏
                State = StateFollow;
                StateTimer = 0;
                StateParam = 0;
                attackCooldown = 30;
                Projectile.netUpdate = Main.myPlayer == Projectile.owner;
            }
        }

        /// <summary>破水浪冠：大环涟漪 + 扇形血珠 + 垂直水柱 + 血雾，蜂后体格压过克眼一线</summary>
        private void BreachBurst(Vector2 hit) {
            KikasaDomainDeco.RippleAt(hit, 2.5f);
            KikasaDomainDeco.RippleAt(hit + new Vector2(42f, 0f), 1.0f);
            KikasaDomainDeco.RippleAt(hit - new Vector2(40f, 0f), 1.0f);
            KikasaDomainDeco.SplashAt(hit + new Vector2(-18f, 0f), 12);
            KikasaDomainDeco.SplashAt(hit + new Vector2(18f, 0f), 12);

            for (int i = 0; i < 24; i++) {
                float angle = -MathHelper.Pi * (0.12f + 0.76f * i / 23f);
                float speed = Main.rand.NextFloat(3.2f, 7.8f);
                PRTLoader.NewParticle<PRT_KikasaBloodGlob>(
                    hit + new Vector2(Main.rand.NextFloat(-30f, 30f), -4f),
                    angle.ToRotationVector2() * speed,
                    Main.rand.NextBool(3) ? BloodDeep : BloodTint,
                    Main.rand.NextFloat(0.5f, 0.9f))?.Configure(Main.rand.Next(22, 38));
            }
            for (int i = 0; i < 10; i++) {
                PRTLoader.NewParticle<PRT_KikasaBloodGlob>(
                    hit + new Vector2(Main.rand.NextFloat(-9f, 9f), -6f),
                    new Vector2(Main.rand.NextFloat(-0.9f, 0.9f), -Main.rand.NextFloat(8.5f, 13.5f)),
                    BloodTint * 0.9f, Main.rand.NextFloat(0.55f, 0.95f))
                    ?.Configure(Main.rand.Next(34, 52));
            }
            for (int i = 0; i < 3; i++) {
                PRTLoader.NewParticle<PRT_GhostRainMist>(
                    hit + new Vector2(Main.rand.NextFloat(-34f, 34f), -10f),
                    new Vector2(Main.rand.NextFloat(-0.3f, 0.3f), -Main.rand.NextFloat(0.35f, 0.75f)),
                    MistBlood * 0.8f, Main.rand.NextFloat(0.75f, 1.05f))
                    ?.Configure(Main.rand.Next(60, 100));
            }
            PRTLoader.NewParticle<PRT_DWave>(hit, Vector2.Zero, BloodDeep, 0.09f)
                ?.Configure(new Vector2(0.5f, 1f), -MathHelper.PiOver2, 0.32f, 10);

            SoundEngine.PlaySound(SoundID.SplashWeak with { Volume = 1f, Pitch = -0.3f, MaxInstances = 2 }, hit);
            SoundEngine.PlaySound(SoundID.DD2_MonkStaffGroundImpact with { Volume = 0.5f, Pitch = -0.65f, MaxInstances = 1 }, hit);
            ShakeViewer(5f);
        }

        //==================== 跟随 ====================

        private void UpdateFollow(Player owner, KikasaDomainPlayer domain, bool authority) {
            int target = FindTarget(owner);

            //悬在主人侧上方，蜂式八字微摆（纵向倍频，虫的悬停不是浮标的漂）
            Vector2 anchor = owner.Center + new Vector2(-owner.direction * 95f, -126f);
            anchor.X += MathF.Sin(Main.GlobalTimeWrappedHourly * 1.7f + Seed) * 10f;
            anchor.Y += MathF.Sin(Main.GlobalTimeWrappedHourly * 3.4f + Seed * 2f) * 5f;

            Vector2 to = anchor - Projectile.Center;
            if (to.Length() > 2400f) {
                //跟丢就贴回来
                Projectile.Center = anchor;
                Projectile.velocity = Vector2.Zero;
                Projectile.netUpdate = authority;
                return;
            }
            Vector2 desired = to * 0.09f;
            const float maxSpeed = 16f;
            if (desired.Length() > maxSpeed) {
                desired = desired.SafeNormalize(Vector2.Zero) * maxSpeed;
            }
            Projectile.velocity = Vector2.Lerp(Projectile.velocity, desired, 0.13f);
            Projectile.rotation = Projectile.rotation.AngleLerp(
                MathHelper.Clamp(Projectile.velocity.X * 0.016f, -0.2f, 0.2f), 0.15f);

            //有猎物盯猎物，闲着看主人
            FaceX(target >= 0 ? Main.npc[target].Center.X : owner.Center.X);

            //悬停滴蜜：腹部血蜜偶发坠落，落水荡微圈
            if (!Main.dedServ && Main.rand.NextBool(34)) {
                DripHoney(domain);
            }

            //出手裁决：耙扫为主轴，血蜂/螫针穿插；规则确定性，owner 盖章
            if (target >= 0 && attackCooldown <= 0 && StateTimer > 26) {
                int slot = attackIndex % 4;
                attackIndex++;
                State = slot switch {
                    0 => StateRake,
                    1 => StateBees,
                    2 => StateRake,
                    _ => StateStingers,
                };
                StateTimer = 0;
                StateParam = State == StateRake ? RakeReposition : 0;
                Projectile.netUpdate = authority;
            }
        }

        /// <summary>腹部滴蜜 + 登记落水微涟漪日程（纯本地演出）</summary>
        private void DripHoney(KikasaDomainPlayer domain) {
            Vector2 belly = BellyPos();
            PRTLoader.NewParticle<PRT_KikasaBloodGlob>(belly,
                new Vector2(Projectile.velocity.X * 0.1f, Main.rand.NextFloat(0.6f, 1.3f)),
                Color.Lerp(BloodTint, HoneyGlow, 0.4f) * Main.rand.NextFloat(0.45f, 0.6f),
                Main.rand.NextFloat(0.3f, 0.5f))?.Configure(Main.rand.Next(60, 90), 0.3f);

            //只有悬在湖面上方不远时才配微圈，日程按自由落体估算
            float h = domain.LakeWorldY - belly.Y;
            if (ViewedOwner && h > 20f && h < 700f && pendingRipples.Count < 6) {
                float eta = MathF.Sqrt(2f * h / 0.3f);
                pendingRipples.Add(new Vector2(belly.X, eta));
            }
        }

        /// <summary>消化滴蜜日程：到点在湖面荡出微圈（X 存落点，Y 存剩余帧）</summary>
        private void UpdatePendingRipples(KikasaDomainPlayer domain) {
            if (Main.dedServ || pendingRipples.Count == 0) {
                return;
            }
            bool lakeAlive = domain.AnyActive && domain.RiseT > 0.5f;
            for (int i = pendingRipples.Count - 1; i >= 0; i--) {
                Vector2 entry = pendingRipples[i];
                entry.Y -= 1f;
                if (entry.Y <= 0f) {
                    pendingRipples.RemoveAt(i);
                    if (lakeAlive && ViewedOwner) {
                        KikasaDomainDeco.RippleAt(new Vector2(entry.X, domain.LakeWorldY),
                            Main.rand.NextFloat(0.16f, 0.24f));
                    }
                    continue;
                }
                pendingRipples[i] = entry;
            }
        }

        //==================== 全横幅定高耙扫 ====================

        private void UpdateRake(Player owner, KikasaDomainPlayer domain, bool authority) {
            int t = (int)StateTimer;
            int pass = RakePass;
            int phase = RakePhase;
            int target = FindTarget(owner);

            void NextPhase(int nextPass, int nextPhase) {
                StateParam = nextPass * 10 + nextPhase;
                StateTimer = 0;
                Projectile.netUpdate = authority;
            }

            //起手与折返 tell 里猎物没了就收势回位
            if (target < 0 && phase is RakeReposition or RakeTell) {
                EndAttack(authority, 45);
                return;
            }

            Vector2 targetPos = target >= 0 ? Main.npc[target].Center : owner.Center;

            if (phase == RakeReposition) {
                //首趟：水平拉出屏外距离——就近一侧闩定，防目标横穿时锚点左右横跳
                if (repoSide == 0f) {
                    repoSide = MathF.Sign(Projectile.Center.X - targetPos.X);
                    if (repoSide == 0f) {
                        repoSide = -owner.direction;
                    }
                }
                //锚点钳进世界安全区：出界的弹幕会被原版直接杀掉
                float stagingX = MathHelper.Clamp(targetPos.X + repoSide * StagingDist,
                    760f, Main.maxTilesX * 16f - 760f);
                Vector2 staging = new(stagingX, targetPos.Y);
                Vector2 want = (staging - Projectile.Center) * 0.075f;
                if (want.Length() > 27f) {
                    want = want.SafeNormalize(Vector2.Zero) * 27f;
                }
                Projectile.velocity = Vector2.Lerp(Projectile.velocity, want, 0.16f);
                FaceX(staging.X);
                Projectile.rotation = Projectile.rotation.AngleLerp(
                    MathHelper.Clamp(Projectile.velocity.X * 0.012f, -0.18f, 0.18f), 0.2f);

                if (MathF.Abs(Projectile.Center.X - staging.X) < 60f || t >= RepositionMax) {
                    NextPhase(0, RakeTell);
                }
                return;
            }

            if (phase == RakeTell) {
                //折返 tell：减速-悬停-对高-转身-振翅蓄力，一次冲锋内高度就此锁定
                int tellDur = pass == 0 ? TellFirst : TellNext;
                float p = MathHelper.Clamp(t / (float)tellDur, 0f, 1f);
                float dir = MathF.Sign(targetPos.X - Projectile.Center.X);
                if (dir == 0f) {
                    dir = faceDir;
                }

                //横向锚定原地，纵向缓缓对齐目标高度（耙扫高度按目标微调）
                float wantY = MathHelper.Clamp((targetPos.Y - Projectile.Center.Y) * 0.11f, -9f, 9f);
                Projectile.velocity.Y = MathHelper.Lerp(Projectile.velocity.Y, wantY, 0.25f);
                //迟发后拉：pow(6) 憋到最后几帧猛吸一口气
                float reel = MathF.Pow(p, 6f) * 14f;
                Projectile.velocity.X = MathHelper.Lerp(Projectile.velocity.X, -dir * (1.2f + reel), 0.3f);

                //转身拍：tell 中点完成掉头，之前保持来向
                if (p > 0.35f) {
                    FaceX(targetPos.X);
                }
                Projectile.rotation = Projectile.rotation.AngleLerp(0f, 0.25f);

                if (lastTellBuzzed < pass) {
                    //折返起点的振翅嗡鸣渐起
                    lastTellBuzzed = pass;
                    wingFlashTimer = tellDur;
                    SoundEngine.PlaySound(SoundID.Zombie125 with {
                        Volume = 0.36f,
                        Pitch = 0.25f + pass * 0.07f,
                        MaxInstances = 3
                    }, Projectile.Center);
                }
                //蓄力血珠向翅根收拢，72% 后静默——爆发前的吸气
                if (!Main.dedServ && p < 0.72f && t % 3 == 1) {
                    Vector2 root = Projectile.Center + new Vector2(0f, -14f);
                    Vector2 from = root + Main.rand.NextVector2Unit() * Main.rand.NextFloat(46f, 92f);
                    PRTLoader.NewParticle<PRT_GhostRainDrop>(from,
                        (root - from) * 0.15f,
                        BloodTint * 0.5f, Main.rand.NextFloat(0.3f, 0.5f))
                        ?.Configure(8, 0f);
                }

                if (t >= tellDur) {
                    NextPhase(pass, RakeSweep);
                }
                return;
            }

            if (phase == RakeSweep) {
                if (lastSweepLaunched < pass) {
                    //launch 一帧定速：车道方向水平贯穿，纵速清零——定高锁死
                    lastSweepLaunched = pass;
                    float dir = target >= 0 ? MathF.Sign(targetPos.X - Projectile.Center.X) : faceDir;
                    if (dir == 0f) {
                        dir = faceDir;
                    }
                    Projectile.velocity = new Vector2(dir * 26f, 0f);
                    Projectile.netUpdate = authority;
                    FaceX(Projectile.Center.X + dir * 120f);
                    SoundEngine.PlaySound(SoundID.Zombie125 with { Volume = 0.65f, Pitch = 0f, MaxInstances = 3 }, Projectile.Center);
                    SoundEngine.PlaySound(SoundID.Roar with { Volume = 0.4f, Pitch = -0.2f, MaxInstances = 2 }, Projectile.Center);
                    if (!Main.dedServ) {
                        PRTLoader.NewParticle<PRT_DWave>(Projectile.Center, Vector2.Zero, BloodDeep, 0.08f)
                            ?.Configure(new Vector2(0.5f, 1f), dir > 0 ? 0f : MathHelper.Pi, 0.3f, 9);
                        //起步反冲的碎珠往身后甩
                        for (int i = 0; i < 6; i++) {
                            PRTLoader.NewParticle<PRT_KikasaBloodGlob>(
                                Projectile.Center + Main.rand.NextVector2Circular(20f, 16f),
                                new Vector2(-dir * Main.rand.NextFloat(2f, 4.5f), Main.rand.NextFloat(-1f, 1f)),
                                BloodTint * 0.5f, Main.rand.NextFloat(0.3f, 0.55f))
                                ?.Configure(Main.rand.Next(12, 20));
                        }
                    }
                    if (ViewedOwner) {
                        ShakeViewer(2.6f);
                    }
                }

                //横扫段：复利续力、纵向死锁——车道式冲锋直才快
                Projectile.velocity.X *= 1.012f;
                Projectile.velocity.X = MathHelper.Clamp(Projectile.velocity.X, -36f, 36f);
                Projectile.velocity.Y = 0f;
                Projectile.rotation = 0f;

                //翅下洗流：车道低掠湖面时压出连串涟漪，贴水则犁出水花
                float dy = domain.LakeWorldY - Projectile.Center.Y;
                if (ViewedOwner && dy > 0f && dy < 300f) {
                    if (dy < 46f) {
                        if (t % 3 == 0) {
                            Vector2 hit = new(Projectile.Center.X, domain.LakeWorldY);
                            KikasaDomainDeco.RippleAt(hit, 0.7f);
                            KikasaDomainDeco.SplashAt(hit, 3);
                        }
                    }
                    else if (t % 4 == 1) {
                        KikasaDomainDeco.RippleAt(new Vector2(Projectile.Center.X, domain.LakeWorldY),
                            MathHelper.Lerp(0.5f, 0.25f, dy / 300f));
                    }
                }
                //沿途甩出速度拉伸的血水
                if (!Main.dedServ && Main.rand.NextBool(2)) {
                    PRTLoader.NewParticle<PRT_KikasaBloodGlob>(
                        Projectile.Center - Projectile.velocity * 0.4f + Main.rand.NextVector2Circular(22f, 14f),
                        -Projectile.velocity * 0.1f + Main.rand.NextVector2Circular(0.7f, 0.7f),
                        BloodTint * 0.55f, Main.rand.NextFloat(0.35f, 0.6f))
                        ?.Configure(Main.rand.Next(10, 18), 0f);
                }

                //收线：贯穿到目标另一侧屏外，或超时（目标没了只扫个短程）；
                //贴近世界边则提前刹——原版会把出界弹幕直接杀掉
                float edgeMargin = 720f;
                bool nearEdge = Projectile.Center.X < edgeMargin
                    || Projectile.Center.X > Main.maxTilesX * 16f - edgeMargin;
                bool beyond = target >= 0
                    && (Projectile.Center.X - targetPos.X) * MathF.Sign(Projectile.velocity.X) > SweepEndMargin;
                bool expire = t >= SweepMax || (target < 0 && t >= 30);
                if (beyond || expire || nearEdge) {
                    NextPhase(pass, RakeBrake);
                }
                return;
            }

            //硬刹：×0.66 急停读出分量，甩出去的水珠是惯性的答话
            Projectile.velocity.X *= t <= 5 ? 0.66f : 0.9f;
            Projectile.velocity.Y *= 0.9f;
            if (lastBrakeFlung < pass) {
                lastBrakeFlung = pass;
                if (!Main.dedServ) {
                    for (int i = 0; i < 7; i++) {
                        PRTLoader.NewParticle<PRT_KikasaBloodGlob>(
                            Projectile.Center + Main.rand.NextVector2Circular(24f, 18f),
                            Projectile.velocity * 0.32f + Main.rand.NextVector2Circular(2f, 2f),
                            BloodTint * 0.5f, Main.rand.NextFloat(0.35f, 0.6f))
                            ?.Configure(Main.rand.Next(12, 22), Main.rand.NextFloat(-0.4f, 0.4f));
                    }
                }
                if (ViewedOwner) {
                    ShakeViewer(1.2f);
                }
            }
            if (t >= BrakeFrames) {
                if (pass + 1 >= RakePassCount) {
                    EndAttack(authority, 110);
                }
                else {
                    //下一趟折返：人已在这一侧屏外，直接进 tell
                    NextPhase(pass + 1, RakeTell);
                }
            }
        }

        //==================== 血蜂群 ====================

        private void UpdateBees(Player owner, bool authority) {
            int t = (int)StateTimer;
            int target = FindTarget(owner);

            if (target < 0 && t <= BeesChargeEnd) {
                EndAttack(authority, 45);
                return;
            }
            Vector2 aimPos = target >= 0 ? Main.npc[target].Center : Projectile.Center + Vector2.UnitX * faceDir * 300f;
            FaceX(aimPos.X);

            if (t <= BeesBrakeEnd) {
                Projectile.velocity *= 0.82f;
                return;
            }

            if (t <= BeesChargeEnd) {
                //腹部鼓动蓄力：身体微仰、血珠向腹囊收拢，72% 静默截断
                float charge = (t - BeesBrakeEnd) / (float)(BeesChargeEnd - BeesBrakeEnd);
                Projectile.velocity *= 0.9f;
                Projectile.rotation = Projectile.rotation.AngleLerp(-faceDir * 0.12f, 0.2f);
                if (t == BeesBrakeEnd + 2) {
                    SoundEngine.PlaySound(SoundID.Item97 with { Volume = 0.32f, Pitch = -0.5f, MaxInstances = 2 }, Projectile.Center);
                }
                if (!Main.dedServ && charge < 0.72f && t % 2 == 0) {
                    Vector2 belly = BellyPos();
                    Vector2 from = belly + Main.rand.NextVector2Unit() * Main.rand.NextFloat(40f, 96f);
                    PRTLoader.NewParticle<PRT_GhostRainDrop>(from,
                        (belly - from) * 0.15f,
                        Color.Lerp(BloodTint, HoneyGlow, 0.35f) * (0.35f + charge * 0.3f),
                        Main.rand.NextFloat(0.3f, 0.5f))?.Configure(9, 0f);
                }
                return;
            }

            if (t <= BeesFireEnd) {
                //三窝连泼：每窝一撮血蜂从腹部泼出
                int burst = (t - BeesChargeEnd) / BeeBurstGap;
                if ((t - BeesChargeEnd) % BeeBurstGap == 0 && burst < BeeBurstCount
                    && lastBeeBurst < burst) {
                    lastBeeBurst = burst;
                    StateParam = burst + 1;
                    SpewBees(owner, aimPos, authority);
                }
                Projectile.velocity *= 0.9f;
                return;
            }

            if (t >= BeesRecoverEnd) {
                EndAttack(authority, 90);
            }
            else {
                Projectile.velocity *= 0.92f;
                Projectile.rotation = Projectile.rotation.AngleLerp(0f, 0.15f);
            }
        }

        /// <summary>泼一窝血蜂：腹口湿爆 + 后坐上仰；弹体只在 owner 端生成，预算封顶</summary>
        private void SpewBees(Player owner, Vector2 aimPos, bool authority) {
            Vector2 belly = BellyPos();
            Vector2 aim = (aimPos - belly).SafeNormalize(Vector2.UnitX * faceDir);

            //泼洒后坐：腹部向反方向顶一下
            Projectile.velocity -= aim * 3.2f;

            SoundEngine.PlaySound(SoundID.Item95 with { Volume = 0.4f, Pitch = -0.2f, MaxInstances = 3 }, belly);
            SoundEngine.PlaySound(SoundID.Item97 with { Volume = 0.5f, Pitch = 0.1f, MaxInstances = 3 }, belly);
            if (!Main.dedServ) {
                for (int i = 0; i < 5; i++) {
                    PRTLoader.NewParticle<PRT_KikasaBloodGlob>(belly + Main.rand.NextVector2Circular(4f, 4f),
                        aim.RotatedByRandom(0.4f) * Main.rand.NextFloat(2.5f, 6f),
                        Main.rand.NextBool(3) ? BloodDeep : BloodTint,
                        Main.rand.NextFloat(0.35f, 0.6f))?.Configure(Main.rand.Next(12, 22));
                }
                PRTLoader.NewParticle<PRT_GhostRainMist>(belly, aim * 0.6f,
                    MistBlood * 0.7f, Main.rand.NextFloat(0.4f, 0.6f))
                    ?.Configure(Main.rand.Next(30, 50));
            }
            if (ViewedOwner) {
                ShakeViewer(0.8f);
            }

            if (!authority) {
                return;
            }
            //数量预算封顶：场上自有血蜂到顶就不再添
            int alive = 0;
            int beeType = ModContent.ProjectileType<KikasaQueenBeeBloodBee>();
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile proj = Main.projectile[i];
                if (proj?.active == true && proj.type == beeType && proj.owner == Projectile.owner) {
                    alive++;
                }
            }
            int damage = (int)owner.GetTotalDamage(DamageClass.Summon).ApplyTo(SwarmDamage);
            for (int k = 0; k < BeesPerBurst && alive < BeeBudgetCap; k++, alive++) {
                //扇形泼出，速度参差——一窝蜂不是一排枪
                Vector2 vel = aim.RotatedBy((k - 1) * 0.34f + Main.rand.NextFloat(-0.12f, 0.12f))
                    * Main.rand.NextFloat(7.5f, 10f);
                Projectile.NewProjectile(Projectile.GetSource_FromAI(), belly, vel,
                    ModContent.ProjectileType<KikasaQueenBeeBloodBee>(), damage, 2f, Projectile.owner);
            }
        }

        //==================== 螫针上抛 → 重力雨 ====================

        private void UpdateStingers(Player owner, bool authority) {
            int t = (int)StateTimer;
            int target = FindTarget(owner);

            if (target < 0 && t <= StingChargeEnd) {
                EndAttack(authority, 45);
                return;
            }
            Vector2 aimPos = target >= 0 ? Main.npc[target].Center : Projectile.Center + Vector2.UnitX * faceDir * 300f;
            FaceX(aimPos.X);

            if (t <= StingBrakeEnd) {
                Projectile.velocity *= 0.82f;
                Projectile.rotation = Projectile.rotation.AngleLerp(0f, 0.2f);
                return;
            }

            if (t <= StingChargeEnd) {
                //尾部蓄力：身体后仰卷尾，血珠向尾针收拢，72% 静默
                float charge = (t - StingBrakeEnd) / (float)(StingChargeEnd - StingBrakeEnd);
                Projectile.velocity *= 0.9f;
                Projectile.velocity.Y = MathHelper.Lerp(Projectile.velocity.Y, 1.2f * charge, 0.2f);
                Projectile.rotation = Projectile.rotation.AngleLerp(-faceDir * 0.3f * charge, 0.22f);
                if (t == StingBrakeEnd + 2) {
                    SoundEngine.PlaySound(SoundID.Item17 with { Volume = 0.35f, Pitch = -0.55f, MaxInstances = 2 }, Projectile.Center);
                }
                if (!Main.dedServ && charge < 0.72f && t % 2 == 1) {
                    Vector2 tail = TailPos();
                    Vector2 from = tail + Main.rand.NextVector2Unit() * Main.rand.NextFloat(36f, 88f);
                    PRTLoader.NewParticle<PRT_GhostRainDrop>(from,
                        (tail - from) * 0.16f,
                        BloodTint * (0.35f + charge * 0.3f), Main.rand.NextFloat(0.3f, 0.5f))
                        ?.Configure(8, 0f);
                }
                return;
            }

            if (!tossDone && t > StingChargeEnd) {
                //上抛一拍：扇排螫针冲天，身体重重下坐——知重量者先沉腰
                tossDone = true;
                TossStingers(owner, aimPos, authority);
            }

            if (t <= StingWatchEnd) {
                //目送：悬停微稳，螫针雨在头顶完成它的抛物线
                Projectile.velocity *= 0.92f;
                Projectile.rotation = Projectile.rotation.AngleLerp(0f, 0.12f);
                return;
            }

            if (t >= StingRecoverEnd) {
                EndAttack(authority, 100);
            }
            else {
                Projectile.velocity *= 0.94f;
            }
        }

        /// <summary>上抛扇排螫针：一帧全出 + 下坐后坐 + 上冲水袖；弹体只在 owner 端生成</summary>
        private void TossStingers(Player owner, Vector2 aimPos, bool authority) {
            Vector2 tail = TailPos();
            //后坐：整个身子往下一沉
            Projectile.velocity.Y += 4.2f;
            Projectile.rotation = 0f;

            SoundEngine.PlaySound(SoundID.Item17 with { Volume = 0.6f, Pitch = -0.15f, MaxInstances = 2 }, Projectile.Center);
            SoundEngine.PlaySound(SoundID.Zombie125 with { Volume = 0.4f, Pitch = 0.35f, MaxInstances = 2 }, Projectile.Center);
            if (!Main.dedServ) {
                //上冲的碎珠水袖与一圈上抛环
                for (int i = 0; i < 9; i++) {
                    PRTLoader.NewParticle<PRT_KikasaBloodGlob>(tail + Main.rand.NextVector2Circular(6f, 6f),
                        new Vector2(Main.rand.NextFloat(-2f, 2f), -Main.rand.NextFloat(4f, 9f)),
                        Main.rand.NextBool(3) ? BloodDeep : BloodTint,
                        Main.rand.NextFloat(0.35f, 0.6f))?.Configure(Main.rand.Next(16, 28));
                }
                PRTLoader.NewParticle<PRT_DWave>(tail, Vector2.Zero, BloodDeep, 0.07f)
                    ?.Configure(new Vector2(0.6f, 1f), -MathHelper.PiOver2, 0.26f, 9);
            }
            if (ViewedOwner) {
                ShakeViewer(2f);
            }

            if (!authority) {
                return;
            }
            int damage = (int)owner.GetTotalDamage(DamageClass.Summon).ApplyTo(SwarmDamage);
            //扇形上抛：横向带一点朝目标的整体偏置，雨幕落在猎物头顶
            float bias = MathHelper.Clamp((aimPos.X - Projectile.Center.X) * 0.012f, -4f, 4f);
            for (int k = 0; k < StingerFanCount; k++) {
                float spread = MathHelper.Lerp(-0.55f, 0.55f, k / (float)(StingerFanCount - 1));
                Vector2 vel = (-MathHelper.PiOver2 + spread).ToRotationVector2()
                    * Main.rand.NextFloat(14.5f, 16.5f);
                vel.X += bias + Main.rand.NextFloat(-0.6f, 0.6f);
                Projectile.NewProjectile(Projectile.GetSource_FromAI(), tail, vel,
                    ModContent.ProjectileType<KikasaQueenBeeStinger>(), damage, 2f, Projectile.owner);
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
                //翅膀先松劲，身体坠回湖里
                Projectile.velocity.X *= 0.92f;
                Projectile.velocity.Y = MathF.Min(Projectile.velocity.Y + 0.26f, 8.5f);
            }
            else {
                //湖已不在：原地化水
                Projectile.velocity *= 0.9f;
            }
            Projectile.rotation = Projectile.rotation.AngleLerp(0f, 0.1f);

            //过水线拍（一次）
            if (lakeAlive && !dissolveSplashed && Projectile.Center.Y >= lakeY) {
                dissolveSplashed = true;
                StateParam = 1f;
                SoundEngine.PlaySound(SoundID.SplashWeak with { Volume = 0.7f, Pitch = -0.3f, MaxInstances = 2 }, Projectile.Center);
                if (ViewedOwner) {
                    Vector2 hit = new(Projectile.Center.X, lakeY);
                    KikasaDomainDeco.SplashAt(hit, 10);
                    KikasaDomainDeco.RippleAt(hit, 1.3f);
                    ShakeViewer(2f);
                }
            }

            //边沉边化成血珠，蜂翅的位置先散
            if (!Main.dedServ && t % 2 == 0 && CurrentAlpha() > 0.15f) {
                Vector2 from = t < 16
                    ? Projectile.Center + new Vector2(Main.rand.NextFloat(-34f, 34f), -Main.rand.NextFloat(8f, 26f))
                    : Projectile.Center + Main.rand.NextVector2Circular(32f, 26f);
                PRTLoader.NewParticle<PRT_KikasaBloodGlob>(from,
                    new Vector2(Main.rand.NextFloat(-0.6f, 0.6f), Main.rand.NextFloat(1.4f, 3f)),
                    BloodTint * 0.5f, Main.rand.NextFloat(0.35f, 0.6f))
                    ?.Configure(Main.rand.Next(12, 22));
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

        /// <summary>横向朝向：死区防抖，贴图原生面左</summary>
        private void FaceX(float worldX) {
            float dx = worldX - Projectile.Center.X;
            if (MathF.Abs(dx) > 16f) {
                faceDir = dx > 0f ? 1 : -1;
            }
        }

        /// <summary>腹囊位置（蜜与蜂的出口）：体下偏后</summary>
        private Vector2 BellyPos()
            => Projectile.Center + new Vector2(-faceDir * 10f, 28f);

        /// <summary>尾针位置（螫针的出口）：体后下方</summary>
        private Vector2 TailPos()
            => Projectile.Center + new Vector2(-faceDir * 26f, 22f);

        /// <summary>振翅烈度 0~1：驱动帧速、翅尖血雾与叠帧残影浓度</summary>
        private float WingIntensity() {
            int t = (int)StateTimer;
            float baseLine = State switch {
                StateEmerge => t < OmenFrames ? 0f : MathHelper.Clamp((t - OmenFrames) / (float)(RiseEnd - OmenFrames), 0.2f, 0.8f),
                StateRake => RakePhase switch {
                    RakeTell => 0.55f + 0.45f * MathHelper.Clamp(t / (float)(RakePass == 0 ? TellFirst : TellNext), 0f, 1f),
                    RakeSweep => 1f,
                    RakeBrake => 0.7f,
                    _ => 0.75f,
                },
                StateBees => t > BeesBrakeEnd && t <= BeesFireEnd ? 0.85f : 0.5f,
                StateStingers => t > StingBrakeEnd && t <= StingChargeEnd + 6 ? 0.8f : 0.5f,
                StateDissolve => MathF.Max(0f, 0.5f - t / 15f * 0.5f),
                _ => 0.45f,
            };
            if (wingFlashTimer > 0) {
                baseLine = MathF.Max(baseLine, 0.9f);
            }
            return baseLine;
        }

        private void UpdateFrames() {
            //冲刺姿态用 0~3 帧（原版突进帧），其余振翅 4~11 帧；振翅越烈翻页越快
            bool dashPose = State == StateRake && RakePhase is RakeSweep or RakeBrake;
            int tickCap = dashPose ? 4 : (int)MathHelper.Lerp(7f, 3f, WingIntensity());
            if (++frameTick >= tickCap) {
                frameTick = 0;
                frameIndex++;
            }
            frameIndex %= dashPose ? 4 : 8;
        }

        /// <summary>翅尖持续甩出细小血雾：烈度即预算</summary>
        private void UpdateWingMist() {
            if (Main.dedServ || CurrentAlpha() < 0.3f) {
                return;
            }
            float intensity = WingIntensity();
            if (intensity < 0.3f || !Main.rand.NextBool(intensity > 0.8f ? 2 : 4)) {
                return;
            }
            float side = Main.rand.NextBool() ? 1f : -1f;
            Vector2 tip = Projectile.Center + new Vector2(side * Main.rand.NextFloat(24f, 36f), -Main.rand.NextFloat(14f, 28f));
            PRTLoader.NewParticle<PRT_GhostRainDrop>(tip,
                new Vector2(side * Main.rand.NextFloat(0.4f, 1.1f) + Projectile.velocity.X * 0.08f,
                    -Main.rand.NextFloat(0.1f, 0.6f)),
                BloodTint * (0.28f + intensity * 0.2f), Main.rand.NextFloat(0.22f, 0.4f))
                ?.Configure(Main.rand.Next(8, 16), 0f);
        }

        private bool ViewedOwner
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

        /// <summary>uForm：1=全血水 0=真身；常态半沉呼吸，出水自上而下凝实</summary>
        private float CurrentForm() {
            int t = (int)StateTimer;
            float steady = 0.34f + MathF.Sin(Main.GlobalTimeWrappedHourly * 3.3f + Seed) * 0.05f;
            return State switch {
                StateEmerge => t < OmenFrames
                    ? 1f
                    : MathHelper.Lerp(1f, steady, SmoothStep01(MathHelper.Clamp((t - OmenFrames) / (float)(RiseEnd - OmenFrames), 0f, 1f))),
                StateDissolve => MathHelper.Clamp(steady + t / (float)DissolveFrames * 0.3f, 0f, 1f),
                _ => steady,
            };
        }

        /// <summary>uScanMode：出水期自上而下扫描凝实，落定后渐回噪声半沉态</summary>
        private float CurrentScanMode() {
            if (State != StateEmerge) {
                return 0f;
            }
            int t = (int)StateTimer;
            if (t <= RiseEnd) {
                return 1f;
            }
            return 1f - MathHelper.Clamp((t - RiseEnd) / (float)(ScanSettleEnd - RiseEnd), 0f, 1f);
        }

        private float CurrentDissolve()
            => State == StateDissolve
                ? MathF.Pow(MathHelper.Clamp(StateTimer / 38f, 0f, 1f), 0.9f)
                : 0f;

        private float BodyScale() {
            float scale = 0.95f;
            int t = (int)StateTimer;
            if (State == StateEmerge && t >= OmenFrames && t < OmenFrames + 10) {
                //破水过冲
                scale *= 1f + 0.08f * (1f - (t - OmenFrames) / 10f);
            }
            else if (State == StateEmerge && wingShakeDone && t < WingShakeFrame + 8) {
                //抖翅小弹
                scale *= 1f + 0.05f * (1f - (t - WingShakeFrame) / 8f);
            }
            else if (State == StateBees && t > BeesBrakeEnd && t <= BeesChargeEnd) {
                //腹囊鼓胀
                float charge = (t - BeesBrakeEnd) / (float)(BeesChargeEnd - BeesBrakeEnd);
                scale *= 1f + 0.1f * charge;
            }
            else if (State == StateStingers && t > StingBrakeEnd && t <= StingChargeEnd) {
                float charge = (t - StingBrakeEnd) / (float)(StingChargeEnd - StingBrakeEnd);
                scale *= 1f + 0.07f * charge;
            }
            return scale;
        }

        /// <summary>血蜂蓄力进度 0~1，腹囊蜜光共用</summary>
        private float BeeCharge() {
            if (State != StateBees) {
                return 0f;
            }
            int t = (int)StateTimer;
            if (t <= BeesBrakeEnd || t > BeesFireEnd) {
                return 0f;
            }
            if (t <= BeesChargeEnd) {
                return (t - BeesBrakeEnd) / (float)(BeesChargeEnd - BeesBrakeEnd);
            }
            //泼洒窗维持余温
            return 0.6f;
        }

        /// <summary>螫针蓄力进度 0~1，尾针积光共用</summary>
        private float StingCharge() {
            if (State != StateStingers) {
                return 0f;
            }
            int t = (int)StateTimer;
            if (t <= StingBrakeEnd || t > StingChargeEnd + 6) {
                return 0f;
            }
            if (t <= StingChargeEnd) {
                return (t - StingBrakeEnd) / (float)(StingChargeEnd - StingBrakeEnd);
            }
            return 0.5f;
        }

        private static float SmoothStep01(float t) => t * t * (3f - 2f * t);

        //==================== 绘制 ====================

        public override bool PreDraw(ref Color lightColor) {
            Main.instance.LoadNPC(NPCID.QueenBee);
            Texture2D tex = TextureAssets.Npc[NPCID.QueenBee]?.Value;
            if (tex == null) {
                return false;
            }
            int frameH = tex.Height / Main.npcFrameCount[NPCID.QueenBee];
            bool dashPose = State == StateRake && RakePhase is RakeSweep or RakeBrake;
            int sheetRow = dashPose ? frameIndex % 4 : 4 + frameIndex % 8;
            Rectangle frame = new(0, frameH * sheetRow, tex.Width, frameH);

            float alpha = CurrentAlpha();
            SpriteBatch sb = Main.spriteBatch;
            SpriteEffects flip = faceDir == 1 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;

            //冲刺残影：只在高速时亮，速度门控免得常开成噪声
            float speed = Projectile.velocity.Length();
            if (alpha > 0.1f && speed > 15f) {
                Vector2 origin = frame.Size() * 0.5f;
                for (int k = Projectile.oldPos.Length - 1; k >= 1; k--) {
                    Vector2 oldCenter = Projectile.oldPos[k] + Projectile.Size * 0.5f;
                    if (oldCenter == Projectile.Size * 0.5f) {
                        continue;
                    }
                    float fall = 1f - k / (float)Projectile.oldPos.Length;
                    sb.Draw(tex, oldCenter - Main.screenPosition, frame,
                        BloodTint * (0.3f * fall * alpha), Projectile.oldRot[k],
                        origin, BodyScale() * (0.96f - k * 0.014f), flip, 0f);
                }
            }

            //本体：血湖材质（含振翅叠帧与蓄力颤抖）
            if (alpha > 0.01f) {
                DrawBody(sb, tex, frame, frameH, alpha, flip, dashPose);
            }

            //加色层：预兆水下血光 / 车道预告 / 腹囊蜜光 / 尾针积光
            DrawGlow(sb, alpha);

            return false;
        }

        private void DrawBody(SpriteBatch sb, Texture2D tex, Rectangle frame, int frameH,
            float alpha, SpriteEffects flip, bool dashPose) {
            Effect form = EffectLoader.KikasaItemForm?.Value;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            bool shaderOk = form != null && noise != null;

            sb.End();
            sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.PointClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

            Vector2 origin = frame.Size() * 0.5f;
            Vector2 pos = Projectile.Center - Main.screenPosition;
            float wing = WingIntensity();

            void ApplyShader(Rectangle rect) {
                Main.instance.GraphicsDevice.Textures[1] = noise;
                Main.instance.GraphicsDevice.SamplerStates[1] = SamplerState.LinearWrap;
                form.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
                form.Parameters["uSeed"]?.SetValue(Seed);
                form.Parameters["uForm"]?.SetValue(CurrentForm());
                form.Parameters["uDissolve"]?.SetValue(CurrentDissolve());
                form.Parameters["uScanMode"]?.SetValue(CurrentScanMode());
                form.Parameters["uUvRect"]?.SetValue(new Vector4(
                    rect.X / (float)tex.Width, rect.Y / (float)tex.Height,
                    rect.Width / (float)tex.Width, rect.Height / (float)tex.Height));
                form.Parameters["uTexel"]?.SetValue(new Vector2(1f / tex.Width, 1f / tex.Height));
                form.Parameters["uAspect"]?.SetValue(rect.Width / (float)rect.Height);
                form.CurrentTechnique.Passes[0].Apply();
            }

            Color BodyColor(float a) => shaderOk
                ? new Color(255, 255, 255, (byte)(a * 255f))
                : Color.Lerp(Color.White, BloodTint, 0.55f) * a;

            //振翅叠帧：下一帧半透明叠画——翅膀高频颤动的双重曝光（悬停姿态专属）
            if (!dashPose && wing > 0.3f && alpha > 0.2f) {
                int nextRow = 4 + (frameIndex + 1) % 8;
                Rectangle nextFrame = new(0, frameH * nextRow, tex.Width, frameH);
                if (shaderOk) {
                    ApplyShader(nextFrame);
                }
                sb.Draw(tex, pos, nextFrame, BodyColor(alpha * (0.22f + wing * 0.2f)),
                    Projectile.rotation, origin, BodyScale(), flip, 0f);
            }

            //蓄力颤抖：折返 tell 末段横向高频抖影
            float tremble = State == StateRake && RakePhase == RakeTell
                ? MathHelper.Clamp(StateTimer / (float)(RakePass == 0 ? TellFirst : TellNext), 0f, 1f)
                : 0f;
            if (tremble > 0.4f && alpha > 0.2f) {
                float shakeX = MathF.Sin(StateTimer * 1.35f + Seed) * 3f * tremble;
                if (shaderOk) {
                    ApplyShader(frame);
                }
                sb.Draw(tex, pos + new Vector2(shakeX, 0f), frame, BodyColor(alpha * 0.24f),
                    Projectile.rotation, origin, BodyScale(), flip, 0f);
            }

            //主帧
            if (shaderOk) {
                ApplyShader(frame);
            }
            sb.Draw(tex, pos, frame, BodyColor(alpha),
                Projectile.rotation, origin, BodyScale(), flip, 0f);

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

            //预兆：水下血光自深处上浮，蜂巢的蜜色掺一线
            if (State == StateEmerge && t < OmenFrames) {
                float ot = MathHelper.Clamp(t / (float)OmenFrames, 0f, 1f);
                float ease = 1f - (1f - ot) * (1f - ot);
                Vector2 pos = new(Projectile.Center.X, domain.LakeWorldY + MathHelper.Lerp(48f, 8f, ease));
                float r = 32f + 24f * ease;
                EnsureBegin();
                sb.Draw(glow, pos - Main.screenPosition, null,
                    Color.Lerp(FoamGlow, HoneyGlow, 0.3f) * (0.4f * ease), 0f,
                    gOrigin, new Vector2(r * 2.8f / glow.Width, r * 1.05f / glow.Height), SpriteEffects.None, 0f);
            }

            //抖翅觉醒：翅线一闪
            if (State == StateEmerge && wingFlashTimer > 0) {
                EnsureBegin();
                float f = wingFlashTimer / 10f;
                sb.Draw(glow, Projectile.Center + new Vector2(0f, -18f) - Main.screenPosition, null,
                    FoamGlow * (0.5f * f), 0f, gOrigin,
                    new Vector2(66f * 2f / glow.Width, 14f * 2f / glow.Height), SpriteEffects.None, 0f);
            }

            //折返 tell：转身完成后沿冲锋方向铺一条渐亮的低幅光带——车道预告（可读性阀门）
            if (State == StateRake && RakePhase == RakeTell && alpha > 0.3f) {
                float tellP = MathHelper.Clamp(t / (float)(RakePass == 0 ? TellFirst : TellNext), 0f, 1f);
                if (tellP > 0.42f) {
                    EnsureBegin();
                    float pulse = 0.7f + 0.3f * MathF.Sin(Main.GlobalTimeWrappedHourly * 22f + Seed);
                    float laneA = (tellP - 0.42f) / 0.58f * 0.16f * pulse;
                    Vector2 lanePos = Projectile.Center + new Vector2(faceDir * 430f, 0f);
                    sb.Draw(glow, lanePos - Main.screenPosition, null, BloodTint * laneA, 0f,
                        gOrigin, new Vector2(780f * 2f / glow.Width, 22f * 2f / glow.Height), SpriteEffects.None, 0f);
                    //身位小灼点
                    sb.Draw(glow, Projectile.Center - Main.screenPosition, null,
                        FoamGlow * (0.3f * tellP), 0f, gOrigin,
                        new Vector2(52f * 2f / glow.Width, 52f * 2f / glow.Height), SpriteEffects.None, 0f);
                }
            }

            //腹囊蜜光：血蜂蓄力时鼓亮
            float beeCharge = BeeCharge();
            if (beeCharge > 0.03f && alpha > 0.1f) {
                EnsureBegin();
                Vector2 belly = BellyPos();
                float r = 10f + 20f * beeCharge;
                sb.Draw(glow, belly - Main.screenPosition, null, HoneyGlow * (0.5f * beeCharge), 0f,
                    gOrigin, new Vector2(r * 2f / glow.Width), SpriteEffects.None, 0f);
            }

            //尾针积光：螫针蓄力时尖锐的小亮点
            float stingCharge = StingCharge();
            if (stingCharge > 0.03f && alpha > 0.1f) {
                EnsureBegin();
                Vector2 tail = TailPos();
                float r = 7f + 12f * stingCharge;
                sb.Draw(glow, tail - Main.screenPosition, null, BloodTint * (0.55f * stingCharge), 0f,
                    gOrigin, new Vector2(r * 2.4f / glow.Width, r * 1.2f / glow.Height), SpriteEffects.None, 0f);
            }

            if (begun) {
                sb.End();
                sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                    DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
            }
        }

        //==================== 命中与谢幕 ====================

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            //耙扫撞击的溅血（OnHit 只在 owner 端跑，队友看拖尾即可）
            if (Main.dedServ) {
                return;
            }
            for (int i = 0; i < 8; i++) {
                PRTLoader.NewParticle<PRT_KikasaBloodGlob>(
                    target.Center + Main.rand.NextVector2Circular(20f, 20f),
                    Projectile.velocity * 0.24f + Main.rand.NextVector2Circular(2.4f, 2.4f),
                    BloodTint * 0.6f, Main.rand.NextFloat(0.4f, 0.7f))
                    ?.Configure(Main.rand.Next(14, 24), Main.rand.NextFloat(-0.5f, 0.5f));
            }
            SoundEngine.PlaySound(SoundID.NPCHit1 with { Volume = 0.6f, Pitch = -0.25f, MaxInstances = 3 }, target.Center);
        }

        public override void OnKill(int timeLeft) {
            //谢幕残珠：溶解尾拍或异常移除都留一口血水，散出去的几粒读作残蜂
            if (Main.dedServ) {
                return;
            }
            for (int i = 0; i < 10; i++) {
                PRTLoader.NewParticle<PRT_KikasaBloodGlob>(
                    Projectile.Center + Main.rand.NextVector2Circular(28f, 24f),
                    new Vector2(Main.rand.NextFloat(-1.2f, 1.2f), Main.rand.NextFloat(0.5f, 2.6f)),
                    BloodTint * 0.5f, Main.rand.NextFloat(0.35f, 0.6f))
                    ?.Configure(Main.rand.Next(14, 26));
            }
            for (int i = 0; i < 3; i++) {
                PRTLoader.NewParticle<PRT_KikasaBloodGlob>(
                    Projectile.Center + Main.rand.NextVector2Circular(20f, 14f),
                    new Vector2(Main.rand.NextFloat(-3f, 3f), -Main.rand.NextFloat(0.5f, 1.8f)),
                    Color.Lerp(BloodTint, HoneyGlow, 0.4f) * 0.55f,
                    Main.rand.NextFloat(0.25f, 0.4f))?.Configure(Main.rand.Next(18, 30));
            }
            PRTLoader.NewParticle<PRT_GhostRainMist>(Projectile.Center,
                new Vector2(0f, -0.25f), MistBlood * 0.7f, Main.rand.NextFloat(0.6f, 0.9f))
                ?.Configure(Main.rand.Next(50, 80));
        }
    }
}
