using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Broadswords
{
    /// <summary>
    /// 【轻捷猎刃】材质：银蓝轻锻的猎手快剑。签名：①全拍最速的三连猎斩
    /// ②「猎鹰扑斩」：终结拍弧更大、触及更远、更重（玩家本体不位移）
    /// </summary>
    internal class GsFalconBlade : GsBroadswordScheme
    {
        public override int TargetItemID => ItemID.FalconBlade;

        protected override int HeldProjID => ModContent.ProjectileType<GsFalconBladeHeld>();

        protected override string GsDescFallback =>
            "Reforged: the fastest three-cut combo of its tier; the third strike is a heavier, longer falcon dive";
        internal static readonly Color FalconBright = new(228, 236, 246); //银白刃缘
        internal static readonly Color FalconMain = new(170, 186, 206);   //淡银身
        internal static readonly Color FalconHot = new(146, 194, 255);    //猎空亮蓝

        //底伤 +5%：三拍全速（快斩 0.92x×2 + 扑斩 1.25x），
        //按 max(useTime, 弹幕总帧) 摊算综合 DPS 约为原版 105%~110%
        public override void GsModifyWeaponDamage(Item item, Player player, ref StatModifier damage)
            => damage *= 1.05f;
    }

    /// <summary>
    /// 轻捷猎刃手持：三拍全速。0/1 交替快斩（举滞斩收全压缩），
    /// 2 猎鹰扑斩（触及 1.12 倍、弧更大、伤害 1.25 倍）。
    /// ai[0]=拍号 ai[1]=交替符号
    /// </summary>
    internal class GsFalconBladeHeld : GsBroadswordHeldBase
    {
        protected override int SwordItemID => ItemID.FalconBlade;
        protected override Color EdgeBright => GsFalconBlade.FalconBright;
        protected override Color BodyMain => GsFalconBlade.FalconMain;
        protected override Color HotAccent => GsFalconBlade.FalconHot;

        protected override GsBroadBeat GetBeat(int stage) {
            return stage switch {
                //猎斩一：全相压缩的最速起手
                0 => new GsBroadBeat {
                    Raise = 4, Hold = 1, Slash = 3, Recover = 6,
                    RaiseBack = 1.7f, Follow = 0.95f, ReachScale = 1f, LeanAmp = 0.035f,
                    DamageMult = 0.92f, Hitstop = 1, LungeSpeed = 0f, SwingPitch = 0.12f,
                },
                //猎斩二：回手更快
                1 => new GsBroadBeat {
                    Raise = 3, Hold = 1, Slash = 3, Recover = 6,
                    RaiseBack = 1.5f, Follow = 1f, ReachScale = 1f, LeanAmp = 0.035f,
                    DamageMult = 0.92f, Hitstop = 1, LungeSpeed = 0f, SwingPitch = 0.2f,
                },
                //猎鹰扑斩：大弧远触及的重终结
                _ => new GsBroadBeat {
                    Raise = 5, Hold = 2, Slash = 4, Recover = 8,
                    RaiseBack = 2f, Follow = 1.2f, ReachScale = 1.12f, LeanAmp = 0.075f,
                    DamageMult = 1.25f, Hitstop = 2, LungeSpeed = 0f, SwingPitch = -0.1f,
                },
            };
        }
    }
}
