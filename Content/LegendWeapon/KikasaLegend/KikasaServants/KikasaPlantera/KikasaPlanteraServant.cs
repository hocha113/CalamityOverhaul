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

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaServants.KikasaPlantera
{
    /// <summary>
    /// 鬼奴·湖水版世纪之花。全鬼奴中唯一的锚定炮台：花体悬在湖面上方一点点
    /// 随水波轻晃，三根钩须呈扇形垂下扎进水面以下——它锚在湖里，湖是它的土壤。
    /// 出水为钩须逐根探出扎位、链身绷直后把花体从水里拽起；跟随即搬家演出
    /// （拔须→低掠水面→扎回），不做漂移追击。攻击三式：湖面藤袭（目标脚下
    /// 涟漪预告→血藤破水鞭笞，独立弹幕）、种子机关枪（口部后坐、弹壳水珠）、
    /// 钩须皮筋弹性前扑咬（拉伸 tell→弹弓弹射→被链拽回，够不着就不扑）。
    /// 联机同克眼契约：状态机走 ai[0..2]，owner 转场盖 netUpdate 章，
    /// 钩须姿态全参数化各端本地重建，节拍闩防快照回卷，生命线只有 owner 判
    /// </summary>
    internal class KikasaPlanteraServant : ModProjectile, IKikasaServant
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        //==================== 可调基数（占位初值，验收再调）====================

        /// <summary>前扑咬接触/湖面藤鞭基伤（召唤加成前）</summary>
        internal const int BiteDamage = 680;

        /// <summary>种子单发基伤（召唤加成前）</summary>
        internal const int SeedDamage = 360;

        //==================== 形体几何 ====================

        /// <summary>花体悬高（体心到湖面）</summary>
        private const float HoverHeight = 98f;

        /// <summary>钩须扎根深度（水面下，镜面遮挡，只露链与入水涟漪）</summary>
        private const float RootDepth = 46f;

        /// <summary>钩须皮筋极限 = 前扑最大射程（诚实约束：够不着就不扑）</summary>
        internal const float ChainReach = 440f;

        /// <summary>主人横向走远多少触发搬家</summary>
        private const float RelocateTrigger = 720f;

        //==================== 状态 ====================

        private const int StateEmerge = 0;
        private const int StateAnchored = 1;
        private const int StateRelocate = 2;
        private const int StateVine = 3;
        private const int StateSeed = 4;
        private const int StatePounce = 5;
        private const int StateDissolve = 6;

        private int State { get => (int)Projectile.ai[0]; set => Projectile.ai[0] = value; }
        private ref float StateTimer => ref Projectile.ai[1];
        /// <summary>状态内子参数：搬家/前扑=阶段号，藤袭=本轮藤数（1 或 3）</summary>
        private ref float StateParam => ref Projectile.ai[2];

        //==================== 时序 ====================

        //出水：预兆聚涟漪→钩须逐根探出扎位→链绷直静默→拽起破水→弹性落定绽放
        private const int OmenFrames = 22;
        private const int HookProbeGap = 13;
        private const int HookProbeRise = 10;
        private const int HookProbeHang = 5;
        private const int HookProbeDur = 22;
        private const int TautFrame = OmenFrames + HookProbeGap * 2 + HookProbeDur + 4;
        private const int HoistFrame = TautFrame + 4;
        private const int RiseEnd = 104;
        private const int AwakenFrame = 112;
        private const int EmergeTotal = 124;

        //搬家：拔须→低掠→扎回
        private const int UprootGap = 9;
        private const int UprootDur = 12;
        private const int UprootTotal = UprootGap * 2 + UprootDur + 4;
        private const int TravelTimeout = 170;
        private const int RootStart = 8;
        private const int RootGap = 9;
        private const int RootDur = 10;
        private const int RootTotal = RootStart + RootGap * 2 + RootDur + 6;

        //藤袭：抬冠蓄势→逐拍点穴施藤→收势
        private const int VineStateTotal = 56;
        private static readonly int[] VineCastBeats = { 14, 24, 34 };

        //种子机关枪：瞄准蓄口（72% 后静默）→连发→收口回摆
        private const int SeedAimFrames = 14;
        private const int SeedShotGap = 3;
        private const int SeedShotCount = 12;
        private const int SeedFireEnd = SeedAimFrames + SeedShotGap * SeedShotCount;
        private const int SeedRecoverEnd = SeedFireEnd + 14;

        //前扑：迟发后拉拉弓→一帧弹射→皮筋拽回弹性回摆
        private const int PounceWindup = 26;
        private const int PounceFlightMax = 20;
        private const int PounceSpringFrames = 42;

        private const int DissolveFrames = 54;
        private const int HookReleaseGap = 7;

        //==================== 本地表现量（不入同步）====================

        private int frameTick;
        private int frameIndex;
        /// <summary>二阶段獠口帧：仅前扑愤怒收招期间亮相</summary>
        private bool jawOpen;
        private int attackCooldown;
        private int attackIndex;
        private int relocateRest;
        private int lastSeenState = -1;
        private int lastPhaseSeen;
        /// <summary>通用节拍位掩码，换场/换阶段清零，防快照回卷重播</summary>
        private int beatMask;
        private int lastVineCast = -1;
        private int lastShotFired = -1;
        private bool dissolveSplashed;
        /// <summary>搬家低掠的停靠侧（进相位时定夺，避免逐帧翻侧抖动）</summary>
        private float relocateSide;
        /// <summary>链体崩弹余振 0..1，绘制层横振幅来源</summary>
        private float chainTwang;

        private bool poseInit;
        /// <summary>锚定横位：各端由位置同步 + 确定性规则本地重建</summary>
        private float anchorX;

        //钩须姿态缓存：AI 每帧参数化重算，绘制与涟漪复用
        private readonly Vector2[] hookPos = new Vector2[3];
        private readonly float[] hookRot = new float[3];
        private readonly float[] hookAlpha = new float[3];
        private readonly bool[] hookRooted = new bool[3];
        private readonly float[] hookDissolve = new float[3];
        /// <summary>溶解入场帧的钩须位置快照：搬家/出场半途遣返也从实际所在处松脱，不瞬移回扎根位</summary>
        private readonly Vector2[] hookAtDissolve = new Vector2[3];
        /// <summary>三须扇形横位基准</summary>
        private static readonly float[] FanBase = { -158f, -12f, 150f };
        /// <summary>逐根动作的出场次序（中→左→右），slot = 时间席位</summary>
        private static readonly int[] ProbeSlot = { 1, 0, 2 };

        //==================== 配色（血系随观看域鬼雨异化冷化）====================

        internal static Color BloodMain => KikasaDomain.CoolTint(new(237, 77, 69), new(126, 158, 164));
        internal static Color BloodDeep => KikasaDomain.CoolTint(new(140, 32, 30), new(84, 104, 110));
        internal static Color FoamGlow => KikasaDomain.CoolTint(new(246, 133, 112), new(176, 200, 204));
        internal static Color MistBlood => KikasaDomain.CoolTint(new(58, 18, 20), new(52, 62, 66));
        /// <summary>世纪之花的花瓣粉，只做次要点缀层</summary>
        internal static Color PetalPink => KikasaDomain.CoolTint(new(244, 106, 150), new(162, 180, 192));

        private Player Owner => Main.player[Projectile.owner];

        /// <summary>连续量抖动的确定性相位，各端一致（9.1：不掷 Main.rand）</summary>
        private float Seed => Projectile.identity * 0.7391f;

        //==================== 召唤入口 ====================

        /// <summary>KikasaServantIndex 登记的召唤委托；emergeAt.Y = 湖面</summary>
        internal static void Summon(Player owner, Vector2 emergeAt) {
            if (owner.whoAmI != Main.myPlayer) {
                return;
            }
            int damage = (int)owner.GetTotalDamage(DamageClass.Summon).ApplyTo(BiteDamage);
            //生成在出水点正下方水下，钩须先行探出
            Projectile.NewProjectile(owner.GetSource_Misc("KikasaServant"),
                emergeAt + new Vector2(0f, 64f), Vector2.Zero,
                ModContent.ProjectileType<KikasaPlanteraServant>(), damage, 8f, owner.whoAmI);
        }

        //==================== 定义 ====================

        public override void SetStaticDefaults() {
            Main.projPet[Projectile.type] = true;
            ProjectileID.Sets.MinionSacrificable[Projectile.type] = false;
            //钩须链最远拉到皮筋极限之外，体心出屏也要画
            ProjectileID.Sets.DrawScreenCheckFluff[Projectile.type] = 800;
        }

        public override void SetDefaults() {
            Projectile.width = 96;
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

        /// <summary>接触伤害只开在前扑飞行窗，与可见的弹射严格对齐</summary>
        public override bool? CanDamage()
            => State == StatePounce && (int)StateParam == 1 ? null : false;

        public override bool? CanCutTiles() => false;

        //==================== 遣返 ====================

        public bool IsDismissing => State == StateDissolve;

        public void BeginDismiss() {
            if (Main.myPlayer == Projectile.owner && State != StateDissolve) {
                BeginDissolve();
            }
        }

        private void BeginDissolve() {
            //钩须还没探出水面就要收场：什么都没露，不演谢幕
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

            //生命线：只有 owner 裁决——服务器无领域状态（既定契约），
            //迟入场客户端首份快照前也会误判；其余端只跟 owner 的同步包换场
            if (authority && State != StateDissolve && !LakeHealthy(owner, domain)) {
                BeginDissolve();
            }

            Projectile.timeLeft = 180;
            Projectile.damage = (int)owner.GetTotalDamage(DamageClass.Summon).ApplyTo(BiteDamage);

            if (!poseInit) {
                poseInit = true;
                anchorX = Projectile.Center.X;
            }

            //换场清闩：远端可能靠收包切状态而非本地同拍转场；
            //迟入场时静默补齐已过节拍，防同帧连环重播
            if (State != lastSeenState) {
                lastSeenState = State;
                lastPhaseSeen = (int)StateParam;
                beatMask = 0;
                lastVineCast = -1;
                lastShotFired = -1;
                if (State == StateDissolve) {
                    dissolveSplashed = false;
                    for (int i = 0; i < 3; i++) {
                        hookAtDissolve[i] = hookPos[i];
                    }
                }
                if (StateTimer > 3f) {
                    SyncLatchesQuietly();
                }
            }
            //换阶段清闩（搬家/前扑用 StateParam 作阶段号）；快照跨阶段跳变同样静默补齐
            if ((State == StateRelocate || State == StatePounce) && (int)StateParam != lastPhaseSeen) {
                lastPhaseSeen = (int)StateParam;
                beatMask = 0;
                if (StateTimer > 3f) {
                    SyncLatchesQuietly();
                }
            }

            StateTimer++;
            switch (State) {
                case StateEmerge: UpdateEmerge(domain); break;
                case StateAnchored: UpdateAnchored(owner, domain, authority); break;
                case StateRelocate: UpdateRelocate(owner, domain, authority); break;
                case StateVine: UpdateVine(owner, domain, authority); break;
                case StateSeed: UpdateSeed(owner, domain, authority); break;
                case StatePounce: UpdatePounce(owner, domain, authority); break;
                case StateDissolve: UpdateDissolve(domain, authority); break;
            }

            UpdateHookPoses(domain);
            UpdateFrames();
            UpdateIdleFX(domain);
            if (attackCooldown > 0) {
                attackCooldown--;
            }
            if (relocateRest > 0) {
                relocateRest--;
            }
            chainTwang *= 0.9f;

            float glow = CurrentAlpha() * 0.5f;
            if (glow > 0.02f) {
                Lighting.AddLight(Projectile.Center, 0.4f * glow, 0.1f * glow, 0.1f * glow);
            }
        }

        private static bool LakeHealthy(Player owner, KikasaDomainPlayer domain)
            => !owner.dead && domain.AnyActive
            && domain.Phase != KikasaDomainPhase.Closing
            && domain.RiseT >= 0.9f;

        /// <summary>节拍闩：首次到达该位返回 true，重复/回卷不再触发</summary>
        private bool PlayOnce(int bit) {
            int flag = 1 << bit;
            if ((beatMask & flag) != 0) {
                return false;
            }
            beatMask |= flag;
            return true;
        }

        /// <summary>
        /// 迟入场静默补齐已过节拍：只预标记比当前计时旧 12 帧以上的拍——
        /// 正常联机包迟到几帧的拍照常补播，真正的中途入场才整段静默，
        /// 防止首帧连环重播开场炮
        /// </summary>
        private void SyncLatchesQuietly() {
            int cutoff = (int)StateTimer - 12;
            if (cutoff < 0) {
                return;
            }
            void MarkIfOld(int beatFrame, int bit) {
                if (beatFrame <= cutoff) {
                    beatMask |= 1 << bit;
                }
            }
            int phase = (int)StateParam;
            switch (State) {
                case StateEmerge:
                    for (int i = 0; i < 3; i++) {
                        int start = OmenFrames + ProbeSlot[i] * HookProbeGap;
                        MarkIfOld(start + 5, i * 2);
                        MarkIfOld(start + 20, i * 2 + 1);
                    }
                    MarkIfOld(TautFrame, 6);
                    MarkIfOld(HoistFrame, 7);
                    MarkIfOld(AwakenFrame, 8);
                    break;
                case StateRelocate:
                    for (int i = 0; i < 3; i++) {
                        if (phase == 0) {
                            MarkIfOld(ProbeSlot[i] * UprootGap + 3, i);
                        }
                        else if (phase == 2) {
                            MarkIfOld(RootStart + ProbeSlot[i] * RootGap + RootDur - 2, i);
                        }
                    }
                    break;
                case StatePounce:
                    if (phase == 0) {
                        MarkIfOld(8, 4);
                        MarkIfOld(19, 5);
                    }
                    else if (phase == 1) {
                        MarkIfOld(0, 0);
                        MarkIfOld(6, 1);
                    }
                    else {
                        MarkIfOld(4, 2);
                        MarkIfOld(10, 3);
                    }
                    break;
                case StateVine:
                    for (int c = 0; c < VineCastBeats.Length; c++) {
                        if (VineCastBeats[c] <= cutoff) {
                            lastVineCast = c;
                        }
                    }
                    break;
                case StateSeed:
                    if (cutoff > SeedAimFrames) {
                        lastShotFired = Math.Min((cutoff - SeedAimFrames) / SeedShotGap, SeedShotCount - 1);
                    }
                    break;
                case StateDissolve:
                    for (int i = 0; i < 3; i++) {
                        MarkIfOld(ProbeSlot[i] * HookReleaseGap + 2, i);
                    }
                    break;
            }
        }

        //==================== 出水：根须先行 ====================

        private void UpdateEmerge(KikasaDomainPlayer domain) {
            float lakeY = domain.LakeWorldY;
            int t = (int)StateTimer;
            bool viewed = ViewedOwner;

            //拽起前花体在水下待命，缓慢上浮蓄势
            if (t < HoistFrame) {
                float wantY = lakeY + MathHelper.Lerp(64f, 38f, MathHelper.Clamp(t / (float)TautFrame, 0f, 1f));
                Projectile.velocity = new Vector2(0f, (wantY - Projectile.Center.Y) * 0.08f);
            }

            if (t < OmenFrames) {
                //预兆：出水点涟漪收拢 + 三处扎根位各自小圈——湖在替它量地
                if (viewed) {
                    if (t % 6 == 2) {
                        float converge = 1f - t / (float)OmenFrames;
                        float side = t / 6 % 2 == 0 ? 1f : -1f;
                        KikasaDomainDeco.RippleAt(
                            new Vector2(anchorX + side * converge * 58f, lakeY),
                            0.38f + (1f - converge) * 0.5f);
                    }
                    if (t % 8 == 5) {
                        int i = t / 8 % 3;
                        KikasaDomainDeco.RippleAt(new Vector2(RootX(i), lakeY), 0.24f);
                    }
                    if (t == 4 || t == 15) {
                        SoundEngine.PlaySound(SoundID.Drip with {
                            Volume = 0.45f,
                            Pitch = t == 4 ? -0.5f : -0.2f,
                            MaxInstances = 2
                        }, new Vector2(anchorX, lakeY));
                    }
                }
                return;
            }

            //钩须逐根破水/扎位节拍（姿态本身由 UpdateHookPoses 参数化推进）
            for (int i = 0; i < 3; i++) {
                int start = OmenFrames + ProbeSlot[i] * HookProbeGap;
                if (t >= start + 5 && PlayOnce(i * 2)) {
                    //探出拍：钩爪破水而出
                    SoundEngine.PlaySound(SoundID.SplashWeak with { Volume = 0.4f, Pitch = -0.15f + i * 0.06f, MaxInstances = 3 },
                        new Vector2(RootX(i), lakeY));
                    if (viewed) {
                        KikasaDomainDeco.RippleAt(new Vector2(RootX(i), lakeY), 0.6f);
                        KikasaDomainDeco.SplashAt(new Vector2(RootX(i), lakeY), 4);
                    }
                }
                if (t >= start + 20 && PlayOnce(i * 2 + 1)) {
                    //扎位拍：倒转下刺回水，闷钉一声
                    SoundEngine.PlaySound(SoundID.SplashWeak with { Volume = 0.5f, Pitch = -0.5f, MaxInstances = 3 },
                        new Vector2(RootX(i), lakeY));
                    SoundEngine.PlaySound(SoundID.Drip with { Volume = 0.55f, Pitch = -0.85f, MaxInstances = 3 },
                        new Vector2(RootX(i), lakeY));
                    if (viewed) {
                        KikasaDomainDeco.RippleAt(new Vector2(RootX(i), lakeY), 0.9f);
                        KikasaDomainDeco.SplashAt(new Vector2(RootX(i), lakeY), 6);
                        ShakeViewer(0.8f);
                    }
                }
            }

            if (t >= TautFrame && PlayOnce(6)) {
                //绷直拍：三链同时吃劲，纤维吱嘎——拽起前最后一口气
                chainTwang = 0.7f;
                SoundEngine.PlaySound(SoundID.NPCHit7 with { Volume = 0.45f, Pitch = -0.85f, MaxInstances = 2 }, Projectile.Center);
                if (viewed) {
                    for (int i = 0; i < 3; i++) {
                        KikasaDomainDeco.RippleAt(new Vector2(RootX(i), lakeY), 0.42f);
                    }
                }
            }

            if (t >= HoistFrame && PlayOnce(7)) {
                //拽起破水拍：一帧起速 + 浪冠 + 尖啸，三链吃满冲量嗡一声
                Projectile.velocity = new Vector2(0f, -12.5f);
                chainTwang = 1f;
                SoundEngine.PlaySound(SoundID.Roar with { Volume = 0.55f, Pitch = 0.15f, MaxInstances = 2 }, Projectile.Center);
                if (viewed) {
                    BreachBurst(new Vector2(Projectile.Center.X, lakeY));
                }
            }

            if (t > HoistFrame) {
                //升起：指数衰减，前快后慢；接近悬位后换弹簧落定（自带过冲回弹）
                float hoverY = lakeY - HoverHeight;
                if (Projectile.Center.Y > hoverY + 16f && t < RiseEnd) {
                    Projectile.velocity.Y *= 0.93f;
                    Projectile.velocity.X = 0f;
                }
                else {
                    HoldAnchor(domain, 0.08f, 0.16f);
                }
                //升起中身上血水成帘往下淌
                if (viewed && t < RiseEnd && t % 2 == 0) {
                    PRTLoader.NewParticle<PRT_KikasaBloodGlob>(
                        Projectile.Center + new Vector2(Main.rand.NextFloat(-42f, 42f), Main.rand.NextFloat(4f, 34f)),
                        new Vector2(Main.rand.NextFloat(-0.4f, 0.4f), Main.rand.NextFloat(2.2f, 3.6f)),
                        BloodMain * Main.rand.NextFloat(0.4f, 0.6f),
                        Main.rand.NextFloat(0.4f, 0.65f))?.Configure(Main.rand.Next(14, 26));
                }
            }

            if (t >= AwakenFrame && PlayOnce(8)) {
                //绽放拍：花冠展开、瓣缘泛粉光，入水点常驻涟漪自此接管
                SoundEngine.PlaySound(SoundID.Grass with { Volume = 0.6f, Pitch = -0.35f, MaxInstances = 2 }, Projectile.Center);
                SoundEngine.PlaySound(SoundID.MaxMana with { Volume = 0.4f, Pitch = -0.4f, MaxInstances = 2 }, Projectile.Center);
                if (viewed) {
                    KikasaDomainDeco.RippleAt(new Vector2(Projectile.Center.X, lakeY), 0.6f);
                    ShakeViewer(1.5f);
                }
            }

            //升起期姿态回正，觉醒后交给跟随逻辑
            Projectile.rotation = Projectile.rotation.AngleLerp(IdleSway(), 0.15f);

            if (t >= EmergeTotal) {
                State = StateAnchored;
                StateTimer = 0;
                StateParam = 0;
                attackCooldown = 40;
                relocateRest = 60;
                Projectile.netUpdate = Main.myPlayer == Projectile.owner;
            }
        }

        /// <summary>拽起浪冠：大环涟漪 + 扇形血珠 + 垂直水柱 + 血雾 + 花瓣粉点缀</summary>
        private void BreachBurst(Vector2 hit) {
            KikasaDomainDeco.RippleAt(hit, 2.6f);
            KikasaDomainDeco.RippleAt(hit + new Vector2(44f, 0f), 1.1f);
            KikasaDomainDeco.RippleAt(hit - new Vector2(40f, 0f), 1.0f);
            KikasaDomainDeco.SplashAt(hit + new Vector2(-18f, 0f), 12);
            KikasaDomainDeco.SplashAt(hit + new Vector2(18f, 0f), 12);

            for (int i = 0; i < 24; i++) {
                float angle = -MathHelper.Pi * (0.12f + 0.76f * i / 23f);
                float speed = Main.rand.NextFloat(3.2f, 7.8f);
                PRTLoader.NewParticle<PRT_KikasaBloodGlob>(
                    hit + new Vector2(Main.rand.NextFloat(-30f, 30f), -4f),
                    angle.ToRotationVector2() * speed,
                    Main.rand.NextBool(5) ? PetalPink : Main.rand.NextBool(3) ? BloodDeep : BloodMain,
                    Main.rand.NextFloat(0.5f, 0.9f))?.Configure(Main.rand.Next(22, 38));
            }
            for (int i = 0; i < 10; i++) {
                PRTLoader.NewParticle<PRT_KikasaBloodGlob>(
                    hit + new Vector2(Main.rand.NextFloat(-8f, 8f), -6f),
                    new Vector2(Main.rand.NextFloat(-0.9f, 0.9f), -Main.rand.NextFloat(8.5f, 13f)),
                    BloodMain * 0.9f, Main.rand.NextFloat(0.55f, 0.9f))?.Configure(Main.rand.Next(34, 50));
            }
            for (int i = 0; i < 3; i++) {
                PRTLoader.NewParticle<PRT_GhostRainMist>(
                    hit + new Vector2(Main.rand.NextFloat(-34f, 34f), -10f),
                    new Vector2(Main.rand.NextFloat(-0.3f, 0.3f), -Main.rand.NextFloat(0.3f, 0.7f)),
                    MistBlood * 0.8f, Main.rand.NextFloat(0.7f, 1.05f))?.Configure(Main.rand.Next(60, 100));
            }
            PRTLoader.NewParticle<PRT_DWave>(hit, Vector2.Zero, BloodDeep, 0.09f)
                ?.Configure(new Vector2(0.5f, 1f), -MathHelper.PiOver2, 0.36f, 11);

            SoundEngine.PlaySound(SoundID.SplashWeak with { Volume = 1f, Pitch = -0.35f, MaxInstances = 2 }, hit);
            SoundEngine.PlaySound(SoundID.DD2_MonkStaffGroundImpact with { Volume = 0.55f, Pitch = -0.7f, MaxInstances = 1 }, hit);
            ShakeViewer(5.5f);
        }

        //==================== 锚定跟随 ====================

        private void UpdateAnchored(Player owner, KikasaDomainPlayer domain, bool authority) {
            //锚位自愈：迟入场端可能在飞扑/搬家半途初始化了错锚，
            //锚定态下体位由同步包权威回正后，锚随体走一次
            if (StateTimer > 8 && MathF.Abs(Projectile.Center.X - anchorX) > 260f) {
                anchorX = Projectile.Center.X;
            }
            HoldAnchor(domain, 0.12f, 0.26f);

            //跟丢硬贴回：直接搬到主人身边重扎
            if (Vector2.Distance(owner.Center, Projectile.Center) > 2400f) {
                anchorX = owner.Center.X + (anchorX > owner.Center.X ? 220f : -220f);
                Projectile.Center = new Vector2(anchorX, domain.LakeWorldY - HoverHeight);
                Projectile.velocity = Vector2.Zero;
                Projectile.netUpdate = authority;
                return;
            }

            int target = FindTarget(owner);
            if (target >= 0) {
                FaceToward(Main.npc[target].Center, 0.07f);
            }
            else {
                Projectile.rotation = Projectile.rotation.AngleLerp(IdleSway(), 0.05f);
            }

            //搬家裁决：主人横向走远且驻留期已过（规则确定性，owner 盖章）
            if (StateTimer > 30 && relocateRest <= 0
                && MathF.Abs(owner.Center.X - anchorX) > RelocateTrigger) {
                State = StateRelocate;
                StateTimer = 0;
                StateParam = 0;
                Projectile.netUpdate = authority;
                return;
            }

            TryStartAttack(owner, domain, authority);
        }

        /// <summary>锚位弹簧：悬在锚点上方随水波轻晃；后坐位移由弹簧拽回=皮筋手感</summary>
        private void HoldAnchor(KikasaDomainPlayer domain, float rate, float mix) {
            float hoverY = domain.LakeWorldY - HoverHeight;
            float bob = MathF.Sin(Main.GlobalTimeWrappedHourly * 1.15f + Seed) * 7f
                + MathF.Sin(Main.GlobalTimeWrappedHourly * 0.53f + Seed * 2.3f) * 3f;
            Vector2 want = new(anchorX, hoverY + bob);
            Vector2 desired = (want - Projectile.Center) * rate;
            if (desired.Length() > 12f) {
                desired = desired.SafeNormalize(Vector2.Zero) * 12f;
            }
            Projectile.velocity = Vector2.Lerp(Projectile.velocity, desired, mix);
        }

        private void TryStartAttack(Player owner, KikasaDomainPlayer domain, bool authority) {
            int target = FindTarget(owner);
            if (target < 0 || attackCooldown > 0 || StateTimer < 26) {
                return;
            }
            NPC npc = Main.npc[target];
            float lakeY = domain.LakeWorldY;
            Vector2 anchorBase = new(anchorX, lakeY - HoverHeight);
            //皮筋极限内且在水上才扑：够不着就不扑
            bool pounceOk = npc.Center.Y < lakeY + 24f
                && Vector2.Distance(npc.Center, anchorBase) < ChainReach - 70f;
            //藤只够到湖面上方一段：太高/水下的交给种子
            bool vineOk = npc.Center.Y > lakeY - 560f && npc.Center.Y < lakeY + 32f;

            attackIndex++;
            int next;
            if (attackIndex % 3 == 0 && pounceOk) {
                next = StatePounce;
            }
            else if (attackIndex % 2 == 1 && vineOk) {
                next = StateVine;
            }
            else {
                next = StateSeed;
            }
            State = next;
            StateTimer = 0;
            //藤袭的子参数=本轮藤数：单点名与三连错拍轮替
            StateParam = next == StateVine ? (attackIndex % 4 == 3 ? 3 : 1) : 0;
            Projectile.netUpdate = authority;
        }

        //==================== 搬家（拔须→低掠→扎回）====================

        private void UpdateRelocate(Player owner, KikasaDomainPlayer domain, bool authority) {
            float lakeY = domain.LakeWorldY;
            int t = (int)StateTimer;
            int phase = (int)StateParam;
            bool viewed = ViewedOwner;

            void NextPhase(int next) {
                StateParam = next;
                StateTimer = 0;
                Projectile.netUpdate = authority;
            }

            if (phase == 0) {
                //拔须：花体轻抬蓄势，钩须逐根拔出水面收回身下
                HoldAnchor(domain, 0.1f, 0.2f);
                Projectile.velocity.Y -= 0.15f;
                for (int i = 0; i < 3; i++) {
                    int start = ProbeSlot[i] * UprootGap;
                    if (t >= start + 3 && PlayOnce(i)) {
                        SoundEngine.PlaySound(SoundID.SplashWeak with { Volume = 0.42f, Pitch = 0.05f, MaxInstances = 3 },
                            new Vector2(RootX(i), lakeY));
                        if (viewed) {
                            KikasaDomainDeco.RippleAt(new Vector2(RootX(i), lakeY), 0.7f);
                            KikasaDomainDeco.SplashAt(new Vector2(RootX(i), lakeY), 5);
                        }
                    }
                }
                if (t >= UprootTotal) {
                    relocateSide = anchorX >= owner.Center.X ? 1f : -1f;
                    NextPhase(1);
                }
                return;
            }

            if (phase == 1) {
                //低掠：贴水面滑向主人近旁，尾迹涟漪一路跟随——搬家不是漂移
                if ((int)relocateSide == 0) {
                    relocateSide = anchorX >= owner.Center.X ? 1f : -1f;
                }
                float destX = owner.Center.X + relocateSide * 156f;
                float skimY = lakeY - 44f + MathF.Sin(t * 0.35f + Seed) * 4f;
                float vx = MathHelper.Clamp((destX - Projectile.Center.X) * 0.06f, -16.5f, 16.5f);
                Projectile.velocity.X = MathHelper.Lerp(Projectile.velocity.X, vx, 0.12f);
                Projectile.velocity.Y = MathHelper.Clamp((skimY - Projectile.Center.Y) * 0.14f, -8f, 8f);
                //身姿顺势前倾
                Projectile.rotation = Projectile.rotation.AngleLerp(Projectile.velocity.X * 0.028f, 0.12f);

                if (viewed && t % 3 == 0) {
                    KikasaDomainDeco.RippleAt(new Vector2(Projectile.Center.X, lakeY),
                        0.3f + MathF.Abs(Projectile.velocity.X) * 0.014f);
                    if (t % 6 == 0) {
                        PRTLoader.NewParticle<PRT_KikasaBloodGlob>(
                            new Vector2(Projectile.Center.X - Projectile.velocity.X * 1.5f, lakeY - 3f),
                            new Vector2(-Projectile.velocity.X * 0.08f, -Main.rand.NextFloat(1.4f, 2.8f)),
                            BloodMain * 0.5f, Main.rand.NextFloat(0.3f, 0.5f))?.Configure(Main.rand.Next(10, 18));
                    }
                }

                if (MathF.Abs(destX - Projectile.Center.X) < 26f || t > TravelTimeout) {
                    anchorX = Projectile.Center.X;
                    NextPhase(2);
                }
                return;
            }

            //扎回：花体升回悬位，钩须逐根下刺入水
            HoldAnchor(domain, 0.1f, 0.2f);
            Projectile.rotation = Projectile.rotation.AngleLerp(IdleSway(), 0.1f);
            for (int i = 0; i < 3; i++) {
                int start = RootStart + ProbeSlot[i] * RootGap;
                if (t >= start + RootDur - 2 && PlayOnce(i)) {
                    SoundEngine.PlaySound(SoundID.SplashWeak with { Volume = 0.5f, Pitch = -0.45f, MaxInstances = 3 },
                        new Vector2(RootX(i), lakeY));
                    SoundEngine.PlaySound(SoundID.Drip with { Volume = 0.5f, Pitch = -0.8f, MaxInstances = 3 },
                        new Vector2(RootX(i), lakeY));
                    if (viewed) {
                        KikasaDomainDeco.RippleAt(new Vector2(RootX(i), lakeY), 0.85f);
                        KikasaDomainDeco.SplashAt(new Vector2(RootX(i), lakeY), 5);
                        ShakeViewer(0.7f);
                    }
                }
            }
            if (t >= RootTotal) {
                State = StateAnchored;
                StateTimer = 0;
                StateParam = 0;
                relocateRest = 90;
                Projectile.netUpdate = authority;
            }
        }

        //==================== 湖面藤袭（签名：点名式水面攻击）====================

        private void UpdateVine(Player owner, KikasaDomainPlayer domain, bool authority) {
            int t = (int)StateTimer;
            int casts = Math.Max((int)StateParam, 1);
            int target = FindTarget(owner);
            HoldAnchor(domain, 0.11f, 0.24f);

            //抬冠蓄势：花体上挺、面向猎物，湖在脚下听令
            if (t < VineCastBeats[0]) {
                if (target < 0) {
                    EndAttack(authority, 50);
                    return;
                }
                Projectile.velocity.Y -= 0.22f;
                FaceToward(Main.npc[target].Center, 0.2f);
                if (t == 2) {
                    SoundEngine.PlaySound(SoundID.Grass with { Volume = 0.5f, Pitch = -0.6f, MaxInstances = 2 }, Projectile.Center);
                }
                return;
            }

            //逐拍点穴施藤：每拍花体一记下顿指挥，湖面在点名处起藤
            int nextCast = lastVineCast + 1;
            if (nextCast < casts && t >= VineCastBeats[nextCast]) {
                lastVineCast = nextCast;
                chainTwang = MathF.Min(1f, chainTwang + 0.45f);
                Projectile.velocity.Y += 3.2f;
                SoundEngine.PlaySound(SoundID.Item17 with { Volume = 0.42f, Pitch = -0.55f, MaxInstances = 3 }, Projectile.Center);
                if (ViewedOwner) {
                    KikasaDomainDeco.RippleAt(new Vector2(Projectile.Center.X, domain.LakeWorldY), 0.4f);
                }

                //藤体只在 owner 端生成，spawn 参数自带全部初值
                if (authority && target >= 0) {
                    NPC npc = Main.npc[target];
                    float[] spread = casts >= 3 ? new float[] { 0f, -135f, 135f } : new float[] { 0f };
                    float strikeX = npc.Center.X + npc.velocity.X * 16f + spread[Math.Min(nextCast, spread.Length - 1)];
                    float height = MathHelper.Clamp(domain.LakeWorldY - npc.position.Y + 46f, 170f, 560f);
                    float lashDir = npc.velocity.X >= 0f ? 1f : -1f;
                    int damage = (int)owner.GetTotalDamage(DamageClass.Summon).ApplyTo(BiteDamage);
                    Projectile.NewProjectile(Projectile.GetSource_FromAI(),
                        new Vector2(strikeX, domain.LakeWorldY), Vector2.Zero,
                        ModContent.ProjectileType<KikasaPlanteraVine>(), damage, 6f,
                        Projectile.owner, height, 0f, lashDir);
                }
            }

            if (target >= 0) {
                FaceToward(Main.npc[target].Center, 0.12f);
            }
            if (t >= VineStateTotal) {
                EndAttack(authority, 100);
            }
        }

        //==================== 种子机关枪 ====================

        private void UpdateSeed(Player owner, KikasaDomainPlayer domain, bool authority) {
            int t = (int)StateTimer;
            int target = FindTarget(owner);
            //低速弹簧：后坐能明显把花体顶开，再被链拽回
            HoldAnchor(domain, 0.07f, 0.18f);

            if (t <= SeedAimFrames) {
                if (target < 0) {
                    EndAttack(authority, 45);
                    return;
                }
                FaceToward(Main.npc[target].Center, 0.3f);
                if (t == 3) {
                    SoundEngine.PlaySound(SoundID.DD2_BookStaffCast with { Volume = 0.4f, Pitch = -0.7f, MaxInstances = 2 }, Projectile.Center);
                }
                //蓄口血珠向口部汇聚，72% 后静默——喷发前吸气
                if (!Main.dedServ && t < SeedAimFrames * 0.72f && t % 2 == 1) {
                    Vector2 mouth = MouthPos();
                    Vector2 from = mouth + Main.rand.NextVector2Unit() * Main.rand.NextFloat(40f, 90f);
                    PRTLoader.NewParticle<PRT_KikasaBloodGlob>(from, (mouth - from) * 0.15f,
                        BloodMain * 0.5f, Main.rand.NextFloat(0.28f, 0.45f))?.Configure(8, 0f);
                }
                return;
            }

            if (t <= SeedFireEnd) {
                Vector2 aimPos = target >= 0 ? Main.npc[target].Center + Main.npc[target].velocity * 6f
                    : Projectile.Center + MouthDir() * 400f;
                FaceToward(aimPos, 0.35f);

                int shotIdx = (t - SeedAimFrames - 1) / SeedShotGap;
                if ((t - SeedAimFrames - 1) % SeedShotGap == 0 && shotIdx < SeedShotCount
                    && lastShotFired < shotIdx) {
                    lastShotFired = shotIdx;
                    FireSeed(owner, aimPos, shotIdx, authority);
                }
                return;
            }

            if (t >= SeedRecoverEnd) {
                EndAttack(authority, 120);
            }
        }

        private void FireSeed(Player owner, Vector2 aimPos, int shotIdx, bool authority) {
            Vector2 mouth = MouthPos();
            Vector2 aim = (aimPos - mouth).SafeNormalize(-Vector2.UnitY);

            //口部后坐：每发退一小步，链体跟着发颤，弹簧再拽回来
            Projectile.velocity -= aim * 2.3f;
            chainTwang = MathF.Min(1f, chainTwang + 0.22f);

            //原版世纪之花种子同款湿吐声，逐发微变调
            SoundEngine.PlaySound(SoundID.Item17 with {
                Volume = 0.38f,
                Pitch = -0.15f + shotIdx % 3 * 0.06f,
                MaxInstances = 4
            }, mouth);
            if (!Main.dedServ) {
                //弹壳水珠：垂直弹道两侧甩出、吃重力坠回湖里
                Vector2 side = new(-aim.Y, aim.X);
                for (int k = 0; k < 2; k++) {
                    float dir = k == 0 ? 1f : -1f;
                    PRTLoader.NewParticle<PRT_KikasaBloodGlob>(mouth - aim * 4f,
                        side * dir * Main.rand.NextFloat(1.6f, 3f) - aim * 0.6f
                            + new Vector2(0f, -Main.rand.NextFloat(0.5f, 1.5f)),
                        Main.rand.NextBool(3) ? BloodDeep : BloodMain,
                        Main.rand.NextFloat(0.3f, 0.5f))?.Configure(Main.rand.Next(20, 32), 0.42f);
                }
                //出膛烟尘
                PRTLoader.NewParticle<PRT_DWave>(mouth + aim * 8f, Vector2.Zero, BloodDeep, 0.05f)
                    ?.Configure(new Vector2(0.55f, 1f), aim.ToRotation(), 0.16f, 7);
            }
            if (ViewedOwner && shotIdx % 4 == 0) {
                ShakeViewer(0.5f);
            }

            //种子只在 owner 端生成，spawn 参数自带全部初值
            if (authority) {
                int damage = (int)owner.GetTotalDamage(DamageClass.Summon).ApplyTo(SeedDamage);
                Vector2 vel = aim.RotatedBy(Main.rand.NextFloat(-0.075f, 0.075f))
                    * Main.rand.NextFloat(12.5f, 14f);
                Projectile.NewProjectile(Projectile.GetSource_FromAI(), mouth, vel,
                    ModContent.ProjectileType<KikasaPlanteraSeed>(), damage, 2.5f, Projectile.owner);
            }
        }

        //==================== 弹性前扑咬（压轴）====================

        private void UpdatePounce(Player owner, KikasaDomainPlayer domain, bool authority) {
            int t = (int)StateTimer;
            int phase = (int)StateParam;
            int target = FindTarget(owner);
            //回弹目标每帧由锚位确定性重算：远端中途入场也不会拿到脏值
            Vector2 pounceAnchor = new(anchorX, domain.LakeWorldY - HoverHeight);
            Vector2 aimPos = target >= 0 ? Main.npc[target].Center + Main.npc[target].velocity * 7f
                : Projectile.Center + MouthDir() * 300f;
            Vector2 aim = (aimPos - Projectile.Center).SafeNormalize(-Vector2.UnitY);

            if (phase == 0) {
                //拉弓：迟发后拉 pow(6)，链在背侧绷到吱嘎作响
                if (target < 0 && t < PounceWindup / 2) {
                    EndAttack(authority, 45);
                    return;
                }
                float k = MathF.Pow(MathHelper.Clamp(t / (float)PounceWindup, 0f, 1f), 6f);
                Vector2 hold = pounceAnchor - aim * (k * 74f);
                Projectile.velocity = Vector2.Lerp(Projectile.velocity, (hold - Projectile.Center) * 0.3f, 0.35f);
                FaceToward(aimPos, 0.4f);

                //纤维吱嘎两声递进：皮筋快到极限了
                if ((t == 8 && PlayOnce(4)) || (t == 19 && PlayOnce(5))) {
                    chainTwang = MathF.Min(1f, chainTwang + 0.4f);
                    SoundEngine.PlaySound(SoundID.NPCHit7 with {
                        Volume = 0.42f,
                        Pitch = t < 12 ? -0.85f : -0.6f,
                        MaxInstances = 2
                    }, Projectile.Center);
                }
                //蓄势收拢血珠，72% 后静默
                if (!Main.dedServ && t < PounceWindup * 0.72f && t % 3 == 1) {
                    Vector2 from = Projectile.Center + Main.rand.NextVector2Unit() * Main.rand.NextFloat(50f, 100f);
                    PRTLoader.NewParticle<PRT_KikasaBloodGlob>(from,
                        (Projectile.Center - from) * 0.14f,
                        BloodMain * 0.5f, Main.rand.NextFloat(0.3f, 0.5f))?.Configure(9, 0f);
                }

                if (t >= PounceWindup) {
                    StateParam = 1;
                    StateTimer = 0;
                    Projectile.netUpdate = authority;
                }
                return;
            }

            if (phase == 1) {
                if (PlayOnce(0)) {
                    //弹射：一帧定速不做斜坡；獠口亮相，背向甩出挂身水珠
                    Projectile.velocity = aim * 27f;
                    chainTwang = 1f;
                    SoundEngine.PlaySound(SoundID.Roar with { Volume = 0.5f, Pitch = 0.35f, MaxInstances = 2 }, Projectile.Center);
                    SoundEngine.PlaySound(SoundID.SplashWeak with { Volume = 0.4f, Pitch = 0.15f, MaxInstances = 3 }, Projectile.Center);
                    if (!Main.dedServ) {
                        for (int i = 0; i < 7; i++) {
                            PRTLoader.NewParticle<PRT_KikasaBloodGlob>(
                                Projectile.Center + Main.rand.NextVector2Circular(26f, 26f),
                                -Projectile.velocity * 0.12f + Main.rand.NextVector2Circular(1.8f, 1.8f),
                                BloodMain * 0.55f, Main.rand.NextFloat(0.35f, 0.6f))
                                ?.Configure(Main.rand.Next(12, 22));
                        }
                    }
                    if (ViewedOwner) {
                        ShakeViewer(3f);
                    }
                }

                //飞行：复利续力，口朝行进向；沿途甩血
                Projectile.velocity *= 1.012f;
                Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;
                if (!Main.dedServ && Main.rand.NextBool(2)) {
                    PRTLoader.NewParticle<PRT_KikasaBloodGlob>(
                        Projectile.Center - Projectile.velocity * 0.4f + Main.rand.NextVector2Circular(22f, 22f),
                        -Projectile.velocity * 0.1f + Main.rand.NextVector2Circular(0.6f, 0.6f),
                        BloodMain * 0.5f, Main.rand.NextFloat(0.35f, 0.6f))?.Configure(Main.rand.Next(10, 18));
                }

                //皮筋极限：链绷到头被硬生生拽停——够到够不到都认账
                bool leashed = Vector2.Distance(Projectile.Center, pounceAnchor) >= ChainReach;
                if (leashed && PlayOnce(1)) {
                    Projectile.velocity *= -0.32f;
                    chainTwang = 1f;
                    SoundEngine.PlaySound(SoundID.NPCHit7 with { Volume = 0.5f, Pitch = -0.45f, MaxInstances = 2 }, Projectile.Center);
                    SoundEngine.PlaySound(SoundID.SplashWeak with { Volume = 0.35f, Pitch = -0.1f, MaxInstances = 3 }, Projectile.Center);
                    if (!Main.dedServ) {
                        //惯性把挂身血水向前甩出去
                        for (int i = 0; i < 6; i++) {
                            PRTLoader.NewParticle<PRT_KikasaBloodGlob>(
                                Projectile.Center + Main.rand.NextVector2Circular(24f, 24f),
                                -Projectile.velocity.SafeNormalize(Vector2.Zero) * Main.rand.NextFloat(4f, 8f)
                                    + Main.rand.NextVector2Circular(1.5f, 1.5f),
                                BloodMain * 0.55f, Main.rand.NextFloat(0.35f, 0.6f))?.Configure(Main.rand.Next(14, 24));
                        }
                    }
                    if (ViewedOwner) {
                        ShakeViewer(2f);
                    }
                }
                if (leashed || t >= PounceFlightMax) {
                    StateParam = 2;
                    StateTimer = 0;
                    Projectile.netUpdate = authority;
                }
                return;
            }

            //拽回：弹簧回摆带过冲，前半程还叼着獠口嚼两下
            Projectile.velocity += (pounceAnchor - Projectile.Center) * 0.028f;
            Projectile.velocity *= 0.88f;
            Projectile.rotation = Projectile.rotation.AngleLerp(IdleSway(), 0.08f);
            if ((t == 4 && PlayOnce(2)) || (t == 10 && PlayOnce(3))) {
                //咀嚼拍
                SoundEngine.PlaySound(SoundID.NPCHit7 with { Volume = 0.35f, Pitch = -0.2f + t * 0.01f, MaxInstances = 2 }, Projectile.Center);
            }
            if (t >= PounceSpringFrames) {
                EndAttack(authority, 140);
            }
        }

        private void EndAttack(bool authority, int cooldown) {
            State = StateAnchored;
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

            //花瓣垂头合拢，钩须逐根松脱，身子沉回湖里
            Projectile.rotation = Projectile.rotation.AngleLerp(MathHelper.Pi * 0.9f, 0.03f);
            if (lakeAlive) {
                Projectile.velocity.X *= 0.92f;
                Projectile.velocity.Y = MathF.Min(Projectile.velocity.Y + 0.22f, 8f);
            }
            else {
                Projectile.velocity *= 0.9f;
            }

            //逐根松脱的入水轻响：只有本来扎在水里的须才有涟漪答话
            for (int i = 0; i < 3; i++) {
                int start = ProbeSlot[i] * HookReleaseGap;
                if (t >= start + 2 && PlayOnce(i)) {
                    Vector2 from = hookAtDissolve[i] == Vector2.Zero ? RootPos(i, lakeY) : hookAtDissolve[i];
                    SoundEngine.PlaySound(SoundID.Drip with { Volume = 0.4f, Pitch = -0.4f, MaxInstances = 3 },
                        new Vector2(from.X, lakeY));
                    if (ViewedOwner && lakeAlive && from.Y > lakeY - 12f) {
                        KikasaDomainDeco.RippleAt(new Vector2(from.X, lakeY), 0.5f);
                    }
                }
            }

            //过水线拍（一次）
            if (lakeAlive && !dissolveSplashed && Projectile.Center.Y >= lakeY) {
                dissolveSplashed = true;
                SoundEngine.PlaySound(SoundID.SplashWeak with { Volume = 0.7f, Pitch = -0.35f, MaxInstances = 2 }, Projectile.Center);
                if (ViewedOwner) {
                    Vector2 hit = new(Projectile.Center.X, lakeY);
                    KikasaDomainDeco.SplashAt(hit, 9);
                    KikasaDomainDeco.RippleAt(hit, 1.25f);
                    ShakeViewer(2f);
                }
            }

            //边沉边化血珠
            if (!Main.dedServ && t % 2 == 0 && CurrentAlpha() > 0.15f) {
                PRTLoader.NewParticle<PRT_KikasaBloodGlob>(
                    Projectile.Center + Main.rand.NextVector2Circular(34f, 30f),
                    new Vector2(Main.rand.NextFloat(-0.6f, 0.6f), Main.rand.NextFloat(1.4f, 2.8f)),
                    (Main.rand.NextBool(6) ? PetalPink : BloodMain) * 0.5f,
                    Main.rand.NextFloat(0.35f, 0.6f))?.Configure(Main.rand.Next(12, 22));
            }

            //owner 到点收场；远端多给 10 帧等 kill 包，兜底自杀
            if (authority && t >= DissolveFrames) {
                Projectile.Kill();
            }
            else if (!authority && t >= DissolveFrames + 10) {
                Projectile.Kill();
            }
        }

        //==================== 钩须姿态（全参数化，各端本地重建）====================

        /// <summary>第 i 根钩须的扎根横位（扇形 + 实例微差）</summary>
        private float RootX(int i)
            => anchorX + FanBase[i] * (1f + 0.07f * MathF.Sin(Seed * 5.3f + i * 2.1f));

        private Vector2 RootPos(int i, float lakeY) => new(RootX(i), lakeY + RootDepth);

        /// <summary>搬家/出场时挂在花体身下的收拢位</summary>
        private Vector2 DanglePos(int i) {
            float sway = MathF.Sin(Main.GlobalTimeWrappedHourly * 3f + i * 2.2f + Seed) * 4f;
            return Projectile.Center + new Vector2((i - 1) * 46f + sway, i == 1 ? 64f : 50f);
        }

        private void UpdateHookPoses(KikasaDomainPlayer domain) {
            float lakeY = domain.LakeWorldY;
            int t = (int)StateTimer;

            for (int i = 0; i < 3; i++) {
                Vector2 root = RootPos(i, lakeY);
                Vector2 pos = root;
                float rot = MathHelper.Pi;   //爪尖朝下=贴图倒置（爪原生朝上）
                float alpha = 1f;
                bool rooted = true;
                float dissolve = 0f;

                switch (State) {
                    case StateEmerge: {
                            int start = OmenFrames + ProbeSlot[i] * HookProbeGap;
                            int local = t - start;
                            Vector2 apex = new(root.X, lakeY - 58f);
                            if (local < 0) {
                                pos = root + new Vector2(0f, 70f);
                                alpha = 0f;
                                rooted = false;
                            }
                            else if (local < HookProbeRise) {
                                float u = local / (float)HookProbeRise;
                                float e = 1f - (1f - u) * (1f - u);
                                pos = Vector2.Lerp(root + new Vector2(0f, 70f), apex, e);
                                rot = 0f + MathF.Sin(Seed + i) * 0.12f;
                                alpha = MathHelper.Clamp(u * 2.2f, 0f, 1f);
                                rooted = false;
                            }
                            else if (local < HookProbeRise + HookProbeHang) {
                                //悬拍：钩爪当空翻转，爪尖由天转地
                                float u = (local - HookProbeRise) / (float)HookProbeHang;
                                pos = apex + new Vector2(MathF.Sin(local * 0.8f + i) * 2f, -MathF.Sin(u * MathHelper.Pi) * 5f);
                                float side = i == 0 ? -1f : 1f;
                                rot = MathHelper.Lerp(0f, MathHelper.Pi * side, u * u);
                                rooted = false;
                            }
                            else if (local < HookProbeDur) {
                                float u = (local - HookProbeRise - HookProbeHang) / (float)(HookProbeDur - HookProbeRise - HookProbeHang);
                                pos = Vector2.Lerp(apex, root, u * u);
                                rooted = false;
                            }
                            break;
                        }
                    case StateRelocate: {
                            int phase = (int)StateParam;
                            if (phase == 0) {
                                int start = ProbeSlot[i] * UprootGap;
                                int local = t - start;
                                if (local >= 0 && local < UprootDur) {
                                    float u = local / (float)UprootDur;
                                    pos = Vector2.Lerp(root, DanglePos(i), 1f - (1f - u) * (1f - u));
                                    rot = MathHelper.Lerp(MathHelper.Pi, MathHelper.Pi + MathF.Sin(u * 6f) * 0.2f, u);
                                    rooted = false;
                                }
                                else if (local >= UprootDur) {
                                    pos = DanglePos(i);
                                    rooted = false;
                                }
                            }
                            else if (phase == 1) {
                                //低掠：钩须挂在身下顺流后摆
                                pos = DanglePos(i) + new Vector2(-Projectile.velocity.X * 1.6f, 6f);
                                rot = MathHelper.Pi + Projectile.velocity.X * 0.02f
                                    + MathF.Sin(Main.GlobalTimeWrappedHourly * 4f + i) * 0.12f;
                                rooted = false;
                            }
                            else {
                                int start = RootStart + ProbeSlot[i] * RootGap;
                                int local = t - start;
                                if (local < 0) {
                                    pos = DanglePos(i);
                                    rooted = false;
                                }
                                else if (local < RootDur) {
                                    float u = local / (float)RootDur;
                                    pos = Vector2.Lerp(DanglePos(i), root, u * u);
                                    rooted = false;
                                }
                            }
                            break;
                        }
                    case StateDissolve: {
                            //从溶解入场帧的实际位置松脱（快照为零向量=迟入场兜底回扎根位）
                            Vector2 from = hookAtDissolve[i] == Vector2.Zero ? root : hookAtDissolve[i];
                            int start = ProbeSlot[i] * HookReleaseGap;
                            int local = t - start;
                            pos = from;
                            if (local > 0) {
                                //松脱：往深处沉去，随溶解淡出
                                pos = from + new Vector2(0f, local * 1.1f);
                                dissolve = MathHelper.Clamp(local / 26f, 0f, 1f);
                                alpha = 1f - dissolve;
                            }
                            rooted = false;
                            break;
                        }
                }

                hookPos[i] = pos;
                hookRot[i] = rot;
                hookAlpha[i] = alpha;
                hookRooted[i] = rooted;
                hookDissolve[i] = dissolve;
            }
        }

        /// <summary>链与水面的交点：入水涟漪与常驻微圈的落点</summary>
        private Vector2 ChainEntry(int i, float lakeY) {
            Vector2 body = Projectile.Center;
            Vector2 hook = hookPos[i];
            if (hook.Y <= lakeY || body.Y >= lakeY) {
                return new Vector2(hook.X, lakeY);
            }
            float f = (lakeY - body.Y) / (hook.Y - body.Y);
            return new Vector2(body.X + (hook.X - body.X) * f, lakeY);
        }

        //==================== 常驻小演出 ====================

        private void UpdateIdleFX(KikasaDomainPlayer domain) {
            if (Main.dedServ || !ViewedOwner) {
                return;
            }
            float lakeY = domain.LakeWorldY;
            int t = (int)StateTimer;

            //入水点常驻微涟漪：锚在湖里的呼吸证明（scale<0.3 不占行波槽）
            bool anchoredLike = State is StateAnchored or StateVine or StateSeed
                || (State == StatePounce && (int)StateParam == 0);
            if (anchoredLike) {
                for (int i = 0; i < 3; i++) {
                    if (hookRooted[i] && (t + i * 13) % 38 == 0) {
                        KikasaDomainDeco.RippleAt(ChainEntry(i, lakeY), 0.22f);
                    }
                }
                //花瓣缘偶发凝珠
                if (Main.rand.NextBool(30) && CurrentAlpha() > 0.5f) {
                    PRTLoader.NewParticle<PRT_KikasaBloodGlob>(
                        Projectile.Center + new Vector2(Main.rand.NextFloat(-46f, 46f), Main.rand.NextFloat(10f, 34f)),
                        new Vector2(0f, Main.rand.NextFloat(0.5f, 1.1f)),
                        (Main.rand.NextBool(5) ? PetalPink : BloodMain) * Main.rand.NextFloat(0.4f, 0.55f),
                        Main.rand.NextFloat(0.3f, 0.5f))?.Configure(Main.rand.Next(18, 30), 0.3f);
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

        /// <summary>原版约定：贴图口部朝上，rotation = 目标方向角 + PiOver2</summary>
        private void FaceToward(Vector2 worldPos, float rate) {
            float want = (worldPos - Projectile.Center).ToRotation() + MathHelper.PiOver2;
            Projectile.rotation = Projectile.rotation.AngleLerp(want, rate);
        }

        private Vector2 MouthDir() => (Projectile.rotation - MathHelper.PiOver2).ToRotationVector2();

        private Vector2 MouthPos() => Projectile.Center + MouthDir() * 44f;

        /// <summary>闲时直立微摆：花对着天，随波轻晃</summary>
        private float IdleSway()
            => MathF.Sin(Main.GlobalTimeWrappedHourly * 0.9f + Seed) * 0.06f;

        private void UpdateFrames() {
            int phase = (int)StateParam;
            //獠口帧只留给前扑的愤怒收招：弹射、锁咬与回摆前半程
            jawOpen = State == StatePounce && (phase == 1 || (phase == 2 && StateTimer < 14f));
            bool agitated = State == StateSeed && StateTimer > SeedAimFrames && StateTimer <= SeedFireEnd;
            int speed = jawOpen ? 4 : agitated ? 5 : 8;
            if (++frameTick >= speed) {
                frameTick = 0;
                frameIndex = (frameIndex + 1) % 4;
            }
        }

        private bool ViewedOwner
            => KikasaDomain.Viewed != null && KikasaDomain.Viewed.Player.whoAmI == Projectile.owner;

        private static void ShakeViewer(float amount)
            => Main.LocalPlayer?.CWR()?.GetScreenShake(amount);

        //==================== 表现参数 ====================

        private float CurrentAlpha() {
            int t = (int)StateTimer;
            return State switch {
                StateEmerge => t < HoistFrame ? 0f : MathHelper.Clamp((t - HoistFrame) / 5f, 0f, 1f),
                StateDissolve => MathHelper.Clamp((DissolveFrames - t) / 14f, 0f, 1f),
                _ => 1f,
            };
        }

        /// <summary>uForm：1=全血水 0=真身；拽起期自上而下凝实，常态半沉呼吸</summary>
        private float CurrentForm() {
            int t = (int)StateTimer;
            float steady = 0.36f + MathF.Sin(Main.GlobalTimeWrappedHourly * 2.9f + Seed) * 0.05f;
            return State switch {
                StateEmerge => t < HoistFrame
                    ? 1f
                    : MathHelper.Lerp(1f, steady, SmoothStep01(MathHelper.Clamp((t - HoistFrame) / (float)(RiseEnd - HoistFrame), 0f, 1f))),
                StateDissolve => MathHelper.Clamp(steady + t / (float)DissolveFrames * 0.3f, 0f, 1f),
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
            return 1f - MathHelper.Clamp((t - RiseEnd) / 10f, 0f, 1f);
        }

        private float CurrentDissolve()
            => State == StateDissolve
                ? MathF.Pow(MathHelper.Clamp(StateTimer / 40f, 0f, 1f), 0.9f)
                : 0f;

        private float BodyScale() {
            float scale = 0.96f + MathF.Sin(Main.GlobalTimeWrappedHourly * 2.2f + Seed) * 0.012f;
            int t = (int)StateTimer;
            if (State == StateEmerge && t >= HoistFrame && t < HoistFrame + 10) {
                //破水过冲
                scale *= 1f + 0.08f * (1f - (t - HoistFrame) / 10f);
            }
            else if (State == StatePounce && (int)StateParam == 0) {
                //拉弓憋气微鼓
                scale *= 1f + 0.05f * MathHelper.Clamp(t / (float)PounceWindup, 0f, 1f);
            }
            else if (State == StateDissolve) {
                scale *= MathHelper.Lerp(1f, 0.9f, MathHelper.Clamp(t / (float)DissolveFrames, 0f, 1f));
            }
            return scale;
        }

        private static float SmoothStep01(float t) => t * t * (3f - 2f * t);

        //==================== 绘制 ====================

        public override bool PreDraw(ref Color lightColor) {
            if (!poseInit) {
                return false;
            }
            Main.instance.LoadNPC(NPCID.Plantera);
            Main.instance.LoadNPC(NPCID.PlanterasHook);
            Texture2D bodyTex = TextureAssets.Npc[NPCID.Plantera]?.Value;
            Texture2D hookTex = TextureAssets.Npc[NPCID.PlanterasHook]?.Value;
            if (bodyTex == null || hookTex == null) {
                return false;
            }

            SpriteBatch sb = Main.spriteBatch;
            float alpha = CurrentAlpha();

            //钩须链：血藤垂坠贝塞尔（水下段由湖面镜面自然遮挡）
            DrawChains(sb);

            //本体与钩爪：血湖材质
            DrawShaderBodies(sb, bodyTex, hookTex, alpha);

            //加色层：预兆血光 / 绽放粉光 / 蓄口积光 / 入水点微光
            DrawGlow(sb, alpha);

            return false;
        }

        private void DrawChains(SpriteBatch sb) {
            Texture2D chain = TextureAssets.Chain26?.Value;
            if (chain == null) {
                return;
            }
            float fade = State == StateDissolve
                ? MathHelper.Clamp(1f - StateTimer / (float)DissolveFrames, 0f, 1f) : 1f;
            for (int i = 0; i < 3; i++) {
                float a = hookAlpha[i] * fade;
                if (a <= 0.02f) {
                    continue;
                }
                DrawOneChain(sb, chain, i, Projectile.Center, hookPos[i], a);
            }
        }

        /// <summary>单链：二次贝塞尔（松弛垂坠 + 崩弹横振），原版 Chain26 藤蔓 16px 步进</summary>
        private void DrawOneChain(SpriteBatch sb, Texture2D chain, int i, Vector2 from, Vector2 to, float alpha) {
            float dist = Vector2.Distance(from, to);
            if (dist < 10f) {
                return;
            }
            //静息链长：锚定悬姿下的自然长度，短于它=松弛垂坠
            float restLen = MathF.Sqrt(HoverHeight * HoverHeight + FanBase[i] * FanBase[i]) + RootDepth * 0.6f;
            float slack = MathF.Max(0f, restLen - dist);
            Vector2 dir = (to - from) / dist;
            Vector2 perp = new(-dir.Y, dir.X);
            Vector2 mid = (from + to) * 0.5f
                + new Vector2(0f, 7f + slack * 0.32f)
                + perp * (MathF.Sin(Main.GlobalTimeWrappedHourly * 21f + i * 1.7f + Seed) * 12f * chainTwang);

            float approxLen = (Vector2.Distance(from, mid) + Vector2.Distance(mid, to) + dist) * 0.5f;
            int steps = Math.Min((int)(approxLen / 16f) + 1, 44);
            Color col = Color.Lerp(Color.White, BloodMain, 0.62f) * alpha;
            Color deep = Color.Lerp(Color.White, BloodDeep, 0.75f) * (alpha * 0.9f);

            Vector2 prev = from;
            for (int s = 1; s <= steps; s++) {
                float u = s / (float)steps;
                Vector2 p = Bezier(from, mid, to, u);
                Vector2 seg = p - prev;
                float len = seg.Length();
                if (len > 0.5f) {
                    int srcH = (int)MathF.Min(len + 1f, chain.Height);
                    //深处的链段沉色：越近水底越暗
                    Color c = u > 0.72f ? deep : col;
                    sb.Draw(chain, (prev + p) * 0.5f - Main.screenPosition,
                        new Rectangle(0, 0, chain.Width, srcH), c,
                        seg.ToRotation() - MathHelper.PiOver2,
                        new Vector2(chain.Width * 0.5f, srcH * 0.5f), 1f, SpriteEffects.None, 0f);
                }
                prev = p;
            }
        }

        private static Vector2 Bezier(Vector2 a, Vector2 b, Vector2 c, float u) {
            float v = 1f - u;
            return v * v * a + 2f * v * u * b + u * u * c;
        }

        private void DrawShaderBodies(SpriteBatch sb, Texture2D bodyTex, Texture2D hookTex, float bodyAlpha) {
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

            //钩爪先画（压在花体之下）
            int hookFrames = Main.npcFrameCount[NPCID.PlanterasHook];
            int hookFrameH = hookTex.Height / hookFrames;
            for (int i = 0; i < 3; i++) {
                if (hookAlpha[i] <= 0.02f) {
                    continue;
                }
                Rectangle frame = new(0, hookFrameH * ((frameIndex + i) % hookFrames), hookTex.Width, hookFrameH);
                Color color;
                if (shaderOk) {
                    //钩须常年泡在水里：血水占比高于花体
                    float segForm = MathHelper.Clamp(0.5f + MathF.Sin(Main.GlobalTimeWrappedHourly * 3.1f + Seed + i * 1.9f) * 0.08f, 0f, 1f);
                    form.Parameters["uSeed"]?.SetValue(Seed + i * 2.3f);
                    form.Parameters["uForm"]?.SetValue(segForm);
                    form.Parameters["uDissolve"]?.SetValue(hookDissolve[i]);
                    SetFrameParams(form, hookTex, frame);
                    form.CurrentTechnique.Passes[0].Apply();
                    color = new Color(255, 255, 255, (byte)(hookAlpha[i] * 255f));
                }
                else {
                    color = Color.Lerp(Color.White, BloodMain, 0.55f) * hookAlpha[i];
                }
                sb.Draw(hookTex, hookPos[i] - Main.screenPosition, frame, color,
                    hookRot[i], frame.Size() * 0.5f, 0.9f, SpriteEffects.None, 0f);
            }

            //花体
            if (bodyAlpha > 0.01f) {
                int bodyFrames = Main.npcFrameCount[NPCID.Plantera];
                int frameH = bodyTex.Height / bodyFrames;
                int row = frameIndex + (jawOpen ? 4 : 0);
                Rectangle frame = new(0, frameH * row, bodyTex.Width, frameH);
                Color color;
                if (shaderOk) {
                    form.Parameters["uSeed"]?.SetValue(Seed);
                    form.Parameters["uForm"]?.SetValue(CurrentForm());
                    form.Parameters["uDissolve"]?.SetValue(CurrentDissolve());
                    form.Parameters["uScanMode"]?.SetValue(CurrentScanMode());
                    SetFrameParams(form, bodyTex, frame);
                    form.CurrentTechnique.Passes[0].Apply();
                    color = new Color(255, 255, 255, (byte)(bodyAlpha * 255f));
                }
                else {
                    color = Color.Lerp(Color.White, BloodMain, 0.55f) * bodyAlpha;
                }
                sb.Draw(bodyTex, Projectile.Center - Main.screenPosition, frame, color,
                    Projectile.rotation, frame.Size() * 0.5f, BodyScale(), SpriteEffects.None, 0f);
            }

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
        }

        private static void SetFrameParams(Effect form, Texture2D tex, Rectangle frame) {
            form.Parameters["uUvRect"]?.SetValue(new Vector4(
                frame.X / (float)tex.Width, frame.Y / (float)tex.Height,
                frame.Width / (float)tex.Width, frame.Height / (float)tex.Height));
            form.Parameters["uTexel"]?.SetValue(new Vector2(1f / tex.Width, 1f / tex.Height));
            form.Parameters["uAspect"]?.SetValue(frame.Width / (float)frame.Height);
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
            float lakeY = domain.LakeWorldY;
            int t = (int)StateTimer;

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

            //预兆：出水点与三处扎根位的水下血光自深处上浮
            if (State == StateEmerge && t < OmenFrames) {
                float ot = MathHelper.Clamp(t / (float)OmenFrames, 0f, 1f);
                float ease = 1f - (1f - ot) * (1f - ot);
                EnsureBegin();
                Vector2 pos = new(anchorX, lakeY + MathHelper.Lerp(48f, 8f, ease));
                float r = 30f + 22f * ease;
                sb.Draw(glow, pos - Main.screenPosition, null, FoamGlow * (0.4f * ease), 0f,
                    gOrigin, new Vector2(r * 2.6f / glow.Width, r * 1.1f / glow.Height), SpriteEffects.None, 0f);
                for (int i = 0; i < 3; i++) {
                    Vector2 rootGlow = new(RootX(i), lakeY + MathHelper.Lerp(30f, 6f, ease));
                    sb.Draw(glow, rootGlow - Main.screenPosition, null, FoamGlow * (0.24f * ease), 0f,
                        gOrigin, new Vector2(26f * 2f / glow.Width, 26f * 0.8f / glow.Height), SpriteEffects.None, 0f);
                }
            }

            //绽放拍：花冠一层粉光（世纪之花的点缀色只做次要层）
            if (State == StateEmerge && t >= AwakenFrame) {
                float f = MathHelper.Clamp((t - AwakenFrame) / (float)(EmergeTotal - AwakenFrame), 0f, 1f);
                float a = MathF.Sin(f * MathHelper.Pi) * 0.55f;
                if (a > 0.02f) {
                    EnsureBegin();
                    float r = 44f + 26f * f;
                    sb.Draw(glow, Projectile.Center - Main.screenPosition, null, PetalPink * a, 0f,
                        gOrigin, new Vector2(r * 2f / glow.Width), SpriteEffects.None, 0f);
                }
            }

            //种子蓄口/连发：口部积光
            if (State == StateSeed && alpha > 0.1f && t <= SeedFireEnd) {
                float charge = t <= SeedAimFrames
                    ? t / (float)SeedAimFrames
                    : 0.75f + 0.25f * MathF.Sin(t * 1.4f);
                EnsureBegin();
                Vector2 mouth = MouthPos();
                float r = 8f + 15f * charge;
                sb.Draw(glow, mouth - Main.screenPosition, null, FoamGlow * (0.5f * charge), 0f,
                    gOrigin, new Vector2(r * 2f / glow.Width), SpriteEffects.None, 0f);
            }

            //拉弓：体心积光憋到弹射前一刻
            if (State == StatePounce && (int)StateParam == 0) {
                float charge = MathHelper.Clamp(t / (float)PounceWindup, 0f, 1f);
                EnsureBegin();
                float r = 20f + 26f * charge * charge;
                sb.Draw(glow, Projectile.Center - Main.screenPosition, null,
                    BloodMain with { A = 0 } * (0.35f * charge), 0f,
                    gOrigin, new Vector2(r * 2f / glow.Width), SpriteEffects.None, 0f);
            }

            //入水点常驻微光：锚在湖里的暗示
            if (alpha > 0.5f && State != StateDissolve) {
                for (int i = 0; i < 3; i++) {
                    if (!hookRooted[i]) {
                        continue;
                    }
                    EnsureBegin();
                    Vector2 entry = ChainEntry(i, lakeY);
                    float pulse = 0.14f + 0.05f * MathF.Sin(Main.GlobalTimeWrappedHourly * 2.4f + i * 2.1f + Seed);
                    sb.Draw(glow, entry - Main.screenPosition, null, FoamGlow * pulse, 0f,
                        gOrigin, new Vector2(30f * 2.4f / glow.Width, 30f * 0.7f / glow.Height), SpriteEffects.None, 0f);
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
            //前扑咬中：獠口啃咬的碎肉血花（OnHit 只在 owner 端跑）
            if (Main.dedServ) {
                return;
            }
            for (int i = 0; i < 9; i++) {
                PRTLoader.NewParticle<PRT_KikasaBloodGlob>(
                    target.Center + Main.rand.NextVector2Circular(22f, 22f),
                    Projectile.velocity * 0.25f + Main.rand.NextVector2Circular(2.6f, 2.6f),
                    BloodMain * 0.6f, Main.rand.NextFloat(0.4f, 0.7f))
                    ?.Configure(Main.rand.Next(14, 26), Main.rand.NextFloat(-0.4f, 0.4f));
            }
            SoundEngine.PlaySound(SoundID.NPCHit7 with { Volume = 0.65f, Pitch = -0.35f, MaxInstances = 3 }, target.Center);
            SoundEngine.PlaySound(SoundID.NPCHit1 with { Volume = 0.5f, Pitch = -0.25f, MaxInstances = 3 }, target.Center);
        }

        public override void OnKill(int timeLeft) {
            //谢幕残珠：花体与三须各留一口血水
            if (Main.dedServ || !poseInit) {
                return;
            }
            for (int i = 0; i < 10; i++) {
                PRTLoader.NewParticle<PRT_KikasaBloodGlob>(
                    Projectile.Center + Main.rand.NextVector2Circular(30f, 26f),
                    new Vector2(Main.rand.NextFloat(-1.2f, 1.2f), Main.rand.NextFloat(0.5f, 2.6f)),
                    (Main.rand.NextBool(6) ? PetalPink : BloodMain) * 0.5f,
                    Main.rand.NextFloat(0.35f, 0.6f))?.Configure(Main.rand.Next(14, 26));
            }
            for (int i = 0; i < 3; i++) {
                PRTLoader.NewParticle<PRT_KikasaBloodGlob>(
                    Vector2.Lerp(Projectile.Center, hookPos[i], 0.55f),
                    new Vector2(0f, Main.rand.NextFloat(0.8f, 1.8f)),
                    BloodDeep * 0.5f, Main.rand.NextFloat(0.3f, 0.5f))?.Configure(Main.rand.Next(12, 22));
            }
            PRTLoader.NewParticle<PRT_GhostRainMist>(Projectile.Center,
                new Vector2(0f, -0.25f), MistBlood * 0.7f, Main.rand.NextFloat(0.6f, 0.9f))
                ?.Configure(Main.rand.Next(50, 80));
        }
    }
}
