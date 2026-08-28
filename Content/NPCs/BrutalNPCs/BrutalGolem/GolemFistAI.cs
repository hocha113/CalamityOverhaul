using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalGolem.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalGolem.Rendering;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalGolem.States.Fists;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using InnoVault.StateMachines;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalGolem
{
    /// <summary>拳 NPCOverride 基类，行为见 States.Fists</summary>
    internal abstract class GolemFistAI : BrutalNPCOverride
    {
        internal NPC body;
        internal Player player;
        internal GolemFistStateContext fistContext;
        internal VaultStateMachine<GolemFistStateContext> fistStateMachine;

        /// <summary>拳侧 -1左 / 1右</summary>
        protected abstract int Side { get; }

        public sealed override bool? CanBrutalOverride() {
            return null;
        }

        public sealed override void SetProperty() {
            fistContext = null;
            fistStateMachine = null;
            //拳血量为独立子目标：可打掉，削减连段密度
            npc.lifeMax = System.Math.Max((int)(npc.lifeMax * 1.4f), npc.lifeMax);
            npc.life = npc.lifeMax;
            npc.knockBackResist = 0f;
            NPCID.Sets.TrailingMode[npc.type] = 1;
            NPCID.Sets.TrailCacheLength[npc.type] = 12;
        }

        public override bool AI() {
            body = Main.npc[(int)npc.ai[GolemAiSlots.PartBodyIndex]];
            player = Main.player[npc.target];
            npc.aiStyle = -1;
            npc.netOffset = Vector2.Zero;
            npc.damage = 0;
            npc.dontTakeDamage = false;
            npc.noTileCollide = true;
            npc.noGravity = true;

            //躯干失效则拳自毁，服务端决策
            if (!GolemFacts.BodyValid(body)) {
                KillSelfOnServer();
                return false;
            }

            //免疫debuff
            for (int i = 0; i < npc.buffImmune.Length; i++) {
                npc.buffImmune[i] = true;
            }

            //原版 SetDefaults 出生 alpha=255（NPC.cs:7295），原版由 AI_047 淡入；
            //接管后必须自己降，否则拳被绘制为全透明（各端本地执行）
            if (npc.alpha > 0) {
                npc.alpha = System.Math.Max(npc.alpha - 12, 0);
            }
            //躯干沉地淡出时同步隐去：拳锚随躯干下沉，不淡出会留下贴地实体残影
            if (GolemFacts.GetStateIndex(body) == GolemStateIndex.Despawn) {
                npc.alpha = System.Math.Max(npc.alpha, body.alpha);
            }

            EnsureStateMachine();
            UpdateContext();

            //激怒惩罚与躯干同拍：防御翻倍(拳是独立子目标)
            npc.defense = fistContext.Enraged ? npc.defDefense * 2 : npc.defDefense;

            //服务端广播位置，客户端傀儡
            if (!VaultUtils.isClient) {
                npc.netUpdate = true;
            }

            GolemStateIndex bodyState = GolemFacts.GetStateIndex(body);
            int bodyPhase = (int)body.ai[GolemAiSlots.BodyPhase];

            //死亡演出兜底：进入坠地状态
            if (bodyPhase >= GolemPhase.DeathShow
                && (GolemFistStateIndex)(int)npc.ai[GolemAiSlots.PartStateSlot] != GolemFistStateIndex.DeathFall
                && !VaultUtils.isClient) {
                fistStateMachine.ChangeState(new GolemFistDeathFallState());
            }

            //仪式期强制收拢：打断在途拳（Anchor 态的指令分发覆盖不到已出手的拳）
            if (!VaultUtils.isClient
                && bodyState is GolemStateIndex.HeadDetach or GolemStateIndex.SolarOverdrive) {
                GolemFistStateIndex current = (GolemFistStateIndex)(int)npc.ai[GolemAiSlots.PartStateSlot];
                if (current is GolemFistStateIndex.Windup or GolemFistStateIndex.Punch) {
                    fistStateMachine.ChangeState(new GolemFistGuardState());
                }
            }

            //脱战跟随退场
            if (bodyState == GolemStateIndex.Despawn && npc.timeLeft > 10) {
                npc.timeLeft = 10;
            }

            //蓄力表现衰减
            fistContext.WindupGlow = MathHelper.Clamp(fistContext.WindupGlow - 0.05f, 0f, 1f);
            //残影门控速度：客户端清零速度前缓存
            fistContext.VisualSpeed = npc.velocity.Length();

            //客户端只呈现同步位置；坠地崩解走本地物理模拟（服务器广播仍会纠偏）
            bool clientShadow = VaultUtils.isClient
                && (GolemFistStateIndex)(int)npc.ai[GolemAiSlots.PartStateSlot] != GolemFistStateIndex.DeathFall;
            Vector2 savedPos = npc.position;

            fistStateMachine.Update();

            //推进器视觉：傀儡清零前缓存速度向量 + 反弹侧喷检测 + 尾迹余烬
            UpdateThrusterVisual();

            if (clientShadow) {
                npc.position = savedPos;
                npc.velocity = Vector2.Zero;
            }

            return false;
        }

        /// <summary>喷焰表现数据维护（各端本地，不参与决策）</summary>
        private void UpdateThrusterVisual() {
            Vector2 newVel = npc.velocity;
            GolemFistStateIndex st = (GolemFistStateIndex)(int)npc.ai[GolemAiSlots.PartStateSlot];

            //反弹侧向修正喷：飞行中速度方向骤变时点燃（撞墙反弹的本地读法，免网络事件）
            if (st == GolemFistStateIndex.Punch
                && newVel.LengthSquared() > 64f && fistContext.ThrustVel.LengthSquared() > 64f
                && Vector2.Dot(Vector2.Normalize(newVel), Vector2.Normalize(fistContext.ThrustVel)) < 0.25f) {
                fistContext.BounceBurst = 10;
                if (!VaultUtils.isServer) {
                    GolemScreenEffects.Shake(2f);
                    for (int i = 0; i < 7; i++) {
                        PRTLoader.NewParticle<PRT_Spark>(npc.Center, VaultUtils.RandVr(2f, 6f),
                            new Color(255, 190, 80), Main.rand.NextFloat(0.7f, 1.1f)).Configure(true, 16);
                    }
                }
            }
            //傀儡端包间隙读到零速时保留上帧向量（Punch/Return 语义上无合法零速），防喷向单帧塌零
            bool puppetGap = VaultUtils.isClient && newVel.LengthSquared() < 0.01f
                && st is GolemFistStateIndex.Punch or GolemFistStateIndex.Return;
            if (!puppetGap) {
                fistContext.ThrustVel = newVel;
            }
            if (fistContext.BounceBurst > 0) {
                fistContext.BounceBurst--;
            }
            if (fistContext.MuzzleFlash > 0) {
                fistContext.MuzzleFlash--;
            }

            //尾迹余烬 + 淡热浪：高速飞行/回收期从喷口洒出
            if (!VaultUtils.isServer && newVel.LengthSquared() > 100f
                && st is GolemFistStateIndex.Punch or GolemFistStateIndex.Return) {
                Vector2 dir = Vector2.Normalize(newVel);
                Vector2 pos = npc.Center - dir * 20f + Main.rand.NextVector2Circular(6f, 6f);
                if (Main.rand.NextBool(2)) {
                    PRTLoader.NewParticle<PRT_Spark>(pos,
                        -dir * Main.rand.NextFloat(1.5f, 4f) + VaultUtils.RandVr(0f, 1.2f),
                        new Color(255, 170, 60), Main.rand.NextFloat(0.5f, 0.9f)).Configure(true, 14);
                }
                if (Main.rand.NextBool(5)) {
                    PRTLoader.NewParticle<PRT_Smoke>(pos, -dir * 1.2f,
                        new Color(110, 84, 62), Main.rand.NextFloat(0.4f, 0.7f)).Configure(24, 0.5f);
                }
            }
        }

        #region 状态机维护
        private void EnsureStateMachine() {
            fistContext ??= new GolemFistStateContext {
                Npc = npc,
                Owner = this,
                Side = Side,
                //兜底引用，防状态恢复时 OnEnter 空解引用
                Body = body,
                Target = player
            };

            if (fistStateMachine != null) {
                return;
            }

            fistStateMachine = new NpcStateMachine<GolemFistStateContext>(fistContext, aiSlot: GolemAiSlots.PartStateSlot);

            //中途加入从同步槽恢复
            IVaultState<GolemFistStateContext> syncedState = null;
            int syncedStateId = (int)npc.ai[GolemAiSlots.PartStateSlot];
            if (VaultUtils.isClient && syncedStateId > 0) {
                syncedState = VaultStateRegistry<GolemFistStateContext>.Create(syncedStateId);
            }
            fistStateMachine.SetInitialState(syncedState ?? new GolemFistAnchorState());
        }

        private void UpdateContext() {
            fistContext.Npc = npc;
            fistContext.Body = body;
            fistContext.Target = player;
            fistContext.Owner = this;
            fistContext.Side = Side;
            fistContext.AsuraMode = CWRWorld.Asura;
            //读躯干滞回后的激怒旗标，与本体同拍落怒/解除
            fistContext.Enraged = GolemBodyAI.SharedEnrage(body, player);
        }

        private void KillSelfOnServer() {
            if (VaultUtils.isClient) {
                return;
            }
            npc.life = 0;
            npc.HitEffect();
            npc.active = false;
            npc.netUpdate = true;
        }
        #endregion

        #region 绘制
        public override bool? Draw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            //喷焰与残影垫在拳本体之下
            GolemRenderHelper.DrawFistThruster(spriteBatch, npc, fistContext);
            GolemRenderHelper.DrawFistTrail(spriteBatch, npc, screenPos, fistContext?.VisualSpeed ?? -1f);
            //返回 null 继续原版本体绘制
            return null;
        }

        public override bool PostDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            //蓄力辉光盖在本体上
            if (fistContext != null && fistContext.WindupGlow > 0.05f) {
                GolemRenderHelper.DrawFistWindup(spriteBatch, npc, fistContext);
            }
            return false;
        }
        #endregion
    }

    /// <summary>左拳</summary>
    internal class GolemFistLeftAI : GolemFistAI
    {
        public override int TargetID => NPCID.GolemFistLeft;
        protected override int Side => -1;
    }

    /// <summary>右拳</summary>
    internal class GolemFistRightAI : GolemFistAI
    {
        public override int TargetID => NPCID.GolemFistRight;
        protected override int Side => 1;
    }
}
