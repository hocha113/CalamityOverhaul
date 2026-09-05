using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
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
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.WebSpit;

        public override string LocalizationCategory => "GodSmithSummonMinionsA";

        internal const int BindFrames = 90;

        private ref float Life => ref Projectile.localAI[0];

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
        }

        /// <summary>区域尺寸提示：原版蛛网贴图按目标体型拉伸一笔罩在身上（进出场渐隐）</summary>
        public override bool PreDraw(ref Color lightColor) {
            NPC target = BoundTarget;
            float fadeIn = MathHelper.Clamp(Life / 8f, 0f, 1f);
            float fadeOut = MathHelper.Clamp(Projectile.timeLeft / 14f, 0f, 1f);
            float fade = fadeIn * fadeOut;
            if (target == null || fade <= 0.01f) {
                return false;
            }
            Texture2D tex = TextureAssets.Projectile[Type].Value;
            Main.EntitySpriteDraw(tex, Projectile.Center - Main.screenPosition, null, lightColor * fade,
                0f, tex.Size() / 2f,
                new Vector2(target.width * 1.35f / tex.Width, target.height * 1.3f / tex.Height),
                SpriteEffects.None, 0);
            return false;
        }
    }
}
