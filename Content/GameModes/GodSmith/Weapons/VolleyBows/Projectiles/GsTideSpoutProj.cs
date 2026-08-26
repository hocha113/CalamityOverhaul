using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.VolleyBows.Projectiles
{
    /// <summary>
    /// 海啸「拍浪」处决水柱：自标记敌脚下拔起的一柱潮涌。
    /// 生成端把 Center 定在敌脚上方约 55px，判定窗 5~18 帧每目标一次。
    /// 柱体 Extra_98（真 alpha）三层拉伸：底部泡沫帽收口、顶部圆帽收口，
    /// 宽度随生命周期涨落，不做两端平切的贴条
    /// </summary>
    internal class GsTideSpoutProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private ref float Life => ref Projectile.localAI[0];

        private const int TotalLife = 34;

        //潮涌色板
        private static readonly Color TideDeep = new(18, 78, 132);
        private static readonly Color TideMain = new(52, 164, 216);
        private static readonly Color TideBright = new(196, 244, 255);

        public override void SetDefaults() {
            Projectile.width = 46;
            Projectile.height = 112;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.penetrate = -1;
            Projectile.timeLeft = TotalLife;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
        }

        public override bool? CanDamage() => Life >= 5f && Life <= 18f ? null : false;

        public override void AI() {
            if (Life == 0f && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Splash with { Volume = 1f, Pitch = -0.2f }, Projectile.Center);
            }
            Life++;

            if (!VaultUtils.isServer && Life < 24f && Life % 3 == 0) {
                //白沫沿柱身上涌
                float h = Projectile.height * 0.5f;
                PRTLoader.NewParticle<PRT_CampfireBubble>(
                    Projectile.Center + new Vector2(Main.rand.NextFloat(-16f, 16f), Main.rand.NextFloat(-h, h)),
                    new Vector2(Main.rand.NextFloat(-0.5f, 0.5f), -Main.rand.NextFloat(1.6f, 3.2f)),
                    TideBright, 0.55f)?.Configure(24);
            }
            Lighting.AddLight(Projectile.Center, TideMain.ToVector3() * 0.35f);
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = CWRAsset.Extra_98?.Value;
            if (tex == null) {
                return false;
            }
            //宽度生命周期：6 帧涨潮、尾 10 帧退潮
            float grow = MathHelper.Clamp(Life / 6f, 0f, 1f);
            grow = 1f - (1f - grow) * (1f - grow);
            float fade = MathHelper.Clamp((TotalLife - Life) / 10f, 0f, 1f);
            float widthK = grow * (0.65f + 0.35f * fade);
            float wob = 1f + 0.06f * MathF.Sin(Life * 0.5f + Projectile.identity * 0.9f);

            Vector2 origin = tex.Size() * 0.5f;
            Vector2 center = Projectile.Center - Main.screenPosition;
            Vector2 bottom = center + new Vector2(0f, Projectile.height * 0.5f);
            float colH = Projectile.height * grow;
            Vector2 colCenter = bottom - new Vector2(0f, colH * 0.5f);
            //Extra_98 竖拉当柱体画布
            float sy = colH / tex.Height;

            //柱体三层：深水鞘、主浪、亮芯（亮芯加色）
            Main.EntitySpriteDraw(tex, colCenter, null, TideDeep * (0.75f * fade), 0f, origin,
                new Vector2(0.62f * widthK * wob, sy * 1.02f), SpriteEffects.None, 0);
            Main.EntitySpriteDraw(tex, colCenter, null, TideMain * (0.85f * fade), 0f, origin,
                new Vector2(0.46f * widthK, sy), SpriteEffects.None, 0);
            Color core = TideBright with { A = 0 };
            Main.EntitySpriteDraw(tex, colCenter, null, core * (0.55f * fade), 0f, origin,
                new Vector2(0.2f * widthK * wob, sy * 0.94f), SpriteEffects.None, 0);

            //底部收口：贴地泡沫盘（宽扁椭圆）
            Main.EntitySpriteDraw(tex, bottom, null, TideMain * (0.7f * fade), 0f, origin,
                new Vector2(0.95f * widthK, 0.16f), SpriteEffects.None, 0);
            Main.EntitySpriteDraw(tex, bottom, null, core * (0.4f * fade), 0f, origin,
                new Vector2(0.7f * widthK, 0.1f), SpriteEffects.None, 0);

            //顶部收口：浪头圆帽，随涨潮上移
            Vector2 top = bottom - new Vector2(0f, colH);
            Main.EntitySpriteDraw(tex, top, null, TideBright * (0.8f * fade), 0f, origin,
                new Vector2(0.34f * widthK * wob, 0.2f + 0.06f * wob), SpriteEffects.None, 0);
            return false;
        }
    }
}
