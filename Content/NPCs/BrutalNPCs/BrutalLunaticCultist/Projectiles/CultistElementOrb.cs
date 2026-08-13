using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Rendering;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Projectiles
{
    /// <summary>
    /// 元素轮盘球：纯演出实体（无伤害），随本体状态编舞；
    /// ai[0]=元素 ai[1]=轮位索引 ai[2]=本体whoAmI
    /// </summary>
    internal class CultistElementOrb : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        internal const float WheelMaxRadius = 260f;
        internal const int WheelGrowTime = 40;

        private CultistElement Element => (CultistElement)(int)Projectile.ai[0];
        private int OrbIndex => (int)Projectile.ai[1];

        private float age;
        private float charge;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 54;
            Projectile.hostile = false;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 3600;
        }

        #region 轮盘几何（状态与球共用的确定性公式）

        /// <summary>轮盘半径（弹性展开）</summary>
        internal static float WheelRadius(float orbAge) {
            float t = MathHelper.Clamp(orbAge / WheelGrowTime, 0f, 1f);
            //elasticOut 简化：过冲1.12再回稳
            float e = 1f - (float)(Math.Pow(2, -8 * t) * Math.Cos(t * 9.4));
            return WheelMaxRadius * MathHelper.Clamp(e, 0f, 1.12f);
        }

        /// <summary>轮盘角：角速度 0.012→0.036 rad/f 线性爬升300帧后恒速</summary>
        internal static float WheelAngle(int orbIndex, float orbAge) {
            const float w0 = 0.012f;
            const float w1 = 0.036f;
            const float rampTime = 300f;
            float angle;
            if (orbAge <= rampTime) {
                //ω(t)=w0+(w1-w0)t/T 的积分
                angle = w0 * orbAge + (w1 - w0) * orbAge * orbAge / (2f * rampTime);
            }
            else {
                float rampArea = w0 * rampTime + (w1 - w0) * rampTime * 0.5f;
                angle = rampArea + w1 * (orbAge - rampTime);
            }
            return orbIndex * MathHelper.TwoPi / 3f + angle - MathHelper.PiOver2;
        }

        internal static Vector2 WheelPos(Vector2 center, int orbIndex, float orbAge) {
            return center + WheelAngle(orbIndex, orbAge).ToRotationVector2() * WheelRadius(orbAge);
        }

        #endregion

        private NPC Boss {
            get {
                int idx = (int)Projectile.ai[2];
                if (idx < 0 || idx >= Main.maxNPCs) {
                    return null;
                }
                NPC boss = Main.npc[idx];
                return boss.active && boss.type == NPCID.CultistBoss ? boss : null;
            }
        }

        public override void AI() {
            NPC boss = Boss;
            if (boss == null) {
                Projectile.Kill();
                return;
            }

            age++;
            charge = MathHelper.Clamp(charge + 0.04f, 0f, 1f);

            CultistStateIndex bossState = (CultistStateIndex)(int)boss.ai[2];
            switch (bossState) {
                case CultistStateIndex.ElementWheel:
                case CultistStateIndex.Cataclysm:
                    //轮盘编队（灾变期由状态另行收拢，这里保持轨道）
                    Projectile.Center = WheelPos(boss.Center, OrbIndex, age);
                    Projectile.velocity = Vector2.Zero;
                    break;
                case CultistStateIndex.Death: {
                    //失控震荡轨道：振幅渐增
                    float wobble = 1f + age * 0.004f;
                    float r = 130f + (float)Math.Sin(age * 0.11f + OrbIndex * 2.1f) * 46f * wobble;
                    float a = OrbIndex * MathHelper.TwoPi / 3f + age * (0.05f + age * 0.0001f);
                    Projectile.Center = boss.Center + a.ToRotationVector2() * r;
                    Projectile.velocity = Vector2.Zero;
                    break;
                }
                default:
                    //本体离开编舞状态即收场
                    Projectile.Kill();
                    return;
            }

            //元素性微粒
            if (!VaultUtils.isServer && Main.rand.NextBool(5)) {
                CultistRenderHelper.SpawnElementMote(Projectile.Center, Main.rand.NextVector2Circular(1.2f, 1.2f),
                    Element, Main.rand.NextFloat(0.5f, 0.9f), Main.rand.Next(12, 22));
            }
            Lighting.AddLight(Projectile.Center, CultistPalette.Main(Element).ToVector3() * 0.8f * charge);
        }

        public override void OnKill(int timeLeft) {
            if (!VaultUtils.isServer) {
                CultistRenderHelper.ElementImpact(Projectile.Center, Element, 1.2f);
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            CultistRenderHelper.DrawOrb(Main.spriteBatch, Projectile.Center, 40f, Element,
                charge, 0f, Projectile.identity * 0.77f);
            return false;
        }
    }
}
