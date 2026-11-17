Shader "LeftRight/Storm Evening Sky"
{
    Properties
    {
        _ZenithColor ("Upper evening sky", Color) = (0.15, 0.20, 0.29, 1)
        _HorizonColor ("Evening horizon", Color) = (0.49, 0.46, 0.43, 1)
        _SunsetColor ("Last warm light", Color) = (0.64, 0.37, 0.19, 1)
        _CloudDark ("Storm cloud underside", Color) = (0.105, 0.13, 0.18, 1)
        _CloudLight ("Cloud silver edges", Color) = (0.43, 0.46, 0.51, 1)
        _CloudCoverage ("Cloud cover", Range(0, 1)) = 0.72
        _CloudScale ("Cloud formation scale", Range(1, 10)) = 3.6
        _WindSpeed ("Cloud drift", Range(0, 0.05)) = 0.006
        _Exposure ("Sky brightness", Range(0.1, 3)) = 1.25
        _Rotation ("Sky rotation", Range(0, 360)) = 0
    }
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
        Cull Off ZWrite Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"

            float4 _ZenithColor, _HorizonColor, _SunsetColor, _CloudDark, _CloudLight;
            float _CloudCoverage, _CloudScale, _WindSpeed, _Exposure, _Rotation;

            struct appdata { float4 vertex : POSITION; };
            struct v2f { float4 vertex : SV_POSITION; float3 ray : TEXCOORD0; };

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                float angle = radians(_Rotation);
                float s = sin(angle), c = cos(angle);
                o.ray = float3(c*v.vertex.x + s*v.vertex.z, v.vertex.y, -s*v.vertex.x + c*v.vertex.z);
                return o;
            }

            float hash(float3 p)
            {
                p = frac(p * 0.3183099 + float3(0.13, 0.37, 0.71));
                p *= 17.0;
                return frac(p.x*p.y*p.z*(p.x+p.y+p.z));
            }

            float noise(float3 p)
            {
                float3 i = floor(p), f = frac(p);
                f = f*f*(3.0-2.0*f);
                return lerp(lerp(lerp(hash(i), hash(i+float3(1,0,0)), f.x),
                                 lerp(hash(i+float3(0,1,0)), hash(i+float3(1,1,0)), f.x), f.y),
                            lerp(lerp(hash(i+float3(0,0,1)), hash(i+float3(1,0,1)), f.x),
                                 lerp(hash(i+float3(0,1,1)), hash(i+float3(1,1,1)), f.x), f.y), f.z);
            }

            float clouds(float3 p)
            {
                float value = 0, weight = 0.52;
                [unroll] for (int octave = 0; octave < 5; octave++)
                {
                    value += weight * noise(p);
                    p = p * 2.07 + float3(11.7, 4.3, 8.1);
                    weight *= 0.49;
                }
                return value;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float3 ray = normalize(i.ray);
                float elevation = saturate(ray.y);
                float3 lightDirection = normalize(float3(-0.60, 0.08, 0.79));
                float eveningGlow = pow(saturate(dot(ray, lightDirection)), 12.0) * exp(-elevation*5.0);
                float3 sky = lerp(_HorizonColor.rgb, _ZenithColor.rgb, pow(elevation, 0.45));
                sky += _SunsetColor.rgb * eveningGlow * 0.38;

                // A broad, warped cloud bank with smaller curls along its edges.
                float3 p = ray * _CloudScale;
                p.y *= 1.6;
                p += float3(_Time.y*_WindSpeed, 0.55, _Time.y*_WindSpeed*0.35);
                float warp = clouds(p*0.63 + 7.1);
                p += float3(warp, warp*0.35, -warp) * 1.3;
                float density = clouds(p);
                float mask = smoothstep(0.66-_CloudCoverage*0.45, 0.82-_CloudCoverage*0.35, density);
                float shade = saturate(0.45 + (clouds(p+lightDirection*0.22)-density)*3.5);
                float thickness = smoothstep(0.34, 0.70, density);
                float3 cloudColor = lerp(_CloudDark.rgb, _CloudLight.rgb, shade*(1.0-thickness*0.65));
                cloudColor += _SunsetColor.rgb * eveningGlow * (1.0-thickness) * 0.12;
                float3 color = lerp(sky, cloudColor, mask);
                // Atmospheric haze keeps the treeline/horizon transition soft.
                color = lerp(color, _HorizonColor.rgb * 0.78, exp(-abs(ray.y)*18.0)*0.25);
                return float4(color*_Exposure, 1);
            }
            ENDCG
        }
    }
    Fallback Off
}
