using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.VolleyBows.Projectiles
{
    /// <summary>
    /// 幻影弓「猎魂标」召来的幻影射手：悬于射手后上方的星旋残影（保底交付形态：
    /// 幻影弓贴图悬浮开火 + SoftGlow 人形雾，不捕玩家皮肤快照）。
    /// 本体无伤害；开火由方案在 owner 端射击流里驱动（生成 25% 幻影箭），
    /// 后座帧走 localAI 纯本端视觉。至多 1 名/玩家，时长可被连击与右键续满
    /// </summary>
    internal class GsPhantomArcherProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        /// <summary>开火后座剩余帧（owner 端视觉）</summary>
        internal ref float Recoil => ref Projectile.localAI[0];

        private ref float Life => ref Projectile.localAI[1];

        //星旋幻影色板
        internal static readonly Color PhantomTeal = new(96, 222, 218);
        internal static readonly Color PhantomBlue = new(70, 140, 235);

        public override void SetDefaults() {
            Projectile.width = 30;
            Projectile.height = 44;
            Projectile.friendly = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 180;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
        }

        public override bool ShouldUpdatePosition() => false;

        public override void AI() {
            Player owner = Main.player[Projectile.owner];
            if (!owner.active || owner.dead) {
                Projectile.Kill();
                return;
            }
            Life++;
            if (Recoil > 0f) {
                Recoil--;
            }
            //锚定射手后上方，缓动跟随
            Vector2 anchor = owner.Center + new Vector2(-owner.direction * 34f, -50f);
            Projectile.Center = Life <= 1f ? anchor : Vector2.Lerp(Projectile.Center, anchor, 0.18f);

            if (!VaultUtils.isServer && Life % 5 == 0) {
                PRTLoader.NewParticle<PRT_Light>(
                    Projectile.Center + Main.rand.NextVector2Circular(14f, 20f),
                    new Vector2(0f, -Main.rand.NextFloat(0.2f, 0.6f)),
                    Main.rand.NextBool() ? PhantomTeal : PhantomBlue, 0.08f)?.Configure(14, 0.7f);
            }
            Lighting.AddLight(Projectile.Center, PhantomTeal.ToVector3() * 0.25f);
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (glow == null) {
                return false;
            }
            Player owner = Main.player[Projectile.owner];
            Vector2 pos = Projectile.Center - Main.screenPosition;
            Vector2 gOrigin = glow.Size() * 0.5f;
            float fadeIn = MathHelper.Clamp(Life / 10f, 0f, 1f);
            float fadeOut = MathHelper.Clamp(Projectile.timeLeft / 12f, 0f, 1f);
            float fade = fadeIn * fadeOut;
            float breathe = 0.9f + 0.1f * MathF.Sin(Life * 0.16f + Projectile.identity * 0.7f);
            Color teal = PhantomTeal with { A = 0 };
            Color blue = PhantomBlue with { A = 0 };

            //人形雾：躯干团 + 头部小团（加色，不做黑块）
            Main.EntitySpriteDraw(glow, pos, null, blue * (0.42f * fade), 0f, gOrigin,
                new Vector2(0.5f, 0.66f) * breathe, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(glow, pos + new Vector2(0f, -24f), null, teal * (0.5f * fade), 0f, gOrigin,
                0.24f * breathe, SpriteEffects.None, 0);

            //幻影弓：原版 Phantasm 贴图加色残影，随 owner 举弓角开火
            Main.instance.LoadItem(ItemID.Phantasm);
            Texture2D bow = TextureAssets.Item[ItemID.Phantasm].Value;
            float aim = owner.itemAnimation > 0 ? owner.itemRotation : owner.direction * -0.22f;
            SpriteEffects flip = owner.direction < 0 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
            Vector2 bowPos = pos + new Vector2(owner.direction * (10f - Recoil * 1.1f), -6f);
            Main.EntitySpriteDraw(bow, bowPos, null, teal * (0.85f * fade), aim,
                bow.Size() * 0.5f, 0.92f, flip, 0);
            Main.EntitySpriteDraw(bow, bowPos, null, (Color.White with { A = 0 }) * (0.3f * fade * breathe), aim,
                bow.Size() * 0.5f, 0.92f, flip, 0);
            return false;
        }

        public override void OnKill(int timeLeft) {
            if (VaultUtils.isServer) {
                return;
            }
            for (int i = 0; i < 4; i++) {
                PRTLoader.NewParticle<PRT_Light>(Projectile.Center + Main.rand.NextVector2Circular(12f, 18f),
                    Main.rand.NextVector2Circular(0.8f, 0.8f),
                    Main.rand.NextBool() ? PhantomTeal : PhantomBlue, 0.11f)?.Configure(16, 0.8f);
            }
        }
    }
}
