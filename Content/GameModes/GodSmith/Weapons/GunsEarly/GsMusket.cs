using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using CalamityOverhaul.Content.GameModes.UI;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.GunsEarly
{
    /// <summary>
    /// 火枪「贯石重弹」：纯前装 MagSize 1，每发皆末发。<br/>
    /// 重弹：弹速 +40%、穿透 +1、命中起小冲击环；Reload 55t 三段式（倒药/杵压/上膛三响三烟）。
    /// 完美窗在杵压拍（位置 0.5、宽 10t）：本发 +30% 且穿透 +2。后坐 3px，跳射位移可感知。<br/>
    /// 账目：周期 max(32ut,55t)=55t，射速比 0.58；伤害行 ×1.6 → 0.93，
    /// 完美拍与穿透群体价值补足至约 105%（待游戏内标定）
    /// </summary>
    internal class GsMusket : GsMagazineScheme
    {
        public override int TargetItemID => ItemID.Musket;

        protected override string GsDescFallback =>
            "Reforged: a true muzzle-loader; every ball is a stone-piercer that flies faster and punches through one more foe.\n" +
            "Reload in three beats: pour, ram, prime. Right-click on the ram beat for +30% damage and extra pierce";

        public override int MagSize => 1;
        public override int ReloadTicks => 55;
        public override GsReloadStyle Style => GsReloadStyle.Muzzle;
        public override int PerfectWindow => 10;
        public override float PerfectWindowPos => 0.5f;
        public override float PerfectShotDamageMul => 1.3f;
        protected override float GetRecoil(bool lastRound) => 3f;
        protected override bool EjectsShell => false;
        protected override int ReloadCueCount => 3;

        /// <summary>伤害行：把装填空窗折回持续 DPS 的补偿，账目见类注释</summary>
        public override void GsModifyWeaponDamage(Item item, Player player, ref StatModifier damage) => damage *= 1.6f;

        protected override void ModifyShot(Item item, Player player, GsGunsEarlyPlayer mp, ref Vector2 position,
            ref Vector2 velocity, ref int type, ref int damage, ref float knockback, bool lastRound) {
            velocity *= 1.4f;   //贯石重弹：弹速 +40%
        }

        protected override bool? FireLastRound(Item item, Player player, GsGunsEarlyPlayer mp,
            EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            //完美拍出膛的重弹打 2 档标（穿透 +2），普通 1 档（穿透 +1）
            pendingMark = mp.perfectNextShot ? 2f : 1f;
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item36 with { Volume = 0.5f, Pitch = -0.45f }, position);
            }
            return null;    //原版弹幕照常生成，交给路由打标
        }

        protected override void OnSpawnMarkedExtra(Projectile proj, GodSmithProjRouter router) {
            //穿透只在 owner 端生效即可（命中在 owner 端裁决）；>0 守卫防 -1 无限穿被写坏
            int add = router.MarkData >= 2f ? 2 : 1;
            if (proj.penetrate > 0) {
                proj.penetrate += add;
            }
        }

        //==================== 三段式前装 ====================

        protected override void OnReloadStart(Item item, Player player, GsGunsEarlyPlayer mp) {
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Grab with { Volume = 0.5f, Pitch = -0.3f }, player.Center);
            }
        }

        protected override void OnReloadCue(Item item, Player player, GsGunsEarlyPlayer mp, int index, int total) {
            if (VaultUtils.isServer) {
                return;
            }
            //三响三烟：倒药沙声、杵压闷响（完美拍就在这里）、上膛脆响
            float pitch = index switch { 1 => -0.5f, 2 => -0.15f, _ => 0.3f };
            SoundEngine.PlaySound(SoundID.Unlock with { Volume = 0.6f + 0.1f * index, Pitch = pitch }, player.Center);
            for (int i = 0; i < 2; i++) {
                PRTLoader.NewParticle<PRT_Smoke>(player.Top + new Vector2(player.direction * 10f, 2f),
                    new Vector2(player.direction * Main.rand.NextFloat(0.2f, 0.6f), -Main.rand.NextFloat(0.5f, 1.1f)),
                    new Color(150, 140, 120), Main.rand.NextFloat(0.04f, 0.07f))
                    ?.Configure(Main.rand.Next(14, 22), 0.35f, 0.02f);
            }
        }

        //==================== 重弹表现 ====================

        public override void GsProjPostAI(Projectile proj, GodSmithProjRouter router) {
            if (router.MarkData < 1f || VaultUtils.isServer) {
                return;
            }
            Lighting.AddLight(proj.Center, 0.24f, 0.18f, 0.1f);
            if (proj.timeLeft % 3 == 0) {
                PRTLoader.NewParticle<PRT_Smoke>(proj.Center - proj.velocity * 0.5f,
                    -proj.velocity * 0.03f, new Color(140, 132, 118), Main.rand.NextFloat(0.03f, 0.05f))
                    ?.Configure(Main.rand.Next(10, 16), 0.3f);
            }
        }

        public override void GsProjOnHitNPC(Projectile proj, NPC target, NPC.HitInfo hit, int damageDone, GodSmithProjRouter router) {
            if (router.MarkData < 1f || VaultUtils.isServer) {
                return;
            }
            //贯石命中：小震荡环 + 石屑火星（owner 端个人反馈）
            bool perfect = router.MarkData >= 2f;
            PRTLoader.NewParticle<PRT_StarPulseRing>(target.Center, Vector2.Zero,
                perfect ? GameModeTheme.GodSmithEmber : new Color(210, 190, 150), 0f)
                ?.Configure(0.03f, perfect ? 0.42f : 0.3f, 10);
            for (int i = 0; i < 5; i++) {
                PRTLoader.NewParticle<PRT_ProcSpark>(target.Center,
                    (-proj.velocity.SafeNormalize(Vector2.UnitX)).RotatedByRandom(0.8) * Main.rand.NextFloat(2f, 5f),
                    new Color(230, 205, 160), Main.rand.NextFloat(0.3f, 0.5f));
            }
        }
    }
}
