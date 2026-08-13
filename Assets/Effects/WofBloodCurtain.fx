// ============================================================================
//WofBloodCurtain.fx 后方血幕(绯红大迁徙)
//世界锚定quad：垂落血帘+前缘噪声撕裂热线+深处压黑
//坐标全笛卡尔；预乘输出 AlphaBlend
// ============================================================================

sampler uImage0 : register(s0);
sampler uImage1 : register(s1); //PerlinNoise 512

float4 uWorldRect;   //quad世界矩形 x,y,w,h
float uEdgeX;        //血幕前缘世界X
float uDir;          //前缘朝向口袋的方向 ±1(深处在 -uDir 侧)
float uTime;
float uIntensity;    //0~1 展开强度

float4 PixelShaderFunction(float2 coords : TEXCOORD0, float4 vertexColor : COLOR0) : COLOR0
{
    float2 worldPos = uWorldRect.xy + coords * uWorldRect.zw;

    //前缘撕裂：边界随高度被噪声撕开
    float fray = (tex2D(uImage1, float2(worldPos.y * 0.0046 + uTime * 0.07, uTime * 0.11)).r - 0.5) * 74.0;
    //深度 px：0=前缘，正值进入幕内
    float depth = (uEdgeX + fray - worldPos.x) * uDir;

    //前缘热线高斯在 -70px 处已衰减到 0.02%，此处截断不产生可见台阶
    if (depth < -70.0)
    {
        return float4(0, 0, 0, 0);
    }

    //=== 垂落血帘：纵向流动噪声柱 ===
    float strand1 = tex2D(uImage1, float2(worldPos.x * 0.0062, worldPos.y * 0.0016 - uTime * 0.34)).r;
    float strand2 = tex2D(uImage1, float2(worldPos.x * 0.013 + 0.37, worldPos.y * 0.004 - uTime * 0.62)).g;
    float strands = strand1 * 0.6 + strand2 * 0.4;

    //=== 分层 ===
    //前缘热线 0~54px
    float rim = exp(-pow(depth * 0.042, 2.0));
    //幕体 54~430px 渐浓
    float body = smoothstep(10.0, 200.0, depth);
    //深处压黑
    float deep = smoothstep(180.0, 430.0, depth);

    //心跳明暗：幕体随呼吸微涨
    float breath = 0.9 + 0.1 * sin(uTime * 2.7);

    //=== 调色 ===
    float3 cRim  = float3(0.96, 0.22, 0.11);
    float3 cBody = float3(0.30, 0.03, 0.05);
    float3 cDeep = float3(0.05, 0.006, 0.012);

    float3 color = cBody * (0.55 + strands * 0.7) * breath;
    color = lerp(color, cDeep, deep);
    color += cRim * rim * (0.7 + strands * 0.5);

    float alpha = saturate(rim * 0.7 + body * (0.72 + strands * 0.2) + deep * 0.2);
    alpha = min(alpha, 0.96);

    //纵向端点包络：quad上下边前被血丝噪声撕散归零，不暴露水平切边
    float vNorm = coords.y;
    float vTear = strand2 * 0.05;
    float vFade = smoothstep(0.0, 0.075 + vTear, vNorm) * smoothstep(1.0, 0.925 - vTear, vNorm);
    //深侧退场：背缘(560px)前雾化归零，不暴露垂直切边
    float backFade = smoothstep(560.0, 410.0, depth);
    alpha *= vFade * backFade;

    color *= uIntensity;
    alpha *= uIntensity;
    //预乘输出
    return float4(color * alpha, alpha) * vertexColor.a;
}

technique Technique1
{
    pass WofBloodCurtainPass
    {
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
