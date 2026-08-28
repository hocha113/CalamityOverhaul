using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMoonLord.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMoonLord.Projectiles;
using System;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMoonLord.States
{
    /// <summary>
    /// 幻影协奏（四臂分声部）：基线火力网兼连接拍。上对错拍星球扇射（布阵声部），
    /// 下对同拍低位剪切波（自两肋交叉掠过玩家脚下，执行声部），头交叉波弹，
    /// 中段留一个可读的换位喘息。心脏与残口不客串炮口：缺手缺头的席位由真眼代射，
    /// 全员尽墨时该拍静默（裸露期真眼集群自有火力）。
    /// 出手前各声部抬手亮眼（预备动作兼弹幕预告，手部 AI 经 <see cref="BeatWindup"/> 查询）。
    /// 循环内非首位协奏走短变体（连接拍收紧，节拍表只保留前段）
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)MLordStateIndex.Concerto, typeof(MLordContext))]
    internal class MLordConcertoState : MLordStateBase
    {
        public override string StateName => "Concerto";
        public override MLordStateIndex StateIndex => MLordStateIndex.Concerto;

        /// <summary>公平阀（契约3）：此帧起进入连接拍，节拍表整体停射，
        /// 状态尾段保证无新弹幕的喘息窗</summary>
        internal const int BreatherStart = 200;
        internal const int FullLength = 300;
        /// <summary>短变体：时长与喘息点整体前移（连接拍不再每次都是完整五秒）</summary>
        internal const int ShortBreatherStart = 132;
        internal const int ShortLength = 192;
        /// <summary>声部预备窗帧长：出弹前抬手亮眼（预备动作兼预告，契约2）</summary>
        internal const int WindupLead = 20;

        //―――― 声部节拍（原始帧，运行期吃 Frames 压缩）――――
        private const int FanABeat = 24;
        private const int FanBBeat = 70;
        private const int ShearABeat = 46;
        private const int ShearBBeat = 148;
        private const int BoltStartBeat = 124;

        private int stateLength;
        private int breatherStart;

        public override void OnEnter(MLordContext context) {
            base.OnEnter(context);
            bool shortVariant = context.ConcertoShortVariant;
            stateLength = Frames(context, shortVariant ? ShortLength : FullLength);
            breatherStart = Frames(context, shortVariant ? ShortBreatherStart : BreatherStart);
        }

        public override IMLordState OnUpdate(MLordContext context) {
            Player target = context.Target;

            //核心横漂意图交给爬行系统：四手轮流抓点把身体拽向游走位
            float drift = (float)Math.Sin(context.FormationClock * 0.017f) * 260f;
            RequestMove(context, target.Center + new Vector2(drift, 0f) + MLordDirector.CoreHoverOffset, 0.6f);
            UpdateLean(context);

            if (!VaultUtils.isClient) {
                RunServerBeats(context);
            }

            Timer++;
            if (Timer >= stateLength) {
                return NextAttack(context);
            }
            return null;
        }

        /// <summary>服务端节拍表；喘息点起进入连接拍（无新弹幕，编队复位喘息）</summary>
        private void RunServerBeats(MLordContext context) {
            //喘息窗硬闸：连接拍内任何分支都射不出弹幕
            if (Timer >= breatherStart) {
                return;
            }
            int fanA = Frames(context, FanABeat);
            int fanB = Frames(context, FanBBeat);
            int shearA = Frames(context, ShearABeat);
            int shearB = Frames(context, ShearBBeat);
            int boltStart = Frames(context, BoltStartBeat);

            //错拍星球扇（上对声部）：上左先手，上右后半拍
            if (Timer == fanA) {
                SpawnOrbFan(context, preferSlot: 0);
            }
            if (Timer == fanB) {
                SpawnOrbFan(context, preferSlot: 1);
            }

            //低位剪切波（下对声部）：两肋同拍交叉掠向玩家脚下
            if (Timer == shearA || Timer == shearB) {
                SpawnLowShear(context);
            }

            //头部交叉波弹：整个协奏只齐射一次（无活头时真眼代射）
            if (Timer == boltStart) {
                SpawnHeadBoltCross(context);
            }
        }

        /// <summary>从偏好手槽（缺位就近换手→头→核心）放出持握星球扇</summary>
        private void SpawnOrbFan(MLordContext context, int preferSlot) {
            NPC origin = PickPart(context, preferSlot);
            if (origin == null) {
                return;
            }
            Vector2 aim = (context.Target.Center - origin.Center).SafeNormalize(Vector2.UnitY);
            int count = context.CoreExposed ? 6 : 5;
            int damage = ScaleDamage(context, MLordDirector.OrbDamage);
            int launchDelay = Frames(context, 52);
            for (int i = 0; i < count; i++) {
                float spread = MathHelper.Lerp(-0.52f, 0.52f, count <= 1 ? 0.5f : i / (float)(count - 1));
                Vector2 offset = aim.RotatedBy(spread) * 74f;
                Projectile.NewProjectile(origin.GetSource_FromAI(), origin.Center + offset, Vector2.Zero,
                    ModContent.ProjectileType<MLordOrbProj>(), damage, 0f, Main.myPlayer,
                    origin.whoAmI, 0f, launchDelay + i * 4);
            }
            origin.netUpdate = true;
        }

        /// <summary>头部交叉波弹：一发直指两发斜掠（无活头时真眼代射，心脏不开火）</summary>
        private void SpawnHeadBoltCross(MLordContext context) {
            NPC origin = context.Parts.Head >= 0 && context.Parts.HeadAlive
                ? Main.npc[context.Parts.Head] : MLordFacts.GetFreeEye(context.Npc, 2);
            if (origin == null) {
                return;
            }
            Vector2 muzzle = origin.Center + new Vector2(0f, 30f);
            Vector2 aim = (context.Target.Center - muzzle).SafeNormalize(Vector2.UnitY);
            int damage = ScaleDamage(context, MLordDirector.BoltDamage);
            float speed = context.CoreExposed ? 9.5f : 8f;
            for (int i = -1; i <= 1; i++) {
                Vector2 vel = aim.RotatedBy(i * 0.34f) * speed;
                Projectile.NewProjectile(origin.GetSource_FromAI(), muzzle, vel,
                    ModContent.ProjectileType<MLordBoltProj>(), damage, 0f, Main.myPlayer);
            }
        }

        /// <summary>下对同拍剪切波：两只下手各向玩家脚下点位掠射短扇波矢，弹道在低位交叉封走位（幻影眼已除役）</summary>
        private void SpawnLowShear(MLordContext context) {
            MLordPartsStatus parts = context.Parts;
            int damage = ScaleDamage(context, MLordDirector.BoltDamage);
            Vector2 aimPoint = context.Target.Center + new Vector2(0f, 90f);
            for (int slot = 2; slot < MLordPartsStatus.HandSlots; slot++) {
                if (!parts.HandAlive(slot) || parts.HandIndex(slot) < 0) {
                    continue;
                }
                NPC hand = Main.npc[parts.HandIndex(slot)];
                Vector2 aim = (aimPoint - hand.Center).SafeNormalize(Vector2.UnitY);
                for (int i = -1; i <= 1; i++) {
                    Projectile.NewProjectile(hand.GetSource_FromAI(), hand.Center + aim * 46f,
                        aim.RotatedBy(i * 0.16f) * 6.4f, ModContent.ProjectileType<MLordBoltProj>(), damage, 0f, Main.myPlayer);
                }
            }
        }

        /// <summary>优先取指定手槽，缺位就近换手→活头→真眼（心脏不客串炮口）；全无返回 null 该拍静默</summary>
        private static NPC PickPart(MLordContext context, int preferSlot) {
            MLordPartsStatus parts = context.Parts;
            int hand = parts.FirstAliveHand(preferSlot);
            if (hand >= 0) {
                return Main.npc[hand];
            }
            if (parts.HeadAlive && parts.Head >= 0) {
                return Main.npc[parts.Head];
            }
            return MLordFacts.GetFreeEye(context.Npc, preferSlot);
        }

        /// <summary>
        /// 该手槽声部预备窗进度 0~1（出弹前 <see cref="WindupLead"/> 帧内爬升）。
        /// 手部 AI 消费：抬手 + 亮眼 + 张掌，预备动作即弹幕预告（契约2）
        /// </summary>
        internal static float BeatWindup(MLordContext context, int stateTimer, int slot) {
            Span<int> beats = stackalloc int[2];
            int count = 0;
            if (slot == 0) {
                beats[count++] = FanABeat;
            }
            else if (slot == 1) {
                beats[count++] = FanBBeat;
            }
            else {
                beats[count++] = ShearABeat;
                beats[count++] = ShearBBeat;
            }
            int breather = context.ConcertoShortVariant ? ShortBreatherStart : BreatherStart;
            float best = 0f;
            for (int i = 0; i < count; i++) {
                int beat = MLordDirector.Frames(beats[i], context.DeathMode);
                //喘息窗后的节拍不会开火，不给预备（短变体裁掉的拍不抬手）
                if (beats[i] >= breather) {
                    continue;
                }
                int wait = beat - stateTimer;
                if (wait > 0 && wait <= WindupLead) {
                    best = Math.Max(best, 1f - wait / (float)WindupLead);
                }
            }
            return best;
        }

        /// <summary>头声部（交叉波弹单次齐射）的预备窗进度 0~1：出弹前额眼睁大提亮（头部姿态消费）</summary>
        internal static float HeadBoltWindup(MLordContext context, int stateTimer) {
            int breather = context.ConcertoShortVariant ? ShortBreatherStart : BreatherStart;
            if (BoltStartBeat >= breather) {
                return 0f;
            }
            int beat = MLordDirector.Frames(BoltStartBeat, context.DeathMode);
            int wait = beat - stateTimer;
            if (wait > 0 && wait <= WindupLead) {
                return 1f - wait / (float)WindupLead;
            }
            return 0f;
        }
    }
}
