using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalPlantera.Projectiles
{
    /// <summary>猛扑/鞭刑预警线：ai[0]=角度 ai[1]=时长 ai[2]=长度；末20%白热</summary>
    internal class PlanteraTelegraphLine : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder2;

        private float LineRotation => Projectile.ai[0];
        private int Duration => (int)Math.Max(Projectile.ai[1], 1f);
        private float LineLength => Projectile.ai[2] > 0f ? Projectile.ai[2] : 2200f;
        private float Progress => MathHelper.Clamp(1f - Projectile.timeLeft / (float)Duration, 0f, 1f);

        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 2600;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 2;
            Projectile.hostile = false;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 60;
            Projectile.netImportant = true;
        }

        public override bool? CanDamage() => false;

        public override void AI() {
            Lighting.AddLight(Projectile.Center, new Vector3(0.25f, 0.5f, 0.15f) * (0.4f + 0.6f * Progress));
        }

        /// <summary>生成一条预警线，服务端裁决</summary>
        internal static void Spawn(NPC owner, Vector2 center, float rotation, int duration, float length = 2200f) {
            if (VaultUtils.isClient) {
                return;
            }
            int id = Projectile.NewProjectile(owner.GetSource_FromAI(), center, Vector2.Zero,
                ModContent.ProjectileType<PlanteraTelegraphLine>(), 0, 0f, Main.myPlayer,
                rotation, duration, length);
            if (id >= 0 && id < Main.maxProjectiles) {
                Main.projectile[id].timeLeft = duration;
                Main.projectile[id].netUpdate = true;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D line = CWRAsset.MaskLaserLine.Value;
            Texture2D glow = CWRAsset.SoftGlow.Value;
            Vector2 origin = new(0f, line.Height / 2f);
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            float progress = Progress;
            float pulse = 0.85f + 0.15f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 9f + Projectile.whoAmI * 0.7f);

            //末20%白热提示起跳
            float flash = MathHelper.Clamp((progress - 0.8f) / 0.2f, 0f, 1f);
            Color baseCol = Color.Lerp(new Color(110, 230, 70), new Color(220, 255, 170), flash) with { A = 0 };
            Color hotCol = Color.Lerp(new Color(190, 255, 120), Color.White, flash) with { A = 0 };

            float lenScale = LineLength / line.Width;

            //基线
            Main.EntitySpriteDraw(line, drawPos, null, baseCol * (0.45f * pulse),
                LineRotation, origin, new Vector2(lenScale, 0.2f), SpriteEffects.None, 0);

            //充能段追进
            if (progress > 0.02f) {
                Rectangle chargeSrc = new(0, 0, (int)(line.Width * progress), line.Height);
                Main.EntitySpriteDraw(line, drawPos, chargeSrc, hotCol * 0.85f,
                    LineRotation, origin, new Vector2(lenScale, 0.4f * pulse), SpriteEffects.None, 0);
            }

            //前端光点+根部辉光
            Vector2 tip = drawPos + LineRotation.ToRotationVector2() * LineLength * progress;
            Main.EntitySpriteDraw(glow, tip, null, hotCol * 0.85f,
                0f, glow.Size() / 2f, 0.5f + 0.3f * flash, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(glow, drawPos, null, baseCol * 0.7f,
                0f, glow.Size() / 2f, 0.65f * pulse, SpriteEffects.None, 0);

            return false;
        }

        public override void DrawBehind(int index, List<int> behindNPCsAndTiles, List<int> behindNPCs,
            List<int> behindProjectiles, List<int> overPlayers, List<int> overWiresUI) => behindNPCsAndTiles.Add(index);
    }
}
