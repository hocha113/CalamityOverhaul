using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.SummonMinions.Projectiles
{
    /// <summary>
    /// 缚网罩：蜘蛛协同的标记载体。跟随目标 90 帧，本身不伤害；
    /// owner 端命中修饰查询「目标身上有无自家网罩」实现 +15% 集火加成，队友可见网罩本体。<br/>
    /// ai[0] = 目标 NPC 索引，ai[1] = 目标类型校验（经 NewProjectile 形参传入，随生成包过线）；
    /// 各端本地检测目标失效即消亡，确定性一致
    /// </summary>
    internal class GsWebBindProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        public override string LocalizationCategory => "GodSmithSummonMinionsA";

        private static readonly Color WebPale = new(232, 228, 244);
        private static readonly Color WebVenom = new(168, 130, 226);

        internal const int BindFrames = 90;

        private ref float Life => ref Projectile.localAI[0];

        private float Seed => Projectile.identity * 0.7717f % MathHelper.TwoPi;

        private NPC BoundTarget {
            get {
                int idx = (int)Projectile.ai[0];
                if (idx < 0 || idx >= Main.maxNPCs) {
                    return null;
                }
                NPC npc = Main.npc[idx];
                return npc.active && npc.type == (int)Projectile.ai[1] ? npc : null;
            }
        }

        public override void SetDefaults() {
            Projectile.width = 16;
            Projectile.height = 16;
            Projectile.friendly = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = BindFrames;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
        }

        public override void AI() {
            Life++;
            //收网音放 AI 首帧（各端都跑，远端也可闻）
            if (Life == 1f && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item17 with { Volume = 0.5f, Pitch = -0.2f },
                    Projectile.Center);
            }
            NPC target = BoundTarget;
            if (target == null) {
                //目标失效：各端本地同判即时收网
                Projectile.Kill();
                return;
            }
            Projectile.Center = target.Center;
            Projectile.velocity = Vector2.Zero;

            if (!VaultUtils.isServer && Main.rand.NextBool(7)) {
                Dust web = Dust.NewDustDirect(target.position, target.width, target.height,
                    DustID.Web, 0f, 0.3f, 120, default, 0.9f);
                web.noGravity = true;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            NPC target = BoundTarget;
            Texture2D soft = CWRAsset.Extra_98?.Value;
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (target == null || soft == null || glow == null) {
                return false;
            }
            float fadeIn = MathHelper.Clamp(Life / 8f, 0f, 1f);
            float fadeOut = MathHelper.Clamp(Projectile.timeLeft / 14f, 0f, 1f);
            float fade = fadeIn * fadeOut;
            Vector2 pos = Projectile.Center - Main.screenPosition;
            float wrapW = target.width * 1.35f / soft.Width;
            float wrapH = target.height * 1.3f / soft.Height;

            //网罩暗底（真 alpha 压暗）+ 缠绕丝光带
            Main.EntitySpriteDraw(soft, pos, null, (WebVenom * 0.35f) * fade, 0f,
                soft.Size() / 2f, new Vector2(wrapW, wrapH), SpriteEffects.None, 0);
            for (int i = 0; i < 3; i++) {
                float ang = Seed + i * (MathHelper.Pi / 3f)
                    + 0.06f * (float)Math.Sin(Life * 0.1f + i);
                Main.EntitySpriteDraw(soft, pos, null, WebPale * (0.4f * fade), ang,
                    soft.Size() / 2f,
                    new Vector2(wrapW * 1.2f, 3.2f / soft.Height), SpriteEffects.None, 0);
            }
            Main.EntitySpriteDraw(glow, pos, null, (WebVenom with { A = 0 }) * (0.3f * fade),
                0f, glow.Size() / 2f, new Vector2(wrapW * 1.5f, wrapH * 1.4f) * 0.35f,
                SpriteEffects.None, 0);
            return false;
        }
    }
}
