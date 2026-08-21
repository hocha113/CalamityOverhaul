using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Core;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.States
{
    /// <summary>
    /// 入场演出：静默祷姿 → 法阵按弧序描绘 → 定形迸发显真身<br/>
    /// 全程无伤输出；前 90 帧免伤防抢戏
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)CultistStateIndex.Intro, typeof(CultistStateContext))]
    internal class CultistIntroState : CultistStateBase
    {
        public override string StateName => "CultistIntro";
        public override CultistStateIndex StateIndex => CultistStateIndex.Intro;

        private const int Duration = 130;

        public override void OnEnter(CultistStateContext context) {
            base.OnEnter(context);
            NPC npc = context.Npc;
            npc.alpha = 255;
            npc.dontTakeDamage = true;
            npc.velocity = Vector2.Zero;
            context.SigilReveal = 0f;
        }

        public override ICultistState OnUpdate(CultistStateContext context) {
            NPC npc = context.Npc;
            Timer++;

            npc.velocity *= 0.9f;
            SetPose(npc, 13);
            FaceTarget(npc, context.Target.Center);

            //法阵描绘：20~90 帧弧序展开
            context.SigilReveal = MathHelper.Clamp((Timer - 20) / 70f, 0f, 1f);
            //真身显形：30~80 帧渐显
            npc.alpha = (int)MathHelper.Clamp(255f - (Timer - 30) * 5.2f, 0f, 255f);

            CultistScreenFX.SetVeil(0.3f, npc.Center, CultistMotion.ElementCore(context.Element), 560f);

            //描绘起音
            if (Timer == 20 && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item117 with { Volume = 0.7f, Pitch = -0.35f }, npc.Center);
            }

            //定形迸发：印记落定，仪式开始
            if (Timer == 92) {
                context.SigilCommit = 1f;
                context.PushAura(1f, CultistMotion.ElementCore(context.Element));
                CultistMotion.SigilCommitFX(npc.Center, CultistMotion.ElementCore(context.Element), 1.4f);
                CultistMotion.RuneBurst(npc.Center, CultistMotion.RuneGold, 14, 6f);
                CultistMotion.Shake(npc.Center, 5f, 12);
                CultistScreenFX.PushFlash(0.35f);
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Zombie105 with { Volume = 0.9f, Pitch = 0.1f }, npc.Center);
                }
            }

            if (Timer >= 92) {
                npc.dontTakeDamage = false;
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
            context.Npc.alpha = 0;
            context.Npc.dontTakeDamage = false;
            context.SigilReveal = 1f;
        }
    }
}
