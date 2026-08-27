// ============================================================================
// QueenPrismGem.fx 皇后棱晶宝石(节点/囚茧专用)
// 材质=切割圣光凝胶宝石：五边形冠亭轮廓+七分面伪3D法线+游走键光逐面闪光
//   +面折射马赛克(法线弯折采样,分面边界内部纹理错位)+深层视差
//   +轮廓三色色散缘+分面谱色棱线+腰棱线+体内高光闪点
// uGrow 物化(分面错帧点亮) uShatter 裂纹网+蚀散 uCharge 白热核脉动
// 全程直线算术:无 atan2、无动态分支(step门控)；预乘输出+AlphaBlend
// 噪声实测值域 0.227~0.776,阈值一律过 nrm() 归一(VFX.md Noise-threshold rule)
// ============================================================================

float4x4 transformMatrix;
float uTime;
float uGrow;      //物化进度 0~1
float uShatter;   //碎裂进度 0~1(节点=受损度)
float uCharge;    //蓄能 0~1(馈线供能/囚茧终结)
float uHueSeed;   //色相种子
float seed;       //实例种子

//噪声固定 s1：C# 侧在 pass.Apply 前显式 Textures[1]=PerlinNoise + LinearWrap
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

//实测值域归一
float nrm(float x)
{
    return saturate((x - 0.227) / 0.549);
}

float3 PrismHue(float t)
{
    return 0.72 + 0.28 * cos(6.28318 * (t + float3(0.0, 0.35, 0.68)));
}

//五边形宝石轮廓SDF(x已折叠为ax)：顶台面/上斜肩/下亭斜三半平面取max，d<0在内
//顶点：台缘(±0.34,-0.60) 腰棱(±0.60,-0.08) 亭尖(0,0.86)
float GemSDF(float ax, float py)
{
    float d1 = -py - 0.60;                                //台面(顶边)
    float d2 = 0.8944 * ax - 0.4472 * py - 0.5724;        //冠斜面
    float d3 = 0.8429 * ax + 0.5380 * py - 0.4627;        //亭斜面
    return max(d1, max(d2, d3));
}

float4 PixelShaderFunction(PSInput input) : COLOR0
{
    float2 uv = input.TexCoords;
    float2 p = (uv - 0.5) * 2.0;              //-1~1 画布坐标
    float2 pg = p / max(uGrow, 0.02);         //物化：形体从中心长出
    float ax = abs(pg.x);
    float py = pg.y;

    //=========================================================
    //轮廓与分面判定
    //=========================================================
    float d1 = -py - 0.60;
    float d2 = 0.8944 * ax - 0.4472 * py - 0.5724;
    float d3 = 0.8429 * ax + 0.5380 * py - 0.4627;
    float m = max(d1, max(d2, d3));

    float body = smoothstep(0.02, -0.03, m);  //软体掩码

    //支配边→分面族(平局由顺序压制,视觉无碍)
    float isUp = step(d1, d2) * step(d3, d2);
    float isTop = (1.0 - isUp) * step(d2, d1) * step(d3, d1);
    float isLow = saturate(1.0 - isUp - isTop);

    const float bw = 0.20;                    //斜面带宽(SDF单位)
    float bevel = step(-bw, m);               //外圈斜面带
    float crown = step(py, -0.08);            //腰线以上=冠区

    //分面编号：斜面 0~4(顶/右冠/左冠/右亭/左亭)，台面 5冠 6亭
    float xl = step(pg.x, 0.0);
    float fidBevel = isUp * (1.0 + xl) + isLow * (3.0 + xl);
    float fid = lerp(6.0 - crown, fidBevel, bevel);

    //伪3D法线：台面近朝观者微倾，斜面沿轮廓外法线外倾
    float xs = step(0.0, pg.x) * 2.0 - 1.0;
    float2 nb2 = isTop * float2(0.0, -1.0)
               + isUp * float2(0.8944, -0.4472)
               + isLow * float2(0.8429, 0.5380);
    nb2.x *= xs;
    float ntY = lerp(0.30, -0.22, crown);
    float3 N = normalize(lerp(float3(0.0, ntY, 1.0), float3(nb2 * 0.85, 0.62), bevel));

    //=========================================================
    //双灯照明：游走键光(逐面镜面闪光)+反向补光
    //=========================================================
    float la = uTime * 0.7 + seed * 6.28318;
    float3 L = normalize(float3(cos(la) * 0.85, sin(la * 0.83) * 0.55 - 0.30, 0.58));
    float3 H = normalize(L + float3(0.0, 0.0, 1.0));
    float dif = 0.26 + 0.74 * pow(saturate(dot(N, L)), 1.35);
    float specKey = pow(saturate(dot(N, H)), 10.0);

    float lb = -uTime * 0.43 + seed * 9.4;
    float3 L2 = normalize(float3(cos(lb) * 0.8, sin(lb) * 0.5 + 0.35, 0.50));
    float3 H2 = normalize(L2 + float3(0.0, 0.0, 1.0));
    float specFill = pow(saturate(dot(N, H2)), 8.0) * 0.45;

    //逐面闪光相位(晶体转光)；受损分面失稳抖闪
    float flash = 0.55 + 0.45 * sin(uTime * 2.3 + fid * 2.53 + seed * 12.0);
    float unstab = 1.0 - uShatter * 0.45 * (0.5 + 0.5 * sin(uTime * 31.0 + fid * 5.1));
    float spec = (specKey + specFill) * flash * unstab * (1.0 + uCharge * 0.8);

    //轮流整面燃亮：各分面错相轮值,保证闪光节拍不靠灯位撞运气
    float flare = smoothstep(0.58, 0.96, sin(uTime * 2.1 + fid * 2.53 + seed * 12.0)) * unstab;

    //物化分面错帧点亮
    float fVis = smoothstep(fid * 0.07, fid * 0.07 + 0.30, uGrow);

    //=========================================================
    //体内采样：面折射马赛克+深层视差+高光闪点(刚体旋转坐标)
    //=========================================================
    float ra = uTime * 0.16 + seed * 6.0;
    float ca = cos(ra);
    float sa = sin(ra);
    float2 prc = float2(pg.x * ca - pg.y * sa, pg.x * sa + pg.y * ca);

    //法线弯折采样：分面边界处体内纹理错位=切割宝石的镶嵌读法
    float refrN = nrm(tex2D(noiseSamp, prc * 0.55 + N.xy * -0.30 + float2(seed * 7.0, uTime * 0.03)).g);
    //更大偏移+更粗尺度=体内深层
    float deepN = nrm(tex2D(noiseSamp, prc * 0.32 + N.xy * -0.62 + float2(seed * 3.0, -uTime * 0.02)).g);
    //游走高光闪点
    float sparkN = nrm(tex2D(noiseSamp, prc * 1.5 + float2(uTime * 0.045, seed * 11.0)).g);
    float glint = smoothstep(0.76, 0.90, sparkN) * (0.35 + 0.65 * flash);

    //逐面各向异性内部光纹：采样轴随分面法线转,边界处光纹错断=体内折射镶嵌
    float2 sAxis = normalize(N.xy + float2(0.0001, 0.12));
    float2 sPerp = float2(-sAxis.y, sAxis.x);
    float streakN = nrm(tex2D(noiseSamp, float2(dot(prc, sAxis) * 0.45 + seed * 5.0,
        dot(prc, sPerp) * 1.9 - uTime * 0.05)).g);
    float streak = smoothstep(0.55, 0.88, streakN);

    //亭尖聚光：切割宝石把光汇进底尖
    float culet = exp2(-(pg.x * pg.x + (pg.y - 0.86) * (pg.y - 0.86)) * 7.0);

    //=========================================================
    //碎裂：裂纹亮线+自边缘蚀散(uv锚定,裂纹是结构不随体转)
    //=========================================================
    float crackN = nrm(tex2D(noiseSamp, uv * 3.0 + seed * 4.0).g);   //细尺度:裂纹线
    float chunkN = nrm(tex2D(noiseSamp, uv * 1.35 + seed * 9.0).g);  //粗尺度:缺块+线段门
    //细等值线+粗噪声门撕成断续裂纹段,防成片发白
    float crackLine = smoothstep(0.035, 0.0, abs(crackN - 0.5)) * smoothstep(0.30, 0.72, chunkN)
                    * smoothstep(0.0, 0.35, uShatter);
    //蚀散走粗尺度:少数大缺块,不是满身锈斑
    float erode = smoothstep(uShatter * 1.15 - 0.15, uShatter * 1.15 + 0.1, chunkN + 0.25);
    body *= lerp(1.0, erode, step(0.02, uShatter));

    //=========================================================
    //棱线族：轮廓色散缘/分面棱线/台缘环线/腰棱线
    //=========================================================
    //轮廓缘带三色错径向缩放=色散
    float axR = abs(pg.x) * 1.022;
    float pyR = pg.y * 1.022;
    float mR = GemSDF(axR, pyR);
    float axB = abs(pg.x) * 0.978;
    float pyB = pg.y * 0.978;
    float mB = GemSDF(axB, pyB);
    float rimG = smoothstep(0.045, 0.0, abs(m));
    float rimR = smoothstep(0.045, 0.0, abs(mR));
    float rimB = smoothstep(0.045, 0.0, abs(mB));

    //分面棱线：两支配边近等处(限斜面带内)
    float b12 = smoothstep(0.022, 0.0, abs(d1 - d2));
    float b23 = smoothstep(0.022, 0.0, abs(d2 - d3));
    float facetLine = saturate(b12 + b23) * bevel * body;
    //台缘环线(内五边形轮廓)
    float tableLine = smoothstep(0.020, 0.0, abs(m + bw)) * body;
    //腰棱线(最宽切割线,横贯全宽)
    float girdle = smoothstep(0.030, 0.0, abs(py + 0.08)) * body;

    //=========================================================
    //调色合成(预乘)
    //=========================================================
    //棱镜身份：每分面把光拆到不同谱段,各面自带色相倾斜+慢漂
    float fidHash = frac(sin(fid * 12.9898 + seed * 78.233) * 43758.5453);
    float3 facetHue = PrismHue(uHueSeed + fid * 0.05 + fidHash * 0.06 + uTime * 0.015);
    float3 hueDeep = PrismHue(uHueSeed + 0.45);
    float3 lineHue = PrismHue(uHueSeed + fid * 0.09 + uTime * 0.04);
    float3 cWhite = float3(1.0, 0.98, 0.94);

    //斜面带内自台缘向轮廓提亮：切割棱台的立体渐变
    float bevGrad = saturate((m + bw) / bw) * bevel;

    float3 color = hueDeep * 0.34 * (0.50 + deepN * 0.50);        //深层底
    color += facetHue * dif * (0.34 + refrN * 0.52);              //主体折射×分面漫反(逐面谱色)
    color += (facetHue * 0.7 + cWhite * 0.3) * streak * 0.30 * dif; //体内折射光纹
    color += cWhite * culet * (0.30 + 0.25 * flash);              //亭尖聚光
    color += facetHue * bevGrad * 0.22;                           //棱台立体渐变
    color += facetHue * spec * 0.85 * fVis;                       //分面镜面(色体)
    color += cWhite * spec * 0.55 * fVis;                         //分面镜面(白顶)
    color += (facetHue * 0.55 + cWhite * 0.45) * flare * 0.55 * fVis; //轮值整面燃亮
    color += lineHue * facetLine * (0.70 + uCharge * 0.55) * fVis; //分面谱色棱线(馈能点亮)
    color += lineHue * tableLine * 0.38 * fVis;                   //台缘环线
    color += cWhite * girdle * 0.24;                              //腰棱线
    color += cWhite * glint * 1.25;                               //体内闪点
    color += cWhite * crackLine * 1.10;                           //裂纹白线

    //轮廓色散缘：绿通道白缘+红蓝错径谱边
    float rimBoost = 1.0 + uCharge * 0.9;
    color += cWhite * rimG * 0.50 * rimBoost;
    color += float3(1.0, 0.25, 0.15) * rimR * 0.38 * rimBoost;
    color += float3(0.20, 0.35, 1.0) * rimB * 0.38 * rimBoost;

    //蓄能白热核脉动(收紧半径,白只住核心)
    float pulse = 0.5 + 0.5 * sin(uTime * 9.0 + seed * 20.0);
    float coreG = exp2(-dot(pg, pg) * 8.0);
    color += (facetHue * 0.5 + cWhite * 0.5) * uCharge * (0.35 + 0.65 * pulse) * coreG * 0.95;

    //物化前沿闪光(径向生长环)
    float growEdge = (1.0 - smoothstep(0.0, 0.25, abs(uGrow - length(p)))) * (1.0 - uGrow) * 0.6;
    color += cWhite * growEdge;

    //=========================================================
    //透明度：体半透+棱线实+缘带略溢出体外(色散光晕)
    //=========================================================
    float rimOut = smoothstep(0.06, 0.0, m);  //体外近缘软罩
    float alpha = body * (0.52 + dif * 0.16 + spec * 0.28 + flare * 0.20 + facetLine * 0.30
                + tableLine * 0.12 + glint * 0.30 + girdle * 0.08 + bevGrad * 0.10
                + streak * 0.08 + culet * 0.15
                + crackLine * 0.18 + uCharge * coreG * 0.30);
    alpha += (rimG * 0.50 + (rimR + rimB) * 0.22) * rimOut * rimBoost;
    alpha += growEdge * 0.4;

    //画布护栏：最后8%渐零防切边
    float guard = smoothstep(1.0, 0.92, max(abs(p.x), abs(p.y)));
    alpha = saturate(alpha) * guard;

    return float4(color * alpha, alpha) * input.Color;
}

technique Technique1
{
    pass QueenPrismGemPass
    {
        VertexShader = compile vs_3_0 VertexShaderFunction();
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
