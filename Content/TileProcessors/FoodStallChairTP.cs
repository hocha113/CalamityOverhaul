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

        public override void Update() {
            if (Main.LocalPlayer.sitting.isSitting && Main.LocalPlayer.position.DistanceSQ(PosInWorld) < 1024) {
                foreach (var player in Main.ActivePlayers) {
                    player.CWR().InFoodStallChair = true;
                }
            }

            if (Main.LocalPlayer.CWR().InFoodStallChair) {
                Main.raining = true;
                Main.maxRaining = 0.99f;
                Main.cloudAlpha = 0.99f;
                Main.windSpeedTarget = 0.8f;
                float sengs = Math.Abs(MathF.Sin(Main.GameUpdateCount * 0.05f));
                Lighting.AddLight(Main.LocalPlayer.Center, new Color(Main.DiscoB, Main.DiscoG, 220 + (sengs * 30)).ToVector3() * sengs * 113);
                PunchCameraModifier modifier2 = new(Main.LocalPlayer.Center
                    , new Vector2(0, Main.rand.NextFloat(-2, 2)), 2f, 3f, 2, 1000f, nameof(FoodStallChairTP));
                Main.instance.CameraModifiers.Add(modifier2);
            }
        }
    }
}
