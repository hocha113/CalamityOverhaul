using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.Martians.Projectiles
{
    /// <summary>
    /// 死亡射线扫描线预演：ai[0]=飞碟核心索引（类型校验 MartianSaucerCore）。
    /// 二阶段全程锚在核心正下方画竖直扫描列——死亡射线唯一可能出现的位置；
    /// 核心横移时外加沿移动方向的前导刻线（预告射线即将扫过的路径）；
    /// 侦测到原版死亡射线在场时增亮为警报形态。只加预告，不读写核心 ai、不改原版射线；
    /// 永不造成伤害
    /// </summary>
    internal class MrtSaucerScanProj : ModProjectile
    {
        public override string Texture => CWRConstant.Masking + "MaskLaserLine";

        /// <summary>扫描列长度（自核心向下）</summary>
        private const float ColumnLength = 1600f;
        /// <summary>前导刻线的提前量帧数：刻线画在核心当前横速 × 此帧数的位置</summary>
        private const float ScanLeadFrames = 30f;
        /// <summary>前导刻线长度</summary>
        private const float LeadTickLength = 420f;
        /// <summary>死亡射线在场侦测半径与节流帧</summary>
        private const float DeathrayDetectRange = 700f;
        private const int DetectInterval = 5;

        private static readonly Color ScanMagenta = new(255, 96, 190, 0);

        private int CoreIndex => (int)Projectile.ai[0];
        private bool RayLive => Projectile.localAI[1] == 1f;

        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 1800;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 16;
            Projectile.hostile = false;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 60;
            Projectile.netImportant = true;
        }

        public override bool ShouldUpdatePosition() => false;

        public override void AI() {
            //核心索引+类型双校验：核心没了（战斗结束）→ 预演线随之消散
            NPC core = CoreIndex >= 0 && CoreIndex < Main.maxNPCs ? Main.npc[CoreIndex] : null;
            if (core == null || !core.active || core.type != NPCID.MartianSaucerCore) {
                Projectile.Kill();
                return;
            }
            //自续留存：核心在场则一直预演（二阶段不可逆，无需权威端反复决策）
            Projectile.timeLeft = 60;
            Projectile.Center = core.Center;

            if (Projectile.localAI[0] == 0f && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item93 with { Volume = 0.5f, Pitch = -0.6f, MaxInstances = 3 }, Projectile.Center);
            }

            //死亡射线在场侦测（节流；各端从同步的弹幕数组确定性得到相同结论）
            if ((int)Projectile.localAI[0] % DetectInterval == 0) {
                bool live = false;
                for (int i = 0; i < Main.maxProjectiles; i++) {
                    Projectile proj = Main.projectile[i];
                    if (proj.active && proj.type == ProjectileID.SaucerDeathray
                        && Vector2.DistanceSquared(proj.Center, Projectile.Center) <= DeathrayDetectRange * DeathrayDetectRange) {
                        live = true;
                        break;
                    }
                }
                Projectile.localAI[1] = live ? 1f : 0f;
            }
            Projectile.localAI[0]++;

            if (!VaultUtils.isServer) {
                Lighting.AddLight(Projectile.Center + Vector2.UnitY * 120f,
                    ScanMagenta.R / 255f * 0.1f, ScanMagenta.G / 255f * 0.1f, ScanMagenta.B / 255f * 0.1f);
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            NPC core = CoreIndex >= 0 && CoreIndex < Main.maxNPCs ? Main.npc[CoreIndex] : null;
            if (core == null || !core.active) {
                return false;
            }

            Texture2D tex = TextureAssets.Projectile[Type].Value;
            Vector2 origin = new Vector2(0f, tex.Height / 2f);
            Vector2 basePos = core.Center + new Vector2(0f, core.gfxOffY) - Main.screenPosition;
            float fadeIn = MathHelper.Clamp(Projectile.localAI[0] / 20f, 0f, 1f);
            float scaleX = ColumnLength / tex.Width;
            float pulse = 0.72f + 0.28f * MathF.Sin(Main.GlobalTimeWrappedHourly * 9f + Projectile.identity);

            if (RayLive) {
                //警报形态：射线正在场，扫描列全亮 + 白热芯
                Main.EntitySpriteDraw(tex, basePos, null, ScanMagenta * (0.55f * fadeIn), MathHelper.PiOver2,
                    origin, new Vector2(scaleX, 70f / tex.Height), SpriteEffects.None, 0);
                Main.EntitySpriteDraw(tex, basePos, null, new Color(255, 235, 245, 0) * (0.5f * fadeIn * pulse), MathHelper.PiOver2,
                    origin, new Vector2(scaleX, 22f / tex.Height), SpriteEffects.None, 0);
            }
            else {
                //常态预演：淡扫描列标出死亡射线的唯一落位
                Main.EntitySpriteDraw(tex, basePos, null, ScanMagenta * (0.3f * fadeIn * pulse), MathHelper.PiOver2,
                    origin, new Vector2(scaleX, 34f / tex.Height), SpriteEffects.None, 0);
                Main.EntitySpriteDraw(tex, basePos, null, ScanMagenta * (0.16f * fadeIn), MathHelper.PiOver2,
                    origin, new Vector2(scaleX, 12f / tex.Height), SpriteEffects.None, 0);
            }

            //前导刻线：标出射线列即将平移扫过的位置（横速可观时才有意义）
            float leadX = core.velocity.X * ScanLeadFrames;
            if (Math.Abs(leadX) > 24f) {
                Vector2 leadPos = basePos + new Vector2(leadX, 40f);
                Main.EntitySpriteDraw(tex, leadPos, null, new Color(255, 200, 230, 0) * (0.28f * fadeIn * pulse), MathHelper.PiOver2,
                    origin, new Vector2(LeadTickLength / tex.Width, 10f / tex.Height), SpriteEffects.None, 0);
            }

            //核心处的扫描座标记
            Texture2D glow = CWRAsset.SoftGlow.Value;
            Main.EntitySpriteDraw(glow, basePos, null, ScanMagenta * ((RayLive ? 0.8f : 0.45f) * fadeIn * pulse),
                0f, glow.Size() / 2f, 0.6f, SpriteEffects.None, 0);
            return false;
        }

        public override void DrawBehind(int index, List<int> behindNPCsAndTiles, List<int> behindNPCs,
            List<int> behindProjectiles, List<int> overPlayers, List<int> overWiresUI) => behindNPCsAndTiles.Add(index);
    }
}
