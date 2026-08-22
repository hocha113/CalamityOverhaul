using CalamityOverhaul.Common;
using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaDomains;
using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaServants.KikasaEye;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.Graphics.Shaders;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaServants.KikasaEmpress
{
    /// <summary>
    /// 鬼奴·湖水版光之女皇「泣血虹裳」。血湖之水凝成的弹幕女皇：
    /// 出水为虹光花纹涟漪绽开→携逆血雨浮出→展翅定格；战斗循环为
    /// 花瓣扇曼陀罗、圣舞血矛列雨、血虹缎带弧扫三攻轮换，全部是几何图案，
    /// 预告清晰、绽放华丽、留白讲究，接触伤害恒关（她不用身体碰人）。
    /// 多层特殊绘制复刻原版 HallowBoss（后翅/翅辉/触手裙/本体/裙裾/双臂），
    /// 血水材质走 KikasaItemForm，翅膀珠光借原版 HallowBoss 渐变着色器压低透明度。
    /// 联机同克眼契约：owner 裁决转场盖 netUpdate 章，节拍闩防快照回卷，
    /// 子弹幕只在 owner 端生成、spawn 参数完整，生命线只有 owner 判
    /// </summary>
    internal class KikasaEmpressServant : ModProjectile, IKikasaServant
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        //==================== 可调基数（占位初值，验收再调）====================

        /// <summary>接触基伤（召唤入口换算用；CanDamage 恒关，弹幕女皇不撞人）</summary>
        internal const int ContactDamage = 760;

        /// <summary>花瓣/血矛/缎带共用基伤（召唤加成前）</summary>
        internal const int BoltDamage = 410;

        //==================== 状态 ====================

        private const int StateEmerge = 0;
        private const int StateFollow = 1;
        private const int StateMandala = 2;
        private const int StateLanceRain = 3;
        private const int StateRibbon = 4;
        private const int StateDissolve = 5;

        private int State { get => (int)Projectile.ai[0]; set => Projectile.ai[0] = value; }
        private ref float StateTimer => ref Projectile.ai[1];
        /// <summary>状态内子计数：曼陀罗=已放波数，其余状态为普通相位号</summary>
        private ref float StateParam => ref Projectile.ai[2];

        //==================== 时序 ====================

        //出水：花纹涟漪预兆→破水携逆血雨→升起凝实→展翅定格→落定
        private const int OmenFrames = 34;
        private const int RiseEnd = 78;
        private const int WingSnapFrame = 78;
        private const int EmergeTotal = 106;

        //曼陀罗：抬臂蓄势（72% 静默）→三波花瓣绽放（彼此旋进错角）→收势
        private const int MandalaWindup = 30;
        private const int MandalaWaveGap = 32;
        private const int MandalaWaves = 3;
        internal const int MandalaPetalsPerWave = 6;
        private const int MandalaEnd = MandalaWindup + MandalaWaveGap * MandalaWaves + 34;

        //血矛列雨：举臂→一帧布阵（矛自己演预告与激发）→持姿观礼→收势
        private const int LanceRaiseFrames = 18;
        internal const int LanceCount = 7;
        private const int LanceWatchEnd = 122;
        private const int LanceEnd = 140;

        //缎带：转身后拉蓄势（72% 静默）→一帧挥臂放带→持姿目送→收势
        private const int RibbonWindup = 26;
        private const int RibbonEnd = 118;

        private const int DissolveFrames = 62;

        //==================== 本地表现量（不入同步，节拍闩防快照回卷重播）====================

        private float wingPhase;
        private float wingRate = 1f;
        private int tentaclePhase;
        private int attackCooldown;
        private int attackIndex;
        private int lastSeenState = -1;
        private bool breachDone;
        private bool wingSnapDone;
        private int lastWaveFired = -1;
        private bool lanceArraySpawned;
        private bool ribbonLaunched;
        private bool dissolveSplashed;
        private int danceTimer;
        private float facing = -1f;
        private float wavePulse;

        //血系配色随观看域鬼雨异化冷化，与沉溺/湖藏同族
        private static Color BloodMain => KikasaDomain.CoolTint(new(237, 77, 69), new(126, 158, 164));
        private static Color BloodDeep => KikasaDomain.CoolTint(new(140, 32, 30), new(84, 104, 110));
        private static Color PearlSheen => KikasaDomain.CoolTint(new(246, 170, 150), new(180, 204, 208));
        private static Color MistBlood => KikasaDomain.CoolTint(new(58, 18, 20), new(52, 62, 66));

        /// <summary>血系窄虹彩：hue 只在紫红~红~橙一段窄环上游走，珠光不盖血底</summary>
        internal static Color IridescentTint(float t) {
            Color iri = Main.hslToRgb((0.88f + 0.20f * (t % 1f)) % 1f, 0.85f, 0.60f);
            return Color.Lerp(iri, new Color(170, 195, 200), KikasaDomain.ViewedRainBlend * 0.6f);
        }

        private Player Owner => Main.player[Projectile.owner];

        /// <summary>连续量抖动的确定性相位，各端一致（9.1：不掷 Main.rand）</summary>
        private float Seed => Projectile.identity * 0.7391f;

        //==================== 召唤入口 ====================

        /// <summary>KikasaServantIndex 登记的召唤委托；emergeAt.Y = 湖面</summary>
        internal static void Summon(Player owner, Vector2 emergeAt) {
            if (owner.whoAmI != Main.myPlayer) {
                return;
            }
            int damage = (int)owner.GetTotalDamage(DamageClass.Summon).ApplyTo(ContactDamage);
            Projectile.NewProjectile(owner.GetSource_Misc("KikasaServant"),
                emergeAt + new Vector2(0f, 60f), Vector2.Zero,
                ModContent.ProjectileType<KikasaEmpressServant>(), damage, 4f, owner.whoAmI);
        }

        //==================== 定义 ====================

        public override void SetStaticDefaults() {
            Main.projPet[Projectile.type] = true;
            ProjectileID.Sets.MinionSacrificable[Projectile.type] = false;
            //翅膀×2 缩放远超判定盒，出屏也要画
            ProjectileID.Sets.DrawScreenCheckFluff[Projectile.type] = 480;
        }

        public override void SetDefaults() {
            Projectile.width = 100;
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

        /// <summary>弹幕女皇不以身伤人：接触窗恒关，威胁全部交给图案弹幕</summary>
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
            //还没破水就要收场：什么都没露出来，不走溶解演出
            //否则透明度会从 0 跳到 1，水下凭空闪出一位女皇再化掉
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

            //生命线：湖塌/收域/退水/主人死亡 → 溶解回湖。只有 owner 裁决
            //服务器没有领域状态（恒 Closed 是既定契约），在那边跑这条会把鬼奴当场判死；
            //迟入场的客户端在首份领域快照到达前同样会误判。其余端只跟 owner 的同步包换场
            if (authority && State != StateDissolve && !LakeHealthy(owner, domain)) {
                BeginDissolve();
            }

            Projectile.timeLeft = 180;
            //伤害随召唤加成逐帧刷新（接触窗恒关，只为维持契约形式与后续调参余地）
            Projectile.damage = (int)owner.GetTotalDamage(DamageClass.Summon).ApplyTo(ContactDamage);

            //换场清闩：远端可能靠收包切状态而非本地同拍转场，
            //上一场残闩会吞掉新场的节拍（波拍音、布阵、挥缎、过水线拍）
            if (State != lastSeenState) {
                lastSeenState = State;
                lastWaveFired = -1;
                lanceArraySpawned = false;
                ribbonLaunched = false;
                wavePulse = 0f;
                if (State == StateDissolve) {
                    dissolveSplashed = false;
                }
            }

            StateTimer++;
            switch (State) {
                case StateEmerge: UpdateEmerge(owner, domain); break;
                case StateFollow: UpdateFollow(owner, authority); break;
                case StateMandala: UpdateMandala(owner, authority); break;
                case StateLanceRain: UpdateLanceRain(owner, authority); break;
                case StateRibbon: UpdateRibbon(owner, authority); break;
                case StateDissolve: UpdateDissolve(domain, authority); break;
            }

            UpdatePresentation();
            if (attackCooldown > 0) {
                attackCooldown--;
            }

            float glow = CurrentAlpha() * (0.5f + WingGlow() * 0.35f);
            if (glow > 0.02f) {
                Lighting.AddLight(Projectile.Center, 0.46f * glow, 0.14f * glow, 0.18f * glow);
            }
        }

        private static bool LakeHealthy(Player owner, KikasaDomainPlayer domain)
            => !owner.dead && domain.AnyActive
            && domain.Phase != KikasaDomainPhase.Closing
            && domain.RiseT >= 0.9f;

        //==================== 出水 ====================

        private void UpdateEmerge(Player owner, KikasaDomainPlayer domain) {
            float lakeY = domain.LakeWorldY;
            int t = (int)StateTimer;
            bool viewed = ViewedOwner;

            if (t < OmenFrames) {
                //预兆：湖面自出水点向外绽开三圈成对涟漪，花纹先开，人后到
                Projectile.velocity = Vector2.Zero;
                wingRate = 0.35f;
                if (viewed) {
                    if (t == 6 || t == 16 || t == 26) {
                        int ring = t / 10;
                        float r = 14f + ring * 34f;
                        KikasaDomainDeco.RippleAt(new Vector2(Projectile.Center.X - r, lakeY), 0.45f + ring * 0.2f);
                        KikasaDomainDeco.RippleAt(new Vector2(Projectile.Center.X + r, lakeY), 0.45f + ring * 0.2f);
                        if (ring == 1) {
                            KikasaDomainDeco.RippleAt(new Vector2(Projectile.Center.X, lakeY), 0.75f);
                        }
                        //水下珠光碎星随圈上浮
                        for (int k = 0; k < 3; k++) {
                            PRTLoader.NewParticle<PRT_Sparkle>(
                                new Vector2(Projectile.Center.X + Main.rand.NextFloat(-r, r), lakeY + Main.rand.NextFloat(6f, 30f)),
                                new Vector2(0f, -Main.rand.NextFloat(0.4f, 1.1f)),
                                IridescentTint(Main.rand.NextFloat()) * 0.6f, Main.rand.NextFloat(0.3f, 0.5f))
                                ?.Configure(PearlSheen * 0.5f, 20, 0.02f, 0.6f);
                        }
                    }
                    if (t == 6 || t == 20) {
                        SoundEngine.PlaySound(SoundID.Item161 with { Volume = 0.3f, Pitch = 0.25f, MaxInstances = 2 },
                            new Vector2(Projectile.Center.X, lakeY));
                        SoundEngine.PlaySound(SoundID.Drip with { Volume = 0.4f, Pitch = -0.3f, MaxInstances = 2 },
                            new Vector2(Projectile.Center.X, lakeY));
                    }
                }
                return;
            }

            if (!breachDone) {
                //破水拍：一帧起速 + 浪冠 + 逆血雨登记（水珠倒升是她的开场帷幕）
                breachDone = true;
                Projectile.velocity = new Vector2(0f, -9.5f);
                SoundEngine.PlaySound(SoundID.Item163 with { Volume = 0.55f, Pitch = -0.25f, MaxInstances = 2 }, Projectile.Center);
                if (viewed) {
                    BreachBloom(new Vector2(Projectile.Center.X, lakeY));
                }
            }

            //升起：起速后指数衰减，前快后慢，禁匀速
            Projectile.velocity.Y *= 0.94f;
            Projectile.velocity.X = 0f;
            wingRate = 0.45f;

            //升起期身周持续水珠倒升：湖把它的雨还给天
            if (viewed && t < RiseEnd && t % 3 == 1) {
                Vector2 from = new(Projectile.Center.X + Main.rand.NextFloat(-70f, 70f), lakeY - 2f);
                PRTLoader.NewParticle<PRT_KikasaBloodGlob>(from,
                    new Vector2(Main.rand.NextFloat(-0.3f, 0.3f), -Main.rand.NextFloat(1.6f, 3.4f)),
                    (Main.rand.NextBool(3) ? BloodDeep : BloodMain) * Main.rand.NextFloat(0.45f, 0.6f),
                    Main.rand.NextFloat(0.32f, 0.55f))
                    ?.Configure(Main.rand.Next(26, 40), -0.055f, 0.992f);
            }

            if (!wingSnapDone && t >= WingSnapFrame) {
                //展翅定格拍：一帧硬刹 + 翅辉点亮 + 珠光环绽
                wingSnapDone = true;
                Projectile.velocity *= 0.5f;
                SoundEngine.PlaySound(SoundID.Item160 with { Volume = 0.6f, Pitch = 0.05f, MaxInstances = 2 }, Projectile.Center);
                SoundEngine.PlaySound(SoundID.MaxMana with { Volume = 0.5f, Pitch = -0.4f, MaxInstances = 2 }, Projectile.Center);
                if (viewed) {
                    for (int k = 0; k < 10; k++) {
                        float ang = MathHelper.TwoPi * k / 10f;
                        PRTLoader.NewParticle<PRT_Sparkle>(
                            Projectile.Center + ang.ToRotationVector2() * 46f,
                            ang.ToRotationVector2() * 1.6f,
                            IridescentTint(k / 10f) * 0.7f, Main.rand.NextFloat(0.4f, 0.6f))
                            ?.Configure(PearlSheen * 0.6f, 26, 0.03f, 0.8f);
                    }
                    PRTLoader.NewParticle<PRT_DWave>(Projectile.Center, Vector2.Zero, BloodMain, 0.1f)
                        ?.Configure(new Vector2(1f, 1f), 0f, 0.5f, 14);
                    KikasaDomainDeco.RippleAt(new Vector2(Projectile.Center.X, lakeY), 1.4f);
                    ShakeViewer(2.5f);
                }
            }
            wingRate = t >= WingSnapFrame ? 1.6f : wingRate;

            //面向主人侧，等觉醒
            facing = owner.Center.X < Projectile.Center.X ? -1f : 1f;

            if (t >= EmergeTotal) {
                //转场确定性（纯计时），各端同拍；owner 盖章纠偏
                State = StateFollow;
                StateTimer = 0;
                StateParam = 0;
                attackCooldown = 40;
                Projectile.netUpdate = Main.myPlayer == Projectile.owner;
            }
        }

        /// <summary>破水浪冠·女皇版：对称双侧水花 + 环形涟漪 + 逆升血雨帷幕 + 血雾</summary>
        private void BreachBloom(Vector2 hit) {
            KikasaDomainDeco.RippleAt(hit, 2.2f);
            KikasaDomainDeco.RippleAt(hit + new Vector2(44f, 0f), 1.0f);
            KikasaDomainDeco.RippleAt(hit - new Vector2(44f, 0f), 1.0f);
            KikasaDomainDeco.SplashAt(hit + new Vector2(-14f, 0f), 10);
            KikasaDomainDeco.SplashAt(hit + new Vector2(14f, 0f), 10);

            //逆血雨帷幕：一圈血珠自水面向上倒升，越靠中央升得越高
            for (int i = 0; i < 26; i++) {
                float off = MathHelper.Lerp(-92f, 92f, i / 25f);
                float centered = 1f - MathF.Abs(off) / 92f;
                PRTLoader.NewParticle<PRT_KikasaBloodGlob>(
                    hit + new Vector2(off, -2f),
                    new Vector2(Main.rand.NextFloat(-0.25f, 0.25f), -(1.6f + centered * 2.6f) * Main.rand.NextFloat(0.8f, 1.15f)),
                    (Main.rand.NextBool(3) ? BloodDeep : BloodMain) * Main.rand.NextFloat(0.5f, 0.68f),
                    Main.rand.NextFloat(0.4f, 0.66f))
                    ?.Configure(Main.rand.Next(30, 46), -0.05f, 0.994f);
            }
            //少量正常回落的浪冠珠，给逆雨一个对照底
            for (int i = 0; i < 8; i++) {
                float angle = -MathHelper.Pi * (0.2f + 0.6f * i / 7f);
                PRTLoader.NewParticle<PRT_KikasaBloodGlob>(hit + new Vector2(Main.rand.NextFloat(-20f, 20f), -4f),
                    angle.ToRotationVector2() * Main.rand.NextFloat(2.6f, 5.4f),
                    BloodMain * Main.rand.NextFloat(0.4f, 0.6f),
                    Main.rand.NextFloat(0.4f, 0.7f))?.Configure(Main.rand.Next(22, 34));
            }
            for (int i = 0; i < 3; i++) {
                PRTLoader.NewParticle<PRT_GhostRainMist>(
                    hit + new Vector2(Main.rand.NextFloat(-34f, 34f), -10f),
                    new Vector2(Main.rand.NextFloat(-0.3f, 0.3f), -Main.rand.NextFloat(0.3f, 0.6f)),
                    MistBlood * 0.8f, Main.rand.NextFloat(0.7f, 1.0f))
                    ?.Configure(Main.rand.Next(60, 100));
            }

            SoundEngine.PlaySound(SoundID.SplashWeak with { Volume = 0.95f, Pitch = -0.3f, MaxInstances = 2 }, hit);
            SoundEngine.PlaySound(SoundID.DD2_MonkStaffGroundImpact with { Volume = 0.4f, Pitch = -0.6f, MaxInstances = 1 }, hit);
            ShakeViewer(5f);
        }

        //==================== 跟随（舞姿衔接）====================

        private void UpdateFollow(Player owner, bool authority) {
            int target = FindTarget(owner);
            wingRate = 1f;

            //悬于主人侧上方，呼吸浮动优雅缓慢
            Vector2 anchor = owner.Center + new Vector2(-owner.direction * 104f, -128f);
            anchor.Y += MathF.Sin(Main.GlobalTimeWrappedHourly * 1.7f + Seed) * 7f;
            anchor.X += MathF.Sin(Main.GlobalTimeWrappedHourly * 1.1f + Seed * 2f) * 5f;

            Vector2 to = anchor - Projectile.Center;
            if (to.Length() > 2400f) {
                //跟丢就贴回来，别在半个地图外淌血
                Projectile.Center = anchor;
                Projectile.velocity = Vector2.Zero;
                Projectile.netUpdate = authority;
                return;
            }
            Vector2 desired = to * 0.07f;
            const float maxSpeed = 15f;
            if (desired.Length() > maxSpeed) {
                desired = desired.SafeNormalize(Vector2.Zero) * maxSpeed;
            }
            Projectile.velocity = Vector2.Lerp(Projectile.velocity, desired, 0.11f);

            //有猎物看猎物，闲着看主人
            Vector2 look = target >= 0 ? Main.npc[target].Center : owner.Center;
            facing = look.X < Projectile.Center.X ? -1f : 1f;

            //舞姿衔接：周期小舞步（转身抬臂、裙裾旋开、瓣屑轻散），纯本地表现
            if (!Main.dedServ) {
                if ((int)StateTimer % 110 == 68 && danceTimer <= 0) {
                    danceTimer = 26;
                    if (ViewedOwner) {
                        for (int k = 0; k < 2; k++) {
                            PRTLoader.NewParticle<PRT_KikasaEmpressPetal>(
                                Projectile.Center + new Vector2(Main.rand.NextFloat(-28f, 28f), Main.rand.NextFloat(14f, 34f)),
                                new Vector2(Main.rand.NextFloat(-0.5f, 0.5f), Main.rand.NextFloat(0.2f, 0.7f)),
                                BloodMain * 0.6f, Main.rand.NextFloat(0.4f, 0.6f))
                                ?.Configure(Main.rand.Next(40, 64), Main.rand.NextFloat(0.5f, 1.1f));
                        }
                    }
                }
                //裙摆凝珠滴落
                if (Main.rand.NextBool(22)) {
                    Vector2 hem = Projectile.Center + new Vector2(Main.rand.NextFloat(-26f, 26f), Main.rand.NextFloat(20f, 40f));
                    PRTLoader.NewParticle<PRT_KikasaBloodGlob>(hem,
                        new Vector2(0f, Main.rand.NextFloat(0.5f, 1.1f)),
                        BloodMain * Main.rand.NextFloat(0.4f, 0.55f),
                        Main.rand.NextFloat(0.3f, 0.5f))?.Configure(Main.rand.Next(20, 34), 0.3f);
                }
                //翅尖偶发珠光碎星
                if (Main.rand.NextBool(30)) {
                    Vector2 tip = Projectile.Center + new Vector2(facing * -Main.rand.NextFloat(50f, 90f), Main.rand.NextFloat(-40f, 10f));
                    PRTLoader.NewParticle<PRT_Sparkle>(tip, new Vector2(0f, -0.3f),
                        IridescentTint(Main.rand.NextFloat()) * 0.5f, Main.rand.NextFloat(0.25f, 0.4f))
                        ?.Configure(PearlSheen * 0.4f, 18, 0.02f, 0.5f);
                }
            }

            //出手裁决：三攻轮换；转场规则各端一致，owner 盖章
            if (target >= 0 && attackCooldown <= 0 && StateTimer > 45) {
                attackIndex++;
                State = StateMandala + (attackIndex - 1) % 3;
                StateTimer = 0;
                StateParam = 0;
                Projectile.netUpdate = authority;
            }
        }

        //==================== 攻击一：花瓣扇曼陀罗 ====================

        private void UpdateMandala(Player owner, bool authority) {
            int t = (int)StateTimer;
            int target = FindTarget(owner);
            wingRate = 1.15f;

            if (target < 0 && t <= MandalaWindup) {
                EndAttack(authority, 45);
                return;
            }
            Vector2 aimPos = target >= 0 ? Main.npc[target].Center : Projectile.Center + new Vector2(facing * 300f, 0f);
            facing = aimPos.X < Projectile.Center.X ? -1f : 1f;

            if (t <= MandalaWindup) {
                //蓄势：刹停微微后飘（憋气），身前珠光收拢，72% 后静默
                float k = t / (float)MandalaWindup;
                Projectile.velocity *= 0.86f;
                Projectile.velocity.Y -= 0.03f * k;
                if (t == 3) {
                    SoundEngine.PlaySound(SoundID.DD2_BookStaffCast with { Volume = 0.4f, Pitch = -0.5f, MaxInstances = 2 }, Projectile.Center);
                }
                if (!Main.dedServ && k < 0.72f && t % 3 == 1) {
                    Vector2 from = Projectile.Center + Main.rand.NextVector2Unit() * Main.rand.NextFloat(60f, 120f);
                    PRTLoader.NewParticle<PRT_KikasaBloodGlob>(from,
                        (Projectile.Center - from) * 0.13f,
                        BloodMain * 0.5f, Main.rand.NextFloat(0.3f, 0.5f))?.Configure(9, 0f);
                }
                return;
            }

            //三波绽放：波内 6 瓣等分放射，波间旋进错角、旋向交替，呼吸的玫瑰纹样。
            //rel 从蓄势结束后的第一帧起算，首波在出窗当帧就绽
            int rel = t - MandalaWindup - 1;
            int waveIndex = rel / MandalaWaveGap;
            bool onBeat = rel % MandalaWaveGap == 0;
            if (onBeat && waveIndex < MandalaWaves && lastWaveFired < waveIndex) {
                lastWaveFired = waveIndex;
                StateParam = waveIndex + 1;
                FireMandalaWave(owner, aimPos, waveIndex, authority);
            }

            //波间悬停微稳，留白也要有呼吸
            Projectile.velocity *= 0.9f;

            if (t >= MandalaEnd) {
                EndAttack(authority, 125);
            }
        }

        private void FireMandalaWave(Player owner, Vector2 aimPos, int waveIndex, bool authority) {
            //绽放拍：抬臂 snap + 环形波纹 + 铃音上阶
            wavePulse = 1f;
            SoundEngine.PlaySound(SoundID.Item160 with { Volume = 0.5f, Pitch = -0.1f + waveIndex * 0.12f, MaxInstances = 3 }, Projectile.Center);
            SoundEngine.PlaySound(SoundID.Item95 with { Volume = 0.3f, Pitch = -0.2f, MaxInstances = 3 }, Projectile.Center);
            if (!Main.dedServ) {
                PRTLoader.NewParticle<PRT_DWave>(Projectile.Center, Vector2.Zero, BloodDeep, 0.09f)
                    ?.Configure(new Vector2(1f, 1f), 0f, 0.34f, 10);
                for (int i = 0; i < 6; i++) {
                    float ang = MathHelper.TwoPi * i / 6f + waveIndex * MathHelper.Pi / 6f;
                    PRTLoader.NewParticle<PRT_KikasaBloodGlob>(
                        Projectile.Center + ang.ToRotationVector2() * 20f,
                        ang.ToRotationVector2() * Main.rand.NextFloat(2f, 4f),
                        BloodMain * 0.55f, Main.rand.NextFloat(0.35f, 0.55f))?.Configure(Main.rand.Next(12, 20));
                }
            }
            if (ViewedOwner) {
                ShakeViewer(1.4f);
            }

            //花瓣只在 owner 端生成，spawn 参数自带全部初值（旋向/波序/扇相位）
            if (!authority) {
                return;
            }
            int damage = (int)owner.GetTotalDamage(DamageClass.Summon).ApplyTo(BoltDamage);
            float aimAngle = (aimPos - Projectile.Center).ToRotation();
            //波间错角 30°，旋向交替：三波彼此旋进，图案咬合成玫瑰
            float baseAngle = aimAngle + waveIndex * MathHelper.Pi / 6f;
            float spin = (waveIndex % 2 == 0 ? 1f : -1f) * 0.012f;
            for (int i = 0; i < MandalaPetalsPerWave; i++) {
                float ang = baseAngle + MathHelper.TwoPi * i / MandalaPetalsPerWave;
                Vector2 dir = ang.ToRotationVector2();
                Projectile.NewProjectile(Projectile.GetSource_FromAI(),
                    Projectile.Center + dir * 24f, dir * 7.2f,
                    ModContent.ProjectileType<KikasaEmpressPetal>(), damage, 2.5f, Projectile.owner,
                    spin, waveIndex * MandalaPetalsPerWave + i);
            }
        }

        //==================== 攻击二：圣舞血矛列雨 ====================

        private void UpdateLanceRain(Player owner, bool authority) {
            int t = (int)StateTimer;
            int target = FindTarget(owner);
            wingRate = 1.05f;

            if (target < 0 && t <= LanceRaiseFrames) {
                EndAttack(authority, 45);
                return;
            }
            Vector2 aimPos = target >= 0 ? Main.npc[target].Center : Projectile.Center + new Vector2(facing * 400f, 0f);
            facing = aimPos.X < Projectile.Center.X ? -1f : 1f;

            if (t <= LanceRaiseFrames) {
                //举臂拍：刹停、单臂高举，矛阵将在指尖点亮
                Projectile.velocity *= 0.85f;
                return;
            }

            if (!lanceArraySpawned) {
                //一帧布阵：整排矛虚影浮现在她头顶弧线上，预告与激发交给矛自己演
                lanceArraySpawned = true;
                SoundEngine.PlaySound(SoundID.Item164 with { Volume = 0.5f, Pitch = -0.15f, MaxInstances = 2 }, Projectile.Center);
                if (ViewedOwner) {
                    ShakeViewer(1.2f);
                }
                if (authority) {
                    int damage = (int)owner.GetTotalDamage(DamageClass.Summon).ApplyTo(BoltDamage);
                    NPC npc = target >= 0 ? Main.npc[target] : null;
                    Vector2 lead = npc != null ? npc.Center + npc.velocity * 14f : aimPos;
                    for (int i = 0; i < LanceCount; i++) {
                        //冠状弧阵：以她头顶为心横跨 ±0.9 弧度；落点沿目标横向排开成列
                        float arcAng = -MathHelper.PiOver2 + (i - (LanceCount - 1) * 0.5f) * 0.3f;
                        Vector2 pos = Projectile.Center + new Vector2(facing * 26f, -66f)
                            + arcAng.ToRotationVector2() * 148f;
                        Vector2 aim = lead + new Vector2((i - (LanceCount - 1) * 0.5f) * 92f, 0f);
                        Projectile.NewProjectile(Projectile.GetSource_FromAI(),
                            pos, Vector2.Zero,
                            ModContent.ProjectileType<KikasaEmpressLance>(), damage, 3f, Projectile.owner,
                            i, aim.X, aim.Y);
                    }
                }
            }

            //持姿观礼：矛群逐根点亮与俯冲期间，她保持高举、缓慢漂稳
            Projectile.velocity *= 0.93f;
            //每根矛的激发拍在她的时间轴上都是确定的，逐拍配一记下挥脉冲（纯本地表现）
            int rel = t - LanceRaiseFrames;
            if (rel >= KikasaEmpressLance.IgniteStart
                && (rel - KikasaEmpressLance.IgniteStart) % KikasaEmpressLance.IgniteGap == 0
                && (rel - KikasaEmpressLance.IgniteStart) / KikasaEmpressLance.IgniteGap < LanceCount) {
                wavePulse = MathF.Max(wavePulse, 0.78f);
            }

            if (t >= LanceEnd) {
                EndAttack(authority, 135);
            }
        }

        //==================== 攻击三：血虹缎带 ====================

        private void UpdateRibbon(Player owner, bool authority) {
            int t = (int)StateTimer;
            int target = FindTarget(owner);
            wingRate = 1.1f;

            if (target < 0 && t <= RibbonWindup) {
                EndAttack(authority, 45);
                return;
            }
            Vector2 aimPos = target >= 0 ? Main.npc[target].Center : Projectile.Center + new Vector2(facing * 400f, 0f);
            facing = aimPos.X < Projectile.Center.X ? -1f : 1f;
            Vector2 aim = (aimPos - Projectile.Center).SafeNormalize(Vector2.UnitX * facing);

            if (t <= RibbonWindup) {
                //转身后拉蓄势：pow 憋气，身前珠光收拢，72% 静默
                float k = MathF.Pow(t / (float)RibbonWindup, 4f);
                Projectile.velocity = Vector2.Lerp(Projectile.velocity, -aim * (1.2f + 5f * k), 0.3f);
                if (!Main.dedServ && t < RibbonWindup * 0.72f && t % 3 == 1) {
                    Vector2 hand = Projectile.Center + aim * 34f;
                    Vector2 from = hand + Main.rand.NextVector2Unit() * Main.rand.NextFloat(46f, 90f);
                    PRTLoader.NewParticle<PRT_Sparkle>(from, (hand - from) * 0.14f,
                        IridescentTint(Main.rand.NextFloat()) * 0.5f, Main.rand.NextFloat(0.25f, 0.45f))
                        ?.Configure(PearlSheen * 0.4f, 10, 0f, 0.5f);
                }
                return;
            }

            if (!ribbonLaunched) {
                //挥臂拍：一帧放带 + 后坐 + 双层音（铃与水）
                ribbonLaunched = true;
                wavePulse = 1f;
                Projectile.velocity = -aim * 5f;
                SoundEngine.PlaySound(SoundID.Item161 with { Volume = 0.6f, Pitch = -0.5f, MaxInstances = 2 }, Projectile.Center);
                SoundEngine.PlaySound(SoundID.SplashWeak with { Volume = 0.5f, Pitch = -0.1f, MaxInstances = 2 }, Projectile.Center);
                if (ViewedOwner) {
                    ShakeViewer(2.5f);
                }
                if (authority) {
                    int damage = (int)owner.GetTotalDamage(DamageClass.Summon).ApplyTo(BoltDamage);
                    //垂摆起向：目标在上方先向下卷，反之先向上卷，曲线大开大合
                    float swaySign = aimPos.Y < Projectile.Center.Y ? 1f : -1f;
                    Projectile.NewProjectile(Projectile.GetSource_FromAI(),
                        Projectile.Center + aim * 36f, aim * KikasaEmpressRibbon.LaunchSpeed,
                        ModContent.ProjectileType<KikasaEmpressRibbon>(), damage, 3f, Projectile.owner,
                        aim.ToRotation(), swaySign);
                }
            }

            //目送：缎带扫场期间她缓缓回稳
            Projectile.velocity *= 0.92f;

            if (t >= RibbonEnd) {
                EndAttack(authority, 145);
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
            //翅膀先失色（WingGlow 已按状态归零），扇动渐停
            wingRate = MathF.Max(0.15f, 1f - t * 0.04f);

            if (lakeAlive) {
                //徐徐坠回湖里
                Projectile.velocity.X *= 0.93f;
                Projectile.velocity.Y = MathF.Min(Projectile.velocity.Y + 0.2f, 7f);
            }
            else {
                //湖已不在：原地化瓣
                Projectile.velocity *= 0.9f;
            }

            //化作一场花瓣血雨：从翅与裙裾剥落瓣片，飘坠向湖
            if (!Main.dedServ && t % 2 == 0 && CurrentAlpha() > 0.12f) {
                Vector2 from = Projectile.Center + new Vector2(Main.rand.NextFloat(-70f, 70f), Main.rand.NextFloat(-46f, 36f));
                PRTLoader.NewParticle<PRT_KikasaEmpressPetal>(from,
                    new Vector2(Main.rand.NextFloat(-0.7f, 0.7f), Main.rand.NextFloat(0.4f, 1.2f)),
                    (Main.rand.NextBool(3) ? BloodDeep : BloodMain) * Main.rand.NextFloat(0.5f, 0.7f),
                    Main.rand.NextFloat(0.42f, 0.66f))
                    ?.Configure(Main.rand.Next(44, 70), Main.rand.NextFloat(0.6f, 1.3f));
                if (Main.rand.NextBool(4)) {
                    PRTLoader.NewParticle<PRT_KikasaBloodGlob>(from,
                        new Vector2(Main.rand.NextFloat(-0.5f, 0.5f), Main.rand.NextFloat(1.4f, 2.8f)),
                        BloodMain * 0.5f, Main.rand.NextFloat(0.32f, 0.55f))?.Configure(Main.rand.Next(14, 24));
                }
            }
            if (t == 2) {
                SoundEngine.PlaySound(SoundID.Item163 with { Volume = 0.4f, Pitch = -0.7f, MaxInstances = 2 }, Projectile.Center);
            }

            //过水线拍（一次）
            if (lakeAlive && !dissolveSplashed && Projectile.Center.Y >= lakeY) {
                dissolveSplashed = true;
                StateParam = 1f;
                SoundEngine.PlaySound(SoundID.SplashWeak with { Volume = 0.7f, Pitch = -0.35f, MaxInstances = 2 }, Projectile.Center);
                if (ViewedOwner) {
                    Vector2 hit = new(Projectile.Center.X, lakeY);
                    KikasaDomainDeco.SplashAt(hit, 10);
                    KikasaDomainDeco.RippleAt(hit, 1.3f);
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

        /// <summary>逐帧推进翅膀/触手裙相位与舞步小计时，全是本地表现量</summary>
        private void UpdatePresentation() {
            wingPhase += wingRate;
            tentaclePhase = (int)(wingPhase / 4f) % 8;
            if (danceTimer > 0) {
                danceTimer--;
            }
            if (wavePulse > 0f) {
                wavePulse = MathF.Max(0f, wavePulse - 0.08f);
            }
            //优雅的倾身：随横速轻摆，不做大旋转
            Projectile.rotation = MathHelper.Clamp(Projectile.velocity.X * 0.012f, -0.12f, 0.12f);
        }

        internal bool ViewedOwner
            => KikasaDomain.Viewed != null && KikasaDomain.Viewed.Player.whoAmI == Projectile.owner;

        private static void ShakeViewer(float amount)
            => Main.LocalPlayer?.CWR()?.GetScreenShake(amount);

        //==================== 表现参数 ====================

        private float CurrentAlpha() {
            int t = (int)StateTimer;
            return State switch {
                StateEmerge => t < OmenFrames ? 0f : MathHelper.Clamp((t - OmenFrames) / 5f, 0f, 1f),
                StateDissolve => MathHelper.Clamp((DissolveFrames - t) / 14f, 0f, 1f),
                _ => 1f,
            };
        }

        /// <summary>uForm：1=全血水 0=真身；常态半沉呼吸，出水自上而下凝实</summary>
        private float CurrentForm() {
            int t = (int)StateTimer;
            float steady = 0.34f + MathF.Sin(Main.GlobalTimeWrappedHourly * 2.7f + Seed) * 0.05f;
            return State switch {
                StateEmerge => t < OmenFrames
                    ? 1f
                    : MathHelper.Lerp(1f, steady, SmoothStep01(MathHelper.Clamp((t - OmenFrames) / (float)(RiseEnd - OmenFrames), 0f, 1f))),
                StateDissolve => MathHelper.Clamp(steady + t / (float)DissolveFrames * 0.32f, 0f, 1f),
                _ => steady,
            };
        }

        private float CurrentScanMode() {
            if (State != StateEmerge) {
                return 0f;
            }
            int t = (int)StateTimer;
            if (t <= RiseEnd) {
                return 1f;
            }
            return 1f - MathHelper.Clamp((t - RiseEnd) / (float)(EmergeTotal - RiseEnd), 0f, 1f);
        }

        private float CurrentDissolve()
            => State == StateDissolve
                ? MathF.Pow(MathHelper.Clamp(StateTimer / 44f, 0f, 1f), 0.9f)
                : 0f;

        /// <summary>翅膀辉光包络，她的灵魂：出水展翅点亮、蓄力增辉、溶解最先失色</summary>
        private float WingGlow() {
            int t = (int)StateTimer;
            float breath = 0.55f + 0.12f * MathF.Sin(Main.GlobalTimeWrappedHourly * 2.3f + Seed);
            float value = State switch {
                StateEmerge => t < WingSnapFrame ? 0f : MathHelper.Clamp((t - WingSnapFrame) / 9f, 0f, 1f) * breath / 0.55f * 0.8f,
                StateDissolve => breath * MathF.Max(0f, 1f - t / 12f),
                StateMandala => breath + 0.3f * ChargeLevel(),
                StateRibbon => breath + 0.25f * ChargeLevel(),
                _ => breath,
            };
            //鬼雨异化冷化时珠光收敛
            return value * (1f - KikasaDomain.ViewedRainBlend * 0.5f);
        }

        /// <summary>蓄力进度 0~1：曼陀罗/缎带的收拢流光与翅辉共用</summary>
        private float ChargeLevel() {
            int t = (int)StateTimer;
            if (State == StateMandala) {
                if (t <= MandalaWindup) {
                    return t / (float)MandalaWindup;
                }
                //波期维持余温，末波后 12 帧内熄灭
                return t < MandalaWindup + 1 + (MandalaWaves - 1) * MandalaWaveGap + 12 ? 0.55f : 0f;
            }
            if (State == StateRibbon && t <= RibbonWindup) {
                return t / (float)RibbonWindup;
            }
            return 0f;
        }

        private float BodyScale() => 1f + wavePulse * 0.06f;

        /// <summary>左右臂帧：0 垂落 / 1 轻抬 / 2 半举 / 3 高举 / 5 下挥（原版帧语义）</summary>
        private void GetArmFrames(out int armLeft, out int armRight) {
            int t = (int)StateTimer;
            armLeft = 0;
            armRight = 0;
            switch (State) {
                case StateEmerge:
                    if (t >= WingSnapFrame) {
                        armLeft = armRight = t < WingSnapFrame + 16 ? 3 : 1;
                    }
                    else if (t >= OmenFrames) {
                        armLeft = armRight = 2;
                    }
                    break;
                case StateFollow:
                    if (danceTimer > 0) {
                        armLeft = danceTimer > 13 ? 1 : 2;
                    }
                    break;
                case StateMandala:
                    if (t <= MandalaWindup) {
                        armLeft = armRight = t < MandalaWindup / 2 ? 2 : 3;
                    }
                    else {
                        //波拍瞬间双臂下挥 snap，随即回举
                        armLeft = armRight = wavePulse > 0.55f ? 5 : 3;
                    }
                    if (t > MandalaEnd - 20) {
                        armLeft = armRight = 2;
                    }
                    break;
                case StateLanceRain:
                    armRight = t < LanceRaiseFrames / 2 ? 2 : 3;
                    if (wavePulse > 0.55f) {
                        armRight = 5;
                    }
                    if (t > LanceWatchEnd) {
                        armRight = 2;
                    }
                    break;
                case StateRibbon:
                    if (t <= RibbonWindup) {
                        armLeft = 2;
                    }
                    else {
                        armLeft = wavePulse > 0.4f ? 5 : 3;
                        if (t > RibbonEnd - 24) {
                            armLeft = 2;
                        }
                    }
                    break;
            }
        }

        private static float SmoothStep01(float t) => t * t * (3f - 2f * t);

        //==================== 绘制 ====================

        public override bool PreDraw(ref Color lightColor) {
            Main.instance.LoadNPC(NPCID.HallowBoss);
            Texture2D body = TextureAssets.Npc[NPCID.HallowBoss]?.Value;
            if (body == null) {
                return false;
            }
            float alpha = CurrentAlpha();
            SpriteBatch sb = Main.spriteBatch;

            if (alpha > 0.01f) {
                DrawLayeredBody(sb, body, alpha);
            }
            DrawGlow(sb, alpha);
            return false;
        }

        /// <summary>
        /// 多层女皇（原版 DrawNPCDirect_HallowBoss 布局的血水版）：
        /// 后翅(Extra159 ×2) → 翅底血水(Extra157 ×2) → 翅辉珠光(原版 HallowBoss 渐变着色器) →
        /// 触手裙(Extra187) → 本体(二阶段帧) → 左右臂(Extra158/160)。
        /// 血水各层同一 Immediate 批逐层设参过 KikasaItemForm，着色器为空回退 CPU 血染
        /// </summary>
        private void DrawLayeredBody(SpriteBatch sb, Texture2D body, float alpha) {
            Texture2D wingsBack = TextureAssets.Extra[159]?.Value;
            Texture2D wings = TextureAssets.Extra[157]?.Value;
            Texture2D armsLeft = TextureAssets.Extra[158]?.Value;
            Texture2D armsRight = TextureAssets.Extra[160]?.Value;
            Texture2D tentacles = TextureAssets.Extra[187]?.Value;
            if (wingsBack == null || wings == null || armsLeft == null || armsRight == null || tentacles == null) {
                return;
            }

            int bodyFrameCount = Math.Max(Main.npcFrameCount[NPCID.HallowBoss], 1);
            int bodyFrameH = body.Height / bodyFrameCount;
            //血湖复制体常驻二阶段的华彩形态
            Rectangle bodyFrame = new(0, bodyFrameCount > 1 ? bodyFrameH : 0, body.Width, bodyFrameH);
            Vector2 halfSize = bodyFrame.Size() * 0.5f;

            int wingFrame = (int)(wingPhase / 4f) % 11;
            Rectangle wingRect = wingsBack.Frame(1, 11, 0, wingFrame);
            GetArmFrames(out int armLeftFrame, out int armRightFrame);
            Rectangle armLeftRect = armsLeft.Frame(1, 7, 0, armLeftFrame);
            Rectangle armRightRect = armsRight.Frame(1, 7, 0, armRightFrame);
            Rectangle tentacleRect = tentacles.Frame(1, 8, 0, tentaclePhase);

            Vector2 center = Projectile.Center - Main.screenPosition;
            SpriteEffects flip = facing > 0 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
            float scale = BodyScale();
            float rot = Projectile.rotation;

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
                form.Parameters["uDissolve"]?.SetValue(CurrentDissolve());
                form.Parameters["uScanMode"]?.SetValue(CurrentScanMode());
            }

            float baseForm = CurrentForm();
            Color white = new(255, 255, 255, (byte)(alpha * 255f));

            void DrawFormLayer(Texture2D tex, Rectangle frame, Vector2 origin, float layerScale, float formBias, float seedBias) {
                Color color;
                if (shaderOk) {
                    form.Parameters["uSeed"]?.SetValue(Seed + seedBias);
                    form.Parameters["uForm"]?.SetValue(MathHelper.Clamp(baseForm + formBias, 0f, 1f));
                    form.Parameters["uUvRect"]?.SetValue(new Vector4(
                        frame.X / (float)tex.Width, frame.Y / (float)tex.Height,
                        frame.Width / (float)tex.Width, frame.Height / (float)tex.Height));
                    form.Parameters["uTexel"]?.SetValue(new Vector2(1f / tex.Width, 1f / tex.Height));
                    form.Parameters["uAspect"]?.SetValue(frame.Width / (float)frame.Height);
                    form.CurrentTechnique.Passes[0].Apply();
                    color = white;
                }
                else {
                    //无着色器回退：CPU 血染
                    color = Color.Lerp(Color.White, BloodMain, 0.55f) * alpha;
                }
                sb.Draw(tex, center, frame, color, rot, origin, layerScale, flip, 0f);
            }

            //后翅：血水化最重，×2 缩放是原版贴图设计
            DrawFormLayer(wingsBack, wingRect, wingRect.Size() * 0.5f, scale * 2f, 0.16f, 3.1f);
            //翅底：翅辉的血色骨架
            DrawFormLayer(wings, wingRect, wingRect.Size() * 0.5f, scale * 2f, 0.10f, 5.7f);

            //翅辉珠光：原版 HallowBoss 渐变着色器压低透明度，血底之上极克制的虹彩，她的灵魂
            float wingGlow = WingGlow();
            if (wingGlow > 0.02f
                && GameShaders.Misc.TryGetValue("HallowBoss", out MiscShaderData hallowShader)) {
                Color glowColor = Color.White * (0.34f * wingGlow * alpha);
                glowColor.A = 0;
                DrawData wingData = new(wings, center, wingRect, glowColor, rot,
                    wingRect.Size() * 0.5f, scale * 2f, flip);
                hallowShader.Apply(wingData);
                wingData.Draw(sb);
                //原版着色器把自家渐变条绑上了 Textures[1]，后续血水层要把噪声图抢回来
                if (shaderOk) {
                    Main.instance.GraphicsDevice.Textures[1] = noise;
                    Main.instance.GraphicsDevice.SamplerStates[1] = SamplerState.LinearWrap;
                }
            }

            //触手裙 → 本体 → 双臂（帧 5 的臂后画压上层，原版层序）
            DrawFormLayer(tentacles, tentacleRect, halfSize, scale, 0.08f, 8.3f);
            DrawFormLayer(body, bodyFrame, halfSize, scale, 0f, 0f);
            int leftLayer = armLeftFrame == 5 ? 1 : 0;
            int rightLayer = armRightFrame == 5 ? 1 : 0;
            for (int layer = 0; layer < 2; layer++) {
                if (layer == leftLayer) {
                    DrawFormLayer(armsLeft, armLeftRect, armLeftRect.Size() * 0.5f, scale, 0.04f, 11.9f);
                }
                if (layer == rightLayer) {
                    DrawFormLayer(armsRight, armRightRect, armRightRect.Size() * 0.5f, scale, 0.04f, 13.7f);
                }
            }

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
        }

        /// <summary>加色层：预兆水下血光 / 裙裾虹彩幻影 / 蓄力花位预告环 / 展翅辉闪</summary>
        private void DrawGlow(SpriteBatch sb, float alpha) {
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            Texture2D skirt = TextureAssets.Extra[188]?.Value;
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
            Vector2 center = Projectile.Center - Main.screenPosition;

            //预兆：水下虹光自深处上浮，花纹涟漪的光底
            if (State == StateEmerge && t < OmenFrames) {
                float ot = MathHelper.Clamp(t / (float)OmenFrames, 0f, 1f);
                float ease = 1f - (1f - ot) * (1f - ot);
                Vector2 pos = new(Projectile.Center.X, domain.LakeWorldY + MathHelper.Lerp(48f, 8f, ease));
                float r = 40f + 26f * ease;
                EnsureBegin();
                sb.Draw(glow, pos - Main.screenPosition, null, PearlSheen * (0.34f * ease), 0f,
                    gOrigin, new Vector2(r * 2.8f / glow.Width, r * 1.0f / glow.Height), SpriteEffects.None, 0f);
                sb.Draw(glow, pos - Main.screenPosition, null, IridescentTint(ot * 0.8f) * (0.18f * ease), 0f,
                    gOrigin, new Vector2(r * 2.0f / glow.Width, r * 0.7f / glow.Height), SpriteEffects.None, 0f);
            }

            if (alpha <= 0.05f) {
                if (begun) {
                    RestoreBatch(sb);
                }
                return;
            }

            //裙裾虹彩幻影：四向偏移的原版语法，血虹色极克制
            float wingGlow = WingGlow();
            if (skirt != null && wingGlow > 0.05f) {
                EnsureBegin();
                float pulse = MathF.Sin(Main.GlobalTimeWrappedHourly * MathHelper.Pi) * 0.5f + 0.5f;
                Vector2 halfSkirt = skirt.Size() * 0.5f;
                SpriteEffects flip = facing > 0 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
                //舞步/波拍时裙裾旋速加快
                float spinBoost = 1f + (danceTimer > 0 ? 1.6f : 0f) + wavePulse * 1.2f;
                for (int m = 0; m < 4; m++) {
                    float ang = m * MathHelper.PiOver2 + MathHelper.PiOver4
                        + Main.GlobalTimeWrappedHourly * 0.9f * spinBoost;
                    Vector2 off = ang.ToRotationVector2() * MathHelper.Lerp(2f, 8f, pulse);
                    //真加色批源因子是 SourceAlpha：A 置零=整层不画，A 必须随强度走
                    Color iri = IridescentTint(m * 0.25f + Main.GlobalTimeWrappedHourly * 0.08f);
                    sb.Draw(skirt, center + off, null, iri * (0.20f * wingGlow * alpha),
                        Projectile.rotation, halfSkirt, BodyScale(), flip, 0f);
                }
            }

            //曼陀罗蓄力：六点花位预告环，花开在哪一瓣，先亮哪一点
            float charge = ChargeLevel();
            if (State == StateMandala && charge > 0.03f && charge < 1f) {
                EnsureBegin();
                float ringR = MathHelper.Lerp(86f, 30f, charge);
                for (int i = 0; i < MandalaPetalsPerWave; i++) {
                    float ang = MathHelper.TwoPi * i / MandalaPetalsPerWave
                        + Main.GlobalTimeWrappedHourly * 1.4f + Seed;
                    Vector2 pos = center + ang.ToRotationVector2() * ringR;
                    float a = charge * 0.5f * (0.7f + 0.3f * MathF.Sin(Main.GlobalTimeWrappedHourly * 6f + i));
                    sb.Draw(glow, pos, null, BloodMain * a, ang,
                        gOrigin, new Vector2(22f / glow.Width * 2f, 7f / glow.Height), SpriteEffects.None, 0f);
                    sb.Draw(glow, pos, null, IridescentTint(i / 6f) * (a * 0.5f), 0f,
                        gOrigin, new Vector2(10f / glow.Width * 2f), SpriteEffects.None, 0f);
                }
                sb.Draw(glow, center, null, PearlSheen * (0.4f * charge), 0f,
                    gOrigin, new Vector2((12f + 20f * charge) * 2f / glow.Width), SpriteEffects.None, 0f);
            }

            //缎带蓄力：执手侧珠光积聚
            if (State == StateRibbon && charge > 0.03f) {
                EnsureBegin();
                Vector2 hand = center + new Vector2(facing * 30f, -4f);
                float r = 8f + 16f * charge;
                sb.Draw(glow, hand, null, PearlSheen * (0.5f * charge), 0f,
                    gOrigin, new Vector2(r * 2f / glow.Width), SpriteEffects.None, 0f);
            }

            //展翅拍余辉：短促的一圈亮
            if (State == StateEmerge && t >= WingSnapFrame) {
                float f = MathHelper.Clamp((t - WingSnapFrame) / (float)(EmergeTotal - WingSnapFrame), 0f, 1f);
                float a = MathF.Sin(f * MathHelper.Pi) * 0.5f;
                if (a > 0.02f) {
                    EnsureBegin();
                    sb.Draw(glow, center, null, PearlSheen * a, 0f,
                        gOrigin, new Vector2((60f + 50f * f) * 2f / glow.Width, (40f + 30f * f) * 2f / glow.Height), SpriteEffects.None, 0f);
                }
            }

            if (begun) {
                RestoreBatch(sb);
            }
        }

        private static void RestoreBatch(SpriteBatch sb) {
            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
        }

        //==================== 谢幕 ====================

        public override void OnKill(int timeLeft) {
            //谢幕残瓣：溶解尾拍或异常移除都留一场小小的花瓣血雨
            if (Main.dedServ) {
                return;
            }
            for (int i = 0; i < 8; i++) {
                PRTLoader.NewParticle<PRT_KikasaEmpressPetal>(
                    Projectile.Center + Main.rand.NextVector2Circular(40f, 30f),
                    new Vector2(Main.rand.NextFloat(-0.8f, 0.8f), Main.rand.NextFloat(0.5f, 1.6f)),
                    (Main.rand.NextBool(3) ? BloodDeep : BloodMain) * 0.6f,
                    Main.rand.NextFloat(0.4f, 0.62f))
                    ?.Configure(Main.rand.Next(40, 64), Main.rand.NextFloat(0.6f, 1.2f));
            }
            for (int i = 0; i < 6; i++) {
                PRTLoader.NewParticle<PRT_KikasaBloodGlob>(
                    Projectile.Center + Main.rand.NextVector2Circular(30f, 30f),
                    new Vector2(Main.rand.NextFloat(-1f, 1f), Main.rand.NextFloat(0.5f, 2.4f)),
                    BloodMain * 0.5f, Main.rand.NextFloat(0.35f, 0.6f))
                    ?.Configure(Main.rand.Next(14, 26));
            }
            PRTLoader.NewParticle<PRT_GhostRainMist>(Projectile.Center,
                new Vector2(0f, -0.25f), MistBlood * 0.7f, Main.rand.NextFloat(0.6f, 0.9f))
                ?.Configure(Main.rand.Next(50, 80));
        }
    }
}
