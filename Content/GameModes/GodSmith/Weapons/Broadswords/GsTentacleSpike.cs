using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Broadswords
{
    /// <summary>
    /// 【克苏鲁触手钉】材质：仍在蠕动的血肉触手。签名：①「黏触」——命中给目标
    /// 挂 40 帧黏液沾附（Slimed，目标滴淌黏液；原版 Slow 对 NPC 无效，已查证）
    /// ②对血肉目标（会流血的非钢质）命中吮回 1 点生命，每拍最多一次
    /// ③命中黏液拉丝——紫染黏尘混血尘沿命中线拽出
    /// </summary>
    internal class GsTentacleSpike : GsBroadswordScheme
    {
        public override int TargetItemID => ItemID.TentacleSpike;

        protected override int HeldProjID => ModContent.ProjectileType<GsTentacleSpikeHeld>();

        protected override string GsDescFallback =>
            "Reforged: strikes smear clinging slime onto victims, " +
            "and sinking it into flesh drinks a sliver of life back";

        //肉紫色板
        internal static readonly Color FleshBright = new(234, 192, 216); //苍粉肉缘
        internal static readonly Color FleshMain = new(158, 90, 134);    //瘀肉紫
        internal static readonly Color FleshHot = new(218, 98, 152);     //渗血亮紫
        internal static readonly Color FleshDeep = new(36, 14, 30);      //腐暗紫黑

        //底伤 +6%：黏触标记纯演出向、吸血每拍至多 1 点已计入收益，
        //按 max(useTime, 弹幕总帧) 摊算综合 DPS 约为原版 108%~112%
        public override void GsModifyWeaponDamage(Item item, Player player, ref StatModifier damage)
            => damage *= 1.06f;
    }

    /// <summary>
    /// 克苏鲁触手钉手持：三拍黏重连击。0/1 交替抽打，2 重甩终结。
    /// 命中挂 Slimed 40 帧；血肉目标吮血 1 点（owner 端守门，每拍一次）。
    /// ai[0]=拍号 ai[1]=交替符号
    /// </summary>
    internal class GsTentacleSpikeHeld : GsBroadswordHeldBase
    {
        protected override int SwordItemID => ItemID.TentacleSpike;
        protected override Color EdgeBright => GsTentacleSpike.FleshBright;
        protected override Color BodyMain => GsTentacleSpike.FleshMain;
        protected override Color HotAccent => GsTentacleSpike.FleshHot;
        protected override Color DeepShadow => GsTentacleSpike.FleshDeep;

        /// <summary>本拍是否已吮过血（每拍最多回 1 点）</summary>
        private bool lifeDrunk;

        protected override GsBroadBeat GetBeat(int stage) {
            return stage switch {
                //黏抽一：湿重的低音起手
                0 => new GsBroadBeat {
                    Raise = 6, Hold = 2, Slash = 4, Recover = 9,
                    RaiseBack = 1.85f, Follow = 1f, ReachScale = 1f, LeanAmp = 0.045f,
                    DamageMult = 1f, Hitstop = 1, LungeSpeed = 0f, SwingPitch = -0.22f,
                },
                //黏抽二：回手更沉
                1 => new GsBroadBeat {
                    Raise = 5, Hold = 2, Slash = 4, Recover = 9,
                    RaiseBack = 1.7f, Follow = 1.05f, ReachScale = 1f, LeanAmp = 0.045f,
                    DamageMult = 1f, Hitstop = 1, LungeSpeed = 0f, SwingPitch = -0.3f,
                },
                //重甩终结：触手全幅抡出
                _ => new GsBroadBeat {
                    Raise = 7, Hold = 3, Slash = 5, Recover = 11,
                    RaiseBack = 2.1f, Follow = 1.2f, ReachScale = 1.1f, LeanAmp = 0.08f,
                    DamageMult = 1.3f, Hitstop = 2, LungeSpeed = 2.6f, SwingPitch = -0.4f,
                },
            };
        }

        //瘀肉质感：刀身吸暗
        protected override Color BodyTint(Color lightColor)
            => Color.Lerp(lightColor, GsTentacleSpike.FleshDeep, 0.18f);
        protected override Color GlowColor => GsTentacleSpike.FleshHot;

        /// <summary>黏触与吮血：Slimed 各端可见；回血只在 owner 端结算</summary>
        protected override void OnHitTarget(NPC target, NPC.HitInfo hit, int damageDone) {
            //黏液沾附 40 帧（drippingSlime，目标可见滴淌）
            target.AddBuff(BuffID.Slimed, 40);

            //血肉目标（会流血的非钢质）吮回 1 点，每拍最多一次
            if (!lifeDrunk && !CWRLoad.NPCValue.ISTheofSteel(target)
                && Owner.whoAmI == Main.myPlayer && Owner.statLife < Owner.statLifeMax2) {
                lifeDrunk = true;
                Owner.Heal(1);
            }
        }

        /// <summary>命中黏液拉丝：紫染黏尘沿命中→手的线拽出，混两粒血尘</summary>
        protected override void OnHitFX(NPC target, NPC.HitInfo hit, int damageDone) {
            base.OnHitFX(target, hit, damageDone);
            Vector2 toHand = (Hand - target.Center).SafeNormalize(Vector2.UnitX);
            Color goo = new(150, 60, 140);
            int strands = IsFinisher ? 7 : 5;
            for (int i = 0; i < strands; i++) {
                //沿拉丝方向带散布甩出，黏尘有重力，坠出弧线才有拉丝感
                Vector2 vel = toHand.RotatedByRandom(0.5) * Main.rand.NextFloat(1.5f, 5f);
                Dust d = Dust.NewDustPerfect(target.Center, DustID.t_Slime, vel, 120, goo,
                    Main.rand.NextFloat(0.9f, 1.5f));
                d.noGravity = false;
            }
            for (int i = 0; i < 2; i++) {
                Dust d = Dust.NewDustPerfect(target.Center, DustID.Blood,
                    toHand.RotatedByRandom(0.8) * Main.rand.NextFloat(1f, 3f), 100, default,
                    Main.rand.NextFloat(1f, 1.4f));
                d.noGravity = false;
            }
        }

        protected override void HandleParticles(int phase) {
            base.HandleParticles(phase);
            //挥动途中触手滴淌黏液
            if (phase is PhaseSlash or PhaseRecover && Main.rand.NextBool(4) && fanFade > 0.2f) {
                Vector2 at = Vector2.Lerp(Hand, mainTip, Main.rand.NextFloat(0.5f, 1f));
                Dust d = Dust.NewDustPerfect(at, DustID.t_Slime,
                    new Vector2(0f, Main.rand.NextFloat(0.5f, 1.5f)), 140, new Color(150, 60, 140),
                    Main.rand.NextFloat(0.8f, 1.2f));
                d.noGravity = false;
            }
        }
    }
}
