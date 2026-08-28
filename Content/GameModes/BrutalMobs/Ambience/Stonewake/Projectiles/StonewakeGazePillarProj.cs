using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.Ambience.Stonewake.Projectiles
{
    /// <summary>
    /// 凝视之柱（大理石厅）。生成位置即地面锚点（预告即承诺），无 ai 参数。<br/>
    /// 预告：地面刻纹亮起+石化质感音逐拍推进（50 帧，公平契约 ≥45）；<br/>
    /// 落地：美杜莎凝视凝成石化金柱（暗石纹剪影+大理石白实体+金光缘，真 alpha 实体层），
    /// 柱内玩家受微量伤害并挂短暂石纹视觉（绝不做真石化/禁锢），
    /// 柱体静止不追，走出柱区即安全（具名逃生阀门）；<br/>
    /// 余韵：光柱自顶回落入地，刻纹余温冷却熄灭
    /// </summary>
    internal class StonewakeGazePillarProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        //LightBeam 256x1024 真alpha 竖梁（内容约 195x911，镜像 Dungeonworld 实测）
        [VaultLoaden(CWRConstant.Masking)]
        private static Asset<Texture2D> LightBeam = null;
        private const float BeamContentW = 195f;
        private const float BeamContentH = 911f;

        /// <summary>刻纹预告帧数（公平契约 ≥45，各档位一律不缩短）</summary>
        private const int EngraveFrames = 50;
        /// <summary>光柱立起帧数</summary>
        private const int RiseFrames = 12;
        /// <summary>光柱驻立帧数</summary>
        private const int HoldFrames = 36;
        /// <summary>回落熄灭帧数</summary>
        private const int FadeFrames = 20;
        /// <summary>柱半宽（判定），可见柱体略宽于判定</summary>
        private const float ColumnHalfWidth = 30f;
        /// <summary>柱高</summary>
        private const float ColumnHeight = 250f;
        /// <summary>刻纹半径</summary>
        private const float EngraveRadius = 46f;
        /// <summary>石化质感音拍间隔</summary>
        private const int StoneBeat = 14;

        private int TotalLife => EngraveFrames + RiseFrames + HoldFrames + FadeFrames;
        private int Elapsed => TotalLife - Projectile.timeLeft;

        /// <summary>立柱程度 0~1（快起）</summary>
        private float RiseProgress {
            get {
                int t = Elapsed - EngraveFrames;
                if (t <= 0) {
                    return 0f;
                }
                if (t >= RiseFrames) {
                    return 1f;
                }
                float x = t / (float)RiseFrames;
                return 1f - (1f - x) * (1f - x) * (1f - x);
            }
        }

        /// <summary>回落收束 1→0（光柱自顶缩回地面）</summary>
        private float RetractFactor {
            get {
                int t = Elapsed - EngraveFrames - RiseFrames - HoldFrames;
                if (t <= 0) {
                    return 1f;
                }
                return MathHelper.Clamp(1f - t / (float)FadeFrames, 0f, 1f);
            }
        }

        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 360;

        public override void SetDefaults() {
            Projectile.width = 32;
            Projectile.height = 32;
            Projectile.hostile = false;//立柱窗口内才置真
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = EngraveFrames + RiseFrames + HoldFrames + FadeFrames;
            Projectile.netImportant = true;
        }

        public override bool ShouldUpdatePosition() => false;

        public override void AI() {
            int elapsed = Elapsed;

            //判定窗=可见立柱窗；Boss 在场时伤害暂停（表现保留）
            Projectile.hostile = elapsed >= EngraveFrames
                && elapsed < EngraveFrames + RiseFrames + HoldFrames && !CWRWorld.HasBoss;

            if (Main.dedServ) {
                return;
            }

            //==== 预告期：刻纹亮起，石化质感音逐拍推进 ====
            if (elapsed < EngraveFrames) {
                float progress = elapsed / (float)EngraveFrames;
                if (elapsed == 0) {
                    SoundEngine.PlaySound(SoundID.Dig with { Volume = 0.55f, Pitch = -0.6f, MaxInstances = 4 }, Projectile.Center);
                }
                //石质拍点：磨石声逐拍沉底
                if (elapsed > 0 && elapsed % StoneBeat == 0) {
                    int beat = elapsed / StoneBeat;
                    SoundEngine.PlaySound(SoundID.Tink with {
                        Volume = 0.4f,
                        Pitch = -0.15f - 0.1f * beat,
                        MaxInstances = 4,
                    }, Projectile.Center);
                }
                //末段亮起：两记升调水晶鸣
                if (elapsed == EngraveFrames - 12 || elapsed == EngraveFrames - 5) {
                    SoundEngine.PlaySound(SoundID.MaxMana with {
                        Volume = 0.6f,
                        Pitch = elapsed == EngraveFrames - 5 ? 0.35f : 0.05f,
                        MaxInstances = 4,
                    }, Projectile.Center);
                }
                //刻纹上方的鎏金浮尘（≤1 粒/帧）
                if (Main.rand.NextBool(2)) {
                    Dust dust = Dust.NewDustPerfect(
                        Projectile.Center + new Vector2(Main.rand.NextFloat(-EngraveRadius, EngraveRadius) * progress, -2f),
                        DustID.GoldCoin, new Vector2(0f, -Main.rand.NextFloat(0.5f, 1.4f + progress)), 120, default, 0.8f);
                    dust.noGravity = true;
                }
                Lighting.AddLight(Projectile.Center, StonewakeFX.MarbleGold.ToVector3() * (0.15f + 0.5f * progress));
                return;
            }

            //==== 立柱帧：光柱破地而起 ====
            if (elapsed == EngraveFrames) {
                SoundEngine.PlaySound(SoundID.Shatter with { Volume = 0.5f, Pitch = -0.25f, MaxInstances = 4 }, Projectile.Center);
                SoundEngine.PlaySound(SoundID.MaxMana with { Volume = 0.85f, Pitch = 0.45f, MaxInstances = 4 }, Projectile.Center);
                for (int i = 0; i < 5; i++) {
                    PRTLoader.NewParticle<PRT_MarbleChip>(Projectile.Center + new Vector2(Main.rand.NextFloat(-20f, 20f), -4f),
                        new Vector2(Main.rand.NextFloat(-2.5f, 2.5f), -Main.rand.NextFloat(3f, 7f)),
                        StonewakeFX.MarbleGold, Main.rand.NextFloat(0.4f, 0.7f)).Configure(Main.rand.Next(20, 32));
                }
                PRTLoader.NewParticle<PRT_Smoke>(Projectile.Center, new Vector2(0f, -0.6f),
                    StonewakeFX.MarbleDust, 0.5f).Configure(24, 0.5f, 0.04f);
            }

            float body = RiseProgress * RetractFactor;
            //驻立期：柱身缓浮的石屑与微光（≤1 粒/5 帧）
            if (body > 0.4f && elapsed % 5 == 0 && elapsed < EngraveFrames + RiseFrames + HoldFrames) {
                PRTLoader.NewParticle<PRT_MarbleChip>(
                    Projectile.Center + new Vector2(Main.rand.NextFloat(-ColumnHalfWidth, ColumnHalfWidth) * 0.8f,
                        -Main.rand.NextFloat(10f, ColumnHeight * 0.8f)),
                    new Vector2(0f, -Main.rand.NextFloat(0.4f, 1f)),
                    StonewakeFX.MarbleGold, Main.rand.NextFloat(0.3f, 0.5f)).Configure(Main.rand.Next(16, 26), 0.02f);
            }
            //回落期的第一帧：熄灭前噗一口石尘（余韵开场）
            if (elapsed == EngraveFrames + RiseFrames + HoldFrames) {
                SoundEngine.PlaySound(SoundID.Tink with { Volume = 0.35f, Pitch = -0.4f, MaxInstances = 4 }, Projectile.Center);
                for (int i = 0; i < 3; i++) {
                    PRTLoader.NewParticle<PRT_Smoke>(Projectile.Center + new Vector2(Main.rand.NextFloat(-16f, 16f), -6f),
                        new Vector2(0f, -0.4f), StonewakeFX.MarbleDust,
                        Main.rand.NextFloat(0.3f, 0.45f)).Configure(26, 0.4f, 0.03f);
                }
            }
            if (body > 0.05f) {
                Lighting.AddLight(Projectile.Center - Vector2.UnitY * ColumnHeight * 0.5f * body,
                    StonewakeFX.MarbleGold.ToVector3() * body);
            }
        }

        /// <summary>柱形判定：沿柱轴分三段取样（判定窗已由 hostile 门控，可见柱体略宽于判定）</summary>
        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            if (!Projectile.hostile) {
                return false;
            }
            float rise = RiseProgress;
            if (rise < 0.25f) {
                return false;
            }
            float height = ColumnHeight * rise * RetractFactor;
            for (int i = 0; i < 3; i++) {
                Vector2 point = Projectile.Center - new Vector2(0f, height * (0.17f + 0.33f * i));
                Rectangle sample = Utils.CenteredRectangle(point, new Vector2(ColumnHalfWidth * 2f, height * 0.4f));
                if (sample.Intersects(targetHitbox)) {
                    return true;
                }
            }
            return false;
        }

        public override bool PreDraw(ref Color lightColor) {
            int elapsed = Elapsed;
            Vector2 basePos = Projectile.Center - Main.screenPosition;
            Texture2D ring = CWRAsset.DiffusionCircle.Value;
            Texture2D line = CWRAsset.Line.Value;
            Texture2D glow = CWRAsset.SoftGlow.Value;

            //加色敷料染色（A=0）：只给刻纹预告、金光缘、辉芒这类"本身是光"的层
            Color gold = StonewakeFX.MarbleGold; gold.A = 0;
            Color core = StonewakeFX.MarbleCore; core.A = 0;
            //实体层染色（A>0）：LightBeam 是真 alpha 贴图，A=0 染色会把贴图自带 alpha 乘没
            Color stoneDark = new(64, 54, 42);//暗石纹衬底
            Color marbleBody = Color.Lerp(StonewakeFX.MarbleDust, StonewakeFX.MarbleGold, 0.3f);//大理石白实体

            //==== 地面刻纹：预告期亮起，立柱期满亮，回落期余温冷却 ====
            float engraveK;
            if (elapsed < EngraveFrames) {
                engraveK = elapsed / (float)EngraveFrames;
            }
            else {
                engraveK = MathHelper.Clamp(0.4f + 0.6f * RetractFactor, 0f, 1f);
            }
            float flick = 0.8f + 0.2f * MathF.Sin(Main.GlobalTimeWrappedHourly * 9f + Projectile.identity);
            float ringScale = EngraveRadius / (ring.Width * 0.5f);
            //压扁成地面视角的椭圆刻环
            Main.EntitySpriteDraw(ring, basePos, null, gold * (0.75f * engraveK * flick), 0f,
                ring.Size() / 2f, new Vector2(ringScale, ringScale * 0.32f), SpriteEffects.None, 0);
            Main.EntitySpriteDraw(ring, basePos, null, core * (0.4f * engraveK), 0f,
                ring.Size() / 2f, new Vector2(ringScale * 0.7f, ringScale * 0.22f), SpriteEffects.None, 0);
            //六道放射刻痕（确定性排布，贴地横铺）
            for (int i = 0; i < 6; i++) {
                float side = i < 3 ? -1f : 1f;
                float reach = EngraveRadius * (0.35f + 0.28f * (i % 3)) * engraveK;
                Vector2 tickPos = basePos + new Vector2(side * (10f + reach * 0.5f), 1f + (i % 3) * 2f - 2f);
                Main.EntitySpriteDraw(line, tickPos, null, gold * (0.55f * engraveK), MathHelper.PiOver2,
                    line.Size() / 2f, new Vector2(0.04f, reach / line.Height), SpriteEffects.None, 0);
            }

            //==== 预告末段：柱位发丝预束（自刻纹心向上透出一线，底端锚地） ====
            if (elapsed >= EngraveFrames - 12 && elapsed < EngraveFrames) {
                float hint = (elapsed - (EngraveFrames - 12)) / 12f;
                Main.EntitySpriteDraw(line, basePos, null,
                    core * (0.4f * hint), 0f, new Vector2(line.Width / 2f, line.Height),
                    new Vector2(0.03f, ColumnHeight * 0.9f * hint / line.Height), SpriteEffects.None, 0);
            }

            //==== 石化金柱：暗石纹剪影+金光缘+大理石白实体，三段收口（底亮顶散），回落期整柱缩回地面 ====
            float bodyK = RiseProgress * RetractFactor;
            Texture2D beam = LightBeam?.Value;
            if (bodyK > 0.02f && beam != null && !beam.IsDisposed) {
                float height = ColumnHeight * bodyK;
                Vector2 top = basePos - new Vector2(0f, height);
                //段长比 / 段透明度（底段最亮）/ 段宽度（底段最宽）
                ReadOnlySpan<float> segFrac = [0.26f, 0.34f, 0.40f];
                ReadOnlySpan<float> segAlpha = [0.30f, 0.62f, 1f];
                ReadOnlySpan<float> segWide = [0.86f, 1f, 1.12f];
                const float beamWidth = 72f;
                Vector2 beamOrig = new(beam.Width * 0.5f, 0f);

                //暗接地座（真 alpha）：柱底压暗椭圆，成柱后的落地实感
                Main.EntitySpriteDraw(ring, basePos + new Vector2(0f, 2f), null,
                    stoneDark * (0.5f * bodyK), 0f, ring.Size() / 2f,
                    new Vector2(ringScale * 0.95f, ringScale * 0.26f), SpriteEffects.None, 0);

                //第一遍：暗石纹剪影（最宽，A>0），亮厅背景上给柱体可辨轮廓
                float cum = 0f;
                for (int s = 0; s < 3; s++) {
                    float segLen = height * segFrac[s];
                    Vector2 scale = new(beamWidth * segWide[s] * 1.16f / BeamContentW, segLen / BeamContentH);
                    Main.EntitySpriteDraw(beam, top + new Vector2(0f, cum), null,
                        stoneDark * ((0.5f + 0.45f * segAlpha[s]) * 0.8f * RetractFactor), 0f,
                        beamOrig, scale, SpriteEffects.None, 0);
                    cum += segLen * 0.92f;
                }
                //第二遍：金光缘（A=0 加色），亮在剪影与实体之间的边带
                cum = 0f;
                for (int s = 0; s < 3; s++) {
                    float segLen = height * segFrac[s];
                    Vector2 scale = new(beamWidth * segWide[s] * 1.08f / BeamContentW, segLen / BeamContentH);
                    Main.EntitySpriteDraw(beam, top + new Vector2(0f, cum), null,
                        gold * (0.7f * segAlpha[s] * RetractFactor), 0f,
                        beamOrig, scale, SpriteEffects.None, 0);
                    cum += segLen * 0.92f;
                }
                //第三遍：大理石白实体核（A>0），石化金柱的实体感来源
                cum = 0f;
                for (int s = 0; s < 3; s++) {
                    float segLen = height * segFrac[s];
                    Vector2 scale = new(beamWidth * segWide[s] / BeamContentW, segLen / BeamContentH);
                    Main.EntitySpriteDraw(beam, top + new Vector2(0f, cum), null,
                        marbleBody * (0.78f * segAlpha[s] * RetractFactor), 0f,
                        beamOrig, scale, SpriteEffects.None, 0);
                    cum += segLen * 0.92f;
                }
                //白热芯线与基座辉光
                Main.EntitySpriteDraw(line, basePos, null, core * (0.75f * bodyK), 0f,
                    new Vector2(line.Width / 2f, line.Height),
                    new Vector2(0.06f, height / line.Height), SpriteEffects.None, 0);
                Main.EntitySpriteDraw(glow, basePos - new Vector2(0f, 4f), null, gold * (0.6f * bodyK), 0f,
                    glow.Size() / 2f, new Vector2(1.4f, 0.6f), SpriteEffects.None, 0);
                //柱顶凝视辉芒（美杜莎的目光落点）
                Main.EntitySpriteDraw(glow, top, null, core * (0.5f * bodyK * flick), 0f,
                    glow.Size() / 2f, 0.55f * bodyK, SpriteEffects.None, 0);
            }
            return false;
        }

        public override void OnKill(int timeLeft) {
            if (Main.dedServ) {
                return;
            }
            //刻纹熄灭：一缕石尘收场
            for (int i = 0; i < 3; i++) {
                PRTLoader.NewParticle<PRT_Smoke>(
                    Projectile.Center + new Vector2(Main.rand.NextFloat(-EngraveRadius, EngraveRadius) * 0.5f, -4f),
                    new Vector2(0f, -0.3f), StonewakeFX.MarbleDust,
                    Main.rand.NextFloat(0.25f, 0.4f)).Configure(22, 0.35f, 0.03f);
            }
        }
    }
}
