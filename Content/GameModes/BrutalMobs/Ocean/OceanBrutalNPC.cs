using CalamityOverhaul.Content.GameModes.BrutalMobs.Common;
using CalamityOverhaul.Content.GameModes.BrutalMobs.Ocean.Projectiles;
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.Ocean
{
    /// <summary>
    /// 残酷模式海洋组行为机制层，主题：潮汐猎场（掠食者利用水体的伏击与围猎）。
    /// 叠加在原版 AI 之上，不接管：鲨鱼掠食冲刺/破水跃咬（贴身潜行+水面泡沫痕预告）、
    /// 水母电场脉冲（专用环实体，可见环=判定环，三型差异走 ByType 表：半径/充能/拍数）、
    /// 鱿鱼墨幕脱身（受击或被逼近时反向喷射+滞留墨云纯遮视）、螃蟹掘沙伏击（沙堆实体半埋+破土钳击）与近战蓄力钳击。
    /// 覆盖名单：Shark / BlueJellyfish / PinkJellyfish / GreenJellyfish / Squid / Crab。
    /// 豁免名单：SeaSnail——移动力过低，主动招式机制不适配（前摇→突进的动作语言在它身上无法成立），
    /// 只吃 GameModeNPC 的数值层，特此声明豁免。
    /// 蓝/绿水母栖于洞穴水体，与海洋粉水母同族共用电场装备（同族差异见 <see cref="OceanPulseRing"/> ByType 表，非换皮）；
    /// 原版水母受击反电机制保留不动（本层只加行为，不碰原版逻辑）。
    /// 决策与生成只在权威端跑（客户端 PostAI 早退），客户端可见状态一律来自同步弹幕实体与 NPC 速度原生同步；
    /// 数值增强由 GameModeNPC 统一负责，此处只加行为。本批不做氛围联动（Tidecall 联动后续统一接）
    /// </summary>
    internal class OceanBrutalNPC : GlobalNPC
    {
        //==== 通用节奏 ====
        /// <summary>触发条件不满足时的复查间隔</summary>
        private const int RetryDelay = 30;
        /// <summary>资格不符（雕像怪等）的复查间隔</summary>
        private const int IneligibleDelay = 120;
        /// <summary>出生首攻错拍窗（M7 密度预算：60~180 帧，遭遇 ≤3 秒可见首个机制）</summary>
        private const int FirstCooldownMin = 60;
        private const int FirstCooldownMax = 180;
        /// <summary>冷却随机抖动上限</summary>
        private const int CooldownJitter = 40;

        //==== 鲨鱼·掠食冲刺 / 破水跃咬（仅 npc.wet 时触发） ====
        private const float SharkMinRange = 130f;
        private const float SharkMaxRange = 560f;
        /// <summary>绕行侧翼帧数（缓移压速+泡沫尘，贴身预告实体同步计时）</summary>
        private const int SharkFlankFrames = 40;
        /// <summary>龇牙前摇帧数（锁向发生在本段起点，此后不再重瞄）</summary>
        private const int SharkSnarlFrames = 24;
        /// <summary>绕行导引速度（名义值，注入前除回 MoveGain）</summary>
        private const float SharkFlankSpeed = 3.6f;
        /// <summary>侧翼点离目标的横向距离</summary>
        private const float SharkFlankOffset = 150f;
        /// <summary>突进包络三段：爬升/保持/衰减</summary>
        private const int SharkDashRise = 5;
        private const int SharkDashHold = 9;
        private const int SharkDashDecay = 16;
        private const int SharkDashTotal = SharkDashRise + SharkDashHold + SharkDashDecay;
        /// <summary>突进名义峰速（档位只调强度；注入时除回 MoveGain）</summary>
        private static readonly float[] SharkDashPeakByTier = [10.5f, 11.5f, 12.5f];
        /// <summary>力竭漂移帧（突进后收势）</summary>
        private const int SharkDashRecover = 16;
        private static readonly int[] SharkDashCooldownByTier = [400, 360, 320];
        /// <summary>目标离水面多近算"水面附近"（改走破水跃咬）</summary>
        private const float BreachSurfaceBand = 96f;
        /// <summary>破水触发要求鲨鱼至少潜在水面下多深</summary>
        private const float BreachMinDepth = 60f;
        /// <summary>预备位：破浪点后方偏移与下潜深度</summary>
        private const float BreachStageBack = 60f;
        private const float BreachStageDepth = 80f;
        private const float BreachStageSpeed = 4.5f;
        /// <summary>跃咬滞空帧数（弧线时长）</summary>
        private const int BreachFlightFrames = 30;
        /// <summary>跃咬自持重力（原版鱼类离水行为离线不可查证，弧线由本层确定性持有）</summary>
        private const float BreachGravity = 0.3f;
        private const float BreachMaxVx = 12f;
        private const float BreachMaxUpVy = -13f;
        /// <summary>跃咬落水收势帧</summary>
        private const int BreachRecover = 20;
        /// <summary>跃咬相对冲刺的附加冷却（签名招更贵）</summary>
        private const int BreachExtraCooldown = 120;
        /// <summary>鲨鱼预告实体全局并发上限</summary>
        private const int SharkOmenCap = 6;

        //==== 水母·电场脉冲（环参数档见 OceanPulseRing 的 ByType 表） ====
        /// <summary>触发距离=判定半径+此余量（环外恒安全，触发本身不越环承诺）</summary>
        private const float PulseTriggerPad = 90f;
        /// <summary>脉冲伤害 = 已缩放 npc.damage × 此值</summary>
        private const float PulseDamageFrac = 0.55f;
        /// <summary>泄力下沉帧（拍完后的后摇）</summary>
        private const int JellySinkFrames = 20;
        /// <summary>下沉速度（缓沉，不注入位移承诺，无需补偿）</summary>
        private const float JellySinkSpeed = 1.4f;
        private static readonly int[] PulseCooldownByTier = [380, 340, 300];
        /// <summary>脉冲环全局并发上限</summary>
        private const int RingCap = 6;

        //==== 鱿鱼·墨幕脱身 ====
        /// <summary>被逼近触发距离（受击触发不受距离限制）</summary>
        private const float SquidPanicRange = 160f;
        /// <summary>短前摇帧数（脱身喷射无伤害、无预告债，12 帧压速即为可见起手）</summary>
        private const int SquidWindupFrames = 12;
        /// <summary>喷射包络三段</summary>
        private const int SquidJetRise = 4;
        private const int SquidJetHold = 6;
        private const int SquidJetDecay = 14;
        private const int SquidJetTotal = SquidJetRise + SquidJetHold + SquidJetDecay;
        /// <summary>喷射名义峰速（注入时除回 MoveGain）</summary>
        private const float SquidJetPeak = 10.5f;
        private const int SquidRecover = 12;
        /// <summary>墨云半径（档位只加强度；可见=判定同一值）</summary>
        private static readonly float[] InkRadiusByTier = [100f, 110f, 120f];
        /// <summary>墨云滞留帧</summary>
        private const int InkLingerFrames = 150;
        /// <summary>墨幕冷却（任务底线 ≥420）</summary>
        private static readonly int[] InkCooldownByTier = [480, 450, 420];
        /// <summary>墨云全局并发上限</summary>
        private const int InkCap = 6;

        //==== 螃蟹·掘沙伏击 / 近战蓄力钳击 ====
        /// <summary>玩家远于此距离且脚下是沙才入土半埋</summary>
        private const float CrabAmbushMinPlayerDist = 300f;
        /// <summary>玩家进到此距离触发破土</summary>
        private const float CrabBurstTriggerDist = 200f;
        /// <summary>破土钳击前摇帧（沙尘鼓包由沙堆实体状态位驱动，≥30 契约）</summary>
        private const int CrabBurstWindupFrames = 30;
        /// <summary>半埋潜伏时限（无人上钩则自行破土离场）</summary>
        private const int BuryMaxFrames = 600;
        /// <summary>空埋收场后的复查冷却</summary>
        private const int UnburyCooldown = 240;
        /// <summary>近战钳击蓄力帧（纯近身体术：≥24 帧压速定身即可见起手，M3 姿态前摇条款）</summary>
        private const int CrabClawWindupFrames = 24;
        private const float CrabClawMinRange = 40f;
        private const float CrabClawMaxRange = 150f;
        private const float CrabClawMaxDy = 80f;
        /// <summary>钳击突进包络三段</summary>
        private const int CrabLungeRise = 4;
        private const int CrabLungeHold = 6;
        private const int CrabLungeDecay = 12;
        private const int CrabLungeTotal = CrabLungeRise + CrabLungeHold + CrabLungeDecay;
        /// <summary>破土钳击名义峰速（注入时除回 MoveGain）</summary>
        private static readonly float[] CrabBurstPeakByTier = [8.0f, 8.6f, 9.2f];
        /// <summary>近战钳击名义峰速</summary>
        private static readonly float[] CrabClawPeakByTier = [7.0f, 7.6f, 8.2f];
        /// <summary>破土起跳竖向脉冲（位移承诺，除回 MoveGain）</summary>
        private const float CrabBurstPopVy = -3.4f;
        private const int CrabRecover = 14;
        private static readonly int[] CrabClawCooldownByTier = [320, 290, 260];
        /// <summary>伏击得手/收场后的再装填冷却</summary>
        private static readonly int[] CrabAmbushRearmByTier = [540, 500, 460];
        /// <summary>沙堆标记全局并发上限</summary>
        private const int MoundCap = 6;
        /// <summary>脚下寻沙的向下扫描瓦格数</summary>
        private const int SandSearchTiles = 3;
        /// <summary>水面扫描范围（向上/向下，瓦格）</summary>
        private const int SurfaceScanUp = 8;
        private const int SurfaceScanDown = 4;

        private enum OceanFamily : byte
        {
            None,
            /// <summary>鲨鱼：掠食冲刺/破水跃咬</summary>
            Shark,
            /// <summary>水母族：电场脉冲</summary>
            Jelly,
            /// <summary>鱿鱼：墨幕脱身</summary>
            Squid,
            /// <summary>螃蟹：掘沙伏击+蓄力钳击</summary>
            Crab,
        }

        private const byte PhaseIdle = 0;
        /// <summary>前摇甲段：鲨=绕行/预备位、水母=充能+拍窗定身、鱿=短前摇、蟹=半埋潜伏</summary>
        private const byte PhaseWindupA = 1;
        /// <summary>前摇乙段：鲨=龇牙、蟹=破土前摇/近战蓄力</summary>
        private const byte PhaseWindupB = 2;
        private const byte PhaseStrike = 3;
        private const byte PhaseRecover = 4;

        public override bool InstancePerEntity => true;

        /// <summary>本个体出生时绑定的档位，0=未绑定（镜像 GameModeNPC；中途切模式不影响已出生个体）</summary>
        private int boundTier;
        private OceanFamily family;
        private byte phase;
        private int timer;
        private int cooldown;
        /// <summary>0=常规招（鲨=侧翼冲刺 / 蟹=近战钳击），1=变招（鲨=破水跃咬 / 蟹=掘沙伏击）</summary>
        private byte moveVariant;
        /// <summary>锁定方向（锁定帧后不再改写，预告即承诺）</summary>
        private float lockDir;
        /// <summary>锁定咬点（破水跃咬，预告生成帧锁死）</summary>
        private Vector2 lockPoint;
        /// <summary>侧翼/突进横向符号（±1，起手锁定）</summary>
        private float flankSide = 1f;
        /// <summary>破浪点水面高度（像素，预告生成帧锁死）</summary>
        private float breachSurfaceY;
        /// <summary>跃咬期自持的弧线速度（服务端私产，靠 netUpdate 低频推给客户端）</summary>
        private float breachVx;
        private float breachVy;
        /// <summary>本次进攻的预告实体槽位（权威端私产，取用前必过 TryGetBoundOmen 校验）</summary>
        private int omenIndex = -1;
        /// <summary>受击检测的上一帧血量（权威端私产）</summary>
        private int lastLife;
        /// <summary>受击余波窗（帧）：受击落在冷却复查间隙也能触发脱身，窗短不留陈旧旗标</summary>
        private int hurtWindow;

        private static OceanFamily ResolveFamily(int type) => type switch {
            NPCID.Shark => OceanFamily.Shark,
            NPCID.BlueJellyfish or NPCID.PinkJellyfish or NPCID.GreenJellyfish => OceanFamily.Jelly,
            NPCID.Squid => OceanFamily.Squid,
            NPCID.Crab => OceanFamily.Crab,
            _ => OceanFamily.None,
        };

        private static int JellyVariant(int type) => type switch {
            NPCID.PinkJellyfish => OceanPulseRing.VariantPink,
            NPCID.GreenJellyfish => OceanPulseRing.VariantGreen,
            _ => OceanPulseRing.VariantBlue,
        };

        public override bool AppliesToEntity(NPC entity, bool lateInstantiation)
            => lateInstantiation && ResolveFamily(entity.type) != OceanFamily.None;

        public override void SetDefaults(NPC npc) {
            boundTier = 0;
            family = OceanFamily.None;
            int tier = GameModeSystem.EffectiveTier;
            if (tier <= 0) {
                return;
            }
            OceanFamily resolved = ResolveFamily(npc.type);
            if (resolved == OceanFamily.None) {
                return;
            }
            family = resolved;
            boundTier = tier;
            //首攻错拍：冷却是权威端决策私产（客户端副本不被读取），Main.rand 无同步语义；
            //此刻 npc.whoAmI 恒为 0（NewNPC 之后才赋值），不可用作错拍源
            cooldown = FirstCooldownMin + Main.rand.Next(FirstCooldownMax - FirstCooldownMin + 1);
        }

        /// <summary>机制入口资格：友方/无敌/Boss 旗标/雕像怪/共享血池体节逐项排除（每个入口都要过）</summary>
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
        /// 提速位移补偿：GameModeNPC.PostAI 对非 Boss 怪按 velocity×SpeedBonus 追加位置推进，
        /// 本层注入的承诺性速度一律除回该系数（位移项除、重力项不除），运行时读旗标
        /// </summary>
        private float MoveGain(NPC npc)
            => !npc.boss && npc.realLife < 0 ? 1f + GameModeTuning.SpeedBonus(boundTier) : 1f;

        /// <summary>同型弹幕并发计数（只在触发时调用，非每帧）</summary>
        private static int CountActive(int projType, int stopAt = 32) {
            int count = 0;
            foreach (Projectile proj in Main.ActiveProjectiles) {
                if (proj.type == projType && ++count >= stopAt) {
                    break;
                }
            }
            return count;
        }

        /// <summary>来源打包（与各预告实体 ai[0] 的取消检查同一口径）</summary>
        private static int PackSource(NPC npc) => (npc.whoAmI + 1) | (npc.type << 8);

        /// <summary>回读绑定的预告实体：索引+类型+来源三重校验；缺位=取消，失败方向=安全方向</summary>
        private bool TryGetBoundOmen(NPC npc, int projType, out Projectile omen) {
            omen = null;
            if (omenIndex < 0 || omenIndex >= Main.maxProjectiles) {
                return false;
            }
            Projectile candidate = Main.projectile[omenIndex];
            if (!candidate.active || candidate.type != projType || (int)candidate.ai[0] != PackSource(npc)) {
                return false;
            }
            omen = candidate;
            return true;
        }

        /// <summary>提前收场绑定实体（权威端 Kill 原生同步）</summary>
        private void KillBoundOmen(NPC npc, int projType) {
            if (TryGetBoundOmen(npc, projType, out Projectile omen)) {
                omen.Kill();
            }
            omenIndex = -1;
        }

        /// <summary>进攻中途失效（目标死亡/实体缺位）的统一回退：清相位回冷却</summary>
        private void Abort(NPC npc, int killProjType = -1) {
            if (killProjType >= 0) {
                KillBoundOmen(npc, killProjType);
            }
            omenIndex = -1;
            phase = PhaseIdle;
            cooldown = RetryDelay;
        }

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

        /// <summary>脚下是否沙地（向下少许扫描到第一块实心物块并验沙系）</summary>
        private static bool StandingOnSand(NPC npc) {
            Point feet = npc.Bottom.ToTileCoordinates();
            for (int dy = 0; dy <= SandSearchTiles; dy++) {
                int y = feet.Y + dy;
                if (!WorldGen.InWorld(feet.X, y, 10)) {
                    return false;
                }
                if (WorldGen.SolidTile(feet.X, y)) {
                    return Main.tileSand[Framing.GetTileSafely(feet.X, y).TileType];
                }
            }
            return false;
        }

        /// <summary>
        /// 在指定位置附近竖直扫描开放水面（上方无实心盖顶），返回水面像素高度。
        /// 液面像素换算镜像 Fleshfen 口径：y*16 + (255-LiquidAmount)/255*16
        /// </summary>
        private static bool TryFindWaterSurface(Vector2 around, out float surfaceY) {
            surfaceY = 0f;
            Point start = around.ToTileCoordinates();
            int waterY = -1;
            for (int dy = -SurfaceScanUp; dy <= SurfaceScanDown; dy++) {
                int y = start.Y + dy;
                if (!WorldGen.InWorld(start.X, y, 10)) {
                    return false;
                }
                Tile tile = Framing.GetTileSafely(start.X, y);
                if (tile.LiquidAmount > 32 && tile.LiquidType == LiquidID.Water && !WorldGen.SolidTile(start.X, y)) {
                    waterY = y;
                    break;
                }
            }
            if (waterY < 0) {
                return false;
            }
            int top = waterY;
            for (int i = 0; i < SurfaceScanUp + SurfaceScanDown; i++) {
                int y = top - 1;
                if (!WorldGen.InWorld(start.X, y, 10)) {
                    break;
                }
                Tile tile = Framing.GetTileSafely(start.X, y);
                if (tile.LiquidAmount > 32 && tile.LiquidType == LiquidID.Water) {
                    top = y;
                    continue;
                }
                break;
            }
            Tile above = Framing.GetTileSafely(start.X, top - 1);
            if (above.HasTile && Main.tileSolid[above.TileType] && !Main.tileSolidTop[above.TileType]) {
                return false;
            }
            Tile surf = Framing.GetTileSafely(start.X, top);
            surfaceY = top * 16f + (255 - surf.LiquidAmount) / 255f * 16f;
            return true;
        }

        public override void PostAI(NPC npc) {
            if (boundTier <= 0) {
                return;
            }
            if (VaultUtils.isClient) {
                //决策只在权威端；客户端画面全部来自同步弹幕实体与 NPC 原生同步
                return;
            }

            //受击检测（鱿鱼触发源）：权威端血量为准，逐帧对比；短余波窗兜住冷却复查间隙里的受击
            if (lastLife > 0 && npc.life < lastLife) {
                hurtWindow = 20;
            }
            else if (hurtWindow > 0) {
                hurtWindow--;
            }
            lastLife = npc.life;

            switch (phase) {
                case PhaseIdle:
                    if (--cooldown > 0) {
                        return;
                    }
                    TryStart(npc);
                    return;
                case PhaseWindupA:
                    TickWindupA(npc);
                    return;
                case PhaseWindupB:
                    TickWindupB(npc);
                    return;
                case PhaseStrike:
                    TickStrike(npc);
                    return;
                default:
                    TickRecover(npc);
                    return;
            }
        }

        private void TryStart(NPC npc) {
            if (!Eligible(npc)) {
                cooldown = IneligibleDelay;
                return;
            }
            if (!TryGetTarget(npc, out Player player)) {
                cooldown = RetryDelay;
                return;
            }
            switch (family) {
                case OceanFamily.Shark:
                    TryStartShark(npc, player);
                    return;
                case OceanFamily.Jelly:
                    TryStartJelly(npc, player);
                    return;
                case OceanFamily.Squid:
                    TryStartSquid(npc, player);
                    return;
                case OceanFamily.Crab:
                    TryStartCrab(npc, player);
                    return;
                default:
                    cooldown = IneligibleDelay;
                    return;
            }
        }

        //==================== 鲨鱼 ====================

        /// <summary>掠食冲刺/破水跃咬：仅 npc.wet 触发；目标在水面附近改跃咬，其余走侧翼冲刺</summary>
        private void TryStartShark(NPC npc, Player player) {
            cooldown = RetryDelay;
            if (!npc.wet) {
                return;
            }
            float dist = npc.Distance(player.Center);
            if (dist < SharkMinRange || dist > SharkMaxRange) {
                return;
            }
            if (CountActive(ModContent.ProjectileType<OceanSharkOmen>()) >= SharkOmenCap) {
                return;
            }

            bool nearSurface = TryFindWaterSurface(player.Center, out float surfaceY)
                && Math.Abs(surfaceY - player.Center.Y) <= BreachSurfaceBand;
            if (nearSurface && npc.Center.Y > surfaceY + BreachMinDepth) {
                //破水跃咬：咬点与破浪点在预告生成帧锁死（预告即承诺）
                lockPoint = player.Center;
                breachSurfaceY = surfaceY;
                flankSide = lockPoint.X >= npc.Center.X ? 1f : -1f;
                Vector2 breachPos = new Vector2(lockPoint.X, surfaceY);
                int omen = Projectile.NewProjectile(npc.GetSource_FromAI(), breachPos, Vector2.Zero,
                    ModContent.ProjectileType<OceanSharkOmen>(), 0, 0f, Main.myPlayer,
                    PackSource(npc), OceanSharkOmen.ModeBreach, flankSide);
                if (omen < 0 || omen >= Main.maxProjectiles) {
                    return;
                }
                omenIndex = omen;
                moveVariant = 1;
                timer = OceanSharkOmen.BreachFrames;
                phase = PhaseWindupA;
                npc.netUpdate = true;
                return;
            }

            if (!player.wet || !Collision.CanHitLine(npc.Center, 1, 1, player.Center, 1, 1)) {
                return;
            }
            //侧翼冲刺：先绕行到目标侧面（贴身预告实体同步起表）
            flankSide = npc.Center.X >= player.Center.X ? 1f : -1f;
            int stalk = Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, Vector2.Zero,
                ModContent.ProjectileType<OceanSharkOmen>(), 0, 0f, Main.myPlayer,
                PackSource(npc), OceanSharkOmen.ModeStalk, 0f);
            if (stalk < 0 || stalk >= Main.maxProjectiles) {
                return;
            }
            omenIndex = stalk;
            moveVariant = 0;
            timer = SharkFlankFrames;
            phase = PhaseWindupA;
            npc.netUpdate = true;
        }

        /// <summary>鲨鱼前摇甲段：侧翼绕行（缓移压速）或跃咬预备位下潜</summary>
        private void TickSharkWindupA(NPC npc) {
            timer--;
            int omenType = ModContent.ProjectileType<OceanSharkOmen>();
            if (!TryGetBoundOmen(npc, omenType, out Projectile omen)) {
                Abort(npc);//预告缺位=进攻作废（失败方向=安全方向）
                return;
            }
            if (!TryGetTarget(npc, out Player player)) {
                Abort(npc, omenType);
                return;
            }

            float gain = MoveGain(npc);
            if (moveVariant == 0) {
                //绕行导引：压速缓移到侧翼点（此段仍在跟踪，锁向发生在龇牙起点）
                Vector2 flankPoint = player.Center + new Vector2(flankSide * SharkFlankOffset, 20f);
                Vector2 desired = (flankPoint - npc.Center).SafeNormalize(Vector2.UnitX * flankSide)
                    * (SharkFlankSpeed / gain);
                npc.velocity = Vector2.Lerp(npc.velocity, desired, 0.08f);
                if (timer % 6 == 0) {
                    npc.netUpdate = true;
                }
                if (timer > 0) {
                    return;
                }
                //转龇牙：方向自此锁死并写回预告实体（各端画同一条突进巷）
                lockDir = (player.Center - npc.Center).ToRotation();
                omen.ai[2] = lockDir + 10f;
                omen.netUpdate = true;
                timer = SharkSnarlFrames;
                phase = PhaseWindupB;
                npc.velocity *= 0.5f;
                npc.netUpdate = true;
                return;
            }

            //跃咬预备：潜到破浪点后下方（破浪点已锁死，不追踪目标）
            Vector2 stagePoint = new Vector2(lockPoint.X - flankSide * BreachStageBack, breachSurfaceY + BreachStageDepth);
            Vector2 stageDir = (stagePoint - npc.Center).SafeNormalize(Vector2.UnitY) * (BreachStageSpeed / gain);
            npc.velocity = Vector2.Lerp(npc.velocity, stageDir, 0.10f);
            if (timer % 6 == 0) {
                npc.netUpdate = true;
            }
            if (timer > 0) {
                return;
            }
            if (!npc.wet) {
                Abort(npc, omenType);//被打出水体=跃咬作废
                return;
            }
            //弧线解算（镜像 SlimeKin 口径）：位移项除回提速补偿，重力项不除；
            //非追踪保证：跃咬只飞向生成帧锁死的 lockPoint，执行期从不重瞄
            Vector2 d = (lockPoint - npc.Center) / gain;
            breachVx = MathHelper.Clamp(d.X / BreachFlightFrames, -BreachMaxVx, BreachMaxVx);
            breachVy = MathHelper.Clamp(
                (d.Y - 0.5f * BreachGravity * BreachFlightFrames * BreachFlightFrames) / BreachFlightFrames,
                BreachMaxUpVy, 2f);
            npc.velocity = new Vector2(breachVx, breachVy);
            npc.netUpdate = true;
            timer = BreachFlightFrames;
            phase = PhaseStrike;
        }

        //==================== 相位分发 ====================

        private void TickWindupA(NPC npc) {
            switch (family) {
                case OceanFamily.Shark:
                    TickSharkWindupA(npc);
                    return;
                case OceanFamily.Jelly:
                    TickJellyHold(npc);
                    return;
                case OceanFamily.Squid:
                    TickSquidWindup(npc);
                    return;
                case OceanFamily.Crab:
                    TickCrabBuried(npc);
                    return;
                default:
                    phase = PhaseIdle;
                    cooldown = IneligibleDelay;
                    return;
            }
        }

        /// <summary>前摇乙段：鲨=龇牙定身、蟹=破土前摇/近战蓄力（方向已在本段起点锁死）</summary>
        private void TickWindupB(NPC npc) {
            timer--;
            if (family == OceanFamily.Shark) {
                npc.velocity *= 0.75f;//龇牙压速蓄势
                if (timer % 6 == 0) {
                    npc.netUpdate = true;
                }
                if (timer > 0) {
                    return;
                }
                if (!TryGetBoundOmen(npc, ModContent.ProjectileType<OceanSharkOmen>(), out _)) {
                    Abort(npc);
                    return;
                }
                //非追踪保证：突进只读龇牙起点锁死的 lockDir，执行期从不重瞄（预告巷=实际轨迹）
                timer = SharkDashTotal;
                phase = PhaseStrike;
                npc.netUpdate = true;
                return;
            }

            //螃蟹（破土前摇 / 近战蓄力）：横向定身即可见起手
            npc.velocity.X *= 0.2f;
            if (timer % 6 == 0) {
                npc.netUpdate = true;
            }
            if (timer > 0) {
                return;
            }
            float gain = MoveGain(npc);
            if (moveVariant == 1) {
                int moundType = ModContent.ProjectileType<OceanSandMound>();
                if (!TryGetBoundOmen(npc, moundType, out _)) {
                    Abort(npc);//沙堆缺位=伏击作废（失败方向=安全方向）
                    return;
                }
                //破土瞬间收场沙堆（实体 OnKill 在各端归还透明度=瞬间现身）
                KillBoundOmen(npc, moundType);
                npc.velocity.Y = CrabBurstPopVy / gain;
            }
            //非追踪保证：钳击只读前摇起点锁死的 flankSide 横向，执行期从不重瞄
            timer = CrabLungeTotal;
            phase = PhaseStrike;
            npc.netUpdate = true;
        }

        private void TickStrike(NPC npc) {
            timer--;
            float gain = MoveGain(npc);
            switch (family) {
                case OceanFamily.Shark when moveVariant == 0: {
                    //包络塑形突进（缓入→峰值→力竭），承诺性速度除回提速补偿
                    int t = SharkDashTotal - timer;
                    float env = MobDash.Envelope(t, SharkDashRise, SharkDashHold, SharkDashDecay);
                    npc.velocity = lockDir.ToRotationVector2()
                        * (SharkDashPeakByTier[boundTier - 1] * env / gain);
                    if (t == 1 || timer % 6 == 0) {
                        npc.netUpdate = true;
                    }
                    break;
                }
                case OceanFamily.Shark: {
                    //跃咬弧：自持重力逐帧演化（重力项不吃提速层、不除补偿）
                    breachVy += BreachGravity;
                    npc.velocity = new Vector2(breachVx, breachVy);
                    if (timer % 6 == 0) {
                        npc.netUpdate = true;
                    }
                    if (timer < BreachFlightFrames - 10 && npc.wet) {
                        timer = 0;//提前落水则弧线结束
                    }
                    break;
                }
                case OceanFamily.Squid: {
                    int t = SquidJetTotal - timer;
                    float env = MobDash.Envelope(t, SquidJetRise, SquidJetHold, SquidJetDecay);
                    npc.velocity = lockDir.ToRotationVector2() * (SquidJetPeak * env / gain);
                    if (t == 1 || timer % 6 == 0) {
                        npc.netUpdate = true;
                    }
                    break;
                }
                case OceanFamily.Crab: {
                    //横向包络钳击，纵向留给原版重力
                    int t = CrabLungeTotal - timer;
                    float env = MobDash.Envelope(t, CrabLungeRise, CrabLungeHold, CrabLungeDecay);
                    float peak = (moveVariant == 1 ? CrabBurstPeakByTier : CrabClawPeakByTier)[boundTier - 1];
                    npc.velocity.X = flankSide * peak * env / gain;
                    if (t == 1 || timer % 6 == 0) {
                        npc.netUpdate = true;
                    }
                    break;
                }
            }
            if (timer > 0) {
                return;
            }
            phase = PhaseRecover;
            omenIndex = -1;
            timer = family switch {
                OceanFamily.Shark => moveVariant == 1 ? BreachRecover : SharkDashRecover,
                OceanFamily.Squid => SquidRecover,
                OceanFamily.Crab => CrabRecover,
                _ => 12,
            };
            npc.netUpdate = true;
        }

        /// <summary>后摇：衰减清残速，把控制权干净还给原版 AI，然后进冷却</summary>
        private void TickRecover(NPC npc) {
            timer--;
            if (family == OceanFamily.Jelly) {
                //泄力下沉（后摇即下沉，非注入承诺，无需补偿）
                npc.velocity = Vector2.Lerp(npc.velocity, new Vector2(0f, JellySinkSpeed), 0.2f);
            }
            else if (family == OceanFamily.Crab) {
                npc.velocity.X *= 0.85f;
            }
            else {
                npc.velocity *= 0.90f;//力竭漂移
            }
            if (timer % 6 == 0) {
                npc.netUpdate = true;
            }
            if (timer > 0) {
                return;
            }
            phase = PhaseIdle;
            int baseCooldown = family switch {
                OceanFamily.Shark => SharkDashCooldownByTier[boundTier - 1]
                    + (moveVariant == 1 ? BreachExtraCooldown : 0),
                OceanFamily.Jelly => PulseCooldownByTier[boundTier - 1],
                OceanFamily.Squid => InkCooldownByTier[boundTier - 1],
                OceanFamily.Crab => moveVariant == 1
                    ? CrabAmbushRearmByTier[boundTier - 1] : CrabClawCooldownByTier[boundTier - 1],
                _ => 300,
            };
            cooldown = baseCooldown + Main.rand.Next(CooldownJitter + 1);
        }

        //==================== 水母 ====================

        /// <summary>电场脉冲：静止充能→环实体开窗放电；环参数三型差异见 OceanPulseRing 的 ByType 表</summary>
        private void TryStartJelly(NPC npc, Player player) {
            cooldown = RetryDelay;
            if (!npc.wet) {
                return;
            }
            int variant = JellyVariant(npc.type);
            float radius = OceanPulseRing.RadiusFor(variant, boundTier - 1);
            if (npc.Distance(player.Center) > radius + PulseTriggerPad) {
                return;
            }
            if (CountActive(ModContent.ProjectileType<OceanPulseRing>()) >= RingCap) {
                return;
            }
            int damage = Math.Max(1, (int)(npc.damage * PulseDamageFrac));
            int ring = Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, Vector2.Zero,
                ModContent.ProjectileType<OceanPulseRing>(), damage, 2f, Main.myPlayer,
                PackSource(npc), OceanPulseRing.Pack(variant, boundTier - 1));
            if (ring < 0 || ring >= Main.maxProjectiles) {
                return;
            }
            omenIndex = ring;
            timer = OceanPulseRing.NpcHoldFrames(variant);
            phase = PhaseWindupA;
            npc.velocity *= 0.3f;
            npc.netUpdate = true;
        }

        /// <summary>水母定身段：充能+拍窗全程压速定身；环实体缺位立即回冷却（失败方向=安全方向）</summary>
        private void TickJellyHold(NPC npc) {
            timer--;
            if (!TryGetBoundOmen(npc, ModContent.ProjectileType<OceanPulseRing>(), out _)) {
                Abort(npc);
                return;
            }
            npc.velocity *= 0.30f;//静止充能（长保持段低频重推）
            if (timer % 10 == 0) {
                npc.netUpdate = true;
            }
            if (timer > 0) {
                return;
            }
            phase = PhaseRecover;
            timer = JellySinkFrames;
            omenIndex = -1;
            npc.netUpdate = true;
        }

        //==================== 鱿鱼 ====================

        /// <summary>墨幕脱身：受击（余波窗内）或被逼近触发；反向锁定于前摇起点（脱身无伤害，无预告债）</summary>
        private void TryStartSquid(NPC npc, Player player) {
            cooldown = RetryDelay;
            if (!npc.wet) {
                return;
            }
            if (hurtWindow <= 0 && npc.Distance(player.Center) > SquidPanicRange) {
                return;
            }
            hurtWindow = 0;
            lockDir = (npc.Center - player.Center).SafeNormalize(Vector2.UnitX * -npc.direction).ToRotation();
            moveVariant = 0;
            timer = SquidWindupFrames;
            phase = PhaseWindupA;
            npc.velocity *= 0.5f;
            npc.netUpdate = true;
        }

        private void TickSquidWindup(NPC npc) {
            timer--;
            npc.velocity *= 0.6f;//短促压势
            if (timer % 6 == 0) {
                npc.netUpdate = true;
            }
            if (timer > 0) {
                return;
            }
            //墨云留在起跳点；并发闸满则放弃墨云只保喷射（脱身机制不空转）
            if (CountActive(ModContent.ProjectileType<OceanInkCloud>()) < InkCap) {
                Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, Vector2.Zero,
                    ModContent.ProjectileType<OceanInkCloud>(), 0, 0f, Main.myPlayer,
                    InkRadiusByTier[boundTier - 1], InkLingerFrames);
            }
            timer = SquidJetTotal;
            phase = PhaseStrike;
            npc.netUpdate = true;
        }

        //==================== 螃蟹 ====================

        /// <summary>掘沙伏击（远距+沙地）或近战蓄力钳击（贴身）</summary>
        private void TryStartCrab(NPC npc, Player player) {
            cooldown = RetryDelay;
            if (npc.velocity.Y != 0f) {
                return;
            }
            float dist = npc.Distance(player.Center);

            if (dist > CrabAmbushMinPlayerDist && StandingOnSand(npc)
                && CountActive(ModContent.ProjectileType<OceanSandMound>()) < MoundCap) {
                //半埋伏击：沙堆实体负责所有端的可见性与宿主半透明盖戳
                int mound = Projectile.NewProjectile(npc.GetSource_FromAI(),
                    npc.Bottom + new Vector2(0f, -6f), Vector2.Zero,
                    ModContent.ProjectileType<OceanSandMound>(), 0, 0f, Main.myPlayer,
                    PackSource(npc), OceanSandMound.StateBuried);
                if (mound < 0 || mound >= Main.maxProjectiles) {
                    return;
                }
                omenIndex = mound;
                moveVariant = 1;
                timer = BuryMaxFrames;
                phase = PhaseWindupA;
                npc.velocity.X *= 0.2f;
                npc.netUpdate = true;
                return;
            }

            if (dist < CrabClawMinRange || dist > CrabClawMaxRange
                || Math.Abs(player.Center.Y - npc.Center.Y) > CrabClawMaxDy) {
                return;
            }
            if (!Collision.CanHitLine(npc.position, npc.width, npc.height, player.position, player.width, player.height)) {
                return;
            }
            //近战蓄力钳击：横向锁定于蓄力起点（M3 纯近身体术条款：≥24 帧压速定身即可见起手）
            flankSide = player.Center.X >= npc.Center.X ? 1f : -1f;
            moveVariant = 0;
            timer = CrabClawWindupFrames;
            phase = PhaseWindupB;
            npc.velocity.X *= 0.2f;
            npc.netUpdate = true;
        }

        /// <summary>半埋潜伏：定身等待，玩家进圈转破土前摇；潜伏超时自行收场</summary>
        private void TickCrabBuried(NPC npc) {
            timer--;
            int moundType = ModContent.ProjectileType<OceanSandMound>();
            if (!TryGetBoundOmen(npc, moundType, out Projectile mound)) {
                Abort(npc);//沙堆缺位（被外力清除等）=伏击作废
                return;
            }
            if (!TryGetTarget(npc, out Player player)) {
                Abort(npc, moundType);
                return;
            }
            npc.velocity.X *= 0.1f;//半埋定身
            if (timer % 10 == 0) {
                npc.netUpdate = true;
            }

            if (npc.Distance(player.Center) < CrabBurstTriggerDist) {
                //破土前摇：横向此刻锁死；沙堆转入鼓包状态（ai[1] 随包同步，各端沙尘鼓包）
                flankSide = player.Center.X >= npc.Center.X ? 1f : -1f;
                mound.ai[1] = OceanSandMound.StateBurst;
                mound.netUpdate = true;
                timer = CrabBurstWindupFrames;
                phase = PhaseWindupB;
                npc.netUpdate = true;
                return;
            }
            if (timer <= 0) {
                //空埋收场：无人上钩，起身回巡逻
                KillBoundOmen(npc, moundType);
                phase = PhaseIdle;
                cooldown = UnburyCooldown;
            }
        }
    }
}
