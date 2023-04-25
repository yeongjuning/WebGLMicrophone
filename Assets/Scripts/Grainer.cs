using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Grainer
{
    // 샘플을 추출하고 추출한 샘플을 특정 구간으로 변환한 그레인(Grain)을 나타내는 클래스
    public class Grain
    {
        public float origStart; // 그레인이 추출된 오디오의 원래 시작 위치를 캐싱
        public float start;     // 스트림에 콘텐츠를 추가하기 시작할 시간(초 단위)
        public float width;     // 콘텐츠를 스트림에 추가할 시간(초) -> Grain.samples 변수와 연결
        public float inEdge;    // 콘텐츠를 스트림에 추가할 때 in-edge로 적용될 시간(초) -> (Grain.width - Grain.outEdge)보다 크면 안 됨.
        public float outEdge;   // 콘텐츠를 스트림에 추가할 때 in-edge로 적용될 시간(초) -> (Grain.width - Grain.outEdge)보다 크면 안 됨.
        public float[] samples; // PCM 샘플 저장
    }

    List<Grain> grains = new List<Grain>(); // 추출한 오디오 클립에서 생성한 그레인(Grain)의 리스트

    /// <summary>
    /// 
    /// </summary>
    public int Count
    { 
        get
        { 
            if(this.grains == null) // Sanity check
                return 0;
            return this.grains.Count;
        }
    }

    /// <summary>
    /// 오디오 데이터를 그레인(Grain)으로 변환하여 그레인 리스트 반환
    /// </summary>
    /// <param name="grainLen">그레인의 길이(초)</param>
    /// <param name="skipLen">그레인 간 거리(초)</param>
    /// <param name="inlen">각 그레인에서 램프 업(ramp-up)하는데 필요한 시간(초)</param>
    /// <param name="outlen">각 그레인에서 램프 다운(ramp-down)하는데 필요한 시간</param>
    public static List<Grain> GranulateFromClip(AudioClip clip, float grainLen, float skipLen, float inlen, float outlen)
    {
        List<Grain> ret = new List<Grain>();

        if (clip == null)
            return ret;

        // Sanity fix
        grainLen = Mathf.Max(grainLen, inlen + outlen);

        int sampleCt = clip.samples;
        float[] samps = new float[sampleCt];
        clip.GetData(samps, 0);

        float loc = 0.0f;

        int grainSamples = (int)(clip.frequency * grainLen);
        while (loc < clip.length)
        {
            Grain g = new Grain();
            g.inEdge = inlen;
            g.outEdge = outlen;
            g.start = loc;
            g.origStart = loc;
            g.width = grainLen;

            int grainStart = (int)(clip.frequency * loc);
            int grainEnd = grainStart + grainSamples;

            // 마지막 샘플을 복사할 수 있을때까지 감지(더 이상 녹음할 수 없을때 또는 그레누레이션할 마지막 샘플을 지나갈때)
            int sampleCpy = Mathf.Min(grainEnd, sampleCt) - grainStart;

            g.samples = new float[grainSamples];

            int i = 0;
            for (; i < sampleCpy; ++i)
                g.samples[i] = samps[i + grainStart];

            // 마지막 그레인에 대한 예비책 -> 아마 샘플 끝을 넘어갈 것.
            // 정확하게 끝내려고 길이를 수정할수도 있지만, 여기서는 0으로
            for (; i < grainSamples; ++i)
                g.samples[i] = 0;

            ret.Add(g);
            loc += skipLen;
        }

        return ret;
    }

    public int Granulate(AudioClip clip, float grainLen, float skipLen, float inlen, float outlen)
    { 
        this.grains = GranulateFromClip(clip, grainLen, skipLen, inlen, outlen);
        return this.grains.Count;
    }

    /// <summary>
    /// 주어진 그레인 목록의 시작 시간을 상수 값으로 전체적인 조절
    /// by a constant value.
    /// </summary>
    /// <param name="grains">시작 시간을 조절할 그레인들</param>
    /// <param name="scale">시작 시간을 조절 할 양</param>
    /// <param name="compound">true이면 현재 그레인 시간에 따라 조절하고, false이면 원래 시간에 따라 조절</param>
    public void ScaleGrainTime(float scale, bool compound)
    {
        for (int i = 0; i < this.grains.Count; ++i)
        {
            if (compound == true)
                this.grains[i].start *= scale;
            else
                this.grains[i].start = this.grains[i].origStart * scale;
        }
    }

    /// <summary>
    /// 주어진 그레인 목록을 오디오 스트림의 PCM으로 재구성.
    /// 이 함수는 목록이 정렬되어 있다고 가정 -> 최소한 마지막 그레인이 가장 먼 시간을 가지고 있다고 가정</remarks>
    /// </summary>
    /// <param name="grains">재구성할 그레인들입니다.</param>
    /// <returns>그레인들로부터 재구성된 오디오입니다.</returns>
    public float[] ReconstructGrains(float gain)
    {
        Grain lastGrain = this.grains[this.grains.Count - 1];
        int retSampleCt = (int)(lastGrain.start * WebMic.FreqRate) + lastGrain.samples.Length;

        // 각 시간별로 시간에 대한 가중평균을 수행
        float[] ret = new float[retSampleCt];  // 누적된 Sample_Value * Weight
        float[] wt = new float[retSampleCt];   // 누적된 Weight
        for (int i = 0; i < retSampleCt; ++i)
        {
            ret[i] = 0;
            wt[i] = 0;
        }

        foreach (Grain g in this.grains)
        {
            int start = (int)(g.start * WebMic.FreqRate);      // 데이터를 누적하기 시작할 샘플 인덱스
            int end = g.samples.Length;                        // 쓸 샘플의 수
            int endup = (int)(g.inEdge * WebMic.FreqRate);     // 램프 업을 위한 샘플 수
            int endhigh = (int)(g.outEdge * WebMic.FreqRate);  // 램프 다운 전까지의 샘풀 수

            int i = 0;  // 현재 기록 중인 그레인 샘플

            // 램프 업하는 샘플 쓰기 - 가중치가 선형적으로 증가.
            for (i = 0; i < endhigh; ++i)
            {
                float lam = ((float)i / (float)endhigh);
                ret[start + i] += lam * g.samples[i];
                wt[start + i] += lam;
            }

            // 램프가 없는 샘프 쓰기 - 가중치가 1.0
            for (i = endup; i < endhigh; ++i)
            {
                ret[start + i] += g.samples[i];
                wt[start + i] += 1.0f;
            }

            // 램프 다운하는 샘플 쓰기 - 가중치가 선형적으로 감소
            float sub = 1.0f / ((float)end - (float)endhigh);
            float downWt = 1.0f;
            for (i = endhigh; i < end; ++i)
            {
                ret[start + i] += g.samples[i] * downWt;
                wt[start + i] += downWt;
                downWt -= sub;
            }
        }

        // 각 샘플을 가중칠 나누어 샘플을 가중 평균으로 바꿈
        for (int i = 0; i < ret.Length; ++i)
        {
            if (wt[i] == 0.0f)
                ret[i] = 0.0f;
            else
                ret[i] = ret[i] / wt[i] * gain;
        }

        return ret;
    }

    /// <summary>
    /// 기록된 그레인 클리어.
    /// </summary>
    public void Clear()
    { 
        this.grains.Clear();
    }

    /// <summary>
    /// 그레인이 존재하는지 체크.
    /// </summary>
    public bool HasGrains()
    { 
        return
            this.grains != null && // Sanity check
            this.grains.Count > 0;
    }
}
