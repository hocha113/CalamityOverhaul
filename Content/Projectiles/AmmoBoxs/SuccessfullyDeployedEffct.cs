using Terraria;
using Terraria.GameContent;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.AmmoBoxs
{
    internal class SuccessfullyDeployedEffct : ModProjectile
    {
        public override string Texture => CWRConstant.Placeholder;
        public string text = "Null";
        public Color textColor = Color.White;
        public Color fontColor = Color.Black;
        public override void SetDefaults() {
            Projectile.width = Projectile.height = 22;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 120;
            Projectile.scale = 2;
            Projectile.alpha = 0;
        }

        public override void AI() {
            Projectile.Center = Main.player[Projectile.owner].Center;
            if (Projectile.ai[0] < 15) {
                Projectile.alpha += 15;
            }
            if (Projectile.ai[0] >= 120 - 15) {
                Projectile.alpha -= 15;
            }
            Projectile.ai[0]++;
        }

        public override bool PreDraw(ref Color lightColor) {
            if (Projectile.IsOwnedByLocalPlayer()) {
                float textAlp = Projectile.alpha / 255f;
                Vector2 pos = new Vector2(Main.screenWidth / 2, Main.screenHeight * 0.65f)
                    - (FontAssets.ItemStack.Value.MeasureString(text) / 2 * Projectile.scale);
                Utils.DrawBorderStringFourWay(Main.spriteBatch, FontAssets.ItemStack.Value, text
                    , pos.X, pos.Y, textColor * textAlp, fontColor * textAlp, Vector2.Zero, Projectile.scale);
            }
            return false;
        }
    }
}
