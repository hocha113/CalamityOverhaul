using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.FrostMoon.Projectiles
{
    /// <summary>
    /// 常绿尖叫怪·饰品炮列预兆：ai[0]=每列投放数×100000+单发伤害 ai[1]=走廊起始列 ai[2]=锚NPC索引×1000+类型。
    /// 生成位置即炮列中心（预告即承诺）：预告期亮出各列投放柱，走廊列恒空（发射循环与绘制
    /// 读取同一跳列判据，可见缺口=真实缺口）；预告期锚体被击杀则整列取消（反制有效）。
    /// 本体全程无判定（威胁在投放的坠落饰品）
    /// </summary>
    internal class FrmOrnamentRowOmen : ModProjectile
    {
        public override string Texture => CWRConstant.Masking + "MaskLaserLine";

        /// <summary>炮列列数</summary>
        internal const int RowColumns = 9;
        /// <summary>列间距（像素）</summary>
        internal const float ColumnSpacing = 86f;
        /// <summary>走廊净空列数（发射循环与绘制共用的逃生阀门）</summary>
        internal const int CorridorClearColumns = 2;
        /// <summary>预告帧（小Boss契约 ≥40）</summary>
        internal const int TelegraphFrames = 48;
        /// <summary>投放顶距锚点的最大抬升（像素，遇天花板自动压低）</summary>
        internal const float DropRise = 360f;
        private const int AfterFrames = 22;

        private static readonly Color ColumnWarn = new Color(255, 120, 100, 0);

        private int DropsPerColumn => (int)Projectile.ai[0] / 100000;
        private int DropDamage => (int)Projectile.ai[0] % 100000;
        private int CorridorStart => (int)Projectile.ai[1];
        private int AnchorIndex => (int)Projectile.ai[2] / 1000;
        private int AnchorType => (int)Projectile.ai[2] % 1000;

        private int TotalLife => TelegraphFrames + AfterFrames;
        private int Elapsed => TotalLife - Projectile.timeLeft;

        private bool Cancelled {
            get => Projectile.localAI[1] == 1f;
            set => Projectile.localAI[1] = value ? 1f : 0f;
        }

        /// <summary>各列投放顶 Y 与地表 Y（首帧按世界物块确定性缓存，各端一致）</summary>
        private readonly float[] columnTopY = new float[RowColumns];
        private readonly float[] columnGroundY = new float[RowColumns];

        /// <summary>走廊跳列判据：发射与绘制共用（可见缺口=真实缺口）</summary>
        private bool IsCorridor(int column)
            => column >= CorridorStart && column < CorridorStart + CorridorClearColumns;

        private float ColumnX(int column) => Projectile.Center.X + (column - (RowColumns - 1) * 0.5f) * ColumnSpacing;

        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 960;

        public override void SetDefaults() {
            Projectile.width = 16;
            Projectile.height = 16;
            Projectile.hostile = false;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = TelegraphFrames + AfterFrames;
            Projectile.netImportant = true;
        }

        public override bool ShouldUpdatePosition() => false;

        public override void AI() {
            if (Projectile.localAI[0] == 0f) {
                Projectile.localAI[0] = 1f;
                //各列几何按世界物块确定性求得，各端一致；顶部遇天花板自动压低
                for (int i = 0; i < RowColumns; i++) {
                    Vector2 basePos = new Vector2(ColumnX(i), Projectile.Center.Y);
                    columnTopY[i] = FrmSiegeUtils.FindDropTopY(basePos, DropRise);
                    columnGroundY[i] = FrmSiegeUtils.TryFindGroundY(basePos, 46, out float g)
                        ? g : Projectile.Center.Y + 240f;
                }
                if (!Main.dedServ) {
                    SoundEngine.PlaySound(SoundID.MaxMana with { Volume = 0.5f, Pitch = 0.1f, MaxInstances = 4 }, Projectile.Center);
                }
            }

            int elapsed = Elapsed;

            //预告期锚体校验（索引+类型防槽位复用）：尖叫怪被击杀则取消炮列
            if (!Cancelled && elapsed < TelegraphFrames) {
                NPC anchor = AnchorIndex.TryGetNPC(out NPC a) ? a : null;
                if (!anchor.Alives() || anchor.type != AnchorType) {
                    Cancelled = true;
                }
            }

            if (elapsed == TelegraphFrames && !Cancelled) {
                if (!Main.dedServ) {
                    SoundEngine.PlaySound(SoundID.Item1 with { Volume = 0.65f, Pitch = 0.35f, MaxInstances = 4 }, Projectile.Center);
                }
                //投放只在权威端；走廊列由 IsCorridor 恒跳过（具名缺口被发射循环真实读取）
                if (Main.netMode != NetmodeID.MultiplayerClient) {
                    for (int col = 0; col < RowColumns; col++) {
                        if (IsCorridor(col)) {
                            continue;
                        }
                        for (int k = 0; k < DropsPerColumn; k++) {
                            //梯次投放沿投放柱向下错开（顶部已做天花板钳制，出生点恒在柱内）
                            Projectile.NewProjectile(Projectile.GetSource_FromAI(),
                                new Vector2(ColumnX(col), columnTopY[col] + k * 46f), new Vector2(0f, 3.2f),
                                ModContent.ProjectileType<FrmOrnamentProj>(), DropDamage, 1f, Main.myPlayer);
                        }
                    }
                }
            }

            //预告期列脚雪尘（≤2 粒/帧）
            if (!Main.dedServ && !Cancelled && elapsed < TelegraphFrames && Main.rand.NextBool(2)) {
                int col = Main.rand.Next(RowColumns);
                if (!IsCorridor(col)) {
                    Dust dust = Dust.NewDustPerfect(new Vector2(ColumnX(col), columnGroundY[col] - 2f),
                        DustID.Snow, new Vector2(0f, -Main.rand.NextFloat(0.6f, 1.6f)), 120, default, 1f);
                    dust.noGravity = true;
                }
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            int elapsed = Elapsed;
            float cancelDim = Cancelled ? 0.3f : 1f;
            float strength;
            if (elapsed < TelegraphFrames) {
                strength = MathHelper.Clamp(elapsed / 10f, 0f, 1f) * cancelDim;
            }
            else {
                strength = MathHelper.Clamp(1f - (elapsed - TelegraphFrames) / (float)AfterFrames, 0f, 1f) * 0.3f * cancelDim;
            }
            if (strength <= 0.01f) {
                return false;
            }

            Texture2D line = TextureAssets.Projectile[Type].Value;
            Texture2D glow = CWRAsset.SoftGlow.Value;
            float urgency = MathHelper.Clamp(elapsed / (float)TelegraphFrames, 0f, 1f);
            float pulse = 0.7f + 0.3f * MathF.Sin(Main.GlobalTimeWrappedHourly * 11f + Projectile.identity);

            for (int col = 0; col < RowColumns; col++) {
                if (IsCorridor(col)) {
                    continue;//走廊列不亮（绘制与发射同一判据，可见缺口=真实缺口）
                }
                float x = ColumnX(col);
                float topY = columnTopY[col];
                float len = columnGroundY[col] - topY;
                if (len < 24f) {
                    len = 24f;
                }
                Vector2 top = new Vector2(x, topY) - Main.screenPosition;
                //投放柱：自顶向下的细光柱
                Main.EntitySpriteDraw(line, top, null, ColumnWarn * (0.34f * strength * pulse), MathHelper.PiOver2,
                    new Vector2(0f, line.Height / 2f), new Vector2(len / line.Width, (10f + 8f * urgency) / line.Height),
                    SpriteEffects.None, 0);
                //顶部投放点
                Main.EntitySpriteDraw(glow, top, null, ColumnWarn * (0.6f * strength * pulse), 0f,
                    glow.Size() / 2f, 0.22f + 0.08f * urgency, SpriteEffects.None, 0);
                //地表落点刻线
                Main.EntitySpriteDraw(glow, new Vector2(x, columnGroundY[col]) - Main.screenPosition, null,
                    ColumnWarn * (0.42f * strength), 0f, glow.Size() / 2f,
                    new Vector2(0.4f, 0.12f), SpriteEffects.None, 0);
            }
            return false;
        }
    }
}
