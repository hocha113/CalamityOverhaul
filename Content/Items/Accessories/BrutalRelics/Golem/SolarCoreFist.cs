using CalamityOverhaul.Common;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using System;
using System.IO;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Accessories.BrutalRelics.Golem
{
    /// <summary>
    /// 日核拳骨：石巨人残酷遗物。站定入石卫姿态（50% 减伤+免击退），
    /// 受击转化日核蓄能叠层（封顶 24）；姿态中按住下键半秒原地引拳（全额+大新星），
    /// 移动打破姿态则甩出仓促拳（×0.75）。连续姿态 5 秒未出拳强制过热 2 秒
    /// </summary>
    internal class SolarCoreFist : BaseBrutalRelic
    {
        public override void SetDefaults() {
            base.SetDefaults();
            //框架 §9 T4 梯度统一 75 金
            Item.value = Item.buyPrice(0, 75, 0, 0);
        }

        public override void UpdateAccessory(Player player, bool hideVisual) {
            SolarCoreFistPlayer mp = player.GetModPlayer<SolarCoreFistPlayer>();
            mp.Equipped = true;
            mp.RelicItem = Item;
        }
    }

    /// <summary>
    /// 石卫姿态状态机。逐帧逻辑在每个端点上对每名玩家模拟（站定判定吃同步速度），
    /// 层数只在所有者端的 OnHurt 里累积（该钩子仅在受伤玩家本机运行），
    /// 经 <see cref="SolarCoreFistNet"/> 转播给旁观端做可视化；
    /// 重拳弹幕、光环结算、减伤全部所有者端权威
    /// </summary>
    internal class SolarCoreFistPlayer : ModPlayer
    {
        #region 常量
        /// <summary>站定入姿态所需连续帧数（半秒）</summary>
        public const int StanceEntryFrames = 30;
        /// <summary>站定速度阈值（平方），低于视为站定</summary>
        public const float StandSpeedSq = 0.25f;
        /// <summary>打破姿态速度阈值（平方），滞回防抖</summary>
        public const float BreakSpeedSq = 0.81f;
        /// <summary>姿态减伤比（框架 §2.4 窗口 DR 上限）</summary>
        public const float StanceDR = 0.5f;
        /// <summary>蓄能层数封顶（视觉三档 8/16/24）</summary>
        public const int MaxCharge = 24;
        /// <summary>连续姿态时长上限（帧），超时未出拳强制过热</summary>
        public const int StanceMaxDuration = 300;
        /// <summary>过热时长（帧）：无减伤、不可蓄能、不可入姿态</summary>
        public const int OverheatDuration = 120;
        /// <summary>原地引拳所需的按住下键帧数（半秒）</summary>
        public const int BracedHoldFrames = 30;
        /// <summary>仓促拳（移动打破姿态）伤害倍率</summary>
        public const float RushedPunchMult = 0.75f;
        /// <summary>灼热光环点亮层数</summary>
        public const int AuraStacks = 8;
        /// <summary>光环半径（判定与 TechAura 可见环同源）</summary>
        public const float AuraRadius = 170f;
        /// <summary>光环结算间隔帧</summary>
        public const int AuraStrikeInterval = 20;
        /// <summary>光环单跳基数与每层增量（吃 Generic 加成）</summary>
        public const int AuraBaseDamage = 40;
        public const int AuraStackDamage = 4;
        /// <summary>重拳基础伤害（基数，吃 Generic 加成）</summary>
        public const int PunchBaseDamage = 500;
        /// <summary>每层伤害增幅（对基础乘算，24 层满 ×4）</summary>
        public const float PunchStackScale = 0.125f;
        #endregion

        #region 状态
        /// <summary>渲染层帧戳：任一端存在可绘状态时盖戳，RenderHandle 据此跳过空场全表扫</summary>
        internal static ActivityStamp PresenceStamp;

        /// <summary>本帧装备中（ResetEffects 清）</summary>
        public bool Equipped;
        /// <summary>饰品实例，弹幕生成源用</summary>
        public Item RelicItem;
        /// <summary>连续站定帧数</summary>
        public int StanceTimer;
        /// <summary>石卫姿态生效中</summary>
        public bool InStance;
        /// <summary>日核蓄能层数（封顶 24）；旁观端由网络写入</summary>
        public int ChargeStacks;
        /// <summary>过热剩余帧。owner 权威推进；远端由网络包对齐后本地跑表（只喂视觉与姿态门）</summary>
        public int OverheatTimer;
        /// <summary>原地引拳蓄劲 0..1（owner 本地，渲染层聚光用）</summary>
        public float HoldCharge;

        //视觉包络（各端本地推进）
        /// <summary>石壳成形 0..1</summary>
        public float ShellForm;
        /// <summary>受击闪 0..1</summary>
        public float Flare;
        /// <summary>灼热光环渐入 0..1</summary>
        public float AuraGlow;

        private int auraStrikeTimer;
        private int punchCooldown;
        /// <summary>连续姿态时长（owner 本地，过热裁决用）</summary>
        private int stanceDuration;
        /// <summary>按住下键连续帧数（owner 本地）</summary>
        private int holdDownFrames;
        /// <summary>引拳就绪闩：PostUpdateEquips 置位，PreUpdateMovement 姿态机消费，只活一帧</summary>
        private bool queuedBracedPunch;
        /// <summary>上帧层数（各端统一的受击反馈/档位拍检测）</summary>
        private int prevStacksVisual;
        /// <summary>上帧过热态（各端统一的过热起止反馈）</summary>
        private bool prevOverheatVisual;
        /// <summary>原地引拳事件待发包（owner；远端无按键沿，靠包补演出）</summary>
        private bool punchEventPending;
        private bool netDirty;
        private int netThrottle;
        #endregion

        public override void Initialize() {
            Equipped = false;
            RelicItem = null;
            StanceTimer = 0;
            InStance = false;
            ChargeStacks = 0;
            OverheatTimer = 0;
            HoldCharge = 0f;
            ShellForm = 0f;
            Flare = 0f;
            AuraGlow = 0f;
            auraStrikeTimer = 0;
            punchCooldown = 0;
            stanceDuration = 0;
            holdDownFrames = 0;
            queuedBracedPunch = false;
            prevStacksVisual = 0;
            prevOverheatVisual = false;
            punchEventPending = false;
            netDirty = false;
            netThrottle = 0;
        }

        public override void ResetEffects() => Equipped = false;

        //按住下键计数只能放在这里或更早：原版在 Update 中段把 releaseDown 改写为
        //"按住即 false"，到 PreUpdateMovement 时任何按键沿检测恒假（旧双击方案的死因）。
        //controlDown 持续计数不依赖沿，但统一段位降低后人踩坑面（键位裁决 §5）
        public override void PostUpdateEquips() {
            if (Player.whoAmI != Main.myPlayer || !Equipped || Player.dead) {
                holdDownFrames = 0;
                HoldCharge = 0f;
                return;
            }
            if (InStance && OverheatTimer <= 0 && Player.controlDown) {
                holdDownFrames++;
                HoldCharge = Math.Min(holdDownFrames / (float)BracedHoldFrames, 1f);
                //沉腰蓄劲的阶梯轻响（仅操作者听）
                if (holdDownFrames % 10 == 0 && holdDownFrames < BracedHoldFrames && !VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Tink with {
                        Pitch = -0.25f + holdDownFrames / (float)BracedHoldFrames * 0.5f,
                        Volume = 0.4f
                    }, Player.Center);
                }
                if (holdDownFrames >= BracedHoldFrames) {
                    queuedBracedPunch = true;
                    holdDownFrames = 0;
                }
                //吞掉下键对平台穿落的传导：原版 fallThrough 快照（Player.cs:24704）晚于本钩子，
                //置 false 后站平台引拳不再穿板坠落误耗蓄能。姿态语义即站定，本帧计数已用读取值
                Player.controlDown = false;
            }
            else {
                holdDownFrames = 0;
                HoldCharge = Math.Max(HoldCharge - 0.12f, 0f);
            }
        }

        public override void UpdateDead() {
            //死亡蓄能清空，不打拳；过热一并归零（重生从头来）
            StanceTimer = 0;
            InStance = false;
            stanceDuration = 0;
            holdDownFrames = 0;
            HoldCharge = 0f;
            queuedBracedPunch = false;
            bool dirty = ChargeStacks != 0 || OverheatTimer != 0;
            ChargeStacks = 0;
            OverheatTimer = 0;
            if (dirty) {
                MarkNetDirty(force: true);
            }
            ShellForm = Math.Max(ShellForm - 0.2f, 0f);
            AuraGlow = 0f;
            if (ShellForm > 0f) {
                PresenceStamp.Stamp();
            }
            TickNet();
        }

        #region 主逻辑
        public override void PreUpdateMovement() {
            if (punchCooldown > 0) {
                punchCooldown--;
            }
            //过热各端本地跑表：owner 权威置值，远端由网络包对齐后自行倒数
            if (OverheatTimer > 0) {
                OverheatTimer--;
            }

            if (!Equipped) {
                if (InStance || ChargeStacks > 0) {
                    InStance = false;
                    StanceTimer = 0;
                    stanceDuration = 0;
                    if (ChargeStacks != 0) {
                        ChargeStacks = 0;
                        MarkNetDirty(force: true);
                    }
                }
                UpdateVisualEnvelopes();
                TickNet();
                return;
            }

            float speedSq = Player.velocity.LengthSquared();
            bool standing = speedSq <= (InStance ? BreakSpeedSq : StandSpeedSq)
                && !Player.mount.Active && !Player.pulley && Player.grapCount == 0;

            if (OverheatTimer > 0) {
                //过热期不可入姿态；远端收到过热包也由此收掉本地模拟的姿态视觉
                InStance = false;
                StanceTimer = 0;
                stanceDuration = 0;
            }
            else if (standing) {
                bool wasInStance = InStance;
                StanceTimer++;
                InStance = StanceTimer >= StanceEntryFrames;
                if (InStance && !wasInStance) {
                    stanceDuration = 0;
                    OnStanceEnter();
                }

                //入姿态前的聚石预兆
                if (!InStance && !VaultUtils.isServer && StanceTimer > 6 && Main.rand.NextBool(3)) {
                    Vector2 from = Player.Center + Main.rand.NextVector2CircularEdge(1f, 1f) * Main.rand.NextFloat(34f, 52f);
                    Dust dust = Dust.NewDustPerfect(from, DustID.Stone, (Player.Center - from) * 0.06f, 120, default, 1.1f);
                    dust.noGravity = true;
                }
            }
            else {
                if (InStance) {
                    //移动打破姿态：有蓄能即甩出仓促拳（×0.75），出拳即正确释放、不触发过热
                    FirePunch(braced: false);
                }
                InStance = false;
                StanceTimer = 0;
                stanceDuration = 0;
            }

            if (InStance) {
                Player.noKnockback = true;

                //姿态时长与过热/引拳裁决只在 owner 端（远端由网络包对齐过热态与引拳事件）
                if (Player.whoAmI == Main.myPlayer) {
                    stanceDuration++;
                    //引拳裁决先于过热：蓄劲 30 帧恰逢姿态第 300 帧时，按住下已是明确的正确释放意图
                    if (queuedBracedPunch && punchCooldown <= 0) {
                        if (ChargeStacks > 0) {
                            //原地引拳：全额伤害 + 大新星，出拳即结束姿态且不触发过热
                            FirePunch(braced: true);
                            ExitStance();
                        }
                        else if (!VaultUtils.isServer) {
                            //无蓄能空引：闷响提示，姿态保持
                            SoundEngine.PlaySound(SoundID.Tink with { Pitch = -0.6f, Volume = 0.5f }, Player.Center);
                        }
                    }
                    else if (stanceDuration >= StanceMaxDuration) {
                        //超时未出拳：强制过热收姿态，不出拳（打出去 = 正确释放，不会走到这里）
                        EnterOverheat();
                    }
                    else {
                        TickOverheatWarning();
                    }
                }

                //满层灼热光环：结算只在所有者端，AddBuff/SimpleStrikeNPC 自带联机同步
                if (InStance && ChargeStacks >= AuraStacks && Player.whoAmI == Main.myPlayer) {
                    if (++auraStrikeTimer >= AuraStrikeInterval) {
                        auraStrikeTimer = 0;
                        AuraStrike();
                    }
                }
                else {
                    auraStrikeTimer = 0;
                }
            }

            //闩只活一帧：无论姿态是否消费，帧末清
            queuedBracedPunch = false;
            UpdateVisualEnvelopes();
            TickNet();
        }

        /// <summary>出拳后的姿态收束：正确释放，不触发过热</summary>
        private void ExitStance() {
            InStance = false;
            StanceTimer = 0;
            stanceDuration = 0;
            HoldCharge = 0f;
        }

        /// <summary>站桩超时：强制过热，收姿态不出拳。远端过热态经状态包对齐</summary>
        private void EnterOverheat() {
            InStance = false;
            StanceTimer = 0;
            stanceDuration = 0;
            holdDownFrames = 0;
            HoldCharge = 0f;
            OverheatTimer = OverheatDuration;
            MarkNetDirty(force: true);
        }

        /// <summary>
        /// 过热预警（owner 本地，站桩管理者的操作提示）：
        /// 最后 1.5 秒壳面加急闪烁 + 音高上行的滴答，越临界越急
        /// </summary>
        private void TickOverheatWarning() {
            int remain = StanceMaxDuration - stanceDuration;
            if (remain > 90) {
                return;
            }
            int period = remain <= 30 ? 8 : remain <= 60 ? 14 : 20;
            if (stanceDuration % period == 0) {
                Flare = Math.Max(Flare, 0.55f);
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Tink with {
                        Pitch = -0.3f + (90 - remain) / 90f * 0.65f,
                        Volume = 0.5f
                    }, Player.Center);
                }
            }
        }

        /// <summary>姿态成立拍：石壳砸地成形</summary>
        private void OnStanceEnter() {
            if (VaultUtils.isServer) {
                return;
            }
            SoundEngine.PlaySound(SoundID.DD2_OgreGroundPound with { Pitch = 0.25f, Volume = 0.65f }, Player.Center);
            SoundEngine.PlaySound(SoundID.Tink with { Pitch = -0.4f, Volume = 0.8f }, Player.Center);
            for (int i = 0; i < 14; i++) {
                Vector2 vel = Main.rand.NextVector2Unit() * Main.rand.NextFloat(1.5f, 4f);
                vel.Y -= 1.2f;
                Dust dust = Dust.NewDustPerfect(Player.Bottom + new Vector2(Main.rand.NextFloat(-16f, 16f), 0f),
                    DustID.Stone, vel, 90, default, 1.35f);
                dust.noGravity = Main.rand.NextBool();
            }
        }

        /// <summary>蓄能档位（三档阶梯：8/16/24 层各一档），渲染层脉络亮度与档位拍共用</summary>
        internal static int ChargeTier(int stacks)
            => stacks >= MaxCharge ? 3 : stacks >= 16 ? 2 : stacks >= AuraStacks ? 1 : 0;

        /// <summary>视觉包络与统一的层数/过热反馈（所有端点一致推进）</summary>
        private void UpdateVisualEnvelopes() {
            bool overheatNow = OverheatTimer > 0;
            if (InStance) {
                ShellForm = Math.Min(ShellForm + 1f / 12f, 1f);
            }
            else if (overheatNow) {
                //过热期护壳不散场：钳在随余热消退的地板上，读作"烧红的壳在冷却"
                float heatFloor = 0.25f + 0.4f * (OverheatTimer / (float)OverheatDuration);
                ShellForm = Math.Max(ShellForm - 1f / 8f, heatFloor);
            }
            else {
                ShellForm = Math.Max(ShellForm - 1f / 8f, 0f);
            }
            Flare = Math.Max(Flare - 0.07f, 0f);

            bool auraOn = InStance && ChargeStacks >= AuraStacks;
            AuraGlow = auraOn
                ? Math.Min(AuraGlow + 0.06f, 1f)
                : Math.Max(AuraGlow - 0.08f, 0f);

            //渲染层帧戳：有可绘状态才放行全表扫
            if (Equipped || ShellForm > 0f || AuraGlow > 0f || overheatNow) {
                PresenceStamp.Stamp();
            }

            //过热起止的统一反馈：owner 由裁决立即触发，旁观端由网络包触发
            if (overheatNow && !prevOverheatVisual && !VaultUtils.isServer) {
                //过热开始拍：高压蒸汽泄压 + 石壳闷响
                SoundEngine.PlaySound(SoundID.LiquidsWaterLava with { Pitch = 0.15f, Volume = 0.9f }, Player.Center);
                SoundEngine.PlaySound(SoundID.Tink with { Pitch = -0.55f, Volume = 0.7f }, Player.Center);
                for (int i = 0; i < 10; i++) {
                    Dust steam = Dust.NewDustPerfect(
                        Player.Center + Main.rand.NextVector2Circular(16f, 22f), DustID.Smoke,
                        new Vector2(Main.rand.NextFloat(-0.8f, 0.8f), -Main.rand.NextFloat(1.2f, 2.6f)),
                        150, new Color(205, 205, 210), Main.rand.NextFloat(1.2f, 1.8f));
                    steam.noGravity = true;
                }
            }
            if (!overheatNow && prevOverheatVisual && Equipped && !Player.dead && !VaultUtils.isServer) {
                //冷却完毕的轻脆声：可以重新站桩了
                SoundEngine.PlaySound(SoundID.Tink with { Pitch = 0.35f, Volume = 0.55f }, Player.Center);
            }
            prevOverheatVisual = overheatNow;

            //过热持续演出：蒸汽外泄 + 暗红余热体光（随剩余时间消退 = 冷却进度肉眼可读）
            if (overheatNow && !VaultUtils.isServer) {
                float heatT = OverheatTimer / (float)OverheatDuration;
                if (Main.rand.NextBool(3)) {
                    Dust steam = Dust.NewDustPerfect(
                        Player.Center + new Vector2(Main.rand.NextFloat(-14f, 14f), Main.rand.NextFloat(-22f, 4f)),
                        DustID.Smoke, new Vector2(Main.rand.NextFloat(-0.35f, 0.35f), -Main.rand.NextFloat(0.8f, 1.8f)),
                        160, new Color(200, 200, 205), 1.1f + 0.6f * heatT);
                    steam.noGravity = true;
                }
                Lighting.AddLight(Player.Center, new Vector3(0.55f, 0.12f, 0.05f) * (0.25f + 0.5f * heatT));
            }

            //层数上升的统一反馈：所有者由 OnHurt 立即触发，旁观端由网络包触发
            if (ChargeStacks > prevStacksVisual) {
                Flare = 1f;
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.NPCHit41 with { Pitch = 0.15f, Volume = 0.55f }, Player.Center);
                    int gained = Math.Min(ChargeStacks - prevStacksVisual, 4);
                    for (int i = 0; i < 5 + gained * 3; i++) {
                        Vector2 vel = Main.rand.NextVector2Unit() * Main.rand.NextFloat(2f, 5.5f);
                        PRTLoader.NewParticle<PRT_Spark>(Player.Center + Main.rand.NextVector2Circular(18f, 26f),
                            vel, Color.Lerp(new Color(255, 170, 60), new Color(255, 220, 130), Main.rand.NextFloat()),
                            Main.rand.NextFloat(0.8f, 1.3f)).Configure(true, Main.rand.Next(14, 24), Player);
                    }
                }
                //档位拍（8/16/24 三档阶梯，音高随档位上行；复用原满层拍语汇）
                int prevTier = ChargeTier(prevStacksVisual);
                int newTier = ChargeTier(ChargeStacks);
                if (newTier > prevTier && !VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.MaxMana with { Pitch = -0.2f + 0.18f * newTier, Volume = 0.9f }, Player.Center);
                    SoundEngine.PlaySound(SoundID.DD2_BetsyFireballShot with { Pitch = -0.35f + 0.1f * newTier, Volume = 0.45f }, Player.Center);
                    int spokes = 14 + newTier * 4;
                    for (int i = 0; i < spokes; i++) {
                        float angle = MathHelper.TwoPi * i / spokes;
                        PRTLoader.NewParticle<PRT_Light>(Player.Center, angle.ToRotationVector2() * (4f + newTier),
                            new Color(255, 200, 90), 0.5f).Configure(Main.rand.Next(20, 32), opacity: 1.3f, squishStrenght: 2.2f);
                    }
                }
            }
            prevStacksVisual = ChargeStacks;

            //蓄能体光（三档阶梯）
            if (ChargeStacks > 0 && InStance) {
                float glow = ChargeTier(ChargeStacks) / 3f;
                Lighting.AddLight(Player.Center, new Vector3(1f, 0.6f, 0.22f) * (0.35f + 0.45f * glow));
            }
        }
        #endregion

        #region 受击与减伤
        public override void ModifyHurt(ref Player.HurtModifiers modifiers) {
            if (InStance) {
                modifiers.FinalDamage *= 1f - StanceDR;
            }
        }

        public override void OnHurt(Player.HurtInfo info) {
            if (!InStance) {
                return;
            }
            //每次受击 +1 层，攻击原始伤害每满 100 点额外 +1 层，封顶 24 层
            int next = Math.Min(ChargeStacks + 1 + Math.Max(info.SourceDamage, 0) / 100, MaxCharge);
            if (next != ChargeStacks) {
                ChargeStacks = next;
                MarkNetDirty();
            }
        }
        #endregion

        #region 重拳与光环
        /// <summary>
        /// 释放日核重拳。braced=原地引拳（全额+大新星），否则为仓促拳（×0.75）。
        /// 演出各端本地播（仓促拳远端靠速度打破本地模拟触发；引拳远端靠状态包事件补演），
        /// 弹幕仅所有者端生成
        /// </summary>
        private void FirePunch(bool braced) {
            if (ChargeStacks <= 0 || punchCooldown > 0) {
                return;
            }
            punchCooldown = 12;
            PlayPunchReleaseFX();

            if (Player.whoAmI == Main.myPlayer) {
                Vector2 dir = Player.Center.To(Main.MouseWorld).SafeNormalize(Vector2.UnitX * Player.direction);
                float raw = PunchBaseDamage * (1f + PunchStackScale * ChargeStacks);
                if (!braced) {
                    raw *= RushedPunchMult;
                }
                int damage = (int)Player.GetTotalDamage(DamageClass.Generic).ApplyTo(raw);
                IEntitySource source = RelicItem != null
                    ? Player.GetSource_Accessory(RelicItem)
                    : Player.GetSource_Misc("SolarCoreFist");
                Projectile.NewProjectile(source, Player.Center, dir * 8f,
                    ModContent.ProjectileType<SolarCoreFistPunch>(), damage, 9f, Player.whoAmI,
                    ChargeStacks, braced ? 1f : 0f);
                if (braced) {
                    //引拳无远端可见的触发沿，事件随状态包捎带给旁观端
                    punchEventPending = true;
                }
            }

            HoldCharge = 0f;
            ChargeStacks = 0;
            MarkNetDirty(force: true);
        }

        /// <summary>出拳释放演出（无方向分量：远端玩家的光标不可知，方向表现交给弹幕自身）</summary>
        internal void PlayPunchReleaseFX() {
            if (VaultUtils.isServer) {
                return;
            }
            SoundEngine.PlaySound(SoundID.DD2_BetsyFireballShot with { Pitch = -0.2f, Volume = 0.95f }, Player.Center);
            SoundEngine.PlaySound(SoundID.Item14 with { Pitch = 0.35f, Volume = 0.6f }, Player.Center);
            for (int i = 0; i < 18; i++) {
                Vector2 vel = Main.rand.NextVector2Unit() * Main.rand.NextFloat(3f, 9f);
                PRTLoader.NewParticle<PRT_Light>(Player.Center, vel,
                    Color.Lerp(new Color(255, 150, 40), new Color(255, 230, 150), Main.rand.NextFloat()),
                    0.45f).Configure(Main.rand.Next(14, 26), opacity: 1.2f, squishStrenght: 2.4f);
            }
        }

        /// <summary>旁观端收到引拳事件：补出拳演出并收掉本地模拟的姿态视觉</summary>
        internal void OnRemotePunchEvent() {
            InStance = false;
            StanceTimer = 0;
            PlayPunchReleaseFX();
        }

        /// <summary>
        /// 灼热光环结算（仅所有者端调用；打击与上buff自带网络同步）。
        /// shader 缺编时判定与演出一起关停：判定期永不隐形，不许敌人被空气点燃
        /// </summary>
        private void AuraStrike() {
            if (EffectLoader.BRelicStoneGuard?.Value == null) {
                return;
            }
            int auraDamage = (int)Player.GetTotalDamage(DamageClass.Generic)
                .ApplyTo(AuraBaseDamage + AuraStackDamage * ChargeStacks);
            foreach (var npc in Main.ActiveNPCs) {
                if (npc.friendly || npc.dontTakeDamage || !npc.CanBeChasedBy()) {
                    continue;
                }
                if (Vector2.Distance(npc.Center, Player.Center) > AuraRadius + npc.width * 0.5f) {
                    continue;
                }
                int dir = npc.Center.X > Player.Center.X ? 1 : -1;
                npc.SimpleStrikeNPC(auraDamage, dir, false, 2f, null, false, 0f, true);
                npc.AddBuff(BuffID.OnFire3, 240);
                npc.AddBuff(BuffID.Daybreak, 240);
            }
        }
        #endregion

        #region 层数同步
        private void MarkNetDirty(bool force = false) {
            netDirty = true;
            if (force) {
                netThrottle = 0;
            }
        }

        /// <summary>状态变化节流转播（仅所有者端出包）</summary>
        private void TickNet() {
            if (netThrottle > 0) {
                netThrottle--;
            }
            if (!netDirty || netThrottle > 0 || Player.whoAmI != Main.myPlayer) {
                return;
            }
            netDirty = false;
            netThrottle = 8;
            SolarCoreFistNet.SendState(Player, ChargeStacks, OverheatTimer, punchEventPending);
            punchEventPending = false;
        }
        #endregion
    }

    /// <summary>
    /// 姿态状态纯表现转播（层数 + 过热剩余帧 + 引拳事件位）：
    /// 旁观端石壳脉络/暗红余热/引拳演出可视化用，权威值只在所有者端。
    /// 包型不变，只在原层数负载后追加字段（两端同二进制，编号与格式必一致）
    /// </summary>
    internal class SolarCoreFistNet : CWRNetChannel
    {
        internal static void SendState(Player owner, int stacks, int overheatLeft, bool punchEvent) {
            if (Main.netMode != NetmodeID.MultiplayerClient || owner == null
                || owner.whoAmI != Main.myPlayer) {
                return;
            }
            ModPacket packet = CWRNetWork.GetPacket<SolarCoreFistNet>();
            packet.Write((byte)owner.whoAmI);
            packet.Write((ushort)Math.Clamp(stacks, 0, ushort.MaxValue));
            packet.Write((byte)Math.Clamp(overheatLeft, 0, byte.MaxValue));
            packet.Write(punchEvent);
            packet.Send();
        }

        public override void Receive(BinaryReader reader, int whoAmI) {
            //定长负载先读满，校验只做丢弃
            int declaredOwner = reader.ReadByte();
            int stacks = reader.ReadUInt16();
            int overheatLeft = reader.ReadByte();
            bool punchEvent = reader.ReadBoolean();

            if (Main.netMode == NetmodeID.Server) {
                //来源以连接为准，原样转播给除发送者外的所有人
                ModPacket packet = CWRNetWork.GetPacket<SolarCoreFistNet>();
                packet.Write((byte)whoAmI);
                packet.Write((ushort)stacks);
                packet.Write((byte)overheatLeft);
                packet.Write(punchEvent);
                packet.Send(-1, whoAmI);
                return;
            }
            if (Main.netMode != NetmodeID.MultiplayerClient
                || declaredOwner < 0 || declaredOwner >= Main.maxPlayers
                || declaredOwner == Main.myPlayer) {
                return;
            }
            Player owner = Main.player[declaredOwner];
            if (owner?.active != true) {
                return;
            }
            SolarCoreFistPlayer mp = owner.GetModPlayer<SolarCoreFistPlayer>();
            mp.ChargeStacks = stacks;
            //过热剩余帧对齐后由远端自行倒数（时长恒定，事件式同步足够）
            mp.OverheatTimer = overheatLeft;
            if (punchEvent) {
                mp.OnRemotePunchEvent();
            }
        }
    }
}
