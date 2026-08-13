using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalGolem.Core;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalGolem.States
{
    /// <summary>脱战离场：无有效目标时沉回大地，机关归寂</summary>
    [InnoVault.StateMachines.VaultState((int)GolemStateIndex.Despawn, typeof(GolemStateContext))]
    internal class GolemDespawnState : GolemStateBase
    {
        public override string StateName => "Despawn";
        public override GolemStateIndex StateIndex => GolemStateIndex.Despawn;

        internal static int SinkStart => 60;
        internal static int EndTime => 150;

        public override IGolemState OnUpdate(GolemStateContext context) {
            NPC npc = context.Npc;
            npc.damage = 0;
            npc.dontTakeDamage = true;
            context.FrameMode = 1;
            //光焰熄灭
            context.VeinGlow = MathHelper.Clamp(1f - Timer / 50f, 0f, 1f) * context.VeinGlow;

            if (Timer < SinkStart) {
                npc.noTileCollide = false;
                GroundBrake(npc);
                if (!VaultUtils.isServer && Timer == 10) {
                    SoundEngine.PlaySound(SoundID.WormDig with { Pitch = -0.6f, Volume = 0.9f }, npc.Center);
                }
            }
            else {
                //沉入大地
                npc.noTileCollide = true;
                npc.velocity = new Vector2(0f, 3.2f + (Timer - SinkStart) * 0.06f);
                npc.alpha = System.Math.Min(npc.alpha + 5, 255);
                if (!VaultUtils.isServer && Timer % 5 == 0) {
                    Dust dust = Dust.NewDustDirect(new Vector2(npc.position.X, npc.Bottom.Y - 8f),
                        npc.width, 8, DustID.Smoke, 0f, -1.5f, 100, default, 1.6f);
                    dust.velocity *= 0.4f;
                }
                if (!VaultUtils.isServer && Timer % 18 == 0) {
                    GolemScreenEffects.Shake(2f);
                }
            }

            Timer++;
            if (Timer >= EndTime && !VaultUtils.isClient) {
                //静默清场：部件与本体一并移除，不掉落
                GolemLimbStatus limbs = context.Limbs;
                RemovePart(limbs.HeadIndex);
                RemovePart(limbs.FreeHeadIndex);
                RemovePart(limbs.LeftFistIndex);
                RemovePart(limbs.RightFistIndex);
                npc.active = false;
                npc.netUpdate = true;
            }
            return null;
        }

        private static void RemovePart(int index) {
            if (index < 0 || index >= Main.maxNPCs) {
                return;
            }
            NPC part = Main.npc[index];
            if (part.active) {
                part.life = 0;
                part.active = false;
                part.netUpdate = true;
            }
        }
    }
}
