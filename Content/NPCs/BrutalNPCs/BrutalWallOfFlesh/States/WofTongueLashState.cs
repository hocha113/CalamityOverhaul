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
    /// 舌鞭钩曳：巨口洞开，舌头沿锁定线暴射而出，命中即把玩家往嘴里拽一程。
    /// 预告线充分可读；阶段3连甩两鞭
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)WofStateIndex.TongueLash, typeof(WofStateContext))]
    internal class WofTongueLashState : WofStateBase
    {
        public override string StateName => "TongueLash";
        public override WofStateIndex StateIndex => WofStateIndex.TongueLash;

        /// <summary>舌体存活帧(伸出+回收)</summary>
        private const int LashLife = 66;
        private const int Recover = 28;

        private int segTimer;
        private int lashDone;
        /// <summary>0预告 1甩鞭 2收尾</summary>
        private int stage;

        private int TotalLashes(WofStateContext ctx) => ctx.Phase >= 3 ? 2 : 1;

        public override void OnEnter(WofStateContext context) {
            base.OnEnter(context);
            segTimer = 0;
            lashDone = 0;
            stage = 0;
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Zombie10 with { Pitch = -0.3f, Volume = 0.9f }, context.Npc.Center);
            }
        }

        public override IWofState OnUpdate(WofStateContext context) {
            NPC npc = context.Npc;
            Timer++;
            segTimer++;

            switch (stage) {
                case 0: {
                    //预告：口洞开、舌根蓄势红光
                    float p = MathHelper.Clamp(segTimer / (float)WofDirector.TongueTelegraph, 0f, 1f);
                    context.AdvanceFactor = 0.6f;
                    context.MouthCommand = 1;
                    context.SetChargeState(5, p);
                    context.WallFlush = 0.4f + 0.3f * p;

                    if (segTimer >= WofDirector.TongueTelegraph) {
                        FireLash(context);
                        stage = 1;
                        segTimer = 0;
                    }
                    break;
                }
                case 1: {
                    context.AdvanceFactor = 0.6f;
                    context.MouthCommand = 1;
                    context.WallFlush = 0.55f;
                    if (segTimer >= LashLife) {
                        lashDone++;
                        segTimer = 0;
                        stage = lashDone < TotalLashes(context) ? 0 : 2;
                    }
                    break;
                }
                default: {
                    context.AdvanceFactor = 0.85f;
                    context.MouthCommand = 2;
                    if (segTimer >= Recover) {
                        return new WofAdvanceState();
                    }
                    break;
                }
            }
            return null;
        }

        /// <summary>甩鞭(服务端生成舌体，锁定预测位)</summary>
        private void FireLash(WofStateContext context) {
            NPC npc = context.Npc;
            if (!VaultUtils.isClient && context.Target.Alives()) {
                //提前量锁线：预告结束瞬间定格，之后不再追踪(可读性)
                Vector2 predicted = context.Target.Center + context.Target.velocity * 12f;
                Vector2 dir = (predicted - npc.Center).SafeNormalize(Vector2.UnitX * npc.direction);
                Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, dir,
                    ModContent.ProjectileType<WofTongueLashProj>(),
                    WallOfFleshAI.ScaleDamage(npc, WofDirector.TongueDamage), 0f, Main.myPlayer,
                    npc.whoAmI, LashLife);
                npc.netUpdate = true;
            }
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item17 with { Pitch = -0.6f, Volume = 1.1f }, npc.Center);
                SoundEngine.PlaySound(SoundID.NPCHit18 with { Pitch = 0.2f, Volume = 0.8f }, npc.Center);
                WofMotionFX.SpawnBloodBurst(npc.Center + new Vector2(npc.direction * 40f, 0f), 0.9f,
                    new Vector2(npc.direction, 0f));
                WofMotionFX.CameraPunch(npc.Center, 3.5f, 10, "WofTongueFire", new Vector2(npc.direction, 0f));
            }
        }
    }
}
