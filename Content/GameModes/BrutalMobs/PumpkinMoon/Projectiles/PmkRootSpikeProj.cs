using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.PumpkinMoon.Projectiles
{
    /// <summary>
    /// 哀恸根须（根须墙单元）。ai[0]=裂隙预告帧 ai[1]=来源哀木索引+1 ai[2]=驻留帧。
    /// 生成位置即锁定（预告即承诺）：地面裂隙 ≥40 帧 → 破土 → 驻留（仅此窗口有判定）→ 缩回。
    /// 预告期击杀哀木则取消破土（反制有效，index+type 双校验防槽位复用）；判定沿根轴取样，
    /// 窗口与可见破土精确对齐。墙的缺口由发射端跳过槽位实现（无裂隙标记=安全门）
    /// </summary>
    internal class PmkRootSpikeProj : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.Stake;

        /// <summary>裂隙预告帧（小 Boss 契约 ≥40）</summary>
        internal const int TelegraphFrames = 44;
        internal const int EruptFrames = 10;
        internal const int RetractFrames = 14;
        /// <summary>根须全高</summary>
        private const float RootHeight = 134f;
        /// <summary>根须判定半宽</summary>
        private const float RootHalfWidth = 15f;

        private static readonly Color RootBark = new Color(146, 96, 54);
        private static readonly Color RootDeep = new Color(64, 36, 20);
        private static readonly Color FissureWarn = new Color(255, 132, 44, 0);

        private int Telegraph => Math.Max((int)Projectile.ai[0], 40);
        private int SourceIndex => (int)Projectile.ai[1] - 1;
        private int HoldFrames => Math.Max((int)Projectile.ai[2], 20);
        private int TotalLife => Telegraph + EruptFrames + HoldFrames + RetractFrames;
        private int Elapsed => TotalLife - Projectile.timeLeft;

        private bool Cancelled {
            get => Projectile.localAI[1] == 1f;
            set => Projectile.localAI[1] = value ? 1f : 0f;
        }

        /// <summary>破土程度 0~1（快速爆出）</summary>
        private float EruptProgress {
            get {
                int t = Elapsed - Telegraph;
                if (t <= 0) {
                    return 0f;
                }
                if (t >= EruptFrames) {
                    return 1f;
                }
                float x = t / (float)EruptFrames;
                return 1f - (1f - x) * (1f - x) * (1f - x);
            }
        }

        /// <summary>缩回 1→0</summary>
        private float RetractFactor {
            get {
                int t = Elapsed - Telegraph - EruptFrames - HoldFrames;
                if (t <= 0) {
                    return 1f;
                }
                return MathHelper.Clamp(1f - t / (float)RetractFrames, 0f, 1f);
            }
        }

        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 320;

        public override void SetDefaults() {
            Projectile.width = 30;
            Projectile.height = 30;
            Projectile.hostile = false;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 600;
            Projectile.netImportant = true;
        }

        public override bool ShouldUpdatePosition() => false;

        public override void AI() {
            if (Projectile.localAI[0] == 0f) {
                Projectile.localAI[0] = 1f;
                Projectile.timeLeft = TotalLife;
                if (!Main.dedServ) {
                    SoundEngine.PlaySound(SoundID.Dig with { Volume = 0.5f, Pitch = -0.4f, MaxInstances = 5 }, Projectile.Center);
                }
            }

            int elapsed = Elapsed;

            //预告期来源检查：哀木被击杀则取消破土（已破土则走完，不回收）。
            //index+type 双校验：原树死后同槽刷出新怪时不放行
            if (!Cancelled && elapsed < Telegraph) {
                int src = SourceIndex;
                if (src < 0 || src >= Main.maxNPCs || !Main.npc[src].active
                    || Main.npc[src].type != NPCID.MourningWood) {
                    Cancelled = true;
                }
            }
            if (Cancelled && elapsed >= Telegraph) {
                Projectile.Kill();
                return;
            }

            //判定窗=可见破土窗
            Projectile.hostile = !Cancelled && elapsed >= Telegraph && elapsed < Telegraph + EruptFrames + HoldFrames;

            if (Cancelled || Main.dedServ) {
                return;
            }

            if (elapsed < Telegraph) {
                //裂隙期：木屑与土粒外翻（≤2 粒/帧）
                float progress = elapsed / (float)Telegraph;
                if (Main.rand.NextBool(3)) {
                    Dust chip = Dust.NewDustPerfect(
                        Projectile.Center + new Vector2(Main.rand.NextFloat(-RootHalfWidth, RootHalfWidth), 2f),
                        DustID.WoodFurniture, new Vector2(0f, -Main.rand.NextFloat(0.8f, 2f + progress * 2f)),
                        90, default, 0.9f + progress * 0.5f);
                    chip.noGravity = Main.rand.NextBool();
                }
                return;
            }

            if (elapsed == Telegraph) {
                //破土帧
                SoundEngine.PlaySound(SoundID.Dig with { Volume = 0.85f, Pitch = -0.15f, MaxInstances = 5 }, Projectile.Center);
                for (int i = 0; i < 6; i++) {
                    Dust burst = Dust.NewDustPerfect(Projectile.Center, DustID.WoodFurniture,
                        new Vector2(Main.rand.NextFloat(-2.2f, 2.2f), -Main.rand.NextFloat(3f, 8f)), 80, default,
                        Main.rand.NextFloat(1.1f, 1.7f));
                    burst.noGravity = Main.rand.NextBool();
                }
            }

            float vis = EruptProgress * RetractFactor;
            if (vis > 0.05f) {
                Lighting.AddLight(Projectile.Center - Vector2.UnitY * RootHeight * 0.5f * vis,
                    RootBark.ToVector3() * 0.12f * vis);
            }
        }

        /// <summary>沿根轴分三段取样（判定窗已由 hostile 门控）</summary>
        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            if (!Projectile.hostile) {
                return false;
            }
            float erupt = EruptProgress * RetractFactor;
            if (erupt < 0.25f) {
                return false;
            }
            float height = RootHeight * erupt;
            for (int i = 0; i < 3; i++) {
                Vector2 point = Projectile.Center - new Vector2(0f, height * (0.17f + 0.33f * i));
                Rectangle sample = Utils.CenteredRectangle(point, new Vector2(RootHalfWidth * 2f, height * 0.4f));
                if (sample.Intersects(targetHitbox)) {
                    return true;
                }
            }
            return false;
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            target.AddBuff(BuffID.Bleeding, 150);
        }

        public override bool PreDraw(ref Color lightColor) {
            int elapsed = Elapsed;
            float cancelDim = Cancelled ? 0.4f : 1f;

            if (elapsed < Telegraph) {
                //裂隙期：地表警示光缝（脉动）
                float progress = elapsed / (float)Telegraph;
                float pulse = 0.7f + 0.3f * MathF.Sin(Main.GlobalTimeWrappedHourly * 14f + Projectile.identity);
                Texture2D slit = CWRAsset.SoftGlow.Value;
                Vector2 markPos = Projectile.Center + new Vector2(0f, 2f) - Main.screenPosition;
                Main.EntitySpriteDraw(slit, markPos, null, FissureWarn * (0.6f * progress * pulse * cancelDim), 0f,
                    slit.Size() / 2f, new Vector2(0.55f, 0.16f + 0.1f * progress), SpriteEffects.None, 0);
                return false;
            }

            float eruptVis = EruptProgress;
            float retract = RetractFactor;
            if (eruptVis <= 0.01f || retract <= 0.01f) {
                return false;
            }
            float height = RootHeight * eruptVis * retract;

            //暗须衬底（真 alpha，根柱轮廓）
            Texture2D under = CWRAsset.Extra_98.Value;
            Color underColor = RootDeep * (0.7f * retract);
            Main.EntitySpriteDraw(under, Projectile.Center - new Vector2(0f, height * 0.5f) - Main.screenPosition,
                null, underColor, 0f, under.Size() / 2f,
                new Vector2(RootHalfWidth * 2.6f / under.Width, height * 1.15f / under.Height), SpriteEffects.None, 0);

            //根须段：原版木桩贴图三段堆叠（实体层），底粗顶细，确定性侧倾
            Texture2D tex = TextureAssets.Projectile[Type].Value;
            Vector2 origin = tex.Size() / 2f;
            for (int i = 0; i < 3; i++) {
                float seg = (i + 0.5f) / 3f;
                float lean = MathF.Sin(Projectile.identity * 1.71f + i * 2.3f) * 0.16f;
                Vector2 pos = Projectile.Center - new Vector2(-lean * 14f, height * seg) - Main.screenPosition;
                float segScale = (1.25f - 0.45f * seg) * MathHelper.Lerp(0.6f, 1f, eruptVis);
                Color segColor = Color.Lerp(lightColor, RootBark, 0.5f) * (retract * (1f - 0.2f * seg));
                Main.EntitySpriteDraw(tex, pos, null, segColor, lean, origin, segScale, SpriteEffects.None, 0);
            }

            //破土白闪（短暂）
            float flash = MathHelper.Clamp(1f - (elapsed - Telegraph) / 8f, 0f, 1f);
            if (flash > 0f) {
                Texture2D glow = CWRAsset.SoftGlow.Value;
                Main.EntitySpriteDraw(glow, Projectile.Center - new Vector2(0f, height * 0.4f) - Main.screenPosition,
                    null, (Color.White with { A = 0 }) * (0.4f * flash), 0f, glow.Size() / 2f,
                    new Vector2(0.4f, height / 160f), SpriteEffects.None, 0);
            }
            return false;
        }

        public override void OnKill(int timeLeft) {
            if (Main.dedServ || Cancelled) {
                return;
            }
            //缩回收场：落屑
            for (int i = 0; i < 4; i++) {
                Dust chip = Dust.NewDustPerfect(
                    Projectile.Center - new Vector2(Main.rand.NextFloat(-RootHalfWidth, RootHalfWidth),
                        Main.rand.NextFloat(0f, RootHeight * 0.5f)),
                    DustID.WoodFurniture, new Vector2(Main.rand.NextFloat(-1f, 1f), Main.rand.NextFloat(1f, 2.5f)),
                    100, default, Main.rand.NextFloat(0.8f, 1.2f));
                chip.noGravity = false;
            }
        }
    }
}
