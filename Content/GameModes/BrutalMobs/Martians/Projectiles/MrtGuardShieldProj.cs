using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.Martians.Projectiles
{
    /// <summary>
    /// 军官护盾链接·单次格挡护罩：ai[0]=受护 NPC 索引，ai[1]=受护 NPC 类型（索引+类型双校验），
    /// ai[2]=军官索引+1（仅链接尘线视觉用）。存续 <see cref="GuardFrames"/> 帧，
    /// 期间受护者首次掉血即视为格挡消耗，泡壳破裂消散（单次格挡语义）。
    /// 格挡判定在攻击方本机经 <see cref="IsGuarding"/> 读窗，窗口证据=本已同步实体；
    /// 破裂由各端对原生同步的 npc.life 自行观测得出同一结论，无需额外包。
    /// 本实体永不伤害、不改受护者任何数据
    /// </summary>
    internal class MrtGuardShieldProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder2;

        /// <summary>护罩存续帧（任务口径 60 帧，档位不改）</summary>
        internal const int GuardFrames = 60;
        /// <summary>破裂/到期后的消散帧</summary>
        private const int PopFadeFrames = 10;
        /// <summary>链接尘线可见帧（出生初帧军官→受护者，支援关系可读）</summary>
        private const int TetherFrames = 14;

        private static readonly Color ShellCyan = new(140, 220, 255);

        private int TargetIndex => (int)Projectile.ai[0];
        private int TargetType => (int)Projectile.ai[1];
        private int OfficerIndex => (int)Projectile.ai[2] - 1;
        private int Elapsed => GuardFrames + PopFadeFrames - Projectile.timeLeft;

        /// <summary>受护者生命快照（首帧记录，掉血即破裂）</summary>
        private ref float LifeSnapshot => ref Projectile.localAI[0];
        /// <summary>破裂标记（0=完好；1=已破，走消散）</summary>
        private ref float Popped => ref Projectile.localAI[1];

        /// <summary>格挡窗：完好且在存续期内</summary>
        private bool Blocking => Popped == 0f && Elapsed < GuardFrames;

        /// <summary>攻击方本机判窗：该 NPC 当前是否被完好护罩覆盖（索引+类型双校验）</summary>
        internal static bool IsGuarding(int npcIndex, int npcType) {
            int type = ModContent.ProjectileType<MrtGuardShieldProj>();
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile proj = Main.projectile[i];
                if (proj.active && proj.type == type && (int)proj.ai[0] == npcIndex && (int)proj.ai[1] == npcType
                    && proj.ModProjectile is MrtGuardShieldProj shield && shield.Blocking) {
                    return true;
                }
            }
            return false;
        }

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 24;
            Projectile.hostile = false;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = GuardFrames + PopFadeFrames;
            Projectile.netImportant = true;
        }

        public override bool ShouldUpdatePosition() => false;

        public override void AI() {
            //受护者索引+类型双校验：人没了护罩随之消散（槽位复用不冒充）
            NPC target = TargetIndex >= 0 && TargetIndex < Main.maxNPCs ? Main.npc[TargetIndex] : null;
            if (target == null || !target.active || target.type != TargetType) {
                Projectile.Kill();
                return;
            }
            Projectile.Center = target.Center;

            if (LifeSnapshot == 0f) {
                LifeSnapshot = target.life;
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.MaxMana with { Volume = 0.5f, Pitch = -0.1f, MaxInstances = 4 }, Projectile.Center);
                }
            }

            //完好期观测掉血：任何一次受击即消耗格挡，泡壳破裂（各端读同步生命值，结论一致）
            if (Blocking && target.life < (int)LifeSnapshot) {
                Popped = 1f;
                Projectile.timeLeft = Math.Min(Projectile.timeLeft, PopFadeFrames);
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Item27 with { Volume = 0.6f, Pitch = 0.3f, MaxInstances = 4 }, Projectile.Center);
                    for (int i = 0; i < 12; i++) {
                        Dust dust = Dust.NewDustPerfect(target.Center + Main.rand.NextVector2Circular(target.width * 0.6f, target.height * 0.6f),
                            DustID.MartianSaucerSpark, Main.rand.NextVector2Circular(3.2f, 3.2f), 0, default, Main.rand.NextFloat(0.9f, 1.4f));
                        dust.noGravity = true;
                    }
                }
            }

            if (VaultUtils.isServer) {
                return;
            }

            //链接尘线（前几帧，≤1 粒/帧）
            if (Elapsed < TetherFrames && OfficerIndex >= 0 && OfficerIndex < Main.maxNPCs) {
                NPC officer = Main.npc[OfficerIndex];
                if (officer.active && Main.rand.NextBool()) {
                    Dust link = Dust.NewDustPerfect(Vector2.Lerp(officer.Center, target.Center, Main.rand.NextFloat()),
                        DustID.MartianSaucerSpark, Vector2.Zero, 100, default, 0.7f);
                    link.noGravity = true;
                }
            }
            //完好期泡壳缘微光尘（≤1 粒/帧）
            if (Blocking && Main.rand.NextBool(3)) {
                float ang = Main.rand.NextFloat(MathHelper.TwoPi);
                float radius = Math.Max(target.width, target.height) * 0.8f + 12f;
                Dust shim = Dust.NewDustPerfect(target.Center + ang.ToRotationVector2() * radius,
                    DustID.Electric, Vector2.Zero, 120, default, 0.5f);
                shim.noGravity = true;
                shim.velocity = Vector2.Zero;
            }
            Lighting.AddLight(Projectile.Center, ShellCyan.ToVector3() * 0.16f);
        }

        public override bool PreDraw(ref Color lightColor) {
            NPC target = TargetIndex >= 0 && TargetIndex < Main.maxNPCs ? Main.npc[TargetIndex] : null;
            if (target == null || !target.active) {
                return false;
            }
            float fade = Projectile.timeLeft <= PopFadeFrames
                ? Projectile.timeLeft / (float)PopFadeFrames
                : MathHelper.Clamp(Elapsed / 6f, 0f, 1f);
            if (fade <= 0.02f) {
                return false;
            }

            //泡壳为无伤状态标记（加色纯光合法），实体感由受护 NPC 本体承载
            Texture2D ring = CWRAsset.DiffusionCircle.Value;
            Texture2D glow = CWRAsset.SoftGlow.Value;
            Vector2 drawPos = target.Center + new Vector2(0f, target.gfxOffY) - Main.screenPosition;
            float radius = Math.Max(target.width, target.height) * 0.8f + 12f;
            float wobble = 1f + 0.05f * MathF.Sin(Main.GlobalTimeWrappedHourly * 9f + Projectile.identity);
            Color shell = ShellCyan with { A = 0 };

            Main.EntitySpriteDraw(ring, drawPos, null, shell * (0.85f * fade), 0f, ring.Size() / 2f,
                radius * 2f / ring.Width * wobble, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(glow, drawPos, null, shell * (0.30f * fade), 0f, glow.Size() / 2f,
                radius * 2.2f / glow.Width, SpriteEffects.None, 0);
            //顶侧高光点：泡壳质感
            Main.EntitySpriteDraw(glow, drawPos + new Vector2(radius * 0.4f, -radius * 0.45f), null,
                new Color(255, 255, 255, 0) * (0.35f * fade), 0f, glow.Size() / 2f, 0.22f, SpriteEffects.None, 0);
            return false;
        }
    }
}
