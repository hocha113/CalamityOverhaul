using CalamityOverhaul.Common;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.SummonWhips.Projectiles
{
    /// <summary>
    /// 皮鞭处决「皮革响鞭冲击」：单段全额爆（2.0x 鞭面板由生成方折算进 damage），
    /// 三相：聚拢白点、响鞭爆、震环余散。owner 生成真弹幕，全端可见可闻
    /// </summary>
    internal class GsWhipLeatherCrackProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        //鞍革棕金色板
        private static readonly Color LeatherBright = new(255, 230, 180);
        private static readonly Color LeatherMain = new(214, 154, 82);
        private static readonly Color LeatherDeep = new(120, 74, 38);

        private const int GatherFrames = 3;   //聚拢
        private const int CrackFrames = 5;    //爆窗
        private const int LifeFrames = 24;

        private int Elapsed => LifeFrames - Projectile.timeLeft;

        public override void SetDefaults() {
            Projectile.width = 120;
            Projectile.height = 120;
            Projectile.friendly = true;
            Projectile.tileCollide = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = LifeFrames;
            Projectile.DamageType = DamageClass.Summon;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
        }

        public override bool? CanDamage() => Elapsed >= GatherFrames && Elapsed < GatherFrames + CrackFrames ? null : false;

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
            => targetHitbox.Intersects(Utils.CenteredRectangle(Projectile.Center, new Vector2(120f)));

        public override void AI() {
            if (Elapsed == GatherFrames && !VaultUtils.isServer) {
                //响鞭爆帧：全端主音 + 迸溅
                SoundEngine.PlaySound(SoundID.Item153 with { Volume = 1f, Pitch = 0.05f }, Projectile.Center);
                for (int i = 0; i < 8; i++) {
                    PRTLoader.NewParticle<PRT_Spark>(Projectile.Center,
                        Main.rand.NextVector2Circular(6.5f, 5f),
                        i % 3 == 0 ? LeatherBright : LeatherMain,
                        Main.rand.NextFloat(0.32f, 0.55f))?.Configure(true, Main.rand.Next(14, 22));
                }
                PRTLoader.NewParticle<PRT_Light>(Projectile.Center, Vector2.Zero, LeatherBright, 0.18f)
                    ?.Configure(10, 0.8f);
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            float t = Elapsed / (float)LifeFrames;
            Vector2 pos = Projectile.Center - Main.screenPosition;
            Texture2D flare = CWRUtils.GetT2DAsset(CWRConstant.Masking + "StarFlare01")?.Value;
            if (Elapsed < GatherFrames) {
                //聚拢相：白点收缩
                if (flare != null) {
                    float g = 1f - Elapsed / (float)GatherFrames;
                    Main.EntitySpriteDraw(flare, pos, null, LeatherBright with { A = 0 } * 0.8f,
                        Projectile.identity * 0.7f, flare.Size() * 0.5f, 0.3f * g + 0.08f, SpriteEffects.None, 0);
                }
                return false;
            }
            //爆相与余散：震环外扩 + 中心闪衰减
            float burst = MathF.Min(1f, (Elapsed - GatherFrames) / (float)(LifeFrames - GatherFrames));
            float radius = MathHelper.Lerp(10f, 66f, 1f - (1f - burst) * (1f - burst));
            float alpha = 1f - burst;
            ShockRingDraw.Draw(Main.spriteBatch, Projectile.Center, radius, 12f - 6f * burst,
                LeatherBright, LeatherMain, LeatherDeep, alpha,
                squish: 1f, innerGlow: 0.3f, timeSeed: Projectile.identity * 0.31f);
            if (flare != null && burst < 0.5f) {
                float f = 1f - burst * 2f;
                Main.EntitySpriteDraw(flare, pos, null, LeatherBright with { A = 0 } * (0.9f * f),
                    -Projectile.identity * 0.4f, flare.Size() * 0.5f, 0.5f * f + 0.1f, SpriteEffects.None, 0);
            }
            return false;
        }
    }
}
