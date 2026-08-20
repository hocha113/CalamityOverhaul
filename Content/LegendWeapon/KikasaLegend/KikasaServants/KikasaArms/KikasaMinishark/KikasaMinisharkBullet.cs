using CalamityOverhaul.Common;
using CalamityOverhaul.Content.LegendWeapon.HalibutLegend.FishSkills;
using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaDomains;
using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaServants.KikasaEye;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using InnoVault.Trails;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaServants.KikasaArms.KikasaMinishark
{
    /// <summary>
    /// 械奴鲨群的湖水滴弹：血痰（<see cref="KikasaEyeBloodShot"/>）的枪弹化移植——
    /// 同一套有体积的液团语法：三层液团头（暗血压边→血红主体→血沫亮芯湿反光）
    /// 带表面张力抖动，身后拖一条会珠化断裂的粘血线（复用灵液液柱条带 shader 换血色板），
    /// 飞行中失稳甩珠；整体缩小约四分之一。弹道改成枪弹的快与直：
    /// 不吃重力、复利续力越飞越钻、只带极小幅鱼摆尾（转向恒为弧）。
    /// 命中窄扇迸溅、贴壁留渍（手动地形检测只认水线上真地形）、落空坠湖被收走
    /// </summary>
    internal class KikasaMinisharkBullet : ModProjectile, IPrimitiveDrawable
    {
        public override string Texture => CWRConstant.Masking + "Extra_98";

        /// <summary>各端本地计帧，仅供表现淡入与抖动相位（extraUpdates 下按更新递增）</summary>
        private ref float Life => ref Projectile.localAI[0];

        private Trail trail;
        //贴壁演出已放，OnKill 不再补迸溅
        private bool burstDone;
        //被湖收走：谢幕换成涟漪，不走迸溅
        private bool lakeSwallowed;

        /// <summary>连续量抖动的确定性相位（绘制路径不掷 Main.rand）</summary>
        private float Seed => Projectile.identity * 0.7391f % 3.71f;

        /// <summary>出生 4 更新淡入，避免第一帧硬弹出</summary>
        private float VisualFade => MathHelper.Clamp(Life / 4f, 0f, 1f);

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 13;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 12;
            Projectile.height = 12;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Summon;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 120;
            Projectile.extraUpdates = 1;
            //不吃引擎地形碰撞：湖下真地形被湖面演出盖住，撞上去像凭空截停；
            //贴壁迸溅+留渍改走 AI 内手动检测（只认水线以上的真地形）
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
        }

        public override void AI() {
            Life++;

            //枪弹弹道：不吃重力、复利续力越飞越钻 + 极小幅鱼摆尾（转向恒为弧，快时收紧）
            Projectile.velocity *= 1.009f;
            float sway = MathF.Sin(Life * 0.5f + Projectile.identity * 1.3f)
                * 0.009f * MathHelper.Clamp(28f / (Projectile.velocity.Length() + 1f), 0.5f, 1f);
            Projectile.velocity = Projectile.velocity.RotatedBy(sway);
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;

            //表面张力失稳：从团身后侧撕下小血珠（比血痰稀一半，枪弹密不轰屏）
            if (!Main.dedServ && (int)Life % 6 == 0) {
                Vector2 dir = Projectile.velocity.SafeNormalize(Vector2.UnitY);
                Vector2 spawnPos = Projectile.Center - dir * Main.rand.NextFloat(5f, 13f);
                Vector2 dropVel = Projectile.velocity * Main.rand.NextFloat(0.15f, 0.35f)
                    + dir.RotatedBy(MathHelper.PiOver2) * Main.rand.NextFloat(-1f, 1f);
                PRTLoader.NewParticle<PRT_KikasaBloodGlob>(spawnPos, dropVel,
                    Main.rand.NextBool(3) ? KikasaMinisharkServant.BloodDeep : KikasaMinisharkServant.BloodMain,
                    Main.rand.NextFloat(0.28f, 0.45f))?.Configure(Main.rand.Next(12, 20));
            }

            float glow = 0.42f * VisualFade;
            Lighting.AddLight(Projectile.Center, 0.5f * glow, 0.12f * glow, 0.11f * glow);

            //落空坠回血湖：湖收回自己的水，不迸溅
            Player owner = Main.player[Projectile.owner];
            bool lakeAlive = owner?.active == true
                && owner.TryGetModPlayer(out KikasaDomainPlayer domain)
                && domain.AnyActive && domain.RiseT > 0.5f;
            KikasaDomainPlayer kdp = lakeAlive ? owner.GetModPlayer<KikasaDomainPlayer>() : null;
            if (lakeAlive && Projectile.Center.Y >= kdp.LakeWorldY + 4f) {
                lakeSwallowed = true;
                if (!Main.dedServ && KikasaDomain.Viewed == kdp) {
                    KikasaDomainDeco.RippleAt(new Vector2(Projectile.Center.X, kdp.LakeWorldY), 0.55f);
                    KikasaDomainDeco.SplashAt(new Vector2(Projectile.Center.X, kdp.LakeWorldY), 3);
                }
                SoundEngine.PlaySound(SoundID.Drip with { Volume = 0.32f, Pitch = -0.15f, MaxInstances = 3 }, Projectile.Center);
                Projectile.Kill();
                return;
            }

            //贴壁：迸溅 + 留渍——湖线以下的真地形被湖面盖住，交给上面的落湖收走
            if (Life > 3
                && (!lakeAlive || Projectile.Center.Y < kdp.LakeWorldY - 2f)
                && Collision.SolidCollision(Projectile.position, Projectile.width, Projectile.height)) {
                burstDone = true;
                ImpactBurst(Projectile.Center, Projectile.velocity, onTile: true);
                Projectile.Kill();
            }
        }

        //==================== 命中与谢幕 ====================

        public override void OnKill(int timeLeft) {
            if (Main.dedServ || lakeSwallowed) {
                return;
            }
            if (!burstDone) {
                //命中 NPC / 超时坠灭共用（penetrate=1，Kill 各端都跑，队友也看得见）
                ImpactBurst(Projectile.Center, Projectile.velocity, onTile: false);
            }
            //血线失压散珠：拖尾旧位上留几粒回落的残珠
            Vector2[] oldPos = Projectile.oldPos;
            if (oldPos == null) {
                return;
            }
            for (int i = 2; i < oldPos.Length; i += 5) {
                if (oldPos[i] == Vector2.Zero) {
                    continue;
                }
                Vector2 pos = oldPos[i] + Projectile.Size * 0.5f;
                PRTLoader.NewParticle<PRT_KikasaBloodGlob>(pos + Main.rand.NextVector2Circular(3f, 3f),
                    Projectile.velocity * 0.08f + Main.rand.NextVector2Circular(0.6f, 0.6f),
                    Main.rand.NextBool(3) ? KikasaMinisharkServant.BloodDeep : KikasaMinisharkServant.BloodMain,
                    Main.rand.NextFloat(0.26f, 0.45f))?.Configure(Main.rand.Next(12, 22));
            }
        }

        /// <summary>滴弹命中：窄扇前向迸溅 + 一粒沉珠 + 细环——比血痰收一号，密射不轰屏</summary>
        private static void ImpactBurst(Vector2 pos, Vector2 impactVel, bool onTile) {
            if (Main.dedServ) {
                return;
            }
            Vector2 dir = impactVel.SafeNormalize(Vector2.UnitX);
            float angle = dir.ToRotation();

            //窄扇：水珠贴着入射向前钻
            for (int i = 0; i < 5; i++) {
                Vector2 vel = dir.RotatedBy(Main.rand.NextFloat(-0.3f, 0.3f))
                    * Main.rand.NextFloat(2.2f, 6f);
                PRTLoader.NewParticle<PRT_KikasaBloodGlob>(pos + Main.rand.NextVector2Circular(3f, 3f),
                    vel, Main.rand.NextBool(3) ? KikasaMinisharkServant.BloodDeep : KikasaMinisharkServant.BloodMain,
                    Main.rand.NextFloat(0.32f, 0.55f))?.Configure(Main.rand.Next(14, 24));
            }
            //一粒沉珠：坠得急，给收口一点分量
            PRTLoader.NewParticle<PRT_KikasaBloodGlob>(pos,
                dir.RotatedByRandom(0.5f) * Main.rand.NextFloat(1.4f, 3f),
                KikasaMinisharkServant.BloodDeep,
                Main.rand.NextFloat(0.6f, 0.8f))?.Configure(Main.rand.Next(20, 32), 0.4f);
            PRTLoader.NewParticle<PRT_DWave>(pos, Vector2.Zero, KikasaMinisharkServant.BloodBright, 0.045f)
                ?.Configure(new Vector2(0.5f, 1f), angle, 0.14f, 7);
            if (onTile) {
                PRTLoader.NewParticle<PRT_KikasaBloodSmear>(pos - dir * 2f, Vector2.Zero,
                    KikasaMinisharkServant.BloodMain, Main.rand.NextFloat(0.5f, 0.7f))
                    ?.Configure(Main.rand.Next(50, 80));
            }

            SoundEngine.PlaySound(SoundID.NPCHit13 with { Volume = 0.26f, Pitch = 0.3f, MaxInstances = 3 }, pos);
        }

        //==================== 图元绘制：血痰同款粘血线 + 液团头，整体缩小一号 ====================

        public float GetWidthFunc(float completionRatio)
            => MathHelper.Lerp(6.4f, 1f, completionRatio) * VisualFade; //0=团后颈最宽，尾端收成丝

        public Color GetColorFunc(Vector2 coord) => Color.White;

        void IPrimitiveDrawable.DrawPrimitives() {
            if (Main.dedServ || !Projectile.active || VisualFade <= 0.01f) {
                return;
            }
            DrawSlimeTrail();

            //液团头部画在条带之上
            SpriteBatch sb = Main.spriteBatch;
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
            DrawGlobHead(sb);
            sb.End();
        }

        /// <summary>粘水线条带：借灵液液柱 shader（四色全参数化），换血色板；尾段自带珠化断裂</summary>
        private void DrawSlimeTrail() {
            Effect fx = FishIchornAssets.FishIchornJet;
            if (fx == null || Projectile.oldPos == null || Projectile.oldPos.Length == 0) {
                return;
            }
            fx.Parameters["transformMatrix"]?.SetValue(VaultUtils.GetTransfromMatrix());
            fx.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            fx.Parameters["uSeed"]?.SetValue(Seed);
            fx.Parameters["uFade"]?.SetValue(VisualFade * 0.85f);
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            if (noise != null) {
                fx.Parameters["uNoiseTex"]?.SetValue(noise);
            }
            fx.Parameters["uColDark"]?.SetValue(KikasaMinisharkServant.BloodDark.ToVector3());
            fx.Parameters["uColDeep"]?.SetValue(KikasaMinisharkServant.BloodDeep.ToVector3());
            fx.Parameters["uColGold"]?.SetValue(KikasaMinisharkServant.BloodMain.ToVector3());
            fx.Parameters["uColBright"]?.SetValue(KikasaMinisharkServant.BloodBright.ToVector3());

            Vector2[] positions = new Vector2[Projectile.oldPos.Length];
            for (int i = 0; i < positions.Length; i++) {
                if (Projectile.oldPos[i] == Vector2.Zero) {
                    Projectile.oldPos[i] = Projectile.position;
                }
                positions[i] = Projectile.oldPos[i] + Projectile.Size * 0.5f;
            }
            trail ??= new Trail(positions, GetWidthFunc, GetColorFunc);
            trail.TrailPositions = positions;
            Main.graphics.GraphicsDevice.BlendState = BlendState.AlphaBlend;
            trail.DrawTrail(fx);
        }

        /// <summary>液团头部：暗血压边→血红主体→血沫亮芯，表面张力抖动 + 速度拉伸（血痰 ×0.75）</summary>
        private void DrawGlobHead(SpriteBatch sb) {
            Texture2D tex = CWRAsset.Extra_98?.Value;
            if (tex == null) {
                return;
            }
            float fade = VisualFade;
            Vector2 pos = Projectile.Center + Projectile.velocity * 0.3f - Main.screenPosition;
            Vector2 origin = tex.Size() * 0.5f;
            float rotation = Projectile.rotation;
            float stretch = MathHelper.Clamp(Projectile.velocity.Length() * 0.028f, 0.15f, 0.75f);

            //表面张力抖动：宽窄反相呼吸，滴在飞行里晃（extraUpdates 下相位减半保同视觉频率）
            float wob = MathF.Sin(Life * 0.32f + Seed * 6f) * 0.12f;
            Vector2 jiggle = new(1f + wob, 1f - wob * 0.8f);

            //暗血压边
            sb.Draw(tex, pos, null, KikasaMinisharkServant.BloodDark * (0.85f * fade), rotation, origin,
                new Vector2(0.40f, 0.43f + stretch * 0.62f) * jiggle, SpriteEffects.None, 0f);
            //血红主体
            sb.Draw(tex, pos, null, KikasaMinisharkServant.BloodMain * fade, rotation, origin,
                new Vector2(0.31f, 0.35f + stretch * 0.55f) * jiggle, SpriteEffects.None, 0f);
            //血沫亮芯：极小面积加色湿反光
            Color core = KikasaMinisharkServant.BloodBright with { A = 0 };
            sb.Draw(tex, pos, null, core * (0.6f * fade), rotation, origin,
                new Vector2(0.11f, 0.18f + stretch * 0.24f) * jiggle, SpriteEffects.None, 0f);
        }

        public override bool PreDraw(ref Color lightColor) => false;
    }
}
