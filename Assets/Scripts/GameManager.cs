using UnityEngine;
using UnityEngine.UI;

public class GameManager : MonoBehaviour
{
    public enum BtnType
    {
        Record,
        Stop
    }

    [SerializeField] Button[] btnExcuteByType;
    [SerializeField] Text txtDevice;
    [SerializeField] Text txtTime;
    [SerializeField] AudioSource audioSource;

    AudioClip audioClip = null;
    WebMic webMic;
    Grainer grainer = new Grainer();
    float timeStartedRecording = 0.0f;
    bool recording = false;

    void Start()
    {
        webMic = GetComponent<WebMic>();
        webMic.SetDefaultRecordingDevice();
#if UNITY_EDITOR
        txtDevice.text = $"Mic : {this.webMic.recordingDevice}";
#else
        txtDevice.text = "";
#endif
        btnExcuteByType[(int)BtnType.Record].interactable = true;
        btnExcuteByType[(int)BtnType.Stop].interactable = false;
    }

    void Update()
    {
#if UNITY_WEBGL
        if (webMic.RecordingState() == WebMic.State.Recording)
        {
            float recordLeft = Time.time - this.timeStartedRecording;
            if (recordLeft >= WebMic.MaxRecordTime)
            {
                this.audioClip = this.webMic.StopRecording();
                this.recording = false;
                txtTime.text = "Stop Recording";
            }
        }
#endif
    }

    /// <summary>
    /// 녹음시작
    /// </summary>
    public void OnClickRecord()
    {
        if (this.webMic.RecordingState() == WebMic.State.NotActive)
        {
            btnExcuteByType[(int)BtnType.Record].interactable = false;
            btnExcuteByType[(int)BtnType.Stop].interactable = true;

            this.webMic.StartRecording();
            this.timeStartedRecording = Time.time;
            this.grainer.Clear();

        }
        else
        {
            RunningRecording();
        }

        if (this.recording)
        {
            this.audioClip = this.webMic.RecordingClip;
            this.recording = false;
        }
    }

    /// <summary>
    /// 녹음된 Clip Play
    /// </summary>
    public void OnClickPlayAudio()
    {
        if (this.audioSource.clip == null)
        {
            txtTime.text = "Load clip of AudioSource null";
            return;
        }

        this.audioSource.Play();
    }

    /// <summary>
    /// 녹음 중지 및 AudioSource에 클립 할당
    /// </summary>
    public void OnClickStop()
    {
        if (this.audioClip = null)
        {
            txtTime.text = "this.audioClip is null";
            return;
        }
        
        this.audioClip = this.webMic.StopRecording();
        float[] samples = new float[this.audioClip.samples];
        if (false == this.audioClip.GetData(samples, 0))
        {
            txtTime.text = "this.audioClip Sample is null";
            return;
        }
        else
        {
            btnExcuteByType[(int)BtnType.Stop].interactable = false;
            btnExcuteByType[(int)BtnType.Record].interactable = true;

            this.audioSource.clip = this.audioClip;
        }

        //RunningRecording();
    }

    void RunningRecording()
    {
        this.recording = true;
        txtTime.text = (WebMic.MaxRecordTime - (Time.time - this.timeStartedRecording)).ToString("0.00");
    }
}
