using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.JungleHell.Projectiles
{
    /// <summary>
    /// 齐射预兆：原地凝形，倒计时结束沿锁定扇面瞬发弹幕幕，扇面里留一条具名走廊缺口。<br/>
    /// ai[0]=Pack(模式,档位) ai[1]=锁定瞄角 ai[2]=走廊中心偏移（生成瞬间全部锁定，预告即承诺）。<br/>
    /// 幽灵预览与发射循环共用同一条缺口判定，看见的缺口就是安全的缺口
    /// </summary>
    internal class JhVolleyOmen : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        internal const int ModeHornet = 0;
        internal const int ModeDemon = 1;
        internal const int ModeDevil = 2;
        internal const int ModeTortoise = 3;

        //各模式预告帧数（一律 ≥30）
        private static readonly int[] TelegraphFrames = [32, 36, 36, 30];
        /// <summary>扇面半张角（弧度）</summary>
        private static readonly float[] SpreadHalf = [0.62f, 0.55f, 0.50f, 0.85f];
        /// <summary>走廊缺口半张角（弧度）：发射循环真正读取的逃生阀门</summary>
        private static readonly float[] CorridorHalf = [0.16f, 0.18f, 0.17f, 0.24f];
        private static readonly int[] BoltCount = [9, 7, 7, 8];
        private static readonly float[] BoltSpeed = [7.5f, 5.2f, 9f, 6f];
        /// <summary>幽灵预览用的原版贴图（即各自要发射的弹体贴图）</summary>
        private static readonly int[] DonorProj = [
            ProjectileID.Stinger, ProjectileID.DemonSickle,
            ProjectileID.UnholyTridentHostile, ProjectileID.JungleSpike,
        ];
        /// <summary>档位 3 追加弹数（保持对称一次加二）</summary>
        private const int TierExtraBolts = 2;
        /// <summary>走廊中心离瞄准线的最大偏移，保证缺口始终落在扇面内侧</summary>
        private const float CorridorSafetyMargin = 0.06f;

        private static readonly Color[] ModeColor = [
            new Color(255, 200, 90),
            new Color(200, 90, 255),
            new Color(255, 110, 70),
            new Color(150, 220, 90),
        ];

        internal static float Pack(int mode, int tier) => mode + tier * 4;

        private int Mode => (int)Projectile.ai[0] % 4;
        private int Tier => Math.Max(1, (int)Projectile.ai[0] / 4);
        private float Aim => Projectile.ai[1];
        private int Total => TelegraphFrames[Mode];
        private int Age => Total - Projectile.timeLeft;

        /// <summary>走廊中心角：锁定偏移钳制在扇面内侧</summary>
        private float CorridorCenter {
            get {
                float maxOffset = Math.Max(0f, SpreadHalf[Mode] - CorridorHalf[Mode] - CorridorSafetyMargin);
                return Aim + MathHelper.Clamp(Projectile.ai[2], -maxOffset, maxOffset);
            }
        }

        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 600;

        public override void SetDefaults() {
            Projectile.width = 24;
            Projectile.height = 24;
            Projectile.hostile = false;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 36;
            Projectile.netImportant = true;
        }

        /// <summary>纯预告体，永不参与伤害</summary>
        public override bool? CanDamage() => false;

        public override bool ShouldUpdatePosition() => false;

        /// <summary>某个发射角是否落在走廊缺口里（发射与预览共用，缺口即所见）</summary>
        private bool InCorridor(float angle) =>
            Math.Abs(MathHelper.WrapAngle(angle - CorridorCenter)) < CorridorHalf[Mode];

        public override void AI() {
            if (Projectile.localAI[0] == 0f) {
                Projectile.localAI[0] = 1f;
                Projectile.timeLeft = Total;
                Projectile.rotation = Aim;
                if (!Main.dedServ) {
                    SoundEngine.PlaySound(SoundID.Item17 with { Volume = 0.4f, Pitch = -0.35f, MaxInstances = 4 }, Projectile.Center);
                }
            }

            //凝形勾边尘（≤3/帧）
            if (!Main.dedServ && Main.rand.NextBool(2)) {
                float edge = Main.rand.NextBool() ? 1f : -1f;
                Vector2 dir = (Aim + edge * SpreadHalf[Mode]).ToRotationVector2();
                Dust dust = Dust.NewDustPerfect(Projectile.Center + dir * Main.rand.NextFloat(10f, 34f),
                    ModeDust(), dir * 0.6f, 140, default, 0.9f);
                dust.noGravity = true;
            }

            Lighting.AddLight(Projectile.Center, ModeColor[Mode].ToVector3() * 0.25f);

            if (Projectile.timeLeft == 1 && !VaultUtils.isClient) {
                FireVolley();
            }
        }

        private int ModeDust() => Mode switch {
            ModeDemon => DustID.PurpleTorch,
            ModeDevil => DustID.Torch,
            ModeTortoise => DustID.JungleGrass,
            _ => DustID.GreenTorch,
        };

        /// <summary>瞬发齐射：固定散布，跳过走廊缺口内的所有弹位</summary>
        private void FireVolley() {
            int mode = Mode;
            int count = BoltCount[mode] + (Tier >= 3 ? TierExtraBolts : 0);
            float spread = SpreadHalf[mode];
            int boltType = mode switch {
                ModeDemon => ModContent.ProjectileType<JhScytheBolt>(),
                ModeDevil => ModContent.ProjectileType<JhTridentBolt>(),
                ModeTortoise => ModContent.ProjectileType<JhShellSpike>(),
                _ => ModContent.ProjectileType<JhStingerBolt>(),
            };

            for (int i = 0; i < count; i++) {
                float angle = Aim - spread + 2f * spread * i / (count - 1);
                //走廊缺口：这里跳过的方向就是预览里空着的方向
                if (InCorridor(angle)) {
                    continue;
                }
                Projectile.NewProjectile(Projectile.GetSource_FromAI(), Projectile.Center,
                    angle.ToRotationVector2() * BoltSpeed[mode], boltType,
                    Projectile.damage, 0f, Main.myPlayer, Tier);
            }
        }

        /// <summary>
        /// 齐射音效在死亡帧各端本地播放：发射走服务端路径，音效若留在那里，
        /// 联机时专用服务器上无人能听见（预兆只会自然超时死亡，死亡帧即发射帧）
        /// </summary>
        public override void OnKill(int timeLeft) {
            if (Main.dedServ) {
                return;
            }
            int mode = Mode;
            SoundEngine.PlaySound((mode == ModeHornet || mode == ModeTortoise
                ? SoundID.Item17 with { Volume = 0.8f }
                : SoundID.Item8 with { Volume = 0.8f, Pitch = mode == ModeDevil ? -0.3f : 0f })
                with { MaxInstances = 5 }, Projectile.Center);
        }

        public override bool PreDraw(ref Color lightColor) {
            int mode = Mode;
            int donor = DonorProj[mode];
            Main.instance.LoadProjectile(donor);
            Texture2D tex = TextureAssets.Projectile[donor].Value;
            Texture2D core = CWRAsset.Extra_98.Value;
            Vector2 origin = tex.Size() * 0.5f;
            Vector2 center = Projectile.Center - Main.screenPosition;

            float fadeIn = MathHelper.Clamp(Age / 10f, 0f, 1f);
            float urgency = MathHelper.Clamp(Age / (float)Total, 0f, 1f);
            float pulse = 0.7f + 0.3f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 11f + Projectile.identity * 0.9f);
            Color warn = ModeColor[mode];

            //凝形核：真透贴图打底（有遮挡像素），外围加色晕
            Main.EntitySpriteDraw(core, center, null, warn * (0.75f * fadeIn),
                Projectile.rotation, core.Size() / 2f, 0.34f + 0.1f * urgency, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(core, center, null, (warn with { A = 0 }) * (0.5f * fadeIn * pulse),
                0f, core.Size() / 2f, 0.55f + 0.15f * urgency, SpriteEffects.None, 0);

            //幽灵弹位预览：逐弹位画将射弹体贴图，走廊缺口空出来，所见即所射
            int count = BoltCount[mode] + (Tier >= 3 ? TierExtraBolts : 0);
            float spreadHalf = SpreadHalf[mode];
            float ghostAlpha = (0.28f + 0.3f * urgency) * fadeIn * pulse;
            for (int i = 0; i < count; i++) {
                float angle = Aim - spreadHalf + 2f * spreadHalf * i / (count - 1);
                if (InCorridor(angle)) {
                    continue;
                }
                Vector2 dir = angle.ToRotationVector2();
                for (int r = 0; r < 2; r++) {
                    float radius = 26f + 26f * r + 10f * urgency;
                    Main.EntitySpriteDraw(tex, center + dir * radius, null,
                        warn * (ghostAlpha * (r == 0 ? 1f : 0.6f)),
                        angle + MathHelper.PiOver2, origin, 0.85f, SpriteEffects.None, 0);
                }
            }
            return false;
        }
    }
}
