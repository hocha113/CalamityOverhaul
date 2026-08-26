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
    /// 恶魔镰刀重铸：收割节拍。正拍镰刀滞空缩短约四成（更快出手），失拍反而
    /// 拖长三成；共鸣 3 层起镰刀有概率在飞远后回旋一程（回程 0.6 倍）；
    /// 满层强化「死神十字」：横竖双镰交叉钉向准星（各 0.9 倍）。材质身份：暗焰。<br/>
    /// 滞空干预走 vanilla ai[0] 计数的增速/减速（各端按同步的 MarkData 一致推演）
    /// </summary>
    internal class GsDemonScythe : GsChantScheme
    {
        public override int TargetItemID => ItemID.DemonScythe;

        protected override string GsDescFallback =>
            "Reforged: on-beat scythes wind up faster and may boomerang back at high resonance;" +
            "\nat full resonance the next cast crosses two scythes over your cursor";

        //原版已强，定价 110%
        protected override float BaseDamageMult => 1.04f;

        //滞空节拍原版已慢，正拍返蓝按计划压到 25%
        protected override float OnBeatManaRefund => 0.25f;

        protected override Color ChantColor => new(196, 96, 235);

        /// <summary>形态：死神十字镰（MarkData2 = 0 横 / 1 竖）</summary>
        private const float FormCross = 10f;

        /// <summary>MarkData2 回旋标志位（层数 + 100）</summary>
        private const float ReturnFlag = 100f;

        private static readonly Color DemonDeep = new(120, 40, 160);

        /// <summary>回旋状态（端本地：回旋触发按本地飞行帧计，伤害只认 owner 端）</summary>
        private class ScytheState
        {
            public int Frames;
            public bool Returning;
        }

        protected override bool? ChantEmpowerShoot(Item item, Player player, GsChantPlayer chant,
            EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity,
            int type, int damage, float knockback) {
            //死神十字：横镰自侧方平扫、竖镰自上方直落，交叉于准星
            Vector2 aim = Main.MouseWorld;
            int crossDamage = Math.Max(1, (int)(damage * 0.9f));
            int side = Math.Sign(aim.X - player.Center.X);
            if (side == 0) {
                side = 1;
            }
            for (int i = 0; i < 2; i++) {
                bool vertical = i == 1;
                Vector2 from = vertical ? aim - Vector2.UnitY * 260f : aim - new Vector2(side * 260f, 0f);
                Vector2 vel = (aim - from).SafeNormalize(Vector2.UnitX) * 11f;
                QueueForm(player, FormCross, vertical ? 1f : 0f);
                Projectile.NewProjectile(source, from, vel, type, crossDamage, knockback, player.whoAmI);
            }
            return false;
        }

        protected override void ChantProjOnSpawnMarked(Projectile proj, GodSmithProjRouter router, GsChantPlayer chant) {
            //回旋裁决：3 层起 owner 掷一次（每高一层 +25%），结果编码进 MarkData2 过线
            if (router.MarkData is FormOnBeat or FormEmpower && router.MarkData2 >= 3f) {
                float chance = 0.25f * (router.MarkData2 - 2f);
                if (Main.rand.NextFloat() < chance) {
                    router.MarkData2 += ReturnFlag;
                    proj.netUpdate = true;
                }
            }
        }

        public override bool GsProjPreAI(Projectile proj, GodSmithProjRouter router) {
            //十字镰全接管：直线钉飞不滞空，自转读作锯刃
            if (router.MarkData == FormCross) {
                proj.rotation += 0.38f;
                proj.alpha = Math.Max(0, proj.alpha - 40);
                return false;
            }
            return true;
        }

        public override void GsProjPostAI(Projectile proj, GodSmithProjRouter router) {
            float mark = router.MarkData;
            //收割节拍：正拍加速滞空计数（约 -40%），平拍拖慢（约 +30%）
            if (mark is FormOnBeat or FormEmpower && proj.ai[0] < 100f) {
                proj.ai[0] += 0.67f;
            }
            else if (mark == FormStraight && proj.ai[0] > 1f) {
                proj.ai[0] -= 0.23f;
            }
            //回旋：飞行 52 帧后掉头扑向持杖人，一去一回两段收割
            if (mark is FormOnBeat or FormEmpower && router.MarkData2 >= ReturnFlag) {
                ScytheState state = router.GetOrCreateState<ScytheState>();
                state.Frames++;
                if (!state.Returning && state.Frames > 52) {
                    state.Returning = true;
                    Player owner = Main.player[proj.owner];
                    proj.velocity = (owner.Center - proj.Center).SafeNormalize(Vector2.UnitX)
                        * Math.Max(6f, proj.velocity.Length() * 0.6f);
                    if (!VaultUtils.isServer) {
                        for (int i = 0; i < 4; i++) {
                            PRTLoader.NewParticle<PRT_HellFlame>(proj.Center,
                                Main.rand.NextVector2Circular(1.5f, 1.5f), DemonDeep,
                                Main.rand.NextFloat(0.4f, 0.6f));
                        }
                    }
                }
            }

            if (VaultUtils.isServer) {
                return;
            }
            Lighting.AddLight(proj.Center, ChantColor.ToVector3() * 0.22f);
            //飞行相：暗焰曳尾
            int interval = mark is FormOnBeat or FormEmpower or FormCross ? 4 : 6;
            if (proj.timeLeft % interval == 0) {
                PRTLoader.NewParticle<PRT_HellFlame>(proj.Center + Main.rand.NextVector2Circular(5f, 5f),
                    -proj.velocity * 0.1f, ChantColor, Main.rand.NextFloat(0.35f, 0.6f));
            }
        }

        public override void GsProjModifyHitNPC(Projectile proj, NPC target, ref NPC.HitModifiers modifiers, GodSmithProjRouter router) {
            //回程收割衰减：伤害裁决在 owner 端，本地状态即权威
            if (router.MarkData2 >= ReturnFlag
                && router.LocalState is ScytheState { Returning: true }) {
                modifiers.FinalDamage *= 0.6f;
            }
        }

        public override void GsProjOnHitNPC(Projectile proj, NPC target, NPC.HitInfo hit, int damageDone, GodSmithProjRouter router) {
            //命中相：暗焰爆闪
            if (VaultUtils.isServer) {
                return;
            }
            for (int i = 0; i < 5; i++) {
                PRTLoader.NewParticle<PRT_HellFlame>(target.Center + Main.rand.NextVector2Circular(8f, 8f),
                    Main.rand.NextVector2Circular(2.5f, 2.5f),
                    i % 2 == 0 ? ChantColor : DemonDeep, Main.rand.NextFloat(0.4f, 0.65f));
            }
            PRTLoader.NewParticle<PRT_Light>(target.Center, Vector2.Zero, ChantColor, 0.13f)?.Configure(8, 0.7f);
        }

        public override void GsProjOnKill(Projectile proj, int timeLeft, GodSmithProjRouter router) {
            //余痕相：焰屑散落
            if (VaultUtils.isServer) {
                return;
            }
            for (int i = 0; i < 3; i++) {
                PRTLoader.NewParticle<PRT_Spark>(proj.Center + Main.rand.NextVector2Circular(5f, 5f),
                    Main.rand.NextVector2Circular(1.2f, 1.2f) - Vector2.UnitY * 0.5f,
                    DemonDeep, Main.rand.NextFloat(0.22f, 0.38f))?.Configure(true, Main.rand.Next(14, 24));
            }
        }
    }
}
