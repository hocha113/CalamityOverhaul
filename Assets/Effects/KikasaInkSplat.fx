// ============================================================================
//KikasaInkSplat.fx 墨渍溅斑贴花——命中的余韵,比弹幕活得久
//TechSplat:墨在表面上的一生——晕染(缘先扩后定,湿时缘糊)→干涸(咖啡环缘沉芯褪)
//          →滴淌(重力向柱流,uRunScale 随命中面:地面几乎不滴/墙顺流/顶垂挂);
//          uDir 主轴+uSquish 垂轴压扁承载贴面姿态(地=扁宽墨泊,墙=窄长竖渍),
//          quad 恒不旋转,滴淌恒沿世界重力;死墨不爬——无 uTime,只有包络在走;
//          uCanvasFit 把各向异性摊开收进 UV 预算,C# 同步放大 quad(只放大正方形不够);
//          uProf 地形剖面(C# 出生时逐列烘焙):像素先沿 uProfN 按列位移——
//          墨随台阶下沉、贴斜坡、翻上墙角,悬空列淡出,渍不再是一张悬空的完整椭圆
//TechLakeBlot:湖面墨晕——墨入水沿水线晕开的极扁墨膜,缘先扩、随时间稀释
//          (墨入水是"散"不是"干"),缘外散丝、水下渗色、水线一线薄光
//坐标全笛卡尔(无 atan2);直线算术+普通 tex2D,FNA3D 安全
//预乘输出,进 AlphaBlend 批;消费入口 KikasaRains/KikasaInkFX.cs
// ============================================================================

float uSeed;
float uBloom;     //晕染扩张 0~1
float uDry;       //干涸 0~1:缘沉色、芯褪淡(湖晕语义=稀释)
float uRun;       //滴淌长度包络 0~1
float uFade;      //谢幕淡出(含宿主消亡的快淡)
float uAniso;     //主轴拉伸 1~1.8
float uSquish;    //垂轴压扁 0.2~1:贴面姿态
float uRunScale;  //滴淌长度系数:地 0.3/墙 1.25/顶 1.6
float2 uDir;      //各向异性主轴(单位向量,quad 空间)
float2 uCanvasFit;//逻辑坐标/UV 倍率,与 C# quad 缩放配套;(1,1)=旧行为。只放大正方形不够,UV 仍满幅会切左右
float uProf[24];  //地形剖面:逐取样列的表面偏移(世界像素,相对锚点;±72 悬空哨兵)
float2 uProfN;    //剖面位移轴:地/顶=(0,1),墙=(1,0),NPC 渍=(0,0) 即不扭
float uProfQScale;//切向 q → 取样下标倍率(=Size*1.2/16)
float uProfQOff;  //取样下标偏移(含墙面切向的 0.18 印面上移折算)
float uInvWorldPerQ; //世界像素 → q 单位(=1/(Size*1.2))
float uEdgeSign;  //悬空淡出方向符号:地+1/顶-1/左墙-1/右墙+1;0=不淡出
float3 uColBody;  //墨体
float3 uColDeep;  //缘沉/干痕
float3 uColCore;  //新鲜期血芯
float3 uColSheen; //新鲜期湿反光

sampler uNoiseTex : register(s1);

float4 PSSplat(float2 coords : TEXCOORD0, float4 vertexColor : COLOR0) : COLOR0
{
    float2 raw = coords * 2.0 - 1.0;
    float2 fit = max(uCanvasFit, float2(1.0, 1.0));
    //印面中心略上,下方画布留给滴淌;fit 把各向异性摊开收进 UV 预算,C# 同步放大 quad 保世界尺寸
    float2 q = raw * fit - float2(0.0, -0.18);

    //地形剖面:沿切向(uDir)取列高,q 先按剖面逐列位移——整套形体/滴淌随台阶斜坡垂落。
    //帐篷权重求和=线性插值,ps_3_0 不能动态寻址常量,展开循环用字面下标
    float st = clamp(dot(q, uDir) * uProfQScale + uProfQOff, 0.0, 23.0);
    float prof = 0.0;
    [unroll] for (int pi = 0; pi < 24; pi++) {
        prof += uProf[pi] * saturate(1.0 - abs(st - (float)pi));
    }
    q -= uProfN * (clamp(prof, -56.0, 56.0) * uInvWorldPerQ);
    //悬空列(剖面里探不到面)整列淡出:墨溜过棱缘再散掉,不悬空画墨
    float edgeKeep = 1.0 - smoothstep(44.0, 64.0, prof * uEdgeSign);

    //贴面姿态:先把主轴外的方向压扁(地渍摊平/墙渍收窄),再沿主轴拉伸
    float2 perpU = float2(-uDir.y, uDir.x);
    float2 qp = q + perpU * (dot(q, perpU) * (1.0 / max(uSquish, 0.2) - 1.0));
    float along = dot(qp, uDir);
    float2 qa = qp + uDir * along * (1.0 / max(uAniso, 1.0) - 1.0);
    float r = length(qa);

    //两频噪声揉形:主瓣轮廓+高频耳瓣,墨点不是圆
    float n1 = tex2D(uNoiseTex, qa * 0.8 + uSeed).r;
    float n2 = tex2D(uNoiseTex, qa * 2.2 + uSeed * 1.7).g;
    float field = r - (n1 - 0.5) * 0.5 - (n2 - 0.5) * 0.2;

    //晕染:半径随 uBloom 扩张,湿时缘糊、定型后缘紧
    float R = 0.30 + 0.34 * uBloom;
    float soft = lerp(0.17, 0.05, uBloom);
    float blot = 1.0 - smoothstep(R - soft, R + soft, field);

    //卫星溅点:主体外圈的高阈噪声碎斑
    float ringZone = smoothstep(R * 0.95, R * 1.12, field) * (1.0 - smoothstep(R * 1.45, R * 1.8, field));
    float specks = ringZone * smoothstep(0.66, 0.80, n2) * uBloom;

    //咖啡环干涸:缘带沉色,芯褪淡
    float edgeBand = smoothstep(R - 0.17, R, field) * blot;
    float centerFade = 1.0 - uDry * 0.55 * (1.0 - edgeBand);
    float fresh = 1.0 - uDry;

    float3 col = lerp(uColBody, uColDeep, edgeBand * (0.35 + 0.55 * uDry));
    col = lerp(col, uColCore, fresh * saturate(1.0 - field / max(R, 1e-3)) * 0.30);

    //滴淌:印底噪声选列的重力向柱流,长度随命中面;quad 不旋转,+y 恒为世界向下
    float colN = tex2D(uNoiseTex, float2(qa.x * 3.4 + uSeed * 3.1, 0.37)).b;
    float below = q.y - R * 0.45 * uSquish;
    float runLen = uRun * uRunScale * (0.22 + colN * 0.5);
    float runnel = smoothstep(0.55, 0.85, colN)
        * smoothstep(0.0, 0.04, below)
        * (1.0 - smoothstep(runLen * 0.6, max(runLen, 1e-3), below))
        * smoothstep(R * 1.25, R * 0.55, abs(qa.x));

    //新鲜期湿反光:印内一点玻光
    float sheen = 1.0 - smoothstep(0.0, 0.12, length(qa - float2(0.07, -0.06)));
    sheen *= blot * fresh;

    //预乘合成
    float aBlot = blot * 0.92 * centerFade;
    float aSpeck = specks * 0.7;
    float aRun = runnel * 0.55;
    float3 outCol = col * aBlot
                  + lerp(uColBody, uColDeep, 0.5) * (aSpeck + aRun);
    float a = saturate(aBlot + aSpeck + aRun);
    outCol += uColSheen * sheen * 0.3;

    float guard = smoothstep(1.0, 0.88, max(abs(raw.x), abs(raw.y)));
    float k = uFade * guard * edgeKeep;
    return float4(outCol * k, a * k) * vertexColor;
}

//==================== 湖面墨晕(墨入水) ====================

float4 PSLakeBlot(float2 coords : TEXCOORD0, float4 vertexColor : COLOR0) : COLOR0
{
    float2 raw = coords * 2.0 - 1.0; //y=0 即水线,y>0 在水下
    float2 fit = max(uCanvasFit, float2(1.0, 1.0));
    float2 p = raw * fit;

    //沿水线晕开的墨膜:横向扩张吃 uBloom,缘被噪声撕散
    float R = 0.16 + 0.72 * uBloom;
    float nE = tex2D(uNoiseTex, float2(p.x * 1.4 + uSeed, uSeed * 2.3)).r;
    float field = abs(p.x) - (nE - 0.5) * 0.30;
    float film = 1.0 - smoothstep(R - 0.14, R + 0.06, field);

    //膜厚:水上一线、水下略厚(墨往下渗)
    float halfH = 0.10 + nE * 0.05;
    float thick = halfH * (1.0 + step(0.0, p.y) * 1.7);
    float vert = 1.0 - smoothstep(0.0, max(thick, 1e-3), abs(p.y));

    //缘外散丝:墨在水里的指状扩散
    float nF = tex2D(uNoiseTex, float2(p.x * 3.1 + uSeed * 3.7, uSeed + 0.5)).g;
    float fingerZone = smoothstep(R * 0.85, R * 1.05, field)
        * (1.0 - smoothstep(R * 1.3, R * 1.6, field));
    float fingers = fingerZone * smoothstep(0.55, 0.78, nF) * uBloom;

    //稀释:墨入水是"散"——整体变淡、色向浊(uDry 在此语义为稀释)
    float dilute = uDry;
    float3 col = lerp(uColBody, uColDeep, 0.35 + dilute * 0.45);

    //水下渗色:自水线向下渐弱的一段染色
    float bleed = step(0.0, p.y) * (1.0 - smoothstep(0.0, 0.55, p.y)) * film * 0.30;

    float aFilm = film * vert * (0.55 - dilute * 0.30);
    float aFingers = fingers * vert * 0.30;
    float a = saturate(aFilm + aFingers + bleed * 0.4);
    float3 outCol = col * a;

    //水线薄光:新鲜墨膜上沿一线湿光,稀释后熄灭
    float lineGlow = exp2(-p.y * p.y * 300.0) * film * (1.0 - dilute);
    outCol += uColSheen * lineGlow * 0.12;

    float guard = smoothstep(1.0, 0.88, max(abs(raw.x), abs(raw.y)));
    float k = uFade * guard;
    return float4(outCol * k, a * k) * vertexColor;
}

technique TechSplat
{
    pass SplatPass
    {
        PixelShader = compile ps_3_0 PSSplat();
    }
}

technique TechLakeBlot
{
    pass LakeBlotPass
    {
        PixelShader = compile ps_3_0 PSLakeBlot();
    }
}
