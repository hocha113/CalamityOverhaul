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
    /// 火花魔杖重铸：引燃链跳。正拍火花命中后向 90px 内最近敌跳段，
    /// 跳段上限 = 施法瞬间共鸣层数，跳伤 0.5 倍；满层强化「星火燎原」：
    /// 扇形 40 度喷 12 枚火星雨（各 0.6 倍）后清层。材质身份：燃焰
    /// </summary>
    internal class GsWandOfSparking : GsChantScheme
    {
        public override int TargetItemID => ItemID.WandofSparking;

        protected override string GsDescFallback =>
            "Reforged: casting on the beat builds resonance, on-beat sparks chain-ignite nearby foes;" +
            "\nat full resonance the next cast erupts into a fan of twelve stray sparks";

        //公认弱势武器，定价 135%
        protected override float BaseDamageMult => 1.15f;

        protected override Color ChantColor => new(255, 150, 60);

        /// <summary>形态：链跳火花，MarkData2 = 剩余跳段数</summary>
        private const float FormChain = 10f;
        /// <summary>形态：星火燎原的火星雨</summary>
        private const float FormRain = 11f;

        private static readonly Color EmberDeep = new(210, 84, 30);

        protected override bool? ChantEmpowerShoot(Item item, Player player, GsChantPlayer chant,
            EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity,
            int type, int damage, float knockback) {
            //星火燎原：40 度扇 12 枚火星雨，各 0.6 倍
            int rainDamage = Math.Max(1, (int)(damage * 0.6f));
            for (int i = 0; i < 12; i++) {
                float off = MathHelper.ToRadians(-20f + 40f * i / 11f);
                Vector2 vel = velocity.RotatedBy(off) * Main.rand.NextFloat(0.85f, 1.2f);
                QueueForm(player, FormRain);
                Projectile.NewProjectile(source, position, vel, type, rainDamage, knockback * 0.5f, player.whoAmI);
            }
            return false;
        }

        protected override void ChantProjOnSpawnMarked(Projectile proj, GodSmithProjRouter router, GsChantPlayer chant) {
            if (router.MarkData == FormRain) {
                //火星雨：短寿散射，出手即带散布
                proj.timeLeft = Math.Min(proj.timeLeft, 42);
                proj.scale *= 0.8f;
            }
        }

        public override void GsProjPostAI(Projectile proj, GodSmithProjRouter router) {
            if (VaultUtils.isServer) {
                return;
            }
            //飞行相：燃焰拖尾，正拍与链弹更密
            bool hot = router.MarkData is FormOnBeat or FormEmpower or FormChain;
            Lighting.AddLight(proj.Center, ChantColor.ToVector3() * 0.25f);
            int interval = hot ? 3 : 5;
            if (proj.timeLeft % interval == 0) {
                PRTLoader.NewParticle<PRT_Spark>(proj.Center + Main.rand.NextVector2Circular(2f, 2f),
                    -proj.velocity * 0.08f, hot ? ChantColor : EmberDeep,
                    Main.rand.NextFloat(0.2f, 0.34f))?.Configure(false, Main.rand.Next(8, 14));
            }
            //火星雨形态：重力下坠成雨弧
            if (router.MarkData == FormRain) {
                proj.velocity.Y += 0.14f;
            }
        }

        public override void GsProjOnHitNPC(Projectile proj, NPC target, NPC.HitInfo hit, int damageDone, GodSmithProjRouter router) {
            //命中相：火星迸溅（owner 端个人反馈）
            if (!VaultUtils.isServer) {
                Vector2 dir = proj.velocity.SafeNormalize(Vector2.UnitX);
                for (int i = 0; i < 5; i++) {
                    PRTLoader.NewParticle<PRT_Spark>(target.Center,
                        (-dir).RotatedByRandom(0.8) * Main.rand.NextFloat(2f, 5f),
                        Main.rand.NextBool() ? ChantColor : EmberDeep,
                        Main.rand.NextFloat(0.25f, 0.42f))?.Configure(true, Main.rand.Next(10, 18));
                }
            }

            //引燃链跳：正拍原生弹按施法层数起链，链弹按剩余段数续链
            if (!proj.IsOwnedByLocalPlayer()) {
                return;
            }
            int hopsLeft;
            int hopDamage;
            if (router.MarkData is FormOnBeat or FormEmpower) {
                hopsLeft = (int)router.MarkData2;
                hopDamage = Math.Max(1, (int)(proj.damage * 0.5f));
            }
            else if (router.MarkData == FormChain && router.MarkData2 > 0f) {
                hopsLeft = (int)router.MarkData2;
                hopDamage = proj.damage;
            }
            else {
                return;
            }
            if (hopsLeft <= 0) {
                return;
            }
            NPC next = FindNearestEnemy(target.Center, 90f, target.whoAmI);
            if (next == null) {
                return;
            }
            Vector2 vel = (next.Center - target.Center).SafeNormalize(Vector2.UnitX) * 9f;
            QueueForm(Main.player[proj.owner], FormChain, hopsLeft - 1);
            Projectile.NewProjectile(proj.GetSource_FromThis(), target.Center, vel,
                proj.type, hopDamage, proj.knockBack * 0.5f, proj.owner);
            //链跳线：沿跳线撒火星，读作火苗窜过去了
            if (!VaultUtils.isServer) {
                for (int i = 0; i < 4; i++) {
                    Vector2 p = Vector2.Lerp(target.Center, next.Center, (i + 1) / 5f);
                    PRTLoader.NewParticle<PRT_Spark>(p, vel * 0.05f, ChantColor, 0.22f)
                        ?.Configure(false, Main.rand.Next(8, 12));
                }
            }
        }

        public override void GsProjOnKill(Projectile proj, int timeLeft, GodSmithProjRouter router) {
            //余痕相：火花熄灭处回落的余烬比弹体活得久
            if (VaultUtils.isServer) {
                return;
            }
            int count = router.MarkData == FormStraight ? 2 : 3;
            for (int i = 0; i < count; i++) {
                PRTLoader.NewParticle<PRT_Spark>(proj.Center + Main.rand.NextVector2Circular(3f, 3f),
                    new Vector2(Main.rand.NextFloat(-0.7f, 0.7f), -Main.rand.NextFloat(0.3f, 1f)),
                    Main.rand.NextBool() ? ChantColor : EmberDeep,
                    Main.rand.NextFloat(0.22f, 0.36f))?.Configure(true, Main.rand.Next(16, 26));
            }
        }
    }
}
