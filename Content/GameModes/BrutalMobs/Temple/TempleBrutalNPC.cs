using CalamityOverhaul.Content.GameModes.BrutalMobs.Common;
using CalamityOverhaul.Content.GameModes.BrutalMobs.Temple.Projectiles;
using System;
using System.IO;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.Temple
{
    /// <summary>
    /// 残酷模式丛林神庙组行为机制层，主题：守庙武僧（蜥蜴人是受训殿卫，讲武术节奏与阵地控制）。
    /// 覆盖名单：Lihzahrd 直立形态（压桩前摇→突进爪击→顿→档位≥2 二段回旋尾扫→力竭惩罚窗）、
    /// LihzahrdCrawler 爬行形态（贴顶时天花板突袭：落点预兆→垂直扑落→落地滚身；地面时蓄力扑咬）、
    /// FlyingSnake（三连俯冲，ByStage 递进：预告逐段加长、峰速逐段加快，段间 20 帧喘息窗）；
    /// 神庙原生怪单全覆盖，无刻意除外。蜥蜴系死亡 25% 在尸位留庙火余烬（阵地控制）。
    /// 直立形态半血变身爬行形态时 SetDefaults 重跑、本实例重新绑定（重新吃出生错拍，属预期）；
    /// LihzahrdCrawler 原版是否贴顶爬行离线未核实，地面扑咬分支保证其战斗内覆盖不落空。
    /// 叠加在原版 AI 之上不接管，数值层归 GameModeNPC。决策只在权威端跑，
    /// 客户端经 SendExtraAI 相位镜像驱动本地表现（尘/倾斜/音效沿），危险承诺一律走同步预兆实体
    /// </summary>
    internal class TempleBrutalNPC : GlobalNPC
    {
        public override bool InstancePerEntity => true;

        //==== 通用节奏 ====
        /// <summary>出生首攻错拍窗（M7 密度预算：60~180 帧）</summary>
        private const int FirstCooldownMin = 60;
        private const int FirstCooldownMax = 180;
        /// <summary>触发条件不满足的复查间隔</summary>
        private const int RetryDelay = 30;
        /// <summary>资格不符（雕像怪等）的复查间隔</summary>
        private const int IneligibleDelay = 120;
        /// <summary>前摇被反制中止后的短冷却（失败方向=安全方向）</summary>
        private const int AbortDelay = 45;
        /// <summary>主力招冷却的随机抖动上限</summary>
        private const int CooldownJitter = 45;
        /// <summary>两类预兆实体各自的全局并发上限</summary>
        private const int OmenCap = 6;

        //==== 武僧连段（Lihzahrd 直立形态） ====
        /// <summary>压桩前摇帧（契约 ≥24：蹲身压速+落尘可见）</summary>
        private const int MonkWindupFrames = 26;
        /// <summary>爪击突进包络三段：爬升/保持/衰减帧</summary>
        private const int MonkDashRise = 6;
        private const int MonkDashHold = 14;
        private const int MonkDashDecay = 10;
        private const int MonkDashFrames = MonkDashRise + MonkDashHold + MonkDashDecay;
        /// <summary>连段中顿帧（爪击与尾扫之间的节拍停顿）</summary>
        private const int MonkPauseFrames = 6;
        /// <summary>回旋尾扫包络三段（仅档位 ≥2 触发）</summary>
        private const int MonkSweepRise = 5;
        private const int MonkSweepHold = 10;
        private const int MonkSweepDecay = 9;
        private const int MonkSweepFrames = MonkSweepRise + MonkSweepHold + MonkSweepDecay;
        /// <summary>力竭后摇帧（惩罚窗，全档一致）</summary>
        private const int MonkExhaustFrames = 24;
        /// <summary>爪击/尾扫名义峰速（未含提速补偿，注入时除回 MoveGain）</summary>
        private static readonly float[] MonkDashPeakByTier = [8.2f, 9f, 9.8f];
        private static readonly float[] MonkSweepPeakByTier = [7.4f, 8.2f, 9f];
        /// <summary>武僧连段冷却（主力招 ≤600 契约）</summary>
        private static readonly int[] MonkCooldownByTier = [420, 360, 300];
        private const float MonkMinRangeX = 60f;
        private const float MonkMaxRangeX = 300f;
        private const float MonkMaxRangeY = 120f;
        /// <summary>尾扫二段的重锁最大距离（超出则直接力竭）</summary>
        private const float MonkSweepMaxRange = 260f;
        /// <summary>前摇/顿/力竭的横向压速阻尼</summary>
        private const float MonkWindupDamp = 0.78f;
        private const float MonkPauseDamp = 0.25f;
        private const float MonkExhaustDamp = 0.85f;
        /// <summary>蓄势后仰角与突进前倾上限（弧度）</summary>
        private const float MonkWindupLean = 0.12f;
        private const float MonkDashLeanMax = 0.2f;

        //==== 天花板突袭 / 蓄力扑咬（LihzahrdCrawler 爬行形态） ====
        /// <summary>贴顶判定：头顶向上探测实心瓦的格数</summary>
        private const int CeilingProbeTiles = 3;
        /// <summary>「目标从下方经过」的横向触发半宽</summary>
        private const float CeilingTriggerHalfWidth = 70f;
        /// <summary>触发与落差下限：目标和落点都要在此距离之下才值得扑落</summary>
        private const float CeilingMinDropGap = 90f;
        /// <summary>向下寻找落点的最大瓦格数（超出视为深渊，放弃突袭）</summary>
        private const int DropSearchTiles = 40;
        /// <summary>扑落名义峰速（垂直包络，注入时除回 MoveGain）</summary>
        private static readonly float[] PlungePeakByTier = [12f, 13f, 14f];
        /// <summary>落地滚身后摇帧与滚速/阻尼</summary>
        private const int PlungeRollFrames = 18;
        private const float RollSpeed = 3.5f;
        private const float RollDamp = 0.85f;
        /// <summary>地面蓄力扑咬前摇帧（契约 ≥24）</summary>
        private const int PounceWindupFrames = 24;
        /// <summary>扑咬包络三段</summary>
        private const int PounceRise = 6;
        private const int PounceHold = 12;
        private const int PounceDecay = 10;
        private const int PounceFrames = PounceRise + PounceHold + PounceDecay;
        private static readonly float[] PouncePeakByTier = [7.6f, 8.4f, 9.2f];
        /// <summary>扑咬起跳小上抬（位移承诺项，注入时除回 MoveGain）</summary>
        private const float PounceUpKick = -3.2f;
        private const int PounceRecoverFrames = 16;
        private const float PounceWindupDamp = 0.78f;
        /// <summary>爬行形态冷却（两分支共用）</summary>
        private static readonly int[] CrawlerCooldownByTier = [460, 400, 340];
        private const float CrawlerMinRangeX = 60f;
        private const float CrawlerMaxRangeX = 300f;
        private const float CrawlerMaxRangeY = 140f;

        //==== 三连俯冲（FlyingSnake，段表见 TempleDiveOmen.*ByStage） ====
        /// <summary>俯冲总段数</summary>
        private const int SnakeDiveStages = 3;
        /// <summary>段间调整位帧（喘息窗，原版 AI 自由走位）</summary>
        private const int SnakeAdjustFrames = 20;
        /// <summary>第三段后的收势帧</summary>
        private const int SnakeRecoverFrames = 16;
        /// <summary>三连俯冲整套冷却（签名招 ≤600 契约）</summary>
        private static readonly int[] SnakeCooldownByTier = [560, 500, 440];
        private const float SnakeMinRange = 130f;
        private const float SnakeMaxRange = 420f;
        /// <summary>高位要求：至少比目标高出此距离才起手（绕高位盘旋的身份）</summary>
        private const float SnakeMinHeightAdv = 40f;
        /// <summary>预告期悬停压速脉冲间隔与阻尼</summary>
        private const int SnakeHoverDampInterval = 12;
        private const float SnakeHoverDamp = 0.55f;

        //==== 庙火余烬（蜥蜴系死亡阵地控制） ====
        /// <summary>留烬概率分母（NextBool(4)=25%）</summary>
        private const int EmberChanceDenom = 4;
        /// <summary>斑间强制间距：新烬与既有烬的最小距离（发射检查真正读取的公平常量）</summary>
        private const float EmberMinSpacing = 120f;
        /// <summary>余烬伤害 = 已缩放 npc.damage × 此值</summary>
        private const float EmberDamageFrac = 0.45f;
        /// <summary>余烬全局并发上限（独立于预兆闸）</summary>
        private const int EmberCap = 4;

        //==== 相位常量（family 区分语义空间） ====
        private const byte PhaseIdle = 0;
        //武僧
        private const byte MkWindup = 1;
        private const byte MkDash = 2;
        private const byte MkPause = 3;
        private const byte MkSweep = 4;
        private const byte MkExhaust = 5;
        //爬虫
        private const byte CwOmen = 1;
        private const byte CwPlunge = 2;
        private const byte CwRoll = 3;
        private const byte CwWindup = 4;
        private const byte CwPounce = 5;
        private const byte CwRecover = 6;
        //飞蛇
        private const byte SnTelegraph = 1;
        private const byte SnDive = 2;
        private const byte SnAdjust = 3;
        private const byte SnRecover = 4;

        private enum TplFamily : byte
        {
            None,
            /// <summary>Lihzahrd 直立武僧</summary>
            Monk,
            /// <summary>LihzahrdCrawler 爬行殿卫</summary>
            Crawler,
            /// <summary>FlyingSnake 翔蛇</summary>
            Snake,
        }

        /// <summary>本个体出生时绑定的档位，0=未绑定（镜像 GameModeNPC；中途切模式不影响已出生个体）</summary>
        private int boundTier;
        private TplFamily family;
        private byte phase;
        private int timer;
        /// <summary>飞蛇俯冲段号 0..2（权威端推进，随镜像下发）</summary>
        private byte stage;
        private int cooldown;
        /// <summary>锁定方向（锁定帧后不再改写，预告即承诺；尾扫允许重锁一次）</summary>
        private float lockDir;
        /// <summary>爬虫扑落的锁定落点（生成预兆时即承诺）</summary>
        private Vector2 lockPoint;
        /// <summary>本次攻击的预兆槽位（权威端私产，客户端不读）</summary>
        private int omenIndex = -1;
        /// <summary>各端本地已播过相位沿音效的相位号（重同步同相位不重放）</summary>
        private byte lastCuePhase;

        private static TplFamily ResolveFamily(int type) => type switch {
            NPCID.Lihzahrd => TplFamily.Monk,
            NPCID.LihzahrdCrawler => TplFamily.Crawler,
            NPCID.FlyingSnake => TplFamily.Snake,
            _ => TplFamily.None,
        };

        public override bool AppliesToEntity(NPC entity, bool lateInstantiation)
            => lateInstantiation && ResolveFamily(entity.type) != TplFamily.None;

        public override void SetDefaults(NPC npc) {
            boundTier = 0;
            family = ResolveFamily(npc.type);
            int tier = GameModeSystem.EffectiveTier;
            if (tier <= 0 || family == TplFamily.None) {
                return;
            }
            boundTier = tier;
            //出生错拍：SetDefaults 期 whoAmI 恒 0 不可作种子；冷却是权威端决策私产，Main.rand 无同步语义
            cooldown = FirstCooldownMin + Main.rand.Next(FirstCooldownMax - FirstCooldownMin + 1);
        }

        /// <summary>机制入口资格：友方/无敌/Boss/小血量载体/雕像怪/共享血池体节逐项排除（每个入口都过）</summary>
        private static bool Eligible(NPC npc) {
            if (npc.friendly || npc.townNPC || npc.immortal || npc.dontTakeDamage || npc.boss) {
                return false;
            }
            if (npc.lifeMax <= 5 || npc.damage <= 0) {
                return false;
            }
            if (npc.SpawnedFromStatue) {
                return false;
            }
            return npc.realLife < 0;
        }

        /// <summary>
        /// 提速位移补偿：GameModeNPC.PostAI 对非 Boss 且非体节个体按 velocity×SpeedBonus 追加位移，
        /// 本层注入的承诺性速度一律除回该系数（位移项除回、重力项不除；口径镜像 PumpkinMoonNPC.MoveGain）
        /// </summary>
        private float MoveGain(NPC npc) => !npc.boss && npc.realLife < 0 ? 1f + GameModeTuning.SpeedBonus(boundTier) : 1f;

        /// <summary>统计某类弹幕的活动实例数（只在触发时调用，非每帧）</summary>
        private static int CountActive(int projType, int stopAt = 32) {
            int count = 0;
            foreach (Projectile proj in Main.ActiveProjectiles) {
                if (proj.type == projType && ++count >= stopAt) {
                    break;
                }
            }
            return count;
        }

        /// <summary>来源打包（槽位+1|类型&lt;&lt;8）：预兆实体与 NPC 侧回读共用，防槽位复用欺骗</summary>
        private static int SrcPack(NPC npc) => (npc.whoAmI + 1) | (npc.type << 8);

        /// <summary>回读绑定预兆：索引+类型+来源三重校验，缺位=预告不在场，招式必须放弃（失败方向=安全方向）</summary>
        private bool OmenAlive(NPC npc, int projType) {
            if (omenIndex < 0 || omenIndex >= Main.maxProjectiles) {
                return false;
            }
            Projectile proj = Main.projectile[omenIndex];
            return proj.active && proj.type == projType && (int)proj.ai[0] == SrcPack(npc);
        }

        private static bool CanSee(NPC npc, Player player)
            => Collision.CanHit(npc.position, npc.width, npc.height, player.position, player.width, player.height);

        /// <summary>目标有效且取回引用（触发判定用）</summary>
        private static bool TryGetTarget(NPC npc, out Player player) {
            player = null;
            if (npc.target < 0 || npc.target >= Main.maxPlayers) {
                return false;
            }
            Player candidate = Main.player[npc.target];
            if (!candidate.Alives()) {
                return false;
            }
            player = candidate;
            return true;
        }

        public override void PostAI(NPC npc) {
            if (boundTier <= 0) {
                return;
            }
            if (VaultUtils.isClient) {
                //决策只在权威端；客户端推进镜像计时并驱动本地表现
                MirrorAdvance();
                PresentTick(npc);
                return;
            }
            switch (family) {
                case TplFamily.Monk: MonkTick(npc); break;
                case TplFamily.Crawler: CrawlerTick(npc); break;
                case TplFamily.Snake: SnakeTick(npc); break;
            }
            PresentTick(npc);
        }

        /// <summary>待机公共闸：冷却未走完/资格不符/目标缺位时统一处理，返回 null 表示本帧不可起手</summary>
        private Player IdleGate(NPC npc) {
            if (cooldown > 0) {
                cooldown--;
                return null;
            }
            if (!Eligible(npc)) {
                cooldown = IneligibleDelay;
                return null;
            }
            if (!TryGetTarget(npc, out Player player)) {
                cooldown = RetryDelay;
                return null;
            }
            return player;
        }

        #region 武僧连段（Lihzahrd）
        private void MonkTick(NPC npc) {
            switch (phase) {
                case MkWindup: MonkWindupTick(npc); break;
                case MkDash: MonkDashTick(npc); break;
                case MkPause: MonkPauseTick(npc); break;
                case MkSweep: MonkSweepTick(npc); break;
                case MkExhaust: MonkExhaustTick(npc); break;
                default: TryStartMonk(npc); break;
            }
        }

        private void TryStartMonk(NPC npc) {
            Player player = IdleGate(npc);
            if (player == null) {
                return;
            }
            float dx = Math.Abs(player.Center.X - npc.Center.X);
            float dy = Math.Abs(player.Bottom.Y - npc.Bottom.Y);
            if (npc.velocity.Y != 0f || dx < MonkMinRangeX || dx > MonkMaxRangeX
                || dy > MonkMaxRangeY || !CanSee(npc, player)) {
                cooldown = RetryDelay;
                return;
            }
            //压桩起手：姿态前摇即预告（纯近身体术，≥24 可见帧）
            phase = MkWindup;
            timer = MonkWindupFrames;
            npc.netUpdate = true;
        }

        private void MonkAbort(NPC npc) {
            phase = PhaseIdle;
            timer = 0;
            cooldown = AbortDelay;
            npc.netUpdate = true;
        }

        private void MonkWindupTick(NPC npc) {
            timer--;
            //压速蓄势（可见信号之一；只压横向，重力项不动）
            npc.velocity.X *= MonkWindupDamp;
            if (!TryGetTarget(npc, out Player player)) {
                MonkAbort(npc);
                return;
            }
            if (timer <= 0) {
                //锁向：承诺自此冻结，爪击期不再重瞄
                lockDir = (player.Center - npc.Center).ToRotation();
                phase = MkDash;
                timer = MonkDashFrames;
                npc.netUpdate = true;
                return;
            }
            if (timer % 6 == 0) {
                npc.netUpdate = true;
            }
        }

        private void MonkDashTick(NPC npc) {
            timer--;
            int t = MonkDashFrames - timer;
            //撞墙即坠入衰减段（突进撞空=反制有效）
            if (t > MonkDashRise && npc.collideX) {
                timer = Math.Min(timer, MonkDashDecay);
            }
            float dirX = MathF.Cos(lockDir) >= 0f ? 1f : -1f;
            float env = MobDash.Envelope(t, MonkDashRise, MonkDashHold, MonkDashDecay);
            //只塑形横向，纵向交给原版重力；位移项除回提速补偿
            npc.velocity.X = dirX * (MonkDashPeakByTier[boundTier - 1] / MoveGain(npc)) * env;
            if (timer % 6 == 0) {
                npc.netUpdate = true;
            }
            if (timer <= 0) {
                npc.velocity.X = 0f;
                phase = MkPause;
                timer = MonkPauseFrames;
                npc.netUpdate = true;
            }
        }

        private void MonkPauseTick(NPC npc) {
            timer--;
            //顿：节拍停顿，硬压残速
            npc.velocity.X *= MonkPauseDamp;
            if (timer > 0) {
                return;
            }
            if (boundTier >= 2 && TryGetTarget(npc, out Player player)
                && npc.Distance(player.Center) <= MonkSweepMaxRange) {
                //二段回旋尾扫：允许重新锁向一次，此后冻结
                lockDir = (player.Center - npc.Center).ToRotation();
                phase = MkSweep;
                timer = MonkSweepFrames;
            }
            else {
                phase = MkExhaust;
                timer = MonkExhaustFrames;
            }
            npc.netUpdate = true;
        }

        private void MonkSweepTick(NPC npc) {
            timer--;
            int t = MonkSweepFrames - timer;
            if (t > MonkSweepRise && npc.collideX) {
                timer = Math.Min(timer, MonkSweepDecay);
            }
            float dirX = MathF.Cos(lockDir) >= 0f ? 1f : -1f;
            float env = MobDash.Envelope(t, MonkSweepRise, MonkSweepHold, MonkSweepDecay);
            npc.velocity.X = dirX * (MonkSweepPeakByTier[boundTier - 1] / MoveGain(npc)) * env;
            if (timer % 6 == 0) {
                npc.netUpdate = true;
            }
            if (timer <= 0) {
                npc.velocity.X = 0f;
                phase = MkExhaust;
                timer = MonkExhaustFrames;
                npc.netUpdate = true;
            }
        }

        private void MonkExhaustTick(NPC npc) {
            timer--;
            //力竭惩罚窗：横向持续泄力，控制权逐帧还给原版
            npc.velocity.X *= MonkExhaustDamp;
            if (timer <= 0) {
                npc.velocity.X = 0f;
                phase = PhaseIdle;
                cooldown = MonkCooldownByTier[boundTier - 1] + Main.rand.Next(CooldownJitter + 1);
                //空闲帧位=0 随包出线，自清客户端镜像残留
                npc.netUpdate = true;
                return;
            }
            if (timer % 6 == 0) {
                npc.netUpdate = true;
            }
        }
        #endregion

        #region 天花板突袭 / 蓄力扑咬（LihzahrdCrawler）
        private void CrawlerTick(NPC npc) {
            switch (phase) {
                case CwOmen: CrawlerOmenTick(npc); break;
                case CwPlunge: CrawlerPlungeTick(npc); break;
                case CwRoll: CrawlerRollTick(npc); break;
                case CwWindup: CrawlerWindupTick(npc); break;
                case CwPounce: CrawlerPounceTick(npc); break;
                case CwRecover: CrawlerRecoverTick(npc); break;
                default: TryStartCrawler(npc); break;
            }
        }

        private void TryStartCrawler(NPC npc) {
            Player player = IdleGate(npc);
            if (player == null) {
                return;
            }
            if (TryCeilingAmbush(npc, player)) {
                return;
            }
            //地面分支：蓄力扑咬（姿态前摇 ≥24 可见帧）
            float dx = Math.Abs(player.Center.X - npc.Center.X);
            float dy = Math.Abs(player.Center.Y - npc.Center.Y);
            if (npc.velocity.Y != 0f || dx < CrawlerMinRangeX || dx > CrawlerMaxRangeX
                || dy > CrawlerMaxRangeY || !CanSee(npc, player)) {
                cooldown = RetryDelay;
                return;
            }
            phase = CwWindup;
            timer = PounceWindupFrames;
            npc.netUpdate = true;
        }

        /// <summary>贴顶判定：头顶数格内有实心瓦（原版爬行形态是否上顶离线未核实，不满足时走地面分支）</summary>
        private static bool IsCeilingClung(NPC npc) {
            Point top = npc.Top.ToTileCoordinates();
            for (int dy = 0; dy <= CeilingProbeTiles; dy++) {
                int tileY = top.Y - dy;
                if (!WorldGen.InWorld(top.X, tileY, 10)) {
                    return false;
                }
                if (WorldGen.SolidTile(top.X, tileY)) {
                    return true;
                }
            }
            return false;
        }

        /// <summary>从爬虫脚下向下找实心落点（同列扫描，找不到=深渊，放弃突袭）</summary>
        private static bool TryFindDropPoint(NPC npc, out Vector2 landing) {
            landing = default;
            Point feet = npc.Bottom.ToTileCoordinates();
            for (int dy = 1; dy <= DropSearchTiles; dy++) {
                int tileY = feet.Y + dy;
                if (!WorldGen.InWorld(feet.X, tileY, 10)) {
                    return false;
                }
                if (WorldGen.SolidTile(feet.X, tileY)) {
                    landing = new Vector2(npc.Center.X, tileY * 16f);
                    return true;
                }
            }
            return false;
        }

        /// <summary>天花板突袭起手：目标从正下方经过时植落点预兆（落点锁定即承诺）</summary>
        private bool TryCeilingAmbush(NPC npc, Player player) {
            if (!IsCeilingClung(npc)) {
                return false;
            }
            if (Math.Abs(player.Center.X - npc.Center.X) > CeilingTriggerHalfWidth) {
                return false;
            }
            if (player.Center.Y - npc.Center.Y < CeilingMinDropGap) {
                return false;
            }
            if (CountActive(ModContent.ProjectileType<TempleDropOmen>()) >= OmenCap) {
                return false;
            }
            if (!TryFindDropPoint(npc, out Vector2 landing)
                || landing.Y - npc.Bottom.Y < CeilingMinDropGap) {
                return false;
            }
            if (!Collision.CanHitLine(npc.Center, 1, 1, landing - Vector2.UnitY * 8f, 1, 1)) {
                return false;
            }

            //预告即实体：生成失败（弹幕位满）则整次突袭作废
            int idx = Projectile.NewProjectile(npc.GetSource_FromAI(), landing, Vector2.Zero,
                ModContent.ProjectileType<TempleDropOmen>(), 0, 0f, Main.myPlayer,
                SrcPack(npc), npc.Center.Y);
            if (idx < 0 || idx >= Main.maxProjectiles) {
                cooldown = RetryDelay;
                return true;
            }
            omenIndex = idx;
            lockPoint = landing;
            //贴顶蓄势：压速滞留
            npc.velocity *= 0.3f;
            phase = CwOmen;
            timer = TempleDropOmen.TelegraphFrames;
            npc.netUpdate = true;
            return true;
        }

        private void CrawlerAbort(NPC npc) {
            phase = PhaseIdle;
            timer = 0;
            omenIndex = -1;
            cooldown = AbortDelay;
            npc.netUpdate = true;
        }

        private void CrawlerOmenTick(NPC npc) {
            timer--;
            //贴顶滞留：横向软伺服贴住承诺柱（对既定落点的贴合，非重瞄），竖向压速
            npc.velocity.X = MathHelper.Clamp((lockPoint.X - npc.Center.X) * 0.1f, -2f, 2f);
            npc.velocity.Y *= 0.3f;
            if (!OmenAlive(npc, ModContent.ProjectileType<TempleDropOmen>())) {
                //预告缺位→回冷却（无预告不出手）
                CrawlerAbort(npc);
                return;
            }
            if (timer <= 0) {
                phase = CwPlunge;
                timer = TempleDropOmen.PlungeWindowFrames;
                npc.netUpdate = true;
                return;
            }
            if (timer % 6 == 0) {
                npc.netUpdate = true;
            }
        }

        private void CrawlerPlungeTick(NPC npc) {
            timer--;
            int t = TempleDropOmen.PlungeWindowFrames - timer;
            float env = MobDash.Envelope(t, TempleDropOmen.PlungeRise, TempleDropOmen.PlungeHold, TempleDropOmen.PlungeDecay);
            //垂直包络扑落；横向只做贴柱伺服。两项都是承诺位移，除回提速补偿
            float gain = MoveGain(npc);
            npc.velocity.Y = (PlungePeakByTier[boundTier - 1] / gain) * env;
            npc.velocity.X = MathHelper.Clamp((lockPoint.X - npc.Center.X) * 0.15f, -3f, 3f) / gain;
            bool landed = t > TempleDropOmen.PlungeRise && (npc.collideY || npc.Bottom.Y >= lockPoint.Y - 4f);
            if (landed || timer <= 0) {
                //落地滚身后摇：小幅侧滚泄力（收势机动，非攻击瞄准）
                npc.velocity.Y = 0f;
                npc.velocity.X = npc.direction * (RollSpeed / gain);
                phase = CwRoll;
                timer = PlungeRollFrames;
                npc.netUpdate = true;
                return;
            }
            if (timer % 6 == 0) {
                npc.netUpdate = true;
            }
        }

        private void CrawlerRollTick(NPC npc) {
            timer--;
            npc.velocity.X *= RollDamp;
            if (timer <= 0) {
                npc.velocity.X = 0f;
                EndCrawlerMove(npc);
                return;
            }
            if (timer % 6 == 0) {
                npc.netUpdate = true;
            }
        }

        private void CrawlerWindupTick(NPC npc) {
            timer--;
            npc.velocity.X *= PounceWindupDamp;
            if (!TryGetTarget(npc, out Player player)) {
                CrawlerAbort(npc);
                return;
            }
            if (timer <= 0) {
                //锁向即承诺：扑咬期不再重瞄；起跳小上抬为位移承诺项
                lockDir = (player.Center - npc.Center).ToRotation();
                npc.velocity.Y = PounceUpKick / MoveGain(npc);
                phase = CwPounce;
                timer = PounceFrames;
                npc.netUpdate = true;
                return;
            }
            if (timer % 6 == 0) {
                npc.netUpdate = true;
            }
        }

        private void CrawlerPounceTick(NPC npc) {
            timer--;
            int t = PounceFrames - timer;
            if (t > PounceRise && npc.collideX) {
                timer = Math.Min(timer, PounceDecay);
            }
            float dirX = MathF.Cos(lockDir) >= 0f ? 1f : -1f;
            float env = MobDash.Envelope(t, PounceRise, PounceHold, PounceDecay);
            //横向包络扑咬，纵向交给原版重力（起跳上抬已在锁定帧注入）
            npc.velocity.X = dirX * (PouncePeakByTier[boundTier - 1] / MoveGain(npc)) * env;
            if (timer % 6 == 0) {
                npc.netUpdate = true;
            }
            if (timer <= 0) {
                npc.velocity.X = 0f;
                phase = CwRecover;
                timer = PounceRecoverFrames;
                npc.netUpdate = true;
            }
        }

        private void CrawlerRecoverTick(NPC npc) {
            timer--;
            npc.velocity.X *= RollDamp;
            if (timer <= 0) {
                npc.velocity.X = 0f;
                EndCrawlerMove(npc);
            }
        }

        private void EndCrawlerMove(NPC npc) {
            phase = PhaseIdle;
            omenIndex = -1;
            cooldown = CrawlerCooldownByTier[boundTier - 1] + Main.rand.Next(CooldownJitter + 1);
            npc.netUpdate = true;
        }
        #endregion

        #region 三连俯冲（FlyingSnake）
        private void SnakeTick(NPC npc) {
            switch (phase) {
                case SnTelegraph: SnakeTelegraphTick(npc); break;
                case SnDive: SnakeDiveTick(npc); break;
                case SnAdjust: SnakeAdjustTick(npc); break;
                case SnRecover: SnakeRecoverTick(npc); break;
                default: TryStartSnake(npc); break;
            }
        }

        private void TryStartSnake(NPC npc) {
            Player player = IdleGate(npc);
            if (player == null) {
                return;
            }
            float dist = npc.Distance(player.Center);
            if (dist < SnakeMinRange || dist > SnakeMaxRange
                || npc.Center.Y > player.Center.Y - SnakeMinHeightAdv || !CanSee(npc, player)) {
                cooldown = RetryDelay;
                return;
            }
            if (CountActive(ModContent.ProjectileType<TempleDiveOmen>()) >= OmenCap) {
                cooldown = RetryDelay;
                return;
            }
            stage = 0;
            if (!SpawnDiveOmen(npc, player)) {
                cooldown = RetryDelay;
                return;
            }
            //高位蓄势：压速入悬停
            npc.velocity *= 0.4f;
            phase = SnTelegraph;
            timer = TempleDiveOmen.TelegraphByStage[0];
            npc.netUpdate = true;
        }

        /// <summary>植当段俯冲标线（预告即实体，生成失败=本段作废）；锁向初值此刻写入，锁定帧再定格</summary>
        private bool SpawnDiveOmen(NPC npc, Player player) {
            int idx = Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, Vector2.Zero,
                ModContent.ProjectileType<TempleDiveOmen>(), 0, 0f, Main.myPlayer,
                SrcPack(npc), stage, 0f);
            if (idx < 0 || idx >= Main.maxProjectiles) {
                return false;
            }
            omenIndex = idx;
            lockDir = (player.Center - npc.Center).ToRotation();
            return true;
        }

        private void SnakeAbort(NPC npc) {
            phase = PhaseIdle;
            timer = 0;
            stage = 0;
            omenIndex = -1;
            cooldown = IneligibleDelay;
            npc.netUpdate = true;
        }

        private void SnakeTelegraphTick(NPC npc) {
            timer--;
            if (!OmenAlive(npc, ModContent.ProjectileType<TempleDiveOmen>())) {
                //预告缺位→整套俯冲作废（无预告不出手）
                SnakeAbort(npc);
                return;
            }
            //悬停脉冲：离散压速抵住原版游荡，让标线贴住实际出发点
            if (timer % SnakeHoverDampInterval == 0) {
                npc.velocity *= SnakeHoverDamp;
                npc.netUpdate = true;
            }
            if (timer == TempleDiveOmen.LockFreezeFrames) {
                //锁定帧：方向自此为承诺，写回预兆实体做各端权威纠偏
                if (TryGetTarget(npc, out Player player)) {
                    lockDir = (player.Center - npc.Center).ToRotation();
                }
                Projectile omen = Main.projectile[omenIndex];
                omen.ai[2] = lockDir + 10f;
                omen.netUpdate = true;
            }
            if (timer <= 0) {
                phase = SnDive;
                timer = TempleDiveOmen.DiveWindowFrames;
                npc.netUpdate = true;
            }
        }

        private void SnakeDiveTick(NPC npc) {
            timer--;
            int t = TempleDiveOmen.DiveWindowFrames - timer;
            if (t > TempleDiveOmen.DiveRise && (npc.collideX || npc.collideY)) {
                //撞墙即坠入衰减段泄力
                timer = Math.Min(timer, TempleDiveOmen.DiveDecay);
            }
            float peak = TempleDiveOmen.DivePeakByStage[stage] / MoveGain(npc);
            npc.velocity = MobDash.Velocity(lockDir.ToRotationVector2(), peak,
                t, TempleDiveOmen.DiveRise, TempleDiveOmen.DiveHold, TempleDiveOmen.DiveDecay);
            if (timer % 6 == 0) {
                npc.netUpdate = true;
            }
            if (timer <= 0) {
                //残速软清，扑翼权还给原版
                npc.velocity *= 0.3f;
                if (stage < SnakeDiveStages - 1) {
                    phase = SnAdjust;
                    timer = SnakeAdjustFrames;
                }
                else {
                    phase = SnRecover;
                    timer = SnakeRecoverFrames;
                    omenIndex = -1;
                }
                npc.netUpdate = true;
            }
        }

        /// <summary>段间调整位：20 帧喘息窗，原版 AI 自由走位，窗尾起下一段（每段都有完整预告并重新锁向）</summary>
        private void SnakeAdjustTick(NPC npc) {
            timer--;
            if (timer > 0) {
                return;
            }
            if (!TryGetTarget(npc, out Player player)) {
                SnakeAbort(npc);
                return;
            }
            stage++;
            if (!SpawnDiveOmen(npc, player)) {
                SnakeAbort(npc);
                return;
            }
            phase = SnTelegraph;
            timer = TempleDiveOmen.TelegraphByStage[stage];
            npc.netUpdate = true;
        }

        private void SnakeRecoverTick(NPC npc) {
            timer--;
            if (timer <= 0) {
                phase = PhaseIdle;
                stage = 0;
                cooldown = SnakeCooldownByTier[boundTier - 1] + Main.rand.Next(CooldownJitter + 1);
                npc.netUpdate = true;
            }
        }
        #endregion

        #region 表现层（各端本地跑，客户端由镜像相位驱动）
        /// <summary>客户端镜像推进：全部相位一律倒数，只喂表现不做决策</summary>
        private void MirrorAdvance() {
            if (phase != PhaseIdle && timer > 0) {
                timer--;
            }
        }

        /// <summary>相位沿一次性音效：以本地已播相位号防重同步重放（快照回卷不二响）</summary>
        private void PhaseCue(NPC npc) {
            if (family == TplFamily.Monk) {
                if (phase == MkDash) {
                    SoundEngine.PlaySound(SoundID.Item1 with { Volume = 0.6f, Pitch = -0.1f, MaxInstances = 4 }, npc.Center);
                }
                else if (phase == MkSweep) {
                    SoundEngine.PlaySound(SoundID.Item1 with { Volume = 0.65f, Pitch = 0.25f, MaxInstances = 4 }, npc.Center);
                }
                else if (phase == MkPause) {
                    SoundEngine.PlaySound(SoundID.Dig with { Volume = 0.45f, Pitch = -0.3f, MaxInstances = 4 }, npc.Center);
                }
            }
            else if (family == TplFamily.Crawler) {
                if (phase == CwPounce) {
                    SoundEngine.PlaySound(SoundID.Item1 with { Volume = 0.55f, Pitch = -0.25f, MaxInstances = 4 }, npc.Center);
                }
                else if (phase == CwRoll) {
                    SoundEngine.PlaySound(SoundID.Dig with { Volume = 0.6f, Pitch = -0.15f, MaxInstances = 4 }, npc.Center);
                }
            }
            //飞蛇的锁定/俯冲音效由同步的标线预兆实体各端本地播放
        }

        private void PresentTick(NPC npc) {
            if (phase != lastCuePhase) {
                if (phase != PhaseIdle && !Main.dedServ) {
                    PhaseCue(npc);
                }
                lastCuePhase = phase;
            }
            if (Main.dedServ || phase == PhaseIdle) {
                return;
            }
            switch (family) {
                case TplFamily.Monk: MonkPresent(npc); break;
                case TplFamily.Crawler: CrawlerPresent(npc); break;
                case TplFamily.Snake: SnakePresent(npc); break;
            }
        }

        private void MonkPresent(NPC npc) {
            if (phase == MkWindup) {
                //蹲身压桩：脚下石尘外滚+金焰点，身体后仰读作蓄势
                float progress = 1f - timer / (float)MonkWindupFrames;
                for (int i = 0; i < 2; i++) {
                    Dust dust = Dust.NewDustDirect(npc.BottomLeft - new Vector2(0f, 6f), npc.width, 6,
                        DustID.Stone, 0f, 0f, 120, default, 1.1f);
                    dust.velocity = new Vector2(Main.rand.NextFloat(-1.2f, 1.2f), -Main.rand.NextFloat(0.2f, 0.8f));
                    dust.noGravity = true;
                }
                if (Main.rand.NextBool(3)) {
                    Dust spark = Dust.NewDustDirect(npc.position, npc.width, npc.height,
                        DustID.GoldFlame, 0f, 0f, 100, default, 0.9f);
                    spark.velocity *= 0.3f;
                    spark.noGravity = true;
                }
                npc.rotation = -npc.direction * MonkWindupLean * progress;
                Lighting.AddLight(npc.Center, 0.25f + 0.2f * progress, 0.18f, 0.05f);
                return;
            }
            if (phase == MkDash || phase == MkSweep) {
                int total = phase == MkDash ? MonkDashFrames : MonkSweepFrames;
                int rise = phase == MkDash ? MonkDashRise : MonkSweepRise;
                int hold = phase == MkDash ? MonkDashHold : MonkSweepHold;
                int decay = phase == MkDash ? MonkDashDecay : MonkSweepDecay;
                float env = MobDash.Envelope(total - timer, rise, hold, decay);
                //按包络强度压身发力（读作出爪而非贴图平移）
                npc.rotation = MobDash.Lean(env, MathF.Cos(lockDir), MonkDashLeanMax);
                if (Main.rand.NextBool(2)) {
                    Dust trail = Dust.NewDustDirect(npc.position, npc.width, npc.height,
                        DustID.GoldFlame, 0f, 0f, 110, default, 1.1f);
                    trail.velocity = -npc.velocity * 0.25f;
                    trail.noGravity = true;
                }
                if (phase == MkSweep) {
                    //尾扫环尘：绕身旋转一圈的回旋轨迹
                    float ang = MathHelper.TwoPi * (total - timer) / total;
                    Dust ring = Dust.NewDustPerfect(npc.Center + ang.ToRotationVector2() * 30f,
                        DustID.GoldFlame, ang.ToRotationVector2().RotatedBy(MathHelper.PiOver2) * 1.5f, 110, default, 1f);
                    ring.noGravity = true;
                }
                Lighting.AddLight(npc.Center, 0.4f, 0.28f, 0.08f);
                return;
            }
            if (phase == MkPause) {
                //顿帧：定身收劲
                npc.rotation *= 0.7f;
                return;
            }
            if (phase == MkExhaust) {
                //力竭惩罚窗：塌肩喘息烟
                npc.rotation *= 0.8f;
                if (timer <= 1) {
                    npc.rotation = 0f;//倾角复位，残姿不留给原版
                }
                if (Main.rand.NextBool(4)) {
                    Dust smoke = Dust.NewDustDirect(npc.position, npc.width, npc.height,
                        DustID.Smoke, 0f, -0.5f, 150, default, 0.9f);
                    smoke.noGravity = true;
                }
            }
        }

        private void CrawlerPresent(NPC npc) {
            //爬行形态原版自转，本层不写 rotation 防打架，全部用尘表达
            if (phase == CwOmen || phase == CwWindup) {
                for (int i = 0; i < (phase == CwOmen ? 1 : 2); i++) {
                    Dust dust = Dust.NewDustDirect(npc.position, npc.width, npc.height,
                        DustID.Stone, 0f, phase == CwOmen ? 1.2f : 0f, 120, default, 1f);
                    dust.noGravity = phase != CwOmen;//贴顶时碎屑带重力先落，提示上方有物
                }
                Lighting.AddLight(npc.Center, 0.28f, 0.16f, 0.05f);
                return;
            }
            if (phase == CwPlunge || phase == CwPounce) {
                if (Main.rand.NextBool(2)) {
                    Dust trail = Dust.NewDustDirect(npc.position, npc.width, npc.height,
                        DustID.Stone, 0f, 0f, 110, default, 1.1f);
                    trail.velocity = -npc.velocity * 0.2f;
                    trail.noGravity = true;
                }
                return;
            }
            if (phase == CwRoll && timer >= PlungeRollFrames - 1) {
                //落地帧：石尘环冲击圈
                for (int i = 0; i < 10; i++) {
                    float ang = MathHelper.TwoPi * i / 10f;
                    Dust burst = Dust.NewDustPerfect(npc.Bottom, DustID.Stone,
                        ang.ToRotationVector2() * new Vector2(2.6f, 0.9f), 100, default, 1.2f);
                    burst.noGravity = true;
                }
            }
        }

        private void SnakePresent(NPC npc) {
            if (phase == SnTelegraph) {
                //盘旋凝势：金焰点身（标线主体由预兆实体绘制）
                if (Main.rand.NextBool(2)) {
                    Dust dust = Dust.NewDustDirect(npc.position, npc.width, npc.height,
                        DustID.GoldFlame, 0f, 0f, 120, default, 0.9f);
                    dust.velocity *= 0.3f;
                    dust.noGravity = true;
                }
                return;
            }
            if (phase == SnDive) {
                for (int i = 0; i < 2; i++) {
                    Dust trail = Dust.NewDustDirect(npc.position, npc.width, npc.height,
                        DustID.GoldFlame, 0f, 0f, 100, default, 1.1f);
                    trail.velocity = -npc.velocity * 0.15f;
                    trail.noGravity = true;
                }
                Lighting.AddLight(npc.Center, 0.4f, 0.3f, 0.1f);
            }
        }
        #endregion

        /// <summary>庙火余烬：蜥蜴系倒下 25% 在尸位留火斑（权威端裁决，isClient 双保险）</summary>
        public override void OnKill(NPC npc) {
            if (boundTier <= 0 || VaultUtils.isClient) {
                return;
            }
            if (family != TplFamily.Monk && family != TplFamily.Crawler) {
                return;
            }
            if (!Eligible(npc) || !Main.rand.NextBool(EmberChanceDenom)) {
                return;
            }
            int emberType = ModContent.ProjectileType<TempleEmberProj>();
            if (CountActive(emberType) >= EmberCap) {
                return;
            }
            //斑间强制间距：新烬离任何既有烬太近则不留（生成检查真正读取的公平常量）
            foreach (Projectile proj in Main.ActiveProjectiles) {
                if (proj.type == emberType && proj.Distance(npc.Center) < EmberMinSpacing) {
                    return;
                }
            }
            int damage = Math.Max(1, (int)(npc.damage * EmberDamageFrac));
            Projectile.NewProjectile(npc.GetSource_Death(), npc.Center, Vector2.Zero,
                emberType, damage, 0f, Main.myPlayer);
        }

        /// <summary>
        /// 相位镜像随 SyncNPC 过线（GlobalNPC 实例字段本身不同步）：
        /// 活跃时付相位/段号/计时/锁向，空闲帧位=0 自清客户端残留，丢包由低频重推自愈
        /// </summary>
        public override void SendExtraAI(NPC npc, BitWriter bitWriter, BinaryWriter binaryWriter) {
            bool engaged = phase != PhaseIdle;
            bitWriter.WriteBit(engaged);
            if (!engaged) {
                return;
            }
            binaryWriter.Write(phase);
            binaryWriter.Write(stage);
            binaryWriter.Write((short)timer);
            binaryWriter.Write(lockDir);
        }

        public override void ReceiveExtraAI(NPC npc, BitReader bitReader, BinaryReader binaryReader) {
            if (!bitReader.ReadBit()) {
                phase = PhaseIdle;
                timer = 0;
                stage = 0;
                return;
            }
            //先读齐再用：流对齐优先，哪怕本端档位未绑定也要消费同样的字节数
            phase = binaryReader.ReadByte();
            stage = binaryReader.ReadByte();
            timer = binaryReader.ReadInt16();
            lockDir = binaryReader.ReadSingle();
        }
    }
}
