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

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaServants.KikasaMoonLord
{
    /// <summary>
    /// 鬼奴·噬月心藏。血湖之水凝成的月球领主核心——只有一颗巨大的裸心，
    /// 没有躯体没有头手。数条主动脉血管自心脏垂坠没入湖面，湖就是它的供血源：
    /// 血管以可见的蠕动鼓包把湖水一口一口逆流泵上去，心脏按心缩/心舒节拍搏动，
    /// 全部演出与攻击时序都锚在心跳拍上。出水演出为甲壳裹心破水升起、
    /// 血管逐条被拽起接驳、甲壳崩碎露心的第一次心跳=觉醒拍。
    /// 攻击：幻月球（心跳挤出缓行血球爆十字）、血管鞭（远处目标脚下湖面暴起）、
    /// 幻月血芒（长充能心跳狂加速→湖面变暗→心脏睁开→贯屏毁灭射线慢弧扫荡）。
    /// 溶解为停搏谢幕：心跳减慢→血管逐根松脱坠湖→最后一拍后停搏、
    /// 整颗化作大股血水倾泻回湖。联机契约与克眼/毁灭者基准同构：
    /// 状态机走 ai[0..2] 确定性推进、owner 转场盖章、节拍闩防快照回卷、
    /// 子弹幕只在 owner 端生成、生命线只有 owner 判
    /// </summary>
    internal class KikasaMoonLordServant : ModProjectile, IKikasaServant
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        //==================== 可调基数（占位初值，验收再调）====================

        /// <summary>心搏接触/血管鞭基伤（召唤加成前）</summary>
        internal const int ContactDamage = 850;

        /// <summary>幻月血芒射线基伤（召唤加成前）</summary>
        internal const int RayDamage = 460;

        /// <summary>幻月球与十字血芒基伤（召唤加成前）</summary>
        internal const int OrbDamage = 420;

        //==================== 状态 ====================

        private const int StateEmerge = 0;
        private const int StateFollow = 1;
        private const int StateMoonOrbs = 2;
        private const int StateVesselWhip = 3;
        private const int StateMoonRay = 4;
        private const int StateDissolve = 5;

        private int State { get => (int)Projectile.ai[0]; set => Projectile.ai[0] = value; }
        private ref float StateTimer => ref Projectile.ai[1];
        /// <summary>状态内子参数：射线=相位号，其余为通用相位/计数</summary>
        private ref float StateParam => ref Projectile.ai[2];

        //==================== 时序 ====================

        //出水：湖底心跳预兆→甲壳破水→升起+血管逐条拽起→甲壳崩碎觉醒拍→落定
        private const int OmenEnd = 44;
        private const int RiseEnd = 116;
        private const int VesselRaiseStart = 56;
        private const int VesselRaiseGap = 12;
        private const int VesselRaiseFrames = 22;
        private const int AwakenFrame = 128;
        private const int EmergeTotal = 154;

        //幻月球：刹停→三次心跳挤球→回摆
        private const int OrbBrakeEnd = 10;
        private static readonly int[] OrbSqueezeFrames = [16, 50, 84];
        private const int OrbStateEnd = 118;

        //血管鞭：心脏攥紧 tell→owner 甩出鞭弹幕→收势
        private const int WhipSpawnFrame = 8;
        private const int WhipStateEnd = 64;

        //幻月血芒：锁定→长充能(72%后静默)→睁眼→射线→收势
        private const int RayLockEnd = 14;
        private const int RayChargeFrames = 120;
        private const int RayChargeEnd = RayLockEnd + RayChargeFrames;      //134
        private const int RaySilenceFrame = RayLockEnd + (int)(RayChargeFrames * 0.72f);
        private const int RayEyeEnd = RayChargeEnd + 18;                    //152
        private const int RayFireFrame = RayEyeEnd;
        private const int RayBeamEnd = RayFireFrame + KikasaMoonRay.TotalLife;
        private const int RayRecoverEnd = RayBeamEnd + 26;

        //溶解：心跳减慢+血管逐根松脱→最后一拍→停搏死寂→倾泻回湖
        private const int DetachStart = 18;
        private const int DetachGap = 16;
        private const int DetachFrames = 24;
        private const int LastBeatFrame = 122;
        private const int PourStart = 134;
        private const int PourEnd = 178;
        private const int DissolveTotal = 190;

        //==================== 心跳时钟（本支线独占系统）====================

        //相位逐帧按当前状态的搏率累加，整数跨越=一拍；纯本地表现量，
        //各端从同一状态计时器推同一公式；节律跨状态连续，只在觉醒拍起跳
        private float beatPhase;
        private float beatEnvelope;
        private int lastBeatIndex = -1;
        private int lastDubIndex = -1;

        //==================== 血管（本地重建的表现几何，不入同步）====================

        private const int VesselCount = 5;
        private const int VesselSamples = 20;
        /// <summary>入水点相对心脏的横向摊开目标</summary>
        private static readonly float[] VesselSpread = [-192f, -96f, 0f, 98f, 190f];
        private static readonly float[] VesselWidthMul = [1.05f, 0.78f, 1.18f, 0.72f, 0.92f];
        /// <summary>心脏侧接驳点角（π/2=正下方）</summary>
        private static readonly float[] VesselAnchorAng = [2.34f, 2.00f, 1.57f, 1.13f, 0.82f];
        private static readonly float[] VesselBowDir = [-1f, -0.5f, 0.6f, 0.5f, 1f];

        private readonly float[] vesselEntryX = new float[VesselCount];
        private readonly float[] vesselRaise = new float[VesselCount];
        private readonly float[] vesselDrain = new float[VesselCount];
        private readonly bool[] vesselDocked = new bool[VesselCount];
        private readonly bool[] vesselDetached = new bool[VesselCount];
        private bool vesselsInit;

        /// <summary>本帧泵血鼓包的世界位与强度，DrawVessels 填、DrawGlow 消费</summary>
        private readonly List<(Vector2 pos, float power)> bulgeGlows = new();

        //==================== 本地表现量（节拍闩防快照回卷）====================

        private int lastSeenState = -1;
        private bool breachDone;
        private bool awakenDone;
        private int attackCooldown;
        private int attackIndex;
        private bool silenceLatched;
        private bool fireLatched;
        private bool lastBeatLatched;
        private bool pourLatched;
        /// <summary>充能期逐帧刷新的瞄准角（本地表现用，开火参数由 owner 盖章）</summary>
        private float chargeAimAngle;

        //==================== 配色（血湖家族 + 幻月苍青次要点缀）====================

        private static Color BloodMain => KikasaDomain.CoolTint(new(237, 77, 69), new(126, 158, 164));
        private static Color BloodDeep => KikasaDomain.CoolTint(new(140, 32, 30), new(84, 104, 110));
        private static Color FoamGlow => KikasaDomain.CoolTint(new(246, 133, 112), new(176, 200, 204));
        private static Color MistBlood => KikasaDomain.CoolTint(new(58, 18, 20), new(52, 62, 66));
        /// <summary>幻月苍青：瞳芯/球缘/射线核晕的次要点缀色</summary>
        internal static Color MoonGlint => KikasaDomain.CoolTint(new(168, 226, 214), new(150, 190, 186));

        private Player Owner => Main.player[Projectile.owner];

        /// <summary>连续量抖动的确定性相位，各端一致（9.1：不掷 Main.rand）</summary>
        private float Seed => Projectile.identity * 0.7391f;

        private const float BaseScale = 2.35f;

        //==================== 召唤入口 ====================

        /// <summary>KikasaServantIndex 登记的召唤委托；emergeAt.Y = 湖面</summary>
        internal static void Summon(Player owner, Vector2 emergeAt) {
            if (owner.whoAmI != Main.myPlayer) {
                return;
            }
            int damage = (int)owner.GetTotalDamage(DamageClass.Summon).ApplyTo(ContactDamage);
            Projectile.NewProjectile(owner.GetSource_Misc("KikasaServant"),
                emergeAt + new Vector2(0f, 64f), Vector2.Zero,
                ModContent.ProjectileType<KikasaMoonLordServant>(), damage, 9f, owner.whoAmI);
        }

        //==================== 定义 ====================

        public override void SetStaticDefaults() {
            Main.projPet[Projectile.type] = true;
            ProjectileID.Sets.MinionSacrificable[Projectile.type] = false;
            //血管垂到湖面，心脏出屏也要画
            ProjectileID.Sets.DrawScreenCheckFluff[Projectile.type] = 2000;
        }

        public override void SetDefaults() {
            Projectile.width = 100;
            Projectile.height = 140;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Summon;
            Projectile.minion = true;
            Projectile.minionSlots = 0f;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.netImportant = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 26;
            Projectile.timeLeft = 180;
        }

        public override bool MinionContactDamage() => true;

        /// <summary>接触伤害只开在心缩拍的收缩窗：碰它的代价按它的节拍收——
        /// 每拍心脏可见地猛缩+微光圈，窗口与演出严格对齐；甲壳期/停搏期恒关</summary>
        public override bool? CanDamage() {
            if (State == StateDissolve || State == StateEmerge && !awakenDone) {
                return false;
            }
            return beatEnvelope > 0.5f ? null : false;
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
            //还没破水就收场：什么都没露出来，不演停搏谢幕
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

            //生命线：只有 owner 裁决——服务器无领域状态（既定契约），
            //迟入场客户端首份快照前也会误判；其余端只跟包换场
            if (authority && State != StateDissolve && !LakeHealthy(owner, domain)) {
                BeginDissolve();
            }

            Projectile.timeLeft = 180;
            Projectile.damage = (int)owner.GetTotalDamage(DamageClass.Summon).ApplyTo(ContactDamage);

            //换场清闩：远端可能靠收包换场，残闩会吞掉新场节拍；
            //心跳相位刻意不清——心脏的节律跨状态连续，只在觉醒拍起跳
            if (State != lastSeenState) {
                lastSeenState = State;
                silenceLatched = false;
                fireLatched = false;
                if (State == StateDissolve) {
                    lastBeatLatched = false;
                    pourLatched = false;
                }
            }

            //迟入场重建：不在出水态被创建/首见时血管直接就位
            if (!vesselsInit) {
                vesselsInit = true;
                bool preRaised = State != StateEmerge;
                for (int i = 0; i < VesselCount; i++) {
                    vesselRaise[i] = preRaised ? 1f : 0f;
                    vesselEntryX[i] = Projectile.Center.X + (preRaised ? VesselSpread[i] : VesselSpread[i] * 0.18f);
                }
            }

            StateTimer++;
            switch (State) {
                case StateEmerge: UpdateEmerge(owner, domain); break;
                case StateFollow: UpdateFollow(owner, domain, authority); break;
                case StateMoonOrbs: UpdateMoonOrbs(owner, domain, authority); break;
                case StateVesselWhip: UpdateVesselWhip(owner, domain, authority); break;
                case StateMoonRay: UpdateMoonRay(owner, domain, authority); break;
                case StateDissolve: UpdateDissolve(domain, authority); break;
            }

            UpdateHeartbeat();
            UpdateVesselAnchors();
            if (attackCooldown > 0) {
                attackCooldown--;
            }

            //心跳呼吸光：充能末段被吸走（湖面变暗的另一半）
            float glow = CurrentAlpha() * (0.35f + 0.45f * beatEnvelope) * (1f - ChargeDim() * 0.85f);
            if (glow > 0.02f) {
                Lighting.AddLight(Projectile.Center, 0.55f * glow, 0.13f * glow, 0.12f * glow);
            }
        }

        private static bool LakeHealthy(Player owner, KikasaDomainPlayer domain)
            => !owner.dead && domain.AnyActive
            && domain.Phase != KikasaDomainPhase.Closing
            && domain.RiseT >= 0.9f;

        //==================== 心跳时钟 ====================

        /// <summary>当前搏率（每帧相位增量）：状态与状态内进度的确定性函数</summary>
        private float BeatRate() {
            int t = (int)StateTimer;
            switch (State) {
                case StateEmerge:
                    return awakenDone ? 1f / 46f : 0f;
                case StateMoonOrbs:
                    return 1f / 38f;
                case StateVesselWhip:
                    return 1f / 44f;
                case StateMoonRay:
                    if (t < RayLockEnd) {
                        return 1f / 46f;
                    }
                    if (t < RaySilenceFrame) {
                        //骤然加速：46f/拍 → 15f/拍
                        float c = ChargeT();
                        return 1f / MathHelper.Lerp(46f, 15f, MathF.Pow(c, 1.15f));
                    }
                    if (t < RayBeamEnd) {
                        return 0f;   //静默与射线：心跳骤停
                    }
                    return 1f / 70f; //收势缓慢复搏
                case StateDissolve:
                    return t >= 110 ? 0f : 1f / (54f + t * 0.42f);
                default:
                    return 1f / 54f;
            }
        }

        /// <summary>拍强度：溶解逐渐衰竭、充能越擂越重</summary>
        private float BeatStrength() {
            if (State == StateDissolve) {
                return MathF.Max(0.25f, 1f - StateTimer / 130f);
            }
            if (State == StateMoonRay && StateTimer > RayLockEnd && StateTimer < RaySilenceFrame) {
                return 0.9f + 0.5f * ChargeT();
            }
            return 1f;
        }

        private void UpdateHeartbeat() {
            float strength = BeatStrength();
            float rate = BeatRate();

            if (rate <= 0f) {
                //停搏/静默：包络自然衰竭，不许冻在收缩位卡住接触窗
                beatEnvelope *= 0.85f;

                //溶解的最后一拍：死寂里手动擂一记
                if (State == StateDissolve && !lastBeatLatched && (int)StateTimer == LastBeatFrame) {
                    lastBeatLatched = true;
                    beatPhase = MathF.Floor(beatPhase) + 1.0001f;
                    lastBeatIndex = (int)beatPhase;
                    lastDubIndex = (int)beatPhase;
                    beatEnvelope = 1.15f;
                    OnBeat(1.15f, dub: false);
                }
                return;
            }

            beatPhase += rate;

            //心缩/心舒包络：lub 重拍 + dub 轻拍的双峰
            float p = beatPhase - MathF.Floor(beatPhase);
            float lub = MathF.Exp(-p * 26f);
            float dubT = p - 0.18f;
            float dub = dubT > 0f ? MathF.Exp(-dubT * 30f) * 0.55f : 0f;
            beatEnvelope = MathHelper.Clamp((lub + dub) * strength, 0f, 1.25f);

            //整数跨越=心缩拍
            int beatIndex = (int)MathF.Floor(beatPhase);
            if (beatIndex > lastBeatIndex && beatPhase > 0.001f) {
                lastBeatIndex = beatIndex;
                OnBeat(strength, dub: false);
            }
            //0.18 相位=心舒回响拍
            if (beatIndex > lastDubIndex && p >= 0.18f) {
                lastDubIndex = beatIndex;
                OnBeat(strength * 0.6f, dub: true);
            }
        }

        /// <summary>一拍：双层闷响 + 心口微光圈；重要拍由调用处补震屏</summary>
        private void OnBeat(float strength, bool dub) {
            if (Main.dedServ || CurrentAlpha() < 0.3f) {
                return;
            }
            SoundEngine.PlaySound(SoundID.DD2_MonkStaffGroundImpact with {
                Volume = (dub ? 0.15f : 0.30f) * strength,
                Pitch = dub ? -0.72f : -0.92f,
                MaxInstances = 3
            }, Projectile.Center);
            SoundEngine.PlaySound(SoundID.NPCHit57 with {
                Volume = (dub ? 0.08f : 0.15f) * strength,
                Pitch = -0.55f,
                MaxInstances = 3
            }, Projectile.Center);
            if (!dub) {
                PRTLoader.NewParticle<PRT_DWave>(Projectile.Center, Vector2.Zero,
                    BloodDeep * 0.8f, 0.06f + 0.04f * strength)
                    ?.Configure(new Vector2(0.9f, 1f), 0f, 0.16f + 0.1f * strength, 9);
            }
        }

        //==================== 出水演出 ====================

        private void UpdateEmerge(Player owner, KikasaDomainPlayer domain) {
            float lakeY = domain.LakeWorldY;
            int t = (int)StateTimer;
            bool viewed = ViewedOwner;

            if (t < OmenEnd) {
                //预兆：湖底先传来两记闷心跳，涟漪向出水点收拢，水下血光自深处鼓起
                Projectile.velocity = new Vector2(0f, -0.55f);
                if (t == 10 || t == 34) {
                    SoundEngine.PlaySound(SoundID.DD2_MonkStaffGroundImpact with {
                        Volume = t == 10 ? 0.28f : 0.4f,
                        Pitch = -1f,
                        MaxInstances = 2
                    }, new Vector2(Projectile.Center.X, lakeY));
                    if (viewed) {
                        KikasaDomainDeco.RippleAt(new Vector2(Projectile.Center.X, lakeY), t == 10 ? 0.8f : 1.2f);
                        ShakeViewer(t == 10 ? 0.8f : 1.4f);
                    }
                }
                if (viewed && t % 5 == 2) {
                    float converge = 1f - t / (float)OmenEnd;
                    float side = t / 5 % 2 == 0 ? 1f : -1f;
                    KikasaDomainDeco.RippleAt(
                        new Vector2(Projectile.Center.X + side * converge * 90f, lakeY),
                        0.4f + (1f - converge) * 0.6f);
                }
                return;
            }

            if (!breachDone) {
                //破水拍：甲壳裹心一帧起速顶出湖面，浪冠量级全场最大
                breachDone = true;
                Projectile.velocity = new Vector2(0f, -12.5f);
                SoundEngine.PlaySound(SoundID.Roar with { Volume = 0.62f, Pitch = -0.82f, MaxInstances = 2 }, Projectile.Center);
                if (viewed) {
                    BreachBurst(new Vector2(Projectile.Center.X, lakeY));
                }
            }

            //升起：指数衰减 + 末段锚点收拢，禁匀速
            Projectile.velocity.Y *= 0.95f;
            Projectile.velocity.X = 0f;
            Vector2 hoverAnchor = new(Projectile.Center.X, lakeY - 238f);
            if (t > 90) {
                Projectile.Center = Vector2.Lerp(Projectile.Center, hoverAnchor, 0.045f);
            }

            //升起期血水成帘往下淌
            if (viewed && t < RiseEnd && t % 2 == 0) {
                Vector2 dropPos = Projectile.Center + new Vector2(
                    Main.rand.NextFloat(-44f, 44f), Main.rand.NextFloat(0f, 46f));
                PRTLoader.NewParticle<PRT_GhostRainDrop>(dropPos,
                    new Vector2(Main.rand.NextFloat(-0.4f, 0.4f), Main.rand.NextFloat(2.6f, 4.2f)),
                    BloodMain * Main.rand.NextFloat(0.4f, 0.6f),
                    Main.rand.NextFloat(0.5f, 0.8f))
                    ?.Configure(Main.rand.Next(16, 30), 0f);
            }

            //血管逐条从湖里被拽起接驳
            UpdateVesselRaise(domain, viewed);

            if (!awakenDone && t >= AwakenFrame) {
                //觉醒拍：甲壳崩碎露出裸心，第一次心跳启动一切
                awakenDone = true;
                ShellCrack();
                beatPhase = 0.0001f;
                lastBeatIndex = -1;
                lastDubIndex = -1;
            }

            if (t >= EmergeTotal) {
                State = StateFollow;
                StateTimer = 0;
                StateParam = 0;
                attackCooldown = 60;
                Projectile.netUpdate = Main.myPlayer == Projectile.owner;
            }
        }

        /// <summary>出水期血管拽起进度：按帧表逐条接驳，接驳帧给确认拍</summary>
        private void UpdateVesselRaise(KikasaDomainPlayer domain, bool viewed) {
            int t = (int)StateTimer;
            for (int i = 0; i < VesselCount; i++) {
                int start = VesselRaiseStart + i * VesselRaiseGap;
                float raise = MathHelper.Clamp((t - start) / (float)VesselRaiseFrames, 0f, 1f);
                if (raise > 0f && vesselRaise[i] <= 0f) {
                    //起拽拍：入水点先炸开一蓬水花——它是被拽出来的
                    SoundEngine.PlaySound(SoundID.SplashWeak with { Volume = 0.5f, Pitch = -0.5f + i * 0.06f, MaxInstances = 3 },
                        new Vector2(vesselEntryX[i], domain.LakeWorldY));
                    if (viewed) {
                        Vector2 hit = new(vesselEntryX[i], domain.LakeWorldY);
                        KikasaDomainDeco.SplashAt(hit, 7);
                        KikasaDomainDeco.RippleAt(hit, 1.0f);
                    }
                }
                if (raise >= 1f && !vesselDocked[i]) {
                    //接驳拍：贴上心壁的一声湿响
                    vesselDocked[i] = true;
                    SoundEngine.PlaySound(SoundID.NPCHit13 with { Volume = 0.35f, Pitch = -0.6f, MaxInstances = 3 }, Projectile.Center);
                    if (viewed) {
                        ShakeViewer(0.8f);
                    }
                }
                vesselRaise[i] = raise;
            }
        }

        /// <summary>破水浪冠：压轴级——涟漪/水花/抛血柱/血雾全量级压过毁灭者一头</summary>
        private void BreachBurst(Vector2 hit) {
            KikasaDomainDeco.RippleAt(hit, 3.4f);
            KikasaDomainDeco.RippleAt(hit + new Vector2(64f, 0f), 1.4f);
            KikasaDomainDeco.RippleAt(hit - new Vector2(58f, 0f), 1.3f);
            KikasaDomainDeco.SplashAt(hit + new Vector2(-24f, 0f), 16);
            KikasaDomainDeco.SplashAt(hit + new Vector2(24f, 0f), 16);
            KikasaDomainDeco.BloodBurst(hit, 24, 1.6f);

            for (int i = 0; i < 34; i++) {
                float angle = -MathHelper.Pi * (0.08f + 0.84f * i / 33f);
                float speed = Main.rand.NextFloat(3.6f, 9f);
                PRTLoader.NewParticle<PRT_KikasaBloodGlob>(
                    hit + new Vector2(Main.rand.NextFloat(-40f, 40f), -4f),
                    angle.ToRotationVector2() * speed,
                    Main.rand.NextBool(3) ? BloodDeep : BloodMain,
                    Main.rand.NextFloat(0.6f, 1f))?.Configure(Main.rand.Next(26, 44));
            }
            for (int i = 0; i < 12; i++) {
                PRTLoader.NewParticle<PRT_KikasaBloodGlob>(
                    hit + new Vector2(Main.rand.NextFloat(-12f, 12f), -8f),
                    new Vector2(Main.rand.NextFloat(-1f, 1f), -Main.rand.NextFloat(10f, 15f)),
                    BloodMain * 0.9f, Main.rand.NextFloat(0.65f, 1.05f))
                    ?.Configure(Main.rand.Next(38, 56));
            }
            for (int i = 0; i < 5; i++) {
                PRTLoader.NewParticle<PRT_GhostRainMist>(
                    hit + new Vector2(Main.rand.NextFloat(-48f, 48f), -14f),
                    new Vector2(Main.rand.NextFloat(-0.35f, 0.35f), -Main.rand.NextFloat(0.4f, 1f)),
                    MistBlood * 0.85f, Main.rand.NextFloat(0.85f, 1.2f))
                    ?.Configure(Main.rand.Next(80, 120));
            }
            PRTLoader.NewParticle<PRT_DWave>(hit, Vector2.Zero, BloodDeep, 0.1f)
                ?.Configure(new Vector2(0.5f, 1f), -MathHelper.PiOver2, 0.46f, 13);

            SoundEngine.PlaySound(SoundID.SplashWeak with { Volume = 1f, Pitch = -0.5f, MaxInstances = 2 }, hit);
            SoundEngine.PlaySound(SoundID.DD2_MonkStaffGroundImpact with { Volume = 0.65f, Pitch = -0.8f, MaxInstances = 1 }, hit);
            ShakeViewer(7f);
        }

        /// <summary>甲壳崩碎拍：帧 0 的壳一声脆响炸成血珠，裸心露出即第一次心跳</summary>
        private void ShellCrack() {
            SoundEngine.PlaySound(SoundID.NPCHit2 with { Volume = 0.6f, Pitch = -0.35f, MaxInstances = 2 }, Projectile.Center);
            SoundEngine.PlaySound(SoundID.NPCHit13 with { Volume = 0.5f, Pitch = -0.5f, MaxInstances = 2 }, Projectile.Center);
            if (!Main.dedServ) {
                for (int i = 0; i < 16; i++) {
                    Vector2 vel = Main.rand.NextVector2Unit() * Main.rand.NextFloat(2.4f, 6.4f);
                    PRTLoader.NewParticle<PRT_KikasaBloodGlob>(
                        Projectile.Center + Main.rand.NextVector2Circular(36f, 50f),
                        vel, Main.rand.NextBool(3) ? BloodDeep : BloodMain,
                        Main.rand.NextFloat(0.5f, 0.85f))?.Configure(Main.rand.Next(20, 34), 0.3f);
                }
                PRTLoader.NewParticle<PRT_DWave>(Projectile.Center, Vector2.Zero, FoamGlow, 0.08f)
                    ?.Configure(new Vector2(0.9f, 1f), 0f, 0.34f, 12);
            }
            if (ViewedOwner) {
                ShakeViewer(4f);
            }
        }

        //==================== 锚定跟随 ====================

        private void UpdateFollow(Player owner, KikasaDomainPlayer domain, bool authority) {
            float lakeY = domain.LakeWorldY;

            //悬浮位置基本固定：血管拖着缓慢挪锚，横向被血管拽着迟滞跟随
            Vector2 anchor = new(owner.Center.X, lakeY - 238f
                + MathF.Sin(Main.GlobalTimeWrappedHourly * 0.9f + Seed) * 7f);
            Vector2 to = anchor - Projectile.Center;
            if (to.Length() > 2400f) {
                //跟丢硬贴回，血管入水点一并重置
                Projectile.Center = anchor;
                Projectile.velocity = Vector2.Zero;
                for (int i = 0; i < VesselCount; i++) {
                    vesselEntryX[i] = anchor.X + VesselSpread[i];
                }
                Projectile.netUpdate = authority;
                return;
            }
            Vector2 desired = to * 0.016f;
            const float maxSpeed = 6.5f;
            if (desired.Length() > maxSpeed) {
                desired = desired.SafeNormalize(Vector2.Zero) * maxSpeed;
            }
            Projectile.velocity = Vector2.Lerp(Projectile.velocity, desired, 0.08f);

            //闲时心尖偶发凝珠
            if (!Main.dedServ && Main.rand.NextBool(26)) {
                PRTLoader.NewParticle<PRT_KikasaBloodGlob>(
                    Projectile.Center + new Vector2(Main.rand.NextFloat(-30f, 30f), Main.rand.NextFloat(46f, 66f)),
                    new Vector2(0f, Main.rand.NextFloat(0.6f, 1.4f)),
                    BloodDeep * Main.rand.NextFloat(0.4f, 0.55f),
                    Main.rand.NextFloat(0.35f, 0.55f))?.Configure(Main.rand.Next(20, 34), 0.3f);
            }

            //出手裁决：幻月球→血管鞭→幻月血芒轮转，规则确定性、owner 盖章；
            //鞭需要距离才读得出来，太近换球
            int target = FindTarget(owner);
            if (target >= 0 && attackCooldown <= 0 && StateTimer > 34) {
                attackIndex++;
                int pick = attackIndex % 3;
                int next = pick switch {
                    1 => StateMoonOrbs,
                    2 => Vector2.Distance(Main.npc[target].Center, Projectile.Center) > 340f
                        ? StateVesselWhip : StateMoonOrbs,
                    _ => StateMoonRay,
                };
                State = next;
                StateTimer = 0;
                StateParam = 0;
                Projectile.netUpdate = authority;
            }
        }

        //==================== 幻月球 ====================

        private void UpdateMoonOrbs(Player owner, KikasaDomainPlayer domain, bool authority) {
            int t = (int)StateTimer;
            int target = FindTarget(owner);

            //目标没了就别空挤：最后一颗之前随时收场（规则确定性，各端同判）
            if (target < 0 && t < OrbSqueezeFrames[^1]) {
                EndAttack(authority, 50);
                return;
            }
            Projectile.velocity *= 0.9f;

            //三次心跳拍上各挤出一颗：挤球帧与心跳时钟同源（38f/拍）
            for (int k = 0; k < OrbSqueezeFrames.Length; k++) {
                if (t != OrbSqueezeFrames[k]) {
                    continue;
                }
                //挤出的湿噗与心口涌血
                SoundEngine.PlaySound(SoundID.Item95 with { Volume = 0.5f, Pitch = -0.5f, MaxInstances = 3 }, Projectile.Center);
                SoundEngine.PlaySound(SoundID.NPCHit57 with { Volume = 0.3f, Pitch = -0.3f, MaxInstances = 3 }, Projectile.Center);
                Vector2 mouth = Projectile.Center + new Vector2((k - 1) * 16f, 44f);
                if (!Main.dedServ) {
                    for (int i = 0; i < 7; i++) {
                        PRTLoader.NewParticle<PRT_KikasaBloodGlob>(mouth + Main.rand.NextVector2Circular(6f, 6f),
                            Main.rand.NextVector2Circular(1.6f, 1.6f) + new Vector2(0f, 1.2f),
                            Main.rand.NextBool(3) ? BloodDeep : BloodMain,
                            Main.rand.NextFloat(0.4f, 0.65f))?.Configure(Main.rand.Next(16, 26), 0.2f);
                    }
                }
                if (ViewedOwner) {
                    ShakeViewer(1.2f);
                }
                if (authority && target >= 0) {
                    int damage = (int)owner.GetTotalDamage(DamageClass.Summon).ApplyTo(OrbDamage);
                    //初速缓慢下漂，寻的交给球自己；spawn 参数带齐目标与相位
                    Vector2 vel = new((k - 1) * 0.9f, 1.6f);
                    Projectile.NewProjectile(Projectile.GetSource_FromAI(), mouth, vel,
                        ModContent.ProjectileType<KikasaMoonOrb>(), damage, 4f, Projectile.owner,
                        target, k);
                }
            }

            if (t >= OrbStateEnd) {
                EndAttack(authority, 110);
            }
        }

        //==================== 血管鞭 ====================

        private void UpdateVesselWhip(Player owner, KikasaDomainPlayer domain, bool authority) {
            int t = (int)StateTimer;
            int target = FindTarget(owner);

            Projectile.velocity *= 0.9f;

            if (t < WhipSpawnFrame) {
                //攥紧 tell：心脏鼓起憋劲（BodyScale 侧读 StateParam=0 窗口）
                if (t == 2) {
                    SoundEngine.PlaySound(SoundID.NPCHit57 with { Volume = 0.4f, Pitch = -0.75f, MaxInstances = 2 }, Projectile.Center);
                }
                if (target < 0) {
                    EndAttack(authority, 45);
                }
                return;
            }

            if (t == WhipSpawnFrame && authority) {
                //owner 甩出鞭：落点=目标脚下湖面（带前置量），spawn 参数自带打击高度与甩向
                if (target < 0) {
                    EndAttack(authority, 45);
                    return;
                }
                NPC npc = Main.npc[target];
                float strikeX = MathHelper.Clamp(npc.Center.X + npc.velocity.X * 14f,
                    Projectile.Center.X - 1300f, Projectile.Center.X + 1300f);
                float side = npc.Center.X >= Projectile.Center.X ? -1f : 1f;
                int damage = (int)owner.GetTotalDamage(DamageClass.Summon).ApplyTo(ContactDamage);
                Projectile.NewProjectile(Projectile.GetSource_FromAI(),
                    new Vector2(strikeX, domain.LakeWorldY), Vector2.Zero,
                    ModContent.ProjectileType<KikasaMoonVesselWhip>(), damage, 8f, Projectile.owner,
                    npc.Center.Y, side);
            }

            if (t >= WhipStateEnd) {
                EndAttack(authority, 130);
            }
        }

        //==================== 幻月血芒 ====================

        /// <summary>充能进度 0~1</summary>
        private float ChargeT() {
            if (State != StateMoonRay) {
                return 0f;
            }
            return MathHelper.Clamp((StateTimer - RayLockEnd) / (float)RayChargeFrames, 0f, 1f);
        }

        /// <summary>湖面变暗程度 0~1：充能爬升、静默持满、开火即散</summary>
        private float ChargeDim() {
            if (State != StateMoonRay) {
                return 0f;
            }
            int t = (int)StateTimer;
            if (t < RayLockEnd) {
                return 0f;
            }
            if (t < RayChargeEnd) {
                return MathHelper.SmoothStep(0f, 1f, MathHelper.Clamp((ChargeT() - 0.12f) / 0.7f, 0f, 1f));
            }
            if (t < RayFireFrame) {
                return 1f;
            }
            //开火：光炸回来
            return MathHelper.Clamp(1f - (t - RayFireFrame) / 7f, 0f, 1f);
        }

        /// <summary>竖缝睁开度 0~1：睁眼段弹性张开，射线期全开，收势缓合</summary>
        private float SlitOpen() {
            if (State != StateMoonRay) {
                return 0f;
            }
            int t = (int)StateTimer;
            if (t < RayChargeEnd) {
                return 0f;
            }
            if (t < RayEyeEnd) {
                float e = (t - RayChargeEnd) / (float)(RayEyeEnd - RayChargeEnd);
                //弹性张开：过冲一拍再落定
                return MathHelper.Clamp(1f - MathF.Cos(e * MathHelper.PiOver2 * 1.35f) * (1f - e * 0.4f), 0f, 1.15f);
            }
            if (t < RayBeamEnd) {
                return 1f;
            }
            return MathHelper.Clamp(1f - (t - RayBeamEnd) / 20f, 0f, 1f);
        }

        private void UpdateMoonRay(Player owner, KikasaDomainPlayer domain, bool authority) {
            int t = (int)StateTimer;
            int target = FindTarget(owner);

            //充能前半段目标没了就散功；后半段已经箭在弦上，照最后的方向轰
            if (target < 0 && t < RayLockEnd + 60) {
                EndAttack(authority, 60);
                return;
            }
            if (target >= 0) {
                chargeAimAngle = (Main.npc[target].Center - Projectile.Center).ToRotation();
            }

            if (t < RayLockEnd) {
                //锁定停悬：刹死，血管开始绷紧
                Projectile.velocity *= 0.78f;
                return;
            }

            if (t < RayChargeEnd) {
                //长充能：心跳骤然加速（BeatRate 侧），泵血频率随相位自然拉满；
                //光向心脏汇聚——72% 后一切静默
                Projectile.velocity *= 0.92f;
                float c = ChargeT();

                if (!silenceLatched && t >= RaySilenceFrame) {
                    //静默拍：心跳骤停，一记吞咽后万籁俱寂
                    silenceLatched = true;
                    SoundEngine.PlaySound(SoundID.MaxMana with { Volume = 0.6f, Pitch = -0.9f, MaxInstances = 2 }, Projectile.Center);
                }

                if (!Main.dedServ && c < 0.72f) {
                    //汇聚血珠：自四周与湖面被吸进心脏
                    if (Main.rand.NextFloat() < 0.3f + 0.5f * c) {
                        Vector2 from = Projectile.Center + Main.rand.NextVector2Unit() * Main.rand.NextFloat(120f, 380f);
                        PRTLoader.NewParticle<PRT_KikasaBloodGlob>(from,
                            (Projectile.Center - from) * 0.085f,
                            BloodMain * (0.35f + c * 0.3f), Main.rand.NextFloat(0.32f, 0.55f))
                            ?.Configure(13, 0f);
                    }
                    if (Main.rand.NextBool(3)) {
                        //湖面的光也被拔起来
                        Vector2 from = new(Projectile.Center.X + Main.rand.NextFloat(-320f, 320f), domain.LakeWorldY - 4f);
                        PRTLoader.NewParticle<PRT_Spark>(from, (Projectile.Center - from) * 0.05f,
                            FoamGlow, Main.rand.NextFloat(0.7f, 1.2f))?.Configure(false, 18);
                    }
                }
                //低鸣震屏随充能平方爬升；静默段不震——死寂才吓人
                if (c < 0.72f && t % 6 == 0 && ViewedOwner) {
                    ShakeViewer(0.5f + 2.2f * c * c);
                }
                return;
            }

            if (t < RayEyeEnd) {
                //睁眼：竖缝裂开露出瞳状芯
                Projectile.velocity *= 0.7f;
                if (t == RayChargeEnd + 1) {
                    SoundEngine.PlaySound(SoundID.NPCHit2 with { Volume = 0.55f, Pitch = -0.2f, MaxInstances = 2 }, Projectile.Center);
                    SoundEngine.PlaySound(SoundID.NPCHit57 with { Volume = 0.45f, Pitch = -0.1f, MaxInstances = 2 }, Projectile.Center);
                    if (!Main.dedServ) {
                        //缝口喷出细血
                        for (int i = 0; i < 8; i++) {
                            PRTLoader.NewParticle<PRT_KikasaBloodGlob>(
                                Projectile.Center + new Vector2(Main.rand.NextFloat(-6f, 6f), Main.rand.NextFloat(-40f, 40f)),
                                new Vector2(Main.rand.NextFloat(-2.6f, 2.6f), Main.rand.NextFloat(-1.4f, 1.4f)),
                                BloodMain, Main.rand.NextFloat(0.35f, 0.55f))?.Configure(Main.rand.Next(14, 22), 0.25f);
                        }
                    }
                    if (ViewedOwner) {
                        ShakeViewer(3f);
                    }
                }
                return;
            }

            if (!fireLatched && t >= RayFireFrame) {
                //开火拍：owner 定扫荡参数并生成射线；心脏重后坐
                fireLatched = true;
                float aim = chargeAimAngle;
                if (target >= 0) {
                    aim = (Main.npc[target].Center - Projectile.Center).ToRotation();
                }
                float side = MathF.Cos(aim) >= 0f ? 1f : -1f;
                float startAngle = aim - KikasaMoonRay.ArcHalf * side;
                float sweepSpeed = 2f * KikasaMoonRay.ArcHalf / KikasaMoonRay.SweepFrames * side;

                Projectile.velocity = -aim.ToRotationVector2() * 7f;
                SoundEngine.PlaySound(SoundID.Zombie104 with { Volume = 1f, Pitch = -0.15f, MaxInstances = 2 }, Projectile.Center);
                SoundEngine.PlaySound(SoundID.ForceRoar with { Volume = 0.7f, Pitch = -0.4f, MaxInstances = 2 }, Projectile.Center);
                if (ViewedOwner) {
                    ShakeViewer(7f);
                }
                if (authority) {
                    int damage = (int)owner.GetTotalDamage(DamageClass.Summon).ApplyTo(RayDamage);
                    Projectile.NewProjectile(Projectile.GetSource_FromAI(),
                        Projectile.Center, Vector2.Zero,
                        ModContent.ProjectileType<KikasaMoonRay>(), damage, 6f, Projectile.owner,
                        startAngle, sweepSpeed);
                }
            }

            if (t < RayBeamEnd) {
                //射线期：钉在原地扛后坐，缝口跟着权威光束角走（表现见 DrawGlow）
                Projectile.velocity *= 0.86f;
                Projectile beam = KikasaMoonRay.FindFor(Projectile.owner);
                if (beam != null) {
                    chargeAimAngle = beam.rotation;
                }
                if (t % 6 == 0 && ViewedOwner) {
                    ShakeViewer(1.8f);
                }
                return;
            }

            //收势：脱力下沉半拍，心跳缓慢复搏
            Projectile.velocity *= 0.9f;
            Projectile.velocity.Y += 0.06f;
            if (t >= RayRecoverEnd) {
                EndAttack(authority, 240);
            }
        }

        private void EndAttack(bool authority, int cooldown) {
            State = StateFollow;
            StateTimer = 0;
            StateParam = 0;
            attackCooldown = cooldown;
            Projectile.netUpdate = authority;
        }

        //==================== 停搏溶解（压轴谢幕）====================

        private void UpdateDissolve(KikasaDomainPlayer domain, bool authority) {
            int t = (int)StateTimer;
            bool lakeAlive = domain.AnyActive && domain.RiseT > 0.5f;
            float lakeY = domain.LakeWorldY;
            bool viewed = ViewedOwner;

            //心脏悬停原地缓慢下沉——供血断了，浮力也跟着走
            Projectile.velocity *= 0.9f;
            Projectile.velocity.Y += 0.015f;

            //血管一根根松脱坠湖
            for (int i = 0; i < VesselCount; i++) {
                int start = DetachStart + i * DetachGap;
                float drain = MathHelper.Clamp((t - start) / (float)DetachFrames, 0f, 1f);
                if (drain > 0f && !vesselDetached[i]) {
                    vesselDetached[i] = true;
                    SoundEngine.PlaySound(SoundID.NPCHit13 with { Volume = 0.4f, Pitch = -0.4f, MaxInstances = 3 }, Projectile.Center);
                    if (viewed && lakeAlive) {
                        Vector2 hit = new(vesselEntryX[i], lakeY);
                        KikasaDomainDeco.SplashAt(hit, 8);
                        KikasaDomainDeco.RippleAt(hit, 1.1f);
                    }
                }
                vesselDrain[i] = drain;
            }

            //倾泻拍：最后一拍停搏后，整颗化作大股血水倒回湖里
            if (!pourLatched && t >= PourStart) {
                pourLatched = true;
                SoundEngine.PlaySound(SoundID.SplashWeak with { Volume = 0.9f, Pitch = -0.6f, MaxInstances = 2 }, Projectile.Center);
                SoundEngine.PlaySound(SoundID.NPCHit57 with { Volume = 0.5f, Pitch = -0.9f, MaxInstances = 2 }, Projectile.Center);
                if (viewed) {
                    ShakeViewer(3f);
                }
            }

            //倾泻期：血柱着水点持续搅湖
            if (pourLatched && t < PourEnd && lakeAlive) {
                if (viewed && t % 4 == 0) {
                    Vector2 hit = new(Projectile.Center.X + Main.rand.NextFloat(-18f, 18f), lakeY);
                    KikasaDomainDeco.RippleAt(hit, Main.rand.NextFloat(0.8f, 1.5f));
                    if (t % 12 == 0) {
                        KikasaDomainDeco.SplashAt(hit, 8);
                        KikasaDomainDeco.BloodBurst(hit, 8, 0.9f);
                    }
                }
                if (t % 9 == 0) {
                    SoundEngine.PlaySound(SoundID.SplashWeak with { Volume = 0.5f, Pitch = -0.35f, MaxInstances = 3 },
                        new Vector2(Projectile.Center.X, lakeY));
                }
                //倾泻本体的重血珠随柱而下
                if (!Main.dedServ && t % 2 == 0) {
                    PRTLoader.NewParticle<PRT_KikasaBloodGlob>(
                        Projectile.Center + new Vector2(Main.rand.NextFloat(-24f, 24f), Main.rand.NextFloat(0f, 40f)),
                        new Vector2(Main.rand.NextFloat(-0.5f, 0.5f), Main.rand.NextFloat(6f, 11f)),
                        Main.rand.NextBool(3) ? BloodDeep : BloodMain,
                        Main.rand.NextFloat(0.6f, 1f))?.Configure(Main.rand.Next(20, 32), 0.4f);
                }
            }

            //湖不在了：原地化珠，不走倾泻
            if (!lakeAlive && !Main.dedServ && t % 3 == 0 && CurrentAlpha() > 0.15f) {
                PRTLoader.NewParticle<PRT_KikasaBloodGlob>(
                    Projectile.Center + Main.rand.NextVector2Circular(40f, 56f),
                    new Vector2(Main.rand.NextFloat(-0.6f, 0.6f), Main.rand.NextFloat(1.5f, 3f)),
                    BloodMain * 0.55f, Main.rand.NextFloat(0.4f, 0.65f))
                    ?.Configure(Main.rand.Next(14, 24), 0.3f);
            }

            if (authority && t >= DissolveTotal) {
                Projectile.Kill();
            }
            else if (!authority && t >= DissolveTotal + 10) {
                Projectile.Kill();
            }
        }

        //==================== 血管锚点推进 ====================

        /// <summary>入水点迟滞追心脏：血管拖着走，入水点跟着迁移</summary>
        private void UpdateVesselAnchors() {
            for (int i = 0; i < VesselCount; i++) {
                float want = Projectile.Center.X + VesselSpread[i];
                //出水期从近处摊开到全幅
                if (State == StateEmerge) {
                    want = Projectile.Center.X + VesselSpread[i] * MathHelper.Lerp(0.18f, 1f,
                        MathHelper.Clamp((StateTimer - VesselRaiseStart) / 70f, 0f, 1f));
                }
                vesselEntryX[i] = MathHelper.Lerp(vesselEntryX[i], want, 0.028f);
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
            float bestDist = 1150f;
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

        /// <summary>射线锚定用：心脏中心（含节拍位移）</summary>
        internal Vector2 HeartPos => Projectile.Center + new Vector2(0f, beatEnvelope * 5f);

        //==================== 表现参数 ====================

        private float CurrentAlpha() {
            int t = (int)StateTimer;
            return State switch {
                StateEmerge => t < OmenEnd ? 0f : MathHelper.Clamp((t - OmenEnd) / 5f, 0f, 1f),
                StateDissolve => t < PourStart ? 1f
                    : MathHelper.Clamp(1f - (t - PourStart) / (float)(PourEnd - PourStart - 6), 0f, 1f),
                _ => 1f,
            };
        }

        /// <summary>uForm：1=全血水 0=真身；心脏常态比克眼更血——它本来就是血做的器官</summary>
        private float CurrentForm() {
            int t = (int)StateTimer;
            float steady = 0.42f + MathF.Sin(Main.GlobalTimeWrappedHourly * 2.7f + Seed) * 0.05f;
            return State switch {
                StateEmerge => t < OmenEnd
                    ? 1f
                    : MathHelper.Lerp(1f, steady, SmoothStep01(MathHelper.Clamp((t - OmenEnd) / (float)(RiseEnd - OmenEnd), 0f, 1f))),
                StateDissolve => MathHelper.Clamp(steady + MathHelper.Clamp((t - 90) / 60f, 0f, 1f) * (1f - steady), 0f, 1f),
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
            return 1f - MathHelper.Clamp((t - RiseEnd) / 14f, 0f, 1f);
        }

        private float CurrentDissolve()
            => State == StateDissolve
                ? MathF.Pow(MathHelper.Clamp((StateTimer - PourStart + 10) / (float)(PourEnd - PourStart), 0f, 1f), 0.9f)
                : 0f;

        /// <summary>体积：心缩拍猛缩回弹；充能鼓胀、静默前收拢（爆发前先变小）；
        /// 倾泻期竖向瘪掉——血袋被倒空</summary>
        private void BodyScale(out float sx, out float sy) {
            float s = BaseScale * (1f - 0.085f * beatEnvelope);
            int t = (int)StateTimer;
            if (State == StateEmerge && t >= OmenEnd && t < OmenEnd + 10) {
                s *= 1f + 0.09f * (1f - (t - OmenEnd) / 10f);
            }
            else if (State == StateMoonRay) {
                float c = ChargeT();
                s *= 1f + 0.07f * MathHelper.Clamp(c / 0.72f, 0f, 1f);
                if (t >= RaySilenceFrame && t < RayChargeEnd) {
                    //静默收拢
                    float k = (t - RaySilenceFrame) / (float)(RayChargeEnd - RaySilenceFrame);
                    s *= 1f - 0.075f * SmoothStep01(k);
                }
            }
            else if (State == StateVesselWhip && t < WhipSpawnFrame) {
                s *= 1f + 0.05f * (t / (float)WhipSpawnFrame);
            }
            else if (State == StateMoonOrbs) {
                //挤球前一拍鼓起
                for (int k = 0; k < OrbSqueezeFrames.Length; k++) {
                    int d = OrbSqueezeFrames[k] - t;
                    if (d > 0 && d <= 8) {
                        s *= 1f + 0.06f * (1f - d / 8f);
                    }
                }
            }
            sx = s;
            sy = s;
            if (State == StateDissolve && (int)StateTimer >= PourStart) {
                float pourT = MathHelper.Clamp(((int)StateTimer - PourStart) / (float)(PourEnd - PourStart), 0f, 1f);
                sy *= 1f - pourT * 0.66f;
                sx *= 1f + pourT * 0.12f - pourT * pourT * 0.3f;
            }
        }

        /// <summary>心跳帧：帧 0=甲壳，觉醒后 1~4 按拍内相位走完一次收缩循环</summary>
        private int HeartFrame() {
            if (State == StateEmerge && !awakenDone) {
                return 0;
            }
            float p = beatPhase - MathF.Floor(beatPhase);
            return 1 + Math.Clamp((int)(p * 4f), 0, 3);
        }

        private static float SmoothStep01(float t) => t * t * (3f - 2f * t);

        //==================== 绘制 ====================

        public override bool PreDraw(ref Color lightColor) {
            Main.instance.LoadNPC(NPCID.MoonLordCore);
            Texture2D tex = TextureAssets.Npc[NPCID.MoonLordCore]?.Value;
            if (tex == null) {
                return false;
            }
            KikasaDomainPlayer domain = Owner?.GetModPlayer<KikasaDomainPlayer>();
            if (domain == null) {
                return false;
            }

            SpriteBatch sb = Main.spriteBatch;
            float alpha = CurrentAlpha();

            //充能暗幕（画在最底：湖面与心脏之间的光被吸走）
            DrawChargeVeil(sb, domain);

            //血管条带（心脏身后）+ 倾泻血柱
            DrawVesselLayer(sb, domain);

            //心脏本体：血湖材质
            if (alpha > 0.01f) {
                DrawBody(sb, tex, alpha);
            }

            //加色层：预兆血光/心跳圈/泵血鼓包/竖缝瞳芯/汇聚流线
            DrawGlow(sb, domain, alpha);

            return false;
        }

        /// <summary>充能暗幕：软径向黑罩沉在心脏与湖面之间 + 水线暗带，
        /// 静默段持满、开火即散——湖面整体变暗变静的主载体</summary>
        private void DrawChargeVeil(SpriteBatch sb, KikasaDomainPlayer domain) {
            float dim = ChargeDim();
            if (dim < 0.02f) {
                return;
            }
            //暗幕必须用真 alpha 的 Extra_98——黑底 SoftGlow 的 alpha 通道是全 255，
            //在 AlphaBlend 里压不出软径向，只会糊出一整块硬边黑矩形
            Texture2D veil = CWRAsset.Extra_98?.Value;
            if (veil == null) {
                return;
            }
            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

            Vector2 origin = veil.Size() * 0.5f;
            float lakeY = domain.LakeWorldY;
            Vector2 mid = new(Projectile.Center.X, (Projectile.Center.Y + lakeY) * 0.5f);
            //主暗池：罩住心脏到湖面的整片区域（×2 补偿 Extra_98 更紧的径向衰减）
            sb.Draw(veil, mid - Main.screenPosition, null, Color.Black * (0.4f * dim), 0f,
                origin, new Vector2(1700f / veil.Width, 900f / veil.Height) * 2f, SpriteEffects.None, 0f);
            //水线暗带：湖面失光
            sb.Draw(veil, new Vector2(Projectile.Center.X, lakeY + 10f) - Main.screenPosition, null,
                Color.Black * (0.34f * dim), 0f, origin,
                new Vector2(2100f / veil.Width, 240f / veil.Height) * 2f, SpriteEffects.None, 0f);

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
        }

        /// <summary>血管与倾泻血柱：KikasaHand 血水条带材质，世界空间三角带；
        /// 无着色器时线链回退</summary>
        private void DrawVesselLayer(SpriteBatch sb, KikasaDomainPlayer domain) {
            bulgeGlows.Clear();
            float alpha = CurrentAlpha();
            bool anyVessel = false;
            for (int i = 0; i < VesselCount; i++) {
                if (vesselRaise[i] > 0.02f && vesselDrain[i] < 0.98f) {
                    anyVessel = true;
                    break;
                }
            }
            bool pouring = State == StateDissolve && (int)StateTimer >= PourStart && (int)StateTimer < PourEnd + 8;
            if (!anyVessel && !pouring || State == StateEmerge && !breachDone) {
                return;
            }

            Effect fx = EffectLoader.KikasaHand?.Value;
            Texture2D noise = CWRAsset.NoiseSoft01?.Value;

            sb.End();

            if (fx != null && noise != null) {
                GraphicsDevice device = Main.instance.GraphicsDevice;
                BlendState prevBlend = device.BlendState;
                RasterizerState prevRaster = device.RasterizerState;
                device.BlendState = BlendState.AlphaBlend;
                device.RasterizerState = RasterizerState.CullNone;

                fx.Parameters["transformMatrix"]?.SetValue(VaultUtils.GetTransfromMatrix());
                fx.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
                fx.Parameters["uNoiseTex"]?.SetValue(noise);

                for (int i = 0; i < VesselCount; i++) {
                    if (vesselRaise[i] <= 0.02f || vesselDrain[i] >= 0.98f) {
                        continue;
                    }
                    var verts = BuildVesselStrip(i, domain);
                    if (verts == null) {
                        continue;
                    }
                    fx.Parameters["uOpacity"]?.SetValue(alpha * MathHelper.Clamp(vesselRaise[i] * 3f, 0f, 1f));
                    fx.Parameters["uGrip"]?.SetValue(VesselGrip());
                    fx.Parameters["uSeed"]?.SetValue(Seed + i * 2.3f);
                    fx.Parameters["uFoam"]?.SetValue(VesselFoam(i));
                    fx.Parameters["uDrain"]?.SetValue(vesselDrain[i]);
                    foreach (EffectPass pass in fx.CurrentTechnique.Passes) {
                        pass.Apply();
                        device.DrawUserPrimitives(PrimitiveType.TriangleStrip, verts, 0, verts.Length - 2);
                    }
                }

                if (pouring) {
                    var verts = BuildPourStrip(domain);
                    if (verts != null) {
                        float pourT = MathHelper.Clamp(((int)StateTimer - PourStart) / (float)(PourEnd - PourStart), 0f, 1f);
                        fx.Parameters["uOpacity"]?.SetValue(MathHelper.Clamp(pourT * 6f, 0f, 1f)
                            * MathHelper.Clamp((1f - pourT) * 4f, 0f, 1f));
                        fx.Parameters["uGrip"]?.SetValue(0f);
                        fx.Parameters["uSeed"]?.SetValue(Seed * 1.7f);
                        fx.Parameters["uFoam"]?.SetValue(1f);
                        fx.Parameters["uDrain"]?.SetValue(0f);
                        foreach (EffectPass pass in fx.CurrentTechnique.Passes) {
                            pass.Apply();
                            device.DrawUserPrimitives(PrimitiveType.TriangleStrip, verts, 0, verts.Length - 2);
                        }
                    }
                }

                device.BlendState = prevBlend;
                device.RasterizerState = prevRaster;
            }
            else {
                //CPU 回退：血管画成折线
                Texture2D pixel = VaultAsset.placeholder2?.Value;
                if (pixel != null) {
                    sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp,
                        DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
                    Color arm = BloodDeep * (0.8f * alpha);
                    for (int i = 0; i < VesselCount; i++) {
                        if (vesselRaise[i] <= 0.02f || vesselDrain[i] >= 0.98f) {
                            continue;
                        }
                        Vector2 prev = default;
                        for (int s = 0; s < VesselSamples; s++) {
                            Vector2 pos = VesselPoint(i, s / (float)(VesselSamples - 1), domain, out _);
                            if (s > 0) {
                                Vector2 d = pos - prev;
                                float len = d.Length();
                                if (len > 0.5f) {
                                    sb.Draw(pixel, prev - Main.screenPosition, new Rectangle(0, 0, 1, 1), arm,
                                        MathF.Atan2(d.Y, d.X), new Vector2(0f, 0.5f),
                                        new Vector2(len, 9f), SpriteEffects.None, 0f);
                                }
                            }
                            prev = pos;
                        }
                    }
                    sb.End();
                }
            }

            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
        }

        /// <summary>血管绷紧度：鞭 tell 与充能期拉满，随心跳轻微搏动</summary>
        private float VesselGrip() {
            float grip = 0.12f + beatEnvelope * 0.2f;
            if (State == StateVesselWhip) {
                grip = 0.7f;
            }
            else if (State == StateMoonRay) {
                grip = MathHelper.Clamp(0.2f + ChargeT() * 0.6f, 0f, 0.8f);
            }
            return grip;
        }

        private float VesselFoam(int i) {
            //拽起与松脱时根口泡沫最烈
            float f = 0.35f;
            if (vesselRaise[i] < 1f) {
                f = 1f;
            }
            if (vesselDrain[i] > 0f && vesselDrain[i] < 1f) {
                f = 1f;
            }
            return f;
        }

        /// <summary>血管中线采样：根(湖面)→心壁锚点，弓弧+摆动+未接驳端下垂；
        /// out width 为该点半宽（含泵血鼓包）</summary>
        private Vector2 VesselPoint(int i, float t, KikasaDomainPlayer domain, out float width) {
            float lakeY = domain.LakeWorldY;
            Vector2 root = new(vesselEntryX[i], lakeY + 8f);
            BodyScale(out float sx, out float sy);
            Vector2 anchorDir = VesselAnchorAng[i].ToRotationVector2();
            Vector2 anchor = HeartPos + new Vector2(anchorDir.X * 46f * sx / BaseScale, anchorDir.Y * 52f * sy / BaseScale);

            //拽起进度：端点从水下走到心壁
            float raise = SmoothStep01(vesselRaise[i]);
            Vector2 endNow = Vector2.Lerp(new Vector2(root.X, root.Y + 26f), anchor, raise);

            Vector2 chord = endNow - root;
            Vector2 chordDir = chord.SafeNormalize(-Vector2.UnitY);
            Vector2 normal = new(-chordDir.Y, chordDir.X);
            float chordLen = chord.Length();

            float arcT = MathF.Sin(t * MathHelper.Pi);
            float bow = (16f + chordLen * 0.09f) * VesselBowDir[i] * arcT;
            float sway = MathF.Sin(t * 2.6f + Main.GlobalTimeWrappedHourly * 1.15f + i * 1.7f + Seed) * 4.5f * arcT;
            //未接驳端下垂
            float tipDrop = (1f - raise) * 60f * t * t;

            Vector2 pos = root + chord * t + normal * (bow + sway) + new Vector2(0f, tipDrop);

            //宽度：根粗心细的水柱形 + 泵血鼓包（一拍一口，向心脏上行）
            float baseW = MathHelper.Lerp(15f, 9f, t) * VesselWidthMul[i];
            float bulgeT = beatPhase - MathF.Floor(beatPhase + i * 0.13f) + i * 0.13f;
            bulgeT -= MathF.Floor(bulgeT);
            float bulge = MathF.Exp(-(t - bulgeT) * (t - bulgeT) / 0.0075f);
            width = baseW * (1f + 0.5f * bulge * BeatStrength() * raise);
            return pos;
        }

        /// <summary>血管条带装配：u=0 融水根 → 0.70 心壁端（KikasaHand 臂段），
        /// 顺带把鼓包高光位写进 bulgeGlows</summary>
        private VertexPositionColorTexture[] BuildVesselStrip(int i, KikasaDomainPlayer domain) {
            var verts = new VertexPositionColorTexture[VesselSamples * 2];
            Vector2 prev = VesselPoint(i, 0f, domain, out _);
            //鼓包高光取样
            float bulgeT = beatPhase - MathF.Floor(beatPhase + i * 0.13f) + i * 0.13f;
            bulgeT -= MathF.Floor(bulgeT);
            if (vesselRaise[i] >= 1f && BeatStrength() > 0.2f) {
                Vector2 bp = VesselPoint(i, bulgeT, domain, out _);
                bulgeGlows.Add((bp, BeatStrength() * (0.5f + 0.5f * beatEnvelope)));
            }

            for (int s = 0; s < VesselSamples; s++) {
                float t = s / (float)(VesselSamples - 1);
                Vector2 pos = VesselPoint(i, t, domain, out float width);
                Vector2 next = s < VesselSamples - 1
                    ? VesselPoint(i, (s + 1) / (float)(VesselSamples - 1), domain, out _)
                    : pos;
                Vector2 tangent = (s < VesselSamples - 1 ? next - pos : pos - prev).SafeNormalize(-Vector2.UnitY);
                Vector2 normal = new(-tangent.Y, tangent.X);

                //下侧微宽：水往下坠
                float downDot = Vector2.Dot(Vector2.UnitY, normal);
                float w0 = width * (1f + 0.14f * downDot);
                float w1 = width * (1f - 0.14f * downDot);
                Color vCenter = new(w0 / (w0 + w1), 0f, 0f);
                float u = t * 0.70f;
                verts[s * 2] = new VertexPositionColorTexture((pos + normal * w0).ToVector3(),
                    vCenter, new Vector2(u, 0f));
                verts[s * 2 + 1] = new VertexPositionColorTexture((pos - normal * w1).ToVector3(),
                    vCenter, new Vector2(u, 1f));
                prev = pos;
            }
            return verts;
        }

        /// <summary>倾泻血柱条带：湖面(根 u=0)→心口，宽幅重柱，流层天然向下淌</summary>
        private VertexPositionColorTexture[] BuildPourStrip(KikasaDomainPlayer domain) {
            const int samples = 12;
            float pourT = MathHelper.Clamp(((int)StateTimer - PourStart) / (float)(PourEnd - PourStart), 0f, 1f);
            Vector2 root = new(Projectile.Center.X, domain.LakeWorldY + 8f);
            Vector2 top = Projectile.Center + new Vector2(0f, 24f);
            if (root.Y - top.Y < 30f) {
                return null;
            }
            var verts = new VertexPositionColorTexture[samples * 2];
            for (int s = 0; s < samples; s++) {
                float t = s / (float)(samples - 1);
                Vector2 pos = Vector2.Lerp(root, top, t);
                pos.X += MathF.Sin(t * 5f + Main.GlobalTimeWrappedHourly * 3.2f + Seed) * 5f * MathF.Sin(t * MathHelper.Pi);
                //柱身宽：落点摊宽、心口收口；前段随倾泻进度涨粗再收
                float env = MathHelper.Clamp(pourT * 3f, 0f, 1f) * MathHelper.Clamp((1f - pourT) * 2.2f, 0f, 1f);
                float width = MathHelper.Lerp(34f, 22f, t) * (0.4f + 0.6f * env);
                Color vCenter = new(0.5f, 0f, 0f);
                Vector2 normal = Vector2.UnitX;
                verts[s * 2] = new VertexPositionColorTexture((pos + normal * width).ToVector3(),
                    vCenter, new Vector2(t * 0.70f, 0f));
                verts[s * 2 + 1] = new VertexPositionColorTexture((pos - normal * width).ToVector3(),
                    vCenter, new Vector2(t * 0.70f, 1f));
            }
            return verts;
        }

        private void DrawBody(SpriteBatch sb, Texture2D tex, float alpha) {
            int frameCount = Math.Max(Main.npcFrameCount[NPCID.MoonLordCore], 1);
            int frameH = tex.Height / frameCount;
            Rectangle frame = new(0, frameH * Math.Min(HeartFrame(), frameCount - 1), tex.Width, frameH);

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
                color = Color.Lerp(Color.White, BloodMain, 0.55f) * alpha;
            }

            BodyScale(out float sx, out float sy);
            sb.Draw(tex, HeartPos - Main.screenPosition, frame, color,
                0f, frame.Size() * 0.5f, new Vector2(sx, sy), SpriteEffects.None, 0f);

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
        }

        /// <summary>加色层：出水预兆血光 / 心跳呼吸辉 / 泵血鼓包 / 充能汇聚流线 /
        /// 竖缝瞳芯；批次成对还原</summary>
        private void DrawGlow(SpriteBatch sb, KikasaDomainPlayer domain, float alpha) {
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (glow == null) {
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

            //出水预兆：湖底血光自深处鼓起，比克眼更大更沉——那是一颗器官的分量
            if (State == StateEmerge && t < OmenEnd) {
                float ot = MathHelper.Clamp(t / (float)OmenEnd, 0f, 1f);
                float ease = 1f - (1f - ot) * (1f - ot);
                //湖底两记闷心跳时血光同步搏一次
                float pulse = 1f + (t is >= 10 and < 20 ? MathF.Exp(-(t - 10) * 0.35f) * 0.5f : 0f)
                    + (t is >= 34 and < 44 ? MathF.Exp(-(t - 34) * 0.35f) * 0.6f : 0f);
                Vector2 pos = new(Projectile.Center.X, domain.LakeWorldY + MathHelper.Lerp(72f, 12f, ease));
                float r = 52f + 40f * ease;
                EnsureBegin();
                sb.Draw(glow, pos - Main.screenPosition, null, BloodDeep * (0.5f * ease * pulse), 0f,
                    gOrigin, new Vector2(r * 3f / glow.Width, r * 1.2f / glow.Height), SpriteEffects.None, 0f);
                sb.Draw(glow, pos - Main.screenPosition, null, FoamGlow * (0.3f * ease * pulse), 0f,
                    gOrigin, new Vector2(r * 1.6f / glow.Width, r * 0.7f / glow.Height), SpriteEffects.None, 0f);
            }

            if (alpha > 0.05f) {
                //心跳呼吸辉：每拍一圈微光
                float breathe = beatEnvelope * (1f - ChargeDim() * 0.7f);
                if (breathe > 0.04f) {
                    EnsureBegin();
                    BodyScale(out float sx, out _);
                    float r = 66f * sx / BaseScale * (1f + 0.35f * breathe);
                    sb.Draw(glow, HeartPos - Main.screenPosition, null, BloodMain * (0.3f * breathe * alpha), 0f,
                        gOrigin, new Vector2(r * 2f / glow.Width, r * 2.4f / glow.Height), SpriteEffects.None, 0f);
                }

                //泵血鼓包高光：沿管上行的一口口湖水
                foreach ((Vector2 pos, float power) in bulgeGlows) {
                    EnsureBegin();
                    sb.Draw(glow, pos - Main.screenPosition, null, FoamGlow * (0.34f * power * alpha), 0f,
                        gOrigin, new Vector2(26f / glow.Width * 2f, 20f / glow.Height * 2f), SpriteEffects.None, 0f);
                }
            }

            //充能汇聚流线：各向异性拉长指向心脏，72% 后静默截断
            float c = ChargeT();
            if (State == StateMoonRay && c > 0.03f && c < 0.72f && alpha > 0.1f) {
                EnsureBegin();
                int streaks = 9;
                for (int i = 0; i < streaks; i++) {
                    float phase = (Main.GlobalTimeWrappedHourly * 0.8f + i / (float)streaks + Seed * 0.13f) % 1f;
                    float ang = Seed + i * MathHelper.TwoPi / streaks + MathF.Sin(Seed * 3f + i) * 0.6f;
                    float dist = MathHelper.Lerp(180f, 30f, phase);
                    Vector2 pos = HeartPos + ang.ToRotationVector2() * dist;
                    float a = c * 0.45f * MathF.Sin(phase * MathHelper.Pi);
                    sb.Draw(glow, pos - Main.screenPosition, null, FoamGlow * a, ang,
                        gOrigin, new Vector2(44f / glow.Width * 2.2f, 9f / glow.Height), SpriteEffects.None, 0f);
                }
            }

            //竖缝与瞳状芯：睁眼后压在心脏正面，恒竖直——光是从缝里挤出来的
            float slit = SlitOpen();
            if (slit > 0.02f && alpha > 0.1f) {
                EnsureBegin();
                Vector2 pos = HeartPos - Main.screenPosition;
                float beamGlow = State == StateMoonRay && t >= RayFireFrame && t < RayBeamEnd ? 1f : 0.55f;
                //瞳状芯：幻月苍青的窄芯 + 白热点
                sb.Draw(glow, pos, null, MoonGlint * (0.85f * slit * beamGlow), 0f,
                    gOrigin, new Vector2(16f * slit / glow.Width * 2f, 78f / glow.Height * 2f), SpriteEffects.None, 0f);
                sb.Draw(glow, pos, null, Color.White * (0.6f * slit * beamGlow), 0f,
                    gOrigin, new Vector2(7f * slit / glow.Width * 2f, 46f / glow.Height * 2f), SpriteEffects.None, 0f);
                //缝口血色余光
                sb.Draw(glow, pos, null, BloodMain * (0.4f * slit), 0f,
                    gOrigin, new Vector2(30f * slit / glow.Width * 2f, 110f / glow.Height * 2f), SpriteEffects.None, 0f);
            }

            if (begun) {
                sb.End();
                sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                    DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
            }
        }

        //==================== 命中与谢幕 ====================

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            //心缩拍撞上来的代价：重溅血（OnHit 只在 owner 端跑）
            if (Main.dedServ) {
                return;
            }
            for (int i = 0; i < 10; i++) {
                PRTLoader.NewParticle<PRT_KikasaBloodGlob>(
                    target.Center + Main.rand.NextVector2Circular(24f, 24f),
                    Main.rand.NextVector2Circular(3f, 3f),
                    BloodMain * 0.6f, Main.rand.NextFloat(0.45f, 0.75f))
                    ?.Configure(Main.rand.Next(16, 28), Main.rand.NextFloat(-0.5f, 0.5f));
            }
            SoundEngine.PlaySound(SoundID.NPCHit1 with { Volume = 0.7f, Pitch = -0.5f, MaxInstances = 3 }, target.Center);
        }

        public override void OnKill(int timeLeft) {
            //谢幕残珠：倾泻尾拍或异常移除都留一摊血
            if (Main.dedServ) {
                return;
            }
            for (int i = 0; i < 14; i++) {
                PRTLoader.NewParticle<PRT_KikasaBloodGlob>(
                    Projectile.Center + Main.rand.NextVector2Circular(36f, 48f),
                    new Vector2(Main.rand.NextFloat(-1.4f, 1.4f), Main.rand.NextFloat(1f, 4f)),
                    Main.rand.NextBool(3) ? BloodDeep : BloodMain,
                    Main.rand.NextFloat(0.4f, 0.7f))?.Configure(Main.rand.Next(16, 30), 0.35f);
            }
            PRTLoader.NewParticle<PRT_GhostRainMist>(Projectile.Center,
                new Vector2(0f, -0.25f), MistBlood * 0.75f, Main.rand.NextFloat(0.7f, 1.05f))
                ?.Configure(Main.rand.Next(55, 90));
        }
    }
}
