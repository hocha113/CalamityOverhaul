using Microsoft.Xna.Framework.Graphics;

namespace CalamityOverhaul.Content.Items.Melee.CursedflameBloodfists
{
    /// <summary>
    /// 咒焰血拳的贴图路径与火焰色板。色值直接取自玩家给的两张贴图：
    /// 亮部是诅咒焰的荧光绿，暗部是烧穿血肉的锈橙，中间由 <see cref="Ramp"/> 过渡
    /// </summary>
    internal static class CursedflameFX
    {
        /// <summary>整条燃烧的断臂，物品图标</summary>
        public const string ItemTexture = CWRConstant.Item_Melee + "CursedflameBloodfist";
        /// <summary>紧凑的火焰拳头，握持出拳与飞行弹幕共用</summary>
        public const string FistTexture = CWRConstant.Projectile_Melee + "CursedflameFist";

        /// <summary>
        /// 拳头贴图朝下的一端是拳锋、朝上的一端是燃烧的断口。
        /// 把贴图转到飞行方向时统一加这个偏移，拳锋在前、绿焰在后接进拖尾。
        /// 若发现方向反了，只需把这里改成 +PiOver2
        /// </summary>
        public const float FistRotationOffset = -MathHelper.PiOver2;

        //高饱和取向：焰心不发白、深色段不发灰，整条斜坡都钉在纯色相上
        /// <summary>焰心，最热的一档，亮而不白</summary>
        public static readonly Color FlameCore = new(168, 255, 64);
        public static readonly Color FlameGreen = new(120, 255, 0);
        public static readonly Color FlameMoss = new(104, 216, 0);
        public static readonly Color FlameOrange = new(224, 96, 0);
        public static readonly Color FlameRust = new(160, 64, 0);
        /// <summary>烧尽的焦棕，火舌末端与余烬落点</summary>
        public static readonly Color FlameChar = new(52, 24, 0);
        /// <summary>拖尾末端的深绿，拖尾整条只在绿系里走</summary>
        public static readonly Color TrailDeep = new(34, 132, 4);
        /// <summary>手臂本体的血色，命中飞溅用</summary>
        public static readonly Color Blood = new(161, 0, 0);

        /// <summary>火焰冷却斜坡，0 是最热的绿核，1 是烧尽的焦橙</summary>
        public static Color Ramp(float t) => VaultUtils.MultiStepColorLerp(MathHelper.Clamp(t, 0f, 1f)
            , FlameCore, FlameGreen, FlameMoss, FlameOrange, FlameRust, FlameChar);

        public static Texture2D SoftGlow => CWRAsset.SoftGlow?.Value;
        public static Texture2D Streak => CWRAsset.Extra_98?.Value;
        public static Texture2D Voronoi => CWRAsset.Extra_193?.Value;
        public static Texture2D Gradient => CWRAsset.CursedflameFist_Bar?.Value;
    }
}
