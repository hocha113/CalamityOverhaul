using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalBrainOfCthulhu.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalBrainOfCthulhu.Projectiles;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalBrainOfCthulhu.States
{
    /// <summary>
    /// 二阶段投技·摄心镜狱：六具镜像绕锁死锚点收环（纯预告无判定，走出环心即可反制），
    /// 收环完成时玩家仍在环心则被念力定身悬空，三具镜像逐一穿刺，真身最后蓄力贯穿撞散镜阵掷飞
    /// 落空则镜阵内爆自碎，真身陷入受创加深的力竭惩罚窗
    /// 网络形状：判定与时序服务端权威（override.ai[4]=受害者+1、[5]=掷飞角，netUpdate 载运）；
    /// 受害者的位移钉锚/输入锁/伤害结算全部在其本地客户端（BrainMindSeizePlayer），服务端不写玩家位置
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)BrainStateIndex.MindSeize, typeof(BrainStateContext))]
    internal class BrainMindSeizeState : BrainStateBase
    {
        public override string StateName => "MindSeize";
        public override BrainStateIndex StateIndex => BrainStateIndex.MindSeize;
        public override bool AllowFarSnap => false;

        #region 同步槽与节奏常量（受害端/镜像与本状态共用）
        /// <summary>override.ai 槽位：受害者 whoAmI+1，0=未捕获</summary>
        internal const int SlotVictim = 4;
        /// <summary>override.ai 槽位：掷飞方向（弧度，捕获帧服务端 roll）</summary>
        internal const int SlotFlingAngle = 5;

        /// <summary>镜像具数</summary>
        internal const int MirrorCount = 6;
        /// <summary>收环起始半径</summary>
        internal const float StartRadius = 560f;
        /// <summary>收环完成的持环半径</summary>
        internal const float HoldRadius = 210f;
        /// <summary>捕获判定半径（玩家距锚点）</summary>
        internal const float CaptureRadius = 170f;
        /// <summary>收环完成帧（镜像寿命与状态 Timer 同起点）</summary>
        internal const int SnapTick = 96;
        /// <summary>镜环自旋速率（弧度/帧）</summary>
        internal const float RingSpinRate = 0.006f;
        /// <summary>收环模式镜像的自碎兜底寿命</summary>
        internal const int MirrorLifeCap = 60 * 9;

        /// <summary>穿刺收势时长</summary>
        internal const int PierceReelTime = 14;
        /// <summary>穿刺冲刺速度</summary>
        internal const float PierceDashSpeed = 36f;
        /// <summary>三拍穿刺的收势起始帧（相对捕获边沿，槽 0/2/4；间隔宽于受伤无敌帧）</summary>
        internal static readonly int[] PierceReelStarts = [36, 84, 130];
        /// <summary>三拍穿刺的受害结算帧（镜像恰好掠过环心）</summary>
        internal static readonly int[] PierceHurtTicks = [58, 106, 152];
        /// <summary>真身终结收势起始帧</summary>
        internal const int FinisherReelTick = 156;
        /// <summary>真身终结冲刺出发帧</summary>
        internal const int FinisherDashTick = 186;
        /// <summary>掷飞帧：受害端结算终结伤害+掷飞+解锁，服务端撞散残镜</summary>
        internal const int FlingTick = 200;
        /// <summary>连段总长（掷飞后恢复拍结束）</summary>
        internal const int ComboEndTick = 252;

        /// <summary>单次穿刺伤害占最大生命比例（结算钳制永不致死）</summary>
        internal const float PierceHurtFraction = 0.08f;
        /// <summary>终结掷飞伤害占最大生命比例</summary>
        internal const float FlingHurtFraction = 0.13f;
        /// <summary>掷飞初速</summary>
        internal const float FlingSpeed = 21f;

        /// <summary>解锁血量比（二阶段 0.55 转换后再压一段才开放，最佳招式扣押到深水区）</summary>
        internal const float UnlockLifeRatio = 0.45f;
        /// <summary>落空惩罚：力竭窗（受创加深）</summary>
        internal const int WhiffFalter = 120;
        /// <summary>命中后的完整冷却</summary>
        internal const int CooldownHit = 60 * 38;
        /// <summary>落空后的缩短冷却</summary>
        internal const int CooldownWhiff = 60 * 20;
        /// <summary>状态硬超时（任何异常都保证退出）</summary>
        internal const int HardTimeout = 460;
        /// <summary>受害者被外力挪出此距离即断投</summary>
        internal const float BreakDistance = 700f;
        /// <summary>真身终结冲刺的出发距离（锚点反掷向）</summary>
        internal const float BrainLaunchDist = 480f;
        #endregion

        /// <summary>服务端：收环判定已裁决</summary>
        private bool snapResolved;
        /// <summary>服务端：本次落空</summary>
        private bool whiffed;
        /// <summary>服务端连段时钟（捕获帧起算，-1=未捕获）</summary>
        private int comboTick = -1;
        /// <summary>各端表现时钟（ai[4] 上升沿起算，-1=未捕获）</summary>
        private int presentTick = -1;
        /// <summary>本端见过捕获（掷飞后清标记不得误入落空表现）</summary>
        private bool captureSeen;
        /// <summary>落空后的收场计时</summary>
        private int whiffTimer;

        public BrainMindSeizeState() {
        }

        #region 共用数学（镜像与受害端同式，保证各端画面一致）

        /// <summary>收环半径：四步阶收缩，每步锐利落位，末段骤然合拢</summary>
        internal static float RingRadius(float age) {
            if (age >= SnapTick) {
                return HoldRadius;
            }
            //分段：0-24 / 24-48 / 48-72 / 72-96，逐步收紧
            ReadOnlySpan<float> radii = [StartRadius, 450f, 340f, 262f, HoldRadius];
            int step = Math.Clamp((int)(age / 24f), 0, 3);
            float t = MathHelper.Clamp(age % 24f / 14f, 0f, 1f);
            return MathHelper.Lerp(radii[step], radii[step + 1], BrainMotion.SharpOut(t, 5));
        }

        /// <summary>槽位→穿刺收势起始帧（仅偶数槽出手，奇数槽驻环到终结）</summary>
        internal static int PierceReelStart(int slot) {
            int order = Math.Clamp(slot / 2, 0, PierceReelStarts.Length - 1);
            return PierceReelStarts[order];
        }

        #endregion

        private Vector2 Anchor(BrainStateContext context) => new(context.Master.ai[0], context.Master.ai[1]);

        private static int VictimIndex(BrainStateContext context) => (int)context.Master.ai[SlotVictim] - 1;

        private float FlingAngle(BrainStateContext context) => context.Master.ai[SlotFlingAngle];

        public override void OnEnter(BrainStateContext context) {
            base.OnEnter(context);
            NPC npc = context.Npc;
            npc.damage = 0;
            snapResolved = false;
            whiffed = false;
            comboTick = -1;
            presentTick = -1;
            captureSeen = false;
            whiffTimer = 0;

            if (!VaultUtils.isClient) {
                //锚点锁死：目标当前位+轻微速度预判，此后绝不追踪（走出环心即是反制）
                Vector2 anchor = context.Target.Center + context.Target.velocity * 8f;
                context.Master.ai[0] = anchor.X;
                context.Master.ai[1] = anchor.Y;
                context.Master.ai[SlotVictim] = 0f;
                context.Master.ai[SlotFlingAngle] = 0f;
                npc.netUpdate = true;

                for (int i = 0; i < MirrorCount; i++) {
                    Projectile.NewProjectile(npc.GetSource_FromAI(), anchor, Vector2.Zero,
                        ModContent.ProjectileType<BrainMirrorImage>(), 0, 0f, Main.myPlayer,
                        BrainMirrorImage.PackMode(BrainMirrorImage.ModeSeizeRing, i), anchor.X, anchor.Y);
                }
            }

            //摄心低语：与其他招式区分的专属预兆声
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Zombie103 with { Volume = 0.75f, Pitch = -0.55f, MaxInstances = 2 }, npc.Center);
                SoundEngine.PlaySound(SoundID.ForceRoarPitched with { Volume = 0.5f, Pitch = -0.85f }, npc.Center);
            }
        }

        public override IBrainState OnUpdate(BrainStateContext context) {
            NPC npc = context.Npc;
            Timer++;

            npc.damage = 0;
            context.HideFromMinions = false;
            Vector2 anchor = Anchor(context);
            int victim = VictimIndex(context);

            //表现时钟：ai[4] 上升沿起算（与镜像模式翻转同包抵达，各端自洽）
            presentTick = victim >= 0 ? presentTick < 0 ? 0 : presentTick + 1 : -1;
            if (presentTick == 0 && !captureSeen) {
                captureSeen = true;
                //捕获顿帧重拍在上升沿统一触发：滞后客户端此刻可能仍在收环分支，放在连段表现里会丢拍
                BrainHeartbeat.Thump(1.45f, 0.93f);
                if (!VaultUtils.isServer) {
                    BrainHeartbeat.PlayThumpSound(anchor, 1.05f, 0.06f);
                    BrainMotion.FleshSquish(anchor, 0.9f, -0.2f);
                    BrainMotion.Roar(npc.Center, 0.9f, -0.45f);
                    BrainMotion.Shake(anchor, 6f, 12);
                }
            }

            //硬超时兜底：任何流程异常都保证退出
            if (Timer >= HardTimeout && !VaultUtils.isClient) {
                return new BrainHoverState();
            }

            //收环段：Snap 前所有端播预告；Snap 帧服务端裁决
            if (Timer <= SnapTick) {
                UpdateContractPhase(context, anchor);
                if (!VaultUtils.isClient && Timer >= SnapTick && !snapResolved) {
                    ResolveSnap(context, anchor);
                }
                return null;
            }

            //Snap 后：服务端按裁决分支；客户端凭同步痕迹推断（标记>0=连段，镜像全灭=落空，否则滞留等包）
            //captureSeen 防掷飞后清标记被误判为落空
            bool isWhiff = VaultUtils.isClient ? !captureSeen && victim < 0 && MirrorsGone() : whiffed;
            if (victim < 0 && !isWhiff && !captureSeen) {
                UpdateContractPhase(context, anchor);
                return null;
            }
            if (isWhiff) {
                return UpdateWhiffPhase(context);
            }

            return UpdateComboPhase(context, anchor, victim);
        }

        #region 收环段

        /// <summary>收环预告：镜环自缩（镜像自驱），真身环外游曳注视，节拍与灯效渐强</summary>
        private void UpdateContractPhase(BrainStateContext context, Vector2 anchor) {
            NPC npc = context.Npc;
            float progress = MathHelper.Clamp(Timer / (float)SnapTick, 0f, 1f);
            context.BeatIntensity = 0.7f + 0.25f * progress;
            context.TelegraphGlow = 0.25f + 0.55f * progress;
            context.EyeGlint = progress * 0.5f;

            //真身：环外慢速游曳，凝视环心（不参与收环，威压来自旁观）
            if (!VaultUtils.isClient) {
                float prowlAngle = Timer * 0.010f + MathHelper.Pi * 0.35f;
                Vector2 prowlPos = anchor + prowlAngle.ToRotationVector2() * (RingRadius(Timer) + 330f);
                BrainMotion.SpringHover(npc, prowlPos, 0.02f, 0.11f, 22f);
            }

            //每步收缩落位的湿滑挤压音+轻震（与镜像 SharpOut 落位同帧感）
            if (Timer is 24 or 48 or 72 or 90 && !VaultUtils.isServer) {
                float k = Timer / (float)SnapTick;
                BrainHeartbeat.Thump(0.65f + 0.3f * k);
                if (BrainMotion.OnScreen(anchor, 900f)) {
                    BrainMotion.FleshSquish(anchor, 0.55f + 0.25f * k, -0.6f + 0.35f * k);
                }
            }

            //环心聚势血雾：向锚点收束的冷紫低语
            if (!VaultUtils.isServer && Timer % 6 == 0 && BrainMotion.OnScreen(anchor)) {
                float r = RingRadius(Timer) * 0.8f;
                Vector2 pos = anchor + Main.rand.NextVector2CircularEdge(r, r);
                var mist = PRTLoader.NewParticle<PRT_BrainBloodMist>(pos, (anchor - pos) * 0.03f,
                    Color.Lerp(BrainMotion.MirrorCold, BrainMotion.BloodDark, Main.rand.NextFloat(0.5f)) * 0.7f,
                    Main.rand.NextFloat(0.6f, 1f));
                mist?.Configure(Main.rand.Next(26, 40));
            }

            Lighting.AddLight(anchor, BrainMotion.MirrorCold.ToVector3() * 0.5f * progress);
        }

        /// <summary>服务端裁决：环心内最近的存活玩家被摄住，否则落空</summary>
        private void ResolveSnap(BrainStateContext context, Vector2 anchor) {
            snapResolved = true;
            NPC npc = context.Npc;

            Player caught = null;
            float best = CaptureRadius;
            foreach (var player in Main.ActivePlayers) {
                if (!player.Alives()) {
                    continue;
                }
                //已被其他脑摄持的玩家不重复捕获（双脑同场的兜底）
                if (BrainMindSeizePlayer.FindSeizingBrain(player.whoAmI, out _) != null) {
                    continue;
                }
                float dist = player.Distance(anchor);
                if (dist <= best) {
                    best = dist;
                    caught = player;
                }
            }

            if (caught == null) {
                //落空：镜阵内爆自碎，缩短冷却允许更快再试（力竭窗在 whiff 表现层各端统一设置）
                whiffed = true;
                KillSeizeMirrors();
                context.MindSeizeCooldown = Math.Min(context.MindSeizeCooldown, CooldownWhiff);
                npc.netUpdate = true;
                return;
            }

            //捕获：写受害者与掷飞角（上半球随机，保证向上抛掷不砸地），点名偶数槽镜像转穿刺
            //残留血珠一并清场：被定身的玩家躲不了场外杂伤，连段期间伤害只许来自脚本
            comboTick = 0;
            context.Master.ai[SlotVictim] = caught.whoAmI + 1;
            context.Master.ai[SlotFlingAngle] = -MathHelper.PiOver2 + Main.rand.NextFloat(-1.15f, 1.15f);
            FlipPierceMirrors();
            KillStrayShards();
            npc.netUpdate = true;
        }

        /// <summary>捕获清场：杀掉残留的血珠/壳片（不动摄心镜像）</summary>
        private static void KillStrayShards() {
            int shard = ModContent.ProjectileType<BrainBloodShard>();
            int shell = ModContent.ProjectileType<BrainShellFragment>();
            foreach (var proj in Main.ActiveProjectiles) {
                if (proj.type == shard || proj.type == shell) {
                    proj.Kill();
                }
            }
        }

        #endregion

        #region 落空段

        private IBrainState UpdateWhiffPhase(BrainStateContext context) {
            NPC npc = context.Npc;
            whiffTimer++;

            //力竭窗各端统一设置：On_ModifyIncomingHit 在受击结算端本地读它，只在服务端设会导致数字不一致
            if (whiffTimer == 1) {
                context.FalterTimer = Math.Max(context.FalterTimer, WhiffFalter);
            }

            //力竭踉跄：垂速下沉+发光熄灭
            npc.velocity *= 0.94f;
            npc.velocity.Y += 0.04f;
            context.TelegraphGlow = MathHelper.Clamp(0.5f - whiffTimer / 40f, 0f, 1f) * 0.5f;
            context.BeatIntensity = 0.5f;

            //内爆瞬间的塌陷重拍（镜像 OnKill 的六连碎裂自带音效）
            if (whiffTimer == 2) {
                BrainHeartbeat.Thump(1.0f, 0.88f);
            }

            if (whiffTimer >= 54 && !VaultUtils.isClient) {
                return new BrainHoverState();
            }
            return null;
        }

        #endregion

        #region 连段

        /// <summary>捕获后的连段：定身持环→穿刺三拍→真身蓄力贯穿→掷飞→恢复</summary>
        private IBrainState UpdateComboPhase(BrainStateContext context, Vector2 anchor, int victimIndex) {
            NPC npc = context.Npc;
            Player victim = victimIndex >= 0 && victimIndex < Main.maxPlayers ? Main.player[victimIndex] : null;
            float flingAngle = FlingAngle(context);
            Vector2 flingDir = flingAngle.ToRotationVector2();

            //服务端权威时钟推进与异常出口
            if (!VaultUtils.isClient && comboTick >= 0) {
                comboTick++;

                //受害者死亡/离线/被外力挪走→立刻断投（受害端凭状态翻转自解）
                if (comboTick < FlingTick
                    && (!victim.Alives() || victim.Distance(anchor) > BreakDistance)) {
                    return new BrainHoverState();
                }

                UpdateBrainMotionServer(npc, anchor, flingDir);

                //掷飞帧：撞散残余镜阵，清受害者标记（受害端已按本地时钟先行掷飞解锁）
                if (comboTick == FlingTick + 10) {
                    context.Master.ai[SlotVictim] = 0f;
                    npc.netUpdate = true;
                }
                if (comboTick >= ComboEndTick) {
                    return new BrainHoverState();
                }
            }

            UpdateComboPresentation(context, anchor, victim, flingDir);
            return null;
        }

        /// <summary>服务端真身运动：滑向掷向反位→终结收势→直线贯穿→急刹恢复</summary>
        private void UpdateBrainMotionServer(NPC npc, Vector2 anchor, Vector2 flingDir) {
            if (comboTick < FinisherReelTick) {
                //滑向发起位：锚点沿掷向反方向外推
                Vector2 launchPos = anchor - flingDir * BrainLaunchDist;
                BrainMotion.SpringHover(npc, launchPos, 0.028f, 0.13f, 26f);
                return;
            }
            if (comboTick < FinisherDashTick) {
                //收势：末段骤然后撑（pow 末爆语法）
                float t = (comboTick - FinisherReelTick) / (float)(FinisherDashTick - FinisherReelTick);
                npc.velocity = -flingDir * (float)Math.Pow(t, 6) * 18f;
                return;
            }
            if (comboTick == FinisherDashTick) {
                //贯穿出发：定速定向，恰在 FlingTick 掠过锚点
                float speed = BrainLaunchDist / (FlingTick - FinisherDashTick);
                npc.velocity = flingDir * speed;
                npc.netUpdate = true;
                return;
            }
            if (comboTick == FlingTick) {
                //撞散镜阵（残余驻环镜像全碎）
                KillSeizeMirrors();
                return;
            }
            if (comboTick > FlingTick + 4) {
                //贯穿后急刹进入恢复拍
                npc.velocity *= 0.88f;
            }
        }

        /// <summary>各端连段表现：黑幕、定身血雾、节拍重音、眼芒（由 presentTick 驱动，旁观者可见完整动作）</summary>
        private void UpdateComboPresentation(BrainStateContext context, Vector2 anchor, Player victim, Vector2 flingDir) {
            if (presentTick < 0) {
                return;
            }

            //持环期黑幕与静默心跳：全部重音走脚本拍
            bool holding = presentTick < FlingTick;
            context.BlackoutTarget = holding ? 0.3f : 0f;
            context.BeatSilenced = holding;
            context.TelegraphGlow = holding ? 0.45f : 0f;

            //穿刺三拍重音（与镜像掠心同帧感；捕获重拍已在上升沿统一触发）
            for (int i = 0; i < PierceHurtTicks.Length; i++) {
                if (presentTick == PierceHurtTicks[i]) {
                    BrainHeartbeat.Thump(1.1f + i * 0.08f, 0.91f);
                    if (!VaultUtils.isServer) {
                        BrainMotion.Shake(anchor, 3.5f + i * 0.8f, 9);
                        BrainMotion.BloodMistBurst(anchor, 1f + i * 0.15f, 5, 7f);
                    }
                }
            }

            //真身终结收势的眼芒与蓄力吼
            if (presentTick >= FinisherReelTick && presentTick < FlingTick) {
                context.EyeGlint = MathHelper.Clamp(
                    (presentTick - FinisherReelTick) / (float)(FinisherDashTick - FinisherReelTick), 0f, 1f);
                if (presentTick == FinisherDashTick && !VaultUtils.isServer) {
                    BrainMotion.Roar(context.Npc.Center, 1.1f, 0.05f, true);
                }
            }

            //掷飞帧：全场最重的一拍
            if (presentTick == FlingTick) {
                BrainHeartbeat.Thump(1.5f, 0.94f);
                if (!VaultUtils.isServer) {
                    BrainMotion.Shake(anchor, 9f, 16);
                    BrainMotion.BloodMistBurst(anchor, 2.2f, 14, 10f);
                    PRTLoader.NewParticle<PRT_StarPulseRing>(anchor, Vector2.Zero,
                        BrainMotion.MirrorCold, 0.07f)?.Configure(0.05f, 0.5f, 18);
                    SoundEngine.PlaySound(SoundID.Shatter with { Volume = 0.8f, Pitch = -0.2f, MaxInstances = 3 }, anchor);
                }
            }

            //定身悬空的摄心血雾：环心冷紫雾+内吸光屑（旁观者同样可见）
            if (holding && !VaultUtils.isServer && presentTick % 5 == 0 && BrainMotion.OnScreen(anchor)) {
                Vector2 holdPos = victim.Alives() ? victim.Center : anchor;
                var mist = PRTLoader.NewParticle<PRT_BrainBloodMist>(
                    holdPos + Main.rand.NextVector2Circular(34f, 40f), Vector2.UnitY * -0.4f,
                    Color.Lerp(BrainMotion.MirrorCold, BrainMotion.BloodBright, Main.rand.NextFloat(0.35f)) * 0.75f,
                    Main.rand.NextFloat(0.5f, 0.9f));
                mist?.Configure(Main.rand.Next(22, 36));
                for (int i = 0; i < 2; i++) {
                    Vector2 pos = holdPos + Main.rand.NextVector2CircularEdge(70f, 70f);
                    PRTLoader.NewParticle<PRT_Spark>(pos, (holdPos - pos) * 0.05f,
                        Color.Lerp(BrainMotion.MirrorCold, Color.White, Main.rand.NextFloat(0.4f)),
                        Main.rand.NextFloat(0.45f, 0.8f))?.Configure(true, Main.rand.Next(10, 16));
                }
                Lighting.AddLight(holdPos, BrainMotion.MirrorCold.ToVector3() * 0.6f);
            }
        }

        #endregion

        #region 清场工具

        /// <summary>是否已无摄心镜像（客户端落空推断用）</summary>
        private static bool MirrorsGone() {
            int mirrorType = ModContent.ProjectileType<BrainMirrorImage>();
            foreach (var proj in Main.ActiveProjectiles) {
                if (proj.type != mirrorType) {
                    continue;
                }
                int mode = (int)proj.ai[0] / 100;
                if (mode is BrainMirrorImage.ModeSeizeRing or BrainMirrorImage.ModeSeizePierce) {
                    return false;
                }
            }
            return true;
        }

        /// <summary>偶数槽镜像转穿刺模式（服务端改写 ai[0] 一次性同步）</summary>
        private static void FlipPierceMirrors() {
            int mirrorType = ModContent.ProjectileType<BrainMirrorImage>();
            foreach (var proj in Main.ActiveProjectiles) {
                if (proj.type != mirrorType || (int)proj.ai[0] / 100 != BrainMirrorImage.ModeSeizeRing) {
                    continue;
                }
                int slot = (int)proj.ai[0] % 100;
                if (slot % 2 == 0) {
                    proj.ai[0] = BrainMirrorImage.PackMode(BrainMirrorImage.ModeSeizePierce, slot);
                    proj.netUpdate = true;
                }
            }
        }

        /// <summary>清空全部摄心镜像（落空内爆/掷飞撞散/异常出口共用）</summary>
        private static void KillSeizeMirrors() {
            int mirrorType = ModContent.ProjectileType<BrainMirrorImage>();
            foreach (var proj in Main.ActiveProjectiles) {
                if (proj.type != mirrorType) {
                    continue;
                }
                int mode = (int)proj.ai[0] / 100;
                if (mode is BrainMirrorImage.ModeSeizeRing or BrainMirrorImage.ModeSeizePierce) {
                    proj.Kill();
                }
            }
        }

        #endregion

        public override void OnExit(BrainStateContext context) {
            base.OnExit(context);
            NPC npc = context.Npc;
            npc.damage = npc.defDamage;
            //任何出口都清残镜与受害者标记（受害端凭状态/标记翻转自解并获无敌帧）
            if (!VaultUtils.isClient) {
                KillSeizeMirrors();
                context.Master.ai[SlotVictim] = 0f;
                context.Master.ai[SlotFlingAngle] = 0f;
                npc.netUpdate = true;
            }
        }
    }
}
