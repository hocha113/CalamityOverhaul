using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Broadswords
{
    /// <summary>
    /// 【孢子迷雾】材质：叶绿锭锻的轻军刀，刃面覆生孢子苔。
    /// 签名：①原版孢子云保留升级：每一斩都在挥弧外缘留下驻留孢子雾
    /// （软噪声云缓慢漂移，触之中毒）②雾中的目标被刀刃命中会叠上剧毒并吃额外伤害
    /// ③快拍轻剑手感：三拍短举快出，斩切飘散叶绿孢尘
    /// </summary>
    internal class GsChlorophyteSaber : GsBroadswordScheme
    {
        public override int TargetItemID => ItemID.ChlorophyteSaber;

        protected override int HeldProjID => ModContent.ProjectileType<GsChlorophyteSaberHeld>();

        protected override string GsDescFallback =>
            "Reforged: a swift three-beat saber; every slash seeds a lingering spore shroud " +
            "along the arc's edge, and blade strikes on shrouded targets " +
            "deal bonus damage and stack venom";

        //叶绿色板
        internal static readonly Color SporeBright = new(208, 255, 170); //苔绿亮缘
        internal static readonly Color SporeMain = new(96, 200, 90);     //叶绿体色
        internal static readonly Color SporeHot = new(150, 255, 80);     //剧毒亮绿
        internal static readonly Color SporeDeep = new(16, 40, 22);      //幽林暗绿

        //原版每斩附带孢子云（驻留毒云），这里以 0.32x 雾团驻留 150 帧多跳对位替代
        //（30 帧跳一次，实战 2~3 跳 ≈ 0.6~1.0x/斩）；拍均 1.05x、三拍循环 ~51 帧
        //对原版 16 帧/斩 帧效率 ~0.97x；雾内近战 +12% 与剧毒为条件收益 →
        //综合 DPS 约为原版 103%~117%，底伤不动
        public override void GsModifyWeaponDamage(Item item, Player player, ref StatModifier damage) { }
    }

    /// <summary>
    /// 孢子迷雾手持：三拍快剑。0/1 交替轻斩（短举快收）/ 2 撒孢终结（略重，雾更浓）。
    /// 每拍收势首帧在挥弧外缘播下孢子雾。ai[0]=拍号 ai[1]=交替符号
    /// </summary>
    internal class GsChlorophyteSaberHeld : GsBroadswordHeldBase
    {
        protected override int SwordItemID => ItemID.ChlorophyteSaber;
        protected override Color EdgeBright => GsChlorophyteSaber.SporeBright;
        protected override Color BodyMain => GsChlorophyteSaber.SporeMain;
        protected override Color HotAccent => GsChlorophyteSaber.SporeHot;
        protected override Color DeepShadow => GsChlorophyteSaber.SporeDeep;

        //轻军刀：触及略短、判定略窄
        protected override float BaseReach => 108f;
        protected override float CollisionWidth => 36f;

        private bool mistSpawned;

        protected override GsBroadBeat GetBeat(int stage) => stage switch {
            //拍0 轻斩：短举快出，音调轻扬
            0 => new GsBroadBeat {
                Raise = 4, Hold = 1, Slash = 3, Recover = 7,
                RaiseBack = 1.6f, Follow = 0.95f, ReachScale = 1f, LeanAmp = 0.035f,
                DamageMult = 1f, Hitstop = 1, LungeSpeed = 0f, SwingPitch = 0.05f,
            },
            //拍1 返斩：同样轻快，音调更高
            1 => new GsBroadBeat {
                Raise = 4, Hold = 1, Slash = 3, Recover = 7,
                RaiseBack = 1.65f, Follow = 1.0f, ReachScale = 1f, LeanAmp = 0.04f,
                DamageMult = 1f, Hitstop = 1, LungeSpeed = 0f, SwingPitch = 0.14f,
            },
            //拍2 撒孢：稍长的举拍抖苔，斩幅放宽
            _ => new GsBroadBeat {
                Raise = 6, Hold = 2, Slash = 4, Recover = 9,
                RaiseBack = 1.95f, Follow = 1.15f, ReachScale = 1.08f, LeanAmp = 0.06f,
                DamageMult = 1.15f, Hitstop = 1, LungeSpeed = 1.4f, SwingPitch = -0.1f,
            },
        };

        //苔生刀身微微渗绿
        protected override Color BodyTint(Color lightColor)
            => Color.Lerp(lightColor, GsChlorophyteSaber.SporeMain, 0.12f);

        protected override void HandlePhaseEvents(int phase) {
            base.HandlePhaseEvents(phase);
            //收势首帧在挥弧外缘播雾：终结拍雾更浓（伤害系数在生成时区分）
            if (!mistSpawned && phase == PhaseRecover) {
                mistSpawned = true;
                float midAng = MathHelper.Lerp(ArcStart, ArcEnd, 0.55f);
                Vector2 dir = midAng.ToRotationVector2();
                Vector2 at = Hand + dir * (FullReach * 0.82f);
                int mistDamage = Math.Max(1, (int)(Projectile.damage * 0.32f));
                SpawnOwnedProj(ModContent.ProjectileType<GsChlorophyteSaberMistProj>(),
                    at, dir * 0.7f + new Vector2(0f, -0.15f), mistDamage, 0f,
                    IsFinisher ? 1f : 0f);
            }
        }

        /// <summary>雾内命中：额外 12% 伤害（剧毒在命中记账里叠）</summary>
        protected override void ModifyHitExtra(NPC target, ref NPC.HitModifiers modifiers) {
            if (TargetInMist(target)) {
                modifiers.SourceDamage *= 1.12f;
            }
        }

        protected override void OnHitTarget(NPC target, NPC.HitInfo hit, int damageDone) {
            //雾内命中叠剧毒（AddBuff 自动同步，各端一致）
            if (TargetInMist(target)) {
                target.AddBuff(BuffID.Venom, 90);
            }
        }

        /// <summary>目标是否处于本玩家任意一团孢子雾内</summary>
        private bool TargetInMist(NPC target) {
            int mistType = ModContent.ProjectileType<GsChlorophyteSaberMistProj>();
            foreach (Projectile proj in Main.ActiveProjectiles) {
                if (proj.owner != Projectile.owner || proj.type != mistType) {
                    continue;
                }
                if (target.Hitbox.Distance(proj.Center) <= GsChlorophyteSaberMistProj.MistRadius) {
                    return true;
                }
            }
            return false;
        }

        protected override void PlaySwingSound() {
            SoundEngine.PlaySound(SoundID.Item1 with { Volume = 0.7f, Pitch = Beat.SwingPitch }, Owner.Center);
            if (IsFinisher) {
                //撒孢：一记潮湿的叶响垫底
                SoundEngine.PlaySound(SoundID.Grass with { Volume = 0.55f, Pitch = -0.2f }, Owner.Center);
            }
        }

        protected override void HandleParticles(int phase) {
            base.HandleParticles(phase);
            if (phase == PhaseSlash && Main.rand.NextBool(2)) {
                //斩切期刃面抖落叶绿孢尘
                Dust d = Dust.NewDustPerfect(Vector2.Lerp(Hand, mainTip, Main.rand.NextFloat(0.5f, 1f)),
                    DustID.ChlorophyteWeapon, Vector2.Zero, 100, default, Main.rand.NextFloat(0.7f, 1.1f));
                d.noGravity = true;
                d.velocity = (mainAngle + swingDir * MathHelper.PiOver2).ToRotationVector2() * 1.8f;
            }
        }

        protected override void OnHitFX(NPC target, NPC.HitInfo hit, int damageDone) {
            base.OnHitFX(target, hit, damageDone);
            //孢尘扑溅：雾内命中更浓
            int motes = TargetInMist(target) ? 6 : 3;
            for (int i = 0; i < motes; i++) {
                Dust d = Dust.NewDustPerfect(target.Center, DustID.ChlorophyteWeapon,
                    Main.rand.NextVector2Unit() * Main.rand.NextFloat(1.5f, 4f), 80, default,
                    Main.rand.NextFloat(0.9f, 1.4f));
                d.noGravity = true;
            }
        }
    }

    /// <summary>
    /// 驻留孢子雾：每一斩留在挥弧外缘的软雾团。缓慢漂移渐停，150 帧寿命，
    /// 30 帧一跳并挂中毒；ai[0]=浓雾旗（终结拍更大更浓）。
    /// 暗绿雾体用真 alpha 压暗背景，苔绿光点走加色；绘制抖动全部 identity 播种
    /// </summary>
    internal class GsChlorophyteSaberMistProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        internal const float MistRadius = 74f;
        private const int TotalLife = 150;
        private const int Blobs = 4;

        private bool Dense => Projectile.ai[0] > 0.5f;
        private float SizeMul => Dense ? 1.22f : 1f;
        private ref float Life => ref Projectile.localAI[0];
        private float Life01 => MathHelper.Clamp(Life / TotalLife, 0f, 1f);

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 24;
            Projectile.friendly = true;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 30;
            Projectile.timeLeft = TotalLife;
        }

        public override void AI() {
            Life++;
            //漂移渐停：雾团出弧后慢慢驻定
            Projectile.velocity *= 0.965f;

            Lighting.AddLight(Projectile.Center,
                GsChlorophyteSaber.SporeMain.ToVector3() * (0.35f * (1f - Life01) * SizeMul));

            if (!VaultUtils.isServer && Main.rand.NextBool(5)) {
                //雾内孢尘缓浮
                Vector2 at = Projectile.Center + Main.rand.NextVector2Circular(MistRadius * 0.8f, MistRadius * 0.8f);
                Dust d = Dust.NewDustPerfect(at, DustID.ChlorophyteWeapon,
                    new Vector2(0f, -Main.rand.NextFloat(0.2f, 0.7f)), 140, default, Main.rand.NextFloat(0.5f, 0.9f));
                d.noGravity = true;
            }
        }

        //出生 6 帧成形后才开始跳伤
        public override bool? CanDamage() => Life >= 6f && Projectile.timeLeft > 10 ? null : false;

        /// <summary>圆形雾域判定</summary>
        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
            => targetHitbox.Distance(Projectile.Center) <= MistRadius * SizeMul;

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            //雾的跳伤挂普通中毒；浓雾时间更长
            target.AddBuff(BuffID.Poisoned, Dense ? 240 : 150);
        }

        /// <summary>绘制路径确定性伪随机</summary>
        private float SegRand(int salt) {
            uint h = (uint)(Projectile.identity * 374761393 + salt * 668265263);
            h = (h ^ (h >> 13)) * 1274126177u;
            return ((h ^ (h >> 16)) & 0xFFFFFF) / (float)0x1000000;
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D blot = CWRAsset.Extra_98?.Value;
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (blot == null || glow == null) {
                return false;
            }
            Vector2 center = Projectile.Center - Main.screenPosition;
            float life = Life01;
            //出生 8 帧带 8% 过冲撑开
            float grow = Life <= 8f ? 1.08f * (Life / 8f)
                : MathHelper.Lerp(1.08f, 1f, MathHelper.Clamp((Life - 8f) / 6f, 0f, 1f));
            float baseAlpha = Dense ? 0.55f : 0.42f;

            //雾体：数团真 alpha 暗绿噪斑，各团独立漂移与蚀散次序
            for (int i = 0; i < Blobs; i++) {
                float dieAt = 0.55f + 0.45f * SegRand(i);
                float segFade = MathHelper.Clamp((dieAt - life) / 0.3f, 0f, 1f);
                if (segFade <= 0.01f) {
                    continue;
                }
                float drift = Main.GlobalTimeWrappedHourly * (0.25f + 0.2f * SegRand(i + 10)) + SegRand(i) * 6.28f;
                Vector2 at = center + drift.ToRotationVector2() * (MistRadius * 0.4f * SizeMul * SegRand(i + 20));
                float scale = (0.42f + 0.22f * SegRand(i + 30)) * SizeMul * grow;
                Color dark = GsChlorophyteSaber.SporeDeep * (baseAlpha * segFade);
                Main.EntitySpriteDraw(blot, at, null, dark, SegRand(i + 40) * 6.28f + Life * 0.004f,
                    blot.Size() * 0.5f, scale, SpriteEffects.None, 0);
            }

            //苔绿光点：雾内几粒缓慢明灭的孢子光
            for (int i = 0; i < 5; i++) {
                float dieAt = 0.6f + 0.4f * SegRand(i + 50);
                float segFade = MathHelper.Clamp((dieAt - life) / 0.25f, 0f, 1f);
                if (segFade <= 0.01f) {
                    continue;
                }
                float orbit = Main.GlobalTimeWrappedHourly * (0.3f + 0.25f * SegRand(i + 60)) + SegRand(i + 70) * 6.28f;
                Vector2 at = center + orbit.ToRotationVector2() * (MistRadius * 0.55f * SizeMul * SegRand(i + 80));
                float pulse = 0.6f + 0.4f * MathF.Sin(Main.GlobalTimeWrappedHourly * 5f + SegRand(i + 90) * 6.28f);
                Color c = (SegRand(i) > 0.5f
                    ? GsChlorophyteSaber.SporeHot : GsChlorophyteSaber.SporeBright) * (0.4f * segFade * pulse);
                c.A = 0;
                Main.EntitySpriteDraw(glow, at, null, c, 0f, glow.Size() * 0.5f,
                    0.16f + 0.1f * SegRand(i + 95), SpriteEffects.None, 0);
            }
            return false;
        }
    }
}
