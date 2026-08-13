using CalamityOverhaul.Common;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.ScrapCommanders.Projectiles
{
    /// <summary>
    /// 熔断全械的自主工具：脱链的工具各占玩家一个方位，
    /// 按各自错拍做"收劲 → 直线突贯 → 刹车归位"的独立猎杀循环，
    /// 寿命尽头飞回统帅归位。伤害窗只在突贯速度上（公平阀）。
    /// ai[0]=工具号（贴图与错拍），ai[1]=统帅 whoAmI
    /// </summary>
    internal class ScrapAutonomousTool : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private const int LifeFrames = 216;
        /// <summary>猎杀循环长度：悬位 26 + 收劲 14 + 突贯 12 + 刹车 18</summary>
        private const int Cycle = 70;

        private int ToolIndex => (int)Projectile.ai[0];
        private NPC Boss => Main.npc[(int)Projectile.ai[1]];
        private ref float LocalTimer => ref Projectile.localAI[0];
        private Vector2 dashAim = Vector2.UnitX;
        private bool returning;

        public override void SetDefaults() {
            Projectile.width = 42;
            Projectile.height = 42;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.aiStyle = -1;
            Projectile.timeLeft = LifeFrames;
        }

        /// <summary>伤害窗对齐可见冲势</summary>
        public override bool? CanDamage() => Projectile.velocity.Length() > 17f ? null : false;

        public override void AI() {
            NPC boss = Boss;
            if (boss == null || !boss.active) {
                Projectile.Kill();
                return;
            }

            //寿命末段：飞回统帅归位
            if (Projectile.timeLeft < 34 || returning) {
                returning = true;
                Vector2 home = boss.Center;
                Vector2 want = (home - Projectile.Center).SafeNormalize(Vector2.UnitY) * 21f;
                Projectile.velocity = Vector2.Lerp(Projectile.velocity, want, 0.12f);
                Projectile.rotation += 0.2f;
                if (Vector2.Distance(Projectile.Center, home) < 80f) {
                    SoundEngine.PlaySound(SoundID.Item37 with { Volume = 0.5f, Pitch = 0.2f, MaxInstances = 3 }, home);
                    Projectile.Kill();
                }
                return;
            }

            LocalTimer++;
            Player target = Main.player[Player.FindClosest(Projectile.position, Projectile.width, Projectile.height)];
            if (!target.Alives()) {
                Projectile.velocity *= 0.94f;
                return;
            }

            //错拍猎杀循环：每件工具差 17 帧起手
            int local = ((int)LocalTimer + ToolIndex * 17) % Cycle;
            //方位悬点：四件工具各占一个象限并缓转
            float slotAng = ToolIndex * MathHelper.PiOver2 + LocalTimer * 0.014f;
            Vector2 hoverAnchor = target.Center + slotAng.ToRotationVector2() * 310f;

            if (local < 26) {
                //悬位游弋
                Vector2 to = hoverAnchor - Projectile.Center;
                Projectile.velocity = Vector2.Lerp(Projectile.velocity, to * 0.05f, 0.1f);
                if (Projectile.velocity.Length() > 12f) {
                    Projectile.velocity = Projectile.velocity.SafeNormalize(Vector2.Zero) * 12f;
                }
                Projectile.rotation = Projectile.rotation.AngleLerp(
                    (target.Center - Projectile.Center).ToRotation() - MathHelper.PiOver2, 0.1f);
            }
            else if (local < 40) {
                //收劲：晚爆式后拉
                dashAim = (target.Center + target.velocity * 7f - Projectile.Center).SafeNormalize(Vector2.UnitX);
                float k = MathF.Pow((local - 26) / 14f, 5f);
                Projectile.velocity = Vector2.Lerp(Projectile.velocity, -dashAim * (9f * k), 0.25f);
                Projectile.rotation = Projectile.rotation.AngleLerp(dashAim.ToRotation() - MathHelper.PiOver2, 0.3f);
                if (local == 39) {
                    SoundEngine.PlaySound(SoundID.Item22 with { Volume = 0.4f, Pitch = 0.35f, MaxInstances = 3 }, Projectile.Center);
                }
            }
            else if (local < 52) {
                //直线突贯
                if (local == 40) {
                    Projectile.velocity = dashAim * 25f;
                    SoundEngine.PlaySound(SoundID.DD2_WyvernDiveDown with { Volume = 0.45f, Pitch = 0.3f, MaxInstances = 3 }, Projectile.Center);
                }
                Projectile.velocity *= 1.012f;
                Projectile.rotation += 0.32f * MathF.Sign(Projectile.velocity.X);
                if (!Main.dedServ && local % 2 == 0) {
                    PRTLoader.NewParticle<PRT_Spark>(Projectile.Center, -Projectile.velocity * 0.12f,
                        new Color(255, 150, 58) * 0.7f, Main.rand.NextFloat(0.4f, 0.7f))
                        ?.Configure(false, Main.rand.Next(7, 11));
                }
            }
            else {
                //刹车归位
                Projectile.velocity *= 0.86f;
                Projectile.rotation = Projectile.rotation.AngleLerp(
                    (target.Center - Projectile.Center).ToRotation() - MathHelper.PiOver2, 0.12f);
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            int npcType = ScrapCommander.ArmNpcType(ToolIndex);
            Main.instance.LoadNPC(npcType);
            Texture2D tex = TextureAssets.Npc[npcType]?.Value;
            if (tex == null) {
                return false;
            }
            int frameH = tex.Height / Main.npcFrameCount[npcType];
            Rectangle frame = new(0, 0, tex.Width, frameH);

            //突贯热涂抹（预乘批加色技巧）
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (glow != null && Projectile.velocity.Length() > 16f) {
                for (int j = 1; j <= 2; j++) {
                    Main.spriteBatch.Draw(glow,
                        Projectile.Center - Projectile.velocity * (j * 1.6f) - Main.screenPosition, null,
                        new Color(255, 130, 48, 0) * (0.34f / j), 0f, glow.Size() * 0.5f,
                        new Vector2(50f / glow.Width * 2f), SpriteEffects.None, 0f);
                }
            }

            Color tint = lightColor.MultiplyRGB(ScrapCommander.RustMul);
            Main.EntitySpriteDraw(tex, Projectile.Center - Main.screenPosition, frame, tint,
                Projectile.rotation, frame.Size() * 0.5f, 0.9f, SpriteEffects.None, 0);
            //过载焊光
            if (glow != null) {
                Main.spriteBatch.Draw(glow, Projectile.Center - Main.screenPosition, null,
                    new Color(255, 150, 58, 0) * 0.3f, 0f, glow.Size() * 0.5f,
                    new Vector2(26f * 2f / glow.Width), SpriteEffects.None, 0f);
            }
            return false;
        }

        public override void OnKill(int timeLeft) {
            ScrapVfx.HitSparks(Projectile.Center, Vector2.UnitY, 0.9f);
        }
    }
}
