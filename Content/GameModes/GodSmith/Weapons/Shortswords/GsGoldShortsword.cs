using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Shortswords
{
    /// <summary>
    /// 金短剑重铸「鎏金掠夺」。<br/>
    /// 材质：鎏金镀刃，出鞘即生辉。签名行为：①命中 25% 概率从目标身上迸出小额钱币
    /// ②击杀必迸一把 ③金光粒子与钱币脆响的富贵反馈，刃身鎏金流光常驻
    /// </summary>
    internal class GsGoldShortsword : GsShortswordScheme
    {
        public override int TargetItemID => ItemID.GoldShortsword;

        protected override string GsDescFallback =>
            "Reforged: gilded edge that shakes loose coins, one hit in four rattles change from the wound;" +
            "\nkills always spill a handful";

        protected override int HeldProjType => ModContent.ProjectileType<GsGoldShortswordHeld>();

        public override void GsModifyWeaponDamage(Item item, Player player, ref StatModifier damage)
            => damage *= 1.12f;//掠夺经济收益在机制端，底伤只补一成出头
    }

    /// <summary>
    /// 金短剑手持突刺：命中掠币（owner 端 rand + IsOwnedByLocalPlayer 守门，
    /// noGrabDelay 常规掉落随原版物品同步过线）；假人/雕像怪不结算
    /// </summary>
    internal class GsGoldShortswordHeld : GsThrustHeldBase
    {
        protected override int TargetItemType => ItemID.GoldShortsword;

        //鎏金色板
        internal static readonly Color GoldBright = new(255, 238, 152);
        internal static readonly Color GoldMain = new(242, 192, 72);
        internal static readonly Color GoldDeep = new(152, 102, 32);

        protected override float WindupFrames => 3f;
        protected override float ThrustFrames => 4f;
        protected override float DwellFrames => 2f;
        protected override float RecoverFrames => 6f;
        protected override float PullbackDist => 10f;
        protected override float StabReach => 32f;
        protected override float BladeLength => 44f;
        protected override float ThrustEasePower => 2.6f;
        protected override int HitstopFrames => 2;
        protected override float LeanAmp => 0.030f;
        protected override float ThrustPitch => 0.15f;

        protected override Color EdgeColor => GoldBright;
        protected override Color CoreColor => GoldMain;
        protected override Color ShaftColor => GoldDeep with { A = 235 };

        /// <summary>掠夺结算护栏：假人/雕像怪/召唤物不出币</summary>
        private static bool ValidPlunderTarget(NPC target)
            => target.active && !target.friendly && target.lifeMax > 5
               && target.type != NPCID.TargetDummy && !target.SpawnedFromStatue;

        protected override void OnHitTarget(NPC target, NPC.HitInfo hit, int damageDone, bool firstOnTarget) {
            if (!firstOnTarget || !Projectile.IsOwnedByLocalPlayer() || !ValidPlunderTarget(target)) {
                return;
            }
            bool kill = target.life <= 0;
            //击杀必迸，普通命中 25% 概率（owner 端 rand）；币值护栏远低于 1 银
            if (!kill && !Main.rand.NextBool(4)) {
                return;
            }
            int stack = kill ? Main.rand.Next(8, 15) : Main.rand.Next(2, 6);
            Item.NewItem(Projectile.GetSource_FromThis(), target.Hitbox, ItemID.CopperCoin, stack, noGrabDelay: true);

            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Coins with { Volume = kill ? 0.6f : 0.4f, Pitch = kill ? -0.1f : 0.2f }, target.Center);
                //掠夺金闪：击杀更盛
                PRTLoader.NewParticle<PRT_Light>(target.Center, Vector2.Zero, GoldBright,
                    kill ? 0.30f : 0.20f)?.Configure(kill ? 13 : 10, 0.85f);
                int glints = kill ? 8 : 4;
                for (int i = 0; i < glints; i++) {
                    Vector2 vel = Main.rand.NextVector2Unit() * Main.rand.NextFloat(2f, 5.5f);
                    vel.Y -= 2f;//钱币往上蹦
                    Color c = Main.rand.NextBool() ? GoldBright : GoldMain;
                    PRTLoader.NewParticle<PRT_Spark>(target.Center, vel, c, Main.rand.NextFloat(0.35f, 0.6f))
                        ?.Configure(true, Main.rand.Next(14, 24));
                }
            }
        }

        /// <summary>命中反馈：金屑迸溅，比素铁多一分璀璨</summary>
        protected override void SpawnHitEffects(NPC target, NPC.HitInfo hit) {
            Vector2 pos = Vector2.Lerp(TipPos, target.Center, 0.5f);
            for (int i = 0; i < 6; i++) {
                Vector2 vel = stabUnit.RotatedByRandom(0.6) * Main.rand.NextFloat(3f, 7.5f);
                Color c = Main.rand.NextBool(3) ? GoldDeep : (Main.rand.NextBool() ? GoldBright : GoldMain);
                PRTLoader.NewParticle<PRT_Spark>(pos, vel, c, Main.rand.NextFloat(0.32f, 0.58f))
                    ?.Configure(true, Main.rand.Next(10, 18));
            }
            if (!CWRLoad.NPCValue.ISTheofSteel(target)) {
                Dust d = Dust.NewDustPerfect(pos, DustID.Blood,
                    stabUnit.RotatedByRandom(0.8) * Main.rand.NextFloat(1.5f, 3f), 100, default, Main.rand.NextFloat(0.8f, 1.1f));
                d.noGravity = Main.rand.NextBool();
            }
        }

        /// <summary>鎏金流光：刃身常驻低频呼吸辉光（定值脉动，无随机）</summary>
        protected override float ExtraGlowStrength()
            => 0.10f + 0.06f * MathF.Sin(Main.GlobalTimeWrappedHourly * 5f + Projectile.whoAmI);
    }
}
