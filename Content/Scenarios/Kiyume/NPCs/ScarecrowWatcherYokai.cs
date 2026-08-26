using CalamityOverhaul.Content.Scenarios.Kiyume.Gen;
using CalamityOverhaul.Content.Scenarios.Kiyume.Stealth;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Kiyume.NPCs
{
    /// <summary>
    /// 守田人（P4 §2.3）：旱田里的稻草人，只在无人观测时行动。<br/>
    /// 观测口径 = <see cref="KiyumeStealthSense.ObservedByAnyPlayer"/>（全玩家保守视窗并集 +
    /// 解析雾盲 ScareFogBlind，天使雕像正典）；30t 一拍服务器掷 挪步60/消隐15/复现25。
    /// 挪步是瞬移不是走路：锁帧 0、位置变更显式 SyncNPC 原子过线、每拍清 netOffset
    /// （SyncNPC 收包端会把 &lt;800px 位移折进 netOffset 滑行补间，不清就会「走过去」）。<br/>
    /// 袭击：贴身 ≤60px 且该玩家连续 ≥120t 未观测 → 收割一击（70，单次）后散作干草，
    /// 全程唯一一声就在这一拍。识破（≤300px 内被观测）→ 冻结，600px 内有人期间保持死物
    /// （含袭击），全员离开累计 300t 解冻。玩家击杀走 OnKill：上报噪声（裁决11，
    /// 枪杀稻草人会引狗）+ 盲拆惩罚（未被观测者立即免费行动一次）。<br/>
    /// 联机：裁决全服务器；per-player 未观测累计存服务器侧实例数组（不入同步）；
    /// 消隐回导演补员池、复现/补员出池（scarecrowPoolLeft），击杀不回池——池耗尽即终局
    /// </summary>
    internal class ScarecrowWatcherYokai : KiyumeYokaiNPC
    {
        public override string Texture => "Terraria/Images/NPC_" + NPCID.Scarecrow1;

        //──── 状态（ai[0]）；ai[3]=样式种子（0/1/2 → Scarecrow1/2/4） ────
        private const int StateStand = 0;
        private const int StateFrozen = 1;
        private const int StateStruck = 2;

        //袭击拍后的滞留窗（tick）：让状态沿先过线，各端播完散草再消失
        private const int StruckLingerTicks = 6;
        //挪步落点探地：自当前脚位抬升起探（px）/ 向下探行数（容 ±4~10 格坡，超出弃权）
        private const float StepProbeLiftPx = 64f;
        private const int StepProbeRows = 14;
        //复现落点探地行数（从玩家高度向下，容沟壑）
        private const int ReturnProbeRows = 40;
        //平地探测回退窗（tile 列）：滩涂东段，覆盖裁决17 旱田 [516,558]；连续 ≥6 列高差 ≤1
        private const int FallbackMinRun = 6;
        private const int FallbackMaxRun = 40;
        private static int FallbackScanL => KiyumeMetrics.ShoalLeft + 150;
        private static int FallbackScanR => KiyumeMetrics.SpawnReserveLeft - 6;
        //陈草色（GetAlpha 乘色，场景血暮光在 drawColor 里自然叠上）
        private static readonly Color StrawMul = new(200, 160, 110);

        //──── 服务器侧裁决量（不入同步；per-player 挂实例数组，禁 static） ────
        //连续未观测计数；stamp 断拍即重计（槽位换人/重生不继承旧账）
        private readonly int[] unseenTicks = new int[Main.maxPlayers];
        private readonly uint[] unseenStamp = new uint[Main.maxPlayers];
        //30t 行动判定钟（whoAmI 错峰起拍，避免全场同帧齐跳）
        private int judgeClock = -1;

        //ai[3] 样式种子 → 原版稻草人贴图（三款混编，305/306/308 帧数皆 6，已对源帧表）
        private int StyleNpcId => (int)StackCount switch {
            1 => NPCID.Scarecrow2,
            2 => NPCID.Scarecrow4,
            _ => NPCID.Scarecrow1,
        };

        protected override void SetYokaiStaticDefaults() {
            Main.npcFrameCount[Type] = 6;
            //瞬移怪必须常驻绘制（底座合同：基类不代设，子类自设）
            NPCID.Sets.MustAlwaysDraw[Type] = true;
            NPCID.Sets.SpecificDebuffImmunity[Type][BuffID.Confused] = true;
        }

        protected override void SetYokaiDefaults() {
            //尺寸对源原版稻草人（NPC.cs type 305..314：18×40）
            NPC.width = 18;
            NPC.height = 40;
            NPC.damage = 0;    //无接触伤：唯一伤害是收割一击（服务器 Hurt 路径）
            NPC.defense = 0;
            NPC.lifeMax = KiyumeYokaiMetrics.ScareLife;
            NPC.knockBackResist = 0f;   //死物不挪窝
            NPC.aiStyle = -1;
            NPC.npcSlots = 0f;
            NPC.alpha = 0;   //出生透明度显式（VFX 高复发缺陷②）
            //挨打的干草窸窣是玩家动作的回馈，不算它自己出声；死亡表现全在 HitEffect，无人形死音
            NPC.HitSound = SoundID.Grass;
            NPC.DeathSound = null;
        }

        //接触判定永不伤人：贴身站着也安全，直到它出手那一拍
        public override bool CanHitPlayer(Player target, ref int cooldownSlot) => false;

        /// <summary>锁帧 0：行走动画永不播，玩家永远看不到它动</summary>
        public override void FindFrame(int frameHeight) {
            NPC.frame.Y = 0;
        }

        public override Color? GetAlpha(Color drawColor)
            => drawColor.MultiplyRGB(StrawMul) * NPC.Opacity;

        //==================== 行为 ====================

        protected override void YokaiAI() {
            AmbientClock++;
            //瞬移是唯一位移：横速死锁；netOffset 每拍清零（见类注），位置变更即原子跳变
            NPC.velocity.X = 0f;
            NPC.netOffset = Vector2.Zero;

            if (StateEdge() && (int)State == StateStruck) {
                StruckBurst();
            }

            if ((int)State == StateStruck) {
                //各端确定性滞留后消失，服务器补发同步兜底迟到端
                if (++StateTimer >= StruckLingerTicks) {
                    Despawn();
                }
                return;
            }

            HealAlpha(0);

            if (VaultUtils.isClient) {
                return;   //裁决全在服务器，客户端只从 ai/SyncNPC 重放
            }
            ServerThink();
        }

        private void ServerThink() {
            AccumulateUnseen(out float nearestDist);

            if ((int)State == StateFrozen) {
                //识破后：600px 内有人就保持死物（行动与袭击一并封死）；全员离开累计 300t 解冻
                if (nearestDist <= KiyumeYokaiMetrics.ScareFreezeHoldRange) {
                    StateTimer = 0f;
                }
                else if (++StateTimer >= KiyumeYokaiMetrics.ScareRefreezeTicks) {
                    ChangeState(StateStand);
                }
                return;
            }

            //袭击独立于行动节拍：贴身且该玩家连续未观测足时，逐 tick 查（120t 累计本就逐 tick）
            if (TryStrike()) {
                return;
            }

            //30t 一拍行动判定
            if (judgeClock < 0) {
                judgeClock = NPC.whoAmI % KiyumeYokaiMetrics.ScareJudgeInterval;
            }
            if (++judgeClock < KiyumeYokaiMetrics.ScareJudgeInterval) {
                return;
            }
            judgeClock = 0;

            //观测判定：正式版反向观测通道（裁决10），任何人看着都不许动
            bool observed = KiyumeStealthSense.ObservedByAnyPlayer(
                NPC.Hitbox, KiyumeYokaiMetrics.ScareFogBlind);
            if (observed) {
                //≤300px 内被观测 = 识破 → 冻结
                if (nearestDist <= KiyumeYokaiMetrics.ScareSpotRange) {
                    ChangeState(StateFrozen);
                }
                return;
            }
            ActOnce();
        }

        /// <summary>
        /// 逐 tick 维护 per-player「连续未观测」与最近距离。
        /// 视窗构造与 <see cref="KiyumeStealthSense.ObservedByAnyPlayer"/> 同一套保守常量
        /// （KiyumeHoundMetrics.Observe*），雾盲同阈——这是官方并集口径的逐玩家分解
        /// </summary>
        private void AccumulateUnseen(out float nearestDist) {
            nearestDist = float.MaxValue;
            bool fogBlind = KiyumeStealthSense.FogConcealmentAt(NPC.Center)
                >= KiyumeYokaiMetrics.ScareFogBlind;
            Rectangle hitbox = NPC.Hitbox;
            foreach (Player player in Main.ActivePlayers) {
                int who = player.whoAmI;
                if (player.dead || player.ghost) {
                    unseenTicks[who] = 0;
                    unseenStamp[who] = Main.GameUpdateCount;
                    continue;
                }
                //断拍（掉线换人/中途进场）不吃旧账，从零重计
                if (unseenStamp[who] != Main.GameUpdateCount - 1) {
                    unseenTicks[who] = 0;
                }
                unseenStamp[who] = Main.GameUpdateCount;

                var view = new Rectangle(
                    (int)player.Center.X - KiyumeHoundMetrics.ObserveHalfWidthPx,
                    (int)player.Center.Y - KiyumeHoundMetrics.ObserveHalfHeightPx,
                    KiyumeHoundMetrics.ObserveHalfWidthPx * 2,
                    KiyumeHoundMetrics.ObserveHalfHeightPx * 2);
                bool sees = !fogBlind && view.Intersects(hitbox);
                unseenTicks[who] = sees ? 0 : unseenTicks[who] + 1;

                float dist = Vector2.Distance(player.Center, NPC.Center);
                if (dist < nearestDist) {
                    nearestDist = dist;
                }
            }
        }

        /// <summary>贴身 ≤60px 且该玩家连续 ≥120t 未观测 → 收割一击（单次），随即自毁散干草</summary>
        private bool TryStrike() {
            foreach (Player player in Main.ActivePlayers) {
                if (player.dead || player.ghost
                    || unseenTicks[player.whoAmI] < KiyumeYokaiMetrics.ScareUnseenTicks
                    || Vector2.Distance(player.Center, NPC.Center) > KiyumeYokaiMetrics.ScareStrikeRange) {
                    continue;
                }
                //服务器 Hurt 只写本端镜像（对源：发包分支仅 netMode==1 且 myPlayer），
                //联机须显式广播 HurtInfo，受害端收包按 info 原值结算；击退方向背离稻草人
                double dealt = player.Hurt(PlayerDeathReason.ByNPC(NPC.whoAmI),
                    KiyumeYokaiMetrics.ScareStrikeDamage,
                    player.Center.X < NPC.Center.X ? -1 : 1, out Player.HurtInfo info);
                if (dealt > 0.0 && VaultUtils.isServer) {
                    NetMessage.SendPlayerHurt(player.whoAmI, info);
                }
                ChangeState(StateStruck);
                return true;
            }
            return false;
        }

        /// <summary>掷一次行动（挪步60/消隐15/复现25）；盲拆惩罚的免费行动也走这里</summary>
        internal void ActOnce() {
            Player near = NearestLivePlayer(NPC.Center);
            if (near == null) {
                return;
            }
            int roll = Main.rand.Next(100);
            if (roll < KiyumeYokaiMetrics.ScareWeightStep) {
                TryStep(near);
            }
            else if (roll < KiyumeYokaiMetrics.ScareWeightStep + KiyumeYokaiMetrics.ScareWeightVanish) {
                Vanish();
            }
            else {
                //复现：距最近玩家 240~480px 处悄悄多一个（落点同样要无人观测才成立）
                float x = near.Center.X + (Main.rand.NextBool() ? 1f : -1f)
                    * Main.rand.NextFloat(KiyumeYokaiMetrics.ScareReturnMin, KiyumeYokaiMetrics.ScareReturnMax);
                TrySpawnHidden(x, near.Center.Y);
            }
        }

        /// <summary>挪步：向最近玩家瞬移 32~80px，落点重探地；探地失败或落点入观测则该拍弃权</summary>
        private void TryStep(Player target) {
            float dx = target.Center.X - NPC.Center.X;
            float step = Main.rand.NextFloat(KiyumeYokaiMetrics.ScareStepMin, KiyumeYokaiMetrics.ScareStepMax);
            //钳到不越过玩家：留半个袭击距，贴到跟前就停手等那 120t
            step = MathF.Min(step, MathF.Abs(dx) - KiyumeYokaiMetrics.ScareStrikeRange * 0.5f);
            if (step < 4f) {
                return;
            }
            float landX = NPC.Center.X + MathF.Sign(dx) * step;
            if (!TryFindGround(landX, NPC.Bottom.Y - StepProbeLiftPx, StepProbeRows, out float groundY)) {
                return;   //宁静止不悬空
            }
            Rectangle landRect = HitboxAtBottom(new Vector2(landX, groundY));
            //落点也不许被看见：挪进视野边缘同样穿帮（宁可少动不穿帮）
            if (KiyumeStealthSense.ObservedByAnyPlayer(landRect, KiyumeYokaiMetrics.ScareFogBlind)) {
                return;
            }
            NPC.Bottom = new Vector2(landX, groundY);
            NPC.velocity = Vector2.Zero;
            //瞬移原子过线：位置变更立即显式 SyncNPC（case 23 全量包，位置+ai 一次到齐；
            //不再另置 netUpdate，免得同帧发两包）
            if (VaultUtils.isServer) {
                NetMessage.SendData(MessageID.SyncNPC, -1, -1, null, NPC.whoAmI);
            }
        }

        /// <summary>消隐：悄悄少一个，个体回补员池（击杀不回池，那才是真损耗）</summary>
        private void Vanish() {
            KiyumeHauntDirector inst = KiyumeHauntDirector.Instance;
            if (inst != null) {
                inst.scarecrowPoolLeft++;
            }
            Despawn();
        }

        private void Despawn() {
            NPC.active = false;
            if (VaultUtils.isServer) {
                NetMessage.SendData(MessageID.SyncNPC, -1, -1, null, NPC.whoAmI);
            }
        }

        /// <summary>袭击拍（StateEdge 各端一次）：全程唯一一声 + 干草爆散，身体就地散掉</summary>
        private void StruckBurst() {
            SoundEngine.PlaySound(SoundID.Item1 with { Volume = 0.9f, Pitch = -0.4f }, NPC.Center);
            NPC.alpha = 255;
            NPC.dontTakeDamage = true;   //滞留窗内的空壳不再吃伤害（防补刀触发盲拆惩罚）
            if (Main.dedServ) {
                return;
            }
            for (int i = 0; i < 20; i++) {
                Dust dust = Dust.NewDustDirect(NPC.position, NPC.width, NPC.height, DustID.Hay,
                    Main.rand.NextFloat(-2.5f, 2.5f), Main.rand.NextFloat(-3f, 0.5f));
                dust.noGravity = Main.rand.NextBool(3);
            }
        }

        //==================== 死亡（玩家击杀路径；自毁走 Despawn 不进这里） ====================

        public override void OnKill() {
            //枪声草响挂上听觉地图：量级取开火脉冲同阶（FirePulse 1.0 × WeaponImpulse）
            KiyumeStealthSense.ReportNoise(NPC.Center, KiyumeHoundMetrics.WeaponImpulse);
            //盲拆惩罚：场上所有未被观测的守田人立即免费行动一次（识破冻结者除外，死物保持死物）。
            //先快照后行动：ActOnce 的复现分支会出池新增个体，边遍历边动名单
            //会让同帧新生个体也吃到免费行动并可能链式出池，一枪抽干补员池
            List<ScarecrowWatcherYokai> pending = [];
            foreach (NPC other in Main.ActiveNPCs) {
                if (other.whoAmI == NPC.whoAmI || other.type != Type
                    || other.ModNPC is not ScarecrowWatcherYokai watcher
                    || (int)other.ai[0] != StateStand) {
                    continue;
                }
                if (KiyumeStealthSense.ObservedByAnyPlayer(other.Hitbox, KiyumeYokaiMetrics.ScareFogBlind)) {
                    continue;
                }
                pending.Add(watcher);
            }
            foreach (ScarecrowWatcherYokai watcher in pending) {
                if (watcher.NPC.active) {
                    watcher.ActOnce();
                }
            }
        }

        public override void HitEffect(NPC.HitInfo hit) {
            if (Main.dedServ) {
                return;
            }
            int count = NPC.life > 0 ? 4 : 20;
            for (int i = 0; i < count; i++) {
                Dust dust = Dust.NewDustDirect(NPC.position, NPC.width, NPC.height, DustID.Hay,
                    hit.HitDirection * Main.rand.NextFloat(0.5f, 2f), Main.rand.NextFloat(-2.5f, 0.5f));
                if (NPC.life > 0) {
                    dust.velocity *= 0.6f;
                }
            }
        }

        //==================== 绘制：三款原版稻草人混编，无 shader，恐怖在于完全普通 ====================

        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            //上游实体批状态泄漏自愈（netcode 7.2）
            BeginDefault(spriteBatch);
            if ((int)State == StateStruck) {
                return false;   //散作干草之后没有身体
            }
            int styleId = StyleNpcId;
            Main.instance.LoadNPC(styleId);
            Texture2D tex = TextureAssets.Npc[styleId]?.Value;
            if (tex == null) {
                return false;
            }
            int frameH = tex.Height / Main.npcFrameCount[styleId];
            var src = new Rectangle(0, 0, tex.Width, frameH);   //锁帧 0（站立帧）
            //原版站姿锚地：底对齐 + 原版帧内 4px 余量
            Vector2 drawPos = new Vector2(
                NPC.position.X + NPC.width * 0.5f - tex.Width * 0.5f,
                NPC.position.Y + NPC.height - frameH + 4f + NPC.gfxOffY) - screenPos;
            Color col = GetAlpha(drawColor) ?? drawColor;
            spriteBatch.Draw(tex, drawPos, src, col, 0f, Vector2.Zero, 1f, SpriteEffects.None, 0f);
            return false;
        }

        //==================== 布防/补员静态面（导演泵与本体共用，全部服务器侧调用） ====================

        /// <summary>
        /// 田块解析：ScarecrowPlot 非空用之（W3 生成端写入）；null 走平地探测回退——
        /// 滩涂东段窗内找最长的「相邻列地板高差 ≤1」平段，不足 6 列则本会话无田
        /// </summary>
        internal static Rectangle? ResolvePlot() {
            if (KiyumeStructures.ScarecrowPlot != null) {
                return KiyumeStructures.ScarecrowPlot;
            }
            int bestStart = -1, bestLen = 0;
            int runStart = FallbackScanL;
            int minRow = int.MaxValue, maxRow = int.MinValue;
            for (int x = FallbackScanL; x < FallbackScanR; x++) {
                int row = KiyumePlans.FloorTopAt(x);
                if (Math.Max(maxRow, row) - Math.Min(minRow, row) <= 1) {
                    minRow = Math.Min(minRow, row);
                    maxRow = Math.Max(maxRow, row);
                }
                else {
                    runStart = x;
                    minRow = row;
                    maxRow = row;
                }
                int len = x - runStart + 1;
                if (len > bestLen) {
                    bestLen = len;
                    bestStart = runStart;
                }
            }
            if (bestLen < FallbackMinRun) {
                return null;
            }
            int width = Math.Min(bestLen, FallbackMaxRun);
            int fieldX = bestStart + (bestLen - width) / 2;   //取平段中央一节当田
            var plot = new Rectangle(fieldX, KiyumePlans.FloorTopAt(fieldX + width / 2) - 1, width, 2);
            //运行时回退回填单一真相：P5 静默区门读同一字段即自然覆盖回退场地；
            //Reset 时随注册表清零（骨架 pass 每次重生成前跑，无跨会话泄漏）
            KiyumeStructures.ScarecrowPlot = plot;
            return plot;
        }

        /// <summary>一次性布防：沿田带均布 ScareFieldInit 只（初始不耗池、不查观测——入梦时它们本就该站在那）</summary>
        internal static void SeedField() {
            Rectangle? plotOpt = ResolvePlot();
            if (plotOpt == null) {
                return;   //连平地都探不出：这一梦没有田，守田人缺席
            }
            Rectangle plot = plotOpt.Value;
            int type = ModContent.NPCType<ScarecrowWatcherYokai>();
            for (int i = 0; i < KiyumeYokaiMetrics.ScareFieldInit; i++) {
                float t = (i + 0.5f) / KiyumeYokaiMetrics.ScareFieldInit;
                int col = plot.X + (int)(t * plot.Width) + Main.rand.Next(-1, 2);
                col = Math.Clamp(col, plot.X, plot.X + Math.Max(plot.Width - 1, 0));
                KiyumeHauntDirector.SpawnYokai(type,
                    new Vector2(col * 16f + 8f, KiyumePlans.FloorTopAt(col) * 16f),
                    ai3: Main.rand.Next(3));
            }
        }

        /// <summary>会话补员（导演泵兜底）：全场归零而池有余量时，在无人观测的田位悄悄补一只</summary>
        internal static void TryReplenishField() {
            //池耗尽即终局：田恢复为纯装饰，不再扫描
            if ((KiyumeHauntDirector.Instance?.scarecrowPoolLeft ?? 0) <= 0 || CountOnField() > 0) {
                return;
            }
            Rectangle? plot = ResolvePlot();
            if (plot == null) {
                return;
            }
            int col = plot.Value.X + Main.rand.Next(Math.Max(plot.Value.Width, 1));
            TrySpawnHidden(col * 16f + 8f, KiyumePlans.FloorTopAt(col) * 16f);
        }

        /// <summary>出池生成一只：池有余、未超上限、探得到地、落点无人观测，四关全过才成立</summary>
        private static bool TrySpawnHidden(float x, float probeFromY) {
            KiyumeHauntDirector inst = KiyumeHauntDirector.Instance;
            if (inst == null || inst.scarecrowPoolLeft <= 0
                || CountOnField() >= KiyumeYokaiMetrics.ScareCap) {
                return false;
            }
            if (!TryFindGround(x, probeFromY - 160f, ReturnProbeRows, out float groundY)) {
                return false;
            }
            Rectangle rect = HitboxAtBottom(new Vector2(x, groundY));
            if (KiyumeStealthSense.ObservedByAnyPlayer(rect, KiyumeYokaiMetrics.ScareFogBlind)) {
                return false;
            }
            int idx = KiyumeHauntDirector.SpawnYokai(ModContent.NPCType<ScarecrowWatcherYokai>(),
                new Vector2(x, groundY), ai3: Main.rand.Next(3));
            if (idx < 0 || idx >= Main.maxNPCs) {
                return false;
            }
            inst.scarecrowPoolLeft--;
            return true;
        }

        internal static int CountOnField() {
            int type = ModContent.NPCType<ScarecrowWatcherYokai>();
            int count = 0;
            foreach (NPC npc in Main.ActiveNPCs) {
                if (npc.type == type) {
                    count++;
                }
            }
            return count;
        }

        private static Player NearestLivePlayer(Vector2 from) {
            Player best = null;
            float bestDist = float.MaxValue;
            foreach (Player player in Main.ActivePlayers) {
                if (player.dead || player.ghost) {
                    continue;
                }
                float dist = Vector2.Distance(player.Center, from);
                if (dist < bestDist) {
                    bestDist = dist;
                    best = player;
                }
            }
            return best;
        }

        //从起始高度向下探地表（TryFindGround 语义同 KiyumeHoundShade：实心且非平台）
        private static bool TryFindGround(float x, float fromY, int probeRows, out float groundY) {
            int tileX = (int)(x / 16f);
            int tileY = (int)(fromY / 16f);
            for (int i = 0; i < probeRows; i++) {
                int y = tileY + i;
                if (!WorldGen.InWorld(tileX, y, 20)) {
                    break;
                }
                Tile tile = Framing.GetTileSafely(tileX, y);
                if (tile.HasTile && Main.tileSolid[tile.TileType] && !Main.tileSolidTop[tile.TileType]) {
                    groundY = y * 16f;
                    return true;
                }
            }
            groundY = 0f;
            return false;
        }

        private static Rectangle HitboxAtBottom(Vector2 bottom)
            => new((int)(bottom.X - 9f), (int)(bottom.Y - 40f), 18, 40);
    }
}
