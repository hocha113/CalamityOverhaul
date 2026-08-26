using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.Martians.Projectiles
{
    /// <summary>
    /// 飞碟相位弱点·过热标记：ai[0]=部件 NPC 索引，ai[1]=部件类型（索引+类型双校验），
    /// ai[2]=核心索引+1（归属计数用，核心以此保证同时至多一个签名技进行中）。
    /// 升温 <see cref="RampFrames"/>（≥40）帧 → 过热保持 <see cref="HoldFrames"/> 帧
    /// （此窗口该部件受伤加深，攻击方本机经 <see cref="IsVulnerable"/> 读窗）→ 冷却消散。
    /// 标记随核心轮转迁移到下一门炮（轮转可见）。本实体永不伤害玩家、不改部件任何数据
    /// </summary>
    internal class MrtOverheatMarkProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder2;

        /// <summary>升温帧（签名技预告 ≥40，各档位一律不缩短）</summary>
        internal const int RampFrames = 42;
        /// <summary>过热保持帧（易伤窗=发光保持窗）</summary>
        internal const int HoldFrames = 240;
        internal const int FadeFrames = 12;
        internal const int TotalLifeFrames = RampFrames + HoldFrames + FadeFrames;

        private static readonly Color HeatOrange = new(255, 150, 60);

        private int PartIndex => (int)Projectile.ai[0];
        private int PartType => (int)Projectile.ai[1];
        private int Elapsed => TotalLifeFrames - Projectile.timeLeft;
        private bool InVulnWindow => Elapsed >= RampFrames && Elapsed < RampFrames + HoldFrames;

        /// <summary>
        /// 攻击方本机判窗：该部件当前是否处于过热易伤窗（标记实体已同步，索引+类型双校验）
        /// </summary>
        internal static bool IsVulnerable(int npcIndex, int npcType) {
            int type = ModContent.ProjectileType<MrtOverheatMarkProj>();
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile proj = Main.projectile[i];
                if (proj.active && proj.type == type && (int)proj.ai[0] == npcIndex && (int)proj.ai[1] == npcType
                    && proj.ModProjectile is MrtOverheatMarkProj mark && mark.InVulnWindow) {
                    return true;
                }
            }
            return false;
        }

        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 320;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 24;
            Projectile.hostile = false;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = TotalLifeFrames;
            Projectile.netImportant = true;
        }

        public override bool ShouldUpdatePosition() => false;

        public override void AI() {
            //部件索引+类型双校验：部件被打掉 → 标记随之消散（轮转由核心择时重启）
            NPC part = PartIndex >= 0 && PartIndex < Main.maxNPCs ? Main.npc[PartIndex] : null;
            if (part == null || !part.active || part.type != PartType) {
                Projectile.Kill();
                return;
            }
            Projectile.Center = part.Center;

            int elapsed = Elapsed;
            if (elapsed == RampFrames && !VaultUtils.isServer) {
                //过热点亮：提示集火窗开启
                SoundEngine.PlaySound(SoundID.Item93 with { Volume = 0.5f, Pitch = 0.55f, MaxInstances = 3 }, Projectile.Center);
            }

            if (VaultUtils.isServer) {
                return;
            }

            float heat = HeatFactor(elapsed);
            Lighting.AddLight(Projectile.Center, HeatOrange.ToVector3() * (0.5f * heat));
            //升温/保持期的热浪粒子（≤1 粒/帧）
            if (heat > 0.2f && Main.rand.NextBool(2)) {
                Dust dust = Dust.NewDustPerfect(part.Center + Main.rand.NextVector2Circular(part.width * 0.4f, part.height * 0.4f),
                    DustID.Torch, new Vector2(0f, -Main.rand.NextFloat(0.8f, 2.2f) * heat), 100, default, 0.9f + 0.7f * heat);
                dust.noGravity = true;
            }
        }

        /// <summary>热度 0~1：升温线性爬升 → 保持满热 → 消散回落（易伤窗=满热窗）</summary>
        private float HeatFactor(int elapsed) {
            if (elapsed < RampFrames) {
                return elapsed / (float)RampFrames * 0.65f;
            }
            if (elapsed < RampFrames + HoldFrames) {
                return 1f;
            }
            return MathHelper.Clamp(1f - (elapsed - RampFrames - HoldFrames) / (float)FadeFrames, 0f, 1f);
        }

        public override bool PreDraw(ref Color lightColor) {
            NPC part = PartIndex >= 0 && PartIndex < Main.maxNPCs ? Main.npc[PartIndex] : null;
            if (part == null || !part.active) {
                return false;
            }
            int elapsed = Elapsed;
            float heat = HeatFactor(elapsed);
            if (heat <= 0.02f) {
                return false;
            }

            //过热辉光敷在部件本体上（实体感由部件 NPC 贴图自身提供），补 gfxOffY
            Texture2D glow = CWRAsset.SoftGlow.Value;
            Vector2 drawPos = part.Center + new Vector2(0f, part.gfxOffY) - Main.screenPosition;
            float pulse = 0.8f + 0.2f * MathF.Sin(Main.GlobalTimeWrappedHourly * (InVulnWindow ? 18f : 8f) + Projectile.identity);
            float baseScale = Math.Max(part.width, part.height) / (float)glow.Width * 2.6f;

            Main.EntitySpriteDraw(glow, drawPos, null, HeatOrange with { A = 0 } * (0.7f * heat * pulse),
                0f, glow.Size() / 2f, baseScale * (0.8f + 0.25f * heat), SpriteEffects.None, 0);
            if (InVulnWindow) {
                //白热芯：易伤窗的明确视觉标识
                Main.EntitySpriteDraw(glow, drawPos, null, new Color(255, 235, 200, 0) * (0.5f * pulse),
                    0f, glow.Size() / 2f, baseScale * 0.4f, SpriteEffects.None, 0);
            }
            return false;
        }
    }
}
