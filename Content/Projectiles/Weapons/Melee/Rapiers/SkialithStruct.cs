using Microsoft.Xna.Framework;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Melee.Rapiers
{
    public struct SkialithStruct
    {
        public Vector2 pos;
        public Vector2 ver;
        public float rot;
        public int time;

        public SkialithStruct(Vector2 pos, Vector2 ver, float rot, int time) {
            this.pos = pos;
            this.ver = ver;
            this.rot = rot;
            this.time = time;
        }
    }
}
