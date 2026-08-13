using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletron.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletron.Rendering;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletron.States.Hands;
using InnoVault.PRT;
using InnoVault.StateMachines;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletron
{
    /// <summary>骷髅手 NPCOverride：锁链骨掌，行为见 States.Hands</summary>
    internal class SkeletronHandAI : CWRNPCOverride
    {
        public override int TargetID => NPCID.SkeletronHand;

        internal SkeletronHandContext handContext;
        internal VaultStateMachine<SkeletronHandContext> handStateMachine;
        internal NPC head;

        /// <summary>冲击广播计数本地锚（防重播/防入场补播）</summary>
        private float impactLatch;
        private bool impactLatchInit;

        public override bool? CanCWROverride() {
            return null;
        }

        public override void SetProperty() {
            handContext = null;
            handStateMachine = null;
            npc.aiStyle = -1;
            npc.knockBackResist = 0f;
        }

        public override bool AI() {
            int headIndex = (int)npc.ai[SkeletronAiSlots.HandHeadIndex];
            if (headIndex < 0 || headIndex >= Main.maxNPCs) {
                KillSelfOnServer();
                return false;
            }
            head = Main.npc[headIndex];

            //头没了或不是我们接管的骷髅王头，手殉葬
            if (!head.active || head.type != NPCID.SkeletronHead) {
                KillSelfOnServer();
                return false;
            }

            npc.aiStyle = -1;
            npc.damage = 0;
            npc.spriteDirection = -(int)npc.ai[SkeletronAiSlots.HandSide];
            npc.target = head.target;

            EnsureStateMachine();
            UpdateContext();

            //冲击反馈广播：ai[3] 计数变化即在各端本地播放
            float impactCount = npc.ai[SkeletronAiSlots.HandFree];
            if (!impactLatchInit) {
                impactLatchInit = true;
                impactLatch = impactCount;
            }
            else if (impactLatch != impactCount) {
                impactLatch = impactCount;
                PlayImpactFeedback(npc);
            }

            //脱战跟随退场
            SkeletronStateIndex headState = SkeletronHeadAI.GetStateIndex(head);
            if (headState == SkeletronStateIndex.Despawn) {
                npc.velocity *= 0.94f;
                npc.velocity -= Vector2.UnitY * 0.12f;
                npc.alpha = Math.Min(npc.alpha + 4, 255);
                if (npc.timeLeft > 10) {
                    npc.timeLeft = 10;
                }
                return false;
            }

            //死亡演出期间静默垂落
            if (headState == SkeletronStateIndex.Death) {
                npc.dontTakeDamage = true;
                npc.velocity *= 0.9f;
                npc.velocity += Vector2.UnitY * 0.1f;
                handContext.ChainTension = 0f;
                return false;
            }

            //透明度收口：渐隐只在撤离分支推进，头中断撤离回场后手必须跟着显形
            if (npc.alpha > 0) {
                npc.alpha = Math.Max(npc.alpha - 12, 0);
            }

            //服务端每帧广播位置，客户端傀儡呈现
            if (!VaultUtils.isClient) {
                npc.netUpdate = true;
            }

            bool clientShadow = VaultUtils.isClient;
            Vector2 savedPos = npc.position;

            handStateMachine.Update();

            if (clientShadow) {
                npc.position = savedPos;
                npc.velocity = Vector2.Zero;
            }

            //张紧度自然回落，状态内主动抬升
            handContext.ChainTension = MathHelper.Clamp(handContext.ChainTension - 0.02f, 0f, 1f);
            handContext.PalmFlame = MathHelper.Clamp(handContext.PalmFlame - 0.03f, 0f, 1f);

            return false;
        }

        #region 状态机维护

        private void EnsureStateMachine() {
            handContext ??= new SkeletronHandContext {
                Npc = npc,
                Owner = this
            };

            if (handStateMachine != null) {
                return;
            }

            handStateMachine = new NpcStateMachine<SkeletronHandContext>(handContext, aiSlot: SkeletronAiSlots.HandStateSlot);

            //中途加入从同步槽恢复
            IVaultState<SkeletronHandContext> syncedState = null;
            int syncedStateId = (int)npc.ai[SkeletronAiSlots.HandStateSlot];
            if (VaultUtils.isClient && syncedStateId > 0) {
                syncedState = VaultStateRegistry<SkeletronHandContext>.Create(syncedStateId);
            }
            handStateMachine.SetInitialState(syncedState ?? new HandGuardState());
        }

        private void UpdateContext() {
            handContext.Npc = npc;
            handContext.Head = head;
            handContext.Target = Main.player[npc.target];
            handContext.Owner = this;
            handContext.BossRush = CWRRef.GetBossRushActive();
            handContext.MasterMode = Main.masterMode || handContext.BossRush;
            handContext.Death = CWRRef.GetDeathMode() || handContext.BossRush;
            handContext.Side = npc.ai[SkeletronAiSlots.HandSide] < 0f ? -1 : 1;
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

        /// <summary>落掌冲击反馈：钝响+骨屑+幽火+震屏（各端本地，服务端静默）</summary>
        internal static void PlayImpactFeedback(NPC hand) {
            if (VaultUtils.isServer) {
                return;
            }
            SoundEngine.PlaySound(SoundID.DD2_MonkStaffGroundImpact with { Volume = 1f, Pitch = -0.2f }, hand.Center);
            SoundEngine.PlaySound(SoundID.NPCHit2 with { Volume = 0.75f, Pitch = -0.6f }, hand.Center);

            //骨屑迸射 ∝ 冲击
            for (int i = 0; i < 12; i++) {
                Vector2 vel = new Vector2(Main.rand.NextFloat(-6f, 6f), Main.rand.NextFloat(-8.5f, -2f));
                PRTLoader.NewParticle<PRT_SkeleBoneChip>(hand.Center + Main.rand.NextVector2Circular(22f, 12f),
                    vel, Color.White, Main.rand.NextFloat(0.7f, 1.25f))?.Configure(Main.rand.Next(40, 70));
            }
            //幽火腾起
            for (int i = 0; i < 6; i++) {
                PRTLoader.NewParticle<PRT_SkeleGhostFlame>(hand.Center + Main.rand.NextVector2Circular(26f, 10f),
                    new Vector2(Main.rand.NextFloat(-1.6f, 1.6f), Main.rand.NextFloat(-3.4f, -1.2f)),
                    SkeletronRenderHelper.GhostCyan, Main.rand.NextFloat(1.3f, 2.2f))?.Configure(Main.rand.Next(24, 40));
            }
            //尘雾
            for (int i = 0; i < 5; i++) {
                Dust dust = Dust.NewDustDirect(hand.position, hand.width, hand.height, DustID.Bone,
                    Main.rand.NextFloat(-3f, 3f), Main.rand.NextFloat(-2f, 0f), 130, default, 1.4f);
                dust.noGravity = false;
            }

            SkeletronScreenEffects.PushShockRing(hand.Center, 0.55f, 340f, 18);
            SkeletronScreenEffects.PushShake(hand.Center, 6.5f);
        }

        #endregion

        #region 绘制

        public override bool FindFrame(int frameHeight) {
            return false;
        }

        public override bool? Draw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            return false;
        }

        public override bool PostDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            if (head == null || !head.active) {
                return true;
            }
            float alphaFade = 1f - npc.alpha / 255f;
            float tension = handContext?.ChainTension ?? 0f;

            //锁链骨节：自头颅侧锚垂到掌根
            Vector2 anchor = head.Center + new Vector2(npc.ai[SkeletronAiSlots.HandSide] * 36f, 14f).RotatedBy(head.rotation);
            SkeletronRenderHelper.DrawBoneChain(spriteBatch, anchor, npc.Center, tension, alphaFade, npc.whoAmI * 0.157f);

            Main.instance.LoadNPC(NPCID.SkeletronHand);
            Texture2D tex = TextureAssets.Npc[NPCID.SkeletronHand].Value;
            Rectangle rect = new Rectangle(0, 0, tex.Width, tex.Height);
            Vector2 orig = rect.Size() / 2f;
            Vector2 drawPos = npc.Center - screenPos + new Vector2(0f, npc.gfxOffY);
            SpriteEffects fx = npc.spriteDirection < 0 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;

            //高速砸击拖影
            float speed = npc.velocity.Length();
            float heat = MathHelper.Clamp((speed - 10f) / 22f, 0f, 1f);

            //砸击轨迹绸带（顶点层，压在掌骨之下）
            SkeletronRenderHelper.DrawMotionRibbon(npc, heat, 26f * npc.scale, 0.55f * alphaFade);

            if (heat > 0.05f) {
                for (int i = 1; i < npc.oldPos.Length; i += 2) {
                    if (npc.oldPos[i] == Vector2.Zero) {
                        continue;
                    }
                    Vector2 ghostPos = npc.oldPos[i] + npc.Size / 2f - screenPos;
                    float fade = 1f - i / (float)npc.oldPos.Length;
                    spriteBatch.Draw(tex, ghostPos, rect,
                        SkeletronRenderHelper.AsAdditive(SkeletronRenderHelper.GhostDeep) * (0.38f * fade * heat * alphaFade),
                        npc.rotation, orig, npc.scale, fx, 0f);
                }
            }

            //本体
            spriteBatch.Draw(tex, drawPos, rect, drawColor * alphaFade, npc.rotation, orig, npc.scale, fx, 0f);

            //掌心幽火（蓄力/预警读数，冷焰顶点批：焰轴沿掌口法线）
            float palm = handContext?.PalmFlame ?? 0f;
            if (palm > 0.03f) {
                float palmAngle = npc.rotation - MathHelper.PiOver2;
                Vector2 palmRoot = npc.Center + palmAngle.ToRotationVector2() * 6f;
                float pulse = 0.8f + 0.2f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 17f + npc.whoAmI);
                SkeletronFlameRender.Push(palmRoot, palmAngle,
                    new Vector2(30f, 48f * pulse) * palm * npc.scale,
                    0.45f + 0.5f * palm, npc.whoAmI * 0.23f, 0.15f,
                    0.9f * palm * alphaFade);
            }

            return false;
        }

        #endregion
    }
}
