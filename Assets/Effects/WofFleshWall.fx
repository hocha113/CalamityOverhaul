// ============================================================================
//WofFleshWall.fx 血肉墙体覆膜
//世界锚定quad：墙条带蠕动血肉+面缘渗血热线+身后拖曳肉髓
//坐标全笛卡尔无极角；预乘输出 AlphaBlend
// ============================================================================

sampler uImage0 : register(s0);
sampler uImage1 : register(s1); //PerlinNoise 512

float4 uWorldRect;   //quad世界矩形 x,y,w,h
float uFaceX;        //墙面前缘世界X
float uDir;          //推进方向 ±1
float uTop;          //墙域上缘世界Y
float uBottom;       //墙域下缘世界Y
float uTime;
float uFlush;        //全墙潮红 0~1(心跳/蓄力)
float uCharge;       //蓄力进度 0~1(突进前缘白热)
float uOpacity;

float4 PixelShaderFunction(float2 coords : TEXCOORD0, float4 vertexColor : COLOR0) : COLOR0
{
    float2 worldPos = uWorldRect.xy + coords * uWorldRect.zw;
    //面后深度 px：0=墙面前缘，正值向墙体内
    float behind = (uFaceX - worldPos.x) * uDir;
    float ySpan = max(uBottom - uTop, 1.0);
    float yNorm = saturate((worldPos.y - uTop) / ySpan);

    //上下缘渐隐(与地形交接)
    float edgeFade = smoothstep(0.0, 0.055, yNorm) * smoothstep(1.0, 0.945, yNorm);

    //=== 蠕动场：双八度噪声，整体向上+向前爬行 ===
    float2 crawl1 = worldPos * 0.0042 + float2(uDir * uTime * 0.02, -uTime * 0.055);
    float2 crawl2 = worldPos * 0.0105 + float2(-uDir * uTime * 0.012, -uTime * 0.11);
    float n1 = tex2D(uImage1, crawl1).r;
    float n2 = tex2D(uImage1, crawl2).g;
    float meat = n1 * 0.62 + n2 * 0.38;

    //=== 血管网：窄脊线 ===
    float vein = 1.0 - smoothstep(0.0, 0.11, abs(n1 - 0.5));
    float veinSlow = 1.0 - smoothstep(0.0, 0.16, abs(n2 - 0.52));

    //=== 心跳波：亮带自下而上爬过墙面，随潮红增强 ===
    float beatPhase = frac(yNorm * 1.6 + uTime * 0.42);
    float beat = exp(-pow((beatPhase - 0.5) * 5.2, 2.0)) * uFlush;

    //=== 分区包络 ===
    //墙条带 0..250px
    float wallZone = smoothstep(-24.0, 6.0, behind) * (1.0 - smoothstep(210.0, 265.0, behind));
    //拖曳肉髓 250..1150px
    float trailZone = smoothstep(215.0, 300.0, behind) * (1.0 - smoothstep(650.0, 1150.0, behind));
    //面缘热线 -34..30px，被噪声撕成参差唇缘
    float ripple = (n2 - 0.5) * 26.0;
    float rim = exp(-pow((behind + ripple * 0.4) * 0.062, 2.0));

    //=== 调色 ===
    float3 cMeatDark = float3(0.23, 0.028, 0.045);
    float3 cMeatMid  = float3(0.44, 0.065, 0.075);
    float3 cVein     = float3(0.62, 0.10, 0.09);
    float3 cHot      = float3(0.95, 0.26, 0.14);
    float3 cWhite    = float3(1.0, 0.86, 0.70);

    //墙条带：肉底+血管+心跳
    float3 wallCol = lerp(cMeatDark, cMeatMid, meat);
    wallCol = lerp(wallCol, cVein, vein * 0.55 + veinSlow * 0.25);
    wallCol += cHot * beat * 0.8;
    wallCol *= (0.85 + 0.3 * uFlush);
    float wallAlpha = wallZone * (0.34 + 0.20 * uFlush + beat * 0.22);

    //拖曳肉髓：深暗翻搅的体量，远端消散
    float churn = tex2D(uImage1, worldPos * 0.0028 + float2(uDir * uTime * 0.03, -uTime * 0.02)).b;
    float3 trailCol = lerp(cMeatDark * 0.55, cMeatDark, churn);
    trailCol += cVein * veinSlow * 0.2;
    float trailAlpha = trailZone * (0.30 + churn * 0.22) * (0.75 + 0.25 * uFlush);

    //面缘：渗血热线，蓄力时白热
    float3 rimCol = lerp(cHot, cWhite, uCharge * uCharge);
    float rimAlpha = rim * (0.34 + 0.42 * uFlush + 0.9 * uCharge);
    //面缘垂淌血丝
    float dripN = tex2D(uImage1, float2(worldPos.x * 0.012, worldPos.y * 0.0035 - uTime * 0.24)).r;
    float drip = pow(saturate(dripN * 1.5 - 0.55), 2.0) * rim;
    rimCol += cHot * drip;
    rimAlpha += drip * 0.5;

    //=== 合成(预乘) ===
    float3 color = wallCol * wallAlpha + trailCol * trailAlpha + rimCol * rimAlpha;
    float alpha = saturate(wallAlpha + trailAlpha + rimAlpha);
    color *= edgeFade * uOpacity;
    alpha *= edgeFade * uOpacity;
    return float4(color, alpha) * vertexColor;
}

technique Technique1
{
    pass WofFleshWallPass
    {
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
