using System.Runtime.InteropServices;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WebMic : MonoBehaviour
{
    public enum State
    { 
        Booting,    // 마이크 사용준비중
        NotActive,  // 녹음 가능한 상황
        Recording   // 현재 녹음중인 상황
    }

    /// <summary>
    /// 자바스크립트 Plugin으로 연결된 자바스크립트 함수 호출.
    /// </summary>
    [DllImport("__Internal")]
    public static extern void Recording_Start();

    [DllImport("__Internal")]
    public static extern void Recording_Stop();

    [DllImport("__Internal")]
    public static extern bool Recording_UpdatePointer(float [] idx);

    public const int FreqRate = 44100;  // 기록 및 재생 속도.

    AudioClip _recordingClip = null;    // 녹음이 끝나면 저장되는 Audioclip -> 마지막 녹화만 유지.
    public AudioClip RecordingClip 
    {
        get=>this._recordingClip; 
        private set{this._recordingClip = value; } 
    }

    public string recordingDevice;      // 녹음 장치 이름 (디버깅용)
    public const int MaxRecordTime = 5; // 녹화에 허용되는 최대 시간(초)

#if !UNITY_WEBGL || UNITY_EDITOR
    //#if false
    public bool SetDefaultRecordingDevice()
    {
        if (Microphone.devices.Length > 0)
        {
            this.recordingDevice = Microphone.devices[0];
            return true;
        }
        return false;
    }

    public bool StartRecording()
    {
        this.RecordingClip = Microphone.Start(this.recordingDevice, false, MaxRecordTime, FreqRate);
        return this.RecordingClip != null;
    }

    public AudioClip StopRecording()
    {
        Microphone.End(this.recordingDevice);
        return this.RecordingClip;
    }

    public State RecordingState()
    {
        return
            Microphone.IsRecording(this.recordingDevice) ?
                State.Recording :
                State.NotActive;
    }

    public bool ClearRecording()
    {
        return true;
    }

#else

    const int BufferSize = 2048;
    public struct FloatArray
    { 
        public float [] buffer;
        public int written;
    }

    private List<FloatArray> binaryStreams = new List<FloatArray>();
    State recordingState = State.NotActive;

    FloatArray currentBuffer;

    public void Awake()
    {
        this.currentBuffer = new FloatArray();
        this.currentBuffer.buffer = new float[BufferSize];
        Recording_UpdatePointer(this.currentBuffer.buffer);
    }

    public void LogWrittenBuffer(int written)
    { 
        if(this.recordingState != State.Recording)
            return;

        this.currentBuffer.written = written;
        this.binaryStreams.Add(this.currentBuffer);

        this.currentBuffer = new FloatArray();
        this.currentBuffer.buffer = new float[BufferSize];
        Recording_UpdatePointer(this.currentBuffer.buffer);
    }

    public bool SetDefaultRecordingDevice()
    {
        return false;
    }

    public bool StartRecording()
    {
        if(this.recordingState != State.NotActive)
            return false;

        this.recordingState = State.Booting;

        Recording_Start();

        this.RecordingClip = null;
        return true;
    }

    public AudioClip StopRecording()
    {
        Recording_Stop();
        return this.RecordingClip;
    }

    public State RecordingState()
    {
        return this.recordingState;
    }

    public bool ClearRecording()
    {
        if (this.binaryStreams.Count == 0)
            return false;

        this.binaryStreams.Clear();

        return true;
    }

    public float [] GetData(bool clear = true)
    { 
        int fCt = 0;
        foreach(FloatArray fa in this.binaryStreams)
            fCt += fa.written;

        float [] ret = new float[fCt];


        int write = 0;
        foreach(FloatArray fa in this.binaryStreams)
        { 
            System.Buffer.BlockCopy(fa.buffer, 0, ret, write * 4, fa.written * 4);
            write += fa.written;
        }

        if (clear == true)
            ClearRecording();

        return ret;
    }

    /// <summary>
    // 앱에 마이크 녹음을 알리기 위해서 JavaScript 호출 => 웹에서 SendMessage로 호출
    /// </summary>
    public void NotifyRecordingChange(int newRS)
    { 
        if((int)this.recordingState == newRS)
            return;

        State oldState = this.recordingState;
        this.recordingState = (State)newRS;

        if(oldState == State.Recording)
            this.RecordingClip = this.FlushDataIntoClip();
    }

    AudioClip FlushDataIntoClip()
    {
        float[] pcm = this.GetData();
        if (pcm != null && pcm.Length > 0)
        {
            AudioClip ac = AudioClip.Create("", pcm.Length, 1, FreqRate, false);
            ac.SetData(pcm, 0);
            return ac;
        }
        return null;
    }

#endif
}
