using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.ChargeBows.Projectiles
{
    /// <summary>
    /// 血雨弓 T3 血泊：贴地滞留 2 秒的地面域。踩踏跳伤（本地免疫 30 帧一跳）+ 对非 boss 轻减速。
    /// 判定宽度与可见体同源（hitbox 即绘制宽）；贴图用真 alpha 的 Extra_98 压扁，暗红不透光
    /// </summary>
    internal class GsBloodPuddleProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        //不注册新键，显示名指向原版物品键
        public override LocalizedText DisplayName => Language.GetText("ItemName.BloodRainBow");

        private static readonly Color PoolMain = new(150, 22, 34);
        private static readonly Color PoolBright = new(214, 42, 54);

        private const int LifeFrames = 120;

        public override void SetDefaults() {
            Projectile.width = 110;
            Projectile.height = 22;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.penetrate = -1;
            Projectile.timeLeft = LifeFrames;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 30;
        }

        public override bool ShouldUpdatePosition() => false;

        public override void AI() {
            //域内非 boss 轻减速（NPC 由服务端权威模拟，客户端同跑无害）
            Rectangle zone = Projectile.Hitbox;
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npc = Main.npc[i];
                if (!npc.active || npc.friendly || npc.boss || npc.dontTakeDamage) {
                    continue;
                }
                if (zone.Intersects(npc.Hitbox)) {
                    npc.velocity.X *= 0.88f;
                }
            }

            if (!VaultUtils.isServer) {
                Lighting.AddLight(Projectile.Center, PoolBright.ToVector3() * 0.16f);
                //表面血泡：低频冒泡
                if (Main.rand.NextBool(4)) {
                    Vector2 at = Projectile.Center + new Vector2(Main.rand.NextFloat(-0.45f, 0.45f) * Projectile.width, -4f);
                    Dust dust = Dust.NewDustPerfect(at, DustID.Blood,
                        new Vector2(0f, -Main.rand.NextFloat(0.3f, 1f)), 120, default, Main.rand.NextFloat(0.7f, 1.1f));
                    dust.noGravity = Main.rand.NextBool();
                }
                if (Main.rand.NextBool(14)) {
                    PRTLoader.NewParticle<PRT_HeartcarverDroplet>(
                        Projectile.Center + new Vector2(Main.rand.NextFloat(-0.4f, 0.4f) * Projectile.width, -6f),
                        new Vector2(0f, -Main.rand.NextFloat(0.8f, 1.6f)), PoolBright, Main.rand.NextFloat(0.5f, 0.8f))
                        ?.Configure(Main.rand.Next(14, 22));
                }
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = CWRUtils.GetT2DAsset(CWRConstant.Masking + "Extra_98")?.Value;
            if (tex == null) {
                return false;
            }
            //起收包络：前 12 帧铺开、后 20 帧收干
            float grow = MathHelper.Clamp((LifeFrames - Projectile.timeLeft) / 12f, 0f, 1f);
            float fade = MathHelper.Clamp(Projectile.timeLeft / 20f, 0f, 1f);
            float alpha = 0.72f * grow * fade;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            Vector2 baseScale = new(Projectile.width / (float)tex.Width * grow, Projectile.height / (float)tex.Height);

            //底层暗池（真 alpha 贴图可真实压暗）
            Main.EntitySpriteDraw(tex, drawPos, null, PoolMain * alpha, 0f,
                tex.Size() / 2f, baseScale, SpriteEffects.None);
            //面层亮泽：identity 定相微涌
            float sway = 0.92f + 0.08f * MathF.Sin(Main.GlobalTimeWrappedHourly * 5f + Projectile.identity * 0.83f);
            Main.EntitySpriteDraw(tex, drawPos + new Vector2(0f, -2f), null, PoolBright * (alpha * 0.55f * sway), 0f,
                tex.Size() / 2f, baseScale * new Vector2(0.9f * sway, 0.7f), SpriteEffects.None);
            return false;
        }
    }
}
