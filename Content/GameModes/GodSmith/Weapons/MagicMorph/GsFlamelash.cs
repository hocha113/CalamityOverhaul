using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.MagicMorph
{
    /// <summary>
    /// 烈焰鞭重铸：引导二形态（火色梯度）。<br/>
    /// A 形态引导提速 25%，硫火尾迹；
    /// B 形态（右键蓄 45t）「炎蛇」：单主控引导弹升格为八节火蛇，
    /// 蛇身沿引导路径迟滞跟随（位置史存 LocalState，各端自绘不入包），命中点燃 3s
    /// </summary>
    internal class GsFlamelash : GsMorphScheme
    {
        public override int TargetItemID => ItemID.Flamelash;

        protected override string GsDescFallback =>
            "Reforged: guided flight steers 25% faster.\nHold right click to charge; release a serpent of flame whose coils trail your guidance and set foes ablaze";

        protected override int ChargeTicksB => 45;
        protected override float ChargeManaMult => 1.8f;
        protected override Color ChargeColor => new(255, 150, 70);
        protected override float BaseDamageMult => 1.10f;

        private static readonly Color FlameAmber = new(255, 168, 82);

        /// <summary>蛇身位置史（每弹幕本地状态包，纯表现不过线）</summary>
        private class SerpentPath
        {
            public List<Vector2> Points = [];
        }

        protected override void FireMorphB(Item item, Player player) {
            SoundEngine.PlaySound(SoundID.Item20 with { Volume = 0.9f, Pitch = -0.2f }, player.Center);
            Vector2 dir = GsAimUnit(player);
            int dmg = (int)(player.GetWeaponDamage(item) * 1.2f);
            float speed = MathHelper.Max(item.shootSpeed, 7f);
            SpawnMorph(player, item, player.Center + dir * 16f, dir * speed,
                ProjectileID.Flamelash, dmg, item.knockBack, KindB);
        }

        public override void GsProjPostAI(Projectile proj, GodSmithProjRouter router) {
            Player owner = Main.player[proj.owner];
            if (proj.owner == Main.myPlayer && owner.channel) {
                //引导提速：原版每帧重置速度基准，恒定放大不会累乘
                proj.velocity *= 1.25f;
            }
            int kind = KindOf(router);
            //蛇身位置史：各端自记（位置由弹幕同步驱动，链形各端近似一致）
            if (kind == KindB) {
                SerpentPath path = router.GetOrCreateState<SerpentPath>();
                if (proj.timeLeft % 2 == 0) {
                    path.Points.Insert(0, proj.Center);
                    if (path.Points.Count > 26) {
                        path.Points.RemoveAt(path.Points.Count - 1);
                    }
                }
            }
            if (!VaultUtils.isServer && proj.timeLeft % (kind == KindB ? 2 : 4) == 0) {
                PRTLoader.NewParticle<PRT_HellFlame>(
                    proj.Center - proj.velocity * 0.3f + Main.rand.NextVector2Circular(4f, 4f),
                    -proj.velocity * 0.06f, FlameAmber, Main.rand.NextFloat(0.4f, 0.7f));
            }
        }

        public override void GsProjPostDraw(Projectile proj, Color lightColor, GodSmithProjRouter router) {
            if (KindOf(router) != KindB || router.LocalState is not SerpentPath path) {
                return;
            }
            //八节蛇身：沿位置史等距取点，本体贴图缩小 + 灼芯光晕（A=0），identity 定相脉动
            Main.instance.LoadProjectile(proj.type);
            var tex = TextureAssets.Projectile[proj.type].Value;
            for (int seg = 1; seg <= 8; seg++) {
                int idx = seg * 3;
                if (idx >= path.Points.Count) {
                    break;
                }
                Vector2 pos = path.Points[idx];
                float shrink = 1f - seg * 0.09f;
                float pulse = 0.8f + 0.2f * MathF.Sin(Main.GlobalTimeWrappedHourly * 8f + proj.identity * 0.71f + seg * 0.9f);
                Color glow = FlameAmber * (0.5f * shrink * pulse);
                glow.A = 0;
                Vector2 toPrev = (path.Points[idx - 1] - pos).SafeNormalize(Vector2.UnitX);
                float rot = toPrev.ToRotation() + MathHelper.PiOver2;
                Main.EntitySpriteDraw(tex, pos - Main.screenPosition, null, glow, rot,
                    tex.Size() / 2f, shrink * 1.05f, Microsoft.Xna.Framework.Graphics.SpriteEffects.None, 0);
                Main.EntitySpriteDraw(tex, pos - Main.screenPosition, null, Color.White * (0.55f * shrink), rot,
                    tex.Size() / 2f, shrink * 0.8f, Microsoft.Xna.Framework.Graphics.SpriteEffects.None, 0);
            }
        }

        public override void GsProjOnHitNPC(Projectile proj, NPC target, NPC.HitInfo hit, int damageDone, GodSmithProjRouter router) {
            if (KindOf(router) == KindB) {
                target.AddBuff(BuffID.OnFire, 180);
            }
        }
    }
}
