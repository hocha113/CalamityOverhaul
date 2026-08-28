using CalamityOverhaul.Content.GameModes.BrutalMobs.Common;
using CalamityOverhaul.Content.GameModes.BrutalMobs.Legion.Projectiles;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.Legion
{
    /// <summary>
    /// 残酷模式军团机制层（血月 + 哥布林军队），叠加在原版 AI 之上，不接管、不动数值属性。
    /// 军官光环：新郎/新娘/哥布林法师/哥布林召唤师给半径内同军团现役怪挂原版铁皮 buff
    /// （NPC 侧无原生数值效果，纯作已同步的军阵状态载体），受庇护者减伤、战士盾更硬、
    /// 弓手解锁齐射——斩首军官即全部剥除，制造战术选择。
    /// 盾墙：哥布林战士接敌时正面举盾减伤，绕后/越顶/趁跳跃是正解，姿态可见（盾牌实绘）。
    /// 潮汐节拍：血月怪按世界时钟分两波错拍推进，波间有全体喘息窗；军官不随潮（稳定锚点=斩首窗口）。
    /// 近战三怪：苦工盾肩撞（举盾前摇→包络肩撞→命中小击退）、窃贼挥刀偷袭（得手旗+
    /// 逃离倾向+击杀返币）、斥候拉距点射（后跃脱离→立定瞄准线→单发短矢）。
    /// 血月困难精英：血鳗破水三连跃（每段水面警戒环）、血鲨两栖猎杀（陆突进/水跃咬）、
    /// 小丑滚地炸弹（引信+警示环+可打哑火）、血乌贼墨汁三连（固定节奏缺口）；精英不随潮汐。
    /// 豁免声明：血鹦鹉螺 BloodNautilus 原版已是小 Boss 强度、弹幕行为完整，只吃数值层不入机制表；
    /// 影焰幽灵 ShadowFlameApparition 原版自带穿墙追袭行为，不强加（原版细节离线未核实）。
    /// 联机：运动调制两端确定性同跑（输入均为已同步原语 Main.time / whoAmI / buff，镜像
    /// <see cref="GameModeNPC.PostAI"/> 的零网络模式）；buff 授予与弹幕生成只在权威端
    /// </summary>
    internal class LegionNPC : GlobalNPC
    {
        public override bool InstancePerEntity => true;

        //——军官光环——
        /// <summary>光环半径</summary>
        private const float OfficerAuraRadius = 520f;
        /// <summary>权威端授环间隔（帧），军官间按 whoAmI 错帧摊开扫描开销</summary>
        private const int AuraRefreshInterval = 30;
        /// <summary>单次授予的 buff 时长；军官死亡后至多此帧数内光环自然剥落</summary>
        private const int AuraBuffTime = 50;
        /// <summary>光环标记 buff：原版铁皮（原生同步，NPC 侧无任何原版数值挂钩）</summary>
        internal const int WardBuff = BuffID.Ironskin;
        /// <summary>受庇护者的减伤比例（档位只调强度）</summary>
        private static float AuraDamageResist(int tier) => tier switch { 3 => 0.25f, 2 => 0.20f, _ => 0.15f };

        //——盾墙——
        /// <summary>正面格挡减伤（档位只调强度）</summary>
        private static float BlockReduction(int tier) => tier switch { 3 => 0.52f, 2 => 0.46f, _ => 0.40f };
        /// <summary>受军官庇护时的格挡追加</summary>
        private const float WardedBlockBonus = 0.15f;
        /// <summary>举盾所需的交战横向距离</summary>
        private const float BraceEngageRange = 560f;
        /// <summary>越顶豁免（公平阀门）：伤害来源高于战士头顶此距离则绕过盾墙</summary>
        private const float BraceOverheadBypass = 64f;
        /// <summary>举盾时横向步伐阻滞（每帧乘）：姿态在运动上同样可读，并给绕后留时间</summary>
        private const float BraceMoveDamp = 0.94f;
        /// <summary>叠加减伤下限（公平阀门）：伤害保留系数永不低于此值，保证永远打得动</summary>
        private const float CombinedResistFloor = 0.25f;

        //——潮汐节拍——
        /// <summary>完整潮汐周期（帧）：A波 + 喘息 + B波 + 喘息</summary>
        private const int TideCycle = 840;
        /// <summary>单波推进时长</summary>
        private const int TideWaveLen = 300;
        /// <summary>波间全体喘息窗（公平阀门）：此窗口内两波都不推进，发射循环真正读取见 <see cref="TideStrength"/></summary>
        private const int TideGapLen = 120;
        /// <summary>波沿缓入缓出帧数，避免速度突变</summary>
        private const int TideRamp = 30;
        /// <summary>涨潮位置推进系数（档位只调强度，叠加在通用提速之上）</summary>
        private static float SurgeBonus(int tier) => tier switch { 3 => 0.40f, 2 => 0.32f, _ => 0.25f };
        /// <summary>受军官庇护的涨潮加乘</summary>
        private const float WardedSurgeMult = 1.25f;
        /// <summary>退潮阻滞（每帧乘）：涨潮之外的血月怪明显放慢，喘息窗可读</summary>
        private const float LullDamp = 0.90f;

        //——军团箭令（哥布林弓手齐射，仅军官在场时解锁）——
        /// <summary>齐射冷却（档位只调强度）</summary>
        private static int VolleyCooldown(int tier) => tier switch { 3 => 220, 2 => 260, _ => 300 };
        /// <summary>预告帧数（公平底线 ≥30）</summary>
        internal const int VolleyTelegraphFrames = 45;
        /// <summary>最小射距（公平阀门）：贴脸不放箭</summary>
        private const float VolleyMinRange = 220f;
        /// <summary>最大射距</summary>
        private const float VolleyMaxRange = 780f;
        /// <summary>全局并发上限（预告体 + 战矢合计）</summary>
        private const int VolleyGlobalCap = 6;
        /// <summary>战矢伤害 = npc.damage（已含通用缩放）× 此系数</summary>
        private const float VolleyDamageMult = 0.6f;
        /// <summary>战矢初速</summary>
        internal const float VolleyArrowSpeed = 12.5f;

        //——新招通用节奏（近战三怪与血月精英共用，M7 密度口径）——
        /// <summary>新招首发错拍窗下限（60~180 帧内见招）</summary>
        private const int FirstStrikeMin = 60;
        /// <summary>新招首发错拍窗上限</summary>
        private const int FirstStrikeMax = 180;
        /// <summary>条件未满足的重试间隔</summary>
        private const int RetryDelay = 30;
        /// <summary>冷却随机抖动上限</summary>
        private const int CooldownJitter = 50;

        //——哥布林近战三怪（苦工盾肩撞 / 窃贼挥刀偷袭 / 斥候拉距点射）——
        /// <summary>苦工肩撞冷却（档位只调强度）</summary>
        private static readonly int[] PeonCooldownByTier = [300, 260, 220];
        /// <summary>苦工肩撞名义峰速（未含提速补偿，注入时除回 MoveGain）</summary>
        private const float PeonDashPeak = 7.2f;
        /// <summary>苦工肩撞触发横距下限（贴脸不起手）</summary>
        private const float PeonEngageMin = 40f;
        /// <summary>苦工肩撞触发横距上限</summary>
        private const float PeonEngageMax = 260f;
        /// <summary>盾肩撞命中的小击退横向推力（差异点）</summary>
        private const float PeonShoveKickX = 5f;
        /// <summary>盾肩撞命中的小上抬</summary>
        private const float PeonShoveKickY = -2f;
        /// <summary>窃贼快扑冷却</summary>
        private static readonly int[] ThiefCooldownByTier = [340, 300, 260];
        /// <summary>窃贼快扑名义峰速（比苦工快而短促）</summary>
        private const float ThiefPouncePeak = 9f;
        /// <summary>窃贼快扑触发横距下限</summary>
        private const float ThiefEngageMin = 50f;
        /// <summary>窃贼快扑触发横距上限</summary>
        private const float ThiefEngageMax = 300f;
        /// <summary>得手旗 buff：原版迅捷（原生同步，NPC 侧无原版数值挂钩，镜像 WardBuff 的载体用法）</summary>
        internal const int FleeBuff = BuffID.Swiftness;
        /// <summary>得手后加速逃离倾向时长（帧）</summary>
        private const int FleeFrames = 120;
        /// <summary>逃离横向位移增益（横速 1.2 倍 = 追加 0.2 推进，仅背离玩家移动时生效）</summary>
        private const float FleeGainBonus = 0.2f;
        /// <summary>得手补偿：被击杀掉落的银币数（按档位；只有得手闩真时掉）</summary>
        private static readonly int[] LootCoinsByTier = [15, 25, 40];
        /// <summary>斥候点射冷却</summary>
        private static readonly int[] ScoutCooldownByTier = [320, 280, 240];
        /// <summary>斥候被贴近的脱离触发距离（~200px）</summary>
        private const float ScoutPanicRange = 200f;
        /// <summary>斥候后跃名义横速（除回 MoveGain）</summary>
        private const float ScoutHopVx = 6.5f;
        /// <summary>斥候后跃上抬初速（逃逸跳非承诺轨迹，纵向交原版重力不除）</summary>
        private const float ScoutHopVy = -5.5f;
        /// <summary>斥候后跃持续帧</summary>
        private const int ScoutHopFrames = 22;
        /// <summary>斥候点射最小射距（落地后仍贴脸则放弃）</summary>
        private const float ScoutAimMinRange = 120f;
        /// <summary>斥候点射最大射距</summary>
        private const float ScoutAimMaxRange = 560f;
        /// <summary>短矢伤害 = npc.damage × 此系数</summary>
        private const float ScoutBoltDamageMult = 0.6f;
        /// <summary>斥候族并发闸（瞄准线+短矢合计，独立于弓手齐射的 VolleyGlobalCap）</summary>
        private const int ScoutGlobalCap = 4;

        //——血月困难精英（血鳗跃击 / 血鲨两栖 / 小丑滚弹 / 血乌贼墨汁）——
        /// <summary>血鳗三连跃冷却（招牌招，仍 ≤600）</summary>
        private static readonly int[] EelCooldownByTier = [560, 500, 440];
        /// <summary>血鳗单段跃弧名义横速</summary>
        private const float EelLeapVx = 4.6f;
        /// <summary>血鳗单段跃弧名义上抬初速</summary>
        private const float EelLeapVy = -10.5f;
        /// <summary>跃弧合成重力（本层合成项：连同初速一起除回 MoveGain，实现弧线=名义弧线）</summary>
        private const float LeapGravity = 0.30f;
        /// <summary>血鳗跃击总段数（每段重新预告）</summary>
        private const int EelLeapCount = 3;
        /// <summary>单段跃行保底超时帧（正常以再次入水收段）</summary>
        private const int LeapMaxFrames = 70;
        /// <summary>破水类招式允许的最大水深（太深则预告环与实际出水点失配，不起跳）</summary>
        private const float SurfMaxDepth = 128f;
        /// <summary>水面上方需要的净空（物块格数），不足则不起跳</summary>
        private const int SurfAirClearTiles = 6;
        /// <summary>水面预告并发闸</summary>
        private const int SurfGlobalCap = 4;
        /// <summary>血鲨冷却</summary>
        private static readonly int[] SharkCooldownByTier = [380, 330, 280];
        /// <summary>血鲨陆上突进名义峰速</summary>
        private const float SharkDashPeak = 8.5f;
        /// <summary>血鲨陆上触发横距下限</summary>
        private const float SharkEngageMin = 60f;
        /// <summary>血鲨陆上触发横距上限</summary>
        private const float SharkEngageMax = 340f;
        /// <summary>血鲨水中跃咬的目标横距上限</summary>
        private const float SharkAquaMaxRangeX = 420f;
        /// <summary>血鲨破浪跃咬名义横速</summary>
        private const float SharkLeapVx = 6.0f;
        /// <summary>血鲨破浪跃咬名义上抬初速</summary>
        private const float SharkLeapVy = -11f;
        /// <summary>小丑掷弹冷却</summary>
        private static readonly int[] ClownCooldownByTier = [520, 460, 400];
        /// <summary>小丑掷弹触发横距下限</summary>
        private const float ClownThrowMin = 120f;
        /// <summary>小丑掷弹触发横距上限</summary>
        private const float ClownThrowMax = 520f;
        /// <summary>炸弹抛物飞行帧（掷点解算用）</summary>
        private const int BombFlightFrames = 45;
        /// <summary>炸弹伤害 = npc.damage × 此系数（引信长、警示环足，换高单发）</summary>
        private const float BombDamageMult = 0.85f;
        /// <summary>滚地炸弹并发闸</summary>
        private const int BombGlobalCap = 3;
        /// <summary>血乌贼墨汁冷却</summary>
        private static readonly int[] SquidCooldownByTier = [400, 350, 300];
        /// <summary>血乌贼射程环带下限</summary>
        private const float SquidMinRange = 180f;
        /// <summary>血乌贼射程环带上限</summary>
        private const float SquidMaxRange = 620f;
        /// <summary>墨弹伤害 = npc.damage × 此系数</summary>
        private const float InkDamageMult = 0.5f;
        /// <summary>墨汁族并发闸（预告+墨弹合计）</summary>
        private const int InkGlobalCap = 6;

        /// <summary>军团角色，由 NPC 类型静态决定（跨端天然一致，无需同步）</summary>
        private enum LegionRole : byte
        {
            None,
            /// <summary>血月士卒：随潮汐推进</summary>
            BloodTroop,
            /// <summary>血月军官：新郎/新娘</summary>
            BloodOfficer,
            /// <summary>哥布林士卒：战士带盾墙、弓手带齐射</summary>
            GoblinTroop,
            /// <summary>哥布林军官：法师/召唤师</summary>
            GoblinOfficer,
            /// <summary>血月困难精英：血鳗/血鲨/小丑/血乌贼，各带独立招式分支；
            /// 不随潮汐（突进承诺轨迹不吃潮涌推进），也不受军官光环（血月侧光环只授士卒）</summary>
            BloodElite,
        }

        /// <summary>本个体生成时绑定的档位，0 = 无机制</summary>
        private int boundTier;
        private LegionRole role;
        /// <summary>弓手齐射计时（权威端决策私产，客户端可见状态全在预告体实体上）</summary>
        private int volleyTimer;
        /// <summary>战士举盾态：各端从已同步原语确定性求值，绘制与判伤读同一个值</summary>
        private bool braced;
        /// <summary>上一帧涨潮强度，仅作涨潮沿的视觉检测</summary>
        private float prevSurge;

        //——新招相位机（权威端决策私产；客户端可见状态全在预告实体与同步速度上）——
        private const byte PhaseIdle = 0;
        private const byte PhaseWindup = 1;
        private const byte PhaseStrike = 2;
        private const byte PhaseRecover = 3;
        private const byte PhaseHop = 4;
        /// <summary>当前相位</summary>
        private byte movePhase;
        /// <summary>相位内计时</summary>
        private int moveTimer;
        /// <summary>新招冷却计时（SetDefaults 播首发错拍）</summary>
        private int moveCooldown;
        /// <summary>锁定横向 ±1（前摇起手即锁，预告即承诺）</summary>
        private float lockSign;
        /// <summary>锁定点（小丑掷点 / 水面破浪点）</summary>
        private Vector2 lockPoint;
        /// <summary>跃弧持有速度（血鳗/血鲨：合成重力逐帧累加，抵住原版蠕行/游动转向）</summary>
        private Vector2 leapVel;
        /// <summary>血鳗已完成的跃段数</summary>
        private int leapSegment;
        /// <summary>跃行段"已出过水"证据（再次入水即收段）</summary>
        private bool leapExitedWater;
        /// <summary>血鲨本次招式是否走水线（决定前摇/执行分支）</summary>
        private bool aquaticMove;
        /// <summary>绑定的预告体槽位（前摇逐帧回读校验，缺位=回冷却，失败方向=安全方向）</summary>
        private int omenIndex = -1;
        /// <summary>窃贼得手闩（权威端私产，只驱动 OnKill 补偿掉落；可见状态走 FleeBuff 原生同步）</summary>
        private bool lootTaken;

        //豁免：NPCID.BloodNautilus（血鹦鹉螺）原版已是小 Boss 强度、自带完整弹幕行为，
        //不入机制表，只吃 GameModeNPC 数值层。
        //豁免：NPCID.ShadowFlameApparition（哥布林召唤师的影焰幽灵）原版自带穿墙追袭的
        //完整战斗行为，不强加机制（其弹幕细节离线未核实，留待实测再议）
        private static LegionRole ResolveRole(int type) => type switch {
            NPCID.BloodZombie or NPCID.Drippler or NPCID.ZombieMerman or NPCID.EyeballFlyingFish => LegionRole.BloodTroop,
            NPCID.TheGroom or NPCID.TheBride => LegionRole.BloodOfficer,
            NPCID.GoblinPeon or NPCID.GoblinThief or NPCID.GoblinWarrior
                or NPCID.GoblinArcher or NPCID.GoblinScout => LegionRole.GoblinTroop,
            NPCID.GoblinSorcerer or NPCID.GoblinSummoner => LegionRole.GoblinOfficer,
            //血月困难精英只挂血鳗头（Body/Tail 类型不在表内天然排除）
            NPCID.BloodEelHead or NPCID.GoblinShark or NPCID.Clown or NPCID.BloodSquid => LegionRole.BloodElite,
            _ => LegionRole.None,
        };

        /// <summary>该类型是否带新增相位招式（首发错拍窗只对这些类型播种）</summary>
        private static bool HasPhaseMove(int type) => type is NPCID.GoblinPeon or NPCID.GoblinThief
            or NPCID.GoblinScout or NPCID.BloodEelHead or NPCID.GoblinShark or NPCID.Clown or NPCID.BloodSquid;

        public override bool AppliesToEntity(NPC entity, bool lateInstantiation)
            => ResolveRole(entity.type) != LegionRole.None;

        public override void SetDefaults(NPC npc) {
            boundTier = 0;
            role = LegionRole.None;
            int tier = GameModeSystem.EffectiveTier;
            if (tier <= 0) {
                return;
            }
            //资格排除：友方/无敌/小动物口径 + Boss + 蠕虫体节。血鳗只挂头：Body/Tail 类型
            //不在表内天然排除；SetDefaults 时 realLife 尚未被原版 AI 赋值，此检查为纪律性保留
            if (npc.friendly || npc.townNPC || npc.immortal || npc.dontTakeDamage) {
                return;
            }
            if (npc.lifeMax <= 5 || npc.damage <= 0 || npc.boss || npc.realLife >= 0) {
                return;
            }
            role = ResolveRole(npc.type);
            if (role == LegionRole.None) {
                return;
            }
            boundTier = tier;
            if (npc.type == NPCID.GoblinArcher) {
                //哨兵：NewNPC 先跑 SetDefaults 再写 whoAmI，此刻读 whoAmI 恒为 0，
                //首发错拍推迟到首个决策帧（届时 whoAmI 已有效）播种
                volleyTimer = -1;
            }
            if (HasPhaseMove(npc.type)) {
                //新招首发错拍（60~180）：冷却是权威端决策私产，Main.rand 此处无同步语义；
                //whoAmI 此刻恒 0 不可作错拍源（镜像 PumpkinMoonNPC 的播种口径）
                moveCooldown = FirstStrikeMin + Main.rand.Next(FirstStrikeMax - FirstStrikeMin + 1);
            }
        }

        /// <summary>机制运行资格：出生已绑定，且排除雕像怪与临时无敌态（这两项在 SetDefaults 后才可能置位）</summary>
        private bool MechanicActive(NPC npc)
            => boundTier > 0 && !npc.SpawnedFromStatue && !npc.dontTakeDamage;

        public override void PostAI(NPC npc) {
            if (!MechanicActive(npc)) {
                return;
            }
            switch (role) {
                case LegionRole.BloodTroop:
                    TideStep(npc);
                    WardSparkle(npc);
                    break;
                case LegionRole.GoblinTroop:
                    if (npc.type == NPCID.GoblinWarrior) {
                        BraceStep(npc);
                    }
                    else if (npc.type == NPCID.GoblinArcher) {
                        VolleyStep(npc);
                    }
                    else if (npc.type == NPCID.GoblinPeon) {
                        PeonStep(npc);
                    }
                    else if (npc.type == NPCID.GoblinThief) {
                        ThiefStep(npc);
                    }
                    else if (npc.type == NPCID.GoblinScout) {
                        ScoutStep(npc);
                    }
                    WardSparkle(npc);
                    break;
                case LegionRole.BloodOfficer:
                case LegionRole.GoblinOfficer:
                    OfficerStep(npc);
                    break;
                case LegionRole.BloodElite:
                    EliteStep(npc);
                    break;
            }
        }

        #region 军官光环
        private void OfficerStep(NPC npc) {
            //授环只在权威端跑，buff 走原版 AddNPCBuff 包原生同步；军官间按 whoAmI 错帧
            if (Main.netMode != NetmodeID.MultiplayerClient
                && Main.GameUpdateCount % AuraRefreshInterval == (uint)(npc.whoAmI % AuraRefreshInterval)) {
                bool bloodSide = role == LegionRole.BloodOfficer;
                float radiusSq = OfficerAuraRadius * OfficerAuraRadius;
                foreach (NPC other in Main.ActiveNPCs) {
                    if (other.whoAmI == npc.whoAmI || other.SpawnedFromStatue) {
                        continue;
                    }
                    LegionRole otherRole = ResolveRole(other.type);
                    bool sameLegion = bloodSide
                        ? otherRole == LegionRole.BloodTroop
                        : otherRole == LegionRole.GoblinTroop;
                    if (!sameLegion) {
                        continue;
                    }
                    if (Vector2.DistanceSquared(other.Center, npc.Center) > radiusSq) {
                        continue;
                    }
                    other.AddBuff(WardBuff, AuraBuffTime);
                }
            }

            //军官仪仗：头顶旗辉，血月军官猩红、哥布林军官鎏金（身份由类型静态决定，客户端直接绘）
            if (!Main.dedServ && Main.rand.NextBool(6)) {
                int dustType = role == LegionRole.BloodOfficer ? DustID.CrimsonTorch : DustID.GoldFlame;
                Dust glint = Dust.NewDustPerfect(
                    npc.Top + new Vector2(Main.rand.NextFloat(-8f, 8f), -6f),
                    dustType, new Vector2(0f, -Main.rand.NextFloat(0.8f, 1.6f)), 100, default, 1.1f);
                glint.noGravity = true;
            }
        }

        /// <summary>受庇护士卒的金辉勾边（低频，客户端）</summary>
        private void WardSparkle(NPC npc) {
            if (Main.dedServ || !npc.HasBuff(WardBuff) || !Main.rand.NextBool(16)) {
                return;
            }
            Dust spark = Dust.NewDustDirect(npc.position, npc.width, npc.height,
                DustID.GoldFlame, 0f, -0.5f, 130, default, 0.8f);
            spark.noGravity = true;
            spark.velocity *= 0.4f;
        }
        #endregion

        #region 盾墙
        private void BraceStep(NPC npc) {
            braced = false;
            //离地即破盾：原版战士 AI 频繁跳跃，天然形成开火窗
            if (npc.velocity.Y != 0f || !npc.HasValidTarget) {
                return;
            }
            Player target = Main.player[npc.target];
            float dx = target.Center.X - npc.Center.X;
            braced = Math.Abs(dx) < BraceEngageRange && Math.Sign(dx) == npc.direction;
            if (braced) {
                npc.velocity.X *= BraceMoveDamp;
            }
        }

        /// <summary>正面格挡判定：绘制（PostDraw 的盾）与减伤读取同一 braced，伤害窗口=可见窗口</summary>
        private bool FrontBlocked(NPC npc, Vector2 source, bool overhead) {
            if (!braced || overhead) {
                return false;
            }
            if (source.Y < npc.position.Y - BraceOverheadBypass) {
                return false;
            }
            return (source.X - npc.Center.X) * npc.direction >= 0f;
        }

        /// <summary>弹幕的越顶豁免：命中瞬间弹幕中心必然贴着受击者，源点高度差永远凑不满
        /// <see cref="BraceOverheadBypass"/>（该规则只对近战的玩家身位成立），故弹幕路径按
        /// 来向角度判"高打"——下坠分量超过水平分量（俯冲陡于 45°）即绕过盾墙，平射不受影响。
        /// velocity 是命中结算端的本地精确值，判定与反馈同源</summary>
        private static bool PlungingShot(Projectile projectile)
            => projectile.velocity.Y > Math.Abs(projectile.velocity.X);

        public override void ModifyHitByItem(NPC npc, Player player, Item item, ref NPC.HitModifiers modifiers) {
            if (!MechanicActive(npc)) {
                return;
            }
            ApplyLegionDefense(npc, player.Center, overhead: false, ref modifiers);
        }

        public override void ModifyHitByProjectile(NPC npc, Projectile projectile, ref NPC.HitModifiers modifiers) {
            if (!MechanicActive(npc)) {
                return;
            }
            ApplyLegionDefense(npc, projectile.Center, PlungingShot(projectile), ref modifiers);
        }

        /// <summary>军阵减伤合成：光环减伤 × 盾墙格挡，钳制在保底可击穿线之上。
        /// tML 打击判定在攻击方本机结算，braced 与 buff 均为各端确定性状态，无需额外同步</summary>
        private void ApplyLegionDefense(NPC npc, Vector2 source, bool overhead, ref NPC.HitModifiers modifiers) {
            float keep = 1f;
            bool warded = npc.HasBuff(WardBuff);
            if (warded) {
                keep *= 1f - AuraDamageResist(boundTier);
            }
            if (npc.type == NPCID.GoblinWarrior && FrontBlocked(npc, source, overhead)) {
                keep *= 1f - (BlockReduction(boundTier) + (warded ? WardedBlockBonus : 0f));
            }
            if (keep >= 1f) {
                return;
            }
            modifiers.FinalDamage *= Math.Max(keep, CombinedResistFloor);
        }

        public override void OnHitByItem(NPC npc, Player player, Item item, NPC.HitInfo hit, int damageDone)
            => BlockFeedback(npc, player.Center, overhead: false);

        public override void OnHitByProjectile(NPC npc, Projectile projectile, NPC.HitInfo hit, int damageDone)
            => BlockFeedback(npc, projectile.Center, PlungingShot(projectile));

        /// <summary>格挡反馈：铁火花 + 金铁声（命中方本机，让被减伤的攻击者立刻明白原因）</summary>
        private void BlockFeedback(NPC npc, Vector2 source, bool overhead) {
            if (Main.dedServ || !MechanicActive(npc)
                || npc.type != NPCID.GoblinWarrior || !FrontBlocked(npc, source, overhead)) {
                return;
            }
            SoundEngine.PlaySound(SoundID.NPCHit4 with { Volume = 0.4f, Pitch = 0.35f }, npc.Center);
            Vector2 shieldPos = npc.Center + new Vector2(npc.direction * 16f, -2f);
            for (int i = 0; i < 4; i++) {
                Dust spark = Dust.NewDustPerfect(shieldPos, DustID.Iron,
                    new Vector2(npc.direction * Main.rand.NextFloat(1f, 3f), -Main.rand.NextFloat(0.5f, 2f)),
                    60, default, Main.rand.NextFloat(0.7f, 1.1f));
                spark.noGravity = Main.rand.NextBool();
            }
        }

        public override void PostDraw(NPC npc, SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            if (!MechanicActive(npc)) {
                return;
            }
            if (npc.type == NPCID.GoblinPeon) {
                DrawPeonShield(npc, spriteBatch, screenPos, drawColor);
                return;
            }
            if (npc.type != NPCID.GoblinWarrior || !braced) {
                return;
            }
            //盾墙姿态实绘：原版钴蓝盾贴图染铁灰，真 alpha 本体有遮挡像素；受庇护时镶金边
            Main.instance.LoadItem(ItemID.CobaltShield);
            Texture2D tex = TextureAssets.Item[ItemID.CobaltShield].Value;
            //gfxOffY：上坡步进的绘制补偿，缺了它盾会在走台阶时与身体脱节
            Vector2 pos = npc.Center + new Vector2(npc.direction * 16f, npc.gfxOffY - 2f) - screenPos;
            float tilt = npc.direction * 0.15f;
            SpriteEffects flip = npc.direction > 0 ? SpriteEffects.None : SpriteEffects.FlipHorizontally;
            Color iron = Color.Lerp(drawColor, new Color(150, 155, 165), 0.45f);
            if (npc.HasBuff(WardBuff)) {
                spriteBatch.Draw(tex, pos, null, new Color(255, 200, 90, 0) * 0.35f,
                    tilt, tex.Size() / 2f, 1.12f, flip, 0f);
            }
            spriteBatch.Draw(tex, pos, null, iron, tilt, tex.Size() / 2f, 1f, flip, 0f);
        }

        /// <summary>苦工肩撞的举盾实绘：窗口期从已同步的姿态实体读取（复用战士的盾贴图思路，
        /// 独立分支不触碰战士的 braced 逻辑）；前摇期盾面微光随蓄势渐亮，突进期压低前顶</summary>
        private static void DrawPeonShield(NPC npc, SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            LegionPoseOmen pose = LegionPoseOmen.FindFor(npc.whoAmI, LegionPoseOmen.ModePeon);
            if (pose == null) {
                return;
            }
            Main.instance.LoadItem(ItemID.CobaltShield);
            Texture2D tex = TextureAssets.Item[ItemID.CobaltShield].Value;
            bool striking = pose.InStrike;
            float charge = pose.WindupCharge;
            //突进期盾更前顶更低，读作肩撞发力；gfxOffY 补上坡步进的绘制脱节
            Vector2 pos = npc.Center + new Vector2(npc.direction * (striking ? 20f : 14f),
                npc.gfxOffY + (striking ? 2f : -4f)) - screenPos;
            float tilt = npc.direction * (striking ? 0.42f : 0.15f + 0.12f * charge);
            SpriteEffects flip = npc.direction > 0 ? SpriteEffects.None : SpriteEffects.FlipHorizontally;
            Color iron = Color.Lerp(drawColor, new Color(150, 155, 165), 0.45f);
            //盾面微光：蓄势渐亮的加色描辉（突进期定格全亮）
            float glow = striking ? 0.4f : 0.35f * charge;
            if (glow > 0.01f) {
                spriteBatch.Draw(tex, pos, null, new Color(255, 210, 120, 0) * glow,
                    tilt, tex.Size() / 2f, 0.98f, flip, 0f);
            }
            spriteBatch.Draw(tex, pos, null, iron, tilt, tex.Size() / 2f, 0.9f, flip, 0f);
        }
        #endregion

        #region 潮汐节拍
        /// <summary>
        /// 潮汐强度 0~1。时钟用 Main.time（各端本地推进，服务端借天象/天气等事件的 WorldData 包重锚；
        /// 短时漂移由 <see cref="TideRamp"/> 帧缓坡吸收，且潮汐只影响运动，位置真值始终以服务端 NPC 同步为准）；
        /// 分组用 whoAmI 奇偶（NPC 槽位服务端权威，跨端一致）。
        /// 时间轴：A波[0,300) 全体喘息[300,420) B波[420,720) 全体喘息[720,840)
        /// </summary>
        private static float TideStrength(int whoAmI) {
            float pos = (float)(Main.time % TideCycle);
            float start = (whoAmI & 1) == 0 ? 0f : TideWaveLen + TideGapLen;
            float local = pos - start;
            if (local < 0f || local >= TideWaveLen) {
                return 0f;
            }
            float edgeIn = MathHelper.Clamp(local / TideRamp, 0f, 1f);
            float edgeOut = MathHelper.Clamp((TideWaveLen - local) / TideRamp, 0f, 1f);
            return edgeIn * edgeOut;
        }

        private void TideStep(NPC npc) {
            float surge = TideStrength(npc.whoAmI);
            if (surge > 0f) {
                //涨潮：位置推进（镜像通用提速的碰撞钳制口径），军官在场推得更凶
                float bonus = SurgeBonus(boundTier) * surge;
                if (npc.HasBuff(WardBuff)) {
                    bonus *= WardedSurgeMult;
                }
                Vector2 advance = npc.velocity * bonus;
                if (!npc.noTileCollide) {
                    advance = Collision.TileCollision(npc.position, advance, npc.width, npc.height);
                }
                npc.position += advance;

                if (!Main.dedServ) {
                    if (prevSurge <= 0f) {
                        //涨潮沿：一次性血雾爆点，本波成员可辨
                        for (int i = 0; i < 4; i++) {
                            Dust burst = Dust.NewDustDirect(npc.position, npc.width, npc.height,
                                DustID.Blood, npc.velocity.X * 0.4f, -1.2f, 80, default, 1.2f);
                            burst.noGravity = true;
                        }
                    }
                    else if (Main.rand.NextBool(10)) {
                        Dust drip = Dust.NewDustDirect(npc.position, npc.width, npc.height,
                            DustID.Blood, 0f, 0.6f, 110, default, 0.9f);
                        drip.velocity *= 0.5f;
                    }
                }
            }
            else {
                //退潮：明显放慢=喘息窗可读；有重力者只阻滞横向，避免悬浮感
                npc.velocity.X *= LullDamp;
                if (npc.noGravity) {
                    npc.velocity.Y *= LullDamp;
                }
            }
            prevSurge = surge;
        }
        #endregion

        #region 军团箭令
        private void VolleyStep(NPC npc) {
            //决策与生成只在权威端；客户端的全部可见状态在预告体实体上（原生同步）
            if (Main.netMode == NetmodeID.MultiplayerClient) {
                return;
            }
            if (volleyTimer < 0) {
                //首发冷却减半并按 whoAmI 错拍，避免多弓手同帧齐鸣
                volleyTimer = VolleyCooldown(boundTier) / 2 + npc.whoAmI % 45;
            }
            if (volleyTimer > 0) {
                volleyTimer--;
                return;
            }
            //齐射资格：军官庇护中（斩首即哑火）、立定、目标在射程环带内且有视线
            if (!npc.HasBuff(WardBuff) || !npc.HasValidTarget || npc.velocity.Y != 0f) {
                return;
            }
            Player target = Main.player[npc.target];
            float dist = npc.Distance(target.Center);
            if (dist < VolleyMinRange || dist > VolleyMaxRange) {
                return;
            }
            if (!Collision.CanHitLine(npc.Center, 1, 1, target.position, target.width, target.height)) {
                return;
            }
            //全局并发闸：预告体与在飞战矢合计超限则本次跳过
            int omenType = ModContent.ProjectileType<LegionVolleyOmen>();
            int arrowType = ModContent.ProjectileType<LegionVolleyArrow>();
            int live = 0;
            foreach (Projectile proj in Main.ActiveProjectiles) {
                if (proj.type == omenType || proj.type == arrowType) {
                    live++;
                }
            }
            if (live >= VolleyGlobalCap) {
                volleyTimer = 40;
                return;
            }
            //预告即承诺：方向在此刻锁死进 velocity（随生成包原生同步），此后不再重瞄
            Vector2 aim = npc.DirectionTo(target.Center);
            int damage = (int)(npc.damage * VolleyDamageMult);
            Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, aim,
                omenType, 0, 0f, Main.myPlayer, damage);
            volleyTimer = VolleyCooldown(boundTier) + npc.whoAmI % 45;
        }
        #endregion

        #region 新招共用
        /// <summary>来源打包：whoAmI+1 | type&lt;&lt;8，预告实体据此做施法者死亡/槽位复用校验</summary>
        internal static float PackSource(NPC npc) => (npc.whoAmI + 1) | (npc.type << 8);

        /// <summary>
        /// 提速位移补偿（镜像 PumpkinMoonNPC.MoveGain）：<see cref="GameModeNPC.PostAI"/> 按
        /// velocity×SpeedBonus 追加位置推进，本层注入的承诺性速度一律除回该系数。
        /// 口径与 GameModeNPC.RageEligible 一致且运行时读旗标：boss 旗标个体与共享血池体节
        /// 不吃提速层，系数为 1（血鳗头若运行期共享血池则天然落在此支，与提速层同口径联动）
        /// </summary>
        private float MoveGain(NPC npc) => !npc.boss && npc.realLife < 0 ? 1f + GameModeTuning.SpeedBonus(boundTier) : 1f;

        /// <summary>同型弹幕并发计数（仅触发时调用，自愈无漂移）</summary>
        private static int CountActive(int projType) {
            int count = 0;
            foreach (Projectile proj in Main.ActiveProjectiles) {
                if (proj.type == projType) {
                    count++;
                }
            }
            return count;
        }

        /// <summary>回读绑定预告体的通用校验：索引 + 类型 + 归属（打包低位）三重比对</summary>
        private bool BoundOmenValid(NPC npc, int projType) {
            if (omenIndex < 0 || omenIndex >= Main.maxProjectiles) {
                return false;
            }
            Projectile proj = Main.projectile[omenIndex];
            return proj.active && proj.type == projType && ((int)proj.ai[0] & 255) - 1 == npc.whoAmI;
        }

        /// <summary>预告缺位/条件破产的统一回退：失败方向=安全方向（回冷却，不出手）</summary>
        private void AbortMove(int cooldown) {
            movePhase = PhaseIdle;
            omenIndex = -1;
            moveCooldown = cooldown;
        }

        /// <summary>新招收尾：清相位，按档位表+随机抖动重置冷却（权威端 Main.rand 无同步语义）</summary>
        private void FinishMove(int[] cooldownByTier) {
            movePhase = PhaseIdle;
            omenIndex = -1;
            moveCooldown = cooldownByTier[boundTier - 1] + Main.rand.Next(CooldownJitter + 1);
        }

        /// <summary>触发通用闸：冷却走完 + 目标有效存活，否则短延迟复查</summary>
        private bool MoveReady(NPC npc, out Player target) {
            target = null;
            if (moveCooldown > 0) {
                moveCooldown--;
                return false;
            }
            if (!npc.HasValidTarget) {
                moveCooldown = RetryDelay;
                return false;
            }
            Player player = Main.player[npc.target];
            if (!player.Alives()) {
                moveCooldown = RetryDelay;
                return false;
            }
            target = player;
            return true;
        }

        /// <summary>起手地面突进：锁向 + 生成姿态实体 + 刹车定身（锁向即承诺，突进不再重瞄）。
        /// 姿态实体生成失败（弹幕位满）返回假，整次进攻作废</summary>
        private bool StartGroundDash(NPC npc, Player target, int mode) {
            lockSign = target.Center.X >= npc.Center.X ? 1f : -1f;
            int omen = Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, Vector2.Zero,
                ModContent.ProjectileType<LegionPoseOmen>(), 0, 0f, Main.myPlayer,
                PackSource(npc), mode);
            if (omen < 0 || omen >= Main.maxProjectiles) {
                return false;
            }
            omenIndex = omen;
            //刹车脉冲：急停蓄势即前摇起手
            npc.velocity.X *= 0.2f;
            npc.netUpdate = true;
            movePhase = PhaseWindup;
            moveTimer = LegionPoseOmen.WindupFrames[mode];
            return true;
        }

        /// <summary>
        /// 地面突进三相推进（苦工/窃贼/血鲨陆上共用骨架，参数各异）。
        /// 前摇：姿态实体逐帧回读 + 离散刹车脉冲（脉冲帧才跟同步，镜像 NightPackNPC）；
        /// 执行：包络横速持有抵住原版行走 AI 的每帧改写（除回 MoveGain），纵向交原版重力；
        /// 后摇：衰减清残速，把控制权干净还给原版 AI
        /// </summary>
        private void TickGroundDash(NPC npc, int mode, float peak, int rise, int hold, int decay, int[] cooldownByTier) {
            if (movePhase == PhaseWindup) {
                moveTimer--;
                if (!BoundOmenValid(npc, ModContent.ProjectileType<LegionPoseOmen>())) {
                    AbortMove(RetryDelay);
                    return;
                }
                if (moveTimer == 12) {
                    npc.velocity.X *= 0.4f;
                    npc.netUpdate = true;
                }
                if (moveTimer <= 0) {
                    movePhase = PhaseStrike;
                    moveTimer = LegionPoseOmen.StrikeFrames[mode];
                    npc.netUpdate = true;
                }
                return;
            }
            if (movePhase == PhaseStrike) {
                moveTimer--;
                int elapsed = LegionPoseOmen.StrikeFrames[mode] - moveTimer;
                npc.velocity.X = lockSign * (peak / MoveGain(npc)) * MobDash.Envelope(elapsed, rise, hold, decay);
                if (elapsed == 1 || moveTimer % 6 == 0) {
                    npc.netUpdate = true;
                }
                if (moveTimer <= 0) {
                    movePhase = PhaseRecover;
                    moveTimer = 12;
                    npc.velocity.X *= 0.5f;
                    npc.netUpdate = true;
                }
                return;
            }
            moveTimer--;
            npc.velocity.X *= 0.75f;
            if (moveTimer <= 0) {
                npc.velocity.X = 0f;
                npc.netUpdate = true;
                FinishMove(cooldownByTier);
            }
        }
        #endregion

        #region 哥布林近战三怪
        /// <summary>苦工·盾肩撞：举盾前摇（压速 + 盾面微光实绘）→ 短距包络肩撞（锁向）→ 后摇清残速。
        /// 与战士的差异点：主动撞击 + 命中小击退（战士是被动盾墙减伤，braced 逻辑互不相触）</summary>
        private void PeonStep(NPC npc) {
            if (VaultUtils.isClient) {
                //决策只在权威端；客户端可见状态全在姿态实体与同步速度上
                return;
            }
            if (movePhase != PhaseIdle) {
                TickGroundDash(npc, LegionPoseOmen.ModePeon, PeonDashPeak, 5, 8, 12, PeonCooldownByTier);
                return;
            }
            if (!MoveReady(npc, out Player target)) {
                return;
            }
            //资格：立地、横距环带内、近似同层、有视线
            float dx = target.Center.X - npc.Center.X;
            if (npc.velocity.Y != 0f || Math.Abs(dx) < PeonEngageMin || Math.Abs(dx) > PeonEngageMax
                || Math.Abs(target.Center.Y - npc.Center.Y) > 140f
                || !Collision.CanHit(npc.position, npc.width, npc.height, target.position, target.width, target.height)) {
                moveCooldown = RetryDelay;
                return;
            }
            if (!StartGroundDash(npc, target, LegionPoseOmen.ModePeon)) {
                moveCooldown = RetryDelay;
            }
        }

        /// <summary>窃贼·挥刀偷袭：压低前摇 → 快扑（锁向包络）→ 命中挂得手旗（FleeBuff 原生同步
        /// 承载可见状态）→ ~120 帧逃离倾向；得手后被击杀由 OnKill 掉银币补偿。
        /// 与苦工的差异点：更快更短促的扑击 + 得手/逃离/返利闭环（玩家叫得出"它偷了我钱"）</summary>
        private void ThiefStep(NPC npc) {
            //逃离倾向：读已同步的 NPC buff，两端确定性同跑（镜像 TideStep 的零网络模式）。
            //不接管原版 AI：只在其本就背离玩家移动的帧上放大横向位移
            if (npc.HasBuff(FleeBuff)) {
                if (npc.HasValidTarget) {
                    Player t = Main.player[npc.target];
                    float away = npc.Center.X - t.Center.X;
                    if (npc.velocity.X != 0f && Math.Sign(npc.velocity.X) == Math.Sign(away)) {
                        Vector2 advance = new Vector2(npc.velocity.X * FleeGainBonus, 0f);
                        if (!npc.noTileCollide) {
                            advance = Collision.TileCollision(npc.position, advance, npc.width, npc.height);
                        }
                        npc.position += advance;
                    }
                }
                //得手金光：身上金币闪尘（客户端）
                if (!Main.dedServ && Main.rand.NextBool(3)) {
                    Dust coin = Dust.NewDustDirect(npc.position, npc.width, npc.height,
                        DustID.GoldCoin, 0f, -0.8f, 90, default, 1f);
                    coin.noGravity = true;
                }
            }
            if (VaultUtils.isClient) {
                return;
            }
            //得手闩：服务端看到 buff 即落闩（buff 会过期，补偿资格保留到死）
            if (!lootTaken && npc.HasBuff(FleeBuff)) {
                lootTaken = true;
            }
            if (movePhase != PhaseIdle) {
                TickGroundDash(npc, LegionPoseOmen.ModeThief, ThiefPouncePeak, 3, 7, 9, ThiefCooldownByTier);
                return;
            }
            if (!MoveReady(npc, out Player target)) {
                return;
            }
            float dx = target.Center.X - npc.Center.X;
            //得手期不再起扑（正在逃离），其余资格同苦工口径
            if (npc.HasBuff(FleeBuff) || npc.velocity.Y != 0f
                || Math.Abs(dx) < ThiefEngageMin || Math.Abs(dx) > ThiefEngageMax
                || Math.Abs(target.Center.Y - npc.Center.Y) > 140f
                || !Collision.CanHit(npc.position, npc.width, npc.height, target.position, target.width, target.height)) {
                moveCooldown = RetryDelay;
                return;
            }
            if (!StartGroundDash(npc, target, LegionPoseOmen.ModeThief)) {
                moveCooldown = RetryDelay;
            }
        }

        /// <summary>斥候·拉距点射：玩家进 ~200px 则小包络后跃脱离 → 立定 ≥30 帧瞄准线（锁向）→ 单发短矢。
        /// 与弓手的差异点：无军官依赖、单发点射、被贴脸主动拉开（弓手是军官庇护下的齐射箭幕）</summary>
        private void ScoutStep(NPC npc) {
            if (VaultUtils.isClient) {
                return;
            }
            if (movePhase == PhaseHop) {
                TickScoutHop(npc);
                return;
            }
            if (movePhase == PhaseWindup) {
                moveTimer--;
                //立定瞄准：压死横速（瞄准线实体在场即姿态可见）
                npc.velocity.X *= 0.6f;
                if (moveTimer % 6 == 0) {
                    npc.netUpdate = true;
                }
                //回读瞄准线：缺位=预告被打断，不出手回冷却（失败方向=安全方向）
                if (!BoundOmenValid(npc, ModContent.ProjectileType<LegionScoutOmen>())) {
                    AbortMove(RetryDelay);
                    return;
                }
                if (moveTimer <= 0) {
                    movePhase = PhaseRecover;
                    moveTimer = 12;
                }
                return;
            }
            if (movePhase == PhaseRecover) {
                moveTimer--;
                if (moveTimer <= 0) {
                    FinishMove(ScoutCooldownByTier);
                }
                return;
            }
            if (!MoveReady(npc, out Player target)) {
                return;
            }
            //触发：玩家进入贴身圈且斥候立地
            if (npc.velocity.Y != 0f || npc.Distance(target.Center) > ScoutPanicRange) {
                moveCooldown = RetryDelay;
                return;
            }
            //小包络后跃：横向背离玩家（除回 MoveGain）；上抬交原版重力（逃逸跳非承诺轨迹不除）
            lockSign = npc.Center.X >= target.Center.X ? 1f : -1f;
            npc.velocity = new Vector2(lockSign * ScoutHopVx / MoveGain(npc), ScoutHopVy);
            npc.netUpdate = true;
            movePhase = PhaseHop;
            moveTimer = ScoutHopFrames;
        }

        /// <summary>斥候后跃推进：横速走包络，落地即起瞄准线；迟迟不落地则放弃（安全回退）</summary>
        private void TickScoutHop(NPC npc) {
            moveTimer--;
            int elapsed = ScoutHopFrames - moveTimer;
            if (moveTimer > 0) {
                npc.velocity.X = lockSign * (ScoutHopVx / MoveGain(npc)) * MobDash.Envelope(elapsed, 3, 8, 11);
                if (elapsed == 1 || moveTimer % 6 == 0) {
                    npc.netUpdate = true;
                }
                return;
            }
            if (npc.velocity.Y != 0f) {
                if (moveTimer < -30) {
                    //跳下断崖等意外：放弃本次点射
                    AbortMove(RetryDelay);
                }
                return;
            }
            //落地起瞄准线：方向此刻锁死进 velocity（随生成包原生同步，预告即承诺）
            npc.velocity.X *= 0.2f;
            npc.netUpdate = true;
            if (!npc.HasValidTarget) {
                AbortMove(RetryDelay);
                return;
            }
            Player target = Main.player[npc.target];
            float dist = npc.Distance(target.Center);
            if (!target.Alives() || dist < ScoutAimMinRange || dist > ScoutAimMaxRange
                || !Collision.CanHitLine(npc.Center, 1, 1, target.position, target.width, target.height)) {
                AbortMove(RetryDelay);
                return;
            }
            //并发闸：瞄准线+在飞短矢合计超限则本次跳过
            if (CountActive(ModContent.ProjectileType<LegionScoutOmen>())
                + CountActive(ModContent.ProjectileType<LegionScoutBolt>()) >= ScoutGlobalCap) {
                AbortMove(40);
                return;
            }
            int damage = Math.Max(1, (int)(npc.damage * ScoutBoltDamageMult));
            int omen = Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center,
                npc.DirectionTo(target.Center), ModContent.ProjectileType<LegionScoutOmen>(),
                0, 0f, Main.myPlayer, PackSource(npc), damage);
            if (omen < 0 || omen >= Main.maxProjectiles) {
                AbortMove(RetryDelay);
                return;
            }
            omenIndex = omen;
            movePhase = PhaseWindup;
            moveTimer = LegionScoutOmen.TelegraphFrames;
        }
        #endregion

        #region 血月困难精英
        /// <summary>血月困难精英分发（权威端）：血鳗跃击 / 血鲨两栖 / 小丑滚弹 / 血乌贼墨汁</summary>
        private void EliteStep(NPC npc) {
            if (VaultUtils.isClient) {
                return;
            }
            switch (npc.type) {
                case NPCID.BloodEelHead:
                    EelStep(npc);
                    break;
                case NPCID.GoblinShark:
                    SharkStep(npc);
                    break;
                case NPCID.Clown:
                    ClownStep(npc);
                    break;
                case NPCID.BloodSquid:
                    SquidStep(npc);
                    break;
            }
        }

        /// <summary>自水下基准点向上寻水面：返回水面世界 Y（水柱顶沿），要求上方净空；
        /// 有界循环 + 世界边界防护（镜像 PumpkinMoonNPC.FindSurfaceY 的防护口径）</summary>
        private static bool TryFindWaterSurface(Vector2 from, out float surfaceY) {
            surfaceY = 0f;
            int tx = (int)(from.X / 16f);
            int ty = (int)(from.Y / 16f);
            const int MaxScanTiles = 12;
            for (int i = 0; i <= MaxScanTiles; i++) {
                int y = ty - i;
                if (tx < 10 || tx > Main.maxTilesX - 10 || y < 10) {
                    return false;
                }
                Tile tile = Framing.GetTileSafely(tx, y);
                if (tile.HasTile && Main.tileSolid[tile.TileType]) {
                    return false;//水体被物块封顶：无法破水
                }
                if (tile.LiquidAmount > 0) {
                    if (tile.LiquidType != LiquidID.Water) {
                        return false;//岩浆/蜂蜜不玩跃击
                    }
                    continue;
                }
                //首个无水格：其下沿即水面，再要求上方净空足够容纳跃弧
                for (int k = 1; k <= SurfAirClearTiles; k++) {
                    int ay = y - k;
                    if (ay < 10) {
                        return false;
                    }
                    Tile air = Framing.GetTileSafely(tx, ay);
                    if (air.LiquidAmount > 0 || (air.HasTile && Main.tileSolid[air.TileType])) {
                        return false;
                    }
                }
                surfaceY = (y + 1) * 16f;
                return true;
            }
            return false;//扫描上界内没找到水面（水太深，等它游上来再说）
        }

        /// <summary>起手破水预告：寻水面、锁破浪点与跃向、生成水面预告体（位置即承诺）。
        /// 破浪点按名义弧线预推横向漂移量，使警戒环与实际出水点贴合</summary>
        private bool StartSurfWindup(NPC npc, Player target, bool isShark) {
            if (!npc.wet || !TryFindWaterSurface(npc.Center, out float surfaceY)) {
                return false;
            }
            float depth = npc.Center.Y - surfaceY;
            if (depth < 0f || depth > SurfMaxDepth) {
                return false;//太浅不成弧、太深预告失真，都不起跳
            }
            if (CountActive(ModContent.ProjectileType<LegionSurfOmen>()) >= SurfGlobalCap) {
                return false;
            }
            lockSign = target.Center.X >= npc.Center.X ? 1f : -1f;
            float vx = isShark ? SharkLeapVx : EelLeapVx;
            float vy = isShark ? SharkLeapVy : EelLeapVy;
            //上升到水面的名义帧数 → 预推破浪点的横向漂移
            float riseFrames = depth / -vy;
            lockPoint = new Vector2(npc.Center.X + lockSign * vx * riseFrames, surfaceY);
            int omen = Projectile.NewProjectile(npc.GetSource_FromAI(), lockPoint, Vector2.Zero,
                ModContent.ProjectileType<LegionSurfOmen>(), 0, 0f, Main.myPlayer,
                PackSource(npc), isShark ? LegionSurfOmen.ModeSharkFoam : LegionSurfOmen.ModeEelRing, lockSign);
            if (omen < 0 || omen >= Main.maxProjectiles) {
                return false;
            }
            omenIndex = omen;
            npc.netUpdate = true;
            movePhase = PhaseWindup;
            moveTimer = LegionSurfOmen.TelegraphFrames;
            return true;
        }

        /// <summary>破水前摇推进：水下持续阻滞定身让出水点贴住警戒环（低频重推同步），
        /// 预告体缺位即中止；倒数尽头注入合成跃弧（初速与合成重力一并除回 MoveGain，
        /// 实现弧线=名义弧线，预告环兑现）</summary>
        private void TickSurfWindup(NPC npc, bool isShark) {
            moveTimer--;
            npc.velocity *= 0.82f;
            if (moveTimer % 6 == 0) {
                npc.netUpdate = true;
            }
            if (!BoundOmenValid(npc, ModContent.ProjectileType<LegionSurfOmen>())) {
                AbortMove(RetryDelay);
                return;
            }
            if (moveTimer > 0) {
                return;
            }
            float gain = MoveGain(npc);
            float vx = isShark ? SharkLeapVx : EelLeapVx;
            float vy = isShark ? SharkLeapVy : EelLeapVy;
            leapVel = new Vector2(lockSign * vx / gain, vy / gain);
            npc.velocity = leapVel;
            npc.netUpdate = true;
            leapExitedWater = false;
            movePhase = PhaseStrike;
            moveTimer = LeapMaxFrames;
        }

        /// <summary>跃行推进：持有合成弧线速度抵住原版蠕行/游动转向（弧线的升-顶-坠即包络），
        /// 再次入水或超时收段；血鳗未满三段则重新预告下一段</summary>
        private void TickLeap(NPC npc) {
            moveTimer--;
            leapVel.Y += LeapGravity / MoveGain(npc);
            npc.velocity = leapVel;
            if (moveTimer % 6 == 0) {
                npc.netUpdate = true;
            }
            if (!npc.wet) {
                leapExitedWater = true;
            }
            bool splashBack = leapExitedWater && npc.wet;
            if (!splashBack && moveTimer > 0) {
                return;
            }
            npc.velocity *= 0.5f;
            npc.netUpdate = true;
            if (npc.type == NPCID.BloodEelHead) {
                leapSegment++;
                if (leapSegment < EelLeapCount && npc.HasValidTarget
                    && Main.player[npc.target].Alives()
                    && StartSurfWindup(npc, Main.player[npc.target], isShark: false)) {
                    //下一段：重新预告（每段各自承诺）
                    return;
                }
            }
            movePhase = PhaseRecover;
            moveTimer = 16;
        }

        /// <summary>血鳗·破水跃击：水面警戒环 ≥34 帧（实体+来源校验）→ 跃出弧线穿环 → 回水收段，
        /// 三连跃每段重新预告；只挂头，体节随原版蠕虫链自然跟随</summary>
        private void EelStep(NPC npc) {
            if (movePhase == PhaseWindup) {
                TickSurfWindup(npc, isShark: false);
                return;
            }
            if (movePhase == PhaseStrike) {
                TickLeap(npc);
                return;
            }
            if (movePhase == PhaseRecover) {
                moveTimer--;
                npc.velocity *= 0.9f;
                if (moveTimer <= 0) {
                    FinishMove(EelCooldownByTier);
                }
                return;
            }
            if (!MoveReady(npc, out Player target)) {
                return;
            }
            if (npc.Distance(target.Center) > 900f) {
                moveCooldown = RetryDelay;
                return;
            }
            leapSegment = 0;
            if (!StartSurfWindup(npc, target, isShark: false)) {
                moveCooldown = RetryDelay;
            }
        }

        /// <summary>血鲨·两栖猎杀：陆上=龇牙前摇 ≥30 帧 + 包络突进；水中=水面泡沫痕 → 破浪跃咬。
        /// 与血鳗的差异点：单段跃咬 + 上岸还能追（血鳗只从水里打三连跃）</summary>
        private void SharkStep(NPC npc) {
            if (movePhase != PhaseIdle) {
                if (!aquaticMove) {
                    TickGroundDash(npc, LegionPoseOmen.ModeShark, SharkDashPeak, 6, 10, 14, SharkCooldownByTier);
                    return;
                }
                if (movePhase == PhaseWindup) {
                    TickSurfWindup(npc, isShark: true);
                }
                else if (movePhase == PhaseStrike) {
                    TickLeap(npc);
                }
                else {
                    moveTimer--;
                    npc.velocity *= 0.9f;
                    if (moveTimer <= 0) {
                        FinishMove(SharkCooldownByTier);
                    }
                }
                return;
            }
            if (!MoveReady(npc, out Player target)) {
                return;
            }
            if (npc.wet) {
                //水线：目标横距可及才起跃咬
                if (Math.Abs(target.Center.X - npc.Center.X) > SharkAquaMaxRangeX) {
                    moveCooldown = RetryDelay;
                    return;
                }
                aquaticMove = true;
                if (!StartSurfWindup(npc, target, isShark: true)) {
                    moveCooldown = RetryDelay;
                }
                return;
            }
            //陆线：立地、横距环带、近似同层、有视线
            float dx = target.Center.X - npc.Center.X;
            if (npc.velocity.Y != 0f || Math.Abs(dx) < SharkEngageMin || Math.Abs(dx) > SharkEngageMax
                || Math.Abs(target.Center.Y - npc.Center.Y) > 160f
                || !Collision.CanHit(npc.position, npc.width, npc.height, target.position, target.width, target.height)) {
                moveCooldown = RetryDelay;
                return;
            }
            aquaticMove = false;
            if (!StartGroundDash(npc, target, LegionPoseOmen.ModeShark)) {
                moveCooldown = RetryDelay;
            }
        }

        /// <summary>小丑·滚动炸弹：抬手前摇 ≥24 帧（姿态实体）→ 掷出滚地炸弹
        /// （原版炸弹贴图+引线火花+爆前警示环+可被玩家弹幕打哑火）。掷点前摇起手即锁</summary>
        private void ClownStep(NPC npc) {
            if (movePhase == PhaseWindup) {
                moveTimer--;
                npc.velocity.X *= 0.8f;
                if (moveTimer % 6 == 0) {
                    npc.netUpdate = true;
                }
                if (!BoundOmenValid(npc, ModContent.ProjectileType<LegionPoseOmen>())) {
                    AbortMove(RetryDelay);
                    return;
                }
                if (moveTimer <= 0) {
                    ThrowBomb(npc);
                    movePhase = PhaseRecover;
                    moveTimer = LegionPoseOmen.StrikeFrames[LegionPoseOmen.ModeClown];
                }
                return;
            }
            if (movePhase == PhaseRecover) {
                moveTimer--;
                if (moveTimer <= 0) {
                    FinishMove(ClownCooldownByTier);
                }
                return;
            }
            if (!MoveReady(npc, out Player target)) {
                return;
            }
            float dx = Math.Abs(target.Center.X - npc.Center.X);
            if (npc.velocity.Y != 0f || dx < ClownThrowMin || dx > ClownThrowMax
                || Math.Abs(target.Center.Y - npc.Center.Y) > 260f
                || !Collision.CanHit(npc.position, npc.width, npc.height, target.position, target.width, target.height)) {
                moveCooldown = RetryDelay;
                return;
            }
            if (CountActive(ModContent.ProjectileType<LegionClownBomb>()) >= BombGlobalCap) {
                moveCooldown = 40;
                return;
            }
            //掷点此刻锁死（预告即承诺，出手不再追瞄）
            lockPoint = target.Center;
            int omen = Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, Vector2.Zero,
                ModContent.ProjectileType<LegionPoseOmen>(), 0, 0f, Main.myPlayer,
                PackSource(npc), LegionPoseOmen.ModeClown);
            if (omen < 0 || omen >= Main.maxProjectiles) {
                moveCooldown = RetryDelay;
                return;
            }
            omenIndex = omen;
            npc.velocity.X *= 0.2f;
            npc.netUpdate = true;
            movePhase = PhaseWindup;
            moveTimer = LegionPoseOmen.WindupFrames[LegionPoseOmen.ModeClown];
        }

        /// <summary>掷弹：向锁定点做定时长抛物解算。弹幕不吃 NPC 提速层，不除 MoveGain；
        /// 重力补偿项引用炸弹自身的 <see cref="LegionClownBomb.BombGravity"/> 保持同源</summary>
        private void ThrowBomb(NPC npc) {
            Vector2 from = npc.Center + new Vector2(npc.direction * 8f, -12f);
            Vector2 to = lockPoint - from;
            Vector2 vel = new Vector2(to.X / BombFlightFrames,
                to.Y / BombFlightFrames - LegionClownBomb.BombGravity * BombFlightFrames * 0.5f);
            int damage = Math.Max(1, (int)(npc.damage * BombDamageMult));
            Projectile.NewProjectile(npc.GetSource_FromAI(), from, vel,
                ModContent.ProjectileType<LegionClownBomb>(), damage, 2f, Main.myPlayer);
        }

        /// <summary>血乌贼·墨汁三连：短前摇凝墨（预告实体跟身，瞄向即锁）→ 三发弧线血墨，
        /// 第 <see cref="LegionInkOmen.InkGapIndex"/> 发固定跳过（具名节奏缺口）。发射由预告体到期执行</summary>
        private void SquidStep(NPC npc) {
            if (movePhase == PhaseWindup) {
                moveTimer--;
                //凝墨定身：飞行怪漂移大，持续阻滞 + 预告体跟身补视觉
                npc.velocity *= 0.85f;
                if (moveTimer % 6 == 0) {
                    npc.netUpdate = true;
                }
                if (!BoundOmenValid(npc, ModContent.ProjectileType<LegionInkOmen>())) {
                    AbortMove(RetryDelay);
                    return;
                }
                if (moveTimer <= 0) {
                    movePhase = PhaseRecover;
                    moveTimer = 14;
                }
                return;
            }
            if (movePhase == PhaseRecover) {
                moveTimer--;
                if (moveTimer <= 0) {
                    FinishMove(SquidCooldownByTier);
                }
                return;
            }
            if (!MoveReady(npc, out Player target)) {
                return;
            }
            float dist = npc.Distance(target.Center);
            if (dist < SquidMinRange || dist > SquidMaxRange
                || !Collision.CanHitLine(npc.Center, 1, 1, target.position, target.width, target.height)) {
                moveCooldown = RetryDelay;
                return;
            }
            if (CountActive(ModContent.ProjectileType<LegionInkOmen>())
                + CountActive(ModContent.ProjectileType<LegionInkGlob>()) >= InkGlobalCap) {
                moveCooldown = 40;
                return;
            }
            int damage = Math.Max(1, (int)(npc.damage * InkDamageMult));
            //瞄向此刻锁死（随生成包原生同步，预告即承诺）
            int omen = Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, Vector2.Zero,
                ModContent.ProjectileType<LegionInkOmen>(), 0, 0f, Main.myPlayer,
                PackSource(npc), npc.DirectionTo(target.Center).ToRotation(), damage);
            if (omen < 0 || omen >= Main.maxProjectiles) {
                moveCooldown = RetryDelay;
                return;
            }
            omenIndex = omen;
            npc.velocity *= 0.5f;
            npc.netUpdate = true;
            movePhase = PhaseWindup;
            moveTimer = LegionInkOmen.TelegraphFrames;
        }
        #endregion

        #region 命中与击杀结算
        public override void OnHitPlayer(NPC npc, Player target, Player.HurtInfo hurtInfo) {
            if (!MechanicActive(npc)) {
                return;
            }
            //受击者本机结算（玩家接触判定在受害端跑）；突进窗由已同步的姿态实体判定，
            //不读服务端私产相位计时器（镜像 NightPackNPC 的判窗口径）
            if (npc.type == NPCID.GoblinPeon
                && LegionPoseOmen.IsStrikeWindowFor(npc.whoAmI, LegionPoseOmen.ModePeon)) {
                //盾肩撞差异点：小击退（玩家速度改动走原生自同步）
                float dir = Math.Sign(target.Center.X - npc.Center.X);
                if (dir == 0f) {
                    dir = npc.direction;
                }
                target.velocity.X += dir * PeonShoveKickX;
                if (target.velocity.Y == 0f) {
                    target.velocity.Y = PeonShoveKickY;
                }
            }
            else if (npc.type == NPCID.GoblinThief
                && LegionPoseOmen.IsStrikeWindowFor(npc.whoAmI, LegionPoseOmen.ModeThief)
                && !npc.HasBuff(FleeBuff)) {
                //偷袭得手：挂得手旗（受击端 AddBuff 发原版 AddNPCBuff 包，服务端应用后广播，
                //镜像 WardBuff 的载体口径）。"偷"是演出+击杀返利：不真动玩家钱包，
                //服务器写不了客户端背包（联机口径）
                npc.AddBuff(FleeBuff, FleeFrames);
                if (!Main.dedServ) {
                    SoundEngine.PlaySound(SoundID.Coins with { Volume = 0.6f }, npc.Center);
                    for (int i = 0; i < 8; i++) {
                        Dust coin = Dust.NewDustDirect(npc.position, npc.width, npc.height,
                            DustID.GoldCoin, 0f, -1f, 80, default, Main.rand.NextFloat(0.9f, 1.3f));
                        coin.noGravity = true;
                        coin.velocity = Main.rand.NextVector2Circular(2f, 1.5f) - new Vector2(0f, 1f);
                    }
                }
            }
        }

        public override void OnKill(NPC npc) {
            //OnKill 本就只在权威端触发，双保险再拦一次（镜像 EvilBiomeMobsNPC）
            if (boundTier <= 0 || VaultUtils.isClient || npc.type != NPCID.GoblinThief || !lootTaken) {
                return;
            }
            //得手补偿：吐出小钱袋（世界掉落物原生同步；只有得手闩真时掉）
            Item.NewItem(npc.GetSource_Loot(), npc.getRect(), ItemID.SilverCoin, LootCoinsByTier[boundTier - 1]);
        }
        #endregion
    }
}
