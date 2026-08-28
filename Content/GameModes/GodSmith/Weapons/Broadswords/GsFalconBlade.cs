using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Broadswords
{
    /// <summary>
    /// 【轻捷猎刃】材质：银蓝轻锻的猎手快剑。签名：①全拍最速的三连猎斩
    /// ②「猎鹰步」：终结拍向瞄准方向扑击（前压 5.5 + 小幅垂直分量），残影加倍
    /// ③命中风切白羽——白色短线沿挥砍切线飞散
    /// </summary>
    internal class GsFalconBlade : GsBroadswordScheme
    {
        public override int TargetItemID => ItemID.FalconBlade;

        protected override int HeldProjID => ModContent.ProjectileType<GsFalconBladeHeld>();

        protected override string GsDescFallback =>
            "Reforged: the fastest three-cut combo of its tier; " +
            "the third strike is a falcon dive that pounces toward your aim, trailing doubled afterimages";

        //银蓝轻色板
        internal static readonly Color FalconBright = new(228, 236, 246); //银白刃缘
        internal static readonly Color FalconMain = new(170, 186, 206);   //淡银身
        internal static readonly Color FalconHot = new(146, 194, 255);    //猎空亮蓝
        internal static readonly Color FalconDeep = new(24, 28, 38);      //暗银影

        //底伤 +5%：三拍全速（快斩 0.92x×2 + 扑击 1.25x）+ 猎鹰步机动收益，
        //按 max(useTime, 弹幕总帧) 摊算综合 DPS 约为原版 105%~110%
        public override void GsModifyWeaponDamage(Item item, Player player, ref StatModifier damage)
            => damage *= 1.05f;
    }

    /// <summary>
    /// 轻捷猎刃手持：三拍全速。0/1 交替快斩（举滞斩收全压缩），
    /// 2 猎鹰步扑击（LungeSpeed 5.5 + 瞄准方向小幅位移含垂直分量，残影 +2）。
    /// ai[0]=拍号 ai[1]=交替符号
    /// </summary>
    internal class GsFalconBladeHeld : GsBroadswordHeldBase
    {
        protected override int SwordItemID => ItemID.FalconBlade;
        protected override Color EdgeBright => GsFalconBlade.FalconBright;
        protected override Color BodyMain => GsFalconBlade.FalconMain;
        protected override Color HotAccent => GsFalconBlade.FalconHot;
        protected override Color DeepShadow => GsFalconBlade.FalconDeep;

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
                //猎鹰步：向瞄准方向扑击
                _ => new GsBroadBeat {
                    Raise = 5, Hold = 2, Slash = 4, Recover = 8,
                    RaiseBack = 2f, Follow = 1.2f, ReachScale = 1.12f, LeanAmp = 0.075f,
                    DamageMult = 1.25f, Hitstop = 2, LungeSpeed = 5.5f, SwingPitch = -0.1f,
                },
            };
        }

        /// <summary>扑击拍残影加倍（5 层），快斩拍保持轻盈 2 层</summary>
        protected override int GhostCount => IsFinisher ? 5 : 2;
        protected override float GhostSpacing => IsFinisher ? 0.2f : 0.16f;

        protected override Color GlowColor => GsFalconBlade.FalconHot;

        /// <summary>
        /// 猎鹰步：base 已沿朝向前压 5.5，这里再叠小幅瞄准方向位移（含垂直分量），
        /// 俯冲/跃斩都跟手；owner 端权威，位置随原版同步
        /// </summary>
        protected override void OnSlashBegin() {
            if (!IsFinisher || Owner.whoAmI != Main.myPlayer || Owner.mount.Active) {
                return;
            }
            Vector2 aim = baseAngle.ToRotationVector2();
            Owner.velocity.X += aim.X * 1.6f;
            Owner.velocity.Y += MathHelper.Clamp(aim.Y * 2.4f, -3.5f, 2.5f);
        }

        /// <summary>命中风切白羽：白色短线沿挥砍切线飞散，扑击拍更密</summary>
        protected override void OnHitFX(NPC target, NPC.HitInfo hit, int damageDone) {
            base.OnHitFX(target, hit, damageDone);
            Vector2 tangent = (mainAngle + (swingDir * MathHelper.PiOver2)).ToRotationVector2();
            int feathers = IsFinisher ? 6 : 3;
            for (int i = 0; i < feathers; i++) {
                Vector2 vel = tangent.RotatedByRandom(0.45) * Main.rand.NextFloat(4f, 9f);
                PRTLoader.NewParticle<PRT_Line>(target.Center, vel,
                    Color.Lerp(GsFalconBlade.FalconBright, Color.White, 0.5f),
                    Main.rand.NextFloat(0.5f, 0.8f))?.Configure(true, Main.rand.Next(10, 16));
            }
        }

        protected override void HandleParticles(int phase) {
            base.HandleParticles(phase);
            //扑击途中刀尖拖出银蓝气流线
            if (IsFinisher && phase == PhaseSlash && Main.rand.NextBool(2)) {
                Vector2 at = Vector2.Lerp(Hand, mainTip, Main.rand.NextFloat(0.7f, 1f));
                PRTLoader.NewParticle<PRT_Line>(at, -Owner.velocity * 0.3f
                    + Main.rand.NextVector2Circular(1f, 1f), GsFalconBlade.FalconHot,
                    Main.rand.NextFloat(0.35f, 0.55f))?.Configure(false, Main.rand.Next(8, 13));
            }
        }
    }
}
