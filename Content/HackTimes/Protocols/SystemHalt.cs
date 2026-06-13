using CalamityOverhaul.Content.ADV.Scenarios.VoidColonys.GlitchWraith;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.HackTimes.Protocols
{
    /// <summary>死机，灵异目标短暂沉寂</summary>
    internal class SystemHalt : QuickHackDef
    {
        public override void SetDefaults() {
            UploadTime = 180;
            RamCost = 50;
            Category = QuickHackCategory.Paranormal;
            SupportedTargets = HackTargetKind.Wraith;
        }

        public override bool OnApply(IHackTarget target, Player caster) {
            if (target is not GlitchWraithActor wraith) return false;
            //灵异权威状态仅施法端，远端靠 Actor 同步
            if (!HackTimeNetSync.IsRemoteApply) {
                wraith.ApplySystemHalt(60 * 10);
            }
            SoundEngine.PlaySound(SoundID.NPCDeath59 with { Volume = 0.9f, Pitch = -0.6f }, wraith.Center);
            for (int i = 0; i < 20; i++) {
                Vector2 vel = Main.rand.NextVector2CircularEdge(3f, 3f);
                PRTLoader.NewParticle<PRT_Spark>(wraith.Center, vel, new Color(200, 60, 220), 0.8f).Configure(false, 40);
            }
            return true;
        }
    }
}
