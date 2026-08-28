using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Broadswords
{
    /// <summary>
    /// 【利刃手套】材质：指节镶深红钢刃的拳套。签名：①五拍贴身爪连击，
    /// 触及全族最短、节奏全族最密 ②第 5 拍三重爪痕：三道错开角度的涂抹扇形
    /// 同时撕开，伤害 +30% ③命中短促撕裂火花
    /// </summary>
    internal class GsBladedGlove : GsBroadswordScheme
    {
        public override int TargetItemID => ItemID.BladedGlove;

        protected override int HeldProjID => ModContent.ProjectileType<GsBladedGloveHeld>();

        protected override int ComboBeats => 5;

        protected override string GsDescFallback =>
            "Reforged: a five-beat claw flurry at point-blank range; " +
            "the fifth strike rakes with a triple claw mark for bonus damage";

        //深红钢色板
        internal static readonly Color ClawBright = new(242, 152, 142); //钢刃亮红
        internal static readonly Color ClawMain = new(152, 42, 52);     //深红钢身
        internal static readonly Color ClawHot = new(255, 98, 64);      //撕裂灼红
        internal static readonly Color ClawDeep = new(42, 14, 18);      //干血暗红

        //原版本身极快，包络收在 1.1 以内：底伤 +5%，
        //第 5 拍 1.3x 均摊到五拍约 +6%，综合 DPS 约为原版 108%~111%
        public override void GsModifyWeaponDamage(Item item, Player player, ref StatModifier damage)
            => damage *= 1.05f;
    }

    /// <summary>
    /// 利刃手套手持：五拍爪连击。0~3 拍极短交替抓挠（Raise 2~3/Slash 2/Recover 4），
    /// 第 5 拍三重爪痕（残影上调 + 两道错角涂抹成三爪扇形，+30% 伤害，小前压）。
    /// ai[0]=拍号 ai[1]=交替符号
    /// </summary>
    internal class GsBladedGloveHeld : GsBroadswordHeldBase
    {
        protected override int SwordItemID => ItemID.BladedGlove;
        protected override Color EdgeBright => GsBladedGlove.ClawBright;
        protected override Color BodyMain => GsBladedGlove.ClawMain;
        protected override Color HotAccent => GsBladedGlove.ClawHot;
        protected override Color DeepShadow => GsBladedGlove.ClawDeep;

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

        //第 5 拍残影上调，爪痕更密
        protected override int GhostCount => IsFinisher ? 5 : 2;
        protected override float GhostSpacing => IsFinisher ? 0.16f : 0.13f;

        /// <summary>三爪扇形：第 5 拍斩切期在主涂抹两侧再画两道错开角度的涂抹</summary>
        protected override void DrawExtra(SpriteBatch sb, Color lightColor) {
            if (!IsFinisher || slashProgress <= 0.02f || fanFade <= 0.02f) {
                return;
            }
            Texture2D wave = CWRAsset.SemiCircularSmear?.Value;
            if (wave == null) {
                return;
            }
            float alpha = fanFade * (0.22f + slashProgress * 0.28f);
            //两道副爪痕分列主弧两侧，角度错开成扇
            for (int i = -1; i <= 1; i += 2) {
                float ang = mainAngle + i * 0.42f;
                Vector2 at = Hand + (ang.ToRotationVector2() * mainReach * 0.5f) - Main.screenPosition;
                Color c = GsBladedGlove.ClawHot * (alpha * 0.8f);
                c.A = 0;
                sb.Draw(wave, at, null, c, ang + (swingDir * 0.35f), wave.Size() / 2f,
                    new Vector2(0.34f, 0.12f) * (mainReach / 118f), SpriteEffects.None, 0f);
            }
        }

        protected override void OnHitFX(NPC target, NPC.HitInfo hit, int damageDone) {
            base.OnHitFX(target, hit, damageDone);
            //短促撕裂火花：快生快灭的小灼红
            int rips = IsFinisher ? 6 : 3;
            Vector2 aimDir = (mainAngle + (swingDir * MathHelper.PiOver2)).ToRotationVector2();
            for (int i = 0; i < rips; i++) {
                Vector2 vel = aimDir.RotatedByRandom(0.5) * Main.rand.NextFloat(4f, 7f);
                PRTLoader.NewParticle<PRT_Spark>(target.Center, vel, GsBladedGlove.ClawHot,
                    Main.rand.NextFloat(0.3f, 0.45f))?.Configure(true, Main.rand.Next(6, 11));
            }
            if (IsFinisher) {
                //三爪撕开的一瞬：三道细线沿爪向飞出
                for (int i = -1; i <= 1; i++) {
                    Vector2 vel = (mainAngle + i * 0.42f).ToRotationVector2()
                        * Main.rand.NextFloat(3f, 5f);
                    PRTLoader.NewParticle<PRT_Line>(target.Center, vel, GsBladedGlove.ClawBright,
                        Main.rand.NextFloat(0.3f, 0.5f))?.Configure(true, Main.rand.Next(10, 16));
                }
            }
        }

        protected override void HandleParticles(int phase) {
            //贴身快爪不用族默认量，改小而密的灼红碎火
            if (phase != PhaseSlash) {
                return;
            }
            int count = IsFinisher ? 2 : 1;
            Vector2 sweepVel = (mainAngle + (swingDir * MathHelper.PiOver2)).ToRotationVector2();
            for (int i = 0; i < count; i++) {
                Vector2 at = Vector2.Lerp(Hand, mainTip, Main.rand.NextFloat(0.5f, 1f));
                Color c = Main.rand.NextBool(3) ? GsBladedGlove.ClawHot : GsBladedGlove.ClawBright;
                PRTLoader.NewParticle<PRT_Spark>(at, sweepVel * Main.rand.NextFloat(2f, 4.5f), c,
                    Main.rand.NextFloat(0.22f, 0.38f))?.Configure(true, Main.rand.Next(7, 12));
            }
        }
    }
}
