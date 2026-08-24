using CalamityOverhaul.Common;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using Terraria;
using Terraria.Graphics.CameraModifiers;
using Terraria.ID;

namespace CalamityOverhaul.Content.LegendWeapon.HalibutLegend.FishSkills
{
    /// <summary>溺尸共置贴图（真 alpha 软斑，AlphaBlend 直绘安全）</summary>
    internal class FishZombieAssets
    {
        [VaultLoaden(CWRConstant.Masking + "Extra_98")]
        public static Asset<Texture2D> Blob { get; private set; }
    }

    /// <summary>
    /// 溺尸调色板与粒子协作，浸水腐肉的灰绿/青灰 + 浊水暗青 + 尸胀浊气的低饱和橄榄
    /// 全程 AlphaBlend 哑光零发光，禁鲜绿荧光
    /// </summary>
    internal static class FishZombieVFX
    {
        /// <summary>浸水腐肉（主体染色目标）</summary>
        public static readonly Color FleshSoak = new(98, 116, 100);
        /// <summary>腐肉暗部（残影/阴影）</summary>
        public static readonly Color FleshDark = new(56, 70, 60);
        /// <summary>浊水中间调</summary>
        public static readonly Color MurkMid = new(52, 88, 92);
        /// <summary>浊水深调（水斑/雾底）</summary>
        public static readonly Color MurkDeep = new(30, 52, 56);
        /// <summary>尸胀浊气亮调（低饱和橄榄，非毒液绿）</summary>
        public static readonly Color GasOlive = new(116, 118, 66);
        /// <summary>尸胀浊气暗调</summary>
        public static readonly Color GasDeep = new(74, 78, 46);
        /// <summary>稀释陈血（尸块喷点）</summary>
        public static readonly Color BloodOld = new(88, 52, 46);

        /// <summary>浊水随机取色，中间调与深调之间</summary>
        public static Color Murk() => Color.Lerp(MurkMid, MurkDeep, Main.rand.NextFloat());

        /// <summary>爆点定向震屏，尊重服务器配置</summary>
        public static void Punch(Vector2 pos) {
            if (Main.dedServ || !CWRClientConfig.Instance.ScreenVibration) {
                return;
            }
            //尸群会接连爆开，单发幅度压低防叠震过量
            Main.instance.CameraModifiers.Add(new PunchCameraModifier(
                pos, Main.rand.NextVector2Unit(), 3.2f, 13f, 6, 640f, "FishZombie"));
        }

        /// <summary>身上滴一滴浊水，vel 叠加宿主速度分量</summary>
        public static void Drip(Vector2 pos, Vector2 vel, float scale = 1f) {
            PRTLoader.NewParticle<PRT_FishZombieDrip>(pos, vel, Murk()
                , Main.rand.NextFloat(0.8f, 1.15f) * scale)?.Configure(Main.rand.Next(36, 54));
        }

        /// <summary>甩水，以 center 为心的环状水珠迸出（锁定预告拍/猛拔出土）</summary>
        public static void ShakeOff(Vector2 center, int count, float speed) {
            for (int i = 0; i < count; i++) {
                Vector2 vel = Main.rand.NextVector2Unit() * Main.rand.NextFloat(0.5f, 1f) * speed;
                vel.Y -= speed * 0.35f;//上抛偏置，落回时更有抛物感
                Drip(center + Main.rand.NextVector2Circular(10f, 16f), vel);
            }
        }

        /// <summary>
        /// 尸胀爆裂粒子套装，橄榄浊气外扩 + 暗青水雾 + 径向水珠喷洒
        /// 尸块 Gore 与音效由弹幕自持（需要 source 与服务端判定）
        /// </summary>
        public static void BloatBurst(Vector2 center) {
            //浊气云，大团慢胀慢散
            for (int i = 0; i < 5; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(1.6f, 1.2f) - new Vector2(0f, 0.35f);
                PRTLoader.NewParticle<PRT_FishZombieMurk>(center + Main.rand.NextVector2Circular(30f, 24f)
                    , vel, GasOlive, Main.rand.NextFloat(0.40f, 0.62f))
                    ?.Configure(Main.rand.Next(46, 72), GasOlive, GasDeep, 1.008f, 0.010f);
            }
            //浑浊水雾，更小更沉，往下坠
            for (int i = 0; i < 4; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(2.2f, 1.6f);
                PRTLoader.NewParticle<PRT_FishZombieMurk>(center + Main.rand.NextVector2Circular(16f, 14f)
                    , vel, MurkMid, Main.rand.NextFloat(0.26f, 0.42f))
                    ?.Configure(Main.rand.Next(30, 46), MurkMid, MurkDeep, 1.006f, -0.012f);
            }
            //径向水珠，飞出去落地铺一圈水斑
            for (int i = 0; i < 16; i++) {
                Vector2 vel = Main.rand.NextVector2Unit() * Main.rand.NextFloat(3f, 9f);
                vel.Y -= 1.6f;
                Drip(center + Main.rand.NextVector2Circular(8f, 8f), vel, Main.rand.NextFloat(0.9f, 1.3f));
            }
            //稀释陈血底噪，Dust 只做廉价填充
            for (int i = 0; i < 10; i++) {
                Dust blood = Dust.NewDustPerfect(center, DustID.Blood
                    , Main.rand.NextVector2CircularEdge(4.5f, 4.5f), 60, BloodOld, Main.rand.NextFloat(0.8f, 1.3f));
                blood.noGravity = false;
            }
        }
    }

    /// <summary>
    /// 溺尸浊水珠，受重力、随速拉伸的哑光液滴，触地转为 <see cref="PRT_FishZombieSplat"/> 水斑
    /// Extra_98 为真 alpha 贴图，AlphaBlend 直绘安全
    /// </summary>
    internal class PRT_FishZombieDrip : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "Extra_98";
        public override bool CanPool => true;

        private Color initColor;

        public PRT_FishZombieDrip Configure(int lifetime) {
            Lifetime = lifetime;
            initColor = Color;
            return this;
        }

        public override void Reset() {
            base.Reset();
            initColor = default;
        }

        public override void SetProperty() {
            if (Lifetime <= 0) {
                Lifetime = Main.rand.Next(36, 54);
                initColor = Color;
            }
        }

        public override void AI() {
            Velocity.X *= 0.985f;
            Velocity.Y += 0.34f;
            if (Velocity.Y > 13f) {
                Velocity.Y = 13f;
            }
            Rotation = Velocity.ToRotation() + MathHelper.PiOver2;

            float t = LifetimeCompletion;
            Color = Color.Lerp(initColor, FishZombieVFX.MurkDeep, t * 0.6f);
            Opacity = MathF.Min(Time / 3f, 1f) * (1f - MathF.Pow(t, 3f)) * 0.88f;

            //下落中触地，向上吸附到砖顶铺成水斑
            if (Velocity.Y > 0f && Collision.SolidCollision(Position - Vector2.One, 2, 2)) {
                int tx = (int)(Position.X / 16f);
                int ty = (int)(Position.Y / 16f);
                for (int i = 0; i < 3 && ty > 10; i++) {
                    Tile above = Framing.GetTileSafely(tx, ty - 1);
                    if (!(above.HasTile && Main.tileSolid[above.TileType])) {
                        break;
                    }
                    ty--;
                }
                Vector2 splatPos = new(Position.X, ty * 16f - 1f);
                PRTLoader.NewParticle<PRT_FishZombieSplat>(splatPos, Vector2.Zero
                    , initColor, Scale * Main.rand.NextFloat(0.8f, 1.2f))?.Configure(Main.rand.Next(38, 60));
                active = false;
            }
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D tex = PRTLoader.PRT_IDToTexture[ID];
            Vector2 pos = Position - Main.screenPosition;
            Vector2 origin = tex.Size() * 0.5f;
            //快则成线慢则成珠
            float stretch = MathHelper.Clamp(Velocity.Length() * 0.05f, 0f, 0.9f);
            Vector2 scale = new Vector2(0.26f * (1f - stretch * 0.35f), 0.5f * (1f + stretch * 1.6f)) * Scale;

            //吃环境光
            Color env = Lighting.GetColor(Position.ToTileCoordinates());
            Color lit = Color.Lerp(Color.MultiplyRGB(env), Color, 0.25f) * Opacity;

            //双层窄叠
            spriteBatch.Draw(tex, pos, null, lit, Rotation, origin, scale, SpriteEffects.None, 0f);
            spriteBatch.Draw(tex, pos, null, lit, Rotation, origin, scale * new Vector2(0.45f, 1f), SpriteEffects.None, 0f);
            return false;
        }
    }

    /// <summary>溺尸水斑，贴地横铺的暗青浅渍，微微漫开后干涸淡出</summary>
    internal class PRT_FishZombieSplat : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "Extra_98";
        public override bool CanPool => true;

        private Color initColor;

        public PRT_FishZombieSplat Configure(int lifetime) {
            Lifetime = lifetime;
            initColor = Color;
            return this;
        }

        public override void Reset() {
            base.Reset();
            initColor = default;
        }

        public override void SetProperty() {
            Velocity = Vector2.Zero;
            Rotation = 0f;
            if (Lifetime <= 0) {
                Lifetime = Main.rand.Next(38, 60);
                initColor = Color;
            }
        }

        public override bool ShouldUpdatePosition() => false;

        public override void AI() {
            float t = LifetimeCompletion;
            Color = Color.Lerp(initColor, FishZombieVFX.MurkDeep, t);
            //快进慢出，落点即刻显形，随后缓慢干涸
            float tail = MathHelper.Clamp((t - 0.25f) / 0.75f, 0f, 1f);
            Opacity = MathF.Min(Time / 4f, 1f) * (1f - tail * tail) * 0.5f;
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D tex = PRTLoader.PRT_IDToTexture[ID];
            Vector2 pos = Position - Main.screenPosition;
            Vector2 origin = tex.Size() * 0.5f;
            //横铺扁渍，随时间轻微漫开
            float spread = 1f + LifetimeCompletion * 0.45f;
            Vector2 scale = new Vector2(0.62f * spread, 0.11f) * Scale;

            Color env = Lighting.GetColor(Position.ToTileCoordinates());
            Color lit = Color.Lerp(Color.MultiplyRGB(env), Color, 0.25f) * Opacity;

            spriteBatch.Draw(tex, pos, null, lit, 0f, origin, scale, SpriteEffects.None, 0f);
            spriteBatch.Draw(tex, pos, null, lit * 0.6f, 0f, origin, scale * new Vector2(1.6f, 0.7f), SpriteEffects.None, 0f);
            return false;
        }
    }

    /// <summary>
    /// 溺尸浊雾团，Fog 随机取向 AlphaBlend 染色的哑光雾
    /// 浊气（上飘）与水雾（下沉）靠 Configure 的浮力符号分身
    /// </summary>
    internal class PRT_FishZombieMurk : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "Fog";
        public override bool CanPool => true;

        private float spin;
        private Color hotColor;
        private Color coldColor;
        private float expandRate;
        private float buoyancy;

        public PRT_FishZombieMurk Configure(int lifetime, Color hot, Color cold, float expand = 1.010f, float rise = 0.008f) {
            Lifetime = lifetime;
            hotColor = hot;
            coldColor = cold;
            expandRate = expand;
            buoyancy = rise;
            return this;
        }

        public override void Reset() {
            base.Reset();
            spin = 0f;
            hotColor = coldColor = default;
            expandRate = 1.010f;
            buoyancy = 0.008f;
        }

        public override void SetProperty() {
            spin = Main.rand.NextFloat(0.006f, 0.016f) * (Main.rand.NextBool() ? 1f : -1f);
            Rotation = Main.rand.NextFloat(MathHelper.TwoPi);
            if (Lifetime <= 0) {
                Lifetime = Main.rand.Next(30, 46);
                hotColor = FishZombieVFX.MurkMid;
                coldColor = FishZombieVFX.MurkDeep;
            }
        }

        public override void AI() {
            float t = LifetimeCompletion;
            Scale *= expandRate;
            Rotation += spin;
            Velocity *= 0.93f;
            Velocity.Y -= buoyancy;

            Color = Color.Lerp(hotColor, coldColor, MathF.Min(1f, t * 1.4f));
            //峰值压低
            float tail = MathHelper.Clamp((t - 0.30f) / 0.65f, 0f, 1f);
            Opacity = MathF.Min(t / 0.10f, 1f) * (1f - tail * tail * (3f - 2f * tail)) * 0.44f;
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D tex = PRTLoader.PRT_IDToTexture[ID];
            Color env = Lighting.GetColor(Position.ToTileCoordinates());
            Color lit = Color.Lerp(Color.MultiplyRGB(env), Color, 0.30f) * Opacity;

            spriteBatch.Draw(tex, Position - Main.screenPosition, null, lit, Rotation
                , tex.Size() * 0.5f, Scale * 0.6f, SpriteEffects.None, 0f);
            return false;
        }
    }
}
