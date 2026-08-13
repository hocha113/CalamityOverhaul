using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Projectiles;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Rendering;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.States
{
    /// <summary>
    /// 真假瞬移博弈：环阵洗牌→同步施法（真身先动5帧）→再洗牌；
    /// 看破真身（累伤阈值）=幻术溃散+破绽硬直；打分身=雷光反击（分身AI处理）；
    /// npc.ai[3]=环阵旋转种子
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)CultistStateIndex.MirrorBlink, typeof(CultistStateContext))]
    internal class CultistMirrorBlinkState : CultistStateBase
    {
        public override string StateName => "MirrorBlink";
        public override CultistStateIndex StateIndex => CultistStateIndex.MirrorBlink;

        private const int BeatLength = 66;
        private const int MaterializeEnd = 14;
        private const int TrueCastMoment = 26;
        private const int CloneCastMoment = TrueCastMoment + CultistCloneAI.MirrorLag;
        private const float RingRadius = 460f;
        /// <summary>看破阈值：真身累伤占比</summary>
        private const float RevealThreshold = 0.03f;

        private int BeatCount(CultistStateContext ctx) => ctx.IsPhase2 ? 5 : 4;

        private int currentBeat = -1;

        public override void OnEnter(CultistStateContext context) {
            base.OnEnter(context);
            currentBeat = -1;
            context.TrueBodyHurtAccum = 0;
            context.LifeSnapshot = context.Npc.life;
            if (!VaultUtils.isClient) {
                CultistBossAI.EnsureClones(context, context.DesiredCloneCount);
                context.Npc.ai[3] = Main.rand.Next(360);
                context.Npc.netUpdate = true;
            }
        }

        public override ICultistState OnUpdate(CultistStateContext context) {
            NPC npc = context.Npc;
            Player player = context.Target;
            Timer++;

            context.SkipDefaultHover = true;
            context.ElementAura = 0.7f;
            FaceTarget(context);

            int beat = (int)Timer / BeatLength;
            int t = (int)Timer % BeatLength;

            //新拍：洗牌换位（末拍收场不再洗）
            if (beat != currentBeat && beat < BeatCount(context)) {
                currentBeat = beat;
                Shuffle(context, player);
            }

            //现身淡入（各端按拍内时间同算）
            npc.alpha = t < MaterializeEnd ? (int)MathHelper.Lerp(255f, 0f, t / (float)MaterializeEnd) : 0;

            //原地悬定+微沉浮
            npc.velocity = new Vector2(0f, (float)Math.Sin(Main.GameUpdateCount * 0.05f + npc.whoAmI) * 0.6f);

            //施法拍
            if (t >= TrueCastMoment - 8 && t <= TrueCastMoment + 12) {
                context.CastPose = CultistPose.CastForward;
                context.CastGlow = 1f - Math.Abs(t - TrueCastMoment) / 12f;
            }

            if (t == TrueCastMoment) {
                Vector2 hand = HandPos(npc);
                Vector2 aim = player.Alives() ? AimWithLead(npc, player, 14f) : new Vector2(npc.direction, 0f);
                if (!VaultUtils.isServer) {
                    CultistRenderHelper.CastBurst(hand, aim, context.Element, 1.2f);
                    SoundEngine.PlaySound(SoundID.Item72 with { Volume = 0.7f, Pitch = 0.1f, MaxInstances = 4 }, hand);
                }
                if (!VaultUtils.isClient && player.Alives()) {
                    FireVolley(context, npc.GetSource_FromAI(), hand, aim, true);
                }
            }

            //分身施法拍（滞后5帧——可学习破绽：真身永远先动）
            if (t == CloneCastMoment && player.Alives()) {
                context.RefreshClones();
                foreach (var clone in context.Clones) {
                    if (!clone.Alives()) {
                        continue;
                    }
                    Vector2 hand = clone.Center + new Vector2(clone.direction * 30f, 12f);
                    Vector2 aim = (player.Center + player.velocity * 14f - clone.Center).SafeNormalize(Vector2.UnitY);
                    //分身爆点各端可见，与真身错拍强化“谁先动”的读法
                    if (!VaultUtils.isServer) {
                        CultistRenderHelper.CastBurst(hand, aim, context.Element, 0.9f);
                    }
                    if (!VaultUtils.isClient) {
                        FireVolley(context, clone.GetSource_FromAI(), hand, aim, false);
                    }
                }
            }

            //看破判定（服务端，全程累计）
            if (!VaultUtils.isClient) {
                int hurt = context.LifeSnapshot - npc.life;
                context.LifeSnapshot = npc.life;
                if (hurt > 0) {
                    context.TrueBodyHurtAccum += hurt;
                }
                if (context.TrueBodyHurtAccum >= npc.lifeMax * RevealThreshold) {
                    //真身被看破：奖励硬直，写cue供客户端播报
                    context.StaggerTimer = 100;
                    npc.ai[1] = 1f;
                    npc.netUpdate = true;
                    if (!VaultUtils.isServer) {
                        //单机端直接演出
                        PlayRevealFx(context);
                    }
                    CultistBossAI.DismissClones(context);
                    return new CultistWeaveState();
                }
            }

            if (Timer >= BeatCount(context) * BeatLength) {
                if (!VaultUtils.isClient) {
                    CultistBossAI.DismissClones(context);
                    return new CultistWeaveState();
                }
            }
            return null;
        }

        /// <summary>看破演出（本地）</summary>
        internal static void PlayRevealFx(CultistStateContext context) {
            if (VaultUtils.isServer) {
                return;
            }
            CultistScreenFX.PushFlash(0.45f, 16);
            SoundEngine.PlaySound(SoundID.Shatter with { Volume = 0.9f, Pitch = 0.2f }, context.Npc.Center);
            CultistBossAI.LocalText(CultistBossAI.LunaticCultist_MirrorRevealText, CultistPalette.Bright(context.Element));
        }

        /// <summary>环阵洗牌：服务端摆位并同步</summary>
        private void Shuffle(CultistStateContext context, Player player) {
            NPC npc = context.Npc;

            //各端都放离场符（用洗牌前位置）
            context.RefreshClones();
            CultistRenderHelper.BlinkOut(npc.Center, context.Element);
            foreach (var clone in context.Clones) {
                if (clone.Alives()) {
                    CultistRenderHelper.BlinkOut(clone.Center, context.Element);
                }
            }

            if (VaultUtils.isClient || !player.Alives()) {
                return;
            }

            int slotCount = context.Clones.Count + 1;
            Vector2 ringCenter = player.Center + player.velocity * 10f;

            //旋转种子+真身随机占位
            float baseAngle = MathHelper.ToRadians(npc.ai[3]) + currentBeat * 0.7f;
            int bossSlot = Main.rand.Next(slotCount);

            for (int i = 0; i < slotCount; i++) {
                float angle = baseAngle + MathHelper.TwoPi * i / slotCount;
                Vector2 pos = ringCenter + angle.ToRotationVector2() * RingRadius;
                if (i == bossSlot) {
                    npc.Center = pos;
                    npc.velocity = Vector2.Zero;
                    npc.netUpdate = true;
                }
                else {
                    int cloneIdx = i > bossSlot ? i - 1 : i;
                    if (cloneIdx < context.Clones.Count) {
                        NPC clone = context.Clones[cloneIdx];
                        clone.Center = pos;
                        clone.velocity = Vector2.Zero;
                        clone.ai[0] = cloneIdx;
                        clone.netUpdate = true;
                    }
                }
            }

            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item8 with { Volume = 0.8f, Pitch = 0.4f }, ringCenter);
            }
        }

        /// <summary>齐射：真身3发全速，分身2发回声（更少更缓）</summary>
        private void FireVolley(CultistStateContext context, Terraria.DataStructures.IEntitySource source,
            Vector2 hand, Vector2 aim, bool trueBody) {
            NPC npc = context.Npc;
            int count = trueBody ? 3 : 2;
            float speedMul = trueBody ? 1f : 0.85f;
            int damage = ProjDamage(npc, trueBody ? 36f : 30f, trueBody ? 26f : 21f);

            for (int i = 0; i < count; i++) {
                float spread = MathHelper.Lerp(-0.22f, 0.22f, count <= 1 ? 0.5f : i / (float)(count - 1));
                Vector2 dir = aim.RotatedBy(spread);
                switch (context.Element) {
                    case CultistElement.Fire:
                        Projectile.NewProjectile(source, hand, dir * 6f * speedMul,
                            ModContent.ProjectileType<CultistFireBolt>(), damage, 0f, Main.myPlayer, 25f, 0f);
                        break;
                    case CultistElement.Ice:
                        Projectile.NewProjectile(source, hand + dir * 30f, dir,
                            ModContent.ProjectileType<CultistIceLance>(), damage, 0f, Main.myPlayer, 20f, 17f * speedMul);
                        break;
                    default:
                        Projectile.NewProjectile(source, hand, dir * 7.2f * speedMul,
                            ModContent.ProjectileType<CultistArcSpark>(), damage, 0f, Main.myPlayer,
                            (float)CultistElement.Thunder, 0f);
                        break;
                }
            }
        }

        public override void OnExit(CultistStateContext context) {
            base.OnExit(context);
            context.Npc.alpha = 0;
        }
    }
}
