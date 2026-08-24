using CalamityOverhaul.Common;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;
using static CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaServants.KikasaArms.KikasaArmsPalette;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaServants.KikasaArms.KikasaBows
{
    /// <summary>
    /// 湖水箭：弓奴的箭矢。血水凝成的细长镖体，速度拉伸承载动感，
    /// 尾迹滴珠、落点溅珠。ai0 = 弹型（0 平箭 / 1 贯穿重箭 / 2 雨箭），
    /// 生成包自含，远端首个本地更新按 ai 自配
    /// </summary>
    internal class KikasaBowArrow : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private ref float ArrowMode => ref Projectile.ai[0];

        /// <summary>飞行计数（本地）：重力起始与尾迹节拍用</summary>
        private ref float FlightTime => ref Projectile.localAI[0];

        private bool ModeApplied { get => Projectile.localAI[1] > 0f; set => Projectile.localAI[1] = value ? 1f : 0f; }

        private bool Heavy => (int)ArrowMode == 1;
        private bool Rain => (int)ArrowMode == 2;

        public override void SetDefaults() {
            Projectile.width = 10;
            Projectile.height = 10;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Summon;
            Projectile.penetrate = 2;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 240;
            Projectile.extraUpdates = 1;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 14;
        }

        public override void AI() {
            //远端也走同一初始化：ai0 随生成包到位
            if (!ModeApplied) {
                ModeApplied = true;
                if (Heavy) {
                    Projectile.penetrate = 5;
                    Projectile.scale = 1.25f;
                }
                else if (Rain) {
                    Projectile.penetrate = 1;
                }
            }
            FlightTime++;

            //重力：平箭后段坠弧，雨箭全程坠，重箭几乎直线
            float grav = Rain ? 0.11f : Heavy ? 0.012f : FlightTime > 44f ? 0.05f : 0f;
            if (grav > 0f) {
                Projectile.velocity.Y = MathF.Min(Projectile.velocity.Y + grav, 19f);
            }
            Projectile.rotation = Projectile.velocity.ToRotation();

            //尾迹滴珠：客户端隔拍甩
            if (!Main.dedServ && FlightTime % 5f == 0f && Main.rand.NextBool(2)) {
                PRTLoader.NewParticle<PRT_GhostRainDrop>(
                    Projectile.Center - Projectile.velocity * 0.4f,
                    -Projectile.velocity * 0.06f + Main.rand.NextVector2Circular(0.5f, 0.5f),
                    BloodMain * Main.rand.NextFloat(0.35f, 0.5f),
                    Main.rand.NextFloat(0.24f, 0.4f) * (Heavy ? 1.3f : 1f))
                    ?.Configure(Main.rand.Next(10, 18), 0f);
            }

            float glow = Heavy ? 0.42f : 0.24f;
            Lighting.AddLight(Projectile.Center, 0.9f * glow, 0.28f * glow, 0.24f * glow);
        }

        public override void OnKill(int timeLeft) {
            if (Main.dedServ) {
                return;
            }
            SoundEngine.PlaySound(SoundID.Drip with {
                Volume = 0.3f, Pitch = 0.2f, MaxInstances = 3
            }, Projectile.Center);
            int burst = Heavy ? 8 : 5;
            for (int k = 0; k < burst; k++) {
                PRTLoader.NewParticle<PRT_GhostRainDrop>(Projectile.Center,
                    -Projectile.velocity.SafeNormalize(Vector2.UnitY).RotatedBy(Main.rand.NextFloat(-0.8f, 0.8f))
                        * Main.rand.NextFloat(1.5f, Heavy ? 5f : 3.5f),
                    BloodMain * Main.rand.NextFloat(0.4f, 0.6f),
                    Main.rand.NextFloat(0.3f, 0.5f))
                    ?.Configure(Main.rand.Next(12, 22), Main.rand.NextFloat(-0.3f, 0.3f));
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (glow == null) {
                return false;
            }
            SpriteBatch sb = Main.spriteBatch;
            Vector2 pos = Projectile.Center - Main.screenPosition;
            Vector2 origin = glow.Size() * 0.5f;
            float rot = Projectile.rotation;
            float len = (Heavy ? 46f : 34f) * Projectile.scale;
            float wid = (Heavy ? 7.5f : 5f) * Projectile.scale;
            float fade = MathHelper.Clamp(FlightTime / 6f, 0f, 1f);

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

            //暗血底衬、主体、亮芯三层：镖体沿速度拉伸
            sb.Draw(glow, pos, null, BloodDeep * (0.5f * fade), rot, origin,
                new Vector2(len * 1.25f / glow.Width * 2f, wid * 1.7f / glow.Height * 2f), SpriteEffects.None, 0f);
            sb.Draw(glow, pos, null, BloodMain * (0.85f * fade), rot, origin,
                new Vector2(len / glow.Width * 2f, wid / glow.Height * 2f), SpriteEffects.None, 0f);
            sb.Draw(glow, pos + Projectile.velocity.SafeNormalize(Vector2.Zero) * len * 0.22f, null,
                BloodBright * (0.7f * fade), rot, origin,
                new Vector2(len * 0.45f / glow.Width * 2f, wid * 0.55f / glow.Height * 2f), SpriteEffects.None, 0f);

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
            return false;
        }
    }
}
