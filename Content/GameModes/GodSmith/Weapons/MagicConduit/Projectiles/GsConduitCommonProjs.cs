using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.MagicConduit.Projectiles
{
    /// <summary>
    /// 通用延时小爆（熔融引爆）。
    /// ai[0] = 半径 + 预设×1024（预设现仅作生成参数保留，半径取模得出），
    /// ai[1] = 引信帧（0 立即起爆），ai[2] = 跟随目标 whoAmI+1（0 不跟随）。
    /// 生成源用 GetSource_Misc：不打标不承签，防增强递归
    /// </summary>
    internal class GsConduitBurstProj : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.InfernoFriendlyBlast;

        private const int BurstWindow = 4;
        private const int AfterglowTicks = 18;

        private float Radius => Projectile.ai[0] % 1024f;
        private int Fuse => (int)Projectile.ai[1];
        private ref float Timer => ref Projectile.localAI[0];
        private bool Bursting => Timer >= Fuse;

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
            if (Timer >= Fuse + BurstWindow + AfterglowTicks) {
                Projectile.Kill();
            }
        }

        private void BurstCue() {
            if (VaultUtils.isServer) {
                return;
            }
            SoundEngine.PlaySound(SoundID.Item14 with { Volume = 0.45f, Pitch = 0.35f, MaxInstances = 5 }, Projectile.Center);
        }

        //只在爆窗内造成伤害
        public override bool? CanDamage() => Bursting && Timer < Fuse + BurstWindow ? null : false;

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
            => GsConduitVFX.CircleVsRect(Projectile.Center, Radius, targetHitbox);

        public override bool PreDraw(ref Color lightColor) {
            //原版爆焰贴图一笔：引信期原尺寸贴附目标作识别，起爆后按判定半径缩放（尺寸提示与判定同源）
            Texture2D tex = TextureAssets.Projectile[Type].Value;
            float scale = Bursting ? Radius * 2f / Math.Max(tex.Width, tex.Height) : 1f;
            Main.EntitySpriteDraw(tex, Projectile.Center - Main.screenPosition, null, lightColor,
                0f, tex.Size() / 2f, scale, SpriteEffects.None, 0);
            return false;
        }
    }

    /// <summary>
    /// N 向射线主控（R2 保留件的右键泄压：蛇发怒视 8 向石化线 / 日冕闪射单向重束）。
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
                coreColor * (0.8f * fade), 0f, glow.Size() / 2f, 0.5f * fade + 0.15f, SpriteEffects.None, 0);
            return false;
        }
    }
}
