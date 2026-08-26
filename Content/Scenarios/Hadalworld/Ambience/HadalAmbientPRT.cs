using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.Scenarios.Hadalworld.Ambience
{
    //====================================================================
    //深渊海沟环境粒(C 路):海雪 + 远景生物微光。
    //全部 CanPool=true(自定义字段在 Reset 复位),纯客户端表现,零同步。
    //贴图身份:SoftGlow 黑底图只进 AdditiveBlend 且染色随强度整体缩
    //(加色批 A=0=整层隐形,brief §5 缺陷①)
    //====================================================================

    //共享安全采样:粒子漂移可能贴近世界边,查询前先钳制。
    //自持副本,不引用 Dungeonworld 同名件(跨场景零耦合)
    internal static class HadalPRTUtil
    {
        internal static float SafeBright(Vector2 worldPx) {
            int x = (int)MathHelper.Clamp(worldPx.X / 16f, 1, Main.maxTilesX - 2);
            int y = (int)MathHelper.Clamp(worldPx.Y / 16f, 1, Main.maxTilesY - 2);
            return Lighting.Brightness(x, y);
        }

        internal static Tile SafeTile(Vector2 worldPx) {
            int x = (int)MathHelper.Clamp(worldPx.X / 16f, 1, Main.maxTilesX - 2);
            int y = (int)MathHelper.Clamp(worldPx.Y / 16f, 1, Main.maxTilesY - 2);
            return Framing.GetTileSafely(x, y);
        }
    }

    /// <summary>
    /// 海雪(marine snow):布朗微漂+极缓沉降+光照门控明灭。
    /// 潜渊症式签名:黑暗里不可见,只在玩家光源半径内显形,像潜艇灯照亮的悬浮物。
    /// SoftGlow 黑底图,加色批,染色随强度整体缩
    /// </summary>
    internal class PRT_HadalSnow : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "SoftGlow";
        public override bool CanPool => true;
        public override int InGame_World_MaxCount => 140;

        private float sinkSpeed;
        private float driftPhase;
        private float lightCache;

        public PRT_HadalSnow Configure(int lifetime, float sink) {
            Lifetime = lifetime;
            sinkSpeed = sink;
            return this;
        }

        public override void SetProperty() {
            PRTDrawMode = PRTDrawModeEnum.AdditiveBlend;
            //默认参数保证非 Configure 生成路径(/vlab 验收)也成立,Configure 覆盖
            Lifetime = 200;
            sinkSpeed = 0.15f;
            driftPhase = Main.rand.NextFloat(MathHelper.TwoPi);
        }

        public override void Reset() {
            base.Reset();
            sinkSpeed = 0f;
            driftPhase = 0f;
            lightCache = 0f;
        }

        public override void AI() {
            //布朗微漂:横向抖动 + 纵向缓趋沉降速度(水的粘滞感,漂比落多)
            Velocity += new Vector2(Main.rand.NextFloat(-0.012f, 0.012f), Main.rand.NextFloat(-0.008f, 0.008f));
            Velocity = new Vector2(
                MathHelper.Clamp(Velocity.X, -0.22f, 0.22f),
                MathHelper.Lerp(Velocity.Y, sinkSpeed, 0.015f));

            if (Time % 4 == 0) {
                lightCache = HadalPRTUtil.SafeBright(Position);
                //漂出水体(气穴/实心)即散
                Tile here = HadalPRTUtil.SafeTile(Position);
                if (here.LiquidAmount < 32 || (here.HasTile && Main.tileSolid[here.TileType])) {
                    Kill();
                    return;
                }
            }
            float t = LifetimeCompletion;
            float env = Math.Min(t / 0.18f, 1f) * MathHelper.Clamp((1f - t) / 0.22f, 0f, 1f);
            //海雪只在光里显形:黑暗处自然隐没(材质光照签名)
            Opacity = env * MathHelper.Clamp(lightCache * 1.25f, 0f, 1f) * 0.9f;
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            float shimmer = 0.85f + 0.15f * MathF.Sin(Time * 0.09f + driftPhase);
            //加色批:Color * 强度,A 一起缩(A=0 在 SrcAlpha 加色批=整层隐形,禁)
            spriteBatch.Draw(TexValue, Position - Main.screenPosition, null,
                Color * (Opacity * shimmer), 0f, TexValue.Size() * 0.5f,
                Scale * (0.9f + 0.1f * shimmer), SpriteEffects.None, 0f);
            return false;
        }
    }

    /// <summary>
    /// 远景生物微光:淡入-慢闪呼吸-淡出,极缓单向漂移,自带微弱点光。
    /// 潜渊症式"你看见了什么但不确定":只生在黑暗处,靠近的强光会让它先行熄灭。
    /// SoftGlow 黑底图双层(晕+芯),加色批
    /// </summary>
    internal class PRT_HadalGleam : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "SoftGlow";
        public override bool CanPool => true;
        //稀疏克制:同屏硬帽 4,由类型帽兜底(生成端另有调度冷却)
        public override int InGame_World_MaxCount => 4;

        private float blinkRate;
        private float blinkPhase;
        private float brightCache;
        private float retreat;

        public PRT_HadalGleam Configure(int lifetime, float blink) {
            Lifetime = lifetime;
            blinkRate = blink;
            return this;
        }

        public override void SetProperty() {
            PRTDrawMode = PRTDrawModeEnum.AdditiveBlend;
            //默认参数保证非 Configure 生成路径(/vlab 验收)也成立,Configure 覆盖
            Lifetime = 320;
            blinkRate = 0.03f;
            blinkPhase = Main.rand.NextFloat(MathHelper.TwoPi);
        }

        public override void Reset() {
            base.Reset();
            blinkRate = 0f;
            blinkPhase = 0f;
            brightCache = 0f;
            retreat = 0f;
        }

        public override void AI() {
            //极缓漂移+轻微垂直起伏(活物的暗示,不是灯)
            Velocity = new Vector2(Velocity.X * 0.998f,
                Velocity.Y * 0.998f + MathF.Sin(Time * 0.017f + blinkPhase) * 0.002f);

            if (Time % 6 == 0) {
                brightCache = HadalPRTUtil.SafeBright(Position);
            }
            //玩家光照到它 → "什么东西躲开了":加速熄灭
            if (brightCache > 0.35f) {
                retreat = MathHelper.Clamp(retreat + 0.03f, 0f, 1f);
            }

            float t = LifetimeCompletion;
            float env = Math.Min(t / 0.16f, 1f) * MathHelper.Clamp((1f - t) / 0.24f, 0f, 1f);
            //慢闪呼吸:2~3 次明灭,谷底不彻底熄(似灭非灭)
            float blink = 0.35f + 0.65f * (0.5f + 0.5f * MathF.Sin(Time * blinkRate + blinkPhase));
            Opacity = env * blink * (1f - retreat);
            if (retreat >= 1f) {
                Kill();
                return;
            }
            //微弱点光:黑暗里的一粒真光源(纯客户端表现光)
            if (Opacity > 0.02f) {
                Vector3 c = Color.ToVector3() * (Opacity * 0.14f);
                Lighting.AddLight(Position, c.X, c.Y, c.Z);
            }
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Vector2 pos = Position - Main.screenPosition;
            Vector2 origin = TexValue.Size() * 0.5f;
            //晕层大而虚,芯层小而定:远景发光体的最小可信结构
            spriteBatch.Draw(TexValue, pos, null, Color * (Opacity * 0.30f), 0f, origin,
                Scale * 3.0f, SpriteEffects.None, 0f);
            spriteBatch.Draw(TexValue, pos, null, Color * Opacity, 0f, origin,
                Scale, SpriteEffects.None, 0f);
            return false;
        }
    }
}
