using Terraria.ModLoader;

namespace CalamityOverhaul.Content.ADV.Scenarios.VoidColonys.GlitchWraith
{
    /// <summary>鬼乱码生成（当前逻辑已注释）</summary>
    internal class GlitchWraithSpawner : ModSystem
    {
        ////生成距离，初次 spawn 足够远
        //private const float MinSpawnDistance = 2200f;
        //private const float MaxSpawnDistance = 3000f;

        //public override void PostUpdateEverything() {
        //   if (Main.dedServ) return;
        //   if (Main.netMode == NetmodeID.MultiplayerClient) return;
        //   if (!VoidColony.Active) return;

        //   //厉鬼唯一，已经存在就不再生成
        //   if (ActorLoader.GetActiveActors<GlitchWraithActor>().Count > 0) return;
        //   //Spawner(Main.LocalPlayer);
        //}

        //public static void Spawner(Player target) {
        //   if (target == null || !target.active || target.dead) return;

        //   float angle = Main.rand.NextFloat(MathHelper.TwoPi);
        //   float radius = Main.rand.NextFloat(MinSpawnDistance, MaxSpawnDistance);
        //   Vector2 spawn = target.Center + angle.ToRotationVector2() * radius;

        //   ActorLoader.NewActor<GlitchWraithActor>(spawn, Vector2.Zero);
        //}
    }
}
