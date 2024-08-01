using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Dusts
{
    internal class SparkDust : ModDust
    {
        internal int formIndex;

        internal Color InitialColor;

        public bool AffectedByGravity;

        public int Time;

        public int Lifetime;

        public float LifetimeCompletion {
            get {
                return Lifetime == 0 ? 0f : Time / (float)Lifetime;
            }
        }

        public override string Texture => "CalamityMod/Projectiles/StarProj";

        public override void OnSpawn(Dust dust) {
            Lifetime = Main.rand.Next(31, 73);
            dust.scale = Main.rand.NextFloat(0.5f, 0.9f);
            InitialColor = Main.rand.NextBool(7) ? Color.Aqua : Color.Fuchsia;
            dust.velocity = new Vector2(0f, Main.rand.NextFloat(-5f, 5f));
            AffectedByGravity = false;
        }

        public override bool Update(Dust dust) {
            NPC formes = CWRUtils.GetNPCInstance(dust.alpha);
            if (formes == null) {
                dust.active = false;
                return false;
            }
            Lifetime--;
            dust.scale *= 0.95f;
            dust.color = InitialColor;
            dust.velocity *= 0.95f;
            if (dust.velocity.Length() < 12f && AffectedByGravity) {
                dust.velocity.X *= 0.94f;
                dust.velocity.Y += 0.25f;
            }
            if (Lifetime <= 0) {
                dust.active = false;
            }
            dust.rotation = dust.velocity.ToRotation() + MathHelper.PiOver2;
            dust.position += formes.velocity;
            dust.position += dust.velocity;
            return false;
        }

        public override bool PreDraw(Dust dust) {
            Vector2 dustSlp = new Vector2(0.5f, 1.6f) * dust.scale;
            Texture2D texture = CWRUtils.GetT2DValue(Texture);
            Main.spriteBatch.Draw(texture, dust.position - Main.screenPosition, null, dust.color, dust.rotation, texture.Size() * 0.5f, dustSlp, 0, 0f);
            Main.spriteBatch.Draw(texture, dust.position - Main.screenPosition, null, dust.color, dust.rotation, texture.Size() * 0.5f, dustSlp * new Vector2(0.45f, 1f), 0, 0f);
            return false;
        }
    }
}
