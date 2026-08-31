using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using CalamityOverhaul.Content.GameModes.GodSmith.Weapons.MagicChant.Projectiles;
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
    /// 气象痛重铸：风眼共鸣。龙卷命中处留 0.8s 风眼标记，正拍下一发朝标记
    /// 增压突进（初速 1.4 倍指向风眼，行至途中体量渐涨）；共鸣 3 层起双龙卷
    /// 对旋齐出（各 0.7 倍）；满层强化「风暴合唱」：召来驻场大风暴柱
    /// （0.5 倍每 0.4s 一跳，1.8s）。材质身份：风暴（灰绿云涡）
    /// </summary>
    internal class GsWeatherPain : GsChantScheme
    {
        public override int TargetItemID => ItemID.WeatherPain;

        protected override string GsDescFallback =>
            "Reforged: hits leave a storm eye, on-beat casts surge toward it and swell;" +
            "\nhigh resonance twins the whirlwinds, at full resonance the next cast summons a grand storm chorus";

        protected override float BaseDamageMult => 1.10f;

        protected override Color ChantColor => new(150, 205, 175);

        /// <summary>形态：对旋双龙卷（MarkData2 = 旋向 ±1）</summary>
        private const float FormDual = 10f;

        /// <summary>风眼增压标志（叠加在 MarkData2 层数之上）</summary>
        private const float SurgeFlag = 100f;

        private static readonly Color GaleMist = new(206, 232, 214);

        /// <summary>增压涨体状态（端本地，按本地飞行帧渐涨，判定在 owner 端）</summary>
        private class SurgeState
        {
            public int Frames;
        }

        protected override void ChantModifyShootStats(Item item, Player player, GsChantPlayer chant,
            ref Vector2 position, ref Vector2 velocity, ref int type, ref int damage, ref float knockback) {
            //风眼增压：正拍且风眼在期，初速转向风眼并提至 1.4 倍（初速随生成包过线）
            if (chant.CurrentBeat == ChantBeat.OnBeat
                && chant.AnchorUntil > Main.GameUpdateCount && chant.AnchorPos != Vector2.Zero) {
                velocity = (chant.AnchorPos - position).SafeNormalize(velocity.SafeNormalize(Vector2.UnitX))
                    * velocity.Length() * 1.4f;
            }
        }

        protected override bool? ChantShoot(Item item, Player player, GsChantPlayer chant,
            EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity,
            int type, int damage, float knockback) {
            //3 层起：双龙卷对旋（各 0.7 倍，错位齐出）
            if (chant.Resonance < 3) {
                return null;
            }
            int dualDamage = Math.Max(1, (int)(damage * 0.7f));
            Vector2 side = velocity.SafeNormalize(Vector2.UnitX).RotatedBy(MathHelper.PiOver2) * 14f;
            for (int i = 0; i < 2; i++) {
                float spin = i == 0 ? 1f : -1f;
                QueueForm(player, FormDual, spin);
                Projectile.NewProjectile(source, position + side * spin, velocity,
                    type, dualDamage, knockback, player.whoAmI);
            }
            return false;
        }

        protected override bool? ChantEmpowerShoot(Item item, Player player, GsChantPlayer chant,
            EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity,
            int type, int damage, float knockback) {
            //风暴合唱：准星处驻场大风暴柱（弹幕承载，全端可见）
            Vector2 anchor = Main.MouseWorld;
            float drift = Math.Sign(velocity.X);
            if (drift == 0f) {
                drift = 1f;
            }
            Projectile.NewProjectile(source, anchor, Vector2.Zero,
                ModContent.ProjectileType<GsChantStormChoirProj>(),
                Math.Max(1, (int)(damage * 0.5f)), knockback, player.whoAmI, drift);
            return false;
        }

        protected override void ChantProjOnSpawnMarked(Projectile proj, GodSmithProjRouter router, GsChantPlayer chant) {
            //增压弹打上涨体标（层数 +100 编码，随生成包过线）
            if (router.MarkData == FormOnBeat
                && chant.AnchorUntil > Main.GameUpdateCount && chant.AnchorPos != Vector2.Zero) {
                router.MarkData2 += SurgeFlag;
                proj.netUpdate = true;
            }
        }

        protected override void ChantHoldItem(Item item, Player player) {
            //风眼锚读数：锚点在期时打呼吸风环脉冲（锚是 owner 本地量，个人读数即可，
            //镜像族杖尖读数的 myPlayer 路径）
            if (player.whoAmI != Main.myPlayer) {
                return;
            }
            GsChantPlayer chant = Chant(player);
            if (chant.AnchorUntil <= Main.GameUpdateCount || chant.AnchorPos == Vector2.Zero) {
                return;
            }
            Lighting.AddLight(chant.AnchorPos, ChantColor.ToVector3() * 0.2f);
            if (Main.GameUpdateCount % 12 == 0) {
                PRTLoader.NewParticle<PRT_ProcRing>(chant.AnchorPos, Vector2.Zero, ChantColor, 1f)
                    ?.Configure(22f, 42f, 12);
            }
        }

        public override void GsProjPostAI(Projectile proj, GodSmithProjRouter router) {
            //增压弹涨体：飞行 22 帧后体量渐涨到 1.5 倍（各端按同一标推演）
            if (router.MarkData == FormOnBeat && router.MarkData2 >= SurgeFlag) {
                SurgeState state = router.GetOrCreateState<SurgeState>();
                state.Frames++;
                if (state.Frames > 22 && proj.scale < 1.5f) {
                    proj.scale += 0.02f;
                }
            }
            //对旋：垂直正弦分量叠加，双柱互为反相
            if (router.MarkData == FormDual) {
                float spin = Math.Sign(router.MarkData2);
                Vector2 side = proj.velocity.SafeNormalize(Vector2.UnitX).RotatedBy(MathHelper.PiOver2);
                proj.position += side * MathF.Sin(Main.GameUpdateCount * 0.22f + proj.identity * 0.7f) * 1.6f * spin;
            }

            if (VaultUtils.isServer) {
                return;
            }
            Lighting.AddLight(proj.Center, ChantColor.ToVector3() * 0.18f);
            //飞行相：云絮跟涡
            bool hot = router.MarkData is FormOnBeat or FormEmpower or FormDual;
            int interval = hot ? 5 : 8;
            if (proj.timeLeft % interval == 0) {
                PRTLoader.NewParticle<PRT_SvcCloud>(proj.Center + Main.rand.NextVector2Circular(6f, 6f),
                    -proj.velocity * 0.06f, ChantColor * 0.5f,
                    Main.rand.NextFloat(0.35f, 0.6f))?.Configure(Main.rand.Next(14, 22));
            }
        }

        public override void GsProjOnHitNPC(Projectile proj, NPC target, NPC.HitInfo hit, int damageDone, GodSmithProjRouter router) {
            if (!VaultUtils.isServer) {
                //命中相：风压拍散云屑
                for (int i = 0; i < 4; i++) {
                    PRTLoader.NewParticle<PRT_Spark>(target.Center,
                        Main.rand.NextVector2Circular(3f, 3f), i % 2 == 0 ? ChantColor : GaleMist,
                        Main.rand.NextFloat(0.22f, 0.38f))?.Configure(false, Main.rand.Next(8, 14));
                }
            }
            //风眼标记：命中处留 0.8s 风眼（owner 本地量，引导下一发）
            if (proj.owner == Main.myPlayer) {
                GsChantPlayer chant = Chant(Main.player[proj.owner]);
                chant.AnchorPos = target.Center;
                chant.AnchorUntil = Main.GameUpdateCount + 48;
            }
        }

        public override void GsProjOnKill(Projectile proj, int timeLeft, GodSmithProjRouter router) {
            //余痕相：残涡云絮缓散
            if (VaultUtils.isServer) {
                return;
            }
            for (int i = 0; i < 3; i++) {
                PRTLoader.NewParticle<PRT_SvcCloud>(proj.Center + Main.rand.NextVector2Circular(8f, 8f),
                    Main.rand.NextVector2Circular(1f, 1f) - Vector2.UnitY * 0.4f,
                    ChantColor * 0.45f, Main.rand.NextFloat(0.4f, 0.7f))?.Configure(Main.rand.Next(20, 32));
            }
        }
    }
}
