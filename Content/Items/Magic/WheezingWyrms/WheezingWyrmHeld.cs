using CalamityOverhaul.Content.Projectiles;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using System.IO;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Magic.WheezingWyrms
{
    /// <summary>
    /// 哮龙杖持握。冷启动先咳嗽喷烟(四声渐急，后两声带火星)，咳满点燃；
    /// 点燃后持续喷吐升温，焰色沿黑体色带从暗红烧到炽蓝；
    /// 松手降温，龙喉尚温可续燃，凉透吐一口白烟熄火，下次得重新咳
    /// </summary>
    internal class WheezingWyrmHeld : BaseHeldGun
    {
        public override string Texture => CWRConstant.Item_Magic + "WheezingWyrmHeld";
        public override int TargetID => ModContent.ItemType<WheezingWyrm>();

        [VaultLoaden(CWRConstant.Masking + "TearFlame01")]
        private static Asset<Texture2D> FlameTex = null;

        //——贴图锚点(1x像素，手持图已镜像成龙嘴朝右)——
        private const float HeldScale = 1.15f;
        private static readonly Vector2 GripPx = new(27, 33);   //握点，绘制原点
        private static readonly Vector2 MouthPx = new(41, 12);  //龙嘴出焰口
        private static readonly Vector2 EyePx = new(21, 9);     //龙眼

        //——节奏参数——
        /// <summary>咳嗽起动总帧数</summary>
        private const int CoughTime = 58;
        /// <summary>四声咳的进度点，渐急</summary>
        private static readonly float[] CoughBeats = [0.14f, 0.38f, 0.6f, 0.79f];
        /// <summary>点燃后烧满(蓝焰)约5秒</summary>
        private const float HeatRise = 1f / 300f;
        /// <summary>松手降温速率</summary>
        private const float HeatFall = 1f / 150f;
        /// <summary>蓝焰阈值</summary>
        private const float BlueHeat = 0.92f;
        /// <summary>热度归零后余烬苟延帧数，过后正式熄火</summary>
        private const int ExtinguishGraceTime = 26;

        private float coughProgress;
        private float heat;
        private bool ignited;
        /// <summary>入蓝火时的一次性演出闩，降温回落后解锁</summary>
        private bool blueAnnounced;
        private int extinguishGrace;
        /// <summary>上一轮供焰是否成功，断蓝时热度不再上升</summary>
        private bool fedThisRound = true;

        /// <summary>龙喉还有火或余烟未散就别收杖</summary>
        public override bool StayAlive() => ignited || coughProgress > 0.01f;

        /// <summary>展示温度：点燃后随热度走，咳嗽阶段只有喉底一点暗红</summary>
        private float DisplayTemp => ignited ? 0.12f + 0.88f * heat : coughProgress * 0.1f;

        public override SoundStyle? ShootSound => SoundID.Item34 with {
            Volume = 0.34f,
            MaxInstances = 3,
            Pitch = MathHelper.Lerp(-0.4f, 0.35f, heat),
        };

        public override void SetGunProperty() {
            Projectile.DamageType = DamageClass.Magic;
            HandIdleDistanceX = 10;
            HandIdleDistanceY = 0;
            HandFireDistanceX = 6;
            HandFireDistanceY = -2;
            MuzzleForwardOffset = (MouthPx.X - GripPx.X) * HeldScale;
            MuzzleNormalOffset = (MouthPx.Y - GripPx.Y) * HeldScale;
            GunPressure = 0.09f;            //咳嗽顿挫的上抬上限
            ControlForce = 0.015f;
            RecoilOffsetRecoverValue = 0.72f;
            Onehanded = true;
            FireLight = 0;                  //火光自己画，按温度定色
        }

        public override void NetHeldSend(BinaryWriter writer) {
            writer.Write(heat);
            writer.Write(coughProgress);
        }

        public override void NetHeldReceive(BinaryReader reader) {
            heat = reader.ReadSingle();
            coughProgress = reader.ReadSingle();
        }

        public override BitsByte SendBitsByte(BitsByte flags) {
            flags = base.SendBitsByte(flags);
            flags[3] = ignited;
            return flags;
        }

        public override void ReceiveBitsByte(BitsByte flags) {
            base.ReceiveBitsByte(flags);
            ignited = flags[3];
        }

        public override void AI() {
            UpdateHeldPose(CanFire);
            if (CanFire) {
                HoldManaRegenDelay();
            }

            if (ignited) {
                BreathePhase(WantsFireLeft);
            }
            else {
                CoughPhase(WantsFireLeft);
            }

            float glowTemp = DisplayTemp;
            if (glowTemp > 0.03f) {
                Lighting.AddLight(ShootPos, Wyrmfire.TempColor(glowTemp).ToVector3() * (0.12f + 0.4f * glowTemp));
            }
            Time++;
        }

        #region 咳嗽起动
        private void CoughPhase(bool firing) {
            if (!firing) {
                //没按住就慢慢泄气，进度保留一半速率衰减
                coughProgress = MathF.Max(coughProgress - 0.5f / CoughTime, 0f);
                return;
            }

            float prev = coughProgress;
            coughProgress += 1f / CoughTime;

            foreach (float beat in CoughBeats) {
                if (prev < beat && coughProgress >= beat) {
                    CoughBeat(beat);
                }
            }

            //咳间隙从嘴角漏的余烟
            if ((int)Time % 8 == 0) {
                PRTLoader.NewParticle<PRT_WyrmSmoke>(ShootPos + Main.rand.NextVector2Circular(3f, 3f)
                    , UnitToMouseV * Main.rand.NextFloat(0.3f, 0.9f)
                    , new Color(64, 58, 54) * 0.45f, Main.rand.NextFloat(0.11f, 0.17f))
                    ?.Configure(Main.rand.Next(22, 34), 0.05f);
            }

            if (coughProgress >= 1f) {
                Ignite();
            }
        }

        /// <summary>一声咳：龙首猛缩顿挫，烟从嘴里蹾出来；后两声带火星与短命火舌</summary>
        private void CoughBeat(float beat) {
            RecoilPitch = MathF.Min(RecoilPitch + 0.085f, GunPressure * 2f);
            RecoilOffset -= UnitToMouseV * (3.2f + beat * 2.5f);
            SoundEngine.PlaySound(SoundID.DD2_BetsyWindAttack with {
                Volume = 0.62f + beat * 0.3f,
                Pitch = -0.72f + beat * 0.42f,
                MaxInstances = 3,
            }, Projectile.Center);

            Vector2 mouth = ShootPos;
            Vector2 dir = UnitToMouseV;
            int smokeN = 4 + (int)(beat * 5f);
            for (int i = 0; i < smokeN; i++) {
                Vector2 vel = dir.RotatedBy(Main.rand.NextFloat(-0.42f, 0.42f)) * Main.rand.NextFloat(1.4f, 4f + beat * 2.4f);
                PRTLoader.NewParticle<PRT_WyrmSmoke>(mouth + Main.rand.NextVector2Circular(4f, 4f), vel
                    , new Color(58, 52, 48) * Main.rand.NextFloat(0.5f, 0.78f), Main.rand.NextFloat(0.17f, 0.3f))
                    ?.Configure(Main.rand.Next(30, 52), 0.05f);
            }
            //坠地烟灰
            for (int i = 0; i < 2; i++) {
                PRTLoader.NewParticle<PRT_WyrmSmoke>(mouth, dir.RotatedBy(Main.rand.NextFloat(-0.3f, 0.3f)) * Main.rand.NextFloat(1f, 2.4f)
                    , new Color(46, 42, 40) * 0.6f, Main.rand.NextFloat(0.07f, 0.11f))
                    ?.Configure(Main.rand.Next(20, 30), 0f, 0.05f);
            }

            //快点着了：后两声咳带火星，火苗蹿一下又灭
            if (beat > 0.5f) {
                for (int i = 0; i < 3; i++) {
                    PRTLoader.NewParticle<PRT_Spark>(mouth, dir.RotatedBy(Main.rand.NextFloat(-0.5f, 0.5f)) * Main.rand.NextFloat(2.5f, 6f)
                        , Wyrmfire.TempColor(0.3f), Main.rand.NextFloat(0.5f, 0.85f))
                        ?.Configure(true, Main.rand.Next(8, 14));
                }
                PRTLoader.NewParticle<PRT_WyrmTongue>(mouth, dir * 1.6f, default, Main.rand.NextFloat(0.8f, 1.2f))
                    ?.Configure(dir, Main.rand.NextFloat(0.6f, 1f), 5, 0.2f);
            }
        }

        /// <summary>点燃拍：火舌炸开，把最后一口浓烟顶出去</summary>
        private void Ignite() {
            ignited = true;
            heat = 0.1f;
            coughProgress = 0f;
            blueAnnounced = false;
            fedThisRound = true;
            NetUpdate();

            SoundEngine.PlaySound(SoundID.DD2_BetsyFireballShot with { Volume = 0.85f, Pitch = -0.2f }, Projectile.Center);
            SoundEngine.PlaySound(SoundID.Item20 with { Volume = 0.75f, Pitch = -0.1f }, Projectile.Center);

            Vector2 mouth = ShootPos;
            Vector2 dir = UnitToMouseV;
            for (int i = 0; i < 9; i++) {
                Vector2 od = dir.RotatedBy(Main.rand.NextFloat(-0.75f, 0.75f));
                PRTLoader.NewParticle<PRT_WyrmTongue>(mouth, od * 2.2f, default, Main.rand.NextFloat(0.9f, 1.6f))
                    ?.Configure(od, Main.rand.NextFloat(0.8f, 1.5f), Main.rand.Next(5, 10), 0.35f);
            }
            for (int i = 0; i < 3; i++) {
                PRTLoader.NewParticle<PRT_WyrmSmoke>(mouth, dir.RotatedBy(Main.rand.NextFloat(-0.25f, 0.25f)) * Main.rand.NextFloat(3.5f, 6f)
                    , new Color(70, 62, 58) * 0.6f, Main.rand.NextFloat(0.2f, 0.3f))
                    ?.Configure(Main.rand.Next(24, 36), 0.06f);
            }
            PRT_DWave wave = PRTLoader.NewParticle<PRT_DWave>(mouth, Vector2.Zero, Wyrmfire.TempColor(0.4f), 0.55f);
            wave?.Configure(new Vector2(1f, 0.6f), dir.ToRotation(), 1.25f, 12);
            Lighting.AddLight(mouth, Wyrmfire.TempColor(0.4f).ToVector3() * 1f);
        }
        #endregion

        #region 喷焰升温
        private void BreathePhase(bool firing) {
            if (firing) {
                extinguishGrace = 0;
                //只有真在喷焰才升温；断蓝时火头停在原档
                if (fedThisRound) {
                    heat = MathF.Min(heat + HeatRise, 1f);
                    if (!blueAnnounced && heat >= BlueHeat) {
                        BlueFlare();
                    }
                }

                if (FireCooldown <= 0) {
                    if (PayMana()) {
                        fedThisRound = true;
                        FireVolley();
                        SetFireCooldown();
                    }
                    else {
                        //没蓝：火头噎住，干喘一口烟
                        fedThisRound = false;
                        SetFireCooldown(2f);
                        PRTLoader.NewParticle<PRT_WyrmSmoke>(ShootPos, UnitToMouseV * 0.8f
                            , new Color(72, 64, 60) * 0.5f, 0.1f)
                            ?.Configure(Main.rand.Next(18, 26), 0.05f);
                    }
                }
                MouthFlameFX();
            }
            else {
                heat -= HeatFall;
                if (heat < 0.8f) {
                    blueAnnounced = false;
                }
                if (heat <= 0f) {
                    heat = 0f;
                    if (++extinguishGrace >= ExtinguishGraceTime) {
                        Extinguish();
                    }
                }
                MouthCoolFX();
            }
        }

        private void FireVolley() {
            //点燃初期焰短势弱，慢慢才喷开；温度越高焰压越足
            float jet = MathHelper.Clamp((heat - 0.08f) / 0.2f, 0.3f, 1f);
            PlayShootSound();
            RecoilPitch = MathF.Min(RecoilPitch + 0.012f, GunPressure * 2f);
            RecoilOffset -= UnitToMouseV * 0.55f;

            if (Projectile.IsOwnedByLocalPlayer()) {
                Vector2 vel = UnitToMouseV.RotatedByRandom(0.11f)
                    * (AmmoState.ShootSpeed * (0.5f + 0.5f * jet) * (0.86f + 0.26f * heat))
                    + Owner.velocity * 0.3f;
                int damage = (int)(WeaponDamage * (0.85f + 0.75f * heat));
                Projectile.NewProjectile(Source, ShootPos, vel, ModContent.ProjectileType<WyrmFlame>()
                    , damage, WeaponKnockback, Owner.whoAmI, heat, Main.rand.NextFloat(9f));
            }
        }

        /// <summary>入蓝火的一拍：嗓音陡然拔高，蓝舌炸开</summary>
        private void BlueFlare() {
            blueAnnounced = true;
            NetUpdate();
            SoundEngine.PlaySound(SoundID.Item20 with { Volume = 0.9f, Pitch = 0.45f }, Projectile.Center);
            SoundEngine.PlaySound(SoundID.Item34 with { Volume = 0.65f, Pitch = 0.6f }, Projectile.Center);

            Vector2 mouth = ShootPos;
            Vector2 dir = UnitToMouseV;
            for (int i = 0; i < 10; i++) {
                Vector2 od = dir.RotatedBy(Main.rand.NextFloat(-0.85f, 0.85f));
                PRTLoader.NewParticle<PRT_WyrmTongue>(mouth, od * 2.6f, default, Main.rand.NextFloat(0.9f, 1.6f))
                    ?.Configure(od, Main.rand.NextFloat(0.8f, 1.5f), Main.rand.Next(5, 10), 1.05f);
            }
            for (int i = 0; i < 8; i++) {
                PRTLoader.NewParticle<PRT_Spark>(mouth, dir.RotatedBy(Main.rand.NextFloat(-0.6f, 0.6f)) * Main.rand.NextFloat(3.5f, 8f)
                    , Wyrmfire.TempColor(1.1f), Main.rand.NextFloat(0.5f, 0.9f))
                    ?.Configure(false, Main.rand.Next(10, 16));
            }
            PRT_DWave wave = PRTLoader.NewParticle<PRT_DWave>(mouth, Vector2.Zero, Wyrmfire.TempColor(1.05f), 0.6f);
            wave?.Configure(new Vector2(1f, 0.6f), dir.ToRotation(), 1.4f, 12);
        }

        /// <summary>喷焰期贴根火舌：焰流的根锚在龙嘴上；低温带烟，蓝焰几乎无烟只溅火星</summary>
        private void MouthFlameFX() {
            Vector2 mouth = ShootPos;
            Vector2 dir = UnitToMouseV;
            float temp = DisplayTemp;

            //主根舌每帧一条，隔拍再补一条错相侧舌，根口始终是一簇活火
            Vector2 od = dir.RotatedBy(Main.rand.NextFloat(-0.3f, 0.3f));
            PRTLoader.NewParticle<PRT_WyrmTongue>(mouth + Main.rand.NextVector2Circular(3f, 3f), od * 1.4f
                , default, Main.rand.NextFloat(0.9f, 1.5f))
                ?.Configure(od, Main.rand.NextFloat(0.8f, 1.3f), Main.rand.Next(3, 6), temp);
            if ((int)Time % 3 == 0) {
                Vector2 od2 = dir.RotatedBy(Main.rand.NextFloat(-0.55f, 0.55f));
                PRTLoader.NewParticle<PRT_WyrmTongue>(mouth, od2 * 1.1f, default, Main.rand.NextFloat(0.6f, 1f))
                    ?.Configure(od2, Main.rand.NextFloat(0.6f, 1f), Main.rand.Next(3, 5), temp);
            }
            //不完全燃烧的烟，升温后烧净
            if (heat < 0.55f && (int)Time % 6 == 0) {
                PRTLoader.NewParticle<PRT_WyrmSmoke>(mouth + dir * 14f, dir * Main.rand.NextFloat(1.5f, 3f)
                    , new Color(70, 62, 58) * ((0.55f - heat) * 1.1f), Main.rand.NextFloat(0.14f, 0.22f))
                    ?.Configure(Main.rand.Next(20, 32), 0.06f);
            }
            //蓝焰相偶发白蓝火星
            if (heat > 0.8f && Main.rand.NextBool(3)) {
                PRTLoader.NewParticle<PRT_Spark>(mouth, dir.RotatedBy(Main.rand.NextFloat(-0.35f, 0.35f)) * Main.rand.NextFloat(2.5f, 6f)
                    , Wyrmfire.TempColor(1.08f), Main.rand.NextFloat(0.4f, 0.75f))
                    ?.Configure(false, Main.rand.Next(8, 13));
            }
        }

        /// <summary>松手降温：喉口余焰舔动，丝烟上飘。杖已垂回闲置位，方向跟杖身走</summary>
        private void MouthCoolFX() {
            Vector2 muzzleDir = Projectile.rotation.ToRotationVector2();
            if ((int)Time % 4 == 0) {
                Vector2 od = muzzleDir.RotatedBy(Main.rand.NextFloat(-0.4f, 0.4f));
                PRTLoader.NewParticle<PRT_WyrmTongue>(ShootPos, od * 0.4f, default, Main.rand.NextFloat(0.6f, 1f))
                    ?.Configure(od, Main.rand.NextFloat(0.4f, 0.8f), Main.rand.Next(3, 6), DisplayTemp);
            }
            if ((int)Time % 10 == 0) {
                PRTLoader.NewParticle<PRT_WyrmSmoke>(ShootPos, -Vector2.UnitY * Main.rand.NextFloat(0.3f, 0.7f)
                    , new Color(78, 70, 66) * 0.4f, Main.rand.NextFloat(0.1f, 0.16f))
                    ?.Configure(Main.rand.Next(20, 32), 0.06f);
            }
        }

        /// <summary>凉透熄火：吐一口白烟，掉一粒暗烬，下次开火得重新咳</summary>
        private void Extinguish() {
            ignited = false;
            blueAnnounced = false;
            extinguishGrace = 0;
            NetUpdate();

            SoundEngine.PlaySound(SoundID.LiquidsWaterLava with { Volume = 0.4f, Pitch = 0.35f }, Projectile.Center);
            Vector2 mouth = ShootPos;
            for (int i = 0; i < 5; i++) {
                PRTLoader.NewParticle<PRT_WyrmSmoke>(mouth + Main.rand.NextVector2Circular(4f, 4f)
                    , -Vector2.UnitY * Main.rand.NextFloat(0.6f, 1.5f) + Main.rand.NextVector2Circular(0.5f, 0.3f)
                    , new Color(168, 162, 156) * 0.55f, Main.rand.NextFloat(0.15f, 0.24f))
                    ?.Configure(Main.rand.Next(30, 46), 0.08f);
            }
            PRTLoader.NewParticle<PRT_WyrmEmber>(mouth, Vector2.UnitY * 0.5f, default, 0.7f)
                ?.Configure(Main.rand.Next(14, 20), 0.25f);
        }
        #endregion

        #region 绘制
        public override void GunDraw(Vector2 drawPos, ref Color lightColor) {
            Texture2D tex = TextureValue;
            bool facingRight = DirSign > 0;
            Vector2 origin = facingRight ? GripPx : new Vector2(GripPx.X, tex.Height - GripPx.Y);
            Main.EntitySpriteDraw(tex, drawPos, null, lightColor, Projectile.rotation
                , origin, HeldScale * Projectile.scale
                , facingRight ? SpriteEffects.None : SpriteEffects.FlipVertically);

            float glowTemp = DisplayTemp;
            if (glowTemp <= 0.02f) {
                return;
            }

            Texture2D glow = CWRAsset.SoftGlow.Value;
            Color glowCol = Wyrmfire.TempColor(glowTemp) with { A = 0 };
            float pulse = 0.8f + 0.2f * MathF.Sin(Main.GlobalTimeWrappedHourly * (6f + heat * 9f));
            float alpha = ignited ? 0.36f + 0.5f * heat : coughProgress * 0.35f;

            //喉膛温度光
            Vector2 mouthScreen = drawPos + (ShootPos - Projectile.Center);
            Main.EntitySpriteDraw(glow, mouthScreen, null, glowCol * (alpha * pulse), 0f
                , glow.Size() * 0.5f, 0.42f + heat * 0.3f + pulse * 0.06f, SpriteEffects.None, 0);

            //点燃后的常驻根锥：三条逐帧抖动的火舌直画在嘴上，焰流的根永远咬在龙嘴里
            if (ignited) {
                DrawRootCone(mouthScreen, glowTemp);
            }

            //龙眼随温度亮起
            Vector2 eyeWorld = GetMuzzlePos((EyePx.X - GripPx.X) * HeldScale, (EyePx.Y - GripPx.Y) * HeldScale);
            Main.EntitySpriteDraw(glow, drawPos + (eyeWorld - Projectile.Center), null, glowCol * (alpha * 0.85f), 0f
                , glow.Size() * 0.5f, 0.13f + heat * 0.07f, SpriteEffects.None, 0);

            //蓝焰相的白热内芯
            if (ignited && heat > 0.75f) {
                Color coreCol = Wyrmfire.CoreColor(1.05f) with { A = 0 };
                Main.EntitySpriteDraw(glow, mouthScreen, null, coreCol * ((heat - 0.75f) * 2.4f * pulse), 0f
                    , glow.Size() * 0.5f, 0.2f, SpriteEffects.None, 0);
            }
        }

        /// <summary>
        /// 龙嘴根锥：暗鞘+主舌+白热芯三条 TearFlame01 沿当前杖口方向逐帧抖动。
        /// 开火时全长，松手降温缩成喉口余焰
        /// </summary>
        private void DrawRootCone(Vector2 mouthScreen, float temp) {
            Texture2D flame = FlameTex?.Value;
            if (flame == null) {
                return;
            }

            float jet = MathHelper.Clamp((heat - 0.08f) / 0.2f, 0.3f, 1f);
            float reach = CanFire ? 1f : 0.45f;
            float bright = Wyrmfire.Brightness(temp);
            float aimRot = Projectile.rotation + MathHelper.PiOver2;
            var origin = new Vector2(flame.Width * 0.5f, flame.Height);
            float t = Main.GlobalTimeWrappedHourly * 60f;

            Color mantle = Wyrmfire.MantleColor(temp) with { A = 0 };
            Color body = Wyrmfire.TempColor(temp) with { A = 0 };
            Color core = Wyrmfire.CoreColor(temp) with { A = 0 };

            //三层根舌：外鞘宽而暗、主舌居中、热芯短而亮，抖动相位各自错开
            float j0 = 0.8f + 0.3f * MathF.Sin(t * 0.53f);
            float j1 = 0.8f + 0.3f * MathF.Sin(t * 0.71f + 2.1f);
            float j2 = 0.8f + 0.3f * MathF.Sin(t * 0.64f + 4.3f);
            float len = (0.2f + 0.14f * heat) * reach * jet;
            Main.EntitySpriteDraw(flame, mouthScreen, null, mantle * (0.5f * bright * reach)
                , aimRot + MathF.Sin(t * 0.11f) * 0.12f, origin
                , new Vector2(0.17f, len * 1.15f * j0), SpriteEffects.None, 0);
            Main.EntitySpriteDraw(flame, mouthScreen, null, body * (0.9f * bright * reach)
                , aimRot + MathF.Sin(t * 0.17f + 1.3f) * 0.1f, origin
                , new Vector2(0.12f, len * j1), SpriteEffects.None, 0);
            Main.EntitySpriteDraw(flame, mouthScreen, null, core * (0.8f * bright * reach)
                , aimRot, origin
                , new Vector2(0.08f, len * 0.7f * j2), SpriteEffects.None, 0);
        }
        #endregion
    }
}
