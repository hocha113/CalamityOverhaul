using CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Shortswords;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Spears
{
    /// <summary>
    /// 蘑菇矛重铸：菌雷。<br/>
    /// 材质：发光菌木矛。签名行为：①两拍连刺——轻拍快刺、重拍深刺；重拍命中在目标处
    /// 种下一枚发光菌雷 ②菌雷驻场约 0.8 秒、蓝荧光呼吸加速预警，随后孢爆——
    /// 小范围 60% 伤害，并给持矛者回 2 点生命微疗 ③命中反馈是软木闷响与蓝荧光孢尘
    /// </summary>
    internal class GsMushroomSpear : GsSpearScheme
    {
        public override int TargetItemID => ItemID.MushroomSpear;

        protected override string GsDescFallback =>
            "Reforged: the heavy second beat plants a glowing spore mine in the wound;" +
            "\nit bursts moments later, harming foes around and feeding 2 life back to the wielder";

        protected override int HeldProjType => ModContent.ProjectileType<GsMushroomSpearHeld>();

        protected override int ComboBeats => 2;

        public override void GsModifyWeaponDamage(Item item, Player player, ref StatModifier damage)
            => damage *= 1.06f;//菌雷延迟爆 + 微疗吃机制预算，综合 DPS 落在原版 106%~118%
    }

    /// <summary>
    /// 蘑菇矛手持突刺。ai[0]=拍号 0 轻快刺 / 1 重深刺；
    /// 重拍首个命中在目标处种菌雷（owner 端生成，驻场后爆 60% 伤害）
    /// </summary>
    internal class GsMushroomSpearHeld : GsThrustHeldBase
    {
        protected override int TargetItemType => ItemID.MushroomSpear;

        //发光菌木色板
        internal static readonly Color ShroomBlue = new(96, 176, 255);    //蓝荧光
        internal static readonly Color ShroomCyan = new(150, 232, 255);   //亮孢青
        internal static readonly Color ShroomDeep = new(36, 60, 130);     //深菌影

        private bool IsHeavy => ComboStage == 1;

        protected override float WindupFrames => IsHeavy ? 5f : 4f;
        protected override float ThrustFrames => IsHeavy ? 5f : 4f;
        protected override float DwellFrames => IsHeavy ? 4f : 3f;
        protected override float RecoverFrames => IsHeavy ? 9f : 8f;
        protected override float RestHoldout => 10f;
        protected override float PullbackDist => IsHeavy ? 16f : 13f;
        protected override float StabReach => IsHeavy ? 68f : 56f;
        protected override float BladeLength => 90f;
        protected override float CollisionWidth => 29f;
        protected override float TipGreedRadius => 26f;
        protected override bool TwoHanded => true;
        protected override float LeanAmp => IsHeavy ? 0.045f : 0.034f;
        protected override int HitboxSize => 50;
        protected override int HitstopFrames => IsHeavy ? 3 : 2;
        protected override float ThrustPitch => IsHeavy ? -0.30f : -0.12f;

        protected override Color EdgeColor => ShroomCyan;
        protected override Color CoreColor => ShroomBlue;

        protected override void OnInit() {
            if (IsHeavy) {
                Projectile.damage = (int)(Projectile.damage * 1.12f);
            }
        }

        protected override void OnHitTarget(NPC target, NPC.HitInfo hit, int damageDone, bool firstOnTarget) {
            //重拍首个命中种菌雷（owner 端生成，随生成包过线）
            if (!IsHeavy || !firstOnTarget || Projectile.numHits > 1 || !Projectile.IsOwnedByLocalPlayer()) {
                return;
            }
            Projectile.NewProjectile(Projectile.GetSource_FromAI(), target.Center, Vector2.Zero,
                ModContent.ProjectileType<GsMushroomSpearMineProj>(),
                (int)(BaseDamage * 0.6f), Projectile.knockBack * 0.3f, Owner.whoAmI);
        }

        /// <summary>命中反馈：软木闷响 + 蓝荧光孢尘（低速漂浮，不是金属火花）</summary>
        protected override void SpawnHitEffects(NPC target, NPC.HitInfo hit) {
            Vector2 pos = Vector2.Lerp(TipPos, target.Center, 0.5f);
            SoundEngine.PlaySound(SoundID.NPCHit1 with { Volume = 0.45f, Pitch = -0.5f, MaxInstances = 3 }, target.Center);
            PRTLoader.NewParticle<PRT_Light>(pos, Vector2.Zero, ShroomBlue, IsHeavy ? 0.24f : 0.17f)
                ?.Configure(11, 0.65f, 1.2f);
            int motes = IsHeavy ? 8 : 5;
            for (int i = 0; i < motes; i++) {
                Vector2 vel = stabUnit.RotatedByRandom(0.85) * Main.rand.NextFloat(1.2f, 3.5f) - Vector2.UnitY * 0.6f;
                Color c = Main.rand.NextBool(3) ? ShroomCyan : ShroomBlue;
                PRTLoader.NewParticle<PRT_Spark>(pos, vel, c, Main.rand.NextFloat(0.28f, 0.5f))
                    ?.Configure(false, Main.rand.Next(14, 22));
            }
        }

        /// <summary>重拍蓄势期矛头蓝荧升档</summary>
        protected override float ExtraGlowStrength() => IsHeavy && CurrentPhase == PhaseWindup ? 0.22f : 0f;
    }

    /// <summary>
    /// 发光菌雷：重拍种在目标处，驻场约 0.8 秒（呼吸荧光逐渐加速预警）后孢爆——
    /// 小范围伤害窗 5 帧，爆点给持矛者回 2 点生命。<br/>
    /// 自绘三层呼吸菌冠 + 环脉冲爆闪；驻场期无伤害，只有爆窗结算
    /// </summary>
    internal class GsMushroomSpearMineProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;
        public override LocalizedText DisplayName => Language.GetText("ItemName.MushroomSpear");

        /// <summary>驻场帧数（真实帧）；到点转入爆窗</summary>
        private const int ArmFrames = 48;
        /// <summary>爆窗帧数：Resize 后的伤害判定窗</summary>
        private const int BurstFrames = 5;

        private ref float Life => ref Projectile.localAI[0];
        private float Seed => Projectile.whoAmI * 0.733f % 3.1f;
        private bool Bursting => Life > ArmFrames;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 30;
            Projectile.friendly = true;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1;
            Projectile.timeLeft = ArmFrames + BurstFrames;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
        }

        /// <summary>驻场期不结算，只有爆窗有伤害</summary>
        public override bool? CanDamage() => Bursting ? null : false;

        public override void AI() {
            Life++;
            Projectile.velocity = Vector2.Zero;

            if (Life == ArmFrames + 1) {
                Detonate();
            }

            float glow = Bursting ? 0.6f : 0.3f;
            Lighting.AddLight(Projectile.Center, GsMushroomSpearHeld.ShroomBlue.ToVector3() * glow);

            if (VaultUtils.isServer || Bursting) {
                return;
            }
            //驻场期缓慢上浮的孢尘
            if (Life % 6f == 0f) {
                PRTLoader.NewParticle<PRT_Light>(Projectile.Center + Main.rand.NextVector2Circular(10f, 10f),
                    -Vector2.UnitY * Main.rand.NextFloat(0.4f, 1f),
                    Main.rand.NextBool() ? GsMushroomSpearHeld.ShroomCyan : GsMushroomSpearHeld.ShroomBlue,
                    Main.rand.NextFloat(0.22f, 0.4f))?.Configure(Main.rand.Next(10, 16), 0.5f, 1.2f);
            }
        }

        /// <summary>孢爆：伤害窗展开 + 微疗 + 爆闪演出</summary>
        private void Detonate() {
            //爆窗判定范围（一次性展开，配合 CanDamage 的爆窗开关）
            Projectile.Resize(120, 120);

            //微疗：菌雷把一口养分还给持矛者（owner 端本地结算，Heal 自带同步）
            if (Projectile.IsOwnedByLocalPlayer()) {
                Main.player[Projectile.owner].Heal(2);
            }

            if (VaultUtils.isServer) {
                return;
            }
            SoundEngine.PlaySound(SoundID.Item62 with { Volume = 0.4f, Pitch = 0.45f }, Projectile.Center);
            PRTLoader.NewParticle<PRT_Light>(Projectile.Center, Vector2.Zero, GsMushroomSpearHeld.ShroomCyan, 0.4f)
                ?.Configure(12, 0.8f);
            PRTLoader.NewParticle<PRT_StarPulseRing>(Projectile.Center, Vector2.Zero, GsMushroomSpearHeld.ShroomBlue, 0.05f)
                ?.Configure(0.08f, 0.55f, 16);
            for (int i = 0; i < 12; i++) {
                Vector2 vel = Main.rand.NextVector2Unit() * Main.rand.NextFloat(2f, 6f);
                Color c = Main.rand.NextBool(3) ? GsMushroomSpearHeld.ShroomCyan : GsMushroomSpearHeld.ShroomBlue;
                PRTLoader.NewParticle<PRT_Spark>(Projectile.Center, vel, c, Main.rand.NextFloat(0.32f, 0.55f))
                    ?.Configure(false, Main.rand.Next(12, 20));
            }
        }

        /// <summary>三层呼吸菌冠：驻场呼吸逐渐加速预警，爆窗白闪（whoAmI 种子，无随机）</summary>
        public override bool PreDraw(ref Color lightColor) {
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            Texture2D star = CWRAsset.StarGlow01?.Value;
            if (glow == null || star == null) {
                return false;
            }
            Vector2 pos = Projectile.Center - Main.screenPosition;
            Vector2 origin = glow.Size() / 2f;
            float armT = MathHelper.Clamp(Life / ArmFrames, 0f, 1f);
            //呼吸频率随驻场推进加速：4Hz → 14Hz 的临爆预警
            float breathSpeed = MathHelper.Lerp(4f, 14f, armT * armT);
            float breath = 0.85f + 0.15f * MathF.Sin(Main.GlobalTimeWrappedHourly * breathSpeed + Seed * 3f);
            float burstFlash = Bursting ? MathHelper.Clamp(Projectile.timeLeft / (float)BurstFrames, 0f, 1f) : 0f;

            //深菌影底晕
            Main.spriteBatch.Draw(glow, pos, null,
                (GsMushroomSpearHeld.ShroomDeep with { A = 0 }) * (0.6f + burstFlash * 0.3f), 0f,
                origin, (0.55f + burstFlash * 0.9f) * breath, SpriteEffects.None, 0f);
            //蓝荧主冠
            Main.spriteBatch.Draw(glow, pos, null,
                (GsMushroomSpearHeld.ShroomBlue with { A = 0 }) * (0.8f + burstFlash * 0.2f), 0f,
                origin, (0.38f + burstFlash * 0.7f) * breath, SpriteEffects.None, 0f);
            //亮孢芯：四芒星光缓旋
            Main.spriteBatch.Draw(star, pos, null,
                (GsMushroomSpearHeld.ShroomCyan with { A = 0 }) * (0.65f + burstFlash * 0.35f),
                Main.GlobalTimeWrappedHourly * 1.5f + Seed, star.Size() / 2f,
                (0.2f + armT * 0.08f + burstFlash * 0.35f) * breath, SpriteEffects.None, 0f);
            return false;
        }
    }
}
