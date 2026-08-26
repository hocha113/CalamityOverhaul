using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.ChargeBows
{
    /// <summary>
    /// 肌腱弓：T2 血栓箭（命中挂放血失血），T3 血弦重击（箭体 ×1.4、击退 ×2、
    /// 命中迸血珠并小额吸血，吸血 30 帧内限一次、owner 本地结算）
    /// </summary>
    internal class GsTendonBow : GsChargeBowScheme
    {
        public override int TargetItemID => ItemID.TendonBow;
        protected override string GsDescFallback =>
            "Reforged: three-stage draw. Full-drawn arrows open bleeding wounds; an overdrawn heavy shot knocks foes flying and sips two life back";
        internal override float DpsTarget => 1.0f;
        internal override Color TrailMain => new(220, 70, 80);
        internal override Color TrailHot => new(255, 150, 150);
        internal override Color TrailDeep => new(110, 24, 34);

        /// <summary>吸血冷却记录；命中钩子只在攻击方端执行，本字段天然只属本机玩家</summary>
        private uint lastLifestealTick;

        internal override void ModifyArrowHit(Projectile proj, NPC target, ref NPC.HitModifiers modifiers, int tier, int kind) {
            if (tier >= 3 && kind == KindMain) {
                modifiers.Knockback *= 2f;
            }
        }

        internal override void ArrowPostAI(Projectile proj, GodSmithProjRouter router, int tier, int kind) {
            //血弦重击的体量：各端在 PostAI 统一涨
            if (tier >= 3 && kind == KindMain && proj.scale < 1.4f) {
                proj.scale = 1.4f;
            }
        }

        internal override void OnQualityHit(Projectile proj, NPC target, NPC.HitInfo hit, int damageDone, GodSmithProjRouter router, int tier) {
            if (ValidRiderTarget(target)) {
                //血栓：放血失血（T2 起）
                target.AddBuff(ModContent.BuffType<GsChargeBleedBuff>(), 360);
            }
            if (tier < 3) {
                return;
            }
            //血珠迸溅
            if (!VaultUtils.isServer) {
                Vector2 dir = proj.velocity.SafeNormalize(Vector2.UnitX);
                for (int i = 0; i < 5; i++) {
                    PRTLoader.NewParticle<PRT_HeartcarverDroplet>(target.Center,
                        (-dir).RotatedByRandom(1.1) * Main.rand.NextFloat(2f, 5.5f),
                        Main.rand.NextBool() ? TrailMain : TrailDeep, Main.rand.NextFloat(0.7f, 1.1f))
                        ?.Configure(Main.rand.Next(18, 30));
                }
            }
            //小额吸血：owner 本地结算，30 帧限一次，钳制防吸血流膨胀
            if (ValidRiderTarget(target) && Main.GameUpdateCount - lastLifestealTick >= 30) {
                lastLifestealTick = Main.GameUpdateCount;
                Player owner = Main.player[proj.owner];
                if (owner.statLife < owner.statLifeMax2) {
                    owner.statLife = Math.Min(owner.statLife + 2, owner.statLifeMax2);
                    owner.HealEffect(2);
                }
            }
        }
    }
}
