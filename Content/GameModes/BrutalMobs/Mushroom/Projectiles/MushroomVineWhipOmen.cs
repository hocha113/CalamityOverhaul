using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.Mushroom.Projectiles
{
    /// <summary>
    /// 藤鞭弧线预告。ai[0]=锁定弧度 ai[1]=打包(巨型|挥扫侧) ai[2]=来源NPC+1|类型&lt;&lt;8。
    /// 世界锁位（生成帧的球体位置）+ 出手锁向（预告即承诺）：藤梢发光 34 帧，
    /// 弧带虚影标出将要挥扫的判定带（几何常量与鞭击实体共用，所见弧带=判定弧带）；
    /// 预告期来源死亡则取消，提交帧由本体生成鞭击实体
    /// </summary>
    internal class MushroomVineWhipOmen : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        /// <summary>预告帧数（任务契约 ≥34，各档位一律不缩短）</summary>
        internal const int TelegraphFrames = 34;
        private const int FadeFrames = 8;
        /// <summary>挥扫弧半张角（弧度），预告与鞭击共用</summary>
        internal const float ArcHalf = 0.62f;
        /// <summary>鞭长（判定与预告共用同一常量：所见弧带=判定弧带）</summary>
        internal const float ReachBulb = 168f;
        /// <summary>巨型版鞭长</summary>
        internal const float ReachGiant = 212f;

        internal static float ReachFor(bool giant) => giant ? ReachGiant : ReachBulb;

        //==== ai[1] 位打包 ====
        internal static int Pack(bool giant, bool sideNegative) => (giant ? 1 : 0) | (sideNegative ? 2 : 0);
        internal static bool UnpackGiant(int packed) => (packed & 1) != 0;
        internal static float UnpackSide(int packed) => (packed & 2) != 0 ? -1f : 1f;

        private float LockedAim => Projectile.ai[0];
        private int Packed => (int)Projectile.ai[1];
        private bool Giant => UnpackGiant(Packed);
        private float Side => UnpackSide(Packed);
        private int TotalLife => TelegraphFrames + FadeFrames;
        private int Elapsed => TotalLife - Projectile.timeLeft;

        private bool Cancelled {
            get => Projectile.localAI[1] == 1f;
            set => Projectile.localAI[1] = value ? 1f : 0f;
        }

        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 480;

        public override void SetDefaults() {
            Projectile.width = 16;
            Projectile.height = 16;
            Projectile.hostile = false;//纯预告体，伤害经由鞭击实体
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = TelegraphFrames + FadeFrames;
            Projectile.netImportant = true;
        }

        public override bool? CanDamage() => false;

        public override bool ShouldUpdatePosition() => false;

        public override void AI() {
            int elapsed = Elapsed;

            if (elapsed == 0 && !Main.dedServ) {
                SoundEngine.PlaySound(SoundID.Item17 with { Volume = 0.4f, Pitch = -0.2f, MaxInstances = 4 }, Projectile.Center);
            }

            //来源检查：施法者死亡则取消提交（玩家击杀=有效反制）；类型比对防槽位复用
            if (!Cancelled && elapsed < TelegraphFrames) {
                int srcPacked = (int)Projectile.ai[2];
                int src = (srcPacked & 255) - 1;
                if (src < 0 || src >= Main.maxNPCs || !Main.npc[src].active
                    || Main.npc[src].type != srcPacked >> 8) {
                    Cancelled = true;
                }
            }

            //藤梢凝光尘：沿锁定方向的鞭梢处聚拢（≤2 粒/帧）
            if (!Cancelled && elapsed < TelegraphFrames && !Main.dedServ && Main.rand.NextBool(2)) {
                Vector2 tip = Projectile.Center + LockedAim.ToRotationVector2() * ReachFor(Giant);
                Vector2 dir = Main.rand.NextVector2Unit();
                Dust dust = Dust.NewDustPerfect(tip + dir * Main.rand.NextFloat(8f, 24f),
                    DustID.GlowingMushroom, -dir * Main.rand.NextFloat(0.8f, 1.8f), 130, default, 1f);
                dust.noGravity = true;
            }

            Lighting.AddLight(Projectile.Center + LockedAim.ToRotationVector2() * ReachFor(Giant),
                MushroomSporeBoltProj.SporeBright.ToVector3()
                * (0.25f * MathHelper.Clamp(elapsed / (float)TelegraphFrames, 0f, 1f)));

            if (elapsed == TelegraphFrames && !Cancelled && Main.netMode != NetmodeID.MultiplayerClient) {
                //提交帧：沿同一几何常量放出鞭击实体（锁角/打包原样传递，预告即承诺）
                Projectile.NewProjectile(Projectile.GetSource_FromAI(), Projectile.Center, Vector2.Zero,
                    ModContent.ProjectileType<MushroomVineWhipProj>(), Projectile.damage, 2f, Main.myPlayer,
                    LockedAim, Packed, 0f);
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            int elapsed = Elapsed;
            float fade;
            if (Cancelled) {
                fade = 0.35f * MathHelper.Clamp(1f - elapsed / (float)TelegraphFrames, 0f, 1f);
            }
            else if (elapsed >= TelegraphFrames) {
                fade = MathHelper.Clamp(1f - (elapsed - TelegraphFrames) / (float)FadeFrames, 0f, 1f) * 0.4f;
            }
            else {
                fade = MathHelper.Clamp(elapsed / 8f, 0f, 1f);
            }
            if (fade <= 0.01f) {
                return false;
            }

            float progress = MathHelper.Clamp(elapsed / (float)TelegraphFrames, 0f, 1f);
            float pulse = 0.7f + 0.3f * MathF.Sin(Main.GlobalTimeWrappedHourly * 16f + Projectile.identity);
            float reach = ReachFor(Giant);
            Vector2 center = Projectile.Center - Main.screenPosition;
            Texture2D glow = CWRAsset.SoftGlow.Value;

            //弧带虚影：沿将要挥扫的弧逐点标出（与鞭击同一 ArcHalf/Reach，所见即所打）
            const int arcDots = 9;
            for (int i = 0; i < arcDots; i++) {
                float t = i / (float)(arcDots - 1);
                //虚影随进度从起扫侧向收扫侧亮起，读出挥扫方向
                if (t > progress * 1.2f) {
                    break;
                }
                float ang = LockedAim + MathHelper.Lerp(-ArcHalf, ArcHalf, t) * Side;
                Vector2 pos = center + ang.ToRotationVector2() * reach;
                MushroomSporeBoltProj.DrawGlobAt(pos, ang + MathHelper.PiOver2,
                    0.45f * fade * pulse, new Vector2(0.18f, 0.26f));
            }

            //藤梢发光：锁定方向鞭梢处的亮核（暗底+加色芯，随预告渐亮）
            Vector2 tipPos = center + LockedAim.ToRotationVector2() * reach;
            Texture2D rim = CWRAsset.Extra_98.Value;
            Main.EntitySpriteDraw(rim, tipPos, null,
                MushroomSporeBoltProj.SporeDeep * (0.75f * fade), 0f,
                rim.Size() / 2f, 0.2f + 0.12f * progress, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(glow, tipPos, null,
                (MushroomSporeBoltProj.SporeBright with { A = 0 }) * ((0.3f + 0.5f * progress) * fade * pulse),
                0f, glow.Size() / 2f, 0.28f + 0.22f * progress, SpriteEffects.None, 0);

            //球体到鞭梢的细连线（弱加色，标出弧的来源）
            int lineDots = 4;
            for (int i = 1; i <= lineDots; i++) {
                Vector2 pos = Vector2.Lerp(center, tipPos, i / (float)(lineDots + 1));
                Main.EntitySpriteDraw(glow, pos, null,
                    (MushroomSporeBoltProj.SporeBright with { A = 0 }) * (0.2f * fade * pulse),
                    0f, glow.Size() / 2f, 0.08f, SpriteEffects.None, 0);
            }
            return false;
        }
    }
}
