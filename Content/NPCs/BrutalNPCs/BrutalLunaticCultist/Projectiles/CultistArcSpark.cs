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
                Color main = PunishStyle ? new Color(240, 240, 255) : CultistPalette.Main(Element);
                CultistRenderHelper.ElementImpact(Projectile.Center, CultistElement.Thunder, 0.6f);
                SoundEngine.PlaySound(SoundID.Item94 with { Volume = 0.35f, Pitch = 0.4f, MaxInstances = 6 }, Projectile.Center);
                //余韵：命中点残留放射电痕，频闪衰减 14~22 帧（活得比弹体久）
                float baseAngle = Projectile.velocity.ToRotation();
                for (int i = 0; i < 3; i++) {
                    float ang = baseAngle + MathHelper.Lerp(-1.4f, 1.4f, i / 2f) + Main.rand.NextFloat(-0.3f, 0.3f);
                    PRTLoader.NewParticle<PRT_CultistArcTrace>(Projectile.Center, Vector2.Zero, main,
                        Main.rand.NextFloat(0.7f, 1.1f))?.Configure(ang, Main.rand.NextFloat(30f, 58f), Main.rand.Next(14, 22));
                }
            }
        }

        /// <summary>确定性抖动哈希（时间片驱动，各帧内稳定2帧再跳）</summary>
        private static float JitterHash(int seed, int i) {
            float h = (float)Math.Sin(seed * 12.9898f + i * 78.233f) * 43758.5453f;
            return h - (float)Math.Floor(h);
        }

        public override bool PreDraw(ref Color lightColor) {
            SpriteBatch sb = Main.spriteBatch;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            Texture2D bolt = CWRAsset.ThunderTrail.Value;
            Texture2D glow = CWRAsset.SoftGlow.Value;
            Color main = PunishStyle ? new Color(240, 240, 255) : CultistPalette.Main(Element);
            Color bright = PunishStyle ? Color.White : CultistPalette.Bright(Element);

            //频闪：2帧时间片的亮度骤变+偶发1帧压暗（电的时间签名）
            int slice = (int)(Projectile.localAI[0] / 2f) + Projectile.identity * 7;
            float flicker = 0.62f + 0.38f * JitterHash(slice, 3);
            if (JitterHash(slice, 11) < 0.09f) {
                flicker *= 0.25f;
            }

            CultistRenderHelper.BeginAdditive(sb);

            //分形折线本体：oldPos 链 + 每2帧重掷的法向抖offset（电弧不走直线）
            Vector2 prev = Projectile.Center;
            for (int i = 0; i < Projectile.oldPos.Length - 1; i++) {
                if (Projectile.oldPos[i + 1] == Vector2.Zero) {
                    break;
                }
                Vector2 rawA = i == 0 ? Projectile.Center : Projectile.oldPos[i] + Projectile.Size / 2f;
                Vector2 rawB = Projectile.oldPos[i + 1] + Projectile.Size / 2f;
                Vector2 dir = rawB - rawA;
                float seg = dir.Length();
                if (seg < 2f) {
                    continue;
                }
                Vector2 normal = dir.SafeNormalize(Vector2.UnitY).RotatedBy(MathHelper.PiOver2);
                //抖动幅度沿链衰减；相邻段共享端点（prevOff）保持折线连续（断口平滑）
                float fade = 1f - i / (float)Projectile.oldPos.Length;
                float thisOff = (JitterHash(slice, i) - 0.5f) * 11f * fade;
                Vector2 a = prev;
                Vector2 b = rawB + normal * thisOff;
                Vector2 span = b - a;
                sb.Draw(bolt, a - Main.screenPosition, null, main * (0.75f * fade * flicker),
                    span.ToRotation(), new Vector2(0f, bolt.Height / 2f),
                    new Vector2(span.Length() / bolt.Width, 0.16f * fade + 0.05f), SpriteEffects.None, 0f);
                prev = b;
            }

            //弹头电芒+分叉小须：1~2条短刺，随时间片重掷方向
            float stretch = MathHelper.Clamp(Projectile.velocity.Length() * 0.1f, 0.5f, 1.4f);
            sb.Draw(bolt, drawPos, null, bright * (0.95f * flicker),
                Projectile.rotation, new Vector2(bolt.Width * 0.3f, bolt.Height / 2f),
                new Vector2(0.42f * stretch, 0.2f), SpriteEffects.None, 0f);
            int whiskers = JitterHash(slice, 21) > 0.45f ? 2 : 1;
            for (int w = 0; w < whiskers; w++) {
                float wAng = Projectile.rotation + (JitterHash(slice, 30 + w) - 0.5f) * 1.9f;
                float wLen = 18f + JitterHash(slice, 40 + w) * 26f;
                sb.Draw(bolt, drawPos, null, bright * (0.55f * flicker),
                    wAng, new Vector2(0f, bolt.Height / 2f),
                    new Vector2(wLen / bolt.Width, 0.09f), SpriteEffects.None, 0f);
            }
            //弹头底晕（垫底层，≤30%）
            sb.Draw(glow, drawPos, null, main * (0.5f * flicker),
                0f, glow.Size() / 2f, 0.3f, SpriteEffects.None, 0f);

            CultistRenderHelper.EndAdditive(sb);
            return false;
        }
    }
}
