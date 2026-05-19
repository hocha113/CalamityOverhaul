using CalamityOverhaul.Content.ADV.Scenarios.VoidColonys.GlitchWraith;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.HackTimes.Protocols
{
    /// <summary>
    /// 记忆修改：植入虚假的记忆，令灵异目标短暂中断追杀行为或抑制其杀人规律
    /// </summary>
    internal class FalseMemory : QuickHackDef
    {
        public override void SetDefaults() {
            UploadTime = 240;
            RamCost = 75;
            Category = QuickHackCategory.Paranormal;
            SupportedTargets = HackTargetKind.Wraith;
        }

        public override bool OnApply(IHackTarget target, Player caster) {
            if (target is not GlitchWraithActor wraith) return false;
            //灵异 Actor 的权威状态变更只在施法端执行
            if (!HackTimeNetSync.IsRemoteApply) {
                wraith.ApplyFalseMemory(60 * 15);
            }
            SoundEngine.PlaySound(SoundID.Item122 with { Volume = 0.85f, Pitch = -0.3f }, wraith.Center);
            for (int i = 0; i < 15; i++) {
                Vector2 pos = wraith.Center + Main.rand.NextVector2Circular(
                    wraith.Width * 0.35f, wraith.Height * 0.35f);
                Vector2 vel = new(Main.rand.NextFloat(-0.6f, 0.6f), Main.rand.NextFloat(-2.2f, -0.4f));
                PRTLoader.NewParticle<PRT_Spark>(pos, vel, new Color(80, 255, 200), 0.65f).Configure(false, 45);
            }
            return true;
        }
    }
}
