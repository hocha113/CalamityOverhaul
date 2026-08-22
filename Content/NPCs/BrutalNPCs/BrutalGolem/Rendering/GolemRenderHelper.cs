using CalamityOverhaul.Common;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalGolem.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalGolem.States;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.GameContent;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalGolem.Rendering
{
    /// <summary>石巨人渲染辅助：岩浆脉络/宝石充能/崩解侵蚀/拳残影</summary>
    internal static class GolemRenderHelper
    {
        /// <summary>岩浆脉络覆盖层：贴体采样身体贴图，脉络亮度随 VeinGlow</summary>
        internal static void DrawMagmaVeins(SpriteBatch sb, NPC npc, GolemStateContext ctx) {
            float glow = ctx?.VeinGlow ?? 0f;
            if (glow < 0.03f) {
                return;
            }
            Effect shader = EffectLoader.GolemMagmaVein?.Value;
            if (shader == null) {
                //兜底：宝石处热光
                Texture2D soft = CWRAsset.SoftGlow.Value;
                Vector2 gemPos = npc.Center + new Vector2(0f, -6f) - Main.screenPosition;
                sb.Draw(soft, gemPos, null, new Color(255, 160, 60, 0) * (0.5f * glow),
                    0f, soft.Size() / 2f, 0.8f + 0.3f * glow, SpriteEffects.None, 0f);
                return;
            }

            Texture2D body = TextureAssets.Npc[npc.type].Value;
            Rectangle frame = npc.frame;
            Vector2 origin = frame.Size() / 2f;
            Vector2 drawPos = npc.Center - Main.screenPosition;

            sb.End();
            sb.Begin(SpriteSortMode.Immediate, BlendState.Additive, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

            shader.CurrentTechnique = shader.Techniques["VeinTech"];
            shader.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            shader.Parameters["uGlow"]?.SetValue(glow);
            shader.Parameters["uCrumble"]?.SetValue(0f);
            //帧区域归一（防串帧）
            shader.Parameters["uFrame"]?.SetValue(new Vector4(
                frame.X / (float)body.Width, frame.Y / (float)body.Height,
                frame.Width / (float)body.Width, frame.Height / (float)body.Height));
            //噪声显式绑到 s1：SpriteBatch.Draw 会把 s0 覆写成本体贴图，
            //参数式贴图绑定实机失效（合同同 ShockRingDraw.Draw）
            GraphicsDevice gd = Main.instance.GraphicsDevice;
            gd.Textures[1] = CWRAsset.PerlinNoise.Value;
            gd.SamplerStates[1] = SamplerState.LinearWrap;
            shader.CurrentTechnique.Passes[0].Apply();

            sb.Draw(body, drawPos, frame, Color.White, npc.rotation, origin, npc.scale, SpriteEffects.None, 0f);

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
        }

        /// <summary>宝石蓄力：旋涡吸积 + 核心亮斑</summary>
        internal static void DrawGemCharge(SpriteBatch sb, NPC npc, GolemStateContext ctx) {
            float progress = ctx.ChargeProgress;
            Vector2 gemPos = npc.Center + new Vector2(0f, -6f) - Main.screenPosition;
            Texture2D soft = CWRAsset.SoftGlow.Value;
            Texture2D cyclone = CWRAsset.Cyclone.Value;

            Color main = ctx.ChargeType >= 2 ? new Color(255, 150, 40) : new Color(255, 200, 90);

            //默认预乘批直接画，A=0 颜色即加色；
            //显式 BlendState.Additive 源因子是 SrcAlpha，A=0 顶点色会整段乘零导致不可见
            //旋涡吸积盘
            float spin = Main.GlobalTimeWrappedHourly * (2.2f + progress * 3f);
            float size = 0.5f + progress * 0.9f;
            sb.Draw(cyclone, gemPos, null, (main with { A = 0 }) * (0.5f * progress),
                spin, cyclone.Size() / 2f, size, SpriteEffects.None, 0f);
            sb.Draw(cyclone, gemPos, null, (Color.White with { A = 0 }) * (0.3f * progress),
                -spin * 0.7f, cyclone.Size() / 2f, size * 0.6f, SpriteEffects.None, 0f);
            //核心亮斑
            sb.Draw(soft, gemPos, null, (main with { A = 0 }) * (0.4f + 0.6f * progress),
                0f, soft.Size() / 2f, 0.5f + progress * 0.7f, SpriteEffects.None, 0f);
        }

        /// <summary>死亡演出：崩解侵蚀绘制（接管主绘制）</summary>
        internal static void DrawBodyCrumble(SpriteBatch sb, NPC npc, GolemStateContext ctx, Vector2 screenPos, Color drawColor) {
            int deathTimer = ctx?.DeathTimer ?? 0;
            float crumble = GolemDeathState.GetCrumble(deathTimer);

            Texture2D body = TextureAssets.Npc[npc.type].Value;
            Rectangle frame = npc.frame;
            Vector2 origin = frame.Size() / 2f;
            Vector2 drawPos = npc.Center - screenPos;

            Effect shader = EffectLoader.GolemMagmaVein?.Value;
            if (shader != null && crumble < 0.999f) {
                sb.End();
                sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.LinearClamp,
                    DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

                shader.CurrentTechnique = shader.Techniques["CrumbleTech"];
                shader.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
                shader.Parameters["uGlow"]?.SetValue(1f);
                shader.Parameters["uCrumble"]?.SetValue(crumble);
                shader.Parameters["uFrame"]?.SetValue(new Vector4(
                    frame.X / (float)body.Width, frame.Y / (float)body.Height,
                    frame.Width / (float)body.Width, frame.Height / (float)body.Height));
                shader.Parameters["uColor"]?.SetValue(drawColor.ToVector4());
                //噪声显式绑到 s1：SpriteBatch.Draw 会把 s0 覆写成本体贴图，
                //参数式贴图绑定实机失效（合同同 ShockRingDraw.Draw）
                GraphicsDevice gd = Main.instance.GraphicsDevice;
                gd.Textures[1] = CWRAsset.PerlinNoise.Value;
                gd.SamplerStates[1] = SamplerState.LinearWrap;
                shader.CurrentTechnique.Passes[0].Apply();

                sb.Draw(body, drawPos, frame, Color.White, npc.rotation, origin, npc.scale, SpriteEffects.None, 0f);

                //存留区叠满强度岩浆脉络（与侵蚀线同步遮罩）
                sb.End();
                sb.Begin(SpriteSortMode.Immediate, BlendState.Additive, SamplerState.LinearClamp,
                    DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
                shader.CurrentTechnique = shader.Techniques["VeinTech"];
                shader.Parameters["uGlow"]?.SetValue(1f);
                shader.Parameters["uCrumble"]?.SetValue(crumble);
                shader.CurrentTechnique.Passes[0].Apply();
                sb.Draw(body, drawPos, frame, Color.White, npc.rotation, origin, npc.scale, SpriteEffects.None, 0f);

                sb.End();
                sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                    DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
            }
            else if (crumble < 0.999f) {
                //兜底：整体透明化
                sb.Draw(body, drawPos, frame, drawColor * (1f - crumble),
                    npc.rotation, origin, npc.scale, SpriteEffects.None, 0f);
            }

            //宝石谢幕层
            if (ctx != null && ctx.DeathPhase == GolemDeathPhase.GemFinale) {
                DrawGemFinale(sb, npc, deathTimer, screenPos);
            }
        }

        /// <summary>宝石谢幕：太阳宝石浮出废墟，碎响间隙闪烁</summary>
        private static void DrawGemFinale(SpriteBatch sb, NPC npc, int deathTimer, Vector2 screenPos) {
            Vector2 gemPos = GolemDeathState.GolemRenderHelperGemPos(npc, deathTimer) - screenPos;
            Texture2D soft = CWRAsset.SoftGlow.Value;
            Texture2D star = CWRAsset.StarTexture.Value;

            //终爆后不再绘制
            if (deathTimer >= 326) {
                return;
            }

            float t = MathHelper.Clamp((deathTimer - GolemDeathState.CollapseEnd) / 20f, 0f, 1f);
            //碎响帧白闪
            float crackFlash = 0f;
            if (deathTimer is >= 292 and < 296 || deathTimer is >= 307 and < 311 || deathTimer is >= 318 and < 322) {
                crackFlash = 1f;
            }
            float pulse = 0.8f + 0.2f * (float)Math.Sin(deathTimer * 0.3f);

            Color gold = new Color(255, 200, 90, 0);
            sb.Draw(soft, gemPos, null, gold * (0.85f * t * pulse),
                0f, soft.Size() / 2f, 0.9f + crackFlash * 0.4f, SpriteEffects.None, 0f);
            sb.Draw(soft, gemPos, null, (Color.White with { A = 0 }) * (0.7f * t),
                0f, soft.Size() / 2f, 0.4f + crackFlash * 0.25f, SpriteEffects.None, 0f);
            sb.Draw(star, gemPos, null, gold * (0.8f * t * pulse),
                deathTimer * 0.02f, star.Size() / 2f, 0.16f + crackFlash * 0.08f, SpriteEffects.None, 0f);
        }

        /// <summary>高速残影（速度门控，通用于拳/飞头）；overrideSpeed≥0 时代替实时速度</summary>
        internal static void DrawFistTrail(SpriteBatch sb, NPC npc, Vector2 screenPos, float overrideSpeed = -1f) {
            float speed = overrideSpeed >= 0f ? overrideSpeed : npc.velocity.Length();
            float heat = MathHelper.Clamp((speed - 13f) / 22f, 0f, 1f);
            if (heat <= 0.05f || npc.oldPos.Length == 0) {
                return;
            }

            Texture2D tex = TextureAssets.Npc[npc.type].Value;
            Rectangle frame = npc.frame;
            if (frame.Width <= 0 || frame.Height <= 0) {
                frame = new Rectangle(0, 0, tex.Width, Math.Max(tex.Height / Math.Max(Main.npcFrameCount[npc.type], 1), 1));
            }
            Vector2 origin = frame.Size() / 2f;

            float alpha = 0.36f * heat;
            for (int i = 1; i < npc.oldPos.Length; i += 2) {
                Vector2 drawOldPos = npc.oldPos[i] + npc.Size / 2f - screenPos;
                Color trailColor = Color.Lerp(new Color(255, 170, 70, 0), new Color(140, 60, 20, 0), i / (float)npc.oldPos.Length);
                sb.Draw(tex, drawOldPos, frame, trailColor * alpha,
                    npc.rotation, origin, npc.scale, SpriteEffects.None, 0f);
                alpha *= 0.82f;
            }
        }

        /// <summary>
        /// 拳部推进器喷焰（火箭拳身份件）：随状态切换喷焰语言
        /// 飞行全开随速伸缩 / 蓄力喷口预热脉冲 / 反弹侧向修正喷 / 回归反向减速喷 / 待机导火苗
        /// </summary>
        internal static void DrawFistThruster(SpriteBatch sb, NPC npc, GolemFistStateContext ctx) {
            if (ctx == null) {
                return;
            }

            //肩口发射闪（出拳点火余辉，默认批 A=0 加色）
            Texture2D soft = CWRAsset.SoftGlow.Value;
            if (ctx.MuzzleFlash > 0) {
                float mf = ctx.MuzzleFlash / 12f;
                Vector2 mp = ctx.MuzzlePos - Main.screenPosition;
                sb.Draw(soft, mp, null, new Color(255, 180, 70, 0) * (0.55f * mf),
                    0f, soft.Size() / 2f, 1.25f * (1.9f - mf), SpriteEffects.None, 0f);
            }

            GolemFistStateIndex st = (GolemFistStateIndex)(int)npc.ai[GolemAiSlots.PartStateSlot];

            //拳离膛期：肩口发射座余温（拳与躯干的视觉粘接改由发射口语言承担），随躯干淡出收光
            if (st is GolemFistStateIndex.Punch or GolemFistStateIndex.Return
                && ctx.Body != null && ctx.Body.active) {
                Vector2 socket = GolemFacts.FistAnchor(ctx.Body, ctx.Side) - Main.screenPosition;
                float breath = 0.75f + 0.25f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 11f + ctx.Side);
                float bodyFade = 1f - ctx.Body.alpha / 255f;
                sb.Draw(soft, socket, null, new Color(255, 150, 55, 0) * (0.3f * breath * bodyFade),
                    0f, soft.Size() / 2f, 0.5f, SpriteEffects.None, 0f);
            }

            //拳面朝向：状态按速度对齐 rotation，左拳贴图朝 -X 需镜像
            Vector2 forward = npc.rotation.ToRotationVector2() * (ctx.Side < 0 ? -1f : 1f);
            Vector2 vel = ctx.ThrustVel;
            float speed = vel.Length();

            float power, len, width;
            Vector2 dir;
            switch (st) {
                case GolemFistStateIndex.Punch:
                    //飞行全开：焰长随速伸缩
                    power = MathHelper.Clamp(speed / 24f, 0.4f, 1f);
                    len = 46f + speed * 5f;
                    width = 34f;
                    dir = speed > 1f ? -vel / speed : -forward;
                    break;
                case GolemFistStateIndex.Windup: {
                    //喷口预热：间歇小脉冲，随蓄力升温
                    float pulse = Math.Max((float)Math.Sin(Main.GlobalTimeWrappedHourly * 24f + ctx.Side * 2.1f), 0f);
                    power = (0.2f + 0.6f * pulse) * MathHelper.Clamp(ctx.WindupGlow * 1.6f, 0.15f, 1f);
                    len = 20f + 26f * ctx.WindupGlow * pulse;
                    width = 22f;
                    dir = -forward;
                    break;
                }
                case GolemFistStateIndex.Return:
                    //反向减速喷：焰口朝行进方向刹车
                    //回收期拳背领先（forward=拳峰=逆行进向），零速兜底取 -forward 才与主分支同向
                    power = MathHelper.Clamp(speed / 26f, 0.25f, 0.75f);
                    len = 28f + speed * 2.6f;
                    width = 24f;
                    dir = speed > 1f ? vel / speed : -forward;
                    break;
                case GolemFistStateIndex.Guard:
                    //编队维持喷：低功率常明
                    power = 0.3f;
                    len = 24f;
                    width = 18f;
                    dir = -forward;
                    break;
                case GolemFistStateIndex.DeathFall:
                    //垂死机件：坠落中间歇喷溅（相位按 whoAmI 去相关），落地熄火
                    power = speed > 2f && (Main.GameUpdateCount + (uint)(npc.whoAmI * 7)) % 14 < 5 ? 0.4f : 0f;
                    len = 26f;
                    width = 18f;
                    dir = speed > 1f ? -vel / speed : -forward;
                    break;
                default:
                    //Anchor 待机导火苗（机关未熄）
                    power = 0.16f + 0.06f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 9f + npc.whoAmI);
                    len = 14f;
                    width = 14f;
                    dir = -forward;
                    break;
            }

            //反弹瞬间侧向修正喷全开
            if (ctx.BounceBurst > 0) {
                float b = ctx.BounceBurst / 10f;
                power = Math.Max(power, 0.6f + 0.4f * b);
                len = Math.Max(len, 70f + 50f * b);
            }

            //生成淡入/沉地退场期随本体透明度收焰，透明拳不挂满功率喷焰
            power *= 1f - npc.alpha / 255f;

            if (power <= 0.04f) {
                return;
            }

            Vector2 nozzleScreen = npc.Center + dir * 14f * npc.scale - Main.screenPosition;

            //喷口亮斑
            sb.Draw(soft, nozzleScreen, null, new Color(255, 200, 110, 0) * (0.55f * power),
                0f, soft.Size() / 2f, 0.34f + 0.2f * power, SpriteEffects.None, 0f);

            Effect shader = EffectLoader.GolemThruster?.Value;
            if (shader == null) {
                DrawThrusterFallback(sb, nozzleScreen, dir, len, power);
                return;
            }

            Texture2D noise = CWRAsset.PerlinNoise.Value;
            sb.End();
            sb.Begin(SpriteSortMode.Immediate, BlendState.Additive, SamplerState.LinearWrap,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

            shader.CurrentTechnique = shader.Techniques["FlameTech"];
            shader.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            shader.Parameters["uPower"]?.SetValue(power);
            shader.Parameters["uSeed"]?.SetValue(npc.whoAmI * 0.37f + (ctx.Side < 0 ? 0f : 0.5f));
            shader.Parameters["uAspect"]?.SetValue(len / Math.Max(width, 1f));
            shader.CurrentTechnique.Passes[0].Apply();

            //quad 本体即噪声贴图（刻意 s0，LinearWrap 批）；origin 左端中点，+X 即喷向
            sb.Draw(noise, nozzleScreen, null, Color.White, dir.ToRotation(),
                new Vector2(0f, noise.Height / 2f),
                new Vector2(len / noise.Width, width / (float)noise.Height),
                SpriteEffects.None, 0f);

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
        }

        /// <summary>着色器缺失回退：沿喷向叠瓣粒子喷流（白热→琥珀→深红），不许无形</summary>
        private static void DrawThrusterFallback(SpriteBatch sb, Vector2 nozzleScreen, Vector2 dir, float len, float power) {
            Texture2D soft = CWRAsset.SoftGlow.Value;
            const int blobs = 4;
            for (int i = 0; i < blobs; i++) {
                float t = i / (float)(blobs - 1);
                //确定性闪烁，避免绘制线随机
                float flick = 0.9f + 0.1f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 40f + i * 2.3f);
                Vector2 pos = nozzleScreen + dir * (len * 0.85f * t);
                Color c = Color.Lerp(new Color(255, 240, 200, 0), new Color(150, 40, 8, 0), t);
                float s = MathHelper.Lerp(0.5f, 0.16f, t) * flick;
                sb.Draw(soft, pos, null, c * (power * (1f - t * 0.7f)), 0f, soft.Size() / 2f, s, SpriteEffects.None, 0f);
            }
        }

        /// <summary>拳蓄力辉光：汇聚亮斑 + 星芒（末段收缩）</summary>
        internal static void DrawFistWindup(SpriteBatch sb, NPC npc, GolemFistStateContext ctx) {
            float glow = ctx.WindupGlow;
            Texture2D soft = CWRAsset.SoftGlow.Value;
            Texture2D star = CWRAsset.StarGlow01.Value;
            Vector2 drawPos = npc.Center - Main.screenPosition;

            //爆发前收缩：越满越小越亮
            float shrink = MathHelper.Lerp(1.15f, 0.62f, glow);
            Color gold = new Color(255, 190, 80, 0);
            sb.Draw(soft, drawPos, null, gold * (0.55f * glow), 0f,
                soft.Size() / 2f, 1.1f * shrink, SpriteEffects.None, 0f);
            sb.Draw(star, drawPos, null, (Color.White with { A = 0 }) * (0.75f * glow),
                Main.GlobalTimeWrappedHourly * 3f, star.Size() / 2f, 0.2f * shrink, SpriteEffects.None, 0f);
        }
    }
}
