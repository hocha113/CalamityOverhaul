// ============================================================================
// DeductDraw.fx 矩形区域抠除绘制
// 矩形内透明，其余采样 uImage0；ps_3_0
// ============================================================================

sampler uImage0 : register(s0);
float2 topLeft;       //矩形左上(像素)
float width;          //矩形宽(像素)
float height;         //矩形高(像素)
float4 drawColor;     //区外乘色
float2 textureSize;   //纹理像素尺寸

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
        PixelShader = compile ps_3_0 Function();
    }
}
