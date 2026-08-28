// ============================================================================
// QueenPrismCrystal.fx 皇后棱晶体
// uMode: 0=棱晶节点(六面宝石,已退役——节点/囚茧改走专用 QueenPrismGem.fx)
//        1=水晶尖塔(竖直) 2=水晶吊灯(倒悬晶簇)
// 材质=圣光凝胶水晶：半透明体+分面明暗+内部折射闪点+亮缘
// 分面用符号位组合，无 atan2；预乘输出+AlphaBlend
// ============================================================================

float4x4 transformMatrix;
float uTime;
float uMode;      //0宝石 1尖塔 2吊灯
float uGrow;      //物化进度 0~1
float uShatter;   //碎裂进度 0~1(节点=受损度)
float uCharge;    //蓄能 0~1(吊灯用)
float uHueSeed;   //色相种子
float seed;       //实例种子

// 噪声固定 s1：sampler_state 自动分配会落 s0，图元路径今日侥幸、批次路径必坏；
// C# 侧须在 pass.Apply 前显式 Textures[1]=PerlinNoise + SamplerStates[1]=LinearWrap
sampler noiseSamp : register(s1);

struct VSInput
{
    float4 Position : POSITION0;
    float2 TexCoords : TEXCOORD0;
    float4 Color : COLOR0;
};

struct PSInput
{
    float4 Position : POSITION0;
    float4 Color : COLOR0;
    float2 TexCoords : TEXCOORD0;
};

PSInput VertexShaderFunction(VSInput v)
{
    PSInput o;
    o.Position = mul(v.Position, transformMatrix);
    o.Color = v.Color;
    o.TexCoords = v.TexCoords;
    return o;
}

float3 PrismHue(float t)
{
    return 0.72 + 0.28 * cos(6.28318 * (t + float3(0.0, 0.35, 0.68)));
}

//宝石轮廓SDF：竖长菱形(上尖下尖)，d<0在内部
float GemSDF(float2 p)
{
    //上尖较缓下尖较锐的鸢形
    float upper = p.y < 0.0 ? 1.35 : 1.0;
    float d = abs(p.x) * 1.55 + abs(p.y) * upper - 0.86;
    return d;
}

//尖塔轮廓：y 0顶→1底，半宽随高度收窄
float SpireSDF(float2 uv, float edgeNoise)
{
    float halfW = lerp(0.045, 0.34, pow(uv.y, 0.72));
    halfW += (edgeNoise - 0.5) * 0.06 * uv.y; //侧缘参差
    return abs(uv.x - 0.5) - halfW;
}

float4 PixelShaderFunction(PSInput input) : COLOR0
{
    float2 uv = input.TexCoords;
    float2 p = (uv - 0.5) * 2.0;   //-1~1 居中坐标

    float inside = 0.0;      //体内掩码
    float edgeBand = 0.0;    //亮缘带
    float facet = 0.0;       //分面明暗
    float dist = 0.0;        //SDF值(负=内)

    //边缘参差噪声(刚体坐标，无极角)
    float edgeNoise = tex2D(noiseSamp, float2(uv.x * 2.3 + seed * 7.0, uv.y * 2.3 + seed * 3.0)).r;

    if (uMode < 0.5) {
        //================= 宝石 =================
        //物化：从中心长出
        float2 pg = p / max(uGrow, 0.02);
        dist = GemSDF(pg);
        inside = smoothstep(0.03, -0.04, dist);
        edgeBand = smoothstep(0.055, 0.0, abs(dist)) * inside;

        //分面：符号位组合出6分面，每面固定亮度差
        float fx = step(0.0, pg.x);
        float fy = step(0.0, pg.y);
        float fd = step(abs(pg.x) * 1.9, abs(pg.y));
        float facetId = fx + fy * 2.0 + fd * 4.0;
        facet = frac(facetId * 0.318 + seed * 5.0) * 0.42;
        //面间随时间缓慢换亮(晶体转光)
        facet += 0.16 * sin(uTime * 1.7 + facetId * 2.1 + seed * 9.0);
    }
    else if (uMode < 1.5) {
        //================= 尖塔 =================
        dist = SpireSDF(uv, edgeNoise);
        inside = smoothstep(0.02, -0.02, dist);
        edgeBand = smoothstep(0.035, 0.0, abs(dist)) * inside;
        //底部渐入地面
        inside *= smoothstep(1.0, 0.9, uv.y);
        //左右两面+中脊
        float sideFacet = step(0.5, uv.x);
        facet = sideFacet * 0.3 + 0.12 * sin(uTime * 1.4 + sideFacet * 3.0 + seed * 8.0);
        float ridge = smoothstep(0.06, 0.0, abs(uv.x - 0.5)) * 0.5;
        facet += ridge;
    }
    else {
        //================= 吊灯(倒悬三棱簇) =================
        //中央长棱+两侧短棱，均为下尖
        float2 pc = p / max(uGrow, 0.02);
        //中棱
        float dc = abs(pc.x) * 1.7 + abs(pc.y + 0.15) * (pc.y > -0.15 ? 0.85 : 2.6) - 0.72;
        //侧棱(镜像对折，一次算两根)
        float2 ps = float2(abs(pc.x) - 0.52, pc.y + 0.3);
        float dsd = abs(ps.x) * 2.2 + abs(ps.y) * (ps.y > 0.0 ? 1.2 : 3.2) - 0.5;
        dist = min(dc, dsd);
        inside = smoothstep(0.03, -0.04, dist);
        edgeBand = smoothstep(0.06, 0.0, abs(dist)) * inside;
        //棱区分面
        float branch = step(dsd, dc);
        facet = branch * 0.26 + 0.14 * sin(uTime * 1.9 + branch * 2.4 + seed * 6.0);
    }

    if (inside < 0.003) {
        return float4(0.0, 0.0, 0.0, 0.0);
    }

    //=========================================================
    //内部折射闪点：刚体旋转坐标下的噪声阈值
    //=========================================================
    float ca = cos(uTime * 0.21 + seed * 6.0);
    float sa = sin(uTime * 0.21 + seed * 6.0);
    float2 pr = float2(p.x * ca - p.y * sa, p.x * sa + p.y * ca);
    float sparkNoise = tex2D(noiseSamp, pr * 0.9 + float2(seed * 11.0, uTime * 0.05)).r;
    float sparkle = smoothstep(0.80, 0.95, sparkNoise) * 1.3;

    //缓慢体内流光(凝胶感)
    float flow = tex2D(noiseSamp, float2(uv.x * 1.2 + uTime * 0.06, uv.y * 1.2 - uTime * 0.04) + seed).r;

    //=========================================================
    //碎裂：裂纹亮线+体积蚀散
    //=========================================================
    float crackNoise = tex2D(noiseSamp, uv * 3.1 + seed * 4.0).r;
    float crackLine = smoothstep(0.09, 0.0, abs(crackNoise - 0.5)) * smoothstep(0.0, 0.35, uShatter);
    //蚀散：噪声阈值随uShatter咬掉体积(自边缘先碎)
    float erode = smoothstep(uShatter * 1.15 - 0.15, uShatter * 1.15 + 0.1, crackNoise + 0.25);
    inside *= lerp(1.0, erode, step(0.02, uShatter));

    //=========================================================
    //调色合成(半透明水晶体)
    //=========================================================
    float3 hue = PrismHue(uHueSeed);
    float3 hueDeep = PrismHue(uHueSeed + 0.5) * 0.55;
    float3 cWhite = float3(1.0, 0.98, 0.94);

    float3 color = hueDeep * 0.5;                    //深层底色
    color += hue * (0.35 + flow * 0.3);              //体色流光
    color += hue * facet;                            //分面
    color += cWhite * sparkle;                       //折射闪点
    color += cWhite * edgeBand * 0.9;                //亮缘
    color += cWhite * crackLine * 1.1;               //裂纹白线
    color += hue * uCharge * (0.5 + 0.5 * sin(uTime * 9.0 + seed * 20.0)); //蓄能脉动

    //透明度：体半透+缘实+闪点提亮
    float alpha = inside * (0.52 + facet * 0.3 + edgeBand * 0.45 + sparkle * 0.3 + uCharge * 0.25);
    alpha = saturate(alpha);

    //物化前沿闪光(径向生长环，只对宝石/吊灯有意义，尖塔的生长由几何高度承担)
    float radialModes = 1.0 - step(0.5, uMode) * (1.0 - step(1.5, uMode));
    float growEdge = (1.0 - smoothstep(0.0, 0.25, abs(uGrow - length(p)))) * (1.0 - uGrow) * 0.6 * radialModes;
    color += cWhite * growEdge;
    alpha = saturate(alpha + growEdge * 0.4);

    //预乘输出
    return float4(color * alpha, alpha) * input.Color;
}

technique Technique1
{
    pass QueenPrismCrystalPass
    {
        VertexShader = compile vs_3_0 VertexShaderFunction();
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
