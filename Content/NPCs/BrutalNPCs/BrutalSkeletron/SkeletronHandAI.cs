using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletron.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletron.Rendering;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletron.States;
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
    /// <summary>骷髅手 NPCOverride，行为见 States.Hands</summary>
    internal class SkeletronHandAI : BrutalNPCOverride
    {
        public override int TargetID => NPCID.SkeletronHand;

        internal SkeletronHandContext handContext;
        internal VaultStateMachine<SkeletronHandContext> handStateMachine;
        internal NPC head;

        /// <summary>冲击广播计数本地锚（防重播/防入场补播）</summary>
        private float impactLatch;
        private bool impactLatchInit;

        public override bool? CanBrutalOverride() {
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

            //脱战跟随退场：掌竖起挥手告别，渐隐上飘
            SkeletronStateIndex headState = SkeletronHeadAI.GetStateIndex(head);
            if (headState == SkeletronStateIndex.Despawn) {
                npc.velocity *= 0.94f;
                npc.velocity -= Vector2.UnitY * 0.12f;
                float wave = (Main.GameUpdateCount + npc.whoAmI * 53) * 0.14f;
                npc.rotation = npc.rotation.AngleLerp((float)Math.Sin(wave) * 0.55f, 0.16f);
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

            //轴向挤压阻尼弹簧（渲染层弹性，各端本地推演）
            handContext.SquashVel -= handContext.SquashOff * 0.22f;
            handContext.SquashVel *= 0.82f;
            handContext.SquashOff = MathHelper.Clamp(handContext.SquashOff + handContext.SquashVel, -0.45f, 0.45f);

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
            handContext.Asura = CWRWorld.Asura;
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

        /// <summary>落掌冲击反馈：钝响+骨屑+幽火+震屏+掌骨挤压回弹（各端本地，服务端静默）</summary>
        internal static void PlayImpactFeedback(NPC hand) {
            //挤压回弹在服务端也推演（画面无关但保持上下文一致性开销可忽略）
            if (hand.TryGetOverride(out SkeletronHandAI handOverride)) {
                handOverride?.handContext?.TriggerSquash(0.5f);
            }
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

            //合掌拍捉对峙走廊预警（左手绘制，防双份）
            DrawSnatchCorridor(spriteBatch, alphaFade);

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

            //本体（轴向挤压回弹：Y=指轴压扁，X=横向补偿鼓起）
            float squash = handContext?.SquashOff ?? 0f;
            Vector2 bodyScale = new Vector2(npc.scale * (1f + squash * 0.55f), npc.scale * (1f - squash * 0.8f));
            spriteBatch.Draw(tex, drawPos, rect, drawColor * alphaFade, npc.rotation, orig, bodyScale, fx, 0f);

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

        #region 拍捉走廊预警

        private static readonly Vector2[] corridorPts = new Vector2[10];

        /// <summary>合掌拍捉对峙期：左右掌间的走廊灵息绸带（拍捉判定区可读化）</summary>
        private void DrawSnatchCorridor(SpriteBatch spriteBatch, float alphaFade) {
            //左手负责整条走廊，右手跳过
            if (npc.ai[SkeletronAiSlots.HandSide] >= 0f) {
                return;
            }
            if ((int)npc.ai[SkeletronAiSlots.HandStateSlot] != (int)SkeletronHandStateIndex.Snatch
                || SkeletronHeadAI.GetStateIndex(head) != SkeletronStateIndex.PalmSnatch) {
                return;
            }
            int sub = (int)head.ai[SkeletronAiSlots.HeadParamB];
            if (sub > SkeletronPalmSnatchState.SubSnap) {
                return;
            }
            SkeletronFacts.CountHands(head, out NPC left, out NPC right);
            if (left == null || right == null || left.whoAmI != npc.whoAmI) {
                return;
            }

            float tension = handContext?.ChainTension ?? 0f;
            float strength = MathHelper.Clamp(0.25f + tension * 0.75f, 0f, 1f) * alphaFade;
            if (strength <= 0.04f) {
                return;
            }

            for (int i = 0; i < corridorPts.Length; i++) {
                corridorPts[i] = Vector2.Lerp(left.Center, right.Center, i / (corridorPts.Length - 1f));
            }
            float pulse = 0.8f + 0.2f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 21f);
            float halfW = MathHelper.Lerp(7f, 20f, tension) * pulse;
            if (SkeletronRenderHelper.DrawSpecterRibbon(corridorPts, corridorPts.Length, halfW, halfW,
                0.55f * strength, 0.7f, 0.41f, 0.12f, 0.12f, 2.2f)) {
                return;
            }
            //回退：灰度光线衬光（着色器缺失时预警不许消失）
            Texture2D beam = CWRAsset.LightShot?.Value;
            if (beam == null) {
                return;
            }
            Vector2 span = right.Center - left.Center;
            Vector2 scale = new Vector2(span.Length() / beam.Width, MathHelper.Lerp(0.3f, 0.9f, tension) * pulse);
            spriteBatch.Draw(beam, left.Center - Main.screenPosition, null,
                SkeletronRenderHelper.AsAdditive(SkeletronRenderHelper.GhostCyan) * (0.4f * strength),
                span.ToRotation(), new Vector2(0f, beam.Height / 2f), scale, SpriteEffects.None, 0f);
        }

        #endregion

        #endregion
    }
}
