using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Rendering;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
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
            Texture2D tongue = CultistRenderHelper.TearFlame01?.Value;
            float t = Projectile.localAI[0];
            float speed = Projectile.velocity.Length();
            float stretch = MathHelper.Clamp(speed * 0.09f, 0.7f, 1.9f);

            //本体基底=原版信徒火球467（4帧，全亮，自旋），帧率随速度滚得越急
            Main.instance.LoadProjectile(ProjectileID.CultistBossFireBall);
            Texture2D fireball = TextureAssets.Projectile[ProjectileID.CultistBossFireBall].Value;
            int fh = fireball.Height / 4;
            int frameIdx = (int)(t / MathHelper.Clamp(4.5f - speed * 0.18f, 2f, 4.5f) + Projectile.whoAmI) % 4;
            Rectangle src = new(0, frameIdx * fh, fireball.Width, fh);
            Vector2 origin = new(fireball.Width / 2f, fh / 2f);
            float bodyRot = t * 0.21f * (Projectile.whoAmI % 2 == 0 ? 1f : -1f);

            //---- 叠加层（加色批）：焰尾条+舔边火舌+残影链+底晕 ----
            CultistRenderHelper.BeginAdditive(sb);

            //速度拉伸焰尾条（暗红外缘→亮芯）
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

            //残影链：旧位置的火球帧低亮重影（火在身后卷剩的热气）
            for (int i = 2; i < Projectile.oldPos.Length; i += 3) {
                if (Projectile.oldPos[i] == Vector2.Zero) {
                    continue;
                }
                float fade = 1f - i / (float)Projectile.oldPos.Length;
                Vector2 gp = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                sb.Draw(fireball, gp, src, CultistPalette.FireDeep * (0.3f * fade),
                    bodyRot - i * 0.2f, origin, 0.55f * fade + 0.2f, SpriteEffects.None, 0f);
            }

            //底晕垫底（≤30%视觉量）
            sb.Draw(glow, drawPos, null, CultistPalette.FireDeep * 0.5f,
                0f, glow.Size() / 2f, 0.7f, SpriteEffects.None, 0f);

            CultistRenderHelper.EndAdditive(sb);

            //---- 本体：原版火球真实纹理，全亮（实体批） ----
            sb.Draw(fireball, drawPos, src, new Color(255, 255, 255, 255),
                bodyRot, origin, new Vector2(1f, MathHelper.Clamp(stretch, 1f, 1.3f)), SpriteEffects.None, 0f);

            return false;
        }
    }
}
