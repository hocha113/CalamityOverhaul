using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.MagicConduit.Projectiles
{
    /// <summary>
    /// 充能爆破炮引导通道（原版按住充能玩法引导化，item 2882 channel=true 读 Owner.channel）。
    /// 低热每 19t 喷一枚电浆弹；白热升格为每 14t 一段「聚能弧束」短粗脉冲
    /// （脉冲窗内束线判定，与可见束同源）；引导持续积热，触顶走 Lock 政策的「湮灭炮」
    /// </summary>
    internal class GsChargedBlasterCannonHeldProj : GsConduitHeldProj
    {
        internal static readonly Color PlasmaBright = new(170, 240, 255);
        internal static readonly Color PlasmaMain = new(66, 178, 240);
        internal static readonly Color PlasmaDeep = new(20, 62, 120);

        private const float PulseLength = 520f;
        private const float PulseWidth = 26f;
        private const int PulseWindow = 6;

        public override string LocalizationCategory => "GodSmithMagicConduit";

        protected override int BoundItemID => ItemID.ChargedBlasterCannon;
        protected override float ManaPerSecond => 9f;
        protected override float HeatPerTick => 0.8f;
        protected override int HitCooldown => 8;
        protected override float TickDamageCoef => 0.5f;
        protected override float MuzzleOffset => 26f;

        /// <summary>炮体沿瞄准，炮口钉在束根（原版由宿主 460 自绘，物品贴图口朝右）</summary>
        protected override GsConduitBodyPose BodyPose => GsConduitBodyPose.MuzzleAimed;

        /// <summary>发射节拍：白热 14t 弧束脉冲 / 低热 19t 电浆弹</summary>
        private int CycleTicks => HeatStageSync >= 1 ? 14 : 19;

        /// <summary>脉冲相内帧（localAI[1] 由基类各端同步自增，节拍确定性一致）</summary>
        private int CyclePhase => (int)Projectile.localAI[1] % CycleTicks;

        /// <summary>白热弧束的脉冲窗（判定与绘制同源）</summary>
        private bool InPulse => HeatStageSync >= 1 && CyclePhase < PulseWindow && Projectile.localAI[1] > 8f;

        private readonly float[] laserSamples = new float[3];
        private float beamLength = PulseLength;

        protected override void ChannelAI(float collapse01) {
            //脉冲落点探地（tile 各端一致）
            Vector2 dir = AimUnit;
            Collision.LaserScan(Projectile.Center, dir, PulseWidth * 0.5f, PulseLength, laserSamples);
            beamLength = (laserSamples[0] + laserSamples[1] + laserSamples[2]) / 3f;

            if (Projectile.localAI[1] == 1f && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item15 with { Volume = 0.6f, Pitch = -0.3f }, Projectile.Center);
            }

            //节拍事件：各端从同步热段 + 本地相位一致推进
            if (CyclePhase == 0 && Projectile.localAI[1] > 8f && collapse01 <= 0f) {
                if (HeatStageSync >= 1) {
                    //弧束脉冲起点：全端音画
                    if (!VaultUtils.isServer) {
                        SoundEngine.PlaySound(SoundID.Item94 with { Volume = 0.5f, Pitch = -0.1f, MaxInstances = 4 }, Projectile.Center);
                        PRTLoader.NewParticle<PRT_Light>(Projectile.Center, Vector2.Zero, PlasmaBright, 0.14f)?.Configure(8, 0.8f);
                    }
                }
                else if (Projectile.IsOwnedByLocalPlayer()) {
                    //低热电浆弹：owner 端生成（0.5 倍）
                    int orbDamage = Math.Max(1, (int)(Owner.GetWeaponDamage(Owner.HeldItem) * 0.5f));
                    Projectile.NewProjectile(Owner.GetSource_Misc("GsBlasterPulse"), Projectile.Center,
                        AimUnit * 11f, ProjectileID.ChargedBlasterOrb, orbDamage, 3f, Projectile.owner);
                    if (!VaultUtils.isServer) {
                        SoundEngine.PlaySound(SoundID.Item91 with { Volume = 0.45f, Pitch = 0.1f, MaxInstances = 4 }, Projectile.Center);
                    }
                }
            }

            if (VaultUtils.isServer) {
                return;
            }
            Lighting.AddLight(Projectile.Center, PlasmaMain.ToVector3() * (HeatStageSync >= 1 ? 0.5f : 0.3f));
            //炮口电弧读数：充能越满弧越密
            if (Main.GameUpdateCount % (HeatStageSync >= 1 ? 2 : 5) == 0) {
                PRTLoader.NewParticle<PRT_Spark>(Projectile.Center + Main.rand.NextVector2Circular(6f, 6f),
                    AimUnit.RotatedBy(Main.rand.NextFloat(-0.5f, 0.5f)) * Main.rand.NextFloat(0.6f, 1.6f),
                    Main.rand.NextBool() ? PlasmaMain : PlasmaBright,
                    Main.rand.NextFloat(0.16f, 0.28f))?.Configure(false, Main.rand.Next(6, 12));
            }
        }

        //只有白热弧束脉冲窗造成本体伤害；低热伤害全由电浆弹承担
        protected override bool? DamageGate() => InPulse ? null : false;

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            if (!InPulse) {
                return false;
            }
            float point = 0f;
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(),
                Projectile.Center, Projectile.Center + AimUnit * beamLength, PulseWidth * 0.7f, ref point);
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (VaultUtils.isServer) {
                return;
            }
            for (int i = 0; i < 3; i++) {
                PRTLoader.NewParticle<PRT_Spark>(target.Center + Main.rand.NextVector2Circular(7f, 7f),
                    Main.rand.NextVector2Circular(2.2f, 2.2f), PlasmaBright,
                    Main.rand.NextFloat(0.22f, 0.36f))?.Configure(true, Main.rand.Next(10, 16));
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            //先画炮体，脉冲束与炮口充能核压在其上
            DrawWeaponBody();
            SpriteBatch sb = Main.spriteBatch;
            Texture2D glow = CWRAsset.SoftGlow.Value;
            Vector2 muzzle = Projectile.Center - Main.screenPosition;
            float flick = 1f + 0.09f * MathF.Sin(Main.GlobalTimeWrappedHourly * 43f + Projectile.identity * 0.58f);

            //弧束脉冲：脉冲窗内三层断续束（窗内宽度先胀后收，判定同窗）
            if (InPulse) {
                float p = CyclePhase / (float)PulseWindow;
                float w = PulseWidth * MathF.Sin(p * MathHelper.Pi) * flick;
                if (w > 1f) {
                    GsConduitVFX.DrawBeam(sb, Projectile.Center, AimUnit.ToRotation(), beamLength, w,
                        PlasmaMain, PlasmaBright);
                    Vector2 impact = Projectile.Center + AimUnit * beamLength - Main.screenPosition;
                    sb.Draw(glow, impact, null, PlasmaBright with { A = 0 } * 0.8f, 0f,
                        glow.Size() / 2f, w / 40f, SpriteEffects.None, 0f);
                }
            }
            //炮口充能核：热段越高越亮
            float charge = HeatStageSync >= 1 ? 1f : 0.6f;
            sb.Draw(glow, muzzle, null, PlasmaDeep with { A = 0 } * (0.7f * charge), 0f,
                glow.Size() / 2f, 0.6f * charge * flick, SpriteEffects.None, 0f);
            sb.Draw(glow, muzzle, null, PlasmaBright with { A = 0 } * (0.6f * charge), 0f,
                glow.Size() / 2f, 0.28f * charge, SpriteEffects.None, 0f);
            return false;
        }
    }

    /// <summary>
    /// 湮灭炮：过载触发的巨型电浆球。缓速贯穿推进，
    /// owner 端每 20t 向近敌舔一道电弧小弹（0.3 倍原版爆破激光）；
    /// 自绘三层电浆辉体 + 差速旋弧
    /// </summary>
    internal class GsChargedBlasterCannonOrbProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        public override string LocalizationCategory => "GodSmithMagicConduit";

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 56;
            Projectile.friendly = true;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.DamageType = DamageClass.Magic;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 20;
            Projectile.timeLeft = 150;
        }

        public override void AI() {
            Projectile.velocity *= 0.995f;
            Projectile.rotation += 0.04f;

            //电弧舔舐：owner 端每 20t 向近敌甩一道小弧
            if (Projectile.IsOwnedByLocalPlayer() && Projectile.timeLeft % 20 == 0) {
                NPC prey = Projectile.Center.FindClosestNPC(420f);
                if (prey != null) {
                    int arcDamage = Math.Max(1, (int)(Projectile.damage * 0.3f));
                    Vector2 dir = (prey.Center - Projectile.Center).SafeNormalize(Vector2.UnitX);
                    Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center, dir * 12f,
                        ProjectileID.ChargedBlasterLaser, arcDamage, 1.5f, Projectile.owner);
                }
            }

            if (VaultUtils.isServer) {
                return;
            }
            Lighting.AddLight(Projectile.Center, GsChargedBlasterCannonHeldProj.PlasmaMain.ToVector3() * 0.6f);
            if (Projectile.timeLeft % 3 == 0) {
                PRTLoader.NewParticle<PRT_Spark>(Projectile.Center + Main.rand.NextVector2Circular(20f, 20f),
                    Main.rand.NextVector2Circular(1.2f, 1.2f) - Projectile.velocity * 0.1f,
                    GsChargedBlasterCannonHeldProj.PlasmaBright,
                    Main.rand.NextFloat(0.2f, 0.34f))?.Configure(false, Main.rand.Next(8, 14));
            }
        }

        public override void OnKill(int timeLeft) {
            if (VaultUtils.isServer) {
                return;
            }
            SoundEngine.PlaySound(SoundID.Item94 with { Volume = 0.8f, Pitch = -0.4f }, Projectile.Center);
            for (int i = 0; i < 10; i++) {
                PRTLoader.NewParticle<PRT_Spark>(Projectile.Center,
                    Main.rand.NextVector2CircularEdge(1f, 1f) * Main.rand.NextFloat(2.5f, 6f),
                    GsChargedBlasterCannonHeldProj.PlasmaMain,
                    Main.rand.NextFloat(0.3f, 0.5f))?.Configure(true, Main.rand.Next(14, 24));
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            //三层电浆辉体 + 差速旋弧（A=0 加色，identity 定相脉动）
            Texture2D glow = CWRAsset.SoftGlow.Value;
            Texture2D star = CWRAsset.StarTexture.Value;
            Vector2 pos = Projectile.Center - Main.screenPosition;
            float t = Main.GlobalTimeWrappedHourly;
            float seed = Projectile.identity * 0.49f;
            float pulse = 0.85f + 0.15f * MathF.Sin(t * 7f + seed);
            Main.EntitySpriteDraw(glow, pos, null,
                GsChargedBlasterCannonHeldProj.PlasmaDeep with { A = 0 } * (0.7f * pulse), 0f,
                glow.Size() / 2f, 1.1f * pulse, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(glow, pos, null,
                GsChargedBlasterCannonHeldProj.PlasmaMain with { A = 0 } * (0.65f * pulse), 0f,
                glow.Size() / 2f, 0.65f * pulse, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(glow, pos, null, Color.White with { A = 0 } * (0.5f * pulse), 0f,
                glow.Size() / 2f, 0.3f, SpriteEffects.None, 0);
            for (int i = 0; i < 3; i++) {
                float ang = t * (2.4f + i * 0.8f) * (i % 2 == 0 ? 1f : -1f) + seed + i * 2.1f;
                Main.EntitySpriteDraw(star, pos + ang.ToRotationVector2() * 26f, null,
                    GsChargedBlasterCannonHeldProj.PlasmaBright with { A = 0 } * 0.5f, ang,
                    star.Size() / 2f, 0.12f, SpriteEffects.None, 0);
            }
            return false;
        }
    }
}
