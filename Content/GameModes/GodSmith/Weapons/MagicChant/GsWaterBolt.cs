using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using System;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.MagicChant
{
    /// <summary>
    /// 水矢重铸：回声咏唱。正拍水弹反弹上限每层 +1，且每次反弹在墙面荡开一圈
    /// 水环涟漪（0.3 倍判一次）；满层强化「大潮」：巨水矢 2.2 倍贯穿。材质身份：流体
    /// </summary>
    internal class GsWaterBolt : GsChantScheme
    {
        public override int TargetItemID => ItemID.WaterBolt;

        protected override string GsDescFallback =>
            "Reforged: on-beat bolts bounce further and ripple on every ricochet;\nat full resonance the next cast surges into a great tide that pierces through everything";
        protected override float BaseDamageMult => 1.08f;

        /// <summary>形态：反弹涟漪</summary>
        private const float FormRipple = 10f;
        /// <summary>形态：大潮巨水矢</summary>
        private const float FormTide = 11f;

        /// <summary>反弹检测状态（端本地，只做表现与 owner 端生成裁决）</summary>
        private class BounceState
        {
            public Vector2 PrevVel;
            public bool Primed;
        }

        protected override bool? ChantEmpowerShoot(Item item, Player player, GsChantPlayer chant,
            EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity,
            int type, int damage, float knockback) {
            //大潮：单发巨水矢，慢而重，穿透大幅抬高
            QueueForm(player, FormTide);
            int idx = Projectile.NewProjectile(source, position, velocity * 0.8f, type,
                Math.Max(1, (int)(damage * 2.2f)), knockback * 1.6f, player.whoAmI);
            if (idx >= 0 && idx < Main.maxProjectiles) {
                Projectile tide = Main.projectile[idx];
                tide.scale *= 1.9f;
                if (tide.penetrate > 0) {
                    tide.penetrate += 5;
                }
                tide.netUpdate = true;
            }
            return false;
        }

        protected override void ChantProjOnSpawnMarked(Projectile proj, GodSmithProjRouter router, GsChantPlayer chant) {
            //回声咏唱：正拍弹反弹上限每层 +1（水矢的反弹次数走 penetrate 计数）
            if (router.MarkData is FormOnBeat or FormEmpower && proj.penetrate > 0) {
                proj.penetrate += (int)router.MarkData2;
            }
        }

        public override void GsProjPostAI(Projectile proj, GodSmithProjRouter router) {
            //反弹检测：速度分量翻转即撞墙（水矢无重力匀速反弹）
            if (router.MarkData is FormOnBeat or FormEmpower or FormTide) {
                BounceState state = router.GetOrCreateState<BounceState>();
                if (state.Primed) {
                    bool bounced = Math.Sign(proj.velocity.X) != Math.Sign(state.PrevVel.X) && state.PrevVel.X != 0f;
                    bounced |= Math.Sign(proj.velocity.Y) != Math.Sign(state.PrevVel.Y) && state.PrevVel.Y != 0f;
                    if (bounced) {
                        OnBoltBounce(proj, router);
                    }
                }
                state.PrevVel = proj.velocity;
                state.Primed = true;
            }
        }

        /// <summary>反弹瞬间：owner 端生成涟漪判定</summary>
        private void OnBoltBounce(Projectile proj, GodSmithProjRouter router) {
            if (!proj.IsOwnedByLocalPlayer() || router.MarkData == FormRipple) {
                return;
            }
            QueueForm(Main.player[proj.owner], FormRipple);
            int idx = Projectile.NewProjectile(proj.GetSource_FromThis(), proj.Center, Vector2.Zero,
                proj.type, Math.Max(1, (int)(proj.damage * 0.3f)), 0f, proj.owner);
            if (idx >= 0 && idx < Main.maxProjectiles) {
                Projectile ripple = Main.projectile[idx];
                ripple.timeLeft = 12;
                ripple.Resize(80, 80);
                ripple.netUpdate = true;
            }
        }

        public override bool GsProjPreAI(Projectile proj, GodSmithProjRouter router) {
            //涟漪：定身一跳，原版反弹 AI 压掉（本体沿用原版水矢贴图默认绘制）
            if (router.MarkData == FormRipple) {
                proj.velocity = Vector2.Zero;
                return false;
            }
            return true;
        }
    }
}
