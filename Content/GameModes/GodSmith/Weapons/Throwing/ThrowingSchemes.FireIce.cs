using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Throwing.Projectiles;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Throwing
{
    /// <summary>
    /// 莫洛托夫鸡尾酒:火域更宽更久;五秒内砸回同一片(120px)升级成烈焰墙(火伤 +30%,焰舌更密)。
    /// 火域是原版 MolotovFire 子弹幕,走承签通道增强,引用零新增
    /// </summary>
    internal class GsMolotovCocktail : GsThrowScheme
    {
        public override int TargetItemID => ItemID.MolotovCocktail;
        protected override string GsDescFallback =>
            "Reforged: 25% chance not to consume; the fire pool spreads 40% wider and burns 50% longer\nShatter another bottle on the same spot within 5s to raise a firewall that burns 30% hotter";

        protected override float NoConsumeChance => 0.25f;
        protected override float DamageMul => 1.05f;

        /// <summary>火域承签的烈焰墙码(写进火焰自己的 MarkData)</summary>
        private const float WallCode = 1f;

        //上一瓶碎裂点(owner 契约:OnKill 写,下一瓶的承签回调读)
        private Vector2 lastBreakPos;
        private uint lastBreakTick;

        private static bool IsFire(int type)
            => type == ProjectileID.MolotovFire || type == ProjectileID.MolotovFire2 || type == ProjectileID.MolotovFire3;

        public override void GsProjOnSpawnInherited(Projectile proj, GodSmithProjRouter router,
            Projectile parent, GodSmithProjRouter parentRouter) {
            if (!IsFire(proj.type)) {
                return;
            }
            //火域铺宽 40%(生成端改速度,随生成包过线)
            proj.velocity.X *= 1.4f;
            //连投同点判定:与上一瓶碎裂点足够近即升级烈焰墙(改 MarkData 仍在安全窗口)
            bool wall = Main.GameUpdateCount - lastBreakTick <= 300
                && proj.Distance(lastBreakPos) <= 120f;
            router.MarkData = wall ? WallCode : 0f;
        }

        public override bool GsProjPreAI(Projectile proj, GodSmithProjRouter router) {
            if (IsFire(proj.type) && router.IsMarked) {
                GsThrowProjState st = router.GetOrCreateState<GsThrowProjState>();
                if (!st.FuseSet) {
                    //各端首帧统一延燃 50%
                    st.FuseSet = true;
                    proj.timeLeft = (int)(proj.timeLeft * 1.5f);
                }
            }
            return true;
        }

        public override void GsProjPostAI(Projectile proj, GodSmithProjRouter router) {
            if (!IsFire(proj.type) || !router.IsMarked || router.MarkData != WallCode || VaultUtils.isServer) {
                return;
            }
            //烈焰墙:焰舌加密(整片火域共担预算,单焰低频)
            if (Main.rand.NextBool(9)) {
                PRTLoader.NewParticle<PRT_Spark>(proj.Center + Main.rand.NextVector2Circular(6f, 4f),
                    -Vector2.UnitY * Main.rand.NextFloat(1f, 2.4f),
                    new Color(255, 140, 40), Main.rand.NextFloat(0.3f, 0.5f))?.Configure(false, 14);
            }
        }

        protected override void GsThrowModifyHit(Projectile proj, NPC target, ref NPC.HitModifiers modifiers, GodSmithProjRouter router) {
            if (IsFire(proj.type) && router.MarkData == WallCode) {
                modifiers.FinalDamage *= 1.3f;
            }
        }

        protected override void GsThrowOnKill(Projectile proj, int timeLeft, GodSmithProjRouter router) {
            //主瓶碎裂:owner 记下落点,供下一瓶比对(本批火焰承签发生在此之前,读到的是上一瓶)
            if (proj.type == ProjectileID.MolotovCocktail && proj.owner == Main.myPlayer
                && router.LocalState is GsThrowProjState { IsPrimary: true }) {
                lastBreakPos = proj.Center;
                lastBreakTick = Main.GameUpdateCount;
            }
        }
    }

    /// <summary>
    /// 霜冻匕首鱼:多穿一目标;命中带霜的敌人时鱼必滑回来(回收体);
    /// 命中叠失温,3 层触发碎冰(+50% 一击并挂霜火)
    /// </summary>
    internal class GsFrostDaggerfish : GsThrowScheme
    {
        public override int TargetItemID => ItemID.FrostDaggerfish;
        protected override string GsDescFallback =>
            "Reforged: pierces one more foe; crits refund one\nHits stack hypothermia, 3 stacks shatter for +50% and frostburn; hitting a frosted foe always drops the fish back as a pickup";

        protected override float NoConsumeChance => 0.10f;
        protected override float RecoverOnTileChance => 0.35f;
        protected override float RecoverOnFadeChance => 0.15f;
        protected override bool CritRefund => true;
        protected override float DamageMul => 1.10f;

        protected override void GsThrowOnSpawn(Projectile proj, GodSmithProjRouter router, GsThrowProjState st) {
            if (proj.penetrate > 0) {
                proj.penetrate++;
            }
        }

        protected override void GsThrowModifyHit(Projectile proj, NPC target, ref NPC.HitModifiers modifiers, GodSmithProjRouter router) {
            GsThrowGlobalNPC gn = target.GetGlobalNPC<GsThrowGlobalNPC>();
            if (gn.ChillStacks >= 3 && Main.GameUpdateCount <= gn.ChillWindowUntil) {
                //碎冰:第 3 层之后的这一击结算 +50%
                modifiers.FinalDamage *= 1.5f;
            }
        }

        protected override void GsThrowOnHit(Projectile proj, NPC target, NPC.HitInfo hit, int damageDone,
            GodSmithProjRouter router, GsThrowProjState st) {
            if (proj.owner != Main.myPlayer || !st.IsPrimary || target.friendly) {
                return;
            }
            GsThrowGlobalNPC gn = target.GetGlobalNPC<GsThrowGlobalNPC>();
            bool frosted = target.HasBuff(BuffID.Frostburn) || gn.ChillStacks > 0;
            //滑手的鱼又滑了回来:命中带霜敌必掉回收体(每尾一次,免耗投掷不参与)
            if (frosted && !st.Latch && !st.FreeThrow) {
                st.Latch = true;
                SpawnRecoveryAt(proj.GetSource_FromThis(), target.Center, proj.owner);
            }
            //失温记账:碎冰在 ModifyHit 已结算,此处清层重计
            if (Main.GameUpdateCount > gn.ChillWindowUntil) {
                gn.ChillStacks = 0;
            }
            if (gn.ChillStacks >= 3) {
                gn.ChillStacks = 0;
                target.AddBuff(BuffID.Frostburn, 180);
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Item27 with { Volume = 0.7f }, target.Center);
                    for (int i = 0; i < 7; i++) {
                        PRTLoader.NewParticle<PRT_Spark>(target.Center + Main.rand.NextVector2Circular(10f, 10f),
                            Main.rand.NextVector2Circular(3f, 3f),
                            new Color(150, 220, 255), Main.rand.NextFloat(0.26f, 0.44f))?.Configure(true, 18);
                    }
                }
            }
            gn.ChillStacks++;
            gn.ChillWindowUntil = Main.GameUpdateCount + 240;
        }
    }
}
