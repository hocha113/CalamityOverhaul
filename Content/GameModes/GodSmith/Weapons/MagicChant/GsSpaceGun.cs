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
    /// 太空枪重铸：充能节拍。共鸣层即弹计数：正拍 +1、失拍停滞不清；
    /// 每攒满 4 层自动清层，该发升格为「宽束脉冲」（1.8 倍、穿透 2、束宽加倍、
    /// 微微吸附小敌）。流星套 0 蓝语义原样保留，法力经济不动。材质身份：相干光（绿）
    /// </summary>
    internal class GsSpaceGun : GsChantScheme
    {
        public override int TargetItemID => ItemID.SpaceGun;

        protected override string GsDescFallback =>
            "Reforged: every on-beat shot charges the coil, the charge never decays on a miss;" +
            "\nevery fourth charge discharges as a wide piercing pulse that drags in lesser foes";

        protected override float BaseDamageMult => 1.06f;

        protected override int MaxResonance => 4;

        //充能语义：攒满即放、失拍停滞、免蓝语义不动
        protected override bool EmpowerTriggersInstantly => true;
        protected override bool DecayEnabled => false;
        protected override float OnBeatManaRefund => 0f;

        protected override Color ChantColor => new(110, 255, 150);

        private static readonly Color PhotonWhite = new(214, 255, 226);

        protected override bool? ChantEmpowerShoot(Item item, Player player, GsChantPlayer chant,
            EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity,
            int type, int damage, float knockback) {
            //宽束脉冲：单发放大增压（形态即强化标，档位随生成包过线）
            int idx = Projectile.NewProjectile(source, position, velocity * 1.1f, type,
                Math.Max(1, (int)(damage * 1.8f)), knockback * 1.4f, player.whoAmI);
            if (idx >= 0 && idx < Main.maxProjectiles) {
                Projectile pulse = Main.projectile[idx];
                pulse.scale *= 2f;
                if (pulse.penetrate > 0) {
                    pulse.penetrate += 2;
                }
                pulse.netUpdate = true;
            }
            return false;
        }

        public override void GsProjPostAI(Projectile proj, GodSmithProjRouter router) {
            bool pulse = router.MarkData == FormEmpower;
            //宽束脉冲微吸附：30px 级近旁小敌被拉向束心（NPC 位移走服务器权威端）
            if (pulse && !VaultUtils.isClient) {
                foreach (NPC npc in Main.ActiveNPCs) {
                    if (npc.boss || !npc.CanBeChasedBy() || npc.knockBackResist <= 0f) {
                        continue;
                    }
                    float dist = Vector2.Distance(npc.Center, proj.Center);
                    if (dist > 46f || dist < 4f) {
                        continue;
                    }
                    Vector2 pull = (proj.Center - npc.Center).SafeNormalize(Vector2.Zero) * 0.7f * npc.knockBackResist;
                    npc.velocity += pull;
                }
            }

            if (VaultUtils.isServer) {
                return;
            }
            Lighting.AddLight(proj.Center, ChantColor.ToVector3() * (pulse ? 0.4f : 0.2f));
            //飞行相：相干光的绿曳光斑
            int interval = pulse ? 2 : 5;
            if (proj.timeLeft % interval == 0) {
                PRTLoader.NewParticle<PRT_SparkAlpha>(proj.Center + Main.rand.NextVector2Circular(3f, 3f),
                    -proj.velocity * 0.05f, ChantColor,
                    Main.rand.NextFloat(0.25f, 0.45f) * (pulse ? 1.6f : 1f));
            }
        }

        public override bool? GsProjPreDraw(Projectile proj, ref Color lightColor, GodSmithProjRouter router) {
            //宽束脉冲：叠一层速度拉伸的加色重影，束读作被充能撑宽（identity 定相）
            if (router.MarkData != FormEmpower) {
                return null;
            }
            Main.instance.LoadProjectile(proj.type);
            var tex = Terraria.GameContent.TextureAssets.Projectile[proj.type].Value;
            float pulseWave = 0.7f + 0.3f * MathF.Sin(Main.GlobalTimeWrappedHourly * 11f + proj.identity * 0.53f);
            Color glow = PhotonWhite * (0.5f * pulseWave);
            glow.A = 0;
            Main.EntitySpriteDraw(tex, proj.Center - Main.screenPosition, null, glow,
                proj.rotation, tex.Size() / 2f, new Vector2(2.6f, 1.5f) * proj.scale * 0.6f,
                Microsoft.Xna.Framework.Graphics.SpriteEffects.None, 0);
            return null;
        }

        public override void GsProjOnHitNPC(Projectile proj, NPC target, NPC.HitInfo hit, int damageDone, GodSmithProjRouter router) {
            //命中相：光爆
            if (VaultUtils.isServer) {
                return;
            }
            int count = router.MarkData == FormEmpower ? 6 : 3;
            for (int i = 0; i < count; i++) {
                PRTLoader.NewParticle<PRT_SparkAlpha>(target.Center + Main.rand.NextVector2Circular(6f, 6f),
                    Main.rand.NextVector2Circular(2.5f, 2.5f),
                    i % 2 == 0 ? ChantColor : PhotonWhite, Main.rand.NextFloat(0.3f, 0.5f));
            }
            PRTLoader.NewParticle<PRT_Light>(target.Center, Vector2.Zero, ChantColor, 0.12f)?.Configure(8, 0.7f);
        }

        public override void GsProjOnKill(Projectile proj, int timeLeft, GodSmithProjRouter router) {
            //余痕相：光子衰散
            if (VaultUtils.isServer) {
                return;
            }
            int count = router.MarkData == FormEmpower ? 4 : 2;
            for (int i = 0; i < count; i++) {
                PRTLoader.NewParticle<PRT_SparkAlpha>(proj.Center + Main.rand.NextVector2Circular(4f, 4f),
                    Main.rand.NextVector2Circular(1f, 1f), ChantColor * 0.8f,
                    Main.rand.NextFloat(0.22f, 0.38f));
            }
        }
    }
}
