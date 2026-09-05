using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Broadswords
{
    /// <summary>
    /// 【利刃手套】材质：指节镶深红钢刃的拳套。签名：①五拍贴身爪连击，
    /// 触及全族最短、节奏全族最密 ②第 5 拍三重爪痕，伤害 +30%
    /// </summary>
    internal class GsBladedGlove : GsBroadswordScheme
    {
        public override int TargetItemID => ItemID.BladedGlove;

        protected override int HeldProjID => ModContent.ProjectileType<GsBladedGloveHeld>();

        protected override int ComboBeats => 5;

        protected override string GsDescFallback =>
            "Reforged: a five-beat claw flurry at point-blank range; the fifth strike rakes with a triple claw mark for bonus damage";
        internal static readonly Color ClawBright = new(242, 152, 142); //钢刃亮红
        internal static readonly Color ClawMain = new(152, 42, 52);     //深红钢身
        internal static readonly Color ClawHot = new(255, 98, 64);      //撕裂灼红

        //原版本身极快，包络收在 1.1 以内：底伤 +5%，
        //第 5 拍 1.3x 均摊到五拍约 +6%，综合 DPS 约为原版 108%~111%
        public override void GsModifyWeaponDamage(Item item, Player player, ref StatModifier damage)
            => damage *= 1.05f;
    }

    /// <summary>
    /// 利刃手套手持：五拍爪连击。0~3 拍极短交替抓挠（Raise 2~3/Slash 2/Recover 4），
    /// 第 5 拍三重爪痕（+30% 伤害，小前压）。
    /// ai[0]=拍号 ai[1]=交替符号
    /// </summary>
    internal class GsBladedGloveHeld : GsBroadswordHeldBase
    {
        protected override int SwordItemID => ItemID.BladedGlove;
        protected override Color EdgeBright => GsBladedGlove.ClawBright;
        protected override Color BodyMain => GsBladedGlove.ClawMain;
        protected override Color HotAccent => GsBladedGlove.ClawHot;

        protected override int BeatCount => 5;
        //拳刃贴身：全族最短触及
        protected override float BaseReach => 70f;
        protected override float CollisionWidth => 34f;
        protected override float PointBlankRadius => 46f;

        protected override GsBroadBeat GetBeat(int stage) {
            if (stage == 4) {
                //三重爪痕终结：稍长的举拍蓄爪，撕开三道
                return new GsBroadBeat {
                    Raise = 4, Hold = 2, Slash = 3, Recover = 6,
                    RaiseBack = 1.7f, Follow = 1.0f, ReachScale = 1.12f, LeanAmp = 0.05f,
                    DamageMult = 1.3f, Hitstop = 2, LungeSpeed = 2f, SwingPitch = -0.1f,
                };
            }
            //极短抓挠拍：偶数拍略快于奇数拍，抓出不规则的密集节奏
            bool quick = stage % 2 == 0;
            return new GsBroadBeat {
                Raise = quick ? 2 : 3, Hold = 1, Slash = 2, Recover = 4,
                RaiseBack = 1.25f, Follow = 0.85f, ReachScale = 1f, LeanAmp = 0.02f,
                DamageMult = 1f, Hitstop = 1, LungeSpeed = 0f,
                SwingPitch = quick ? 0.4f : 0.28f,
            };
        }
    }
}
