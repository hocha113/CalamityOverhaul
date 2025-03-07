using InnoVault.TileProcessors;
using System;
using Terraria;
using Terraria.Graphics.CameraModifiers;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.TileProcessors
{
    internal class FoodStallChairTP : TileProcessor
    {
        public override int TargetTileID => ModContent.TileType<Tiles.FoodStallChair>();

        private Player player => Main.LocalPlayer;

        public override void Update() {
            if (player.sitting.isSitting && player.position.DistanceSQ(PosInWorld) < 1024) {
                player.CWR().InFoodStallChair = true;
                Main.raining = true;
                Main.maxRaining = 0.99f;
                Main.cloudAlpha = 0.99f;
                Main.windSpeedTarget = 0.8f;
                float sengs = Math.Abs(MathF.Sin(Main.GameUpdateCount * 0.05f));
                Lighting.AddLight(player.Center, new Color(Main.DiscoB, Main.DiscoG, 220 + (sengs * 30)).ToVector3() * sengs * 113);
                PunchCameraModifier modifier2 = new(player.Center
                    , new Vector2(0, Main.rand.NextFloat(-2, 2)), 2f, 3f, 2, 1000f, nameof(FoodStallChairTP));
                Main.instance.CameraModifiers.Add(modifier2);
            }
        }
    }
}
