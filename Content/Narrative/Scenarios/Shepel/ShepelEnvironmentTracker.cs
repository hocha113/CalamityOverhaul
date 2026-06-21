using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Narrative.Scenarios.Shepel
{
    //监听各类环境事件，边沿检测后向本地玩家写入对应ReactiveEvent标记
    //只在客户端运行，逻辑量极轻（每帧仅做bool比较）
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

            //血月开始（上升沿）
            if (Main.bloodMoon && !_wasBloodMoon)
                ShepelReactiveEvents.Enqueue(Player, ShepelReactiveEvent.BloodMoon);
            _wasBloodMoon = Main.bloodMoon;

            //日食开始（上升沿）
            if (Main.eclipse && !_wasEclipse)
                ShepelReactiveEvents.Enqueue(Player, ShepelReactiveEvent.SolarEclipse);
            _wasEclipse = Main.eclipse;

            //降雨开始（上升沿）
            if (Main.raining && !_wasRaining)
                ShepelReactiveEvents.Enqueue(Player, ShepelReactiveEvent.RainStarted);
            _wasRaining = Main.raining;

            //玩家死亡（上升沿，死后复活时TALK才会播放对话）
            if (Player.dead && !_wasDead)
                ShepelReactiveEvents.Enqueue(Player, ShepelReactiveEvent.PlayerRespawned);
            _wasDead = Player.dead;

            //低血量警告：HP低于25%时触发一次，超过50%后才允许再次触发
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
