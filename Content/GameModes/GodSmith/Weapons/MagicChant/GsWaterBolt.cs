using CalamityOverhaul.Common;
using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using System;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.MagicChant
{
    /// <summary>
    /// 水矢重铸：回声咏唱。正拍水弹反弹上限每层 +1，且每次反弹在墙面荡开一圈
    /// 水环涟漪（0.3 倍判一次）；满层强化「大潮」：巨水矢 2.2 倍贯穿，
    /// 途经处留下泡沫水雾（纯表现）。材质身份：流体
    /// </summary>
    internal class GsWaterBolt : GsChantScheme
    {
        public override int TargetItemID => ItemID.WaterBolt;

        protected override string GsDescFallback =>
            "Reforged: on-beat bolts bounce further and ripple on every ricochet;" +
            "\nat full resonance the next cast surges into a great tide that pierces through everything";

        protected override float BaseDamageMult => 1.08f;

        protected override Color ChantColor => new(96, 176, 255);

        /// <summary>形态：反弹涟漪</summary>
        private const float FormRipple = 10f;
        /// <summary>形态：大潮巨水矢</summary>
        private const float FormTide = 11f;

        private static readonly Color FoamWhite = new(212, 238, 255);

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

            if (VaultUtils.isServer) {
                return;
            }
            Lighting.AddLight(proj.Center, ChantColor.ToVector3() * 0.22f);
            //飞行相：流体身份是拖尾水泡，大潮更密并甩泡沫
            bool tide = router.MarkData == FormTide;
            int interval = tide ? 2 : 6;
            if (proj.timeLeft % interval == 0) {
                PRTLoader.NewParticle<PRT_CampfireBubble>(
                    proj.Center + Main.rand.NextVector2Circular(4f, 4f) * (tide ? 2.2f : 1f),
                    -proj.velocity * 0.1f - Vector2.UnitY * 0.4f,
                    ChantColor * 0.7f, Main.rand.NextFloat(0.3f, 0.55f) * (tide ? 1.4f : 1f));
            }
        }

        /// <summary>反弹瞬间：各端荡水花，owner 端生成涟漪判定</summary>
        private void OnBoltBounce(Projectile proj, GodSmithProjRouter router) {
            if (!VaultUtils.isServer) {
                for (int i = 0; i < 4; i++) {
                    PRTLoader.NewParticle<PRT_Spark>(proj.Center,
                        Main.rand.NextVector2Circular(2.5f, 2.5f) - Vector2.UnitY,
                        FoamWhite, Main.rand.NextFloat(0.2f, 0.35f))?.Configure(true, Main.rand.Next(10, 16));
                }
            }
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
            //涟漪：定身一跳，原版反弹 AI 压掉
            if (router.MarkData == FormRipple) {
                proj.velocity = Vector2.Zero;
                proj.alpha = 255;
                return false;
            }
            return true;
        }

        public override bool? GsProjPreDraw(Projectile proj, ref Color lightColor, GodSmithProjRouter router) {
            //涟漪自绘：一圈扩张水环替代弹体
            if (router.MarkData != FormRipple) {
                return null;
            }
            float t = 1f - proj.timeLeft / 12f;
            ShockRingDraw.Draw(Main.spriteBatch, proj.Center, 12f + 30f * t, 6f,
                FoamWhite, ChantColor, new Color(30, 70, 140), 0.8f * (1f - t * t),
                squish: 1f, innerGlow: 0.2f, timeSeed: proj.identity * 0.37f);
            return false;
        }

        public override void GsProjOnHitNPC(Projectile proj, NPC target, NPC.HitInfo hit, int damageDone, GodSmithProjRouter router) {
            //命中相：水花迸溅
            if (VaultUtils.isServer) {
                return;
            }
            Vector2 dir = proj.velocity.SafeNormalize(Vector2.UnitX);
            for (int i = 0; i < 5; i++) {
                PRTLoader.NewParticle<PRT_Spark>(target.Center,
                    (-dir).RotatedByRandom(0.9) * Main.rand.NextFloat(1.5f, 4f),
                    i % 2 == 0 ? ChantColor : FoamWhite,
                    Main.rand.NextFloat(0.22f, 0.4f))?.Configure(true, Main.rand.Next(10, 18));
            }
        }

        public override void GsProjOnKill(Projectile proj, int timeLeft, GodSmithProjRouter router) {
            //余痕相：弹亡处泡沫上浮，比弹体活得久
            if (VaultUtils.isServer || router.MarkData == FormRipple) {
                return;
            }
            int count = router.MarkData == FormTide ? 5 : 3;
            for (int i = 0; i < count; i++) {
                PRTLoader.NewParticle<PRT_CampfireBubble>(proj.Center + Main.rand.NextVector2Circular(6f, 6f),
                    -Vector2.UnitY * Main.rand.NextFloat(0.5f, 1.2f),
                    ChantColor * 0.6f, Main.rand.NextFloat(0.3f, 0.5f));
            }
        }
    }
}
