// ============================================================================
//FishScorpioNado.fx 沙蝎的沙龙卷漏斗（单quad，SpriteBatch Immediate直绘）
//uv.x：0..1 横跨漏斗；uv.y：0=顶 → 1=地面
//s0 绑定 PerlinNoise（LinearWrap），全笛卡尔坐标无极角，接缝协议天然合规
//
//层次：三层异相旋带（层2逆向）叠出圆柱体积，横向滚动读作自旋；
//暗外圈/饱和中层/零白芯，哑光沙色全程无发光；顶端与外缘被噪声撕散，
//uPower 衰减时旋带失能变糊（风力耗尽），uGrow 从地面向上生长成形。
//预乘 alpha，配 BlendState.AlphaBlend
// ============================================================================

sampler uImage0 : register(s0);

float uTime;   //秒
float uSeed;   //实例随机相位
float uPower;  //风力0..1，衰减时旋带失能
float uGrow;   //出生包络0..1，从地面向上生长
float uFade;   //整体不透明度

//暖沙黄哑光色谱：亮沙/主沙/暗沙/沙影，最亮不过亮沙
static const float3 ColLight = float3(0.898, 0.792, 0.580);
static const float3 ColMid = float3(0.769, 0.647, 0.427);
static const float3 ColDark = float3(0.545, 0.427, 0.263);
static const float3 ColDeep = float3(0.376, 0.290, 0.180);

float4 PixelShaderFunction(float2 uv : TEXCOORD0) : COLOR0
{
    float vUp = 1.0 - uv.y; //0地面 → 1顶

    //出生包络：漏斗从地面向上长出来
    float vv = vUp / max(uGrow, 0.05);
    if (vv > 1.05)
        return float4(0, 0, 0, 0);

    //轴线蛇摆：噪声沿高度缓慢扭动中轴
    float sway = (tex2D(uImage0, float2(uSeed * 0.31, vv * 0.35 - uTime * 0.22)).r - 0.5)
               * (0.10 + 0.16 * vv) * uPower;

    //漏斗半宽：地窄顶宽
    float halfW = (0.13 + 0.30 * pow(vv, 1.25)) * (0.55 + 0.45 * saturate(uGrow));
    float x = (uv.x - 0.5 - sway) / halfW; //-1..1 跨漏斗
    float ax = abs(x);
    if (ax > 1.3)
        return float4(0, 0, 0, 0);

    //三层旋带：横向滚动=自旋；层1/层3同向异速，层2逆向，相位错开叠体积
    float spin = uTime * (2.2 + frac(uSeed) * 0.5) * (0.3 + 0.7 * uPower);
    float tw = vv * 1.8; //扭拧：带纹沿高度错相
    float b1 = tex2D(uImage0, float2(x * 0.40 + spin + tw + uSeed * 3.0, vv * 0.85 - uTime * 0.5)).r;
    float b2 = tex2D(uImage0, float2(x * 0.33 - spin * 0.7 + tw * 0.8 + uSeed * 7.0, vv * 1.25 - uTime * 0.36)).r;
    float b3 = tex2D(uImage0, float2(x * 0.52 + spin * 1.35 + tw * 1.2 + uSeed * 11.0, vv * 0.6 - uTime * 0.66)).r;

    //侧缘与顶端撕散：噪声阈值，禁平滑收口
    float edgeNoise = tex2D(uImage0, float2(x * 0.8 + uSeed * 5.0 + spin * 0.5, vv * 1.6 - uTime * 0.75)).r;
    float silho = 1.0 - smoothstep(0.5, 1.0 + 0.3 * edgeNoise, ax);
    float topTear = 1.0 - smoothstep(0.66, 1.04, vv + (edgeNoise - 0.5) * 0.4);

    //旋带叠合成沙密度：风力衰减时带纹失能变糊
    float density = saturate(b1 * 0.5 + b2 * 0.42 + b3 * 0.3 - 0.24);
    density = density * (0.5 + 0.5 * uPower) + 0.2;

    //圆柱体积明暗：中央厚、侧缘薄而暗
    float lat = sqrt(saturate(1.0 - ax * ax));
    float3 col = lerp(ColDark, ColMid, lat);
    //受光带：亮沙只跟着最强的带纹走，永不到白
    col = lerp(col, ColLight, saturate(b1 * 1.25 - 0.6) * lat * 0.7);
    //暗外圈压边
    col = lerp(ColDeep, col, smoothstep(1.08, 0.55, ax));
    //底部接地处沉暗
    col *= lerp(0.8, 1.0, smoothstep(0.0, 0.22, vv));

    //细颗粒：高频噪声乘出哑光沙面
    float grain = tex2D(uImage0, uv * float2(5.5, 4.0) + float2(spin * 0.7, -uTime * 1.5)).r;
    col *= 0.86 + grain * 0.26;

    float alpha = silho * topTear * density * (0.42 + 0.36 * lat) * uFade;
    return float4(col * alpha, alpha);
}

technique Technique1
{
    pass P0
    {
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
