using System;
using CalamityOverhaul.Content.GameModes.GodSmith.Core;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Accessories.Offense
{
    /// <summary>
    /// 【狙击镜链】步枪镜/狙击镜/侦察镜多认领共用「稳像狙击」：站稳蓄稳像，
    /// 蓄满后下一次远程命中放出贯穿曳光；伤害比例与穿透数随链递进，高速移动流失稳像。<br/>
    /// 曳光为 DamageClass.Default（远程过滤防自喂）；稳像在同文件私有 <see cref="ScopeSteadyPlayer"/>，
    /// 触发用链共用冷却键（TargetItemIDs[0]）防两镜同时结算
    /// </summary>
    internal class GodSmithRifleScope : GodSmithAccEffect
    {
        /// <summary>蓄满稳像所需帧数</summary>
        internal const int StabilityMax = 75;

        public override int[] TargetItemIDs => [ItemID.RifleScope, ItemID.SniperScope, ItemID.ReconScope];

        protected override string EffectDescFallback =>
            "Steady Aim: standing still for 1.25s steadies the scope; your next ranged hit looses a piercing tracer\nThe tracer deals 45% / 60% / 75% of that hit (Rifle / Sniper / Recon) and pierces 2 / 3 / 4 foes\nMoving fast drains your aim";

        /// <summary>档位：1 步枪镜 / 2 狙击镜 / 3 侦察镜</summary>
        internal static int TierOf(int itemType)
            => itemType == ItemID.RifleScope ? 1 : itemType == ItemID.SniperScope ? 2 : 3;

        public override void UpdateAccessory(Item item, Player player, bool hideVisual, GodSmithPlayer state) {
            //登记本帧最高档位；稳像积累在私有 ModPlayer 里每帧只走一次
            ScopeSteadyPlayer steady = player.GetModPlayer<ScopeSteadyPlayer>();
            steady.BestTier = Math.Max(steady.BestTier, TierOf(item.type));
        }

        public override void OnHitNPC(Item item, Player player, GodSmithPlayer state, NPC target,
            in NPC.HitInfo hit, int damageDone, bool fromProjectile) {
            if (!hit.DamageType.CountsAsClass(DamageClass.Ranged)) {
                return;
            }
            ScopeSteadyPlayer steady = player.GetModPlayer<ScopeSteadyPlayer>();
            //链共用冷却键：两镜同佩也只结算一发
            if (steady.Stability < StabilityMax || !state.TryUseCooldown(TargetItemIDs[0], 10)) {
                return;
            }
            steady.Stability = 0;
            int tier = Math.Max(steady.BestTier, 1);
            SoundEngine.PlaySound(SoundID.Item40 with { Volume = 0.45f, Pitch = 0.5f }, player.Center);
            //镜面反光一闪（命中钩子只在攻击方端跑）
            PRTLoader.NewParticle<PRT_Light>(player.Center + new Vector2(player.direction * 10f, -4f),
                Vector2.Zero, new Color(220, 235, 245), 0.12f)?.Configure(10, 0.9f);
            if (player.whoAmI == Main.myPlayer) {
                float ratio = tier == 1 ? 0.45f : tier == 2 ? 0.60f : 0.75f;
                int pierce = tier + 1;
                int tracerDamage = Math.Clamp((int)(damageDone * ratio), 10, 320);
                Vector2 vel = (target.Center - player.Center).SafeNormalize(Vector2.UnitX) * 20f;
                Projectile.NewProjectile(player.GetSource_Accessory(item), player.Center, vel,
                    ModContent.ProjectileType<GodSmithRifleScopeTracerProj>(), tracerDamage, 3f, player.whoAmI,
                    pierce);
            }
        }
    }

    /// <summary>
    /// 精准曳光：一发被稳像压出的贯穿弹道，笔直、快、不回头；
    /// 银白双层曳光自绘 + 穿透衰减，命中弹出弹道火花
    /// </summary>
    internal class GodSmithRifleScopeTracerProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private ref float PierceParam => ref Projectile.ai[0];

        /// <summary>确定性绘制相位，绘制路径不掷 Main.rand</summary>
        private float Seed => Projectile.identity * 0.4931f % 2.19f;

        public override void SetDefaults() {
            Projectile.width = 8;
            Projectile.height = 8;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Default;
            Projectile.penetrate = 2;
            Projectile.timeLeft = 50;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
            Projectile.extraUpdates = 3;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
        }

        public override void AI() {
            //穿透数随生成参数定档（ai 随生成包过线，各端一致）
            if (Projectile.localAI[0] == 0f) {
                Projectile.localAI[0] = 1f;
                Projectile.penetrate = (int)MathHelper.Clamp(PierceParam <= 0f ? 2f : PierceParam, 1f, 5f);
            }
            Projectile.rotation = Projectile.velocity.ToRotation();
            if (!Main.dedServ && Projectile.timeLeft % 6 == 0) {
                PRTLoader.NewParticle<PRT_Spark>(Projectile.Center, -Projectile.velocity * 0.03f,
                    new Color(225, 235, 245), Main.rand.NextFloat(0.14f, 0.24f))?.Configure(false, 8);
            }
            Lighting.AddLight(Projectile.Center, new Vector3(0.35f, 0.37f, 0.4f));
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (Main.dedServ) {
                return;
            }
            //穿体火花顺弹道向前迸
            for (int i = 0; i < 4; i++) {
                PRTLoader.NewParticle<PRT_Spark>(target.Center,
                    Projectile.velocity.SafeNormalize(Vector2.UnitX).RotatedByRandom(0.5f)
                        * Main.rand.NextFloat(2f, 5f),
                    Main.rand.NextBool() ? new Color(225, 235, 245) : new Color(255, 230, 170),
                    Main.rand.NextFloat(0.24f, 0.4f))?.Configure(true, Main.rand.Next(10, 16));
            }
        }

        public override void OnKill(int timeLeft) {
            if (Main.dedServ) {
                return;
            }
            SoundEngine.PlaySound(SoundID.Item10 with { Volume = 0.3f, Pitch = 0.3f }, Projectile.Center);
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = CWRAsset.LightShot?.Value;
            if (tex == null) {
                return false;
            }
            Vector2 pos = Projectile.Center - Main.screenPosition;
            Vector2 origin = tex.Size() * 0.5f;
            //高速弹道拉得极长，宽度极窄
            float stretch = MathHelper.Clamp(Projectile.velocity.Length() * 0.09f, 0.6f, 1.6f);
            float wob = 1f + MathF.Sin(Projectile.timeLeft * 0.9f + Seed * 6f) * 0.06f;
            Main.EntitySpriteDraw(tex, pos, null, new Color(200, 215, 230) with { A = 0 } * 0.8f,
                Projectile.rotation, origin, new Vector2(stretch, 0.05f * wob), SpriteEffects.None, 0);
            Main.EntitySpriteDraw(tex, pos, null, new Color(255, 255, 250) with { A = 0 } * 0.75f,
                Projectile.rotation, origin, new Vector2(stretch * 0.6f, 0.025f * wob), SpriteEffects.None, 0);
            return false;
        }
    }

    /// <summary>狙击镜链私有状态载体：稳像积累与本帧最高档位。攻击方端本地量，无需同步</summary>
    internal class ScopeSteadyPlayer : ModPlayer
    {
        /// <summary>稳像积累（帧）</summary>
        internal int Stability;

        /// <summary>本帧佩戴的最高镜档（UpdateAccessory 登记，ResetEffects 清零）</summary>
        internal int BestTier;

        private bool wasFull;

        public override void ResetEffects() => BestTier = 0;

        public override void PostUpdateMiscEffects() {
            if (BestTier <= 0) {
                Stability = 0;
                wasFull = false;
                return;
            }
            float speed = Player.velocity.Length();
            if (speed < 0.5f) {
                Stability = Math.Min(Stability + 1, GodSmithRifleScope.StabilityMax);
            }
            else if (speed > 3f) {
                Stability = Math.Max(0, Stability - 3);
            }
            bool full = Stability >= GodSmithRifleScope.StabilityMax;
            //蓄满一瞬：镜心咔哒定格（个人读数，仅佩戴者本端）
            if (full && !wasFull && Player.whoAmI == Main.myPlayer && !Main.dedServ) {
                SoundEngine.PlaySound(SoundID.Unlock with { Volume = 0.35f, Pitch = 0.7f }, Player.Center);
                PRTLoader.NewParticle<PRT_Light>(Player.Center + new Vector2(Player.direction * 10f, -4f),
                    Vector2.Zero, new Color(180, 230, 200), 0.1f)?.Configure(12, 0.9f);
            }
            wasFull = full;
        }

        public override void UpdateDead() {
            Stability = 0;
            wasFull = false;
        }
    }
}
