using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Projectiles;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.States
{
    /// <summary>法阵齐射：弧列悬空法阵涟漪开火；npc.ai[3]=布阵种子</summary>
    [InnoVault.StateMachines.VaultState((int)CultistStateIndex.SigilVolley, typeof(CultistStateContext))]
    internal class CultistSigilVolleyState : CultistStateBase
    {
        public override string StateName => "SigilVolley";
        public override CultistStateIndex StateIndex => CultistStateIndex.SigilVolley;

        private const int PlaceMoment = 18;
        private const int Duration = 212;

        public override void OnEnter(CultistStateContext context) {
            base.OnEnter(context);
            if (!VaultUtils.isClient) {
                context.Npc.ai[3] = Main.rand.Next(1000);
                context.Npc.netUpdate = true;
            }
        }

        public override ICultistState OnUpdate(CultistStateContext context) {
            NPC npc = context.Npc;
            Player player = context.Target;
            Timer++;

            FaceTarget(context);
            context.ElementAura = 0.9f;

            //布阵期高举施法
            if (Timer >= 6 && Timer <= 60) {
                context.CastPose = CultistPose.CastUp;
                context.CastGlow = MathHelper.Clamp((Timer - 6) / 26f, 0f, 1f);
            }
            else if (Timer > 60 && Timer < Duration - 30) {
                //齐射期前指维持
                context.CastPose = CultistPose.CastForward;
                context.CastGlow = 0.5f;
            }

            //顶位压场
            if (player.Alives()) {
                SetHover(context, player.Center + new Vector2(0f, -430f));
            }

            if ((int)Timer == PlaceMoment && !VaultUtils.isClient && player.Alives()) {
                int count = context.IsPhase2 ? 5 : 3;
                int shots = context.IsDeathMode ? 4 : 3;
                int damage = ProjDamage(npc, 40f, 28f);
                float seed = npc.ai[3] * 0.31f;
                //以玩家为心的上半弧列
                for (int i = 0; i < count; i++) {
                    float arcT = count <= 1 ? 0.5f : i / (float)(count - 1);
                    float angle = MathHelper.Lerp(-2.6f, -0.54f, arcT) + (float)Math.Sin(seed + i) * 0.1f;
                    Vector2 pos = player.Center + angle.ToRotationVector2() * 500f;
                    Projectile.NewProjectile(npc.GetSource_FromAI(), pos, Vector2.Zero,
                        ModContent.ProjectileType<CultistSigilProj>(), damage, 0f, Main.myPlayer,
                        (float)context.Element, i * 12f, shots);
                }
            }

            if ((int)Timer == PlaceMoment && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item78 with { Volume = 0.8f, Pitch = -0.1f }, npc.Center);
            }

            if (Timer >= Duration) {
                return new CultistWeaveState();
            }
            return null;
        }
    }
}
