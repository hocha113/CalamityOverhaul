using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Broadswords
{
    /// <summary>
    /// 【虹彩珍珠】材质：镶珠贝的珍珠木伪圣剑。
    /// 签名：①每一斩沿挥弧外抛两枚珍珠光珠，命中或落地炸成虹彩碎光
    /// ②第三拍呼来珍珠雨，五枚光珠自头顶加速砸落
    /// ③终结拍刀光换虹彩流色，命中带珠贝脆响
    /// </summary>
    internal class GsPearlwoodSword : GsBroadswordScheme
    {
        public override int TargetItemID => ItemID.PearlwoodSword;

        protected override int HeldProjID => ModContent.ProjectileType<GsPearlwoodSwordHeld>();

        protected override string GsDescFallback =>
            "Reforged: a playful pearlwood combo that flings bursting pearls with every slash; " +
            "the third strike calls a short pearl rain from above";

        //珠贝虹彩色板
        internal static readonly Color PearlBright = new(255, 246, 232); //珠光乳白
        internal static readonly Color PearlMain = new(230, 186, 168);   //珠木暖粉
        internal static readonly Color PearlHot = new(255, 176, 220);    //虹粉强调
        internal static readonly Color PearlDeep = new(66, 48, 52);      //暖木垫影

        /// <summary>虹彩取色：相位 0~1 环行色相（调用方自供确定性相位或掷点）</summary>
        internal static Color Iris(float t) => Main.hslToRgb(((t % 1f) + 1f) % 1f, 0.55f, 0.72f);

        //弱势特许账（30 伤 15 帧的硬模式木剑公认垫底 → 上限放宽至 135%）：
        //拍均 (1+1+1.2)/3≈1.07；每斩 2 珠 ×0.30x 沿弧外抛（单体重叠约 1/4 → +0.15x/拍）；
        //终结珍珠雨 5×0.24x（单体实取 1~2 珠 → +0.36x/循环 → +0.12x/拍）；
        //连段总帧 51 对原版 45 (+13%) → 综合单体 DPS ≈ (1.07+0.27)×0.88 ≈ 原版 118%，
        //全珠命中的理论上界 ~135% 在弱势特许内（多目标溢出是覆盖收益），底伤不再加成
        public override void GsModifyWeaponDamage(Item item, Player player, ref StatModifier damage) { }
    }

    /// <summary>
    /// 虹彩珍珠手持：三拍轻快连击，每拍小步跳进、音高全族最高。
    /// 每拍斩切爆发外抛珍珠，终结拍另呼珍珠雨并换虹彩涂抹。ai[0]=拍号 ai[1]=交替符号
    /// </summary>
    internal class GsPearlwoodSwordHeld : GsBroadswordHeldBase
    {
        protected override int SwordItemID => ItemID.PearlwoodSword;
        protected override Color EdgeBright => GsPearlwoodSword.PearlBright;
        protected override Color BodyMain => GsPearlwoodSword.PearlMain;
        protected override Color HotAccent => GsPearlwoodSword.PearlHot;
        protected override Color DeepShadow => GsPearlwoodSword.PearlDeep;

        private bool pearlsFired;

        protected override GsBroadBeat GetBeat(int stage) => stage switch {
            //拍0 轻横斩：小步跳进
            0 => new GsBroadBeat {
                Raise = 4, Hold = 1, Slash = 3, Recover = 7,
                RaiseBack = 1.65f, Follow = 0.95f, ReachScale = 0.96f, LeanAmp = 0.035f,
                DamageMult = 1f, Hitstop = 1, LungeSpeed = 0.8f, SwingPitch = 0.3f,
            },
            //拍1 返斩：音高再上一阶
            1 => new GsBroadBeat {
                Raise = 4, Hold = 1, Slash = 3, Recover = 7,
                RaiseBack = 1.7f, Follow = 1.0f, ReachScale = 0.98f, LeanAmp = 0.04f,
                DamageMult = 1f, Hitstop = 1, LungeSpeed = 0.8f, SwingPitch = 0.38f,
            },
            //拍2 呼雨上撩：稍沉半档换珍珠雨
            _ => new GsBroadBeat {
                Raise = 6, Hold = 2, Slash = 4, Recover = 9,
                RaiseBack = 2.0f, Follow = 1.2f, ReachScale = 1.1f, LeanAmp = 0.06f,
                DamageMult = 1.2f, Hitstop = 2, LungeSpeed = 1.6f, SwingPitch = 0.12f,
            },
        };

        /// <summary>轻剑高音快哨；终结拍垫一记呼雨铃音</summary>
        protected override void PlaySwingSound() {
            SoundEngine.PlaySound(SoundID.Item1 with { Volume = 0.72f, Pitch = Beat.SwingPitch }, Owner.Center);
            if (IsFinisher) {
                SoundEngine.PlaySound(SoundID.Item4 with { Volume = 0.35f, Pitch = 0.45f }, Owner.Center);
            }
        }

        //终结拍涂抹换虹彩流色（timer+identity 播种，绘制路径无随机）
        protected override Color SmearOuterColor =>
            IsFinisher ? GsPearlwoodSword.Iris(timer * 0.03f + DrawRand01(2)) : EdgeBright;

        /// <summary>每拍外抛两枚珍珠；终结拍另撒五枚珍珠雨（上方遇实心块自动压低雨高）</summary>
        protected override void OnSlashBegin() {
            if (pearlsFired) {
                return;
            }
            pearlsFired = true;
            int pearlDamage = Math.Max(1, (int)(Projectile.damage * 0.30f));
            for (int i = 0; i < 2; i++) {
                float ang = MathHelper.Lerp(ArcStart, ArcEnd, 0.35f + 0.3f * i);
                Vector2 dir = ang.ToRotationVector2();
                Vector2 vel = dir * Main.rand.NextFloat(6.5f, 8f) + new Vector2(0f, -2.4f);
                SpawnOwnedProj(ModContent.ProjectileType<GsPearlwoodSwordPearlProj>(),
                    Hand + dir * (FullReach * 0.7f), vel, pearlDamage, Projectile.knockBack * 0.3f);
            }
            if (!IsFinisher) {
                return;
            }
            SetFlash(6);
            //珍珠雨：瞄准向前方上空撒五枚；向上探 11 格实心块，洞穴里雨线自动放矮
            int rainDamage = Math.Max(1, (int)(Projectile.damage * 0.24f));
            Vector2 anchor = Hand + baseAngle.ToRotationVector2() * 120f;
            float ceiling = 170f;
            Point tile = anchor.ToTileCoordinates();
            for (int j = 2; j <= 11; j++) {
                if (WorldGen.SolidTile(tile.X, tile.Y - j)) {
                    ceiling = MathF.Max(46f, (j - 1) * 16f - 10f);
                    break;
                }
            }
            for (int i = 0; i < 5; i++) {
                Vector2 at = new(anchor.X + ((i - 2) * 34f) + Main.rand.NextFloat(-10f, 10f),
                    anchor.Y - ceiling - Main.rand.NextFloat(0f, 24f));
                SpawnOwnedProj(ModContent.ProjectileType<GsPearlwoodSwordPearlProj>(),
                    at, new Vector2(Main.rand.NextFloat(-0.5f, 0.5f), 2.6f), rainDamage,
                    Projectile.knockBack * 0.3f, 0f, 1f);
            }
        }

        protected override void HandleParticles(int phase) {
            base.HandleParticles(phase);
            if (!IsFinisher || phase > PhaseHold) {
                return;
            }
            //呼雨蓄势：虹彩光尘自四周汇入刀身
            Vector2 hand = Hand;
            Vector2 at = hand + Main.rand.NextVector2Unit() * Main.rand.NextFloat(36f, 66f);
            PRTLoader.NewParticle<PRT_Light>(at, (Vector2.Lerp(hand, mainTip, 0.6f) - at) * 0.14f,
                GsPearlwoodSword.Iris(Main.rand.NextFloat()), Main.rand.NextFloat(0.05f, 0.09f))?.Configure(9, 0.55f);
        }

        protected override void OnHitFX(NPC target, NPC.HitInfo hit, int damageDone) {
            base.OnHitFX(target, hit, damageDone);
            //珠贝脆响 + 一两粒虹彩星屑
            SoundEngine.PlaySound(SoundID.Item27 with { Volume = 0.25f, Pitch = 0.5f, MaxInstances = 3 }, target.Center);
            int motes = IsFinisher ? 3 : 1;
            for (int i = 0; i < motes; i++) {
                PRTLoader.NewParticle<PRT_Sparkle>(target.Center + Main.rand.NextVector2Circular(10f, 10f),
                    -Vector2.UnitY * Main.rand.NextFloat(0.4f, 1.4f), Color.White, 0.8f)
                    ?.Configure(GsPearlwoodSword.Iris(Main.rand.NextFloat()), Main.rand.Next(16, 24), 0.1f, 1.1f);
            }
        }
    }

    /// <summary>
    /// 珍珠光珠：抛珠走轻重力抛物线、雨珠加速直坠，命中/落地炸成虹彩碎光。
    /// 自绘四层：虹彩晕圈（色相随寿命环行）+ 珠光本体 + 白亮芯 + 旋转星芒；
    /// 拖尾按速度回溯三段渐淡。ai[1]=1 为雨珠。绘制色相 identity+寿命播种，无随机
    /// </summary>
    internal class GsPearlwoodSwordPearlProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;
        public override LocalizedText DisplayName => Language.GetText("ItemName.PearlwoodSword");

        private bool Rain => Projectile.ai[1] > 0.5f;
        private ref float Life => ref Projectile.localAI[0];

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 16;
            Projectile.friendly = true;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 90;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
        }

        public override void AI() {
            Life++;
            if (Rain) {
                //雨珠：加速直坠，末速 15
                Projectile.velocity.Y = MathF.Min(Projectile.velocity.Y + 0.5f, 15f);
                Projectile.velocity.X *= 0.99f;
            }
            else {
                //抛珠：轻重力抛物线，横速缓收
                Projectile.velocity.Y = MathF.Min(Projectile.velocity.Y + 0.22f, 12f);
                Projectile.velocity.X *= 0.985f;
            }
            Projectile.rotation += 0.18f * (Projectile.velocity.X >= 0f ? 1f : -1f);

            Lighting.AddLight(Projectile.Center, GsPearlwoodSword.PearlBright.ToVector3() * 0.28f);

            if (!VaultUtils.isServer && Main.rand.NextBool(4)) {
                //航迹上飘一粒虹尘
                PRTLoader.NewParticle<PRT_Light>(Projectile.Center, -Projectile.velocity * 0.06f,
                    GsPearlwoodSword.Iris(Main.rand.NextFloat()), Main.rand.NextFloat(0.05f, 0.09f))?.Configure(9, 0.55f);
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (VaultUtils.isServer) {
                return;
            }
            for (int i = 0; i < 3; i++) {
                PRTLoader.NewParticle<PRT_Spark>(target.Center,
                    Main.rand.NextVector2Unit() * Main.rand.NextFloat(2f, 4.5f),
                    GsPearlwoodSword.Iris(Main.rand.NextFloat()), Main.rand.NextFloat(0.28f, 0.45f))
                    ?.Configure(true, Main.rand.Next(9, 15));
            }
        }

        /// <summary>珠碎：虹彩碎光四散 + 珠贝脆响</summary>
        public override void OnKill(int timeLeft) {
            if (VaultUtils.isServer) {
                return;
            }
            SoundEngine.PlaySound(SoundID.Item27 with { Volume = 0.35f, Pitch = 0.45f, MaxInstances = 3 }, Projectile.Center);
            for (int i = 0; i < 6; i++) {
                PRTLoader.NewParticle<PRT_Spark>(Projectile.Center,
                    Main.rand.NextVector2Unit() * Main.rand.NextFloat(2f, 5.5f),
                    GsPearlwoodSword.Iris(Main.rand.NextFloat()), Main.rand.NextFloat(0.3f, 0.5f))
                    ?.Configure(true, Main.rand.Next(10, 18));
            }
            for (int i = 0; i < 2; i++) {
                PRTLoader.NewParticle<PRT_Light>(Projectile.Center + Main.rand.NextVector2Circular(6f, 6f),
                    -Vector2.UnitY * Main.rand.NextFloat(0.3f, 0.8f), GsPearlwoodSword.PearlBright,
                    Main.rand.NextFloat(0.07f, 0.11f))?.Configure(11, 0.6f);
            }
        }

        /// <summary>绘制路径确定性伪随机（identity+salt 播种）</summary>
        private float SegRand(int salt) {
            uint h = (uint)(Projectile.identity * 374761393 + salt * 668265263);
            h = (h ^ (h >> 13)) * 1274126177u;
            return ((h ^ (h >> 16)) & 0xFFFFFF) / (float)0x1000000;
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            Texture2D star = CWRAsset.StarGlow01?.Value;
            if (glow == null || star == null) {
                return false;
            }
            Vector2 center = Projectile.Center - Main.screenPosition;
            //色相随寿命环行：每珠自带 identity 相位差
            float hueBase = SegRand(1) + Life * 0.016f;
            float fadeIn = MathHelper.Clamp(Life / 3f, 0f, 1f);

            //速度回溯拖尾：三段渐淡虹晕
            for (int i = 1; i <= 3; i++) {
                Vector2 back = center - Projectile.velocity * (i * 1.7f);
                Color trail = GsPearlwoodSword.Iris(hueBase - i * 0.06f) * (0.16f * (1f - i / 4f) * fadeIn);
                trail.A = 0;
                Main.EntitySpriteDraw(glow, back, null, trail, 0f, glow.Size() * 0.5f,
                    0.26f * (1f - i * 0.12f), SpriteEffects.None, 0);
            }

            //虹彩晕圈
            Color halo = GsPearlwoodSword.Iris(hueBase) * (0.55f * fadeIn);
            halo.A = 0;
            Main.EntitySpriteDraw(glow, center, null, halo, 0f, glow.Size() * 0.5f, 0.34f, SpriteEffects.None, 0);
            //珠光本体
            Color body = GsPearlwoodSword.PearlBright * (0.8f * fadeIn);
            body.A = 0;
            Main.EntitySpriteDraw(glow, center, null, body, 0f, glow.Size() * 0.5f, 0.2f, SpriteEffects.None, 0);
            //白亮芯
            Color core = Color.White * (0.6f * fadeIn);
            core.A = 0;
            Main.EntitySpriteDraw(glow, center, null, core, 0f, glow.Size() * 0.5f, 0.1f, SpriteEffects.None, 0);
            //旋转星芒：珠面反光
            float twinkle = 0.7f + 0.3f * MathF.Sin(Life * 0.35f + SegRand(7) * 6.28f);
            Color glint = GsPearlwoodSword.Iris(hueBase + 0.33f) * (0.5f * twinkle * fadeIn);
            glint.A = 0;
            Main.EntitySpriteDraw(star, center, null, glint, Projectile.rotation, star.Size() * 0.5f,
                0.16f * twinkle, SpriteEffects.None, 0);
            return false;
        }
    }
}
