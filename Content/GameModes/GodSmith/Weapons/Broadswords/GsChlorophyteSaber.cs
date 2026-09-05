using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Broadswords
{
    /// <summary>
    /// 【孢子迷雾】材质：叶绿锭锻的轻军刀，刃面覆生孢子苔。
    /// 签名：①原版孢子云保留升级：每一斩都在挥弧外缘留下驻留孢子雾
    /// （缓慢漂移，触之中毒）②雾中的目标被刀刃命中会叠上剧毒并吃额外伤害
    /// ③快拍轻剑手感：三拍短举快出
    /// </summary>
    internal class GsChlorophyteSaber : GsBroadswordScheme
    {
        public override int TargetItemID => ItemID.ChlorophyteSaber;

        protected override int HeldProjID => ModContent.ProjectileType<GsChlorophyteSaberHeld>();

        protected override string GsDescFallback =>
            "Reforged: a swift three-beat saber; every slash seeds a lingering spore shroud along the arc's edge, and blade strikes on shrouded targets deal bonus damage and stack venom";
        internal static readonly Color SporeBright = new(208, 255, 170); //苔绿亮缘
        internal static readonly Color SporeMain = new(96, 200, 90);     //叶绿体色
        internal static readonly Color SporeHot = new(150, 255, 80);     //剧毒亮绿

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
    }

    /// <summary>
    /// 驻留孢子雾：每一斩留在挥弧外缘的雾团。缓慢漂移渐停，150 帧寿命，
    /// 30 帧一跳并挂中毒；ai[0]=浓雾旗（终结拍更大更浓）。
    /// 用原版叶绿军刀孢子云贴图按雾域半径缩放画一笔作范围提示
    /// </summary>
    internal class GsChlorophyteSaberMistProj : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.SporeCloud;

        internal const float MistRadius = 74f;
        private const int TotalLife = 150;

        private bool Dense => Projectile.ai[0] > 0.5f;
        private float SizeMul => Dense ? 1.22f : 1f;
        private ref float Life => ref Projectile.localAI[0];

        public override void SetStaticDefaults() {
            Main.projFrames[Type] = Main.projFrames[ProjectileID.SporeCloud];
        }

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

        /// <summary>范围提示：原版孢子云贴图按雾域半径缩放画一笔，出生 8 帧撑开、末段随寿命淡出</summary>
        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = TextureAssets.Projectile[Type].Value;
            Rectangle frame = tex.Frame(1, Math.Max(1, Main.projFrames[Type]), 0, 0);
            float grow = MathHelper.Clamp(Life / 8f, 0f, 1f);
            float fade = MathHelper.Clamp(Projectile.timeLeft / 20f, 0f, 1f);
            float scale = MistRadius * SizeMul * 2f * grow / MathF.Max(frame.Width, 1);
            Main.EntitySpriteDraw(tex, Projectile.Center - Main.screenPosition, frame, lightColor * (0.7f * fade),
                0f, frame.Size() * 0.5f, scale, SpriteEffects.None, 0);
            return false;
        }
    }
}
