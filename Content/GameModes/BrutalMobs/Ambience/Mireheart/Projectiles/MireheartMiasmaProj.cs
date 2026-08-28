using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.Ambience.Mireheart.Projectiles
{
    /// <summary>
    /// 「沼气袋」破裂后沉底的贴地沼瘴。ai[0]=体型。
    /// 材质身份：暗绿褐真 alpha 腐气（乘环境光，留极弱磷光下限），比空气重：
    /// 底部浓稠、顶部消散、沿地面缓慢摊开，与孢子云的「悬浮圆盘」划清。
    /// 预告自持：沉降 <see cref="ArmFrame"/>（≥45）帧后判定才开启，不依赖上游气泡包；
    /// 沉降期咕噜升调三拍双通道。
    /// 逃逸声明：贴地薄层（竖直半高 <see cref="PoolHeightFrac"/>），跳离地面/站上高处即免；
    /// 走出横向椭圆亦免。Boss 在场判定即停
    /// </summary>
    internal class MireheartMiasmaProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        //====== 具名数值块 ======
        /// <summary>沉降成形期帧数（亦是自持预告窗，≥45 公平契约）</summary>
        private const int GrowFrames = 48;
        /// <summary>判定开启帧：预告由瘴体自己持有，气泡包被打断或中途进场都不吃亏</summary>
        private const int ArmFrame = GrowFrames;
        /// <summary>驻留期帧数</summary>
        private const int HoldFrames = 64;
        /// <summary>散尽期帧数（散逸过 35% 即失去判定）</summary>
        private const int DryFrames = 44;
        private const int TotalFrames = GrowFrames + HoldFrames + DryFrames;
        /// <summary>满横向半宽（像素，×体型）</summary>
        private const float BaseRadius = 58f;
        /// <summary>判定横向半宽 = 可见半宽 × 此系数（判定略窄，偏袒玩家）</summary>
        private const float PoolWidthFrac = 0.85f;
        /// <summary>贴地瘴层竖直半高 = 横向半宽 × 此系数；跳离地面/站高于带顶即免（判定循环真读）</summary>
        private const float PoolHeightFrac = 0.42f;
        /// <summary>驻留期横向摊开增量（沿地面缓慢摊开）</summary>
        private const float SpreadFrac = 0.18f;
        /// <summary>成形期沉降速度（像素/帧，腐气贴向水面泥底）</summary>
        private const float SettleSpeed = 0.16f;
        /// <summary>伤害 = 原版黄蜂接触伤害 × 此值（镜像 DamageFrac 写法）</summary>
        private const float DamageFrac = 0.5f;
        /// <summary>敌对弹幕对玩家结算自带 ×2（专家 ×4），此处回折一半取回接触口径</summary>
        private const float HostileProjHalf = 0.5f;
        /// <summary>中毒时长（帧），档位不调伤害与减益，只调沼气频率</summary>
        private const int PoisonFrames = 210;
        /// <summary>中层流动雾团份数</summary>
        private const int BandPuffCount = 7;
        /// <summary>顶部消散丝缕份数</summary>
        private const int WispCount = 4;
        /// <summary>环境光乘算下限（腐气几乎不发光，只留极弱磷光防无光洞穴判定隐形）</summary>
        private const float LightFloor = 0.22f;

        //更暗更浓的绿褐（对照孢子云的蓝澜）
        private static readonly Color DeepMurk = new(38, 44, 22);
        private static readonly Color MidMurk = new(58, 66, 30);

        private float Scale => Projectile.ai[0] <= 0f ? 1f : Projectile.ai[0];
        private int Elapsed => TotalFrames - Projectile.timeLeft;

        private float GrowProgress => MathHelper.Clamp(Elapsed / (float)GrowFrames, 0f, 1f);
        private float DryProgress => MathHelper.Clamp(
            (DryFrames - Projectile.timeLeft) / (float)DryFrames, 0f, 1f);

        /// <summary>当前横向半宽：成形缓扩，驻留期继续沿地面缓慢摊开</summary>
        private float CurrentHalfWidth {
            get {
                float t = GrowProgress;
                float grown = BaseRadius * Scale * (1f - (1f - t) * (1f - t));
                float holdT = MathHelper.Clamp((Elapsed - GrowFrames) / (float)HoldFrames, 0f, 1f);
                return grown * (1f + SpreadFrac * holdT);
            }
        }

        /// <summary>浓稠带竖直半高（判定与绘制同源）</summary>
        private float PoolHalfHeight => CurrentHalfWidth * PoolHeightFrac;

        /// <summary>伤害基准：原版黄蜂（本群系代表敌怪）接触伤害折算，微量口径</summary>
        internal static int MiasmaDamage() {
            int baseContact = ContentSamples.NpcsByNetId.TryGetValue(NPCID.Hornet, out NPC hornet)
                ? hornet.damage : 26;
            return Math.Max(3, (int)(baseContact * DamageFrac * HostileProjHalf));
        }

        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 240;

        public override void SetDefaults() {
            Projectile.width = 40;
            Projectile.height = 40;
            Projectile.hostile = false;//自持预告期无判定，沉降完成后由 AI 开启
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = TotalFrames;
            Projectile.netImportant = true;
        }

        public override bool ShouldUpdatePosition() => false;

        public override void AI() {
            //判定窗=沉降完成后的浓稠窗；Boss 在场判定即停（各端从同步世界状态得出同一结论）
            Projectile.hostile = Elapsed >= ArmFrame && DryProgress <= 0.35f && !CWRWorld.HasBoss;

            //比空气重：成形期缓缓沉贴水面泥底，此后不再位移（不上浮不游走）
            if (Elapsed < GrowFrames) {
                Projectile.position.Y += SettleSpeed;
            }

            if (Main.dedServ) {
                return;
            }

            //沉降期咕噜升调三拍：瘴体自己的听觉预告通道（接续气泡包的声音身份）
            if (Elapsed == 14) {
                SoundEngine.PlaySound(SoundID.Item13 with { Volume = 0.24f, Pitch = -0.55f, MaxInstances = 3 }, Projectile.Center);
            }
            else if (Elapsed == 32) {
                SoundEngine.PlaySound(SoundID.Item13 with { Volume = 0.27f, Pitch = -0.32f, MaxInstances = 3 }, Projectile.Center);
            }
            else if (Elapsed == ArmFrame) {
                SoundEngine.PlaySound(SoundID.Item13 with { Volume = 0.3f, Pitch = -0.08f, MaxInstances = 3 }, Projectile.Center);
            }

            //贴地渗尘：沿浓稠带底部向两侧爬（≤1 粒/2 帧预算）
            if (Main.rand.NextBool(2) && CurrentHalfWidth > 16f) {
                float freshness = 1f - DryProgress;
                float lx = Main.rand.NextFloat(-1f, 1f);
                Vector2 pos = Projectile.Center + new Vector2(lx * CurrentHalfWidth,
                    PoolHalfHeight * Main.rand.NextFloat(0.1f, 0.8f));
                Dust dust = Dust.NewDustPerfect(pos, DustID.Poisoned,
                    new Vector2(MathF.Sign(lx) * 0.3f, -0.05f), 140, default, 0.9f * freshness + 0.3f);
                dust.noGravity = true;
            }
            Lighting.AddLight(Projectile.Center,
                new Vector3(0.10f, 0.20f, 0.05f) * (1f - DryProgress));
        }

        /// <summary>
        /// 贴地椭圆判定：横向走出椭圆即免；目标底边高于浓稠带顶直接判空
        /// （跳离地面/站上高处即免的显式通道），顶部消散丝缕不参与判定
        /// </summary>
        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            if (!Projectile.hostile) {
                return false;
            }
            float a = CurrentHalfWidth * PoolWidthFrac;
            float b = PoolHalfHeight;
            if (a < 1f || b < 1f) {
                return false;
            }
            Vector2 center = Projectile.Center;
            if (targetHitbox.Bottom < center.Y - b) {
                return false;//整体在带顶之上：跳离地面/站高即免
            }
            //纵向按 a/b 拉直成圆再做最近点检测（轴对齐缩放下矩形仍是矩形）
            float k = a / b;
            float cy = center.Y * k;
            float closestX = MathHelper.Clamp(center.X, targetHitbox.Left, targetHitbox.Right);
            float closestY = MathHelper.Clamp(cy, targetHitbox.Top * k, targetHitbox.Bottom * k);
            float dx = closestX - center.X;
            float dy = closestY - cy;
            return dx * dx + dy * dy <= a * a;
        }

        /// <summary>短暂原版中毒（命中方本机结算，原生同步；禁新建 ModBuff）</summary>
        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            target.AddBuff(BuffID.Poisoned, PoisonFrames);
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D fog = CWRAsset.Fog.Value;
            Vector2 fogOrigin = fog.Size() * 0.5f;

            float halfW = CurrentHalfWidth;
            float halfH = PoolHalfHeight;
            float fade = MathHelper.Clamp(Elapsed / 14f, 0f, 1f) * (1f - DryProgress);
            if (fade <= 0.01f || halfW < 6f) {
                return false;
            }
            float time = Main.GlobalTimeWrappedHourly;

            //层序自下而上：底部浓稠暗带 → 中层流动雾团 → 顶部消散丝缕；
            //沉重腐气不发光，全程无加法层（与孢子云的发光点缀划清）

            //1 底部浓稠暗带（贴地最暗最浓，两片错相压出厚度）
            Vector2 baseWorld = Projectile.Center + new Vector2(0f, halfH * 0.4f);
            float baseK = LightK(baseWorld);
            Vector2 baseScale = new(halfW * 2.15f / fog.Width, halfH * 1.5f / fog.Height);
            Main.EntitySpriteDraw(fog, baseWorld - Main.screenPosition, null,
                DeepMurk * (0.62f * fade * baseK), 0f, fogOrigin, baseScale, SpriteEffects.None, 0);
            float baseSway = MathF.Sin(time * 0.4f + Projectile.identity) * halfW * 0.06f;
            Main.EntitySpriteDraw(fog, baseWorld - Main.screenPosition + new Vector2(baseSway, -halfH * 0.18f),
                null, DeepMurk * (0.38f * fade * baseK), 0f, fogOrigin,
                baseScale * new Vector2(0.8f, 0.85f), SpriteEffects.FlipHorizontally, 0);

            //2 中层流动雾团：底重分布（hY² 压向底部），从破裂点向两侧摊开，出缘溶掉回中心重来
            for (int i = 0; i < BandPuffCount; i++) {
                float hY = Hash(i, 2);
                float hS = Hash(i, 3);
                float dir = hS > 0.5f ? 1f : -1f;
                float u = (Hash(i, 1) + time * (0.05f + 0.05f * hS)) % 1f;
                float flowFade = MathF.Sin(u * MathHelper.Pi);//新生与出缘两端都稀薄
                Vector2 world = Projectile.Center + new Vector2(
                    dir * u * halfW * 0.92f, halfH * (0.55f - 0.95f * hY * hY));
                float sx = halfW * (0.85f + 0.3f * hS) / fog.Width;
                Main.EntitySpriteDraw(fog, world - Main.screenPosition, null,
                    MidMurk * (0.5f * fade * flowFade * LightK(world)), 0f, fogOrigin,
                    new Vector2(sx, sx * 0.45f),
                    dir > 0f ? SpriteEffects.None : SpriteEffects.FlipHorizontally, 0);
            }

            //3 顶部消散丝缕：自带顶缓升缓淡，升尽即回（纯演出，不参与判定）
            for (int i = 0; i < WispCount; i++) {
                float hX = Hash(i, 4);
                float hS = Hash(i, 5);
                float cycle = (hS + time * (0.09f + 0.05f * hX)) % 1f;
                float wispA = (1f - cycle) * (1f - cycle) * 0.3f * fade;
                Vector2 world = Projectile.Center + new Vector2(
                    (hX * 2f - 1f) * halfW * 0.55f, -halfH * (0.2f + 1.3f * cycle));
                float sx = halfW * 0.5f / fog.Width;
                Main.EntitySpriteDraw(fog, world - Main.screenPosition, null,
                    MidMurk * (wispA * LightK(world)), (hS - 0.5f) * 1.1f, fogOrigin,
                    new Vector2(sx * (0.8f + 0.4f * hS), sx * (1.2f + 0.4f * hS)), SpriteEffects.None, 0);
            }
            return false;
        }

        /// <summary>环境光系数（乘环境光；磷光下限防无光洞穴隐形）</summary>
        private static float LightK(Vector2 worldPos) {
            Color lit = Lighting.GetColor((int)(worldPos.X / 16f), (int)(worldPos.Y / 16f));
            return LightFloor + (1f - LightFloor) * ((lit.R + lit.G + lit.B) / 765f);
        }

        public override void OnKill(int timeLeft) {
            if (Main.dedServ) {
                return;
            }
            //散尽余韵：沉重腐气最后向两侧塌散（不上飘）
            for (int i = 0; i < 4; i++) {
                float side = Main.rand.NextBool() ? 1f : -1f;
                Dust dust = Dust.NewDustPerfect(
                    Projectile.Center + Main.rand.NextVector2Circular(24f, 10f),
                    DustID.Poisoned, new Vector2(side * Main.rand.NextFloat(0.3f, 0.7f), 0.05f),
                    150, default, 0.8f);
                dust.noGravity = true;
            }
        }

        /// <summary>确定性散列（各端一致，不触碰 Main.rand）</summary>
        private float Hash(int i, int salt) => (Projectile.identity * 131 + i * 53 + salt * 29) % 89 / 89f;
    }
}
