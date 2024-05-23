using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Events
{
    internal class GameEventContentSystem : ModSystem
    {
        public override void OnModLoad() {
            new TungstenRiot().Load();
        }

        public override void PostUpdateWorld() {
            TungstenRiot.Instance.Update();
        }

        public override void Unload() {
            TungstenRiot.UnLoad();
        }
    }
}
