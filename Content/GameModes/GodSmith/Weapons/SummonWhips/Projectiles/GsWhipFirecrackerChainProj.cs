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
    /// 火鞭连环爆单元：ai[0] 传起爆延迟帧，到点炸出 90px 火团（1.0x 鞭面板）。
    /// 三枚错位错时组成连环，逐爆推进爆竹节奏
    /// </summary>
    internal class GsWhipFirecrackerChainProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private static readonly Color FireBright = new(255, 224, 150);
        private static readonly Color FireMain = new(255, 110, 40);
        private static readonly Color FireDeep = new(150, 44, 20);

        private const int BoomWindow = 4;
        private const int LifeFrames = 52;

        private int Elapsed => LifeFrames - Projectile.timeLeft;

        private int Delay => (int)Projectile.ai[0];

        public override void SetDefaults() {
            Projectile.width = 180;
            Projectile.height = 180;
            Projectile.friendly = true;
            Projectile.tileCollide = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = LifeFrames;
            Projectile.DamageType = DamageClass.Summon;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
        }

        public override bool? CanDamage() => Elapsed >= Delay && Elapsed < Delay + BoomWindow ? null : false;

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
            => targetHitbox.Intersects(Utils.CenteredRectangle(Projectile.Center, new Vector2(180f)));

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
            => target.AddBuff(BuffID.OnFire, 180);

        public override void AI() {
            int elapsed = Elapsed;
            if (VaultUtils.isServer) {
                return;
            }
            //引信期：滋滋火花
            if (elapsed < Delay && Main.GameUpdateCount % 4 == 0) {
                PRTLoader.NewParticle<PRT_Spark>(
                    Projectile.Center + Main.rand.NextVector2Circular(6f, 6f),
                    -Vector2.UnitY * Main.rand.NextFloat(0.6f, 1.4f),
                    FireBright, Main.rand.NextFloat(0.2f, 0.32f))?.Configure(false, Main.rand.Next(8, 12));
            }
            //起爆帧：全端主音 + 火团迸溅
            if (elapsed == Delay) {
                SoundEngine.PlaySound(SoundID.Item14 with { Volume = 0.85f, Pitch = 0.15f }, Projectile.Center);
                PRTLoader.NewParticle<PRT_MechExplosion>(Projectile.Center, Vector2.Zero,
                    FireMain, Main.rand.NextFloat(0.7f, 0.9f));
                for (int i = 0; i < 6; i++) {
                    PRTLoader.NewParticle<PRT_HellFire>(Projectile.Center,
                        Main.rand.NextVector2Circular(5f, 5f) - Vector2.UnitY * 1.5f,
                        FireMain, Main.rand.NextFloat(0.5f, 0.9f));
                }
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            int elapsed = Elapsed;
            Texture2D flash = CWRUtils.GetT2DAsset(CWRConstant.Masking + "Flashimpact")?.Value;
            if (flash == null) {
                return false;
            }
            Vector2 pos = Projectile.Center - Main.screenPosition;
            float seed = Projectile.identity * 0.83f;
            if (elapsed < Delay) {
                //引信读数：微光呼吸渐快
                float urgency = elapsed / MathF.Max(1f, Delay);
                float flicker = 0.5f + 0.5f * MathF.Sin(elapsed * (0.5f + urgency) + seed);
                Main.EntitySpriteDraw(flash, pos, null, FireMain with { A = 0 } * (0.3f * flicker),
                    seed, flash.Size() * 0.5f, 0.1f + 0.05f * urgency, SpriteEffects.None, 0);
                return false;
            }
            //爆闪与余晖
            float t = MathHelper.Clamp((elapsed - Delay) / (float)(LifeFrames - Delay), 0f, 1f);
            float fade = 1f - t;
            float grow = 0.28f + 0.5f * (1f - fade * fade);
            Main.EntitySpriteDraw(flash, pos, null, FireBright with { A = 0 } * (0.85f * fade),
                seed + t * 0.6f, flash.Size() * 0.5f, grow, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(flash, pos, null, FireDeep with { A = 0 } * (0.5f * fade),
                -seed, flash.Size() * 0.5f, grow * 1.4f, SpriteEffects.None, 0);
            return false;
        }
    }
}
