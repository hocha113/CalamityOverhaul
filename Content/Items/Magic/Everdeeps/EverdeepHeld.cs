using CalamityOverhaul.Content.LegendWeapon.HalibutLegend;
using CalamityOverhaul.Content.Projectiles;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.IO;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Magic.Everdeeps
{
    /// <summary>
    /// 永渊持握。魔典悬于手前保持直立,只随瞄准俯仰轻倾;
    /// 每次施放在书前聚水成环射出。水环命中回报共鸣,
    /// 共鸣以环绕书体的水珠计数,满八层在最后命中处掀起水龙卷
    /// </summary>
    internal class EverdeepHeld : BaseHeldGun
    {
        public override string Texture => CWRConstant.Item_Magic + "Everdeep";
        public override int TargetID => ModContent.ItemType<Everdeep>();

        //——贴图锚点(1x像素,竖版魔典)——
        private const float HeldScale = 1f;
        /// <summary>书脊握点,绘制原点</summary>
        private static readonly Vector2 GripPx = new(25, 40);
        /// <summary>封面漩涡印记中心</summary>
        private static readonly Vector2 SigilPx = new(26, 24);

        /// <summary>共鸣阈值:攒满在最后命中处生成水龙卷</summary>
        internal const int ResonanceMax = 8;
        /// <summary>一次命中给的共鸣保鲜帧</summary>
        private const int ResonanceHold = 150;
        /// <summary>保鲜耗尽后逐层消退的间隔</summary>
        private const int ResonanceBleed = 40;

        private int resonance;
        private int resonanceDecay;
        /// <summary>书体浮沉相位</summary>
        private float bobPhase;
        /// <summary>共鸣满溢的一拍演出闩</summary>
        private int surgeFlash;

        private float Charge => resonance / (float)ResonanceMax;

        /// <summary>共鸣未散尽就别收书</summary>
        public override bool StayAlive() => resonance > 0;

        public override SoundStyle? ShootSound => SoundID.Item84 with {
            Volume = 0.42f,
            Pitch = -0.1f + Charge * 0.25f,
            MaxInstances = 3,
        };

        public override void SetGunProperty() {
            Projectile.DamageType = DamageClass.Magic;
            HandIdleDistanceX = 12;
            HandIdleDistanceY = -4;
            HandFireDistanceX = 12;
            HandFireDistanceY = -4;
            MuzzleForwardOffset = 30;
            MuzzleNormalOffset = 0;
            GunPressure = 0.05f;
            ControlForce = 0.02f;
            RecoilOffsetRecoverValue = 0.8f;
            Onehanded = true;
            FireLight = 0;//光自己按共鸣定色
        }

        public override void NetHeldSend(BinaryWriter writer) {
            writer.Write((byte)resonance);
        }

        public override void NetHeldReceive(BinaryReader reader) {
            resonance = reader.ReadByte();
        }

        public override void AI() {
            UpdateHeldPose(CanFire);
            if (CanFire) {
                HoldManaRegenDelay();
            }

            if (WantsFireLeft && CanFire && FireCooldown <= 0) {
                if (PayMana()) {
                    FireRing();
                    SetFireCooldown();
                }
                else {
                    SetFireCooldown(2f);
                }
            }

            //共鸣衰减:保鲜期过后逐层泄掉,各端本地推进,命中同步会重新校准
            if (resonance > 0 && --resonanceDecay <= 0) {
                resonance--;
                resonanceDecay = ResonanceBleed;
            }
            if (surgeFlash > 0) {
                surgeFlash--;
            }

            bobPhase += 0.055f;
            AmbientFX();
            Time++;
        }

        /// <summary>施放一环:书前聚水,环体成形射出</summary>
        private void FireRing() {
            PlayShootSound();
            RecoilPitch = MathF.Min(RecoilPitch + 0.045f, GunPressure * 2f);
            RecoilOffset -= UnitToMouseV * 3.5f;

            Vector2 muzzle = ShootPos;
            Vector2 dir = UnitToMouseV;

            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.SplashWeak with { Volume = 0.42f, Pitch = 0.35f }, muzzle);
                //聚拢:水滴从四周向施放点收束
                for (int i = 0; i < 6; i++) {
                    Vector2 off = Main.rand.NextVector2CircularEdge(24f, 24f);
                    PRTLoader.NewParticle<PRT_OceanCurrentDroplet>(muzzle + off
                        , -off * 0.14f + dir * 2.2f, EverdeepVFX.RandomWater(0.2f)
                        , Main.rand.NextFloat(0.09f, 0.16f))
                        ?.Configure(Main.rand.Next(10, 18), gravityPerFrame: 0.05f
                            , dragMultiplier: 0.97f, turbulence: 0.02f, canSplit: false);
                }
                PRTLoader.NewParticle<PRT_OceanCurrentWake>(muzzle, Vector2.Zero
                    , EverdeepVFX.AbyssGlow, 0.05f)
                    ?.Configure(dir, new Vector2(1f, 0.55f), 0.3f, Main.rand.Next(9, 13));
            }

            if (Projectile.IsOwnedByLocalPlayer()) {
                Vector2 vel = dir * AmmoState.ShootSpeed + Owner.velocity * 0.2f;
                Projectile.NewProjectile(Source, muzzle, vel, ModContent.ProjectileType<EverdeepRing>()
                    , WeaponDamage, WeaponKnockback, Owner.whoAmI, 0f, Main.rand.NextFloat(1f));
            }
        }

        /// <summary>
        /// 水环命中回报共鸣(主人端调用)。攒满 <see cref="ResonanceMax"/> 层清零,
        /// 在触发目标处掀起水龙卷
        /// </summary>
        internal void AddResonance(NPC target) {
            if (!Projectile.IsOwnedByLocalPlayer()) {
                return;
            }
            resonance++;
            resonanceDecay = ResonanceHold;

            if (resonance >= ResonanceMax) {
                resonance = 0;
                surgeFlash = 14;
                SummonMaelstrom(target);
            }
            else if (!VaultUtils.isServer) {
                //每层小反馈:水声轻响,音高随层数爬
                SoundEngine.PlaySound(SoundID.Item85 with {
                    Volume = 0.26f,
                    Pitch = -0.35f + resonance * 0.09f,
                    MaxInstances = 3,
                }, Projectile.Center);
            }
            NetUpdate();
        }

        /// <summary>共鸣满溢:在触发目标处掀起巨大的水龙卷</summary>
        private void SummonMaelstrom(NPC target) {
            Vector2 pos = target != null && target.active ? target.Center : Main.MouseWorld;
            int damage = (int)(WeaponDamage * 1.5f);
            Projectile.NewProjectile(Source, pos, Vector2.Zero
                , ModContent.ProjectileType<EverdeepMaelstrom>()
                , damage, WeaponKnockback * 1.5f, Owner.whoAmI);

            //书侧的满溢拍:龙卷自身的出生拍在各端自播,这里只管书的反馈
            SoundEngine.PlaySound(SoundID.Item84 with { Volume = 0.7f, Pitch = 0.45f }, Projectile.Center);
            if (!VaultUtils.isServer) {
                for (int i = 0; i < 8; i++) {
                    Vector2 vel = Main.rand.NextVector2CircularEdge(3.5f, 3.5f) - Vector2.UnitY * 1.2f;
                    EverdeepVFX.ShedDroplet(Projectile.Center + Main.rand.NextVector2Circular(10f, 14f)
                        , vel, Main.rand.NextFloat(0.7f, 1.1f));
                }
            }
        }

        /// <summary>常驻氛围:书体微光,共鸣时漏泡沫</summary>
        private void AmbientFX() {
            if (VaultUtils.isServer) {
                return;
            }
            float charge = Charge;
            Lighting.AddLight(Projectile.Center
                , EverdeepVFX.AbyssGlow.ToVector3() * (0.10f + 0.38f * charge + surgeFlash * 0.03f));

            if (resonance > 0 && Main.rand.NextBool(Math.Max(14 - resonance, 4))) {
                PRTLoader.NewParticle<PRT_OceanCurrentFoam>(
                    Projectile.Center + Main.rand.NextVector2Circular(12f, 16f)
                    , -Vector2.UnitY * Main.rand.NextFloat(0.2f, 0.55f)
                    , EverdeepVFX.AbyssFoam * 0.7f, Main.rand.NextFloat(0.04f, 0.07f))
                    ?.Configure(Main.rand.Next(18, 30), 0.03f);
            }
        }

        #region 绘制
        public override void GunDraw(Vector2 drawPos, ref Color lightColor) {
            Texture2D tex = TextureValue;
            bool facingRight = DirSign > 0;
            float charge = Charge;

            //书保持直立,只随瞄准俯仰轻倾,外加浮沉呼吸
            float lean = UnitToMouseV.Y * DirSign * 0.20f;
            float rot = lean + MathF.Sin(bobPhase * 0.7f) * 0.035f;
            Vector2 bob = new(0, MathF.Sin(bobPhase) * 2.4f);
            Vector2 bookPos = drawPos + bob;

            Vector2 origin = facingRight ? GripPx : new Vector2(tex.Width - GripPx.X, GripPx.Y);
            SpriteEffects flip = facingRight ? SpriteEffects.None : SpriteEffects.FlipHorizontally;

            //书下暗渊垫光
            Texture2D glow = CWRAsset.SoftGlow.Value;
            Color underCol = EverdeepVFX.AbyssBlue with { A = 0 };
            Main.EntitySpriteDraw(glow, bookPos, null, underCol * (0.16f + charge * 0.22f), 0f
                , glow.Size() * 0.5f, 0.5f + charge * 0.12f, SpriteEffects.None, 0);

            Main.EntitySpriteDraw(tex, bookPos, null, lightColor, rot, origin
                , HeldScale * Projectile.scale, flip);

            //封面漩涡印记辉光:随共鸣涨亮,满溢拍炸白
            float pulse = 0.75f + 0.25f * MathF.Sin(Main.GlobalTimeWrappedHourly * (4f + charge * 6f));
            Vector2 sigilOff = new((SigilPx.X - GripPx.X) * (facingRight ? 1f : -1f), SigilPx.Y - GripPx.Y);
            Vector2 sigilPos = bookPos + sigilOff.RotatedBy(rot);
            Color sigilCol = EverdeepVFX.AbyssGlow with { A = 0 };
            float sigilAlpha = 0.30f + charge * 0.55f + surgeFlash * 0.05f;
            Main.EntitySpriteDraw(glow, sigilPos, null, sigilCol * (sigilAlpha * pulse), 0f
                , glow.Size() * 0.5f, 0.24f + charge * 0.10f, SpriteEffects.None, 0);
            if (surgeFlash > 0) {
                Main.EntitySpriteDraw(glow, sigilPos, null
                    , EverdeepVFX.AbyssFoam with { A = 0 } * (surgeFlash / 14f * 0.8f), 0f
                    , glow.Size() * 0.5f, 0.4f, SpriteEffects.None, 0);
            }

            DrawResonanceOrbits(bookPos, charge);
        }

        /// <summary>共鸣读数:resonance 个水珠绕书体椭圆环游,越满转得越急</summary>
        private void DrawResonanceOrbits(Vector2 bookPos, float charge) {
            if (resonance <= 0) {
                return;
            }
            Texture2D glint = CWRAsset.LightShot.Value;
            float orbitSpeed = 2.0f + charge * 2.8f;
            for (int i = 0; i < resonance; i++) {
                float ang = Main.GlobalTimeWrappedHourly * orbitSpeed
                    + i * MathHelper.TwoPi / ResonanceMax;
                //椭圆轨道,sin 相位造前后景深:后半圈更小更暗
                Vector2 orbit = new(MathF.Cos(ang) * 32f, MathF.Sin(ang) * 12f - 6f);
                float depth = 0.65f + 0.35f * MathF.Sin(ang + MathHelper.PiOver2);
                Color col = Color.Lerp(EverdeepVFX.AbyssGlow, EverdeepVFX.AbyssFoam, depth) with { A = 0 };
                Main.EntitySpriteDraw(glint, bookPos + orbit, null, col * (0.5f + depth * 0.4f)
                    , ang, glint.Size() * 0.5f
                    , new Vector2(0.05f, 0.028f) * (0.7f + depth * 0.5f), SpriteEffects.None, 0);
            }
        }
        #endregion
    }
}
