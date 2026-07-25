using CalamityOverhaul.Common;
using CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.CrimsonRendSlashs;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.GameContent;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.Inscriptions
{
    /// <summary>
    /// 假切「假身」：疾走起步留下的短命残影，替真身吸收一击后碎裂。
    /// 气质对齐咎影错位残像；禁止扭 Omokage
    /// </summary>
    internal class OniMeiFalseBody : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private ref float LifeMax => ref Projectile.ai[0];
        private int timer;
        private float seed;
        private bool shattered;

        public override void SetDefaults() {
            Projectile.width = 40;
            Projectile.height = 56;
            Projectile.friendly = false;
            Projectile.hostile = false;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 180;
            Projectile.netImportant = true;
        }

        public override bool? CanDamage() => false;

        public override bool ShouldUpdatePosition() => false;

        /// <summary>清旧影后于 pos 生成；owner 端</summary>
        public static void Fire(Player player, Vector2 pos) {
            if (player == null || Main.myPlayer != player.whoAmI) {
                return;
            }
            int type = ModContent.ProjectileType<OniMeiFalseBody>();
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile old = Main.projectile[i];
                if (old.active && old.owner == player.whoAmI && old.type == type) {
                    old.Kill();
                }
            }
            Projectile.NewProjectile(player.GetSource_Misc("CWR_OniMeiFalseBody"), pos, Vector2.Zero
                , type, 0, 0f, player.whoAmI, ai0: OniMeiCombat.FalseBodyLifeTicks);
        }

        public static bool AnyOwned(Player player) {
            if (player == null) {
                return false;
            }
            int type = ModContent.ProjectileType<OniMeiFalseBody>();
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile proj = Main.projectile[i];
                if (proj.active && proj.owner == player.whoAmI && proj.type == type
                    && proj.ModProjectile is OniMeiFalseBody body && !body.shattered) {
                    return true;
                }
            }
            return false;
        }

        public static OniMeiFalseBody TryGetOwned(Player player) {
            if (player == null) {
                return null;
            }
            int type = ModContent.ProjectileType<OniMeiFalseBody>();
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile proj = Main.projectile[i];
                if (proj.active && proj.owner == player.whoAmI && proj.type == type
                    && proj.ModProjectile is OniMeiFalseBody body && !body.shattered) {
                    return body;
                }
            }
            return null;
        }

        public override void AI() {
            if (timer == 0) {
                if (LifeMax > 0) {
                    Projectile.timeLeft = (int)LifeMax;
                }
                seed = Projectile.identity * 0.618f % 1f;
            }
            timer++;
            if (Main.dedServ || shattered) {
                return;
            }
            if (timer % 8 == 0) {
                Vector2 offset = new((seed - 0.5f) * 10f, Main.rand.NextFloat(-6f, 6f));
                PRTLoader.NewParticle<PRT_CrimsonSmoke>(Projectile.Center + offset
                    , -Vector2.UnitY * Main.rand.NextFloat(0.3f, 0.8f) + Main.rand.NextVector2Circular(0.3f, 0.3f)
                    , Color.White, Main.rand.NextFloat(0.04f, 0.07f))
                    ?.Configure(Main.rand.Next(12, 20), new Color(90, 22, 32), new Color(22, 10, 16));
            }
            Lighting.AddLight(Projectile.Center, new Vector3(0.55f, 0.12f, 0.14f));
        }

        /// <summary>吸伤碎裂：粒子 + 通知 Player 真空窗</summary>
        public void Shatter() {
            if (shattered || !Projectile.active) {
                return;
            }
            shattered = true;
            if (!Main.dedServ) {
                for (int i = 0; i < 8; i++) {
                    Vector2 vel = Main.rand.NextVector2Circular(3.5f, 3.5f);
                    PRTLoader.NewParticle<PRT_CrimsonSpark>(Projectile.Center, vel
                        , new Color(255, 180, 190), Main.rand.NextFloat(0.22f, 0.4f))
                        ?.Configure(Main.rand.Next(10, 16), affectedByGravity: false);
                }
                for (int i = 0; i < 5; i++) {
                    PRTLoader.NewParticle<PRT_CrimsonSmoke>(Projectile.Center + Main.rand.NextVector2Circular(12f, 12f)
                        , Main.rand.NextVector2Circular(1.5f, 1.5f), Color.White
                        , Main.rand.NextFloat(0.06f, 0.10f))
                        ?.Configure(Main.rand.Next(14, 22), new Color(100, 24, 34), new Color(20, 10, 14));
                }
            }
            if (Main.player[Projectile.owner] is Player owner
                && owner.TryGetModPlayer(out OnikiriPlayer okp)) {
                okp.OnFalseBodyShattered();
            }
            Projectile.Kill();
        }

        public override bool PreDraw(ref Color lightColor) {
            if (Main.dedServ || shattered) {
                return false;
            }
            Texture2D blade = TextureAssets.Item[ModContent.ItemType<OnikiriItem>()].Value;
            Vector2 origin = blade.Size() * new Vector2(0.12f, 0.55f);
            float lifeT = Projectile.timeLeft / Math.Max(LifeMax, 1f);
            float alpha = MathHelper.Clamp(0.35f + 0.25f * lifeT, 0.2f, 0.55f);
            float rot = -0.55f + seed * 0.35f;
            int facing = Projectile.Center.X >= Main.player[Projectile.owner].Center.X ? 1 : -1;
            SpriteEffects fx = facing < 0 ? SpriteEffects.FlipVertically : SpriteEffects.None;
            Vector2 split = (rot + MathHelper.PiOver2).ToRotationVector2() * (3.5f + (1f - lifeT) * 4f);

            Color tint = new Color(180, 40, 55, 0) * alpha;
            Color ghost = new Color(40, 10, 16, 0) * (alpha * 0.55f);
            Main.spriteBatch.Draw(blade, Projectile.Center - Main.screenPosition + split, null, ghost
                , rot, origin, 0.92f, fx, 0f);
            Main.spriteBatch.Draw(blade, Projectile.Center - Main.screenPosition - split * 0.6f, null, tint
                , rot + 0.08f, origin, 0.90f, fx, 0f);
            return false;
        }
    }
}
