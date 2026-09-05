using CalamityOverhaul.Common;
using InnoVault.RenderHandles;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
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
            int columnType = ModContent.ProjectileType<KikasaBloodColumn>();
            int geyserType = ModContent.ProjectileType<KikasaInkGeyser>();

            //墨形态体与血形态体分两批(不同 Effect 不能共批);血珠/血柱/血索/血形态三泉走血批
            bool anyInk = false;
            bool anyBlood = false;
            bool anyUmbrella = false;
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile proj = Main.projectile[i];
                if (!proj.active) {
                    continue;
                }
                if (proj.type == dropType) {
                    if (proj.ModProjectile is KikasaInkDrop drop && drop.IsBloodBead) {
                        anyBlood = true;
                    }
                    else {
                        anyInk = true;
                    }
                }
                else if (proj.type == pourType) {
                    anyInk = true;
                }
                else if (proj.type == columnType) {
                    anyBlood = true;
                }
                else if (proj.type == geyserType) {
                    anyBlood |= proj.ModProjectile is KikasaInkGeyser geyser && geyser.BloodMode;
                }
                else if (proj.type == umbrellaType) {
                    anyUmbrella = true;
                    anyBlood |= proj.ModProjectile is KikasaRainUmbrella umbrella && umbrella.SiphonVisible;
                }
            }
            if (!anyInk && !anyBlood && !anyUmbrella) {
                return;
            }

            Rectangle view = new((int)Main.screenPosition.X - 160, (int)Main.screenPosition.Y - 160,
                Main.screenWidth + 320, Main.screenHeight + 320);

            if (anyInk) {
                DrawInkBodies(spriteBatch, dropType, pourType, view);
            }
            if (anyBlood) {
                DrawBloodBodies(spriteBatch, dropType, columnType, geyserType, umbrellaType, view);
            }
            if (anyUmbrella) {
                DrawUmbrellas(spriteBatch, umbrellaType, view);
            }
        }

        /// <summary>
        /// 血形态批(KikasaBloodRain):层序 血形态三泉 → 血柱 → 血索 → 血珠。
        /// 柱与索纵向体量大,可见性用加高的视窗判;共享参数一帧一次,逐体只上形变参数
        /// </summary>
        private static void DrawBloodBodies(SpriteBatch spriteBatch, int dropType, int columnType,
            int geyserType, int umbrellaType, Rectangle view) {
            Effect blood = EffectLoader.KikasaBloodRain?.Value;
            Texture2D canvas = VaultAsset.placeholder2?.Value;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            Rectangle tallView = view;
            tallView.Inflate(0, 420);

            if (blood != null && canvas != null && noise != null) {
                spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend,
                    SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone,
                    blood, Main.GameViewMatrix.TransformationMatrix);
                GraphicsDevice gd = Main.instance.GraphicsDevice;
                gd.Textures[1] = noise;
                gd.SamplerStates[1] = SamplerState.LinearWrap;
                blood.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
                blood.Parameters["uColGhost"]?.SetValue(KikasaInk.GhostCore.ToVector3());

                for (int i = 0; i < Main.maxProjectiles; i++) {
                    Projectile proj = Main.projectile[i];
                    if (proj.active && proj.type == geyserType && tallView.Contains(proj.Center.ToPoint())
                        && proj.ModProjectile is KikasaInkGeyser geyser && geyser.BloodMode) {
                        geyser.DrawBloodColumn(spriteBatch, blood, canvas);
                    }
                }
                for (int i = 0; i < Main.maxProjectiles; i++) {
                    Projectile proj = Main.projectile[i];
                    if (proj.active && proj.type == columnType && tallView.Contains(proj.Center.ToPoint())
                        && proj.ModProjectile is KikasaBloodColumn column) {
                        column.DrawColumnQuad(spriteBatch, blood, canvas);
                    }
                }
                for (int i = 0; i < Main.maxProjectiles; i++) {
                    Projectile proj = Main.projectile[i];
                    if (proj.active && proj.type == umbrellaType
                        && proj.ModProjectile is KikasaRainUmbrella umbrella && umbrella.SiphonVisible) {
                        umbrella.DrawSiphonQuad(spriteBatch, blood, canvas);
                    }
                }
                for (int i = 0; i < Main.maxProjectiles; i++) {
                    Projectile proj = Main.projectile[i];
                    if (proj.active && proj.type == dropType && view.Contains(proj.Center.ToPoint())
                        && proj.ModProjectile is KikasaInkDrop drop && drop.IsBloodBead) {
                        drop.DrawBloodQuad(spriteBatch, blood, canvas);
                    }
                }
                spriteBatch.End();
                return;
            }

            //精灵回退(血形态三泉的回退由泉自己的 PreDraw 承担)
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend,
                SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone,
                null, Main.GameViewMatrix.TransformationMatrix);
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile proj = Main.projectile[i];
                if (!proj.active) {
                    continue;
                }
                if (proj.type == columnType && tallView.Contains(proj.Center.ToPoint())
                    && proj.ModProjectile is KikasaBloodColumn column) {
                    column.DrawColumnFallback(spriteBatch);
                }
                else if (proj.type == umbrellaType
                    && proj.ModProjectile is KikasaRainUmbrella umbrella && umbrella.SiphonVisible) {
                    umbrella.DrawSiphonFallback(spriteBatch);
                }
                else if (proj.type == dropType && view.Contains(proj.Center.ToPoint())
                    && proj.ModProjectile is KikasaInkDrop drop && drop.IsBloodBead) {
                    drop.DrawInk(spriteBatch);
                }
            }
            spriteBatch.End();
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

                //共享参数一帧一次,逐体只上形变参数(瀑与滴各自重设四色,血形态瀑换血色板)
                ink.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
                ink.Parameters["uColBody"]?.SetValue(KikasaInk.InkBody.ToVector3());
                ink.Parameters["uColDeep"]?.SetValue(KikasaInk.InkDeep.ToVector3());
                ink.Parameters["uColCore"]?.SetValue(KikasaInk.BloodCore.ToVector3());
                ink.Parameters["uColSheen"]?.SetValue(KikasaInk.WetSheen.ToVector3());
                ink.Parameters["uColGhost"]?.SetValue(KikasaInk.GhostCore.ToVector3());

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
                        && proj.ModProjectile is KikasaInkDrop drop && !drop.IsBloodBead) {
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
                    && proj.ModProjectile is KikasaInkDrop drop && !drop.IsBloodBead) {
                    drop.DrawInk(spriteBatch);
                }
            }
            spriteBatch.End();
        }

        private static void DrawUmbrellas(SpriteBatch spriteBatch, int umbrellaType, Rectangle view) {
            Effect fx = EffectLoader.KikasaUmbrella?.Value;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;

            if (fx != null && noise != null) {
                //墨痕拖尾:顶点条带层必须画在 SpriteBatch 批之前,伞体随后盖在其上。
                //拖尾必须用独立 Effect,绝不与伞体批共享实例——vs+ps technique 在共享
                //实例上 Apply 过一次,MojoShader 的 current_vert_raw 就永久残留,之后
                //每次 ps-only pass 的 CommitChanges 都会把 transformMatrix 写进精灵
                //顶点着色器的投影寄存器,伞体整批瞬移出屏(2026-08 伞体隐形回归根因)
                DrawInkTrails(EffectLoader.KikasaUmbrellaTrail?.Value, noise, umbrellaType);

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

        //==================== 墨痕拖尾(TechTrail 世界空间条带) ====================

        private static VertexPositionColorTexture[] trailVertexBuf = new VertexPositionColorTexture[128];

        /// <summary>
        /// 逐伞画墨痕条带:自管设备状态的顶点图元层,TriangleStrip 世界坐标经
        /// transformMatrix 直入(勿减 screenPosition);fx 为独立的
        /// KikasaUmbrellaTrail 效果,缺席直接跳过——拖尾是纯锦上添花层,无 CPU 回退
        /// </summary>
        private static void DrawInkTrails(Effect fx, Texture2D noise, int umbrellaType) {
            if (fx == null) {
                return;
            }
            GraphicsDevice gd = Main.instance.GraphicsDevice;
            bool deviceReady = false;
            BlendState origBlend = null;
            RasterizerState origRaster = null;

            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile proj = Main.projectile[i];
                if (!proj.active || proj.type != umbrellaType
                    || proj.ModProjectile is not KikasaRainUmbrella umbrella
                    || umbrella.TrailPoints.Count < 2 || umbrella.TrailHeadFade <= 0.02f
                    || !TrailOnScreen(umbrella.TrailPoints)) {
                    continue;
                }

                if (!deviceReady) {
                    deviceReady = true;
                    origBlend = gd.BlendState;
                    origRaster = gd.RasterizerState;
                    //暗墨要读作黑:预乘输出进 AlphaBlend,加色批画不出黑
                    gd.BlendState = BlendState.AlphaBlend;
                    gd.RasterizerState = RasterizerState.CullNone;
                    gd.Textures[1] = noise;
                    gd.SamplerStates[1] = SamplerState.LinearWrap;
                    fx.Parameters["transformMatrix"]?.SetValue(VaultUtils.GetTransfromMatrix());
                    fx.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
                    fx.Parameters["uColInk"]?.SetValue(KikasaInk.InkBody.ToVector3());
                    fx.Parameters["uColDeep"]?.SetValue(KikasaInk.InkDeep.ToVector3());
                    fx.Parameters["uColCore"]?.SetValue(KikasaInk.BloodCore.ToVector3());
                    fx.Parameters["uColSheen"]?.SetValue(KikasaInk.WetSheen.ToVector3());
                    fx.CurrentTechnique = fx.Techniques["TechTrail"];
                }
                fx.Parameters["uSeed"]?.SetValue(proj.identity * 0.173f % 4f);
                DrawOneInkTrail(gd, fx, umbrella);
            }

            if (deviceReady) {
                gd.BlendState = origBlend;
                gd.RasterizerState = origRaster;
            }
        }

        /// <summary>包围盒粗剔除:整条墨痕在屏外(含宽度余量)则跳过</summary>
        private static bool TrailOnScreen(List<KikasaRainUmbrella.InkTrailPoint> pts) {
            float minX = float.MaxValue, minY = float.MaxValue;
            float maxX = float.MinValue, maxY = float.MinValue;
            for (int i = 0; i < pts.Count; i++) {
                Vector2 p = pts[i].Pos;
                if (p.X < minX) minX = p.X;
                if (p.X > maxX) maxX = p.X;
                if (p.Y < minY) minY = p.Y;
                if (p.Y > maxY) maxY = p.Y;
            }
            const float pad = 64f;   //幕级半宽 ~34px + 尾端垂坠 ~14px 的余量
            Vector2 screen = Main.screenPosition;
            return maxX + pad >= screen.X && minX - pad <= screen.X + Main.screenWidth
                && maxY + pad >= screen.Y && minY - pad <= screen.Y + Main.screenHeight;
        }

        /// <summary>
        /// 单伞条带:末尾追加实时头点(头部不留采样步长的滞后缝),
        /// 每点按点龄二次方叠加下坠偏移——墨有重量,离伞即缓缓下沉
        /// </summary>
        private static void DrawOneInkTrail(GraphicsDevice gd, Effect fx, KikasaRainUmbrella umbrella) {
            List<KikasaRainUmbrella.InkTrailPoint> pts = umbrella.TrailPoints;
            int count = pts.Count + 1;
            if (trailVertexBuf.Length < count * 2) {
                trailVertexBuf = new VertexPositionColorTexture[count * 2 + 32];
            }

            long now = Main.GameUpdateCount;
            float headFade = umbrella.TrailHeadFade;
            float widthScale = umbrella.TrailVisualScale;
            KikasaRainUmbrella.InkTrailPoint lastPt = pts[^1];
            Vector2 headPos = umbrella.Projectile.Center;
            float headDist = lastPt.Dist + Vector2.Distance(lastPt.Pos, headPos);

            //尾端包络:末端约两成点位随点序压向 0。高速拖幕时最老点死于
            //MaxPoints 硬删而非寿命过期(被删时寿命还剩大半,满宽实心段一帧
            //消失=平切断裂);包络保证无论哪条死亡路径,末端都先撕散再消失,
            //端点收零(柱/束长条效果两端收口纪律)
            int tailN = Math.Max(4, count / 5);
            float TailT(int idx) => MathHelper.Clamp(idx / (float)tailN, 0f, 1f);

            Vector2 PosAt(int idx) {
                if (idx >= pts.Count) {
                    return headPos;
                }
                float lifeT = MathHelper.Clamp(
                    (pts[idx].DeathAt - now) / (float)KikasaRainUmbrella.TrailLifetime, 0f, 1f);
                //幕级垂坠:尾端轴心约 11px,配合下缘追加共 14px 量级;
                //吃包络后的等效寿命,硬删尾同样先坠再散
                float eff = MathF.Min(lifeT, TailT(idx));
                float sag = (1f - eff) * (1f - eff) * 11f;
                return pts[idx].Pos + new Vector2(0f, sag);
            }

            Vector2 prevNormal = default;
            for (int i = 0; i < count; i++) {
                Vector2 pos = PosAt(i);
                float lifeT;
                float strength;
                float dist;
                if (i < pts.Count) {
                    lifeT = MathHelper.Clamp(
                        (pts[i].DeathAt - now) / (float)KikasaRainUmbrella.TrailLifetime, 0f, 1f);
                    strength = pts[i].Strength;
                    dist = pts[i].Dist;
                }
                else {
                    lifeT = 1f;
                    strength = umbrella.TrailStrength;
                    dist = headDist;
                }

                //中心差分切向,路径折返时翻转法线保持条带连续不打结
                Vector2 dirA = i > 0 ? pos - PosAt(i - 1) : PosAt(i + 1) - pos;
                Vector2 dirB = i < count - 1 ? PosAt(i + 1) - pos : pos - PosAt(i - 1);
                Vector2 normal = (dirA + dirB).SafeNormalize(Vector2.UnitX).RotatedBy(MathHelper.PiOver2);
                if (i > 0 && Vector2.Dot(normal, prevNormal) < 0f) {
                    normal = -normal;
                }
                prevNormal = normal;

                //等效寿命=寿命与尾端包络取小:尾端点即使寿命充足也按濒死老段
                //处理,自动吃 shader 现有撕丝+熄灭管线,材质故事统一
                float effLife = MathF.Min(lifeT, TailT(i));

                //宽度:幕级量宽——满速峰值全宽≈伞盖直径(halfW 34×widthScale);
                //峰值压在离头约两成处,头端收窄让墨幕读作"从伞底钻出来"
                //而非方头探出伞缘;尾端随等效寿命收窄到零
                float s = count > 1 ? i / (float)(count - 1) : 1f;
                float headTaper = 1f - 0.35f * MathF.Max(0f, (s - 0.78f) / 0.22f);
                float halfW = (12f + 22f * strength) * widthScale
                    * MathF.Pow(effLife, 0.62f) * headTaper;
                //顶点色 R=等效寿命 G=速度强度 A=头部整体透明度,与 fx 契约一致
                Color data = new(effLife, strength, 0f, headFade);
                float u = dist / 32f;
                Vector2 off = normal * halfW;
                Vector2 pA = new(pos.X + off.X, pos.Y + off.Y);
                Vector2 pB = new(pos.X - off.X, pos.Y - off.Y);
                //幕下缘比上缘再多坠一分:布/液体的下摆感
                float sagExtra = (1f - effLife) * (1f - effLife) * 3.5f;
                if (pA.Y >= pB.Y) {
                    pA.Y += sagExtra;
                }
                else {
                    pB.Y += sagExtra;
                }
                trailVertexBuf[i * 2] = new VertexPositionColorTexture(
                    new Vector3(pA.X, pA.Y, 0f), data, new Vector2(u, 0f));
                trailVertexBuf[i * 2 + 1] = new VertexPositionColorTexture(
                    new Vector3(pB.X, pB.Y, 0f), data, new Vector2(u, 1f));
            }

            foreach (EffectPass pass in fx.CurrentTechnique.Passes) {
                pass.Apply();
                gd.DrawUserPrimitives(PrimitiveType.TriangleStrip, trailVertexBuf, 0, count * 2 - 2);
            }
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
