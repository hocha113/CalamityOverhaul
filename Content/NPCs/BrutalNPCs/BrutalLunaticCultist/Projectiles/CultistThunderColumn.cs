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
    /// 雷枢天柱：地标预警→天雷贯落→残响消散；锚点在地面；
    /// ai[0]=预警帧 ai[1]=柱高px（默认1400）
    /// </summary>
    internal class CultistThunderColumn : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder2;

        private const int StrikeWindow = 16;
        private const int FadeTime = 26;
        private const float HitWidth = 76f;

        private int TelegraphTime => Math.Max((int)Projectile.ai[0], 20);
        private float ColumnHeight => Projectile.ai[1] > 0f ? Projectile.ai[1] : 1400f;

        private float Timer => Projectile.localAI[0];
        private bool Struck => Timer > TelegraphTime;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 30;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 300;
            CooldownSlot = ImmunityCooldownID.Bosses;
        }

        public override void AI() {
            Projectile.localAI[0]++;
            Projectile.velocity = Vector2.Zero;

            //首帧缓存伤害并按预警参数定寿命（各端确定性）
            if ((int)Timer == 1) {
                Projectile.localAI[1] = Projectile.damage;
                Projectile.timeLeft = TelegraphTime + StrikeWindow + FadeTime;
            }
            Projectile.damage = Struck && Timer <= TelegraphTime + StrikeWindow ? (int)Projectile.localAI[1] : 0;

            if ((int)Timer == 1 && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item93 with { Volume = 0.5f, Pitch = 0.4f, MaxInstances = 6 }, Projectile.Center);
            }

            //预警期地面电花攀升
            if (!Struck && !VaultUtils.isServer && Main.rand.NextBool(3)) {
                PRTLoader.NewParticle<PRT_CultistVolt>(
                    Projectile.Center + new Vector2(Main.rand.NextFloat(-30f, 30f), 0f),
                    -Vector2.UnitY * Main.rand.NextFloat(2f, 6f),
                    CultistPalette.ThunderBright, Main.rand.NextFloat(0.6f, 1f))?.Configure(Main.rand.Next(10, 18));
            }

            //贯落帧
            if ((int)Timer == TelegraphTime + 1 && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Thunder with { Volume = 0.95f, Pitch = 0.15f, MaxInstances = 4 }, Projectile.Center);
                CultistScreenFX.Punch(Projectile.Center, 6f, 14, "CultistThunder", Vector2.UnitY);
                CultistRenderHelper.ElementImpact(Projectile.Center, CultistElement.Thunder, 1.5f);
                for (int i = 0; i < 10; i++) {
                    PRTLoader.NewParticle<PRT_CultistVolt>(
                        Projectile.Center + new Vector2(0f, -Main.rand.NextFloat(ColumnHeight * 0.8f)),
                        Main.rand.NextVector2Circular(4f, 1.5f),
                        CultistPalette.ThunderBright, Main.rand.NextFloat(0.8f, 1.4f))?.Configure(Main.rand.Next(12, 22));
                }
            }

            if (Struck) {
                Lighting.AddLight(Projectile.Center - new Vector2(0f, 300f), CultistPalette.ThunderMain.ToVector3() * 1.4f);
            }
            Lighting.AddLight(Projectile.Center, CultistPalette.ThunderMain.ToVector3() * 0.6f);
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            if (!Struck || Timer > TelegraphTime + StrikeWindow) {
                return false;
            }
            //竖直贯柱碰撞
            Rectangle column = new(
                (int)(Projectile.Center.X - HitWidth / 2f),
                (int)(Projectile.Center.Y - ColumnHeight),
                (int)HitWidth,
                (int)ColumnHeight + 20);
            return column.Intersects(targetHitbox);
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            target.AddBuff(BuffID.Electrified, 45);
        }

        public override bool PreDraw(ref Color lightColor) {
            SpriteBatch sb = Main.spriteBatch;
            Vector2 groundPos = Projectile.Center - Main.screenPosition;
            Texture2D beam = CultistRenderHelper.LightBeam?.Value;
            Texture2D line = CWRAsset.LightShot.Value;
            Texture2D glow = CWRAsset.SoftGlow.Value;
            Texture2D bolt = CWRAsset.ThunderTrail.Value;

            CultistRenderHelper.BeginAdditive(sb);

            if (!Struck) {
                //预警：细线+地面收缩环
                float t = Timer / TelegraphTime;
                float warn = 0.3f + 0.5f * t + 0.2f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 20f);
                sb.Draw(line, groundPos, null, CultistPalette.ThunderMain * (0.4f * warn),
                    -MathHelper.PiOver2, new Vector2(0f, line.Height / 2f),
                    new Vector2(ColumnHeight / line.Width, 0.1f + 0.12f * t), SpriteEffects.None, 0f);
                //地面警示光点收缩
                float ringScale = MathHelper.Lerp(1.5f, 0.45f, t);
                sb.Draw(glow, groundPos, null, CultistPalette.ThunderBright * (0.5f * t + 0.2f),
                    0f, glow.Size() / 2f, ringScale, SpriteEffects.None, 0f);
            }
            else {
                float sincePeak = Timer - TelegraphTime;
                float total = StrikeWindow + FadeTime;
                float fade = 1f - MathHelper.Clamp(sincePeak / total, 0f, 1f);
                float coreW = MathHelper.Lerp(0.65f, 0.1f, sincePeak / total);

                //白热主柱
                if (beam != null) {
                    sb.Draw(beam, groundPos, null, Color.White * (0.9f * fade),
                        -MathHelper.PiOver2, new Vector2(0f, beam.Height / 2f),
                        new Vector2(ColumnHeight / beam.Width, coreW), SpriteEffects.None, 0f);
                    sb.Draw(beam, groundPos, null, CultistPalette.ThunderMain * (0.75f * fade),
                        -MathHelper.PiOver2, new Vector2(0f, beam.Height / 2f),
                        new Vector2(ColumnHeight / beam.Width, coreW * 2.2f), SpriteEffects.None, 0f);
                }

                //边缘电弧抖丝（纯表现随机）
                int arcs = fade > 0.5f ? 3 : 1;
                for (int i = 0; i < arcs; i++) {
                    float ox = Main.rand.NextFloat(-26f, 26f);
                    float segY = Main.rand.NextFloat(0.15f, 0.95f);
                    sb.Draw(bolt, groundPos + new Vector2(ox, -ColumnHeight * segY), null,
                        CultistPalette.ThunderBright * (0.6f * fade),
                        -MathHelper.PiOver2 + Main.rand.NextFloat(-0.2f, 0.2f), bolt.Size() / 2f,
                        new Vector2(1.2f, 0.5f), SpriteEffects.None, 0f);
                }

                //落点光爆
                sb.Draw(glow, groundPos, null, CultistPalette.ThunderBright * fade,
                    0f, glow.Size() / 2f, 1.3f * fade + 0.3f, SpriteEffects.None, 0f);
            }

            CultistRenderHelper.EndAdditive(sb);
            return false;
        }
    }
}
