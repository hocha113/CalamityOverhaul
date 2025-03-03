using CalamityMod.Tiles.DraedonStructures;
using CalamityOverhaul.Content.Industrials.Generator.Thermal;
using CalamityOverhaul.Content.Industrials.MaterialFlow;
using CalamityOverhaul.Content.Tiles.Core;
using InnoVault;
using InnoVault.TileProcessors;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Industrials.Modifys
{
    internal class ModifyChargingStation : TileOverride
    {
        public override int TargetID => ModContent.TileType<ChargingStation>();
        public override bool? RightClick(int i, int j, Tile tile) {
            if (TileProcessorLoader.AutoPositionGetTP<ChargingStationTP>(i, j, out var tp)) {
                tp.OpenUI = !tp.OpenUI;
                if (tp.OpenUI) {//如果是开启，就先关掉其他所有的同类实体的UI
                    foreach (var tp2 in TileProcessorLoader.TP_InWorld) {
                        if (tp2.ID != tp.ID || tp2.WhoAmI == tp.WhoAmI) {
                            continue;
                        }
                        if (tp2 is ChargingStationTP chargingStation) {
                            chargingStation.OpenUI = false;
                        }
                    }
                }
                SoundEngine.PlaySound(SoundID.MenuTick);
            }
            return false;
        }
    }

    internal class ChargingStationTP : BaseBattery//是的，把这个东西当成是一个电池会更好写
    {
        public override int TargetTileID => ModContent.TileType<ChargingStation>();
        internal bool OpenUI;
        internal float sengs;
        private bool hoverBar;
        private Vector2 MousePos;
        public override float MaxUEValue => 1000;
        public override void SetProperty() => MachineData = new MachineData();
        public override void Update() {
            if (OpenUI) {
                if (sengs < 1f) {
                    sengs += 0.1f;
                }
                Vector2 drawPos = CenterInWorld + new Vector2(0, -120) * sengs;
                drawPos += new Vector2(-30, 30);
                hoverBar = (drawPos).GetRectangle(60, 22).Intersects(Main.MouseWorld.GetRectangle(1));
                MousePos = Main.MouseScreen;
            }
            else {
                if (sengs > 0f) {
                    sengs -= 0.1f;
                }
            }
        }

        public void DrawUI(SpriteBatch spriteBatch) {
            Texture2D texture = CWRUtils.GetT2DValue(CWRConstant.UI + "Generator/GeneratorPanel");
            Texture2D slotTex = ModContent.Request<Texture2D>("CalamityMod/UI/DraedonsArsenal/ChargerWeaponSlot").Value;
            Vector2 drawPos = CenterInWorld - Main.screenPosition + new Vector2(0, -120) * sengs;
            spriteBatch.Draw(texture, drawPos, null, Color.White, 0, texture.Size() / 2, 0.75f * sengs, SpriteEffects.None, 0);
            spriteBatch.Draw(slotTex, drawPos, null, Color.White, 0, slotTex.Size() / 2, sengs, SpriteEffects.None, 0);

            Texture2D barTop = ModContent.Request<Texture2D>("CalamityMod/UI/DraedonsArsenal/ChargeMeterBorder").Value;
            Texture2D barFull = ModContent.Request<Texture2D>("CalamityMod/UI/DraedonsArsenal/ChargeMeter").Value;

            drawPos += new Vector2(-30, 30);
            int uiBarByWidthSengs = (int)(barFull.Width * (MachineData.UEvalue / MaxUEValue));
            // 绘制温度相关的图像
            Rectangle fullRec = new Rectangle(0, 0, uiBarByWidthSengs, barFull.Height);
            Main.spriteBatch.Draw(barTop, drawPos, null, Color.White * sengs, 0, Vector2.Zero, 1, SpriteEffects.None, 0);
            Main.spriteBatch.Draw(barFull, drawPos + new Vector2(10, 0), fullRec, Color.White * sengs, 0, Vector2.Zero, 1, SpriteEffects.None, 0);

            if (hoverBar) {
                Utils.DrawBorderStringFourWay(spriteBatch, FontAssets.MouseText.Value
                    , (MachineData.UEvalue + "/" + MaxUEValue + "UE").ToString()
                    , MousePos.X - 10, MousePos.Y + 20, Color.White, Color.Black, new Vector2(0.3f), 0.6f);
            }
        }

        public override void Draw(SpriteBatch spriteBatch) {
            if (OpenUI || sengs > 0) {
                DrawUI(spriteBatch);
            }
        }
    }
}
