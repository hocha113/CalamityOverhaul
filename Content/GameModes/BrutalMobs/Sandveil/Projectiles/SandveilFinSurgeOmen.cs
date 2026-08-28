using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.Sandveil.Projectiles
{
    /// <summary>
    /// 沙鲨跃咬预兆：鳍迹停驻后在锁定落点原地渐强的沙涌。
    /// ai[0]=来源NPC+1|类型&lt;&lt;8|激烈沙暴位（时长以生成帧世界状态定格，两侧同源）。
    /// 生成位置即锁定落点（预告即承诺）；预告期来源死亡/槽位复用则取消（击杀施法者是有效反制）。
    /// 突进期本体保留为跃咬判定窗载体（受害端 <see cref="SandveilBrutalNPC.OnHitPlayer"/> 扫描本实体），
    /// 永不造成伤害
    /// </summary>
    internal class SandveilFinSurgeOmen : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        /// <summary>基础预告帧数（公平契约 ≥30，档位一律不缩短）</summary>
        internal const int TelegraphFrames = 32;
        /// <summary>激烈沙暴（Severity&gt;0.7）追加预告帧：沙隐猎场的公平回款</summary>
        internal const int StormBonusFrames = 8;
        /// <summary>ai[0] 的激烈沙暴标志位（低 8 位=槽+1、8..19 位=类型，互不重叠）</summary>
        internal const int StormBit = 1 << 20;
        /// <summary>跃咬判定窗帧数：覆盖最长滞空 38 帧 + 入沙余量</summary>
        internal const int StrikeFrames = 46;

        /// <summary>落点标记半宽：画得比鲨鱼判定更宽，把跳弧解算的横向余差也包进警示范围</summary>
        private const float MarkerHalfWidth = 66f;

        //色板参考 DuneStorm 沙漠色板（SandDeep/SandBright/WarnGlow），数值抄色、代码独立
        private static readonly Color SandDeep = new(140, 108, 62);
        private static readonly Color SandBright = new(232, 202, 126, 0);
        private static readonly Color WarnGlow = new(255, 200, 110, 0);

        internal static int TelegraphOf(bool severe) => TelegraphFrames + (severe ? StormBonusFrames : 0);

        private int Packed => (int)Projectile.ai[0];
        private int SourceIndex => (Packed & 255) - 1;
        private int SourceType => (Packed >> 8) & 0xFFF;
        private int Telegraph => TelegraphOf((Packed & StormBit) != 0);
        private int TotalLife => Telegraph + StrikeFrames;
        private int Elapsed => TotalLife - Projectile.timeLeft;
        internal bool InStrike => Elapsed >= Telegraph;

        private bool Cancelled {
            get => Projectile.localAI[1] == 1f;
            set => Projectile.localAI[1] = value ? 1f : 0f;
        }

        /// <summary>受害端判定：该沙鲨当前是否处于跃咬窗（猩红流血只在此窗内挂）</summary>
        internal static bool IsStrikeWindowFor(int npcIndex) {
            int type = ModContent.ProjectileType<SandveilFinSurgeOmen>();
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile proj = Main.projectile[i];
                if (proj.active && proj.type == type && ((int)proj.ai[0] & 255) == npcIndex + 1
                    && proj.ModProjectile is SandveilFinSurgeOmen omen && omen.InStrike && !omen.Cancelled) {
                    return true;
                }
            }
            return false;
        }

        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 480;

        public override void SetDefaults() {
            Projectile.width = 12;
            Projectile.height = 12;
            Projectile.hostile = false;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = TelegraphFrames + StormBonusFrames + StrikeFrames;
            Projectile.netImportant = true;
        }

        /// <summary>纯预告体，永不参与伤害</summary>
        public override bool? CanDamage() => false;

        public override bool ShouldUpdatePosition() => false;

        public override void AI() {
            if (Projectile.localAI[0] == 0f) {
                //时长从同步的 ai[0] 各端确定性展开（激烈沙暴位在生成帧定格）
                Projectile.localAI[0] = 1f;
                Projectile.timeLeft = TotalLife;
                if (!Main.dedServ) {
                    SoundEngine.PlaySound(SoundID.WormDig with { Volume = 0.6f, Pitch = 0.25f, MaxInstances = 5 }, Projectile.Center);
                }
            }
            int elapsed = Elapsed;

            //来源校验：沙鲨死亡/槽位被新怪复用则取消（各端读同步的 npc.active，结论一致）
            if (!Cancelled && elapsed < Telegraph) {
                if (SourceIndex < 0 || SourceIndex >= Main.maxNPCs || !Main.npc[SourceIndex].active
                    || Main.npc[SourceIndex].type != SourceType) {
                    Cancelled = true;
                }
            }

            if (Cancelled) {
                return;
            }

            if (elapsed < Telegraph) {
                //预告期：原地沙涌渐强（≤2 粒/帧），涌高与频率随进度爬升
                float progress = elapsed / (float)Telegraph;
                if (!Main.dedServ && Main.rand.NextBool(2)) {
                    Dust boil = Dust.NewDustPerfect(
                        Projectile.Center + new Vector2(Main.rand.NextFloat(-0.8f, 0.8f) * MarkerHalfWidth * progress, 4f),
                        DustID.Sand, new Vector2(0f, -Main.rand.NextFloat(1f, 2f + 3.5f * progress)),
                        110, default, 0.9f + progress * 0.9f);
                    boil.noGravity = true;
                }
                Lighting.AddLight(Projectile.Center, 0.2f * progress, 0.16f * progress, 0.07f * progress);
                return;
            }

            if (elapsed == Telegraph && !Main.dedServ) {
                //破沙帧：爆沙+咬空声（各端本地播放）
                SoundEngine.PlaySound(SoundID.Item1 with { Volume = 0.7f, Pitch = -0.2f, MaxInstances = 5 }, Projectile.Center);
                for (int i = 0; i < 8; i++) {
                    Dust burst = Dust.NewDustPerfect(Projectile.Center, DustID.Sand,
                        new Vector2(Main.rand.NextFloat(-3f, 3f), -Main.rand.NextFloat(3f, 8f)),
                        90, default, Main.rand.NextFloat(1.2f, 1.9f));
                    burst.noGravity = Main.rand.NextBool();
                }
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            int elapsed = Elapsed;
            float strength;
            if (Cancelled) {
                strength = 0.3f * MathHelper.Clamp(1f - elapsed / (float)Telegraph, 0f, 1f);
            }
            else if (InStrike) {
                //突进期标记降为余痕：可见窗与判定窗同一实体
                strength = MathHelper.Clamp(1f - (elapsed - Telegraph) / 14f, 0f, 1f) * 0.25f;
            }
            else {
                float fadeIn = MathHelper.Clamp(elapsed / 8f, 0f, 1f);
                float pulse = 0.65f + 0.35f * MathF.Sin(Main.GlobalTimeWrappedHourly * 12f + Projectile.identity * 0.9f);
                strength = fadeIn * pulse;
            }
            if (strength <= 0.01f) {
                return false;
            }
            float progress = MathHelper.Clamp(elapsed / (float)Telegraph, 0f, 1f);

            Texture2D rim = CWRAsset.Extra_98.Value;
            Texture2D glow = CWRAsset.SoftGlow.Value;
            Vector2 markPos = Projectile.Center + new Vector2(0f, 2f) - Main.screenPosition;

            //暗沙实底外圈（真 alpha 压亮背景）+ 亮沙加色芯，宽度随进度铺开
            float width = MarkerHalfWidth * 2f * (0.5f + 0.5f * progress);
            Vector2 rimScale = new Vector2(width / rim.Width, 30f / rim.Height) * 1.15f;
            Main.EntitySpriteDraw(rim, markPos, null, SandDeep * (0.75f * strength), 0f,
                rim.Size() / 2f, rimScale, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(glow, markPos, null, SandBright * (0.7f * strength), 0f,
                glow.Size() / 2f, new Vector2(width / glow.Width, 26f / glow.Height), SpriteEffects.None, 0);

            //渐起的沙涌小丘：原版沙块贴图确定性抖动（实体感锚点）
            if (!InStrike && !Cancelled) {
                Main.instance.LoadProjectile(ProjectileID.SandBallFalling);
                Texture2D sand = TextureAssets.Projectile[ProjectileID.SandBallFalling].Value;
                for (int i = 0; i < 3; i++) {
                    float jig = MathF.Sin(Main.GlobalTimeWrappedHourly * 22f + Projectile.identity + i * 2.1f);
                    Vector2 pos = markPos + new Vector2((i - 1) * 13f + jig * 2f, -3f * progress - 2f);
                    Color mound = Color.Lerp(lightColor, new Color(226, 196, 120), 0.5f) * (0.85f * progress * strength);
                    Main.EntitySpriteDraw(sand, pos, null, mound, jig * 0.4f, sand.Size() / 2f,
                        0.6f + 0.35f * progress, SpriteEffects.None, 0);
                }
                //临近破沙的警示暖光
                if (progress > 0.55f) {
                    Main.EntitySpriteDraw(glow, markPos, null, WarnGlow * (0.5f * (progress - 0.55f) / 0.45f * strength),
                        0f, glow.Size() / 2f, new Vector2(1.3f, 0.5f), SpriteEffects.None, 0);
                }
            }
            return false;
        }

        public override void DrawBehind(int index, List<int> behindNPCsAndTiles, List<int> behindNPCs,
            List<int> behindProjectiles, List<int> overPlayers, List<int> overWiresUI) => behindNPCsAndTiles.Add(index);
    }
}
