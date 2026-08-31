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
    /// 秘银戟重铸：秘银月刃。<br/>
    /// 材质：翠绿秘银戟刃。签名行为：①两拍连刺——轻拍快刺、重拍深刺几何差异化
    /// ②重拍刺出的爆发帧放出一道短程秘银月牙刃，飞约 220 像素渐隐消散、可穿透两个目标
    /// ③重拍命中翠绿闪辉与月鸣泛音，轻拍是干净的秘银脆响
    /// </summary>
    internal class GsMythrilHalberd : GsSpearScheme
    {
        public override int TargetItemID => ItemID.MythrilHalberd;

        protected override string GsDescFallback =>
            "Reforged: two-beat halberd work, a quick jab then a deep heavy thrust;" +
            "\nthe heavy beat looses a short-ranged mythril crescent that pierces twice";

        protected override int HeldProjType => ModContent.ProjectileType<GsMythrilHalberdHeld>();

        protected override int ComboBeats => 2;

        public override void GsModifyWeaponDamage(Item item, Player player, ref StatModifier damage)
            => damage *= 1.06f;//月牙刃吃掉大半机制预算，综合 DPS 落在原版 106%~118%
    }

    /// <summary>
    /// 秘银戟手持突刺。ai[0]=拍号 0 轻快刺 / 1 重深刺；
    /// 重拍爆发帧放 GsMythrilHalberdWaveProj（60% 伤害，穿透 2）
    /// </summary>
    internal class GsMythrilHalberdHeld : GsThrustHeldBase
    {
        protected override int TargetItemType => ItemID.MythrilHalberd;

        //翠绿秘银色板
        internal static readonly Color MythrilBright = new(170, 255, 208);  //翠亮
        internal static readonly Color MythrilGreen = new(74, 200, 138);    //秘银绿
        internal static readonly Color MythrilDeep = new(24, 84, 66);       //深翠影

        private bool IsHeavy => ComboStage == 1;

        //重拍更沉更深
        protected override float WindupFrames => IsHeavy ? 6f : 4f;
        protected override float ThrustFrames => IsHeavy ? 6f : 5f;
        protected override float DwellFrames => IsHeavy ? 4f : 3f;
        protected override float RecoverFrames => IsHeavy ? 9f : 8f;
        protected override float RestHoldout => 10f;
        protected override float PullbackDist => IsHeavy ? 18f : 13f;
        protected override float StabReach => IsHeavy ? 72f : 58f;
        protected override float BladeLength => 92f;
        protected override float CollisionWidth => 30f;
        protected override float TipGreedRadius => 27f;
        protected override float ThrustEasePower => IsHeavy ? 3.2f : 2.7f;
        protected override bool TwoHanded => true;
        protected override float LeanAmp => IsHeavy ? 0.05f : 0.035f;
        protected override int HitboxSize => 52;
        protected override int HitstopFrames => IsHeavy ? 3 : 2;
        protected override float ThrustPitch => IsHeavy ? -0.28f : -0.10f;

        protected override Color EdgeColor => MythrilBright;
        protected override Color CoreColor => MythrilGreen;
        protected override Color ShaftColor => MythrilDeep with { A = 235 };

        protected override void OnInit() {
            if (IsHeavy) {
                Projectile.damage = (int)(Projectile.damage * 1.15f);
            }
        }

        protected override void OnThrustBurst() {
            //重拍爆发帧放月牙刃（owner 端生成，随生成包过线）
            if (IsHeavy && Projectile.IsOwnedByLocalPlayer()) {
                Projectile.NewProjectile(Projectile.GetSource_FromAI(), TipPos, stabUnit * 11f,
                    ModContent.ProjectileType<GsMythrilHalberdWaveProj>(),
                    (int)(BaseDamage * 0.6f), Projectile.knockBack * 0.4f, Owner.whoAmI);
            }
            if (VaultUtils.isServer) {
                return;
            }
            SoundEngine.PlaySound(SoundID.Item1 with { Volume = 0.8f, Pitch = ThrustPitch }, Owner.Center);
            if (IsHeavy) {
                SoundEngine.PlaySound(SoundID.Item71 with { Volume = 0.4f, Pitch = 0.25f }, Owner.Center);
            }
            int count = IsHeavy ? 4 : 2;
            for (int i = 0; i < count; i++) {
                Vector2 at = Vector2.Lerp(Hand, TipPos, Main.rand.NextFloat(0.5f, 1f));
                Color c = Main.rand.NextBool(3) ? MythrilBright : MythrilGreen;
                PRTLoader.NewParticle<PRT_Spark>(at, stabUnit * Main.rand.NextFloat(4f, 8f), c,
                    Main.rand.NextFloat(0.32f, 0.55f))?.Configure(true, Main.rand.Next(10, 16));
            }
        }

        /// <summary>命中反馈：秘银脆响；重拍升级为翠绿闪辉 + 月鸣泛音</summary>
        protected override void SpawnHitEffects(NPC target, NPC.HitInfo hit) {
            Vector2 pos = Vector2.Lerp(TipPos, target.Center, 0.5f);
            SoundEngine.PlaySound(SoundID.Item37 with { Volume = 0.35f, Pitch = IsHeavy ? -0.1f : 0.3f, MaxInstances = 3 }, target.Center);
            if (IsHeavy) {
                SoundEngine.PlaySound(SoundID.Item101 with { Volume = 0.25f, Pitch = 0.5f, MaxInstances = 2 }, target.Center);
            }
            PRTLoader.NewParticle<PRT_Light>(pos, Vector2.Zero, IsHeavy ? MythrilBright : MythrilGreen,
                IsHeavy ? 0.24f : 0.16f)?.Configure(9, 0.75f);
            int sparks = IsHeavy ? 8 : 5;
            for (int i = 0; i < sparks; i++) {
                Vector2 vel = stabUnit.RotatedByRandom(0.55) * Main.rand.NextFloat(3.5f, 7.5f);
                Color c = Main.rand.NextBool(3) ? MythrilBright : MythrilGreen;
                PRTLoader.NewParticle<PRT_Spark>(pos, vel, c, Main.rand.NextFloat(0.35f, 0.6f))
                    ?.Configure(true, Main.rand.Next(12, 20));
            }
        }

        /// <summary>重拍蓄势期戟刃泛翠光</summary>
        protected override float ExtraGlowStrength() => IsHeavy && CurrentPhase == PhaseWindup ? 0.25f : 0f;
    }

    /// <summary>
    /// 秘银月牙刃：重拍刺出时自戟尖放出的短程刃气，飞约 220 像素渐隐消散，穿透 2。<br/>
    /// 自绘三层：深翠涂抹垫底 + 秘银绿月牙主体 + 翠亮刃缘，残影拖迹随旧位置渐淡
    /// </summary>
    internal class GsMythrilHalberdWaveProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;
        public override LocalizedText DisplayName => Language.GetText("ItemName.MythrilHalberd");

        private ref float Life => ref Projectile.localAI[0];

        /// <summary>出生 2 帧淡入、末尾 6 帧淡出（渐隐消散）</summary>
        private float VisualFade => Math.Min(
            MathHelper.Clamp(Life / 2f, 0f, 1f),
            MathHelper.Clamp(Projectile.timeLeft / 6f, 0f, 1f));

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 5;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 34;
            Projectile.friendly = true;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = 2;//穿透 2：至多命中两个目标
            Projectile.timeLeft = 20;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
        }

        public override void AI() {
            Life++;
            Projectile.rotation = Projectile.velocity.ToRotation();
            //末段骤减收势（不匀速直飞）
            if (Life > 12f) {
                Projectile.velocity *= 0.88f;
            }
            Lighting.AddLight(Projectile.Center, GsMythrilHalberdHeld.MythrilGreen.ToVector3() * (0.3f * VisualFade));
            if (VaultUtils.isServer) {
                return;
            }
            if (Life % 2f == 0f) {
                PRTLoader.NewParticle<PRT_Light>(
                    Projectile.Center + Main.rand.NextVector2Circular(8f, 8f),
                    -Projectile.velocity * 0.08f,
                    Main.rand.NextBool(3) ? GsMythrilHalberdHeld.MythrilBright : GsMythrilHalberdHeld.MythrilGreen,
                    Main.rand.NextFloat(0.3f, 0.5f))?.Configure(Main.rand.Next(8, 13), 0.5f, 1.4f);
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (VaultUtils.isServer) {
                return;
            }
            SoundEngine.PlaySound(SoundID.NPCHit54 with { Volume = 0.3f, Pitch = 0.4f, MaxInstances = 3 }, target.Center);
            for (int i = 0; i < 5; i++) {
                Vector2 vel = Projectile.velocity.SafeNormalize(Vector2.UnitX).RotatedByRandom(0.6) * Main.rand.NextFloat(2.5f, 6f);
                Color c = Main.rand.NextBool() ? GsMythrilHalberdHeld.MythrilBright : GsMythrilHalberdHeld.MythrilGreen;
                PRTLoader.NewParticle<PRT_Spark>(target.Center, vel, c, Main.rand.NextFloat(0.3f, 0.5f))
                    ?.Configure(true, Main.rand.Next(10, 16));
            }
        }

        /// <summary>三层月牙 + 残影拖迹（全加色 A=0，无随机）</summary>
        public override bool PreDraw(ref Color lightColor) {
            Texture2D crescent = CWRAsset.CrescentEdge01?.Value;
            if (crescent == null) {
                return false;
            }
            float fade = VisualFade;
            float rotation = Projectile.rotation;
            Vector2 origin = crescent.Size() * 0.5f;

            //残影拖迹：旧位置画渐淡月牙
            for (int i = Projectile.oldPos.Length - 1; i >= 1; i--) {
                if (Projectile.oldPos[i] == Vector2.Zero) {
                    continue;
                }
                float ghost = (1f - i / (float)Projectile.oldPos.Length) * 0.32f * fade;
                Vector2 gpos = Projectile.oldPos[i] + Projectile.Size * 0.5f - Main.screenPosition;
                Main.EntitySpriteDraw(crescent, gpos, null,
                    (GsMythrilHalberdHeld.MythrilDeep with { A = 0 }) * ghost, rotation, origin,
                    new Vector2(0.32f, 0.22f) * (1f - i * 0.07f), SpriteEffects.None, 0);
            }

            Vector2 pos = Projectile.Center - Main.screenPosition;
            //深翠垫底
            Main.EntitySpriteDraw(crescent, pos, null,
                (GsMythrilHalberdHeld.MythrilDeep with { A = 0 }) * (0.7f * fade), rotation, origin,
                new Vector2(0.40f, 0.30f), SpriteEffects.None, 0);
            //秘银绿主体
            Main.EntitySpriteDraw(crescent, pos, null,
                (GsMythrilHalberdHeld.MythrilGreen with { A = 0 }) * fade, rotation, origin,
                new Vector2(0.36f, 0.26f), SpriteEffects.None, 0);
            //翠亮刃缘
            Main.EntitySpriteDraw(crescent, pos, null,
                (GsMythrilHalberdHeld.MythrilBright with { A = 0 }) * (0.8f * fade), rotation, origin,
                new Vector2(0.30f, 0.18f), SpriteEffects.None, 0);
            return false;
        }
    }
}
