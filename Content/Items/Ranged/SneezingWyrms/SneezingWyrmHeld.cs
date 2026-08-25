using CalamityOverhaul.Content.Items.Magic.WheezingWyrms;
using CalamityOverhaul.Content.Projectiles;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.IO;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Ranged.SneezingWyrms
{
    /// <summary>
    /// 嚏龙铳持握。速射龙击弹，连射令枪膛升温，弹色沿 <see cref="Wyrmfire"/> 黑体色带变亮；
    /// 每积攒数发，被烟气憋住的龙鼻打一个喷嚏，向枪身上侧喷出浓烟，
    /// 烟团(<see cref="WyrmSneezeFume"/>)阴燃片刻后自燃成龙焰。松手枪膛缓慢冷却
    /// </summary>
    internal class SneezingWyrmHeld : BaseHeldGun
    {
        public override string Texture => CWRConstant.Item_Ranged + "SneezingWyrm";
        public override int TargetID => ModContent.ItemType<SneezingWyrm>();

        //——贴图锚点(1x像素，炮口朝右)——
        private const float HeldScale = 1f;
        private static readonly Vector2 GripPx = new(25, 28);    //握把，绘制原点
        private static readonly Vector2 MuzzlePx = new(50, 16);  //炮口
        private static readonly Vector2 NostrilPx = new(37, 10); //龙鼻喷烟口
        private static readonly Vector2 EyePx = new(22, 9);      //龙眼

        //——温度与喷嚏节奏——
        /// <summary>连射烧满约70发</summary>
        private const float HeatPerShot = 1f / 70f;
        /// <summary>停火每帧降温</summary>
        private const float HeatFall = 1f / 160f;
        /// <summary>每积攒这么多发打一个喷嚏</summary>
        private const int ShotsPerSneeze = 8;

        private float heat;
        private int sneezeCharge;

        /// <summary>枪膛尚温就别收枪，让余热演完</summary>
        public override bool StayAlive() => heat > 0.03f;

        /// <summary>龙击弹出生温度：冷枪橙红，烧满贴着蓝焰边缘</summary>
        private float TracerTemp => 0.5f + 0.42f * heat;
        /// <summary>鼻腔憋压程度 0~1，喷嚏前的透光预告用</summary>
        private float SneezePressure => sneezeCharge / (float)ShotsPerSneeze;

        public override SoundStyle? ShootSound => SoundID.Item11 with {
            Volume = 0.26f,
            MaxInstances = 5,
            Pitch = -0.18f + heat * 0.3f,
            PitchVariance = 0.08f,
        };

        public override void SetGunProperty() {
            HandIdleDistanceX = 14;
            HandIdleDistanceY = 2;
            HandFireDistanceX = 14;
            HandFireDistanceY = -3;
            MuzzleForwardOffset = (MuzzlePx.X - GripPx.X) * HeldScale;
            MuzzleNormalOffset = (MuzzlePx.Y - GripPx.Y) * HeldScale;
            GunPressure = 0.055f;
            ControlForce = 0.03f;
            RecoilOffsetRecoverValue = 0.8f;
            FireLight = 0;//火光自己画，按温度定色
        }

        public override void NetHeldSend(BinaryWriter writer) {
            writer.Write(heat);
            writer.Write((byte)sneezeCharge);
        }

        public override void NetHeldReceive(BinaryReader reader) {
            heat = reader.ReadSingle();
            sneezeCharge = reader.ReadByte();
        }

        public override void AI() {
            UpdateHeldPose(CanFire);

            if (WantsFireLeft && FireCooldown <= 0 && HasAmmo) {
                Fire();
                SetFireCooldown();
            }

            if (!WantsFireLeft || !HasAmmo) {
                CoolPhase();
            }
            HotBodyFX();

            if (heat > 0.03f) {
                Lighting.AddLight(ShootPos, Wyrmfire.TempColor(TracerTemp).ToVector3() * (0.1f + 0.3f * heat));
            }
            Time++;
        }

        private void Fire() {
            SnapToAimPose();
            PlayShootSound();

            heat = MathF.Min(heat + HeatPerShot, 1f);
            float temp = TracerTemp;

            RecoilPitch = MathF.Min(RecoilPitch + 0.02f, GunPressure * 2f);
            RecoilOffset -= UnitToMouseV * 1.1f;

            //口焰：短舌+温度光，不用默认白光
            Vector2 muzzle = ShootPos;
            Vector2 dir = UnitToMouseV;
            Vector2 od = dir.RotatedBy(Main.rand.NextFloat(-0.2f, 0.2f));
            PRTLoader.NewParticle<PRT_WyrmTongue>(muzzle, od * 2f, default, Main.rand.NextFloat(0.6f, 0.9f))
                ?.Configure(od, Main.rand.NextFloat(0.75f, 1.1f), 4, temp);
            if (Main.rand.NextBool(3)) {
                PRTLoader.NewParticle<PRT_Spark>(muzzle, dir.RotatedBy(Main.rand.NextFloat(-0.3f, 0.3f)) * Main.rand.NextFloat(3f, 6f)
                    , Wyrmfire.TempColor(temp), Main.rand.NextFloat(0.3f, 0.5f))
                    ?.Configure(true, Main.rand.Next(6, 10));
            }
            Lighting.AddLight(muzzle, Wyrmfire.TempColor(temp).ToVector3() * 0.55f);

            if (Projectile.IsOwnedByLocalPlayer()) {
                //普通子弹转化为龙击弹，特殊弹药保持原样
                int roundType = ModContent.ProjectileType<WyrmstrikeRound>();
                Vector2 vel = ShootVelocity.RotatedByRandom(0.032f) * Main.rand.NextFloat(0.96f, 1.04f);
                if (AmmoTypes == ProjectileID.Bullet) {
                    Projectile.NewProjectile(Source, muzzle, vel, roundType
                        , WeaponDamage, WeaponKnockback, Owner.whoAmI, temp, Main.rand.NextFloat(9f));
                }
                else {
                    Projectile.NewProjectile(Source, muzzle, vel, AmmoTypes
                        , WeaponDamage, WeaponKnockback, Owner.whoAmI);
                }
            }
            ConsumeAmmo();

            if (++sneezeCharge >= ShotsPerSneeze) {
                Sneeze();
            }
        }

        /// <summary>龙鼻憋满打嚏：向枪身上侧喷出浓烟，烟团稍后自燃成龙焰</summary>
        private void Sneeze() {
            sneezeCharge = 0;

            SoundEngine.PlaySound(SoundID.DD2_BetsyWindAttack with {
                Volume = 0.55f,
                Pitch = 0.28f + heat * 0.25f,
                MaxInstances = 3,
            }, Projectile.Center);

            Vector2 nostril = GetMuzzlePos((NostrilPx.X - GripPx.X) * HeldScale, (NostrilPx.Y - GripPx.Y) * HeldScale);
            Vector2 aim = UnitToMouseV;
            //枪身上侧法向，掺一点前向让烟往弹道前方飘
            Vector2 sneezeDir = (aim.RotatedBy(-MathHelper.PiOver2 * DirSign) * 0.9f + aim * 0.3f).SafeNormalize(-Vector2.UnitY);

            //鼻息顿挫
            RecoilPitch = MathF.Min(RecoilPitch + 0.05f, GunPressure * 2f);
            RecoilOffset -= aim * 2.2f;

            int smokeN = 8 + (int)(heat * 4f);
            for (int i = 0; i < smokeN; i++) {
                Vector2 vel = sneezeDir.RotatedBy(Main.rand.NextFloat(-0.4f, 0.4f)) * Main.rand.NextFloat(1.8f, 4.4f);
                PRTLoader.NewParticle<PRT_WyrmSmoke>(nostril + Main.rand.NextVector2Circular(3f, 3f), vel
                    , new Color(92, 84, 76) * Main.rand.NextFloat(0.5f, 0.75f), Main.rand.NextFloat(0.16f, 0.3f))
                    ?.Configure(Main.rand.Next(34, 56), 0.05f);
            }
            //压轴一大团慢烟垫在后面
            PRTLoader.NewParticle<PRT_WyrmSmoke>(nostril, sneezeDir * Main.rand.NextFloat(1f, 1.6f)
                , new Color(84, 76, 70) * 0.6f, Main.rand.NextFloat(0.3f, 0.38f))
                ?.Configure(Main.rand.Next(40, 60), 0.06f);
            //热枪打嚏带火星
            if (heat > 0.4f) {
                for (int i = 0; i < 3; i++) {
                    PRTLoader.NewParticle<PRT_Spark>(nostril, sneezeDir.RotatedBy(Main.rand.NextFloat(-0.4f, 0.4f)) * Main.rand.NextFloat(2f, 5f)
                        , Wyrmfire.TempColor(0.35f + heat * 0.3f), Main.rand.NextFloat(0.4f, 0.7f))
                        ?.Configure(true, Main.rand.Next(8, 14));
                }
            }

            //真正会自燃的烟团
            if (Projectile.IsOwnedByLocalPlayer()) {
                float fumeTemp = 0.35f + 0.55f * heat;
                Vector2 vel = sneezeDir.RotatedBy(Main.rand.NextFloat(-0.22f, 0.22f)) * Main.rand.NextFloat(1.8f, 2.6f) + Owner.velocity * 0.2f;
                Projectile.NewProjectile(Source, nostril, vel, ModContent.ProjectileType<WyrmSneezeFume>()
                    , (int)(WeaponDamage * 1.5f), WeaponKnockback, Owner.whoAmI, fumeTemp, Main.rand.NextFloat(9f));
            }
        }

        /// <summary>停火降温：枪口冒余烟，热度缓慢回落</summary>
        private void CoolPhase() {
            if (heat <= 0f) {
                return;
            }
            heat = MathF.Max(heat - HeatFall, 0f);

            Vector2 muzzleDir = Projectile.rotation.ToRotationVector2();
            if (heat > 0.25f && (int)Time % 8 == 0) {
                PRTLoader.NewParticle<PRT_WyrmSmoke>(ShootPos, -Vector2.UnitY * Main.rand.NextFloat(0.3f, 0.7f) + muzzleDir * 0.3f
                    , new Color(96, 88, 82) * (0.35f + heat * 0.25f), Main.rand.NextFloat(0.1f, 0.17f))
                    ?.Configure(Main.rand.Next(24, 38), 0.06f);
            }
        }

        /// <summary>高热枪身：炮管上方升腾热烟</summary>
        private void HotBodyFX() {
            if (heat < 0.55f || (int)Time % 6 != 0) {
                return;
            }
            Vector2 barrel = GetMuzzlePos(Main.rand.NextFloat(8f, 22f), (MuzzlePx.Y - GripPx.Y) * HeldScale - 3f);
            PRTLoader.NewParticle<PRT_WyrmSmoke>(barrel, -Vector2.UnitY * Main.rand.NextFloat(0.2f, 0.5f)
                , new Color(90, 82, 76) * ((heat - 0.55f) * 0.7f), Main.rand.NextFloat(0.08f, 0.14f))
                ?.Configure(Main.rand.Next(16, 26), 0.05f);
        }

        public override void GunDraw(Vector2 drawPos, ref Color lightColor) {
            Texture2D tex = TextureValue;
            bool facingRight = DirSign > 0;
            Vector2 origin = facingRight ? GripPx : new Vector2(GripPx.X, tex.Height - GripPx.Y);
            Main.EntitySpriteDraw(tex, drawPos, null, lightColor, Projectile.rotation
                , origin, HeldScale * Projectile.scale
                , facingRight ? SpriteEffects.None : SpriteEffects.FlipVertically);

            if (heat <= 0.02f && sneezeCharge == 0) {
                return;
            }

            Texture2D glow = CWRAsset.SoftGlow.Value;
            float pulse = 0.82f + 0.18f * MathF.Sin(Main.GlobalTimeWrappedHourly * (7f + heat * 8f));

            if (heat > 0.02f) {
                //炮管余温
                Color barrelCol = Wyrmfire.TempColor(0.28f + heat * 0.55f) with { A = 0 };
                Vector2 barrelWorld = GetMuzzlePos((MuzzlePx.X - GripPx.X - 8f) * HeldScale, (MuzzlePx.Y - GripPx.Y) * HeldScale);
                Main.EntitySpriteDraw(glow, drawPos + (barrelWorld - Projectile.Center), null, barrelCol * (heat * 0.45f * pulse), 0f
                    , glow.Size() * 0.5f, 0.3f + heat * 0.2f, SpriteEffects.None, 0);

                //龙眼随热度烧成炽蓝
                Color eyeCol = Wyrmfire.TempColor(1.05f) with { A = 0 };
                Vector2 eyeWorld = GetMuzzlePos((EyePx.X - GripPx.X) * HeldScale, (EyePx.Y - GripPx.Y) * HeldScale);
                Main.EntitySpriteDraw(glow, drawPos + (eyeWorld - Projectile.Center), null, eyeCol * (0.2f + heat * 0.5f), 0f
                    , glow.Size() * 0.5f, 0.12f + heat * 0.08f, SpriteEffects.None, 0);
            }

            //鼻腔憋压：喷嚏前的透光预告
            float pressure = SneezePressure;
            if (pressure > 0.2f) {
                Color noseCol = Wyrmfire.TempColor(0.25f + heat * 0.4f) with { A = 0 };
                Vector2 noseWorld = GetMuzzlePos((NostrilPx.X - GripPx.X) * HeldScale, (NostrilPx.Y - GripPx.Y) * HeldScale);
                Main.EntitySpriteDraw(glow, drawPos + (noseWorld - Projectile.Center), null
                    , noseCol * (pressure * (0.3f + heat * 0.35f) * pulse), 0f
                    , glow.Size() * 0.5f, 0.16f + pressure * 0.12f, SpriteEffects.None, 0);
            }
        }
    }
}
