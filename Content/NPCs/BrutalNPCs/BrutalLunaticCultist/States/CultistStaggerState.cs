using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Core;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.States
{
    /// <summary>
    /// 仪式被破的踉跄：身体下坠飘摇，仪式辉光熄灭，受伤加深 ×1.25：玩家拆台的奖励窗口
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)CultistStateIndex.Stagger, typeof(CultistStateContext))]
    internal class CultistStaggerState : CultistStateBase
    {
        public override string StateName => "CultistStagger";
        public override CultistStateIndex StateIndex => CultistStateIndex.Stagger;

        public override void OnEnter(CultistStateContext context) {
            base.OnEnter(context);
            NPC npc = context.Npc;
            npc.velocity = new Vector2(npc.velocity.X * 0.3f, 2.2f);
            CultistMotion.RuneBurst(npc.Center, CultistMotion.RuneGold, 10, 6.5f);
            CultistMotion.Shake(npc.Center, 4.5f, 10);
            CultistScreenFX.PushFlash(0.3f);
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.NPCHit55 with { Volume = 1f, Pitch = -0.6f }, npc.Center);
                SoundEngine.PlaySound(SoundID.Item78 with { Volume = 0.6f, Pitch = -0.4f }, npc.Center);
            }
        }

        public override ICultistState OnUpdate(CultistStateContext context) {
            NPC npc = context.Npc;
            Timer++;

            SetPose(npc, 0);
            //飘摇下坠后缓停
            npc.velocity *= 0.965f;
            npc.velocity.Y += Timer < context.StaggerDuration * 0.4f ? 0.06f : -0.02f;

            //辉光熄灭期的残火抖动
            if (Timer % 9 == 0) {
                CultistMotion.RuneBurst(npc.Center + Main.rand.NextVector2Circular(20f, 30f),
                    CultistMotion.PaleClone, 1, 2.5f);
            }

            if (VaultUtils.isClient) {
                return null;
            }
            if (Timer >= context.StaggerDuration) {
                return new CultistWeaveState();
            }
            return null;
        }

        public override void OnExit(CultistStateContext context) {
            context.StaggerDuration = 90;
        }
    }
}
