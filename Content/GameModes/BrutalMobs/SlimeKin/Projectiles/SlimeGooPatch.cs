using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.SlimeKin.Projectiles
{
    /// <summary>
    /// 地面黏化减速带（场地实体，可见存续期）。本身零伤害零判定，只做控制：
    /// 通用=缓速 / 熔岩=灼烧 / 冰=打滑物理 / 毒泥=毒云（判定区抬高）。
    /// 效果窗与可见窗由同一 timeLeft 门控；各端只对本地玩家结算（弹幕实体原生同步）。
    /// ai[0]=档位×10+风味，ai[1]=凝胶色，ai[2]=宽度系数
    /// </summary>
    internal class SlimeGooPatch : ModProjectile
    {
        public override string Texture => CWRConstant.Masking + "Extra_98";

        private const int GrowFrames = 14;
        /// <summary>收场淡出帧：淡出期效果一并关闭（效果窗=可见窗）</summary>
        private const int FadeFrames = 30;
        /// <summary>贴地效果带高度</summary>
        private const float PatchHeight = 26f;
        /// <summary>毒云判定抬高（具名：毒泥风味的判定区=可见雾柱高）</summary>
        private const float ToxicCloudHeight = 64f;
        /// <summary>半宽基准，乘 ai[2] 体型系数</summary>
        private const float HalfWidthBase = 66f;
        /// <summary>存续帧（不含铺开与淡出），档位只延长存续</summary>
        private static readonly int[] PatchLifeByTier = [480, 540, 600];
        private const int SlowFrames = 20;
        private const int BurnFrames = 60;
        private const int PoisonFrames = 90;

        private GooFlavor Flavor => (GooFlavor)((int)Projectile.ai[0] % 10);
        private int Tier => (int)MathHelper.Clamp((int)Projectile.ai[0] / 10, 1, 3);
        private Color Gel => SlimeKinFlavor.UnpackColor(Projectile.ai[1]);
        private float WidthScale => Projectile.ai[2] <= 0f ? 1f : Projectile.ai[2];

        private int TotalLife => GrowFrames + PatchLifeByTier[Tier - 1] + FadeFrames;
        private int Elapsed => TotalLife - Projectile.timeLeft;
        /// <summary>铺开进度 0→1</summary>
        private float Spread => MathHelper.Clamp(Elapsed / (float)GrowFrames, 0f, 1f);
        /// <summary>可见度（铺开 × 淡出）</summary>
        private float Vis => Spread * MathHelper.Clamp(Projectile.timeLeft / (float)FadeFrames, 0f, 1f);
        /// <summary>效果窗：铺开过半且未进淡出（与 Vis 同一 timeLeft 驱动）</summary>
        private bool EffectActive => Projectile.timeLeft > FadeFrames && Spread >= 0.5f;

        public override void SetDefaults() {
            Projectile.width = 24;
            Projectile.height = 24;
            Projectile.hostile = false;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 700;
            Projectile.netImportant = true;
        }

        public override bool ShouldUpdatePosition() => false;

        /// <summary>纯控制区，永无伤害</summary>
        public override bool? CanDamage() => false;

        /// <summary>效果判定区：贴地带；毒云风味抬高到雾柱顶</summary>
        private Rectangle EffectRect() {
            float halfW = HalfWidthBase * WidthScale * Spread;
            float h = Flavor == GooFlavor.Toxic ? ToxicCloudHeight : PatchHeight;
            return new Rectangle((int)(Projectile.Center.X - halfW), (int)(Projectile.Center.Y - h),
                (int)(halfW * 2f), (int)h);
        }

        public override void AI() {
            if (Projectile.localAI[0] == 0f) {
                Projectile.localAI[0] = 1f;
                //寿命按档位本地确定性展开（ai[0] 已同步）
                Projectile.timeLeft = TotalLife;
            }

            //效果结算：各端只管本地玩家，减益/物理走原生同步
            if (EffectActive && !Main.dedServ) {
                Player lp = Main.LocalPlayer;
                if (lp.active && !lp.dead && !lp.ghost && lp.Hitbox.Intersects(EffectRect())) {
                    switch (Flavor) {
                        case GooFlavor.Molten:
                            lp.AddBuff(BuffID.OnFire, BurnFrames);
                            break;
                        case GooFlavor.Slick:
                            lp.GetModPlayer<SlimeKinPlayer>().slickFrames = 2;
                            break;
                        case GooFlavor.Toxic:
                            lp.AddBuff(BuffID.Poisoned, PoisonFrames);
                            break;
                        default:
                            lp.AddBuff(BuffID.Slow, SlowFrames);
                            break;
                    }
                }
            }

            //风味环境粉尘（低频，量产场地实体的性能红线）
            if (!VaultUtils.isServer && EffectActive && Main.rand.NextBool(6)) {
                float halfW = HalfWidthBase * WidthScale;
                Vector2 pos = Projectile.Center + new Vector2(Main.rand.NextFloat(-halfW, halfW) * 0.9f, -4f);
                switch (Flavor) {
                    case GooFlavor.Molten: {
                        Dust dust = Dust.NewDustPerfect(pos, DustID.Torch, -Vector2.UnitY * Main.rand.NextFloat(0.8f, 2f), 90, default, 1.2f);
                        dust.noGravity = true;
                        break;
                    }
                    case GooFlavor.Slick: {
                        Dust dust = Dust.NewDustPerfect(pos, DustID.IceTorch, -Vector2.UnitY * Main.rand.NextFloat(0.2f, 0.7f), 130, default, 0.8f);
                        dust.noGravity = true;
                        break;
                    }
                    case GooFlavor.Toxic: {
                        Dust dust = Dust.NewDustPerfect(pos, DustID.JungleSpore,
                            -Vector2.UnitY * Main.rand.NextFloat(0.4f, 1.3f), 140, default, 1.0f);
                        dust.noGravity = true;
                        break;
                    }
                    default: {
                        Dust dust = Dust.NewDustPerfect(pos, DustID.t_Slime, -Vector2.UnitY * Main.rand.NextFloat(0.2f, 0.8f), 140, Gel, 0.9f);
                        dust.noGravity = true;
                        break;
                    }
                }
            }

            float glow = Flavor == GooFlavor.Molten ? 0.35f : 0.18f;
            Lighting.AddLight(Projectile.Center, Gel.ToVector3() * glow * Vis);
        }

        public override bool PreDraw(ref Color lightColor) {
            float vis = Vis;
            if (vis <= 0.01f) {
                return false;
            }
            Texture2D blob = TextureAssets.Projectile[Type].Value;
            Vector2 origin = blob.Size() * 0.5f;
            Vector2 groundPos = Projectile.Center - Main.screenPosition;
            Color gel = Gel;
            float fullW = HalfWidthBase * 2f * WidthScale * Spread;

            //毒云雾柱：判定抬高的可视对应物
            if (Flavor == GooFlavor.Toxic) {
                Main.EntitySpriteDraw(blob, groundPos - new Vector2(0f, ToxicCloudHeight * 0.5f), null,
                    (gel with { A = 0 }) * (0.22f * vis), 0f, origin,
                    new Vector2(fullW * 0.8f / blob.Width, ToxicCloudHeight * 1.3f / blob.Height), SpriteEffects.None, 0);
            }

            //黏浆本体：真 alpha 暗层 + 亮芯 + 加色顶光
            Main.EntitySpriteDraw(blob, groundPos - new Vector2(0f, 4f), null, gel * (0.62f * vis), 0f, origin,
                new Vector2(fullW / blob.Width, 22f / blob.Height), SpriteEffects.None, 0);
            Main.EntitySpriteDraw(blob, groundPos - new Vector2(0f, 5f), null,
                Color.Lerp(gel, Color.White, 0.22f) * (0.4f * vis), 0f, origin,
                new Vector2(fullW * 0.72f / blob.Width, 15f / blob.Height), SpriteEffects.None, 0);
            Main.EntitySpriteDraw(blob, groundPos - new Vector2(0f, 8f), null,
                (Color.Lerp(gel, Color.White, 0.5f) with { A = 0 }) * (0.2f * vis), 0f, origin,
                new Vector2(fullW * 0.5f / blob.Width, 8f / blob.Height), SpriteEffects.None, 0);

            //半沉凝胶粒（identity 定席位，各端一致）
            Main.instance.LoadItem(ItemID.Gel);
            Texture2D gelTex = TextureAssets.Item[ItemID.Gel].Value;
            for (int i = 0; i < 3; i++) {
                int seed = Projectile.identity * 31 + i * 97;
                float off = (seed % 100 / 100f - 0.5f) * fullW * 0.7f;
                float lumpScale = 0.55f + seed % 7 * 0.05f;
                Main.EntitySpriteDraw(gelTex, groundPos + new Vector2(off, -5f), null,
                    Color.Lerp(gel, Color.White, 0.25f) * (0.7f * vis), 0f,
                    new Vector2(gelTex.Width * 0.5f, gelTex.Height * 0.35f), lumpScale, SpriteEffects.None, 0);
            }
            return false;
        }
    }
}
