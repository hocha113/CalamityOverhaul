using CalamityOverhaul.Content.Tiles;
using CalamityOverhaul.Content.UIs.CompressorUIs;
using InnoVault.TileProcessors;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace CalamityOverhaul.Content.TileProcessors
{
    internal class CompressorTP : TileProcessor
    {
        public override int TargetTileID => ModContent.TileType<DarkMatterCompressor>();
        internal CompressorUI compressorUIInstance;
        private const int maxleng = 120;
        private bool mouseOnTile;
        internal bool drawGlow;
        internal Color gloaColor;
        private int gloawTime;
        internal int frame;
        internal Vector2 Center => PosInWorld + new Vector2(DarkMatterCompressor.Width, DarkMatterCompressor.Height) * 8;
        public override void Update() {
            VaultUtils.ClockFrame(ref frame, 8, 3);
            if (frame != 2) {
                Lighting.AddLight(Center, Color.White.ToVector3() * (Main.GameUpdateCount % 40 / 40f));
            }

            if (compressorUIInstance != null && !CompressorUI.Instance.Active) {
                compressorUIInstance.TPUpdate();
            }

            Player player = Main.LocalPlayer;
            if (!player.active || Main.myPlayer != player.whoAmI) {
                return;
            }

            if (mouseOnTile) {
                Lighting.AddLight(Center, Color.White.ToVector3());
            }

            CWRPlayer modPlayer = player.CWR();

            if (VaultUtils.isServer) {
                return;
            }

            Rectangle tileRec = new Rectangle(Position.X * 16, Position.Y * 16, BloodAltar.Width * 18, BloodAltar.Height * 18);
            mouseOnTile = tileRec.Intersects(new Rectangle((int)Main.MouseWorld.X, (int)Main.MouseWorld.Y, 1, 1));

            float leng = PosInWorld.Distance(player.Center);
            drawGlow = leng < maxleng && mouseOnTile && !CompressorUI.Instance.Active;
            if (drawGlow) {
                gloawTime++;
                gloaColor = Color.AliceBlue * MathF.Abs(MathF.Sin(gloawTime * 0.04f));
            }
            else {
                gloawTime = 0;
            }

            if (!CompressorUI.Instance.Active) {
                return;
            }

            if ((leng >= maxleng || player.dead) && modPlayer.CompressorContrType == WhoAmI) {
                CompressorUI.Instance.Active = false;
                modPlayer.CompressorContrType = -1;
                SoundEngine.PlaySound(SoundID.MenuClose with { Pitch = -0.2f });
            }
        }
    }
}
