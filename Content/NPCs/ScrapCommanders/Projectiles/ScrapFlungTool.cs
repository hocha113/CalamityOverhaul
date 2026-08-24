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
    /// 甩壳飞掷工具（转阶段一次性弹幕）：外抛减速 → 悬停亮预警线 →
    /// 直线扑向玩家，撞地炸火星。ai[0]=工具号（决定贴图）
    /// </summary>
    internal class ScrapFlungTool : ScrapModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        //==================== 时序：外抛[0,26) 预警[26,44) 突进[44,...] ====================

        private const int AimBeat = 26;
        private const int DashBeat = 44;

        private int ToolIndex => (int)Projectile.ai[0];
        private ref float StateTimer => ref Projectile.localAI[0];

        private Vector2 dashAim = Vector2.UnitX;
        private bool dashed;

        public override void SetDefaults() {
            Projectile.width = 40;
            Projectile.height = 40;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.aiStyle = -1;
            Projectile.timeLeft = 240;
        }

        /// <summary>伤害窗只开在突进段：外抛与悬停亮线阶段是演出不是攻击</summary>
        public override bool? CanDamage() => dashed ? null : false;

        public override void AI() {
            StateTimer++;
            int t = (int)StateTimer;

            if (t < AimBeat) {
                //外抛减速翻滚
                Projectile.velocity *= 0.94f;
                Projectile.rotation += 0.24f + Projectile.velocity.Length() * 0.02f;
                return;
            }

            if (t < DashBeat) {
                //悬停亮线：咬住目标方向
                Projectile.velocity *= 0.86f;
                Player target = Main.player[Player.FindClosest(Projectile.position, Projectile.width, Projectile.height)];
                if (target.Alives()) {
                    dashAim = (target.Center + target.velocity * 8f - Projectile.Center)
                        .SafeNormalize(Vector2.UnitY);
                }
                Projectile.rotation = Projectile.rotation.AngleLerp(dashAim.ToRotation() - MathHelper.PiOver2, 0.2f);
                return;
            }

            if (!dashed) {
                dashed = true;
                Projectile.velocity = dashAim * 23f;
                Projectile.tileCollide = true;
                SoundEngine.PlaySound(SoundID.DD2_WyvernDiveDown with { Volume = 0.5f, Pitch = 0.2f, MaxInstances = 3 }, Projectile.Center);
            }
            //突进：微加速 + 速度火星
            Projectile.velocity *= 1.01f;
            Projectile.rotation += 0.3f * MathF.Sign(Projectile.velocity.X);
            if (!Main.dedServ && t % 3 == 0) {
                PRTLoader.NewParticle<PRT_Spark>(Projectile.Center, -Projectile.velocity * 0.1f,
                    new Color(255, 150, 58) * 0.6f, Main.rand.NextFloat(0.4f, 0.7f))
                    ?.Configure(false, Main.rand.Next(8, 12));
            }
        }

        public override void OnKill(int timeLeft) {
            if (Main.dedServ) {
                return;
            }
            SoundEngine.PlaySound(SoundID.NPCHit4 with { Volume = 0.5f, Pitch = -0.3f, MaxInstances = 3 }, Projectile.Center);
            for (int i = 0; i < 8; i++) {
                PRTLoader.NewParticle<PRT_Spark>(Projectile.Center + Main.rand.NextVector2Circular(12f, 12f),
                    Main.rand.NextVector2Circular(4f, 4f),
                    Color.Lerp(new Color(255, 150, 58), Color.White, Main.rand.NextFloat(0.4f)),
                    Main.rand.NextFloat(0.5f, 0.9f))?.Configure(true, Main.rand.Next(10, 16));
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
            Color tint = lightColor.MultiplyRGB(ScrapCommander.RustMul);
            Main.EntitySpriteDraw(tex, Projectile.Center - Main.screenPosition, frame, tint,
                Projectile.rotation, frame.Size() * 0.5f, 0.85f, SpriteEffects.None, 0);

            //预警线：悬停段亮起，越接近突进越亮
            int t = (int)StateTimer;
            if (t >= AimBeat && t < DashBeat) {
                Texture2D glow = CWRAsset.SoftGlow?.Value;
                if (glow != null) {
                    float k = (t - AimBeat) / (float)(DashBeat - AimBeat);
                    float blink = 0.7f + 0.3f * MathF.Sin(Main.GlobalTimeWrappedHourly * 30f);
                    const float lineLen = 900f;
                    Main.spriteBatch.Draw(glow,
                        Projectile.Center + dashAim * lineLen * 0.5f - Main.screenPosition, null,
                        new Color(255, 64, 46, 0) * (0.32f * k * blink), dashAim.ToRotation(),
                        glow.Size() * 0.5f,
                        new Vector2(lineLen * 2f / glow.Width, 6f / glow.Height), SpriteEffects.None, 0f);
                }
            }
            return false;
        }
    }
}
