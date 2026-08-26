using CalamityOverhaul.Common;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.MagicConduit.Projectiles
{
    /// <summary>
    /// 通用延时小爆（智焰小爆/熔融引爆/泡泡二段）。
    /// ai[0] = 半径 + 色彩预设×1024（0 金橙 / 1 熔橙 / 2 泡蓝），
    /// ai[1] = 引信帧（0 立即起爆），ai[2] = 跟随目标 whoAmI+1（0 不跟随）。
    /// 生成源用 GetSource_Misc：不打标不承签，防增强递归
    /// </summary>
    internal class GsConduitBurstProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private const int BurstWindow = 4;
        private const int AfterglowTicks = 18;

        private float Radius => Projectile.ai[0] % 1024f;
        private int Preset => (int)(Projectile.ai[0] / 1024f);
        private int Fuse => (int)Projectile.ai[1];
        private ref float Timer => ref Projectile.localAI[0];
        private bool Bursting => Timer >= Fuse;

        private (Color bright, Color main, Color deep) Palette => Preset switch {
            1 => (GsConduitVFX.ForgeBright, GsConduitVFX.ForgeMain, GsConduitVFX.ForgeDeep),
            2 => (GsConduitVFX.SeaBright, GsConduitVFX.SeaMain, GsConduitVFX.SeaDeep),
            _ => (new Color(255, 236, 170), new Color(255, 188, 82), new Color(140, 84, 20)),
        };

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 16;
            Projectile.friendly = true;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.DamageType = DamageClass.Magic;
            Projectile.penetrate = -1;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
            Projectile.timeLeft = 300;
        }

        public override bool ShouldUpdatePosition() => false;

        public override void AI() {
            //引信期贴附目标（各端读同步的 NPC 位置，确定性一致）；目标亡则停在原地
            int follow = (int)Projectile.ai[2] - 1;
            if (!Bursting && follow >= 0 && follow < Main.maxNPCs) {
                NPC npc = Main.npc[follow];
                if (npc.active && !npc.dontTakeDamage) {
                    Projectile.Center = npc.Center;
                }
            }

            if (Timer == Fuse) {
                BurstCue();
            }
            Timer++;
            if (Bursting) {
                Lighting.AddLight(Projectile.Center, Palette.main.ToVector3() * 0.6f * (1f - BurstProgress()));
            }
            if (Timer >= Fuse + BurstWindow + AfterglowTicks) {
                Projectile.Kill();
            }
        }

        private float BurstProgress() => MathHelper.Clamp((Timer - Fuse) / (float)(BurstWindow + AfterglowTicks), 0f, 1f);

        private void BurstCue() {
            if (VaultUtils.isServer) {
                return;
            }
            (Color bright, Color main, _) = Palette;
            SoundEngine.PlaySound(SoundID.Item14 with { Volume = 0.45f, Pitch = 0.35f, MaxInstances = 5 }, Projectile.Center);
            PRTLoader.NewParticle<PRT_Light>(Projectile.Center, Vector2.Zero, bright, 0.10f + Radius * 0.0016f)?.Configure(9, 0.85f);
            int sparks = Math.Min(8, 4 + (int)(Radius / 30f));
            for (int i = 0; i < sparks; i++) {
                PRTLoader.NewParticle<PRT_Spark>(Projectile.Center,
                    Main.rand.NextVector2CircularEdge(1f, 1f) * Main.rand.NextFloat(2.5f, 5.5f),
                    Main.rand.NextBool() ? bright : main, Main.rand.NextFloat(0.3f, 0.5f))?.Configure(true, Main.rand.Next(12, 20));
            }
        }

        //只在爆窗内造成伤害
        public override bool? CanDamage() => Bursting && Timer < Fuse + BurstWindow ? null : false;

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
            => GsConduitVFX.CircleVsRect(Projectile.Center, Radius, targetHitbox);

        public override bool PreDraw(ref Color lightColor) {
            (Color bright, Color main, Color deep) = Palette;
            if (!Bursting) {
                //引信读数：贴附目标的微光心跳（识别点）
                float pulse = 0.6f + 0.4f * MathF.Sin(Timer * 0.7f + Projectile.identity * 0.83f);
                Texture2D glow = CWRAsset.SoftGlow.Value;
                Main.EntitySpriteDraw(glow, Projectile.Center - Main.screenPosition, null,
                    main with { A = 0 } * (0.5f * pulse), 0f, glow.Size() / 2f, 0.34f, Microsoft.Xna.Framework.Graphics.SpriteEffects.None, 0);
                return false;
            }
            float p = BurstProgress();
            float ringR = Radius * VaultUtils.EaseOutCubic(MathHelper.Clamp(p * 1.6f, 0f, 1f));
            ShockRingDraw.Draw(Main.spriteBatch, Projectile.Center, ringR, 12f,
                bright, main, deep, (1f - p) * 0.9f, innerGlow: 0.35f, timeSeed: Projectile.identity * 0.31f);
            return false;
        }
    }

    /// <summary>
    /// 通用环爆（炉心喷发/磁暴/血爆/风暴眼散爆）。波前环带判定与可见冲击环同源。
    /// ai[0] = 最大半径 + 色彩预设×1024（0 炉橙 / 1 磁品红 / 2 血红 / 3 靛蓝），
    /// ai[1] = 1 时向心击退（非 Boss 拉向环心，走原版击退同步）。伤害生成时烘焙
    /// </summary>
    internal class GsConduitNovaProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private const int ExpandTicks = 12;
        private const int TotalTicks = 26;
        private const float BandHalf = 26f;

        private float MaxRadius => Projectile.ai[0] % 1024f;
        private int Preset => (int)(Projectile.ai[0] / 1024f);
        private bool Attract => Projectile.ai[1] == 1f;
        private ref float Timer => ref Projectile.localAI[0];

        private float CurRadius => MaxRadius * VaultUtils.EaseOutCubic(MathHelper.Clamp(Timer / ExpandTicks, 0f, 1f));

        private (Color bright, Color main, Color deep) Palette => Preset switch {
            1 => (GsConduitVFX.MagnetBright, GsConduitVFX.MagnetMain, GsConduitVFX.MagnetDeep),
            2 => (GsConduitVFX.BloodBright, GsConduitVFX.BloodMain, GsConduitVFX.BloodDeep),
            3 => (GsConduitVFX.SeaBright, new Color(72, 110, 224), new Color(24, 32, 96)),
            _ => (GsConduitVFX.ForgeBright, GsConduitVFX.ForgeMain, GsConduitVFX.ForgeDeep),
        };

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 16;
            Projectile.friendly = true;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.DamageType = DamageClass.Magic;
            Projectile.penetrate = -1;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
            Projectile.timeLeft = 60;
        }

        public override bool ShouldUpdatePosition() => false;

        public override void AI() {
            if (Timer == 0 && !VaultUtils.isServer) {
                (Color bright, Color main, _) = Palette;
                SoundEngine.PlaySound(SoundID.Item14 with { Volume = 0.8f, Pitch = -0.15f, MaxInstances = 4 }, Projectile.Center);
                PRTLoader.NewParticle<PRT_Light>(Projectile.Center, Vector2.Zero, bright, 0.22f)?.Configure(10, 0.9f);
                for (int i = 0; i < 10; i++) {
                    Vector2 dir = Main.rand.NextVector2CircularEdge(1f, 1f);
                    PRTLoader.NewParticle<PRT_Spark>(Projectile.Center + dir * 10f, dir * Main.rand.NextFloat(4f, 9f),
                        Main.rand.NextBool() ? bright : main, Main.rand.NextFloat(0.35f, 0.6f))?.Configure(true, Main.rand.Next(14, 24));
                }
            }
            Timer++;
            Lighting.AddLight(Projectile.Center, Palette.main.ToVector3() * (0.8f * (1f - Timer / TotalTicks)));
            if (Timer >= TotalTicks) {
                Projectile.Kill();
            }
        }

        //只在波前扩张期造成伤害
        public override bool? CanDamage() => Timer <= ExpandTicks ? null : false;

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            //环带判定：只判正在扫过的波前（内侧已被更早的波前扫过）
            float nx = MathHelper.Clamp(Projectile.Center.X, targetHitbox.Left, targetHitbox.Right);
            float ny = MathHelper.Clamp(Projectile.Center.Y, targetHitbox.Top, targetHitbox.Bottom);
            float dist = new Vector2(nx - Projectile.Center.X, ny - Projectile.Center.Y).Length();
            return MathF.Abs(dist - CurRadius) <= BandHalf;
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {
            if (!Attract || target.boss) {
                return;
            }
            //向心击退：把非 Boss 推向环心（吸附感），方向走原版击退同步
            modifiers.HitDirectionOverride = target.Center.X < Projectile.Center.X ? 1 : -1;
            modifiers.Knockback *= 1.4f;
        }

        public override bool PreDraw(ref Color lightColor) {
            (Color bright, Color main, Color deep) = Palette;
            float fade = 1f - MathHelper.Clamp(Timer / TotalTicks, 0f, 1f);
            ShockRingDraw.Draw(Main.spriteBatch, Projectile.Center, CurRadius, 22f,
                bright, main, deep, fade * 0.95f, innerGlow: 0.3f, timeSeed: Projectile.identity * 0.47f);
            return false;
        }
    }

    /// <summary>
    /// N 向射线主控（蛇发怒视 8 向石化线 / 最后棱镜分光 3 向短爆束 / 日冕闪射单向重束）。
    /// 单主控自判 N 条线段，判定线宽与可见亮体同源（×0.75 内收）。
    /// ai[0] = 向数，ai[1] = 基准角，ai[2] = 预设（0 石绿 / 1 虹彩 / 2 日冕重束）。伤害生成时烘焙
    /// </summary>
    internal class GsConduitRayProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private const int GrowTicks = 4;
        private const int FullTicks = 12;
        private const int TotalTicks = 22;

        private int RayCount => Math.Clamp((int)Projectile.ai[0], 1, 12);
        private float BaseAngle => Projectile.ai[1];
        private int Preset => (int)Projectile.ai[2];
        private ref float Timer => ref Projectile.localAI[0];

        private float RayLength => Preset switch { 0 => 420f, 2 => 900f, _ => 330f };
        private float RayWidth => Preset switch { 0 => 16f, 2 => 40f, _ => 20f };

        private float LengthProgress => VaultUtils.EaseOutCubic(MathHelper.Clamp(Timer / GrowTicks, 0f, 1f));
        private float WidthFade => Timer <= FullTicks ? 1f
            : 1f - VaultUtils.EaseInQuad(MathHelper.Clamp((Timer - FullTicks) / (TotalTicks - FullTicks), 0f, 1f));

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 16;
            Projectile.friendly = true;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.DamageType = DamageClass.Magic;
            Projectile.penetrate = -1;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
            Projectile.timeLeft = 60;
        }

        public override bool ShouldUpdatePosition() => false;

        private Vector2 RayDir(int i) => (BaseAngle + MathHelper.TwoPi * i / RayCount).ToRotationVector2();

        public override void AI() {
            if (Timer == 0 && !VaultUtils.isServer) {
                SoundStyle cue = Preset switch {
                    0 => SoundID.Item71 with { Volume = 0.85f, Pitch = -0.35f },
                    2 => SoundID.Item74 with { Volume = 0.95f, Pitch = -0.2f },
                    _ => SoundID.Item29 with { Volume = 0.8f, Pitch = 0.25f },
                };
                SoundEngine.PlaySound(cue, Projectile.Center);
            }
            Timer++;
            for (int i = 0; i < RayCount; i++) {
                Lighting.AddLight(Projectile.Center + RayDir(i) * RayLength * LengthProgress * 0.6f,
                    RayColor(i).ToVector3() * 0.35f * WidthFade);
            }
            if (Timer >= TotalTicks) {
                Projectile.Kill();
            }
        }

        //展开完成到维持末的相位闩锁伤害窗
        public override bool? CanDamage() => Timer >= 3f && Timer <= FullTicks + 1 ? null : false;

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            float point = 0f;
            float len = RayLength * LengthProgress;
            for (int i = 0; i < RayCount; i++) {
                if (Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(),
                    Projectile.Center, Projectile.Center + RayDir(i) * len, RayWidth * 0.75f, ref point)) {
                    return true;
                }
            }
            return false;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            //石化射线：叠 2 层石纹（只在攻击方端执行，触发结果走 buff 同步）
            if (Preset == 0) {
                GsConduitVFX.ApplyPetrify(target, 2);
            }
        }

        private Color RayColor(int i) {
            if (Preset == 0) {
                return GsConduitVFX.StoneMain;
            }
            if (Preset == 2) {
                return GsConduitVFX.ForgeMain;
            }
            //虹彩逐向色散（identity 定相，绘制路径零随机）
            return Main.hslToRgb((i / (float)RayCount + Projectile.identity * 0.137f) % 1f, 0.78f, 0.62f);
        }

        private Color BrightEdge => Preset switch {
            0 => GsConduitVFX.StoneBright,
            2 => GsConduitVFX.ForgeBright,
            _ => Color.White,
        };

        public override bool PreDraw(ref Color lightColor) {
            float len = RayLength * LengthProgress;
            float fade = WidthFade;
            if (fade <= 0.02f || len < 8f) {
                return false;
            }
            for (int i = 0; i < RayCount; i++) {
                GsConduitVFX.DrawBeam(Main.spriteBatch, Projectile.Center,
                    RayDir(i).ToRotation(), len, RayWidth * fade, RayColor(i), BrightEdge, fade);
            }
            //中心辉光收口
            Texture2D glow = CWRAsset.SoftGlow.Value;
            Color coreColor = BrightEdge with { A = 0 };
            Main.EntitySpriteDraw(glow, Projectile.Center - Main.screenPosition, null,
                coreColor * (0.8f * fade), 0f, glow.Size() / 2f, 0.5f * fade + 0.15f, Microsoft.Xna.Framework.Graphics.SpriteEffects.None, 0);
            return false;
        }
    }
}
