using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Projectiles;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.States
{
    /// <summary>
    /// 帷幕挪移：符文散身 → 出口印记预告落点（预告即实体）→ 符文聚形重现<br/>
    /// 全程无伤输出；藏移动、亮停顿
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)CultistStateIndex.VeilStep, typeof(CultistStateContext))]
    internal class CultistVeilStepState : CultistStateBase
    {
        public override string StateName => "CultistVeilStep";
        public override CultistStateIndex StateIndex => CultistStateIndex.VeilStep;

        private const int FadeOut = 20;
        private const int Blink = 32;
        private const int Duration = 58;

        public override ICultistState OnUpdate(CultistStateContext context) {
            NPC npc = context.Npc;
            Player player = context.Target;
            Timer++;

            SetPose(npc, 0);
            npc.velocity *= 0.86f;

            if (Timer <= FadeOut) {
                //散身：渐隐+符文剥落
                npc.alpha = (int)(255f * Timer / FadeOut);
                if (Timer % 4 == 0) {
                    CultistMotion.RuneBurst(npc.Center, CultistMotion.PhaseCore(context.Phase), 2, 4f);
                }
                if (Timer == 4 && !VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Item8 with { Volume = 0.6f, Pitch = -0.2f }, npc.Center);
                }
            }

            //挪移与出口印记（权威端裁决位置）
            if (Timer == FadeOut && !VaultUtils.isClient) {
                float side = Main.rand.NextBool() ? 1f : -1f;
                //出口在玩家侧上方，保底 300px 距离（公平阀：不出现在脸上）
                Vector2 exit = player.Center + new Vector2(side * Main.rand.NextFloat(320f, 420f), -Main.rand.NextFloat(120f, 220f));
                npc.Center = exit;
                npc.velocity = Vector2.Zero;
                npc.netUpdate = true;
                //出口印记：12 帧预告后真身聚形
                Projectile.NewProjectile(npc.GetSource_FromAI(), exit, Vector2.Zero,
                    ModContent.ProjectileType<CultistSigilProj>(), 0, 0f, Main.myPlayer,
                    context.Element, 1f, 12f);
            }

            //蛰伏帧：完全不可见
            if (Timer > FadeOut && Timer <= Blink) {
                npc.alpha = 255;
            }

            if (Timer > Blink) {
                //聚形：渐显
                npc.alpha = (int)MathHelper.Clamp(255f - (Timer - Blink) * 16f, 0f, 255f);
                if (Timer == Blink + 2) {
                    CultistMotion.RuneBurst(npc.Center, CultistMotion.PhaseCore(context.Phase), 8, 5f);
                    CultistMotion.CastFlash(npc.Center, CultistMotion.PhaseCore(context.Phase), 0.8f);
                }
                FaceTarget(npc, player.Center);
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
        }
    }
}
