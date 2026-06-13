using CalamityOverhaul.Common;
using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Cyberspaces;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Grip
{
    /// <summary>谐振握把：命中溢流灵雾，拾取回蓝叠谐鸣层（最多 5 层 +3% 攻速）</summary>
    internal sealed class HarmonyGripModule : SHPCModuleItem
    {
        public override SHPCSlotCategory SlotCategory => SHPCSlotCategory.Grip;
        //节能薄荷绿
        public override Color TintColor => new(120, 255, 180);

        private const int MaxResonance = 5;
        /// <summary>当前谐鸣层数，由灵雾拾取叠加</summary>
        internal int ResonanceStacks;
        /// <summary>层数保持计时，归零后开始衰减</summary>
        internal int ResonanceTimer;

        public override void Apply(ref ShootContext ctx) {
            ctx.ManaCostMul += -0.2f;
            if (ResonanceStacks > 0) {
                ctx.AttackSpeedMul += ResonanceStacks * 0.03f;
            }
        }

        /// <summary>灵雾拾取入口：返还法力的同时叠层刷新计时</summary>
        internal void AddResonance() {
            ResonanceStacks = Math.Min(ResonanceStacks + 1, MaxResonance);
            ResonanceTimer = 300;
        }

        public override void OnBeamHitNPC(CyberTraceBeamProj beam, NPC target, NPC.HitInfo hit, int damageDone) {
            if (beam.Projectile.owner != Main.myPlayer || beam.IsDerived) return;
            if (!Main.rand.NextBool(2, 5)) return; //40% 概率溢流
            SpawnWisp(beam.Projectile, target.Center);
        }

        public override void OnLaserHitNPC(CyberPrismLaserProj laser, NPC target, NPC.HitInfo hit, int damageDone) {
            if (laser.Projectile.owner != Main.myPlayer) return;
            if (!Main.rand.NextBool(7)) return; //激光命中频繁，节流
            SpawnWisp(laser.Projectile, target.Center);
        }

        public override void OnOrbDetonation(CyberChargeOrbProj orb) {
            if (orb.Projectile.owner != Main.myPlayer) return;
            for (int i = 0; i < 3; i++) {
                SpawnWisp(orb.Projectile, orb.Projectile.Center + Main.rand.NextVector2Circular(40f, 40f));
            }
        }

        private static void SpawnWisp(Projectile source, Vector2 pos) {
            Projectile.NewProjectile(source.GetSource_FromThis(),
                pos, Main.rand.NextVector2CircularEdge(3f, 3f) - Vector2.UnitY * 1.5f,
                ModContent.ProjectileType<SHPCHarmonyWispProj>(),
                0, 0f, source.owner);
        }

        public override void OnPlayerUpdate(Player player) {
            if (ResonanceStacks <= 0) return;
            if (ResonanceTimer > 0) {
                ResonanceTimer--;
                //满层时指尖萦绕薄荷电雾，提示玩家处于全速谐鸣
                if (ResonanceStacks >= MaxResonance && Main.netMode != NetmodeID.Server && Main.rand.NextBool(6)) {
                    Vector2 pos = player.Center + Main.rand.NextVector2Circular(20f, 26f);
                    PRTLoader.NewParticle<PRT_CyberSquare>(pos, -Vector2.UnitY * Main.rand.NextFloat(0.5f, 1.4f),
                        new Color(140, 255, 190), Main.rand.NextFloat(0.4f, 0.8f)).Configure(new Color(40, 170, 110), Main.rand.Next(10, 20));
                }
                return;
            }
            ResonanceStacks--;
            ResonanceTimer = 60;
        }
    }

    /// <summary>谐振灵雾拾取弹幕：漂移后追随玩家，触 pickup 回蓝叠层</summary>
    internal sealed class SHPCHarmonyWispProj : ModProjectile, IAdditiveDrawable
    {
        public override string Texture => CWRConstant.Placeholder;

        private const int Lifetime = 420;
        private const int DriftPhase = 30;
        private static readonly Color WispCore = new(190, 255, 220);
        private static readonly Color WispGlow = new(90, 230, 160);
        private static readonly Color WispAura = new(25, 120, 80);

        private float fadeAlpha;

        public override void SetDefaults() {
            Projectile.width = 16;
            Projectile.height = 16;
            Projectile.friendly = false;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = Lifetime;
        }

        public override void AI() {
            Player owner = Main.player[Projectile.owner];
            if (owner == null || !owner.active || owner.dead) {
                Projectile.Kill();
                return;
            }

            int age = Lifetime - Projectile.timeLeft;
            fadeAlpha = MathHelper.Clamp(age / 12f, 0f, 1f)
                * MathHelper.Clamp(Projectile.timeLeft / 30f, 0f, 1f);

            if (age < DriftPhase) {
                //漂散段：惯性外抛逐渐悬停
                Projectile.velocity *= 0.93f;
            }
            else {
                //追随段：朝玩家缓缓加速，越近越快，带一点波浪摆动
                Vector2 toOwner = owner.Center - Projectile.Center;
                float dist = toOwner.Length();
                float chase = MathHelper.Clamp(MathHelper.Lerp(0.18f, 0.55f, 1f - dist / 600f), 0.18f, 0.55f);
                Vector2 desired = toOwner.SafeNormalize(Vector2.Zero) * MathHelper.Clamp(3f + (600f - dist) * 0.02f, 3f, 13f);
                desired += toOwner.SafeNormalize(Vector2.Zero).RotatedBy(MathHelper.PiOver2)
                    * MathF.Sin(age * 0.12f + Projectile.whoAmI) * 1.2f;
                Projectile.velocity = Vector2.Lerp(Projectile.velocity, desired, chase * 0.12f);

                //拾取判定：仅弹幕拥有者本地结算法力与层数
                if (dist < 42f) {
                    Collect(owner);
                    return;
                }
            }

            Lighting.AddLight(Projectile.Center, WispGlow.ToVector3() * 0.35f * fadeAlpha);
            if (Main.netMode != NetmodeID.Server && Main.rand.NextBool(5)) {
                PRTLoader.NewParticle<PRT_CyberSquare>(
                    Projectile.Center + Main.rand.NextVector2Circular(6f, 6f),
                    -Projectile.velocity * 0.15f - Vector2.UnitY * 0.4f,
                    WispCore, Main.rand.NextFloat(0.3f, 0.7f)).Configure(WispAura, Main.rand.Next(12, 22));
            }
        }

        private void Collect(Player owner) {
            if (Projectile.owner == Main.myPlayer) {
                int mana = Main.rand.Next(5, 9);
                owner.statMana = Math.Min(owner.statMana + mana, owner.statManaMax2);
                owner.ManaEffect(mana);
                SHPCModificationSystem.ForEachModule(owner, mod => {
                    if (mod is HarmonyGripModule grip) grip.AddResonance();
                });
            }
            if (Main.netMode != NetmodeID.Server) {
                SoundEngine.PlaySound(SoundID.Item112 with { Volume = 0.35f, Pitch = 0.65f }, Projectile.Center);
                for (int i = 0; i < 7; i++) {
                    Vector2 vel = Main.rand.NextVector2CircularEdge(2.6f, 2.6f);
                    PRTLoader.NewParticle<PRT_CyberSquare>(Projectile.Center, vel, WispCore, Main.rand.NextFloat(0.45f, 0.9f)).Configure(WispGlow, Main.rand.Next(12, 22));
                }
            }
            Projectile.Kill();
        }

        public override bool PreDraw(ref Color lightColor) => false;

        void IAdditiveDrawable.DrawAdditiveAfterNon(SpriteBatch spriteBatch) {
            if (fadeAlpha < 0.01f) return;
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (glow == null) return;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            Vector2 origin = glow.Size() * 0.5f;
            float pulse = 0.85f + 0.15f * MathF.Sin((float)Main.timeForVisualEffects * 0.18f + Projectile.whoAmI * 1.7f);
            //三层灵雾光晕：薄荷核心 → 翠绿辉光 → 暗绿外晕
            spriteBatch.Draw(glow, drawPos, null, WispAura * fadeAlpha * 0.45f * pulse, 0f, origin, 1.15f, SpriteEffects.None, 0f);
            spriteBatch.Draw(glow, drawPos, null, WispGlow * fadeAlpha * 0.7f * pulse, 0f, origin, 0.62f, SpriteEffects.None, 0f);
            spriteBatch.Draw(glow, drawPos, null, WispCore * fadeAlpha * pulse, 0f, origin, 0.3f, SpriteEffects.None, 0f);
            //中心十字微光，强调"可拾取物"的存在感
            Texture2D star = CWRAsset.StarTexture_White?.Value;
            if (star != null) {
                float starScale = 0.055f * pulse;
                spriteBatch.Draw(star, drawPos, null, WispCore * fadeAlpha * 0.8f,
                    (float)Main.timeForVisualEffects * 0.02f, star.Size() * 0.5f, starScale, SpriteEffects.None, 0f);
            }
        }
    }
}
