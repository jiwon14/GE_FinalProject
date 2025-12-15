using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class BGM : MonoBehaviour
{
    [Header("BGM 설정")]
    public AudioClip bgmClip;
    [Range(0f, 1f)]
    public float volume = 0.5f;

    [Header("루프 설정")]
    public bool isLoop = true;
    [Tooltip("루프 시작 시간(초)입니다. 인트로가 있는 BGM의 경우 인트로 이후 시점을 설정하세요.")]
    public float loopStartTime = 0f;
    [Tooltip("루프 종료 시간(초)입니다. 0으로 설정하면 곡의 끝까지 재생 후 루프합니다.")]
    public float loopEndTime = 0f;

    private AudioSource audioSource;

    void Awake()
    {
        audioSource = GetComponent<AudioSource>();
    }

    void Start()
    {
        if (bgmClip != null)
        {
            audioSource.clip = bgmClip;
            audioSource.volume = volume;
            audioSource.loop = false; // Update에서 직접 루프를 제어하기 위해 false로 설정
            audioSource.Play();
        }
    }

    void Update()
    {
        if (!isLoop || audioSource == null || audioSource.clip == null) return;

        // 루프 종료 지점 설정 (0이면 클립 전체 길이)
        float actualEndTime = (loopEndTime > 0f) ? loopEndTime : audioSource.clip.length;

        // 지정된 종료 시간을 넘었거나, 곡이 끝났을 경우 루프 시작 지점으로 이동
        if (audioSource.time >= actualEndTime || !audioSource.isPlaying)
        {
            audioSource.time = loopStartTime;
            if (!audioSource.isPlaying) audioSource.Play();
        }
    }
}
