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
    /// 焚焰弧弹：复合加速+短暂追踪的弯弧火球；ai[0]=初始追踪帧 ai[1]=1落地留焚地
    /// </summary>
    internal class CultistFireBolt : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder2;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 10;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 22;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 330;
            CooldownSlot = ImmunityCooldownID.Bosses;
        }

        public override void AI() {
            Projectile.localAI[0]++;

            if (Projectile.localAI[0] == 1 && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item20 with { Volume = 0.6f, Pitch = 0.2f, MaxInstances = 6 }, Projectile.Center);
            }

            //出膛30帧后才碰撞地形，避免贴墙出生即灭
            Projectile.tileCollide = Projectile.localAI[0] > 30;

            //复合加速：越飞越快
            float speed = Projectile.velocity.Length();
            if (speed < 15.5f) {
                Projectile.velocity *= 1.017f;
            }

            //初期轻追踪画出弧线（确定性衰减转率）
            float homingLife = Projectile.ai[0];
            if (Projectile.localAI[0] < homingLife) {
                int idx = Player.FindClosest(Projectile.position, Projectile.width, Projectile.height);
                Player target = Main.player[idx];
                if (target.Alives()) {
                    float turnRate = MathHelper.Lerp(0.032f, 0.004f, Projectile.localAI[0] / homingLife);
                    Vector2 aim = (target.Center - Projectile.Center).SafeNormalize(Vector2.UnitY);
                    Vector2 dir = Projectile.velocity.SafeNormalize(Vector2.UnitY);
                    Projectile.velocity = dir.ToRotation().AngleTowards(aim.ToRotation(), turnRate).ToRotationVector2() * speed;
                }
            }

            Projectile.rotation = Projectile.velocity.ToRotation();

            //火烬甩尾（活得比弹体久）
            if (!VaultUtils.isServer && Main.rand.NextBool(2)) {
                PRTLoader.NewParticle<PRT_CultistEmber>(Projectile.Center + Main.rand.NextVector2Circular(6f, 6f),
                    -Projectile.velocity * 0.12f + Main.rand.NextVector2Circular(0.8f, 0.8f),
                    CultistPalette.FireBright, Main.rand.NextFloat(0.7f, 1.2f))?.Configure(Main.rand.Next(16, 28));
            }
            //烟尾：火→烟过渡，断续留在航迹上（材质签名：野火拖烟）
            if (!VaultUtils.isServer && (int)Projectile.localAI[0] % 5 == 0) {
                PRTLoader.NewParticle<PRT_CultistSmoke>(Projectile.Center - Projectile.velocity * 1.4f,
                    -Projectile.velocity * 0.03f + Main.rand.NextVector2Circular(0.4f, 0.4f),
                    new Color(200, 110, 60), Main.rand.NextFloat(0.5f, 0.9f))?.Configure(Main.rand.Next(30, 48));
            }

            Lighting.AddLight(Projectile.Center, CultistPalette.FireMain.ToVector3() * 0.7f);
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            target.AddBuff(BuffID.OnFire, 180);
        }

        public override void OnKill(int timeLeft) {
            if (!VaultUtils.isServer) {
                Vector2 dir = Projectile.velocity.SafeNormalize(Vector2.UnitX);
                CultistRenderHelper.ElementImpact(Projectile.Center, CultistElement.Fire, 1.1f);
                SoundEngine.PlaySound(SoundID.Item74 with { Volume = 0.5f, Pitch = -0.1f, MaxInstances = 5 }, Projectile.Center);
                //命中爆裂：前锥烬喷+滞留烟团（余韵超弹体寿命）
                for (int i = 0; i < 7; i++) {
                    Vector2 vel = dir.RotatedBy(Main.rand.NextFloat(-1.1f, 1.1f)) * Main.rand.NextFloat(3f, 9f);
                    PRTLoader.NewParticle<PRT_CultistEmber>(Projectile.Center, vel,
                        CultistPalette.FireBright, Main.rand.NextFloat(0.7f, 1.3f))?.Configure(Main.rand.Next(20, 34));
                }
                for (int i = 0; i < 3; i++) {
                    PRTLoader.NewParticle<PRT_CultistSmoke>(Projectile.Center + Main.rand.NextVector2Circular(10f, 10f),
                        -Vector2.UnitY * Main.rand.NextFloat(0.4f, 1.2f) + Main.rand.NextVector2Circular(0.6f, 0.3f),
                        new Color(190, 100, 55), Main.rand.NextFloat(0.8f, 1.3f))?.Configure(Main.rand.Next(40, 64));
                }
            }
            //焚地余灾
            if (!VaultUtils.isClient && Projectile.ai[1] >= 1f) {
                Projectile.NewProjectile(Projectile.GetSource_FromAI(), Projectile.Center, Vector2.Zero,
                    ModContent.ProjectileType<CultistCinderPatch>(), (int)(Projectile.damage * 0.7f), 0f, Main.myPlayer);
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            SpriteBatch sb = Main.spriteBatch;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            Texture2D glow = CWRAsset.SoftGlow.Value;
            Texture2D streak = CWRAsset.SlashStreak01.Value;
            Texture2D fire = CWRAsset.Fire.Value;
            Texture2D tongue = CultistRenderHelper.TearFlame01?.Value;
            float t = Projectile.localAI[0];
            float speed = Projectile.velocity.Length();

            //火焰帧序列 4×4，帧率随速度（越快滚得越急）
            int fw = fire.Width / 4;
            int fh = fire.Height / 4;
            int frameIdx = (int)(t / MathHelper.Clamp(4.5f - speed * 0.18f, 1.5f, 4.5f) + Projectile.whoAmI * 3) % 16;
            Rectangle src = new(frameIdx % 4 * fw, frameIdx / 4 * fh, fw, fh);
            //错帧的第二层（滚卷的内芯）
            int frameIdx2 = (frameIdx + 7) % 16;
            Rectangle src2 = new(frameIdx2 % 4 * fw, frameIdx2 / 4 * fh, fw, fh);

            CultistRenderHelper.BeginAdditive(sb);

            //速度拉伸焰尾条（暗红外缘→亮芯）
            float stretch = MathHelper.Clamp(speed * 0.09f, 0.7f, 1.9f);
            Vector2 tailScale = new(stretch * 1.15f, 0.24f);
            sb.Draw(streak, drawPos, null, CultistPalette.FireDeep * 0.85f,
                Projectile.rotation + MathHelper.Pi, new Vector2(0, streak.Height / 2f), tailScale * 1.25f, SpriteEffects.None, 0f);
            sb.Draw(streak, drawPos, null, CultistPalette.FireMain * 0.9f,
                Projectile.rotation + MathHelper.Pi, new Vector2(0, streak.Height / 2f), tailScale, SpriteEffects.None, 0f);

            //舔边火舌：两条错相后掠舌，根锚弹体、噪声撕裂端向后（材质签名）
            if (tongue != null) {
                for (int i = 0; i < 2; i++) {
                    float sway = (float)Math.Sin(t * 0.31f + i * 2.6f + Projectile.whoAmI) * 0.22f;
                    float side = i == 0 ? 1f : -1f;
                    float len = (0.5f + 0.16f * (float)Math.Sin(t * 0.47f + i * 1.7f)) * stretch;
                    SpriteEffects fxFlip = i == 0 ? SpriteEffects.None : SpriteEffects.FlipVertically;
                    sb.Draw(tongue, drawPos, null, CultistPalette.FireMain * 0.75f,
                        Projectile.rotation + MathHelper.Pi + sway * side, new Vector2(0f, tongue.Height / 2f),
                        new Vector2(len, 0.24f), fxFlip, 0f);
                }
            }

            //残影链（焰核帧的低亮度重影，火在身后卷剩的热气）
            for (int i = 2; i < Projectile.oldPos.Length; i += 3) {
                if (Projectile.oldPos[i] == Vector2.Zero) {
                    continue;
                }
                float fade = 1f - i / (float)Projectile.oldPos.Length;
                Vector2 gp = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                sb.Draw(fire, gp, src2, CultistPalette.FireDeep * (0.3f * fade),
                    Projectile.rotation + MathHelper.PiOver2, new Vector2(fw / 2f, fh / 2f),
                    0.3f * fade + 0.1f, SpriteEffects.None, 0f);
            }

            //焰核：外晕垫底（≤30%）+暗红大帧+金橙错帧内芯（帧动画=时间签名）
            sb.Draw(glow, drawPos, null, CultistPalette.FireDeep * 0.55f,
                0f, glow.Size() / 2f, 0.72f, SpriteEffects.None, 0f);
            sb.Draw(fire, drawPos, src, CultistPalette.FireMain * 0.95f,
                Projectile.rotation + MathHelper.PiOver2, new Vector2(fw / 2f, fh / 2f),
                new Vector2(0.5f, 0.56f * stretch), SpriteEffects.None, 0f);
            sb.Draw(fire, drawPos, src2, CultistPalette.FireBright * 0.85f,
                Projectile.rotation + MathHelper.PiOver2, new Vector2(fw / 2f, fh / 2f),
                new Vector2(0.32f, 0.38f * stretch), SpriteEffects.None, 0f);

            CultistRenderHelper.EndAdditive(sb);
            return false;
        }
    }
}
