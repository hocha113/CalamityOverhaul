// ============================================================================
//ToriiDissolve.fx 鸟居退场合成：对 Models3D 层 RT 做世界空间噪声溶解
//uImage0 = 层 RT（预乘 alpha 风格），uNoise = Perlin 灰度
//溶解阈值与入土裁剪都在世界坐标下计算：花纹钉在鸟居上而不是屏幕上，
//uGroundY 之下的像素视为已沉入土层，免费获得正确的埋没遮挡
// ============================================================================

sampler uImage0 : register(s0);
sampler uNoise : register(s1);

float uProgress;    //0=完好 1=溶尽
float2 uWorldScale; //uv→世界坐标：world = uv * uWorldScale + uWorldOffset
float2 uWorldOffset;
float uGroundY;     //地面线（世界Y）
float3 uEdgeColor;  //溶解前沿的绯红
float4 uBounds;     //鸟居包围盒（uv 空间 minXY/maxXY），盒外像素原样通过

float4 PS(float2 coords : TEXCOORD0, float4 vertexColor : COLOR0) : COLOR0
{
    float4 src = tex2D(uImage0, coords);
    if (coords.x < uBounds.x || coords.y < uBounds.y
        || coords.x > uBounds.z || coords.y > uBounds.w)
    {
        return src;
    }
    if (src.a <= 0.002)
    {
        return src;
    }

    float2 world = coords * uWorldScale + uWorldOffset;

    //入土裁剪：地面线附近 4px 软过渡
    float ground = 1.0 - smoothstep(uGroundY - 1.0, uGroundY + 3.0, world.y);
    if (ground <= 0.002)
    {
        return float4(0, 0, 0, 0);
    }

    //世界空间双倍频噪声，镜头移动时溶解花纹跟着鸟居走
    float n = tex2D(uNoise, world / 384.0).r * 0.68
            + tex2D(uNoise, world / 132.0 + float2(0.37, 0.61)).r * 0.32;

    //阈值重映射：0/1 两端留出"完好/溶尽"的余量，避免起步即缺块
    float t = lerp(-0.18, 1.12, uProgress);
    float keep = smoothstep(t, t + 0.09, n);
    float edge = smoothstep(t - 0.12, t, n) * (1.0 - keep);

    float vis = keep * ground;
    float glow = edge * ground * src.a;
    float3 rgb = src.rgb * vis + uEdgeColor * glow;
    float a = saturate(src.a * vis + glow * 0.85);
    return float4(rgb, a);
}

technique DissolveTech
{
    pass P0
    {
        PixelShader = compile ps_3_0 PS();
    }
}
