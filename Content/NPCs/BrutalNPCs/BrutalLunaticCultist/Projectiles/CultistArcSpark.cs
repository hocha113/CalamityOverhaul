using CalamityOverhaul.Common;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Rendering;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Projectiles
{
    /// <summary>
    /// 电蛇火花：确定性蛇形抖动的快速电弹；ai[0]=元素染色 ai[1]=1惩罚样式（白电，分身反击）
    /// </summary>
    internal class CultistArcSpark : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder2;

        private CultistElement Element => (CultistElement)(int)Projectile.ai[0];
        private bool PunishStyle => Projectile.ai[1] >= 1f;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 8;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 16;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = true;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 210;
            CooldownSlot = ImmunityCooldownID.Bosses;
        }

        public override void AI() {
            Projectile.localAI[0]++;

            //出膛短暂穿墙，防出生即撞
            Projectile.tileCollide = Projectile.localAI[0] > 14;

            //确定性蛇形：以identity为种子的正弦侧摆
            float phase = Projectile.identity * 0.61f;
            float wobble = (float)Math.Sin(Projectile.localAI[0] * 0.38f + phase) * 0.12f;
            Projectile.velocity = Projectile.velocity.RotatedBy(wobble * 0.32f);
            Projectile.rotation = Projectile.velocity.ToRotation();

            if (!VaultUtils.isServer && Main.rand.NextBool(3)) {
                Color c = PunishStyle ? CultistPalette.ThunderBright : CultistPalette.Main(Element);
                PRTLoader.NewParticle<PRT_CultistVolt>(Projectile.Center,
                    -Projectile.velocity * 0.08f, c, Main.rand.NextFloat(0.5f, 0.9f))?.Configure(Main.rand.Next(8, 16));
            }
            Lighting.AddLight(Projectile.Center, CultistPalette.Main(Element).ToVector3() * 0.4f);
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            if (Element == CultistElement.Thunder) {
                target.AddBuff(BuffID.Electrified, 45);
            }
        }

        public override void OnKill(int timeLeft) {
            if (!VaultUtils.isServer) {
                CultistRenderHelper.ElementImpact(Projectile.Center, CultistElement.Thunder, 0.6f);
                SoundEngine.PlaySound(SoundID.Item94 with { Volume = 0.35f, Pitch = 0.4f, MaxInstances = 6 }, Projectile.Center);
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            SpriteBatch sb = Main.spriteBatch;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            Texture2D line = CWRAsset.Line.Value;
            Texture2D glow = CWRAsset.SoftGlow.Value;
            Color main = PunishStyle ? new Color(240, 240, 255) : CultistPalette.Main(Element);
            Color bright = PunishStyle ? Color.White : CultistPalette.Bright(Element);

            CultistRenderHelper.BeginAdditive(sb);

            //折线尾迹
            for (int i = 0; i < Projectile.oldPos.Length - 1; i++) {
                if (Projectile.oldPos[i] == Vector2.Zero || Projectile.oldPos[i + 1] == Vector2.Zero) {
                    continue;
                }
                Vector2 a = Projectile.oldPos[i] + Projectile.Size / 2f;
                Vector2 b = Projectile.oldPos[i + 1] + Projectile.Size / 2f;
                float seg = (a - b).Length();
                float fade = 1f - i / (float)Projectile.oldPos.Length;
                sb.Draw(line, a - Main.screenPosition, null, main * (0.55f * fade),
                    (b - a).ToRotation(), new Vector2(0f, line.Height / 2f),
                    new Vector2(seg / line.Width, 0.1f * fade + 0.03f), SpriteEffects.None, 0f);
            }

            //弹头电芒
            float stretch = MathHelper.Clamp(Projectile.velocity.Length() * 0.1f, 0.5f, 1.4f);
            sb.Draw(line, drawPos, null, bright * 0.9f,
                Projectile.rotation, new Vector2(line.Width / 2f, line.Height / 2f),
                new Vector2(0.5f * stretch, 0.1f), SpriteEffects.None, 0f);
            sb.Draw(glow, drawPos, null, main * 0.7f,
                0f, glow.Size() / 2f, 0.28f, SpriteEffects.None, 0f);

            CultistRenderHelper.EndAdditive(sb);
            return false;
        }
    }
}
