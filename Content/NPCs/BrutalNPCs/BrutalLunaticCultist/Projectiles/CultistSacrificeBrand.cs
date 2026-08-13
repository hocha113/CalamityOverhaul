using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Rendering;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.States;
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
    /// 献祭印记：首次打错分身的烙印警示，跟随玩家的红色仪式标记；
    /// 印记在身期间再次错击即触发献祭投技（服务端裁决在分身AI）；
    /// ai[0]=玩家索引 ai[1]=本体whoAmI；纯视觉无伤害
    /// </summary>
    internal class CultistSacrificeBrand : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private static readonly Color BrandRed = new(255, 70, 60);
        private static readonly Color BrandGold = new(255, 170, 90);

        private Player Target {
            get {
                int idx = (int)Projectile.ai[0];
                if (idx < 0 || idx >= Main.maxPlayers) {
                    return null;
                }
                Player player = Main.player[idx];
                return player.Alives() ? player : null;
            }
        }

        private NPC Boss {
            get {
                int idx = (int)Projectile.ai[1];
                if (idx < 0 || idx >= Main.maxNPCs) {
                    return null;
                }
                NPC boss = Main.npc[idx];
                return boss.active && boss.type == NPCID.CultistBoss ? boss : null;
            }
        }

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 40;
            Projectile.hostile = false;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.penetrate = -1;
            //时长与服务端印记计时同源常量，各端本地倒数（不改 timeLeft 防同步坑）
            Projectile.timeLeft = CultistSacrificeGrabState.BrandDuration;
            Projectile.netImportant = true;
        }

        public override void AI() {
            Player target = Target;
            NPC boss = Boss;
            if (target == null || boss == null) {
                Projectile.Kill();
                return;
            }

            //服务端权威消印：印记被消费（触发投技）或被清除时收掉
            if (!VaultUtils.isClient && boss.TryGetOverride(out CultistBossAI bossOverride)
                && bossOverride?.Context != null
                && bossOverride.Context.BrandTimers[(int)Projectile.ai[0]] <= 0) {
                Projectile.Kill();
                return;
            }

            //各端本地贴身跟随（玩家位置自身有同步）
            Projectile.Center = target.Center;
            Projectile.velocity = Vector2.Zero;

            if (VaultUtils.isServer) {
                return;
            }

            //烙印落身的一次性音画
            if (Projectile.localAI[0] == 0f) {
                Projectile.localAI[0] = 1f;
                SoundEngine.PlaySound(SoundID.Item104 with { Volume = 0.85f, Pitch = -0.4f }, target.Center);
                SoundEngine.PlaySound(SoundID.Zombie89 with { Volume = 0.8f, Pitch = 0.3f, MaxInstances = 3 }, target.Center);
                for (int i = 0; i < 10; i++) {
                    float angle = MathHelper.TwoPi * i / 10f;
                    PRTLoader.NewParticle<PRT_CultistRune>(target.Center + angle.ToRotationVector2() * 60f,
                        Vector2.Zero, BrandRed, 1.1f)?.Configure(target.Center, 0.16f, 22);
                }
            }

            //环绕符文：低频补充（预算友好）
            if (Main.rand.NextBool(5)) {
                float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                Vector2 pos = target.Center + angle.ToRotationVector2() * Main.rand.NextFloat(34f, 46f);
                PRTLoader.NewParticle<PRT_CultistEmber>(pos, -Vector2.UnitY * Main.rand.NextFloat(0.4f, 1.1f),
                    BrandRed, Main.rand.NextFloat(0.5f, 0.8f))?.Configure(Main.rand.Next(12, 20));
            }

            Lighting.AddLight(target.Center, BrandRed.ToVector3() * 0.5f);
        }

        public override bool PreDraw(ref Color lightColor) {
            Player target = Target;
            if (target == null) {
                return false;
            }

            SpriteBatch sb = Main.spriteBatch;
            //尾声急闪：剩 180 帧内呼吸频率翻倍（印记快过期的可读提示）
            float pulseSpeed = Projectile.timeLeft < 180 ? 16f : 7f;
            float pulse = 0.5f + 0.5f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * pulseSpeed);

            CultistRenderHelper.BeginAdditive(sb);
            Texture2D ring = CWRAsset.DiffusionCircle.Value;
            Texture2D glow = CWRAsset.SoftGlow.Value;
            Vector2 feet = target.Bottom + new Vector2(0f, 8f) - Main.screenPosition;
            Vector2 head = target.Top + new Vector2(0f, -22f) - Main.screenPosition;
            float ringHalf = ring.Width * 0.5f;

            //足下小仪式环：双环反旋
            sb.Draw(ring, feet, null, BrandRed * (0.4f + 0.25f * pulse), Main.GlobalTimeWrappedHourly * 2.6f,
                ring.Size() / 2f, 44f / ringHalf, SpriteEffects.None, 0f);
            sb.Draw(ring, feet, null, BrandGold * (0.28f + 0.18f * pulse), -Main.GlobalTimeWrappedHourly * 1.9f,
                ring.Size() / 2f, 58f / ringHalf, SpriteEffects.None, 0f);

            //头顶印记点：被仪式盯上的"祭品编号"
            sb.Draw(glow, head, null, BrandRed * (0.55f + 0.35f * pulse), 0f, glow.Size() / 2f, 0.3f, SpriteEffects.None, 0f);
            sb.Draw(glow, head, null, BrandGold * (0.35f * pulse), 0f, glow.Size() / 2f, 0.16f, SpriteEffects.None, 0f);
            CultistRenderHelper.EndAdditive(sb);
            return false;
        }
    }
}
