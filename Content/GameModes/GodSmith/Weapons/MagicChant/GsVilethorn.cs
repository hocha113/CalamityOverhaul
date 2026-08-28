using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using System;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.MagicChant
{
    /// <summary>
    /// 魔刺重铸：蔓生节拍。正拍荆棘任意段命中时，命中点分叉 V 形二次短刺
    /// （夹角 56 度，各 0.5 倍）；满层强化「棘环」：主刺照常刺出，
    /// 同时以玩家为心八向绽放短棘（各 0.6 倍）。材质身份：腐化荆棘。<br/>
    /// 与设计的偏差：原版延展长度由 aiStyle 递归内部计数控制，跨端干预不可靠，
    /// 「刺长 +12%/层」降级为 V 形分叉的覆盖面增益（计划已列此兜底）
    /// </summary>
    internal class GsVilethorn : GsChantScheme
    {
        public override int TargetItemID => ItemID.Vilethorn;

        protected override string GsDescFallback =>
            "Reforged: on-beat thorns fork into vile spikes wherever they bite;" +
            "\nat full resonance the next cast also erupts a ring of thorns around you";

        protected override float BaseDamageMult => 1.10f;

        protected override Color ChantColor => new(150, 96, 220);

        /// <summary>形态：V 形分叉短刺 / 棘环短棘</summary>
        private const float FormVSpike = 10f;

        private static readonly Color VileGreen = new(126, 190, 88);

        protected override bool? ChantEmpowerShoot(Item item, Player player, GsChantPlayer chant,
            EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity,
            int type, int damage, float knockback) {
            //棘环：八向短棘各 0.6 倍；返回 null 让原版主刺照常刺出（主刺带强化标）
            int ringDamage = Math.Max(1, (int)(damage * 0.6f));
            for (int i = 0; i < 8; i++) {
                float ang = MathHelper.TwoPi * i / 8f;
                Vector2 dir = ang.ToRotationVector2();
                QueueForm(player, FormVSpike);
                Projectile.NewProjectile(source, player.MountedCenter + dir * 34f, dir * 0.5f,
                    ProjectileID.VilethornTip, ringDamage, knockback * 0.5f, player.whoAmI);
            }
            return null;
        }

        public override void GsProjPostAI(Projectile proj, GodSmithProjRouter router) {
            if (VaultUtils.isServer) {
                return;
            }
            //飞行相：腐化荆棘的酸绿微光，本体延展动画由原版负责
            Lighting.AddLight(proj.Center, VileGreen.ToVector3() * 0.16f);
            if (proj.timeLeft % 9 == 0 && Main.rand.NextBool(2)) {
                PRTLoader.NewParticle<PRT_ToxicMist>(proj.Center + Main.rand.NextVector2Circular(6f, 6f),
                    Main.rand.NextVector2Circular(0.5f, 0.5f), VileGreen * 0.5f,
                    Main.rand.NextFloat(0.3f, 0.5f))?.Configure(Main.rand.Next(12, 20));
            }
        }

        public override void GsProjOnHitNPC(Projectile proj, NPC target, NPC.HitInfo hit, int damageDone, GodSmithProjRouter router) {
            if (!VaultUtils.isServer) {
                //命中相：孢尘一撮
                for (int i = 0; i < 3; i++) {
                    PRTLoader.NewParticle<PRT_ToxicMist>(target.Center + Main.rand.NextVector2Circular(6f, 6f),
                        Main.rand.NextVector2Circular(1.2f, 1.2f), VileGreen * 0.55f,
                        Main.rand.NextFloat(0.35f, 0.55f))?.Configure(Main.rand.Next(10, 16));
                }
            }
            //蔓生节拍：正拍荆棘（任意延展段承签同标）命中点分叉 V 形二次刺
            if (!proj.IsOwnedByLocalPlayer() || router.MarkData is not (FormOnBeat or FormEmpower)) {
                return;
            }
            int spikeDamage = Math.Max(1, (int)(proj.damage * 0.5f));
            Vector2 dir = proj.velocity.SafeNormalize(Vector2.UnitX);
            for (int i = 0; i < 2; i++) {
                Vector2 vDir = dir.RotatedBy(i == 0 ? MathHelper.ToRadians(28f) : MathHelper.ToRadians(-28f));
                QueueForm(Main.player[proj.owner], FormVSpike);
                Projectile.NewProjectile(proj.GetSource_FromThis(), target.Center, vDir * 0.5f,
                    ProjectileID.VilethornTip, spikeDamage, proj.knockBack * 0.4f, proj.owner);
            }
        }

        public override void GsProjOnKill(Projectile proj, int timeLeft, GodSmithProjRouter router) {
            //余痕相：刺尖枯散的孢尘比刺活得久
            if (VaultUtils.isServer || !Main.rand.NextBool(2)) {
                return;
            }
            PRTLoader.NewParticle<PRT_ToxicMist>(proj.Center, -Vector2.UnitY * 0.4f,
                VileGreen * 0.45f, Main.rand.NextFloat(0.35f, 0.55f))?.Configure(Main.rand.Next(16, 26));
        }
    }
}
