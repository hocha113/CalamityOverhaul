using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalWallOfFlesh.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalWallOfFlesh.Projectiles;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalWallOfFlesh.Rendering;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalWallOfFlesh.States
{
    /// <summary>
    /// 水蛭浪：干呕蓄势后成群水蛭错拍冲锋，佐以血凝块飞沫。
    /// 波次冲锋的活体弹幕，蛭群会追猎，处理它们或被淹没
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)WofStateIndex.LeechWave, typeof(WofStateContext))]
    internal class WofLeechWaveState : WofStateBase
    {
        public override string StateName => "LeechWave";
        public override WofStateIndex StateIndex => WofStateIndex.LeechWave;

        private const int SpitInterval = 9;
        private const int Recover = 46;

        private int LeechCount(WofStateContext ctx) {
            int count = ctx.Phase switch {
                1 => 3,
                2 => 4,
                _ => 5,
            };
            if (ctx.IsAsuraMode) {
                count++;
            }
            return count;
        }

        public override void OnEnter(WofStateContext context) {
            base.OnEnter(context);
            if (!VaultUtils.isServer) {
                //干呕前兆
                SoundEngine.PlaySound(SoundID.Zombie10 with { Pitch = -0.55f, Volume = 1f }, context.Npc.Center);
            }
        }

        public override IWofState OnUpdate(WofStateContext context) {
            NPC npc = context.Npc;
            Timer++;
            int spitCount = LeechCount(context);
            int spitEnd = WofDirector.RetchWindup + spitCount * SpitInterval;
            int totalEnd = spitEnd + Recover;

            if (Timer <= WofDirector.RetchWindup) {
                //干呕蓄势：墙身痉挛收缩
                float p = Timer / (float)WofDirector.RetchWindup;
                context.AdvanceFactor = 0.7f - 0.35f * p;
                context.MouthCommand = 2;
                context.WallFlush = 0.4f + 0.4f * p * p;
                if (!VaultUtils.isServer && Timer % 6 == 0) {
                    WofMotionFX.CameraPunch(npc.Center, 1.2f + p * 2f, 8, "WofRetch");
                }
                return null;
            }

            if (Timer <= spitEnd) {
                context.AdvanceFactor = 0.5f;
                context.MouthCommand = 1;
                context.WallFlush = 0.7f;

                //错拍逐条喷吐
                if ((Timer - WofDirector.RetchWindup) % SpitInterval == 1) {
                    SpitLeech(context);
                }
                return null;
            }

            //目送蛭群：恢复推进
            context.AdvanceFactor = 0.9f;
            if (Timer >= totalEnd) {
                return new WofAdvanceState();
            }
            return null;
        }

        /// <summary>喷吐一条水蛭+伴随血沫(服务端生成，各端演出)</summary>
        private void SpitLeech(WofStateContext context) {
            NPC npc = context.Npc;
            int spitIndex = (Timer - WofDirector.RetchWindup) / SpitInterval;

            if (!VaultUtils.isClient) {
                //蛭口上限(与原版滴漏共享余量)
                if (context.CountLeeches() < WofDirector.LeechCap) {
                    int leech = NPC.NewNPC(npc.GetSource_FromAI(), (int)npc.Center.X,
                        (int)(npc.Center.Y + 20f), NPCID.LeechHead, 1);
                    if (leech < Main.maxNPCs) {
                        //错拍散射：速度与仰角随索引变化，波次感
                        float speed = (context.IsAsuraMode ? 11f : 9f) + spitIndex * 0.8f;
                        Main.npc[leech].velocity.X = npc.direction * speed;
                        Main.npc[leech].velocity.Y = -2.5f + spitIndex * 1.3f;
                        Main.npc[leech].netUpdate = true;
                    }
                }
                //血沫伴射
                if (context.Target.Alives()) {
                    Vector2 aim = (context.Target.Center - npc.Center).SafeNormalize(Vector2.UnitX * npc.direction);
                    Vector2 vel = aim.RotatedBy(Main.rand.NextFloat(-0.3f, 0.3f)) * Main.rand.NextFloat(7f, 10f)
                        - Vector2.UnitY * 3f;
                    Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center + aim * 40f, vel,
                        ModContent.ProjectileType<WofBloodClot>(),
                        WallOfFleshAI.ScaleDamage(npc, WofDirector.BloodClotDamage), 0f, Main.myPlayer);
                }
                npc.netUpdate = true;
            }

            if (!VaultUtils.isServer) {
                WofMotionFX.SpawnBloodBurst(npc.Center + new Vector2(npc.direction * 50f, 10f), 1f,
                    new Vector2(npc.direction, -0.3f));
                SoundEngine.PlaySound(SoundID.NPCDeath13 with { Pitch = -0.25f, Volume = 0.9f }, npc.Center);
                WofMotionFX.CameraPunch(npc.Center, 2.4f, 8, "WofSpit", new Vector2(-npc.direction, 0f));
            }
        }
    }
}
