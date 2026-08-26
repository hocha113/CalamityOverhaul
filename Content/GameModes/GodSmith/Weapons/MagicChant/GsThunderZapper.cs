using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.MagicChant
{
    /// <summary>
    /// 雷矢重铸：扳机节奏的电弧学。正拍雷弹走三折闪电弹道，折点有几率分叉小电弧；
    /// 共鸣层提供每层 3% 射速；满层强化「落雷印」：该发命中处挂 0.4s 雷印，
    /// 随后天降雷柱（1.8 倍）。材质身份：电弧
    /// </summary>
    internal class GsThunderZapper : GsChantScheme
    {
        public override int TargetItemID => ItemID.ThunderStaff;

        protected override string GsDescFallback =>
            "Reforged: on-beat bolts zigzag like true lightning and may fork at each bend, resonance quickens the trigger;" +
            "\nat full resonance the next hit brands the target and calls down a thunder pillar";

        protected override float BaseDamageMult => 1.10f;

        protected override Color ChantColor => new(150, 190, 255);

        /// <summary>形态：折点分叉的小电弧</summary>
        private const float FormArc = 10f;
        /// <summary>形态：落雷印（隐形倒计时载体）</summary>
        private const float FormMark = 11f;
        /// <summary>形态：天降雷柱</summary>
        private const float FormPillar = 12f;

        private static readonly Color VoltWhite = new(226, 240, 255);

        /// <summary>正拍折线状态（端本地：折向由 identity 哈希决定，各端一致）</summary>
        private class ZapState
        {
            public int Frames;
            public int Bends;
        }

        public override float GsUseSpeedMultiplier(Item item, Player player) {
            if (player.whoAmI != Main.myPlayer) {
                return 1f;
            }
            GsChantPlayer chant = Chant(player);
            return chant.BoundItemType == item.type ? 1f + 0.03f * chant.Resonance : 1f;
        }

        protected override void ChantProjOnSpawnMarked(Projectile proj, GodSmithProjRouter router, GsChantPlayer chant) {
            if (router.MarkData == FormArc) {
                proj.timeLeft = Math.Min(proj.timeLeft, 22);
                proj.scale *= 0.7f;
            }
            else if (router.MarkData == FormMark) {
                //雷印：隐形倒计时载体，不判定不移动
                proj.timeLeft = 24;
                proj.friendly = false;
            }
            else if (router.MarkData == FormPillar) {
                proj.scale *= 1.6f;
                if (proj.penetrate > 0) {
                    proj.penetrate += 2;
                }
            }
        }

        public override bool GsProjPreAI(Projectile proj, GodSmithProjRouter router) {
            //雷印全接管：定身倒数，各端一致静止
            if (router.MarkData == FormMark) {
                proj.velocity = Vector2.Zero;
                proj.alpha = 255;
                return false;
            }
            return true;
        }

        public override void GsProjPostAI(Projectile proj, GodSmithProjRouter router) {
            //正拍弹三折闪电：每 7 帧一折共 3 折，折向按 identity 哈希各端一致
            if (router.MarkData == FormOnBeat || router.MarkData == FormEmpower) {
                ZapState state = router.GetOrCreateState<ZapState>();
                state.Frames++;
                if (state.Bends < 3 && state.Frames % 7 == 0) {
                    state.Bends++;
                    int side = (int)((proj.identity * 2654435761u >> state.Bends) & 1) * 2 - 1;
                    float bend = MathHelper.ToRadians(24f) * side;
                    proj.velocity = proj.velocity.RotatedBy(bend);
                    //折点分叉：25% 掷一枚半伤小电弧（owner 端裁决，弹幕过线全端可见）
                    if (proj.IsOwnedByLocalPlayer() && Main.rand.NextBool(4)) {
                        Vector2 vel = proj.velocity.RotatedBy(-bend * 2.2f) * 0.8f;
                        QueueForm(Main.player[proj.owner], FormArc);
                        Projectile.NewProjectile(proj.GetSource_FromThis(), proj.Center, vel,
                            proj.type, Math.Max(1, (int)(proj.damage * 0.5f)), proj.knockBack * 0.4f, proj.owner);
                    }
                    if (!VaultUtils.isServer) {
                        PRTLoader.NewParticle<PRT_GraniteVolt>(proj.Center, Vector2.Zero,
                            VoltWhite, Main.rand.NextFloat(0.5f, 0.8f));
                    }
                }
            }

            if (VaultUtils.isServer || router.MarkData == FormMark) {
                //雷印读数走 OnKill 前的静默：不画飞行相
                return;
            }
            //飞行相：稀疏电花 + 电蓝光
            Lighting.AddLight(proj.Center, ChantColor.ToVector3() * 0.3f);
            if (proj.timeLeft % 4 == 0) {
                PRTLoader.NewParticle<PRT_Spark>(proj.Center + Main.rand.NextVector2Circular(3f, 3f),
                    -proj.velocity * 0.06f + Main.rand.NextVector2Circular(0.6f, 0.6f),
                    Main.rand.NextBool(3) ? VoltWhite : ChantColor,
                    Main.rand.NextFloat(0.2f, 0.34f))?.Configure(false, Main.rand.Next(6, 12));
            }
        }

        public override void GsProjOnHitNPC(Projectile proj, NPC target, NPC.HitInfo hit, int damageDone, GodSmithProjRouter router) {
            if (!VaultUtils.isServer) {
                //命中相：电弧迸裂
                for (int i = 0; i < 5; i++) {
                    PRTLoader.NewParticle<PRT_GraniteVolt>(target.Center + Main.rand.NextVector2Circular(6f, 6f),
                        Main.rand.NextVector2Circular(2f, 2f), ChantColor, Main.rand.NextFloat(0.4f, 0.7f));
                }
            }
            //落雷印：强化弹命中处挂 0.4s 雷印，雷柱伤害此刻烘焙进 MarkData2（防换武器后错算）
            if (proj.IsOwnedByLocalPlayer() && router.MarkData == FormEmpower) {
                QueueForm(Main.player[proj.owner], FormMark, proj.damage * 1.8f);
                Projectile.NewProjectile(proj.GetSource_FromThis(), target.Center, Vector2.Zero,
                    proj.type, 1, 0f, proj.owner);
            }
        }

        public override void GsProjOnKill(Projectile proj, int timeLeft, GodSmithProjRouter router) {
            if (router.MarkData == FormMark) {
                //雷印倒数完毕：owner 生成雷柱弹（判定），各端自绘天雷视觉
                Vector2 strikeFrom = proj.Center - Vector2.UnitY * 320f;
                if (proj.IsOwnedByLocalPlayer()) {
                    QueueForm(Main.player[proj.owner], FormPillar);
                    Projectile.NewProjectile(proj.GetSource_FromThis(), strikeFrom, Vector2.UnitY * 26f,
                        proj.type, Math.Max(1, (int)router.MarkData2), 4f, proj.owner);
                }
                if (!VaultUtils.isServer) {
                    PRTLoader.NewParticle<PRT_SkyBolt>(proj.Center, Vector2.Zero, ChantColor, 1f)
                        ?.Configure(strikeFrom, proj.Center, 24);
                }
                return;
            }
            //余痕相：弹亡处残电一两闪
            if (VaultUtils.isServer) {
                return;
            }
            for (int i = 0; i < 2; i++) {
                PRTLoader.NewParticle<PRT_GraniteVolt>(proj.Center + Main.rand.NextVector2Circular(4f, 4f),
                    Vector2.Zero, i == 0 ? VoltWhite : ChantColor, Main.rand.NextFloat(0.35f, 0.55f));
            }
        }
    }
}
