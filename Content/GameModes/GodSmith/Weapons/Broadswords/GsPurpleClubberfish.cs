using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Broadswords
{
    /// <summary>
    /// 【紫棒鲈】材质：湿滑弹韧的整条鱼肉钝器。签名：①宽扫钝拍几何，击退 +60%，
    /// 挥起来带 Boing 的高音弹性 ②命中炸开大水花并淋湿目标 ③拍在着火的目标身上
    /// 会浇灭火焰且该次 +20% 伤害（灭火拍鱼）
    /// </summary>
    internal class GsPurpleClubberfish : GsBroadswordScheme
    {
        public override int TargetItemID => ItemID.PurpleClubberfish;

        protected override int HeldProjID => ModContent.ProjectileType<GsPurpleClubberfishHeld>();

        protected override string GsDescFallback =>
            "Reforged: a wide wet slap with massive knockback that drenches whatever it hits; " +
            "slapping a burning enemy puts the fire out for bonus damage";

        //湿鱼色板
        internal static readonly Color FishBright = new(255, 172, 208); //鱼腹粉
        internal static readonly Color FishMain = new(188, 112, 192);   //鱼背紫
        internal static readonly Color FishHot = new(138, 216, 255);    //水花淡蓝
        internal static readonly Color FishDeep = new(48, 26, 54);      //深水暗紫

        //公认弱势趣味武器，包络放宽到 130%：底伤 +25%，
        //终结拍 1.2x + 灭火 +20%（条件触发不常驻），综合 DPS 约为原版 125%~130%
        public override void GsModifyWeaponDamage(Item item, Player player, ref StatModifier damage)
            => damage *= 1.25f;

        /// <summary>钝拍击退 +60%，拍鱼的意义就在把人拍飞</summary>
        public override void GsModifyWeaponKnockback(Item item, Player player, ref StatModifier knockback)
            => knockback *= 1.6f;
    }

    /// <summary>
    /// 紫棒鲈手持：三拍宽扫钝拍。RaiseBack 小 Follow 大（不往回拉、往前抡穿），
    /// 音高上扬带弹性；命中水花四溅，着火目标被浇灭。ai[0]=拍号 ai[1]=交替符号
    /// </summary>
    internal class GsPurpleClubberfishHeld : GsBroadswordHeldBase
    {
        protected override int SwordItemID => ItemID.PurpleClubberfish;
        protected override Color EdgeBright => GsPurpleClubberfish.FishBright;
        protected override Color BodyMain => GsPurpleClubberfish.FishMain;
        protected override Color HotAccent => GsPurpleClubberfish.FishHot;
        protected override Color DeepShadow => GsPurpleClubberfish.FishDeep;

        /// <summary>本次命中浇灭了火（ModifyHitExtra 置位，命中反馈消费）</summary>
        private bool dousedFire;

        //鱼身宽厚，判定跟着加宽
        protected override float CollisionWidth => 52f;
        protected override float BaseReach => 126f;

        protected override GsBroadBeat GetBeat(int stage) {
            if (stage == 2) {
                //终结大抡：整条鱼抡满一个大圆弧把人拍飞
                return new GsBroadBeat {
                    Raise = 8, Hold = 2, Slash = 6, Recover = 12,
                    RaiseBack = 1.5f, Follow = 1.9f, ReachScale = 1.12f, LeanAmp = 0.09f,
                    DamageMult = 1.2f, Hitstop = 3, LungeSpeed = 2.2f, SwingPitch = 0.55f,
                };
            }
            //钝拍几何：后摆小、跟进大，是「抡穿」不是「劈到位」
            GsBroadBeat b = GsBroadBeat.Standard;
            b.Raise = 7;
            b.Slash = 5;
            b.RaiseBack = 1.3f;
            b.Follow = 1.5f;
            b.LeanAmp = 0.06f;
            b.SwingPitch = stage == 0 ? 0.42f : 0.34f;//Boing 的湿肉弹性音
            return b;
        }

        /// <summary>拍在着火的目标身上 +20%（火随后在 OnHitTarget 里被浇灭）</summary>
        protected override void ModifyHitExtra(NPC target, ref NPC.HitModifiers modifiers) {
            if (target.HasBuff(BuffID.OnFire) || target.HasBuff(BuffID.OnFire3)) {
                dousedFire = true;
                modifiers.FinalDamage *= 1.2f;
            }
        }

        protected override void OnHitTarget(NPC target, NPC.HitInfo hit, int damageDone) {
            //浇灭火焰（RequestBuffRemoval 联机下自带同步）
            if (dousedFire) {
                target.RequestBuffRemoval(BuffID.OnFire);
                target.RequestBuffRemoval(BuffID.OnFire3);
            }
        }

        protected override void OnHitFX(NPC target, NPC.HitInfo hit, int damageDone) {
            base.OnHitFX(target, hit, damageDone);
            //大水花：水尘喷溅 + 溅水音
            SoundEngine.PlaySound(SoundID.Splash with { Volume = 0.75f, Pitch = 0.2f }, target.Center);
            int drops = IsFinisher ? 16 : 10;
            for (int i = 0; i < drops; i++) {
                Dust d = Dust.NewDustPerfect(target.Center, DustID.Water,
                    Main.rand.NextVector2Unit() * Main.rand.NextFloat(2f, 6.5f), 40, default,
                    Main.rand.NextFloat(0.9f, 1.5f));
                d.noGravity = Main.rand.NextBool(3);
            }
            PRTLoader.NewParticle<PRT_Light>(target.Center, Vector2.Zero,
                GsPurpleClubberfish.FishHot, 0.22f)?.Configure(10, 0.8f);

            //灭火反馈：一团白汽升腾
            if (dousedFire) {
                dousedFire = false;
                SoundEngine.PlaySound(SoundID.LiquidsWaterLava with { Volume = 0.8f }, target.Center);
                for (int i = 0; i < 8; i++) {
                    Dust d = Dust.NewDustPerfect(target.Center, DustID.Smoke,
                        new Vector2(Main.rand.NextFloat(-1f, 1f), -Main.rand.NextFloat(1f, 3f)), 120,
                        default, Main.rand.NextFloat(1f, 1.6f));
                    d.noGravity = true;
                }
            }
        }

        protected override void HandleParticles(int phase) {
            base.HandleParticles(phase);
            //挥动时甩出湿滑水珠
            if (phase == PhaseSlash && Main.rand.NextBool(2)) {
                Dust d = Dust.NewDustPerfect(Vector2.Lerp(Hand, mainTip, Main.rand.NextFloat(0.6f, 1f)),
                    DustID.Water, (mainAngle + swingDir * MathHelper.PiOver2).ToRotationVector2()
                    * Main.rand.NextFloat(2f, 4f), 60, default, Main.rand.NextFloat(0.8f, 1.2f));
                d.noGravity = false;
            }
        }
    }
}
