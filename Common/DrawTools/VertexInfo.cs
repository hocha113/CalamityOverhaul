using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CalamityOverhaul.Common.DrawTools
{
    public struct CustomVertexInfo : IVertexType
    {
        public Vector2 Position;
        public Color Color;
        public Vector3 TexCoord;

        private static VertexDeclaration _vertexDeclaration = new VertexDeclaration(new VertexElement[3]
        {
                new VertexElement(0,VertexElementFormat.Vector2,VertexElementUsage.Position,0),
                new VertexElement(8,VertexElementFormat.Color,VertexElementUsage.Color,0),
                new VertexElement(12,VertexElementFormat.Vector3,VertexElementUsage.TextureCoordinate,0)
        });

        public CustomVertexInfo(Vector2 position, Color color, Vector3 texCoord) {
            this.Position = position;
            this.Color = color;
            this.TexCoord = texCoord;
        }

        public VertexDeclaration VertexDeclaration {
            get { return _vertexDeclaration; }
        }
    }

    public struct VertexInfo : IVertexType
    {
        public Vector2 Position;
        public Vector3 TexCoord;

        public VertexInfo(Vector2 position, Vector3 texCoord) {
            Position = position;
            TexCoord = texCoord;
        }

        public VertexDeclaration VertexDeclaration {
            get { return _vertexDeclaration; }
        }

        private static VertexDeclaration _vertexDeclaration = new VertexDeclaration(new VertexElement[]
        {
                new VertexElement(0,VertexElementFormat.Vector2,VertexElementUsage.Position,0),
                new VertexElement(8,VertexElementFormat.Color,VertexElementUsage.Color,0),
        });
    }
}
