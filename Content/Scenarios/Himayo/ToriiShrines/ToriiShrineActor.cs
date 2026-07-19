using CalamityOverhaul.Content.PRTTypes;
using InnoVault.Actors;
using InnoVault.Models3D.Runtime;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Graphics;
using System;
using Terraria;
using Terraria.GameContent;

namespace CalamityOverhaul.Content.Scenarios.Himayo.ToriiShrines
{
    /// <summary>
    /// 鸟居Actor：负责3D鸟居模型的每帧提交、鸟居下插地鬼切的绘制与拔刀交互提示。<br/>
    /// 逻辑锚点 <see cref="Actor.Position"/> 约定为鸟居正下方的地表中心（非左上角），
    /// 所有绘制/粒子/光照都相对该锚点展开
    /// </summary>
    internal class ToriiShrineActor : Actor
    {
        //模型包围盒半高约64.7单位(FlipY后上下对称)，pivot在包围盒中心：
        //把pivot抬到地面上方 半高*缩放 处，鸟居柱脚正好落在锚点地表
        private const float ModelBottomOffset = 64.7f;
        /// <summary>鸟居整体缩放；模型原始尺寸约142x129单位，2倍后约18x16格</summary>
        private const float ModelScale = 2f;
        //轻微的Y轴偏转让鸟居露出一点侧面进深，避免看起来像一张平面贴纸
        private const float ModelYaw = 0.32f;

        //刀的中心离地高度：贴图对角半长约65px，刀尖入土约18px
        internal const float SwordCenterHeight = 47f;
        //刀身旋转：原贴图刀尖朝右上(-45°)，转到刀尖朝下再往回带一点倾角
        private const float SwordRotation = MathHelper.PiOver4 * 3f - 0.26f;

        private float glowTimer;
        private int motePrtTimer;
        private int sparklePrtTimer;

        /// <summary>刀的中心点（世界坐标）</summary>
        public Vector2 SwordAnchor => Position + new Vector2(0f, -SwordCenterHeight);

        public override void OnSpawn(params object[] args) {
            Width = 64;
            Height = 128;
            //鸟居在2倍缩放下横向延展约±142px、纵向约260px，剔除扩张给足余量防止半入屏时弹出
            DrawExtendMode = 700;
            DrawLayer = ActorDrawLayer.AfterTiles;

            glowTimer = Main.rand.NextFloat(MathHelper.TwoPi);
        }

        public override void AI() {
            glowTimer += 0.03f;
            if (glowTimer > MathHelper.TwoPi) {
                glowTimer -= MathHelper.TwoPi;
            }

            if (Main.dedServ) {
                return;
            }

            if (ToriiShrine.SwordPresentForLocalPlayer()) {
                Lighting.AddLight(SwordAnchor, 0.5f, 0.12f, 0.16f);
                UpdateAmbience();
            }
        }

        /// <summary>
        /// 氛围粒子：刀周缓慢升腾的绯红光点，鸟居梁上偶尔一粒白色微光
        /// </summary>
        private void UpdateAmbience() {
            motePrtTimer++;
            if (motePrtTimer >= 26) {
                motePrtTimer = 0;
                Vector2 spawnPos = SwordAnchor + new Vector2(Main.rand.NextFloat(-26f, 26f), Main.rand.NextFloat(-10f, 24f));
                Vector2 velocity = new Vector2(Main.rand.NextFloat(-0.15f, 0.15f), Main.rand.NextFloat(-0.7f, -0.35f));
                PRTLoader.NewParticle<PRT_Light>(spawnPos, velocity, new Color(255, 70, 92), Main.rand.NextFloat(0.14f, 0.24f))
                    .Configure(Main.rand.Next(40, 70), opacity: 0.8f);
            }

            sparklePrtTimer++;
            if (sparklePrtTimer >= 110) {
                sparklePrtTimer = 0;
                //在鸟居横梁高度附近取一点
                Vector2 beamPos = Position + new Vector2(Main.rand.NextFloat(-120f, 120f) * ModelScale * 0.5f
                    , -Main.rand.NextFloat(150f, 240f));
                PRTLoader.NewParticle<PRT_Sparkle>(beamPos, Vector2.Zero, new Color(255, 220, 225), Main.rand.NextFloat(0.4f, 0.7f));
            }
        }

        /// <summary>
        /// 拔刀瞬间的本地演出：绯红光点环状迸发 + 白色碎晶，由 <see cref="ToriiShrine.PullSword"/> 调用
        /// </summary>
        public void SwordPulledBurst() {
            if (Main.dedServ) {
                return;
            }

            for (int i = 0; i < 26; i++) {
                float angle = MathHelper.TwoPi * i / 26f + Main.rand.NextFloat(-0.1f, 0.1f);
                Vector2 velocity = angle.ToRotationVector2() * Main.rand.NextFloat(2f, 7f);
                PRTLoader.NewParticle<PRT_Light>(SwordAnchor, velocity, new Color(255, 70, 92), Main.rand.NextFloat(0.2f, 0.42f))
                    .Configure(Main.rand.Next(30, 55), opacity: 0.9f);
            }
            for (int i = 0; i < 12; i++) {
                Vector2 velocity = Main.rand.NextVector2Circular(4f, 4f) - new Vector2(0, 2f);
                PRTLoader.NewParticle<PRT_Sparkle>(SwordAnchor, velocity, new Color(255, 235, 238), Main.rand.NextFloat(0.5f, 0.9f));
            }
        }

        public override bool PreDraw(SpriteBatch spriteBatch, ref Color drawColor) {
            SubmitToriiModel();

            if (ToriiShrine.SwordPresentForLocalPlayer()) {
                DrawSword(spriteBatch);
            }
            return false;
        }

        /// <summary>
        /// 每个渲染帧向Models3D管线提交一次鸟居实例：生命周期跟随Actor绘制，无需常驻注册/注销
        /// </summary>
        private void SubmitToriiModel() {
            Vault3DModel model = ToriiShrine.ToriiModel;
            if (model is null || !model.IsValid) {
                return;
            }

            //取鸟居中段的环境光做整体着色，混一点白保证夜里仍有轮廓
            Color light = Lighting.GetColor((int)(Position.X / 16f), (int)((Position.Y - 130f) / 16f));
            Model3DRenderer.Submit(new Model3DInstance(model) {
                Position = Position + new Vector2(0f, -ModelBottomOffset * ModelScale + 2),
                Rotation = new Vector3(0f, ModelYaw, 0f),
                Scale = new Vector3(ModelScale),
                Layer = Model3DLayer.AfterTiles,
                LightingEnabled = true,
                Tint = light,
            });
        }

        /// <summary>
        /// 插在鸟居下的鬼切：软辉光衬底 + 脉动的绯红发光层 + 受环境光的刀身本体
        /// </summary>
        private void DrawSword(SpriteBatch spriteBatch) {
            Texture2D sword = ToriiShrine.OnikiriTexture?.Value;
            if (sword == null) {
                return;
            }

            Vector2 drawPos = SwordAnchor - Main.screenPosition;
            Vector2 origin = sword.Size() / 2f;
            float pulse = MathF.Sin(glowTimer * 2f) * 0.5f + 0.5f;

            //软辉光衬底
            Texture2D glow = CWRAsset.SoftGlow.Value;
            Color backing = new Color(255, 60, 84) with { A = 0 } * (0.22f + pulse * 0.14f);
            spriteBatch.Draw(glow, drawPos, null, backing, 0f, glow.Size() / 2f
                , new Vector2(150f / glow.Width, 130f / glow.Height), SpriteEffects.None, 0f);

            //刀形发光层
            Color bladeGlow = new Color(255, 82, 100) with { A = 0 };
            for (int i = 0; i < 3; i++) {
                float glowScale = 1.06f + i * 0.06f;
                float glowAlpha = (0.3f + pulse * 0.3f) * (1f - i * 0.3f);
                spriteBatch.Draw(sword, drawPos, null, bladeGlow * glowAlpha, SwordRotation
                    , origin, glowScale, SpriteEffects.None, 0f);
            }

            //本体
            Color bodyColor = Lighting.GetColor((SwordAnchor / 16f).ToPoint());
            //刀身自带一点微光，避免夜晚完全看不见
            bodyColor = Color.Lerp(bodyColor, Color.White, 0.25f);
            spriteBatch.Draw(sword, drawPos, null, bodyColor, SwordRotation, origin, 1f, SpriteEffects.None, 0f);
        }

        public override void PostDraw(SpriteBatch spriteBatch, Color drawColor) {
            if (ToriiShrine.SwordPresentForLocalPlayer()) {
                DrawInteractPrompt(spriteBatch);
            }
        }

        /// <summary>
        /// 交互提示：柔光衬底+描边文字，绯红配色呼应鬼切主题（拒绝方框UI）
        /// </summary>
        private void DrawInteractPrompt(SpriteBatch sb) {
            float alpha = ToriiShrine.GetInteractPromptAlpha();
            if (alpha <= 0.01f) {
                return;
            }

            Vector2 textPos = SwordAnchor - Main.screenPosition + new Vector2(0, -96f);

            DynamicSpriteFont font = FontAssets.MouseText.Value;
            string hintText = ToriiShrine.GetPromptText();
            Vector2 textSize = font.MeasureString(hintText) * 0.9f;

            Texture2D glow = CWRAsset.SoftGlow.Value;
            float pulse = MathF.Sin(Main.GlobalTimeWrappedHourly * 3f) * 0.5f + 0.5f;

            //柔光椭圆衬底
            Vector2 backingScale = new Vector2((textSize.X + 50f) / glow.Width, (textSize.Y + 30f) / glow.Height);
            Color backingColor = new Color(190, 55, 80) with { A = 0 } * (alpha * (0.3f + pulse * 0.12f));
            sb.Draw(glow, textPos, null, backingColor, 0f, glow.Size() / 2f, backingScale, SpriteEffects.None, 0f);

            //文字
            Color textColor = new Color(255, 228, 232) * alpha;
            Utils.DrawBorderString(sb, hintText, textPos - textSize / 2, textColor, 0.9f);

            //脉动光带
            float lineWidth = textSize.X * (0.7f + pulse * 0.25f);
            Vector2 linePos = textPos + new Vector2(0, textSize.Y / 2f + 6f);
            Color lineColor = new Color(235, 95, 118) with { A = 0 } * (alpha * 0.6f);
            sb.Draw(glow, linePos, null, lineColor, 0f, glow.Size() / 2f
                , new Vector2(lineWidth / glow.Width, 4f / glow.Height), SpriteEffects.None, 0f);
        }
    }
}
