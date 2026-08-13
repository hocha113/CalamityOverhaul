using CalamityOverhaul.Common;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Rendering;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
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

            Lighting.AddLight(Projectile.Center, CultistPalette.FireMain.ToVector3() * 0.7f);
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            target.AddBuff(BuffID.OnFire, 180);
        }

        public override void OnKill(int timeLeft) {
            if (!VaultUtils.isServer) {
                CultistRenderHelper.ElementImpact(Projectile.Center, CultistElement.Fire, 1.1f);
                SoundEngine.PlaySound(SoundID.Item74 with { Volume = 0.5f, Pitch = -0.1f, MaxInstances = 5 }, Projectile.Center);
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

            CultistRenderHelper.BeginAdditive(sb);

            //速度拉伸焰尾（暗红外缘→亮芯）
            float speed = Projectile.velocity.Length();
            float stretch = MathHelper.Clamp(speed * 0.09f, 0.7f, 1.9f);
            Vector2 tailScale = new(stretch * 1.15f, 0.24f);
            sb.Draw(streak, drawPos, null, CultistPalette.FireDeep * 0.85f,
                Projectile.rotation + MathHelper.Pi, new Vector2(0, streak.Height / 2f), tailScale * 1.25f, SpriteEffects.None, 0f);
            sb.Draw(streak, drawPos, null, CultistPalette.FireMain * 0.9f,
                Projectile.rotation + MathHelper.Pi, new Vector2(0, streak.Height / 2f), tailScale, SpriteEffects.None, 0f);

            //残影链
            for (int i = 0; i < Projectile.oldPos.Length; i += 2) {
                if (Projectile.oldPos[i] == Vector2.Zero) {
                    continue;
                }
                float fade = 1f - i / (float)Projectile.oldPos.Length;
                Vector2 gp = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                sb.Draw(glow, gp, null, CultistPalette.FireMain * (0.3f * fade),
                    0f, glow.Size() / 2f, 0.4f * fade + 0.12f, SpriteEffects.None, 0f);
            }

            //弹头：外晕+亮芯
            sb.Draw(glow, drawPos, null, CultistPalette.FireMain * 0.95f,
                0f, glow.Size() / 2f, 0.62f, SpriteEffects.None, 0f);
            sb.Draw(glow, drawPos, null, CultistPalette.FireBright * 0.8f,
                0f, glow.Size() / 2f, 0.3f, SpriteEffects.None, 0f);

            CultistRenderHelper.EndAdditive(sb);
            return false;
        }
    }
}
