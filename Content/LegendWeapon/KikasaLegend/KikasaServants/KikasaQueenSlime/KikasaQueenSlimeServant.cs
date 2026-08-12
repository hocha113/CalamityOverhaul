using CalamityOverhaul.Common;
using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaDomains;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaServants.KikasaQueenSlime
{
    /// <summary>
    /// 鬼奴·湖水版史莱姆皇后。血湖之水凝成的血晶凤辇，飞行优雅系——
    /// 与史莱姆王的差异是生死线：他跳，你飞。出水五拍（皇冠形涟漪预兆→
    /// 破水浪冠→披水帘垂直升空→加冕拍皇冠凝成→晶翼展开定格），
    /// 战斗循环为空中晶格雷布阵（弧线/环形交替）与加冕俯冲压轴砸交替；
    /// 溶解走"皇冠先失色脱落→晶翼失泽化水→身躯蚀溶沉湖"的层次谢幕。
    /// 联机契约同克眼基准：状态机走 ai[0..2]、owner 转场盖章纠偏、
    /// 节拍闩防快照回卷、生命线只有 owner 判、子弹幕只在 owner 端生成
    /// </summary>
    internal class KikasaQueenSlimeServant : ModProjectile, IKikasaServant
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        //==================== 可调基数（占位初值，验收再调）====================

        /// <summary>加冕俯冲接触基伤（召唤加成前）</summary>
        internal const int DiveDamage = 580;

        /// <summary>晶片弹基伤（召唤加成前），晶格雷碎裂与俯冲晶爆共用</summary>
        internal const int ShardDamage = 320;

        //==================== 血晶色板（血水基底随域冷化，晶面高光只做次要层）====================

        /// <summary>凝胶血主色：比克眼血更偏粉一分</summary>
        internal static Color GelBlood => KikasaDomain.CoolTint(new(233, 84, 92), new(128, 158, 164));
        /// <summary>晶体深色：偏洋红的深血</summary>
        internal static Color CrystalDeep => KikasaDomain.CoolTint(new(146, 36, 66), new(86, 106, 112));
        /// <summary>晶面高光：粉白锐反光</summary>
        internal static Color CrystalGlint => KikasaDomain.CoolTint(new(255, 178, 196), new(188, 208, 212));
        /// <summary>晶芯加色（A=0，加色批/加色画法用）</summary>
        internal static Color CrystalCore => KikasaDomain.CoolTint(new(255, 128, 156, 0), new(162, 192, 198, 0));
        private static Color MistBlood => KikasaDomain.CoolTint(new(58, 18, 20), new(52, 62, 66));

        //==================== 状态 ====================

        private const int StateEmerge = 0;
        private const int StateFollow = 1;
        private const int StateMines = 2;
        private const int StateDive = 3;
        private const int StateDissolve = 4;

        private int State { get => (int)Projectile.ai[0]; set => Projectile.ai[0] = value; }
        private ref float StateTimer => ref Projectile.ai[1];
        /// <summary>
        /// 状态内子参数。布雷=已掷数；俯冲编码为相位：0=升位 1=悬停亮冠
        /// ≥2=俯冲中（值即锁定的落点世界 Y）负=收势；溶解=过水线闩
        /// </summary>
        private ref float StateParam => ref Projectile.ai[2];

        //==================== 时序 ====================

        //出水：皇冠涟漪预兆→破水→披帘升空→加冕拍→晶翼展开→定格→首扇落定
        private const int OmenFrames = 30;
        private const int RiseEnd = 66;
        private const int CrownBeat = 66;
        private const int WingBeat = 76;
        private const int WingHoldEnd = 96;
        private const int EmergeTotal = 108;

        //布雷：驻停→五连掷（弧线/环形交替）→回摆
        private const int MineBrakeEnd = 14;
        private const int MineCount = 5;
        private const int MineGap = 9;
        private const int MineVolleyEnd = MineBrakeEnd + MineCount * MineGap;
        private const int MineRecoverEnd = MineVolleyEnd + 18;

        //俯冲：升至目标正上方→悬停定格亮冠 tell→垂直砸落→晶爆收势
        private const int DiveAscendMax = 56;
        private const int DiveHoverFrames = 26;
        private const int DivePlungeMax = 80;
        private const int DiveRecoverFrames = 44;
        private const float DiveHoverHeight = 360f;

        //溶解：皇冠先失色脱落→晶翼失泽化水→身躯蚀溶
        private const int DissolveFrames = 68;
        private const int DullFrames = 22;
        private const int CrownMeltEnd = 14;
        private const int WingMeltStart = 8;
        private const int WingMeltEnd = 30;
        private const int BodyErodeStart = 18;
        private const int BodyErodeEnd = 60;

        //==================== 本地表现量（不入同步，节拍闩防快照回卷重播）====================

        private int frameTick;
        private int frameIndex;
        private int wingCounter;
        private int faceDir = 1;
        private int attackCooldown;
        private int attackIndex;
        private int lastSeenState = -1;
        private bool breachDone;
        private bool crownFxDone;
        private bool wingFxDone;
        private bool flapFxDone;
        private int lastTossFired = -1;
        private int lastDivePhase = -1;
        private bool hoverChimeDone;
        private bool plungeSplashed;
        private bool dissolveSplashed;
        private bool crownMeltFxDone;
        /// <summary>悬停期缓存的落点参考（本地各算，owner 盖章的 ai[2] 为准）</summary>
        private Vector2 hoverAimPos;
        /// <summary>俯冲落点缓存：收势拍起爆用（转场后 ai[2] 已改写）</summary>
        private float lastImpactY;
        /// <summary>布雷阵位缓存（仅 owner 有意义，spawn 参数随包带全）</summary>
        private readonly Vector2[] mineTargets = new Vector2[MineCount];
        private bool mineTargetsReady;

        private Player Owner => Main.player[Projectile.owner];

        /// <summary>连续量抖动的确定性相位，各端一致（9.1：不掷 Main.rand）</summary>
        private float Seed => Projectile.identity * 0.7391f;

        //==================== 召唤入口 ====================

        /// <summary>KikasaServantIndex 登记的召唤委托；emergeAt.Y = 湖面</summary>
        internal static void Summon(Player owner, Vector2 emergeAt) {
            if (owner.whoAmI != Main.myPlayer) {
                return;
            }
            int damage = (int)owner.GetTotalDamage(DamageClass.Summon).ApplyTo(DiveDamage);
            Projectile.NewProjectile(owner.GetSource_Misc("KikasaServant"),
                emergeAt + new Vector2(0f, 56f), Vector2.Zero,
                ModContent.ProjectileType<KikasaQueenSlimeServant>(), damage, 7.5f, owner.whoAmI);
        }

        //==================== 定义 ====================

        public override void SetStaticDefaults() {
            Main.projPet[Projectile.type] = true;
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 8;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
            ProjectileID.Sets.MinionSacrificable[Projectile.type] = false;
            //皇冠与晶翼远超 hitbox，出屏也要画
            ProjectileID.Sets.DrawScreenCheckFluff[Projectile.type] = 800;
        }

        public override void SetDefaults() {
            Projectile.width = 104;
            Projectile.height = 96;
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

        /// <summary>接触伤害只开在俯冲下落窗，与可见的垂直砸落严格对齐；常态 false</summary>
        public override bool? CanDamage()
            => State == StateDive && StateParam >= 2f ? null : false;

        public override bool? CanCutTiles() => false;

        //==================== 遣返 ====================

        public bool IsDismissing => State == StateDissolve;

        public void BeginDismiss() {
            if (Main.myPlayer == Projectile.owner && State != StateDissolve) {
                BeginDissolve();
            }
        }

        private void BeginDissolve() {
            //还没破水（或刚破水淡入未满）就要收场：不走溶解演出——
            //否则透明度会从半途跳到 1，水面凭空闪出一位皇后再化掉
            if (State == StateEmerge && StateTimer < OmenFrames + 4) {
                Projectile.Kill();
                return;
            }
            //她的晶格雷失去供养，一并失泽化水
            OrderMinesMelt();
            State = StateDissolve;
            StateTimer = 0;
            StateParam = 0;
            Projectile.netUpdate = Main.myPlayer == Projectile.owner;
        }

        /// <summary>遣返/湖塌时让场上晶格雷全部失泽化水；只在 owner 端有效</summary>
        private void OrderMinesMelt() {
            if (Main.myPlayer != Projectile.owner) {
                return;
            }
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile proj = Main.projectile[i];
                if (proj?.active == true && proj.owner == Projectile.owner
                    && proj.ModProjectile is KikasaQueenGelMine mine) {
                    mine.OrderMelt();
                }
            }
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

            //生命线：湖塌/收域/退水/主人死亡 → 溶解回湖。只有 owner 裁决——
            //服务器没有领域状态（恒 Closed 是既定契约），别处判会当场误杀；
            //迟入场客户端首份快照前同样会误判。其余端只跟 owner 的同步包换场
            if (authority && State != StateDissolve && !LakeHealthy(owner, domain)) {
                BeginDissolve();
            }

            Projectile.timeLeft = 180;
            //伤害随召唤加成逐帧刷新，命中在 owner 端结算
            Projectile.damage = (int)owner.GetTotalDamage(DamageClass.Summon).ApplyTo(DiveDamage);

            //换场清闩：远端可能靠收包切状态而非本地同拍转场，
            //上一场残闩会吞掉新场的节拍（掷雷音、亮冠 tell、过水线拍）
            if (State != lastSeenState) {
                lastSeenState = State;
                lastTossFired = -1;
                lastDivePhase = -1;
                hoverChimeDone = false;
                plungeSplashed = false;
                mineTargetsReady = false;
                if (State == StateDissolve) {
                    dissolveSplashed = false;
                    crownMeltFxDone = false;
                }
            }

            StateTimer++;
            switch (State) {
                case StateEmerge: UpdateEmerge(owner, domain); break;
                case StateFollow: UpdateFollow(owner, domain, authority); break;
                case StateMines: UpdateMines(owner, authority); break;
                case StateDive: UpdateDive(owner, domain, authority); break;
                case StateDissolve: UpdateDissolve(domain, authority); break;
            }

            UpdateFrames();
            UpdateWings();
            if (attackCooldown > 0) {
                attackCooldown--;
            }

            float glow = CurrentAlpha() * 0.5f * LusterK();
            if (glow > 0.02f) {
                Lighting.AddLight(Projectile.Center, 0.44f * glow, 0.14f * glow, 0.2f * glow);
            }
        }

        private static bool LakeHealthy(Player owner, KikasaDomainPlayer domain)
            => !owner.dead && domain.AnyActive
            && domain.Phase != KikasaDomainPhase.Closing
            && domain.RiseT >= 0.9f;

        //==================== 出水：加冕升空 ====================

        private void UpdateEmerge(Player owner, KikasaDomainPlayer domain) {
            float lakeY = domain.LakeWorldY;
            int t = (int)StateTimer;
            bool viewed = ViewedOwner;
            faceDir = owner.Center.X >= Projectile.Center.X ? 1 : -1;

            if (t < OmenFrames) {
                //水下待命：湖面浮起皇冠形涟漪——中间高两侧低的五点冠齿
                Projectile.velocity = Vector2.Zero;
                if (viewed) {
                    if (t == 8 || t == 20) {
                        CrownRipples(new Vector2(Projectile.Center.X, lakeY), t == 20 ? 1.15f : 0.8f);
                    }
                    if (t == 6) {
                        SoundEngine.PlaySound(SoundID.Drip with { Volume = 0.45f, Pitch = -0.4f, MaxInstances = 2 },
                            new Vector2(Projectile.Center.X, lakeY));
                    }
                    if (t == 20) {
                        //水下闷响的晶铃，加冕的先声
                        SoundEngine.PlaySound(SoundID.Item29 with { Volume = 0.3f, Pitch = -0.35f, MaxInstances = 2 },
                            new Vector2(Projectile.Center.X, lakeY));
                    }
                }
                return;
            }

            if (!breachDone) {
                //破水拍：一帧起速 + 浪冠 + 轻吼与晶铃层叠
                breachDone = true;
                Projectile.velocity = new Vector2(0f, -12.5f);
                SoundEngine.PlaySound(SoundID.Roar with { Volume = 0.3f, Pitch = 0.25f, MaxInstances = 2 }, Projectile.Center);
                SoundEngine.PlaySound(SoundID.Item29 with { Volume = 0.5f, Pitch = 0f, MaxInstances = 2 }, Projectile.Center);
                if (viewed) {
                    BreachBurst(new Vector2(Projectile.Center.X, lakeY));
                }
            }

            //披着水帘垂直升空：起速后指数衰减，前快后慢，禁匀速
            Projectile.velocity.Y *= t < RiseEnd ? 0.95f : 0.8f;
            Projectile.velocity.X = 0f;
            Projectile.rotation = Projectile.rotation.AngleLerp(0f, 0.2f);

            if (viewed && t < RiseEnd) {
                //水帘：轮廓上密集垂落的血水，落点连环小涟漪
                for (int i = 0; i < 2; i++) {
                    Vector2 dropPos = Projectile.Center + new Vector2(
                        Main.rand.NextFloat(-46f, 46f), Main.rand.NextFloat(-26f, 42f));
                    PRTLoader.NewParticle<PRT_GhostRainDrop>(dropPos,
                        new Vector2(Main.rand.NextFloat(-0.3f, 0.3f), Main.rand.NextFloat(2.6f, 4.4f)),
                        GelBlood * Main.rand.NextFloat(0.4f, 0.58f),
                        Main.rand.NextFloat(0.45f, 0.7f))
                        ?.Configure(Main.rand.Next(16, 28), 0f);
                }
                if (t % 5 == 3) {
                    KikasaDomainDeco.RippleAt(
                        new Vector2(Projectile.Center.X + Main.rand.NextFloat(-24f, 24f), lakeY), 0.4f);
                }
            }

            if (!crownFxDone && t >= CrownBeat) {
                //加冕拍：皇冠自血水凝成，闪光 + 双层晶铃
                crownFxDone = true;
                SoundEngine.PlaySound(SoundID.Item29 with { Volume = 0.6f, Pitch = 0.15f, MaxInstances = 2 }, Projectile.Center);
                SoundEngine.PlaySound(SoundID.Item4 with { Volume = 0.3f, Pitch = 0.2f, MaxInstances = 2 }, Projectile.Center);
                if (viewed) {
                    Vector2 crown = CrownWorldPos();
                    for (int i = 0; i < 6; i++) {
                        Vector2 from = crown + Main.rand.NextVector2Unit() * Main.rand.NextFloat(40f, 80f);
                        PRTLoader.NewParticle<PRT_Sparkle>(from, (crown - from) * 0.14f,
                            CrystalGlint * 0.6f, Main.rand.NextFloat(0.24f, 0.4f))
                            ?.Configure(CrystalGlint * 0.5f, 12, 0f, 0.7f);
                    }
                    ShakeViewer(1.5f);
                }
            }

            if (!wingFxDone && t >= WingBeat) {
                //晶翼拍：血水沿身侧凝成晶翼（蚀入式凝实在绘制层）
                wingFxDone = true;
                SoundEngine.PlaySound(SoundID.Item27 with { Volume = 0.55f, Pitch = -0.15f, MaxInstances = 2 }, Projectile.Center);
                if (viewed) {
                    for (int side = -1; side <= 1; side += 2) {
                        PRTLoader.NewParticle<PRT_Sparkle>(
                            Projectile.Center + new Vector2(side * 52f, -8f), Vector2.Zero,
                            CrystalGlint * 0.5f, 0.34f)
                            ?.Configure(CrystalGlint * 0.45f, 12, 0f, 0.7f);
                    }
                }
            }

            if (!flapFxDone && t >= WingHoldEnd) {
                //定格结束的首扇：湿翼轻挥，甩下几滴
                flapFxDone = true;
                SoundEngine.PlaySound(SoundID.SplashWeak with { Volume = 0.25f, Pitch = 0.5f, MaxInstances = 2 }, Projectile.Center);
                if (viewed) {
                    for (int i = 0; i < 4; i++) {
                        int side = i % 2 == 0 ? 1 : -1;
                        PRTLoader.NewParticle<PRT_GhostRainDrop>(
                            Projectile.Center + new Vector2(side * Main.rand.NextFloat(40f, 62f), 0f),
                            new Vector2(side * Main.rand.NextFloat(0.4f, 1f), Main.rand.NextFloat(1.6f, 3f)),
                            GelBlood * 0.5f, Main.rand.NextFloat(0.4f, 0.6f))
                            ?.Configure(Main.rand.Next(16, 26), 0f);
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

        /// <summary>皇冠形涟漪：五点冠齿，中间高两侧低</summary>
        private static void CrownRipples(Vector2 center, float power) {
            ReadOnlySpan<float> offsets = [-46f, -24f, 0f, 24f, 46f];
            ReadOnlySpan<float> scales = [0.32f, 0.5f, 0.85f, 0.5f, 0.32f];
            for (int i = 0; i < offsets.Length; i++) {
                KikasaDomainDeco.RippleAt(center + new Vector2(offsets[i], 0f), scales[i] * power);
            }
        }

        /// <summary>破水浪冠：环涟漪 + 扇形血珠 + 垂直水柱 + 晶光点缀，端庄而不失量级</summary>
        private void BreachBurst(Vector2 hit) {
            KikasaDomainDeco.RippleAt(hit, 2.2f);
            KikasaDomainDeco.RippleAt(hit + new Vector2(40f, 0f), 1f);
            KikasaDomainDeco.RippleAt(hit - new Vector2(38f, 0f), 0.95f);
            KikasaDomainDeco.SplashAt(hit + new Vector2(-16f, 0f), 11);
            KikasaDomainDeco.SplashAt(hit + new Vector2(16f, 0f), 11);

            //浪冠血珠扇
            for (int i = 0; i < 20; i++) {
                float angle = -MathHelper.Pi * (0.14f + 0.72f * i / 19f);
                float speed = Main.rand.NextFloat(3f, 7f);
                PRTLoader.NewParticle<PRT_GhostRainDrop>(
                    hit + new Vector2(Main.rand.NextFloat(-28f, 28f), -4f),
                    angle.ToRotationVector2() * speed,
                    GelBlood * Main.rand.NextFloat(0.42f, 0.62f),
                    Main.rand.NextFloat(0.5f, 0.8f))
                    ?.Configure(Main.rand.Next(22, 36), Main.rand.NextFloat(-0.4f, 0.4f));
            }
            //垂直水柱束
            for (int i = 0; i < 9; i++) {
                PRTLoader.NewParticle<PRT_GhostRainDrop>(
                    hit + new Vector2(Main.rand.NextFloat(-8f, 8f), -6f),
                    new Vector2(Main.rand.NextFloat(-0.7f, 0.7f), -Main.rand.NextFloat(8f, 12.5f)),
                    GelBlood * Main.rand.NextFloat(0.5f, 0.68f),
                    Main.rand.NextFloat(0.55f, 0.9f))
                    ?.Configure(Main.rand.Next(32, 48), Main.rand.NextFloat(-0.3f, 0.3f));
            }
            //破水携出的细碎晶光
            for (int i = 0; i < 5; i++) {
                PRTLoader.NewParticle<PRT_Sparkle>(
                    hit + new Vector2(Main.rand.NextFloat(-24f, 24f), -Main.rand.NextFloat(4f, 30f)),
                    new Vector2(Main.rand.NextFloat(-0.5f, 0.5f), -Main.rand.NextFloat(1f, 2.6f)),
                    CrystalGlint * 0.55f, Main.rand.NextFloat(0.2f, 0.36f))
                    ?.Configure(CrystalGlint * 0.45f, 16, 0f, 0.7f);
            }
            for (int i = 0; i < 3; i++) {
                PRTLoader.NewParticle<PRT_GhostRainMist>(
                    hit + new Vector2(Main.rand.NextFloat(-30f, 30f), -10f),
                    new Vector2(Main.rand.NextFloat(-0.3f, 0.3f), -Main.rand.NextFloat(0.3f, 0.7f)),
                    MistBlood * 0.8f, Main.rand.NextFloat(0.7f, 1f))
                    ?.Configure(Main.rand.Next(60, 100));
            }

            SoundEngine.PlaySound(SoundID.SplashWeak with { Volume = 1f, Pitch = -0.35f, MaxInstances = 2 }, hit);
            SoundEngine.PlaySound(SoundID.DD2_MonkStaffGroundImpact with { Volume = 0.45f, Pitch = -0.7f, MaxInstances = 1 }, hit);
            ShakeViewer(5.5f);
        }

        //==================== 跟随：振翅悬浮 ====================

        private void UpdateFollow(Player owner, KikasaDomainPlayer domain, bool authority) {
            int target = FindTarget(owner);

            //悬在主人侧上方，呼吸浮动——端庄的滑翔，不做急转
            Vector2 anchor = owner.Center + new Vector2(-owner.direction * 112f, -148f);
            anchor.Y += MathF.Sin(Main.GlobalTimeWrappedHourly * 1.9f + Seed) * 7f;
            anchor.X += MathF.Sin(Main.GlobalTimeWrappedHourly * 1.2f + Seed * 2f) * 5f;

            Vector2 to = anchor - Projectile.Center;
            if (to.Length() > 2400f) {
                //跟丢硬贴回，别在半个地图外滴血
                Projectile.Center = anchor;
                Projectile.velocity = Vector2.Zero;
                Projectile.netUpdate = authority;
                return;
            }
            Vector2 desired = to * 0.085f;
            const float maxSpeed = 14f;
            if (desired.Length() > maxSpeed) {
                desired = desired.SafeNormalize(Vector2.Zero) * maxSpeed;
            }
            Projectile.velocity = Vector2.Lerp(Projectile.velocity, desired, 0.13f);

            //端庄的侧倾：比原版更收敛
            float bank = MathHelper.Clamp(Projectile.velocity.X * 0.05f, -0.3f, 0.3f);
            Projectile.rotation = Projectile.rotation.AngleLerp(bank, 0.15f);
            UpdateFaceDir(target >= 0 ? Main.npc[target].Center : owner.Center);

            if (!Main.dedServ) {
                //轮廓下缘偶发凝珠滴落
                if (Main.rand.NextBool(26)) {
                    DripFromRim();
                }
                //扇翅甩出的细碎晶光：扇到最高点时从翼尖洒落
                if (wingCounter % 24 == 0 && Projectile.velocity.Length() > 2f && ViewedOwner) {
                    for (int side = -1; side <= 1; side += 2) {
                        PRTLoader.NewParticle<PRT_Sparkle>(
                            Projectile.Center + new Vector2(side * 56f, -10f),
                            new Vector2(side * 0.4f, Main.rand.NextFloat(0.2f, 0.7f)),
                            CrystalGlint * 0.45f, Main.rand.NextFloat(0.16f, 0.28f))
                            ?.Configure(CrystalGlint * 0.4f, 11, 0f, 0.6f);
                    }
                }
            }

            //出手裁决：布雷与俯冲交替；转场规则各端一致，owner 盖章
            if (target >= 0 && attackCooldown <= 0 && StateTimer > 26) {
                attackIndex++;
                State = attackIndex % 2 == 1 ? StateMines : StateDive;
                StateTimer = 0;
                StateParam = 0;
                Projectile.netUpdate = authority;
            }
        }

        //==================== 攻击一：空中结晶晶格雷 ====================

        private void UpdateMines(Player owner, bool authority) {
            int t = (int)StateTimer;
            int target = FindTarget(owner);

            if (t <= MineBrakeEnd) {
                //驻停摆位：减速回正，姿态先端住
                Projectile.velocity *= 0.86f;
                Projectile.rotation = Projectile.rotation.AngleLerp(0f, 0.25f);
                if (target >= 0) {
                    UpdateFaceDir(Main.npc[target].Center);
                }
                if (t == MineBrakeEnd) {
                    if (target < 0) {
                        EndAttack(authority, 45);
                        return;
                    }
                    //阵位定型（仅 owner 需要，spawn 参数随包带全）：弧线/环形交替
                    if (authority) {
                        BuildMineFormation(Main.npc[target]);
                    }
                }
                return;
            }

            if (t <= MineVolleyEnd) {
                //owner 既无阵位又无目标：收手，不演空掷哑剧
                if (authority && !mineTargetsReady && target < 0) {
                    EndAttack(authority, 45);
                    return;
                }
                //五连掷：节拍闩防快照回卷重掷
                int tossIndex = (t - MineBrakeEnd) / MineGap;
                if ((t - MineBrakeEnd) % MineGap == 0 && tossIndex < MineCount
                    && lastTossFired < tossIndex) {
                    lastTossFired = tossIndex;
                    StateParam = tossIndex + 1;
                    TossMine(owner, tossIndex, authority);
                }
                //掷间悬稳
                Projectile.velocity *= 0.92f;
                return;
            }

            //回摆
            Projectile.velocity *= 0.9f;
            if (t >= MineRecoverEnd) {
                EndAttack(authority, 150);
            }
        }

        /// <summary>阵位计算：弧线在皇后与目标之间张幕，环形绕目标合围；owner 端专用</summary>
        private void BuildMineFormation(NPC target) {
            Vector2 focus = target.Center + target.velocity * 16f;
            bool ringForm = attackIndex / 2 % 2 == 1;
            if (ringForm) {
                //环形：五点均布，随出手序转相位
                float baseAng = attackIndex * 0.37f + Seed;
                for (int i = 0; i < MineCount; i++) {
                    float ang = baseAng + MathHelper.TwoPi * i / MineCount;
                    mineTargets[i] = focus + ang.ToRotationVector2() * 198f;
                }
            }
            else {
                //弧线：面向皇后一侧的封锁幕
                float angBase = (Projectile.Center - focus).ToRotation();
                for (int i = 0; i < MineCount; i++) {
                    float ang = angBase + MathHelper.ToRadians(33f) * (i - (MineCount - 1) * 0.5f);
                    mineTargets[i] = focus + ang.ToRotationVector2() * 176f;
                }
            }
            mineTargetsReady = true;
        }

        private void TossMine(Player owner, int index, bool authority) {
            Vector2 hand = Projectile.Center + new Vector2(faceDir * 28f, -6f);

            //兜底：快照跳帧错过建阵整点时，就地补算，不许静默空掷
            if (authority && !mineTargetsReady) {
                int target = FindTarget(owner);
                if (target >= 0) {
                    BuildMineFormation(Main.npc[target]);
                }
            }

            //甩掷后坐：知重量者先退半步，顺带一点上浮
            Projectile.velocity += new Vector2(-faceDir * 1.7f, -0.7f);
            SoundEngine.PlaySound(SoundID.Item17 with { Volume = 0.45f, Pitch = -0.05f, MaxInstances = 3 }, hand);
            SoundEngine.PlaySound(SoundID.Item29 with { Volume = 0.22f, Pitch = 0.4f, MaxInstances = 3 }, hand);
            if (!Main.dedServ) {
                //出手喷洒的凝胶珠
                for (int i = 0; i < 4; i++) {
                    PRTLoader.NewParticle<PRT_GhostRainDrop>(hand + Main.rand.NextVector2Circular(4f, 4f),
                        new Vector2(faceDir * Main.rand.NextFloat(1.5f, 4f), -Main.rand.NextFloat(0.5f, 2f)),
                        GelBlood * 0.55f, Main.rand.NextFloat(0.32f, 0.5f))
                        ?.Configure(Main.rand.Next(12, 20), 0f);
                }
            }

            //雷体只在 owner 端生成，阵位点随 spawn 包带全
            if (authority && mineTargetsReady) {
                Vector2 point = mineTargets[index];
                Vector2 tossVel = (point - hand).SafeNormalize(Vector2.UnitX * faceDir) * 8f
                    + new Vector2(0f, -2f);
                Projectile.NewProjectile(Projectile.GetSource_FromAI(), hand, tossVel,
                    ModContent.ProjectileType<KikasaQueenGelMine>(), 0, 0f, Projectile.owner,
                    point.X, point.Y);
            }
        }

        //==================== 攻击二：加冕俯冲 ====================

        /// <summary>俯冲相位归一：0升位 1悬停 2俯冲(StateParam=落点Y) 3收势</summary>
        private int DivePhaseKey() {
            if (StateParam < 0f) {
                return 3;
            }
            if (StateParam >= 2f) {
                return 2;
            }
            return (int)StateParam;
        }

        private void UpdateDive(Player owner, KikasaDomainPlayer domain, bool authority) {
            int t = (int)StateTimer;
            int phase = DivePhaseKey();
            int target = FindTarget(owner);
            bool viewed = ViewedOwner;

            //相位换拍闩：远端可能靠收包换相，节拍在此统一起爆
            if (phase != lastDivePhase) {
                int prev = lastDivePhase;
                lastDivePhase = phase;
                if (phase == 2) {
                    OnPlungeLaunch();
                }
                else if (phase == 3 && prev != -1) {
                    ImpactBurst(owner, domain, authority);
                }
            }

            if (phase == 0) {
                //升位：滑至目标正上方高处，快而不乱
                if (target < 0) {
                    EndAttack(authority, 50);
                    return;
                }
                NPC npc = Main.npc[target];
                hoverAimPos = npc.Center;
                Vector2 want = npc.Center + new Vector2(npc.velocity.X * 12f, -DiveHoverHeight);
                Vector2 to = want - Projectile.Center;
                Vector2 desired = to * 0.09f;
                const float maxSpeed = 19f;
                if (desired.Length() > maxSpeed) {
                    desired = desired.SafeNormalize(Vector2.Zero) * maxSpeed;
                }
                Projectile.velocity = Vector2.Lerp(Projectile.velocity, desired, 0.14f);
                Projectile.rotation = Projectile.rotation.AngleLerp(
                    MathHelper.Clamp(Projectile.velocity.X * 0.04f, -0.25f, 0.25f), 0.15f);
                UpdateFaceDir(npc.Center);

                if (MathF.Abs(to.X) < 30f && MathF.Abs(to.Y) < 34f || t >= DiveAscendMax) {
                    StateParam = 1;
                    StateTimer = 0;
                    Projectile.netUpdate = authority;
                }
                return;
            }

            if (phase == 1) {
                //悬停定格：皇冠先行发亮的 tell；前半微微上提——吸气
                if (target >= 0) {
                    hoverAimPos = Main.npc[target].Center;
                }
                Projectile.velocity *= 0.8f;
                if (t < 13) {
                    Projectile.velocity.Y -= 0.05f;
                }
                Projectile.rotation = Projectile.rotation.AngleLerp(0f, 0.3f);

                if (!hoverChimeDone && t >= 3) {
                    hoverChimeDone = true;
                    SoundEngine.PlaySound(SoundID.Item29 with { Volume = 0.6f, Pitch = -0.1f, MaxInstances = 2 }, Projectile.Center);
                }
                //向皇冠汇聚的晶光，72% 后静默——落砸前的吸气
                float charge = t / (float)DiveHoverFrames;
                if (!Main.dedServ && charge < 0.72f && t % 2 == 0 && viewed) {
                    Vector2 crown = CrownWorldPos();
                    Vector2 from = crown + Main.rand.NextVector2Unit() * Main.rand.NextFloat(38f, 84f);
                    PRTLoader.NewParticle<PRT_Sparkle>(from, (crown - from) * 0.15f,
                        CrystalGlint * (0.4f + charge * 0.3f), Main.rand.NextFloat(0.2f, 0.34f))
                        ?.Configure(CrystalGlint * 0.45f, 10, 0f, 0.6f);
                }
                if (viewed && t % 8 == 4) {
                    ShakeViewer(0.6f + charge * charge * 1.2f);
                }

                if (t >= DiveHoverFrames) {
                    //锁落点：目标当前高度，钳在合理落程内；owner 盖章，远端包到即纠
                    float aimY = hoverAimPos != Vector2.Zero ? hoverAimPos.Y : Projectile.Center.Y + 420f;
                    float impactY = MathHelper.Clamp(aimY,
                        Projectile.Center.Y + 140f, Projectile.Center.Y + 780f);
                    StateParam = impactY;
                    StateTimer = 0;
                    Projectile.netUpdate = authority;
                }
                return;
            }

            if (phase == 2) {
                //垂直砸落：复利续速，越坠越急
                float impactY = StateParam;
                lastImpactY = impactY;
                Projectile.velocity.Y = MathF.Min(Projectile.velocity.Y * 1.02f + 0.4f, 44f);
                Projectile.rotation = Projectile.rotation.AngleLerp(0f, 0.4f);

                //坠程拖出的晶屑与血线
                if (!Main.dedServ && viewed) {
                    if (t % 2 == 0) {
                        PRTLoader.NewParticle<PRT_GhostRainDrop>(
                            Projectile.Center + new Vector2(Main.rand.NextFloat(-30f, 30f), -Main.rand.NextFloat(20f, 52f)),
                            new Vector2(0f, -Main.rand.NextFloat(1f, 2.4f)),
                            GelBlood * 0.5f, Main.rand.NextFloat(0.4f, 0.6f))
                            ?.Configure(Main.rand.Next(10, 18), 0f);
                    }
                    if (t % 3 == 1) {
                        PRTLoader.NewParticle<PRT_KikasaQueenSlimeFacet>(
                            Projectile.Center + new Vector2(Main.rand.NextFloat(-24f, 24f), -30f),
                            new Vector2(Main.rand.NextFloat(-0.6f, 0.6f), -Main.rand.NextFloat(0.5f, 1.6f)),
                            CrystalGlint, Main.rand.NextFloat(0.3f, 0.5f))
                            ?.Configure(Main.rand.Next(12, 20), 0.1f, Main.rand.NextFloat(-0.1f, 0.1f));
                    }
                }

                //穿过湖面：过水线水花拍（一次）
                bool lakeAlive = domain.AnyActive && domain.RiseT > 0.5f;
                if (lakeAlive && !plungeSplashed && Projectile.Center.Y >= domain.LakeWorldY
                    && impactY > domain.LakeWorldY + 40f) {
                    plungeSplashed = true;
                    SoundEngine.PlaySound(SoundID.SplashWeak with { Volume = 0.7f, Pitch = -0.2f, MaxInstances = 2 }, Projectile.Center);
                    if (viewed) {
                        Vector2 hit = new(Projectile.Center.X, domain.LakeWorldY);
                        KikasaDomainDeco.SplashAt(hit, 9);
                        KikasaDomainDeco.RippleAt(hit, 1.4f);
                    }
                }

                //预判式触底：下一帧会穿过落点就在本帧收口并贴齐，
                //否则 44px/帧 的坠速会把晶爆点甩过头
                if (Projectile.Center.Y + Projectile.velocity.Y >= impactY || t > DivePlungeMax) {
                    lastImpactY = MathF.Min(Projectile.Center.Y + Projectile.velocity.Y, impactY);
                    Projectile.Center = new Vector2(Projectile.Center.X, lastImpactY);
                    //触底帧速度清零：一帧死停读出"砸实了"，下一帧收势拍再回弹
                    Projectile.velocity = Vector2.Zero;
                    StateParam = -1;
                    StateTimer = 0;
                    Projectile.netUpdate = authority;
                }
                return;
            }

            //收势：回弹起身整理姿态
            if (t < 8) {
                Projectile.velocity.Y *= 0.88f;
            }
            else {
                Projectile.velocity.Y += 0.12f;
                Projectile.velocity *= 0.94f;
            }
            Projectile.rotation = Projectile.rotation.AngleLerp(0f, 0.2f);
            if (t >= DiveRecoverFrames) {
                EndAttack(authority, 170);
            }
        }

        /// <summary>砸落起跳拍：一帧定速向下，不做斜坡</summary>
        private void OnPlungeLaunch() {
            float dx = hoverAimPos != Vector2.Zero ? hoverAimPos.X - Projectile.Center.X : 0f;
            Projectile.velocity = new Vector2(MathHelper.Clamp(dx * 0.05f, -5f, 5f), 30f);
            SoundEngine.PlaySound(SoundID.DD2_WyvernDiveDown with { Volume = 0.7f, Pitch = -0.2f, MaxInstances = 2 }, Projectile.Center);
            SoundEngine.PlaySound(SoundID.SplashWeak with { Volume = 0.3f, Pitch = 0.2f, MaxInstances = 2 }, Projectile.Center);
            frameIndex = 8;
            frameTick = 0;
            if (ViewedOwner) {
                ShakeViewer(2.5f);
            }
        }

        /// <summary>落点晶爆新星：一圈晶片扇 + 大水花 + 震屏；晶片只在 owner 端生成</summary>
        private void ImpactBurst(Player owner, KikasaDomainPlayer domain, bool authority) {
            Vector2 hit = new(Projectile.Center.X, lastImpactY != 0f ? lastImpactY : Projectile.Center.Y);
            bool viewed = ViewedOwner;

            //回弹：砸下去的力被地面还回来一半
            Projectile.velocity = new Vector2(0f, -7.6f);
            frameIndex = 12;
            frameTick = 0;

            SoundEngine.PlaySound(SoundID.DD2_MonkStaffGroundImpact with { Volume = 0.75f, Pitch = -0.55f, MaxInstances = 2 }, hit);
            SoundEngine.PlaySound(SoundID.Shatter with { Volume = 0.7f, Pitch = -0.1f, MaxInstances = 2 }, hit);
            SoundEngine.PlaySound(SoundID.Item27 with { Volume = 0.5f, Pitch = -0.3f, MaxInstances = 2 }, hit);

            if (!Main.dedServ) {
                //晶爆碎屑半球
                for (int i = 0; i < 14; i++) {
                    float ang = -MathHelper.Pi * (0.08f + 0.84f * i / 13f);
                    Vector2 vel = ang.ToRotationVector2() * Main.rand.NextFloat(2.5f, 7.5f);
                    PRTLoader.NewParticle<PRT_KikasaQueenSlimeFacet>(
                        hit + new Vector2(Main.rand.NextFloat(-20f, 20f), -4f), vel,
                        Main.rand.NextBool(3) ? CrystalDeep : GelBlood,
                        Main.rand.NextFloat(0.45f, 0.8f))
                        ?.Configure(Main.rand.Next(24, 40), 0.24f, Main.rand.NextFloat(-0.18f, 0.18f));
                }
                //迸溅血珠
                for (int i = 0; i < 8; i++) {
                    PRTLoader.NewParticle<PRT_GhostRainDrop>(
                        hit + new Vector2(Main.rand.NextFloat(-24f, 24f), -4f),
                        new Vector2(Main.rand.NextFloat(-3f, 3f), -Main.rand.NextFloat(2f, 6f)),
                        GelBlood * 0.55f, Main.rand.NextFloat(0.45f, 0.7f))
                        ?.Configure(Main.rand.Next(20, 34), Main.rand.NextFloat(-0.4f, 0.4f));
                }
                PRTLoader.NewParticle<PRT_DWave>(hit, Vector2.Zero, CrystalGlint, 0.1f)
                    ?.Configure(new Vector2(0.55f, 1f), -MathHelper.PiOver2, 0.4f, 11);
                PRTLoader.NewParticle<PRT_Sparkle>(hit, Vector2.Zero, CrystalGlint, 0.9f)
                    ?.Configure(CrystalGlint * 0.7f, 13, 0.1f, 1.2f);
            }

            //大水花：落点贴着湖面才有，湖是实体存在
            bool lakeAlive = domain.AnyActive && domain.RiseT > 0.5f;
            if (lakeAlive && MathF.Abs(hit.Y - domain.LakeWorldY) < 120f) {
                SoundEngine.PlaySound(SoundID.SplashWeak with { Volume = 0.9f, Pitch = -0.4f, MaxInstances = 2 }, hit);
                if (viewed) {
                    Vector2 lakeHit = new(hit.X, domain.LakeWorldY);
                    KikasaDomainDeco.SplashAt(lakeHit + new Vector2(-14f, 0f), 12);
                    KikasaDomainDeco.SplashAt(lakeHit + new Vector2(14f, 0f), 12);
                    KikasaDomainDeco.RippleAt(lakeHit, 2.4f);
                }
            }
            if (viewed) {
                ShakeViewer(6f);
            }

            //晶爆新星：一圈晶片只在 owner 端生成，spawn 参数带全
            if (authority) {
                int damage = (int)owner.GetTotalDamage(DamageClass.Summon).ApplyTo(ShardDamage);
                const int novaCount = 10;
                float baseAng = Seed % MathHelper.TwoPi;
                for (int i = 0; i < novaCount; i++) {
                    float ang = baseAng + MathHelper.TwoPi * i / novaCount;
                    Projectile.NewProjectile(Projectile.GetSource_FromAI(), hit,
                        ang.ToRotationVector2() * 13f,
                        ModContent.ProjectileType<KikasaQueenCrystalShard>(), damage, 2f, Projectile.owner);
                }
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

            if (!crownMeltFxDone && t >= 1) {
                //失冕拍：晶体先失去光泽的第一声
                crownMeltFxDone = true;
                SoundEngine.PlaySound(SoundID.Item27 with { Volume = 0.35f, Pitch = -0.6f, MaxInstances = 2 }, Projectile.Center);
            }

            if (lakeAlive) {
                //坠回湖里
                Projectile.velocity.X *= 0.94f;
                Projectile.velocity.Y = MathF.Min(Projectile.velocity.Y + 0.24f, 8f);
            }
            else {
                //湖已不在：原地化水
                Projectile.velocity *= 0.9f;
            }
            Projectile.rotation = Projectile.rotation.AngleLerp(0f, 0.1f);

            if (!Main.dedServ) {
                //皇冠脱落化水：冠位淌珠
                if (t < CrownMeltEnd && t % 3 == 1) {
                    Vector2 crown = CrownWorldPos();
                    PRTLoader.NewParticle<PRT_GhostRainDrop>(crown + Main.rand.NextVector2Circular(12f, 6f),
                        new Vector2(Main.rand.NextFloat(-0.4f, 0.4f), Main.rand.NextFloat(1.2f, 2.6f)),
                        GelBlood * 0.55f, Main.rand.NextFloat(0.35f, 0.55f))
                        ?.Configure(Main.rand.Next(14, 24), 0f);
                }
                //晶翼失泽化水：翼尖淌珠 + 偶发失色晶屑
                if (t >= WingMeltStart && t < WingMeltEnd && t % 3 == 0) {
                    int side = t % 6 == 0 ? 1 : -1;
                    PRTLoader.NewParticle<PRT_GhostRainDrop>(
                        Projectile.Center + new Vector2(side * Main.rand.NextFloat(36f, 58f), -6f),
                        new Vector2(0f, Main.rand.NextFloat(1.4f, 2.8f)),
                        GelBlood * 0.5f, Main.rand.NextFloat(0.35f, 0.55f))
                        ?.Configure(Main.rand.Next(14, 24), 0f);
                    if (t % 6 == 0) {
                        PRTLoader.NewParticle<PRT_KikasaQueenSlimeFacet>(
                            Projectile.Center + new Vector2(side * Main.rand.NextFloat(30f, 52f), -4f),
                            new Vector2(side * 0.3f, Main.rand.NextFloat(0.8f, 1.8f)),
                            CrystalDeep, Main.rand.NextFloat(0.3f, 0.5f))
                            ?.Configure(Main.rand.Next(16, 26), 0.2f, 0.05f, 0.15f);
                    }
                }
                //边沉边化成血珠
                if (t % 2 == 0 && CurrentAlpha() > 0.15f) {
                    PRTLoader.NewParticle<PRT_GhostRainDrop>(
                        Projectile.Center + Main.rand.NextVector2Circular(34f, 34f),
                        new Vector2(Main.rand.NextFloat(-0.6f, 0.6f), Main.rand.NextFloat(1.5f, 3f)),
                        GelBlood * 0.5f, Main.rand.NextFloat(0.35f, 0.6f))
                        ?.Configure(Main.rand.Next(12, 22), 0f);
                }
            }

            //过水线拍（一次）
            if (lakeAlive && !dissolveSplashed && Projectile.Center.Y >= lakeY) {
                dissolveSplashed = true;
                StateParam = 1f;
                SoundEngine.PlaySound(SoundID.SplashWeak with { Volume = 0.7f, Pitch = -0.35f, MaxInstances = 2 }, Projectile.Center);
                if (ViewedOwner) {
                    Vector2 hit = new(Projectile.Center.X, lakeY);
                    KikasaDomainDeco.SplashAt(hit, 10);
                    KikasaDomainDeco.RippleAt(hit, 1.5f);
                    ShakeViewer(2f);
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

        /// <summary>横向朝向带滞回，端庄不来回抽</summary>
        private void UpdateFaceDir(Vector2 look) {
            float dx = look.X - Projectile.Center.X;
            if (MathF.Abs(dx) > 16f) {
                faceDir = dx >= 0f ? 1 : -1;
            }
        }

        private void DripFromRim() {
            Vector2 rim = Projectile.Center + new Vector2(Main.rand.NextFloat(-34f, 34f), Main.rand.NextFloat(22f, 40f));
            PRTLoader.NewParticle<PRT_GhostRainDrop>(rim,
                new Vector2(0f, Main.rand.NextFloat(0.5f, 1.2f)),
                GelBlood * Main.rand.NextFloat(0.4f, 0.55f),
                Main.rand.NextFloat(0.35f, 0.6f))
                ?.Configure(Main.rand.Next(20, 34), 0f);
        }

        private bool ViewedOwner
            => KikasaDomain.Viewed != null && KikasaDomain.Viewed.Player.whoAmI == Projectile.owner;

        private static void ShakeViewer(float amount)
            => Main.LocalPlayer?.CWR()?.GetScreenShake(amount);

        //==================== 帧动画（本地表现，不入同步）====================

        private void UpdateFrames() {
            int t = (int)StateTimer;
            switch (State) {
                case StateEmerge:
                    if (t < OmenFrames) {
                        frameIndex = 0;
                    }
                    else if (t < RiseEnd) {
                        AdvanceRise(4);
                    }
                    else {
                        AdvanceFly(7);
                    }
                    return;
                case StateMines:
                    if (t > MineBrakeEnd && t <= MineVolleyEnd) {
                        //甩掷帧：原版凝胶喷洒三帧轮换
                        frameIndex = 13 + (t - MineBrakeEnd) / 3 % 3;
                        return;
                    }
                    AdvanceFly(5);
                    return;
                case StateDive:
                    switch (DivePhaseKey()) {
                        case 0:
                            AdvanceFly(4);
                            return;
                        case 1:
                            //悬停定格：帧冻结
                            return;
                        case 2:
                            AdvanceFall();
                            return;
                        default:
                            //收势：砸落深蹲→半蹲→重新升起
                            if (t < 8) {
                                frameIndex = 12;
                            }
                            else if (t < 14) {
                                frameIndex = 11;
                            }
                            else if (t < 30) {
                                frameIndex = 4 + Math.Min((t - 14) / 4, 3);
                            }
                            else {
                                AdvanceFly(5);
                            }
                            return;
                    }
                case StateDissolve:
                    //失泽后帧冻结，生命的钟停在那一格
                    if (t < DullFrames) {
                        AdvanceFly(9);
                    }
                    return;
                default:
                    AdvanceFly(5);
                    return;
            }
        }

        /// <summary>飞行循环帧 20~23</summary>
        private void AdvanceFly(int rate) {
            if (frameIndex is < 20 or > 23) {
                frameIndex = 20;
                frameTick = 0;
            }
            if (++frameTick >= rate) {
                frameTick = 0;
                frameIndex++;
                if (frameIndex > 23) {
                    frameIndex = 20;
                }
            }
        }

        /// <summary>升起帧 4~7</summary>
        private void AdvanceRise(int rate) {
            if (frameIndex is < 4 or > 7) {
                frameIndex = 4;
                frameTick = 0;
            }
            if (++frameTick >= rate) {
                frameTick = 0;
                frameIndex = Math.Min(frameIndex + 1, 7);
            }
        }

        /// <summary>下落帧 8~10</summary>
        private void AdvanceFall() {
            if (frameIndex is < 8 or > 10) {
                frameIndex = 8;
                frameTick = 0;
            }
            if (++frameTick >= 3) {
                frameTick = 0;
                frameIndex = Math.Min(frameIndex + 1, 10);
            }
        }

        /// <summary>晶翼扇动计数：0~23 循环，每 6 计一帧（原版同步率）</summary>
        private void UpdateWings() {
            int t = (int)StateTimer;
            switch (State) {
                case StateEmerge:
                    //展开→定格期冻结在张开姿；首扇后开始循环
                    if (t < WingHoldEnd) {
                        wingCounter = 0;
                        return;
                    }
                    break;
                case StateDive: {
                    int phase = DivePhaseKey();
                    if (phase == 1) {
                        //悬停定格：翼冻结在张开姿——出水定格的同款收束
                        wingCounter = 0;
                        return;
                    }
                    if (phase == 2) {
                        //俯冲收翼
                        wingCounter = 12;
                        return;
                    }
                    break;
                }
                case StateDissolve:
                    //扇动渐止
                    if (t >= DullFrames || t % 2 == 0) {
                        return;
                    }
                    break;
            }
            //快移时扇得更急
            wingCounter += Projectile.velocity.Length() > 8f ? 2 : 1;
            if (wingCounter >= 24) {
                wingCounter -= 24;
            }
        }

        //==================== 表现参数 ====================

        private float CurrentAlpha() {
            int t = (int)StateTimer;
            return State switch {
                StateEmerge => t < OmenFrames ? 0f : MathHelper.Clamp((t - OmenFrames) / 4f, 0f, 1f),
                StateDissolve => MathHelper.Clamp((DissolveFrames - t) / 12f, 0f, 1f),
                _ => 1f,
            };
        }

        /// <summary>uForm：1=全血水 0=真身；常态半沉呼吸，出水自上而下凝实，溶解回涨血相</summary>
        private float CurrentForm() {
            int t = (int)StateTimer;
            float steady = 0.34f + MathF.Sin(Main.GlobalTimeWrappedHourly * 2.9f + Seed) * 0.05f;
            return State switch {
                StateEmerge => t < OmenFrames
                    ? 1f
                    : MathHelper.Lerp(1f, steady, SmoothStep01(MathHelper.Clamp((t - OmenFrames) / (float)(RiseEnd - OmenFrames), 0f, 1f))),
                StateDissolve => MathHelper.Clamp(steady + t / (float)DissolveFrames * 0.34f, 0f, 1f),
                _ => steady,
            };
        }

        /// <summary>uScanMode：出水期自上而下扫描凝实，加冕拍前后渐回噪声斑驳</summary>
        private float CurrentScanMode() {
            if (State != StateEmerge) {
                return 0f;
            }
            int t = (int)StateTimer;
            if (t <= RiseEnd) {
                return 1f;
            }
            return 1f - MathHelper.Clamp((t - RiseEnd) / 14f, 0f, 1f);
        }

        /// <summary>身躯蚀溶进度：晶体失泽之后才开始化水</summary>
        private float CurrentDissolve()
            => State == StateDissolve
                ? MathF.Pow(MathHelper.Clamp((StateTimer - BodyErodeStart) / (float)(BodyErodeEnd - BodyErodeStart), 0f, 1f), 0.9f)
                : 0f;

        /// <summary>晶翼成长度：出水加冕后展开，溶解期失泽收拢</summary>
        private float WingGrow() {
            int t = (int)StateTimer;
            return State switch {
                StateEmerge => MathHelper.Clamp((t - WingBeat) / 10f, 0f, 1f),
                StateDissolve => 1f - MathHelper.Clamp((t - WingMeltStart) / (float)(WingMeltEnd - WingMeltStart), 0f, 1f),
                _ => 1f,
            };
        }

        /// <summary>皇冠凝成度：加冕拍凝成，溶解期最先失色脱落</summary>
        private float CrownGrow() {
            int t = (int)StateTimer;
            return State switch {
                StateEmerge => MathHelper.Clamp((t - CrownBeat) / 8f, 0f, 1f),
                StateDissolve => 1f - MathHelper.Clamp(t / (float)CrownMeltEnd, 0f, 1f),
                _ => 1f,
            };
        }

        /// <summary>光泽系数：溶解先失光泽再化水——晶面高光与脉动灯全走这条包络</summary>
        private float LusterK()
            => State == StateDissolve
                ? 1f - MathHelper.Clamp(StateTimer / (float)DullFrames, 0f, 1f)
                : 1f;

        /// <summary>皇冠 tell 亮度：加冕拍一闪 / 俯冲悬停持续爬升 / 跟随态偶尔一瞬</summary>
        private float CrownFlash() {
            int t = (int)StateTimer;
            if (State == StateEmerge && t >= CrownBeat) {
                return MathF.Sin(MathHelper.Clamp((t - CrownBeat) / 14f, 0f, 1f) * MathHelper.Pi) * 0.9f;
            }
            if (State == StateDive && DivePhaseKey() == 1) {
                float charge = t / (float)DiveHoverFrames;
                return charge * charge * 0.95f;
            }
            if (State == StateFollow) {
                //偶发的一瞬冠辉，王权在暗处也醒着
                int cycle = t % 110;
                if (cycle >= 50 && cycle < 64) {
                    return MathF.Sin((cycle - 50) / 14f * MathHelper.Pi) * 0.3f;
                }
            }
            return 0f;
        }

        /// <summary>逐轴身形：破水过冲 / 悬停吸气收紧 / 俯冲纵拉 / 落地压扁弹性回正</summary>
        private Vector2 BodyScaleVec() {
            const float baseScale = 0.94f;
            int t = (int)StateTimer;
            Vector2 axis = Vector2.One;
            if (State == StateEmerge && t >= OmenFrames && t < OmenFrames + 10) {
                float k = 1f - (t - OmenFrames) / 10f;
                axis = new Vector2(1f - 0.06f * k, 1f + 0.1f * k);
            }
            else if (State == StateDive) {
                switch (DivePhaseKey()) {
                    case 1: {
                        float charge = MathHelper.Clamp(t / (float)DiveHoverFrames, 0f, 1f);
                        axis = new Vector2(1f + 0.05f * charge, 1f - 0.07f * charge);
                        break;
                    }
                    case 2:
                        axis = new Vector2(0.9f, 1.14f);
                        break;
                    case 3: {
                        if (t < 8) {
                            axis = new Vector2(1.18f, 0.8f);
                        }
                        else {
                            //弹性回正：衰减震荡的果冻余韵
                            float k = MathHelper.Clamp((t - 8) / 20f, 0f, 1f);
                            float wobble = MathF.Sin(k * MathHelper.Pi * 2.4f) * 0.1f * (1f - k);
                            axis = new Vector2(1f + 0.18f * (1f - k) - wobble, 1f - 0.2f * (1f - k) + wobble);
                        }
                        break;
                    }
                }
            }
            return axis * baseScale;
        }

        private static float SmoothStep01(float t) => t * t * (3f - 2f * t);

        //==================== 绘制几何 ====================

        /// <summary>视觉底缘：原版皇后以 Bottom 为锚（帧下缘贴此），旋转也绕它</summary>
        private Vector2 VisualBottom => Projectile.Center + new Vector2(0f, Projectile.height * 0.5f + 2f);

        /// <summary>本体帧矩形：贴图 2 列 × 16 行，帧号 0~31</summary>
        private Rectangle BodyFrameRect(Texture2D tex) {
            int perColumn = Main.npcFrameCount[NPCID.QueenSlimeBoss];
            Rectangle frame = tex.Frame(2, 16, frameIndex / perColumn, frameIndex % perColumn);
            frame.Inflate(0, -2);
            return frame;
        }

        /// <summary>晶核相对身体中心的纵向偏移（原版逐帧表）</summary>
        private static float CoreOffsetY(int f) => f switch {
            1 or 6 => -10f,
            3 or 5 => 10f,
            4 or 12 or 13 or 14 or 15 => 18f,
            7 or 8 => -14f,
            9 => -16f,
            10 => -18f,
            11 => 20f,
            20 => -14f,
            21 or 23 => -18f,
            22 => -22f,
            _ => 0f,
        };

        /// <summary>皇冠纵向偏移（原版逐帧表，与晶核表在帧 6 上分叉）</summary>
        private static float CrownOffsetY(int f) => f switch {
            1 => -10f,
            3 or 5 or 6 => 10f,
            4 or 12 or 13 or 14 or 15 => 18f,
            7 or 8 => -14f,
            9 => -16f,
            10 => -18f,
            11 => 20f,
            20 => -14f,
            21 or 23 => -18f,
            22 => -22f,
            _ => 0f,
        };

        /// <summary>皇冠世界坐标（含帧偏移与悬浮微动，未含整体旋转）</summary>
        private Vector2 CrownWorldPos() {
            float crownH = 34f;
            //Extra 贴图在资产初始化时已全量加载，直接取用
            Texture2D crown = TextureAssets.Extra[177]?.Value;
            if (crown != null) {
                crownH = crown.Height;
            }
            float hover = MathF.Sin(Main.GlobalTimeWrappedHourly * 1.7f + Seed) * 2f;
            return new Vector2(Projectile.Center.X,
                Projectile.Center.Y - Projectile.height * 0.5f - crownH + 44f + CrownOffsetY(frameIndex) + hover);
        }

        //==================== 绘制 ====================

        public override bool PreDraw(ref Color lightColor) {
            Main.instance.LoadNPC(NPCID.QueenSlimeBoss);
            Texture2D bodyTex = TextureAssets.Npc[NPCID.QueenSlimeBoss]?.Value;
            Texture2D crownTex = TextureAssets.Extra[177]?.Value;
            Texture2D wingTex = TextureAssets.Extra[185]?.Value;
            Texture2D coreTex = TextureAssets.Extra[186]?.Value;
            if (bodyTex == null) {
                return false;
            }

            SpriteBatch sb = Main.spriteBatch;
            float alpha = CurrentAlpha();
            KikasaDomainPlayer domain = Owner?.GetModPlayer<KikasaDomainPlayer>();

            //预兆：水下血光自深处上浮（她比物件更亮些——这是加冕的生物）
            if (domain != null) {
                DrawOmenGlow(sb, domain);
            }

            if (alpha > 0.01f) {
                DrawPlungeGhosts(sb, bodyTex, alpha);
                DrawShaderPieces(sb, bodyTex, wingTex, crownTex, alpha);
                DrawCoreAndAccents(sb, coreTex, alpha);
            }
            return false;
        }

        private void DrawOmenGlow(SpriteBatch sb, KikasaDomainPlayer domain) {
            if (State != StateEmerge || StateTimer >= OmenFrames) {
                return;
            }
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (glow == null) {
                return;
            }
            float ot = MathHelper.Clamp(StateTimer / (float)OmenFrames, 0f, 1f);
            float ease = 1f - (1f - ot) * (1f - ot);
            Vector2 pos = new(Projectile.Center.X, domain.LakeWorldY + MathHelper.Lerp(56f, 10f, ease));
            float r = 38f + 24f * ease;
            //A=0 加色：主批直接画，无需切批
            sb.Draw(glow, pos - Main.screenPosition, null,
                (CrystalGlint with { A = 0 }) * (0.4f * ease), 0f, glow.Size() * 0.5f,
                new Vector2(r * 2.8f / glow.Width, r * 1.1f / glow.Height), SpriteEffects.None, 0f);
        }

        /// <summary>俯冲残影：坠程旧位的纵拉幻影，速度门控只在砸落中亮</summary>
        private void DrawPlungeGhosts(SpriteBatch sb, Texture2D tex, float alpha) {
            if (State != StateDive || DivePhaseKey() != 2 || Projectile.velocity.Length() < 15f) {
                return;
            }
            Rectangle frame = BodyFrameRect(tex);
            Vector2 origin = frame.Size() * new Vector2(0.5f, 1f);
            SpriteEffects fx = faceDir >= 0 ? SpriteEffects.None : SpriteEffects.FlipHorizontally;
            for (int k = Projectile.oldPos.Length - 1; k >= 1; k--) {
                Vector2 oldCenter = Projectile.oldPos[k] + Projectile.Size * 0.5f;
                if (oldCenter == Projectile.Size * 0.5f) {
                    continue;
                }
                float fall = 1f - k / (float)Projectile.oldPos.Length;
                Vector2 ghostBottom = oldCenter + new Vector2(0f, Projectile.height * 0.5f + 2f);
                sb.Draw(tex, ghostBottom - Main.screenPosition, frame,
                    GelBlood * (0.3f * fall * alpha), 0f, origin,
                    new Vector2(0.86f, 1.1f + k * 0.03f) * 0.94f, fx, 0f);
            }
        }

        /// <summary>血水材质批：晶翼→本体→皇冠，同一 Immediate 批逐件换参</summary>
        private void DrawShaderPieces(SpriteBatch sb, Texture2D bodyTex, Texture2D wingTex, Texture2D crownTex, float alpha) {
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
                form.Parameters["uScanMode"]?.SetValue(CurrentScanMode());
            }

            float bodyDissolve = CurrentDissolve();
            Vector2 scaleVec = BodyScaleVec();
            Vector2 bottom = VisualBottom;
            SpriteEffects fx = faceDir >= 0 ? SpriteEffects.None : SpriteEffects.FlipHorizontally;

            //晶翼（身后层）：血水凝成，蚀入式展开/失泽收拢
            float wingGrow = WingGrow();
            if (wingTex != null && wingGrow > 0.01f) {
                int wingFrame = wingCounter / 6 % 4;
                Rectangle wr = wingTex.Frame(1, 4, 0, wingFrame);
                float wingDissolve = MathF.Max(bodyDissolve, 1f - wingGrow);
                Vector2 wingScale = new Vector2(0.8f * (0.3f + 0.7f * wingGrow), 0.8f * (0.55f + 0.45f * wingGrow)) * 0.94f;
                float flapTilt = MathHelper.Clamp(Projectile.velocity.Y, -6f, 6f) * -0.1f;
                for (int i = 0; i < 2; i++) {
                    Vector2 origin = wr.Size() * new Vector2(i == 0 ? 1f : 0f, 0.5f);
                    SpriteEffects wingFx = i == 0 ? SpriteEffects.None : SpriteEffects.FlipHorizontally;
                    Vector2 pos = new(Projectile.Center.X + (i == 1 ? 2f : 0f), Projectile.Center.Y);
                    if (Projectile.rotation != 0f) {
                        pos = pos.RotatedBy(Projectile.rotation, bottom);
                    }
                    float tilt = i == 0 ? -flapTilt : flapTilt;
                    Color wingColor;
                    if (shaderOk) {
                        ApplyFormPiece(form, wingTex, wr, 3.7f + i, wingDissolve);
                        wingColor = new Color(255, 255, 255, (byte)(alpha * 255f));
                    }
                    else {
                        wingColor = Color.Lerp(Color.White, GelBlood, 0.55f) * (alpha * wingGrow);
                    }
                    sb.Draw(wingTex, pos - Main.screenPosition, wr, wingColor,
                        Projectile.rotation + tilt, origin, wingScale, wingFx, 0f);
                }
            }

            //本体
            Rectangle frame = BodyFrameRect(bodyTex);
            Color bodyColor;
            if (shaderOk) {
                ApplyFormPiece(form, bodyTex, frame, 0f, bodyDissolve);
                bodyColor = new Color(255, 255, 255, (byte)(alpha * 255f));
            }
            else {
                bodyColor = Color.Lerp(Color.White, GelBlood, 0.55f) * alpha;
            }
            sb.Draw(bodyTex, bottom - Main.screenPosition, frame, bodyColor,
                Projectile.rotation, frame.Size() * new Vector2(0.5f, 1f), scaleVec, fx, 0f);

            //皇冠：加冕拍凝成，溶解最先脱落（失冕下滑）
            float crownGrow = CrownGrow();
            if (crownTex != null && crownGrow > 0.01f) {
                Rectangle cr = crownTex.Frame();
                Vector2 crownPos = CrownWorldPos();
                if (State == StateDissolve) {
                    crownPos.Y += (1f - crownGrow) * 12f;
                }
                if (Projectile.rotation != 0f) {
                    crownPos = crownPos.RotatedBy(Projectile.rotation, bottom);
                }
                float crownDissolve = MathF.Max(bodyDissolve, 1f - crownGrow);
                Color crownColor;
                if (shaderOk) {
                    ApplyFormPiece(form, crownTex, cr, 7.3f, crownDissolve);
                    crownColor = new Color(255, 255, 255, (byte)(alpha * 255f));
                }
                else {
                    crownColor = Color.Lerp(Color.White, GelBlood, 0.55f) * (alpha * crownGrow);
                }
                sb.Draw(crownTex, crownPos - Main.screenPosition, cr, crownColor,
                    Projectile.rotation, cr.Size() * 0.5f, 0.94f, fx, 0f);
            }

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
        }

        /// <summary>逐件血水参数：uForm/uDissolve 共享身体节律，uSeed 错相防蚀纹撞样</summary>
        private void ApplyFormPiece(Effect form, Texture2D tex, Rectangle frame, float seedOffset, float dissolve) {
            form.Parameters["uSeed"]?.SetValue(Seed + seedOffset);
            form.Parameters["uForm"]?.SetValue(CurrentForm());
            form.Parameters["uDissolve"]?.SetValue(dissolve);
            form.Parameters["uUvRect"]?.SetValue(new Vector4(
                frame.X / (float)tex.Width, frame.Y / (float)tex.Height,
                frame.Width / (float)tex.Width, frame.Height / (float)tex.Height));
            form.Parameters["uTexel"]?.SetValue(new Vector2(1f / tex.Width, 1f / tex.Height));
            form.Parameters["uAspect"]?.SetValue(frame.Width / (float)frame.Height);
            form.CurrentTechnique.Passes[0].Apply();
        }

        /// <summary>晶核与高光层：核心脉动、皇冠 tell 辉光、俯冲流光——全走 A=0 加色，无需切批</summary>
        private void DrawCoreAndAccents(SpriteBatch sb, Texture2D coreTex, float alpha) {
            float luster = LusterK();
            Vector2 bottom = VisualBottom;
            SpriteEffects fx = faceDir >= 0 ? SpriteEffects.None : SpriteEffects.FlipHorizontally;

            //晶核：她胸腔里那颗没被血水吃掉的心（真身层，晶面读数的主锚）
            if (coreTex != null && alpha > 0.02f) {
                Vector2 corePos = Projectile.Center + new Vector2(0f, CoreOffsetY(frameIndex));
                if (Projectile.rotation != 0f) {
                    corePos = corePos.RotatedBy(Projectile.rotation, bottom);
                }
                float coreDissolve = CurrentDissolve();
                float coreA = alpha * (1f - coreDissolve);
                if (coreA > 0.02f) {
                    Rectangle cr = coreTex.Frame();
                    sb.Draw(coreTex, corePos - Main.screenPosition, cr,
                        Color.Lerp(Color.White, GelBlood, 0.45f) * coreA,
                        Projectile.rotation, cr.Size() * 0.5f, 0.94f, fx, 0f);
                    //心晶脉动：微光一涨一收，失泽即熄
                    Texture2D soft = CWRAsset.SoftGlow?.Value;
                    if (soft != null && luster > 0.03f) {
                        float pulse = 0.3f + 0.2f * MathF.Sin(Main.GlobalTimeWrappedHourly * 3.1f + Seed);
                        sb.Draw(soft, corePos - Main.screenPosition, null,
                            CrystalCore * (pulse * luster * coreA), 0f, soft.Size() * 0.5f,
                            new Vector2(52f / soft.Width * 2f), SpriteEffects.None, 0f);
                    }
                    sb.Draw(coreTex, corePos - Main.screenPosition, cr,
                        (CrystalGlint with { A = 0 }) * (0.35f * luster * coreA
                            * (0.7f + 0.3f * MathF.Sin(Main.GlobalTimeWrappedHourly * 3.1f + Seed))),
                        Projectile.rotation, cr.Size() * 0.5f, 0.94f, fx, 0f);
                }
            }

            //皇冠 tell 辉光：加冕一闪 / 俯冲悬停爬升 / 跟随偶醒
            float flash = CrownFlash() * luster * alpha * CrownGrow();
            if (flash > 0.03f) {
                Vector2 crownPos = CrownWorldPos();
                if (Projectile.rotation != 0f) {
                    crownPos = crownPos.RotatedBy(Projectile.rotation, bottom);
                }
                Texture2D flare = CWRAsset.StarFlare01?.Value;
                Texture2D soft = CWRAsset.SoftGlow?.Value;
                if (soft != null) {
                    sb.Draw(soft, crownPos - Main.screenPosition, null, CrystalCore * (0.55f * flash),
                        0f, soft.Size() * 0.5f, new Vector2(60f / soft.Width * 2f), SpriteEffects.None, 0f);
                }
                if (flare != null) {
                    sb.Draw(flare, crownPos - Main.screenPosition, null, CrystalCore * (0.8f * flash),
                        Seed + Main.GlobalTimeWrappedHourly * 0.4f, flare.Size() * 0.5f,
                        0.26f + 0.22f * flash, SpriteEffects.None, 0f);
                }
            }

            //俯冲流光：坠速门控的垂直拉丝
            if (State == StateDive && DivePhaseKey() == 2 && Projectile.velocity.Y > 18f) {
                Texture2D soft = CWRAsset.SoftGlow?.Value;
                if (soft != null) {
                    float speedK = MathHelper.Clamp(Projectile.velocity.Y / 44f, 0f, 1f);
                    Vector2 streakPos = Projectile.Center - Projectile.velocity * 0.6f;
                    sb.Draw(soft, streakPos - Main.screenPosition, null,
                        CrystalCore * (0.4f * speedK * alpha), MathHelper.PiOver2,
                        soft.Size() * 0.5f,
                        new Vector2(150f * speedK / soft.Width * 2f, 34f / soft.Height * 2f),
                        SpriteEffects.None, 0f);
                }
            }
        }

        //==================== 命中与谢幕 ====================

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            //俯冲撞击的溅血与碎晶（OnHit 只在 owner 端跑，队友看残影即可）
            if (Main.dedServ) {
                return;
            }
            for (int i = 0; i < 7; i++) {
                PRTLoader.NewParticle<PRT_GhostRainDrop>(
                    target.Center + Main.rand.NextVector2Circular(22f, 22f),
                    Projectile.velocity * 0.2f + Main.rand.NextVector2Circular(2.5f, 2.5f),
                    GelBlood * 0.6f, Main.rand.NextFloat(0.4f, 0.7f))
                    ?.Configure(Main.rand.Next(14, 24), Main.rand.NextFloat(-0.5f, 0.5f));
            }
            for (int i = 0; i < 3; i++) {
                PRTLoader.NewParticle<PRT_KikasaQueenSlimeFacet>(
                    target.Center + Main.rand.NextVector2Circular(18f, 18f),
                    Projectile.velocity * 0.15f + Main.rand.NextVector2Circular(2f, 2f),
                    CrystalGlint, Main.rand.NextFloat(0.35f, 0.6f))
                    ?.Configure(Main.rand.Next(16, 26), 0.22f, Main.rand.NextFloat(-0.15f, 0.15f));
            }
            SoundEngine.PlaySound(SoundID.NPCHit1 with { Volume = 0.6f, Pitch = -0.25f, MaxInstances = 3 }, target.Center);
            SoundEngine.PlaySound(SoundID.Item27 with { Volume = 0.3f, Pitch = -0.1f, MaxInstances = 3 }, target.Center);
        }

        public override void OnKill(int timeLeft) {
            //她走了，她的晶格雷也不该留下
            OrderMinesMelt();
            //谢幕残珠：溶解尾拍或异常移除都留一口血水与几粒失泽晶屑
            if (Main.dedServ) {
                return;
            }
            for (int i = 0; i < 10; i++) {
                PRTLoader.NewParticle<PRT_GhostRainDrop>(
                    Projectile.Center + Main.rand.NextVector2Circular(30f, 30f),
                    new Vector2(Main.rand.NextFloat(-1.2f, 1.2f), Main.rand.NextFloat(0.5f, 2.8f)),
                    GelBlood * 0.5f, Main.rand.NextFloat(0.35f, 0.6f))
                    ?.Configure(Main.rand.Next(14, 26), 0f);
            }
            for (int i = 0; i < 4; i++) {
                PRTLoader.NewParticle<PRT_KikasaQueenSlimeFacet>(
                    Projectile.Center + Main.rand.NextVector2Circular(24f, 24f),
                    new Vector2(Main.rand.NextFloat(-0.8f, 0.8f), Main.rand.NextFloat(0.8f, 2f)),
                    CrystalDeep, Main.rand.NextFloat(0.32f, 0.55f))
                    ?.Configure(Main.rand.Next(16, 28), 0.22f, 0.06f, 0.1f);
            }
            PRTLoader.NewParticle<PRT_GhostRainMist>(Projectile.Center,
                new Vector2(0f, -0.25f), MistBlood * 0.7f, Main.rand.NextFloat(0.6f, 0.9f))
                ?.Configure(Main.rand.Next(50, 80));
        }
    }
}
