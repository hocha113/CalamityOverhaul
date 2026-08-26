using CalamityOverhaul.Common;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using InnoVault.TileProcessors;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;

namespace CalamityOverhaul.Content.Industrials.ElectricPowers.ShieldGenerators
{
    /// <summary>
    /// 护盾膜受击涟漪登记与吸收演出:数据源是 <see cref="ShieldGeneratorPlayer"/> 的吸收事件,
    /// 纯客户端表现零网络。本地玩家走 ModifyHurt 真实吸收;远端玩家走 OnHurt 的本地模拟近似
    /// </summary>
    internal static class ShieldDomeFX
    {
        /// <summary>单条涟漪:挂在具体发生器上,寿命 40 帧</summary>
        internal struct Ripple
        {
            public int TpWhoAmI;
            public Vector2 ImpactWorld;
            public uint StartTick;
            public float Strength;
        }

        /// <summary>涟漪寿命(帧)</summary>
        internal const int RippleLife = 40;
        private const int MaxRipples = 24;

        //40帧自灭,无需跨世界清理钩子
        internal static readonly List<Ripple> Ripples = [];

        /// <summary>清理过期涟漪,每绘制帧调用一次</summary>
        internal static void Prune() {
            for (int i = Ripples.Count - 1; i >= 0; i--) {
                if (Main.GameUpdateCount - Ripples[i].StartTick > RippleLife) {
                    Ripples.RemoveAt(i);
                }
            }
        }

        /// <summary>
        /// 玩家在护盾庇护下吸收了一次伤害:向所有覆盖该玩家的运转中发生器登记膜涟漪,
        /// 并给玩家本体一记吸收闪光。absorb 为吸收量,只影响涟漪强度
        /// </summary>
        internal static void OnAbsorb(Player player, float absorb) {
            if (Main.dedServ) {
                return;
            }
            float strength = MathHelper.Clamp(0.55f + absorb / 45f, 0.55f, 1.3f);

            foreach (var tp in TileProcessorLoader.TP_InWorld) {
                if (tp is not ShieldGeneratorTP gen || !gen.Active) {
                    continue;
                }
                if (!gen.WorkingActive || gen.DomeVisualRadius < 60f) {
                    continue;
                }
                if (player.Center.DistanceSQ(gen.CenterInWorld) > gen.DomeVisualRadius * gen.DomeVisualRadius * 1.2f) {
                    continue;
                }

                //受击方位:发生器心指向玩家;玩家贴心时随机取向
                Vector2 dir = gen.CenterInWorld.To(player.Center);
                dir = dir.LengthSquared() < 900f ? Main.rand.NextVector2Unit() : dir.UnitVector();
                Vector2 impact = gen.CenterInWorld + dir * gen.DomeVisualRadius;

                Ripples.Add(new Ripple {
                    TpWhoAmI = gen.WhoAmI,
                    ImpactWorld = impact,
                    StartTick = Main.GameUpdateCount,
                    Strength = strength,
                });
                if (Ripples.Count > MaxRipples) {
                    Ripples.RemoveAt(0);
                }

                //膜冲击点碎光:少量玻璃质碎片外溅
                for (int i = 0; i < 4; i++) {
                    Vector2 vel = dir.RotatedBy(Main.rand.NextFloat(-0.7f, 0.7f)) * Main.rand.NextFloat(1.2f, 3f);
                    PRTLoader.NewParticle<PRT_DefCrystalShard>(impact, vel, ShieldGenerator.Tint,
                        Main.rand.NextFloat(0.5f, 0.85f))?.Configure(Main.rand.Next(18, 28), Main.rand.NextFloat(-0.2f, 0.2f), 0.03f);
                }
            }

            player.GetModPlayer<ShieldGeneratorPlayer>().AbsorbFlash = 1f;
        }

        /// <summary>断电/关机塌缩:膜碎成一圈玻璃质碎片,读作力场破碎</summary>
        internal static void SpawnCollapseBurst(Vector2 center, float radius) {
            int count = (int)MathHelper.Clamp(radius * 0.035f, 12, 30);
            for (int i = 0; i < count; i++) {
                float ang = MathHelper.TwoPi * i / count + Main.rand.NextFloat(-0.1f, 0.1f);
                Vector2 dir = ang.ToRotationVector2();
                Vector2 pos = center + dir * radius * Main.rand.NextFloat(0.96f, 1.02f);
                //碎片微向内塌又受重力,读作膜失去张力
                Vector2 vel = -dir * Main.rand.NextFloat(0.5f, 1.6f) + Main.rand.NextVector2Circular(0.6f, 0.6f);
                PRTLoader.NewParticle<PRT_DefCrystalShard>(pos, vel, ShieldGenerator.Tint,
                    Main.rand.NextFloat(0.55f, 1f))?.Configure(Main.rand.Next(24, 40), Main.rand.NextFloat(-0.25f, 0.25f), 0.06f);
            }
        }

        /// <summary>把指定发生器的至多4条最新涟漪装进 shader 槽位;空槽 w=0 在 shader 内自灭</summary>
        internal static void FillImpactSlots(ShieldGeneratorTP gen, float quadHalf, Vector4[] slots) {
            for (int i = 0; i < slots.Length; i++) {
                slots[i] = new Vector4(0f, 0f, 1f, 0f);
            }
            int slot = 0;
            //倒序取最新
            for (int i = Ripples.Count - 1; i >= 0 && slot < slots.Length; i--) {
                Ripple r = Ripples[i];
                if (r.TpWhoAmI != gen.WhoAmI) {
                    continue;
                }
                float age = (Main.GameUpdateCount - r.StartTick) / (float)RippleLife;
                Vector2 norm = (r.ImpactWorld - gen.CenterInWorld) / quadHalf;
                slots[slot++] = new Vector4(norm.X, norm.Y, MathHelper.Clamp(age, 0f, 1f), r.Strength);
            }
        }
    }

    /// <summary>
    /// 护盾能量膜绘制:<see cref="EffectLoader.ShieldDome"/> 画在归一化圆盘 quad 上,
    /// 管线镜像 TeslaGuardRingDraw(PreDrawEverything 自开 Immediate 批,物块之上实体之下)。
    /// 尾段用普通批补画受庇护玩家的贴身护盾光晕(受益可见性)
    /// </summary>
    internal class ShieldDomeDraw : GlobalTileProcessor
    {
        private static readonly Vector4[] impactSlots = new Vector4[4];

        public override bool PreDrawEverything(SpriteBatch spriteBatch) {
            if (Main.dedServ) {
                return true;
            }
            ShieldDomeFX.Prune();

            Effect shader = EffectLoader.ShieldDome?.Value;
            Texture2D canvas = VaultAsset.placeholder2?.Value;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            if (shader != null && canvas != null && noise != null) {
                DrawDomes(spriteBatch, shader, canvas, noise);
            }
            //着色器缺失时发生器侧已回退粒子环,这里只补玩家光晕
            DrawPlayerHalos(spriteBatch);
            return true;
        }

        private static void DrawDomes(SpriteBatch spriteBatch, Effect shader, Texture2D canvas, Texture2D noise) {
            bool begun = false;
            foreach (var tp in TileProcessorLoader.TP_InWorld) {
                if (tp is not ShieldGeneratorTP gen || !gen.Active) {
                    continue;
                }
                if (gen.DomeVisualIntensity <= 0.02f || gen.DomeVisualRadius < 8f) {
                    continue;
                }
                if (!VaultUtils.IsPointOnScreen(gen.PosInWorld - Main.screenPosition, gen.DrawExtendMode)) {
                    continue;
                }

                if (!begun) {
                    begun = true;
                    spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend,
                        SamplerState.LinearWrap, DepthStencilState.None, RasterizerState.CullNone,
                        null, Main.GameViewMatrix.TransformationMatrix);
                    GraphicsDevice gd = Main.instance.GraphicsDevice;
                    gd.Textures[1] = noise;
                    gd.SamplerStates[1] = SamplerState.LinearWrap;
                }

                float radius = gen.DomeVisualRadius;
                //quad 外留余量装膜辉;小半径时保底 130px
                float quadHalf = MathF.Max(radius * 1.30f, radius + 130f);
                float phase = (gen.Position.X * 11 + gen.Position.Y * 17) * 0.149f;
                ShieldDomeFX.FillImpactSlots(gen, quadHalf, impactSlots);

                //共享 uniform 全参数重设,防跨实例残留
                shader.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly + phase);
                shader.Parameters["ringProgress"]?.SetValue(radius / quadHalf);
                shader.Parameters["uQuadHalf"]?.SetValue(quadHalf);
                shader.Parameters["intensity"]?.SetValue(gen.DomeVisualIntensity);
                shader.Parameters["expandGlow"]?.SetValue(gen.DomeExpandGlow);
                shader.Parameters["uStress"]?.SetValue(gen.DomeStress);
                shader.Parameters["seed"]?.SetValue(phase - MathF.Floor(phase));
                shader.Parameters["uImpact0"]?.SetValue(impactSlots[0]);
                shader.Parameters["uImpact1"]?.SetValue(impactSlots[1]);
                shader.Parameters["uImpact2"]?.SetValue(impactSlots[2]);
                shader.Parameters["uImpact3"]?.SetValue(impactSlots[3]);
                shader.CurrentTechnique.Passes[0].Apply();

                float diameter = quadHalf * 2f;
                spriteBatch.Draw(canvas, gen.CenterInWorld - Main.screenPosition, null, Color.White,
                    0f, canvas.Size() * 0.5f, new Vector2(diameter / canvas.Width, diameter / canvas.Height),
                    SpriteEffects.None, 0f);
            }

            if (begun) {
                spriteBatch.End();
            }
        }

        /// <summary>受庇护玩家贴身微弱护盾光晕:池满亮、池空暗,吸收瞬间闪亮后消退</summary>
        private static void DrawPlayerHalos(SpriteBatch spriteBatch) {
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (glow == null) {
                return;
            }

            bool begun = false;
            foreach (Player player in Main.ActivePlayers) {
                if (player.dead || player.ghost) {
                    continue;
                }
                var sp = player.GetModPlayer<ShieldGeneratorPlayer>();
                float fill = sp.ShieldCharge / ShieldGeneratorPlayer.ShieldMax;
                float flash = sp.AbsorbFlash;
                if ((!sp.ShieldAuraActive || fill <= 0.02f) && flash <= 0.02f) {
                    continue;
                }
                if (!VaultUtils.IsPointOnScreen(player.Center - Main.screenPosition, 120)) {
                    continue;
                }

                if (!begun) {
                    begun = true;
                    spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend,
                        SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone,
                        null, Main.GameViewMatrix.TransformationMatrix);
                }

                //呼吸相位逐玩家错开;黑底贴图 A=0 加色画法
                float breath = 0.5f + 0.5f * MathF.Sin(Main.GlobalTimeWrappedHourly * 2.6f + player.whoAmI * 1.7f);
                float alpha = (0.10f + 0.07f * breath) * fill + flash * 0.5f;
                Color halo = ShieldGenerator.Tint;
                halo.A = 0;
                Vector2 pos = player.MountedCenter - Main.screenPosition;
                float scale = 1.05f + breath * 0.06f + flash * 0.22f;
                spriteBatch.Draw(glow, pos, null, halo * alpha, 0f, glow.Size() * 0.5f,
                    scale * 1.35f, SpriteEffects.None, 0f);
                spriteBatch.Draw(glow, pos, null, halo * (alpha * 0.8f), 0f, glow.Size() * 0.5f,
                    scale * 0.7f, SpriteEffects.None, 0f);
            }

            if (begun) {
                spriteBatch.End();
            }
        }
    }
}
