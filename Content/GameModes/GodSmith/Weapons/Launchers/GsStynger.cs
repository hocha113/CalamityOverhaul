using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Launchers
{
    /// <summary>
    /// 毒刺发射器重铸：引信三循环。右键在 [触发]（原版碰撞爆）/ [定距]（飞抵发射时
    /// 光标距离处空爆，弹片收束成定向锥砸向光标线，密度 +30%）/ [跳弹]（砖面弹跳
    /// 一次再爆，绕角打击）之间切换。Stynger Bolt 专属弹药与弹片体系原样保留。<br/>
    /// MarkData = 引信模式，MarkData2 = 定距距离；跳弹旗走每弹幕本地包
    /// </summary>
    internal class GsStynger : GsLauncherScheme
    {
        public override int TargetItemID => ItemID.Stynger;

        protected override string GsDescFallback =>
            "Reforged: right click cycles the fuze. Impact detonates on hit; Range airbursts at your cursor's distance with a focused shrapnel cone; Ricochet bounces off blocks once before arming";

        /// <summary>蜂刺黄</summary>
        internal static readonly Color StingAmber = new(232, 186, 60);

        private LocalizedText modeImpact;
        private LocalizedText modeRange;
        private LocalizedText modeRicochet;

        /// <summary>每弹幕本地包：飞行里程与跳弹旗</summary>
        private class StyngerState
        {
            public float travel;
            public bool bounced;
        }

        public override void GsSetStaticDefaults() {
            modeImpact = this.GetLocalization("ModeImpact", () => "Fuze: impact");
            modeRange = this.GetLocalization("ModeRange", () => "Fuze: range airburst");
            modeRicochet = this.GetLocalization("ModeRicochet", () => "Fuze: ricochet");
        }

        public override void GsModifyWeaponDamage(Item item, Player player, ref StatModifier damage)
            => damage *= 1.08f;

        protected override void OnAltAction(Item item, Player player, GsLaunchersPlayer mp) {
            mp.fuzeMode = (mp.fuzeMode + 1) % 3;
            SoundEngine.PlaySound(SoundID.Unlock with { Volume = 0.6f, Pitch = 0.15f * mp.fuzeMode }, player.Center);
            LocalTip(player, mp.fuzeMode switch { 1 => modeRange, 2 => modeRicochet, _ => modeImpact }, StingAmber);
        }

        public override bool? GsShoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source,
            Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            LaunchPresentation(player, position, velocity, 1.0f, StingAmber);
            return null;
        }

        public override void GsProjOnSpawnMarked(Projectile proj, GodSmithProjRouter router) {
            if (proj.type != ProjectileID.Stynger) {
                return;
            }
            Player player = Main.player[proj.owner];
            GsLaunchersPlayer mp = player.GetModPlayer<GsLaunchersPlayer>();
            router.MarkData = mp.fuzeMode;
            if (mp.fuzeMode == 1) {
                router.MarkData2 = Vector2.Distance(player.Center, Main.MouseWorld);
            }
        }

        public override void GsProjPostAI(Projectile proj, GodSmithProjRouter router) {
            if (proj.type != ProjectileID.Stynger || proj.timeLeft <= 3) {
                return;
            }
            StyngerState st = router.GetOrCreateState<StyngerState>();
            st.travel += proj.velocity.Length();
            int mode = (int)router.MarkData;

            //定距引信：里程到点 owner 端空爆
            if (mode == 1 && proj.IsOwnedByLocalPlayer() && st.travel >= router.MarkData2) {
                GsDetonate(proj);
                return;
            }

            //跳弹引信：首次将撞砖时反弹一次，之后恢复原版触发
            if (mode == 2 && !st.bounced) {
                Vector2 moved = Collision.TileCollision(proj.position, proj.velocity, proj.width, proj.height);
                if (moved != proj.velocity) {
                    st.bounced = true;
                    if (Math.Abs(moved.X - proj.velocity.X) > 0.01f) {
                        proj.velocity.X = -proj.velocity.X * 0.9f;
                    }
                    if (Math.Abs(moved.Y - proj.velocity.Y) > 0.01f) {
                        proj.velocity.Y = -proj.velocity.Y * 0.9f;
                    }
                    if (proj.IsOwnedByLocalPlayer()) {
                        proj.netUpdate = true;
                    }
                    if (!VaultUtils.isServer) {
                        SoundEngine.PlaySound(SoundID.Item56 with { Volume = 0.5f, Pitch = 0.5f }, proj.Center);
                        for (int i = 0; i < 4; i++) {
                            PRTLoader.NewParticle<PRT_Spark>(proj.Center,
                                Main.rand.NextVector2Unit() * Main.rand.NextFloat(1.5f, 3.5f),
                                StingAmber, Main.rand.NextFloat(0.22f, 0.36f))?.Configure(true, Main.rand.Next(10, 16));
                        }
                    }
                }
            }

            //飞行尾迹：低频蜂刺火星
            if (!VaultUtils.isServer && proj.timeLeft % 4 == 0) {
                PRTLoader.NewParticle<PRT_Spark>(proj.Center - proj.velocity * 0.4f,
                    -proj.velocity * 0.04f, StingAmber, Main.rand.NextFloat(0.18f, 0.3f))
                    ?.Configure(false, Main.rand.Next(7, 12));
            }
        }

        public override void GsProjOnSpawnInherited(Projectile proj, GodSmithProjRouter router,
            Projectile parent, GodSmithProjRouter parentRouter) {
            base.GsProjOnSpawnInherited(proj, router, parent, parentRouter);
            //定距空爆的弹片收束成定向锥，砸向光标线（生成端即 owner，鼠标可用）
            if (proj.type == ProjectileID.StyngerShrapnel && (int)parentRouter.MarkData == 1
                && proj.IsOwnedByLocalPlayer()) {
                Vector2 dir = (Main.MouseWorld - proj.Center).SafeNormalize(Vector2.UnitY);
                float speed = Math.Max(proj.velocity.Length(), 9f) * 1.15f;
                proj.velocity = dir.RotatedBy(Main.rand.NextFloat(-0.21f, 0.21f)) * speed;
            }
        }

        public override void GsProjOnKill(Projectile proj, int timeLeft, GodSmithProjRouter router) {
            if (proj.type != ProjectileID.Stynger) {
                return;
            }
            ExplosionAftermath(proj.Center, StingAmber, 0.9f);
            //定距空爆密度 +30%：补一枚弹片（承签自动认爹并被收束引导）
            if ((int)router.MarkData == 1 && proj.IsOwnedByLocalPlayer()) {
                Vector2 dir = (Main.MouseWorld - proj.Center).SafeNormalize(Vector2.UnitY);
                Projectile.NewProjectile(proj.GetSource_FromThis(), proj.Center, dir * 10f,
                    ProjectileID.StyngerShrapnel, Math.Max(1, proj.damage / 2), 1f, proj.owner);
            }
        }
    }
}
