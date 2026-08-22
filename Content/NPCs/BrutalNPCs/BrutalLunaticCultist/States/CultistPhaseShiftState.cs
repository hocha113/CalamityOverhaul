using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Core;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.States
{
    /// <summary>
    /// 转阶段演出：清场→仰首嘶吼→帷幕变调；进 P3 时唤出幻影龙（共享血池，可猎杀换充能削减）<br/>
    /// 全程免伤但不出手，纯语调重置拍
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)CultistStateIndex.PhaseShift, typeof(CultistStateContext))]
    internal class CultistPhaseShiftState : CultistStateBase
    {
        public override string StateName => "CultistPhaseShift";
        public override CultistStateIndex StateIndex => CultistStateIndex.PhaseShift;

        private const int Duration = 110;

        public override void OnEnter(CultistStateContext context) {
            base.OnEnter(context);
            NPC npc = context.Npc;
            context.IsInPhaseTransition = true;
            npc.dontTakeDamage = true;
            npc.velocity = Vector2.Zero;

            if (!VaultUtils.isClient) {
                context.Phase++;
                npc.ai[0] = context.Phase;
                context.ClearAttackBag();
                //转阶段后给玩家缓冲，别立刻咏唱
                context.ChantCooldown = System.Math.Max(context.ChantCooldown, 360);
                npc.netUpdate = true;
            }
        }

        public override ICultistState OnUpdate(CultistStateContext context) {
            NPC npc = context.Npc;
            Timer++;

            SetPose(npc, 13);
            npc.velocity *= 0.9f;

            Color core = CultistMotion.ElementCore(context.Element);
            CultistScreenFX.SetVeil(0.65f, npc.Center, core, 680f);
            context.PushAura(0.9f, core);

            //清场：旧攻势不跨阶段（权威端）
            if (Timer == 10 && !VaultUtils.isClient) {
                CultistBossAI.ClearMinionsAndProjectiles(npc);
            }

            //嘶吼顿点
            if (Timer == 34) {
                context.SigilCommit = 1f;
                CultistScreenFX.PushFlash(0.45f);
                CultistMotion.Shake(npc.Center, 6f, 14);
                CultistMotion.RuneBurst(npc.Center, CultistMotion.RuneGold, 18, 7.5f);
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Zombie105 with { Volume = 1.2f, Pitch = -0.15f }, npc.Center);
                }
            }

            //P3：唤出幻影龙（权威端）
            if (Timer == 64 && context.Phase >= 2 && !context.DragonSpawned && !VaultUtils.isClient) {
                Vector2 pos = npc.Center + new Vector2(npc.direction * -500f, -320f);
                //ai[0]=0 让龙头自建 30 节体段并共享血池（realLife）
                int head = NPC.NewNPC(npc.GetSource_FromAI(), (int)pos.X, (int)pos.Y, NPCID.CultistDragonHead);
                if (head < Main.maxNPCs) {
                    Main.npc[head].lifeMax = Main.npc[head].life = 3200;
                    CultistAncientRiteState.SyncMinion(head);
                }
                context.DragonSpawned = true;
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Zombie102 with { Volume = 1f }, pos);
                }
            }

            //嘶吼后符文持续涌
            if (Timer > 34 && Timer % 6 == 0) {
                CultistMotion.RuneBurst(npc.Center + Main.rand.NextVector2Circular(24f, 32f), core, 1, 3f);
            }

            if (VaultUtils.isClient) {
                return null;
            }
            if (Timer >= Duration) {
                return new CultistWeaveState();
            }
            return null;
        }

        public override void OnExit(CultistStateContext context) {
            context.IsInPhaseTransition = false;
            context.Npc.dontTakeDamage = false;
        }
    }
}
