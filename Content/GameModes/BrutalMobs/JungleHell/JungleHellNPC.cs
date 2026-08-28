using CalamityOverhaul.Content.GameModes.BrutalMobs.Common;
using CalamityOverhaul.Content.GameModes.BrutalMobs.JungleHell.Projectiles;
using System;
using System.Collections.Generic;
using System.IO;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.JungleHell
{
    /// <summary>
    /// 残酷模式丛林+地狱小怪行为机制层，主题「伏击与弹幕幕」。
    /// 不接管原版 AI，只做叠加：齐射幕(蜂族/恶魔/血魔/龟壳)、藤蔓鞭击(食人花族)、
    /// 小鬼传送开窗、骨蛇破土预告、闻血追猎(鱼/蝠/蛛，嗅探定身→锁向突进→力竭后摇)。
    /// 数值增强由 GameModeNPC 统一负责，此处只加行为
    /// </summary>
    internal class JungleHellNPC : GlobalNPC
    {
        public override bool InstancePerEntity => true;

        #region 调参常量
        /// <summary>各机制族全局并发上限（同屏量产怪的公平与性能阀门）</summary>
        internal const int MaxConcurrent = 6;
        /// <summary>档位每级冷却缩短比例</summary>
        private const float TierCooldownStep = 0.12f;

        //齐射幕族
        private const float VolleyMinRange = 170f;
        private const float VolleyMaxRange = 720f;
        private const int VolleyCooldownHornet = 300;
        private const int VolleyCooldownDemon = 340;
        private const int VolleyCooldownDevil = 320;
        private const int VolleyCooldownStagger = 60;
        /// <summary>走廊缺口中心相对瞄准线的最大随机偏移（弧度），生成瞬间锁定</summary>
        private const float CorridorOffsetMax = 0.22f;
        private const float StingerDamageFrac = 0.5f;
        private const float ScytheDamageFrac = 0.6f;
        private const float TridentDamageFrac = 0.65f;

        //龟壳崩棘（旋转冲锋起跳瞬间植下反应式齐射预兆）
        private const int TortoiseSpinCooldown = 300;
        /// <summary>识别起旋：上一帧慢于此值</summary>
        private const float TortoisePrevSpeedMax = 2.5f;
        /// <summary>识别起旋：当前帧快于此值</summary>
        private const float TortoiseLaunchSpeedMin = 5.5f;
        private const float SpikeDamageFrac = 0.55f;

        //藤蔓鞭击族
        private const float WhipMinRange = 170f;
        private const float WhipMaxRange = 430f;
        private const int WhipCooldown = 260;
        private const int WhipCooldownStagger = 50;
        private const float WhipDamageFrac = 0.65f;

        //小鬼传送开窗
        /// <summary>单帧位移超过此距离视为传送（施法者原地不动，击退远小于此）</summary>
        private const float TeleportJumpDist = 128f;
        private const int ImpWindowCooldown = 90;
        private const float ImpBoltDamageFrac = 0.5f;
        private const float ImpTargetRange = 1000f;

        //骨蛇破土预告 + 地下伏击提速
        private const float SerpentOmenRange = 900f;
        private const float SerpentAmbushBase = 0.3f;
        private const float SerpentAmbushPerTier = 0.1f;

        //闻血追猎族（嗅探定身→锁向突进→力竭后摇的三段相位机）
        private const float FrenzyMinRange = 80f;
        private const float FrenzyMaxRange = 560f;
        /// <summary>嗅探定身前摇帧数（姿态前摇替代预告实体，≥30 可见帧，档位不缩短）</summary>
        private const int FrenzyWindupFrames = 34;
        /// <summary>突进包络三段：爬升/保持/衰减帧数（MobDash.Envelope 塑形）</summary>
        private const int FrenzyDashRise = 8;
        private const int FrenzyDashHold = 14;
        private const int FrenzyDashDecay = 20;
        /// <summary>力竭后摇帧数：清残速后横向阻尼，把控制权干净还给原版 AI</summary>
        private const int FrenzyRecoverFrames = 20;
        /// <summary>单个体突进冷却（M7 要求 ≥360，不随档位缩短）与向上随机抖动</summary>
        private const int FrenzyCooldownBase = 360;
        private const int FrenzyCooldownJitter = 60;
        /// <summary>射程/视线/并发闸未过时的复查间隔</summary>
        private const int FrenzyRetryDelay = 30;
        /// <summary>前摇被反制（清流血/目标失效/鱼离水）后的重整冷却</summary>
        private const int FrenzyAbortDelay = 90;
        /// <summary>同时处于前摇或突进段的同族个体上限（权威端扫描计数）</summary>
        private const int FrenzyMaxConcurrent = 4;
        /// <summary>前摇每帧压速阻尼（急停蓄势，前摇可见信号之一）</summary>
        private const float FrenzyWindupDamp = 0.82f;
        /// <summary>后摇每帧横向阻尼</summary>
        private const float FrenzyRecoverDamp = 0.8f;
        //突进名义峰速（未含提速补偿，注入时除回 MoveGain；约为原怪常速的 1.3~1.5 倍）
        private const float FrenzyPeakSmallBat = 6.8f;
        private const float FrenzyPeakGiantBat = 8f;
        private const float FrenzyPeakPiranha = 6.6f;
        private const float FrenzyPeakArapaima = 8f;
        private const float FrenzyPeakSpider = 7.2f;
        /// <summary>档位峰速倍率（只调强度不改机制形状）</summary>
        private static readonly float[] FrenzyPeakByTier = [1f, 1.1f, 1.2f];
        /// <summary>地面蜘蛛突进倾角上限（弧度）；蝙蝠/鱼原版自转，跳过 Lean</summary>
        private const float FrenzySpiderLean = 0.18f;
        /// <summary>咬伤流血时长（帧），反制=处理自己的减益</summary>
        private const int BiteBleedBase = 240;
        private const int BiteBleedPerTier = 60;

        //出生冷却宽限：刚刷出的个体不许立刻放特殊攻击（首发错拍窗收进 60~180，M7）
        private const int InitialGraceMin = 60;
        private const int InitialGraceRand = 120;

        //闻血追猎相位常量
        private const byte FrenzyIdle = 0;
        private const byte FrenzyWindup = 1;
        private const byte FrenzyDash = 2;
        private const byte FrenzyRecover = 3;
        #endregion

        /// <summary>机制族</summary>
        private enum MechKind : byte
        {
            None,
            /// <summary>齐射幕（蜂族/恶魔/血魔）</summary>
            Volley,
            /// <summary>龟壳崩棘（反应式齐射）</summary>
            Tortoise,
            /// <summary>藤蔓鞭击</summary>
            Whip,
            /// <summary>传送开窗</summary>
            ImpWindow,
            /// <summary>破土预告</summary>
            Serpent,
            /// <summary>闻血追猎</summary>
            Frenzy,
        }

        /// <summary>本组接管的类型表（大小写变体走 netID 映射回基类型，无需单列）</summary>
        private static readonly HashSet<int> TargetTypes = [
            //丛林
            NPCID.Piranha, NPCID.Arapaima, NPCID.JungleBat, NPCID.GiantFlyingFox,
            NPCID.JungleCreeper, NPCID.JungleCreeperWall,
            NPCID.Hornet, NPCID.HornetFatty, NPCID.HornetHoney, NPCID.HornetLeafy,
            NPCID.HornetSpikey, NPCID.HornetStingy, NPCID.MossHornet,
            NPCID.ManEater, NPCID.Snatcher, NPCID.AngryTrapper, NPCID.GiantTortoise,
            //地狱
            NPCID.Hellbat, NPCID.Lavabat, NPCID.Demon, NPCID.RedDevil,
            NPCID.FireImp, NPCID.BoneSerpentHead,
        ];

        //各机制族存活弹幕计数（服务端决策用，Counter 系统周期重计自愈）
        internal static int LiveVolleyOmens;
        internal static int LiveLashes;
        internal static int LiveAmbushOmens;
        internal static int LiveBreachOmens;

        /// <summary>出生绑定档位，0 = 未增强</summary>
        private int boundTier;
        private MechKind kind;
        /// <summary>资格缓存：0 未判 / 1 通过 / -1 排除（雕像怪等在首帧才可判）</summary>
        private int gate;
        private int cooldown;
        /// <summary>小鬼传送检测的上一帧位置</summary>
        private Vector2 lastPos;
        /// <summary>乌龟起旋检测的上一帧速度</summary>
        private float prevSpeed;
        /// <summary>闻血追猎相位（权威端真相；客户端经 ExtraAI 镜像收表现）</summary>
        private byte frenzyPhase;
        /// <summary>相位内计时：前摇/后摇倒数，突进段递增喂包络</summary>
        private int frenzyTimer;
        /// <summary>突进锁定方向（前摇结束帧锁定，预告即承诺，此后不重瞄）</summary>
        private float frenzyLockDir;
        /// <summary>破土预兆最近一次刷新的帧号（预兆实体每帧回写，各端本地一致）</summary>
        internal int lastOmenFrame = -100000;

        public override bool AppliesToEntity(NPC entity, bool lateInstantiation) => TargetTypes.Contains(entity.type);

        public override void SetDefaults(NPC npc) {
            boundTier = 0;
            gate = 0;
            int tier = GameModeSystem.EffectiveTier;
            if (tier <= 0) {
                return;
            }
            boundTier = tier;
            kind = ResolveKind(npc.type);
            cooldown = InitialGraceMin + Main.rand.Next(InitialGraceRand);
        }

        private static MechKind ResolveKind(int type) {
            if (type == NPCID.Hornet || type == NPCID.HornetFatty || type == NPCID.HornetHoney
                || type == NPCID.HornetLeafy || type == NPCID.HornetSpikey || type == NPCID.HornetStingy
                || type == NPCID.MossHornet || type == NPCID.Demon || type == NPCID.RedDevil) {
                return MechKind.Volley;
            }
            if (type == NPCID.GiantTortoise) {
                return MechKind.Tortoise;
            }
            if (type == NPCID.ManEater || type == NPCID.Snatcher || type == NPCID.AngryTrapper) {
                return MechKind.Whip;
            }
            if (type == NPCID.FireImp) {
                return MechKind.ImpWindow;
            }
            if (type == NPCID.BoneSerpentHead) {
                return MechKind.Serpent;
            }
            return MechKind.Frenzy;
        }

        /// <summary>机制资格：雕像怪/Boss/友方/蠕虫体节等一律排除</summary>
        private static bool MechEligible(NPC npc) {
            if (npc.friendly || npc.townNPC || npc.immortal || npc.dontTakeDamage) {
                return false;
            }
            if (npc.lifeMax <= 5 || npc.damage <= 0) {
                return false;
            }
            if (npc.SpawnedFromStatue || npc.boss || npc.realLife >= 0) {
                return false;
            }
            return true;
        }

        /// <summary>档位冷却折算</summary>
        private int TierCd(int baseCd) => (int)(baseCd * (1f - TierCooldownStep * (boundTier - 1)));

        /// <summary>
        /// 提速位移补偿：GameModeNPC.PostAI 按 velocity×SpeedBonus 追加位置推进，
        /// 本层注入的承诺性速度一律除回该系数（位移项除回、重力项不除）
        /// </summary>
        private float MoveGain(NPC npc) => !npc.boss && npc.realLife < 0 ? 1f + GameModeTuning.SpeedBonus(boundTier) : 1f;

        public override void PostAI(NPC npc) {
            if (boundTier <= 0) {
                return;
            }
            if (gate == 0) {
                gate = MechEligible(npc) ? 1 : -1;
            }
            if (gate < 0) {
                return;
            }

            switch (kind) {
                case MechKind.Frenzy: FrenzyTick(npc); return;
                case MechKind.Volley: VolleyTick(npc); return;
                case MechKind.Tortoise: TortoiseTick(npc); return;
                case MechKind.Whip: WhipTick(npc); return;
                case MechKind.ImpWindow: ImpTick(npc); return;
                case MechKind.Serpent: SerpentTick(npc); return;
            }
        }

        /// <summary>目标有效且在射程内（服务端触发判定用）</summary>
        private static bool TargetInRange(NPC npc, float range, out Player target) {
            target = null;
            if (!npc.HasValidTarget) {
                return false;
            }
            Player player = Main.player[npc.target];
            if (!player.active || player.dead) {
                return false;
            }
            if (npc.Distance(player.Center) > range) {
                return false;
            }
            target = player;
            return true;
        }

        #region 闻血追猎（鱼/蝠/蛛：目标带流血→嗅探定身→锁向突进→力竭后摇）
        private static bool IsFrenzyFish(int type) => type == NPCID.Piranha || type == NPCID.Arapaima;

        /// <summary>突进名义峰速按类型分档（小蝙蝠/大狐蝠/食人鱼/巨骨舌鱼/蜘蛛各不同）</summary>
        private static float FrenzyPeak(int type) => type switch {
            NPCID.Piranha => FrenzyPeakPiranha,
            NPCID.Arapaima => FrenzyPeakArapaima,
            NPCID.GiantFlyingFox => FrenzyPeakGiantBat,
            NPCID.JungleCreeper or NPCID.JungleCreeperWall => FrenzyPeakSpider,
            _ => FrenzyPeakSmallBat,
        };

        /// <summary>猎物有效：目标存活且带流血；鱼类只在水中追猎（离水翻滚不狂暴）</summary>
        private bool FrenzyPreyValid(NPC npc, out Player target) {
            target = null;
            if (!npc.HasValidTarget) {
                return false;
            }
            Player player = Main.player[npc.target];
            if (!player.active || player.dead || !player.HasBuff(BuffID.Bleeding)) {
                return false;
            }
            if (IsFrenzyFish(npc.type) && !npc.wet) {
                return false;
            }
            target = player;
            return true;
        }

        /// <summary>权威端扫描同族处于前摇/突进段的个体数（全局并发闸，客户端不跑此判定）</summary>
        private static int CountFrenzyEngaged() {
            int count = 0;
            foreach (NPC other in Main.ActiveNPCs) {
                if (other.TryGetGlobalNPC(out JungleHellNPC global) && global.kind == MechKind.Frenzy
                    && (global.frenzyPhase == FrenzyWindup || global.frenzyPhase == FrenzyDash)) {
                    count++;
                }
            }
            return count;
        }

        private void FrenzyTick(NPC npc) {
            if (VaultUtils.isClient) {
                //客户端不做决策：推进本地镜像计时，让红雾/倾斜在低频同步间隙也连续
                FrenzyMirrorAdvance();
                FrenzyPresentTick(npc);
                return;
            }
            switch (frenzyPhase) {
                case FrenzyWindup: FrenzyWindupTick(npc); break;
                case FrenzyDash: FrenzyDashTick(npc); break;
                case FrenzyRecover: FrenzyRecoverTick(npc); break;
                default: FrenzyIdleTick(npc); break;
            }
            FrenzyPresentTick(npc);
        }

        /// <summary>待机：冷却走完且嗅到血（目标带流血+射程+视线+并发闸）才进前摇</summary>
        private void FrenzyIdleTick(NPC npc) {
            if (cooldown > 0) {
                cooldown--;
                return;
            }
            if (!FrenzyPreyValid(npc, out Player target)) {
                return;
            }
            float dist = npc.Distance(target.Center);
            if (dist < FrenzyMinRange || dist > FrenzyMaxRange
                || !Collision.CanHitLine(npc.Center, 1, 1, target.Center, 1, 1)) {
                cooldown = FrenzyRetryDelay;
                return;
            }
            if (CountFrenzyEngaged() >= FrenzyMaxConcurrent) {
                cooldown = FrenzyRetryDelay;
                return;
            }
            frenzyPhase = FrenzyWindup;
            frenzyTimer = FrenzyWindupFrames;
            //相位沿同步：镜像相位随包出线，客户端立刻开播前摇红雾
            npc.netUpdate = true;
        }

        /// <summary>前摇被反制（清流血/目标失效/鱼离水）：中止回短冷却，失败方向=安全方向</summary>
        private void FrenzyAbort(NPC npc) {
            frenzyPhase = FrenzyIdle;
            frenzyTimer = 0;
            cooldown = FrenzyAbortDelay;
            npc.netUpdate = true;
        }

        /// <summary>嗅探定身：压速蓄势（可见信号），结束帧锁向进突进（预告即承诺）</summary>
        private void FrenzyWindupTick(NPC npc) {
            frenzyTimer--;
            if (!FrenzyPreyValid(npc, out Player target)) {
                FrenzyAbort(npc);
                return;
            }
            //压速：地面蜘蛛只阻尼横向（重力项不动），游/飞类全向阻尼
            if (npc.type == NPCID.JungleCreeper) {
                npc.velocity.X *= FrenzyWindupDamp;
            }
            else {
                npc.velocity *= FrenzyWindupDamp;
            }
            if (frenzyTimer <= 0) {
                //锁定帧：方向自此为承诺，突进期不再重瞄
                frenzyLockDir = (target.Center - npc.Center).ToRotation();
                frenzyPhase = FrenzyDash;
                frenzyTimer = 0;
                npc.netUpdate = true;
                return;
            }
            if (frenzyTimer % 6 == 0) {
                //低频载波：压速期间客户端位置纠偏 + 镜像计时对齐
                npc.netUpdate = true;
            }
        }

        /// <summary>锁向突进：包络塑形注入速度（爬升→保持→衰减），撞墙快进衰减段泄力</summary>
        private void FrenzyDashTick(NPC npc) {
            frenzyTimer++;
            if (IsFrenzyFish(npc.type) && !npc.wet) {
                //鱼被引出水面=反制成功，立即力竭
                FrenzyEnterRecover(npc);
                return;
            }
            //撞墙即泄力：爬升段不判（出发帧还带着站地的陈旧碰撞旗），入保持段后撞上就快进衰减
            if (frenzyTimer > FrenzyDashRise && frenzyTimer < FrenzyDashRise + FrenzyDashHold
                && (npc.collideX || npc.collideY)) {
                frenzyTimer = FrenzyDashRise + FrenzyDashHold;
            }
            float peak = FrenzyPeak(npc.type) * FrenzyPeakByTier[boundTier - 1] / MoveGain(npc);
            npc.velocity = MobDash.Velocity(frenzyLockDir.ToRotationVector2(), peak,
                frenzyTimer, FrenzyDashRise, FrenzyDashHold, FrenzyDashDecay);
            if (frenzyTimer % 6 == 0) {
                npc.netUpdate = true;
            }
            if (frenzyTimer >= FrenzyDashRise + FrenzyDashHold + FrenzyDashDecay) {
                FrenzyEnterRecover(npc);
            }
        }

        /// <summary>衰减段结束：清残速进力竭后摇</summary>
        private void FrenzyEnterRecover(NPC npc) {
            if (npc.type == NPCID.JungleCreeper) {
                npc.velocity.X = 0f;//重力项留给原版
            }
            else {
                npc.velocity = Vector2.Zero;
            }
            frenzyPhase = FrenzyRecover;
            frenzyTimer = FrenzyRecoverFrames;
            npc.netUpdate = true;
        }

        /// <summary>力竭后摇：短暂横向阻尼后把控制权干净还给原版 AI，回长冷却</summary>
        private void FrenzyRecoverTick(NPC npc) {
            frenzyTimer--;
            npc.velocity.X *= FrenzyRecoverDamp;
            if (npc.type != NPCID.JungleCreeper) {
                npc.velocity.Y *= FrenzyRecoverDamp;
            }
            if (frenzyTimer <= 0) {
                frenzyPhase = FrenzyIdle;
                frenzyTimer = 0;
                cooldown = FrenzyCooldownBase + Main.rand.Next(FrenzyCooldownJitter + 1);
                //空闲帧传输：镜像位=0 自清客户端残留
                npc.netUpdate = true;
                return;
            }
            if (frenzyTimer % 6 == 0) {
                npc.netUpdate = true;
            }
        }

        /// <summary>客户端镜像推进：按相位规则本地走表，只喂表现不做决策</summary>
        private void FrenzyMirrorAdvance() {
            if (frenzyPhase == FrenzyWindup || frenzyPhase == FrenzyRecover) {
                if (frenzyTimer > 0) {
                    frenzyTimer--;
                }
            }
            else if (frenzyPhase == FrenzyDash
                && frenzyTimer < FrenzyDashRise + FrenzyDashHold + FrenzyDashDecay) {
                frenzyTimer++;
            }
        }

        /// <summary>各端本地表现：前摇红雾+微光、突进血尘拖尾+蜘蛛倾角、后摇残滴</summary>
        private void FrenzyPresentTick(NPC npc) {
            if (Main.dedServ) {
                return;
            }
            if (frenzyPhase == FrenzyWindup && frenzyTimer > 0) {
                //嗅探红雾：身周起雾上飘，尘量给足让前摇可读
                for (int i = 0; i < 2; i++) {
                    Dust mist = Dust.NewDustDirect(npc.position - new Vector2(6f, 6f),
                        npc.width + 12, npc.height + 12, DustID.Blood, 0f, 0f, 150, default,
                        Main.rand.NextFloat(1.1f, 1.7f));
                    mist.velocity = new Vector2(Main.rand.NextFloat(-0.4f, 0.4f),
                        Main.rand.NextFloat(-0.9f, -0.2f));
                    mist.noGravity = true;
                }
                if (Main.rand.NextBool(3)) {
                    Dust spark = Dust.NewDustDirect(npc.position, npc.width, npc.height,
                        DustID.RedTorch, 0f, 0f, 100, default, 0.9f);
                    spark.velocity *= 0.3f;
                    spark.noGravity = true;
                }
                //微光随前摇进度增强（越临近突进越亮）
                float progress = 1f - frenzyTimer / (float)FrenzyWindupFrames;
                Lighting.AddLight(npc.Center, 0.3f + 0.25f * progress, 0.04f, 0.05f);
                return;
            }
            if (frenzyPhase == FrenzyDash
                && frenzyTimer < FrenzyDashRise + FrenzyDashHold + FrenzyDashDecay) {
                //嗜血余迹拖尾
                if (Main.rand.NextBool(2)) {
                    Dust trail = Dust.NewDustDirect(npc.position, npc.width, npc.height,
                        DustID.Blood, 0f, 0f, 120, default, Main.rand.NextFloat(1f, 1.5f));
                    trail.velocity = -npc.velocity * 0.2f;
                    trail.noGravity = true;
                }
                Lighting.AddLight(npc.Center, 0.4f, 0.05f, 0.06f);
                //地面蜘蛛按包络强度压身发力；蝙蝠/鱼原版自转，跳过 Lean 防打架
                if (npc.type == NPCID.JungleCreeper) {
                    float envelope = MobDash.Envelope(frenzyTimer,
                        FrenzyDashRise, FrenzyDashHold, FrenzyDashDecay);
                    npc.rotation = MobDash.Lean(envelope,
                        frenzyLockDir.ToRotationVector2().X, FrenzySpiderLean);
                }
                return;
            }
            if (frenzyPhase == FrenzyRecover && frenzyTimer > 0) {
                //力竭残滴（带重力下坠）
                if (Main.rand.NextBool(4)) {
                    Dust.NewDustDirect(npc.position, npc.width, npc.height, DustID.Blood,
                        0f, 1f, 140, default, 1f);
                }
                if (npc.type == NPCID.JungleCreeper) {
                    npc.rotation = 0f;//倾角复位，残姿不留给原版
                }
            }
        }

        /// <summary>
        /// 狂暴相位镜像随 SyncNPC 过线（GlobalNPC 实例字段本身不同步）：
        /// 活跃时付相位/计时/锁向，空闲帧位=0 自清客户端残留，丢包自愈
        /// </summary>
        public override void SendExtraAI(NPC npc, BitWriter bitWriter, BinaryWriter binaryWriter) {
            bool engaged = kind == MechKind.Frenzy && frenzyPhase != FrenzyIdle;
            bitWriter.WriteBit(engaged);
            if (!engaged) {
                return;
            }
            binaryWriter.Write(frenzyPhase);
            binaryWriter.Write((short)frenzyTimer);
            binaryWriter.Write(frenzyLockDir);
        }

        public override void ReceiveExtraAI(NPC npc, BitReader bitReader, BinaryReader binaryReader) {
            if (!bitReader.ReadBit()) {
                frenzyPhase = FrenzyIdle;
                frenzyTimer = 0;
                return;
            }
            //先读齐再用：流对齐优先，哪怕本端档位未绑定也要消费同样的字节数
            frenzyPhase = binaryReader.ReadByte();
            frenzyTimer = binaryReader.ReadInt16();
            frenzyLockDir = binaryReader.ReadSingle();
        }
        #endregion

        #region 齐射幕（蜂族毒刺 / 恶魔镰刃 / 血魔三叉戟）
        private void VolleyTick(NPC npc) {
            if (VaultUtils.isClient) {
                return;
            }
            if (cooldown > 0) {
                cooldown--;
                return;
            }
            if (LiveVolleyOmens >= MaxConcurrent || !TargetInRange(npc, VolleyMaxRange, out Player target)) {
                return;
            }
            float dist = npc.Distance(target.Center);
            if (dist < VolleyMinRange) {
                return;
            }
            if (!Collision.CanHitLine(npc.Center, 1, 1, target.Center, 1, 1)) {
                return;
            }

            int mode = npc.type == NPCID.Demon ? JhVolleyOmen.ModeDemon
                : npc.type == NPCID.RedDevil ? JhVolleyOmen.ModeDevil : JhVolleyOmen.ModeHornet;
            float frac = mode == JhVolleyOmen.ModeDemon ? ScytheDamageFrac
                : mode == JhVolleyOmen.ModeDevil ? TridentDamageFrac : StingerDamageFrac;
            SpawnVolley(npc, mode, (target.Center - npc.Center).ToRotation(), frac);

            int baseCd = mode == JhVolleyOmen.ModeDemon ? VolleyCooldownDemon
                : mode == JhVolleyOmen.ModeDevil ? VolleyCooldownDevil : VolleyCooldownHornet;
            cooldown = TierCd(baseCd) + Main.rand.Next(VolleyCooldownStagger);
        }

        /// <summary>植下齐射预兆：瞄角与走廊偏移在此刻锁定（预告即承诺），参数全部随生成包同步</summary>
        private void SpawnVolley(NPC npc, int mode, float aim, float damageFrac) {
            int damage = Math.Max(1, (int)(npc.damage * damageFrac));
            float corridorOffset = Main.rand.NextFloat(-CorridorOffsetMax, CorridorOffsetMax);
            Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, aim.ToRotationVector2(),
                ModContent.ProjectileType<JhVolleyOmen>(), damage, 0f, Main.myPlayer,
                JhVolleyOmen.Pack(mode, boundTier), aim, corridorOffset);
            LiveVolleyOmens++;
        }
        #endregion

        #region 龟壳崩棘（起旋瞬间沿冲锋线植下延迟棘幕，双拍读招）
        private void TortoiseTick(NPC npc) {
            float speed = npc.velocity.Length();
            if (VaultUtils.isClient) {
                prevSpeed = speed;
                return;
            }
            if (cooldown > 0) {
                cooldown--;
            }
            //旋转冲锋在缩壳瞬间锁定弹道，起跳帧速度陡增；棘幕沿同一条锁定线布设
            if (cooldown <= 0 && prevSpeed < TortoisePrevSpeedMax && speed > TortoiseLaunchSpeedMin
                && LiveVolleyOmens < MaxConcurrent && npc.HasValidTarget) {
                SpawnVolley(npc, JhVolleyOmen.ModeTortoise, npc.velocity.ToRotation(), SpikeDamageFrac);
                cooldown = TierCd(TortoiseSpinCooldown);
            }
            prevSpeed = speed;
        }
        #endregion

        #region 藤蔓鞭击（食人花族：预告延伸鞭打，超出常规藤蔓够不到的距离）
        private void WhipTick(NPC npc) {
            if (VaultUtils.isClient) {
                return;
            }
            if (cooldown > 0) {
                cooldown--;
                return;
            }
            if (LiveLashes >= MaxConcurrent || !TargetInRange(npc, WhipMaxRange, out Player target)) {
                return;
            }
            float dist = npc.Distance(target.Center);
            if (dist < WhipMinRange) {
                return;
            }
            if (!Collision.CanHitLine(npc.Center, 1, 1, target.Center, 1, 1)) {
                return;
            }

            //鞭线基点/方向/长度在此刻全部锁定
            float reach = MathHelper.Clamp(dist + 40f, 120f, WhipMaxRange);
            float aim = (target.Center - npc.Center).ToRotation();
            int buffMode = npc.type == NPCID.AngryTrapper ? 1 : 0;
            int damage = Math.Max(1, (int)(npc.damage * WhipDamageFrac));
            Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, aim.ToRotationVector2(),
                ModContent.ProjectileType<JhVineLash>(), damage, 0f, Main.myPlayer,
                boundTier * 4 + buffMode, reach, npc.whoAmI);
            LiveLashes++;
            cooldown = TierCd(WhipCooldown) + Main.rand.Next(WhipCooldownStagger);
        }
        #endregion

        #region 小鬼传送开窗（传送后 45 帧可见凝形，锁向后才开火）
        private void ImpTick(NPC npc) {
            if (VaultUtils.isClient) {
                return;
            }
            if (cooldown > 0) {
                cooldown--;
            }
            Vector2 prev = lastPos;
            lastPos = npc.position;
            if (prev == Vector2.Zero || cooldown > 0) {
                return;
            }
            //单帧大位移 = 传送落点，此处开窗
            if (Vector2.DistanceSquared(prev, npc.position) < TeleportJumpDist * TeleportJumpDist) {
                return;
            }
            if (LiveAmbushOmens >= MaxConcurrent || !TargetInRange(npc, ImpTargetRange, out Player target)) {
                return;
            }

            int damage = Math.Max(1, (int)(npc.damage * ImpBoltDamageFrac));
            Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, Vector2.Zero,
                ModContent.ProjectileType<JhAmbushOmen>(), damage, 0f, Main.myPlayer,
                npc.whoAmI + boundTier * 1000, target.whoAmI, JhAmbushOmen.UnlockedAngle);
            LiveAmbushOmens++;
            cooldown = ImpWindowCooldown;
        }
        #endregion

        #region 骨蛇破土预告（地下逼近时尘柱示警，预兆在场才吃伏击提速）
        private void SerpentTick(NPC npc) {
            bool underground = Collision.SolidCollision(npc.position, npc.width, npc.height);
            bool omenLive = (int)Main.GameUpdateCount - lastOmenFrame < 4;

            //伏击提速：只在"预兆可见"时生效，读同步速度全端确定性推进
            if (underground && omenLive) {
                npc.position += npc.velocity * (SerpentAmbushBase + SerpentAmbushPerTier * (boundTier - 1));
            }

            if (VaultUtils.isClient) {
                return;
            }
            if (cooldown > 0) {
                cooldown--;
                return;
            }
            if (!underground || omenLive || LiveBreachOmens >= MaxConcurrent
                || !TargetInRange(npc, SerpentOmenRange, out _)) {
                return;
            }
            //残留渐隐中的旧预兆还在就不重复挂（低频触发路径才走这次扫描）
            if (HasLiveBreachOmen(npc.whoAmI)) {
                cooldown = 10;
                return;
            }
            Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, Vector2.Zero,
                ModContent.ProjectileType<JhBreachOmen>(), 0, 0f, Main.myPlayer, npc.whoAmI);
            LiveBreachOmens++;
            cooldown = 30;
        }

        private static bool HasLiveBreachOmen(int npcWhoAmI) {
            int type = ModContent.ProjectileType<JhBreachOmen>();
            foreach (Projectile proj in Main.ActiveProjectiles) {
                if (proj.type == type && (int)proj.ai[0] == npcWhoAmI) {
                    return true;
                }
            }
            return false;
        }
        #endregion

        /// <summary>撕咬者挂流血（命中方本机结算，原生同步）；流血是闻血追猎族的触发器</summary>
        public override void OnHitPlayer(NPC npc, Player target, Player.HurtInfo hurtInfo) {
            if (boundTier <= 0) {
                return;
            }
            //出生帧的接触咬合可能先于 PostAI 的懒判定，这里同口径补判（堵雕像怪首帧挂流血的窗口）
            if (gate == 0) {
                gate = MechEligible(npc) ? 1 : -1;
            }
            if (gate < 0) {
                return;
            }
            bool biter = npc.type == NPCID.Piranha || npc.type == NPCID.Arapaima
                || npc.type == NPCID.GiantFlyingFox || npc.type == NPCID.BoneSerpentHead;
            if (!biter) {
                return;
            }
            target.AddBuff(BuffID.Bleeding, BiteBleedBase + BiteBleedPerTier * (boundTier - 1));
        }
    }

    /// <summary>周期重计各机制族的存活弹幕数，自愈式并发上限（服务端决策专用）</summary>
    internal class JungleHellCounterSystem : ModSystem
    {
        private const int RecountInterval = 30;

        public override void PostUpdateProjectiles() {
            if (VaultUtils.isClient || Main.GameUpdateCount % RecountInterval != 0) {
                return;
            }
            int volley = 0, lash = 0, ambush = 0, breach = 0;
            int tVolley = ModContent.ProjectileType<JhVolleyOmen>();
            int tLash = ModContent.ProjectileType<JhVineLash>();
            int tAmbush = ModContent.ProjectileType<JhAmbushOmen>();
            int tBreach = ModContent.ProjectileType<JhBreachOmen>();
            foreach (Projectile proj in Main.ActiveProjectiles) {
                if (proj.type == tVolley) {
                    volley++;
                }
                else if (proj.type == tLash) {
                    lash++;
                }
                else if (proj.type == tAmbush) {
                    ambush++;
                }
                else if (proj.type == tBreach) {
                    breach++;
                }
            }
            JungleHellNPC.LiveVolleyOmens = volley;
            JungleHellNPC.LiveLashes = lash;
            JungleHellNPC.LiveAmbushOmens = ambush;
            JungleHellNPC.LiveBreachOmens = breach;
        }
    }
}
