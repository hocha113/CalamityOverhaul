using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;

namespace CalamityOverhaul.Content.RemakeItems.Melee.ArkoftheOverride
{
    internal class ArkoftheAsset : ICWRLoader
    {
        public static Asset<Texture2D> RendingScissorsRight;
        public static Asset<Texture2D> RendingScissorsLeft;
        public static Asset<Texture2D> RendingScissorsRightGlow;
        public static Asset<Texture2D> RendingScissorsLeftGlow;
        public static Asset<Texture2D> TrientCircularSmear;
        public static Asset<Texture2D> SunderingScissorsRight;
        public static Asset<Texture2D> SunderingScissorsLeft;
        public static Asset<Texture2D> SunderingScissorsRightGlow;
        public static Asset<Texture2D> SunderingScissorsLeftGlow;
        public static Asset<Texture2D> SunderingScissorsGlow;
        void ICWRLoader.LoadAsset() {
            RendingScissorsRight = CWRUtils.GetT2DAsset("CalamityMod/Projectiles/Melee/RendingScissorsRight");
            RendingScissorsLeft = CWRUtils.GetT2DAsset("CalamityMod/Projectiles/Melee/RendingScissorsLeft");
            RendingScissorsRightGlow = CWRUtils.GetT2DAsset("CalamityMod/Projectiles/Melee/RendingScissorsRightGlow");
            RendingScissorsLeftGlow = CWRUtils.GetT2DAsset("CalamityMod/Projectiles/Melee/RendingScissorsLeftGlow");
            TrientCircularSmear = CWRUtils.GetT2DAsset("CalamityMod/Particles/TrientCircularSmear");
            SunderingScissorsRight = CWRUtils.GetT2DAsset("CalamityMod/Projectiles/Melee/SunderingScissorsRight");
            SunderingScissorsLeft = CWRUtils.GetT2DAsset("CalamityMod/Projectiles/Melee/SunderingScissorsLeft");
            SunderingScissorsRightGlow = CWRUtils.GetT2DAsset("CalamityMod/Projectiles/Melee/SunderingScissorsRightGlow");
            SunderingScissorsLeftGlow = CWRUtils.GetT2DAsset("CalamityMod/Projectiles/Melee/SunderingScissorsLeftGlow");
            SunderingScissorsGlow = CWRUtils.GetT2DAsset("CalamityMod/Projectiles/Melee/SunderingScissorsGlow");
        }
    }
}
