using System;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.MagicChant
{
    /// <summary>
    /// 灰色滋滋枪重铸：混沌节拍化。随机效果池原样保留（经典味不动）；
    /// 正拍光束必然滋出高压段（1.3 倍）；满层强化「超载滋滋」：单发 2.5 倍
    /// 超载光束，穿透 +2、体积膨大。免蓝语义原样保留。材质身份：混沌电（灰白）。<br/>
    /// 与设计的偏差：原版随机效果硬编码在弹幕 AI 内不可安全拦截，
    /// 「锁定重现」按计划兜底降级为「正拍必 roll 高伤段」，只动伤害乘区不碰效果池
    /// </summary>
    internal class GsGrayZapinator : GsChantScheme
    {
        public override int TargetItemID => ItemID.ZapinatorGray;

        protected override string GsDescFallback =>
            "Reforged: chaos stays chaos, but on-beat zaps always surge high voltage;\nat full resonance the next zap overloads into a swollen piercing beam";
        protected override float BaseDamageMult => 1.04f;

        //免蓝武器：法力经济不动
        protected override float OnBeatManaRefund => 0f;

        protected override void ChantModifyShootStats(Item item, Player player, GsChantPlayer chant,
            ref Vector2 position, ref Vector2 velocity, ref int type, ref int damage, ref float knockback) {
            //正拍必 roll 高压段：混沌之上叠一层确定性
            if (chant.CurrentBeat == ChantBeat.OnBeat) {
                damage = (int)(damage * 1.3f);
            }
        }

        protected override bool? ChantEmpowerShoot(Item item, Player player, GsChantPlayer chant,
            EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity,
            int type, int damage, float knockback) {
            //超载滋滋：单发膨大穿透光束（随机效果池由原版弹幕 AI 照常掷）
            int idx = Projectile.NewProjectile(source, position, velocity, type,
                Math.Max(1, (int)(damage * 2.5f)), knockback * 1.3f, player.whoAmI);
            if (idx >= 0 && idx < Main.maxProjectiles) {
                Projectile beam = Main.projectile[idx];
                beam.scale *= 1.4f;
                if (beam.penetrate > 0) {
                    beam.penetrate += 2;
                }
                beam.netUpdate = true;
            }
            return false;
        }
    }
}
