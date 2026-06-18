using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Audio;
using UnityEngine.Networking;
using System.Collections;
using System.IO;
using SFB; // StandaloneFileBrowser 네임스페이스 추가

public class AudioManager : MonoBehaviour
{
    public AudioSource audioSource;
    public AudioMixer mainMixer;
    public ScrollEditor scrollEditor;

    [Header("UI Elements")]
    public Slider playbackSlider;
    public Text AudioTimeText;

    private double songStartTime;
    private double pauseTime;
    public bool isPlaying = false;
    private bool isDragging = false; // 슬라이더 조작 중인지 체크

    public void SetPlaybackSpeed(float speed)
    {
        //audioSource.pitch = speed;
        if (speed > 0)
        {
            mainMixer.SetFloat("MyPitch", 1f / speed);
        }

        Debug.Log($"현재 배속: {speed}x (음정 고정)");
    }

    // UI 버튼
    public void OpenFileBrowser()
    {
        // 확장자 필터 설정
        var extensions = new[] {
            new ExtensionFilter("Audio Files", "mp3", "wav", "ogg")
        };

        // 파일 탐색기 열기 (동기 방식이지만 코루틴으로 처리 권장)
        string[] paths = StandaloneFileBrowser.OpenFilePanel("Open Audio File", "", extensions, false);

        if (paths.Length > 0 && !string.IsNullOrEmpty(paths[0]))
        {
            StartCoroutine(LoadAudioCoroutine(paths[0]));
        }
    }

    IEnumerator LoadAudioCoroutine(string path)
    {
        // 경로 처리 (Windows의 경우 file:/// 추가 필요)
        string url = "file:///" + path;

        using (UnityWebRequest www = UnityWebRequestMultimedia.GetAudioClip(url, AudioType.UNKNOWN))
        {
            yield return www.SendWebRequest();

            if (www.result == UnityWebRequest.Result.Success)
            {
                AudioClip clip = DownloadHandlerAudioClip.GetContent(www);
                clip.name = Path.GetFileName(path);

                audioSource.clip = clip;
                pauseTime = 0; // 새 곡 로드 시 시간 초기화
                isPlaying = false;

                Debug.Log($"곡 로드 완료: {clip.name}");
            }
            else
            {
                Debug.LogError($"오디오 로드 실패: {www.error}");
            }
        }
    }

    // --- DSP 기반 재생 로직 (기존 유지) ---
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space)) TogglePlayback();
        if (Input.GetKeyDown(KeyCode.R)) ResetPlayback(); // R키로 리셋

        if (audioSource.clip != null)
        {
            UpdateUI();

            if (isPlaying && !isDragging)
            {
                double elapsed = AudioSettings.dspTime - songStartTime;
                scrollEditor.SyncScrollToAudio((float)elapsed);

                // 슬라이더 위치 동기화
                playbackSlider.value = (float)(elapsed / audioSource.clip.length);
            }
        }
        if (Input.GetKeyDown(KeyCode.F1)) SetPlaybackSpeed(2f);
        if (Input.GetKeyDown(KeyCode.F2)) SetPlaybackSpeed(1.5f);
        if (Input.GetKeyDown(KeyCode.F3)) SetPlaybackSpeed(1.0f);
        if (Input.GetKeyDown(KeyCode.F4)) SetPlaybackSpeed(0.5f);
        if (Input.GetKeyDown(KeyCode.F5)) SetPlaybackSpeed(0f);
    }

    public void TogglePlayback()
    {
        if (audioSource.clip == null) return;

        if (!isPlaying)
        {
            songStartTime = AudioSettings.dspTime - pauseTime;
            audioSource.Play();
            isPlaying = true;
        }
        else
        {
            pauseTime = AudioSettings.dspTime - songStartTime;
            audioSource.Pause();
            isPlaying = false;
        }
    }

    void StopPlayback()
    {
        isPlaying = false;
        audioSource.Stop();
        pauseTime = 0;
    }
    public void ResetPlayback()
    {
        pauseTime = 0;
        songStartTime = AudioSettings.dspTime;
        audioSource.Stop();
        if (isPlaying) audioSource.Play();
        else audioSource.Stop();

        scrollEditor.SyncScrollToAudio(0);
        playbackSlider.value = 0;
    }
    void UpdateUI()
    {
        double elapsed = isPlaying ? AudioSettings.dspTime - songStartTime : pauseTime;
        AudioTimeText.text = $"{FormatTime((float)elapsed)} / {FormatTime(audioSource.clip.length)}";
    }

    string FormatTime(float time)
    {
        int minutes = Mathf.FloorToInt(time / 60f);
        int seconds = Mathf.FloorToInt(time % 60f);
        return string.Format("{0:00}:{1:00}", minutes, seconds);
    }
    public void OnSliderValueChanged()
    {
        if (audioSource.clip == null) return;

        // 유저가 직접 드래그할 때만 반응하도록 함
        float targetTime = playbackSlider.value * audioSource.clip.length;
        pauseTime = targetTime;
        songStartTime = AudioSettings.dspTime - pauseTime;

        audioSource.time = (float)targetTime;
        scrollEditor.SyncScrollToAudio((float)targetTime);
    }

}