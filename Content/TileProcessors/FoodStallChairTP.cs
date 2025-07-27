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
        private bool inFoodStallChair;
        private RainDataStruct rainData = new();
        private struct RainDataStruct
        {
            public bool raining;
            public float maxRaining;
            public float cloudAlpha;
            public float windSpeedTarget;
        }
        public override void Update() {
            foreach (var player in Main.ActivePlayers) {
                if (player.sitting.isSitting
                && player.position.DistanceSQ(PosInWorld) < 1024) {
                    Main.LocalPlayer.CWR().InFoodStallChair = true;
                }
            }

            if (inFoodStallChair) {
                Main.raining = true;
                Main.maxRaining = 0.99f;
                Main.cloudAlpha = 0.99f;
                Main.windSpeedTarget = 0.8f;
                float sengs = Math.Abs(MathF.Sin(Main.GameUpdateCount * 0.05f));
                Lighting.AddLight(Main.LocalPlayer.Center
                    , new Color(Main.DiscoB, Main.DiscoG, 220 + (sengs * 30)).ToVector3() * sengs * 113);
                PunchCameraModifier modifier2 = new(Main.LocalPlayer.Center
                    , new Vector2(0, Main.rand.NextFloat(-2, 2)), 2f, 3f, 2, 1000f, nameof(FoodStallChairTP));
                Main.instance.CameraModifiers.Add(modifier2);
            }

            var newInFoodStallChair = Main.LocalPlayer.CWR().InFoodStallChair;
            if (newInFoodStallChair && !inFoodStallChair) {//说明刚坐上去，记录天气状况
                rainData = new RainDataStruct() {
                    raining = Main.raining,
                    maxRaining = Main.maxRaining,
                    cloudAlpha = Main.cloudAlpha,
                    windSpeedTarget = Main.windSpeedTarget,
                };
            }
            if (!newInFoodStallChair && inFoodStallChair) {//说明刚下来，恢复记录前的天气状况
                Main.raining = rainData.raining;
                Main.maxRaining = rainData.maxRaining;
                Main.cloudAlpha = rainData.cloudAlpha;
                Main.windSpeedTarget = rainData.windSpeedTarget;
            }
            inFoodStallChair = newInFoodStallChair;
        }
    }
}
