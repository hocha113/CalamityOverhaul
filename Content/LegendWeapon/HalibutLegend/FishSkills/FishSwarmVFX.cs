using CalamityOverhaul.Common;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.Graphics.CameraModifiers;
using Terraria.ID;

namespace CalamityOverhaul.Content.LegendWeapon.HalibutLegend.FishSkills
{
    /// <summary>鱼形换影域内 shader 资源（域内加载器，不经 EffectLoader）</summary>
    internal class FishSwarmAssets
    {
        /// <summary>群体流线束：鱼群整体共享的水流缎带（非单鱼尾流）</summary>
        [VaultLoaden(CWRConstant.Effects)]
        public static Effect FishSwarmWake { get; private set; }
    }

    /// <summary>
    /// 鱼形换影共享演出协作类。<br/>
    /// 材质：椋鸟群式银鳞流场，群体是一个介质而非 N 条独立鱼。<br/>
    /// 色彩脚本：深水暗蓝压底 + 水流蓝中层 + 鳞银体色，银白只作 ≤2 帧离散 glint；
    /// 化形入/出由剪影鳞片分批溶解/收拢承担，禁瞬移 pop
    /// </summary>
    internal static class FishSwarmVFX
    {
        //==== 色彩脚本 ====
        /// <summary>深水暗蓝（拖影尾端/外圈压底）</summary>
        public static readonly Color Deep = new(20, 38, 58);
        /// <summary>水流蓝（饱和中层主色）</summary>
        public static readonly Color Flow = new(66, 124, 176);
        /// <summary>鳞银（鱼体染色基准）</summary>
        public static readonly Color Silver = new(168, 194, 214);
        /// <summary>鳞光近白冷银（仅限 ≤2 帧瞬闪与离散 glint）</summary>
        public static readonly Color Spec = new(224, 240, 250);

        /// <summary>剪影格：横向格数</summary>
        private const int GridX = 4;
        /// <summary>剪影格：纵向格数</summary>
        private const int GridY = 7;

        /// <summary>FishSwarmWake 标准参数；phase 传实例派生量避免多带同相</summary>
        public static void ApplyWake(Effect fx, float phase) {
            fx.Parameters["transformMatrix"]?.SetValue(VaultUtils.GetTransfromMatrix());
            fx.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly * 0.9f + phase);
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            if (noise != null) {
                fx.Parameters["uNoiseTex"]?.SetValue(noise);
            }
            fx.Parameters["uColDeep"]?.SetValue(Deep.ToVector3());
            fx.Parameters["uColFlow"]?.SetValue(Flow.ToVector3());
            fx.Parameters["uColSpec"]?.SetValue(Spec.ToVector3());
        }

        /// <summary>
        /// 化形入场：玩家剪影按 4×7 体格分批溶解成鳞片，边缘格先起飞、核心格滞留后跟，
        /// 配沿冲刺方向压扁的破水环与少量回甩水珠
        /// </summary>
        public static void DissolveBurst(Player player, Vector2 dashDir) {
            if (Main.dedServ) {
                return;
            }
            dashDir = dashDir.SafeNormalize(Vector2.UnitX);
            Rectangle box = player.Hitbox;
            for (int x = 0; x < GridX; x++) {
                for (int y = 0; y < GridY; y++) {
                    Vector2 cell = new(box.X + (x + 0.5f) * box.Width / (float)GridX
                        , box.Y + (y + 0.5f) * box.Height / (float)GridY);
                    //边缘格 0，越靠核心越大：边缘先散
                    int edge = Math.Min(Math.Min(x, GridX - 1 - x), Math.Min(y, GridY - 1 - y));
                    int hold = edge * 4 + Main.rand.Next(0, 4);
                    Color col = Color.Lerp(Silver, Flow, Main.rand.NextFloat(0f, 0.55f));
                    PRTLoader.NewParticle<PRT_FishSwarmFlake>(cell
                        , player.velocity * 0.22f + Main.rand.NextVector2Circular(0.8f, 0.8f)
                        , col, Main.rand.NextFloat(0.5f, 0.8f))
                        ?.ConfigureDissolve(hold, dashDir.RotatedBy(Main.rand.NextFloat(-0.38f, 0.38f))
                            , Main.rand.NextFloat(-0.14f, 0.14f));
                }
            }
            //破水口：沿冲刺方向压扁的扩散环，读作水面被穿开
            PRTLoader.NewParticle<PRT_DWave>(player.Center, Vector2.Zero, Flow, 0.15f)
                ?.Configure(new Vector2(1f, 0.45f), dashDir.ToRotation(), 0.9f, 16);
            //离体回甩的水珠（受重力）
            DropletFan(player.Center, -dashDir, 6, 2f, 5f, 0.9f);
            Main.instance.CameraModifiers.Add(new PunchCameraModifier(player.Center, dashDir, 1.6f, 5f, 7, 900f, "FishSwarmDissolve"));
        }

        /// <summary>
        /// 化形退场：鳞片从身后弧域向体格收拢重织人形，核心先织、边缘后合，
        /// 配向内收缩环、体表甩水与一次胸口碎光
        /// </summary>
        public static void ReformBurst(Player player) {
            if (Main.dedServ) {
                return;
            }
            Rectangle box = player.Hitbox;
            Vector2 backDir = -player.velocity.SafeNormalize(Vector2.UnitY);
            for (int x = 0; x < GridX; x++) {
                for (int y = 0; y < GridY; y++) {
                    Vector2 cell = new(box.X + (x + 0.5f) * box.Width / (float)GridX
                        , box.Y + (y + 0.5f) * box.Height / (float)GridY);
                    Vector2 offset = cell - player.Center;
                    int edge = Math.Min(Math.Min(x, GridX - 1 - x), Math.Min(y, GridY - 1 - y));
                    //核心格先织(travel 短)，边缘格后合
                    int travel = 7 + (2 - Math.Min(edge, 2)) * 3 + Main.rand.Next(0, 3);
                    Vector2 spawn = cell + backDir.RotatedBy(Main.rand.NextFloat(-1.1f, 1.1f)) * Main.rand.NextFloat(30f, 64f);
                    Color col = Color.Lerp(Silver, Flow, Main.rand.NextFloat(0f, 0.55f));
                    PRTLoader.NewParticle<PRT_FishSwarmFlake>(spawn, Vector2.Zero, col, Main.rand.NextFloat(0.5f, 0.8f))
                        ?.ConfigureConverge(player, offset, travel);
                }
            }
            //收拢环：由外向内
            PRTLoader.NewParticle<PRT_DWave>(player.Center, Vector2.Zero, Flow, 0.62f)
                ?.Configure(new Vector2(1f, 0.7f), 0f, 0.08f, 14);
            //重织成形的体表甩水
            DropletFan(player.Center, -Vector2.UnitY, 8, 1.5f, 4f, 1.6f);
            //胸口一次碎光
            PRTLoader.NewParticle<PRT_Sparkle>(player.Center - new Vector2(0f, 6f), Vector2.Zero, Spec, 0.34f)
                ?.Configure(Flow, 10, 0.04f, 0.5f);
            SoundEngine.PlaySound(SoundID.Splash with { Volume = 0.5f, Pitch = 0.35f }, player.Center);
            SoundEngine.PlaySound(SoundID.SplashWeak with { Volume = 0.6f, Pitch = -0.1f }, player.Center);
        }

        /// <summary>突袭聚拢预告：向内收缩环 + 环带上向心的碎光</summary>
        public static void GatherCue(Player player) {
            if (Main.dedServ) {
                return;
            }
            PRTLoader.NewParticle<PRT_DWave>(player.Center, Vector2.Zero, Flow, 0.68f)
                ?.Configure(Vector2.One, 0f, 0.08f, 12);
            for (int i = 0; i < 8; i++) {
                float ang = MathHelper.TwoPi * i / 8f + Main.rand.NextFloat(0.3f);
                Vector2 off = ang.ToRotationVector2() * Main.rand.NextFloat(70f, 110f);
                PRTLoader.NewParticle<PRT_Sparkle>(player.Center + off
                    , -off.SafeNormalize(Vector2.Zero) * Main.rand.NextFloat(4f, 7f)
                    , Spec, Main.rand.NextFloat(0.26f, 0.4f))?.Configure(Flow, 12, 0.05f, 0.55f);
            }
        }

        /// <summary>突袭释放拍：沿突袭方向压扁的破水环 + 反向水珠喷洒 + 定向震 + 声层</summary>
        public static void ReleaseCue(Player player, Vector2 dir) {
            if (Main.dedServ) {
                return;
            }
            dir = dir.SafeNormalize(Vector2.UnitX);
            PRTLoader.NewParticle<PRT_DWave>(player.Center, Vector2.Zero, Flow, 0.16f)
                ?.Configure(new Vector2(1f, 0.42f), dir.ToRotation(), 1.05f, 15);
            DropletFan(player.Center, -dir, 9, 2.5f, 6f, 0.55f);
            Main.instance.CameraModifiers.Add(new PunchCameraModifier(player.Center, dir, 2.2f, 5f, 9, 900f, "FishSwarmSurge"));
            SoundEngine.PlaySound(SoundID.Splash with { Volume = 0.6f, Pitch = -0.15f }, player.Center);
            SoundEngine.PlaySound(CWRSound.Dash with { Volume = 0.55f, Pitch = 0.2f }, player.Center);
        }

        /// <summary>高速巡游的水纹涟漪（折射暗示，低频调用）</summary>
        public static void TravelRipple(Vector2 pos, Vector2 vel) {
            if (Main.dedServ) {
                return;
            }
            PRTLoader.NewParticle<PRT_DWave>(pos, Vector2.Zero, Flow * 0.55f, 0.10f)
                ?.Configure(new Vector2(1f, 0.5f), vel.ToRotation(), 0.5f, 16);
        }

        /// <summary>突袭鱼命中：小水珠扇 + 偶发碎光与轻水声</summary>
        public static void HitSplash(Vector2 pos, Vector2 vel) {
            if (Main.dedServ) {
                return;
            }
            DropletFan(pos, -vel, 3, 2f, 5f, 0.9f);
            if (Main.rand.NextBool(4)) {
                PRTLoader.NewParticle<PRT_Sparkle>(pos, Main.rand.NextVector2Circular(1.5f, 1.5f)
                    , Spec, Main.rand.NextFloat(0.24f, 0.36f))?.Configure(Flow, 9, 0.08f, 0.45f);
                SoundEngine.PlaySound(SoundID.SplashWeak with { Volume = 0.3f, Pitch = 0.4f }, pos);
            }
        }

        /// <summary>突袭鱼耗尽穿透而死：留两滴受重力的水珠，水失去了推它的鱼</summary>
        public static void SurgeFishDeath(Vector2 pos, Vector2 vel) {
            if (Main.dedServ) {
                return;
            }
            DropletFan(pos, -vel, 2, 1.5f, 3.5f, 0.8f);
        }

        /// <summary>水珠扇：沿 dir 锥形甩出的受重力水珠（复用刻心者液滴载体，水色配色）</summary>
        public static void DropletFan(Vector2 pos, Vector2 dir, int count, float speedMin, float speedMax, float spread) {
            if (Main.dedServ) {
                return;
            }
            dir = dir.SafeNormalize(-Vector2.UnitY);
            for (int i = 0; i < count; i++) {
                Vector2 vel = dir.RotatedByRandom(spread) * Main.rand.NextFloat(speedMin, speedMax);
                Color col = Color.Lerp(Spec, Flow, Main.rand.NextFloat(0.3f, 0.8f));
                PRTLoader.NewParticle<PRT_HeartcarverDroplet>(pos + Main.rand.NextVector2Circular(5f, 5f)
                    , vel, col, Main.rand.NextFloat(0.5f, 0.85f))
                    ?.Configure(Main.rand.Next(18, 30), 0.26f, 0.984f);
            }
        }
    }

    /// <summary>
    /// 换影鳞片：化形入/出的剪影碎块。<br/>
    /// 溶解模式：在体格位滞留 hold 帧后沿冲刺方向加速掠出（边缘先散），离体瞬间 ≤2 帧鳞闪；<br/>
    /// 收拢模式：从身后弧域向玩家体格位渐进逼近并跟随其滑行，贴合瞬间 ≤2 帧鳞闪后隐没
    /// </summary>
    internal class PRT_FishSwarmFlake : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "Extra_98";
        public override bool CanPool => true;

        private const int ModeDissolve = 0;
        private const int ModeConverge = 1;

        private int mode;
        private int age;
        private int hold;
        private Vector2 dartDir;
        private float curl;
        private Player owner;
        private Vector2 targetOffset;
        private int travel;
        private int arrivalAge;
        private Color initialColor;

        public PRT_FishSwarmFlake ConfigureDissolve(int holdFrames, Vector2 dir, float curlAmount) {
            mode = ModeDissolve;
            hold = holdFrames;
            dartDir = dir;
            curl = curlAmount;
            Lifetime = holdFrames + 20;
            initialColor = Color;
            Rotation = dir.ToRotation() + MathHelper.PiOver2;
            return this;
        }

        public PRT_FishSwarmFlake ConfigureConverge(Player player, Vector2 offset, int travelFrames) {
            mode = ModeConverge;
            owner = player;
            targetOffset = offset;
            travel = Math.Max(travelFrames, 4);
            Lifetime = travel + 8;
            initialColor = Color;
            return this;
        }

        public override void Reset() {
            base.Reset();
            mode = ModeDissolve;
            age = 0;
            hold = 0;
            dartDir = default;
            curl = 0f;
            owner = null;
            targetOffset = default;
            travel = 0;
            arrivalAge = 0;
            initialColor = default;
        }

        //收拢模式自管位置（Velocity 仅供拉伸绘制），避免加载器再叠加一次位移
        public override bool ShouldUpdatePosition() => mode == ModeDissolve;

        public override void AI() {
            age++;
            if (mode == ModeDissolve) {
                DissolveAI();
            }
            else {
                ConvergeAI();
            }
        }

        private void DissolveAI() {
            if (age <= hold) {
                //起飞前的松动微颤
                Velocity *= 0.82f;
                Velocity += Main.rand.NextVector2Circular(0.3f, 0.3f);
            }
            else {
                //离体后沿冲刺方向加速掠出，附轻微侧向卷曲
                int f = age - hold;
                Vector2 side = dartDir.RotatedBy(MathHelper.PiOver2) * ((float)Math.Sin(age * 0.45f) * curl * 6f);
                Velocity += dartDir * Math.Min(0.5f + f * 0.18f, 2.2f) + side;
                if (Velocity.Length() > 26f) {
                    Velocity = Velocity.SafeNormalize(Vector2.Zero) * 26f;
                }
                Rotation = Velocity.ToRotation() + MathHelper.PiOver2;
                Scale *= 0.985f;
            }
            float ft = MathHelper.Clamp((age - hold) / 18f, 0f, 1f);
            Color = Color.Lerp(initialColor, Color.Transparent, MathF.Pow(ft, 1.6f));
        }

        private void ConvergeAI() {
            if (owner == null || !owner.active) {
                Kill();
                return;
            }
            Vector2 desired = owner.Center + targetOffset;
            float t = MathHelper.Clamp(age / (float)travel, 0f, 1f);
            Vector2 old = Position;
            Position = Vector2.Lerp(Position, desired, 0.10f + t * t * 0.55f);
            Velocity = Position - old;    //仅供拉伸绘制
            if (Velocity.LengthSquared() > 0.2f) {
                Rotation = Velocity.ToRotation() + MathHelper.PiOver2;
            }
            if (arrivalAge == 0 && Vector2.DistanceSquared(Position, desired) < 49f) {
                arrivalAge = age;
            }
            //淡入 2 帧，贴合后迅速隐没
            float fadeIn = MathHelper.Clamp(age / 2f, 0f, 1f);
            float fadeOut = arrivalAge > 0 ? MathHelper.Clamp(1f - (age - arrivalAge) / 4f, 0f, 1f) : 1f;
            Color = initialColor * (fadeIn * fadeOut);
            if (arrivalAge > 0 && age - arrivalAge > 4) {
                Kill();
            }
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D tex = PRTLoader.PRT_IDToTexture[ID];
            Vector2 origin = tex.Size() * 0.5f;
            Vector2 pos = Position - Main.screenPosition;
            //随速度纵向拉伸：快则成线、慢则成鳞
            float stretch = MathHelper.Clamp(Velocity.Length() * 0.06f, 0f, 1.1f);
            Vector2 scale = new Vector2(0.30f * (1f - stretch * 0.3f), 0.5f * (1f + stretch * 1.8f)) * Scale;

            //双层同色窄叠：中心更实，读作鳞片而非光斑
            spriteBatch.Draw(tex, pos, null, Color, Rotation, origin, scale, SpriteEffects.None, 0f);
            spriteBatch.Draw(tex, pos, null, Color, Rotation, origin, scale * new Vector2(0.5f, 1f), SpriteEffects.None, 0f);

            //离体/贴合瞬间的 ≤2 帧鳞闪（A=0 加色观感，瞬现即灭）
            bool flash = mode == ModeDissolve
                ? age > hold && age <= hold + 2
                : arrivalAge > 0 && age - arrivalAge <= 2;
            if (flash) {
                Texture2D star = CWRAsset.StarGlow01?.Value;
                if (star != null) {
                    spriteBatch.Draw(star, pos, null, FishSwarmVFX.Spec with { A = 0 } * 0.9f
                        , Rotation, star.Size() * 0.5f, 0.06f + 0.06f * Scale, SpriteEffects.None, 0f);
                }
            }
            return false;
        }
    }
}
