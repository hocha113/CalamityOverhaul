using CalamityOverhaul.Common;
using InnoVault.RenderHandles;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaRains
{
    /// <summary>
    /// 墨雨渲染层:渍斑贴花最底、墨滴居中、悬伞最上。
    /// 独立于领域渲染，普攻在领域外也要工作;
    /// 地面渍与墨滴画在 EndEntityDraw,域内会被血湖镜面自动倒影;
    /// 湖晕由领域 EndCapture 在 TechUnify 之后叠到水面上
    /// </summary>
    internal class KikasaRainRender : RenderHandle
    {
        /// <summary>压在血湖领域(1.24)之前,墨画完再交给湖面镜面与泡沫</summary>
        public override float Weight => 1.22f;

        public override void UpdateBySystem(int index) {
            //主菜单兜底清场(PostUpdateEverything 不再运行)
            if (Main.gameMenu) {
                KikasaInkFX.Clear();
            }
        }

        public override void EndEntityDraw(SpriteBatch spriteBatch, Main main) {
            if (Main.gameMenu) {
                return;
            }
            //渍斑最底:命中的余韵垫在飞行的墨之下
            KikasaInkFX.Draw(spriteBatch);

            int dropType = ModContent.ProjectileType<KikasaInkDrop>();
            int pourType = ModContent.ProjectileType<KikasaInkPour>();
            int umbrellaType = ModContent.ProjectileType<KikasaRainUmbrella>();

            bool anyInk = false;
            bool anyUmbrella = false;
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile proj = Main.projectile[i];
                if (!proj.active) {
                    continue;
                }
                anyInk |= proj.type == dropType || proj.type == pourType;
                anyUmbrella |= proj.type == umbrellaType;
            }
            if (!anyInk && !anyUmbrella) {
                return;
            }

            Rectangle view = new((int)Main.screenPosition.X - 160, (int)Main.screenPosition.Y - 160,
                Main.screenWidth + 320, Main.screenHeight + 320);

            if (anyInk) {
                DrawInkBodies(spriteBatch, dropType, pourType, view);
            }
            if (anyUmbrella) {
                DrawUmbrellas(spriteBatch, umbrellaType, view);
            }
        }

        private static void DrawInkBodies(SpriteBatch spriteBatch, int dropType, int pourType, Rectangle view) {
            Effect ink = EffectLoader.KikasaInkDrop?.Value;
            Texture2D canvas = VaultAsset.placeholder2?.Value;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;

            if (ink != null && canvas != null && noise != null) {
                spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend,
                    SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone,
                    ink, Main.GameViewMatrix.TransformationMatrix);
                GraphicsDevice gd = Main.instance.GraphicsDevice;
                gd.Textures[1] = noise;
                gd.SamplerStates[1] = SamplerState.LinearWrap;

                //共享参数一帧一次,逐体只上形变参数
                ink.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
                ink.Parameters["uColBody"]?.SetValue(KikasaInk.InkBody.ToVector3());
                ink.Parameters["uColDeep"]?.SetValue(KikasaInk.InkDeep.ToVector3());
                ink.Parameters["uColCore"]?.SetValue(KikasaInk.BloodCore.ToVector3());
                ink.Parameters["uColSheen"]?.SetValue(KikasaInk.WetSheen.ToVector3());

                //墨瀑最底(体量大,垫在墨滴之下)
                for (int i = 0; i < Main.maxProjectiles; i++) {
                    Projectile proj = Main.projectile[i];
                    if (proj.active && proj.type == pourType
                        && proj.ModProjectile is KikasaInkPour pour) {
                        pour.DrawPourQuad(spriteBatch, ink, canvas);
                    }
                }
                for (int i = 0; i < Main.maxProjectiles; i++) {
                    Projectile proj = Main.projectile[i];
                    if (proj.active && proj.type == dropType
                        && view.Contains(proj.Center.ToPoint())
                        && proj.ModProjectile is KikasaInkDrop drop) {
                        drop.DrawInkQuad(spriteBatch, ink, canvas);
                    }
                }
                spriteBatch.End();
                return;
            }

            //精灵回退
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend,
                SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone,
                null, Main.GameViewMatrix.TransformationMatrix);
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile proj = Main.projectile[i];
                if (!proj.active) {
                    continue;
                }
                if (proj.type == pourType && proj.ModProjectile is KikasaInkPour pour) {
                    pour.DrawPourFallback(spriteBatch);
                }
                else if (proj.type == dropType && view.Contains(proj.Center.ToPoint())
                    && proj.ModProjectile is KikasaInkDrop drop) {
                    drop.DrawInk(spriteBatch);
                }
            }
            spriteBatch.End();
        }

        private static void DrawUmbrellas(SpriteBatch spriteBatch, int umbrellaType, Rectangle view) {
            Effect fx = EffectLoader.KikasaUmbrella?.Value;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;

            if (fx != null && noise != null) {
                spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend,
                    SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone,
                    fx, Main.GameViewMatrix.TransformationMatrix);
                GraphicsDevice gd = Main.instance.GraphicsDevice;
                gd.Textures[1] = noise;
                gd.SamplerStates[1] = SamplerState.LinearWrap;

                fx.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
                fx.Parameters["uColInk"]?.SetValue(KikasaInk.InkBody.ToVector3());
                fx.Parameters["uColDeep"]?.SetValue(KikasaInk.InkDeep.ToVector3());
                fx.Parameters["uColCore"]?.SetValue(KikasaInk.BloodCore.ToVector3());
                fx.Parameters["uColSheen"]?.SetValue(KikasaInk.WetSheen.ToVector3());

                for (int i = 0; i < Main.maxProjectiles; i++) {
                    Projectile proj = Main.projectile[i];
                    if (proj.active && proj.type == umbrellaType
                        && view.Contains(proj.Center.ToPoint())
                        && proj.ModProjectile is KikasaRainUmbrella umbrella) {
                        umbrella.DrawUmbrellaShader(spriteBatch, fx);
                    }
                }
                spriteBatch.End();

                //伞下鬼另开无着色器批:躲开 TechCanopy 对非伞贴图的染指
                DrawCanopyGhosts(spriteBatch, umbrellaType, view);
                return;
            }

            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend,
                SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone,
                null, Main.GameViewMatrix.TransformationMatrix);
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile proj = Main.projectile[i];
                if (proj.active && proj.type == umbrellaType
                    && view.Contains(proj.Center.ToPoint())
                    && proj.ModProjectile is KikasaRainUmbrella umbrella) {
                    umbrella.DrawUmbrella(spriteBatch);
                    umbrella.DrawCanopyGhosts(spriteBatch);
                }
            }
            spriteBatch.End();
        }

        /// <summary>伞下鬼批:伞体着色器批结束后单独一趟</summary>
        private static void DrawCanopyGhosts(SpriteBatch spriteBatch, int umbrellaType, Rectangle view) {
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend,
                SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone,
                null, Main.GameViewMatrix.TransformationMatrix);
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile proj = Main.projectile[i];
                if (proj.active && proj.type == umbrellaType
                    && view.Contains(proj.Center.ToPoint())
                    && proj.ModProjectile is KikasaRainUmbrella umbrella) {
                    umbrella.DrawCanopyGhosts(spriteBatch);
                }
            }
            spriteBatch.End();
        }
    }
}
