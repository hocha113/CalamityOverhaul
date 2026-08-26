using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Magic.Elysiums.Serpents;
using CalamityOverhaul.Content.Projectiles;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Magic.Elysiums
{
    /// <summary>天国极乐手持权杖：按住左键蓄力化蛇术，松开释放圣光波潮</summary>
    internal class ElysiumHeld : BaseHeldGun
    {
        public override string Texture => CWRConstant.Item_Magic + "Elysium";
        public override int TargetID => ModContent.ItemType<Elysium>();

        //蓄力节点(帧)
        private const int MinCharge = 26;
        private const int FullCharge = 118;
        private const int BurstTime = 26;

        //色板：暖金/亮金/圣白
        private static readonly Vector3 WarmGold = new(1f, 0.863f, 0.588f);
        private static readonly Vector3 BrightGold = new(1f, 0.784f, 0.392f);
        private static readonly Vector3 HolyWhite = new(1f, 0.98f, 0.94f);

        private float chargeTime;
        private bool releasing;
        private float releaseTimer;
        private float releaseCharge;
        private float glowPulse;

        //装饰圣环(纯客户端视觉)
        private readonly List<RingFX> rings = [];
        private class RingFX
        {
            public float Life;
            public float MaxLife;
            public float MaxRadius;
            public float Rotation;
            public Vector3 Color;
        }

        public override void SetGunProperty() {
            Projectile.DamageType = DamageClass.Magic;
            HandFireDistanceX = 12;
            HandFireDistanceY = -2;
            MuzzleForwardOffset = 118;
            GunPressure = 0;
            ControlForce = 0;
            Onehanded = false;
            AlwaysAimPose = true;
        }

        public override bool StayAlive() => releasing || chargeTime > 0;

        public override void AI() {
            UpdateHeldPose(true);
            glowPulse += 0.1f;
            UpdateRings();

            //释放爆发阶段：只演出不再蓄力
            if (releasing) {
                releaseTimer++;
                float burstProg = releaseTimer / (float)BurstTime;
                float light = (1f - burstProg) * 1.2f * releaseCharge;
                Lighting.AddLight(Owner.Center, light, light * 0.9f, light * 0.7f);
                if (releaseTimer >= BurstTime) {
                    Projectile.Kill();
                }
                Time++;
                return;
            }

            if (DownLeft) {
                chargeTime++;
                HoldManaRegenDelay();

                //持续蓄力的法力上供，供不上则强制释放
                if (chargeTime > 20 && chargeTime % 30 == 0 && !TryConsumeMana(6)) {
                    ReleaseOrFizzle();
                    Time++;
                    return;
                }

                UpdateChargeCues();
                SpawnChargeMotes();

                float ch = Math.Min(chargeTime / FullCharge, 1f);
                float tipLight = 0.35f + ch * 0.6f + MathF.Sin(glowPulse) * 0.1f;
                Lighting.AddLight(ShootPos, tipLight, tipLight * 0.92f, tipLight * 0.72f);
            }
            else if (chargeTime > 0) {
                ReleaseOrFizzle();
            }

            Time++;
        }

        /// <summary>蓄力阶段节点：圣环与音阶逐级抬升</summary>
        private void UpdateChargeCues() {
            switch ((int)chargeTime) {
                case MinCharge:
                    SoundEngine.PlaySound(SoundID.Item29 with { Volume = 0.7f, Pitch = 0.05f }, Projectile.Center);
                    SpawnRing(130f, 30, WarmGold);
                    break;
                case 58:
                    SoundEngine.PlaySound(SoundID.Item29 with { Volume = 0.85f, Pitch = 0.22f }, Projectile.Center);
                    SpawnRing(190f, 36, BrightGold);
                    break;
                case 88:
                    SoundEngine.PlaySound(SoundID.Item29 with { Volume = 1f, Pitch = 0.38f }, Projectile.Center);
                    SpawnRing(250f, 42, BrightGold);
                    break;
                case FullCharge:
                    SoundEngine.PlaySound(SoundID.MaxMana with { Volume = 1.1f, Pitch = 0.1f }, Projectile.Center);
                    SoundEngine.PlaySound(SoundID.Item29 with { Volume = 1f, Pitch = 0.5f }, Projectile.Center);
                    SpawnRing(330f, 50, HolyWhite);
                    break;
            }
        }

        /// <summary>圣光尘向杖尖汇聚，蓄力越深汇聚越急</summary>
        private void SpawnChargeMotes() {
            if (Main.dedServ || chargeTime < 14 || chargeTime % 3 != 0) {
                return;
            }
            float ch = Math.Min(chargeTime / FullCharge, 1f);
            Vector2 tip = ShootPos;
            float angle = Main.rand.NextFloat(MathHelper.TwoPi);
            float dist = 70f + ch * 60f;
            Vector2 spawnPos = tip + angle.ToRotationVector2() * dist;
            Vector2 vel = (tip - spawnPos).SafeNormalize(Vector2.Zero) * (2.6f + ch * 3.4f);
            Color moteColor = Color.Lerp(new Color(255, 220, 150), Color.White, Main.rand.NextFloat(0.4f));
            PRTLoader.NewParticle<PRT_Light>(spawnPos, vel, moteColor, Main.rand.NextFloat(0.22f, 0.4f))
                ?.Configure(Main.rand.Next(16, 26), 0.9f);
        }

        private void ReleaseOrFizzle() {
            if (chargeTime >= MinCharge) {
                Release();
            }
            else {
                Fizzle();
            }
        }

        /// <summary>释放化蛇术：波潮弹幕主人端生成，各端本地演出</summary>
        private void Release() {
            SnapToAimPose();
            float charge01 = Math.Min(chargeTime / FullCharge, 1f);

            SoundEngine.PlaySound(SoundID.Item122 with { Volume = 1.1f, Pitch = -0.15f }, Owner.Center);
            SoundEngine.PlaySound(SoundID.Item29 with { Volume = 1.2f, Pitch = -0.3f }, Owner.Center);

            if (Projectile.IsOwnedByLocalPlayer()) {
                float maxRadius = 250f + charge01 * 380f;
                int damage = (int)(WeaponDamage * (1f + 0.9f * charge01));
                Projectile.NewProjectile(Source, Owner.Center, Vector2.Zero
                    , ModContent.ProjectileType<SnakeConversionWave>()
                    , damage, WeaponKnockback, Owner.whoAmI, maxRadius, charge01);
            }

            //爆发圣环层
            SpawnRing(170f + charge01 * 110f, 22, HolyWhite);
            SpawnRing(240f + charge01 * 140f, 27, WarmGold);
            if (charge01 > 0.45f) {
                SpawnRing(320f + charge01 * 90f, 24, BrightGold);
            }

            Owner.CWR().ScreenShakeValue = Math.Max(Owner.CWR().ScreenShakeValue, 2.5f + charge01 * 3.5f);

            releasing = true;
            releaseTimer = 0;
            releaseCharge = charge01;
        }

        /// <summary>蓄力不足松手：轻声消散</summary>
        private void Fizzle() {
            SoundEngine.PlaySound(SoundID.Item16 with { Volume = 0.45f, Pitch = -0.3f }, Projectile.Center);
            if (!Main.dedServ) {
                for (int i = 0; i < 5; i++) {
                    PRTLoader.NewParticle<PRT_Light>(ShootPos, VaultUtils.RandVr(1.5f, 3f)
                        , new Color(255, 228, 170), 0.25f)?.Configure(14, 0.7f);
                }
            }
            Projectile.Kill();
        }

        private void SpawnRing(float maxRadius, int lifetime, Vector3 color) {
            if (Main.dedServ) {
                return;
            }
            rings.Add(new RingFX {
                Life = 0,
                MaxLife = lifetime,
                MaxRadius = maxRadius,
                Rotation = Main.rand.NextFloat(MathHelper.TwoPi),
                Color = color
            });
        }

        private void UpdateRings() {
            for (int i = rings.Count - 1; i >= 0; i--) {
                RingFX ring = rings[i];
                ring.Life++;
                ring.Rotation += 0.045f;
                if (ring.Life >= ring.MaxLife) {
                    rings.RemoveAt(i);
                }
            }
        }

        #region 绘制
        public override bool PreDraw(ref Color lightColor) {
            SpriteBatch sb = Main.spriteBatch;
            sb.End();

            if (releasing) {
                DrawBurst(sb);
            }
            DrawRings(sb);
            if (!releasing && chargeTime > 10) {
                DrawAura(sb);
            }

            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend,
                Main.DefaultSamplerState, DepthStencilState.None,
                Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);

            if (OnHandheldDisplayBool) {
                Color staffColor = lightColor;
                if (chargeTime > MinCharge) {
                    float glowFactor = Math.Min((chargeTime - MinCharge) / 90f, 1f) * 0.4f;
                    staffColor = Color.Lerp(staffColor, new Color(255, 230, 180), glowFactor);
                }
                GunDraw(Projectile.Center - Main.screenPosition + SpecialDrawPositionOffset, ref staffColor);
            }
            return false;
        }

        /// <summary>权杖贴图斜置：杖轴相对+X约-80°，翻面时补偿角与支点一并镜像</summary>
        public override void GunDraw(Vector2 drawPos, ref Color lightColor) {
            const float axisOffset = 1.39f;
            float drawRot;
            Vector2 origin;
            SpriteEffects flip;
            if (DirSign > 0) {
                flip = SpriteEffects.None;
                origin = new Vector2(17, 161);
                drawRot = Projectile.rotation + axisOffset;
            }
            else {
                flip = SpriteEffects.FlipVertically;
                origin = new Vector2(17, TextureValue.Height - 161);
                drawRot = Projectile.rotation - axisOffset;
            }
            Main.EntitySpriteDraw(TextureValue, drawPos, null, lightColor
                , drawRot, origin, Projectile.scale * 0.9f, flip);
        }

        /// <summary>蓄力神圣光辉(杖尖 DivineAura)</summary>
        private void DrawAura(SpriteBatch sb) {
            Effect effect = EffectLoader.ElysiumStaff?.Value;
            Texture2D canvas = VaultAsset.placeholder2?.Value;
            Texture2D noise = CWRAsset.Extra_193?.Value;
            if (effect == null || canvas == null || noise == null) {
                return;
            }

            float ch = Math.Min(chargeTime / FullCharge, 1f);
            Vector2 tipPos = ShootPos - Main.screenPosition;
            float auraSize = (44f + ch * 64f) * 2f;

            effect.CurrentTechnique = effect.Techniques["DivineAura"];
            effect.Parameters["uTime"]?.SetValue(glowPulse);
            effect.Parameters["fadeAlpha"]?.SetValue(1f);
            effect.Parameters["chargeRatio"]?.SetValue(ch);
            effect.Parameters["auraRotation"]?.SetValue(glowPulse * 0.3f);
            SetPalette(effect);

            Main.graphics.GraphicsDevice.Textures[1] = noise;
            Main.graphics.GraphicsDevice.SamplerStates[1] = SamplerState.LinearWrap;

            sb.Begin(SpriteSortMode.Immediate, BlendState.Additive,
                SamplerState.LinearClamp, DepthStencilState.None,
                RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
            effect.CurrentTechnique.Passes[0].Apply();
            sb.Draw(canvas, tipPos, null, Color.White, 0f, canvas.Size() * 0.5f, auraSize, SpriteEffects.None, 0f);
            sb.End();
        }

        /// <summary>释放爆发(DivineBurst，以持杖者为心)</summary>
        private void DrawBurst(SpriteBatch sb) {
            Effect effect = EffectLoader.ElysiumStaff?.Value;
            Texture2D canvas = VaultAsset.placeholder2?.Value;
            Texture2D noise = CWRAsset.Extra_193?.Value;
            if (effect == null || canvas == null || noise == null) {
                return;
            }

            Vector2 center = Owner.Center - Main.screenPosition;
            float burstProg = releaseTimer / (float)BurstTime;
            float burstSize = (130f + releaseCharge * 110f) * (1f + burstProg * 0.4f);

            effect.CurrentTechnique = effect.Techniques["DivineBurst"];
            effect.Parameters["uTime"]?.SetValue(glowPulse);
            effect.Parameters["fadeAlpha"]?.SetValue(1f);
            effect.Parameters["burstProgress"]?.SetValue(burstProg);
            effect.Parameters["burstIntensity"]?.SetValue(0.6f + releaseCharge * 0.4f);
            effect.Parameters["auraRotation"]?.SetValue(glowPulse * 0.3f);
            SetPalette(effect);

            Main.graphics.GraphicsDevice.Textures[1] = noise;
            Main.graphics.GraphicsDevice.SamplerStates[1] = SamplerState.LinearWrap;

            sb.Begin(SpriteSortMode.Immediate, BlendState.Additive,
                SamplerState.LinearClamp, DepthStencilState.None,
                RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
            effect.CurrentTechnique.Passes[0].Apply();
            sb.Draw(canvas, center, null, Color.White, 0f, canvas.Size() * 0.5f, burstSize, SpriteEffects.None, 0f);
            sb.End();
        }

        /// <summary>装饰圣环(SacredRing)</summary>
        private void DrawRings(SpriteBatch sb) {
            if (rings.Count == 0) {
                return;
            }
            Effect effect = EffectLoader.ElysiumStaff?.Value;
            Texture2D canvas = VaultAsset.placeholder2?.Value;
            if (effect == null || canvas == null) {
                return;
            }

            Vector2 center = Owner.Center - Main.screenPosition;
            effect.CurrentTechnique = effect.Techniques["SacredRing"];
            SetPalette(effect);

            sb.Begin(SpriteSortMode.Immediate, BlendState.Additive,
                SamplerState.LinearClamp, DepthStencilState.None,
                RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

            foreach (RingFX ring in rings) {
                float progress = ring.Life / ring.MaxLife;
                float quadSize = (ring.MaxRadius + 40f) * 2f;
                effect.Parameters["uTime"]?.SetValue(glowPulse);
                effect.Parameters["fadeAlpha"]?.SetValue(1f);
                effect.Parameters["ringProgress"]?.SetValue(progress);
                effect.Parameters["ringColor"]?.SetValue(ring.Color);
                effect.Parameters["ringRotation"]?.SetValue(ring.Rotation);
                effect.CurrentTechnique.Passes[0].Apply();
                sb.Draw(canvas, center, null, Color.White, 0f, canvas.Size() * 0.5f, quadSize, SpriteEffects.None, 0f);
            }
            sb.End();
        }

        private static void SetPalette(Effect effect) {
            effect.Parameters["warmGold"]?.SetValue(WarmGold);
            effect.Parameters["brightGold"]?.SetValue(BrightGold);
            effect.Parameters["holyWhite"]?.SetValue(HolyWhite);
        }
        #endregion
    }
}
