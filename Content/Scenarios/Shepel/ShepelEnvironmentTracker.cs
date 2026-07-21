using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Shepel
{
    //环境边沿→ReactiveEvent，仅本地玩家
    internal class ShepelEnvironmentTracker : ModPlayer
    {
        private bool _wasBloodMoon;
        private bool _wasEclipse;
        private bool _wasRaining;
        private bool _wasDead;
        private bool _lowHealthQueued;

        public override void PreUpdate() {
            if (Main.dedServ) return;
            if (Player != Main.LocalPlayer) return;

            //血月上升沿
            if (Main.bloodMoon && !_wasBloodMoon)
                ShepelReactiveEvents.Enqueue(Player, ShepelReactiveEvent.BloodMoon);
            _wasBloodMoon = Main.bloodMoon;

            //日食上升沿
            if (Main.eclipse && !_wasEclipse)
                ShepelReactiveEvents.Enqueue(Player, ShepelReactiveEvent.SolarEclipse);
            _wasEclipse = Main.eclipse;

            //降雨上升沿
            if (Main.raining && !_wasRaining)
                ShepelReactiveEvents.Enqueue(Player, ShepelReactiveEvent.RainStarted);
            _wasRaining = Main.raining;

            //死亡上升沿(复活后TALK播)
            if (Player.dead && !_wasDead)
                ShepelReactiveEvents.Enqueue(Player, ShepelReactiveEvent.PlayerRespawned);
            _wasDead = Player.dead;

            //HP<25%触发一次，>50%重置
            if (!Player.dead && Player.statLifeMax2 > 0) {
                float ratio = (float)Player.statLife / Player.statLifeMax2;
                if (ratio < 0.25f && !_lowHealthQueued) {
                    ShepelReactiveEvents.Enqueue(Player, ShepelReactiveEvent.LowHealth);
                    _lowHealthQueued = true;
                }
                else if (ratio > 0.5f) {
                    _lowHealthQueued = false;
                }
            }
        }
    }
}
