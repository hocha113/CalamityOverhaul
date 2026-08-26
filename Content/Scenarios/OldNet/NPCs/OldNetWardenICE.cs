using CalamityOverhaul.Common;
using CalamityOverhaul.Content.LegendWeapon.SHPCLegend;
using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules;
using CalamityOverhaul.Content.RAMSystems;
using CalamityOverhaul.Content.Scenarios.OldNet.Backgrounds;
using CalamityOverhaul.Content.Scenarios.OldNet.Gen;
using CalamityOverhaul.Content.UIs.UIEffect;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.OldNet.NPCs
{
    /// <summary>
    /// 回收官：整张网的免疫应答收束成的处决单元，T4 清剿波持续 45s 后升格派遣
    /// （每潜一次，OldNetICEDirector.DispatchWarden）。三相 shuffle-bag 状态机：
    /// P1 断言冲锋/协议齐射/字形雨压制，P2 追加双段冲锋与吞噬牵引，P3 处决姿态
    /// （雨幕缝缩 1、终末协议走精英池）。每状态均有完成路径+超时兜底，无离场态：
    /// 玩家离世由 OldNetWorld.Active 门控自杀兜底。
    /// 击杀奖励：碎片喷付 16 + 全网静默（SilenceNoise 直落 30 + 静默余量增量减半），
    /// 全家族唯一的主动降噪回路。
    /// 视觉：CPU 全保真（SvgPathPen 纹章骨架 + 甲片编队 + 遥测前摇），
    /// shader 富层（纹章环/核心独目）由 OldNetWardenRender 消费 OldNetWarden.fx。
    /// TODO MP: 袋序列/相位游标/甲片编队为实例字段，联机化走 SendExtraAI
    /// </summary>
    internal class OldNetWardenICE : ModNPC
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        //ai[0] 状态位
        private const int StateEntrance = 0;
        private const int StateSelect = 1;
        private const int StateDash = 2;
        private const int StateVolley = 3;
        private const int StateRain = 4;
        private const int StatePull = 5;
        private const int StateExecute = 6;
        private const int StatePhaseShift = 7;
        private const int StateChase = 8;
        private const int StateDeath = 9;

        //shuffle-bag 招式 id
        private const int AtkDash = 0;
        private const int AtkVolley = 1;
        private const int AtkRain = 2;
        private const int AtkPull = 3;
        private const int AtkExecute = 4;

        /// <summary>ai[0]：状态</summary>
        private ref float State => ref NPC.ai[0];
        /// <summary>ai[1]：状态内计时</summary>
        private ref float StateTimer => ref NPC.ai[1];
        /// <summary>ai[3]：状态内子游标（冲锋轮次/齐射发数/追近子相）</summary>
        private ref float SubIndex => ref NPC.ai[3];

        //════════ 实例状态（M1 单人语义；TODO MP: 联机化走 SendExtraAI）════════

        //shuffle-bag：袋内每招一次、禁连发同招、上袋末招不进下袋首位
        private readonly List<int> bag = [];
        private int lastAttack = -1;
        //首轮公平阀：前 3 招（首袋）前摇 ×1.5、冲速 ×0.75
        private int attacksDone;
        //已应用的相位（换相检测游标）
        private int appliedPhase = 1;
        //冲锋方向
        private Vector2 dashDir;
        //字形雨：投放面中心/高度/缝位
        private float rainPlaneX;
        private float rainPlaneY;
        private readonly float[] gapCenters = new float[2];
        private readonly float[] gapDrift = new float[2];
        private int gapCount = 2;
        //死亡全弧：警鸣游标与倒数
        private int deathBeepIndex;
        private float deathBeepTimer;
        private bool deathArcDone;

        //甲片编队：受击弹开重排=质量反馈；P3 逐片脱轨；死亡逐拍弹射
        private struct WardenPlate
        {
            internal float BaseAngle;
            internal float Radius;
            internal Vector2 Jitter;
            internal Vector2 JitterVel;
            internal bool Detached;
            internal Vector2 Pos;
            internal Vector2 Vel;
            internal float Spin;
        }
        private readonly WardenPlate[] plates = new WardenPlate[10];
        private bool platesInit;
        private float plateOrbit;

        //════════ 渲染通道（OldNetWardenRender 与 PreDraw 共同消费）════════

        /// <summary>失血比 0..1（shader uDecay：降解侵蚀）</summary>
        internal float RenderDecay => 1f - MathHelper.Clamp(NPC.life / (float)NPC.lifeMax, 0f, 1f);
        /// <summary>当前招式充能 0..1（前摇可读层）</summary>
        internal float RenderCharge { get; private set; }
        /// <summary>入场/演出透明度</summary>
        internal float RenderAlpha { get; private set; }
        /// <summary>入场假纵深缩放 0.4 → 1</summary>
        internal float RenderScale { get; private set; } = 0.4f;
        /// <summary>纹章环累计相位（充能期加速旋转）</summary>
        internal float RingSpin { get; private set; }
        /// <summary>终末协议全屏红沿脉冲 0..1（EndEntityDraw 层）</summary>
        internal float EdgePulse { get; private set; }
        /// <summary>死亡 impact-frame 全屏白闪 0..1</summary>
        internal float WhiteFlash { get; private set; }

        private float Seed => NPC.whoAmI * 0.477f;
        private int Phase => NPC.life > NPC.lifeMax * OldNetMetrics.WardenP2LifeFrac ? 1
            : NPC.life > NPC.lifeMax * OldNetMetrics.WardenP3LifeFrac ? 2 : 3;
        //首袋公平阀：换场后首轮攻击速度 50%（前摇更长、冲速更缓）
        private float PaceMul => attacksDone < 3 ? 1.5f : 1f;
        private float SpeedMul => attacksDone < 3 ? 0.75f : 1f;

        public override void SetStaticDefaults() {
            Main.npcFrameCount[Type] = 1;
            NPCID.Sets.NPCBestiaryDrawModifiers hide = new() { Hide = true };
            NPCID.Sets.NPCBestiaryDrawOffset.Add(Type, hide);
            NPCID.Sets.MustAlwaysDraw[Type] = true;
        }

        public override void SetDefaults() {
            NPC.width = 64;
            NPC.height = 64;
            //平时零伤害，仅冲锋/贴身弹开窗口设回（Wraith 门控惯例）
            NPC.damage = 0;
            NPC.defense = OldNetMetrics.WardenDefense;
            NPC.lifeMax = OldNetMetrics.WardenLife;
            NPC.knockBackResist = 0f;
            NPC.aiStyle = -1;
            NPC.noGravity = true;
            NPC.noTileCollide = true;
            NPC.value = 0;
            NPC.npcSlots = 3f;
            NPC.HitSound = SoundID.NPCHit4;
            NPC.DeathSound = SoundID.NPCDeath14;
        }

        //它永不主动离场：不参与原版远离 despawn（门控自杀兜底）
        public override bool CheckActive() => false;

        public override void AI() {
            //旧网门控：绝不泄漏到主世界与其他子世界
            if (!OldNetWorld.Active) {
                NPC.active = false;
                return;
            }
            InitPlates();
            //在场即降解：每帧向氛围故障贡献位续写 max（通道自衰减，客户端演出）
            if (!Main.dedServ) {
                OldNetLinkFX.ExternalGlitch01 = MathF.Max(OldNetLinkFX.ExternalGlitch01,
                    0.06f + 0.10f * RenderDecay);
            }
            //纹章环转速随充能升档
            RingSpin += 0.004f + RenderCharge * 0.03f;
            EdgePulse = MathF.Max(0f, EdgePulse - 0.02f);
            WhiteFlash = MathF.Max(0f, WhiteFlash - 0.09f);
            UpdatePlates();

            if ((int)State == StateDeath) {
                UpdateDeathArc();
                return;
            }

            NPC.TargetClosest(faceTarget: false);
            Player player = Main.player[NPC.target];
            bool hasTarget = player != null && player.active && !player.dead;
            if (!hasTarget) {
                //目标缺席（死亡/弹出过渡帧）：悬停待机，世界门控随后收尾
                NPC.damage = 0;
                NPC.velocity *= 0.95f;
                return;
            }

            switch ((int)State) {
                case StateSelect:
                    UpdateSelect(player);
                    break;
                case StateDash:
                    UpdateDash(player);
                    break;
                case StateVolley:
                    UpdateVolley(player);
                    break;
                case StateRain:
                    UpdateRain(player);
                    break;
                case StatePull:
                    UpdatePull(player);
                    break;
                case StateExecute:
                    UpdateExecute(player);
                    break;
                case StatePhaseShift:
                    UpdatePhaseShift();
                    break;
                case StateChase:
                    UpdateChase(player);
                    break;
                default:
                    UpdateEntrance(player);
                    break;
            }

            //处决单元的在场压迫光
            Lighting.AddLight(NPC.Center, 0.5f, 0.1f, 0.08f);
        }

        //──── 入场：黑墙侧滑入（假纵深）→ 静止威压 ────

        private void UpdateEntrance(Player player) {
            NPC.damage = 0;
            StateTimer++;
            //入场首帧一次性 glitch 尖峰：写一次，靠通道自衰减出拖尾
            if (StateTimer == 1f && !Main.dedServ) {
                OldNetLinkFX.ExternalGlitch01 = MathF.Max(OldNetLinkFX.ExternalGlitch01, 0.7f);
            }
            if (StateTimer <= 60f) {
                float frac = StateTimer / 60f;
                //缓动放大 = 从景深里驶出来；透明度托底防出生隐形
                float ease = 1f - (1f - frac) * (1f - frac);
                RenderScale = MathHelper.Lerp(0.4f, 1f, ease);
                RenderAlpha = MathF.Max(0.15f, ease);
                //滑向玩家入场站位环
                Vector2 stand = player.Center
                    + (NPC.Center - player.Center).SafeNormalize(-Vector2.UnitX)
                    * OldNetMetrics.WardenEntranceStandoff;
                NPC.velocity = Vector2.Lerp(NPC.velocity, (stand - NPC.Center) * 0.04f, 0.1f);
            }
            else {
                //威压=静止：入位后一动不动面向玩家 60t
                RenderScale = 1f;
                RenderAlpha = 1f;
                NPC.velocity *= 0.85f;
            }
            NPC.direction = NPC.spriteDirection = player.Center.X >= NPC.Center.X ? 1 : -1;

            if (StateTimer == 61f && !Main.dedServ) {
                SoundEngine.PlaySound(CWRSound.Hacker with { Volume = 0.9f, Pitch = -0.8f }, NPC.Center);
            }
            if (StateTimer >= 120f) {
                EnterSelect();
            }
        }

        //──── 选招连接拍：站位整理 + shuffle-bag 出招 ────

        private void EnterSelect() {
            State = StateSelect;
            StateTimer = 0f;
            SubIndex = 0f;
            RenderCharge = 0f;
            //入战即全显：绕过入场态的生成路径（调试指令）也不会隐形
            RenderAlpha = 1f;
            RenderScale = 1f;
            NPC.damage = 0;
            NPC.netUpdate = true;
        }

        private void UpdateSelect(Player player) {
            //换相优先：跨过相位阈值先走换相硬直（含弹幕清场公平阀）
            if (Phase != appliedPhase) {
                appliedPhase = Phase;
                State = StatePhaseShift;
                StateTimer = 0f;
                ClearGlyphBolts();
                PopAllPlates(4f);
                NPC.netUpdate = true;
                if (!Main.dedServ) {
                    SoundEngine.PlaySound(CWRSound.Fault with { Volume = 0.85f, Pitch = -0.5f }, NPC.Center);
                }
                return;
            }

            MaintainStandoff(player, OldNetMetrics.WardenSelectStandoff, 0.05f);
            NPC.direction = NPC.spriteDirection = player.Center.X >= NPC.Center.X ? 1 : -1;

            if (++StateTimer < 24f) {
                return;
            }
            //拉太远：长距贯穿追近（位移式，不闪现）
            if (Vector2.Distance(player.Center, NPC.Center) > OldNetMetrics.WardenChaseRange) {
                State = StateChase;
                StateTimer = 0f;
                SubIndex = 0f;
                NPC.netUpdate = true;
                return;
            }
            int pick = RollAttack();
            attacksDone++;
            State = pick switch {
                AtkVolley => StateVolley,
                AtkRain => StateRain,
                AtkPull => StatePull,
                AtkExecute => StateExecute,
                _ => StateDash,
            };
            StateTimer = 0f;
            SubIndex = 0f;
            NPC.netUpdate = true;
        }

        //站位纪律：向玩家 preferred 距离的环位缓漂（钳进 [Min,Max] 站位带）
        private void MaintainStandoff(Player player, float preferred, float rate) {
            preferred = MathHelper.Clamp(preferred,
                OldNetMetrics.WardenStandoffMin, OldNetMetrics.WardenStandoffMax);
            Vector2 stand = player.Center
                + (NPC.Center - player.Center).SafeNormalize(-Vector2.UnitX) * preferred
                + new Vector2(0f, -OldNetMetrics.WardenHoverLift);
            NPC.velocity = Vector2.Lerp(NPC.velocity, (stand - NPC.Center) * rate, 0.08f);
        }

        //──── shuffle-bag：袋内每招一次、禁连发、上袋末招不进下袋首位 ────

        private int RollAttack() {
            if (bag.Count == 0) {
                RebuildBag();
            }
            int pick = bag[0];
            bag.RemoveAt(0);
            lastAttack = pick;
            return pick;
        }

        private void RebuildBag() {
            int[] pool = Phase switch {
                1 => [AtkDash, AtkVolley, AtkRain],
                2 => [AtkDash, AtkVolley, AtkRain, AtkPull],
                _ => [AtkDash, AtkRain, AtkExecute],
            };
            bag.Clear();
            bag.AddRange(pool);
            //Fisher-Yates 洗牌
            for (int i = bag.Count - 1; i > 0; i--) {
                int j = Main.rand.Next(i + 1);
                (bag[i], bag[j]) = (bag[j], bag[i]);
            }
            //抗重复：新袋首位不得等于上袋末招
            if (bag.Count > 1 && bag[0] == lastAttack) {
                (bag[0], bag[1]) = (bag[1], bag[0]);
            }
        }

        //──── A/A' 断言冲锋：反向蓄力 → 极速直线 → 硬刹过冲 ────

        private void UpdateDash(Player player) {
            int cycle = (int)SubIndex;
            //P2+ 双段变奏：二段蓄力缩至 60%（学过的节拍被变奏）
            float ant = OldNetMetrics.WardenDashAnticipationTicks * PaceMul
                * (cycle >= 1 && Phase >= 2 ? 0.6f : 1f);
            float dashEnd = ant + OldNetMetrics.WardenDashTicks;
            float brakeEnd = dashEnd + OldNetMetrics.WardenDashBrakeTicks;

            StateTimer++;
            if (StateTimer < ant) {
                NPC.damage = 0;
                //最小起手距离阀：贴脸不蓄力，先退开
                float dist = Vector2.Distance(player.Center, NPC.Center);
                Vector2 away = (NPC.Center - player.Center).SafeNormalize(Vector2.UnitX);
                if (dist < OldNetMetrics.WardenStandoffMin) {
                    NPC.velocity = Vector2.Lerp(NPC.velocity, away * 6f, 0.1f);
                    //距离不合法不计时且倒扣（顶帧 ++ 后净 -1）：僵持 180t 兜底收招
                    StateTimer -= 2f;
                    if (StateTimer < -180f) {
                        EnterSelect();
                    }
                    return;
                }
                //反向蓄力漂移：velocity 渐停 + 末段急促后吸（late-snap 反动作）
                //clamp 防蓄力僵持倒扣出的负值流进 uCharge/RingSpin
                float frac = MathHelper.Clamp(StateTimer / ant, 0f, 1f);
                NPC.velocity = Vector2.Lerp(NPC.velocity * 0.9f,
                    away * 4f * frac * frac * frac * frac, 0.12f);
                RenderCharge = frac;
                NPC.direction = NPC.spriteDirection = player.Center.X >= NPC.Center.X ? 1 : -1;
                //固定 36t 节拍警鸣：可学习的鼓点
                if ((int)StateTimer == (int)(ant - OldNetMetrics.WardenDashBeepLead) && !Main.dedServ) {
                    SoundEngine.PlaySound(SoundID.MenuTick with { Volume = 0.9f, Pitch = -0.5f }, NPC.Center);
                }
                //冲刺矢量在蓄力末段锁定（committed，玩家可读线）
                dashDir = (player.Center + player.velocity * 12f - NPC.Center)
                    .SafeNormalize(Vector2.UnitX * NPC.direction);
                return;
            }
            if (StateTimer < dashEnd) {
                //发射是一次性置值不是渐加速；接触窗口唯一开启
                if (StateTimer - ant < 1f) {
                    NPC.velocity = dashDir * OldNetMetrics.WardenDashSpeed * SpeedMul;
                    NPC.damage = OldNetMetrics.WardenContactDamage;
                    RenderCharge = 0f;
                    if (!Main.dedServ) {
                        SoundEngine.PlaySound(SoundID.Item74 with { Volume = 0.7f, Pitch = -0.3f }, NPC.Center);
                        Main.LocalPlayer.CWR().GetScreenShake(4f);
                    }
                }
                return;
            }
            if (StateTimer < brakeEnd) {
                //硬刹过冲：惩罚窗口
                NPC.damage = 0;
                NPC.velocity *= 0.65f;
                return;
            }
            //下一轮或收招（×2 循环）
            SubIndex++;
            StateTimer = 0f;
            if (SubIndex >= 2f) {
                EnterSelect();
            }
        }

        //──── B 协议齐射：站定 3 连施放骇入协议（压 UI/RAM 不压走位）────

        private void UpdateVolley(Player player) {
            float interval = OldNetMetrics.WardenVolleyIntervalTicks * PaceMul;
            float start = 60f * PaceMul;
            NPC.damage = 0;
            MaintainStandoff(player, OldNetMetrics.WardenVolleyStandoff, 0.03f);
            NPC.direction = NPC.spriteDirection = player.Center.X >= NPC.Center.X ? 1 : -1;

            StateTimer++;
            int shot = (int)SubIndex;
            float fireAt = start + shot * interval;
            //红弧前摇：每发前 30t 充能可读
            RenderCharge = MathHelper.Clamp(1f - (fireAt - StateTimer) / 30f, 0f, 1f);

            if (StateTimer >= fireAt && shot < OldNetMetrics.WardenVolleyCount) {
                SubIndex++;
                RenderCharge = 0f;
                //协议桥内建单人门禁（TryCast 自拒非单人）
                OldNetHostileHack.TryCast(player,
                    OldNetHostileHack.PickForTier(4, elite: false), NPC.TypeName);
                if (!Main.dedServ) {
                    SoundEngine.PlaySound(CWRSound.Hacker with { Volume = 0.6f, Pitch = -0.2f }, NPC.Center);
                }
            }
            //完成或超时兜底
            if (SubIndex >= OldNetMetrics.WardenVolleyCount || StateTimer > start + interval * 3.5f) {
                EnterSelect();
            }
        }

        //──── C/C' 字形雨压制：头顶投放面 + 安全缝（P3 缝缩 1 且缓漂）────

        private void UpdateRain(Player player) {
            float deploy = 30f * PaceMul;
            NPC.damage = 0;
            MaintainStandoff(player, OldNetMetrics.WardenRainStandoff, 0.03f);
            NPC.direction = NPC.spriteDirection = player.Center.X >= NPC.Center.X ? 1 : -1;

            StateTimer++;
            if (StateTimer < deploy) {
                //部署期：定面、掷缝、亮标
                if (StateTimer < 2f) {
                    rainPlaneX = player.Center.X;
                    rainPlaneY = player.Center.Y - 420f;
                    gapCount = Phase >= 3 ? 1 : 2;
                    float halfW = OldNetMetrics.WardenRainWidth * 0.5f - 80f;
                    if (gapCount > 1) {
                        //双缝各掷面心一侧半区：两个逃生选项的间距
                        //≥2×HalfZoneMin 由构造保证，不靠重掷碰运气
                        gapCenters[0] = rainPlaneX
                            - Main.rand.NextFloat(OldNetMetrics.WardenRainGapHalfZoneMin, halfW);
                        gapCenters[1] = rainPlaneX
                            + Main.rand.NextFloat(OldNetMetrics.WardenRainGapHalfZoneMin, halfW);
                    }
                    else {
                        gapCenters[0] = rainPlaneX + Main.rand.NextFloat(-halfW, halfW);
                        gapCenters[1] = gapCenters[0];
                    }
                    //P3 缝缓漂 ±0.5px/f
                    gapDrift[0] = Phase >= 3 ? (Main.rand.NextBool() ? 0.5f : -0.5f) : 0f;
                    gapDrift[1] = 0f;
                    if (!Main.dedServ) {
                        SoundEngine.PlaySound(SoundID.DD2_EtherianPortalOpen with {
                            Volume = 0.5f, Pitch = 0.4f
                        }, player.Center);
                    }
                }
                RenderCharge = StateTimer / deploy;
                return;
            }

            //缝缓漂（P3）：撞边反弹
            float bound = OldNetMetrics.WardenRainWidth * 0.5f - 60f;
            for (int g = 0; g < gapCount; g++) {
                gapCenters[g] += gapDrift[g];
                if (MathF.Abs(gapCenters[g] - rainPlaneX) > bound) {
                    gapDrift[g] = -gapDrift[g];
                }
            }
            RenderCharge = 0f;

            //投放：每 5t 一发，共 24 发；避开安全缝
            int rainTick = (int)(StateTimer - deploy);
            if (rainTick % 5 == 0 && rainTick / 5 < OldNetMetrics.WardenRainBoltCount) {
                //权威端生成（镜像 TurretBolt 惯例）
                if (!VaultUtils.isClient) {
                    float gapHalf = OldNetMetrics.WardenRainGapTiles * 16f * 0.5f + 12f;
                    for (int attempt = 0; attempt < 8; attempt++) {
                        float x = rainPlaneX + Main.rand.NextFloat(-0.5f, 0.5f) * OldNetMetrics.WardenRainWidth;
                        bool inGap = false;
                        for (int g = 0; g < gapCount; g++) {
                            if (MathF.Abs(x - gapCenters[g]) < gapHalf) {
                                inGap = true;
                                break;
                            }
                        }
                        if (inGap) {
                            continue;
                        }
                        Projectile.NewProjectile(NPC.GetSource_FromAI(),
                            new Vector2(x, rainPlaneY),
                            new Vector2(0f, OldNetMetrics.WardenGlyphFallSpeed),
                            ModContent.ProjectileType<OldNetWardenGlyphBolt>(),
                            OldNetMetrics.WardenGlyphDamage, 1f, Main.myPlayer,
                            ai0: Main.rand.NextFloat(10f));
                        break;
                    }
                }
                if (rainTick % 15 == 0 && !Main.dedServ) {
                    SoundEngine.PlaySound(SoundID.Item12 with { Volume = 0.3f, Pitch = 0.5f }, NPC.Center);
                }
            }

            //收招：投放完 + 幕落净余量
            if (StateTimer >= deploy + OldNetMetrics.WardenRainBoltCount * 5f + 40f) {
                EnterSelect();
            }
        }

        //──── D 吞噬牵引（P2+）：真空拉扯 + 核心外露 ×2 受伤 ────

        private void UpdatePull(Player player) {
            StateTimer++;
            if (StateTimer < 30f) {
                //收束前摇
                NPC.damage = 0;
                NPC.velocity *= 0.9f;
                RenderCharge = StateTimer / 30f;
                if (StateTimer == 1f && !Main.dedServ) {
                    SoundEngine.PlaySound(SoundID.DD2_EtherianPortalIdleLoop with {
                        Volume = 0.7f, Pitch = -0.6f
                    }, NPC.Center);
                }
                return;
            }
            RenderCharge = 1f;
            NPC.velocity *= 0.92f;

            float dist = Vector2.Distance(player.Center, NPC.Center);
            if (dist < OldNetMetrics.WardenPullRadius) {
                //TODO MP: 牵引施加于目标玩家为 per-player 语义，联机化广播力场
                player.velocity += (NPC.Center - player.Center).SafeNormalize(Vector2.Zero)
                    * OldNetMetrics.WardenPullAccel;
            }
            //被拉贴身=30 伤弹开窗口；其余时刻核心只挨打不咬人
            NPC.damage = dist < 80f ? OldNetMetrics.WardenPullTouchDamage : 0;

            if (StateTimer >= 30f + OldNetMetrics.WardenPullTicks) {
                NPC.damage = 0;
                EnterSelect();
            }
        }

        //核心外露：牵引期受伤 ×2（顶着拉力反冲进脸=最大 DPS 窗口）
        public override void ModifyIncomingHit(ref NPC.HitModifiers modifiers) {
            if ((int)State == StatePull) {
                modifiers.FinalDamage *= 2f;
            }
        }

        //──── E 终末协议（P3）：90t 读秒 + 全屏红沿 → 精英池施放 ────

        private void UpdateExecute(Player player) {
            NPC.damage = 0;
            NPC.velocity *= 0.9f;
            NPC.direction = NPC.spriteDirection = player.Center.X >= NPC.Center.X ? 1 : -1;

            StateTimer++;
            float frac = MathHelper.Clamp(StateTimer / OldNetMetrics.WardenExecuteTelegraphTicks, 0f, 1f);
            RenderCharge = frac;
            EdgePulse = MathF.Max(EdgePulse, frac * 0.85f);
            //读秒渐密警鸣
            int beepGap = (int)MathHelper.Lerp(24f, 6f, frac);
            if ((int)StateTimer % Math.Max(beepGap, 1) == 0 && !Main.dedServ) {
                SoundEngine.PlaySound(SoundID.MenuTick with {
                    Volume = 0.6f, Pitch = -0.3f + frac * 0.9f
                }, NPC.Center);
            }

            if (StateTimer == OldNetMetrics.WardenExecuteTelegraphTicks) {
                //终局狠度沿用协议桥既定分配（1/3 概率 MeltdownBrand），不新增协议
                OldNetHostileHack.TryCast(player,
                    OldNetHostileHack.PickForTier(4, elite: true), NPC.TypeName);
                RenderCharge = 0f;
                if (!Main.dedServ) {
                    SoundEngine.PlaySound(CWRSound.Fault with { Volume = 1f, Pitch = -0.2f }, NPC.Center);
                    Main.LocalPlayer.CWR().GetScreenShake(6f);
                }
            }
            if (StateTimer >= OldNetMetrics.WardenExecuteTelegraphTicks + 20f) {
                EnterSelect();
            }
        }

        //──── 换相硬直：40t 甲片弹开重排（相变公平阀，弹幕已清）────

        private void UpdatePhaseShift() {
            NPC.damage = 0;
            NPC.velocity *= 0.85f;
            RenderCharge = 0f;
            if (++StateTimer >= OldNetMetrics.WardenPhaseStunTicks) {
                //新相位重建袋（含新招池）；换相后重新吃首袋减速公平阀
                bag.Clear();
                attacksDone = 0;
                EnterSelect();
            }
        }

        //──── 追近：长距贯穿冲刺（位移式，不闪现）────

        private void UpdateChase(Player player) {
            StateTimer++;
            switch ((int)SubIndex) {
                case 0:
                    //30t 锁向前摇
                    NPC.damage = 0;
                    NPC.velocity *= 0.9f;
                    RenderCharge = StateTimer / 30f;
                    NPC.direction = NPC.spriteDirection = player.Center.X >= NPC.Center.X ? 1 : -1;
                    dashDir = (player.Center - NPC.Center).SafeNormalize(Vector2.UnitX);
                    if (StateTimer == 6f && !Main.dedServ) {
                        SoundEngine.PlaySound(SoundID.MenuTick with { Volume = 0.9f, Pitch = -0.6f }, NPC.Center);
                    }
                    if (StateTimer >= 30f) {
                        SubIndex = 1f;
                        StateTimer = 0f;
                        RenderCharge = 0f;
                        NPC.velocity = dashDir * OldNetMetrics.WardenChaseDashSpeed;
                        //追近是位移工具不是主伤害招：独立较低接触伤
                        NPC.damage = OldNetMetrics.WardenChaseContactDamage;
                    }
                    break;
                case 1:
                    //贯穿飞行：追进收刹距或超时 → 硬刹
                    if (Vector2.Distance(player.Center, NPC.Center) < OldNetMetrics.WardenChaseBrakeRange
                        || StateTimer > OldNetMetrics.WardenChaseTimeoutTicks) {
                        SubIndex = 2f;
                        StateTimer = 0f;
                    }
                    break;
                default:
                    NPC.damage = 0;
                    NPC.velocity *= 0.7f;
                    if (StateTimer >= 15f) {
                        EnterSelect();
                    }
                    break;
            }
        }

        //──── 接触结算：冲锋咬 RAM，牵引贴身弹开 ────

        public override void OnHitPlayer(Player target, Player.HurtInfo hurtInfo) {
            //TODO MP: 本钩子只在被打端跑，RAM 扣减联机化走请求包
            if ((int)State == StateDash || (int)State == StateChase) {
                RamSystem.TryConsume(target, OldNetMetrics.WardenDashRam, out _);
            }
            else if ((int)State == StatePull) {
                //贴身弹开：把人推离核心
                target.velocity = (target.Center - NPC.Center).SafeNormalize(-Vector2.UnitY) * 12f;
            }
        }

        //──── 死亡全弧：停攻清弹 → 踉跄 → 警鸣加速+甲片逐拍弹射 → 白闪 → 坠核碎裂 ────

        public override bool CheckDead() {
            if ((int)State != StateDeath) {
                State = StateDeath;
                StateTimer = 0f;
                SubIndex = 0f;
                deathBeepIndex = 0;
                deathBeepTimer = 36f;
                NPC.life = 1;
                NPC.dontTakeDamage = true;
                NPC.damage = 0;
                NPC.velocity *= 0.5f;
                ClearGlyphBolts();
                NPC.netUpdate = true;
                return false;
            }
            return deathArcDone;
        }

        private void UpdateDeathArc() {
            StateTimer++;
            //0-45t：踉跄制动
            if (StateTimer <= 45f) {
                NPC.velocity *= 0.93f;
                return;
            }
            //警鸣加速序列：间隔 36t → 5t 几何递减 12 步，每拍弹射一片甲
            if (deathBeepIndex < 12) {
                if (--deathBeepTimer <= 0f) {
                    float t = deathBeepIndex / 11f;
                    deathBeepTimer = 36f * MathF.Pow(5f / 36f, t);
                    if (!Main.dedServ) {
                        SoundEngine.PlaySound(SoundID.MenuTick with {
                            Volume = 0.9f, Pitch = -0.4f + t * 1.1f
                        }, NPC.Center);
                    }
                    EjectPlate(deathBeepIndex);
                    deathBeepIndex++;
                    if (deathBeepIndex == 12) {
                        //12t 全屏白闪 impact-frame（一场戏只给这一次）
                        WhiteFlash = 1f;
                        SubIndex = StateTimer;
                        if (!Main.dedServ) {
                            SoundEngine.PlaySound(CWRSound.Fault with { Volume = 1f, Pitch = 0.3f }, NPC.Center);
                            Main.LocalPlayer.CWR().GetScreenShake(10f);
                        }
                    }
                }
                NPC.velocity *= 0.95f;
                return;
            }
            //白闪后：核心坠地
            if (StateTimer > SubIndex + 12f) {
                NPC.noTileCollide = false;
                NPC.velocity = new Vector2(NPC.velocity.X * 0.95f, NPC.velocity.Y + 0.35f);
                bool grounded = MathF.Abs(NPC.velocity.Y) < 0.4f && StateTimer > SubIndex + 24f;
                if (grounded || StateTimer > SubIndex + 150f) {
                    //碎裂：真实死亡（OnKill 兑付奖励）
                    deathArcDone = true;
                    NPC.life = 0;
                    NPC.checkDead();
                }
            }
        }

        public override void OnKill() {
            int idx = NPC.lastInteraction;
            Player killer = idx >= 0 && idx < Main.maxPlayers ? Main.player[idx] : null;
            if (killer?.active != true) {
                killer = Main.LocalPlayer;
            }
            if (killer?.active != true) {
                return;
            }
            OldNetPlayer session = OldNetPlayer.Get(killer);

            //① 碎片喷付：分类别多次入账，满载溢出即失（撤离前清账的老决策在这里最疼）
            //TODO MP: 击杀归属与入账为本机语义，联机化走归属端裁决
            int remaining = OldNetMetrics.WardenShardPayout;
            bool overflowed = false;
            while (remaining > 0) {
                int chunk = Math.Min(remaining, Main.rand.Next(2, 5));
                int category = Main.rand.Next(SHPCData.SlotCount);
                if (!session.TryAddHarvest(category, chunk)) {
                    overflowed = true;
                    break;
                }
                remaining -= chunk;
                Color color = SHPCModuleItem.SlotCategoryColor((SHPCSlotCategory)category);
                OldNetAbsorbFX.Emit(NPC.Center + Main.rand.NextVector2Circular(24f, 24f), color, chunk);
            }
            if (overflowed) {
                session.NotifyLedgerFull(NPC.Center);
            }

            //② 全网静默：直落 30（清剿波由 Director 既有释放逻辑自然解除）+ 静默余量
            session.SilenceNoise(OldNetMetrics.WardenSilenceFloor);

            if (killer.whoAmI == Main.myPlayer) {
                CombatText.NewText(killer.getRect(), new Color(120, 255, 170),
                    OldNetTexts.WardenSlain.Value, dramatic: true);
                SoundEngine.PlaySound(SoundID.ResearchComplete with { Volume = 0.8f }, killer.Center);
            }
            CWRMod.Instance.Logger.Info("[OldNet] warden slain: silence granted");
        }

        //清空自家字形弹幕（相变/死亡公平阀）
        private void ClearGlyphBolts() {
            int boltType = ModContent.ProjectileType<OldNetWardenGlyphBolt>();
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile proj = Main.projectile[i];
                if (proj.active && proj.type == boltType) {
                    proj.Kill();
                }
            }
        }

        //──── 甲片编队 ────

        private void InitPlates() {
            if (platesInit) {
                return;
            }
            platesInit = true;
            for (int i = 0; i < plates.Length; i++) {
                plates[i].BaseAngle = MathHelper.TwoPi * i / plates.Length;
                plates[i].Radius = 34f + i % 2 * 7f;
            }
        }

        private void UpdatePlates() {
            plateOrbit += 0.01f + RenderCharge * 0.05f;
            //P3 逐片脱轨：失血越深漂离越多（降解演出）
            int detachTarget = Phase >= 3 && (int)State != StateDeath
                ? (int)((RenderDecay - 0.75f) * 4f / 0.25f + 1f) : 0;
            int detached = 0;
            for (int i = 0; i < plates.Length; i++) {
                ref WardenPlate plate = ref plates[i];
                if (plate.Detached) {
                    detached++;
                    //脱轨甲片：缓慢漂移+自旋+微阻尼（留在战场附近的残骸）
                    plate.Vel *= 0.985f;
                    plate.Vel.Y += (int)State == StateDeath ? 0.12f : 0f;
                    plate.Pos += plate.Vel;
                    plate.Spin += 0.06f;
                    continue;
                }
                //受击弹开重排：阻尼弹簧回位
                plate.JitterVel += -plate.Jitter * 0.15f;
                plate.JitterVel *= 0.82f;
                plate.Jitter += plate.JitterVel;
            }
            //按失血推进脱轨（只增不减）
            for (int i = 0; i < plates.Length && detached < detachTarget; i++) {
                if (!plates[i].Detached) {
                    DetachPlate(i, Main.rand.NextVector2Circular(1.2f, 1.2f));
                    detached++;
                }
            }
        }

        private void DetachPlate(int i, Vector2 vel) {
            ref WardenPlate plate = ref plates[i];
            plate.Detached = true;
            plate.Pos = NPC.Center
                + (plate.BaseAngle + plateOrbit).ToRotationVector2() * plate.Radius;
            plate.Vel = vel;
        }

        //死亡逐拍弹射：一拍一片，带真实初速
        private void EjectPlate(int beepIndex) {
            for (int i = 0; i < plates.Length; i++) {
                if (!plates[i].Detached) {
                    DetachPlate(i, Main.rand.NextVector2Unit() * Main.rand.NextFloat(3f, 7f)
                        - Vector2.UnitY * 2f);
                    return;
                }
            }
        }

        private void PopAllPlates(float impulse) {
            for (int i = 0; i < plates.Length; i++) {
                plates[i].JitterVel += Main.rand.NextVector2Unit() * impulse;
            }
        }

        public override void HitEffect(NPC.HitInfo hit) {
            //受击甲片弹开：质量反馈
            PopAllPlates(MathHelper.Clamp(hit.Damage * 0.01f, 0.8f, 3f));
            if (Main.dedServ) {
                return;
            }
            for (int i = 0; i < (NPC.life <= 0 ? 20 : 3); i++) {
                Dust dust = Dust.NewDustDirect(NPC.position, NPC.width, NPC.height,
                    DustID.Electric, Main.rand.NextFloat(-3f, 3f), Main.rand.NextFloat(-3f, 3f));
                dust.noGravity = true;
                dust.scale = Main.rand.NextFloat(0.6f, 1.2f);
            }
        }

        //──── CPU 全保真绘制：纹章骨架(仅shader缺编) + 甲片编队 + 核心 + 遥测 ────
        //shader 富层（纹章环/核心独目）由 OldNetWardenRender 在实体层下方绘制；
        //全接管绘制：透明度全程显式给值（RenderAlpha 托底 0.15，出生不隐形）

        //纹章骨架 d 串：八芒外环 + 内菱 + 四向刻线（SvgPathPen 归一 [-1,1] 空间）
        private const string EmblemPath =
            "M 0 -1 L 0.38 -0.38 L 1 0 L 0.38 0.38 L 0 1 L -0.38 0.38 L -1 0 L -0.38 -0.38 Z "
            + "M 0 -0.52 L 0.52 0 L 0 0.52 L -0.52 0 Z "
            + "M 0 -0.82 L 0 -0.66 M 0.82 0 L 0.66 0 M 0 0.82 L 0 0.66 M -0.82 0 L -0.66 0";

        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            Texture2D px = VaultAsset.placeholder2?.Value;
            if (px == null || px.IsDisposed) {
                return false;
            }
            Vector2 center = NPC.Center - screenPos;
            Vector2 origin = new(px.Width * 0.5f, px.Height * 0.5f);
            float t = Main.GlobalTimeWrappedHourly;
            float alpha = MathF.Max(RenderAlpha, 0.15f);
            float scale = RenderScale;

            Color ember = new(235, 64, 44);
            Color body = new(14, 7, 9);

            Vector2 Size(float w, float h) => new(w / px.Width, h / px.Height);

            //冲刺残影：3 帧速度拉伸拖影（速度门控，仅高速时出现）
            float vel = NPC.velocity.Length();
            if (vel > 14f) {
                for (int g = 1; g <= 3; g++) {
                    Vector2 back = center - NPC.velocity * (g * 1.9f);
                    spriteBatch.Draw(px, back, null, ember * (alpha * (0.3f - g * 0.08f)),
                        NPC.velocity.ToRotation(), origin,
                        Size(46f * scale + vel, 20f * scale), SpriteEffects.None, 0f);
                }
            }

            //shader 缺编时的全保真纹章骨架（战斗可读性不依赖 shader）
            if (Common.EffectLoader.OldNetWarden?.Value == null) {
                SvgPath emblem = SvgPathPen.Path(EmblemPath);
                SvgPathPen.Stroke(spriteBatch, emblem, center, 52f * scale, RingSpin,
                    ember, 2f, alpha * 0.75f, core: Color.White * 0.6f);
                //环上巡行亮笔：转起来的活环
                SvgPathPen.StrokeRunner(spriteBatch, emblem, center, 52f * scale, RingSpin,
                    Color.White, 2.6f, alpha * 0.8f, t * 0.23f + Seed, 0.08f);
            }

            //甲片编队：8-12 片绕核 quad（受击弹开重排 = 质量反馈）
            for (int i = 0; i < plates.Length; i++) {
                WardenPlate plate = plates[i];
                Vector2 pos;
                float ang;
                if (plate.Detached) {
                    pos = plate.Pos - screenPos;
                    ang = plate.Spin;
                }
                else {
                    float orbitAng = plate.BaseAngle + plateOrbit;
                    pos = center + orbitAng.ToRotationVector2() * (plate.Radius * scale)
                        + plate.Jitter;
                    ang = orbitAng + MathHelper.PiOver2;
                }
                spriteBatch.Draw(px, pos, null, body * alpha, ang,
                    origin, Size(15f * scale, 6f * scale), SpriteEffects.None, 0f);
                spriteBatch.Draw(px, pos, null, ember * (alpha * 0.55f), ang,
                    origin, Size(13f * scale, 1.6f * scale), SpriteEffects.None, 0f);
            }

            //核心：暗盘 + 红辉 + 白芯（shader 独目就绪时它是芯上的实体锚）
            spriteBatch.Draw(px, center, null, body * alpha, MathHelper.PiOver4 + t * 0.3f,
                origin, Size(26f * scale, 26f * scale), SpriteEffects.None, 0f);
            Texture2D glowTex = CWRAsset.SoftGlow?.Value;
            if (glowTex != null) {
                float chargePulse = 0.55f + RenderCharge * 0.45f
                    + 0.12f * MathF.Sin(t * 6f + Seed);
                Color coreGlow = ember * (chargePulse * alpha);
                coreGlow.A = 0;
                spriteBatch.Draw(glowTex, center, null, coreGlow, 0f,
                    glowTex.Size() * 0.5f, (0.5f + RenderCharge * 0.16f) * scale,
                    SpriteEffects.None, 0f);
                Color whiteCore = Color.White * (0.5f * alpha + WhiteFlash * 0.5f);
                whiteCore.A = 0;
                spriteBatch.Draw(glowTex, center, null, whiteCore, 0f,
                    glowTex.Size() * 0.5f, 0.16f * scale, SpriteEffects.None, 0f);
            }

            DrawTelegraphs(spriteBatch, px, center, origin, screenPos, alpha, t);
            return false;
        }

        //遥测层：每招的前摇/警示可读化（CPU 恒有，不依赖 shader）
        private void DrawTelegraphs(SpriteBatch spriteBatch, Texture2D px, Vector2 center,
            Vector2 origin, Vector2 screenPos, float alpha, float t) {
            Color ember = new(235, 64, 44);
            Color cyan = new(0, 220, 255);
            int state = (int)State;

            Vector2 Size(float w, float h) => new(w / px.Width, h / px.Height);

            //冲锋/追近蓄力：锁定方向的三道渐长警示线（发射前的可读直线）
            if ((state == StateDash || state == StateChase) && RenderCharge > 0.05f) {
                for (int i = -1; i <= 1; i++) {
                    Vector2 rayDir = dashDir.RotatedBy(i * 0.1f);
                    float rayLen = 90f * RenderCharge;
                    spriteBatch.Draw(px, center + rayDir * (34f + rayLen * 0.5f), null,
                        ember * (0.5f * RenderCharge * alpha), rayDir.ToRotation(),
                        origin, Size(rayLen, 1.3f), SpriteEffects.None, 0f);
                }
            }

            //齐射前摇：环绕红弧段（纹章环展开的语汇）
            if (state == StateVolley && RenderCharge > 0.05f) {
                int segs = (int)(RenderCharge * 10f);
                for (int i = 0; i < segs; i++) {
                    float ang = -MathHelper.PiOver2 + (i - segs * 0.5f) * 0.3f;
                    Vector2 segPos = center + ang.ToRotationVector2() * 48f;
                    spriteBatch.Draw(px, segPos, null, ember * (0.7f * alpha),
                        ang + MathHelper.PiOver2, origin, Size(3f, 8f), SpriteEffects.None, 0f);
                }
            }

            //字形雨：投放面线 + 安全缝缘亮青光标（缝上下缘各一粒）
            if (state == StateRain && StateTimer > 2f) {
                Vector2 planeL = new Vector2(rainPlaneX - OldNetMetrics.WardenRainWidth * 0.5f, rainPlaneY)
                    - screenPos;
                spriteBatch.Draw(px, planeL + new Vector2(OldNetMetrics.WardenRainWidth * 0.5f, 0f),
                    null, ember * (0.35f * alpha), 0f, origin,
                    Size(OldNetMetrics.WardenRainWidth, 2f), SpriteEffects.None, 0f);
                float gapHalf = OldNetMetrics.WardenRainGapTiles * 16f * 0.5f;
                for (int g = 0; g < gapCount; g++) {
                    for (int s = -1; s <= 1; s += 2) {
                        Vector2 edge = new Vector2(gapCenters[g] + s * gapHalf, rainPlaneY) - screenPos;
                        //缝缘竖标：从投放面下垂的亮青短线 + 缘点
                        spriteBatch.Draw(px, edge + new Vector2(0f, 30f), null,
                            cyan * (0.4f * alpha), 0f, origin, Size(1.4f, 60f), SpriteEffects.None, 0f);
                        spriteBatch.Draw(px, edge, null, cyan * (0.9f * alpha),
                            MathHelper.PiOver4, origin, Size(5f, 5f), SpriteEffects.None, 0f);
                    }
                }
            }

            //吞噬牵引：向核心收束的流线（半径场的可读化）
            if (state == StatePull && RenderCharge > 0.5f) {
                for (int i = 0; i < 6; i++) {
                    float ang = t * 1.7f + i * MathHelper.TwoPi / 6f + Seed;
                    float dist = 90f + (1f - (t * 2f + i * 0.37f) % 1f) * 160f;
                    Vector2 streakPos = center + ang.ToRotationVector2() * dist;
                    spriteBatch.Draw(px, streakPos, null, ember * (0.4f * alpha),
                        ang + MathHelper.Pi, origin, Size(22f, 1.4f), SpriteEffects.None, 0f);
                }
            }

            //终末协议：向内收缩的读秒环
            if (state == StateExecute && RenderCharge > 0.02f) {
                float ringR = MathHelper.Lerp(130f, 30f, RenderCharge);
                for (int k = 0; k < 8; k++) {
                    float ang = MathHelper.TwoPi * k / 8f + RingSpin * 2f;
                    Vector2 dotPos = center + ang.ToRotationVector2() * ringR;
                    spriteBatch.Draw(px, dotPos, null, ember * (0.8f * alpha),
                        ang, origin, Size(7f, 2f), SpriteEffects.None, 0f);
                }
            }
        }
    }

    /// <summary>
    /// 处决字形：回收官字形雨的竖落弹。慢落（3.5px/f），命中小额 HP + 1 RAM，
    /// 落地即灭。竖长字形条形制：主体 + 亮芯头 + 渐隐尾 + 乱码刻痕
    /// </summary>
    internal class OldNetWardenGlyphBolt : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        /// <summary>ai[0]：字形种子（生成时随机，乱码刻痕相位）</summary>
        private ref float GlyphSeed => ref Projectile.ai[0];

        public override void SetDefaults() {
            Projectile.width = 10;
            Projectile.height = 18;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 300;
            Projectile.aiStyle = -1;
        }

        public override void AI() {
            if (!OldNetWorld.Active) {
                Projectile.Kill();
                return;
            }
            //慢落恒速：幕的压迫感在密度不在弹速
            Projectile.velocity = new Vector2(0f, OldNetMetrics.WardenGlyphFallSpeed);
            Projectile.rotation = MathHelper.PiOver2;
            Lighting.AddLight(Projectile.Center, 0.16f, 0.04f, 0.03f);
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            //TODO MP: 本钩子只在被打端跑，RAM 扣减联机化走请求包
            RamSystem.TryConsume(target, OldNetMetrics.WardenGlyphRam, out _);
        }

        public override void OnKill(int timeLeft) {
            if (Main.dedServ) {
                return;
            }
            for (int i = 0; i < 4; i++) {
                Dust dust = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height,
                    DustID.Electric, Main.rand.NextFloat(-1.2f, 1.2f), Main.rand.NextFloat(-1.5f, 0f));
                dust.noGravity = true;
                dust.scale = Main.rand.NextFloat(0.4f, 0.7f);
            }
        }

        //竖长字形条：暗体 + 白热头芯 + 三段渐隐尾 + 闪烁乱码刻痕
        public override bool PreDraw(ref Color lightColor) {
            Texture2D px = VaultAsset.placeholder2?.Value;
            if (px == null || px.IsDisposed) {
                return false;
            }
            Vector2 center = Projectile.Center - Main.screenPosition;
            Vector2 origin = new(px.Width * 0.5f, px.Height * 0.5f);
            float t = Main.GlobalTimeWrappedHourly;
            Color ember = new(235, 64, 44);
            Color body = new(16, 8, 10);

            Vector2 Size(float w, float h) => new(w / px.Width, h / px.Height);

            //渐隐尾（上方三段）
            for (int i = 1; i <= 3; i++) {
                Vector2 back = center - new Vector2(0f, i * 7f);
                Main.EntitySpriteDraw(px, back, null, ember * (0.32f - i * 0.08f), 0f,
                    origin, Size(2f, 8f - i * 1.6f), SpriteEffects.None, 0);
            }
            //主体竖条：暗体 + 红缘
            Main.EntitySpriteDraw(px, center, null, ember * 0.65f, 0f,
                origin, Size(3.4f, 12f), SpriteEffects.None, 0);
            Main.EntitySpriteDraw(px, center, null, body, 0f,
                origin, Size(2f, 10f), SpriteEffects.None, 0);
            //乱码刻痕：两粒横向短刻，随字形种子换相闪烁
            for (int i = 0; i < 2; i++) {
                float notchPhase = MathF.Floor(t * 6f + GlyphSeed + i * 3.7f);
                float offY = (notchPhase * 7.31f % 8f) - 4f;
                Main.EntitySpriteDraw(px, center + new Vector2(0f, offY), null,
                    ember * 0.8f, 0f, origin, Size(5f, 1.2f), SpriteEffects.None, 0);
            }
            //白热落头
            Main.EntitySpriteDraw(px, center + new Vector2(0f, 6f), null, Color.White * 0.8f,
                0f, origin, Size(3f, 3f), SpriteEffects.None, 0);
            return false;
        }
    }
}
