using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.SummonWhips.Projectiles
{
    /// <summary>
    /// 处决印脉冲：标记归属者每秒对每个挂印目标发一枚的纯视觉真弹幕，
    /// 让队友也能看见「这个目标已就绪处决」。零伤害零判定，只随包同步存在。<br/>
    /// ai[0] = 目标 npc.whoAmI（NPC 槽位由服务器权威分配，各端一致）；
    /// ai[1] = 归属鞭物品 ID（借方案表取印记配色）
    /// </summary>
    internal class GsWhipSealPulseProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private const int LifeFrames = 26;

        public override void SetDefaults() {
            Projectile.width = 8;
            Projectile.height = 8;
            Projectile.friendly = false;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = LifeFrames;
            Projectile.netImportant = true;
        }

        public override void AI() {
            int npcIndex = (int)Projectile.ai[0];
            if (npcIndex >= 0 && npcIndex < Main.maxNPCs) {
                NPC npc = Main.npc[npcIndex];
                if (npc.active) {
                    Projectile.Center = npc.Top + new Vector2(0f, -20f);
                }
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D star = CWRUtils.GetT2DAsset(CWRConstant.Masking + "StarTexture")?.Value;
            Texture2D cross = CWRUtils.GetT2DAsset(CWRConstant.Masking + "RayCross01")?.Value;
            if (star == null || cross == null) {
                return false;
            }
            //一次扩张渐隐的脉搏；配色随归属鞭
            float t = 1f - Projectile.timeLeft / (float)LifeFrames;
            float fade = 1f - t;
            float grow = 0.14f + 0.3f * t;
            GsWhipScheme scheme = GsWhipScheme.SchemeOfItem((int)Projectile.ai[1]);
            Color main = scheme?.MarkColor ?? new Color(255, 92, 46);
            Color rim = Color.Lerp(main, new Color(255, 206, 96), 0.5f);
            main.A = 0;
            rim.A = 0;
            Vector2 pos = Projectile.Center - Main.screenPosition;
            float rot = Main.GlobalTimeWrappedHourly * 1.3f + Projectile.identity * 0.53f;
            Main.EntitySpriteDraw(star, pos, null, main * (0.7f * fade), rot,
                star.Size() * 0.5f, grow, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(cross, pos, null, rim * (0.5f * fade), -rot * 0.7f,
                cross.Size() * 0.5f, grow * 0.62f, SpriteEffects.None, 0);
            return false;
        }
    }
}
