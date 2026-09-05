using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Broadswords
{
    /// <summary>
    /// 【染匠异国弯刀】材质：浸饱染料的异国弯刃。签名：①弯刀长弧几何：小后摆大跟进的流畅弧线
    /// ②终结拍最长的满弧重斩
    /// </summary>
    internal class GsDyeTradersScimitar : GsBroadswordScheme
    {
        public override int TargetItemID => ItemID.DyeTradersScimitar;

        protected override int HeldProjID => ModContent.ProjectileType<GsDyeTradersScimitarHeld>();

        protected override string GsDescFallback =>
            "Reforged: each sweep dyes its arc a new color of the rainbow; the third stroke flares all seven at once and splatters the target with dye";
        internal static readonly Color DyeBright = new(250, 222, 158); //暖金刃缘
        internal static readonly Color DyeMain = new(46, 132, 150);    //孔雀蓝身
        internal static readonly Color DyeHot = new(255, 196, 96);     //鎏金亮

        //底伤 +15%：商店弯刀本就偏弱，终结拍已含 1.3x 拍伤，
        //按 max(useTime, 弹幕总帧) 摊算综合 DPS 约为原版 112%~120%
        public override void GsModifyWeaponDamage(Item item, Player player, ref StatModifier damage)
            => damage *= 1.15f;
    }

    /// <summary>
    /// 染匠异国弯刀手持：三拍长弧。0/1 流畅弯斩（小后摆大跟进），2 满弧终结重斩。
    /// ai[0]=拍号 ai[1]=交替符号
    /// </summary>
    internal class GsDyeTradersScimitarHeld : GsBroadswordHeldBase
    {
        protected override int SwordItemID => ItemID.DyeTradersScimitar;
        protected override Color EdgeBright => GsDyeTradersScimitar.DyeBright;
        protected override Color BodyMain => GsDyeTradersScimitar.DyeMain;
        protected override Color HotAccent => GsDyeTradersScimitar.DyeHot;

        protected override GsBroadBeat GetBeat(int stage) {
            return stage switch {
                //弯斩一：小后摆大跟进的流畅长弧
                0 => new GsBroadBeat {
                    Raise = 5, Hold = 2, Slash = 5, Recover = 9,
                    RaiseBack = 1.1f, Follow = 1.7f, ReachScale = 1f, LeanAmp = 0.045f,
                    DamageMult = 0.95f, Hitstop = 1, LungeSpeed = 0f, SwingPitch = 0f,
                },
                //弯斩二：弧更长
                1 => new GsBroadBeat {
                    Raise = 5, Hold = 2, Slash = 5, Recover = 9,
                    RaiseBack = 1f, Follow = 1.8f, ReachScale = 1f, LeanAmp = 0.045f,
                    DamageMult = 0.95f, Hitstop = 1, LungeSpeed = 0f, SwingPitch = 0.1f,
                },
                //终结：最长的满弧重斩
                _ => new GsBroadBeat {
                    Raise = 7, Hold = 3, Slash = 6, Recover = 11,
                    RaiseBack = 1.35f, Follow = 2.1f, ReachScale = 1.1f, LeanAmp = 0.075f,
                    DamageMult = 1.3f, Hitstop = 2, LungeSpeed = 2.4f, SwingPitch = -0.12f,
                },
            };
        }
    }
}
