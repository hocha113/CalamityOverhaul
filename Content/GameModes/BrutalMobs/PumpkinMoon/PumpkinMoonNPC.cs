using CalamityOverhaul.Content.GameModes.BrutalMobs.Common;
using CalamityOverhaul.Content.GameModes.BrutalMobs.PumpkinMoon.Projectiles;
using System;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.PumpkinMoon
{
    /// <summary>
    /// 南瓜月「祭火与镰阵」行为层：叠加在原版 AI 之上，不接管、不动数值属性。
    /// 稻草人族/小木灵=祭火小卒，战斗内分两风味：稻草人1-5 聚火抛掷火种（落点带具名缺口），
    /// 稻草人6-10+小木灵 聚火自燃突进（包络塑形，仅小木灵二连）；亡语仍掉落地面祭火种，燃起前可踩灭；
    /// 地狱犬/无头骑士=定向冲锋（预告线锁定即承诺，骑士沿途撒火种）；
    /// 幽灵=火种祭圈（圈心锁定的公转灯魂环，带具名旋转缺口）。
    /// 小 Boss（哀木/南瓜王）走同一叠加形态，各持两个签名技（预告 ≥40 帧，每实例同刻至多一技）：
    /// 哀木=祭火炮排（落点标记弹道）+根须墙（带具名缺口）；南瓜王=镰刃轮盘（旋转环带缺口）+爪击十字（锁定承诺）。
    /// 小 Boss 按显式类型名单放行、不检查 boss 旗标（离线不可证）；速度补偿系数运行时读旗标，
    /// 且小 Boss 机制零速度注入，天然与旗标无关。南瓜月波次值只读作风味输入（高波次缩短冷却），不碰计分。
    /// 决策全在权威端（客户端 PostAI 早退），客户端可见状态一律来自已同步的弹幕实体
    /// </summary>
    internal class PumpkinMoonNPC : GlobalNPC
    {
        public override bool InstancePerEntity => true;

        //==== 通用节奏 ====
        /// <summary>条件未满足的重试间隔</summary>
        private const int RetryDelay = 30;
        /// <summary>资格不符（雕像怪等）的复查间隔</summary>
        private const int IneligibleDelay = 120;
        /// <summary>出生首攻等待窗，随机错开避免同屏齐动（M7 契约收进 60~180）</summary>
        private const int FirstCooldownMin = 60;
        private const int FirstCooldownMax = 180;
        private const int CooldownJitter = 60;
        /// <summary>高波次阈值：达到后小 Boss 冷却缩短、火种延燃（波次值只读，风味输入）</summary>
        private const int HighWaveThreshold = 15;
        private const float HighWaveCooldownMult = 0.85f;

        //==== 祭火种（稻草人族死亡掉落 / 骑士冲锋沿途）====
        /// <summary>全局同时在场火种上限（并发闸，超限跳过掉落）</summary>
        private const int EmberGlobalCap = 6;
        /// <summary>火种伤害 = npc.damage（已缩放值）× 此系数</summary>
        private const float EmberDamageFrac = 0.45f;
        /// <summary>燃烧存续帧（档位只延长存续）</summary>
        private static readonly int[] EmberLitByTier = [150, 195, 240];
        /// <summary>高波次的延燃追加帧</summary>
        private const int EmberHighWaveBonus = 45;
        /// <summary>火种落点向下寻地面的最大格数</summary>
        private const int EmberGroundScanTiles = 12;

        //==== 祭火小卒战斗招式（投火：稻草人1-5 / 自燃突进：稻草人6-10+小木灵）====
        /// <summary>招式基础冷却（ByTier 只缩短节奏，机制形状不变）</summary>
        private static readonly int[] EmberMoveCooldownByTier = [560, 485, 420];
        /// <summary>同刻处于前摇/突进/后摇的祭火小卒全局上限（并发闸，仅触发时计数）</summary>
        private const int EmberActiveCap = 6;
        /// <summary>投火前摇帧（胸口火光渐亮，≥30 契约）</summary>
        private const int ThrowWindupFrames = 32;
        /// <summary>投火收势后摇帧</summary>
        private const int ThrowRecoverFrames = 18;
        /// <summary>具名缺口：相邻火种落点强制间距（格），布点循环按其展开；
        /// 扣除火种体宽 24px 与两侧散布抖动后间隙仍 ≥2.7 格，人站间隙即安全，档位与型号均不得收窄</summary>
        private const float EmberGapTiles = 5f;
        /// <summary>落点散布抖动上限（远小于间距，不吞缺口）</summary>
        private const float ThrowScatter = 6f;
        private const float ThrowMinRangeX = 120f;
        private const float ThrowMaxRangeX = 520f;
        private const float ThrowMaxRangeY = 260f;
        /// <summary>掷种数 ByType（行=稻草人1..5，组内签名差异之一）</summary>
        private static readonly int[] ThrowCountByType = [2, 3, 2, 3, 2];
        /// <summary>火种抛体滞空帧 ByType（帧多=弧高：3 号高抛慢种、4 号平射急火）</summary>
        private static readonly int[] ThrowFlightByType = [26, 32, 44, 24, 36];
        /// <summary>落地火种燃烧存续修正 ByType（叠加 EmberLitByTier：3 号久燃、4 号速熄、5 号最久）</summary>
        private static readonly int[] ThrowLitDeltaByType = [0, 0, 30, -30, 75];
        /// <summary>火种引燃期延长 ByType（只许 ≥0：5 号闷燃巨种，踩灭窗只放宽不收紧）</summary>
        private static readonly int[] ThrowKindleExtraByType = [0, 0, 0, 0, 12];
        private const float IgniteMinRangeX = 90f;
        private const float IgniteMaxRangeX = 400f;
        private const float IgniteMaxRangeY = 140f;
        /// <summary>自燃组聚火前摇帧（行=稻草人6..10、小木灵=5；≥24 契约，8 号重踏聚得最久）</summary>
        private static readonly int[] IgniteWindupByType = [24, 26, 32, 24, 24, 24];
        /// <summary>突进名义峰速（未含提速补偿，注入时除回 MoveGain；9 号燎原最快、8 号最沉）</summary>
        private static readonly float[] IgnitePeakByType = [8.5f, 8f, 7f, 10f, 8.5f, 8f];
        /// <summary>突进包络三段：爬升/保持/衰减帧（保持长=突进远：7 号长驱、小木灵短刺）</summary>
        private static readonly int[] IgniteRiseByType = [6, 6, 8, 5, 6, 5];
        private static readonly int[] IgniteHoldByType = [8, 16, 12, 8, 10, 6];
        private static readonly int[] IgniteDecayByType = [8, 10, 12, 7, 8, 7];
        /// <summary>力竭后摇帧（10 号破势急停收招最快、8 号最僵）</summary>
        private static readonly int[] IgniteRecoverByType = [12, 20, 26, 14, 8, 16];
        /// <summary>小木灵二连：第二段重新前摇帧（重新锁向=新的承诺）</summary>
        private const int SplinterChainWindup = 12;
        /// <summary>突进朝向倾斜上限（弧度，读作发力，后摇回正）</summary>
        private const float IgniteLeanMax = 0.16f;

        //==== 定向冲锋（地狱犬 / 无头骑士）====
        private static readonly int[] ChargeCooldownByTier = [320, 260, 210];
        private static readonly float[] HoundChargeSpeedByTier = [10.5f, 11.5f, 12.5f];
        private static readonly float[] HorsemanChargeSpeedByTier = [12f, 13f, 14f];
        /// <summary>冲锋窗命中的点燃时长</summary>
        private const int ChargeBurnTicks = 120;
        /// <summary>冲锋包络爬升帧（保持段=突进窗总帧−爬升−衰减，犬/骑士窗长不同）</summary>
        private const int ChargeRiseFrames = 4;
        /// <summary>冲锋包络力竭衰减帧（撞墙钳短与衰减段先到者为准，末帧包络归零即清残速）</summary>
        private const int ChargeDecayFrames = 8;
        /// <summary>起手帧横向初始推力系数（衔接包络首帧，避免速度空窗）</summary>
        private const float ChargeLaunchPush = 0.35f;
        /// <summary>冲锋预告全局并发上限</summary>
        private const int ChargeConcurrentCap = 6;
        /// <summary>骑士单次冲锋沿途火种数上限与间隔帧</summary>
        private const int HorsemanTrailEmbers = 2;
        private const int HorsemanTrailInterval = 14;
        private const float HoundMinRangeX = 130f;
        private const float HoundMaxRangeX = 560f;
        private const float HoundMaxRangeY = 200f;
        private const float HorsemanMinRangeX = 170f;
        private const float HorsemanMaxRangeX = 760f;
        private const float HorsemanMaxRangeY = 240f;

        //==== 火种祭圈（幽灵）====
        private static readonly int[] RitualCooldownByTier = [520, 440, 380];
        private const float RitualMinRange = 150f;
        private const float RitualMaxRange = 520f;
        /// <summary>灯魂伤害 = npc.damage × 此系数</summary>
        private const float RitualDamageFrac = 0.5f;

        //==== 哀木签名技 ====
        private static readonly int[] TreantCooldownByTier = [430, 370, 320];
        /// <summary>炮排落点数（档位只加落点）</summary>
        private static readonly int[] MortarCountByTier = [3, 4, 5];
        /// <summary>相邻落点中心间距（公平阀门：布点循环按其倍数展开，走廊=安全带）</summary>
        private const float MortarSpacing = 230f;
        /// <summary>落点随机抖动上限（远小于间距，不吞走廊）</summary>
        private const float MortarJitter = 16f;
        private const float MortarDamageFrac = 0.65f;
        private const float MortarMinRange = 160f;
        private const float MortarMaxRange = 980f;
        private const int MortarGroundScanTiles = 42;
        /// <summary>根须墙槽位数与间距；缺口=连续 RootGapSlots 个槽位从不生成（公平阀门，布点循环真正跳过）</summary>
        private const int RootWallSlots = 9;
        private const float RootSlotSpacing = 42f;
        private const int RootGapSlots = 2;
        /// <summary>根须驻留帧（档位只延长驻留）</summary>
        private static readonly int[] RootHoldByTier = [46, 58, 70];
        private const float RootDamageFrac = 0.6f;
        private const float RootMaxRange = 640f;
        private const int RootGroundScanTiles = 10;

        //==== 南瓜王签名技 ====
        private static readonly int[] PumpkingCooldownByTier = [400, 340, 290];
        private const float WheelDamageFrac = 0.6f;
        private const float CrossDamageFrac = 0.7f;
        private const float CrossMaxRange = 760f;

        private const byte PhaseIdle = 0;
        private const byte PhaseTelegraph = 1;
        private const byte PhaseStrike = 2;
        /// <summary>签名技/祭圈进行中（每实例同刻至多一技）</summary>
        private const byte PhaseBusy = 3;

        private enum PmkFamily : byte
        {
            None,
            /// <summary>祭火小卒：稻草人十式 + 小木灵</summary>
            Ember,
            /// <summary>定向冲锋：地狱犬 / 无头骑士</summary>
            Charge,
            /// <summary>火种祭圈：幽灵</summary>
            Ritual,
            /// <summary>小 Boss 哀木</summary>
            Treant,
            /// <summary>小 Boss 南瓜王</summary>
            Pumpking,
        }

        /// <summary>本个体出生时绑定的档位，0=未绑定（镜像 GameModeNPC；中途切模式不影响已出生个体）</summary>
        private int boundTier;
        private PmkFamily family;
        private byte phase;
        private int timer;
        private int cooldown;
        /// <summary>锁定冲锋方向（锁定帧后不再改写，预告即承诺）</summary>
        private float lockDir;
        /// <summary>冲锋期抵住原版横向衰减的持有速度</summary>
        private float dashVX;
        /// <summary>本次冲锋的预告体槽位（权威端私产）</summary>
        private int omenIndex = -1;
        /// <summary>小 Boss 双技交替开关（权威端私产）</summary>
        private bool moveToggle;
        private int emberDropsLeft;
        private int emberDropTick;
        /// <summary>投火锚点：前摇起手锁定的落点展开中心（预告即承诺）</summary>
        private Vector2 lockPoint;
        /// <summary>小木灵二连剩余段数（权威端私产）</summary>
        private int chainLeft;

        /// <summary>
        /// 类型表。小 Boss（哀木/南瓜王）为显式名单放行；PumpkingBlade（南瓜王镰刃部件）不入表：
        /// 部件间 ai 链接方式离线不可查证，机制全部只锚定本体、不依赖部件关系
        /// </summary>
        private static PmkFamily ResolveFamily(int type) {
            if (type >= NPCID.Scarecrow1 && type <= NPCID.Scarecrow10) {
                return PmkFamily.Ember;
            }
            return type switch {
                NPCID.Splinterling => PmkFamily.Ember,
                NPCID.Hellhound or NPCID.HeadlessHorseman => PmkFamily.Charge,
                NPCID.Poltergeist => PmkFamily.Ritual,
                NPCID.MourningWood => PmkFamily.Treant,
                NPCID.Pumpking => PmkFamily.Pumpking,
                _ => PmkFamily.None,
            };
        }

        public override bool AppliesToEntity(NPC entity, bool lateInstantiation)
            => lateInstantiation && ResolveFamily(entity.type) != PmkFamily.None;

        public override void SetDefaults(NPC npc) {
            boundTier = 0;
            family = PmkFamily.None;
            int tier = GameModeSystem.EffectiveTier;
            if (tier <= 0) {
                return;
            }
            PmkFamily resolved = ResolveFamily(npc.type);
            if (resolved == PmkFamily.None) {
                return;
            }
            family = resolved;
            boundTier = tier;
            //首攻错拍：冷却是权威端决策私产（客户端副本不被读取），Main.rand 无同步语义；
            //此刻 npc.whoAmI 恒为 0（NewNPC 之后才赋值），不可用作错拍源
            cooldown = FirstCooldownMin + Main.rand.Next(FirstCooldownMax - FirstCooldownMin + 1);
        }

        /// <summary>小怪机制入口资格：友方/无敌/Boss 旗标/小动物载体/雕像怪/共享血池体节逐项排除</summary>
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
        /// 小 Boss 触发资格：显式类型名单已由 <see cref="ResolveFamily"/> 放行，
        /// 此处不检查 boss 旗标（离线查证不实，机制与旗标无关），其余排除照常
        /// </summary>
        private static bool MinibossReady(NPC npc, out Player target) {
            target = null;
            if (npc.dontTakeDamage || npc.SpawnedFromStatue || !npc.HasValidTarget) {
                return false;
            }
            Player player = Main.player[npc.target];
            if (!player.Alives()) {
                return false;
            }
            target = player;
            return true;
        }

        /// <summary>
        /// 提速位移补偿：<see cref="GameModeNPC.PostAI"/> 对非 Boss 怪按 velocity×SpeedBonus 追加位置推进，
        /// 本层注入的承诺性速度一律除回该系数（位移项除回、重力项不除）。
        /// 口径与 GameModeNPC.RageEligible 一致且运行时读旗标：boss 旗标个体与体节不吃提速层，系数为 1
        /// </summary>
        private float MoveGain(NPC npc) => !npc.boss && npc.realLife < 0 ? 1f + GameModeTuning.SpeedBonus(boundTier) : 1f;

        /// <summary>同型弹幕并发计数（仅触发时调用，自愈无漂移）</summary>
        private static int CountActive(int projType) {
            int count = 0;
            for (int i = 0; i < Main.maxProjectiles; i++) {
                if (Main.projectile[i].active && Main.projectile[i].type == projType) {
                    count++;
                }
            }
            return count;
        }

        /// <summary>自基准点上方一格向下寻可站立地表，返回地表世界 Y（格顶），找不到返回 -1</summary>
        private static float FindSurfaceY(float worldX, float fromWorldY, int maxTiles) {
            int tx = (int)(worldX / 16f);
            int ty = (int)(fromWorldY / 16f) - 1;
            for (int i = 0; i < maxTiles; i++) {
                int y = ty + i;
                if (tx < 10 || tx > Main.maxTilesX - 10 || y < 10 || y > Main.maxTilesY - 10) {
                    return -1f;
                }
                if (WorldGen.ActiveAndWalkableTile(tx, y)) {
                    return y * 16f;
                }
            }
            return -1f;
        }

        /// <summary>小 Boss 冷却：基础+抖动，高波次缩短（南瓜月波次只读，权威端决策路径）</summary>
        private void SetMinibossCooldown(int[] table) {
            cooldown = table[boundTier - 1] + Main.rand.Next(CooldownJitter + 1);
            if (Main.pumpkinMoon && NPC.waveNumber >= HighWaveThreshold) {
                cooldown = (int)(cooldown * HighWaveCooldownMult);
            }
        }

        public override void PostAI(NPC npc) {
            if (boundTier <= 0) {
                return;
            }
            if (VaultUtils.isClient) {
                //决策只在权威端；客户端画面全部来自已同步的弹幕实体与 NPC 速度
                return;
            }
            switch (family) {
                case PmkFamily.Ember:
                    EmberStep(npc);
                    break;
                case PmkFamily.Charge:
                    ChargeStep(npc);
                    break;
                case PmkFamily.Ritual:
                    RitualStep(npc);
                    break;
                case PmkFamily.Treant:
                    TreantStep(npc);
                    break;
                case PmkFamily.Pumpking:
                    PumpkingStep(npc);
                    break;
            }
        }

        #region 祭火种
        public override void OnKill(NPC npc) {
            if (boundTier <= 0 || family != PmkFamily.Ember) {
                return;
            }
            //死亡资格懒式复查：SpawnedFromStatue 在 SetDefaults 之后才置位
            if (npc.SpawnedFromStatue || npc.realLife >= 0) {
                return;
            }
            //死亡机制的预告：火种落地后有 ≥30 帧无害引燃期（踩灭窗口），由实体自身承载
            TrySpawnEmber(npc, npc.Bottom, npc.type == NPCID.Splinterling ? 1 : 0);
        }

        /// <summary>
        /// 权威端落一枚祭火种的公共入口：全局限额、寻地面（亡语/骑士沿途/火种抛体共用同一把关）。
        /// flavorPack=风味+引燃延长×10（<see cref="PmkEmberProj"/> 的 ai[1] 口径）
        /// </summary>
        internal static bool SpawnEmberAt(IEntitySource source, Vector2 basePos, int damage, int litFrames, int flavorPack) {
            int emberType = ModContent.ProjectileType<PmkEmberProj>();
            if (CountActive(emberType) >= EmberGlobalCap) {
                return false;
            }
            float groundY = FindSurfaceY(basePos.X, basePos.Y, EmberGroundScanTiles);
            if (groundY < 0f) {
                return false;
            }
            Projectile.NewProjectile(source, new Vector2(basePos.X, groundY - 12f),
                Vector2.Zero, emberType, damage, 0f, Main.myPlayer, litFrames, flavorPack);
            return true;
        }

        /// <summary>权威端掉落一枚祭火种：档位存续、波次延燃风味</summary>
        private bool TrySpawnEmber(NPC npc, Vector2 basePos, int flavor) {
            int lit = EmberLitByTier[boundTier - 1];
            if (Main.pumpkinMoon && NPC.waveNumber >= HighWaveThreshold) {
                lit += EmberHighWaveBonus;
            }
            int damage = Math.Max(1, (int)(npc.damage * EmberDamageFrac));
            return SpawnEmberAt(npc.GetSource_FromAI(), basePos, damage, lit, flavor);
        }
        #endregion

        #region 祭火小卒战斗招式
        /// <summary>投火组（稻草人1-5）；其余祭火小卒（稻草人6-10+小木灵）走自燃突进</summary>
        private static bool EmberThrows(int type) => type >= NPCID.Scarecrow1 && type <= NPCID.Scarecrow5;

        /// <summary>自燃组行号：稻草人6..10→0..4，小木灵→5</summary>
        private static int IgniteRow(int type) => type == NPCID.Splinterling ? 5 : type - NPCID.Scarecrow6;

        /// <summary>突进执行窗总帧=包络三段之和（包络走完速度自然归零）</summary>
        private static int IgniteStrikeFrames(int row)
            => IgniteRiseByType[row] + IgniteHoldByType[row] + IgniteDecayByType[row];

        /// <summary>同刻施招（前摇/突进/后摇）的祭火小卒计数（并发闸，仅触发时调用，自愈无漂移）</summary>
        private static int CountBusyEmbers() {
            int count = 0;
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC other = Main.npc[i];
                if (other.active && other.TryGetGlobalNPC(out PumpkinMoonNPC pack)
                    && pack.family == PmkFamily.Ember && pack.phase != PhaseIdle) {
                    count++;
                }
            }
            return count;
        }

        private void EmberStep(NPC npc) {
            if (phase == PhaseIdle) {
                if (--cooldown > 0) {
                    return;
                }
                TryStartEmberMove(npc);
                return;
            }
            if (phase == PhaseTelegraph) {
                TickEmberWindup(npc);
                return;
            }
            if (phase == PhaseStrike) {
                TickIgniteDash(npc);
                return;
            }
            TickEmberRecover(npc);
        }

        private void TryStartEmberMove(NPC npc) {
            if (!Eligible(npc)) {
                cooldown = IneligibleDelay;
                return;
            }
            if (npc.target < 0 || npc.target >= Main.maxPlayers) {
                cooldown = RetryDelay;
                return;
            }
            Player player = Main.player[npc.target];
            if (!player.Alives()) {
                cooldown = RetryDelay;
                return;
            }
            if (CountBusyEmbers() >= EmberActiveCap) {
                cooldown = RetryDelay;
                return;
            }
            bool throws = EmberThrows(npc.type);
            float dx = Math.Abs(player.Center.X - npc.Center.X);
            float dy = Math.Abs(player.Center.Y - npc.Center.Y);
            bool ready = npc.velocity.Y == 0f
                && dx >= (throws ? ThrowMinRangeX : IgniteMinRangeX)
                && dx <= (throws ? ThrowMaxRangeX : IgniteMaxRangeX)
                && dy <= (throws ? ThrowMaxRangeY : IgniteMaxRangeY)
                && Collision.CanHit(npc.position, npc.width, npc.height, player.position, player.width, player.height);
            if (!ready) {
                cooldown = RetryDelay;
                return;
            }
            int windup;
            int strike = 0;
            lockDir = player.Center.X >= npc.Center.X ? 1f : -1f;
            if (throws) {
                //火种满额或目标脚下够不到地面：这轮不投（免得前摇后放空）
                if (CountActive(ModContent.ProjectileType<PmkEmberProj>()) >= EmberGlobalCap
                    || FindSurfaceY(player.Center.X, player.Center.Y, EmberGroundScanTiles) < 0f) {
                    cooldown = RetryDelay;
                    return;
                }
                //落点锚自此锁定（预告即承诺，落点不追人）
                lockPoint = player.Center;
                windup = ThrowWindupFrames;
            }
            else {
                //锁向不重瞄：方向自前摇起手即为承诺
                chainLeft = npc.type == NPCID.Splinterling ? 1 : 0;
                int row = IgniteRow(npc.type);
                windup = IgniteWindupByType[row];
                strike = IgniteStrikeFrames(row);
            }
            //预告即实体：胸口火光载体生成失败（弹幕位满）则整次进攻作废
            if (!SpawnEmberFlare(npc, windup, strike)) {
                cooldown = RetryDelay;
                return;
            }
            //刹车脉冲：急停蓄势（仅脉冲帧跟同步）
            npc.velocity.X *= 0.2f;
            npc.netUpdate = true;
            timer = windup;
            phase = PhaseTelegraph;
        }

        /// <summary>生成前摇火光预告体，槽位记入 omenIndex；失败=不起招（预告即实体）</summary>
        private bool SpawnEmberFlare(NPC npc, int windup, int strike) {
            float modePack = (EmberThrows(npc.type) ? 0f : 1f) + (lockDir < 0f ? 2f : 0f);
            omenIndex = Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, Vector2.Zero,
                ModContent.ProjectileType<PmkEmberFlareProj>(), 0, 0f, Main.myPlayer,
                (npc.whoAmI + 1) | (npc.type << 8), modePack, windup + strike * 1000);
            if (omenIndex < 0 || omenIndex >= Main.maxProjectiles) {
                omenIndex = -1;
                return false;
            }
            return true;
        }

        /// <summary>校验名下火光预告体仍有效（index+type+来源包三重校验，槽位不是身份）</summary>
        private bool TryGetEmberFlare(NPC npc, out Projectile flare) {
            flare = null;
            if (omenIndex < 0 || omenIndex >= Main.maxProjectiles) {
                return false;
            }
            Projectile proj = Main.projectile[omenIndex];
            if (!proj.active || proj.type != ModContent.ProjectileType<PmkEmberFlareProj>()
                || (int)proj.ai[0] != ((npc.whoAmI + 1) | (npc.type << 8))) {
                return false;
            }
            flare = proj;
            return true;
        }

        private void TickEmberWindup(NPC npc) {
            timer--;
            //预告体缺位（弹幕满额等异常）：不出招回冷却（失败方向=安全方向）
            if (!TryGetEmberFlare(npc, out _)) {
                phase = PhaseIdle;
                omenIndex = -1;
                chainLeft = 0;
                cooldown = EmberMoveCooldownByTier[boundTier - 1];
                return;
            }
            //离散刹车脉冲压住走位漂移（脉冲帧才跟同步）
            if (timer > 0 && timer % 8 == 0) {
                npc.velocity.X *= 0.4f;
                npc.netUpdate = true;
            }
            if (timer > 0) {
                return;
            }
            if (EmberThrows(npc.type)) {
                ReleaseSeeds(npc);
                phase = PhaseBusy;
                timer = ThrowRecoverFrames;
                npc.netUpdate = true;
                return;
            }
            //点火起步：速度由包络自零爬升（TickIgniteDash 逐帧赋形）
            phase = PhaseStrike;
            timer = IgniteStrikeFrames(IgniteRow(npc.type));
            npc.netUpdate = true;
        }

        /// <summary>释放帧：向锁定锚点定帧抛物线掷出火种抛体（落点=承诺，抵达帧由抛体放置火种）</summary>
        private void ReleaseSeeds(NPC npc) {
            int row = npc.type - NPCID.Scarecrow1;
            int flight = ThrowFlightByType[row];
            int lit = EmberLitByTier[boundTier - 1] + ThrowLitDeltaByType[row];
            if (Main.pumpkinMoon && NPC.waveNumber >= HighWaveThreshold) {
                lit += EmberHighWaveBonus;
            }
            int damage = Math.Max(1, (int)(npc.damage * EmberDamageFrac));
            int count = ThrowCountByType[row];
            Vector2 muzzle = npc.Center + new Vector2(npc.direction * 6f, -8f);
            for (int i = 0; i < count; i++) {
                //具名缺口：落点沿锚点按 EmberGapTiles 格间距展开，间隙即安全带（循环真正读取）
                float x = lockPoint.X + (i - (count - 1) * 0.5f) * (EmberGapTiles * 16f)
                    + Main.rand.NextFloat(-ThrowScatter, ThrowScatter);
                float groundY = FindSurfaceY(x, lockPoint.Y, EmberGroundScanTiles);
                if (groundY < 0f) {
                    continue;
                }
                Vector2 impact = new Vector2(x, groundY - 12f);
                //定帧弹道解算（与抛体每帧先加速后位移严格同源），落点即承诺
                Vector2 v0 = new Vector2((impact.X - muzzle.X) / flight,
                    (impact.Y - muzzle.Y) / flight - PmkEmberSeedProj.SeedGravity * (flight + 1) * 0.5f);
                Projectile.NewProjectile(npc.GetSource_FromAI(), muzzle, v0,
                    ModContent.ProjectileType<PmkEmberSeedProj>(), damage, 0f, Main.myPlayer,
                    flight, lit, ThrowKindleExtraByType[row]);
            }
        }

        private void TickIgniteDash(NPC npc) {
            int row = IgniteRow(npc.type);
            int strike = IgniteStrikeFrames(row);
            timer--;
            //包络塑形的承诺性速度：位移项除回提速补偿（重力不注入、自然不除）
            float env = MobDash.Envelope(strike - timer, IgniteRiseByType[row], IgniteHoldByType[row], IgniteDecayByType[row]);
            npc.velocity.X = lockDir * (IgnitePeakByType[row] / MoveGain(npc)) * env;
            npc.rotation = MobDash.Lean(env, lockDir, IgniteLeanMax);
            //撞墙即坠入衰减段（突进撞空=反制有效）
            if (npc.collideX) {
                timer = Math.Min(timer, IgniteDecayByType[row]);
            }
            if (timer % 6 == 0) {
                npc.netUpdate = true;
            }
            if (timer > 0) {
                return;
            }
            //包络已衰竭，显式清残速
            npc.velocity.X = 0f;
            if (chainLeft > 0 && StartIgniteChain(npc)) {
                return;
            }
            chainLeft = 0;
            phase = PhaseBusy;
            timer = IgniteRecoverByType[row];
            npc.netUpdate = true;
        }

        /// <summary>小木灵二连：第二段重新前摇并重新锁向（新的承诺）；预告体生成失败则直接收势</summary>
        private bool StartIgniteChain(NPC npc) {
            chainLeft--;
            if (npc.target < 0 || npc.target >= Main.maxPlayers || !Main.player[npc.target].Alives()) {
                return false;
            }
            lockDir = Main.player[npc.target].Center.X >= npc.Center.X ? 1f : -1f;
            if (!SpawnEmberFlare(npc, SplinterChainWindup, IgniteStrikeFrames(IgniteRow(npc.type)))) {
                return false;
            }
            npc.velocity.X *= 0.2f;
            npc.netUpdate = true;
            timer = SplinterChainWindup;
            phase = PhaseTelegraph;
            return true;
        }

        /// <summary>力竭后摇：衰减清残速、姿态回正，结束把控制权干净还给原版 AI</summary>
        private void TickEmberRecover(NPC npc) {
            timer--;
            npc.velocity.X *= 0.75f;
            npc.rotation *= 0.65f;
            if (timer % 6 == 0) {
                npc.netUpdate = true;
            }
            if (timer <= 0) {
                npc.rotation = 0f;
                phase = PhaseIdle;
                omenIndex = -1;
                chainLeft = 0;
                cooldown = EmberMoveCooldownByTier[boundTier - 1] + Main.rand.Next(CooldownJitter + 1);
                npc.netUpdate = true;
            }
        }
        #endregion

        #region 定向冲锋
        private bool IsHorseman(NPC npc) => npc.type == NPCID.HeadlessHorseman;

        private void ChargeStep(NPC npc) {
            if (phase == PhaseIdle) {
                if (--cooldown > 0) {
                    return;
                }
                TryStartCharge(npc);
                return;
            }
            if (phase == PhaseTelegraph) {
                TickChargeTelegraph(npc);
                return;
            }
            TickChargeStrike(npc);
        }

        private void TryStartCharge(NPC npc) {
            if (!Eligible(npc)) {
                cooldown = IneligibleDelay;
                return;
            }
            if (npc.target < 0 || npc.target >= Main.maxPlayers) {
                cooldown = RetryDelay;
                return;
            }
            Player player = Main.player[npc.target];
            if (!player.Alives()) {
                cooldown = RetryDelay;
                return;
            }
            bool horseman = IsHorseman(npc);
            float dx = Math.Abs(player.Center.X - npc.Center.X);
            float dy = Math.Abs(player.Center.Y - npc.Center.Y);
            bool ready = npc.velocity.Y == 0f
                && dx >= (horseman ? HorsemanMinRangeX : HoundMinRangeX)
                && dx <= (horseman ? HorsemanMaxRangeX : HoundMaxRangeX)
                && dy <= (horseman ? HorsemanMaxRangeY : HoundMaxRangeY)
                && Collision.CanHit(npc.position, npc.width, npc.height, player.position, player.width, player.height);
            if (!ready) {
                cooldown = RetryDelay;
                return;
            }
            if (CountActive(ModContent.ProjectileType<PmkChargeOmen>()) >= ChargeConcurrentCap) {
                cooldown = RetryDelay;
                return;
            }
            //预告即实体：生成失败（弹幕位满）则整次冲锋作废
            omenIndex = Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, Vector2.Zero,
                ModContent.ProjectileType<PmkChargeOmen>(), 0, 0f, Main.myPlayer,
                npc.whoAmI, horseman ? 1f : 0f, 0f);
            if (omenIndex < 0 || omenIndex >= Main.maxProjectiles) {
                omenIndex = -1;
                cooldown = RetryDelay;
                return;
            }
            //刹车脉冲：急停蓄势（仅脉冲帧跟同步）
            npc.velocity.X *= 0.2f;
            npc.netUpdate = true;
            timer = horseman ? PmkChargeOmen.HorsemanTelegraphFrames : PmkChargeOmen.HoundTelegraphFrames;
            phase = PhaseTelegraph;
        }

        /// <summary>校验名下预告体仍有效（index+type+ai 三重校验，槽位不是身份）</summary>
        private bool TryGetChargeOmen(int npcIndex, out Projectile omen) {
            omen = null;
            if (omenIndex < 0 || omenIndex >= Main.maxProjectiles) {
                return false;
            }
            Projectile proj = Main.projectile[omenIndex];
            if (!proj.active || proj.type != ModContent.ProjectileType<PmkChargeOmen>()
                || (int)proj.ai[0] != npcIndex) {
                return false;
            }
            omen = proj;
            return true;
        }

        private void TickChargeTelegraph(NPC npc) {
            timer--;
            bool horseman = IsHorseman(npc);
            //预告体缺位（弹幕满额等异常）：无预告不冲锋（失败方向=安全方向）
            if (!TryGetChargeOmen(npc.whoAmI, out Projectile omen)) {
                phase = PhaseIdle;
                omenIndex = -1;
                cooldown = ChargeCooldownByTier[boundTier - 1];
                return;
            }
            //中段再刹一次，压住走位漂移让出发点贴住预告线
            if (timer == 20) {
                npc.velocity.X *= 0.35f;
                npc.netUpdate = true;
            }
            int lockFrames = horseman ? PmkChargeOmen.HorsemanLockFrames : PmkChargeOmen.HoundLockFrames;
            if (timer == lockFrames) {
                //锁定帧：方向自此为承诺（浅角钳制与注入共用同一函数），写回预告体做各端权威纠偏
                if (npc.target >= 0 && npc.target < Main.maxPlayers && Main.player[npc.target].Alives()) {
                    lockDir = PmkChargeOmen.ClampChargeDir(npc.Center, Main.player[npc.target].Center);
                }
                else {
                    lockDir = npc.direction > 0 ? 0f : MathHelper.Pi;
                }
                omen.ai[2] = lockDir + 10f;
                omen.netUpdate = true;
            }
            if (timer <= 0) {
                //提速位移补偿：注入速度除回 (1+SpeedBonus)，实际轨迹与预告线一致
                float speed = (horseman ? HorsemanChargeSpeedByTier : HoundChargeSpeedByTier)[boundTier - 1];
                Vector2 dir = lockDir.ToRotationVector2();
                dashVX = dir.X * (speed / MoveGain(npc));
                //起手帧：纵向承诺分量一次注入（此后交给重力），横向给初始推力衔接包络首帧（避免空窗）
                npc.velocity = new Vector2(dashVX * ChargeLaunchPush, dir.Y * (speed / MoveGain(npc)));
                npc.netUpdate = true;
                timer = horseman ? PmkChargeOmen.HorsemanStrikeFrames : PmkChargeOmen.HoundStrikeFrames;
                emberDropsLeft = horseman ? HorsemanTrailEmbers : 0;
                emberDropTick = 6;
                phase = PhaseStrike;
            }
        }

        private void TickChargeStrike(NPC npc) {
            timer--;
            int strike = IsHorseman(npc) ? PmkChargeOmen.HorsemanStrikeFrames : PmkChargeOmen.HoundStrikeFrames;
            //包络塑形抵住原版横向衰减：爬升→保持→力竭（末帧包络归零=清残速），服务端持有低频同步
            npc.velocity.X = dashVX * MobDash.Envelope(strike - timer, ChargeRiseFrames,
                strike - ChargeRiseFrames - ChargeDecayFrames, ChargeDecayFrames);
            if (timer % 6 == 0) {
                npc.netUpdate = true;
            }
            //撞墙即收势（冲锋撞空=反制有效）
            if (npc.collideX) {
                timer = Math.Min(timer, 3);
            }
            //无头骑士：冲锋沿途撒火种（火种自带 ≥30 帧引燃预告）
            if (emberDropsLeft > 0 && --emberDropTick <= 0) {
                emberDropTick = HorsemanTrailInterval;
                if (TrySpawnEmber(npc, npc.Bottom, 2)) {
                    emberDropsLeft--;
                }
            }
            if (timer <= 0) {
                phase = PhaseIdle;
                omenIndex = -1;
                cooldown = ChargeCooldownByTier[boundTier - 1] + Main.rand.Next(CooldownJitter + 1);
            }
        }

        public override void OnHitPlayer(NPC npc, Player target, Player.HurtInfo hurtInfo) {
            if (boundTier <= 0 || family != PmkFamily.Charge) {
                return;
            }
            //命中方本机结算，减益原生同步；冲锋窗由已同步的预告实体判定（不读权威端私产计时器），
            //雕像怪不经机制入口、无从获得预告实体，天然被排除
            if (PmkChargeOmen.IsStrikeWindowFor(npc.whoAmI, npc.type)) {
                target.AddBuff(BuffID.OnFire, ChargeBurnTicks);
            }
        }
        #endregion

        #region 火种祭圈
        private void RitualStep(NPC npc) {
            if (phase == PhaseBusy) {
                if (--timer <= 0) {
                    phase = PhaseIdle;
                    cooldown = RitualCooldownByTier[boundTier - 1] + Main.rand.Next(CooldownJitter + 1);
                }
                return;
            }
            if (--cooldown > 0) {
                return;
            }
            if (!Eligible(npc)) {
                cooldown = IneligibleDelay;
                return;
            }
            if (!npc.HasValidTarget) {
                cooldown = RetryDelay;
                return;
            }
            Player player = Main.player[npc.target];
            if (!player.Alives()) {
                cooldown = RetryDelay;
                return;
            }
            float dist = npc.Distance(player.Center);
            if (dist < RitualMinRange || dist > RitualMaxRange) {
                cooldown = RetryDelay;
                return;
            }
            int wispType = ModContent.ProjectileType<PmkRitualWispProj>();
            //并发闸：一环在场不叠环
            if (CountActive(wispType) > 0) {
                cooldown = RetryDelay;
                return;
            }
            //圈心在施法瞬间锁定于世界坐标（预告即承诺，不追玩家）
            Vector2 center = player.Center;
            int damage = Math.Max(1, (int)(npc.damage * RitualDamageFrac));
            for (int slot = 0; slot < PmkRitualWispProj.RingSlots; slot++) {
                if (slot < PmkRitualWispProj.RingGapSlots) {
                    //具名缺口：连续槽位从不生成=物理缺口，随公转成为旋转安全扇区
                    continue;
                }
                Projectile.NewProjectile(npc.GetSource_FromAI(), center, Vector2.Zero, wispType,
                    damage, 0f, Main.myPlayer, center.X, center.Y, boundTier * 100 + slot);
            }
            phase = PhaseBusy;
            timer = PmkRitualWispProj.TotalFrames(boundTier) + 10;
        }
        #endregion

        #region 哀木签名技
        private void TreantStep(NPC npc) {
            if (phase == PhaseBusy) {
                if (--timer <= 0) {
                    phase = PhaseIdle;
                    SetMinibossCooldown(TreantCooldownByTier);
                }
                return;
            }
            if (--cooldown > 0) {
                return;
            }
            if (!MinibossReady(npc, out Player target)) {
                cooldown = RetryDelay * 2;
                return;
            }
            moveToggle = !moveToggle;
            bool started = moveToggle ? TryMortarBattery(npc, target) : TryRootWall(npc, target);
            if (!started) {
                started = moveToggle ? TryRootWall(npc, target) : TryMortarBattery(npc, target);
            }
            if (!started) {
                cooldown = 45;
            }
        }

        /// <summary>祭火炮排：少量大落点。每个落点先落标记（预告即实体，伤害载体），
        /// 炮弹为定帧弹道纯视觉，恰在标记引爆帧抵达；落点间距 ≥ MortarSpacing-2×抖动，走廊=安全带</summary>
        private bool TryMortarBattery(NPC npc, Player target) {
            float dist = npc.Distance(target.Center);
            if (dist < MortarMinRange || dist > MortarMaxRange) {
                return false;
            }
            int count = MortarCountByTier[boundTier - 1];
            int damage = Math.Max(1, (int)(npc.damage * MortarDamageFrac));
            int flight = PmkMortarShellProj.FlightFrames;
            Vector2 muzzle = npc.Top + new Vector2(0f, -26f);
            int spawned = 0;
            for (int i = 0; i < count; i++) {
                float offX = (i - (count - 1) * 0.5f) * MortarSpacing
                    + Main.rand.NextFloat(-MortarJitter, MortarJitter);
                float x = target.Center.X + offX;
                float groundY = FindSurfaceY(x, target.Center.Y, MortarGroundScanTiles);
                if (groundY < 0f) {
                    continue;
                }
                Vector2 impact = new Vector2(x, groundY - 8f);
                int marker = Projectile.NewProjectile(npc.GetSource_FromAI(), impact, Vector2.Zero,
                    ModContent.ProjectileType<PmkMortarMarkerProj>(), damage, 0f, Main.myPlayer, flight);
                if (marker < 0 || marker >= Main.maxProjectiles) {
                    continue;
                }
                //定帧弹道解算（每帧先加速后位移的精确离散解），落点即承诺
                Vector2 v0 = new Vector2((impact.X - muzzle.X) / flight,
                    (impact.Y - muzzle.Y) / flight - PmkMortarShellProj.ShellGravity * (flight + 1) * 0.5f);
                Projectile.NewProjectile(npc.GetSource_FromAI(), muzzle, v0,
                    ModContent.ProjectileType<PmkMortarShellProj>(), 0, 0f, Main.myPlayer, flight);
                spawned++;
            }
            if (spawned == 0) {
                return false;
            }
            phase = PhaseBusy;
            timer = flight + PmkMortarMarkerProj.BlastFrames + PmkMortarMarkerProj.FadeFrames + 10;
            return true;
        }

        /// <summary>根须墙：以目标脚下地表为基线的一排根须，缺口槽位从不生成（缺口无裂隙标记=可读安全门），
        /// 缺口不贴边保证墙沿可读；预告期击杀哀木可取消未破土的根须</summary>
        private bool TryRootWall(NPC npc, Player target) {
            if (npc.Distance(target.Center) > RootMaxRange) {
                return false;
            }
            float centerGroundY = FindSurfaceY(target.Center.X, target.Center.Y, RootGroundScanTiles);
            if (centerGroundY < 0f) {
                //目标悬空太高，根须够不着
                return false;
            }
            int damage = Math.Max(1, (int)(npc.damage * RootDamageFrac));
            int gapStart = 1 + Main.rand.Next(RootWallSlots - RootGapSlots - 1);
            int hold = RootHoldByTier[boundTier - 1];
            int spawned = 0;
            for (int slot = 0; slot < RootWallSlots; slot++) {
                if (slot >= gapStart && slot < gapStart + RootGapSlots) {
                    //具名缺口：布点循环真正跳过
                    continue;
                }
                float x = target.Center.X + (slot - (RootWallSlots - 1) * 0.5f) * RootSlotSpacing;
                float groundY = FindSurfaceY(x, centerGroundY - 32f, RootGroundScanTiles);
                if (groundY < 0f) {
                    continue;
                }
                Projectile.NewProjectile(npc.GetSource_FromAI(), new Vector2(x, groundY - 6f), Vector2.Zero,
                    ModContent.ProjectileType<PmkRootSpikeProj>(), damage, 0f, Main.myPlayer,
                    PmkRootSpikeProj.TelegraphFrames, npc.whoAmI + 1, hold);
                spawned++;
            }
            if (spawned == 0) {
                return false;
            }
            phase = PhaseBusy;
            timer = PmkRootSpikeProj.TelegraphFrames + PmkRootSpikeProj.EruptFrames + hold
                + PmkRootSpikeProj.RetractFrames + 10;
            return true;
        }
        #endregion

        #region 南瓜王签名技
        private void PumpkingStep(NPC npc) {
            if (phase == PhaseBusy) {
                if (--timer <= 0) {
                    phase = PhaseIdle;
                    SetMinibossCooldown(PumpkingCooldownByTier);
                }
                return;
            }
            if (--cooldown > 0) {
                return;
            }
            if (!MinibossReady(npc, out Player target)) {
                cooldown = RetryDelay * 2;
                return;
            }
            moveToggle = !moveToggle;
            bool started = moveToggle ? TryScytheWheel(npc) : TryClawCross(npc, target);
            if (!started) {
                started = moveToggle ? TryClawCross(npc, target) : TryScytheWheel(npc);
            }
            if (!started) {
                cooldown = 45;
            }
        }

        /// <summary>镰刃轮盘：锚定本体的公转镰刃环，缺口方位由权威端一次掷定经 ai 同步，
        /// 预告期缺口方位有安全辉光；机制零速度注入，与 boss 旗标无关</summary>
        private bool TryScytheWheel(NPC npc) {
            int bladeType = ModContent.ProjectileType<PmkScytheBladeProj>();
            //同刻至多一轮盘（双王在场也不叠）
            if (CountActive(bladeType) > 0) {
                return false;
            }
            int damage = Math.Max(1, (int)(npc.damage * WheelDamageFrac));
            float baseAngle = Main.rand.NextFloat(MathHelper.TwoPi);
            for (int slot = 0; slot < PmkScytheBladeProj.WheelSlots; slot++) {
                if (slot < PmkScytheBladeProj.WheelGapSlots) {
                    //具名缺口：连续槽位从不生成，随轮盘匀速公转
                    continue;
                }
                Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, Vector2.Zero, bladeType,
                    damage, 0f, Main.myPlayer, npc.whoAmI, boundTier * 100 + slot, baseAngle);
            }
            phase = PhaseBusy;
            timer = PmkScytheBladeProj.TotalFrames + 10;
            return true;
        }

        /// <summary>爪击十字：追踪→锁定→四臂爪现。锁定后坐标冻结（预告即承诺），对角象限恒为安全区</summary>
        private bool TryClawCross(NPC npc, Player target) {
            if (npc.Distance(target.Center) > CrossMaxRange) {
                return false;
            }
            int crossType = ModContent.ProjectileType<PmkClawCrossProj>();
            if (CountActive(crossType) >= 2) {
                return false;
            }
            int damage = Math.Max(1, (int)(npc.damage * CrossDamageFrac));
            int index = Projectile.NewProjectile(npc.GetSource_FromAI(), target.Center, Vector2.Zero,
                crossType, damage, 0f, Main.myPlayer, npc.whoAmI, 0f, 0f);
            if (index < 0 || index >= Main.maxProjectiles) {
                return false;
            }
            phase = PhaseBusy;
            timer = PmkClawCrossProj.TotalFrames + 10;
            return true;
        }
        #endregion
    }
}
