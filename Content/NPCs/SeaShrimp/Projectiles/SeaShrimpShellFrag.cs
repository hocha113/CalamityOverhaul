using CalamityOverhaul.Content.NPCs.SeaShrimp.Rendering;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.SeaShrimp.Projectiles
{
    /// <summary>
    /// 蜕壳崩落的旧壳碎屑：慢速重力弧（可读的物理残骸），落地碎成晶屑。
    /// 本体直接复用海虾体节贴图（旧壳就是它自己），ai[0]=贴图变体
    /// </summary>
    internal class SeaShrimpShellFrag : SeaShrimpModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private int Variant => (int)Projectile.ai[0];

        public override void SetDefaults() {
            Projectile.width = 30;
            Projectile.height = 26;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.penetrate = 1;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
            Projectile.aiStyle = -1;
            Projectile.timeLeft = 300;
        }

        public override void AI() {
            Projectile.velocity.Y += 0.24f;
            if (Projectile.velocity.Y > 11f) {
                Projectile.velocity.Y = 11f;
            }
            Projectile.velocity.X *= 0.995f;
            Projectile.rotation += Projectile.velocity.X * 0.03f + 0.02f;
            Lighting.AddLight(Projectile.Center, 0.05f, 0.1f, 0.2f);
        }

        public override void OnKill(int timeLeft) {
            if (Main.dedServ) {
                return;
            }
            SoundEngine.PlaySound(SoundID.Dig with { Volume = 0.5f, Pitch = 0.2f, MaxInstances = 3 }, Projectile.Center);
            for (int i = 0; i < 6; i++) {
                PRTLoader.NewParticle<PRT_DefCrystalShard>(Projectile.Center,
                    new Vector2(Main.rand.NextFloat(-2f, 2f), -Main.rand.NextFloat(0.5f, 2.5f)),
                    SeaShrimpRenderer.CrystalBlue * 0.8f,
                    Main.rand.NextFloat(0.35f, 0.7f))?.Configure(Main.rand.Next(18, 30), Main.rand.NextFloat(-0.3f, 0.3f));
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            //旧壳=体节贴图本身：暗调压一档表达"死壳"，带自旋
            Texture2D tex = SeaShrimpRenderer.SegmentTexture(Variant);
            if (tex == null) {
                return false;
            }
            Color shell = lightColor.MultiplyRGB(new Color(150, 150, 165));
            Main.EntitySpriteDraw(tex, Projectile.Center - Main.screenPosition, null, shell,
                Projectile.rotation, tex.Size() * 0.5f, 1f, SpriteEffects.None);
            return false;
        }
    }
}
