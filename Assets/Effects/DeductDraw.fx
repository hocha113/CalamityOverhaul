sampler uImage0 : register(s0);
float2 topLeft;
float width;
float height;
float4 drawColor;
float2 textureSize; //传入的纹理尺寸（像素单位）

float4 Function(float2 coords : TEXCOORD0) : COLOR0
{
    //将0到1的纹理坐标转换为像素坐标
    float2 pixelCoords = coords * textureSize;

    //判断当前像素坐标是否在矩形范围内
    bool isInsideRect = pixelCoords.x >= topLeft.x && pixelCoords.x <= topLeft.x + width &&
                        pixelCoords.y >= topLeft.y && pixelCoords.y <= topLeft.y + height;

    //如果在矩形内，返回透明；否则返回原颜色
    if (isInsideRect)
    {
        return float4(0, 0, 0, 0); //返回完全透明
    }
    return tex2D(uImage0, coords) * drawColor; //返回原纹理颜色
}

technique Technique1
{
    pass DeductDrawPass
    {
        PixelShader = compile ps_2_0 Function();
    }
}
