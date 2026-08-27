using CalamityOverhaul.Content.GameModes.BrutalMobs.DungeonDeep.Projectiles;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.DungeonDeep
{
    /// <summary>
    /// 深层地牢「亡骨军仪」行为层主表：地牢是一支有纪律的亡灵军队，讲队列、军械与仪式感。
    /// 怒骨近战族军列冲锋（同族并发 ≤2 + 静态节拍错拍）、法师族吟唱齐射（法阵预告实体）、
    /// 诅咒颅族咒锁俯冲、军官层（Paladin 锤震地 + 举盾格挡 / BoneLee 二连段）、射手族三型三态。
    /// 只叠加行为不动数值（数值层归 <see cref="GameModeNPC"/>），原版 AI 全程继续跑；
    /// 决策全在权威端（客户端 PostAI 早退），客户端可见状态一律来自已同步的预告弹幕实体。
    /// —— 分工声明（地牢栖息但不入本表的类型）——
    /// Skeleton / HeadacheSkeleton / MisassembledSkeleton / PantlessSkeleton / UndeadMiner：NightPack 已覆盖（抛物掷骨）；
    /// ArmoredSkeleton：EliteMove 已覆盖（格挡反击）；SkeletonArcher：EliteMove 已覆盖（具名缺口散射）；
    /// DungeonSpirit：豁免——稀有掉落怪，不加压，只吃数值层
    /// </summary>
    internal partial class DungeonDeepNPC : GlobalNPC
    {
        public override bool InstancePerEntity => true;

        //==== 通用节奏 ====
        /// <summary>触发条件未满足的复查间隔</summary>
        private const int RetryDelay = 30;
        /// <summary>资格不符（雕像怪等）的复查间隔</summary>
        private const int IneligibleDelay = 120;
        /// <summary>出生首攻错拍窗（M7 密度预算：60~180 帧，遭遇 ≤3 秒可见首个机制）</summary>
        private const int FirstCooldownMin = 60;
        private const int FirstCooldownMax = 180;
        /// <summary>各族冷却的公共随机抖动上限</summary>
        private const int CooldownJitter = 60;

        //==== 军仪光环 / 举盾格挡（承伤门常量） ====
        /// <summary>死灵法师吟唱光环内三系甲骨的承伤保留系数（10% 减伤）</summary>
        private const float AuraKeep = 0.9f;
        /// <summary>Paladin 举盾期间承伤保留系数</summary>
        private const float GuardKeep = 0.6f;
        /// <summary>Paladin 受击触发举盾的概率</summary>
        private const float BlockChance = 0.30f;
        /// <summary>举盾姿态持续帧</summary>
        private const int GuardStanceFrames = 90;
        /// <summary>举盾内置冷却</summary>
        private const int BlockCooldownFrames = 240;
        /// <summary>举盾姿态实体全局并发上限</summary>
        private const int GuardCap = 6;
        /// <summary>狱甲系冲锋命中的点燃时长（2 秒，不随档位增长）</summary>
        private const int HellOnFireTicks = 120;

        /// <summary>行为家族</summary>
        private enum DdFamily : byte
        {
            None,
            /// <summary>怒骨近战族：军列冲锋</summary>
            Charge,
            /// <summary>法师族：吟唱齐射</summary>
            Caster,
            /// <summary>诅咒颅族：咒锁俯冲</summary>
            Skull,
            /// <summary>军官：锤震地 + 举盾格挡</summary>
            Paladin,
            /// <summary>军官：压身突进拳 + 回旋踢二连段</summary>
            BoneLee,
            /// <summary>射手：长标线单发</summary>
            Sniper,
            /// <summary>射手：短扇面三连</summary>
            Tactical,
            /// <summary>射手：火箭抛物</summary>
            Commando,
        }

        //==== 相位 ====
        private const byte PhaseIdle = 0;
        private const byte PhaseWindup = 1;
        private const byte PhaseStrike = 2;
        private const byte PhaseRecover = 3;
        /// <summary>BoneLee 两段之间的顿帧</summary>
        private const byte PhasePause = 4;
        /// <summary>BoneLee 回旋踢二段</summary>
        private const byte PhaseStrike2 = 5;

        /// <summary>
        /// 怒骨冲锋参数行：系旗味 + 前摇/包络/力竭帧 + 蓝系横扫推力。
        /// 系级签名：蓝系收尾横扫小击退 / 锈系更快但力竭更长 / 狱系带火尘且命中点燃；
        /// 系内每型至少一项参数差异可被玩家叫出（前摇更长、峰值保持更久、力竭更短等）
        /// </summary>
        internal readonly struct DdChargeRow(byte flavor, int windup, float peak, int rise, int hold, int decay, int recover, float sweepPush)
        {
            /// <summary>系：0 怒骨 / 1 蓝甲 / 2 锈甲 / 3 狱甲</summary>
            public readonly byte Flavor = flavor;
            /// <summary>跺骨前摇帧（契约 ≥24，档位不缩短）</summary>
            public readonly int Windup = windup;
            /// <summary>名义峰速（未含模式提速补偿）</summary>
            public readonly float Peak = peak;
            public readonly int Rise = rise;
            public readonly int Hold = hold;
            public readonly int Decay = decay;
            /// <summary>力竭收势帧（锈系显著更长=惩罚窗更大）</summary>
            public readonly int Recover = recover;
            /// <summary>蓝系收尾横扫的击退推力（其余系为 0）</summary>
            public readonly float SweepPush = sweepPush;
            public int StrikeFrames => Rise + Hold + Decay;
        }

        /// <summary>法师齐射参数行（模式语义见 <see cref="DdCastOmen"/>）</summary>
        internal readonly struct DdCastRow(byte mode, float auxA, float auxB, float speedBonus)
        {
            /// <summary>0 三连水矢 / 1 双发缓追咒焰 / 2 影束+军仪光环 / 3 地狱火柱</summary>
            public readonly byte Mode = mode;
            /// <summary>咒焰=追踪帧数；火柱=半宽（像素）</summary>
            public readonly float AuxA = auxA;
            /// <summary>咒焰=每帧限转弧度；火柱=柱高（像素）</summary>
            public readonly float AuxB = auxB;
            /// <summary>弹速加成（型差）</summary>
            public readonly float SpeedBonus = speedBonus;
        }

        /// <summary>怒骨近战族 16 型：军列冲锋参数（每型至少一项可叫出的差异）</summary>
        internal static readonly Dictionary<int, DdChargeRow> ChargeRows = new() {
            //怒骨系：标准列兵 / 重装起手久冲更猛 / 峰值保持更长 / 起步快峰速低
            [NPCID.AngryBones] = new(DdChargeOmen.FlavorAngry, 24, 8.2f, 8, 14, 12, 18, 0f),
            [NPCID.AngryBonesBig] = new(DdChargeOmen.FlavorAngry, 30, 9.0f, 10, 14, 14, 22, 0f),
            [NPCID.AngryBonesBigMuscle] = new(DdChargeOmen.FlavorAngry, 26, 8.6f, 8, 20, 12, 20, 0f),
            [NPCID.AngryBonesBigHelmet] = new(DdChargeOmen.FlavorAngry, 24, 7.6f, 6, 12, 12, 16, 0f),
            //蓝甲系：收尾横扫小击退（推力=型差：链锤更重 / 无裤更快收势短 / 剑型突进保持长）
            [NPCID.BlueArmoredBones] = new(DdChargeOmen.FlavorBlue, 26, 9.0f, 8, 14, 12, 20, 5.5f),
            [NPCID.BlueArmoredBonesMace] = new(DdChargeOmen.FlavorBlue, 28, 8.8f, 8, 14, 12, 22, 7.5f),
            [NPCID.BlueArmoredBonesNoPants] = new(DdChargeOmen.FlavorBlue, 24, 9.4f, 7, 12, 12, 16, 5.0f),
            [NPCID.BlueArmoredBonesSword] = new(DdChargeOmen.FlavorBlue, 26, 9.0f, 8, 18, 12, 20, 5.5f),
            //锈甲系：冲锋更快但力竭更长（无甲剑型最轻装最快）
            [NPCID.RustyArmoredBonesAxe] = new(DdChargeOmen.FlavorRusty, 26, 10.4f, 7, 12, 12, 30, 0f),
            [NPCID.RustyArmoredBonesFlail] = new(DdChargeOmen.FlavorRusty, 28, 10.0f, 8, 16, 12, 32, 0f),
            [NPCID.RustyArmoredBonesSword] = new(DdChargeOmen.FlavorRusty, 26, 10.8f, 6, 12, 12, 30, 0f),
            [NPCID.RustyArmoredBonesSwordNoArmor] = new(DdChargeOmen.FlavorRusty, 24, 11.2f, 6, 10, 12, 26, 0f),
            //狱甲系：躯干带火尘、命中点燃（尖盾型蓄势更久 / 链锤保持长 / 剑型起步快）
            [NPCID.HellArmoredBones] = new(DdChargeOmen.FlavorHell, 28, 9.4f, 8, 14, 12, 22, 0f),
            [NPCID.HellArmoredBonesSpikeShield] = new(DdChargeOmen.FlavorHell, 30, 9.0f, 8, 16, 12, 22, 0f),
            [NPCID.HellArmoredBonesMace] = new(DdChargeOmen.FlavorHell, 28, 9.0f, 8, 18, 12, 24, 0f),
            [NPCID.HellArmoredBonesSword] = new(DdChargeOmen.FlavorHell, 26, 9.8f, 6, 14, 12, 22, 0f),
        };

        /// <summary>法师族 7 型：吟唱齐射参数（型差=追踪帧/弹速/柱形）</summary>
        internal static readonly Dictionary<int, DdCastRow> CastRows = new() {
            [NPCID.DarkCaster] = new(DdCastOmen.ModeWater, 0f, 0f, 0f),
            //破袍系：双发缓追咒焰（敞衣版追踪更短但飞得更急）
            [NPCID.RaggedCaster] = new(DdCastOmen.ModeCursed, 60f, 0.030f, 0f),
            [NPCID.RaggedCasterOpenCoat] = new(DdCastOmen.ModeCursed, 50f, 0.030f, 0.9f),
            //死灵系：影束单发 + 吟唱期军仪光环（甲版影束更快）
            [NPCID.Necromancer] = new(DdCastOmen.ModeShadow, 0f, 0f, 0f),
            [NPCID.NecromancerArmored] = new(DdCastOmen.ModeShadow, 0f, 0f, 2.0f),
            //恶魔学者系：目标脚下地狱火柱（红袍宽矮 / 白袍窄高）
            [NPCID.DiabolistRed] = new(DdCastOmen.ModePillar, 46f, 240f, 0f),
            [NPCID.DiabolistWhite] = new(DdCastOmen.ModePillar, 34f, 310f, 0f),
        };

        internal static bool TryGetCastRow(int npcType, out DdCastRow row) => CastRows.TryGetValue(npcType, out row);

        private static DdFamily ResolveFamily(int type) {
            if (ChargeRows.ContainsKey(type)) {
                return DdFamily.Charge;
            }
            if (CastRows.ContainsKey(type)) {
                return DdFamily.Caster;
            }
            return type switch {
                NPCID.CursedSkull or NPCID.GiantCursedSkull => DdFamily.Skull,
                NPCID.Paladin => DdFamily.Paladin,
                NPCID.BoneLee => DdFamily.BoneLee,
                NPCID.SkeletonSniper => DdFamily.Sniper,
                NPCID.TacticalSkeleton => DdFamily.Tactical,
                NPCID.SkeletonCommando => DdFamily.Commando,
                _ => DdFamily.None,
            };
        }

        //==== 权威端决策私产（客户端不读） ====
        /// <summary>出生时绑定的档位，0=未绑定（中途切模式不影响已出生个体）</summary>
        private int boundTier;
        private DdFamily family;
        private byte phase;
        private int timer;
        private int cooldown;
        /// <summary>锁定方向（弧度；冲锋族只取 0/π 水平向）。锁定后不再改写（预告即承诺）</summary>
        private float lockDir;
        /// <summary>锁定点（吟唱起点 / 火箭落点）</summary>
        private Vector2 lockPoint;
        /// <summary>本次动作的主预告实体槽位</summary>
        private int omenIndex = -1;
        /// <summary>副实体槽位（Diabolist 的火柱）</summary>
        private int auxIndex = -1;
        /// <summary>Paladin 举盾冷却</summary>
        private int blockCooldown;
        /// <summary>Paladin 受击侦测的生命值追踪（打击包只改生命值，掉血=本帧被打）</summary>
        private int lifeTracker;

        /// <summary>冲锋军列的静态节拍基准（权威端私产，进出世界清零）</summary>
        private static uint lastChargeBeat;

        /// <summary>世界清理：GameUpdateCount 每次进世界归零，残留节拍会伪装成未到拍，须清零</summary>
        internal static void ResetStaticBeats() => lastChargeBeat = 0;

        public override bool AppliesToEntity(NPC entity, bool lateInstantiation)
            => lateInstantiation && ResolveFamily(entity.type) != DdFamily.None;

        public override void SetDefaults(NPC npc) {
            boundTier = 0;
            family = DdFamily.None;
            int tier = GameModeSystem.EffectiveTier;
            if (tier <= 0) {
                return;
            }
            DdFamily resolved = ResolveFamily(npc.type);
            if (resolved == DdFamily.None) {
                return;
            }
            family = resolved;
            boundTier = tier;
            //首攻错拍：此刻 npc.whoAmI 恒为 0（NewNPC 之后才赋值），不可用作错拍源；
            //冷却是权威端决策私产，Main.rand 无同步语义
            cooldown = FirstCooldownMin + Main.rand.Next(FirstCooldownMax - FirstCooldownMin + 1);
        }

        /// <summary>机制入口资格：友方/无敌/Boss/小动物载体/雕像怪/共享血池体节逐项排除（每个入口都要过）</summary>
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

        private static bool CanSee(NPC npc, Player player)
            => Collision.CanHit(npc.position, npc.width, npc.height, player.position, player.width, player.height);

        /// <summary>统计某类弹幕的活动实例数（到 stopAt 提前退出；只在冷却尽头调用，非每帧）</summary>
        internal static int CountActive(int projType, int stopAt = 32) {
            int count = 0;
            foreach (Projectile proj in Main.ActiveProjectiles) {
                if (proj.type == projType && ++count >= stopAt) {
                    break;
                }
            }
            return count;
        }

        /// <summary>来源打包：低 8 位=槽位+1，高位=类型（槽位复用时校验不被骗过）</summary>
        internal static int PackSource(NPC npc) => (npc.whoAmI + 1) | (npc.type << 8);

        /// <summary>
        /// 校验自己名下的预告实体仍然有效（索引+类型+来源打包三重校验，防槽位复用）。
        /// sourceSlot 指定来源打包所在的 ai 槽位。实体缺位=回冷却（失败方向=安全方向）
        /// </summary>
        private static bool TryGetBoundOmen(int index, int projType, NPC npc, int sourceSlot, out Projectile proj) {
            proj = null;
            if (index < 0 || index >= Main.maxProjectiles) {
                return false;
            }
            Projectile p = Main.projectile[index];
            if (!p.active || p.type != projType) {
                return false;
            }
            int packed = (int)p.ai[sourceSlot];
            if ((packed & 255) != npc.whoAmI + 1 || packed >> 8 != npc.type) {
                return false;
            }
            proj = p;
            return true;
        }

        /// <summary>从目标脚下向下找可站立地表，返回柱底/环心锚点（找不到视为悬空）</summary>
        private static bool TryFindGround(Player target, out Vector2 basePos) {
            basePos = default;
            Point feet = target.Bottom.ToTileCoordinates();
            for (int dy = 0; dy < 12; dy++) {
                int tileY = feet.Y + dy;
                if (!WorldGen.InWorld(feet.X, tileY, 10)) {
                    return false;
                }
                if (WorldGen.SolidTile(feet.X, tileY)) {
                    basePos = new Vector2(feet.X * 16f + 8f, tileY * 16f);
                    return true;
                }
            }
            return false;
        }

        /// <summary>模式提速补偿：承诺性速度全部除回该系数（位移项除、重力项不除）</summary>
        private float MoveGain => 1f + GameModeTuning.SpeedBonus(boundTier);

        /// <summary>动作作废回退：清空实体绑定、退回待机</summary>
        private void AbortToCooldown(int cd) {
            phase = PhaseIdle;
            omenIndex = -1;
            auxIndex = -1;
            timer = 0;
            cooldown = cd;
        }

        public override void PostAI(NPC npc) {
            if (boundTier <= 0) {
                return;
            }
            if (VaultUtils.isClient) {
                //决策只在权威端；客户端画面全部来自同步的预告实体与 NPC 速度原生同步
                return;
            }
            if (lifeTracker <= 0) {
                lifeTracker = npc.life;
            }
            if (family == DdFamily.Paladin) {
                TickGuardWatch(npc);
            }
            if (phase == PhaseIdle) {
                if (--cooldown > 0) {
                    return;
                }
                TryStart(npc);
                return;
            }
            switch (family) {
                case DdFamily.Charge:
                    TickCharge(npc);
                    break;
                case DdFamily.Caster:
                    TickCaster(npc);
                    break;
                case DdFamily.Skull:
                    TickSkull(npc);
                    break;
                case DdFamily.Paladin:
                    TickHammer(npc);
                    break;
                case DdFamily.BoneLee:
                    TickBoneLee(npc);
                    break;
                case DdFamily.Sniper:
                case DdFamily.Tactical:
                    TickMarksman(npc);
                    break;
                case DdFamily.Commando:
                    TickRocketRecover(npc);
                    break;
            }
        }

        /// <summary>取有效目标；失败时统一回短冷却</summary>
        private bool TryGetTarget(NPC npc, out Player player) {
            player = null;
            if (!Eligible(npc)) {
                cooldown = IneligibleDelay;
                return false;
            }
            if (npc.target < 0 || npc.target >= Main.maxPlayers) {
                cooldown = RetryDelay;
                return false;
            }
            player = Main.player[npc.target];
            if (!player.Alives()) {
                player = null;
                cooldown = RetryDelay;
                return false;
            }
            return true;
        }

        private void TryStart(NPC npc) {
            if (!TryGetTarget(npc, out Player player)) {
                return;
            }
            switch (family) {
                case DdFamily.Charge:
                    TryStartCharge(npc, player);
                    break;
                case DdFamily.Caster:
                    TryStartCaster(npc, player);
                    break;
                case DdFamily.Skull:
                    TryStartSkull(npc, player);
                    break;
                case DdFamily.Paladin:
                    TryStartHammer(npc, player);
                    break;
                case DdFamily.BoneLee:
                    TryStartBoneLee(npc, player);
                    break;
                case DdFamily.Sniper:
                case DdFamily.Tactical:
                    TryStartMarksman(npc, player);
                    break;
                case DdFamily.Commando:
                    TryStartRocket(npc, player);
                    break;
            }
        }

        #region 命中门与减益（读同步实体，各端一致）
        public override void ModifyHitByItem(NPC npc, Player player, Item item, ref NPC.HitModifiers modifiers)
            => ApplyDefenseGates(npc, ref modifiers);

        public override void ModifyHitByProjectile(NPC npc, Projectile projectile, ref NPC.HitModifiers modifiers)
            => ApplyDefenseGates(npc, ref modifiers);

        /// <summary>承伤门：Paladin 举盾 ×0.6；军仪光环覆盖内的三系甲骨 ×0.9。门只读同步实体，不读权威私产</summary>
        private void ApplyDefenseGates(NPC npc, ref NPC.HitModifiers modifiers) {
            if (boundTier <= 0) {
                return;
            }
            if (family == DdFamily.Paladin) {
                if (DdGuardStanceProj.GuardActiveFor(npc.whoAmI)) {
                    modifiers.FinalDamage *= GuardKeep;
                }
                return;
            }
            if (family == DdFamily.Charge
                && ChargeRows.TryGetValue(npc.type, out DdChargeRow row)
                && row.Flavor >= DdChargeOmen.FlavorBlue
                && DdCastOmen.ShadowAuraCovers(npc.Center)) {
                modifiers.FinalDamage *= AuraKeep;
            }
        }

        public override void OnHitPlayer(NPC npc, Player target, Player.HurtInfo hurtInfo) {
            if (boundTier <= 0 || family != DdFamily.Charge) {
                return;
            }
            if (!ChargeRows.TryGetValue(npc.type, out DdChargeRow row)) {
                return;
            }
            //受击方本机结算（原生同步）；攻击窗由已同步的冲锋预告实体判定，不读权威端私产计时器
            if (row.Flavor == DdChargeOmen.FlavorHell && DdChargeOmen.IsStrikeWindowFor(npc.whoAmI)) {
                target.AddBuff(BuffID.OnFire, HellOnFireTicks);
            }
            else if (row.Flavor == DdChargeOmen.FlavorBlue && row.SweepPush > 0f
                && DdChargeOmen.IsSweepWindowFor(npc.whoAmI)) {
                //蓝甲系收尾横扫：小击退（受击者本机把速度顶出去，随原生玩家同步收尾）
                float sign = target.Center.X >= npc.Center.X ? 1f : -1f;
                target.velocity.X += sign * row.SweepPush;
                target.velocity.Y -= 1.6f;
            }
        }
        #endregion
    }

    /// <summary>世界清理钩子：冲锋节拍是世界级 static，进出世界统一清零（服务端与单人都会走到）</summary>
    internal class DungeonDeepWorldReset : ModSystem
    {
        public override void ClearWorld() => DungeonDeepNPC.ResetStaticBeats();
    }
}
