using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Projectiles;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.States
{
    /// <summary>
    /// 镜像仪式：真身混入假身环阵。识真三线索：足影光渍（静态）、体色苍白（静态）、弹色元素/苍白（动态）<br/>
    /// 打真身：仪式当场破碎（踉跄+充能大扣）；打假身：30 帧鼓胀预告后苍弹环爆（朝玩家扇区留空），充能小涨有上限；<br/>
    /// 拖满 600 帧：教徒自行收阵，充能中涨——不许无限拖
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)CultistStateIndex.MirrorRite, typeof(CultistStateContext))]
    internal class CultistMirrorRiteState : CultistStateBase
    {
        public override string StateName => "CultistMirrorRite";
        public override CultistStateIndex StateIndex => CultistStateIndex.MirrorRite;

        private const int PlaceFrame = 24;
        private const int Timeout = 600;
        private const float RingRadius = 430f;

        private int lifeAtEnter;
        private int lastAliveClones;

        private int CloneCount(CultistStateContext context) => context.Phase >= 2 ? 4 : 3;

        public override void OnEnter(CultistStateContext context) {
            base.OnEnter(context);
            lifeAtEnter = context.Npc.life;
            lastAliveClones = -1;
            context.MirrorPenaltyGained = 0f;
            context.MirrorActive = true;
        }

        public override ICultistState OnUpdate(CultistStateContext context) {
            NPC npc = context.Npc;
            Player player = context.Target;
            Timer++;

            CultistScreenFX.SetVeil(0.4f, player.Center, CultistMotion.PaleClone, 760f);

            //洗牌前奏：真身半隐
            if (Timer <= PlaceFrame) {
                SetPose(npc, 0);
                npc.velocity *= 0.85f;
                npc.alpha = (int)MathHelper.Clamp(Timer * 8f, 0f, 190f);
                if (Timer == 6 && !VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Item8 with { Volume = 0.7f, Pitch = -0.45f }, npc.Center);
                }
                if (Timer % 5 == 0) {
                    CultistMotion.RuneBurst(npc.Center, CultistMotion.PaleClone, 2, 4f);
                }
            }

            //布阵（权威端）：环绕玩家等距布位，真身随机占位
            if (Timer == PlaceFrame && !VaultUtils.isClient) {
                int clones = CloneCount(context);
                int total = clones + 1;
                float angle0 = Main.rand.NextFloat(MathHelper.TwoPi);
                int trueSlot = Main.rand.Next(total);
                int cloneSlot = 0;
                for (int i = 0; i < total; i++) {
                    Vector2 pos = player.Center + (angle0 + MathHelper.TwoPi * i / total).ToRotationVector2() * RingRadius;
                    if (i == trueSlot) {
                        npc.Center = pos;
                        npc.velocity = Vector2.Zero;
                        npc.netUpdate = true;
                    }
                    else {
                        //ai0=槽位 ai2=齐射相位差 ai3=本体索引
                        int index = NPC.NewNPC(npc.GetSource_FromAI(), (int)pos.X, (int)pos.Y,
                            NPCID.CultistBossClone, 0, cloneSlot, 0f, cloneSlot * 9f, npc.whoAmI);
                        if (index < Main.maxNPCs) {
                            Main.npc[index].life = Main.npc[index].lifeMax = 900;
                            if (Main.netMode == NetmodeID.Server) {
                                NetMessage.SendData(MessageID.SyncNPC, -1, -1, null, index);
                            }
                        }
                        cloneSlot++;
                    }
                }
                lastAliveClones = clones;
            }

            //阵中：真身显形并齐射真言弹（弹色=元素色，动态识真线索）
            if (Timer > PlaceFrame) {
                SetPose(npc, 11);
                FaceTarget(npc, player.Center);
                npc.alpha = (int)MathHelper.Clamp(npc.alpha - 14, 0f, 255f);
                npc.velocity *= 0.9f;

                if ((Timer - PlaceFrame) % 55 == 30) {
                    Vector2 dir = (player.Center - npc.Center).SafeNormalize(Vector2.UnitY);
                    if (!VaultUtils.isClient) {
                        Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center + dir * 26f, dir * 7.8f,
                            ModContent.ProjectileType<CultistTrueBolt>(), 34, 0f, Main.myPlayer, context.Element);
                    }
                    context.PushAura(0.8f, CultistMotion.ElementCore(context.Element));
                    CultistMotion.CastFlash(npc.Center + dir * 26f, CultistMotion.ElementCore(context.Element), 0.7f);
                }
            }

            if (VaultUtils.isClient) {
                return null;
            }

            //监听假身减员：布阵后每少一个 = 玩家打错，充能小涨（有上限阀）
            if (Timer > PlaceFrame) {
                int alive = CountClones(npc.whoAmI);
                if (lastAliveClones >= 0 && alive < lastAliveClones) {
                    int popped = lastAliveClones - alive;
                    for (int i = 0; i < popped && context.MirrorPenaltyGained < 50f; i++) {
                        context.AddRitual(25f);
                        context.MirrorPenaltyGained += 25f;
                    }
                }
                lastAliveClones = alive;
            }

            //识破：真身吃到任意伤害 → 仪式当场破碎
            if (Timer > PlaceFrame + 4 && npc.life < lifeAtEnter) {
                DismissClones(npc.whoAmI);
                context.AddRitual(-40f);
                context.StaggerDuration = 60;
                CultistScreenFX.PushFlash(0.35f);
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.NPCDeath6 with { Volume = 0.9f, Pitch = 0.3f }, npc.Center);
                }
                return new CultistStaggerState();
            }

            //拖满：自行收阵，仪式得逞
            if (Timer >= Timeout) {
                DismissClones(npc.whoAmI);
                context.AddRitual(40f);
                return new CultistWeaveState();
            }
            return null;
        }

        public override void OnExit(CultistStateContext context) {
            context.MirrorActive = false;
            context.Npc.alpha = 0;
        }

        /// <summary>数活着的己方假身</summary>
        internal static int CountClones(int parentWho) {
            int count = 0;
            foreach (NPC other in Main.ActiveNPCs) {
                if (other.type == NPCID.CultistBossClone && (int)other.ai[3] == parentWho) {
                    count++;
                }
            }
            return count;
        }

        /// <summary>软收阵：假身接令化符散去，不放环爆（权威端）</summary>
        internal static void DismissClones(int parentWho) {
            foreach (NPC other in Main.ActiveNPCs) {
                if (other.type == NPCID.CultistBossClone && (int)other.ai[3] == parentWho) {
                    other.ai[1] = 2f;
                    other.netUpdate = true;
                }
            }
        }
    }
}
