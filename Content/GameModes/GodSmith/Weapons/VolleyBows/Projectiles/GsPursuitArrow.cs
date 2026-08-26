using CalamityOverhaul.Content.GameModes.UI;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.VolleyBows.Projectiles
{
    /// <summary>
    /// 猎标追击箭：每 8°/帧咬向标记目标的镀金箭。
    /// ai[0] = 目标 NPC whoAmI（生成端选定，whoAmI 跨端一致，各端转向确定性同步）。
    /// Misc 源生成，不进路由打标流，完全自治
    /// </summary>
    internal class GsPursuitArrow : ModProjectile
    {
        public override string Texture => $"Terraria/Images/Projectile_{ProjectileID.WoodenArrowFriendly}";

        private ref float TargetIndex => ref Projectile.ai[0];

        private ref float Life => ref Projectile.localAI[0];

        public override void SetDefaults() {
            Projectile.width = 10;
            Projectile.height = 10;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 240;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
            Projectile.arrow = true;
        }

        public override void AI() {
            Life++;
            int idx = (int)TargetIndex;
            NPC target = idx >= 0 && idx < Main.maxNPCs ? Main.npc[idx] : null;
            //前 6 帧直飞拉开距离再咬弯，读作「分裂而出」
            if (Life > 6f && target != null && target.active && GsHuntMarkNPC.CanMark(target)) {
                float current = Projectile.velocity.ToRotation();
                float desired = (target.Center - Projectile.Center).ToRotation();
                float turned = current.AngleTowards(desired, MathHelper.ToRadians(8f));
                Projectile.velocity = turned.ToRotationVector2() * Projectile.velocity.Length();
            }
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;

            if (!VaultUtils.isServer && Life % 3 == 0) {
                PRTLoader.NewParticle<PRT_Light>(
                    Projectile.Center - Projectile.velocity * 0.6f,
                    -Projectile.velocity * 0.04f,
                    GameModeTheme.GodSmithAccent, 0.08f)?.Configure(8, 0.8f);
            }
            Lighting.AddLight(Projectile.Center, GameModeTheme.GodSmithAccent.ToVector3() * 0.16f);
        }

        public override bool PreDraw(ref Color lightColor) {
            //镀金速度重影垫底，本体照常绘制
            Main.instance.LoadProjectile(Projectile.type);
            var tex = Terraria.GameContent.TextureAssets.Projectile[Projectile.type].Value;
            Color glow = GameModeTheme.GodSmithEmber with { A = 0 };
            float pulse = 0.8f + 0.2f * MathF.Sin(Main.GlobalTimeWrappedHourly * 9f + Projectile.identity * 0.7f);
            for (int i = 1; i <= 3; i++) {
                Main.EntitySpriteDraw(tex, Projectile.Center - Projectile.velocity * (0.5f * i) - Main.screenPosition,
                    null, glow * (0.3f * pulse / i), Projectile.rotation, tex.Size() * 0.5f, 1f,
                    Microsoft.Xna.Framework.Graphics.SpriteEffects.None, 0);
            }
            return true;
        }

        public override void OnKill(int timeLeft) {
            if (VaultUtils.isServer) {
                return;
            }
            for (int i = 0; i < 4; i++) {
                PRTLoader.NewParticle<PRT_Spark>(Projectile.Center,
                    Main.rand.NextVector2Unit() * Main.rand.NextFloat(1.5f, 4f),
                    Main.rand.NextBool() ? GameModeTheme.GodSmithAccent : GameModeTheme.GodSmithEmber,
                    Main.rand.NextFloat(0.25f, 0.42f))?.Configure(true, Main.rand.Next(12, 20));
            }
        }
    }
}
